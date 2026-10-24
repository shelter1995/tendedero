namespace Liangtu;

internal static class EditorChecks
{
    public static void Run(Action<string, Action> test)
    {
        void Assert(bool ok) { if (!ok) throw new InvalidOperationException("Editor regression"); }
        BitmapSource Bitmap(int w, int h, byte red) {
            var pixels = new byte[w * h * 4]; for (int i = 0; i < pixels.Length; i += 4) { pixels[i + 2] = red; pixels[i + 3] = 255; }
            var b = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, w * 4); b.Freeze(); return b;
        }
        test("editor redo restores cropped image and matching eraser baseline", () => {
            var original = Bitmap(4, 3, 20); var cropped = Bitmap(2, 2, 80); var baseline = Bitmap(2, 2, 20);
            var history = new EditHistory(original); history.Commit(cropped, baseline);
            Assert(history.Undo() && !history.IsDirty && history.CanRedo);
            Assert(history.Redo() && history.Current.Image == cropped && history.Current.Baseline == baseline && history.IsDirty);
        });
        test("editor new edit after undo discards redo branch", () => {
            var b = Bitmap(2, 2, 20); var history = new EditHistory(b);
            history.Commit(Bitmap(2, 2, 30), b); history.Undo(); history.Commit(Bitmap(2, 2, 40), b);
            Assert(!history.CanRedo && !history.Redo());
        });
        test("editor history tracks saved revision through undo and redo", () => {
            var b = Bitmap(2, 2, 20); var history = new EditHistory(b);
            history.Commit(Bitmap(2, 2, 30), b); history.MarkSaved(); history.Undo(); Assert(history.IsDirty);
            Assert(history.Redo() && !history.IsDirty);
        });
        test("editor zoom preserves image pixel under mouse including zoom limits", () => {
            var state = new ImageViewState { ImageSize = new Size(1200, 800), ViewSize = new Size(900, 600) }; state.Fit();
            var mouse = new Point(620, 170); var pixel = state.ToImage(mouse);
            foreach (var scale in new[] { 2.0, .0001, 100.0, .75 }) { state.ZoomAt(scale, mouse); Assert((state.ToImage(mouse) - pixel).Length < .0001); }
            Assert(!state.Fitted);
        });
        test("editor pan and inverse coordinates remain exact at noninteger zoom", () => {
            var state = new ImageViewState { ImageSize = new Size(800, 600), ViewSize = new Size(1000, 700) }; state.Fit(); state.ZoomAt(1.25, new Point(500, 350));
            var before = state.ToView(new Point(120, 90)); state.Pan(new Vector(-37, 82));
            Assert((state.ToView(new Point(120, 90)) - before - new Vector(-37, 82)).Length < .0001);
            Assert((state.ToImage(state.ToView(new Point(20, 40))) - new Point(20, 40)).Length < .0001);
        });
        test("custom annotation colors survive settings reload", () => {
            var settings = new Settings { AnnotationColor = "#123ABC", AnnotationCustomColors = new[] { 0x123ABC, 0xABC123 } }; settings.Save();
            var restored = Settings.Load(); var color = AnnotationColors.Parse(restored.AnnotationColor); var slots = AnnotationColors.Slots(restored.AnnotationCustomColors);
            Assert(color.R == 0x12 && color.G == 0x3A && color.B == 0xBC && slots.Length == 16 && slots[0] == 0x123ABC && slots[15] == 0xFFFFFF);
            Assert(AnnotationColors.Slots(null).Length == 16 && AnnotationColors.Parse("bad").R == 226);
        });
        test("Shift shape constraint produces equal sides in all four drag directions", () => {
            foreach (int dx in new[] { -1, 1 }) foreach (int dy in new[] { -1, 1 }) {
                var anchor = new Point(300, 300); var pointer = anchor + new Vector(dx * 120, dy * 50);
                var rect = ShapeBounds.Calculate(anchor, pointer, new Size(800, 700), true);
                Assert(rect.Width == 120 && rect.Height == 120 && rect.X == (dx < 0 ? 180 : 300) && rect.Y == (dy < 0 ? 180 : 300));
                var free = ShapeBounds.Calculate(anchor, pointer, new Size(800, 700), false); Assert(free.Width == 120 && free.Height == 50);
            }
        });
        test("Shift shape stays square at canvas edges and never reverses outward drag", () => {
            var rect = ShapeBounds.Calculate(new Point(900, 500), new Point(1100, 540), new Size(960, 600), true);
            Assert(rect == new Rect(900, 500, 60, 60));
            rect = ShapeBounds.Calculate(new Point(60, 60), new Point(-100, 40), new Size(960, 600), true);
            Assert(rect == new Rect(0, 0, 60, 60));
            rect = ShapeBounds.Calculate(new Point(0, 100), new Point(-100, 200), new Size(960, 600), true);
            Assert(rect.Width == 0 && rect.Height == 0);
        });
        test("Shift line snaps to all eight horizontal vertical and diagonal directions", () => {
            var anchor = new Point(300, 300); var size = new Size(800, 700);
            foreach (var pair in new[] {
                (new Vector(150, 20), new Vector(150, 0)), (new Vector(100, 70), new Vector(85, 85)),
                (new Vector(20, 150), new Vector(0, 150)), (new Vector(-70, 100), new Vector(-85, 85)),
                (new Vector(-150, 20), new Vector(-150, 0)), (new Vector(-100, -70), new Vector(-85, -85)),
                (new Vector(20, -150), new Vector(0, -150)), (new Vector(100, -70), new Vector(85, -85)) }) {
                Assert((ShapeBounds.LineEnd(anchor, anchor + pair.Item1, size, true) - anchor - pair.Item2).Length < .001);
                Assert(ShapeBounds.LineEnd(anchor, anchor + pair.Item1, size, false) == anchor + pair.Item1);
            }
        });
        test("Shift line clipping preserves angle and zero-length outward drag", () => {
            var size = new Size(960, 600); var anchor = new Point(900, 500);
            Assert(ShapeBounds.LineEnd(anchor, new Point(1100, 630), size, true) == new Point(960, 560));
            Assert(ShapeBounds.LineEnd(anchor, new Point(1100, 520), size, true) == new Point(960, 500));
            Assert(ShapeBounds.LineEnd(anchor, new Point(920, 800), size, true) == new Point(900, 600));
            Assert(ShapeBounds.LineEnd(new Point(0, 100), new Point(-120, 60), size, true) == new Point(0, 100));
        });
    }
    public static void RunUi(App app, Photo photo, Action<string, Action> test)
    {
        byte[] Pixels(BitmapSource bitmap) { var bytes = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4]; new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0).CopyPixels(bytes, bitmap.PixelWidth * 4, 0); return bytes; }
        void Assert(bool value) { if (!value) throw new InvalidOperationException("Editor UI regression"); }
        EditorWindow Create() {
            var editor = new EditorWindow(app, photo); var content = (FrameworkElement)editor.Content;
            content.Measure(new Size(900, 640)); content.Arrange(new Rect(0, 0, 900, 640)); content.UpdateLayout(); return editor;
        }
        Canvas? FindCanvas(DependencyObject node) {
            if (node is Canvas canvas && System.Windows.Automation.AutomationProperties.GetName(canvas) == "图片标注画布") return canvas;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) { var result = FindCanvas(VisualTreeHelper.GetChild(node, i)); if (result != null) return result; }
            return null;
        }
        foreach (var tool in new[] { "矩形", "圆形" })
        test("Shift live preview, final commit and undo/redo: " + tool, () => {
            var editor = Create();
            try {
                var original = Pixels(editor.Flatten()); editor.Viewport.SetZoom(2.25); editor.Viewport.Pan(-140, 90);
                editor.SelectTool(tool); editor.BeginStroke(new Point(100, 100)); editor.MoveStroke(new Point(250, 160));
                var canvas = FindCanvas((DependencyObject)editor.Content) ?? throw new Exception("Canvas missing");
                var shape = canvas.Children.OfType<System.Windows.Shapes.Shape>().Single();
                Assert(shape.Width == 150 && shape.Height == 60);
                Assert(editor.UpdateShapeConstraint(true) && shape.Width == 150 && shape.Height == 150);
                Assert(editor.UpdateShapeConstraint(false) && shape.Width == 150 && shape.Height == 60);
                editor.UpdateShapeConstraint(true); var preview = Pixels(editor.Flatten());
                editor.EndStroke(new Point(250, 160), true); Assert(preview.SequenceEqual(Pixels(editor.Flatten())));
                editor.Undo(); Assert(original.SequenceEqual(Pixels(editor.Flatten())));
                editor.BeginStroke(new Point(0, 100)); editor.EndStroke(new Point(-100, 180), true);
                editor.Redo(); Assert(preview.SequenceEqual(Pixels(editor.Flatten())));
                editor.SelectTool("裁剪"); editor.BeginStroke(new Point(20, 20)); editor.MoveStroke(new Point(100, 60), true);
                Assert(!editor.UpdateShapeConstraint(true)); editor.EndStroke(new Point(100, 60), true);
                Assert(editor.Flatten().PixelWidth == 80 && editor.Flatten().PixelHeight == 40);
            } finally { for (int i = 0; i < 35; i++) editor.Undo(); editor.Close(); }
        });
        foreach (var tool in new[] { "直线", "箭头" })
        test("Shift line live toggle, arrowhead alignment and history: " + tool, () => {
            var editor = Create();
            try {
                var original = Pixels(editor.Flatten()); editor.Viewport.SetZoom(2.5); editor.Viewport.Pan(-120, 70);
                editor.SelectTool(tool); editor.BeginStroke(new Point(100, 100)); editor.MoveStroke(new Point(240, 180));
                var path = FindCanvas((DependencyObject)editor.Content)!.Children.OfType<System.Windows.Shapes.Path>().Single();
                Point EndPoint() {
                    var flattened = PathGeometry.CreateFromGeometry(path.Data);
                    if (flattened.Figures.Count == 0) throw new Exception($"Empty flattened geometry: type={path.Data.GetType().Name}, data={path.Data}, bounds={path.Data.Bounds}");
                    var segment = flattened.Figures[0].Segments.Last();
                    return segment switch { LineSegment line => line.Point, PolyLineSegment line => line.Points[^1], _ => throw new Exception("Unexpected line geometry") };
                }
                Assert(EndPoint() == new Point(240, 180));
                Assert(editor.UpdateShapeConstraint(true)); Assert((EndPoint() - new Point(210, 210)).Length < .001);
                if (tool == "箭头") {
                    var head = PathGeometry.CreateFromGeometry(path.Data).Figures[1];
                    var points = head.Segments.SelectMany(segment => segment is PolyLineSegment poly ? poly.Points.AsEnumerable() : new[] { ((LineSegment)segment).Point }).ToArray();
                    Assert((points[0] - EndPoint()).Length < .001);
                    var a = head.StartPoint - EndPoint(); var b = points[^1] - EndPoint();
                    Assert(Math.Abs(a.Length - b.Length) < .001 && Math.Abs((a + b).X - (a + b).Y) < .001);
                }
                editor.UpdateShapeConstraint(false); Assert(EndPoint() == new Point(240, 180));
                editor.UpdateShapeConstraint(true); var preview = Pixels(editor.Flatten());
                editor.EndStroke(new Point(240, 180), true); Assert(preview.SequenceEqual(Pixels(editor.Flatten())));
                editor.Undo(); Assert(original.SequenceEqual(Pixels(editor.Flatten())));
                editor.BeginStroke(new Point(0, 100)); editor.EndStroke(new Point(-120, 60), true);
                editor.Redo(); Assert(preview.SequenceEqual(Pixels(editor.Flatten())));
            } finally { for (int i = 0; i < 35; i++) editor.Undo(); editor.Close(); }
        });
        test("editor zoom/pan never changes saved pixel dimensions or annotation coordinates", () => {
            var editor = Create();
            try {
                var original = editor.Flatten();
                editor.SelectTool("矩形"); editor.BeginStroke(new Point(100, 100)); editor.EndStroke(new Point(300, 200));
                var marked = editor.Flatten(); var expected = Pixels(marked); Assert(!expected.SequenceEqual(Pixels(original)));
                editor.Viewport.SetZoom(3.25); editor.Viewport.Pan(-230, 120);
                var afterView = editor.Flatten(); Assert(afterView.PixelWidth == original.PixelWidth && afterView.PixelHeight == original.PixelHeight && expected.SequenceEqual(Pixels(afterView)));
                var output = Path.Combine(Settings.Root, "zoomed-editor-export.png"); MediaTools.SavePng(afterView, output);
                Assert(expected.SequenceEqual(Pixels(MediaTools.LoadImage(output))));
                editor.Undo(); Assert(Pixels(editor.Flatten()).SequenceEqual(Pixels(original)));
                editor.Redo(); Assert(Pixels(editor.Flatten()).SequenceEqual(expected));
            } finally { for (int i = 0; i < 35; i++) editor.Undo(); editor.Close(); }
        });
        test("editor cancelled crop preserves redo and crop/rotate restore correct image sizes", () => {
            var editor = Create();
            try {
                editor.SelectTool("矩形"); editor.BeginStroke(new Point(100, 100)); editor.EndStroke(new Point(300, 200)); var marked = Pixels(editor.Flatten());
                editor.Undo(); editor.SelectTool("裁剪"); editor.BeginStroke(new Point(20, 20)); editor.EndStroke(new Point(20, 20)); editor.Redo(); Assert(marked.SequenceEqual(Pixels(editor.Flatten())));
                editor.BeginStroke(new Point(50, 50)); editor.EndStroke(new Point(450, 250)); Assert(editor.Flatten().PixelWidth == 400 && editor.Flatten().PixelHeight == 200);
                editor.Rotate(); Assert(editor.Flatten().PixelWidth == 200 && editor.Flatten().PixelHeight == 400);
                var rotated = Pixels(editor.Flatten()); editor.Undo(); editor.Redo(); Assert(rotated.SequenceEqual(Pixels(editor.Flatten())));
                editor.SelectTool("橡皮擦"); editor.BeginStroke(new Point(100, 100)); editor.EndStroke(new Point(105, 100)); editor.Undo(); Assert(rotated.SequenceEqual(Pixels(editor.Flatten())));
            } finally { for (int i = 0; i < 35; i++) editor.Undo(); editor.Close(); }
        });
    }
}
