# ClipRange

*[Version francaise](README.fr.md)*

Turn a part of a YouTube video into a GIF from Chrome or Brave, without downloading the whole video.

Two buttons show up in the player bar: `Start` and `End` capture the current time, `GIF` starts the job.
The file lands in your Downloads folder and a Windows notification lets you open it. Three quality presets,
or your own settings, in the extension options.

Windows only. The interface is in English, or in French if your browser is.

> This tool goes against YouTube's terms of service and cannot be published on the Chrome Web Store.
> It is shared here for personal use, to be loaded in developer mode.
>
> You use it at your own risk and under your own responsibility. Nothing here is meant to be harmful,
> but the software comes with no warranty of any kind, and the author cannot be held liable for
> anything that results from installing or using it.

## Install

1. Download `ClipRange-Setup-x.y.z.exe` from the [latest release](../../releases/latest) and run it.
   No admin rights needed.
2. In Chrome or Brave, open `chrome://extensions` (or `brave://extensions`), turn on **Developer mode**,
   click **Load unpacked** and pick the `extension` folder inside `%LOCALAPPDATA%\Programs\ClipRange`.
3. The extension options page opens: click **Download** for yt-dlp and for ffmpeg.
   If you already have them, enter their path instead.

Then open any YouTube video. The browser will warn about developer mode extensions at every start,
that is the price of an extension outside the store.

yt-dlp has to be updated regularly to keep up with YouTube: **Update** button in the options
(click the ClipRange icon in the toolbar to open them).

## How it works

```
extension/   Chrome MV3 extension (content script, service worker, options page)
host/        Native Messaging host in C# (.NET 9, NativeAOT): yt-dlp then ffmpeg
installer/   Inno Setup script that installs the host, the extension and the registry key
```

The extension talks to the host through `chrome.runtime.connectNative`. The browser starts the host for
each request and it exits when the port closes. During a job it sends a progress message at least every
10 seconds so the browser does not unload the service worker.

Pipeline: `yt-dlp --download-sections` only fetches the requested range (exact cut thanks to
`--force-keyframes-at-cuts`, resolution capped by the GIF width), then ffmpeg encodes in one pass with
`palettegen` and `paletteuse`. Presets live in `host/Messages.cs`.

Brave reads the same registry key as Chrome (`HKCU\Software\Google\Chrome\NativeMessagingHosts`).
Edge uses `HKCU\Software\Microsoft\Edge\NativeMessagingHosts`, which the installer does not handle.

Settings: `%LOCALAPPDATA%\ClipRange\settings.json`. Log: `%LOCALAPPDATA%\ClipRange\host.log`
(commands run and error output from yt-dlp and ffmpeg).

## Development

Requirements: .NET 9 SDK and the Visual Studio C++ tools (NativeAOT). Build the host from PowerShell or
cmd, not Git Bash (the AOT toolchain looks for `vswhere.exe` through `%ProgramFiles(x86)%`):

```
dotnet publish host -c Release
```

To test without the installer: copy `host/com.cliprange.host.json` next to the published executable,
then register that manifest in the registry, with your own path:

```
reg add "HKCU\Software\Google\Chrome\NativeMessagingHosts\com.cliprange.host" /ve /d "<path>\publish\com.cliprange.host.json" /f
```

Load `extension/` in developer mode. The `key` field in the manifest pins the extension id to
`jndjblelcmehihkifdeblpobkadfkfbn`, the only one allowed by the native manifest. After changing the
extension: "Reload" on the extensions page, then refresh the YouTube page.

Installer: [Inno Setup 6](https://jrsoftware.org/isinfo.php), then `iscc installer\cliprange.iss`.

Release: push a `vX.Y.Z` tag after aligning the version in `extension/manifest.json`,
`host/ClipRange.Host.csproj` and `installer/cliprange.iss`. The GitHub workflow builds the host and the
installer, then publishes the release with the installer and a zip of the extension.

Host protocol: JSON messages typed by a `type` field. Requests `status`, `install-tool`, `set-settings`,
`make-gif`, `open`; replies `status`, `progress`, `done`, `error`. See `host/Messages.cs`.
