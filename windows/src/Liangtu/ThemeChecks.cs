namespace Liangtu;

internal static class ThemeChecks
{
    [STAThread]
    public static int Main(string[] args)
    {
        Settings.Root = Path.GetFullPath(args[0]); Directory.CreateDirectory(Settings.Inbox);
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown }; var results = new List<string>();
        app.Startup += async (_, _) => {
            void Check(string name, Action action) { try { action(); results.Add("PASS " + name); } catch (Exception e) { results.Add("FAIL " + name + ": " + e); } }
            var settings = new Settings { Sounds = false, Welcomed = true, ThemeId = "sky", CollectionRevision = 1 };
            typeof(App).GetProperty(nameof(App.Settings))!.SetValue(app, settings); typeof(App).GetProperty(nameof(App.TestMode))!.SetValue(app, true);
            var store = new Store(settings); typeof(App).GetProperty(nameof(App.Store))!.SetValue(app, store);
            var line = new LineWindow(app); typeof(App).GetProperty(nameof(App.Line))!.SetValue(app, line);
            try {
                var path = Path.Combine(Settings.Inbox, "项目界面参考.png"); CreateSample(path); await store.Add(path); var photo = store.Collection[0];
                Check("shutdown gate cannot open another editor", () => {
                    var flag = typeof(App).GetField("exiting", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
                    flag.SetValue(app, true); var before = app.Windows.OfType<EditorWindow>().Count();
                    try { app.Edit(photo); if (app.Windows.OfType<EditorWindow>().Count() != before) throw new Exception("Editor opened during shutdown handoff"); }
                    finally { flag.SetValue(app, false); foreach (var editor in app.Windows.OfType<EditorWindow>().ToArray()) Clean(editor); }
                });
                Check("update handoff blocks edit preview capture and pin and can be cancelled", () => {
                    var flag = typeof(App).GetField("updateHandoff", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
                    var before = app.Windows.Count;
                    try { flag.SetValue(app, true); app.Edit(photo); app.Preview(photo); app.Capture(); app.Pin(photo); if (app.Windows.Count != before || !app.WorkBlocked) throw new Exception("New work bypassed update handoff"); }
                    finally { flag.SetValue(app, false); }
                    if (app.WorkBlocked) throw new Exception("Cancelled handoff still blocks work");
                });
                var staged = Path.Combine(Settings.Root, "invalid-upgrade"); Directory.CreateDirectory(staged);
                var stagedExe = Path.Combine(staged, "Liangtu.exe"); File.WriteAllText(stagedExe, "synthetic prepared payload");
                var prepared = new Updates.PreparedUpdate(new Updates.StagedPackage(staged, Updates.UpdateVersion.Parse("9.0.0"), "portable", null), Path.Combine(Settings.Root, "upgrade-work"));
                File.AppendAllText(stagedExe, "tampered");
                bool rejected = false;
                try { await (Task<bool>)typeof(App).GetMethod("ApplyPreparedUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(app, new object[] { prepared })!; }
                catch (InvalidDataException) { rejected = true; }
                Check("failed updater handoff restores editing and clipboard exit gate", () => {
                    var copyStopped = (bool)typeof(Store).GetField("exitRequested", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(store)!;
                    if (!rejected || app.WorkBlocked || !line.IsEnabled || copyStopped) throw new Exception("Failed handoff left work disabled");
                });
                var second = Path.Combine(Settings.Inbox, "版本计划.png"); File.Copy(path, second, true); await store.Add(second);
                Check("seven complete palettes preserve legacy IDs and four original material constructions", () => {
                    if (ThemeProfiles.All.Count != 7 || ThemeProfiles.All.Select(p => p.Material).Distinct().Count() != 7) throw new Exception("Material variants missing");
                    foreach(var id in new[]{"sky","paper","night","porcelain","darkroom","linen","color-play"}) if(!ThemeProfiles.Valid(id) || ThemeProfiles.Resolve(id,false).Id!=id) throw new Exception("Theme ID no longer load compatible: "+id);
                    var originals=ThemeProfiles.All.Where(p=>p.OriginalArtwork).ToArray();
                    if(originals.Length!=4 || originals.Select(p=>p.PhotoMat).Distinct().Count()!=4 || originals.Select(p=>p.ControlRadius).Distinct().Count()!=4) throw new Exception("Only palette recolors were supplied");
                    foreach (var p in ThemeProfiles.All) foreach (var key in p.Colors.Keys) _ = p.Color(key);
                });
                Check("palette brushes stay bound and recolor existing controls", () => {
                    Theme.Select("sky"); var brush = (SolidColorBrush)Theme.Ink; var before = brush.Color;
                    Theme.Select("night"); if (brush.IsFrozen || brush.Color == before || !ReferenceEquals(brush, Theme.Ink)) throw new Exception("Live brush did not update");
                });
                Check("theme body and secondary text meet4.5to1contrast on window and control surfaces", () => {
                    double L(Color c) { double V(byte b) { var v = b / 255d; return v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4); } return .2126 * V(c.R) + .7152 * V(c.G) + .0722 * V(c.B); }
                    foreach (var p in ThemeProfiles.All) foreach (var ink in new[] { "Ink", "Muted" }) foreach (var face in new[] { "WindowSurface", "ControlSurface" }) {
                        var a = L(p.Color(ink)); var b = L(p.Color(face)); var contrast = (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
                        if (contrast < 4.5) throw new Exception(p.Name + " " + ink + " contrast=" + contrast);
                    }
                });
                Check("theme selection persists without replacing user media", () => {
                    foreach(var p in ThemeProfiles.All) { app.SetTheme(p.Id); var saved = Settings.Load(); if (saved.ThemeId != p.Id || !File.Exists(path)) throw new Exception("Theme persistence: "+p.Id); }
                });
                Check("live theme changes update native panel geometry without replacing controls", () => {
                    var host=new Window(); Theme.WindowStyle(host); var child=new Button { Content="existing control" }; var panel=Theme.Panel(child); host.Content=panel;
                    foreach(var p in ThemeProfiles.All) { Theme.Select(p.Id); if(!ReferenceEquals(panel.Child,child) || panel.CornerRadius != (CornerRadius)host.Resources["PanelRadius"]) throw new Exception("Stale panel geometry: "+p.Id); }
                    Theme.Select("color-play"); Capture(new MaterialPanel(),10,10,Theme.Current.Color("WindowSurface"));
                    host.Close();
                });
                Check("basket mouth contract and peg animation render for every material at 100 150 and 200 percent", () => {
                    foreach(var p in ThemeProfiles.All) {
                        foreach(var scale in new[]{1d,1.5,2d}) {
                            var art=new BasketVisual { Palette=p, Width=p.BasketArtWidth*scale, Height=p.BasketArtHeight*scale };
                            Capture(art,art.Width,art.Height,p.Color("WindowSurface"));
                            var bounds=art.MouthBounds;
                            var expected=RasterThemeArt.Has(p)?RasterThemeArt.BasketMouth(p):new Rect(17,31,30,24);
                            if(Math.Abs(bounds.X/(p.BasketArtWidth*scale)-expected.X/64)>.0001 || Math.Abs(bounds.Y/(p.BasketArtHeight*scale)-expected.Y/76)>.0001) throw new Exception("Mouth anchor shifted: "+p.Id);
                        }
                        ArtworkBoard(p);
                    }
                });
                Check("larger material baskets reserve a separate lane even with80photos", () => {
                    foreach(var p in ThemeProfiles.All) foreach(var width in new[]{640d,1280d,1920d,3440d}) foreach(var count in new[]{1,12,80}) {
                        var layout=Liangtu.Core.ShelfLayout.Create(width,count,1.35,p.BasketButtonWidth);
                        var basketLeft=width-p.BasketButtonWidth-14;
                        if(layout.Right>basketLeft-40 || layout.Visible<1 || !double.IsFinite(layout.MaxScroll)) throw new Exception("Basket collides with photo lane: "+p.Id);
                        if(p.Id is "linen" or "color-play" && (p.BasketArtWidth<100 || p.BasketArtHeight<118)) throw new Exception("Material basket still undersized");
                        foreach(var offset in new[]{0d,layout.MaxScroll}) for(int i=0;i<count;i++) {
                            var center=layout.Center(i,offset); var clippedRight=Math.Min(layout.Right,center+125);
                            if(clippedRight>=basketLeft) throw new Exception("Clipped card crosses basket lane");
                        }
                    }
                });
                Check("ColorPlay native frame fits wide portrait and smallphotos without decoding angledframePNG", () => {
                    var p=ThemeProfiles.ColorPlay;
                    foreach(var size in new[]{new Size(220,140),new Size(110,180),new Size(280,80),new Size(52,40)}) foreach(var scale in new[]{1d,1.5,2d}) {
                        var frame=new ThemeFramePreview(p) { Width=size.Width,Height=size.Height };
                        frame.Measure(size); frame.Arrange(new Rect(size)); frame.UpdateLayout();
                        var bitmap=new RenderTargetBitmap((int)Math.Ceiling(size.Width*scale),(int)Math.Ceiling(size.Height*scale),96*scale,96*scale,PixelFormats.Pbgra32); bitmap.Render(frame);
                        MediaTools.SavePng(bitmap,Path.Combine(Settings.Root,$"color-play-frame-{size.Width}x{size.Height}-{scale*100}.png"));
                        var front=RasterThemeArt.ColorPlayPaperBounds(new Rect(size)); var mat=p.PhotoMat;
                        if(front.Right<size.Width-mat.Right+4 || front.Bottom<size.Height-mat.Bottom+4 || p.PhotoImageRadius!=4) throw new Exception("Photo inset escaped rounded white mat");
                    }
                    if(RasterThemeArt.HasDecodedFrame("color-play")) throw new Exception("ColorPlay still decodes angled frame asset");
                });
                Check("generated frame openings retain opaque backing for desktop status labels", () => {
                    foreach(var p in new[]{ThemeProfiles.Linen,ThemeProfiles.ColorPlay}) {
                        var visual=new DrawingVisual(); using(var dc=visual.RenderOpen()) RasterThemeArt.Frame(dc,p,new Rect(0,0,240,100));
                        var bitmap=new RenderTargetBitmap(240,100,96,96,PixelFormats.Pbgra32); bitmap.Render(visual);
                        var pixel=new byte[4]; bitmap.CopyPixels(new Int32Rect(120,50,1,1),pixel,4,0);
                        if(pixel[3]!=255) throw new Exception("Desktop leaks behind status text: "+p.Id);
                    }
                });
                Check("all material paths and theme picker render under comma decimal cultures", () => {
                    var savedCulture=System.Globalization.CultureInfo.CurrentCulture;
                    try {
                        foreach(var culture in new[]{"fr-FR","de-DE"}) {
                            System.Globalization.CultureInfo.CurrentCulture=System.Globalization.CultureInfo.GetCultureInfo(culture);
                            foreach(var p in ThemeProfiles.All) {
                                Theme.Select(p.Id); Capture(new BasketVisual { Palette=p },72,86,p.Color("WindowSurface"));
                                Capture(new ThemeFramePreview(p),180.5,118.5,p.Color("WindowSurface"));
                                Capture(new ThemeScene(p),310,200,p.Color("WindowSurface"));
                            }
                            var picker=new ThemeWindow(app); try { CaptureWindow(picker,980,740); } finally { picker.Close(); }
                        }
                    } finally { System.Globalization.CultureInfo.CurrentCulture=savedCulture; }
                });
                var liveEditor = new EditorWindow(app, photo); liveEditor.SelectTool("矩形"); liveEditor.BeginStroke(new Point(130, 160)); liveEditor.EndStroke(new Point(340, 280));
                var pixels = Pixels(liveEditor.Flatten()); var shape = liveEditor.Shapes.Single();
                Check("theme switching preserves unsaved geometry history and exported pixels", () => {
                    foreach (var p in ThemeProfiles.All) {
                        app.SetTheme(p.Id);
                        if (liveEditor.Shapes.Single() != shape || !pixels.SequenceEqual(Pixels(liveEditor.Flatten()))) throw new Exception("Theme changed image data");
                        if(liveEditor.NativeToolDock!=(p.Id=="color-play"?Dock.Left:Dock.Top) || liveEditor.NativeToolLabelsVisible!=(p.Id is "linen" or "color-play")) throw new Exception("Wrong native toolbar layout: "+p.Id);
                    }
                    liveEditor.Undo(); if (liveEditor.Shapes.Count != 0) throw new Exception("Undo lost across theme change"); liveEditor.Redo(); if (liveEditor.Shapes.Single() != shape) throw new Exception("Redo lost across theme change");
                });
                Clean(liveEditor);
                Check("thumbnail gallery realizes a bounded viewport from5000items and scrolls toend", () => {
                    var list=new ListBox { ItemsSource=Enumerable.Range(0,5000).ToArray(), ItemsPanel=new ItemsPanelTemplate(new FrameworkElementFactory(typeof(VirtualizingTilePanel))) };
                    ScrollViewer.SetCanContentScroll(list,true); ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Disabled);
                    var host=new Window { Content=list }; Theme.WindowStyle(host);
                    try {
                    CaptureWindow(host,540,350);
                    T? Find<T>(DependencyObject element) where T:DependencyObject { if(element is T result) return result; for(int i=0;i<VisualTreeHelper.GetChildrenCount(element);i++) if(Find<T>(VisualTreeHelper.GetChild(element,i)) is T child) return child; return null; }
                    var panel=Find<VirtualizingTilePanel>(list)??throw new Exception("Tile panel not realized");
                    if(panel.RealizedCount<1 || panel.RealizedCount>30) throw new Exception("Unbounded gallery realization: "+panel.RealizedCount);
                    panel.SetVerticalOffset(panel.ExtentHeight); CaptureWindow(host,540,350);
                    if(panel.RealizedCount>30 || list.ItemContainerGenerator.ContainerFromIndex(4999)==null) throw new Exception("Last tile is unreachable or grid realizes full history");
                    list.SelectedIndex=4999; if((int)list.SelectedItem!=4999) throw new Exception("Tile selection lost");
                    } finally { host.Close(); }
                });
                Check("basket theme reflow preserves multiselection and filtering", () => {
                    var basket=new BasketWindow(app); try {
                        basket.MediaList.SelectAll(); var count=basket.MediaList.SelectedItems.Count;
                        foreach(var p in ThemeProfiles.All) { app.SetTheme(p.Id); CaptureWindow(basket,1000,640); if(basket.MediaList.SelectedItems.Count!=count) throw new Exception("Selection lost: "+p.Id); }
                        basket.SearchBox.Text="版本计划"; if(basket.MediaList.Items.Count!=1) throw new Exception("Gallery filename filter failed");
                    } finally { basket.Close(); }
                });
                foreach (var profile in ThemeProfiles.All) {
                    app.SetTheme(profile.Id);
                    line.ApplyDisplayLayout(new DisplayGeometry("theme-preview", new System.Drawing.Rectangle(0, 0, 1500, 900), 1)); line.Refresh();
                    var lineCanvas = ((Canvas)line.Content).Children.OfType<Canvas>().Single(); ((TranslateTransform)lineCanvas.RenderTransform).BeginAnimation(TranslateTransform.YProperty, null); ((TranslateTransform)lineCanvas.RenderTransform).Y = 0;
                    foreach (var card in lineCanvas.Children.OfType<Card>()) card.FinishArrival();
                    var basket = new BasketWindow(app); basket.MediaList.SelectedIndex = 0;
                    var editor = new EditorWindow(app, photo); editor.SelectTool("矩形"); editor.BeginStroke(new Point(62, 142)); editor.EndStroke(new Point(330, 340));
                    editor.SelectTool("箭头"); editor.BeginStroke(new Point(410, 220)); editor.EndStroke(new Point(590, 220));
                    editor.SelectTool("选择"); editor.BeginStroke(new Point(62, 200)); editor.EndStroke(new Point(62, 200));
                    var update = new UpdateWindow(_ => Task.FromResult(false)); var pin = new PinnedImageWindow(app, photo); var picker = new ThemeWindow(app);
                    await Task.Delay(80);
                    try {
                        Check(profile.Name + " renders shelf basket editor pin updates and theme picker", () => {
                            if (editor.Shapes.Count != 2 || editor.Shapes.Any(s => s.Geometry.IsEmpty() || s.Bounds.IsEmpty)) throw new Exception("Theme preview lost authored shape geometry");
                            line.ApplyDisplayLayout(new DisplayGeometry("theme-preview", new System.Drawing.Rectangle(0, 0, 1500, 900), 1)); line.Refresh();
                            ((TranslateTransform)lineCanvas.RenderTransform).BeginAnimation(TranslateTransform.YProperty, null); ((TranslateTransform)lineCanvas.RenderTransform).Y = 0;
                            foreach (var element in lineCanvas.Children.OfType<UIElement>()) element.InvalidateVisual();
                            var shelf = Capture((FrameworkElement)line.Content, 1500, 220, profile.Color("WindowSurface"));
                            var edit = CaptureWindow(editor, 1100, 740); var bin = CaptureWindow(basket, 1000, 640); var updates = CaptureWindow(update, 620, 540); var pinned = CaptureWindow(pin, 450, 300); var themes = CaptureWindow(picker, 1000, 740);
                            if(profile.OriginalArtwork) MediaTools.SavePng(CaptureWindow(editor,760,520),Path.Combine(Settings.Root,profile.Id+"-editor-compact.png"));
                            if(picker.ThemeButtons.Count!=7 || picker.ThemeButtons.Any(b=>!b.Focusable) || picker.ThemeScroll.ScrollableHeight<=0) throw new Exception("Theme picker is not keyboard accessible and scrollable");
                            picker.ThemeButtons[^1].RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                            if(app.Settings.ThemeId!="night") throw new Exception("Last theme cannot be selected"); app.SetTheme(profile.Id);
                            foreach (var entry in new[] { ("shelf", shelf), ("editor", edit), ("basket", bin), ("updates", updates), ("pin", pinned), ("themes", themes) }) MediaTools.SavePng(entry.Item2, Path.Combine(Settings.Root, profile.Id + "-" + entry.Item1 + ".png"));
                            Board(profile, shelf, edit, bin, updates, pinned, themes);
                        });
                    } finally { Clean(editor); basket.Close(); pin.CloseWithoutReturn(); update.Close(); picker.Close(); }
                }
                Check("all theme previews are visually distinct", () => {
                    var hashes = ThemeProfiles.All.Select(p => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(Settings.Root, p.Id + "-board.png"))))).Distinct().Count();
                    if (hashes != ThemeProfiles.All.Count) throw new Exception("Duplicate theme boards");
                });
                Check("video timeline renders with live theme brushes in all seven themes", () => {
                    foreach (var profile in ThemeProfiles.All) {
                        Theme.Select(profile.Id); var timeline = new VideoTimeline(true); timeline.SetDuration(12); timeline.SetFrames(Enumerable.Repeat(photo.Image, 8).ToArray()); timeline.SetStart(2); timeline.SetEnd(9.5);
                        var bitmap = Capture(timeline, 900, 108, profile.Color("WindowSurface")); MediaTools.SavePng(bitmap, Path.Combine(Settings.Root, profile.Id + "-video-timeline.png"));
                    }
                });
            } catch (Exception e) { results.Add("FAIL runner: " + e); }
            finally { line.Close(); }
            await File.WriteAllLinesAsync(Path.Combine(Settings.Root, "results.txt"), results); app.Shutdown(results.Any(s => s.StartsWith("FAIL")) ? 1 : 0);
        };
        return app.Run();
    }
    static void Clean(EditorWindow editor) { for (int i = 0; i < 40; i++) editor.Undo(); editor.Close(); }
    static void ArtworkBoard(ThemeProfile p)
    {
        var root=new Canvas { Width=960, Height=480, Background=new SolidColorBrush(p.Color("WindowSurface")) };
        void Add(FrameworkElement el,double x,double y) { Canvas.SetLeft(el,x); Canvas.SetTop(el,y); root.Children.Add(el); }
        var title=new TextBlock { Text=p.Name+(RasterThemeArt.Has(p)?" · MiniMax素材原生渲染":" · 原生矢量材质"), Foreground=new SolidColorBrush(p.Color("Ink")), FontFamily=Theme.Font, FontSize=24 }; Add(title,24,18);
        Add(new BasketVisual { Palette=p,Width=256,Height=304 },20,95);
        Add(new ClothesPegVisual { Palette=p,Width=78,Height=120 },315,118);
        Add(new ClothesPegVisual { Palette=p,Width=78,Height=120,Opening=1 },425,118);
        Add(new ThemeScene(p),565,85);
        Add(new BasketVisual { Palette=p,Width=72,Height=86 },320,300);
        Add(new BasketVisual { Palette=p,Width=20,Height=20,Compact=true },425,340);
        Add(new ThemeFramePreview(p) { Width=180,Height=118 },610,316);
        MediaTools.SavePng(Capture(root,root.Width,root.Height,p.Color("WindowSurface")),Path.Combine(Settings.Root,p.Id+"-materials.png"));
    }
    static byte[] Pixels(BitmapSource image) { var bytes = new byte[image.PixelWidth * image.PixelHeight * 4]; image.CopyPixels(bytes, image.PixelWidth * 4, 0); return bytes; }
    static BitmapSource CaptureWindow(Window window, double width, double height)
    {
        var root = (FrameworkElement)window.Content;
        root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout();
        root.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
        var snapshot = Capture(root, width, height, (Theme.WindowSurface as SolidColorBrush)!.Color); return snapshot;
    }
    static BitmapSource Capture(FrameworkElement root, double width, double height, Color background)
    {
        root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
        var baseVisual = new DrawingVisual(); using (var dc = baseVisual.RenderOpen()) dc.DrawRectangle(new SolidColorBrush(background), null, new Rect(0, 0, width, height));
        bitmap.Render(baseVisual); bitmap.Render(root); bitmap.Freeze(); return bitmap;
    }
    static void Board(ThemeProfile p, BitmapSource shelf, BitmapSource editor, BitmapSource basket, BitmapSource update, BitmapSource pin, BitmapSource picker)
    {
        var root = new Canvas { Width = 1560, Height = 1240, Background = new SolidColorBrush(p.Color("WindowSurface")) };
        void Label(string text, double x, double y, double size) { var label = new TextBlock { Text = text, FontFamily = Theme.Font, FontSize = size, Foreground = new SolidColorBrush(p.Color("Ink")) }; Canvas.SetLeft(label, x); Canvas.SetTop(label, y); root.Children.Add(label); }
        void Place(BitmapSource bitmap, double x, double y, double width, double height) { var image = new Image { Source = bitmap, Width = width, Height = height, Stretch = Stretch.Uniform }; Canvas.SetLeft(image, x); Canvas.SetTop(image, y); root.Children.Add(image); }
        Label(p.Name, 30, 20, 30); Label(p.Description + "  /  原生界面预览", 275, 32, 15);
        Place(shelf, 30, 72, 1500, 220);
        Label("图片标注 · 对象可移动、复制和删除", 30, 294, 16); Place(editor, 30, 326, 920, 619);
        Label("图片篮子", 982, 294, 16); Place(basket, 982, 326, 548, 351);
        Label("Windows 更新", 982, 706, 16); Place(update, 982, 738, 548, 477);
        Label("桌面贴图", 30, 973, 16); Place(pin, 30, 1004, 390, 220);
        Label("整套主题切换", 450, 973, 16); Place(picker, 450, 1004, 500, 220);
        MediaTools.SavePng(Capture(root, root.Width, root.Height, p.Color("WindowSurface")), Path.Combine(Settings.Root, p.Id + "-board.png"));
    }
    static void CreateSample(string file)
    {
        var visual = new DrawingVisual(); using (var dc = visual.RenderOpen()) {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(239, 243, 248)), null, new Rect(0, 0, 900, 560));
            dc.DrawRoundedRectangle(Brushes.White, null, new Rect(36, 30, 828, 500), 12, 12);
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(45, 66, 86)), null, new Rect(36, 30, 828, 66));
            var title = new FormattedText("项目计划 / 工作台", System.Globalization.CultureInfo.GetCultureInfo("zh-CN"), FlowDirection.LeftToRight, new Typeface("Microsoft YaHei UI"), 25, Brushes.White, 1); dc.DrawText(title, new Point(64, 48));
            foreach (var x in new[] { 64, 334, 604 }) { dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(223, 231, 241)), null, new Rect(x, 145, 236, 185), 6, 6); dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(188, 202, 219)), null, new Rect(x, 352, 172, 12)); }
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(59, 111, 178)), null, new Rect(64, 430, 148, 40), 5, 5);
        }
        var bitmap = new RenderTargetBitmap(900, 560, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); MediaTools.SavePng(bitmap, file);
    }
}
