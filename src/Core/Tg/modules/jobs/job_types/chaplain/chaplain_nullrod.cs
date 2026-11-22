// Translated from tgstation (snapshot 1d99bd7f): code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J2 (D298): the null rod, the holy weapon menu and the variants' procs. Rows and Deferred leaves (not-translated.md 106): the
// components that wait for their own packages (the alternative sharpness, butchering, walking aid, spirit holding, kneejerk, prosthetic
// icon, chemical transfer); the menu's non-rod variants (the plush, the bow, the shard, the skateboard, the katana, the energy sword)
// are inert items until theirs.

// tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:3
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:3  GLOBAL_LIST_INIT(nullrod_variants, init_nullrod_variants())
    public DmList nullrod_variants;

    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:3  (GLOBAL_MANAGED(nullrod_variants, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalnullrod_variants()
    {
        nullrod_variants = init_nullrod_variants();
        Dm.append(ref gvars_datum_init_order, "nullrod_variants");
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:5
    public static DmList init_nullrod_variants()
    {
        DmList rods = Dm.list();
        foreach (Type nullrod_type in Dm.each_cast<Type>(typesof(typeof(obj_item_nullrod))))
        {
            if (!Dm.truthy(Dm.initial(nullrod_type, "chaplain_spawnable")))
                continue;
            rods[nullrod_type] = Dm.initial(nullrod_type, "menu_description");
        }
        //special non-nullrod subtyped shit
        rods[typeof(obj_item_toy_plush_carpplushie_nullrod)] = "A plushie dealing a little less damage due to its cute form. Capable of blessing one person with the Carp-Sie favor, which grants friendship of all wild space carps. Fits in pockets. Can be worn on the belt.";
        rods[typeof(obj_item_gun_ballistic_bow_divine)] = "A divine bow and 10 quivered holy arrows.";
        rods[typeof(obj_item_organ_cyberimp_arm_toolkit_shard_scythe)] = "A shard that implants itself into your arm, allowing you to conjure forth a vorpal scythe. Allows you to behead targets for empowered strikes. Harms you if you dismiss the scythe without first causing harm to a creature. The shard also causes you to become Morbid, shifting your interests towards the macabre.";
        rods[typeof(obj_item_melee_skateboard_holyboard)] = "A skateboard that grants you flight and anti-magic abilities while ridden. Fits in your bag.";
        rods[typeof(obj_item_storage_belt_sheath_hanzo_katana)] = "A sharp katana which provides a low chance of blocking incoming melee attacks. Can be worn on the back or belt, wearing the sheath on belt allows for a swift counterattack.";

        foreach (Type energy_nullrod_type in Dm.each_cast<Type>(typesof(typeof(obj_item_melee_energy_sword_nullrod))))
            rods[energy_nullrod_type] = "An energy sword, but with a lower force, no armour penetration and a low chance of blocking. Can be switched on and off. Can be stored away easily while off, but impossible while on.";

        return rods;
    }
}

// tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:31
public partial class obj_item_nullrod : obj_item
{
    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:53
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_nullrod_core), chaplain_spawnable));

        if ((GLOB.holy_weapon_type != null && Dm.truthy(station_holy_item)) || type != typeof(obj_item_nullrod))
            return dot;
        Waiting.NoSleep(AddComponent(typeof(datum_component_subtype_picker), GLOB.nullrod_variants, Dm.@new<datum_callback>(this, nameof(on_holy_weapon_picked))));
        return dot;
    }

    /// Callback for subtype picker, invoked when the chaplain picks a new nullrod
    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:62
    public virtual void on_holy_weapon_picked(obj_item new_holy_weapon = null, mob_living picker = null)
    {
        if (!Dm.truthy(station_holy_item))
            return;
        GLOB.holy_weapon_type = new_holy_weapon.type;
        // SEND_GLOBAL_SIGNAL(COMSIG_GLOB_NULLROD_PICKED) (rules 11.1 (e), a statement)
        if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_NULLROD_PICKED]))
            SSdcs._SendSignal(COMSIG_GLOB_NULLROD_PICKED, Dm.list(SSdcs));
        SSblackbox.record_feedback("tally", "chaplain_weapon", 1, Dm.text("[]", new_holy_weapon.name));
    }

    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:69
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is killing []self with []! It looks like [] trying to get closer to god!", user, user.p_them(), this, user.p_theyre())));
        return Dm.bitor(BRUTELOSS, FIRELOSS);
    }
}

// tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:81
public partial class obj_item_nullrod_claymore : obj_item_nullrod
{
    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:103
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        alt_continuous = string_list(alt_continuous) as DmList;
        alt_simple = string_list(alt_simple) as DmList;
        Dm.Deferred("code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:106", "Initialize: AddComponent(/datum/component/alternative_sharpness, ...): the right-click's pointy attack");
        return dot;
    }

    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:109
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        attack_text ??= "the attack";
        final_block_chance ??= 0;
        damage ??= 0;
        attack_type ??= MELEE_ATTACK;
        damage_type ??= BRUTE;
        if (Dm.eq(attack_type, PROJECTILE_ATTACK) || Dm.eq(attack_type, LEAP_ATTACK) || Dm.eq(attack_type, OVERWHELMING_ATTACK))
            final_block_chance = 0; //Don't bring a sword to a gunfight, and also you aren't going to really block someone full body tackling you with a sword. Or a road roller, if one happened to hit you.
        return await base.hit_reaction(owner, hitby, attack_text, final_block_chance, damage, attack_type, damage_type);
    }
}

// tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:160
public partial class obj_item_nullrod_claymore_multiverse : obj_item_nullrod_claymore
{
    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:170
    public override async DmTask<object> pre_attack(atom target = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        attack_modifiers ??= Dm.list();
        attack_modifiers[FORCE_OVERRIDE] = rand(Dm.max(force - 15, 1), force + 15); // SET_ATTACK_FORCE
        return await base.pre_attack(target, user, modifiers, attack_modifiers);
    }
}

// tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:187
public partial class obj_item_nullrod_claymore_talking : obj_item_nullrod_claymore
{
    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:203
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:205", "Initialize: AddComponent(/datum/component/spirit_holding), the talking sword's ghost");
        return dot;
    }
}

// tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:207
public partial class obj_item_nullrod_claymore_talking_chainsword : obj_item_nullrod_claymore_talking
{
    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:222
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:224", "Initialize: AddElement(/datum/element/cuffable_item) and the butchering component");
        return dot;
    }
}

// tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:249
public partial class obj_item_nullrod_vibro : obj_item_nullrod
{
    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:274
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        alt_continuous = string_list(alt_continuous) as DmList;
        alt_simple = string_list(alt_simple) as DmList;
        Dm.Deferred("code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:278", "Initialize: AddComponent(/datum/component/alternative_sharpness, ...): the right-click's pointy attack");
        Waiting.NoSleep(AddComponent(typeof(datum_component_two_handed), Dm.named("force_unwielded", 10), Dm.named("force_wielded", 18)));
        Dm.Deferred("code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:283", "Initialize: AddComponent(/datum/component/butchering, ...): butchering");
        return dot;
    }

    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:289
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        attack_text ??= "the attack";
        final_block_chance ??= 0;
        attack_type ??= MELEE_ATTACK;
        if (prob(Dm.num(final_block_chance) * (HAS_TRAIT(this, TRAIT_WIELDED) ? 2 : 1)) && Dm.eq(attack_type, OVERWHELMING_ATTACK))
        {
            owner.visible_message(span_danger(I18n.T("[] parries [] with []!", owner, attack_text, this)));
            return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:295
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        inhand_icon_state = Dm.text("[][]", base_icon_state, HAS_TRAIT(this, TRAIT_WIELDED) ? 1 : 0);
        icon_state = Dm.str(inhand_icon_state);
        return await base.update_icon_state(__extra);
    }
}

// tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:301
public partial class obj_item_nullrod_godhand : obj_item_nullrod
{
    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:319
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NODROP, HAND_REPLACEMENT_TRAIT);
        return dot;
    }
}

// tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:325
public partial class obj_item_nullrod_staff : obj_item_nullrod
{
    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:343
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:345", "Initialize: AddComponent(/datum/component/walking_aid): the cane's walking aid");
        return dot;
    }

    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:347
    public override object worn_overlays(mutable_appearance standing = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        DmList dot = base.worn_overlays(standing, isinhands, icon_file, bodyshape) as DmList;
        if (Dm.truthy(isinhands))
            Dm.append(ref dot, mutable_appearance(Dm.resource("icons/effects/effects.dmi"), shield_icon, MOB_SHIELD_LAYER));
        return dot;
    }
}

// tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:361
public partial class obj_item_nullrod_sord : obj_item_nullrod
{
    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:379
    public override async DmTask<object> suicide_act(mob_living user = null) //a near-exact copy+paste of the actual sord suicide_act()
    {
        user.visible_message(span_suicide(I18n.T("[] is trying to impale []self with []! It might be a suicide attempt if it weren't so HOLY.", user, user.p_them(), this)),
            span_suicide(I18n.T("You try to impale yourself with [], but it's TOO HOLY...", this)));
        return SHAME;
    }
}

// tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:386
public partial class obj_item_nullrod_hammer : obj_item_nullrod
{
    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:401
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:403", "Initialize: AddElement(/datum/element/kneejerk): the hammer's knee tap");
        return dot;
    }
}

// tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:407
public partial class obj_item_nullrod_chainsaw : obj_item_nullrod
{
    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:427
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:429", "Initialize: AddElement(/datum/element/prosthetic_icon, ...) and the butchering component");
        RegisterSignal(this, COMSIG_ITEM_SUBTYPE_PICKER_SELECTED, nameof(on_selected));
        return dot;
    }

    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:438
    [SignalHandler]
    public virtual void on_selected(datum source = null, obj_item old_weapon = null, mob_living picker = null)
    {
        if (!iscarbon(picker))
            return;
        to_chat(picker, span_warning(I18n.T("[] takes the place of your arm!", this)));
        Dm.Deferred("code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:443", "on_selected: make_item_prosthetic, the arm replaced by the chainsaw (the prosthetic items, not ported)");
    }

    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:447
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (!iscarbon(user) || HAS_TRAIT_FROM(this, TRAIT_NODROP, HAND_REPLACEMENT_TRAIT))
            return;
        if (!Dm.truthy(Dm.bitand(slot, ITEM_SLOT_HANDS)))
            return;
        to_chat(user, span_warning(I18n.T("As you lay your hands on [], it latches onto your arm!", this)));
        Dm.Deferred("code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:454", "equipped: make_item_prosthetic, the arm replaced by the chainsaw (the prosthetic items, not ported)");
    }
}

// tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:479
public partial class obj_item_nullrod_pride_hammer : obj_item_nullrod
{
    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:498
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:500", "Initialize: AddElement(/datum/element/kneejerk) and AddElement(/datum/element/chemical_transfer, ...): the pride hammer's knee tap and reagent swap");
        return dot;
    }
}

// tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:533
public partial class obj_item_nullrod_fedora : obj_item_nullrod
{
    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:552
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is killing []self with []! It looks like [] trying to get further from god!", user, user.p_them(), this, user.p_theyre())));
        return Dm.bitor(BRUTELOSS, FIRELOSS);
    }
}

// tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:558
public partial class obj_item_nullrod_armblade : obj_item_nullrod
{
    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:576
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NODROP, HAND_REPLACEMENT_TRAIT);
        Dm.Deferred("code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:578", "Initialize: AddComponent(/datum/component/butchering, ...): butchering");
        return dot;
    }
}

// tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:612
public partial class obj_item_nullrod_bostaff : obj_item_nullrod
{
    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:633
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_two_handed), Dm.named("force_unwielded", 14), Dm.named("force_wielded", 18)));
        Dm.Deferred("code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:638", "Initialize: AddComponent(/datum/component/walking_aid): the cane's walking aid");
        return dot;
    }

    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:641
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        inhand_icon_state = Dm.text("[][]", base_icon_state, HAS_TRAIT(this, TRAIT_WIELDED) ? 1 : 0);
        icon_state = Dm.str(inhand_icon_state);
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:645
    public override async DmTask<object> hit_reaction(mob_living_carbon_human owner = null, atom_movable hitby = null, string attack_text = null, float? final_block_chance = null, float? damage = null, float? attack_type = null, string damage_type = null)
    {
        attack_text ??= "the attack";
        final_block_chance ??= 0;
        damage ??= 0;
        attack_type ??= MELEE_ATTACK;
        damage_type ??= BRUTE;
        if (Dm.eq(attack_type, PROJECTILE_ATTACK) || Dm.eq(attack_type, LEAP_ATTACK) || Dm.eq(attack_type, OVERWHELMING_ATTACK))
            final_block_chance = 0; //Don't bring a stick to a gunfight, and also you aren't going to really block someone full body tackling you with a stick. Or a road roller, if one happened to hit you.
        return await base.hit_reaction(owner, hitby, attack_text, final_block_chance, damage, attack_type, damage_type);
    }
}

// tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:652
public partial class obj_item_nullrod_tribal_knife : obj_item_nullrod
{
    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:672
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        START_PROCESSING(SSobj, this);
        alt_continuous = string_list(alt_continuous) as DmList;
        alt_simple = string_list(alt_simple) as DmList;
        Dm.Deferred("code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:676", "Initialize: AddComponent(/datum/component/alternative_sharpness, ...) and the butchering component");
        return dot;
    }

    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:683
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }

    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:687
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        slowdown = rand(-10, 10) / 10f;
        if (iscarbon(loc))
        {
            mob_living_carbon wielder = loc as mob_living_carbon;
            if (Dm.truthy(wielder.is_holding(this)))
                Waiting.NoSleep(wielder.update_equipment_speed_mods());
        }
        return null;
    }
}

// tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:696
public partial class obj_item_nullrod_pitchfork : obj_item_nullrod
{
    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:714
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:716", "Initialize: AddComponent(/datum/component/walking_aid): the cane's walking aid");
        return dot;
    }
}

// tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:720
public partial class obj_item_nullrod_egyptian : obj_item_nullrod
{
    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:736
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:738", "Initialize: AddComponent(/datum/component/walking_aid): the cane's walking aid");
        return dot;
    }
}

// tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:762
public partial class obj_item_nullrod_spear : obj_item_nullrod
{
    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:780
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Dm.Deferred("code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:782", "Initialize: AddComponent(/datum/component/walking_aid): the cane's walking aid");
        return dot;
    }
}

// tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:813
public partial class obj_item_nullrod_nullblade : obj_item_nullrod
{
    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:840
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        alt_continuous = string_list(alt_continuous) as DmList;
        alt_simple = string_list(alt_simple) as DmList;
        Dm.Deferred("code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:844", "Initialize: AddComponent(/datum/component/alternative_sharpness, ...): the right-click's pointy attack");
        return dot;
    }

    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:846
    public override async DmTask<object> pre_attack(atom target = null, mob_living user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        //Check for our user's potential 'strength' value. As a baseline, we'll use a default value of 4 for the sake of nonhuman users.
        float strength_value = 4;
        // We can use our human wielder's arm strength to determine their 'strength'. We add unarmed lower and upper, then divide by four.
        // This isn't how strength works in dnd but who fucking cares.
        if (ishuman(user))
        {
            mob_living_carbon_human human_user = user as mob_living_carbon_human;
            obj_item_bodypart wielding_bodypart = human_user.get_active_hand() as obj_item_bodypart;
            strength_value = Dm.round((wielding_bodypart.unarmed_damage_low + wielding_bodypart.unarmed_damage_high) * 0.25f, 1);
        }
        // Our force becomes 1d6 + strength + some modifier (based on force - base force) to account for whetstones and other things.
        attack_modifiers ??= Dm.list();
        attack_modifiers[FORCE_OVERRIDE] = Dm.roll("1d6") + strength_value + (force - Dm.num(Dm.initial(this, "force"))); // SET_ATTACK_FORCE
        return await base.pre_attack(target, user, modifiers, attack_modifiers);
    }

    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:859
    public override async DmTask afterattack(atom target = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (!isliving(target))
            return;

        mob_living living_target = target as mob_living;

        if (user == living_target)
            return;

        if (Dm.eq(living_target.stat, DEAD) || QDELETED(living_target))
            return;

        await sneak_attack(living_target, user);
    }

    // tg: code/modules/jobs/job_types/chaplain/chaplain_nullrod.dm:874
    public virtual async DmTask sneak_attack(mob_living living_target = null, mob user = null)
    {
        // Did we successfully meet the criteria for a sneak attack?
        bool successful_sneak_attack = false;

        // Did our sneak attack fail due to a special effect?
        bool sneak_attack_fail_message = false;

        // The force our sneak attack applies. Starts as 3d6, then changed based on certain factors.
        float sneak_attack_dice = Dm.roll("3d6");

        // Status effects on the target that grant us sneak attacks
        if (Dm.truthy(living_target.is_blind()))
            successful_sneak_attack = true;
        else if (Dm.truthy(living_target.get_timed_status_effect_duration(typeof(datum_status_effect_staggered))))
            successful_sneak_attack = true;
        else if (Dm.truthy(living_target.get_timed_status_effect_duration(typeof(datum_status_effect_confusion))))
            successful_sneak_attack = true;
        // Our target is in some kind of grapple, which prevents them form protecting themselves.
        else if (living_target.pulledby != null && Dm.num(living_target.pulledby.grab_state) >= GRAB_AGGRESSIVE)
            successful_sneak_attack = true;
        // blocked hands renders you unable to defend yourself properly from an attack
        else if (HAS_TRAIT(living_target, TRAIT_HANDS_BLOCKED))
            successful_sneak_attack = true;
        // Check for various positional outcomes to determine a sneak attack. We want to sneak attack whenever our target is behind.
        else if (Dm.truthy(check_behind(user, living_target)))
            successful_sneak_attack = true;

        /// Now we'll check for things that STOP a sneak attack. Why? Because this mechanic isn't complicated enough and I must insert more ivory tower design.

        if (Dm.truthy(Dm.bitand(living_target.mob_biotypes, MOB_SLIME))) // SLIMES HAVE NO ANATOMY.
        {
            successful_sneak_attack = false;
            sneak_attack_fail_message = true;
        }
        else if (Dm.num(living_target.incorporeal_move) >= 1 && !HAS_TRAIT(living_target, TRAIT_REVENANT_REVEALED)) // WE CAN'T SNEAK ATTACK INCORPOREAL JERKS. BUT WE CAN SNEAK ATTACK REVEALED REVENANTS BECAUSE DUH, NULLROD.
        {
            successful_sneak_attack = false;
            sneak_attack_fail_message = true;
        }
        else if (HAS_TRAIT(living_target, TRAIT_HERETIC_SUMMON) && prob(50)) // IT IS HARD TO SNEAK ATTACK SOMETHING WITH TOO MANY REDUNDANT EVERYTHINGS.
        {
            successful_sneak_attack = false;
            sneak_attack_fail_message = true;
        }
        else if (HAS_TRAIT(living_target, TRAIT_STABLEHEART) && prob(50)) // THEIR ANATOMY IS FUCKING WEIRD.
        {
            successful_sneak_attack = false;
            sneak_attack_fail_message = true;
        }
        else if (HAS_TRAIT(living_target, TRAIT_MIND_READER) && !Dm.truthy(user.can_block_magic(MAGIC_RESISTANCE_MIND, charge_cost: 0))) // FORESIGHT SAYS 'FUCK YOU' TO SNEAK ATTACKERS. BUT IF YOU HAVE A TIN FOIL HAT, YOU'RE SAFE!
        {
            successful_sneak_attack = false;
            sneak_attack_fail_message = true;
        }
        else if (Dm.truthy(user.is_blind())) // YOU CAN'T STAB PRECISELY WHAT YOU CAN'T SEE.
        {
            successful_sneak_attack = false;
            sneak_attack_fail_message = true;
        }

        /// And so we return here if we are not entitled to a sneak attack.
        if (!successful_sneak_attack)
        {
            if (sneak_attack_fail_message)
                user.balloon_alert(living_target, I18n.T("sneak attack avoided!"));
            return;
        }

        /// And now we'll deal with sneak attack damage modifiers.

        // If our target is also unconscious for some reason, we get even more damage. Coup de grace, motherfucker.
        if (IS_UNCONSCIOUS(living_target))
        {
            sneak_attack_dice += Dm.roll("1d6");
            Dm.@new<obj_effect_temp_visual_crit>(get_turf(living_target));
        }

        // If the target is rebuked, we also add some additional damage. It is the closest thing to 'studying' your target, okay?
        if (Dm.truthy(living_target.has_status_effect(typeof(datum_status_effect_rebuked))))
            sneak_attack_dice += 2;

        // If we're morbid, and the target has been dissected, we get an extra d6.
        // The chances of this occuring are quite low, as even having this weapon means you're locked out of becoming morbid as a chaplain, but when it does come up...
        // Or the coroner stole this blade to go hunt the recently dead...
        if (HAS_TRAIT(user, TRAIT_MORBID) && HAS_TRAIT(living_target, TRAIT_DISSECTED))
            sneak_attack_dice += Dm.roll("1d6");

        // Baton + this weapon might be a little too much fun so we're nerfing this combination outright.
        if (HAS_TRAIT(living_target, TRAIT_IWASBATONED))
            sneak_attack_dice *= 0.5f;

        // Affecting body part check.
        obj_item_bodypart affecting = living_target.get_bodypart(Dm.str(user.get_random_valid_zone(user.zone_selected))) as obj_item_bodypart;
        // Target's armor value. Accounts for armor penetration even though we have no armour_penetration defined on the parent.
        float? armor_block = Dm.num(living_target.run_armor_check(affecting, MELEE, armour_penetration: armour_penetration));

        // We got a sneak attack!
        await living_target.apply_damage(Dm.round(sneak_attack_dice, DAMAGE_PRECISION), BRUTE, def_zone: affecting, blocked: armor_block, wound_bonus: exposed_wound_bonus, sharpness: SHARP_EDGED);
        living_target.balloon_alert(user, I18n.T("sneak attack!"));
        playsound(living_target, Dm.resource("sound/items/weapons/guillotine.ogg"), 50, TRUE);
    }
}
