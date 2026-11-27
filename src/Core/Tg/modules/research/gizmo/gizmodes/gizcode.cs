// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/gizmo/gizmodes/gizcode.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the code-cracking gizmo modes (the tutorial one with its over and under pings, its cheese and the hard-mode gizmo
// it gives once, and the hard-mode "moo" one with its unique digits, bulls and cows and its minibomb punishment), and the four
// pulses that start the puzzle, move the head, turn the knob and try the code, each with its sounds. The var blocks are generated
// (D111).

/** A code-cracking puzzle gizmo
 * 	First, the user must pulse activate_puzzle - this randomizes the code and resets the attempt counter.
 * 	The user may pulse activate_puzzle a limited amount of times again to retry it.
 *
 * 	Next, the user needs to input a code.
 * 	Imagine a row of knobs, each of them with 10 notches, and a head that rotates the knobs.
 *  Pulsing cycle_position moves the head right,
 * 	pulsing cycle_digit makes the head bump the knob by 1 notch.
 *  If the head reaches the rightmost position, then cycle_position resets it, moving it back left.
 *  Similarly, pulsing cycle_digit when the knob is already in the rightmost notch resets it to the leftmost one.
 *
 * 	Finally, the user has to pulse try_crack to check if their input is correct.
 * 	If it's correct, a reward is dispensed and the puzzle gets deactivated.
 *  If it's not, some sort of feedback is provided.
 * 	If the user takes too many attempts to solve the code, they are punished.
 *
 * 	There may be restrictions set upon the code.
 * 	These are checked upon code generation and cracking attempt.
 */
// tg: code/modules/research/gizmo/gizmodes/gizcode.dm:24
public partial class datum_gizmodes_code_crack : datum_gizmodes
{
    // How many times we try to generate the code
    // tg: code/modules/research/gizmo/gizmodes/gizcode.dm:2  #define MAX_CODEGEN_RETRY_ATTEMPTS 5 (file-local)
    private const float MAX_CODEGEN_RETRY_ATTEMPTS = 5;
    // There are 10 digits
    // tg: code/modules/research/gizmo/gizmodes/gizcode.dm:4  #define DIGIT_COUNT 10 (file-local)
    internal const float DIGIT_COUNT = 10;

    // Proc that generates the code (solution to the puzzle)
    // We retry generation if the code happened to be invalid
    // Returns TRUE if generation was successful and FALSE otherwise
    // tg: code/modules/research/gizmo/gizmodes/gizcode.dm:59
    [ShouldCallParent]
    public virtual object generate_code()
    {
        solution.Cut();
        solution.len = code_length;
        for (float i = 1, i__end = MAX_CODEGEN_RETRY_ATTEMPTS; i <= i__end; i++)
        {
            for (float j = 1, j__end = code_length; j <= j__end; j++)
                solution[j] = rand(0, 9); // Randomize every digit
            if (Dm.truthy(validate_code(solution)))
                return TRUE;
        }
        return FALSE;
    }

    // Proc that checks if code is valid or not (matches the restrictions)
    // tg: code/modules/research/gizmo/gizmodes/gizcode.dm:71
    public virtual object validate_code(DmList code = null)
    {
        // Restrictions are defined by the subtype
        return TRUE;
    }

    // Proc that checks if user input matches the solution
    // tg: code/modules/research/gizmo/gizmodes/gizcode.dm:76
    public virtual object check_code()
    {
        for (float i = 1, i__end = code_length; i <= i__end; i++)
        {
            if (!Dm.eq(code_input[i], solution[i]))
                return FALSE;
        }
        return TRUE;
    }

    // Proc to dispense the reward from the loot table
    // tg: code/modules/research/gizmo/gizmodes/gizcode.dm:83
    [ShouldCallParent]
    public virtual async DmTask dispense_reward(atom_movable holder = null)
    {
        await DmTask.CompletedTask;
        object loot = pick_weight_recursive(loot_table);
        Dm.@new(loot as Type, get_turf(holder));
        playsound(holder, "sound/machines/machine_vend.ogg", 100);
    }

    // Proc that punishes the user when they go over the attempt limit
    // Technically, user can try to crack the code as many times as they want, as long as they can endure the punishment
    // tg: code/modules/research/gizmo/gizmodes/gizcode.dm:91
    public virtual async DmTask punishment(atom_movable holder = null)
    {
        await DmTask.CompletedTask;
        // Punishment has to be defined by the subtype
        return;
    }

    // Proc that produces feedback when the user inputs an incorrect code
    // By default, all of these gizmos tell the user how many attempts are left
    // tg: code/modules/research/gizmo/gizmodes/gizcode.dm:97
    [ShouldCallParent]
    public virtual async DmTask feedback(atom_movable holder = null)
    {
        // This is kind of ass, but there's probably no way around it
        DmList digit_to_name = TgState.Current.datum_gizmodes_code_crack__feedback__digit_to_name ??= Dm.list("one", "two", "three", "four", "five", "six", "seven", "eight", "nine"); // var/static/list/digit_to_name
        if (attempts_left <= 0 || attempts_left >= 10)
            return;
        playsound(holder, Dm.text("sound/announcer/vox_fem/[].ogg", digit_to_name[attempts_left]), 100);
        await holder.say(digit_to_name[attempts_left]);
    }

    // Proc that resets user input
    // tg: code/modules/research/gizmo/gizmodes/gizcode.dm:107
    public virtual void reset_input()
    {
        code_input.Cut();
        code_input.len = code_length; // stretch it
        for (float i = 1, i__end = code_length; i <= i__end; i++)
            code_input[i] = 0; // Fill it with zeroes
        position = Dm.num_or_null(Dm.initial(this, nameof(position)));
    }
}

// Gizpulses

// Gizpulse to activate the puzzle
// tg: code/modules/research/gizmo/gizmodes/gizcode.dm:117
public partial class datum_gizpulse_activate_puzzle : datum_gizpulse
{
    // tg: code/modules/research/gizmo/gizmodes/gizcode.dm:117
    public override async DmTask activate(atom_movable holder = null, datum_gizmodes master = null, datum_gizmo_interface @interface = null)
    {
        await DmTask.CompletedTask;
        datum_gizmodes_code_crack puzzle_holder = master as datum_gizmodes_code_crack;
        if (puzzle_holder == null)
            return;
        // If the puzzle cannot be retried, produce a bad buzz and stop
        if (puzzle_holder.puzzles_left <= 0)
        {
            playsound(holder, "sound/machines/uplink/uplinkerror.ogg", 100);
            return;
        }
        // If it can be tried again (or is launched for the first time), do what we gotta do
        if (!Dm.truthy(puzzle_holder.generate_code())) // Code generation may fail, if the restrictions are too severe
        {
            playsound(holder, "sound/items/ceramic_break.ogg", 100);
            return;
        }
        playsound(holder, puzzle_holder.init_jingle, 100);
        puzzle_holder.puzzles_left--;
        puzzle_holder.active = TRUE;
        puzzle_holder.reset_input();
        puzzle_holder.attempts_left = Dm.num(Dm.initial(puzzle_holder, nameof(puzzle_holder.attempts_left)));
    }
}

// Gizpulse to cycle the currently selected position
// Example (if code_input is 0000):
// 0000 -> 0000
// ^		^
// tg: code/modules/research/gizmo/gizmodes/gizcode.dm:139
public partial class datum_gizpulse_cycle_position : datum_gizpulse
{
    // tg: code/modules/research/gizmo/gizmodes/gizcode.dm:139
    public override async DmTask activate(atom_movable holder = null, datum_gizmodes master = null, datum_gizmo_interface @interface = null)
    {
        await DmTask.CompletedTask;
        datum_gizmodes_code_crack puzzle_holder = master as datum_gizmodes_code_crack;
        if (puzzle_holder == null)
            return;
        if (!Dm.truthy(puzzle_holder.active)) // If the puzzle is inactive, produce a loud buzz and get out
        {
            playsound(holder, "sound/machines/scanner/scanbuzz.ogg", 100);
            return;
        }
        // Cycle position: 0 -> 1 -> 2 -> .. -> (code_length - 1) -> reset back to 0
        puzzle_holder.position = (Dm.num(puzzle_holder.position) + 1) % puzzle_holder.code_length;

        // If we simply bumped the position by 1, produce a single piston-move sound
        if (Dm.num(puzzle_holder.position) != 0)
        {
            playsound(holder, "sound/machines/eject.ogg", 100);
            return;
        }
        // Otherwise, produce a different sound, indicating the position has been reset
        playsound(holder, "sound/items/weapons/autoguninsert.ogg", 100);
    }
}

// Gizpulse to cycle the currently selected digit
// Example (if second digit is selected and code_input is 0000):
//	0000 -> 0100
//   ^		 ^
// tg: code/modules/research/gizmo/gizmodes/gizcode.dm:160
public partial class datum_gizpulse_cycle_digit : datum_gizpulse
{
    // tg: code/modules/research/gizmo/gizmodes/gizcode.dm:160
    public override async DmTask activate(atom_movable holder = null, datum_gizmodes master = null, datum_gizmo_interface @interface = null)
    {
        await DmTask.CompletedTask;
        datum_gizmodes_code_crack puzzle_holder = master as datum_gizmodes_code_crack;
        if (puzzle_holder == null)
            return;
        if (!Dm.truthy(puzzle_holder.active)) // If the puzzle is inactive, produce a loud buzz and get out
        {
            playsound(holder, "sound/machines/scanner/scanbuzz.ogg", 100);
            return;
        }

        // List indices start with 1, so we add 1 here
        float position = Dm.num(puzzle_holder.position) + 1;
        float previous_digit = Dm.num(puzzle_holder.code_input[position]);
        // Cycle the digit
        puzzle_holder.code_input[position] = (previous_digit + 1) % datum_gizmodes_code_crack.DIGIT_COUNT;

        // If we simply bumped the digit by 1, produce a single click
        if (previous_digit != datum_gizmodes_code_crack.DIGIT_COUNT - 1)
        {
            playsound(holder, "sound/machines/creak.ogg", 100);
            return;
        }
        // Otherwise, produce a different sound, indicating the digit has been reset
        playsound(holder, "sound/items/reel/reel4.ogg", 100);
    }
}

// Gizpulse that actually cracks the code
// tg: code/modules/research/gizmo/gizmodes/gizcode.dm:182
public partial class datum_gizpulse_try_crack : datum_gizpulse
{
    // tg: code/modules/research/gizmo/gizmodes/gizcode.dm:182
    public override async DmTask activate(atom_movable holder = null, datum_gizmodes master = null, datum_gizmo_interface @interface = null)
    {
        datum_gizmodes_code_crack puzzle_holder = master as datum_gizmodes_code_crack;
        if (puzzle_holder == null)
            return;
        if (!Dm.truthy(puzzle_holder.active)) // If the puzzle is inactive, produce a loud buzz and get out
        {
            playsound(holder, "sound/machines/scanner/scanbuzz.ogg", 100);
            return;
        }

        // If the input is invalid, emit an invalid-input sound and let the user make corrections
        object validity = puzzle_holder.validate_code(puzzle_holder.code_input);
        if (!Dm.truthy(validity))
        {
            playsound(holder, "sound/machines/terminal/terminal_error.ogg", 100);
            return;
        }

        // If the input is correct, dispense a reward and reset the puzzle
        object correctness = puzzle_holder.check_code();
        if (Dm.truthy(correctness))
        {
            await puzzle_holder.dispense_reward(holder);
            puzzle_holder.active = FALSE;
            return;
        }

        // If the input is incorrect..
        puzzle_holder.attempts_left--;
        if (puzzle_holder.attempts_left <= 0)
            await puzzle_holder.punishment(holder);
        await puzzle_holder.feedback(holder);
        puzzle_holder.reset_input();
        // Play the input reset sound
        playsound(holder, "sound/machines/terminal/terminal_eject.ogg", 100);
    }
}

// Tutorial version
// Restrictions: none
// Code length: 2
// Feedback: over/under
// Punishment: evil rat
// Loot: cheese
// Also, dispenses a hard-mode code-crack gizmo upon completion
// tg: code/modules/research/gizmo/gizmodes/gizcode.dm:219
public partial class datum_gizmodes_code_crack_tutorial : datum_gizmodes_code_crack
{
    // tg: code/modules/research/gizmo/gizmodes/gizcode.dm:236
    public override async DmTask dispense_reward(atom_movable holder = null)
    {
        if (!Dm.truthy(dispensed_hardmode))
        {
            dispensed_hardmode = TRUE;
            // Hard-mode
            Dm.@new<obj_item_gizmo_moo>(get_turf(holder));
        }
        await base.dispense_reward(holder);
    }

    // tg: code/modules/research/gizmo/gizmodes/gizcode.dm:243
    public override async DmTask feedback(atom_movable holder = null)
    {
        for (float i = 1, i__end = code_length; i <= i__end; i++)
        {
            if (Dm.lt(code_input[i], solution[i]))
            {
                playsound(holder, "sound/machines/defib/defib_saftyOff.ogg", 100);
                holder.visible_message(span_notice(I18n.T("[] pings low.", holder)));
            }
            else if (Dm.gt(code_input[i], solution[i]))
            {
                playsound(holder, "sound/machines/defib/defib_saftyOn.ogg", 100);
                holder.visible_message(span_notice(I18n.T("[] pings high.", holder)));
            }
            else
            {
                playsound(holder, "sound/machines/defib/defib_ready.ogg", 100);
                holder.visible_message(span_notice(I18n.T("[] pings affirmatively.", holder)));
            }
            await sleep(0.5f * SECONDS);
        }
        await base.feedback(holder);
    }

    // tg: code/modules/research/gizmo/gizmodes/gizcode.dm:257
    public override async DmTask punishment(atom_movable holder = null)
    {
        await DmTask.CompletedTask;
        // Evil rat
        Dm.@new<mob_living_basic_mouse_rat>(get_turf(holder));
    }
}

// Hardmode
// Restrictions: all digits must be unique
// Code length: 2-3
// Feedback: bulls and cows (number of correctly placed digits, number of incorrectly placed digits that are included in the code)
// Punishment: explosion
// Loot: all sorts of stuff
// tg: code/modules/research/gizmo/gizmodes/gizcode.dm:267
public partial class datum_gizmodes_code_crack_moo : datum_gizmodes_code_crack
{
    // All digits must be unique
    // tg: code/modules/research/gizmo/gizmodes/gizcode.dm:275
    public override object validate_code(DmList code = null)
    {
        for (float i = 1, i__end = code_length; i <= i__end; i++)
        {
            for (float j = 1, j__end = i - 1; j <= j__end; j++)
            {
                if (Dm.eq(code[i], code[j]))
                    return FALSE;
            }
        }
        return TRUE;
    }

    // tg: code/modules/research/gizmo/gizmodes/gizcode.dm:282
    public override object generate_code()
    {
        code_length = rand(min_code_length, max_code_length);
        return base.generate_code();
    }

    // tg: code/modules/research/gizmo/gizmodes/gizcode.dm:286
    public override async DmTask feedback(atom_movable holder = null)
    {
        float bulls = 0;
        float cows = 0;
        for (float i = 1, i__end = code_length; i <= i__end; i++)
        {
            for (float j = 1, j__end = code_length; j <= j__end; j++)
            {
                if (Dm.eq(code_input[i], solution[j]))
                {
                    if (i == j) 	// Digit is correct and correctly placed
                        bulls++;
                    else	 	// Digit is correct, but incorrectly placed
                        cows++;
                    break;
                }
            }
        }
        // Bull beeps, cow beeps and the sound from parent call shouldn't play simultaneously, so sleep() is probably unavoidable here
        for (float i = 1, i__end = bulls; i <= i__end; i++)
        {
            playsound(holder, "sound/machines/synth/synth_yes.ogg", 100);
            await sleep(0.25f * SECONDS);
        }
        for (float i = 1, i__end = cows; i <= i__end; i++)
        {
            playsound(holder, "sound/machines/synth/synth_no.ogg", 100);
            await sleep(0.25f * SECONDS);
        }

        holder.visible_message(span_notice(I18n.T("[] emits [] high-pitched beeps and [] low-pitched ones.", holder, bulls, cows)));

        await base.feedback(holder);
    }

    // tg: code/modules/research/gizmo/gizmodes/gizcode.dm:309
    public override async DmTask punishment(atom_movable holder = null)
    {
        obj_item_grenade_syndieminibomb punishment = Dm.@new<obj_item_grenade_syndieminibomb>(get_turf(holder));
        await punishment.arm_grenade(null, 5 * SECONDS);
        qdel(holder);
    }
}

// The code-crack gizmo's proc-level static (rules 10.6).
public sealed partial class TgState
{
    // tg: code/modules/research/gizmo/gizmodes/gizcode.dm:99
    internal DmList datum_gizmodes_code_crack__feedback__digit_to_name;
}
