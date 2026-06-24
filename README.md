# QConvert

Fast image conversion and resizing straight from the Windows Explorer context menu, by [Code-iX](https://github.com/Code-iX).

Right-click an image, open the **QConvert** submenu, pick an action — a converted copy appears next to the original. No window, no wait.

## Features

- **Convert**: PNG, JPG/JPEG, WebP, AVIF, and ICO to supported output formats
- **Resize to fit**: scales down (never up) to fit inside a box, keeping the aspect ratio
- **Crop to size**: scales (up or down) to cover an exact size, then center-crops — the result is exactly that size (e.g. 1920×1080)
- **Crop to aspect ratio**: center-crops to a ratio like 4:3 without any resizing
- **Cleanup and export tools**: remove metadata, compress JPG, optimize PNG, create favicon bundles, create square avatars, and paste clipboard images as PNG/JPG
- Converted copies keep the original name; if it already exists, a numeric suffix is inserted before the extension (`photo.jpg` → `photo.001.jpg`, …). Size operations include the output dimensions in the name (`photo.1920x1080.jpg`)
- The original file is never touched
- Transparent pixels are flattened onto white when writing JPG (instead of turning black)
- Common EXIF orientations are baked in so phone photos stay upright
- Per-user settings: JPEG quality plus the resize/crop entries shown in the menu
- No admin rights required: installs and registers per user

## Installation

### winget

```
winget install Code-iX.QConvert
```

### Manual

Download the MSI from the [latest release](../../releases/latest) and run it. The MSI is self-contained; no separate .NET Desktop Runtime install is required. The Explorer context menu is off by default and can be enabled from QConvert settings.

> **WebP/AVIF note:** opening these formats depends on Windows image codec support. WebP output and AVIF output are written through bundled SkiaSharp support.

> **Windows 11 note:** the QConvert menu appears in the classic context menu, under **Show more options** (Shift+F10).

## Usage

### Context menu

Right-click a `.png`, `.jpg`, `.jpeg`, or `.webp` file and open **QConvert**:

- Format conversion (Convert to JPG / Convert to PNG, depending on the file type)
- below a separator: the resize and crop entries you selected in the settings

### Settings

Launch **QConvert** from the Start Menu (or run `QConvert.exe` without arguments) to:

- set the **JPEG quality** (1–100, default 90)
- pick the **Resize to fit** / **Crop to size** boxes (HD, Full HD, WQHD, 4K, mobile, or any custom `WxH`)
- pick the **Crop to aspect ratio** entries (4:3, 3:2, 16:9, 21:9, 1:1, 9:16, or any custom `X:Y`)

Press **Save & update context menu** — settings are stored per user in `%APPDATA%\QConvert\settings.json` and the context menu is rebuilt.

### Command line

```
QConvert.Cli.exe (--to <jpg|png|ico|webp|avif> | --fit <WxH> | --cover <WxH> | --crop <X:Y> | --strip-metadata | --sepia <0-100> | --compress-jpg | --optimize-png | --favicon | --avatar <size> | --paste <jpg|png>) [--quality <1-100>] [--background <#rrggbb>] <file-or-folder> [<file-or-folder> ...]
```

`QConvert.Cli.exe` is a silent Windows executable, so Explorer context-menu actions do not flash a console window. `QConvert.exe` remains the graphical settings and image-editor app.

| Option | Effect |
|---|---|
| `--to jpg` / `--to png` / `--to ico` / `--to webp` / `--to avif` | format conversion |
| `--fit 1920x1080` | resize to fit inside the box (no upscaling) |
| `--cover 1920x1080` | scale to cover, center-crop to exactly this size |
| `--crop 4:3` | center-crop to this aspect ratio, no resizing |
| `--strip-metadata` | remove image metadata |
| `--sepia 60` | apply a sepia effect |
| `--compress-jpg` / `--optimize-png` | optimize existing JPG/PNG files |
| `--favicon` / `--avatar 512` | create favicon bundles or square avatar images |
| `--paste jpg` / `--paste png` | save the clipboard image into the selected folder |
| `--quality 85` | override the saved JPEG quality for this call |
| `--background #ffffff` | background color for formats without transparency |

Exit codes: `0` success, `1` one or more conversions failed, `2` invalid arguments.

## Building from source

Requires the .NET 10 SDK.

```
dotnet build QConvert.slnx         # build
dotnet test QConvert.slnx          # run tests
```

Build the installer:

```
dotnet build QConvert.Installer/QConvert.Installer.wixproj -c Release -p:ProductVersion=1.0.0
```

## Releasing

- Push a tag like `v1.2.3`.
- The release workflow builds/tests, publishes self-contained `win-x64`, builds `QConvert-1.2.3-x64.msi`, creates a GitHub Release, and runs `winget-releaser` when `WINGET_TOKEN` is configured.
- Keep the installer `UpgradeCode` stable across releases.
- The winget identifier is `Code-iX.QConvert`; the installer is self-contained and should not declare a .NET runtime dependency.
- First winget listing still requires the package to exist in `microsoft/winget-pkgs`; after that, `winget-releaser` can submit update PRs.

## License
[MIT](LICENSE) © Code-iX
