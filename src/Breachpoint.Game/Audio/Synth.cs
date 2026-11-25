namespace Breachpoint.Audio;

/// <summary>
/// Tiny offline synthesizer. Every sound in the game is built from filtered noise, oscillators and envelopes
/// into float buffers, which are then turned into MonoGame SoundEffects. No audio files are shipped.
/// </summary>
public sealed class Synth
{
    public const int SR = 22050;
    private readonly Random _r;
    public Synth(int seed) => _r = new Random(seed);
    public double Rnd(double a, double b) => a + _r.NextDouble() * (b - a);

    public static float[] Buf(double seconds) => new float[(int)(seconds * SR)];

    private sealed class Biquad
    {
        private readonly string _type;
        private double _b0, _b1, _b2, _a1, _a2, _x1, _x2, _y1, _y2;
        public Biquad(string type) => _type = type;
        public void Set(double f, double q)
        {
            f = Math.Clamp(f, 10, SR / 2.0 - 100);
            double w = 2 * Math.PI * f / SR, cs = Math.Cos(w), sn = Math.Sin(w), alpha = sn / (2 * Math.Max(0.05, q)), a0;
            switch (_type)
            {
                case "highpass": _b0 = (1 + cs) / 2; _b1 = -(1 + cs); _b2 = (1 + cs) / 2; break;
                case "bandpass": _b0 = alpha; _b1 = 0; _b2 = -alpha; break;
                default: _b0 = (1 - cs) / 2; _b1 = 1 - cs; _b2 = (1 - cs) / 2; break;
            }
            a0 = 1 + alpha; _a1 = -2 * cs / a0; _a2 = (1 - alpha) / a0; _b0 /= a0; _b1 /= a0; _b2 /= a0;
        }
        public double Process(double x)
        {
            double y = _b0 * x + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
            _x2 = _x1; _x1 = x; _y2 = _y1; _y1 = y;
            return y;
        }
    }

    private static double Env(double t, double a, double dec) =>
        t < a ? t / Math.Max(1e-4, a) : Math.Exp(-(t - a) / Math.Max(1e-4, dec) * 6.9);

    /// <summary>Filtered noise burst. f2 sweeps the filter exponentially.</summary>
    public void Noise(float[] buf, double t0, string type = "lowpass", double f = 2000, double q = 0.7, double a = 0.002, double dec = 0.2, double vol = 1, double? f2 = null, bool brown = false)
    {
        var bq = new Biquad(type); bq.Set(f, q);
        int s0 = (int)(t0 * SR), n = (int)((a + dec * 1.15) * SR);
        double last = 0;
        for (int i = 0; i < n && s0 + i < buf.Length; i++)
        {
            double t = (double)i / SR;
            if (f2.HasValue && (i & 31) == 0) bq.Set(f * Math.Pow(f2.Value / f, Math.Min(1, t / (a + dec))), q);
            double w = _r.NextDouble() * 2 - 1;
            if (brown) { last = (last + 0.02 * w) / 1.02; w = last * 3.5; }
            buf[s0 + i] += (float)(bq.Process(w) * Env(t, a, dec) * vol);
        }
    }

    /// <summary>Oscillator with optional exponential pitch sweep and band-pass "formant".</summary>
    public void Tone(float[] buf, double t0, string wave = "sine", double f = 440, double? f2 = null, double a = 0.002, double dec = 0.2, double vol = 0.5, double? filt = null, double vibHz = 0, double vibAmt = 0)
    {
        Biquad bq = null;
        if (filt.HasValue) { bq = new Biquad("bandpass"); bq.Set(filt.Value, 3); }
        int s0 = (int)(t0 * SR), n = (int)((a + dec * 1.15) * SR);
        double ph = 0;
        for (int i = 0; i < n && s0 + i < buf.Length; i++)
        {
            double t = (double)i / SR;
            double fr = f2.HasValue ? f * Math.Pow(f2.Value / f, Math.Min(1, t / (a + dec))) : f;
            if (vibAmt > 0) fr += Math.Sin(t * vibHz * 2 * Math.PI) * vibAmt;
            ph += fr / SR; ph -= Math.Floor(ph);
            double v = wave switch
            {
                "square" => ph < 0.5 ? 1 : -1,
                "saw" => ph * 2 - 1,
                "triangle" => ph < 0.5 ? ph * 4 - 1 : 3 - ph * 4,
                _ => Math.Sin(ph * 2 * Math.PI),
            };
            if (bq != null) v = bq.Process(v) * 3;
            buf[s0 + i] += (float)(v * Env(t, a, dec) * vol);
        }
    }

    /// <summary>Schroeder reverb, mixed in place.</summary>
    public static void Reverb(float[] buf, double wet, double room = 0.82)
    {
        int[] combs = { 1116, 1188, 1277, 1356 };
        int[] alls = { 556, 441 };
        var outp = new float[buf.Length];
        foreach (int c0 in combs)
        {
            int c = c0 * SR / 44100;
            var line = new float[c]; int idx = 0; float lp = 0;
            for (int i = 0; i < buf.Length; i++)
            {
                float y = line[idx];
                lp = y * 0.6f + lp * 0.4f;
                line[idx] = buf[i] + lp * (float)room;
                idx = (idx + 1) % c;
                outp[i] += y * 0.25f;
            }
        }
        foreach (int a0 in alls)
        {
            int a = a0 * SR / 44100;
            var line = new float[a]; int idx = 0;
            for (int i = 0; i < outp.Length; i++)
            {
                float bufout = line[idx], x = outp[i];
                outp[i] = -x + bufout;
                line[idx] = x + bufout * 0.5f;
                idx = (idx + 1) % a;
            }
        }
        for (int i = 0; i < buf.Length; i++) buf[i] += (float)(outp[i] * wet);
    }

    public static void Normalize(float[] buf, float peak = 0.9f)
    {
        float m = 0;
        foreach (var v in buf) m = Math.Max(m, Math.Abs(v));
        if (m < 1e-6f) return;
        float g = peak / m;
        for (int i = 0; i < buf.Length; i++) buf[i] *= g;
    }

    public static void SoftClip(float[] buf)
    {
        for (int i = 0; i < buf.Length; i++) buf[i] = (float)Math.Tanh(buf[i]);
    }

    /// <summary>Crossfade the tail into the head so the buffer loops seamlessly.</summary>
    public static float[] MakeLoop(float[] buf, double fadeSec = 1.0)
    {
        int f = (int)(fadeSec * SR);
        var o = new float[buf.Length - f];
        Array.Copy(buf, o, o.Length);
        for (int i = 0; i < f; i++)
        {
            float k = (float)i / f;
            o[i] = buf[i] * k + buf[o.Length + i] * (1 - k);
        }
        return o;
    }

    public static byte[] ToPcm16(float[] buf)
    {
        var bytes = new byte[buf.Length * 2];
        for (int i = 0; i < buf.Length; i++)
        {
            short s = (short)(Math.Clamp(buf[i], -1f, 1f) * 32000);
            bytes[i * 2] = (byte)(s & 0xFF);
            bytes[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
        }
        return bytes;
    }

    public static double Note(int n) => 440 * Math.Pow(2, (n - 69) / 12.0);
}
