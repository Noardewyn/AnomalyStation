// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/mecha/mech_melee_attack.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * ## Mech melee attack
 * Called when a mech melees a target with fists
 * Handles damaging the target & associated effects
 * return value is number of damage dealt. returning a value puts our mech onto attack cooldown.
 * Arguments:
 * * mecha_attacker: Mech attacking this target
 * * user: mob that initiated the attack from inside the mech as a controller
 */
// tg: code/modules/vehicles/mecha/mech_melee_attack.dm:10
public partial class atom : datum
{
    public virtual DmTask<object> mech_melee_attack(obj_vehicle_sealed_mecha mecha_attacker = null, mob_living user = null)
    {
        SEND_SIGNAL(COMSIG_ATOM_ATTACK_MECH, Dm.list(this, mecha_attacker, user));
        log_combat(user, this, "attacked", mecha_attacker, Dm.text("(COMBAT MODE: [] (DAMTYPE: [])", uppertext(user?.combat_mode), uppertext(mecha_attacker.damtype)));
        return DmTask<object>.FromResult(null);
    }
}

// tg: code/modules/vehicles/mecha/mech_melee_attack.dm:17
public partial class turf_closed_wall : turf_closed
{
    public override async DmTask<object> mech_melee_attack(obj_vehicle_sealed_mecha mecha_attacker = null, mob_living user = null)
    {
        if (!Dm.truthy(user.combat_mode))
            return null;

        await mecha_attacker.do_attack_animation(this);
        if (Dm.eq(mecha_attacker.damtype, BRUTE))
            playsound(this, mecha_attacker.brute_attack_sound, 50, TRUE);
        else if (Dm.eq(mecha_attacker.damtype, BURN))
            playsound(this, mecha_attacker.burn_attack_sound, 50, TRUE);
        else
            return null;
        mecha_attacker.visible_message(span_danger(I18n.T("[] hits []!", mecha_attacker, this)), span_danger(I18n.T("You hit []!", this)), null, COMBAT_MESSAGE_RANGE);
        if (prob(Dm.num(hardness) + Dm.num(mecha_attacker.force)) && Dm.num(mecha_attacker.force) > 20)
        {
            await dismantle_wall(1);
            playsound(this, mecha_attacker.destroy_wall_sound, 100, TRUE);
        }
        else
            add_dent(WALL_DENT_HIT);
        await base.mech_melee_attack(mecha_attacker, user);
        return 100; //this is an arbitrary "damage" number since the actual damage is rng dismantle
    }
}

// tg: code/modules/vehicles/mecha/mech_melee_attack.dm:38
public partial class obj_structure : obj
{
    public override async DmTask<object> mech_melee_attack(obj_vehicle_sealed_mecha mecha_attacker = null, mob_living user = null)
    {
        if (!Dm.truthy(user.combat_mode))
            return 0;

        await mecha_attacker.do_attack_animation(this);
        if (Dm.eq(mecha_attacker.damtype, BRUTE))
            playsound(this, Dm.resource("sound/items/weapons/punch4.ogg"), 50, TRUE);
        else if (Dm.eq(mecha_attacker.damtype, BURN))
            playsound(this, Dm.resource("sound/items/tools/welder.ogg"), 50, TRUE);
        else
            return null;
        mecha_attacker.visible_message(span_danger(I18n.T("[] hits []!", mecha_attacker, this)), span_danger(I18n.T("You hit []!", this)), null, COMBAT_MESSAGE_RANGE);
        await base.mech_melee_attack(mecha_attacker, user);
        return await take_damage(Dm.num(mecha_attacker.force) * 3, mecha_attacker.damtype as string, "melee", FALSE, get_dir(this, mecha_attacker)); // multiplied by 3 so we can hit objs hard but not be overpowered against mobs.
    }
}

// tg: code/modules/vehicles/mecha/mech_melee_attack.dm:54
public partial class obj_machinery : obj
{
    public override async DmTask<object> mech_melee_attack(obj_vehicle_sealed_mecha mecha_attacker = null, mob_living user = null)
    {
        if (!Dm.truthy(user.combat_mode))
            return null;

        await mecha_attacker.do_attack_animation(this);
        if (Dm.eq(mecha_attacker.damtype, BRUTE))
            playsound(this, mecha_attacker.brute_attack_sound, 50, TRUE);
        else if (Dm.eq(mecha_attacker.damtype, BURN))
            playsound(this, mecha_attacker.burn_attack_sound, 50, TRUE);
        else
            return null;
        mecha_attacker.visible_message(span_danger(I18n.T("[] hits []!", mecha_attacker, this)), span_danger(I18n.T("You hit []!", this)), null, COMBAT_MESSAGE_RANGE);
        await base.mech_melee_attack(mecha_attacker, user);
        return await take_damage(Dm.num(mecha_attacker.force) * 3, mecha_attacker.damtype as string, "melee", FALSE, get_dir(this, mecha_attacker)); // multiplied by 3 so we can hit objs hard but not be overpowered against mobs.
    }
}

// tg: code/modules/vehicles/mecha/mech_melee_attack.dm:70
public partial class obj_structure_window : obj_structure
{
    public override async DmTask<object> mech_melee_attack(obj_vehicle_sealed_mecha mecha_attacker = null, mob_living user = null)
    {
        if (!Dm.truthy(user.combat_mode))
            return null;
        if (!Dm.truthy(await can_be_reached()))
            return null;

        await mecha_attacker.do_attack_animation(this);
        if (Dm.eq(mecha_attacker.damtype, BRUTE))
            playsound(this, mecha_attacker.brute_attack_sound, 50, TRUE);
        else if (Dm.eq(mecha_attacker.damtype, BURN))
            playsound(this, mecha_attacker.burn_attack_sound, 50, TRUE);
        else
            return null;
        mecha_attacker.visible_message(span_danger(I18n.T("[] smashes []!", mecha_attacker, this)), span_danger(I18n.T("You smash []!", this)), null, COMBAT_MESSAGE_RANGE);
        // Additionally destroy any grilles
        foreach (obj_structure_grille grille in Dm.each<obj_structure_grille>(loc))
        {
            if (istype(grille, typeof(obj_structure_grille)))
                await grille.take_damage(Dm.num(mecha_attacker.force) * 10, mecha_attacker.damtype as string, "melee", FALSE, get_dir(this, mecha_attacker));
        }
        await base.mech_melee_attack(mecha_attacker, user);
        return await take_damage(Dm.num(mecha_attacker.force) * 100, mecha_attacker.damtype as string, "melee", FALSE, get_dir(this, mecha_attacker));
    }
}

// tg: code/modules/vehicles/mecha/mech_melee_attack.dm:92
public partial class obj_vehicle : obj
{
    public override async DmTask<object> mech_melee_attack(obj_vehicle_sealed_mecha mecha_attacker = null, mob_living user = null)
    {
        if (!Dm.truthy(user.combat_mode))
            return FALSE;

        await mecha_attacker.do_attack_animation(this);
        if (Dm.eq(mecha_attacker.damtype, BRUTE))
            playsound(this, Dm.resource("sound/items/weapons/punch4.ogg"), 50, TRUE);
        else if (Dm.eq(mecha_attacker.damtype, BURN))
            playsound(this, Dm.resource("sound/items/tools/welder.ogg"), 50, TRUE);
        else
            return null;
        mecha_attacker.visible_message(span_danger(I18n.T("[] hits []!", mecha_attacker, this)), span_danger(I18n.T("You hit []!", this)), null, COMBAT_MESSAGE_RANGE);
        await base.mech_melee_attack(mecha_attacker, user);
        return await take_damage(mecha_attacker.force, mecha_attacker.damtype as string, "melee", FALSE, get_dir(this, mecha_attacker));
    }
}

// tg: code/modules/vehicles/mecha/mech_melee_attack.dm:108
public partial class mob_living : mob
{
    public override async DmTask<object> mech_melee_attack(obj_vehicle_sealed_mecha mecha_attacker = null, mob_living user = null)
    {
        if (istype(user, typeof(mob_living)) && !Dm.truthy(user.combat_mode))
        {
            await step_away(this, mecha_attacker);
            log_combat(user, this, "pushed", mecha_attacker);
            visible_message(span_warning(I18n.T("[] pushes [] out of the way.", mecha_attacker, this)),
                span_warning(I18n.T("[] pushes you out of the way.", mecha_attacker)), span_hear(I18n.T("You hear aggressive shuffling!")), 5, Dm.list(mecha_attacker));
            to_chat(mecha_attacker, span_danger(I18n.T("You push [] out of the way.", this)));
            return null;
        }

        if (user != null && HAS_TRAIT(user, TRAIT_PACIFISM))
        {
            to_chat(user, span_warning(I18n.T("You don't want to harm other living beings!")));
            return null;
        }

        await mecha_attacker.do_attack_animation(this);
        if (Dm.eq(mecha_attacker.damtype, BRUTE))
            await step_away(this, mecha_attacker, 15);

        float force = Dm.num(mecha_attacker.force);
        if (Dm.truthy(await check_block(mecha_attacker, force * 3, Dm.text("the []", mecha_attacker.attack_verbs[1]), attack_type: OVERWHELMING_ATTACK)))
            return null;

        if (Dm.eq(mecha_attacker.damtype, BRUTE))
        {
            if (force > 35) // durand and other heavy mechas
                await mecha_attacker.melee_attack_effect(this, TRUE);
            else if (force > 20 && !Dm.truthy(IsKnockdown())) // lightweight mechas like gygax
                await mecha_attacker.melee_attack_effect(this, FALSE);
            playsound(this, mecha_attacker.brute_attack_sound, 50, TRUE);
        }
        else if (Dm.eq(mecha_attacker.damtype, BURN))
            playsound(this, mecha_attacker.burn_attack_sound, 50, TRUE);
        else if (Dm.eq(mecha_attacker.damtype, TOX))
        {
            playsound(this, mecha_attacker.tox_attack_sound, 50, TRUE);
            float bio_armor = (100 - Dm.num(run_armor_check(attack_flag: BIO, silent: TRUE))) / 100;
            datum_reagents held = reagents as datum_reagents;
            if (Dm.num(held.get_reagent_amount(typeof(datum_reagent_cryptobiolin))) + force < force * 2)
                await held.add_reagent(typeof(datum_reagent_cryptobiolin), force / 2 * bio_armor);
            if (Dm.num(held.get_reagent_amount(typeof(datum_reagent_toxin))) + force < force * 2)
                await held.add_reagent(typeof(datum_reagent_toxin), force / 2.5f * bio_armor);
        }
        else
            return null;

        object damage = rand(force * 0.5f, force);
        if (Dm.eq(mecha_attacker.damtype, BRUTE) || Dm.eq(mecha_attacker.damtype, BURN))
        {
            object def_zone = get_random_valid_zone(user?.zone_selected, even_weights: TRUE);
            object zone_readable = parse_zone_with_bodypart(def_zone);
            await apply_damage(Dm.num(damage), mecha_attacker.damtype as string, def_zone, Dm.num(run_armor_check(
                def_zone: def_zone,
                attack_flag: MELEE,
                absorb_text: span_notice(I18n.T("Your armor has protected your []!", zone_readable)),
                soften_text: span_warning(I18n.T("Your armor has softened a hit to your []!", zone_readable)))));
        }

        visible_message(span_danger(I18n.T("[] [] []!", mecha_attacker.name, mecha_attacker.attack_verbs[1], this)),
            span_userdanger(I18n.T("[] [] you!", mecha_attacker.name, mecha_attacker.attack_verbs[2])), span_hear(I18n.T("You hear a sickening sound of flesh [] flesh!", mecha_attacker.attack_verbs[3])), COMBAT_MESSAGE_RANGE, Dm.list(mecha_attacker));
        to_chat(mecha_attacker, span_danger(I18n.T("You [] []!", mecha_attacker.attack_verbs[1], this)));
        await base.mech_melee_attack(mecha_attacker, user);
        return damage;
    }
}
