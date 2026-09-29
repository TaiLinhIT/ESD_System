namespace ESD.Service;

public sealed class FileLogger
{
    private readonly string _directory;
    private readonly object _lock = new();

    public FileLogger(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(_directory);
    }

    public void Info(string message) => Write("INFO", message);
    public void Error(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
        lock (_lock)
            File.AppendAllText(Path.Combine(_directory, $"{DateTime.Now:yyyy-MM-dd}.log"), line);
    }
}
