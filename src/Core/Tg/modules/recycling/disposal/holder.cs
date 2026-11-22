// Translated from tgstation (snapshot 1d99bd7f): code/modules/recycling/disposal/holder.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (D325: the disposal system).

// virtual disposal object
// travels through pipes in lieu of actual items
// contents will be items flushed by the disposal
// this allows the gas flushed to be tracked

// tg: code/modules/recycling/disposal/holder.dm:6
public partial class obj_structure_disposalholder : obj_structure
{
    // tg: code/modules/recycling/disposal/holder.dm:25
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_WEATHER_IMMUNE, REF(this));
        return dot;
    }

    // tg: code/modules/recycling/disposal/holder.dm:29
    public override object Destroy(float? force = null)
    {
        active = FALSE;
        last_pipe = null;
        current_pipe = null;
        return base.Destroy(force);
    }

    /// Initializes a holder from the contents of a disposal unit
    // tg: code/modules/recycling/disposal/holder.dm:36
    public virtual async DmTask init(obj_machinery_disposal D = null)
    {
        gas = D.air_contents; // transfer gas resv. into holder object

        //Check for any living mobs trigger hasmob.
        //hasmob effects whether the package goes to cargo or its tagged destination.
        foreach (mob_living M in Dm.each<mob_living>(D.contents.Copy()))
        {
            if (M.client != null)
                await M.reset_perspective(this);
            hasmob = TRUE;
            RegisterSignal(M, COMSIG_LIVING_RESIST, nameof(struggle_prep));
        }

        //Checks 1 contents level deep. This means that players can be sent through disposals mail...
        //...but it should require a second person to open the package. (i.e. person inside a wrapped locker)
        foreach (obj O in Dm.each<obj>(D.contents.Copy()))
        {
            if (Dm.locate<mob_living>(O) != null)
            {
                hasmob = TRUE;
                break;
            }
        }

        // now everything inside the disposal gets put into the holder
        // note AM since can contain mobs or objs
        foreach (object A in Dm.each(D.contents.Copy()))
        {
            atom_movable atom_in_transit = A as atom_movable;
            if (atom_in_transit == this)
                continue;
            atom_in_transit.SEND_SIGNAL(COMSIG_MOVABLE_DISPOSING, Dm.list(atom_in_transit, this, D, hasmob));
            await atom_in_transit.forceMove(this);
            if (iscyborg(atom_in_transit))
            {
                obj_item_dest_tagger_borg tagger = Dm.locate<obj_item_dest_tagger_borg>(atom_in_transit);
                if (tagger != null)
                    destinationTag = Dm.num(tagger.currTag);
            }
        }
    }

    /// Starts the movement process, argument is the disposal unit the holder started in
    // tg: code/modules/recycling/disposal/holder.dm:70
    public virtual async DmTask start(obj_machinery_disposal D = null)
    {
        if (QDELETED(D.trunk))
        {
            await D.expel(this); // no trunk connected, so expel immediately
            return;
        }
        await forceMove(D.trunk);
        active = TRUE;
        await setDir(DOWN);
        start_moving();
    }

    /// Starts the movement process, persists while the holder is moving through pipes
    // tg: code/modules/recycling/disposal/holder.dm:80
    public virtual void start_moving()
    {
        float delay = world.tick_lag;
        datum_move_loop our_loop = GLOB.move_manager.move_disposals(this, delay: delay, timeout: delay * count) as datum_move_loop;
        if (our_loop != null)
        {
            RegisterSignal(our_loop, COMSIG_MOVELOOP_PREPROCESS_CHECK, nameof(pre_move));
            RegisterSignal(our_loop, COMSIG_MOVELOOP_POSTPROCESS, nameof(try_expel));
            RegisterSignal(our_loop, COMSIG_QDELETING, nameof(movement_stop));
            current_pipe = loc as obj_structure_disposalpipe;
        }
    }

    /// Handles the preprocess check signal, sets the current pipe as the last pipe
    // tg: code/modules/recycling/disposal/holder.dm:90
    public virtual void pre_move(datum_move_loop source = null)
    {
        last_pipe = loc as obj_structure_disposalpipe;
    }

    /// Handles the postprocess check signal, tries to leave the pipe
    // tg: code/modules/recycling/disposal/holder.dm:95
    public virtual void try_expel(datum_move_loop source = null, object result = null, object visual_delay = null)
    {
        if (current_pipe != null || !Dm.truthy(active))
            return;
        // a signal handler (SIGNAL_HANDLER) cannot sleep: the pipe's expel throws the contents without waiting
        Waiting.NoSleep(last_pipe.expel(this, get_turf(this), dir));
    }

    /// Handles what happens to the contents when the qdel signal triggers
    // tg: code/modules/recycling/disposal/holder.dm:102
    public virtual void movement_stop(datum source = null)
    {
        current_pipe = null;
        last_pipe = null;
        active = FALSE;
        foreach (mob_living piperider in Dm.each<mob_living>(contents))
            to_chat(piperider, span_notice(I18n.T("Your movement has slowed to a stop. If you tried, you could probably <b>struggle</b> free.")));
    }

    /**
     * Starts the struggle code
     *
     * Called by resist verb (or hotkey) via signal. Makes a sanity
     * check and then calls part 2.
     */
    // tg: code/modules/recycling/disposal/holder.dm:117
    public virtual void struggle_prep(mob_living escapee = null)
    {
        if (escapee.loc != this)
        {
            UnregisterSignal(escapee, COMSIG_LIVING_RESIST);
            return; //Somehow they got out without telling us
        }
        Waiting.Detach(struggle_free(escapee));
    }

    /**
     * Completes the struggle code
     *
     * The linter gets upsetti spaghetti if this is part of the above proc
     * because the do_after is a sleep.
     */
    // tg: code/modules/recycling/disposal/holder.dm:131
    public virtual async DmTask struggle_free(mob_living escapee = null)
    {
        if (!istype(loc, typeof(obj_structure_disposalpipe)))
            return; //Somehow we're not in a pipe, shits probably fucked
        obj_structure_disposalpipe transport_cylinder = loc as obj_structure_disposalpipe;
        if (Dm.truthy(active))
        {
            to_chat(escapee, span_danger(I18n.T("You slide past [] and are unable to keep your grip!", loc)));
            return;
        }
        if (Dm.@in(this, escapee.do_afters))
            return; //already trying to escape
        to_chat(escapee, span_warning(I18n.T("You push against the thin pipe walls...")));
        playsound(loc, Dm.resource("sound/machines/airlock/airlock_alien_prying.ogg"), vol: 30, vary: FALSE, extrarange: 3); //yeah I know but at least it sounds like metal being bent.

        if (!Dm.truthy(await do_after(escapee, 20 * SECONDS, get_turf(loc))))
            return;
        foreach (mob_living jailbird in Dm.each<mob_living>(contents.Copy()))
            await jailbird.apply_damage(rand(5, 15), damagetype: BRUTE);
        await transport_cylinder.spew_forth();
        await transport_cylinder.take_damage(transport_cylinder.max_integrity);
    }

    //failsafe in the case the holder is somehow forcemoved somewhere that's not a disposal pipe. Otherwise the above loop breaks.
    // tg: code/modules/recycling/disposal/holder.dm:153
    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        momentum_change ??= TRUE;
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        //Moved to nullspace gang
        if (loc == null || loc is obj_structure_disposalpipe)
            return dot;
        turf T = get_turf(loc);
        if (T != null)
            await vent_gas(T);
        foreach (object A in Dm.each(contents.Copy()))
        {
            atom_movable AM = A as atom_movable;
            await AM.forceMove(drop_location() as atom);
        }
        qdel(this);
        return dot;
    }

    /// Finds the turf which should contain the next pipe
    // tg: code/modules/recycling/disposal/holder.dm:167
    public virtual object nextloc()
    {
        return get_step(this, Dm.num(dir));
    }

    /// Finds a matching pipe on a turf
    // tg: code/modules/recycling/disposal/holder.dm:171
    public virtual object findpipe(turf T = null)
    {
        if (T == null)
            return null;

        object fdir = REVERSE_DIR(dir); // flip the movement direction
        foreach (obj_structure_disposalpipe P in Dm.each<obj_structure_disposalpipe>(T.contents))
        {
            if (Dm.truthy(Dm.bitand(fdir, P.dpdir))) // find pipe direction mask that matches flipped dir
            {
                if (QDELING(P))
                    throw Dm.CRASH(Dm.text("Pipe is being deleted while being used by a disposal holder at ([], [], []", P.x, P.y, P.z), "code/modules/recycling/disposal/holder.dm", 180);
                return P;
            }
        }
        // if no matching pipe, return null
        return null;
    }

    /// Merge two holder objects, used when a holder meets a stuck holder
    // tg: code/modules/recycling/disposal/holder.dm:186
    public virtual async DmTask merge(obj_structure_disposalholder other = null)
    {
        foreach (atom_movable movable in Dm.each<atom_movable>(other.contents.Copy()))
        {
            await movable.forceMove(this); // move everything in other holder to this one
            if (ismob(movable))
            {
                mob mob = movable as mob;
                await mob.reset_perspective(this); // if a client mob, update eye to follow this holder
                RegisterSignal(mob, COMSIG_LIVING_RESIST, nameof(struggle_prep));
                hasmob = TRUE;
            }
        }
        if (destinationTag == 0 && other.destinationTag != 0)
            destinationTag = other.destinationTag;
        if (!Dm.truthy(tomail) && Dm.truthy(other.tomail))
            tomail = TRUE;
        qdel(other);
    }

    // called when player tries to move while in a pipe
    // tg: code/modules/recycling/disposal/holder.dm:202
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        if (Dm.truthy(user.incapacitated))
            return null;
        foreach (mob M in Dm.each<mob>(range(5, get_turf(this))))
            M.show_message(Dm.text("<FONT size=[]>CLONG, clong!</FONT>", max(0, 5 - get_dist(this, M))), MSG_AUDIBLE);
        playsound(loc, Dm.resource("sound/effects/clang.ogg"), 50, FALSE, FALSE);
        return null;
    }

    /// Called to vent all gas in holder to a location
    // tg: code/modules/recycling/disposal/holder.dm:209
    public virtual async DmTask vent_gas(turf T = null)
    {
        await T.assume_air(gas);
    }

    // tg: code/modules/recycling/disposal/holder.dm:212
    public override object AllowDrop()
    {
        return TRUE;
    }

    // tg: code/modules/recycling/disposal/holder.dm:215
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        return FALSE;
    }
}
