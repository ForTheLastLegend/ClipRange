using System.Text.Json.Serialization;

namespace ClipRange.Host;

[JsonConverter(typeof(JsonStringEnumConverter<Tool>))]
enum Tool
{
    [JsonStringEnumMemberName("yt-dlp")] YtDlp,
    [JsonStringEnumMemberName("ffmpeg")] Ffmpeg,
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(StatusRequest), "status")]
[JsonDerivedType(typeof(InstallToolRequest), "install-tool")]
[JsonDerivedType(typeof(SetSettingsRequest), "set-settings")]
[JsonDerivedType(typeof(MakeGifRequest), "make-gif")]
[JsonDerivedType(typeof(OpenRequest), "open")]
abstract record Request;

sealed record StatusRequest : Request;
sealed record InstallToolRequest(Tool Tool) : Request;
sealed record SetSettingsRequest(SettingsPatch Settings) : Request;
sealed record MakeGifRequest(string Url, double Start, double End) : Request;
sealed record OpenRequest(string Path, bool Folder) : Request;

sealed record SettingsPatch(string? YtDlpPath, string? FfmpegPath, string? OutputDir, string? Preset, GifQuality? Custom);

[JsonConverter(typeof(JsonStringEnumConverter<Dither>))]
enum Dither
{
    [JsonStringEnumMemberName("none")] None,
    [JsonStringEnumMemberName("bayer-light")] BayerLight,
    [JsonStringEnumMemberName("bayer")] Bayer,
    [JsonStringEnumMemberName("sierra")] Sierra,
}

// PalettePerFrame trades file size for fidelity on clips whose colors change a lot.
sealed record GifQuality(int Width, int Fps, int Colors, bool PalettePerFrame, Dither Dither);

sealed record GifPreset(string Id, GifQuality Quality)
{
    public const string Custom = "custom";

    public static readonly GifPreset[] All =
    [
        new("light", new(Width: 320, Fps: 10, Colors: 128, PalettePerFrame: false, Dither.BayerLight)),
        new("standard", new(Width: 480, Fps: 15, Colors: 256, PalettePerFrame: false, Dither.Bayer)),
        new("high", new(Width: 640, Fps: 20, Colors: 256, PalettePerFrame: true, Dither.Sierra)),
    ];

    public static GifPreset? Find(string id) => Array.Find(All, p => p.Id == id);
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(StatusReply), "status")]
[JsonDerivedType(typeof(ProgressReply), "progress")]
[JsonDerivedType(typeof(DoneReply), "done")]
[JsonDerivedType(typeof(ErrorReply), "error")]
abstract record Reply;

sealed record ToolStatus(string Path, string? Version);
sealed record StatusReply(ToolStatus? YtDlp, ToolStatus? Ffmpeg, Settings Settings, GifQuality Quality, GifPreset[] Presets, GifLimits Limits) : Reply;
sealed record ProgressReply(string Stage, int? Percent) : Reply;
sealed record DoneReply(string Path) : Reply;
sealed record ErrorReply(string Code, string Message) : Reply;

sealed record GifLimits(int MinWidth, int MaxWidth, int MinFps, int MaxFps, int MaxSeconds)
{
    public static readonly GifLimits Default = new(MinWidth: 100, MaxWidth: 1920, MinFps: 5, MaxFps: 50, MaxSeconds: 180);
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Request))]
[JsonSerializable(typeof(Reply))]
[JsonSerializable(typeof(Settings))]
partial class Json : JsonSerializerContext;
