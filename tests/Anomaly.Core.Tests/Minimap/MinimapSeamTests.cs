using Anomaly.Core.DmShim;
using Anomaly.Core.Tests.Tg;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Minimap;

/// <summary>D315: tg's load_z (minimap.dm) through <see cref="MinimapSeam"/>: the colours of a wall, a floor, a door, a window and a department's floor, the areas, and a world left as it was.</summary>
public sealed class MinimapSeamTests
{
    [Fact]
    public void LoadZDrawsTgsColoursPerCellWithTheAreasAndLeavesTheWorldAsItWas()
    {
        TgToyRoom r = Room();
        r.In(() => Dm.@new<obj_machinery_door_airlock>(locate(5, 4, 1)));
        r.In(() => Dm.@new<obj_structure_window_fulltile>(locate(4, 5, 1)));
        ulong before = r.World.ComputeStateHash();

        MinimapPicture map = MinimapSeam.Build(r.World, 1);

        Assert.Equal(before, r.World.ComputeStateHash());
        Assert.Empty(r.RuntimeText);
        Assert.NotNull(map);
        Assert.Equal((1, 1, TgToyRoom.Size, TgToyRoom.Size), (map.MinX, map.MinY, map.Width, map.Height));
        uint At(int x, int y) => map.Pixels[(y - map.MinY) * map.Width + (x - map.MinX)];
        Assert.Equal(0x111111D0u, At(1, 1)); // TACMAP_BLACK, the closed turf
        Assert.Equal(0xEBE5E5EEu, At(3, 3)); // TACMAP_SOLID, the open turf of an area with no colour
        Assert.Equal(0x451E5EEEu, At(5, 4)); // TACMAP_DOOR
        Assert.Equal(0x525252D0u, At(4, 5)); // TACMAP_WINDOW
        string name = Assert.Single(map.AreaNames);
        Assert.Equal(r.In(() => r.Room.name), name);
        Assert.Equal("", Assert.Single(map.AreaCodes));
        Assert.All(map.Areas, a => Assert.Equal(1, a));
    }

    [Fact]
    public void AnAreaWithADepartmentColourBlendsItsFloorsAndNamesItsDepartment()
    {
        TgToyRoom r = Room();
        r.In(() => r.Room.tacmap_color = TACMAP_AREA_MEDICAL);

        MinimapPicture map = MinimapSeam.Build(r.World, 1);

        uint expected = r.In(() =>
        {
            DmList c = Dm.rgb2num((string)BlendRGB(TACMAP_SOLID, TACMAP_AREA_MEDICAL, 0.5f));
            return (uint)Dm.num(c[1]) << 24 | (uint)Dm.num(c[2]) << 16 | (uint)Dm.num(c[3]) << 8 | (uint)(c.Count > 3 ? Dm.num(c[4]) : 255);
        });
        Assert.Equal(expected, map.Pixels[2 * map.Width + 2]);
        Assert.Equal(0x111111D0u, map.Pixels[0]); // walls stay black
        Assert.Equal("MED", Assert.Single(map.AreaCodes));
    }

    // The toy room registers no space level; load_z refuses a z beyond SSmapping.z_list, so the room's level gets one, with no
    // trait (silicons SI2: an airlock's Initialize joins the diagnostic HUD, which reads its level's traits, as tg's always can)
    private static TgToyRoom Room()
    {
        TgToyRoom r = TgToyRoom.Create();
        r.In(() => SSmapping.z_list = Dm.list(Dm.@new<datum_space_level>(1, "Toy room", Dm.list())));
        return r;
    }
}
