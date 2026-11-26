using UnityEngine;

// Match settings chosen in the menu, read by MatchManager, the robots and the AI.
public static class GameConfig
{
    public enum Players { One, Two }
    public enum Kind { QuickMatch, Tournament }
    public enum Difficulty { Easy, Medium, Hard }
    public enum Format { OneVsOne, ThreeVsThree }

    public static Players players = Players.One;
    public static Kind kind = Kind.QuickMatch;
    public static Format format = Format.OneVsOne;   // 3x3 = three robots a side on a bigger rink
    public static Difficulty difficulty = Difficulty.Medium;
    public static int arena;                 // index into ArenaTheme.All
    public static int minutesPerPeriod = 2;
    public static int goalsPerPeriod = 3;    // a period also ends when a team reaches this
    public static float volume = 0.8f;

    // Robot picks (RobotSpec ids). 1x1 uses slot 0; 3x3 uses all three.
    public static string[] blueRobots = { "omega", "faisca", "trator" };
    public static string[] redRobots = { "omega", "canhao", "ima" };
    public static string[] Robots(bool blue) => blue ? blueRobots : redRobots;
    public static int RobotsPerTeam => format == Format.ThreeVsThree ? 3 : 1;
    public static bool WithinBudget(bool blue) => RobotsPerTeam < 3 || RobotCatalog.TeamWeight(Robots(blue), 3) <= RobotCatalog.TeamWeightBudget;

    // Robot handling (old menu: mostradado.speed / rotaao).
    public static float speed { get => mostradado.speed; set => mostradado.speed = value; }
    public static int rotation { get => mostradado.rotaao; set => mostradado.rotaao = value; }

    // Tournament ladder: arena + difficulty per stage.
    public static readonly (int arena, Difficulty diff)[] Ladder =
        { (0, Difficulty.Easy), (1, Difficulty.Medium), (2, Difficulty.Hard) };
    public static int stage;

    public static string Name(Difficulty d) => d == Difficulty.Easy ? "Fácil" : d == Difficulty.Medium ? "Médio" : "Difícil";
}
