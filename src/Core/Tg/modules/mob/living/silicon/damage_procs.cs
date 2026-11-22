// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/silicon/damage_procs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/mob/living/silicon/damage_procs.dm:1
public partial class mob_living_silicon : mob_living
{
    // tg: code/modules/mob/living/silicon/damage_procs.dm:1
    public override async DmTask<object> apply_effect(float? effect = null, string effecttype = null, float? blocked = null)
    {
        return FALSE; //The only effect that can hit them atm is flashes and they still directly edit so this works for now. (This was written in at least 2016. Help)
    }

    // tg: code/modules/mob/living/silicon/damage_procs.dm:4
    public override async DmTask<object> adjust_tox_loss(object amount = null, float? updating_health = null, float? forced = null, float? required_biotype = null) //immune to tox damage
    {
        return FALSE;
    }

    // tg: code/modules/mob/living/silicon/damage_procs.dm:7
    public override async DmTask<object> set_tox_loss(object amount = null, float? updating_health = null, float? forced = null, float? required_biotype = null)
    {
        return FALSE;
    }

    // tg: code/modules/mob/living/silicon/damage_procs.dm:10
    public override async DmTask<object> adjust_stamina_loss(object amount = null, float? updating_stamina = null, float? forced = null, float? required_biotype = null) //immune to stamina damage.
    {
        return FALSE;
    }

    // tg: code/modules/mob/living/silicon/damage_procs.dm:13
    public override async DmTask<object> set_stamina_loss(object amount = null, float? updating_stamina = null, float? forced = null, float? required_biotype = null)
    {
        return FALSE;
    }

    // tg: code/modules/mob/living/silicon/damage_procs.dm:16
    public override async DmTask<object> adjust_organ_loss(object slot = null, object amount = null, object maximum = null, object required_organ_flag = null) //immune to organ damage (no organs, duh)
    {
        return FALSE;
    }

    // tg: code/modules/mob/living/silicon/damage_procs.dm:19
    public override async DmTask<object> set_organ_loss(object slot = null, object amount = null, object maximum = null, object required_organ_flag = null)
    {
        return FALSE;
    }

    // tg: code/modules/mob/living/silicon/damage_procs.dm:22
    public override async DmTask<object> adjust_oxy_loss(object amount = null, float? updating_health = null, float? forced = null, float? required_biotype = null) //immune to oxygen damage
    {
        if (isAI(this)) //ais are snowflakes and use oxyloss for being in AI cards and having no battery
            return await base.adjust_oxy_loss(amount, updating_health, forced, required_biotype);

        return FALSE;
    }

    // tg: code/modules/mob/living/silicon/damage_procs.dm:28
    public override async DmTask<object> set_oxy_loss(object amount = null, float? updating_health = null, float? forced = null, float? required_biotype = null)
    {
        if (isAI(this)) //ditto
            return await base.set_oxy_loss(amount, updating_health, forced, required_biotype);

        return FALSE;
    }
}
