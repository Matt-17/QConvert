using System.Windows;

using QConvert.Core;

namespace QConvert
{
    internal static class WindowPlacement
    {
        public static void Restore(Window window, WindowPlacementSetting placement)
        {
            if (HasUsableBounds(placement)
                && IntersectsVirtualScreen(placement.Left!.Value, placement.Top!.Value, placement.Width!.Value, placement.Height!.Value))
            {
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = placement.Left.Value;
                window.Top = placement.Top.Value;
                window.Width = Math.Max(window.MinWidth, placement.Width.Value);
                window.Height = Math.Max(window.MinHeight, placement.Height.Value);
            }

            if (string.Equals(placement.State, "Maximized", StringComparison.OrdinalIgnoreCase))
            {
                window.WindowState = WindowState.Maximized;
            }
        }

        public static void Save(Window window, WindowPlacementSetting placement)
        {
            placement.State = window.WindowState == WindowState.Maximized ? "Maximized" : "Normal";

            if (window.WindowState != WindowState.Normal)
            {
                return;
            }

            if (!IsFinite(window.Left) || !IsFinite(window.Top) || !IsFinite(window.Width) || !IsFinite(window.Height))
            {
                return;
            }

            placement.Left = window.Left;
            placement.Top = window.Top;
            placement.Width = window.Width;
            placement.Height = window.Height;
        }

        private static bool HasUsableBounds(WindowPlacementSetting placement) =>
            IsFinite(placement.Left)
            && IsFinite(placement.Top)
            && IsFinite(placement.Width)
            && IsFinite(placement.Height)
            && placement.Width > 0
            && placement.Height > 0;

        private static bool IntersectsVirtualScreen(double left, double top, double width, double height)
        {
            const double minimumVisibleSize = 80;
            var saved = new Rect(left, top, width, height);
            var screen = new Rect(
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight);

            saved.Intersect(screen);
            return saved.Width >= minimumVisibleSize && saved.Height >= minimumVisibleSize;
        }

        private static bool IsFinite(double? value) =>
            value is not null && double.IsFinite(value.Value);
    }
}
