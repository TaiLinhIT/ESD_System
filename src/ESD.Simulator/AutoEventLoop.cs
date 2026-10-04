using ESD.Core;

namespace ESD.Simulator;

/// <summary>
/// Runs automatic event generation in the background.
/// Simulates realistic factory floor behavior:
///   - Workers arrive and leave stations
///   - Wrist straps connect / disconnect
///   - Occasional NG / alarm events
/// </summary>
internal sealed class AutoEventLoop
{
    private readonly HardwareSimulator _sim;
    private readonly int               _intervalMs;
    private CancellationTokenSource?   _cts;
    private Task?                      _loop;

    private int  _counter;
    private byte _currentEmployee = 1;

    public bool IsRunning => _loop is { IsCompleted: false };

    public AutoEventLoop(HardwareSimulator sim, int intervalMs = 2000)
    {
        _sim        = sim;
        _intervalMs = intervalMs;
    }

    public void Start()
    {
        _cts  = new CancellationTokenSource();
        _loop = RunAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        // First frame: device connected
        _sim.PushEvent(EsdEventType.DeviceConnected, 0, WristStrapStatus.NotConnected);
        await Task.Delay(500, ct);

        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(_intervalMs, ct);
            _counter++;

            // Simulate a typical shift cycle every 8 ticks
            var phase = _counter % 8;

            switch (phase)
            {
                case 0: // worker arrives
                    _currentEmployee = (byte)((_currentEmployee % 10) + 1);
                    _sim.PushEvent(EsdEventType.WorkerDetected,
                        _currentEmployee, WristStrapStatus.NotConnected);
                    break;

                case 1: // connects wrist strap — OK
                    _sim.PushEvent(EsdEventType.WristStrapConnected,
                        _currentEmployee, WristStrapStatus.Ok);
                    break;

                case 2: // ESD test pass
                    _sim.PushEvent(EsdEventType.EsdTestPass,
                        _currentEmployee, WristStrapStatus.Ok);
                    break;

                case 4: // occasional NG (every 4th worker)
                    if (_currentEmployee % 4 == 0)
                    {
                        _sim.PushEvent(EsdEventType.EsdTestFail,
                            _currentEmployee, WristStrapStatus.Ng);
                        await Task.Delay(300, ct);
                        _sim.PushAlarm($"STRAP_NG EMP{_currentEmployee:0000}");
                    }
                    else
                    {
                        _sim.PushEvent(EsdEventType.EsdTestPass,
                            _currentEmployee, WristStrapStatus.Ok);
                    }
                    break;

                case 6: // strap disconnect
                    _sim.PushEvent(EsdEventType.WristStrapDisconnected,
                        _currentEmployee, WristStrapStatus.NotConnected);
                    break;

                case 7: // worker leaves
                    _sim.PushEvent(EsdEventType.WorkerRemoved,
                        _currentEmployee, WristStrapStatus.NotConnected);
                    break;
            }
        }
    }
}
