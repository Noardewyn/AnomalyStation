// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/xenobiology/crossbreeding/regenerative.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the regenerative extracts, crushed over a living creature to heal it fully, each colour with its own effect
// before or after. The var blocks are generated (D111).

/*
Regenerative extracts:
	Work like a legion regenerative core.
	Has a unique additional effect.
*/
// tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:7
public partial class obj_item_slimecross_regenerative : obj_item_slimecross
{
    // tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:13
    public virtual async DmTask core_effect(object target = null, mob user = null)
    {
        await DmTask.CompletedTask;
    }

    // the target is any living creature (tg declares a human; every override and the caller treat it as a living one)
    // tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:15
    public virtual async DmTask core_effect_before(mob_living target = null, mob user = null)
    {
        await DmTask.CompletedTask;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:18
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!isliving(interacting_with))
            return null;
        mob_living H = (mob_living)interacting_with;
        if (H.stat == DEAD)
        {
            to_chat(user, span_warning(I18n.T("[] will not work on the dead!", this)));
            return ITEM_INTERACT_BLOCKING;
        }
        if (H != user)
            user.visible_message(span_notice(I18n.T("[] crushes [] over [], the milky goo quickly regenerating all of [] injuries!", user, this, H, H.p_their())),
                span_notice(I18n.T("You squeeze [], and it bursts over [], the milky goo regenerating [] injuries.", this, H, H.p_their())));
        else
            user.visible_message(span_notice(I18n.T("[] crushes [] over []self, the milky goo quickly regenerating all of [] injuries!", user, this, user.p_them(), user.p_their())),
                span_notice(I18n.T("You squeeze [], and it bursts in your hand, splashing you with milky goo which quickly regenerates your injuries!", this)));
        await core_effect_before(H, user);
        await user.do_attack_animation(interacting_with);
        await H.revive(Dm.bitand(HEAL_ALL, Dm.bitnot(HEAL_REFRESH_ORGANS)));
        await core_effect(H, user);
        playsound(H, "sound/effects/splat.ogg", 40, TRUE);
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:44
public partial class obj_item_slimecross_regenerative_orange : obj_item_slimecross_regenerative
{
    // tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:46
    public override async DmTask core_effect_before(mob_living target = null, mob user = null)
    {
        await DmTask.CompletedTask;
        target.visible_message(span_warning(I18n.T("The [] boils over!", this)));
        foreach (turf targetturf in Dm.each<turf>(RANGE_TURFS(1, target)))
        {
            if (Dm.locate<obj_effect_hotspot>(targetturf) == null)
                Dm.@new<obj_effect_hotspot>(targetturf);
        }
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:53
public partial class obj_item_slimecross_regenerative_purple : obj_item_slimecross_regenerative
{
    // tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:56
    public override async DmTask core_effect(object target = null, mob user = null)
    {
        await ((datum_reagents)((mob_living)target).reagents).add_reagent(typeof(datum_reagent_medicine_regen_jelly), 10);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:60
public partial class obj_item_slimecross_regenerative_blue : obj_item_slimecross_regenerative
{
    // tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:63
    public override async DmTask core_effect(object target = null, mob user = null)
    {
        mob_living living = (mob_living)target;
        if (isturf(living.loc))
        {
            turf_open T = get_turf(living) as turf_open;
            await T.MakeSlippery(TURF_WET_WATER, min_wet_time: 10, wet_time_to_add: 5);
            living.visible_message(span_warning(I18n.T("The milky goo in the extract gets all over the floor!")));
        }
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:70
public partial class obj_item_slimecross_regenerative_metal : obj_item_slimecross_regenerative
{
    // tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:73
    public override async DmTask core_effect(object target = null, mob user = null)
    {
        mob_living living = (mob_living)target;
        living.visible_message(span_warning(I18n.T("The milky goo hardens and reshapes itself, encasing []!", living)));
        obj_structure_closet C = Dm.@new<obj_structure_closet>(living.loc);
        C.name = "slimy closet";
        C.desc = "Looking closer, it seems to be made of a sort of solid, opaque, metal-like goo.";
        if (Dm.num(living.mob_size) > C.max_mob_size) //Prevents capturing megafauna or other large mobs in the closets
        {
            C.bust_open();
            C.visible_message(span_warning(I18n.T("[] is too big, and immediately breaks \\the [] open!", living, C.name)));
        }
        else //This can't be allowed to actually happen to the too-big mobs or it breaks some actions
            await living.forceMove(C);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:85
public partial class obj_item_slimecross_regenerative_yellow : obj_item_slimecross_regenerative
{
    // tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:88
    public override async DmTask core_effect(object target = null, mob user = null)
    {
        await DmTask.CompletedTask;
        mob_living living = (mob_living)target;
        DmList batteries = Dm.list();
        foreach (obj_item_stock_parts_power_store cell in Dm.each<obj_item_stock_parts_power_store>(assoc_to_values(living.get_all_cells() as DmList)))
        {
            if (Dm.num(cell.charge) < Dm.num(cell.maxcharge))
                Dm.append(ref batteries, cell);
        }
        if (batteries.len > 0)
        {
            obj_item_stock_parts_power_store ToCharge = pick(batteries) as obj_item_stock_parts_power_store;
            ToCharge.charge = ToCharge.maxcharge;
            to_chat(living, span_notice(I18n.T("You feel a strange electrical pulse, and one of your electrical items was recharged.")));
        }
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:99
public partial class obj_item_slimecross_regenerative_darkpurple : obj_item_slimecross_regenerative
{
    // tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:102
    public override async DmTask core_effect(object target = null, mob user = null)
    {
        mob_living living = (mob_living)target;
        float equipped = 0;
        equipped += Dm.num(await living.equip_to_slot_or_del(Dm.@new<obj_item_clothing_shoes_sneakers_purple>((object)null), ITEM_SLOT_FEET));
        equipped += Dm.num(await living.equip_to_slot_or_del(Dm.@new<obj_item_clothing_under_color_lightpurple>((object)null), ITEM_SLOT_ICLOTHING));
        equipped += Dm.num(await living.equip_to_slot_or_del(Dm.@new<obj_item_clothing_gloves_color_purple>((object)null), ITEM_SLOT_GLOVES));
        equipped += Dm.num(await living.equip_to_slot_or_del(Dm.@new<obj_item_clothing_head_soft_purple>((object)null), ITEM_SLOT_HEAD));
        if (equipped > 0)
            living.visible_message(span_notice(I18n.T("The milky goo congeals into clothing!")));
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:112
public partial class obj_item_slimecross_regenerative_darkblue : obj_item_slimecross_regenerative
{
    // tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:115
    public override async DmTask core_effect(object target = null, mob user = null)
    {
        if (!ishuman(target))
            return;
        mob_living_carbon_human H = (mob_living_carbon_human)target;
        bool fireproofed = false;
        if (H.get_item_by_slot(ITEM_SLOT_OCLOTHING) != null)
        {
            fireproofed = true;
            obj_item_clothing C = H.get_item_by_slot(ITEM_SLOT_OCLOTHING) as obj_item_clothing;
            await fireproof(C);
        }
        if (H.get_item_by_slot(ITEM_SLOT_HEAD) != null)
        {
            fireproofed = true;
            obj_item_clothing C = H.get_item_by_slot(ITEM_SLOT_HEAD) as obj_item_clothing;
            await fireproof(C);
        }
        if (fireproofed)
            H.visible_message(span_notice(I18n.T("Some of []'s clothing gets coated in the goo, and turns blue!", H)));
    }

    // tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:131
    public virtual async DmTask fireproof(obj_item_clothing clothing_piece = null)
    {
        clothing_piece.name = Dm.text("fireproofed []", clothing_piece.name);
        await clothing_piece.remove_atom_colour(WASHABLE_COLOUR_PRIORITY);
        await clothing_piece.add_atom_colour(color_transition_filter(COLOR_NAVY, SATURATION_OVERRIDE), FIXED_COLOUR_PRIORITY);
        clothing_piece.max_heat_protection_temperature = FIRE_IMMUNITY_MAX_TEMP_PROTECT;
        clothing_piece.heat_protection = clothing_piece.body_parts_covered;
        clothing_piece.resistance_flags = Dm.bitor(clothing_piece.resistance_flags, FIRE_PROOF);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:140
public partial class obj_item_slimecross_regenerative_silver : obj_item_slimecross_regenerative
{
    // tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:143
    public override async DmTask core_effect(object target = null, mob user = null)
    {
        mob_living living = (mob_living)target;
        await living.set_nutrition(NUTRITION_LEVEL_FULL - 1);
        to_chat(living, span_notice(I18n.T("You feel satiated.")));
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:148
public partial class obj_item_slimecross_regenerative_bluespace : obj_item_slimecross_regenerative
{
    // tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:152
    public override async DmTask core_effect(object target = null, mob user = null)
    {
        mob_living living = (mob_living)target;
        turf old_location = get_turf(living);
        if (Dm.truthy(await do_teleport(living, T, channel: TELEPORT_CHANNEL_QUANTUM))) //despite being named a bluespace teleportation method the quantum channel is used to preserve precision teleporting with a bag of holding
        {
            old_location.visible_message(span_warning(I18n.T("[] disappears in a shower of sparks!", living)));
            to_chat(living, span_danger(I18n.T("The milky goo teleports you somewhere it remembers!")));
        }

        if (HAS_TRAIT(living, TRAIT_NO_TELEPORT))
            old_location.visible_message(span_warning(I18n.T("[] sparks briefly, but is prevented from teleporting!", living)));
    }

    // tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:161
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        T = get_turf(this) as turf_open;
        return dot;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:166
public partial class obj_item_slimecross_regenerative_sepia : obj_item_slimecross_regenerative
{
    // tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:169
    public override async DmTask core_effect_before(mob_living target = null, mob user = null)
    {
        to_chat(target, span_notice(I18n.T("You try to forget how you feel.")));
        await target.AddComponent(typeof(datum_component_dejavu));
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:174
public partial class obj_item_slimecross_regenerative_cerulean : obj_item_slimecross_regenerative
{
    // tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:177
    public override async DmTask core_effect(object target = null, mob user = null)
    {
        await forceMove(user.loc);
        obj_item_slimecross X = Dm.@new<obj_item_slimecross_regenerative>(user.loc);
        X.name = name;
        X.desc = desc;
        await user.put_in_active_hand(X);
        to_chat(user, span_notice(I18n.T("Some of the milky goo congeals in your hand!")));
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:186
public partial class obj_item_slimecross_regenerative_pyrite : obj_item_slimecross_regenerative
{
    // tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:189
    public override async DmTask core_effect(object target = null, mob user = null)
    {
        mob_living living = (mob_living)target;
        living.visible_message(span_warning(I18n.T("The milky goo coating [] leaves [] a different color!", living, living.p_them())));
        await living.add_atom_colour(color_transition_filter(rgb(rand(0, 255), rand(0, 255), rand(0, 255)), SATURATION_OVERRIDE), WASHABLE_COLOUR_PRIORITY);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:194
public partial class obj_item_slimecross_regenerative_red : obj_item_slimecross_regenerative
{
    // tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:197
    public override async DmTask core_effect(object target = null, mob user = null)
    {
        mob_living living = (mob_living)target;
        to_chat(living, span_notice(I18n.T("You feel... <i>faster.</i>")));
        await ((datum_reagents)living.reagents).add_reagent(typeof(datum_reagent_medicine_ephedrine), 3);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:202
public partial class obj_item_slimecross_regenerative_green : obj_item_slimecross_regenerative
{
    // tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:205
    public override async DmTask core_effect(object target = null, mob user = null)
    {
        mob_living living = (mob_living)target;
        if (isslime(living))
        {
            living.visible_message(span_warning(I18n.T("The [] suddenly changes color!", living)));
            mob_living_basic_slime target_slime = (mob_living_basic_slime)living;
            target_slime.set_slime_type();
        }
        if (Dm.truthy(Dm.bitand(living.mob_biotypes, MOB_SLIME)))
            await ((datum_reagents)living.reagents).add_reagent(typeof(datum_reagent_mutationtoxin_jelly), 5);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:214
public partial class obj_item_slimecross_regenerative_pink : obj_item_slimecross_regenerative
{
    // tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:217
    public override async DmTask core_effect(object target = null, mob user = null)
    {
        mob_living living = (mob_living)target;
        to_chat(living, span_notice(I18n.T("You feel more calm.")));
        await ((datum_reagents)living.reagents).add_reagent(typeof(datum_reagent_drug_krokodil), 4);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:222
public partial class obj_item_slimecross_regenerative_gold : obj_item_slimecross_regenerative
{
    // tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:225
    public override async DmTask core_effect(object target = null, mob user = null)
    {
        mob_living living = (mob_living)target;
        object newcoin = get_random_coin();
        obj_item_coin C = Dm.@new((Type)newcoin, living.loc) as obj_item_coin;
        playsound(C, "sound/items/coinflip.ogg", 50, TRUE);
        await living.put_in_hand(C);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:232
public partial class obj_item_slimecross_regenerative_oil : obj_item_slimecross_regenerative
{
    // tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:235
    public override async DmTask core_effect(object target = null, mob user = null)
    {
        playsound(this, "sound/items/weapons/flash.ogg", 100, TRUE);
        foreach (mob_living L in Dm.each<mob_living>(view(user, 7)))
            await L.flash_act();
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:241
public partial class obj_item_slimecross_regenerative_black : obj_item_slimecross_regenerative
{
    // tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:244
    public override async DmTask core_effect_before(mob_living target = null, mob user = null)
    {
        Type dummytype = (Type)target.type;
        if (Dm.truthy(Dm.bitand(target.mob_biotypes, MOB_SPECIAL))) //Prevents megafauna and voidwalker duping in a lame way
        {
            dummytype = typeof(mob_living_basic_slime);
            to_chat(user, span_warning(I18n.T("The milky goo flows over [], falling into a weak puddle.", target)));
        }
        mob_living dummy = Dm.@new(dummytype, target.loc) as mob_living;
        to_chat(target, span_notice(I18n.T("The milky goo flows from your skin, forming an imperfect copy of you.")));
        if (iscarbon(target) && iscarbon(dummy))
        {
            mob_living_carbon carbon_target = (mob_living_carbon)target;
            mob_living_carbon carbon_dummy = (mob_living_carbon)dummy;
            carbon_dummy.real_name = carbon_target.real_name;
            await carbon_target.dna.copy_dna(carbon_dummy.dna, Dm.bitor(COPY_DNA_SE, COPY_DNA_SPECIES));
            await carbon_dummy.updateappearance(mutcolor_update: TRUE);
        }
        await dummy.adjust_brute_loss(target.get_brute_loss());
        await dummy.adjust_fire_loss(target.get_fire_loss());
        await dummy.adjust_tox_loss(target.get_tox_loss());
        await dummy.death();
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:263
public partial class obj_item_slimecross_regenerative_lightpink : obj_item_slimecross_regenerative
{
    // tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:266
    public override async DmTask core_effect(object target = null, mob user = null)
    {
        if (!isliving(user))
            return;
        if (target == user)
            return;
        mob_living U = (mob_living)user;
        await U.revive(Dm.bitand(HEAL_ALL, Dm.bitnot(HEAL_REFRESH_ORGANS)));
        to_chat(U, span_notice(I18n.T("Some of the milky goo sprays onto you, as well!")));
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:276
public partial class obj_item_slimecross_regenerative_adamantine : obj_item_slimecross_regenerative
{
    // tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:279
    public override async DmTask core_effect(object target = null, mob user = null) //WIP - Find out why this doesn't work.
    {
        await ((mob_living)target).apply_status_effect(typeof(datum_status_effect_slimeskin));
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:283
public partial class obj_item_slimecross_regenerative_rainbow : obj_item_slimecross_regenerative
{
    // tg: code/modules/research/xenobiology/crossbreeding/regenerative.dm:286
    public override async DmTask core_effect(object target = null, mob user = null)
    {
        await ((mob_living)target).apply_status_effect(typeof(datum_status_effect_rainbow_protection));
    }
}
