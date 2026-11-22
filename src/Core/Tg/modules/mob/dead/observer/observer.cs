// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/dead/observer/observer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the ghosts in the owner's minimal scope (2026-10-16, D151): becoming a ghost (ghostize, Initialize, the ghost's
// appearance), flying (Move, forceMove, Moved, Process_Spacemove), Re-enter Corpse, and the ghost's invisibility to the
// living (set_invisibility, update_sight, the ghost images, toggle_ghost_hud_flag), with Destroy, examine, the light cutoff
// and the perspective; M10x slice C (D281): Teleport, Orbit, Jump to Mob and Observe (the verbs' bodies), ManualFollow, the
// orbit and its end, the chat links (Topic, observer_view), observe-through-eyes (do_observe, cleanup_observe) and the ghost in
// the points of interest. Out of the scope and recorded (docs/ai/specs/not-translated.md section 16, live-partial.txt "Ghosts"):
// the data HUDs and scans, DNR, the revival alert, the view range, darkness and ghost-see toggles, the manifest, boo, possess,
// the spawners and minigames menus, the ghost appearance from preferences, the admin procs and the ghost's single clicks
// (attack_ghost). The [DmPath] of /mob/dead/observer is on _onclick/click.cs (rules 2.3).

// tg: code/modules/mob/dead/observer/observer.dm:1
public partial class datum_controller_global_vars : datum_controller
{
    //this is a list of the default (non-accessorized, non-dir) images of the ghosts themselves
    // tg: code/modules/mob/dead/observer/observer.dm:1  GLOBAL_LIST_EMPTY(ghost_images_default)
    public DmList ghost_images_default;

    // tg: code/modules/mob/dead/observer/observer.dm:1  (GLOBAL_MANAGED(ghost_images_default, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalghost_images_default()
    {
        ghost_images_default = Dm.list();
        Dm.append(ref gvars_datum_init_order, "ghost_images_default");
    }

    //this is a list of all ghost images as the simple white ghost
    // tg: code/modules/mob/dead/observer/observer.dm:2  GLOBAL_LIST_EMPTY(ghost_images_simple)
    public DmList ghost_images_simple;

    // tg: code/modules/mob/dead/observer/observer.dm:2  (GLOBAL_MANAGED(ghost_images_simple, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalghost_images_simple()
    {
        ghost_images_simple = Dm.list();
        Dm.append(ref gvars_datum_init_order, "ghost_images_simple");
    }

    // tg: code/modules/mob/dead/observer/observer.dm:4  GLOBAL_VAR_INIT(observer_default_invisibility, INVISIBILITY_OBSERVER)
    public object observer_default_invisibility;

    // tg: code/modules/mob/dead/observer/observer.dm:4  (GLOBAL_MANAGED(observer_default_invisibility, INVISIBILITY_OBSERVER), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalobserver_default_invisibility()
    {
        observer_default_invisibility = INVISIBILITY_OBSERVER;
        Dm.append(ref gvars_datum_init_order, "observer_default_invisibility");
    }
}

// tg: code/modules/mob/dead/observer/observer.dm:7
public partial class mob_dead_observer : mob_dead
{
    // tg: code/modules/mob/dead/observer/observer.dm:80
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        set_invisibility(GLOB.observer_default_invisibility);
        if (Dm.@in(icon_state, GLOB.ghost_forms_with_directions_list))
            ghostimage_default = image(this.icon, this, Dm.add(this.icon_state, "_nodir"));
        else
            ghostimage_default = image(this.icon, this, this.icon_state);
        ghostimage_default.@override = TRUE;
        Dm.combine(ref GLOB.ghost_images_default, ghostimage_default);

        ghostimage_simple = image(this.icon, this, "ghost_nodir");
        ghostimage_simple.@override = TRUE;
        Dm.combine(ref GLOB.ghost_images_simple, ghostimage_simple);

        updateallghostimages();

        turf T = null;
        mob body = loc as mob;
        if (ismob(body))
        {
            T = get_turf(body); //Where is the body located?

            gender = body.gender;
            if (Dm.truthy(body.mind) && Dm.truthy(body.mind.name))
                name = (body.mind.ghostname is var or__1 && Dm.truthy(or__1) ? or__1 : body.mind.name) as string;
            else
                name = (body.real_name is var or__2 && Dm.truthy(or__2) ? or__2 : generate_random_mob_name(gender)) as string;

            mind = body.mind; //we don't transfer the mind but we keep a reference to it.

            if (HAS_TRAIT_FROM_ONLY(body, TRAIT_SUICIDED, REF(body))) // transfer if the body was killed due to suicide
                ADD_TRAIT(this, TRAIT_SUICIDED, REF(body));

            if (ishuman(body))
            {
                mob_living_carbon_human body_human = body as mob_living_carbon_human;
                datum_species human_species = body_human.dna.species as datum_species;
                if (Dm.truthy(human_species.check_head_flags(HEAD_HAIR)))
                {
                    hairstyle = body_human.hairstyle;
                    hair_color = ghostify_color(body_human.hair_color);
                }
                if (Dm.truthy(human_species.check_head_flags(HEAD_FACIAL_HAIR)))
                {
                    facial_hairstyle = body_human.facial_hairstyle;
                    facial_hair_color = ghostify_color(body_human.facial_hair_color);
                }
            }
        }

        update_appearance();

        if (!Dm.truthy(T) || Dm.truthy(is_secret_level(T.z)))
        {
            DmList turfs = get_area_turfs(typeof(area_shuttle_arrival)) as DmList;
            if (Dm.truthy(length(turfs)))
                T = pick(turfs) as turf;
            else
                T = SSmapping.get_station_center() as turf;
        }

        Waiting.NoSleep(abstract_move(T));

        //To prevent nameless ghosts
        if (!Dm.truthy(name))
            name = generate_random_mob_name(FALSE) as string;
        real_name = name;

        Waiting.NoSleep(AddElement(typeof(datum_element_movetype_handler)));

        add_to_dead_mob_list();

        foreach (datum_atom_hud_alternate_appearance alt_hud in Dm.each_cast<datum_atom_hud_alternate_appearance>(GLOB.active_alternate_appearances))
        {
            alt_hud.apply_to_new_mob(this);
        }

        dot = base.Initialize(mapload, __extra);

        grant_all_languages();
        setup_hud_traits();
        toggle_ghost_hud_flag(Dm.bitor(GHOST_VISION, GHOST_DATA_HUDS));

        // tg: :150  (M10x slice C: the ghost in the orbit menu)
        Waiting.NoSleep(SSpoints_of_interest.make_point_of_interest(this));
        add_traits(Dm.list(TRAIT_HEAR_THROUGH_DARKNESS, TRAIT_GOOD_HEARING, TRAIT_DETECT_STORM, TRAIT_GHOSTLY_MOB), INNATE_TRAIT);
        return dot;
    }

    // tg: code/modules/mob/dead/observer/observer.dm:163
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(Dm.bitand(ghost_hud_flags, GHOST_DATA_HUDS)))
            remove_data_huds();

        // Update our old body's medhud since we're abandoning it
        if (isliving(mind?.current))
        {
            (mind.current as mob_living).med_hud_set_status();
        }

        Dm.remove(ref GLOB.ghost_images_default, ghostimage_default);
        ghostimage_default = null;

        Dm.remove(ref GLOB.ghost_images_simple, ghostimage_simple);
        ghostimage_simple = null;

        updateallghostimages();

        QDEL_NULL(ref spawners_menu);
        QDEL_NULL(ref minigames_menu);
        return base.Destroy(force);
    }

    /*
     * This proc will update the icon of the ghost itself, with hair overlays, as well as the ghost image.
     * Please call update_icon(updates, icon_state) from now on when you want to update the icon_state of the ghost,
     * or you might end up with hair on a sprite that's not supposed to get it.
     * Hair will always update its dir, so if your sprite has no dirs the haircut will go all over the place.
     * |- Ricotez
     */
    // tg: code/modules/mob/dead/observer/observer.dm:190
    [DmParams("updates", "new_form")]
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        updates ??= ALL;
        object new_form = Dm.arg(__extra, 0);
        object dot = null;
        dot = await base.update_icon(updates, __extra);

        if (Dm.truthy(client)) //We update our preferences in case they changed right before update_appearance was called.
        {
            ghost_accs = client.prefs.read_preference(typeof(datum_preference_choiced_ghost_accessories)) as string;
            ghost_others = client.prefs.read_preference(typeof(datum_preference_choiced_ghost_others)) as string;
        }

        if (Dm.truthy(hair_overlay))
        {
            cut_overlay(hair_overlay);
            hair_overlay = null;
        }

        if (Dm.truthy(facial_hair_overlay))
        {
            cut_overlay(facial_hair_overlay);
            facial_hair_overlay = null;
        }

        if (Dm.truthy(new_form))
        {
            icon_state = Dm.text("[]", new_form);
            if (Dm.@in(icon_state, GLOB.ghost_forms_with_directions_list))
                ghostimage_default.icon_state = Dm.text("[]", Dm.add(new_form, "_nodir")); //if this icon has dirs, the default ghostimage must use its nodir version or clients with the preference set to default sprites only will see the dirs
            else
                ghostimage_default.icon_state = Dm.text("[]", new_form);
        }

        if ((Dm.eq(ghost_accs, GHOST_ACCS_DIR) || Dm.eq(ghost_accs, GHOST_ACCS_FULL)) && Dm.@in(icon_state, GLOB.ghost_forms_with_directions_list)) //if this icon has dirs AND the client wants to show them, we make sure we update the dir on movement
        {
            updatedir = 1;
        }
        else
        {
            updatedir = 0; //stop updating the dir in case we want to show accessories with dirs on a ghost sprite without dirs
            await setDir(2); //reset the dir to its default so the sprites all properly align up
        }

        if (Dm.eq(ghost_accs, GHOST_ACCS_FULL) && Dm.@in(icon_state, GLOB.ghost_forms_with_accessories_list)) //check if this form supports accessories and if the client wants to show them
        {
            if (Dm.truthy(facial_hairstyle))
            {
                var S = SSaccessories.facial_hairstyles_list[facial_hairstyle] as datum_sprite_accessory;
                if (S != null && !Dm.eq(S.icon_state, SPRITE_ACCESSORY_NONE))
                {
                    facial_hair_overlay = (mutable_appearance)mutable_appearance(S.icon, S.icon_state as string, -HAIR_LAYER);
                    facial_hair_overlay.color = facial_hair_color;
                    facial_hair_overlay.alpha = 200;
                    add_overlay(facial_hair_overlay);
                }
            }
            if (Dm.truthy(hairstyle))
            {
                var S = SSaccessories.hairstyles_list[hairstyle] as datum_sprite_accessory_hair;
                if (S != null && !Dm.eq(S.icon_state, SPRITE_ACCESSORY_NONE))
                {
                    hair_overlay = (mutable_appearance)mutable_appearance(S.icon, S.icon_state as string, -HAIR_LAYER);
                    hair_overlay.color = hair_color;
                    hair_overlay.alpha = 200;
                    hair_overlay.pixel_z = S.y_offset;
                    add_overlay(hair_overlay);
                }
            }
        }
        return dot;
    }
}

// tg: code/modules/mob/dead/observer/observer.dm:244 (global procs; interleaved types, rules 1.9)
public static partial class GlobalProcs
{
    /*
     * Increase the brightness of a color and desaturates it slightly to make it suitable for ghosts
     * We use HSL for this, makes life SOOO easy
     */
    // tg: code/modules/mob/dead/observer/observer.dm:244
    public static object ghostify_color(object input_color = null)
    {
        DmList read_color = rgb2num(input_color as string, COLORSPACE_HSL);
        float sat = Dm.num(read_color[2]);
        float lum = Dm.num(read_color[3]);

        // Clamp so it still has color, can't get too bright/desaturated
        sat -= 15;
        if (sat < 30)
            sat = Dm.num(min(read_color[2], 30));

        lum += 15;
        if (lum > 80)
            lum = Dm.num(max(read_color[3], 80));
        return rgb(Dm.num(read_color[1]), sat, lum, space: COLORSPACE_HSL);
    }
}

// tg: code/modules/mob/dead/observer/observer.dm:264 (interleaved types, rules 1.9)
public partial class mob : atom_movable
{
    /**
     * # Ghostize
     *
     * Creates a /mob/dead/observer and moves the player's key into it (among other handling for player->observer)
     * Ignores things like adminghosts and corpselocked (ethereal) players.
     * Args:
     * can_reenter_corse: Whether the new Ghost will be able to click "Re-enter body", TRUE by default.
     * forced: Whether we are forcing this player to be ghosted, ignoring things like corpselocking, FALSE by default.
     */
    // tg: code/modules/mob/dead/observer/observer.dm:264
    public virtual object ghostize(float? can_reenter_corpse = null, float? forced = null)
    {
        can_reenter_corpse ??= TRUE;
        forced ??= FALSE;
        if (!Dm.truthy(key))
            return null;
        if (IS_FAKE_KEY(key)) // Skip aghosts.
            return null;

        if (HAS_TRAIT(this, TRAIT_CORPSELOCKED) && !Dm.truthy(forced))
        {
            if (Dm.truthy(can_reenter_corpse)) //If you can re-enter the corpse you can't leave when corpselocked
                return null;
            if (ishuman(usr)) //following code only applies to those capable of having an ethereal heart, ie humans
            {
                mob_living_carbon_human crystal_fella = usr as mob_living_carbon_human;
                object our_heart = crystal_fella.get_organ_slot(ORGAN_SLOT_HEART);
                if (istype(our_heart, typeof(obj_item_organ_heart_ethereal))) //so you got the heart?
                {
                    // tg: :278-279  var/obj/item/organ/heart/ethereal/ethereal_heart = our_heart;
                    //               ethereal_heart.stop_crystalization_process(crystal_fella) //stops the crystallization process
                    // GAP(lead): /obj/item/organ/heart/ethereal is generated only (the ethereals, after the core version).
                    Dm.NotTranslated("code/modules/mob/dead/observer/observer.dm:279", "ghostize: the ethereal heart's stop_crystalization_process (ethereals, after the core version)");
                }
            }
        }

        stop_sound_channel(CHANNEL_HEARTBEAT); //Stop heartbeat sounds because You Are A Ghost Now
        mob_dead_observer ghost = Dm.@new<mob_dead_observer>(this); // Transfer safety to observer spawning proc.
        SStgui.on_transfer(this, ghost); // Transfer NanoUIs.
        ghost.can_reenter_corpse = can_reenter_corpse;
        ghost.PossessByPlayer(key);
        ghost.client?.init_verbs();
        if (!Dm.truthy(can_reenter_corpse)) // Disassociates observer mind from the body mind
            ghost.mind = null;

        float recordable_time = world.time;
        mob former_mob = ghost.mind?.current; // tg types it /mob/living; the mind's current may be any mob (rules 3.2)
        if (isliving(former_mob))
            recordable_time = ((mob_living)former_mob).timeofdeath;

        ghost.persistent_client?.time_of_death = recordable_time;
        if (Dm.truthy(_listen_lookup?[COMSIG_MOB_GHOSTIZED]))
            _SendSignal(COMSIG_MOB_GHOSTIZED, Dm.list(this));
        return ghost;
    }
}

// tg: code/modules/mob/dead/observer/observer.dm:298 (interleaved types, rules 1.9)
public partial class mob_living : mob
{
    // tg: code/modules/mob/dead/observer/observer.dm:298
    public override object ghostize(float? can_reenter_corpse = null, float? forced = null)
    {
        can_reenter_corpse ??= TRUE;
        forced ??= FALSE;
        object dot = null;
        dot = base.ghostize(can_reenter_corpse, forced);
        if (Dm.truthy(dot) && Dm.truthy(can_reenter_corpse))
        {
            mob_dead_observer ghost = dot as mob_dead_observer;
            // tg: :302  ghost.mind.current?.med_hud_set_status()
            (ghost.mind.current as mob_living)?.med_hud_set_status();
        }
        return dot;
    }
}

// tg: code/modules/mob/dead/observer/observer.dm:328 (interleaved types, rules 1.9)
public partial class mob_dead_observer : mob_dead
{
    // tg: code/modules/mob/dead/observer/observer.dm:328  /mob/dead/observer/Move(NewLoc, direct, glide_size_override = 32)
    // (the root's parameter names, rules 7.2: NewLoc is newloc, direct is direction; tg's override has no update_dir)
    public override async DmTask<object> Move(atom newloc = null, object direction = null, float? glide_size_override = null, float? update_dir = null)
    {
        glide_size_override ??= 32;
        if (Dm.truthy(updatedir))
            await setDir(direction); //only update dir if we actually need it, so overlays won't spin on base sprites that don't have directions of their own

        if (Dm.truthy(glide_size_override))
            set_glide_size(glide_size_override);
        if (Dm.truthy(newloc))
        {
            await abstract_move(newloc);
        }
        else
        {
            turf destination = get_turf(this);

            if (Dm.truthy(Dm.bitand(direction, NORTH)) && y < world.maxy)
                destination = get_step(destination, NORTH);

            else if (Dm.truthy(Dm.bitand(direction, SOUTH)) && y > 1)
                destination = get_step(destination, SOUTH);

            if (Dm.truthy(Dm.bitand(direction, EAST)) && x < world.maxx)
                destination = get_step(destination, EAST);

            else if (Dm.truthy(Dm.bitand(direction, WEST)) && x > 1)
                destination = get_step(destination, WEST);

            await abstract_move(destination); //Get out of closets and such as a ghost
        }
        return null;
    }

    // tg: code/modules/mob/dead/observer/observer.dm:353
    public override async DmTask<object> forceMove(atom destination = null, params object[] __extra)
    {
        await abstract_move(destination); // move like the wind
        return TRUE;
    }

    // tg: code/modules/mob/dead/observer/observer.dm:357
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = null;
        dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        area new_area = get_area(this) as area;
        if (new_area != ambience_tracked_area)
        {
            update_ambience_area(new_area); // tg: :361
        }
        return dot;
    }

    // tg: code/modules/mob/dead/observer/observer.dm:363  GAME_VERB(/mob/dead/observer, reenter_corpse, "Re-enter Corpse", null):
    // the verb's body (the body proc /mob/dead/observer/proc/__gvb_reenter_corpse(list/structured_args),
    // __DEFINES/verb.dm:58); the verb itself (reenter_corpse) is below.
    public virtual object __gvb_reenter_corpse(DmList structured_args = null)
    {
        if (!Dm.truthy(client))
            return null;
        if (!Dm.truthy(mind) || QDELETED(mind.current))
        {
            to_chat(this, span_warning(I18n.T("You have no body.")));
            return null;
        }
        if (!Dm.truthy(can_reenter_corpse))
        {
            to_chat(this, span_warning(I18n.T("You cannot re-enter your body.")));
            return null;
        }
        if (Dm.truthy(mind.current.key) && !IS_FAKE_KEY(mind.current.key)) //makes sure we don't accidentally kick any clients
        {
            to_chat(usr, span_warning(I18n.T("Another consciousness is in your body...It is resisting you.")));
            return null;
        }
        // GAP(lead, modules/client/client_procs.cs): client/New's view size datum is a deferred leaf (client_procs.dm:589, the
        // UI, M9), so a client has none yet; the ghost's view range verbs that change it are out of D151's minimal scope.
        client.view_size?.resetToDefault(); //Let's reset so people can't become allseeing gods
        SStgui.on_transfer(this, mind.current); // Transfer NanoUIs.
        if (mind.current.stat == DEAD)
        {
            // tg: :378-379  if(... && SSlag_switch.measures[DISABLE_DEAD_KEYLOOP] && !client.holder) to_chat(src, span_warning("To
            // leave your body again use the 'Ghost verb' (in the command bar)."))
            // GAP(lead): SSlag_switch (controllers/subsystem/lag_switch.dm) is not declared; its measures are off by default, so
            // the test is FALSE (as death.cs).
            Dm.Deferred("code/modules/mob/dead/observer/observer.dm:378", "reenter_corpse: the lag switch's disabled dead keyloop notice (SSlag_switch, M10+)");
        }
        mind.current.PossessByPlayer(key);
        mind.current.client.init_verbs();
        return TRUE;
    }

    // tg: code/modules/mob/dead/observer/observer.dm:363
    // GAME_VERB(/mob/dead/observer, reenter_corpse, "Re-enter Corpse", null) expands (__DEFINES/verb.dm:33, :39-58) to the
    // type /datum/verb_metadata/mob/dead/observer/reenter_corpse (the verb registry), the verb below, which hands its arguments
    // to SSverbs.invoke_verb, and the proc __gvb_reenter_corpse with the body. tg's own code calls the verb as a proc (the HUD's
    // Re-enter Corpse button, screen_objects/ghost.dm:34; /datum/mind/grab_ghost). As pulled (living.cs, rules 7.10's M4a
    // exception): the set statements (the verb panel) stay M9 and so does SSverbs; the verb's INVOKE_ASYNC(SSverbs,
    // invoke_verb, src, /mob/dead/observer/verb/reenter_corpse, args, usr) is the body called with no arguments after
    // invoke_verb's own check (controllers/subsystem/verbs.dm:43-50): a caller without a client does nothing. The body does
    // not sleep, so the asynchronous call is a call.
    public virtual void reenter_corpse(params object[] __extra)
    {
        mob resolved_caller = usr ?? this;
        if (resolved_caller.client == null)
            return;
        __gvb_reenter_corpse(Dm.list());
    }

    // tg: code/modules/mob/dead/observer/observer.dm:415  (M8 slice E: the defibrillator's notice to the body's ghost)
    public virtual void send_revival_notification(object message = null, object sound = null, atom source = null, object flashwindow = null)
    {
        if (Dm.truthy(flashwindow))
        {
            // window_flash(client) (__HELPERS/game.dm:197): the client's taskbar flash is a winset (the window, M9)
            Dm.Deferred("code/modules/mob/dead/observer/observer.dm:417", "send_revival_notification: window_flash(client), the game window's taskbar flash (the client window, M10+)");
        }
        if (Dm.truthy(message))
        {
            to_chat(this, span_ghostalert(Dm.text("[]", message)));
            if (source != null)
            {
                // throw_alert("[REF(source)]_revival", /atom/movable/screen/alert/revival) with the source's picture and the
                // preference's HUD style: no caller of the port passes a source (the defibrillator does not)
                Dm.NotTranslated("code/modules/mob/dead/observer/observer.dm:421", "send_revival_notification: the revival alert with the source's picture (ghost alerts, M10+)");
            }
        }
        to_chat(this, span_ghostalert(Dm.text("<a href=byond://?src=[];reenter=1>(Click to re-enter)</a>", REF(this))));
        if (Dm.truthy(sound))
            Dm.output_to(this, Dm.sound(sound)); // SEND_SOUND(src, sound(sound)) (DIRECT_OUTPUT, rules 12)
    }

    // tg: code/modules/mob/dead/observer/observer.dm:438  GAME_VERB(/mob/dead/observer, dead_tele, "Teleport", null): the verb's
    // body (M10x slice C; the ghost HUD's Teleport button calls the verb, screen_objects/ghost.dm:60)
    public virtual async DmTask __gvb_dead_tele(DmList structured_args = null)
    {
        if (!isobserver(usr))
        {
            to_chat(usr, span_warning(I18n.T("Not when you're not dead!")));
            return;
        }
        DmList filtered = Dm.list();
        foreach (area A in Dm.each_cast<area>(get_sorted_areas()))
        {
            if (!Dm.truthy(Dm.bitand(A.area_flags, HIDDEN_AREA)))
                Dm.append(ref filtered, A);
        }
        area thearea = await tgui_input_list(usr, I18n.T("Area to jump to"), I18n.T("BOOYEA"), filtered) as area;

        if (thearea == null)
            return;
        // QDELETED: a ghost deleted while the list was open (its player went back to the body: SStgui.on_transfer moves the window to the
        // new mob) stays where it is; tg moves the deleted ghost and the spatial grid's enter_cell CRASHes
        if (!isobserver(usr) || QDELETED(usr))
        {
            to_chat(usr, span_warning(I18n.T("Not when you're not dead!")));
            return;
        }

        DmList L = Dm.list();
        foreach (turf T in Dm.each<turf>(get_area_turfs(thearea.type)))
            Dm.append(ref L, T);

        if (!Dm.truthy(L) || !Dm.truthy(length(L)))
        {
            to_chat(usr, span_warning(I18n.T("No area available.")));
            return;
        }

        await usr.abstract_move(pick(L) as atom);
    }

    // tg: code/modules/mob/dead/observer/observer.dm:438  the verb (as reenter_corpse's): a detached call of the body after
    // SSverbs.invoke_verb's check (a caller without a client does nothing); the body sleeps in tgui_input_list.
    public virtual void dead_tele(params object[] __extra)
    {
        mob resolved_caller = usr ?? this;
        if (resolved_caller.client == null)
            return;
        Waiting.Detach(__gvb_dead_tele(Dm.list()));
    }

    // tg: code/modules/mob/dead/observer/observer.dm:465  GAME_VERB(/mob/dead/observer, follow, "Orbit", null): the verb's body
    // (M10x slice C; the ghost HUD's Orbit button calls the orbit menu itself, screen_objects/ghost.dm:20)
    public virtual async DmTask __gvb_follow(DmList structured_args = null)
    {
        await GLOB.orbit_menu.show(this);
    }

    // tg: code/modules/mob/dead/observer/observer.dm:465  the verb (as dead_tele's)
    public virtual void follow(params object[] __extra)
    {
        mob resolved_caller = usr ?? this;
        if (resolved_caller.client == null)
            return;
        Waiting.Detach(__gvb_follow(Dm.list()));
    }

    // tg: code/modules/mob/dead/observer/observer.dm:469  GAME_VERB(/mob/dead/observer, jumptomob, "Jump to Mob", null): the
    // verb's body (M10x slice C). Moves the ghost instead of just changing the ghosts's eye -Nodrak. tg's only way in is the verb
    // panel and the command bar, which the port has not (lobby.md section 5).
    public virtual async DmTask __gvb_jumptomob(DmList structured_args = null)
    {
        if (!isobserver(usr)) //Make sure they're an observer!
            return;

        DmList possible_destinations = await SSpoints_of_interest.get_mob_pois() as DmList;
        object target = null;

        target = await tgui_input_list(usr, I18n.T("Please, select a player!"), I18n.T("Jump to Mob"), possible_destinations);
        if (target == null)
            return;
        if (!isobserver(usr))
            return;

        mob destination_mob = possible_destinations[target] as mob; //Destination mob

        // During the break between opening the input menu and selecting our target, has this become an invalid option?
        if (!Dm.truthy(await SSpoints_of_interest.is_valid_poi(destination_mob)))
            return;

        mob source_mob = this; //Source mob
        turf destination_turf = get_turf(destination_mob); //Turf of the destination mob

        if (isturf(destination_turf))
            await source_mob.abstract_move(destination_turf);
        else
            to_chat(source_mob, span_danger(I18n.T("This mob is not located in the game world.")));
    }

    // tg: code/modules/mob/dead/observer/observer.dm:469  the verb (as dead_tele's)
    public virtual void jumptomob(params object[] __extra)
    {
        mob resolved_caller = usr ?? this;
        if (resolved_caller.client == null)
            return;
        Waiting.Detach(__gvb_jumptomob(Dm.list()));
    }

    // tg: code/modules/mob/dead/observer/observer.dm:538  GAME_VERB(/mob/dead/observer, observe, "Observe", null): the verb's body
    // (M10x slice C). tg's ways in are the verb panel and the command bar, which the port has not; the orbit menu's auto-observe
    // reaches do_observe (orbit.dm:40).
    public virtual async DmTask __gvb_observe(DmList structured_args = null)
    {
        if (!isobserver(usr) || HAS_TRAIT(this, TRAIT_NO_OBSERVE)) //Make sure they're an observer!
            return;

        await reset_perspective(null);

        DmList possible_destinations = await SSpoints_of_interest.get_mob_pois() as DmList;
        object target = null;

        target = await tgui_input_list(usr, I18n.T("Please, select a player!"), I18n.T("Jump to Mob"), possible_destinations);
        if (target == null)
            return;
        if (!isobserver(usr))
            return;

        await reset_perspective(null); // Reset again for sanity

        mob chosen_target = possible_destinations[target] as mob;

        // During the break between opening the input menu and selecting our target, has this become an invalid option?
        if (!Dm.truthy(await SSpoints_of_interest.is_valid_poi(chosen_target)))
            return;

        if (chosen_target == usr)
            return;

        await do_observe(chosen_target);
    }

    // tg: code/modules/mob/dead/observer/observer.dm:538  the verb (as dead_tele's)
    public virtual void observe(params object[] __extra)
    {
        mob resolved_caller = usr ?? this;
        if (resolved_caller.client == null)
            return;
        Waiting.Detach(__gvb_observe(Dm.list()));
    }

    // tg: code/modules/mob/dead/observer/observer.dm:618
    /// Toggles a flag from ghost hud and updates the mob accordingly
    public virtual void toggle_ghost_hud_flag(object toggled = null)
    {
        ghost_hud_flags = Dm.num(Dm.bitxor(ghost_hud_flags, toggled));
        if (Dm.truthy(Dm.bitand(ghost_hud_flags, GHOST_DATA_HUDS)))
            show_data_huds();
        else
            remove_data_huds();
        update_sight();
        foreach (object hud_key in Dm.each(hud_used?.screen_objects))
        {
            atom_movable_screen_ghost_hudbox hud = hud_used.screen_objects[hud_key] as atom_movable_screen_ghost_hudbox;
            if (istype(hud, typeof(atom_movable_screen_ghost_hudbox)) && Dm.truthy(Dm.bitand(hud.relevant_flag, toggled)))
                hud.update_appearance(UPDATE_ICON_STATE);
        }
    }

    // This is the ghost's follow verb with an argument
    // tg: code/modules/mob/dead/observer/observer.dm:631
    public virtual async DmTask ManualFollow(atom_movable target = null)
    {
        if (!(target is atom_movable) || (Dm.truthy(is_secret_level(target.z)) && !Dm.truthy(client?.holder)))
            return;

        DmList icon_dimensions = get_icon_dimensions(target.icon);
        float orbitsize = (Dm.num(icon_dimensions["width"]) + Dm.num(icon_dimensions["height"])) * 0.5f;
        orbitsize -= (orbitsize / ICON_SIZE_ALL) * (ICON_SIZE_ALL * 0.25f);

        float rot_seg;

        string sw__1 = ghost_orbit;
        if (sw__1 == GHOST_ORBIT_TRIANGLE)
            rot_seg = 3;
        else if (sw__1 == GHOST_ORBIT_SQUARE)
            rot_seg = 4;
        else if (sw__1 == GHOST_ORBIT_PENTAGON)
            rot_seg = 5;
        else if (sw__1 == GHOST_ORBIT_HEXAGON)
            rot_seg = 6;
        else //Circular
            rot_seg = 36; //360/10 bby, smooth enough aproximation of a circle

        await orbit(target, orbitsize, FALSE, 20, rot_seg);
    }

    // tg: code/modules/mob/dead/observer/observer.dm:656
    public override async DmTask<object> orbit(atom A = null, float? radius = null, float? clockwise = null, float? rotation_speed = null, float? rotation_segments = null, float? pre_rotation = null, params object[] __extra)
    {
        await setDir(2); //reset dir so the right directional sprites show up
        return await base.orbit(A, radius, clockwise, rotation_speed, rotation_segments, pre_rotation, __extra);
    }

    // tg: code/modules/mob/dead/observer/observer.dm:660
    public override async DmTask stop_orbit(datum_component_orbiter orbits = null, float? refreshing = null)
    {
        await base.stop_orbit(orbits, refreshing);
        //restart our floating animation after orbit is done.
        pixel_y = base_pixel_y;
        // if we were autoobserving, reset perspective
        if (client != null && client.eye != null)
            await reset_perspective(null);
    }

    // tg: code/modules/mob/dead/observer/observer.dm:686
    public override void update_sight()
    {
        if (Dm.truthy(client))
        {
            ghost_others = client.prefs.read_preference(typeof(datum_preference_choiced_ghost_others)) as string; //A quick update just in case this setting was changed right before calling the proc
        }

        if (!Dm.truthy(Dm.bitand(ghost_hud_flags, GHOST_VISION)))
            set_invis_see(SEE_INVISIBLE_LIVING);
        else
            set_invis_see(SEE_INVISIBLE_OBSERVER);

        updateghostimages();
        base.update_sight();
    }

    // tg: code/modules/mob/dead/observer/observer.dm:705
    public virtual void updateghostimages()
    {
        if (!Dm.truthy(client))
            return;

        if (Dm.truthy(lastsetting))
        {
            object sw__1 = lastsetting; //checks the setting we last came from, for a little efficiency so we don't try to delete images from the client that it doesn't have anyway
            if (Dm.eq(sw__1, GHOST_OTHERS_DEFAULT_SPRITE))
            {
                if (client != null)
                    Dm.remove(client.images, GLOB.ghost_images_default);
            }
            else if (Dm.eq(sw__1, GHOST_OTHERS_SIMPLE))
            {
                if (client != null)
                    Dm.remove(client.images, GLOB.ghost_images_simple);
            }
        }
        lastsetting = client?.prefs.read_preference(typeof(datum_preference_choiced_ghost_others));
        if (!Dm.truthy(Dm.bitand(ghost_hud_flags, GHOST_VISION)))
            return;
        if (!Dm.eq(lastsetting, GHOST_OTHERS_THEIR_SETTING))
        {
            object sw__2 = lastsetting;
            if (Dm.eq(sw__2, GHOST_OTHERS_DEFAULT_SPRITE))
            {
                if (client != null)
                    Dm.combine(client.images, Dm.sub(GLOB.ghost_images_default, ghostimage_default));
            }
            else if (Dm.eq(sw__2, GHOST_OTHERS_SIMPLE))
            {
                if (client != null)
                    Dm.combine(client.images, Dm.sub(GLOB.ghost_images_simple, ghostimage_simple));
            }
        }
    }

    // tg: code/modules/mob/dead/observer/observer.dm:764  (M10x slice C: the chat links of dead chat and the ghost alerts, D154)
    public override async DmTask<object> Topic(object href = null, DmList href_list = null)
    {
        await base.Topic(href, href_list);
        if (usr == this)
        {
            if (Dm.truthy(href_list?["follow"]))
            {
                atom_movable target = Dm.locate_ref(href_list["follow"]) as atom_movable;
                if (target is atom_movable && target != this)
                {
                    await ManualFollow(target);
                    return null;
                }
            }

            if (Dm.truthy(href_list?["x"]) && Dm.truthy(href_list?["y"]) && Dm.truthy(href_list?["z"]))
            {
                float? tx = text2num(href_list["x"]);
                float? ty = text2num(href_list["y"]);
                float? tz = text2num(href_list["z"]);
                turf target = tx is float x && ty is float y && tz is float z ? locate(x, y, z) : null;
                if (target is turf)
                {
                    await abstract_move(target);
                    return null;
                }
            }

            if (Dm.truthy(href_list?["reenter"]))
            {
                reenter_corpse();
                return null;
            }

            if (Dm.truthy(href_list?["view"]))
            {
                atom target = Dm.locate_ref(href_list["view"]) as atom;
                await observer_view(target);
                return null;
            }

            if (Dm.truthy(href_list?["play"]))
            {
                atom_movable target = Dm.locate_ref(href_list["play"]) as atom_movable;
                await jump_to_interact(target);
            }
        }
        return null;
    }

    /// We orbit and interact with the target
    // tg: code/modules/mob/dead/observer/observer.dm:796
    public virtual async DmTask jump_to_interact(atom target = null)
    {
        if (target == null || target == this)
            return;

        await ManualFollow(target as atom_movable);
        // target.attack_ghost(usr) (:801): a ghost's interaction (_onclick/observer.dm; examine, the machines' windows to observers,
        // the scans) is not declared; no link of the port's chat names "play" (the minigames and spawners)
        Dm.NotTranslated("code/modules/mob/dead/observer/observer.dm:801", "jump_to_interact: target.attack_ghost(usr), a ghost's interaction (the ghost's clicks, M10+)");
    }

    /// We orbit the target or jump if its a turf
    // tg: code/modules/mob/dead/observer/observer.dm:804
    public virtual async DmTask observer_view(atom target = null)
    {
        if (target == null || target == this)
            return;

        if (isturf(target))
        {
            await abstract_move(target);
            return;
        }

        await ManualFollow(target as atom_movable);
    }

    //We don't want to update the current var
    //But we will still carry a mind.
    // tg: code/modules/mob/dead/observer/observer.dm:816
    public override async DmTask mind_initialize()
    {
        return;
    }

    // tg: code/modules/mob/dead/observer/observer.dm:819
    [DmPrivate]
    public virtual void show_data_huds()
    {
        ghost_hud_flags = Dm.num(Dm.bitor(ghost_hud_flags, GHOST_DATA_HUDS)); // only for safety, it should be set already.
        // tg: :822  add_traits(observer_hud_traits, REF(src))
        Dm.Deferred("code/modules/mob/dead/observer/observer.dm:822", "show_data_huds: add_traits(observer_hud_traits, REF(src)), the security, medical, diagnostic and bot path HUDs of a ghost (the data HUDs, out of the minimal ghost scope, D151; M10+)");
    }

    // tg: code/modules/mob/dead/observer/observer.dm:824
    [DmPrivate]
    public virtual void remove_data_huds()
    {
        ghost_hud_flags = Dm.num(Dm.bitand(ghost_hud_flags, Dm.bitnot(GHOST_DATA_HUDS))); // only for safety, it should be unset already.
        // tg: :827  remove_traits(observer_hud_traits, REF(src))
        Dm.Deferred("code/modules/mob/dead/observer/observer.dm:827", "remove_data_huds: remove_traits(observer_hud_traits, REF(src)), the ghost's data HUDs (the data HUDs, out of the minimal ghost scope, D151; M10+)");
    }

    // tg: code/modules/mob/dead/observer/observer.dm:829
    public virtual void set_ghost_appearance()
    {
        if (client?.prefs == null)
            return;

        client.prefs.apply_character_randomization_prefs();

        object species_type = client.prefs.read_preference(typeof(datum_preference_choiced_species));
        datum_species species = GLOB.species_prototypes[species_type] as datum_species;
        if (Dm.truthy(species.check_head_flags(HEAD_HAIR)))
        {
            hairstyle = client.prefs.read_preference(typeof(datum_preference_choiced_hairstyle));
            hair_color = ghostify_color(client.prefs.read_preference(typeof(datum_preference_color_hair_color)));
        }

        if (Dm.truthy(species.check_head_flags(HEAD_FACIAL_HAIR)))
        {
            facial_hairstyle = client.prefs.read_preference(typeof(datum_preference_choiced_facial_hairstyle));
            facial_hair_color = ghostify_color(client.prefs.read_preference(typeof(datum_preference_color_facial_hair_color)));
        }

        update_appearance();
    }

    // tg: code/modules/mob/dead/observer/observer.dm:847
    public override async DmTask<object> can_perform_action(atom target = null, object action_bitflags = null)
    {
        // tg: return isAdminGhostAI(usr)
        // GAP(lead): /proc/isAdminGhostAI and isAdminObserver (modules/mob/mob_helpers.dm:342, :357) are not declared: an
        // admin observer is a client with an admin holder (check_rights_for), and the port has no admins until M10, so it is
        // FALSE for every ghost.
        if (Dm.truthy(usr?.client?.holder))
            Dm.NotTranslated("code/modules/mob/dead/observer/observer.dm:848", "can_perform_action: isAdminGhostAI(usr) of an admin (admin tooling, M10+)");
        await DmTask.CompletedTask;
        return FALSE;
    }

    // tg: code/modules/mob/dead/observer/observer.dm:850
    public override object is_literate()
    {
        return TRUE;
    }

    // tg: code/modules/mob/dead/observer/observer.dm:853
    public override object can_read(atom viewed_atom = null, float? reading_check_flags = null, float? silent = null)
    {
        return TRUE; // we want to bypass all the checks
    }

    // tg: code/modules/mob/dead/observer/observer.dm:868  /mob/dead/observer/reset_perspective(atom/A) (the root's
    // parameter name, rules 7.2)
    public override async DmTask<object> reset_perspective(atom new_eye = null, params object[] __extra)
    {
        if (Dm.truthy(client))
        {
            if (ismob(client.eye) && client.eye != this)
                cleanup_observe();
        }
        if (Dm.truthy(await base.reset_perspective(new_eye, __extra)))
        {
            if (Dm.truthy(hud_used))
            {
                client.clear_screen();
                await hud_used.show_hud(hud_used.hud_version);
            }
        }
        return null;
    }

    // tg: code/modules/mob/dead/observer/observer.dm:878
    public virtual void cleanup_observe()
    {
        if (observetarget == null)
            return;
        mob target = observetarget;
        observetarget = null;
        if (client != null)
            client.perspective = MOB_PERSPECTIVE; // initial(client.perspective): BYOND's default
        set_sight(Dm.initial(this, (mob p) => p.sight));
        if (Dm.truthy(target))
        {
            UnregisterSignal(target, COMSIG_MOVABLE_Z_CHANGED);
            hide_other_mob_action_buttons(target);
            LAZYREMOVE(ref target.observers, this);
        }
    }

    // tg: code/modules/mob/dead/observer/observer.dm:890
    public virtual async DmTask do_observe(mob mob_eye = null)
    {
        if (isnewplayer(mob_eye))
        {
            stack_trace(Dm.text("/mob/dead/new_player: \\[[]\\] is being observed by []. This should never happen and has been blocked.", mob_eye, key_name(this)), "code/modules/mob/dead/observer/observer.dm", 892);
            // ADMIN_LOOKUPFLW(...) (__DEFINES/admin.dm): the admins' lookup and follow links are not declared; the key names stand for them
            message_admins(Dm.text("[] attempted to observe someone in the lobby: []. This should not be possible and has been blocked.", key_name(this), key_name(mob_eye)));
            return;
        }

        if (observetarget != null)
        {
            stack_trace(Dm.text("do_observe called on an observer ([]) who was already observing something! (observing: [], new target: [])", this, observetarget, mob_eye), "code/modules/mob/dead/observer/observer.dm", 897);
            message_admins(Dm.text("[] attempted to observe someone while already observing someone, this is a bug (and a past exploit) and should be investigated.", key_name(this)));
            return;
        }

        if (HAS_TRAIT(this, TRAIT_NO_OBSERVE))
            return;

        //Istype so we filter out points of interest that are not mobs
        if (Dm.truthy(client) && Dm.truthy(mob_eye) && mob_eye is mob)
        {
            await client.set_eye(mob_eye);
            client.perspective = EYE_PERSPECTIVE;
            if (Dm.truthy(is_secret_level(mob_eye.z)) && !Dm.truthy(client?.holder))
                set_sight(null); //we dont want ghosts to see through walls in secret areas
            RegisterSignal(mob_eye, COMSIG_MOVABLE_Z_CHANGED, nameof(on_observing_z_changed));
            if (Dm.truthy(mob_eye.hud_used))
            {
                client.clear_screen();
                LAZYOR(ref mob_eye.observers, this);
                await mob_eye.hud_used.show_hud(mob_eye.hud_used.hud_version, this);
                observetarget = mob_eye;
            }
        }
    }

    // tg: code/modules/mob/dead/observer/observer.dm:918
    [SignalHandler]
    public virtual void on_observing_z_changed(datum source = null, turf old_turf = null, turf new_turf = null)
    {
        if (Dm.truthy(is_secret_level(new_turf.z)) && !Dm.truthy(client?.holder))
            set_sight(null); //we dont want ghosts to see through walls in secret areas
        else
            set_sight(Dm.initial(this, (mob p) => p.sight));
    }

    // tg: code/modules/mob/dead/observer/observer.dm:926  (gaps package G1: the ghost's alt click opens the loot panel)
    public override async DmTask AltClickOn(atom target = null)
    {
        await client.loot_panel.open(get_turf(target));
    }

    // tg: code/modules/mob/dead/observer/observer.dm:939
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.examine(user, __extra);
        if (!Dm.truthy(invisibility))
            Dm.append(ref dot, I18n.T("It seems extremely obvious."));
        return dot;
    }

    // tg: code/modules/mob/dead/observer/observer.dm:951
    public virtual void set_invisibility(object value = null)
    {
        SetInvisibility(value, id: type);
        Waiting.NoSleep(set_light_on(!Dm.truthy(value) ? TRUE : FALSE));
    }

    // Ghosts have no momentum, being massless ectoplasm
    // tg: code/modules/mob/dead/observer/observer.dm:957
    public override async DmTask<object> Process_Spacemove(float? movement_dir = null, float? continuous_move = null)
    {
        await DmTask.CompletedTask;
        return TRUE;
    }

    // tg: code/modules/mob/dead/observer/observer.dm:984
    public override object default_lighting_cutoff()
    {
        datum_preferences prefs = client?.prefs;
        if (!Dm.truthy(prefs) || (Dm.truthy(client?.combo_hud_enabled) && Dm.truthy(Dm.bitand(prefs.toggles, COMBOHUD_LIGHTING))))
            return base.default_lighting_cutoff();
        return GLOB.ghost_lightings[prefs.read_preference(typeof(datum_preference_choiced_ghost_lighting))];
    }

    /// Called when we exit the orbiting state
    // tg: code/modules/mob/dead/observer/observer.dm:991
    [SignalHandler]
    public virtual void on_deorbit(datum source = null)
    {
        orbiting_ref = null;
    }
}

// tg: code/modules/mob/dead/observer/observer.dm:698 (global procs; interleaved types, rules 1.9)
public static partial class GlobalProcs
{
    // tg: code/modules/mob/dead/observer/observer.dm:961 (M10 slice H: the round's end makes the observers visible)
    public static void set_observer_default_invisibility(object amount = null, object message = null)
    {
        foreach (mob_dead_observer G in Dm.each<mob_dead_observer>(GLOB.player_list))
        {
            G.set_invisibility(amount);
            if (Dm.truthy(message))
                to_chat(G, message);
        }
        GLOB.observer_default_invisibility = amount;
    }

    // tg: code/modules/mob/dead/observer/observer.dm:698
    public static void updateallghostimages()
    {
        list_clear_nulls(GLOB.ghost_images_default);
        list_clear_nulls(GLOB.ghost_images_simple);

        foreach (mob_dead_observer O in Dm.each<mob_dead_observer>(GLOB.player_list))
            O.updateghostimages();
    }
}
