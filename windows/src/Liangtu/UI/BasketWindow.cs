using System.Windows.Data;
using System.Windows.Controls.Primitives;
namespace Liangtu;

public sealed class BasketWindow : Window
{
    readonly App app;
    readonly ListBox list = new() { SelectionMode = SelectionMode.Extended, BorderThickness = new Thickness(0) };
    readonly Image preview = new() { Stretch = Stretch.Uniform, Margin = new Thickness(20) };
    readonly TextBox search = new() { MinWidth = 160, Height = 32, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(8, 0, 8, 0) };
    readonly TextBlock status = Theme.Text("", 12, Theme.Muted), details = Theme.Text("", 12, Theme.Muted), empty = Theme.Text("", 14, Theme.Muted);
    readonly TextBlock headingTitle = Theme.Text("图片篮子", 24);
    readonly Button mount, takeDown, delete, pin, copy, save, open;
    readonly RadioButton all = new() { Content = "全部", GroupName = "BasketFilter", IsChecked = true }, pending = new() { Content = "待挂", GroupName = "BasketFilter" };
    bool refreshing, closed;
    int previewRequest;
    Photo? previewPhoto;
    BitmapSource? previewVersion;
    readonly Dictionary<ListBoxItem, (Photo photo, IDisposable lease)> thumbnailLeases = new();
    internal ListBox MediaList => list;
    internal TextBox SearchBox => search;
    internal RadioButton PendingFilter => pending;
    public BasketWindow(App app)
    {
        this.app = app; Title = "图片篮子 · 晾图"; Width = 1000; Height = 670; MinWidth = 640; MinHeight = 440;
        Theme.WindowStyle(this);
        var root = new DockPanel { Background = Theme.WindowSurface }; Content = root;
        var top = new DockPanel { Margin = new Thickness(24, 18, 24, 12) };
        var auto = new CheckBox { Content = "自动挂上新截图", IsChecked = app.Settings.AutoHang, VerticalAlignment = VerticalAlignment.Center };
        auto.Click += (_, _) => { app.Settings.AutoHang = auto.IsChecked == true; app.SaveSettings(); };
        DockPanel.SetDock(auto, Dock.Right); top.Children.Add(auto);
        var heading = new StackPanel(); heading.Children.Add(headingTitle); var subtitle=Theme.Text("取下的图片留在这里，随时挂回。", 12, Theme.Muted); heading.Children.Add(subtitle);
        var identity = new StackPanel { Orientation = Orientation.Horizontal };
        var headingArt=new BasketVisual { Width = 44, Height = 53, Margin = new Thickness(0, -3, 13, 0) }; identity.Children.Add(headingArt); identity.Children.Add(heading); top.Children.Add(identity);
        DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        var filters = new DockPanel { Margin = new Thickness(24, 0, 24, 14) };
        search.ToolTip = "按文件名搜索"; System.Windows.Automation.AutomationProperties.SetName(search, "搜索篮子文件名");
        var searchField = new StackPanel { Orientation = Orientation.Horizontal };
        var searchLabel = Theme.Text("搜索文件名", 12, Theme.Muted); searchLabel.VerticalAlignment = VerticalAlignment.Center; searchLabel.Margin = new Thickness(0, 0, 8, 0); searchField.Children.Add(searchLabel); searchField.Children.Add(search);
        DockPanel.SetDock(searchField, Dock.Right); filters.Children.Add(searchField);
        var tabs = new StackPanel { Orientation = Orientation.Horizontal }; tabs.Children.Add(all); tabs.Children.Add(pending); filters.Children.Add(tabs);
        DockPanel.SetDock(filters, Dock.Top); root.Children.Add(filters);
        var footer = new DockPanel { Margin = new Thickness(20, 10, 20, 14) };
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        delete = Theme.Button("移到回收站", DeleteSelected); actions.Children.Add(delete);
        takeDown = Theme.Button("取下所选", () => { foreach (var p in Selected()) app.Store.TakeDown(p); }); actions.Children.Add(takeDown);
        mount = Theme.Primary("挂上绳子", () => { foreach (var p in Selected()) app.ReturnToLine(p); }); actions.Children.Add(mount);
        DockPanel.SetDock(actions, Dock.Right); footer.Children.Add(actions); status.VerticalAlignment = VerticalAlignment.Center; footer.Children.Add(status);
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var split = new Grid(); split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        split.RowDefinitions.Add(new RowDefinition { Height=new GridLength(1,GridUnitType.Star) }); split.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
        list.Background = Theme.ControlSurface; list.Foreground = Theme.Ink;
        VirtualizingPanel.SetIsVirtualizing(list, true); VirtualizingPanel.SetVirtualizationMode(list, VirtualizationMode.Recycling); ScrollViewer.SetCanContentScroll(list, true);
        var rowTemplate=ItemTemplate(); var tileTemplate=TileTemplate(); list.ItemTemplate = rowTemplate; list.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
        var rowStyle = new Style(typeof(ListBoxItem)); rowStyle.Setters.Add(new Setter(HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch)); rowStyle.Setters.Add(new Setter(PaddingProperty, new Thickness(12, 8, 12, 8))); rowStyle.Setters.Add(new Setter(MarginProperty, new Thickness(4, 2, 4, 2))); list.ItemContainerStyle = rowStyle;
        rowStyle.Setters.Add(new Setter(Control.TemplateProperty, (ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ListBoxItem'><Border x:Name='row' Padding='{TemplateBinding Padding}' Background='Transparent' BorderBrush='Transparent' BorderThickness='1' CornerRadius='{DynamicResource ControlRadius}'><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='row' Property='Background' Value='{DynamicResource Hover}'/></Trigger><Trigger Property='IsSelected' Value='True'><Setter TargetName='row' Property='Background' Value='{DynamicResource Selection}'/><Setter TargetName='row' Property='BorderBrush' Value='{DynamicResource Accent}'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='row' Property='BorderBrush' Value='{DynamicResource Accent}'/></Trigger></ControlTemplate.Triggers></ControlTemplate>")));
        rowStyle.Setters.Add(new EventSetter(LoadedEvent, new RoutedEventHandler(ThumbnailRowLoaded)));
        rowStyle.Setters.Add(new EventSetter(UnloadedEvent, new RoutedEventHandler(ThumbnailRowUnloaded)));
        var tileStyle=new Style(typeof(ListBoxItem),rowStyle); tileStyle.Setters.Add(new Setter(PaddingProperty,new Thickness(5))); tileStyle.Setters.Add(new Setter(MarginProperty,new Thickness(5)));
        var left = new Grid(); left.Children.Add(list); empty.HorizontalAlignment = HorizontalAlignment.Center; empty.VerticalAlignment = VerticalAlignment.Center; empty.TextAlignment = TextAlignment.Center; empty.IsHitTestVisible = false; left.Children.Add(empty);
        var leftPanel = Theme.Panel(left); leftPanel.Margin = new Thickness(16, 0, 8, 0); leftPanel.SetResourceReference(Border.PaddingProperty, "PanelInset"); split.Children.Add(leftPanel);
        var right = new DockPanel(); var rightPanel = Theme.Panel(right); rightPanel.Margin = new Thickness(8, 0, 20, 0);
        details.Margin = new Thickness(18, 12, 18, 12); details.TextWrapping=TextWrapping.Wrap;
        var previewActions = new WrapPanel { Margin = new Thickness(12, 8, 12, 8), HorizontalAlignment = HorizontalAlignment.Center };
        copy = Theme.Button("复制", async () => { if (Current is Photo p) { copy!.IsEnabled = false; details.Text = "正在复制…"; try { await app.Store.Copy(p); details.Text = "已复制"; } catch { details.Text = "复制失败，请重试"; } finally { SelectionChanged(); } } });
        pin = Theme.Button("钉在桌面", () => { if (Current is Photo p) app.Pin(p); });
        save = Theme.Button("保存副本…", () => { if (Current is Photo p) app.SaveCopy(p, this); });
        open = Theme.Button("查看 / 编辑", () => { if (Current is Photo p) app.Edit(p); });
        foreach (var button in new[] { copy, pin, open, save }) previewActions.Children.Add(button);
        DockPanel.SetDock(previewActions, Dock.Bottom); right.Children.Add(previewActions);
        var previewContent=new Grid(); previewContent.ColumnDefinitions.Add(new ColumnDefinition()); previewContent.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(0) });
        previewContent.RowDefinitions.Add(new RowDefinition()); previewContent.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
        previewContent.Children.Add(preview); previewContent.Children.Add(details); Grid.SetRow(details,1); right.Children.Add(previewContent);
        Grid.SetColumn(rightPanel, 1); split.Children.Add(rightPanel); root.Children.Add(split);
        search.TextChanged += (_, _) => Refresh(); all.Checked += (_, _) => Refresh(); pending.Checked += (_, _) => Refresh();
        list.SelectionChanged += (_, _) => { if (!refreshing) SelectionChanged(); };
        list.MouseDoubleClick += (_, _) => { if (Current is Photo p) app.Preview(p); };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        bool? galleryMode=null;
        void ApplyThemeLayout()
        {
            var linen=Theme.Current.Id=="linen"; var colorPlay=Theme.Current.Id=="color-play"; var gallery=linen||colorPlay;
            if(galleryMode!=gallery) {
                var selection=Selected();
                list.ItemsPanel=new ItemsPanelTemplate(new FrameworkElementFactory(gallery?typeof(VirtualizingTilePanel):typeof(VirtualizingStackPanel)));
                list.ItemTemplate=gallery?tileTemplate:rowTemplate; list.ItemContainerStyle=gallery?tileStyle:rowStyle;
                foreach(var photo in selection) if(list.Items.Cast<Row>().FirstOrDefault(r=>r.Photo==photo) is Row row && !list.SelectedItems.Contains(row)) list.SelectedItems.Add(row);
                galleryMode=gallery;
            }
            list.Background=gallery?Brushes.Transparent:Theme.ControlSurface;
            subtitle.Visibility=gallery?Visibility.Collapsed:Visibility.Visible;
            top.Margin=gallery?new Thickness(20,14,20,8):new Thickness(24,18,24,12);
            headingTitle.FontSize=gallery?22:24;
            headingArt.Width=gallery?58:44; headingArt.Height=gallery?69:53;
            var layoutWidth=root.ActualWidth>0?root.ActualWidth:Width;
            search.Width=gallery?Math.Max(180,Math.Min(360,layoutWidth*.4)):160;
            search.Height=gallery?36:32; searchLabel.Text=gallery?"搜索图片":"搜索文件名";
            leftPanel.Padding=new Thickness(gallery?7:9);
            leftPanel.Margin=new Thickness(16,0,8,0);
            Grid.SetColumn(leftPanel,0); Grid.SetColumnSpan(leftPanel,1);
            Grid.SetRow(rightPanel,0); Grid.SetColumn(rightPanel,1); Grid.SetColumnSpan(rightPanel,1);
            rightPanel.Height=double.NaN;
            rightPanel.Margin=new Thickness(8,0,20,0);
            split.ColumnDefinitions[0].Width=new GridLength(gallery?1.5:1,GridUnitType.Star);
            split.ColumnDefinitions[1].Width=new GridLength(1,GridUnitType.Star);
            preview.Margin=new Thickness(gallery?12:20);
            previewContent.ColumnDefinitions[0].Width=new GridLength(1,GridUnitType.Star);
            previewContent.ColumnDefinitions[1].Width=new GridLength(0);
            previewContent.RowDefinitions[0].Height=new GridLength(1,GridUnitType.Star);
            previewContent.RowDefinitions[1].Height=GridLength.Auto;
            Grid.SetColumn(details,0); Grid.SetRow(details,1); details.VerticalAlignment=VerticalAlignment.Top;
            previewActions.HorizontalAlignment=gallery?HorizontalAlignment.Stretch:HorizontalAlignment.Center;
            var inspectorWidth=Math.Max(140,rightPanel.ActualWidth>0?rightPanel.ActualWidth-28:(layoutWidth-68)/(colorPlay?2.7:2)-28);
            foreach(var button in new[]{copy,pin,save,open}) {
                button.Width=gallery?Math.Max(80,(inspectorWidth-22)/2):double.NaN;
                button.MinHeight=gallery?35:32;
            }
            save.SetResourceReference(BackgroundProperty,gallery?"Accent":"ControlSurface");
            save.SetResourceReference(ForegroundProperty,gallery?"OnAccent":"Ink");
            save.SetResourceReference(BorderBrushProperty,gallery?"Accent":"Outline");
            if(colorPlay) open.Width=save.Width=Math.Max(120,inspectorWidth-10);
            previewActions.Margin=new Thickness(12,8,12,8);
            InvalidateMeasure();
        }
        Theme.Changed+=ApplyThemeLayout; root.SizeChanged+=(_,_)=>ApplyThemeLayout(); rightPanel.SizeChanged+=(_,_)=>ApplyThemeLayout();
        app.Store.Changed += StoreChanged; Closed += (_, _) => {
            closed = true; ++previewRequest; app.Store.Changed -= StoreChanged; Theme.Changed-=ApplyThemeLayout;
            foreach (var item in thumbnailLeases.Keys.ToArray()) ReleaseThumbnailRow(item);
            preview.Source = null; previewPhoto = null; previewVersion = null;
        };
        ApplyThemeLayout();
        Refresh();
    }
    DataTemplate TileTemplate()
    {
        var tile=new FrameworkElementFactory(typeof(StackPanel));
        var frame=new FrameworkElementFactory(typeof(Border)); frame.SetValue(Border.BackgroundProperty,Theme.CanvasSurface); frame.SetValue(Border.BorderBrushProperty,Theme.Outline); frame.SetValue(Border.BorderThicknessProperty,new Thickness(.7)); frame.SetResourceReference(Border.CornerRadiusProperty,"ToolRadius"); frame.SetValue(HeightProperty,96d); frame.SetValue(Border.PaddingProperty,new Thickness(3));
        var image=new FrameworkElementFactory(typeof(Image)); image.SetBinding(Image.SourceProperty,new Binding("Photo.Image")); image.SetValue(Image.StretchProperty,Stretch.Uniform); frame.AppendChild(image); tile.AppendChild(frame);
        var title=new FrameworkElementFactory(typeof(TextBlock)); title.SetBinding(TextBlock.TextProperty,new Binding("Name")); title.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis); title.SetValue(TextBlock.FontSizeProperty,11d); title.SetValue(MarginProperty,new Thickness(2,5,2,0)); tile.AppendChild(title);
        var meta=new FrameworkElementFactory(typeof(TextBlock)); meta.SetBinding(TextBlock.TextProperty,new Binding("State")); meta.SetValue(TextBlock.ForegroundProperty,Theme.Muted); meta.SetValue(TextBlock.FontSizeProperty,10d); meta.SetValue(MarginProperty,new Thickness(2,2,2,0)); tile.AppendChild(meta);
        return new DataTemplate { VisualTree=tile };
    }
    DataTemplate ItemTemplate()
    {
        var row = new FrameworkElementFactory(typeof(DockPanel));
        var thumb = new FrameworkElementFactory(typeof(Image)); thumb.SetValue(WidthProperty, 88d); thumb.SetValue(HeightProperty, 62d); thumb.SetValue(MarginProperty, new Thickness(0, 0, 14, 0)); thumb.SetBinding(Image.SourceProperty, new Binding("Photo.Image")); thumb.SetValue(Image.StretchProperty, Stretch.Uniform); row.AppendChild(thumb);
        var labels = new FrameworkElementFactory(typeof(StackPanel)); labels.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        var title = new FrameworkElementFactory(typeof(TextBlock)); title.SetBinding(TextBlock.TextProperty, new Binding("Name")); title.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis); title.SetValue(TextBlock.FontSizeProperty, 13d); labels.AppendChild(title);
        var meta = new FrameworkElementFactory(typeof(TextBlock)); meta.SetBinding(TextBlock.TextProperty, new Binding("State")); meta.SetValue(TextBlock.ForegroundProperty, Theme.Muted); meta.SetValue(MarginProperty, new Thickness(0, 5, 0, 0)); meta.SetValue(TextBlock.FontSizeProperty, 11d); labels.AppendChild(meta); row.AppendChild(labels);
        return new DataTemplate { VisualTree = row };
    }
    sealed record Row(Photo Photo, string Name, string State);
    void ThumbnailRowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ListBoxItem item || closed) return;
        item.DataContextChanged -= ThumbnailRowChanged; item.DataContextChanged += ThumbnailRowChanged;
        RetainThumbnailRow(item, item.Content as Row);
    }
    void ThumbnailRowChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is ListBoxItem item && item.IsLoaded && !closed) RetainThumbnailRow(item, e.NewValue as Row);
    }
    void ThumbnailRowUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is ListBoxItem item) ReleaseThumbnailRow(item);
    }
    void RetainThumbnailRow(ListBoxItem item, Row? row)
    {
        if (thumbnailLeases.TryGetValue(item, out var previous)) {
            if (row?.Photo == previous.photo) return;
            thumbnailLeases.Remove(item); previous.lease.Dispose();
        }
        if (row == null) return;
        thumbnailLeases[item] = (row.Photo, app.Store.RetainThumbnail(row.Photo));
        _ = app.Store.EnsureThumbnail(row.Photo, notify: app.Store.Items.Contains(row.Photo));
    }
    void ReleaseThumbnailRow(ListBoxItem item)
    {
        item.DataContextChanged -= ThumbnailRowChanged;
        if (thumbnailLeases.Remove(item, out var entry)) entry.lease.Dispose();
    }
    Photo? Current => (list.SelectedItem as Row)?.Photo;
    Photo[] Selected() => list.SelectedItems.Cast<Row>().Select(r => r.Photo).ToArray();
    void StoreChanged(Photo? _) => Refresh();
    internal void Refresh()
    {
        if (closed) return;
        var selected = Selected().Select(p => p.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        refreshing = true;
        var query = search.Text.Trim(); var mounted = app.Store.Items.ToHashSet();
        var rows = app.Store.Collection.AsEnumerable().Reverse().Where(p => (pending.IsChecked != true || !mounted.Contains(p)) && Path.GetFileName(p.Path).Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(p => new Row(p, Path.GetFileName(p.Path), (mounted.Contains(p) ? "已挂上" : "待挂") + (p.Video ? " · 视频" : " · 图片"))).ToArray();
        // Thumbnail notifications already flow through Photo.Image binding.
        // Keep the same rows/scroll position when collection membership is stable.
        if(list.ItemsSource is not Row[] previous || !previous.SequenceEqual(rows)) {
            list.ItemsSource = rows;
            foreach (var row in rows.Where(r => selected.Contains(r.Photo.Path))) list.SelectedItems.Add(row);
        }
        empty.Text = app.Store.Collection.Count == 0 ? "篮子还是空的\n截一张图，或开启剪贴板图片接收。" : "这里没有符合条件的图片"; empty.Visibility = rows.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        refreshing = false; SelectionChanged();
    }
    void SelectionChanged()
    {
        var selected = Selected(); status.Text = $"{list.Items.Count} 项 · 已选 {selected.Length} 项"; headingTitle.Text=$"图片篮子 ({app.Store.Collection.Count})";
        mount.IsEnabled = selected.Any(p => !app.Store.Items.Contains(p)); takeDown.IsEnabled = selected.Any(app.Store.Items.Contains); delete.IsEnabled = selected.Length > 0;
        copy.IsEnabled = save.IsEnabled = open.IsEnabled = selected.Length == 1; pin.IsEnabled = selected.Length == 1 && !selected[0].Video && !app.Settings.GameDoNotDisturb;
        var photo = selected.Length == 1 ? Current : null;
        if (photo == previewPhoto && photo?.Image == previewVersion) return;
        previewPhoto = photo; previewVersion = photo?.Image; _ = LoadPreview(photo, ++previewRequest);
    }
    async Task LoadPreview(Photo? photo, int request)
    {
        preview.Source = photo?.Image;
        details.Text = photo == null ? "选择一项查看大图；按 Ctrl / Shift 多选。" : Path.GetFileName(photo.Path) + (photo.Video ? "\n视频 · 点击查看 / 编辑可裁剪" : "\n正在载入预览…");
        if (photo == null || photo.Video) return;
        try {
            var bitmap = await Task.Run(() => MediaTools.LoadImage(photo.Path));
            if (closed || request != previewRequest) return;
            preview.Source = bitmap; details.Text = $"{Path.GetFileName(photo.Path)}\n{bitmap.PixelWidth} × {bitmap.PixelHeight} px";
        } catch (Exception e) { if (!closed && request == previewRequest) details.Text = "无法预览：" + e.Message; }
    }
    void DeleteSelected()
    {
        var selected = Selected(); if (selected.Length == 0) return;
        app.DeletePhotos(selected, this);
    }
}
