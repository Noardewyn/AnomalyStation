// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/reagent_containers/cups/soda.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps H3 (D333, the bar): the whole file: the soda cans (opening with a pop, the fizziness of a shaken or thrown can that bursts it,
// crushing an empty can on a forehead, the shot can, the fuse and tape of the improvised explosive, the vacuum that bursts a sealed
// can), the random can. The soda spill's mood events are live since gaps MS1. The var blocks are generated.

//////////////////////////soda_cans//
//These are in their own group to be used as IED's in /obj/item/grenade/ghettobomb.dm
// tg: code/modules/reagents/reagent_containers/cups/soda.dm:11
public partial class obj_item_reagent_containers_cup_soda_cans : obj_item_reagent_containers_cup
{
    // tg: code/modules/reagents/reagent_containers/cups/soda.dm:1-6 (the file's #defines, #undef'd at its end)
    /// How much fizziness is added to the can of soda by throwing it, in percentage points
    private const float SODA_FIZZINESS_THROWN = 15;
    /// How much fizziness is added to the can of soda by shaking it, in percentage points
    private const float SODA_FIZZINESS_SHAKE = 5;
    /// At what atmospheric pressure do we burst a soda can? Empirical evidance (one googled experiment video) states that ~67.458 kPa is where a can bursts.
    private const float SODA_EXPLOSION_PRESSURE = 67.458f;

    // tg: code/modules/reagents/reagent_containers/cups/soda.dm:32
    [DmParams("mapload", "vol")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_slapcrafting), string_list(Dm.list(typeof(datum_crafting_recipe_improv_explosive)))));
        Waiting.NoSleep(AddElement(typeof(datum_element_atmos_sensitive), mapload)); //Enables soda cans to explode in vaccuum.
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/soda.dm:43
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        var holder = (datum_reagents)reagents;
        if (!Dm.truthy(holder.total_volume))
        {
            user.visible_message(span_warning(I18n.T("[] is trying to take a big sip from []... The can is empty!", user, this)));
            return SHAME;
        }
        if (!Dm.truthy(is_drainable()))
        {
            await open_soda(user);
            await sleep(1 * SECONDS);
        }
        user.visible_message(span_suicide(I18n.T("[] takes a big sip from []! It looks like [] trying to commit suicide!", user, this, user.p_theyre())));
        playsound(user, Dm.resource("sound/items/drink.ogg"), 80, TRUE);
        await holder.trans_to(user, holder.total_volume, transferred_by: user); //a big sip
        await sleep(0.5f * SECONDS);
        await user.say(Dm.str(pick(
            "Now, Outbomb Cuban Pete, THAT was a game.",
            "All these new fangled arcade games are too slow. I prefer the classics.",
            "They don't make 'em like Orion Trail anymore.",
            "You know what they say. Worst day of spess carp fishing is better than the best day at work.",
            "They don't make 'em like good old-fashioned singularity engines anymore.")));
        if (user is mob_living_carbon_human drinker)
        {
            if (Dm.num(drinker.age) >= 30)
            {
                await drinker.Stun(50);
                await sleep(5 * SECONDS);
                playsound(drinker, Dm.resource("sound/items/drink.ogg"), 80, TRUE);
                await drinker.say(Dm.str(pick(
                    "Another day, another dollar.",
                    "I wonder if I should hold?",
                    "Diversifying is for young'ns.",
                    "Yeap, times were good back then.")));
                return MANUAL_SUICIDE_NONLETHAL;
            }
        }
        await sleep(2 * SECONDS); //dramatic pause
        return TOXLOSS;
    }

    // tg: code/modules/reagents/reagent_containers/cups/soda.dm:79
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        atom target = interacting_with;
        if (!iscarbon(target) || Dm.truthy(((datum_reagents)reagents).total_volume) || !Dm.truthy(user.combat_mode) || !Dm.eq(user.zone_selected, BODY_ZONE_HEAD))
            return await base.interact_with_atom(interacting_with, user, modifiers);
        if (target == user)
        {
            user.visible_message(
                span_warning(I18n.T("[] crushes the can of [] on [] forehead!", user, this, user.p_their())),
                span_notice(I18n.T("You crush the can of [] on your forehead.", this)));
        }
        else
        {
            user.visible_message(
                span_warning(I18n.T("[] crushes the can of [] on []'s forehead!", user, this, target)),
                span_notice(I18n.T("You crush the can of [] on []'s forehead.", this, target)));
        }
        playsound(this, Dm.resource("sound/items/weapons/pierce.ogg"), rand(10, 50), TRUE);
        var crushed_can = Dm.@new<obj_item_trash_can>(target.drop_location());
        crushed_can.icon_state = icon_state;
        qdel(this);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/reagents/reagent_containers/cups/soda.dm:99
    public override async DmTask<object> bullet_act(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, float? blocked = null)
    {
        obj_projectile proj = hitting_projectile;
        object dot = await base.bullet_act(hitting_projectile, def_zone, piercing_hit, blocked);
        if (QDELETED(this))
            return dot;
        if (!Dm.truthy(proj.damage) || !Dm.eq(proj.damage_type, BRUTE))
            return dot;

        var crushed_can = Dm.@new<obj_item_trash_can>(loc);
        crushed_can.icon_state = icon_state;
        var throw_target = get_edge_target_turf(crushed_can, pick(GLOB.alldirs)) as atom;
        await crushed_can.throw_at(throw_target, rand(1, 2), 7);
        qdel(this);
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/soda.dm:111
    public virtual async DmTask open_soda(mob user = null)
    {
        if (Dm.truthy(tape_color))
        {
            to_chat(user, I18n.T("You rip off the tape covering []'s hole.", this));
            playsound(user, Dm.resource("sound/items/duct_tape/duct_tape_rip.ogg"), 50, TRUE);
            tape_color = null;
            add_container_flags(OPENCONTAINER);
            update_appearance();
            return;
        }

        if (prob(fizziness))
        {
            user.visible_message(span_danger(I18n.T("[] opens [], and is suddenly sprayed by the fizzing contents!", user, this)), span_danger(I18n.T("You pull back the tab of [], and are suddenly sprayed with a torrent of liquid! Ahhh!!", this)));
            await burst_soda(user);
            return;
        }

        to_chat(user, I18n.T("You pull back the tab of [] with a satisfying pop.", this)); //Ahhhhhhhh
        add_container_flags(OPENCONTAINER);
        playsound(this, SFX_CAN_OPEN, 50, TRUE);
        throwforce = 0;
    }

    /**
     * Burst the soda open on someone. Fun! Opens and empties the soda can, but does not crush it.
     *
     * Arguments:
     * * target - Who's getting covered in soda
     * * hide_message - Stops the generic fizzing message, so you can do your own
     */
    // tg: code/modules/reagents/reagent_containers/cups/soda.dm:137
    public virtual async DmTask burst_soda(atom target = null, float? hide_message = null)
    {
        hide_message ??= FALSE;
        if (target == null)
            return;

        if (target is mob_living target_mob)
        {
            await target_mob.add_mood_event("soda_spill", typeof(datum_mood_event_soda_spill), this);
            foreach (mob_living iter_mob in Dm.each<mob_living>(view(this, 7)))
            {
                if (iter_mob != target)
                    await iter_mob.add_mood_event("observed_soda_spill", typeof(datum_mood_event_observed_soda_spill), target, this);
            }
        }

        playsound(this, Dm.resource("sound/items/can/can_pop.ogg"), 80, TRUE);
        if (!Dm.truthy(hide_message))
            visible_message(span_danger(I18n.T("[] spills over, fizzing its contents all over []!", this, target)));

        add_container_flags(OPENCONTAINER);
        await ((datum_reagents)reagents).expose(target, TOUCH);
        await ((datum_reagents)reagents).clear_reagents();
        throwforce = 0;
    }

    // tg: code/modules/reagents/reagent_containers/cups/soda.dm:156
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        if (!Dm.truthy(fuse_color))
            return NONE;
        to_chat(user, span_notice(I18n.T("You snip []'s fuse off.", this)));
        tool.play_tool_sound(this, 50);
        add_fingerprint(user);
        fuse_color = null;
        if (fuse_timer != null)
        {
            deltimer(fuse_timer);
            fuse_timer = null;
            log_bomber(user, "has disarmed", this);
        }
        if (Dm.truthy(heatable))
            Waiting.NoSleep(AddElement(typeof(datum_element_reagents_item_heatable)));
        update_appearance();
        return null;
    }

    // tg: code/modules/reagents/reagent_containers/cups/soda.dm:171
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is obj_item_stack_cable_coil coil)
        {
            if (Dm.truthy(fuse_color))
            {
                to_chat(user, span_warning(I18n.T("[] already has a fuse attached to it!", this)));
                return ITEM_INTERACT_BLOCKING;
            }
            if (Dm.truthy(tape_color))
            {
                to_chat(user, span_warning(I18n.T("[]'s hole is covered up with tape!", this)));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(is_drainable()))
            {
                to_chat(user, span_warning(I18n.T("[] hasn't been opened yet!", this)));
                return ITEM_INTERACT_BLOCKING;
            }
            object coil_color = GLOB.cable_colors[coil.cable_color];
            add_fingerprint(user);
            if (!Dm.truthy(await coil.use(1)))
                return ITEM_INTERACT_BLOCKING;

            fuse_color = coil_color;
            // Heating replaced with lighting the fuse
            RemoveElement(typeof(datum_element_reagents_item_heatable));
            to_chat(user, span_notice(I18n.T("You attach a fuse to [].", this)));
            log_bomber(user, "attached a fuse to", this);
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (tool is obj_item_stack_medical_wrap_sticky_tape tape)
        {
            if (Dm.truthy(tape_color))
            {
                to_chat(user, span_warning(I18n.T("[]'s hole is already covered up with tape!", this)));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(is_drainable()))
            {
                to_chat(user, span_warning(I18n.T("[] hasn't been opened yet!", this)));
                return ITEM_INTERACT_BLOCKING;
            }
            var tape_colors = SSgreyscale.ParseColorString(tape.greyscale_colors) as DmList;
            add_fingerprint(user);
            if (!Dm.truthy(await tape.use(1)))
                return ITEM_INTERACT_BLOCKING;

            tape_color = tape_colors?[1];
            to_chat(user, span_notice(I18n.T("You wrap [] up in [].", this, tape)));
            reset_container_flags();
            update_appearance();
            return ITEM_INTERACT_SUCCESS;
        }

        if (!Dm.truthy(fuse_color) || Dm.num(tool.get_temperature()) < FIRE_MINIMUM_TEMPERATURE_TO_EXIST)
            return await base.item_interaction(user, tool, modifiers, __extra);

        if (fuse_timer != null)
        {
            to_chat(user, span_warning(I18n.T("[] is already lit!", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        add_fingerprint(user);
        log_bomber(user, "has primed a rigged", this);
        to_chat(user, span_warning(I18n.T("You light []'s fuse!", this)));
        fuse_timer = _addtimer(Dm.@new<datum_callback>(this, nameof(try_detonate)), rand(2 * SECONDS, 4 * SECONDS), file: "code/modules/reagents/reagent_containers/cups/soda.dm", line: 230);
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/reagents/reagent_containers/cups/soda.dm:234
    public virtual async DmTask try_detonate()
    {
        float spark_flags = SPARK_ACT_WEAKEN_COMMON;
        if (Dm.truthy(tape_color))
            spark_flags = Dm.bitor(spark_flags, SPARK_ACT_ENCLOSED);

        playsound(this, Dm.resource("sound/effects/sparks/sparks1.ogg"), 50, TRUE);
        if (Dm.truthy(Dm.bitand(await ((datum_reagents)reagents).spark_act(0, spark_flags), SPARK_ACT_DESTRUCTIVE)))
        {
            qdel(this);
            return;
        }

        // Was a dud
        fuse_color = null;
        tape_color = null;
        add_container_flags(OPENCONTAINER);
        if (Dm.truthy(heatable))
            Waiting.NoSleep(AddElement(typeof(datum_element_reagents_item_heatable)));
        update_appearance();
    }

    // tg: code/modules/reagents/reagent_containers/cups/soda.dm:252
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (Dm.truthy(fuse_color))
        {
            var fuse_overlay = (mutable_appearance)mutable_appearance(Dm.resource("icons/obj/weapons/grenade.dmi"), "improvised_grenade_fuse");
            fuse_overlay.color = fuse_color;
            Dm.append(ref dot, fuse_overlay);
        }
        if (Dm.truthy(tape_color))
        {
            var tape_overlay = (mutable_appearance)mutable_appearance(Dm.resource("icons/obj/weapons/grenade.dmi"), "improvised_grenade_tape");
            tape_overlay.color = tape_color;
            Dm.append(ref dot, tape_overlay);
        }
        if (fuse_timer != null)
            Dm.append(ref dot, mutable_appearance(Dm.resource("icons/obj/weapons/grenade.dmi"), "improvised_grenade_active"));
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/soda.dm:267
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        object dot = base.throw_impact(hit_atom, throwingdatum, __extra);
        if (Dm.truthy(dot) || Dm.truthy(is_open_container()) || !Dm.truthy(((datum_reagents)reagents).total_volume) || Dm.truthy(tape_color)) // if it was caught, already opened, or has nothing in it
            return dot;

        fizziness += SODA_FIZZINESS_THROWN;
        if (!prob(fizziness))
            return dot;

        Waiting.NoSleep(burst_soda(hit_atom, hide_message: TRUE));
        visible_message(span_danger(I18n.T("[]'s impact with [] causes it to rupture, spilling everywhere!", this, hit_atom)));
        var crushed_can = Dm.@new<obj_item_trash_can>(loc);
        crushed_can.icon_state = icon_state;
        Waiting.NoSleep(moveToNullspace());
        // QDEL_IN(src, 1 SECONDS), expanded inline (qdel.cs): give it a second so it can still be logged for the throw impact
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), 1 * SECONDS > GC_FILTER_QUEUE ? WEAKREF(this) : this), 1 * SECONDS,
            file: "code/modules/reagents/reagent_containers/cups/soda.dm", line: 281);
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/soda.dm:283
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (fuse_timer != null)
        {
            balloon_alert(user, "the fuse is on fire!");
            return null;
        }
        if (!Dm.truthy(is_drainable()))
        {
            await open_soda(user);
            return null;
        }
        return await base.attack_self(user, modifiers, __extra);
    }

    // tg: code/modules/reagents/reagent_containers/cups/soda.dm:294
    public override async DmTask<object> attack_self_secondary(mob user = null, object modifiers = null)
    {
        if (!Dm.truthy(is_drainable()))
        {
            playsound(this, Dm.resource("sound/items/can/can_shake.ogg"), 50, TRUE);
            user.visible_message(span_danger(I18n.T("[] shakes []!", user, this)), span_danger(I18n.T("You shake up []!", this)), vision_distance: 2);
            fizziness += SODA_FIZZINESS_SHAKE;
            return null;
        }
        return await base.attack_self_secondary(user, modifiers);
    }

    // tg: code/modules/reagents/reagent_containers/cups/soda.dm:302
    public override async DmTask<object> examine_more(mob user = null)
    {
        DmList dot = await base.examine_more(user) as DmList;
        if (!Dm.truthy(in_range(user, this)))
            return dot;
        if (fizziness > 30 && prob(fizziness * 2))
        {
            Dm.append(ref dot, span_notice(I18n.T("<i>You examine [] closer, and note the following...</i>", this)));
            Dm.append(ref dot, "\t" + span_warning(I18n.T("You get a menacing aura of fizziness from it...")));
        }
        return dot;
    }

    // tg: code/modules/reagents/reagent_containers/cups/soda.dm:310
    public override object should_atmos_process(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        return Dm.num(air.return_pressure()) <= SODA_EXPLOSION_PRESSURE && !Dm.truthy(Dm.bitand(((datum_reagents)reagents).flags, OPENCONTAINER));
    }

    // tg: code/modules/reagents/reagent_containers/cups/soda.dm:313
    public override async DmTask<object> atmos_expose(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        if (Dm.truthy(((datum_reagents)reagents).total_volume) && !Dm.truthy(Dm.bitand(((datum_reagents)reagents).flags, OPENCONTAINER)))
            await burst_soda(loc as atom);
        return null;
    }
}

// tg: code/modules/reagents/reagent_containers/cups/soda.dm:37
public partial class obj_item_reagent_containers_cup_soda_cans_random : obj_item_reagent_containers_cup_soda_cans
{
    // tg: code/modules/reagents/reagent_containers/cups/soda.dm:37
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        var T = (Type)pick(Dm.sub(subtypesof(typeof(obj_item_reagent_containers_cup_soda_cans)), typeof(obj_item_reagent_containers_cup_soda_cans_random)));
        Dm.@new(T, loc);
        return INITIALIZE_HINT_QDEL;
    }
}

// tg: code/modules/reagents/reagent_containers/cups/soda.dm:346
public partial class obj_item_reagent_containers_cup_soda_cans_lemon_lime : obj_item_reagent_containers_cup_soda_cans
{
    // tg: code/modules/reagents/reagent_containers/cups/soda.dm:354
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        name = "lemon-lime soda";
        return dot;
    }
}

// tg: code/modules/reagents/reagent_containers/cups/soda.dm:482
public partial class obj_item_reagent_containers_cup_soda_cans_beer_rice : obj_item_reagent_containers_cup_soda_cans_beer
{
    // tg: code/modules/reagents/reagent_containers/cups/soda.dm:489
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        string brand = Dm.str(pick("Ebisu Super Dry", "Shimauma Ichiban", "Moonlabor Malt's"));
        name = brand;
        switch (brand)
        {
            case "Ebisu Super Dry":
                icon_state = "ebisu";
                desc = "Mars' favourite rice beer brand, 200 years running.";
                break;
            case "Shimauma Ichiban":
                icon_state = "shimauma";
                desc = "Mars' most middling rice beer brand. Not as popular as Ebisu, but it's comfortable in second place.";
                break;
            case "Moonlabor Malt's":
                icon_state = "moonlabor";
                desc = "Mars' underdog rice beer brand. Popular amongst the Yakuza, for reasons unknown.";
                break;
        }
        return dot;
    }
}
