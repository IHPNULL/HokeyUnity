using System.Collections.Generic;
using UnityEngine;

// Metal impact sounds for robots and the puck, synthesized at startup (no audio assets).
// Added automatically to every robot (moverp1) and puck when a scene loads.
[RequireComponent(typeof(AudioSource))]
public class ImpactSound : MonoBehaviour
{
    public enum Kind { Robot, Puck }
    public Kind kind;
    public static float Volume = 0.8f;

    const float MinSpeed = 120f;   // world units/s (arena is ~2600 units long)
    const float FullSpeed = 1500f;
    const float Cooldown = 0.07f;

    AudioSource src;
    float lastTime = -1f;

    static AudioClip[] heavyClang, clank, clink;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        Attach();
    }

    static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => Attach();

    static void Attach()
    {
        foreach (var r in FindObjectsByType<moverp1>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Add(r.gameObject, Kind.Robot);
        foreach (var p in FindObjectsByType<puck>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Add(p.gameObject, Kind.Puck);
    }

    static void Add(GameObject go, Kind k)
    {
        if (go.GetComponent<ImpactSound>()) return;
        if (!go.TryGetComponent<AudioSource>(out var a)) a = go.AddComponent<AudioSource>();
        a.playOnAwake = false; a.spatialBlend = 0f;
        go.AddComponent<ImpactSound>().kind = k;
    }

    void Awake()
    {
        src = GetComponent<AudioSource>();
        if (heavyClang == null) BuildClips();
    }

    void OnCollisionEnter(Collision c)
    {
        // Floor contacts (robot driving, puck landing) are not impacts.
        if (c.contactCount > 0 && c.GetContact(0).normal.y > 0.7f) return;
        float speed = c.relativeVelocity.magnitude;
        if (speed < MinSpeed || Time.time - lastTime < Cooldown) return;

        // When both bodies have an ImpactSound, only one of them plays it.
        var other = c.collider.attachedRigidbody ? c.collider.attachedRigidbody.GetComponent<ImpactSound>() : null;
        if (other != null && other.GetInstanceID() < GetInstanceID()) return;

        AudioClip[] set;
        bool otherIsRobot = other != null && other.kind == Kind.Robot;
        if (kind == Kind.Robot && otherIsRobot) set = heavyClang;
        else if (kind == Kind.Puck || (other != null && other.kind == Kind.Puck)) set = clink;
        else set = clank;

        float t = Mathf.InverseLerp(MinSpeed, FullSpeed, speed);
        src.pitch = Random.Range(0.92f, 1.08f) * Mathf.Lerp(0.95f, 1.05f, t);
        src.PlayOneShot(set[Random.Range(0, set.Length)], Volume * Mathf.Lerp(0.25f, 1f, t));
        lastTime = Time.time;
        LastPlayed = set == heavyClang ? "clang" : set == clink ? "clink" : "clank";
        NetSession.SendEvent(NetSession.Ev.Impact, "", default, (byte)(set == heavyClang ? 0 : set == clink ? 2 : 1), Volume * Mathf.Lerp(0.25f, 1f, t));
        LastPair = name + "/" + c.GetContact(0).thisCollider.name + " <-> " + c.collider.name + " n=" + c.GetContact(0).normal.ToString("F2") + " v=" + speed.ToString("F0");
        PlayCount++;
    }

    // Online joiner: the host's impacts (0 clang, 1 clank, 2 clink).
    static AudioSource remote;
    public static void PlayRemote(byte kind, float volume)
    {
        if (heavyClang == null) BuildClips();
        if (remote == null) { var go = new GameObject("Remote Impacts"); remote = go.AddComponent<AudioSource>(); remote.playOnAwake = false; remote.spatialBlend = 0f; }
        var set = kind == 0 ? heavyClang : kind == 2 ? clink : clank;
        remote.pitch = Random.Range(0.92f, 1.08f);
        remote.PlayOneShot(set[Random.Range(0, set.Length)], volume);
    }

    // For tests.
    public static string LastPlayed;
    public static int PlayCount;
    public static string LastPair;

    static void BuildClips()
    {
        // Inharmonic partial ratios of a free metal bar give the "metallic" character.
        float[] bar = { 1f, 2.756f, 5.404f, 8.933f };
        heavyClang = new[] { Synth("clang1", 170f, bar, 0.9f, 0.55f), Synth("clang2", 205f, bar, 0.8f, 0.55f), Synth("clang3", 145f, bar, 1.0f, 0.6f) };
        clank = new[] { Synth("clank1", 340f, bar, 0.45f, 0.4f), Synth("clank2", 410f, bar, 0.4f, 0.4f), Synth("clank3", 290f, bar, 0.5f, 0.45f) };
        clink = new[] { Synth("clink1", 1150f, bar, 0.22f, 0.25f), Synth("clink2", 1320f, bar, 0.2f, 0.25f), Synth("clink3", 980f, bar, 0.25f, 0.3f) };
    }

    static AudioClip Synth(string name, float f0, float[] ratios, float decay, float noiseMix)
    {
        const int rate = 44100;
        int len = (int)(rate * (decay * 2.2f + 0.05f));
        var data = new float[len];
        var rng = new System.Random(name.GetHashCode());
        var amps = new List<float>(); var phases = new List<float>();
        for (int i = 0; i < ratios.Length; i++) { amps.Add(1f / (1f + i * 0.9f)); phases.Add((float)rng.NextDouble() * 6.283f); }
        float peak = 0f, lp = 0f;
        for (int n = 0; n < len; n++)
        {
            float t = n / (float)rate, s = 0f;
            for (int i = 0; i < ratios.Length; i++)
            {
                float f = f0 * ratios[i] * (1f + 0.002f * i);        // slight detune
                float env = Mathf.Exp(-t / (decay / (1f + i * 0.8f))); // higher partials die faster
                s += amps[i] * env * Mathf.Sin(6.2832f * f * t + phases[i]);
            }
            // Strike transient: short filtered noise burst.
            float noise = (float)(rng.NextDouble() * 2 - 1);
            lp += (noise - lp) * 0.35f;
            s += noiseMix * lp * Mathf.Exp(-t / 0.012f) * 3f;
            data[n] = s; peak = Mathf.Max(peak, Mathf.Abs(s));
        }
        float g = peak > 0 ? 0.9f / peak : 1f;
        for (int n = 0; n < len; n++) data[n] *= g;
        var clip = AudioClip.Create(name, len, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
