// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/traitor/components/demoraliser.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G3b: the seditious spraycan's rune, the demotivational posters). The mood categories and events have no
// procs; the moods they give are live since gaps MS1. The var blocks are generated (D111).

/*
 * Applies a role-based mood if you can see the parent.
 *
 * - Applies a mood to people who are in visible range of the item.
 * - Does not re-apply mood to people who already have it.
 * - Sends a signal if a mood is successfully applied.
 */
// tg: code/modules/antagonists/traitor/components/demoraliser.dm:8
public partial class datum_proximity_monitor_advanced_demoraliser : datum_proximity_monitor_advanced
{
    // tg: code/modules/antagonists/traitor/components/demoraliser.dm:11
    [DmParams("_host", "range", "_ignore_if_not_on_turf", "moods")]
    public override async DmTask New(params object[] __extra)
    {
        datum_demoralise_moods moods = Dm.arg<datum_demoralise_moods>(__extra, 3);
        await base.New(__extra);
        this.moods = moods;
        RegisterSignal(host, COMSIG_ATOM_EXAMINE, nameof(on_examine));
    }

    // tg: code/modules/antagonists/traitor/components/demoraliser.dm:16
    public override void field_turf_crossed(atom_movable crossed = null, turf old_location = null, turf new_location = null)
    {
        if (!isliving(crossed))
            return;
        if (!Dm.truthy(can_see(crossed, host, current_range)))
            return;
        on_seen((mob_living)crossed);
    }

    /*
     * Signal proc for [COMSIG_ATOM_EXAMINE].
     * Immediately tries to apply a mood to the examiner, ignoring the proximity check.
     * If someone wants to make themselves sad through a camera that's their choice I guess.
     */
    // tg: code/modules/antagonists/traitor/components/demoraliser.dm:28
    [SignalHandler]
    public virtual void on_examine(datum source = null, mob examiner = null)
    {
        if (isliving(examiner))
            on_seen((mob_living)examiner);
    }

    /**
     * Called when someone is looking at a demoralising object.
     * Applies a mood if they are conscious and don't already have it.
     * Different moods are applied based on whether they are an antagonist, authority, or 'other' (presumed crew).
     *
     * Arguments
     * * viewer - Whoever is looking at this.
     */
    // tg: code/modules/antagonists/traitor/components/demoraliser.dm:41
    public virtual void on_seen(mob_living viewer = null)
    {
        if (viewer.mind == null)
            return;
        // If you're not conscious you're too busy or dead to look at propaganda
        if (IS_UNCONSCIOUS_OR_CRIT(viewer))
            return;
        if (Dm.truthy(viewer.is_blind()))
            return;
        if (!Dm.truthy(should_demoralise(viewer)))
            return;
        if (!Dm.truthy(viewer.can_read(host, moods.reading_requirements, TRUE))) //if it's a text based demoralization datum, make sure the mob has the capability to read. if it's only an image, make sure it's just bright enough for them to see it.
            return;

        if (Dm.truthy(viewer.is_antag()))
        {
            to_chat(viewer, span_notice(Dm.text("[]", moods.antag_notification)));
            Waiting.NoSleep(viewer.add_mood_event(moods.mood_category, moods.antag_mood));
        }
        else if (Dm.truthy(Dm.bitand(viewer.mind.assigned_role.departments_bitflags, Dm.bitor(DEPARTMENT_BITFLAG_SECURITY, DEPARTMENT_BITFLAG_COMMAND))))
        {
            to_chat(viewer, span_notice(Dm.text("[]", moods.authority_notification)));
            Waiting.NoSleep(viewer.add_mood_event(moods.mood_category, moods.authority_mood));
        }
        else
        {
            to_chat(viewer, span_notice(Dm.text("[]", moods.crew_notification)));
            Waiting.NoSleep(viewer.add_mood_event(moods.mood_category, moods.crew_mood));
        }

        if (Dm.truthy(host._listen_lookup?[COMSIG_DEMORALISING_EVENT]))
            host._SendSignal(COMSIG_DEMORALISING_EVENT, Dm.list(host, viewer.mind));
    }

    /**
     * Returns true if user is capable of experiencing moods and doesn't already have the one relevant to this datum, false otherwise.
     *
     * Arguments
     * * viewer - Whoever just saw the parent.
     */
    // tg: code/modules/antagonists/traitor/components/demoraliser.dm:71
    public virtual object should_demoralise(mob_living viewer = null)
    {
        if (viewer.mob_mood == null)
            return FALSE;

        return Dm.num(!Dm.truthy(viewer.mob_mood.has_mood_of_category(moods.mood_category)));
    }
}
