# QConvert Feature Plan

This document collects future feature ideas for QConvert and describes them in enough detail that each item can be implemented later as a focused task.

## Product Direction

QConvert should stay a fast Explorer context-menu utility for common image operations. The strongest direction is not a full image editor, but a set of reliable one-click and low-friction batch actions:

- Convert common image formats.
- Resize, crop, and export assets for real workflows.
- Reduce file size.
- Remove private metadata.
- Generate web/app icon bundles.
- Keep output predictable and non-destructive.

## 1. WebP Export

### Goal

Allow users to convert PNG, JPG, JPEG, ICO, and possibly other WIC-readable sources to `.webp`.

### User Value

WebP is common for web images because it usually produces smaller files than PNG or JPEG while preserving good quality. This would make QConvert more useful for website and app asset workflows.

### User-Facing Behavior

- Add `Convert to WebP` in the Explorer context menu.
- Add CLI support:

```text
QConvert.exe --to webp file.png
```

- Output path should follow the existing sibling naming behavior:

```text
photo.png -> photo.webp
photo.webp already exists -> photo.001.webp
```

- Existing JPEG quality setting may not map directly to WebP. Add a separate WebP quality setting if the encoder supports lossy quality.

### Implementation Notes

- WPF/WIC generally cannot encode WebP by default.
- Add a dependency that can encode WebP reliably from .NET.
- Candidate libraries to evaluate:
  - `SixLabors.ImageSharp` plus WebP support if license/deployment is acceptable.
  - `Magick.NET`, larger but very capable.
  - A dedicated WebP encoder package if smaller and actively maintained.
- Keep the current WIC path for JPEG/PNG/ICO if possible.
- Add `ConversionTarget.WebP`.
- Update:
  - `ConversionTarget.cs`
  - `CommandLine.cs`
  - `ShellIntegration.cs`
  - `ImageConverter.cs`
  - UI text in `MainWindow.xaml`

### Settings

- Add `WebPQuality`, default probably `85`.
- Min/max range: `1..100`.
- Add a settings card or combine with JPEG quality under an `Output quality` section.

### Risks

- New dependency size.
- Native binaries if using ImageMagick or libwebp wrappers.
- Transparency behavior:
  - PNG -> WebP should preserve alpha.
  - JPEG -> WebP has no alpha.

### Verification

- Convert PNG with transparency to WebP and inspect alpha.
- Convert JPG to WebP.
- Batch-convert multiple files.
- Check output naming collisions.
- Check Explorer menu registration.

## 2. AVIF Export

### Goal

Allow users to convert images to `.avif`.

### User Value

AVIF can produce very small files at high quality and is increasingly used on the web.

### User-Facing Behavior

- Add `Convert to AVIF`.
- CLI:

```text
QConvert.exe --to avif file.png
```

- Preserve transparency where supported.
- Use predictable sibling output naming.

### Implementation Notes

- WIC does not provide reliable AVIF encoding out of the box.
- Likely requires a third-party dependency.
- This should be implemented after WebP, because the architecture for non-WIC encoders can be reused.
- Add `ConversionTarget.Avif`.
- Consider whether AVIF should have its own quality setting.

### Risks

- Encoding can be slow.
- Dependency may be large or require native binaries.
- Some Windows viewers may not preview AVIF without extensions.

### Verification

- Convert PNG, JPEG, and transparent PNG.
- Confirm output opens in common viewers or browsers.
- Compare speed on large images.

## 3. PNG Compression

### Goal

Add an `Optimize PNG` action that reduces PNG file size without changing visible pixels.

### User Value

Useful for screenshots, UI assets, icons, and website images.

### User-Facing Behavior

- Context-menu entry:

```text
Optimize PNG
```

- CLI:

```text
QConvert.exe --optimize-png file.png
```

- Output should not overwrite by default:

```text
image.png -> image.optimized.png
```

- If the optimized file would be larger, either:
  - still write it and let the user decide, or
  - skip output and show a message.

Recommended behavior: write the file only if it is smaller, otherwise report that no smaller output was produced.

### Implementation Notes

- WPF `PngBitmapEncoder` may not produce optimal compression.
- Consider:
  - `ZopfliPNG`
  - `oxipng`
  - `pngquant` for lossy palette compression
  - managed PNG optimizer if available
- Decide whether optimization should be lossless only.
- A simple first version can re-encode using a better PNG encoder.

### Settings

- `PNG optimization mode`:
  - `Lossless`
  - `Lossy smaller files`

This can wait until after a first implementation.

### Risks

- External tools complicate deployment.
- Lossy PNG can surprise users if not clearly labeled.

### Verification

- Optimize several PNGs.
- Confirm pixel equality for lossless mode.
- Confirm output file is smaller or skipped.
- Confirm alpha is preserved.

## 4. JPEG Recompression

### Goal

Add an action to recompress existing JPEG files using the configured JPEG quality.

### User Value

Fast way to shrink large photos before sharing or uploading.

### User-Facing Behavior

- Context-menu entry for JPEG files:

```text
Compress JPG
```

- CLI:

```text
QConvert.exe --compress-jpg file.jpg
```

- Output:

```text
photo.jpg -> photo.compressed.jpg
```

- Use configured JPEG quality.

### Implementation Notes

- Existing JPEG encoder path can be reused.
- Add a new operation type such as `CompressJpegOperation`.
- Unlike `--to jpg`, this operation should be available even when the source is already JPEG.
- Consider applying EXIF orientation before saving, as current conversion does.

### Settings

- Uses existing JPEG quality.
- Optional future setting:
  - preserve metadata
  - strip metadata

### Risks

- Recompressing JPEG is lossy.
- Repeated recompression lowers quality.

### Verification

- Compress JPEG with several quality values.
- Confirm file size usually decreases.
- Confirm output naming collision behavior.

## 5. Remove Metadata

### Goal

Add a privacy-focused operation that removes EXIF, GPS, camera, and other metadata from images.

### User Value

Useful before sharing photos publicly. GPS metadata in photos can expose private location data.

### User-Facing Behavior

- Context-menu entry:

```text
Remove metadata
```

- CLI:

```text
QConvert.exe --strip-metadata file.jpg
```

- Output:

```text
photo.jpg -> photo.clean.jpg
```

- For PNG:

```text
image.png -> image.clean.png
```

### Implementation Notes

- Loading through WIC and re-encoding often drops metadata already.
- Make this explicit and reliable.
- Preserve pixels.
- For JPEG, apply EXIF orientation before saving so the cleaned file still appears upright.
- For PNG, avoid carrying text chunks and metadata chunks.

### Settings

None required for first version.

### Risks

- ICC color profiles are metadata but affect color rendering.
- Decide whether to strip ICC profiles:
  - Privacy mode should remove all nonessential metadata.
  - Color-accurate mode may preserve ICC.

Recommended first version: strip all metadata and document that behavior in the operation name or help text.

### Verification

- Test with a JPEG containing GPS EXIF.
- Confirm GPS tags are removed.
- Confirm image orientation remains correct.
- Confirm PNG text chunks are removed.

## 6. Preserve Metadata Toggle

### Goal

Give users control over whether metadata is preserved during format-preserving operations.

### User Value

Some users want privacy. Others want camera/date/color metadata retained.

### User-Facing Behavior

- Add a settings toggle:

```text
Preserve metadata when possible
```

- Default should probably be off for converted files, because current behavior likely drops metadata.

### Implementation Notes

- This is easiest after `Remove metadata`, because the metadata behavior will already be understood.
- WIC metadata copying can be complex and format-specific.
- Start with JPEG -> JPEG.
- Preserve EXIF, date, camera metadata, and ICC profile where possible.

### Risks

- Metadata copy failures across formats.
- Orientation metadata can conflict with baked pixel rotation.

### Verification

- JPEG with EXIF orientation.
- JPEG with GPS.
- JPEG with ICC profile.
- Confirm expected metadata remains only when toggle is enabled.

## 7. Configurable Transparency Flattening

### Goal

Let users choose the background color used when converting transparent images to formats without alpha, especially JPEG.

### User Value

Current JPEG conversion flattens transparency onto white. That is sensible, but not always correct. Users may need black, transparent checkerboard-like previews, brand colors, or custom backgrounds.

### User-Facing Behavior

- Add setting:

```text
Transparency background for JPG
```

- Options:
  - White
  - Black
  - Custom color

- CLI optional future support:

```text
QConvert.exe --to jpg --background #ffffff file.png
```

### Implementation Notes

- Replace `FlattenToWhite` with a generalized `FlattenToColor`.
- Add color parsing and persistence in `AppSettings`.
- UI can use a simple text box first:

```text
#ffffff
```

- Later, add a color picker.

### Risks

- Invalid color input.
- Need to keep command-line parsing simple.

### Verification

- Transparent PNG -> JPG with white.
- Transparent PNG -> JPG with black.
- Transparent PNG -> JPG with custom color.
- Invalid setting fallback.

## 8. Square Icon and Avatar Export

### Goal

Add one-click square crop and resize presets for avatars, profile pictures, and app icons.

### User Value

Common workflow for web profiles, app stores, Discord, GitHub, Steam, and other platforms.

### User-Facing Behavior

- Context-menu entries such as:

```text
Make 512x512 avatar
Make 256x256 avatar
Make 128x128 avatar
```

- Output:

```text
photo.jpg -> photo.512x512.png
```

- Operation should center-crop to square, then resize.

### Implementation Notes

- Existing `CropToSize` already supports cover crop.
- Add default square presets to `CoverSizes`, or create a separate settings section for avatars.
- PNG output is preferable for avatars because it preserves quality and alpha.

### Settings

- Add default square export presets:
  - `512x512`
  - `256x256`
  - `128x128`
  - `64x64`

### Risks

- Center crop may cut off faces or important content.
- Later improvement: crop-position selector.

### Verification

- Landscape photo -> square.
- Portrait photo -> square.
- Transparent PNG -> square PNG.

## 9. Favicon Export

### Goal

Generate a ready-to-use favicon bundle from one source image.

### User Value

Web projects often need several icon files. A single context-menu action could save time.

### User-Facing Behavior

- Context-menu entry:

```text
Create favicon bundle
```

- CLI:

```text
QConvert.exe --favicon file.png
```

- Output folder:

```text
file.favicon/
  favicon.ico
  favicon-16x16.png
  favicon-32x32.png
  apple-touch-icon.png
  android-chrome-192x192.png
  android-chrome-512x512.png
  site.webmanifest
```

### Implementation Notes

- Reuse ICO writer for `favicon.ico`.
- Generate PNG sizes with alpha-aware resizing.
- Add an operation that writes multiple files.
- Folder naming should avoid collisions:

```text
logo.favicon
logo.favicon.001
```

- `site.webmanifest` can be generated with a generic app name first.

### Settings

- Optional app/site name for manifest.
- Optional theme color and background color.

First version can use defaults:

```json
{
  "name": "",
  "short_name": "",
  "icons": [...]
}
```

### Risks

- Users may expect all favicon best practices, which change over time.
- Keep first version practical and simple.

### Verification

- Generated folder contains all expected files.
- ICO contains expected sizes.
- PNG files have correct dimensions.
- Manifest JSON is valid.

## 10. Batch Output Folder

### Goal

Allow users to send converted files to a subfolder instead of writing next to the source.

### User Value

Batch conversions can clutter a folder. A `_converted` folder keeps outputs organized.

### User-Facing Behavior

- Add setting:

```text
Save converted files into subfolder
```

- Subfolder text box:

```text
_converted
```

- If disabled, keep current sibling behavior.

### Implementation Notes

- Extend `OutputPathResolver`.
- It should support:
  - sibling output
  - subfolder output
  - operation-specific suffixes
- Create folder if missing.
- Avoid path traversal in setting values.

### Risks

- Permissions when creating subfolders.
- Relative path validation.

### Verification

- Convert one file with setting off.
- Convert multiple files with setting on.
- Confirm output collision behavior inside subfolder.

## 11. Clipboard Support

### Goal

Allow users to paste an image from the clipboard into QConvert and save or convert it.

### User Value

Useful for screenshots and copied browser images.

### User-Facing Behavior

- In the app window:
  - If clipboard contains an image, show quick actions.
  - Actions could include:
    - Save as PNG
    - Save as JPG
    - Save as ICO
    - Resize/crop using configured presets

- Optional CLI later:

```text
QConvert.exe --from-clipboard --to png
```

### Implementation Notes

- Use WPF clipboard APIs.
- `Clipboard.ContainsImage()`
- `Clipboard.GetImage()`
- Need a default save location:
  - Pictures folder
  - Desktop
  - ask via SaveFileDialog

Recommended first version: app window only, with SaveFileDialog.

### Risks

- Clipboard image formats vary.
- Some clipboard images may have premultiplied alpha.

### Verification

- Copy screenshot.
- Copy transparent PNG from browser/app.
- Save as PNG/JPG/ICO.

## 12. Drag-and-Drop Window

### Goal

Let the main app window accept dropped image files and expose quick action buttons.

### User Value

Users can use QConvert without Explorer context menus.

### User-Facing Behavior

- Drag files onto the window.
- Show a compact list of selected files.
- Show action buttons:
  - Convert to JPG
  - Convert to PNG
  - Convert to ICO
  - Resize to selected presets
  - Crop to selected presets

### Implementation Notes

- Set `AllowDrop="True"` on the window or a dedicated drop zone.
- Handle `DragEnter` and `Drop`.
- Reuse existing command execution methods.
- Consider progress and error reporting for multiple files.

### Risks

- Main window may become cluttered.
- Need to preserve current settings-focused simplicity.

### Verification

- Drop one file.
- Drop multiple files.
- Drop unsupported file.
- Confirm errors are shown without stopping all conversions.

## 13. Crop Position Selector

### Goal

Let users choose how cover-crop operations align the image.

### User Value

Center crop is often fine, but it can cut off heads, subjects, or important content.

### User-Facing Behavior

- Add crop anchor setting:
  - Center
  - Top
  - Bottom
  - Left
  - Right
  - Top-left
  - Top-right
  - Bottom-left
  - Bottom-right

- Context-menu labels could stay the same and use the configured anchor.

### Implementation Notes

- Extend `ResizeMath.Cover` to accept an anchor.
- Current crop rect centers:

```text
x = (scaled.Width - box.Width) / 2
y = (scaled.Height - box.Height) / 2
```

- Anchor changes x/y:
  - left: `x = 0`
  - right: `x = scaled.Width - box.Width`
  - top: `y = 0`
  - bottom: `y = scaled.Height - box.Height`

### Risks

- A global setting may not be enough for every image.
- Per-operation context-menu entries would multiply menu size.

Recommended first version: one global crop anchor setting.

### Verification

- Landscape image cropped to portrait with each horizontal anchor.
- Portrait image cropped to landscape with each vertical anchor.

## 14. Rename Pattern

### Goal

Let users customize output file names.

### User Value

Different workflows prefer different naming styles.

### User-Facing Behavior

- Add setting:

```text
Output name pattern
```

- Supported tokens:
  - `{name}`
  - `{ext}`
  - `{target}`
  - `{width}`
  - `{height}`
  - `{operation}`

- Examples:

```text
{name}.{target}
{name}-{operation}
{name}.{width}x{height}
converted-{name}
```

### Implementation Notes

- Extend `OutputPathResolver`.
- Keep current behavior as default.
- Validate invalid file name characters.
- If pattern produces same name as source, still avoid overwrite.

### Risks

- Invalid patterns.
- Duplicate output names.
- User confusion if extension token is misused.

### Verification

- Each token replacement.
- Invalid characters.
- Collision handling.

## 15. Custom ICO Sizes

### Goal

Let users configure which sizes are included in `.ico` output.

### User Value

Some workflows need `256x256`. Some only need small sizes.

### User-Facing Behavior

- Add settings section:

```text
ICO sizes
```

- Default checked:
  - `16`
  - `32`
  - `48`
  - `64`
  - `128`

- Optional:
  - `256`

- Allow custom numeric sizes from `1..256`.

### Implementation Notes

- Move `IconSizes` into `AppSettings`.
- Pass settings into `ImageConverter.Convert`, or add an `EncoderOptions` object.
- ICO directory stores width/height `0` for `256`.
- Current writer needs this adjustment if 256 is supported.

### Risks

- Very large ICO files if too many sizes are selected.
- Need to validate unique sizes.

### Verification

- Default sizes.
- Custom sizes.
- 256x256 entry encoded with width/height byte `0`.

## 16. Copy Converted Image to Clipboard

### Goal

Convert an image and place the result on the clipboard instead of writing a file.

### User Value

Useful when pasting into chat, design tools, documents, or web forms.

### User-Facing Behavior

- Context-menu entries:

```text
Copy as PNG
Copy as JPG
```

- Maybe avoid ICO clipboard support because clipboard consumers rarely expect ICO.

### Implementation Notes

- Clipboard operations need a running STA thread, which WPF already provides.
- Existing conversion pipeline writes files. Add stream-based encoding helpers.
- For PNG clipboard, can use `Clipboard.SetImage(BitmapSource)`, but this may not preserve exact PNG encoding.
- For file-like clipboard data, more complex formats may be needed.

Recommended first version:

- `Copy as PNG` using `Clipboard.SetImage`.
- `Copy as JPG` may be less reliable because WPF clipboard image is bitmap-like, not JPEG file data.

### Risks

- Clipboard format expectations vary by target application.
- Context-menu operation exits quickly; ensure clipboard data persists after process exit.

### Verification

- Copy as PNG, paste into Paint.
- Copy transparent image, paste into apps that support alpha.
- Copy large images.

## 17. Send To Integration

### Goal

Add QConvert to the Windows `Send to` menu as an alternative entry point.

### User Value

Some users prefer `Send to` over extended context menus, especially on Windows 11.

### User-Facing Behavior

- Settings button:

```text
Install Send To shortcut
```

- Shortcut points to QConvert.
- Dropped files open the app or run a default action.

### Implementation Notes

- Windows SendTo folder:

```text
%APPDATA%\Microsoft\Windows\SendTo
```

- A plain shortcut to the app may open the settings UI with file arguments depending on how Windows passes files.
- Need to decide:
  - Show quick action window for Send To files.
  - Run a configured default operation.

Recommended first version: Send To opens QConvert with dropped files in a quick-action window.

### Risks

- Requires adding a file-selection workflow to the main app.
- Shortcut management edge cases.

### Verification

- Install shortcut.
- Send one file.
- Send multiple files.
- Remove shortcut.

## Suggested Implementation Order

1. Remove metadata.
2. JPEG recompression.
3. Configurable transparency flattening.
4. Custom ICO sizes.
5. Favicon export.
6. Batch output folder.
7. WebP export.
8. Square avatar export.
9. Crop position selector.
10. Drag-and-drop window.
11. Clipboard support.
12. PNG compression.
13. Rename pattern.
14. Send To integration.
15. AVIF export.

This order prioritizes features that fit the current codebase with low dependency risk before adding larger format dependencies or UI workflows.
