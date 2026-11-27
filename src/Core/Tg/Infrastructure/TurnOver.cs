using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// DEVIATION(DV600): the radial's Turn over (package T, slice T4c). Not in tg: someone next to a lying carbon turns it
// face up or face down, a timed action of tg's do_after (1.5 seconds, tg's progress bar and interruptions), so the eyes and the mouth of a conscious
// lying person can be aimed at by looking. The side is the port's own state on the mob (dm_turned_side, as DV96's dm_shots_landed), sent in the
// posture byte (PostureBits.SideFixed and FaceUp) and read by the frame choice of the clients and of the server's shots (ShotGeometry.ChooseLyingDir).

public partial class mob_living
{
    /// <summary>
    /// DV600: the side a lying body was turned to from the radial's Turn over, as the frame it shows: <c>SOUTH</c> face up (its front),
    /// <c>NORTH</c> face down (its back), 0 when no one turned it. Forgotten when the body stands up (<c>set_body_position</c>); it dies with the mob.
    /// </summary>
    public float dm_turned_side;

    /// <summary>DV600: the turned side as the posture byte's (<c>PostureBits.TurnedFaceUp</c>): true face up, false face down, null not turned.</summary>
    public bool? dm_turned_face_up => dm_turned_side == SOUTH ? true : dm_turned_side == NORTH ? false : null;

    /// <summary>DV600 (and DV573): lying buckled to an object (a bed, an operating table), the posture byte's lying and buckled bits together.</summary>
    public bool dm_lies_buckled => body_position == LYING_DOWN && buckled is not null and not mob_living;

    /// <summary>
    /// DV600: whether the radial offers Turn over on this mob to <paramref name="user"/> (RadialExtras.Of, which has checked the mob is next to
    /// him and not himself): a carbon (its lying picture has a front and a back) lying on a turf.
    /// </summary>
    public bool dm_can_be_turned_over => this is mob_living_carbon && body_position == LYING_DOWN && !QDELETED(this) && isturf(loc);

    /// <summary>
    /// DV600: the do_after's <c>extra_checks</c> (silent, every tick): the target still a lying carbon next to <paramref name="user"/>, and the user
    /// a living mob with a free hand (<see cref="dm_has_free_hand"/>). tg's do_after itself ends it when either moves, the user is incapacitated or
    /// his active hand's item changes, and when either is deleted.
    /// </summary>
    public virtual object turn_over_checks(mob user = null)
    {
        if (user is not mob_living actor || QDELETED(actor) || actor == this || !dm_can_be_turned_over || !dm_has_free_hand(actor))
            return FALSE;
        return Dm.truthy(Waiting.NoSleep(Adjacent(actor))) ? TRUE : FALSE;
    }

    /// <summary>
    /// DV600: tg's usual free hand: the hands not blocked (<c>TRAIT_HANDS_BLOCKED</c>: cuffed, stunned, in crit) and an empty held slot that has a
    /// hand (<c>get_empty_held_indexes</c>, each asked <c>has_hand_for_held_index</c> as tg's choke does, choke.dm:234: a lost or disabled arm's
    /// empty slot is no free hand).
    /// </summary>
    public static bool dm_has_free_hand(mob_living actor)
    {
        if (HAS_TRAIT(actor, TRAIT_HANDS_BLOCKED) || actor.get_empty_held_indexes() is not DmList empty)
            return false;
        foreach (object index in Dm.each<object>(empty))
            if (Dm.truthy(actor.has_hand_for_held_index(index)))
                return true;
        return false;
    }

    // At the pick and at the end: the silent checks, then tg's check of a hands-on action (can_perform_action with NEED_HANDS and
    // FORBID_TELEKINESIS_REACH: conscious, not incapacitated, the mobility of the hands, the hands not blocked, within reach), which tells the user why not.
    private async DmTask<bool> turn_over_allowed(mob user)
        => Dm.truthy(turn_over_checks(user)) && Dm.truthy(await user.can_perform_action(this, Dm.bitor(NEED_HANDS, FORBID_TELEKINESIS_REACH)));

    /// <summary>
    /// DV600: the radial's Turn over picked by <paramref name="user"/> (RadialExtras.Pick): tg's do_after of 1.5 seconds on this mob, one at a time
    /// per user and mob (tg's interaction key, the target), with "starts turning ... over." when it begins and "turns ... over." when it ends;
    /// interrupted, nothing changes and nothing more is said. The first turn shows the side the body does not show by default: face down for one
    /// lying on a bed or a table (drawn face up), face up otherwise (a conscious body on the floor is drawn face down, a limp one either way); a
    /// turned body flips.
    /// </summary>
    public async DmTask radial_turn_over(mob user)
    {
        if (Dm.truthy(DOING_INTERACTION_WITH_TARGET(user, this))) // tg's do_after would refuse a second one silently; so does its start line
            return;
        if (!await turn_over_allowed(user))
            return;
        user.visible_message(span_notice(I18n.T("[] starts turning [] over.", user, this)), span_notice(I18n.T("You start turning [] over.", this)));
        if (!Dm.truthy(await do_after(user, 1.5f * SECONDS, this, extra_checks: Dm.@new<datum_callback>(this, nameof(turn_over_checks), user))))
            return;
        if (!await turn_over_allowed(user))
            return;
        dm_turned_side = dm_turned_side == SOUTH ? NORTH : dm_turned_side == NORTH ? SOUTH : dm_lies_buckled ? NORTH : SOUTH;
        dm_appearance_changed(AppearanceAspect.Transform); // the replicator reads the posture again (a mob with no capsule is not polled)
        user.visible_message(span_notice(I18n.T("[] turns [] over.", user, this)), span_notice(I18n.T("You turn [] over.", this)));
    }
}
