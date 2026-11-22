// Translated from tgstation (snapshot 1d99bd7f): code/modules/language/common.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: /datum/language/common's var overrides except syllables (:21-58) and mutual_understanding (:61-64), which only
// speech reads (scrambling and understanding other languages, M9), and which name language types M2 does not declare
// (/datum/language/beachbum, /datum/language/uncommon). Common always uses the default human namelist
// (always_use_default_namelist), so a random name does not read the syllables either (get_random_name, _language.dm:157).

// 'basic' language; spoken by default.
// tg: code/modules/language/common.dm:2
[DmPath("/datum/language/common")]
public partial class datum_language_common : datum_language
{
    // tg: code/modules/language/common.dm:3-18
    public datum_language_common()
    {
        name = "Galactic Common";
        desc = "The common galactic tongue.";
        key = "0";
        flags = Dm.bitor(LANGUAGE_TONGUELESS_SPEECH, LANGUAGE_HIDE_ICON_IF_UNDERSTOOD);
        default_priority = 100;
        space_chance = 20;
        sentence_chance = 0;
        between_word_sentence_chance = 10;
        between_word_space_chance = 75;
        additional_syllable_low = 0;
        additional_syllable_high = 0;

        icon_state = "galcom";
        // Default namelist is the human namelist, and common is the human language, so might as well.
        // Feel free to remove this at some point because common can generate some pretty cool names.
        always_use_default_namelist = TRUE;
    }
}
