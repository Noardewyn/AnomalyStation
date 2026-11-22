// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/crates_lockers/crates/abandoned_crates/abandoned_crates.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST3 (D333): the file whole. The abandoned crate: a secure crate locked by a random code of four different digits, guessed
// by hand (tg's text input) with ten tries before its anti-tamper bomb; each wrong guess is answered by its bulls and cows, which the
// multitool shows in tg's AbandonedCrate window; the right code spawns one pick of tg's weighted loot table inside and unlocks it.
// `atom_deconstruct` is translated as tg has it, though tg's closet overrides handle_deconstruct without calling it. The var block
// is generated (D111).

// tg: code/game/objects/structures/crates_lockers/crates/abandoned_crates/abandoned_crates.dm:3
public partial class obj_structure_closet_crate_secure_loot : obj_structure_closet_crate_secure
{
    // tg: code/game/objects/structures/crates_lockers/crates/abandoned_crates/abandoned_crates.dm:100
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        code = generate_code(code_length);
        return dot;
    }

    /// Generates a random code of specified length with no repeating digits
    // tg: code/game/objects/structures/crates_lockers/crates/abandoned_crates/abandoned_crates.dm:105
    public virtual string generate_code(float length)
    {
        DmList digits = Dm.list("1", "2", "3", "4", "5", "6", "7", "8", "9", "0");
        DmList code_digits = Dm.list();

        for (float i = 1; i <= length; i++)
        {
            if (digits.Count == 0)
                break;
            object digit = pick(digits);
            Dm.append(code_digits, digit);
            Dm.remove(digits, digit); //there are never matching digits in the answer
        }

        return code_digits.Join("");
    }

    //ATTACK HAND IGNORING PARENT RETURN VALUE
    // tg: code/game/objects/structures/crates_lockers/crates/abandoned_crates/abandoned_crates.dm:119
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(locked))
            return await base.attack_hand(user, modifiers, __extra);
        if (!Dm.truthy(await user.can_perform_action(this)))
            return null;

        object input = await tgui_input_text(user, title: I18n.T("Deca-code lock"), message: I18n.T("Enter [] digits. All digits must be unique.", code_length), max_length: code_length);

        if (Dm.eq(input, code))
        {
            if (!Dm.truthy(spawned_loot))
                spawn_loot();
            tamperproof = 0; // set explosion chance to zero, so we dont accidently hit it with a multitool and instantly die
            await togglelock(user as mob_living);
            SStgui.close_user_uis(user, this);
            return null;
        }

        if (!validate_input(input))
        {
            to_chat(user, span_notice(I18n.T("You leave the crate alone.")));
            return null;
        }

        to_chat(user, span_warning(I18n.T("A red light flashes.")));
        Dm.append(previous_attempts, Dm.list(bulls_and_cows((string)input)));
        attempts--;

        if (attempts <= 0)
            await boom(user);
        return null;
    }

    /// Checks if user input is a valid code attempt
    // tg: code/game/objects/structures/crates_lockers/crates/abandoned_crates/abandoned_crates.dm:147
    public virtual bool validate_input(object input)
    {
        if (!Dm.truthy(input) || code_length != Dm.length(input))
            return false;

        string text = (string)input;
        DmList used_digits = Dm.list();
        for (int i = 1; i <= text.Length; i++)
        {
            string @char = text[i - 1].ToString();
            if (!(string.CompareOrdinal(@char, "0") >= 0 && string.CompareOrdinal(@char, "9") <= 0)) //if a non-digit is found, reject the input
                return false;
            if (Dm.@in(@char, used_digits)) //if a digit is repeated, reject the input
                return false;
            Dm.append(used_digits, @char);
        }

        return true;
    }

    // tg: code/game/objects/structures/crates_lockers/crates/abandoned_crates/abandoned_crates.dm:162
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        await attack_hand(user); //this helps you not blow up so easily by overriding unlocking which results in an immediate boom.
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/structures/crates_lockers/crates/abandoned_crates/abandoned_crates.dm:166
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        await base.ui_interact(user, ui, __extra);

        // Attempt to update tgui ui, open and update if needed.
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "AbandonedCrate", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/game/objects/structures/crates_lockers/crates/abandoned_crates/abandoned_crates.dm:175
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();

        data["previous_attempts"] = previous_attempts;
        data["attempts_left"] = attempts;

        return data;
    }

    // tg: code/game/objects/structures/crates_lockers/crates/abandoned_crates/abandoned_crates.dm:183
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(locked))
            return null;
        if (Dm.truthy(await Adjacent(user)))
            await ui_interact(user);

        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/crates_lockers/crates/abandoned_crates/abandoned_crates.dm:191
    public override async DmTask<object> multitool_act_secondary(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(locked))
            return null;
        await attack_hand(user);
        return ITEM_INTERACT_SUCCESS;
    }

    /// Implements bulls and cows algorithm to compare guess against actual code
    // tg: code/game/objects/structures/crates_lockers/crates/abandoned_crates/abandoned_crates.dm:198
    public virtual DmList bulls_and_cows(string guess)
    {
        float bulls = 0;
        float cows = 0;
        string code_text = (string)code;

        for (int i = 1; i <= code_length; i++)
        {
            string guess_char = guess[i - 1].ToString();
            string code_char = code_text[i - 1].ToString();

            if (guess_char == code_char)
                bulls++;
            else if (Dm.truthy(findtext(code_text, guess_char)))
                cows++;
        }

        return Dm.list(Dm.kv("attempt", guess), Dm.kv("bulls", bulls), Dm.kv("cows", cows));
    }

    // tg: code/game/objects/structures/crates_lockers/crates/abandoned_crates/abandoned_crates.dm:213
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        object dot = await base.emag_act(user, emag_card, __extra);

        if (Dm.truthy(locked))
        {
            await boom(user); // no feedback since it just explodes, thats its own feedback
            return TRUE;
        }
        return dot;
    }

    // tg: code/game/objects/structures/crates_lockers/crates/abandoned_crates/abandoned_crates.dm:221
    public override async DmTask<object> togglelock(mob_living user = null, object silent = null)
    {
        silent ??= FALSE;
        if (!Dm.truthy(locked))
        {
            object dot = await base.togglelock(user, silent); //Run the normal code.
            if (Dm.truthy(locked)) //Double check if the crate actually locked itself when the normal code ran.
            {
                //reset the anti-tampering, number of attempts and last attempt when the lock is re-enabled.
                tamperproof = Dm.num(Dm.initial(this, nameof(tamperproof)));
                attempts = Dm.num(Dm.initial(this, nameof(attempts)));
                previous_attempts = Dm.list();
            }
            return dot;
        }
        if (Dm.truthy(tamperproof))
            return null;
        return await base.togglelock(user, silent);
    }

    // tg: code/game/objects/structures/crates_lockers/crates/abandoned_crates/abandoned_crates.dm:234
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        if (Dm.truthy(locked))
        {
            await boom();
            return;
        }
        await base.atom_deconstruct(disassembled ?? TRUE);
    }

    // tg: code/game/objects/structures/crates_lockers/crates/abandoned_crates/abandoned_crates.dm:240
    public override async DmTask after_open(mob_living user = null, float? force = null)
    {
        await base.after_open(user, force);
        if (Dm.truthy(qdel_on_open))
            qdel(this);
    }

    // tg: code/game/objects/structures/crates_lockers/crates/abandoned_crates/abandoned_crates.dm:245
    public virtual void spawn_loot()
    {
        object loot = pick_weight_recursive(possible_loot);
        Dm.@new((Type)loot, this);
        spawned_loot = TRUE;
    }
}
