using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

// Online play, peer to peer by IP: one player hosts (blue), the other joins (red).
// The host runs the whole game (physics, AI, rules); the joiner sends its keys and robot picks
// and shows the host's snapshots. Netcode for GameObjects is only the connection layer here:
// everything travels as named messages, so the scene needs no NetworkObjects.
public class NetSession : MonoBehaviour
{
    public enum Role { Offline, Host, Client }
    public static Role role { get; private set; } = Role.Offline;
    public static bool Online => role != Role.Offline;
    public static bool IsHost => role == Role.Host;
    public static bool IsClient => role == Role.Client;
    public static NetSession Instance { get; private set; }

    public const ushort Port = 7777;
    public static string Status = "";
    public static bool PeerConnected { get; private set; }   // host: a player joined / client: connected to the host
    public static int SnapshotsReceived { get; private set; }
    public static event Action Changed;                       // UI refresh

    // Host: the joiner's keys.
    public readonly RemoteInput Remote = new RemoteInput();
    public class RemoteInput : IRobotInput
    {
        public float Drive { get; set; }
        public float Turn { get; set; }
        public bool Action { get; set; }
    }

    NetworkManager nm; UnityTransport transport;
    ulong peer;
    int fixedCount; byte switchCount, lastSwitch;
    bool clientInMatch;

    const string MsgConfig = "hk.cfg", MsgPick = "hk.pick", MsgInput = "hk.in", MsgSnap = "hk.snap", MsgEvent = "hk.ev";

    void Awake() { Instance = this; }
    void OnDestroy()
    {
        if (Instance != this) return;
        Shutdown(); Instance = null;
        if (nm) Destroy(nm.gameObject);
    }

    // ---- connect ------------------------------------------------------------------------------

    NetworkManager Manager()
    {
        if (nm) return nm;
        if (NetworkManager.Singleton) Destroy(NetworkManager.Singleton.gameObject);   // a stale one from an earlier scene load
        // NetworkManager must be a root object, and its config must exist before it wakes up
        // (built inactive, configured, then activated).
        var go = new GameObject("Network");
        go.SetActive(false);
        transport = go.AddComponent<UnityTransport>();
        nm = go.AddComponent<NetworkManager>();
        nm.NetworkConfig = new NetworkConfig { NetworkTransport = transport, ConnectionApproval = true, EnableSceneManagement = false };
        go.SetActive(true);
        nm.ConnectionApprovalCallback = (req, resp) => { resp.Approved = !PeerConnected; resp.CreatePlayerObject = false; resp.Reason = PeerConnected ? "Partida cheia" : ""; };
        nm.OnClientConnectedCallback += OnConnected;
        nm.OnClientDisconnectCallback += OnDisconnected;
        return nm;
    }

    public bool Host()
    {
        Shutdown();
        var m = Manager();
        transport.SetConnectionData("0.0.0.0", Port, "0.0.0.0");
        if (!m.StartHost()) { Status = "Não foi possível abrir a porta " + Port; Changed?.Invoke(); return false; }
        role = Role.Host; Register();
        Status = "Aguardando um jogador… seu IP: " + LocalIP() + "  (porta " + Port + ")";
        Changed?.Invoke();
        return true;
    }

    public bool Join(string ip)
    {
        Shutdown();
        ip = (ip ?? "").Trim();
        if (!System.Net.IPAddress.TryParse(ip, out _)) { Status = "IP inválido"; Changed?.Invoke(); return false; }
        var m = Manager();
        transport.SetConnectionData(ip, Port);
        if (!m.StartClient()) { Status = "Falha ao conectar"; Changed?.Invoke(); return false; }
        role = Role.Client; Register();
        Status = "Conectando a " + ip + "…";
        Changed?.Invoke();
        return true;
    }

    public void Shutdown()
    {
        if (nm && (nm.IsListening || nm.ShutdownInProgress)) nm.Shutdown();
        bool wasClient = IsClient;
        role = Role.Offline; PeerConnected = false; clientInMatch = false; SnapshotsReceived = 0;
        if (wasClient && MatchManager.Instance) MatchManager.Instance.SetRemoteView(false);
        Changed?.Invoke();
    }

    void Register()
    {
        var cm = nm.CustomMessagingManager;
        cm.RegisterNamedMessageHandler(MsgConfig, OnConfig);
        cm.RegisterNamedMessageHandler(MsgPick, OnPick);
        cm.RegisterNamedMessageHandler(MsgInput, OnInput);
        cm.RegisterNamedMessageHandler(MsgSnap, OnSnapshot);
        cm.RegisterNamedMessageHandler(MsgEvent, OnEvent);
    }

    void OnConnected(ulong id)
    {
        if (IsHost && id != NetworkManager.ServerClientId)
        {
            peer = id; PeerConnected = true; Status = "Jogador conectado";
            SendConfig();
        }
        else if (IsClient && id == nm.LocalClientId) { PeerConnected = true; Status = "Conectado! Escolha seus robôs (VERMELHO)."; SendPicks(); }
        Changed?.Invoke();
    }

    void OnDisconnected(ulong id)
    {
        if (IsHost && id == peer)
        {
            PeerConnected = false; Status = "O outro jogador saiu. Aguardando…";
            var mm = MatchManager.Instance;
            if (mm && mm.state != MatchManager.State.Menu) { mm.QuitToMenu(); mm.RaiseBanner("O OUTRO JOGADOR SAIU", Color.white); }
            Changed?.Invoke();
        }
        else if (IsClient)
        {
            string reason = nm.DisconnectReason;
            Shutdown();
            Status = string.IsNullOrEmpty(reason) ? "Conexão com o anfitrião perdida" : reason;
            var mm = MatchManager.Instance; if (mm) mm.QuitToMenu();
            Changed?.Invoke();
        }
    }

    // ---- per step -----------------------------------------------------------------------------

    void FixedUpdate()
    {
        if (!Online || !PeerConnected || nm == null) return;
        fixedCount++;
        var mm = MatchManager.Instance; if (!mm) return;
        if (IsHost && fixedCount % 2 == 0) SendSnapshot(mm);      // 25 per second
        if (IsClient && clientInMatch) SendInput();
    }

    void Update()
    {
        if (IsClient && clientInMatch && (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))) switchCount++;
    }

    // ---- messages: host -> client -------------------------------------------------------------

    // Match settings and both teams' robots, plus whether a match is on (from the host's state,
    // so a late message can't pull the joiner out of a running match).
    public void SendConfig(bool _ = false)
    {
        if (!IsHost || !PeerConnected) return;
        var mmi = MatchManager.Instance;
        bool inMatch = mmi && mmi.state != MatchManager.State.Menu;
        using var w = new FastBufferWriter(512, Allocator.Temp);
        w.WriteValueSafe(inMatch);
        w.WriteValueSafe((byte)GameConfig.format); w.WriteValueSafe((byte)GameConfig.arena);
        w.WriteValueSafe((byte)GameConfig.minutesPerPeriod); w.WriteValueSafe((byte)GameConfig.goalsPerPeriod);
        w.WriteValueSafe(GameConfig.speed); w.WriteValueSafe(GameConfig.rotation);
        for (int i = 0; i < 3; i++) w.WriteValueSafe(GameConfig.blueRobots[i]);
        for (int i = 0; i < 3; i++) w.WriteValueSafe(GameConfig.redRobots[i]);
        nm.CustomMessagingManager.SendNamedMessage(MsgConfig, peer, w, NetworkDelivery.ReliableSequenced);
    }

    void OnConfig(ulong sender, FastBufferReader r)
    {
        if (!IsClient) return;
        r.ReadValueSafe(out bool inMatch);
        r.ReadValueSafe(out byte format); r.ReadValueSafe(out byte arena); r.ReadValueSafe(out byte minutes); r.ReadValueSafe(out byte goals);
        r.ReadValueSafe(out float speed); r.ReadValueSafe(out int rotation);
        for (int i = 0; i < 3; i++) { r.ReadValueSafe(out string id); GameConfig.blueRobots[i] = id; }
        for (int i = 0; i < 3; i++) { r.ReadValueSafe(out string id); if (inMatch) GameConfig.redRobots[i] = id; }   // in the menu the joiner owns its picks
        GameConfig.format = (GameConfig.Format)format; GameConfig.arena = arena;
        GameConfig.minutesPerPeriod = minutes; GameConfig.goalsPerPeriod = goals;
        GameConfig.speed = speed; GameConfig.rotation = rotation;
        var mm = MatchManager.Instance;
        if (mm)
        {
            ArenaTheme.Apply(GameConfig.arena);
            mm.ApplyFormat();
            if (inMatch && !clientInMatch) mm.SetRemoteView(true);
            if (!inMatch && clientInMatch) { mm.SetRemoteView(false); mm.QuitToMenu(); }
        }
        clientInMatch = inMatch;
        Changed?.Invoke();
    }

    public struct RobotState { public bool active; public Vector3 pos; public Quaternion rot; public bool boost; }

    void SendSnapshot(MatchManager mm)
    {
        using var w = new FastBufferWriter(1024, Allocator.Temp);
        WriteSnapshot(w, mm);
        nm.CustomMessagingManager.SendNamedMessage(MsgSnap, peer, w, NetworkDelivery.UnreliableSequenced);
    }

    // Public for tests: snapshot round trip without a network.
    public static void WriteSnapshot(FastBufferWriter w, MatchManager mm)
    {
        w.WriteValueSafe((byte)mm.state); w.WriteValueSafe(mm.stateTime); w.WriteValueSafe(mm.clock);
        w.WriteValueSafe((byte)mm.period); w.WriteValueSafe((byte)mm.periodsRed); w.WriteValueSafe((byte)mm.periodsBlue);
        w.WriteValueSafe((byte)mm.GoalsRed); w.WriteValueSafe((byte)mm.GoalsBlue); w.WriteValueSafe(mm.suddenDeath);
        w.WriteValueSafe((sbyte)(mm.matchWinner.HasValue ? (int)mm.matchWinner.Value : -1));
        w.WriteValueSafe((sbyte)mm.HumanIndex(MatchManager.Team.Red)); w.WriteValueSafe((sbyte)mm.HumanIndex(MatchManager.Team.Blue));
        for (int t = 0; t < 2; t++)
            foreach (var r in mm.TeamRobots((MatchManager.Team)t))
            {
                var rb = r.GetComponent<Rigidbody>(); var m = r.GetComponent<moverp1>();
                w.WriteValueSafe(r.activeSelf); w.WriteValueSafe(rb.position); w.WriteValueSafe(rb.rotation); w.WriteValueSafe(m && m.Boosting);
            }
        var p = mm.Puck.GetComponent<Rigidbody>();
        w.WriteValueSafe(p.position); w.WriteValueSafe(p.rotation);
    }

    public static void ReadSnapshot(FastBufferReader r, MatchManager mm)
    {
        r.ReadValueSafe(out byte state); r.ReadValueSafe(out float stateTime); r.ReadValueSafe(out float clock);
        r.ReadValueSafe(out byte period); r.ReadValueSafe(out byte pr); r.ReadValueSafe(out byte pb);
        r.ReadValueSafe(out byte gr); r.ReadValueSafe(out byte gb); r.ReadValueSafe(out bool sudden);
        r.ReadValueSafe(out sbyte winner); r.ReadValueSafe(out sbyte hr); r.ReadValueSafe(out sbyte hb);
        var robots = new RobotState[6];
        for (int i = 0; i < 6; i++)
        {
            r.ReadValueSafe(out robots[i].active); r.ReadValueSafe(out robots[i].pos); r.ReadValueSafe(out robots[i].rot); r.ReadValueSafe(out robots[i].boost);
        }
        r.ReadValueSafe(out Vector3 puckPos); r.ReadValueSafe(out Quaternion puckRot);
        mm.NetApply((MatchManager.State)state, stateTime, clock, period, pr, pb, gr, gb, sudden,
            winner < 0 ? (MatchManager.Team?)null : (MatchManager.Team)winner, hr, hb, robots, puckPos, puckRot);
    }

    void OnSnapshot(ulong sender, FastBufferReader r)
    {
        if (!IsClient || !clientInMatch) return;
        var mm = MatchManager.Instance; if (!mm) return;
        ReadSnapshot(r, mm);
        SnapshotsReceived++;
    }

    // Banners and sounds the joiner should hear.
    public enum Ev : byte { Banner, Horn, Buzzer, Beep, Go, Kick, Impact }

    public static void SendEvent(Ev e, string text = "", Color color = default, byte a = 0, float v = 0)
    {
        var s = Instance; if (!IsHost || s == null || !PeerConnected) return;
        using var w = new FastBufferWriter(256, Allocator.Temp);
        w.WriteValueSafe((byte)e); w.WriteValueSafe(text ?? ""); w.WriteValueSafe(color); w.WriteValueSafe(a); w.WriteValueSafe(v);
        s.nm.CustomMessagingManager.SendNamedMessage(MsgEvent, s.peer, w, e == Ev.Impact || e == Ev.Kick ? NetworkDelivery.Unreliable : NetworkDelivery.Reliable);
    }

    void OnEvent(ulong sender, FastBufferReader r)
    {
        if (!IsClient) return;
        r.ReadValueSafe(out byte e); r.ReadValueSafe(out string text); r.ReadValueSafe(out Color color); r.ReadValueSafe(out byte a); r.ReadValueSafe(out float v);
        switch ((Ev)e)
        {
            case Ev.Banner: if (MatchManager.Instance) MatchManager.Instance.RaiseBanner(text, color); break;
            case Ev.Horn: GameSfx.Horn(); break;
            case Ev.Buzzer: GameSfx.Buzzer(); break;
            case Ev.Beep: GameSfx.Beep(); break;
            case Ev.Go: GameSfx.Go(); break;
            case Ev.Kick: GameSfx.Kick(); break;
            case Ev.Impact: ImpactSound.PlayRemote(a, v); break;
        }
    }

    // ---- messages: client -> host -------------------------------------------------------------

    public void SendPicks()
    {
        if (!IsClient || !PeerConnected) return;
        using var w = new FastBufferWriter(256, Allocator.Temp);
        for (int i = 0; i < 3; i++) w.WriteValueSafe(GameConfig.redRobots[i]);
        nm.CustomMessagingManager.SendNamedMessage(MsgPick, NetworkManager.ServerClientId, w, NetworkDelivery.ReliableSequenced);
    }

    void OnPick(ulong sender, FastBufferReader r)
    {
        if (!IsHost) return;
        for (int i = 0; i < 3; i++) { r.ReadValueSafe(out string id); GameConfig.redRobots[i] = RobotCatalog.Get(id) ? RobotCatalog.Get(id).id : GameConfig.redRobots[i]; }
        if (MatchManager.Instance && MatchManager.Instance.state == MatchManager.State.Menu) MatchManager.Instance.ApplyLoadouts();   // else: next match
        SendConfig();
        Changed?.Invoke();
    }

    void SendInput()
    {
        float drive = Mathf.Clamp(Input.GetAxis("Horizontal") + Input.GetAxis("Horizontal2"), -1, 1);
        float turn = Mathf.Clamp(Input.GetAxis("Vertical") + Input.GetAxis("Vertical2"), -1, 1);
        bool action = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter);
        if (TestInput != null) { var ti = TestInput(); drive = ti.x; turn = ti.y; }
        using var w = new FastBufferWriter(32, Allocator.Temp);
        w.WriteValueSafe(drive); w.WriteValueSafe(turn); w.WriteValueSafe(action); w.WriteValueSafe(switchCount);
        nm.CustomMessagingManager.SendNamedMessage(MsgInput, NetworkManager.ServerClientId, w, NetworkDelivery.UnreliableSequenced);
    }

    public static Func<Vector2> TestInput;   // automated network test drives the joiner's robot

    void OnInput(ulong sender, FastBufferReader r)
    {
        if (!IsHost) return;
        r.ReadValueSafe(out float drive); r.ReadValueSafe(out float turn); r.ReadValueSafe(out bool action); r.ReadValueSafe(out byte sw);
        Remote.Drive = drive; Remote.Turn = turn; Remote.Action = action;
        if (sw != lastSwitch) { lastSwitch = sw; if (MatchManager.Instance) MatchManager.Instance.SwitchRobot(MatchManager.Team.Red); }
    }

    // ---- helpers ------------------------------------------------------------------------------

    public static string LocalIP()
    {
        var found = new List<string>();
        try
        {
            foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                foreach (var a in ni.GetIPProperties().UnicastAddresses)
                    if (a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(a.Address))
                        found.Add(a.Address.ToString());
            }
        }
        catch { }
        return found.Count > 0 ? string.Join(" / ", found) : "127.0.0.1";
    }
}
