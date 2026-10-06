using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;

namespace ESD.Service;

public sealed class FileLogger
{
    private readonly string _directory;
    private readonly ConcurrentQueue<(string Level, string Message)>
        _logQueue = new();
    private readonly Channel<(string Level, string Message)> _channel;
    private readonly Task _loggerTask;
    private readonly CancellationTokenSource _cts = new();

    public FileLogger(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(_directory);

        _channel = Channel.CreateBounded<(string Level, string Message)>(new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        _loggerTask = Task.Run(async () => await LogWorkerAsync(_cts.Token));
    }

    public void Info(string message)  => WriteAsync("INFO ", message);
    public void Error(string message) => WriteAsync("ERROR", message);
    public void Warn(string message)  => WriteAsync("WARN ", message);

    /// <summary>Log a raw TX frame with HEX dump.</summary>
    public void Tx(string device, byte[] frame, string decoded = "")
    {
        var hex = Convert.ToHexString(frame);
        WriteAsync("TX   ", $"[{device}] {hex}{(decoded.Length > 0 ? $" | {decoded}" : "")}");
    }

    /// <summary>Log a raw RX frame with HEX dump.</summary>
    public void Rx(string device, byte[] frame, string decoded = "")
    {
        var hex = Convert.ToHexString(frame);
        WriteAsync("RX   ", $"[{device}] {hex}{(decoded.Length > 0 ? $" | {decoded}" : "")}");
    }

    private async ValueTask WriteAsync(string prefix, string message)
    {
        var line = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} [{prefix}] {message}{Environment.NewLine}";
        await _channel.Writer.WriteAsync((prefix, message)).ConfigureAwait(false);
    }

    private async Task LogWorkerAsync(CancellationToken ct)
    {
        var filePath = Path.Combine(_directory, $"{DateTime.Today:yyyy-MM-dd}.log");
        var dir = Path.GetDirectoryName(filePath)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        using var sw = new StreamWriter(filePath, append: true, new UTF8Encoding(false));
        sw.AutoFlush = true;

        await foreach (var (Level, Message) in _channel.Reader.ReadAllAsync(ct))
        {
            var line = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} [{Level}] {Message}{Environment.NewLine}";
            await sw.WriteAsync(line).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _loggerTask.Wait(); } catch { }
        _channel.Writer.Complete();
    }
}
