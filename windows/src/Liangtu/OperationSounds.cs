using System.Diagnostics;
using System.Media;
namespace Liangtu;

internal enum OperationCue { Hang, TakeDown, Copy, Edit }

internal sealed class SoundGate
{
    double last = double.NegativeInfinity;
    public bool Accept(double seconds, bool enabled)
    {
        if (!enabled || seconds - last < .28) return false;
        last = seconds; return true;
    }
}

// Exact B-family audition files approved by the user on 2026-10-24.
// One short voice per action; no system notification or network dependency.
internal sealed class OperationSounds : IDisposable
{
    readonly Dictionary<OperationCue, (MemoryStream stream, SoundPlayer player)> players = new();
    readonly SoundGate gate = new();
    readonly Stopwatch clock = Stopwatch.StartNew();
    SoundPlayer? current;
    public void Play(OperationCue cue, bool enabled)
    {
        if (!gate.Accept(clock.Elapsed.TotalSeconds, enabled)) return;
        try {
            if (!players.TryGetValue(cue, out var entry)) {
                var stream = new MemoryStream(CreateWave(cue), false);
                var player = new SoundPlayer(stream); player.Load();
                entry = (stream, player); players.Add(cue, entry);
            }
            current?.Stop(); current = entry.player; current.Play();
        } catch (Exception e) { Settings.Log("Sound " + e.Message); }
    }
    public void Stop() { current?.Stop(); }
    public void Dispose()
    {
        Stop(); foreach (var entry in players.Values) { entry.player.Dispose(); entry.stream.Dispose(); } players.Clear();
    }
    internal static byte[] CreateWave(OperationCue cue)
    {
        var name = cue switch { OperationCue.Hang => "hang", OperationCue.Copy => "copy", OperationCue.TakeDown => "remove", OperationCue.Edit => "edit", _ => throw new ArgumentOutOfRangeException(nameof(cue)) };
        using var resource = typeof(OperationSounds).Assembly.GetManifestResourceStream("Liangtu.Assets.Sounds.B-" + name + ".wav")
            ?? throw new IOException("Missing approved sound: " + name);
        using var output = new MemoryStream(); resource.CopyTo(output); return output.ToArray();
    }
}
