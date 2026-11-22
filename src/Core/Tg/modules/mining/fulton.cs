// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/fulton.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X1b (D316): the whole file but the networked pack, core and beacon (the mining's "Candela" networks,
// not-translated section 138), whose types stay generated and inert.

public partial class datum_controller_global_vars
{
    // tg: code/modules/mining/fulton.dm:1  GLOBAL_LIST_EMPTY(total_extraction_beacons)
    public DmList total_extraction_beacons;

    // tg: code/modules/mining/fulton.dm:1  (GLOBAL_MANAGED(total_extraction_beacons, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaltotal_extraction_beacons()
    {
        total_extraction_beacons = Dm.list();
        Dm.append(ref gvars_datum_init_order, "total_extraction_beacons");
    }
}

// tg: code/modules/mining/fulton.dm:3
public partial class obj_item_extraction_pack : obj_item
{
    // tg: code/modules/mining/fulton.dm:24
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        dot.Add(span_infoplain(I18n.T("It has [] use[] remaining.", uses_left, uses_left == 1 ? "" : "s")));

        var beacon = beacon_ref?.resolve() as obj_structure_extraction_point;

        if (beacon == null)
        {
            beacon_ref = null;
            dot.Add(span_infoplain(I18n.T("It is not linked to a beacon.")));
            return dot;
        }

        dot.Add(span_infoplain(I18n.T("It is linked to [].", beacon.name)));
        return dot;
    }

    // tg: code/modules/mining/fulton.dm:37
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        DmList possible_beacons = Dm.list();
        foreach (datum_weakref point_ref in Dm.each<datum_weakref>(GLOB.total_extraction_beacons.Copy()))
        {
            var extraction_point = point_ref.resolve() as obj_structure_extraction_point;
            if (extraction_point == null)
            {
                GLOB.total_extraction_beacons.Remove(point_ref);
                continue;
            }
            if (Dm.@in(extraction_point.beacon_network, beacon_networks))
                possible_beacons.Add(extraction_point);
        }

        if (length(possible_beacons) == 0)
        {
            balloon_alert(user, I18n.T("no beacons"));
            return null;
        }

        object chosen_beacon = await tgui_input_list(user, "Beacon to connect to", "Balloon Extraction Pack", sort_names(possible_beacons) as DmList);
        if (chosen_beacon == null)
            return null;

        beacon_ref = WEAKREF(chosen_beacon as datum) as datum_weakref;
        balloon_alert(user, I18n.T("linked!"));
        return null;
    }

    // tg: code/modules/mining/fulton.dm:58
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!ismovable(interacting_with))
            return NONE;
        if (!isturf(interacting_with.loc)) // no extracting stuff inside other stuff
            return NONE;
        var thing = (atom_movable)interacting_with;
        if (Dm.truthy(thing.anchored))
            return NONE;

        atom_movable beacon = get_beacon(user);
        if (beacon == null)
            return ITEM_INTERACT_BLOCKING;
        var area = (area)get_area(thing);
        if (!Dm.truthy(can_use_indoors))
        {
            if (!Dm.truthy(area.outdoors))
            {
                balloon_alert(user, I18n.T("not outdoors!"));
                return ITEM_INTERACT_BLOCKING;
            }
        }
        if (Dm.truthy(Dm.bitand(area.area_flags, NOTELEPORT)))
        {
            balloon_alert(user, I18n.T("unable to activate!"));
            return ITEM_INTERACT_BLOCKING;
        }
        var target_area = (area)get_area(beacon);
        if (area != target_area && (Dm.truthy(Dm.bitand(area.area_flags, LOCAL_TELEPORT)) || Dm.truthy(Dm.bitand(target_area.area_flags, LOCAL_TELEPORT))))
        {
            balloon_alert(user, I18n.T("unable to activate!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (!Dm.truthy(safe_for_living_creatures) && Dm.truthy(check_for_living_mobs(thing)))
        {
            to_chat(user, span_warning(I18n.T("[] is not safe for use with living creatures, they wouldn't survive the trip back!", this)));
            balloon_alert(user, I18n.T("not safe!"));
            return ITEM_INTERACT_BLOCKING;
        }
        if (thing.move_resist > max_force_fulton)
        {
            balloon_alert(user, I18n.T("too heavy!"));
            return ITEM_INTERACT_BLOCKING;
        }
        balloon_alert_to_viewers(I18n.T("attaching..."));
        playsound(thing, "sound/items/zip/zip.ogg", vol: 50, vary: TRUE);
        if (isliving(thing))
        {
            var creature = (mob_living)thing;
            if (creature.mind != null)
                to_chat(thing, span_userdanger(I18n.T("You are being extracted! Stand still to proceed.")));
        }

        if (!Dm.truthy(await do_after(user, 5 * SECONDS, target: thing)))
            return ITEM_INTERACT_BLOCKING;

        if (QDELETED(beacon))
        {
            balloon_alert(user, I18n.T("beacon lost!"));
            return ITEM_INTERACT_BLOCKING;
        }

        balloon_alert_to_viewers(I18n.T("extracting!"));
        if (loc == user && ishuman(user))
        {
            var human_user = (mob_living_carbon_human)user;
            human_user.back?.atom_storage?.attempt_insert(this, user, force: STORAGE_SOFT_LOCKED);
        }
        uses_left--;

        if (uses_left <= 0)
            await user.transferItemToLoc(this, thing, TRUE);

        if (isliving(thing))
        {
            var creature = (mob_living)thing;
            await creature.Paralyze(paralyze_duration); // Keep them from moving during the duration of the extraction
            ADD_TRAIT(creature, TRAIT_FORCED_STANDING, FULTON_PACK_TRAIT); // Prevents animation jank from happening
            if (creature.buckled != null)
                await creature.buckled.unbuckle_mob(creature, TRUE); // Unbuckle them to prevent anchoring problems
        }
        else
        {
            await thing.set_anchored(TRUE);
            thing.set_density(FALSE);
        }

        var holder_obj = Dm.@new<obj_effect_extraction_holder>(get_turf(thing));
        holder_obj.shown_type = ismob(thing) ? thing.GetType() : null; // DEVIATION(DV377): the clients draw a lifted mob as itself
        holder_obj.appearance = thing.appearance;
        await thing.forceMove(holder_obj);
        var balloon2 = (mutable_appearance)mutable_appearance(Dm.resource("icons/effects/fulton_balloon.dmi"), "fulton_expand", layer: VEHICLE_LAYER,
            appearance_flags: Dm.bitor(Dm.bitor(Dm.bitor(RESET_COLOR, RESET_ALPHA), RESET_TRANSFORM), KEEP_APART));
        balloon2.pixel_z = 10;
        holder_obj.add_overlay(balloon2);
        _addtimer(Dm.@new<datum_callback>(this, nameof(create_balloon), thing, user, holder_obj, balloon2, get_turf(beacon)), 0.4f * SECONDS,
            file: "code/modules/mining/fulton.dm", line: 129);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/mining/fulton.dm:131
    public virtual atom_movable get_beacon(mob_living user = null)
    {
        var beacon = beacon_ref?.resolve() as obj_structure_extraction_point;
        if (beacon == null)
        {
            balloon_alert(user, I18n.T("not linked!"));
            beacon_ref = null;
        }
        return beacon;
    }

    // tg: code/modules/mining/fulton.dm:138
    public virtual async DmTask create_balloon(atom_movable thing = null, mob_living user = null, obj_effect_extraction_holder holder_obj = null, mutable_appearance balloon2 = null, turf beacon_turf = null)
    {
        const float balloon_flags = RESET_COLOR + RESET_ALPHA + RESET_TRANSFORM + KEEP_APART;
        var balloon = (mutable_appearance)mutable_appearance(Dm.resource("icons/effects/fulton_balloon.dmi"), "fulton_balloon", layer: VEHICLE_LAYER, appearance_flags: balloon_flags);
        balloon.pixel_z = 10;
        holder_obj.cut_overlay(balloon2);
        holder_obj.add_overlay(balloon);
        playsound(holder_obj.loc as atom, "sound/items/fulton/fultext_deploy.ogg", vol: 50, vary: TRUE, extrarange: -3);

        animate(holder_obj, Dm.named("pixel_z", 10), Dm.named("time", 2 * SECONDS), Dm.named("flags", ANIMATION_RELATIVE));
        animate(Dm.named("pixel_z", 5), Dm.named("time", 1 * SECONDS), Dm.named("flags", ANIMATION_RELATIVE));
        animate(Dm.named("pixel_z", -5), Dm.named("time", 1 * SECONDS), Dm.named("flags", ANIMATION_RELATIVE));
        animate(Dm.named("pixel_z", 5), Dm.named("time", 1 * SECONDS), Dm.named("flags", ANIMATION_RELATIVE));
        animate(Dm.named("pixel_z", -5), Dm.named("time", 1 * SECONDS), Dm.named("flags", ANIMATION_RELATIVE));

        await sleep(6 * SECONDS);

        playsound(holder_obj.loc as atom, "sound/items/fulton/fultext_launch.ogg", vol: 50, vary: TRUE, extrarange: -3);
        animate(holder_obj, Dm.named("pixel_z", 1000), Dm.named("time", 3 * SECONDS), Dm.named("flags", ANIMATION_RELATIVE));

        if (ishuman(thing))
        {
            var creature = (mob_living_carbon_human)thing;
            await creature.SetUnconscious(0);
            creature.remove_status_effect(typeof(datum_status_effect_drowsiness));
            await creature.SetSleeping(0);
        }

        await sleep(3 * SECONDS);

        DmList flooring_near_beacon = Dm.list();
        foreach (turf floor in Dm.each<turf>(RANGE_TURFS(1, beacon_turf)))
        {
            if (!Dm.truthy(await floor.is_blocked_turf()))
                flooring_near_beacon.Add(floor);
        }

        if (length(flooring_near_beacon) == 0)
            flooring_near_beacon.Add(beacon_turf);

        await holder_obj.forceMove(pick(flooring_near_beacon) as atom);

        animate(holder_obj, Dm.named("pixel_z", -990), Dm.named("time", 5 * SECONDS), Dm.named("flags", ANIMATION_RELATIVE));
        animate(Dm.named("pixel_z", 5), Dm.named("time", 1 * SECONDS), Dm.named("flags", ANIMATION_RELATIVE));
        animate(Dm.named("pixel_z", -5), Dm.named("time", 1 * SECONDS), Dm.named("flags", ANIMATION_RELATIVE));

        await sleep(7 * SECONDS);

        var balloon3 = (mutable_appearance)mutable_appearance(Dm.resource("icons/effects/fulton_balloon.dmi"), "fulton_retract", layer: VEHICLE_LAYER, appearance_flags: balloon_flags);
        balloon3.pixel_z = 10;
        holder_obj.cut_overlay(balloon);
        holder_obj.add_overlay(balloon3);

        await sleep(0.4f * SECONDS);

        holder_obj.cut_overlay(balloon3);
        if (isliving(thing))
            REMOVE_TRAIT(thing, TRAIT_FORCED_STANDING, FULTON_PACK_TRAIT);
        await thing.set_anchored(FALSE); // An item has to be unanchored to be extracted in the first place.
        thing.set_density(Dm.initial(thing, (atom a) => a.density));
        animate(holder_obj, Dm.named("pixel_z", -10), Dm.named("time", 0.5f * SECONDS), Dm.named("flags", ANIMATION_RELATIVE));
        await sleep(0.5f * SECONDS);
        await thing.forceMove(holder_obj.loc);
        qdel(holder_obj);
        if (uses_left <= 0)
            qdel(this);
    }

    // tg: code/modules/mining/fulton.dm:362
    public virtual object check_for_living_mobs(atom A = null)
    {
        if (isliving(A))
        {
            var L = (mob_living)A;
            if (L.stat != DEAD)
                return TRUE;
        }
        foreach (object thing in Dm.each<object>(A.get_all_contents() as DmList))
        {
            // tg checks A again here, not the content (a tg bug, kept)
            if (isliving(A))
            {
                var L = (mob_living)A;
                if (L.stat != DEAD)
                    return TRUE;
            }
        }
        return FALSE;
    }
}

// tg: code/modules/mining/fulton.dm:380
public partial class obj_item_extraction_pack_syndicate : obj_item_extraction_pack
{
}

// tg: code/modules/mining/fulton.dm:271
public partial class obj_item_fulton_core : obj_item
{
    // tg: code/modules/mining/fulton.dm:279
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        var user_area = (area)get_area(user);
        if (Dm.truthy(Dm.bitand(user_area.area_flags, NOTELEPORT)))
        {
            balloon_alert(user, I18n.T("unable to deploy!"));
            return null;
        }

        if (!Dm.truthy(await do_after(user, 1.5f * SECONDS, target: user)) || QDELETED(this))
            return null;

        Dm.@new(point_type, get_turf(user), user);
        playsound(this, "sound/items/deconstruct.ogg", vol: 50, vary: TRUE, extrarange: MEDIUM_RANGE_SOUND_EXTRARANGE);
        qdel(this);
        return null;
    }
}

// tg: code/modules/mining/fulton.dm:297
public partial class obj_item_fulton_core_syndicate : obj_item_fulton_core
{
}

// tg: code/modules/mining/fulton.dm:301
public partial class obj_structure_extraction_point : obj_structure
{
    // tg: code/modules/mining/fulton.dm:314
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        var creator = (__extra.Length > 0 ? __extra[0] : null) as mob;
        name += Dm.text(" ([]) ([])", rand(100, 999), get_area_name(this, TRUE));
        Waiting.Detach(poll_name(creator));
        GLOB.total_extraction_beacons.Add(WEAKREF(this));
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/modules/mining/fulton.dm:321
    public virtual async DmTask poll_name(mob creator = null)
    {
        object name_input = await tgui_input_text(creator, "What would you like to name the beacon?", "Fulton Beacon", @default: name, max_length: MAX_LABEL_LEN);
        if (Dm.truthy(name_input) && !QDELETED(this) && !QDELETED(creator) && Dm.truthy(await creator.Adjacent(this)))
            name = Dm.str(name_input);
    }

    // tg: code/modules/mining/fulton.dm:326
    public override object Destroy(float? force = null)
    {
        GLOB.total_extraction_beacons.Remove(WEAKREF(this));
        return base.Destroy(force);
    }

    // tg: code/modules/mining/fulton.dm:330
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        balloon_alert_to_viewers(I18n.T("undeploying..."));
        if (!Dm.truthy(await do_after(user, 1.5f * SECONDS, target: this)))
            return dot;
        Dm.@new(core_type, drop_location());
        playsound(this, "sound/items/deconstruct.ogg", vol: 50, vary: TRUE, extrarange: MEDIUM_RANGE_SOUND_EXTRARANGE);
        qdel(this);
        return dot;
    }

    // tg: code/modules/mining/fulton.dm:339
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        // DEVIATION(DV12): . += emissive_appearance(icon, "[icon_state]_light", src, alpha = src.alpha), the glow on the emissive plane, is not created
        return dot;
    }
}

// tg: code/modules/mining/fulton.dm:343
public partial class obj_structure_extraction_point_syndicate : obj_structure_extraction_point
{
}

// tg: code/modules/mining/fulton.dm:357
public partial class obj_effect_extraction_holder : obj_effect, IShownAsType
{
    /// DEVIATION(DV377): the clients draw a holder that lifts a mob as that mob (a person at a person's size, under the balloon), as the
    /// chameleon projector's dummy (DV376); null for anything else, drawn by its own type
    public Type shown_type;

    Type IShownAsType.ShownType => shown_type;

    // tg: code/modules/mining/fulton.dm:374
    public override async DmTask<object> singularity_act()
    {
        return null;
    }

    // tg: code/modules/mining/fulton.dm:377
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        return null;
    }
}
