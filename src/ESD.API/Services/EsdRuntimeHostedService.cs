using ESD.Service;

namespace ESD.API;

/// <summary>
/// Starts/stops the ESD runtime with the host lifetime,
/// so Kestrel, device listeners and the DB worker share
/// the same cancellation token.
/// </summary>
public sealed class EsdRuntimeHostedService : IHostedService
{
    private readonly EsdRuntime _runtime;

    public EsdRuntimeHostedService(EsdRuntime runtime)
        => _runtime = runtime;

    public async Task StartAsync(CancellationToken ct)
        => await _runtime.StartAsync(ct);

    public async Task StopAsync(CancellationToken ct)
        => await _runtime.DisposeAsync();
}
