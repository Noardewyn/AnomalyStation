// Translated from tgstation (snapshot 1d99bd7f): code/modules/recycling/disposal/bin.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (D325: the disposal system): the disposal machine (its air, the flush into the pipes, the expel of what comes back,
// stuffing a mob or a body bag in, the rat king's rummage, a shove into it, a push broom's sweep), the disposal bin (its window,
// DisposalUnit; its pump charging from the room's air; the mounted destination tagger; the critter living in it) and the delivery
// chute. The bin's emissive lights are DV12.

public partial class datum_controller_global_vars : datum_controller
{
    /// Number of animals infesting disposal bins.
    // tg: code/modules/recycling/disposal/bin.dm:2  GLOBAL_VAR_INIT(disposals_animals_spawned, 0)
    public object disposals_animals_spawned;

    // tg: code/modules/recycling/disposal/bin.dm:2  (GLOBAL_MANAGED, __DEFINES/__globals.dm:5)
    public virtual void InitGlobaldisposals_animals_spawned()
    {
        disposals_animals_spawned = 0;
        Dm.append(ref gvars_datum_init_order, "disposals_animals_spawned");
    }
}

// tg: code/modules/recycling/disposal/bin.dm:9
public partial class obj_machinery_disposal : obj_machinery
{
    // tg: code/modules/recycling/disposal/bin.dm:4
    public const float CONTAINS_ANIMAL_CHANCE = 5;
    // tg: code/modules/recycling/disposal/bin.dm:5
    public const float MAXIMUM_ANIMAL_SPAWNS = 1;
    // tg: code/modules/recycling/disposal/bin.dm:6
    public const float ANIMAL_SHAKE_CHANCE = 20;
    // tg: code/modules/recycling/disposal/bin.dm:7
    public const float SEND_PRESSURE = 0.05f * ONE_ATMOSPHERE;

    // create a new disposal
    // find the attached trunk (if present) and init gas resvr.
    // tg: code/modules/recycling/disposal/bin.dm:50
    [DmParams("mapload", "make_from")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_structure_disposalconstruct make_from = Dm.arg<obj_structure_disposalconstruct>(__extra, 0);
        object dot = base.Initialize(mapload, Dm.forward(__extra, make_from));

        if (make_from != null)
        {
            Waiting.NoSleep(setDir(make_from.dir));
            Waiting.NoSleep(make_from.moveToNullspace());
            stored = make_from;
            pressure_charging = FALSE; // newly built disposal bins start with pump off
        }
        else
            stored = Dm.@new<obj_structure_disposalconstruct>(null, null, SOUTH, FALSE, this);

        trunk_check();

        air_contents = Dm.@new<datum_gas_mixture>();
        //gas.volume = 1.05 * CELLSTANDARD
        update_appearance();
        RegisterSignal(this, COMSIG_RAT_INTERACT, nameof(on_rat_rummage));
        RegisterSignal(this, COMSIG_STORAGE_DUMP_CONTENT, nameof(on_storage_dump));
        DmList loc_connections = TgState.Current.obj_machinery_disposal_loc_connections ??= Dm.list(
            Dm.kv(COMSIG_LIVING_DISARM_COLLIDE, nameof(trash_living)),
            Dm.kv(COMSIG_TURF_RECEIVE_SWEEPED_ITEMS, nameof(ready_for_trash)));
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), loc_connections));
        ADD_TRAIT(this, TRAIT_COMBAT_MODE_SKIP_INTERACTION, INNATE_TRAIT);

        return INITIALIZE_HINT_LATELOAD; //we need turfs to have air
    }

    // tg: code/modules/recycling/disposal/bin.dm:77
    public override object AllowDrop()
    {
        return TRUE;
    }

    /// Checks if there a connecting trunk diposal pipe under the disposal
    // tg: code/modules/recycling/disposal/bin.dm:81
    public virtual void trunk_check()
    {
        obj_structure_disposalpipe_trunk found_trunk = Dm.locate<obj_structure_disposalpipe_trunk>(loc);
        if (found_trunk == null)
        {
            pressure_charging = FALSE;
            flush = FALSE;
        }
        else
        {
            if (Dm.truthy(Dm.initial(this, nameof(pressure_charging))))
                pressure_charging = TRUE;
            flush = Dm.num(Dm.initial(this, nameof(flush)));

            found_trunk.set_linked(this); // link the pipe trunk to self
            trunk = found_trunk;
        }
    }

    // tg: code/modules/recycling/disposal/bin.dm:94
    public override object Destroy(float? force = null)
    {
        Waiting.NoSleep(eject());
        if (trunk != null)
        {
            trunk.linked = null;
            trunk = null;
        }
        return base.Destroy(force);
    }

    // tg: code/modules/recycling/disposal/bin.dm:101
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == stored && !QDELETED(this))
        {
            stored = null;
            await deconstruct(FALSE);
        }
        return dot;
    }

    // tg: code/modules/recycling/disposal/bin.dm:107
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        await base.singularity_pull(singularity, current_size);
        if (Dm.num(current_size) >= STAGE_FIVE)
            await deconstruct();
        return null;
    }

    // tg: code/modules/recycling/disposal/bin.dm:112
    public override async DmTask post_machine_initialize()
    {
        await base.post_machine_initialize();
        //this will get a copy of the air turf and take a SEND PRESSURE amount of air from it
        atom L = loc;
        datum_gas_mixture env = Dm.@new<datum_gas_mixture>();
        env.copy_from(await L.return_air() as datum_gas_mixture);
        datum_gas_mixture removed = env.remove(SEND_PRESSURE + 1) as datum_gas_mixture;
        air_contents.merge(removed);
        trunk_check();
    }

    // tg: code/modules/recycling/disposal/bin.dm:122
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        add_fingerprint(user);
        if (!Dm.truthy(user.combat_mode) || Dm.truthy(Dm.bitand(tool.item_flags, NOBLUDGEON)))
        {
            if (Dm.truthy(Dm.bitand(tool.item_flags, ABSTRACT)))
                return ITEM_INTERACT_BLOCKING;
            if (Dm.truthy(await place_item_in_disposal(tool, user)))
            {
                update_appearance();
                return ITEM_INTERACT_SUCCESS;
            }
        }
        return NONE;
    }

    // tg: code/modules/recycling/disposal/bin.dm:132
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(pressure_charging) || Dm.truthy(full_pressure) || Dm.truthy(flush))
            return NONE;
        toggle_panel_open();
        tool.play_tool_sound(this);
        to_chat(user, span_notice(I18n.T("You [] the screws around the power connection.", Dm.truthy(panel_open) ? "remove" : "attach")));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/recycling/disposal/bin.dm:140
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(pressure_charging) || Dm.truthy(full_pressure) || Dm.truthy(flush) || !Dm.truthy(panel_open))
            return NONE;
        if (!Dm.truthy(tool.tool_start_check(user, amount: 1, heat_required: HIGH_TEMPERATURE_REQUIRED)))
            return ITEM_INTERACT_BLOCKING;
        to_chat(user, span_notice(I18n.T("You start slicing the floorweld off \\the []...", this)));
        if (!Dm.truthy(await tool.use_tool(this, user, 20, volume: SMALL_MATERIAL_AMOUNT)) || !Dm.truthy(panel_open))
            return ITEM_INTERACT_BLOCKING;
        to_chat(user, span_notice(I18n.T("You slice the floorweld off \\the [].", this)));
        await deconstruct();
        return ITEM_INTERACT_SUCCESS;
    }

    /// The regal rat spawns ratty treasures from the disposal
    // tg: code/modules/recycling/disposal/bin.dm:153
    public virtual async DmTask rat_rummage(mob_living_basic_regal_rat king = null)
    {
        king.visible_message(span_warning(I18n.T("[] starts rummaging through [].", king, this)), span_notice(I18n.T("You rummage through []...", this)));
        if (!Dm.truthy(await do_after(king, 2 * SECONDS, this, interaction_key: "regalrat")))
            return;

        bool cheese = false;
        float loot = rand(1, 100);
        if (loot >= 1 && loot <= 5)
        {
            to_chat(king, span_notice(I18n.T("You find some leftover coins. More for the royal treasury!")));
            object pickedcoin = pick(GLOB.ratking_coins);
            float coins = rand(1, 3);
            for (int i = 1; i <= coins; i++)
                Dm.@new((Type)pickedcoin, king.drop_location());
        }
        else if (loot >= 6 && loot <= 33)
        {
            cheese = true;
            await king.say(pick("Treasure!", "Our precious!", "Cheese!"), ignore_spam: TRUE, forced: "regal rat rummaging");
            to_chat(king, span_notice(I18n.T("Score! You find some cheese!")));
            Dm.@new<obj_item_food_cheese_wedge>(king.drop_location());
        }
        else
        {
            object pickedtrash = pick(GLOB.ratking_trash);
            to_chat(king, span_notice(I18n.T("You just find more garbage and dirt. Lovely, but beneath you now.")));
            Dm.@new((Type)pickedtrash, king.drop_location());
        }

        if (cheese)
            return; // We don't want them to eat your reward
        float rat_cap = Dm.num(config.Get(typeof(datum_config_entry_number_ratcap))) /* CONFIG_GET(number/ratcap) */;
        if (LAZYLEN(datum_controller_subsystem_mobs.cheeserats) < rat_cap && prob(33))
        {
            mob_living_basic_mouse new_subject = Dm.@new<mob_living_basic_mouse>(king.drop_location());
            ADD_TRAIT(new_subject, TRAIT_SPAWNED_MOB, INNATE_TRAIT);
            playsound(new_subject, Dm.resource("sound/mobs/non-humanoids/mouse/mousesqueek.ogg"), 100);
            visible_message(span_warning(I18n.T("[] climbs out of []!", new_subject, this)));
        }
    }

    /// Moves an item into the diposal bin
    // tg: code/modules/recycling/disposal/bin.dm:186
    public virtual async DmTask<object> place_item_in_disposal(obj_item disposing_item = null, mob user = null)
    {
        if (!Dm.truthy(await user.transferItemToLoc(disposing_item, newloc: this)))
            return FALSE;
        user.visible_message(
            span_notice(I18n.T("[] places \\the [] into \\the [].", user.name, disposing_item, this)),
            span_notice(I18n.T("You place \\the [] into \\the [].", disposing_item, this)));
        return TRUE;
    }

    /// Mouse drop another mob or self
    // tg: code/modules/recycling/disposal/bin.dm:196
    public override async DmTask<object> mouse_drop_receive(atom dropped = null, mob user = null, object @params = null)
    {
        atom target = dropped;
        if (isliving(target))
            await stuff_mob_in(target as mob_living, user as mob_living);
        if (istype(target, typeof(obj_structure_closet_body_bag)) && Dm.eq(Dm.bitand((user as mob_living).mobility_flags, MOBILITY_PICKUP | MOBILITY_STAND), MOBILITY_PICKUP | MOBILITY_STAND))
            await stuff_bodybag_in(target as obj_structure_closet_body_bag, user as mob_living);
        return null;
    }

    /// Handles stuffing a grabbed mob into the disposal
    // tg: code/modules/recycling/disposal/bin.dm:203
    public virtual async DmTask<object> stuff_mob_in(mob_living target = null, mob_living user = null)
    {
        bool ventcrawler = HAS_TRAIT(user, TRAIT_VENTCRAWLER_ALWAYS) || HAS_TRAIT(user, TRAIT_VENTCRAWLER_NUDE);
        if (user == target) //we didn't check this before even though we're asserting here whether we can put ourself in, bruh.
        {
            if (!iscarbon(user) && !ventcrawler) //only carbon and ventcrawlers can climb into disposal by themselves.
            {
                if (iscyborg(user))
                {
                    mob_living_silicon_robot borg = user as mob_living_silicon_robot;
                    if (borg.model == null || !Dm.truthy(borg.model.canDispose))
                        return FALSE;
                }
                else
                    return FALSE;
            }
        }
        if (!isturf(user.loc)) //No magically doing it from inside closets
            return FALSE;
        if (Dm.truthy(target.buckled) || Dm.truthy(target.has_buckled_mobs()))
            return FALSE;
        if (Dm.num(target.mob_size) > MOB_SIZE_HUMAN)
        {
            to_chat(user, span_warning(I18n.T("[] doesn't fit inside []!", target, this)));
            return FALSE;
        }
        add_fingerprint(user);
        if (user == target)
            user.visible_message(span_warning(I18n.T("[] starts climbing into [].", user, this)), span_notice(I18n.T("You start climbing into []...", this)));
        else
            target.visible_message(span_danger(I18n.T("[] starts putting [] into [].", user, target, this)), span_userdanger(I18n.T("[] starts putting you into []!", user, this)));
        if (!Dm.truthy(await do_after(user, 2 * SECONDS, target)) || QDELETED(this))
            return FALSE;
        await target.forceMove(this);
        if (user == target)
            user.visible_message(span_warning(I18n.T("[] climbs into [].", user, this)), span_notice(I18n.T("You climb into [].", this)));
        else
        {
            target.visible_message(span_danger(I18n.T("[] places [] in [].", user, target, this)), span_userdanger(I18n.T("[] places you in [].", user, this)));
            log_combat(user, target, "stuffed", addition: Dm.text("into []", this));
        }
        update_appearance();
        return TRUE;
    }

    // tg: code/modules/recycling/disposal/bin.dm:236
    public virtual async DmTask<object> stuff_bodybag_in(obj_structure_closet_body_bag bag = null, mob_living user = null)
    {
        if (!Dm.truthy(length(bag.contents)))
        {
            await bag.undeploy_bodybag(this);
            qdel(bag);
            user.visible_message(
                span_warning(I18n.T("[] stuffs the empty [] into [].", user, bag.name, this)),
                span_notice(I18n.T("You stuff the empty [] into [].", bag.name, this)));
            return TRUE;
        }

        user.visible_message(
            span_warning(I18n.T("[] starts putting [] into [].", user, bag, this)),
            span_notice(I18n.T("You start putting [] into []...", bag, this)));

        if (!Dm.truthy(await do_after(user, 4 * SECONDS, bag)) || QDELETED(this))
            return FALSE;

        user.visible_message(
            span_warning(I18n.T("[] places [] in [].", user, bag, this)),
            span_notice(I18n.T("You place [] in [].", bag, this)));

        if (!Dm.truthy(length(bag.contents)))
        {
            await bag.undeploy_bodybag(this);
            qdel(bag);
        }
        else
        {
            bag.add_fingerprint(user);
            await bag.forceMove(this);
        }

        add_fingerprint(user);
        update_appearance();
        return TRUE;
    }

    // tg: code/modules/recycling/disposal/bin.dm:270
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        await attempt_escape(user);
        return null;
    }

    // resist to escape the bin
    // tg: code/modules/recycling/disposal/bin.dm:274
    public override async DmTask<object> container_resist_act(mob_living user = null, params object[] __extra)
    {
        await attempt_escape(user);
        return null;
    }

    /// Checks if a mob can climb out of the disposal, and lets them if they can
    // tg: code/modules/recycling/disposal/bin.dm:278
    public virtual async DmTask attempt_escape(mob user = null)
    {
        if (Dm.truthy(flushing))
            return;
        await go_out(user);
    }

    /// Makes a mob in the disposal climb out
    // tg: code/modules/recycling/disposal/bin.dm:284
    public virtual async DmTask go_out(mob user = null)
    {
        await user.forceMove(loc);
        update_appearance();
    }

    // clumsy monkeys and xenos can only pull the flush lever
    // tg: code/modules/recycling/disposal/bin.dm:289
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        if (ISADVANCEDTOOLUSER(user))
            return await base.attack_paw(user, modifiers);
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            return null;
        flush = Dm.truthy(flush) ? FALSE : TRUE;
        update_appearance();
        return null;
    }

    /// Ejects the contents of the disposal unit
    // tg: code/modules/recycling/disposal/bin.dm:299
    public virtual async DmTask eject()
    {
        await pipe_eject(this, FALSE, FALSE);
        update_appearance();
    }

    /// Plays the animations and sounds for flushing, and initializes the diposal holder object
    // tg: code/modules/recycling/disposal/bin.dm:304
    public virtual async DmTask flush__proc() // rules 2.4: the var flush shares the name
    {
        flushing = TRUE;
        flushAnimation();
        await sleep(1 * SECONDS);
        if (last_sound < world.time - 1) //Prevents piles of items from playing a dozen sounds at once
        {
            playsound(this, Dm.resource("sound/machines/disposalflush.ogg"), 50, FALSE, FALSE);
            last_sound = world.time;
        }
        await sleep(0.5f * SECONDS);
        if (QDELETED(this))
            return;
        obj_structure_disposalholder H = Dm.@new<obj_structure_disposalholder>(this);
        newHolderDestination(H);
        await H.init(this);
        air_contents = Dm.@new<datum_gas_mixture>();
        await H.start(this);
        flushing = FALSE;
        flush = FALSE;
    }

    /// Sets the default destinationTag of the disposal holder object
    // tg: code/modules/recycling/disposal/bin.dm:323
    public virtual void newHolderDestination(obj_structure_disposalholder H = null)
    {
        H.destinationTag = SORT_TYPE_DISPOSALS;
        foreach (obj_item_delivery O in Dm.each<obj_item_delivery>(contents))
        {
            H.tomail = TRUE;
            return;
        }
    }

    /// Plays the flushing animation of the disposal
    // tg: code/modules/recycling/disposal/bin.dm:330
    public virtual void flushAnimation()
    {
        flick(Dm.text("[]-flush", icon_state), this);
    }

    /// Called when holder is expelled from a disposal
    // tg: code/modules/recycling/disposal/bin.dm:334
    public virtual async DmTask expel(obj_structure_disposalholder H = null)
    {
        H.active = FALSE;

        playsound(this, Dm.resource("sound/machines/hiss.ogg"), 50, FALSE, FALSE);

        await pipe_eject(H);

        await H.vent_gas(loc as turf);
        qdel(H);
    }

    // tg: code/modules/recycling/disposal/bin.dm:344
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        turf T = loc as turf;
        if (stored != null)
        {
            obj_structure_disposalconstruct construct = stored;
            stored = null;
            await construct.forceMove(T);
            transfer_fingerprints_to(construct);
            await construct.set_anchored(FALSE);
            construct.set_density(TRUE);
            construct.update_appearance();
        }
        foreach (atom_movable AM in Dm.each<atom_movable>(contents.Copy())) //out, out, darned crowbar!
            await AM.forceMove(T);
        return null;
    }

    ///How disposal handles getting a storage dump from a storage object
    // tg: code/modules/recycling/disposal/bin.dm:358
    public virtual object on_storage_dump(datum source = null, datum_storage storage = null, mob user = null)
    {
        object dot = STORAGE_DUMP_HANDLED;

        to_chat(user, span_notice(I18n.T("You dump out [] into [].", storage.parent, this)));

        foreach (obj_item to_dump in Dm.each<obj_item>((storage.real_location as atom)?.contents?.Copy()))
        {
            if (user.active_storage != storage && Dm.truthy(Waiting.NoSleep(to_dump.on_found(user))))
                return dot;
            if (!Dm.truthy(storage.attempt_remove(to_dump, this, silent: TRUE)))
                continue;
            to_dump.pixel_x = to_dump.base_pixel_x + rand(-5, 5);
            to_dump.pixel_y = to_dump.base_pixel_y + rand(-5, 5);
        }

        update_appearance();
        return dot;
    }

    // tg: code/modules/recycling/disposal/bin.dm:375
    public override async DmTask<object> force_pushed(atom_movable pusher = null, float? force = null, object direction = null)
    {
        force ??= MOVE_FORCE_DEFAULT;
        object dot = await base.force_pushed(pusher, force, direction);
        visible_message(span_warning(I18n.T("[] is ripped free from the floor!", this)));
        await deconstruct();
        return dot;
    }

    // tg: code/modules/recycling/disposal/bin.dm:380
    public override async DmTask<object> move_crushed(atom_movable pusher = null, float? force = null, object direction = null)
    {
        force ??= MOVE_FORCE_DEFAULT;
        object dot = await base.move_crushed(pusher, force, direction);
        visible_message(span_warning(I18n.T("[] is ripped free from the floor!", this)));
        await deconstruct();
        return dot;
    }

    /// Handles the signal for the rat king looking inside the disposal
    // tg: code/modules/recycling/disposal/bin.dm:387
    public virtual object on_rat_rummage(datum source = null, mob_living_basic_regal_rat king = null)
    {
        if (Dm.truthy(king.combat_mode))
            return null;

        Waiting.Detach(rat_rummage(king));
        return COMPONENT_RAT_INTERACTED;
    }

    /// Handles a carbon mob getting shoved into the disposal bin
    // tg: code/modules/recycling/disposal/bin.dm:396
    public virtual object trash_living(datum source = null, mob_living shover = null, mob_living target = null, object shove_flags = null, obj_item weapon = null)
    {
        if (Dm.truthy(Dm.bitand(shove_flags, SHOVE_KNOCKDOWN_BLOCKED)) || !Dm.truthy(Dm.bitand(shove_flags, SHOVE_BLOCKED)))
            return null;
        object cur_density = density;
        density = FALSE;
        if (!Dm.truthy(Waiting.NoSleep(target.Move(get_turf(this), get_dir(target, this)))))
        {
            density = Dm.num(cur_density);
            return null;
        }
        density = Dm.num(cur_density);
        Waiting.NoSleep(target.Knockdown(SHOVE_KNOCKDOWN_SOLID));
        Waiting.NoSleep(target.forceMove(this));
        target.visible_message(span_danger(I18n.T("[] shoves [] into \\the []!", shover.name, target.name, this)),
            span_userdanger(I18n.T("You're shoved into \\the [] by []!", this, target.name)), span_hear(I18n.T("You hear aggressive shuffling followed by a loud thud!")), COMBAT_MESSAGE_RANGE, shover);
        to_chat(this, span_danger(I18n.T("You shove [] into \\the []!", target.name, this)));
        log_combat(shover, target, "shoved", Dm.text("into [] (disposal bin)[]", this, weapon != null ? Dm.text(" with []", weapon) : ""));
        return COMSIG_LIVING_SHOVE_HANDLED;
    }

    ///Called when a push broom is trying to sweep items onto the turf this object is standing on. Garbage will be moved inside.
    // tg: code/modules/recycling/disposal/bin.dm:415
    public virtual void ready_for_trash(datum source = null, obj_item_pushbroom broom = null, mob user = null, DmList items_to_sweep = null)
    {
        if (!Dm.truthy(items_to_sweep))
            return;
        foreach (obj_item garbage in Dm.each<obj_item>(items_to_sweep.Copy()))
            Waiting.NoSleep(user.transferItemToLoc(garbage, this));

        items_to_sweep.Cut();

        update_appearance();
        to_chat(user, span_notice(I18n.T("You sweep the pile of garbage into [].", this)));
        playsound(broom.loc, Dm.resource("sound/items/weapons/thudswoosh.ogg"), 30, TRUE, -1);
    }

    /// Called to check if an atom can fit inside the diposal
    // (the /atom/movable root and its overrides are below, bin.dm:746)
}

/// Disposal bin
// Holds items for disposal into pipe system
// Draws air from turf, gradually charges internal reservoir
// Once full (~1 atm), uses air resv to flush items into the pipes
// Automatically recharges air (unless off), will flush when ready if pre-set
// Can hold items and human size things, no other draggables

// tg: code/modules/recycling/disposal/bin.dm:436
public partial class obj_machinery_disposal_bin : obj_machinery_disposal
{
    // tg: code/modules/recycling/disposal/bin.dm:451
    [DmParams("mapload", "make_from")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(mapload) && prob(CONTAINS_ANIMAL_CHANCE) && Dm.num(GLOB.disposals_animals_spawned) < MAXIMUM_ANIMAL_SPAWNS)
            spawn_contained_animal();
        return dot;
    }

    // tg: code/modules/recycling/disposal/bin.dm:456
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!istype(tool, typeof(obj_item_storage_bag_trash)))
            return await base.item_interaction(user, tool, modifiers, __extra);
        obj_item_storage_bag_trash bag = tool as obj_item_storage_bag_trash;
        to_chat(user, span_warning(I18n.T("You empty the bag.")));
        await bag.atom_storage.remove_all(this);
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/recycling/disposal/bin.dm:465
    public override async DmTask<object> item_interaction_secondary(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        if (!istype(tool, typeof(obj_item_dest_tagger)))
            return await base.item_interaction_secondary(user, tool, modifiers);
        if (mounted_tagger != null)
        {
            balloon_alert(user, I18n.T("already has a tagger!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (HAS_TRAIT(tool, TRAIT_NODROP) || !Dm.truthy(await user.transferItemToLoc(tool, this)))
        {
            balloon_alert(user, I18n.T("stuck to your hand!"));
            return ITEM_INTERACT_BLOCKING;
        }
        await tool.moveToNullspace();
        user.visible_message(span_notice(I18n.T("[] snaps \\the [] onto []!", user, tool, this)));
        balloon_alert(user, I18n.T("tagger returned"));
        playsound(this, Dm.resource("sound/machines/click.ogg"), 50, TRUE);
        mounted_tagger = tool as obj_item_dest_tagger;
        update_appearance();
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/recycling/disposal/bin.dm:482
    public override async DmTask<object> attack_hand_secondary(mob user = null, DmList modifiers = null)
    {
        await base.attack_hand_secondary(user, modifiers);
        if (mounted_tagger == null)
        {
            balloon_alert(user, I18n.T("no destination tagger!"));
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }
        if (!Dm.truthy(await user.put_in_hands(mounted_tagger)))
        {
            balloon_alert(user, I18n.T("destination tagger falls!"));
            mounted_tagger = null;
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        }
        user.visible_message(span_notice(I18n.T("[] unhooks the [] from [].", user, mounted_tagger, this)));
        balloon_alert(user, I18n.T("tagger pulled"));
        playsound(this, Dm.resource("sound/machines/click.ogg"), 60, TRUE);
        mounted_tagger = null;
        update_appearance(UPDATE_OVERLAYS);
        return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
    }

    // tg: code/modules/recycling/disposal/bin.dm:498
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if ((mounted_tagger == null))
            Dm.append(ref dot, span_notice(I18n.T("The destination tagger mount is empty.")));
        else
            Dm.append(ref dot, span_notice(I18n.T("\\The [] is hanging on the side. Right Click to remove.", mounted_tagger)));
        return dot;
    }

    // tg: code/modules/recycling/disposal/bin.dm:505
    public override object Destroy(float? force = null)
    {
        if (!(mounted_tagger == null))
            QDEL_NULL(ref mounted_tagger);
        return base.Destroy(force);
    }

    // tg: code/modules/recycling/disposal/bin.dm:510
    public override async DmTask<object> on_deconstruction(object disassembled = null)
    {
        object dot = await base.on_deconstruction(disassembled);
        if (!(mounted_tagger == null))
        {
            await mounted_tagger.forceMove(drop_location() as atom);
            mounted_tagger = null;
        }
        return dot;
    }

    // tg: code/modules/recycling/disposal/bin.dm:516
    public override object ui_state(mob user = null)
    {
        return GLOB.notcontained_state;
    }

    // tg: code/modules/recycling/disposal/bin.dm:519
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            return null;
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "DisposalUnit", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/recycling/disposal/bin.dm:527
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["flush"] = flush;
        data["full_pressure"] = full_pressure;
        data["pressure_charging"] = pressure_charging;
        data["panel_open"] = panel_open;
        data["per"] = CLAMP01(Dm.num(air_contents.return_pressure()) / SEND_PRESSURE);
        data["isai"] = HAS_AI_ACCESS(user) ? TRUE : FALSE;
        return data;
    }

    // tg: code/modules/recycling/disposal/bin.dm:537
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        switch (action as string)
        {
            case "handle-0":
                flush = FALSE;
                update_appearance();
                dot = TRUE;
                break;
            case "handle-1":
                if (!Dm.truthy(panel_open))
                {
                    flush = TRUE;
                    update_appearance();
                }
                dot = TRUE;
                break;
            case "pump-0":
                if (Dm.truthy(pressure_charging))
                {
                    pressure_charging = FALSE;
                    update_appearance();
                }
                dot = TRUE;
                break;
            case "pump-1":
                if (!Dm.truthy(pressure_charging))
                {
                    pressure_charging = TRUE;
                    update_appearance();
                }
                dot = TRUE;
                break;
            case "eject":
                await eject();
                dot = TRUE;
                break;
        }
        return dot;
    }

    // tg: code/modules/recycling/disposal/bin.dm:567
    public override async DmTask<object> hitby(atom_movable hitting_atom = null, object skipcatch = null, object hitpush = null, object blocked = null, datum_thrownthing throwingdatum = null)
    {
        atom_movable AM = hitting_atom;
        if (isitem(AM) && Dm.truthy(AM.CanEnterDisposals()))
        {
            mob thrower = throwingdatum?.get_thrower() as mob;
            if ((istype(thrower, typeof(mob)) && HAS_TRAIT(thrower, TRAIT_THROWINGARM)) || prob(75))
            {
                await AM.forceMove(this);
                visible_message(span_notice(I18n.T("[] lands in [].", AM, this)));
                update_appearance();
            }
            else
            {
                visible_message(span_notice(I18n.T("[] bounces off of []'s rim!", AM, this)));
                return await base.hitby(AM, skipcatch, hitpush, blocked, throwingdatum);
            }
        }
        else
            return await base.hitby(AM, skipcatch, hitpush, blocked, throwingdatum);
        return null;
    }

    // tg: code/modules/recycling/disposal/bin.dm:580
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        object dot = await base.Entered(arrived, old_loc, old_locs);
        if (Dm.truthy(contained_animal))
            await release_animal();
        return dot;
    }

    // tg: code/modules/recycling/disposal/bin.dm:585
    public override async DmTask flush__proc()
    {
        await base.flush__proc();
        full_pressure = FALSE;
        pressure_charging = TRUE;
        if (Dm.truthy(contained_animal))
            await release_animal();
        update_appearance();
    }

    // tg: code/modules/recycling/disposal/bin.dm:593
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN)))
            return dot;

        //flush handle
        if (Dm.truthy(flush))
            Dm.append(ref dot, "dispover-handle");

        if (mounted_tagger != null)
            Dm.append(ref dot, "tagger_mount");

        //only handle is shown if no power
        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER)) || Dm.truthy(panel_open))
            return dot;

        //check for items in disposal - occupied light
        // DEVIATION(DV12): each light's emissive_appearance(icon, "...-glow", src, alpha = src.alpha) on the emissive plane is not created
        // (lighting planes and emissives are not ported)
        if (Dm.truthy(length(contents)))
            Dm.append(ref dot, "dispover-full");

        //charging and ready light
        if (Dm.truthy(pressure_charging))
            Dm.append(ref dot, "dispover-charge");
        else if (Dm.truthy(full_pressure))
            Dm.append(ref dot, "dispover-ready");
        return dot;
    }

    /// Create a random animal to be infesting this disposals bin
    // tg: code/modules/recycling/disposal/bin.dm:623
    public virtual void spawn_contained_animal()
    {
        DmList weighted_animal_list = TgState.Current.obj_machinery_disposal_bin_weighted_animal_list ??= Dm.list(
            Dm.kv(typeof(mob_living_basic_stoat), 5),
            Dm.kv(typeof(mob_living_basic_stoat_kit), 1));
        contained_animal = pick_weight(weighted_animal_list);
        GLOB.disposals_animals_spawned = Dm.num(GLOB.disposals_animals_spawned) + 1;
    }

    /// Release whoever is living inside here
    // tg: code/modules/recycling/disposal/bin.dm:628
    public virtual async DmTask release_animal()
    {
        DmList open_turfs = get_adjacent_open_turfs(this) as DmList;
        turf final_turf = Dm.truthy(length(open_turfs)) ? pick(open_turfs) as turf : drop_location() as turf;
        mob_living startled_animal = Dm.@new((Type)contained_animal, drop_location()) as mob_living;
        await startled_animal.Move(final_turf);
        visible_message(span_notice(I18n.T("A startled [] jumps out of [].", startled_animal, this)));
        contained_animal = null;
    }

    /// Initiates flushing
    // tg: code/modules/recycling/disposal/bin.dm:637  (set waitfor = FALSE)
    public virtual void do_flush()
    {
        Waiting.Detach(flush__proc());
    }

    //timed process
    //charge the gas reservoir and perform flush if ready
    // tg: code/modules/recycling/disposal/bin.dm:643
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.truthy(contained_animal) && prob(ANIMAL_SHAKE_CHANCE))
            Shake(pixelshiftx: 1, pixelshifty: 0, duration: Dm.num(seconds_per_tick) * SECONDS, shake_interval: 0.1f * SECONDS); // tg names duration twice: the second wins

        if (Dm.truthy(Dm.bitand(machine_stat, BROKEN))) //nothing can happen if broken
            return null;

        flush_count++;
        if (flush_count >= flush_every_ticks)
        {
            if (Dm.truthy(length(contents)))
            {
                if (Dm.truthy(full_pressure))
                    do_flush();
            }
            flush_count = 0;
        }

        if (Dm.truthy(flush) && Dm.num(air_contents.return_pressure()) >= SEND_PRESSURE) // flush can happen even without power
            do_flush();

        if (Dm.truthy(Dm.bitand(machine_stat, NOPOWER))) // won't charge if no power
            return null;

        Waiting.NoSleep(use_energy(idle_power_usage)); // base power usage

        if (!Dm.truthy(pressure_charging)) // if off or ready, no need to charge
            return null;

        // otherwise charge
        Waiting.NoSleep(use_energy(idle_power_usage)); // charging power usage

        atom L = loc; //recharging from loc turf

        datum_gas_mixture env = Waiting.NoSleep(L.return_air()) as datum_gas_mixture;
        if (!Dm.truthy(env.temperature))
            return null;
        float pressure_delta = (SEND_PRESSURE * 1.01f) - Dm.num(air_contents.return_pressure());

        float transfer_moles = 0.05f * Dm.num(seconds_per_tick) * (pressure_delta * Dm.num(air_contents.volume)) / (Dm.num(env.temperature) * R_IDEAL_GAS_EQUATION);

        //Actually transfer the gas
        datum_gas_mixture removed = env.remove(transfer_moles) as datum_gas_mixture;
        air_contents.merge(removed);
        air_update_turf(FALSE, FALSE);

        //if full enough, switch to ready mode
        if (Dm.num(air_contents.return_pressure()) >= SEND_PRESSURE)
        {
            full_pressure = TRUE;
            pressure_charging = FALSE;
            update_appearance();
        }
        return null;
    }

    // tg: code/modules/recycling/disposal/bin.dm:693
    public override void get_remote_view_fullscreens(mob user = null)
    {
        if (Dm.eq(user.stat, DEAD) || !Dm.truthy(Dm.bitand(user.sight, Dm.bitor(SEEOBJS, SEEMOBS))))
            user.overlay_fullscreen("remote_view", typeof(atom_movable_screen_fullscreen_impaired), 2);
    }
}

// tg: code/modules/recycling/disposal/bin.dm:697
public partial class obj_machinery_disposal_bin_tagger : obj_machinery_disposal_bin
{
    // tg: code/modules/recycling/disposal/bin.dm:697
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        mounted_tagger = Dm.@new<obj_item_dest_tagger>(null);
        return base.Initialize(mapload, __extra);
    }
}

/// Delivery Chute

// tg: code/modules/recycling/disposal/bin.dm:704
public partial class obj_machinery_disposal_delivery_chute : obj_machinery_disposal
{
    // tg: code/modules/recycling/disposal/bin.dm:711
    public override async DmTask<object> place_item_in_disposal(obj_item disposing_item = null, mob user = null)
    {
        obj_item I = disposing_item;
        if (Dm.truthy(I.CanEnterDisposals()))
        {
            await base.place_item_in_disposal(I, user);
            await flush__proc();
        }
        return null;
    }

    // tg: code/modules/recycling/disposal/bin.dm:716
    public override object Bumped(atom_movable AM = null) //Go straight into the chute
    {
        if (QDELETED(AM) || !Dm.truthy(AM.CanEnterDisposals()))
            return null;
        switch (Dm.num(dir))
        {
            case NORTH:
                if (AM.loc.y != loc.y + 1)
                    return null;
                break;
            case EAST:
                if (AM.loc.x != loc.x + 1)
                    return null;
                break;
            case SOUTH:
                if (AM.loc.y != loc.y - 1)
                    return null;
                break;
            case WEST:
                if (AM.loc.x != loc.x - 1)
                    return null;
                break;
        }

        if (isobj(AM))
        {
            obj O = AM as obj;
            Waiting.NoSleep(O.forceMove(this));
        }
        else if (ismob(AM))
        {
            mob M = AM as mob;
            if (prob(2)) // to prevent mobs being stuck in infinite loops
            {
                to_chat(M, span_warning(I18n.T("You hit the edge of the chute.")));
                return null;
            }
            Waiting.NoSleep(M.forceMove(this));
        }
        // Bumped is a sync proc here (the bump's Move cannot sleep): the flush runs on its own, as tg's sleeps in it would detach the caller
        Waiting.Detach(flush__proc());
        return null;
    }
}

public partial class atom_movable : atom
{
    /// Called to check if an atom can fit inside the diposal
    // tg: code/modules/recycling/disposal/bin.dm:746  (no live ancestor declares it: the root slot)
    public virtual object CanEnterDisposals()
    {
        return TRUE;
    }
}

// tg: code/modules/recycling/disposal/bin.dm:749
public partial class obj_projectile : obj
{
    public override object CanEnterDisposals()
    {
        return null;
    }
}

// tg: code/modules/recycling/disposal/bin.dm:752
public partial class obj_effect : obj
{
    public override object CanEnterDisposals()
    {
        return null;
    }
}

// tg: code/modules/recycling/disposal/bin.dm:755
public partial class obj_vehicle_sealed_mecha : obj_vehicle_sealed
{
    public override object CanEnterDisposals()
    {
        return null;
    }
}

// The type-level statics of the disposal machines (rules 10.6: they live in TgState).
public sealed partial class TgState
{
    // tg: code/modules/recycling/disposal/bin.dm:68  var/static/list/loc_connections
    internal DmList obj_machinery_disposal_loc_connections;

    // tg: code/modules/recycling/disposal/bin.dm:444  var/static/list/weighted_animal_list
    internal DmList obj_machinery_disposal_bin_weighted_animal_list;
}
