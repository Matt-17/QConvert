using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace QConvert.Cli;

/// <summary>
/// Selects a file in Windows Explorer and puts it straight into inline-rename mode
/// (the "type a new name" edit box). This is what lets a pasted clipboard image be
/// saved with an auto name and then immediately renamed by the user in place.
///
/// The Shell exposes this via <c>IShellView::SelectItem</c> with the <c>SVSI_EDIT</c>
/// flag. We locate the Explorer window already showing the target folder (the one the
/// user right-clicked); if none is open we open one and wait for it to be ready.
/// The whole thing is best-effort: any failure is swallowed so the paste still succeeds.
/// </summary>
internal static class ExplorerRename
{
    // SVSI_* selection flags for IShellView::SelectItem.
    private const uint SVSI_SELECT = 0x00000001;
    private const uint SVSI_EDIT = 0x00000003; // includes SVSI_SELECT; starts inline rename
    private const uint SVSI_DESELECTOTHERS = 0x00000004;
    private const uint SVSI_ENSUREVISIBLE = 0x00000008;
    private const uint SVSI_FOCUSED = 0x00000010;

    private const uint RenameFlags =
        SVSI_EDIT | SVSI_DESELECTOTHERS | SVSI_ENSUREVISIBLE | SVSI_FOCUSED;

    private static readonly Guid SID_STopLevelBrowser =
        new("4C96BE40-915C-11CF-99D3-00AA004AE837");
    private static readonly Guid IID_IShellBrowser =
        new("000214E2-0000-0000-C000-000000000046");

    public static void SelectForRename(string filePath)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (string.IsNullOrEmpty(folder))
        {
            return;
        }

        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType is null)
            {
                return;
            }

            dynamic shell = Activator.CreateInstance(shellType)!;

            // The folder the user right-clicked usually already has an open window.
            if (TrySelectInOpenWindow(shell, folder, filePath))
            {
                return;
            }

            // Otherwise open one and give it a moment to initialize its shell view.
            shell.Open(folder);
            for (var attempt = 0; attempt < 25; attempt++)
            {
                Thread.Sleep(120);
                if (TrySelectInOpenWindow(shell, folder, filePath))
                {
                    return;
                }
            }
        }
        catch
        {
            // Best-effort only; the file is already saved.
        }
    }

    private static bool TrySelectInOpenWindow(dynamic shell, string folder, string filePath)
    {
        dynamic windows = shell.Windows();
        int count = windows.Count;
        for (var i = 0; i < count; i++)
        {
            object? windowObj = null;
            try
            {
                windowObj = windows.Item(i);
                if (windowObj is null)
                {
                    continue;
                }

                dynamic window = windowObj;

                // Filter out Internet Explorer windows and match on folder path.
                string? windowFolder;
                try
                {
                    windowFolder = window.Document?.Folder?.Self?.Path as string;
                }
                catch
                {
                    continue;
                }

                if (!string.Equals(windowFolder, folder, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (SelectViaShellView(windowObj, filePath))
                {
                    return true;
                }
            }
            catch
            {
                // Try the next window.
            }
            finally
            {
                if (windowObj is not null && Marshal.IsComObject(windowObj))
                {
                    Marshal.ReleaseComObject(windowObj);
                }
            }
        }

        return false;
    }

    private static bool SelectViaShellView(object windowObj, string filePath)
    {
        if (windowObj is not IServiceProvider provider)
        {
            return false;
        }

        var serviceGuid = SID_STopLevelBrowser;
        var browserGuid = IID_IShellBrowser;
        if (provider.QueryService(ref serviceGuid, ref browserGuid, out var browserObj) != 0 ||
            browserObj is not IShellBrowser browser)
        {
            return false;
        }

        if (browser.QueryActiveShellView(out var view) != 0 || view is null)
        {
            return false;
        }

        var pidl = IntPtr.Zero;
        try
        {
            if (SHParseDisplayName(filePath, IntPtr.Zero, out pidl, 0, out _) != 0 ||
                pidl == IntPtr.Zero)
            {
                return false;
            }

            // ILFindLastID returns the child (leaf) id within pidl, which is what
            // IShellView::SelectItem expects (a folder-relative PIDL).
            var child = ILFindLastID(pidl);
            return view.SelectItem(child, RenameFlags) == 0;
        }
        finally
        {
            if (pidl != IntPtr.Zero)
            {
                ILFree(pidl);
            }
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(
        string name, IntPtr bindingContext, out IntPtr pidl, uint sfgaoIn, out uint psfgaoOut);

    [DllImport("shell32.dll")]
    private static extern IntPtr ILFindLastID(IntPtr pidl);

    [DllImport("shell32.dll")]
    private static extern void ILFree(IntPtr pidl);

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("6d5140c1-7436-11ce-8034-00aa006009fa")]
    private interface IServiceProvider
    {
        [PreserveSig]
        int QueryService(ref Guid guidService, ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out object service);
    }

    // Only members up to (and including) QueryActiveShellView need real signatures;
    // earlier vtable slots are declared as stubs to keep the layout correct.
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214E2-0000-0000-C000-000000000046")]
    private interface IShellBrowser
    {
        void GetWindow(out IntPtr phwnd);
        void ContextSensitiveHelp(bool fEnterMode);
        void InsertMenusSB();
        void SetMenuSB();
        void RemoveMenusSB();
        void SetStatusTextSB();
        void EnableModelessSB();
        void TranslateAcceleratorSB();
        void BrowseObject();
        void GetViewStateStream();
        void GetControlWindow();
        void SendControlMsg();
        [PreserveSig]
        int QueryActiveShellView([MarshalAs(UnmanagedType.Interface)] out IShellView ppshv);
        void OnViewWindowActive();
        void SetToolbarItems();
    }

    // Only SelectItem needs a real signature; earlier slots are stubs.
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214E3-0000-0000-C000-000000000046")]
    private interface IShellView
    {
        void GetWindow(out IntPtr phwnd);
        void ContextSensitiveHelp(bool fEnterMode);
        void TranslateAccelerator();
        void EnableModeless();
        void UIActivate();
        void Refresh();
        void CreateViewWindow();
        void DestroyViewWindow();
        void GetCurrentInfo();
        void AddPropertySheetPages();
        void SaveViewState();
        [PreserveSig]
        int SelectItem(IntPtr pidlItem, uint uFlags);
        void GetItemObject();
    }
}
