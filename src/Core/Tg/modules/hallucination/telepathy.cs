// Translated from tgstation (snapshot 1d99bd7f): code/modules/hallucination/telepathy.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice G (D187): the whole file. The var block is generated (D111).

// tg: code/modules/hallucination/telepathy.dm:1
public partial class datum_hallucination_telepathy : datum_hallucination
{
    // tg: code/modules/hallucination/telepathy.dm:5
    public override async DmTask<object> start()
    {
        Type mimiced_type = pick(typesof(typeof(datum_action_cooldown_spell_list_target_telepathy))) as Type;
        hallucinator.balloon_alert(hallucinator, "you hear a voice");
        to_chat(hallucinator, Dm.text("<span class='[]'>You hear a voice in your head...</span><span class='[]'> []</span>",
            Dm.initial(mimiced_type, (datum_action_cooldown_spell_list_target_telepathy p) => p.bold_telepathy_span),
            Dm.initial(mimiced_type, (datum_action_cooldown_spell_list_target_telepathy p) => p.telepathy_span),
            get_telepath_message()));
        return TRUE;
    }

    // tg: code/modules/hallucination/telepathy.dm:15
    public virtual object get_telepath_message()
    {
        if (prob(0.001f))
            return "horse";

        object memo = pick(
            pick_list_replacements(HALLUCINATION_FILE, "advice"),
            pick_list_replacements(HALLUCINATION_FILE, "aggressive"),
            pick_list_replacements(HALLUCINATION_FILE, "conversation"),
            pick_list_replacements(HALLUCINATION_FILE, "didyouhearthat"),
            pick_list_replacements(HALLUCINATION_FILE, "doubt"),
            pick_list_replacements(HALLUCINATION_FILE, "escape"),
            pick_list_replacements(HALLUCINATION_FILE, "getout"),
            pick_list_replacements(HALLUCINATION_FILE, "greetings"),
            pick_list_replacements(HALLUCINATION_FILE, "suspicion"));
        object names = pick(
            first_name(hallucinator.name),
            last_name(hallucinator.name),
            first_name(hallucinator.real_name),
            last_name(hallucinator.real_name));

        return replacetext(memo, "%TARGETNAME%", names);
    }
}
