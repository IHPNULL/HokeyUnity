using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Match flow: menu, best-of-3 periods, sudden death, 1P camera, AI opponent.
// Game code lives in Assembly-CSharp, which test assemblies can't reference: reflection below.
public class MatchTest
{
    static GameObject Find(string name) => Resources.FindObjectsOfTypeAll<GameObject>().First(g => g.name == name && g.scene.IsValid());
    static System.Type T(string n) => System.Type.GetType(n + ", Assembly-CSharp");
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static Vector3 W(float x, float z, float y = 0f) => (Vector3)System.Type.GetType("Rink, Assembly-CSharp").GetMethod("W").Invoke(null, new object[] { x, z, y });   // scene -> stretched rink
    static float Rk(string p) => (float)System.Type.GetType("Rink, Assembly-CSharp").GetProperty(p).GetValue(null);


    object mm;
    object P(string name) => mm.GetType().GetProperty(name, All).GetValue(mm);
    string State => P("state").ToString();
    int I(string name) => (int)P(name);
    void Call(string m, params object[] a) => mm.GetType().GetMethod(m, All).Invoke(mm, a);

    static void Cfg(string field, object value)
    {
        var t = T("GameConfig");
        var f = t.GetField(field, All);
        if (f != null) { f.SetValue(null, value is string s && f.FieldType.IsEnum ? System.Enum.Parse(f.FieldType, s) : value); return; }
        var p = t.GetProperty(field, All); p.SetValue(null, value);
    }
    static int Score(string type, string field) => (int)T(type).GetField(field).GetValue(null);
    static void AddGoal(string type, string field) => T(type).GetField(field).SetValue(null, Score(type, field) + 1);
    static void Blue() => AddGoal("marcadorazul", "pontosazul");
    static void Red() => AddGoal("marcadorverm", "pontosverm");

    IEnumerator Load()
    {
        Time.timeScale = 1f;
        Cfg("format", "OneVsOne");
        SceneManager.LoadScene("MENU");
        yield return null; yield return null;
        mm = T("MatchManager").GetProperty("Instance").GetValue(null);
        Assert.IsNotNull(mm, "MatchManager missing");
    }

    IEnumerator Shot(string name)
    {
        var dir = System.Environment.GetEnvironmentVariable("PROBE_DIR");
        if (dir == null) yield break;
        yield return new WaitForEndOfFrame();
        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
    }

    IEnumerator WaitState(string s, float max)
    {
        float t = 0; while (State != s && t < max) { t += Time.unscaledDeltaTime; yield return null; }
        Assert.AreEqual(s, State, "waiting for " + s);
    }

    [UnityTest]
    public IEnumerator MenuThenSetupScreens()
    {
        yield return Load();
        Assert.AreEqual("Menu", State);
        Assert.IsTrue(Find("Menu Camera").activeInHierarchy, "menu camera");
        Assert.IsTrue(Find("Title").activeInHierarchy, "title screen");
        yield return new WaitForSeconds(0.5f);
        yield return Shot("ui_title");
        Find("1 JOGADOR Button").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        yield return null;
        Assert.IsTrue(Find("Setup").activeInHierarchy, "setup screen");
        yield return Shot("ui_setup_1p");
        Find("Modo").GetComponentsInChildren<UnityEngine.UI.Button>()[1].onClick.Invoke(); // -> tournament
        yield return null;
        Assert.IsFalse(Find("Arena").activeInHierarchy, "arena is set by the ladder in a tournament");
        yield return Shot("ui_setup_tournament");
        Find("Modo").GetComponentsInChildren<UnityEngine.UI.Button>()[1].onClick.Invoke();
    }

    [UnityTest]
    public IEnumerator BestOfThreeWithSuddenDeath()
    {
        yield return Load();
        Cfg("players", "Two"); Cfg("kind", "QuickMatch"); Cfg("goalsPerPeriod", 2); Cfg("minutesPerPeriod", 1);
        Call("StartMatch");
        Assert.IsFalse(Find("Menu Camera").activeInHierarchy);
        yield return WaitState("Playing", 5);
        Assert.AreEqual(1, I("period"));
        yield return Shot("ui_hud_2p");

        // Period 1: blue reaches 2 goals.
        Blue(); yield return null;
        yield return WaitState("GoalPause", 1);
        yield return WaitState("Playing", 8);
        Blue(); yield return null;
        Assert.AreEqual(1, I("periodsBlue"), "blue won period 1");
        yield return WaitState("PeriodBreak", 1);
        yield return WaitState("Playing", 10);
        Assert.AreEqual(2, I("period"));
        Assert.AreEqual(0, I("GoalsBlue"), "period score resets");

        // Period 2: tied at the buzzer -> sudden death, red scores.
        Red(); yield return null;
        yield return WaitState("Playing", 8);
        Blue(); yield return null;
        yield return WaitState("Playing", 8);
        Call("SetClock", 0.001f);
        for (int i = 0; i < 4; i++) yield return null;
        Assert.IsTrue((bool)P("suddenDeath"), "tie at time -> sudden death");
        yield return WaitState("Playing", 8);
        Red(); yield return null;
        Assert.AreEqual(1, I("periodsRed"), "sudden-death goal wins the period");
        yield return WaitState("Playing", 10);
        Assert.AreEqual(3, I("period"));

        // Period 3: clock runs out with red ahead -> red wins the match 2-1.
        Red(); yield return null;
        yield return WaitState("Playing", 8);
        Call("SetClock", 0.001f);
        yield return WaitState("Result", 2);
        Assert.AreEqual("Red", P("matchWinner").ToString());
        Assert.AreEqual(2, I("periodsRed")); Assert.AreEqual(1, I("periodsBlue"));
        yield return new WaitForSeconds(0.2f);
        yield return Shot("ui_result");
        Call("QuitToMenu");
        Assert.AreEqual("Menu", State);
    }

    [UnityTest]
    public IEnumerator OnePlayerFullScreenAndAIScores([Values("Easy", "Medium", "Hard")] string diff, [Values("center", "ownCorner", "farCorner")] string spot, [Values(0, 1, 2)] int arena)
    {
        yield return Load();
        Cfg("players", "One"); Cfg("kind", "QuickMatch"); Cfg("difficulty", diff);
        Cfg("goalsPerPeriod", 5); Cfg("minutesPerPeriod", 5); Cfg("arena", arena);
        Call("StartMatch");
        var red = Find("unidadeverm"); var blue = Find("Unidadeazul");
        Assert.IsFalse(red.GetComponentInChildren<Camera>(true).gameObject.activeInHierarchy, "red camera off in 1P");
        Assert.AreEqual(new Rect(0, 0, 1, 1), blue.GetComponentInChildren<Camera>(true).rect, "blue camera full screen");
        yield return WaitState("Playing", 5);
        if (spot == "center" && diff == "Medium") yield return Shot("arena_" + arena);

        // Idle player parked out of the way; the AI must score.
        ((Behaviour)blue.GetComponent(T("moverp1"))).enabled = false;
        var brb = blue.GetComponent<Rigidbody>(); brb.position = W(600, 850, brb.position.y);
        var prb = Find("PUCK").GetComponent<Rigidbody>();
        if (spot == "ownCorner") prb.position = W(1500, 60, prb.position.y);     // deep in the AI's own end
        if (spot == "farCorner") prb.position = W(-250, 820, prb.position.y);    // corner beside the target goal
        prb.linearVelocity = Vector3.zero;
        Time.timeScale = 3f;
        var trace = new System.Text.StringBuilder();
        float t = 0, minX = 1e9f, maxX = -1e9f, minZ = 1e9f, maxZ = -1e9f;
        while (Score("marcadorverm", "pontosverm") == 0 && t < 90f)
        {
            t += Time.deltaTime;
            var p = red.transform.position;
            if ((int)(t * 2) != (int)((t - Time.deltaTime) * 2) && t < 40)
            {
                var ai = red.GetComponent(T("AIRobotInput"));
                var tg = (Vector3)ai.GetType().GetProperty("Target").GetValue(ai); var f = -red.transform.up; var rv = red.GetComponent<Rigidbody>().linearVelocity;
                trace.Append($" [{t:F1}s r({p.x:F0},{p.z:F0}) f({f.x:F1},{f.z:F1}) v({rv.x:F0},{rv.z:F0}) tg({tg.x:F0},{tg.z:F0}) d={ai.GetType().GetProperty("Drive").GetValue(ai):F2} p({prb.position.x:F0},{prb.position.z:F0}) {ai.GetType().GetProperty("Mode").GetValue(ai)}{(red.GetComponent(T("RobotTools")) is Component rt && (bool)rt.GetType().GetProperty("Holding").GetValue(rt) ? " H" : "")}]");
            }
            minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z);
            yield return null;
        }
        Time.timeScale = 1f;
        Debug.Log($"AI {diff} {spot} arena{arena}: scored={Score("marcadorverm", "pontosverm")} after {t:F1}s, x {minX:F0}..{maxX:F0} z {minZ:F0}..{maxZ:F0}, blue goals {Score("marcadorazul", "pontosazul")}" + (Score("marcadorverm", "pontosverm") == 0 || Score("marcadorazul", "pontosazul") > 0 ? trace.ToString() : ""));
        // A puck rebounding off the far post or boards can occasionally beat the AI back to its own net.
        Assert.LessOrEqual(Score("marcadorazul", "pontosazul"), 1, "AI own goals");   // a missed shot can rebound the length of the rink once in a while (the test player is idle)
        Assert.Greater(Score("marcadorverm", "pontosverm"), 0, "AI did not score within 90 s");
        // Inside the rink; it may skid into its own net mouth on ice (then the exit rule drives it out).
        Assert.Greater(minX, W(-450, 0).x); Assert.Less(maxX, W(1656, 0).x + 230f); Assert.Greater(minZ, W(0, -180).z); Assert.Less(maxZ, W(0, 980).z);
        Call("QuitToMenu");
    }

    GameObject[] Robots(string team) => (GameObject[])mm.GetType().GetMethod("TeamRobots").Invoke(mm, new[] { System.Enum.Parse(T("MatchManager+Team"), team) });

    [UnityTest]
    public IEnumerator ThreeVsThreeBigRinkSwitchAndPlay()
    {
        yield return Load();
        Cfg("players", "One"); Cfg("kind", "QuickMatch"); Cfg("difficulty", "Medium");
        Cfg("goalsPerPeriod", 5); Cfg("minutesPerPeriod", 5); Cfg("arena", 0); Cfg("format", "ThreeVsThree");
        Call("StartMatch");
        var reds = Robots("Red"); var blues = Robots("Blue");
        Assert.AreEqual(3, reds.Count(r => r.activeInHierarchy)); Assert.AreEqual(3, blues.Count(r => r.activeInHierarchy));
        Assert.Greater(Find("gol Visual").transform.position.x, 2200f, "3x3 rink is longer");
        Assert.Greater(Find("PUCK").transform.lossyScale.x, 59f); Assert.Less(Find("PUCK").transform.lossyScale.x, 61f);
        yield return WaitState("Playing", 5);
        yield return new WaitForSeconds(0.5f);
        yield return Shot("three_v_three");

        // Switch: the player takes another robot and its camera.
        var team = System.Enum.Parse(T("MatchManager+Team"), "Blue");
        Call("SwitchRobot", team);
        int h = (int)mm.GetType().GetMethod("HumanIndex").Invoke(mm, new[] { team });
        Assert.AreNotEqual(0, h, "switched robot");
        Assert.IsTrue(blues[h].GetComponentInChildren<Camera>(true).gameObject.activeInHierarchy, "camera follows the switch");
        Assert.IsFalse(blues[0].GetComponentInChildren<Camera>(true).gameObject.activeInHierarchy);

        // The player idles; five AIs play. Someone scores and nobody leaves the rink.
        Time.timeScale = 3f;
        float t = 0, minX = 1e9f, maxX = -1e9f, minZ = 1e9f, maxZ = -1e9f;
        var puckT = Find("PUCK").transform; var path = new System.Text.StringBuilder();
        while (Score("marcadorverm", "pontosverm") + Score("marcadorazul", "pontosazul") == 0 && t < 120f)
        {
            t += Time.deltaTime;
            if ((int)(t * 4) != (int)((t - Time.deltaTime) * 4)) path.Append($" {t:F1}:({puckT.position.x:F0},{puckT.position.z:F0})");
            foreach (var r in reds.Concat(blues)) { var p = r.transform.position; minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z); }
            yield return null;
        }
        Time.timeScale = 1f;
        Debug.Log($"3x3: red {Score("marcadorverm", "pontosverm")} blue {Score("marcadorazul", "pontosazul")} after {t:F1}s, x {minX:F0}..{maxX:F0} z {minZ:F0}..{maxZ:F0} puck{path}");
        Assert.Greater(Score("marcadorverm", "pontosverm") + Score("marcadorazul", "pontosazul"), 0, "no goal in 120 s of 3x3");
        Assert.Greater(minX, W(-400, 0).x - 230f); Assert.Less(maxX, W(1656, 0).x + 230f); Assert.Greater(minZ, W(0, -180).z); Assert.Less(maxZ, W(0, 980).z);   // rink incl. the nets

        Call("QuitToMenu");
        Cfg("format", "OneVsOne"); Call("ApplyFormat");
        Assert.IsFalse(reds[1].activeInHierarchy, "extra robots hidden in 1x1");
        Assert.Greater(Find("gol Visual").transform.position.x, 2200f, "1x1 also plays on the big rink");
    }

    [UnityTest]
    public IEnumerator TwoVsTwoOnBigRinkPlays()
    {
        yield return Load();
        Cfg("players", "One"); Cfg("kind", "QuickMatch"); Cfg("difficulty", "Medium");
        Cfg("goalsPerPeriod", 5); Cfg("minutesPerPeriod", 5); Cfg("arena", 0); Cfg("format", "TwoVsTwo");
        Call("StartMatch");
        var reds = Robots("Red"); var blues = Robots("Blue");
        Assert.AreEqual(2, reds.Count(r => r.activeInHierarchy), "two red robots"); Assert.AreEqual(2, blues.Count(r => r.activeInHierarchy), "two blue robots");
        Assert.Greater(Find("gol Visual").transform.position.x, 2200f, "2x2 plays on the big rink");
        Assert.Greater(Mathf.Abs(reds[0].transform.position.z - reds[1].transform.position.z), 100f, "teammates start side by side");
        Assert.IsTrue(Find("placar").activeInHierarchy, "scoreboard shown on the big rink");
        yield return WaitState("Playing", 5);

        var team = System.Enum.Parse(T("MatchManager+Team"), "Blue");
        Call("SwitchRobot", team);
        Assert.AreEqual(1, (int)mm.GetType().GetMethod("HumanIndex").Invoke(mm, new[] { team }), "switch to the teammate");

        Time.timeScale = 3f;
        float t = 0, minX = 1e9f, maxX = -1e9f, minZ = 1e9f, maxZ = -1e9f;
        while (Score("marcadorverm", "pontosverm") + Score("marcadorazul", "pontosazul") == 0 && t < 120f)
        {
            t += Time.deltaTime;
            foreach (var r in reds.Concat(blues).Where(r => r.activeInHierarchy)) { var p = r.transform.position; minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z); }
            yield return null;
        }
        Time.timeScale = 1f;
        Debug.Log($"2x2: red {Score("marcadorverm", "pontosverm")} blue {Score("marcadorazul", "pontosazul")} after {t:F1}s, x {minX:F0}..{maxX:F0} z {minZ:F0}..{maxZ:F0}");
        Assert.Greater(Score("marcadorverm", "pontosverm") + Score("marcadorazul", "pontosazul"), 0, "no goal in 120 s of 2x2");
        Assert.Greater(minX, W(-400, 0).x - 230f); Assert.Less(maxX, W(1656, 0).x + 230f); Assert.Greater(minZ, W(0, -180).z); Assert.Less(maxZ, W(0, 980).z);
        Call("QuitToMenu");
        Cfg("format", "OneVsOne"); Call("ApplyFormat");
    }

    [UnityTest]
    public IEnumerator RobotsCanBeShoved()
    {
        yield return Load();
        Cfg("players", "Two"); Cfg("arena", 0);
        Call("StartMatch");
        yield return WaitState("Playing", 5);
        var red = Find("unidadeverm"); var blue = Find("Unidadeazul");
        var rrb = red.GetComponent<Rigidbody>(); var brb = blue.GetComponent<Rigidbody>();
        var start = new Vector3(634, rrb.position.y, 400);
        rrb.position = start; rrb.linearVelocity = Vector3.zero;
        brb.position = new Vector3(634, brb.position.y, 160); brb.rotation = Quaternion.AngleAxis(-90, Vector3.up) * brb.rotation;
        yield return new WaitForFixedUpdate();
        brb.linearVelocity = new Vector3(0, 0, 1500);   // blue rams red side-on
        var tr = new System.Text.StringBuilder();
        for (int i = 0; i < 40; i++) { yield return new WaitForFixedUpdate(); if (i % 3 == 0) tr.Append($" b{brb.position.z:F0}/{brb.linearVelocity.z:F0} r{rrb.position.z:F0}/{rrb.linearVelocity.z:F0}"); }
        var moved = rrb.position - start; moved.y = 0;
        Debug.Log("PROBE shove: red moved " + moved.magnitude.ToString("F0") + " " + moved.ToString("F0") + tr);
        Assert.Greater(moved.z, 60f, "a hit robot should be shoved, not stop dead");
        Call("QuitToMenu");
    }

    [UnityTest]
    public IEnumerator ArenasChangeFloorPhysicsAndObstacles()
    {
        yield return Load();
        var theme = T("ArenaTheme");
        var floor = Find("Rink Floor").GetComponent<Renderer>();
        var puckRb = Find("PUCK").GetComponent<Rigidbody>();
        string[] mats = new string[3]; float[] drag = new float[3]; bool[] pillars = new bool[3]; float[] grip = new float[3];
        for (int i = 0; i < 3; i++)
        {
            theme.GetMethod("Apply").Invoke(null, new object[] { i });
            yield return null;
            mats[i] = floor.sharedMaterial.name; drag[i] = puckRb.linearDamping;
            pillars[i] = Find("Fabrica Pillars").activeInHierarchy;
            grip[i] = (float)T("moverp1").GetField("Grip").GetValue(null);
            Debug.Log($"ARENA {i}: floor={mats[i]} drag={drag[i]} pillars={pillars[i]} grip={grip[i]}");
        }
        Assert.AreNotEqual(mats[0], mats[1]); Assert.AreNotEqual(mats[0], mats[2]); Assert.AreNotEqual(mats[1], mats[2]);
        Assert.Less(drag[1], drag[0], "ice: puck glides further");
        Assert.Less(grip[1], 1f, "ice: robots skid");
        Assert.IsFalse(pillars[0]); Assert.IsFalse(pillars[1]); Assert.IsTrue(pillars[2], "factory pillars");
        theme.GetMethod("Apply").Invoke(null, new object[] { 0 });
        Assert.AreEqual(mats[0], floor.sharedMaterial.name, "back to the classic floor");
        Assert.AreEqual(1f, (float)T("moverp1").GetField("Grip").GetValue(null));
    }

    [UnityTest]
    public IEnumerator TournamentAdvancesOnWin()
    {
        yield return Load();
        Cfg("players", "One"); Cfg("kind", "Tournament"); Cfg("stage", 0); Cfg("goalsPerPeriod", 1); Cfg("minutesPerPeriod", 5);
        Call("StartMatch");
        Assert.AreEqual(0, (int)T("ArenaTheme").GetProperty("Current").GetValue(null), "match 1 in Clássica");
        Assert.AreEqual("Easy", T("GameConfig").GetField("difficulty").GetValue(null).ToString());
        for (int period = 0; period < 2; period++)
        {
            yield return WaitState("Playing", 10);
            Blue(); yield return null;
        }
        yield return WaitState("Result", 2);
        Assert.IsTrue((bool)P("TournamentNextAvailable"), "next match offered after a win");
        Assert.IsTrue(Find("PRÓXIMA PARTIDA Button").activeInHierarchy);
        Call("NextTournamentMatch");
        Assert.AreEqual(1, (int)T("GameConfig").GetField("stage").GetValue(null));
        Assert.AreEqual(1, (int)T("ArenaTheme").GetProperty("Current").GetValue(null), "match 2 in Gelo");
        Assert.AreEqual("Medium", T("GameConfig").GetField("difficulty").GetValue(null).ToString());
        Assert.AreEqual(0, I("periodsBlue"), "fresh match");
        Call("QuitToMenu");
        Cfg("kind", "QuickMatch"); Cfg("stage", 0);
        T("ArenaTheme").GetMethod("Apply").Invoke(null, new object[] { 0 });
    }
}
