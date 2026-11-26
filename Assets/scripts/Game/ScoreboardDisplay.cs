using UnityEngine;

// Turns the plain scoreboard box (placar/Box001) into a neon jumbotron: bevelled gunmetal housing,
// glossy black screens on both faces, team-coloured neon frames (blue half / red half), a crown
// light bar, and a live 7-segment display: blue score, period clock, red score, current period and
// periods won. The scoring team's frame strobes for a few seconds after a goal.
// Built when the arena activates; the old renderers are hidden, the pole and joints are repainted.
public class ScoreboardDisplay : MonoBehaviour
{
    public float flashSeconds = 3f;

    Transform board;
    Mesh display;
    Material frameBlue, frameRed;
    Color frameBlueBase, frameRedBase;
    Vector2 screen;
    float faceZ;
    string shown;
    int lastBlue, lastRed, flashTeam = -1;   // 0 red, 1 blue
    float flashUntil;

    public string Shown => shown;            // tests: last rendered state key
    public int DisplayTriangles { get; private set; }

    static readonly Color BlueGlow = new Color(0.08f, 0.42f, 1f) * 2.4f, RedGlow = new Color(1f, 0.07f, 0.04f) * 2.4f, WhiteGlow = new Color(1f, 0.9f, 0.7f) * 1.7f;

    void Awake()
    {
        var src = transform.Find("Box001");
        var srcR = src ? src.GetComponent<Renderer>() : null;
        if (srcR == null) { enabled = false; return; }
        var b = srcR.bounds;
        foreach (var n in new[] { "Box001", "Box002" }) { var t = transform.Find(n); if (t && t.TryGetComponent(out Renderer r)) r.enabled = false; }

        var gunmetal = Mat("Scoreboard Housing", new Color(0.16f, 0.17f, 0.19f), 0.8f, 0.6f);
        foreach (var n in new[] { "Cylinder001", "Cylinder002", "Sphere001", "Sphere002" })
        { var t = transform.Find(n); if (t && t.TryGetComponent(out Renderer r)) r.sharedMaterial = gunmetal; }

        // World-axis frame at the old screen's centre: X across, Y up, ±Z = the two faces.
        board = new GameObject("Jumbotron").transform;
        board.SetPositionAndRotation(b.center, Quaternion.identity);
        board.SetParent(transform, true);
        screen = new Vector2(b.size.x, b.size.y);
        float depth = b.size.z + 14f;
        faceZ = depth / 2f;

        frameBlueBase = BlueGlow * 0.6f; frameRedBase = RedGlow * 0.6f;
        frameBlue = Mat("Scoreboard Neon Blue", new Color(0.1f, 0.3f, 0.6f), 0f, 0.9f, frameBlueBase);
        frameRed = Mat("Scoreboard Neon Red", new Color(0.6f, 0.08f, 0.06f), 0f, 0.9f, frameRedBase);
        var crown = Mat("Scoreboard Crown", Color.white, 0f, 0.9f, WhiteGlow * 0.8f);
        var glass = Mat("Scoreboard Screen", new Color(0.012f, 0.014f, 0.02f), 0.2f, 0.95f);

        // Housing (bevelled), screens, frames, crown: one mesh, one submesh per material.
        var m = new FacetMesh();
        float W = screen.x, H = screen.y, rim = 12f;
        m.Sub(0).Panel(Vector3.zero, new Vector3(W + rim * 2, H + rim * 2, depth), 7f);
        foreach (int s in new[] { 1, -1 })
        {
            m.Sub(1);
            float z = s * (faceZ + 0.6f);
            var nrm = new Vector3(0, 0, s);
            m.Flat(nrm, new Vector3(-W / 2, -H / 2, z), new Vector3(W / 2, -H / 2, z), new Vector3(W / 2, H / 2, z), new Vector3(-W / 2, H / 2, z));
            // Neon frame: left half blue, right half red (as seen from that face).
            float fz = s * (faceZ + 2.5f), t = 6f;
            for (int side = 0; side < 2; side++)
            {
                m.Sub(side == 0 ? 2 : 3);
                float xs = side == 0 ? -1 : 1;        // viewer's left / right
                float lx = -xs * s;                   // local x of that side on this face (viewer facing -s*Z has right = -s*X)
                m.Box(new Vector3(lx * W / 4, H / 2 + t / 2, fz), new Vector3(W / 2 - 2f, t, 5f));   // top half
                m.Box(new Vector3(lx * W / 4, -H / 2 - t / 2, fz), new Vector3(W / 2 - 2f, t, 5f));  // bottom half
                m.Box(new Vector3(lx * (W / 2 + t / 2), 0, fz), new Vector3(t, H + t * 2, 5f));      // side
            }
        }
        m.Sub(4).Box(new Vector3(0, H / 2 + rim + 5f, 0), new Vector3(W * 0.55f, 6f, depth * 0.6f));     // crown light bar
        var go = new GameObject("Housing", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(board, false);
        go.GetComponent<MeshFilter>().sharedMesh = m.ToMesh("Scoreboard Housing");
        go.GetComponent<MeshRenderer>().sharedMaterials = new[] { gunmetal, glass, frameBlue, frameRed, crown };

        // Live display.
        var dgo = new GameObject("Display", typeof(MeshFilter), typeof(MeshRenderer));
        dgo.transform.SetParent(board, false);
        display = new Mesh { name = "Scoreboard Display" };
        display.MarkDynamic();
        dgo.GetComponent<MeshFilter>().sharedMesh = display;
        var dr = dgo.GetComponent<MeshRenderer>();
        dr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        dr.sharedMaterials = new[]
        {
            Mat("LED Off", new Color(0.05f, 0.055f, 0.07f), 0f, 0.6f, new Color(0.03f, 0.035f, 0.05f)),
            Mat("LED Blue", new Color(0.2f, 0.5f, 1f), 0f, 0.8f, BlueGlow),
            Mat("LED Red", new Color(1f, 0.2f, 0.15f), 0f, 0.8f, RedGlow),
            Mat("LED White", new Color(1f, 0.95f, 0.85f), 0f, 0.8f, WhiteGlow),
        };
        lastBlue = marcadorazul.pontosazul; lastRed = marcadorverm.pontosverm;
        Refresh(true);
    }

    void Update()
    {
        if (display == null) return;
        if (marcadorazul.pontosazul > lastBlue) Flash(1);
        if (marcadorverm.pontosverm > lastRed) Flash(0);
        lastBlue = marcadorazul.pontosazul; lastRed = marcadorverm.pontosverm;

        // Goal strobe on the scorer's half of the frame.
        float k = Time.time < flashUntil ? (Mathf.Repeat(Time.time * 6f, 1f) < 0.5f ? 4f : 0.6f) : 1f;
        frameBlue.SetColor("_EmissionColor", frameBlueBase * (flashTeam == 1 ? k : 1f));
        frameRed.SetColor("_EmissionColor", frameRedBase * (flashTeam == 0 ? k : 1f));
        Refresh(false);
    }

    void Flash(int team) { flashTeam = team; flashUntil = Time.time + flashSeconds; }

    void Refresh(bool force)
    {
        var mm = MatchManager.Instance;
        float clock = mm ? mm.clock : GameConfig.minutesPerPeriod * 60f;
        if (mm && mm.state == MatchManager.State.Menu) clock = GameConfig.minutesPerPeriod * 60f;
        int secs = Mathf.CeilToInt(clock);
        bool sudden = mm && mm.suddenDeath;
        bool blinkOff = sudden && Mathf.Repeat(Time.time, 1f) < 0.35f;
        int period = mm ? Mathf.Max(1, mm.period) : 1;
        int pb = mm ? mm.periodsBlue : 0, pr = mm ? mm.periodsRed : 0;
        string key = marcadorazul.pontosazul + "|" + marcadorverm.pontosverm + "|" + secs + "|" + period + "|" + pb + "|" + pr + "|" + sudden + blinkOff;
        if (!force && key == shown) return;
        shown = key;

        var m = new FacetMesh();
        foreach (int s in new[] { 1, -1 }) Face(m, s, marcadorazul.pontosazul, marcadorverm.pontosverm, secs, sudden, blinkOff, period, pb, pr);
        m.Sub(3);   // make sure all 4 submeshes exist
        m.ToMesh("Scoreboard Display", display);
        DisplayTriangles = m.Triangles;
    }

    // One face. Screen coordinates: x to the viewer's right, y up, origin at the screen centre.
    void Face(FacetMesh m, int s, int blue, int red, int secs, bool sudden, bool blinkOff, int period, int pb, int pr)
    {
        const int Off = 0, Blue = 1, Red = 2, White = 3;
        var right = new Vector3(-s, 0, 0); var up = Vector3.up; var nrm = new Vector3(0, 0, s);
        var o = new Vector3(0, 0, s * (faceZ + 1.2f));
        float W = screen.x, H = screen.y;

        // Scores: big digits, blue on the viewer's left, red on the right.
        float dh = H * 0.42f, dw = dh * 0.48f, th = dh * 0.13f, sp = dw * 0.35f;
        void Number(int value, float cx, float cy, int sub)
        {
            value = Mathf.Clamp(value, 0, 99);
            float total = dw * 2 + sp;
            float x0 = cx - total / 2;
            int tens = value / 10, ones = value % 10;
            if (value < 10)   // single digit: centred in the slot, so it never reads as part of the clock
            {
                SevenSegment.Digit(m, SevenSegment.MaskOf(ones), sub, Off, o + right * (cx - dw / 2) + up * cy, right, up, nrm, dw, dh, th);
                return;
            }
            SevenSegment.Digit(m, value >= 10 ? SevenSegment.MaskOf(tens) : 0, sub, Off, o + right * x0 + up * cy, right, up, nrm, dw, dh, th);
            SevenSegment.Digit(m, SevenSegment.MaskOf(ones), sub, Off, o + right * (x0 + dw + sp) + up * cy, right, up, nrm, dw, dh, th);
        }
        float scoreY = -H * 0.28f;
        Number(blue, -W * 0.32f, scoreY, Blue);
        Number(red, W * 0.32f, scoreY, Red);

        // Clock M:SS in the middle, smaller.
        float ch = H * 0.20f, cw = ch * 0.52f, ct = ch * 0.15f, cs = cw * 0.35f;
        int mins = Mathf.Min(9, secs / 60), sec = secs % 60;
        float clockW = cw * 3 + cs * 2 + ct * 1.5f;
        float cx = -clockW / 2, cy = -H * 0.18f;
        int clockSub = blinkOff ? Off : White;
        SevenSegment.Digit(m, SevenSegment.MaskOf(mins), clockSub, Off, o + right * cx + up * cy, right, up, nrm, cw, ch, ct);
        float colonX = cx + cw + cs * 0.9f + ct * 0.4f;
        SevenSegment.Dot(m, clockSub, o, right, up, nrm, colonX, cy + ch * 0.28f, ct);
        SevenSegment.Dot(m, clockSub, o, right, up, nrm, colonX, cy + ch * 0.72f, ct);
        float x2 = colonX + ct * 0.6f + cs * 0.9f;
        SevenSegment.Digit(m, SevenSegment.MaskOf(sec / 10), clockSub, Off, o + right * x2 + up * cy, right, up, nrm, cw, ch, ct);
        SevenSegment.Digit(m, SevenSegment.MaskOf(sec % 10), clockSub, Off, o + right * (x2 + cw + cs) + up * cy, right, up, nrm, cw, ch, ct);

        // Period: three pips under the clock (current one lit; sudden death lights all three).
        float pip = H * 0.05f;
        for (int i = 0; i < 3; i++)
            SevenSegment.Dot(m, sudden || i + 1 == period ? White : Off, o, right, up, nrm, (i - 1) * pip * 2f, -H * 0.36f, pip);

        // Team chips above each score + periods won (two pips each) under the chip.
        for (int side = 0; side < 2; side++)
        {
            float x = (side == 0 ? -1 : 1) * W * 0.32f;
            int sub = side == 0 ? Blue : Red, won = side == 0 ? pb : pr;
            m.Sub(sub);
            float cw2 = W * 0.22f, chh = H * 0.045f, y = H * 0.36f;
            m.Flat(nrm, o + right * (x - cw2 / 2) + up * (y - chh / 2), o + right * (x + cw2 / 2) + up * (y - chh / 2), o + right * (x + cw2 / 2) + up * (y + chh / 2), o + right * (x - cw2 / 2) + up * (y + chh / 2));
            for (int i = 0; i < 2; i++) SevenSegment.Dot(m, i < won ? sub : Off, o, right, up, nrm, x + (i - 0.5f) * pip * 2.2f, H * 0.25f, pip);
        }
    }

    static Material Mat(string name, Color c, float metal, float smooth, Color emission = default)
    {
        var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(sh) { name = name };
        m.SetColor("_BaseColor", c); m.SetColor("_Color", c);
        m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", smooth);
        if (emission.maxColorComponent > 0)
        {
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        return m;
    }
}
