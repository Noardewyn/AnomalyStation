using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tests.Tg.M3Maps.T2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Structures.T2;

/// <summary>A turf decal whose icon is a file other than the decals' own (the key of the decal element names the icon by REF()).</summary>
[DmPath("/obj/effect/turf_decal/t2_other_icon")]
public class obj_effect_turf_decal_t2_other_icon : obj_effect_turf_decal
{
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        icon = Dm.resource("icons/turf/floors.dmi");
        icon_state = "warningline";
    }
}

/// <summary>
/// M3 T2 phase B: the turf decal and its element (effects/decals/decal.dm:60-83, datums/elements/decals/_decal.dm). The element is
/// bespoke (ELEMENT_BESPOKE, argument_hash_start_idx 2): SSdcs.GetElement keeps one instance per id, built from the arguments after
/// the element type, each non-text, non-number one by REF() (dcs.dm:56-104; main d286630 gives an icon its REF()); the decal gives
/// its turf the element and deletes itself, and the element puts its picture into the turf's overlays.
/// </summary>
public class DecalTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public DecalTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static int DecalElements()
    {
        int n = 0;
        foreach (object key in SSdcs.elements_by_type.Items())
        {
            if (SSdcs.elements_by_type[key] is datum_element_decal) n++;
        }
        return n;
    }

    private static List<IDmAppearance> Pictures(turf t) => t.overlays.Items().OfType<IDmAppearance>().ToList();

    [Fact]
    public void The_element_exists_once_per_bespoke_key()
    {
        var room = TgToyRoom.Create(9, 9);
        using (room.World.Enter())
        {
            int before = DecalElements();
            Dm.@new<obj_effect_turf_decal_tile_blue>(locate(2, 2, 1));
            Assert.Equal(before + 1, DecalElements());
            // the same icon, state, dir, layer, alpha and colour on another turf: the same element instance
            Dm.@new<obj_effect_turf_decal_tile_blue>(locate(3, 3, 1));
            Assert.Equal(before + 1, DecalElements());
            // another dir: another key (dir is one of the arguments)
            var turned = Dm.@new<obj_effect_turf_decal_tile_blue>(locate(4, 4, 1));
            // a decal made with a dir of its own: the map's `dir` var, here set before the element is made by a second decal type
            Dm.@new<obj_effect_turf_decal_tile_red>(locate(5, 5, 1));
            Assert.Equal(before + 2, DecalElements());
            // another icon file with the same state name: REF() of the icon differs (d286630), so the key differs
            Dm.@new<obj_effect_turf_decal_t2_other_icon>(locate(6, 6, 1));
            Assert.Equal(before + 3, DecalElements());
            Dm.@new<obj_effect_turf_decal_t2_other_icon>(locate(7, 7, 1));
            Assert.Equal(before + 3, DecalElements());
            Assert.Empty(room.RuntimeText);
        }
    }

    [Fact]
    public void The_overlay_is_on_the_turfs_appearance_once_per_key_and_goes_when_the_element_detaches()
    {
        var room = TgToyRoom.Create(9, 9);
        using (room.World.Enter())
        {
            turf at = locate(4, 4, 1);
            int baseline = Pictures(at).Count;
            var decal = Dm.@new<obj_effect_turf_decal_tile_blue>(at);
            Assert.True(QDELETED(decal)); // INITIALIZE_HINT_QDEL (decal.dm:83)
            Assert.DoesNotContain(decal, at.contents.Items().Cast<object>()); // loc = null
            var icon = decal.icon;
            string state = decal.icon_state;
            var after = Pictures(at);
            Assert.Equal(baseline + 1, after.Count);
            IDmAppearance pic = after.Single(p => p.icon_state == state);
            Assert.Equal(icon, pic.icon);
            Assert.Equal(decal.dir, pic.dir);
            Assert.Equal(decal.layer, pic.layer);
            Assert.Equal(decal.alpha, pic.alpha);

            // the same decal again on the same turf: the element registers once (RegisterSignal override, _decal.dm:84), so there
            // is still one picture of it
            Dm.@new<obj_effect_turf_decal_tile_blue>(at);
            Assert.Equal(baseline + 1, Pictures(at).Count);
            // another key adds a picture
            Dm.@new<obj_effect_turf_decal_tile_red>(at);
            Assert.Equal(baseline + 2, Pictures(at).Count);

            // Detach (_decal.dm:128-134) with the same arguments removes the picture and updates the overlays
            at.RemoveElement(typeof(datum_element_decal), decal.icon, decal.icon_state, decal.dir, null, decal.layer, decal.alpha, decal.color, null, 0f, null);
            Assert.Equal(baseline + 1, Pictures(at).Count);
            Assert.DoesNotContain(Pictures(at), p => p.icon_state == state && Equals(p.color, decal.color)); // the red decal of the same state stays
            Assert.Empty(room.RuntimeText);
        }
    }

    [MetaFact]
    public void Meta_has_no_turf_decal_object_left_and_each_decal_of_tgs_map_is_on_its_turfs_overlays()
    {
        string clone = MapFixtures.TgClone();
        string dmmPath = clone == null ? null : System.IO.Path.Combine(clone, "_maps", "map_files", "MetaStation", "MetaStation.dmm");
        World world = _meta.World;
        int alive = 0, cellsWithDecals = 0, missing = 0;
        var wrong = new List<string>();
        using (world.Enter())
        {
            for (int y = 1; y <= Dm.world.maxy; y++)
                for (int x = 1; x <= Dm.world.maxx; x++)
                    alive += locate(x, y, 1).contents.Items().Count(m => m is obj_effect_turf_decal);
            Assert.Equal(0, alive);
            if (dmmPath == null || !System.IO.File.Exists(dmmPath)) return;

            TgDmm dmm = TgDmm.Load(dmmPath);
            for (int y = 1; y <= dmm.MaxY; y++)
            {
                for (int x = 1; x <= dmm.MaxX; x++)
                {
                    var decals = dmm.At(x, y, 1).Where(p => p.StartsWith("/obj/effect/turf_decal", StringComparison.Ordinal)).Distinct().ToList();
                    if (decals.Count == 0) continue;
                    cellsWithDecals++;
                    var pictures = Pictures(locate(x, y, 1));
                    foreach (string path in decals)
                    {
                        Type type = DmTypes.FromPath(path);
                        string state = Dm.initial(type, "icon_state") as string;
                        // TgDmm keeps paths and not var edits: Meta's 28 plaques set their icon_state in the map (MetaStation.dmm:2925
                        // `icon_state = "L2"` and so on), so the type's own state is not what their picture shows
                        if (path == "/obj/effect/turf_decal/plaque") continue;
                        if (!pictures.Any(p => p.icon_state == state))
                        {
                            missing++;
                            wrong.Add($"({x},{y}) {path}: no picture with icon_state \"{state}\" among {pictures.Count} overlays");
                        }
                    }
                }
            }
        }
        _output.WriteLine($"{cellsWithDecals} cells with turf decals in tg's map; {missing} decal types without their picture\n" + string.Join("\n", wrong.Take(12)));
        Assert.True(cellsWithDecals > 1000);
        Assert.True(missing == 0, $"{missing} of {cellsWithDecals}: {string.Join("\n", wrong.Take(8))}");
    }
}
