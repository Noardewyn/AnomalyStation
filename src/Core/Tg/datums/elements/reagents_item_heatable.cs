// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/reagents_item_heatable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice C: the whole file (a hot item warms a cup's contents, a fire extinguisher cools them).

/// This atom can be touched by a hot item to warm up its reagents
// tg: code/datums/elements/reagents_item_heatable.dm:2
public partial class datum_element_reagents_item_heatable : datum_element
{
    // tg: code/datums/elements/reagents_item_heatable.dm:4
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!isatom(target))
            return ELEMENT_INCOMPATIBLE;
        RegisterSignal(target, COMSIG_ATOM_ITEM_INTERACTION, nameof(on_item_interact));
        return dot;
    }

    // tg: code/datums/elements/reagents_item_heatable.dm:10
    public override object Detach(datum source = null, params object[] __extra)
    {
        UnregisterSignal(source, COMSIG_ATOM_ITEM_INTERACTION);
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/reagents_item_heatable.dm:14
    [SignalHandler]
    public virtual object on_item_interact(atom source = null, mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        datum_reagents holder = source.reagents as datum_reagents;
        if (holder == null || holder.total_volume <= 0)
            return NONE;

        object hotness = tool.get_temperature();
        if (Dm.truthy(hotness))
        {
            Waiting.NoSleep(holder.expose_temperature(hotness));
            source.balloon_alert(user, I18n.T("heated []", source));
            return ITEM_INTERACT_SUCCESS;
        }

        //Cooling method
        if (tool is obj_item_extinguisher extinguisher)
        {
            if (Dm.truthy(extinguisher.safety))
                return NONE;
            if ((extinguisher.reagents as datum_reagents)?.total_volume < 1)
            {
                extinguisher.balloon_alert(user, I18n.T("extinguisher is empty!")); // being a bit more verbose to clarify the extinguisher - not source - is empty
                return ITEM_INTERACT_BLOCKING;
            }
            float cooling = (0 - holder.chem_temp) * Dm.num(extinguisher.cooling_power) * 2;
            Waiting.NoSleep(holder.expose_temperature(cooling));
            source.balloon_alert(user, I18n.T("cooled []", source));
            playsound(source, Dm.resource("sound/effects/extinguish.ogg"), 75, TRUE, -3);
            Waiting.NoSleep(((datum_reagents)extinguisher.reagents).remove_all(1));
            return ITEM_INTERACT_SUCCESS;
        }

        return NONE;
    }
}
