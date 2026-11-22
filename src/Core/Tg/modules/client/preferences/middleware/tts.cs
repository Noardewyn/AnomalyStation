// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/middleware/tts.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (D307): play_voice and play_voice_robot (D312: and the port's play_voice_radio), as the jobs' and the randomization's middleware (jobs.cs, random.cs): the middleware
// datum itself is not translated; its var and procs are the preferences' own, prefixed tts_, and preferences.cs calls them where tg
// loops over its middleware. Not translated: play_blips (the blips are not ported, DV262). The blip preferences are not ported, so the
// previews pass tg's defaults.

public partial class datum_preferences : datum
{
    /// Cooldown on requesting a TTS preview.
    // tg: code/modules/client/preferences/middleware/tts.dm:4  COOLDOWN_DECLARE(tts_test_cooldown) (__DEFINES/cooldowns.dm: var/tts_test_cooldown = 0)
    public float tts_test_cooldown = 0;

    // tg: code/modules/client/preferences/middleware/tts.dm:12
    public virtual object tts_play_voice(DmList @params = null, mob user = null)
    {
        // COOLDOWN_FINISHED(src, tts_test_cooldown) (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        if (!(this.tts_test_cooldown <= world.time))
            return TRUE;
        object speaker = read_preference(typeof(datum_preference_choiced_voice));
        object pitch = read_preference(typeof(datum_preference_numeric_tts_voice_pitch));
        // tg: :16-21  the blip base and number (not ported)
        // COOLDOWN_START(src, tts_test_cooldown, 0.5 SECONDS) (__DEFINES/cooldowns.dm:110), inline (rules 11.1 (a))
        this.tts_test_cooldown = world.time + (0.5f * SECONDS);
        Waiting.DetachSync(() => SStts.queue_tts_message(user.client, "Hello, this is my voice.", speaker: speaker, pitch: Dm.num(pitch), local: TRUE));
        return TRUE;
    }

    // tg: code/modules/client/preferences/middleware/tts.dm:28
    public virtual object tts_play_voice_robot(DmList @params = null, mob user = null)
    {
        if (!(this.tts_test_cooldown <= world.time))
            return TRUE;
        object speaker = read_preference(typeof(datum_preference_choiced_voice));
        object pitch = read_preference(typeof(datum_preference_numeric_tts_voice_pitch));
        this.tts_test_cooldown = world.time + (0.5f * SECONDS);
        // the silicon filter: the client applies it (D308)
        // DEVIATION(D312, the owner 2026-11-03): the robot preview says the plain preview's line, not tg's SHODAN quote
        Waiting.DetachSync(() => SStts.queue_tts_message(user.client, "Hello, this is my voice.", speaker: speaker, pitch: Dm.num(pitch), special_filters: TTS_FILTER_SILICON, local: TRUE));
        return TRUE;
    }

    // DEVIATION(D312, the owner 2026-11-03): port-only, the voice as a headset plays it (the clients' walkie-talkie band and clicks, D309):
    // the plain preview's line to this client alone, marked as a broadcast on the common frequency
    public virtual object tts_play_voice_radio(DmList @params = null, mob user = null)
    {
        if (!(this.tts_test_cooldown <= world.time))
            return TRUE;
        object speaker = read_preference(typeof(datum_preference_choiced_voice));
        object pitch = read_preference(typeof(datum_preference_numeric_tts_voice_pitch));
        this.tts_test_cooldown = world.time + (0.5f * SECONDS);
        if (Dm.truthy(SStts?.tts_enabled) && Dm.@in(speaker, SStts.available_speakers))
            DmWorldState.Current.Sinks.Messages.Tts(user.client, null, "Hello, this is my voice.", speaker as string, Dm.num(pitch), 100, radio_frequency: FREQ_COMMON);
        return TRUE;
    }
}
