using UnityEngine;

// Match sounds synthesized at startup (no audio assets): goal horn, period buzzer, countdown beeps.
public static class GameSfx
{
    static AudioClip horn, buzzer, beep, go;
    static AudioSource src;

    public static void Init(GameObject host)
    {
        if (!host.TryGetComponent(out src)) src = host.AddComponent<AudioSource>();
        src.playOnAwake = false; src.spatialBlend = 0f;
        if (horn != null) return;
        horn = Make("horn", 1.6f, (t, ph) => Saw(ph[0]) * 0.5f + Saw(ph[1]) * 0.35f + Saw(ph[2]) * 0.25f, new[] { 110f, 138.6f, 164.8f }, 0.04f, 0.35f);
        buzzer = Make("buzzer", 1.1f, (t, ph) => Square(ph[0]) * 0.6f + Square(ph[1]) * 0.4f, new[] { 196f, 201f }, 0.01f, 0.08f);
        beep = Make("beep", 0.16f, (t, ph) => Mathf.Sin(ph[0] * 6.2832f), new[] { 880f }, 0.005f, 0.05f);
        go = Make("go", 0.45f, (t, ph) => Mathf.Sin(ph[0] * 6.2832f) * 0.7f + Mathf.Sin(ph[1] * 6.2832f) * 0.3f, new[] { 1320f, 1760f }, 0.005f, 0.2f);
    }

    public static void Horn() => Play(horn, 0.9f);
    public static void Buzzer() => Play(buzzer, 0.7f);
    public static void Beep() => Play(beep, 0.6f);
    public static void Go() => Play(go, 0.7f);

    static void Play(AudioClip c, float v) { if (src && c) src.PlayOneShot(c, v); }

    static float Saw(float ph) => 2f * (ph - Mathf.Floor(ph + 0.5f));
    static float Square(float ph) => (ph - Mathf.Floor(ph)) < 0.5f ? 1f : -1f;

    static AudioClip Make(string name, float len, System.Func<float, float[], float> wave, float[] freqs, float attack, float release)
    {
        const int rate = 44100;
        int n = (int)(rate * len);
        var data = new float[n]; var ph = new float[freqs.Length];
        float lp = 0f, peak = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            for (int k = 0; k < freqs.Length; k++) ph[k] += freqs[k] * (1f + 0.003f * Mathf.Sin(t * 30f)) / rate;
            float env = Mathf.Clamp01(t / attack) * Mathf.Clamp01((len - t) / release);
            float s = wave(t, ph) * env;
            lp += (s - lp) * 0.25f;               // soften the edges of the raw waveforms
            data[i] = lp; peak = Mathf.Max(peak, Mathf.Abs(lp));
        }
        float g = peak > 0 ? 0.9f / peak : 1f;
        for (int i = 0; i < n; i++) data[i] *= g;
        var clip = AudioClip.Create(name, n, 1, rate, false); clip.SetData(data, 0);
        return clip;
    }
}
