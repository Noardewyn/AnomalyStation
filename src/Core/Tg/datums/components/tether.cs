// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/tether.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MO2 (D333): the file whole, with the MOD emergency tether module and the tether anchor (modules_engineering.dm). A tether holds
// its two ends within its length: a move that would overstretch it pulls the other end along when it can, a line it would snag on is
// walked around, and it snaps when broken out of. Clicking its beam shortens it, right-clicking lengthens it, ctrl-clicking cuts it.
// The var block is generated (D111).

/// Creates a tether between two objects that limits movement range. Tether requires LOS and can be adjusted by left/right clicking its
// tg: code/datums/components/tether.dm:2
public partial class datum_component_tether : datum_component
{
    // tg: code/datums/components/tether.dm:25
    [DmParams("tether_target", "max_dist", "tether_name", "embed_target", "start_distance", "parent_module", "tether_trait_source", "no_target_trait")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        atom tether_target = Dm.arg(__extra, 0) as atom;
        object max_dist = Dm.arg(__extra, 1) ?? 7;
        object tether_name = Dm.arg(__extra, 2);
        atom embed_target = Dm.arg(__extra, 3) as atom;
        object start_distance = Dm.arg(__extra, 4);
        object parent_module = Dm.arg(__extra, 5);
        object tether_trait_source = Dm.arg(__extra, 6);
        object no_target_trait = Dm.arg(__extra, 7) ?? FALSE;
        if (!ismovable(parent) || !istype(tether_target, typeof(atom)) || tether_target.loc == null)
            return COMPONENT_INCOMPATIBLE;
        if (isatom(tether_trait_source))
        {
            stack_trace(Dm.text("Tried to add a [] with a tether_trait_source that is a hard ref! Use REF() first before passing!", this.type), "code/datums/components/tether.dm", 30);
            return COMPONENT_INCOMPATIBLE;
        }

        this.tether_target = tether_target;
        this.embed_target = embed_target;
        this.max_dist = max_dist;
        this.parent_module = parent_module as obj_item_mod_module_tether;
        this.tether_trait_source = tether_trait_source;
        this.no_target_trait = no_target_trait;
        cur_dist = max_dist;
        if (start_distance != null)
            cur_dist = start_distance;
        datum_beam beam = tether_target.Beam(parent as atom, "line", Dm.resource("icons/obj/clothing/modsuit/mod_modules.dmi"), emissive: NONE, beam_type: typeof(obj_effect_ebeam_tether)) as datum_beam;
        tether_beam = beam;
        if (ispath(tether_name, typeof(atom)))
        {
            Type tmp = tether_name as Type;
            this.tether_name = Dm.initial(tmp, (atom p) => p.name);
        }
        else
            this.tether_name = tether_name;
        if (tether_trait_source != null)
        {
            ADD_TRAIT(parent, TRAIT_TETHER_ATTACHED, tether_trait_source);
            if (!Dm.truthy(no_target_trait))
                ADD_TRAIT(tether_target, TRAIT_TETHER_ATTACHED, tether_trait_source);
        }
        return null;
    }

    // tg: code/datums/components/tether.dm:54
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_MOVABLE_PRE_MOVE, nameof(check_tether));
        RegisterSignal(parent, COMSIG_MOVABLE_MOVED, nameof(check_snap));
        RegisterSignal(tether_target, COMSIG_MOVABLE_PRE_MOVE, nameof(check_tether));
        RegisterSignal(tether_target, COMSIG_MOVABLE_MOVED, nameof(check_snap));
        RegisterSignal(tether_target, COMSIG_QDELETING, nameof(on_delete));
        RegisterSignal(tether_beam.visuals, COMSIG_CLICK, nameof(beam_click));
        // Also snap if the beam gets deleted, more of a backup check than anything
        RegisterSignal(tether_beam.visuals, COMSIG_QDELETING, nameof(on_delete));

        if (embed_target != null)
        {
            RegisterSignal(embed_target, COMSIG_ITEM_UNEMBEDDED, nameof(on_embedded_removed));
            RegisterSignal(embed_target, COMSIG_QDELETING, nameof(on_delete));
        }

        if (parent_module != null)
        {
            RegisterSignals(parent_module, Dm.list(COMSIG_QDELETING, COMSIG_MOVABLE_MOVED, COMSIG_MOD_TETHER_SNAP), nameof(snap));
            RegisterSignal(parent_module, COMSIG_MODULE_TRIGGERED, nameof(on_parent_use));
        }
    }

    // tg: code/datums/components/tether.dm:72
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_MOVABLE_PRE_MOVE, COMSIG_MOVABLE_MOVED));
        if (tether_trait_source != null)
            REMOVE_TRAIT(parent, TRAIT_TETHER_ATTACHED, tether_trait_source);
        if (!QDELETED(tether_beam))
        {
            UnregisterSignal(tether_beam.visuals, Dm.list(COMSIG_CLICK, COMSIG_QDELETING));
            qdel(tether_beam);
        }
        if (!QDELETED(embed_target))
            UnregisterSignal(embed_target, Dm.list(COMSIG_ITEM_UNEMBEDDED, COMSIG_QDELETING));
        if (!QDELETED(tether_target))
        {
            UnregisterSignal(tether_target, Dm.list(COMSIG_MOVABLE_PRE_MOVE, COMSIG_MOVABLE_MOVED, COMSIG_QDELETING));
            if (tether_trait_source != null && !Dm.truthy(no_target_trait))
                REMOVE_TRAIT(tether_target, TRAIT_TETHER_ATTACHED, tether_trait_source);
            tether_target.SEND_SIGNAL(COMSIG_ATOM_TETHER_SNAPPED, Dm.list(tether_target, tether_trait_source));
        }
        parent.SEND_SIGNAL(COMSIG_ATOM_TETHER_SNAPPED, Dm.list(parent, tether_trait_source));
    }

    // tg: code/datums/components/tether.dm:88
    [SignalHandler]
    public virtual object check_tether(atom source = null, object new_loc = null)
    {
        // tg: if (check_snap(is_moving = TRUE)) return: check_snap (a void slot) returns nothing, so the test never passes
        check_snap(is_moving: TRUE);

        if (!isturf(new_loc))
        {
            to_chat(source, span_warning(I18n.T("[] prevents you from entering []!", tether_name, new_loc)));
            return COMPONENT_MOVABLE_BLOCK_PRE_MOVE;
        }

        // If this was called, we know its a movable
        atom_movable movable_source = source as atom_movable;
        atom_movable anchor = (source == tether_target ? parent : tether_target) as atom_movable;

        // Ignore distance limitations if we're attempting to move the other part of the tether
        if (get_dist(anchor, new_loc) > Dm.num(cur_dist) && !Dm.truthy(force_moving_target))
        {
            if (!istype(anchor, typeof(atom_movable)) || Dm.truthy(anchor.anchored) || anchor.move_resist > movable_source.move_force)
            {
                to_chat(source, span_warning(I18n.T("[] runs out of slack and prevents you from moving!", tether_name)));
                return COMPONENT_MOVABLE_BLOCK_PRE_MOVE;
            }

            force_moving_target = TRUE;
            if (!Dm.truthy(Waiting.NoSleep(try_adjust_position(anchor, new_loc as atom, source))))
            {
                force_moving_target = FALSE;
                to_chat(source, span_warning(I18n.T("[] runs out of slack and prevents you from moving!", tether_name)));
                return COMPONENT_MOVABLE_BLOCK_PRE_MOVE;
            }

            force_moving_target = FALSE;
        }

        atom blocker = check_line(anchor, new_loc as atom, Dm.list(source)) as atom;
        if (blocker != null)
        {
            if (!istype(anchor, typeof(atom_movable)) || Dm.truthy(anchor.anchored) || anchor.move_resist > movable_source.move_force)
            {
                to_chat(source, span_warning(I18n.T("[] runs out of slack and prevents you from moving!", tether_name)));
                return COMPONENT_MOVABLE_BLOCK_PRE_MOVE;
            }

            // If the tether would snag on something when we move, see if we could move to the side to get LOS back
            if (!Dm.truthy(Waiting.NoSleep(try_adjust_position(anchor, new_loc as atom, source))))
            {
                to_chat(source, span_warning(I18n.T("[] catches on [] and prevents you from moving!", tether_name, blocker)));
                return COMPONENT_MOVABLE_BLOCK_PRE_MOVE;
            }
        }
        return null;
    }

    /// Try adjust the anchor's position to move closer to the target or regain LOS
    /// true_source is an optional argument in case we're looking for a LOS/closer turf to a new location rather than the actual owner, and need to ignore them
    // tg: code/datums/components/tether.dm:129
    public virtual async DmTask<object> try_adjust_position(atom_movable anchor = null, atom target = null, atom true_source = null)
    {
        if (!istype(anchor, typeof(atom_movable)) || Dm.truthy(anchor.anchored))
            return FALSE;

        if (anchor.x == target.x && anchor.y == target.y)
            return TRUE;

        datum_can_pass_info pass_info = Dm.@new<datum_can_pass_info>(Dm.named("no_id", TRUE));
        pass_info.pass_flags = anchor.pass_flags;
        pass_info.movement_type = anchor.movement_type;
        if (isliving(anchor))
        {
            mob_living living_anchor = anchor as mob_living;
            pass_info.is_living = TRUE;
            pass_info.mob_size = living_anchor.mob_size;
            pass_info.incorporeal_move = living_anchor.incorporeal_move;
            pass_info.is_bot = Dm.num(isbot(living_anchor));
        }

        DmList pass_turfs = Dm.list();
        turf anchor_turf = get_turf(anchor);

        object primary_cardinal = null;
        if (abs(anchor.x - target.x) > abs(anchor.y - target.y))
            primary_cardinal = anchor.x > target.x ? WEST : EAST;
        else
            primary_cardinal = anchor.y > target.y ? SOUTH : NORTH;

        float anchor_dir = get_dir(anchor, target);
        if (Dm.eq(primary_cardinal, anchor_dir))
            Dm.append(ref pass_turfs, get_step(anchor, Dm.num(primary_cardinal)));
        else if (get_dist(anchor, get_step(target, REVERSE_DIR(Dm.num(primary_cardinal)))) >= get_dist(anchor, get_step(target, REVERSE_DIR(anchor_dir))))
        {
            Dm.append(ref pass_turfs, get_step(anchor, anchor_dir));
            Dm.append(ref pass_turfs, get_step(anchor, Dm.num(primary_cardinal)));
        }
        else
        {
            Dm.append(ref pass_turfs, get_step(anchor, Dm.num(primary_cardinal)));
            Dm.append(ref pass_turfs, get_step(anchor, anchor_dir));
        }

        // Make a list of secondary dirs to try and sidestep into if we cannot go in our main direction
        DmList match_dirs = null;
        if (Dm.eq(primary_cardinal, NORTH) || Dm.eq(primary_cardinal, SOUTH))
            match_dirs = Dm.list(EAST, WEST);
        else
            match_dirs = Dm.list(NORTH, SOUTH);

        foreach (object match_dir in Dm.each(match_dirs))
        {
            if (!Dm.eq(Dm.bitand(match_dir, primary_cardinal), anchor_dir))
                Dm.append(ref pass_turfs, get_step(anchor, Dm.num(Dm.bitor(match_dir, primary_cardinal))));
        }

        foreach (object match_dir in Dm.each(match_dirs))
            Dm.append(ref pass_turfs, get_step(anchor, Dm.num(match_dir)));

        // The final list is something like (direct path, main cardinal, diagonals to main cardinal, 90* dirs to the main cardinal)
        // Whichever one we manage to move onto first is our pick

        DmList turf_cache = Dm.list();
        foreach (turf pass_turf in Dm.each<turf>(pass_turfs)) // keep the typecheck in case we accidentally go out of map bounds
        {
            if (Dm.truthy(pass_turf.density) || get_dist(pass_turf, target) > Dm.num(cur_dist))
                continue;
            if (Dm.truthy(anchor_turf.LinkBlockedWithAccess(pass_turf, pass_info)))
                continue;
            if (Dm.truthy(check_line(pass_turf, target, Dm.list(anchor, true_source), turf_cache)))
                continue;
            if (Dm.truthy(await anchor.Move(pass_turf)))
                return TRUE;
        }
        return FALSE;
    }

    /// Check LOS availibility of a tile, returns a blocking atom, if any
    /// turf_cache could be used to reduce the amount of calculations if multiple lines are cast and expected to have multiple shared turfs
    /// by sharing located results
    // tg: code/datums/components/tether.dm:197
    public virtual object check_line(atom start = null, atom end = null, DmList to_ignore = null, DmList turf_cache = null)
    {
        turf_cache ??= Dm.list();
        turf start_loc = get_turf(start);
        turf end_loc = get_turf(end);
        float start_dir = get_dir(start_loc, end_loc);
        float end_dir = REVERSE_DIR(start_dir);
        DmList turf_line = get_line(start_loc, end_loc) as DmList;
        foreach (turf line_turf in Dm.each<turf>(turf_line))
        {
            if (Dm.truthy(turf_cache[line_turf]))
                return turf_cache[line_turf];

            if (Dm.truthy(line_turf.density) && line_turf != start_loc && line_turf != end_loc)
            {
                turf_cache[line_turf] = line_turf;
                return line_turf;
            }

            if (line_turf == start_loc)
            {
                foreach (atom in_turf in Dm.each<atom>(line_turf))
                {
                    if (Dm.truthy(in_turf.density) && Dm.truthy(Dm.bitand(in_turf.flags_1, ON_BORDER_1)) && Dm.truthy(Dm.bitand(in_turf.dir, start_dir)) && in_turf != start && !Dm.@in(in_turf, to_ignore))
                    {
                        turf_cache[line_turf] = in_turf;
                        return in_turf;
                    }
                }
                continue;
            }

            if (line_turf == end_loc)
            {
                foreach (atom in_turf in Dm.each<atom>(line_turf))
                {
                    if (Dm.truthy(in_turf.density) && Dm.truthy(Dm.bitand(in_turf.flags_1, ON_BORDER_1)) && Dm.truthy(Dm.bitand(in_turf.dir, end_dir)) && in_turf != end && !Dm.@in(in_turf, to_ignore))
                    {
                        turf_cache[line_turf] = in_turf;
                        return in_turf;
                    }
                }
                continue;
            }

            foreach (atom in_turf in Dm.each<atom>(line_turf))
            {
                if (!Dm.truthy(in_turf.density) || Dm.@in(in_turf, to_ignore))
                    continue;
                if (Dm.truthy(Dm.bitand(in_turf.flags_1, ON_BORDER_1)))
                {
                    // If the tether is in a straight line, we can ignore border objects parallel to us
                    if (!Dm.truthy(Dm.bitand(in_turf.dir, start_dir)) && !Dm.truthy(Dm.bitand(in_turf.dir, end_dir)))
                        continue;
                    // Also ignore objects that we don't intersect with
                    if (!Dm.@in(get_step(in_turf, in_turf.dir), turf_line))
                        continue;
                }

                turf_cache[line_turf] = in_turf;
                return in_turf;
            }

            turf_cache[line_turf] = null;
        }
        return null;
    }

    // tg: code/datums/components/tether.dm:241
    [SignalHandler]
    public virtual void check_snap(atom_movable source = null, atom old_loc = null, object dir = null, object forced = null, DmList old_locs = null, float? is_moving = null)
    {
        is_moving ??= FALSE;
        atom atom_target = parent as atom;
        // Something broke us out, snap the tether
        if (get_dist(atom_target, tether_target) > Dm.num(cur_dist) + 1 || !isturf(atom_target.loc) || !isturf(tether_target.loc) || atom_target.z != tether_target.z)
            snap();
        else if (!Dm.truthy(is_moving) && Dm.truthy(check_line(atom_target, tether_target)) && !(Dm.truthy(Waiting.NoSleep(try_adjust_position(atom_target as atom_movable, tether_target))) || Dm.truthy(Waiting.NoSleep(try_adjust_position(tether_target as atom_movable, atom_target)))))
            snap();
    }

    // tg: code/datums/components/tether.dm:251
    [SignalHandler]
    public virtual void snap()
    {
        atom atom_target = parent as atom;
        atom_target.visible_message(span_warning(I18n.T("[]'s [] snaps!", atom_target, tether_name)), span_userdanger(I18n.T("Your [] snaps!", tether_name)), span_hear(I18n.T("You hear a cable snapping.")));
        playsound(atom_target, Dm.resource("sound/effects/snap.ogg"), 50, TRUE);
        qdel(this);
    }

    // tg: code/datums/components/tether.dm:259
    [SignalHandler]
    public virtual object on_parent_use(obj_item_mod_module module = null, atom target = null)
    {
        if (get_turf(target) == get_turf(tether_target))
            return MOD_ABORT_USE;
        return null;
    }

    // tg: code/datums/components/tether.dm:265
    [SignalHandler]
    public virtual void on_delete()
    {
        qdel(this);
    }

    // tg: code/datums/components/tether.dm:269
    [SignalHandler]
    public virtual void on_embedded_removed(atom source = null, mob_living victim = null)
    {
        Waiting.NoSleep(parent.AddComponent(typeof(datum_component_tether), source, max_dist, tether_name, cur_dist));
        qdel(this);
    }

    // tg: code/datums/components/tether.dm:274
    [SignalHandler]
    public virtual void beam_click(atom source = null, atom location = null, object control = null, object @params = null, mob user = null)
    {
        Waiting.Detach(process_beam_click(source, location, @params, user)); // INVOKE_ASYNC
    }

    // tg: code/datums/components/tether.dm:279
    public virtual async DmTask process_beam_click(atom source = null, atom location = null, object @params = null, mob user = null)
    {
        turf nearest_turf = null;
        foreach (turf line_turf in Dm.each<turf>(get_line(get_turf(parent), get_turf(tether_target))))
        {
            if (Dm.truthy(await line_turf.IsReachableBy(user)))
            {
                nearest_turf = line_turf;
                break;
            }
        }

        if (nearest_turf == null)
            return;

        if (!Dm.truthy(await user.can_perform_action(nearest_turf)))
        {
            nearest_turf.balloon_alert(user, I18n.T("cannot reach!"));
            return;
        }

        DmList modifiers = params2list(@params) as DmList;
        if (Dm.truthy(LAZYACCESS(modifiers, CTRL_CLICK)))
        {
            location.balloon_alert(user, I18n.T("cutting the tether..."));
            if (!Dm.truthy(await do_after(user, 2 * SECONDS, user, (user == parent || user == tether_target) ? Dm.num(Dm.bitor(IGNORE_USER_LOC_CHANGE, IGNORE_TARGET_LOC_CHANGE)) : NONE)))
                return;

            qdel(this);
            location.balloon_alert(user, I18n.T("tether cut!"));
            to_chat(parent, span_danger(I18n.T("Your [] has been cut!", tether_name)));
            return;
        }

        if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
        {
            if (Dm.num(cur_dist) >= Dm.num(max_dist))
            {
                location.balloon_alert(user, I18n.T("no coil remaining!"));
                return;
            }
            cur_dist = Dm.num(cur_dist) + 1;
            location.balloon_alert(user, I18n.T("tether extended"));
            return;
        }

        if (Dm.num(cur_dist) <= 0)
        {
            location.balloon_alert(user, I18n.T("too short!"));
            return;
        }

        if (Dm.num(cur_dist) > CEILING(get_dist(parent, tether_target), 1))
        {
            cur_dist = Dm.num(cur_dist) - 1;
            location.balloon_alert(user, I18n.T("tether shortened"));
            return;
        }

        if (!ismovable(parent) && !ismovable(tether_target))
        {
            location.balloon_alert(user, I18n.T("too short!"));
            return;
        }

        atom_movable movable_parent = parent as atom_movable;
        atom_movable movable_target = tether_target as atom_movable;

        if (istype(movable_parent, typeof(atom_movable)) && !Dm.truthy(movable_parent.anchored) && movable_parent.move_resist <= movable_target.move_force && Dm.truthy(await movable_parent.Move(get_step(movable_parent.loc, get_dir(movable_parent, movable_target)))))
        {
            cur_dist = Dm.num(cur_dist) - 1;
            location.balloon_alert(user, I18n.T("tether shortened"));
            return;
        }

        if (istype(movable_target, typeof(atom_movable)) && !Dm.truthy(movable_target.anchored) && movable_target.move_resist <= movable_parent.move_force && Dm.truthy(await movable_target.Move(get_step(movable_target.loc, get_dir(movable_target, movable_parent)))))
        {
            cur_dist = Dm.num(cur_dist) - 1;
            location.balloon_alert(user, I18n.T("tether shortened"));
            return;
        }

        location.balloon_alert(user, I18n.T("too short!"));
    }
}
