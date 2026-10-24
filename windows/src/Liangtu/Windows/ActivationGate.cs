using System.Security.Cryptography;
using System.Text;
using System.Threading;
namespace Liangtu;
public sealed class ActivationGate : IDisposable
{
    readonly Mutex mutex;
    readonly EventWaitHandle activation;
    RegisteredWaitHandle? wait;
    public bool IsFirst
    {
        get;
    }
    public ActivationGate(string dataRoot, Action reveal)
    {
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(dataRoot).ToUpperInvariant())))[..16];
        var name = "Local\\Liangtu." + id;
        mutex = new Mutex(false, name, out var first);
        IsFirst = first;
        activation = new EventWaitHandle(false, EventResetMode.AutoReset, name + ".Reveal");
        if (first)
            wait = ThreadPool.RegisterWaitForSingleObject(activation, (_, _) => reveal(), null, Timeout.Infinite, false);
        else
            activation.Set();
    }
    public void Dispose()
    {
        wait?.Unregister(null);
        activation.Dispose();
        mutex.Dispose();
    }
}
