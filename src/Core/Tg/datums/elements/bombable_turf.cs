// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/bombable_turf.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the watchers' grave's cracked wall): a turf that any explosion scrapes away, with its crack.

/**
 * Apply this to a turf (usually a wall) and it will be destroyed instantly by any explosion.
 * Most walls can already be destroyed by explosions so this is largely for usually indestructible ones.
 * For applying it in a map editor, use /obj/effect/mapping_helpers/bombable_wall
 */
// tg: code/datums/elements/bombable_turf.dm:6
public partial class datum_element_bombable_turf : datum_element
{
    // tg: code/datums/elements/bombable_turf.dm:8
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        if (!isturf(target))
            return ELEMENT_INCOMPATIBLE;
        var target_turf = (turf)target;
        target_turf.explosive_resistance = 1;

        RegisterSignal(target_turf, COMSIG_ATOM_EX_ACT, nameof(detonate));
        RegisterSignal(target_turf, COMSIG_TURF_CHANGE, nameof(turf_changed));
        RegisterSignal(target_turf, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(on_update_overlays));
        RegisterSignal(target_turf, COMSIG_ATOM_EXAMINE, nameof(on_examined));

        target_turf.update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/datums/elements/bombable_turf.dm:21
    public override object Detach(datum source = null, params object[] __extra)
    {
        var source_turf = (turf)source;
        UnregisterSignal(source_turf, Dm.list(COMSIG_ATOM_EX_ACT, COMSIG_TURF_CHANGE, COMSIG_ATOM_UPDATE_OVERLAYS, COMSIG_ATOM_EXAMINE));
        source_turf.explosive_resistance = Dm.initial(source_turf, (turf t) => t.explosive_resistance);
        source_turf.update_appearance(UPDATE_OVERLAYS);
        return base.Detach(source, __extra);
    }

    /// If we get blowed up, move to the next turf
    // tg: code/datums/elements/bombable_turf.dm:28
    [SignalHandler]
    public virtual void detonate(turf source = null)
    {
        Waiting.NoSleep(source.ScrapeAway());
    }

    /// If this turf becomes something else we either just went off or regardless don't want this any more
    // tg: code/datums/elements/bombable_turf.dm:33
    [SignalHandler]
    public virtual void turf_changed(turf source = null)
    {
        Detach(source);
    }

    /// Show a little crack on here
    // tg: code/datums/elements/bombable_turf.dm:38
    [SignalHandler]
    public virtual void on_update_overlays(turf source = null, DmList overlays = null)
    {
        Dm.append(ref overlays, mutable_appearance(Dm.resource("icons/turf/overlays.dmi"), "explodable", source.layer + 0.1f));
    }

    /// Show a little extra on examine
    // tg: code/datums/elements/bombable_turf.dm:43
    [SignalHandler]
    public virtual void on_examined(turf source = null, mob user = null, DmList examine_list = null)
    {
        Dm.append(ref examine_list, span_notice(I18n.T("It seems to be slightly cracked...")));
    }
}
