// Translated from tgstation (snapshot 1d99bd7f): code/datums/storage/subtypes/others/cards.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the trading card pile's storage, which holds only trading cards and shuffles the pile after someone looked
// through it. The var block is generated (D111).

/**
 *A storage component to be used on card piles, for use as hands/decks/discard piles. Don't use on something that's not a card pile!
 */
// tg: code/datums/storage/subtypes/others/cards.dm:4
public partial class datum_storage_tcg : datum_storage
{
    // tg: code/datums/storage/subtypes/others/cards.dm:9
    [DmParams("parent", "max_slots", "max_specific_storage", "max_total_storage")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_holdable(typeof(obj_item_tcgcard));
    }

    // tg: code/datums/storage/subtypes/others/cards.dm:18
    public override async DmTask<object> show_contents(mob to_show = null)
    {
        // sometimes, show contents is called when the mob is already seeing the contents of the deck, to refresh the view.
        // to avoid spam, we only show the message if they weren't already seeing the contents.
        bool was_already_seeing = to_show.active_storage == this;
        object dot = await base.show_contents(to_show);
        if (!Dm.truthy(dot))
            return dot;
        if (!was_already_seeing)
        {
            to_show.visible_message(
                span_notice(I18n.T("[] starts to look through the contents of []!", to_show, parent)),
                span_notice(I18n.T("You begin looking into the contents of [].", parent)));
        }
        return dot;
    }

    // tg: code/datums/storage/subtypes/others/cards.dm:32
    public override object hide_contents(mob to_hide = null)
    {
        // see above
        bool was_actually_seeing = to_hide.active_storage == this;
        object dot = base.hide_contents(to_hide);
        if (!Dm.truthy(dot))
            return dot;
        if (QDELING(this))
            return dot;
        if (was_actually_seeing)
        {
            real_location.visible_message(span_notice(I18n.T("[] is shuffled after looking through it.", parent)));
            real_location.contents = shuffle(real_location.contents) as DmList;
        }
        return dot;
    }
}
