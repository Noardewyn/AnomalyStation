// Translated from tgstation (snapshot 1d99bd7f): code/modules/logging/categories/log_category_game.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/logging/categories/log_category_game.dm:1
[DmPath("/datum/log_category/game")]
public partial class datum_log_category_game : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_game.dm:1
    public datum_log_category_game()
    {
        category = LOG_CATEGORY_GAME;
        config_flag = typeof(datum_config_entry_flag_log_game);
    }
}

// tg: code/modules/logging/categories/log_category_game.dm:5
[DmPath("/datum/log_category/game_vote")]
public partial class datum_log_category_game_vote : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_game.dm:5
    public datum_log_category_game_vote()
    {
        category = LOG_CATEGORY_GAME_VOTE;
        config_flag = typeof(datum_config_entry_flag_log_vote);
        master_category = typeof(datum_log_category_game);
    }
}

// tg: code/modules/logging/categories/log_category_game.dm:10
[DmPath("/datum/log_category/game_emote")]
public partial class datum_log_category_game_emote : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_game.dm:10
    public datum_log_category_game_emote()
    {
        category = LOG_CATEGORY_GAME_EMOTE;
        config_flag = typeof(datum_config_entry_flag_log_emote);
        master_category = typeof(datum_log_category_game);
    }
}

// tg: code/modules/logging/categories/log_category_game.dm:15
[DmPath("/datum/log_category/game_ghost_polls")]
public partial class datum_log_category_game_ghost_polls : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_game.dm:15
    public datum_log_category_game_ghost_polls()
    {
        category = LOG_CATEGORY_GAME_GHOST_POLLS;
        config_flag = typeof(datum_config_entry_flag_log_ghost_poll);
        master_category = typeof(datum_log_category_game);
    }
}

// tg: code/modules/logging/categories/log_category_game.dm:20
[DmPath("/datum/log_category/game_internet_request")]
public partial class datum_log_category_game_internet_request : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_game.dm:20
    public datum_log_category_game_internet_request()
    {
        category = LOG_CATEGORY_GAME_INTERNET_REQUEST;
        config_flag = typeof(datum_config_entry_flag_log_internet_request);
        master_category = typeof(datum_log_category_game);
    }
}

// tg: code/modules/logging/categories/log_category_game.dm:25
[DmPath("/datum/log_category/game_radio_emote")]
public partial class datum_log_category_game_radio_emote : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_game.dm:25
    public datum_log_category_game_radio_emote()
    {
        category = LOG_CATEGORY_GAME_RADIO_EMOTE;
        config_flag = typeof(datum_config_entry_flag_log_emote);
        master_category = typeof(datum_log_category_game);
    }
}

// tg: code/modules/logging/categories/log_category_game.dm:30
[DmPath("/datum/log_category/game_traitor")]
public partial class datum_log_category_game_traitor : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_game.dm:30
    public datum_log_category_game_traitor()
    {
        category = LOG_CATEGORY_GAME_TRAITOR;
        config_flag = typeof(datum_config_entry_flag_log_traitor);
        master_category = typeof(datum_log_category_game);
    }
}

// tg: code/modules/logging/categories/log_category_game.dm:35
[DmPath("/datum/log_category/game_topic")]
public partial class datum_log_category_game_topic : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_game.dm:35
    public datum_log_category_game_topic()
    {
        category = LOG_CATEGORY_GAME_TOPIC;
        config_flag = typeof(datum_config_entry_flag_log_world_topic);
        master_category = typeof(datum_log_category_game);
    }
}

// tg: code/modules/logging/categories/log_category_game.dm:40
[DmPath("/datum/log_category/game_say")]
public partial class datum_log_category_game_say : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_game.dm:40
    public datum_log_category_game_say()
    {
        category = LOG_CATEGORY_GAME_SAY;
        config_flag = typeof(datum_config_entry_flag_log_say);
        master_category = typeof(datum_log_category_game);
    }
}

// tg: code/modules/logging/categories/log_category_game.dm:45
[DmPath("/datum/log_category/game_whisper")]
public partial class datum_log_category_game_whisper : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_game.dm:45
    public datum_log_category_game_whisper()
    {
        category = LOG_CATEGORY_GAME_WHISPER;
        config_flag = typeof(datum_config_entry_flag_log_whisper);
        master_category = typeof(datum_log_category_game);
    }
}

// tg: code/modules/logging/categories/log_category_game.dm:50
[DmPath("/datum/log_category/game_ooc")]
public partial class datum_log_category_game_ooc : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_game.dm:50
    public datum_log_category_game_ooc()
    {
        category = LOG_CATEGORY_GAME_OOC;
        config_flag = typeof(datum_config_entry_flag_log_ooc);
        master_category = typeof(datum_log_category_game);
    }
}

// tg: code/modules/logging/categories/log_category_game.dm:55
[DmPath("/datum/log_category/game_prayer")]
public partial class datum_log_category_game_prayer : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_game.dm:55
    public datum_log_category_game_prayer()
    {
        category = LOG_CATEGORY_GAME_PRAYER;
        config_flag = typeof(datum_config_entry_flag_log_prayer);
        master_category = typeof(datum_log_category_game);
    }
}

// tg: code/modules/logging/categories/log_category_game.dm:60
[DmPath("/datum/log_category/game_access")]
public partial class datum_log_category_game_access : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_game.dm:60
    public datum_log_category_game_access()
    {
        category = LOG_CATEGORY_GAME_ACCESS;
        config_flag = typeof(datum_config_entry_flag_log_access);
        master_category = typeof(datum_log_category_game);
    }
}

// tg: code/modules/logging/categories/log_category_game.dm:65
[DmPath("/datum/log_category/game_blood_worm")]
public partial class datum_log_category_game_blood_worm : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_game.dm:65
    public datum_log_category_game_blood_worm()
    {
        category = LOG_CATEGORY_GAME_BLOOD_WORM;
        config_flag = typeof(datum_config_entry_flag_log_blood_worm);
        master_category = typeof(datum_log_category_game);
    }
}

// tg: code/modules/logging/categories/log_category_game.dm:70
[DmPath("/datum/log_category/minimap_drawing")]
public partial class datum_log_category_minimap_drawing : datum_log_category
{
    // tg: code/modules/logging/categories/log_category_game.dm:70
    public datum_log_category_minimap_drawing()
    {
        category = LOG_CATEGORY_GAME_MINIMAP_DRAWING;
        master_category = typeof(datum_log_category_game);
    }
}
