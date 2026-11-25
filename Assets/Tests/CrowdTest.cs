using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class CrowdTest
{
    static GameObject Find(string name) => Resources.FindObjectsOfTypeAll<GameObject>().First(g => g.name == name && g.scene.IsValid());

    [UnityTest]
    public IEnumerator CrowdFillsStandsAndCheersForScorer()
    {
        SceneManager.LoadScene("MENU");
        yield return null;
        Find("JOGANDO").SetActive(true);
        yield return null; yield return null;

        var crowd = Find("Crowd").GetComponent("CrowdSpawner");
        var type = crowd.GetType();
        int count = (int)type.GetProperty("FanCount").GetValue(crowd);
        Debug.Log("CROWD fans=" + count);
        Assert.Greater(count, 500, "too few fans placed");
        Assert.AreEqual(-1, (int)type.GetProperty("CheeringTeam").GetValue(crowd));

        // Blue scores -> blue fans cheer.
        var blueScore = System.Type.GetType("marcadorazul, Assembly-CSharp").GetField("pontosazul");
        blueScore.SetValue(null, (int)blueScore.GetValue(null) + 1);
        yield return null;
        Assert.AreEqual(1, (int)type.GetProperty("CheeringTeam").GetValue(crowd), "blue fans should cheer");
        var sources = ((Component)crowd).GetComponents<AudioSource>();
        Assert.IsTrue(sources.Any(s => s.isPlaying && !s.loop), "roar should play");

        yield return new WaitForSeconds(0.15f);
        var dir = System.Environment.GetEnvironmentVariable("PROBE_DIR");
        if (dir != null)
        {
            yield return new WaitForEndOfFrame();
            var shot = ScreenCapture.CaptureScreenshotAsTexture();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "crowd_gameview.png"), shot.EncodeToPNG());
            Debug.Log("CROWD shot " + shot.width + "x" + shot.height);
        }
        blueScore.SetValue(null, 0);
    }
}
