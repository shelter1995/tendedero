using System.Windows.Controls.Primitives;
namespace Liangtu;

internal sealed class ThemeWindow : Window
{
    readonly App app;
    readonly Dictionary<string, Border> choices = new();
    readonly TextBlock selected;
    readonly CheckBox follow;
    internal readonly List<Button> ThemeButtons = new();
    internal ScrollViewer ThemeScroll { get; }
    public ThemeWindow(App app)
    {
        this.app = app; Title = "外观主题 · 晾图"; Width = 980; Height = 740; MinWidth = 760; MinHeight = 470; Theme.WindowStyle(this);
        var root = new DockPanel { Margin = new Thickness(26) }; Content = root;
        var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 22) }; heading.Children.Add(Theme.Text("选择一套完整的外观", 25));
        heading.Children.Add(Theme.Text("绳子、夹子、篮子、窗口图标和托盘图标一起变化。", 13, Theme.Muted)); DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        var footer = new StackPanel { Margin = new Thickness(0, 20, 0, 0) };
        follow = new CheckBox { Content = "跟随 Windows 深浅色，自动使用晴空或夜航", Margin = new Thickness(0, 0, 0, 10), IsChecked = app.Settings.ThemeId == "system", Foreground = Theme.Ink };
        follow.Click += (_, _) => { app.SetTheme(follow.IsChecked == true ? "system" : Theme.Current.Id); RefreshSelection(); };
        selected = Theme.Text("", 12, Theme.Muted); footer.Children.Add(follow); footer.Children.Add(selected);
        var export=Theme.Button("导出当前主题图标…",()=>{
            var dialog=new Microsoft.Win32.SaveFileDialog { Title="导出主题图标",FileName="晾图-"+Theme.Current.Name+".ico",DefaultExt=".ico",Filter="Windows 图标|*.ico",AddExtension=true };
            if(dialog.ShowDialog(this)!=true)return;
            try{File.WriteAllBytes(dialog.FileName,ThemeIcons.AppIco(Theme.Current));}catch(Exception error){MessageBox.Show(this,"无法导出图标："+error.Message,"晾图");}
        });export.HorizontalAlignment=HorizontalAlignment.Left;export.Margin=new Thickness(0,8,0,0);export.ToolTip="窗口和托盘会自动切换；导出的图标可用于桌面快捷方式。";footer.Children.Add(export);
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var grid = new UniformGrid { Columns = 2 };
        KeyboardNavigation.SetDirectionalNavigation(grid, KeyboardNavigationMode.Contained);
        foreach (var profile in ThemeProfiles.All) {
            var stack = new StackPanel(); var preview = new Viewbox { Child = new ThemeScene(profile), Height = 150, Stretch = Stretch.Uniform }; stack.Children.Add(preview);
            var label = new StackPanel { Orientation=Orientation.Horizontal,Margin=new Thickness(0,10,0,5) };
            label.Children.Add(new Image { Source=ThemeIcons.AppImage(profile),Width=32,Height=32,Margin=new Thickness(0,0,10,0) });
            var title=Theme.Text(profile.Name,18);title.VerticalAlignment=VerticalAlignment.Center;label.Children.Add(title);stack.Children.Add(label);
            stack.Children.Add(Theme.Text(profile.Description, 12, Theme.Muted));
            var palette = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            foreach (var key in new[] { "WindowSurface", "ControlSurface", "Accent", "PegFace", "BasketFill" }) palette.Children.Add(new Border { Width = 22, Height = 6, Background = new SolidColorBrush(profile.Color(key)), Margin = new Thickness(0, 0, 4, 0), CornerRadius = new CornerRadius(2) });
            stack.Children.Add(palette);
            var face = new Border { Child = stack, Background = Theme.ControlSurface, Padding = new Thickness(14), CornerRadius = new CornerRadius(profile.ControlRadius), BorderThickness = new Thickness(1), BorderBrush = Theme.Outline, Margin = new Thickness(5) };
            var button = new Button { Content = face, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0), Cursor = Cursors.Hand, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            button.Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><ContentPresenter/></ControlTemplate>");
            System.Windows.Automation.AutomationProperties.SetName(button, "使用" + profile.Name + "主题"); button.Click += (_, _) => { app.SetTheme(profile.Id); RefreshSelection(); };
            button.GotKeyboardFocus += (_, _) => { face.BorderBrush = Theme.Accent; button.BringIntoView(); }; button.LostKeyboardFocus += (_, _) => RefreshSelection();
            choices[profile.Id] = face; grid.Children.Add(button);
            ThemeButtons.Add(button);
        }
        ThemeScroll = new ScrollViewer { Content = grid, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        root.Children.Add(ThemeScroll);
        grid.PreviewKeyDown += (_, e) => {
            var index = ThemeButtons.FindIndex(b=>b.IsKeyboardFocusWithin); if(index<0) return;
            var next = e.Key switch { Key.Left=>index-1, Key.Right=>index+1, Key.Up=>index-2, Key.Down=>index+2, Key.Home=>0, Key.End=>ThemeButtons.Count-1, _=>-1 };
            if(next<0 && e.Key is not (Key.Left or Key.Up)) return;
            next=Math.Clamp(next,0,ThemeButtons.Count-1); ThemeButtons[next].Focus(); ThemeButtons[next].BringIntoView(); e.Handled=true;
        };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); }; RefreshSelection();
    }
    void RefreshSelection()
    {
        follow.IsChecked = app.Settings.ThemeId == "system";
        foreach (var (id, face) in choices) { face.BorderBrush = id == Theme.Current.Id ? Theme.Accent : Theme.Outline; face.BorderThickness = new Thickness(id == Theme.Current.Id ? 2 : 1); }
        selected.Text = "当前：" + Theme.Current.Name + "。切换主题不会清除正在编辑的标注。";
    }
}

internal sealed class ThemeScene : Canvas
{
    readonly ThemeProfile profile;
    public ThemeScene(ThemeProfile profile)
    {
        this.profile = profile; Width = 310; Height = 224; ClipToBounds = true;
        Background = new SolidColorBrush(profile.Color("WindowSurface"));
        for(int i=0;i<2;i++) {
            var photo = new ThemeFramePreview(profile) { Variant=i, Width=82, Height=66, RenderTransform=new RotateTransform(i==0?-3:3,41,0) };
            SetLeft(photo,15+i*95); SetTop(photo,32); Children.Add(photo);
            var peg = new ClothesPegVisual { Palette=profile, Variant=i, Width=19, Height=30 };
            SetLeft(peg,48+i*95); SetTop(peg,12); Children.Add(peg);
        }
        var basket=new BasketVisual { Palette=profile, Width=profile.BasketArtWidth, Height=profile.BasketArtHeight };
        SetLeft(basket,306-basket.Width); SetTop(basket,12); Children.Add(basket);
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc); Brush B(string k)=>new SolidColorBrush(profile.Color(k));
        ThemeArtwork.Rope(dc,Geometry.Parse("M0,13 Q155,32 310,13"),profile);
        dc.PushTransform(new TranslateTransform(0,26));
        var box=new Rect(12,114,286,77); var r=profile.ControlRadius;
        dc.DrawRoundedRectangle(B("ControlSurface"),new Pen(B("Outline"),1),box,r,r);
        if(profile.Id=="color-play") {
            dc.DrawRoundedRectangle(B("ControlSurface"),new Pen(B("Outline"),.7),new Rect(18,121,27,62),7,7);
            for(int i=0;i<4;i++) dc.DrawRoundedRectangle(i==1?B("Selection"):null,new Pen(B("Muted"),.65),new Rect(26,126+i*13,10,9),2,2);
            dc.DrawRoundedRectangle(B("CanvasSurface"),null,new Rect(53,123,235,57),8,8);
        } else {
            for(int i=0;i<7;i++) dc.DrawRoundedRectangle(i==1?B("Selection"):null,new Pen(B(i==1?"Accent":"Muted"),.85),new Rect(24+i*22,124,12,12),profile.ToolRadius/2,profile.ToolRadius/2);
            if(profile.Id=="darkroom") dc.DrawLine(new Pen(B("Accent"),2),new(45,140),new(59,140));
            if(profile.Id=="linen") for(int i=0;i<7;i++) dc.DrawLine(new Pen(B("Muted"),.6),new(25+i*22,139),new(34+i*22,139));
            dc.DrawRoundedRectangle(B("CanvasSurface"),null,new Rect(23,146,264,34),profile.ToolRadius,profile.ToolRadius);
        }
        if(profile.Id=="linen") {
            dc.PushOpacity(.3); for(int x=24;x<286;x+=5) dc.DrawLine(new Pen(B("Outline"),.4),new(x,146),new(x,180)); dc.Pop();
        }
        var selection=new Rect(105,153,74,21);
        dc.DrawRectangle(null,new Pen(B("Accent"),1){DashStyle=new DashStyle(new[]{3d,2d},0)},selection);
        foreach(var pt in new[]{selection.TopLeft,selection.TopRight,selection.BottomLeft,selection.BottomRight}) dc.DrawRectangle(B("ControlSurface"),new Pen(B("Accent"),1),new Rect(pt.X-2,pt.Y-2,4,4));
        dc.Pop();
    }
}

internal sealed class ThemeFramePreview : FrameworkElement
{
    readonly ThemeProfile profile;
    public int Variant { get; init; }
    public ThemeFramePreview(ThemeProfile profile) => this.profile=profile;
    protected override void OnRender(DrawingContext dc)
    {
        Brush B(string k)=>new SolidColorBrush(profile.Color(k));
        ThemeArtwork.Frame(dc,profile,new Rect(RenderSize),profile.FrameRadius,Variant);
        var mat=profile.PhotoMat; var inset=profile.Id=="color-play"?1:.65; var photo=new Rect(mat.Left*inset,mat.Top*inset,Math.Max(1,ActualWidth-(mat.Left+mat.Right)*inset),Math.Max(1,ActualHeight-(mat.Top+mat.Bottom)*inset));
        var imageRadius=profile.Id=="color-play"?profile.PhotoImageRadius:0;
        dc.DrawRoundedRectangle(B("CanvasSurface"),null,photo,imageRadius,imageRadius);
        dc.PushClip(new RectangleGeometry(photo,imageRadius,imageRadius));
        var landscape=Geometry.Parse("M0,60 L25,20 L44,43 L58,30 L85,65 Z");
        dc.PushOpacity(.6); dc.DrawGeometry(B("Accent"),null,landscape); dc.Pop();
        dc.DrawEllipse(B("BasketStrap"),null,new Point(62,17),5,5); dc.Pop();
    }
}

