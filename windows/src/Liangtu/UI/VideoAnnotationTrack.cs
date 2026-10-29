using System.Windows.Controls.Primitives;

namespace Liangtu;

internal sealed class VideoAnnotationTrack : ScrollViewer
{
    readonly Canvas canvas = new() { Background = Brushes.Transparent, ClipToBounds = true };
    readonly Border cursor = new() { Width = 2, Background = Theme.Ink, IsHitTestVisible = false, RenderTransform = new TranslateTransform() };
    readonly Dictionary<Guid, (Grid row, VideoAnnotation element)> rows = new();
    IReadOnlyList<VideoAnnotation> elements = Array.Empty<VideoAnnotation>();
    Guid? selected;
    bool dragging;
    double duration, position;
    const double Inset = 18, RowHeight = 32;
    double TrackWidth => Math.Max(1, canvas.ActualWidth - 2 * Inset);
    double X(double seconds) => Inset + seconds / Math.Max(.001, duration) * TrackWidth;
    public event Action<Guid>? Selected;
    public event Action<double>? SeekRequested;
    public event Action? EditStarted, EditCompleted;
    public event Action<Guid, VideoElementTime>? TimeChanging;
    public VideoAnnotationTrack()
    {
        Content = canvas; HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden; VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        Height = MaxHeight = 108; Visibility = Visibility.Visible; Focusable = false;
        // Keep the full timeline width even when a vertical scrollbar appears.
        // Its 18-DIP end inset already leaves room for the scrollbar.
        SizeChanged += (_, _) => { canvas.Width = Math.Max(1,ActualWidth); RefreshPositions(); };
        System.Windows.Automation.AutomationProperties.SetName(this, "标注显示时长轨道");
    }
    public void SetElements(IReadOnlyList<VideoAnnotation> value, double length, Guid? id)
    {
        elements = value.ToArray(); duration = length; selected = id;
        if (!dragging) Rebuild();
    }
    public void SetPosition(double value) { position = value; if (elements.Count > 0) ((TranslateTransform)cursor.RenderTransform).X = X(value) - 1; }
    void Rebuild()
    {
        var hadFocus = IsKeyboardFocusWithin;
        canvas.Children.Clear(); rows.Clear();
        if (elements.Count == 0) {
            var empty = VideoChrome.Label("添加文字或图形后，在这里调整显示时长",11); empty.IsHitTestVisible = false; Canvas.SetLeft(empty,Inset); Canvas.SetTop(empty,8); canvas.Children.Add(empty);
        }
        var lanes = new List<double>();
        foreach (var element in elements.OrderBy(x => x.Time.Start)) {
            int lane = lanes.FindIndex(end => end <= element.Time.Start); if (lane < 0) { lane = lanes.Count; lanes.Add(0); } lanes[lane] = element.Time.End;
            var row = new Grid { Height = 26, ToolTip = element.Name + " · " + VideoRange.Format(element.Time.Start) + " — " + VideoRange.Format(element.Time.End), RenderTransform = new TranslateTransform(), Focusable = true };
            var face = new Border { Background = element.Id == selected ? Theme.Accent : Theme.Token("Hover"), BorderBrush = element.Id == selected ? Theme.Accent : Theme.Outline, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4) };
            var label = new TextBlock { Text = element.Text?.Text.Replace('\n', ' ') is { Length: > 0 } text ? text : element.Name, Margin = new Thickness(12, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 11, Foreground = element.Id == selected ? Theme.Token("OnAccent") : Theme.Ink, IsHitTestVisible = false };
            face.Child = label; row.Children.Add(face);
            foreach (var kind in new[] { "move", "start", "end" }) {
                var thumb = new Thumb { Cursor = Cursors.SizeWE, Background = Brushes.Transparent, Focusable = false };
                var border = new FrameworkElementFactory(typeof(Border)); border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
                if (kind != "move") { thumb.Width = 10; thumb.HorizontalAlignment = kind == "start" ? HorizontalAlignment.Left : HorizontalAlignment.Right; border.SetValue(Border.BorderBrushProperty, element.Id == selected ? Theme.Token("OnAccent") : Theme.Ink); border.SetValue(Border.BorderThicknessProperty, new Thickness(kind == "start" ? 2 : 0, 0, kind == "end" ? 2 : 0, 0)); border.SetValue(Border.MarginProperty, new Thickness(3, 6, 3, 6)); }
                thumb.Template = new ControlTemplate(typeof(Thumb)) { VisualTree = border };
                System.Windows.Automation.AutomationProperties.SetName(thumb, element.Name + (kind == "start" ? " 显示起点" : kind == "end" ? " 显示终点" : " 移动显示时段"));
                var original = element.Time; double startX = 0; bool changed = false;
                thumb.DragStarted += (_, _) => { dragging = true; changed = false; original = elements.FirstOrDefault(x => x.Id == element.Id)?.Time ?? element.Time; startX = Mouse.GetPosition(canvas).X; row.Focus(); EditStarted?.Invoke(); Selected?.Invoke(element.Id); };
                thumb.DragDelta += (_, _) => {
                    var delta = (Mouse.GetPosition(canvas).X - startX) / TrackWidth * duration;
                    var time = kind == "start" ? original.ResizeStart(original.Start + delta) : kind == "end" ? original.ResizeEnd(original.End + delta, duration) : original.Move(delta, duration);
                    changed = time != original;
                    TimeChanging?.Invoke(element.Id, time);
                    var index = elements.ToList().FindIndex(x => x.Id == element.Id); if (index >= 0) { var copy = elements.ToArray(); copy[index] = copy[index] with { Time = time }; elements = copy; }
                    rows[element.Id] = (row, element with { Time = time }); RefreshPositions();
                };
                thumb.DragCompleted += (_, e) => { if (e.Canceled) TimeChanging?.Invoke(element.Id,original); dragging = false; EditCompleted?.Invoke(); Rebuild(); if (!e.Canceled && !changed) SeekRequested?.Invoke(original.Start); };
                row.Children.Add(thumb);
            }
            row.PreviewKeyDown += (_, e) => {
                if (e.Key is not (Key.Left or Key.Right)) return;
                dragging = true; EditStarted?.Invoke(); Selected?.Invoke(element.Id);
                var current = elements.First(x => x.Id == element.Id); TimeChanging?.Invoke(element.Id, current.Time.Move((e.Key == Key.Left ? -1 : 1) * (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 1 : .1), duration)); dragging = false; EditCompleted?.Invoke(); e.Handled = true;
            };
            Canvas.SetTop(row, lane * RowHeight + 3); canvas.Children.Add(row); rows[element.Id] = (row, element);
        }
        canvas.Height = Math.Max(RowHeight, lanes.Count * RowHeight + 6); cursor.Height = canvas.Height; cursor.Visibility = elements.Count == 0 ? Visibility.Hidden : Visibility.Visible; canvas.Children.Add(cursor); RefreshPositions();
        if (hadFocus && selected is Guid id && rows.TryGetValue(id,out var focused)) focused.row.Focus();
    }
    void RefreshPositions()
    {
        foreach (var (row, element) in rows.Values) { ((TranslateTransform)row.RenderTransform).X = X(element.Time.Start); row.Width = Math.Max(18, (element.Time.End - element.Time.Start) / Math.Max(.001, duration) * TrackWidth); }
        SetPosition(position);
    }
}
