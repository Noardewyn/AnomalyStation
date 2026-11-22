// Translated from tgstation (snapshot 1d99bd7f): code/modules/emoji/emoji_parse.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file's entry (M9 slice E: a PDA message's emojis): emoji_parse and emoji_sanitize behind the emojis config flag, which the
// host does not set (tg's config.txt default is off unless EMOJIS is written), so a message's text passes through. Left out as
// marked leaves: the parse and the sanitize themselves (the chat's emoji sprite sheet, icon_tag of
// /datum/asset/spritesheet_batched/chat, and icon_states of EMOJI_SET: the chat icons).

public static partial class GlobalProcs
{
    // tg: code/modules/emoji/emoji_parse.dm:1
    public static object emoji_parse(object text = null) //turns :ai: into an emoji in text.
    {
        if (!Dm.truthy(text))
            return text;
        object dot = text;
        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_emojis))))
            return dot;
        Dm.NotTranslated("code/modules/emoji/emoji_parse.dm:7", "emoji_parse: the chat's emoji sprite sheet (icon_tag of the chat spritesheet, the chat icons)");
        return dot;
    }

    // tg: code/modules/emoji/emoji_parse.dm:35
    public static object emoji_sanitize(object text = null) //cuts any text that would not be parsed as an emoji
    {
        object dot = text;
        if (!Dm.truthy(config.Get(typeof(datum_config_entry_flag_emojis))))
            return dot;
        Dm.NotTranslated("code/modules/emoji/emoji_parse.dm:39", "emoji_sanitize: the emoji set's icon states (EMOJI_SET, the chat icons)");
        return dot;
    }
}
