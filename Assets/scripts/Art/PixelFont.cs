using System.Collections.Generic;
using UnityEngine;

// 5x7 LED dot-matrix lettering built into a FacetMesh: each lit pixel is a small square facing
// `normal` (visible from the front only, so it never shows mirrored or through other geometry).
// Use an emissive material for the "LED board" look. Unknown characters render as blanks.
public static class PixelFont
{
    static readonly Dictionary<char, string[]> G = new Dictionary<char, string[]>
    {
        ['A'] = new[] { " ### ", "#   #", "#   #", "#####", "#   #", "#   #", "#   #" },
        ['B'] = new[] { "#### ", "#   #", "#   #", "#### ", "#   #", "#   #", "#### " },
        ['C'] = new[] { " ### ", "#   #", "#    ", "#    ", "#    ", "#   #", " ### " },
        ['D'] = new[] { "#### ", "#   #", "#   #", "#   #", "#   #", "#   #", "#### " },
        ['E'] = new[] { "#####", "#    ", "#    ", "#### ", "#    ", "#    ", "#####" },
        ['F'] = new[] { "#####", "#    ", "#    ", "#### ", "#    ", "#    ", "#    " },
        ['G'] = new[] { " ### ", "#   #", "#    ", "# ###", "#   #", "#   #", " ####" },
        ['H'] = new[] { "#   #", "#   #", "#   #", "#####", "#   #", "#   #", "#   #" },
        ['I'] = new[] { "#####", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", "#####" },
        ['K'] = new[] { "#   #", "#  # ", "# #  ", "##   ", "# #  ", "#  # ", "#   #" },
        ['L'] = new[] { "#    ", "#    ", "#    ", "#    ", "#    ", "#    ", "#####" },
        ['M'] = new[] { "#   #", "## ##", "# # #", "# # #", "#   #", "#   #", "#   #" },
        ['N'] = new[] { "#   #", "##  #", "# # #", "#  ##", "#   #", "#   #", "#   #" },
        ['O'] = new[] { " ### ", "#   #", "#   #", "#   #", "#   #", "#   #", " ### " },
        ['P'] = new[] { "#### ", "#   #", "#   #", "#### ", "#    ", "#    ", "#    " },
        ['R'] = new[] { "#### ", "#   #", "#   #", "#### ", "# #  ", "#  # ", "#   #" },
        ['S'] = new[] { " ####", "#    ", "#    ", " ### ", "    #", "    #", "#### " },
        ['T'] = new[] { "#####", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", "  #  " },
        ['U'] = new[] { "#   #", "#   #", "#   #", "#   #", "#   #", "#   #", " ### " },
        ['V'] = new[] { "#   #", "#   #", "#   #", "#   #", "#   #", " # # ", "  #  " },
        ['W'] = new[] { "#   #", "#   #", "#   #", "# # #", "# # #", "## ##", "#   #" },
        ['Y'] = new[] { "#   #", "#   #", " # # ", "  #  ", "  #  ", "  #  ", "  #  " },
        ['Z'] = new[] { "#####", "    #", "   # ", "  #  ", " #   ", "#    ", "#####" },
        ['0'] = new[] { " ### ", "#   #", "#  ##", "# # #", "##  #", "#   #", " ### " },
        ['1'] = new[] { "  #  ", " ##  ", "  #  ", "  #  ", "  #  ", "  #  ", " ### " },
        ['2'] = new[] { " ### ", "#   #", "    #", "   # ", "  #  ", " #   ", "#####" },
        ['6'] = new[] { "  ## ", " #   ", "#    ", "#### ", "#   #", "#   #", " ### " },
        ['Ω'] = new[] { " ### ", "#   #", "#   #", "#   #", " # # ", " # # ", "## ##" },
    };

    // Text centred on `centre`, in the plane spanned by right/up, facing `normal`. px = pixel pitch.
    public static void Text(FacetMesh m, string s, Vector3 centre, Vector3 right, Vector3 up, Vector3 normal, float px, float fill = 0.78f)
    {
        s = s.ToUpperInvariant();
        float w = (s.Length * 6 - 1) * px, h = 7 * px;
        var origin = centre - right * (w / 2f) + up * (h / 2f);
        float d = px * fill / 2f;
        for (int i = 0; i < s.Length; i++)
        {
            if (!G.TryGetValue(s[i], out var rows)) continue;
            for (int y = 0; y < 7; y++)
                for (int x = 0; x < 5; x++)
                {
                    if (rows[y][x] != '#') continue;
                    var c = origin + right * ((i * 6 + x + 0.5f) * px) - up * ((y + 0.5f) * px);
                    m.Flat(normal, c - right * d - up * d, c + right * d - up * d, c + right * d + up * d, c - right * d + up * d);
                }
        }
    }
}
