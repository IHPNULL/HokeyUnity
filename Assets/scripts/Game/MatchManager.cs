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
    int seenRed, seenBlue;
    State beforePause;
    float menuAngle;
    AudioSource music; float musicVolume = 0.7f;
    int lastBeep;

    const float CountdownTime = 3f, GoalPauseTime = 2.2f, PeriodBreakTime = 3.5f;

    void Awake()
    {
        Instance = this;
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
        AudioListener.volume = GameConfig.volume;
        ArenaTheme.Apply(GameConfig.arena);
        GameSfx.Init(gameObject);
        foreach (var r in gameObject.scene.GetRootGameObjects())
            if (r.name == "Audio Source" && r.TryGetComponent(out music)) { music.loop = true; musicVolume = music.volume; if (!music.isPlaying) music.Play(); }
        EnterMenu();
    }

    void OnDestroy() { if (Instance == this) Instance = null; moverp1.Frozen = false; Time.timeScale = 1f; }

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
        ai.enabled = false;
        SetCameras(menu: true);
        SetState(State.Menu);
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

        bool onePlayer = GameConfig.players == GameConfig.Players.One;
        var redM = red.GetComponent<moverp1>(); var blueM = blue.GetComponent<moverp1>();
        if (onePlayer)
        {
            ai.SetDifficulty(GameConfig.difficulty);
            ai.enabled = true;
            redM.input = ai;
            blueM.input = new KeyboardRobotInput(new[] { "Horizontal", "Horizontal2" }, new[] { "Vertical", "Vertical2" });
        }
        else { ai.enabled = false; redM.input = null; blueM.input = null; }
        SetCameras(menu: false);

        periodsRed = periodsBlue = 0; period = 0; matchWinner = null;
        NextPeriod();
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

    public void TogglePause()
    {
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
        float dt = Time.unscaledDeltaTime;
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
                    var c = new Vector3(634f, 10f, 400f);
                    var off = Quaternion.Euler(0, a, 0) * new Vector3(0, 0, -900f);
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
        if (music) music.volume = (s == State.Menu || s == State.Result) ? musicVolume : musicVolume * 0.3f;
        if (s == State.Countdown) lastBeep = 0;
        if (!keepTime) stateTime = 0f;
        StateChanged?.Invoke();
    }

    void SyncSeen() { seenRed = marcadorverm.pontosverm; seenBlue = marcadorazul.pontosazul; }

    void ResetRobots()
    {
        Place(red, redStart, redRot); Place(blue, blueStart, blueRot);
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

    void SetCameras(bool menu)
    {
        if (menuCam) menuCam.gameObject.SetActive(menu);
        bool one = GameConfig.players == GameConfig.Players.One;
        redCam.gameObject.SetActive(!menu && !one);
        blueCam.gameObject.SetActive(!menu);
        blueCam.rect = one ? new Rect(0, 0, 1, 1) : blueRect;
        redCam.rect = redRect;
    }
}
