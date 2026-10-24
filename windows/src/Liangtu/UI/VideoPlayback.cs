namespace Liangtu;

// Owns media state, the playback clock and selection limits for both windows.
internal sealed class VideoPlayback : IDisposable
{
    public MediaElement Media { get; } = new() { LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Close, Stretch = Stretch.Uniform, ScrubbingEnabled = true, Margin = new Thickness(24, 16, 24, 16) };
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    readonly DispatcherTimer seekTimer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(50) };
    readonly bool autoplay;
    bool disposed;
    bool scrubbing;
    double pausedPosition;
    double? pendingSeek;
    public bool Ready { get; private set; }
    public bool Playing { get; private set; }
    public double Duration { get; private set; }
    public double Start { get; set; }
    public double End { get; set; }
    // Paused UI follows the requested position, not an asynchronously decoded frame.
    public double Position => Ready ? Playing ? Media.Position.TotalSeconds : pausedPosition : 0;
    public event Action? Opened, Changed;
    public event Action<string>? Failed;
    public VideoPlayback(string path, bool autoplay)
    {
        this.autoplay = autoplay;
        Media.MediaOpened += OnOpened; Media.MediaEnded += OnEnded; Media.MediaFailed += OnFailed;
        Media.Loaded += OnLoaded; Media.MouseLeftButtonDown += OnClick;
        timer.Tick += OnTick; seekTimer.Tick += OnSeekTick; Media.Source = new Uri(path);
    }
    void OnLoaded(object sender, RoutedEventArgs e) { if (disposed) return; Media.Play(); if (!autoplay) Media.Pause(); }
    void OnOpened(object sender, RoutedEventArgs e)
    {
        if (disposed || !Media.NaturalDuration.HasTimeSpan) return;
        Duration = Media.NaturalDuration.TimeSpan.TotalSeconds; Start = 0; End = Duration; pausedPosition = 0; Ready = Duration > 0;
        Playing = autoplay && Ready; if (!Playing) Media.Pause();
        Opened?.Invoke(); if (Playing) timer.Start(); Changed?.Invoke();
    }
    void OnTick(object? sender, EventArgs e)
    {
        // Let the native end event finish full-length playback; pausing at the
        // exact duration can suppress MediaEnded and leave replay state stale.
        if (!Playing) return;
        if (End < Duration - .001 && Position >= End) { Seek(End); return; }
        Changed?.Invoke();
    }
    void OnEnded(object sender, RoutedEventArgs e) { if (Playing) Seek(Start); }
    void OnFailed(object? sender, ExceptionRoutedEventArgs e) { Ready = Playing = false; timer.Stop(); CancelSeek(); Failed?.Invoke("无法播放此视频：" + e.ErrorException.Message); Changed?.Invoke(); }
    void OnClick(object sender, MouseButtonEventArgs e) { Toggle(); e.Handled = true; }
    public void Toggle()
    {
        if (!Ready || disposed) return;
        if (Playing) Pause();
        else { EndScrub(); if (Position < Start || Position >= End - .03) { pausedPosition = Start; Media.Position = TimeSpan.FromSeconds(Start); } Playing = true; Media.Play(); timer.Start(); Changed?.Invoke(); }
    }
    void PauseCore() { if (!Playing) return; pausedPosition = Media.Position.TotalSeconds; Playing = false; timer.Stop(); Media.Pause(); }
    public void Pause() { if (!Playing) return; PauseCore(); Changed?.Invoke(); }
    public void BeginScrub() { if (!Ready) return; PauseCore(); scrubbing = true; }
    public void EndScrub() { scrubbing = false; FlushSeek(); }
    public void Seek(double position)
    {
        if (!Ready || disposed || !double.IsFinite(position)) return;
        PauseCore(); pausedPosition = Math.Clamp(position, 0, Duration);
        if (scrubbing) { pendingSeek = pausedPosition; if (!seekTimer.IsEnabled) seekTimer.Start(); }
        else { CancelSeek(); Media.Position = TimeSpan.FromSeconds(pausedPosition); }
        Changed?.Invoke();
    }
    void OnSeekTick(object? sender, EventArgs e) => FlushSeek();
    void FlushSeek()
    {
        seekTimer.Stop(); var target = pendingSeek; pendingSeek = null;
        if (target is double value && Ready && !disposed) Media.Position = TimeSpan.FromSeconds(value);
    }
    void CancelSeek() { seekTimer.Stop(); pendingSeek = null; }
    public void CloseMedia() { timer.Stop(); CancelSeek(); scrubbing = false; Playing = Ready = false; Media.Close(); Changed?.Invoke(); }
    public void Reopen(string path) { Ready = Playing = false; Media.Source = new Uri(path); Media.Play(); Media.Pause(); }
    public void Dispose()
    {
        if (disposed) return; disposed = true; timer.Stop(); timer.Tick -= OnTick; CancelSeek(); seekTimer.Tick -= OnSeekTick;
        Media.Loaded -= OnLoaded; Media.MediaOpened -= OnOpened; Media.MediaEnded -= OnEnded; Media.MediaFailed -= OnFailed; Media.MouseLeftButtonDown -= OnClick; Media.Close();
    }
}
