// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/click.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: the click layer (M2 P4, scope 2f, Q8; M4a P1: the item branches of ClickOn, the modifier clicks, the reach
// checkers and the examine) translated whole but for the BYOND map screen's click catcher (:460-505, the 2D map's
// background: the 3D client clicks by a ray, scope 2f; never as such, rules 13) and the ghost's mouse wheel (:511, ghosts
// M10), which stay Dm.NotTranslated. Pointing (ShiftMiddleClickOn, M9) is a Dm.NotTranslated block.

// tg: code/_onclick/click.dm:453 (M1 contracts: the type the /client var block names, client_defines.dm:102)
[DmPath("/atom/movable/screen")]
public partial class atom_movable_screen : atom_movable
{
}

// tg: code/_onclick/click.dm:460
[DmPath("/atom/movable/screen/click_catcher")]
public partial class atom_movable_screen_click_catcher : atom_movable_screen
{
}

// tg: code/_onclick/click.dm:511
[DmPath("/mob/dead/observer")]
public partial class mob_dead_observer : mob_dead
{
}

// tg: code/_onclick/click.dm:8
public partial class mob : atom_movable
{
    // 1 decisecond click delay (above and beyond mob/next_move)
    //This is mainly modified by click code, to modify click delays elsewhere, use next_move and changeNext_move()
    // tg: code/_onclick/click.dm:8
    public float next_click = 0;

    // THESE DO NOT EFFECT THE BASE 1 DECISECOND DELAY OF NEXT_CLICK
    // tg: code/_onclick/click.dm:11
    public float next_move_adjust = 0; //Amount to adjust action/click delays by, + or -
    // tg: code/_onclick/click.dm:12
    public float next_move_modifier = 1; //Value to multiply action/click delays by

    //Delays the mob's next click/action by num deciseconds
    // eg: 10-3 = 7 deciseconds of delay
    // eg: 10*0.5 = 5 deciseconds of delay
    // DOES NOT EFFECT THE BASE 1 DECISECOND DELAY OF NEXT_CLICK

    // tg: code/_onclick/click.dm:20
    public virtual void changeNext_move(object num = null)
    {
        next_move = Dm.add(world.time, Dm.mul(Dm.add(num, next_move_adjust), next_move_modifier));
    }
}

// tg: code/_onclick/click.dm:23
public partial class mob_living : mob
{
    // tg: code/_onclick/click.dm:23
    public override void changeNext_move(object num = null)
    {
        float mod = next_move_modifier;
        float adj = next_move_adjust;
        foreach (datum_status_effect effect in Dm.each_cast<datum_status_effect>(status_effects))
        {
            mod *= Dm.num(effect.nextmove_modifier());
            adj += Dm.num(effect.nextmove_adjust());
        }
        next_move = Dm.add(world.time, Dm.mul(Dm.add(num, adj), mod));
        if (Dm.truthy(this._listen_lookup?[COMSIG_LIVING_CHANGENEXT_MOVE]))
            this._SendSignal(COMSIG_LIVING_CHANGENEXT_MOVE, Dm.list(this, next_move, num));
    }

    // tg: code/_onclick/click.dm:32
    public virtual void change_next_special_move(object num = null)
    {
        changeNext_move(num);
        last_special = world.time + Dm.num(num);
    }
}

// tg: code/_onclick/click.dm:45
public partial class atom : datum
{
    /**
     * Before anything else, defer these calls to a per-mobtype handler.  This allows us to
     * remove istype() spaghetti code, but requires the addition of other handler procs to simplify it.
     *
     * Alternately, you could hardcode every mob's variation in a flat [/mob/proc/ClickOn] proc; however,
     * that's a lot of code duplication and is hard to maintain.
     *
     * Note that this proc can be overridden, and is in the case of screen objects.
     */
    // tg: code/_onclick/click.dm:45
    public virtual async DmTask<object> Click(object location = null, object control = null, object @params = null)
    {
        if (Dm.truthy(Dm.bitand(flags_1, INITIALIZED_1)))
        {
            if (Dm.truthy(this._listen_lookup?[COMSIG_CLICK]))
                this._SendSignal(COMSIG_CLICK, Dm.list(this, location, control, @params, usr));

            await usr.ClickOn(this, @params);
        }
        return null;
    }

    // tg: code/_onclick/click.dm:51
    public virtual async DmTask DblClick(object location = null, object control = null, object @params = null)
    {
        if (Dm.truthy(Dm.bitand(flags_1, INITIALIZED_1)))
            await usr.DblClickOn(this, @params);
    }

    // tg: code/_onclick/click.dm:55
    public virtual void MouseWheel(object delta_x = null, object delta_y = null, object location = null, object control = null, object @params = null)
    {
        if (Dm.truthy(Dm.bitand(flags_1, INITIALIZED_1)))
            usr.MouseWheelOn(this, delta_x, delta_y, @params);
    }
}

// tg: code/_onclick/click.dm:71
public partial class mob : atom_movable
{
    /**
     * Standard mob ClickOn()
     *
     * After that, mostly just check your state, check whether you're holding an item,
     * check whether you're adjacent to the target, then pass off the click to whoever is receiving it.
     *
     * The most common are:
     * * [mob/proc/UnarmedAttack] (atom,adjacent) - used here only when adjacent, with no item in hand; in the case of humans, checks gloves
     * * [atom/proc/attackby] (item,user) - used only when adjacent
     * * [obj/item/proc/afterattack] (atom,user,adjacent,params) - used both ranged and adjacent
     * * [mob/proc/RangedAttack] (atom,modifiers) - used only ranged, only used for tk and laser eyes but could be changed
     */
    // tg: code/_onclick/click.dm:71
    public virtual async DmTask ClickOn(atom A = null, object @params = null)
    {
        if (world.time <= next_click)
            return;
        next_click = world.time + 1;

        if (DevCommands.ShieldsFrom(this, A))
            return; // DEVIATION(DV258): the dev invulnerability

        if (Dm.truthy(await check_click_intercept(@params, A)) || HAS_TRAIT(this, TRAIT_NO_TRANSFORM))
            return;

        DmList modifiers = params2list(@params);
        dm_attack_modifiers = modifiers;
        dm_attack_target = A;
        dm_attack_tick = Anomaly.Core.Kernel.World.Current.Tick;

        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_MOB_CLICKON]) ? (object)NONE : this._SendSignal(COMSIG_MOB_CLICKON, Dm.list(this, A, modifiers))), COMSIG_MOB_CANCEL_CLICKON)))
            return;

        if (Dm.truthy(LAZYACCESS(modifiers, BUTTON4)) || Dm.truthy(LAZYACCESS(modifiers, BUTTON5)))
            return;

        if (Dm.truthy(LAZYACCESS(modifiers, SHIFT_CLICK)))
        {
            if (Dm.truthy(LAZYACCESS(modifiers, MIDDLE_CLICK)))
            {
                await ShiftMiddleClickOn(A);
                return;
            }
            if (Dm.truthy(LAZYACCESS(modifiers, CTRL_CLICK)))
            {
                await CtrlShiftClickOn(A);
                return;
            }
            if (Dm.truthy(LAZYACCESS(modifiers, ALT_CLICK)))
            {
                await alt_shift_click_on(A);
                return;
            }
            await ShiftClickOn(A);
            return;
        }
        if (Dm.truthy(LAZYACCESS(modifiers, MIDDLE_CLICK)))
        {
            if (Dm.truthy(LAZYACCESS(modifiers, CTRL_CLICK)))
                await CtrlMiddleClickOn(A);
            else
                MiddleClickOn(A, @params);
            return;
        }
        if (Dm.truthy(LAZYACCESS(modifiers, ALT_CLICK))) // alt and alt-gr (rightalt)
        {
            if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
                await AltClickSecondaryOn(A);
            else
                await AltClickOn(A);
            return;
        }
        if (Dm.truthy(LAZYACCESS(modifiers, CTRL_CLICK)))
        {
            await CtrlClickOn(A);
            return;
        }

        if (Dm.truthy(INCAPACITATED_IGNORING(this, Dm.bitor(INCAPABLE_RESTRAINTS, INCAPABLE_STASIS))))
            return;

        face_atom(A);

        if (Dm.gt(next_move, world.time)) // in the year 2000...
            return;

        if (!Dm.truthy(LAZYACCESS(modifiers, "catcher")) && Dm.truthy(A.IsObscured()))
            return;

        if (HAS_TRAIT(this, TRAIT_HANDS_BLOCKED))
        {
            changeNext_move(CLICK_CD_HANDCUFFED); //Doing shit in cuffs shall be vey slow
            await UnarmedAttack(A, await Adjacent(A), modifiers);
            return;
        }

        if (Dm.truthy(throw_mode))
        {
            if (Dm.truthy(await throw_item(A)))
                changeNext_move(CLICK_CD_THROW);
            return;
        }

        obj_item W = get_active_held_item() as obj_item;

        if (W == A)
        {
            if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
            {
                await W.attack_self_secondary(this, modifiers);
                await update_held_items();
                return;
            }
            else
            {
                await W.attack_self(this, modifiers);
                await update_held_items();
                return;
            }
        }

        //These are always reachable.
        //User itself, current loc, and user inventory
        if (Dm.@in(A, DirectAccess()))
        {
            if (Dm.truthy(W))
                await W.melee_attack_chain(this, A, modifiers);
            else
            {
                if (ismob(A))
                    changeNext_move(CLICK_CD_MELEE);

                await UnarmedAttack(A, TRUE, modifiers);
            }
            return;
        }

        //Can't reach anything else in lockers or other weirdness
        if (!Dm.truthy(loc.AllowClick()))
            return;

        // In a storage item with a disassociated storage parent
        obj_item item_atom = A as obj_item;
        if (item_atom is obj_item)
        {
            if (Dm.truthy(Dm.bitand(item_atom.item_flags, IN_STORAGE)) && Dm.truthy(Dm.bitand(item_atom.loc.flags_1, HAS_DISASSOCIATED_STORAGE_1)))
                await UnarmedAttack(item_atom, TRUE, modifiers);
        }

        //Standard reach turf to turf or reaching inside storage
        if (Dm.truthy(await A.IsReachableBy(this, W?.reach)))
        {
            if (Dm.truthy(W))
                await W.melee_attack_chain(this, A, modifiers);
            else
            {
                if (ismob(A))
                    changeNext_move(CLICK_CD_MELEE);
                await UnarmedAttack(A, TRUE, modifiers);
            }
        }
        else
        {
            if (Dm.truthy(W))
                await A.base_ranged_item_interaction(this as mob_living, W, modifiers);
            else
            {
                if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
                    await ranged_secondary_attack(A, modifiers);
                else
                    await RangedAttack(A, modifiers);
            }
        }
    }
}

// tg: code/_onclick/click.dm:188
public partial class atom : datum
{
    /// Is the atom obscured by a PREVENT_CLICK_UNDER_1 object above it
    // tg: code/_onclick/click.dm:188
    [ShouldBePure]
    public virtual object IsObscured()
    {
        if (!isturf(loc)) //This only makes sense for things directly on turfs for now
            return FALSE;
        // M4a P1: get_turf_pixel is translated (its icon-size term reads 32x32, __HELPERS/turfs.cs; DV18 narrowed to it).
        turf click_loc = get_turf_pixel(this) as turf;
        if (click_loc == null)
            return FALSE;
        foreach (atom_movable blocker in Dm.each_cast<atom_movable>(click_loc))
        {
            if (!Dm.truthy(blocker.density) || !Dm.truthy(Dm.bitand(blocker.flags_1, PREVENT_CLICK_UNDER_1)))
                continue;
            if (COMPARE_LAYERS(blocker.layer, this.layer))
                return TRUE;
        }
        return FALSE;
    }
}

// tg: code/_onclick/click.dm:202
public partial class turf : atom
{
    // tg: code/_onclick/click.dm:202
    public override object IsObscured()
    {
        foreach (atom_movable blocker in Dm.each_cast<atom_movable>(this))
        {
            if (Dm.truthy(Dm.bitand(blocker.flags_1, PREVENT_CLICK_UNDER_1)))
                return TRUE;
        }
        return FALSE;
    }
}

// tg: code/_onclick/click.dm:217
public partial class atom : datum
{
    /**
     * Returns TRUE if a movable can "Reach" this atom. This is defined as adjacency
     *
     * Args:
     * * user: The movable trying to reach us.
     * * reacher_range: How far the reacher can reach.
     * * depth: How deep nested inside of an atom contents stack an object can be.
     * * direct_access: Do not override. Used for recursion.
     */
    // tg: code/_onclick/click.dm:217
    [ShouldNotOverride]
    public virtual async DmTask<object> IsReachableBy(atom_movable user = null, float? reacher_range = null, float? depth = null, object direct_access = null)
    {
        reacher_range ??= 1;
        depth ??= INFINITY;
        direct_access ??= user.DirectAccess();

        if (user == null)
            return FALSE;

        if (Dm.@in(this, direct_access))
            return TRUE;

        // This is a micro-opt, if any turf ever returns false from IsContainedAtomAccessible, change this.
        if (isturf(loc) || isturf(this))
        {
            if (Dm.truthy(await CheckReachableAdjacency(user, reacher_range)))
                return TRUE;
        }

        depth--;

        if (depth <= 0)
            return FALSE;

        if (loc == null || isarea(loc))
            return FALSE;
        if (!HAS_TRAIT(this, TRAIT_SKIP_BASIC_REACH_CHECK) && !Dm.truthy(loc.IsContainedAtomAccessible(this, user)))
            return FALSE;

        return await loc.IsReachableBy(user, reacher_range, depth, direct_access);
    }

    // tg: code/_onclick/click.dm:243
    public virtual async DmTask<object> CheckReachableAdjacency(atom_movable reacher = null, object reacher_range = null)
    {
        if (Dm.truthy(await reacher.Adjacent(this)))
            return TRUE;

        if (isliving(reacher))
        {
            mob_living living_reacher = reacher as mob_living;
            if (Dm.gt(living_reacher.reach_length, reacher_range))
                reacher_range = living_reacher.reach_length;
        }

        return (Dm.num(Dm.gt(reacher_range, 1)) is var and__1 && !Dm.truthy(and__1) ? (object)and__1 : await RangedReachCheck(reacher, this as atom_movable, reacher_range));
    }
}

// tg: code/_onclick/click.dm:255 (global procs)
public static partial class GlobalProcs
{
    /// Called by IsReachableBy() to check for ranged reaches.
    // tg: code/_onclick/click.dm:255
    public static async DmTask<object> RangedReachCheck(atom_movable here = null, atom_movable there = null, object reach = null)
    {
        if (!Dm.truthy(here) || !Dm.truthy(there))
            return FALSE;

        if (Dm.le(reach, 1))
            return FALSE;

        object dot = null;
        // Prevent infinite loop.
        if (istype(here, typeof(obj_effect_abstract_reach_checker)))
            return FALSE;

        obj_effect_abstract_reach_checker dummy = Dm.@new<obj_effect_abstract_reach_checker>(get_turf(here));
        for (float i = 1, i__end = Dm.num(reach); i <= i__end; i++) //Limit it to that many tries
        {
            turf T = get_step(dummy, get_dir(dummy, there));
            if (Dm.truthy(await there.IsReachableBy(dummy)))
            {
                dot = TRUE;
                break;
            }

            if (!Dm.truthy(await dummy.Move(T))) //we're blocked!
                break;
        }

        qdel(dummy);
        return dot;
    }
}

// tg: code/_onclick/click.dm:279
public partial class atom : datum
{
    /// Returns TRUE if an atom contained within our contents is reachable.
    // tg: code/_onclick/click.dm:279
    public virtual object IsContainedAtomAccessible(atom contained = null, atom_movable user = null)
    {
        return TRUE;
    }
}

// tg: code/_onclick/click.dm:282
public partial class atom_movable : atom
{
    // tg: code/_onclick/click.dm:282
    public override object IsContainedAtomAccessible(atom contained = null, atom_movable user = null)
    {
        return Dm.not(Dm.not(atom_storage));
    }
}

// tg: code/_onclick/click.dm:285
public partial class mob_living : mob
{
    // tg: code/_onclick/click.dm:285
    public override object IsContainedAtomAccessible(atom contained = null, atom_movable user = null)
    {
        return Dm.not(Dm.not(contained.atom_storage));
    }
}

// tg: code/_onclick/click.dm:288
public partial class atom : datum
{
    // tg: code/_onclick/click.dm:288
    public virtual object DirectAccess(params object[] __extra)
    {
        return Dm.list(this, loc);
    }
}

// tg: code/_onclick/click.dm:291
public partial class mob : atom_movable
{
    // tg: code/_onclick/click.dm:291
    [DmParams("target")]
    public override object DirectAccess(params object[] __extra)
    {
        return Dm.add(base.DirectAccess(__extra), contents);
    }
}

// tg: code/_onclick/click.dm:294
public partial class mob_living : mob
{
    // tg: code/_onclick/click.dm:294
    [DmParams("target")]
    public override object DirectAccess(params object[] __extra)
    {
        return Dm.add(base.DirectAccess(__extra), get_all_contents());
    }
}

// tg: code/_onclick/click.dm:297
public partial class atom : datum
{
    // tg: code/_onclick/click.dm:297
    public virtual object AllowClick()
    {
        return FALSE;
    }
}

// tg: code/_onclick/click.dm:300
public partial class turf : atom
{
    // tg: code/_onclick/click.dm:300
    public override object AllowClick()
    {
        return TRUE;
    }
}

// tg: code/_onclick/click.dm:303 (global procs)
public static partial class GlobalProcs
{
    // tg: code/_onclick/click.dm:303
    public static async DmTask<object> CheckToolReach(atom_movable here = null, atom_movable there = null, object reach = null)
    {
        if (!Dm.truthy(here) || !Dm.truthy(there))
            return null;
        turf turf = get_turf(here);
        if (turf.z != there.z)
            return FALSE;
        object sw__1 = reach;
        if (Dm.eq(sw__1, 0))
            return FALSE;
        else if (Dm.eq(sw__1, 1))
            return FALSE; //here.Adjacent(there)
        else if (Dm.in_to(sw__1, 2, INFINITY))
        {
            obj dummy = Dm.@new<obj>(turf);
            dummy.pass_flags = Dm.bitor(dummy.pass_flags, PASSTABLE);
            dummy.SetInvisibility(INVISIBILITY_ABSTRACT);
            for (float i = 1, i__end = Dm.num(reach); i <= i__end; i++) //Limit it to that many tries
            {
                turf T = get_step(dummy, get_dir(dummy, there));
                if (Dm.truthy(await there.IsReachableBy(dummy)))
                {
                    qdel(dummy);
                    return TRUE;
                }
                if (!Dm.truthy(await dummy.Move(T))) //we're blocked!
                {
                    qdel(dummy);
                    return null;
                }
            }
            qdel(dummy);
        }
        return null;
    }
}

// tg: code/_onclick/click.dm:329
public partial class mob : atom_movable
{
    /// Default behavior: ignore double clicks (the second click that makes the doubleclick call already calls for a normal click)
    // tg: code/_onclick/click.dm:329
    public virtual async DmTask DblClickOn(atom A = null, object @params = null)
    {
        return;
    }

    /**
     * UnarmedAttack: The higest level of mob click chain discounting click itself.
     *
     * This handles, just "clicking on something" without an item. It translates
     * into [atom/proc/attack_hand], [atom/proc/attack_animal] etc.
     *
     * Note: proximity_flag here is used to distinguish between normal usage (flag=1),
     * and usage when clicking on things telekinetically (flag=0).  This proc will
     * not be called at ranged except with telekinesis.
     *
     * proximity_flag is not currently passed to attack_hand, and is instead used
     * in human click code to allow glove touches only at melee range.
     *
     * modifiers is a lazy list of click modifiers this attack had,
     * used for figuring out different properties of the click, mostly right vs left and such.
     */
    // tg: code/_onclick/click.dm:350
    public virtual async DmTask<object> UnarmedAttack(atom A = null, object proximity_flag = null, DmList modifiers = null)
    {
        if (ismob(A))
            changeNext_move(CLICK_CD_MELEE);
        return null;
    }

    /**
     * Ranged unarmed attack:
     *
     * This currently is just a default for all mobs, involving
     * laser eyes and telekinesis.  You could easily add exceptions
     * for things like ranged glove touches, spitting alien acid/neurotoxin,
     * animals lunging, etc.
     */
    // tg: code/_onclick/click.dm:363
    public virtual async DmTask<object> RangedAttack(atom A = null, object modifiers = null)
    {
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_MOB_ATTACK_RANGED]) ? (object)NONE : this._SendSignal(COMSIG_MOB_ATTACK_RANGED, Dm.list(this, A, modifiers))), COMPONENT_CANCEL_ATTACK_CHAIN)))
            return TRUE;
        await A.RangedAttackOn(this, modifiers as DmList);
        return null;
    }
}

// tg: code/_onclick/click.dm:371
public partial class atom : datum
{
    /**
     * Atom's version of RangedAttack, for when you want to do something when a mob clicks on this with more sanity than just Click()
     */
    // tg: code/_onclick/click.dm:371
    public virtual async DmTask RangedAttackOn(mob attacker = null, DmList modifiers = null)
    {
        return;
    }
}

// tg: code/_onclick/click.dm:381
public partial class mob : atom_movable
{
    /**
     * Ranged secondary attack
     *
     * If the same conditions are met to trigger RangedAttack but it is
     * instead initialized via a right click, this will trigger instead.
     * Useful for mobs that have their abilities mapped to right click.
     */
    // tg: code/_onclick/click.dm:381
    public virtual async DmTask<object> ranged_secondary_attack(atom target = null, object modifiers = null)
    {
        if (Dm.truthy(Dm.bitand((!Dm.truthy(this._listen_lookup?[COMSIG_MOB_ATTACK_RANGED_SECONDARY]) ? (object)NONE : this._SendSignal(COMSIG_MOB_ATTACK_RANGED_SECONDARY, Dm.list(this, target, modifiers))), COMPONENT_CANCEL_ATTACK_CHAIN)))
            return TRUE;
        return null;
    }

    /**
     * Middle click
     * Mainly used for swapping hands
     */
    // tg: code/_onclick/click.dm:389
    public virtual object MiddleClickOn(atom A = null, object @params = null)
    {
        object dot = null;
        dot = (!Dm.truthy(this._listen_lookup?[COMSIG_MOB_MIDDLECLICKON]) ? (object)NONE : this._SendSignal(COMSIG_MOB_MIDDLECLICKON, Dm.list(this, A, @params)));
        if (Dm.truthy(Dm.bitand(dot, COMSIG_MOB_CANCEL_CLICKON)))
            return dot;
        swap_hand();
        return dot;
    }

    /**
     * Shift click
     * For most mobs, examine.
     * This is overridden in ai.dm
     */
    // tg: code/_onclick/click.dm:400
    public virtual async DmTask ShiftClickOn(atom A = null)
    {
        await A.ShiftClick(this);
        return;
    }
}

// tg: code/_onclick/click.dm:404
public partial class atom : datum
{
    // tg: code/_onclick/click.dm:404
    public virtual async DmTask ShiftClick(mob user = null)
    {
        if (Dm.truthy(this._listen_lookup?[COMSIG_SHIFT_CLICKED_ON]))
            this._SendSignal(COMSIG_SHIFT_CLICKED_ON, Dm.list(this, user));
        object shiftclick_flags = (!Dm.truthy(user._listen_lookup?[COMSIG_CLICK_SHIFT]) ? (object)NONE : user._SendSignal(COMSIG_CLICK_SHIFT, Dm.list(user, this)));
        if (Dm.truthy(Dm.bitand(shiftclick_flags, COMSIG_MOB_CANCEL_CLICKON)))
            return;
        if (Dm.truthy(user.client))
            user.examinate(this);
    }
}

// tg: code/_onclick/click.dm:412
public partial class mob : atom_movable
{
    // tg: code/_onclick/click.dm:412
    public virtual async DmTask<object> TurfAdjacent(turf tile = null)
    {
        return await tile.Adjacent(this);
    }

    // tg: code/_onclick/click.dm:415
    public virtual async DmTask ShiftMiddleClickOn(atom A = null)
    {
        await pointed(A);
        return;
    }

    /*
        Misc helpers
        face_atom: turns the mob towards what you clicked on
    */

    /// Simple helper to face what you clicked on, in case it should be needed in more than one place
    // tg: code/_onclick/click.dm:425
    public virtual void face_atom(atom atom_to_face = null)
    {
        if (Dm.truthy(buckled) || IS_UNCONSCIOUS_OR_CRIT(this) || !Dm.truthy(atom_to_face) || !Dm.truthy(x) || !Dm.truthy(y) || !Dm.truthy(atom_to_face.x) || !Dm.truthy(atom_to_face.y))
            return;
        float dx = atom_to_face.x - x;
        float dy = atom_to_face.y - y;
        if (!Dm.truthy(dx) && !Dm.truthy(dy)) // Wall items are graphically shifted but on the floor
        {
            if (atom_to_face.pixel_y > 16)
                Waiting.NoSleep(setDir(NORTH));
            else if (atom_to_face.pixel_y < -16)
                Waiting.NoSleep(setDir(SOUTH));
            else if (atom_to_face.pixel_x > 16)
                Waiting.NoSleep(setDir(EAST));
            else if (atom_to_face.pixel_x < -16)
                Waiting.NoSleep(setDir(WEST));
            return;
        }

        if (abs(dx) < abs(dy))
        {
            if (dy > 0)
                Waiting.NoSleep(setDir(NORTH));
            else
                Waiting.NoSleep(setDir(SOUTH));
        }
        else
        {
            if (dx > 0)
                Waiting.NoSleep(setDir(EAST));
            else
                Waiting.NoSleep(setDir(WEST));
        }
    }
}

//debug
// tg: code/_onclick/click.dm:453
public partial class atom_movable_screen : atom_movable
{
    // tg: code/_onclick/click.dm:453
    public virtual void scale_to(object x1 = null, object y1 = null)
    {
        if (!Dm.truthy(y1))
            y1 = x1;
        matrix M = Dm.@new<matrix>();
        M.Scale(Dm.num(x1), Dm.num_or_null(y1));
        transform = M;
    }
}

// tg: code/_onclick/click.dm:470
public partial class atom_movable_screen_click_catcher : atom_movable_screen
{
    // tg: code/_onclick/click.dm:470
    public virtual void UpdateGreed(float? view_size_x = null, float? view_size_y = null)
    {
        Dm.NotTranslated("code/_onclick/click.dm:470", "UpdateGreed: the BYOND map screen's click catcher, scaled to the view (icon(), /icon procs; the 3D client clicks by a ray, scope 2f)");
    }

    // tg: code/_onclick/click.dm:491
    [SignalHandler]
    public virtual void offset_increased(datum source = null, object old_offset = null, object new_offset = null)
    {
        Dm.NotTranslated("code/_onclick/click.dm:491", "offset_increased: the click catcher's plane offsets (multi-z planes; the 3D client clicks by a ray, scope 2f)");
    }
}

// tg: code/_onclick/click.dm:485-505: the click catcher's Initialize and Click overrides are not declared: the BYOND map
// screen's click catcher is never made (mob/Login's add_click_catcher is a deferred leaf; the 3D client clicks by a ray).

// tg: code/_onclick/click.dm:508
public partial class mob : atom_movable
{
    /// MouseWheelOn
    // tg: code/_onclick/click.dm:508
    public virtual void MouseWheelOn(atom A = null, object delta_x = null, object delta_y = null, object @params = null)
    {
        if (Dm.truthy(this._listen_lookup?[COMSIG_MOUSE_SCROLL_ON]))
            this._SendSignal(COMSIG_MOUSE_SCROLL_ON, Dm.list(this, A, delta_x, delta_y, @params));
    }
}

// tg: code/_onclick/click.dm:511
public partial class mob_dead_observer : mob_dead
{
    // tg: code/_onclick/click.dm:511
    public override void MouseWheelOn(atom A = null, object delta_x = null, object delta_y = null, object @params = null)
    {
        Dm.NotTranslated("code/_onclick/click.dm:511", "MouseWheelOn: a ghost's shift-scroll zoom (add_view_range; ghosts, M10+)");
    }
}

// tg: code/_onclick/click.dm:521
public partial class mob : atom_movable
{
    // tg: code/_onclick/click.dm:521
    public virtual async DmTask<object> check_click_intercept(object @params = null, object A = null)
    {
        //Client level intercept
        if (Dm.truthy(client?.click_intercept))
        {
            if (Dm.truthy(await Dm.Call(client.click_intercept, "InterceptClickOn", this, @params, A)))
                return TRUE;
        }

        //Mob level intercept
        if (Dm.truthy(click_intercept))
        {
            if (Dm.truthy(await Dm.Call(click_intercept, "InterceptClickOn", this, @params, A)))
                return TRUE;
        }

        return FALSE;
    }
}
