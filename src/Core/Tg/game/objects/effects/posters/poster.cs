// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/posters/poster.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. M3 I1a translated the poster structure's init (Initialize, randomise, apply_holiday), so that a mapped
// random poster becomes a real poster with the world's RNG; syndicate G3c (D316) the rest: the rolled poster item (hanging it
// on a wall, the shard booby trap) and the structure's interactions (tearing, cutting down, the trap). The context tips are
// not ported (D264). official.dm and contraband.dm are data: their var blocks are generated (D111).

// The poster item

/**
 * The rolled up item form of a poster
 *
 * In order to create one of these for a specific poster, you must pass the structure form of the poster as an argument to /new().
 * This structure then gets moved into the contents of the item where it will stay until the poster is placed by a player.
 * The structure form is [obj/structure/sign/poster] and that's where all the specific posters are defined.
 * If you just want a random poster, see [/obj/item/poster/random_official] or [/obj/item/poster/random_contraband]
 */
// tg: code/game/objects/effects/posters/poster.dm:15
public partial class obj_item_poster : obj_item
{
    // tg: code/game/objects/effects/posters/poster.dm:2  #define PLACE_SPEED 37 (This is synced up to the poster placing animation.)
    private const float PLACE_SPEED = 37;

    // tg: code/game/objects/effects/posters/poster.dm:24
    [DmParams("mapload", "new_poster_structure")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_structure_sign_poster new_poster_structure = Dm.arg(__extra, 0) as obj_structure_sign_poster;
        object dot = base.Initialize(mapload, __extra);

        // var/static/list/hovering_item_typechecks (a shard: "Booby trap poster") and
        // AddElement(/datum/element/contextual_screentip_item_typechecks, hovering_item_typechecks): the context tips (D264)

        if (new_poster_structure != null && new_poster_structure.loc != this)
        {
            qdel(new_poster_structure.GetComponent(typeof(datum_component_atom_mounted)));
            Waiting.NoSleep(new_poster_structure.forceMove(this));
        }
        poster_structure = new_poster_structure;
        if (poster_type == null) // If we weren't already assigned a poster_type, we infer from the contained poster_structure
        {
            if (poster_structure is obj_structure_sign_poster) // Make sure our poster structure is valid
                poster_type = poster_structure.GetType();
            else
            {
                stack_trace(Dm.text("Rolled poster [] was created without either a valid poster_type [] or poster_structure []", type, poster_type, poster_structure), "code/game/objects/effects/posters/poster.dm", 42);
                poster_type = typeof(obj_structure_sign_poster_random); // Panic, do something random
            }
        }
        if (ispath(poster_type, typeof(obj_structure_sign_poster))) // Make sure we have a valid poster_type before using it
        {
            name = Dm.text("[] - []", Dm.initial(poster_type, (obj_structure_sign_poster p) => p.poster_item_name), Dm.initial(poster_type, (obj_structure_sign_poster p) => p.name));
            desc = Dm.initial(poster_type, (obj_structure_sign_poster p) => p.poster_item_desc);
            icon_state = Dm.initial(poster_type, (obj_structure_sign_poster p) => p.poster_item_icon_state);
        }
        else // We did not have a valid poster_type, light the beacons
            throw Dm.CRASH(Dm.text("Rolled poster [] has an invalid or null poster_type []", type, poster_type), "code/game/objects/effects/posters/poster.dm", 50);
        return dot;
    }

    // tg: code/game/objects/effects/posters/poster.dm:52
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref poster_structure);
        return base.Destroy(force);
    }

    // tg: code/game/objects/effects/posters/poster.dm:56
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_notice(I18n.T("You can booby-trap the poster by using a glass shard on it before you put it up.")));
        return dot;
    }

    // tg: code/game/objects/effects/posters/poster.dm:60
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (tool is not obj_item_shard)
            return NONE;

        if (Dm.locate<obj_item_shard>((object)poster_structure?.contents ?? contents) != null)
        {
            user.balloon_alert(user, I18n.T("already trapped!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            return ITEM_INTERACT_BLOCKING;

        to_chat(user, span_notice(I18n.T("You conceal \\the [] inside the rolled up poster.", tool)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/effects/posters/poster.dm:74
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        if (!isclosedturf(interacting_with))
            return NONE;
        turf wall_structure = (turf)interacting_with;

        turf user_turf = get_turf(user);
        float dir = Dm.num(get_dir(user_turf, wall_structure));
        if (!Dm.@in(dir, GLOB.cardinals))
        {
            user.balloon_alert(user, I18n.T("stand in line with wall!"));
            return ITEM_INTERACT_BLOCKING;
        }

        // Deny placing posters on currently-diagonal walls, although the wall may change in the future.
        if (((int)Dm.num(wall_structure.smoothing_flags) & SMOOTH_DIAGONAL_CORNERS) != 0)
        {
            foreach (object overlay in Dm.each(wall_structure.overlays))
            {
                if (overlay is image new_image && Dm.str(copytext(new_image.icon_state, 1, 3)) == "d-") //3 == length("d-") + 1
                {
                    to_chat(user, span_warning(I18n.T("Cannot place on diagonal wall!")));
                    return ITEM_INTERACT_FAILURE;
                }
            }
        }

        float stuff_on_wall = 0;
        foreach (obj contained_object in Dm.each<obj>(wall_structure.contents)) //Let's see if it already has a poster on it or too much stuff
        {
            if (contained_object is obj_structure_sign_poster)
            {
                user.balloon_alert(user, I18n.T("no room!"));
                return ITEM_INTERACT_FAILURE;
            }
            stuff_on_wall++;
            if (stuff_on_wall == 3)
            {
                user.balloon_alert(user, I18n.T("no room!"));
                return ITEM_INTERACT_FAILURE;
            }
        }

        user.balloon_alert(user, I18n.T("hanging poster..."));
        obj_structure_sign_poster placed_poster = poster_structure ?? Dm.@new(poster_type, this) as obj_structure_sign_poster;
        await placed_poster.forceMove(user_turf);
        await placed_poster.setDir(dir);
        switch (dir)
        {
            case NORTH:
                placed_poster.pixel_y = 32;
                break;
            case SOUTH:
                placed_poster.pixel_y = -32;
                break;
            case EAST:
                placed_poster.pixel_x = 32;
                break;
            case WEST:
                placed_poster.pixel_x = -32;
                break;
        }
        obj_item_shard trap = Dm.locate<obj_item_shard>(contents);
        if (trap != null)
            await trap.forceMove(placed_poster);
        placed_poster.poster_item_type = GetType();
        poster_structure = null;
        flick("poster_being_set", placed_poster);
        playsound(this, Dm.resource("sound/items/poster/poster_being_created.ogg"), 100, TRUE);
        qdel(this);

        turf user_drop_location = get_turf(user);
        if (!Dm.truthy(await do_after(user, PLACE_SPEED, placed_poster, extra_checks: Dm.@new<datum_callback>(placed_poster, nameof(obj_structure_sign_poster.snowflake_closed_turf_check), wall_structure))))
        {
            //only put back if the poster wasen't teared off or snipped with a wirecutter during placing
            if (!QDELETED(placed_poster))
                await placed_poster.roll_and_drop(user_drop_location, user);
            return ITEM_INTERACT_FAILURE;
        }

        await placed_poster.find_and_mount_on_atom();
        await placed_poster.on_placed_poster(user);
        return ITEM_INTERACT_SUCCESS;
    }
}

/**
 * The structure form of a poster.
 * These are what get placed on maps as posters. They are also what gets created when a player places a poster on a wall.
 * For the item form that can be spawned for players, see [/obj/item/poster]
 */

// tg: code/game/objects/effects/posters/poster.dm:139
public partial class obj_structure_sign_poster : obj_structure_sign
{
    // tg: code/game/objects/effects/posters/poster.dm:161
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        cutdown_type = type;
        if (Dm.truthy(random_basetype))
            randomise(random_basetype);
        if (!Dm.truthy(ruined))
        {
            original_name = name; // can't use initial because of random posters
            name = Dm.text("poster - []", name);
            desc = Dm.text("A large piece of space-resistant printed paper. []", desc);
        }

        Waiting.NoSleep(AddElement(typeof(datum_element_beauty), 300));
        return dot;
    }

    // tg: code/game/objects/effects/posters/poster.dm:189
    public virtual void randomise(object base_type = null)
    {
        DmList poster_types = subtypesof(base_type);
        if (Dm.truthy(length(blacklisted_types)))
        {
            foreach (object iterated_type in Dm.each(blacklisted_types))
                Dm.remove(ref poster_types, typesof(iterated_type));
        }
        DmList approved_types = Dm.list();
        foreach (Type type_of_poster in Dm.each_cast<Type>(poster_types))
        {
            // It must have an icon state, not be banned from the random pool, and not be pixel shifted (eliminates directional subtypes)
            if (Dm.truthy(Dm.initial(type_of_poster, (obj_structure_sign_poster p) => p.icon_state))
                && !Dm.truthy(Dm.initial(type_of_poster, (obj_structure_sign_poster p) => p.never_random))
                && !Dm.truthy(Dm.initial(type_of_poster, (obj_structure_sign_poster p) => p.pixel_x))
                && !Dm.truthy(Dm.initial(type_of_poster, (obj_structure_sign_poster p) => p.pixel_y)))
                Dm.combine(ref approved_types, type_of_poster);
        }

        Type selected = pick(approved_types) as Type;

        name = Dm.initial(selected, (obj_structure_sign_poster p) => p.name);
        desc = Dm.initial(selected, (obj_structure_sign_poster p) => p.desc);
        icon_state = Dm.initial(selected, (obj_structure_sign_poster p) => p.icon_state);
        icon = Dm.initial(selected, (obj_structure_sign_poster p) => p.icon);
        poster_item_name = Dm.initial(selected, (obj_structure_sign_poster p) => p.poster_item_name);
        poster_item_desc = Dm.initial(selected, (obj_structure_sign_poster p) => p.poster_item_desc);
        poster_item_icon_state = Dm.initial(selected, (obj_structure_sign_poster p) => p.poster_item_icon_state);
        ruined = Dm.initial(selected, (obj_structure_sign_poster p) => p.ruined);
        cutdown_type = Dm.initial(selected, (obj_structure_sign_poster p) => p.type);
        if (Dm.length(GLOB.holidays) > 0 && prob(30)) // its the holidays! lets get festive
            apply_holiday();
        update_appearance();
    }

    /// allows for posters to become festive posters during holidays
    // tg: code/game/objects/effects/posters/poster.dm:216
    public virtual void apply_holiday()
    {
        if (Dm.length(GLOB.holidays) == 0)
            return;
        object active_holiday = pick(GLOB.holidays);
        var holi_data = (datum_holiday)GLOB.holidays[active_holiday];
        if (holi_data.poster_name == "generic celebration poster")
            return;
        name = holi_data.poster_name;
        desc = holi_data.poster_desc;
        icon_state = holi_data.poster_icon;
    }

    // tg: code/game/objects/effects/posters/poster.dm:174  add_context: the context tips (D264)

    // tg: code/game/objects/effects/posters/poster.dm:228
    public override async DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        tool.play_tool_sound(this, 100);
        if (Dm.truthy(ruined))
        {
            to_chat(user, span_notice(I18n.T("You remove the remnants of the poster.")));
            qdel(this);
        }
        else
        {
            to_chat(user, span_notice(I18n.T("You carefully remove the poster from the wall.")));
            await roll_and_drop(Dm.truthy(await Adjacent(user)) ? get_turf(user) : loc as atom, user);
        }
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/effects/posters/poster.dm:238
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot) || !Dm.truthy(check_tearability()))
            return dot;
        await tear_poster(user);
        return dot;
    }

    /// Check to see if this poster is tearable and gives the user feedback if it is not.
    // tg: code/game/objects/effects/posters/poster.dm:245
    public virtual object check_tearability(mob user = null)
    {
        if (Dm.truthy(ruined))
        {
            // tg calls this without the user (attack_hand above), so the balloon goes to no one
            user?.balloon_alert(user, I18n.T("already ruined!"));
            return FALSE;
        }
        return TRUE;
    }

    // HO-HO-HOHOHO HU HU-HU HU-HU
    // tg: code/game/objects/effects/posters/poster.dm:252
    public virtual async DmTask spring_trap(mob user = null)
    {
        obj_item_shard payload = Dm.locate<obj_item_shard>(contents);
        if (payload == null)
            return;

        to_chat(user, span_warning(I18n.T("There's something sharp behind this! What the hell?")));
        if (!Dm.truthy(can_embed_trap(user as mob_living_carbon_human)) || !Dm.truthy(await payload.force_embed(user as mob_living_carbon, user.get_active_hand())))
        {
            visible_message(span_notice(I18n.T("A [] falls from behind the poster.", payload.name)));
            await payload.forceMove(user.drop_location() as atom);
        }
    }

    // tg: code/game/objects/effects/posters/poster.dm:262
    public virtual object can_embed_trap(mob_living_carbon_human user = null)
    {
        if (user == null || HAS_TRAIT(user, TRAIT_PIERCEIMMUNE))
            return FALSE;
        return user.gloves == null || ((int)Dm.num(user.gloves.body_parts_covered) & HANDS) == 0 || HAS_TRAIT(user, TRAIT_FINGERPRINT_PASSTHROUGH) || HAS_TRAIT(user.gloves, TRAIT_FINGERPRINT_PASSTHROUGH) ? TRUE : FALSE;
    }

    // tg: code/game/objects/effects/posters/poster.dm:267
    public virtual async DmTask<object> roll_and_drop(atom location = null, mob user = null)
    {
        pixel_x = 0;
        pixel_y = 0;
        obj_item_poster rolled_poster = await return_to_poster_item(location) as obj_item_poster;
        if (!(user != null && Dm.truthy(await user.put_in_hands(rolled_poster))))
            await forceMove(rolled_poster);
        return rolled_poster;
    }

    /// Re-creates the poster item from the poster structure
    // tg: code/game/objects/effects/posters/poster.dm:277
    public virtual DmTask<object> return_to_poster_item(atom location = null)
    {
        object dot = Dm.@new(poster_item_type, location, Dm.@new(cutdown_type as Type));
        qdel(this);
        return DmTask<object>.FromResult(dot);
    }

    //since turfs never get deleted but instead change type, make sure we're still being placed on a wall.
    // tg: code/game/objects/effects/posters/poster.dm:282
    public virtual object snowflake_closed_turf_check(atom hopefully_still_a_closed_turf = null)
        => isclosedturf(hopefully_still_a_closed_turf) ? TRUE : FALSE;

    // tg: code/game/objects/effects/posters/poster.dm:285
    public virtual DmTask on_placed_poster(mob user = null)
    {
        to_chat(user, span_notice(I18n.T("You place the poster!")));
        return DmTask.CompletedTask;
    }

    // tg: code/game/objects/effects/posters/poster.dm:288
    public virtual async DmTask tear_poster(mob user = null)
    {
        visible_message(span_notice(I18n.T("[] rips [] in a single, decisive motion!", user, this)));
        playsound(loc, Dm.resource("sound/items/poster/poster_ripped.ogg"), 100, TRUE);
        await spring_trap(user);

        obj_structure_sign_poster_ripped torn_poster = Dm.@new<obj_structure_sign_poster_ripped>(loc);
        torn_poster.pixel_y = pixel_y;
        torn_poster.pixel_x = pixel_x;
        torn_poster.add_fingerprint(user);
        qdel(this);
    }
}

// The whole of code/game/objects/effects/poster_demotivational.dm (syndicate G3c): the seditious posters. The demoraliser they
// carry is live, and demoralises its viewers since the moods came (gaps MS1).

// tg: code/game/objects/effects/poster_demotivational.dm:7
public partial class obj_structure_sign_poster_traitor : obj_structure_sign_poster
{
    // tg: code/game/objects/effects/poster_demotivational.dm:15
    public override void apply_holiday()
    {
        Type holi_data = typeof(obj_structure_sign_poster_traitor_festive);
        name = Dm.initial(holi_data, (obj_structure_sign_poster_traitor p) => p.name);
        desc = Dm.initial(holi_data, (obj_structure_sign_poster_traitor p) => p.desc);
        icon_state = Dm.initial(holi_data, (obj_structure_sign_poster_traitor p) => p.icon_state);
    }

    // tg: code/game/objects/effects/poster_demotivational.dm:21
    public override async DmTask on_placed_poster(mob user = null)
    {
        datum_demoralise_moods_poster mood_category = Dm.@new<datum_demoralise_moods_poster>();
        demoraliser = Dm.@new<datum_proximity_monitor_advanced_demoraliser>(this, 7, TRUE, mood_category);
        await base.on_placed_poster(user);
    }

    // tg: code/game/objects/effects/poster_demotivational.dm:26
    public override DmTask<object> wirecutter_act(mob_living user = null, obj_item tool = null)
    {
        QDEL_NULL(ref demoraliser);
        return base.wirecutter_act(user, tool);
    }

    // tg: code/game/objects/effects/poster_demotivational.dm:30
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref demoraliser);
        return base.Destroy(force);
    }
}
