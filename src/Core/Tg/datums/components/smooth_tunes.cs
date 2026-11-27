// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/smooth_tunes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: a song tuned by the festival god's rite, its effect on every listener while it plays, its finishing effect when
// the song was long enough, its musical notes and the instrument's glow. The var block is generated (D111).

///Smooth tunes component! Applied to musicians to give the songs they play special effects, according to a rite!
///Comes with BARTICLES!!!
// tg: code/datums/components/smooth_tunes.dm:3
public partial class datum_component_smooth_tunes : datum_component
{
    // tg: code/datums/components/smooth_tunes.dm:19
    [DmParams("linked_songtuner_rite", "allow_repeats", "particles_path", "glow_color")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object linked_songtuner_rite = Dm.arg(__extra, 0);
        object allow_repeats = Dm.arg(__extra, 1);
        object particles_path = Dm.arg(__extra, 2);
        object glow_color = Dm.arg(__extra, 3);
        if (!isinstrument(parent) && !isliving(parent))
            return COMPONENT_INCOMPATIBLE;
        this.linked_songtuner_rite = linked_songtuner_rite as datum_religion_rites_song_tuner;
        this.allow_repeats = Dm.num(allow_repeats);
        this.particles_path = particles_path as Type;
        this.glow_color = glow_color;
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/datums/components/smooth_tunes.dm:27
    public override object Destroy(float? force = null)
    {
        if (particle_holder != null)
            QDEL_NULL(ref particle_holder);
        qdel(linked_songtuner_rite);
        return base.Destroy(force);
    }

    // tg: code/datums/components/smooth_tunes.dm:33
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ATOM_STARTING_INSTRUMENT, nameof(start_singing));
        await DmTask.CompletedTask;
    }

    // tg: code/datums/components/smooth_tunes.dm:36
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, COMSIG_ATOM_STARTING_INSTRUMENT);
        await DmTask.CompletedTask;
    }

    ///Initiates the effect when the song begins playing.
    // tg: code/datums/components/smooth_tunes.dm:40
    [SignalHandler]
    public virtual void start_singing(datum source = null, datum_song starting_song = null)
    {
        if (starting_song == null)
            return;
        if (starting_song.parent is obj_structure_musician)
            return; //TODO: make stationary instruments work with no hiccups

        if (starting_song.lines.len * starting_song.tempo > FESTIVAL_SONG_LONG_ENOUGH)
            viable_for_final_effect = TRUE;
        else
            to_chat(parent, span_warning(I18n.T("This song is too short, so it won't include the song finishing effect.")));

        START_PROCESSING(SSobj, this); //even though WE aren't an object, our parent is!
        if (Dm.truthy(linked_songtuner_rite.song_start_message))
            starting_song.parent.visible_message(linked_songtuner_rite.song_start_message);

        linked_songtuner_rite.performer_start_effect(parent as mob_living_carbon_human, starting_song.parent);

        ///prevent more songs from being blessed concurrently, mob signal
        UnregisterSignal(parent, COMSIG_ATOM_STARTING_INSTRUMENT);
        ///and hook into the instrument this time, preventing other weird exploity stuff.
        RegisterSignal(starting_song.parent, COMSIG_INSTRUMENT_TEMPO_CHANGE, nameof(tempo_change));
        RegisterSignal(starting_song.parent, COMSIG_INSTRUMENT_END, nameof(stop_singing));
        if (!Dm.truthy(allow_repeats))
            RegisterSignal(starting_song.parent, COMSIG_INSTRUMENT_REPEAT, nameof(stop_singing));

        linked_song = starting_song;

        //barticles
        if (particles_path != null && ismovable(linked_song.parent))
            particle_holder = Dm.@new<obj_effect_abstract_particle_holder>(linked_song.parent, particles_path, PARTICLE_ATTACH_MOB);
        //filters
        linked_song.parent?.add_filter("smooth_tunes_outline", 9, Dm.list(Dm.kv("type", "outline"), Dm.kv("color", glow_color)));
    }

    ///Prevents changing tempo during a song to sneak in final effects quicker

    // tg: code/datums/components/smooth_tunes.dm:76
    [SignalHandler]
    public virtual void tempo_change(datum source = null, datum_song modified_song = null)
    {
        if (Dm.truthy(modified_song.playing) && Dm.truthy(viable_for_final_effect))
        {
            to_chat(parent, span_warning(I18n.T("Modifying the song mid-performance has removed your ability to perform the song finishing effect.")));
            viable_for_final_effect = FALSE;
        }
    }

    ///Ends the effect when the song is no longer playing.
    // tg: code/datums/components/smooth_tunes.dm:83
    [SignalHandler]
    public virtual void stop_singing(datum source = null, object finished = null)
    {
        STOP_PROCESSING(SSobj, this);
        if (Dm.truthy(viable_for_final_effect))
        {
            if (Dm.truthy(finished) && linked_songtuner_rite != null && linked_song != null)
            {
                foreach (mob_living_carbon_human listener in Dm.each<mob_living_carbon_human>(linked_song.hearing_mobs))
                {
                    if (listener == parent || Dm.truthy(listener.can_block_magic(MAGIC_RESISTANCE_HOLY, charge_cost: 1)))
                        continue;

                    Waiting.NoSleep(linked_songtuner_rite.finish_effect(listener, parent as atom));
                }
            }
            else
                to_chat(parent, span_warning(I18n.T("The song was interrupted, you cannot activate the finishing ability!")));
        }

        linked_song.parent?.remove_filter("smooth_tunes_outline");
        UnregisterSignal(linked_song.parent, Dm.list(
            COMSIG_INSTRUMENT_TEMPO_CHANGE,
            COMSIG_INSTRUMENT_END,
            COMSIG_INSTRUMENT_REPEAT));
        linked_song = null;
        qdel(this);
    }

    // tg: code/datums/components/smooth_tunes.dm:105
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        seconds_per_tick ??= SSOBJ_DT();
        if (linked_songtuner_rite != null && linked_song != null)
        {
            foreach (mob_living_carbon_human listener in Dm.each<mob_living_carbon_human>(linked_song.hearing_mobs))
            {
                if (listener == parent || Dm.truthy(listener.can_block_magic(MAGIC_RESISTANCE_HOLY, charge_cost: 0)))
                    continue;

                Waiting.NoSleep(linked_songtuner_rite.song_effect(listener, parent as atom));
            }
        }
        else
            stop_singing();
        return null;
    }
}
