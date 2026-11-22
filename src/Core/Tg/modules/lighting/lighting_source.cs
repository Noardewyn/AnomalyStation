// Translated from tgstation (snapshot 1d99bd7f): code/modules/lighting/lighting_source.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7 lumcount: the whole file. The server keeps tg's light sources and corners for gameplay (lumcount); the client still
// computes its own light from the replicated light vars (D24, docs/ai/specs/light-grid.md). Not here: print_sheet and read_sheet
// (debug output, docs/ai/specs/not-translated.md section 42). The macros SETUP_CORNERS_CACHE, SETUP_CORNERS_REMOVAL_CACHE,
// LUM_FALLOFF, LUM_FALLOFF_MULTIZ, APPLY_CORNER and REMOVE_CORNER (:142-210) become the struct corners_cache and its helpers
// apply_corner and remove_corner (rules 11: a macro that declares its caller's locals); EFFECT_UPDATE,
// GENERATE_MISSING_CORNERS and INSERT_CORNERS are expanded inline.

// tg: code/modules/lighting/lighting_source.dm:2  GLOBAL_LIST_EMPTY(lighting_sheets)
public partial class datum_controller_global_vars : datum_controller
{
    /// Cached global list of generated lighting sheets. See: datum/light_source/proc/get_sheet()
    // tg: code/modules/lighting/lighting_source.dm:2
    public DmList lighting_sheets;

    // tg: code/modules/lighting/lighting_source.dm:2
    public virtual void InitGloballighting_sheets()
    {
        lighting_sheets = Dm.list();
        Dm.append(ref gvars_datum_init_order, "lighting_sheets");
    }
}

// tg: code/modules/lighting/lighting_source.dm:7
[DmPath("/datum/light_source")]
public partial class datum_light_source : datum
{
    // tg: code/modules/lighting/lighting_source.dm:57
    [DmParams("owner", "top")]
    public override async DmTask New(params object[] __extra)
    {
        atom owner = Dm.arg<atom>(__extra, 0);
        atom top = Dm.arg<atom>(__extra, 1);
        source_atom = owner; // Set our new owner.
        add_to_light_sources(source_atom);
        top_atom = top;
        if (top_atom != source_atom)
            add_to_light_sources(top_atom);

        source_turf = top_atom as turf;

        light_power = source_atom.light_power;
        light_range = source_atom.light_range;
        light_color = source_atom.light_color;

        PARSE_LIGHT_COLOR();

        update();
        // tg: :74-75  if(GLOB.light_debug_enabled) source_atom.debug_lights(): the light debugger (admin debug, not ported)
    }

    // tg: code/modules/lighting/lighting_source.dm:77
    [DmParams()]
    public override object Destroy(float? force = null)
    {
        remove_lum();
        if (source_atom != null)
            remove_from_light_sources(source_atom);

        if (top_atom != null)
            remove_from_light_sources(top_atom);

        if (Dm.truthy(needs_update))
        {
            Dm.remove(ref datum_controller_subsystem_lighting.sources_queue, this);
            Dm.remove(ref datum_controller_subsystem_lighting.current_sources, this);
        }

        top_atom = null;
        source_atom = null;
        source_turf = null;

        return base.Destroy(force);
    }

    // PARSE_LIGHT_COLOR(src) (__DEFINES/lighting.dm:134), expanded as a helper of the source it reads and writes
    private void PARSE_LIGHT_COLOR()
    {
        if (!Dm.eq(light_color, COLOR_WHITE))
        {
            DmList color_parts = rgb2num(light_color as string);
            lum_r = Dm.num(color_parts[1]) / 255;
            lum_g = Dm.num(color_parts[2]) / 255;
            lum_b = Dm.num(color_parts[3]) / 255;
        }
        else
        {
            lum_r = 1f;
            lum_g = 1f;
            lum_b = 1f;
        }
    }

    ///add this light source to new_atom_host's light_sources list. updating movement registrations as needed
    // tg: code/modules/lighting/lighting_source.dm:95
    public virtual object add_to_light_sources(atom new_atom_host = null)
    {
        if (QDELETED(new_atom_host))
            return FALSE;

        LAZYADD(ref new_atom_host.light_sources, this);
        //yes, we register the signal to the top atom too, this is intentional and ensures contained lighting updates properly
        if (ismovable(new_atom_host))
            RegisterSignal(new_atom_host, COMSIG_MOVABLE_MOVED, nameof(update_host_lights));
        RegisterSignal(new_atom_host, COMSIG_TURF_NO_LONGER_BLOCK_LIGHT, nameof(force_update));
        return TRUE;
    }

    ///remove this light source from old_atom_host's light_sources list, unsetting movement registrations
    // tg: code/modules/lighting/lighting_source.dm:107
    public virtual object remove_from_light_sources(atom old_atom_host = null)
    {
        if (QDELETED(old_atom_host))
            return FALSE;

        LAZYREMOVE(ref old_atom_host.light_sources, this);
        if (ismovable(old_atom_host))
            UnregisterSignal(old_atom_host, COMSIG_MOVABLE_MOVED);
        UnregisterSignal(old_atom_host, COMSIG_TURF_NO_LONGER_BLOCK_LIGHT);
        return TRUE;
    }

    // EFFECT_UPDATE(level) (:121-127), expanded as a helper of the source it reads and writes
    private void EFFECT_UPDATE(float level)
    {
        if (needs_update == LIGHTING_NO_UPDATE)
            Dm.append(ref datum_controller_subsystem_lighting.sources_queue, this);
        if (needs_update < level)
            needs_update = level;
    }

    ///signal handler for when our host atom moves and we need to update our effects
    // tg: code/modules/lighting/lighting_source.dm:130
    [SignalHandler]
    public virtual void update_host_lights(atom_movable host = null)
    {
        if (QDELETED(host))
            return;

        // If the host is our owner, we want to call their update so they can decide who the top atom should be
        if (host == source_atom)
        {
            host.update_light();
            return;
        }

        // Otherwise, our top atom just moved, so we trigger a normal rebuild
        EFFECT_UPDATE(LIGHTING_CHECK_UPDATE);
    }

    /// This proc will cause the light source to update the top atom, and add itself to the update queue.
    // tg: code/modules/lighting/lighting_source.dm:144
    public virtual void update(atom new_top_atom = null)
    {
        // This top atom is different.
        if (new_top_atom != null && new_top_atom != top_atom)
        {
            if (top_atom != source_atom && top_atom.light_sources != null) // Remove ourselves from the light sources of that top atom.
                remove_from_light_sources(top_atom);

            top_atom = new_top_atom;

            if (top_atom != source_atom)
                add_to_light_sources(top_atom);
        }

        EFFECT_UPDATE(LIGHTING_CHECK_UPDATE);
    }

    // Will force an update without checking if it's actually needed.
    // tg: code/modules/lighting/lighting_source.dm:158
    [SignalHandler]
    public virtual void force_update()
    {
        EFFECT_UPDATE(LIGHTING_FORCE_UPDATE);
    }

    // Will cause the light source to recalculate turfs that were removed or added to visibility only.
    // tg: code/modules/lighting/lighting_source.dm:162
    public virtual void vis_update()
    {
        EFFECT_UPDATE(LIGHTING_VIS_UPDATE);
    }

    // SETUP_CORNERS_CACHE(lighting_source) (:168-185): the locals it declares, with the numbers read once (typed locals)
    private struct corners_cache
    {
        public float _turf_x, _turf_y, _turf_z;
        public DmList _sheet, _multiz_sheet;
        public float _range_offset, _multiz_offset, _light_power;
        public float _applied_lum_r, _applied_lum_g, _applied_lum_b;
        public float _lum_r, _lum_g, _lum_b;
    }

    // tg: code/modules/lighting/lighting_source.dm:168  SETUP_CORNERS_CACHE(src)
    private corners_cache SETUP_CORNERS_CACHE()
    {
        corners_cache c = default;
        c._turf_x = source_turf.x;
        c._turf_y = source_turf.y;
        c._turf_z = source_turf.z;
        c._sheet = get_sheet() as DmList;
        c._multiz_sheet = Dm.list();
        if (Dm.truthy(GET_LOWEST_STACK_OFFSET(source_turf.z)))
            c._multiz_sheet = get_sheet(multiz: TRUE) as DmList;
        c._range_offset = CEILING(Dm.num(light_range), 1) + 0.5f + 1 + Dm.num(visual_offset);
        c._multiz_offset = Dm.num(SSmapping.max_plane_offset) + 1;
        c._light_power = Dm.num(light_power);
        c._applied_lum_r = Dm.num(applied_lum_r);
        c._applied_lum_g = Dm.num(applied_lum_g);
        c._applied_lum_b = Dm.num(applied_lum_b);
        c._lum_r = Dm.num(lum_r);
        c._lum_g = Dm.num(lum_g);
        c._lum_b = Dm.num(lum_b);
        return c;
    }

    // tg: code/modules/lighting/lighting_source.dm:187  SETUP_CORNERS_REMOVAL_CACHE(src)
    private corners_cache SETUP_CORNERS_REMOVAL_CACHE()
    {
        corners_cache c = default;
        c._applied_lum_r = Dm.num(applied_lum_r);
        c._applied_lum_g = Dm.num(applied_lum_g);
        c._applied_lum_b = Dm.num(applied_lum_b);
        return c;
    }

    // tg: code/modules/lighting/lighting_source.dm:193-210  LUM_FALLOFF, LUM_FALLOFF_MULTIZ and APPLY_CORNER(C): returns `.`
    private float APPLY_CORNER(in corners_cache c, datum_lighting_corner C)
    {
        float dot;
        float cx = C.x ?? 0, cy = C.y ?? 0, cz = C.z ?? 0;
        if (cz == c._turf_z)
            dot = Dm.num(((DmList)c._sheet[cx - c._turf_x + c._range_offset])[cy - c._turf_y + c._range_offset]);
        else
            dot = Dm.num(((DmList)((DmList)c._multiz_sheet[cz - c._turf_z + c._multiz_offset])[cx - c._turf_x + c._range_offset])[cy - c._turf_y + c._range_offset]);
        dot *= c._light_power;
        float OLD = Dm.num(effect_str[C]);

        C.update_lumcount(
            (dot * c._lum_r) - (OLD * c._applied_lum_r),
            (dot * c._lum_g) - (OLD * c._applied_lum_g),
            (dot * c._lum_b) - (OLD * c._applied_lum_b));
        return dot;
    }

    // tg: code/modules/lighting/lighting_source.dm:212  REMOVE_CORNER(C)
    private void REMOVE_CORNER(in corners_cache c, datum_lighting_corner C)
    {
        float dot = -Dm.num(effect_str[C]);
        C.update_lumcount(
            dot * c._applied_lum_r,
            dot * c._applied_lum_g,
            dot * c._applied_lum_b);
    }

    /// Returns a list of lists, indexed with ints, that can be read to get the lighting multiplier at any one point
    /// If the requested sheet is multiz, this will be 3 lists deep, first handling z level then x and y
    /// otherwise it's just two, x then y
    // tg: code/modules/lighting/lighting_source.dm:223
    public virtual object get_sheet(float? multiz = null)
    {
        multiz ??= FALSE;
        object range = max(1, Dm.num(light_range));
        string key = Dm.text("[]-[]-[]-[]-[]-[]-[]-[]", range, visual_offset, offset_x, offset_y, light_dir, light_angle, light_height, multiz);
        DmList hand_back = GLOB.lighting_sheets[key] as DmList;
        if (!Dm.truthy(hand_back))
        {
            if (Dm.truthy(multiz))
                hand_back = generate_sheet_multiz(range, visual_offset, offset_x, offset_y, light_dir, light_angle, light_height) as DmList;
            else
                hand_back = generate_sheet(range, visual_offset, offset_x, offset_y, light_dir, light_angle, light_height) as DmList;
            GLOB.lighting_sheets[key] = hand_back;
        }
        return hand_back;
    }

    /// Returns a list of lists that encodes the light falloff of our source
    /// Takes anything that impacts our generation as input
    /// This function should be "pure", no side effects or reads from the source object
    // tg: code/modules/lighting/lighting_source.dm:239
    public virtual object generate_sheet(object range = null, object visual_offset = null, object x_offset = null, object y_offset = null, object center_dir = null, object angle = null, object height = null, float? z_level = null)
    {
        z_level ??= 0;
        DmList encode = Dm.list();
        // How far away the turfs we get are, and how many there are are often not the same calculation
        // So we need to include the visual offset, so we can ensure our sheet is large enough to accept all the distance differences
        float bound_range = ceil(Dm.num(range)) + Dm.num(visual_offset);

        // Corners are placed at 0.5 offsets
        // We need our coords to reflect that (though x_offsets that change the basis for how things are calculated are fine too)
        // `for(x in A to B)`: the 2 * bound_range + 2 corner coordinates from A. The port counts them: stepping a float from an offset
        // start (an impact's pixel offset of -1.77 gives -4.5553672) can pass B by a rounding error after the last step, and a sheet one
        // row short makes APPLY_CORNER read past its end (M7x slice A: shots' impacts on open floor at any offset).
        float x__start = -(bound_range) + Dm.num(x_offset) - 0.5f, y__start = -(bound_range) + Dm.num(y_offset) - 0.5f;
        int steps = (int)Math.Round(2 * bound_range + 1);
        for (int i = 0; i <= steps; i++)
        {
            float x = x__start + i;
            DmList row = Dm.list();
            for (int j = 0; j <= steps; j++)
                Dm.append(row, falloff_at_coord(x, y__start + j, z_level, range, center_dir, angle, height));
            Dm.append(encode, Dm.list(row));
        }
        return encode;
    }

    /// Returns a THREE dimensional list of lists that encodes the lighting falloff of our source
    /// Takes anything that impacts our generation as input
    /// This function should be "pure", no side effects or reads from the passed object
    // tg: code/modules/lighting/lighting_source.dm:259
    public virtual object generate_sheet_multiz(object range = null, object visual_offset = null, object x_offset = null, object y_offset = null, object center_dir = null, object angle = null, object height = null)
    {
        DmList encode = Dm.list();
        float z_range = Dm.num(SSmapping.max_plane_offset); // Let's just be safe yeah?
        for (float z = -z_range; z <= z_range; z++)
        {
            DmList sheet = generate_sheet(range, visual_offset, x_offset, y_offset, center_dir, angle, height, z) as DmList;
            Dm.append(encode, Dm.list(sheet));
        }
        return encode;
    }

    /// Takes x y and z offsets from the source as input, alongside our source's range
    /// Returns a value between 0 and 1, 0 being dark on that tile, 1 being fully lit
    // tg: code/modules/lighting/lighting_source.dm:270
    public virtual object falloff_at_coord(object x = null, object y = null, object z = null, object range = null, object center_dir = null, object angle = null, object height = null)
    {
        float range_divisor = max(1, Dm.num(range));

        // You may notice we use squares here even though there are three components
        // Because z diffs are so functionally small, cubes and cube roots are too aggressive
        // The larger the distance is, the less bright our light will be
        float fx = Dm.num(x), fy = Dm.num(y), fz = Dm.num(z);
        float multiplier = 1 - CLAMP01(sqrt(fx * fx + fy * fy + fz * fz + Dm.num(height)) / range_divisor);
        if (Dm.num(angle) >= 360 || Dm.num(angle) <= 0)
            return multiplier;

        // Turn our positional offset into an angle
        float coord_angle = Dm.num(delta_to_angle(x, y));
        // Get the difference between the angle we want, and the angle we have
        float center_angle = Dm.num(dir2angle(center_dir));
        float angle_delta = abs(center_angle - coord_angle);
        // Now we have to normalize the angle delta to be between 0 and 180, instead of 0 and 360
        // This ensures removing say, 15 degrees removes it from both sides, rather then just one
        // Turns an unfurling fan into a pair of scissors
        if (angle_delta > 180)
            angle_delta = 180 - (angle_delta - 180);
        // We allow angle deltas to a certian amount, angle / 2
        // If we pass that, then it starts effecting the visuals
        // Oh and we'll scale it so 30 degrees is the "0" point, where things become fully dark
        // This could be variable, it just isn't yet yaknow?
        return max(multiplier * (1 - max(angle_delta - (Dm.num(angle) / 2), 0) / 30), 0);
    }

    // tg: code/modules/lighting/lighting_source.dm:297-331  print_sheet and /proc/read_sheet: debug output to chat, not
    // translated (docs/ai/specs/not-translated.md section 42).

    /// This is the define used to calculate falloff.
    // tg: code/modules/lighting/lighting_source.dm:334
    public virtual object remove_lum()
    {
        corners_cache c = SETUP_CORNERS_REMOVAL_CACHE();
        applied = FALSE;
        foreach (datum_lighting_corner corner in Dm.each<datum_lighting_corner>(effect_str))
        {
            REMOVE_CORNER(c, corner);
            LAZYREMOVE(ref corner.affecting, this);
        }

        effect_str = null;
        return null;
    }

    // tg: code/modules/lighting/lighting_source.dm:343
    public virtual object recalc_corner(datum_lighting_corner corner = null)
    {
        corners_cache c = SETUP_CORNERS_CACHE();
        LAZYINITLIST(ref effect_str);
        if (Dm.truthy(effect_str[corner])) // Already have one.
        {
            REMOVE_CORNER(c, corner);
            effect_str[corner] = 0f;
        }

        float dot = APPLY_CORNER(c, corner);
        effect_str[corner] = dot;
        return dot;
    }

    // GENERATE_MISSING_CORNERS(gen_for) and INSERT_CORNERS(insert_into, draw_from) (:356-380)
    private static void INSERT_CORNERS(DmList insert_into, turf draw_from)
    {
        if (!Dm.truthy(draw_from.lighting_corners_initialised))
        {
            // Keep in mind. Lighting corners accept the bottom left (northwest) set of cords to them as input
            if (draw_from.lighting_corner_NE == null)
                draw_from.lighting_corner_NE = Dm.@new<datum_lighting_corner>(draw_from.x, draw_from.y, draw_from.z);
            if (draw_from.lighting_corner_SE == null)
                draw_from.lighting_corner_SE = Dm.@new<datum_lighting_corner>(draw_from.x, draw_from.y - 1, draw_from.z);
            if (draw_from.lighting_corner_SW == null)
                draw_from.lighting_corner_SW = Dm.@new<datum_lighting_corner>(draw_from.x - 1, draw_from.y - 1, draw_from.z);
            if (draw_from.lighting_corner_NW == null)
                draw_from.lighting_corner_NW = Dm.@new<datum_lighting_corner>(draw_from.x - 1, draw_from.y, draw_from.z);
            draw_from.lighting_corners_initialised = TRUE;
        }
        insert_into[draw_from.lighting_corner_NE] = 0f;
        insert_into[draw_from.lighting_corner_SE] = 0f;
        insert_into[draw_from.lighting_corner_SW] = 0f;
        insert_into[draw_from.lighting_corner_NW] = 0f;
    }

    /// Refreshes our lighting source to match its parent atom
    /// Returns TRUE if an update is needed, FALSE otherwise
    // tg: code/modules/lighting/lighting_source.dm:384
    public virtual object refresh_values()
    {
        float update = FALSE;
        atom source_atom = this.source_atom;
        turf old_source_turf = source_turf;

        if (QDELETED(source_atom))
        {
            qdel(this);
            return FALSE;
        }

        if (!Dm.eq(source_atom.light_power, light_power))
        {
            light_power = source_atom.light_power;
            update = TRUE;
        }

        if (!Dm.eq(source_atom.light_range, light_range))
        {
            light_range = source_atom.light_range;
            update = TRUE;
        }

        if (top_atom == null)
        {
            top_atom = source_atom;
            update = TRUE;
        }

        if (!Dm.truthy(light_range) || !Dm.truthy(light_power))
        {
            qdel(this);
            return FALSE;
        }

        atom visual_source = source_atom;
        if (isturf(top_atom))
        {
            visual_source = source_atom;
            if (source_turf != top_atom)
            {
                source_turf = (turf)top_atom;
                update = TRUE;
            }
        }
        else
        {
            visual_source = top_atom;
            if (top_atom.loc != source_turf)
            {
                // tg stores any loc in the typed var; a non-turf loc is null here and fails the isturf check just below
                source_turf = top_atom.loc as turf;
                update = TRUE;
            }
        }

        if (!isturf(source_turf))
        {
            if (Dm.truthy(applied))
                remove_lum();
            return FALSE;
        }

        if (Dm.truthy(light_range) && Dm.truthy(light_power) && !Dm.truthy(applied))
            update = TRUE;

        if (!Dm.eq(source_atom.light_color, light_color))
        {
            light_color = source_atom.light_color;
            PARSE_LIGHT_COLOR();
            update = TRUE;
        }

        else if (!Dm.eq(applied_lum_r, lum_r) || !Dm.eq(applied_lum_g, lum_g) || !Dm.eq(applied_lum_b, lum_b))
            update = TRUE;

        if (source_atom.light_dir != light_dir)
        {
            light_dir = source_atom.light_dir;
            update = TRUE;
        }

        if (source_atom.light_angle != light_angle)
        {
            light_angle = source_atom.light_angle;
            update = TRUE;
        }

        if (!Dm.eq(source_atom.light_height, light_height))
        {
            light_height = source_atom.light_height;
            update = TRUE;
        }

        DmList visual_offsets = calculate_light_offset(visual_source) as DmList;
        if (!Dm.eq(visual_offsets[1], offset_x) || !Dm.eq(visual_offsets[2], offset_y) || source_turf != old_source_turf)
        {
            offset_x = Dm.num(visual_offsets[1]);
            offset_y = Dm.num(visual_offsets[2]);
            visual_offset = max(ceil(abs(offset_x)), ceil(abs(offset_y)));
            update = TRUE;
        }

        // If we need to update, well, update
        if (Dm.truthy(update))
        {
            needs_update = LIGHTING_CHECK_UPDATE;
            applied = TRUE;
            return TRUE;
        }

        // Otherwise, go off the needs_update var. If it requires an update provide one, otherwise we're kosher
        if (needs_update == LIGHTING_CHECK_UPDATE)
            return FALSE; //nothing's changed
        return TRUE;
    }

    /// Returns a list of lighting corners this source impacts
    // tg: code/modules/lighting/lighting_source.dm:476
    public virtual object impacted_corners()
    {
        DmList corners = Dm.list();
        if (source_turf == null)
            return Dm.list();

        float oldlum = source_turf.luminosity;
        float working_range = ceil(Dm.num(light_range) + Dm.num(visual_offset));
        source_turf.luminosity = working_range;

        bool uses_multiz = Dm.truthy(GET_LOWEST_STACK_OFFSET(source_turf.z));

        if (!uses_multiz) // Yes I know this could be acomplished with an if in the for loop, but it's fukin lighting code man
        {
            foreach (turf T in view_turfs(working_range, source_turf)) // view()'s turfs (Builtins.view_turfs, an optimization)
            {
                if (T.directional_opacity == ALL_CARDINALS) // IS_OPAQUE_TURF(T) (__DEFINES/turfs.dm:12)
                    continue;
                INSERT_CORNERS(corners, T);
            }
            source_turf.luminosity = oldlum;
            return corners;
        }

        foreach (turf T in view_turfs(working_range, source_turf)) // view()'s turfs (Builtins.view_turfs, an optimization)
        {
            if (T.directional_opacity == ALL_CARDINALS) // IS_OPAQUE_TURF(T)
                continue;
            INSERT_CORNERS(corners, T);

            turf below = GET_TURF_BELOW(T) as turf;
            turf previous = T;
            while (below != null)
            {
                // If we find a non transparent previous, end
                if (!istransparentturf(previous))
                    break;
                if (below.directional_opacity == ALL_CARDINALS) // IS_OPAQUE_TURF(below)
                    // If we're opaque but the tile above us is transparent, then we should be counted as part of the potential "space"
                    // Of this corner
                    break;
                // Now we do lighting things to it
                INSERT_CORNERS(corners, below);
                // ANNND then we add the one below it
                previous = below;
                below = GET_TURF_BELOW(below) as turf;
            }

            turf above = GET_TURF_ABOVE(T) as turf;
            while (above != null)
            {
                // If we find a non transparent turf, end
                if (!istransparentturf(above) || above.directional_opacity == ALL_CARDINALS)
                    break;
                INSERT_CORNERS(corners, above);
                above = GET_TURF_ABOVE(above) as turf;
            }
        }

        source_turf.luminosity = oldlum;
        return corners;
    }

    // tg: code/modules/lighting/lighting_source.dm:529
    public virtual object update_corners()
    {
        if (!Dm.truthy(refresh_values()))
            return null;

        DmList corners = impacted_corners() as DmList;
        corners_cache c = SETUP_CORNERS_CACHE();
        float dot = 0;

        DmList new_corners = (DmList)Dm.sub(corners, this.effect_str);
        LAZYINITLIST(ref this.effect_str);
        foreach (datum_lighting_corner corner in Dm.each<datum_lighting_corner>(new_corners))
        {
            dot = APPLY_CORNER(c, corner);
            if (dot != 0)
            {
                LAZYADD(ref corner.affecting, this);
                effect_str[corner] = dot;
            }
        }
        // New corners are a subset of corners. so if they're both the same length, there are NO old corners!
        if (needs_update != LIGHTING_VIS_UPDATE && length(corners) != length(new_corners))
        {
            foreach (datum_lighting_corner corner in Dm.each<datum_lighting_corner>(Dm.sub(corners, new_corners))) // Existing corners
            {
                dot = APPLY_CORNER(c, corner);
                if (dot != 0)
                    effect_str[corner] = dot;
                else
                {
                    LAZYREMOVE(ref corner.affecting, this);
                    Dm.remove(ref effect_str, corner);
                }
            }
        }

        DmList gone_corners = (DmList)Dm.sub(effect_str, corners);
        foreach (datum_lighting_corner corner in Dm.each<datum_lighting_corner>(gone_corners))
        {
            REMOVE_CORNER(c, corner);
            LAZYREMOVE(ref corner.affecting, this);
        }
        Dm.remove(ref effect_str, gone_corners);

        applied_lum_r = lum_r;
        applied_lum_g = lum_g;
        applied_lum_b = lum_b;

        UNSETEMPTY(ref this.effect_str);
        return dot;
    }
}
