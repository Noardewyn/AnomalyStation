using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M2 (F1): stood in for the job system until M10 slice F, when the joins became tg's own (the lobby's new player and
// AttemptLateSpawn, D270). Now only the dev commands' and the console's client-less humans are named and dressed with it. Host code,
// not tg code (docs/ai/specs/apps.md 3): it calls tg's own procs in the order tg's join takes them, and sets nothing by hand.

/// <summary>
/// What tg's join gives a new crew member, until the lobby, the preferences and the job system exist. tg's late join
/// (<c>/mob/dead/new_player/proc/AttemptLateSpawn</c>) makes the character in <c>create_character</c>, where the
/// preferences give the human its name (<c>safe_transfer_prefs_to</c>: a new player's <c>real_name</c> preference is a
/// random unique name of its species), and then <c>SSjob.equip_rank</c> equips the job's outfit
/// (<c>job.equip</c> → <c>equipOutfit(outfit)</c>). Every M2 player is an assistant, the overflow job
/// (<c>CONFIG_GET(string/overflow_job)</c> is <c>JOB_ASSISTANT</c>), so the outfit is <c>/datum/outfit/job/assistant</c>,
/// whose <c>give_jumpsuit</c> takes the random-colour jumpsuit of <c>GLOB.colored_assistant</c>.
/// </summary>
public static class CrewStandIn
{
    /// <summary>
    /// The name of a new crew member: <c>fully_replace_character_name(null, generate_random_mob_name(TRUE))</c>, tg's own
    /// form for a fresh character without records (<c>mob_spawn/ghost_roles/mining_roles.dm:243</c>); the random unique
    /// name is what the <c>real_name</c> preference holds for a new player. It needs tg's name lists in the world's file
    /// seam (<see cref="TgStrings.Mount"/>); without them the human keeps tg's "Unknown" and false is returned.
    /// </summary>
    public static bool Name(mob_living_carbon_human human)
    {
        ArgumentNullException.ThrowIfNull(human);
        if (!TgStrings.HaveNames())
            return false;
        Waiting.NoSleep(human.fully_replace_character_name(null, human.generate_random_mob_name(Dm.TRUE)));
        return true;
    }

    /// <summary>
    /// The job: tg's late join assigns the job to the mind (<c>SSjob.assign_role</c>: <c>mind.set_assigned_role</c>, the
    /// assistant), equips its outfit (<c>equipOutfit(/datum/outfit/job/assistant)</c>, as <c>/datum/job/proc/equip</c> does) and,
    /// last, puts the crew member in the crew manifest (<c>GLOB.manifest.inject</c>, new_player.dm:255; M9 slice E). A human
    /// without a mind (a spawned dummy) only gets the outfit, and the manifest skips it, as tg's inject does.
    /// </summary>
    public static void Equip(mob_living_carbon_human human)
    {
        ArgumentNullException.ThrowIfNull(human);
        human.mind?.set_assigned_role(SSjob.get_job_type(typeof(datum_job_assistant)) as datum_job);
        Waiting.NoSleep(human.equipOutfit(typeof(datum_outfit_job_assistant)));
        GLOB.manifest?.inject(human);
    }

    /// <summary><see cref="Name"/> then <see cref="Equip"/>, in tg's order (the character's preferences, then the job's outfit).</summary>
    public static void Join(mob_living_carbon_human human)
    {
        Name(human);
        Equip(human);
    }
}
