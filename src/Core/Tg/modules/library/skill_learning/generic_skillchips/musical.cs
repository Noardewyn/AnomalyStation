// Translated from tgstation (snapshot 1d99bd7f): code/modules/library/skill_learning/generic_skillchips/musical.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SL1 (D333): the whole file. The musical chip: its user sings, holding the last vowel of each line. The var block is generated
// (D111).

// tg: code/modules/library/skill_learning/generic_skillchips/musical.dm:1
public partial class obj_item_skillchip_musical : obj_item_skillchip
{
    // tg: code/modules/library/skill_learning/generic_skillchips/musical.dm:12
    [DmParams("mapload", "is_removable")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        name = replacetext(name, "Old", round(CURRENT_STATION_YEAR - Dm.num(pick(50, 100, 150, 200, 250)), 5));
        return dot;
    }

    // tg: code/modules/library/skill_learning/generic_skillchips/musical.dm:16
    public override async DmTask on_activate(mob_living_carbon user = null, float? silent = null)
    {
        silent ??= FALSE;
        await base.on_activate(user, silent);
        RegisterSignal(user, COMSIG_MOB_SAY, nameof(make_music));
    }

    // tg: code/modules/library/skill_learning/generic_skillchips/musical.dm:20
    public override async DmTask on_deactivate(mob_living_carbon user = null, float? silent = null)
    {
        await base.on_deactivate(user, silent);
        UnregisterSignal(user, COMSIG_MOB_SAY);
    }

    // tg: code/modules/library/skill_learning/generic_skillchips/musical.dm:24
    [SignalHandler]
    public virtual void make_music(mob_living_carbon source = null, DmList say_args = null)
    {
        object raw_message = say_args[SPEECH_MESSAGE];
        DmList words = splittext(raw_message, " ");
        if (length(words) <= 1)
        {
            ((DmList)say_args[SPEECH_MODS])[MODE_SING] = TRUE;
            return;
        }
        object last_word = words[length(words)];
        float num_chars = length_char(last_word);
        string last_vowel = "";
        // find the last vowel present in the word
        for (float i = 1; i <= num_chars; i++)
        {
            string @char = copytext_char(last_word, i, i + 1);
            if (Dm.@in(@char, VOWELS()))
                last_vowel = @char;
        }

        // now we'll reshape the final word to make it sound like they're singing it
        string final_word = "";
        bool has_ellipsis = Dm.eq(copytext(last_word, -3), "...");
        for (float i = 1; i <= num_chars; i++)
        {
            string @char = copytext_char(last_word, i, i + 1);
            // replacing any final periods with exclamation marks (so long as it's not an ellipsis)
            if (@char == "." && i == num_chars && !has_ellipsis)
                final_word += "!";
            // or if it's the vowel we found, we're gonna repeat it a few times (holding the note)
            else if (@char == last_vowel)
            {
                for (float j = 1; j <= 4; j++)
                    final_word += @char;
                // if we dragged out the last character of the word, just period it
                if (i == num_chars)
                    final_word += ".";
            }
            // no special handing otherwise
            else
                final_word += @char;
        }

        if (!has_ellipsis)
        {
            // adding an extra exclamation mark at the end if there's no period
            string last_char = copytext_char(final_word, -1);
            if (last_char != ".")
                final_word += "!";
        }

        words[length(words)] = final_word;
        // now we siiiiiiing
        say_args[SPEECH_MESSAGE] = jointext(words, " ");
        ((DmList)say_args[SPEECH_MODS])[MODE_SING] = TRUE;
    }

    // tg: code/modules/library/skill_learning/generic_skillchips/musical.dm:71
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_tinynoticeital(I18n.T("Huh, looks like it'd fit in a skillchip adapter.")));
        return dot;
    }

    // tg: code/modules/library/skill_learning/generic_skillchips/musical.dm:75
    public override async DmTask<object> examine_more(mob user = null)
    {
        object dot = await base.examine_more(user);
        DmList songs = Dm.list();
        Dm.append(ref songs, I18n.T("&bull; \"The Ballad of Space Station 13\""));
        Dm.append(ref songs, I18n.T("&bull; \"The Captain's Call\""));
        Dm.append(ref songs, I18n.T("&bull; \"A Mime's Lament\""));
        Dm.append(ref songs, I18n.T("&bull; \"Banned from Cargo\""));
        Dm.append(ref songs, I18n.T("&bull; \"Botany Blues\""));
        Dm.append(ref songs, I18n.T("&bull; \"Clown Song\""));
        Dm.append(ref songs, I18n.T("&bull; \"Elegy to an Engineer\""));
        Dm.append(ref songs, I18n.T("&bull; \"Medical Malpractitioner\""));
        Dm.append(ref songs, I18n.T("&bull; \"Security Strike\""));
        Dm.append(ref songs, I18n.T("&bull; \"Send for the Shuttle\""));
        Dm.append(ref songs, I18n.T("&bull;  And one song scratched out..."));

        Dm.append(ref dot, span_notice(I18n.T("<i>On the back of the chip, you see a list of songs:</i>")));
        Dm.append(ref dot, span_smallnotice(I18n.T("<i>[]</i>", jointext(songs, "<br>"))));
        return dot;
    }
}
