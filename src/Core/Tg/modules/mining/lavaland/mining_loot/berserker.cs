// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/lavaland/mining_loot/berserker.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

// tg: code/modules/mining/lavaland/mining_loot/berserker.dm:7
public partial class obj_item_clothing_suit_hooded_berserker : obj_item_clothing_suit_hooded
{
    // tg: code/modules/mining/lavaland/mining_loot/berserker.dm:44
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_anti_magic), ALL, Dm.named("inventory_flags", ITEM_SLOT_OCLOTHING)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_armor_plate), Dm.named("maxamount", 1), Dm.named("upgrade_item", typeof(obj_item_drake_remains)), Dm.named("armor_mod", typeof(datum_armor_drake_empowerment)), Dm.named("upgrade_prefix", "empowered")));
        allowed = GLOB.mining_suit_allowed;
        return dot;
    }
}

// tg: code/modules/mining/lavaland/mining_loot/berserker.dm:50
public partial class obj_item_clothing_head_hooded_berserker : obj_item_clothing_head_hooded
{
    // tg: code/modules/mining/lavaland/mining_loot/berserker.dm:1-5 (file-local #defines)
    private const float MAX_BERSERK_CHARGE = 100;
    private const float PROJECTILE_HIT_MULTIPLIER = 1.5f;
    private const float DAMAGE_TO_CHARGE_SCALE = 0.75f;
    private const float CHARGE_DRAINED_PER_SECOND = 5;
    private const float BERSERK_ATTACK_SPEED_MODIFIER = 0.25f;

    // tg: code/modules/mining/lavaland/mining_loot/berserker.dm:73
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NODROP, LOCKED_HELMET_TRAIT);
        Waiting.NoSleep(AddComponent(typeof(datum_component_armor_plate), Dm.named("maxamount", 1), Dm.named("upgrade_item", typeof(obj_item_drake_remains)), Dm.named("armor_mod", typeof(datum_armor_drake_empowerment)), Dm.named("upgrade_prefix", "empowered")));
        Waiting.NoSleep(AddComponent(typeof(datum_component_item_equipped_movement_rustle), SFX_PLATE_ARMOR_RUSTLE, 8));
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/berserker.dm:79
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_notice(I18n.T("Berserk mode is []% charged.", berserk_charge)));
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/berserker.dm:83
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(berserk_active))
            berserk_charge = clamp(berserk_charge - CHARGE_DRAINED_PER_SECOND * Dm.num(seconds_per_tick), 0, MAX_BERSERK_CHARGE);

        if (!Dm.truthy(berserk_charge))
        {
            if (ishuman(loc))
                end_berserk(loc as mob_living_carbon_human);
        }
        return null;
    }

    // tg: code/modules/mining/lavaland/mining_loot/berserker.dm:91
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        end_berserk(user as mob_living_carbon_human);
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/berserker.dm:95
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        attack_text ??= "the attack";
        final_block_chance ??= 0;
        damage ??= 0;
        attack_type ??= MELEE_ATTACK;
        damage_type ??= BRUTE;
        if (Dm.truthy(berserk_active))
            return null;
        float berserk_value = Dm.num(damage) * DAMAGE_TO_CHARGE_SCALE;
        if (attack_type == PROJECTILE_ATTACK)
            berserk_value *= PROJECTILE_HIT_MULTIPLIER;
        berserk_charge = clamp(round(berserk_charge + berserk_value), 0, MAX_BERSERK_CHARGE);
        if (berserk_charge >= MAX_BERSERK_CHARGE)
        {
            datum_action_item_action_berserk_mode ragemode = Dm.locate<datum_action_item_action_berserk_mode>(actions);
            to_chat(owner, span_cult_large(I18n.T("Berserk mode is fully charged.")));
            balloon_alert(owner, "berserk charged");
            if (ragemode != null)
                await ragemode.build_all_button_icons(UPDATE_BUTTON_STATUS);
        }
        return null;
    }

    // tg: code/modules/mining/lavaland/mining_loot/berserker.dm:108
    public override async DmTask<object> IsReflect(object def_zone = null)
    {
        await DmTask.CompletedTask;
        return berserk_active;
    }

    /// Starts berserk, reducing incoming brute by 50%, doubled attacking speed, NOGUNS trait, adding a color and giving them the berserk movespeed modifier
    // tg: code/modules/mining/lavaland/mining_loot/berserker.dm:112
    public virtual void berserk_mode(mob_living_carbon_human user = null)
    {
        datum_action_item_action_berserk_mode ragemode = Dm.locate<datum_action_item_action_berserk_mode>(actions);
        to_chat(user, span_cult(I18n.T("You enter berserk mode.")));
        playsound(user, Dm.resource("sound/effects/magic/staff_healing.ogg"), 50);
        Waiting.NoSleep(user.add_movespeed_modifier(typeof(datum_movespeed_modifier_berserk)));
        MODIFY_PHYSIOLOGY(user, BRUTE, 0.5f);
        user.next_move_modifier *= BERSERK_ATTACK_SPEED_MODIFIER;
        Waiting.NoSleep(user.add_atom_colour(COLOR_BUBBLEGUM_RED, TEMPORARY_COLOUR_PRIORITY));
        user.add_traits(Dm.list(TRAIT_NOGUNS, TRAIT_TOSS_GUN_HARD), BERSERK_TRAIT);
        ADD_TRAIT(this, TRAIT_NODROP, BERSERK_TRAIT);
        berserk_active = TRUE;
        START_PROCESSING(SSobj, this);
        if (ragemode != null)
            Waiting.NoSleep(ragemode.build_all_button_icons(UPDATE_BUTTON_STATUS));
    }

    /// Ends berserk, reverting the changes from the proc [berserk_mode]
    // tg: code/modules/mining/lavaland/mining_loot/berserker.dm:127
    public virtual void end_berserk(mob_living_carbon_human user = null)
    {
        if (!Dm.truthy(berserk_active))
            return;
        berserk_active = FALSE;
        if (QDELETED(user))
            return;
        datum_action_item_action_berserk_mode ragemode = Dm.locate<datum_action_item_action_berserk_mode>(actions);
        if (ragemode != null)
            Waiting.NoSleep(ragemode.build_all_button_icons(UPDATE_BUTTON_STATUS));
        to_chat(user, span_cult(I18n.T("You exit berserk mode.")));
        playsound(user, Dm.resource("sound/effects/magic/summonitems_generic.ogg"), 50);
        user.remove_movespeed_modifier(typeof(datum_movespeed_modifier_berserk));
        MODIFY_PHYSIOLOGY(user, BRUTE, 2);
        user.next_move_modifier /= BERSERK_ATTACK_SPEED_MODIFIER;
        Waiting.NoSleep(user.remove_atom_colour(TEMPORARY_COLOUR_PRIORITY, COLOR_BUBBLEGUM_RED));
        user.remove_traits(Dm.list(TRAIT_NOGUNS, TRAIT_TOSS_GUN_HARD), BERSERK_TRAIT);
        REMOVE_TRAIT(this, TRAIT_NODROP, BERSERK_TRAIT);
        STOP_PROCESSING(SSobj, this);
    }
}
