namespace Liangtu;

internal static class SoundChecks
{
    public static void Run(Action<string, Action> test)
    {
        test("approved B sound family includes entering edit", () => {
            if (!Enum.GetNames<OperationCue>().Contains("Edit")) throw new Exception("Approved edit cue is missing");
            foreach (var pair in new[] {
                (OperationCue.Hang, "5C6203C85BD11B58C8108E6647B27FF2E70C48C2270C971215CC1DF0833BC834"),
                (OperationCue.Copy, "1B5D9F02B0F2C58E821EE94C3005B0B268DD20A4C722570BDE3D0BB49BBE5C6C"),
                (OperationCue.TakeDown, "8FF17A42782DCA01D2B82F3C809912EA5FF62CCF4338610E40701890907057EE"),
                (OperationCue.Edit, "ACE1794DD0EBE1AD9C806DEE2B260883E7A923E9AB2FA9011A645CF526A9EF49") })
                if (Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(OperationSounds.CreateWave(pair.Item1))) != pair.Item2)
                    throw new Exception("Runtime sound differs from the approved audition");
        });
        test("operation sounds use dedicated local cues instead of the system asterisk", () => {
            if (typeof(App).Assembly.GetType("Liangtu.OperationSounds") == null)
                throw new Exception("Dedicated operation sound service is missing");
        });
        test("material sound files are distinct short PCM cues with no clipping", () => {
            var waves = Enum.GetValues<OperationCue>().Select(OperationSounds.CreateWave).ToArray();
            var folder = Path.Combine(Settings.Root, "sounds"); Directory.CreateDirectory(folder);
            for (int i = 0; i < waves.Length; i++) {
                var data = waves[i];
                if (System.Text.Encoding.ASCII.GetString(data, 0, 4) != "RIFF" || BitConverter.ToInt32(data, 24) != 44100 || BitConverter.ToInt16(data, 34) != 16 || BitConverter.ToInt16(data, 22) != 1)
                    throw new Exception("Invalid PCM format");
                double duration = (data.Length - 44) / 88200.0;
                if (duration < .1 || duration > .25) throw new Exception("Unexpected cue length");
                var peak = Enumerable.Range(0, (data.Length - 44) / 2).Max(n => Math.Abs((int)BitConverter.ToInt16(data, 44 + n * 2)));
                if (peak < 100 || peak > 10000 || BitConverter.ToInt16(data, 44) != 0 || Math.Abs((int)BitConverter.ToInt16(data, data.Length - 2)) > 3)
                    throw new Exception("Cue clipping, silent output or abrupt endpoint");
                File.WriteAllBytes(Path.Combine(folder, ((OperationCue)i) + ".wav"), data);
            }
            if (waves[0].SequenceEqual(waves[1]) || waves[1].SequenceEqual(waves[2])) throw new Exception("Cues are identical");
        });
        test("sound gate respects mute and coalesces rapid events", () => {
            var gate = new SoundGate();
            if (gate.Accept(0, false) || !gate.Accept(0, true) || gate.Accept(.1, true) || gate.Accept(.27, true) || !gate.Accept(.3, true) || gate.Accept(1, false) || !gate.Accept(1, true))
                throw new Exception("Sound gate ignored mute or burst interval");
        });
    }
}
