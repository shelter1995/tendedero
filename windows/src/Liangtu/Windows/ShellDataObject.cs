using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
namespace Liangtu;

[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
public sealed class ShellDataObject : System.Runtime.InteropServices.ComTypes.IDataObject
{
    readonly System.Runtime.InteropServices.ComTypes.IDataObject shell, payload;
    [DllImport("urlmon.dll")] static extern int CopyStgMedium(ref STGMEDIUM source, out STGMEDIUM destination);
    [DllImport("ole32.dll")] static extern void ReleaseStgMedium(ref STGMEDIUM medium);
    [DllImport("shell32.dll", PreserveSig = true)]
    static extern int SHCreateDataObject(IntPtr folder, uint count, IntPtr items, [MarshalAs(UnmanagedType.Interface)] System.Runtime.InteropServices.ComTypes.IDataObject? inner, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out System.Runtime.InteropServices.ComTypes.IDataObject result);
    public ShellDataObject(DataObject data)
    {
        payload = (System.Runtime.InteropServices.ComTypes.IDataObject)data;
        var iid = new Guid("0000010e-0000-0000-C000-000000000046");
        // A non-null inner object makes Shell delegate unknown SetData calls to WPF,
        // which returns E_NOTIMPL. Keep private storage separate, as in Microsoft's sample.
        Marshal.ThrowExceptionForHR(SHCreateDataObject(IntPtr.Zero, 0, IntPtr.Zero, null, ref iid, out shell));
    }
    System.Runtime.InteropServices.ComTypes.IDataObject Source(ref FORMATETC f) => shell.QueryGetData(ref f) == 0 ? shell : payload;
    public void GetData(ref FORMATETC f, out STGMEDIUM m) => Source(ref f).GetData(ref f, out m);
    public void GetDataHere(ref FORMATETC f, ref STGMEDIUM m) => Source(ref f).GetDataHere(ref f, ref m);
    public int QueryGetData(ref FORMATETC f) => Source(ref f).QueryGetData(ref f);
    public int GetCanonicalFormatEtc(ref FORMATETC f, out FORMATETC output) => Source(ref f).GetCanonicalFormatEtc(ref f, out output);
    public void SetData(ref FORMATETC f, ref STGMEDIUM m, bool release)
    {
        try
        {
            if (release)
                shell.SetData(ref f, ref m, true);
            else
            {
                Marshal.ThrowExceptionForHR(CopyStgMedium(ref m, out var owned));
                try
                {
                    shell.SetData(ref f, ref owned, true);
                }
                catch { ReleaseStgMedium(ref owned); throw; }
            }
        }
        catch (Exception e) { Settings.Log($"Shell SetData format={f.cfFormat},tymed={m.tymed},index={f.lindex},aspect={f.dwAspect},hr=0x{e.HResult:X8}: {e}"); throw; }
    }
    public IEnumFORMATETC EnumFormatEtc(DATADIR direction) => direction == DATADIR.DATADIR_GET ? new Combined(shell.EnumFormatEtc(direction), payload.EnumFormatEtc(direction)) : shell.EnumFormatEtc(direction);
    public int DAdvise(ref FORMATETC f, ADVF flags, IAdviseSink sink, out int connection) => shell.DAdvise(ref f, flags, sink, out connection);
    public void DUnadvise(int connection) => shell.DUnadvise(connection);
    public int EnumDAdvise(out IEnumSTATDATA? result) => shell.EnumDAdvise(out result);
    sealed class Combined : IEnumFORMATETC
    {
        readonly IEnumFORMATETC first, second; bool inSecond;
        public Combined(IEnumFORMATETC first, IEnumFORMATETC second, bool inSecond = false)
        {
            this.first = first;
            this.second = second;
            this.inSecond = inSecond;
        }
        public int Next(int count, FORMATETC[] output, int[]? fetched)
        {
            int n = 0;
            var one = new FORMATETC[1];
            var got = new int[1];
            while (n < count)
            {
                var status = (inSecond ? second : first).Next(1, one, got);
                if (got[0] > 0)
                    output[n++] = one[0];
                if (status != 0)
                {
                    if (inSecond)
                        break;
                    inSecond = true;
                }
            }
            if (fetched?.Length > 0)
                fetched[0] = n;
            return n == count ? 0 : 1;
        }
        public int Skip(int count)
        {
            var item = new FORMATETC[1];
            var fetched = new int[1];
            for (int i = 0; i < count; i++)
            {
                if (Next(1, item, fetched) != 0)
                    return 1;
                if (item[0].ptd != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(item[0].ptd);
            }
            return 0;
        }
        public int Reset()
        {
            inSecond = false;
            first.Reset();
            return second.Reset();
        }
        public void Clone(out IEnumFORMATETC result)
        {
            first.Clone(out var a);
            second.Clone(out var b);
            result = new Combined(a, b, inSecond);
        }
    }
}
