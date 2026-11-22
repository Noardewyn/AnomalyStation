// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/knockoff.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps J3, D298: the cigarettes carry it). The var block is generated.

/// Items with this component will have a chance to get knocked off
/// (unequipped and sent to the ground) when the wearer is disarmed or knocked down.
// tg: code/datums/components/knockoff.dm:3
public partial class datum_component_knockoff : datum_component
{
    // tg: code/datums/components/knockoff.dm:15
    [DmParams("knockoff_chance", "target_zones", "slots_knockoffable")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;

        this.knockoff_chance = Dm.num(Dm.arg(__extra, 0) ?? 100);
        this.target_zones = Dm.arg(__extra, 1) as DmList;
        this.slots_knockoffable = Dm.num(Dm.arg(__extra, 2) ?? NONE);
        return null;
    }

    // tg: code/datums/components/knockoff.dm:23
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ITEM_EQUIPPED, nameof(on_equipped));
        RegisterSignal(parent, COMSIG_ITEM_DROPPED, nameof(on_dropped));
        atom_movable atom_parent = (atom_movable)parent;
        if (ismob(atom_parent.loc))
        {
            mob as_mob = (mob)atom_parent.loc;
            on_equipped(atom_parent, as_mob as mob_living_carbon_human, as_mob.get_slot_by_item((obj_item)atom_parent)); // lie a little
        }
    }

    // tg: code/datums/components/knockoff.dm:31
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_ITEM_EQUIPPED, COMSIG_ITEM_DROPPED));

        obj_item item_parent = (obj_item)parent;
        if (ismob(item_parent.loc))
            UnregisterSignal(item_parent.loc, Dm.list(COMSIG_LIVING_DISARM_HIT, COMSIG_LIVING_STATUS_KNOCKDOWN));
    }

    /// Signal proc for [COMSIG_LIVING_DISARM_HIT] on the mob who's equipped our parent
    /// Rolls a chance for knockoff whenever we're disarmed
    // tg: code/datums/components/knockoff.dm:40
    [SignalHandler]
    public virtual void on_equipped_mob_disarm(mob_living source = null, mob_living attacker = null, object zone = null, obj_item weapon = null)
    {
        if (source == null)
            return;

        if (target_zones != null && !Dm.@in(zone, target_zones))
            return;
        if (!prob(knockoff_chance))
            return;

        obj_item item_parent = (obj_item)parent;
        if (!Dm.truthy(Waiting.NoSleep(source.dropItemToGround(item_parent))))
            return;

        source.visible_message(
            span_warning(I18n.T("[] knocks off []'s []!", attacker, source, item_parent.name)),
            span_userdanger(I18n.T("[] knocks off your []!", attacker, item_parent.name)));
    }

    /// Signal proc for [COMSIG_LIVING_STATUS_KNOCKDOWN] on the mob who's equipped our parent
    /// Rolls a chance for knockoff whenever we're knocked down
    // tg: code/datums/components/knockoff.dm:62
    [SignalHandler]
    public virtual void on_equipped_mob_knockdown(mob_living_carbon_human source = null, object amount = null)
    {
        if (source == null)
            return;

        // Healing knockdown or setting knockdown to zero or something? Don't knock off.
        if (Dm.num(amount) <= 0)
            return;
        if (!prob(knockoff_chance))
            return;

        obj_item item_parent = (obj_item)parent;
        if (!Dm.truthy(Waiting.NoSleep(source.dropItemToGround(item_parent))))
            return;

        source.visible_message(
            span_warning(I18n.T("[]'s [] get[] knocked off!", source, item_parent.name, item_parent.p_s())),
            span_userdanger(I18n.T("Your [] [] knocked off!", item_parent.name, item_parent.p_were())));
    }

    /// Signal proc for [COMSIG_ITEM_EQUIPPED]
    /// Registers our signals which can cause a knockdown whenever we're equipped correctly
    // tg: code/datums/components/knockoff.dm:85
    [SignalHandler]
    public virtual void on_equipped(datum source = null, mob_living_carbon_human equipper = null, object slot = null)
    {
        if (equipper == null)
            return;

        if (Dm.truthy(slots_knockoffable) && !Dm.truthy(Dm.bitand(slot, slots_knockoffable)))
        {
            UnregisterSignal(equipper, Dm.list(COMSIG_LIVING_DISARM_HIT, COMSIG_LIVING_STATUS_KNOCKDOWN));
            return;
        }

        RegisterSignal(equipper, COMSIG_LIVING_DISARM_HIT, nameof(on_equipped_mob_disarm), TRUE);
        RegisterSignal(equipper, COMSIG_LIVING_STATUS_KNOCKDOWN, nameof(on_equipped_mob_knockdown), TRUE);
    }

    /// Signal proc for [COMSIG_ITEM_DROPPED]
    /// Unregisters our signals which can cause a knockdown when we're unequipped (dropped)
    // tg: code/datums/components/knockoff.dm:100
    [SignalHandler]
    public virtual void on_dropped(datum source = null, mob_living dropper = null)
    {
        UnregisterSignal(dropper, Dm.list(COMSIG_LIVING_DISARM_HIT, COMSIG_LIVING_STATUS_KNOCKDOWN));
    }
}
