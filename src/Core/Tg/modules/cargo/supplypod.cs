// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/supplypod.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G4c, D316: the monkey agent comes by pod; the surplus crate and the contractor's extraction need it
// too) but the gondola style's pod mob (only the admins' pod launcher picks that style, not ported). The var blocks are generated
// (D111). close, insert, insertion_allowed and take_contents take other kinds of values than the closet slots declared; the slots
// are widened (decided rows, translation-rules Appendix A).

//The "pod_landingzone" temp visual is created by anything that "launches" a supplypod. This is what animates the pod and makes the pod forcemove to the station.
//------------------------------------SUPPLY POD-------------------------------------//
// tg: code/modules/cargo/supplypod.dm:3
public partial class obj_structure_closet_supplypod : obj_structure_closet
{
    // tg: code/modules/cargo/supplypod.dm:157
    [DmParams("mapload", "customStyle")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object customStyle = Dm.arg(__extra, 0) ?? FALSE;
        object dot = base.Initialize(mapload, __extra);
        if (loc == null)
        {
            atom shippingLane = GLOB.areas_by_type[typeof(area_centcom_central_command_areas_supplypod_supplypod_temp_holding)] as atom; //temporary holder for supplypods mid-transit
            Waiting.NoSleep(forceMove(shippingLane));
        }
        if (Dm.truthy(customStyle))
            style = customStyle as Type;
        set_style(style); //Upon initialization, give the supplypod an iconstate, name, and description based on the "style" variable. This system is important for the centcom_podlauncher to function correctly
        return dot;
    }

    // tg: code/modules/cargo/supplypod.dm:166
    public virtual void set_style(Type chosen_style) //Used to give the sprite an icon state, name, and description.
    {
        style = chosen_style;
        icon_state = Dm.initial(chosen_style, (datum_pod_style p) => p.icon_state);
        decal = Dm.initial(chosen_style, (datum_pod_style p) => p.decal_icon) as string;
        rubble_type = Dm.initial(chosen_style, (datum_pod_style p) => p.rubble_type);
        if (!Dm.truthy(adminNamed) && !Dm.truthy(specialised)) //We dont want to name it ourselves if it has been specifically named by an admin using the centcom_podlauncher datum
        {
            name = Dm.initial(chosen_style, (datum_pod_style p) => p.name);
            desc = Dm.initial(chosen_style, (datum_pod_style p) => p.desc);
        }
        if (Dm.truthy(Dm.initial(chosen_style, (datum_pod_style p) => p.has_door)))
            door = Dm.text("[]_door", icon_state);
        else
            door = null; // tg: door = FALSE
        update_appearance();
    }

    // tg: code/modules/cargo/supplypod.dm:180
    public virtual void SetReverseIcon()
    {
        fin_mask = "bottomfin";
        if (Dm.eq(Dm.initial(style, (datum_pod_style p) => p.shape), POD_SHAPE_NORMAL))
            icon_state = Dm.initial(style, (datum_pod_style p) => p.icon_state) + "_reverse";
        pixel_x = Dm.initial(this, (obj_structure_closet_supplypod p) => p.pixel_x);
        transform = Dm.matrix();
        update_appearance();
    }

    // tg: code/modules/cargo/supplypod.dm:188
    public virtual void backToNonReverseIcon()
    {
        fin_mask = Dm.initial(this, (obj_structure_closet_supplypod p) => p.fin_mask);
        if (Dm.eq(Dm.initial(style, (datum_pod_style p) => p.shape), POD_SHAPE_NORMAL))
            icon_state = Dm.initial(style, (datum_pod_style p) => p.icon_state);
        pixel_x = Dm.initial(this, (obj_structure_closet_supplypod p) => p.pixel_x);
        transform = Dm.matrix();
        update_appearance();
    }

    // tg: code/modules/cargo/supplypod.dm:196
    public override object closet_update_overlays(DmList new_overlays = null)
    {
        return null;
    }

    // tg: code/modules/cargo/supplypod.dm:199
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (ispath(style, typeof(datum_pod_style_invisible)))
            return dot;

        if (rubble != null)
            Dm.append(ref dot, rubble.get_foreground(this));

        if (ispath(style, typeof(datum_pod_style_seethrough)))
        {
            foreach (atom A in Dm.each<atom>(contents))
            {
                mutable_appearance itemIcon = Dm.@new<mutable_appearance>(A);
                itemIcon.transform = Dm.matrix().Translate(-1 * SUPPLYPOD_X_OFFSET, 0);
                Dm.append(ref dot, itemIcon);
            }
            foreach (object t in Dm.each(turfs_in_cargo)) //T is just a turf's type
            {
                Type turf_type = t as Type;
                mutable_appearance itemIcon = mutable_appearance(Dm.initial(turf_type, (turf p) => p.icon), Dm.initial(turf_type, (turf p) => p.icon_state)) as mutable_appearance;
                itemIcon.transform = Dm.matrix().Translate(-1 * SUPPLYPOD_X_OFFSET, 0);
                Dm.append(ref dot, itemIcon);
            }
            return dot;
        }

        if (Dm.truthy(opened)) //We're opened means all we have to worry about is masking a decal if we have one
        {
            if (!Dm.truthy(decal)) //We don't have a decal to mask
                return dot;
            if (!Dm.truthy(door)) //We have a decal but no door, so let's just add the decal
            {
                Dm.append(ref dot, decal);
                return dot;
            }
            icon masked_decal = Dm.icon(icon, decal); //The decal we want to apply
            icon door_masker = Dm.icon(icon, door); //The door shape we want to 'cut out' of the decal
            door_masker.MapColors(0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1);
            door_masker.SwapColor("#ffffffff", null);
            door_masker.Blend(COLOR_BLACK, ICON_SUBTRACT);
            masked_decal.Blend(door_masker, ICON_ADD);
            Dm.append(ref dot, masked_decal);
            return dot;
        }

        //If we're closed
        if (!Dm.truthy(door)) //We have no door, lets see if we have a decal. If not, theres nothing we need to do
        {
            if (Dm.truthy(decal))
                Dm.append(ref dot, decal);
            return dot;
        }
        else if (!Dm.eq(Dm.initial(style, (datum_pod_style p) => p.shape), POD_SHAPE_NORMAL)) //If we're not a normal pod shape (aka, if we don't have fins), just add the door without masking
            Dm.append(ref dot, door);
        else
        {
            icon masked_door = Dm.icon(icon, door); //The door we want to apply
            icon fin_masker = Dm.icon(icon, "mask_" + fin_mask); //The fin shape we want to 'cut out' of the door
            fin_masker.MapColors(0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1);
            fin_masker.SwapColor("#ffffffff", null);
            fin_masker.Blend(COLOR_BLACK, ICON_SUBTRACT);
            masked_door.Blend(fin_masker, ICON_ADD);
            Dm.append(ref dot, masked_door);
        }
        if (Dm.truthy(decal))
            Dm.append(ref dot, decal);
        return dot;
    }

    // tg: code/modules/cargo/supplypod.dm:255
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (Dm.truthy(bluespace)) //We dont want to worry about interacting with bluespace pods, as they are due to delete themselves soon anyways.
            return ITEM_INTERACT_BLOCKING;
        return await base.item_interaction(user, tool, modifiers, __extra);
    }

    // tg: code/modules/cargo/supplypod.dm:260
    public override object ex_act(object severity = null, object target = null, params object[] __extra) //Explosions dont do SHIT TO US! This is because supplypods create explosions when they land.
    {
        return FALSE;
    }

    // tg: code/modules/cargo/supplypod.dm:263
    public override async DmTask contents_explosion(object severity = null, object target = null) //Supplypods also protect their contents from the harmful effects of fucking exploding.
    {
        return;
    }

    // tg: code/modules/cargo/supplypod.dm:266
    public override async DmTask<object> toggle(mob_living user = null)
    {
        return null;
    }

    // tg: code/modules/cargo/supplypod.dm:269
    public override async DmTask<object> open(mob_living user = null, float? force = null, float? special_effects = null)
    {
        return null;
    }

    ///Called by the drop pods that return captured crewmembers from the ninja den.
    // tg: code/modules/cargo/supplypod.dm:273
    public virtual async DmTask return_from_capture(mob_living victim, turf destination = null)
    {
        destination ??= find_safe_turf(extended_safety_checks: TRUE, dense_atoms: FALSE) as turf;
        if (destination == null) //Uuuuh, something went wrong. This is gonna hurt.
        {
            to_chat(victim, span_hypnophrase(I18n.T("A million voices echo in your head... \"Seems where you got sent won't be able to handle our pod... as if we wanted the occupant to survive. Brace yourself, corporate dog.\"")));
            flags_1 = Dm.num(Dm.bitand(flags_1, Dm.bitnot(PREVENT_CONTENTS_EXPLOSION_1)));
            explosionSize = Dm.list(0, 1, 1, 1);
            destination = get_random_station_turf() as turf;
        }

        do_sparks(8, FALSE, victim);
        victim.visible_message(span_notice(I18n.T("[] vanishes...", victim)));

        await victim.forceMove(this);

        Dm.@new<obj_effect_pod_landingzone>(destination, this);

        if (Dm.truthy(victim._listen_lookup?[COMSIG_LIVING_RETURN_FROM_CAPTURE]))
            victim._SendSignal(COMSIG_LIVING_RETURN_FROM_CAPTURE, Dm.list(victim, destination));
    }

    // tg: code/modules/cargo/supplypod.dm:294
    public virtual async DmTask handleReturnAfterDeparting(atom_movable holder = null)
    {
        holder ??= this;
        reversing = FALSE; //Now that we're done reversing, we set this to false (otherwise we would get stuck in an infinite loop of calling the close proc at the bottom of open_pod() )
        bluespace = TRUE; //Make it so that the pod doesn't stay in centcom forever
        pod_flags = Dm.num(Dm.bitand(pod_flags, Dm.bitnot(FIRST_SOUNDS))); //Make it so we play sounds now
        if (!Dm.truthy(effectQuiet) && !ispath(style, typeof(datum_pod_style_seethrough)))
            audible_message(span_notice(I18n.T("The pod hisses, closing and launching itself away from the station.")), span_notice(I18n.T("The ground vibrates, and you hear the sound of engines firing.")));
        stay_after_drop = FALSE;
        holder.pixel_z = Dm.initial(holder, (atom_movable p) => p.pixel_z);
        holder.alpha = Dm.initial(holder, (atom_movable p) => p.alpha);
        if (holder != this)
        {
            foreach (object moved in Dm.each(holder.contents.Copy()))
            {
                if (!Dm.@in(moved, contents))
                    await ((atom_movable)moved).forceMove(this); // contents |= holder.contents
            }
            qdel(holder);
        }
        atom shippingLane = GLOB.areas_by_type[typeof(area_centcom_central_command_areas_supplypod_supplypod_temp_holding)] as atom;
        await forceMove(shippingLane); //Move to the centcom-z-level until the pod_landingzone says we can drop back down again
        if (!Dm.truthy(reverse_dropoff_coords)) //If we're centcom-launched, the reverse dropoff turf will be a centcom loading bay. If we're an extraction pod, it should be the ninja jail. Thus, this shouldn't ever really happen.
        {
            obj_effect_landmark_error error_landmark = locate<obj_effect_landmark_error>(GLOB.landmarks_list);
            turf error_landmark_turf = get_turf(error_landmark);
            reverse_dropoff_coords = Dm.list(error_landmark_turf.x, error_landmark_turf.y, error_landmark_turf.z);
        }
        if (Dm.truthy(custom_rev_delay))
            delays = reverse_delays;
        backToNonReverseIcon();
        if (Dm.truthy(this._listen_lookup?[COMSIG_SUPPLYPOD_RETURNING]))
            this._SendSignal(COMSIG_SUPPLYPOD_RETURNING, Dm.list(this));
        turf return_turf = locate(Dm.num(reverse_dropoff_coords[1]), Dm.num(reverse_dropoff_coords[2]), Dm.num(reverse_dropoff_coords[3]));
        Dm.@new<obj_effect_pod_landingzone>(return_turf, this);
    }

    // tg: code/modules/cargo/supplypod.dm:320
    public virtual async DmTask pre_open() //Called before the open_pod() proc. Handles anything that occurs right as the pod lands.
    {
        turf turf_underneath = get_turf(this);
        DmList boom = explosionSize;
        resistance_flags = Dm.initial(this, (obj_structure_closet_supplypod p) => p.resistance_flags);
        set_density(TRUE); //Density is originally false so the pod doesn't block anything while it's still falling through the air
        if (Dm.truthy(effectShrapnel))
            await AddComponent(typeof(datum_component_pellet_cloud), Dm.named("projectile_type", shrapnel_type), Dm.named("magnitude", shrapnel_magnitude));
        if (Dm.truthy(this._listen_lookup?[COMSIG_SUPPLYPOD_LANDED]))
            this._SendSignal(COMSIG_SUPPLYPOD_LANDED, Dm.list(this));
        foreach (mob_living target_living in Dm.each<mob_living>(turf_underneath))
        {
            if (iscarbon(target_living)) //If effectLimb is true (which means we pop limbs off when we hit people):
            {
                if (Dm.truthy(effectLimb))
                {
                    mob_living_carbon carbon_target_mob = target_living as mob_living_carbon;
                    foreach (obj_item_bodypart bodypart in Dm.each<obj_item_bodypart>(carbon_target_mob.get_bodyparts())) //Look at the bodyparts in our poor mob beneath our pod as it lands
                    {
                        if (!Dm.eq(bodypart.body_part, HEAD) && !Dm.eq(bodypart.body_part, CHEST)) //we dont want to kill him, just teach em a lesson!
                        {
                            if (Dm.truthy(await bodypart.dismember())) //Using the power of flextape i've sawed this man's limb in half!
                                break;
                        }
                    }
                }
                if (Dm.truthy(effectOrgans)) //effectOrgans means remove every organ in our mob
                {
                    mob_living_carbon carbon_target_mob = target_living as mob_living_carbon;
                    foreach (obj_item_organ organ_to_yeet in Dm.each<obj_item_organ>(carbon_target_mob.organs))
                    {
                        turf destination = get_edge_target_turf(turf_underneath, pick(GLOB.alldirs)) as turf; //Pick a random direction to toss them in
                        await organ_to_yeet.Remove(carbon_target_mob); //Note that this isn't the same proc as for lists
                        await organ_to_yeet.forceMove(turf_underneath); //Move the organ outta the body
                        await organ_to_yeet.throw_at(destination, 2, 3); //Thow the organ at a random tile 3 spots away
                        await sleep(0.1f * SECONDS);
                    }
                    foreach (obj_item_bodypart bodypart in Dm.each<obj_item_bodypart>(carbon_target_mob.get_bodyparts())) //Look at the bodyparts in our poor mob beneath our pod as it lands
                    {
                        turf destination = get_edge_target_turf(turf_underneath, pick(GLOB.alldirs)) as turf;
                        if (Dm.truthy(await bodypart.dismember())) //Using the power of flextape i've sawed this man's bodypart in half!
                        {
                            await bodypart.throw_at(destination, 2, 3);
                            await sleep(0.1f * SECONDS);
                        }
                    }
                }
            }

            if (Dm.truthy(effectGib)) //effectGib is on, that means whatever's underneath us better be fucking oof'd on
            {
                await target_living.adjust_brute_loss(5000); //THATS A LOT OF DAMAGE (called just in case gib() doesnt work on em)
                if (!QDELETED(target_living))
                    await target_living.gib(DROP_ALL_REMAINS); //After adjusting the fuck outta that brute loss we finish the job with some satisfying gibs
            }
            else
                await target_living.adjust_brute_loss(damage);
        }

        if (boom != null && boom.len == 4)
            boom.len += 1;
        if (boom != null && Dm.truthy(Dm.num(boom[1]) + Dm.num(boom[2]) + Dm.num(boom[3]) + Dm.num(boom[4]) + Dm.num(boom[5]))) // spawn explosion only if there is actual explosion range
            await explosion(turf_underneath, Dm.num(boom[1]), Dm.num(boom[2]), Dm.num(boom[3]), flame_range: boom[4], flash_range: boom[5], silent: effectQuiet, ignorecap: this is obj_structure_closet_supplypod_centcompod ? TRUE : FALSE, explosion_cause: this);
        else if (!Dm.truthy(effectQuiet) && !Dm.truthy(Dm.bitand(pod_flags, FIRST_SOUNDS))) //If our explosion list IS all zeroes, we still make a nice explosion sound (unless the effectQuiet var is true)
            playsound(this, SFX_EXPLOSION, Dm.truthy(landingSound) ? soundVolume * 0.25f : soundVolume, TRUE);
        if (Dm.truthy(landingSound))
            playsound(turf_underneath, landingSound, soundVolume, FALSE, FALSE);
        if (Dm.truthy(effectMissile)) //If we are acting like a missile, then right after we land and finish fucking shit up w explosions, we should delete
        {
            opened = TRUE; //We set opened to TRUE to avoid spending time trying to open (due to being deleted) during the Destroy() proc
            qdel(this);
            return;
        }
        if (ispath(style, typeof(datum_pod_style_gondola))) //Checks if we are supposed to be a gondola pod. If so, create a gondolapod mob, and move this pod to nullspace. I'd like to give a shout out, to my man oranges
        {
            // var/mob/living/basic/pet/gondola/gondolapod/benis = new(turf_underneath, src) ... open_pod(benis) after the opening delay
            Dm.NotTranslated("code/modules/cargo/supplypod.dm:385", "pre_open: the gondola style's pod mob (/mob/living/basic/pet/gondola/gondolapod; only the admins' pod launcher picks the style)");
        }
        else if (ispath(style, typeof(datum_pod_style_seethrough)))
        {
            transform = Dm.matrix();
            await open_pod(this);
        }
        else
            _addtimer(Dm.@new<datum_callback>(this, nameof(open_pod), this), Dm.num(delays[POD_OPENING]), file: "code/modules/cargo/supplypod.dm", line: 396); //After the opening delay passes, we use the open proc from this supplypod, while referencing this supplypod's contents
    }

    // tg: code/modules/cargo/supplypod.dm:398
    public virtual async DmTask open_pod(atom_movable holder, float? broken = null, float? forced = null) //The holder var represents an atom whose contents we will be working with
    {
        broken ??= FALSE;
        forced ??= FALSE;
        if (holder == null)
            return;
        if (Dm.truthy(opened)) //This is to ensure we don't open something that has already been opened
            return;
        holder.set_opened();
        turf turf_underneath = get_turf(holder); //Get the turf of whoever's contents we're talking about
        if (holder is mob holder_as_mob) //Allows mobs to assume the role of the holder, meaning we look at the mob's contents rather than the supplypod's contents. Typically by this point the supplypod's contents have already been moved over to the mob's contents
        {
            if (Dm.truthy(holder_as_mob.key) && !Dm.truthy(forced) && !Dm.truthy(broken)) //If we are player controlled, then we shouldn't open unless the opening is manual, or if it is due to being destroyed (represented by the "broken" parameter)
                return;
        }
        if (Dm.truthy(openingSound))
            playsound(get_turf(holder), openingSound, soundVolume, FALSE, FALSE); //Special admin sound to play
        foreach (object turf_type in Dm.each(turfs_in_cargo))
            await turf_underneath.place_on_top(turf_type as Type);
        foreach (atom_movable movable_cargo in Dm.each<atom_movable>(holder.contents))
            await movable_cargo.forceMove(turf_underneath);
        if (!Dm.truthy(effectQuiet) && !Dm.truthy(openingSound) && !ispath(style, typeof(datum_pod_style_seethrough)) && !Dm.truthy(Dm.bitand(pod_flags, FIRST_SOUNDS))) //If we aren't being quiet, play the default pod open sound
            playsound(get_turf(holder), open_sound, 15, TRUE, -3);
        if (Dm.truthy(broken)) //If the pod is opening because it's been destroyed, we end here
            return;
        if (ispath(style, typeof(datum_pod_style_seethrough)))
            await start_exit_sequence(this);
        else
        {
            if (Dm.truthy(reversing))
                _addtimer(Dm.@new<datum_callback>(this, nameof(SetReverseIcon)), Dm.num(delays[POD_LEAVING]) / 2, file: "code/modules/cargo/supplypod.dm", line: 424); //Finish up the pod's duties after a certain amount of time
            if (!Dm.truthy(stay_after_drop)) // Departing should be handled manually
                _addtimer(Dm.@new<datum_callback>(this, nameof(start_exit_sequence), holder), Dm.num(delays[POD_LEAVING]) * (4f / 5), file: "code/modules/cargo/supplypod.dm", line: 426); //Finish up the pod's duties after a certain amount of time
        }
    }

    // tg: code/modules/cargo/supplypod.dm:428
    public virtual async DmTask start_exit_sequence(atom_movable holder)
    {
        if (Dm.truthy(leavingSound))
            playsound(get_turf(holder), leavingSound, soundVolume, FALSE, FALSE);
        if (Dm.truthy(reversing)) //If we're reversing, we call the close proc. This sends the pod back up to centcom
            await close(holder);
        else if (Dm.truthy(bluespace)) //If we're a bluespace pod, then delete ourselves (along with our holder, if a separate holder exists)
        {
            delete_rubble();
            if (!Dm.truthy(effectQuiet) && Dm.truthy(create_sparks) && !ispath(style, typeof(datum_pod_style_invisible)) && !ispath(style, typeof(datum_pod_style_seethrough)))
                do_sparks(5, TRUE, holder); //Create some sparks right before closing
            qdel(this); //Delete ourselves and the holder
            if (holder != this)
                qdel(holder);
        }
    }

    // tg: code/modules/cargo/supplypod.dm:441
    public override async DmTask<object> close(atom_movable user = null) //Closes the supplypod and sends it back to centcom. Should only ever be called if the "reversing" variable is true
    {
        atom_movable holder = user;
        if (holder == null)
            return null;
        await take_contents(holder);
        if (Dm.truthy(close_sound))
            playsound(holder, close_sound, soundVolume * 0.75f, TRUE, -3);
        holder.set_closed();
        _addtimer(Dm.@new<datum_callback>(this, nameof(pre_return), holder), Dm.num(delays[POD_LEAVING]) * 0.2f, file: "code/modules/cargo/supplypod.dm", line: 448); //Start to leave a bit after closing for cinematic effect
        return null;
    }

    // tg: code/modules/cargo/supplypod.dm:450
    public override async DmTask take_contents(object mapload = null)
    {
        atom_movable holder = mapload as atom_movable;
        turf turf_underneath = holder.drop_location() as turf;
        foreach (object atom_to_check in Dm.each(turf_underneath.contents.Copy()))
        {
            if (atom_to_check != this && !Dm.truthy(await insert(atom_to_check, holder))) // Can't insert that
                continue;
        }
        await insert(turf_underneath, holder);
    }

    // tg: code/modules/cargo/supplypod.dm:457
    public override async DmTask<object> insert(object inserted = null, object mapload = null)
    {
        atom to_insert = inserted as atom;
        atom_movable holder = mapload as atom_movable;
        if (!Dm.truthy(await insertion_allowed(to_insert)))
            return FALSE;

        if (to_insert is turf turf_to_insert)
        {
            turfs_in_cargo.Add(turf_to_insert.type);
            await turf_to_insert.ScrapeAway();
            return TRUE;
        }

        atom_movable movable_to_insert = to_insert as atom_movable;
        if (movable_to_insert is mob mob_to_insert)
        {
            if (mob_to_insert.buckled != null)
                await mob_to_insert.buckled.unbuckle_mob(mob_to_insert as mob_living, force: TRUE);
        }
        await movable_to_insert.forceMove(holder);
        return null;
    }

    // tg: code/modules/cargo/supplypod.dm:474
    public override async DmTask<object> insertion_allowed(atom AM = null)
    {
        atom to_insert = AM;
        if (Dm.eq(to_insert.invisibility, INVISIBILITY_ABSTRACT))
            return FALSE;
        if (ismob(to_insert))
        {
            if (!Dm.truthy(reverse_option_list["Mobs"]))
                return FALSE;
            if (!isliving(to_insert)) //let's not put ghosts or eye mobs inside
                return FALSE;
            mob_living mob_to_insert = to_insert as mob_living;
            if (Dm.truthy(mob_to_insert.anchored) || Dm.truthy(mob_to_insert.incorporeal_move))
                return FALSE;
            await mob_to_insert.stop_pulling();
        }
        else if (isobj(to_insert))
        {
            obj obj_to_insert = to_insert as obj;
            if (obj_to_insert is obj_structure_closet_supplypod)
                return FALSE;
            if (obj_to_insert is obj_effect_supplypod_smoke)
                return FALSE;
            if (obj_to_insert is obj_effect_pod_landingzone)
                return FALSE;
            if (obj_to_insert is obj_effect_supplypod_rubble)
                return FALSE;

            if (HAS_TRAIT(obj_to_insert, TRAIT_UNDERFLOOR))
                return Dm.truthy(reverse_option_list["Underfloor"]) ? TRUE : FALSE;
            if (isProbablyWallMounted(obj_to_insert))
                return Dm.truthy(reverse_option_list["Wallmounted"]) ? TRUE : FALSE;

            if (!Dm.truthy(obj_to_insert.anchored) && Dm.truthy(reverse_option_list["Unanchored"]))
                return TRUE;
            if (Dm.truthy(obj_to_insert.anchored) && !ismecha(obj_to_insert) && Dm.truthy(reverse_option_list["Anchored"])) //Mecha are anchored but there is a separate option for them
                return TRUE;
            if (ismecha(obj_to_insert) && Dm.truthy(reverse_option_list["Mecha"]))
                return TRUE;
            return FALSE;
        }
        else if (isturf(to_insert))
        {
            if (isfloorturf(to_insert) && Dm.truthy(reverse_option_list["Floors"]))
                return TRUE;
            if (isfloorturf(to_insert) && !Dm.truthy(reverse_option_list["Floors"]))
                return FALSE;
            if (isclosedturf(to_insert) && Dm.truthy(reverse_option_list["Walls"]))
                return TRUE;
            if (isclosedturf(to_insert) && !Dm.truthy(reverse_option_list["Walls"]))
                return FALSE;
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/cargo/supplypod.dm:525
    public virtual DmTask pre_return(atom_movable holder)
    {
        delete_rubble();
        animate(holder, Dm.named("alpha", 0), Dm.named("time", 8), Dm.named("easing", Dm.bitor(QUAD_EASING, EASE_IN)), Dm.named("flags", ANIMATION_PARALLEL));
        animate(holder, Dm.named("pixel_z", 400), Dm.named("time", 10), Dm.named("easing", Dm.bitor(QUAD_EASING, EASE_IN)), Dm.named("flags", ANIMATION_PARALLEL)); //Animate our rising pod
        _addtimer(Dm.@new<datum_callback>(this, nameof(handleReturnAfterDeparting), holder), 15, file: "code/modules/cargo/supplypod.dm", line: 529); //Finish up the pod's duties after a certain amount of time
        return DmTask.CompletedTask;
    }

    // tg: code/modules/cargo/supplypod.dm:538
    public override void set_opened() //Proc exists here, as well as in any atom that can assume the role of a "holder" of a supplypod. Check the open_pod() proc for more details
    {
        opened = TRUE;
        set_density(FALSE);
        update_appearance();
        Waiting.NoSleep(after_open(null, FALSE));
    }

    // tg: code/modules/cargo/supplypod.dm:550
    public override void set_closed() //Ditto
    {
        opened = FALSE;
        set_density(TRUE);
        update_appearance();
        Waiting.NoSleep(after_close(null, FALSE));
    }

    // tg: code/modules/cargo/supplypod.dm:556
    public virtual void try_make_rubble(turf T) //Ditto
    {
        if (Dm.eq(rubble_type, RUBBLE_NONE))
            return;
        if (rubble != null)
            return;
        if (Dm.truthy(effectMissile))
            return;
        if (isspaceturf(T) || isclosedturf(T))
            return;
        rubble = Dm.@new<obj_effect_supplypod_rubble>(T);
        rubble.set_style(rubble_type, this);
        update_appearance();
    }

    // tg: code/modules/cargo/supplypod.dm:569
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        momentum_change ??= TRUE;
        delete_rubble();
        return await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
    }

    // tg: code/modules/cargo/supplypod.dm:573
    public virtual void delete_rubble()
    {
        rubble?.fade_away();
        rubble = null;
        update_appearance();
    }

    // tg: code/modules/cargo/supplypod.dm:578
    public virtual void add_glow()
    {
        if (!Dm.eq(Dm.initial(style, (datum_pod_style p) => p.shape), POD_SHAPE_NORMAL))
            return;
        glow_effect = Dm.@new<obj_effect_engineglow>(this);
        glow_effect.icon_state = "pod_glow_" + Dm.initial(style, (datum_pod_style p) => p.glow_color);
        Dm.append(vis_contents, glow_effect);
        glow_effect.layer = GASFIRE_LAYER;
        SET_PLANE_EXPLICIT(glow_effect, ABOVE_GAME_PLANE, this);
        RegisterSignal(glow_effect, COMSIG_QDELETING, nameof(remove_glow));
    }

    // tg: code/modules/cargo/supplypod.dm:588
    public override async DmTask on_changed_z_level(turf old_turf = null, turf new_turf = null, object same_z_layer = null, float? notify_contents = null)
    {
        await base.on_changed_z_level(old_turf, new_turf, same_z_layer, notify_contents);
        if (Dm.truthy(same_z_layer))
            return;
        if (glow_effect != null)
            SET_PLANE_EXPLICIT(glow_effect, ABOVE_GAME_PLANE, this);
    }

    // tg: code/modules/cargo/supplypod.dm:595
    public virtual void end_glow()
    {
        if (glow_effect == null)
            return;
        glow_effect.layer = LOW_ITEM_LAYER;
        glow_effect.fade_away(Dm.num(delays[POD_OPENING]));
        //Trust the signals
    }

    // tg: code/modules/cargo/supplypod.dm:602
    [SignalHandler]
    public virtual void remove_glow()
    {
        UnregisterSignal(glow_effect, COMSIG_QDELETING);
        vis_contents.Remove(glow_effect);
        glow_effect = null;
    }

    // tg: code/modules/cargo/supplypod.dm:608
    public override object Destroy(float? force = null)
    {
        delete_rubble();
        //Trust the signals even harder
        qdel(glow_effect);
        Waiting.NoSleep(open_pod(this, broken: TRUE)); //Lets dump our contents by opening up
        return base.Destroy(force);
    }
}

// tg: code/modules/cargo/supplypod.dm:87
public partial class obj_structure_closet_supplypod_podspawn_deathmatch : obj_structure_closet_supplypod_podspawn
{
    // tg: code/modules/cargo/supplypod.dm:91
    public override async DmTask pre_open()
    {
        foreach (mob_living critter in Dm.each<mob_living>(contents))
            critter.set_faction(Dm.list(FACTION_HOSTILE)); //No infighting, but also KILL!!
        await base.pre_open();
    }
}

// tg: code/modules/cargo/supplypod.dm:96
public partial class obj_structure_closet_supplypod_extractionpod : obj_structure_closet_supplypod
{
    // tg: code/modules/cargo/supplypod.dm:531
    public override async DmTask pre_return(atom_movable holder)
    {
        // Double ensure we're loaded, this SHOULD be here by now but you never know (the load sleeps: the timer's callback waits)
        await SSmapping.lazy_load_template(LAZY_TEMPLATE_KEY_NINJA_HOLDING_FACILITY);
        turf picked_turf = pick(GLOB.holdingfacility) as turf;
        reverse_dropoff_coords = Dm.list(picked_turf.x, picked_turf.y, picked_turf.z);
        await base.pre_return(holder);
    }

    // tg: code/modules/cargo/supplypod.dm:544
    public override void set_opened()
    {
        opened = TRUE;
        set_density(TRUE);
        update_appearance();
        Waiting.NoSleep(after_open(null, FALSE));
    }
}

//------------------------------------TEMPORARY_VISUAL-------------------------------------//
// tg: code/modules/cargo/supplypod.dm:616
public partial class obj_effect_supplypod_smoke : obj_effect //Falling pod smoke
{
    // tg: code/modules/cargo/supplypod.dm:640
    public virtual void draw_self(object amount)
    {
        alpha = max(0, 255 - (Dm.num(amount) * 20));
    }
}

// tg: code/modules/cargo/supplypod.dm:625
public partial class obj_effect_engineglow : obj_effect //Falling pod smoke
{
    // tg: code/modules/cargo/supplypod.dm:635
    public virtual void fade_away(float leaveTime)
    {
        float duration = min(leaveTime, 25);
        animate(this, Dm.named("alpha", 0), Dm.named("time", duration));
        float delay = duration + 5;
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), delay > GC_FILTER_QUEUE ? WEAKREF(this) : this), delay, file: "code/modules/cargo/supplypod.dm", line: 638);
    }
}

// tg: code/modules/cargo/supplypod.dm:643
public partial class obj_effect_supplypod_rubble : obj_effect
{
    // tg: code/modules/cargo/supplypod.dm:655
    public virtual mutable_appearance get_foreground(obj_structure_closet_supplypod pod)
    {
        mutable_appearance rubble_overlay = mutable_appearance(Dm.resource("icons/obj/supplypods.dmi"), foreground) as mutable_appearance;
        rubble_overlay.appearance_flags = Dm.num(Dm.bitor(KEEP_APART, RESET_TRANSFORM));
        rubble_overlay.transform = Dm.matrix().Translate(SUPPLYPOD_X_OFFSET - pod.pixel_x, verticle_offset);
        return rubble_overlay;
    }

    // tg: code/modules/cargo/supplypod.dm:661
    public virtual void fade_away()
    {
        animate(this, Dm.named("alpha", 0), Dm.named("time", 30));
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), 35 > GC_FILTER_QUEUE ? WEAKREF(this) : this), 35, file: "code/modules/cargo/supplypod.dm", line: 663);
    }

    // tg: code/modules/cargo/supplypod.dm:665
    public virtual void set_style(object type, obj_structure_closet_supplypod pod)
    {
        if (Dm.eq(type, RUBBLE_WIDE))
        {
            icon_state += "_wide";
            foreground += "_wide";
        }
        if (Dm.eq(type, RUBBLE_THIN))
        {
            icon_state += "_thin";
            foreground += "_thin";
        }
        if (ispath(pod.style, typeof(datum_pod_style_box)))
            verticle_offset = -2;
        else
            verticle_offset = Dm.initial(this, (obj_effect_supplypod_rubble p) => p.verticle_offset);

        pixel_y = verticle_offset;
    }
}

// tg: code/modules/cargo/supplypod.dm:679
public partial class obj_effect_pod_landingzone_effect : obj_effect
{
    // tg: code/modules/cargo/supplypod.dm:686
    [DmParams("mapload", "pod")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_structure_closet_supplypod pod = Dm.arg(__extra, 0) as obj_structure_closet_supplypod;
        object dot = base.Initialize(mapload, __extra);
        if (pod == null)
        {
            stack_trace("Pod landingzone effect created with no pod", "code/modules/cargo/supplypod.dm", 689);
            return INITIALIZE_HINT_QDEL;
        }
        transform = Dm.matrix().Multiply(1.5f);
        float arrival_time = Dm.num(pod.delays[POD_TRANSIT]) + Dm.num(pod.delays[POD_FALLING]);
        if (arrival_time > 0)
            animate(this, Dm.named("transform", Dm.matrix().Multiply(0.01f)), Dm.named("time", arrival_time));
        else
            alpha = 0;
        return dot;
    }
}

// tg: code/modules/cargo/supplypod.dm:698
public partial class obj_effect_pod_landingzone : obj_effect //This is the object that forceMoves the supplypod to its location
{
    // tg: code/modules/cargo/supplypod.dm:712
    [DmParams("mapload", "podParam", "single_order", "clientman")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object podParam = Dm.arg(__extra, 0);
        object single_order = Dm.arg(__extra, 1);
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(podParam))
        {
            stack_trace("Pod landingzone created with no pod", "code/modules/cargo/supplypod.dm", 715);
            return INITIALIZE_HINT_QDEL;
        }
        if (ispath(podParam)) //We can pass either a path for a pod (as expressconsoles do), or a reference to an instantiated pod (as the centcom_podlauncher does)
            podParam = Dm.@new((Type)podParam); //If it's just a path, instantiate it
        pod = podParam as obj_structure_closet_supplypod;
        pod.resistance_flags = Dm.num(Dm.bitor(pod.resistance_flags, INDESTRUCTIBLE | LAVA_PROOF | FIRE_PROOF | UNACIDABLE | ACID_PROOF | FREEZE_PROOF));
        if (!Dm.truthy(pod.effectStealth))
        {
            helper = Dm.@new<obj_effect_pod_landingzone_effect>(drop_location(), pod);
            alpha = 255;
        }
        float arrival_time = Dm.num(pod.delays[POD_TRANSIT]) + Dm.num(pod.delays[POD_FALLING]);
        if (arrival_time > 0)
            animate(this, Dm.named("transform", Dm.matrix().Turn(90)), Dm.named("time", arrival_time));
        else
            alpha = 0;
        if (Dm.truthy(single_order))
        {
            if (single_order is datum_supply_order SO)
            {
                if (SO.pack.crate_type != null)
                    Waiting.NoSleep(SO.generate(pod));
                else if (Dm.truthy(Dm.bitand(SO.pack.order_flags, ORDER_GOODY))) //Goody orders lack a crate_type and need special handling
                    SO.generateCombo(pod, SO.orderer, SO.pack.contains, SO.pack.cost);
            }
            else if (single_order is atom_movable O)
                Waiting.NoSleep(O.forceMove(pod));
        }
        foreach (mob_living mob_in_pod in Dm.each<mob_living>(pod)) //If there are any mobs in the supplypod, we want to set their view to the pod_landingzone. This is so that they can see where they are about to land
            Waiting.NoSleep(mob_in_pod.reset_perspective(this));
        if (Dm.truthy(pod.effectStun)) //If effectStun is true, stun any mobs caught on this pod_landingzone until the pod gets a chance to hit them
        {
            foreach (mob_living target_living in Dm.each<mob_living>(get_turf(this)))
                Waiting.NoSleep(target_living.Stun(Dm.num(pod.delays[POD_TRANSIT]) + 10, ignore_canstun: TRUE)); //you ain't goin nowhere, kid.
        }
        if (arrival_time < pod.fallingSoundLength)
        {
            pod.fallingSoundLength = 3; //The default falling sound is a little long, so if the landing time is shorter than the default falling sound, use a special, shorter default falling sound
            pod.fallingSound = Dm.resource("sound/items/weapons/mortar_whistle.ogg");
        }
        float soundStartTime = Dm.num(pod.delays[POD_TRANSIT]) - pod.fallingSoundLength + Dm.num(pod.delays[POD_FALLING]);
        if (soundStartTime < 0)
            soundStartTime = 1;
        _addtimer(Dm.@new<datum_callback>(this, nameof(play_falling_sound)), soundStartTime, file: "code/modules/cargo/supplypod.dm", line: 750);
        _addtimer(Dm.@new<datum_callback>(this, nameof(begin_launch), pod.effectCircle), Dm.num(pod.delays[POD_TRANSIT]), file: "code/modules/cargo/supplypod.dm", line: 751);
        return dot;
    }

    // tg: code/modules/cargo/supplypod.dm:753
    public virtual void play_falling_sound()
    {
        playsound(this, pod.fallingSound, pod.soundVolume, TRUE, 6);
    }

    // tg: code/modules/cargo/supplypod.dm:756
    public virtual async DmTask begin_launch(object effectCircle) //Begin the animation for the pod falling. The effectCircle param determines whether the pod gets to come in from any descent angle
    {
        pod.add_glow();
        pod.update_appearance();
        await pod.forceMove(drop_location() as atom);
        foreach (mob_living M in Dm.each<mob_living>(pod)) //Remember earlier (initialization) when we moved mobs into the pod_landingzone so they wouldnt get lost in nullspace? Time to get them out
            await M.reset_perspective(null);
        float angle = Dm.truthy(effectCircle) ? rand(0, 360) : rand(70, 110); //The angle that we can come in from
        pod.pixel_x = cos(angle) * 32 * Dm.length(smoke_effects); //Use some ADVANCED MATHEMATICS to set the animated pod's position to somewhere on the edge of a circle with the center being the pod_landingzone
        pod.pixel_z = sin(angle) * 32 * Dm.length(smoke_effects);
        float rotation = get_pixel_angle(pod.pixel_z, pod.pixel_x); //CUSTOM HOMEBREWED proc that is just arctan with extra steps
        setup_smoke(rotation);
        pod.transform = Dm.matrix().Turn(rotation);
        pod.layer = FLY_LAYER;
        SET_PLANE_EXPLICIT(pod, ABOVE_GAME_PLANE, this);
        if (!ispath(pod.style, typeof(datum_pod_style_invisible)))
            animate(pod, Dm.named("pixel_z", -1 * abs(sin(rotation)) * 4), Dm.named("pixel_x", SUPPLYPOD_X_OFFSET + (sin(rotation) * 20)), Dm.named("time", Dm.num(pod.delays[POD_FALLING])), Dm.named("easing", LINEAR_EASING)); //Make the pod fall! At an angle!
        _addtimer(Dm.@new<datum_callback>(this, nameof(end_launch)), Dm.num(pod.delays[POD_FALLING]), TIMER_CLIENT_TIME, file: "code/modules/cargo/supplypod.dm", line: 772); //Go onto the last step after a very short falling animation
    }

    // tg: code/modules/cargo/supplypod.dm:774
    public virtual void setup_smoke(float rotation)
    {
        if (ispath(pod.style, typeof(datum_pod_style_invisible)) || ispath(pod.style, typeof(datum_pod_style_seethrough)))
            return;
        turf our_turf = get_turf(drop_location() as atom);
        int count = (int)Dm.length(smoke_effects);
        for (int i = 1; i <= count; i++)
        {
            obj_effect_supplypod_smoke smoke_part = Dm.@new<obj_effect_supplypod_smoke>(drop_location());
            if (i == 1)
            {
                smoke_part.layer = FLY_LAYER;
                SET_PLANE(smoke_part, ABOVE_GAME_PLANE, our_turf);
                smoke_part.icon_state = "smoke_start";
            }
            smoke_part.transform = Dm.matrix().Turn(rotation);
            smoke_effects[i] = smoke_part;
            smoke_part.pixel_x = sin(rotation) * 32 * i;
            smoke_part.pixel_y = abs(cos(rotation)) * 32 * i;
            smoke_part.add_filter("smoke_blur", 1, gauss_blur_filter(size: 4) as DmList);
            float time = (Dm.num(pod.delays[POD_FALLING]) / count) * (count - i);
            _addtimer(Dm.@new<datum_callback>(smoke_part, nameof(obj_effect_supplypod_smoke.draw_self), i), time, TIMER_CLIENT_TIME, file: "code/modules/cargo/supplypod.dm", line: 790); //Go onto the last step after a very short falling animation
            float delay = Dm.num(pod.delays[POD_FALLING]) + 35;
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), delay > GC_FILTER_QUEUE ? WEAKREF(smoke_part) : smoke_part), delay, file: "code/modules/cargo/supplypod.dm", line: 791);
        }
    }

    // tg: code/modules/cargo/supplypod.dm:793
    public virtual void draw_smoke()
    {
        if (ispath(pod.style, typeof(datum_pod_style_invisible)) || ispath(pod.style, typeof(datum_pod_style_seethrough)))
            return;
        foreach (obj_effect_supplypod_smoke smoke_part in Dm.each<obj_effect_supplypod_smoke>(smoke_effects))
        {
            animate(smoke_part, Dm.named("alpha", 0), Dm.named("time", 20), Dm.named("flags", ANIMATION_PARALLEL));
            animate(smoke_part.get_filter("smoke_blur"), Dm.named("size", 6), Dm.named("time", 15), Dm.named("easing", Dm.bitor(CUBIC_EASING, EASE_OUT)), Dm.named("flags", ANIMATION_PARALLEL));
        }
    }

    // tg: code/modules/cargo/supplypod.dm:800
    public virtual async DmTask end_launch()
    {
        turf our_turf = get_turf(drop_location() as atom);
        pod.try_make_rubble(drop_location() as turf);
        pod.layer = Dm.initial(pod, (obj_structure_closet_supplypod p) => p.layer);
        SET_PLANE(pod, Dm.initial(pod, (obj_structure_closet_supplypod p) => p.plane), our_turf);
        pod.end_glow();
        QDEL_NULL(ref helper);
        await pod.pre_open(); //Begin supplypod open procedures. Here effects like explosions, damage, and other dangerous (and potentially admin-caused, if the centcom_podlauncher datum was used) memes will take place
        draw_smoke();
        qdel(this); //The pod_landingzone's purpose is complete. It can rest easy now
    }
}

//------------------------------------UPGRADES-------------------------------------//
// tg: code/modules/cargo/supplypod.dm:812
public partial class obj_item_disk_cargo_bluespace_pod : obj_item_disk_cargo //Disk that can be inserted into the Express Console to allow for Advanced Bluespace Pods
{
    // tg: code/modules/cargo/supplypod.dm:819
    public override async DmTask setup_reskins()
    {
        return;
    }
}
