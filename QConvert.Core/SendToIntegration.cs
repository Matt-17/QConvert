using System.IO;

namespace QConvert.Core
{
    /// <summary>
    /// Manages a Windows "Send To" shortcut for QConvert.
    /// </summary>
    public static class SendToIntegration
    {
        private static string SendToFolder =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Windows\SendTo");

        private static string ShortcutPath =>
            Path.Combine(SendToFolder, "QConvert.lnk");

        /// <summary>Creates or updates the Send To shortcut pointing to <paramref name="exePath"/>.</summary>
        public static void Install(string exePath)
        {
            Directory.CreateDirectory(SendToFolder);

            // Use the WScript.Shell COM object — available on all Windows versions.
            var shellType = Type.GetTypeFromProgID("WScript.Shell")
                ?? throw new InvalidOperationException("WScript.Shell COM object is not available.");

            dynamic shell = Activator.CreateInstance(shellType)!;
            try
            {
                dynamic shortcut = shell.CreateShortcut(ShortcutPath);
                shortcut.TargetPath = exePath;
                shortcut.Description = "QConvert – Convert images";
                shortcut.Save();
            }
            finally
            {
                if (shell is IDisposable d) d.Dispose();
            }
        }

        /// <summary>Removes the Send To shortcut if it exists.</summary>
        public static void Uninstall()
        {
            if (File.Exists(ShortcutPath))
            {
                File.Delete(ShortcutPath);
            }
        }

        /// <summary>Returns true if the Send To shortcut currently exists.</summary>
        public static bool IsInstalled() => File.Exists(ShortcutPath);
    }
}
