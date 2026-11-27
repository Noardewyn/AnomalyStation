// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/icemoon/ice_whelp/ice_whelp_abilities.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


/// Breathe "fire" in a line (it's freezing cold)
// tg: code/modules/mob/living/basic/icemoon/ice_whelp/ice_whelp_abilities.dm:2
public partial class datum_action_cooldown_mob_cooldown_fire_breath_ice : datum_action_cooldown_mob_cooldown_fire_breath
{
    // tg: code/modules/mob/living/basic/icemoon/ice_whelp/ice_whelp_abilities.dm:21
    [DmParams("Target", "original")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        forecast_overlay = image(Dm.resource("icons/mob/simple/icemoon/icemoon_monsters.dmi"), forecast_overlay_state);
    }

    /// Apply our specific fire breathing shape, in proc form so we can override it in subtypes
    // tg: code/modules/mob/living/basic/icemoon/ice_whelp/ice_whelp_abilities.dm:26
    public override async DmTask attack_sequence(atom target = null)
    {
        target_turf = get_turf(target) as turf;
        Waiting.Detach(attack_forecast()); // INVOKE_ASYNC
    }

    /// Charge up before we breathe fire
    // tg: code/modules/mob/living/basic/icemoon/ice_whelp/ice_whelp_abilities.dm:31
    public virtual async DmTask attack_forecast()
    {
        owner.face_atom(target_turf);
        owner.Shake(pixelshiftx: 1, pixelshifty: 0, duration: forecast_delay);
        forecast_overlay.dir = get_dir(owner, target_turf); // forecast_overlay.setDir(...): /image/proc/setDir is `dir = newdir` (__HELPERS/icons.dm:829)
        owner.add_overlay(forecast_overlay);
        object succeeded = await do_after(owner, delay: forecast_delay, target: owner, cog_icon: null);
        owner.cut_overlay(forecast_overlay);
        if (Dm.truthy(succeeded))
        {
            playsound(owner.loc, fire_sound, 200, TRUE);
            await breath_attack();
        }
    }

    /// Actually breathe fire
    // tg: code/modules/mob/living/basic/icemoon/ice_whelp/ice_whelp_abilities.dm:43
    public virtual async DmTask breath_attack()
    {
        owner.face_atom(target_turf);
        fire_line(target_turf);
        target_turf = null;
    }
}

/// Breathe really cold fire in a plus shape, like bomberman
// tg: code/modules/mob/living/basic/icemoon/ice_whelp/ice_whelp_abilities.dm:49
public partial class datum_action_cooldown_mob_cooldown_fire_breath_ice_eruption : datum_action_cooldown_mob_cooldown_fire_breath_ice
{
    // tg: code/modules/mob/living/basic/icemoon/ice_whelp/ice_whelp_abilities.dm:61
    public override async DmTask breath_attack()
    {
        target_turf = null;

        DmList hit_list = Dm.list(owner);
        DmList nearby_turfs = null;
        foreach (turf_open target_turf in Dm.each<turf_open>(circle_range(owner, fire_range)))
        {
            if (Dm.truthy(await target_turf.is_blocked_turf(exclude_mobs: TRUE)))
                continue;
            float turf_dist = get_dist(owner, target_turf);
            if (turf_dist == 0)
                continue;
            LAZYADDASSOCLIST(ref nearby_turfs, Dm.text("[]", turf_dist), target_turf);
        }

        for (float i = 1; i <= fire_range; i++)
        {
            foreach (turf_open kindling in Dm.each<turf_open>(nearby_turfs?[Dm.text("[]", i)]))
                burn_turf(kindling, hit_list, owner as mob_living);
            await sleep(fire_delay);
        }
    }
}
