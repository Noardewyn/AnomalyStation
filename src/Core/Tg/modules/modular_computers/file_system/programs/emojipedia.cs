// Translated from tgstation (snapshot 1d99bd7f): code/modules/modular_computers/file_system/programs/emojipedia.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps S4c, D327): EmojiPedia, the list of the emoji a PDA message can send, with its NtosEmojipedia window (the
// client draws each from tg's emoji icon, icons/ui/chat/emoji.dmi). The var block is generated (D111).

/// A tablet app that lets anyone see all the valid emoji they can send via a PDA message (and even OOC!)
// tg: code/modules/modular_computers/file_system/programs/emojipedia.dm:2
public partial class datum_computer_file_program_emojipedia : datum_computer_file_program
{
    /// Store the list of potential emojis here.
    // tg: code/modules/modular_computers/file_system/programs/emojipedia.dm:12  var/static/list/emoji_list = icon_states(icon(EMOJI_SET))
    // (the icon's states come from the committed icon state table, as icon_states_fast; EMOJI_SET is 'icons/ui/chat/emoji.dmi', __DEFINES/fonts.dm:16)
    private static DmList emoji_list => TgState.Current.datum_computer_file_program_emojipedia__emoji_list ??= icon_states_fast(Dm.resource("icons/ui/chat/emoji.dmi"));

    // tg: code/modules/modular_computers/file_system/programs/emojipedia.dm:14
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        DmList data = Dm.list();
        foreach (object emoji in Dm.each(emoji_list))
            Dm.append_at(data, "emoji_list", Dm.list(Dm.list(Dm.kv("name", emoji))));

        return data;
    }

    // tg: code/modules/modular_computers/file_system/programs/emojipedia.dm:23
    public override async DmTask<object> ui_assets(mob user = null)
    {
        // list(get_asset_datum(/datum/asset/spritesheet_batched/emojipedia)): the asset transport is the client's (DV44); the client
        // draws each emoji from tg's icon
        return Dm.list();
    }
}

public sealed partial class TgState
{
    // tg: code/modules/modular_computers/file_system/programs/emojipedia.dm:12  the program's var/static/list/emoji_list (rules 10.6)
    internal DmList datum_computer_file_program_emojipedia__emoji_list;
}
