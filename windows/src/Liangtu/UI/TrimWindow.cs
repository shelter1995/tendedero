using System.Threading;
namespace Liangtu;
public sealed partial class TrimWindow : Window
{
    readonly App app;
    readonly Photo photo;
    readonly VideoPlayback player;
    readonly VideoTimeline timeline = new(true);
    readonly TextBlock clock = VideoChrome.Label("00:00.000 / 00:00.000", 12, false), kept = VideoChrome.Label("正在载入视频", 13, false), status = VideoChrome.Label("拖动两端手柄保留片段 · 点击胶片定位");
    readonly TextBox startInput = new(), endInput = new();
    readonly Button play, save, reset, undo, redo;
    VideoEditHistory? history;
    readonly VideoAnnotationSurface annotations;
    readonly VideoAnnotationTrack elementTrack = new();
    readonly DockPanel workArea = new();
    CancellationTokenSource? exportCancellation;
    Button cancelButton = null!;
    bool editingRange;
    bool editingAttributes;
    readonly CancellationTokenSource lifetime = new();
    bool saving, closed, failed, restoringRange;
    (double start, double end)? retryRange;
    internal VideoTimeline Timeline => timeline;
    internal bool HasPendingSelection => annotations.Elements.Count > 0 || player.Ready && (timeline.Range.Start > .000001 || timeline.Range.End < player.Duration - .000001);
    internal VideoPlayback Playback => player;
    internal VideoAnnotationSurface AnnotationSurface => annotations;
    internal VideoAnnotationTrack ElementTrack => elementTrack;
    public TrimWindow(App app, Photo photo)
    {
        this.app = app; this.photo = photo;
        Title = Path.GetFileName(photo.Path) + " — 视频编辑"; VideoChrome.Window(this);
        MinHeight = Math.Min(700,Math.Max(480,SystemParameters.WorkArea.Height - 24));
        MinWidth = 760; Height = Math.Min(900,SystemParameters.WorkArea.Height - 40);
        player = new VideoPlayback(photo.Path, false);
        annotations = new VideoAnnotationSurface(player.Media, player.Pause) { TextFont = app.Settings.TextFontFamily, TextSize = Math.Clamp(app.Settings.TextFontSize, 6, 512), Bold = app.Settings.TextBold, Italic = app.Settings.TextItalic, Ink = AnnotationColors.Parse(app.Settings.AnnotationColor) };
        play = VideoChrome.Icon("播放选区 · 空格", VideoChrome.Play,() => {annotations.CommitText();player.Toggle();});
        save = Theme.Primary("保存片段", Save); VideoChrome.Hint(save, "保存片段 · Ctrl + S"); save.IsEnabled = false; save.MinWidth = 104;
        undo = VideoChrome.Icon("撤销 · Ctrl + Z", ToolbarVisual.Undo, UndoSelection); undo.IsEnabled = false;
        redo = VideoChrome.Icon("重做 · Ctrl + Y / Ctrl + Shift + Z", ToolbarVisual.Redo, RedoSelection); redo.IsEnabled = false;
        reset = Theme.Button("重置选区", () => timeline.ResetSelection()); reset.MinHeight = 32; reset.Padding = new Thickness(9,5,9,5); reset.SetResourceReference(StyleProperty,"ToolbarButton"); VideoChrome.Hint(reset,"恢复完整视频范围（可撤销）"); reset.IsEnabled = false;
        Content = BuildLayout(); timeline.SetFrames(Enumerable.Repeat(photo.Image, 8).ToArray());
        timeline.SelectionChanged += isStart => { if (!restoringRange) failed = false; player.Start = timeline.Range.Start; player.End = timeline.Range.End; annotations.CreationRange = new(player.Start, player.End); player.Seek(isStart ? player.Start : player.End); UpdateFields(); if (!restoringRange && !editingRange) CommitSelection(); };
        timeline.SeekRequested += player.Seek;
        timeline.ScrubStarted += () => { editingRange = true; player.BeginScrub(); };
        timeline.ScrubCompleted += () => { player.EndScrub(); editingRange = false; CommitSelection(); };
        player.Opened += () => {
            timeline.SetDuration(player.Duration);
            history ??= new VideoEditHistory(new(new(0,player.Duration), Array.Empty<VideoAnnotation>()));
            annotations.SetSize(player.Media.NaturalVideoWidth, player.Media.NaturalVideoHeight);
            if (retryRange is { } range) { restoringRange = true; timeline.SetEnd(range.end); timeline.SetStart(range.start); restoringRange = false; retryRange = null; }
            annotations.CreationRange = new(timeline.Range.Start,timeline.Range.End); annotations.IsEnabled = true; UpdateFields(); UpdateHistory(); save.IsEnabled = true; UpdateElementTrack(); _ = LoadFrames();
        };
        player.Changed += SyncPlayback;
        annotations.IsEnabled = false;
        annotations.Edited += () => { if (!restoringRange && !editingAttributes) CommitSelection(); UpdateElementTrack(); UpdateFields(); };
        annotations.SelectionChanged += () => { SyncAnnotationProperties(); UpdateElementTrack(); UpdateFields(); };
        annotations.Hint += message => status.Text = message;
        elementTrack.EditStarted += () => { player.Pause(); annotations.CommitText(); };
        elementTrack.Selected += id => annotations.Select(id);
        elementTrack.SeekRequested += player.Seek;
        elementTrack.TimeChanging += (id, time) => { annotations.SetTime(id,time,false); UpdateFields(); };
        elementTrack.EditCompleted += () => { CommitSelection(); UpdateElementTrack(); UpdateFields(); };
        player.Failed += message => { failed = true; status.Text = message; save.IsEnabled = false; timeline.IsEnabled = false; };
        PreviewKeyDown += OnKey;
        SizeChanged += (_,_) => elementTrack.MaxHeight = ActualHeight < 840 ? 38 : 108;
        Closing += (_, e) => { if (saving) { exportCancellation?.Cancel(); e.Cancel = true; } };
        Closed += (_, _) => { closed = true; lifetime.Cancel(); annotations.CommitText(false); annotations.CancelGesture(); player.Dispose(); lifetime.Dispose(); };
        SyncPlayback();
    }
    UIElement BuildLayout()
    {
        var root = new DockPanel { Background = Theme.WindowSurface }; workArea.Background = Theme.CanvasSurface;
        InstallAnnotationToolbar(root,workArea);
        var properties = BuildAnnotationProperties(); DockPanel.SetDock(properties,Dock.Top); root.Children.Add(properties);
        var bottom = new StackPanel();
        var transport = new Grid { Margin = new Thickness(24, 12, 24, 4) };
        transport.ColumnDefinitions.Add(new ColumnDefinition()); transport.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); transport.ColumnDefinitions.Add(new ColumnDefinition());
        clock.FontFamily = new FontFamily("Segoe UI"); transport.Children.Add(clock);
        var playback = new StackPanel { Orientation = Orientation.Horizontal };
        playback.Children.Add(VideoChrome.Icon("后退 0.1 秒 · ←", VideoChrome.Back, () => player.Seek(timeline.Range.ClampSelection(player.Position - .1)))); playback.Children.Add(play);
        playback.Children.Add(VideoChrome.Icon("前进 0.1 秒 · →", VideoChrome.Forward, () => player.Seek(timeline.Range.ClampSelection(player.Position + .1))));
        Grid.SetColumn(playback, 1); transport.Children.Add(playback);
        kept.HorizontalAlignment = HorizontalAlignment.Right; kept.Foreground = Theme.Accent; Grid.SetColumn(kept, 2); transport.Children.Add(kept); bottom.Children.Add(transport);
        elementTrack.Margin = new Thickness(24,0,24,0); bottom.Children.Add(elementTrack);
        timeline.Margin = new Thickness(24, 0, 24, 0); bottom.Children.Add(timeline);
        var fields = new Grid { Margin = new Thickness(24, 2, 24, 16) };
        fields.ColumnDefinitions.Add(new ColumnDefinition()); fields.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var inputs = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        inputs.Children.Add(TimeField("开始", startInput, true)); var arrow = VideoChrome.Label("—"); arrow.Margin = new Thickness(12, 0, 12, 0); inputs.Children.Add(arrow); inputs.Children.Add(TimeField("结束", endInput, false));reset.VerticalAlignment=VerticalAlignment.Bottom;reset.Margin=new Thickness(10,0,0,0);inputs.Children.Add(reset); fields.Children.Add(inputs);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; cancelButton = Theme.Button("取消",() => { if (saving) exportCancellation?.Cancel(); else Close(); }); actions.Children.Add(cancelButton); actions.Children.Add(save);
        var annotationActions = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom }; annotationActions.Children.Add(elementFields); annotationActions.Children.Add(actions); Grid.SetColumn(annotationActions,1); fields.Children.Add(annotationActions); bottom.Children.Add(fields);
        status.Margin = new Thickness(24, 0, 24, 12); bottom.Children.Add(status);
        var footer = VideoChrome.Bar(bottom, true); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer); annotationMat=Theme.Panel(annotations);annotationMat.Background=Theme.CanvasSurface;workArea.Children.Add(annotationMat); root.Children.Add(workArea); return root;
    }
    UIElement TimeField(string label, TextBox input, bool start)
    {
        var group = new StackPanel(); var text = VideoChrome.Label(label, 11); text.Margin = new Thickness(0, 0, 0, 5); group.Children.Add(text);
        input.Text = "00:00.000"; input.Width = 114; input.Padding = new Thickness(10, 6, 10, 6); input.FontFamily = new FontFamily("Segoe UI"); input.TextAlignment = TextAlignment.Center;
        var box = new FrameworkElementFactory(typeof(Border)); box.Name = "TimeBox"; box.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
        box.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        box.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        box.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        var host = new FrameworkElementFactory(typeof(ScrollViewer)); host.Name = "PART_ContentHost"; host.SetBinding(MarginProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent }); box.AppendChild(host);
        var template = new ControlTemplate(typeof(TextBox)) { VisualTree = box };
        var focus = new Trigger { Property = IsKeyboardFocusWithinProperty, Value = true }; focus.Setters.Add(new Setter(Border.BorderBrushProperty, Theme.Accent, "TimeBox")); template.Triggers.Add(focus);
        var disabled = new Trigger { Property = IsEnabledProperty, Value = false }; disabled.Setters.Add(new Setter(OpacityProperty, .45)); template.Triggers.Add(disabled);
        input.Template = template;
        VideoChrome.Hint(input, label + "时间 · 分:秒.毫秒 或秒数"); input.IsEnabled = false;
        input.LostKeyboardFocus += (_, _) => CommitTime(input, start);
        input.PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter) { CommitTime(input, start); e.Handled = true; } };
        group.Children.Add(input); return group;
    }
    bool CommitTime(TextBox input, bool start)
    {
        if (!player.Ready || saving) return true;
        if (!VideoRange.TryParseTime(input.Text, out var value)) { status.Text = "请输入有效时间，例如 00:02.500 或 2.5"; input.BorderBrush = Theme.Accent; return false; }
        if (start) timeline.SetStart(value); else timeline.SetEnd(value);
        input.Text = VideoRange.Format(start ? timeline.Range.Start : timeline.Range.End); input.BorderBrush = Theme.Outline; return true;
    }
    void UpdateFields()
    {
        if (!startInput.IsKeyboardFocusWithin) startInput.Text = VideoRange.Format(timeline.Range.Start);
        if (!endInput.IsKeyboardFocusWithin) endInput.Text = VideoRange.Format(timeline.Range.End);
        kept.Text = $"保留 {timeline.Range.End - timeline.Range.Start:0.00} 秒";
        UpdateElementFields();
        if (!saving && !failed) status.Text = "拖动两端手柄保留片段 · 点击胶片定位";
    }
    void CommitSelection()
    {
        if (player.Ready && !restoringRange) history?.Commit(new(new(timeline.Range.Start,timeline.Range.End),annotations.Elements));
        UpdateHistory();
    }
    void UpdateHistory()
    {
        undo.IsEnabled = player.Ready && !saving && history?.CanUndo == true;
        redo.IsEnabled = player.Ready && !saving && history?.CanRedo == true;
        reset.IsEnabled = player.Ready && !saving && (timeline.Range.Start > .000001 || timeline.Range.End < player.Duration - .000001);
    }
    void RestoreSelection()
    {
        if (history == null) return;
        player.EndScrub(); restoringRange = true; timeline.SetSelection(history.Current.Selection); annotations.SetElements(history.Current.Elements); restoringRange = false; UpdateElementTrack(); UpdateFields();
        startInput.Text = VideoRange.Format(timeline.Range.Start); endInput.Text = VideoRange.Format(timeline.Range.End); UpdateHistory();
    }
    void UndoSelection() { if (saving || !player.Ready || editingRange || editingAttributes) return; if(history?.Undo()==true){RestoreSelection();status.Text="已撤销编辑";} }
    void RedoSelection() { if (saving || !player.Ready || editingRange || editingAttributes) return; if(history?.Redo()==true){RestoreSelection();status.Text="已重做编辑";} }
    void SyncPlayback()
    {
        if (closed) return;
        timeline.SetPosition(player.Position); clock.Text = VideoRange.Format(player.Position) + " / " + VideoRange.Format(player.Duration);
        annotations.SetPosition(player.Position,player.Playing); elementTrack.SetPosition(player.Position);
        var name = player.Playing ? "暂停 · 空格" : "播放选区 · 空格";
        if (!Equals(play.ToolTip, name)) { play.Content = VideoChrome.Glyph(player.Playing ? VideoChrome.Pause : VideoChrome.Play); VideoChrome.Hint(play, name); }
        play.IsEnabled = player.Ready && !saving; startInput.IsEnabled = endInput.IsEnabled = player.Ready && !saving;
        UpdateHistory();
    }
    async Task LoadFrames()
    {
        try { await VideoFrames.Load(photo.Path, player.Duration, frames => { if (!closed && !saving) timeline.SetFrames(frames); }, lifetime.Token); }
        catch (OperationCanceledException) { }
        catch (Exception e) { Settings.Log("Video filmstrip: " + e.Message); }
    }
    void OnKey(object sender, KeyEventArgs e)
    {
        if(AnnotationTextEditor.HasComposition(e.OriginalSource as DependencyObject))return;
        if (saving) { if (e.Key == Key.Escape) exportCancellation?.Cancel(); e.Handled = true; return; }
        if (e.Key == Key.S && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { Save(); e.Handled = true; return; }
        if(AnnotationControls.IsRangeInput(e.OriginalSource as DependencyObject)){
            var mods=Keyboard.Modifiers;
            if(e.Key==Key.Z&&mods==ModifierKeys.Control){UndoSelection();e.Handled=true;}
            else if(e.Key==Key.Y&&mods==ModifierKeys.Control||e.Key==Key.Z&&mods==(ModifierKeys.Control|ModifierKeys.Shift)){RedoSelection();e.Handled=true;}
            return;
        }
        if (elementTrack.IsKeyboardFocusWithin && e.Key is Key.Left or Key.Right) return;
        if(AnnotationControls.IsAttributeInput(e.OriginalSource as DependencyObject))return;
        for (var node = e.OriginalSource as DependencyObject; node != null; node = node is Visual ? VisualTreeHelper.GetParent(node) : null)
            if (node is ComboBox or System.Windows.Controls.Primitives.ToggleButton) return;
        if (HandleAnnotationKey(e)) return;
        if (e.OriginalSource is TextBox && e.Key != Key.Escape && !(e.Key == Key.S && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))) return;
        var modifiers = Keyboard.Modifiers;
        if (e.Key == Key.Space) annotations.CommitText();
        if (e.Key == Key.Z && modifiers == ModifierKeys.Control) UndoSelection();
        else if (e.Key == Key.Y && modifiers == ModifierKeys.Control || e.Key == Key.Z && modifiers == (ModifierKeys.Control | ModifierKeys.Shift)) RedoSelection();
        else if (e.Key == Key.Space) player.Toggle();
        else if (e.Key == Key.Escape) Close();
        else if (e.Key == Key.S && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) Save();
        else if (e.Key is Key.Left or Key.Right && e.OriginalSource is not TimelineThumb) player.Seek(timeline.Range.ClampSelection(player.Position + (e.Key == Key.Left ? -.1 : .1)));
        else return;
        e.Handled = true;
    }
    void UpdateElementTrack() => elementTrack.SetElements(annotations.Elements,player.Duration,annotations.SelectedId);
    async void Save()
    {
        if (saving || !player.Ready) return;
        annotations.CommitText(); annotations.CancelGesture();
        if (!CommitElementFields()) return;
        if (!CommitTime(startInput, true) || !CommitTime(endInput, false)) return;
        var s = timeline.Range.Start; var e = timeline.Range.End; if (e <= s) return;
        saving = true; lifetime.Cancel(); annotations.IsEnabled = elementTrack.IsEnabled = timeline.IsEnabled = annotationToolbar.IsEnabled = annotationProperties.IsEnabled = false; save.IsEnabled = false; UpdateHistory(); player.CloseMedia(); status.Text = "正在保存视频，请稍候…"; save.Content = "正在保存…"; cancelButton.Content = "取消保存";
        exportCancellation = new CancellationTokenSource();
        try { await VideoAnnotationExporter.Export(photo.Path,new(s,e),annotations.Elements.ToArray(),annotations.PixelWidth,annotations.PixelHeight,exportCancellation.Token,new Progress<double>(value => { if (saving) status.Text = $"正在保存视频… {value:P0}"; })); await app.Store.Reload(photo); saving = false; Close(); }
        catch (Exception ex) { saving = false; save.Content = "保存片段"; cancelButton.Content = "取消"; failed = true; status.Text = ex is OperationCanceledException ? "已取消保存，原视频和编辑内容已保留。" : "保存失败，原视频已保留。" + ex.Message; status.ToolTip = status.Text; retryRange = (s,e); annotations.IsEnabled = elementTrack.IsEnabled = annotationToolbar.IsEnabled = annotationProperties.IsEnabled = true; player.Reopen(photo.Path); }
        finally { exportCancellation.Dispose(); exportCancellation = null; }
    }
}
