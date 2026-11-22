// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/drone/drone_say.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

public static partial class GlobalProcs
{
    /**
     * Broadcast a message to all drones in a faction
     *
     * Arguments:
     * * msg - The message to send
     * * dead_can_hear - Boolean that determines if ghosts can hear the message (`FALSE` by default)
     * * source - [/atom] source that created the message
     * * faction_checked_mob - [/mob/living] to determine faction matches from
     * * exact_faction_match - Passed to [/mob/proc/faction_check_atom]
     */
    // tg: code/modules/mob/living/basic/drone/drone_say.dm:11
    public static void _alert_drones(object msg = null, float? dead_can_hear = null, atom source = null, mob_living faction_checked_mob = null, object exact_faction_match = null)
    {
        dead_can_hear ??= FALSE;
        if (Dm.truthy(dead_can_hear) && source != null)
        {
            foreach (mob dead_mob in Dm.each<mob>(GLOB.dead_mob_list))
            {
                string link = FOLLOW_LINK(dead_mob, source);
                to_chat(dead_mob, Dm.text("[] []", link, msg));
            }
        }
        foreach (object global_drone in Dm.each(GLOB.drones_list))
        {
            mob_living_basic_drone drone = global_drone as mob_living_basic_drone;
            if (!(drone is mob_living_basic_drone))
                continue;
            if (drone.stat == DEAD)
                continue;
            if (faction_checked_mob != null && !Dm.truthy(drone.faction_check_atom(faction_checked_mob, exact_faction_match)))
                continue;
            to_chat(
                drone,
                msg,
                type: MESSAGE_TYPE_RADIO,
                avoid_highlighting: (drone == source) ? TRUE : FALSE);
        }
    }
}

// tg: code/modules/mob/living/basic/drone/drone_say.dm:40
public partial class mob_living_basic_drone : mob_living_basic
{
    /**
     * Wraps [/proc/_alert_drones] with defaults
     *
     * * source - `src`
     * * faction_check_atom - `src`
     * * dead_can_hear - `TRUE`
     */
    // tg: code/modules/mob/living/basic/drone/drone_say.dm:40
    public virtual void alert_drones(object msg = null, float? dead_can_hear = null)
    {
        dead_can_hear ??= FALSE;
        _alert_drones(msg, dead_can_hear, this, this, TRUE);
    }

    /**
     * Wraps [/mob/living/basic/drone/proc/alert_drones] as a Drone Chat
     *
     * Shares the same radio code with binary
     */
    // tg: code/modules/mob/living/basic/drone/drone_say.dm:48
    public virtual void drone_chat(object message = null, DmList spans = null, DmList message_mods = null)
    {
        spans ??= Dm.list();
        message_mods ??= Dm.list();
        log_sayverb_talk(message, message_mods, tag: "drone chat");
        object message_part = generate_messagepart(message, spans, message_mods);
        alert_drones(span_drone(Dm.text("Drone Chat: [] []", span_name(Dm.text("[]", name)), span_message(Dm.str(message_part)))), TRUE);
    }
}
