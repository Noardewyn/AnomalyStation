// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/gulag_teleporter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
// Gaps LC1 (lavaland.md L3's labour camp): the whole file. The labour camp teleporter: a pod that opens, closes, locks and holds
// the prisoner (a breakout takes a minute, half when not locked and restrained), strips what the camp does not allow into the item
// reclaimer and dresses the prisoner in the jumpsuit, the orange shoes and the prisoner ID, marking the record incarcerated. The
// board and the camp's beacon are vars only (generated).

/*
The gulag teleporter teleports a prisoner to the gulag outpost.
It automatically strips the prisoner and equips a prisoner ID, prisoner jumpsuit and oranges sneakers.
You can set the amount of points in the console
The console is located at computer/gulag_teleporter.dm
*/

using System.Linq;
using Anomaly.Core.DmShim;
using static Anomaly.Core.DmShim.Dm;
using Anomaly.Core.Kernel;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

//Gulag teleporter
// tg: code/game/machinery/gulag_teleporter.dm:9
public partial class obj_machinery_gulag_teleporter : obj_machinery
{
    // tg: code/game/machinery/gulag_teleporter.dm:28  var/static/list/telegulag_required_items
    private static DmList telegulag_required_items => TgState.Current.obj_machinery_gulag_teleporter__telegulag_required_items ??= typecacheof(Dm.list(
        typeof(obj_item_implant),
        typeof(obj_item_clothing_suit_space_eva_plasmaman),
        typeof(obj_item_clothing_under_plasmaman),
        typeof(obj_item_clothing_head_helmet_space_plasmaman),
        typeof(obj_item_clothing_gloves_color_plasmaman),
        typeof(obj_item_tank_internals),
        typeof(obj_item_clothing_mask_breath),
        typeof(obj_item_clothing_mask_gas))) as DmList;

    // tg: code/game/machinery/gulag_teleporter.dm:39
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        locate_reclaimer();
        return dot;
    }

    // tg: code/game/machinery/gulag_teleporter.dm:43
    public override object Destroy(float? force = null)
    {
        if (linked_reclaimer != null)
            linked_reclaimer.linked_teleporter = null;
        linked_reclaimer = null;
        return base.Destroy(force);
    }

    // tg: code/game/machinery/gulag_teleporter.dm:49
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        object dot = await base.interact(user, __extra);
        if (Dm.truthy(locked))
        {
            to_chat(user, span_warning(I18n.T("[] is locked!", this)));
            return dot;
        }
        await toggle_open();
        return dot;
    }

    // tg: code/game/machinery/gulag_teleporter.dm:56
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        return occupant == null ? default_deconstruction_screwdriver(user, tool) : NONE;
    }

    // tg: code/game/machinery/gulag_teleporter.dm:59
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_pry_open(user, tool, deconstruct_on_fail: TRUE);
    }

    // tg: code/game/machinery/gulag_teleporter.dm:62
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        string state = Dm.text("[][]", base_icon_state, Dm.truthy(state_open) ? "_open" : null);
        //no power or maintenance
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER | BROKEN)))
        {
            state += "_unpowered";
            if (Dm.truthy(Dm.bitand(machine_stat, MAINT)) || Dm.truthy(panel_open))
                state += "_maintenance";
            icon_state = state;
            return await base.update_icon_state(__extra);
        }

        if (Dm.truthy(Dm.bitand(machine_stat, MAINT)) || Dm.truthy(panel_open))
        {
            state += "_maintenance";
            icon_state = state;
            return await base.update_icon_state(__extra);
        }

        //running and someone in there
        if (occupant != null)
            state += "_occupied";
        icon_state = state;
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/machinery/gulag_teleporter.dm:81
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        if (IS_UNCONSCIOUS_OR_CRIT(user))
            return null;
        if (Dm.truthy(locked))
        {
            if (Dm.num(message_cooldown) <= world.time)
            {
                message_cooldown = world.time + 50;
                to_chat(user, span_warning(I18n.T("[]'s door won't budge!", this)));
            }
            return null;
        }
        await open_machine();
        return null;
    }

    // tg: code/game/machinery/gulag_teleporter.dm:91
    public override async DmTask<object> container_resist_act(mob_living user = null, params object[] __extra)
    {
        float resist_time = breakout_time;
        if (!Dm.truthy(locked))
        {
            if (!HAS_TRAIT(user, TRAIT_RESTRAINED))
            {
                await open_machine();
                return null;
            }
            resist_time *= 0.5f;
        }

        user.change_next_special_move(CLICK_CD_BREAKOUT);
        user.visible_message(
            span_notice(I18n.T("You see [] kicking against the door of []!", user, this)),
            span_notice(I18n.T("You lean on the back of [] and start pushing the door open... (this will take about [].)", this, DisplayTimeText(resist_time))),
            span_hear(I18n.T("You hear a metallic creaking from [].", this)));

        if (Dm.truthy(await do_after(user, resist_time, target: this)))
        {
            if (user == null || IS_UNCONSCIOUS_OR_CRIT(user) || !ReferenceEquals(user.loc, this) || Dm.truthy(state_open) || !Dm.truthy(locked))
                return null;
            locked = FALSE;
            user.visible_message(span_warning(I18n.T("[] successfully broke out of []!", user, this)),
                span_notice(I18n.T("You successfully break out of []!", this)));
            await open_machine();
        }
        return null;
    }

    // tg: code/game/machinery/gulag_teleporter.dm:115
    public virtual void locate_reclaimer()
    {
        linked_reclaimer = Dm.each<obj_machinery_gulag_item_reclaimer>().FirstOrDefault(); // locate(/obj/machinery/gulag_item_reclaimer)
        if (linked_reclaimer != null)
            linked_reclaimer.linked_teleporter = this;
    }

    // tg: code/game/machinery/gulag_teleporter.dm:120
    public virtual async DmTask toggle_open()
    {
        if (Dm.truthy(panel_open))
        {
            to_chat(usr, span_notice(I18n.T("Close the maintenance panel first.")));
            return;
        }

        if (Dm.truthy(state_open))
        {
            await close_machine();
            return;
        }
        if (!Dm.truthy(locked))
            await open_machine();
    }

    /// Strips the occupant of any items that are not allowed to be teleported to the gulag.
    /// Will either place those items in the linked_reclaimer or on the ground if the reclaimer is deleted.
    // tg: code/game/machinery/gulag_teleporter.dm:133
    public virtual async DmTask strip_prisoner(mob_living_carbon_human victim)
    {
        if (!QDELETED(linked_reclaimer))
            linked_reclaimer.stored_items[victim] = Dm.list();

        foreach (obj_item thing in Dm.each<obj_item>((victim.contents as DmList)?.Copy()))
        {
            if (Dm.truthy(is_type_in_typecache(thing, telegulag_required_items)))
                continue;

            if (!Dm.truthy(await victim.temporarilyRemoveItemFromInventory(thing)))
                continue;

            if (QDELETED(linked_reclaimer) || thing is obj_item_restraints_handcuffs)
            {
                await thing.forceMove(get_turf(this));
                continue;
            }

            DmList stored = (DmList)linked_reclaimer.stored_items[victim];
            Dm.append(ref stored, thing);
            linked_reclaimer.stored_items[victim] = stored;
            await thing.forceMove(linked_reclaimer);
        }
    }

    // tg: code/game/machinery/gulag_teleporter.dm:151
    public virtual async DmTask handle_prisoner(obj_item id, datum_record_crew target)
    {
        if (occupant is not mob_living_carbon_human prisoner)
            return;
        await strip_prisoner(prisoner);
        if (!isplasmaman(prisoner) && jumpsuit_type != null)
        {
            Type suit_or_skirt = Dm.eq(prisoner.jumpsuit_style, PREF_SKIRT) ? jumpskirt_type : jumpsuit_type; //Check player prefs for jumpsuit or jumpskirt toggle, then give appropriate prison outfit.
            await prisoner.equip_to_appropriate_slot(Dm.@new(suit_or_skirt) as obj_item, qdel_on_fail: TRUE);
        }
        if (isplasmaman(prisoner) && prisoner.gloves == null && emergency_plasglove_type != null)
            await prisoner.equip_to_appropriate_slot(Dm.@new(emergency_plasglove_type) as obj_item, qdel_on_fail: TRUE);
        if (shoes_type != null)
            await prisoner.equip_to_appropriate_slot(Dm.@new(shoes_type) as obj_item, qdel_on_fail: TRUE);
        if (id != null)
            await prisoner.equip_to_appropriate_slot(id, qdel_on_fail: TRUE);
        if (target != null)
            target.wanted_status = WANTED_PRISONER;

        await use_energy(active_power_usage);
    }
}

public sealed partial class TgState
{
    internal DmList obj_machinery_gulag_teleporter__telegulag_required_items;
}
