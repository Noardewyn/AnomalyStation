// Translated from tgstation (snapshot 1d99bd7f): code/controllers/configuration/entries/general.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// if autoadmin is enabled
// tg: code/controllers/configuration/entries/general.dm:2
[DmPath("/datum/config_entry/flag/autoadmin")]
public partial class datum_config_entry_flag_autoadmin : datum_config_entry_flag
{
    public datum_config_entry_flag_autoadmin()
    {
        protection = CONFIG_ENTRY_LOCKED;
    }
}

/// the rank given to autoadmins
// tg: code/controllers/configuration/entries/general.dm:6
[DmPath("/datum/config_entry/string/autoadmin_rank")]
public partial class datum_config_entry_string_autoadmin_rank : datum_config_entry_string
{
    public datum_config_entry_string_autoadmin_rank()
    {
        @default = "Game Master";
        protection = CONFIG_ENTRY_LOCKED;
    }
}

// tg: code/controllers/configuration/entries/general.dm:10
[DmPath("/datum/config_entry/flag/auto_deadmin_always")]
public partial class datum_config_entry_flag_auto_deadmin_always : datum_config_entry_flag
{
    public datum_config_entry_flag_auto_deadmin_always()
    {
        protection = CONFIG_ENTRY_LOCKED;
    }
}

// tg: code/controllers/configuration/entries/general.dm:13
[DmPath("/datum/config_entry/number/auto_deadmin_timegate")]
public partial class datum_config_entry_number_auto_deadmin_timegate : datum_config_entry_number
{
    public datum_config_entry_number_auto_deadmin_timegate()
    {
        @default = null;
        protection = CONFIG_ENTRY_LOCKED;
    }
}

// tg: code/controllers/configuration/entries/general.dm:17
[DmPath("/datum/config_entry/flag/auto_deadmin_antagonists")]
public partial class datum_config_entry_flag_auto_deadmin_antagonists : datum_config_entry_flag
{
    public datum_config_entry_flag_auto_deadmin_antagonists()
    {
        protection = CONFIG_ENTRY_LOCKED;
    }
}

// tg: code/controllers/configuration/entries/general.dm:20
[DmPath("/datum/config_entry/flag/auto_deadmin_heads")]
public partial class datum_config_entry_flag_auto_deadmin_heads : datum_config_entry_flag
{
    public datum_config_entry_flag_auto_deadmin_heads()
    {
        protection = CONFIG_ENTRY_LOCKED;
    }
}

// tg: code/controllers/configuration/entries/general.dm:23
[DmPath("/datum/config_entry/flag/auto_deadmin_on_ready_or_latejoin")]
public partial class datum_config_entry_flag_auto_deadmin_on_ready_or_latejoin : datum_config_entry_flag
{
    public datum_config_entry_flag_auto_deadmin_on_ready_or_latejoin()
    {
        protection = CONFIG_ENTRY_LOCKED;
    }
}

// tg: code/controllers/configuration/entries/general.dm:26
[DmPath("/datum/config_entry/flag/auto_deadmin_silicons")]
public partial class datum_config_entry_flag_auto_deadmin_silicons : datum_config_entry_flag
{
    public datum_config_entry_flag_auto_deadmin_silicons()
    {
        protection = CONFIG_ENTRY_LOCKED;
    }
}

// tg: code/controllers/configuration/entries/general.dm:29
[DmPath("/datum/config_entry/flag/auto_deadmin_security")]
public partial class datum_config_entry_flag_auto_deadmin_security : datum_config_entry_flag
{
    public datum_config_entry_flag_auto_deadmin_security()
    {
        protection = CONFIG_ENTRY_LOCKED;
    }
}

/// server name (the name of the game window)
// tg: code/controllers/configuration/entries/general.dm:34
[DmPath("/datum/config_entry/string/servername")]
public partial class datum_config_entry_string_servername : datum_config_entry_string
{
}

/// short form server name used for the DB
// tg: code/controllers/configuration/entries/general.dm:37
[DmPath("/datum/config_entry/string/serversqlname")]
public partial class datum_config_entry_string_serversqlname : datum_config_entry_string
{
}

/// station name (the name of the station in-game)
// tg: code/controllers/configuration/entries/general.dm:40
[DmPath("/datum/config_entry/string/stationname")]
public partial class datum_config_entry_string_stationname : datum_config_entry_string
{
    // Deviation: the port's default station name; an empty STATIONNAME rolls tg's random one (a port-only flag type
    // would not register: the type registry takes the Tg assembly's types from the generated tg type list only).
    public datum_config_entry_string_stationname()
    {
        @default = "Anomaly Station";
    }
}

/// Countdown between lobby and the round starting.
// tg: code/controllers/configuration/entries/general.dm:43
[DmPath("/datum/config_entry/number/lobby_countdown")]
public partial class datum_config_entry_number_lobby_countdown : datum_config_entry_number
{
    public datum_config_entry_number_lobby_countdown()
    {
        @default = 120;
        integer = FALSE;
        min_val = 0;
    }
}

/// Post round murder death kill countdown.
// tg: code/controllers/configuration/entries/general.dm:49
[DmPath("/datum/config_entry/number/round_end_countdown")]
public partial class datum_config_entry_number_round_end_countdown : datum_config_entry_number
{
    public datum_config_entry_number_round_end_countdown()
    {
        @default = 25;
        integer = FALSE;
        min_val = 0;
    }
}

/// if the game appears on the hub or not
// tg: code/controllers/configuration/entries/general.dm:55
[DmPath("/datum/config_entry/flag/hub")]
public partial class datum_config_entry_flag_hub : datum_config_entry_flag
{
}

/// Pop requirement for the server to be removed from the hub
// tg: code/controllers/configuration/entries/general.dm:58
[DmPath("/datum/config_entry/number/max_hub_pop")]
public partial class datum_config_entry_number_max_hub_pop : datum_config_entry_number
{
    public datum_config_entry_number_max_hub_pop()
    {
        @default = 0; //0 means disabled
        integer = TRUE;
        min_val = 0;
    }
}

/// log messages sent in OOC
// tg: code/controllers/configuration/entries/general.dm:64
[DmPath("/datum/config_entry/flag/log_ooc")]
public partial class datum_config_entry_flag_log_ooc : datum_config_entry_flag
{
}

/// log login/logout
// tg: code/controllers/configuration/entries/general.dm:67
[DmPath("/datum/config_entry/flag/log_access")]
public partial class datum_config_entry_flag_log_access : datum_config_entry_flag
{
}

/// log blood worm actions like entering/leaving hosts
// tg: code/controllers/configuration/entries/general.dm:70
[DmPath("/datum/config_entry/flag/log_blood_worm")]
public partial class datum_config_entry_flag_log_blood_worm : datum_config_entry_flag
{
    public datum_config_entry_flag_log_blood_worm()
    {
        @default = TRUE;
    }
}

/// Config entry which special logging of failed logins under suspicious circumstances.
// tg: code/controllers/configuration/entries/general.dm:74
[DmPath("/datum/config_entry/flag/log_suspicious_login")]
public partial class datum_config_entry_flag_log_suspicious_login : datum_config_entry_flag
{
}

/// log client say
// tg: code/controllers/configuration/entries/general.dm:77
[DmPath("/datum/config_entry/flag/log_say")]
public partial class datum_config_entry_flag_log_say : datum_config_entry_flag
{
}

/// log admin actions
// tg: code/controllers/configuration/entries/general.dm:80
[DmPath("/datum/config_entry/flag/log_admin")]
public partial class datum_config_entry_flag_log_admin : datum_config_entry_flag
{
    public datum_config_entry_flag_log_admin()
    {
        protection = CONFIG_ENTRY_LOCKED;
    }
}

/// log prayers
// tg: code/controllers/configuration/entries/general.dm:84
[DmPath("/datum/config_entry/flag/log_prayer")]
public partial class datum_config_entry_flag_log_prayer : datum_config_entry_flag
{
}

///Log Music Requests
// tg: code/controllers/configuration/entries/general.dm:87
[DmPath("/datum/config_entry/flag/log_internet_request")]
public partial class datum_config_entry_flag_log_internet_request : datum_config_entry_flag
{
}

/// log silicons
// tg: code/controllers/configuration/entries/general.dm:90
[DmPath("/datum/config_entry/flag/log_silicon")]
public partial class datum_config_entry_flag_log_silicon : datum_config_entry_flag
{
}

// tg: code/controllers/configuration/entries/general.dm:92
[DmPath("/datum/config_entry/flag/log_law")]
public partial class datum_config_entry_flag_log_law : datum_config_entry_flag
{
    public datum_config_entry_flag_log_law()
    {
        deprecated_by = typeof(datum_config_entry_flag_log_silicon);
    }

    // tg: code/controllers/configuration/entries/general.dm:95
    public override object DeprecationUpdate(object value = null)
    {
        return value;
    }
}

/// log usage of tools
// tg: code/controllers/configuration/entries/general.dm:99
[DmPath("/datum/config_entry/flag/log_tools")]
public partial class datum_config_entry_flag_log_tools : datum_config_entry_flag
{
}

/// log game events
// tg: code/controllers/configuration/entries/general.dm:102
[DmPath("/datum/config_entry/flag/log_game")]
public partial class datum_config_entry_flag_log_game : datum_config_entry_flag
{
}

/// log minimap drawing events
// tg: code/controllers/configuration/entries/general.dm:105
[DmPath("/datum/config_entry/flag/log_minimap_drawing")]
public partial class datum_config_entry_flag_log_minimap_drawing : datum_config_entry_flag
{
}

/// log mech data
// tg: code/controllers/configuration/entries/general.dm:108
[DmPath("/datum/config_entry/flag/log_mecha")]
public partial class datum_config_entry_flag_log_mecha : datum_config_entry_flag
{
}

/// log virology data
// tg: code/controllers/configuration/entries/general.dm:111
[DmPath("/datum/config_entry/flag/log_virus")]
public partial class datum_config_entry_flag_log_virus : datum_config_entry_flag
{
}

/// log assets
// tg: code/controllers/configuration/entries/general.dm:114
[DmPath("/datum/config_entry/flag/log_asset")]
public partial class datum_config_entry_flag_log_asset : datum_config_entry_flag
{
}

/// log voting
// tg: code/controllers/configuration/entries/general.dm:117
[DmPath("/datum/config_entry/flag/log_vote")]
public partial class datum_config_entry_flag_log_vote : datum_config_entry_flag
{
}

/// log manual zone switching
// tg: code/controllers/configuration/entries/general.dm:120
[DmPath("/datum/config_entry/flag/log_zone_switch")]
public partial class datum_config_entry_flag_log_zone_switch : datum_config_entry_flag
{
}

/// log client whisper
// tg: code/controllers/configuration/entries/general.dm:123
[DmPath("/datum/config_entry/flag/log_whisper")]
public partial class datum_config_entry_flag_log_whisper : datum_config_entry_flag
{
}

/// log attack messages
// tg: code/controllers/configuration/entries/general.dm:126
[DmPath("/datum/config_entry/flag/log_attack")]
public partial class datum_config_entry_flag_log_attack : datum_config_entry_flag
{
}

/// log emotes
// tg: code/controllers/configuration/entries/general.dm:129
[DmPath("/datum/config_entry/flag/log_emote")]
public partial class datum_config_entry_flag_log_emote : datum_config_entry_flag
{
}

/// log ghost polling
// tg: code/controllers/configuration/entries/general.dm:132
[DmPath("/datum/config_entry/flag/log_ghost_poll")]
public partial class datum_config_entry_flag_log_ghost_poll : datum_config_entry_flag
{
    public datum_config_entry_flag_log_ghost_poll()
    {
        @default = TRUE;
    }
}

/// log economy actions
// tg: code/controllers/configuration/entries/general.dm:136
[DmPath("/datum/config_entry/flag/log_econ")]
public partial class datum_config_entry_flag_log_econ : datum_config_entry_flag
{
}

/// log traitor objectives
// tg: code/controllers/configuration/entries/general.dm:139
[DmPath("/datum/config_entry/flag/log_traitor")]
public partial class datum_config_entry_flag_log_traitor : datum_config_entry_flag
{
}

/// log admin chat messages
// tg: code/controllers/configuration/entries/general.dm:142
[DmPath("/datum/config_entry/flag/log_adminchat")]
public partial class datum_config_entry_flag_log_adminchat : datum_config_entry_flag
{
    public datum_config_entry_flag_log_adminchat()
    {
        protection = CONFIG_ENTRY_LOCKED;
    }
}

/// log pda messages
// tg: code/controllers/configuration/entries/general.dm:146
[DmPath("/datum/config_entry/flag/log_pda")]
public partial class datum_config_entry_flag_log_pda : datum_config_entry_flag
{
}

/// log uplink/spellbook/codex ciatrix purchases and refunds
// tg: code/controllers/configuration/entries/general.dm:149
[DmPath("/datum/config_entry/flag/log_uplink")]
public partial class datum_config_entry_flag_log_uplink : datum_config_entry_flag
{
}

/// log telecomms messages
// tg: code/controllers/configuration/entries/general.dm:152
[DmPath("/datum/config_entry/flag/log_telecomms")]
public partial class datum_config_entry_flag_log_telecomms : datum_config_entry_flag
{
}

/// log speech indicators(started/stopped speaking)
// tg: code/controllers/configuration/entries/general.dm:155
[DmPath("/datum/config_entry/flag/log_speech_indicators")]
public partial class datum_config_entry_flag_log_speech_indicators : datum_config_entry_flag
{
}

/// log certain expliotable parrots and other such fun things in a JSON file of twitter valid phrases.
// tg: code/controllers/configuration/entries/general.dm:158
[DmPath("/datum/config_entry/flag/log_twitter")]
public partial class datum_config_entry_flag_log_twitter : datum_config_entry_flag
{
}

/// log all world.Topic() calls
// tg: code/controllers/configuration/entries/general.dm:161
[DmPath("/datum/config_entry/flag/log_world_topic")]
public partial class datum_config_entry_flag_log_world_topic : datum_config_entry_flag
{
}

/// log crew manifest to separate file
// tg: code/controllers/configuration/entries/general.dm:164
[DmPath("/datum/config_entry/flag/log_manifest")]
public partial class datum_config_entry_flag_log_manifest : datum_config_entry_flag
{
}

/// log roundstart divide occupations debug information to a file
// tg: code/controllers/configuration/entries/general.dm:167
[DmPath("/datum/config_entry/flag/log_job_debug")]
public partial class datum_config_entry_flag_log_job_debug : datum_config_entry_flag
{
}

/// log shuttle related actions, ie shuttle computers, shuttle manipulator, emergency console
// tg: code/controllers/configuration/entries/general.dm:170
[DmPath("/datum/config_entry/flag/log_shuttle")]
public partial class datum_config_entry_flag_log_shuttle : datum_config_entry_flag
{
}

/// log image authoring, such as using the modular computer paint app
// tg: code/controllers/configuration/entries/general.dm:173
[DmPath("/datum/config_entry/flag/log_image")]
public partial class datum_config_entry_flag_log_image : datum_config_entry_flag
{
}

/// logs all timers in buckets on automatic bucket reset (Useful for timer debugging)
// tg: code/controllers/configuration/entries/general.dm:176
[DmPath("/datum/config_entry/flag/log_timers_on_bucket_reset")]
public partial class datum_config_entry_flag_log_timers_on_bucket_reset : datum_config_entry_flag
{
}

/// Log human readable versions of json log entries
// tg: code/controllers/configuration/entries/general.dm:179
[DmPath("/datum/config_entry/flag/log_as_human_readable")]
public partial class datum_config_entry_flag_log_as_human_readable : datum_config_entry_flag
{
    public datum_config_entry_flag_log_as_human_readable()
    {
        @default = TRUE;
    }
}

/// allows admins with relevant permissions to have their own ooc colour
// tg: code/controllers/configuration/entries/general.dm:183
[DmPath("/datum/config_entry/flag/allow_admin_ooccolor")]
public partial class datum_config_entry_flag_allow_admin_ooccolor : datum_config_entry_flag
{
}

/// allows admins with relevant permissions to have a personalized asay color
// tg: code/controllers/configuration/entries/general.dm:186
[DmPath("/datum/config_entry/flag/allow_admin_asaycolor")]
public partial class datum_config_entry_flag_allow_admin_asaycolor : datum_config_entry_flag
{
}

/// allow votes to restart
// tg: code/controllers/configuration/entries/general.dm:189
[DmPath("/datum/config_entry/flag/allow_vote_restart")]
public partial class datum_config_entry_flag_allow_vote_restart : datum_config_entry_flag
{
}

/// allow votes to change map
// tg: code/controllers/configuration/entries/general.dm:192
[DmPath("/datum/config_entry/flag/allow_vote_map")]
public partial class datum_config_entry_flag_allow_vote_map : datum_config_entry_flag
{
}

/// minimum time between voting sessions (deciseconds, 10 minute default)
// tg: code/controllers/configuration/entries/general.dm:195
[DmPath("/datum/config_entry/number/vote_delay")]
public partial class datum_config_entry_number_vote_delay : datum_config_entry_number
{
    public datum_config_entry_number_vote_delay()
    {
        @default = 10 * MINUTES;
        integer = FALSE;
        min_val = 0;
    }
}

/// length of voting period (deciseconds, default 1 minute)
// tg: code/controllers/configuration/entries/general.dm:201
[DmPath("/datum/config_entry/number/vote_period")]
public partial class datum_config_entry_number_vote_period : datum_config_entry_number
{
    public datum_config_entry_number_vote_period()
    {
        @default = 1 * MINUTES;
        integer = FALSE;
        min_val = 0;
    }
}

/// If disabled, non-voters will automatically have their votes added to certain vote options
/// (For example: restart votes will default to "no restart", map votes will default to their preferred map / default map, rocking the vote will default to "no")
// tg: code/controllers/configuration/entries/general.dm:208
[DmPath("/datum/config_entry/flag/default_no_vote")]
public partial class datum_config_entry_flag_default_no_vote : datum_config_entry_flag
{
}

/// Prevents dead people from voting.
// tg: code/controllers/configuration/entries/general.dm:211
[DmPath("/datum/config_entry/flag/no_dead_vote")]
public partial class datum_config_entry_flag_no_dead_vote : datum_config_entry_flag
{
}

/// Gives the ability to send players a maptext popup.
// tg: code/controllers/configuration/entries/general.dm:214
[DmPath("/datum/config_entry/flag/popup_admin_pm")]
public partial class datum_config_entry_flag_popup_admin_pm : datum_config_entry_flag
{
}

// tg: code/controllers/configuration/entries/general.dm:216
[DmPath("/datum/config_entry/number/fps")]
public partial class datum_config_entry_number_fps : datum_config_entry_number
{
    // tg: code/controllers/configuration/entries/general.dm:221
    public float sync_validate = FALSE;

    public datum_config_entry_number_fps()
    {
        @default = 20;
        integer = FALSE;
        min_val = 1;
        max_val = 100; //byond will start crapping out at 50, so this is just ridic
    }

    // tg: code/controllers/configuration/entries/general.dm:223
    public override async DmTask<object> ValidateAndSet(object str_val = null)
    {
        object dot = null;
        dot = await base.ValidateAndSet(str_val);
        if (Dm.truthy(dot))
        {
            sync_validate = TRUE;
            datum_config_entry_number_ticklag TL = config.entries_by_type[typeof(datum_config_entry_number_ticklag)] as datum_config_entry_number_ticklag;
            if (!Dm.truthy(TL.sync_validate))
                await TL.ValidateAndSet(Dm.text("[]", Dm.div(10, config_entry_value)));
            sync_validate = FALSE;
        }
        return dot;
    }
}

// tg: code/controllers/configuration/entries/general.dm:232
[DmPath("/datum/config_entry/number/ticklag")]
public partial class datum_config_entry_number_ticklag : datum_config_entry_number
{
    // tg: code/controllers/configuration/entries/general.dm:234
    public float sync_validate = FALSE;

    public datum_config_entry_number_ticklag()
    {
        integer = FALSE;
    }

    //ticklag weirdly just mirrors fps
    // tg: code/controllers/configuration/entries/general.dm:236
    public override async DmTask New(params object[] __extra)
    {
        Type CE = typeof(datum_config_entry_number_fps);
        @default = Dm.div(10, Dm.initial(CE, (datum_config_entry p) => p.@default));
        await base.New(__extra);
    }

    // tg: code/controllers/configuration/entries/general.dm:241
    public override async DmTask<object> ValidateAndSet(object str_val = null)
    {
        object dot = null;
        dot = (Dm.num(Dm.num(text2num(str_val)) > 0) is var and__1 && !Dm.truthy(and__1) ? and__1 : await base.ValidateAndSet(str_val));
        if (Dm.truthy(dot))
        {
            sync_validate = TRUE;
            datum_config_entry_number_fps FPS = config.entries_by_type[typeof(datum_config_entry_number_fps)] as datum_config_entry_number_fps;
            if (!Dm.truthy(FPS.sync_validate))
                await FPS.ValidateAndSet(Dm.text("[]", Dm.div(10, config_entry_value)));
            sync_validate = FALSE;
        }
        return dot;
    }
}

// tg: code/controllers/configuration/entries/general.dm:250
[DmPath("/datum/config_entry/flag/allow_holidays")]
public partial class datum_config_entry_flag_allow_holidays : datum_config_entry_flag
{
}

// tg: code/controllers/configuration/entries/general.dm:253
[DmPath("/datum/config_entry/flag/admin_legacy_system")]
public partial class datum_config_entry_flag_admin_legacy_system : datum_config_entry_flag //Defines whether the server uses the legacy admin system with admins.txt or the SQL system
{
    public datum_config_entry_flag_admin_legacy_system()
    {
        protection = CONFIG_ENTRY_LOCKED;
    }
}

// tg: code/controllers/configuration/entries/general.dm:256
[DmPath("/datum/config_entry/flag/protect_legacy_admins")]
public partial class datum_config_entry_flag_protect_legacy_admins : datum_config_entry_flag //Stops any admins loaded by the legacy system from having their rank edited by the permissions panel
{
    public datum_config_entry_flag_protect_legacy_admins()
    {
        protection = CONFIG_ENTRY_LOCKED;
    }
}

// tg: code/controllers/configuration/entries/general.dm:259
[DmPath("/datum/config_entry/flag/protect_legacy_ranks")]
public partial class datum_config_entry_flag_protect_legacy_ranks : datum_config_entry_flag //Stops any ranks loaded by the legacy system from having their flags edited by the permissions panel
{
    public datum_config_entry_flag_protect_legacy_ranks()
    {
        protection = CONFIG_ENTRY_LOCKED;
    }
}

// tg: code/controllers/configuration/entries/general.dm:262
[DmPath("/datum/config_entry/flag/enable_localhost_rank")]
public partial class datum_config_entry_flag_enable_localhost_rank : datum_config_entry_flag //Gives the !localhost! rank to any client connecting from 127.0.0.1 or ::1
{
    public datum_config_entry_flag_enable_localhost_rank()
    {
        protection = CONFIG_ENTRY_LOCKED;
    }
}

// tg: code/controllers/configuration/entries/general.dm:265
[DmPath("/datum/config_entry/flag/load_legacy_ranks_only")]
public partial class datum_config_entry_flag_load_legacy_ranks_only : datum_config_entry_flag //Loads admin ranks only from legacy admin_ranks.txt, while enabled ranks are mirrored to the database
{
    public datum_config_entry_flag_load_legacy_ranks_only()
    {
        protection = CONFIG_ENTRY_LOCKED;
    }
}

// tg: code/controllers/configuration/entries/general.dm:268
[DmPath("/datum/config_entry/string/hostedby")]
public partial class datum_config_entry_string_hostedby : datum_config_entry_string
{
}

/// Determines if a player can respawn after dying.
/// 0 / RESPAWN_FLAG_DISABLED = Cannot respawn (default)
/// 1 / RESPAWN_FLAG_FREE = Can respawn
/// 2 / RESPAWN_FLAG_NEW_CHARACTER = Can respawn if choosing a different character
// tg: code/controllers/configuration/entries/general.dm:274
[DmPath("/datum/config_entry/flag/allow_respawn")]
public partial class datum_config_entry_flag_allow_respawn : datum_config_entry_flag
{
    public datum_config_entry_flag_allow_respawn()
    {
        @default = RESPAWN_FLAG_DISABLED;
    }

    // tg: code/controllers/configuration/entries/general.dm:277
    public override async DmTask<object> ValidateAndSet(object str_val = null)
    {
        if (!Dm.truthy(VASProcCallGuard(str_val)))
            return FALSE;
        object val_as_num = text2num(str_val);
        if (Dm.@in(val_as_num, Dm.list(RESPAWN_FLAG_DISABLED, RESPAWN_FLAG_FREE, RESPAWN_FLAG_NEW_CHARACTER)))
        {
            config_entry_value = val_as_num;
            return TRUE;
        }
        return FALSE;
    }
}

/// Determines how long (in deciseconds) before a player is allowed to respawn.
// tg: code/controllers/configuration/entries/general.dm:287
[DmPath("/datum/config_entry/number/respawn_delay")]
public partial class datum_config_entry_number_respawn_delay : datum_config_entry_number
{
    public datum_config_entry_number_respawn_delay()
    {
        @default = 0 * SECONDS;
    }
}

// tg: code/controllers/configuration/entries/general.dm:290
[DmPath("/datum/config_entry/flag/usewhitelist")]
public partial class datum_config_entry_flag_usewhitelist : datum_config_entry_flag
{
}

// tg: code/controllers/configuration/entries/general.dm:292
[DmPath("/datum/config_entry/flag/use_age_restriction_for_jobs")]
public partial class datum_config_entry_flag_use_age_restriction_for_jobs : datum_config_entry_flag //Do jobs use account age restrictions? --requires database
{
}

// tg: code/controllers/configuration/entries/general.dm:294
[DmPath("/datum/config_entry/flag/use_account_age_for_jobs")]
public partial class datum_config_entry_flag_use_account_age_for_jobs : datum_config_entry_flag //Uses the time they made the account for the job restriction stuff. New player joining alerts should be unaffected.
{
}

// tg: code/controllers/configuration/entries/general.dm:296
[DmPath("/datum/config_entry/flag/use_exp_tracking")]
public partial class datum_config_entry_flag_use_exp_tracking : datum_config_entry_flag
{
}

/// Enables head jobs time restrictions.
// tg: code/controllers/configuration/entries/general.dm:299
[DmPath("/datum/config_entry/flag/use_exp_restrictions_heads")]
public partial class datum_config_entry_flag_use_exp_restrictions_heads : datum_config_entry_flag
{
}

// tg: code/controllers/configuration/entries/general.dm:301
[DmPath("/datum/config_entry/number/use_exp_restrictions_heads_hours")]
public partial class datum_config_entry_number_use_exp_restrictions_heads_hours : datum_config_entry_number
{
    public datum_config_entry_number_use_exp_restrictions_heads_hours()
    {
        @default = 0;
        integer = FALSE;
        min_val = 0;
    }
}

// tg: code/controllers/configuration/entries/general.dm:306
[DmPath("/datum/config_entry/flag/use_exp_restrictions_heads_department")]
public partial class datum_config_entry_flag_use_exp_restrictions_heads_department : datum_config_entry_flag
{
}

/// Enables non-head jobs time restrictions.
// tg: code/controllers/configuration/entries/general.dm:309
[DmPath("/datum/config_entry/flag/use_exp_restrictions_other")]
public partial class datum_config_entry_flag_use_exp_restrictions_other : datum_config_entry_flag
{
}

// tg: code/controllers/configuration/entries/general.dm:311
[DmPath("/datum/config_entry/flag/use_exp_restrictions_admin_bypass")]
public partial class datum_config_entry_flag_use_exp_restrictions_admin_bypass : datum_config_entry_flag
{
}

// tg: code/controllers/configuration/entries/general.dm:313
[DmPath("/datum/config_entry/flag/use_low_living_hour_intern")]
public partial class datum_config_entry_flag_use_low_living_hour_intern : datum_config_entry_flag
{
}

// tg: code/controllers/configuration/entries/general.dm:315
[DmPath("/datum/config_entry/number/use_low_living_hour_intern_hours")]
public partial class datum_config_entry_number_use_low_living_hour_intern_hours : datum_config_entry_number
{
    public datum_config_entry_number_use_low_living_hour_intern_hours()
    {
        @default = 0;
        integer = FALSE;
        min_val = 0;
    }
}

// tg: code/controllers/configuration/entries/general.dm:320
[DmPath("/datum/config_entry/string/server")]
public partial class datum_config_entry_string_server : datum_config_entry_string
{
}

// tg: code/controllers/configuration/entries/general.dm:322
[DmPath("/datum/config_entry/string/public_address")]
public partial class datum_config_entry_string_public_address : datum_config_entry_string
{
}

// tg: code/controllers/configuration/entries/general.dm:324
[DmPath("/datum/config_entry/string/banappeals")]
public partial class datum_config_entry_string_banappeals : datum_config_entry_string
{
}

// tg: code/controllers/configuration/entries/general.dm:326
[DmPath("/datum/config_entry/string/wikiurl")]
public partial class datum_config_entry_string_wikiurl : datum_config_entry_string
{
    public datum_config_entry_string_wikiurl()
    {
        @default = "https://wiki.tgstation13.org";
    }
}

// tg: code/controllers/configuration/entries/general.dm:329
[DmPath("/datum/config_entry/string/forumurl")]
public partial class datum_config_entry_string_forumurl : datum_config_entry_string
{
    public datum_config_entry_string_forumurl()
    {
        @default = "http://tgstation13.org/phpBB/index.php";
    }
}

// tg: code/controllers/configuration/entries/general.dm:332
[DmPath("/datum/config_entry/string/rulesurl")]
public partial class datum_config_entry_string_rulesurl : datum_config_entry_string
{
    public datum_config_entry_string_rulesurl()
    {
        @default = "http://tgstation13.org/wiki/Rules";
    }
}

// tg: code/controllers/configuration/entries/general.dm:335
[DmPath("/datum/config_entry/string/githuburl")]
public partial class datum_config_entry_string_githuburl : datum_config_entry_string
{
    public datum_config_entry_string_githuburl()
    {
        @default = "https://www.github.com/tgstation/tgstation";
    }
}

// tg: code/controllers/configuration/entries/general.dm:338
[DmPath("/datum/config_entry/string/configurl")]
public partial class datum_config_entry_string_configurl : datum_config_entry_string
{
    public datum_config_entry_string_configurl()
    {
        @default = "https://github.com/tgstation-operations/server-config";
    }
}

// tg: code/controllers/configuration/entries/general.dm:341
[DmPath("/datum/config_entry/string/discordbotcommandprefix")]
public partial class datum_config_entry_string_discordbotcommandprefix : datum_config_entry_string
{
    public datum_config_entry_string_discordbotcommandprefix()
    {
        @default = "?";
    }
}

// tg: code/controllers/configuration/entries/general.dm:344
[DmPath("/datum/config_entry/string/roundstatsurl")]
public partial class datum_config_entry_string_roundstatsurl : datum_config_entry_string
{
}

// tg: code/controllers/configuration/entries/general.dm:346
[DmPath("/datum/config_entry/string/gamelogurl")]
public partial class datum_config_entry_string_gamelogurl : datum_config_entry_string
{
}

// tg: code/controllers/configuration/entries/general.dm:348
[DmPath("/datum/config_entry/flag/guest_ban")]
public partial class datum_config_entry_flag_guest_ban : datum_config_entry_flag
{
}

// tg: code/controllers/configuration/entries/general.dm:350
[DmPath("/datum/config_entry/number/id_console_jobslot_delay")]
public partial class datum_config_entry_number_id_console_jobslot_delay : datum_config_entry_number
{
    public datum_config_entry_number_id_console_jobslot_delay()
    {
        @default = 30;
        integer = FALSE;
        min_val = 0;
    }
}

// tg: code/controllers/configuration/entries/general.dm:355
[DmPath("/datum/config_entry/number/inactivity_period")]
public partial class datum_config_entry_number_inactivity_period : datum_config_entry_number //time in ds until a player is considered inactive
{
    public datum_config_entry_number_inactivity_period()
    {
        @default = 3000;
        integer = FALSE;
        min_val = 0;
    }

    // tg: code/controllers/configuration/entries/general.dm:360
    public override async DmTask<object> ValidateAndSet(object str_val = null)
    {
        object dot = null;
        dot = await base.ValidateAndSet(str_val);
        if (Dm.truthy(dot))
            config_entry_value = Dm.mul(config_entry_value, 10); //documented as seconds in config.txt
        return dot;
    }
}

// tg: code/controllers/configuration/entries/general.dm:365
[DmPath("/datum/config_entry/number/afk_period")]
public partial class datum_config_entry_number_afk_period : datum_config_entry_number //time in ds until a player is considered inactive
{
    public datum_config_entry_number_afk_period()
    {
        @default = 3000;
        integer = FALSE;
        min_val = 0;
    }

    // tg: code/controllers/configuration/entries/general.dm:370
    public override async DmTask<object> ValidateAndSet(object str_val = null)
    {
        object dot = null;
        dot = await base.ValidateAndSet(str_val);
        if (Dm.truthy(dot))
            config_entry_value = Dm.mul(config_entry_value, 10); //documented as seconds in config.txt
        return dot;
    }
}

// tg: code/controllers/configuration/entries/general.dm:375
[DmPath("/datum/config_entry/flag/kick_inactive")]
public partial class datum_config_entry_flag_kick_inactive : datum_config_entry_flag //force disconnect for inactive players
{
}

// tg: code/controllers/configuration/entries/general.dm:377
[DmPath("/datum/config_entry/flag/load_jobs_from_txt")]
public partial class datum_config_entry_flag_load_jobs_from_txt : datum_config_entry_flag
{
}

// tg: code/controllers/configuration/entries/general.dm:379
[DmPath("/datum/config_entry/flag/forbid_singulo_possession")]
public partial class datum_config_entry_flag_forbid_singulo_possession : datum_config_entry_flag
{
}

// tg: code/controllers/configuration/entries/general.dm:381
[DmPath("/datum/config_entry/flag/automute_on")]
public partial class datum_config_entry_flag_automute_on : datum_config_entry_flag //enables automuting/spam prevention
{
}

// tg: code/controllers/configuration/entries/general.dm:383
[DmPath("/datum/config_entry/string/panic_server_name")]
public partial class datum_config_entry_string_panic_server_name : datum_config_entry_string
{
    // tg: code/controllers/configuration/entries/general.dm:385
    public override async DmTask<object> ValidateAndSet(object str_val = null)
    {
        return (Dm.num(!Dm.eq(str_val, "[Put the name here]")) is var and__1 && !Dm.truthy(and__1) ? and__1 : await base.ValidateAndSet(str_val));
    }
}

// tg: code/controllers/configuration/entries/general.dm:388
[DmPath("/datum/config_entry/string/panic_server_address")]
public partial class datum_config_entry_string_panic_server_address : datum_config_entry_string //Reconnect a player this linked server if this server isn't accepting new players
{
    // tg: code/controllers/configuration/entries/general.dm:390
    public override async DmTask<object> ValidateAndSet(object str_val = null)
    {
        return (Dm.num(!Dm.eq(str_val, "byond://address:port")) is var and__1 && !Dm.truthy(and__1) ? and__1 : await base.ValidateAndSet(str_val));
    }
}

// tg: code/controllers/configuration/entries/general.dm:393
[DmPath("/datum/config_entry/string/invoke_youtubedl")]
public partial class datum_config_entry_string_invoke_youtubedl : datum_config_entry_string
{
    public datum_config_entry_string_invoke_youtubedl()
    {
        protection = Dm.bitor(CONFIG_ENTRY_LOCKED, CONFIG_ENTRY_HIDDEN);
    }
}

// tg: code/controllers/configuration/entries/general.dm:396
[DmPath("/datum/config_entry/flag/request_internet_sound")]
public partial class datum_config_entry_flag_request_internet_sound : datum_config_entry_flag
{
}

// tg: code/controllers/configuration/entries/general.dm:398
[DmPath("/datum/config_entry/string/request_internet_allowed")]
public partial class datum_config_entry_string_request_internet_allowed : datum_config_entry_string
{
    public datum_config_entry_string_request_internet_allowed()
    {
        protection = CONFIG_ENTRY_LOCKED;
    }
}

// tg: code/controllers/configuration/entries/general.dm:401
[DmPath("/datum/config_entry/flag/show_irc_name")]
public partial class datum_config_entry_flag_show_irc_name : datum_config_entry_flag
{
}

// tg: code/controllers/configuration/entries/general.dm:403
[DmPath("/datum/config_entry/flag/no_default_techweb_link")]
public partial class datum_config_entry_flag_no_default_techweb_link : datum_config_entry_flag
{
}

// tg: code/controllers/configuration/entries/general.dm:405
[DmPath("/datum/config_entry/flag/see_own_notes")]
public partial class datum_config_entry_flag_see_own_notes : datum_config_entry_flag //Can players see their own admin notes
{
}

// tg: code/controllers/configuration/entries/general.dm:407
[DmPath("/datum/config_entry/number/note_fresh_days")]
public partial class datum_config_entry_number_note_fresh_days : datum_config_entry_number
{
    public datum_config_entry_number_note_fresh_days()
    {
        @default = null;
        min_val = 0;
        integer = FALSE;
    }
}

// tg: code/controllers/configuration/entries/general.dm:412
[DmPath("/datum/config_entry/number/note_stale_days")]
public partial class datum_config_entry_number_note_stale_days : datum_config_entry_number
{
    public datum_config_entry_number_note_stale_days()
    {
        @default = null;
        min_val = 0;
        integer = FALSE;
    }
}

// tg: code/controllers/configuration/entries/general.dm:417
[DmPath("/datum/config_entry/flag/maprotation")]
public partial class datum_config_entry_flag_maprotation : datum_config_entry_flag
{
}

// tg: code/controllers/configuration/entries/general.dm:419
[DmPath("/datum/config_entry/number/auto_lag_switch_pop")]
public partial class datum_config_entry_number_auto_lag_switch_pop : datum_config_entry_number //Number of clients at which drastic lag mitigation measures kick in
{
    public datum_config_entry_number_auto_lag_switch_pop()
    {
        config_entry_value = null;
        min_val = 0;
    }
}

// tg: code/controllers/configuration/entries/general.dm:423
[DmPath("/datum/config_entry/number/soft_popcap")]
public partial class datum_config_entry_number_soft_popcap : datum_config_entry_number
{
    public datum_config_entry_number_soft_popcap()
    {
        @default = null;
        min_val = 0;
    }
}

// tg: code/controllers/configuration/entries/general.dm:427
[DmPath("/datum/config_entry/number/hard_popcap")]
public partial class datum_config_entry_number_hard_popcap : datum_config_entry_number
{
    public datum_config_entry_number_hard_popcap()
    {
        @default = null;
        min_val = 0;
    }
}

// tg: code/controllers/configuration/entries/general.dm:431
[DmPath("/datum/config_entry/number/extreme_popcap")]
public partial class datum_config_entry_number_extreme_popcap : datum_config_entry_number
{
    public datum_config_entry_number_extreme_popcap()
    {
        @default = null;
        min_val = 0;
    }
}

// tg: code/controllers/configuration/entries/general.dm:435
[DmPath("/datum/config_entry/string/soft_popcap_message")]
public partial class datum_config_entry_string_soft_popcap_message : datum_config_entry_string
{
    public datum_config_entry_string_soft_popcap_message()
    {
        @default = "Be warned that the server is currently serving a high number of users, consider using alternative game servers.";
    }
}

// tg: code/controllers/configuration/entries/general.dm:438
[DmPath("/datum/config_entry/string/hard_popcap_message")]
public partial class datum_config_entry_string_hard_popcap_message : datum_config_entry_string
{
    public datum_config_entry_string_hard_popcap_message()
    {
        @default = "The server is currently serving a high number of users, You cannot currently join. You may wait for the number of living crew to decline, observe, or find alternative servers.";
    }
}

// tg: code/controllers/configuration/entries/general.dm:441
[DmPath("/datum/config_entry/string/extreme_popcap_message")]
public partial class datum_config_entry_string_extreme_popcap_message : datum_config_entry_string
{
    public datum_config_entry_string_extreme_popcap_message()
    {
        @default = "The server is currently serving a high number of users, find alternative servers.";
    }
}

// tg: code/controllers/configuration/entries/general.dm:444
[DmPath("/datum/config_entry/flag/byond_member_bypass_popcap")]
public partial class datum_config_entry_flag_byond_member_bypass_popcap : datum_config_entry_flag
{
}

// tg: code/controllers/configuration/entries/general.dm:446
[DmPath("/datum/config_entry/flag/panic_bunker")]
public partial class datum_config_entry_flag_panic_bunker : datum_config_entry_flag // prevents people the server hasn't seen before from connecting
{
}

// tg: code/controllers/configuration/entries/general.dm:448
[DmPath("/datum/config_entry/number/panic_bunker_living")]
public partial class datum_config_entry_number_panic_bunker_living : datum_config_entry_number // living time in minutes that a player needs to pass the panic bunker
{
}

/// Flag for requiring players who would otherwise be denied access by the panic bunker to complete a written interview
// tg: code/controllers/configuration/entries/general.dm:451
[DmPath("/datum/config_entry/flag/panic_bunker_interview")]
public partial class datum_config_entry_flag_panic_bunker_interview : datum_config_entry_flag
{
}

// tg: code/controllers/configuration/entries/general.dm:453
[DmPath("/datum/config_entry/string/panic_bunker_message")]
public partial class datum_config_entry_string_panic_bunker_message : datum_config_entry_string
{
    public datum_config_entry_string_panic_bunker_message()
    {
        @default = "Sorry but the server is currently not accepting connections from never before seen players.";
    }
}

// tg: code/controllers/configuration/entries/general.dm:456
[DmPath("/datum/config_entry/number/notify_new_player_age")]
public partial class datum_config_entry_number_notify_new_player_age : datum_config_entry_number // how long do we notify admins of a new player
{
    public datum_config_entry_number_notify_new_player_age()
    {
        min_val = -1;
    }
}

// tg: code/controllers/configuration/entries/general.dm:459
[DmPath("/datum/config_entry/number/notify_new_player_account_age")]
public partial class datum_config_entry_number_notify_new_player_account_age : datum_config_entry_number // how long do we notify admins of a new byond account
{
    public datum_config_entry_number_notify_new_player_account_age()
    {
        min_val = 0;
    }
}

// tg: code/controllers/configuration/entries/general.dm:462
[DmPath("/datum/config_entry/flag/irc_first_connection_alert")]
public partial class datum_config_entry_flag_irc_first_connection_alert : datum_config_entry_flag // do we notify the irc channel when somebody is connecting for the first time?
{
}

// tg: code/controllers/configuration/entries/general.dm:464
[DmPath("/datum/config_entry/string/ipintel_base")]
public partial class datum_config_entry_string_ipintel_base : datum_config_entry_string
{
    public datum_config_entry_string_ipintel_base()
    {
        @default = "check.getipintel.net";
    }
}

// tg: code/controllers/configuration/entries/general.dm:467
[DmPath("/datum/config_entry/string/ipintel_email")]
public partial class datum_config_entry_string_ipintel_email : datum_config_entry_string
{
    // tg: code/controllers/configuration/entries/general.dm:469
    public override async DmTask<object> ValidateAndSet(object str_val = null)
    {
        return (Dm.num(!Dm.eq(str_val, "ch@nge.me")) is var and__1 && !Dm.truthy(and__1) ? and__1
            : (Dm.num(!Dm.truthy(length(str_val))) is var or__1 && Dm.truthy(or__1) ? or__1 : findtext(str_val, "@")) is var and__2 && !Dm.truthy(and__2) ? and__2
            : await base.ValidateAndSet(str_val));
    }
}

// tg: code/controllers/configuration/entries/general.dm:472
[DmPath("/datum/config_entry/number/ipintel_rating_bad")]
public partial class datum_config_entry_number_ipintel_rating_bad : datum_config_entry_number
{
    public datum_config_entry_number_ipintel_rating_bad()
    {
        @default = 1;
        integer = FALSE;
        min_val = 0;
        max_val = 1;
    }
}

// tg: code/controllers/configuration/entries/general.dm:478
[DmPath("/datum/config_entry/flag/ipintel_reject_rate_limited")]
public partial class datum_config_entry_flag_ipintel_reject_rate_limited : datum_config_entry_flag
{
    public datum_config_entry_flag_ipintel_reject_rate_limited()
    {
        @default = FALSE;
    }
}

// tg: code/controllers/configuration/entries/general.dm:481
[DmPath("/datum/config_entry/flag/ipintel_reject_bad")]
public partial class datum_config_entry_flag_ipintel_reject_bad : datum_config_entry_flag
{
    public datum_config_entry_flag_ipintel_reject_bad()
    {
        @default = FALSE;
    }
}

// tg: code/controllers/configuration/entries/general.dm:484
[DmPath("/datum/config_entry/flag/ipintel_reject_unknown")]
public partial class datum_config_entry_flag_ipintel_reject_unknown : datum_config_entry_flag
{
    public datum_config_entry_flag_ipintel_reject_unknown()
    {
        @default = FALSE;
    }
}

// tg: code/controllers/configuration/entries/general.dm:487
[DmPath("/datum/config_entry/number/ipintel_rate_minute")]
public partial class datum_config_entry_number_ipintel_rate_minute : datum_config_entry_number
{
    public datum_config_entry_number_ipintel_rate_minute()
    {
        @default = 15;
        min_val = 0;
    }
}

// tg: code/controllers/configuration/entries/general.dm:491
[DmPath("/datum/config_entry/number/ipintel_cache_length")]
public partial class datum_config_entry_number_ipintel_cache_length : datum_config_entry_number
{
    public datum_config_entry_number_ipintel_cache_length()
    {
        @default = 7;
        min_val = 0;
    }
}

// tg: code/controllers/configuration/entries/general.dm:495
[DmPath("/datum/config_entry/number/ipintel_exempt_playtime_living")]
public partial class datum_config_entry_number_ipintel_exempt_playtime_living : datum_config_entry_number
{
    public datum_config_entry_number_ipintel_exempt_playtime_living()
    {
        @default = 5;
        min_val = 0;
    }
}

// tg: code/controllers/configuration/entries/general.dm:499
[DmPath("/datum/config_entry/flag/aggressive_changelog")]
public partial class datum_config_entry_flag_aggressive_changelog : datum_config_entry_flag
{
}

// tg: code/controllers/configuration/entries/general.dm:501
[DmPath("/datum/config_entry/flag/autoconvert_notes")]
public partial class datum_config_entry_flag_autoconvert_notes : datum_config_entry_flag //if all connecting player's notes should attempt to be converted to the database
{
    public datum_config_entry_flag_autoconvert_notes()
    {
        protection = CONFIG_ENTRY_LOCKED;
    }
}

// tg: code/controllers/configuration/entries/general.dm:504
[DmPath("/datum/config_entry/flag/allow_webclient")]
public partial class datum_config_entry_flag_allow_webclient : datum_config_entry_flag
{
}

// tg: code/controllers/configuration/entries/general.dm:506
[DmPath("/datum/config_entry/flag/webclient_only_byond_members")]
public partial class datum_config_entry_flag_webclient_only_byond_members : datum_config_entry_flag
{
}

// tg: code/controllers/configuration/entries/general.dm:508
[DmPath("/datum/config_entry/flag/announce_admin_logout")]
public partial class datum_config_entry_flag_announce_admin_logout : datum_config_entry_flag
{
}

// tg: code/controllers/configuration/entries/general.dm:510
[DmPath("/datum/config_entry/flag/announce_admin_login")]
public partial class datum_config_entry_flag_announce_admin_login : datum_config_entry_flag
{
}

// tg: code/controllers/configuration/entries/general.dm:512
[DmPath("/datum/config_entry/flag/allow_map_voting")]
public partial class datum_config_entry_flag_allow_map_voting : datum_config_entry_flag
{
    public datum_config_entry_flag_allow_map_voting()
    {
        deprecated_by = typeof(datum_config_entry_flag_preference_map_voting);
    }

    // tg: code/controllers/configuration/entries/general.dm:515
    public override object DeprecationUpdate(object value = null)
    {
        return value;
    }
}

// tg: code/controllers/configuration/entries/general.dm:518
[DmPath("/datum/config_entry/flag/preference_map_voting")]
public partial class datum_config_entry_flag_preference_map_voting : datum_config_entry_flag
{
}

/// Allows players to export their own preferences as a JSON file. Left as a config toggle in case it needs to be turned off due to server-specific needs.
// tg: code/controllers/configuration/entries/general.dm:521
[DmPath("/datum/config_entry/flag/forbid_preferences_export")]
public partial class datum_config_entry_flag_forbid_preferences_export : datum_config_entry_flag
{
    public datum_config_entry_flag_forbid_preferences_export()
    {
        @default = FALSE;
    }
}

/// The number of seconds a player must wait between preference export attempts.
// tg: code/controllers/configuration/entries/general.dm:525
[DmPath("/datum/config_entry/number/seconds_cooldown_for_preferences_export")]
public partial class datum_config_entry_number_seconds_cooldown_for_preferences_export : datum_config_entry_number
{
    public datum_config_entry_number_seconds_cooldown_for_preferences_export()
    {
        @default = 10;
        min_val = 1;
    }
}

// tg: code/controllers/configuration/entries/general.dm:529
[DmPath("/datum/config_entry/number/client_warn_version")]
public partial class datum_config_entry_number_client_warn_version : datum_config_entry_number
{
    public datum_config_entry_number_client_warn_version()
    {
        @default = null;
        min_val = 500;
    }
}

// tg: code/controllers/configuration/entries/general.dm:533
[DmPath("/datum/config_entry/number/client_warn_build")]
public partial class datum_config_entry_number_client_warn_build : datum_config_entry_number
{
    public datum_config_entry_number_client_warn_build()
    {
        @default = null;
        min_val = 0;
    }
}

// tg: code/controllers/configuration/entries/general.dm:537
[DmPath("/datum/config_entry/string/client_warn_message")]
public partial class datum_config_entry_string_client_warn_message : datum_config_entry_string
{
    public datum_config_entry_string_client_warn_message()
    {
        @default = "Your version of byond may have issues or be blocked from accessing this server in the future.";
    }
}

// tg: code/controllers/configuration/entries/general.dm:540
[DmPath("/datum/config_entry/flag/client_warn_popup")]
public partial class datum_config_entry_flag_client_warn_popup : datum_config_entry_flag
{
}

// tg: code/controllers/configuration/entries/general.dm:542
[DmPath("/datum/config_entry/number/client_error_version")]
public partial class datum_config_entry_number_client_error_version : datum_config_entry_number
{
    public datum_config_entry_number_client_error_version()
    {
        @default = null;
        min_val = 500;
    }
}

// tg: code/controllers/configuration/entries/general.dm:546
[DmPath("/datum/config_entry/string/client_error_message")]
public partial class datum_config_entry_string_client_error_message : datum_config_entry_string
{
    public datum_config_entry_string_client_error_message()
    {
        @default = "Your version of byond is too old, may have issues, and is blocked from accessing this server.";
    }
}

// tg: code/controllers/configuration/entries/general.dm:549
[DmPath("/datum/config_entry/number/client_error_build")]
public partial class datum_config_entry_number_client_error_build : datum_config_entry_number
{
    public datum_config_entry_number_client_error_build()
    {
        @default = null;
        min_val = 0;
    }
}

// tg: code/controllers/configuration/entries/general.dm:553
[DmPath("/datum/config_entry/number/minute_topic_limit")]
public partial class datum_config_entry_number_minute_topic_limit : datum_config_entry_number
{
    public datum_config_entry_number_minute_topic_limit()
    {
        @default = null;
        min_val = 0;
    }
}

// tg: code/controllers/configuration/entries/general.dm:557
[DmPath("/datum/config_entry/number/second_topic_limit")]
public partial class datum_config_entry_number_second_topic_limit : datum_config_entry_number
{
    public datum_config_entry_number_second_topic_limit()
    {
        @default = null;
        min_val = 0;
    }
}

// tg: code/controllers/configuration/entries/general.dm:561
[DmPath("/datum/config_entry/number/minute_click_limit")]
public partial class datum_config_entry_number_minute_click_limit : datum_config_entry_number
{
    public datum_config_entry_number_minute_click_limit()
    {
        @default = 400;
        min_val = 0;
    }
}

// tg: code/controllers/configuration/entries/general.dm:565
[DmPath("/datum/config_entry/number/second_click_limit")]
public partial class datum_config_entry_number_second_click_limit : datum_config_entry_number
{
    public datum_config_entry_number_second_click_limit()
    {
        @default = 15;
        min_val = 0;
    }
}

// tg: code/controllers/configuration/entries/general.dm:569
[DmPath("/datum/config_entry/number/error_cooldown")]
public partial class datum_config_entry_number_error_cooldown : datum_config_entry_number // The "cooldown" time for each occurrence of a unique error
{
    public datum_config_entry_number_error_cooldown()
    {
        @default = 600;
        integer = FALSE;
        min_val = 0;
    }
}

// tg: code/controllers/configuration/entries/general.dm:574
[DmPath("/datum/config_entry/number/error_limit")]
public partial class datum_config_entry_number_error_limit : datum_config_entry_number // How many occurrences before the next will silence them
{
    public datum_config_entry_number_error_limit()
    {
        @default = 50;
    }
}

// tg: code/controllers/configuration/entries/general.dm:577
[DmPath("/datum/config_entry/number/error_silence_time")]
public partial class datum_config_entry_number_error_silence_time : datum_config_entry_number // How long a unique error will be silenced for
{
    public datum_config_entry_number_error_silence_time()
    {
        @default = 6000;
        integer = FALSE;
    }
}

// tg: code/controllers/configuration/entries/general.dm:581
[DmPath("/datum/config_entry/number/error_msg_delay")]
public partial class datum_config_entry_number_error_msg_delay : datum_config_entry_number // How long to wait between messaging admins about occurrences of a unique error
{
    public datum_config_entry_number_error_msg_delay()
    {
        @default = 50;
        integer = FALSE;
    }
}

// tg: code/controllers/configuration/entries/general.dm:585
[DmPath("/datum/config_entry/flag/irc_announce_new_game")]
public partial class datum_config_entry_flag_irc_announce_new_game : datum_config_entry_flag
{
    public datum_config_entry_flag_irc_announce_new_game()
    {
        deprecated_by = typeof(datum_config_entry_str_list_channel_announce_new_game);
    }

    // tg: code/controllers/configuration/entries/general.dm:588
    public override object DeprecationUpdate(object value = null)
    {
        return ""; //default broadcast
    }
}

// tg: code/controllers/configuration/entries/general.dm:591
[DmPath("/datum/config_entry/string/chat_announce_new_game")]
public partial class datum_config_entry_string_chat_announce_new_game : datum_config_entry_string
{
    public datum_config_entry_string_chat_announce_new_game()
    {
        deprecated_by = typeof(datum_config_entry_str_list_channel_announce_new_game);
    }

    // tg: code/controllers/configuration/entries/general.dm:594
    public override object DeprecationUpdate(object value = null)
    {
        return ""; //default broadcast
    }
}

// tg: code/controllers/configuration/entries/general.dm:597
[DmPath("/datum/config_entry/str_list/channel_announce_new_game")]
public partial class datum_config_entry_str_list_channel_announce_new_game : datum_config_entry_str_list
{
}

// tg: code/controllers/configuration/entries/general.dm:599
[DmPath("/datum/config_entry/str_list/channel_announce_end_game")]
public partial class datum_config_entry_str_list_channel_announce_end_game : datum_config_entry_str_list
{
}

// tg: code/controllers/configuration/entries/general.dm:601
[DmPath("/datum/config_entry/str_list/chat_new_game_notifications")]
public partial class datum_config_entry_str_list_chat_new_game_notifications : datum_config_entry_str_list
{
}

/// validate ownership of admin flags for chat commands
// tg: code/controllers/configuration/entries/general.dm:604
[DmPath("/datum/config_entry/flag/secure_chat_commands")]
public partial class datum_config_entry_flag_secure_chat_commands : datum_config_entry_flag
{
    public datum_config_entry_flag_secure_chat_commands()
    {
        @default = FALSE;
    }
}

// tg: code/controllers/configuration/entries/general.dm:607
[DmPath("/datum/config_entry/flag/debug_admin_hrefs")]
public partial class datum_config_entry_flag_debug_admin_hrefs : datum_config_entry_flag
{
}

// tg: code/controllers/configuration/entries/general.dm:609
[DmPath("/datum/config_entry/number/mc_tick_rate/base_mc_tick_rate")]
public partial class datum_config_entry_number_mc_tick_rate_base_mc_tick_rate : datum_config_entry_number_mc_tick_rate
{
    public datum_config_entry_number_mc_tick_rate_base_mc_tick_rate()
    {
        integer = FALSE;
        @default = 1;
    }
}

// tg: code/controllers/configuration/entries/general.dm:613
[DmPath("/datum/config_entry/number/mc_tick_rate/high_pop_mc_tick_rate")]
public partial class datum_config_entry_number_mc_tick_rate_high_pop_mc_tick_rate : datum_config_entry_number_mc_tick_rate
{
    public datum_config_entry_number_mc_tick_rate_high_pop_mc_tick_rate()
    {
        integer = FALSE;
        @default = 1.1f;
    }
}

// tg: code/controllers/configuration/entries/general.dm:617
[DmPath("/datum/config_entry/number/mc_tick_rate/high_pop_mc_mode_amount")]
public partial class datum_config_entry_number_mc_tick_rate_high_pop_mc_mode_amount : datum_config_entry_number_mc_tick_rate
{
    public datum_config_entry_number_mc_tick_rate_high_pop_mc_mode_amount()
    {
        @default = 65;
    }
}

// tg: code/controllers/configuration/entries/general.dm:620
[DmPath("/datum/config_entry/number/mc_tick_rate/disable_high_pop_mc_mode_amount")]
public partial class datum_config_entry_number_mc_tick_rate_disable_high_pop_mc_mode_amount : datum_config_entry_number_mc_tick_rate
{
    public datum_config_entry_number_mc_tick_rate_disable_high_pop_mc_mode_amount()
    {
        @default = 60;
    }
}

// tg: code/controllers/configuration/entries/general.dm:623
[DmPath("/datum/config_entry/number/mc_tick_rate")]
public partial class datum_config_entry_number_mc_tick_rate : datum_config_entry_number
{
    public datum_config_entry_number_mc_tick_rate()
    {
        abstract_type = typeof(datum_config_entry_number_mc_tick_rate);
    }

    // tg: code/controllers/configuration/entries/general.dm:626
    public override async DmTask<object> ValidateAndSet(object str_val = null)
    {
        object dot = null;
        dot = await base.ValidateAndSet(str_val);
        if (Dm.truthy(dot))
            Master.UpdateTickRate();
        return dot;
    }
}

// tg: code/controllers/configuration/entries/general.dm:631
[DmPath("/datum/config_entry/flag/resume_after_initializations")]
public partial class datum_config_entry_flag_resume_after_initializations : datum_config_entry_flag
{
    // tg: code/controllers/configuration/entries/general.dm:633
    public override async DmTask<object> ValidateAndSet(object str_val = null)
    {
        object dot = null;
        dot = await base.ValidateAndSet(str_val);
        if (Dm.truthy(dot) && MC_RUNNING())
            world.sleep_offline = Dm.not(config_entry_value);
        return dot;
    }
}

// tg: code/controllers/configuration/entries/general.dm:638
[DmPath("/datum/config_entry/number/rounds_until_hard_restart")]
public partial class datum_config_entry_number_rounds_until_hard_restart : datum_config_entry_number
{
    public datum_config_entry_number_rounds_until_hard_restart()
    {
        @default = -1;
        min_val = 0;
    }
}

// tg: code/controllers/configuration/entries/general.dm:642
[DmPath("/datum/config_entry/flag/log_pictures")]
public partial class datum_config_entry_flag_log_pictures : datum_config_entry_flag
{
}

// tg: code/controllers/configuration/entries/general.dm:644
[DmPath("/datum/config_entry/flag/picture_logging_camera")]
public partial class datum_config_entry_flag_picture_logging_camera : datum_config_entry_flag
{
}

// tg: code/controllers/configuration/entries/general.dm:646
[DmPath("/datum/config_entry/number/roundstart_logout_report_time_average")]
public partial class datum_config_entry_number_roundstart_logout_report_time_average : datum_config_entry_number
{
    public datum_config_entry_number_roundstart_logout_report_time_average()
    {
        @default = 10 * MINUTES;
        min_val = 0;
    }
}

// tg: code/controllers/configuration/entries/general.dm:650
[DmPath("/datum/config_entry/number/roundstart_logout_report_time_variance")]
public partial class datum_config_entry_number_roundstart_logout_report_time_variance : datum_config_entry_number
{
    public datum_config_entry_number_roundstart_logout_report_time_variance()
    {
        @default = 3 * MINUTES;
        min_val = 0;
    }
}

// tg: code/controllers/configuration/entries/general.dm:654
[DmPath("/datum/config_entry/flag/reopen_roundstart_suicide_roles")]
public partial class datum_config_entry_flag_reopen_roundstart_suicide_roles : datum_config_entry_flag
{
}

// tg: code/controllers/configuration/entries/general.dm:656
[DmPath("/datum/config_entry/flag/reopen_roundstart_suicide_roles_command_positions")]
public partial class datum_config_entry_flag_reopen_roundstart_suicide_roles_command_positions : datum_config_entry_flag
{
}

// tg: code/controllers/configuration/entries/general.dm:658
[DmPath("/datum/config_entry/number/reopen_roundstart_suicide_roles_delay")]
public partial class datum_config_entry_number_reopen_roundstart_suicide_roles_delay : datum_config_entry_number
{
    public datum_config_entry_number_reopen_roundstart_suicide_roles_delay()
    {
        min_val = 30;
    }
}

// tg: code/controllers/configuration/entries/general.dm:661
[DmPath("/datum/config_entry/flag/reopen_roundstart_suicide_roles_command_report")]
public partial class datum_config_entry_flag_reopen_roundstart_suicide_roles_command_report : datum_config_entry_flag
{
}

// tg: code/controllers/configuration/entries/general.dm:663
[DmPath("/datum/config_entry/flag/auto_profile")]
public partial class datum_config_entry_flag_auto_profile : datum_config_entry_flag
{
}

// tg: code/controllers/configuration/entries/general.dm:665
[DmPath("/datum/config_entry/number/profiler_interval")]
public partial class datum_config_entry_number_profiler_interval : datum_config_entry_number
{
    public datum_config_entry_number_profiler_interval()
    {
        @default = 300 * SECONDS;
    }
}

// tg: code/controllers/configuration/entries/general.dm:668
[DmPath("/datum/config_entry/number/drift_dump_threshold")]
public partial class datum_config_entry_number_drift_dump_threshold : datum_config_entry_number
{
    public datum_config_entry_number_drift_dump_threshold()
    {
        @default = 4 * SECONDS;
    }
}

// tg: code/controllers/configuration/entries/general.dm:671
[DmPath("/datum/config_entry/number/drift_profile_delay")]
public partial class datum_config_entry_number_drift_profile_delay : datum_config_entry_number
{
    public datum_config_entry_number_drift_profile_delay()
    {
        @default = 15 * SECONDS;
    }
}

// tg: code/controllers/configuration/entries/general.dm:674
[DmPath("/datum/config_entry/string/centcom_ban_db")]
public partial class datum_config_entry_string_centcom_ban_db : datum_config_entry_string // URL for the CentCom Galactic Ban DB API
{
}

// tg: code/controllers/configuration/entries/general.dm:676
[DmPath("/datum/config_entry/string/centcom_source_whitelist")]
public partial class datum_config_entry_string_centcom_source_whitelist : datum_config_entry_string
{
}

/// URL for admins to be redirected to for 2FA
// tg: code/controllers/configuration/entries/general.dm:679
[DmPath("/datum/config_entry/string/admin_2fa_url")]
public partial class datum_config_entry_string_admin_2fa_url : datum_config_entry_string
{
}

// tg: code/controllers/configuration/entries/general.dm:681
[DmPath("/datum/config_entry/number/hard_deletes_overrun_threshold")]
public partial class datum_config_entry_number_hard_deletes_overrun_threshold : datum_config_entry_number
{
    public datum_config_entry_number_hard_deletes_overrun_threshold()
    {
        integer = FALSE;
        min_val = 0;
        @default = 0.5f;
    }
}

// tg: code/controllers/configuration/entries/general.dm:686
[DmPath("/datum/config_entry/number/hard_deletes_overrun_limit")]
public partial class datum_config_entry_number_hard_deletes_overrun_limit : datum_config_entry_number
{
    public datum_config_entry_number_hard_deletes_overrun_limit()
    {
        @default = 0;
        min_val = 0;
    }
}

// tg: code/controllers/configuration/entries/general.dm:690
[DmPath("/datum/config_entry/str_list/motd")]
public partial class datum_config_entry_str_list_motd : datum_config_entry_str_list
{
}

// tg: code/controllers/configuration/entries/general.dm:692
[DmPath("/datum/config_entry/number/urgent_ahelp_cooldown")]
public partial class datum_config_entry_number_urgent_ahelp_cooldown : datum_config_entry_number
{
    public datum_config_entry_number_urgent_ahelp_cooldown()
    {
        @default = 300;
    }
}

// tg: code/controllers/configuration/entries/general.dm:695
[DmPath("/datum/config_entry/string/urgent_ahelp_message")]
public partial class datum_config_entry_string_urgent_ahelp_message : datum_config_entry_string
{
    public datum_config_entry_string_urgent_ahelp_message()
    {
        @default = "This ahelp is urgent!";
    }
}

// tg: code/controllers/configuration/entries/general.dm:698
[DmPath("/datum/config_entry/string/ahelp_message")]
public partial class datum_config_entry_string_ahelp_message : datum_config_entry_string
{
    public datum_config_entry_string_ahelp_message()
    {
        @default = "";
    }
}

// tg: code/controllers/configuration/entries/general.dm:701
[DmPath("/datum/config_entry/string/urgent_ahelp_user_prompt")]
public partial class datum_config_entry_string_urgent_ahelp_user_prompt : datum_config_entry_string
{
    public datum_config_entry_string_urgent_ahelp_user_prompt()
    {
        @default = "There are no admins currently on. Do not press the button below if your ahelp is a joke, a request or a question. Use it only for cases of obvious grief.";
    }
}

// tg: code/controllers/configuration/entries/general.dm:704
[DmPath("/datum/config_entry/string/urgent_adminhelp_webhook_url")]
public partial class datum_config_entry_string_urgent_adminhelp_webhook_url : datum_config_entry_string
{
}

// tg: code/controllers/configuration/entries/general.dm:706
[DmPath("/datum/config_entry/string/regular_adminhelp_webhook_url")]
public partial class datum_config_entry_string_regular_adminhelp_webhook_url : datum_config_entry_string
{
}

// tg: code/controllers/configuration/entries/general.dm:708
[DmPath("/datum/config_entry/string/adminhelp_webhook_pfp")]
public partial class datum_config_entry_string_adminhelp_webhook_pfp : datum_config_entry_string
{
}

// tg: code/controllers/configuration/entries/general.dm:710
[DmPath("/datum/config_entry/string/adminhelp_webhook_name")]
public partial class datum_config_entry_string_adminhelp_webhook_name : datum_config_entry_string
{
}

// tg: code/controllers/configuration/entries/general.dm:712
[DmPath("/datum/config_entry/string/adminhelp_ahelp_link")]
public partial class datum_config_entry_string_adminhelp_ahelp_link : datum_config_entry_string
{
}

// tg: code/controllers/configuration/entries/general.dm:714
[DmPath("/datum/config_entry/flag/cache_assets")]
public partial class datum_config_entry_flag_cache_assets : datum_config_entry_flag
{
    public datum_config_entry_flag_cache_assets()
    {
        @default = TRUE;
    }
}

// tg: code/controllers/configuration/entries/general.dm:717
[DmPath("/datum/config_entry/flag/smart_cache_assets")]
public partial class datum_config_entry_flag_smart_cache_assets : datum_config_entry_flag
{
    public datum_config_entry_flag_smart_cache_assets()
    {
        @default = TRUE;
    }
}

// tg: code/controllers/configuration/entries/general.dm:720
[DmPath("/datum/config_entry/flag/save_spritesheets")]
public partial class datum_config_entry_flag_save_spritesheets : datum_config_entry_flag
{
    public datum_config_entry_flag_save_spritesheets()
    {
        @default = FALSE;
    }
}

// tg: code/controllers/configuration/entries/general.dm:723
[DmPath("/datum/config_entry/flag/station_name_in_hub_entry")]
public partial class datum_config_entry_flag_station_name_in_hub_entry : datum_config_entry_flag
{
    public datum_config_entry_flag_station_name_in_hub_entry()
    {
        @default = FALSE;
    }
}

// tg: code/controllers/configuration/entries/general.dm:726
[DmPath("/datum/config_entry/number/pr_announcements_per_round")]
public partial class datum_config_entry_number_pr_announcements_per_round : datum_config_entry_number
{
    public datum_config_entry_number_pr_announcements_per_round()
    {
        @default = 5;
        min_val = 0;
        integer = TRUE;
    }
}

// tg: code/controllers/configuration/entries/general.dm:731
[DmPath("/datum/config_entry/flag/forbid_all_profiling")]
public partial class datum_config_entry_flag_forbid_all_profiling : datum_config_entry_flag
{
}

// tg: code/controllers/configuration/entries/general.dm:733
[DmPath("/datum/config_entry/flag/forbid_admin_profiling")]
public partial class datum_config_entry_flag_forbid_admin_profiling : datum_config_entry_flag
{
}

// tg: code/controllers/configuration/entries/general.dm:736
[DmPath("/datum/config_entry/flag/morgue_cadaver_disable_nonhumans")]
public partial class datum_config_entry_flag_morgue_cadaver_disable_nonhumans : datum_config_entry_flag
{
    public datum_config_entry_flag_morgue_cadaver_disable_nonhumans()
    {
        @default = FALSE;
    }
}

// tg: code/controllers/configuration/entries/general.dm:739
[DmPath("/datum/config_entry/number/morgue_cadaver_other_species_probability")]
public partial class datum_config_entry_number_morgue_cadaver_other_species_probability : datum_config_entry_number
{
    public datum_config_entry_number_morgue_cadaver_other_species_probability()
    {
        @default = 50;
    }
}

// tg: code/controllers/configuration/entries/general.dm:742
[DmPath("/datum/config_entry/string/morgue_cadaver_override_species")]
public partial class datum_config_entry_string_morgue_cadaver_override_species : datum_config_entry_string
{
}

// tg: code/controllers/configuration/entries/general.dm:744
[DmPath("/datum/config_entry/flag/toast_notification_on_init")]
public partial class datum_config_entry_flag_toast_notification_on_init : datum_config_entry_flag
{
}

// tg: code/controllers/configuration/entries/general.dm:746
[DmPath("/datum/config_entry/flag/config_errors_runtime")]
public partial class datum_config_entry_flag_config_errors_runtime : datum_config_entry_flag
{
    public datum_config_entry_flag_config_errors_runtime()
    {
        @default = FALSE;
    }
}

// tg: code/controllers/configuration/entries/general.dm:749
[DmPath("/datum/config_entry/number/upload_limit")]
public partial class datum_config_entry_number_upload_limit : datum_config_entry_number
{
    public datum_config_entry_number_upload_limit()
    {
        @default = 524288;
        min_val = 0;
    }
}

// tg: code/controllers/configuration/entries/general.dm:753
[DmPath("/datum/config_entry/number/upload_limit_admin")]
public partial class datum_config_entry_number_upload_limit_admin : datum_config_entry_number
{
    public datum_config_entry_number_upload_limit_admin()
    {
        @default = 5242880;
        min_val = 0;
    }
}

/// The minimum number of tallies a map vote entry can have.
// tg: code/controllers/configuration/entries/general.dm:758
[DmPath("/datum/config_entry/number/map_vote_minimum_tallies")]
public partial class datum_config_entry_number_map_vote_minimum_tallies : datum_config_entry_number
{
    public datum_config_entry_number_map_vote_minimum_tallies()
    {
        @default = 1;
        min_val = 0;
        max_val = 50;
    }
}

/// The flat amount all maps get by default
// tg: code/controllers/configuration/entries/general.dm:764
[DmPath("/datum/config_entry/number/map_vote_flat_bonus")]
public partial class datum_config_entry_number_map_vote_flat_bonus : datum_config_entry_number
{
    public datum_config_entry_number_map_vote_flat_bonus()
    {
        @default = 5;
        min_val = 0;
        max_val = INFINITY;
    }
}

/// The maximum number of tallies a map vote entry can have.
// tg: code/controllers/configuration/entries/general.dm:770
[DmPath("/datum/config_entry/number/map_vote_maximum_tallies")]
public partial class datum_config_entry_number_map_vote_maximum_tallies : datum_config_entry_number
{
    public datum_config_entry_number_map_vote_maximum_tallies()
    {
        @default = 200;
        min_val = 0;
        max_val = INFINITY;
    }
}

/// The number of tallies that are carried over between rounds.
// tg: code/controllers/configuration/entries/general.dm:776
[DmPath("/datum/config_entry/number/map_vote_tally_carryover_percentage")]
public partial class datum_config_entry_number_map_vote_tally_carryover_percentage : datum_config_entry_number
{
    public datum_config_entry_number_map_vote_tally_carryover_percentage()
    {
        @default = 100;
        min_val = 0;
        max_val = 100;
    }
}

/// Pop requirement to exclude recently played maps from votes.
// tg: code/controllers/configuration/entries/general.dm:782
[DmPath("/datum/config_entry/number/map_vote_minimum_pop_to_remember_maps")]
public partial class datum_config_entry_number_map_vote_minimum_pop_to_remember_maps : datum_config_entry_number
{
    public datum_config_entry_number_map_vote_minimum_pop_to_remember_maps()
    {
        @default = 0; //0 means disabled
        integer = TRUE;
        min_val = 0;
    }
}

/// If admins with +DEBUG can initialize byond-tracy midround.
// tg: code/controllers/configuration/entries/general.dm:788
[DmPath("/datum/config_entry/flag/allow_tracy_start")]
public partial class datum_config_entry_flag_allow_tracy_start : datum_config_entry_flag
{
    public datum_config_entry_flag_allow_tracy_start()
    {
        protection = CONFIG_ENTRY_LOCKED;
    }
}

/// If admins with +DEBUG can queue byond-tracy to run the next round.
// tg: code/controllers/configuration/entries/general.dm:792
[DmPath("/datum/config_entry/flag/allow_tracy_queue")]
public partial class datum_config_entry_flag_allow_tracy_queue : datum_config_entry_flag
{
    public datum_config_entry_flag_allow_tracy_queue()
    {
        protection = CONFIG_ENTRY_LOCKED;
    }
}

/**
 * Tgui ui_act payloads larger than 2kb are split into chunks a maximum of 1kb in size.
 * This flag represents the maximum chunk count the server is willing to receive.
 */
// tg: code/controllers/configuration/entries/general.dm:799
[DmPath("/datum/config_entry/number/tgui_max_chunk_count")]
public partial class datum_config_entry_number_tgui_max_chunk_count : datum_config_entry_number
{
    public datum_config_entry_number_tgui_max_chunk_count()
    {
        @default = 32;
    }
}

// If set, enables the "Link forum account" OOC verb
// tg: code/controllers/configuration/entries/general.dm:803
[DmPath("/datum/config_entry/string/forum_link_uri")]
public partial class datum_config_entry_string_forum_link_uri : datum_config_entry_string
{
}

// tg: code/controllers/configuration/entries/general.dm:805
[DmPath("/datum/config_entry/flag/generate_assets_in_init")]
public partial class datum_config_entry_flag_generate_assets_in_init : datum_config_entry_flag
{
    public datum_config_entry_flag_generate_assets_in_init()
    {
        @default = FALSE;
    }
}

// tg: code/controllers/configuration/entries/general.dm:808
[DmPath("/datum/config_entry/flag/fishing")]
public partial class datum_config_entry_flag_fishing : datum_config_entry_flag
{
}

// tg: code/controllers/configuration/entries/general.dm:810
[DmPath("/datum/config_entry/string/policy_json_path")]
public partial class datum_config_entry_string_policy_json_path : datum_config_entry_string
{
    public datum_config_entry_string_policy_json_path()
    {
        @default = "policy.json";
    }
}

// tg: code/controllers/configuration/entries/general.dm:813
[DmPath("/datum/config_entry/number_list/ignored_cids")]
public partial class datum_config_entry_number_list_ignored_cids : datum_config_entry_number_list
{
    // an object-creating default (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        @default = Dm.list();
    }
}
