using System;
using UnityEngine;

// Runs a match: best of 3 periods. A period ends when its clock runs out or a team reaches
// GameConfig.goalsPerPeriod; the team with more goals wins it, a tie goes to sudden death.
// The first team to win 2 periods wins the match. Goals are read from the puck's score
// counters (marcadorazul / marcadorverm), which hold the current period's goals.
public class MatchManager : MonoBehaviour
{
    public enum State { Menu, Countdown, Playing, GoalPause, PeriodBreak, Paused, Result }
    public enum Team { Red = 0, Blue = 1 }

    public static MatchManager Instance { get; private set; }

    public State state { get; private set; } = State.Menu;
    public int period { get; private set; }          // 1..3
    public int periodsRed { get; private set; }
    public int periodsBlue { get; private set; }
    public float clock { get; private set; }          // seconds left in the period
    public bool suddenDeath { get; private set; }
    public float stateTime { get; private set; }      // seconds since state changed
    public Team? matchWinner { get; private set; }
    public int GoalsRed => marcadorverm.pontosverm;
    public int GoalsBlue => marcadorazul.pontosazul;

    // UI hooks.
    public event Action<string, Color> Banner;
    public event Action StateChanged;

    public static readonly Color RedColor = new Color(0.95f, 0.30f, 0.30f), BlueColor = new Color(0.30f, 0.58f, 1f);

    GameObject jogando, red, blue, puckGo;
    Camera redCam, blueCam, menuCam;
    Rect redRect, blueRect;
    AIRobotInput ai;
    Vector3 redStart, blueStart; Quaternion redRot, blueRot;

    // 3x3: robots [0] are the scene's robots, [1] and [2] are copies. human[t] = index the
    // team's player drives (-1 = all AI, e.g. red in 1 Player).
    readonly GameObject[][] teams = new GameObject[2][];
    readonly int[] human = new int[2], attacker = { -1, -1 };
    Transform arena, placar; Vector3 placarPos; Light[] floods; Vector3[] floodPos;
    public int RobotsPerTeam => GameConfig.RobotsPerTeam;
    public GameObject[] TeamRobots(Team t) => teams[(int)t];
    public int HumanIndex(Team t) => human[(int)t];
    public GameObject Puck => puckGo;
    public void RaiseBanner(string text, Color c) => Banner?.Invoke(text, c);
    int seenRed, seenBlue;
    State beforePause;
    float menuAngle;
    AudioSource music; float musicVolume = 0.7f;
    int lastBeep;

    const float CountdownTime = 3f, GoalPauseTime = 2.2f, PeriodBreakTime = 3.5f;

    void Awake()
    {
        Instance = this;
        if (!GetComponent<NetSession>()) gameObject.AddComponent<NetSession>();
        Banner += (t, c) => NetSession.SendEvent(NetSession.Ev.Banner, t, c);   // host -> joiner
        jogando = Find("JOGANDO");
        red = Find("unidadeverm"); blue = Find("Unidadeazul"); puckGo = Find("PUCK");
        redCam = red.GetComponentInChildren<Camera>(true); blueCam = blue.GetComponentInChildren<Camera>(true);
        redRect = redCam.rect; blueRect = blueCam.rect;
        redStart = red.transform.position; redRot = red.transform.rotation;
        blueStart = blue.transform.position; blueRot = blue.transform.rotation;
        var mc = transform.Find("Menu Camera"); if (mc) menuCam = mc.GetComponent<Camera>();
        if (!red.TryGetComponent(out ai)) ai = red.AddComponent<AIRobotInput>();
        ai.puck = puckGo.transform;
        ai.enabled = false;
        arena = jogando.transform.Find("arena");
        puckGo.transform.SetParent(jogando.transform, true);   // the rink stretches in 3x3, the puck doesn't
        var pl = jogando.transform.Find("cenario externo/placar"); if (pl) { placar = pl; placarPos = pl.position; }
        var lights = jogando.transform.Find("Lighting");
        floods = lights ? System.Array.FindAll(lights.GetComponentsInChildren<Light>(true), l => l.type == LightType.Spot) : new Light[0];
        floodPos = System.Array.ConvertAll(floods, l => l.transform.position);
        teams[0] = new[] { red, Clone(red, 2), Clone(red, 3) };
        teams[1] = new[] { blue, Clone(blue, 2), Clone(blue, 3) };
        AudioListener.volume = GameConfig.volume;
        Rink.Scale = Vector2.one;               // arena decorations are built at the scene's size
        if (arena) arena.localScale = Vector3.one;
        SetupRealSizeGoals();
        ArenaTheme.Apply(GameConfig.arena);
        ApplyFormat();
        GameSfx.Init(gameObject);
        foreach (var r in gameObject.scene.GetRootGameObjects())
            if (r.name == "Audio Source" && r.TryGetComponent(out music)) { music.loop = true; musicVolume = music.volume; if (!music.isPlaying) music.Play(); }
        EnterMenu();
    }

    void OnDestroy() { if (Instance == this) Instance = null; moverp1.Frozen = false; Time.timeScale = 1f; }

    static GameObject Clone(GameObject src, int n)
    {
        var c = Instantiate(src, src.transform.parent);
        c.name = src.name + " " + n;
        c.SetActive(false);
        return c;
    }

    // 1x1, 2x2 or 3x3 robots a side, always on the big rink. Called on start, from the setup screen and per match.
    public void ApplyFormat()
    {
        Rink.Scale = Rink.BigScale;
        if (arena) arena.localScale = new Vector3(Rink.Scale.x, 1f, Rink.Scale.y);
        Rink.ApplyKeeps();
        RinkBuilder.Rebuild();
        // Scoreboard: hung from the roof above the far boards, facing the rink (no room for its pole).
        if (placar)
        {
            placar.gameObject.SetActive(true);
            var sb = placar.GetComponent<ScoreboardDisplay>();
            var hang = new Vector3(Rink.Center.x, ScoreboardDisplay.HangHeight, Rink.MaxZ + ScoreboardDisplay.HangOutside);
            if (sb) sb.HangAt(hang); else placar.position = placarPos;
        }
        for (int i = 0; i < floods.Length; i++) { var f = floodPos[i]; floods[i].transform.position = Rink.W(f.x, f.z, f.y); }
        for (int t = 0; t < 2; t++)
            for (int i = 1; i < 3; i++) teams[t][i].SetActive(i < RobotsPerTeam);
        ApplyLoadouts();
        ResetRobots(); ResetPuck();
    }

    // Goals keep their real size on the stretched rink: each scoring trigger moves under its goal
    // model, and the models undo the stretch. Board pieces fill the gap the stretched end-board
    // opening leaves beside a goal.
    void SetupRealSizeGoals()
    {
        foreach (var n in new[] { "gol", "gol (1)" })
        {
            var vis = arena ? arena.Find(n + " Visual") : null; var trig = arena ? arena.Find(n) : null;
            if (!vis) continue;
            if (trig) trig.SetParent(vis, true);
            Rink.KeepSize(vis);
        }
        // Rounded boards and floor, generated per rink size (the scene's stretched boards and
        // square floor are retired; the floor plane keeps only its collider).
        var boards = arena ? arena.Find("policarbonato") : null;
        var plane = arena ? arena.Find("Plane") : null;
        RinkBuilder.Setup(jogando.transform,
            plane ? plane.GetComponent<Renderer>().sharedMaterial : null,
            boards ? boards.GetComponent<Renderer>().sharedMaterial : null,
            boards ? boards.GetComponent<Collider>().sharedMaterial : null);
        if (boards) { boards.GetComponent<Renderer>().enabled = false; boards.GetComponent<Collider>().enabled = false; }
        if (plane) plane.GetComponent<Renderer>().enabled = false;
    }

    // Robot picks (GameConfig.blueRobots / redRobots) -> stats, size and tool on each robot.
    public void ApplyLoadouts()
    {
        for (int t = 0; t < 2; t++)
        {
            var ids = GameConfig.Robots(t == 1);
            for (int i = 0; i < 3; i++) RobotLoadout.Apply(teams[t][i], RobotCatalog.Get(ids[i]), i);
        }
    }

    static GameObject Find(string n)
    {
        foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>())
            if (g.name == n && g.scene.IsValid()) return g;
        throw new Exception("MatchManager: missing " + n);
    }

    // ---- flow -------------------------------------------------------------------------------

    public void EnterMenu()
    {
        Time.timeScale = 1f;
        jogando.SetActive(true);              // the arena (and crowd) is the menu backdrop
        ResetRobots(); ResetPuck();
        marcadorazul.pontosazul = 0; marcadorverm.pontosverm = 0; SyncSeen();
        moverp1.Frozen = true;
        foreach (var tm in teams) foreach (var r in tm) if (r.TryGetComponent(out AIRobotInput a)) a.enabled = false;
        SetCameras(menu: true);
        SetState(State.Menu);
        if (NetSession.IsHost && NetSession.Instance) NetSession.Instance.SendConfig();
    }

    public void StartMatch()
    {
        if (GameConfig.kind == GameConfig.Kind.Tournament)
        {
            var s = GameConfig.Ladder[Mathf.Clamp(GameConfig.stage, 0, GameConfig.Ladder.Length - 1)];
            GameConfig.arena = s.arena; GameConfig.difficulty = s.diff;
        }
        ArenaTheme.Apply(GameConfig.arena);
        AudioListener.volume = GameConfig.volume;

        ApplyFormat();
        if (NetSession.IsClient) return;   // the host runs the match
        bool onePlayer = GameConfig.players == GameConfig.Players.One && !NetSession.Online;
        human[0] = onePlayer ? -1 : 0; human[1] = 0;
        attacker[0] = attacker[1] = -1;
        for (int t = 0; t < 2; t++)
            for (int i = 0; i < 3; i++)
            {
                var r = teams[t][i];
                var a = Ai(r); a.puck = puckGo.transform; a.blueTeam = t == 1; a.role = AIRobotInput.Role.Attack;
                a.SetDifficulty(GameConfig.difficulty);
            }
        for (int t = 0; t < 2; t++) Control(t, human[t]);
        SetCameras(menu: false);

        periodsRed = periodsBlue = 0; period = 0; matchWinner = null;
        NextPeriod();
        if (NetSession.IsHost && NetSession.Instance) NetSession.Instance.SendConfig();
    }

    void NextPeriod()
    {
        period++;
        suddenDeath = false;
        clock = GameConfig.minutesPerPeriod * 60f;
        marcadorazul.pontosazul = 0; marcadorverm.pontosverm = 0; SyncSeen();
        Kickoff();
    }

    void Kickoff()
    {
        ResetRobots(); ResetPuck();
        moverp1.Frozen = true;
        SetState(State.Countdown);
    }

    AIRobotInput Ai(GameObject r) => r.TryGetComponent(out AIRobotInput a) ? a : r.AddComponent<AIRobotInput>();

    IRobotInput Keys(int t)
    {
        if (NetSession.IsHost && t == 0 && NetSession.Instance) return NetSession.Instance.Remote;   // the joiner
        if (GameConfig.players == GameConfig.Players.One || NetSession.Online)
            return new KeyboardRobotInput(new[] { "Horizontal", "Horizontal2" }, new[] { "Vertical", "Vertical2" }, new[] { KeyCode.Space, KeyCode.Return, KeyCode.KeypadEnter });
        var m = teams[t][0].GetComponent<moverp1>();
        // Tool key: blue Space, red Enter (Mac keyboards have no right Ctrl).
        var keys = t == 1 ? new[] { KeyCode.Space } : new[] { KeyCode.Return, KeyCode.KeypadEnter, KeyCode.RightControl };
        return new KeyboardRobotInput(new[] { m.horizontal }, new[] { m.vertical }, keys);
    }

    // Team t's player takes robot h (-1 = none); every other active robot of the team is AI.
    void Control(int t, int h)
    {
        human[t] = h;
        for (int i = 0; i < RobotsPerTeam; i++)
        {
            var r = teams[t][i]; var m = r.GetComponent<moverp1>(); var a = Ai(r);
            bool me = i == h;
            a.enabled = !me;
            m.input = me ? Keys(t) : a;
        }
    }

    // 3x3: Tab (blue) / right Shift (red) hands the player the teammate nearest the puck.
    public void SwitchRobot(Team team)
    {
        int t = (int)team;
        if (RobotsPerTeam < 2 || human[t] < 0) return;
        int best = human[t]; float bd = float.MaxValue;
        for (int i = 0; i < RobotsPerTeam; i++)
        {
            if (i == human[t]) continue;
            float d = Vector3.Distance(teams[t][i].transform.position, puckGo.transform.position);
            if (d < bd) { bd = d; best = i; }
        }
        Control(t, best);
        if (attacker[t] == best) attacker[t] = -1;
        SetCameras(menu: false);
    }

    // 3x3 roles: the AI nearest the puck attacks (unless the player is nearer), the AI nearest
    // our net defends, the last one supports. A little hysteresis stops the roles flickering.
    void AssignRoles(int t)
    {
        int n = RobotsPerTeam; if (n < 2) return;
        Vector3 p = puckGo.transform.position; p.y = 0;
        Vector3 own = t == 1 ? Rink.BlueGoal : Rink.RedGoal;
        float D(int i) { var q = teams[t][i].transform.position; q.y = 0; return Vector3.Distance(q, p); }
        int near = 0; for (int i = 1; i < n; i++) if (D(i) < D(near)) near = i;
        int cur = attacker[t];
        if (cur < 0 || cur == human[t] || D(near) < D(cur) - 120f) cur = near;
        if (cur == human[t]) cur = -1;
        attacker[t] = cur;
        int defender = -1; float dd = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            if (i == cur || i == human[t]) continue;
            float d = Vector3.Distance(teams[t][i].transform.position, own);
            if (d < dd) { dd = d; defender = i; }
        }
        for (int i = 0; i < n; i++)
        {
            if (i == human[t]) continue;
            Ai(teams[t][i]).role = i == cur ? AIRobotInput.Role.Attack : i == defender ? AIRobotInput.Role.Defend : AIRobotInput.Role.Support;
        }
    }

    void FixedUpdate()
    {
        if (state != State.Playing && state != State.Countdown) return;
        AssignRoles(0); AssignRoles(1);
    }

    public void TogglePause()
    {
        if (NetSession.Online) return;   // no pausing someone else's game
        if (state == State.Paused) { Time.timeScale = 1f; SetState(beforePause, keepTime: true); }
        else if (state == State.Playing || state == State.Countdown || state == State.GoalPause)
        { beforePause = state; Time.timeScale = 0f; SetState(State.Paused, keepTime: true); }
    }

    public void QuitToMenu() => EnterMenu();

    // Tests: jump the period clock.
    public void SetClock(float seconds) => clock = seconds;

    // Result screen buttons.
    public bool TournamentNextAvailable => GameConfig.kind == GameConfig.Kind.Tournament && matchWinner == Team.Blue && GameConfig.stage < GameConfig.Ladder.Length - 1;
    public bool TournamentWon => GameConfig.kind == GameConfig.Kind.Tournament && matchWinner == Team.Blue && GameConfig.stage >= GameConfig.Ladder.Length - 1;
    public void NextTournamentMatch() { GameConfig.stage++; StartMatch(); }
    public void Rematch() => StartMatch();

    // ---- per frame ---------------------------------------------------------------------------

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)) TogglePause();
        if (state == State.Playing || state == State.Countdown)
        {
            if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.LeftShift)) SwitchRobot(Team.Blue);
            if (Input.GetKeyDown(KeyCode.RightShift)) SwitchRobot(GameConfig.players == GameConfig.Players.One ? Team.Blue : Team.Red);
        }
        float dt = Time.unscaledDeltaTime;
        if (NetSession.IsClient && remoteView) { SmoothRemote(); if (state != State.Menu) return; }
        if (state != State.Paused) stateTime += Time.deltaTime;

        switch (state)
        {
            case State.Menu:
                SyncSeen();
                if (menuCam)
                {
                    // Slow pendulum over the far side and both ends (the scoreboard pole stands on the near side).
                    menuAngle += dt;
                    float a = 180f + 105f * Mathf.Sin(menuAngle * 0.07f);
                    var c = Rink.Center + Vector3.up * 10f;
                    var off = Quaternion.Euler(0, a, 0) * new Vector3(0, 0, -900f * Mathf.Max(Rink.Scale.x, Rink.Scale.y));
                    menuCam.transform.position = c + off + Vector3.up * 820f;
                    menuCam.transform.LookAt(c + Vector3.up * 40f);
                }
                break;

            case State.Countdown:
                SyncSeen();
                int n = Mathf.CeilToInt(CountdownTime - stateTime);
                if (n != lastBeep && n > 0) { lastBeep = n; GameSfx.Beep(); }
                if (stateTime >= CountdownTime) { moverp1.Frozen = false; lastBeep = 0; GameSfx.Go(); SetState(State.Playing); Banner?.Invoke("VALENDO!", Color.white); }
                break;

            case State.Playing:
                if (!suddenDeath)
                {
                    clock -= Time.deltaTime;
                    if (clock <= 0f) { clock = 0f; EndPeriodOnTime(); break; }
                }
                CheckGoals();
                break;

            case State.GoalPause:
                SyncSeen();
                if (stateTime >= GoalPauseTime) Kickoff();
                break;

            case State.PeriodBreak:
                SyncSeen();
                if (stateTime >= PeriodBreakTime) NextPeriod();
                break;

            case State.Result:
                SyncSeen();
                break;
        }
    }

    void CheckGoals()
    {
        int r = marcadorverm.pontosverm, b = marcadorazul.pontosazul;
        if (r == seenRed && b == seenBlue) return;
        Team scorer = r > seenRed ? Team.Red : Team.Blue;
        SyncSeen();
        GameSfx.Horn();
        Banner?.Invoke(scorer == Team.Red ? "GOL DO VERMELHO!" : "GOL DO AZUL!", scorer == Team.Red ? RedColor : BlueColor);

        if (suddenDeath || r >= GameConfig.goalsPerPeriod || b >= GameConfig.goalsPerPeriod) { EndPeriod(scorer); return; }
        moverp1.Frozen = true;
        SetState(State.GoalPause);
    }

    void EndPeriodOnTime()
    {
        int r = marcadorverm.pontosverm, b = marcadorazul.pontosazul;
        if (r == b)
        {
            GameSfx.Buzzer();
            suddenDeath = true;
            Banner?.Invoke("MORTE SÚBITA!", new Color(1f, 0.8f, 0.2f));
            Kickoff();
            return;
        }
        EndPeriod(r > b ? Team.Red : Team.Blue);
    }

    void EndPeriod(Team winner)
    {
        if (winner == Team.Red) periodsRed++; else periodsBlue++;
        moverp1.Frozen = true;
        GameSfx.Buzzer();
        if (periodsRed >= 2 || periodsBlue >= 2)
        {
            matchWinner = periodsRed >= 2 ? Team.Red : Team.Blue;
            SetState(State.Result);
            return;
        }
        Banner?.Invoke((winner == Team.Red ? "VERMELHO" : "AZUL") + " VENCE O " + period + "º PERÍODO", winner == Team.Red ? RedColor : BlueColor);
        SetState(State.PeriodBreak);
    }

    // ---- helpers -----------------------------------------------------------------------------

    void SetState(State s, bool keepTime = false)
    {
        state = s;
        if (music)
        {
            bool calm = s == State.Menu || s == State.Result;
            music.volume = calm ? musicVolume : musicVolume * 0.45f;
            // Menu track in the menus, the match track while playing (free Asset Store music).
            var bank = SoundBank.Get();
            var want = bank ? (calm ? bank.menuMusic : bank.matchMusic) : null;
            if (want && music.clip != want && s != State.Paused) { music.clip = want; music.Play(); }
        }
        if (s == State.Countdown) lastBeep = 0;
        if (!keepTime) stateTime = 0f;
        StateChanged?.Invoke();
    }

    void SyncSeen() { seenRed = marcadorverm.pontosverm; seenBlue = marcadorazul.pontosazul; }

    // Kickoff spots in 1x1 coordinates (mapped onto the rink with Rink.W): [0] center forward, wings behind.
    static readonly Vector2[] RedSpots3 = { new Vector2(1343f, 400f), new Vector2(1470f, 160f), new Vector2(1470f, 640f) };
    static readonly Vector2[] RedSpots2 = { new Vector2(1343f, 270f), new Vector2(1343f, 530f) };

    void ResetRobots()
    {
        if (teams[0] == null) { Place(red, redStart, redRot); Place(blue, blueStart, blueRot); return; }
        var spots = RobotsPerTeam == 3 ? RedSpots3 : RobotsPerTeam == 2 ? RedSpots2 : null;
        for (int i = 0; i < RobotsPerTeam; i++)
        {
            Vector3 rp = Rink.W(redStart.x, redStart.z, redStart.y), bp = Rink.W(blueStart.x, blueStart.z, blueStart.y);
            if (spots != null)
            {
                var s = spots[i];
                rp = Rink.W(s.x, s.y, redStart.y);
                bp = Rink.W(2f * Rink.Center.x - s.x, 2f * Rink.Center.z - s.y, blueStart.y);
            }
            Place(teams[0][i], rp, redRot); Place(teams[1][i], bp, blueRot);
        }
    }

    static void Place(GameObject g, Vector3 pos, Quaternion rot)
    {
        var rb = g.GetComponent<Rigidbody>();
        if (rb && !rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        if (rb) { rb.position = pos; rb.rotation = rot; }
        g.transform.SetPositionAndRotation(pos, rot);
    }

    void ResetPuck()
    {
        var pos = puck.originalPos != Vector3.zero ? puck.originalPos : new Vector3(668.3125f, 67.53125f, 390.6067f);
        Place(puckGo, pos, puckGo.transform.rotation);
    }

    // Each robot carries its own chase camera; the ones of the robots the players drive are on.
    void SetCameras(bool menu)
    {
        if (menuCam) menuCam.gameObject.SetActive(menu);
        bool online = NetSession.Online;
        bool one = GameConfig.players == GameConfig.Players.One || online;   // one full-screen view
        int myTeam = NetSession.IsClient ? 0 : 1;
        for (int t = 0; t < 2; t++)
            for (int i = 0; i < 3; i++)
            {
                var cam = teams[t][i].GetComponentInChildren<Camera>(true); if (!cam) continue;
                bool teamShown = online ? t == myTeam : (t == 1 || !one);
                bool on = !menu && i == human[t] && teamShown;
                cam.gameObject.SetActive(on);
                cam.rect = one ? new Rect(0, 0, 1, 1) : t == 1 ? blueRect : redRect;
            }
        blueCam = teams[1][Mathf.Max(0, human[1])].GetComponentInChildren<Camera>(true);
    }

    // ---- online: the joiner shows the host's snapshots ---------------------------------------

    bool remoteView;
    readonly Vector3[] netPos = new Vector3[7]; readonly Quaternion[] netRot = new Quaternion[7];
    bool netHasPose;

    // Joiner: robots and puck stop simulating and follow the host.
    public void SetRemoteView(bool on)
    {
        remoteView = on; netHasPose = false;
        foreach (var tm in teams)
            foreach (var r in tm)
            {
                var rb = r.GetComponent<Rigidbody>();
                rb.isKinematic = on; rb.interpolation = on ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
                foreach (var b in new Behaviour[] { r.GetComponent<moverp1>(), r.GetComponent<RobotTools>() }) if (b) b.enabled = !on;
                if (r.TryGetComponent(out AIRobotInput a)) a.enabled = false;
            }
        var prb = puckGo.GetComponent<Rigidbody>();
        prb.isKinematic = on; prb.interpolation = on ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
        if (puckGo.TryGetComponent(out puck pk)) pk.enabled = !on;
        if (on) { moverp1.Frozen = true; human[0] = 0; human[1] = 0; SetCameras(menu: false); }
    }

    public void NetApply(State s, float sTime, float clk, int per, int pr, int pb, int gr, int gb, bool sudden, Team? winner,
                         int hr, int hb, NetSession.RobotState[] robots, Vector3 puckPos, Quaternion puckRot)
    {
        bool camChange = hr != human[0] || hb != human[1];
        human[0] = hr; human[1] = hb;
        clock = clk; period = per; periodsRed = pr; periodsBlue = pb; suddenDeath = sudden; matchWinner = winner;
        marcadorverm.pontosverm = gr; marcadorazul.pontosazul = gb; SyncSeen();
        if (s != state) { SetState(s); camChange = true; }
        stateTime = sTime;
        for (int i = 0; i < 6; i++)
        {
            var r = teams[i / 3][i % 3];
            if (r.activeSelf != robots[i].active) r.SetActive(robots[i].active);
            netPos[i] = robots[i].pos; netRot[i] = robots[i].rot;
            if (r.TryGetComponent(out RobotTools tl) && tl.flames != null) foreach (var f in tl.flames) if (f) f.gameObject.SetActive(robots[i].boost);
        }
        netPos[6] = puckPos; netRot[6] = puckRot;
        if (!netHasPose) { netHasPose = true; SmoothRemote(1f); }
        if (camChange) SetCameras(menu: s == State.Menu);
    }

    // Ease toward the latest snapshot (snapshots arrive ~25/s; frames are faster).
    void SmoothRemote(float k = -1f)
    {
        if (!netHasPose) return;
        if (k < 0) k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 18f);
        for (int i = 0; i < 7; i++)
        {
            var tr = i < 6 ? teams[i / 3][i % 3].transform : puckGo.transform;
            tr.SetPositionAndRotation(Vector3.Lerp(tr.position, netPos[i], k), Quaternion.Slerp(tr.rotation, netRot[i], k));
        }
    }
}
