using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace ControllerBattery.Behaviors;

public static class SmoothScrollBehavior
{
    private const double WheelDistance = 92;

    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(SmoothScrollBehavior),
        new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly DependencyProperty AnimatedOffsetProperty = DependencyProperty.RegisterAttached(
        "AnimatedOffset", typeof(double), typeof(SmoothScrollBehavior),
        new PropertyMetadata(0d, OnAnimatedOffsetChanged));

    private static readonly DependencyProperty TargetOffsetProperty = DependencyProperty.RegisterAttached(
        "TargetOffset", typeof(double), typeof(SmoothScrollBehavior), new PropertyMetadata(0d));

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is not ScrollViewer viewer) return;
        if ((bool)args.NewValue)
        {
            viewer.PreviewMouseWheel += Viewer_PreviewMouseWheel;
            viewer.ScrollChanged += Viewer_ScrollChanged;
        }
        else
        {
            viewer.PreviewMouseWheel -= Viewer_PreviewMouseWheel;
            viewer.ScrollChanged -= Viewer_ScrollChanged;
        }
    }

    private static void Viewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer viewer || viewer.ScrollableHeight <= 0) return;
        e.Handled = true;

        var previousTarget = (double)viewer.GetValue(TargetOffsetProperty);
        var start = Math.Abs(previousTarget - viewer.VerticalOffset) > WheelDistance * 4
            ? viewer.VerticalOffset
            : previousTarget;
        var target = Math.Clamp(start - Math.Sign(e.Delta) * WheelDistance, 0, viewer.ScrollableHeight);
        viewer.SetValue(TargetOffsetProperty, target);

        if (!SystemParameters.ClientAreaAnimation)
        {
            viewer.ScrollToVerticalOffset(target);
            return;
        }

        viewer.SetValue(AnimatedOffsetProperty, viewer.VerticalOffset);
        var animation = new DoubleAnimation
        {
            From = viewer.VerticalOffset,
            To = target,
            Duration = TimeSpan.FromMilliseconds(190),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        viewer.BeginAnimation(AnimatedOffsetProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private static void Viewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is ScrollViewer viewer && e.VerticalChange != 0 &&
            viewer.GetAnimationBaseValue(AnimatedOffsetProperty).Equals(viewer.GetValue(AnimatedOffsetProperty)))
        {
            viewer.SetValue(TargetOffsetProperty, viewer.VerticalOffset);
        }
    }

    private static void OnAnimatedOffsetChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is ScrollViewer viewer)
            viewer.ScrollToVerticalOffset((double)args.NewValue);
    }
}
