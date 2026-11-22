using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Silicons;

/// <summary>
/// Silicons SI6c (D326): the instruments. SSinstruments samples every synthesized instrument over the 128 keys (a key between two
/// samples plays the nearer one pitched by 2^(deviation/12)); a song's lines compile to chords of keys; a piano plays its song to the
/// player next to it on a reserved channel at the key's pitch, decays the note and stops at the end; the legacy guitar plays its note
/// files; the editor's actions set the song; a pAI's Music Synthesizer is a synthesizer.
/// </summary>
public class InstrumentTests
{
    private const int Second = 20;

    private static (AudioRoom R, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(3, 5, "Bob");
        r.Step(Second);
        return (r, bob);
    }

    private static object Act(AudioRoom r, mob user, datum_song song, string action, DmList @params) => r.In(() =>
    {
        World.Current.usr = user;
        return Waiting.NoSleep(song.ui_act(action, @params ?? Dm.list(), Dm.@new<datum_tgui>(user, song, "InstrumentEditor"), null));
    });

    [Fact]
    public void TheTypesAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(datum_song), typeof(datum_song_handheld), typeof(datum_song_stationary), typeof(datum_instrument), typeof(datum_instrument_key),
            typeof(datum_controller_subsystem_processing_instruments), typeof(obj_structure_musician), typeof(obj_structure_musician_piano),
            typeof(obj_item_instrument_violin), typeof(obj_item_instrument_piano_synth), typeof(obj_item_instrument_harmonica),
            typeof(obj_item_instrument_trumpet_spectral), typeof(datum_element_spooky), typeof(datum_status_effect_good_music),
            typeof(datum_action_item_action_instrument),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Fact]
    public void TheInstrumentsAreSampledOverTheKeys()
    {
        var (r, _) = Room(9801);
        r.In(() =>
        {
            var grand = SSinstruments.get_instrument("r3grand");
            Assert.NotNull(grand);
            Assert.Equal(TRUE, grand.ready());
            Assert.Equal(128, Dm.length(grand.samples));
            // every key plays one of the instrument's samples, pitched by the twelfth root of two per key away from it
            foreach (int key in new[] { 0, 36, 60, 127 })
            {
                var k = (datum_instrument_key)grand.samples[num2text(key)];
                Assert.NotNull(k.sample);
                float deviation = Dm.num(k.deviation);
                float expected = deviation == 0 ? 0 : MathF.Pow(2, deviation / 12);
                Assert.Equal(expected, Dm.num(k.frequency), 3);
            }
            Assert.Contains("r3grand", Dm.each(SSinstruments.synthesizer_instrument_ids).Select(Dm.str));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASongCompilesToChordsOfKeys()
    {
        var (r, bob) = Room(9802);
        var piano = r.In(() => Dm.@new<obj_structure_musician_piano>(locate(4, 5, 1)));
        r.In(() =>
        {
            var song = piano.song;
            song.lines = Dm.list("C,D-F#,,E4/2");
            song.compile_chords();
            DmList chords = song.compiled_chords;
            Assert.Equal(4, Dm.length(chords));
            Assert.Equal(new object[] { 36f, 1f }, Dm.each((DmList)chords[1]).Select(o => (object)Dm.num(o)).ToArray()); // C3, tempodiv 1
            Assert.Equal(new object[] { 38f, 42f, 1f }, Dm.each((DmList)chords[2]).Select(o => (object)Dm.num(o)).ToArray()); // D3 and F#3
            Assert.Equal(1, Dm.length((DmList)chords[3])); // a rest: its tempodiv alone
            Assert.Equal(new object[] { 52f, 2f }, Dm.each((DmList)chords[4]).Select(o => (object)Dm.num(o)).ToArray()); // E4 for half a beat
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APianoPlaysItsSongToThePlayerBesideIt()
    {
        var (r, bob) = Room(9803);
        var piano = r.In(() => Dm.@new<obj_structure_musician_piano>(locate(4, 5, 1)));
        r.Sounds.Heard.Clear();
        var song = r.In(() => piano.song);
        r.In(() => song.lines = Dm.list("C,E,G"));
        Act(r, bob, song, "play_music", null);
        Assert.Equal(TRUE, r.In(() => song.playing));
        r.Step(4 * Second);
        List<HeardSound> notes = r.Sounds.Heard.Where(h => ReferenceEquals(h.Target, bob) && h.File != null && h.File.Contains("instruments")).ToList();
        Assert.True(notes.Count >= 3, "Bob heard the three notes: " + r.Sounds.Dump());
        Assert.All(notes, n => Assert.True(n.Channel > 0, "a note plays on a reserved channel"));
        // a note that decays below the dropoff frees its channel for a later note (channels_idle, play_synthesized.dm:96)
        Assert.InRange(notes.Select(n => n.Channel).Distinct().Count(), 1, 3);
        Assert.Contains(r.Sounds.Heard, h => ReferenceEquals(h.Target, bob) && h.File == null && Dm.truthy(Dm.bitand(h.Status, SOUND_UPDATE))); // the decay's volume updates
        // the song ended: the playing stops and the channels are freed
        Assert.Equal(FALSE, r.In(() => song.playing));
        Assert.Equal(0f, r.In(() => song.using_sound_channels));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheEditorSetsTheSong()
    {
        var (r, bob) = Room(9804);
        var piano = r.In(() => Dm.@new<obj_structure_musician_piano>(locate(4, 5, 1)));
        var song = r.In(() => piano.song);
        Act(r, bob, song, "set_volume", r.In(() => Dm.list(Dm.kv("amount", 60))));
        Assert.Equal(60f, r.In(() => Dm.num(song.volume)));
        Act(r, bob, song, "set_sustain_mode", r.In(() => Dm.list(Dm.kv("new_mode", SUSTAIN_EXPONENTIAL))));
        Assert.Equal(SUSTAIN_EXPONENTIAL, r.In(() => song.sustain_mode));
        Act(r, bob, song, "change_instrument", r.In(() => Dm.list(Dm.kv("new_instrument", "piano"))));
        Assert.Equal(TRUE, r.In(() => Dm.num(song.legacy)));
        r.In(() => song.ParseSong(bob, "BPM: 300\nC,D\nE"));
        Assert.Equal(2, r.In(() => Dm.length(song.lines)));
        Assert.Equal(2f, r.In(() => song.tempo)); // 600 / 300
        DmList data = r.In(() => Waiting.NoSleep(song.ui_data(bob)) as DmList);
        Assert.Equal(300f, r.In(() => Dm.num(data["bpm"])));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheLegacyPianoPlaysItsNoteFiles()
    {
        var (r, bob) = Room(9805);
        var piano = r.In(() => Dm.@new<obj_structure_musician_piano>(locate(4, 5, 1)));
        var song = r.In(() => piano.song);
        r.In(() =>
        {
            song.set_instrument("piano");
            song.lines = Dm.list("C,A");
        });
        r.Sounds.Heard.Clear();
        Act(r, bob, song, "play_music", null);
        r.Step(3 * Second);
        List<string> files = r.Sounds.Heard.Where(h => ReferenceEquals(h.Target, bob) && h.File != null).Select(h => h.File).ToList();
        Assert.Contains("sound/runtime/instruments/piano/Cn3.ogg", files);
        Assert.Contains("sound/runtime/instruments/piano/An3.ogg", files);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APaisMusicSynthesizerIsASynthesizer()
    {
        var (r, bob) = Room(9806);
        var card = r.In(() => Dm.@new<obj_item_pai_card>(bob.loc));
        var pai = r.In(() =>
        {
            var p = Dm.@new<mob_living_silicon_pai>(card);
            card.set_personality(p);
            return p;
        });
        r.In(() =>
        {
            World.Current.usr = pai;
            Waiting.NoSleep(pai.ui_act("buy", Dm.list(Dm.kv("selection", "Music Synthesizer")), Dm.@new<datum_tgui>(pai, pai, "PaiInterface"), null));
        });
        Assert.NotNull(r.In(() => pai.instrument));
        Assert.True(r.In(() => Dm.length(pai.instrument.song.allowed_instrument_ids)) > 1, "the synthesizer offers SSinstruments' instruments");
        r.AssertNoRuntimes();
    }
}
