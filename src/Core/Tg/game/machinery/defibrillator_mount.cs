// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/defibrillator_mount.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice E: the whole file. The wall mount holds a defibrillator whose paddles are taken from the wall with an empty
// hand; the PENLITE mount charges the defib's cell from its area's equipment channel; the mobile mount rolls. The wall
// frames and the var blocks are generated. The red alert's clamps for anyone read the security level since gaps R4.

//Holds defibs does NOT recharge them
//You can activate the mount with an empty hand to grab the paddles
//Not being adjacent will cause the paddles to snap back
// tg: code/game/machinery/defibrillator_mount.dm:4
public partial class obj_machinery_defibrillator_mount : obj_machinery
{
    // tg: code/game/machinery/defibrillator_mount.dm:24
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(mapload))
            Waiting.NoSleep(find_and_mount_on_atom());
        return dot;
    }

    // tg: code/game/machinery/defibrillator_mount.dm:33
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref defib);
        return base.Destroy(force);
    }

    // tg: code/game/machinery/defibrillator_mount.dm:37
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == defib)
        {
            // Make sure processing ends before the defib is nulled
            end_processing();
            defib = null;
            update_appearance();
        }
        return dot;
    }

    // tg: code/game/machinery/defibrillator_mount.dm:45
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (defib != null)
        {
            Dm.append(ref dot, span_notice(I18n.T("There is a defib unit hooked up. Alt-click to remove it.")));
            if (Dm.num(SSsecurity_level.get_current_level_as_number()) >= SEC_LEVEL_RED)
                Dm.append(ref dot, span_notice(I18n.T("Due to a security situation, its locking clamps can be toggled by swiping any ID.")));
            else
                Dm.append(ref dot, span_notice(I18n.T("Its locking clamps can be []engaged by swiping an ID with access.", Dm.truthy(clamps_locked) ? "dis" : "")));
        }
        return dot;
    }

    // tg: code/game/machinery/defibrillator_mount.dm:54
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (defib == null)
            return dot;

        mutable_appearance defib_overlay = mutable_appearance(icon, "defib", layer: layer + 0.01f, offset_spokesman: this) as mutable_appearance;

        if (Dm.truthy(defib.powered))
        {
            obj_item_stock_parts_power_store cell = defib.cell;
            mutable_appearance safety = mutable_appearance(icon, Dm.truthy(defib.safety) ? "online" : "emagged", offset_spokesman: this) as mutable_appearance;
            mutable_appearance charge_overlay = mutable_appearance(icon, Dm.text("charge[]", CEILING((cell.charge / cell.maxcharge) * 4, 1) * 25), offset_spokesman: this) as mutable_appearance;

            Dm.combine(ref defib_overlay.overlays, Dm.list(safety, charge_overlay));
        }

        if (Dm.truthy(clamps_locked))
        {
            mutable_appearance clamps = mutable_appearance(icon, "clamps", offset_spokesman: this) as mutable_appearance;
            Dm.append(ref defib_overlay.overlays, clamps);
        }

        Dm.append(ref dot, defib_overlay);
        return dot;
    }

    //defib interaction
    // tg: code/game/machinery/defibrillator_mount.dm:75
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (defib == null)
        {
            to_chat(user, span_warning(I18n.T("There's no defibrillator unit loaded!")));
            return dot;
        }
        if (defib.paddles.loc != defib)
        {
            to_chat(user, span_warning(I18n.T("[] holding []'s paddles!", defib.paddles.loc == user ? "You are already" : "Someone else is", defib)));
            return dot;
        }
        if (!in_range(this, user))
        {
            to_chat(user, span_warning(I18n.T("[]'s paddles overextend and come out of your hands!", defib)));
            return dot;
        }
        await user.put_in_hands(defib.paddles);
        return dot;
    }

    // tg: code/game/machinery/defibrillator_mount.dm:88
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (istype(tool, typeof(obj_item_defibrillator)))
        {
            if (defib != null)
            {
                to_chat(user, span_warning(I18n.T("There's already a defibrillator in []!", this)));
                return ITEM_INTERACT_BLOCKING;
            }
            obj_item_defibrillator new_defib = tool as obj_item_defibrillator;
            if (!Dm.truthy(new_defib.get_cell()))
            {
                to_chat(user, span_warning(I18n.T("Only defibrilators containing a cell can be hooked up to []!", this)));
                return ITEM_INTERACT_BLOCKING;
            }
            if (HAS_TRAIT(new_defib, TRAIT_NODROP) || !Dm.truthy(await user.transferItemToLoc(new_defib, this)))
            {
                to_chat(user, span_warning(I18n.T("[] is stuck to your hand!", new_defib)));
                return ITEM_INTERACT_BLOCKING;
            }
            user.visible_message(span_notice(I18n.T("[] hooks up [] to []!", user, new_defib, this)),
                span_notice(I18n.T("You press [] into the mount, and it clicks into place.", new_defib)));
            playsound(this, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
            // Make sure the defib is set before processing begins.
            defib = new_defib;
            begin_processing();
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (defib != null && tool == defib.paddles)
        {
            defib.paddles.snap_back();
            return ITEM_INTERACT_SUCCESS;
        }

        if (!Dm.truthy(tool.GetID()))
            return NONE;

        if ((!Dm.truthy(await allowed__proc(user)) && Dm.num(SSsecurity_level.get_current_level_as_number()) < SEC_LEVEL_RED)) //anyone can toggle the clamps in red alert!
        {
            to_chat(user, span_warning(I18n.T("Insufficient access.")));
            return ITEM_INTERACT_BLOCKING;
        }

        if (defib == null)
        {
            to_chat(user, span_warning(I18n.T("You can't engage the clamps on a defibrillator that isn't there.")));
            return ITEM_INTERACT_BLOCKING;
        }

        clamps_locked = Dm.truthy(clamps_locked) ? FALSE : TRUE;
        to_chat(user, span_notice(I18n.T("Clamps []engaged.", Dm.truthy(clamps_locked) ? "" : "dis")));
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/defibrillator_mount.dm:129
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        await base.multitool_act(user, tool);
        if (defib == null)
        {
            to_chat(user, span_warning(I18n.T("There isn't any defibrillator to clamp in!")));
            return TRUE;
        }
        if (!Dm.truthy(clamps_locked))
        {
            to_chat(user, span_warning(I18n.T("[]'s clamps are disengaged!", this)));
            return TRUE;
        }
        user.visible_message(span_notice(I18n.T("[] presses [] into []'s ID slot...", user, tool, this)),
            span_notice(I18n.T("You begin overriding the clamps on []...", this)));
        playsound(this, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
        if (!Dm.truthy(await do_after(user, 10 * SECONDS, target: this)) || !Dm.truthy(clamps_locked))
            return null;
        user.visible_message(span_notice(I18n.T("[] pulses [], and []'s clamps slide up.", user, tool, this)),
            span_notice(I18n.T("You override the locking clamps on []!", this)));
        playsound(this, Dm.resource("sound/machines/locktoggle.ogg"), 50, TRUE);
        clamps_locked = FALSE;
        update_appearance();
        return TRUE;
    }

    // tg: code/game/machinery/defibrillator_mount.dm:149
    public override async DmTask<object> wrench_act_secondary(mob_living user = null, obj_item tool = null)
    {
        if (wallframe_type == null)
            return await base.wrench_act_secondary(user, tool);
        if (Dm.truthy(user.combat_mode))
            return await base.wrench_act_secondary(user, tool);
        if (defib != null)
        {
            to_chat(user, span_warning(I18n.T("The mount can't be deconstructed while a defibrillator unit is loaded!")));
            await base.wrench_act_secondary(user, tool);
            return TRUE;
        }
        Dm.@new(wallframe_type, get_turf(this));
        qdel(this);
        tool.play_tool_sound(user);
        to_chat(user, span_notice(I18n.T("You remove [] from the wall.", this)));
        return TRUE;
    }

    // tg: code/game/machinery/defibrillator_mount.dm:164
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (defib == null)
        {
            to_chat(user, span_warning(I18n.T("It'd be hard to remove a defib unit from a mount that has none.")));
            return CLICK_ACTION_BLOCKING;
        }
        if (Dm.truthy(clamps_locked))
        {
            to_chat(user, span_warning(I18n.T("You try to tug out [], but the mount's clamps are locked tight!", defib)));
            return CLICK_ACTION_BLOCKING;
        }
        if (!Dm.truthy(await user.put_in_hands(defib)))
        {
            to_chat(user, span_warning(I18n.T("You need a free hand!")));
            user.visible_message(span_notice(I18n.T("[] unhooks [] from [], dropping it on the floor.", user, defib, this)),
                span_notice(I18n.T("You slide out [] from [] and unhook the charging cables, dropping it on the floor.", defib, this)));
        }
        else
        {
            user.visible_message(span_notice(I18n.T("[] unhooks [] from [].", user, defib, this)),
                span_notice(I18n.T("You slide out [] from [] and unhook the charging cables.", defib, this)));
        }
        playsound(this, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
        return CLICK_ACTION_SUCCESS;
    }
}

// tg: code/game/machinery/defibrillator_mount.dm:29
public partial class obj_machinery_defibrillator_mount_loaded : obj_machinery_defibrillator_mount
{
    // tg: code/game/machinery/defibrillator_mount.dm:29
    public override object Initialize(object mapload = null, params object[] __extra) //loaded subtype for mapping use
    {
        object dot = base.Initialize(mapload, __extra);
        defib = Dm.@new<obj_item_defibrillator_loaded>(this);
        return dot;
    }
}

// tg: code/game/machinery/defibrillator_mount.dm:181
public partial class obj_machinery_defibrillator_mount_charging : obj_machinery_defibrillator_mount
{
    // tg: code/game/machinery/defibrillator_mount.dm:189
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(is_operational))
            begin_processing();
        return dot;
    }

    // tg: code/game/machinery/defibrillator_mount.dm:195
    public override async DmTask on_set_is_operational(object old_value = null)
    {
        if (Dm.truthy(old_value)) //Turned off
            end_processing();
        else //Turned on
            begin_processing();
    }

    // tg: code/game/machinery/defibrillator_mount.dm:202
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (defib == null)
            return null;
        obj_item_stock_parts_power_store defib_cell = defib.get_cell() as obj_item_stock_parts_power_store;
        if (defib_cell == null) // Something is very wrong if we hit this, so we should stack trace
        {
            stack_trace(Dm.text("[] was set to process with no cell inside its defib", this), "code/game/machinery/defibrillator_mount.dm", 207);
            return PROCESS_KILL;
        }
        if (defib_cell.charge < defib_cell.maxcharge)
        {
            Waiting.NoSleep(charge_cell__proc(active_power_usage * Dm.num(seconds_per_tick), defib_cell));
            defib.update_power();
        }
        return null;
    }
}

//mobile defib

// tg: code/game/machinery/defibrillator_mount.dm:233
public partial class obj_machinery_defibrillator_mount_mobile : obj_machinery_defibrillator_mount
{
    // tg: code/game/machinery/defibrillator_mount.dm:240
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_noisy_movement)));
        return dot;
    }

    // tg: code/game/machinery/defibrillator_mount.dm:244
    public override async DmTask<object> find_and_mount_on_atom(float? mark_for_late_init = null, float? late_init = null)
    {
        return null; //its mobile
    }

    // tg: code/game/machinery/defibrillator_mount.dm:247
    public override async DmTask<object> wrench_act_secondary(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(user.combat_mode))
            return await base.wrench_act_secondary(user, tool);
        if (defib != null)
        {
            to_chat(user, span_warning(I18n.T("The mount can't be deconstructed while a defibrillator unit is loaded!")));
            await base.wrench_act_secondary(user, tool);
            return TRUE;
        }
        balloon_alert(user, I18n.T("deconstructing..."));
        tool.play_tool_sound(this);
        if (Dm.truthy(await tool.use_tool(this, user, 5 * SECONDS)))
        {
            playsound(loc, Dm.resource("sound/items/deconstruct.ogg"), 50, vary: TRUE);
            await deconstruct();
        }
        return TRUE;
    }

    // tg: code/game/machinery/defibrillator_mount.dm:261
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        atom drop = drop_location() as atom;
        if (Dm.truthy(disassembled))
        {
            Dm.@new<obj_item_stack_sheet_iron>(drop, 5);
            Dm.@new<obj_item_stack_sheet_mineral_silver>(drop);
            Dm.@new<obj_item_stack_cable_coil>(drop, 15);
        }
        else
        {
            Dm.@new<obj_item_stack_sheet_iron>(drop, 5);
        }
        return null;
    }
}
