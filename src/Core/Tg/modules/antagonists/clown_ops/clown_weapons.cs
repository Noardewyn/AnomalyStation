// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/clown_ops/clown_weapons.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N3, D316: the clown operatives' weapons: the super lube flower, the bananium sword and shield, the bombanana,
// the tear-stache grenade). The var blocks are generated (D111).

//BANANIUM SWORD

// tg: code/modules/antagonists/clown_ops/clown_weapons.dm:18
public partial class obj_item_melee_energy_sword_bananium : obj_item_melee_energy_sword
{
    // tg: code/modules/antagonists/clown_ops/clown_weapons.dm:33
    public override async DmTask<object> make_transformable()
    {
        await AddComponent(
            typeof(datum_component_transforming),
            Dm.named("throw_speed_on", 4),
            Dm.named("attack_verb_continuous_on", Dm.list("slips")),
            Dm.named("attack_verb_simple_on", Dm.list("slip")),
            Dm.named("clumsy_check", FALSE));
        await make_less_slippery();
        RegisterSignal(this, COMSIG_TRANSFORMING_ON_TRANSFORM, nameof(on_transform));
        return null;
    }

    // tg: code/modules/antagonists/clown_ops/clown_weapons.dm:44
    [SignalHandler]
    public override object on_transform(obj_item source = null, mob user = null, object active = null)
    {
        object dot = base.on_transform(source, user, active);
        if (HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE))
        {
            ADD_TRAIT(this, TRAIT_CUSTOM_TAP_SOUND, INNATE_TRAIT);
            hitsound = Dm.resource("sound/misc/slip.ogg");
        }
        else
        {
            REMOVE_TRAIT(this, TRAIT_CUSTOM_TAP_SOUND, INNATE_TRAIT);
            hitsound = Dm.initial(this, nameof(hitsound));
        }
        return dot;
    }

    // tg: code/modules/antagonists/clown_ops/clown_weapons.dm:53
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_HANDS)) && HAS_TRAIT(user, TRAIT_CLUMSY))
            await make_very_slippery();
        else
            await make_less_slippery();
    }

    // tg: code/modules/antagonists/clown_ops/clown_weapons.dm:60
    public virtual async DmTask make_very_slippery()
    {
        await AddComponent(
            typeof(datum_component_slippery),
            Dm.named("knockdown", 8 * SECONDS),
            Dm.named("lube_flags", GALOSHES_DONT_HELP | SLIP_WHEN_CRAWLING),
            Dm.named("force_drop", TRUE),
            Dm.named("can_slip_callback", Dm.@new<datum_callback>(this, nameof(is_active))),
            Dm.named("slip_on_damage", TRUE));
    }

    // tg: code/modules/antagonists/clown_ops/clown_weapons.dm:70
    public virtual async DmTask make_less_slippery()
    {
        await AddComponent(
            typeof(datum_component_slippery),
            Dm.named("knockdown", 4 * SECONDS),
            Dm.named("lube_flags", GALOSHES_DONT_HELP),
            Dm.named("force_drop", FALSE),
            Dm.named("can_slip_callback", Dm.@new<datum_callback>(this, nameof(is_active))),
            Dm.named("slip_on_damage", FALSE));
    }

    // tg: code/modules/antagonists/clown_ops/clown_weapons.dm:80
    public virtual object is_active()
    {
        return HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE) ? TRUE : FALSE;
    }

    // tg: code/modules/antagonists/clown_ops/clown_weapons.dm:83
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_melee_energy_sword_bananium)
            return await base.item_interaction(user, tool, modifiers, __extra);

        // COOLDOWN_FINISHED(src, next_trombone_allowed) (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        if (!(next_trombone_allowed <= world.time))
            return ITEM_INTERACT_BLOCKING;

        next_trombone_allowed = world.time + 5 * SECONDS; // COOLDOWN_START(src, next_trombone_allowed, 5 SECONDS)
        to_chat(user, span_warning(I18n.T("You slap the two swords together. Sadly, they do not seem to fit!")));
        playsound(this, Dm.resource("sound/misc/sadtrombone.ogg"), 50);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/antagonists/clown_ops/clown_weapons.dm:95
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        if (!HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE))
            await attack_self(user);
        user.visible_message(span_suicide(I18n.T("[] is [] []! It looks like [] trying to commit seppuku, but the blade slips off of [] harmlessly!",
            user, pick(I18n.T("slitting [] stomach open with", user.p_their()), I18n.T("falling on")), this, user.p_theyre(), user.p_them())));
        await user.attackby(this, user);
        return SHAME;
    }
}

//BANANIUM SHIELD

// tg: code/modules/antagonists/clown_ops/clown_weapons.dm:104
public partial class obj_item_shield_energy_bananium : obj_item_shield_energy
{
    // tg: code/modules/antagonists/clown_ops/clown_weapons.dm:121
    [SignalHandler]
    public override object on_transform(obj_item source = null, mob user = null, object active = null)
    {
        object dot = base.on_transform(source, user, active);
        if (HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE))
        {
            Waiting.NoSleep(AddComponent(typeof(datum_component_slippery), 6 * SECONDS, GALOSHES_DONT_HELP, Dm.named("slip_on_damage", TRUE), Dm.named("can_slip_callback", Dm.@new<datum_callback>(this, nameof(is_active)))));
            if (HAS_TRAIT(user, TRAIT_CLUMSY))
                Waiting.NoSleep(AddComponent(typeof(datum_component_boomerang), throw_range + 2, TRUE));
            else
                Waiting.NoSleep(AddComponent(typeof(datum_component_boomerang), throw_range + 1));
            ADD_TRAIT(this, TRAIT_CUSTOM_TAP_SOUND, INNATE_TRAIT);
            hitsound = Dm.resource("sound/misc/slip.ogg");
        }
        else
        {
            qdel(GetComponent(typeof(datum_component_slippery)));
            qdel(GetComponent(typeof(datum_component_boomerang)));
            REMOVE_TRAIT(this, TRAIT_CUSTOM_TAP_SOUND, INNATE_TRAIT);
            hitsound = Dm.initial(this, nameof(hitsound));
        }
        return dot;
    }

    // tg: code/modules/antagonists/clown_ops/clown_weapons.dm:138
    public virtual object is_active()
    {
        return HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE) ? TRUE : FALSE;
    }

    // tg: code/modules/antagonists/clown_ops/clown_weapons.dm:141
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (!HAS_TRAIT(this, TRAIT_TRANSFORM_ACTIVE))
            return;

        if (HAS_TRAIT(user, TRAIT_CLUMSY))
            await AddComponent(typeof(datum_component_boomerang), throw_range + 2, TRUE);
        else
            await AddComponent(typeof(datum_component_boomerang), throw_range + 1);
    }
}

//BOMBANANA

// tg: code/modules/antagonists/clown_ops/clown_weapons.dm:159
public partial class obj_item_food_grown_banana_bombanana : obj_item_food_grown_banana
{
    // tg: code/modules/antagonists/clown_ops/clown_weapons.dm:165
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_FOOD_CONSUMED, nameof(on_consumed));
        return dot;
    }

    /// Log whenever someone eats this with an explicit message since it willspawn a live bomb.
    // tg: code/modules/antagonists/clown_ops/clown_weapons.dm:170
    [SignalHandler]
    public virtual object on_consumed(datum source = null, mob_living eater = null, mob feeder = null)
    {
        DmList concatable = Dm.list(Dm.text("[] has eaten a bombanana!", key_name_and_tag(eater)));
        if (feeder != eater)
            Dm.append(ref concatable, Dm.text("This person was fed this by [].", key_name_and_tag(feeder)));

        Dm.append(ref concatable, Dm.text("As a result of this, a bombanana peel will be spawned at [].", AREACOORD(this)));

        object final_string = jointext(concatable, " ");
        log_bomber(details: final_string); // sorta wacks out the traditional "log_bomber" format but it gets the point across better
        return NONE;
    }
}

// tg: code/modules/antagonists/clown_ops/clown_weapons.dm:182
public partial class obj_item_grown_bananapeel_bombanana : obj_item_grown_bananapeel
{
    // tg: code/modules/antagonists/clown_ops/clown_weapons.dm:190
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_slippery), det_time));
        bomb = Dm.@new<obj_item_grenade_syndieminibomb>(this);
        bomb.name = "bombanana peel";
        bomb.det_time = det_time;

        mob potential_user = null;
        if (iscarbon(loc))
        {
            to_chat(loc, span_danger(I18n.T("[] begins to beep.", this)));
            potential_user = loc as mob; // just for fingerprint diagnosis in explosion logging, the on_consumed proc will have provided the necessary context already
        }

        Waiting.NoSleep(bomb.arm_grenade(potential_user, msg: FALSE));
        return dot;
    }

    // tg: code/modules/antagonists/clown_ops/clown_weapons.dm:204
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        QDEL_NULL(ref bomb);
        return dot;
    }

    // tg: code/modules/antagonists/clown_ops/clown_weapons.dm:208
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is deliberately slipping on \\the []! It looks like \\he's trying to commit suicide.", user, this)));
        playsound(loc, Dm.resource("sound/misc/slip.ogg"), 50, TRUE, -1);
        await bomb.arm_grenade(user, 0, FALSE);
        return BRUTELOSS;
    }
}

//TEARSTACHE GRENADE

// tg: code/modules/antagonists/clown_ops/clown_weapons.dm:216
public partial class obj_item_grenade_chem_grenade_teargas_moustache : obj_item_grenade_chem_grenade_teargas
{
    // tg: code/modules/antagonists/clown_ops/clown_weapons.dm:222
    public override async DmTask<object> detonate(mob_living lanced_by = null)
    {
        turf myloc = get_turf(this);
        object dot = await base.detonate(lanced_by);
        if (!Dm.truthy(dot))
            return dot;

        foreach (mob_living_carbon_human victim in Dm.each<mob_living_carbon_human>(view(6, myloc)))
        {
            if (victim.wear_mask is not obj_item_clothing_mask_gas_clown_hat && victim.wear_mask is not obj_item_clothing_mask_gas_mime)
            {
                if (victim.wear_mask == null || Dm.truthy(await victim.dropItemToGround(victim.wear_mask)))
                {
                    var the_stash = Dm.@new<obj_item_clothing_mask_fakemoustache_sticky>();
                    // tg passes two more TRUEs that /mob/proc/equip_to_slot_or_del (mob/inventory.dm) does not declare: DM drops them
                    await victim.equip_to_slot_or_del(the_stash, ITEM_SLOT_MASK, TRUE, TRUE);
                }
            }
        }
        return dot;
    }
}

// tg: code/modules/antagonists/clown_ops/clown_weapons.dm:234
public partial class obj_item_clothing_mask_fakemoustache_sticky : obj_item_clothing_mask_fakemoustache
{
    // tg: code/modules/antagonists/clown_ops/clown_weapons.dm:237
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NODROP, STICKY_MOUSTACHE_TRAIT);
        _addtimer(Dm.@new<datum_callback>(this, nameof(unstick)), unstick_time, file: "code/modules/antagonists/clown_ops/clown_weapons.dm", line: 240);
        return dot;
    }

    // tg: code/modules/antagonists/clown_ops/clown_weapons.dm:242
    public virtual void unstick()
    {
        REMOVE_TRAIT(this, TRAIT_NODROP, STICKY_MOUSTACHE_TRAIT);
    }
}
