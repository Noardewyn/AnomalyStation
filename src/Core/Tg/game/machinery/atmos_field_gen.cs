// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/atmos_field_gen.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering package E10 (D287): the whole file. The atmospheric shield generator (Meta maps six, in pairs on a mass driver's
// doorway): powered from its area's environment channel it finds a generator facing it within its range (its micro laser's tier
// + 2 tiles, its own tile first) and fills the line between them with holofields that block gas and let people and objects
// through; it waits for power and comes back after a power loss, locks with an engineering or external airlock ID (the right
// click or the card), rotates, unwrenches and deconstructs. Its context tips are a live-partial stub (not now, by the
// owner: D264); its AI and cyborg clicks since silicons SI2. The var blocks are generated (D111).

// tg: code/game/machinery/atmos_field_gen.dm:8
public partial class obj_machinery_atmos_shield_gen : obj_machinery
{
    /// off
    // tg: code/game/machinery/atmos_field_gen.dm:2  #define GENERATOR_INACTIVE 0 (file-local, #undef at the end)
    private const float GENERATOR_INACTIVE = 0;

    /// waiting for power/retrying, will activate and do things if powered
    // tg: code/game/machinery/atmos_field_gen.dm:4  #define GENERATOR_WANTPOWER 1 (file-local, #undef at the end)
    private const float GENERATOR_WANTPOWER = 1;

    /// active
    // tg: code/game/machinery/atmos_field_gen.dm:6  #define GENERATOR_ACTIVE 2 (file-local, #undef at the end)
    private const float GENERATOR_ACTIVE = 2;

    // tg: code/game/machinery/atmos_field_gen.dm:39
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_context();
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation)));
        set_wires(Dm.@new<datum_wires_atmosshieldgen>(this));
        // rules 10.6: tg's Master creates SSmachines before the map; a test harness world may lack it.
        if (SSmachines != null)
            Dm.append(ref SSmachines.processing_early, this);
        if (Dm.truthy(on))
            return INITIALIZE_HINT_LATELOAD;
        return dot;
    }

    // tg: code/game/machinery/atmos_field_gen.dm:48
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        process_early(); // not risking processing falling behind and letting gas out
    }

    // tg: code/game/machinery/atmos_field_gen.dm:52
    public override object Destroy(float? force = null)
    {
        QDEL_LIST(fields);
        master?.turn_off();
        master = null;
        return base.Destroy(force);
    }

    // tg: code/game/machinery/atmos_field_gen.dm:78
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (!in_range(user, this) && !isobserver(user))
            return dot;

        Dm.append(ref dot, span_notice(I18n.T("The status display reads:")));
        Dm.append(ref dot, span_notice(I18n.T("Currently []active.", Dm.truthy(on) ? "" : I18n.T("in"))));
        if (Dm.truthy(locked))
        {
            Dm.append(ref dot, span_boldwarning(I18n.T("LOCKED")));
            return dot;
        }
        Dm.append(ref dot, span_notice(I18n.T("Maximum field length: [] tiles.", max_range)));
        Dm.append(ref dot, span_notice(I18n.T("Its maintenance panel can be [] [].", EXAMINE_HINT(I18n.T("screwed")), Dm.truthy(panel_open) ? I18n.T("close") : I18n.T("open"))));
        if (Dm.truthy(panel_open))
            Dm.append(ref dot, span_notice(I18n.T("It can be [] apart.", EXAMINE_HINT(I18n.T("pried")))));
        return dot;
    }

    // tg: code/game/machinery/atmos_field_gen.dm:93
    public override async DmTask<object> RefreshParts()
    {
        object dot = await base.RefreshParts();
        datum_stock_part_capacitor capacitor = Dm.locate<datum_stock_part_capacitor>(component_parts);
        active_power_usage = Dm.num(Dm.initial(this, (obj_machinery m) => m.active_power_usage)) / Dm.num(capacitor.tier); // 0.25kw per tile at tier 4
        datum_stock_part_micro_laser laser = Dm.locate<datum_stock_part_micro_laser>(component_parts);
        max_range = Dm.num(laser.tier) + 2;
        return dot;
    }

    // tg: code/game/machinery/atmos_field_gen.dm:100
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)))
            return dot;
        if (!Dm.truthy(on))
            return dot;
        Dm.append(ref dot, on == GENERATOR_ACTIVE ? "active" : "wantpower");
        // DEVIATION(DV12): . += emissive_appearance(icon, on == GENERATOR_ACTIVE ? "active" : "wantpower", src, alpha = src.alpha), the glow on the
        // emissive plane, is not created
        return dot;
    }

    // tg: code/game/machinery/atmos_field_gen.dm:109
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(panel_open) && Dm.truthy(locked))
        {
            balloon_alert(user, I18n.T("locked!"));
            return ITEM_INTERACT_FAILURE;
        }
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/game/machinery/atmos_field_gen.dm:115
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(on))
        {
            balloon_alert(user, I18n.T("turn off first!"));
            return ITEM_INTERACT_FAILURE;
        }
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/game/machinery/atmos_field_gen.dm:121
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(on))
        {
            balloon_alert(user, I18n.T("turn off first!"));
            return ITEM_INTERACT_FAILURE;
        }
        if (Dm.truthy(locked))
        {
            balloon_alert(user, I18n.T("unlock first!"));
            return ITEM_INTERACT_FAILURE;
        }
        if (Dm.truthy(await default_unfasten_wrench(user, tool)) && !Dm.truthy(anchored))
            turn_off();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/atmos_field_gen.dm:132
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (!Dm.truthy(anchored))
        {
            balloon_alert(user, I18n.T("not anchored!"));
            return dot;
        }
        if (Dm.truthy(locked) && !issilicon(user))
        {
            balloon_alert(user, I18n.T("locked!"));
            return dot;
        }
        toggle(user);
        return dot;
    }

    // tg: code/game/machinery/atmos_field_gen.dm:142
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_hand_secondary(user, modifiers);
        if (Dm.eq(dot, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN))
            return dot;
        if (Dm.truthy(await allowed__proc(user)))
        {
            locked = Dm.truthy(locked) ? FALSE : TRUE;
            balloon_alert(user, I18n.T("[]locked!", Dm.truthy(locked) ? "" : "un"));
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }
        balloon_alert(user, I18n.T("no access!"));
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/machinery/atmos_field_gen.dm:153
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = NONE;
        add_fingerprint(user);
        if (Dm.truthy(is_wire_tool(tool)) && Dm.truthy(panel_open))
        {
            await wires.interact(user);
            return ITEM_INTERACT_SUCCESS;
        }
        if (tool is obj_item_card_id && Dm.truthy(check_access(tool)))
        {
            locked = Dm.truthy(locked) ? FALSE : TRUE;
            balloon_alert(user, I18n.T("[]locked!", Dm.truthy(locked) ? "" : "un"));
            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }

    // tg: code/game/machinery/atmos_field_gen.dm:164
    public override object process_early(params object[] __extra)
    {
        if (on == GENERATOR_ACTIVE && !Dm.truthy(powered__proc()))
            turn_off(power_failure: TRUE);
        else if (on == GENERATOR_WANTPOWER && Dm.truthy(powered__proc()))
        {
            on = GENERATOR_ACTIVE;
            update_appearance(UPDATE_OVERLAYS);
        }

        if (on != GENERATOR_ACTIVE)
        {
            if (Dm.truthy(length(fields)))
                QDEL_LIST(fields);
            return null;
        }

        if (master != null)
        {
            if (master.on != GENERATOR_ACTIVE)
                turn_off();
            return null;
        }
        float field_len = length(fields);
        if (field_len != 0)
        {
            Waiting.NoSleep(use_energy(active_power_usage * field_len));
            return null;
        }
        turf current_turf = get_turf(this);
        obj_machinery_atmos_shield_gen found_slave = null;
        for (float i = 1, i__end = Dm.num(max_range); i <= i__end; i++)
        {
            if (isclosedturf(current_turf)) // we were blocked by a closed turf
                return null;
            if (found_slave == null)
            {
                foreach (obj_machinery_atmos_shield_gen generator in Dm.each<obj_machinery_atmos_shield_gen>(current_turf))
                {
                    if (generator == this || !Dm.truthy(generator.powered__proc()) || generator.dir != REVERSE_DIR(dir) || generator.master != null || !Dm.truthy(generator.anchored))
                        continue;
                    found_slave = generator;
                    generator.change_master(this);
                    break;
                }
                if (found_slave != null)
                    break;
            }
            current_turf = get_step(current_turf, dir); // advance
        }

        if (found_slave == null)
        {
            on = GENERATOR_WANTPOWER;
            update_appearance(UPDATE_OVERLAYS);
            return null;
        }

        DmList line = get_line(this, found_slave) as DmList;
        foreach (turf line_turf in Dm.each<turf>(line))
            Dm.append(ref fields, Dm.@new<obj_effect_atmos_shield>(line_turf, this));
        return null;
    }

    // tg: code/game/machinery/atmos_field_gen.dm:209
    public virtual void toggle(mob user = null)
    {
        if (Dm.truthy(on))
            turn_off();
        else
        {
            on = GENERATOR_WANTPOWER;
            update_appearance(UPDATE_OVERLAYS);
        }
        if (user != null)
            balloon_alert(user, I18n.T("turned []", Dm.truthy(on) ? "on" : "off"));
    }

    // tg: code/game/machinery/atmos_field_gen.dm:218
    public virtual void turn_off(float? power_failure = null)
    {
        power_failure ??= FALSE;
        if (!Dm.truthy(on))
            return;
        if (Dm.truthy(power_failure))
        {
            balloon_alert_to_viewers(I18n.T("no power!"));
            playsound(this, Dm.resource("sound/machines/cryo_warning.ogg"), 65);
        }
        on = Dm.truthy(power_failure) ? GENERATOR_WANTPOWER : GENERATOR_INACTIVE;
        master?.turn_off(power_failure);
        change_master(null);
        if (!Dm.truthy(power_failure)) // so that it will get cleaned up on the next process so they can react
            QDEL_LIST(fields);
        update_appearance(UPDATE_OVERLAYS);
    }

    /// Changes our master variable and (un)registers signals. Does not check whether active and stuff. Will activate generator if new_master is not null
    // tg: code/game/machinery/atmos_field_gen.dm:232
    public virtual void change_master(object new_master = null)
    {
        if (Dm.eq(master, new_master))
            return;
        if (master != null && !Dm.eq(master, new_master))
            UnregisterSignal(master, COMSIG_QDELETING);
        if (new_master == null)
        {
            master = null;
            on = GENERATOR_INACTIVE;
            update_appearance(UPDATE_OVERLAYS);
            return;
        }
        master = (obj_machinery_atmos_shield_gen)new_master;
        RegisterSignal(master, COMSIG_QDELETING, nameof(master_deleted));
        on = GENERATOR_ACTIVE;
        update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/game/machinery/atmos_field_gen.dm:247
    [SignalHandler]
    public virtual void master_deleted(datum source = null)
    {
        turn_off();
    }
}

// tg: code/game/machinery/atmos_field_gen.dm:268
public partial class obj_effect_atmos_shield : obj_effect
{
    // tg: code/game/machinery/atmos_field_gen.dm:289
    [DmParams("mapload", "owner")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_machinery_atmos_shield_gen owner = Dm.arg<obj_machinery_atmos_shield_gen>(__extra, 0);
        object dot = base.Initialize(mapload, Dm.forward(__extra, owner));
        this.owner = owner;
        QUEUE_SMOOTH(this);
        QUEUE_SMOOTH_NEIGHBORS(this);
        update_appearance(UPDATE_OVERLAYS);
        air_update_turf(TRUE, TRUE);
        RegisterSignal(loc as datum, COMSIG_TURF_CHANGE, nameof(turf_changed)); // Wont ever be spawned inside something hopefully
        Waiting.NoSleep(AddElement(typeof(datum_element_give_turf_traits), string_list(Dm.list(TRAIT_FIREDOOR_STOP))));
        return dot;
    }

    // tg: code/game/machinery/atmos_field_gen.dm:299
    public override object block_superconductivity()
    {
        return TRUE;
    }

    // tg: code/game/machinery/atmos_field_gen.dm:302
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        // DEVIATION(DV12): . += emissive_appearance(icon, icon_state, src, alpha = src.alpha), the glow on the emissive plane, is not created
        return dot;
    }

    // tg: code/game/machinery/atmos_field_gen.dm:306
    public override object Destroy(float? force = null)
    {
        owner = null;
        return base.Destroy(force);
    }

    // tg: code/game/machinery/atmos_field_gen.dm:310
    public override async DmTask atom_destruction(object damage_flag = null)
    {
        owner?.turn_off();
        await base.atom_destruction(damage_flag);
    }

    // tg: code/game/machinery/atmos_field_gen.dm:314
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        owner?.turn_off();
        qdel(this);
        return null;
    }

    // tg: code/game/machinery/atmos_field_gen.dm:318
    public override async DmTask<object> singularity_act()
    {
        owner?.turn_off();
        qdel(this);
        return null;
    }

    // tg: code/game/machinery/atmos_field_gen.dm:322
    [SignalHandler]
    public virtual void turf_changed(datum source = null, object path = null, object new_baseturfs = null, object flags = null, object post_change_callback = null)
    {
        if (ispath(path, typeof(turf_closed)))
            Waiting.NoSleep(atom_destruction(ENERGY));
    }
}

// The AI's and cyborgs' clicks (silicons SI2, D326)
// tg: code/game/machinery/atmos_field_gen.dm:251
public partial class obj_machinery_atmos_shield_gen : obj_machinery
{
    // tg: code/game/machinery/atmos_field_gen.dm:251
    public override async DmTask<object> attack_ai(mob user = null)
    {
        return await attack_hand(user);
    }

    // tg: code/game/machinery/atmos_field_gen.dm:254
    public override async DmTask<object> attack_ai_secondary(mob user = null, DmList modifiers = null)
    {
        return await attack_hand_secondary(user);
    }

    // tg: code/game/machinery/atmos_field_gen.dm:257
    public override async DmTask<object> attack_robot(mob user = null, object modifiers = null)
    {
        return await attack_hand(user);
    }

    // tg: code/game/machinery/atmos_field_gen.dm:260
    public override async DmTask<object> attack_robot_secondary(mob user = null, DmList modifiers = null)
    {
        return await attack_hand_secondary(user);
    }
}
