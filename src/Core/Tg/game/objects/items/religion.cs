// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/religion.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DE1 (D333): the whole file's procs (its crusader armour and the banner recipes are var-only types, live before). A banner
// raised in the hand (tg's rustle, the user swelling for a moment by tg's animate) inspires its bearer and the humans within four
// tiles of its department (or who meet its own test: the hurt for Meditopia, the burning for Sciencia, the mindshielded for
// Command): 15 brute and burn healed and four seconds off every stun, with the staff's healing chime, and the department's extra
// (Meditopia's toxin and oxygen heal and inaprovaline, Engitopia's radiation cured), once a minute; the mundane banners only show
// their colours. The banner backpacks hold more, the godstaves recolour what they wave at after two seconds of chanting, and the
// weak and ceremonial claymores stab less deeply (weaponry/melee/claymore.dm's claymore is translated for them). The var blocks are
// generated (D111).

// tg: code/game/objects/items/religion.dm:1
public partial class obj_item_banner : obj_item
{
    // tg: code/game/objects/items/religion.dm:23
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(inspiration_available))
            Dm.append(ref dot, span_notice(I18n.T("Activate it in your hand to inspire nearby allies of this banner's allegiance!")));
        return dot;
    }

    // tg: code/game/objects/items/religion.dm:28
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(inspiration_available) || Dm.truthy(Dm.bitand(flags_1, HOLOGRAM_1)))
            return null;
        if (morale_time > world.time)
        {
            to_chat(user, span_warning(I18n.T("You aren't feeling inspired enough to flourish [] again yet.", this)));
            return null;
        }
        user.visible_message(I18n.T("<span class='big notice'>[] flourishes []!</span>", user, this),
            span_notice(I18n.T("You raise [] skywards, inspiring your allies!", this)));
        playsound(this, SFX_RUSTLE, 100, FALSE);
        if (Dm.truthy(warcry))
            await user.say(Dm.text("[]", warcry), forced: "banner");
        object old_transform = user.transform;
        user.transform = Dm.mul(user.transform, 1.2f);
        Dm.animate(user, Dm.named("transform", old_transform), Dm.named("time", 10));
        morale_time = world.time + morale_cooldown;

        DmList inspired = Dm.list();
        bool has_job_loyalties = job_loyalties != NONE;
        float has_role_loyalties = LAZYLEN(role_loyalties);
        Dm.append(ref inspired, user); //The user is always inspired, regardless of loyalties
        foreach (mob_living_carbon_human H in Dm.each<mob_living_carbon_human>(range(4, get_turf(this))))
        {
            if (H.stat == DEAD || H == user)
                continue;
            if (H.mind != null && (has_job_loyalties || Dm.truthy(has_role_loyalties)))
            {
                if (has_job_loyalties && Dm.truthy(Dm.bitand(H.mind.assigned_role.departments_bitflags, job_loyalties)))
                    Dm.append(ref inspired, H);
                else if (Dm.truthy(has_role_loyalties) && Dm.truthy(length(Dm.bitand(H.mind.get_special_roles(), role_loyalties))))
                    Dm.append(ref inspired, H);
            }
            else if (Dm.truthy(check_inspiration(H)))
                Dm.append(ref inspired, H);
        }

        foreach (object V in Dm.each(inspired))
        {
            mob_living_carbon_human H = V as mob_living_carbon_human;
            if (H != user)
                to_chat(H, span_notice(I18n.T("Your confidence surges as [] flourishes [] []!", user, user.p_their(), name)));
            await inspiration(H);
            await special_inspiration(H);
        }
        return null;
    }

    // tg: code/game/objects/items/religion.dm:66
    public virtual object check_inspiration(mob_living_carbon_human H = null) //Banner-specific conditions for being eligible
    {
        return null;
    }

    // tg: code/game/objects/items/religion.dm:69
    public virtual async DmTask inspiration(mob_living_carbon_human inspired_human = null)
    {
        float need_mob_update = FALSE;
        need_mob_update += Dm.num(await inspired_human.adjust_brute_loss(-15, updating_health: FALSE));
        need_mob_update += Dm.num(await inspired_human.adjust_fire_loss(-15, updating_health: FALSE));
        if (Dm.truthy(need_mob_update))
            await inspired_human.updatehealth();
        await inspired_human.AdjustStun(-4 * SECONDS);
        await inspired_human.AdjustKnockdown(-4 * SECONDS);
        await inspired_human.AdjustImmobilized(-4 * SECONDS);
        await inspired_human.AdjustParalyzed(-4 * SECONDS);
        await inspired_human.AdjustUnconscious(-4 * SECONDS);
        playsound(inspired_human, Dm.resource("sound/effects/magic/staff_healing.ogg"), 25, FALSE);
    }

    // tg: code/game/objects/items/religion.dm:82
    public virtual async DmTask special_inspiration(mob_living_carbon_human H = null) //Any banner-specific inspiration effects go here
    {
        return;
    }
}

// tg: code/game/objects/items/religion.dm:85
public partial class obj_item_banner_security : obj_item_banner
{
    // tg: code/game/objects/items/religion.dm:92
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        job_loyalties = DEPARTMENT_BITFLAG_SECURITY;
        return dot;
    }
}

// tg: code/game/objects/items/religion.dm:107
public partial class obj_item_banner_medical : obj_item_banner
{
    // tg: code/game/objects/items/religion.dm:114
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        job_loyalties = DEPARTMENT_BITFLAG_MEDICAL;
        return dot;
    }

    // tg: code/game/objects/items/religion.dm:121
    public override object check_inspiration(mob_living_carbon_human H = null)
    {
        return H.stat; //Meditopia is moved to help those in need
    }

    // tg: code/game/objects/items/religion.dm:132
    public override async DmTask special_inspiration(mob_living_carbon_human inspired_human = null)
    {
        float need_mob_update = FALSE;
        need_mob_update += Dm.num(await inspired_human.adjust_tox_loss(-15, updating_health: FALSE));
        need_mob_update += Dm.num(await inspired_human.set_oxy_loss(0, updating_health: FALSE));
        if (Dm.truthy(need_mob_update))
            await inspired_human.updatehealth();
        await ((datum_reagents)inspired_human.reagents).add_reagent(typeof(datum_reagent_medicine_inaprovaline), 5);
    }
}

// tg: code/game/objects/items/religion.dm:140
public partial class obj_item_banner_science : obj_item_banner
{
    // tg: code/game/objects/items/religion.dm:147
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        job_loyalties = DEPARTMENT_BITFLAG_SCIENCE;
        return dot;
    }

    // tg: code/game/objects/items/religion.dm:154
    public override object check_inspiration(mob_living_carbon_human H = null)
    {
        return H.on_fire; //Sciencia is pleased by dedication to the art of Ordnance
    }
}

// tg: code/game/objects/items/religion.dm:165
public partial class obj_item_banner_cargo : obj_item_banner
{
    // tg: code/game/objects/items/religion.dm:172
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        job_loyalties = DEPARTMENT_BITFLAG_CARGO;
        return dot;
    }
}

// tg: code/game/objects/items/religion.dm:187
public partial class obj_item_banner_engineering : obj_item_banner
{
    // tg: code/game/objects/items/religion.dm:194
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        job_loyalties = DEPARTMENT_BITFLAG_ENGINEERING;
        return dot;
    }

    // tg: code/game/objects/items/religion.dm:201
    public override async DmTask special_inspiration(mob_living_carbon_human H = null)
    {
        qdel(H.GetComponent(typeof(datum_component_irradiated)) as datum);
    }
}

// tg: code/game/objects/items/religion.dm:212
public partial class obj_item_banner_command : obj_item_banner
{
    // tg: code/game/objects/items/religion.dm:218
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        job_loyalties = DEPARTMENT_BITFLAG_COMMAND;
        return dot;
    }

    // tg: code/game/objects/items/religion.dm:225
    public override object check_inspiration(mob_living_carbon_human H = null)
    {
        return Dm.num(HAS_TRAIT(H, TRAIT_MINDSHIELD)); //Command is stalwart but rewards their allies.
    }
}

// tg: code/game/objects/items/religion.dm:248
public partial class obj_item_storage_backpack_bannerpack : obj_item_storage_backpack
{
    // tg: code/game/objects/items/religion.dm:253
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        atom_storage.max_total_storage = 27; //6 more then normal, for the tradeoff of declaring yourself an antag at all times.
        return dot;
    }
}

//Structure conversion staff
// tg: code/game/objects/items/religion.dm:328
public partial class obj_item_godstaff : obj_item
{
    // tg: code/game/objects/items/religion.dm:341
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_walking_aid)));
        return dot;
    }

    // tg: code/game/objects/items/religion.dm:345
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (HAS_TRAIT(interacting_with, TRAIT_COMBAT_MODE_SKIP_INTERACTION) && Dm.truthy(user.combat_mode)) // SHOULD_SKIP_INTERACTION (__DEFINES/tools.dm:62), inline
            return NONE;
        return await ranged_interact_with_atom(interacting_with, user, modifiers);
    }

    // tg: code/game/objects/items/religion.dm:350
    public override async DmTask<object> ranged_interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (staffcooldown + staffwait > world.time)
            return ITEM_INTERACT_BLOCKING;

        user.visible_message(span_notice(I18n.T("[] chants deeply and waves [] staff!", user, user.p_their())));
        if (Dm.truthy(await do_after(user, 2 * SECONDS, interacting_with)))
            await interacting_with.add_atom_colour(conversion_color, WASHABLE_COLOUR_PRIORITY); //wololo
        staffcooldown = world.time;
        return ITEM_INTERACT_SUCCESS;
    }
}

// tg: code/game/objects/items/religion.dm:413
public partial class obj_item_claymore_weak : obj_item_claymore
{
    // tg: code/game/objects/items/religion.dm:422
    public override async DmTask make_stabby()
    {
        await AddComponent(typeof(datum_component_alternative_sharpness), SHARP_POINTY, alt_continuous, alt_simple, -9);
    }
}

// tg: code/game/objects/items/religion.dm:425
public partial class obj_item_claymore_weak_ceremonial : obj_item_claymore_weak
{
    // tg: code/game/objects/items/religion.dm:431
    public override async DmTask make_stabby()
    {
        await AddComponent(typeof(datum_component_alternative_sharpness), SHARP_POINTY, alt_continuous, alt_simple, -5);
    }
}
