namespace Liangtu;

// UI-thread-owned LRU. Mounted photos and realized basket rows stay hot even
// when they exceed the budget; only invisible historical bitmaps are evicted.
internal sealed class ThumbnailCache
{
    const long Budget = 16L * 1024 * 1024;
    const long PreviewBudget = 2L * 1024 * 1024;
    readonly LinkedList<Photo> recent = new();
    readonly Dictionary<Photo, (LinkedListNode<Photo> node, long bytes)> entries = new();
    readonly Dictionary<Photo, int> leases = new();
    readonly LinkedList<Photo> previews = new();
    readonly Dictionary<Photo, (LinkedListNode<Photo> node, long bytes)> previewEntries = new();
    readonly Func<Photo, bool> mounted;
    readonly BitmapSource placeholder;
    long bytes, previewBytes;
    public ThumbnailCache(Func<Photo, bool> mounted, BitmapSource placeholder) { this.mounted = mounted; this.placeholder = placeholder; }
    static long Weight(Photo photo) => (long)photo.Image.PixelWidth * photo.Image.PixelHeight * Math.Max(4, (photo.Image.Format.BitsPerPixel + 7) / 8);
    bool Protected(Photo photo) => mounted(photo) || leases.ContainsKey(photo);
    public void Touch(Photo photo)
    {
        Forget(photo);
        if (!photo.NeedsThumbnail) {
            var weight = Weight(photo);
            entries.Add(photo, (recent.AddLast(photo), weight)); bytes += weight;
        }
        Trim();
    }
    public IDisposable Retain(Photo photo)
    {
        leases.TryGetValue(photo, out var count); leases[photo] = count + 1;
        return new Lease(this, photo);
    }
    public void Forget(Photo photo)
    {
        if (entries.Remove(photo, out var entry)) { recent.Remove(entry.node); bytes -= entry.bytes; }
        if (previewEntries.Remove(photo, out var preview)) { previews.Remove(preview.node); previewBytes -= preview.bytes; }
    }
    public void Trim()
    {
        var node = recent.First;
        while (bytes > Budget && node != null) {
            var next = node.Next; var photo = node.Value;
            if (!Protected(photo)) {
                Forget(photo); photo.ReleaseThumbnail();
                var weight = Weight(photo); previewEntries.Add(photo, (previews.AddLast(photo), weight)); previewBytes += weight;
            }
            node = next;
        }
        node = previews.First;
        while ((previewBytes > PreviewBudget || previewEntries.Count > 128) && node != null) {
            var next = node.Next; var photo = node.Value;
            if (!Protected(photo)) { Forget(photo); photo.UseDeferredThumbnail(placeholder); }
            node = next;
        }
    }
    void Release(Photo photo)
    {
        if (leases[photo] == 1) leases.Remove(photo); else leases[photo]--;
        Trim();
    }
    sealed class Lease : IDisposable
    {
        ThumbnailCache? owner; readonly Photo photo;
        public Lease(ThumbnailCache owner, Photo photo) { this.owner = owner; this.photo = photo; }
        public void Dispose() { var cache = owner; owner = null; cache?.Release(photo); }
    }
}
