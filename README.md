# FFMpegWrap

`FFMpegWrap` is a WPF desktop app for working with local media files and online downloads through `ffmpeg`, `ffplay`, `ffprobe`, and `yt-dlp`.

## Features

- local media conversion workflows
- metadata inspection through `ffprobe`
- embedded media preview with transport controls
- audio spectrum preview for audio-only files
- `yt-dlp` format discovery and downloads
- configurable executable paths for external tools
- light, dark, and system theme support

## Requirements

- Windows
- .NET 10 SDK
- `ffmpeg.exe`
- `ffplay.exe`
- `ffprobe.exe`
- `yt-dlp.exe`

The app can discover tools from configured paths, common local folders, or `PATH`.

## Build

```powershell
dotnet build .\FFMpegWrap.slnx
```

## Run

```powershell
dotnet run --project .\FFMpegWrap\FFMpegWrap.csproj
```

## Release

- the GitHub Actions workflow at `.github/workflows/publish-release.yml` publishes a self-contained `win-x64` single-file executable
- push a tag such as `v1.0.0` to build the app and create a GitHub release with the published `.exe` attached
- run the workflow manually from the Actions tab to produce the same `.exe` as a downloadable workflow artifact

## First-time setup

1. Launch the app.
2. Open the `Tools / Paths` tab.
3. Configure paths for `ffmpeg`, `ffplay`, `ffprobe`, and `yt-dlp` if they are not already available on `PATH`.
4. Save the configuration.

## Notes

- local conversions and online downloads default to the Desktop output folder
- preview generation uses temporary cached preview media under the system temp directory
- persisted tool path and theme settings are stored under the current user's local app data folder
