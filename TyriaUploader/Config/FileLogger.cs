namespace TyriaUploader.Config;

public sealed class FileLogger
{
    private readonly string _path;
    private readonly object _lock = new();
    private const long MaxBytes = 5L * 1024 * 1024;

    public FileLogger(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    }

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex == null ? message : $"{message}: {ex}");

    private void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
        lock (_lock)
        {
            try
            {
                if (File.Exists(_path) && new FileInfo(_path).Length > MaxBytes)
                {
                    var rotated = _path + ".old";
                    if (File.Exists(rotated)) File.Delete(rotated);
                    File.Move(_path, rotated);
                }
                File.AppendAllText(_path, line + Environment.NewLine);
            }
            catch
            {

            }
        }
    }
}
