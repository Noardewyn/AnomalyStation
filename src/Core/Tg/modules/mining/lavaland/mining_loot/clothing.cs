// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/lavaland/mining_loot/clothing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

// Memento Mori
// tg: code/modules/mining/lavaland/mining_loot/clothing.dm:2
public partial class obj_item_clothing_neck_necklace_memento_mori : obj_item_clothing_neck_necklace
{
    // tg: code/modules/mining/lavaland/mining_loot/clothing.dm:12
    public override object item_action_slot_check(object slot = null, mob user = null, datum_action action = null)
    {
        return Dm.bitand(slot, ITEM_SLOT_NECK);
    }

    // tg: code/modules/mining/lavaland/mining_loot/clothing.dm:15
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        if (active_owner != null)
            await mori();
        return dot;
    }

    // Just in case
    // tg: code/modules/mining/lavaland/mining_loot/clothing.dm:21
    public override object Destroy(float? force = null)
    {
        if (active_owner != null)
            Waiting.NoSleep(mori());
        return base.Destroy(force);
    }

    // tg: code/modules/mining/lavaland/mining_loot/clothing.dm:26
    public virtual async DmTask memento(mob_living_carbon_human user = null)
    {
        to_chat(user, span_warning(I18n.T("You feel your life being drained by the pendant...")));
        if (!Dm.truthy(await do_after(user, 4 * SECONDS, target: user)))
            return;

        to_chat(user, span_notice(I18n.T("Your lifeforce is now linked to the pendant! You feel like removing it would kill you, and yet you instinctively know that until then, you won't die.")));
        user.add_traits(Dm.list(TRAIT_NODEATH, TRAIT_NOHARDCRIT, TRAIT_NOCRITDAMAGE), CLOTHING_TRAIT);
        RegisterSignal(user, COMSIG_LIVING_HEALTH_UPDATE, nameof(check_health));
        icon_state = "memento_mori_active";
        active_owner = user;
        ADD_TRAIT(this, TRAIT_DANGEROUS_UNEQUIP, TRAIT_GENERIC);
    }

    // tg: code/modules/mining/lavaland/mining_loot/clothing.dm:38
    public virtual async DmTask mori()
    {
        icon_state = "memento_mori";
        if (active_owner == null)
            return;
        UnregisterSignal(active_owner, COMSIG_LIVING_HEALTH_UPDATE);
        mob_living_carbon_human stored_owner = active_owner; //to avoid infinite looping when dust unequips the pendant
        active_owner = null;
        to_chat(stored_owner, span_userdanger(I18n.T("You feel your life rapidly slipping away from you!")));
        await stored_owner.dust(just_ash: TRUE, drop_items: TRUE);
        REMOVE_TRAIT(this, TRAIT_DANGEROUS_UNEQUIP, TRAIT_GENERIC);
    }

    // tg: code/modules/mining/lavaland/mining_loot/clothing.dm:49
    [SignalHandler]
    public virtual void check_health(mob_living source = null)
    {
        DmList guardians = source.get_all_linked_holoparasites();
        if (!Dm.truthy(length(guardians)))
            return;
        if (Dm.num(source.health) <= Dm.num(source.dead_threshold))
        {
            foreach (mob guardian in Dm.each<mob>(guardians))
            {
                if (guardian.loc == this)
                    continue;
                consume_guardian(guardian as mob_living_basic_guardian);
            }
        }
        else if (Dm.num(source.health) > Dm.num(source.crit_threshold))
        {
            foreach (mob guardian in Dm.each<mob>(guardians))
            {
                if (guardian.loc != this)
                    continue;
                Waiting.NoSleep(regurgitate_guardian(guardian as mob_living_basic_guardian));
            }
        }
    }

    // tg: code/modules/mining/lavaland/mining_loot/clothing.dm:66
    public virtual void consume_guardian(mob_living_basic_guardian guardian = null)
    {
        Dm.@new<obj_effect_temp_visual_guardian_phase_out>(get_turf(guardian));
        guardian.locked = TRUE;
        Waiting.NoSleep(guardian.forceMove(this));
        to_chat(guardian, span_userdanger(I18n.T("You have been locked away in your summoner's pendant!")));
        guardian.playsound_local(get_turf(guardian), Dm.resource("sound/effects/magic/summonitems_generic.ogg"), 50, TRUE);
    }

    // tg: code/modules/mining/lavaland/mining_loot/clothing.dm:73
    public virtual async DmTask regurgitate_guardian(mob_living_basic_guardian guardian = null)
    {
        guardian.locked = FALSE;
        await guardian.recall(forced: TRUE);
        to_chat(guardian, span_notice(I18n.T("You have been returned back from your summoner's pendant!")));
        guardian.playsound_local(get_turf(guardian), Dm.resource("sound/effects/magic/repulse.ogg"), 50, TRUE);
    }
}

// tg: code/modules/mining/lavaland/mining_loot/clothing.dm:79
public partial class datum_action_item_action_hands_free_memento_mori : datum_action_item_action_hands_free
{
    // tg: code/modules/mining/lavaland/mining_loot/clothing.dm:84
    public override async DmTask<object> do_effect(object trigger_flags = null)
    {
        obj_item_clothing_neck_necklace_memento_mori memento = target as obj_item_clothing_neck_necklace_memento_mori;
        if (memento.active_owner != null || !ishuman(owner))
            return FALSE;
        await memento.memento(owner as mob_living_carbon_human);
        await Remove(memento.active_owner); //Remove the action button, since there's no real use in having it now.
        return TRUE;
    }
}

// Concussive Gauntlets

// tg: code/modules/mining/lavaland/mining_loot/clothing.dm:94
public partial class obj_item_clothing_gloves_gauntlets : obj_item_clothing_gloves
{
    // tg: code/modules/mining/lavaland/mining_loot/clothing.dm:119
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_martial_art_giver), typeof(datum_martial_art_boxing_hunter)));
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/clothing.dm:123
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (!Dm.truthy(Dm.bitand(slot, ITEM_SLOT_GLOVES)))
            return;
        tool_behaviour = TOOL_MINING;
        RegisterSignal(user, COMSIG_LIVING_UNARMED_ATTACK, nameof(rocksmash));
        RegisterSignal(user, COMSIG_MOVABLE_BUMP, nameof(rocksmash));
    }

    // tg: code/modules/mining/lavaland/mining_loot/clothing.dm:131
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        stopmining(user);
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/clothing.dm:135
    public virtual void stopmining(mob user = null)
    {
        tool_behaviour = Dm.initial(this, (obj_item p) => p.tool_behaviour);
        UnregisterSignal(user, Dm.list(COMSIG_LIVING_UNARMED_ATTACK, COMSIG_MOVABLE_BUMP));
    }

    // tg: code/modules/mining/lavaland/mining_loot/clothing.dm:139
    [SignalHandler]
    public virtual object rocksmash(mob_living_carbon_human user = null, atom rocks = null, object proximity = null)
    {
        if (!Dm.truthy(proximity))
            return NONE;
        if (!ismineralturf(rocks) && !isasteroidturf(rocks))
            return NONE;
        Waiting.Detach(melee_attack_chain(user, rocks));
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }
}
