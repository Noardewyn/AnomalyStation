// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/guardian/guardian_types/charger.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X2 (D316): the whole file. The var blocks are generated.

/**
 * Very fast, has a charging attack, and most importantly can be ridden like a horse.
 */
// tg: code/modules/mob/living/basic/guardian/guardian_types/charger.dm:4
public partial class mob_living_basic_guardian_charger : mob_living_basic_guardian
{
    // tg: code/modules/mob/living/basic/guardian/guardian_types/charger.dm:16
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_ridable), typeof(datum_component_riding_creature_guardian)));
        return dot;
    }
}

/// Guardian charger's charging attack, it knocks items out of people's hands
// tg: code/modules/mob/living/basic/guardian/guardian_types/charger.dm:21
public partial class datum_action_cooldown_mob_cooldown_charge_basic_charge_guardian : datum_action_cooldown_mob_cooldown_charge_basic_charge
{
    // tg: code/modules/mob/living/basic/guardian/guardian_types/charger.dm:37
    public override async DmTask<object> PreActivate(atom target = null)
    {
        if (!isguardian(owner))
            return await base.PreActivate(target);
        mob_living_basic_guardian guardian_owner = (mob_living_basic_guardian)owner;
        if (Dm.truthy(guardian_owner.is_deployed()))
            return await base.PreActivate(target);
        return FALSE;
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/charger.dm:45
    public override void do_charge_indicator(atom charger = null, atom charge_target = null)
    {
        playsound(charger, Dm.resource("sound/items/modsuit/loader_launch.ogg"), 75, TRUE);
        obj_effect_temp_visual_decoy decoy_flash = Dm.@new<obj_effect_temp_visual_decoy>(charger.loc, charger);
        animate(decoy_flash, Dm.named("alpha", 0), Dm.named("color", COLOR_RED), Dm.named("transform", Dm.mul(Dm.matrix(), 2)), Dm.named("time", 3));
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/charger.dm:50
    public override async DmTask<object> can_hit_target(atom_movable source = null, atom target = null)
    {
        await DmTask.CompletedTask;
        mob_living living_target = target as mob_living;
        if (living_target == null)
            return FALSE;
        mob_living_basic_guardian guardian_owner = owner as mob_living_basic_guardian;
        if (guardian_owner == null)
            return TRUE;
        if (living_target == guardian_owner.summoner || Dm.truthy(guardian_owner.shares_summoner(target as mob_living_basic_guardian)))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/charger.dm:61
    public override async DmTask hit_target(atom_movable source = null, object target = null, object damage_dealt = null)
    {
        if (ishuman(target))
        {
            mob_living_carbon_human hit_human = (mob_living_carbon_human)target;
            // tg passes the action itself (a datum) as hit_by; the slot's hit_by is an atom, so the charging guardian is passed
            if (Dm.truthy(await hit_human.check_block(owner, charge_damage, name, attack_type: LEAP_ATTACK)))
                return;
        }
        await base.hit_target(source, target, damage_dealt);
        mob_living hit_mob = (mob_living)target;
        await hit_mob.drop_all_held_items();
    }
}
