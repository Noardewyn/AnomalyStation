// Translated from tgstation (snapshot 1d99bd7f): code/modules/hallucination/station_message.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice G (D187): the whole file; the fake announcements came with M9 slice D (priority_announce) and the biohazard alert, which names
// the station, with slice D2 (station_name), but those that name the shuttle (SSshuttle) or need the heretics' text (M10), which stay marked leaves, so the hallucination still starts and
// ends as in tg. The var blocks are generated (D111).

// tg: code/modules/hallucination/station_message.dm:1  #define CANCEL_FAKE_ALERT -1 (file-local)
// tg: code/modules/hallucination/station_message.dm:3
public partial class datum_hallucination_station_message : datum_hallucination
{
    protected const float CANCEL_FAKE_ALERT = -1;

    // tg: code/modules/hallucination/station_message.dm:10
    public override async DmTask<object> start()
    {
        if (Dm.truthy(require_hearing) && HAS_TRAIT(hallucinator, TRAIT_DEAF))
            return FALSE;
        if (Dm.eq(await do_fake_alert(), CANCEL_FAKE_ALERT))
            return FALSE;

        qdel(this); // To be implemented by subtypes, call parent for easy cleanup
        return TRUE;
    }

    // tg: code/modules/hallucination/station_message.dm:19
    public virtual async DmTask<object> do_fake_alert()
    {
        return CANCEL_FAKE_ALERT;
    }
}

// tg: code/modules/hallucination/station_message.dm:22
public partial class datum_hallucination_station_message_blob_alert : datum_hallucination_station_message
{
    // tg: code/modules/hallucination/station_message.dm:25
    public override async DmTask<object> do_fake_alert()
    {
        priority_announce(I18n.T("Confirmed outbreak of level 5 biohazard aboard []. All personnel must contain the outbreak.", station_name()),
            I18n.T("Biohazard Alert"), ANNOUNCER_OUTBREAK5, players: Dm.list(hallucinator));
        return null;
    }
}

// tg: code/modules/hallucination/station_message.dm:29
public partial class datum_hallucination_station_message_shuttle_dock : datum_hallucination_station_message
{
    // tg: code/modules/hallucination/station_message.dm:31
    public override async DmTask<object> do_fake_alert()
    {
        // priority_announce(text = "[SSshuttle.emergency] has docked with the station. ...", ...): the emergency shuttle (SSshuttle, M10)
        Dm.Deferred("code/modules/hallucination/station_message.dm:32", "do_fake_alert: priority_announce(the emergency shuttle has docked, players = list(hallucinator)), which names the shuttle and its dock time (SSshuttle, M10+)");
        return null;
    }
}

// tg: code/modules/hallucination/station_message.dm:41
public partial class datum_hallucination_station_message_malf_ai : datum_hallucination_station_message
{
    // tg: code/modules/hallucination/station_message.dm:44
    public override async DmTask<object> do_fake_alert()
    {
        bool found_ai = false;
        foreach (mob_living_silicon_ai ai in Dm.each<mob_living_silicon_ai>(GLOB.silicon_mobs))
        {
            found_ai = true; // locate(/mob/living/silicon/ai) in GLOB.silicon_mobs
            break;
        }
        if (!found_ai)
            return CANCEL_FAKE_ALERT;

        priority_announce(I18n.T("Hostile runtimes detected in all station systems, please deactivate your AI to prevent possible damage to its morality core."),
            I18n.T("Anomaly Alert"), ANNOUNCER_AIMALF, players: Dm.list(hallucinator));
        return null;
    }
}

// tg: code/modules/hallucination/station_message.dm:50
public partial class datum_hallucination_station_message_heretic : datum_hallucination_station_message
{
    /// This is gross and will probably easily be outdated in some time but c'est la vie.
    /// Maybe if someone datumizes heretic paths or something this can be improved
    // tg: code/modules/hallucination/station_message.dm:58  var/static/list/ascension_bodies (a type-level static, rules 10.6)
    public static ref DmList ascension_bodies => ref TgState.Current.datum_hallucination_station_message_heretic__ascension_bodies;

    // tg: code/modules/hallucination/station_message.dm:78
    public override async DmTask<object> do_fake_alert()
    {
        // Unfortunately, this will not be synced if mass hallucinated
        mob_living_carbon_human totally_real_heretic = random_non_sec_crewmember() as mob_living_carbon_human;
        if (totally_real_heretic == null)
            return CANCEL_FAKE_ALERT;

        DmList fake_ascension = pick(ascension_bodies) as DmList;
        object announcement_text = replacetext(fake_ascension["text"], "%FAKENAME%", totally_real_heretic.real_name);
        Dm.Deferred("code/modules/hallucination/station_message.dm:85", "do_fake_alert: priority_announce(the fake ascension, players = list(hallucinator)), whose text is the heretics' (generate_heretic_text: the heretics, M10+)");
        return null;
    }
}

// tg: code/modules/hallucination/station_message.dm:95
public partial class datum_hallucination_station_message_cult_summon : datum_hallucination_station_message
{
    // tg: code/modules/hallucination/station_message.dm:98
    public override async DmTask<object> do_fake_alert()
    {
        // Same, will not be synced if mass hallucinated
        mob_living_carbon_human totally_real_cult_leader = random_non_sec_crewmember() as mob_living_carbon_human;
        if (totally_real_cult_leader == null)
            return CANCEL_FAKE_ALERT;

        // Get a fake area that the summoning is happening in
        area hallucinator_area = get_area(hallucinator) as area;
        DmList other_areas = Dm.sub(GLOB.the_station_areas, hallucinator_area.type) as DmList;
        object fake_summon_area_type = pick(other_areas);
        area fake_summon_area = GLOB.areas_by_type[fake_summon_area_type] as area;

        priority_announce(
            text: I18n.T("Figments from an eldritch god are being summoned by [] into [] from an unknown dimension. Disrupt the ritual at all costs!", totally_real_cult_leader.real_name, fake_summon_area),
            title: I18n.T("[] Higher Dimensional Affairs", command_name()),
            sound: Dm.resource("sound/music/antag/bloodcult/bloodcult_scribe.ogg"),
            has_important_message: TRUE,
            players: Dm.list(hallucinator)
        );
        return null;
    }
}

// tg: code/modules/hallucination/station_message.dm:116
public partial class datum_hallucination_station_message_meteors : datum_hallucination_station_message
{
    // tg: code/modules/hallucination/station_message.dm:120
    public override async DmTask<object> do_fake_alert()
    {
        priority_announce(I18n.T("Meteors have been detected on collision course with the station."), I18n.T("Meteor Alert"), ANNOUNCER_METEORS, players: Dm.list(hallucinator));
        return null;
    }
}

// tg: code/modules/hallucination/station_message.dm:123
public partial class datum_hallucination_station_message_supermatter_delam : datum_hallucination_station_message
{
    // tg: code/modules/hallucination/station_message.dm:125
    public override async DmTask<object> do_fake_alert()
    {
        Dm.output_to(hallucinator, sound(Dm.resource("sound/effects/magic/charge.ogg"))); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
        to_chat(hallucinator, span_bolddanger("You feel reality distort for a moment..."));
        return null;
    }
}

// tg: code/modules/hallucination/station_message.dm:129
public partial class datum_hallucination_station_message_clock_cult_ark : datum_hallucination_station_message
{
    // tg: code/modules/hallucination/station_message.dm:134
    public override async DmTask<object> start()
    {
        hallucinator.playsound_local(hallucinator, Dm.resource("sound/machines/clockcult/ark_deathrattle.ogg"), 50, FALSE, pressure_affected: FALSE);
        hallucinator.playsound_local(hallucinator, Dm.resource("sound/effects/clockcult_gateway_disrupted.ogg"), 50, FALSE, pressure_affected: FALSE);
        _addtimer(Dm.@new<datum_callback>(this, nameof(play_distant_explosion_sound)), 2.7f * SECONDS, file: "code/modules/hallucination/station_message.dm", line: 137);
        return TRUE; // does not call parent to finish up the sound in a few seconds
    }

    // tg: code/modules/hallucination/station_message.dm:140
    public virtual void play_distant_explosion_sound()
    {
        if (QDELETED(this))
            return;

        hallucinator.playsound_local(get_turf(hallucinator), Dm.resource("sound/effects/explosion/explosion_distant.ogg"), 50, FALSE, pressure_affected: FALSE);
        qdel(this);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/hallucination/station_message.dm:58  var/static/list/ascension_bodies = list(list("text" = ..., "sound" = ...), ...)
    internal DmList datum_hallucination_station_message_heretic__ascension_bodies = Dm.list(
        Dm.list(
            Dm.kv("text", "Fear the blaze, for the Ashlord, %FAKENAME% has ascended! The flames shall consume all!"),
            Dm.kv("sound", Dm.resource("sound/music/antag/heretic/ascend_blade.ogg"))),
        Dm.list(
            Dm.kv("text", "Master of blades, the Torn Champion's disciple, %FAKENAME% has ascended! Their steel is that which will cut reality in a maelstom of silver!"),
            Dm.kv("sound", Dm.resource("sound/music/antag/heretic/ascend_blade.ogg"))),
        Dm.list(
            Dm.kv("text", "Ever coiling vortex. Reality unfolded. ARMS OUTREACHED, THE LORD OF THE NIGHT, %FAKENAME% has ascended! Fear the ever twisting hand!"),
            Dm.kv("sound", Dm.resource("sound/music/antag/heretic/ascend_flesh.ogg"))),
        Dm.list(
            Dm.kv("text", "Fear the decay, for the Rustbringer, %FAKENAME% has ascended! None shall escape the corrosion!"),
            Dm.kv("sound", Dm.resource("sound/music/antag/heretic/ascend_rust.ogg"))),
        Dm.list(
            Dm.kv("text", "The nobleman of void %FAKENAME% has arrived, stepping along the Waltz that ends worlds!"),
            Dm.kv("sound", Dm.resource("sound/music/antag/heretic/ascend_void.ogg"))));
}
