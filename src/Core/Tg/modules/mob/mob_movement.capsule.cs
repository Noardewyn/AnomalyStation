// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/mob_movement.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the capsule mob's share of client/Move (M2 P4, DV15, DV16; docs/ai/specs/locomotion.md section 4): the gate
// (client/Move's checks, :53-98, in tg's order, as client/capsule_move_gate) and the tail (:135-145, as
// client/capsule_move_tail) that the locomotion bridge (Infrastructure/Locomotion) runs around each tile crossing of a
// capsule mob, and MobBump's signal part (living.dm:176-203, as /mob/living/capsule_mob_bump) that a capsule contact runs.
// These are port procs (DM names the port introduces for its own procs, like client/capsule_move_gate of the contracts
// step) holding tg's lines with a // tg: reference each; tg's own client/Move stays whole in mob_movement.cs and still
// moves the mobs the bridge does not drive (DV14). rules 2.5 (settled at the M2 integration): rules 2.5 names the suffixes the port adds to DM names but no
// rule for a new proc of the port's own that carries tg lines; the contracts step named the first one
// (capsule_move_gate), and the other two follow it.

// tg: code/modules/mob/mob_movement.dm:46
public partial class client : datum
{
    /**
     * The movement gate of a capsule mob (DEVIATION(DV15)): tg's client/Move checks, in tg's order, minus the move_delay
     * bookkeeping (:48-52, the capsule moves continuously at tg's speed) and minus the move itself (:100-133, the `. = ..()`
     * and the glide and move_delay bookkeeping around it: the capsule's step stands for it). The move_delay test itself
     * (:47) stays: a capsule's own steps never push move_delay, so only tg's stops do (Process_Grab's 1 second, a failed
     * resist_grab's 4 seconds, an EMP'd robotic limb), and those hold the capsule as they hold tg's client/Move. The
     * locomotion bridge runs it each tick the player's wish vector is not zero, with the turf the wish points at and its
     * BYOND direction, as keyLoop passes them to client/Move.
     *
     * Returns TRUE where tg's client/Move would go on to move the mob (the capsule may move this tick) and FALSE
     * everywhere else; the relays (remote control, buckled, inside an object) still run as in tg, and their own result,
     * which tg's caller (keyLoop) never reads, is not returned.
     */
    // tg: code/modules/mob/mob_movement.dm:46
    public virtual async DmTask<object> capsule_move_gate(object new_loc = null, object direct = null)
    {
        // tg: :47  (the capsule's steps do not write move_delay: :51 and :123-134 do not run for it)
        if (world.time < move_delay) //do not move anything ahead of this check please
            return FALSE;
        // tg: :53
        if (!Dm.truthy(direct) || !Dm.truthy(new_loc))
            return FALSE;
        // tg: :55
        if (!Dm.truthy(mob?.loc))
            return FALSE;
        // tg: :57
        if (HAS_TRAIT(mob, TRAIT_NO_TRANSFORM))
            return FALSE; //This is sorta the goto stop mobs from moving trait
        // tg: :59
        if (!isliving(mob))
        {
            // tg: :60
            if (Dm.truthy(Dm.bitand((!Dm.truthy(mob._listen_lookup?[COMSIG_MOB_CLIENT_PRE_NON_LIVING_MOVE]) ? (object)NONE : mob._SendSignal(COMSIG_MOB_CLIENT_PRE_NON_LIVING_MOVE, Dm.list(mob, new_loc, direct))), COMSIG_MOB_CLIENT_BLOCK_PRE_NON_LIVING_MOVE)))
                return FALSE;
            // tg: :62  return mob.Move(new_loc, direct)
            // DEVIATION(DV15): a non-living mob is never a capsule mob (the bridge registers /mob/living only), so tg's move
            // of it does not happen here; tg's client/Move moves it.
            return FALSE;
        }
        // tg: :63
        if (mob.stat == DEAD)
        {
            // tg: :64
            mob.ghostize();
            // tg: :65
            return FALSE;
        }
        // tg: :66
        if (Dm.truthy(Dm.bitand((!Dm.truthy(mob._listen_lookup?[COMSIG_MOB_CLIENT_PRE_LIVING_MOVE]) ? (object)NONE : mob._SendSignal(COMSIG_MOB_CLIENT_PRE_LIVING_MOVE, Dm.list(mob, new_loc, direct))), COMSIG_MOB_CLIENT_BLOCK_PRE_LIVING_MOVE)))
            return FALSE;

        // tg: :69
        mob_living L = mob as mob_living; //Already checked for isliving earlier
        // tg: :70
        if (Dm.truthy(L.incorporeal_move) && !Dm.truthy(is_secret_level(mob.z))) //Move though walls
        {
            // DEVIATION(DV29): INCORPOREAL_MOVE_BASIC moves the capsule through walls, and the bridge's crossings are
            // Process_Incorpmove's forceMove (LocomotionSeam.IncorporealMove); the other kinds call tg's proc as before.
            if (Dm.eq(L.incorporeal_move, INCORPOREAL_MOVE_BASIC))
                return TRUE;
            // tg: :71
            await Process_Incorpmove(direct);
            // tg: :72
            return FALSE;
        }

        // tg: :74
        if (Dm.truthy(mob.remote_control)) //we're controlling something, our movement is relayed to it
        {
            // DEVIATION(DV429): a holocall's eye flies freely, as the AI's (DV423)
            if (mob.remote_control is mob_eye_camera_remote_holo holo_eye && LocomotionSeam.FlyRemoteEye(mob as mob_living, holo_eye))
                return FALSE;
            // tg: :75  return mob.remote_control.relaymove(mob, direct)
            await mob.remote_control.relaymove(mob as mob_living, direct);
            return FALSE;
        }

        // tg: :77
        if (isAI(mob))
        {
            // tg: :78-79 (silicons SI4)
            mob_living_silicon_ai smoovin_ai = mob as mob_living_silicon_ai;
            if (!LocomotionSeam.FlyAiEye(smoovin_ai)) // DEVIATION(DV423): the eye flies freely
                smoovin_ai.AIMove(direct);
            return FALSE;
        }

        // tg: :81
        if (Dm.truthy(await Process_Grab())) //are we restrained by someone's grip?
            return FALSE;

        // tg: :84
        if (Dm.truthy(mob.buckled)) //if we're buckled to something, tell it we moved.
        {
            // tg: :85  return mob.buckled.relaymove(mob, direct)
            await mob.buckled.relaymove(mob as mob_living, direct);
            return FALSE;
        }

        // tg: :87
        if (!Dm.truthy(Dm.bitand(L.mobility_flags, MOBILITY_MOVE)))
            return FALSE;

        // tg: :90
        if (ismovable(mob.loc)) //Inside an object, tell it we moved
        {
            // tg: :91
            atom loc_atom = mob.loc;
            // tg: :92  return loc_atom.relaymove(mob, direct)
            await loc_atom.relaymove(mob as mob_living, direct);
            return FALSE;
        }

        // tg: :94
        if (!Dm.truthy(await mob.Process_Spacemove(Dm.num_or_null(direct))))
            return FALSE;

        // tg: :97  (args: client/Move's own arguments, new_loc and direct)
        DmList args = Dm.args(new_loc, direct);
        if (Dm.truthy(Dm.bitand((!Dm.truthy(mob._listen_lookup?[COMSIG_MOB_CLIENT_PRE_MOVE]) ? (object)NONE : mob._SendSignal(COMSIG_MOB_CLIENT_PRE_MOVE, Dm.list(mob, args))), COMSIG_MOB_CLIENT_BLOCK_PRE_MOVE)))
            return FALSE;
        // DM's args are the proc's arguments: a handler that wrote move_args (a confusion's scramble, confusion.dm:44-45) changed
        // new_loc and direct, which tg's `. = ..()` then moves by.
        new_loc = args[1];
        direct = args[2];

        //We are now going to move
        // tg: :100-133 (the move): the capsule's step (DEVIATION(DV15)), in the direction direct now holds
        dm_capsule_direct = direct;
        return TRUE;
    }

    /**
     * client/Move's tail after a successful move (DEVIATION(DV15)): the locomotion bridge runs it after each tile crossing
     * whose Move succeeded, with the crossing's direction and the mob's dir and loc from before that Move (tg's old_dir and
     * old_loc, :118-119). The glide and move_delay lines between (:123-134) do not apply to a capsule mob.
     */
    // tg: code/modules/mob/mob_movement.dm:135
    public virtual async DmTask capsule_move_tail(object direct = null, object old_dir = null, object old_loc = null)
    {
        // tg: :135  if(.) — the crossing's Move succeeded: the bridge calls the tail only then
        // tg: :136
        if (Dm.truthy(mob.throwing))
            mob.throwing.finalize(FALSE);

        // At this point we've moved the client's attached mob. This is one of the only ways to guess that a move was done
        // as a result of player input and not because they were pulled or any other magic.
        // tg: :141
        if (Dm.truthy(mob._listen_lookup?[COMSIG_MOB_CLIENT_MOVED]))
            mob._SendSignal(COMSIG_MOB_CLIENT_MOVED, Dm.list(mob, direct, old_dir, old_loc));

        // tg: :143
        atom_movable P = mob.pulling;
        // tg: :144  mob.setDir(REVERSE_DIR(mob.dir)), after tg's Move set mob.dir to direct. DEVIATION(DV48): the crossing's Move
        // keeps dir (DV15), so the step's direction is turned: the puller faces what it drags, and the bridge's facing leaves
        // dir alone while this holds (LocomotionSeam.PullTurnsAround).
        if (Dm.truthy(P) && !ismob(P) && Dm.truthy(P.density))
            await mob.setDir(REVERSE_DIR(Dm.num(direct)));
    }
}

// tg: code/modules/mob/living/living.dm:176 (interleaved type, rules 1.9: MobBump's signal part for the bridge)
public partial class mob_living : mob
{
    /**
     * A capsule contact (DEVIATION(DV16), D105): MobBump's lines up to its swap and push (living.dm:178-203): the walk
     * intent stop, the bump signals, the fire and disease spread. The rest of MobBump (:205-274: the restrained checks, the
     * mob swap and the walk-push) does not run between capsule mobs: standing players are solid and pass with a shove. The
     * locomotion bridge calls it on the moving mob when its capsule touches a standing one while moving toward it, at most
     * once per tg move delay per pair. tg's own MobBump (P1, living.cs) keeps serving tile-driven bumps.
     */
    // tg: code/modules/mob/living/living.dm:176
    public virtual object capsule_mob_bump(mob M = null)
    {
        //No bumping/swapping/pushing others if you are on walk intent
        // tg: :178
        if (move_intent == MOVE_INTENT_WALK)
            return TRUE;

        // tg: :181
        if (Dm.truthy(Dm.bitand((!Dm.truthy(M._listen_lookup?[COMSIG_LIVING_PRE_MOB_BUMP]) ? (object)NONE : M._SendSignal(COMSIG_LIVING_PRE_MOB_BUMP, Dm.list(M, this))), COMPONENT_LIVING_BLOCK_PRE_MOB_BUMP)))
            return TRUE;

        // tg: :184
        if (Dm.truthy(this._listen_lookup?[COMSIG_LIVING_MOB_BUMP]))
            this._SendSignal(COMSIG_LIVING_MOB_BUMP, Dm.list(this, M));
        // tg: :185
        if (Dm.truthy(M._listen_lookup?[COMSIG_LIVING_MOB_BUMPED]))
            M._SendSignal(COMSIG_LIVING_MOB_BUMPED, Dm.list(M, this));
        //Even if we don't push/swap places, we "touched" them, so spread fire
        // tg: :187  spreadFire(M)
        Dm.Deferred("code/modules/mob/living/living.dm:187", "MobBump: spreadFire(M), fire spreading on contact (fire stacks, M4b)");

        // tg: :189
        if (Dm.truthy(now_pushing))
            return TRUE;

        // tg: :192
        if (isliving(M))
        {
            // tg: :193
            mob_living L = M as mob_living;
            //Also spread diseases
            // tg: :195
            foreach (object thing in Dm.each(diseases))
            {
                // tg: :196-198
                datum_disease D = (datum_disease)thing;
                if (Dm.truthy(Dm.bitand(D.spread_flags, DISEASE_SPREAD_CONTACT_SKIN)))
                    Waiting.NoSleep(L.ContactContractDisease(D)); // gaps VI1
            }

            // tg: :200
            foreach (object thing in Dm.each(L.diseases))
            {
                // tg: :201-203
                datum_disease D = (datum_disease)thing;
                if (Dm.truthy(Dm.bitand(D.spread_flags, DISEASE_SPREAD_CONTACT_SKIN)))
                    Waiting.NoSleep(ContactContractDisease(D)); // gaps VI1
            }
        }

        // tg: :205-274  DEVIATION(DV16): the restrained checks, the mob swap and the walk-push do not run for a capsule contact.
        return TRUE;
    }
}
