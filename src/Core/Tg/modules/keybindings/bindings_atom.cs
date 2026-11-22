// Translated from tgstation (snapshot 1d99bd7f): code/modules/keybindings/bindings_atom.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M1 P1, the client layer).

// You might be wondering why this isn't client level. If focus is null, we don't want you to move.
// Only way to do that is to tie the behavior into the focus's keyLoop().

// tg: code/modules/keybindings/bindings_atom.dm:4
public partial class atom_movable : atom
{
    // tg: code/modules/keybindings/bindings_atom.dm:4 (set waitfor = FALSE, inherited from /datum/keyLoop, setup.dm:9; its
    // body may sleep through client/Move: the wrapper and its body, waiting-model.md 5.4; the body never writes '.')
    public override object keyLoop(client user = null)
    {
        return Waiting.WaitforFalse(keyLoop__body(user, null), null);
    }

    private async DmTask<object> keyLoop__body(client user, DotBox<object> dot)
    {
        // Clients don't go null randomly. They do go null unexpectedly though, when they're poked in particular ways
        // keyLoop is called by a for loop over mobs. We're guarenteed that all the mobs have clients at the START
        // But the move of one mob might poke the client of another, so we do this
        if (!Dm.truthy(user))
            return FALSE;
        // DEVIATION(DV15): the locomotion bridge moves a capsule mob; it sets the client's intended_direction from the
        // player's wish each tick (as calculate_move_dir, :40) and calls client/Move itself where tg's tile steps move the
        // mob (zero gravity, D146), so SSinput's loop must not move it a second time.
        if (LocomotionSeam.IsRegistered(user.mob))
            return FALSE;
        object movement_dir = Dm.bitor(user.intended_direction, user.next_move_dir_add);
        // If we're not movin anywhere, we aren't movin anywhere
        // Safe because nothing adds to movement_dir after this moment
        if (!Dm.truthy(movement_dir))
        {
            // No input == our removal would have done nothing
            // So we can safely forget about it
            user.next_move_dir_sub = NONE;
            return FALSE;
        }

        if (Dm.truthy(user.next_move_dir_sub))
            Dm.mask(ref movement_dir, Dm.bitnot(user.next_move_dir_sub));
        // Sanity checks in case you hold left and right and up to make sure you only go up
        if (Dm.truthy(Dm.bitand(movement_dir, NORTH)) && Dm.truthy(Dm.bitand(movement_dir, SOUTH)))
            Dm.mask(ref movement_dir, Dm.bitnot(Dm.bitor(NORTH, SOUTH)));
        if (Dm.truthy(Dm.bitand(movement_dir, EAST)) && Dm.truthy(Dm.bitand(movement_dir, WEST)))
            Dm.mask(ref movement_dir, Dm.bitnot(Dm.bitor(EAST, WEST)));

        if (user.dir != NORTH && Dm.truthy(movement_dir)) //If we're not moving, don't compensate, as byond will auto-fill dir otherwise
            movement_dir = turn(movement_dir, Dm.num(Dm.neg(dir2angle(user.dir)))); //By doing this we ensure that our input direction is offset by the client (camera) direction

        //turn without moving while using the movement lock key, unless something wants to ignore it and move anyway
        if (Dm.truthy(user.movement_locked) && !Dm.truthy(Dm.bitand((!Dm.truthy(_listen_lookup?[COMSIG_MOVABLE_KEYBIND_FACE_DIR]) ? (object)NONE : _SendSignal(COMSIG_MOVABLE_KEYBIND_FACE_DIR, Dm.list(this, movement_dir))), COMSIG_IGNORE_MOVEMENT_LOCK)))
            await keybind_face_direction(movement_dir);
        // Null check cause of the signal above
        else if (Dm.truthy(user))
        {
            await user.Move(get_step(this, Dm.num(movement_dir)), movement_dir);
            return Dm.not(Dm.not(movement_dir)); //true if there was actually any player input
        }

        return FALSE;
    }
}

// tg: code/modules/keybindings/bindings_atom.dm:40 (interleaved types, rules 1.9)
public partial class client : datum
{
    // tg: code/modules/keybindings/bindings_atom.dm:40
    public virtual void calculate_move_dir()
    {
        float movement_dir = NONE;
        foreach (object _key in Dm.each(keys_held))
            Dm.combine(ref movement_dir, movement_keys[_key]);
        intended_direction = movement_dir;
    }
}
