// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/water_structures/toilet.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps H1: the whole file (the toilet: its lid and cistern, the flush and its overflow foam, the swirlie and the slam, the items hidden
// in the cistern and dropped in the bowl, the plunger, the water reclaimer and the plumbing; the secret toilet; the flesh toilet). The
// station toilets' fishing spot and the fish-safe storage element wait for the fishing (not-translated.md section 190); fish dropped
// in the bowl are tg's (the fish items are generated). The swirlie's achievement waits with the achievements (D332). The var blocks
// are generated (D111).

// tg: code/game/objects/structures/water_structures/toilet.dm:1
public partial class obj_structure_toilet : obj_structure
{
    // tg: code/game/objects/structures/water_structures/toilet.dm:36
    [DmParams("mapload", "has_water_reclaimer")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object has_water_reclaimer = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        cover_open = round(rand(0, 1));
        if (has_water_reclaimer != null)
            this.has_water_reclaimer = Dm.num(has_water_reclaimer);
        update_appearance(UPDATE_ICON);
        if (Dm.truthy(mapload) && Dm.truthy(SSmapping.level_trait(z, ZTRAIT_STATION)))
            Dm.Deferred("code/game/objects/structures/water_structures/toilet.dm:43", "the toilet's fishing spot (/datum/component/fishing_spot, GLOB.preset_fish_sources) waits for the fishing");
        Dm.Deferred("code/game/objects/structures/water_structures/toilet.dm:44", "the fish-safe storage element (/datum/element/fish_safe_storage) waits for the fishing");
        register_context();
        create_reagents(reagent_capacity);
        if (Dm.truthy(this.has_water_reclaimer))
            Waiting.NoSleep(((datum_reagents)reagents).add_reagent(reagent_id, reagent_capacity));
        Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_simple_demand_extended)));
        return dot;
    }

    // tg: code/game/objects/structures/water_structures/toilet.dm:51
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        base.add_context(source, context, held_item, user);
        if (user.pulling != null && isliving(user.pulling))
            context[SCREENTIP_CONTEXT_LMB] = "Give Swirlie";
        if (Dm.truthy(cover_open))
        {
            if (held_item == null)
            {
                if (Dm.truthy(LAZYLEN(fishes)))
                    context[SCREENTIP_CONTEXT_LMB] = "Grab Fish";
            }
            else if (held_item is obj_item_fish)
                context[SCREENTIP_CONTEXT_LMB] = "Insert Fish";
            else if (held_item is obj_item_plunger)
                context[SCREENTIP_CONTEXT_LMB] = "Unclog";
            else if (held_item.w_class <= WEIGHT_CLASS_SMALL)
                context[SCREENTIP_CONTEXT_LMB] = "Insert Item";
        }
        else if (Dm.truthy(cistern_open))
        {
            if (held_item == null)
                context[SCREENTIP_CONTEXT_LMB] = "Check Cistern";
            else if (Dm.eq(held_item.tool_behaviour, TOOL_SCREWDRIVER) && Dm.truthy(has_water_reclaimer))
                context[SCREENTIP_CONTEXT_LMB] = "Remove Reclaimer";
            else if (held_item is obj_item_stock_parts_water_recycler && !Dm.truthy(has_water_reclaimer))
                context[SCREENTIP_CONTEXT_LMB] = "Install Reclaimer";
            else
                context[SCREENTIP_CONTEXT_LMB] = "Insert Item";
        }
        context[SCREENTIP_CONTEXT_RMB] = "Flush";
        context[SCREENTIP_CONTEXT_ALT_LMB] = Dm.text("[] Lid", Dm.truthy(cover_open) ? "Close" : "Open");
        return CONTEXTUAL_SCREENTIP_SET;
    }

    // tg: code/game/objects/structures/water_structures/toilet.dm:78
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(cover_open))
        {
            if (Dm.truthy(LAZYLEN(fishes)))
                Dm.append(ref dot, span_notice(I18n.T("You can see fish in the toilet, you can probably take one out.")));
            if (stuck_item != null)
                Dm.append(ref dot, span_notice(I18n.T("There seems to be something small in []'s bowl...", this)));
        }
        if (Dm.truthy(cistern_open) && Dm.truthy(has_water_reclaimer))
        {
            Dm.append(ref dot, span_notice(I18n.T("A water recycler is installed. Its attached by a pair of screws.")));
            Dm.append(ref dot, span_notice(I18n.T("Its display states: []/[] liquids remaining.", ((datum_reagents)reagents).total_volume, ((datum_reagents)reagents).maximum_volume)));
        }
        return dot;
    }

    // tg: code/game/objects/structures/water_structures/toilet.dm:89
    public override async DmTask<object> examine_more(mob user = null)
    {
        DmList dot = await base.examine_more(user) as DmList;
        if (Dm.truthy(cistern_open) && Dm.truthy(LAZYLEN(cistern_items)))
            Dm.append(ref dot, span_notice(I18n.T("You can see [] items inside of the cistern.", cistern_items.len)));
        return dot;
    }

    // tg: code/game/objects/structures/water_structures/toilet.dm:94
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        QDEL_LAZYLIST(ref fishes);
        QDEL_LAZYLIST(ref cistern_items);
        qdel(stuck_item);
        stuck_item = null;
        return dot;
    }

    // tg: code/game/objects/structures/water_structures/toilet.dm:100
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (Dm.@in(gone, cistern_items))
        {
            LAZYREMOVE(ref cistern_items, gone);
            if (gone is obj_item removed_item)
                w_items -= removed_item.w_class;
            return dot;
        }
        if (Dm.@in(gone, fishes))
            LAZYREMOVE(ref fishes, gone);
        else if (gone == stuck_item)
            stuck_item = null;
        return dot;
    }

    // tg: code/game/objects/structures/water_structures/toilet.dm:113
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        var living = user as mob_living;

        if (swirlie != null)
        {
            living.changeNext_move(CLICK_CD_MELEE);
            playsound(loc, SFX_SWING_HIT, 25, TRUE);
            swirlie.visible_message(span_danger(I18n.T("[] slams the toilet seat onto []'s head!", living, swirlie)), span_userdanger(I18n.T("[] slams the toilet seat onto your head!", living)), span_hear(I18n.T("You hear reverberating porcelain.")));
            log_combat(living, swirlie, "swirlied (brute)");
            await swirlie.adjust_brute_loss(5);
            return dot;
        }

        if (living.pulling != null && isliving(living.pulling))
        {
            living.changeNext_move(CLICK_CD_MELEE);
            var grabbed_mob = (mob_living)living.pulling;
            if (living.grab_state < GRAB_AGGRESSIVE)
            {
                to_chat(living, span_warning(I18n.T("You need a tighter grip!")));
                return dot;
            }
            if (grabbed_mob.loc != get_turf(this))
            {
                to_chat(living, span_warning(I18n.T("[] needs to be on []!", grabbed_mob, this)));
                return dot;
            }
            if (swirlie != null)
                return dot;
            if (Dm.truthy(cover_open))
            {
                if (!Dm.truthy(((datum_reagents)reagents).total_volume))
                {
                    to_chat(living, span_notice(I18n.T("\\The [] is dry!", this)));
                    return dot;
                }
                grabbed_mob.visible_message(span_danger(I18n.T("[] starts to give [] a swirlie!", living, grabbed_mob)), span_userdanger(I18n.T("[] starts to give you a swirlie...", living)));
                swirlie = grabbed_mob;
                bool was_alive = swirlie.stat != DEAD;
                if (!Dm.truthy(await do_after(living, 3 * SECONDS, target: this, timed_action_flags: IGNORE_HELD_ITEM)))
                {
                    swirlie = null;
                    return dot;
                }
                if (!Dm.truthy(((datum_reagents)reagents).total_volume))
                {
                    to_chat(living, span_notice(I18n.T("\\The [] is dry!", this)));
                    return dot;
                }
                grabbed_mob.visible_message(span_danger(I18n.T("[] gives [] a swirlie!", living, grabbed_mob)), span_userdanger(I18n.T("[] gives you a swirlie!", living)), span_hear(I18n.T("You hear a toilet flushing.")));
                if (grabbed_mob is mob_living_carbon carbon_grabbed)
                {
                    if (carbon_grabbed.@internal == null)
                    {
                        log_combat(living, carbon_grabbed, "swirlied (oxy)");
                        await carbon_grabbed.adjust_oxy_loss(5);
                    }
                }
                else
                {
                    log_combat(living, grabbed_mob, "swirlied (oxy)");
                    await grabbed_mob.adjust_oxy_loss(5);
                }
                if (was_alive && swirlie.stat == DEAD && swirlie.client != null)
                    Dm.Deferred("code/game/objects/structures/water_structures/toilet.dm:160", "swirlie.client.give_award(/datum/award/achievement/misc/swirlie, swirlie): the achievements (D332)"); // just like space high school all over again!
                swirlie = null;
            }
            else
            {
                playsound(loc, Dm.resource("sound/effects/bang.ogg"), 25, TRUE);
                grabbed_mob.visible_message(span_danger(I18n.T("[] slams [] into []!", living, grabbed_mob.name, this)), span_userdanger(I18n.T("[] slams you into []!", living, this)));
                log_combat(living, grabbed_mob, "toilet slammed");
                await grabbed_mob.adjust_brute_loss(5);
            }
            return dot;
        }

        if (Dm.truthy(cistern_open) && !Dm.truthy(cover_open) && Dm.truthy(await IsReachableBy(living)))
        {
            if (!Dm.truthy(LAZYLEN(cistern_items)))
            {
                to_chat(living, span_notice(I18n.T("The cistern is empty.")));
                return dot;
            }
            var random_cistern_item = (obj_item)pick(cistern_items);
            if (ishuman(living))
                await living.put_in_hands(random_cistern_item);
            else
                await random_cistern_item.forceMove(drop_location() as atom);
            to_chat(living, span_notice(I18n.T("You find [] in the cistern.", random_cistern_item)));
            return dot;
        }

        if (!Dm.truthy(flushing) && Dm.truthy(LAZYLEN(fishes)) && Dm.truthy(cover_open))
        {
            var random_fish = (obj_item)pick(fishes);
            if (ishuman(living))
                await living.put_in_hands(random_fish);
            else
                await random_fish.forceMove(drop_location() as atom);
            to_chat(living, span_notice(I18n.T("You take [] out of the toilet, poor thing.", random_fish)));
        }
        return dot;
    }

    // tg: code/game/objects/structures/water_structures/toilet.dm:189
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (Dm.truthy(flushing))
            return CLICK_ACTION_BLOCKING;
        cover_open = Dm.truthy(cover_open) ? FALSE : TRUE;
        update_appearance(UPDATE_ICON);
        await DmTask.CompletedTask;
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/structures/water_structures/toilet.dm:196
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        await base.attack_hand_secondary(user, modifiers);
        if (Dm.truthy(flushing))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        if (((datum_reagents)reagents).total_volume <= 50)
        {
            to_chat(user, span_notice(I18n.T("You press the flush lever, but nothing happens.")));
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }

        flushing = TRUE;
        bool something_stuck = stuck_item != null;
        if (!something_stuck && Dm.truthy(LAZYLEN(fishes)))
        {
            foreach (obj_item_fish fish in Dm.each<obj_item_fish>(fishes))
            {
                if (fish.w_class >= WEIGHT_CLASS_NORMAL)
                {
                    something_stuck = true;
                    break;
                }
            }
        }

        if (something_stuck)
            await ((datum_reagents)reagents).create_foam(typeof(datum_effect_system_fluid_spread_foam), 10, notification: span_danger(I18n.T("[] overflows, spilling its cistern's contents everywhere!", this)), log: TRUE);
        else
            await ((datum_reagents)reagents).remove_all(50);

        begin_reclamation();
        playsound(this, Dm.resource("sound/machines/toilet_flush.ogg"), Dm.truthy(cover_open) ? 40 : 20, TRUE);
        if (Dm.truthy(cover_open) && Dm.truthy(Dm.bitand(dir, SOUTH)))
        {
            update_appearance(UPDATE_OVERLAYS);
            flick_overlay_view(mutable_appearance(icon, Dm.text("[]-water-flick", base_icon_state)), 3 * SECONDS);
        }
        _addtimer(Dm.@new<datum_callback>(this, nameof(end_flushing)), 4 * SECONDS, file: "code/game/objects/structures/water_structures/toilet.dm", line: 223);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/game/objects/structures/water_structures/toilet.dm:226
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][][]", base_icon_state, cover_open, cistern_open);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/structures/water_structures/toilet.dm:230
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (!Dm.truthy(flushing) && Dm.truthy(cover_open))
            Dm.append(ref dot, Dm.text("[]-water", base_icon_state));
        return dot;
    }

    // tg: code/game/objects/structures/water_structures/toilet.dm:235
    public override async DmTask dump_contents(params object[] __extra)
    {
        foreach (obj toilet_item in Dm.each<obj>(Dm.add(cistern_items ?? Dm.list(), fishes ?? Dm.list())))
            await toilet_item.forceMove(drop_location() as atom);
        if (stuck_item != null)
            await stuck_item.forceMove(drop_location() as atom);
    }

    // tg: code/game/objects/structures/water_structures/toilet.dm:240
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        await dump_contents();
        await drop_custom_materials();
        if (Dm.truthy(has_water_reclaimer))
            Dm.@new<obj_item_stock_parts_water_recycler>(drop_location());
    }

    // tg: code/game/objects/structures/water_structures/toilet.dm:246
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(user.combat_mode))
            return NONE;

        add_fingerprint(user);
        if (Dm.truthy(cover_open) && tool is obj_item_fish the_fish)
        {
            if (LAZYLEN(fishes) >= 3)
            {
                to_chat(user, span_warning(I18n.T("There's too many fishes, flush them down first.")));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            {
                to_chat(user, span_warning(I18n.T("\\The [] is stuck to your hand!", tool)));
                return ITEM_INTERACT_BLOCKING;
            }
            if (Dm.eq(the_fish.status, FISH_DEAD))
                to_chat(user, span_warning(I18n.T("You place [] into [], may it rest in peace.", tool, this)));
            else
                to_chat(user, span_notice(I18n.T("You place [] into [], hopefully no one will miss it!", tool, this)));
            LAZYADD(ref fishes, tool);
            return ITEM_INTERACT_SUCCESS;
        }

        if (Dm.truthy(cistern_open))
        {
            if (tool is obj_item_stock_parts_water_recycler)
            {
                if (Dm.truthy(has_water_reclaimer))
                {
                    to_chat(user, span_warning(I18n.T("[] already has a water recycler installed.", this)));
                    return ITEM_INTERACT_BLOCKING;
                }

                playsound(this, Dm.resource("sound/machines/click.ogg"), 20, TRUE);
                qdel(tool);
                has_water_reclaimer = TRUE;
                begin_reclamation();
                return ITEM_INTERACT_SUCCESS;
            }

            if (tool.w_class > WEIGHT_CLASS_NORMAL)
            {
                to_chat(user, span_warning(I18n.T("[] does not fit!", tool)));
                return ITEM_INTERACT_BLOCKING;
            }
            if (w_items + tool.w_class > WEIGHT_CLASS_HUGE)
            {
                to_chat(user, span_warning(I18n.T("The cistern is full!")));
                return ITEM_INTERACT_BLOCKING;
            }
            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            {
                to_chat(user, span_warning(I18n.T("\\The [] is stuck to your hand, you cannot put it in the cistern!", tool)));
                return ITEM_INTERACT_BLOCKING;
            }
            add_cistern_item(tool);
            to_chat(user, span_notice(I18n.T("You carefully place [] into the cistern.", tool)));
            return ITEM_INTERACT_SUCCESS;
        }

        if (!Dm.truthy(cover_open))
            return NONE;

        if (!is_reagent_container(tool))
        {
            if (tool.w_class > WEIGHT_CLASS_SMALL)
                return NONE;

            if (stuck_item != null)
            {
                to_chat(user, span_warning(I18n.T("There's already something blocking []'s drain pipe!", this)));
                return ITEM_INTERACT_BLOCKING;
            }

            if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            {
                to_chat(user, span_warning(I18n.T("\\The [] is stuck to your hand!", tool)));
                return ITEM_INTERACT_BLOCKING;
            }

            stuck_item = tool;
            to_chat(user, span_notice(I18n.T("You drop [] into []'s bowl.", tool, this)));
            return ITEM_INTERACT_SUCCESS;
        }

        if (((datum_reagents)reagents).total_volume <= 0)
        {
            to_chat(user, span_notice(I18n.T("\\The [] is dry.", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        // tg checks the monkey cube here, inside the reagent container branch, so a cube (a food, not a container) never reaches it
        if (tool is obj_item_food_monkeycube cube)
        {
            await cube.Expand();
            return ITEM_INTERACT_SUCCESS;
        }

        var container = (obj_item_reagent_containers)tool;
        if (!Dm.truthy(container.is_refillable()))
            return NONE;

        if (Dm.truthy(((datum_reagents)container.reagents).holder_full()))
        {
            to_chat(user, span_notice(I18n.T("\\The [] is full.", container)));
            return ITEM_INTERACT_BLOCKING;
        }

        await ((datum_reagents)reagents).trans_to(container, container.amount_per_transfer_from_this, transferred_by: user);
        begin_reclamation();
        to_chat(user, span_notice(I18n.T("You fill [] from []. Gross.", container, this)));
        return ITEM_INTERACT_SUCCESS;
    }

    /// Hides an item inside the toilet for later retrievalk
    // tg: code/game/objects/structures/water_structures/toilet.dm:333
    public virtual void add_cistern_item(obj_item thing = null)
    {
        if (isitem(thing))
            w_items += thing.w_class;
        LAZYADD(ref cistern_items, thing);
    }

    // tg: code/game/objects/structures/water_structures/toilet.dm:338
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        to_chat(user, span_notice(Dm.truthy(cistern_open) ? I18n.T("You start to replace the lid on the cistern...") : I18n.T("You start to lift the lid off the cistern...")));
        playsound(loc, Dm.resource("sound/effects/stonedoor_openclose.ogg"), 50, TRUE);
        if (Dm.truthy(await tool.use_tool(this, user, 30)))
        {
            user.visible_message(
                span_notice(Dm.truthy(cistern_open) ? I18n.T("[] replaces the lid on the cistern!", user) : I18n.T("[] lifts the lid off the cistern!", user)),
                span_notice(Dm.truthy(cistern_open) ? I18n.T("You replace the lid on the cistern!") : I18n.T("You lift the lid off the cistern!")),
                span_hear(I18n.T("You hear grinding porcelain.")));
            cistern_open = Dm.truthy(cistern_open) ? FALSE : TRUE;
            update_appearance(UPDATE_ICON_STATE);
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/water_structures/toilet.dm:350
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(cistern_open))
        {
            to_chat(user, span_warning(I18n.T("You need to open []'s cistern first!", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(has_water_reclaimer))
        {
            to_chat(user, span_warning(I18n.T("\\the [] doesn't have a water reclaimer installed.", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        tool.play_tool_sound(this);
        has_water_reclaimer = FALSE;
        Dm.@new<obj_item_stock_parts_water_recycler>(drop_location());
        to_chat(user, span_notice(I18n.T("You remove the water reclaimer from \\the [].", this)));
        await DmTask.CompletedTask;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/water_structures/toilet.dm:365
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        tool.play_tool_sound(this);
        await deconstruct();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/water_structures/toilet.dm:370
    public override async DmTask<object> plunger_act(obj_item_plunger attacking_plunger = null, mob_living user = null, object reinforced = null)
    {
        user.balloon_alert_to_viewers("furiously plunging...");
        if (!Dm.truthy(await do_after(user, 3 * SECONDS, target: this)))
            return TRUE;
        user.balloon_alert_to_viewers("finished plunging");
        await ((datum_reagents)reagents).expose(get_turf(this), TOUCH); //splash on the floor
        await ((datum_reagents)reagents).clear_reagents();
        begin_reclamation();
        if (stuck_item != null)
        {
            await stuck_item.forceMove(drop_location() as atom);
            stuck_item = null;
        }
        return TRUE;
    }

    ///Ends the flushing animation and updates overlays if necessary
    // tg: code/game/objects/structures/water_structures/toilet.dm:384
    public virtual void end_flushing()
    {
        flushing = FALSE;
        if (Dm.truthy(cover_open) && Dm.truthy(Dm.bitand(dir, SOUTH)))
            update_appearance(UPDATE_OVERLAYS);
        QDEL_LAZYLIST(ref fishes);
    }

    // tg: code/game/objects/structures/water_structures/toilet.dm:390
    public virtual void begin_reclamation()
    {
        START_PROCESSING(SSobj, this);
    }

    // tg: code/game/objects/structures/water_structures/toilet.dm:393
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        // Water reclamation complete?
        if (!Dm.truthy(has_water_reclaimer) || ((datum_reagents)reagents).total_volume >= ((datum_reagents)reagents).maximum_volume)
            return PROCESS_KILL;
        Waiting.NoSleep(((datum_reagents)reagents).add_reagent(reagent_id, reclaim_rate * Dm.num(seconds_per_tick)));
        return null;
    }
}

// tg: code/game/objects/structures/water_structures/toilet.dm:404
public partial class obj_structure_toilet_secret : obj_structure_toilet
{
    // tg: code/game/objects/structures/water_structures/toilet.dm:407
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (secret_type == null)
            return dot;
        var secret = (obj_item)Dm.@new((Type)secret_type, this);
        secret.desc = Dm.text("[] It's a secret!", secret.desc);
        add_cistern_item(secret);
        return dot;
    }
}

///A toilet made of meat that only drops remains when deconstructed, often unleashed unto this cursed plane of existence by hopeless people off'ing themselves with experi-scanners.
// tg: code/game/objects/structures/water_structures/toilet.dm:416
public partial class obj_structure_toilet_greyscale_flesh : obj_structure_toilet_greyscale
{
    // tg: code/game/objects/structures/water_structures/toilet.dm:420
    [DmParams("mapload", "suicide")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        var suicide = Dm.arg(__extra, 0) as mob_living;
        // tg's ..() passes the victim on as the toilet's has_water_reclaimer argument, so a suicide's toilet gets a reclaimer (a truthy mob)
        object dot = base.Initialize(mapload, suicide != null ? TRUE : null);
        ///The suicide victim's brain that will be placed inside the toilet's cistern
        obj_item_organ_brain toilet_brain;
        if (suicide != null)
        {
            toilet_brain = (suicide as mob_living_carbon)?.get_organ_slot(ORGAN_SLOT_BRAIN) as obj_item_organ_brain;
            foreach (obj_item thing in Dm.each<obj_item>(suicide.contents.Copy()))
            {
                if (Dm.truthy(Waiting.NoSleep(suicide.transferItemToLoc(thing, newloc: this, silent: TRUE))))
                    add_cistern_item(thing);
            }
            Waiting.NoSleep(suicide.gib(DROP_BRAIN)); //we delete everything but the brain, as it's going to be moved to the cistern
            Waiting.NoSleep(set_custom_materials(Dm.list(Dm.kv(Waiting.NoSleep(SSmaterials.get_material(typeof(datum_material_meat_mob_meat), suicide)), SHEET_MATERIAL_AMOUNT))));
        }
        else
        {
            toilet_brain = Dm.@new<obj_item_organ_brain>(drop_location() as atom);
            Waiting.NoSleep(set_custom_materials(Dm.list(Dm.kv(typeof(datum_material_meat), SHEET_MATERIAL_AMOUNT))));
        }

        if (toilet_brain != null)
        {
            Waiting.NoSleep(toilet_brain.forceMove(this));
            add_cistern_item(toilet_brain);
        }
        return dot;
    }

    //this also prevents the toilet from dropping meat sheets. if you want to cheese the meat exepriments, sacrifice more people
    // tg: code/game/objects/structures/water_structures/toilet.dm:440
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        foreach (obj toilet_item in Dm.each<obj>(cistern_items))
            await toilet_item.forceMove(drop_location() as atom);
        Dm.@new<obj_effect_decal_remains_human>(loc);
    }
}
