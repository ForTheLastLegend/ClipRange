using System.Globalization;
using System.Text.RegularExpressions;

namespace ClipRange.Host;

sealed record GifSpec(string Url, double Start, double End, GifQuality Quality)
{
    public double Duration => End - Start;
}

sealed class JobFailed(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

static partial class GifJob
{
    static readonly int[] SourceHeights = [144, 240, 360, 480, 720, 1080, 1440, 2160];

    public static async Task<string> Run(GifSpec spec, ToolPaths tools, string outputDir, Action<string, int?> report)
    {
        Directory.CreateDirectory(outputDir);
        var workDir = Path.Combine(Path.GetTempPath(), "ClipRange", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        try
        {
            var (clipPath, title) = await Download(spec, tools, workDir, report);
            var gifPath = Path.Combine(outputDir, OutputName(title, spec));
            await Encode(clipPath, gifPath, spec, tools.Ffmpeg, report);
            return gifPath;
        }
        finally
        {
            Directory.Delete(workDir, recursive: true);
        }
    }

    static async Task<(string ClipPath, string Title)> Download(GifSpec spec, ToolPaths tools, string workDir, Action<string, int?> report)
    {
        report("download", null);
        var printed = new List<string>();
        var inv = CultureInfo.InvariantCulture;

        var result = await Exec.Run(tools.YtDlp, [
            "--no-playlist",
            "--ffmpeg-location", tools.Ffmpeg,
            "--download-sections", string.Create(inv, $"*{spec.Start:0.###}-{spec.End:0.###}"),
            "--force-keyframes-at-cuts",
            "--format", $"bv*[height<=?{SourceHeightFor(spec.Quality.Width)}]/bv*",
            "--output", Path.Combine(workDir, "clip.%(ext)s"),
            "--print", "after_move:filepath",
            "--print", "after_move:title",
            "--newline", "--progress",
            spec.Url,
        ], line =>
        {
            if (DownloadPercent().Match(line) is { Success: true } m)
                report("download", (int)double.Parse(m.Groups[1].Value, inv));
            else if (!line.StartsWith('['))
                printed.Add(line);
        });

        if (result.ExitCode != 0 || printed.Count < 2)
            throw new JobFailed("yt-dlp", result.StderrTail is "" ? "yt-dlp failed without an error message" : result.StderrTail);

        return (printed[^2], printed[^1]);
    }

    static async Task Encode(string clipPath, string gifPath, GifSpec spec, string ffmpeg, Action<string, int?> report)
    {
        report("encode", 0);
        var q = spec.Quality;
        var filters =
            $"fps={q.Fps},scale={q.Width}:-2:flags=lanczos,split[a][b];" +
            $"[a]palettegen=max_colors={q.Colors}:stats_mode={(q.PalettePerFrame ? "single" : "diff")}[p];" +
            $"[b][p]paletteuse=new={(q.PalettePerFrame ? 1 : 0)}:dither={DitherArgs(q.Dither)}:diff_mode=rectangle";

        var result = await Exec.Run(ffmpeg, [
            "-y", "-hide_banner", "-loglevel", "error", "-nostats",
            "-progress", "pipe:1",
            "-i", clipPath,
            "-filter_complex", filters,
            "-loop", "0",
            gifPath,
        ], line =>
        {
            if (line.StartsWith("out_time_us=") && long.TryParse(line.AsSpan(12), out var us))
                report("encode", (int)Math.Min(100, us / 10_000 / spec.Duration));
        });

        if (result.ExitCode != 0)
            throw new JobFailed("ffmpeg", result.StderrTail);
    }

    static string DitherArgs(Dither dither) => dither switch
    {
        Dither.None => "none",
        Dither.BayerLight => "bayer:bayer_scale=3",
        Dither.Bayer => "bayer:bayer_scale=5",
        Dither.Sierra => "sierra2_4a",
        _ => throw new ArgumentOutOfRangeException(nameof(dither)),
    };

    static int SourceHeightFor(int width)
    {
        var needed = width * 9 / 16;
        return SourceHeights.FirstOrDefault(h => h >= needed, SourceHeights[^1]);
    }

    static string OutputName(string title, GifSpec spec)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safeTitle = new string(title.Where(c => !invalid.Contains(c)).ToArray()).Trim();
        if (safeTitle.Length > 80)
            safeTitle = safeTitle[..80].TrimEnd();
        return $"{safeTitle} {Stamp(spec.Start)}-{Stamp(spec.End)} [ClipRange].gif";
    }

    static string Stamp(double seconds) => TimeSpan.FromSeconds(seconds).ToString(@"mm\.ss");

    [GeneratedRegex(@"^\[download\]\s+([\d.]+)%")]
    private static partial Regex DownloadPercent();
}
