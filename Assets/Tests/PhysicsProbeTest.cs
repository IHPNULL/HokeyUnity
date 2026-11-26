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
    static Vector3 W(float x, float z, float y = 0f) => (Vector3)System.Type.GetType("Rink, Assembly-CSharp").GetMethod("W").Invoke(null, new object[] { x, z, y });   // scene -> stretched rink
    static float Rk(string p) => (float)System.Type.GetType("Rink, Assembly-CSharp").GetProperty(p).GetValue(null);


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
        var cols = red.transform.Find("Loadout").GetComponentsInChildren<Collider>();
        var hull = cols.First(k => k.name == "Hull Collider").bounds;
        var arm = cols.Where(k => k.name == "Arm Collider" && k.bounds.center.z > c.z).OrderBy(k => k.bounds.min.x).First().bounds;

        // Into the open pocket: the puck is caught by the back wall, never passes through the hull.
        yield return Shoot(c + new Vector3(-350, 0, 0), new Vector3(1000, 0, 0), 0.8f);
        Debug.Log($"PROBE robot pocket: puck x={prb.position.x:F0} robot x={c.x:F0} hull back x={hull.max.x:F0} vx={prb.linearVelocity.x:F0}");
        Assert.Less(prb.position.x, hull.max.x, "puck went through the robot hull");

        // Straight at an arm tip: the solid hull bounces it back.
        yield return Shoot(new Vector3(c.x - 350, 0, arm.center.z), new Vector3(1000, 0, 0), 0.8f);
        Debug.Log($"PROBE robot arm: puck x={prb.position.x:F0} arm front x={arm.min.x:F0} vx={prb.linearVelocity.x:F0}");
        Assert.Less(prb.position.x, arm.min.x, "puck went into/through the robot's arm");
    }

    // The puck glides and bounces because of its physics material. Its values once lived only in the
    // Editor's memory (an unrecognised file extension was never saved) and an Editor restart silently
    // reset them to Unity's defaults (friction 0.6, no bounce): the puck stuck to the floor.
    [UnityTest]
    public IEnumerator PuckAndBoardMaterialsAreTuned()
    {
        yield return Setup();
        var pm = prb.GetComponent<Collider>().sharedMaterial;
        Assert.IsNotNull(pm, "puck has a physics material");
        Assert.LessOrEqual(pm.dynamicFriction, 0.05f, "puck glides (low friction)");
        Assert.GreaterOrEqual(pm.bounciness, 0.5f, "puck bounces");
        Assert.AreEqual(PhysicsMaterialCombine.Minimum, pm.frictionCombine);
        Assert.AreEqual(PhysicsMaterialCombine.Maximum, pm.bounceCombine);
        var glass = Find("Glass").GetComponent<Collider>().sharedMaterial;
        Assert.IsNotNull(glass, "boards have a physics material");
        Assert.GreaterOrEqual(glass.bounciness, 0.5f, "boards are bouncy");
    }

    [UnityTest]
    public IEnumerator PuckBouncesOffGoalPost()
    {
        yield return Setup();
        red.SetActive(false); blue.SetActive(false);
        // Post of the +x goal stands at the goal mouth, z≈130 (goals keep their real size). Shoot straight at it.
        float mouth = Find("gol Visual").transform.position.x;
        var score = T("marcadorazul").GetField("pontosazul"); int before = (int)score.GetValue(null);
        yield return Shoot(new Vector3(mouth - 256, 0, 130), new Vector3(1000, 0, 0), 0.45f);
        Debug.Log($"PROBE post: puck x={prb.position.x:F0} vx={prb.linearVelocity.x:F0}");
        Assert.AreEqual(before, (int)score.GetValue(null), "hitting the post must not count as a goal");
        Assert.Less(prb.linearVelocity.x, -100f, "puck should bounce back off the post");
        Assert.Less(prb.position.x, mouth, "puck passed through the goal post");
    }

    [UnityTest]
    public IEnumerator PuckReboundsOffBoards()
    {
        yield return Setup();
        red.SetActive(false); blue.SetActive(false);
        // Side board near z≈-130 (rink spans z≈-130..930). Shoot at it at 45 degrees.
        yield return Shoot(W(634, 100), new Vector3(700, 0, -700), 0.9f);
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
        Assert.Greater(rb.position.z, W(0, -180).z, "robot drove through the boards");   // side boards at W(0,-110); a robot half-width beyond is still inside
    }
}
