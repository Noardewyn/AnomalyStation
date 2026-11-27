// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/icemoon/ice_demon/ice_demon.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


// tg: code/modules/mob/living/basic/icemoon/ice_demon/ice_demon.dm:1
public partial class mob_living_basic_mining_ice_demon : mob_living_basic_mining
{
    // tg: code/modules/mob/living/basic/icemoon/ice_demon/ice_demon.dm:29
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        DmList innate_actions = TgState.Current.mob_living_basic_mining_ice_demon__Initialize__innate_actions ??= Dm.list(
            Dm.kv(typeof(datum_action_cooldown_mob_cooldown_slippery_ice_floors), BB_DEMON_SLIP_ABILITY),
            Dm.kv(typeof(datum_action_cooldown_mob_cooldown_ice_demon_teleport), BB_DEMON_TELEPORT_ABILITY),
            Dm.kv(typeof(datum_action_cooldown_spell_conjure_limit_summons_create_afterimages), BB_DEMON_CLONE_ABILITY));
        grant_actions_by_list(innate_actions);

        Waiting.NoSleep(AddComponent(
            typeof(datum_component_ranged_attacks),
            Dm.named("projectile_type", typeof(obj_projectile_temp_ice_demon)),
            Dm.named("projectile_sound", Dm.resource("sound/items/weapons/pierce.ogg"))));
        DmList death_loot = TgState.Current.mob_living_basic_mining_ice_demon__Initialize__death_loot ??= Dm.list(Dm.kv(typeof(obj_item_stack_ore_bluespace_crystal), 3));
        Waiting.NoSleep(AddElement(typeof(datum_element_death_drops), death_loot));
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_flying)));
        return dot;
    }

    // tg: code/modules/mob/living/basic/icemoon/ice_demon/ice_demon.dm:47
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        // The raw bluespace core stays an inert item until the anomaly cores are ported.
        if (prob(5))
            Dm.@new<obj_item_raw_anomaly_core_bluespace>(loc);
        return await base.death(gibbed, __extra);
    }
}

// tg: code/modules/mob/living/basic/icemoon/ice_demon/ice_demon.dm:53
public partial class mob_living_basic_mining_demon_afterimage : mob_living_basic_mining
{
    // Passable, but mining mobs avoid pathing through them
    // tg: code/modules/mob/living/basic/icemoon/ice_demon/ice_demon.dm:76
    public override object CanAStarPass(object to_dir = null, datum_can_pass_info pass_info = null)
    {
        mob_living requester = pass_info.requester_ref?.resolve() as mob_living;
        // Only block mining mobs so that drones etc can still path through
        if (requester != null && Dm.truthy(Dm.bitand(requester.mob_biotypes, MOB_MINING)) && requester.loc != loc)
            return FALSE;
        return base.CanAStarPass(to_dir, pass_info);
    }

    // tg: code/modules/mob/living/basic/icemoon/ice_demon/ice_demon.dm:83
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_flying)));
        fade_into_nothing(life_time: existence_period);
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/basic/icemoon/ice_demon/ice_demon.dm:33 (var/static/list/innate_actions)
    internal DmList mob_living_basic_mining_ice_demon__Initialize__innate_actions;
    // tg: code/modules/mob/living/basic/icemoon/ice_demon/ice_demon.dm:44 (var/static/list/death_loot)
    internal DmList mob_living_basic_mining_ice_demon__Initialize__death_loot;
}
