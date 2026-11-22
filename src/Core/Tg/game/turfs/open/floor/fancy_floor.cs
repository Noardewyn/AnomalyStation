// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/open/floor/fancy_floor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps G2 (D291): the whole file. The wood floors unscrew (a screwdriver keeps the planks) or pry off (a crowbar breaks them),
// the grass and the fake snow dig up with a shovel (the diggable element), the carpets turn to plating when broken or burnt.
// Left out: the emissive test floor's and the neon carpet's emissive overlays (DEVIATION DV12, lighting planes), the carpet's
// narsie_act (a live-partial row, Nar'Sie).

/* In this file:
 * Wood floor
 * Bamboo floor
 * Grass floor
 * Fake Basalt
 * Carpet floor
 * Fake pits
 * Fake space
 */

// tg: code/game/turfs/open/floor/fancy_floor.dm:11
public partial class turf_open_floor_wood : turf_open_floor
{
    // tg: code/game/turfs/open/floor/fancy_floor.dm:22
    public override object broken_states()
    {
        return Dm.list("wood-broken", "wood-broken2", "wood-broken3", "wood-broken4", "wood-broken5", "wood-broken6", "wood-broken7");
    }

    // tg: code/game/turfs/open/floor/fancy_floor.dm:25
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("There's a few <b>screws</b> and a <b>small crack</b> visible.")));
        return dot;
    }

    // tg: code/game/turfs/open/floor/fancy_floor.dm:29
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(await base.screwdriver_act(user, tool)))
            return TRUE;
        return Dm.truthy(await pry_tile(tool, user)) ? TRUE : FALSE;
    }

    // tg: code/game/turfs/open/floor/fancy_floor.dm:34
    public override async DmTask<object> try_replace_tile(obj_item_stack_tile tile = null, mob user = null, DmList modifiers = null)
    {
        if (Dm.eq(tile.turf_type, type))
            return null;
        obj_item tool = user.is_holding_tool_quality(TOOL_SCREWDRIVER) as obj_item;
        if (!Dm.truthy(tool))
            tool = user.is_holding_tool_quality(TOOL_CROWBAR) as obj_item;
        if (!Dm.truthy(tool))
            return null;
        turf_open_floor_plating bare_floor = await pry_tile(tool, user, TRUE) as turf_open_floor_plating;
        if (!istype(bare_floor, typeof(turf_open_floor_plating)))
            return null;
        await bare_floor.base_item_interaction(user as mob_living, tile, modifiers);
        return null;
    }

    // tg: code/game/turfs/open/floor/fancy_floor.dm:47
    public override async DmTask<object> pry_tile(obj_item I = null, mob user = null, float? silent = null)
    {
        silent ??= FALSE;
        I.play_tool_sound(this, 80);
        return await remove_tile(user, silent, Dm.eq(I.tool_behaviour, TOOL_SCREWDRIVER) ? TRUE : FALSE);
    }

    // tg: code/game/turfs/open/floor/fancy_floor.dm:51
    public override async DmTask<object> remove_tile(mob user = null, float? silent = null, float? make_tile = null, object force_plating = null)
    {
        silent ??= FALSE;
        make_tile ??= TRUE;
        if (Dm.truthy(broken) || Dm.truthy(burnt))
        {
            broken = FALSE;
            burnt = FALSE;
            if (Dm.truthy(user) && !Dm.truthy(silent))
                to_chat(user, span_notice(I18n.T("You remove the broken planks.")));
        }
        else
        {
            if (Dm.truthy(make_tile))
            {
                if (Dm.truthy(user) && !Dm.truthy(silent))
                    to_chat(user, span_notice(I18n.T("You unscrew the planks.")));
                await spawn_tile();
            }
            else
            {
                if (Dm.truthy(user) && !Dm.truthy(silent))
                    to_chat(user, span_notice(I18n.T("You forcefully pry off the planks, destroying them in the process.")));
            }
        }
        return await make_plating(force_plating == null ? null : Dm.num(force_plating));
    }
}

// tg: code/game/turfs/open/floor/fancy_floor.dm:80
public partial class turf_open_floor_wood_tile : turf_open_floor_wood
{
    // tg: code/game/turfs/open/floor/fancy_floor.dm:84
    public override object broken_states()
    {
        return Dm.list("wood_tile-broken", "wood_tile-broken2", "wood_tile-broken3");
    }
}

// tg: code/game/turfs/open/floor/fancy_floor.dm:87
public partial class turf_open_floor_wood_parquet : turf_open_floor_wood
{
    // tg: code/game/turfs/open/floor/fancy_floor.dm:91
    public override object broken_states()
    {
        return Dm.list("wood_parquet-broken", "wood_parquet-broken2", "wood_parquet-broken3", "wood_parquet-broken4", "wood_parquet-broken5", "wood_parquet-broken6", "wood_parquet-broken7");
    }
}

// tg: code/game/turfs/open/floor/fancy_floor.dm:94
public partial class turf_open_floor_wood_large : turf_open_floor_wood
{
    // tg: code/game/turfs/open/floor/fancy_floor.dm:98
    public override object broken_states()
    {
        return Dm.list("wood_large-broken", "wood_large-broken2", "wood_large-broken3");
    }
}

// tg: code/game/turfs/open/floor/fancy_floor.dm:101
public partial class turf_open_floor_bamboo : turf_open_floor
{
    // tg: code/game/turfs/open/floor/fancy_floor.dm:116
    public override object broken_states()
    {
        return Dm.list("bamboodamaged");
    }
}

// tg: code/game/turfs/open/floor/fancy_floor.dm:126
public partial class turf_open_floor_bamboo_tatami : turf_open_floor_bamboo
{
    // tg: code/game/turfs/open/floor/fancy_floor.dm:133
    public override object broken_states()
    {
        // This state doesn't exist why is it here?
        return Dm.list("tatami-damaged");
    }
}

// tg: code/game/turfs/open/floor/fancy_floor.dm:147
public partial class turf_open_floor_grass : turf_open_floor
{
    // tg: code/game/turfs/open/floor/fancy_floor.dm:161
    public override object broken_states()
    {
        return Dm.list(Dm.text("[]_damaged", Dm.initial(this, nameof(icon_state))));
    }

    // tg: code/game/turfs/open/floor/fancy_floor.dm:164
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        spawniconchange();
        Waiting.NoSleep(AddElement(typeof(datum_element_diggable), typeof(obj_item_stack_ore_glass), 2, Dm.named("worm_chance", 50),
            Dm.named("action_text", I18n.T("uproot")), Dm.named("action_text_third_person", I18n.T("uproots"))));
        return dot;
    }

    // tg: code/game/turfs/open/floor/fancy_floor.dm:173
    public virtual void spawniconchange()
    {
        icon_state = Dm.text("grass[]", rand(0, 3));
    }
}

// tg: code/game/turfs/open/floor/fancy_floor.dm:176
public partial class turf_open_floor_grass_norandomicon : turf_open_floor_grass
{
    // tg: code/game/turfs/open/floor/fancy_floor.dm:178
    public override void spawniconchange()
    {
        return;
    }
}

// tg: code/game/turfs/open/floor/fancy_floor.dm:181
public partial class turf_open_floor_grass_lavaland : turf_open_floor_grass
{
    // tg: code/game/turfs/open/floor/fancy_floor.dm:189
    public override void spawniconchange()
    {
        return;
    }
}

// tg: code/game/turfs/open/floor/fancy_floor.dm:192
public partial class turf_open_floor_grass_fairy : turf_open_floor_grass
{
    // tg: code/game/turfs/open/floor/fancy_floor.dm:201
    public override void spawniconchange()
    {
        icon_state = Dm.text("fairygrass[]", rand(0, 3));
    }
}

// tg: code/game/turfs/open/floor/fancy_floor.dm:220
public partial class turf_open_floor_fake_snow : turf_open_floor
{
    // tg: code/game/turfs/open/floor/fancy_floor.dm:241
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_diggable), typeof(obj_item_stack_tile_mineral_snow), 1, Dm.named("worm_chance", 0)));
        return dot;
    }

    // tg: code/game/turfs/open/floor/fancy_floor.dm:245
    public override object broken_states()
    {
        return Dm.list("snow_dug");
    }

    // tg: code/game/turfs/open/floor/fancy_floor.dm:248
    public override async DmTask<object> try_replace_tile(obj_item_stack_tile tile = null, mob user = null, DmList modifiers = null)
    {
        return null;
    }

    // tg: code/game/turfs/open/floor/fancy_floor.dm:251
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return null;
    }
}

// tg: code/game/turfs/open/floor/fancy_floor.dm:254
public partial class turf_open_floor_fakebasalt : turf_open_floor
{
    // tg: code/game/turfs/open/floor/fancy_floor.dm:268
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_diggable), typeof(obj_item_stack_ore_glass_basalt), 2, Dm.named("worm_chance", 0)));
        if (prob(15))
        {
            icon_state = Dm.text("basalt[]", rand(0, 12));
            switch (icon_state)
            {
                case "basalt1":
                case "basalt2":
                case "basalt3":
                    Waiting.NoSleep(set_light(BASALT_LIGHT_RANGE_BRIGHT, BASALT_LIGHT_POWER, LIGHT_COLOR_LAVA));
                    break;
                case "basalt5":
                case "basalt9":
                    Waiting.NoSleep(set_light(BASALT_LIGHT_RANGE_DIM, BASALT_LIGHT_POWER, LIGHT_COLOR_LAVA));
                    break;
            }
        }
        return dot;
    }
}

// tg: code/game/turfs/open/floor/fancy_floor.dm:279
public partial class turf_open_floor_carpet : turf_open_floor
{
    // tg: code/game/turfs/open/floor/fancy_floor.dm:298
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("There's a <b>small crack</b> on the edge of it.")));
        return dot;
    }

    // tg: code/game/turfs/open/floor/fancy_floor.dm:302
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance();
        return dot;
    }

    // tg: code/game/turfs/open/floor/fancy_floor.dm:306
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        updates ??= ALL;
        object dot = await base.update_icon(updates, __extra);
        if (!Dm.truthy(dot) || !Dm.truthy(Dm.bitand(updates, UPDATE_SMOOTHING)))
            return dot;
        if (!Dm.truthy(broken) && !Dm.truthy(burnt))
        {
            if (Dm.truthy(Dm.bitand(smoothing_flags, USES_SMOOTHING)))
                QUEUE_SMOOTH(this);
        }
        else
        {
            // TURF-IDENTITY: tg's src becomes the plating; the smoothing queue is for the cell's neighbours, so the new turf is queued
            atom plating = await make_plating() as atom;
            atom src = plating ?? this;
            if (Dm.truthy(Dm.bitand(src.smoothing_flags, USES_SMOOTHING)))
                QUEUE_SMOOTH_NEIGHBORS(src);
        }
        return dot;
    }

    // tg: code/game/turfs/open/floor/fancy_floor.dm:527
    public override object break_tile()
    {
        broken = TRUE;
        update_appearance();
        return null;
    }

    // tg: code/game/turfs/open/floor/fancy_floor.dm:531
    public override object burn_tile()
    {
        burnt = TRUE;
        update_appearance();
        return null;
    }

    // tg: code/game/turfs/open/floor/fancy_floor.dm:535
    public override object get_smooth_underlay_icon(mutable_appearance underlay_appearance = null, turf asking_turf = null, object adjacency_dir = null)
    {
        return FALSE;
    }
}

/// An emissive turf used to test emissive turfs.
// tg: code/game/turfs/open/floor/fancy_floor.dm:539
public partial class turf_open_floor_emissive_test : turf_open_floor
{
    // tg: code/game/turfs/open/floor/fancy_floor.dm:544
    public override void smooth_icon__proc()
    {
        base.smooth_icon__proc();
        update_appearance(Dm.bitnot(UPDATE_SMOOTHING));
    }

    // tg: code/game/turfs/open/floor/fancy_floor.dm:548
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        // DEVIATION(DV12): . += emissive_appearance(icon, icon_state, src, alpha = src.alpha), the glow on the emissive plane
        Dm.Deferred("code/game/turfs/open/floor/fancy_floor.dm:550", "update_overlays: emissive_appearance of the test floor (DEVIATION DV12)");
        return dot;
    }
}

// tg: code/game/turfs/open/floor/fancy_floor.dm:557
public partial class turf_open_floor_carpet_neon : turf_open_floor_carpet
{
    // tg: code/game/turfs/open/floor/fancy_floor.dm:576
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_decal), neon_icon ?? icon, neon_icon_state ?? base_icon_state, dir, null, null, alpha, neon_color, smoothing_junction));
        // DEVIATION(DV12): the second decal, on EMISSIVE_PLANE with emissive_alpha, is the glow of lighting planes
        Dm.Deferred("code/game/turfs/open/floor/fancy_floor.dm:579", "Initialize: the neon carpet's emissive decal (DEVIATION DV12)");
        return dot;
    }
}

// tg: code/game/turfs/open/floor/fancy_floor.dm:930
public partial class turf_open_floor_fakepit : turf_open_floor
{
    // tg: code/game/turfs/open/floor/fancy_floor.dm:940
    public override object get_smooth_underlay_icon(mutable_appearance underlay_appearance = null, turf asking_turf = null, object adjacency_dir = null)
    {
        underlay_appearance.icon = Dm.resource("icons/turf/floors.dmi");
        underlay_appearance.icon_state = "basalt";
        return TRUE;
    }
}

// tg: code/game/turfs/open/floor/fancy_floor.dm:953
public partial class turf_open_floor_fakeice_slippery : turf_open_floor_fakeice
{
    // tg: code/game/turfs/open/floor/fancy_floor.dm:954
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(MakeSlippery(TURF_WET_PERMAFROST, INFINITY, 0, INFINITY, TRUE));
        return dot;
    }
}

// tg: code/game/turfs/open/floor/fancy_floor.dm:959
public partial class turf_open_floor_fakespace : turf_open_floor
{
    // tg: code/game/turfs/open/floor/fancy_floor.dm:967
    public override object broken_states()
    {
        return Dm.list("damaged");
    }

    // tg: code/game/turfs/open/floor/fancy_floor.dm:970
    public override object get_smooth_underlay_icon(mutable_appearance underlay_appearance = null, turf asking_turf = null, object adjacency_dir = null)
    {
        generate_space_underlay(underlay_appearance, asking_turf);
        return TRUE;
    }
}
