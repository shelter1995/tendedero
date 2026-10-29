using System.Globalization;
using System.Windows.Controls.Primitives;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Automation;
namespace Liangtu;

public sealed class VideoTimeline : Canvas
{
    readonly TimelineThumb left, right, cursor;
    IReadOnlyList<BitmapSource> frames = Array.Empty<BitmapSource>();
    VideoRange range = new(0);
    double position;
    DrawingGroup? background;
    readonly Brush ink = Theme.Ink, muted = Theme.Muted, surface = Theme.ControlSurface, shade = new SolidColorBrush(Color.FromArgb(175, 20, 22, 27));
    readonly Pen outline = new(Theme.Outline, 1), accent = new(Theme.Accent, 2);
    readonly Typeface typeface = new(Theme.Font, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    public bool Selection { get; }
    public VideoRange Range => range;
    public double Position => position;
    public event Action<double>? SeekRequested;
    public event Action<bool>? SelectionChanged;
    public event Action? ScrubStarted, ScrubCompleted;
    const double Inset = 18;
    double TrackWidth => Math.Max(1, ActualWidth - 2 * Inset);
    double X(double value) => Inset + (range.Duration > 0 ? value / range.Duration : 0) * TrackWidth;
    double Time(double x) => Math.Clamp((x - Inset) / TrackWidth, 0, 1) * range.Duration;
    public VideoTimeline(bool selection)
    {
        Selection = selection; Height = selection ? 108 : 32; Background = Brushes.Transparent; ClipToBounds = true;
        left = MakeThumb("裁剪起点", true, () => range.Start, value => SetStart(value));
        right = MakeThumb("裁剪终点", true, () => range.End, value => SetEnd(value));
        cursor = MakeThumb("播放位置", false, () => position, Seek);
        Children.Add(left); Children.Add(right); Children.Add(cursor);
        left.Visibility = right.Visibility = selection ? Visibility.Visible : Visibility.Collapsed;
        SizeChanged += (_, _) => { background = null; Refresh(); }; IsEnabled = false;
        MouseLeftButtonDown += (_, e) => { if (e.Handled || range.Duration <= 0) return; cursor.Focus(); Seek(Time(e.GetPosition(this).X)); e.Handled = true; };
    }
    TimelineThumb MakeThumb(string name, bool handle, Func<double> get, Action<double> set)
    {
        var thumb = new TimelineThumb(get, set, () => range.Duration) { Width = handle ? 16 : 18, Height = Selection ? 60 : 28, Cursor = Cursors.SizeWE, Focusable = true, Foreground = Theme.Ink };
        VideoChrome.Hint(thumb, name + " · ← / → 微调 · Shift 加速");
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "Grip";
        border.SetValue(Border.BackgroundProperty, handle ? Theme.Accent : Theme.Ink);
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(handle ? 4 : Selection ? 1 : 7));
        border.SetValue(Border.BorderBrushProperty, Theme.ControlSurface);
        border.SetValue(Border.BorderThicknessProperty, new Thickness(handle ? 1 : 0));
        if (!handle) { border.SetValue(FrameworkElement.WidthProperty, Selection ? 2.0 : 12.0); border.SetValue(FrameworkElement.HeightProperty, Selection ? 60.0 : 12.0); border.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center); border.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center); }
        else { var grip = new FrameworkElementFactory(typeof(TextBlock)); grip.SetValue(TextBlock.TextProperty, "Ⅱ"); grip.SetValue(TextBlock.FontSizeProperty, 12.0); grip.SetValue(TextBlock.ForegroundProperty, Theme.Token("OnAccent")); grip.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center); grip.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center); border.AppendChild(grip); }
        var template = new ControlTemplate(typeof(Thumb)) { VisualTree = border };
        var hover = new Trigger { Property = IsMouseOverProperty, Value = true }; hover.Setters.Add(new Setter(OpacityProperty, .78, "Grip")); template.Triggers.Add(hover);
        var focus = new Trigger { Property = IsKeyboardFocusedProperty, Value = true }; focus.Setters.Add(new Setter(Border.BorderBrushProperty, Theme.Ink, "Grip")); focus.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(2), "Grip")); template.Triggers.Add(focus);
        thumb.Template = template; thumb.RenderTransform = new TranslateTransform();
        double grabOffset = 0;
        thumb.DragStarted += (_, _) => { thumb.Focus(); grabOffset = Mouse.GetPosition(this).X - X(get()); ScrubStarted?.Invoke(); set(get()); };
        // Mouse coordinates stay relative to the fixed track, even before WPF
        // has arranged the thumb at its new position.
        thumb.DragDelta += (_, _) => set(Time(Mouse.GetPosition(this).X - grabOffset));
        thumb.DragCompleted += (_, _) => ScrubCompleted?.Invoke();
        thumb.PreviewKeyDown += (_, e) => {
            var step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 1 : .1;
            if (e.Key == Key.Left) set(get() - step); else if (e.Key == Key.Right) set(get() + step);
            else if (e.Key == Key.Home) set(0); else if (e.Key == Key.End) set(range.Duration); else return;
            e.Handled = true;
        };
        return thumb;
    }
    public void SetDuration(double duration) { range = new(duration); position = 0; background = null; IsEnabled = duration > 0; Refresh(); }
    public void SetFrames(IReadOnlyList<BitmapSource> images) { frames = images; background = null; InvalidateVisual(); }
    public void SetPosition(double value) { var next = Math.Clamp(double.IsFinite(value) ? value : 0, 0, range.Duration); if (Math.Abs(next - position) < .000001) return; position = next; Refresh(); }
    public void SetStart(double value) { range.SetStart(value); position = range.Start; Refresh(); SelectionChanged?.Invoke(true); }
    public void SetEnd(double value) { range.SetEnd(value); position = range.End; Refresh(); SelectionChanged?.Invoke(false); }
    public void ResetSelection() { range.Reset(); position = 0; Refresh(); SelectionChanged?.Invoke(true); }
    public void SetSelection(VideoSelection value)
    {
        var replacement = new VideoRange(range.Duration); replacement.SetEnd(value.End); replacement.SetStart(value.Start);
        range = replacement; position = range.Start; Refresh(); SelectionChanged?.Invoke(true);
    }
    void Seek(double value) { SetPosition(Selection ? range.ClampSelection(value) : value); SeekRequested?.Invoke(position); }
    void Refresh()
    {
        ((TranslateTransform)left.RenderTransform).X = X(range.Start) - left.Width; SetTop(left, 12);
        ((TranslateTransform)right.RenderTransform).X = X(range.End); SetTop(right, 12);
        ((TranslateTransform)cursor.RenderTransform).X = X(position) - cursor.Width / 2; SetTop(cursor, Selection ? 6 : 2);
        InvalidateVisual();
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth <= 2 * Inset) return;
        if (!Selection)
        {
            dc.DrawRoundedRectangle(outline.Brush, null, new Rect(Inset, 15, TrackWidth, 3), 1.5, 1.5);
            dc.DrawRoundedRectangle(accent.Brush, null, new Rect(Inset, 15, Math.Max(0, X(position) - Inset), 3), 1.5, 1.5); return;
        }
        if (background == null) { background = new DrawingGroup(); using (var cached = background.Open()) DrawBackground(cached); if (background.CanFreeze) background.Freeze(); }
        dc.DrawDrawing(background);
        dc.DrawRectangle(shade, null, new Rect(Inset, 16, Math.Max(0, X(range.Start) - Inset), 52));
        dc.DrawRectangle(shade, null, new Rect(X(range.End), 16, Math.Max(0, ActualWidth - Inset - X(range.End)), 52));
        dc.DrawRectangle(null, accent, new Rect(X(range.Start), 15, Math.Max(0, X(range.End) - X(range.Start)), 54));
    }
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi) { base.OnDpiChanged(oldDpi, newDpi); background = null; InvalidateVisual(); }
    void DrawBackground(DrawingContext dc)
    {
        var strip = new Rect(Inset, 16, TrackWidth, 52);
        dc.PushClip(new RectangleGeometry(strip, 4, 4));
        dc.DrawRectangle(surface, null, strip);
        if (frames.Count > 0)
        {
            var width = TrackWidth / frames.Count;
            for (var i = 0; i < frames.Count; i++)
            {
                var cell = new Rect(Inset + i * width, 16, width, 52); var frame = frames[i];
                var scale = Math.Max(width / frame.PixelWidth, 52.0 / frame.PixelHeight);
                dc.PushClip(new RectangleGeometry(cell)); dc.DrawImage(frame, new Rect(cell.X + (width - frame.PixelWidth * scale) / 2, cell.Y + (52 - frame.PixelHeight * scale) / 2, frame.PixelWidth * scale, frame.PixelHeight * scale)); dc.Pop();
            }
        }
        dc.Pop();
        dc.DrawRoundedRectangle(null, outline, strip, 4, 4);
        for (var i = 0; i <= 4; i++)
        {
            var x = Inset + TrackWidth * i / 4;
            dc.DrawLine(outline, new Point(x, 77), new Point(x, 82));
            var label = new FormattedText(VideoRange.Format(range.Duration * i / 4).Split('.')[0], CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 11, muted, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(label, new Point(Math.Clamp(x - label.Width / 2, 0, Math.Max(0, ActualWidth - label.Width)), 86));
        }
    }
}

internal sealed class TimelineThumb : Thumb
{
    internal readonly Func<double> Read, Limit;
    internal readonly Action<double> Write;
    public TimelineThumb(Func<double> read, Action<double> write, Func<double> limit) { Read = read; Write = write; Limit = limit; }
    protected override AutomationPeer OnCreateAutomationPeer() => new TimelineThumbPeer(this);
}
internal sealed class TimelineThumbPeer : FrameworkElementAutomationPeer, IRangeValueProvider
{
    TimelineThumb Thumb => (TimelineThumb)Owner;
    public TimelineThumbPeer(TimelineThumb owner) : base(owner) { }
    protected override string GetClassNameCore() => "VideoTimelineHandle";
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Slider;
    public override object? GetPattern(PatternInterface pattern) => pattern == PatternInterface.RangeValue ? this : base.GetPattern(pattern);
    public bool IsReadOnly => !Thumb.IsEnabled;
    public double LargeChange => 1;
    public double SmallChange => .1;
    public double Maximum => Thumb.Limit();
    public double Minimum => 0;
    public double Value => Thumb.Read();
    public void SetValue(double value) { if (!Thumb.IsEnabled) throw new ElementNotEnabledException(); Thumb.Write(value); }
}
