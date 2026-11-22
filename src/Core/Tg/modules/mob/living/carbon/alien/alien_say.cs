// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/alien/alien_say.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps X5 (D333): the whole file, the alien hivemind (the xeno say mode, datums/saymode.cs, calls alien_talk).

// tg: code/modules/mob/living/carbon/alien/alien_say.dm:1 (interleaved types, rules 1.9)
public partial class mob_living : mob
{
    // tg: code/modules/mob/living/carbon/alien/alien_say.dm:1
    public virtual void alien_talk(object message = null, DmList spans = null, DmList message_mods = null, object shown_name = null, float? big_voice = null)
    {
        spans ??= Dm.list();
        message_mods ??= Dm.list();
        shown_name ??= real_name;
        big_voice ??= FALSE;
        log_sayverb_talk(message, message_mods, tag: "alien hivemind");
        message = trim(message);
        if (!Dm.truthy(message))
            return;

        object message_a = generate_messagepart(message, spans, message_mods);
        string hivemind_spans = "alien";
        if (Dm.truthy(big_voice))
            hivemind_spans += " big";
        string rendered = I18n.T("<i><span class='[]'>Hivemind, [] <span class='message'>[]</span></span></i>", hivemind_spans, span_name(Dm.text("[]", shown_name)), message_a);
        foreach (mob player in Dm.each<mob>(GLOB.player_list))
        {
            if (!IS_UNCONSCIOUS_OR_CRIT(player) && Dm.truthy(player.hivecheck()))
                to_chat(player, rendered, type: MESSAGE_TYPE_RADIO, avoid_highlighting: Dm.num(player == this));
            else if (Dm.@in(player, GLOB.dead_mob_list))
            {
                string link = FOLLOW_LINK(player, this);
                to_chat(player, Dm.text("[] []", link, rendered), type: MESSAGE_TYPE_RADIO);
            }
        }
    }
}

// tg: code/modules/mob/living/carbon/alien/alien_say.dm:19
public partial class mob_living_carbon_alien_adult_royal_queen : mob_living_carbon_alien_adult_royal
{
    // tg: code/modules/mob/living/carbon/alien/alien_say.dm:19
    public override void alien_talk(object message = null, DmList spans = null, DmList message_mods = null, object shown_name = null, float? big_voice = null)
    {
        spans ??= Dm.list();
        message_mods ??= Dm.list();
        shown_name ??= name;
        big_voice ??= TRUE;
        base.alien_talk(message, spans, message_mods, shown_name, TRUE);
    }
}

// tg: code/modules/mob/living/carbon/alien/alien_say.dm:22
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/living/carbon/alien/alien_say.dm:22
    public override object hivecheck()
    {
        obj_item_organ_alien_hivenode N = get_organ_by_type(typeof(obj_item_organ_alien_hivenode)) as obj_item_organ_alien_hivenode;
        if (N != null && !Dm.truthy(N.recent_queen_death)) //Mob has alien hive node and is not under the dead queen special effect.
            return TRUE;
        return null;
    }
}
