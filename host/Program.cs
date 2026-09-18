using System.Diagnostics;
using ClipRange.Host;

Log.Open();
var settings = Settings.Load();
var port = new NativePort(Console.OpenStandardInput(), Console.OpenStandardOutput());

while (port.Read() is { } request)
{
    try
    {
        await Handle(request);
    }
    catch (JobFailed e)
    {
        port.Write(new ErrorReply(e.Code, e.Message));
    }
    catch (Exception e)
    {
        Log.Write(e.ToString());
        port.Write(new ErrorReply("internal", e.Message));
    }
}

async Task Handle(Request request)
{
    switch (request)
    {
        case StatusRequest:
            port.Write(await Status());
            break;

        case SetSettingsRequest r:
            settings.Apply(Validated(r.Settings));
            settings.Save();
            port.Write(await Status());
            break;

        case InstallToolRequest r:
            using (var heartbeat = new Heartbeat(port, "download"))
                await Tools.Install(r.Tool, percent => heartbeat.Report("download", percent));
            port.Write(await Status());
            break;

        case MakeGifRequest r:
            var tools = Tools.Resolve(settings)
                ?? throw new JobFailed("tool-missing", "yt-dlp or ffmpeg is not installed. Open the extension options to install them.");
            var spec = ToSpec(r);
            string gifPath;
            using (var heartbeat = new Heartbeat(port, "download"))
                gifPath = await GifJob.Run(spec, tools, settings.OutputDir, heartbeat.Report);
            port.Write(new DoneReply(gifPath));
            break;

        case OpenRequest r:
            var isGif = Path.GetExtension(r.Path).Equals(".gif", StringComparison.OrdinalIgnoreCase);
            if (isGif && File.Exists(r.Path))
            {
                if (r.Folder)
                    Process.Start("explorer.exe", $"/select,\"{r.Path}\"");
                else
                    Process.Start(new ProcessStartInfo(r.Path) { UseShellExecute = true });
            }
            port.Write(new DoneReply(r.Path));
            break;
    }
}

async Task<StatusReply> Status() =>
    new(await Tools.Status(Tool.YtDlp, settings), await Tools.Status(Tool.Ffmpeg, settings),
        settings, settings.Quality, GifPreset.All, GifLimits.Default);

GifSpec ToSpec(MakeGifRequest r)
{
    if (r.End <= r.Start)
        throw new JobFailed("range", "End must be after start.");
    if (r.End - r.Start > GifLimits.Default.MaxSeconds)
        throw new JobFailed("range", $"Clips are limited to {GifLimits.Default.MaxSeconds} seconds.");

    return new GifSpec(r.Url, r.Start, r.End, settings.Quality);
}

static SettingsPatch Validated(SettingsPatch patch)
{
    foreach (var path in new[] { patch.YtDlpPath, patch.FfmpegPath })
        if (!string.IsNullOrEmpty(path) && !File.Exists(path))
            throw new JobFailed("path", $"File not found: {path}");
    if (patch.OutputDir != null && !Directory.Exists(patch.OutputDir))
        throw new JobFailed("path", $"Folder not found: {patch.OutputDir}");
    if (patch.Preset != null && patch.Preset != GifPreset.Custom && GifPreset.Find(patch.Preset) == null)
        throw new JobFailed("preset", $"Unknown preset: {patch.Preset}");

    if (patch.Custom == null)
        return patch;

    var limits = GifLimits.Default;
    var custom = patch.Custom with
    {
        Width = Math.Clamp(patch.Custom.Width, limits.MinWidth, limits.MaxWidth),
        Fps = Math.Clamp(patch.Custom.Fps, limits.MinFps, limits.MaxFps),
        Colors = Math.Clamp(patch.Custom.Colors, 2, 256),
    };
    return patch with { Custom = custom };
}
