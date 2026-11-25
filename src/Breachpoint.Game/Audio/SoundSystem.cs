using Breachpoint.Core.Sim;
using Microsoft.Xna.Framework.Audio;

namespace Breachpoint.Audio;

/// <summary>
/// Builds the whole sound bank procedurally (background thread) and plays positional effects,
/// ambience loops, music beds and voice callouts.
/// </summary>
public sealed class SoundSystem
{
    private readonly BreachpointGame _g;
    private readonly Dictionary<string, List<SoundEffect>> _bank = new();
    private readonly Dictionary<string, SoundEffect> _loops = new();
    private readonly Random _r = new();
    private volatile bool _ready;
    private SoundEffectInstance _amb, _amb2, _music;
    private string _ambName, _amb2Name, _musicName;
    private readonly Dictionary<string, double> _lastPlay = new();
    private int _playsThisFrame;
    private double _lastSpeak = -10;
    public double LX, LY, LRange = 900, LRot;
    private bool _audioOk = true;

    public SoundSystem(BreachpointGame g)
    {
        _g = g;
        Task.Run(Build);
    }

    public bool Ready => _ready;

    private void Add(string name, float[] buf, bool norm = true, float peak = 0.9f)
    {
        if (norm) Synth.Normalize(buf, peak);
        var se = new SoundEffect(Synth.ToPcm16(buf), Synth.SR, AudioChannels.Mono);
        lock (_bank)
        {
            if (!_bank.TryGetValue(name, out var l)) _bank[name] = l = new List<SoundEffect>();
            l.Add(se);
        }
    }

    private void Build()
    {
        try
        {
            var s = new Synth(1234);
            // --- gunshots ---
            foreach (var (grp, lp, dec, vol, thump) in new[] { ("rifle", 4200.0, 0.24, 0.85, 95.0), ("heavy", 3200.0, 0.36, 1.05, 70.0), ("pistol", 2600.0, 0.16, 0.85, 95.0), ("shotgun", 1800.0, 0.42, 1.1, 60.0) })
                foreach (bool indoor in new[] { false, true })
                    for (int v = 0; v < 3; v++)
                    {
                        var b = Synth.Buf(indoor ? 1.1 : 0.7);
                        s.Noise(b, 0, "lowpass", lp * s.Rnd(0.9, 1.1), dec: dec, vol: vol, f2: 300);
                        s.Noise(b, 0, "highpass", 2200, dec: 0.04, vol: grp == "heavy" ? 0.7 : 0.45);
                        s.Tone(b, 0, "sine", thump, 30, dec: grp is "shotgun" ? 0.3 : 0.18, vol: grp is "shotgun" or "heavy" ? 0.9 : 0.55);
                        if (indoor) Synth.Reverb(b, 0.55);
                        Synth.SoftClip(b);
                        Add($"shot_{grp}_{(indoor ? "in" : "out")}", b);
                    }
            for (int v = 0; v < 3; v++)
            {
                var b = Synth.Buf(0.25);
                s.Noise(b, 0, "lowpass", s.Rnd(700, 1000), dec: 0.07, vol: 0.35);
                s.Noise(b, 0, "highpass", 3000, dec: 0.015, vol: 0.25);
                s.Tone(b, 0.03, "square", 1800, 900, dec: 0.02, vol: 0.05);
                Add("shotSup", b, peak: 0.45f);
            }
            for (int v = 0; v < 3; v++) { var b = Synth.Buf(0.3); s.Tone(b, 0, "sine", s.Rnd(3800, 5600), dec: 0.05, vol: 0.06); s.Tone(b, s.Rnd(0.06, 0.12), "sine", s.Rnd(3500, 5200), dec: 0.04, vol: 0.035); Add("shell", b, peak: 0.25f); }
            { var b = Synth.Buf(0.3); s.Tone(b, 0, "sine", 780, dec: 0.05, vol: 0.06); s.Tone(b, 0.09, "sine", 650, dec: 0.05, vol: 0.04); Add("shellSg", b, peak: 0.25f); }
            { var b = Synth.Buf(1.0); s.Noise(b, 0, "bandpass", 2500, 2, dec: 0.03, vol: 0.4); s.Noise(b, 0.35, "bandpass", 1600, 2, dec: 0.04, vol: 0.5); s.Noise(b, 0.75, "bandpass", 3200, 3, dec: 0.03, vol: 0.5); Add("reload", b, peak: 0.5f); }
            { var b = Synth.Buf(0.5); s.Noise(b, 0.1, "bandpass", 1200, 2, dec: 0.06, vol: 0.5); s.Noise(b, 0.25, "bandpass", 1800, 2, dec: 0.05, vol: 0.5); Add("pump", b, peak: 0.55f); }
            { var b = Synth.Buf(0.1); s.Tone(b, 0, "square", 2200, dec: 0.015, vol: 0.12); Add("dryfire", b, peak: 0.3f); }
            for (int v = 0; v < 4; v++) { var b = Synth.Buf(0.15); s.Noise(b, 0, "lowpass", s.Rnd(300, 520), dec: 0.07); s.Noise(b, 0, "bandpass", s.Rnd(1500, 2500), 1, dec: 0.02, vol: 0.2); Add("step", b, peak: 0.18f); Add("stepRun", b.ToArray(), peak: 0.3f); }
            for (int v = 0; v < 2; v++)
            {
                var b = Synth.Buf(0.8);
                s.Tone(b, 0, "square", 1400, dec: 0.02, vol: 0.12);
                s.Tone(b, 0.03, "saw", s.Rnd(160, 220), s.Rnd(260, 340), a: 0.05, dec: 0.5, vol: 0.18, filt: 900, vibHz: 23, vibAmt: 30);
                Synth.Reverb(b, 0.3);
                Add("doorOpen", b, peak: 0.45f);
            }
            { var b = Synth.Buf(0.4); s.Tone(b, 0, "sine", 110, 60, dec: 0.12, vol: 0.5); s.Noise(b, 0, "lowpass", 900, dec: 0.08, vol: 0.4); Add("doorClose", b, peak: 0.5f); }
            for (int ok = 0; ok < 2; ok++)
            {
                var b = Synth.Buf(0.8);
                s.Tone(b, 0, "sine", 100, 40, dec: 0.22, vol: 0.9);
                s.Noise(b, 0, "lowpass", 1400, dec: 0.12, vol: 0.7);
                if (ok == 1) { s.Noise(b, 0.02, "bandpass", 2200, 0.8, dec: 0.25, vol: 0.6); for (int i = 0; i < 4; i++) s.Tone(b, s.Rnd(0.02, 0.2), "triangle", s.Rnd(300, 700), dec: 0.05, vol: 0.1); }
                Synth.Reverb(b, 0.4);
                Add(ok == 1 ? "kickOk" : "kick", b);
            }
            { var b = Synth.Buf(2.6); s.Noise(b, 0, "lowpass", 1200, dec: 1.4, vol: 1.3, f2: 80); s.Noise(b, 0, "highpass", 1500, dec: 0.12, vol: 0.9); s.Tone(b, 0, "sine", 60, 22, dec: 1.0, vol: 1.2); for (int i = 0; i < 8; i++) s.Noise(b, s.Rnd(0.1, 0.8), "bandpass", s.Rnd(800, 3000), 2, dec: 0.04, vol: 0.15); Synth.Reverb(b, 0.4); Synth.SoftClip(b); Add("explosion", b, peak: 1f); }
            { var b = Synth.Buf(1.4); s.Noise(b, 0, "highpass", 400, dec: 0.5, vol: 1.3); s.Tone(b, 0, "sine", 80, 30, dec: 0.4, vol: 1); Synth.Reverb(b, 0.5); Synth.SoftClip(b); Add("flashbang", b, peak: 1f); }
            { var b = Synth.Buf(3.0); s.Tone(b, 0.05, "sine", 3900, a: 0.05, dec: 2.8, vol: 0.4); Add("ring", b, peak: 0.12f); }
            { var b = Synth.Buf(2.4); s.Tone(b, 0, "square", 600, dec: 0.03, vol: 0.2); s.Noise(b, 0.05, "highpass", 2500, a: 0.1, dec: 2.0, vol: 0.4); Add("smoke", b, peak: 0.4f); }
            { var b = Synth.Buf(0.3); s.Tone(b, 0, "triangle", 3000, dec: 0.04, vol: 0.2); s.Tone(b, 0.15, "triangle", 2200, dec: 0.05, vol: 0.15); Add("pin", b, peak: 0.35f); }
            for (int v = 0; v < 2; v++) { var b = Synth.Buf(0.12); s.Tone(b, 0, "triangle", s.Rnd(500, 800), dec: 0.05, vol: 0.3); Add("bounce", b, peak: 0.35f); }
            for (int v = 0; v < 2; v++) { var b = Synth.Buf(0.6); s.Noise(b, 0, "highpass", 3500, dec: 0.35, vol: 0.6); for (int i = 0; i < 9; i++) s.Tone(b, s.Rnd(0, 0.25), "sine", s.Rnd(2500, 7500), dec: s.Rnd(0.04, 0.15), vol: 0.08); Add("glass", b, peak: 0.6f); }
            for (int v = 0; v < 3; v++) { var b = Synth.Buf(0.1); s.Noise(b, 0, "bandpass", s.Rnd(900, 1500), 1, dec: 0.05, vol: 0.5); Add("impact", b, peak: 0.35f); }
            for (int v = 0; v < 2; v++) { var b = Synth.Buf(0.25); s.Tone(b, 0, "triangle", s.Rnd(1800, 3000), s.Rnd(900, 1400), dec: 0.15, vol: 0.2); Add("impactMetal", b, peak: 0.3f); }
            for (int v = 0; v < 2; v++) { var b = Synth.Buf(0.15); s.Noise(b, 0, "lowpass", 700, dec: 0.08, vol: 0.8); Add("flesh", b, peak: 0.5f); }
            for (int v = 0; v < 3; v++) { var b = Synth.Buf(0.35); double f = s.Rnd(120, 160); s.Tone(b, 0, "saw", f, f * 0.75, a: 0.02, dec: 0.2, vol: 0.35, filt: 650); Add("grunt", b, peak: 0.45f); }
            for (int v = 0; v < 2; v++) { var b = Synth.Buf(0.35); double f = s.Rnd(260, 320); s.Tone(b, 0, "saw", f, f * 0.75, a: 0.02, dec: 0.2, vol: 0.35, filt: 900); Add("gruntF", b, peak: 0.45f); }
            for (int v = 0; v < 2; v++) { var b = Synth.Buf(1.0); s.Tone(b, 0, "saw", s.Rnd(500, 600), s.Rnd(800, 950), a: 0.05, dec: 0.7, vol: 0.3, filt: 1300, vibHz: 7, vibAmt: 25); Synth.Reverb(b, 0.3); Add("scream", b, peak: 0.35f); }
            { var b = Synth.Buf(0.4); s.Tone(b, 0, "sine", 75, 40, dec: 0.18, vol: 0.6); s.Noise(b, 0, "lowpass", 350, dec: 0.2, vol: 0.5); Add("fall", b, peak: 0.55f); }
            { var b = Synth.Buf(0.35); for (int i = 0; i < 7; i++) s.Noise(b, i * 0.03, "bandpass", 3500, 4, dec: 0.015, vol: 0.4); Add("cuffs", b, peak: 0.45f); }
            { var b = Synth.Buf(0.6); for (int i = 0; i < 4; i++) s.Tone(b, i * s.Rnd(0.08, 0.2), "triangle", s.Rnd(3500, 5000), dec: 0.015, vol: 0.12); Add("pick", b, peak: 0.25f); }
            { var b = Synth.Buf(0.9); for (int i = 0; i < 3; i++) s.Noise(b, i * 0.22, "bandpass", s.Rnd(400, 700), 1.5, a: 0.01, dec: 0.12, vol: 0.6); Add("cough", b, peak: 0.4f); }
            for (int v = 0; v < 2; v++) { var b = Synth.Buf(0.35); s.Tone(b, 0, "sine", s.Rnd(2500, 4000), s.Rnd(900, 1400), dec: 0.25, vol: 0.12); Add("ricochet", b, peak: 0.3f); }
            { var b = Synth.Buf(0.15); s.Noise(b, 0, "bandpass", 2200, 1.5, dec: 0.06, vol: 0.12); s.Tone(b, 0.01, "square", 1450, dec: 0.04, vol: 0.03); Add("radio", b, peak: 0.3f); }
            { var b = Synth.Buf(0.06); s.Tone(b, 0, "triangle", 1300, dec: 0.03, vol: 0.08); Add("ui", b, peak: 0.18f); }
            { var b = Synth.Buf(0.25); s.Tone(b, 0, "triangle", 880, dec: 0.06, vol: 0.1); s.Tone(b, 0.07, "triangle", 1320, dec: 0.1, vol: 0.1); Add("confirm", b, peak: 0.25f); }
            { var b = Synth.Buf(0.25); s.Tone(b, 0, "square", 220, dec: 0.15, vol: 0.06); Add("error", b, peak: 0.2f); }
            { var b = Synth.Buf(0.4); s.Tone(b, 0, "triangle", 1600, dec: 0.08, vol: 0.12); s.Tone(b, 0.09, "triangle", 2400, dec: 0.2, vol: 0.12); s.Noise(b, 0, "bandpass", 4000, 3, dec: 0.05, vol: 0.2); Add("cash", b, peak: 0.3f); }
            { var b = Synth.Buf(0.25); s.Noise(b, 0, "bandpass", 1800, 2, dec: 0.04, vol: 0.4); s.Noise(b, 0.12, "bandpass", 2600, 3, dec: 0.03, vol: 0.4); Add("equip", b, peak: 0.35f); }
            { var b = Synth.Buf(0.7); s.Tone(b, 0, "sine", 90, 40, dec: 0.4, vol: 0.8); s.Noise(b, 0, "lowpass", 1500, dec: 0.2, vol: 0.5); Synth.Reverb(b, 0.4); Add("rankStamp", b, peak: 0.7f); }
            _ready = true;

            // --- ambience loops ---
            _loops["city"] = Loop(s, 16, b =>
            {
                s.Noise(b, 0, "lowpass", 300, a: 0.01, dec: 1e6, vol: 0.5, brown: true);
                for (int i = 0; i < 3; i++) { double t = s.Rnd(0, 12); s.Noise(b, t, "bandpass", 420, 1.2, a: 1.6, dec: 2.2, vol: 0.18); }
                double st = s.Rnd(2, 8); s.Tone(b, st, "sine", 880, a: 2.5, dec: 4, vol: 0.03, vibHz: 0.35, vibAmt: 260);
            });
            _loops["rain"] = Loop(s, 10, b => { s.Noise(b, 0, "highpass", 1400, a: 0.01, dec: 1e6, vol: 0.35); s.Noise(b, 0, "lowpass", 600, a: 0.01, dec: 1e6, vol: 0.2); });
            _loops["rural"] = Loop(s, 14, b =>
            {
                s.Noise(b, 0, "bandpass", 380, 0.6, a: 0.01, dec: 1e6, vol: 0.4);
                for (int i = 0; i < 8; i++) { double t = s.Rnd(0, 12); for (int k = 0; k < 3; k++) s.Tone(b, t + k * 0.12, "sine", s.Rnd(4200, 4800), dec: 0.05, vol: 0.02); }
            });
            _loops["bunker"] = Loop(s, 10, b => { s.Noise(b, 0, "lowpass", 120, a: 0.01, dec: 1e6, vol: 0.6, brown: true); s.Tone(b, 0, "sine", 50, a: 0.01, dec: 1e6, vol: 0.05); });
            _loops["hq"] = Loop(s, 12, b =>
            {
                s.Noise(b, 0, "lowpass", 220, a: 0.01, dec: 1e6, vol: 0.4, brown: true);
                s.Tone(b, 0, "sine", 60, a: 0.01, dec: 1e6, vol: 0.02);
                s.Noise(b, 5, "bandpass", 2200, 1.5, dec: 0.06, vol: 0.05);
            });
            _loops["heli"] = Loop(s, 14, b =>
            {
                var tmp = Synth.Buf(14.5); s.Noise(tmp, 0, "lowpass", 260, a: 0.01, dec: 1e6, vol: 1);
                for (int i = 0; i < tmp.Length; i++) { double t = (double)i / Synth.SR; double am = Math.Sin(t * 13 * 2 * Math.PI) > 0 ? 1 : 0.4; double sw = Math.Sin(Math.PI * Math.Min(1, t / 14)); b[i] += (float)(tmp[i] * am * sw * 0.4); }
            });
            // --- music ---
            _loops["music_hq"] = Music(new[] { new[] { 45, 52, 57, 60 }, new[] { 41, 48, 53, 57 }, new[] { 48, 55, 60, 64 }, new[] { 43, 50, 55, 59 } }, 6, 900, true);
            _loops["music_mission"] = Music(new[] { new[] { 38, 45, 50 }, new[] { 38, 44, 50 }, new[] { 36, 43, 48 }, new[] { 37, 44, 49 } }, 8, 500, false);
            _loops["music_debrief"] = Music(new[] { new[] { 48, 55, 60, 64, 67 }, new[] { 45, 52, 57, 60, 64 } }, 6.5, 1400, false);
        }
        catch (Exception e)
        {
            _audioOk = false;
            Console.Error.WriteLine("Audio init failed: " + e.Message);
        }
    }

    private static SoundEffect Loop(Synth s, double sec, Action<float[]> fill)
    {
        var b = Synth.Buf(sec + 1);
        fill(b);
        Synth.Normalize(b, 0.6f);
        var l = Synth.MakeLoop(b, 1);
        return new SoundEffect(Synth.ToPcm16(l), Synth.SR, AudioChannels.Mono);
    }

    private static SoundEffect Music(int[][] prog, double chordSec, double cut, bool bass)
    {
        double total = prog.Length * chordSec;
        var b = Synth.Buf(total + 1.5);
        var rng = new Random(7);
        for (int c = 0; c < prog.Length; c++)
        {
            double t0 = c * chordSec;
            foreach (int note in prog[c])
                foreach (double det in new[] { -0.004, 0.0035 })
                {
                    double f = Synth.Note(note) * (1 + det);
                    int s0 = (int)(t0 * Synth.SR), n = (int)((chordSec + 1.2) * Synth.SR);
                    double ph = rng.NextDouble(), lp = 0;
                    for (int i = 0; i < n && s0 + i < b.Length; i++)
                    {
                        double t = (double)i / Synth.SR;
                        ph += f / Synth.SR; ph -= Math.Floor(ph);
                        double saw = ph * 2 - 1;
                        double cutoff = cut * (0.6 + 0.4 * Math.Sin(Math.PI * Math.Min(1, t / chordSec)));
                        double k = 1 - Math.Exp(-2 * Math.PI * cutoff / Synth.SR);
                        lp += (saw - lp) * k;
                        double env = Math.Min(1, t / (chordSec * 0.3)) * Math.Max(0, Math.Min(1, (chordSec + 1.1 - t) / 1.1));
                        b[s0 + i] += (float)(lp * env * 0.05);
                    }
                }
            if (bass)
            {
                double f = Synth.Note(prog[c][0] - 12);
                int s0 = (int)(t0 * Synth.SR), n = (int)(chordSec * Synth.SR);
                for (int i = 0; i < n && s0 + i < b.Length; i++) { double t = (double)i / Synth.SR; b[s0 + i] += (float)(Math.Sin(2 * Math.PI * f * t) * Math.Min(1, t / 0.4) * Math.Exp(-t / 3) * 0.12); }
            }
        }
        Synth.Reverb(b, 0.35);
        Synth.Normalize(b, 0.55f);
        var l = Synth.MakeLoop(b, 1.2);
        return new SoundEffect(Synth.ToPcm16(l), Synth.SR, AudioChannels.Mono);
    }

    // ---------------- playback ----------------
    public void Update() { _playsThisFrame = 0; Tick(); }

    public void ApplyVolumes()
    {
        var st = _g.Settings;
        try
        {
            if (_amb != null) _amb.Volume = Math.Clamp(st.Master * st.Ambient, 0, 1);
            if (_amb2 != null) _amb2.Volume = Math.Clamp(st.Master * st.Ambient * 0.8f, 0, 1);
            if (_music != null) _music.Volume = Math.Clamp(st.Master * st.Music, 0, 1);
        }
        catch { }
    }

    private (float vol, float pan) Spatial(double x, double y, bool positional)
    {
        if (!positional) return (1, 0);
        double dx = x - LX, dy = y - LY, d = Math.Sqrt(dx * dx + dy * dy);
        float vol = (float)Math.Clamp(1 - d / (LRange * 1.6), 0.04, 1);
        double sx = Math.Cos(LRot) * dx - Math.Sin(LRot) * dy;
        return (vol, (float)Math.Clamp(sx / (LRange * 0.7), -0.9, 0.9));
    }

    public void Play(string name, double x = double.NaN, double y = double.NaN, float vol = 1, float pitchJitter = 0.06f)
    {
        if (!_ready || !_audioOk) return;
        List<SoundEffect> list;
        lock (_bank) if (!_bank.TryGetValue(name, out list) || list.Count == 0) return;
        if (_playsThisFrame > 10) return;
        double now = _g.Time;
        if (_lastPlay.TryGetValue(name, out var lp) && now - lp < 0.03) return;
        _lastPlay[name] = now;
        var (sv, pan) = Spatial(x, y, !double.IsNaN(x));
        var st = _g.Settings;
        float v = Math.Clamp(vol * sv * st.Master * st.Sfx, 0, 1);
        if (v < 0.01f) return;
        try
        {
            list[_r.Next(list.Count)].Play(v, (float)(_r.NextDouble() * 2 - 1) * pitchJitter, pan);
            _playsThisFrame++;
        }
        catch { }
    }

    public void Ui() => Play("ui");
    public void Confirm() => Play("confirm");
    public void Error() => Play("error");
    public void Cash() => Play("cash");
    public void Equip() => Play("equip");

    /// <summary>Plays a simulation sound event.</summary>
    public void Play(SoundEvent e)
    {
        switch (e.Name)
        {
            case "shot":
            {
                string grp = e.Cal switch { "7.62" or ".338" => "heavy", "12g" => "shotgun", "9mm" or ".45" or "5.7" or "4.6" => "pistol", _ => "rifle" };
                Play($"shot_{grp}_{(e.Flag ? "in" : "out")}", e.X, e.Y);
                break;
            }
            case "shell": Play(e.Cal == "12g" ? "shellSg" : "shell", e.X, e.Y, 0.6f); break;
            case "kick": Play(e.Flag ? "kickOk" : "kick", e.X, e.Y); break;
            case "impact": Play(e.Flag ? "impactMetal" : "impact", e.X, e.Y, 0.5f); break;
            case "grunt": Play(e.Flag ? "gruntF" : "grunt", e.X, e.Y, 0.7f); break;
            case "step": Play("step", e.X, e.Y, 0.5f, 0.15f); break;
            case "stepRun": Play("stepRun", e.X, e.Y, 0.7f, 0.15f); break;
            case "flashbang":
            {
                Play("flashbang", e.X, e.Y);
                var (v, _) = Spatial(e.X, e.Y, true);
                if (v > 0.55f) Play("ring", vol: v);
                break;
            }
            case "error": Error(); break;
            default: Play(e.Name, e.X, e.Y); break;
        }
    }

    public void Voice(string text, double pitch, bool prio)
    {
        if (!_g.Settings.Voice) return;
        double now = _g.Time;
        if (!prio && now - _lastSpeak < 0.9) return;
        _lastSpeak = now;
        Play("radio", vol: 0.8f);
        try { _g.Platform.Speak(text, (float)pitch, 1.15f, prio, 0.8f * _g.Settings.Master); } catch { }
    }

    public void Ambient(string name, string second = null)
    {
        SetLoop(ref _amb, ref _ambName, name, _g.Settings.Ambient);
        SetLoop(ref _amb2, ref _amb2Name, second, _g.Settings.Ambient * 0.8f);
    }

    public void MusicTrack(string name) => SetLoop(ref _music, ref _musicName, name == null ? null : "music_" + name, _g.Settings.Music);

    private void SetLoop(ref SoundEffectInstance inst, ref string cur, string name, float vol)
    {
        if (cur == name && inst != null) return;
        try { inst?.Stop(); inst?.Dispose(); } catch { }
        inst = null; cur = name;
        if (name == null) return;
        if (!_loops.TryGetValue(name, out var se)) { _deferred[name] = true; return; }
        try
        {
            inst = se.CreateInstance();
            inst.IsLooped = true;
            inst.Volume = Math.Clamp(_g.Settings.Master * vol, 0, 1);
            inst.Play();
        }
        catch { inst = null; }
    }
    private readonly Dictionary<string, bool> _deferred = new();

    /// <summary>Retry loops that were requested before the background generator finished.</summary>
    public void Tick()
    {
        if (_deferred.Count == 0) return;
        foreach (var k in _deferred.Keys.ToList())
        {
            if (!_loops.ContainsKey(k)) continue;
            _deferred.Remove(k);
            if (k == _ambName) { _ambName = null; Ambient(k, _amb2Name); }
            else if (k == _amb2Name) { var n = _amb2Name; _amb2Name = null; SetLoop(ref _amb2, ref _amb2Name, n, _g.Settings.Ambient * 0.8f); }
            else if (k == _musicName) { var n = _musicName; _musicName = null; SetLoop(ref _music, ref _musicName, n, _g.Settings.Music); }
        }
    }

    public void PauseAll() { try { SoundEffect.MasterVolume = 0; } catch { } }
    public void ResumeAll() { try { SoundEffect.MasterVolume = 1; } catch { } }
}
