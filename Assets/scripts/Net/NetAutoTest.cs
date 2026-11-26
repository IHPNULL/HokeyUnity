using System;
using UnityEngine;

// Automated online check for builds (no effect unless these command-line flags are given):
//   -netHost            host, start a match as soon as someone joins, log the state every second
//   -netJoin <ip>       join, hold "drive forward + turn" on the joiner's robot, log what arrives
//   -netQuit <seconds>  quit after that long
//   -netCodeFile <path> with -netHost: host through Relay and write the join code there;
//                       with -netJoin code: wait for that file and join with the code
// Used to run two headless copies of the game against each other on one machine.
public class NetAutoTest : MonoBehaviour
{
    bool host; string joinIp; string codeFile; float quitAt = -1, nextLog; bool started, joined;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var args = Environment.GetCommandLineArgs();
        bool h = Array.IndexOf(args, "-netHost") >= 0;
        int j = Array.IndexOf(args, "-netJoin");
        if (!h && j < 0) return;
        var t = new GameObject("NetAutoTest").AddComponent<NetAutoTest>();
        DontDestroyOnLoad(t.gameObject);
        t.host = h;
        if (j >= 0 && j + 1 < args.Length) t.joinIp = args[j + 1];
        int q = Array.IndexOf(args, "-netQuit");
        if (q >= 0 && q + 1 < args.Length && float.TryParse(args[q + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var secs)) t.quitAt = secs;
        if (Array.IndexOf(args, "-net3x3") >= 0) GameConfig.format = GameConfig.Format.ThreeVsThree;
        int cf = Array.IndexOf(args, "-netCodeFile");
        if (cf >= 0 && cf + 1 < args.Length) t.codeFile = args[cf + 1];
    }

    void Start()
    {
        Application.runInBackground = true;
        Application.targetFrameRate = 60;
        var s = NetSession.Instance;
        if (s == null) { Debug.Log("NETTEST no NetSession"); return; }
        if (host)
        {
            if (codeFile != null) { s.HostWithCode(); Debug.Log("NETTEST host via Relay"); }
            else Debug.Log("NETTEST host start=" + s.Host() + " ip=" + NetSession.LocalIP());
        }
        else
        {
            NetSession.TestInput = () => new Vector2(1f, 0.25f);
            GameConfig.redRobots[0] = "trator";   // the joiner's pick must reach the host
            if (codeFile == null) { Debug.Log("NETTEST join start=" + s.Join(joinIp) + " ip=" + joinIp); joined = true; }
        }
    }

    void Update()
    {
        var mm = MatchManager.Instance;
        if (host && codeFile != null && NetSession.JoinCode != "" && !System.IO.File.Exists(codeFile))
        { System.IO.File.WriteAllText(codeFile, NetSession.JoinCode); Debug.Log("NETTEST code " + NetSession.JoinCode); }
        if (!host && !joined && codeFile != null && System.IO.File.Exists(codeFile))
        {
            joined = true; var code = System.IO.File.ReadAllText(codeFile).Trim();
            Debug.Log("NETTEST join with code " + code); NetSession.Instance.JoinWithCode(code);
        }
        if (host && !started && NetSession.PeerConnected && mm != null && mm.state == MatchManager.State.Menu && GameConfig.redRobots[0] == "trator")
        {
            started = true;
            GameConfig.minutesPerPeriod = 2; GameConfig.goalsPerPeriod = 3;
            Debug.Log("NETTEST host starting match, red pick=" + GameConfig.redRobots[0]);
            mm.StartMatch();
        }
        if (Time.time >= nextLog && mm != null)
        {
            nextLog = Time.time + 1f;
            var red = mm.TeamRobots(MatchManager.Team.Red)[0].transform; var blue = mm.TeamRobots(MatchManager.Team.Blue)[0].transform;
            Debug.Log($"NETTEST {(host ? "host" : "join")} t={Time.time:F0} role={NetSession.role} peer={NetSession.PeerConnected} state={mm.state} clock={mm.clock:F1} " +
                      $"score={mm.GoalsRed}-{mm.GoalsBlue} red=({red.position.x:F0},{red.position.z:F0}) redScale={red.lossyScale.x:F0} blue=({blue.position.x:F0},{blue.position.z:F0}) " +
                      $"puck=({mm.Puck.transform.position.x:F0},{mm.Puck.transform.position.z:F0}) snaps={NetSession.SnapshotsReceived} status={NetSession.Status}");
        }
        if (quitAt > 0 && Time.time > quitAt) { Debug.Log("NETTEST quit"); if (NetSession.Instance) NetSession.Instance.Shutdown(); Application.Quit(); quitAt = -1; }
    }
}
