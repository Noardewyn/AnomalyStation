namespace Anomaly.Tools.Maps.Tests;

/// <summary>D147: a door on plating takes its neighbour's floor.</summary>
public class DoorFloorsTests
{
    private static readonly DmmMember Area = new("/area/station", new List<(string, string)>());
    private static DmmMember M(string path, params (string, string)[] overrides) => new(path, overrides.ToList());

    // A 3 x 3 grid; every tile not given is plating with nothing on it.
    private static Dictionary<(int X, int Y, int Z), DmmMember> Plan(Dictionary<(int, int), List<DmmMember>> tiles)
        => DoorFloors.Plan(3, 3, 1, t => tiles.TryGetValue((t.X, t.Y), out List<DmmMember> m) ? m : new List<DmmMember> { M("/turf/open/floor/plating"), Area }, TypeIndex.Permissive());

    private static List<DmmMember> Door(string turf = "/turf/open/floor/plating", params string[] more)
        => more.Select(p => M(p)).Prepend(M("/obj/machinery/door/airlock/maintenance")).Append(M(turf)).Append(Area).ToList();

    [Fact]
    public void ADoorOnPlatingTakesTheCoveredNeighbour()
    {
        var plan = Plan(new() { [(2, 2)] = Door(), [(2, 3)] = new() { M("/turf/open/floor/iron/white"), Area } });
        Assert.Equal("/turf/open/floor/iron/white", Assert.Single(plan).Value.Path);
    }

    [Fact]
    public void ACornerPieceBecomesItsPlainFloor()
    {
        var plan = Plan(new() { [(2, 2)] = Door(), [(1, 2)] = new() { M("/turf/open/floor/iron/dark/corner", ("dir", "1")), Area } });
        DmmMember turf = Assert.Single(plan).Value;
        Assert.Equal("/turf/open/floor/iron/dark", turf.Path);
        Assert.Empty(turf.Overrides);
    }

    [Fact]
    public void AWindowTileAnAirlessNeighbourAndAnotherPlatingAreLeftAlone()
    {
        Assert.Empty(Plan(new() { [(2, 2)] = Door("/turf/open/floor/plating", "/obj/structure/grille"), [(2, 3)] = new() { M("/turf/open/floor/iron"), Area } }));
        Assert.Empty(Plan(new() { [(2, 2)] = Door(), [(2, 3)] = new() { M("/turf/open/floor/iron/airless"), Area } }));
        Assert.Empty(Plan(new() { [(2, 2)] = Door("/turf/open/floor/plating/airless"), [(2, 3)] = new() { M("/turf/open/floor/iron"), Area } }));
        Assert.Empty(Plan(new() { [(2, 2)] = Door() }));
    }

    [Fact]
    public void TheMostFrequentNeighbourWinsThenNorthFirst()
    {
        var floors = new Dictionary<(int, int), List<DmmMember>>
        {
            [(2, 2)] = Door(),
            [(2, 3)] = new() { M("/turf/open/floor/wood"), Area },
            [(1, 2)] = new() { M("/turf/open/floor/iron"), Area },
            [(3, 2)] = new() { M("/turf/open/floor/iron"), Area },
        };
        Assert.Equal("/turf/open/floor/iron", Assert.Single(Plan(floors)).Value.Path);
        floors.Remove((3, 2));
        Assert.Equal("/turf/open/floor/wood", Assert.Single(Plan(floors)).Value.Path);
    }
}
