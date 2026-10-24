using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
namespace Liangtu;

internal static class PackageVerifier
{
    public static int Run()
    {
        try {
            var root = AppContext.BaseDirectory;
            if (!FileRules.IsInside(typeof(object).Assembly.Location, root) || !FileRules.IsInside(typeof(Window).Assembly.Location, root)) throw new IOException("The package is not using its bundled desktop runtime.");
            var manifest = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(root, "SHA256SUMS.json"))) ?? throw new IOException("Missing package manifest");
            foreach (var (relative, expected) in manifest) {
                var path = Path.GetFullPath(Path.Combine(root, relative));
                if (!FileRules.IsInside(path, root) || !File.Exists(path)) throw new IOException("Missing/invalid package file: " + relative);
                using var stream = File.OpenRead(path);
                if (!Convert.ToHexString(SHA256.HashData(stream)).Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new IOException("Package checksum mismatch: " + relative);
            }
            var native = NativeLibrary.Load(Path.Combine(root, "GlassNative.dll")); NativeLibrary.Free(native);
            Console.WriteLine($"PASS portable package {typeof(App).Assembly.GetName().Version}: {manifest.Count} file hashes, bundled {RuntimeInformation.FrameworkDescription}, bundled WPF, native DLL load. No windows or user-data access.");
            return 0;
        } catch (Exception e) { Console.Error.WriteLine("FAIL package verification: " + e.Message); return 1; }
    }
}
