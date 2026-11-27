// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/xenobiology/crossbreeding/consuming.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the consuming extracts, which eat food until they have digested enough nutriment and then make five slime
// cookies of their colour, and the cookies with their effects. The var blocks are generated (D111).

/*
Consuming extracts:
	Can eat food items.
	After consuming enough, produces special cookies.
*/
// tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:7
public partial class obj_item_slimecross_consuming : obj_item_slimecross
{
    // tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:18
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!IS_EDIBLE(tool))
            return NONE;

        if (last_produced + cooldown > world.time)
        {
            to_chat(user, span_warning(I18n.T("[] is still digesting after its last meal!", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        datum_reagent nutriments = ((datum_reagents)tool.reagents).has_reagent(typeof(datum_reagent_consumable_nutriment)) as datum_reagent;
        if (nutriments == null)
        {
            to_chat(user, span_warning(I18n.T("[] burbles unhappily at the offering.", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        nutriment_eaten += Dm.num(nutriments.volume);
        to_chat(user, span_notice(I18n.T("[] opens up and swallows [] whole!", this, tool)));
        qdel(tool);
        playsound(this, "sound/items/eatfood.ogg", 20, TRUE);

        if (nutriment_eaten < nutriment_required)
            return ITEM_INTERACT_SUCCESS;

        nutriment_eaten = 0;
        user.visible_message(span_notice(I18n.T("[] swells up and produces a small pile of cookies!", this)));
        playsound(this, "sound/effects/splat.ogg", 40, TRUE);
        last_produced = world.time;
        for (int i = 1; i <= cookies; i++)
        {
            obj_item cookie = await spawncookie() as obj_item;
            cookie.pixel_x = base_pixel_x + rand(-5, 5);
            cookie.pixel_y = base_pixel_y + rand(-5, 5);
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:49
    public virtual async DmTask<object> spawncookie()
    {
        await DmTask.CompletedTask;
        return Dm.@new(cookietype, get_turf(this));
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:53
public partial class obj_item_slime_cookie : obj_item
{
    // tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:65
    public virtual async DmTask do_effect(mob_living M = null, mob user = null)
    {
        await DmTask.CompletedTask;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:68
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!isliving(interacting_with))
            return NONE;
        mob_living living_mob = (mob_living)interacting_with;
        bool fed = false;
        if (living_mob == user)
        {
            living_mob.visible_message(span_notice(I18n.T("[] eats []!", user, this)), span_notice(I18n.T("You eat [].", this)));
            fed = true;
        }
        else
        {
            living_mob.visible_message(span_danger(I18n.T("[] tries to force [] to eat []!", user, living_mob, this)), span_userdanger(I18n.T("[] tries to force you to eat []!", user, this)));
            if (Dm.truthy(await do_after(user, 2 * SECONDS, target: living_mob)))
            {
                fed = true;
                living_mob.visible_message(span_danger(I18n.T("[] forces [] to eat []!", user, living_mob, this)), span_warning(I18n.T("[] forces you to eat [].", user, this)));
            }
        }
        if (fed)
        {
            if (!HAS_TRAIT(living_mob, TRAIT_AGEUSIA))
                to_chat(living_mob, span_notice(I18n.T("You can taste [].", taste)));
            playsound(get_turf(living_mob), "sound/items/eatfood.ogg", 20, TRUE);
            if (Dm.truthy(nutrition))
                await ((datum_reagents)living_mob.reagents).add_reagent(typeof(datum_reagent_consumable_nutriment), nutrition);
            await do_effect(living_mob, user);
            qdel(this);
            return ITEM_INTERACT_SUCCESS;
        }
        return ITEM_INTERACT_BLOCKING;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:110
public partial class obj_item_slime_cookie_orange : obj_item_slime_cookie
{
    // tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:115
    public override async DmTask do_effect(mob_living M = null, mob user = null)
    {
        await M.apply_status_effect(typeof(datum_status_effect_firecookie));
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:124
public partial class obj_item_slime_cookie_purple : obj_item_slime_cookie
{
    // tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:129
    public override async DmTask do_effect(mob_living M = null, mob user = null)
    {
        float need_mob_update = FALSE;
        need_mob_update += Dm.num(await M.adjust_brute_loss(-5, updating_health: FALSE));
        need_mob_update += Dm.num(await M.adjust_fire_loss(-5, updating_health: FALSE));
        need_mob_update += Dm.num(await M.adjust_tox_loss(-5, updating_health: FALSE, forced: TRUE)); //To heal slimepeople.
        need_mob_update += Dm.num(await M.adjust_oxy_loss(-5, updating_health: FALSE));
        need_mob_update += Dm.num(await M.adjust_organ_loss(ORGAN_SLOT_BRAIN, -5));
        if (Dm.truthy(need_mob_update))
            await M.updatehealth();
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:145
public partial class obj_item_slime_cookie_blue : obj_item_slime_cookie
{
    // tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:150
    public override async DmTask do_effect(mob_living M = null, mob user = null)
    {
        await M.apply_status_effect(typeof(datum_status_effect_watercookie));
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:159
public partial class obj_item_slime_cookie_metal : obj_item_slime_cookie
{
    // tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:164
    public override async DmTask do_effect(mob_living M = null, mob user = null)
    {
        await M.apply_status_effect(typeof(datum_status_effect_metalcookie));
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:173
public partial class obj_item_slime_cookie_yellow : obj_item_slime_cookie
{
    // tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:178
    public override async DmTask do_effect(mob_living M = null, mob user = null)
    {
        await M.apply_status_effect(typeof(datum_status_effect_sparkcookie));
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:187
public partial class obj_item_slime_cookie_darkpurple : obj_item_slime_cookie
{
    // tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:192
    public override async DmTask do_effect(mob_living M = null, mob user = null)
    {
        await M.apply_status_effect(typeof(datum_status_effect_toxincookie));
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:201
public partial class obj_item_slime_cookie_darkblue : obj_item_slime_cookie
{
    // tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:206
    public override async DmTask do_effect(mob_living M = null, mob user = null)
    {
        M.adjust_bodytemperature(-110);
        M.extinguish_mob();
        await DmTask.CompletedTask;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:216
public partial class obj_item_slime_cookie_silver : obj_item_slime_cookie
{
    // tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:222
    public override async DmTask do_effect(mob_living M = null, mob user = null)
    {
        await ((datum_reagents)M.reagents).add_reagent(typeof(datum_reagent_consumable_nutriment_stabilized), 10);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:231
public partial class obj_item_slime_cookie_bluespace : obj_item_slime_cookie
{
    // tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:236
    public override async DmTask do_effect(mob_living eater = null, mob user = null)
    {
        DmList area_turfs = get_area_turfs(get_area(get_turf(eater))) as DmList;
        turf target = null;

        while (Dm.truthy(length(area_turfs)))
        {
            turf check_turf = pick_n_take(area_turfs) as turf;
            if (Dm.truthy(is_centcom_level(check_turf.z)))
                continue; // Probably already filtered out by NOTELEPORT but let's just be careful
            if (Dm.truthy(await check_turf.is_blocked_turf()))
                continue;
            target = check_turf;
            break;
        }

        if (target == null)
        {
            await fail_effect(eater);
            return;
        }
        if (!Dm.truthy(await do_teleport(eater, target, 0, asoundin: "sound/effects/phasein.ogg", channel: TELEPORT_CHANNEL_BLUESPACE)))
        {
            await fail_effect(eater);
            return;
        }
        Dm.@new<obj_effect_particle_effect_sparks>(target);
        playsound(target, SFX_SPARKS, 50, TRUE, SHORT_RANGE_SOUND_EXTRARANGE);
    }

    // tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:258
    public virtual async DmTask fail_effect(mob_living eater = null)
    {
        eater.visible_message(
            message: span_warning(I18n.T("[] briefly vanishes... then slams forcefully into the ground", eater)),
            self_message: span_warning(I18n.T("You briefly vanish... and are returned forcefully to the ground."))
        );
        await eater.Knockdown(0.1 * SECONDS);
        Dm.@new<obj_effect_particle_effect_sparks>(get_turf(eater));
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:272
public partial class obj_item_slime_cookie_sepia : obj_item_slime_cookie
{
    // tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:277
    public override async DmTask do_effect(mob_living M = null, mob user = null)
    {
        await M.apply_status_effect(typeof(datum_status_effect_timecookie));
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:287
public partial class obj_item_slime_cookie_cerulean : obj_item_slime_cookie
{
    // tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:292
    public override async DmTask do_effect(mob_living M = null, mob user = null)
    {
        await DmTask.CompletedTask;
        if (prob(50))
        {
            to_chat(M, span_notice(I18n.T("A piece of [] breaks off while you chew, and falls to the ground.", this)));
            obj_item_slime_cookie_cerulean C = Dm.@new<obj_item_slime_cookie_cerulean>(get_turf(M));
            C.taste = taste + " and a sugar cookie";
        }
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:304
public partial class obj_item_slime_cookie_pyrite : obj_item_slime_cookie
{
    // tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:310
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        string tastemessage = "paint remover";
        switch ((int)rand(1, 7))
        {
            case 1:
                tastemessage = "red dye";
                colour = COLOR_RED;
                break;
            case 2:
                tastemessage = "orange dye";
                colour = "#FFA500";
                break;
            case 3:
                tastemessage = "yellow dye";
                colour = COLOR_YELLOW;
                break;
            case 4:
                tastemessage = "green dye";
                colour = COLOR_VIBRANT_LIME;
                break;
            case 5:
                tastemessage = "blue dye";
                colour = COLOR_BLUE;
                break;
            case 6:
                tastemessage = "indigo dye";
                colour = "#4B0082";
                break;
            case 7:
                tastemessage = "violet dye";
                colour = COLOR_MAGENTA;
                break;
        }
        taste += tastemessage;
        return dot;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:337
    public override async DmTask do_effect(mob_living M = null, mob user = null)
    {
        await M.add_atom_colour(colour, WASHABLE_COLOUR_PRIORITY);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:346
public partial class obj_item_slime_cookie_red : obj_item_slime_cookie
{
    // tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:351
    public override async DmTask do_effect(mob_living M = null, mob user = null)
    {
        Dm.@new<obj_effect_decal_cleanable_blood>(get_turf(M));
        playsound(get_turf(M), "sound/effects/splat.ogg", 10, TRUE);
        if (iscarbon(M))
        {
            mob_living_carbon C = (mob_living_carbon)M;
            C.adjust_blood_volume(25); //Half a vampire drain.
        }
        await DmTask.CompletedTask;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:364
public partial class obj_item_slime_cookie_green : obj_item_slime_cookie
{
    // tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:369
    public override async DmTask do_effect(mob_living M = null, mob user = null)
    {
        if (ishuman(M))
        {
            mob_living_carbon_human H = (mob_living_carbon_human)M;
            await H.vomit(VOMIT_CATEGORY_DEFAULT, lost_nutrition: 25);
        }
        await ((datum_reagents)M.reagents).remove_all();
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:381
public partial class obj_item_slime_cookie_pink : obj_item_slime_cookie
{
    // tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:386
    public override async DmTask do_effect(mob_living M = null, mob user = null)
    {
        await M.apply_status_effect(typeof(datum_status_effect_lovecookie));
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:395
public partial class obj_item_slime_cookie_gold : obj_item_slime_cookie
{
    // tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:400
    public override async DmTask do_effect(mob_living M = null, mob user = null)
    {
        obj_item held = M.get_active_held_item() as obj_item; //This should be itself, but just in case...
        await M.dropItemToGround(held);
        Type newcoin = typeof(obj_item_coin_gold);
        obj_item_coin C = Dm.@new(newcoin, get_turf(M)) as obj_item_coin;
        playsound(get_turf(C), "sound/items/coinflip.ogg", 50, TRUE);
        await M.put_in_hand(C);
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:414
public partial class obj_item_slime_cookie_oil : obj_item_slime_cookie
{
    // tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:419
    public override async DmTask do_effect(mob_living M = null, mob user = null)
    {
        await M.apply_status_effect(typeof(datum_status_effect_tarcookie));
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:428
public partial class obj_item_slime_cookie_black : obj_item_slime_cookie
{
    // tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:433
    public override async DmTask do_effect(mob_living M = null, mob user = null)
    {
        await M.apply_status_effect(typeof(datum_status_effect_spookcookie));
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:442
public partial class obj_item_slime_cookie_lightpink : obj_item_slime_cookie
{
    // tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:447
    public override async DmTask do_effect(mob_living M = null, mob user = null)
    {
        await M.apply_status_effect(typeof(datum_status_effect_peacecookie));
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:456
public partial class obj_item_slime_cookie_adamantine : obj_item_slime_cookie
{
    // tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:461
    public override async DmTask do_effect(mob_living M = null, mob user = null)
    {
        await M.apply_status_effect(typeof(datum_status_effect_adamantinecookie));
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:465
public partial class obj_item_slimecross_consuming_rainbow : obj_item_slimecross_consuming
{
    // tg: code/modules/research/xenobiology/crossbreeding/consuming.dm:468
    public override async DmTask<object> spawncookie()
    {
        await DmTask.CompletedTask;
        object cookie_type = pick(subtypesof(typeof(obj_item_slime_cookie)));
        obj_item_slime_cookie S = Dm.@new((Type)cookie_type, get_turf(this)) as obj_item_slime_cookie;
        S.name = "rainbow cookie";
        S.desc = "A beautiful rainbow cookie, constantly shifting colors in the light.";
        S.icon_state = "rainbow";
        return S;
    }
}
