// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/ghost_role/_ghost_role.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1c, D316: the lone operative's event). The var block is generated (D111).

// tg: code/modules/events/ghost_role/_ghost_role.dm:4
public partial class datum_round_event_ghost_role : datum_round_event
{
    // tg: code/modules/events/ghost_role/_ghost_role.dm:1  #define MAX_SPAWN_ATTEMPT 3
    private const int MAX_SPAWN_ATTEMPT = 3;

    // tg: code/modules/events/ghost_role/_ghost_role.dm:17
    public override async DmTask<object> start()
    {
        await try_spawning();
        return null;
    }

    /**
     * Attempts to spawn the role, and cancels the event if it fails.
     *
     * Pauses the event right as it begins, and waits for setup/polling to end.
     * If successful, continues running the rest of the event and notifies ghosts.
     */
    // tg: code/modules/events/ghost_role/_ghost_role.dm:27
    public virtual async DmTask try_spawning()
    {
        // The event does not run until the spawning has been attempted
        // to prevent us from getting gc'd halfway through
        processing = FALSE;

        status = await spawn_role();
        if (cached_announcement_chance == null)
            cached_announcement_chance = announce_chance; //only announce once we've finished the spawning loop.
        announce_chance = Dm.eq(status, SUCCESSFUL_SPAWN) ? Dm.num(cached_announcement_chance) : 0;
        if (Dm.eq(status, WAITING_FOR_SOMETHING))
        {
            float retry_count = 0;
            if (retry_count >= MAX_SPAWN_ATTEMPT)
            {
                message_admins(Dm.text("[] event has exceeded maximum spawn attempts. Aborting and refunding.", role_name));
                if (control != null && control.occurrences > 0) //Don't refund if it hasn't
                    control.occurrences--;
                return;
            }
            float waittime = 300 * MathF.Pow(2, retry_count);
            message_admins(Dm.text("The event will not spawn a [] until certain conditions are met. Waiting []s and then retrying.", role_name, waittime / 10));
            _addtimer(Dm.@new<datum_callback>(this, nameof(try_spawning), 0, ++retry_count), waittime,
                file: "code/modules/events/ghost_role/_ghost_role.dm", line: 47);
            return;
        }

        if (!Dm.truthy(status))
        {
            message_admins(Dm.text("An attempt to spawn [] returned [], this is a bug.", role_name, status));
            kill();
            return;
        }

        switch (Dm.num(status))
        {
            case MAP_ERROR:
                message_admins(Dm.text("[] cannot be spawned due to a map error.", role_name));
                kill();
                return;
            case NOT_ENOUGH_PLAYERS:
                message_admins(Dm.text("[] cannot be spawned due to lack of players signing up.", role_name));
                deadchat_broadcast(Dm.text(" did not get enough candidates ([]) to spawn.", minimum_required), Dm.text("<b>[]</b>", role_name), message_type: DEADCHAT_ANNOUNCEMENT);
                kill();
                return;
            case SUCCESSFUL_SPAWN:
                message_admins(Dm.text("[] spawned successfully.", role_name));
                if (Dm.truthy(spawned_mobs.len))
                {
                    foreach (mob mobs in Dm.each<mob>(spawned_mobs))
                        announce_to_ghosts(mobs);
                }
                else
                    message_admins("No mobs found in the `spawned_mobs` list, this is a bug.");
                break;
        }

        processing = TRUE;
    }

    /**
     * Performs the spawning of our role. Entirely specific to the event itself.
     *
     * Should return SUCCESSFUL_SPAWN if role was successfully spawned,
     * return NOT_ENOUGH_PLAYERS if less than mimimum_required was found,
     * and return MAP_ERROR if a spawn location could not be found.
     */
    // tg: code/modules/events/ghost_role/_ghost_role.dm:82
    public virtual async DmTask<object> spawn_role()
    {
        return FALSE;
    }
}
