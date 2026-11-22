// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/lavaland/vent_mining/gulag_vent.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Lavaland L2c (D304): the whole file. The var block is generated (D111).

/**
 * A boulder-spawning structure superficially similar to an ore vent which doesnt share any of its behaviour
 * Prisoners can haul boulders up out of it in case the actual mining area is totally spent
 */
// tg: code/game/objects/structures/lavaland/vent_mining/gulag_vent.dm:5
public partial class obj_structure_gulag_vent : obj_structure
{
    // tg: code/game/objects/structures/lavaland/vent_mining/gulag_vent.dm:23
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        object dot = await base.interact(user, __extra);
        if (!isliving(user))
            return dot;
        if (Dm.truthy(occupied))
        {
            balloon_alert(user, I18n.T("occupied!"));
            return dot;
        }
        mob_living living_user = (mob_living)user;
        occupied = TRUE;
        living_user.balloon_alert_to_viewers(I18n.T("hauling..."));

        float boulder_lift_speed = 8 * SECONDS;

        obj_item_organ_cyberimp_chest_spine potential_spine = living_user.get_organ_slot(ORGAN_SLOT_SPINE) as obj_item_organ_cyberimp_chest_spine;
        if (potential_spine != null)
            boulder_lift_speed *= potential_spine.athletics_boost_multiplier;

        object succeeded = await do_after(living_user, boulder_lift_speed, this);
        occupied = FALSE;
        if (!Dm.truthy(succeeded))
            return dot;
        float stamina_damage_to_inflict = HAS_TRAIT(user, TRAIT_STRENGTH) ? 60 : 120; //Decreases the amount of stamina damage inflicted by half if you're STRONG
        living_user.mind?.adjust_experience(typeof(datum_skill_athletics), 10);
        await living_user.apply_status_effect(typeof(datum_status_effect_exercised));

        obj_item_boulder gulag_boulder = Dm.@new(spawned_boulder, get_turf(living_user)) as obj_item_boulder;
        gulag_boulder.platform_lifespan = PLATFORM_LIFE_GULAG;

        living_user.visible_message(span_notice(I18n.T("[] hauls a boulder out of [].", living_user, this)));
        await living_user.apply_damage(stamina_damage_to_inflict, STAMINA);
        playsound(this, Dm.resource("sound/items/weapons/genhit.ogg"), vol: 50, vary: TRUE);
        return dot;
    }
}
