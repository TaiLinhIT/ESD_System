using ESD.Core;

namespace ESD.Device;

public sealed class MockDevice : IDevice
{
    private readonly DeviceConfig _cfg;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private int _counter;

    public string Name => _cfg.Name;
    public bool IsConnected => _cts is not null;
    public event EventHandler<DeviceMessage>? DataReceived;

    public MockDevice(DeviceConfig cfg) => _cfg = cfg;

    public Task OpenAsync(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _loop = Task.Run(async () =>
        {
            DataReceived?.Invoke(this, new DeviceMessage(DateTime.Now, Name, "CONNECT"u8.ToArray(), "CONNECT"));
            while (!_cts.IsCancellationRequested)
            {
                await Task.Delay(_cfg.PollIntervalMs, _cts.Token);
                _counter++;
                var type = _counter % 2 == 0 ? "REMOVE" : "INSTALL";
                var employee = $"EMP{1000 + (_counter % 5):0000}";
                var text = $"{type}:{employee}";
                var bytes = System.Text.Encoding.ASCII.GetBytes(text);
                DataReceived?.Invoke(this, new DeviceMessage(DateTime.Now, Name, bytes, text));
            }
        }, _cts.Token);
        return Task.CompletedTask;
    }

    public Task SendAsync(byte[] data, CancellationToken ct)
    {
        var text = System.Text.Encoding.ASCII.GetString(data);
        DataReceived?.Invoke(this, new DeviceMessage(DateTime.Now, Name, data, $"ECHO:{text}"));
        return Task.CompletedTask;
    }

    public Task CloseAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() { CloseAsync(); return ValueTask.CompletedTask; }
}
