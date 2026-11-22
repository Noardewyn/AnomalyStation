// Translated from tgstation (snapshot 1d99bd7f): code/modules/jobs/job_types/cyborg.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI2 (D326): the cyborg job, whole (its lobby icon is read by the preferences menu's job page, whose root in _job.cs is not
// translated).
// The var block is generated (D111).

// tg: code/modules/jobs/job_types/cyborg.dm:1
public partial class datum_job_cyborg : datum_job
{
    // tg: code/modules/jobs/job_types/cyborg.dm:26
    public override async DmTask after_spawn(mob_living spawned = null, client player_client = null)
    {
        await base.after_spawn(spawned, player_client);
        if (!iscyborg(spawned))
            return;
        mob_living_silicon_robot robot_spawn = (mob_living_silicon_robot)spawned;
        if (player_client != null)
            robot_spawn.set_gender(player_client);

        // cyborgs can late spawn in some strange circumstances
        if (!Dm.eq(SSticker.current_state, GAME_STATE_SETTING_UP))
            robot_spawn.notify_ai(AI_NOTIFICATION_NEW_BORG);
    }

    // tg: code/modules/jobs/job_types/cyborg.dm:38
    public override object get_radio_information()
    {
        return I18n.T("<b>Prefix your message with :b to speak with other cyborgs and AI.</b>");
    }

    // tg: code/modules/jobs/job_types/cyborg.dm:41
    public override async DmTask on_respawn(mob new_character = null)
    {
        await new_character.Robotize(TRUE);
    }

    // tg: code/modules/jobs/job_types/cyborg.dm:44
    public override object get_lobby_icon()
    {
        return icon(Dm.resource("icons/mob/huds/hud.dmi"), "hudcyborg"); // DEFAULT_HUDS_DMI (__DEFINES/atom_hud.dm:75), expanded inline
    }
}
