using UnityEngine;

// Shared particle effects, built in code once per scene: crash sparks, skid spray, kicker flash.
// Particle systems only emit on request (no continuous emission).
public static class FxPool
{
    static ParticleSystem sparks, spray, flash;
    static Material additive, alpha;
    static Texture2D dot;

    public static int SparkCount => sparks ? sparks.particleCount : 0;   // tests
    public static int SprayCount => spray ? spray.particleCount : 0;

    public static Material Additive { get { Ensure(); return additive; } }

    static void Ensure()
    {
        if (sparks && spray && flash) return;
        if (dot == null) dot = SoftDot();
        if (additive == null) additive = Mat("Fx/FxAdditive");
        if (alpha == null) alpha = Mat("Fx/FxAlpha");
        var root = GameObject.Find("Fx Pool") ?? new GameObject("Fx Pool");
        if (!sparks) sparks = Make(root.transform, "Sparks", additive, 600, 0.12f, true);
        if (!spray) spray = Make(root.transform, "Spray", alpha, 600, 0.02f, false);
        if (!flash) flash = Make(root.transform, "Flash", additive, 32, 0f, false);
    }

    static Material Mat(string res)
    {
        var src = Resources.Load<Material>(res);
        var m = src ? new Material(src) : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        m.SetTexture("_BaseMap", dot);
        return m;
    }

    static ParticleSystem Make(Transform root, string name, Material m, int max, float gravity, bool stretch)
    {
        var go = new GameObject(name); go.transform.SetParent(root, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false; main.playOnAwake = false; main.maxParticles = max;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = gravity;            // Physics.gravity is 5000 here
        main.startSpeed = 0f; main.startLifetime = 0.5f;
        var em = ps.emission; em.enabled = false;
        var shape = ps.shape; shape.enabled = false;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                  new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
        col.color = g;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = m;
        r.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
        if (stretch) { r.velocityScale = 0.035f; r.lengthScale = 1f; }
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
        return ps;
    }

    // Crash sparks at a contact point; strength 0..1.
    public static void Sparks(Vector3 at, Vector3 normal, float strength)
    {
        Ensure();
        int n = Mathf.RoundToInt(Mathf.Lerp(8, 40, strength));
        var ep = new ParticleSystem.EmitParams();
        for (int i = 0; i < n; i++)
        {
            var dir = (normal.normalized * 0.6f + Random.onUnitSphere + Vector3.up * 0.5f).normalized;
            ep.position = at; ep.velocity = dir * Random.Range(400f, 1300f) * (0.5f + strength);
            ep.startLifetime = Random.Range(0.2f, 0.5f); ep.startSize = Random.Range(4f, 9f);
            ep.startColor = Color.Lerp(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.45f, 0.1f), Random.value);
            sparks.Emit(ep, 1);
        }
    }

    // Skid spray / dust puffs.
    public static void Spray(Vector3 at, Vector3 away, Color color, int n)
    {
        Ensure();
        var ep = new ParticleSystem.EmitParams();
        for (int i = 0; i < n; i++)
        {
            ep.position = at + Random.insideUnitSphere * 10f;
            ep.velocity = (away.normalized * Random.Range(80f, 260f)) + Vector3.up * Random.Range(40f, 140f) + Random.insideUnitSphere * 50f;
            ep.startLifetime = Random.Range(0.35f, 0.8f); ep.startSize = Random.Range(18f, 40f);
            ep.startColor = color;
            spray.Emit(ep, 1);
        }
    }

    // A short bright flash (kicker).
    public static void Flash(Vector3 at, Color color)
    {
        Ensure();
        var ep = new ParticleSystem.EmitParams { position = at, startLifetime = 0.12f, startSize = 140f, startColor = color, velocity = Vector3.zero };
        flash.Emit(ep, 1);
        Sparks(at, Vector3.up, 0.3f);
    }

    static Texture2D SoftDot()
    {
        const int N = 32;
        var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Soft dot" };
        var px = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(N / 2f, N / 2f)) / (N / 2f);
                float a = Mathf.Clamp01(1f - d); a *= a;
                px[y * N + x] = new Color(1, 1, 1, a);
            }
        t.SetPixels(px); t.Apply();
        return t;
    }
}
