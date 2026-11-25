using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Fills the stand terraces with fans (GPU-instanced baked meshes), makes the scoring team's
// fans jump and cheer on goals, and plays a synthesized crowd murmur / roar.
public class CrowdSpawner : MonoBehaviour
{
    public Mesh idleMesh, cheerMesh;
    public Material redMat, blueMat, greyMat;
    public Collider stands;                       // terrace geometry (bancadatop)
    public float[] terraceHeights = { 150f, 400f, 700f };
    public float spacing = 115f;
    public float fanScale = 110f;                 // 1.83 m figure -> ~200 units
    public Vector2 rinkMinMaxX = new Vector2(-300f, 1550f);
    public float rinkCenterZ = 400f, rinkSplitX = 634f;
    public float cheerSeconds = 3.5f;
    [Range(0, 1)] public float crowdVolume = 0.55f;

    struct Fan { public Vector3 pos; public Quaternion rot; public float scale, phase; public int team; } // 0 red 1 blue 2 grey
    readonly List<Fan> fans = new List<Fan>();
    readonly Matrix4x4[] buf = new Matrix4x4[1023];
    int lastRed, lastBlue;
    float cheerUntil = -1f; int cheeringTeam = -1;
    AudioSource murmur, roar;

    public int FanCount => fans.Count;
    public int CheeringTeam => Time.time < cheerUntil ? cheeringTeam : -1;

    void Start()
    {
        Spawn();
        lastRed = marcadorverm.pontosverm; lastBlue = marcadorazul.pontosazul;
        SetupAudio();
    }

    void Spawn()
    {
        fans.Clear();
        if (stands == null) return;
        var b = stands.bounds;
        var rng = new System.Random(1234);
        for (float x = b.min.x; x <= b.max.x; x += spacing)
            for (float z = b.min.z; z <= b.max.z; z += spacing)
            {
                float jx = x + (float)(rng.NextDouble() - 0.5) * spacing * 0.35f;
                float jz = z + (float)(rng.NextDouble() - 0.5) * spacing * 0.35f;
                if (!stands.Raycast(new Ray(new Vector3(jx, b.max.y + 100f, jz), Vector3.down), out var hit, b.size.y + 200f)) continue;
                if (hit.normal.y < 0.95f || !OnTerrace(hit.point.y)) continue;
                // Keep a little clearance from the riser behind (toward the outside).
                var toRink = new Vector3(Mathf.Clamp(hit.point.x, rinkMinMaxX.x, rinkMinMaxX.y) - hit.point.x, 0, rinkCenterZ - hit.point.z);
                if (stands.Raycast(new Ray(hit.point + Vector3.up * 60f - toRink.normalized * 1f, -toRink.normalized), out _, 70f)) continue;
                var redSide = hit.point.x > rinkSplitX;
                double r = rng.NextDouble();
                int team = r < 0.12 ? 2 : (r < 0.80 ? (redSide ? 0 : 1) : (redSide ? 1 : 0));
                fans.Add(new Fan
                {
                    pos = hit.point,
                    rot = Quaternion.LookRotation(toRink.sqrMagnitude > 1 ? toRink : Vector3.forward) * Quaternion.Euler(0, (float)(rng.NextDouble() - 0.5) * 30f, 0),
                    scale = fanScale * (0.9f + (float)rng.NextDouble() * 0.2f),
                    phase = (float)rng.NextDouble() * 10f,
                    team = team
                });
            }
    }

    bool OnTerrace(float y)
    {
        foreach (var h in terraceHeights) if (Mathf.Abs(y - h) < 20f) return true;
        return false;
    }

    void Update()
    {
        // Goal detection from the existing score counters.
        if (marcadorverm.pontosverm > lastRed) Cheer(0);
        if (marcadorazul.pontosazul > lastBlue) Cheer(1);
        lastRed = marcadorverm.pontosverm; lastBlue = marcadorazul.pontosazul;

        int cheering = CheeringTeam;
        float t = Time.time;
        for (int team = 0; team < 3; team++)
        {
            var mat = team == 0 ? redMat : team == 1 ? blueMat : greyMat;
            DrawGroup(team, mat, cheering, t, cheerPose: false);
            if (cheering >= 0) DrawGroup(team, mat, cheering, t, cheerPose: true);
        }

        if (murmur) murmur.volume = crowdVolume * 0.5f;
    }

    void DrawGroup(int team, Material mat, int cheering, float t, bool cheerPose)
    {
        if (mat == null) return;
        var mesh = cheerPose ? cheerMesh : idleMesh;
        var rp = new RenderParams(mat) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true, layer = gameObject.layer };
        int n = 0;
        foreach (var f in fans)
        {
            if (f.team != team) continue;
            bool isCheering = cheering >= 0 && (f.team == cheering || (f.team == 2 && Mathf.Repeat(f.phase, 2f) < 1f));
            if (isCheering != cheerPose) continue;
            float y = isCheering ? Mathf.Abs(Mathf.Sin(t * 8f + f.phase)) * f.scale * 0.35f
                                 : Mathf.Sin(t * 1.3f + f.phase) * f.scale * 0.015f;
            float sway = isCheering ? 0f : Mathf.Sin(t * 0.9f + f.phase * 1.7f) * 4f;
            buf[n++] = Matrix4x4.TRS(f.pos + Vector3.up * y, f.rot * Quaternion.Euler(0, 0, sway), Vector3.one * f.scale);
            if (n == buf.Length) { Graphics.RenderMeshInstanced(rp, mesh, 0, buf, n); n = 0; }
        }
        if (n > 0) Graphics.RenderMeshInstanced(rp, mesh, 0, buf, n);
    }

    public void Cheer(int team)
    {
        cheeringTeam = team; cheerUntil = Time.time + cheerSeconds;
        if (roar) { roar.volume = crowdVolume; roar.Play(); }
    }

    // ---------- synthesized crowd audio ----------
    void SetupAudio()
    {
        murmur = gameObject.AddComponent<AudioSource>();
        murmur.clip = Noise("crowd_murmur", 8f, 0.25f, 0.06f, loopable: true);
        murmur.loop = true; murmur.spatialBlend = 0f; murmur.volume = crowdVolume * 0.5f; murmur.Play();
        roar = gameObject.AddComponent<AudioSource>();
        roar.clip = Noise("crowd_roar", cheerSeconds, 0.55f, 0.18f, loopable: false);
        roar.spatialBlend = 0f; roar.playOnAwake = false;
    }

    // Many voices ~ band-limited noise with slow, irregular swells.
    static AudioClip Noise(string name, float seconds, float brightness, float swell, bool loopable)
    {
        const int rate = 44100;
        int len = (int)(rate * seconds);
        var d = new float[len];
        var rng = new System.Random(name.GetHashCode());
        float lp1 = 0, lp2 = 0, hp = 0, prev = 0, peak = 0;
        for (int i = 0; i < len; i++)
        {
            float t = i / (float)rate, u = t / seconds;
            float w = (float)(rng.NextDouble() * 2 - 1);
            lp1 += (w - lp1) * brightness; lp2 += (lp1 - lp2) * brightness;     // low-pass: "voice" band upper limit
            hp = lp2 - prev; prev = lp2;                                           // gentle high-pass: remove rumble
            float s = lp2 * 0.7f + hp * 1.5f;
            float env = 1f + swell * (Mathf.Sin(t * 1.7f) + 0.6f * Mathf.Sin(t * 3.1f + 1f) + 0.4f * Mathf.Sin(t * 0.6f + 2f));
            if (!loopable) env *= Mathf.Clamp01(u * 6f) * Mathf.Pow(1f - u, 1.4f) * 1.8f; // roar: fast rise, slow fade
            s *= env;
            d[i] = s; peak = Mathf.Max(peak, Mathf.Abs(s));
        }
        if (loopable) // crossfade the ends so the loop is seamless
        {
            int fade = rate / 2;
            for (int i = 0; i < fade; i++) { float a = i / (float)fade; d[i] = d[i] * a + d[len - fade + i] * (1 - a); }
            System.Array.Resize(ref d, len - fade); len -= fade;
        }
        float g = peak > 0 ? 0.8f / peak : 1f;
        for (int i = 0; i < len; i++) d[i] *= g;
        var clip = AudioClip.Create(name, len, 1, rate, false);
        clip.SetData(d, 0);
        return clip;
    }
}
