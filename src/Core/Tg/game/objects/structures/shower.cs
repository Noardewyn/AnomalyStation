// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/shower.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps H1: the whole file (the shower: its three auto shut-off modes, the thermostat, the spray that washes and exposes everything on
// its tile to its reagents with the washing effect, the mist, the wet floor it leaves, its sound loop, the water reclaimer and the
// plumbing; the shower frame; the mist effect), with the shower's mood event (live since gaps MS1). The var blocks are generated (D111).

// tg: code/game/objects/structures/shower.dm:23  GLOBAL_LIST_INIT(shower_mode_descriptions, ...)
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/game/objects/structures/shower.dm:23
    public DmList shower_mode_descriptions;

    // tg: code/game/objects/structures/shower.dm:23  (GLOBAL_MANAGED, __DEFINES/__globals.dm:5)
    public virtual void InitGlobalshower_mode_descriptions()
    {
        shower_mode_descriptions = Dm.list(
            Dm.kv(Dm.text("[]", obj_machinery_shower.SHOWER_MODE_UNTIL_EMPTY), "run until empty"),
            Dm.kv(Dm.text("[]", obj_machinery_shower.SHOWER_MODE_TIMED), "run for 15 seconds or until empty"),
            Dm.kv(Dm.text("[]", obj_machinery_shower.SHOWER_MODE_FOREVER), "keep running forever and auto turn back on"));
        Dm.append(ref gvars_datum_init_order, "shower_mode_descriptions");
    }
}

// tg: code/game/objects/structures/shower.dm:29
public partial class obj_machinery_shower : obj_machinery
{
    // tg: code/game/objects/structures/shower.dm:1-20 (the file's #defines, #undef'd at its end)
    internal const string SHOWER_FREEZING = "freezing";
    internal const float SHOWER_FREEZING_TEMP = 100;
    internal const string SHOWER_NORMAL = "normal";
    internal const float SHOWER_NORMAL_TEMP = 300;
    internal const string SHOWER_BOILING = "boiling";
    internal const float SHOWER_BOILING_TEMP = 400;
    /// The volume of its internal reagents the shower applies to everything it sprays.
    internal const float SHOWER_SPRAY_VOLUME = 5;
    /// How much the volume of the shower's spay reagents are amplified by when it sprays something.
    internal const float SHOWER_EXPOSURE_MULTIPLIER = 2; // Showers effectively double exposed reagents
    /// How long we run in TIMED mode
    internal const float SHOWER_TIMED_LENGTH = 15 * SECONDS;
    /// Run the shower until we run out of reagents.
    internal const float SHOWER_MODE_UNTIL_EMPTY = 0;
    /// Run the shower for SHOWER_TIMED_LENGTH time, or until we run out of reagents.
    internal const float SHOWER_MODE_TIMED = 1;
    /// Run the shower forever, pausing when we run out of liquid, and then resuming later.
    internal const float SHOWER_MODE_FOREVER = 2;
    /// Number of modes to cycle through
    internal const float SHOWER_MODE_COUNT = 3;

    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_machinery_shower__Initialize__loc_connections;

    // tg: code/game/objects/structures/shower.dm:63
    [DmParams("mapload", "ndir", "has_water_reclaimer")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object ndir = Dm.arg(__extra, 0) ?? 0;
        object has_water_reclaimer = Dm.arg(__extra, 1);
        object dot = base.Initialize(mapload, __extra);

        if (Dm.truthy(ndir))
            dir = Dm.num(ndir);

        if (has_water_reclaimer != null)
            this.has_water_reclaimer = Dm.num(has_water_reclaimer);

        float shift = Dm.num(pixel_shift);
        switch (dir)
        {
            case NORTH:
                pixel_x = 0;
                pixel_y = -shift;
                break;
            case SOUTH:
                pixel_x = 0;
                pixel_y = shift;
                break;
            case EAST:
                pixel_x = -shift;
                pixel_y = 0;
                break;
            case WEST:
                pixel_x = shift;
                pixel_y = 0;
                break;
        }

        create_reagents(reagent_capacity);
        if (Dm.truthy(this.has_water_reclaimer))
            Waiting.NoSleep(((datum_reagents)reagents).add_reagent(reagent_id, reagent_capacity));
        soundloop = Dm.@new<datum_looping_sound_showering>(this, FALSE);
        Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_simple_demand_extended)));
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));
        return dot;
    }

    // tg: code/game/objects/structures/shower.dm:97
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_notice(I18n.T("It looks like the thermostat has an adjustment screw.")));
        if (Dm.truthy(has_water_reclaimer))
            Dm.append(ref dot, span_notice(I18n.T("A water recycler is installed. It looks like you could pry it out.")));
        Dm.append(ref dot, span_notice(I18n.T("The auto shut-off is programmed to [].", I18n.T(Dm.str(GLOB.shower_mode_descriptions[Dm.text("[]", mode)])))));
        Dm.append(ref dot, span_notice(I18n.T("[]/[] liquids remaining.", ((datum_reagents)reagents).total_volume, ((datum_reagents)reagents).maximum_volume)));
        return dot;
    }

    // tg: code/game/objects/structures/shower.dm:105
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref soundloop);
        qdel(reagents);
        reagents = null;
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/shower.dm:110
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        object dot = await base.interact(user, __extra);
        if (Dm.truthy(dot))
            return dot;

        intended_on = Dm.truthy(intended_on) ? FALSE : TRUE;
        if (!update_actually_on(Dm.truthy(intended_on)))
        {
            balloon_alert(user, Dm.text("[] is dry!", this));
            return FALSE;
        }

        balloon_alert(user, Dm.truthy(intended_on) ? "turned on" : "turned off");

        return TRUE;
    }

    // tg: code/game/objects/structures/shower.dm:124
    public override object analyzer_act(mob_living user = null, obj_item tool = null)
    {
        base.analyzer_act(user, tool);

        tool.play_tool_sound(this);
        to_chat(user, span_notice(I18n.T("The water temperature seems to be [].", I18n.T(current_temperature))));
        return TRUE;
    }

    // tg: code/game/objects/structures/shower.dm:131
    public override async DmTask<object> plunger_act(obj_item_plunger attacking_plunger = null, mob_living user = null, object reinforced = null)
    {
        user.balloon_alert_to_viewers("furiously plunging...", "plunging shower...");
        if (!Dm.truthy(await do_after(user, 3 * SECONDS, target: this)))
            return TRUE;
        user.balloon_alert_to_viewers("finished plunging");
        await ((datum_reagents)reagents).expose(get_turf(this), TOUCH); //splash on the floor
        await ((datum_reagents)reagents).clear_reagents();
        begin_processing();
        return TRUE;
    }

    // tg: code/game/objects/structures/shower.dm:141
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_stock_parts_water_recycler)
            return NONE;

        if (Dm.truthy(has_water_reclaimer))
        {
            to_chat(user, span_warning(I18n.T("There is already has a water recycler installed.")));
            return ITEM_INTERACT_BLOCKING;
        }

        playsound(this, Dm.resource("sound/machines/click.ogg"), 20, TRUE);
        qdel(tool);
        has_water_reclaimer = TRUE;
        begin_processing();
        await DmTask.CompletedTask;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/shower.dm:155
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.multitool_act(user, tool);
        if (Dm.truthy(dot))
            return dot;

        tool.play_tool_sound(this);
        mode = (Dm.num(mode) + 1) % SHOWER_MODE_COUNT;
        begin_processing();
        to_chat(user, span_notice(I18n.T("You change the shower's auto shut-off mode to [].", I18n.T(Dm.str(GLOB.shower_mode_descriptions[Dm.text("[]", mode)])))));
        return TRUE;
    }

    // tg: code/game/objects/structures/shower.dm:166
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        object dot = await base.crowbar_act(user, tool);
        if (Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(has_water_reclaimer))
        {
            to_chat(user, span_warning(I18n.T("There isn't a water recycler to remove.")));
            return null;
        }

        tool.play_tool_sound(this);
        has_water_reclaimer = FALSE;
        Dm.@new<obj_item_stock_parts_water_recycler>(get_turf(loc));
        to_chat(user, span_notice(I18n.T("You remove the water reclaimer from []", this)));
        return TRUE;
    }

    // tg: code/game/objects/structures/shower.dm:180
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        await base.screwdriver_act(user, tool);
        to_chat(user, span_notice(I18n.T("You begin to adjust the temperature valve with \\the []...", tool)));
        if (Dm.truthy(await tool.use_tool(this, user, 50)))
        {
            switch (current_temperature)
            {
                case SHOWER_NORMAL:
                    current_temperature = SHOWER_FREEZING;
                    break;
                case SHOWER_FREEZING:
                    current_temperature = SHOWER_BOILING;
                    break;
                case SHOWER_BOILING:
                    current_temperature = SHOWER_NORMAL;
                    break;
            }
            user.visible_message(span_notice(I18n.T("[] adjusts the shower with \\the [].", user, tool)), span_notice(I18n.T("You adjust the shower with \\the [] to [] temperature.", tool, I18n.T(current_temperature))));
            user.log_message(Dm.text("has wrenched a shower to [].", current_temperature), LOG_ATTACK);
            add_hiddenprint(user);
        }
        handle_mist();
        return TRUE;
    }

    // tg: code/game/objects/structures/shower.dm:197
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);
        tool.play_tool_sound(this);
        await deconstruct();
        return TRUE;
    }

    // tg: code/game/objects/structures/shower.dm:203
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (!Dm.truthy(actually_on))
            return dot;
        var water_falling = (mutable_appearance)mutable_appearance(Dm.resource("icons/obj/watercloset.dmi"), "water", ABOVE_MOB_LAYER, appearance_flags: KEEP_APART);
        water_falling.color = mix_color_from_reagents(((datum_reagents)reagents).reagent_list);
        float shift = Dm.num(pixel_shift);
        switch (dir)
        {
            case NORTH:
                water_falling.pixel_y += shift;
                break;
            case SOUTH:
                water_falling.pixel_y -= shift;
                break;
            case EAST:
                water_falling.pixel_x += shift;
                break;
            case WEST:
                water_falling.pixel_x -= shift;
                break;
        }
        Dm.append(ref dot, water_falling);
        return dot;
    }

    // tg: code/game/objects/structures/shower.dm:220
    public override async DmTask on_changed_z_level(turf old_turf = null, turf new_turf = null, object same_z_layer = null, float? notify_contents = null)
    {
        if (Dm.truthy(same_z_layer))
        {
            await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
            return;
        }
        update_appearance();
        await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
    }

    // tg: code/game/objects/structures/shower.dm:226
    public virtual void handle_mist()
    {
        // If there is no mist, and the shower was turned on (on a non-freezing temp): make mist in 5 seconds
        // If there was already mist, and the shower was turned off (or made cold): remove the existing mist in 25 sec
        var mist = Dm.locate<obj_effect_mist>(loc);
        if (mist == null && Dm.truthy(actually_on) && current_temperature != SHOWER_FREEZING)
            _addtimer(Dm.@new<datum_callback>(this, nameof(make_mist)), 5 * SECONDS, file: "code/game/objects/structures/shower.dm", line: 231);

        if (mist != null && !(Dm.truthy(actually_on) && current_temperature != SHOWER_FREEZING))
            _addtimer(Dm.@new<datum_callback>(this, nameof(clear_mist)), 25 * SECONDS, file: "code/game/objects/structures/shower.dm", line: 234);
    }

    // tg: code/game/objects/structures/shower.dm:236
    public virtual void make_mist()
    {
        var mist = Dm.locate<obj_effect_mist>(loc);
        if (mist == null && Dm.truthy(actually_on) && current_temperature != SHOWER_FREEZING)
        {
            var new_mist = Dm.@new<obj_effect_mist>(loc);
            new_mist.color = mix_color_from_reagents(((datum_reagents)reagents).reagent_list);
        }
    }

    // tg: code/game/objects/structures/shower.dm:242
    public virtual void clear_mist()
    {
        var mist = Dm.locate<obj_effect_mist>(loc);
        if (mist != null && !(Dm.truthy(actually_on) && current_temperature != SHOWER_FREEZING))
            qdel(mist);
    }

    // tg: code/game/objects/structures/shower.dm:247
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable enterer = null)
    {
        if (Dm.truthy(actually_on) && Dm.truthy(((datum_reagents)reagents).total_volume))
            Waiting.NoSleep(expose_to_reagents(enterer));
    }

    // tg: code/game/objects/structures/shower.dm:253
    [SignalHandler]
    public virtual void on_exited(datum source = null, atom_movable exiter = null)
    {
        if (!iscarbon(exiter))
            return;

        var locate_new_shower = Dm.locate<obj_machinery_shower>(get_turf(exiter));
        if (locate_new_shower != null && isturf(exiter.loc))
            return;
        var take_his_status_effect = (mob_living)exiter;
        take_his_status_effect.remove_status_effect(typeof(datum_status_effect_washing_regen));
    }

    // tg: code/game/objects/structures/shower.dm:265
    public virtual async DmTask expose_to_reagents(atom target = null)
    {
        float purity_volume = ((datum_reagents)reagents).total_volume * 0.70f; // need 70% of total reagents
        object bloody_shower = ((datum_reagents)reagents).has_reagent(typeof(datum_reagent_blood), amount: purity_volume);
        object clean_shower = ((datum_reagents)reagents).has_reagent(typeof(datum_reagent_water), amount: purity_volume);
        // we only care about blood and h20 for mood/status effect
        object shower_reagent = Dm.truthy(bloody_shower) ? bloody_shower : Dm.truthy(clean_shower) ? clean_shower : null;

        await ((datum_reagents)reagents).expose(target, TOUCH, SHOWER_EXPOSURE_MULTIPLIER * SHOWER_SPRAY_VOLUME / max(((datum_reagents)reagents).total_volume, SHOWER_SPRAY_VOLUME));
        if (target is not mob_living living_target)
            return;
        check_heat(living_target);

        if (iscarbon(living_target))
        {
            await living_target.apply_status_effect(typeof(datum_status_effect_washing_regen), shower_reagent);
            await living_target.add_mood_event("shower", typeof(datum_mood_event_shower), shower_reagent);
        }
    }

    /**
     * Toggle whether shower is actually on and outputting water.
     * May not match what user asked to happen by clicking.
     * Returns TRUE if the state was changed.
     *
     * Arguments:
     * * new_on_state - new state
     */
    // tg: code/game/objects/structures/shower.dm:290
    public virtual bool update_actually_on(bool new_on_state)
    {
        if (new_on_state == Dm.truthy(actually_on))
            return false;

        // Check if we have enough reagents to actually turn on.
        if (new_on_state && ((datum_reagents)reagents).total_volume < SHOWER_SPRAY_VOLUME)
            return false;

        actually_on = new_on_state ? TRUE : FALSE;

        update_appearance();
        handle_mist();
        if (new_on_state)
        {
            begin_processing();
            soundloop.start();
            timed_cooldown = Dm.world.time + SHOWER_TIMED_LENGTH; // COOLDOWN_START, inline
        }
        else
        {
            soundloop.stop();
            if (isopenturf(loc))
            {
                var tile = (turf_open)loc;
                Waiting.NoSleep(tile.MakeSlippery(TURF_WET_WATER, min_wet_time: 5 * SECONDS, wet_time_to_add: 1 * SECONDS));
            }
            foreach (mob_living_carbon showerer in Dm.each<mob_living_carbon>(loc))
                showerer.remove_status_effect(typeof(datum_status_effect_washing_regen));
        }
        return true;
    }

    // tg: code/game/objects/structures/shower.dm:315
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        // the TIMED mode cutoff feature. User has to manually reactivate.
        if (Dm.truthy(intended_on) && Dm.num(mode) == SHOWER_MODE_TIMED && timed_cooldown <= Dm.world.time) // COOLDOWN_FINISHED, inline
        {
            // the TIMED mode cutoff feature. User has to manually reactivate.
            intended_on = FALSE;
        }

        // Out of water.
        if (Dm.truthy(actually_on) && ((datum_reagents)reagents).total_volume < SHOWER_SPRAY_VOLUME)
        {
            update_actually_on(false);

            // Don't turn back on.
            if (Dm.num(mode) != SHOWER_MODE_FOREVER)
                intended_on = FALSE;
        }
        else
        {
            // Cycle: update_actually_on() will only change state if appropriate.
            update_actually_on(Dm.truthy(intended_on));
        }

        // Reclaim water
        if (!Dm.truthy(actually_on))
        {
            if (Dm.truthy(has_water_reclaimer) && ((datum_reagents)reagents).total_volume < ((datum_reagents)reagents).maximum_volume)
            {
                Waiting.NoSleep(((datum_reagents)reagents).add_reagent(reagent_id, refill_rate * Dm.num(seconds_per_tick)));
                return 0;
            }

            // FOREVER mode stays processing so it can cycle back on.
            return Dm.num(mode) == SHOWER_MODE_FOREVER ? 0 : PROCESS_KILL;
        }
        // Assemble cleaning flags
        float purity_volume = ((datum_reagents)reagents).total_volume * 0.70f; // need 70% of total reagents
        object clean_shower = ((datum_reagents)reagents).has_reagent(typeof(datum_reagent_water), amount: purity_volume);

        // radiation my beloved
        float rad_purity_volume = ((datum_reagents)reagents).total_volume * 0.20f; // need 20% of total reagents
        float radium_volume = Dm.num(((datum_reagents)reagents).get_reagent_amount(typeof(datum_reagent_uranium_radium)));
        float uranium_volume = Dm.num(((datum_reagents)reagents).get_reagent_amount(typeof(datum_reagent_uranium)));
        float polonium_volume = Dm.num(((datum_reagents)reagents).get_reagent_amount(typeof(datum_reagent_toxin_polonium))) * 3; // highly radioactive
        float total_radiation_volume = radium_volume + uranium_volume + polonium_volume;
        bool radioactive_shower = total_radiation_volume >= rad_purity_volume;

        float wash_flags = NONE;
        if (Dm.truthy(clean_shower))
            wash_flags = Dm.bitor(wash_flags, CLEAN_WASH);
        if (!radioactive_shower)
        {
            // note it is possible to have a clean_shower that is radioactive (+70% water mixed with +20% radiation)
            wash_flags = Dm.bitor(wash_flags, CLEAN_RAD);
        }

        // Wash up.
        Waiting.NoSleep(loc.wash(wash_flags, TRUE));
        Waiting.NoSleep(expose_to_reagents(loc));
        foreach (atom_movable movable_content in Dm.each<atom_movable>(loc.contents.Copy()))
            Waiting.NoSleep(expose_to_reagents(movable_content)); // Wash the items on the turf (=expose them to the shower reagent)
        Waiting.NoSleep(((datum_reagents)reagents).remove_all(SHOWER_SPRAY_VOLUME));
        return null;
    }

    // tg: code/game/objects/structures/shower.dm:366
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        Dm.@new<obj_item_stack_sheet_iron>(drop_location(), 2);
        if (Dm.truthy(has_water_reclaimer))
            Dm.@new<obj_item_stock_parts_water_recycler>(drop_location());
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/game/objects/structures/shower.dm:371
    public virtual void check_heat(mob_living living = null)
    {
        if (current_temperature == SHOWER_FREEZING)
        {
            living.adjust_bodytemperature(-80, 80);
            to_chat(living, span_warning(I18n.T("[] is freezing!", this)));
        }
        else if (current_temperature == SHOWER_BOILING)
        {
            living.adjust_bodytemperature(35, 0, 500);
            Waiting.NoSleep(living.adjust_fire_loss(5));
            to_chat(living, span_danger(I18n.T("[] is searing!", this)));
        }
    }
}

// tg: code/game/objects/structures/shower.dm:382
public partial class obj_structure_showerframe : obj_structure
{
    // tg: code/game/objects/structures/shower.dm:390
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation)));
        return dot;
    }

    // tg: code/game/objects/structures/shower.dm:394
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_stock_parts_water_recycler)
            return NONE;

        qdel(tool);
        var shower = Dm.@new<obj_machinery_shower>(loc, REVERSE_DIR(dir), TRUE);
        qdel(this);
        playsound(shower, Dm.resource("sound/machines/click.ogg"), 20, TRUE);
        await DmTask.CompletedTask;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/shower.dm:404
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        await base.wrench_act(user, tool);

        tool.play_tool_sound(this);
        Dm.@new<obj_machinery_shower>(loc, REVERSE_DIR(dir), FALSE);
        qdel(this);

        return TRUE;
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/structures/shower.dm:91  var/static/list/loc_connections
    internal DmList obj_machinery_shower__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(obj_machinery_shower.on_entered)),
        Dm.kv(COMSIG_ATOM_EXITED, nameof(obj_machinery_shower.on_exited)));
}
