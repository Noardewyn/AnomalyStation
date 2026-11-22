// Translated from tgstation (snapshot 1d99bd7f): code/modules/mod/modules/module_kinesis.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MO2 (D333): the file whole. The kinesis module (a gravitational anomaly core's anti-gravity grip): it grabs an unanchored
// object, a dead creature (the kinesis+ module: any creature) in sight within its range, holds it with a beam, the kinesis glow and a
// humming loop, drags it toward the wearer's cursor, hits with a held item what it is dragged into, and throws it when used again;
// a thrown structure, machine or vehicle hurts what it hits. The admin kinesis++ grabs anything on the level and may make it phase
// through walls. The cursor tracking that drags the grabbed object (the cursor catcher's calculate_params, DV98) is the 2D view's: in
// the first-person client given_turf stays null, so a grabbed object floats where it was grabbed until it is thrown or let go
// (not-translated.md 222). The var blocks are generated (D111).

///Kinesis - Gives you the ability to move and launch objects.
// tg: code/modules/mod/modules/module_kinesis.dm:2
public partial class obj_item_mod_module_anomaly_locked_kinesis : obj_item_mod_module_anomaly_locked
{
    // tg: code/modules/mod/modules/module_kinesis.dm:39
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        soundloop = Dm.@new<datum_looping_sound_gravgen_kinesis>(this);
        return dot;
    }

    // tg: code/modules/mod/modules/module_kinesis.dm:43
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref soundloop);
        return base.Destroy(force);
    }

    // tg: code/modules/mod/modules/module_kinesis.dm:47
    public override async DmTask<object> on_select_use(atom target = null)
    {
        object dot = await base.on_select_use(target);
        if (!Dm.truthy(dot))
            return dot;
        if (mod.wearer.client == null)
            return dot;
        if (grabbed_atom != null)
        {
            atom_movable launched_object = grabbed_atom;
            clear_grab(playsound: FALSE);
            await launch(launched_object);
            return dot;
        }
        if (!Dm.truthy(range_check(target)))
        {
            balloon_alert(mod.wearer, I18n.T("too far!"));
            return dot;
        }
        if (!Dm.truthy(can_grab(target)))
        {
            balloon_alert(mod.wearer, I18n.T("can't grab!"));
            return dot;
        }
        await drain_power(use_energy_cost);
        grab_atom(target as atom_movable);
        return dot;
    }

    // tg: code/modules/mod/modules/module_kinesis.dm:67
    public override async DmTask on_deactivation(mob activator = null, float? display_message = null, float? deleting = null)
    {
        display_message ??= TRUE;
        deleting ??= FALSE;
        clear_grab(playsound: Dm.truthy(deleting) ? FALSE : TRUE);
    }

    // tg: code/modules/mod/modules/module_kinesis.dm:70
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (mod.wearer.client == null || Dm.truthy(INCAPACITATED_IGNORING(mod.wearer, INCAPABLE_GRAB)))
        {
            clear_grab();
            return null;
        }
        if (!Dm.truthy(range_check(grabbed_atom)))
        {
            balloon_alert(mod.wearer, I18n.T("out of range!"));
            clear_grab();
            return null;
        }
        Waiting.NoSleep(drain_power(use_energy_cost / 10));
        if (Dm.truthy(kinesis_catcher.mouse_params))
            kinesis_catcher.calculate_params();
        // the first-person client never sets the catcher's turf (DV98, see the file's header), so the grabbed object stays put
        if (kinesis_catcher.given_turf == null)
            return null;
        Waiting.NoSleep(mod.wearer.setDir(get_dir(mod.wearer, grabbed_atom)));
        float given_x = Dm.num(kinesis_catcher.given_x);
        float given_y = Dm.num(kinesis_catcher.given_y);
        if (grabbed_atom.loc == kinesis_catcher.given_turf)
        {
            if (grabbed_atom.pixel_x == given_x - ICON_SIZE_X / 2 && grabbed_atom.pixel_y == given_y - ICON_SIZE_Y / 2)
                return null; //spare us redrawing if we are standing still
            animate(grabbed_atom, 0.2f * SECONDS, Dm.named("pixel_x", grabbed_atom.base_pixel_x + given_x - ICON_SIZE_X / 2), Dm.named("pixel_y", grabbed_atom.base_pixel_y + given_y - ICON_SIZE_Y / 2));
            kinesis_beam.redrawing();
            return null;
        }
        animate(grabbed_atom, 0.2f * SECONDS, Dm.named("pixel_x", grabbed_atom.base_pixel_x + given_x - ICON_SIZE_X / 2), Dm.named("pixel_y", grabbed_atom.base_pixel_y + given_y - ICON_SIZE_Y / 2));
        kinesis_beam.redrawing();
        turf next_turf = get_step_towards(grabbed_atom, kinesis_catcher.given_turf) as turf;
        if (Dm.truthy(Waiting.NoSleep(grabbed_atom.Move(next_turf, get_dir(grabbed_atom, next_turf), 8))))
        {
            if (isitem(grabbed_atom) && Dm.@in(mod.wearer, next_turf))
            {
                obj_item grabbed_item = grabbed_atom as obj_item;
                clear_grab();
                Waiting.NoSleep(grabbed_item.pickup(mod.wearer));
                Waiting.NoSleep(mod.wearer.put_in_hands(grabbed_item));
            }
            return null;
        }
        float pixel_x_change = 0;
        float pixel_y_change = 0;
        object direction = get_dir(grabbed_atom, next_turf);
        if (Dm.truthy(Dm.bitand(direction, NORTH)))
            pixel_y_change = ICON_SIZE_Y / 2;
        else if (Dm.truthy(Dm.bitand(direction, SOUTH)))
            pixel_y_change = -ICON_SIZE_Y / 2;
        if (Dm.truthy(Dm.bitand(direction, EAST)))
            pixel_x_change = ICON_SIZE_X / 2;
        else if (Dm.truthy(Dm.bitand(direction, WEST)))
            pixel_x_change = -ICON_SIZE_X / 2;
        animate(grabbed_atom, 0.2f * SECONDS, Dm.named("pixel_x", grabbed_atom.base_pixel_x + pixel_x_change), Dm.named("pixel_y", grabbed_atom.base_pixel_y + pixel_y_change));
        kinesis_beam.redrawing();
        if (!isitem(grabbed_atom) || !(hit_cooldown <= world.time))
            return null;
        atom hitting_atom = null;
        if (Dm.truthy(next_turf.density))
            hitting_atom = next_turf;
        foreach (atom_movable movable_content in Dm.each<atom_movable>(next_turf.contents))
        {
            if (ismob(movable_content))
                continue;
            if (Dm.truthy(movable_content.density))
            {
                hitting_atom = movable_content;
                break;
            }
        }
        obj_item grabbed_held = grabbed_atom as obj_item;
        Waiting.NoSleep(grabbed_held.melee_attack_chain(mod.wearer, hitting_atom));
        hit_cooldown = world.time + hit_cooldown_time;
        return null;
    }

    // tg: code/modules/mod/modules/module_kinesis.dm:128
    public virtual object can_grab(atom target = null)
    {
        if (mod.wearer == target)
            return FALSE;
        if (!ismovable(target))
            return FALSE;
        if (iseffect(target))
            return FALSE;
        atom_movable movable_target = target as atom_movable;
        if (Dm.truthy(movable_target.anchored))
            return FALSE;
        if (Dm.truthy(movable_target.throwing))
            return FALSE;
        if (Dm.num(movable_target.move_resist) >= MOVE_FORCE_OVERPOWERING)
            return FALSE;
        if (ismob(movable_target))
        {
            if (!isliving(movable_target))
                return FALSE;
            mob_living living_target = movable_target as mob_living;
            if (living_target.buckled != null)
                return FALSE;
            if (Dm.num(living_target.stat) < stat_required)
                return FALSE;
        }
        else if (isitem(movable_target))
        {
            obj_item item_target = movable_target as obj_item;
            if (Dm.num(item_target.w_class) >= WEIGHT_CLASS_GIGANTIC)
                return FALSE;
            if (Dm.truthy(Dm.bitand(item_target.item_flags, ABSTRACT)))
                return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/mod/modules/module_kinesis.dm:158
    public virtual void grab_atom(atom_movable target = null)
    {
        grabbed_atom = target;
        if (isliving(grabbed_atom))
        {
            grabbed_atom.add_traits(Dm.list(TRAIT_IMMOBILIZED, TRAIT_HANDS_BLOCKED), REF(this));
            RegisterSignal(grabbed_atom, COMSIG_MOB_STATCHANGE, nameof(on_statchange));
        }
        ADD_TRAIT(grabbed_atom, TRAIT_NO_FLOATING_ANIM, REF(this));
        RegisterSignal(grabbed_atom, COMSIG_MOVABLE_SET_ANCHORED, nameof(on_setanchored));
        playsound(grabbed_atom, Dm.resource("sound/items/weapons/contractor_baton/contractorbatonhit.ogg"), 75, TRUE);
        kinesis_icon = mutable_appearance(icon: Dm.resource("icons/effects/effects.dmi"), icon_state: "kinesis", layer: Dm.num(grabbed_atom.layer) - 0.1f, appearance_flags: Dm.num(Dm.bitor(Dm.bitor(Dm.bitor(RESET_ALPHA, RESET_COLOR), RESET_TRANSFORM), KEEP_APART))) as mutable_appearance;
        // DEVIATION(DV12): kinesis_icon.overlays += emissive_appearance(icon = 'icons/effects/effects.dmi', icon_state = "kinesis",
        // offset_spokesman = grabbed_atom), the glow on the emissive plane
        grabbed_atom.add_overlay(kinesis_icon);
        kinesis_beam = mod.wearer.Beam(grabbed_atom, "kinesis") as datum_beam;
        kinesis_catcher = mod.wearer.overlay_fullscreen("kinesis", typeof(atom_movable_screen_fullscreen_cursor_catcher_kinesis), 0) as atom_movable_screen_fullscreen_cursor_catcher_kinesis;
        kinesis_catcher.assign_to_mob(mod.wearer);
        RegisterSignal(kinesis_catcher, COMSIG_SCREEN_ELEMENT_CLICK, nameof(on_catcher_click));
        soundloop.start();
        START_PROCESSING(SSfastprocess, this);
    }

    // tg: code/modules/mod/modules/module_kinesis.dm:176
    public virtual object clear_grab(float? playsound = null)
    {
        playsound ??= TRUE;
        if (grabbed_atom == null)
            return null;
        object dot = grabbed_atom;
        if (Dm.truthy(playsound))
            GlobalProcs.playsound(grabbed_atom, Dm.resource("sound/effects/empulse.ogg"), 75, TRUE);
        STOP_PROCESSING(SSfastprocess, this);
        UnregisterSignal(grabbed_atom, Dm.list(COMSIG_MOB_STATCHANGE, COMSIG_MOVABLE_SET_ANCHORED));
        kinesis_catcher = null;
        mod.wearer.clear_fullscreen("kinesis");
        grabbed_atom.cut_overlay(kinesis_icon);
        QDEL_NULL(ref kinesis_beam);
        if (isliving(grabbed_atom))
            grabbed_atom.remove_traits(Dm.list(TRAIT_IMMOBILIZED, TRAIT_HANDS_BLOCKED), REF(this));
        REMOVE_TRAIT(grabbed_atom, TRAIT_NO_FLOATING_ANIM, REF(this));
        if (!isitem(grabbed_atom))
            animate(grabbed_atom, 0.2f * SECONDS, Dm.named("pixel_x", grabbed_atom.base_pixel_x), Dm.named("pixel_y", grabbed_atom.base_pixel_y));
        grabbed_atom = null;
        soundloop.stop();
        return dot;
    }

    // tg: code/modules/mod/modules/module_kinesis.dm:196
    public virtual object range_check(atom target = null)
    {
        if (!isturf(mod.wearer.loc))
            return FALSE;
        if (ismovable(target) && !isturf(target.loc))
            return FALSE;
        if (!Dm.truthy(can_see(mod.wearer, target, grab_range)))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/mod/modules/module_kinesis.dm:206
    [SignalHandler]
    public virtual void on_catcher_click(atom source = null, object location = null, object control = null, object @params = null, object user = null)
    {
        DmList modifiers = params2list(@params);
        if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
            clear_grab();
    }

    // tg: code/modules/mod/modules/module_kinesis.dm:213
    [SignalHandler]
    public virtual void on_statchange(mob grabbed_mob = null, object new_stat = null)
    {
        if (Dm.num(new_stat) < stat_required)
            clear_grab();
    }

    // tg: code/modules/mod/modules/module_kinesis.dm:219
    [SignalHandler]
    public virtual void on_setanchored(atom_movable grabbed_atom = null, object anchorvalue = null)
    {
        if (Dm.truthy(grabbed_atom.anchored))
            clear_grab();
    }

    // tg: code/modules/mod/modules/module_kinesis.dm:225
    public virtual async DmTask launch(atom_movable launched_object = null)
    {
        playsound(launched_object, Dm.resource("sound/effects/magic/repulse.ogg"), 100, TRUE);
        RegisterSignal(launched_object, COMSIG_MOVABLE_IMPACT, nameof(launch_impact));
        turf target_turf = get_turf_in_angle(Dm.num(get_angle(mod.wearer, launched_object)), get_turf(this), 10) as turf;
        await launched_object.throw_at(target_turf, range: grab_range, speed: Dm.truthy(launched_object.density) ? 3 : 4, thrower: mod.wearer, spin: isitem(launched_object) ? TRUE : FALSE);
    }

    // tg: code/modules/mod/modules/module_kinesis.dm:231
    public virtual async DmTask launch_impact(atom_movable source = null, atom hit_atom = null, datum_thrownthing thrownthing = null)
    {
        UnregisterSignal(source, COMSIG_MOVABLE_IMPACT);
        if (!(isstructure(source) || ismachinery(source) || isvehicle(source)))
            return;
        bool damage_self = true;
        float damage = 8;
        if (Dm.truthy(source.density))
        {
            damage_self = false;
            damage = 15;
        }
        if (isliving(hit_atom))
        {
            mob_living living_atom = hit_atom as mob_living;
            await living_atom.apply_damage(damage, BRUTE);
        }
        else if (Dm.truthy(hit_atom.uses_integrity))
            await hit_atom.take_damage(damage, BRUTE, MELEE);
        if (damage_self && Dm.truthy(source.uses_integrity))
            await source.take_damage(Dm.num(source.max_integrity) / 5, BRUTE, MELEE);
    }
}

/// Admin suit version of kinesis. Can grab anything at any range, may enable phasing through walls.
// tg: code/modules/mod/modules/module_kinesis.dm:276
public partial class obj_item_mod_module_anomaly_locked_kinesis_admin : obj_item_mod_module_anomaly_locked_kinesis
{
    // tg: code/modules/mod/modules/module_kinesis.dm:288
    public override void grab_atom(atom_movable target = null)
    {
        base.grab_atom(target);
        if (Dm.truthy(phasing))
            ADD_TRAIT(grabbed_atom, TRAIT_MOVE_PHASING, REF(this));
    }

    // tg: code/modules/mod/modules/module_kinesis.dm:293
    public override object clear_grab(float? playsound = null)
    {
        object dot = base.clear_grab(playsound);
        if (!Dm.truthy(dot))
            return dot;
        atom_movable previous_grab = dot as atom_movable;
        if (Dm.truthy(phasing))
            REMOVE_TRAIT(previous_grab, TRAIT_MOVE_PHASING, REF(this));
        return dot;
    }

    // tg: code/modules/mod/modules/module_kinesis.dm:301
    public override object can_grab(atom target = null)
    {
        if (mod.wearer == target)
            return FALSE;
        if (!ismovable(target))
            return FALSE;
        atom_movable movable_target = target as atom_movable;
        if (Dm.truthy(movable_target.throwing))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/mod/modules/module_kinesis.dm:311
    public override object range_check(atom target = null)
    {
        if (!isturf(mod.wearer.loc))
            return FALSE;
        if (ismovable(target) && !isturf(target.loc))
            return FALSE;
        if (!Dm.eq(target.z, mod.wearer.z))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/mod/modules/module_kinesis.dm:320
    public override void on_setanchored(atom_movable grabbed_atom = null, object anchorvalue = null)
    {
        return; //thog dont care
    }

    // tg: code/modules/mod/modules/module_kinesis.dm:323
    public override object get_configuration(mob user = null)
    {
        DmList dot = base.get_configuration(user) as DmList;
        dot["phasing"] = add_ui_configuration(I18n.T("Phasing"), "bool", phasing);
        return dot;
    }

    // tg: code/modules/mod/modules/module_kinesis.dm:327
    public override async DmTask configure_edit(object key = null, object value = null)
    {
        switch (key)
        {
            case "phasing":
                phasing = Dm.num(value);
                if (grabbed_atom == null)
                    return;
                if (Dm.truthy(phasing))
                    ADD_TRAIT(grabbed_atom, TRAIT_MOVE_PHASING, REF(this));
                else
                    REMOVE_TRAIT(grabbed_atom, TRAIT_MOVE_PHASING, REF(this));
                break;
        }
    }
}
