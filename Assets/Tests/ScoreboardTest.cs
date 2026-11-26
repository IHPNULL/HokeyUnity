using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class ScoreboardTest
{
    static GameObject Find(string name) => Resources.FindObjectsOfTypeAll<GameObject>().First(g => g.name == name && g.scene.IsValid());

    [UnityTest]
    public IEnumerator JumbotronShowsLiveScore()
    {
        // The pole is hidden on the 3x3 rink; earlier tests may leave the format there.
        var cfg = System.Type.GetType("GameConfig, Assembly-CSharp");
        var fmt = cfg.GetField("format");
        fmt.SetValue(null, System.Enum.Parse(fmt.FieldType, "OneVsOne"));
        SceneManager.LoadScene("MENU");
        yield return null;
        Find("JOGANDO").SetActive(true);
        yield return null; yield return null;

        var placar = Find("placar");
        var board = placar.GetComponent("ScoreboardDisplay");
        Assert.IsNotNull(board, "placar should carry ScoreboardDisplay");
        Assert.IsNotNull(placar.transform.Find("Jumbotron/Display"), "display mesh not built");
        Assert.IsFalse(placar.transform.Find("Box001").GetComponent<Renderer>().enabled, "old grey box should be hidden");

        var blue = System.Type.GetType("marcadorazul, Assembly-CSharp").GetField("pontosazul");
        var red = System.Type.GetType("marcadorverm, Assembly-CSharp").GetField("pontosverm");
        blue.SetValue(null, 2); red.SetValue(null, 7);
        yield return null; yield return null;
        var shown = (string)board.GetType().GetProperty("Shown").GetValue(board);
        StringAssert.StartsWith("2|7|", shown);
        int tris = (int)board.GetType().GetProperty("DisplayTriangles").GetValue(board);
        Assert.Less(tris, 2000, "display mesh too heavy");
        blue.SetValue(null, 0); red.SetValue(null, 0);
    }
}
