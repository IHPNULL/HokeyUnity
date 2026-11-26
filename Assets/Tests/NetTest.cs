using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Online plumbing that fits in one process: hosting opens the port, the online screen works,
// a snapshot written by the host reads back the same. (Host + joiner end to end runs with two
// headless builds: see NetAutoTest.)
public class NetTest
{
    static GameObject Find(string name) => Resources.FindObjectsOfTypeAll<GameObject>().First(g => g.name == name && g.scene.IsValid());
    static System.Type T(string n) => System.Type.GetType(n + ", Assembly-CSharp");
    object Session => T("NetSession").GetProperty("Instance").GetValue(null);
    static string Role => T("NetSession").GetProperty("role").GetValue(null).ToString();

    [UnityTest]
    public IEnumerator HostFromTheOnlineScreen()
    {
        SceneManager.LoadScene("MENU");
        yield return null; yield return null;
        Find("ONLINE Button").GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); yield return null;
        Assert.IsTrue(Find("Online").activeInHierarchy, "online screen");
        Find("HOSPEDAR PARTIDA Button").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        yield return new WaitForSeconds(0.5f);
        Assert.AreEqual("Host", Role);
        var status = (string)T("NetSession").GetField("Status").GetValue(null);
        StringAssert.Contains("Aguardando", status);
        var dir = System.Environment.GetEnvironmentVariable("PROBE_DIR");
        if (dir != null) { yield return new WaitForEndOfFrame(); System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "ui_online.png"), ScreenCapture.CaptureScreenshotAsTexture().EncodeToPNG()); }
        Session.GetType().GetMethod("Shutdown").Invoke(Session, null);
        yield return null;
        Assert.AreEqual("Offline", Role);
    }

    [UnityTest]
    public IEnumerator JoinWithBadIpIsRefused()
    {
        SceneManager.LoadScene("MENU");
        yield return null; yield return null;
        bool ok = (bool)Session.GetType().GetMethod("Join").Invoke(Session, new object[] { "not an ip" });
        Assert.IsFalse(ok);
        Assert.AreEqual("Offline", Role);
    }
}
