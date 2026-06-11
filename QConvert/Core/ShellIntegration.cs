using Microsoft.Win32;

namespace QConvert.Core
{
    /// <summary>
    /// Registers Explorer context-menu verbs under HKCU (no elevation required).
    /// The MSI installer writes the same keys; this class lets the app repair or
    /// remove them from the settings window.
    /// </summary>
    public static class ShellIntegration
    {
        private const string BaseKey = @"Software\Classes\SystemFileAssociations";

        private static readonly (string Extension, ConversionTarget Target)[] Verbs =
        {
            (".png", ConversionTarget.Jpeg),
            (".webp", ConversionTarget.Jpeg),
            (".webp", ConversionTarget.Png),
            (".jpg", ConversionTarget.Png),
            (".jpeg", ConversionTarget.Png),
        };

        public static void Register(string exePath)
        {
            foreach (var (extension, target) in Verbs)
            {
                using var verbKey = Registry.CurrentUser.CreateSubKey(VerbKeyPath(extension, target));
                verbKey.SetValue("", target == ConversionTarget.Jpeg ? "Convert to JPG" : "Convert to PNG");
                verbKey.SetValue("Icon", exePath);

                using var commandKey = verbKey.CreateSubKey("command");
                commandKey.SetValue("", $"\"{exePath}\" --to {TargetArgument(target)} \"%1\"");
            }
        }

        public static void Unregister()
        {
            foreach (var (extension, target) in Verbs)
            {
                Registry.CurrentUser.DeleteSubKeyTree(VerbKeyPath(extension, target), throwOnMissingSubKey: false);
            }
        }

        public static bool IsRegistered()
        {
            using var key = Registry.CurrentUser.OpenSubKey(VerbKeyPath(Verbs[0].Extension, Verbs[0].Target));
            return key is not null;
        }

        private static string VerbKeyPath(string extension, ConversionTarget target) =>
            $@"{BaseKey}\{extension}\shell\QConvert.To{(target == ConversionTarget.Jpeg ? "Jpg" : "Png")}";

        private static string TargetArgument(ConversionTarget target) =>
            target == ConversionTarget.Jpeg ? "jpg" : "png";
    }
}
