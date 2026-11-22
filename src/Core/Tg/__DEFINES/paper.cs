// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/paper.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The function macro of paper.dm (gaps S3, D298); its constants are generated.

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/paper.dm:19  #define IS_WRITING_UTENSIL(thing) (thing?.get_writing_implement_details()?["interaction_mode"] == MODE_WRITING)
    // The slot is coloured only through the stamp's get_asset_datum (colored-slots.tsv); no definition sleeps, so the macro's
    // synchronous users (screentips) call it as tg does.
    public static bool IS_WRITING_UTENSIL(obj_item thing)
        => thing != null && Dm.eq((Waiting.NoSleep(thing.get_writing_implement_details()) as DmList)?["interaction_mode"], MODE_WRITING);

    ///Use in conjunction with `advanced_html` to have underlined text that, when hovered over, will let you see additional text.
    // tg: code/__DEFINES/paper.dm:49  #define UNDERLINED_HTML_TEXT(text_displayed, revealed_text) "<span title='[revealed_text]' style='border-bottom: 1px dotted;'>[text_displayed]</span>"
    public static string UNDERLINED_HTML_TEXT(object text_displayed, object revealed_text)
        => Dm.text("<span title='[]' style='border-bottom: 1px dotted;'>[]</span>", revealed_text, text_displayed);
}
