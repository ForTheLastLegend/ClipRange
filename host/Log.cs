namespace ClipRange.Host;

static class Log
{
    const long MaxBytes = 1024 * 1024;
    static readonly string FilePath = Path.Combine(Settings.AppDir, "host.log");

    public static void Open()
    {
        Directory.CreateDirectory(Settings.AppDir);
        if (File.Exists(FilePath) && new FileInfo(FilePath).Length > MaxBytes)
            File.Delete(FilePath);
    }

    public static void Write(string line) =>
        File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {line}{Environment.NewLine}");
}
