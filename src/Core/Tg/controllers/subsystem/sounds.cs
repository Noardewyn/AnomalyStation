// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/sounds.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M4b audio slice 1). The var block is generated (D111); this part adds tg's procs and its static vars
// (rules 10.6: TgState fields). The rust-g calls (the sound lengths of precache_sounds, cache_sounds and get_sound_length)
// are the engine part's rustg_sound_length_list and rustg_call_sound_len (Builtin/rust_g.builtin.cs, rules 13), which read
// the host's table of sound lengths (Infrastructure/SoundLengths.cs; the server fills it from the sound tool's index).
// find_all_available_sounds' pathwalk over BYOND's resource folder is a deferred leaf (the sound tool's index lists the
// files, M4b slice 2).

// tg: code/controllers/subsystem/sounds.dm:3 SUBSYSTEM_DEF(sounds)
public partial class datum_controller_subsystem_sounds : datum_controller_subsystem
{
    // tg: code/controllers/subsystem/sounds.dm:1  #define DATUMLESS "NO_DATUM" (file-local, #undef at :281)
    private const string DATUMLESS = "NO_DATUM";

    // tg: code/controllers/subsystem/sounds.dm:7  var/static/using_channels_max = CHANNEL_HIGHEST_AVAILABLE //BYOND max channels
    // (rules 10.6: a TgState field, below)
    public static ref float using_channels_max => ref TgState.Current.datum_controller_subsystem_sounds__using_channels_max;
    /// Amount of channels to reserve for random usage rather than reservations being allowed to reserve all channels. Also a nice safeguard for when someone screws up.
    // tg: code/controllers/subsystem/sounds.dm:9  var/static/random_channels_min = 50  (rules 10.6: a TgState field, below)
    public static ref float random_channels_min => ref TgState.Current.datum_controller_subsystem_sounds__random_channels_min;
    // Comments from https://github.com/DaedalusDock/daedalusdock We love Francinum.
    /// A list of sound formats that work in byond. Indexed for direct accesing rather then loop itteration or usage of `in`
    // tg: code/controllers/subsystem/sounds.dm:39  var/static/list/byond_sound_formats = list(...)  (rules 10.6: a TgState field, below)
    public static ref DmList byond_sound_formats => ref TgState.Current.datum_controller_subsystem_sounds__byond_sound_formats;
    /// File types we can sniff the duration from using rustg.
    // tg: code/controllers/subsystem/sounds.dm:57  var/static/list/safe_formats = list(...)  (rules 10.6: a TgState field, below)
    public static ref DmList safe_formats => ref TgState.Current.datum_controller_subsystem_sounds__safe_formats;
    // Currently set to private as I would prefer you use byond_sound_formats but you can unprivate it if you have a valid use!
    // Put more common extensions first to speed this up a bit (So only ogg and mp3 lol.)
    /// Similar to byond_sound_formats, a list of sound formats that work in byond.
    // tg: code/controllers/subsystem/sounds.dm:65  VAR_PRIVATE/static/list/byond_sound_extensions = list(...)  (rules 10.6: a TgState field, below)
    public static ref DmList byond_sound_extensions => ref TgState.Current.datum_controller_subsystem_sounds__byond_sound_extensions;

    // The New() that SUBSYSTEM_DEF(sounds) defines (__DEFINES/MC.dm:93-96), with NEW_SS_GLOBAL(SSsounds) (__DEFINES/MC.dm:29)
    // expanded inline (it reads src).
    // tg: code/controllers/subsystem/sounds.dm:3
    public override async DmTask New(params object[] __extra)
    {
        if (SSsounds != this)
        {
            if (SSsounds is datum_controller_subsystem_sounds)
            {
                Recover();
                qdel(SSsounds);
            }
            SSsounds = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/sounds.dm:81
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        setup_available_channels();
        find_all_available_sounds();
        init_sound_keys();

        // tg: :86-88  if(!(RUST_G)) { to_chat(world, span_boldnotice("Sounds subsystem: No rust_g detected.")); return ..() }: the port's
        // rust-g is its engine part (Builtin/rust_g.builtin.cs, rules 13), always there, so the branch is never taken.

        // Precache ambience sounds
        foreach (object key in Dm.each(GLOB.ambience_assoc))
            Dm.combine(ref sounds_to_precache, GLOB.ambience_assoc[key]);

        precache_sounds();

        return SS_INIT_SUCCESS;
    }

    // tg: code/controllers/subsystem/sounds.dm:98
    public virtual void setup_available_channels()
    {
        channel_list = Dm.list();
        reserved_channels = Dm.list();
        using_channels = Dm.list();
        using_channels_by_datum = Dm.list();
        for (float i = 1, i__end = using_channels_max; i <= i__end; i++)
            Dm.append(ref channel_list, i);
        channel_random_low = 1;
        channel_reserve_high = length(channel_list);
    }

    // tg: code/controllers/subsystem/sounds.dm:108
    public virtual void find_all_available_sounds()
    {
        all_sounds = Dm.list();
        // tg: :110  all_sounds = pathwalk("sound/", byond_sound_extensions): BYOND's resource folder; the port has none (the
        // client loads the files itself; the sound tool's index lists them, M4b slice 2). The list's only reader is the dream's
        // random sound (flufftext/dreaming.dm:190, sleep and dreams, M9), which can fill it from the host's sound table then.
        Dm.Deferred("code/controllers/subsystem/sounds.dm:110", "find_all_available_sounds: pathwalk(\"sound/\", byond_sound_extensions), the files of BYOND's resource folder: all_sounds stays empty (its only reader, the dream's random sound, is M10+)");
    }

    /// Removes a channel from using list.
    // tg: code/controllers/subsystem/sounds.dm:113
    public virtual void free_sound_channel(object channel = null)
    {
        string text_channel = num2text(Dm.num(channel));
        object @using = using_channels[text_channel];
        Dm.remove(ref using_channels, text_channel);
        if (!Dm.eq(@using, DATUMLESS)) // datum channel
        {
            Dm.remove_at(using_channels_by_datum, @using, channel);
            if (!Dm.truthy(length(using_channels_by_datum[@using])))
                stop_tracking_datum(@using);
        }
        free_channel(channel);
    }

    /// Frees all the channels a datum is using.
    // tg: code/controllers/subsystem/sounds.dm:124
    public virtual void free_datum_channels(object channel_owner = null)
    {
        DmList L = using_channels_by_datum[channel_owner] as DmList;
        if (!Dm.truthy(L))
            return;
        foreach (object channel in Dm.each(L))
        {
            Dm.remove(ref using_channels, num2text(Dm.num(channel)));
            free_channel(channel);
        }
        stop_tracking_datum(channel_owner);
    }

    /// Frees all datumless channels
    // tg: code/controllers/subsystem/sounds.dm:134
    public virtual void free_datumless_channels()
    {
        free_datum_channels(DATUMLESS);
    }

    /// Reserve a sound channel. Free it later with free_sound_channel()
    // tg: code/controllers/subsystem/sounds.dm:138
    public virtual object reserve_sound_channel()
    {
        object dot = null;
        dot = reserve_channel();
        if (!Dm.truthy(dot)) //oh no..
            return FALSE;
        string text_channel = num2text(Dm.num(dot));
        using_channels[text_channel] = DATUMLESS;
        // LAZYINITLIST(using_channels_by_datum[DATUMLESS]): a list element cannot be passed by ref; its expansion
        // `using_channels_by_datum[DATUMLESS] ||= list()` (rules 4.5)
        if (!Dm.truthy(using_channels_by_datum[DATUMLESS]))
            using_channels_by_datum[DATUMLESS] = Dm.list();
        Dm.append_at(using_channels_by_datum, DATUMLESS, dot);
        return dot;
    }

    /// Reserves a channel for a datum. Automatic cleanup only when the datum is deleted. Returns an integer for channel.
    // tg: code/controllers/subsystem/sounds.dm:148
    public virtual object reserve_sound_channel_for_datum(datum channel_owner = null)
    {
        object dot = null;
        if (!Dm.truthy(channel_owner)) //i don't like typechecks but someone will fuck it up
            throw Dm.CRASH("Attempted to reserve sound channel without datum using the managed proc.", "code/controllers/subsystem/sounds.dm", 150);
        dot = reserve_channel();
        if (!Dm.truthy(dot))
            throw Dm.CRASH("No more sound channels can be reserved", "code/controllers/subsystem/sounds.dm", 153);
        string text_channel = num2text(Dm.num(dot));
        using_channels[text_channel] = channel_owner;
        // LAZYINITLIST(using_channels_by_datum[channel_owner]): the expansion, as in reserve_sound_channel
        if (!Dm.truthy(using_channels_by_datum[channel_owner]))
            using_channels_by_datum[channel_owner] = Dm.list();
        Dm.append_at(using_channels_by_datum, channel_owner, dot);

        if (length(using_channels_by_datum[channel_owner]) == 1)
            RegisterSignal(channel_owner, COMSIG_QDELETING, nameof(tracked_datum_deleted));
        return dot;
    }

    /**
     * Reserves a channel and updates the datastructure. Private proc.
     */
    // tg: code/controllers/subsystem/sounds.dm:165
    [DmPrivate]
    public virtual object reserve_channel()
    {
        if (Dm.le(channel_reserve_high, random_channels_min)) // out of channels
            return null;
        object channel = channel_list[channel_reserve_high];
        reserved_channels[num2text(Dm.num(channel))] = Dm.postdec(ref channel_reserve_high);
        return channel;
    }

    /**
     * Frees a channel and updates the datastructure. Private proc.
     */
    // tg: code/controllers/subsystem/sounds.dm:176
    [DmPrivate]
    public virtual void free_channel(object number = null)
    {
        string text_channel = num2text(Dm.num(number));
        object index = reserved_channels[text_channel];
        if (!Dm.truthy(index))
            throw Dm.CRASH("Attempted to (internally) free a channel that wasn't reserved.", "code/controllers/subsystem/sounds.dm", 181);
        Dm.remove(ref reserved_channels, text_channel);
        // push reserve index up, which makes it now on a channel that is reserved
        Dm.inc(ref channel_reserve_high);
        // swap the reserved channel wtih the unreserved channel so the reserve index is now on an unoccupied channel and the freed channel is next to be used.
        channel_list.Swap(Dm.num(channel_reserve_high), Dm.num(index));
        // now, an existing reserved channel will likely (exception: unreserving last reserved channel) be at index
        // get it, and update position.
        string text_reserved = num2text(Dm.num(channel_list[index]));
        if (!Dm.truthy(reserved_channels[text_reserved])) //if it isn't already reserved make sure we don't accidently mistakenly put it on reserved list!
            return;
        reserved_channels[text_reserved] = index;
    }

    /// Random available channel, returns text.
    // tg: code/controllers/subsystem/sounds.dm:195
    public virtual object random_available_channel_text()
    {
        object dot = null;
        if (Dm.gt(channel_random_low, channel_reserve_high))
            channel_random_low = 1;
        dot = Dm.text("[]", channel_list[Dm.postinc(ref channel_random_low)]);
        return dot;
    }

    /// Random available channel, returns number
    // tg: code/controllers/subsystem/sounds.dm:201
    public virtual object random_available_channel()
    {
        object dot = null;
        if (Dm.gt(channel_random_low, channel_reserve_high))
            channel_random_low = 1;
        dot = channel_list[Dm.postinc(ref channel_random_low)];
        return dot;
    }

    /// How many channels we have left.
    // tg: code/controllers/subsystem/sounds.dm:207
    public virtual object available_channels_left()
    {
        return length(channel_list) - random_channels_min;
    }

    // tg: code/controllers/subsystem/sounds.dm:210
    public virtual void precache_sounds()
    {
        if (!Dm.truthy(length(sounds_to_precache)))
            return;

        DmList lengths = rustg_sound_length_list(sounds_to_precache);
        precache_errors = lengths[RUSTG_SOUNDLEN_ERRORS] as DmList;
        sound_lengths = lengths[RUSTG_SOUNDLEN_SUCCESSES] as DmList;
        foreach (object sound_path in Dm.each(sound_lengths))
            sound_lengths[sound_path] = text2num(sound_lengths[sound_path]);

        sounds_to_precache = null;
    }

    /// Cache a list of sound lengths.
    // tg: code/controllers/subsystem/sounds.dm:223
    public virtual void cache_sounds(DmList paths = null)
    {
        DmList reconstructed = Dm.list();
        reconstructed.len = length(paths);

        for (float i = 1, i__end = length(paths); i <= i__end; i++)
            reconstructed[i] = Dm.text("[]", paths[i]);

        DmList @out = rustg_sound_length_list(paths);
        DmList successes = @out[RUSTG_SOUNDLEN_SUCCESSES] as DmList;
        foreach (object sound_path in Dm.each(successes))
            sound_lengths[sound_path] = text2num(successes[sound_path]);
    }

    /// Cache and return a single sound.
    // tg: code/controllers/subsystem/sounds.dm:236
    public virtual object get_sound_length(object file_path = null)
    {
        object dot = null;
        bool missing = false;
        try
        {
            dot = 0;
            if (!istext(file_path))
            {
                if (!isfile(file_path))
                    throw Dm.CRASH("rustg_sound_length error: Passed non-text object", "code/controllers/subsystem/sounds.dm", 240);

                if (Dm.truthy(length(Dm.text("[]", file_path)))) // Runtime generated RSC references stringify into 0-length strings.
                    file_path = Dm.text("[]", file_path);
                else
                    throw Dm.CRASH("rustg_sound_length does not support non-static file refs.", "code/controllers/subsystem/sounds.dm", 245);
            }

            object cached_length = sound_lengths[file_path];
            if (cached_length != null)
                return cached_length;

            object ret = rustg_call_sound_len(file_path);
            object as_num = text2num(ret);
            if (ret == null)
            {
                dot = 0;
                missing = true;
                throw Dm.CRASH(Dm.text("rustg_sound_length error: []", ret), "code/controllers/subsystem/sounds.dm", 255);
            }

            sound_lengths[file_path] = as_num;
            return as_num;
        }
        // DM: a runtime ends only this proc and returns '.' (0, set at :237 and :254; rules 9.3). PORT: a file the host's table
        // lacks is reported once per file (the table does not change while the world runs), not on every call.
        catch (Exception e)
        {
            if (!missing || SoundLengths.FirstMiss(file_path as string))
                World.Current.ReportRuntime(e);
            return dot;
        }
    }

    // tg: code/controllers/subsystem/sounds.dm:260
    public virtual void init_sound_keys()
    {
        foreach (Type sfx in Dm.each_cast<Type>(subtypesof(typeof(datum_sound_effect))))
        {
            // this is for the assoc subtype
            // sfx.key on a typepath reads the type's value (OpenDream DereferenceField: "equivalent to initial()"; rules 10.3)
            if (Dm.initial(sfx, (datum_sound_effect p) => p.key) != null)
                GLOB.sfx_datum_by_key[Dm.initial(sfx, (datum_sound_effect p) => p.key)] = Dm.@new(sfx);
        }
    }


    ///Call to free all channels reserved by a datum.
    // tg: code/controllers/subsystem/sounds.dm:268
    [DmPrivate]
    public virtual void stop_tracking_datum(object channel_owner = null)
    {
        Dm.remove(ref using_channels_by_datum, channel_owner);
        UnregisterSignal((datum)channel_owner, COMSIG_QDELETING);
    }

    /// Handles a tracked datum being deleted, automatically freeing the channels.
    // tg: code/controllers/subsystem/sounds.dm:275
    [SignalHandler, DmPrivate]
    public virtual void tracked_datum_deleted(datum source = null)
    {
        free_datum_channels(source);
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/sounds.dm:3  SUBSYSTEM_DEF(sounds): GLOBAL_REAL(SSsounds, /datum/controller/subsystem/sounds)
    internal datum_controller_subsystem_sounds SSsounds;

    // tg: code/controllers/subsystem/sounds.dm:7  /datum/controller/subsystem/sounds/var/static/using_channels_max = CHANNEL_HIGHEST_AVAILABLE
    internal float datum_controller_subsystem_sounds__using_channels_max = CHANNEL_HIGHEST_AVAILABLE;
    // tg: code/controllers/subsystem/sounds.dm:9  var/static/random_channels_min = 50
    internal float datum_controller_subsystem_sounds__random_channels_min = 50;
    // tg: code/controllers/subsystem/sounds.dm:39  var/static/list/byond_sound_formats
    internal DmList datum_controller_subsystem_sounds__byond_sound_formats = Dm.list(
        Dm.kv("mid", TRUE), //Midi, 8.3 File Name
        Dm.kv("midi", TRUE), //Midi, Long File Name
        Dm.kv("mod", TRUE), //Module, Original Amiga Tracker format
        Dm.kv("it", TRUE), //Impulse Tracker Module format
        Dm.kv("s3m", TRUE), //ScreamTracker 3 Module
        Dm.kv("xm", TRUE), //FastTracker 2 Module
        Dm.kv("oxm", TRUE), //FastTracker 2 (Vorbis Compressed Samples)
        Dm.kv("wav", TRUE), //Waveform Audio File Format, A (R)IFF-class format, and Microsoft's choice in the 80s sound format pissing match.
        Dm.kv("ogg", TRUE), //OGG Audio Container, Usually contains Vorbis-compressed Audio
        //"raw" = TRUE, //On the tin, byond purports to support raw, uncompressed PCM Audio. I actually have no fucking idea how FMOD actually handles these.
        //since they completely lack all information. As a confusion based anti-footgun, I'm just going to wire this to FALSE for now. It's here though.
        Dm.kv("wma", TRUE), //Windows Media Audio container
        Dm.kv("aiff", TRUE), //Audio Interchange File Format, Apple's side of the 80s sound format pissing match. It's also (R)IFF in a trenchcoat.
        Dm.kv("mp3", TRUE) //MPeg Layer 3 Container (And usually, Codec.)
    );
    // tg: code/controllers/subsystem/sounds.dm:57  var/static/list/safe_formats
    internal DmList datum_controller_subsystem_sounds__safe_formats = Dm.list(
        Dm.kv("ogg", TRUE),
        Dm.kv("mp3", TRUE)
    );
    // tg: code/controllers/subsystem/sounds.dm:65  VAR_PRIVATE/static/list/byond_sound_extensions
    internal DmList datum_controller_subsystem_sounds__byond_sound_extensions = Dm.list(
        ".ogg",
        ".mp3",
        ".mid",
        ".midi",
        ".mod",
        ".it",
        ".s3m",
        ".xm",
        ".oxm",
        ".wav",
        //".raw", See byond_sound_formats
        ".wma",
        ".aiff"
    );
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_sounds SSsounds => ref TgState.Current.SSsounds;
}
