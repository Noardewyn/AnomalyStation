// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/climbable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8z slice C (D239): the whole file; the athletics skill's level and the experience a climb gives came with gaps SL1 (D333). The var
// block is generated.

// tg: code/datums/elements/climbable.dm:1
public partial class datum_element_climbable : datum_element
{
    // tg: code/datums/elements/climbable.dm:11
    [DmParams("target", "climb_time", "climb_stun")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object climb_time = Dm.arg(__extra, 0) ?? 2 * SECONDS;
        object climb_stun = Dm.arg(__extra, 1) ?? 2 * SECONDS;
        object dot = await base.Attach(target, Dm.forward(__extra, climb_time, climb_stun));

        if (!isatom(target) || isarea(target))
            return ELEMENT_INCOMPATIBLE;
        this.climb_time = climb_time;
        this.climb_stun = climb_stun;

        RegisterSignal(target, COMSIG_ATOM_ATTACK_HAND, nameof(attack_hand));
        RegisterSignal(target, COMSIG_ATOM_EXAMINE_TAGS, nameof(get_examine_tags));
        RegisterSignal(target, COMSIG_MOUSEDROPPED_ONTO, nameof(mousedrop_receive));
        ADD_TRAIT(target, TRAIT_CLIMBABLE, ELEMENT_TRAIT(type));
        return dot;
    }

    // tg: code/datums/elements/climbable.dm:28
    [DmParams("target")]
    public override object Detach(datum target = null, params object[] __extra)
    {
        UnregisterSignal(target, Dm.list(COMSIG_ATOM_ATTACK_HAND, COMSIG_ATOM_EXAMINE_TAGS, COMSIG_MOUSEDROPPED_ONTO, COMSIG_ATOM_BUMPED));
        REMOVE_TRAIT(target, TRAIT_CLIMBABLE, ELEMENT_TRAIT(type));
        return base.Detach(target, __extra);
    }

    ///Someone inspected our embeddable item
    // tg: code/datums/elements/climbable.dm:34
    [SignalHandler]
    public virtual void get_examine_tags(atom source = null, mob user = null, DmList examine_list = null)
    {
        examine_list["climbable"] = I18n.T("It looks like it can be climbed on.");
    }

    // tg: code/datums/elements/climbable.dm:39
    public virtual async DmTask<object> can_climb(atom source = null, mob user = null)
    {
        if (!Dm.truthy(await source.IsReachableBy(user)))
            return FALSE;
        float dir_step = get_dir(user, source.loc);
        //To jump over a railing you have to be standing next to it, not far behind it.
        if (Dm.truthy(Dm.bitand(source.flags_1, ON_BORDER_1)) && user.loc != source.loc && Dm.eq(Dm.bitand(dir_step, source.dir), source.dir))
            return FALSE;
        return TRUE;
    }

    // tg: code/datums/elements/climbable.dm:48
    [SignalHandler]
    public virtual void attack_hand(atom climbed_thing = null, mob user = null)
    {
        DmList climbers = LAZYACCESS(current_climbers, climbed_thing) as DmList;
        foreach (object i in Dm.each(climbers))
        {
            mob_living structure_climber = (mob_living)i;
            if (structure_climber == user)
                return;
            user.changeNext_move(CLICK_CD_MELEE);
            Waiting.NoSleep(user.do_attack_animation(climbed_thing));
            Waiting.NoSleep(structure_climber.Paralyze(40));
            structure_climber.visible_message(span_warning(I18n.T("[] is knocked off [].", structure_climber, climbed_thing)), span_warning(I18n.T("You're knocked off []!", climbed_thing)), span_hear(I18n.T("You hear a cry from [], followed by a slam.", structure_climber)));
        }
    }

    // tg: code/datums/elements/climbable.dm:61
    public virtual async DmTask climb_structure(atom climbed_thing = null, mob_living user = null, object @params = null)
    {
        if (!Dm.truthy(await can_climb(climbed_thing, user)))
            return;
        climbed_thing.add_fingerprint(user);
        user.visible_message(span_warning(I18n.T("[] starts climbing onto [].", user, climbed_thing)),
                                span_notice(I18n.T("You start climbing onto []...", climbed_thing)));
        // Time in deciseoncds it takes to complete the climb do_after()
        float adjusted_climb_time = Dm.num(climb_time);
        // Time in deciseonds that the mob is stunned after climbing successfully.
        float adjusted_climb_stun = Dm.num(climb_stun);
        // Our climbers fitness level, which removes some climb time and speeds up our climbing do_after, assuming they worked out
        float fitness_level = Dm.num(user.mind?.get_skill_level(typeof(datum_skill_athletics))) - 1;
        adjusted_climb_time = Dm.num(clamp(adjusted_climb_time - fitness_level, 1, climb_time)); //Here we adjust the number of deciseconds we shave off per level of fitness, with a minimum of 1 decisecond and a maximum of climb_time (just in case)

        if (user.get_organ_slot(ORGAN_SLOT_SPINE) is obj_item_organ_cyberimp_chest_spine potential_spine)
        {
            adjusted_climb_time *= potential_spine.athletics_boost_multiplier;
            adjusted_climb_stun *= potential_spine.athletics_boost_multiplier;
        }

        if (HAS_TRAIT(user, TRAIT_HANDS_BLOCKED)) //climbing takes twice as long without help from the hands.
            adjusted_climb_time *= 2;
        if (isalien(user))
            adjusted_climb_time *= 0.25f; //aliens are terrifyingly fast
        if (HAS_TRAIT(user, TRAIT_FREERUNNING)) //do you have any idea how fast I am???
        {
            adjusted_climb_time *= 0.8f;
            adjusted_climb_stun *= 0.8f;
        }
        if (HAS_TRAIT(user, TRAIT_STUBBY_BODY)) //hold on, gimme a moment, my tiny legs can't get over the goshdamn table
        {
            adjusted_climb_time *= 1.5f;
            adjusted_climb_stun *= 1.5f;
        }
        LAZYADDASSOCLIST(ref current_climbers, climbed_thing, user);
        if (Dm.truthy(await do_after(user, adjusted_climb_time, climbed_thing)))
        {
            if (QDELETED(climbed_thing)) //Checking if structure has been destroyed
                return;
            if (Dm.truthy(await do_climb(climbed_thing, user, @params)))
            {
                user.visible_message(span_warning(I18n.T("[] climbs onto [].", user, climbed_thing)),
                                    span_notice(I18n.T("You climb onto [].", climbed_thing)));
                log_combat(user, climbed_thing, "climbed onto");
                if (Dm.truthy(adjusted_climb_stun))
                    await user.Stun(adjusted_climb_stun);
                if (climbed_thing is atom_movable buckle_target)
                {
                    if (Dm.truthy(await buckle_target.is_buckle_possible(user)))
                        await buckle_target.buckle_mob(user);
                }
                user.mind?.adjust_experience(typeof(datum_skill_athletics), round(Dm.div(ATHLETICS_SKILL_MISC_EXP, (fitness_level is var or__1 && Dm.truthy(or__1) ? or__1 : 1)), 1)); //Get a bit fitter with every climb. But it has diminishing returns at a certain point.
            }
            else
                to_chat(user, span_warning(I18n.T("You fail to climb onto [].", climbed_thing)));
        }
        LAZYREMOVEASSOC(ref current_climbers, climbed_thing, user);
    }

    // tg: code/datums/elements/climbable.dm:119
    public virtual async DmTask<object> do_climb(atom climbed_thing = null, mob_living user = null, object @params = null)
    {
        object dot = null;
        if (!Dm.truthy(await can_climb(climbed_thing, user)))
            return dot;
        climbed_thing.set_density(FALSE);
        float dir_step = get_dir(user, climbed_thing.loc);
        bool same_loc = climbed_thing.loc == user.loc;
        // on-border objects can be vaulted over and into the next turf.
        // The reverse dir check is for when normal behavior should apply instead (e.g. John Doe hops east of a railing facing west, ending on the same turf as it).
        if (Dm.truthy(Dm.bitand(climbed_thing.flags_1, ON_BORDER_1)) && (same_loc || !Dm.truthy(Dm.bitand(dir_step, REVERSE_DIR(Dm.num(climbed_thing.dir))))))
        {
            //it can be vaulted over in two different cardinal directions. we choose one.
            if (Dm.truthy(ISDIAGONALDIR(Dm.num(climbed_thing.dir))) && same_loc)
            {
                if (Dm.truthy(@params)) //we check the icon x and y parameters of the click-drag to determine step_dir.
                {
                    DmList modifiers = params2list(@params);
                    float x_dist = (Dm.num(text2num(LAZYACCESS(modifiers, ICON_X))) - ICON_SIZE_X / 2) * (Dm.truthy(Dm.bitand(climbed_thing.dir, WEST)) ? -1 : 1);
                    float y_dist = (Dm.num(text2num(LAZYACCESS(modifiers, ICON_Y))) - ICON_SIZE_Y / 2) * (Dm.truthy(Dm.bitand(climbed_thing.dir, SOUTH)) ? -1 : 1);
                    dir_step = Dm.bitand(x_dist >= y_dist ? Dm.bitor(EAST, WEST) : Dm.bitor(NORTH, SOUTH), climbed_thing.dir);
                }
            }
            else
                dir_step = get_dir(user, get_step(climbed_thing, Dm.num(climbed_thing.dir)));
        }
        dot = await step(user, dir_step);
        climbed_thing.set_density(TRUE);
        return dot;
    }

    ///Handles climbing onto the atom when you click-drag
    // tg: code/datums/elements/climbable.dm:142
    [SignalHandler]
    public virtual object mousedrop_receive(atom climbed_thing = null, atom_movable dropped_atom = null, mob user = null, object @params = null)
    {
        if (user != dropped_atom || !isliving(dropped_atom))
            return null;
        if (!HAS_TRAIT(dropped_atom, TRAIT_FENCE_CLIMBER) && !HAS_TRAIT(dropped_atom, TRAIT_CAN_HOLD_ITEMS)) // If you can hold items you can probably climb a fence
            return null;
        mob_living living_target = (mob_living)dropped_atom;
        if (Dm.truthy(Dm.bitand(living_target.mobility_flags, MOBILITY_MOVE)))
            Waiting.Detach(climb_structure(climbed_thing, living_target, @params));
        return COMPONENT_CANCEL_MOUSEDROPPED_ONTO;
    }
}
