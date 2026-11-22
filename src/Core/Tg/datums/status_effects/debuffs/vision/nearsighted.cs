// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/debuffs/vision/nearsighted.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the owner's play test of 2026-10-27: welding without protection did not make the sight worse).

/// Nearsighted
// tg: code/datums/status_effects/debuffs/vision/nearsighted.dm:5
public partial class datum_status_effect_grouped_nearsighted : datum_status_effect_grouped
{
    /// Maximum severity level possible.
    // tg: code/datums/status_effects/debuffs/vision/nearsighted.dm:2  #define MAX_SEVERITY 3 (file-local, #undef at :128)
    private const int MAX_SEVERITY = 3;

    /// Static list of signals that, when received, we force an update to our nearsighted overlay
    // tg: code/datums/status_effects/debuffs/vision/nearsighted.dm:13  var/static/list/update_signals = list(...)  (rules 10.6: a TgState field, below)
    public static ref DmList update_signals => ref TgState.Current.datum_status_effect_grouped_nearsighted__update_signals;

    // tg: code/datums/status_effects/debuffs/vision/nearsighted.dm:31
    [DmParams("source", "severity", "correctable")]
    public override void source_added(object source = null, params object[] __extra)
    {
        object severity = Dm.arg(__extra, 0) ?? 2;
        object correctable = Dm.arg(__extra, 1) ?? TRUE;
        set_severity(source, severity, correctable);
    }

    // tg: code/datums/status_effects/debuffs/vision/nearsighted.dm:34
    public override void source_removed(object source = null, object removing = null)
    {
        if (Dm.truthy(correctable_sources[source]))
        {
            Dm.remove(ref correctable_sources, source);
            recalculate_severity(correctable: TRUE);
        }
        if (Dm.truthy(absolute_sources[source]))
        {
            Dm.remove(ref absolute_sources, source);
            recalculate_severity(correctable: FALSE);
        }
        if (!Dm.truthy(removing)) //so the overlay doesn't update twice
            update_nearsighted_overlay();
    }

    // tg: code/datums/status_effects/debuffs/vision/nearsighted.dm:44
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        RegisterSignals(owner, update_signals, nameof(update_nearsightedness));
        update_nearsighted_overlay();
        return await base.on_apply(__extra);
    }

    // tg: code/datums/status_effects/debuffs/vision/nearsighted.dm:49
    public override async DmTask<object> on_remove()
    {
        UnregisterSignal(owner, update_signals);
        owner.clear_fullscreen(id);
        return await base.on_remove();
    }

    /// Signal proc for when we gain or lose [TRAIT_NEARSIGHTED_CORRECTED] - (temporarily) disable the overlay if we're correcting it
    // tg: code/datums/status_effects/debuffs/vision/nearsighted.dm:55
    [SignalHandler]
    public virtual void update_nearsightedness(datum source = null)
    {
        update_nearsighted_overlay();
    }

    /// Checks if we should be nearsighted currently, or if we should clear the overlay
    // tg: code/datums/status_effects/debuffs/vision/nearsighted.dm:60
    public virtual float should_be_nearsighted()
    {
        if (HAS_TRAIT(owner, TRAIT_SIGHT_BYPASS))
            return NEARSIGHTED_DISABLED;
        if (HAS_TRAIT(owner, TRAIT_NEARSIGHTED_CORRECTED))
            return NEARSIGHTED_CORRECTED;
        return NEARSIGHTED_ENABLED;
    }

    /// Updates our nearsightd overlay, either removing it if we have the trait or adding it if we don't
    // tg: code/datums/status_effects/debuffs/vision/nearsighted.dm:68
    public virtual void update_nearsighted_overlay()
    {
        float severity = get_severity();
        if (severity <= 0) // We aren't nearsighted
            owner.clear_fullscreen(id);
        else
            owner.overlay_fullscreen(id, typeof(atom_movable_screen_fullscreen_impaired), severity);
    }

    /// Gets the severity value that would be used when calculating impairment.
    // tg: code/datums/status_effects/debuffs/vision/nearsighted.dm:76
    public virtual float get_severity()
    {
        float are_we_nearsighted = should_be_nearsighted();

        if (!Dm.truthy(are_we_nearsighted))
            return 0;

        float final_severity = absolute_severity;
        if (are_we_nearsighted != NEARSIGHTED_CORRECTED) //We don't have corrective vision
            final_severity = max(absolute_severity, correctable_severity);

        final_severity = min(final_severity, MAX_SEVERITY);

        return final_severity;
    }

    /// Sets the severity of a source. Recalculates the severity variables if there is a change
    // tg: code/datums/status_effects/debuffs/vision/nearsighted.dm:91
    public virtual void set_severity(object source = null, object new_severity = null, object correctable = null)
    {
        if (!Dm.truthy(source))
            return;
        if (!isnum(new_severity))
            return;

        ref DmList to_search = ref (Dm.truthy(correctable) ? ref correctable_sources : ref absolute_sources);
        if (Dm.eq(to_search[source], new_severity))
            return;

        if (Dm.num(new_severity) > 0)
            to_search[source] = new_severity;
        else
            Dm.remove(ref to_search, source);

        recalculate_severity(correctable);

        /* If we have no more of this source, let's remove it (and potentially ourselves) */
        if (!Dm.truthy(absolute_sources[source]) && !Dm.truthy(correctable_sources[source]))
            owner.remove_status_effect(type, source); //this will update our overlay as well if we're still around
        else
            update_nearsighted_overlay();
    }

    // tg: code/datums/status_effects/debuffs/vision/nearsighted.dm:114
    public virtual void recalculate_severity(object correctable = null)
    {
        if (correctable == null) // isnull(correctable)
            throw Dm.CRASH("was not provided with an argument (this needs to be explicit)", "code/datums/status_effects/debuffs/vision/nearsighted.dm", 116);

        float highest_severity = 0;
        DmList to_search = Dm.truthy(correctable) ? correctable_sources : absolute_sources;

        foreach (object existing_source in Dm.each(to_search))
        {
            float candidate = Dm.num(to_search[existing_source]);
            if (candidate > highest_severity)
                highest_severity = candidate;
        }

        if (Dm.truthy(correctable))
            correctable_severity = highest_severity;
        else
            absolute_severity = highest_severity;
    }
}

public sealed partial class TgState
{
    // tg: code/datums/status_effects/debuffs/vision/nearsighted.dm:13-18  /datum/status_effect/grouped/nearsighted/var/static/list/update_signals
    internal DmList datum_status_effect_grouped_nearsighted__update_signals = Dm.list(
        SIGNAL_ADDTRAIT(TRAIT_NEARSIGHTED_CORRECTED),
        SIGNAL_REMOVETRAIT(TRAIT_NEARSIGHTED_CORRECTED),
        SIGNAL_ADDTRAIT(TRAIT_SIGHT_BYPASS),
        SIGNAL_REMOVETRAIT(TRAIT_SIGHT_BYPASS));
}
