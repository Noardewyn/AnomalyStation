// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/advance/symptoms/cough.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111). Each cough spreads the disease by air to the people within
// its range (the cough emote's sound and message are the emote's).

// tg: code/datums/diseases/advance/symptoms/cough.dm:9
public partial class datum_symptom_cough : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/cough.dm:36
    [DmParams("active_disease")]
    public override object Start(datum_disease_advance active_disease = null)
    {
        object dot = null;
        dot = base.Start(active_disease);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(active_disease.totalStealth()) >= 4)
            suppress_warning = TRUE;
        if (Dm.num(active_disease.totalTransmittable()) >= 7)
            spread_range = 2;
        if (Dm.num(active_disease.totalResistance()) >= 11) //strong enough to drop items
            power = 1.5f;
        if (Dm.num(active_disease.totalResistance()) >= 15) //strong enough to stun (occasionally)
            power = 2;
        if (Dm.num(active_disease.totalStageSpeed()) >= 6) //cough more often
            symptom_delay = 6;
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/cough.dm:51
    [DmParams("active_disease")]
    public override async DmTask<object> Activate(datum_disease_advance active_disease = null)
    {
        object dot = null;
        dot = await base.Activate(active_disease);
        if (!Dm.truthy(dot))
            return dot;
        mob_living affected_mob = active_disease.affected_mob;
        if (HAS_TRAIT(affected_mob, TRAIT_SOOTHED_THROAT))
            return dot;
        if (active_disease.stage == 1 || active_disease.stage == 2 || active_disease.stage == 3)
        {
            if (prob(base_message_chance) && !Dm.truthy(suppress_warning))
                to_chat(affected_mob, span_warning(I18n.T(Dm.str(pick(Dm.list("You swallow excess mucus.", "You lightly cough."))))));
        }
        else
        {
            // COOLDOWN_FINISHED(src, cough_cooldown) / COOLDOWN_START (__DEFINES/cooldowns.dm:110-113), inline (rules 11.1 (a))
            if ((this.cough_cooldown <= world.time) || !(this.cough_cooldown <= world.time) && prob(60) && !Dm.truthy(off_cooldown_coughed))
            {
                await affected_mob.emote("cough");
                this.cough_cooldown = world.time + (5 * SECONDS);
                if (!Dm.truthy(off_cooldown_coughed) && !(this.cough_cooldown <= world.time))
                    off_cooldown_coughed = TRUE;
                else
                    off_cooldown_coughed = FALSE;
            }
            await active_disease.airborne_spread(spread_range);
            if (Dm.num(power) >= 1.5f)
            {
                obj_item held_object = affected_mob.get_active_held_item() as obj_item;
                if (held_object != null && held_object.w_class == WEIGHT_CLASS_TINY)
                    await affected_mob.dropItemToGround(held_object);
            }
            if (Dm.num(power) >= 2 && prob(30))
            {
                to_chat(affected_mob, span_userdanger(I18n.T(Dm.str(pick(Dm.list("You have a coughing fit!", "You can't stop coughing!"))))));
                await affected_mob.Immobilize(20);
                _addtimer(Dm.@new<datum_callback>(affected_mob, nameof(mob.emote), "cough"), 0.6f * SECONDS, file: "code/datums/diseases/advance/symptoms/cough.dm", line: 82);
                _addtimer(Dm.@new<datum_callback>(affected_mob, nameof(mob.emote), "cough"), 1.2f * SECONDS, file: "code/datums/diseases/advance/symptoms/cough.dm", line: 83);
                _addtimer(Dm.@new<datum_callback>(affected_mob, nameof(mob.emote), "cough"), 1.8f * SECONDS, file: "code/datums/diseases/advance/symptoms/cough.dm", line: 84);
            }
        }
        return dot;
    }
}
