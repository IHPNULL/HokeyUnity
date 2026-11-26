using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// All menus and the in-match HUD, built in code with uGUI (anchored, scales to any screen).
// Title -> Setup (players, quick match / tournament, arena, difficulty, handling, rules, volume)
// -> match HUD (score, clock, periods, banners) -> pause / result screens.
public class GameUI : MonoBehaviour
{
    MatchManager mm;
    Font font;
    Sprite round, circle;
    RectTransform root;

    GameObject title, setup, robots, online, hud, pause, result;
    InputField ipField; Text onlineStatus;
    readonly List<(bool blue, Button[] buttons)> slotButtons = new();
    readonly List<(Text text, Func<string> get, Func<bool> visible)> dyn = new();   // robot screen labels
    Button startButton; Text robotsHint;
    Text setupHeader, setupHint, ladderText;
    readonly List<(GameObject row, Text value, Func<string> get, Func<bool> visible)> rows = new();

    // HUD
    RectTransform board;
    Text redScore, blueScore, clockText, periodText, bannerText, countText;
    Image[] redDots = new Image[2], blueDots = new Image[2];
    float bannerUntil;

    // Result
    Text resultTitle, resultSub, resultInfo;
    GameObject btnNext, btnAgain; Text btnAgainLabel;

    static readonly Color Bg = new Color(0.04f, 0.07f, 0.13f, 0.88f);
    static readonly Color Panel2 = new Color(1f, 1f, 1f, 0.06f);
    static readonly Color Accent = new Color(0.13f, 0.83f, 0.93f);
    static readonly Color Dim = new Color(0.72f, 0.78f, 0.88f);

    public bool InSetup => setup && setup.activeSelf;

    void Start()
    {
        mm = GetComponent<MatchManager>();
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        round = MakeRound(64, 18); circle = MakeRound(64, 32);
        Build();
        mm.StateChanged += Refresh;
        mm.Banner += ShowBanner;
        NetSession.Changed += OnNetChanged;
        Refresh();
    }

    // ---- building ----------------------------------------------------------------------------

    void Build()
    {
        if (!FindAnyObjectByType<EventSystem>())
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)).transform.SetParent(transform, false);

        var cgo = new GameObject("UI Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
        var scaler = cgo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        root = (RectTransform)cgo.transform;

        BuildTitle(); BuildSetup(); BuildRobots(); BuildOnline(); BuildHud(); BuildPause(); BuildResult();
    }

    void BuildTitle()
    {
        title = Stretch("Title", root);
        var shade = Img("Shade", title.transform, new Color(0.02f, 0.03f, 0.07f, 0.55f)); Fill(shade.rectTransform);
        shade.rectTransform.anchorMax = new Vector2(0.48f, 1f);

        var col = Column("Col", title.transform, new Vector2(0.06f, 0.5f), new Vector2(0f, 0.5f), new Vector2(720, 760), 26);
        var t = Label(col, "OMEGA", 150, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold); Size(t, 720, 150);
        var t2 = Label(col, "HOCKEY", 150, Accent, TextAnchor.MiddleLeft, FontStyle.Bold); Size(t2, 720, 140);
        var sub = Label(col, "Robôs no gelo · melhor de 3 períodos", 30, Dim, TextAnchor.MiddleLeft); Size(sub, 720, 60);
        Spacer(col, 30);
        Btn(col, "1 JOGADOR", () => OpenSetup(GameConfig.Players.One), 460, 84, true);
        Btn(col, "2 JOGADORES", () => OpenSetup(GameConfig.Players.Two), 460, 84, false);
        Btn(col, "ONLINE", OpenOnline, 460, 84, false);
        Btn(col, "SAIR", Quit, 460, 72, false, ghost: true);
        ((VerticalLayoutGroup)col.GetComponent<LayoutGroup>()).childAlignment = TextAnchor.MiddleLeft;
    }

    void BuildSetup()
    {
        setup = Stretch("Setup", root);
        var panel = Img("Panel", setup.transform, Bg, round);
        var pr = panel.rectTransform; pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f); pr.sizeDelta = new Vector2(980, 1010);
        var col = Column("Col", panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(880, 960), 10);

        setupHeader = Label(col, "1 JOGADOR", 64, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold); Size(setupHeader, 880, 90);
        Spacer(col, 6);

        bool OneP() => GameConfig.players == GameConfig.Players.One;
        bool Tour() => OneP() && GameConfig.kind == GameConfig.Kind.Tournament;

        Row(col, "Modo", () => GameConfig.kind == GameConfig.Kind.QuickMatch ? "Partida rápida" : "Torneio",
            d => GameConfig.kind = GameConfig.kind == GameConfig.Kind.QuickMatch ? GameConfig.Kind.Tournament : GameConfig.Kind.QuickMatch, OneP);
        Row(col, "Formato", () => GameConfig.FormatName,
            d => { GameConfig.format = (GameConfig.Format)(((int)GameConfig.format + d + 3) % 3); if (MatchManager.Instance) MatchManager.Instance.ApplyFormat(); }, () => true);
        Row(col, "Arena", () => ArenaTheme.Names[GameConfig.arena],
            d => { GameConfig.arena = (GameConfig.arena + d + ArenaTheme.Names.Length) % ArenaTheme.Names.Length; ArenaTheme.Apply(GameConfig.arena); }, () => !Tour());
        Row(col, "Dificuldade", () => GameConfig.Name(GameConfig.difficulty),
            d => GameConfig.difficulty = (GameConfig.Difficulty)Mathf.Clamp((int)GameConfig.difficulty + d, 0, 2), () => OneP() && !Tour());

        ladderText = Label(col, "", 26, Dim, TextAnchor.MiddleCenter); Size(ladderText, 880, 70);

        Row(col, "Velocidade", () => GameConfig.speed.ToString("0"), d => GameConfig.speed = Mathf.Clamp(GameConfig.speed + d * 100, 600, 2600), () => true);
        Row(col, "Rotação", () => GameConfig.rotation.ToString(), d => GameConfig.rotation = Mathf.Clamp(GameConfig.rotation + d, 2, 12), () => true);
        Row(col, "Minutos por período", () => GameConfig.minutesPerPeriod.ToString(), d => GameConfig.minutesPerPeriod = Mathf.Clamp(GameConfig.minutesPerPeriod + d, 1, 10), () => true);
        Row(col, "Gols por período", () => GameConfig.goalsPerPeriod.ToString(), d => GameConfig.goalsPerPeriod = Mathf.Clamp(GameConfig.goalsPerPeriod + d, 1, 10), () => true);
        Row(col, "Volume", () => Mathf.RoundToInt(GameConfig.volume * 100) + "%",
            d => { GameConfig.volume = Mathf.Clamp01(Mathf.Round(GameConfig.volume * 10 + d) / 10f); AudioListener.volume = GameConfig.volume; }, () => true);

        setupHint = Label(col, "", 26, Dim, TextAnchor.MiddleCenter); Size(setupHint, 880, 60);

        var buttons = RowGroup("Buttons", col, 880, 96, 24);
        Btn(buttons, "VOLTAR", () => { if (NetSession.Online) { LeaveOnline(); return; } setup.SetActive(false); title.SetActive(true); Select(title); }, 300, 84, false, ghost: true);
        Btn(buttons, "JOGAR", OpenRobots, 420, 84, true);
    }

    // Robot picker: one slot per robot (1x1: 1 per team, 3x3: 3), stats under each, 3x3 weight budget.
    void BuildRobots()
    {
        robots = Stretch("Robots", root);
        var panel = Img("Panel", robots.transform, Bg, round);
        var pr = panel.rectTransform; pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f); pr.sizeDelta = new Vector2(1760, 800);
        var col = Column("Col", panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1680, 740), 14);
        var h = Label(col, "ESCOLHA OS ROBÔS", 56, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold); Size(h, 1680, 76);

        var teamsRow = RowGroup("Teams", col, 1680, 470, 40);
        for (int t = 1; t >= 0; t--)
        {
            bool blue = t == 1;
            var c = new GameObject(blue ? "Blue Team" : "Red Team", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement)).transform;
            c.SetParent(teamsRow, false);
            var le = c.GetComponent<LayoutElement>(); le.preferredWidth = 800; le.preferredHeight = 470;
            var v = c.GetComponent<VerticalLayoutGroup>(); v.spacing = 6; v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = v.childControlHeight = true; v.childForceExpandWidth = v.childForceExpandHeight = false;

            var head = Label(c, "", 32, blue ? MatchManager.BlueColor : MatchManager.RedColor, TextAnchor.MiddleLeft, FontStyle.Bold); Size(head, 800, 48);
            dyn.Add((head, () => NetSession.Online
                ? (blue ? "AZUL" + (NetSession.IsHost ? " · você (anfitrião)" : " · anfitrião") : "VERMELHO" + (NetSession.IsClient ? " · você" : " · convidado"))
                : blue ? "AZUL" + (GameConfig.players == GameConfig.Players.One ? " · você" : "") : "VERMELHO" + (GameConfig.players == GameConfig.Players.One ? " · computador" : ""), () => true));
            for (int i = 0; i < 3; i++)
            {
                int slot = i;
                Func<bool> vis = () => slot < GameConfig.RobotsPerTeam;
                Row(c, (blue ? "Azul " : "Vermelho ") + (i + 1), () => RobotCatalog.Get(GameConfig.Robots(blue)[slot])?.displayName ?? "-",
                    d =>
                    {
                        var ids = GameConfig.Robots(blue); ids[slot] = RobotCatalog.Next(ids[slot], d);
                        if (mm) mm.ApplyLoadouts();
                        if (NetSession.IsClient && NetSession.Instance) NetSession.Instance.SendPicks();
                    }, vis, 800);
                slotButtons.Add((blue, rows[rows.Count - 1].row.GetComponentsInChildren<Button>(true)));
                var st = Label(c, "", 22, Dim, TextAnchor.MiddleLeft); Size(st, 800, 30);
                dyn.Add((st, () => "   " + RobotCatalog.Stats(RobotCatalog.Get(GameConfig.Robots(blue)[slot])), vis));
            }
            var budget = Label(c, "", 24, Dim, TextAnchor.MiddleLeft, FontStyle.Bold); Size(budget, 800, 40);
            dyn.Add((budget, () =>
            {
                int n = GameConfig.RobotsPerTeam; float w = RobotCatalog.TeamWeight(GameConfig.Robots(blue), n), cap = RobotCatalog.BudgetFor(n);
                return GameConfig.WithinBudget(blue) ? $"Peso do time: {w:0} / {cap:0} kg" : $"<color=#ff6b6b>Peso do time: {w:0} / {cap:0} kg: acima do limite</color>";
            }, () => GameConfig.RobotsPerTeam >= 2));
        }

        robotsHint = Label(col, "", 24, Dim, TextAnchor.MiddleCenter); Size(robotsHint, 1680, 40);
        var buttons = RowGroup("Buttons", col, 1680, 96, 24);
        Btn(buttons, "VOLTAR", () => { if (NetSession.IsClient) { LeaveOnline(); return; } robots.SetActive(false); setup.SetActive(true); RefreshSetup(); Select(setup); }, 300, 84, false, ghost: true);
        startButton = Btn(buttons, "COMEÇAR", Play, 420, 84, true);
        robots.SetActive(false);
    }

    // Online: host a game or join one by IP.
    void BuildOnline()
    {
        online = Stretch("Online", root);
        var panel = Img("Panel", online.transform, Bg, round);
        var pr = panel.rectTransform; pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f); pr.sizeDelta = new Vector2(1100, 780);
        var col = Column("Col", panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1000, 720), 18);
        var h = Label(col, "JOGAR ONLINE", 60, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold); Size(h, 1000, 84);
        var info = Label(col, "Um jogador hospeda (AZUL) e o outro entra (VERMELHO).\nPelo CÓDIGO funciona pela internet, sem configurar o roteador. Na mesma rede também dá pelo IP.", 24, Dim, TextAnchor.MiddleCenter); Size(info, 1000, 80);
        var hostRow = RowGroup("Host", col, 1000, 84, 20);
        Btn(hostRow, "HOSPEDAR (CÓDIGO)", () => { if (NetSession.Instance) NetSession.Instance.HostWithCode(); RefreshOnline(); }, 520, 84, true);
        Btn(hostRow, "HOSPEDAR NA REDE (IP)", () => { if (NetSession.Instance) NetSession.Instance.Host(); RefreshOnline(); }, 440, 84, false);
        Spacer(col, 8);
        var joinRow = RowGroup("Join", col, 1000, 84, 20);
        var bg = Img("IP Field", joinRow, new Color(1, 1, 1, 0.10f), round);
        var le = bg.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 560; le.preferredHeight = 76;
        var txt = Label(bg.transform, "", 34, Color.white, TextAnchor.MiddleLeft); Fill(txt.rectTransform); txt.rectTransform.offsetMin = new Vector2(24, 0); txt.supportRichText = false;
        var ph = Label(bg.transform, "Código (ex.: K7Q2XA) ou IP do anfitrião", 26, new Color(1, 1, 1, 0.35f), TextAnchor.MiddleLeft, FontStyle.Italic); Fill(ph.rectTransform); ph.rectTransform.offsetMin = new Vector2(24, 0);
        ipField = bg.gameObject.AddComponent<InputField>(); ipField.textComponent = txt; ipField.placeholder = ph;
        ipField.characterLimit = 39; ipField.contentType = InputField.ContentType.Standard;
        try { ipField.text = PlayerPrefs.GetString("lastHostIP", ""); } catch { }
        Btn(joinRow, "ENTRAR", Join, 300, 76, false);
        onlineStatus = Label(col, "", 28, Accent, TextAnchor.MiddleCenter, FontStyle.Bold); Size(onlineStatus, 1000, 110);
        Btn(col, "VOLTAR", () => { if (NetSession.Instance) NetSession.Instance.Shutdown(); online.SetActive(false); title.SetActive(true); Select(title); }, 300, 72, false, ghost: true);
        online.SetActive(false);
    }

    void Join()
    {
        try { PlayerPrefs.SetString("lastHostIP", ipField.text.Trim()); } catch { }
        if (NetSession.Instance) NetSession.Instance.JoinAuto(ipField.text);
        RefreshOnline();
    }

    void OpenOnline()
    {
        title.SetActive(false); online.SetActive(true);
        NetSession.Status = "Seu IP: " + NetSession.LocalIP();
        RefreshOnline(); Select(online);
    }

    void LeaveOnline()
    {
        if (NetSession.Instance) NetSession.Instance.Shutdown();
        setup.SetActive(false); robots.SetActive(false); title.SetActive(false);
        online.SetActive(true); RefreshOnline(); Select(online);
    }

    void RefreshOnline() { if (onlineStatus) onlineStatus.text = NetSession.Status; }

    // Connection events: a player joined (host -> setup), connected (joiner -> robots), or the link dropped.
    void OnNetChanged()
    {
        RefreshOnline();
        if (mm == null || mm.state != MatchManager.State.Menu) return;
        if (NetSession.Online && NetSession.PeerConnected)
        {
            GameConfig.players = GameConfig.Players.Two; GameConfig.kind = GameConfig.Kind.QuickMatch;
            if (online.activeSelf || title.activeSelf)
            {
                online.SetActive(false); title.SetActive(false);
                if (NetSession.IsHost) { setup.SetActive(true); RefreshSetup(); Select(setup); }
                else { robots.SetActive(true); RefreshSetup(); Select(robots); }
            }
            else RefreshSetup();
        }
        else if (setup.activeSelf || robots.activeSelf)
        {
            if (NetSession.IsHost) { setup.SetActive(false); robots.SetActive(false); online.SetActive(true); }   // waiting for a new player
            else if (!NetSession.Online && GameConfig.players == GameConfig.Players.Two && wasOnlineScreen) { setup.SetActive(false); robots.SetActive(false); online.SetActive(true); }
            RefreshOnline();
        }
        wasOnlineScreen = NetSession.Online || online.activeSelf;
    }
    bool wasOnlineScreen;

    void OpenRobots()
    {
        setup.SetActive(false); robots.SetActive(true);
        if (mm) mm.ApplyLoadouts();
        RefreshSetup(); Select(robots);
    }

    void BuildHud()
    {
        hud = Stretch("HUD", root);
        var b = Img("Scoreboard", hud.transform, Bg, round);
        board = b.rectTransform; board.sizeDelta = new Vector2(760, 118);

        var redBox = Img("Red", b.transform, new Color(MatchManager.RedColor.r, MatchManager.RedColor.g, MatchManager.RedColor.b, 0.9f), round).rectTransform;
        redBox.anchorMin = new Vector2(0, 0); redBox.anchorMax = new Vector2(0.32f, 1); redBox.offsetMin = new Vector2(8, 8); redBox.offsetMax = new Vector2(-4, -8);
        var blueBox = Img("Blue", b.transform, new Color(MatchManager.BlueColor.r, MatchManager.BlueColor.g, MatchManager.BlueColor.b, 0.9f), round).rectTransform;
        blueBox.anchorMin = new Vector2(0.68f, 0); blueBox.anchorMax = new Vector2(1, 1); blueBox.offsetMin = new Vector2(4, 8); blueBox.offsetMax = new Vector2(-8, -8);

        redScore = Label(redBox, "0", 70, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold); Fill(redScore.rectTransform); redScore.rectTransform.offsetMin = new Vector2(0, 18);
        blueScore = Label(blueBox, "0", 70, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold); Fill(blueScore.rectTransform); blueScore.rectTransform.offsetMin = new Vector2(0, 18);
        var rn = Label(redBox, "VERMELHO", 18, new Color(1, 1, 1, 0.85f), TextAnchor.LowerCenter, FontStyle.Bold); Fill(rn.rectTransform); rn.rectTransform.offsetMin = new Vector2(0, 6);
        var bn = Label(blueBox, "AZUL", 18, new Color(1, 1, 1, 0.85f), TextAnchor.LowerCenter, FontStyle.Bold); Fill(bn.rectTransform); bn.rectTransform.offsetMin = new Vector2(0, 6);

        clockText = Label(b.transform, "2:00", 58, Color.white, TextAnchor.UpperCenter, FontStyle.Bold);
        Fill(clockText.rectTransform); clockText.rectTransform.offsetMax = new Vector2(0, -8);
        periodText = Label(b.transform, "PERÍODO 1", 20, Dim, TextAnchor.LowerCenter, FontStyle.Bold);
        Fill(periodText.rectTransform); periodText.rectTransform.offsetMin = new Vector2(0, 30);

        for (int i = 0; i < 2; i++)
        {
            redDots[i] = Dot(b.transform, new Vector2(0.405f + i * 0.04f, 0.16f));
            blueDots[i] = Dot(b.transform, new Vector2(0.595f - i * 0.04f, 0.16f));
        }

        bannerText = Label(hud.transform, "", 110, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        Fill(bannerText.rectTransform); bannerText.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
        bannerText.rectTransform.anchorMin = new Vector2(0, 0.55f); bannerText.rectTransform.anchorMax = new Vector2(1, 0.75f);
        countText = Label(hud.transform, "", 220, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        Fill(countText.rectTransform); countText.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
        countText.rectTransform.anchorMin = new Vector2(0, 0.3f); countText.rectTransform.anchorMax = new Vector2(1, 0.6f);

        var hint = Label(hud.transform, "Esc: pausar", 22, new Color(1, 1, 1, 0.55f), TextAnchor.LowerRight);
        Fill(hint.rectTransform); hint.rectTransform.offsetMax = new Vector2(-24, 0); hint.rectTransform.offsetMin = new Vector2(0, 16);
    }

    void BuildPause()
    {
        pause = Stretch("Pause", root);
        var shade = Img("Shade", pause.transform, new Color(0, 0, 0, 0.55f)); Fill(shade.rectTransform);
        var panel = Img("Panel", pause.transform, Bg, round).rectTransform; panel.sizeDelta = new Vector2(620, 460);
        var col = Column("Col", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(560, 420), 22);
        Size(Label(col, "PAUSADO", 70, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold), 560, 110);
        Btn(col, "CONTINUAR", () => mm.TogglePause(), 420, 84, true);
        Btn(col, "SAIR PARA O MENU", ToMenu, 420, 76, false, ghost: true);
    }

    void BuildResult()
    {
        result = Stretch("Result", root);
        var shade = Img("Shade", result.transform, new Color(0, 0, 0, 0.45f)); Fill(shade.rectTransform);
        var panel = Img("Panel", result.transform, Bg, round).rectTransform; panel.sizeDelta = new Vector2(900, 700);
        var col = Column("Col", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(820, 640), 18);
        resultTitle = Label(col, "VITÓRIA!", 92, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold); Size(resultTitle, 820, 150);
        resultSub = Label(col, "", 40, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold); Size(resultSub, 820, 60);
        resultInfo = Label(col, "", 28, Dim, TextAnchor.MiddleCenter); Size(resultInfo, 820, 80);
        btnNext = Btn(col, "PRÓXIMA PARTIDA", () => mm.NextTournamentMatch(), 480, 84, true).gameObject;
        var again = Btn(col, "JOGAR DE NOVO", () => mm.Rematch(), 480, 84, false); btnAgain = again.gameObject; btnAgainLabel = again.GetComponentInChildren<Text>();
        Btn(col, "MENU", ToMenu, 480, 72, false, ghost: true);
    }

    // ---- behaviour ---------------------------------------------------------------------------

    void OpenSetup(GameConfig.Players p)
    {
        GameConfig.players = p;
        if (p == GameConfig.Players.Two) GameConfig.kind = GameConfig.Kind.QuickMatch;
        title.SetActive(false); setup.SetActive(true);
        RefreshSetup(); Select(setup);
    }

    void Play()
    {
        if (GameConfig.kind == GameConfig.Kind.Tournament) GameConfig.stage = 0;
        if (!GameConfig.WithinBudget(true) || !GameConfig.WithinBudget(false)) return;
        setup.SetActive(false); robots.SetActive(false);
        mm.StartMatch();
    }

    void ToMenu()
    {
        if (NetSession.IsClient) { NetSession.Instance.Shutdown(); mm.QuitToMenu(); online.SetActive(true); RefreshOnline(); return; }
        mm.QuitToMenu();
    }

    static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void RefreshSetup()
    {
        bool one = GameConfig.players == GameConfig.Players.One;
        setupHeader.text = NetSession.IsHost ? "ONLINE · ANFITRIÃO" : one ? "1 JOGADOR" : "2 JOGADORES";
        foreach (var r in rows) { r.row.SetActive(r.visible()); r.value.text = r.get(); }
        foreach (var d in dyn) { d.text.gameObject.SetActive(d.visible()); d.text.text = d.get(); }
        bool ok = GameConfig.WithinBudget(true) && GameConfig.WithinBudget(false);
        if (startButton) { startButton.interactable = ok && (!NetSession.IsHost || NetSession.PeerConnected); startButton.gameObject.SetActive(!NetSession.IsClient); }
        foreach (var sb in slotButtons)
            foreach (var b in sb.buttons) b.interactable = !NetSession.Online || (NetSession.IsHost ? sb.blue : !sb.blue);
        if (NetSession.IsHost && NetSession.Instance) NetSession.Instance.SendConfig();
        if (robotsHint) robotsHint.text = NetSession.IsClient ? "Arena: " + ArenaTheme.Names[GameConfig.arena] + " · " + GameConfig.FormatName + " · aguardando o anfitrião começar…"
            : !ok ? "Um time passou do limite de peso: troque um robô mais pesado."
            : GameConfig.players == GameConfig.Players.One ? "Ferramenta: Espaço ou Enter" : "Ferramenta: AZUL Espaço · VERMELHO Enter";
        bool tour = one && GameConfig.kind == GameConfig.Kind.Tournament;
        ladderText.gameObject.SetActive(tour);
        if (tour)
        {
            var parts = new List<string>();
            foreach (var s in GameConfig.Ladder) parts.Add(ArenaTheme.Names[s.arena] + " (" + GameConfig.Name(s.diff) + ")");
            ladderText.text = "3 partidas: " + string.Join("  →  ", parts);
        }
        bool three = GameConfig.RobotsPerTeam > 1;   // a team with teammates: switch keys
        setupHint.text = one ? "Você é o AZUL · dirija com W A S D ou as setas" + (three ? " · Tab troca de robô" : "")
                             : three ? "AZUL: W A S D, troca com Tab  ·  VERMELHO: setas, troca com Shift direito"
                                     : "AZUL (embaixo): W A S D    ·    VERMELHO (em cima): setas";
    }

    void Refresh()
    {
        var s = mm.state;
        bool menu = s == MatchManager.State.Menu;
        if (menu)
        {
            if (NetSession.Online && NetSession.PeerConnected)
            {
                if (!setup.activeSelf && !robots.activeSelf)
                {
                    var screen = NetSession.IsHost ? setup : robots;
                    screen.SetActive(true); RefreshSetup(); Select(screen);
                }
            }
            else if (!setup.activeSelf && !robots.activeSelf && !online.activeSelf) { title.SetActive(true); Select(title); }
        }
        else { title.SetActive(false); setup.SetActive(false); robots.SetActive(false); online.SetActive(false); }
        hud.SetActive(!menu);
        pause.SetActive(s == MatchManager.State.Paused);
        result.SetActive(s == MatchManager.State.Result);
        if (s == MatchManager.State.Paused) Select(pause);
        if (s == MatchManager.State.Result) { FillResult(); Select(result); }
        if (s == MatchManager.State.Countdown || s == MatchManager.State.Result || menu) { bannerUntil = 0; bannerText.text = ""; }

        // Scoreboard at the top; banners and the countdown sit on the 2P split line.
        board.anchorMin = board.anchorMax = new Vector2(0.5f, 1f);
        board.pivot = new Vector2(0.5f, 1f);
        board.anchoredPosition = new Vector2(0, -18);
    }

    void FillResult()
    {
        bool one = GameConfig.players == GameConfig.Players.One;
        bool blueWon = mm.matchWinner == MatchManager.Team.Blue;
        if (NetSession.Online)
        {
            bool won = blueWon == NetSession.IsHost;
            resultTitle.text = won ? "VITÓRIA!" : "DERROTA";
            resultTitle.color = blueWon ? MatchManager.BlueColor : MatchManager.RedColor;
        }
        else if (one)
        {
            resultTitle.text = mm.TournamentWon ? "CAMPEÃO!" : blueWon ? "VITÓRIA!" : "DERROTA";
            resultTitle.color = blueWon ? MatchManager.BlueColor : MatchManager.RedColor;
        }
        else
        {
            resultTitle.text = blueWon ? "AZUL VENCE!" : "VERMELHO VENCE!";
            resultTitle.color = blueWon ? MatchManager.BlueColor : MatchManager.RedColor;
        }
        resultSub.text = "Períodos   " + mm.periodsRed + "  –  " + mm.periodsBlue;
        bool tour = one && GameConfig.kind == GameConfig.Kind.Tournament;
        if (tour)
        {
            var st = GameConfig.Ladder[GameConfig.stage];
            resultInfo.text = mm.TournamentWon ? "Você venceu as 3 partidas do torneio!"
                : "Torneio · partida " + (GameConfig.stage + 1) + " de " + GameConfig.Ladder.Length + " · " + ArenaTheme.Names[st.arena] + " (" + GameConfig.Name(st.diff) + ")";
        }
        else resultInfo.text = one ? ArenaTheme.Names[GameConfig.arena] + " · " + GameConfig.Name(GameConfig.difficulty) : ArenaTheme.Names[GameConfig.arena];
        btnNext.SetActive(mm.TournamentNextAvailable && !NetSession.Online);
        btnAgain.SetActive(!mm.TournamentWon && !NetSession.IsClient);
        btnAgainLabel.text = tour && !blueWon ? "TENTAR DE NOVO" : "JOGAR DE NOVO";
    }

    void ShowBanner(string text, Color c)
    {
        bannerText.text = text; bannerText.color = c; bannerUntil = Time.unscaledTime + 2f;
    }

    void Update()
    {
        if (mm == null || hud == null) return;
        if (hud.activeSelf)
        {
            redScore.text = mm.GoalsRed.ToString();
            blueScore.text = mm.GoalsBlue.ToString();
            int secs = Mathf.CeilToInt(mm.clock);
            clockText.text = mm.suddenDeath ? "∞" : (secs / 60) + ":" + (secs % 60).ToString("00");
            clockText.color = !mm.suddenDeath && mm.clock < 10f && mm.state == MatchManager.State.Playing ? new Color(1f, 0.75f, 0.2f) : Color.white;
            periodText.text = mm.suddenDeath ? "MORTE SÚBITA" : "PERÍODO " + Mathf.Max(1, mm.period) + " DE 3";
            for (int i = 0; i < 2; i++)
            {
                redDots[i].color = i < mm.periodsRed ? MatchManager.RedColor : new Color(1, 1, 1, 0.18f);
                blueDots[i].color = i < mm.periodsBlue ? MatchManager.BlueColor : new Color(1, 1, 1, 0.18f);
            }

            if (mm.state == MatchManager.State.Countdown)
            {
                int n = Mathf.CeilToInt(3f - mm.stateTime);
                countText.text = n > 0 ? n.ToString() : "";
                countText.transform.localScale = Vector3.one * (1f + 0.35f * ((3f - mm.stateTime) % 1f));
            }
            else countText.text = "";

            float left = bannerUntil - Time.unscaledTime;
            if (left <= 0) bannerText.text = "";
            else { var c = bannerText.color; c.a = Mathf.Clamp01(left / 0.4f); bannerText.color = c; }
        }
        if (setup.activeSelf && Input.GetKeyDown(KeyCode.Escape)) { setup.SetActive(false); title.SetActive(true); Select(title); }
        else if (robots.activeSelf && Input.GetKeyDown(KeyCode.Escape) && !NetSession.IsClient) { robots.SetActive(false); setup.SetActive(true); RefreshSetup(); Select(setup); }
        else if (online.activeSelf && Input.GetKeyDown(KeyCode.Escape) && !ipField.isFocused) { LeaveOnline(); online.SetActive(false); title.SetActive(true); Select(title); }
    }

    // ---- small UI kit ------------------------------------------------------------------------

    void Row(Transform parent, string label, Func<string> get, Action<int> change, Func<bool> visible, float width = 880)
    {
        var bg = Img(label, parent, Panel2, round);
        var le = bg.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = width; le.preferredHeight = 64;
        var l = Label(bg.transform, label, 32, Color.white, TextAnchor.MiddleLeft); Fill(l.rectTransform); l.rectTransform.offsetMin = new Vector2(28, 0);
        Text value = null;
        var minus = Btn(bg.transform, "‹", () => { change(-1); RefreshSetup(); }, 64, 56, false);
        var plus = Btn(bg.transform, "›", () => { change(+1); RefreshSetup(); }, 64, 56, false);
        Anchor(minus.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(-330, 0));
        Anchor(plus.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(-46, 0));
        value = Label(bg.transform, get(), 32, Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
        Anchor(value.rectTransform, new Vector2(1, 0.5f), new Vector2(-188, 0)); value.rectTransform.sizeDelta = new Vector2(220, 60);
        rows.Add((bg.gameObject, value, get, visible));
    }

    Button Btn(Transform parent, string text, Action onClick, float w, float h, bool primary, bool ghost = false)
    {
        var img = Img(text + " Button", parent, primary ? Accent : ghost ? new Color(1, 1, 1, 0.10f) : new Color(0.20f, 0.28f, 0.42f, 1f), round);
        img.rectTransform.sizeDelta = new Vector2(w, h);
        var le = img.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = w; le.preferredHeight = h;
        var b = img.gameObject.AddComponent<Button>();
        var cb = b.colors; cb.normalColor = new Color(0.88f, 0.88f, 0.88f); cb.highlightedColor = Color.white; cb.selectedColor = Color.white;
        cb.pressedColor = new Color(0.7f, 0.7f, 0.7f); cb.colorMultiplier = 1.15f; cb.fadeDuration = 0.08f; b.colors = cb;
        b.onClick.AddListener(() => onClick());
        var t = Label(img.transform, text, h > 70 ? 34 : 30, primary ? new Color(0.02f, 0.06f, 0.12f) : Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        Fill(t.rectTransform);
        return b;
    }

    Image Dot(Transform parent, Vector2 anchor)
    {
        var d = Img("Dot", parent, Color.white, circle);
        d.type = Image.Type.Simple;
        Anchor(d.rectTransform, anchor, Vector2.zero); d.rectTransform.sizeDelta = new Vector2(16, 16);
        return d;
    }

    Text Label(Transform parent, string text, int size, Color color, TextAnchor align, FontStyle style = FontStyle.Normal)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font; t.fontSize = size; t.color = color; t.alignment = align; t.fontStyle = style; t.text = text;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow; t.raycastTarget = false;
        return t;
    }

    Image Img(string name, Transform parent, Color color, Sprite sprite = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var i = go.GetComponent<Image>(); i.color = color;
        if (sprite) { i.sprite = sprite; i.type = Image.Type.Sliced; }
        return i;
    }

    Transform Column(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 size, float spacing)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot; rt.sizeDelta = size; rt.anchoredPosition = Vector2.zero;
        var v = go.GetComponent<VerticalLayoutGroup>(); v.spacing = spacing; v.childAlignment = TextAnchor.MiddleCenter;
        v.childControlWidth = v.childControlHeight = true; v.childForceExpandWidth = v.childForceExpandHeight = false;
        return go.transform;
    }

    Transform RowGroup(string name, Transform parent, float w, float h, float spacing)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var le = go.GetComponent<LayoutElement>(); le.preferredWidth = w; le.preferredHeight = h;
        var g = go.GetComponent<HorizontalLayoutGroup>(); g.spacing = spacing; g.childAlignment = TextAnchor.MiddleCenter;
        g.childControlWidth = g.childControlHeight = true; g.childForceExpandWidth = g.childForceExpandHeight = false;
        return go.transform;
    }

    static void Spacer(Transform parent, float h)
    {
        var go = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(parent, false); go.GetComponent<LayoutElement>().preferredHeight = h;
    }

    static void Size(Text t, float w, float h) { var le = t.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = w; le.preferredHeight = h; }

    GameObject Stretch(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        Fill((RectTransform)go.transform); go.SetActive(false);
        return go;
    }

    static void Fill(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }
    static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos) { rt.anchorMin = rt.anchorMax = anchor; rt.anchoredPosition = pos; }

    static void Select(GameObject screen)
    {
        var es = EventSystem.current; if (!es) return;
        var b = screen.GetComponentInChildren<Button>();
        es.SetSelectedGameObject(b ? b.gameObject : null);
    }

    static Sprite MakeRound(int size, int r)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, r, size - r), cy = Mathf.Clamp(y + 0.5f, r, size - r);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d + 0.5f) * 255));
            }
        tex.SetPixels32(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
    }
}
