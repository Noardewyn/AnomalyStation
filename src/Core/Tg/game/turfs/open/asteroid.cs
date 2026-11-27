// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/open/asteroid.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps G2, D291): the asteroid sand and the basalt floors (asteroid.dm:1-240), the floor a mined rock leaves (Meta's 3
// airless basalt tiles and the volcanic rocks' floor): a shovel or a pickaxe digs sand or basalt out of them, the basalt's
// glowing variants light up. Not here: the mechs' drill (a live-partial row); the snow, ice, sand and lavaland biome floors of the
// rest of the file keep their own procs and stay inert until mining and the planets.

/**********************Asteroid**************************/

// tg: code/game/turfs/open/asteroid.dm:5
public partial class turf_open_misc_asteroid : turf_open_misc
{
    // tg: code/game/turfs/open/asteroid.dm:3  #define DIG_SHEET_AMOUNT 5 (#undef at the end of the file)
    internal const int DIG_SHEET_AMOUNT = 5;

    // tg: code/game/turfs/open/asteroid.dm:34
    public override object broken_states()
    {
        if (Dm.truthy(Dm.initial(this, nameof(dug))))
            return Dm.list(icon_state);
        return Dm.list(Dm.text("[]_dug", base_icon_state));
    }

    // tg: code/game/turfs/open/asteroid.dm:39
    public override object break_tile()
    {
        object dot = base.break_tile();
        if (!Dm.truthy(dot))
            return FALSE;
        dug = TRUE;
        return TRUE;
    }

    // tg: code/game/turfs/open/asteroid.dm:46
    public override object burn_tile()
    {
        return null;
    }

    // tg: code/game/turfs/open/asteroid.dm:49
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        string proper_name = name;
        object dot = base.Initialize(mapload, __extra);
        name = proper_name;
        if (Dm.truthy(has_floor_variance) && prob(floor_variance))
            icon_state = Dm.text("[][]", base_icon_state, rand(0, 12));
        return dot;
    }

    // tg: code/game/turfs/open/asteroid.dm:56
    public override async DmTask MakeSlippery(float? wet_setting = null, float? min_wet_time = null, float? wet_time_to_add = null, float? max_wet_time = null, float? permanent = null, float? should_display_overlay = null)
    {
        return;
    }

    // tg: code/game/turfs/open/asteroid.dm:59
    public override void MakeDry(float? wet_setting = null, float? immediate = null, float? amount = null)
    {
        return;
    }

    // tg: code/game/turfs/open/asteroid.dm:62
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        return FALSE;
    }

    // tg: code/game/turfs/open/asteroid.dm:65
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.item_interaction(user, tool, modifiers, __extra);
        if (Dm.truthy(Dm.bitand(ITEM_INTERACT_ANY_BLOCKER, dot)))
            return dot;

        if (istype(tool, typeof(obj_item_storage_bag_ore)))
        {
            foreach (obj_item_stack_ore dropped_ore in Dm.each<obj_item_stack_ore>(this))
            {
                if (Dm.truthy(tool._listen_lookup?[COMSIG_ATOM_ATTACKBY]))
                    tool._SendSignal(COMSIG_ATOM_ATTACKBY, Dm.list(tool, dropped_ore));
            }
            return ITEM_INTERACT_SUCCESS;
        }

        if (!Dm.eq(tool.tool_behaviour, TOOL_SHOVEL) && !Dm.eq(tool.tool_behaviour, TOOL_MINING))
            return dot; // it still could be SKIP_TO_ATTACK and we don't want to step on that

        if (!Dm.truthy(can_dig(user)))
            return ITEM_INTERACT_BLOCKING;

        if (!isturf(user.loc))
            return ITEM_INTERACT_BLOCKING;

        balloon_alert(user, I18n.T("digging..."));

        // TURF-IDENTITY: tg's src after the wait is the turf at this cell
        float cell_x = x, cell_y = y, cell_z = z;
        if (!Dm.truthy(await tool.use_tool(this, user, 4 * SECONDS, volume: 50)))
            return ITEM_INTERACT_BLOCKING;

        turf_open_misc_asteroid src = locate(cell_x, cell_y, cell_z) as turf_open_misc_asteroid;
        if (src == null || !Dm.truthy(src.can_dig(user)))
            return ITEM_INTERACT_BLOCKING;

        await src.getDug();
        SSblackbox.record_feedback("tally", "pick_used_mining", 1, tool.type);
        return ITEM_INTERACT_SUCCESS;
    }

    /// Drops itemstack when dug and changes icon
    // tg: code/game/turfs/open/asteroid.dm:97
    public virtual async DmTask<object> getDug()
    {
        if (!Dm.truthy(break_tile()))
            return FALSE;
        Dm.@new(dig_result, this, DIG_SHEET_AMOUNT);
        if (prob(worm_chance))
            Dm.@new<obj_item_food_bait_worm>(this);
        return TRUE;
    }

    /// If the user can dig the turf
    // tg: code/game/turfs/open/asteroid.dm:106
    public virtual object can_dig(mob user = null)
    {
        if (!Dm.truthy(dug) && !Dm.truthy(broken))
            return TRUE;
        if (Dm.truthy(user))
            balloon_alert(user, I18n.T("already excavated!"));
        return FALSE;
    }

    ///Refills the previously dug tile
    // tg: code/game/turfs/open/asteroid.dm:114
    public virtual async DmTask refill_dug()
    {
        dug = FALSE;
        broken = FALSE;
        icon_state = (string)base_icon_state;
        if (Dm.truthy(has_floor_variance) && prob(floor_variance))
            icon_state = Dm.text("[][]", base_icon_state, rand(0, 12));
        update_appearance();
    }
}

// tg: code/game/turfs/open/asteroid.dm:125
public partial class turf_open_misc_asteroid_dug : turf_open_misc_asteroid
{
    // tg: code/game/turfs/open/asteroid.dm:131
    public override object broken_states()
    {
        return Dm.list("asteroid_dug");
    }
}

/// Used by ashstorms to replenish basalt tiles that have been dug up without going through all of them.
public partial class datum_controller_global_vars
{
    // tg: code/game/turfs/open/asteroid.dm:140  GLOBAL_LIST_EMPTY(dug_up_basalt)
    public DmList dug_up_basalt;

    // tg: code/game/turfs/open/asteroid.dm:140  (GLOBAL_MANAGED(dug_up_basalt, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaldug_up_basalt()
    {
        dug_up_basalt = Dm.list();
        Dm.append(ref gvars_datum_init_order, "dug_up_basalt");
    }
}

// tg: code/game/turfs/open/asteroid.dm:142
public partial class turf_open_misc_asteroid_basalt : turf_open_misc_asteroid
{
    // tg: code/game/turfs/open/asteroid.dm:152
    public override async DmTask<object> getDug()
    {
        object dot = await base.getDug();
        if (!Dm.truthy(dot))
            return dot;
        await set_light(0);
        Dm.combine(ref GLOB.dug_up_basalt, this);
        return dot;
    }

    // tg: code/game/turfs/open/asteroid.dm:159
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.dug_up_basalt, this);
        return base.Destroy(force);
    }

    // tg: code/game/turfs/open/asteroid.dm:163
    public override async DmTask refill_dug()
    {
        await base.refill_dug();
        Dm.remove(ref GLOB.dug_up_basalt, this);
        await set_basalt_light();
    }

    // tg: code/game/turfs/open/asteroid.dm:175
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(set_basalt_light());
        return dot;
    }

    // tg: code/game/turfs/open/asteroid.dm:179
    public virtual async DmTask set_basalt_light()
    {
        switch (icon_state)
        {
            case "basalt1":
            case "basalt2":
            case "basalt3":
                await set_light(BASALT_LIGHT_RANGE_BRIGHT, BASALT_LIGHT_POWER, LIGHT_COLOR_LAVA); //more light
                break;
            case "basalt5":
            case "basalt9":
                await set_light(BASALT_LIGHT_RANGE_DIM, BASALT_LIGHT_POWER, LIGHT_COLOR_LAVA); //barely anything!
                break;
        }
    }
}

/// Variant for ruins which replaces itself with the floor of a biome it generates in
// tg: code/game/turfs/open/asteroid.dm:199  (the ruins' floor that takes the biome's about it)
public partial class turf_open_misc_asteroid_basalt_lava_land_surface_biome_replace : turf_open_misc_asteroid_basalt_lava_land_surface
{
    // tg: code/game/turfs/open/asteroid.dm:206
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // Just spawn a normal lavaland rock if we fail to get a mapgen, such as being spawned over lava
        Type supposed_type = typeof(turf_open_misc_asteroid_basalt_lava_land_surface);
        var cur_area = loc as area;
        var map_generator = cur_area?.get_generator();
        if (map_generator == null)
        {
            cur_area = GLOB.areas_by_type[default_area] as area;
            map_generator = cur_area?.get_generator();
        }

        if (map_generator is datum_map_generator_cave_generator cave_generator)
        {
            object biome = cave_generator.get_biome_for_turf(this);
            if (Dm.truthy(biome))
            {
                var generating_biome = (datum_biome)SSmapping.biomes[biome];
                supposed_type = generating_biome.open_turf_type;
            }
        }

        float cur_flags = turf_flags;
        // TURF-IDENTITY: tg's src becomes the new turf
        var new_turf = Waiting.NoSleep(ChangeTurf(supposed_type, flags: Dm.bitor(CHANGETURF_FORCEOP, CHANGETURF_INHERIT_AIR))) as turf;
        if (Dm.truthy(Dm.bitand(cur_flags, NO_RUINS)))
            new_turf.turf_flags = Dm.bitor(new_turf.turf_flags, NO_RUINS);
        return dot;
    }
}

// tg: code/game/turfs/open/asteroid.dm:321  (Lavaland's winter biodome)
public partial class turf_open_misc_asteroid_snow : turf_open_misc_asteroid
{
    // tg: code/game/turfs/open/asteroid.dm:339
    public override object burn_tile()
    {
        if (!Dm.truthy(burnt))
        {
            visible_message(span_danger(I18n.T("[] melts away!.", this)));
            slowdown = 0;
            burnt = TRUE;
            update_appearance();
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/game/turfs/open/asteroid.dm:348
    public override object burnt_states() => Dm.list("snow_dug");

    // tg: code/game/turfs/open/asteroid.dm:351
    public override void add_footprint(mob_living_carbon_human walker = null, object movement_direction = null)
    {
        if (HAS_TRAIT(walker, TRAIT_NO_SNOWPRINTS))
            return;
        // skip the special logic if the level doesn't naturally have snowstorms
        if (!Dm.truthy(SSmapping.level_trait(z, ZTRAIT_SNOWSTORM)))
        {
            base.add_footprint(walker, movement_direction);
            return;
        }

        // if an active snow storm affecting this turf is currently in its main or wind down stage, skip footprint creation
        foreach (datum_weather_snow_storm active_weather in Dm.each<datum_weather_snow_storm>(SSweather.processing))
        {
            if (!Dm.eq(active_weather.stage, MAIN_STAGE) && !Dm.eq(active_weather.stage, WIND_DOWN_STAGE))
                continue;
            if (!Dm.@in(loc, active_weather.impacted_areas))
                continue;
            return;
        }

        base.add_footprint(walker, movement_direction);
        // when a snow storm enters its main stage, clear all of our footprints
        foreach (Type snow_type in Dm.each<Type>(typesof(typeof(datum_weather_snow_storm))))
            RegisterSignal(SSdcs, COMSIG_WEATHER_START(snow_type), nameof(snow_clear_footprints), @override: TRUE);
    }

    // tg: code/game/turfs/open/asteroid.dm:371
    [SignalHandler]
    public virtual void snow_clear_footprints(datum source = null, datum_weather storm = null)
    {
        if (!Dm.@in(loc, storm.impacted_areas))
            return;

        clear_footprints();
        foreach (Type snow_type in Dm.each<Type>(typesof(typeof(datum_weather_snow_storm))))
            UnregisterSignal(SSdcs, COMSIG_WEATHER_START(snow_type));
    }

    // tg: code/game/turfs/open/asteroid.dm:381
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_stack_sheet_mineral_snow)
            return await base.item_interaction(user, tool, modifiers, __extra);

        if (Dm.truthy(dug))
        {
            if (Dm.truthy(await tool.use(DIG_SHEET_AMOUNT)))
            {
                user.visible_message(
                    span_notice(I18n.T("[] packs [] back in.", user, this)),
                    span_notice(I18n.T("You pack [] back in.", this)),
                    vision_distance: COMBAT_MESSAGE_RANGE);
                await refill_dug();
                return ITEM_INTERACT_SUCCESS;
            }

            to_chat(user, I18n.T("You don't have enough [] to fill the hole.", tool.name));
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.truthy(footprint_entrance_dirs) || Dm.truthy(footprint_exit_dirs))
        {
            if (Dm.truthy(await tool.use(1)))
            {
                user.visible_message(
                    span_notice(I18n.T("[] fills in the footprints in [].", user, this)),
                    span_notice(I18n.T("You fill in the footprints in [].", this)),
                    vision_distance: COMBAT_MESSAGE_RANGE);
                clear_footprints();
                return ITEM_INTERACT_SUCCESS;
            }

            return NONE;
        }

        return NONE;
    }
}

// tg: code/game/turfs/open/asteroid.dm:242  (D304: Lavaland's shale and siderite floors)
public partial class turf_open_misc_asteroid_basalt_smooth : turf_open_misc_asteroid_basalt
{
    // tg: code/game/turfs/open/asteroid.dm:254
    public override object set_smoothed_icon_state(object new_junction = null)
    {
        object dot = base.set_smoothed_icon_state(new_junction);
        if (Dm.eq(new_junction, 255) && Dm.truthy(variant_dmi))
        {
            icon = variant_dmi;
            icon_state = Dm.text("[][]", base_icon_state, Dm.rand(1, variant_num));
        }
        else
        {
            icon = Dm.initial(this, nameof(icon));
        }
        return dot;
    }

    // tg: code/game/turfs/open/asteroid.dm:262
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (!Dm.eq(smoothing_junction, 255) && Dm.truthy(variant_dmi))
            dot = Dm.add(Dm.list(mutable_appearance(variant_dmi, Dm.text("[][]", base_icon_state, Dm.rand(1, variant_num)))), dot);
        return dot;
    }

    // tg: code/game/turfs/open/asteroid.dm:267
    public override object get_smooth_underlay_icon(mutable_appearance underlay_appearance = null, turf asking_turf = null, object adjacency_dir = null)
    {
        object dot = base.get_smooth_underlay_icon(underlay_appearance, asking_turf, adjacency_dir);
        if (!Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(smoothing_flags))
            return dot;
        underlay_appearance.transform = transform;
        return dot;
    }

    // tg: code/game/turfs/open/asteroid.dm:275  (no ..(): the basalt's dug list and light are not touched)
    public override async DmTask refill_dug()
    {
        dug = FALSE;
        broken = FALSE;
        set_smoothed_icon_state(smoothing_junction);
        update_appearance();
        await DmTask.CompletedTask;
    }
}
