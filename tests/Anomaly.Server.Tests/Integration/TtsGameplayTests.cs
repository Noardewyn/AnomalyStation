using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// D307 (DV262) through the real server on MetaStation with headless clients: Alice, Bob and Carol stand in a row as in
/// <see cref="SpeechGameplayTests"/>, Bob 3 tiles from Alice and Carol 10. Alice's character has a voice from her preferences (tg's
/// voice preference); her said line reaches Bob's client and her own as a line to voice (protocol version 63) from her entity in her
/// voice and pitch at tg's volumes (85 for a listener, 60 for herself), not Carol 10 tiles off (tg's range 7); a Russian line keeps its
/// letters; the preferences' voice preview reaches only Alice, not placed in the world. D308: a megaphone and a gas mask add tg's filters to
/// her line; her headset line reaches Bob 40 tiles off from the headset in his ear, marked as a Common radio line.
/// </summary>
public class TtsGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static turf East(turf from, int tiles) => locate(from.x + tiles, from.y, from.z) as turf;

    [MetaServerFact]
    public void Alices_lines_go_to_the_clients_that_hear_her_with_her_voice()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 93, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        RealHeadlessWorld.Player carol = w.Join("Carol");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var carolMob = (mob_living_carbon_human)w.MobOf(carol);
        var lines = new Dictionary<string, List<TtsMessage>> { ["Alice"] = new(), ["Bob"] = new(), ["Carol"] = new() };
        foreach (RealHeadlessWorld.Player p in new[] { alice, bob, carol })
        {
            string name = p.Name;
            p.Client.TtsReceived += m => lines[name].Add(m);
        }
        w.Run(4 * Second);

        string voice = In(w, () => aliceMob.voice as string);
        float pitch = In(w, () => aliceMob.pitch);
        output.WriteLine($"Alice's voice: {voice}, pitch {pitch}");
        Assert.Contains(voice, datum_controller_subsystem_tts.PortSpeakers); // the preference's voice on the spawned character

        // D310: tg's machine voices: one per vendor type, the computers' shared one (an inert type never initializes)
        using (w.World.Enter())
        {
            Assert.All(Dm.each(world.contents).OfType<obj_machinery_vending>().Where(v => !InertSeam.IsInert(v)), v => Assert.Contains(v.voice as string, datum_controller_subsystem_tts.PortSpeakers));
            Assert.All(Dm.each(world.contents).OfType<obj_machinery_computer>().Where(c => !InertSeam.IsInert(c)), c => Assert.Equal(SStts.computer_voice, c.voice));
        }

        turf row = In(w, SpeechGameplayTestsRow);
        Assert.NotNull(row);
        using (w.World.Enter())
        {
            Waiting.NoSleep(aliceMob.forceMove(row));
            Waiting.NoSleep(bobMob.forceMove(East(row, 3)));
            Waiting.NoSleep(carolMob.forceMove(East(row, 10)));
        }
        w.Run(Second);

        alice.Session.Say(new SayOut(SayTypes.Entry, SayChannels.Say, "Hello there", false));
        w.Run(Second);
        foreach ((string name, List<TtsMessage> got) in lines)
            output.WriteLine($"{name}: {string.Join(" | ", got.Select(m => $"#{m.Speaker} {m.Voice} {m.Volume} '{m.Text}'"))}");
        TtsMessage heard = Assert.Single(lines["Bob"], m => m.Text.Contains("Hello there", StringComparison.Ordinal));
        Assert.Equal(aliceMob.dm_id, heard.Speaker);
        Assert.Equal(voice, heard.Voice);
        Assert.Equal((int)pitch, heard.Pitch);
        Assert.Equal(85, heard.Volume);
        Assert.Contains(lines["Alice"], m => m.Speaker == aliceMob.dm_id && m.Volume == 60 && m.Text.Contains("Hello there", StringComparison.Ordinal));
        Assert.DoesNotContain(lines["Carol"], m => m.Text.Contains("Hello there", StringComparison.Ordinal));

        // D312: a whisper is voiced breathy and quieter to those near enough to hear it (tg's 1 tile and 1 more starred), not Bob 3 tiles
        // off; a shout ("!!") louder and strained, out to the shout's 14 tiles: Carol 10 tiles off hears it
        alice.Session.Say(new SayOut(SayTypes.Entry, SayChannels.Say, "#Quiet words", false));
        w.Run(Second);
        TtsMessage whisper = Assert.Single(lines["Alice"], m => m.Text.Contains("Quiet words", StringComparison.Ordinal));
        Assert.Equal(TtsFlags.Whisper, whisper.Flags);
        Assert.Equal(60 - 35, whisper.Volume);
        Assert.DoesNotContain(lines["Bob"], m => m.Text.Contains("Quiet words", StringComparison.Ordinal));
        alice.Session.Say(new SayOut(SayTypes.Entry, SayChannels.Say, "Over here!!", false));
        w.Run(Second);
        TtsMessage shout = Assert.Single(lines["Carol"], m => m.Text.Contains("Over here", StringComparison.Ordinal));
        Assert.Equal(TtsFlags.Yell, shout.Flags);
        Assert.Equal(85 + 15, shout.Volume);
        Assert.Contains(lines["Bob"], m => m.Text.Contains("Over here", StringComparison.Ordinal) && m.Flags == TtsFlags.Yell);

        // Poly speaks in her voice, an octave or two up
        mob_living_basic_parrot_poly poly = null;
        using (w.World.Enter())
        {
            poly = Dm.each(world.contents).OfType<mob_living_basic_parrot_poly>().FirstOrDefault();
            Assert.NotNull(poly);
            output.WriteLine($"Poly: voice {poly.voice}, pitch {poly.pitch}, stat {poly.stat}");
            Assert.Contains(poly.voice as string, datum_controller_subsystem_tts.PortSpeakers);
            Waiting.NoSleep(poly.forceMove(East(row, 2)));
        }
        w.Run(Second);
        using (w.World.Enter())
            Waiting.NoSleep(poly.say("Polly wants a cracker"));
        w.Run(Second);
        output.WriteLine($"Bob after Poly: {string.Join(" | ", lines["Bob"].Select(m => $"#{m.Speaker} {m.Voice} {m.Pitch} '{m.Text}'"))}");
        TtsMessage polyLine = Assert.Single(lines["Bob"], m => m.Text.Contains("cracker", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(poly.dm_id, polyLine.Speaker);
        Assert.InRange(polyLine.Pitch, 3, 4); // DV355: 3 or 4 semitones, not tg's octave or two
        Assert.Contains("atempo=1.5", polyLine.Filter); // DV355: the owner's tuning (2026-11-08)
        // DV264: repeating someone, she takes their voice for the line and then gets her own back (tg's reset to the type's initial
        // voice left her voiceless)
        using (w.World.Enter())
        {
            (object own, float ownPitch) = (poly.voice, poly.pitch);
            var repeat = new datum_bt_node_ai_behavior_parrot_repeat_speech();
            repeat.remember_own_voice(poly.ai_controller as datum_ai_controller);
            repeat.modify_voice(poly, Dm.list(Dm.kv("line", "Hello there"), Dm.kv("voice", "Man 2"), Dm.kv("pitch", 1)));
            Assert.Equal("Man 2", poly.voice);
            Assert.Equal(4, poly.pitch); // DV355: 1 + 6 to 12, at most 4
            Waiting.NoSleep(repeat.finish_action(poly.ai_controller as datum_ai_controller, TRUE));
            Assert.Equal(own, poly.voice);
            Assert.Equal(ownPitch, poly.pitch);
        }

        // DV262: a line in another script keeps its letters (tg's filter keeps a-z only: its server voices English)
        alice.Session.Say(new SayOut(SayTypes.Entry, SayChannels.Say, "Позовите врача", false));
        w.Run(Second);
        Assert.Contains(lines["Bob"], m => m.Text.Contains("Позовите врача", StringComparison.Ordinal));

        // D310: playsound_local's deafness: a deaf Bob is sent nothing
        using (w.World.Enter())
            ADD_TRAIT(bobMob, TRAIT_DEAF, "test");
        alice.Session.Say(new SayOut(SayTypes.Entry, SayChannels.Say, "Can you hear me", false));
        w.Run(Second);
        Assert.DoesNotContain(lines["Bob"], m => m.Text.Contains("Can you hear me", StringComparison.Ordinal));
        using (w.World.Enter())
            REMOVE_TRAIT(bobMob, TRAIT_DEAF, "test");

        // the preferences' play button: tg's local preview to Alice alone
        int before = lines["Bob"].Count;
        using (w.World.Enter())
            aliceMob.client.prefs.tts_play_voice(Dm.list(), aliceMob);
        w.Run(Second);
        Assert.Contains(lines["Alice"], m => m.Speaker == 0 && m.Text == "Hello, this is my voice.");
        Assert.Equal(before, lines["Bob"].Count);
        // D312: the robot preview says the same line, the port's radio preview marks it a broadcast
        int previews = lines["Alice"].Count;
        using (w.World.Enter())
        {
            aliceMob.client.prefs.tts_test_cooldown = 0;
            aliceMob.client.prefs.tts_play_voice_robot(Dm.list(), aliceMob);
            aliceMob.client.prefs.tts_test_cooldown = 0;
            aliceMob.client.prefs.tts_play_voice_radio(Dm.list(), aliceMob);
        }
        w.Run(Second);
        Assert.Contains(lines["Alice"].Skip(previews), m => m.Speaker == 0 && m.Text == "Hello, this is my voice." && m.Flags == TtsFlags.Silicon);
        Assert.Contains(lines["Alice"].Skip(previews), m => m.Speaker == 0 && m.Text == "Hello, this is my voice." && (m.Flags & TtsFlags.Radio) != 0);

        // D308: a megaphone in the active hand adds tg's tts_filter to the line
        obj_item_megaphone megaphone = null;
        using (w.World.Enter())
        {
            megaphone = Dm.@new<obj_item_megaphone>(get_turf(aliceMob));
            Assert.True(Dm.truthy(Waiting.NoSleep(aliceMob.put_in_active_hand(megaphone))));
        }
        alice.Session.Say(new SayOut(SayTypes.Entry, SayChannels.Say, "Attention please", false));
        w.Run(Second);
        TtsMessage loud = Assert.Single(lines["Bob"], m => m.Text.Contains("Attention please", StringComparison.Ordinal));
        Assert.StartsWith("acrusher=samples=2:level_out=6", loud.Filter);
        using (w.World.Enter())
            qdel(megaphone);

        // a gas mask's voice_filter (mob/living get_tts_voice)
        using (w.World.Enter())
        {
            if (aliceMob.wear_mask != null)
                qdel(aliceMob.wear_mask);
            Assert.True(Dm.truthy(Waiting.NoSleep(aliceMob.equip_to_slot_if_possible(Dm.@new<obj_item_clothing_mask_gas>(get_turf(aliceMob)), ITEM_SLOT_MASK))));
        }
        alice.Session.Say(new SayOut(SayTypes.Entry, SayChannels.Say, "Muffled words", false));
        w.Run(Second);
        Assert.Equal("lowpass=f=750,volume=2", Assert.Single(lines["Bob"], m => m.Text.Contains("Muffled words", StringComparison.Ordinal)).Filter);

        // D308: a headset line plays from each radio that receives it: Bob 40 tiles off hears it from his own headset, on Common; Alice's
        // own headset gives her the line marked as her own (tg's hear-self-radio option)
        using (w.World.Enter())
        {
            turf start = get_turf(aliceMob);
            turf far = null;
            for (int y = 1; y <= Dm.num(world.maxy) && far == null; y++)
                for (int x = 1; x <= Dm.num(world.maxx) && far == null; x++)
                    if (locate(x, y, 1) is turf t && get_dist(start, t) >= 40 && t is turf_open_floor && !Dm.truthy(t.density)
                        && Waiting.NoSleep(t.return_air()) is datum_gas_mixture air && Dm.num(air.return_pressure()) > 90)
                        far = t;
            Waiting.NoSleep(bobMob.forceMove(far));
            Assert.NotNull(bobMob.ears);
        }
        w.Run(Second);
        alice.Session.Say(new SayOut(SayTypes.Entry, SayChannels.Say, ";Over the radio", false));
        w.Run(2 * Second);
        TtsMessage radio = Assert.Single(lines["Bob"], m => m.Text.Contains("Over the radio", StringComparison.Ordinal));
        Assert.Equal(TtsFlags.Radio | TtsFlags.Common, radio.Flags);
        Assert.Equal(bobMob.dm_id, radio.Speaker); // the headset in his ear speaks from him
        Assert.Equal(voice, radio.Voice);
        Assert.Contains(lines["Alice"], m => m.Text.Contains("Over the radio", StringComparison.Ordinal) && (m.Flags & TtsFlags.Own) != 0);

        // D311: Character Setup's voice fits the gender (a man's for a male, a woman's for a female, any for the others) and its pitch is
        // -5 to 5; a gender that no longer fits the voice gets a fitting one; the character in the round keeps its voice, as in tg
        using (w.World.Enter())
        {
            datum_preferences prefs = aliceMob.client.prefs;
            // the window's set_preference (refused in the round, DV215) is update_preference
            object Set(string key, object value) => prefs.update_preference(GLOB.preference_entries_by_key[key] as datum_preference, value);
            object Voice() => prefs.read_preference(typeof(datum_preference_choiced_voice));
            Set("gender", MALE);
            Set("tts_voice", "Man 3");
            Assert.Equal("Man 3", Voice());
            Set("tts_voice", "Woman 2"); // not a male voice: replaced by a fitting one
            Assert.StartsWith("Man ", Voice() as string);
            Set("gender", FEMALE);
            Assert.StartsWith("Woman ", Voice() as string);
            Set("gender", PLURAL);
            Set("tts_voice", "Man 4");
            Assert.Equal("Man 4", Voice());
            Set("tts_voice_pitch", 9);
            Assert.Equal(0f, Dm.num(prefs.read_preference(typeof(datum_preference_numeric_tts_voice_pitch)))); // out of -5..5: sanitize_float's default
            Set("tts_voice_pitch", -3);
            Assert.Equal(-3f, Dm.num(prefs.read_preference(typeof(datum_preference_numeric_tts_voice_pitch))));
            Assert.Equal(voice, aliceMob.voice);
            Assert.True(datum_controller_subsystem_tts.fits_gender(SStts.random_tts_voice(MALE), MALE));
            Assert.True(datum_controller_subsystem_tts.fits_gender(SStts.random_tts_voice(FEMALE), FEMALE));
        }
    }

    private static turf SpeechGameplayTestsRow()
    {
        for (int y = 1; y <= Dm.num(world.maxy); y++)
        {
            int run = 0;
            for (int x = 1; x <= Dm.num(world.maxx); x++)
            {
                turf t = locate(x, y, 1) as turf;
                bool clear = t is turf_open_floor && !Dm.truthy(t.density) && !Dm.truthy(t.opacity)
                    && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density) || Dm.truthy(a.opacity))
                    && Waiting.NoSleep(t.return_air()) is datum_gas_mixture air && Dm.num(air.return_pressure()) > 90;
                run = clear ? run + 1 : 0;
                if (run == 15)
                    return locate(x - 14, y, 1) as turf;
            }
        }
        return null;
    }
}
