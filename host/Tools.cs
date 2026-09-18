using System.IO.Compression;

namespace ClipRange.Host;

sealed record ToolPaths(string YtDlp, string Ffmpeg);

static class Tools
{
    static readonly string BinDir = Path.Combine(Settings.AppDir, "bin");

    static readonly Uri YtDlpRelease = new("https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe");
    static readonly Uri FfmpegRelease = new("https://github.com/BtbN/FFmpeg-Builds/releases/latest/download/ffmpeg-master-latest-win64-gpl.zip");

    public static string FileName(Tool tool) => tool == Tool.YtDlp ? "yt-dlp.exe" : "ffmpeg.exe";

    public static string? Locate(Tool tool, Settings settings)
    {
        var configured = tool == Tool.YtDlp ? settings.YtDlpPath : settings.FfmpegPath;
        if (configured != null)
            return File.Exists(configured) ? configured : null;

        var bundled = Path.Combine(BinDir, FileName(tool));
        if (File.Exists(bundled))
            return bundled;

        var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(';') ?? [];
        return pathDirs.Select(dir => Path.Combine(dir, FileName(tool))).FirstOrDefault(File.Exists);
    }

    public static async Task<ToolStatus?> Status(Tool tool, Settings settings)
    {
        var path = Locate(tool, settings);
        if (path == null)
            return null;

        var version = tool switch
        {
            Tool.YtDlp => await Exec.FirstOutputLine(path, "--version"),
            _ => (await Exec.FirstOutputLine(path, "-version"))?.Split(' ').ElementAtOrDefault(2),
        };
        return new ToolStatus(path, version);
    }

    public static ToolPaths? Resolve(Settings settings)
    {
        var ytDlp = Locate(Tool.YtDlp, settings);
        var ffmpeg = Locate(Tool.Ffmpeg, settings);
        if (ytDlp == null || ffmpeg == null)
            return null;
        return new ToolPaths(ytDlp, ffmpeg);
    }

    public static async Task Install(Tool tool, Action<int?> onPercent)
    {
        Directory.CreateDirectory(BinDir);
        var target = Path.Combine(BinDir, FileName(tool));
        var download = target + ".download";

        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("ClipRange");

        using (var response = await http.GetAsync(tool == Tool.YtDlp ? YtDlpRelease : FfmpegRelease, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            await using var source = await response.Content.ReadAsStreamAsync();
            await using var file = File.Create(download);
            await Copy(source, file, response.Content.Headers.ContentLength, onPercent);
        }

        if (tool == Tool.Ffmpeg)
        {
            ExtractFfmpeg(download, target);
            File.Delete(download);
        }
        else
        {
            File.Move(download, target, overwrite: true);
        }
    }

    static async Task Copy(Stream source, Stream destination, long? total, Action<int?> onPercent)
    {
        var buffer = new byte[64 * 1024];
        long copied = 0;
        int? lastPercent = -1;

        int read;
        while ((read = await source.ReadAsync(buffer)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read));
            copied += read;

            int? percent = total > 0 ? (int)(copied * 100 / total) : null;
            if (percent != lastPercent)
            {
                onPercent(percent);
                lastPercent = percent;
            }
        }
    }

    static void ExtractFfmpeg(string zipPath, string target)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        var entry = zip.Entries.First(e => e.Name.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase));
        entry.ExtractToFile(target, overwrite: true);
    }
}
