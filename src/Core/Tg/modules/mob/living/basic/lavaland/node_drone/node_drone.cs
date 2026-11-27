// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/node_drone/node_drone.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * Mining drones that are spawned when starting a ore vent's wave defense minigame.
 * They will latch onto the vent to defend it from lavaland mobs, and will flee if attacked by lavaland mobs.
 * If the drone survives, they will fly away to safety as the vent spawns ores.
 * If the drone dies, the wave defense will fail.
 */
// tg: code/modules/mob/living/basic/lavaland/node_drone/node_drone.dm:12
public partial class mob_living_basic_node_drone : mob_living_basic
{
    // tg: code/modules/mob/living/basic/lavaland/node_drone/node_drone.dm:1-3  #define FLY_IN_STATE 1, FLY_OUT_STATE 2, NEUTRAL_STATE 3 (file-local)
    private const float FLY_IN_STATE = 1;
    private const float FLY_OUT_STATE = 2;
    private const float NEUTRAL_STATE = 3;

    // tg: code/modules/mob/living/basic/lavaland/node_drone/node_drone.dm:51
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_traits(Dm.list(TRAIT_ASHSTORM_IMMUNE, TRAIT_SNOWSTORM_IMMUNE, TRAIT_MINING_AOE_IMMUNE), INNATE_TRAIT);
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/node_drone/node_drone.dm:56
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        object dot = await base.death(gibbed, __extra);
        await explosion(origin: this, light_impact_range: 1, smoke: 1);
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/node_drone/node_drone.dm:60
    public override object Destroy(float? force = null)
    {
        if (attached_vent != null)
        {
            attached_vent.node = null;
            attached_vent = null;
        }
        return base.Destroy(force);
    }

    // tg: code/modules/mob/living/basic/lavaland/node_drone/node_drone.dm:66
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        object sameside = user.faction_check_atom(this, exact_match: FALSE);
        if (Dm.truthy(sameside))
            Dm.append(ref dot, span_notice(I18n.T("This drone is currently attached to a mineral vent. You should protect it from harm to secure the mineral vent.")));
        else
            Dm.append(ref dot, span_warning(I18n.T("This vile Nanotrasen trash is trying to destroy the environment. Attack it to free the mineral vent from its grasp.")));
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/node_drone/node_drone.dm:74
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);

        icon_state = "mining_node_active";

        if (flying_state == FLY_IN_STATE || flying_state == FLY_OUT_STATE)
            icon_state = "mining_node_flying";
        return dot;
    }

    // tg: code/modules/mob/living/basic/lavaland/node_drone/node_drone.dm:83
    public virtual void arrive(obj_structure_ore_vent parent_vent = null)
    {
        attached_vent = parent_vent;
        maxHealth = 300 + ((attached_vent.boulder_size / BOULDER_SIZE_SMALL) * 100);
        health = maxHealth;
        flying_state = FLY_IN_STATE;
        update_appearance(UPDATE_ICON_STATE);
        pixel_z = 400;
        animate(this, Dm.named("pixel_z", 0), Dm.named("time", 2 * SECONDS), Dm.named("easing", Dm.bitor(QUAD_EASING, EASE_OUT)), Dm.named("flags", ANIMATION_PARALLEL));
    }

    /**
     * Called when wave defense is completed. Visually flicks the escape sprite and then deletes the mob.
     */
    // tg: code/modules/mob/living/basic/lavaland/node_drone/node_drone.dm:96
    public virtual async DmTask escape(object success = null)
    {
        bool funny_ending = false;
        flying_state = FLY_OUT_STATE;
        update_appearance(UPDATE_ICON_STATE);
        if (prob(1))
        {
            await say(I18n.T("I have to go now, my planet needs me."));
            funny_ending = true;
        }
        if (Dm.truthy(success))
            visible_message(span_notice(I18n.T("The drone flies away to safety as the vent is secured.")));
        else
            visible_message(span_danger(I18n.T("The drone flies away after failing to open the vent!")));
        animate(this, Dm.named("pixel_z", 400), Dm.named("time", 2 * SECONDS), Dm.named("easing", Dm.bitor(QUAD_EASING, EASE_IN)), Dm.named("flags", ANIMATION_PARALLEL));
        await sleep(2 * SECONDS);
        if (funny_ending)
        {
            playsound(this, Dm.resource("sound/effects/explosion/explosion3.ogg"), 50, FALSE); //node drone died on the way back to his home planet.
            visible_message(span_notice(I18n.T("...or maybe not.")));
        }
        qdel(this);
    }

    // tg: code/modules/mob/living/basic/lavaland/node_drone/node_drone.dm:115
    public virtual void pre_escape(object success = null)
    {
        success ??= TRUE;
        if (buckled != null)
            Waiting.NoSleep(buckled.unbuckle_mob(this));
        if (attached_vent != null)
            attached_vent = null;
        if (!Dm.truthy(escaping))
        {
            escaping = TRUE;
            flick("mining_node_escape", this);
            _addtimer(Dm.@new<datum_callback>(this, nameof(escape), success), 1.9f * SECONDS, file: "code/modules/mob/living/basic/lavaland/node_drone/node_drone.dm", line: 123);
            return;
        }
    }
}

/// Validates an ore vent as a valid hunt target: must exist and have no drone already latched.
// tg: code/modules/mob/living/basic/lavaland/node_drone/node_drone.dm:140
public partial class datum_targeting_strategy_ore_vent_unclaimed : datum_targeting_strategy
{
    // tg: code/modules/mob/living/basic/lavaland/node_drone/node_drone.dm:142
    public override async DmTask<object> is_valid_target(mob_living living_mob = null, atom target = null, object vision_range = null, datum_ai_controller controller = null)
    {
        object dot = await base.is_valid_target(living_mob, target, vision_range, controller);
        if (!Dm.truthy(dot))
            return FALSE;
        obj_structure_ore_vent vent = target as obj_structure_ore_vent;
        return vent != null && vent.node == null ? TRUE : FALSE;
    }
}
