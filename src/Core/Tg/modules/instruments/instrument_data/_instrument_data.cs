// Translated from tgstation (snapshot 1d99bd7f): code/modules/instruments/instrument_data/_instrument_data.dm and _instrument_key.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI6c (D326): the whole files; the instruments' own files (brass.dm, piano.dm, ...) are var blocks (generated).

public static partial class GlobalProcs
{
    /**
     * Get all non admin_only instruments as a list of text ids.
     */
    // tg: code/modules/instruments/instrument_data/_instrument_data.dm:4
    public static DmList get_allowed_instrument_ids()
    {
        DmList dot = Dm.list();
        foreach (object id in Dm.each(SSinstruments.instrument_data))
        {
            var I = SSinstruments.instrument_data[id] as datum_instrument;
            if (!Dm.truthy(I.admin_only))
                dot.Add(I.id);
        }
        return dot;
    }
}

// tg: code/modules/instruments/instrument_data/_instrument_data.dm:19
public partial class datum_instrument : datum
{
    // tg: code/modules/instruments/instrument_data/_instrument_data.dm:41-43  var/static HIGHEST_KEY and LOWEST_KEY
    public const int HIGHEST_KEY = 127;
    public const int LOWEST_KEY = 0;

    // tg: code/modules/instruments/instrument_data/_instrument_data.dm:49
    public override async DmTask New(params object[] __extra)
    {
        if (id == null)
            id = Dm.text("[]", type);
        await DmTask.CompletedTask;
    }

    /**
     * Initializes the instrument, calculating its samples if necessary.
     */
    // tg: code/modules/instruments/instrument_data/_instrument_data.dm:56
    public virtual void Initialize()
    {
        if (Dm.truthy(Dm.bitand(instrument_flags, Dm.bitor(INSTRUMENT_LEGACY, INSTRUMENT_DO_NOT_AUTOSAMPLE))))
            return;
        calculate_samples();
    }

    /**
     * Checks if this instrument is ready to play.
     */
    // tg: code/modules/instruments/instrument_data/_instrument_data.dm:64
    public virtual object ready()
    {
        if (Dm.truthy(Dm.bitand(instrument_flags, INSTRUMENT_LEGACY)))
            return Dm.truthy(legacy_instrument_path) && Dm.truthy(legacy_instrument_ext) ? legacy_instrument_ext : FALSE;
        else if (Dm.truthy(Dm.bitand(instrument_flags, INSTRUMENT_DO_NOT_AUTOSAMPLE)))
            return Dm.length(samples);
        return Dm.length(samples) >= 128 ? TRUE : FALSE;
    }

    // tg: code/modules/instruments/instrument_data/_instrument_data.dm:71
    public override object Destroy(float? force = null)
    {
        SSinstruments.instrument_data.Remove(id);
        foreach (datum_song S in Dm.each<datum_song>(songs_using.Copy()))
            S.set_instrument(null);
        real_samples = null;
        samples = null;
        songs_using = null;
        return base.Destroy(force);
    }

    /**
     * For synthesized instruments, this is how the instrument generates the "keys" that a [/datum/song] uses to play notes.
     * Calculating them on the fly would be unperformant, so we do it during init and keep it all cached in a list.
     */
    // tg: code/modules/instruments/instrument_data/_instrument_data.dm:85
    public virtual void calculate_samples()
    {
        if (Dm.length(real_samples) == 0)
            throw Dm.CRASH(Dm.text("No real samples defined for [] [] on calculate_samples() call.", id, type), "code/modules/instruments/instrument_data/_instrument_data.dm", 87);
        var real_keys = new List<int>();
        samples = Dm.list();
        foreach (object key in Dm.each(real_samples))
            real_keys.Add((int)Dm.num(text2num(key)));
        real_keys.Sort(); // sortTim(real_keys, cmp_numeric_asc)

        for (int i = 0; i < real_keys.Count - 1; i++)
        {
            int from_key = real_keys[i];
            int to_key = real_keys[i + 1];
            object sample1 = real_samples[num2text(from_key)];
            object sample2 = real_samples[num2text(to_key)];
            int pivot = (int)Math.Floor((from_key + to_key) / 2.0); //original code was a round but I replaced it because that's effectively a floor, thanks Baystation! who knows what was intended.
            for (int key = from_key; key <= pivot; key++)
                samples[num2text(key)] = Dm.@new<datum_instrument_key>(sample1, key, key - from_key);
            for (int key = pivot + 1; key <= to_key; key++)
                samples[num2text(key)] = Dm.@new<datum_instrument_key>(sample2, key, key - to_key);
        }

        // Fill in 0 to first key and last key to 127
        int first_key = real_keys[0];
        int last_key = real_keys[^1];
        object first_sample = real_samples[num2text(first_key)];
        object last_sample = real_samples[num2text(last_key)];
        for (int key = LOWEST_KEY; key <= first_key - 1; key++)
            samples[num2text(key)] = Dm.@new<datum_instrument_key>(first_sample, key, key - first_key);
        for (int key = last_key; key <= HIGHEST_KEY; key++)
            samples[num2text(key)] = Dm.@new<datum_instrument_key>(last_sample, key, key - last_key);
    }
}

// tg: code/modules/instruments/instrument_data/_instrument_key.dm:5
public partial class datum_instrument_key : datum
{
    /// The numerical key of what this is, from 1 to 127 on a standard piano keyboard.
    // tg: code/modules/instruments/instrument_data/_instrument_key.dm:7 (hand-declared: the name grep types it a typepath; a key is a number)
    public object key;

    // tg: code/modules/instruments/instrument_data/_instrument_key.dm:15
    [DmParams("sample", "key", "deviation", "frequency")]
    public override async DmTask New(params object[] __extra)
    {
        sample = Dm.arg(__extra, 0) ?? sample;
        key = Dm.arg(__extra, 1) ?? key;
        deviation = Dm.arg(__extra, 2) ?? deviation;
        frequency = Dm.arg(__extra, 3) ?? frequency;
        if (!Dm.truthy(frequency) && Dm.truthy(deviation))
            calculate();
        await DmTask.CompletedTask;
    }

    /**
     * Calculates and stores our deviation.
     */
    // tg: code/modules/instruments/instrument_data/_instrument_key.dm:26
    public virtual void calculate()
    {
        if (!Dm.truthy(deviation))
            throw Dm.CRASH("Invalid calculate call: No deviation or sample in instrument_key", "code/modules/instruments/instrument_data/_instrument_key.dm", 28);
        const float KEY_TWELTH = 1f / 12;
        frequency = MathF.Pow(2, KEY_TWELTH * Dm.num(deviation));
    }
}
