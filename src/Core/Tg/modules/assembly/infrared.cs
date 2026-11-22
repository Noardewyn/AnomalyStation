// Translated from tgstation (snapshot 1d99bd7f): code/modules/assembly/infrared.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps AS1, D333): the infrared emitter, its beam of /obj/effect/ebeam/reacting/infrared segments (beam.dm's
// reacting beam) drawn from it (or its holder) along its dir up to the first turf that blocks it, visible or invisible, rebuilt
// when it moves, turns, is secured, toggled, attached or dropped and when the turf at the beam's end clears or changes; a
// dense or thrown thing crossing the beam pulses it with the triple beep (a three-second cooldown); its rotation, its holder
// overlay and its window ("InfraredEmitter"). The beep line's chat icon (icon2html) is left out, as at the other icon2html
// sites (the chat icons, not-translated.md section 76). The var block is generated (D111).

// tg: code/modules/assembly/infrared.dm:1
public partial class obj_item_assembly_infra : obj_item_assembly
{
    // tg: code/modules/assembly/infrared.dm:29
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_simple_rotation)));
        return dot;
    }

    // tg: code/modules/assembly/infrared.dm:33
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref active_beam);
        buffer_turf = null;
        return base.Destroy(force);
    }

    // tg: code/modules/assembly/infrared.dm:39
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("The infrared trigger is [].", Dm.truthy(on) ? I18n.T("on") : I18n.T("off"))));
        return dot;
    }

    /// Checks if the passed movable can block the beam.
    // tg: code/modules/assembly/infrared.dm:44
    public virtual object atom_blocks_beam(atom_movable beam_atom = null)
    {
        if (beam_atom == null)
            return FALSE;
        if (beam_atom == this || beam_atom == holder)
            return FALSE;
        // Blocks beams from triggering themselves, important to avoid infinite loops
        if (istype(beam_atom, typeof(obj_effect_ebeam)))
            return FALSE;
        // Anti-revenant / anti-ghost guard
        if (Dm.truthy(beam_atom.invisibility))
            return FALSE;
        // In general non-dense items should not block beams, but make special cases for things being thrown
        if (!Dm.truthy(beam_atom.density) && beam_atom.throwing == null)
            return FALSE;
        // The actually important check. Ensures stuff like mobs trip it but stuff like laser projectiles don't
        if (Dm.truthy(Dm.bitand(beam_atom.pass_flags_self, beam_pass_flags)))
            return FALSE;
        if (isitem(beam_atom))
        {
            obj_item beam_item = (obj_item)beam_atom;
            if (Dm.truthy(Dm.bitand(beam_item.item_flags, ABSTRACT)))
                return FALSE;
        }

        return TRUE;
    }

    /// Checks if the passed turf (or something on it) can block the beam.
    // tg: code/modules/assembly/infrared.dm:69
    public virtual object turf_blocks_beam(turf beam_turf = null)
    {
        if (Dm.truthy(beam_turf.density))
            return TRUE;
        foreach (atom_movable blocker in Dm.each_cast<atom_movable>(beam_turf))
        {
            if (Dm.truthy(atom_blocks_beam(blocker)))
                return TRUE;
        }
        return FALSE;
    }

    /// Used to refresh the beam in whatever context.
    /// glide_seed/glide_time (from a predecessor beam's get_last_geometry) make the rebuilt beam glide
    /// from the old position instead of snapping; left as defaults for non-movement refreshes.
    // tg: code/modules/assembly/infrared.dm:80
    [ShouldNotSleep]
    public virtual void make_beam(DmList glide_seed = null, float? glide_time = null)
    {
        glide_time ??= 0;

        if (buffer_turf != null)
        {
            UnregisterSignal(buffer_turf, Dm.list(COMSIG_ATOM_EXITED, COMSIG_TURF_CHANGE));
            buffer_turf = null;
        }

        QDEL_NULL(ref active_beam);
        if (!Dm.truthy(on) || !Dm.truthy(secured))
            return;

        atom start_loc = (atom)holder ?? this;
        turf start_turf = start_loc.loc as turf;
        if (!istype(start_turf, typeof(turf)))
            return;
        // One extra turf is added to max length to get an extra buffer
        DmList potential_turfs = get_line(start_turf, get_ranged_target_turf(start_turf, dir, max_beam_length + 1) as atom) as DmList;
        if (!Dm.truthy(length(potential_turfs)))
            return;

        DmList final_turfs = Dm.list();
        foreach (turf target_turf in Dm.each_cast<turf>(potential_turfs))
        {
            if (target_turf != start_turf && Dm.truthy(turf_blocks_beam(target_turf)))
                break;
            Dm.append(ref final_turfs, target_turf);
        }

        if (!Dm.truthy(length(final_turfs)))
            return;

        turf last_turf = (turf)final_turfs[length(final_turfs)];
        buffer_turf = get_step(last_turf, dir);

        active_beam = start_loc.Beam(
            BeamTarget: last_turf,
            beam_type: typeof(obj_effect_ebeam_reacting_infrared),
            icon: Dm.resource("icons/effects/beam.dmi"),
            icon_state: "infrared",
            emissive: TRUE,
            override_target_pixel_x: pixel_x,
            override_target_pixel_y: pixel_y,
            glide_seed: glide_seed,
            glide_time: glide_time) as datum_beam;
        RegisterSignal(active_beam, COMSIG_BEAM_ENTERED, nameof(beam_entered));
        RegisterSignal(active_beam, COMSIG_BEAM_TURFS_CHANGED, nameof(beam_turfs_changed));
        update_visible();
        // Buffer can be null (if we're at map edge for an example) but this fine
        if (buffer_turf != null)
        {
            // We need to check the state of the turf at the end of the beam, to determine when we need to re-grow (if blocked)
            RegisterSignal(buffer_turf, COMSIG_ATOM_EXITED, nameof(buffer_exited));
            RegisterSignal(buffer_turf, COMSIG_TURF_CHANGE, nameof(buffer_changed));
        }
    }

    // tg: code/modules/assembly/infrared.dm:132
    [SignalHandler]
    public virtual void beam_entered(datum_beam source = null, obj_effect_ebeam hit = null, atom_movable entered = null)
    {
        // First doesn't count
        if (hit == active_beam.elements[1])
            return;
        if (!Dm.truthy(atom_blocks_beam(entered)))
            return;

        Waiting.NoSleep(beam_trigger(hit, entered));
    }

    // tg: code/modules/assembly/infrared.dm:143
    [SignalHandler]
    public virtual void beam_turfs_changed(datum_beam source = null, DmList post_change_callbacks = null)
    {
        // If the turfs changed it's possible something is now blocking it, remake when done
        Dm.append(ref post_change_callbacks, Dm.@new<datum_callback>(this, nameof(make_beam)));
    }

    // tg: code/modules/assembly/infrared.dm:148
    [SignalHandler]
    public virtual void buffer_exited(turf source = null, atom_movable exited = null, params object[] __extra)
    {
        if (!Dm.truthy(atom_blocks_beam(exited)))
            return;

        make_beam();
    }

    // tg: code/modules/assembly/infrared.dm:156
    [SignalHandler]
    public virtual void buffer_changed(turf source = null, object path = null, DmList new_baseturfs = null, object flags = null, DmList post_change_callbacks = null)
    {
        Dm.append(ref post_change_callbacks, Dm.@new<datum_callback>(this, nameof(make_beam)));
    }

    // tg: code/modules/assembly/infrared.dm:161
    public virtual async DmTask beam_trigger(obj_effect_ebeam hit = null, atom_movable entered = null)
    {
        make_beam();
        // COOLDOWN_FINISHED(src, next_activate) (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        if (!(next_activate <= world.time))
            return;

        await pulse();
        // [icon2html(src, hearers(holder || src))]: the chat icons (section 76)
        audible_message(
            message: span_infoplain(I18n.T("*beep* *beep* *beep*")),
            hearing_distance: hearing_range);
        playsound(this, Dm.resource("sound/machines/beep/triple_beep.ogg"), ASSEMBLY_BEEP_VOLUME, TRUE, extrarange: hearing_range - SOUND_RANGE + 1, falloff_distance: hearing_range);
        // COOLDOWN_START(src, next_activate, 3 SECONDS) (__DEFINES/cooldowns.dm:110), inline (rules 11.1 (a))
        next_activate = world.time + 3 * SECONDS;
    }

    // tg: code/modules/assembly/infrared.dm:174
    public override async DmTask<object> activate(mob activator = null)
    {
        object dot = await base.activate(activator);
        if (!Dm.truthy(dot))
            return dot;

        toggle_on();
        return dot;
    }

    // tg: code/modules/assembly/infrared.dm:181
    public override async DmTask<object> toggle_secure()
    {
        object dot = await base.toggle_secure();
        make_beam();
        return dot;
    }

    /// Toggles the beam on or off.
    // tg: code/modules/assembly/infrared.dm:186
    public virtual void toggle_on()
    {
        on = Dm.num(!Dm.truthy(on));
        make_beam();
        update_appearance();
    }

    /// Toggles the visibility of the beam.
    // tg: code/modules/assembly/infrared.dm:192
    public virtual void toggle_visible()
    {
        visible = Dm.num(!Dm.truthy(visible));
        update_visible();
        update_appearance();
    }

    /// Updates the visibility of the beam (if active).
    // tg: code/modules/assembly/infrared.dm:198
    public virtual void update_visible()
    {
        if (Dm.truthy(visible))
        {
            foreach (obj_effect_ebeam beam in Dm.each_cast<obj_effect_ebeam>(active_beam?.elements))
                beam.RemoveInvisibility(REF(this));
        }
        else
        {
            foreach (obj_effect_ebeam beam in Dm.each_cast<obj_effect_ebeam>(active_beam?.elements))
                beam.SetInvisibility(INVISIBILITY_ABSTRACT, REF(this));
        }
    }

    // tg: code/modules/assembly/infrared.dm:206
    public override async DmTask<object> vv_edit_var(object var_name = null, object var_value = null)
    {
        object dot = await base.vv_edit_var(var_name, var_value);
        if (!Dm.truthy(dot))
            return dot;
        object sw__1 = var_name;
        if (Dm.eq(sw__1, nameof(visible)))
        {
            update_visible();
            update_appearance();
        }
        else if (Dm.eq(sw__1, nameof(on)) || Dm.eq(sw__1, nameof(max_beam_length)) || Dm.eq(sw__1, nameof(beam_pass_flags)))
        {
            make_beam();
            update_appearance();
        }
        return dot;
    }

    // tg: code/modules/assembly/infrared.dm:219
    public override object update_appearance(float? updates = null)
    {
        object dot = base.update_appearance(updates);
        holder?.update_appearance(updates);
        return dot;
    }

    // tg: code/modules/assembly/infrared.dm:223
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        attached_overlays = Dm.list();
        if (Dm.truthy(on))
            Dm.append(ref attached_overlays, Dm.text("[]_on", base_icon_state));

        Dm.append(ref dot, attached_overlays);
        return dot;
    }

    // tg: code/modules/assembly/infrared.dm:231
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        if (holder != null)
            holder_movement(); //sync the dir of the device as well if it's contained in a TTV or an assembly holder
        else
            make_beam();
        return dot;
    }

    // tg: code/modules/assembly/infrared.dm:238
    public override async DmTask on_attach()
    {
        await base.on_attach();
        make_beam();
        // GAP(types): tg sets it on any holder; a wired emitter on a host that is not an item (a door) has a null holder here
        // (assembly.cs's header)
        if (holder != null)
            holder.set_dir_on_move = set_dir_on_move;
    }

    // tg: code/modules/assembly/infrared.dm:243
    public override async DmTask<object> on_detach()
    {
        if (holder != null) // GAP(types): as on_attach
            holder.set_dir_on_move = Dm.initial(holder, (atom_movable p) => p.set_dir_on_move);
        object dot = await base.on_detach();
        if (!Dm.truthy(dot))
            return dot;
        make_beam();
        return dot;
    }

    // tg: code/modules/assembly/infrared.dm:250
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        DmList glide_seed = active_beam?.get_last_geometry() as DmList;
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        if (loc == old_loc)
            return dot;
        if (!Dm.truthy(visible) || Dm.truthy(forced) || !Dm.truthy(movement_dir) || !Dm.truthy(await Adjacent(old_loc)))
        {
            make_beam();
            return dot;
        }
        float glide_time = ICON_SIZE_ALL / Math.Max(glide_size, MIN_GLIDE_SIZE) * world.tick_lag * GLOB.glide_size_multiplier;
        make_beam(glide_seed, glide_time);
        return dot;
    }

    // tg: code/modules/assembly/infrared.dm:261
    public override async DmTask setDir(object newdir = null)
    {
        float prev_dir = dir;
        await base.setDir(newdir);
        if (dir == prev_dir)
            return;
        make_beam();
    }

    // tg: code/modules/assembly/infrared.dm:268
    public override async DmTask<object> ui_status(mob user = null, datum_ui_state state = null)
    {
        return Dm.truthy(is_secured(user)) ? await base.ui_status(user, state) : UI_CLOSE;
    }

    // tg: code/modules/assembly/infrared.dm:271
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (!Dm.truthy(ui))
        {
            ui = Dm.@new<datum_tgui>(user, this, "InfraredEmitter", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/assembly/infrared.dm:277
    public override async DmTask<object> ui_data(mob user = null)
    {
        DmList data = Dm.list();
        data["on"] = on;
        data["visible"] = visible;
        return data;
    }

    // tg: code/modules/assembly/infrared.dm:283
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        object sw__1 = action;
        if (Dm.eq(sw__1, "power"))
        {
            toggle_on();
            return TRUE;
        }
        else if (Dm.eq(sw__1, "visibility"))
        {
            toggle_visible();
            return TRUE;
        }
        return dot;
    }
}
