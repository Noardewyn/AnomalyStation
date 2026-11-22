// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/flasher.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate brig timers, D316: the brig cells' mounted flashers, the portable flasher and the flasher frame). The var
// blocks are generated (D111), except the frame's id below.

// It is a gizmo that flashes a small area

// tg: code/game/machinery/flasher.dm:3
public partial class obj_machinery_flasher : obj_machinery
{
    // tg: code/game/machinery/flasher.dm:27
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(mapload))
        {
            bulb = Dm.@new<obj_item_assembly_flash_handheld>(this);
            Waiting.NoSleep(find_and_mount_on_atom());
        }
        return dot;
    }

    // tg: code/game/machinery/flasher.dm:33
    public override async DmTask<object> vv_edit_var(object var_name = null, object var_value = null)
    {
        object dot = await base.vv_edit_var(var_name, var_value);
        // COOLDOWN_TIMELEFT(src, flash_cooldown) (__DEFINES/cooldowns.dm:121), inline (rules 11.1 (a))
        if (Dm.eq(var_name, nameof(flash_cooldown_duration)) && max(0, flash_cooldown - world.time) > flash_cooldown_duration)
            flash_cooldown = world.time + flash_cooldown_duration; // COOLDOWN_START(src, flash_cooldown, flash_cooldown_duration)
        return dot;
    }

    // tg: code/game/machinery/flasher.dm:38
    public override async DmTask<object> connect_to_shuttle(object mapload = null, obj_docking_port_mobile port = null, obj_docking_port_stationary dock = null)
    {
        id = Dm.text("[]_[]", port.shuttle_id, id);
        return null;
    }

    // tg: code/game/machinery/flasher.dm:41
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        if (arrived is obj_item_assembly_flash_handheld flash)
            bulb = flash;
        return await base.Entered(arrived, old_loc, old_locs);
    }

    // tg: code/game/machinery/flasher.dm:46
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        if (gone == bulb)
            bulb = null;
        return await base.Exited(gone, direction);
    }

    // tg: code/game/machinery/flasher.dm:51
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref bulb);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/flasher.dm:55
    public override object powered__proc(object chan = null, float? ignore_use_power = null)
    {
        if (!Dm.truthy(anchored) || bulb == null)
            return FALSE;
        return base.powered__proc(chan, ignore_use_power);
    }

    // tg: code/game/machinery/flasher.dm:60
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[]1[]", base_icon_state, (Dm.truthy(bulb?.burnt_out) || !Dm.truthy(powered__proc())) ? "-p" : null);
        return await base.update_icon_state(__extra);
    }

    //Don't want to render prison breaks impossible
    // tg: code/game/machinery/flasher.dm:65
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        add_fingerprint(user);

        if (tool is not obj_item_assembly_flash_handheld)
            return NONE;
        if (bulb != null)
        {
            to_chat(user, span_warning(I18n.T("A flashbulb is already installed in []!", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            return ITEM_INTERACT_BLOCKING;
        user.visible_message(span_notice(I18n.T("[] installs [] into [].", user, tool, this)),
            span_notice(I18n.T("You install [] into [].", tool, this)));
        power_change();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/flasher.dm:81
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        add_fingerprint(user);
        if (bulb == null)
            return NONE;
        user.visible_message(span_notice(I18n.T("[] begins to disconnect []'s flashbulb.", user, this)), span_notice(I18n.T("You begin to disconnect []'s flashbulb...", this)));
        if (!Dm.truthy(await tool.use_tool(this, user, 30, volume: 50)) || bulb == null)
            return ITEM_INTERACT_BLOCKING;
        user.visible_message(span_notice(I18n.T("[] disconnects []'s flashbulb!", user, this)), span_notice(I18n.T("You disconnect []'s flashbulb.", this)));
        await bulb.forceMove(loc);
        power_change();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/flasher.dm:93
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        add_fingerprint(user);
        if (bulb != null)
        {
            to_chat(user, span_warning(I18n.T("Remove a flashbulb from [] first!", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        to_chat(user, span_notice(I18n.T("You start unsecuring the flasher frame...")));
        if (!Dm.truthy(await tool.use_tool(this, user, 40, volume: 50)))
            return ITEM_INTERACT_BLOCKING;
        to_chat(user, span_notice(I18n.T("You unsecure the flasher frame.")));
        await deconstruct(TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    //Let the AI trigger them directly.
    // tg: code/game/machinery/flasher.dm:106  (silicons SI4)
    public override async DmTask<object> attack_ai(mob user = null)
    {
        if (Dm.truthy(anchored))
            return await flash();
        return null;
    }

    // tg: code/game/machinery/flasher.dm:110
    public virtual async DmTask<object> flash()
    {
        if (!Dm.truthy(powered__proc()) || bulb == null)
            return null;

        if (Dm.truthy(bulb.burnt_out) || !(flash_cooldown <= world.time)) // COOLDOWN_FINISHED(src, flash_cooldown)
            return null;

        if (!Dm.truthy(bulb.flash_recharge(30))) //Bulb can burn out if it's used too often too fast
        {
            power_change();
            return null;
        }

        playsound(this, Dm.resource("sound/items/weapons/flash.ogg"), 100, TRUE);
        flick(Dm.text("[]_flash", base_icon_state), this);
        flash_lighting_fx();

        flash_cooldown = world.time + flash_cooldown_duration; // COOLDOWN_START(src, flash_cooldown, flash_cooldown_duration)
        await use_energy(1 * KILO * JOULES);

        bool flashed = false;
        foreach (mob_living living_mob in Dm.each<mob_living>(viewers(this, null)))
        {
            if (Dm.num(get_dist(this, living_mob)) > Dm.num(flash_range))
                continue;

            if (Dm.truthy(await bulb.flash_mob(living_mob, confusion_duration: Dm.num(strength) * 1.5f, extra_log: Dm.text("by []", this))))
            {
                await living_mob.Paralyze(strength);
                flashed = true;
            }
        }

        if (flashed)
            bulb.times_used++;

        return TRUE;
    }

    // tg: code/game/machinery/flasher.dm:142
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (!Dm.truthy(Dm.bitand(machine_stat, BROKEN | NOPOWER)) && !Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
        {
            if (bulb != null && prob(75 / Dm.num(severity)))
            {
                await flash();
                bulb.burn_out();
                power_change();
            }
        }
        return dot;
    }

    // tg: code/game/machinery/flasher.dm:150
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        object dot = await base.atom_break(damage_flag);
        if (Dm.truthy(dot) && bulb != null)
        {
            bulb.burn_out();
            power_change();
        }
        return dot;
    }

    // tg: code/game/machinery/flasher.dm:156
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        if (bulb != null)
            await bulb.forceMove(loc);
        if (Dm.truthy(disassembled))
        {
            var flasher_obj = Dm.@new<obj_item_wallframe_flasher>(get_turf(this));
            transfer_fingerprints_to(flasher_obj);
            flasher_obj.id = id;
            playsound(loc, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
        }
        else
            Dm.@new<obj_item_stack_sheet_iron>(loc, 2);
        return null;
    }
}

// tg: code/game/machinery/flasher.dm:167
public partial class obj_machinery_flasher_portable : obj_machinery_flasher
{
    // tg: code/game/machinery/flasher.dm:179
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        proximity_monitor = Dm.@new<datum_proximity_monitor>(this, 0);
        return dot;
    }

    // tg: code/game/machinery/flasher.dm:183
    public override async DmTask<object> find_and_mount_on_atom(float? mark_for_late_init = null, float? late_init = null)
    {
        return null; //its meant to be carried and mobile
    }

    // tg: code/game/machinery/flasher.dm:186
    public override async DmTask<object> HasProximity(atom_movable proximity_check_mob = null)
    {
        if (!(flash_cooldown <= world.time)) // COOLDOWN_FINISHED(src, flash_cooldown)
            return null;

        if (!isliving(proximity_check_mob))
            return null;

        var proximity_living = (mob_living)proximity_check_mob;
        if (!Dm.eq(proximity_living.move_intent, MOVE_INTENT_WALK) && Dm.truthy(anchored))
            await flash();
        return null;
    }

    // tg: code/game/machinery/flasher.dm:197
    public override async DmTask<object> vv_edit_var(object var_name = null, object var_value = null)
    {
        object dot = await base.vv_edit_var(var_name, var_value);
        if (Dm.eq(var_name, nameof(flash_range)) && proximity_monitor != null)
            await proximity_monitor.set_range(flash_range);
        return dot;
    }

    // tg: code/game/machinery/flasher.dm:202
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        tool.play_tool_sound(this, 100);
        if (!Dm.truthy(anchored) && !Dm.truthy(isinspace()))
        {
            to_chat(user, span_notice(I18n.T("[] is now secured.", this)));
            add_overlay(Dm.text("[]-s", base_icon_state));
            await set_anchored(TRUE);
            power_change();
            await proximity_monitor.set_range(flash_range);
            return ITEM_INTERACT_SUCCESS;
        }

        to_chat(user, span_notice(I18n.T("[] can now be moved.", this)));
        cut_overlays();
        await set_anchored(FALSE);
        power_change();
        await proximity_monitor.set_range(0);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/game/machinery/flasher.dm:219
public partial class obj_item_wallframe_flasher : obj_item_wallframe
{
    // tg: code/game/machinery/flasher.dm:225  GAP(types): the generator types it by name, and other ids are paths, but this one is the
    // cell's id the flasher takes over (on_deconstruction, after_attach), so the var is hand-written
    public object id;

    // tg: code/game/machinery/flasher.dm:228
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(dot, span_notice(I18n.T("Its channel ID is '[]'.", id)));
        return dot;
    }

    // tg: code/game/machinery/flasher.dm:232
    public override void after_attach(obj attached_to = null)
    {
        base.after_attach(attached_to);
        var flasher_obj = (obj_machinery_flasher)attached_to;
        flasher_obj.id = id;
    }
}
