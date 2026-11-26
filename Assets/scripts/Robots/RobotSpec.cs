using UnityEngine;

public enum RobotTool { None, Claw, Booster, Plow, Kicker, Magnets }
public enum RobotBodyKit { None, Claw, Racer, Armor, Cannon, Coils }   // extra bodywork on the standard chassis

// One playable robot. Add a robot = add one of these assets to Assets/Resources/Robots
// (Create > Hockey > Robot). Stats are multipliers on the standard chassis.
[CreateAssetMenu(menuName = "Hockey/Robot", fileName = "Robot")]
public class RobotSpec : ScriptableObject
{
    public string id = "robot";              // stable key (menus, saves, network)
    public string displayName = "Robô";
    [TextArea] public string description;
    public int order;                        // position in the picker
    public Color accent = Color.white;

    [Header("Stats")]
    [Range(0.6f, 1.5f)] public float speed = 1f;     // top speed x the match speed setting
    [Range(4f, 20f)] public float weight = 8f;       // Rigidbody mass: shoves and resists shoves
    [Range(0.7f, 1.4f)] public float size = 1f;      // body scale; bigger reaches further but turns slower
    [Range(0.6f, 1.5f)] public float strength = 1f;  // acceleration and how hard it hits the puck
    public RobotTool tool;

    [Header("Look")]
    public RobotBodyKit body;
    public GameObject model;                 // optional: replaces the standard chassis visuals                 // replaces the standard chassis visuals when set

    public string ToolName => Tools.Name(tool);
}

public static class Tools
{
    public static string Name(RobotTool t) => t switch
    {
        RobotTool.Claw => "Garra",
        RobotTool.Booster => "Turbo",
        RobotTool.Plow => "Pá",
        RobotTool.Kicker => "Chutador",
        RobotTool.Magnets => "Ímãs",
        _ => "Nenhuma",
    };
}

// All robots in Resources/Robots, in picker order.
public static class RobotCatalog
{
    public const float TeamWeightBudget = 30f;   // 3x3: total weight of a team's three robots

    static RobotSpec[] all;
    public static RobotSpec[] All
    {
        get
        {
            if (all == null || all.Length == 0)
            {
                all = Resources.LoadAll<RobotSpec>("Robots");
                System.Array.Sort(all, (a, b) => a.order != b.order ? a.order.CompareTo(b.order) : string.CompareOrdinal(a.id, b.id));
            }
            return all;
        }
    }

    public static RobotSpec Get(string id)
    {
        foreach (var r in All) if (r.id == id) return r;
        return All.Length > 0 ? All[0] : null;
    }

    public static int IndexOf(string id) { for (int i = 0; i < All.Length; i++) if (All[i].id == id) return i; return 0; }

    public static string Next(string id, int d) { int n = All.Length; return n == 0 ? id : All[((IndexOf(id) + d) % n + n) % n].id; }

    public static float TeamWeight(string[] ids, int count)
    {
        float w = 0; for (int i = 0; i < count; i++) { var r = Get(ids[i]); if (r) w += r.weight; }
        return w;
    }

    public static string Stats(RobotSpec r) => r == null ? "" :
        $"Vel {Mathf.RoundToInt(r.speed * 100)}%  ·  Peso {r.weight:0} kg  ·  Tam {Mathf.RoundToInt(r.size * 100)}%  ·  Força {Mathf.RoundToInt(r.strength * 100)}%  ·  {r.ToolName}";
}
