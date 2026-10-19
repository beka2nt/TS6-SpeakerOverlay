using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using TS6_SpeakerOverlay.Models;
using Point = System.Windows.Point;

namespace TS6_SpeakerOverlay.Helpers;

internal readonly record struct OverlayPointerState(Point Cursor, bool ShiftHeld);

internal sealed class OverlayPlacementController : IDisposable
{
    private readonly Window _window;
    private readonly AppConfig _config;
    private readonly Func<OverlayPointerState?> _readPointer;
    private OverlayDragSession? _drag;
    private Point _dragStart;
    private bool _dragStarted;
    private bool _lastShiftHeld;
    private bool _snapHorizontal;
    private bool _snapVertical;

    internal OverlayPlacementController(Window window, AppConfig config, Func<OverlayPointerState?>? readPointer = null)
    {
        _window = window;
        _config = config;
        _readPointer = readPointer ?? ReadPointer;
        config.PropertyChanged += ConfigChanged;
    }

    private IntPtr Handle => new WindowInteropHelper(_window).Handle;

    internal void ApplyPreset(OverlayPosition preset)
    {
        IntPtr handle = Handle;
        if (handle == IntPtr.Zero || !GetWindowRect(handle, out NativeRect window)) return;
        IntPtr monitor = MonitorFromWindow(handle, 2);
        if (!TryGetArea(monitor, out Rect area)) return;
        double scale = DpiScale(handle);
        Point position = OverlayGeometry.PresetPosition(area, window.Bounds.Size, preset, _config.ScreenEdgeGap * scale);
        MoveWindow(position);
        SavePosition();
    }

    internal bool BeginDrag()
    {
        if (_readPointer() is not OverlayPointerState input || !GetWindowRect(Handle, out NativeRect window)) return false;
        _dragStart = input.Cursor;
        _lastShiftHeld = input.ShiftHeld;
        _dragStarted = false;
        _snapHorizontal = _snapVertical = false;
        _drag = new OverlayDragSession(input.Cursor, window.Bounds.TopLeft, input.ShiftHeld,
            4 * DpiScale(Handle));
        return true;
    }

    internal void Drag()
    {
        if (_drag == null || _readPointer() is not OverlayPointerState input || !GetWindowRect(Handle, out NativeRect window)) return;
        Point cursor = input.Cursor;
        bool shiftHeld = input.ShiftHeld;
        _lastShiftHeld = shiftHeld;
        IntPtr monitor = MonitorFromPoint(new NativePoint { X = (int)Math.Round(cursor.X), Y = (int)Math.Round(cursor.Y) }, 2);
        double scale = DpiScale(Handle);
        if (!_dragStarted)
        {
            if ((cursor - _dragStart).Length < 4 * scale)
            {
                // Observe modifier changes even before the drag threshold is crossed.
                _drag.Move(cursor, shiftHeld, window.Bounds.TopLeft);
                return;
            }
            _dragStarted = true;
            _config.PositionPreset = null;
        }
        Point candidate = _drag.Move(cursor, shiftHeld, window.Bounds.TopLeft);
        // A modifier-only sample must not snap the newly released axis. Keep
        // the exact coordinate until the user actually moves along that axis.
        _snapHorizontal = candidate.X != window.Left && (!shiftHeld || _drag.Axis == DragAxis.Horizontal);
        _snapVertical = candidate.Y != window.Top && (!shiftHeld || _drag.Axis == DragAxis.Vertical);
        if (_config.SnapToScreenEdges && TryGetArea(monitor, out Rect area))
            candidate = OverlayGeometry.Snap(candidate, window.Bounds.Size, area, _config.ScreenEdgeGap * scale, 12 * scale,
                _snapHorizontal, _snapVertical);
        if (candidate != window.Bounds.TopLeft) MoveWindow(candidate);
    }

    internal void EndDrag()
    {
        if (_drag == null) return;
        // Re-evaluate the final DPI/size after a monitor transition, without
        // moving the frozen axis of a Shift-constrained drag.
        if (_dragStarted && _config.SnapToScreenEdges && GetWindowRect(Handle, out NativeRect window))
        {
            IntPtr monitor = MonitorFromWindow(Handle, 2);
            if (TryGetArea(monitor, out Rect area))
            {
                double scale = DpiScale(Handle);
                MoveWindow(OverlayGeometry.Snap(window.Bounds.TopLeft, window.Bounds.Size, area, _config.ScreenEdgeGap * scale, 12 * scale,
                    _snapHorizontal && (!_lastShiftHeld || _drag.Axis == DragAxis.Horizontal),
                    _snapVertical && (!_lastShiftHeld || _drag.Axis == DragAxis.Vertical)));
            }
        }
        _drag = null;
        if (_dragStarted) EnsureVisible();
        SavePosition();
    }

    internal void EnsureVisible()
    {
        IntPtr handle = Handle;
        IntPtr monitor = MonitorFromWindow(handle, 2);
        if (!GetWindowRect(handle, out NativeRect window) || !TryGetArea(monitor, out Rect area)) return;
        MoveWindow(OverlayGeometry.KeepVisible(window.Bounds.TopLeft, window.Bounds.Size, area, 48 * DpiScale(handle)));
        SavePosition();
    }

    private void ConfigChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppConfig.ScreenEdgeGap) && _config.PositionPreset is OverlayPosition preset)
            ApplyPreset(preset);
    }

    private void SavePosition()
    {
        if (double.IsFinite(_window.Left)) _config.WindowLeft = _window.Left;
        if (double.IsFinite(_window.Top)) _config.WindowTop = _window.Top;
    }

    private void MoveWindow(Point position)
    {
        // Use one native coordinate space for cursor, monitor and window bounds.
        // WPF then updates its DIP Left/Top bindings from the resulting WM_MOVE.
        SetWindowPos(Handle, IntPtr.Zero, (int)Math.Round(position.X), (int)Math.Round(position.Y), 0, 0, 0x0001 | 0x0004 | 0x0010);
    }

    private static bool TryGetArea(IntPtr monitor, out Rect area)
    {
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        bool success = GetMonitorInfo(monitor, ref info);
        area = success ? info.Work.Bounds : Rect.Empty;
        return success && !area.IsEmpty && area.Width > 0 && area.Height > 0;
    }

    private static double DpiScale(IntPtr window)
    {
        uint dpi = GetDpiForWindow(window);
        return dpi > 0 ? dpi / 96D : 1;
    }

    internal static OverlayPointerState? ReadPointer()
    {
        if (!GetCursorPos(out NativePoint cursor)) return null;
        // WPF's queued keyboard state can be stale when another application has
        // focus. Only the high bit represents a currently held physical key.
        bool shiftHeld = GetAsyncKeyState(0xA0) < 0 || GetAsyncKeyState(0xA1) < 0;
        return new OverlayPointerState(cursor.Point, shiftHeld);
    }

    public void Dispose()
    {
        _drag = null;
        _config.PropertyChanged -= ConfigChanged;
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativePoint
    {
        public int X, Y;
        public readonly Point Point => new(X, Y);
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
        public readonly Rect Bounds => new(Left, Top, Right - Left, Bottom - Top);
    }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor, Work;
        public uint Flags;
    }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int virtualKey);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr handle, out NativeRect rectangle);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr handle, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
}
