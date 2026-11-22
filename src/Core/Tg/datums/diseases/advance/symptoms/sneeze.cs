// Translated from tgstation (snapshot 1d99bd7f): code/datums/diseases/advance/symptoms/sneeze.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341). The var block is generated (D111). Each sneeze spreads the disease by air in a cone in front of
// the host (airborne_spread's require_facing; the sneeze emote's sound and message are the emote's).

// tg: code/datums/diseases/advance/symptoms/sneeze.dm:9
public partial class datum_symptom_sneeze : datum_symptom
{
    // tg: code/datums/diseases/advance/symptoms/sneeze.dm:35
    [DmParams("active_disease")]
    public override object Start(datum_disease_advance active_disease = null)
    {
        object dot = null;
        dot = base.Start(active_disease);
        if (!Dm.truthy(dot))
            return dot;
        if (Dm.num(active_disease.totalTransmittable()) >= 9) //longer spread range
            spread_range = 6;
        if (Dm.num(active_disease.totalStealth()) >= 4)
            suppress_warning = TRUE;
        if (Dm.num(active_disease.totalStageSpeed()) >= 17) //Yep, stage speed 17, not stage speed 7. This is a big boy threshold (effect), like the language-scrambling transmission one for the voice change symptom.
            cartoon_sneezing = TRUE; //for a really fun time, distribute a disease with this threshold met while the gravity generator is down
        return dot;
    }

    // tg: code/datums/diseases/advance/symptoms/sneeze.dm:46
    [DmParams("active_disease")]
    public override async DmTask<object> Activate(datum_disease_advance active_disease = null)
    {
        object dot = null;
        dot = await base.Activate(active_disease);
        if (!Dm.truthy(dot))
            return dot;
        mob_living affected_mob = active_disease.affected_mob;
        if (active_disease.stage == 1 || active_disease.stage == 2 || active_disease.stage == 3)
        {
            if (!Dm.truthy(suppress_warning))
                await affected_mob.emote("sniff");
        }
        else
        {
            await affected_mob.emote("sneeze");
            await active_disease.airborne_spread(spread_range: this.spread_range, force_spread: TRUE, require_facing: TRUE);
            if (Dm.truthy(cartoon_sneezing)) //Yeah, this can fling you around even if you have a space suit helmet on. It's, uh, bluespace snot, yeah.
            {
                to_chat(affected_mob, span_userdanger(I18n.T("You are launched violently backwards by an all-mighty sneeze!")));
                float sneeze_distance = rand(2, 4); //twice as far as a normal baseball bat strike will fling you
                turf target = get_ranged_target_turf(affected_mob, REVERSE_DIR(affected_mob.dir), sneeze_distance) as turf;
                await affected_mob.throw_at(target, sneeze_distance, rand(1, 4)); //with the wounds update, sneezing at 7 speed was causing peoples bones to spontaneously explode, turning cartoonish sneezing into a nightmarishly lethal GBS 2.0 outbreak
            }
            // COOLDOWN_FINISHED(src, sneeze_cooldown) / COOLDOWN_START (__DEFINES/cooldowns.dm:110-113), inline (rules 11.1 (a))
            else if ((this.sneeze_cooldown <= world.time) || !(this.sneeze_cooldown <= world.time) && prob(60) && !Dm.truthy(off_cooldown_sneezed))
            {
                this.sneeze_cooldown = world.time + (5 * SECONDS);
                if (!Dm.truthy(off_cooldown_sneezed) && !(this.sneeze_cooldown <= world.time))
                    off_cooldown_sneezed = TRUE;
                else
                    off_cooldown_sneezed = FALSE;
            }
        }
        return dot;
    }
}
