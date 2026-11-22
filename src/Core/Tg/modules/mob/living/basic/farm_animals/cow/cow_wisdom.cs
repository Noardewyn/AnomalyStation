// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/farm_animals/cow/cow_wisdom.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4; the experience it gives came with gaps SL1, D333).

// tg: code/modules/mob/living/basic/farm_animals/cow/cow_wisdom.dm:3
public partial class mob_living_basic_cow_wisdom : mob_living_basic_cow
{
    // tg: code/modules/mob/living/basic/farm_animals/cow/cow_wisdom.dm:13
    [DmParams("mapload", "granted_wisdom", "granted_experience", "milked_reagent")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object granted_wisdom = Dm.arg(__extra, 0);
        object granted_experience = Dm.arg(__extra, 1) ?? 500f;
        this.milked_reagent = Dm.arg(__extra, 2) as Type;
        object dot = base.Initialize(mapload);
        (ai_controller as datum_ai_controller)?.set_blackboard_key(BB_BASIC_MOB_SPEAK_LINES, Dm.list(Dm.kv(BB_EMOTE_SAY, GLOB.wisdoms), Dm.kv(BB_SPEAK_CHANCE, 15f)));
        this.granted_wisdom = granted_wisdom;
        if (!Dm.truthy(granted_wisdom))
            this.granted_wisdom = pick(GLOB.skill_types);
        this.granted_experience = granted_experience;
        if (Dm.num(granted_experience) < 0)
            name = "unwise cow";
        return dot;
    }

    // tg: code/modules/mob/living/basic/farm_animals/cow/cow_wisdom.dm:28
    public override void setup_eating()
    {
        //cannot tame me! and I don't care about eatin' nothing, neither!
    }

    // tg: code/modules/mob/living/basic/farm_animals/cow/cow_wisdom.dm:31
    public override void setup_udder()
    {
        if (milked_reagent == null)
            milked_reagent = get_random_reagent_id() as Type;
        base.setup_udder();
    }

    ///Wisdom cow, gives XP to a random skill and speaks wisdoms
    // tg: code/modules/mob/living/basic/farm_animals/cow/cow_wisdom.dm:43
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (!IS_UNCONSCIOUS_OR_CRIT(this) && !Dm.truthy((user as mob_living)?.combat_mode))
        {
            to_chat(user, span_nicegreen(Dm.text("[] whispers you some intense wisdoms and then disappears!", this)));
            user.mind?.adjust_experience(granted_wisdom, granted_experience);
            await do_smoke(1, this, get_turf(this));
            qdel(this);
            return null;
        }
        return await base.attack_hand(user, modifiers, __extra);
    }
}
