# QConvert

Fast image conversion and resizing straight from the Windows Explorer context menu, by [Code-iX](https://github.com/Code-iX).

Right-click an image, open the **QConvert** submenu, pick an action — a converted copy appears next to the original. No window, no wait.

## Features

- **Convert**: PNG / WebP → JPG and JPG / WebP → PNG
- **Resize to fit**: scales down (never up) to fit inside a box, keeping the aspect ratio
- **Crop to size**: scales (up or down) to cover an exact size, then center-crops — the result is exactly that size (e.g. 1920×1080)
- **Crop to aspect ratio**: center-crops to a ratio like 4:3 without any resizing
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

Download the MSI from the [latest release](../../releases/latest) and run it. A default context menu (format conversions) is registered during installation.

**Requirement:** [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (x64).

> **WebP note:** decoding WebP uses the Windows codec. If WebP conversion fails, install the free [WebP Image Extensions](https://apps.microsoft.com/detail/9PG2DK419DRG) from the Microsoft Store. It is preinstalled on most Windows 10/11 systems. Resized WebP files are written as PNG (Windows has no WebP encoder).

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
QConvert.exe (--to <jpg|png> | --fit <WxH> | --cover <WxH> | --crop <X:Y>) [--quality <1-100>] <file> [<file> ...]
```

| Option | Effect |
|---|---|
| `--to jpg` / `--to png` | format conversion |
| `--fit 1920x1080` | resize to fit inside the box (no upscaling) |
| `--cover 1920x1080` | scale to cover, center-crop to exactly this size |
| `--crop 4:3` | center-crop to this aspect ratio, no resizing |
| `--quality 85` | override the saved JPEG quality for this call |

Exit codes: `0` success, `1` one or more conversions failed, `2` invalid arguments.

## Building from source

Requires the .NET 8 SDK (or newer).

```
dotnet build QConvert.sln          # build
dotnet test QConvert.sln           # run tests
```

Build the installer (requires the [WiX](https://wixtoolset.org/) CLI, `dotnet tool install --global wix`):

```
dotnet publish QConvert/QConvert.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
wix build installer/Package.wxs -arch x64 -d Version=1.0.0 -d PublishDir=publish -o QConvert.msi
```

## Releasing

Push a tag like `v1.2.3` — CI runs the tests, builds the MSI, creates a GitHub Release, and submits the version to [winget-pkgs](https://github.com/microsoft/winget-pkgs). See [docs/RELEASING.md](docs/RELEASING.md) for the one-time setup.

## License

[MIT](LICENSE) © Code-iX
