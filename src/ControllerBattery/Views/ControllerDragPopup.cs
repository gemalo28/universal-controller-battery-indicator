using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace ControllerBattery;

internal sealed class ControllerDragPopup : Popup
{
    private readonly Visual _dpiSource;

    internal ControllerDragPopup(Visual dpiSource, FrameworkElement source)
    {
        _dpiSource = dpiSource;
        AllowsTransparency = true;
        IsHitTestVisible = false;
        StaysOpen = true;
        Placement = PlacementMode.AbsolutePoint;
        PopupAnimation = PopupAnimation.None;

        var dpi = VisualTreeHelper.GetDpi(source);
        var snapshot = new RenderTargetBitmap(
            Math.Max(1, (int)Math.Ceiling(source.ActualWidth * dpi.DpiScaleX)),
            Math.Max(1, (int)Math.Ceiling(source.ActualHeight * dpi.DpiScaleY)),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        snapshot.Render(source);

        Child = new Border
        {
            Width = source.ActualWidth,
            Height = source.ActualHeight,
            CornerRadius = new CornerRadius(12),
            BorderBrush = new SolidColorBrush(Color.FromRgb(139, 124, 246)),
            BorderThickness = new Thickness(2),
            Background = new ImageBrush(snapshot) { Stretch = Stretch.Fill },
            Opacity = 0.94,
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(10, 8, 18),
                BlurRadius = 20,
                ShadowDepth = 7,
                Opacity = 0.72
            }
        };
    }

    internal void MoveToCursor()
    {
        if (!GetCursorPos(out var cursor)) return;
        var screenPoint = new Point(cursor.X, cursor.Y);
        if (PresentationSource.FromVisual(_dpiSource)?.CompositionTarget is { } target)
            screenPoint = target.TransformFromDevice.Transform(screenPoint);
        // Keep the popup away from the cursor. A Popup owns a separate HWND;
        // placing it under the pointer prevents the underlying tile from
        // receiving native drag/drop events even when WPF hit testing is off.
        HorizontalOffset = screenPoint.X + 18;
        VerticalOffset = screenPoint.Y + 18;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        internal int X;
        internal int Y;
    }
}
