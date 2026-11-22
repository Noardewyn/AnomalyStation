// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/morgue.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MD2: the whole file. The body containers (MetaStation has 12 morgues and a crematorium): a click slides the tray out with
// what lay in it, or back in with what lies on it (not the anchored or the phased); a body stuffed into a closed one breaks out in a
// minute when it is locked. The morgue cools its own air while closed, lights red for a dead body, yellow for things, green when a
// body may still be revived (and beeps for it once a minute unless its beeper is off, alt-click), takes a body bag's tag as its
// label, and an emag fools its alert. The crematorium, lit by its button, burns everything on its tray to ash (the creamatorium of ice
// cream day leaves ice cream). The trays take a mob or a body bag dropped on them. The var blocks are generated (D111), but for
// the containers' and the trays' `connected` (GAP(types) below).

// tg: code/game/objects/structures/morgue.dm:1-4 (file-local, #undef at :622-623)
public partial class obj_structure_bodycontainer
{
    ///The cooldown between messages when attempting to break out of a morgue tray.
    internal const float BREAKOUT_COOLDOWN = 5 * SECONDS;
    ///The amount of time it takes to break out of a morgue tray.
    internal const float BREAKDOWN_TIME = 60 * SECONDS;
    // tg: code/game/objects/structures/morgue.dm:171  #define SLIDE_LENGTH (0.3 SECONDS)
    internal const float SLIDE_LENGTH = 0.3f * SECONDS;
}

public partial class datum_controller_global_vars : datum_controller
{
    //Let them act as spawnpoints for revenants and other ghosties.
    // tg: code/game/objects/structures/morgue.dm:31
    public DmList bodycontainers;

    // tg: code/game/objects/structures/morgue.dm:31  (GLOBAL_LIST_EMPTY(bodycontainers), __DEFINES/__globals.dm)
    public virtual void InitGlobalbodycontainers()
    {
        bodycontainers = Dm.list();
        Dm.append(ref gvars_datum_init_order, "bodycontainers");
    }

    // tg: code/game/objects/structures/morgue.dm:419
    public DmList crematoriums;

    // tg: code/game/objects/structures/morgue.dm:419  (GLOBAL_LIST_EMPTY(crematoriums), __DEFINES/__globals.dm)
    public virtual void InitGlobalcrematoriums()
    {
        crematoriums = Dm.list();
        Dm.append(ref gvars_datum_init_order, "crematoriums");
    }
}

/*
 * Bodycontainer
 * Parent class for morgue and crematorium
 * For overriding only
 */
// tg: code/game/objects/structures/morgue.dm:33
public partial class obj_structure_bodycontainer : obj_structure
{
    ///The morgue tray this container will open/close to put/take things in/out.
    // GAP(types): /obj/structure/bodycontainer/var/connected is generated as a Type (rules 3.2 V3: its default is the tray's
    // typepath); tg keeps the typepath there until Initialize and the tray itself after it, as the machines' circuit does
    // (_machinery.cs). The tray instance is connected_value; connected keeps the tray's type.
    public obj_structure_tray connected_value;

    // tg: code/game/objects/structures/morgue.dm:51
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (connected != null)
        {
            connected_value = Dm.@new(connected, this) as obj_structure_tray;
            connected_value.connected = this;
        }
        Dm.append(ref GLOB.bodycontainers, this);
        register_context();
        return dot;
    }

    // tg: code/game/objects/structures/morgue.dm:59
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        if (!Dm.truthy(locked))
            context[SCREENTIP_CONTEXT_LMB] = "Open/Close";
        return CONTEXTUAL_SCREENTIP_SET;
    }

    // tg: code/game/objects/structures/morgue.dm:64
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.bodycontainers, this);
        Waiting.NoSleep(open());
        if (connected_value != null)
            QDEL_NULL(ref connected_value);
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/morgue.dm:71
    public override void on_log(object login = null)
    {
        base.on_log(login);
        update_appearance(UPDATE_ICON);
    }

    // tg: code/game/objects/structures/morgue.dm:75
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        if (IS_UNCONSCIOUS_OR_CRIT(user) || !isturf(loc))
            return null;
        if (Dm.truthy(locked))
        {
            if (breakout_message_cooldown <= world.time) // COOLDOWN_FINISHED
            {
                breakout_message_cooldown = world.time + BREAKOUT_COOLDOWN; // COOLDOWN_START
                to_chat(user, span_warning(I18n.T("[]'s door won't budge!", this)));
            }
            return null;
        }
        await open();
        return null;
    }

    // tg: code/game/objects/structures/morgue.dm:85
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.truthy(locked))
        {
            to_chat(user, span_danger(I18n.T("It's locked.")));
            return dot;
        }
        if (connected_value == null)
        {
            to_chat(user, I18n.T("That doesn't appear to have a tray."));
            return dot;
        }
        if (ReferenceEquals(connected_value.loc, this))
            await open();
        else
            await close();
        add_fingerprint(user);
        return dot;
    }

    // tg: code/game/objects/structures/morgue.dm:101
    public override DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
        => attack_hand(user, modifiers);

    // tg: code/game/objects/structures/morgue.dm:104
    public override async DmTask<object> attack_robot(mob user = null, object modifiers = null)
    {
        if (!Dm.truthy(await user.Adjacent(this)))
            return null;
        return await attack_hand(user);
    }

    // tg: code/game/objects/structures/morgue.dm:109
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        Dm.@new<obj_item_stack_sheet_iron>(loc, 5);
        await DmTask.CompletedTask;
    }

    // tg: code/game/objects/structures/morgue.dm:112
    public override async DmTask<object> container_resist_act(mob_living user = null, params object[] __extra)
    {
        if (!Dm.truthy(locked))
        {
            await open();
            return null;
        }
        user.change_next_special_move(CLICK_CD_BREAKOUT);
        user.visible_message(null,
            span_notice(I18n.T("You lean on the back of [] and start pushing the tray open... (this will take about [].)", this, DisplayTimeText(BREAKDOWN_TIME))),
            span_hear(I18n.T("You hear a metallic creaking from [].", this)));
        if (!Dm.truthy(await do_after(user, BREAKDOWN_TIME, target: this)))
            return null;
        if (user == null || IS_UNCONSCIOUS_OR_CRIT(user) || !ReferenceEquals(user.loc, this))
            return null;
        user.visible_message(
            span_warning(I18n.T("[] successfully broke out of []!", user, this)),
            span_notice(I18n.T("You successfully break out of []!", this)));
        await open();
        return null;
    }

    // tg: code/game/objects/structures/morgue.dm:130
    public override void get_remote_view_fullscreens(mob user = null)
    {
        if (user.stat == DEAD || !Dm.truthy(Dm.bitand(user.sight, Dm.bitor(SEEOBJS, SEEMOBS))))
            user.overlay_fullscreen("remote_view", typeof(atom_movable_screen_fullscreen_impaired), 2);
    }

    // tg: code/game/objects/structures/morgue.dm:134
    public virtual async DmTask<object> open()
    {
        if (!(open_close_cd <= world.time)) // COOLDOWN_FINISHED
            return FALSE;

        open_close_cd = world.time + 0.25f * SECONDS; // COOLDOWN_START
        playsound(this, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
        playsound(this, Dm.resource("sound/effects/roll.ogg"), 5, TRUE);
        turf dump_turf = get_step(this, dir);
        if (connected_value != null)
            await connected_value.setDir(dir);
        foreach (atom_movable moving in Dm.each<atom_movable>((contents as DmList)?.Copy()))
        {
            await moving.forceMove(dump_turf);
            animate_slide_out(moving);
        }
        update_appearance();
        return TRUE;
    }

    // tg: code/game/objects/structures/morgue.dm:149
    public virtual async DmTask<object> close()
    {
        if (!(open_close_cd <= world.time)) // COOLDOWN_FINISHED
            return FALSE;

        open_close_cd = world.time + 0.5f * SECONDS; // COOLDOWN_START
        playsound(this, Dm.resource("sound/effects/roll.ogg"), 5, TRUE);
        playsound(this, Dm.resource("sound/items/deconstruct.ogg"), 50, TRUE);
        var close_loc = connected_value.loc as turf;
        foreach (atom_movable entering in Dm.each<atom_movable>((close_loc.contents as DmList)?.Copy()))
        {
            if (Dm.truthy(entering.anchored) && !ReferenceEquals(entering, connected_value))
                continue;
            if (entering is mob_living living_mob)
            {
                if (Dm.truthy(living_mob.incorporeal_move))
                    continue;
            }
            else if (entering is obj_effect_dummy_phased_mob || isdead(entering))
                continue;
            animate_slide_in(entering, close_loc);
            await entering.forceMove(this);
        }
        update_appearance();
        return TRUE;
    }

    /// Slides the passed object out of the morgue tray.
    // tg: code/game/objects/structures/morgue.dm:174
    public virtual void animate_slide_out(atom_movable animated)
    {
        float old_layer = Dm.num(animated.layer);
        animated.layer = Dm.num(layer) - (ReferenceEquals(animated, connected_value) ? 0.03f : 0.01f);
        animated.pixel_x = Dm.num(animated.base_pixel_x) + Dm.num(x) * 32 - Dm.num(animated.x) * 32;
        animated.pixel_y = Dm.num(animated.base_pixel_y) + Dm.num(y) * 32 - Dm.num(animated.y) * 32;
        animate(animated,
            Dm.named("pixel_x", animated.base_pixel_x),
            Dm.named("pixel_y", animated.base_pixel_y),
            Dm.named("time", SLIDE_LENGTH),
            Dm.named("easing", Dm.bitor(CUBIC_EASING, EASE_OUT)),
            Dm.named("flags", ANIMATION_PARALLEL));
        // VARSET_CALLBACK(animated, layer, old_layer): ___callbackvarset is not declared (doorcontrol.cs' GAP(lead)); the timer calls
        // layer__varset, its datum branch for this var
        _addtimer(Dm.@new<datum_callback>(this, nameof(layer__varset), animated, old_layer), SLIDE_LENGTH, file: "code/game/objects/structures/morgue.dm", line: 187);
    }

    /// VARSET_CALLBACK(animated, layer, old_layer)'s target: ___callbackvarset's datum branch (varset_callback.dm:8-23) for the layer.
    public virtual void layer__varset(atom_movable animated, object var_value)
    {
        if (!QDELETED(animated))
            animated.layer = Dm.num(var_value);
    }

    /// Slides the passed object into the morgue tray from the passed turf.
    // tg: code/game/objects/structures/morgue.dm:190
    public virtual void animate_slide_in(atom_movable animated, turf from_loc)
    {
        // It's easier to just make a visual for entering than to animate the object itself
        var visual = Dm.@new<obj_effect_temp_visual_morgue_content>(from_loc, animated);
        visual.layer = Dm.num(layer) - (ReferenceEquals(animated, connected_value) ? 0.03f : 0.01f);
        animate(visual,
            Dm.named("pixel_x", Dm.num(visual.base_pixel_x) + Dm.num(x) * 32 - Dm.num(visual.x) * 32),
            Dm.named("pixel_y", Dm.num(visual.base_pixel_y) + Dm.num(y) * 32 - Dm.num(visual.y) * 32),
            Dm.named("time", SLIDE_LENGTH),
            Dm.named("easing", Dm.bitor(CUBIC_EASING, EASE_IN)),
            Dm.named("flags", ANIMATION_PARALLEL));
    }
}

/// Used to mimic the appearance of an object sliding into a morgue tray.
// tg: code/game/objects/structures/morgue.dm:204
public partial class obj_effect_temp_visual_morgue_content : obj_effect_temp_visual
{
    // tg: code/game/objects/structures/morgue.dm:207
    [DmParams("mapload", "sliding_in")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        atom_movable sliding_in = Dm.arg<atom_movable>(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        if (sliding_in == null)
            return dot;

        appearance = sliding_in.appearance;
        dir = sliding_in.dir;
        alpha = sliding_in.alpha;
        base_pixel_x = sliding_in.base_pixel_x;
        base_pixel_y = sliding_in.base_pixel_y;
        return dot;
    }
}

/*
 * Morgue
 */
// tg: code/game/objects/structures/morgue.dm:228
public partial class obj_structure_bodycontainer_morgue : obj_structure_bodycontainer
{
    // tg: code/game/objects/structures/morgue.dm:220-223 (file-local, #undef at :411-414)
    internal const float MORGUE_EMPTY = 1;
    internal const float MORGUE_NO_MOBS = 2;
    internal const float MORGUE_ONLY_BRAINDEAD = 3;
    internal const float MORGUE_HAS_REVIVABLE = 4;

    // tg: code/game/objects/structures/morgue.dm:255
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        base.Initialize(mapload, __extra);
        return INITIALIZE_HINT_LATELOAD;
    }

    // tg: code/game/objects/structures/morgue.dm:259
    public override void LateInitialize(params object[] __extra)
    {
        var external_air = Waiting.NoSleep(loc.return_air()) as datum_gas_mixture;
        if (external_air != null)
            internal_air = external_air.copy() as datum_gas_mixture;
        else
            internal_air = Dm.@new<datum_gas_mixture>();
        START_PROCESSING(SSobj, this);
    }

    // tg: code/game/objects/structures/morgue.dm:267
    public override async DmTask<object> return_air()
    {
        await DmTask.CompletedTask;
        return internal_air;
    }

    // tg: code/game/objects/structures/morgue.dm:270
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(process__body(seconds_per_tick, dot), dot);
    }

    private async DmTask<object> process__body(object seconds_per_tick, DotBox<object> dot)
    {
        update_morgue_status();
        update_appearance(UPDATE_ICON_STATE);
        if (morgue_state == MORGUE_HAS_REVIVABLE && Dm.truthy(beeper) && Dm.num(next_beep) <= world.time) // COOLDOWN_FINISHED
        {
            playsound(this, Dm.resource("sound/items/weapons/gun/general/empty_alarm.ogg"), 50, FALSE); //Revive them you blind fucks
            next_beep = world.time + beep_cooldown; // COOLDOWN_START
        }

        if (connected_value == null || !ReferenceEquals(connected_value.loc, this))
        {
            var current_exposed_air = await loc.return_air() as datum_gas_mixture;
            if (current_exposed_air == null)
                return dot.Value;
            // The internal air won't cool down the external air when the freezer is opened.
            internal_air.temperature = Math.Max(current_exposed_air.temperature, internal_air.temperature);
            if (Dm.truthy(current_exposed_air.equalize(internal_air)))
            {
                turf location = get_turf(this);
                location.air_update_turf();
            }
        }
        else
        {
            if (internal_air.temperature <= minimum_temperature)
                return dot.Value;
            float temperature_decrease_this_tick = Math.Min(cooling_rate_per_second * Dm.num(seconds_per_tick), internal_air.temperature - minimum_temperature);
            internal_air.temperature -= temperature_decrease_this_tick;
        }
        return dot.Value;
    }

    // tg: code/game/objects/structures/morgue.dm:297
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        base.add_context(source, context, held_item, user);
        context[SCREENTIP_CONTEXT_ALT_LMB] = Dm.text("[]", Dm.truthy(beeper) ? "disable beeper" : "enable beeper");
        return CONTEXTUAL_SCREENTIP_SET;
    }

    // tg: code/game/objects/structures/morgue.dm:302
    public virtual void update_morgue_status()
    {
        if (Dm.length(contents) <= 1)
        {
            morgue_state = MORGUE_EMPTY;
            return;
        }

        var stored_living = get_all_contents_type(typeof(mob_living)) as DmList; // Search for mobs in all contents.
        if (Dm.length(stored_living) == 0)
        {
            morgue_state = MORGUE_NO_MOBS;
            return;
        }

        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
        {
            morgue_state = MORGUE_ONLY_BRAINDEAD;
            return;
        }

        foreach (mob_living occupant in Dm.each<mob_living>(stored_living))
        {
            if (occupant_revivable(occupant))
            {
                morgue_state = MORGUE_HAS_REVIVABLE;
                return;
            }
        }
        morgue_state = MORGUE_ONLY_BRAINDEAD;
    }

    // tg: code/game/objects/structures/morgue.dm:322
    public virtual bool occupant_revivable(mob_living occupant)
    {
        if (occupant.stat != DEAD)
            return true;
        if (HAS_TRAIT(occupant, TRAIT_GHOSTROLE_ON_REVIVE) && Dm.length(occupant.get_all_orbiters() as DmList) > 0)
            return true;
        if (occupant is mob_living_carbon carbon_occupant)
            return Dm.truthy(carbon_occupant.can_defib_client());
        if (HAS_TRAIT(occupant, TRAIT_SUICIDED))
            return false;
        if (!Dm.truthy(occupant.key) && !Dm.truthy(occupant.get_ghost(FALSE, TRUE)))
            return false;
        return true;
    }

    // tg: code/game/objects/structures/morgue.dm:336
    public virtual void handle_bodybag_enter(obj_structure_closet_body_bag arrived_bag)
    {
        if (!Dm.truthy(arrived_bag.tag_name))
            return;
        name = Dm.text("[] - ([])", Dm.initial(this, (atom p) => p.name), arrived_bag.tag_name);
        update_appearance(UPDATE_ICON);
    }

    // tg: code/game/objects/structures/morgue.dm:342
    public virtual void handle_bodybag_exit(obj_structure_closet_body_bag exited_bag)
    {
        name = Dm.initial(this, (atom p) => p.name);
        update_appearance(UPDATE_ICON);
    }

    // tg: code/game/objects/structures/morgue.dm:346
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        object dot = await base.Entered(arrived, old_loc, old_locs);
        if (arrived is obj_structure_closet_body_bag bag)
        {
            handle_bodybag_enter(bag);
            return null;
        }
        return dot;
    }

    // tg: code/game/objects/structures/morgue.dm:351
    public override async DmTask<object> close()
    {
        object dot = await base.close();
        update_morgue_status();
        update_appearance(UPDATE_ICON_STATE);
        return dot;
    }

    // tg: code/game/objects/structures/morgue.dm:356
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone is obj_structure_closet_body_bag bag)
        {
            handle_bodybag_exit(bag);
            return null;
        }
        return dot;
    }

    // tg: code/game/objects/structures/morgue.dm:361
    public override async DmTask<object> open()
    {
        object dot = await base.open();
        update_morgue_status();
        update_appearance(UPDATE_ICON_STATE);
        return dot;
    }

    // tg: code/game/objects/structures/morgue.dm:366
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("The speaker is []. Alt-click to toggle it.", Dm.truthy(beeper) ? I18n.T("enabled") : I18n.T("disabled"))));
        return dot;
    }

    // tg: code/game/objects/structures/morgue.dm:370
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        await DmTask.CompletedTask;
        beeper = Dm.truthy(beeper) ? FALSE : TRUE;
        to_chat(user, span_notice(I18n.T("You turn the speaker function [].", Dm.truthy(beeper) ? I18n.T("on") : I18n.T("off"))));
        return CLICK_ACTION_SUCCESS;
    }

    // tg: code/game/objects/structures/morgue.dm:375
    public override async DmTask<object> emag_act(mob user = null, obj_item_card_emag emag_card = null, params object[] __extra)
    {
        if (Dm.truthy(Dm.bitand(obj_flags, EMAGGED)))
            return FALSE;
        balloon_alert(user, I18n.T("alert system overloaded"));
        obj_flags = Dm.bitor(obj_flags, EMAGGED);
        update_appearance(UPDATE_ICON);
        return TRUE;
    }

    // tg: code/game/objects/structures/morgue.dm:383
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (connected_value == null || !ReferenceEquals(connected_value.loc, this)) // Open or tray is gone.
        {
            icon_state = "morgue0";
            return await base.update_icon_state(__extra);
        }

        if (morgue_state == MORGUE_EMPTY) // Empty
        {
            icon_state = "morgue1";
            return await base.update_icon_state(__extra);
        }

        if (morgue_state == MORGUE_NO_MOBS) // No mobs?
        {
            icon_state = "morgue3";
            return await base.update_icon_state(__extra);
        }

        if (morgue_state == MORGUE_HAS_REVIVABLE)
        {
            icon_state = "morgue4"; // Revivable
            return await base.update_icon_state(__extra);
        }

        if (morgue_state == MORGUE_ONLY_BRAINDEAD)
            icon_state = "morgue2"; // Dead, brainded mob.
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/structures/morgue.dm:404
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        underlays = Dm.list(); // underlays.Cut()

        if (!Dm.eq(name, Dm.initial(this, (atom p) => p.name)))
            Dm.append(ref dot, Dm.text("[]_label", base_icon_state));
        return dot;
    }
}

/*
 * Crematorium
 */
// tg: code/game/objects/structures/morgue.dm:420
public partial class obj_structure_bodycontainer_crematorium : obj_structure_bodycontainer
{
    // tg: code/game/objects/structures/morgue.dm:432
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(mapload) && Dm.truthy(check_holidays(ICE_CREAM_DAY)) && this is not obj_structure_bodycontainer_crematorium_creamatorium)
        {
            var creamy = Dm.@new<obj_structure_bodycontainer_crematorium_creamatorium>(loc);
            creamy.id = id;
            return INITIALIZE_HINT_QDEL;
        }
        Dm.append(ref GLOB.crematoriums, this);
        return dot;
    }

    // tg: code/game/objects/structures/morgue.dm:440
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.crematoriums, this);
        return base.Destroy(force);
    }

    //Borgs can't use crematoriums without help
    // tg: code/game/objects/structures/morgue.dm:444
    public override async DmTask<object> attack_robot(mob user = null, object modifiers = null)
    {
        await DmTask.CompletedTask;
        to_chat(user, span_warning(I18n.T("[] is locked against you.", this)));
        return null;
    }

    // tg: code/game/objects/structures/morgue.dm:448
    public override async DmTask<object> connect_to_shuttle(object mapload = null, obj_docking_port_mobile port = null, obj_docking_port_stationary dock = null)
    {
        await DmTask.CompletedTask;
        id = Dm.text("[]_[]", port.shuttle_id, id);
        return null;
    }

    // tg: code/game/objects/structures/morgue.dm:451
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (connected_value == null || !ReferenceEquals(connected_value.loc, this))
        {
            icon_state = Dm.text("[]0", base_icon_state);
            return await base.update_icon_state(__extra);
        }
        if (Dm.truthy(locked))
        {
            icon_state = Dm.text("[]_active", base_icon_state);
            return await base.update_icon_state(__extra);
        }
        icon_state = Dm.text("[][]", base_icon_state, Dm.length(contents) > 1 ? 2 : 1);
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/structures/morgue.dm:461
    public virtual async DmTask<object> cremate(mob user)
    {
        if (Dm.truthy(locked))
            return null; //don't let you cremate something twice or w/e

        // Make sure we don't delete the actual morgue and its tray
        DmList conts = (get_all_contents() as DmList)?.Copy();
        Dm.remove(ref conts, this);
        Dm.remove(ref conts, connected_value);
        if (Dm.length(conts) == 0)
        {
            audible_message(span_hear(I18n.T("You hear a hollow crackle.")));
            return null;
        }

        audible_message(span_hear(I18n.T("You hear a roar as the crematorium activates.")));
        locked = TRUE;
        update_appearance();

        foreach (mob_living victim in Dm.each<mob_living>(conts))
        {
            if (Dm.truthy(victim.incorporeal_move)) //can't cook revenants!
                continue;

            if (victim.stat != DEAD)
                await victim.emote("scream");

            if (user != null)
                log_combat(user, victim, "cremated");
            else
                victim.log_message("was cremated", LOG_ATTACK);

            // tg reads user.stat without a null check here; the port skips the log for a cremation with no user (the button's usr)
            if (user != null && user.stat != DEAD)
                user.investigate_log("has died from being cremated.", INVESTIGATE_DEATHS);

            await victim.death(TRUE);
            if (!QDELETED(victim)) //some animals get automatically deleted on death.
            {
                victim.ghostize();
                qdel(victim);
            }
        }

        foreach (obj to_destroy in Dm.each<obj>(conts)) // conts defined above, ignores crematorium and tray
        {
            // Indestructible atoms should not be destroyed
            if (to_destroy is obj_effect_dummy_phased_mob || Dm.truthy(Dm.bitand(to_destroy.resistance_flags, INDESTRUCTIBLE)))
                continue;
            qdel(to_destroy);
        }

        _addtimer(Dm.@new<datum_callback>(this, nameof(unlock)), 3 * SECONDS, file: "code/game/objects/structures/morgue.dm", line: 501);
        return null;
    }

    // tg: code/game/objects/structures/morgue.dm:503
    public virtual async DmTask unlock()
    {
        if (QDELETED(this))
            return;

        if (Dm.locate<obj_effect_decal_cleanable_ash>(get_step(this, dir)) == null) // As to prevent pile-up
            Dm.@new<obj_effect_decal_cleanable_ash>(this);

        locked = FALSE;
        update_appearance();
        playsound(loc, Dm.resource("sound/machines/ding.ogg"), 50, TRUE); // You horrible people
    }
}

// tg: code/game/objects/structures/morgue.dm:514
public partial class obj_structure_bodycontainer_crematorium_creamatorium : obj_structure_bodycontainer_crematorium
{
    // tg: code/game/objects/structures/morgue.dm:518
    public override async DmTask<object> cremate(mob user)
    {
        DmList icecreams = Dm.list();
        foreach (mob_living i_scream in Dm.each<mob_living>(get_all_contents_type(typeof(mob_living))))
            Dm.append(ref icecreams, Dm.@new<obj_item_food_icecream>(null, Dm.list(Dm.kv(ICE_CREAM_MOB, Dm.list(null, i_scream.name)))));
        object dot = await base.cremate(user);
        foreach (obj ice_cream in Dm.each<obj>(icecreams))
            await ice_cream.forceMove(this);
        return dot;
    }
}

/*
 * Generic Tray
 * Parent class for morguetray and crematoriumtray
 * For overriding only
 */
// tg: code/game/objects/structures/morgue.dm:531
public partial class obj_structure_tray : obj_structure
{
    ///The bodycontainer we are a tray to.
    // tg: code/game/objects/structures/morgue.dm:540  var/obj/structure/bodycontainer/connected
    // Hand-written: V3 types it Type (tg writes `connected = /obj/...` on the containers, and this subtree assigns it no literal).
    public obj_structure_bodycontainer connected;

    // The hand-written field above puts the class in Hand mode (types-converter.md 3.2), so the generator no longer writes its
    // constructor: tg's var defaults, as main's generated part gave them.
    // tg: code/game/objects/structures/morgue.dm:532
    public obj_structure_tray()
    {
        icon = Dm.resource("icons/obj/machines/crematorium.dmi");
        density = 1;
        anchored = 1;
        pass_flags_self = 65;
        max_integrity = 350;
    }

    // tg: code/game/objects/structures/morgue.dm:542
    public override object Destroy(float? force = null)
    {
        if (connected != null)
        {
            connected.connected_value = null;
            connected.update_appearance();
            connected = null;
        }
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/morgue.dm:549
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        Dm.@new<obj_item_stack_sheet_iron>(loc, 2);
        await DmTask.CompletedTask;
    }

    // tg: code/game/objects/structures/morgue.dm:552
    public override DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
        => attack_hand(user, modifiers);

    // tg: code/game/objects/structures/morgue.dm:555
    public override DmTask<object> attack_robot(mob user = null, object modifiers = null)
        => attack_hand(user, modifiers as DmList);

    // tg: code/game/objects/structures/morgue.dm:558
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (connected != null)
            await connected.close();
        else
            to_chat(user, span_warning(I18n.T("That's not connected to anything!")));
        add_fingerprint(user);
        return dot;
    }

    // tg: code/game/objects/structures/morgue.dm:568
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_riding_offhand riding_item)
            return NONE;

        mob_living carried_mob = riding_item.rider;
        if (ReferenceEquals(carried_mob, user)) //Piggyback user.
            return ITEM_INTERACT_BLOCKING;

        await user.unbuckle_mob(carried_mob);
        await mouse_drop_receive(carried_mob, user);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/morgue.dm:581
    public override async DmTask<object> mouse_drop_receive(atom dropped = null, mob user = null, object @params = null)
    {
        if (dropped is not atom_movable O || Dm.truthy(O.anchored) || ReferenceEquals(O.loc, user))
            return null;
        if (O is not mob victim)
        {
            if (O is not obj_structure_closet_body_bag)
                return null;
        }
        else
        {
            if (victim.buckled != null)
                return null;
        }
        await O.forceMove(loc);
        if (!ReferenceEquals(user, O))
            visible_message(span_warning(I18n.T("[] stuffs [] into [].", user, O, this)));
        return null;
    }
}

/*
 * Morgue tray
 */
// tg: code/game/objects/structures/morgue.dm:607
public partial class obj_structure_tray_m_tray : obj_structure_tray
{
    // tg: code/game/objects/structures/morgue.dm:615
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = await base.CanAllowThrough(mover, border_dir);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.locate<obj_structure_table>(get_turf(mover)) != null)
            return TRUE;
        return dot;
    }
}
