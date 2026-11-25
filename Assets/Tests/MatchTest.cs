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
    public IEnumerator OnePlayerFullScreenAndAIScores([Values("Easy", "Medium", "Hard")] string diff, [Values("center", "ownCorner", "farCorner")] string spot)
    {
        yield return Load();
        Cfg("players", "One"); Cfg("kind", "QuickMatch"); Cfg("difficulty", diff);
        Cfg("goalsPerPeriod", 5); Cfg("minutesPerPeriod", 5);
        Call("StartMatch");
        var red = Find("unidadeverm"); var blue = Find("Unidadeazul");
        Assert.IsFalse(red.GetComponentInChildren<Camera>(true).gameObject.activeInHierarchy, "red camera off in 1P");
        Assert.AreEqual(new Rect(0, 0, 1, 1), blue.GetComponentInChildren<Camera>(true).rect, "blue camera full screen");
        yield return WaitState("Playing", 5);
        if (spot == "center") yield return Shot("ui_hud_1p_" + diff);

        // Idle player parked out of the way; the AI must score.
        ((Behaviour)blue.GetComponent(T("moverp1"))).enabled = false;
        var brb = blue.GetComponent<Rigidbody>(); brb.position = new Vector3(600, brb.position.y, 850);
        var prb = Find("PUCK").GetComponent<Rigidbody>();
        if (spot == "ownCorner") prb.position = new Vector3(1500, prb.position.y, 60);     // deep in the AI's own end
        if (spot == "farCorner") prb.position = new Vector3(-250, prb.position.y, 820);    // corner beside the target goal
        prb.linearVelocity = Vector3.zero;
        Time.timeScale = 3f;
        var trace = new System.Text.StringBuilder();
        float t = 0, minX = 1e9f, maxX = -1e9f, minZ = 1e9f, maxZ = -1e9f;
        while (Score("marcadorverm", "pontosverm") == 0 && t < 90f)
        {
            t += Time.deltaTime;
            var p = red.transform.position;
            if ((int)(t * 2) != (int)((t - Time.deltaTime) * 2) && t < 30)
            {
                var ai = red.GetComponent(T("AIRobotInput"));
                trace.Append($" [{t:F1}s r({p.x:F0},{p.z:F0}) p({prb.position.x:F0},{prb.position.z:F0}) {ai.GetType().GetProperty("Mode").GetValue(ai)}]");
            }
            minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z);
            yield return null;
        }
        Time.timeScale = 1f;
        Debug.Log($"AI {diff} {spot}: scored={Score("marcadorverm", "pontosverm")} after {t:F1}s, x {minX:F0}..{maxX:F0} z {minZ:F0}..{maxZ:F0}, blue goals {Score("marcadorazul", "pontosazul")}" + (Score("marcadorverm", "pontosverm") == 0 || Score("marcadorazul", "pontosazul") > 0 ? trace.ToString() : ""));
        // Easy is slow enough that a puck rebounding off the far post can beat it back to its own net once.
        Assert.LessOrEqual(Score("marcadorazul", "pontosazul"), diff == "Easy" ? 1 : 0, "AI own goals");
        Assert.Greater(Score("marcadorverm", "pontosverm"), 0, "AI did not score within 90 s");
        Assert.Greater(minX, -450f); Assert.Less(maxX, 1700f); Assert.Greater(minZ, -180f); Assert.Less(maxZ, 980f);
        Call("QuitToMenu");
    }
}
