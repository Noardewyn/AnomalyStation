// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/gizmo/gizmo_voice.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the voice gizmo's ears. The round's keywords are picked once from strings/gizmo_words.txt (one per pulse of the
// puzzle); what is said near the gizmo is searched for them, and the keywords heard go to the puzzle in the order they were said,
// with feedback for the last one only. The var block is generated (D111).
//
// GAP(types): tg's `var/datum/gizmo_puzzle/puzzle` (:8) is generated as a Type (rules 3.2 V3: the name is typepath-assigned
// elsewhere); the puzzle the component pulses is puzzle_value.

public partial class datum_controller_global_vars
{
    /// The words (or tones really) the gizmo voice interface can listen for. Limit to 2 characters (or it breaks because im cringe)
    // tg: code/modules/research/gizmo/gizmo_voice.dm:2  GLOBAL_LIST_INIT(gizmo_words, world.file2list("strings/gizmo_words.txt"))
    public DmList gizmo_words;

    // tg: code/modules/research/gizmo/gizmo_voice.dm:2  (GLOBAL_MANAGED(gizmo_words, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalgizmo_words()
    {
        gizmo_words = world.file2list("strings/gizmo_words.txt") as DmList;
        Dm.append(ref gvars_datum_init_order, "gizmo_words");
    }
}

/// Listen to the tones and send the sequence to the puzzle datum
// tg: code/modules/research/gizmo/gizmo_voice.dm:5
public partial class datum_component_gizmo_voice : datum_component
{
    /// GAP(types): tg's `puzzle` (:8)
    public datum_gizmo_puzzle puzzle_value;

    /// they're not really words but you get it (var/static/list/active_words)
    // tg: code/modules/research/gizmo/gizmo_voice.dm:10
    public static DmList active_words => TgState.Current.datum_component_gizmo_voice__active_words ??= Dm.list();

    // tg: code/modules/research/gizmo/gizmo_voice.dm:10
    [DmParams("puzzle")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        await DmTask.CompletedTask;
        datum_gizmo_puzzle puzzle = Dm.arg<datum_gizmo_puzzle>(__extra, 0);
        if (!ismovable(parent))
            return COMPONENT_INCOMPATIBLE;

        atom_movable movable = parent as atom_movable;
        movable.become_hearing_sensitive(Dm.text("[]", type));
        RegisterSignal(movable, COMSIG_MOVABLE_HEAR, nameof(on_hear));

        puzzle_value = puzzle;

        if (!Dm.truthy(active_words.len))
            generate_active_words();
        return null;
    }

    /// Pick the activate keywords
    // tg: code/modules/research/gizmo/gizmo_voice.dm:24
    public virtual void generate_active_words()
    {
        DmList possible_words = GLOB.gizmo_words.Copy();
        for (float i = 1, i__end = Dm.num(puzzle_value.cryptic_pulse.len); i <= i__end; i++)
        {
            DmList words = active_words;
            Dm.append(ref words, pick_n_take(possible_words));
        }
    }

    /// Listen to a message, and pick out the puzzle words
    // tg: code/modules/research/gizmo/gizmo_voice.dm:30
    [SignalHandler]
    public virtual void on_hear(datum source = null, DmList hearing_args = null)
    {
        if (hearing_args[HEARING_SPEAKER] == (object)parent)
            return;

        object haystack = hearing_args[HEARING_RAW_MESSAGE];
        DmList text_position_index = Dm.list();

        foreach (object needle in Dm.each(active_words))
        {
            float position = 1;
            // we can have multiple of the same keyword in one sequence
            for (float i = 1, i__end = puzzle_value.code_length; i <= i__end; i++)
            {
                position = findtext(haystack, needle, position);

                if (!Dm.truthy(position))
                    break;

                /// needle + position because assocs need to be unique, we splice it away later when sending it
                text_position_index[Dm.str(needle) + Dm.text("[]", position)] = position;
                // So for the next loop we dont find the exact same word again
                position++;
            }
        }

        if (!Dm.truthy(text_position_index.len))
            return;

        text_position_index = sortTim(text_position_index, associative: TRUE) as DmList;
        foreach ((object thing, object position) in Dm.pairs(text_position_index))
        {
            // Only send feedback for the last speech packet
            object no_feedback = Dm.num(!Dm.eq(text_position_index.Find(thing), text_position_index.len));
            // When solved, accept no further packets from this
            if (Dm.eq(Waiting.NoSleep(puzzle_value.on_pulse(active_words.Find(copytext(thing, 1, 3)), hearing_args[HEARING_SPEAKER] as mob_living, parent as atom_movable, no_feedback: Dm.num(no_feedback))), GIZMO_PUZZLE_SOLVED))
                return;
        }
    }
}

// The gizmo voice's static list of the round's keywords (rules 10.6).
public sealed partial class TgState
{
    // tg: code/modules/research/gizmo/gizmo_voice.dm:10
    internal DmList datum_component_gizmo_voice__active_words;
}
