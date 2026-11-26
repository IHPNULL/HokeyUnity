using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Every match re-applies the robot loadouts. The old loadout must not be copied into the new one:
// a robot keeps the same number of visual parts no matter how many matches are played.
public class LoadoutReapplyTest
{
    static GameObject Find(string name) => Resources.FindObjectsOfTypeAll<GameObject>().First(g => g.name == name && g.scene.IsValid());
    static System.Type T(string n) => System.Type.GetType(n + ", Assembly-CSharp");

    static int Parts(string robot) =>
        Find(robot).transform.Find("Loadout").GetComponentsInChildren<MeshRenderer>(true).Length;

    [UnityTest]
    public IEnumerator RematchesDoNotDuplicateRobotParts()
    {
        Time.timeScale = 1f;
        T("GameConfig").GetField("format").SetValue(null, System.Enum.Parse(T("GameConfig+Format"), "OneVsOne"));
        SceneManager.LoadScene("MENU");
        yield return null; yield return null;
        var mm = T("MatchManager").GetProperty("Instance").GetValue(null);
        var start = mm.GetType().GetMethod("StartMatch");

        start.Invoke(mm, null);
        yield return null; yield return null;
        int red = Parts("unidadeverm"), blue = Parts("Unidadeazul");

        for (int i = 0; i < 3; i++) { start.Invoke(mm, null); yield return null; yield return null; }

        Assert.AreEqual(red, Parts("unidadeverm"), "red robot parts after rematches");
        Assert.AreEqual(blue, Parts("Unidadeazul"), "blue robot parts after rematches");
        Assert.IsFalse(Find("unidadeverm").GetComponentsInChildren<Transform>(true).Any(t => t.name.Contains(") (body)")),
            "no copies of copies");
    }
}
