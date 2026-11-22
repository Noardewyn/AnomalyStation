// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/spawn_text_overlay.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice F: the whole file.

public partial class client : datum
{
    /// Displays a typewriter-style spawn text overlay that includes station/area/job name and time
    // tg: code/modules/client/spawn_text_overlay.dm:2
    public virtual void show_spawn_text_overlay(float? duration = null)
    {
        Waiting.Detach(show_spawn_text_overlay__async(duration ?? 5 * SECONDS)); // set waitfor = FALSE
    }

    private async DmTask show_spawn_text_overlay__async(float duration)
    {
        object mob_name = mob.name;
        object job_title = mob.mind?.assigned_role.title ?? "Unknown";

        object station_name = GlobalProcs.station_name();
        object area_name = get_area_name(mob, format_text: TRUE) ?? "Unknown Location";
        object time_date = server_timestamp(format: "YYYY-MM-DD hh:mm:ss", ic_time: TRUE);

        string text = Dm.text("\n\t\t[] - []\n\t\t[]\n\t\t[]\n\t\t[]\n\t", mob_name, job_title, station_name, area_name, time_date);
        text = uppertext(text);

        atom_movable_screen spawn_text = Dm.@new<atom_movable_screen>();
        spawn_text.maptext_height = 64;
        spawn_text.maptext_width = 512;
        spawn_text.layer = FLY_LAYER;
        spawn_text.plane = FULLSCREEN_PLANE;
        spawn_text.appearance_flags = APPEARANCE_UI_IGNORE_ALPHA;
        spawn_text.screen_loc = "LEFT+1,TOP-3";

        screen.Add(spawn_text);
        animate(spawn_text, Dm.named("alpha", 255), Dm.named("time", 1 * SECONDS));

        for (int i = 1; i <= length_char(text) + 1; i++)
        {
            if (QDELETED(spawn_text) || QDELETED(this))
                return;
            spawn_text.maptext = MAPTEXT_PIXELLARI(copytext_char(text, 1, i));
            await Dm.sleep(1);
        }

        _addtimer(Dm.@new<datum_callback>(this, nameof(fade_spawn_text_overlay), this, spawn_text), duration, file: "code/modules/client/spawn_text_overlay.dm", line: 36);
    }

    // tg: code/modules/client/spawn_text_overlay.dm:38
    public virtual async DmTask fade_spawn_text_overlay(client player_client = null, atom_movable_screen spawn_text = null)
    {
        if (QDELETED(spawn_text))
            return;
        animate(spawn_text, Dm.named("alpha", 0), Dm.named("time", 0.5f * SECONDS));
        await Dm.sleep(5);
        if (player_client != null && !QDELETED(spawn_text))
            player_client.screen.Remove(spawn_text);
        qdel(spawn_text);
    }
}
