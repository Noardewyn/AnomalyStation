// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/decorations.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// E13 (the RCD family): the whole file, the plastic decorations the rapid decoration device places: their init, the plastic litter
// of a broken one and each random style's own picture. E15 translated the examine (the owner's rule of 2026-10-29). The var
// blocks are generated.

// tg: code/game/objects/structures/decorations.dm:1
public partial class obj_structure_decoration : obj_structure
{
    // tg: code/game/objects/structures/decorations.dm:12
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        return dot;
    }

    // tg: code/game/objects/structures/decorations.dm:15
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        disassembled ??= TRUE;
        if (!Dm.truthy(disassembled))
            Dm.@new<obj_effect_decal_cleanable_plastic>(loc);
        return;
    }

    // tg: code/game/objects/structures/decorations.dm:20  (E15)
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("It's made of cheap, hollow plastic.")));
        return dot;
    }
}

// tg: code/game/objects/structures/decorations.dm:39
public partial class obj_structure_decoration_grass_style_random : obj_structure_decoration_grass
{
    // tg: code/game/objects/structures/decorations.dm:39
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("snowgrass[]gb", rand(1, 3));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/decorations.dm:56
public partial class obj_structure_decoration_grass_brown_style_random : obj_structure_decoration_grass_brown
{
    // tg: code/game/objects/structures/decorations.dm:56
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("snowgrass[]bb", rand(1, 3));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/decorations.dm:81
public partial class obj_structure_decoration_jungle_grass_style_random : obj_structure_decoration_jungle_grass
{
    // tg: code/game/objects/structures/decorations.dm:81
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("grassa[]", rand(1, 5));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/decorations.dm:104
public partial class obj_structure_decoration_jungle_grass_b_style_random : obj_structure_decoration_jungle_grass_b
{
    // tg: code/game/objects/structures/decorations.dm:104
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("grassb[]", rand(1, 5));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/decorations.dm:126
public partial class obj_structure_decoration_bush_style_random : obj_structure_decoration_bush
{
    // tg: code/game/objects/structures/decorations.dm:126
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("firstbush_[]", rand(1, 4));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/decorations.dm:147
public partial class obj_structure_decoration_bush_reed_style_random : obj_structure_decoration_bush_reed
{
    // tg: code/game/objects/structures/decorations.dm:147
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("reedbush_[]", rand(1, 4));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/decorations.dm:165
public partial class obj_structure_decoration_bush_leafy_style_random : obj_structure_decoration_bush_leafy
{
    // tg: code/game/objects/structures/decorations.dm:165
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("leafybush_[]", rand(1, 3));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/decorations.dm:186
public partial class obj_structure_decoration_bush_pale_style_random : obj_structure_decoration_bush_pale
{
    // tg: code/game/objects/structures/decorations.dm:186
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("palebush_[]", rand(1, 4));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/decorations.dm:204
public partial class obj_structure_decoration_bush_stalky_style_random : obj_structure_decoration_bush_stalky
{
    // tg: code/game/objects/structures/decorations.dm:204
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("stalkybush_[]", rand(1, 3));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/decorations.dm:225
public partial class obj_structure_decoration_bush_grassy_style_random : obj_structure_decoration_bush_grassy
{
    // tg: code/game/objects/structures/decorations.dm:225
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("grassybush_[]", rand(1, 4));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/decorations.dm:243
public partial class obj_structure_decoration_bush_sparsegrass_style_random : obj_structure_decoration_bush_sparsegrass
{
    // tg: code/game/objects/structures/decorations.dm:243
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("sparsegrass_[]", rand(1, 3));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/decorations.dm:261
public partial class obj_structure_decoration_bush_fullgrass_style_random : obj_structure_decoration_bush_fullgrass
{
    // tg: code/game/objects/structures/decorations.dm:261
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("fullgrass_[]", rand(1, 3));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/decorations.dm:279
public partial class obj_structure_decoration_bush_ferny_style_random : obj_structure_decoration_bush_ferny
{
    // tg: code/game/objects/structures/decorations.dm:279
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("fernybush_[]", rand(1, 3));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/decorations.dm:297
public partial class obj_structure_decoration_bush_sunny_style_random : obj_structure_decoration_bush_sunny
{
    // tg: code/game/objects/structures/decorations.dm:297
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("sunnybush_[]", rand(1, 3));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/decorations.dm:318
public partial class obj_structure_decoration_bush_generic_style_random : obj_structure_decoration_bush_generic
{
    // tg: code/game/objects/structures/decorations.dm:318
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("genericbush_[]", rand(1, 4));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/decorations.dm:339
public partial class obj_structure_decoration_bush_pointy_style_random : obj_structure_decoration_bush_pointy
{
    // tg: code/game/objects/structures/decorations.dm:339
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("pointybush_[]", rand(1, 4));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/decorations.dm:360
public partial class obj_structure_decoration_bush_lavendergrass_style_random : obj_structure_decoration_bush_lavendergrass
{
    // tg: code/game/objects/structures/decorations.dm:360
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("lavendergrass_[]", rand(1, 4));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/decorations.dm:378
public partial class obj_structure_decoration_bush_flowers_yw_style_random : obj_structure_decoration_bush_flowers_yw
{
    // tg: code/game/objects/structures/decorations.dm:378
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("ywflowers_[]", rand(1, 3));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/decorations.dm:396
public partial class obj_structure_decoration_bush_flowers_br_style_random : obj_structure_decoration_bush_flowers_br
{
    // tg: code/game/objects/structures/decorations.dm:396
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("brflowers_[]", rand(1, 3));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/decorations.dm:414
public partial class obj_structure_decoration_bush_flowers_pp_style_random : obj_structure_decoration_bush_flowers_pp
{
    // tg: code/game/objects/structures/decorations.dm:414
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("ppflowers_[]", rand(1, 3));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/decorations.dm:442
public partial class obj_structure_decoration_bush_snow_style_random : obj_structure_decoration_bush_snow
{
    // tg: code/game/objects/structures/decorations.dm:442
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("snowbush[]", rand(1, 6));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/decorations.dm:461
public partial class obj_structure_decoration_bush_jungle_style_random : obj_structure_decoration_bush_jungle
{
    // tg: code/game/objects/structures/decorations.dm:461
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("busha[]", rand(1, 3));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/decorations.dm:478
public partial class obj_structure_decoration_bush_jungle_b_style_random : obj_structure_decoration_bush_jungle_b
{
    // tg: code/game/objects/structures/decorations.dm:478
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("bushb[]", rand(1, 3));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/decorations.dm:495
public partial class obj_structure_decoration_bush_jungle_c_style_random : obj_structure_decoration_bush_jungle_c
{
    // tg: code/game/objects/structures/decorations.dm:495
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("bushc[]", rand(1, 3));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/decorations.dm:519
public partial class obj_structure_decoration_bush_large_style_random : obj_structure_decoration_bush_large
{
    // tg: code/game/objects/structures/decorations.dm:519
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("bush[]", rand(1, 3));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/decorations.dm:541
public partial class obj_structure_decoration_rock_style_random : obj_structure_decoration_rock
{
    // tg: code/game/objects/structures/decorations.dm:541
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("basalt[]", rand(1, 4));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/decorations.dm:560
public partial class obj_structure_decoration_rock_pile_style_random : obj_structure_decoration_rock_pile
{
    // tg: code/game/objects/structures/decorations.dm:560
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("lavarocks[]", Dm.pick_weighted((3, 1), (3, 2), (1, 3)));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/decorations.dm:585
public partial class obj_structure_decoration_rock_pile_jungle_style_random : obj_structure_decoration_rock_pile_jungle
{
    // tg: code/game/objects/structures/decorations.dm:585
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("rock[]", rand(1, 5));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/decorations.dm:606
public partial class obj_structure_decoration_rock_pile_jungle_large_style_random : obj_structure_decoration_rock_pile_jungle_large
{
    // tg: code/game/objects/structures/decorations.dm:606
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("rocks[]", rand(1, 3));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/decorations.dm:631
public partial class obj_structure_decoration_rock_volcano_style_random : obj_structure_decoration_rock_volcano
{
    // tg: code/game/objects/structures/decorations.dm:631
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("volcano_[]", rand(1, 5));
        update_appearance();
        return dot;
    }
}

// tg: code/game/objects/structures/decorations.dm:650
public partial class obj_structure_decoration_misc_glowgrowth_style_random : obj_structure_decoration_misc_glowgrowth
{
    // tg: code/game/objects/structures/decorations.dm:650
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        icon_state = Dm.text("glowgrowth[]", rand(1, 3));
        update_appearance();
        return dot;
    }
}
