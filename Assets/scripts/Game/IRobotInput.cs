using UnityEngine;

// What drives a robot: Drive moves along the robot's nose, Turn rotates it. Both in [-1, 1].
public interface IRobotInput
{
    float Drive { get; }
    float Turn { get; }
}

// Keyboard: one or two axis pairs (1 Player accepts both WASD and the arrows).
public class KeyboardRobotInput : IRobotInput
{
    readonly string[] driveAxes, turnAxes;
    public KeyboardRobotInput(string[] drive, string[] turn) { driveAxes = drive; turnAxes = turn; }
    public float Drive => Sum(driveAxes);
    public float Turn => Sum(turnAxes);
    static float Sum(string[] axes) { float s = 0; foreach (var a in axes) s += Input.GetAxis(a); return Mathf.Clamp(s, -1f, 1f); }
}
