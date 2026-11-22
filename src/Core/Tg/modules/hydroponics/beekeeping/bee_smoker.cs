// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/beekeeping/bee_smoker.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the whole file (the bee smoker). Its smoke particles are not ported (comments).

// tg: code/modules/hydroponics/beekeeping/bee_smoker.dm:4
public partial class obj_item_bee_smoker : obj_item
{
    /// multiplier to decide how much fuel we add to a smoker
    // tg: code/modules/hydroponics/beekeeping/bee_smoker.dm:2
    private const float WEED_WINE_MULTIPLIER = 0.2f;

    // tg: code/modules/hydroponics/beekeeping/bee_smoker.dm:24
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        beesmoke_loop = Dm.@new<datum_looping_sound_beesmoke>(this);
        return dot;
    }

    // tg: code/modules/hydroponics/beekeeping/bee_smoker.dm:28
    public override object Destroy(float? force = null)
    {
        // remove_shared_particles(/particles/smoke/bee_smoke): particles are not ported
        return base.Destroy(force);
    }

    // tg: code/modules/hydroponics/beekeeping/bee_smoker.dm:32
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return TRUE;
        if (!Dm.truthy(activated) && current_herb_fuel <= 0)
        {
            user.balloon_alert(user, I18n.T("no fuel!"));
            return TRUE;
        }
        alter_state();
        user.balloon_alert(user, I18n.T("[]", Dm.truthy(activated) ? "activated" : "deactivated"));
        return TRUE;
    }

    // tg: code/modules/hydroponics/beekeeping/bee_smoker.dm:43
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (interacting_with is not obj_structure_beebox && !isturf(interacting_with) && interacting_with is not mob_living_basic_bee)
            return NONE;

        object dot = ITEM_INTERACT_BLOCKING;
        if (!Dm.truthy(activated))
        {
            user.balloon_alert(user, I18n.T("not activated!"));
            return dot;
        }

        if (current_herb_fuel < single_use_cost)
        {
            user.balloon_alert(user, I18n.T("not enough fuel!"));
            return dot;
        }

        current_herb_fuel -= single_use_cost;
        playsound(this, Dm.resource("sound/effects/spray2.ogg"), 100, TRUE);
        turf target_turf = get_turf(interacting_with);
        Dm.@new<obj_effect_temp_visual_mook_dust>(target_turf);
        foreach (mob_living_basic_bee friend in Dm.each<mob_living_basic_bee>(target_turf))
        {
            if (Dm.truthy(Dm.bitand(Dm.num(friend.flags_1), HOLOGRAM_1)))
                continue;
            await friend.befriend(user);
            dot = ITEM_INTERACT_SUCCESS;
        }

        if (interacting_with is not obj_structure_beebox hive)
            return dot;

        foreach (mob_living bee in Dm.each_cast<mob_living>(hive.bees))
        {
            if (Dm.truthy(Dm.bitand(Dm.num(bee.flags_1), HOLOGRAM_1)))
                continue;
            await bee.befriend(user);
            dot = ITEM_INTERACT_SUCCESS;
        }

        return dot;
    }

    // tg: code/modules/hydroponics/beekeeping/bee_smoker.dm:78
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_food_grown_cannabis weed)
            return NONE;

        if (weed.wine_power == null)
            return ITEM_INTERACT_BLOCKING;

        if (current_herb_fuel == max_herb_fuel)
        {
            user.balloon_alert(user, I18n.T("already at maximum fuel!"));
            return ITEM_INTERACT_BLOCKING;
        }

        float fuel_worth = Dm.num(weed.wine_power) * WEED_WINE_MULTIPLIER;
        current_herb_fuel = (current_herb_fuel + fuel_worth > max_herb_fuel) ? max_herb_fuel : current_herb_fuel + fuel_worth;
        user.balloon_alert(user, I18n.T("fuel added"));
        qdel(weed);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/hydroponics/beekeeping/bee_smoker.dm:96
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        current_herb_fuel--;
        if (current_herb_fuel <= 0)
            alter_state();
        return null;
    }

    // tg: code/modules/hydroponics/beekeeping/bee_smoker.dm:101
    public virtual void alter_state()
    {
        activated = !Dm.truthy(activated) ? TRUE : FALSE;
        playsound(this, Dm.resource("sound/items/tools/welderdeactivate.ogg"), 50, TRUE);

        if (!Dm.truthy(activated))
        {
            beesmoke_loop.stop();
            // remove_shared_particles(/particles/smoke/bee_smoke): particles are not ported
            STOP_PROCESSING(SSobj, this);
            return;
        }

        beesmoke_loop.start();
        START_PROCESSING(SSobj, this);
        // add_shared_particles(/particles/smoke/bee_smoke): particles are not ported
    }
}
