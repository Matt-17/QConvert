# Releasing QConvert

## Regular release

1. Make sure `main` is green.
2. Tag and push:

   ```
   git tag v1.2.3
   git push origin v1.2.3
   ```

The `Release` workflow then:

1. runs the test suite,
2. publishes a single-file, framework-dependent build with the version baked in,
3. builds the per-user MSI with WiX,
4. creates a GitHub Release with the MSI attached and generated notes,
5. submits the new version to [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) via [winget-releaser](https://github.com/vedantmgoyal9/winget-releaser).

The MSI `ProductVersion` must be numeric `major.minor.patch`, so tags must look like `v1.2.3`.

## One-time setup for winget publishing

winget-releaser can only *update* an existing package, so the first version has to be submitted manually:

1. Create a GitHub Release with the MSI (push a tag; the winget job will fail — that's expected the first time).
2. Submit the initial manifest with [wingetcreate](https://github.com/microsoft/winget-create):

   ```
   wingetcreate new https://github.com/Code-iX/QConvert/releases/download/v1.0.0/QConvert-1.0.0-x64.msi
   ```

   Use `Code-iX.QConvert` as the package identifier and add a dependency on
   `Microsoft.DotNet.DesktopRuntime.8` when prompted for optional fields.
3. Wait for the winget-pkgs PR to be merged.
4. Create a classic personal access token with the `public_repo` scope and add it
   to this repository as the `WINGET_TOKEN` secret. winget-releaser uses it to
   fork winget-pkgs and open the update PR.

Every later release is then fully automatic.

## Versioning notes

- The installer `UpgradeCode` in `installer/Package.wxs` must never change — it is
  what makes new MSIs upgrade (replace) older installs.
- The app version shown in *Apps & Features* comes from the tag via
  `-p:Version` and `-d Version`.
