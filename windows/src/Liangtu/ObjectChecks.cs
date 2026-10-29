namespace Liangtu;

internal static class ObjectChecks
{
    [STAThread]
    public static int Main(string[] args)
    {
        Settings.Root = Path.GetFullPath(args[0]); Directory.CreateDirectory(Settings.Root);
        Directory.CreateDirectory(Path.Combine(Settings.Root, "ui"));
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        typeof(App).GetProperty(nameof(App.Settings))!.SetValue(app, new Settings());
        var pixels = Enumerable.Repeat((byte)255, 640 * 480 * 4).ToArray();
        var bitmap = BitmapSource.Create(640, 480, 96, 96, PixelFormats.Bgra32, null, pixels, 640 * 4);
        var path = Path.Combine(Settings.Root, "object-fixture.png"); MediaTools.SavePng(bitmap, path);
        var photo = new Photo { Path = path };
        var lines = new List<string>();
        void Test(string name, Action action) { try { action(); lines.Add("PASS " + name); } catch (Exception e) { lines.Add("FAIL " + name + ": " + e); } }
        Run(app, photo, Test);
        EditorChecks.RunUi(app, photo, Test);
        TextChecks.Run(app, photo, Test);
        testRepeatedWindows(app, photo, Test);
        File.WriteAllLines(Path.Combine(Settings.Root, "object-checks.txt"), lines);
        return lines.Any(l => l.StartsWith("FAIL")) ? 1 : 0;
    }

    public static void Run(App app, Photo photo, Action<string, Action> test)
    {
        test("object geometry capture does not depend on live layout", () => {
            var path = new System.Windows.Shapes.Path { Stroke = Brushes.Red, StrokeThickness = 4,
                Data = Geometry.Parse("M410,220 L590,220 M570,210 L590,220 L570,230"), Visibility = Visibility.Collapsed };
            path.Measure(new Size(900, 560)); path.Arrange(new Rect(0, 0, 0, 0));
            var shape = ShapeAnnotation.FromView(path);
            Assert(!shape.Geometry.IsEmpty() && shape.Hit(new Point(500, 220), 1), "Unarranged path geometry is lost");
        });
        foreach (var tool in new[] { "矩形", "圆形", "直线", "箭头", "画笔", "荧光笔" })
        test("select/delete restores original pixels for " + tool, () => {
            var editor = new EditorWindow(app, photo);
            try {
                var original = Pixels(editor.Flatten());
                editor.SelectTool(tool); editor.BeginStroke(new Point(100, 100)); editor.EndStroke(new Point(250, 160));
                Assert(!original.SequenceEqual(Pixels(editor.Flatten())), "Draw must add visible annotation");
                var hit = tool == "圆形" ? new Point(100, 130) : new Point(100, 100);
                editor.SelectTool("选择"); editor.BeginStroke(hit); editor.EndStroke(hit);
                editor.DeleteSelection();
                Assert(original.SequenceEqual(Pixels(editor.Flatten())), "Deleting shape must recover exact base image");
                editor.Undo(); Assert(!original.SequenceEqual(Pixels(editor.Flatten())), "Undo restores object");
                editor.Redo(); Assert(original.SequenceEqual(Pixels(editor.Flatten())), "Redo deletes object");
            } finally { for (int i = 0; i < 35; i++) editor.Undo(); editor.Close(); }
        });
        test("object drag and copy stay editable at zoom and survive history", () => {
            var editor = new EditorWindow(app, photo);
            try {
                editor.SelectTool("矩形"); editor.BeginStroke(new Point(100, 100)); editor.EndStroke(new Point(250, 160));
                var original = editor.Shapes.Single(); var pixels = Pixels(editor.Flatten());
                editor.Viewport.SetZoom(2.75); editor.Viewport.Pan(-110, 75); editor.SelectTool("选择");
                editor.BeginStroke(new Point(100, 130)); editor.EndStroke(new Point(130, 150));
                Assert(editor.Shapes.Single().Bounds.TopLeft == original.Bounds.TopLeft + new Vector(30, 20), "Drag must use image pixels");
                editor.Undo(); Assert(Pixels(editor.Flatten()).SequenceEqual(pixels), "Drag undo restores exact pixels"); editor.Redo();
                editor.BeginStroke(new Point(130, 150)); editor.EndStroke(new Point(130, 150));
                var selected = Pixels(editor.Flatten()); editor.CopySelection(); editor.PasteSelection();
                Assert(editor.Shapes.Count == 2 && editor.Shapes[0].Id != editor.Shapes[1].Id, "Paste must produce distinct editable object");
                editor.NudgeSelection(new Vector(1, -10)); Assert(editor.Shapes[1].Bounds.TopLeft == editor.Shapes[0].Bounds.TopLeft + new Vector(17, 6), "Nudge selected copy only");
                editor.DeleteSelection(); Assert(editor.Shapes.Count == 1 && selected.SequenceEqual(Pixels(editor.Flatten())), "Delete copy preserves source");
            } finally { Clean(editor); }
        });
        test("selection export is clean and selection click preserves redo", () => {
            var editor = new EditorWindow(app, photo);
            try {
                editor.SelectTool("直线"); editor.BeginStroke(new Point(80, 80)); editor.EndStroke(new Point(300, 80)); var expected = Pixels(editor.Flatten());
                editor.SelectTool("选择"); editor.BeginStroke(new Point(100, 80)); editor.EndStroke(new Point(100, 80));
                Assert(expected.SequenceEqual(Pixels(editor.Flatten())), "Selection chrome leaked into export");
                editor.DuplicateSelection(); editor.Undo();
                editor.BeginStroke(new Point(100, 80)); editor.EndStroke(new Point(100, 80)); editor.Redo();
                Assert(editor.Shapes.Count == 2, "Selecting must not create history or discard redo");
            } finally { Clean(editor); }
        });
        test("partial eraser retains geometry and holes move with annotation", () => {
            var editor = new EditorWindow(app, photo);
            try {
                editor.SelectTool("直线"); editor.BeginStroke(new Point(80, 100)); editor.EndStroke(new Point(350, 100)); var before = Pixels(editor.Flatten());
                editor.SelectTool("橡皮擦"); editor.BeginStroke(new Point(200, 100)); editor.EndStroke(new Point(210, 100));
                Assert(editor.Shapes.Count == 1 && editor.Shapes[0].Clip != null, "Eraser must not flatten editable objects");
                Assert(!editor.Shapes[0].Hit(new Point(200, 100), 0) && editor.Shapes[0].Hit(new Point(100, 100), 0), "Erased gap/hit test");
                editor.SelectTool("选择"); editor.BeginStroke(new Point(100, 100)); editor.EndStroke(new Point(120, 120));
                Assert(!editor.Shapes[0].Hit(new Point(220, 120), 0) && editor.Shapes[0].Hit(new Point(120, 120), 0), "Hole follows movement");
                editor.Undo(); editor.Undo(); Assert(before.SequenceEqual(Pixels(editor.Flatten())), "Erasure undo restores pixels");
            } finally { Clean(editor); }
        });
        test("crop and rotate retain selectable annotations and crop clips permanently", () => {
            var editor = new EditorWindow(app, photo);
            try {
                editor.SelectTool("直线"); editor.BeginStroke(new Point(20, 120)); editor.EndStroke(new Point(350, 120));
                editor.SelectTool("裁剪"); editor.BeginStroke(new Point(80, 60)); editor.EndStroke(new Point(400, 300));
                Assert(editor.Shapes.Count == 1 && editor.Shapes[0].Bounds.Left >= -.001, "Crop must discard invisible portion");
                editor.Rotate(); var expected = Pixels(editor.Flatten());
                Assert(editor.Shapes.Count == 1 && editor.Shapes[0].Hit(new Point(180, 100), 2), "Rotated shape remains selectable");
                editor.Undo(); editor.Redo(); Assert(expected.SequenceEqual(Pixels(editor.Flatten())), "Crop/rotate history exact");
                editor.SelectTool("选择"); editor.BeginStroke(new Point(180, 100)); editor.EndStroke(new Point(190, 110));
                Assert(editor.Shapes[0].Bounds.Top >= 9.99, "Moving cropped annotation must not resurrect removed geometry");
            } finally { Clean(editor); }
        });
        test("text selection duplicates and deletes with shape selection without flattening", () => {
            var editor = new EditorWindow(app, photo);
            try {
                editor.SelectTool("文字"); editor.BeginStroke(new Point(100, 120));
                TextChecks.Descendants((DependencyObject)editor.Content).OfType<TextBox>().Single(t => t.AcceptsReturn && !t.IsReadOnly).Text = "中文对象"; editor.CommitText();
                editor.SelectTool("选择"); editor.BeginStroke(new Point(105, 125)); editor.EndStroke(new Point(105, 125));
                Assert(editor.SelectedText != null, "Select tool must hit text"); editor.DuplicateSelection();
                Assert(editor.Texts.Count == 2 && editor.Texts[1].Text == "中文对象" && editor.Texts[0].Id != editor.Texts[1].Id, "Text copy retains editable content");
                editor.DeleteSelection(); Assert(editor.Texts.Count == 1, "Text selection deletion"); editor.Undo(); Assert(editor.Texts.Count == 2, "Text/object shared undo");
            } finally { Clean(editor); }
        });
    }
    static void Clean(EditorWindow editor) { for (int i = 0; i < 35; i++) editor.Undo(); editor.Close(); }
    static void testRepeatedWindows(App app, Photo photo, Action<string, Action> test)
    {
        foreach (var profile in ThemeProfiles.All) {
            var frame = new DispatcherFrame();
            app.Dispatcher.BeginInvoke(new Action(() => {
                try {
                    test("repeated unshown editor retains arrow before root layout: " + profile.Id, () => {
                        Theme.Select(profile.Id);
                        var editor = new EditorWindow(app, photo);
                        try {
                            editor.SelectTool("矩形"); editor.BeginStroke(new Point(62, 142)); editor.EndStroke(new Point(330, 340));
                            editor.SelectTool("箭头"); editor.BeginStroke(new Point(410, 220)); editor.EndStroke(new Point(590, 220));
                            Assert(editor.Shapes.Count == 2, "Expected rectangle and arrow, actual " + editor.Shapes.Count);
                            var arrow = editor.Shapes[1];
                            Assert(!arrow.Geometry.IsEmpty() && arrow.Bounds.Width > 175 && arrow.Hit(new Point(500, 220), 1), "Arrow geometry lost: " + arrow.Geometry + " bounds " + arrow.Bounds);
                            editor.SelectTool("选择"); editor.BeginStroke(new Point(62, 200)); editor.EndStroke(new Point(62, 200));
                            var bitmap = editor.Flatten(); var pixels = Pixels(bitmap); var i = (220 * bitmap.PixelWidth + 500) * 4;
                            Assert(pixels[i] != 255 || pixels[i + 1] != 255 || pixels[i + 2] != 255, "Retained arrow does not render");
                        } finally { Clean(editor); }
                    });
                } finally { frame.Continue = false; }
            }));
            Dispatcher.PushFrame(frame);
        }
    }
    static byte[] Pixels(BitmapSource bitmap) { var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4]; new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0).CopyPixels(pixels, bitmap.PixelWidth * 4, 0); return pixels; }
    static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
