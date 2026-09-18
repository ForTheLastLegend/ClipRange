using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClipRange.Host;

sealed partial class Settings
{
    public static readonly string AppDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipRange");

    static readonly string FilePath = Path.Combine(AppDir, "settings.json");

    public string? YtDlpPath { get; set; }
    public string? FfmpegPath { get; set; }
    public string OutputDir { get; set; } = DownloadsFolder();
    public string Preset { get; set; } = "standard";
    public GifQuality Custom { get; set; } = GifPreset.Find("standard")!.Quality;

    [JsonIgnore]
    public GifQuality Quality => Preset == GifPreset.Custom ? Custom : GifPreset.Find(Preset)!.Quality;

    public static Settings Load()
    {
        if (!File.Exists(FilePath))
            return new Settings();
        return JsonSerializer.Deserialize(File.ReadAllBytes(FilePath), Json.Default.Settings) ?? new Settings();
    }

    public void Save()
    {
        Directory.CreateDirectory(AppDir);
        File.WriteAllBytes(FilePath, JsonSerializer.SerializeToUtf8Bytes(this, Json.Default.Settings));
    }

    public void Apply(SettingsPatch patch)
    {
        YtDlpPath = PathOrAuto(patch.YtDlpPath, YtDlpPath);
        FfmpegPath = PathOrAuto(patch.FfmpegPath, FfmpegPath);
        OutputDir = patch.OutputDir ?? OutputDir;
        Preset = patch.Preset ?? Preset;
        Custom = patch.Custom ?? Custom;
    }

    // An empty string clears a configured path so the tool is auto-detected again.
    static string? PathOrAuto(string? patch, string? current) => patch switch
    {
        null => current,
        "" => null,
        _ => patch,
    };

    // Environment.SpecialFolder has no entry for Downloads.
    static string DownloadsFolder()
    {
        var downloads = new Guid("374DE290-123F-4565-9164-39C4925E467B");
        SHGetKnownFolderPath(in downloads, 0, IntPtr.Zero, out var path);
        return path;
    }

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int SHGetKnownFolderPath(in Guid folderId, uint flags, IntPtr token, out string path);
}
