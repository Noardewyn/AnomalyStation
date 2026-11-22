// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/rcd/RDD.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// E13 (the RCD family): the rapid decoration device (and its loaded variant) with its designs: a click places the selected plastic
// decoration (game/objects/structures/decorations.cs) on the tile, at most three to a tile, a right click removes one, each after
// tg's half second. The window is RapidDecorationDevice (the client's godot/scripts/Shared/Tgui/Interfaces/RapidDecorationDevice.cs).
// The cyborg's RDD (borg) stays inert with the cyborgs; E15 translated the examine (the owner's rule of 2026-10-29); the tool
// log lines are deferred leaves.

//RAPID DECORATION DEVICE

public partial class datum_controller_global_vars
{
    /// All decoration designs available in the RDD
    // tg: code/game/objects/items/rcd/RDD.dm:7  GLOBAL_LIST_INIT(rdd_designs, list(...))
    public DmList rdd_designs;

    // tg: code/game/objects/items/rcd/RDD.dm:7  (GLOBAL_MANAGED(rdd_designs, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalrdd_designs()
    {
        rdd_designs = rdd_designs_list();
        Dm.append(ref gvars_datum_init_order, "rdd_designs");
    }
}

public static partial class GlobalProcs
{
    // The port's own: GLOBAL_LIST_INIT(rdd_designs)'s list literal (RDD.dm:7-165), a proc so that the client's sprite sheet can build
    // the same designs without a world. The categories and their designs in tg's order.
    public static DmList rdd_designs_list()
    {
        return Dm.list(
            Dm.kv("Grasses", Dm.list(
                Dm.list(Dm.kv("name", "Plastic Grass Patch"), Dm.kv("path", typeof(obj_structure_decoration_grass_first))),
                Dm.list(Dm.kv("name", "Plastic Grass Patch (Alt)"), Dm.kv("path", typeof(obj_structure_decoration_grass_second))),
                Dm.list(Dm.kv("name", "Plastic Grass Patch (Alt 2)"), Dm.kv("path", typeof(obj_structure_decoration_grass_third))),
                Dm.list(Dm.kv("name", "Plastic Grass Patch (Random)"), Dm.kv("path", typeof(obj_structure_decoration_grass_style_random))),
                Dm.list(Dm.kv("name", "Plastic Brown Grass"), Dm.kv("path", typeof(obj_structure_decoration_grass_brown_first))),
                Dm.list(Dm.kv("name", "Plastic Brown Grass (Alt)"), Dm.kv("path", typeof(obj_structure_decoration_grass_brown_second))),
                Dm.list(Dm.kv("name", "Plastic Brown Grass (Alt 2)"), Dm.kv("path", typeof(obj_structure_decoration_grass_brown_third))),
                Dm.list(Dm.kv("name", "Plastic Brown Grass (Random)"), Dm.kv("path", typeof(obj_structure_decoration_grass_brown_style_random))),
                Dm.list(Dm.kv("name", "Plastic Jungle Grass"), Dm.kv("path", typeof(obj_structure_decoration_jungle_grass_first))),
                Dm.list(Dm.kv("name", "Plastic Jungle Grass (Alt)"), Dm.kv("path", typeof(obj_structure_decoration_jungle_grass_second))),
                Dm.list(Dm.kv("name", "Plastic Jungle Grass (Alt 2)"), Dm.kv("path", typeof(obj_structure_decoration_jungle_grass_third))),
                Dm.list(Dm.kv("name", "Plastic Jungle Grass (Alt 3)"), Dm.kv("path", typeof(obj_structure_decoration_jungle_grass_fourth))),
                Dm.list(Dm.kv("name", "Plastic Jungle Grass (Alt 4)"), Dm.kv("path", typeof(obj_structure_decoration_jungle_grass_fifth))),
                Dm.list(Dm.kv("name", "Plastic Jungle Grass (Random)"), Dm.kv("path", typeof(obj_structure_decoration_jungle_grass_style_random))),
                Dm.list(Dm.kv("name", "Plastic Jungle Grass B"), Dm.kv("path", typeof(obj_structure_decoration_jungle_grass_b_first))),
                Dm.list(Dm.kv("name", "Plastic Jungle Grass B (Alt)"), Dm.kv("path", typeof(obj_structure_decoration_jungle_grass_b_second))),
                Dm.list(Dm.kv("name", "Plastic Jungle Grass B (Alt 2)"), Dm.kv("path", typeof(obj_structure_decoration_jungle_grass_b_third))),
                Dm.list(Dm.kv("name", "Plastic Jungle Grass B (Alt 3)"), Dm.kv("path", typeof(obj_structure_decoration_jungle_grass_b_fourth))),
                Dm.list(Dm.kv("name", "Plastic Jungle Grass B (Alt 4)"), Dm.kv("path", typeof(obj_structure_decoration_jungle_grass_b_fifth))),
                Dm.list(Dm.kv("name", "Plastic Jungle Grass B (Random)"), Dm.kv("path", typeof(obj_structure_decoration_jungle_grass_b_style_random))))),
            Dm.kv("Bushes", Dm.list(
                Dm.list(Dm.kv("name", "Plastic Bush"), Dm.kv("path", typeof(obj_structure_decoration_bush_first))),
                Dm.list(Dm.kv("name", "Plastic Bush (Alt)"), Dm.kv("path", typeof(obj_structure_decoration_bush_second))),
                Dm.list(Dm.kv("name", "Plastic Bush (Alt 2)"), Dm.kv("path", typeof(obj_structure_decoration_bush_third))),
                Dm.list(Dm.kv("name", "Plastic Bush (Alt 3)"), Dm.kv("path", typeof(obj_structure_decoration_bush_fourth))),
                Dm.list(Dm.kv("name", "Plastic Bush (Random)"), Dm.kv("path", typeof(obj_structure_decoration_bush_style_random))),
                Dm.list(Dm.kv("name", "Plastic Reeds"), Dm.kv("path", typeof(obj_structure_decoration_bush_reed_first))),
                Dm.list(Dm.kv("name", "Plastic Reeds (Alt)"), Dm.kv("path", typeof(obj_structure_decoration_bush_reed_second))),
                Dm.list(Dm.kv("name", "Plastic Reeds (Alt 2)"), Dm.kv("path", typeof(obj_structure_decoration_bush_reed_third))),
                Dm.list(Dm.kv("name", "Plastic Reeds (Alt 3)"), Dm.kv("path", typeof(obj_structure_decoration_bush_reed_fourth))),
                Dm.list(Dm.kv("name", "Plastic Reeds (Random)"), Dm.kv("path", typeof(obj_structure_decoration_bush_reed_style_random))),
                Dm.list(Dm.kv("name", "Plastic Leafy Bush"), Dm.kv("path", typeof(obj_structure_decoration_bush_leafy_first))),
                Dm.list(Dm.kv("name", "Plastic Leafy Bush (Alt)"), Dm.kv("path", typeof(obj_structure_decoration_bush_leafy_second))),
                Dm.list(Dm.kv("name", "Plastic Leafy Bush (Alt 2)"), Dm.kv("path", typeof(obj_structure_decoration_bush_leafy_third))),
                Dm.list(Dm.kv("name", "Plastic Leafy Bush (Random)"), Dm.kv("path", typeof(obj_structure_decoration_bush_leafy_style_random))),
                Dm.list(Dm.kv("name", "Plastic Pale Bush"), Dm.kv("path", typeof(obj_structure_decoration_bush_pale_first))),
                Dm.list(Dm.kv("name", "Plastic Pale Bush (Alt)"), Dm.kv("path", typeof(obj_structure_decoration_bush_pale_second))),
                Dm.list(Dm.kv("name", "Plastic Pale Bush (Alt 2)"), Dm.kv("path", typeof(obj_structure_decoration_bush_pale_third))),
                Dm.list(Dm.kv("name", "Plastic Pale Bush (Alt 3)"), Dm.kv("path", typeof(obj_structure_decoration_bush_pale_fourth))),
                Dm.list(Dm.kv("name", "Plastic Pale Bush (Random)"), Dm.kv("path", typeof(obj_structure_decoration_bush_pale_style_random))),
                Dm.list(Dm.kv("name", "Plastic Stalky Bush"), Dm.kv("path", typeof(obj_structure_decoration_bush_stalky_first))),
                Dm.list(Dm.kv("name", "Plastic Stalky Bush (Alt)"), Dm.kv("path", typeof(obj_structure_decoration_bush_stalky_second))),
                Dm.list(Dm.kv("name", "Plastic Stalky Bush (Alt 2)"), Dm.kv("path", typeof(obj_structure_decoration_bush_stalky_third))),
                Dm.list(Dm.kv("name", "Plastic Stalky Bush (Random)"), Dm.kv("path", typeof(obj_structure_decoration_bush_stalky_style_random))),
                Dm.list(Dm.kv("name", "Plastic Grassy Bush"), Dm.kv("path", typeof(obj_structure_decoration_bush_grassy_first))),
                Dm.list(Dm.kv("name", "Plastic Grassy Bush (Alt)"), Dm.kv("path", typeof(obj_structure_decoration_bush_grassy_second))),
                Dm.list(Dm.kv("name", "Plastic Grassy Bush (Alt 2)"), Dm.kv("path", typeof(obj_structure_decoration_bush_grassy_third))),
                Dm.list(Dm.kv("name", "Plastic Grassy Bush (Alt 3)"), Dm.kv("path", typeof(obj_structure_decoration_bush_grassy_fourth))),
                Dm.list(Dm.kv("name", "Plastic Grassy Bush (Random)"), Dm.kv("path", typeof(obj_structure_decoration_bush_grassy_style_random))),
                Dm.list(Dm.kv("name", "Plastic Sparse Grass"), Dm.kv("path", typeof(obj_structure_decoration_bush_sparsegrass_first))),
                Dm.list(Dm.kv("name", "Plastic Sparse Grass (Alt)"), Dm.kv("path", typeof(obj_structure_decoration_bush_sparsegrass_second))),
                Dm.list(Dm.kv("name", "Plastic Sparse Grass (Alt 2)"), Dm.kv("path", typeof(obj_structure_decoration_bush_sparsegrass_third))),
                Dm.list(Dm.kv("name", "Plastic Sparse Grass (Random)"), Dm.kv("path", typeof(obj_structure_decoration_bush_sparsegrass_style_random))),
                Dm.list(Dm.kv("name", "Plastic Full Grass"), Dm.kv("path", typeof(obj_structure_decoration_bush_fullgrass_first))),
                Dm.list(Dm.kv("name", "Plastic Full Grass (Alt)"), Dm.kv("path", typeof(obj_structure_decoration_bush_fullgrass_second))),
                Dm.list(Dm.kv("name", "Plastic Full Grass (Alt 2)"), Dm.kv("path", typeof(obj_structure_decoration_bush_fullgrass_third))),
                Dm.list(Dm.kv("name", "Plastic Full Grass (Random)"), Dm.kv("path", typeof(obj_structure_decoration_bush_fullgrass_style_random))),
                Dm.list(Dm.kv("name", "Plastic Ferny Bush"), Dm.kv("path", typeof(obj_structure_decoration_bush_ferny_first))),
                Dm.list(Dm.kv("name", "Plastic Ferny Bush (Alt)"), Dm.kv("path", typeof(obj_structure_decoration_bush_ferny_second))),
                Dm.list(Dm.kv("name", "Plastic Ferny Bush (Alt 2)"), Dm.kv("path", typeof(obj_structure_decoration_bush_ferny_third))),
                Dm.list(Dm.kv("name", "Plastic Ferny Bush (Random)"), Dm.kv("path", typeof(obj_structure_decoration_bush_ferny_style_random))),
                Dm.list(Dm.kv("name", "Plastic Sunny Bush"), Dm.kv("path", typeof(obj_structure_decoration_bush_sunny_first))),
                Dm.list(Dm.kv("name", "Plastic Sunny Bush (Alt)"), Dm.kv("path", typeof(obj_structure_decoration_bush_sunny_second))),
                Dm.list(Dm.kv("name", "Plastic Sunny Bush (Alt 2)"), Dm.kv("path", typeof(obj_structure_decoration_bush_sunny_third))),
                Dm.list(Dm.kv("name", "Plastic Sunny Bush (Random)"), Dm.kv("path", typeof(obj_structure_decoration_bush_sunny_style_random))),
                Dm.list(Dm.kv("name", "Plastic Generic Bush"), Dm.kv("path", typeof(obj_structure_decoration_bush_generic_first))),
                Dm.list(Dm.kv("name", "Plastic Generic Bush (Alt)"), Dm.kv("path", typeof(obj_structure_decoration_bush_generic_second))),
                Dm.list(Dm.kv("name", "Plastic Generic Bush (Alt 2)"), Dm.kv("path", typeof(obj_structure_decoration_bush_generic_third))),
                Dm.list(Dm.kv("name", "Plastic Generic Bush (Alt 3)"), Dm.kv("path", typeof(obj_structure_decoration_bush_generic_fourth))),
                Dm.list(Dm.kv("name", "Plastic Generic Bush (Random)"), Dm.kv("path", typeof(obj_structure_decoration_bush_generic_style_random))),
                Dm.list(Dm.kv("name", "Plastic Pointy Bush"), Dm.kv("path", typeof(obj_structure_decoration_bush_pointy_first))),
                Dm.list(Dm.kv("name", "Plastic Pointy Bush (Alt)"), Dm.kv("path", typeof(obj_structure_decoration_bush_pointy_second))),
                Dm.list(Dm.kv("name", "Plastic Pointy Bush (Alt 2)"), Dm.kv("path", typeof(obj_structure_decoration_bush_pointy_third))),
                Dm.list(Dm.kv("name", "Plastic Pointy Bush (Alt 3)"), Dm.kv("path", typeof(obj_structure_decoration_bush_pointy_fourth))),
                Dm.list(Dm.kv("name", "Plastic Pointy Bush (Random)"), Dm.kv("path", typeof(obj_structure_decoration_bush_pointy_style_random))),
                Dm.list(Dm.kv("name", "Plastic Lavender Grass"), Dm.kv("path", typeof(obj_structure_decoration_bush_lavendergrass_first))),
                Dm.list(Dm.kv("name", "Plastic Lavender Grass (Alt)"), Dm.kv("path", typeof(obj_structure_decoration_bush_lavendergrass_second))),
                Dm.list(Dm.kv("name", "Plastic Lavender Grass (Alt 2)"), Dm.kv("path", typeof(obj_structure_decoration_bush_lavendergrass_third))),
                Dm.list(Dm.kv("name", "Plastic Lavender Grass (Alt 3)"), Dm.kv("path", typeof(obj_structure_decoration_bush_lavendergrass_fourth))),
                Dm.list(Dm.kv("name", "Plastic Lavender Grass (Random)"), Dm.kv("path", typeof(obj_structure_decoration_bush_lavendergrass_style_random))))),
            Dm.kv("Flowers", Dm.list(
                Dm.list(Dm.kv("name", "Plastic Yellow-White Flowers"), Dm.kv("path", typeof(obj_structure_decoration_bush_flowers_yw_first))),
                Dm.list(Dm.kv("name", "Plastic Yellow-White Flowers (Alt)"), Dm.kv("path", typeof(obj_structure_decoration_bush_flowers_yw_second))),
                Dm.list(Dm.kv("name", "Plastic Yellow-White Flowers (Alt 2)"), Dm.kv("path", typeof(obj_structure_decoration_bush_flowers_yw_third))),
                Dm.list(Dm.kv("name", "Plastic Yellow-White Flowers (Random)"), Dm.kv("path", typeof(obj_structure_decoration_bush_flowers_yw_style_random))),
                Dm.list(Dm.kv("name", "Plastic Blue-Red Flowers"), Dm.kv("path", typeof(obj_structure_decoration_bush_flowers_br_first))),
                Dm.list(Dm.kv("name", "Plastic Blue-Red Flowers (Alt)"), Dm.kv("path", typeof(obj_structure_decoration_bush_flowers_br_second))),
                Dm.list(Dm.kv("name", "Plastic Blue-Red Flowers (Alt 2)"), Dm.kv("path", typeof(obj_structure_decoration_bush_flowers_br_third))),
                Dm.list(Dm.kv("name", "Plastic Blue-Red Flowers (Random)"), Dm.kv("path", typeof(obj_structure_decoration_bush_flowers_br_style_random))),
                Dm.list(Dm.kv("name", "Plastic Purple Flowers"), Dm.kv("path", typeof(obj_structure_decoration_bush_flowers_pp_first))),
                Dm.list(Dm.kv("name", "Plastic Purple Flowers (Alt)"), Dm.kv("path", typeof(obj_structure_decoration_bush_flowers_pp_second))),
                Dm.list(Dm.kv("name", "Plastic Purple Flowers (Alt 2)"), Dm.kv("path", typeof(obj_structure_decoration_bush_flowers_pp_third))),
                Dm.list(Dm.kv("name", "Plastic Purple Flowers (Random)"), Dm.kv("path", typeof(obj_structure_decoration_bush_flowers_pp_style_random))))),
            Dm.kv("Snow", Dm.list(
                Dm.list(Dm.kv("name", "Plastic Snowy Bush"), Dm.kv("path", typeof(obj_structure_decoration_bush_snow_first))),
                Dm.list(Dm.kv("name", "Plastic Snowy Bush (Alt)"), Dm.kv("path", typeof(obj_structure_decoration_bush_snow_second))),
                Dm.list(Dm.kv("name", "Plastic Snowy Bush (Alt 2)"), Dm.kv("path", typeof(obj_structure_decoration_bush_snow_third))),
                Dm.list(Dm.kv("name", "Plastic Snowy Bush (Alt 3)"), Dm.kv("path", typeof(obj_structure_decoration_bush_snow_fourth))),
                Dm.list(Dm.kv("name", "Plastic Snowy Bush (Alt 4)"), Dm.kv("path", typeof(obj_structure_decoration_bush_snow_fifth))),
                Dm.list(Dm.kv("name", "Plastic Snowy Bush (Alt 5)"), Dm.kv("path", typeof(obj_structure_decoration_bush_snow_sixth))),
                Dm.list(Dm.kv("name", "Plastic Snowy Bush (Random)"), Dm.kv("path", typeof(obj_structure_decoration_bush_snow_style_random))))),
            Dm.kv("Jungle", Dm.list(
                Dm.list(Dm.kv("name", "Plastic Jungle Bush"), Dm.kv("path", typeof(obj_structure_decoration_bush_jungle_first))),
                Dm.list(Dm.kv("name", "Plastic Jungle Bush (Alt)"), Dm.kv("path", typeof(obj_structure_decoration_bush_jungle_second))),
                Dm.list(Dm.kv("name", "Plastic Jungle Bush (Alt 2)"), Dm.kv("path", typeof(obj_structure_decoration_bush_jungle_third))),
                Dm.list(Dm.kv("name", "Plastic Jungle Bush (Random)"), Dm.kv("path", typeof(obj_structure_decoration_bush_jungle_style_random))),
                Dm.list(Dm.kv("name", "Plastic Jungle Bush B"), Dm.kv("path", typeof(obj_structure_decoration_bush_jungle_b_first))),
                Dm.list(Dm.kv("name", "Plastic Jungle Bush B (Alt)"), Dm.kv("path", typeof(obj_structure_decoration_bush_jungle_b_second))),
                Dm.list(Dm.kv("name", "Plastic Jungle Bush B (Alt 2)"), Dm.kv("path", typeof(obj_structure_decoration_bush_jungle_b_third))),
                Dm.list(Dm.kv("name", "Plastic Jungle Bush B (Random)"), Dm.kv("path", typeof(obj_structure_decoration_bush_jungle_b_style_random))),
                Dm.list(Dm.kv("name", "Plastic Jungle Bush C"), Dm.kv("path", typeof(obj_structure_decoration_bush_jungle_c_first))),
                Dm.list(Dm.kv("name", "Plastic Jungle Bush C (Alt)"), Dm.kv("path", typeof(obj_structure_decoration_bush_jungle_c_second))),
                Dm.list(Dm.kv("name", "Plastic Jungle Bush C (Alt 2)"), Dm.kv("path", typeof(obj_structure_decoration_bush_jungle_c_third))),
                Dm.list(Dm.kv("name", "Plastic Jungle Bush C (Random)"), Dm.kv("path", typeof(obj_structure_decoration_bush_jungle_c_style_random))),
                Dm.list(Dm.kv("name", "Large Plastic Bush"), Dm.kv("path", typeof(obj_structure_decoration_bush_large_first))),
                Dm.list(Dm.kv("name", "Large Plastic Bush (Alt)"), Dm.kv("path", typeof(obj_structure_decoration_bush_large_second))),
                Dm.list(Dm.kv("name", "Large Plastic Bush (Alt 2)"), Dm.kv("path", typeof(obj_structure_decoration_bush_large_third))),
                Dm.list(Dm.kv("name", "Large Plastic Bush (Random)"), Dm.kv("path", typeof(obj_structure_decoration_bush_large_style_random))))),
            Dm.kv("Rocks", Dm.list(
                Dm.list(Dm.kv("name", "Plastic Rock"), Dm.kv("path", typeof(obj_structure_decoration_rock_first))),
                Dm.list(Dm.kv("name", "Plastic Rock (Alt)"), Dm.kv("path", typeof(obj_structure_decoration_rock_second))),
                Dm.list(Dm.kv("name", "Plastic Rock (Alt 2)"), Dm.kv("path", typeof(obj_structure_decoration_rock_third))),
                Dm.list(Dm.kv("name", "Plastic Rock (Alt 3)"), Dm.kv("path", typeof(obj_structure_decoration_rock_fourth))),
                Dm.list(Dm.kv("name", "Plastic Rock (Random)"), Dm.kv("path", typeof(obj_structure_decoration_rock_style_random))),
                Dm.list(Dm.kv("name", "Plastic Rock Pile"), Dm.kv("path", typeof(obj_structure_decoration_rock_pile_first))),
                Dm.list(Dm.kv("name", "Plastic Rock Pile (Alt)"), Dm.kv("path", typeof(obj_structure_decoration_rock_pile_second))),
                Dm.list(Dm.kv("name", "Plastic Rock Pile (Alt 2)"), Dm.kv("path", typeof(obj_structure_decoration_rock_pile_third))),
                Dm.list(Dm.kv("name", "Plastic Rock Pile (Random)"), Dm.kv("path", typeof(obj_structure_decoration_rock_pile_style_random))),
                Dm.list(Dm.kv("name", "Plastic Jungle Rocks"), Dm.kv("path", typeof(obj_structure_decoration_rock_pile_jungle_first))),
                Dm.list(Dm.kv("name", "Plastic Jungle Rocks (Alt)"), Dm.kv("path", typeof(obj_structure_decoration_rock_pile_jungle_second))),
                Dm.list(Dm.kv("name", "Plastic Jungle Rocks (Alt 2)"), Dm.kv("path", typeof(obj_structure_decoration_rock_pile_jungle_third))),
                Dm.list(Dm.kv("name", "Plastic Jungle Rocks (Alt 3)"), Dm.kv("path", typeof(obj_structure_decoration_rock_pile_jungle_fourth))),
                Dm.list(Dm.kv("name", "Plastic Jungle Rocks (Alt 4)"), Dm.kv("path", typeof(obj_structure_decoration_rock_pile_jungle_fifth))),
                Dm.list(Dm.kv("name", "Plastic Jungle Rocks (Random)"), Dm.kv("path", typeof(obj_structure_decoration_rock_pile_jungle_style_random))),
                Dm.list(Dm.kv("name", "Large Plastic Rocks"), Dm.kv("path", typeof(obj_structure_decoration_rock_pile_jungle_large_first))),
                Dm.list(Dm.kv("name", "Large Plastic Rocks (Alt)"), Dm.kv("path", typeof(obj_structure_decoration_rock_pile_jungle_large_second))),
                Dm.list(Dm.kv("name", "Large Plastic Rocks (Alt 2)"), Dm.kv("path", typeof(obj_structure_decoration_rock_pile_jungle_large_third))),
                Dm.list(Dm.kv("name", "Large Plastic Rocks (Random)"), Dm.kv("path", typeof(obj_structure_decoration_rock_pile_jungle_large_style_random))),
                Dm.list(Dm.kv("name", "Plastic Volcano Pore"), Dm.kv("path", typeof(obj_structure_decoration_rock_volcano_first))),
                Dm.list(Dm.kv("name", "Plastic Volcano Pore (Alt)"), Dm.kv("path", typeof(obj_structure_decoration_rock_volcano_second))),
                Dm.list(Dm.kv("name", "Plastic Volcano Pore (Alt2)"), Dm.kv("path", typeof(obj_structure_decoration_rock_volcano_third))),
                Dm.list(Dm.kv("name", "Plastic Volcano Pore (Alt3)"), Dm.kv("path", typeof(obj_structure_decoration_rock_volcano_fourth))),
                Dm.list(Dm.kv("name", "Plastic Volcano Pore (Alt4)"), Dm.kv("path", typeof(obj_structure_decoration_rock_volcano_fifth))),
                Dm.list(Dm.kv("name", "Plastic Volcano Pore (Random)"), Dm.kv("path", typeof(obj_structure_decoration_rock_volcano_style_random))))),
            Dm.kv("Misc", Dm.list(
                Dm.list(Dm.kv("name", "Plastic Glowgrowth Colony"), Dm.kv("path", typeof(obj_structure_decoration_misc_glowgrowth_first))),
                Dm.list(Dm.kv("name", "Plastic Glowgrowth Colony (Alt)"), Dm.kv("path", typeof(obj_structure_decoration_misc_glowgrowth_second))),
                Dm.list(Dm.kv("name", "Plastic Glowgrowth Colony (Alt2)"), Dm.kv("path", typeof(obj_structure_decoration_misc_glowgrowth_third))),
                Dm.list(Dm.kv("name", "Plastic Glowgrowth Colony (Random)"), Dm.kv("path", typeof(obj_structure_decoration_misc_glowgrowth_style_random))))));
    }
}

// tg: code/game/objects/items/rcd/RDD.dm:167
public partial class obj_item_construction_rdd : obj_item_construction
{
    /// Multiplier applied to cost when using RDD — each decoration costs this many matter units
    // tg: code/game/objects/items/rcd/RDD.dm:4  #define RDD_COST_MULTIPLIER 1 (file-local, rules 11.4)
    private const float RDD_COST_MULTIPLIER = 1;

    /// Currently selected decoration path
    // tg: code/game/objects/items/rcd/RDD.dm:190
    // The generated field would be an obj_structure_decoration (tg declares var/obj/structure/decoration/selected_decoration for
    // initial()), which a typepath does not fit: the hand-written member holds the path, and the constructor with tg's overrides
    // (RDD.dm:168-187) is here.
    public Type selected_decoration;

    // tg: code/game/objects/items/rcd/RDD.dm:167
    public obj_item_construction_rdd()
    {
        name = "rapid-decoration-device (RDD)";
        desc = "A device used to rapidly deploy plastic decorative flora. Internally synthesizes cheap plastic replicas of natural scenery.";
        icon = Dm.resource("icons/obj/tools.dmi");
        icon_state = "rdd";
        inhand_icon_state = "rdd";
        worn_icon_state = "RCD";
        lefthand_file = Dm.resource("icons/mob/inhands/equipment/tools_lefthand.dmi");
        righthand_file = Dm.resource("icons/mob/inhands/equipment/tools_righthand.dmi");
        custom_premium_price = PAYCHECK_COMMAND * 1;
        max_matter = 60;
        slot_flags = ITEM_SLOT_BELT;
        item_flags = Dm.bitor(NO_MAT_REDEMPTION, NOBLUDGEON);
        has_ammobar = TRUE;
        banned_upgrades = Dm.bitand(RCD_ALL_UPGRADES, Dm.bitnot(RCD_UPGRADE_SILO_LINK));
        charge_icon_state = "rtd";
        drop_sound = Dm.resource("sound/items/handling/tools/rcd_drop.ogg");
        pickup_sound = Dm.resource("sound/items/handling/tools/rcd_pickup.ogg");
        sound_vary = TRUE;
    }

    // tg: code/game/objects/items/rcd/RDD.dm:187  (a hand-written var block has its own dm_init_vars, types-converter.md 3.4)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        custom_materials = Dm.list(Dm.kv(typeof(datum_material_plastic), SHEET_MATERIAL_AMOUNT * 4), Dm.kv(typeof(datum_material_iron), SHEET_MATERIAL_AMOUNT * 3), Dm.kv(typeof(datum_material_glass), SHEET_MATERIAL_AMOUNT * 1.5f));
    }

    // tg: code/game/objects/items/rcd/RDD.dm:196
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        selected_category = GLOB.rdd_designs[1];
        DmList category_designs = GLOB.rdd_designs[selected_category] as DmList;
        if (Dm.truthy(Dm.length(category_designs)))
        {
            DmList first_design = category_designs[1] as DmList;
            selected_decoration = first_design["path"] as Type;
            selected_design_name = first_design["name"];
        }
        return dot;
    }

    // tg: code/game/objects/items/rcd/RDD.dm:205  (E15)
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_info(I18n.T("Currently set to produce: [].", span_bold(Dm.str(Dm.initial(selected_decoration, (atom a) => a.name))))));
        return dot;
    }

    // tg: code/game/objects/items/rcd/RDD.dm:209
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        await ui_interact(user);
        return dot;
    }

    // tg: code/game/objects/items/rcd/RDD.dm:213
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "RapidDecorationDevice", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/objects/items/rcd/RDD.dm:219
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // list(get_asset_datum(/datum/asset/spritesheet_batched/rdd)): the asset transport is the client's (DV44); the client draws
        // the "rdd" sheet's pictures from its own copy of tg's .dmi files (TguiSpritesheets).
        return Dm.list();
    }

    // tg: code/game/objects/items/rcd/RDD.dm:224
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = await base.ui_static_data(user) as DmList;

        data["categories"] = Dm.list();
        foreach (object category in Dm.each(GLOB.rdd_designs))
        {
            DmList cat_entry = Dm.list(Dm.kv("cat_name", category), Dm.kv("designs", Dm.list()));
            foreach (DmList design in Dm.each<DmList>(GLOB.rdd_designs[category]))
            {
                Dm.append_at(cat_entry, "designs", Dm.list(Dm.list(
                    Dm.kv("name", design["name"]),
                    Dm.kv("icon", sanitize_css_class_name(design["name"])))));
            }
            Dm.append_at(data, "categories", Dm.list(cat_entry));
        }

        return data;
    }

    // tg: code/game/objects/items/rcd/RDD.dm:239
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = await base.ui_data(user) as DmList;

        object total_matter = get_matter(user);
        data["matter"] = Dm.isnum(total_matter) ? total_matter : 0;
        data["max_matter"] = max_matter;
        data["selected_category"] = selected_category;
        data["selected_design"] = selected_design_name;

        return data;
    }

    // tg: code/game/objects/items/rcd/RDD.dm:250
    public override async DmTask<object> handle_ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.handle_ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action)
        {
            case "design":
            {
                object category = @params["category"];
                object design_name = @params["name"];
                foreach (DmList design in Dm.each_cast<DmList>(GLOB.rdd_designs[category]))
                {
                    if (Dm.eq(design["name"], design_name))
                    {
                        selected_decoration = design["path"] as Type;
                        selected_category = category;
                        selected_design_name = design_name;
                        playsound(this, SFX_TOOL_SWITCH, 20, TRUE);
                        return TRUE;
                    }
                }
                break;
            }
        }

        return TRUE;
    }

    // tg: code/game/objects/items/rcd/RDD.dm:269
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        object dot = await base.interact_with_atom(interacting_with, user, modifiers);
        if (Dm.truthy(Dm.bitand(Dm.num(dot), ITEM_INTERACT_ANY_BLOCKER)))
            return dot;

        turf target_turf = get_turf(interacting_with);
        if (!Dm.truthy(target_turf))
            return ITEM_INTERACT_BLOCKING;

        if (Dm.truthy(await target_turf.is_blocked_turf(exclude_mobs: TRUE)))
        {
            balloon_alert(user, I18n.T("tile is blocked!"));
            return ITEM_INTERACT_BLOCKING;
        }

        float decoration_count = 0;
        foreach (obj_structure_decoration existing in Dm.each<obj_structure_decoration>(target_turf.contents))
        {
            decoration_count++;
            if (decoration_count >= 3)
            {
                balloon_alert(user, I18n.T("too many decorations here!"));
                return ITEM_INTERACT_BLOCKING;
            }
        }

        float cost = RDD_COST_MULTIPLIER;
        if (!Dm.truthy(await useResource(cost, user, TRUE)))
            return ITEM_INTERACT_BLOCKING;

        playsound(loc as atom, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
        if (!Dm.truthy(await do_after(user, 0.5f * SECONDS, target_turf)))
            return ITEM_INTERACT_BLOCKING;
        if (!Dm.truthy(await useResource(cost, user, TRUE)))
            return ITEM_INTERACT_BLOCKING;

        playsound(loc as atom, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
        await Dm.new_async(selected_decoration, target_turf);
        await useResource(cost, user);

        log_tool(Dm.text("[] used [] to create [] at []", key_name(user), this, Dm.initial(selected_decoration, (atom p) => p.name), AREACOORD(target_turf)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/rcd/RDD.dm:306
    public override async DmTask<object> interact_with_atom_secondary(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        turf target_turf = get_turf(interacting_with);
        if (!Dm.truthy(target_turf))
            return NONE;

        obj_structure_decoration found = Dm.locate<obj_structure_decoration>(target_turf);

        if (!Dm.truthy(found))
            return NONE;

        playsound(target_turf, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
        if (!Dm.truthy(await do_after(user, 0.5f * SECONDS, target_turf)))
            return ITEM_INTERACT_BLOCKING;

        playsound(target_turf, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
        qdel(found);

        log_tool(Dm.text("[] used [] to deconstruct [] at []", key_name(user), this, found, AREACOORD(target_turf)));
        return ITEM_INTERACT_SUCCESS;
    }
}

// Silicons SI2 (D326): the cyborg's RDD, which runs on its cell (no model carries it in tg).
// tg: code/game/objects/items/rcd/RDD.dm:326
public partial class obj_item_construction_rdd_borg : obj_item_construction_rdd
{
    // tg: code/game/objects/items/rcd/RDD.dm:332
    public override object get_matter(mob user = null)
    {
        if (!iscyborg(user))
            return 0f;
        mob_living_silicon_robot borgy = (mob_living_silicon_robot)user;
        if (borgy.cell__obj == null)
            return 0f;
        max_matter = Dm.num(borgy.cell__obj.maxcharge);
        return borgy.cell__obj.charge;
    }

    // tg: code/game/objects/items/rcd/RDD.dm:341
    public override async DmTask<object> useResource(object amount = null, mob user = null, float? dry_run = null)
    {
        mob_living_silicon_robot borgy = user as mob_living_silicon_robot;
        if (!iscyborg(borgy))
            return FALSE;
        if (borgy.cell__obj == null)
        {
            balloon_alert(user, I18n.T("no cell found!"));
            return FALSE;
        }
        if (Dm.num(borgy.cell__obj.charge) < Dm.num(amount) * energyfactor)
        {
            balloon_alert(user, I18n.T("insufficient charge!"));
            return FALSE;
        }
        if (!Dm.truthy(dry_run))
        {
            playsound(loc, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
            return await borgy.cell__obj.use(Dm.num(amount) * energyfactor);
        }
        return TRUE;
    }
}
