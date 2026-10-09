using System.Net;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("ESD.Api", client =>
{
    var baseUrl = builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5080";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

const string SPA_FALLBACK = "/index.html";

// 1) Reverse proxy: /api/* → ESD.API (avoids CORS in production,
//    keeps the whole UI on a single origin).
app.UseMiddleware<ApiProxyMiddleware>();

// 2) Static files (css / js / images) with aggressive caching.
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        const int days = 7;
        ctx.Context.Response.Headers.CacheControl =
            $"public, max-age={days * 24 * 60 * 60}";
    },
});

// 3) SPA fallback: extensionless GET paths serve index.html
//    (hash router keeps deep-links working without server routes).
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value ?? string.Empty;
    if (ctx.Request.Method == "GET"
        && !Path.HasExtension(path)
        && !path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
    {
        ctx.Request.Path = SPA_FALLBACK;
    }
    await next(ctx);
});

app.Run();

/// <summary>
/// Forwards /api requests to the configured ESD.API instance and
/// streams the response back (SSE-friendly: headers are read early
/// and the body is copied chunk-by-chunk).
/// </summary>
public sealed class ApiProxyMiddleware
{
    private readonly RequestDelegate          _next;
    private readonly IHttpClientFactory     _factory;
    private readonly IConfiguration         _config;
    private readonly ILogger<ApiProxyMiddleware> _logger;

    public ApiProxyMiddleware(
        RequestDelegate next,
        IHttpClientFactory factory,
        IConfiguration config,
        ILogger<ApiProxyMiddleware> logger)
    {
        _next    = next;
        _factory = factory;
        _config  = config;
        _logger  = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (!ctx.Request.Path.StartsWithSegments("/api", out var remaining))
        {
            await _next(ctx);
            return;
        }

        var baseUrl = (_config["Api:BaseUrl"] ?? "http://localhost:5080").TrimEnd('/');
        var target  = baseUrl + remaining + ctx.Request.QueryString;

        var client = _factory.CreateClient("ESD.Api");
        using var request = new HttpRequestMessage(new HttpMethod(ctx.Request.Method), target);

        CopyRequestHeaders(ctx, request);

        if (ctx.Request.ContentLength is > 0)
            request.Content = new StreamContent(ctx.Request.Body);

        try
        {
            using var response = await client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, ctx.RequestAborted);

            ctx.Response.StatusCode = (int)response.StatusCode;
            CopyResponseHeaders(response, ctx);

            await response.Content.CopyToAsync(ctx.Response.Body, ctx.RequestAborted);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "API proxy failed: {Target}", target);
            ctx.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsJsonAsync(new
            {
                error  = "ESD.API is unreachable.",
                target = baseUrl,
                hint   = "Start the ESD.API project, or set Api:BaseUrl in appsettings.json."
            }, ctx.RequestAborted);
        }
        catch (OperationCanceledException) { /* client went away */ }
    }

    private static void CopyRequestHeaders(HttpContext ctx, HttpRequestMessage request)
    {
        foreach (var header in ctx.Request.Headers)
        {
            if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
                request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }
    }

    private static void CopyResponseHeaders(HttpResponseMessage response, HttpContext ctx)
    {
        foreach (var header in response.Headers)
            ctx.Response.Headers[header.Key] = header.Value.ToArray();

        foreach (var header in response.Content.Headers)
            ctx.Response.Headers[header.Key] = header.Value.ToArray();

        ctx.Response.Headers.Remove("transfer-encoding");
    }
}
