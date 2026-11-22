// Translated from tgstation (snapshot 1d99bd7f): code/modules/hallucination/fake_sound.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8 slice G (D187): the whole file. A fake sound is played to the hallucinator's client alone (playsound_local). The var blocks are generated (D111).

/// Hallucination that plays a fake sound somewhere nearby.
// tg: code/modules/hallucination/fake_sound.dm:2
public partial class datum_hallucination_fake_sound : datum_hallucination
{
    // tg: code/modules/hallucination/fake_sound.dm:13
    public override async DmTask<object> start()
    {
        if (HAS_TRAIT(hallucinator, TRAIT_DEAF))
            return FALSE;

        object sound_to_play = islist(sound_type) ? pick(sound_type as DmList) : sound_type;
        play_fake_sound(random_far_turf() as turf, sound_to_play);
        feedback_details += Dm.text("Sound: []", sound_to_play);
        qdel(this);
        return TRUE;
    }

    /// Actually plays the fake sound.
    // tg: code/modules/hallucination/fake_sound.dm:23
    public virtual void play_fake_sound(turf source = null, object sound_to_play = null)
    {
        sound_to_play ??= sound_type;
        hallucinator.playsound_local(source, sound_to_play, volume, sound_vary);
    }

    /// Used to queue additional, delayed fake sounds via a callback.
    // tg: code/modules/hallucination/fake_sound.dm:27
    public virtual void queue_fake_sound(turf source = null, object sound_to_play = null, object volume_override = null, object vary_override = null, object delay = null)
    {
        if (!Dm.truthy(delay))
            throw Dm.CRASH(Dm.text("[] queued a fake sound without a timer.", type), "code/modules/hallucination/fake_sound.dm", 29);

        // Queue the sound to be played with a timer on the mob, not the datum, because we'll probably get qdel'd
        _addtimer(Dm.@new<datum_callback>(hallucinator, "playsound_local", source, sound_to_play, Dm.truthy(volume_override) ? volume_override : volume, Dm.truthy(vary_override) ? vary_override : sound_vary), Dm.num(delay), file: "code/modules/hallucination/fake_sound.dm", line: 32);
    }
}

// tg: code/modules/hallucination/fake_sound.dm:42
public partial class datum_hallucination_fake_sound_normal_airlock_pry : datum_hallucination_fake_sound_normal
{
    // tg: code/modules/hallucination/fake_sound.dm:46
    public override void play_fake_sound(turf source = null, object sound_to_play = null)
    {
        base.play_fake_sound(source, sound_to_play);
        queue_fake_sound(source, Dm.resource("sound/machines/airlock/airlockforced.ogg"), 50, TRUE, delay: 5 * SECONDS);
    }
}

// tg: code/modules/hallucination/fake_sound.dm:71
public partial class datum_hallucination_fake_sound_normal_mech : datum_hallucination_fake_sound_normal
{
    // tg: code/modules/hallucination/fake_sound.dm:82
    public override object Destroy(float? force = null)
    {
        mech_source = null;
        return base.Destroy(force);
    }

    // tg: code/modules/hallucination/fake_sound.dm:86
    public override async DmTask<object> start()
    {
        mech_dir = Dm.num(pick(GLOB.cardinals));
        steps_left = rand(4, 9);
        mech_source = random_far_turf() as turf;

        mech_walk();
        return TRUE;
    }

    // tg: code/modules/hallucination/fake_sound.dm:94
    public virtual void mech_walk()
    {
        if (QDELETED(this))
            return;

        if (prob(75))
        {
            play_fake_sound(mech_source);
            mech_source = get_step(mech_source, mech_dir);
        }
        else
        {
            play_fake_sound(mech_source);
            mech_dir = Dm.num(pick(GLOB.cardinals));
        }

        steps_left--;
        if (steps_left <= 0)
            qdel(this);

        else
            _addtimer(Dm.@new<datum_callback>(this, nameof(mech_walk)), 1 * SECONDS, file: "code/modules/hallucination/fake_sound.dm", line: 112);
    }
}

// tg: code/modules/hallucination/fake_sound.dm:114
public partial class datum_hallucination_fake_sound_normal_wall_deconstruction : datum_hallucination_fake_sound_normal
{
    // tg: code/modules/hallucination/fake_sound.dm:117
    public override void play_fake_sound(turf source = null, object sound_to_play = null)
    {
        base.play_fake_sound(source, sound_to_play);
        queue_fake_sound(source, Dm.resource("sound/items/tools/welder2.ogg"), delay: 10.5f * SECONDS);
        queue_fake_sound(source, Dm.resource("sound/items/tools/ratchet.ogg"), delay: 12 * SECONDS);
    }
}

// tg: code/modules/hallucination/fake_sound.dm:122
public partial class datum_hallucination_fake_sound_normal_door_hacking : datum_hallucination_fake_sound_normal
{
    // tg: code/modules/hallucination/fake_sound.dm:126
    public override void play_fake_sound(turf source = null, object sound_to_play = null)
    {
        // Make it sound like someone's pulsing a multitool one or multiple times.
        // Screwdriver happens immediately...
        base.play_fake_sound(source, sound_to_play);

        float hacking_time = rand(4 * SECONDS, 8 * SECONDS);
        // Multitool sound.
        queue_fake_sound(source, Dm.resource("sound/items/weapons/empty.ogg"), delay: 0.8f * SECONDS);
        if (hacking_time > 4.5f * SECONDS)
        {
            // Another multitool sound if the hacking time is long.
            queue_fake_sound(source, Dm.resource("sound/items/weapons/empty.ogg"), delay: 3 * SECONDS);
            if (prob(50))
            {
                // Bonus multitool sound, rapidly after the last.
                queue_fake_sound(source, Dm.resource("sound/items/weapons/empty.ogg"), delay: 3.5f * SECONDS);
            }
        }

        if (hacking_time > 5.5f * SECONDS)
        {
            // A final multitool sound if the hacking time is very long.
            queue_fake_sound(source, Dm.resource("sound/items/weapons/empty.ogg"), delay: 5 * SECONDS);
        }

        // Crowbarring it open.
        queue_fake_sound(source, Dm.resource("sound/machines/airlock/airlockforced.ogg"), delay: hacking_time);
    }
}

// tg: code/modules/hallucination/fake_sound.dm:159
public partial class datum_hallucination_fake_sound_normal_ringtone : datum_hallucination_fake_sound_normal
{
    // tg: code/modules/hallucination/fake_sound.dm:162
    [DmParams("hallucinator")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (HAS_TRAIT(SSstation, STATION_TRAIT_PDA_GLITCHED))
        {
            sound_type = pick(
                Dm.resource("sound/machines/beep/twobeep_voice1.ogg"),
                Dm.resource("sound/machines/beep/twobeep_voice2.ogg"));
        }
        else
            sound_type = Dm.resource("sound/machines/beep/twobeep_high.ogg");
    }

    // tg: code/modules/hallucination/fake_sound.dm:171
    public override void play_fake_sound(turf source = null, object sound_to_play = null)
    {
        sound_to_play ??= sound_type;
        if (prob(33))
        {
            source = get_turf(hallucinator);
            obj_item_modular_computer_pda pda = null;
            foreach (obj_item_modular_computer_pda found in Dm.each<obj_item_modular_computer_pda>(hallucinator.get_all_contents()))
            {
                pda = found; // locate() in
                break;
            }
            datum_computer_file_program_messenger messenger_app = null;
            foreach (datum_computer_file_program_messenger found in Dm.each<datum_computer_file_program_messenger>(pda?.stored_files))
            {
                messenger_app = found; // locate() in
                break;
            }
            hallucinator.balloon_alert(hallucinator, Dm.text("*[]*", Dm.truthy(messenger_app?.ringtone) ? messenger_app.ringtone : MESSENGER_RINGTONE_DEFAULT));
        }
        base.play_fake_sound(source, sound_to_play);
    }
}

// tg: code/modules/hallucination/fake_sound.dm:180
public partial class datum_hallucination_fake_sound_weird : datum_hallucination_fake_sound
{
    // tg: code/modules/hallucination/fake_sound.dm:186
    public override void play_fake_sound(turf source = null, object sound_to_play = null)
    {
        if (Dm.truthy(no_source))
        {
            base.play_fake_sound(null, sound_to_play);
            return;
        }

        base.play_fake_sound(source, sound_to_play);
    }
}

// tg: code/modules/hallucination/fake_sound.dm:217
public partial class datum_hallucination_fake_sound_weird_creepy : datum_hallucination_fake_sound_weird
{
    // tg: code/modules/hallucination/fake_sound.dm:220
    [DmParams("hallucinator")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        //These sounds are (mostly) taken from Hidden: Source
        sound_type = GLOB.creepy_ambience;
    }
}

// tg: code/modules/hallucination/fake_sound.dm:259
public partial class datum_hallucination_fake_sound_weird_phone : datum_hallucination_fake_sound_weird
{
    // tg: code/modules/hallucination/fake_sound.dm:265
    public override void play_fake_sound(turf source = null, object sound_to_play = null)
    {
        for (float next_ring = 1; next_ring <= 3; next_ring++)
            queue_fake_sound(source, sound_to_play, delay: 2.5f * SECONDS * next_ring);

        base.play_fake_sound(source, sound_to_play);
    }
}

// tg: code/modules/hallucination/fake_sound.dm:292
public partial class datum_hallucination_fake_sound_weird_tesloose : datum_hallucination_fake_sound_weird
{
    // tg: code/modules/hallucination/fake_sound.dm:297
    public override void play_fake_sound(turf source = null, object sound_to_play = null)
    {
        base.play_fake_sound(source, sound_to_play);
        float next_shock__limit = rand(2, 4);
        for (float next_shock = 1; next_shock <= next_shock__limit; next_shock++)
            queue_fake_sound(source, sound_to_play, volume_override: volume + (15 * next_shock), delay: 3 * SECONDS * next_shock);
    }
}
