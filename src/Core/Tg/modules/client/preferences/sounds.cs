// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/sounds.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M10 slice F): the volumes (playsound_local reads them, playtitlemusic the lobby's) and the sound toggles; the ghost poll prompt and
// its volume (syndicate X2, SSpolling reads them). Not translated: the
// TTS and achievement choices (no TTS or achievements). Gaps H3: the jukebox's apply proc (the jukebox is live).

// tg: code/modules/client/preferences/sounds.dm:1
[DmPath("/datum/preference/numeric/volume")]
public partial class datum_preference_numeric_volume : datum_preference_numeric
{
    // tg: code/modules/client/preferences/sounds.dm:6
    public override object create_default_value()
    {
        return maximum;
    }
}

/// Controls hearing lobby music
// tg: code/modules/client/preferences/sounds.dm:125
[DmPath("/datum/preference/numeric/volume/sound_lobby_volume")]
public partial class datum_preference_numeric_volume_sound_lobby_volume : datum_preference_numeric_volume
{
    // tg: code/modules/client/preferences/sounds.dm:130
    public override void apply_to_client_updated(client client = null, object value = null)
    {
        if (Dm.truthy(value) && Dm.truthy(isnewplayer(client.mob)))
            client.playtitlemusic();
        else
            client.mob.stop_sound_channel(CHANNEL_LOBBYMUSIC);
    }
}

/// Controls ambience volume
// tg: code/modules/client/preferences/sounds.dm:10
[DmPath("/datum/preference/numeric/volume/sound_ambience_volume")]
public partial class datum_preference_numeric_volume_sound_ambience_volume : datum_preference_numeric_volume
{
    // tg: code/modules/client/preferences/sounds.dm:15
    public override void apply_to_client(client client = null, object value = null)
    {
        client.update_ambience_pref(value);
    }
}

// tg: code/modules/client/preferences/sounds.dm:18
[DmPath("/datum/preference/toggle/sound_breathing")]
public partial class datum_preference_toggle_sound_breathing : datum_preference_toggle
{
    // tg: code/modules/client/preferences/sounds.dm:23
    public override void apply_to_client_updated(client client = null, object value = null)
    {
        mob_living_carbon carbon_mob = client.mob as mob_living_carbon;
        if (carbon_mob != null && !Dm.truthy(value))
            carbon_mob.breathing_loop.stop();
    }
}

/// Controls hearing announcement sounds
// tg: code/modules/client/preferences/sounds.dm:29
[DmPath("/datum/preference/toggle/sound_announcements")]
public partial class datum_preference_toggle_sound_announcements : datum_preference_toggle
{
}

/// Controls hearing the combat mode sound
// tg: code/modules/client/preferences/sounds.dm:35
[DmPath("/datum/preference/toggle/sound_combatmode")]
public partial class datum_preference_toggle_sound_combatmode : datum_preference_toggle
{
}

/// Controls hearing instruments
// tg: code/modules/client/preferences/sounds.dm:41
[DmPath("/datum/preference/numeric/volume/sound_instruments")]
public partial class datum_preference_numeric_volume_sound_instruments : datum_preference_numeric_volume
{
}

/// Controls admin music
// tg: code/modules/client/preferences/sounds.dm:137
[DmPath("/datum/preference/numeric/volume/sound_midi")]
public partial class datum_preference_numeric_volume_sound_midi : datum_preference_numeric_volume
{
}

/// Controls ship ambience volume
// tg: code/modules/client/preferences/sounds.dm:143
[DmPath("/datum/preference/numeric/volume/sound_ship_ambience_volume")]
public partial class datum_preference_numeric_volume_sound_ship_ambience_volume : datum_preference_numeric_volume
{
    // tg: code/modules/client/preferences/sounds.dm:148
    public override void apply_to_client_updated(client client = null, object value = null)
    {
        client.mob.refresh_looping_ambience();
    }
}

/// Controls radio noise volume
// tg: code/modules/client/preferences/sounds.dm:152
[DmPath("/datum/preference/numeric/volume/sound_radio_noise")]
public partial class datum_preference_numeric_volume_sound_radio_noise : datum_preference_numeric_volume
{
}

/// Controls hearing AI VOX announcements
// tg: code/modules/client/preferences/sounds.dm:158
[DmPath("/datum/preference/numeric/volume/sound_ai_vox")]
public partial class datum_preference_numeric_volume_sound_ai_vox : datum_preference_numeric_volume
{
}

/// Choice of which ghost poll prompt to use
// tg: code/modules/client/preferences/sounds.dm:164  (syndicate X2: the ghosts' polls)
public partial class datum_preference_choiced_sound_ghost_poll_prompt : datum_preference_choiced
{
    // tg: code/modules/client/preferences/sounds.dm:169
    public override object create_default_value()
    {
        return GHOST_POLL_PROMPT_1;
    }

    // tg: code/modules/client/preferences/sounds.dm:172
    public override DmList init_possible_values()
    {
        return Dm.list(GHOST_POLL_PROMPT_DISABLED, GHOST_POLL_PROMPT_1, GHOST_POLL_PROMPT_2);
    }
}

/// Volume which ghost poll prompts are played at
// tg: code/modules/client/preferences/sounds.dm:176  (syndicate X2: the ghosts' polls)
public partial class datum_preference_numeric_sound_ghost_poll_prompt_volume : datum_preference_numeric
{
    /// default value is max/2 because 100 1x modifier, while 200 is 2x
    // tg: code/modules/client/preferences/sounds.dm:185
    public override object create_default_value()
    {
        return Dm.num(maximum) / 2;
    }
}

// tg: code/modules/client/preferences/sounds.dm:47
public partial class datum_preference_numeric_volume_sound_jukebox : datum_preference_numeric_volume
{
    // tg: code/modules/client/preferences/sounds.dm:52
    public override void apply_to_client_updated(client client = null, object value = null)
    {
        var client_mob = client.mob as mob;
        if (client_mob != null)
            client_mob.SEND_SIGNAL(COMSIG_MOB_JUKEBOX_PREFERENCE_APPLIED, Dm.list(client_mob));
    }
}
