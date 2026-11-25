using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Collision behaviour checks for the arena: puck vs robot hull, goal posts, boards; robot vs boards.
public class PhysicsProbeTest
{
    static GameObject Find(string name) => Resources.FindObjectsOfTypeAll<GameObject>().First(g => g.name == name && g.scene.IsValid());
    static System.Type T(string n) => System.Type.GetType(n + ", Assembly-CSharp");

    GameObject puck, red, blue;
    Rigidbody prb;

    IEnumerator Setup()
    {
        SceneManager.LoadScene("MENU");
        yield return null;
        Find("JOGANDO").SetActive(true);
        puck = Find("PUCK"); red = Find("unidadeverm"); blue = Find("Unidadeazul");
        prb = puck.GetComponent<Rigidbody>();
        foreach (var r in new[] { red, blue }) ((Behaviour)r.GetComponent(T("moverp1"))).enabled = false;
        yield return new WaitForSeconds(1.2f); // puck settles on the ice
    }

    IEnumerator Shoot(Vector3 from, Vector3 vel, float seconds)
    {
        prb.position = new Vector3(from.x, prb.position.y, from.z);
        prb.linearVelocity = Vector3.zero; prb.angularVelocity = Vector3.zero;
        yield return new WaitForFixedUpdate();
        prb.linearVelocity = vel;
        yield return new WaitForSeconds(seconds);
    }

    [UnityTest]
    public IEnumerator PuckBouncesOffRobotHull()
    {
        yield return Setup();
        blue.SetActive(false);
        var c = red.transform.position;
        yield return Shoot(c + new Vector3(-350, 0, 0), new Vector3(1000, 0, 0), 0.8f);
        Debug.Log($"PROBE robot: puck x={prb.position.x:F0} robot x={c.x:F0} vx={prb.linearVelocity.x:F0}");
        Assert.Less(prb.position.x, c.x - 40f, "puck went into/through the robot hull");
    }

    [UnityTest]
    public IEnumerator PuckBouncesOffGoalPost()
    {
        yield return Setup();
        red.SetActive(false); blue.SetActive(false);
        // Post of the +x goal stands at the goal mouth (x≈1656), z≈130. Shoot straight at it from the rink.
        var score = T("marcadorazul").GetField("pontosazul"); int before = (int)score.GetValue(null);
        yield return Shoot(new Vector3(1400, 0, 130), new Vector3(1000, 0, 0), 0.45f);
        Debug.Log($"PROBE post: puck x={prb.position.x:F0} vx={prb.linearVelocity.x:F0}");
        Assert.AreEqual(before, (int)score.GetValue(null), "hitting the post must not count as a goal");
        Assert.Less(prb.linearVelocity.x, -100f, "puck should bounce back off the post");
        Assert.Less(prb.position.x, 1656f, "puck passed through the goal post");
    }

    [UnityTest]
    public IEnumerator PuckReboundsOffBoards()
    {
        yield return Setup();
        red.SetActive(false); blue.SetActive(false);
        // Side board near z≈-130 (rink spans z≈-130..930). Shoot at it at 45 degrees.
        yield return Shoot(new Vector3(634, 0, 300), new Vector3(700, 0, -700), 0.9f);
        Debug.Log($"PROBE boards: puck z={prb.position.z:F0} vz={prb.linearVelocity.z:F0} speed={prb.linearVelocity.magnitude:F0}");
        Assert.Greater(prb.linearVelocity.z, 150f, "puck should rebound off the boards (moving back toward the rink)");
    }

    [UnityTest]
    public IEnumerator FastRobotDoesNotTunnelBoards()
    {
        yield return Setup();
        blue.SetActive(false);
        var rb = red.GetComponent<Rigidbody>();
        rb.position = new Vector3(634, rb.position.y, 400);
        for (int i = 0; i < 90; i++) { rb.linearVelocity = new Vector3(0, 0, -2500); yield return new WaitForFixedUpdate(); }
        Debug.Log($"PROBE robot->boards: z={rb.position.z:F0}");
        Assert.Greater(rb.position.z, -200f, "robot drove through the boards");
    }
}
