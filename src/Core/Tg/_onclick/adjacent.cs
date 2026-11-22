// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/adjacent.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (wave 4, package A: the pulling code, atoms_movable.dm:621, and the timed actions, timed_actions.dm:180,
// :190, call Adjacent). adjacent.dm is the earliest file of tgstation.dme that declares something on /obj/item
// (/obj/item/Adjacent, :81), so this part carries its [DmPath] (rules 2.3). The /area, /turf, /atom/movable and /obj/item
// blocks interleave in tg's order (rules 1.9).

/*
	Adjacency proc for determining touch range

	This is mostly to determine if a user can enter a square for the purposes of touching something.
	Examples include reaching a square diagonally or reaching something on the other side of a glass window.

	This is calculated by looking for border items, or in the case of clicking diagonally from yourself, dense items.
	This proc will NOT notice if you are trying to attack a window on the other side of a dense object in its turf.  There is a window helper for that.

	Note that in all cases the neighbor is handled simply; this is usually the user's mob, in which case it is up to you
	to check that the mob is not inside of something
*/
// tg: code/_onclick/adjacent.dm:13
public partial class atom : datum
{
    // tg: code/_onclick/adjacent.dm:13
    public virtual async DmTask<object> Adjacent(atom neighbor = null, atom target = null, atom_movable mover = null, params object[] __extra) // basic inheritance, unused
    {
        return null;
    }
}

// tg: code/_onclick/adjacent.dm:17
public partial class area : atom
{
    // Not a sane use of the function and (for now) indicative of an error elsewhere
    // tg: code/_onclick/adjacent.dm:17
    public override async DmTask<object> Adjacent(atom neighbor = null, atom target = null, atom_movable mover = null, params object[] __extra)
    {
        throw Dm.CRASH("Call to /area/Adjacent(), unimplemented proc", "code/_onclick/adjacent.dm", 18);
    }
}

/*
	Adjacency (to turf):
	* If you are in the same turf, always true
	* If you are vertically/horizontally adjacent, ensure there are no border objects
	* If you are diagonally adjacent, ensure you can pass through at least one of the mutually adjacent square.
		* Passing through in this case ignores anything with the LETPASSTHROW pass flag, such as tables, racks, and morgue trays.
*/
// tg: code/_onclick/adjacent.dm:29
public partial class turf : atom
{
    // tg: code/_onclick/adjacent.dm:29
    public override async DmTask<object> Adjacent(atom neighbor = null, atom target = null, atom_movable mover = null, params object[] __extra)
    {
        turf T0 = get_turf(neighbor);

        if (T0 == this) //same turf
            return TRUE;

        if (T0 != null && z != T0.z)
            return await AdjacentAcrossLevels(T0, neighbor, target, mover); // DEVIATION(DV425): tg's other level is never in reach

        if (get_dist(this, T0) > 1 || z != T0.z) //too far
            return FALSE;

        // Non diagonal case
        if (T0.x == x || T0.y == y)
        {
            // Check for border blockages
            return (await T0.ClickCross(get_dir(T0, this), TRUE, target, mover) is var and__1 && !Dm.truthy(and__1) ? and__1 : await ClickCross(get_dir(this, T0), TRUE, target, mover));
        }

        // Diagonal case
        float in_dir = get_dir(T0, this); // eg. northwest (1+8) = 9 (00001001)
        float d1 = Dm.bitand(in_dir, 3);      // eg. north   (1+8)&3 (0000 0011) = 1 (0000 0001)
        float d2 = Dm.bitand(in_dir, 12);  // eg. west   (1+8)&12 (0000 1100) = 8 (0000 1000)

        foreach (object d in Dm.each(Dm.list(d1, d2)))
        {
            if (!Dm.truthy(await T0.ClickCross(d, TRUE, target, mover)))
                continue; // could not leave T0 in that direction

            turf T1 = get_step(T0, Dm.num(d));
            if (!Dm.truthy(T1) || Dm.truthy(T1.density))
                continue;
            if (!Dm.truthy(await T1.ClickCross(get_dir(T1, this), FALSE, target, mover)) || !Dm.truthy(await T1.ClickCross(get_dir(T1, T0), FALSE, target, mover)))
                continue; // couldn't enter or couldn't leave T1

            if (!Dm.truthy(await ClickCross(get_dir(this, T1), TRUE, target, mover)))
                continue; // could not enter src

            return TRUE; // we don't care about our own density
        }

        return FALSE;
    }
}

// DEVIATION(DV425): in 3D a thing on the level above is in reach through an open column (a hole, the stairs' opening): a turf one level
// above the other is adjacent to it when the lower one opens up into the level above (tg's zPassOut and zPassIn, no ceiling of the port's)
// and the upper turf is adjacent, by tg's rules on its level, to the open turf over the lower one. The reach goes up only (the owner,
// 2026-11-14): the mob of the pair (the mover a mob's own Adjacent passes, else the neighbor) stands on the lower turf; with no mob in it
// the other level stays out of reach. tg's Adjacent never reaches another level.
public partial class turf : atom
{
    private async DmTask<object> AdjacentAcrossLevels(turf other, atom neighbor, atom target, atom_movable mover)
    {
        float dz = Dm.num(other.z) - Dm.num(z);
        if (Math.Abs(dz) != 1)
            return FALSE;
        (turf lower, turf upper) = dz > 0 ? (this, other) : (other, this);
        atom reacher = ismob(mover) ? mover : ismob(neighbor) ? neighbor : null;
        if (reacher == null || get_turf(reacher) != lower)
            return FALSE; // up only: a mob reaches up into the level above, never down into the one below
        if (!FlightSeam.OpensUp(lower) || GET_TURF_ABOVE(lower) is not turf above || above.z != upper.z)
            return FALSE;
        return above == upper ? TRUE : await upper.Adjacent(above, target, mover);
    }
}

/*
	Adjacency (to anything else):
	* Must be on a turf
*/
// tg: code/_onclick/adjacent.dm:69
public partial class atom_movable : atom
{
    // tg: code/_onclick/adjacent.dm:69
    public override async DmTask<object> Adjacent(atom neighbor = null, atom target = null, atom_movable mover = null, params object[] __extra)
    {
        if (neighbor == loc)
            return TRUE;
        if (neighbor?.loc == this)
            return TRUE;
        turf T = loc as turf;
        if (!(T is turf))
            return FALSE;
        if (Dm.truthy(await T.Adjacent(neighbor, target: neighbor, mover: this)))
            return TRUE;
        return FALSE;
    }
}

// This is necessary for storage items not on your person.
// tg: code/_onclick/adjacent.dm:81
[DmPath("/obj/item")]
public partial class obj_item : obj
{
    // tg: code/_onclick/adjacent.dm:81
    [DmParams("neighbor", "target", "mover", "recurse")]
    public override async DmTask<object> Adjacent(atom neighbor = null, atom target = null, atom_movable mover = null, params object[] __extra)
    {
        float? recurse = Dm.arg<float?>(__extra, 0);
        recurse ??= 1;
        if (neighbor == loc)
            return TRUE;
        if (neighbor?.loc == this)
            return TRUE;
        if (isitem(loc))
        {
            if (Dm.num(recurse) > 0)
                return await loc.Adjacent(neighbor, target, mover, Dm.num(recurse) - 1);
            return FALSE;
        }
        return await base.Adjacent(neighbor, target, mover, Dm.forward(__extra, recurse));
    }
}

/*
	This checks if you there is uninterrupted airspace between that turf and this one.
This is defined as any dense ON_BORDER_1 object, or any dense object without LETPASSTHROW or LETPASSCLICKS.
	The border_only flag allows you to not objects (for source and destination squares)
*/
// tg: code/_onclick/adjacent.dm:97
public partial class turf : atom
{
    // tg: code/_onclick/adjacent.dm:97
    public virtual async DmTask<object> ClickCross(object target_dir = null, object border_only = null, atom target = null, atom_movable mover = null)
    {
        foreach (obj O in Dm.each<obj>(this))
        {
            if ((Dm.truthy(mover) && Dm.truthy(await O.CanPass(mover, target_dir))) || (!Dm.truthy(mover) && !Dm.truthy(O.density)))
                continue;

            //If there's a dense object on the turf, only allow the click to pass if you can throw items over it or it has a special flag.
            if (O == target || O == mover || Dm.truthy(Dm.bitand(O.pass_flags_self, Dm.bitor(LETPASSTHROW, LETPASSCLICKS))))
                continue;

            if (Dm.truthy(Dm.bitand(O.flags_1, ON_BORDER_1))) // windows are on border, check them first
            {
                if (Dm.truthy(Dm.bitand(O.dir, target_dir)) || Dm.truthy(Dm.bitand(O.dir, O.dir - 1))) // full tile windows are just diagonals mechanically
                    return FALSE;   //O.dir&(O.dir-1) is false for any cardinal direction, but true for diagonal ones
            }

            else if (!Dm.truthy(border_only)) // dense, not on border, cannot pass over
                return FALSE;
        }

        return TRUE;
    }
}
