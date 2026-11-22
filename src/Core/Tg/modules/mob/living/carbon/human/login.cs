// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/login.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (D310): the species' on_owner_login is not called (its body is not translated, _species.cs).

public partial class mob_living_carbon_human
{
    // tg: code/modules/mob/living/carbon/human/login.dm:1
    public override async DmTask<object> Login()
    {
        object dot = await base.Login();

        // tg: :4  dna?.species?.on_owner_login(src): not translated

        if (Dm.truthy(SStts?.tts_enabled) && !Dm.truthy(voice))
            voice = SStts.random_tts_voice(gender);

        if (!Dm.truthy(LAZYLEN(afk_thefts)))
            return dot;

        DmList print_msg = Dm.list();
        print_msg.Add(span_userdanger("As you snap back to consciousness, you recall people messing with your stuff..."));

        afk_thefts = (DmList)reverse_range(afk_thefts);

        foreach (object entry in Dm.each(afk_thefts))
        {
            if (entry is not DmList iter_theft || Dm.num(LAZYLEN(iter_theft)) != AFK_THEFT_TIME)
            {
                stack_trace(Dm.text("[] ([]) returned to their body and had a null/malformed afk_theft entry. Contents: []", this, ckey, json_encode(entry)), "code/modules/mob/living/carbon/human/login.dm", 16);
                continue;
            }

            object thief_name = iter_theft[AFK_THEFT_NAME];
            object theft_message = iter_theft[AFK_THEFT_MESSAGE];
            float time_since = Dm.num(world.time) - Dm.num(iter_theft[AFK_THEFT_TIME]);

            if (time_since > AFK_THEFT_FORGET_DETAILS_TIME)
                print_msg.Add(Dm.text("\t[]", span_danger(Dm.text("<b>Someone [], but it was at least [] ago.</b>", theft_message, DisplayTimeText(AFK_THEFT_FORGET_DETAILS_TIME)))));
            else
                print_msg.Add(Dm.text("\t[]", span_danger(Dm.text("<b>[] [] roughly [] ago.</b>", thief_name, theft_message, DisplayTimeText(time_since, 10)))));
        }

        if (Dm.num(LAZYLEN(afk_thefts)) >= AFK_THEFT_MAX_MESSAGES)
            print_msg.Add(span_warning("There may have been more, but that's all you can remember..."));

        to_chat(this, boxed_message(print_msg.Join("\n")));
        LAZYNULL(ref afk_thefts);
        return dot;
    }
}
