using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Bigger rinks (1x1 and the bigger 3x3) with goals, pillars and markings at their real size.
public class ArenaSizeTest
{
    static GameObject Find(string name) => Resources.FindObjectsOfTypeAll<GameObject>().First(g => g.name == name && g.scene.IsValid());
    static System.Type T(string n) => System.Type.GetType(n + ", Assembly-CSharp");
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static Vector3 W(float x, float z, float y = 0f) => (Vector3)T("Rink").GetMethod("W").Invoke(null, new object[] { x, z, y });
    static Vector2 Scale => (Vector2)T("Rink").GetField("Scale").GetValue(null);
    static int Score(string type, string field) => (int)T(type).GetField(field).GetValue(null);

    object mm;
    void Call(string m, params object[] a) => mm.GetType().GetMethod(m, All).Invoke(mm, a);

    IEnumerator Load(string format)
    {
        Time.timeScale = 1f;
        var f = T("GameConfig").GetField("format"); f.SetValue(null, System.Enum.Parse(f.FieldType, format));
        T("GameConfig").GetField("arena").SetValue(null, 0);
        SceneManager.LoadScene("MENU");
        yield return null; yield return null;
        mm = T("MatchManager").GetProperty("Instance").GetValue(null);
        Find("unidades").SetActive(false);   // keep robots out of the shot lanes
        yield return new WaitForSeconds(1f); // puck settles
    }

    [UnityTest]
    public IEnumerator RinkGrowsGoalsKeepTheirSize([Values("OneVsOne", "ThreeVsThree")] string format)
    {
        yield return Load(format);
        bool big = format == "ThreeVsThree";
        Assert.Greater(Scale.x, big ? 1.5f : 1.2f, "rink is stretched");
        foreach (var n in new[] { "gol Visual", "gol (1) Visual" })
        {
            var vis = Find(n).transform;
            Assert.AreEqual(1f, vis.lossyScale.x, 0.01f, n + " keeps its length");
            Assert.AreEqual(1f, vis.lossyScale.z, 0.01f, n + " keeps its width");
            var trig = Find(n.Replace(" Visual", "")).GetComponent<BoxCollider>();
            Assert.AreEqual(vis, trig.transform.parent, "scoring trigger moved under its goal");
            var netBounds = new Bounds(vis.position, Vector3.zero);
            foreach (var r in vis.GetComponentsInChildren<Renderer>()) netBounds.Encapsulate(r.bounds);
            Assert.IsTrue(netBounds.Contains(trig.bounds.center), "trigger inside the net");
        }
        Assert.AreEqual(((Vector3)T("Rink").GetProperty("RedGoal").GetValue(null)).x, Find("gol Visual").transform.position.x, 1f, "goal at the stretched goal line");
        var postL = Find("gol Visual").transform.Find("Post L"); var postR = Find("gol Visual").transform.Find("Post R");
        Debug.Log($"PROBE arena {format}: scale {Scale} goal mouth x {Find("gol Visual").transform.position.x:F0} post gap {Mathf.Abs(postL.position.z - postR.position.z):F0}");
        Assert.AreEqual(1f, postL.lossyScale.x / postL.lossyScale.z * (postR.lossyScale.z / postR.lossyScale.x), 0.01f, "posts stay round");

        // Pillars (Fábrica) stay round and spread out.
        T("ArenaTheme").GetMethod("Apply").Invoke(null, new object[] { 2 }); yield return null;
        foreach (Transform p in Find("Fabrica Pillars").transform)
        {
            Assert.AreEqual(80f, p.lossyScale.x, 0.5f, "pillar width"); Assert.AreEqual(80f, p.lossyScale.z, 0.5f, "pillar depth");
        }
        T("ArenaTheme").GetMethod("Apply").Invoke(null, new object[] { 1 }); yield return null;
        var circle = Find("Center Circle").transform;
        Assert.AreEqual(circle.lossyScale.x, circle.lossyScale.z, 0.01f, "center circle stays round");
        T("ArenaTheme").GetMethod("Apply").Invoke(null, new object[] { 0 }); yield return null;

        // Rounded boards generated for this size; the old stretched boards are retired.
        float radius = (float)T("RinkBuilder").GetProperty("CornerRadius").GetValue(null);
        Debug.Log($"PROBE arena {format}: corner radius {radius:F0}");
        Assert.Greater(radius, big ? 420f : 300f, "round corners");
        Assert.IsTrue(Find("Glass").GetComponent<MeshCollider>().enabled, "rounded boards are solid");
        Assert.IsFalse(Find("policarbonato").GetComponent<Collider>().enabled, "old boards retired");

        // A puck sent into a corner comes back off the curve.
        {
            var pc = Find("PUCK").GetComponent<Rigidbody>();
            pc.position = W(634, 400, pc.position.y); pc.linearVelocity = Vector3.zero;
            yield return new WaitForFixedUpdate();
            pc.linearVelocity = new Vector3(1300, 0, 1300) * 0.7f;   // toward the far +x/+z corner
            int o0 = (int)T("puck").GetField("OutOfPlayCount").GetValue(null);
            yield return new WaitForSeconds(2f);
            Assert.AreEqual(o0, (int)T("puck").GetField("OutOfPlayCount").GetValue(null), "corner shot stayed in play");
            Assert.Less(pc.position.z, W(0, 910).z, "puck inside the boards");
        }

        // Goals count from both ends, and a puck sliding along the end boards beside a goal stays in.
        var prb = Find("PUCK").GetComponent<Rigidbody>();
        int outBefore = (int)T("puck").GetField("OutOfPlayCount").GetValue(null);
        prb.position = W(1300, 400 + (big ? 300 : 250), prb.position.y); prb.linearVelocity = Vector3.zero;
        yield return new WaitForFixedUpdate();
        prb.linearVelocity = new Vector3(1300, 0, 0);
        yield return new WaitForSeconds(1.2f);
        Assert.AreEqual(outBefore, (int)T("puck").GetField("OutOfPlayCount").GetValue(null), "puck beside the goal stayed in play");

        foreach (var (dir, type, field) in new[] { (1f, "marcadorazul", "pontosazul"), (-1f, "marcadorverm", "pontosverm") })
        {
            prb.position = W(634, 400, prb.position.y); prb.linearVelocity = Vector3.zero;
            yield return new WaitForFixedUpdate();
            int before = Score(type, field);
            prb.linearVelocity = new Vector3(1600 * dir, 0, 0);
            float t = 0; while (Score(type, field) == before && t < 4f) { t += Time.deltaTime; yield return null; }
            Assert.AreEqual(before + 1, Score(type, field), (dir > 0 ? "gol" : "gol (1)") + " scored");
        }
        yield return new WaitForSeconds(0.3f);
        var dirp = System.Environment.GetEnvironmentVariable("PROBE_DIR");
        if (dirp != null)
        {
            // Overview straight down over the rink (menu hidden).
            Find("UI Canvas").SetActive(false);
            var cam = new GameObject("Probe Cam").AddComponent<Camera>();
            cam.transform.position = W(634, 400, 2600); cam.transform.rotation = Quaternion.Euler(90, 0, 0);
            cam.depth = 100; cam.fieldOfView = 75; cam.farClipPlane = 10000;
            yield return new WaitForEndOfFrame();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dirp, "arena_" + format + ".png"), ScreenCapture.CaptureScreenshotAsTexture().EncodeToPNG());
            Object.Destroy(cam.gameObject);
        }
    }
}
