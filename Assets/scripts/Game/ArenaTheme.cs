// Arena themes applied to the single rink at match start (filled in by the arena milestone).
public static partial class ArenaTheme
{
    public static readonly string[] Names = { "Clássica", "Gelo", "Fábrica" };
    public static void Apply(int index) { ApplyImpl(index); }
    static partial void ApplyImpl(int index);
}
