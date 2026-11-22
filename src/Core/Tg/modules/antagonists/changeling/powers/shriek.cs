// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/changeling/powers/shriek.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps K1 (D327): the whole file: the Resonant Shriek (the hearers deafened and confused, the cyborgs paralyzed, the lights broken)
// and the Technophagic Shriek (an EMP and the lights broken). The var blocks are generated (D111).

// tg: code/modules/antagonists/changeling/powers/shriek.dm:1
public partial class datum_action_changeling_resonant_shriek : datum_action_changeling
{
    //A flashy ability, good for crowd control and sowing chaos.
    // tg: code/modules/antagonists/changeling/powers/shriek.dm:14
    [DmParams("user")]
    public override async DmTask<object> sting_action(mob_living user = null, mob_living target = null)
    {
        await base.sting_action(user, target);
        if (Dm.truthy(Dm.bitand(user.movement_type, VENTCRAWLING)))
        {
            user.balloon_alert(user, I18n.T("can't shriek in pipes!"));
            return FALSE;
        }
        playsound(user, Dm.resource("sound/effects/screech.ogg"), 100);
        foreach (mob_living living in Dm.each<mob_living>(get_hearers_in_view(4, user)))
        {
            if (Dm.truthy(IS_CHANGELING(living)) || !Dm.truthy(await living.soundbang_act(SOUNDBANG_MASSIVE, stun_pwr: 0, damage_pwr: 0, deafen_pwr: 1 * MINUTES, ignore_deafness: TRUE, send_sound: FALSE)))
                continue;
            if (issilicon(living))
            {
                await living.Paralyze(rand(10 * SECONDS, 20 * SECONDS));
                continue;
            }
            await living.adjust_confusion(25 * SECONDS);
            await living.set_jitter_if_lower(100 * SECONDS);
        }

        foreach (obj_machinery_light light in Dm.each<obj_machinery_light>(range(4, user)))
        {
            light.on = TRUE;
            await light.break_light_tube();
            await stoplag();
        }
        return TRUE;
    }
}

// tg: code/modules/antagonists/changeling/powers/shriek.dm:35
public partial class datum_action_changeling_dissonant_shriek : datum_action_changeling
{
    // tg: code/modules/antagonists/changeling/powers/shriek.dm:44
    [DmParams("user")]
    public override async DmTask<object> sting_action(mob_living user = null, mob_living target = null)
    {
        await base.sting_action(user, target);
        if (Dm.truthy(Dm.bitand(user.movement_type, VENTCRAWLING)))
        {
            user.balloon_alert(user, I18n.T("can't shriek in pipes!"));
            return FALSE;
        }
        // tg passes a fourth positional argument, 1, which the named emp_source replaces (empulse takes epicenter, heavy_range,
        // light_range, emp_source)
        await empulse(get_turf(user), 2, 5, emp_source: this);
        foreach (obj_machinery_light L in Dm.each<obj_machinery_light>(range(5, usr)))
        {
            L.on = TRUE;
            await L.break_light_tube();
            await stoplag();
        }

        return TRUE;
    }
}
