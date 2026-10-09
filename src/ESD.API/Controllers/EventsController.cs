using System.Text.Json;
using System.Threading.Channels;
using ESD.API.Dto;
using ESD.API.Services;
using ESD.Core;
using ESD.Data;
using ESD.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ESD.API.Controllers;

/// <summary>
/// Event history (from the database) and the live
/// Server-Sent-Events stream (from the device pipeline).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly EsdEventQuery _query;
    private readonly SseEventBus   _bus;
    private readonly ApiSettings   _settings;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public EventsController(EsdEventQuery query, SseEventBus bus, IOptions<ApiSettings> settings)
    {
        _query    = query;
        _bus      = bus;
        _settings = settings.Value;
    }

    /// <summary>Paged, filtered event history.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponseDto<EventDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvents(
        [FromQuery] string? device,
        [FromQuery] string? employee,
        [FromQuery] string? eventType,
        [FromQuery] string? status,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var filter = new EventQueryFilter
        {
            DeviceName = device,
            EmployeeId = employee,
            EventType  = eventType,
            Status     = status,
            From       = from,
            To         = to,
            Page       = Math.Max(1, page),
            PageSize   = Math.Clamp(pageSize, 1, _settings.MaxPageSize),
        };

        var result = await _query.SearchAsync(filter, ct);

        var response = new PagedResponseDto<EventDto>(
            Items:      result.Items.Select(Map).ToList(),
            TotalCount: result.TotalCount,
            Page:       result.Page,
            PageSize:   result.PageSize,
            TotalPages: result.TotalPages,
            HasNextPage: result.HasNextPage,
            HasPrevPage: result.HasPrevPage);

        return Ok(response);
    }

    /// <summary>Distinct filter values for the search screen.</summary>
    [HttpGet("filters")]
    [ProducesResponseType(typeof(EventFilterValues), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFilters(CancellationToken ct = default)
        => Ok(await _query.GetFilterValuesAsync(ct));

    /// <summary>
    /// Server-Sent Events stream of device events in real time.
    /// Events are pushed the instant a device frame is decoded —
    /// they do NOT wait for the database writer.
    /// </summary>
    [HttpGet("live")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task Live(CancellationToken ct)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no";

        using var subscription = _bus.Subscribe();
        var heartbeat = TimeSpan.FromSeconds(_settings.SSEHeartbeatSeconds);

        // Notify the client that the stream is open
        await WriteCommentAsync("connected").ConfigureAwait(false);
        await Response.Body.FlushAsync(ct).ConfigureAwait(false);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Wait for an event or the heartbeat timeout
                var evt = await WaitForEventAsync(subscription.Reader, heartbeat, ct)
                          .ConfigureAwait(false);

                if (evt is not null)
                    await WriteEventAsync(evt).ConfigureAwait(false);
                else
                    await WriteCommentAsync("heartbeat").ConfigureAwait(false);

                await Response.Body.FlushAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { /* client disconnected */ }
        catch (Exception ex) when (!HttpResponseException(ex))
        {
            // Stream broken — client is gone
        }
    }

    private static async Task<EsdEvent?> WaitForEventAsync(
        ChannelReader<EsdEvent> reader,
        TimeSpan timeout,
        CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        try
        {
            return await reader.ReadAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null; // heartbeat timeout
        }
    }

    private async Task WriteEventAsync(EsdEvent evt)
    {
        var dto = new
        {
            id        = 0, // live events are not yet persisted
            timestamp = evt.Timestamp,
            device    = evt.DeviceName,
            employee  = evt.EmployeeId,
            eventType = evt.EventType.ToString(),
            status    = evt.StrapStatus.ToString(),
            rawData   = evt.RawHex,
            message   = evt.Message,
        };

        await Response.WriteAsync($"event: esd\ndata: {JsonSerializer.Serialize(dto, JsonOpts)}\n\n", default)
            .ConfigureAwait(false);
    }

    private async Task WriteCommentAsync(string text)
        => await Response.WriteAsync($": {text}\n\n", default).ConfigureAwait(false);

    private static EventDto Map(EsdEventEntity e) => new(
        e.Id, e.EventTime, e.DeviceName, e.EmployeeId, e.EventType, e.Status, e.RawData, e.Message);

    private static bool HttpResponseException(Exception ex)
        => ex is InvalidOperationException { Message: "Client disconnected" };
}
