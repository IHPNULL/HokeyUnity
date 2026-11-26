using UnityEngine;

// What drives a robot: Drive moves along the robot's nose, Turn rotates it. Both in [-1, 1].
public interface IRobotInput
{
    float Drive { get; }
    float Turn { get; }
    bool Action { get; }   // held: the robot's tool (kick, boost, release the claw)
}

// Keyboard: one or two axis pairs (1 Player accepts both WASD and the arrows).
public class KeyboardRobotInput : IRobotInput
{
    readonly string[] driveAxes, turnAxes;
    readonly KeyCode[] actionKeys;
    public KeyboardRobotInput(string[] drive, string[] turn, KeyCode[] action = null) { driveAxes = drive; turnAxes = turn; actionKeys = action ?? new KeyCode[0]; }
    public float Drive => Sum(driveAxes);
    public float Turn => Sum(turnAxes);
    public bool Action { get { foreach (var k in actionKeys) if (Input.GetKey(k)) return true; return false; } }
    static float Sum(string[] axes) { float s = 0; foreach (var a in axes) s += Input.GetAxis(a); return Mathf.Clamp(s, -1f, 1f); }
}
