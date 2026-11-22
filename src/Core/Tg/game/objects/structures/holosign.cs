// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/holosign.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 slice T3 (D164): the whole file. The var block is generated. The sign's picture is a vis_contents overlay of the sign itself
// (create_vis_overlay: the sign is alpha 0 and its overlay shows the translucent sprite of holosigns.dmi, whose own pixels are 50 to 75 % opaque),
// which the client draws as the entity's vis children. The penlite barrier turns back a sick human (gaps VI1).

//holographic signs and barriers

// tg: code/game/objects/structures/holosign.dm:3
public partial class obj_structure_holosign : obj_structure
{
    // tg: code/game/objects/structures/holosign.dm:15
    [DmParams("mapload", "source_projector")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        obj_item_holosign_creator source_projector = Dm.arg<obj_item_holosign_creator>(__extra, 0);
        object dot = null;
        dot = base.Initialize(mapload, Dm.forward(__extra, source_projector));
        Waiting.NoSleep(AddComponent(typeof(datum_component_holographic_nature)));
        create_vis_overlay();
        if (Dm.truthy(source_projector))
        {
            projector = source_projector;
            LAZYADD(ref projector.signs, this);
        }
        return dot;
    }

    // tg: code/game/objects/structures/holosign.dm:23
    public override object Destroy(float? force = null)
    {
        if (Dm.truthy(projector))
        {
            LAZYREMOVE(ref projector.signs, this);
            projector = null;
        }
        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/holosign.dm:29
    public override async DmTask update_atom_colour()
    {
        await base.update_atom_colour();
        create_vis_overlay();
    }

    // tg: code/game/objects/structures/holosign.dm:33
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        await attack_holosign(user as mob_living, modifiers);
        return null;
    }

    // tg: code/game/objects/structures/holosign.dm:39
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = await base.CanAllowThrough(mover, border_dir);
        if (!Dm.truthy(dot) && isprojectile(mover)) // Its short enough to be shot over
            return TRUE;
        return dot;
    }

    // tg: code/game/objects/structures/holosign.dm:44
    public override async DmTask<object> emp_act(object severity = null, params object[] __extra)
    {
        object dot = await base.emp_act(severity, __extra);
        if (Dm.truthy(Dm.bitand(dot, EMP_PROTECT_SELF)))
            return dot;

        await take_damage(Dm.div(150, severity), BRUTE, ENERGY, FALSE);
        return dot;
    }

    // tg: code/game/objects/structures/holosign.dm:51
    public virtual async DmTask attack_holosign(mob_living user = null, DmList modifiers = null)
    {
        await user.do_attack_animation(this, ATTACK_EFFECT_PUNCH);
        user.changeNext_move(CLICK_CD_MELEE);
        await take_damage(5, BRUTE, MELEE, 1);
        log_combat(user, this, "swatted");
    }

    // tg: code/game/objects/structures/holosign.dm:57
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
    {
        if (Dm.eq(damage_type, BRUTE))
            playsound(loc, Dm.resource("sound/items/weapons/egloves.ogg"), 80, TRUE);
        else if (Dm.eq(damage_type, BURN))
            playsound(loc, Dm.resource("sound/items/weapons/egloves.ogg"), 80, TRUE);
        return null;
    }

    // tg: code/game/objects/structures/holosign.dm:64
    public virtual void create_vis_overlay()
    {
        if (!Dm.truthy(use_vis_overlay))
            return;

        turf our_turf = get_turf(this);
        alpha = 0;
        SSvis_overlays.remove_vis_overlay(this, managed_vis_overlays);
        obj_effect_overlay_vis overlay = SSvis_overlays.add_vis_overlay(this, icon, icon_state, ABOVE_MOB_LAYER, MUTATE_PLANE(GAME_PLANE, our_turf), dir, add_appearance_flags: Dm.num(Dm.bitor(KEEP_APART, RESET_ALPHA))) as obj_effect_overlay_vis; //you see mobs under it, but you hit them like they are above it
        if (Dm.truthy(color) || Dm.truthy(cached_color_filter))
            Waiting.NoSleep(overlay.add_atom_colour(Dm.truthy(cached_color_filter) ? cached_color_filter : color, FIXED_COLOUR_PRIORITY));
    }
}

// tg: code/game/objects/structures/holosign.dm:79  (var-only)

// tg: code/game/objects/structures/holosign.dm:84
public partial class obj_structure_holosign_barrier : obj_structure_holosign
{
    // tg: code/game/objects/structures/holosign.dm:100
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = await base.CanAllowThrough(mover, border_dir);
        if (Dm.truthy(dot))
            return dot;

        if (Dm.truthy(opened))
            return TRUE;

        if (iscarbon(mover))
        {
            mob_living_carbon moving_carbon = (mob_living_carbon)mover;
            if (Dm.truthy(IS_UNCONSCIOUS_OR_CRIT(moving_carbon))) // Lets not prevent dragging unconscious/dead people.
                return TRUE;
            if (Dm.truthy(allow_walk) && Dm.eq(moving_carbon.move_intent, MOVE_INTENT_WALK))
                return TRUE;
        }
        return dot;
    }

    // tg: code/game/objects/structures/holosign.dm:116
    public override async DmTask<object> ranged_item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null)
    {
        object dot = await base.ranged_item_interaction(user, tool, modifiers);
        if (tool != projector)
            return dot;
        if (Dm.truthy(openable))
            await open(user);
        return dot;
    }

    // tg: code/game/objects/structures/holosign.dm:123
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.item_interaction(user, tool, modifiers, __extra);
        if (tool != projector)
            return dot;
        qdel(this);
        return dot;
    }

    // tg: code/game/objects/structures/holosign.dm:129
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (!Dm.truthy(opened))
            icon_state = Dm.str(base_icon_state);
        else
            icon_state = Dm.str(pass_icon_state);

        create_vis_overlay();
        return await base.update_icon_state(__extra);
    }

    // tg: code/game/objects/structures/holosign.dm:137
    public virtual async DmTask open(object user = null)
    {
        if (!Dm.truthy(openable))
        {
            (user as mob)?.balloon_alert(user as mob, I18n.T("unable!"));
            return;
        }

        if (!(cooldown_open <= world.time)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        {
            (user as mob)?.balloon_alert(user as mob, I18n.T("on cooldown!"));
            return;
        }

        if (!Dm.truthy(opened))
        {
            density = FALSE;
            opened = TRUE;
            playsound(this, Dm.resource("sound/machines/door/door_open.ogg"), 50, TRUE);
        }
        else
        {
            density = TRUE;
            opened = FALSE;
            playsound(this, Dm.resource("sound/machines/door/door_close.ogg"), 50, TRUE);
        }

        await update_icon_state();
        cooldown_open = world.time + 1 * SECONDS; // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline
    }
}

// tg: code/game/objects/structures/holosign.dm:162
public partial class obj_structure_holosign_barrier_wetsign : obj_structure_holosign_barrier
{
    // tg: code/game/objects/structures/holosign.dm:169
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = await base.CanAllowThrough(mover, border_dir);
        if (iscarbon(mover))
        {
            mob_living_carbon C = (mob_living_carbon)mover;
            if (Dm.truthy(IS_UNCONSCIOUS_OR_CRIT(C))) // Lets not prevent dragging unconscious/dead people.
                return TRUE;
            if (Dm.truthy(allow_walk) && !Dm.eq(C.move_intent, MOVE_INTENT_WALK))
                return FALSE;
        }
        return dot;
    }
}

// tg: code/game/objects/structures/holosign.dm:179  /obj/structure/holosign/barrier/engineering (var-only)

// tg: code/game/objects/structures/holosign.dm:187
public partial class obj_structure_holosign_barrier_atmos : obj_structure_holosign_barrier
{
    // tg: code/game/objects/structures/holosign.dm:200
    public virtual void clearview_transparency()
    {
        mouse_opacity = MOUSE_OPACITY_TRANSPARENT;
        alpha = 25;
        SSvis_overlays.remove_vis_overlay(this, managed_vis_overlays);
        turf our_turf = get_turf(this);
        SSvis_overlays.add_vis_overlay(this, icon, icon_state, ABOVE_MOB_LAYER, MUTATE_PLANE(GAME_PLANE, our_turf), dir);
    }

    // tg: code/game/objects/structures/holosign.dm:207
    public virtual void reset_transparency()
    {
        mouse_opacity = Dm.initial(this, (obj_structure_holosign_barrier_atmos p) => p.mouse_opacity);
        alpha = Dm.initial(this, (obj_structure_holosign_barrier_atmos p) => p.alpha);
        SSvis_overlays.remove_vis_overlay(this, managed_vis_overlays);
        turf our_turf = get_turf(this);
        SSvis_overlays.add_vis_overlay(this, icon, icon_state, ABOVE_MOB_LAYER, MUTATE_PLANE(GAME_PLANE, our_turf), dir, add_appearance_flags: RESET_ALPHA);
    }

    // tg: code/game/objects/structures/holosign.dm:227
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        air_update_turf(TRUE, TRUE);
        Waiting.NoSleep(AddElement(typeof(datum_element_give_turf_traits), string_list(Dm.list(TRAIT_FIREDOOR_STOP))));
        return dot;
    }

    // tg: code/game/objects/structures/holosign.dm:232
    public override object block_superconductivity() //Didn't used to do this, but it's "normal", and will help ease heat flow transitions with the players.
    {
        return TRUE;
    }

    // tg: code/game/objects/structures/holosign.dm:236
    public override object Destroy(float? force = null)
    {
        air_update_turf(TRUE, FALSE);
        return base.Destroy(force);
    }
}

// tg: code/game/objects/structures/holosign.dm:215  /obj/structure/holosign/barrier/atmos/sturdy and :221 /tram (var-only)
// tg: code/game/objects/structures/holosign.dm:241  /obj/structure/holosign/barrier/cyborg (var-only)

// tg: code/game/objects/structures/holosign.dm:260
public partial class obj_structure_holosign_barrier_medical : obj_structure_holosign_barrier
{
    // tg: code/game/objects/structures/holosign.dm:269
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = await base.CanAllowThrough(mover, border_dir);
        if (istype(mover, typeof(obj_vehicle_ridden)))
        {
            foreach (object M in Dm.each(mover.buckled_mobs))
            {
                if (ishuman(M))
                {
                    if (!Dm.truthy(CheckHuman((mob_living_carbon_human)M)))
                        return FALSE;
                }
            }
        }
        if (ishuman(mover))
            return CheckHuman((mob_living_carbon_human)mover);
        return TRUE;
    }

    // tg: code/game/objects/structures/holosign.dm:279
    public override object Bumped(atom_movable AM = null)
    {
        object dot = base.Bumped(AM);
        icon_state = Dm.str(base_icon_state);
        Waiting.NoSleep(update_icon_state());
        if (!ishuman(AM) && Dm.truthy(CheckHuman(AM as mob_living_carbon_human)))
            return dot;

        if (!(virus_detected <= world.time)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
            return dot;

        playsound(get_turf(this), Dm.resource("sound/machines/buzz/buzz-sigh.ogg"), 65, TRUE, 4);
        virus_detected = world.time + 1 * SECONDS; // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline
        icon_state = "holo_medical-deny";
        Waiting.NoSleep(update_icon_state());
        return dot;
    }

    // tg: code/game/objects/structures/holosign.dm:293
    public virtual object CheckHuman(mob_living_carbon_human sickboi = null)
    {
        object threat = sickboi.check_virus();
        if (Dm.num(get_disease_severity_value(threat)) > Dm.num(get_disease_severity_value(DISEASE_SEVERITY_MINOR)))
            return FALSE;
        return TRUE;
    }
}

// tg: code/game/objects/structures/holosign.dm:299
public partial class obj_structure_holosign_barrier_cyborg_hacked : obj_structure_holosign_barrier_cyborg
{
    // tg: code/game/objects/structures/holosign.dm:305
    public virtual void cooldown()
    {
        shockcd = FALSE;
    }

    // tg: code/game/objects/structures/holosign.dm:308
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (!Dm.truthy(shockcd))
        {
            if (ismob(user))
            {
                mob_living M = (mob_living)user;
                await M.electrocute_act(15, "Energy Barrier");
                shockcd = TRUE;
                _addtimer(Dm.@new<datum_callback>(this, nameof(cooldown)), 0.5f * SECONDS, file: "code/game/objects/structures/holosign.dm", line: 316);
            }
        }
        return null;
    }

    // tg: code/game/objects/structures/holosign.dm:319
    public override object Bumped(atom_movable AM = null)
    {
        if (Dm.truthy(shockcd))
            return null;

        if (!ismob(AM))
            return null;

        mob_living M = (mob_living)AM;
        Waiting.NoSleep(M.electrocute_act(15, "Energy Barrier"));
        shockcd = TRUE;
        _addtimer(Dm.@new<datum_callback>(this, nameof(cooldown)), 0.5f * SECONDS, file: "code/game/objects/structures/holosign.dm", line: 328);
        return null;
    }
}
