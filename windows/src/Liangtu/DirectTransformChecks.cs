using System.Windows.Interop;
namespace Liangtu;

internal static class DirectTransformChecks
{
    [STAThread]
    public static int Main(string[] args)
    {
        Settings.Root = Path.GetFullPath(args[0]); Directory.CreateDirectory(Settings.Root);
        Directory.CreateDirectory(Path.Combine(Settings.Root, "ui"));
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        typeof(App).GetProperty(nameof(App.Settings))!.SetValue(app, new Settings());
        var bitmap = BitmapSource.Create(640, 480, 96, 96, PixelFormats.Bgra32, null, Enumerable.Repeat((byte)255, 640 * 480 * 4).ToArray(), 640 * 4);
        var path = Path.Combine(Settings.Root, "direct-fixture.png"); MediaTools.SavePng(bitmap, path);
        var photo = new Photo { Path = path }; var lines = new List<string>();
        void Test(string name, Action run) { try { run(); lines.Add("PASS " + name); } catch (Exception e) { lines.Add("FAIL " + name + ": " + e); } }
        Run(app, photo, Test);
        ObjectChecks.Run(app, photo, Test); EditorChecks.Run(Test); EditorChecks.RunUi(app, photo, Test); TextChecks.Run(app, photo, Test);
        File.WriteAllLines(Path.Combine(Settings.Root, "results.txt"), lines);
        return lines.Any(l => l.StartsWith("FAIL")) ? 1 : 0;
    }
    static void Run(App app, Photo photo, Action<string, Action> test)
    {
        test("drawn object auto-selects and direct drag works without selection tool", () => {
            var editor = new EditorWindow(app, photo);
            try {
                editor.SelectTool("矩形"); editor.BeginStroke(new Point(100, 100)); editor.EndStroke(new Point(250, 180));
                var original = editor.Shapes.Single();
                Assert(editor.SelectedShape == original.Id, "Newly drawn object must be selected");
                editor.BeginStroke(new Point(100, 140)); editor.EndStroke(new Point(130, 160));
                Assert(editor.Shapes.Count == 1 && editor.Shapes[0].Bounds.TopLeft == original.Bounds.TopLeft + new Vector(30, 20), "Direct drag must move, not draw");
                editor.Undo(); Assert(editor.Shapes.Single() == original, "One undo must restore complete drag");
            } finally { Clean(editor); }
        });
        test("selection tool is absent from toolbar", () => {
            var editor = new EditorWindow(app, photo);
            try {
                var root = (FrameworkElement)editor.Content; root.Measure(new Size(900, 640)); root.Arrange(new Rect(0, 0, 900, 640));
                Assert(!TextChecks.Descendants(root).OfType<RadioButton>().Any(b => ((string?)b.ToolTip)?.StartsWith("选择 ·") == true), "Redundant selection toolbar button remains");
            } finally { Clean(editor); }
        });
        test("drag selected frame interior moves the object and Escape cancels", () => {
            var editor = new EditorWindow(app, photo);
            try {
                Draw(editor); var original = editor.Shapes.Single();
                editor.BeginStroke(new Point(175, 140)); editor.EndStroke(new Point(195, 160));
                Assert(editor.Shapes.Count == 1 && editor.Shapes[0].Bounds.TopLeft == original.Bounds.TopLeft + new Vector(20, 20), "Selected frame interior must drag the object");
                var moved = editor.Shapes.Single();
                var origin = Center(Handle(editor, "BottomRight")); editor.BeginStroke(origin); editor.MoveStroke(origin + new Vector(80, 50));
                using var source = new HwndSource(new HwndSourceParameters("Direct transform keyboard regression") { Width = 1, Height = 1, WindowStyle = 0 });
                typeof(EditorWindow).GetMethod("OnKey", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(editor,
                    new object[] { editor, new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent } });
                Assert(editor.Shapes.Single() == moved, "Escape must cancel resizing");
            } finally { Clean(editor); }
        });
        test("direct selection takes precedence and Alt draws through, empty space continues drawing", () => {
            var editor = new EditorWindow(app, photo);
            try {
                Draw(editor); var original = editor.Shapes.Single();
                editor.SelectTool("箭头"); editor.BeginStroke(new Point(100, 140)); editor.EndStroke(new Point(120, 150));
                Assert(editor.Shapes.Count == 1 && editor.SelectedShape == original.Id, "Drawing mode must select existing ink");
                editor.BeginStroke(new Point(120, 150), true); editor.EndStroke(new Point(350, 250));
                Assert(editor.Shapes.Count == 2, "Alt must allow an overlapping drawing");
                editor.BeginStroke(new Point(400, 350)); editor.EndStroke(new Point(480, 380));
                Assert(editor.Shapes.Count == 3, "Empty canvas must continue the current drawing mode");
                editor.Viewport.SpaceHeld = true; var before = Pixels(editor.Flatten());
                editor.BeginStroke(new Point(120, 150)); editor.EndStroke(new Point(220, 250));
                Assert(before.SequenceEqual(Pixels(editor.Flatten())), "Space-pan must take precedence"); editor.Viewport.SpaceHeld = false;
            } finally { Clean(editor); }
        });
        test("four resize corners operate from original snapshot, constrain and commit once", () => {
            foreach (var corner in new[] { "TopLeft", "TopRight", "BottomRight", "BottomLeft" }) {
                var editor = new EditorWindow(app, photo);
                try {
                    Draw(editor); var original = editor.Shapes.Single(); var before = Pixels(editor.Flatten());
                    var handle = Handle(editor, corner); var origin = Center(handle);
                    var delta = new Vector(corner.Contains("Left") ? -70 : 70, corner.Contains("Top") ? -10 : 10);
                    editor.BeginStroke(origin); editor.MoveStroke(origin + delta / 2); editor.EndStroke(origin + delta, true);
                    var resized = editor.Shapes.Single();
                    var geometry = original.Geometry.Bounds; var after = resized.Geometry.Bounds;
                    Assert(Math.Abs(after.Width / geometry.Width - after.Height / geometry.Height) < .0001 && after.Width > geometry.Width, "Shift must preserve shape proportions");
                    editor.Undo(); Assert(before.SequenceEqual(Pixels(editor.Flatten())), "Resize previews must produce one undo step");
                    editor.Redo(); Assert(editor.Shapes.Single() == resized, "Redo must restore complete resize");
                } finally { Clean(editor); }
            }
        });
        test("corner resize allows nonuniform scale and prevents flips, zero and invalid geometry", () => {
            var editor = new EditorWindow(app, photo);
            try {
                Draw(editor); var original = editor.Shapes.Single(); var origin = Center(Handle(editor, "BottomRight"));
                editor.BeginStroke(origin); editor.MoveStroke(origin + new Vector(100, 20));
                var resized = editor.Shapes.Single().Geometry.Bounds;
                Assert(Math.Abs(resized.Width / original.Geometry.Bounds.Width - resized.Height / original.Geometry.Bounds.Height) > .1, "Unconstrained shape must resize independently");
                editor.MoveStroke(new Point(double.NaN, 0)); Assert(editor.Shapes.Single().Geometry.Bounds == resized, "NaN pointer must be ignored");
                editor.EndStroke(origin - new Vector(1000, 1000));
                var final = editor.Shapes.Single(); Assert(final.Geometry.Bounds.Width > 0 && final.Geometry.Bounds.Height > 0 && final.Geometry.Transform.Value.Determinant > 0, "Crossing opposite anchor must not flip or collapse object");
            } finally { Clean(editor); }
        });
        test("rotation uses original center and snapshot and Shift snaps to 15 degrees", () => {
            var editor = new EditorWindow(app, photo);
            try {
                Draw(editor); var original = editor.Shapes.Single(); var before = Pixels(editor.Flatten());
                var center = new Point(original.Bounds.X + original.Bounds.Width / 2, original.Bounds.Y + original.Bounds.Height / 2);
                var origin = Center(Handle(editor, "Rotate")); var end = new RotateTransform(90, center.X, center.Y).Transform(origin);
                editor.BeginStroke(origin); editor.MoveStroke(new RotateTransform(47, center.X, center.Y).Transform(origin)); editor.MoveStroke(end); editor.EndStroke(end, true);
                var rotated = editor.Shapes.Single(); var expected = original.Apply(new RotateTransform(90, center.X, center.Y));
                Assert(Near(rotated.Geometry.Bounds, expected.Geometry.Bounds), "Rotation must not accumulate intermediate angles");
                editor.Undo(); Assert(before.SequenceEqual(Pixels(editor.Flatten())), "Whole rotation is one history step");
                editor.Redo(); Assert(editor.Shapes.Single() == rotated, "Rotation redo");
            } finally { Clean(editor); }
        });
        test("text direct drag resize rotate and double-click preserve native editable content", () => {
            var editor = new EditorWindow(app, photo);
            try {
                editor.SelectTool("文字"); editor.BeginStroke(new Point(160, 160));
                Inputs(editor).Single(t => !t.IsReadOnly).Text = "中文 IME\nsecond line"; editor.CommitText();
                editor.SelectTool("画笔"); editor.BeginStroke(new Point(170, 170)); editor.EndStroke(new Point(190, 180));
                Assert(editor.SelectedText != null && editor.Shapes.Count == 0 && Inputs(editor).All(t => t.IsReadOnly), "Single click should transform text, not edit or paint");
                var text = editor.Texts.Single(); var origin = Center(Handle(editor, "BottomRight"));
                editor.BeginStroke(origin); editor.EndStroke(origin + new Vector(90, 70));
                Assert(editor.Texts.Single().FontSize > text.FontSize && editor.Texts.Single().Text == text.Text, "Text scale must retain native text");
                var frame = Handle(editor, "Rotate"); origin = Center(frame);
                var bounds = TextBounds(editor.Texts.Single()); var center = new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
                editor.BeginStroke(origin); editor.EndStroke(new RotateTransform(45, center.X, center.Y).Transform(origin), true);
                Assert(Math.Abs(editor.Texts.Single().Angle - 45) < .001, "Text object rotation");
                var rotated = editor.Texts.Single(); var labelPoint = new RotateTransform(rotated.Angle).Transform(new Point(30, 15)) + new Vector(rotated.X, rotated.Y);
                editor.BeginStroke(labelPoint, false, 2);
                var input = Inputs(editor).Single(t => !t.IsReadOnly); input.Text = "仍可编辑 with spaces"; editor.CommitText();
                Assert(editor.Texts.Single().Text == input.Text && editor.Texts.Single().Angle == rotated.Angle, "Double click must edit transformed text without resetting angle");
                Assert(input.AcceptsReturn && input.AcceptsTab == false, "Native multiline text behavior retained");
            } finally { Clean(editor); }
        });
        test("transform chrome is fixed DIP at zoom and reachable at image edges, export is clean", () => {
            var editor = new EditorWindow(app, photo);
            try {
                Draw(editor); Layout(editor); var expected = Pixels(editor.Flatten());
                foreach (var zoom in new[] { .2, 1.0, 3.75 }) {
                    editor.Viewport.SetZoom(zoom); Layout(editor);
                    foreach (var handle in Handles(editor)) {
                        Assert(Math.Abs(handle.Width * editor.Viewport.Zoom - (Name(handle).EndsWith("Rotate") ? 12 : 10)) < .001, "Handle must have constant DIP size");
                        var center = handle.TranslatePoint(new Point(handle.ActualWidth / 2, handle.ActualHeight / 2), editor.Viewport);
                        Assert(center.X >= 9 && center.X <= editor.Viewport.ActualWidth - 9 && center.Y >= 9 && center.Y <= editor.Viewport.ActualHeight - 9, "Handle outside reachable viewport bounds");
                    }
                    Assert(expected.SequenceEqual(Pixels(editor.Flatten())), "Chrome or viewport leaked into export");
                }
                editor.NudgeSelection(new Vector(-100, -100)); Layout(editor);
                Assert(Handles(editor).All(h => { var p = h.TranslatePoint(new Point(h.Width / 2, h.Height / 2), editor.Viewport); return p.X >= 9 && p.Y >= 9; }), "Edge annotation handles clipped");
            } finally { Clean(editor); }
        });
        test("cancel and lost capture roll back entire transform without consuming redo", () => {
            var editor = new EditorWindow(app, photo);
            try {
                Draw(editor); var original = editor.Shapes.Single(); var before = Pixels(editor.Flatten());
                var origin = Center(Handle(editor, "BottomRight")); editor.BeginStroke(origin); editor.MoveStroke(origin + new Vector(100, 60)); editor.Undo();
                Assert(editor.Shapes.Single() == original && before.SequenceEqual(Pixels(editor.Flatten())), "Undo during gesture cancels preview");
                editor.SelectTool("画笔"); editor.BeginStroke(new Point(100, 140)); editor.EndStroke(new Point(100, 140));
                origin = Center(Handle(editor, "Rotate")); editor.BeginStroke(origin); editor.MoveStroke(origin + new Vector(90, 60));
                var canvas = TextChecks.Descendants((DependencyObject)editor.Content).OfType<Canvas>().Single(c => Name(c) == "图片标注画布");
                canvas.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.LostMouseCaptureEvent });
                Assert(editor.Shapes.Single() == original && before.SequenceEqual(Pixels(editor.Flatten())), "Lost capture cancels geometry and pixel preview");
                editor.Undo(); editor.Redo(); Assert(editor.Shapes.Single() == original, "Cancelled transforms must not add history");
            } finally { Clean(editor); }
        });
        test("erase and crop clips follow resize then rotation without resurrecting discarded ink", () => {
            var editor = new EditorWindow(app, photo);
            try {
                editor.SelectTool("直线"); editor.BeginStroke(new Point(20, 160)); editor.EndStroke(new Point(400, 160));
                editor.SelectTool("橡皮擦"); editor.BeginStroke(new Point(210, 160)); editor.EndStroke(new Point(210, 160));
                editor.SelectTool("裁剪"); editor.BeginStroke(new Point(80, 80)); editor.EndStroke(new Point(500, 360));
                editor.SelectTool("画笔"); editor.BeginStroke(new Point(50, 80)); editor.EndStroke(new Point(50, 80));
                var original = editor.Shapes.Single(); var before = Pixels(editor.Flatten());
                var origin = Center(Handle(editor, "BottomRight")); editor.BeginStroke(origin); editor.EndStroke(origin + new Vector(40, 20), true);
                var scaled = editor.Shapes.Single(); var bounds = scaled.Bounds; var center = new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
                origin = Center(Handle(editor, "Rotate")); editor.BeginStroke(origin); editor.EndStroke(new RotateTransform(90, center.X, center.Y).Transform(origin));
                var transformed = editor.Shapes.Single(); var matrix = original.Geometry.Transform.Value; matrix.Invert(); matrix.Append(transformed.Geometry.Transform.Value);
                Assert(transformed.Clip != null && !transformed.Hit(matrix.Transform(new Point(130, 80)), 0), "Erased gap must survive scale+rotate");
                Assert(!transformed.Hit(matrix.Transform(new Point(-30, 80)), 0), "Crop-discarded ink must stay clipped");
                Assert(transformed.Hit(matrix.Transform(new Point(50, 80)), .1), "Retained ink must remain visible");
                editor.Undo(); editor.Undo(); Assert(before.SequenceEqual(Pixels(editor.Flatten())), "Two transforms must be two exact history steps");
            } finally { Clean(editor); }
        });
        test("nearby actions and object menu use editor-local copies, native input retains text keys", () => {
            var editor = new EditorWindow(app, photo);
            try {
                Draw(editor); var original = editor.Shapes.Single(); var originalPixels = Pixels(editor.Flatten());
                var copy = FloatingActions(editor).Single(b => (string)b.Content == "复制");
                copy.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert(editor.Shapes.Count == 2, "One copy click must immediately create exactly one editable duplicate");
                Assert(editor.Shapes[1].Id != original.Id && editor.SelectedShape == editor.Shapes[1].Id && editor.Shapes[1].Bounds.TopLeft == original.Bounds.TopLeft + new Vector(16, 16), "Copy click must select the offset duplicate");
                var duplicatedPixels = Pixels(editor.Flatten());
                Assert(!originalPixels.SequenceEqual(duplicatedPixels), "Immediate duplicate must appear in export");
                editor.Undo(); Assert(editor.Shapes.Single() == original && originalPixels.SequenceEqual(Pixels(editor.Flatten())), "Copy click must be a single reversible edit");
                editor.Redo(); Assert(editor.Shapes.Count == 2 && duplicatedPixels.SequenceEqual(Pixels(editor.Flatten())), "Redo must exactly restore copy pixels");
                editor.BeginStroke(new Point(116, 156)); editor.EndStroke(new Point(116, 156));
                editor.CopySelection();
                Assert(TextChecks.Descendants((DependencyObject)editor.Content).OfType<TextBlock>().Any(t => t.Text.Contains("本编辑窗口粘贴")), "Copy status must explain local clipboard");
                var canvas = TextChecks.Descendants((DependencyObject)editor.Content).OfType<Canvas>().Single(c => Name(c) == "图片标注画布");
                var menu = canvas.ContextMenu!; menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
                var paste = menu.Items.OfType<MenuItem>().Single(i => (string)i.Header == "粘贴标注");
                Assert(paste.IsEnabled, "Object context paste must be enabled after local copy"); paste.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Assert(editor.Shapes.Count == 3, "Internal clipboard paste must still create another editable object");
                TextChecks.Descendants((DependencyObject)editor.Content).OfType<Button>().Single(b => Name(b) == "删除标注 · Delete").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert(editor.Shapes.Count == 2, "Nearby delete button removes only the selected copy");
                editor.BeginStroke(new Point(116, 156)); editor.EndStroke(new Point(116, 156));
                Assert(FloatingActions(editor).Select(b => (string)b.Content).SequenceEqual(new[] { "复制", "删除", "完成" }), "Floating actions must have one unambiguous copy action");
                var selectedPixels = Pixels(editor.Flatten());
                FloatingActions(editor).Single(b => (string)b.Content == "完成").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert(editor.SelectedShape == null && editor.SelectedText == null && selectedPixels.SequenceEqual(Pixels(editor.Flatten())), "Done must only deselect, without changing exported pixels");
                editor.SelectTool("文字"); editor.BeginStroke(new Point(350, 250)); var input = Inputs(editor).Single(t => !t.IsReadOnly); input.Text = "typing with spaces";
                using var source = new HwndSource(new HwndSourceParameters("Direct text keys") { Width = 1, Height = 1, WindowStyle = 0 });
                foreach (var key in new[] { Key.Space, Key.C, Key.V, Key.Delete, Key.Left }) {
                    var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent, Source = input };
                    typeof(EditorWindow).GetMethod("OnKey", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(editor, new object[] { editor, args });
                    Assert(!args.Handled && !editor.Viewport.SpaceHeld && editor.Texts.Single().Text == "typing with spaces", "Editor must defer native input keys");
                }
            } finally { Clean(editor); }
        });
        test("direct transform layouts render at compact and standard viewport sizes", () => {
            foreach (var size in new[] { new Size(1120, 740), new Size(760, 460) }) {
                var editor = new EditorWindow(app, photo);
                try {
                    Draw(editor);
                    editor.SelectTool("文字"); editor.BeginStroke(new Point(290, 250)); Inputs(editor).Single(t => !t.IsReadOnly).Text = "直接拖动标注\n双击修改文字"; editor.CommitText();
                    editor.SelectTool("画笔"); editor.BeginStroke(new Point(300, 260)); editor.EndStroke(new Point(300, 260));
                    var origin = Center(Handle(editor, "Rotate")); var bounds = TextBounds(editor.Texts.Single()); var center = new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
                    editor.BeginStroke(origin); editor.EndStroke(new RotateTransform(-15, center.X, center.Y).Transform(origin));
                    var root = (FrameworkElement)editor.Content; editor.Content = null;
                    var wrapper = new Border { Background = editor.Background, Resources = editor.Resources, Child = root };
                    wrapper.Measure(size); wrapper.Arrange(new Rect(size)); wrapper.UpdateLayout();
                    var image = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32); image.Render(wrapper);
                    MediaTools.SavePng(image, Path.Combine(Settings.Root, "ui", $"direct-transform-{size.Width}.png"));
                    Assert(TextChecks.Descendants(root).OfType<System.Windows.Shapes.Shape>().Count(h => Name(h).StartsWith("对象手柄 ")) == 5, "Layout must retain all transform handles");
                    wrapper.Child = null; editor.Content = root;
                } finally { Clean(editor); }
            }
        });
        test("one and two-point-five percent zoom keep full-size reachable transform controls", () => {
            var editor = new EditorWindow(app, photo);
            try {
                ShowOffscreen(editor); Draw(editor); Layout(editor);
                Size? actionSize = null;
                foreach (var zoom in new[] { .01, .025 }) {
                    editor.Viewport.SetZoom(zoom); Layout(editor);
                    Assert(Math.Abs(editor.Viewport.Zoom - zoom) < .00001, "Requested low zoom must be used");
                    foreach (var handle in Handles(editor)) {
                        Assert(Math.Abs(handle.Width * zoom - (Name(handle).EndsWith("Rotate") ? 12 : 10)) < .001, "Handle shrinks below five percent zoom");
                        var point = handle.TranslatePoint(new Point(handle.ActualWidth / 2, handle.ActualHeight / 2), editor.Viewport);
                        Assert(point.X >= 9 && point.Y >= 9 && point.X <= editor.Viewport.ActualWidth - 9 && point.Y <= editor.Viewport.ActualHeight - 9, "Low-zoom handle is clipped by viewport");
                        var hit = editor.Viewport.InputHitTest(point) as DependencyObject;
                        Assert(hit != null && !ReferenceEquals(hit, editor.Viewport), "Low-zoom handle outside image must be hit-testable");
                    }
                    foreach (var action in FloatingActions(editor)) {
                        var area = action.TransformToAncestor(editor.Viewport).TransformBounds(new Rect(action.RenderSize));
                        actionSize ??= area.Size;
                        Assert(Math.Abs(area.Width - actionSize.Value.Width) < .01 && Math.Abs(area.Height - actionSize.Value.Height) < .01, "Floating action targets must keep the same DIP size at low zoom");
                        Assert(area.Left >= 0 && area.Top >= 0 && area.Right <= editor.Viewport.ActualWidth && area.Bottom <= editor.Viewport.ActualHeight, "Low-zoom floating action must stay reachable");
                        Assert(IsWithin(editor.Viewport.InputHitTest(new Point(area.Left + area.Width / 2, area.Top + area.Height / 2)) as DependencyObject, action), "Low-zoom action center must be clickable");
                    }
                    var corner = Center(Handle(editor, "BottomRight")); var before = editor.Shapes.Single();
                    editor.BeginStroke(corner + new Vector(7 / zoom, 0)); editor.EndStroke(corner + new Vector(8 / zoom, 0));
                    Assert(editor.Shapes.Count == 1 && editor.Shapes.Single().Bounds.Width > before.Bounds.Width, "Low-zoom handle hit radius must stay nine DIP");
                    editor.Undo(); DrawSelection(editor);
                    Layout(editor);
                    var visual = Handle(editor, "BottomRight");
                    var viewPoint = visual.TranslatePoint(new Point(visual.Width / 2, visual.Height / 2), editor.Viewport);
                    var target = editor.Viewport.InputHitTest(viewPoint) as UIElement ?? throw new Exception("Overlay input target missing");
                    var canvas = TextChecks.Descendants((DependencyObject)editor.Content).OfType<Canvas>().Single(c => Name(c) == "图片标注画布");
                    var down = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent };
                    var start = down.GetPosition(canvas); before = editor.Shapes.Single();
                    target.RaiseEvent(down); editor.EndStroke(start + new Vector(1 / zoom, 0));
                    if (Mouse.Captured == canvas) canvas.ReleaseMouseCapture();
                    Assert(down.Handled && editor.Shapes.Single().Bounds.Width > before.Bounds.Width, "Actual overlay mouse route must start resize outside the image");
                    editor.Undo(); DrawSelection(editor);
                }
            } finally { Clean(editor); }
        });
        test("fit of a long narrow image keeps overlay handles and entire action bar usable", () => {
            var path = Path.Combine(Settings.Root, "long-narrow.png");
            var bitmap = BitmapSource.Create(40, 16000, 96, 96, PixelFormats.Bgra32, null, Enumerable.Repeat((byte)255, 40 * 16000 * 4).ToArray(), 40 * 4); MediaTools.SavePng(bitmap, path);
            var editor = new EditorWindow(app, new Photo { Path = path });
            try {
                ShowOffscreen(editor); editor.SelectTool("矩形"); editor.BeginStroke(new Point(5, 5000)); editor.EndStroke(new Point(35, 7000)); Layout(editor); editor.Viewport.Fit(); Layout(editor);
                var before = Pixels(editor.Flatten());
                var centers = Handles(editor).Select(h => h.TranslatePoint(new Point(h.ActualWidth / 2, h.ActualHeight / 2), editor.Viewport)).ToArray();
                Assert(centers.Length == 5 && centers.SelectMany((a, i) => centers.Skip(i + 1).Select(b => (a - b).Length)).Min() >= 18, "Narrow-image handles must not collapse or overlap");
                var actions = FloatingActions(editor);
                Assert(actions.Length == 3, "Exactly three nearby actions must exist");
                Rect previousAction = Rect.Empty;
                foreach (var button in actions) {
                    var area = button.TransformToAncestor(editor.Viewport).TransformBounds(new Rect(button.RenderSize));
                    Assert(area.Width >= 37.9 && area.Height >= 27.9 && area.Left >= 0 && area.Right <= editor.Viewport.ActualWidth && area.Top >= 0 && area.Bottom <= editor.Viewport.ActualHeight, "Action button clipped on narrow image");
                    Assert(IsWithin(editor.Viewport.InputHitTest(new Point(area.Left + 2, area.Top + 2)) as DependencyObject, button), "Action hit region missing");
                    if (!previousAction.IsEmpty) {
                        var gapPoint = new Point((previousAction.Right + area.Left) / 2, area.Top + area.Height / 2);
                        var gapHit = editor.Viewport.InputHitTest(gapPoint) as DependencyObject;
                        Assert(area.Left > previousAction.Right && actions.All(action => !IsWithin(gapHit, action)), "Adjacent actions need a real gap that cannot accidentally activate either button");
                    }
                    previousAction = area;
                }
                Assert(before.SequenceEqual(Pixels(editor.Flatten())), "Overlay must remain absent from export");
                Capture(editor, "narrow-fit");
            } finally { Clean(editor); }
        });
        test("shown editor double-click restores keyboard focus to rebuilt native text input", () => {
            var editor = new EditorWindow(app, photo) { ShowInTaskbar = false, Left = -10000, Top = -10000 };
            try {
                editor.Show(); Pump(); editor.SelectTool("文字"); editor.BeginStroke(new Point(150, 160)); Pump();
                var input = Inputs(editor).Single(t => !t.IsReadOnly); input.Text = "existing 中文"; editor.CommitText();
                editor.SelectTool("画笔"); editor.Viewport.Focus(); editor.BeginStroke(new Point(180, 175), false, 2); Pump();
                input = Inputs(editor).Single(t => !t.IsReadOnly);
                Assert(input.IsKeyboardFocused && ReferenceEquals(Keyboard.FocusedElement, input), "Rebuilt double-click input does not receive keyboard focus");
                input.SelectedText = "追加"; editor.CommitText(); Assert(editor.Texts.Single().Text.EndsWith("追加"), "Caret must accept insertion after direct double click");
            } finally { Clean(editor); }
        });
        foreach (var mode in new[] { "裁剪", "橡皮擦" }) foreach (var selection in new[] { "paste", "context-menu selection" })
        test($"{mode} retains pointer priority after {selection}, including stale overlay route", () => {
            var editor = new EditorWindow(app, photo);
            try {
                ShowOffscreen(editor); Draw(editor); Layout(editor);
                var handle = Handle(editor, "BottomRight");
                var handlePoint = handle.TranslatePoint(new Point(handle.Width / 2, handle.Height / 2), editor.Viewport);
                var staleTarget = editor.Viewport.InputHitTest(handlePoint) as UIElement ?? throw new Exception("Initial overlay target missing");
                editor.CopySelection(); editor.SelectTool(mode);
                if (selection == "paste") editor.PasteSelection();
                else {
                    // This is the hit-specific branch called by the actual
                    // PreviewMouseRightButtonDown handler, without moving the
                    // user's system pointer to this offscreen fixture window.
                    typeof(EditorWindow).GetMethod("SelectAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(editor, new object[] { new Point(100, 140) });
                }
                Layout(editor);
                Assert(editor.SelectedShape != null, "Selection prerequisite was not established");
                var selected = editor.Shapes.Single(s => s.Id == editor.SelectedShape);
                var before = editor.Shapes.ToArray();
                var canvas = TextChecks.Descendants((DependencyObject)editor.Content).OfType<Canvas>().Single(c => Name(c) == "图片标注画布");
                var corner = selected.Bounds.BottomRight + new Vector(4 / editor.Viewport.Zoom, 4 / editor.Viewport.Zoom);
                var point = canvas.TranslatePoint(corner, editor.Viewport);
                var target = editor.Viewport.InputHitTest(point) as UIElement ?? throw new Exception("Corner input route missing");
                var visibleHandles = Handles(editor).Count();
                // MouseDown is the bubbling event that WPF promotes to the
                // direct MouseLeftButtonDown event at each ancestor (including
                // the canvas when InputHitTest returns its Image child).
                var down = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.MouseDownEvent };
                target.RaiseEvent(down);
                object? Field(string name) => typeof(EditorWindow).GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(editor);
                Assert(Field("selectionStart") == null && Field("transformHandle") == null, "Overlay stole the active crop/eraser gesture");
                Assert(mode == "裁剪" ? Field("start") != null : Field("eraser") != null, $"The active tool did not receive the routed mouse event: target={target.GetType().Name}, handled={down.Handled}, tool={Field("tool")}, captured={Mouse.Captured?.GetType().Name}");
                Assert(before.Select(s => s.Geometry).SequenceEqual(editor.Shapes.Select(s => s.Geometry)), "Tool initiation unexpectedly transformed annotation geometry");
                Assert(visibleHandles == 0, "Crop and eraser must not advertise transform handles");
                editor.Undo();
                Assert(before.SequenceEqual(editor.Shapes), "Cancelling active tool must restore all object data");
                // A detached handle may still have a queued event after a tool
                // change. It must not start a transform or consume that event.
                var staleDown = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent };
                staleTarget.RaiseEvent(staleDown);
                Assert(!staleDown.Handled && Field("selectionStart") == null && Field("transformHandle") == null, "Stale overlay callback bypassed active tool priority");
                Assert((string?)Field("tool") == mode, "Pointer routing silently changed tools");
            } finally { Clean(editor); }
        });
    }
    static void Layout(EditorWindow editor) { var root = (FrameworkElement)editor.Content; root.Measure(new Size(900, 640)); root.Arrange(new Rect(0, 0, 900, 640)); root.UpdateLayout(); }
    static void ShowOffscreen(EditorWindow editor) { editor.ShowInTaskbar = false; editor.Left = editor.Top = -10000; editor.Show(); Pump(); }
    static void DrawSelection(EditorWindow editor) { editor.SelectTool("画笔"); editor.BeginStroke(new Point(100, 140)); editor.EndStroke(new Point(100, 140)); }
    static void Pump() { var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false)); Dispatcher.PushFrame(frame); }
    static bool IsWithin(DependencyObject? hit, DependencyObject ancestor) { for (; hit != null; hit = VisualTreeHelper.GetParent(hit)) if (ReferenceEquals(hit, ancestor)) return true; return false; }
    static void Capture(EditorWindow editor, string name) {
        var root = (FrameworkElement)editor.Content; var image = new RenderTargetBitmap(900, 640, 96, 96, PixelFormats.Pbgra32); image.Render(root);
        MediaTools.SavePng(image, Path.Combine(Settings.Root, "ui", name + ".png"));
    }
    static void Draw(EditorWindow editor) { editor.SelectTool("矩形"); editor.BeginStroke(new Point(100, 100)); editor.EndStroke(new Point(250, 180)); }
    static Button[] FloatingActions(EditorWindow editor) => TextChecks.Descendants((DependencyObject)editor.Content).OfType<Button>().Where(b => b.Content is string value && value is "复制" or "重复" or "删除" or "完成").ToArray();
    static string Name(DependencyObject element) => System.Windows.Automation.AutomationProperties.GetName(element);
    static IEnumerable<System.Windows.Shapes.Shape> Handles(EditorWindow editor) => TextChecks.Descendants((DependencyObject)editor.Content).OfType<System.Windows.Shapes.Shape>().Where(h => Name(h).StartsWith("对象手柄 "));
    static System.Windows.Shapes.Shape Handle(EditorWindow editor, string name) => Handles(editor).Single(h => Name(h) == "对象手柄 " + name);
    static Point Center(FrameworkElement element) => new(Canvas.GetLeft(element) + element.Width / 2, Canvas.GetTop(element) + element.Height / 2);
    static IEnumerable<TextBox> Inputs(EditorWindow editor) => TextChecks.Descendants((DependencyObject)editor.Content).OfType<TextBox>().Where(t => t.AcceptsReturn);
    static byte[] Pixels(BitmapSource image) { var pixels = new byte[image.PixelWidth * image.PixelHeight * 4]; new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0).CopyPixels(pixels, image.PixelWidth * 4, 0); return pixels; }
    static bool Near(Rect a, Rect b) => (a.TopLeft - b.TopLeft).Length < .001 && Math.Abs(a.Width - b.Width) < .001 && Math.Abs(a.Height - b.Height) < .001;
    static Rect TextBounds(TextAnnotation text) { var label = text.CreateLabel(); label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); var bounds = new RotateTransform(text.Angle).TransformBounds(new Rect(label.DesiredSize)); bounds.Offset(text.X, text.Y); return bounds; }
    static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static void Clean(EditorWindow editor) { for (int i = 0; i < 40; i++) editor.Undo(); editor.Close(); }
}
