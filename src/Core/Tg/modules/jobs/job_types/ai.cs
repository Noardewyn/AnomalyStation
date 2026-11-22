// Translated from tgstation (snapshot 1d99bd7f): code/modules/jobs/job_types/ai.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI4 (D326): the AI job, whole (its lobby icon is read by the preferences menu's job page, whose root in _job.cs is not
// translated). The var block is generated (D111).

// tg: code/modules/jobs/job_types/ai.dm:1
public partial class datum_job_ai : datum_job
{
    // tg: code/modules/jobs/job_types/ai.dm:25
    public override async DmTask after_spawn(mob_living spawned = null, client player_client = null)
    {
        await base.after_spawn(spawned, player_client);
        if (!isAI(spawned))
            return;

        mob_living_silicon_ai ai_spawn = (mob_living_silicon_ai)spawned;
        if (player_client != null)
            ai_spawn.set_gender(player_client);

        // when a cyborg is instantiated they will automatically try to link to us
        // but if the cyborg was made first, they will not have an us to link to!
        // gamestart borgs definitely want to be linked to the gamestart ai, so let's clean that up here
        if (Dm.eq(SSticker.current_state, GAME_STATE_SETTING_UP))
        {
            foreach (mob_living_silicon_robot gamestart_borg in Dm.each<mob_living_silicon_robot>(GLOB.silicon_mobs))
            {
                if (gamestart_borg.connected_ai == null)
                    await gamestart_borg.try_connect_to_ai(spawned as mob_living_silicon);
            }
        }

        ai_spawn.log_current_laws();
        await ai_spawn.show_laws(player_client?.mob);
    }

    // tg: code/modules/jobs/job_types/ai.dm:45
    public override async DmTask<object> get_roundstart_spawn_point()
    {
        return await get_latejoin_spawn_point();
    }

    // tg: code/modules/jobs/job_types/ai.dm:48
    public override async DmTask<object> get_latejoin_spawn_point()
    {
        await DmTask.CompletedTask;
        foreach (obj_structure_ai_core_latejoin_inactive inactive_core in Dm.each<obj_structure_ai_core_latejoin_inactive>(GLOB.latejoin_ai_cores.Copy()))
        {
            if (!inactive_core.is_available())
                continue;
            Dm.remove(ref GLOB.latejoin_ai_cores, inactive_core);
            inactive_core.available = FALSE;
            turf core_turf = get_turf(inactive_core);
            qdel(inactive_core);
            return core_turf;
        }
        DmList primary_spawn_points = Dm.list(); // Ideal locations.
        DmList secondary_spawn_points = Dm.list(); // Fallback locations.
        foreach (obj_effect_landmark_start_ai spawn_point in Dm.each<obj_effect_landmark_start_ai>(GLOB.landmarks_list))
        {
            if (Dm.truthy(spawn_point.used))
            {
                Dm.append(ref secondary_spawn_points, Dm.list(spawn_point));
                continue;
            }
            if (Dm.truthy(spawn_point.primary_ai))
            {
                primary_spawn_points = Dm.list(spawn_point);
                break; // Bingo.
            }
            Dm.append(ref primary_spawn_points, spawn_point);
        }
        obj_effect_landmark_start_ai chosen_spawn_point;
        if (Dm.truthy(length(primary_spawn_points)))
            chosen_spawn_point = pick(primary_spawn_points) as obj_effect_landmark_start_ai;
        else if (Dm.truthy(length(secondary_spawn_points)))
            chosen_spawn_point = pick(secondary_spawn_points) as obj_effect_landmark_start_ai;
        else
            throw Dm.CRASH("Failed to find any AI spawn points.", "code/modules/jobs/job_types/ai.dm", 73);
        chosen_spawn_point.used = TRUE;
        return chosen_spawn_point;
    }

    // tg: code/modules/jobs/job_types/ai.dm:77
    public override object special_check_latejoin(client latejoin = null)
    {
        foreach (obj_structure_ai_core_latejoin_inactive latejoin_core in Dm.each<obj_structure_ai_core_latejoin_inactive>(GLOB.latejoin_ai_cores))
        {
            if (latejoin_core.is_available())
                return TRUE;
        }
        return FALSE;
    }

    // tg: code/modules/jobs/job_types/ai.dm:84
    public override async DmTask announce_job(mob_living joining_mob = null)
    {
        await base.announce_job(joining_mob);
        if (Dm.truthy(SSticker.HasRoundStarted()))
            minor_announce(Dm.text("[] has been downloaded to an empty bluespace-networked AI core at [].", joining_mob, AREACOORD(joining_mob)));
    }

    // tg: code/modules/jobs/job_types/ai.dm:90
    public override object config_check()
    {
        return config.Get(typeof(datum_config_entry_flag_allow_ai)); /* CONFIG_GET(flag/allow_ai) */
    }

    // tg: code/modules/jobs/job_types/ai.dm:93
    public override object get_radio_information()
    {
        return I18n.T("<b>Prefix your message with :b to speak with cyborgs and other AIs.</b>");
    }

    // tg: code/modules/jobs/job_types/ai.dm:96
    public override async DmTask on_respawn(mob new_character = null)
    {
        await new_character.AIize();
    }

    // tg: code/modules/jobs/job_types/ai.dm:99
    public override object get_lobby_icon()
    {
        return icon(Dm.resource("icons/mob/huds/hud.dmi"), "hudai"); // DEFAULT_HUDS_DMI (__DEFINES/atom_hud.dm:75), expanded inline
    }
}

// tg: code/game/objects/effects/landmarks.dm:205
public partial class obj_effect_landmark_start_ai : obj_effect_landmark_start
{
    // tg: code/game/objects/effects/landmarks.dm:212
    public override void after_round_start()
    {
        if (Dm.truthy(latejoin_active) && !Dm.truthy(used))
            Dm.@new<obj_structure_ai_core_latejoin_inactive>(loc);
        base.after_round_start();
    }
}
