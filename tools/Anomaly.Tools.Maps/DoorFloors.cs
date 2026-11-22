using System;
using System.Collections.Generic;
using System.Linq;

namespace Anomaly.Tools.Maps;

/// <summary>
/// D147: the floor under a door. tg maps most doors into maintenance on bare plating, where the pipes and the cables show; the
/// top-down door sprite hides the tile, but in first person the player looks past the door frame at them. The converter gives
/// such a tile the floor of its neighbour instead: a tile with an airlock, a firelock or a pod door (and no window or grille,
/// whose tiles get the client's window base) whose turf is exactly <c>/turf/open/floor/plating</c> takes the turf of the
/// cardinal neighbour with a covered floor (not plating, not airless, no gas or temperature override). When neighbours differ,
/// the most frequent turf wins, ties in the order north, south, east, west. A door with no such neighbour keeps its plating.
/// One pass over the original map: a changed tile is never a neighbour of another, so the result does not depend on order.
/// </summary>
public static class DoorFloors
{
    private const string Plating = "/turf/open/floor/plating";

    private static readonly string[] DoorRoots = { "/obj/machinery/door/airlock", "/obj/machinery/door/firedoor", "/obj/machinery/door/poddoor" };
    private static readonly string[] WindowRoots = { "/obj/structure/window", "/obj/structure/grille" };
    private static readonly (int X, int Y)[] Neighbours = { (0, 1), (0, -1), (1, 0), (-1, 0) };

    /// <summary>
    /// The tiles whose first turf changes, with the new turf. <paramref name="membersAt"/> gives a tile's members (movables,
    /// turfs, area), or null outside the map.
    /// </summary>
    public static Dictionary<(int X, int Y, int Z), DmmMember> Plan(int width, int height, int levels, Func<(int X, int Y, int Z), IReadOnlyList<DmmMember>> membersAt, TypeIndex types)
    {
        var plan = new Dictionary<(int, int, int), DmmMember>();
        for (int z = 1; z <= levels; z++)
        {
            for (int y = 1; y <= height; y++)
            {
                for (int x = 1; x <= width; x++)
                {
                    IReadOnlyList<DmmMember> members = membersAt((x, y, z));
                    if (members == null || !IsDoorOnPlating(members, types)) continue;
                    var counts = new List<(DmmMember Turf, string Text, int Count)>();
                    foreach ((int dx, int dy) in Neighbours)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 1 || ny < 1 || nx > width || ny > height) continue;
                        DmmMember turf = FirstTurf(membersAt((nx, ny, z)), types);
                        if (turf == null || !IsCoveredFloor(turf, types)) continue;
                        turf = Plain(turf, types);
                        string text = Text(turf);
                        int i = counts.FindIndex(c => c.Text == text);
                        if (i < 0) counts.Add((turf, text, 1));
                        else counts[i] = (counts[i].Turf, text, counts[i].Count + 1);
                    }
                    if (counts.Count == 0) continue;
                    int best = 0;
                    for (int i = 1; i < counts.Count; i++)
                        if (counts[i].Count > counts[best].Count) best = i;
                    plan[(x, y, z)] = counts[best].Turf;
                }
            }
        }
        return plan;
    }

    /// <summary>The members with the first turf replaced by <paramref name="turf"/>.</summary>
    public static List<DmmMember> Apply(IReadOnlyList<DmmMember> members, DmmMember turf, TypeIndex types)
    {
        var result = members.ToList();
        int i = result.FindIndex(m => types.IsA(m.Path, "/turf"));
        result[i] = turf;
        return result;
    }

    private static bool IsDoorOnPlating(IReadOnlyList<DmmMember> members, TypeIndex types)
    {
        bool door = false;
        foreach (DmmMember m in members)
        {
            if (types.IsA(m.Path, "/turf")) return door && m.Path == Plating;
            if (WindowRoots.Any(r => types.IsA(m.Path, r))) return false;
            if (DoorRoots.Any(r => types.IsA(m.Path, r))) door = true;
        }
        return false;
    }

    private static DmmMember FirstTurf(IReadOnlyList<DmmMember> members, TypeIndex types)
        => members?.FirstOrDefault(m => types.IsA(m.Path, "/turf"));

    // A floor that hides what is under it and breathes like plating.
    private static bool IsCoveredFloor(DmmMember turf, TypeIndex types)
    {
        if (!types.IsA(turf.Path, "/turf/open/floor") || types.IsA(turf.Path, Plating)) return false;
        if (turf.Path.Split('/').Any(s => s.Contains("airless", StringComparison.Ordinal) || s.Contains("coldroom", StringComparison.Ordinal))) return false;
        return !turf.Overrides.Any(o => o.Var is "initial_gas_mix" or "temperature");
    }

    // A pattern piece of a floor (a corner or a side of a coloured band) under a door becomes the plain floor it is cut from.
    private static DmmMember Plain(DmmMember turf, TypeIndex types)
    {
        int slash = turf.Path.LastIndexOf('/');
        string last = turf.Path[(slash + 1)..];
        if (last is not ("corner" or "side" or "half" or "edge")) return turf;
        string parent = turf.Path[..slash];
        if (!types.HasType(parent)) return turf;
        return new DmmMember(parent, turf.Overrides.Where(o => o.Var != "dir").ToList());
    }

    private static string Text(DmmMember m)
        => m.Path + "{" + string.Join("; ", m.Overrides.Select(o => o.Var + " = " + o.Literal)) + "}";
}
