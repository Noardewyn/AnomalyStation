// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/open/_open.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file, per the per-proc plan of the wave-4 scope (package B): the procs of later milestones are
// Dm.NotTranslated with the milestone named, and overrides of slots whose roots are not translated yet are listed as not
// declared. The W4.0 contracts wrote the var block and the var overrides (rules 10.2: /turf/open's constructor is here, in
// the earliest .dme file that overrides a var of /turf/open, and holds can_atmos_pass from
// modules/atmospherics/environmental/LINDA_system.dm:18 too). The [DmPath] of /turf/open is on
// controllers/subsystem/air.cs, the earliest .dme file that declares /turf/open (rules 2.3).

// tg: code/game/turfs/open/_open.dm:1
public partial class turf_open : turf
{
    // tg: code/game/turfs/open/_open.dm:2-4, modules/atmospherics/environmental/LINDA_system.dm:18
    public turf_open()
    {
        abstract_type = typeof(turf_open);
        layer = LOW_FLOOR_LAYER;
        plane = FLOOR_PLANE;
        can_atmos_pass = ATMOS_PASS_PROC;
    }

    ///negative for faster, positive for slower
    // tg: code/game/turfs/open/_open.dm:6
    public float slowdown = 0;

    // tg: code/game/turfs/open/_open.dm:8
    public object footstep;
    // tg: code/game/turfs/open/_open.dm:9
    public object barefootstep;
    // tg: code/game/turfs/open/_open.dm:10
    public object clawfootstep;
    // tg: code/game/turfs/open/_open.dm:11
    public object heavyfootstep;

    /// Determines the type of damage overlay that will be used for the tile
    // tg: code/game/turfs/open/_open.dm:14
    public object damaged_dmi;
    // tg: code/game/turfs/open/_open.dm:15
    public float broken = FALSE;
    /// Are broken overlays smoothed? if they are we have to change a little bit about how we render them
    // tg: code/game/turfs/open/_open.dm:17
    public float smooth_broken = FALSE;
    // tg: code/game/turfs/open/_open.dm:18
    public float burnt = FALSE;
    /// Are burnt overlays smoothed? if they are we have to change a little bit about how we render them
    // tg: code/game/turfs/open/_open.dm:20
    public float smooth_burnt = FALSE;

    /// Custom destination for mirages
    // tg: code/game/turfs/open/_open.dm:23
    public object destination_z;
    /// Custom destination for mirages
    // tg: code/game/turfs/open/_open.dm:25
    public object destination_x;
    /// Custom destination for mirages
    // tg: code/game/turfs/open/_open.dm:27
    public object destination_y;

    /// If TRUE, humans walking in and out of this turf will leave behind footprints, applied as a generic overlay
    // tg: code/game/turfs/open/_open.dm:30
    public float leave_footprints = FALSE;
    /// Lazylist of all shoe types that have walked over this turf
    // tg: code/game/turfs/open/_open.dm:32  VAR_PRIVATE/list/footprint_shoe_types
    [DmPrivate]
    public DmList footprint_shoe_types;
    /// Lazylist of all species that have walked over this turf
    // tg: code/game/turfs/open/_open.dm:34  VAR_PRIVATE/list/footprint_species_types
    [DmPrivate]
    public DmList footprint_species_types;
    /// All dirs from which footprints have entered this turf
    // tg: code/game/turfs/open/_open.dm:36  VAR_PRIVATE/footprint_entrance_dirs = NONE
    [DmPrivate]
    public float footprint_entrance_dirs = NONE;
    /// All dirs from which footprints have exited this turf
    // tg: code/game/turfs/open/_open.dm:38  VAR_PRIVATE/footprint_exit_dirs = NONE
    [DmPrivate]
    public float footprint_exit_dirs = NONE;

    /// Returns a list of every turf state considered "broken".
    /// Will be randomly chosen if a turf breaks at runtime.
    // tg: code/game/turfs/open/_open.dm:42
    public virtual object broken_states()
    {
        return Dm.list();
    }

    /// Returns a list of every turf state considered "burnt".
    /// Will be randomly chosen if a turf is burnt at runtime.
    // tg: code/game/turfs/open/_open.dm:47
    public virtual object burnt_states()
    {
        return Dm.list();
    }

    // tg: code/game/turfs/open/_open.dm:50
    public override object break_tile()
    {
        if (damaged_dmi == null || Dm.truthy(broken))
            return FALSE;
        broken = TRUE;
        if (Dm.truthy(leave_footprints))
            clear_footprints(); // calls update_appearance() for us
        else
            update_appearance();
        return TRUE;
    }

    // tg: code/game/turfs/open/_open.dm:60
    public override object burn_tile()
    {
        if (damaged_dmi == null || Dm.truthy(burnt))
            return FALSE;
        burnt = TRUE;
        if (Dm.truthy(leave_footprints))
            clear_footprints(); // calls update_appearance() for us
        else
            update_appearance();
        return TRUE;
    }

    // tg: code/game/turfs/open/_open.dm:70
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);
        if (Dm.truthy(broken))
        {
            if (damaged_dmi == null)
                stack_trace(Dm.text("[] has broken set to TRUE but doesn't have a damaged_dmi set.", type), "code/game/turfs/open/_open.dm", 74);
            else
            {
                // M3 P4 (the floors' broken state; the broken_floor mapping helpers break tiles at init)
                mutable_appearance broken_appearance = mutable_appearance(damaged_dmi, (string)pick(broken_states() as DmList)) as mutable_appearance;

                if (Dm.truthy(smoothing_flags) && !Dm.truthy(smooth_broken))
                {
                    matrix translation = Dm.@new<matrix>();
                    translation.Translate(-LARGE_TURF_SMOOTHING_X_OFFSET, -LARGE_TURF_SMOOTHING_Y_OFFSET);
                    broken_appearance.transform = translation;
                }

                Dm.append(ref dot, broken_appearance);
            }
        }
        else if (Dm.truthy(burnt))
        {
            if (damaged_dmi == null)
                stack_trace(Dm.text("[] has burnt set to TRUE but doesn't have a damaged_dmi set.", type), "code/game/turfs/open/_open.dm", 87);
            else
            {
                // M3 P4 (the floors' burnt state; the burnt_floor mapping helpers burn tiles at init)
                DmList burnt_states = this.burnt_states() as DmList;
                mutable_appearance burnt_appearance = null;
                if (Dm.truthy(burnt_states.len))
                    burnt_appearance = mutable_appearance(damaged_dmi, (string)pick(burnt_states)) as mutable_appearance;
                else
                    burnt_appearance = mutable_appearance(damaged_dmi, (string)pick(broken_states() as DmList)) as mutable_appearance;

                if (Dm.truthy(smoothing_flags) && !Dm.truthy(smooth_burnt))
                {
                    matrix translation = Dm.@new<matrix>();
                    translation.Translate(-LARGE_TURF_SMOOTHING_X_OFFSET, -LARGE_TURF_SMOOTHING_Y_OFFSET);
                    burnt_appearance.transform = translation;
                }

                Dm.append(ref dot, burnt_appearance);
            }
        }

        if (Dm.truthy(leave_footprints) && (Dm.truthy(footprint_entrance_dirs) || Dm.truthy(footprint_exit_dirs)))
        {
            string icon_state_to_use = "footprint";
            if (Dm.truthy(LAZYACCESS(footprint_species_types, BODYPART_ID_DIGITIGRADE)))
                icon_state_to_use += "_claw";
            else if (Dm.truthy(LAZYACCESS(footprint_species_types, SPECIES_MONKEY)))
                icon_state_to_use += "_paw";

            foreach (object print_dir in Dm.each(GLOB.cardinals))
            {
                if (Dm.truthy(Dm.bitand(footprint_entrance_dirs, print_dir)))
                {
                    string enter_state = Dm.text("entered-[]-[]", icon_state_to_use, print_dir);
                    image enter_overlay = update_overlays__footprint_cache[enter_state] as image;
                    if (!Dm.truthy(enter_overlay))
                    {
                        // image(icon, "state", dir = x): the text second argument is the icon_state, passed by name (rules 10.9)
                        enter_overlay = image(Dm.resource("icons/effects/footprints.dmi"), icon_state: Dm.text("[]1", icon_state_to_use), dir: print_dir);
                        enter_overlay.color = COLOR_DARK;
                        enter_overlay.alpha *= 0.2f;
                        update_overlays__footprint_cache[enter_state] = enter_overlay;
                    }
                    Dm.append(ref dot, enter_overlay);
                }

                if (Dm.truthy(Dm.bitand(footprint_exit_dirs, print_dir)))
                {
                    string exit_state = Dm.text("exited-[]-[]", icon_state_to_use, print_dir);
                    image exit_overlay = update_overlays__footprint_cache[exit_state] as image;
                    if (!Dm.truthy(exit_overlay))
                    {
                        exit_overlay = image(Dm.resource("icons/effects/footprints.dmi"), icon_state: Dm.text("[]2", icon_state_to_use), dir: print_dir);
                        exit_overlay.color = COLOR_DARK;
                        exit_overlay.alpha *= 0.2f;
                        update_overlays__footprint_cache[exit_state] = exit_overlay;
                    }
                    Dm.append(ref dot, exit_overlay);
                }
            }
        }
        return dot;
    }

    // tg: code/game/turfs/open/_open.dm:104  var/static/list/footprint_cache (rules 10.6)
    private static ref DmList update_overlays__footprint_cache => ref TgState.Current.turf_open__update_overlays__footprint_cache;

    // tg: code/game/turfs/open/_open.dm:133
    public override object examine_descriptor(mob user = null)
    {
        return "floor";
    }

    // tg: code/game/turfs/open/_open.dm:136
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(leave_footprints) && (Dm.truthy(footprint_entrance_dirs) || Dm.truthy(footprint_exit_dirs)) && (Dm.truthy(LAZYLEN(footprint_shoe_types)) || Dm.truthy(LAZYLEN(footprint_species_types))))
        {
            Dm.append(ref dot, "You recognise the footprints as belonging to:");
            foreach (object sole_path in Dm.each(footprint_shoe_types))
            {
                Type sole = sole_path as Type;
                object article = Dm.initial(sole, (obj_item_clothing_shoes p) => p.article);
                if (!Dm.truthy(article))
                    article = Dm.eq(Dm.initial(sole, (obj_item_clothing_shoes p) => p.gender), PLURAL) ? "Some" : "A";
                // [icon2html(initial(sole.icon), user, initial(sole.icon_state))]: the shoe's picture in the chat line (chat icons)
                Dm.Deferred("code/game/turfs/open/_open.dm:143", "examine: icon2html of the shoe, its picture in the chat line (the 3D client's chat is text: chat icons, M10+)");
                Dm.append(ref dot, Dm.text("[] [] <b>[]</b>.", "", article, Dm.initial(sole, (obj_item_clothing_shoes p) => p.name)));
            }

            foreach (object species in Dm.each(footprint_species_types))
            {
                Type species_type = GLOB.species_list[species] as Type;
                Dm.append(ref dot, Dm.text("&bull; Some <b>[] feet</b>.", species_type != null ? format_text(Dm.initial(species_type, (datum_species p) => p.plural_form)) : "unknown"));
            }
        }
        return dot;
    }

    //direction is direction of travel of A
    // tg: code/game/turfs/open/_open.dm:149
    public override object zPassIn(object direction = null)
    {
        if (!Dm.eq(direction, DOWN))
            return FALSE;
        foreach (obj on_us in Dm.each<obj>(contents))
        {
            if (Dm.truthy(Dm.bitand(on_us.obj_flags, BLOCK_Z_IN_DOWN)))
                return FALSE;
        }
        return TRUE;
    }

    //direction is direction of travel of an atom
    // tg: code/game/turfs/open/_open.dm:158
    public override object zPassOut(object direction = null)
    {
        if (!Dm.eq(direction, UP))
            return FALSE;
        foreach (obj on_us in Dm.each<obj>(contents))
        {
            if (Dm.truthy(Dm.bitand(on_us.obj_flags, BLOCK_Z_OUT_UP)))
                return FALSE;
        }
        return TRUE;
    }

    //direction is direction of travel of air
    // tg: code/game/turfs/open/_open.dm:167
    public override object zAirIn(object direction = null, turf source = null)
    {
        return Dm.num(Dm.eq(direction, DOWN));
    }

    //direction is direction of travel of air
    // tg: code/game/turfs/open/_open.dm:171
    public override object zAirOut(object direction = null, turf source = null)
    {
        return Dm.num(Dm.eq(direction, UP));
    }

    // tg: code/game/turfs/open/_open.dm:174
    [DmParams()]
    public override async DmTask<object> update_icon(float? updates = null, params object[] __extra)
    {
        object dot = null;
        dot = await base.update_icon(updates, __extra);
        update_visuals();
        return dot;
    }

    //ATTACK GHOST IGNORING PARENT RETURN VALUE
    // tg: code/game/turfs/open/_open.dm:179
    public override async DmTask<object> attack_ghost(mob_dead_observer user = null)
    {
        object dot = await base.attack_ghost(user);
        if (Dm.truthy(destination_z))
        {
            turf T = locate(Dm.num(destination_x), Dm.num(destination_y), Dm.num(destination_z)) as turf;
            await user.forceMove(T);
        }
        return dot;
    }

    // tg: code/game/turfs/open/_open.dm:185
    public virtual object CanBuildHere()
    {
        if (Dm.truthy(destination_z))
            return FALSE;
        return TRUE;
    }

    // tg: code/game/turfs/open/_open.dm:190
    public override object is_transition_turf()
    {
        if (Dm.truthy(destination_x) || Dm.truthy(destination_y) || Dm.truthy(destination_z))
            return TRUE;
        return null;
    }

    /// Add the passed mob's footprint to the turf with the given movement direction
    // tg: code/game/turfs/open/_open.dm:195
    public virtual void add_footprint(mob_living_carbon_human walker = null, object movement_direction = null)
    {
        Dm.NotTranslated("code/game/turfs/open/_open.dm:195", "add_footprint: humans (body_position, shoes, bodyparts) are translated in M2");
    }

    /// Clear all footprints on the turf
    // tg: code/game/turfs/open/_open.dm:219
    public virtual void clear_footprints()
    {
        footprint_entrance_dirs = NONE;
        footprint_exit_dirs = NONE;
        LAZYNULL(ref footprint_shoe_types);
        LAZYNULL(ref footprint_species_types);
        update_appearance();
    }

    // tg: code/game/turfs/open/_open.dm:226
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        object dot = null;
        dot = await base.Entered(arrived, old_loc, old_locs);
        if (!Dm.truthy(arrived) || this != arrived.loc)
            return dot;

        if (Dm.truthy(old_loc) && Dm.truthy(leave_footprints) && !Dm.truthy(broken) && !Dm.truthy(burnt) && ishuman(arrived))
            add_footprint(arrived as mob_living_carbon_human, get_dir(old_loc, this));

        await transfer_occupant(arrived);
        return dot;
    }

    // tg: code/game/turfs/open/_open.dm:236
    public override async DmTask initialize_occupant(atom_movable occupant = null)
    {
        await base.initialize_occupant(occupant);
        await transfer_occupant(occupant);
    }

    /// Moves an occupant through a configured connection to another z-level.
    // tg: code/game/turfs/open/_open.dm:241
    public virtual async DmTask transfer_occupant(atom_movable occupant = null)
    {
        if (!Dm.truthy(occupant) || occupant.loc != this)
            return;
        if (!Dm.truthy(destination_z) || !Dm.truthy(destination_x) || !Dm.truthy(destination_y) || Dm.truthy(occupant.pulledby) || Dm.truthy(occupant.currently_z_moving))
            return;

        if (SSatoms.initialized == INITIALIZATION_INNEW_MAPLOAD) // we don't want to be transitioning atoms to another z-level while we are still in mapload
            return;

        object tx = destination_x;
        object ty = destination_y;
        turf DT = locate(Dm.num(tx), Dm.num(ty), Dm.num(destination_z));
        float itercount = 0;
        while (Dm.truthy(DT.density) || DT.loc is area_shuttle) // Extend towards the center of the map, trying to look for a better place to arrive
        {
            if (itercount++ >= 100)
            {
                log_game(Dm.text("SPACE Z-TRANSIT ERROR: Could not find a safe place to land [] within 100 iterations.", occupant));
                break;
            }
            if (Dm.lt(tx, 128))
                Dm.inc(ref tx);
            else
                Dm.dec(ref tx);
            if (Dm.lt(ty, 128))
                Dm.inc(ref ty);
            else
                Dm.dec(ref ty);
            DT = locate(Dm.num(tx), Dm.num(ty), Dm.num(destination_z));
        }

        await occupant.zMove(null, DT, ZMOVE_ALLOW_BUCKLED);

        atom_movable current_pull = occupant.pulling;
        while (Dm.truthy(current_pull))
        {
            turf target_turf = (get_step(current_pull.pulledby.loc, REVERSE_DIR(current_pull.pulledby.dir)) is var or__1 && Dm.truthy(or__1) ? or__1 : (object)current_pull.pulledby.loc) as turf;
            await current_pull.zMove(null, target_turf, ZMOVE_ALLOW_BUCKLED);
            current_pull = current_pull.pulling;
        }
    }

    // tg: code/game/turfs/open/_open.dm:276
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = null;
        dot = await base.Exited(gone, direction);
        if (Dm.truthy(gone) && Dm.truthy(direction) && Dm.truthy(leave_footprints) && !Dm.truthy(broken) && !Dm.truthy(burnt) && isturf(gone.loc) && ishuman(gone))
            add_footprint(gone as mob_living_carbon_human, direction);
        return dot;
    }

    /**
     * Replace an open turf with another open turf while avoiding the pitfall of replacing plating with a floor tile, leaving a hole underneath.
     * This replaces the current turf if it is plating and is passed plating, is tile and is passed tile.
     * It places the new turf on top of itself if it is plating and is passed a tile.
     * It also replaces the turf if it is tile and is passed plating, essentially destroying the over turf.
     * Flags argument is passed directly to ChangeTurf or PlaceOnTop
     */
    // tg: code/game/turfs/open/_open.dm:288
    // (new_floor_path is a Type, not tg's /turf/open: its callers pass the floor's typepath, rules 3.2 V3; Appendix A; the M7
    // delamination slice: the dimensional anomaly's themes)
    public virtual async DmTask replace_floor(Type new_floor_path = null, object flags = null)
    {
        if (!Dm.truthy(overfloor_placed) && Dm.truthy(Dm.initial(new_floor_path, (turf_open p) => p.overfloor_placed)))
        {
            await place_on_top(new_floor_path, flags: flags);
            return;
        }
        await ChangeTurf(new_floor_path, flags: flags);
    }
}

// tg: code/game/turfs/open/_open.dm:294
[DmPath("/turf/open/indestructible")]
public partial class turf_open_indestructible : turf_open
{
    // tg: code/game/turfs/open/_open.dm:295-303
    public turf_open_indestructible()
    {
        name = "floor";
        desc = "The floor you walk on. It looks near-impervious to damage.";
        icon = Dm.resource("icons/turf/floors.dmi");
        icon_state = "floor";
        footstep = FOOTSTEP_FLOOR;
        barefootstep = FOOTSTEP_HARD_BAREFOOT;
        clawfootstep = FOOTSTEP_HARD_CLAW;
        heavyfootstep = FOOTSTEP_GENERIC_HEAVY;
        tiled_turf = TRUE;
    }

    // tg: code/game/turfs/open/_open.dm:305
    public override async DmTask<object> Melt()
    {
        to_be_destroyed = FALSE;
        return this;
    }

    // tg: code/game/turfs/open/_open.dm:309  singularity_act(): at the file's end (M5x slice 5).
    // tg: code/game/turfs/open/_open.dm:312  TerraformTurf(path, new_baseturf, flags, defer_change, ignore_air): not
    // declared, its root /turf/proc/TerraformTurf (game/turfs/change_turf.dm) is translated with ChangeTurf (M3, M5).
}

// tg: code/game/turfs/open/_open.dm:316
[DmPath("/turf/open/indestructible/large")]
public partial class turf_open_indestructible_large : turf_open_indestructible
{
    // tg: code/game/turfs/open/_open.dm:317
    public turf_open_indestructible_large()
    {
        icon_state = "floor_large";
    }
}

// tg: code/game/turfs/open/_open.dm:319
[DmPath("/turf/open/indestructible/white")]
public partial class turf_open_indestructible_white : turf_open_indestructible
{
    // tg: code/game/turfs/open/_open.dm:320
    public turf_open_indestructible_white()
    {
        icon_state = "white";
    }
}

// tg: code/game/turfs/open/_open.dm:322
[DmPath("/turf/open/indestructible/white/smooth_large")]
public partial class turf_open_indestructible_white_smooth_large : turf_open_indestructible_white
{
    // tg: code/game/turfs/open/_open.dm:323
    public turf_open_indestructible_white_smooth_large()
    {
        icon_state = "white_large";
    }
}

// tg: code/game/turfs/open/_open.dm:325
[DmPath("/turf/open/indestructible/white/textured")]
public partial class turf_open_indestructible_white_textured : turf_open_indestructible_white
{
    // tg: code/game/turfs/open/_open.dm:326
    public turf_open_indestructible_white_textured()
    {
        icon_state = "textured_white";
    }
}

// tg: code/game/turfs/open/_open.dm:328
[DmPath("/turf/open/indestructible/dark")]
public partial class turf_open_indestructible_dark : turf_open_indestructible
{
    // tg: code/game/turfs/open/_open.dm:329
    public turf_open_indestructible_dark()
    {
        icon_state = "darkfull";
    }
}

// tg: code/game/turfs/open/_open.dm:331
[DmPath("/turf/open/indestructible/dark/textured")]
public partial class turf_open_indestructible_dark_textured : turf_open_indestructible_dark
{
    // tg: code/game/turfs/open/_open.dm:332
    public turf_open_indestructible_dark_textured()
    {
        icon_state = "textured_dark";
    }
}

// tg: code/game/turfs/open/_open.dm:334
[DmPath("/turf/open/indestructible/dark/smooth_large")]
public partial class turf_open_indestructible_dark_smooth_large : turf_open_indestructible_dark
{
    // tg: code/game/turfs/open/_open.dm:335
    public turf_open_indestructible_dark_smooth_large()
    {
        icon_state = "dark_large";
    }
}

// tg: code/game/turfs/open/_open.dm:337
[DmPath("/turf/open/indestructible/light")]
public partial class turf_open_indestructible_light : turf_open_indestructible
{
    // tg: code/game/turfs/open/_open.dm:338-341
    public turf_open_indestructible_light()
    {
        icon_state = "light_on-1";
        light_range = 3;
        light_color = LIGHT_COLOR_CYAN;
        light_on = TRUE;
    }
}

// tg: code/game/turfs/open/_open.dm:343
[DmPath("/turf/open/indestructible/permalube")]
public partial class turf_open_indestructible_permalube : turf_open_indestructible
{
    // tg: code/game/turfs/open/_open.dm:344
    public turf_open_indestructible_permalube()
    {
        icon_state = "darkfull";
    }

    // tg: code/game/turfs/open/_open.dm:346
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        Dm.NotTranslated("code/game/turfs/open/_open.dm:348", "Initialize: AddComponent(/datum/component/wet_floor, ...): the permanently wet permalube and honk floors (not on MetaStation; M10+)");
        return dot;
    }
}

// tg: code/game/turfs/open/_open.dm:350
[DmPath("/turf/open/indestructible/honk")]
public partial class turf_open_indestructible_honk : turf_open_indestructible
{
    // tg: code/game/turfs/open/_open.dm:351-356
    public turf_open_indestructible_honk()
    {
        name = "bananium floor";
        icon_state = "bananium";
        footstep = null;
        barefootstep = null;
        clawfootstep = null;
        heavyfootstep = null;
    }

    // tg: code/game/turfs/open/_open.dm:357
    public object sound = Dm.resource("sound/effects/footstep/clownstep1.ogg");

    // tg: code/game/turfs/open/_open.dm:359
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        Dm.NotTranslated("code/game/turfs/open/_open.dm:361", "Initialize: AddComponent(/datum/component/wet_floor, ...): the permanently wet permalube and honk floors (not on MetaStation; M10+)");
        return dot;
    }

    // tg: code/game/turfs/open/_open.dm:363
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_loc = null, DmList old_locs = null)
    {
        object dot = null;
        dot = await base.Entered(arrived, old_loc, old_locs);
        if (ismob(arrived))
            playsound(this, sound, 50, TRUE); // tg: code/game/turfs/open/_open.dm:366
        return dot;
    }

    // tg: code/game/turfs/open/_open.dm:368
    public override async DmTask initialize_occupant(atom_movable occupant = null)
    {
        await base.initialize_occupant(occupant);
        if (ismob(occupant))
            playsound(this, sound, 50, TRUE); // tg: code/game/turfs/open/_open.dm:371
    }
}

// tg: code/game/turfs/open/_open.dm:373
[DmPath("/turf/open/indestructible/necropolis")]
public partial class turf_open_indestructible_necropolis : turf_open_indestructible
{
    // tg: code/game/turfs/open/_open.dm:374-384
    public turf_open_indestructible_necropolis()
    {
        name = "necropolis floor";
        desc = "It's regarding you suspiciously.";
        icon = Dm.resource("icons/turf/floors.dmi");
        icon_state = "necro1";
        baseturfs = typeof(turf_open_indestructible_necropolis);
        initial_gas_mix = LAVALAND_DEFAULT_ATMOS;
        footstep = FOOTSTEP_LAVA;
        barefootstep = FOOTSTEP_LAVA;
        clawfootstep = FOOTSTEP_LAVA;
        heavyfootstep = FOOTSTEP_LAVA;
        tiled_turf = FALSE;
    }

    // tg: code/game/turfs/open/_open.dm:386
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        if (prob(12))
            icon_state = Dm.text("necro[]", rand(2, 3));
        return dot;
    }
}

// tg: code/game/turfs/open/_open.dm:391
[DmPath("/turf/open/indestructible/necropolis/air")]
public partial class turf_open_indestructible_necropolis_air : turf_open_indestructible_necropolis
{
    // tg: code/game/turfs/open/_open.dm:392
    public turf_open_indestructible_necropolis_air()
    {
        initial_gas_mix = OPENTURF_DEFAULT_ATMOS;
    }
}

// tg: code/game/turfs/open/_open.dm:394
[DmPath("/turf/open/indestructible/boss")]
public partial class turf_open_indestructible_boss : turf_open_indestructible //you put stone tiles on this and use it as a base
{
    // tg: code/game/turfs/open/_open.dm:395-400
    public turf_open_indestructible_boss()
    {
        name = "necropolis floor";
        icon = Dm.resource("icons/turf/boss_floors.dmi");
        icon_state = "boss";
        baseturfs = typeof(turf_open_indestructible_boss);
        planetary_atmos = TRUE;
        initial_gas_mix = LAVALAND_DEFAULT_ATMOS;
    }
}

// tg: code/game/turfs/open/_open.dm:402
[DmPath("/turf/open/indestructible/boss/air")]
public partial class turf_open_indestructible_boss_air : turf_open_indestructible_boss
{
    // tg: code/game/turfs/open/_open.dm:403
    public turf_open_indestructible_boss_air()
    {
        initial_gas_mix = OPENTURF_DEFAULT_ATMOS;
    }
}

// tg: code/game/turfs/open/_open.dm:405
[DmPath("/turf/open/indestructible/hierophant")]
public partial class turf_open_indestructible_hierophant : turf_open_indestructible
{
    // tg: code/game/turfs/open/_open.dm:406-416
    public turf_open_indestructible_hierophant()
    {
        name = "palestone floor";
        desc = "A tiled floor made out of an odd pale stone.";
        icon = Dm.resource("icons/turf/floors/hierophant_floor.dmi");
        icon_state = "hierophant_floor-255";
        base_icon_state = "hierophant_floor";
        planetary_atmos = TRUE;
        initial_gas_mix = LAVALAND_DEFAULT_ATMOS;
        baseturfs = typeof(turf_open_indestructible_hierophant);
        smoothing_flags = SMOOTH_BITMASK;
        smoothing_groups = SMOOTH_GROUP_TURF_OPEN + SMOOTH_GROUP_HIEROPHANT;
        canSmoothWith = SMOOTH_GROUP_HIEROPHANT;
    }

    /// Icon for the emissive overlay
    // tg: code/game/turfs/open/_open.dm:418
    public object emissive_icon = Dm.resource("icons/turf/floors/hierophant_floor_e.dmi");

    // tg: code/game/turfs/open/_open.dm:420  set_smoothed_icon_state(new_junction): not declared, its root
    // /atom/proc/set_smoothed_icon_state (__HELPERS/icon_smoothing.dm) is translated with icon smoothing (M3).

    // tg: code/game/turfs/open/_open.dm:424
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = null;
        dot = await base.update_overlays(__extra);
        Dm.NotTranslated("code/game/turfs/open/_open.dm:426", "update_overlays: emissive_appearance (a /mutable_appearance, lead decision D2) is translated with light (M3)");
        return dot;
    }

    // tg: code/game/turfs/open/_open.dm:436  get_smooth_underlay_icon(mutable_appearance/underlay_appearance, ...): not
    // declared, as /turf/proc/get_smooth_underlay_icon (game/turfs/turf.cs: /mutable_appearance is not ported, D2).
}

// tg: code/game/turfs/open/_open.dm:428
[DmPath("/turf/open/indestructible/hierophant/two")]
public partial class turf_open_indestructible_hierophant_two : turf_open_indestructible_hierophant
{
    // tg: code/game/turfs/open/_open.dm:429-434
    public turf_open_indestructible_hierophant_two()
    {
        name = "runic palestone floor";
        desc = "A tiled floor made out of an odd pale stone, inscribed with odd runes.";
        icon = Dm.resource("icons/turf/floors/hierophant_floor_alt.dmi");
        icon_state = "hierophant_floor_alt-255";
        base_icon_state = "hierophant_floor_alt";
        emissive_icon = Dm.resource("icons/turf/floors/hierophant_floor_alt_e.dmi");
    }
}

// tg: code/game/turfs/open/_open.dm:439
[DmPath("/turf/open/indestructible/paper")]
public partial class turf_open_indestructible_paper : turf_open_indestructible
{
    // tg: code/game/turfs/open/_open.dm:440-447
    public turf_open_indestructible_paper()
    {
        name = "notebook floor";
        desc = "A floor made of invulnerable notebook paper.";
        icon_state = "paperfloor";
        footstep = null;
        barefootstep = null;
        clawfootstep = null;
        heavyfootstep = null;
        tiled_turf = FALSE;
    }
}

// tg: code/game/turfs/open/_open.dm:449
[DmPath("/turf/open/indestructible/binary")]
public partial class turf_open_indestructible_binary : turf_open_indestructible
{
    // tg: code/game/turfs/open/_open.dm:450-457
    public turf_open_indestructible_binary()
    {
        name = "tear in the fabric of reality";
        can_atmos_pass = ATMOS_PASS_NO;
        baseturfs = typeof(turf_open_indestructible_binary);
        icon_state = "binary";
        footstep = null;
        barefootstep = null;
        clawfootstep = null;
        heavyfootstep = null;
    }
}

// tg: code/game/turfs/open/_open.dm:459
[DmPath("/turf/open/indestructible/airblock")]
public partial class turf_open_indestructible_airblock : turf_open_indestructible
{
    // tg: code/game/turfs/open/_open.dm:460-463
    public turf_open_indestructible_airblock()
    {
        icon_state = "bluespace";
        blocks_air = TRUE;
        init_air = FALSE;
        baseturfs = typeof(turf_open_indestructible_airblock);
    }
}

// tg: code/game/turfs/open/_open.dm:465
[DmPath("/turf/open/indestructible/meat")]
public partial class turf_open_indestructible_meat : turf_open_indestructible
{
    // tg: code/game/turfs/open/_open.dm:466-472
    public turf_open_indestructible_meat()
    {
        icon_state = "meat";
        footstep = FOOTSTEP_MEAT;
        barefootstep = FOOTSTEP_MEAT;
        clawfootstep = FOOTSTEP_MEAT;
        heavyfootstep = FOOTSTEP_MEAT;
        initial_gas_mix = OPENTURF_DEFAULT_ATMOS;
        baseturfs = typeof(turf_open_indestructible_meat);
    }
}

// tg: code/game/turfs/open/_open.dm:474
[DmPath("/turf/open/indestructible/meat/airless")]
public partial class turf_open_indestructible_meat_airless : turf_open_indestructible_meat
{
    // tg: code/game/turfs/open/_open.dm:475
    public turf_open_indestructible_meat_airless()
    {
        initial_gas_mix = AIRLESS_ATMOS;
    }
}

// tg: code/game/turfs/open/_open.dm:477
[DmPath("/turf/open/indestructible/vault")]
public partial class turf_open_indestructible_vault : turf_open_indestructible
{
    // tg: code/game/turfs/open/_open.dm:478
    public turf_open_indestructible_vault()
    {
        icon_state = "rockvault";
    }
}

// tg: code/game/turfs/open/_open.dm:480
[DmPath("/turf/open/indestructible/plating")]
public partial class turf_open_indestructible_plating : turf_open_indestructible
{
    // tg: code/game/turfs/open/_open.dm:481-486
    public turf_open_indestructible_plating()
    {
        name = "plating";
        icon_state = "plating";
        desc = "The attachment points are all bent to uselessness, looks nigh-impervious to damage.";
        overfloor_placed = FALSE;
        underfloor_accessibility = UNDERFLOOR_INTERACTABLE;
        footstep = FOOTSTEP_PLATING;
    }
}

// tg: code/game/turfs/open/_open.dm:488
[DmPath("/turf/open/indestructible/plating/airless")]
public partial class turf_open_indestructible_plating_airless : turf_open_indestructible_plating
{
    // tg: code/game/turfs/open/_open.dm:489
    public turf_open_indestructible_plating_airless()
    {
        initial_gas_mix = AIRLESS_ATMOS;
    }
}

// tg: code/game/turfs/open/_open.dm:491-501  /turf/open/indestructible/kitchen, /rockyground and /stone: not declared;
// their defaults are compile-time reads of floor types that the map milestone translates
// (/turf/open/floor/iron/kitchen::icon_state, /turf/open/misc/ashplanet/rocky::icon, ::icon_state and ::name,
// /turf/open/floor/stone::icon_state and ::name; rules 10.3), M3.

// tg: code/game/turfs/open/_open.dm:503  (interleaved types, rules 1.9)
public partial class turf_open : turf
{
    // tg: code/game/turfs/open/_open.dm:503
    public override void Initalize_Atmos(object time = null)
    {
        excited = FALSE;
        update_visuals();

        current_cycle = Dm.num(time);
        init_immediate_calculate_adjacent_turfs();
    }

    // tg: code/game/turfs/open/_open.dm:510
    public override object GetHeatCapacity()
    {
        object dot = air.heat_capacity();
        return dot;
    }

    // tg: code/game/turfs/open/_open.dm:513
    public override object GetTemperature()
    {
        object dot = air.temperature;
        return dot;
    }

    // tg: code/game/turfs/open/_open.dm:516
    public override void TakeTemperature(object temp = null)
    {
        air.temperature += Dm.num(temp);
        air_update_turf(FALSE, FALSE);
    }

    // tg: code/game/turfs/open/_open.dm:520
    public virtual async DmTask<object> freeze_turf()
    {
        foreach (obj I in Dm.each<obj>(contents))
        {
            if (!HAS_TRAIT(I, TRAIT_FROZEN) && !Dm.truthy(Dm.bitand(I.resistance_flags, FREEZE_PROOF)))
                await I.AddElement(typeof(datum_element_frozen));
        }

        foreach (mob_living L in Dm.each<mob_living>(contents))
        {
            if (L.bodytemperature <= 50 && !HAS_TRAIT(L, TRAIT_RESISTCOLD))
                await L.apply_status_effect(typeof(datum_status_effect_freon));
        }
        await MakeSlippery(TURF_WET_PERMAFROST, 10 * SECONDS);
        return TRUE;
    }

    // tg: code/game/turfs/open/_open.dm:531
    public virtual async DmTask<object> water_vapor_gas_act()
    {
        await MakeSlippery(TURF_WET_WATER, min_wet_time: 100, wet_time_to_add: 50);

        foreach (mob_living_basic_slime M in Dm.each<mob_living_basic_slime>(contents))
        {
            // M.apply_water(): slimes (xenobiology, a later milestone)
            Dm.NotTranslated("code/game/turfs/open/_open.dm:535", "water_vapor_gas_act: a slime's apply_water (slimes, a later milestone)");
        }

        // wash(CLEAN_WASH | CLEAN_RAD, TRUE): the cleaning of the tile and everything on it (the cleaning code, M5); nothing in
        // M4 reads what it cleans.
        Dm.Deferred("code/game/turfs/open/_open.dm:537", "water_vapor_gas_act: wash(CLEAN_WASH | CLEAN_RAD, TRUE) (the cleaning code, M5)");
        return TRUE;
    }

    // tg: code/game/turfs/open/_open.dm:540  (M4b slice 3: slipping; the slot names the slipper slipped_carbon, tg's turf
    // names it slipper)
    public override async DmTask<object> handle_slip(mob_living_carbon slipped_carbon = null, object knockdown_amount = null, atom slipping_object = null, object lube = null, object paralyze = null, object daze = null, object force_drop = null)
    {
        mob_living_carbon slipper = slipped_carbon;
        atom slippable = slipping_object;
        object paralyze_amount = paralyze;
        object daze_amount = daze;
        if (!Dm.truthy(Dm.bitand(lube, SLIP_IN_NOGRAV)))
        {
            if (Dm.truthy(Dm.bitand(slipper.movement_type, MOVETYPES_NOT_TOUCHING_GROUND)))
                return FALSE;
            if (!Dm.truthy(has_gravity(this)))
                return FALSE;
        }

        float slide_distance = 4;
        if (Dm.truthy(Dm.bitand(lube, SLIDE_ICE)))
        {
            // Ice slides only go 1 tile, this is so you will slip across ice until you reach a non-slip tile
            slide_distance = 1;
        }
        else if (HAS_TRAIT(slipper, TRAIT_CURSED) && !Dm.truthy(Dm.bitand(lube, WEAK_SLIDE)))
        {
            // When cursed, all slips send you flying
            lube = Dm.bitor(lube, SLIDE);
            slide_distance = rand(5, 9);
        }
        else if (HAS_TRAIT(slipper, TRAIT_NO_SLIP_SLIDE))
        {
            // Stops sliding
            slide_distance = 0;
        }
        else if (Dm.truthy(Dm.bitand(lube, WEAK_SLIDE)))
            slide_distance = rand(1, 2);

        obj buckled_obj = null;
        if (Dm.truthy(slipper.buckled))
        {
            if (!Dm.truthy(Dm.bitand(lube, GALOSHES_DONT_HELP))) //can't slip while buckled unless it's lube.
                return FALSE;
            buckled_obj = slipper.buckled as obj;
        }
        else
        {
            if (!Dm.truthy(Dm.bitand(lube, SLIP_WHEN_CRAWLING)) && (slipper.body_position == LYING_DOWN || !Dm.truthy(Dm.bitand(slipper.status_flags, CANKNOCKDOWN)))) // can't slip unbuckled mob if they're lying or can't fall.
                return FALSE;
            if (Dm.eq(slipper.move_intent, MOVE_INTENT_WALK) && Dm.truthy(Dm.bitand(lube, NO_SLIP_WHEN_WALKING)))
                return FALSE;
        }

        if (!Dm.truthy(Dm.bitand(lube, SLIDE_ICE)))
        {
            // Ice slides are intended to be combo'd so don't give the feedback
            to_chat(slipper, span_notice(Dm.truthy(slippable) ? I18n.T("You slipped on \\the []!", slippable) : I18n.T("You slipped!")));
            playsound(slipper.loc, Dm.resource("sound/misc/slip.ogg"), 50, TRUE, -3); // tg: code/game/turfs/open/_open.dm:575
        }

        if (Dm.truthy(slipper._listen_lookup?[COMSIG_ON_CARBON_SLIP]))
            slipper._SendSignal(COMSIG_ON_CARBON_SLIP, Dm.list(slipper));
        if (Dm.truthy(force_drop) && iscarbon(slipper)) //carbon specific behavior that living doesn't have
        {
            mob_living_carbon carbon = slipper;
            foreach (obj_item item in Dm.each<obj_item>(slipper.held_items))
                await carbon.accident(item);
        }

        float olddir = slipper.dir;
        slipper.moving_diagonally = 0; //If this was part of diagonal move slipping will stop it.
        if (Dm.truthy(Dm.bitand(lube, SLIDE_ICE)))
        {
            // They need to be kept upright to maintain the combo effect (So don't knockdown)
            await slipper.Immobilize(1 * SECONDS);
            await slipper.incapacitate(1 * SECONDS);
        }
        else if (Dm.truthy(Dm.bitand(lube, WEAK_SLIDE)))
        {
            await slipper.adjust_timed_status_effect(STAGGERED_SLOWDOWN_LENGTH, typeof(datum_status_effect_staggered), 10 * SECONDS); // adjust_staggered_up_to (__DEFINES/status_effects.dm:157), inline
            await slipper.stop_pulling();
        }
        else
        {
            await slipper.Paralyze(paralyze_amount);
            await slipper.Knockdown(knockdown_amount, daze_amount: Dm.num(daze_amount));
        }

        if (buckled_obj != null && !ismob(buckled_obj))
        {
            await buckled_obj.unbuckle_mob(slipper);
            // This is added onto the end so they slip "out of their chair" (one tile)
            lube = Dm.bitor(lube, SLIDE_ICE);
            slide_distance = 1;
        }

        if (slide_distance != 0)
        {
            turf target = get_ranged_target_turf(slipper, olddir, slide_distance) as turf;
            if (Dm.truthy(Dm.bitand(lube, Dm.bitor(SLIDE, WEAK_SLIDE))))
                await slipper.AddComponent(typeof(datum_component_force_move), target, TRUE);
            else if (Dm.truthy(Dm.bitand(lube, SLIDE_ICE)))
                await slipper.AddComponent(typeof(datum_component_force_move), target, FALSE);//spinning would be bad for ice, fucks up the next dir
        }
        return TRUE;
    }

    // tg: code/game/turfs/open/_open.dm:610
    public virtual async DmTask MakeSlippery(float? wet_setting = null, float? min_wet_time = null, float? wet_time_to_add = null, float? max_wet_time = null, float? permanent = null, float? should_display_overlay = null)
    {
        wet_setting ??= TURF_WET_WATER;
        min_wet_time ??= 0;
        wet_time_to_add ??= 0;
        max_wet_time ??= MAXIMUM_WET_TIME;
        permanent ??= FALSE;
        should_display_overlay ??= TRUE;
        await AddComponent(typeof(datum_component_wet_floor), wet_setting, min_wet_time, wet_time_to_add, max_wet_time, permanent, should_display_overlay);
    }

    // tg: code/game/turfs/open/_open.dm:613
    public virtual void MakeDry(float? wet_setting = null, float? immediate = null, float? amount = null)
    {
        wet_setting ??= TURF_WET_WATER;
        immediate ??= FALSE;
        amount ??= INFINITY;
        if (Dm.truthy(this._listen_lookup?[COMSIG_TURF_MAKE_DRY]))
            this._SendSignal(COMSIG_TURF_MAKE_DRY, Dm.list(this, wet_setting, immediate, amount));
    }

    // tg: code/game/turfs/open/_open.dm:616
    public override object get_dumping_location()
    {
        return this;
    }

    // tg: code/game/turfs/open/_open.dm:619
    public virtual void ClearWet()//Nuclear option of immediately removing slipperiness from the tile instead of the natural drying over time
    {
        qdel(GetComponent(typeof(datum_component_wet_floor)));
    }

    /// Builds with rods. This doesn't exist to be overridden, just to remove duplicate logic for turfs that want
    /// To support floor tile creation
    /// I'd make it a component, but one of these things is space. So no.
    // tg: code/game/turfs/open/_open.dm:625
    public virtual async DmTask build_with_rods(obj_item_stack_rods used_rods = null, mob user = null)
    {
        obj_structure_lattice catwalk_bait = Dm.locate<obj_structure_lattice>(this);
        obj_structure_lattice_catwalk existing_catwalk = Dm.locate<obj_structure_lattice_catwalk>(this);
        if (Dm.truthy(existing_catwalk))
        {
            to_chat(user, span_warning(I18n.T("There is already a catwalk here!")));
            return;
        }

        if (Dm.truthy(catwalk_bait))
        {
            if (Dm.truthy(await used_rods.use(1)))
            {
                to_chat(user, span_notice(I18n.T("You construct a catwalk.")));
                playsound(this, Dm.resource("sound/items/weapons/genhit.ogg"), 50, TRUE);
                await catwalk_bait.replace_with_catwalk();
            }
            else
                to_chat(user, span_warning(I18n.T("You need two rods to build a catwalk!")));
            return;
        }

        if (Dm.truthy(await used_rods.use(1)))
        {
            to_chat(user, span_notice(I18n.T("You construct a lattice.")));
            playsound(this, Dm.resource("sound/items/weapons/genhit.ogg"), 50, TRUE);
            obj_structure_lattice new_lattice = Dm.@new<obj_structure_lattice>(this);
            if (istype(used_rods, typeof(obj_item_stack_rods_shuttle)) && !istype(loc, typeof(area_shuttle)))
                Dm.NotTranslated("code/game/turfs/open/_open.dm:647", "build_with_rods: new_lattice.AddElement(/datum/element/shuttle_construction_lattice) (shuttle construction, M10+)");
        }
        else
            to_chat(user, span_warning(I18n.T("You need one rod to build a lattice.")));
    }

    /// Very similar to build_with_rods, this exists to allow consistent behavior between different types in terms of how
    /// Building floors works
    // tg: code/game/turfs/open/_open.dm:652
    public virtual async DmTask build_with_floor_tiles(obj_item_stack_tile_iron used_tiles = null, object user = null)
    {
        obj_structure_lattice lattice = Dm.locate<obj_structure_lattice>(this);
        if (!Dm.truthy(has_valid_support()) && !Dm.truthy(lattice))
        {
            balloon_alert(user as mob, I18n.T("needs support, place rods!"));
            return;
        }
        if (!Dm.truthy(await used_tiles.use(1)))
        {
            balloon_alert(user as mob, I18n.T("need a floor tile to build!"));
            return;
        }

        playsound(this, Dm.resource("sound/items/weapons/genhit.ogg"), 50, TRUE);
        turf_open_floor_plating new_plating = await place_on_top(typeof(turf_open_floor_plating), flags: CHANGETURF_INHERIT_AIR) as turf_open_floor_plating;
        if (istype(loc ?? new_plating?.loc, typeof(area_shuttle))) // TURF-IDENTITY: tg's loc is the new plating's area
            Dm.NotTranslated("code/game/turfs/open/_open.dm:664", "build_with_floor_tiles: new_plating.insert_baseturf(turf_type = /turf/baseturf_skipover/shuttle) (shuttle construction, M10+)");
        if (Dm.truthy(lattice))
            qdel(lattice);
        else
            new_plating.lattice_underneath = FALSE;
    }

    // tg: code/game/turfs/open/_open.dm:670
    public virtual object has_valid_support()
    {
        foreach (object direction in Dm.each(GLOB.cardinals))
        {
            if (istype(get_step(this, Dm.num(direction)), typeof(turf_open_floor)))
                return TRUE;
        }
        return FALSE;
    }

    /// Very similar to build_with_rods, this exists to allow consistent behavior between different types in terms of how
    /// Building floors works
    // tg: code/game/turfs/open/_open.dm:678
    public virtual async DmTask build_with_transport_tiles(obj_item_stack_thermoplastic used_tiles = null, object user = null)
    {
        obj_structure_transport_linear platform = locate_transport<obj_structure_transport_linear>(this);
        if (platform == null)
        {
            balloon_alert(user as mob, I18n.T("no tram base!"));
            return;
        }
        if (!Dm.truthy(await used_tiles.use(1)))
        {
            balloon_alert(user as mob, I18n.T("no tile!"));
            return;
        }

        playsound(this, Dm.resource("sound/items/weapons/genhit.ogg"), 50, TRUE);
        Dm.@new(used_tiles.tile_type, this);
    }

    // tg: code/game/turfs/open/_open.dm:690  apply_main_material_effects(datum/material/main_material, amount, multiplier):
    // not declared, its root /atom/proc/apply_main_material_effects (game/atom/atom_materials.dm) is translated with
    // materials (M5).

    /// Very similar to build_with_rods, this exists to allow building transport/tram girders on openspace
    // tg: code/game/turfs/open/_open.dm:700
    public virtual async DmTask build_with_titanium(obj_item_stack_sheet_mineral_titanium used_stack = null, object user = null)
    {
        obj_structure_transport_linear platform = locate_transport<obj_structure_transport_linear>(this);
        if (platform == null)
        {
            to_chat(user, span_warning(I18n.T("There is no transport frame to attach the anchor!")));
            return;
        }
        if (!Dm.truthy(await used_stack.use(2)))
        {
            balloon_alert(user as mob, I18n.T("not enough titanium!"));
            return;
        }

        playsound(this, Dm.resource("sound/items/weapons/genhit.ogg"), 50, TRUE);
        Dm.@new<obj_structure_girder_tram>(this);
    }

    // tg: code/game/turfs/open/_open.dm:690
    public override async DmTask apply_main_material_effects(datum_material main_material = null, object amount = null, object multiplier = null)
    {
        await base.apply_main_material_effects(main_material, amount, multiplier);
        if (!Dm.truthy(main_material.turf_sound_override))
            return;
        footstep = main_material.turf_sound_override;
        barefootstep = Dm.add(main_material.turf_sound_override, "barefoot");
        clawfootstep = Dm.add(main_material.turf_sound_override, "claw");
        heavyfootstep = FOOTSTEP_GENERIC_HEAVY;
    }
}

public sealed partial class TgState
{
    // tg: code/game/turfs/open/_open.dm:104  /turf/open/update_overlays: var/static/list/footprint_cache = list()
    internal DmList turf_open__update_overlays__footprint_cache = Dm.list();
}

// tg: code/game/turfs/open/_open.dm:309 (interleaved types, rules 1.9; M5x slice 5)
public partial class turf_open_indestructible : turf_open
{
    // tg: code/game/turfs/open/_open.dm:309
    public override async DmTask<object> singularity_act()
    {
        return null;
    }
}
