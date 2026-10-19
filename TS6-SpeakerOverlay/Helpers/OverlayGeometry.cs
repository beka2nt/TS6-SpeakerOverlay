using System.Windows;
using TS6_SpeakerOverlay.Models;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace TS6_SpeakerOverlay.Helpers;

internal enum DragAxis { None, Horizontal, Vertical }

internal static class OverlayGeometry
{
    internal static (VerticalAlignment Vertical, HorizontalAlignment Horizontal) AlignmentFor(OverlayPosition preset) => preset switch
    {
        OverlayPosition.TopLeft => (VerticalAlignment.Top, HorizontalAlignment.Left),
        OverlayPosition.TopCenter => (VerticalAlignment.Top, HorizontalAlignment.Center),
        OverlayPosition.TopRight => (VerticalAlignment.Top, HorizontalAlignment.Right),
        OverlayPosition.CenterLeft => (VerticalAlignment.Center, HorizontalAlignment.Left),
        OverlayPosition.CenterRight => (VerticalAlignment.Center, HorizontalAlignment.Right),
        OverlayPosition.BottomLeft => (VerticalAlignment.Bottom, HorizontalAlignment.Left),
        OverlayPosition.BottomCenter => (VerticalAlignment.Bottom, HorizontalAlignment.Center),
        OverlayPosition.BottomRight => (VerticalAlignment.Bottom, HorizontalAlignment.Right),
        _ => throw new ArgumentOutOfRangeException(nameof(preset))
    };

    internal static Point PresetPosition(Rect area, Size window, OverlayPosition preset, double gap)
    {
        gap = Math.Clamp(double.IsFinite(gap) ? gap : 0, 0, Math.Max(0, Math.Min(area.Width, area.Height) / 2));
        var alignment = AlignmentFor(preset);
        double x = alignment.Horizontal switch
        {
            HorizontalAlignment.Left => area.Left + gap,
            HorizontalAlignment.Right => area.Right - gap - window.Width,
            _ => area.Left + (area.Width - window.Width) / 2
        };
        double y = alignment.Vertical switch
        {
            VerticalAlignment.Top => area.Top + gap,
            VerticalAlignment.Bottom => area.Bottom - gap - window.Height,
            _ => area.Top + (area.Height - window.Height) / 2
        };
        return new Point(x, y);
    }

    internal static Point Snap(Point position, Size window, Rect area, double gap, double threshold,
        bool horizontal = true, bool vertical = true)
    {
        static double SnapAxis(double value, double first, double last, double limit)
        {
            double target = Math.Abs(value - first) <= Math.Abs(value - last) ? first : last;
            return Math.Abs(value - target) <= limit ? target : value;
        }
        return new Point(
            horizontal ? SnapAxis(position.X, area.Left + gap, area.Right - gap - window.Width, threshold) : position.X,
            vertical ? SnapAxis(position.Y, area.Top + gap, area.Bottom - gap - window.Height, threshold) : position.Y);
    }

    internal static Point KeepVisible(Point position, Size window, Rect area, double minimumVisible)
    {
        double visibleWidth = Math.Min(minimumVisible, Math.Min(window.Width, area.Width));
        double visibleHeight = Math.Min(minimumVisible, Math.Min(window.Height, area.Height));
        return new Point(
            Math.Clamp(double.IsFinite(position.X) ? position.X : area.Left, area.Left - window.Width + visibleWidth, area.Right - visibleWidth),
            Math.Clamp(double.IsFinite(position.Y) ? position.Y : area.Top, area.Top - window.Height + visibleHeight, area.Bottom - visibleHeight));
    }
}

internal sealed class OverlayDragSession
{
    private Point _originCursor;
    private Point _originWindow;
    private bool _shiftHeld;
    private readonly double _threshold;
    internal DragAxis Axis { get; private set; }

    internal OverlayDragSession(Point cursor, Point window, bool shiftHeld, double threshold)
    {
        _originCursor = cursor;
        _originWindow = window;
        _shiftHeld = shiftHeld;
        _threshold = threshold;
    }

    internal Point Move(Point cursor, bool shiftHeld, Point actualWindowPosition)
    {
        // Rebase when Shift changes: do not apply the accumulated perpendicular
        // cursor displacement, or a previously snapped coordinate, as a jump.
        if (_shiftHeld != shiftHeld)
        {
            _originCursor = cursor;
            _originWindow = actualWindowPosition;
            _shiftHeld = shiftHeld;
            Axis = DragAxis.None;
            return actualWindowPosition;
        }
        Vector delta = cursor - _originCursor;
        if (shiftHeld)
        {
            if (Axis == DragAxis.None)
            {
                if (Math.Max(Math.Abs(delta.X), Math.Abs(delta.Y)) < _threshold) return _originWindow;
                Axis = Math.Abs(delta.X) >= Math.Abs(delta.Y) ? DragAxis.Horizontal : DragAxis.Vertical;
            }
            if (Axis == DragAxis.Horizontal) delta.Y = 0;
            else delta.X = 0;
        }
        return _originWindow + delta;
    }
}
