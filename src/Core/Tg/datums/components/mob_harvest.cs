// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/mob_harvest.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the admin var edit (a row) (gaps package C4).

/**
 * Harvesting component. Useful if you want to be able to harvest items from living mobs.
 *
 * Used currently on sheep.
 */
// tg: code/datums/components/mob_harvest.dm:6
public partial class datum_component_mob_harvest : datum_component
{
    //harvest_type, produced_item_typepath and speedup_type are typepaths, not reference
    // tg: code/datums/components/mob_harvest.dm:31
    [DmParams("harvest_tool", "fed_item", "produced_item_typepath", "produced_item_desc", "max_ready", "item_generation_wait", "item_reduction_time", "item_harvest_time", "item_harvest_sound")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;
        this.harvest_tool = Dm.arg(__extra, 0) as Type;
        this.fed_item = Dm.arg(__extra, 1) as Type;
        this.produced_item_typepath = (Type)Dm.arg(__extra, 2);
        this.produced_item_desc = Dm.arg(__extra, 3) as string;
        this.max_ready = Dm.num(Dm.arg(__extra, 4));
        this.item_generation_wait = Dm.num(Dm.arg(__extra, 5));
        this.item_reduction_time = Dm.num(Dm.arg(__extra, 6));
        this.item_harvest_time = Dm.num(Dm.arg(__extra, 7));
        item_generation_time = item_generation_wait;
        START_PROCESSING(SSobj, this);
        return null;
    }

    // tg: code/datums/components/mob_harvest.dm:45
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }

    // tg: code/datums/components/mob_harvest.dm:63
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        ///only track time if we aren't dead and have room for more items
        mob_living harvest_mob = parent as mob_living;
        if (harvest_mob.stat == DEAD || amount_ready >= max_ready)
            return null;

        item_generation_time -= Dm.num(seconds_per_tick);
        if (item_generation_time > 0)
            return null;

        item_generation_time = item_generation_wait;
        amount_ready++;
        if (iscarbon(parent))
            return null;

        mob_living living_parent = parent as mob_living;
        living_parent.update_appearance(UPDATE_ICON_STATE);
        return null;
    }

    // tg: code/datums/components/mob_harvest.dm:81
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(parent, COMSIG_ATOM_ATTACKBY, nameof(on_attackby));

        // Only do update_icon_state business on non-carbon mobs
        if (!iscarbon(parent))
        {
            RegisterSignal(parent, COMSIG_ATOM_UPDATE_ICON_STATE, nameof(on_update_icon_state));

            mob_living living_parent = parent as mob_living;
            living_parent.update_appearance(UPDATE_ICON_STATE);
        }
    }

    // tg: code/datums/components/mob_harvest.dm:92
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_ATOM_EXAMINE, COMSIG_ATOM_ATTACKBY, COMSIG_ATOM_UPDATE_ICON_STATE));
    }

    ///signal called on parent being examined
    // tg: code/datums/components/mob_harvest.dm:96
    [SignalHandler]
    public virtual void on_examine(datum source = null, mob user = null, DmList examine_list = null)
    {
        if (amount_ready < 1)
            Dm.append(ref examine_list, span_notice(Dm.text("[] seems like they could use a bit more time.", parent)));
        if (amount_ready > 1)
            Dm.append(ref examine_list, span_notice(Dm.text("[] looks like they can be harvested about [] times.", parent, amount_ready)));
        if (amount_ready == 1)
            Dm.append(ref examine_list, span_notice(Dm.text("[] looks ready to be harvested.", parent)));
    }

    ///signal called on parent being attacked with an item
    // tg: code/datums/components/mob_harvest.dm:107
    [SignalHandler]
    public virtual object on_attackby(datum source = null, obj_item used_item = null, mob user = null)
    {
        if (Dm.istype(used_item, harvest_tool))
        {
            Waiting.Detach(harvest_item(user));
            return COMPONENT_NO_AFTERATTACK;
        }

        if (Dm.istype(used_item, fed_item))
        {
            remove_wait_time(user);
            qdel(used_item);
            return COMPONENT_NO_AFTERATTACK;
        }
        return null;
    }

    /// Signal proc for [COMSIG_ATOM_UPDATE_ICON_STATE]
    // tg: code/datums/components/mob_harvest.dm:120
    [SignalHandler]
    public virtual void on_update_icon_state(datum source = null)
    {
        // If this is being used on a carbon or human, don't update any icon states, just leave it
        if (iscarbon(parent))
            return;

        mob_living living_parent = parent as mob_living;
        if (living_parent.stat == DEAD)
            return;

        living_parent.icon_state = Dm.text("[][]", living_parent.base_icon_state ?? initial(living_parent, "icon_state"), amount_ready < 1 ? "_harvested" : "");
    }

    // tg: code/datums/components/mob_harvest.dm:139
    public virtual void remove_wait_time(mob user = null)
    {
        if (amount_ready >= max_ready)
        {
            to_chat(user, span_warning(Dm.text("[] looks too full to keep feeding!", parent)));
            return;
        }
        item_generation_time -= item_reduction_time;
        to_chat(user, span_notice(Dm.text("You feed [].", parent)));
    }

    // tg: code/datums/components/mob_harvest.dm:153
    public virtual async DmTask harvest_item(mob user = null)
    {
        if (amount_ready < 1)
        {
            to_chat(user, span_warning(Dm.text("[] doesn't seem to have enough [] to harvest.", parent, produced_item_desc)));
            return;
        }
        to_chat(user, span_notice(Dm.text("You start to harvest [] from []...", produced_item_desc, parent)));
        if (Dm.truthy(await do_after(user, item_harvest_time, target: parent)))
        {
            playsound(parent as atom, item_harvest_sound, 20, TRUE);
            to_chat(user, span_notice(Dm.text("You harvest some [] from [].", produced_item_desc, parent)));
            amount_ready--;
            if (!iscarbon(parent))
            {
                mob_living living_parent = parent as mob_living;
                living_parent.update_appearance(UPDATE_ICON_STATE);
            }
            Dm.@new(produced_item_typepath, get_turf(parent));
            return;
        }
    }
}
