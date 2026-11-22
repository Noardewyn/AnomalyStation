using Anomaly.Core.Motion;
using Anomaly.Core.Tg;

namespace Anomaly.Server;

public sealed partial class ServerHost
{
    private readonly Dictionary<(int X, int Y, int Z), CeilingState> _ceilingEdits = new();

    private string EditCeilings(DevCommand command)
    {
        int x = command.X.Value, y = command.Y.Value, z = command.Z.Value;
        if (x + command.Count - 1 > MapView.Width(World) || y + command.Radius - 1 > MapView.Height(World) || z > MapView.Levels(World))
            return "ceiling: rectangle is outside the map";
        var ceilings = CeilingSystem.Of(World);
        ceilings.Initialize();
        int changed = 0;
        for (int cy = y; cy < y + command.Radius; cy++)
        for (int cx = x; cx < x + command.Count; cx++)
        {
            var before = ceilings.At(cx, cy, z);
            // Debug edits skip the recipes and the support rule (authoring); scrape and break are tg's ScrapeAway and break_tile
            switch (command.Mode)
            {
                case "none": ceilings.Set(cx, cy, z, default); break;
                case "lattice": ceilings.Set(cx, cy, z, CeilingState.Of(CeilingLattice.Lattice)); break;
                case "catwalk": ceilings.Set(cx, cy, z, CeilingState.Of(CeilingLattice.Catwalk)); break;
                case "turf": if (!ceilings.Set(cx, cy, z, CeilingState.Of(command.TypePath))) return $"ceiling: {command.TypePath} is not a floor type"; break;
                case "scrape": ceilings.ScrapeAway(cx, cy, z); break;
                case "break": ceilings.BreakTile(cx, cy, z); break;
                case "fix": if (before.Broken != 0) ceilings.Set(cx, cy, z, before with { Broken = 0 }); break;
            }
            var state = ceilings.At(cx, cy, z);
            if (state == before) continue;
            _ceilingEdits[(cx, cy, z)] = state;
            changed++;
        }
        ceilings.RefreshSupport();
        return $"ceiling: {changed} cells updated (for the round; Anomaly Editor's Tile panel sets a map's ceilings)";
    }
}
