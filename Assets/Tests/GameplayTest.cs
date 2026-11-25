using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class GameplayTest
{
    static int Score(string type, string field) =>
        (int)System.Type.GetType(type + ", Assembly-CSharp").GetField(field, BindingFlags.Public | BindingFlags.Static).GetValue(null);

    static GameObject Find(string name) =>
        Resources.FindObjectsOfTypeAll<GameObject>().First(g => g.name == name && g.scene.IsValid());

    [UnityTest]
    public IEnumerator PuckStaysOnIceAndGoalsScore()
    {
        SceneManager.LoadScene("MENU");
        yield return null;
        Find("JOGANDO").SetActive(true);
        var puck = Find("PUCK");
        puck.SetActive(true);
        Assert.IsNotNull(Find("gol").GetComponent<Collider>(), "gol collider");
        Assert.IsNotNull(Find("gol (1)").GetComponent<Collider>(), "gol (1) collider");

        yield return new WaitForSeconds(2f);
        Debug.Log("TEST puck after settle: " + puck.transform.position);
        Assert.Greater(puck.transform.position.y, 9f, "puck fell through the ice");

        Find("unidades").SetActive(false);   // keep the robots out of the shot lane
        var rb = puck.GetComponent<Rigidbody>();
        int azul = Score("marcadorazul", "pontosazul");
        rb.linearVelocity = new Vector3(1500, 0, 0);   // toward "gol" -> blue scores
        float t = 0; while (Score("marcadorazul", "pontosazul") == azul && t < 5) { t += Time.deltaTime; yield return null; }
        Debug.Log("TEST azul " + azul + " -> " + Score("marcadorazul", "pontosazul") + " puck " + puck.transform.position);
        Assert.AreEqual(azul + 1, Score("marcadorazul", "pontosazul"), "shot at gol did not score");

        yield return new WaitForSeconds(1f);
        int verm = Score("marcadorverm", "pontosverm");
        rb.linearVelocity = new Vector3(-1500, 0, 0);  // toward "gol (1)" -> red scores
        t = 0; while (Score("marcadorverm", "pontosverm") == verm && t < 5) { t += Time.deltaTime; yield return null; }
        Debug.Log("TEST verm " + verm + " -> " + Score("marcadorverm", "pontosverm") + " puck " + puck.transform.position);
        Assert.AreEqual(verm + 1, Score("marcadorverm", "pontosverm"), "shot at gol (1) did not score");
    }
}
