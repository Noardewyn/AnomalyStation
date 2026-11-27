// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/hud/screen_objects/screen_objects.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the base screen object, the walk/run button, the health indicator and the health dolls (with their limb atoms)
// (M2 P3, D107); the swap, inventory and hand slots, close, storage_up, drop, the combat toggle, resist, storage,
// throw_catch and the zone selector (M4a P2, D126, D131). The other screen objects of the file (navigation, the area
// creator, languages, pull, rest, sleep, the space suit, the mood, hunger and stamina displays, ...) come with
// their milestones (M4b-M9); gaps S1 (D291): the hunger display and its bar; the slime console's monkey, slime and potion
// display.

/*
	Screen objects
	Todo: improve/re-implement

	Screen objects are only used for the hud and should not appear anywhere "in-game".
	They are used with the client/screen list and the screen_loc var.
	For more information, see the byond documentation on the screen_loc and screen vars.
*/
// tg: code/_onclick/hud/screen_objects/screen_objects.dm:10 (the [DmPath] is on _onclick/click.cs, click.dm:453)
public partial class atom_movable_screen : atom_movable
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:11-19  overrides of builtin and inherited vars (rules 10.2)
    public atom_movable_screen()
    {
        name = "";
        icon = Dm.resource("icons/hud/screen_gen.dmi");
        // NOTE: screen objects do NOT change their plane to match the z layer of their owner
        // You shouldn't need this, but if you ever do and it's widespread, reconsider what you're doing.
        plane = HUD_PLANE;
        animate_movement = SLIDE_STEPS;
        speech_span = SPAN_ROBOT;
        appearance_flags = APPEARANCE_UI;
        interaction_flags_atom = Dm.bitor(Dm.initial(typeof(atom_movable), (atom_movable p) => p.interaction_flags_atom), INTERACT_ATOM_MOUSEDROP_IGNORE_CHECKS);
    }

    /// A reference to the object in the slot. Grabs or items, generally, but any datum will do.
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:21
    public datum_weakref master_ref = null;
    /// A reference to the owner HUD, if any.
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:23  VAR_PRIVATE/datum/hud/hud
    [DmPrivate]
    public datum_hud hud = null;
    /**
     * Map name assigned to this object.
     * Automatically set by /client/proc/add_obj_to_map.
     */
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:28
    public object assigned_map;
    /**
     * Mark this object as garbage-collectible after you clean the map
     * it was registered on.
     *
     * This could probably be changed to be a proc, for conditional removal.
     * But for now, this works.
     */
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:36
    public float del_on_map_removal = TRUE;

    /// A key for cleaning up references in hud datums
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:39
    public object hud_group_key;
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:40
    public object hud_key;

    /// If FALSE, this will not be cleared when calling /client/clear_screen()
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:43
    public float clear_with_screen = TRUE;
    /// If TRUE, clicking the screen element will fall through and perform a default "Click" call
    /// Obviously this requires your Click override, if any, to call parent on their own.
    /// This is set to FALSE to default to dissade you from doing this.
    /// Generally we don't want default Click stuff, which results in bugs like using Telekinesis on a screen element
    /// or trying to point your gun at your screen.
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:49
    public float default_click = FALSE;
}

// tg: code/_onclick/hud/screen_objects/screen_objects.dm:1
public partial class atom_movable_screen : atom_movable
{
    // INITIALIZE_IMMEDIATE(/atom/movable/screen) // I hate this place (screen_objects.dm:1; __DEFINES/subsystems.dm:96,
    // expanded inline, rules 11.1 (b), (d)): the type and its subtypes always call Initialize in New
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:1
    [DmParams("loc")]
    public override async DmTask New(params object[] __extra)
    {
        object loc = Dm.arg(__extra, 0);
        await base.New(Dm.forward(__extra, loc));
        DmList args = Dm.args(Dm.forward(__extra, loc));
        if (!Dm.truthy(Dm.bitand(flags_1, INITIALIZED_1)))
        {
            float previous_initialized_value = SSatoms.initialized;
            SSatoms.initialized = INITIALIZATION_INNEW_MAPLOAD;
            args[1] = TRUE;
            SSatoms.InitAtom(this, FALSE, args);
            SSatoms.initialized = previous_initialized_value;
        }
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:51
    [DmParams("mapload", "hud_owner")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        datum_hud hud_owner = Dm.arg<datum_hud>(__extra, 0);
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        if (hud_owner == null) //some screens set their hud owners on /new, this prevents overriding them with null post atoms init
            return dot;
        Waiting.NoSleep(set_new_hud(hud_owner));
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:57
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(hud))
        {
            if (Dm.truthy(hud_group_key) && Dm.truthy(hud.screen_groups?[hud_group_key]))
                Dm.remove_at(hud.screen_groups, hud_group_key, this);
            Dm.remove(ref hud.screen_objects, hud_key);
        }
        master_ref = null;
        hud = null;
        return base.Destroy(force);
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:66
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (Dm.truthy(Dm.bitand(flags_1, INITIALIZED_1)))
        {
            if (Dm.truthy(this._listen_lookup?[COMSIG_SCREEN_ELEMENT_CLICK]))
                this._SendSignal(COMSIG_SCREEN_ELEMENT_CLICK, Dm.list(this, location, control, @params, usr));
        }

        if (Dm.truthy(default_click))
            return await base.Click(location, control, @params);
        return null;
    }

    ///Screen elements are always on top of the players screen and don't move so yes they are adjacent
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:74
    public override async DmTask<object> Adjacent(atom neighbor = null, atom target = null, atom_movable mover = null, params object[] __extra)
    {
        return TRUE;
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:77
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        return Dm.list();
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:83
    public virtual async DmTask component_click(atom_movable_screen_component_button component = null, object @params = null)
    {
        return;
    }

    ///setter used to set our new hud
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:87
    public virtual async DmTask set_new_hud(datum_hud hud_owner = null)
    {
        if (Dm.truthy(hud))
            UnregisterSignal(hud, COMSIG_QDELETING);
        if (hud_owner == null)
        {
            hud = null;
            return;
        }
        hud = hud_owner;
        RegisterSignal(hud, COMSIG_QDELETING, nameof(on_hud_delete));
    }

    /// Returns the mob this is being displayed to, if any
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:97
    public virtual object get_mob()
    {
        return hud?.mymob;
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:100
    [SignalHandler]
    public virtual void on_hud_delete(datum source = null)
    {
        Waiting.NoSleep(set_new_hud(hud_owner: null));
    }
}

// tg: code/_onclick/hud/screen_objects/screen_objects.dm:522
[DmPath("/atom/movable/screen/mov_intent")]
public partial class atom_movable_screen_mov_intent : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:523-527
    public atom_movable_screen_mov_intent()
    {
        name = "run/walk toggle";
        icon = Dm.resource("icons/hud/screen_midnight.dmi");
        icon_state = "running";
        // tg: code/_onclick/hud/screen_objects/screen_objects.dm:526
        mouse_over_pointer = MOUSE_HAND_POINTER;
        screen_loc = ui_movi;
    }
}

// tg: code/_onclick/hud/screen_objects/screen_objects.dm:529
public partial class atom_movable_screen_mov_intent : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:529
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        update_appearance(UPDATE_ICON_STATE);
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:533
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        await toggle(usr as mob_living);
        return null;
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:536
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (!Dm.truthy(hud) || !Dm.truthy(hud.mymob) || !isliving(hud.mymob))
            return null;
        mob_living living_hud_owner = hud.mymob as mob_living;
        string sw__1 = living_hud_owner.move_intent;
        if (sw__1 == MOVE_INTENT_WALK)
            icon_state = "walking";
        else if (sw__1 == MOVE_INTENT_RUN)
            icon_state = "running";
        return await base.update_icon_state(__extra);
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:547
    public virtual async DmTask toggle(mob_living user = null)
    {
        if (!(user is mob_living))
            return;
        // user.toggle_move_intent() (:550): GAP(P1, modules/mob/mob_movement.cs): /mob/proc/toggle_move_intent is not
        // declared in this branch (the walk/run keybinding reaches tg's toggle through ClientSeam.ToggleMoveIntent).
        Dm.NotTranslated("code/_onclick/hud/screen_objects/screen_objects.dm:550", "toggle: /mob/proc/toggle_move_intent (P1's mob_movement)");
    }
}

// tg: code/_onclick/hud/screen_objects/screen_objects.dm:868
[DmPath("/atom/movable/screen/healths")]
public partial class atom_movable_screen_healths : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:869-871
    public atom_movable_screen_healths()
    {
        name = "health";
        icon_state = "health0";
        screen_loc = ui_health;
    }
}

// tg: code/_onclick/hud/screen_objects/screen_objects.dm:905
[DmPath("/atom/movable/screen/healthdoll")]
public partial class atom_movable_screen_healthdoll : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:906-907
    public atom_movable_screen_healthdoll()
    {
        name = "health doll";
        screen_loc = ui_healthdoll;
    }
}

// tg: code/_onclick/hud/screen_objects/screen_objects.dm:909
public partial class atom_movable_screen_healthdoll : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:909
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (iscarbon(usr))
        {
            mob_living_carbon C = (mob_living_carbon)usr;
            C.check_self_for_injuries();
        }
        return null;
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:914
    public virtual void update_body_zones()
    {
        return;
    }
}

// tg: code/_onclick/hud/screen_objects/screen_objects.dm:917
[DmPath("/atom/movable/screen/healthdoll/living")]
public partial class atom_movable_screen_healthdoll_living : atom_movable_screen_healthdoll
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:918-919
    public atom_movable_screen_healthdoll_living()
    {
        icon_state = "fullhealth0";
        screen_loc = ui_living_healthdoll;
    }

    ///The image we create on New to use for showing mob's HP on hover. It uses a separate image as to not be cut off by `alpha_mask_filter`.
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:921
    public image health_overlay;
    ///Boolean on whether a mouse is being hovered over us right now.
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:923 (float?: the null-use grep matches hovering = null at
    // screen_objects.dm:784, another type's var of the same name)
    public float? hovering = FALSE;
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:924
    public float filtered = FALSE; //so we don't repeatedly create the mask of the mob every update

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:926
    [DmParams("loc")]
    public override async DmTask New(params object[] __extra)
    {
        object loc = Dm.arg(__extra, 0);
        await base.New(Dm.forward(__extra, loc));
        health_overlay = image(loc: this, layer: this.layer + 0.1f);
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:930
    public override object Destroy(float? force = null)
    {
        health_overlay = null; // QDEL_NULL(health_overlay): an /image is a value with no Destroy (rules 3.7), so qdel does nothing to it
        return base.Destroy(force);
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:934
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);
        if (Dm.truthy(hovering))
            Dm.combine(ref dot, health_overlay);
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:939  (M4c slice 3: the host sends BYOND's hover exits, protocol 14)
    public override async DmTask MouseEntered(object location = null, object control = null, object @params = null)
    {
        if (usr != get_mob())
            return;
        await base.MouseEntered(location, control, @params);
        hovering = TRUE;
        update_appearance(UPDATE_ICON);
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:946
    public override void MouseExited(object location = null, object control = null, object @params = null)
    {
        if (usr != get_mob())
            return;
        base.MouseExited(location, control, @params);
        hovering = FALSE;
        update_appearance(UPDATE_ICON);
    }
}

// tg: code/_onclick/hud/screen_objects/screen_objects.dm:953
[DmPath("/atom/movable/screen/healthdoll/human")]
public partial class atom_movable_screen_healthdoll_human : atom_movable_screen_healthdoll
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:954
    public atom_movable_screen_healthdoll_human()
    {
        mouse_over_pointer = MOUSE_HAND_POINTER;
    }

    /// Tracks components of our doll, each limb is a separate atom in our vis_contents
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:956  VAR_PRIVATE/list/atom/movable/screen/limbs
    [DmPrivate]
    public DmList limbs;
    /// Lazylist, tracks all body zones that are wounded currently
    /// Used so we can sync animations should the list be updated
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:959  VAR_PRIVATE/list/animated_zones
    [DmPrivate]
    public DmList animated_zones;

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:961
    [DmParams("mapload", "hud_owner")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        datum_hud hud_owner = Dm.arg<datum_hud>(__extra, 0);
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        if (hud_owner == null) //we require a hud owner to work properly, so return out.
            return dot;
        update_body_zones();
        update_appearance();
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:968
    public override void update_body_zones()
    {
        vis_contents.Cut();
        QDEL_LIST_ASSOC_VAL(limbs);
        if (!Dm.truthy(limbs))
            limbs = Dm.list();
        mob_living_carbon_human owner = hud.mymob as mob_living_carbon_human;
        foreach (object body_zone in Dm.each(owner.get_all_limbs()))
        {
            atom_movable_screen_healthdoll_limb limb = Dm.@new<atom_movable_screen_healthdoll_limb>(this, null);
            // layer chest above other limbs, it's the center after all
            limb.layer = Dm.eq(body_zone, BODY_ZONE_CHEST) ? layer + 0.05f : layer;
            limbs[body_zone] = limb;
            // why viscontents? why not overlays? - because i want to animate filters
            Dm.append(vis_contents, limb);
        }
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:981
    public override object Destroy(float? force = null)
    {
        QDEL_LIST_ASSOC_VAL(limbs);
        vis_contents.Cut();
        return base.Destroy(force);
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:986
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_icon_state(__extra);
        mob_living_carbon_human owner = hud?.mymob as mob_living_carbon_human;
        if (owner == null)
            return dot;
        if (owner.stat == DEAD)
        {
            foreach (object limb in Dm.each(limbs))
                ((atom)limbs[limb]).icon_state = Dm.text("[]DEAD", limb);
            return dot;
        }

        DmList current_animated = LAZYLISTDUPLICATE(animated_zones);

        foreach ((object part_zone, object body_part_untyped) in Dm.pairs(owner.get_bodyparts_by_zones()))
        {
            // I hate that we "allow" support for more than 2 hands in the codebase
            if (!Dm.truthy(limbs[part_zone]))
                continue;
            object icon_key = 0;
            obj_item_bodypart body_part = body_part_untyped as obj_item_bodypart;
            DmList overridable_key = Dm.list(icon_key);
            if (body_part == null || Dm.truthy(Dm.bitand(body_part.bodypart_flags, BODYPART_STUMP))) // IS_STUMP (__DEFINES/surgery.dm:67), inline
                icon_key = 6;
            else if (Dm.truthy(body_part.bodypart_disabled))
                icon_key = 7;
            else if (owner.stat == DEAD)
                icon_key = "DEAD";
            else if (Dm.truthy(Dm.bitand((!Dm.truthy(body_part._listen_lookup?[COMSIG_BODYPART_UPDATING_HEALTH_HUD]) ? NONE : body_part._SendSignal(COMSIG_BODYPART_UPDATING_HEALTH_HUD, Dm.list(body_part, owner, overridable_key))), OVERRIDE_BODYPART_HEALTH_HUD)))
                icon_key = overridable_key[1]; // thanks i hate it
            // else if(!owner.has_status_effect(/datum/status_effect/grouped/screwy_hud/fake_healthy)): GAP(P1,
            // datums/status_effects): /datum/status_effect/grouped/screwy_hud/fake_healthy is not declared (the screwy HUD of
            // hallucinations and drugs, M8), so no human has it.
            else
            {
                float damage = Dm.num(Dm.div(body_part.get_damage(), body_part.max_damage));
                // calculate what icon state (1-5, or 0 if undamaged) to use based on damage
                icon_key = clamp(ceil(damage * 5), 0, 5);
            }

            if (Dm.truthy(length(body_part?.wounds)))
                LAZYSET(ref animated_zones, part_zone, TRUE);
            else
                LAZYREMOVE(ref animated_zones, part_zone);
            ((atom)limbs[part_zone]).icon_state = Dm.text("[][]", part_zone, icon_key);
        }
        // time to re-sync animations, something changed
        if (!Dm.equiv(animated_zones, current_animated))
        {
            foreach (object animated_zone in Dm.each(animated_zones))
            {
                // M6 slice 7 (D155): the outline reaches the client. Its "alpha" is no parameter of BYOND's outline (OpenDream's
                // DreamFilterOutline: size, color, flags), so the client draws the outline in its colour and the pulse changes nothing
                // drawn (D155 records it).
                var wounded_zone = limbs[animated_zone] as atom;
                object existing_filter = wounded_zone.get_filter("wound_outline");
                if (Dm.truthy(existing_filter))
                    Dm.animate(existing_filter); // stop animation so we can resync
                else
                {
                    wounded_zone.add_filter("wound_outline", 1, Dm.list(Dm.kv("type", "outline"), Dm.kv("color", "#FF0033"), Dm.kv("alpha", 0), Dm.kv("size", 1.2f)));
                    existing_filter = wounded_zone.get_filter("wound_outline");
                }
                Dm.animate(existing_filter, Dm.named("alpha", 200), Dm.named("time", 1.5f * SECONDS), Dm.named("loop", -1));
                Dm.animate(Dm.named("alpha", 0), Dm.named("time", 1.5f * SECONDS));
            }
            if (Dm.truthy(LAZYLEN(current_animated))) // avoid null - list() runtimes please
            {
                foreach (object lost_zone in Dm.each(Dm.sub(current_animated, animated_zones)))
                    ((datum)limbs[lost_zone]).remove_filter("wound_outline");
            }
        }
        return dot;
    }
}

// Basically just holds an icon we can put a filter on
// tg: code/_onclick/hud/screen_objects/screen_objects.dm:1040
[DmPath("/atom/movable/screen/healthdoll_limb")]
public partial class atom_movable_screen_healthdoll_limb : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:1041-1042
    public atom_movable_screen_healthdoll_limb()
    {
        screen_loc = ui_living_healthdoll;
        vis_flags = Dm.bitor(VIS_INHERIT_ID, VIS_INHERIT_PLANE);
    }
}

// tg: code/_onclick/hud/screen_objects/screen_objects.dm:1097
[DmPath("/atom/movable/screen/component_button")]
public partial class atom_movable_screen_component_button : atom_movable_screen
{
}

// M4a contracts: the roots of this file's procs that no part declared yet, with the signatures of
// docs/ai/specs/generated/slots.tsv (DmTask exactly where suspends = yes), Dm.NotTranslated until M4a P2 translates
// them or leaves them for their milestone (docs/ai/specs/m4a-ownership.md). Overrides are not declared, so the inherited
// code keeps running until then.

// tg: code/_onclick/hud/screen_objects/screen_objects.dm:114
public partial class atom_movable_screen_swap_hand : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:118
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        // At this point in client Click() code we have passed the 1/10 sec check and little else
        // We don't even know if it's a middle click
        if (world.time <= Dm.num(usr.next_move))
            return 1;

        if (Dm.truthy(usr.incapacitated))
            return 1;

        if (ismob(usr))
        {
            mob M = usr;
            M.swap_hand();
        }
        return 1;
    }
}

// tg: code/_onclick/hud/screen_objects/screen_objects.dm:167  (gaps L2: the language menu button; the var block is generated)
public partial class atom_movable_screen_language_menu : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:174
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        await ((atom_movable)usr).get_language_holder().open_language_menu(usr);
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/screen_objects.dm:177  (gaps package G4: the memories button opens the memory panel; the var block is
// generated)
public partial class atom_movable_screen_memories : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:184
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (!isliving(usr))
            return TRUE;
        mob_living daydreamer = usr as mob_living;
        await daydreamer.open_memory_panel();
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/screen_objects.dm:190
public partial class atom_movable_screen_inventory : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:200
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        // At this point in client Click() code we have passed the 1/10 sec check and little else
        // We don't even know if it's a middle click
        if (world.time <= Dm.num(usr.next_move))
            return TRUE;

        if (Dm.truthy(INCAPACITATED_IGNORING(usr, INCAPABLE_STASIS)))
            return TRUE;
        if (istype(usr.loc, typeof(obj_vehicle_sealed_mecha))) // stops inventory actions in a mech (ismecha, inline)
            return TRUE;

        if (Dm.truthy(hud?.mymob) && Dm.truthy(slot_id))
        {
            obj_item inv_item = hud.mymob.get_item_by_slot(slot_id) as obj_item;
            if (Dm.truthy(inv_item))
                return await inv_item.Click(location, control, @params);
        }

        if (Dm.truthy(await usr.attack_ui(slot_id, @params)))
            await usr.update_held_items();
        return TRUE;
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:219
    public override async DmTask MouseEntered(object location = null, object control = null, object @params = null)
    {
        await base.MouseEntered(location, control, @params);
        if (usr == hud?.mymob)
            await add_overlays();
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:224
    // BYOND's builtin /atom/MouseExited (slots.tsv: builtin) is declared on the atom's engine part (M4c slice 2); the client
    // seam calls it when the pointer leaves the object (ClientSeam.Hover and Unhover, protocol version 14).
    public override void MouseExited(object location = null, object control = null, object @params = null)
    {
        if (usr != hud?.mymob)
            return;
        cut_overlay(object_overlay);
        object_overlay = null; // QDEL_NULL(object_overlay): an /image is a value with no Destroy (rules 3.7)
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:231
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (!Dm.truthy(icon_empty))
            icon_empty = icon_state;

        if (Dm.truthy(hud?.mymob) && Dm.truthy(slot_id) && Dm.truthy(icon_full))
            icon_state = Dm.str(Dm.truthy(hud.mymob.get_item_by_slot(slot_id)) ? icon_full : icon_empty);
        return await base.update_icon_state(__extra);
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:240
    public virtual async DmTask add_overlays()
    {
        mob user = hud?.mymob;

        if (!Dm.truthy(user) || !Dm.truthy(slot_id))
            return;

        obj_item holding = user.get_active_held_item() as obj_item;

        if (!Dm.truthy(holding) || Dm.truthy(user.get_item_by_slot(slot_id)))
            return;

        image item_overlay = image(holding);
        item_overlay.alpha = 92;

        if (!Dm.truthy(await holding.mob_can_equip(user as mob_living, slot_id, disable_warning: TRUE, bypass_equip_delay_self: TRUE)))
            item_overlay.color = COLOR_RED;
        else
            item_overlay.color = "#00ff00";

        cut_overlay(object_overlay);
        object_overlay = item_overlay;
        add_overlay(object_overlay);
    }
}

// tg: code/_onclick/hud/screen_objects/screen_objects.dm:262
public partial class atom_movable_screen_inventory_hand : atom_movable_screen_inventory
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:265  var/static/mutable_appearance/blocked_overlay =
    // mutable_appearance('icons/hud/screen_gen.dmi', "blocked") (rules 10.6: a TgState field, at the end of this file; made
    // on first use, as the static's initializer runs before any reader)
    public static mutable_appearance blocked_overlay => TgState.Current.atom_movable_screen_inventory_hand__blocked_overlay ??= (mutable_appearance)mutable_appearance(Dm.resource("icons/hud/screen_gen.dmi"), "blocked");

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:269
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);

        if (!Dm.truthy(handcuff_overlay))
        {
            string state = IS_RIGHT_INDEX(held_index) ? "markus" : "gabrielle";
            handcuff_overlay = (mutable_appearance)mutable_appearance(Dm.resource("icons/hud/screen_gen.dmi"), state);
        }

        if (!Dm.truthy(hud?.mymob))
            return dot;

        if (iscarbon(hud.mymob))
        {
            mob_living_carbon C = hud.mymob as mob_living_carbon;
            if (Dm.truthy(C.handcuffed))
                Dm.combine(ref dot, handcuff_overlay);

            if (Dm.truthy(held_index))
            {
                if (!Dm.truthy(C.has_hand_for_held_index(held_index)))
                    Dm.combine(ref dot, blocked_overlay);
            }
        }

        if (held_index == Dm.num(hud.mymob.active_hand_index))
            Dm.combine(ref dot, IS_LEFT_INDEX(held_index) ? "lhandactive" : "rhandactive");
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:291
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        // At this point in client Click() code we have passed the 1/10 sec check and little else
        // We don't even know if it's a middle click
        mob user = hud?.mymob;
        if (usr != user)
            return TRUE;
        if (world.time <= Dm.num(user.next_move))
            return TRUE;
        if (Dm.truthy(user.incapacitated))
            return TRUE;
        if (istype(user.loc, typeof(obj_vehicle_sealed_mecha))) // stops inventory actions in a mech (ismecha, inline)
            return TRUE;

        if (Dm.eq(user.active_hand_index, held_index))
        {
            obj_item I = user.get_active_held_item() as obj_item;
            if (Dm.truthy(I))
                await I.Click(location, control, @params);
        }
        else
            user.swap_hand(held_index);
        return TRUE;
    }
}

// tg: code/_onclick/hud/screen_objects/screen_objects.dm:312
public partial class atom_movable_screen_close : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:320
    [DmParams("mapload", "hud_owner", "new_master")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object new_master = Dm.arg(__extra, 1);
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        master_ref = WEAKREF(new_master as datum) as datum_weakref;
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:324
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        datum_storage storage = master_ref?.resolve() as datum_storage;
        if (!Dm.truthy(storage))
            return null;
        storage.hide_contents(usr);
        return TRUE;
    }
}

// tg: code/_onclick/hud/screen_objects/screen_objects.dm:331
public partial class atom_movable_screen_storage_up : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:339
    [DmParams("mapload", "hud_owner", "new_master")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object new_master = Dm.arg(__extra, 1);
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        master_ref = WEAKREF(new_master as datum) as datum_weakref;
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:343
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        datum_storage storage = master_ref?.resolve() as datum_storage;
        if (!Dm.truthy(storage))
            return null;
        datum_storage up_storage = (storage.parent?.loc as atom)?.atom_storage;
        if (!Dm.truthy(up_storage))
            return null;
        await up_storage.open_storage(usr);
        return TRUE;
    }
}

// tg: code/_onclick/hud/screen_objects/screen_objects.dm:353
public partial class atom_movable_screen_drop : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:359
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (!IS_UNCONSCIOUS_OR_CRIT(usr))
            await usr.dropItemToGround(usr.get_active_held_item() as obj_item);
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/screen_objects.dm:363
public partial class atom_movable_screen_combattoggle : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:372
    [DmParams("mapload", "hud_owner", "default_screen_location")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object default_screen_location = Dm.arg(__extra, 1);
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        update_appearance();
        if (Dm.truthy(default_screen_location))
            screen_loc = Dm.str(default_screen_location);
        this.default_screen_location = screen_loc;
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:379
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (isliving(usr))
        {
            mob_living owner = usr as mob_living;
            owner.set_combat_mode(Dm.num(!Dm.truthy(owner.combat_mode)), FALSE);
            update_appearance();
        }
        return null;
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:385
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        mob_living user = hud?.mymob as mob_living;
        if (!(user is mob_living) || !Dm.truthy(user.client))
            return await base.update_icon_state(__extra);
        icon_state = Dm.truthy(user.combat_mode) ? "combat" : "combat_off"; //Treats the combat_mode
        return await base.update_icon_state(__extra);
    }
}

//Version of the combat toggle with the flashy overlay
// tg: code/_onclick/hud/screen_objects/screen_objects.dm:393
public partial class atom_movable_screen_combattoggle_flashy : atom_movable_screen_combattoggle
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:397
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);
        mob_living user = hud?.mymob as mob_living;
        if (!(user is mob_living) || !Dm.truthy(user.client))
            return dot;

        if (!Dm.truthy(user.combat_mode))
            return dot;

        if (!Dm.truthy(flashy))
        {
            flashy = (mutable_appearance)mutable_appearance(Dm.resource("icons/hud/screen_gen.dmi"), "togglefull_flash");
            flashy.color = "#C62727";
        }
        Dm.combine(ref dot, flashy);
        return dot;
    }
}

// tg: code/_onclick/hud/screen_objects/screen_objects.dm:461  (M4c slice 2: the space suit's cell and thermal regulator
// display, the whole type)
public partial class atom_movable_screen_spacesuit : atom_movable_screen
{
    ///The overlay added on top of the HUD when the thermal regulator is off.
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:466  var/static/mutable_appearance/off_overlay =
    // mutable_appearance('icons/hud/screen_gen.dmi', "off") (rules 10.6: a TgState field, made on first use)
    public static mutable_appearance off_overlay => TgState.Current.atom_movable_screen_spacesuit__off_overlay ??= (mutable_appearance)mutable_appearance(Dm.resource("icons/hud/screen_gen.dmi"), "off");

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:472
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        object dot = null;
        if (usr != get_mob())
            return dot;
        dot = await base.Click(location, control, @params);
        mob_living_carbon_human wearer = hud?.mymob as mob_living_carbon_human;
        obj_item_clothing_suit_space suit = astype(wearer.wear_suit, typeof(obj_item_clothing_suit_space)) as obj_item_clothing_suit_space;
        if (suit != null)
            await suit.toggle_spacesuit(wearer, manual_toggle: TRUE);
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:479
    public override async DmTask MouseEntered(object location = null, object control = null, object @params = null)
    {
        if (usr != get_mob())
            return;
        await base.MouseEntered(location, control, @params);
        hovering = TRUE;
        mob_living_carbon_human wearer = hud?.mymob as mob_living_carbon_human;
        (astype(wearer.wear_suit, typeof(obj_item_clothing_suit_space)) as obj_item_clothing_suit_space)?.update_hud_icon(usr);
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:487
    public override void MouseExited(object location = null, object control = null, object @params = null)
    {
        if (usr != get_mob())
            return;
        base.MouseExited(location, control, @params);
        hovering = FALSE;
        mob_living_carbon_human wearer = hud?.mymob as mob_living_carbon_human;
        (astype(wearer.wear_suit, typeof(obj_item_clothing_suit_space)) as obj_item_clothing_suit_space)?.update_hud_icon(usr);
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:495
    public virtual void update_spacesuit_hud_icon(object cell_state = null, object cell_percent = null, float? thermal_on = null)
    {
        thermal_on ??= TRUE;
        if (Dm.truthy(cell_state))
        {
            switch (cell_state)
            {
                case SPACESUIT_NO_ICON:
                    icon_state = null;
                    maptext = null;
                    cached_thermal_on = TRUE; //for next use
                    update_appearance(UPDATE_ICON);
                    return;
                case SPACESUIT_CELL_MISSING:
                case SPACESUIT_CELL_EMPTY:
                    icon_state = Dm.text("spacesuit_[]", cell_state);
                    maptext = null;
                    break;
                default:
                    icon_state = Dm.text("spacesuit_[]", cell_state);
                    break;
            }
        }
        if (Dm.truthy(cell_percent) && Dm.truthy(hovering))
            maptext = MAPTEXT(Dm.text("<div align='right'>[]%</div>", round(Dm.num(cell_percent), 0.1f)));
        else
            maptext = null;
        if (thermal_on != cached_thermal_on)
        {
            cached_thermal_on = thermal_on.Value;
            update_appearance(UPDATE_ICON);
        }
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:517
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);
        if (!Dm.truthy(cached_thermal_on) && Dm.truthy(icon_state))
            Dm.combine(ref dot, off_overlay);
        return dot;
    }
}

// tg: code/_onclick/hud/screen_objects/screen_objects.dm:552  (pulling, 2026-10-15; the var block is generated)
public partial class atom_movable_screen_pull : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:560
    [DmParams("mapload", "hud_owner")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        update_appearance(UPDATE_ICON_STATE);
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:564
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (isobserver(usr))
            return null;
        await usr.stop_pulling();
        return null;
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:569
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        bool pulling = Dm.truthy(hud?.mymob?.pulling);
        icon_state = Dm.text("[][]", base_icon_state, pulling ? null : 0);
        // DEVIATION(DV422): the owner (2026-11-14) wants the button shown unlit while nothing is pulled; tg's styles but voidwalker have no pull0 (BYOND
        // draws nothing), so there the lit picture stays, greyed and dimmed
        if (!pulling && !Dm.truthy(icon_exists(icon, icon_state)))
        {
            icon_state = Dm.str(base_icon_state);
            color = "#606060";
        }
        else
            color = null;
        return await base.update_icon_state(__extra);
    }
}

// tg: code/_onclick/hud/screen_objects/screen_objects.dm:573
public partial class atom_movable_screen_resist : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:581
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        flick(Dm.text("[]_on", base_icon_state), this);
        if (isliving(usr))
        {
            mob_living L = usr as mob_living;
            L.resist();
        }
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/screen_objects.dm:587  (M8z, D234: the var block is generated)
public partial class atom_movable_screen_rest : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:595
    [DmParams("mapload", "hud_owner")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        update_appearance(UPDATE_ICON_STATE);
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:599
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (isliving(usr))
        {
            mob_living L = usr as mob_living;
            await L.toggle_resting();
        }
        return null;
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:604
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (hud?.mymob is not mob_living user)
            return await base.update_icon_state(__extra);
        icon_state = Dm.text("[][]", base_icon_state, Dm.truthy(user.resting) ? "_on" : null);
        return await base.update_icon_state(__extra);
    }
}

// tg: code/_onclick/hud/screen_objects/screen_objects.dm:611  (M8z, D234, D241: the var block is generated)
public partial class atom_movable_screen_sleep : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:619
    [DmParams("mapload", "hud_owner")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // Invisible by default
        SetInvisibility(INVISIBILITY_ABSTRACT, INVISIBILITY_SOURCE_SLEEP_HUD_BUTTON);
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:624
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (!isliving(usr) || IS_UNCONSCIOUS(usr))
            return null;
        // if(!usr.client?.prefs.read_preference(/datum/preference/toggle/remove_double_click)) { flick("[base_icon_state]_flick", src); return }:
        // DEVIATION (D241): the preferences are M9's and the client sends no double clicks (DblClick below is never reached), so the
        // button takes tg's path for remove_double_click on: a single click asks.

        object tgui_answer = await tgui_alert(usr, I18n.T("You sure you want to sleep for a while?"), I18n.T("Sleeping"), Dm.list(I18n.T("Yes"), I18n.T("No")));
        if (Dm.eq(tgui_answer, I18n.T("Yes")) && !IS_UNCONSCIOUS(usr))
        {
            mob_living L = usr as mob_living;
            await L.Sleeping(400);
        }
        return null;
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:636  (the client sends no double clicks, D241: only a host that does reaches it)
    public override async DmTask DblClick(object location = null, object control = null, object @params = null)
    {
        // if(!isliving(usr) || usr.client?.prefs.read_preference(/datum/preference/toggle/remove_double_click)) return: the preference is
        // taken as on (D241, as Click above), so the double click's own L.Sleeping(400) is never reached
        return;
    }
}

// tg: code/_onclick/hud/screen_objects/screen_objects.dm:643
public partial class atom_movable_screen_storage : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:649
    [DmParams("mapload", "hud_owner", "new_master")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object new_master = Dm.arg(__extra, 1);
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        master_ref = WEAKREF(new_master as datum) as datum_weakref;
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:653
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        datum_storage storage_master = master_ref?.resolve() as datum_storage;
        if (!(storage_master is datum_storage))
            return FALSE;

        if (world.time <= Dm.num(usr.next_move))
            return TRUE;
        if (Dm.truthy(usr.incapacitated))
            return TRUE;
        if (istype(usr.loc, typeof(obj_vehicle_sealed_mecha))) // stops inventory actions in a mech (ismecha, inline)
            return TRUE;

        obj_item inserted = usr.get_active_held_item() as obj_item;
        if (Dm.truthy(inserted))
            storage_master.attempt_insert(inserted, usr);

        return TRUE;
    }
}

// tg: code/_onclick/hud/screen_objects/screen_objects.dm:671
public partial class atom_movable_screen_storage_cell : atom_movable_screen_storage
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:673
    public override async DmTask<object> mouse_drop_receive(atom dropped = null, mob user = null, object @params = null)
    {
        atom target = dropped;
        datum_storage storage = master_ref?.resolve() as datum_storage;

        if (storage == null || !(user is mob_living) || storage != user.active_storage)
            return null;

        if (!Dm.truthy(await user.can_perform_action(storage.parent, FORBID_TELEKINESIS_REACH)))
            return null;

        if (target.loc != storage.real_location)
            return null;

        /// Due to items in storage ignoring transparency for click hitboxes, this only can happen if we drag onto a free cell - aka after all current contents
        Dm.remove(storage.real_location.contents, target);
        Dm.append(storage.real_location.contents, target);
        await storage.refresh_views();
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/screen_objects.dm:710
public partial class atom_movable_screen_throw_catch : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:717
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (isliving(usr))
        {
            mob_living user = usr as mob_living;
            user.toggle_throw_mode();
        }
        return null;
    }
}

// tg: code/_onclick/hud/screen_objects/screen_objects.dm:722
public partial class atom_movable_screen_zone_sel : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:728  var/static/list/hover_overlays_cache = list() (rules 10.6: a
    // TgState field, at the end of this file)
    public static ref DmList hover_overlays_cache => ref TgState.Current.atom_movable_screen_zone_sel__hover_overlays_cache;

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:731
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        update_appearance();
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:735
    public override async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (isobserver(usr))
            return null;

        DmList modifiers = params2list(@params);
        object icon_x = text2num(LAZYACCESS(modifiers, ICON_X));
        object icon_y = text2num(LAZYACCESS(modifiers, ICON_Y));
        object choice = get_zone_at(icon_x, icon_y);
        if (!Dm.truthy(choice))
            return 1;

        return set_selected_zone(choice, usr);
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:748
    public override async DmTask MouseEntered(object location = null, object control = null, object @params = null)
    {
        await base.MouseEntered(location, control, @params);
        MouseMove(location, control, @params);
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:752
    // GAP(lead): BYOND's builtin /atom/MouseMove (slots.tsv: builtin) has no C# root yet, so this is declared as a root here
    // (it becomes an override when the root is declared).
    public virtual void MouseMove(object location = null, object control = null, object @params = null)
    {
        if (isobserver(usr))
            return;

        DmList modifiers = params2list(@params);
        object icon_x = text2num(LAZYACCESS(modifiers, ICON_X));
        object icon_y = text2num(LAZYACCESS(modifiers, ICON_Y));
        object choice = get_zone_at(icon_x, icon_y);

        if (Dm.eq(hovering, choice))
            return;
        Dm.remove(vis_contents, hover_overlays_cache[hovering]);
        hovering = choice;

        // Don't need to account for turf cause we're on the hud babyyy
        obj_effect_overlay_zone_sel overlay_object = hover_overlays_cache[choice] as obj_effect_overlay_zone_sel;
        if (!Dm.truthy(overlay_object))
        {
            overlay_object = Dm.@new<obj_effect_overlay_zone_sel>();
            overlay_object.icon_state = Dm.text("[]", choice);
            hover_overlays_cache[choice] = overlay_object;
        }
        Dm.append(vis_contents, overlay_object);
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:781
    // BYOND's builtin /atom/MouseExited: see atom_movable_screen_inventory.MouseExited.
    public override void MouseExited(object location = null, object control = null, object @params = null)
    {
        if (!isobserver(usr) && Dm.truthy(hovering))
        {
            Dm.remove(vis_contents, hover_overlays_cache[hovering]);
            hovering = null;
        }
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:786
    public virtual object get_zone_at(object icon_x = null, object icon_y = null)
    {
        // switch(icon_y): a null coordinate (text2num of a missing param) matches no range
        if (icon_y == null)
            return null;
        float y = Dm.num(icon_y);
        float x = Dm.num(icon_x);
        bool x_known = icon_x != null;
        if (y >= 1 && y <= 9) //Legs
        {
            if (x_known && x >= 10 && x <= 15)
                return BODY_ZONE_R_LEG;
            if (x_known && x >= 17 && x <= 22)
                return BODY_ZONE_L_LEG;
        }
        else if (y >= 10 && y <= 13) //Hands and groin
        {
            if (x_known && x >= 8 && x <= 11)
                return BODY_ZONE_R_ARM;
            if (x_known && x >= 12 && x <= 20)
                return BODY_ZONE_PRECISE_GROIN;
            if (x_known && x >= 21 && x <= 24)
                return BODY_ZONE_L_ARM;
        }
        else if (y >= 14 && y <= 22) //Chest and arms to shoulders
        {
            if (x_known && x >= 8 && x <= 11)
                return BODY_ZONE_R_ARM;
            if (x_known && x >= 12 && x <= 20)
                return BODY_ZONE_CHEST;
            if (x_known && x >= 21 && x <= 24)
                return BODY_ZONE_L_ARM;
        }
        else if (y >= 23 && y <= 30) //Head, but we need to check for eye or mouth
        {
            if (x_known && x >= 12 && x <= 20)
            {
                if (y >= 23 && y <= 24)
                {
                    if (x >= 15 && x <= 17)
                        return BODY_ZONE_PRECISE_MOUTH;
                }
                else if (y == 26) //Eyeline, eyes are on 15 and 17
                {
                    if (x >= 14 && x <= 18)
                        return BODY_ZONE_PRECISE_EYES;
                }
                else if (y >= 25 && y <= 27)
                {
                    if (x >= 15 && x <= 17)
                        return BODY_ZONE_PRECISE_EYES;
                }
                return BODY_ZONE_HEAD;
            }
        }
        return null;
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:824
    public virtual object set_selected_zone(object choice = null, mob user = null, float? should_log = null)
    {
        should_log ??= TRUE;
        if (user != hud?.mymob)
            return null;

        if (!Dm.eq(choice, hud.mymob.zone_selected))
        {
            if (Dm.truthy(should_log))
            {
                // hud.mymob.log_manual_zone_selected_update("screen_hud", new_target = choice) (:829): GAP(lead):
                // /mob/proc/log_manual_zone_selected_update (logging) is not declared (a deferred leaf: logs).
                Dm.Deferred("code/_onclick/hud/screen_objects/screen_objects.dm:829", "set_selected_zone: log_manual_zone_selected_update (logging)");
            }
            hud.mymob.zone_selected = Dm.str(choice);
            update_appearance();
            if (Dm.truthy(user._listen_lookup?[COMSIG_MOB_SELECTED_ZONE_SET]))
                user._SendSignal(COMSIG_MOB_SELECTED_ZONE_SET, Dm.list(user, choice));
        }

        return TRUE;
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:837
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);
        if (!Dm.truthy(hud?.mymob))
            return dot;
        Dm.combine(ref dot, mutable_appearance(overlay_icon, Dm.text("[]", hud.mymob.zone_selected)));
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:265  var/static/mutable_appearance/blocked_overlay (made on
    // first use: atom_movable_screen_inventory_hand.blocked_overlay)
    internal mutable_appearance atom_movable_screen_inventory_hand__blocked_overlay;

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:466  var/static/mutable_appearance/off_overlay (made on first use:
    // atom_movable_screen_spacesuit.off_overlay)
    internal mutable_appearance atom_movable_screen_spacesuit__off_overlay;

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:728  var/static/list/hover_overlays_cache = list()
    internal DmList atom_movable_screen_zone_sel__hover_overlays_cache = Dm.list();
}

// tg: code/_onclick/hud/screen_objects/screen_objects.dm:1050
public partial class atom_movable_screen_splash : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:1058
    [DmParams("mapload", "hud_owner", "C", "visible", "use_previous_title")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        client C = Dm.arg<client>(__extra, 1);
        object visible = Dm.arg<object>(__extra, 2);
        object use_previous_title = Dm.arg<object>(__extra, 3);
        if (C == null)
            return dot;

        holder = C;

        if (!Dm.truthy(visible))
            alpha = 0;

        // if(!use_previous_title) icon = SStitle.icon, else SStitle.previous_icon or INITIALIZE_HINT_QDEL: DEVIATION(DV44), SStitle
        // is not run (the client draws the title screen itself), so the splash keeps its blank title icon; a previous title never exists
        if (Dm.truthy(use_previous_title))
            return INITIALIZE_HINT_QDEL;

        Dm.append(holder.screen, this);
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:1078
    public virtual void fade(object @out = null, float? qdel_after = null)
    {
        qdel_after ??= TRUE;
        if (QDELETED(this))
            return;
        if (Dm.truthy(@out))
        {
            mouse_opacity = MOUSE_OPACITY_TRANSPARENT;
            animate(this, Dm.named("alpha", 0), Dm.named("time", 30));
        }
        else
        {
            alpha = 0;
            animate(this, Dm.named("alpha", 255), Dm.named("time", 30));
        }
        if (Dm.truthy(qdel_after))
            // QDEL_IN(src, 30) (__DEFINES/qdel.dm:59), expanded inline (rules 11.1 (f))
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), (30 > GC_FILTER_QUEUE ? (object)WEAKREF(this) : this)), 30, file: "code/_onclick/hud/screen_objects/screen_objects.dm", line: 1088);
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:1090
    public override object Destroy(float? force = null)
    {
        if (holder != null)
        {
            Dm.remove(holder.screen, this);
            holder = null;
        }
        return base.Destroy(force);
    }
}

// The syndicate campaign X3a (D316): the martial arts' combo display (the streak's letters over the action bar, fading out), whole.
// tg: code/_onclick/hud/screen_objects/screen_objects.dm:1110
public partial class atom_movable_screen_combo : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:1116
    public virtual void clear_streak()
    {
        animate(this, Dm.named("alpha", 0), 2 * SECONDS, SINE_EASING);
        timerid = _addtimer(Dm.@new<datum_callback>(this, nameof(reset_icons)), 2 * SECONDS, Dm.bitor(TIMER_UNIQUE, TIMER_STOPPABLE), file: "code/_onclick/hud/screen_objects/screen_objects.dm", line: 1118);
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:1120
    public virtual void reset_icons()
    {
        cut_overlays();
        icon_state = "";
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:1124
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        string streak = Dm.arg(__extra, 0) as string ?? "";
        object time = Dm.arg(__extra, 1) ?? 2 * SECONDS;
        reset_icons();
        if (Dm.truthy(timerid))
            deltimer(timerid);
        alpha = 255;
        if (!Dm.truthy(streak))
            return await base.update_icon_state(__extra);
        timerid = _addtimer(Dm.@new<datum_callback>(this, nameof(clear_streak)), Dm.num(time), Dm.bitor(TIMER_UNIQUE, TIMER_STOPPABLE), file: "code/_onclick/hud/screen_objects/screen_objects.dm", line: 1131);
        icon_state = "combo";
        for (int i = 1; i <= length(streak); ++i)
        {
            string intent_text = copytext(streak, i, i + 1) as string;
            image intent_icon = image(icon, this, Dm.text("combo_[]", intent_text));
            intent_icon.pixel_w = 16 * (i - 1) - 8 * length(streak);
            add_overlay(intent_icon);
        }
        return await base.update_icon_state(__extra);
    }
}

// tg: code/_onclick/hud/screen_objects/screen_objects.dm:1152 (gaps S1, D291: the hunger display, its burger and its bar)
public partial class atom_movable_screen_hunger : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:1145-1150  #define HUNGER_STATE_* (file-local, #undef at :1317-1322)
    internal const float HUNGER_STATE_FAT = 5;
    internal const float HUNGER_STATE_FULL = 4;
    internal const float HUNGER_STATE_FINE = 3;
    internal const float HUNGER_STATE_HUNGRY = 2;
    internal const float HUNGER_STATE_VERY_HUNGRY = 1;
    internal const float HUNGER_STATE_STARVING = 0;

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:1170
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        datum_hud hud_owner = Dm.arg<datum_hud>(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        mob_living hungry = hud_owner?.mymob as mob_living;
        if (!(hungry is mob_living))
            return dot;

        if (hungry.mob_mood == null)
            screen_loc = ui_mood; // Slot in where mood normally is if mood is not here (we arent a human)

        // Burger next to the bar
        food_image = image(icon: food_icon, icon_state: food_icon_state, pixel_x: -5);
        food_image.plane = plane;
        food_image.appearance_flags = Dm.bitor(food_image.appearance_flags, KEEP_APART); // To be unaffected by filters applied to src
        food_image.add_filter("simple_outline", 2, outline_filter(1, COLOR_BLACK, OUTLINE_SHARP) as DmList);
        Dm.append(underlays, food_image); // To be below filters applied to src

        // The actual bar
        hunger_bar = Dm.@new<atom_movable_screen_hunger_bar>(this, null);
        Dm.append(vis_contents, hunger_bar);

        update_hunger_bar(instant: TRUE);
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:1192
    public virtual void update_hunger_state()
    {
        mob_living hungry = hud?.mymob as mob_living;
        if (!(hungry is mob_living))
            return;

        if (HAS_TRAIT(hungry, TRAIT_NOHUNGER) || (ishuman(hungry) && !Dm.truthy(hungry.get_organ_slot(ORGAN_SLOT_STOMACH))))
        {
            fullness = NUTRITION_LEVEL_FED;
            state = HUNGER_STATE_FINE;
            return;
        }
        if (HAS_TRAIT(hungry, TRAIT_FAT))
        {
            fullness = NUTRITION_LEVEL_FAT;
            state = HUNGER_STATE_FAT;
            return;
        }

        if (HAS_TRAIT(hungry, TRAIT_GLUTTON))
        {
            fullness = NUTRITION_LEVEL_VERY_HUNGRY;
            state = HUNGER_STATE_HUNGRY; // Can't get enough
            return;
        }

        float full = round(Dm.num(hungry.get_fullness(only_consumable: TRUE)), 0.05f);
        fullness = full;
        // switch(fullness): the first matching range runs
        if (full >= 1 + NUTRITION_LEVEL_FULL)
            state = HUNGER_STATE_FULL;
        else if (full >= 1 + NUTRITION_LEVEL_HUNGRY && full <= NUTRITION_LEVEL_FULL)
            state = HUNGER_STATE_FINE;
        else if (full >= 1 + NUTRITION_LEVEL_VERY_HUNGRY && full <= NUTRITION_LEVEL_HUNGRY)
            state = HUNGER_STATE_FINE;
        else if (full >= 1 + NUTRITION_LEVEL_STARVING && full <= NUTRITION_LEVEL_VERY_HUNGRY)
            state = HUNGER_STATE_HUNGRY;
        else if (full >= 0 && full <= NUTRITION_LEVEL_STARVING)
            state = HUNGER_STATE_STARVING;
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:1224
    public override object update_appearance(float? updates = null)
    {
        update_hunger_bar();
        return base.update_appearance(updates);
    }

    /// Updates the hunger bar's appearance.
    /// If `instant` is TRUE, the bar will update immediately rather than animating.
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:1230
    public virtual void update_hunger_bar(float? instant = null)
    {
        instant ??= FALSE;
        object old_state = state;
        object old_fullness = fullness;
        update_hunger_state();
        if (!Dm.eq(old_state, state) || !Dm.eq(old_fullness, fullness))
        {
            // Fades out if we ARE "fine" AND if our stomach has no food digesting
            mob_living hungry = hud?.mymob as mob_living;
            if (alpha == 255 && (Dm.eq(state, HUNGER_STATE_FINE) && abs(Dm.num(fullness) - hungry.nutrition) < 1))
            {
                if (Dm.truthy(instant))
                    alpha = 0;
                else
                    Dm.animate(this, Dm.named("alpha", 0), Dm.named("time", 1 * SECONDS));
            }
            // Fades in if we WERE "fine" OR if our stomach has food digesting
            else if (alpha == 0 && (!Dm.eq(state, HUNGER_STATE_FINE) || abs(Dm.num(fullness) - hungry.nutrition) >= 1))
            {
                if (Dm.truthy(instant))
                    alpha = 255;
                else
                    Dm.animate(this, Dm.named("alpha", 255), Dm.named("time", 1 * SECONDS));
            }
        }

        if (!Dm.eq(old_state, state))
        {
            // Update filter around the bar
            if (Dm.eq(state, HUNGER_STATE_STARVING))
            {
                if (!Dm.truthy(get_filter("hunger_outline")))
                {
                    add_filter("hunger_outline", 1, Dm.list(Dm.kv("type", "outline"), Dm.kv("color", "#FF0033"), Dm.kv("alpha", 0), Dm.kv("size", 2)));
                    Dm.animate(get_filter("hunger_outline"), Dm.named("alpha", 200), Dm.named("time", 1.5f * SECONDS), Dm.named("loop", -1));
                    Dm.animate(Dm.named("alpha", 0), Dm.named("time", 1.5f * SECONDS));
                }
            }
            else if (Dm.eq(old_state, HUNGER_STATE_STARVING))
                remove_filter("hunger_outline");

            // Update color of the food
            if (Dm.eq(state, HUNGER_STATE_FAT) != Dm.eq(old_state, HUNGER_STATE_FAT))
            {
                Dm.remove(underlays, food_image);
                food_image.color = Dm.eq(state, HUNGER_STATE_FAT) ? COLOR_DARK : null;
                Dm.append(underlays, food_image);
            }
        }

        // Update hunger bar
        if (!Dm.eq(old_fullness, fullness))
            // instant if invisible OR if instant is set
            hunger_bar.update_fullness(fullness, alpha == 0 || Dm.truthy(instant));
    }
}

// tg: code/_onclick/hud/screen_objects/screen_objects.dm:1271 (gaps S1, D291)
public partial class atom_movable_screen_hunger_bar : atom_movable_screen
{
    /// Mask
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:1276  VAR_PRIVATE/static/icon/bar_mask
    public static Anomaly.Core.DmShim.icon bar_mask
    {
        get => TgState.Current.atom_movable_screen_hunger_bar__bar_mask;
        set => TgState.Current.atom_movable_screen_hunger_bar__bar_mask = value;
    }

    /// Gradient used to color the bar
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:1278  VAR_PRIVATE/static/list/hunger_gradient
    public static DmList hunger_gradient => TgState.Current.atom_movable_screen_hunger_bar__hunger_gradient;

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:1292
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        atom_movable movable_loc = ismovable(loc) ? (atom_movable)loc : null;
        screen_loc = movable_loc?.screen_loc;
        bar_mask ??= Dm.icon(icon, "hungerbar_mask");
        return dot;
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:1298
    public virtual void update_fullness(object new_fullness = null, object instant = null)
    {
        float band = round(Dm.num(new_fullness) / NUTRITION_LEVEL_FULL, 0.05f);
        if (band == last_fullness_band)
            return;
        last_fullness_band = band;
        // Update color
        object new_color = gradient(hunger_gradient, clamp(band, 0, 1.2f));
        if (Dm.truthy(instant))
            color = new_color;
        else
            Dm.animate(this, Dm.named("color", new_color), Dm.named("time", 0.5f * SECONDS));
        // Update mask
        object old_bar_offset = bar_offset;
        bar_offset = clamp(-20 + (20 * band), -20, 0);
        if (!Dm.eq(old_bar_offset, bar_offset))
        {
            if (Dm.truthy(instant) || old_bar_offset == null)
                add_filter("hunger_bar_mask", 1, alpha_mask_filter(0, bar_offset, bar_mask) as DmList);
            else
                transition_filter("hunger_bar_mask", alpha_mask_filter(0, bar_offset) as DmList, 0.5f * SECONDS);
        }
    }

    // BYOND's gradient(list, index) for the bar's fixed list of (position, colour) stops: clamped to the end stops, each RGB
    // channel linear between the two stops around the index (BYOND ref /proc/gradient; the shim has no gradient, as BlendRGB
    // in __HELPERS/icons.cs notes).
    private static object gradient(DmList stops, float index)
    {
        int count = (int)Dm.length(stops) / 2;
        float Pos(int i) => Dm.num(stops[2 * i + 1]);
        string Col(int i) => stops[2 * i + 2] as string;
        if (index <= Pos(0))
            return Col(0);
        for (int i = 0; i + 1 < count; i++)
        {
            if (index > Pos(i + 1))
                continue;
            DmList first = Dm.rgb2num(Col(i));
            DmList second = Dm.rgb2num(Col(i + 1));
            float t = (index - Pos(i)) / (Pos(i + 1) - Pos(i));
            float Channel(int c) => Dm.num(first[c]) + (Dm.num(second[c]) - Dm.num(first[c])) * t;
            return Dm.rgb(System.MathF.Round(Channel(1)), System.MathF.Round(Channel(2)), System.MathF.Round(Channel(3)));
        }
        return Col(count - 1);
    }
}

public sealed partial class TgState
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:1276  VAR_PRIVATE/static/icon/bar_mask
    internal Anomaly.Core.DmShim.icon atom_movable_screen_hunger_bar__bar_mask;

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:1278  VAR_PRIVATE/static/list/hunger_gradient = list(...)
    internal DmList atom_movable_screen_hunger_bar__hunger_gradient = Dm.list(
        0.0f, "#FF0000",
        0.2f, "#FF8000",
        0.4f, "#f0f000",
        0.6f, "#00FF00",
        0.8f, "#46daff",
        1.0f, "#2A72AA",
        1.2f, "#494949");
}

// tg: code/_onclick/hud/screen_objects/screen_objects.dm:1346
public partial class atom_movable_screen_blood_level : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:1346
    public virtual void on_mob_life(mob_living source = null, object seconds_per_tick = null)
    {
        Dm.NotTranslated("code/_onclick/hud/screen_objects/screen_objects.dm:1346", "on_mob_life: M4a P2 (skeleton)");
    }
}

// tg: code/_onclick/hud/screen_objects/screen_objects.dm:1382
public partial class atom_movable_screen_slime_power : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:1382
    public virtual void update_maptext()
    {
        Dm.NotTranslated("code/_onclick/hud/screen_objects/screen_objects.dm:1382", "update_maptext: M4a P2 (skeleton)");
    }
}

// tg: code/_onclick/hud/screen_objects/screen_objects.dm:1390
/// Used to show how many monkeys & slimes are in the console
public partial class atom_movable_screen_xenobio_console : atom_movable_screen
{
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:1355  #define FORMAT_XENOBIO_HUD_MAPTEXT(text_to_use) MAPTEXT_SPESSFONT(...) (file-local, #undef at :1463)
    internal static string FORMAT_XENOBIO_HUD_MAPTEXT(object text_to_use) => MAPTEXT_SPESSFONT(Dm.text("<span style='color: []; text-align: center; line-height: 1.9; '>[]</span>", COLOR_WHITE, text_to_use));
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:1356  #define POTION_DROP_SPEED 5 DECISECONDS (file-local, #undef at :1464)
    internal const float POTION_DROP_SPEED = 5 * DECISECONDS;

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:1397
    [DmParams("mapload", "hud_owner")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        potion_hud = Dm.@new<atom_movable_screen_xenobio_potion>();
        potion_hud.layer = layer - 1;
        Dm.append(vis_contents, potion_hud);
        potion_launcher = Dm.@new<atom_movable_screen_xenobio_potion>();
        potion_launcher.layer = layer - 2;
        Dm.append(vis_contents, potion_launcher);
        return dot;
    }

    /// Called by the console any time we update the monkeys, slimes, or max slimes
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:1407
    public virtual void on_update_hud(object slimes = null, object monkeys = null, object max_slimes = null)
    {
        maptext = FORMAT_XENOBIO_HUD_MAPTEXT(Dm.text("[]\n[]/[]", monkeys, slimes, max_slimes));
        maptext_x = 5;
        maptext_y = 2;
    }

    /// Called by the console any time we update the potion
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:1413
    public virtual void update_potion(obj_item_slimepotion_slime potion = null)
    {
        if (potion == null)
        {
            potion_hud.eject_pot();
            flick("xenobio_potion_launch", potion_launcher);
        }
        else if (Dm.truthy(potion_hud.stored_potion))
            potion_hud.swap_pot(potion);
        else
            potion_hud.add_pot(potion);
    }

    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:1422
    public override object Destroy(float? force = null)
    {
        Dm.remove(vis_contents, potion_hud);
        QDEL_NULL(ref potion_hud);
        Dm.remove(vis_contents, potion_launcher);
        QDEL_NULL(ref potion_launcher);
        return base.Destroy(force);
    }
}

// tg: code/_onclick/hud/screen_objects/screen_objects.dm:1429
public partial class atom_movable_screen_xenobio_potion : atom_movable_screen
{
    /// Visually ejects the current potion
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:1436
    public virtual void eject_pot(obj_item_slimepotion_slime potion = null)
    {
        animate(this, 2 * DECISECONDS, Dm.named("pixel_y", 280));
        stored_potion = FALSE;
    }

    /// Visually add the current potion
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:1441
    public virtual void add_pot(obj_item_slimepotion_slime potion = null)
    {
        stored_potion = TRUE;
        icon = potion.icon;
        icon_state = potion.icon_state;
        pixel_y = 280;
        pixel_x = -8;
        add_filter("potion_outline", 1, outline_filter(1, "#eeeeee", OUTLINE_SQUARE) as DmList);
        add_filter("potion_glow", 2, drop_shadow_filter(0.1f, 0.1f, 2, 0, "#eeeeee") as DmList);
        // transform.Scale(0.8, 0.8): tg scales the copy that reading transform gives and drops it, so the icon keeps its size
        animate(this, atom_movable_screen_xenobio_console.POTION_DROP_SPEED, Dm.named("easing", BOUNCE_EASING), Dm.named("pixel_y", 19));
    }

    /// Swap out our current potion for a new one
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:1453
    public virtual void swap_pot(obj_item_slimepotion_slime potion = null)
    {
        _addtimer(Dm.@new<datum_callback>(this, nameof(swap_pot_icon), potion), atom_movable_screen_xenobio_console.POTION_DROP_SPEED, TIMER_CLIENT_TIME, file: "code/_onclick/hud/screen_objects/screen_objects.dm", line: 1454);
        animate(this, atom_movable_screen_xenobio_console.POTION_DROP_SPEED, Dm.named("easing", BACK_EASING), Dm.named("pixel_x", -50));
    }

    /// Swaps the potion icon & name. Made for use w/ addtimer() so as to not disrupt the animation chain
    // tg: code/_onclick/hud/screen_objects/screen_objects.dm:1458
    public virtual void swap_pot_icon(obj_item pot = null)
    {
        name = pot.name;
        icon_state = pot.icon_state;
        animate(this, atom_movable_screen_xenobio_console.POTION_DROP_SPEED, Dm.named("easing", BACK_EASING), Dm.named("pixel_x", -8));
    }
}
