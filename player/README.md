# FPlayer (Media Player)

Native Windows video player built with **WinUI 3** (.NET 8) and **LibVLC**. Supports MP4, MKV, AVI, WebM, MOV, FLV, WMV, M2TS, ASF, RMVB, MXF, VOB, HEVC, AV1, and other containers LibVLC can decode — no codec packs required. **Images are not supported** (JPG, PNG, GIF, WebP, HEIC, etc.).

**Version:** defined in [`Directory.Build.props`](Directory.Build.props) (currently **1.0.0**). Shown in the window title.

## Download a ready-made build

You do not need to build from source — download the ZIP from [GitHub Releases](https://github.com/Fenoty/MediaUtilities/releases):

| Step | Action |
|------|--------|
| 1 | [Releases](https://github.com/Fenoty/MediaUtilities/releases) → latest `player-v*` release |
| 2 | Download **`FPlayer-1.0.0-win-x64.zip`** (name depends on version) |
| 3 | Extract and run **`MediaPlayer.exe`** |

The build is self-contained: **no .NET Runtime install required**. Requires Windows 10 19041+ / Windows 11.

## Requirements (development)

- Windows 10 19041+ / Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows App SDK (via NuGet)

## Run

**From the `player/` folder (recommended):**

```bat
run.bat
```

**Or with dotnet:**

```bash
cd player
dotnet run --project MediaPlayer/MediaPlayer.csproj -c Debug
```

## Build

```bash
cd player
dotnet build MediaPlayer.sln -c Release -p:Platform=x64
```

Debug executable: `MediaPlayer/bin/Debug/net8.0-windows10.0.19041.0/win-x64/MediaPlayer.exe`

### Release ZIP (local)

```powershell
cd player
.\scripts\package-release.ps1
```

Output: `player/artifacts/FPlayer-<version>-win-x64.zip` and an unpacked folder next to it.

To publish on GitHub, create a tag `player-v<version>` (from `Directory.Build.props`) and push — the workflow builds the ZIP and attaches it to the Release.

```bash
git tag player-v1.0.0
git push origin player-v1.0.0
```

> **Note:** do not run the exe from the `player/` folder directly — that path is wrong. Use `run.bat` or the full path above.

## Features

- **Mica** — system translucent window background (Windows 11)
- Dark Fluent theme out of the box
- LibVLCSharp.WinUI — broad format support, HW decode (D3D11)
- Open files (Ctrl+O) and drag-and-drop — any media except images; file dialog includes **All files (*.*)**
- Play / Pause, ±10 sec, playlist with thumbnails
- Click video to play / pause
- Subtitles (.srt, .ass, .vtt)
- Full screen (F / Esc)
- **Video layout** — fit, fill, or stretch (**View** menu, toolbar button)
- Settings stored in `%AppData%/MediaUtilities/player/`

## Video layout

| Mode | Description |
|------|-------------|
| **Fit to screen** | Preserves aspect ratio; letterboxing possible (default) |
| **Fill screen** | No bars; edges may be cropped |
| **Stretch to full screen** | Fills the area; may distort |

Where to change it:
- **View** menu → bottom items
- Frame icon on the control bar (left of full screen)
- On narrow windows: **⋯** overflow menu → same items

Choice is persisted between sessions.

## Keyboard shortcuts

| Key | Action |
|-----|--------|
| Space | Play / Pause |
| F | Full screen |
| Esc | Exit full screen |
| ← / → | −10s / +10s |
| ↑ / ↓ | Volume ±5% |
| M | Mute |
| Ctrl+O | Open file |
| Ctrl+L | Load subtitles |
| N / P | Next / previous |

## Stack

- WinUI 3 + Windows App SDK 2.2
- LibVLCSharp.WinUI + VideoLAN.LibVLC.Windows
- CommunityToolkit.Mvvm
