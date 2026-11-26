using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Robot roster: specs load, loadouts change the robot, each tool does its job, 3x3 weight budget.
public class RobotsTest
{
    static GameObject Find(string name) => Resources.FindObjectsOfTypeAll<GameObject>().First(g => g.name == name && g.scene.IsValid());
    static System.Type T(string n) => System.Type.GetType(n + ", Assembly-CSharp");
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    object mm;
    void Call(string m, params object[] a) => mm.GetType().GetMethod(m, All).Invoke(mm, a);
    string State => mm.GetType().GetProperty("state").GetValue(mm).ToString();
    static object Get(object o, string p) => o.GetType().GetProperty(p, All)?.GetValue(o) ?? o.GetType().GetField(p, All).GetValue(o);

    static void Cfg(string field, object value)
    {
        var f = T("GameConfig").GetField(field, All);
        f.SetValue(null, value is string s && f.FieldType.IsEnum ? System.Enum.Parse(f.FieldType, s) : value);
    }
    static string[] Picks(bool blue) => (string[])T("GameConfig").GetField(blue ? "blueRobots" : "redRobots").GetValue(null);

    IEnumerator Load(string players = "Two", string format = "OneVsOne")
    {
        Time.timeScale = 1f;
        Cfg("format", format); Cfg("players", players); Cfg("kind", "QuickMatch"); Cfg("arena", 0);
        Cfg("blueRobots", new[] { "omega", "faisca", "trator" }); Cfg("redRobots", new[] { "omega", "canhao", "ima" });
        SceneManager.LoadScene("MENU");
        yield return null; yield return null;
        mm = T("MatchManager").GetProperty("Instance").GetValue(null);
    }

    IEnumerator StartAndPlay()
    {
        Call("StartMatch");
        float t = 0; while (State != "Playing" && t < 5) { t += Time.deltaTime; yield return null; }
        Assert.AreEqual("Playing", State);
    }

    Component Tools(GameObject r) => r.GetComponent(T("RobotTools"));
    void Trigger(GameObject r) { var c = Tools(r); c.GetType().GetMethod("Trigger").Invoke(c, null); }

    // Puck at the robot's cradle spot (the pocket between the arms).
    static void PuckInPocket(GameObject robot, Rigidbody prb)
    {
        var rb = robot.GetComponent<Rigidbody>();
        var spot = rb.position + rb.rotation * Vector3.Scale(new Vector3(0, 0.02f, 0), robot.transform.lossyScale);
        prb.position = new Vector3(spot.x, prb.position.y, spot.z); prb.linearVelocity = Vector3.zero;
    }

    [UnityTest]
    public IEnumerator CatalogHasRobotsCoveringEveryTool()
    {
        yield return Load();
        var all = (System.Array)T("RobotCatalog").GetProperty("All").GetValue(null);
        Assert.GreaterOrEqual(all.Length, 5);
        var tools = all.Cast<object>().Select(r => Get(r, "tool").ToString()).ToList();
        foreach (var t in new[] { "Claw", "Booster", "Plow", "Kicker", "Magnets" }) Assert.Contains(t, tools, "robot with " + t);
        Assert.AreEqual(all.Length, all.Cast<object>().Select(r => (string)Get(r, "id")).Distinct().Count(), "unique ids");
    }

    [UnityTest]
    public IEnumerator LoadoutChangesMassSizeStatsAndTool()
    {
        yield return Load();
        var blue = Find("Unidadeazul"); var rb = blue.GetComponent<Rigidbody>(); var m = blue.GetComponent(T("moverp1"));
        Picks(true)[0] = "trator"; Call("ApplyLoadouts"); yield return null;
        Assert.AreEqual(13f, rb.mass, 0.01f);
        Assert.AreEqual(125f, blue.transform.lossyScale.x, 0.5f);
        Assert.AreEqual(0.8f, (float)Get(m, "speedMul"), 0.001f);
        var blade = blue.transform.Find("Loadout/Blade");
        Assert.IsNotNull(blade, "plow blade"); Assert.IsNotNull(blade.GetComponent<Collider>(), "blade is solid");

        Picks(true)[0] = "omega"; Call("ApplyLoadouts"); yield return null;
        Assert.AreEqual(8f, rb.mass, 0.01f);
        Assert.AreEqual(100f, blue.transform.lossyScale.x, 0.5f);
        Assert.IsNull(blue.transform.Find("Loadout/Blade"), "old tool removed");
        Assert.IsNotNull(blue.transform.Find("Loadout/Finger"), "claw fingers");
    }

    [UnityTest]
    public IEnumerator KickerShootsThePuckInItsPocket()
    {
        yield return Load();
        Picks(true)[0] = "canhao";
        yield return StartAndPlay();
        var blue = Find("Unidadeazul"); var prb = Find("PUCK").GetComponent<Rigidbody>();
        PuckInPocket(blue, prb);
        yield return new WaitForFixedUpdate();
        Trigger(blue);
        yield return new WaitForFixedUpdate();
        var fwd = -blue.transform.up; fwd.y = 0; fwd.Normalize();
        var v = prb.linearVelocity; v.y = 0;
        Debug.Log($"PROBE kick: puck speed {v.magnitude:F0} along nose {Vector3.Dot(v.normalized, fwd):F2}");
        Assert.Greater(v.magnitude, 1500f, "kick speed");
        Assert.Greater(Vector3.Dot(v.normalized, fwd), 0.9f, "kicked out of the nose");
        Assert.AreEqual(1, (int)Get(Tools(blue), "Uses"));
        Trigger(blue);
        Assert.AreEqual(1, (int)Get(Tools(blue), "Uses"), "kicker cooldown");
    }

    [UnityTest]
    public IEnumerator BoosterGivesASpeedBurst()
    {
        yield return Load();
        Picks(true)[0] = "faisca";
        yield return StartAndPlay();
        var blue = Find("Unidadeazul"); var rb = blue.GetComponent<Rigidbody>(); var m = blue.GetComponent(T("moverp1"));
        Trigger(blue);
        Assert.IsTrue((bool)Get(m, "Boosting"));
        rb.position = new Vector3(rb.position.x, rb.position.y, 150f);   // clear lane (the puck sits at center)
        float peak = 0;
        for (int i = 0; i < 30; i++) { yield return new WaitForFixedUpdate(); var v = rb.linearVelocity; v.y = 0; peak = Mathf.Max(peak, v.magnitude); }
        float top = (float)T("mostradado").GetField("speed").GetValue(null) * 1.25f;
        Debug.Log($"PROBE boost: peak {peak:F0} (normal top {top:F0})");
        Assert.Greater(peak, top * 1.2f, "faster than its normal top speed");
        yield return new WaitForSeconds(0.4f);
        Assert.IsFalse((bool)Get(m, "Boosting"), "burst ends");
    }

    [UnityTest]
    public IEnumerator ClawCarriesThePuckAndShootsIt()
    {
        yield return Load();
        Picks(true)[0] = "omega";
        yield return StartAndPlay();
        var blue = Find("Unidadeazul"); var rb = blue.GetComponent<Rigidbody>(); var prb = Find("PUCK").GetComponent<Rigidbody>();
        ((Behaviour)blue.GetComponent(T("moverp1"))).enabled = false;   // stand in for a player driving straight
        rb.position = new Vector3(200, rb.position.y, 400);
        yield return new WaitForFixedUpdate();
        PuckInPocket(blue, prb);
        var fwd = -blue.transform.up; fwd.y = 0; fwd.Normalize();
        for (int i = 0; i < 50; i++) { rb.linearVelocity = fwd * 700f; yield return new WaitForFixedUpdate(); }
        var rel = prb.position - rb.position; rel.y = 0;
        Debug.Log($"PROBE claw: puck {rel.magnitude:F0} from robot center after 1 s at 700, holding={Get(Tools(blue), "Holding")}");
        Assert.IsTrue((bool)Get(Tools(blue), "Holding"), "puck held");
        Assert.Less(rel.magnitude, 80f, "puck carried in the pocket");
        Trigger(blue);
        yield return new WaitForFixedUpdate();
        var v = prb.linearVelocity; v.y = 0;
        Assert.Greater(Vector3.Dot(v, fwd), 1300f, "claw shot");
        Assert.IsFalse((bool)Get(Tools(blue), "Holding"));
    }

    [UnityTest]
    public IEnumerator HeavyTeamOverBudgetCannotStart()
    {
        yield return Load("One", "ThreeVsThree");
        Cfg("blueRobots", new[] { "trator", "trator", "trator" });
        Assert.IsFalse((bool)T("GameConfig").GetMethod("WithinBudget").Invoke(null, new object[] { true }));
        Find("1 JOGADOR Button").GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); yield return null;
        Find("JOGAR Button").GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); yield return null;
        Assert.IsTrue(Find("Robots").activeInHierarchy, "robot screen");
        var start = Find("COMEÇAR Button").GetComponent<UnityEngine.UI.Button>();
        Assert.IsFalse(start.interactable, "start disabled over budget");
        start.onClick.Invoke(); yield return null;
        Assert.AreEqual("Menu", State, "match did not start");
        // Lighter teammates (13 + 6 + 6 = 25 kg); the slot buttons (› then ‹) refresh the screen.
        Picks(true)[1] = "faisca"; Picks(true)[2] = "faisca";
        var slot = Find("Azul 1").GetComponentsInChildren<UnityEngine.UI.Button>();
        slot[1].onClick.Invoke(); slot[0].onClick.Invoke(); yield return null;
        Assert.AreEqual("trator", Picks(true)[0]);
        Assert.IsTrue(start.interactable, "within budget");
        yield return new WaitForSeconds(0.3f);
        var dir = System.Environment.GetEnvironmentVariable("PROBE_DIR");
        if (dir != null) { yield return new WaitForEndOfFrame(); System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "ui_robots.png"), ScreenCapture.CaptureScreenshotAsTexture().EncodeToPNG()); }
        start.onClick.Invoke(); yield return null;
        Assert.AreNotEqual("Menu", State, "match started");
        Assert.AreEqual(13f, Find("Unidadeazul").GetComponent<Rigidbody>().mass, 0.01f);
    }

    [UnityTest]
    public IEnumerator AIUsesItsKickerAndScores()
    {
        yield return Load("One");
        Cfg("difficulty", "Medium"); Cfg("goalsPerPeriod", 5); Cfg("minutesPerPeriod", 5);
        Picks(false)[0] = "canhao";
        yield return StartAndPlay();
        var red = Find("unidadeverm"); var blue = Find("Unidadeazul");
        ((Behaviour)blue.GetComponent(T("moverp1"))).enabled = false;
        var brb = blue.GetComponent<Rigidbody>(); brb.position = new Vector3(600, brb.position.y, 850);
        Time.timeScale = 3f;
        float t = 0; int score() => (int)T("marcadorverm").GetField("pontosverm").GetValue(null);
        while (score() == 0 && t < 90f) { t += Time.deltaTime; yield return null; }
        Time.timeScale = 1f;
        Debug.Log($"PROBE AI kicker: scored after {t:F1}s, kicks {Get(Tools(red), "Uses")}");
        Assert.Greater(score(), 0, "AI with a kicker scores");
        Assert.Greater((int)Get(Tools(red), "Uses"), 0, "AI used the kicker");
    }
}
