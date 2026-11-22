// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/regal_rat_minion.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

/// Allows a mob to be converted into a regal rat's minion when a riot is called.
// tg: code/datums/elements/regal_rat_minion.dm:3
public partial class datum_element_regal_rat_minion : datum_element
{
    // tg: code/datums/elements/regal_rat_minion.dm:12
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!isliving(target))
            return ELEMENT_INCOMPATIBLE;
        this.converted_path = Dm.arg(__extra, 0) as Type;
        this.success_balloon = Dm.arg(__extra, 1) ?? "squeak";
        this.pet_commands = Dm.arg(__extra, 2) as DmList;
        RegisterSignal(target, COMSIG_REGAL_RAT_RIOTED, nameof(on_rioted));
        return dot;
    }

    // tg: code/datums/elements/regal_rat_minion.dm:22
    public override object Detach(datum source = null, params object[] __extra)
    {
        object dot = base.Detach(source, __extra);
        UnregisterSignal(source, COMSIG_REGAL_RAT_RIOTED);
        return dot;
    }

    /// Our boss is calling a riot, turn into a minion
    // tg: code/datums/elements/regal_rat_minion.dm:27
    [SignalHandler]
    public virtual void on_rioted(mob_living minion = null, mob_living master = null)
    {
        Waiting.Detach(do_conversion(minion, master));
    }

    /// Actually turn our guy into a minion
    // tg: code/datums/elements/regal_rat_minion.dm:32
    public virtual async DmTask do_conversion(mob_living minion = null, mob_living master = null)
    {
        if (minion.stat == DEAD)
            return;
        object new_name = Dm.eq(minion.name, initial(minion, "name")) ? initial(converted_path, "name") : minion.name;
        mob_living new_minion = await minion.change_mob_type(converted_path, new_name: new_name, delete_old_mob: TRUE) as mob_living;
        if (Dm.truthy(Dm.length(pet_commands)))
            await new_minion.AddComponent(typeof(datum_component_obeys_commands), pet_commands);
        qdel(new_minion.GetComponent(typeof(datum_component_tameable))); // Rats don't share
        await new_minion.befriend(master);
        new_minion.set_faction(master.get_faction()); // SET_FACTION_AND_ALLIES_FROM (__DEFINES/mobfactions.dm:119), inline
        new_minion.set_allies(LAZYLISTDUPLICATE(master.allies));
        new_minion.balloon_alert_to_viewers(success_balloon);
    }
}
