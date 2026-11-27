// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/gizmo/gizpuzzle.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the gizmo puzzle. It makes a random code sequence (three pulses of its eight, two for the voice) for each callback
// of the gizmo's modes, follows the pulses it receives, calls a callback when its sequence is complete and resets on a wrong pulse,
// and gives the feedback: a buzz, a ping, a creak or a clunk with their sounds, at most every 0.2 seconds. The var blocks are
// generated (D111).

/// Holds the puzzle sequences, receives the pulses, decides if theyre correct, and gives feedback and calls the right callbacks when it does
// tg: code/modules/research/gizmo/gizpuzzle.dm:2
public partial class datum_gizmo_puzzle : datum
{
    // tg: code/modules/research/gizmo/gizpuzzle.dm:31
    [DmParams("pulsed")]
    public override async DmTask New(params object[] __extra)
    {
        datum_callback pulsed = Dm.arg<datum_callback>(__extra, 0);
        pulsed_callback = pulsed ?? Dm.@new<datum_callback>(this, nameof(default_on_pulsed));
        await base.New();
    }

    /// Make up a sequence
    // tg: code/modules/research/gizmo/gizpuzzle.dm:36
    public virtual void generate_code_sequences(DmList solution_callbacks = null)
    {
        this.solution_callbacks = solution_callbacks;
        code_sequences = Dm.list();

        for (float i = 1, i__end = Dm.num(solution_callbacks.len); i <= i__end; i++)
        {
            Dm.append(ref code_sequences, Dm.list(Dm.list()));
            for (float j = 1, j__end = code_length; j <= j__end; j++)
                Dm.append_at(code_sequences, i, pick(cryptic_pulse));
        }
    }

    /// Whenever a puzzle attempt is made
    // tg: code/modules/research/gizmo/gizpuzzle.dm:46
    public virtual async DmTask<object> on_pulse(object pulse_number = null, mob_living user = null, atom_movable holder = null, float? no_feedback = null)
    {
        no_feedback ??= FALSE;
        Dm.append(ref current_sequence, cryptic_pulse[pulse_number]);
        object dot = GIZMO_PUZZLE_CORRECT;

        bool succeeded = false;

        for (float i = 1, i__end = Dm.num(code_sequences.len); i <= i__end; i++)
        {
            DmList a = code_sequences[i] as DmList;
            for (float j = 1, j__end = Dm.num(current_sequence.len); j <= j__end; j++)
            {
                if (!Dm.eq(current_sequence[j], a[j]))
                    break;
                if (Dm.num(current_sequence.len) == j)
                    succeeded = true;
                if (j == Dm.num(a.len))
                {
                    datum_callback callback = solution_callbacks[i] as datum_callback;
                    object or__1 = await callback.Invoke(holder);
                    dot = Dm.truthy(or__1) ? or__1 : GIZMO_PUZZLE_SOLVED;
                    current_sequence.Cut();

                    break;
                }
            }
        }

        if (!succeeded)
        {
            current_sequence.Cut();
            dot = GIZMO_PUZZLE_WRONG;
        }

        if (pulsed_callback != null)
            await pulsed_callback.Invoke(holder, user, dot, no_feedback);
        return dot;
    }

    /// Just some feedback so people can start forcing sequences. No feedback if it's done automatically
    // tg: code/modules/research/gizmo/gizpuzzle.dm:73
    public virtual void default_on_pulsed(atom_movable holder = null, mob_living user = null, object solved_type = null, float? no_feedback = null)
    {
        no_feedback ??= FALSE;
        if (!(feedback_cooldown <= world.time) || Dm.truthy(no_feedback)) // COOLDOWN_FINISHED(src, feedback_cooldown), inline (rules 11.1 (a))
            return;

        feedback_cooldown = world.time + feedback_cooldown_time; // COOLDOWN_START(src, feedback_cooldown, feedback_cooldown_time), inline

        object sw__1 = solved_type;
        if (Dm.eq(sw__1, GIZMO_PUZZLE_WRONG))
        {
            holder.balloon_alert_to_viewers(I18n.T("buzz"));
            playsound(holder, "sound/machines/buzz/buzz-sigh.ogg", 30, FALSE);
        }
        else if (Dm.eq(sw__1, GIZMO_PUZZLE_CORRECT))
        {
            holder.balloon_alert_to_viewers(I18n.T("ping"));
            playsound(holder, "sound/machines/ping.ogg", 30, FALSE);
        }
        else if (Dm.eq(sw__1, GIZMO_PUZZLE_SOLVED))
        {
            holder.balloon_alert_to_viewers(I18n.T("creak"));
            playsound(holder, "sound/machines/creak.ogg", 30, FALSE);
        }
        else if (Dm.eq(sw__1, GIZMO_PUZZLE_SOLVED_MODE_CONTROL))
        {
            holder.balloon_alert_to_viewers(I18n.T("clunk"));
            playsound(holder, "sound/machines/machine_vend.ogg", 30, FALSE);
        }
    }
}
