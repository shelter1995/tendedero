using System.Windows.Controls.Primitives;
namespace Liangtu;

// Pixel-scrolling thumbnail grid. Only the viewport plus one row on either
// side owns containers, so a large basket never realizes its entire history.
internal sealed class VirtualizingTilePanel : VirtualizingPanel, IScrollInfo
{
    const double MinimumTileWidth=154, TileHeight=158;
    Size extent, viewport;
    double offset, cellWidth=MinimumTileWidth;
    int columns=1;
    internal int RealizedCount=>InternalChildren.Count;
    public bool CanHorizontallyScroll { get; set; }
    public bool CanVerticallyScroll { get; set; }=true;
    public double ExtentWidth=>extent.Width;
    public double ExtentHeight=>extent.Height;
    public double ViewportWidth=>viewport.Width;
    public double ViewportHeight=>viewport.Height;
    public double HorizontalOffset=>0;
    public double VerticalOffset=>offset;
    public ScrollViewer? ScrollOwner { get; set; }
    protected override Size MeasureOverride(Size availableSize)
    {
        var owner=ItemsControl.GetItemsOwner(this); if(owner==null) return new Size();
        // Touch InternalChildren before asking the generator to establish the
        // visual/container mapping on its first measure.
        _=InternalChildren.Count;
        var width=double.IsFinite(availableSize.Width)?Math.Max(1,availableSize.Width):MinimumTileWidth*3;
        var height=double.IsFinite(availableSize.Height)?Math.Max(0,availableSize.Height):TileHeight*3;
        columns=Math.Max(1,(int)(width/MinimumTileWidth)); cellWidth=width/columns;
        viewport=new Size(width,height); extent=new Size(width,Math.Ceiling(owner.Items.Count/(double)columns)*TileHeight);
        offset=Math.Clamp(offset,0,Math.Max(0,extent.Height-height)); ScrollOwner?.InvalidateScrollInfo();
        var first=Math.Max(0,(int)(offset/TileHeight)-1)*columns;
        var last=Math.Min(owner.Items.Count-1,((int)Math.Ceiling((offset+height)/TileHeight)+1)*columns-1);
        var generator=ItemContainerGenerator;
        for(int i=InternalChildren.Count-1;i>=0;i--) {
            var position=new GeneratorPosition(i,0); var index=generator.IndexFromGeneratorPosition(position);
            if(index<first || index>last) { generator.Remove(position,1); RemoveInternalChildRange(i,1); }
        }
        if(last>=first) {
            var position=generator.GeneratorPositionFromIndex(first);
            var childIndex=position.Offset==0?position.Index:position.Index+1;
            using(generator.StartAt(position,GeneratorDirection.Forward,true)) {
                for(int index=first;index<=last;index++,childIndex++) {
                    var child=(UIElement)generator.GenerateNext(out var generated);
                    if(generated) {
                        if(childIndex>=InternalChildren.Count) AddInternalChild(child); else InsertInternalChild(childIndex,child);
                        generator.PrepareItemContainer(child);
                    }
                    child.Measure(new Size(cellWidth,TileHeight));
                }
            }
        }
        return new Size(width,height);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        for(int i=0;i<InternalChildren.Count;i++) {
            var index=ItemContainerGenerator.IndexFromGeneratorPosition(new GeneratorPosition(i,0));
            if(index>=0) InternalChildren[i].Arrange(new Rect((index%columns)*cellWidth,(index/columns)*TileHeight-offset,cellWidth,TileHeight));
        }
        return finalSize;
    }
    protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
    {
        base.OnItemsChanged(sender,args);
        if(args.Action==System.Collections.Specialized.NotifyCollectionChangedAction.Reset) {
            RemoveInternalChildRange(0,InternalChildren.Count); offset=0;
        } else if(args.ItemUICount>0 && args.Action is System.Collections.Specialized.NotifyCollectionChangedAction.Remove or System.Collections.Specialized.NotifyCollectionChangedAction.Replace or System.Collections.Specialized.NotifyCollectionChangedAction.Move) {
            var start=args.Action==System.Collections.Specialized.NotifyCollectionChangedAction.Move?args.OldPosition.Index:args.Position.Index;
            if(start>=0 && start<InternalChildren.Count) RemoveInternalChildRange(start,Math.Min(args.ItemUICount,InternalChildren.Count-start));
        }
        InvalidateMeasure();
    }
    protected override void BringIndexIntoView(int index) { SetVerticalOffset((index/columns)*TileHeight); UpdateLayout(); }
    public Rect MakeVisible(Visual visual, Rect rectangle)
    {
        if(visual==this || !IsAncestorOf(visual)) return Rect.Empty;
        var bounds=visual.TransformToAncestor(this).TransformBounds(rectangle);
        if(bounds.Top<0) SetVerticalOffset(offset+bounds.Top); else if(bounds.Bottom>viewport.Height) SetVerticalOffset(offset+bounds.Bottom-viewport.Height);
        return bounds;
    }
    public void SetVerticalOffset(double value) { var next=Math.Clamp(double.IsFinite(value)?value:0,0,Math.Max(0,extent.Height-viewport.Height)); if(Math.Abs(offset-next)<.1) return; offset=next; InvalidateMeasure(); ScrollOwner?.InvalidateScrollInfo(); }
    public void SetHorizontalOffset(double value) { }
    public void LineUp()=>SetVerticalOffset(offset-32);
    public void LineDown()=>SetVerticalOffset(offset+32);
    public void MouseWheelUp()=>SetVerticalOffset(offset-96);
    public void MouseWheelDown()=>SetVerticalOffset(offset+96);
    public void PageUp()=>SetVerticalOffset(offset-viewport.Height);
    public void PageDown()=>SetVerticalOffset(offset+viewport.Height);
    public void LineLeft() { } public void LineRight() { }
    public void MouseWheelLeft() { } public void MouseWheelRight() { }
    public void PageLeft() { } public void PageRight() { }
}
