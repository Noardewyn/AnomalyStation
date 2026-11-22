// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/processing/instruments.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI6c (D326): the whole file. The var block is generated (D111); its statics are TgState fields.

// tg: code/controllers/subsystem/processing/instruments.dm:1 PROCESSING_SUBSYSTEM_DEF(instruments)
public partial class datum_controller_subsystem_processing_instruments : datum_controller_subsystem_processing
{
    // tg: :7-21 the static vars
    /// List of all instrument data, associative id = datum
    public DmList instrument_data => TgState.Current.SSinstruments__instrument_data ??= Dm.list();
    /// List of all song datums.
    public DmList songs => TgState.Current.SSinstruments__songs ??= Dm.list();
    /// Max lines in songs
    public const float musician_maxlines = 1000;
    /// Max characters per line in songs
    public const float musician_maxlinechars = 300;
    /// Deciseconds between hearchecks. Too high and instruments seem to lag when people are moving around in terms of who can hear it. Too low and the server lags from this.
    public const float musician_hearcheck_mindelay = 4;
    /// Maximum instrument channels total instruments are allowed to use. This is so you don't have instruments deadlocking all sound channels.
    public const float max_instrument_channels = MAX_INSTRUMENT_CHANNELS;
    /// Current number of channels allocated for instruments
    public float current_instrument_channels
    {
        get => TgState.Current.SSinstruments__current_instrument_channels;
        set => TgState.Current.SSinstruments__current_instrument_channels = value;
    }
    /// Single cached list for synthesizer instrument ids, so you don't have to have a new list with every synthesizer.
    public DmList synthesizer_instrument_ids
    {
        get => TgState.Current.SSinstruments__synthesizer_instrument_ids;
        set => TgState.Current.SSinstruments__synthesizer_instrument_ids = value;
    }
    public DmList note_sustain_modes => TgState.Current.SSinstruments__note_sustain_modes ??= Dm.list(SUSTAIN_LINEAR, SUSTAIN_EXPONENTIAL);

    // The New() that PROCESSING_SUBSYSTEM_DEF(instruments) defines (__DEFINES/MC.dm:116-119), with NEW_SS_GLOBAL(SSinstruments)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/processing/instruments.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSinstruments != this)
        {
            if (SSinstruments is datum_controller_subsystem_processing_instruments)
            {
                Recover();
                qdel(SSinstruments);
            }
            SSinstruments = this;
        }
        PreInit();
        await DmTask.CompletedTask;
    }

    // The fire() that PROCESSING_SUBSYSTEM_DEF(instruments) defines (__DEFINES/MC.dm:120): ..() /*just so it shows up on the profiler*/
    // tg: code/controllers/subsystem/processing/instruments.dm:1
    [DmParams()]
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        await base.fire(resumed, __extra); /*just so it shows up on the profiler*/
    }

    // tg: code/controllers/subsystem/processing/instruments.dm:26
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        initialize_instrument_data();
        synthesizer_instrument_ids = get_allowed_instrument_ids();
        await DmTask.CompletedTask;
        return SS_INIT_SUCCESS;
    }

    // tg: code/controllers/subsystem/processing/instruments.dm:31
    public virtual void on_song_new(datum_song S = null) => songs.Add(S);

    // tg: code/controllers/subsystem/processing/instruments.dm:34
    public virtual void on_song_del(datum_song S = null) => songs.Remove(S);

    // tg: code/controllers/subsystem/processing/instruments.dm:37
    public virtual void initialize_instrument_data()
    {
        foreach (Type path in Dm.each_cast<Type>(valid_subtypesof(typeof(datum_instrument))))
        {
            var I = (datum_instrument)Dm.@new(path);
            I.Initialize();
            if (!Dm.truthy(I.id))
            {
                qdel(I);
                continue;
            }
            instrument_data[I.id] = I;
        }
    }

    // tg: code/controllers/subsystem/processing/instruments.dm:47
    public virtual datum_instrument get_instrument(object id_or_path = null) => instrument_data[Dm.text("[]", id_or_path)] as datum_instrument;

    // tg: code/controllers/subsystem/processing/instruments.dm:50
    public virtual object reserve_instrument_channel(datum I = null)
    {
        if (current_instrument_channels > max_instrument_channels)
            return null;
        object dot = SSsounds.reserve_sound_channel_for_datum(I);
        if (dot != null)
            current_instrument_channels++;
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/processing/instruments.dm:1  PROCESSING_SUBSYSTEM_DEF(instruments): GLOBAL_REAL(SSinstruments, /datum/controller/subsystem/processing/instruments)
    internal datum_controller_subsystem_processing_instruments SSinstruments;
    // tg: :7, :9, :19, :21, :22 the subsystem's static vars
    internal DmList SSinstruments__instrument_data;
    internal DmList SSinstruments__songs;
    internal float SSinstruments__current_instrument_channels;
    internal DmList SSinstruments__synthesizer_instrument_ids;
    internal DmList SSinstruments__note_sustain_modes;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_processing_instruments SSinstruments => ref TgState.Current.SSinstruments;
}
