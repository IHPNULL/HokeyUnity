using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class ImpactSoundTest
{
    static GameObject Find(string name) => Resources.FindObjectsOfTypeAll<GameObject>().First(g => g.name == name && g.scene.IsValid());
    static System.Type T(string n) => System.Type.GetType(n + ", Assembly-CSharp");
    static object S(string field) => T("ImpactSound").GetField(field, BindingFlags.Public | BindingFlags.Static).GetValue(null);

    [UnityTest]
    public IEnumerator PuckHitAndRobotCrashMakeMetalSounds()
    {
        SceneManager.LoadScene("MENU");
        yield return null;
        Find("MWNU").SetActive(false);
        Find("JOGANDO").SetActive(true);
        yield return new WaitForSeconds(1.5f); // puck settles

        var red = Find("unidadeverm"); var blue = Find("Unidadeazul"); var puckGo = Find("PUCK");
        Assert.IsNotNull(red.GetComponent(T("ImpactSound")), "robot has ImpactSound");
        Assert.IsNotNull(puckGo.GetComponent(T("ImpactSound")), "puck has ImpactSound");

        int before = (int)S("PlayCount");
        var prb = puckGo.GetComponent<Rigidbody>();
        prb.position = new Vector3(red.transform.position.x - 300, prb.position.y, red.transform.position.z);
        prb.linearVelocity = new Vector3(1200, 0, 0);
        float t = 0; while ((int)S("PlayCount") == before && t < 3) { t += Time.deltaTime; yield return null; }
        Assert.Greater((int)S("PlayCount"), before, "puck -> robot made no sound");
        Assert.AreEqual("clink", (string)S("LastPlayed"));

        yield return new WaitForSeconds(0.3f);
        before = (int)S("PlayCount");
        var brb = blue.GetComponent<Rigidbody>();
        ((Behaviour)blue.GetComponent(T("moverp1"))).enabled = false; // stand in for a player driving at speed
        brb.position = new Vector3(red.transform.position.x - 260, brb.position.y, red.transform.position.z);
        yield return new WaitForFixedUpdate();
        brb.linearVelocity = new Vector3(1400, 0, 0);
        t = 0; while ((int)S("PlayCount") == before && t < 3) { t += Time.deltaTime; yield return null; }
        Assert.Greater((int)S("PlayCount"), before, "robot -> robot made no sound");
        Assert.AreEqual("clang", (string)S("LastPlayed"));
    }
}
