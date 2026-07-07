using System.IO;
using Microsoft.Win32;

namespace QConvert.Core
{
    /// <summary>
    /// Builds a cascading "QConvert" Explorer context menu under HKCU (no
    /// elevation required). The menu content depends on the user's settings, so
    /// it is rebuilt whenever the context-menu setting is enabled and saved.
    /// </summary>
    public static class ShellIntegration
    {
        private const string BaseKey = @"Software\Classes\SystemFileAssociations";
        private const string DirectoryBackgroundMenuKey = @"Software\Classes\Directory\Background\shell\QConvert";
        private const string DirectoryMenuKey = @"Software\Classes\Directory\shell\QConvert";
        // ECF_SEPARATORBEFORE: draws a separator above the entry in cascade menus.
        private const int SeparatorBefore = 0x20;

        private static readonly string[] Extensions = { ".png", ".jpg", ".jpeg", ".webp", ".avif", ".ico" };

        public static void Register(string guiExePath, string cliExePath, AppSettings settings)
        {
            var iconPath = ContextMenuIconPath(guiExePath);

            foreach (var extension in Extensions)
            {
                Unregister(extension);

                var entries = MenuEntries(extension, settings).ToList();
                if (entries.Count == 0)
                {
                    continue;
                }

                using var root = Registry.CurrentUser.CreateSubKey(MenuKeyPath(extension));
                root.SetValue("MUIVerb", "QConvert");
                root.SetValue("Icon", iconPath);
                root.SetValue("SubCommands", "");

                using var shell = root.CreateSubKey("shell");
                var index = 0;
                int? previousGroup = null;

                foreach (var entry in entries)
                {
                    var commandExePath = entry.Arguments == CommandLine.OpenOption
                        ? guiExePath
                        : cliExePath;

                    AddEntry(shell, ref index, commandExePath, entry.Label, entry.Arguments,
                        iconPath: entry.Arguments == CommandLine.OpenOption ? iconPath : null,
                        separatorBefore: previousGroup is not null && previousGroup != entry.Group);
                    previousGroup = entry.Group;
                }
            }

            Registry.CurrentUser.DeleteSubKeyTree(DirectoryBackgroundMenuKey, throwOnMissingSubKey: false);
            Registry.CurrentUser.DeleteSubKeyTree(DirectoryMenuKey, throwOnMissingSubKey: false);

            if (FolderMenuEntries(settings).Any())
            {
                RegisterFolderMenu(DirectoryBackgroundMenuKey, guiExePath, cliExePath, "%V", settings);
                RegisterFolderMenu(DirectoryMenuKey, guiExePath, cliExePath, "%1", settings);
            }
        }

        /// <summary>One cascading-menu entry. The group number determines where separators are drawn.</summary>
        public sealed record MenuEntry(string Label, string Arguments, int Group);

        public sealed record ContextMenuDiagnostics(
            string? GuiExecutablePath,
            string? CliExecutablePath,
            string? InstallFolder,
            IReadOnlyList<RegistryLocationDiagnostics> Locations);

        public sealed record RegistryLocationDiagnostics(
            string Name,
            string KeyPath,
            bool Exists,
            string? MuiVerb,
            string? Icon,
            string? SubCommands,
            IReadOnlyList<RegistryCommandDiagnostics> Commands,
            string? Error);

        public sealed record RegistryCommandDiagnostics(
            string KeyName,
            string? MuiVerb,
            string? Command,
            string? ExpectedExecutablePath,
            bool? CommandUsesExecutable);

        /// <summary>
        /// Context-menu entries for one file extension, honoring the per-entry
        /// toggles. Also used by the settings UI to render the live preview, so
        /// it must stay in sync with what <see cref="Register"/> writes.
        /// </summary>
        public static IEnumerable<MenuEntry> MenuEntries(string extension, AppSettings settings)
        {
            yield return new MenuEntry("Open", CommandLine.OpenOption, -1);

            foreach (var target in ConversionsFor(extension))
            {
                if (settings.IsConvertTargetEnabled(target))
                {
                    yield return new MenuEntry($"Convert to {target.DisplayName()}", $"--to {target.CliValue()}", 0);
                }
            }

            if (settings.EnableRemoveMetadata)
            {
                yield return new MenuEntry("Remove metadata", "--strip-metadata", 1);
            }

            foreach (var intensity in settings.SepiaIntensities)
            {
                yield return new MenuEntry($"Sepia {intensity}%", $"--sepia {intensity}", 1);
            }

            if (settings.EnableCompressJpeg && extension is ".jpg" or ".jpeg")
            {
                yield return new MenuEntry("Compress JPG", "--compress-jpg", 1);
            }

            if (settings.EnableOptimizePng && extension is ".png")
            {
                yield return new MenuEntry("Optimize PNG", "--optimize-png", 1);
            }

            if (settings.EnableFavicon)
            {
                yield return new MenuEntry("Create favicon bundle", "--favicon", 1);
            }

            foreach (var size in settings.AvatarSizes)
            {
                yield return new MenuEntry($"Make {size}×{size} avatar", $"--avatar {size}", 2);
            }

            foreach (var size in settings.FitSizes)
            {
                yield return new MenuEntry($"Resize to fit {size.Width}×{size.Height}", $"--fit {size.Width}x{size.Height}", 3);
            }

            foreach (var size in settings.CoverSizes)
            {
                yield return new MenuEntry($"Crop to {size.Width}×{size.Height}", $"--cover {size.Width}x{size.Height}", 3);
            }

            foreach (var ratio in settings.AspectRatios)
            {
                yield return new MenuEntry($"Crop to {ratio.X}:{ratio.Y}", $"--crop {ratio.X}:{ratio.Y}", 3);
            }
        }

        /// <summary>Context-menu entries for folders, honoring the per-entry toggles.</summary>
        public static IEnumerable<MenuEntry> FolderMenuEntries(AppSettings settings)
        {
            if (settings.EnablePastePng)
            {
                yield return new MenuEntry("Paste image as PNG", "--paste png", 0);
            }

            if (settings.EnablePasteJpg)
            {
                yield return new MenuEntry("Paste image as JPG", "--paste jpg", 0);
            }
        }

        public static void Unregister()
        {
            foreach (var extension in Extensions)
            {
                Unregister(extension);
            }

            Registry.CurrentUser.DeleteSubKeyTree(DirectoryBackgroundMenuKey, throwOnMissingSubKey: false);
            Registry.CurrentUser.DeleteSubKeyTree(DirectoryMenuKey, throwOnMissingSubKey: false);
        }

        public static bool IsRegistered()
        {
            foreach (var extension in Extensions)
            {
                using var key = Registry.CurrentUser.OpenSubKey(MenuKeyPath(extension));
                if (key is not null) return true;
            }

            using var folderKey = Registry.CurrentUser.OpenSubKey(DirectoryMenuKey);
            return folderKey is not null;
        }

        public static ContextMenuDiagnostics Diagnose(string? expectedGuiExePath, string? expectedCliExePath)
        {
            var normalizedGuiExePath = NormalizeExecutablePath(expectedGuiExePath);
            var normalizedCliExePath = NormalizeExecutablePath(expectedCliExePath);
            var locations = new List<RegistryLocationDiagnostics>();

            foreach (var extension in Extensions)
            {
                locations.Add(ReadRegistryLocation(
                    $"File {extension}",
                    MenuKeyPath(extension),
                    normalizedGuiExePath,
                    normalizedCliExePath));
            }

            locations.Add(ReadRegistryLocation("Folder background", DirectoryBackgroundMenuKey, normalizedGuiExePath, normalizedCliExePath));
            locations.Add(ReadRegistryLocation("Folder", DirectoryMenuKey, normalizedGuiExePath, normalizedCliExePath));

            return new ContextMenuDiagnostics(
                expectedGuiExePath,
                expectedCliExePath,
                ReadInstallFolder(),
                locations);
        }

        private static void Unregister(string extension)
        {
            Registry.CurrentUser.DeleteSubKeyTree(MenuKeyPath(extension), throwOnMissingSubKey: false);

            // Flat verbs written by versions before the cascading menu.
            Registry.CurrentUser.DeleteSubKeyTree($@"{BaseKey}\{extension}\shell\QConvert.ToJpg", throwOnMissingSubKey: false);
            Registry.CurrentUser.DeleteSubKeyTree($@"{BaseKey}\{extension}\shell\QConvert.ToPng", throwOnMissingSubKey: false);
            Registry.CurrentUser.DeleteSubKeyTree($@"{BaseKey}\{extension}\shell\QConvert.ToIco", throwOnMissingSubKey: false);
        }

        private static void RegisterFolderMenu(string keyPath, string guiExePath, string cliExePath, string folderArgument, AppSettings settings)
        {
            Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
            var iconPath = ContextMenuIconPath(guiExePath);

            using var root = Registry.CurrentUser.CreateSubKey(keyPath);
            root.SetValue("MUIVerb", "QConvert");
            root.SetValue("Icon", iconPath);
            root.SetValue("SubCommands", "");

            using var shell = root.CreateSubKey("shell");
            var index = 0;

            foreach (var entry in FolderMenuEntries(settings))
            {
                AddEntry(shell, ref index, cliExePath, entry.Label, entry.Arguments, folderArgument,
                    iconPath: null,
                    separatorBefore: false);
            }
        }

        private static void AddEntry(RegistryKey shell, ref int index, string exePath, string label, string arguments, string? iconPath, bool separatorBefore)
        {
            AddEntry(shell, ref index, exePath, label, arguments, "%1", iconPath, separatorBefore);
        }

        private static void AddEntry(RegistryKey shell, ref int index, string exePath, string label, string arguments, string targetArgument, string? iconPath, bool separatorBefore)
        {
            using var key = shell.CreateSubKey($"{index++:00}");
            key.SetValue("MUIVerb", label);
            if (iconPath is not null)
            {
                key.SetValue("Icon", iconPath);
            }

            if (separatorBefore)
            {
                key.SetValue("CommandFlags", SeparatorBefore, RegistryValueKind.DWord);
            }

            using var command = key.CreateSubKey("command");
            command.SetValue("", $"\"{exePath}\" {arguments} \"{targetArgument}\"");
        }

        private static IEnumerable<ConversionTarget> ConversionsFor(string extension) => extension switch
        {
            ".png"  => new[] { ConversionTarget.Jpeg, ConversionTarget.WebP, ConversionTarget.Avif, ConversionTarget.Ico },
            ".webp" => new[] { ConversionTarget.Jpeg, ConversionTarget.Png, ConversionTarget.Avif, ConversionTarget.Ico },
            ".avif" => new[] { ConversionTarget.Jpeg, ConversionTarget.Png, ConversionTarget.WebP, ConversionTarget.Ico },
            ".ico"  => new[] { ConversionTarget.Png },
            _       => new[] { ConversionTarget.Png, ConversionTarget.WebP, ConversionTarget.Avif, ConversionTarget.Ico },
        };

        private static string ContextMenuIconPath(string exePath) => exePath;

        private static string MenuKeyPath(string extension) =>
            $@"{BaseKey}\{extension}\shell\QConvert";

        private static RegistryLocationDiagnostics ReadRegistryLocation(
            string name,
            string keyPath,
            string? expectedGuiExePath,
            string? expectedCliExePath)
        {
            try
            {
                using var root = Registry.CurrentUser.OpenSubKey(keyPath);
                if (root is null)
                {
                    return new RegistryLocationDiagnostics(
                        name,
                        $@"HKCU\{keyPath}",
                        Exists: false,
                        MuiVerb: null,
                        Icon: null,
                        SubCommands: null,
                        Commands: Array.Empty<RegistryCommandDiagnostics>(),
                        Error: null);
                }

                var commands = new List<RegistryCommandDiagnostics>();
                using var shell = root.OpenSubKey("shell");
                if (shell is not null)
                {
                    foreach (var keyName in shell.GetSubKeyNames().OrderBy(key => key, StringComparer.OrdinalIgnoreCase))
                    {
                        using var entry = shell.OpenSubKey(keyName);
                        using var command = entry?.OpenSubKey("command");
                        var commandText = command?.GetValue("") as string;
                        var arguments = CommandArguments(commandText);
                        var expectedExePath = arguments == CommandLine.OpenOption
                            ? expectedGuiExePath
                            : expectedCliExePath;

                        commands.Add(new RegistryCommandDiagnostics(
                            keyName,
                            entry?.GetValue("MUIVerb") as string,
                            commandText,
                            expectedExePath,
                            expectedExePath is null ? null : CommandUsesExecutable(commandText, expectedExePath)));
                    }
                }

                return new RegistryLocationDiagnostics(
                    name,
                    $@"HKCU\{keyPath}",
                    Exists: true,
                    MuiVerb: root.GetValue("MUIVerb") as string,
                    Icon: root.GetValue("Icon") as string,
                    SubCommands: root.GetValue("SubCommands") as string,
                    Commands: commands,
                    Error: null);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
            {
                return new RegistryLocationDiagnostics(
                    name,
                    $@"HKCU\{keyPath}",
                    Exists: false,
                    MuiVerb: null,
                    Icon: null,
                    SubCommands: null,
                    Commands: Array.Empty<RegistryCommandDiagnostics>(),
                    Error: ex.Message);
            }
        }

        private static string? ReadInstallFolder()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Code-iX\QConvert");
                return key?.GetValue("InstallFolder") as string;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
            {
                return $"unreadable: {ex.Message}";
            }
        }

        private static bool CommandUsesExecutable(string? command, string expectedExePath)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                return false;
            }

            var quoted = $"\"{expectedExePath}\"";
            return command.StartsWith(quoted, StringComparison.OrdinalIgnoreCase)
                || command.StartsWith(expectedExePath, StringComparison.OrdinalIgnoreCase);
        }

        private static string? CommandArguments(string? command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                return null;
            }

            var trimmed = command.TrimStart();
            if (trimmed.StartsWith('"'))
            {
                var closingQuote = trimmed.IndexOf('"', 1);
                return closingQuote < 0
                    ? null
                    : trimmed[(closingQuote + 1)..].TrimStart().Split(' ', 2)[0];
            }

            var firstSpace = trimmed.IndexOf(' ');
            return firstSpace < 0
                ? null
                : trimmed[(firstSpace + 1)..].TrimStart().Split(' ', 2)[0];
        }

        private static string? NormalizeExecutablePath(string? exePath)
        {
            if (string.IsNullOrWhiteSpace(exePath))
            {
                return null;
            }

            try
            {
                return Path.GetFullPath(exePath.Trim('"'));
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
            {
                return exePath.Trim('"');
            }
        }
    }
}
