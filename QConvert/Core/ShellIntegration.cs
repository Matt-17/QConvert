using Microsoft.Win32;

namespace QConvert.Core
{
    /// <summary>
    /// Builds a cascading "QConvert" Explorer context menu under HKCU (no
    /// elevation required). The menu content depends on the user's settings, so
    /// it is rebuilt on every save. The MSI installs a default menu with the
    /// format conversions only; this class overwrites it.
    /// </summary>
    public static class ShellIntegration
    {
        private const string BaseKey = @"Software\Classes\SystemFileAssociations";

        // ECF_SEPARATORBEFORE: draws a separator above the entry in cascade menus.
        private const int SeparatorBefore = 0x20;

        private static readonly string[] Extensions = { ".png", ".jpg", ".jpeg", ".webp", ".ico" };

        public static void Register(string exePath, AppSettings settings)
        {
            foreach (var extension in Extensions)
            {
                Unregister(extension);

                using var root = Registry.CurrentUser.CreateSubKey(MenuKeyPath(extension));
                root.SetValue("MUIVerb", "QConvert");
                root.SetValue("Icon", exePath);
                root.SetValue("SubCommands", "");

                using var shell = root.CreateSubKey("shell");
                var index = 0;

                foreach (var target in ConversionsFor(extension))
                {
                    AddEntry(shell, ref index, exePath,
                        $"Convert to {target.DisplayName()}",
                        $"--to {target.CliValue()}",
                        separatorBefore: false);
                }

                var firstResizeEntry = true;
                foreach (var size in settings.FitSizes)
                {
                    AddEntry(shell, ref index, exePath,
                        $"Resize to fit {size.Width}×{size.Height}",
                        $"--fit {size.Width}x{size.Height}",
                        separatorBefore: firstResizeEntry);
                    firstResizeEntry = false;
                }

                foreach (var size in settings.CoverSizes)
                {
                    AddEntry(shell, ref index, exePath,
                        $"Crop to {size.Width}×{size.Height}",
                        $"--cover {size.Width}x{size.Height}",
                        separatorBefore: firstResizeEntry);
                    firstResizeEntry = false;
                }

                foreach (var ratio in settings.AspectRatios)
                {
                    AddEntry(shell, ref index, exePath,
                        $"Crop to {ratio.X}:{ratio.Y}",
                        $"--crop {ratio.X}:{ratio.Y}",
                        separatorBefore: firstResizeEntry);
                    firstResizeEntry = false;
                }
            }
        }

        public static void Unregister()
        {
            foreach (var extension in Extensions)
            {
                Unregister(extension);
            }
        }

        public static bool IsRegistered()
        {
            using var key = Registry.CurrentUser.OpenSubKey(MenuKeyPath(Extensions[0]));
            return key is not null;
        }

        private static void Unregister(string extension)
        {
            Registry.CurrentUser.DeleteSubKeyTree(MenuKeyPath(extension), throwOnMissingSubKey: false);

            // Flat verbs written by versions before the cascading menu.
            Registry.CurrentUser.DeleteSubKeyTree($@"{BaseKey}\{extension}\shell\QConvert.ToJpg", throwOnMissingSubKey: false);
            Registry.CurrentUser.DeleteSubKeyTree($@"{BaseKey}\{extension}\shell\QConvert.ToPng", throwOnMissingSubKey: false);
            Registry.CurrentUser.DeleteSubKeyTree($@"{BaseKey}\{extension}\shell\QConvert.ToIco", throwOnMissingSubKey: false);
        }

        private static void AddEntry(RegistryKey shell, ref int index, string exePath, string label, string arguments, bool separatorBefore)
        {
            // Sub-verbs are displayed in alphabetical key order, hence the numeric prefix.
            using var key = shell.CreateSubKey($"{index++:00}");
            key.SetValue("MUIVerb", label);
            if (separatorBefore)
            {
                key.SetValue("CommandFlags", SeparatorBefore, RegistryValueKind.DWord);
            }

            using var command = key.CreateSubKey("command");
            command.SetValue("", $"\"{exePath}\" {arguments} \"%1\"");
        }

        private static IEnumerable<ConversionTarget> ConversionsFor(string extension) => extension switch
        {
            ".png" => new[] { ConversionTarget.Jpeg, ConversionTarget.Ico },
            ".webp" => new[] { ConversionTarget.Jpeg, ConversionTarget.Png, ConversionTarget.Ico },
            ".ico" => new[] { ConversionTarget.Png },
            _ => new[] { ConversionTarget.Png, ConversionTarget.Ico },
        };

        private static string MenuKeyPath(string extension) =>
            $@"{BaseKey}\{extension}\shell\QConvert";
    }
}
