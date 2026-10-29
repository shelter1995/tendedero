namespace Liangtu;

public sealed partial class App
{
    BasketWindow? basket;
    readonly Dictionary<string, PinnedImageWindow> pins = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<Photo> manualMounts = new();
    internal int PinCount => pins.Count;
    public void ShowBasket()
    {
        if (WorkBlocked) return;
        if (basket != null) { basket.Show(); basket.WindowState = WindowState.Normal; basket.Activate(); return; }
        Line.Retract();
        basket = new BasketWindow(this);
        basket.Closed += (_, _) => basket = null;
        WindowPresenter.Show(basket, Line.Screen);
    }
    public void Pin(Photo photo)
    {
        if (WorkBlocked || photo.Video || Settings.GameDoNotDisturb || !File.Exists(photo.Path)) return;
        if (pins.TryGetValue(photo.Path, out var existing)) { existing.Show(); existing.Activate(); return; }
        try
        {
            var window = new PinnedImageWindow(this, photo);
            pins.Add(photo.Path, window);
            window.Closed += (_, _) => { pins.Remove(photo.Path); if (window.ReturnOnClose && !exiting && Store.Collection.Contains(photo) && File.Exists(photo.Path)) ReturnToLine(photo, window.LastPictureBounds); };
            window.ShowOn(Line.Screen,Line.CanPinTransfer(photo));
            window.UpdateLayout();
            if (window.PictureBounds() is not Rect destination || !Line.PreparePinTransfer(photo, destination, window.FinishTransfer,window.CaptureAppearance(false),window.PresentTransferDestination,()=>{var look=window.CaptureAppearance(false);return(look,window.PictureBounds());})) window.FinishTransfer();
            Store.TakeDown(photo);
        }
        catch (Exception e) { ReportBackgroundError("无法打开贴图：" + e.Message); }
    }
    public void ReturnToLine(Photo photo, Rect? from = null)
    {
        PinnedImageWindow? heldSource=null;
        PinAppearance? heldAppearance=null;
        if (pins.TryGetValue(photo.Path, out var window)) {
            if(window.Returning)return;
            from ??= window.PictureBounds();
            if(CanDeferPinReturn(photo)&&from is Rect&&!Store.Items.Contains(photo)) {
                heldAppearance=window.CaptureAppearance();
                window.BeginReturn();heldSource=window;
            } else window.CloseWithoutReturn();
        }
        if (!Store.Collection.Contains(photo) || !File.Exists(photo.Path) || WorkBlocked) return;
        if (!Settings.GameDoNotDisturb && !TestMode) {
            if (from is Rect source) Line.MoveTo(System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)(source.X + source.Width / 2), (int)(source.Y + source.Height / 2))), manual: true);
            Line.Reveal(true, 1.8);
            from ??= Line.BasketScreenBounds();
        }
        // Moving between monitors cancels flights on the old display. Register
        // this new handoff only after that cancellation boundary has passed.
        if(heldSource!=null) Line.HoldReturnSource(photo,heldSource.CloseWithoutReturn);
        var addedIntent=!TestMode&&!Settings.GameDoNotDisturb&&manualMounts.Add(photo);
        photo.ReturnAppearance=heldAppearance;
        try { Store.Mount(photo, Settings.GameDoNotDisturb ? null : from); }
        finally { photo.ReturnAppearance=null;if(addedIntent)manualMounts.Remove(photo); }
        if (!Settings.GameDoNotDisturb && !TestMode) Line.Reveal(true, 1.8);
    }
    internal bool CanDeferPinReturn(Photo photo) => !WorkBlocked&&!TestMode&&!Settings.GameDoNotDisturb&&Store.Collection.Contains(photo)&&File.Exists(photo.Path);
    public async void PutPinInBasket(Photo photo)
    {
        if (!pins.TryGetValue(photo.Path, out var window)) return;
        if (TestMode || Settings.GameDoNotDisturb || exiting) { window.CloseWithoutReturn(); return; }
        try {
            if (window.PictureBounds() is Rect source) {
                Line.MoveTo(System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)(source.X + source.Width / 2), (int)(source.Y + source.Height / 2))), manual: true);
                var waitForReveal = !Line.Revealed; Line.Reveal(true, 1.8);
                if (waitForReveal && SystemParameters.ClientAreaAnimation) await Task.Delay(450);
                if (exiting || !pins.TryGetValue(photo.Path, out var current) || current != window) return;
                if (!Settings.GameDoNotDisturb && window.PictureBounds() is Rect actualSource) Line.ReceiveFromScreen(photo, actualSource);
            }
            window.CloseWithoutReturn();
        } catch (InvalidOperationException e) { Settings.Log("Pin basket animation " + e.Message); }
    }
    public bool DeletePhoto(Photo photo, Window? owner = null) => DeletePhotos(new[] { photo }, owner) == 1;
    public int DeletePhotos(IEnumerable<Photo> photos, Window? owner = null)
    {
        if (WorkBlocked) return 0;
        var selected = photos.Distinct().Where(p => Store.Collection.Contains(p) || Store.Items.Contains(p)).ToArray();
        var anchor = owner ?? Line;
        var screen = anchor is LineWindow shelf ? shelf.Screen : anchor?.IsVisible == true
            ? System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(anchor).Handle) : Line.Screen;
        return DeleteOperation.Execute(Settings, selected,
            text => SilentConfirmation.Decide(text, anchor, screen),
            photo => Store.Trash(photo, emitCue: false), () => SaveSettings(), () => PlaySound(OperationCue.TakeDown));
    }
    public void SaveCopy(Photo photo, Window? owner = null)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Title = "保存副本", FileName = Path.GetFileName(photo.Path), DefaultExt = Path.GetExtension(photo.Path), Filter = "原格式文件|*" + Path.GetExtension(photo.Path), AddExtension = true };
        if ((owner != null ? dialog.ShowDialog(owner) : dialog.ShowDialog()) != true) return;
        try
        {
            if (string.Equals(Path.GetFullPath(dialog.FileName), Path.GetFullPath(photo.Path), StringComparison.OrdinalIgnoreCase)) throw new IOException("请选择与原文件不同的位置或名称。");
            File.Copy(photo.Path, dialog.FileName, true);
        }
        catch (Exception e) { MessageBox.Show("保存副本失败：" + e.Message, "晾图"); }
    }
    void UpdatePinVisibility()
    {
        foreach (var window in pins.Values.ToArray()) { if (Settings.GameDoNotDisturb) window.Hide(); else window.Show(); }
    }
}
