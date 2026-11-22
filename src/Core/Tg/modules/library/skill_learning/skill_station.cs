// Translated from tgstation (snapshot 1d99bd7f): code/modules/library/skill_learning/skill_station.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SL1 (D333): the whole file. The Skillsoft station (Meta's library): a click opens or shuts it, a carbon shut inside works it
// from tg's SkillStation window (the client's Interfaces/SkillStation.cs): a skillchip put into the open machine is implanted in fifteen
// seconds, an implanted chip is extracted (or destroyed when it is not removable) in fifteen, or switched on and off; the open,
// occupied and working pictures. The var block is generated (D111).

// tg: code/modules/library/skill_learning/skill_station.dm:4
public partial class obj_machinery_skill_station : obj_machinery
{
    // tg: code/modules/library/skill_learning/skill_station.dm:1  #define SKILLCHIP_IMPLANT_TIME (15 SECONDS) (file-local, #undef at :309)
    private const float SKILLCHIP_IMPLANT_TIME = (15 * SECONDS);
    // tg: code/modules/library/skill_learning/skill_station.dm:2  #define SKILLCHIP_REMOVAL_TIME (15 SECONDS) (file-local, #undef at :310)
    private const float SKILLCHIP_REMOVAL_TIME = (15 * SECONDS);

    // tg: code/modules/library/skill_learning/skill_station.dm:22
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance();
        return dot;
    }

    //Only usable by the person inside
    // tg: code/modules/library/skill_learning/skill_station.dm:27
    public override object ui_state(mob user = null) => GLOB.contained_state;

    // tg: code/modules/library/skill_learning/skill_station.dm:30
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "SkillStation", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/library/skill_learning/skill_station.dm:36
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.initial(this, (obj_machinery_skill_station p) => p.icon_state);
        if (Dm.truthy(state_open))
            icon_state += "_open";
        if (occupant != null)
            icon_state += "_occupied";
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/library/skill_learning/skill_station.dm:44
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(working))
            Dm.append(ref dot, "working");
        return dot;
    }

    // tg: code/modules/library/skill_learning/skill_station.dm:49
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        await open_machine();
        return null;
    }

    // tg: code/modules/library/skill_learning/skill_station.dm:52
    public override async DmTask<object> open_machine(float? drop = null, float? density_to_set = null)
    {
        drop ??= TRUE;
        density_to_set ??= FALSE;
        object dot = await base.open_machine(drop, density_to_set);
        interrupt_operation();
        return dot;
    }

    // tg: code/modules/library/skill_learning/skill_station.dm:56
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == inserted_skillchip)
        {
            inserted_skillchip = null;
            interrupt_operation();
        }
        return dot;
    }

    // tg: code/modules/library/skill_learning/skill_station.dm:62
    public override object power_change()
    {
        object dot = base.power_change();
        if (Dm.truthy(working))
            interrupt_operation();
        return dot;
    }

    // tg: code/modules/library/skill_learning/skill_station.dm:67
    public override async DmTask<object> close_machine(atom_movable target = null, float? density_to_set = null)
    {
        density_to_set ??= TRUE;
        object dot = await base.close_machine(target, density_to_set);
        if (occupant != null)
            await ui_interact(occupant as mob);
        return dot;
    }

    // tg: code/modules/library/skill_learning/skill_station.dm:72
    public virtual void interrupt_operation()
    {
        working = FALSE;
        if (Dm.truthy(work_timer))
        {
            deltimer(work_timer);
            work_timer = null;
        }
        update_appearance();
    }

    // tg: code/modules/library/skill_learning/skill_station.dm:79
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        object dot = await base.interact(user, __extra);
        if (user == occupant)
            await ui_interact(user);
        else
            await toggle_open();
        return dot;
    }

    // tg: code/modules/library/skill_learning/skill_station.dm:86
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_skillchip)
            return NONE;
        if (inserted_skillchip != null)
        {
            to_chat(user, span_notice(I18n.T("There's already a skillchip inside.")));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            return ITEM_INTERACT_BLOCKING;
        inserted_skillchip = (obj_item_skillchip)tool;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/library/skill_learning/skill_station.dm:97
    public override async DmTask dump_contents(params object[] __extra)
    {
        await base.dump_contents(__extra);
        inserted_skillchip = null;
    }

    // tg: code/modules/library/skill_learning/skill_station.dm:101
    public override async DmTask dump_inventory_contents(DmList subset = null)
    {
        // Don't drop the skillchip, it's directly inserted into the machine.
        // dump_contents() will drop everything including the skillchip as an alternative to this.
        await base.dump_inventory_contents(Dm.sub(contents, inserted_skillchip) as DmList);
    }

    // tg: code/modules/library/skill_learning/skill_station.dm:106
    public virtual async DmTask toggle_open(mob user = null)
    {
        if (Dm.truthy(state_open))
            await close_machine();
        else
            await open_machine();
    }

    // Functions below do not validate occupant exists - should be handled outer wrappers.
    /// Start implanting.
    // tg: code/modules/library/skill_learning/skill_station.dm:111
    public virtual void start_implanting()
    {
        mob_living_carbon carbon_occupant = occupant as mob_living_carbon;

        if (Dm.truthy(inserted_skillchip.has_mob_incompatibility(carbon_occupant)))
            throw Dm.CRASH(Dm.text("Unusual error - [] attempted to start implanting of [] when the interface state should not have allowed it.", usr, inserted_skillchip), "code/modules/library/skill_learning/skill_station.dm", 115);

        working = TRUE;
        work_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(implant)), SKILLCHIP_IMPLANT_TIME, TIMER_STOPPABLE, file: "code/modules/library/skill_learning/skill_station.dm", line: 118);
        update_appearance();
    }

    /// Finish implanting.
    // tg: code/modules/library/skill_learning/skill_station.dm:122
    public virtual async DmTask implant()
    {
        working = FALSE;
        work_timer = null;
        mob_living_carbon carbon_occupant = occupant as mob_living_carbon;
        object implant_msg = await carbon_occupant.implant_skillchip(inserted_skillchip, FALSE);
        if (Dm.truthy(implant_msg))
            to_chat(carbon_occupant, span_notice(I18n.T("Operation failed! []", implant_msg)));
        else
        {
            to_chat(carbon_occupant, span_notice(I18n.T("Operation complete!")));
            inserted_skillchip = null;
        }

        update_appearance();
    }

    /// Start removal.
    // tg: code/modules/library/skill_learning/skill_station.dm:136
    public virtual void start_removal(obj_item_skillchip to_be_removed = null)
    {
        if (to_be_removed == null)
            return;

        if (Dm.truthy(to_be_removed.is_on_cooldown()))
        {
            to_chat(occupant, span_notice(I18n.T("DANGER! Operation cannot be completed, removal is unsafe.")));
            throw Dm.CRASH(Dm.text("Unusual error - [] attempted to start removal of [] when the interface state should not have allowed it.", usr, to_be_removed), "code/modules/library/skill_learning/skill_station.dm", 142);
        }

        working = TRUE;
        work_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(remove_skillchip), to_be_removed), SKILLCHIP_REMOVAL_TIME, TIMER_STOPPABLE, file: "code/modules/library/skill_learning/skill_station.dm", line: 145);
        update_appearance();
    }

    /// Finish removal.
    // tg: code/modules/library/skill_learning/skill_station.dm:149
    public virtual async DmTask remove_skillchip(obj_item_skillchip to_be_removed = null)
    {
        working = FALSE;
        work_timer = null;
        update_appearance();

        mob_living_carbon carbon_occupant = occupant as mob_living_carbon;

        if (Dm.truthy(to_be_removed.is_on_cooldown()))
        {
            to_chat(carbon_occupant, span_notice(I18n.T("Safety mechanisms activated! Skillchip cannot be safely removed.")));
            return;
        }

        if (carbon_occupant is not mob_living_carbon)
        {
            to_chat(carbon_occupant, span_notice(I18n.T("Occupant does not appear to be a carbon-based lifeform!")));
            return;
        }

        if (!Dm.truthy(await carbon_occupant.remove_skillchip(to_be_removed)))
        {
            to_chat(carbon_occupant, span_notice(I18n.T("Failed to remove skillchip!")));
            return;
        }

        if (Dm.truthy(to_be_removed.removable))
            await carbon_occupant.put_in_hands(to_be_removed);
        else
            qdel(to_be_removed);

        to_chat(carbon_occupant, span_notice(I18n.T("Operation complete!")));
    }

    // tg: code/modules/library/skill_learning/skill_station.dm:175
    public virtual async DmTask toggle_chip_active(obj_item_skillchip to_be_toggled = null)
    {
        mob_living_carbon carbon_occupant = occupant as mob_living_carbon;

        if (Dm.truthy(to_be_toggled.is_on_cooldown()))
        {
            to_chat(carbon_occupant, span_notice(I18n.T("Safety mechanisms activated! Skillchip cannot be safely modified.")));
            return;
        }

        if (carbon_occupant is not mob_living_carbon)
        {
            to_chat(carbon_occupant, span_notice(I18n.T("Occupant does not appear to be a carbon-based lifeform!")));
            return;
        }

        if (Dm.truthy(to_be_toggled.is_active()))
        {
            object active_msg = await to_be_toggled.try_deactivate_skillchip(FALSE, FALSE);
            if (Dm.truthy(active_msg))
                to_chat(carbon_occupant, span_notice(I18n.T("Failed to deactivate skillchip! []", active_msg)));
            return;
        }

        // This code will fire when to_be_toggled.active is FALSE
        object active_msg__1 = await to_be_toggled.try_activate_skillchip(FALSE, FALSE);
        if (Dm.truthy(active_msg__1))
            to_chat(carbon_occupant, span_notice(I18n.T("Failed to activate skillchip! []", active_msg__1)));
    }

    // tg: code/modules/library/skill_learning/skill_station.dm:197
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList dot = await base.ui_data(user) as DmList;
        dot["working"] = working;
        dot["timeleft"] = Dm.truthy(work_timer) ? timeleft(work_timer) : null;
        mob_living_carbon carbon_occupant = occupant as mob_living_carbon;

        dot["skillchip_ready"] = inserted_skillchip != null ? TRUE : FALSE;
        if (inserted_skillchip != null)
        {
            // This is safe, incompatibility check can accept a null or invalid mob.
            object incompatibility_check = inserted_skillchip.has_mob_incompatibility(carbon_occupant);
            // Grab chip data. We do this because of special chips like Chameleon that may want to
            // spoof their information.
            DmList inserted_chip_data = inserted_skillchip.get_chip_data() as DmList;
            if (Dm.truthy(incompatibility_check))
            {
                dot["implantable"] = FALSE;
                dot["implantable_reason"] = incompatibility_check;
            }
            else
            {
                dot["implantable"] = TRUE;
                dot["implantable_reason"] = null;
            }
            dot["skill_name"] = inserted_chip_data["name"];
            dot["skill_desc"] = inserted_chip_data["desc"];
            dot["skill_icon"] = inserted_chip_data["icon"];
            dot["complexity"] = inserted_chip_data["complexity"];
            dot["slot_use"] = inserted_chip_data["slot_use"];
        }

        // If there's no occupant, we don't need to worry about what skillchips are in their brain.
        if (carbon_occupant == null)
        {
            dot["error"] = I18n.T("No valid occupant detected. Please consult nearest medical practitioner.");
            dot["current"] = null;
            dot["complexity_used"] = null;
            dot["complexity_max"] = null;
            dot["slots_used"] = null;
            dot["slots_max"] = null;
            return dot;
        }

        obj_item_organ_brain occupant_brain = carbon_occupant.get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;

        // If there's no brain, we don't need to worry either.
        if (QDELETED(occupant_brain))
        {
            dot["error"] = I18n.T("Brain not detected. Please consult nearest medical practitioner.");
            dot["current"] = null;
            dot["complexity_used"] = null;
            dot["complexity_max"] = null;
            dot["slots_used"] = null;
            dot["slots_max"] = null;
            return dot;
        }

        dot["complexity_used"] = occupant_brain.get_used_skillchip_complexity();
        dot["complexity_max"] = occupant_brain.get_max_skillchip_complexity();
        dot["slots_used"] = occupant_brain.get_used_skillchip_slots();
        dot["slots_max"] = occupant_brain.get_max_skillchip_slots();

        // If we got here, we have both an occupant and it has a brain, so we can check for skillchips.
        DmList current_skills = Dm.list();
        foreach (obj_item_skillchip skill_chip in Dm.each<obj_item_skillchip>(occupant_brain.skillchips))
            Dm.append(ref current_skills, Dm.list(skill_chip.get_chip_data()));
        dot["current"] = current_skills;
        return dot;
    }

    // tg: code/modules/library/skill_learning/skill_station.dm:255
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;
        if (usr != occupant)
            return dot;
        object sw__1 = action;
        if (Dm.eq(sw__1, "implant"))
        {
            if (Dm.truthy(working))
            {
                stack_trace(Dm.text("[] tried to start skillchip implanting when [] was in an invalid state.", usr, this), "code/modules/library/skill_learning/skill_station.dm", 264);
                return TRUE;
            }
            if (occupant != null && inserted_skillchip != null)
                start_implanting();
            return TRUE;
        }
        else if (Dm.eq(sw__1, "remove"))
        {
            if (Dm.truthy(working))
            {
                stack_trace(Dm.text("[] tried to start skillchip removal when [] was in an invalid state.", usr, this), "code/modules/library/skill_learning/skill_station.dm", 271);
                return TRUE;
            }
            object chipref = @params["ref"];
            mob_living_carbon carbon_occupant = occupant as mob_living_carbon;
            obj_item_organ_brain occupant_brain = carbon_occupant.get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;
            if (QDELETED(carbon_occupant) || QDELETED(occupant_brain))
                return TRUE;
            obj_item_skillchip to_be_removed = Dm.locate_ref(chipref) is obj_item_skillchip found && Dm.@in(found, occupant_brain.skillchips) ? found : null; // locate(chipref) in occupant_brain.skillchips
            if (to_be_removed == null)
                return TRUE;
            start_removal(to_be_removed);
            return TRUE;
        }
        else if (Dm.eq(sw__1, "eject"))
        {
            if (Dm.truthy(working))
            {
                stack_trace(Dm.text("[] tried to toggle skillchip activation when [] was in an invalid state.", usr, this), "code/modules/library/skill_learning/skill_station.dm", 285);
                return TRUE;
            }
            if (inserted_skillchip != null)
            {
                to_chat(occupant, span_notice(I18n.T("You eject the skillchip.")));
                mob_living_carbon_human H = occupant as mob_living_carbon_human;
                await H.put_in_hands(inserted_skillchip);
                inserted_skillchip = null;
                return TRUE;
            }
        }
        else if (Dm.eq(sw__1, "toggle_activate"))
        {
            object chipref = @params["ref"];
            // Check if the machine is already working. If it is, this act should not have sent.
            if (Dm.truthy(working))
            {
                stack_trace(Dm.text("[] tried to toggle skillchip activation when [] was in an invalid state.", usr, this), "code/modules/library/skill_learning/skill_station.dm", 297);
                return TRUE;
            }
            mob_living_carbon carbon_occupant = occupant as mob_living_carbon;
            obj_item_organ_brain occupant_brain = carbon_occupant.get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;
            if (QDELETED(carbon_occupant) || QDELETED(occupant_brain))
                return TRUE;
            obj_item_skillchip to_be_removed = Dm.locate_ref(chipref) is obj_item_skillchip found && Dm.@in(found, occupant_brain.skillchips) ? found : null; // locate(chipref) in occupant_brain.skillchips
            if (to_be_removed == null)
                return TRUE;
            await toggle_chip_active(to_be_removed);
            return TRUE;
        }
        return dot;
    }
}
