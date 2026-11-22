// Translated from tgstation (snapshot 1d99bd7f): code/modules/cards/deck/blank.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G3d, D316).

// tg: code/modules/cards/deck/blank.dm:1
public partial class obj_item_toy_cards_deck_blank : obj_item_toy_cards_deck
{
    // tg: code/modules/cards/deck/blank.dm:15
    public override void initialize_cards()
    {
        for (int _ = 1; _ <= decksize; _++)
            initial_cards.Add(typeof(datum_deck_card_blank));
    }
}

// tg: code/modules/cards/deck/blank.dm:19
public partial class datum_deck_card_blank : datum_deck_card
{
    // tg: code/modules/cards/deck/blank.dm:22
    public override void update_card(obj_item_toy_singlecard card = null)
    {
        card.blank = TRUE;
    }
}
