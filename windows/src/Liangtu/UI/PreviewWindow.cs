namespace Liangtu;
public sealed class PreviewWindow : Window
{
    readonly App app;
    Photo photo;
    readonly Grid body = new();
    readonly TextBlock label = VideoChrome.Label("", 13, false), clock = VideoChrome.Label("", 12, false), status = VideoChrome.Label(""), details = VideoChrome.Label("");
    readonly VideoTimeline timeline = new(false);
    readonly Button play, previous, next, edit;
    readonly StackPanel videoControls = new();
    VideoPlayback? player;
    bool closed;
    public PreviewWindow(App app, Photo photo)
    {
        this.app = app; this.photo = photo; VideoChrome.Window(this);
        previous = VideoChrome.Icon("上一项", "M15,5 L8,12 L15,19", () => Navigate(-1)); next = VideoChrome.Icon("下一项", "M9,5 L16,12 L9,19", () => Navigate(1));
        play = VideoChrome.Icon("播放 · 空格", VideoChrome.Play, () => player?.Toggle());
        edit = VideoChrome.Icon("视频裁剪", VideoChrome.Trim, () => { Close(); app.Edit(this.photo); });
        Content = BuildLayout(); timeline.SeekRequested += value => player?.Seek(value); timeline.ScrubStarted += () => player?.BeginScrub(); timeline.ScrubCompleted += () => player?.EndScrub(); KeyDown += OnKey;
        Closed += (_, _) => { closed = true; player?.Dispose(); }; Render();
    }
    UIElement BuildLayout()
    {
        var root = new DockPanel(); var top = new DockPanel { Margin = new Thickness(14, 8, 14, 8) };
        var navigation = new StackPanel { Orientation = Orientation.Horizontal }; navigation.Children.Add(previous); navigation.Children.Add(next); navigation.Children.Add(VideoChrome.Divider()); DockPanel.SetDock(navigation, Dock.Left); top.Children.Add(navigation);
        var actions = new StackPanel { Orientation = Orientation.Horizontal }; actions.Children.Add(edit); actions.Children.Add(VideoChrome.Icon("用默认应用打开", VideoChrome.Open, () => app.Open(photo))); DockPanel.SetDock(actions, Dock.Right); top.Children.Add(actions);
        label.Margin = new Thickness(12, 0, 12, 0); top.Children.Add(label); var header = VideoChrome.Bar(top); DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        var bottom = new StackPanel();
        var transport = new Grid { Margin = new Thickness(24, 12, 24, 0) }; transport.ColumnDefinitions.Add(new ColumnDefinition()); transport.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); transport.ColumnDefinitions.Add(new ColumnDefinition());
        clock.FontFamily = new FontFamily("Segoe UI"); transport.Children.Add(clock);
        var controls = new StackPanel { Orientation = Orientation.Horizontal }; controls.Children.Add(VideoChrome.Icon("后退 1 秒 · ←", VideoChrome.Back, () => player?.Seek((player?.Position ?? 0) - 1))); controls.Children.Add(play); controls.Children.Add(VideoChrome.Icon("前进 1 秒 · →", VideoChrome.Forward, () => player?.Seek((player?.Position ?? 0) + 1))); Grid.SetColumn(controls, 1); transport.Children.Add(controls);
        details.HorizontalAlignment = HorizontalAlignment.Right; Grid.SetColumn(details, 2); transport.Children.Add(details); videoControls.Children.Add(transport);
        timeline.Margin = new Thickness(24, 0, 24, 4); videoControls.Children.Add(timeline); bottom.Children.Add(videoControls);
        status.Margin = new Thickness(24, 8, 24, 12); bottom.Children.Add(status); var footer = VideoChrome.Bar(bottom, true); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer); root.Children.Add(body); return root;
    }
    void Navigate(int step)
    {
        var i = app.Store.Items.IndexOf(photo) + step; if (i < 0 || i >= app.Store.Items.Count) return; photo = app.Store.Items[i]; Render();
    }
    void Render()
    {
        player?.Dispose(); player = null; body.Children.Clear();
        Title = Path.GetFileName(photo.Path) + " — 预览"; label.Text = Path.GetFileName(photo.Path); label.ToolTip = photo.Path;
        var index = app.Store.Items.IndexOf(photo); previous.IsEnabled = index > 0; next.IsEnabled = index >= 0 && index < app.Store.Items.Count - 1;
        VideoChrome.Hint(edit, photo.Video ? "视频裁剪" : "图片标注"); edit.Content = VideoChrome.Glyph(photo.Video ? VideoChrome.Trim : VideoChrome.Annotate); timeline.SetDuration(0); clock.Text = "正在载入…"; details.Text = "";
        videoControls.Visibility = photo.Video ? Visibility.Visible : Visibility.Collapsed;
        if (photo.Video)
        {
            status.Text = "空格播放 / 暂停 · ← / → 定位 · Esc 关闭";
            var current = player = new VideoPlayback(photo.Path, true); body.Children.Add(current.Media);
            current.Opened += () => { timeline.SetDuration(current.Duration); details.Text = $"{current.Media.NaturalVideoWidth} × {current.Media.NaturalVideoHeight}"; };
            current.Changed += () => {
                if (closed || player != current) return; timeline.SetPosition(current.Position); clock.Text = VideoRange.Format(current.Position) + " / " + VideoRange.Format(current.Duration);
                var name = current.Playing ? "暂停 · 空格" : "播放 · 空格";
                if (!Equals(play.ToolTip, name)) { play.Content = VideoChrome.Glyph(current.Playing ? VideoChrome.Pause : VideoChrome.Play); VideoChrome.Hint(play, name); } play.IsEnabled = current.Ready;
            };
            current.Failed += message => { status.Text = message; timeline.IsEnabled = false; }; play.IsEnabled = false;
        }
        else
        {
            status.Text = "← / → 切换图片 · 空格 / Esc 关闭";
            try { body.Children.Add(new Image { Source = MediaTools.LoadImage(photo.Path), Stretch = Stretch.Uniform, Margin = new Thickness(24) }); } catch (Exception e) { status.Text = e.Message; }
        }
    }
    void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
        else if (e.Key == Key.Space) { if (photo.Video) player?.Toggle(); else Close(); }
        else if (e.Key == Key.Left) { if (photo.Video) player?.Seek((player?.Position ?? 0) - 1); else Navigate(-1); }
        else if (e.Key == Key.Right) { if (photo.Video) player?.Seek((player?.Position ?? 0) + 1); else Navigate(1); }
        else return; e.Handled = true;
    }
}
