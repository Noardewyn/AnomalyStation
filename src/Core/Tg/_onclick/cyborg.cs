// Translated from tgstation (snapshot 1d99bd7f): code/_onclick/cyborg.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI2, D326).

/*
    Cyborg ClickOn()

    Cyborgs have no range restriction on attack_robot(), because it is basically an AI click.
    However, they do have a range restriction on item use, so they cannot do without the
    adjacency code.
*/

// tg: code/_onclick/cyborg.dm:9
public partial class mob_living_silicon_robot : mob_living_silicon
{
    // tg: code/_onclick/cyborg.dm:9
    public override async DmTask ClickOn(atom A = null, object @params = null)
    {
        if (world.time <= next_click)
            return;
        next_click = world.time + 1;

        if (Dm.truthy(await check_click_intercept(@params, A)))
            return;

        if (IS_UNCONSCIOUS_OR_CRIT(this) || Dm.truthy(lockcharge) || Dm.truthy(IsParalyzed()) || Dm.truthy(IsStun()))
            return;

        DmList modifiers = params2list(@params);
        // the 3D melee geometry reads the click's target and tick (Infrastructure/MeleeGeometry.cs), as the base ClickOn sets them
        dm_attack_modifiers = modifiers;
        dm_attack_target = A;
        dm_attack_tick = Anomaly.Core.Kernel.World.Current.Tick;
        if (Dm.truthy(LAZYACCESS(modifiers, SHIFT_CLICK)))
        {
            if (Dm.truthy(LAZYACCESS(modifiers, CTRL_CLICK)))
            {
                await CtrlShiftClickOn(A);
                return;
            }
            if (Dm.truthy(LAZYACCESS(modifiers, MIDDLE_CLICK)))
            {
                await ShiftMiddleClickOn(A);
                return;
            }
            await ShiftClickOn(A);
            return;
        }
        if (Dm.truthy(LAZYACCESS(modifiers, MIDDLE_CLICK)))
        {
            MiddleClickOn(A, @params);
            return;
        }
        if (Dm.truthy(LAZYACCESS(modifiers, ALT_CLICK))) // alt and alt-gr (rightalt)
        {
            if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
                await AltClickSecondaryOn(A);
            else
                await A.borg_click_alt(this);
            return;
        }
        if (Dm.truthy(LAZYACCESS(modifiers, CTRL_CLICK)))
        {
            await CtrlClickOn(A);
            return;
        }

        if (Dm.num(next_move) >= world.time)
            return;

        face_atom(A); // change direction to face what you clicked on

        obj_item W = get_active_held_item() as obj_item;

        //wireless interaction with an atom
        if (W == null && get_dist(this, A) <= Dm.num(interaction_range))
        {
            if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)) && module_active == null)
            {
                object secondary_result = await A.attack_robot_secondary(this, modifiers);

                if (Dm.eq(secondary_result, SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN) || Dm.eq(secondary_result, SECONDARY_ATTACK_CONTINUE_CHAIN))
                    return;
                if (!Dm.eq(secondary_result, SECONDARY_ATTACK_CALL_NORMAL))
                    throw Dm.CRASH("attack_robot_secondary did not return a SECONDARY_ATTACK_* define.", "code/_onclick/cyborg.dm", 57);
            }
            await A.attack_robot(this, modifiers);
            return;
        }

        if (W != null)
        {
            if (Dm.truthy(incapacitated))
                return;

            //while buckled, you can still connect to and control things like doors, but you can't use your modules
            if (buckled != null)
            {
                to_chat(this, span_warning(I18n.T("You can't use modules while buckled to []!", buckled)));
                return;
            }

            //if your "hands" are blocked you shouldn't be able to use modules
            if (HAS_TRAIT(this, TRAIT_HANDS_BLOCKED))
                return;

            if (W == A)
            {
                await W.attack_self(this, modifiers);
                return;
            }

            // cyborgs are prohibited from using storage items so we can I think safely remove (A.loc in contents)
            if (A == loc || Dm.@in(A, loc) || Dm.@in(A, contents))
            {
                await W.melee_attack_chain(this, A, modifiers);
                return;
            }

            if (!isturf(loc))
                return;

            // cyborg rightclick code, allowing borgos to use weapons at range
            if (Dm.truthy(await A.IsReachableBy(this, W?.reach)))
            {
                await W.melee_attack_chain(this, A, modifiers);
                return;
            }
            else if (isturf(A) || isturf(A.loc))
                await A.base_ranged_item_interaction(this, W, modifiers);
        }
    }

    //Give cyborgs hotkey clicks without breaking existing uses of hotkey clicks
    // for non-doors/apcs
    // tg: code/_onclick/cyborg.dm:96
    public override async DmTask CtrlShiftClickOn(atom target = null)
    {
        await target.BorgCtrlShiftClick(this);
    }

    // tg: code/_onclick/cyborg.dm:99
    public override async DmTask ShiftClickOn(atom target = null)
    {
        await target.BorgShiftClick(this);
    }

    // tg: code/_onclick/cyborg.dm:102
    public override async DmTask CtrlClickOn(atom target = null)
    {
        await target.BorgCtrlClick(this);
    }

    /*
        As with AI, these are not used in click code,
        because the code for robots is specific, not generic.

        If you would like to add advanced features to robot
        clicks, you can do so here, but you will have to
        change attack_robot() above to the proper function
    */
    // tg: code/_onclick/cyborg.dm:193
    public override async DmTask<object> UnarmedAttack(atom A = null, object proximity_flag = null, DmList modifiers = null)
    {
        if (!Dm.truthy(can_unarmed_attack()))
            return null;
        if (Dm.truthy(LAZYACCESS(modifiers, RIGHT_CLICK)))
            return await A.attack_robot_secondary(this, modifiers);
        return await A.attack_robot(this, modifiers);
    }
}

// tg: code/_onclick/cyborg.dm:105
public partial class atom
{
    // tg: code/_onclick/cyborg.dm:105
    public virtual async DmTask BorgCtrlShiftClick(mob_living_silicon_robot user = null) //forward to human click if not overridden
    {
        await user.base_click_ctrl_shift(this);
    }

    // tg: code/_onclick/cyborg.dm:114
    public virtual async DmTask BorgShiftClick(mob_living_silicon_robot user = null) //forward to human click if not overridden
    {
        await ShiftClick(user);
    }

    // tg: code/_onclick/cyborg.dm:123
    public virtual async DmTask BorgCtrlClick(mob_living_silicon_robot user = null) //forward to human click if not overridden
    {
        await user.base_click_ctrl(this);
    }

    // tg: code/_onclick/cyborg.dm:169
    public virtual async DmTask borg_click_alt(mob_living_silicon_robot user = null)
    {
        await user.base_click_alt(this);
        return;
    }

    /**
     * What happens when the cyborg holds left-click on an item.
     *
     * Arguments:
     * * user The mob holding the right click
     * * modifiers The list of the custom click modifiers
     */
    // tg: code/_onclick/cyborg.dm:209
    public virtual async DmTask<object> attack_robot(mob user = null, object modifiers = null)
    {
        if (Dm.truthy(Dm.bitand(!Dm.truthy(_listen_lookup?[COMSIG_ATOM_ATTACK_ROBOT]) ? NONE : _SendSignal(COMSIG_ATOM_ATTACK_ROBOT, Dm.list(this, user, modifiers)), COMPONENT_CANCEL_ATTACK_CHAIN)))
            return null;
        await attack_ai(user);
        return null;
    }

    /**
     * What happens when the cyborg without active module holds right-click on an item. Returns a SECONDARY_ATTACK_* value.
     *
     * Arguments:
     * * user The mob holding the right click
     * * modifiers The list of the custom click modifiers
     */
    // tg: code/_onclick/cyborg.dm:222
    public virtual async DmTask<object> attack_robot_secondary(mob user = null, DmList modifiers = null)
    {
        if (Dm.truthy(Dm.bitand(!Dm.truthy(_listen_lookup?[COMSIG_ATOM_ATTACK_ROBOT_SECONDARY]) ? NONE : _SendSignal(COMSIG_ATOM_ATTACK_ROBOT_SECONDARY, Dm.list(this, user, modifiers)), COMPONENT_CANCEL_ATTACK_CHAIN)))
            return SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN;
        return await attack_ai_secondary(user, modifiers);
    }
}

// tg: code/_onclick/cyborg.dm:108
public partial class obj_machinery_door_airlock : obj_machinery_door
{
    // tg: code/_onclick/cyborg.dm:108
    public override async DmTask BorgCtrlShiftClick(mob_living_silicon_robot user = null) // Sets/Unsets Emergency Access Override Forwards to AI code.
    {
        if (get_dist(this, user) <= Dm.num(user.interaction_range) && !Dm.truthy(user.control_disabled))
            await AICtrlShiftClick(user);
        else
            await base.BorgCtrlShiftClick(user);
    }

    // tg: code/_onclick/cyborg.dm:117
    public override async DmTask BorgShiftClick(mob_living_silicon_robot user = null) // Opens and closes doors! Forwards to AI code.
    {
        if (get_dist(this, user) <= Dm.num(user.interaction_range) && !Dm.truthy(user.control_disabled))
            await AIShiftClick(user);
        else
            await base.BorgShiftClick(user);
    }

    // tg: code/_onclick/cyborg.dm:126
    public override async DmTask BorgCtrlClick(mob_living_silicon_robot user = null) // Bolts doors. Forwards to AI code.
    {
        if (get_dist(this, user) <= Dm.num(user.interaction_range) && !Dm.truthy(user.control_disabled))
            await AICtrlClick(user);
        else
            await base.BorgCtrlClick(user);
    }

    // tg: code/_onclick/cyborg.dm:173
    public override async DmTask borg_click_alt(mob_living_silicon_robot user = null) // Eletrifies doors. Forwards to AI code.
    {
        if (get_dist(this, user) <= Dm.num(user.interaction_range) && !Dm.truthy(user.control_disabled))
            await ai_click_alt(user);
        else
            await base.borg_click_alt(user);
    }
}

// tg: code/_onclick/cyborg.dm:132
public partial class obj_machinery_power_apc : obj_machinery_power
{
    // tg: code/_onclick/cyborg.dm:132
    public override async DmTask BorgCtrlClick(mob_living_silicon_robot user = null) // turns off/on APCs. Forwards to AI code.
    {
        if (get_dist(this, user) <= Dm.num(user.interaction_range) && !Dm.truthy(user.control_disabled))
            await AICtrlClick(user);
        else
            await base.BorgCtrlClick(user);
    }

    // tg: code/_onclick/cyborg.dm:138
    public override async DmTask BorgCtrlShiftClick(mob_living_silicon_robot user = null)
    {
        if (get_dist(this, user) <= Dm.num(user.interaction_range) && !Dm.truthy(user.control_disabled))
            await AICtrlShiftClick(user);
        else
            await base.BorgCtrlShiftClick(user);
    }

    // tg: code/_onclick/cyborg.dm:144
    public override async DmTask BorgShiftClick(mob_living_silicon_robot user = null)
    {
        if (get_dist(this, user) <= Dm.num(user.interaction_range) && !Dm.truthy(user.control_disabled))
            await AIShiftClick(user);
        else
            await base.BorgShiftClick(user);
    }

    // tg: code/_onclick/cyborg.dm:150
    public override async DmTask borg_click_alt(mob_living_silicon_robot user = null)
    {
        if (get_dist(this, user) <= Dm.num(user.interaction_range) && !Dm.truthy(user.control_disabled))
            await ai_click_alt(user);
        else
            await base.borg_click_alt(user);
    }

    // tg: code/_onclick/cyborg.dm:157
    public override async DmTask<object> attack_robot_secondary(mob user = null, DmList modifiers = null)
    {
        mob_living_silicon silicon_user = user as mob_living_silicon;
        if (get_dist(this, user) <= Dm.num(silicon_user.interaction_range) && !Dm.truthy(silicon_user.control_disabled))
            return await attack_ai_secondary(user, modifiers);
        else
            await base.attack_robot_secondary(user, modifiers);
        return null;
    }
}

// tg: code/_onclick/cyborg.dm:163
public partial class obj_machinery_turretid : obj_machinery
{
    // tg: code/_onclick/cyborg.dm:163
    public override async DmTask BorgCtrlClick(mob_living_silicon_robot user = null) //turret control on/off. Forwards to AI code.
    {
        if (get_dist(this, user) <= Dm.num(user.interaction_range) && !Dm.truthy(user.control_disabled))
            await AICtrlClick(user);
        else
            await base.BorgCtrlClick(user);
    }

    // tg: code/_onclick/cyborg.dm:179
    public override async DmTask borg_click_alt(mob_living_silicon_robot user = null) //turret lethal on/off. Forwards to AI code.
    {
        if (get_dist(this, user) <= Dm.num(user.interaction_range) && !Dm.truthy(user.control_disabled))
            await ai_click_alt(user);
        else
            await base.borg_click_alt(user);
    }
}
