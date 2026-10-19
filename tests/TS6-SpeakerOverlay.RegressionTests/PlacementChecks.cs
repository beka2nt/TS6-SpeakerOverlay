using System.IO;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TS6_SpeakerOverlay;
using TS6_SpeakerOverlay.Helpers;
using TS6_SpeakerOverlay.Models;
using TS6_SpeakerOverlay.Services;
using TS6_SpeakerOverlay.ViewModels;
using TS6_SpeakerOverlay.Views;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

internal static class PlacementChecks
{
    private static int _checks;
    private static void Check(bool success, string name)
    {
        if (!success) throw new InvalidOperationException("FAIL: " + name);
        _checks++;
        Console.WriteLine("PASS: " + name);
    }
    private static bool Near(double a, double b) => Math.Abs(a - b) < 0.1;

    internal static int Run()
    {
        GeometryTests();
        DragTests();
        ConfigTests();
        ActualLayoutTests();
        NativePlacementTests();
        NativeDragTests();
        return _checks;
    }

    private static void GeometryTests()
    {
        var area = new Rect(0, 0, 1920, 1040);
        var window = new Size(320, 600);
        Point[] expected = [new(16,16),new(800,16),new(1584,16),new(16,220),new(1584,220),new(16,424),new(800,424),new(1584,424)];
        foreach (OverlayPosition preset in Enum.GetValues<OverlayPosition>())
            Check(OverlayGeometry.PresetPosition(area, window, preset, 16) == expected[(int)preset], preset + " preset coordinates");
        var leftMonitor = new Rect(-2560, -200, 2560, 1400);
        Check(OverlayGeometry.PresetPosition(leftMonitor, window, OverlayPosition.CenterLeft, 20) == new Point(-2540,200),
            "negative-coordinate secondary monitor uses its own center");
        var upperMonitor = new Rect(100, -1080, 1920, 1080);
        Check(OverlayGeometry.PresetPosition(upperMonitor, window, OverlayPosition.BottomRight, 0) == new Point(1700,-600),
            "monitor above primary supports right/bottom anchors");
        foreach (double scale in new[] { 1D, 1.25, 1.5, 2D })
        {
            var scaledWindow = new Size(320 * scale, 600 * scale);
            Point atEdge = OverlayGeometry.PresetPosition(area, scaledWindow, OverlayPosition.CenterLeft, 16 * scale);
            Check(Near(atEdge.X, 16 * scale) && Near(atEdge.Y + scaledWindow.Height / 2, area.Height / 2),
                $"preset gap and center respect {scale * 100}% DPI");
        }
        Check(OverlayGeometry.Snap(new Point(22,25), window, area,16,12) == new Point(16,16), "snap inside threshold with retained gap");
        Check(OverlayGeometry.Snap(new Point(29,29), window, area,16,12) == new Point(29,29), "outside threshold is not snapped");
        Check(OverlayGeometry.Snap(new Point(1580,420), window, area,16,12) == new Point(1584,424), "right/bottom edges snap symmetrically");
        Check(OverlayGeometry.Snap(new Point(10,10), window, area,16,12,true,false) == new Point(16,10), "horizontal lock preserves exact vertical gap");
        Check(OverlayGeometry.Snap(new Point(10,10), window, area,16,12,false,true) == new Point(10,16), "vertical lock preserves exact horizontal gap");
        Check(OverlayGeometry.Snap(new Point(7,220), window,area,0,12) == new Point(0,220), "zero gap snaps to actual edge");
        Point recovered = OverlayGeometry.KeepVisible(new Point(9000,9000),window,leftMonitor,48);
        Check(new Rect(recovered,window).IntersectsWith(leftMonitor), "off-screen recovery uses a real monitor, not virtual desktop gaps");
        Check(OverlayGeometry.KeepVisible(new Point(double.NaN,double.PositiveInfinity),window,area,48) == new Point(0,0), "invalid saved coordinates recover safely");
        Point oversized = OverlayGeometry.PresetPosition(new Rect(0,0,640,480),new Size(320,600),OverlayPosition.BottomCenter,16);
        Check(Near(oversized.Y + 600, 464), "oversized transparent window retains visible bottom content anchor");
    }

    private static void DragTests()
    {
        var start = new Point(100,100);
        var session = new OverlayDragSession(new Point(0,0),start,true,4);
        Check(session.Move(new Point(2,1),true,start) == start && session.Axis == DragAxis.None, "Shift ignores initial pointer jitter");
        Point horizontal = session.Move(new Point(8,2),true,start);
        Check(horizontal == new Point(108,100) && session.Axis == DragAxis.Horizontal, "first horizontal movement selects horizontal axis");
        Point continued = session.Move(new Point(20,80),true,horizontal);
        Check(continued == new Point(120,100), "later diagonal movement cannot change locked axis");
        Point released = session.Move(new Point(21,81),false,continued);
        Check(released == continued, "releasing Shift holds the exact current position");
        Check(session.Move(new Point(24,85),false,released) == new Point(123,104), "free drag resumes relative to Shift release, not accumulated offset");
        var delayedRelease = new OverlayDragSession(new Point(0,0),start,true,4);
        Point locked = delayedRelease.Move(new Point(30,100),true,start);
        Check(delayedRelease.Move(new Point(200,300),false,locked)==locked,
            "delayed modifier sample never applies cursor movement from before release");
        var verticalSession = new OverlayDragSession(new Point(50,50),start,true,4);
        Check(verticalSession.Move(new Point(51,59),true,start) == new Point(100,109) && verticalSession.Axis == DragAxis.Vertical,
            "first vertical movement selects vertical axis");
        var midDrag = new OverlayDragSession(new Point(0,0),start,false,4);
        Point moved = midDrag.Move(new Point(40,30),false,start);
        Check(moved == new Point(140,130), "normal drag uses absolute cursor displacement");
        Check(midDrag.Move(new Point(41,31),true,moved) == moved, "pressing Shift mid-drag does not jump");
        Check(midDrag.Move(new Point(42,38),true,moved) == new Point(140,137), "mid-drag Shift selects a new vertical axis");
        var snappedSession = new OverlayDragSession(new Point(0,0),new Point(20,200),false,4);
        snappedSession.Move(new Point(4,0),false,new Point(20,200));
        Check(snappedSession.Move(new Point(30,0),false,new Point(16,200)) == new Point(50,200),
            "snap correction does not accumulate cursor drift or sticky movement");
        Check(snappedSession.Move(new Point(31,1),true,new Point(16,200)) == new Point(16,200),
            "Shift rebases from actual snapped position");
    }

    private static void ConfigTests()
    {
        var config = JsonSerializer.Deserialize<AppConfig>("{\"WindowLeft\":70,\"WindowTop\":80}")!;
        Check(config.UserListAlignment == VerticalAlignment.Top && config.UserListHorizontalAlignment == HorizontalAlignment.Left,
            "old configuration retains top/left defaults");
        config.UserListAlignment = VerticalAlignment.Center;
        config.UserListHorizontalAlignment = HorizontalAlignment.Right;
        config.PositionPreset = OverlayPosition.CenterRight;
        config.ScreenEdgeGap = 27;
        config.SnapToScreenEdges = false;
        var restored = JsonSerializer.Deserialize<AppConfig>(JsonSerializer.Serialize(config))!;
        Check(restored.UserListAlignment == VerticalAlignment.Center && restored.UserListHorizontalAlignment == HorizontalAlignment.Right
            && restored.PositionPreset == OverlayPosition.CenterRight && restored.ScreenEdgeGap == 27 && !restored.SnapToScreenEdges,
            "alignment, preset, gap and snapping persist through config roundtrip");
        config.ScreenEdgeGap = double.NaN;
        Check(config.ScreenEdgeGap == 16, "invalid gap normalizes safely");
        config.ScreenEdgeGap = 999;
        Check(config.ScreenEdgeGap == 80, "gap is bounded to UI range");
        config.UserListAlignment = VerticalAlignment.Stretch;
        Check(config.UserListAlignment == VerticalAlignment.Top, "unsupported list alignment falls back to Top");
        config.ResetDefaults();
        Check(config.PositionPreset == null && config.ScreenEdgeGap == 16 && config.SnapToScreenEdges, "reset restores positioning defaults");
        using var service = new Ts6Service(65534,false);
        using var vm = new MainViewModel(config,service,false);
        OverlayPosition? requested = null;
        vm.PositionPresetRequested += value => requested = value;
        foreach (OverlayPosition preset in Enum.GetValues<OverlayPosition>())
        {
            vm.MoveOverlayCommand.Execute(preset);
            var alignment = OverlayGeometry.AlignmentFor(preset);
            Check(config.PositionPreset == preset && config.UserListAlignment == alignment.Vertical
                && config.UserListHorizontalAlignment == alignment.Horizontal && requested == preset,
                preset + " command applies alignment and requests window move");
        }
    }

    private static void Arrange(FrameworkElement root, double width = 320, double height = 600)
    {
        root.Measure(new Size(width,height));
        root.Arrange(new Rect(0,0,width,height));
        root.UpdateLayout();
    }

    private static void ActualLayoutTests()
    {
        foreach (double scale in new[] { 1D,1.5 })
        {
            using var service = new Ts6Service(65534,false);
            using var vm = new MainViewModel(new AppConfig { Language="en-US", UiScale=scale },service,false);
            vm.Users.Add(new User { ClientId=1,Name="Nova" });
            vm.Users.Add(new User { ClientId=2,Name="Mira" });
            var overlay = new MainWindow(vm);
            var root = (FrameworkElement)overlay.Content;
            var list = (ItemsControl)overlay.FindName("UserList");
            foreach (VerticalAlignment alignment in new[] { VerticalAlignment.Top, VerticalAlignment.Center, VerticalAlignment.Bottom })
            {
                vm.Config.UserListAlignment = alignment;
                Arrange(root);
                double y = list.TransformToAncestor(root).Transform(new Point()).Y;
                double expected = alignment switch { VerticalAlignment.Top=>0, VerticalAlignment.Center=>(root.ActualHeight-list.ActualHeight)/2, _=>root.ActualHeight-list.ActualHeight };
                Check(Near(y,expected), $"actual overlay {alignment} alignment at {scale * 100}% UI scale");
                if (alignment == VerticalAlignment.Bottom)
                {
                    var guide = (Border)overlay.FindName("PositionGuide");
                    double guideBottom = guide.TransformToAncestor(root).Transform(new Point()).Y + guide.ActualHeight;
                    Check(guideBottom <= y, "bottom-aligned members are not obscured by unlocked drag guide");
                }
            }
            vm.Config.UserListAlignment = VerticalAlignment.Center;
            Arrange(root);
            double before = list.TransformToAncestor(root).Transform(new Point()).Y;
            vm.Notifications.Add(new Notification { Message="Fixture notification",Color="#19B889",Icon="●" });
            vm.IsOverlayLocked = true;
            Arrange(root);
            Check(Near(before,list.TransformToAncestor(root).Transform(new Point()).Y), "notification and lock state do not shift centered list");
            vm.Users.RemoveAt(1);
            Arrange(root);
            double center = list.TransformToAncestor(root).Transform(new Point()).Y + list.ActualHeight / 2;
            Check(Near(center,root.ActualHeight/2), "smaller member list remains truly centered");
            vm.Config.UserListHorizontalAlignment = HorizontalAlignment.Right;
            Arrange(root);
            var presenter = (ContentPresenter)list.ItemContainerGenerator.ContainerFromIndex(0);
            var userRow = (FrameworkElement)list.ItemTemplate.FindName("UserRow",presenter);
            double right = userRow.TransformToAncestor(root).Transform(new Point()).X + userRow.ActualWidth;
            Check(Near(right,root.ActualWidth-8), "right anchor aligns actual member card, not transparent row space");
            double oneRowHeight = list.ActualHeight;
            vm.Users[0].IsTalking = true;
            vm.Users.Add(new User { ClientId=3,Name="Silent" });
            vm.Config.ShowOnlyTalking = true;
            Arrange(root);
            Check(Near(list.ActualHeight,oneRowHeight), "speaker-only mode centers visible rows without silent placeholders");
            overlay.Close();
        }
    }

    internal static void RenderPreviews(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (string language in new[] { "zh-CN","en-US","de-DE" })
        {
            using var service = new Ts6Service(65534,false);
            using var vm = new MainViewModel(new AppConfig { Language=language },service,false);
            vm.MoveOverlayCommand.Execute(OverlayPosition.CenterLeft);
            var settings = new SettingsWindow(vm);
            SettingsOrganizationChecks.Select(settings, "Position");
            var content = (FrameworkElement)settings.Content;
            Arrange(content,960,720);
            SaveImage(content,Path.Combine(directory,"settings-"+language+".png"),960,720);
            settings.Close();
        }
        foreach (VerticalAlignment alignment in new[] { VerticalAlignment.Top,VerticalAlignment.Center,VerticalAlignment.Bottom })
        {
            using var service = new Ts6Service(65534,false);
            using var vm = new MainViewModel(new AppConfig { Language="en-US",UserListAlignment=alignment },service,false);
            vm.ConnectionStatus = "";
            vm.Users.Add(new User { ClientId=1,Name="Nova",IsTalking=true });
            vm.Users.Add(new User { ClientId=2,Name="Mira" });
            var overlay = new MainWindow(vm);
            var content = (FrameworkElement)overlay.Content;
            Arrange(content);
            SaveImage(content,Path.Combine(directory,"overlay-"+alignment+".png"),320,600);
            overlay.Close();
        }
    }

    private static void SaveImage(FrameworkElement root,string path,int width,int height)
    {
        var bitmap = new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static void NativePlacementTests()
    {
        using var service = new Ts6Service(65534,false);
        using var vm = new MainViewModel(new AppConfig { Language="en-US" },service,false);
        var overlay = new MainWindow(vm);
        IntPtr handle = new WindowInteropHelper(overlay).EnsureHandle();
        var work = System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
        double scale = GetDpiForWindow(handle) / 96D;
        vm.MoveOverlayCommand.Execute(OverlayPosition.CenterLeft);
        Check(GetWindowRect(handle,out NativeRect left) && Near(left.Left,Math.Round(work.Left + 16 * scale)),
            "hidden native window reaches left preset using current DPI");
        Check(Math.Abs((left.Top + left.Bottom) / 2D - (work.Top + work.Bottom) / 2D) <= 0.5,
            "hidden native window is centered in real work area");
        vm.MoveOverlayCommand.Execute(OverlayPosition.BottomRight);
        GetWindowRect(handle,out NativeRect right);
        Check(Near(right.Right,Math.Round(work.Right - 16 * scale)) && Near(right.Bottom,Math.Round(work.Bottom - 16 * scale)),
            "native bottom/right preset preserves edge gap and excludes taskbar");
        vm.Config.ScreenEdgeGap = 28;
        GetWindowRect(handle,out NativeRect changed);
        Check(Near(changed.Right,Math.Round(work.Right - 28 * scale)) && Near(changed.Bottom,Math.Round(work.Bottom - 28 * scale)),
            "gap changes reposition the selected preset immediately");
        vm.Config.UserListAlignment = VerticalAlignment.Center;
        vm.Config.ScreenEdgeGap = 30;
        Check(vm.Config.UserListAlignment == VerticalAlignment.Center, "gap adjustment preserves manual alignment override");
        Check(!overlay.IsVisible, "native placement checks never show or activate a test window");
        overlay.Close();
    }

    private static void NativeDragTests()
    {
        using var service = new Ts6Service(65534,false);
        using var vm = new MainViewModel(new AppConfig { Language="en-US", SnapToScreenEdges=false },service,false);
        var overlay = new MainWindow(vm);
        IntPtr handle = new WindowInteropHelper(overlay).EnsureHandle();
        var work = System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
        int x = work.Left + 100, y = work.Top + 100;
        SetWindowPos(handle,IntPtr.Zero,x,y,0,0,0x0015);
        OverlayPointerState input = new(new Point(x+20,y+20),true);
        using var controller = new OverlayPlacementController(overlay,vm.Config,()=>input);
        Check(controller.BeginDrag(), "native drag starts from injected pointer snapshot without keyboard focus");
        input = input with { Cursor=input.Cursor+new Vector(40,3) };
        controller.Drag();
        GetWindowRect(handle,out NativeRect horizontal);
        Check(horizontal.Left==x+40 && horizontal.Top==y, "controller moves real HWND horizontally with Shift");
        input = input with { Cursor=input.Cursor+new Vector(20,90) };
        controller.Drag();
        GetWindowRect(handle,out NativeRect diagonal);
        Check(diagonal.Left==x+60 && diagonal.Top==y, "controller freezes vertical HWND coordinate despite diagonal input");
        input = input with { ShiftHeld=false };
        controller.Drag();
        GetWindowRect(handle,out NativeRect released);
        Check(released.Left==diagonal.Left && released.Top==diagonal.Top, "stationary Shift release leaves real HWND unchanged");
        input = input with { Cursor=input.Cursor+new Vector(5,6) };
        controller.Drag();
        GetWindowRect(handle,out NativeRect resumed);
        Check(resumed.Left==released.Left+5 && resumed.Top==released.Top+6, "controller resumes free HWND movement without accumulated 93px offset");
        controller.EndDrag();

        input = input with { ShiftHeld=true };
        controller.BeginDrag();
        input = input with { Cursor=input.Cursor+new Vector(2,40) };
        controller.Drag();
        GetWindowRect(handle,out NativeRect vertical);
        Check(vertical.Left==resumed.Left && vertical.Top==resumed.Top+40, "controller also selects vertical axis by first direction");
        controller.EndDrag();

        // Near an edge, releasing Shift must not silently snap the frozen axis.
        vm.Config.SnapToScreenEdges=true;
        int nearEdgeY = work.Top+(int)Math.Round(20*GetDpiForWindow(handle)/96D);
        SetWindowPos(handle,IntPtr.Zero,x,nearEdgeY,0,0,0x0015);
        input = new(new Point(x+20,nearEdgeY+20),true);
        controller.BeginDrag();
        input = input with { Cursor=input.Cursor+new Vector(40,2) };
        controller.Drag();
        input = input with { ShiftHeld=false };
        controller.Drag();
        controller.Drag(); // stationary timer sample after release
        GetWindowRect(handle,out NativeRect nearEdge);
        Check(nearEdge.Top==nearEdgeY, "Shift release and stationary polling cannot snap previously frozen axis");
        controller.EndDrag();
        GetWindowRect(handle,out NativeRect ended);
        Check(ended.Top==nearEdgeY, "mouse release does not introduce a late perpendicular snap");
        Check(OverlayPlacementController.ReadPointer() is not null, "production native pointer reader is available without overlay activation");
        Check(!overlay.IsVisible && !overlay.IsKeyboardFocused, "native drag regression runs without visible or focused test window");
        overlay.Close();
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left,Top,Right,Bottom; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr window,out NativeRect rectangle);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
}
