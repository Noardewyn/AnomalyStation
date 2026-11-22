// Translated from tgstation (snapshot 1d99bd7f): code/modules/tgui/status_composers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 3 (D148): the whole file except the pAI's get_ui_access override (:91-96: the pAI's radio, with the pAI, silicons SI6);
// the AI's (:84) came with the AI (silicons SI4). /mob/living/silicon's root get_ui_access (:74) is declared; the
// cyborg's view (:77) came with the cyborgs (silicons SI2, D326).

public static partial class GlobalProcs
{
    /// The sane defaults for a UI such as a computer or a machine.
    // tg: code/modules/tgui/status_composers.dm:2
    public static object default_ui_state(mob user = null, atom source = null)
    {
        return Dm.min(
            ui_status_user_is_abled(user, source),
            ui_status_user_has_free_hands(user, source),
            ui_status_user_is_advanced_tool_user(user),
            ui_status_only_living(user),
            Dm.max(
                ui_status_user_is_adjacent(user, source),
                ui_status_silicon_has_access(user, source)
            )
        );
    }

    /// Returns a UI status such that users adjacent to source will be able to interact,
    /// far away users will be able to see, and anyone farther won't see anything.
    /// Dead users will receive updates no matter what, though you likely want to add
    /// a [`ui_status_only_living`] check for finer observer interactions.
    // tg: code/modules/tgui/status_composers.dm:18
    public static object ui_status_user_is_adjacent(mob user = null, atom source = null, float? allow_tk = null)
    {
        allow_tk ??= TRUE;
        if (isliving(user))
        {
            mob_living living_user = (mob_living)user;
            return living_user.shared_living_ui_distance(source as atom_movable, allow_tk: allow_tk);
        }
        else
            return UI_UPDATE;
    }

    /// Returns a UI status such that the dead will be able to watch, but not interact.
    // tg: code/modules/tgui/status_composers.dm:26
    public static object ui_status_only_living(mob user = null, object source = null)
    {
        if (isliving(user))
            return UI_INTERACTIVE;

        if (isobserver(user))
        {
            // If they turn on ghost AI control, admins can always interact.
            bool isAdminGhostAI__gap = false; // GAP(lead): isAdminGhostAI (admin ghost AI control, M10)
            if (isAdminGhostAI__gap)
                return UI_INTERACTIVE;

            // Regular ghosts can always at least view if in range.
            client client = GET_CLIENT(user) as client;
            if (Dm.truthy(client))
            {
                DmList clientviewlist = getviewsize(client.view);
                if (get_dist(source, user) < Dm.num(Dm.max(clientviewlist[1f], clientviewlist[2f])))
                    return UI_UPDATE;
            }
        }

        return UI_CLOSE;
    }

    /// Returns a UI status such that users with debilitating conditions, such as
    /// being dead or not having power for silicons, will not be able to interact.
    /// Being dead will disable UI, being incapacitated will continue updating it,
    /// and anything else will make it interactive.
    // tg: code/modules/tgui/status_composers.dm:48
    public static object ui_status_user_is_abled(mob user = null, atom source = null)
    {
        return user.shared_ui_interaction(source);
    }

    /// Returns a UI status such that those without blocked hands will be able to interact,
    /// but everyone else can only watch.
    // tg: code/modules/tgui/status_composers.dm:53
    public static object ui_status_user_has_free_hands(mob user = null, atom source = null, object allowed_source = null)
    {
        if (Dm.truthy(allowed_source))
            return HAS_TRAIT_NOT_FROM(user, TRAIT_HANDS_BLOCKED, allowed_source) ? UI_UPDATE : UI_INTERACTIVE;
        return HAS_TRAIT(user, TRAIT_HANDS_BLOCKED) ? UI_UPDATE : UI_INTERACTIVE;
    }

    /// Returns a UI status such that advanced tool users will be able to interact,
    /// but everyone else can only watch.
    // tg: code/modules/tgui/status_composers.dm:60
    public static object ui_status_user_is_advanced_tool_user(mob user = null)
    {
        return ISADVANCEDTOOLUSER(user) ? UI_INTERACTIVE : UI_UPDATE;
    }

    /// Returns a UI status such that silicons will be able to interact with whatever
    /// they would have access to if this was a machine. For example, AIs can
    /// interact if there's cameras with wireless control is enabled.
    // tg: code/modules/tgui/status_composers.dm:66
    public static object ui_status_silicon_has_access(mob user = null, atom source = null)
    {
        if (!issilicon(user))
            return UI_CLOSE;
        mob_living_silicon silicon_user = (mob_living_silicon)user;
        return silicon_user.get_ui_access(source);
    }

    /// Returns UI_INTERACTIVE if the user is conscious and lying down.
    /// Returns UI_UPDATE otherwise.
    // tg: code/modules/tgui/status_composers.dm:99
    public static object ui_status_user_is_conscious_and_lying_down(mob user = null)
    {
        if (!isliving(user))
            return UI_UPDATE;

        mob_living living_user = (mob_living)user;
        return (Dm.truthy(living_user.body_position == LYING_DOWN) && !IS_UNCONSCIOUS_OR_CRIT(living_user))
            ? UI_INTERACTIVE
            : UI_UPDATE;
    }

    /// Return UI_INTERACTIVE if the user is strictly adjacent to the target atom, whether they can see it or not.
    /// Return UI_CLOSE otherwise.
    // tg: code/modules/tgui/status_composers.dm:110
    public static object ui_status_user_strictly_adjacent(mob user = null, atom target = null)
    {
        if (get_dist(target, user) > 1)
            return UI_CLOSE;

        return UI_INTERACTIVE;
    }

    /// Return UI_INTERACTIVE if the user is inside the target atom, whether they can see it or not.
    /// Return UI_CLOSE otherwise.
    // tg: code/modules/tgui/status_composers.dm:118
    public static object ui_status_user_inside(mob user = null, atom target = null)
    {
        if (Dm.truthy(target.contains(user)))
            return UI_INTERACTIVE;

        return UI_CLOSE;
    }
}

public partial class mob_living_silicon
{
    /// Returns a UI status representing this silicon's capability to access
    /// the given source. Called by `ui_status_silicon_has_access`.
    // tg: code/modules/tgui/status_composers.dm:74
    public virtual object get_ui_access(atom source = null)
    {
        return UI_CLOSE;
    }
}

// tg: code/modules/tgui/status_composers.dm:77
public partial class mob_living_silicon_robot : mob_living_silicon
{
    // tg: code/modules/tgui/status_composers.dm:77
    public override object get_ui_access(atom source = null)
    {
        // Robots can interact with anything they can see.
        DmList clientviewlist = getviewsize(client.view) as DmList;
        if (get_dist(this, source) <= min(Dm.num(clientviewlist[1]), Dm.num(clientviewlist[2])))
            return UI_INTERACTIVE;
        return UI_DISABLED; // Otherwise they can keep the UI open.
    }
}

// tg: code/modules/tgui/status_composers.dm:84  (silicons SI4)
public partial class mob_living_silicon_ai : mob_living_silicon
{
    // tg: code/modules/tgui/status_composers.dm:84
    public override object get_ui_access(atom source = null)
    {
        // The AI can interact with anything it can see nearby, or with cameras while wireless control is enabled.
        if (!Dm.truthy(control_disabled) && Dm.truthy(can_see(source)))
            return UI_INTERACTIVE;
        return UI_CLOSE;
    }
}

// tg: code/modules/tgui/status_composers.dm:90  (silicons SI6)
public partial class mob_living_silicon_pai : mob_living_silicon
{
    // tg: code/modules/tgui/status_composers.dm:90
    public override object get_ui_access(atom source = null)
    {
        // pAIs can only use themselves and the owner's radio.
        if ((source == this || source == radio) && !Dm.truthy(stat))
            return UI_INTERACTIVE;
        else
            return UI_CLOSE;
    }
}
