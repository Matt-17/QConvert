# QConvert

Fast image format conversion straight from the Windows Explorer context menu, by [Code-iX](https://github.com/Code-iX).

Right-click an image, pick **Convert to JPG** or **Convert to PNG**, and a converted copy appears next to the original. No window, no wait.

## Features

- **PNG / WebP → JPG** and **JPG / WebP → PNG**
- Converted copy keeps the original name; if it already exists, a numeric suffix is inserted before the extension (`photo.jpg` → `photo.001.jpg`, `photo.002.jpg`, …)
- The original file is never touched
- Transparent pixels are flattened onto white when converting to JPG (instead of turning black)
- Common EXIF orientations are baked in so phone photos stay upright
- Per-user settings (JPEG quality) — open QConvert from the Start Menu to change them
- No admin rights required: installs and registers per user

## Installation

### winget

```
winget install Code-iX.QConvert
```

### Manual

Download the MSI from the [latest release](../../releases/latest) and run it. The context-menu entries are registered automatically during installation.

**Requirement:** [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (x64).

> **WebP note:** decoding WebP uses the Windows codec. If WebP conversion fails, install the free [WebP Image Extensions](https://apps.microsoft.com/detail/9PG2DK419DRG) from the Microsoft Store. It is preinstalled on most Windows 10/11 systems.

## Usage

### Context menu

Right-click a `.png`, `.jpg`, `.jpeg`, or `.webp` file in Explorer:

| File type | Menu entry |
|---|---|
| `.png` | Convert to JPG |
| `.jpg`, `.jpeg` | Convert to PNG |
| `.webp` | Convert to JPG, Convert to PNG |

### Settings

Launch **QConvert** from the Start Menu (or run `QConvert.exe` without arguments) to:

- set the **JPEG quality** (1–100, default 90) — saved per user in `%APPDATA%\QConvert\settings.json`
- add or remove the context-menu entries

### Command line

```
QConvert.exe --to <jpg|png> <file> [<file> ...]
```

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
