namespace Liangtu;
public sealed class DragHarness : Window
{
    public DragHarness(App app)
    {
        Title = "晾图 · 跨应用拖放测试";
        Width = 460;
        Height = 320;
        Left = 120;
        Top = 300;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Background = Theme.Surface;
        Foreground = Theme.Ink;
        ShowInTaskbar = true;
        var canvas = new Canvas();
        Content = canvas;
        var text = new TextBlock { Text = "将下方测试卡片拖进其他应用或文件夹", FontSize = 16, Foreground = Theme.Ink };
        Canvas.SetLeft(text, 24);
        Canvas.SetTop(text, 24);
        canvas.Children.Add(text);
        var card = new Card(app.Store.Items.First(), app);
        Canvas.SetLeft(card, 95);
        Canvas.SetTop(card, 80);
        canvas.Children.Add(card);
        var status = new TextBlock { Text = "仅使用工作区生成的测试图片", Foreground = Theme.Ink };
        Canvas.SetLeft(status, 24);
        Canvas.SetTop(status, 230);
        canvas.Children.Add(status);
        app.Store.Changed += _ => status.Text = "晾衣绳剩余 " + app.Store.Items.Count + " 张";
    }
}
