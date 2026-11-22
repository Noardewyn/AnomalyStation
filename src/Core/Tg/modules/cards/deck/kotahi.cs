// Translated from tgstation (snapshot 1d99bd7f): code/modules/cards/deck/kotahi.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G3d, D316).

// tg: code/modules/cards/deck/kotahi.dm:1
public partial class obj_item_toy_cards_deck_kotahi : obj_item_toy_cards_deck
{
    // tg: code/modules/cards/deck/kotahi.dm:8
    public override void initialize_cards()
    {
        foreach (string colour in new[] { "Red", "Yellow", "Green", "Blue" })
        {
            initial_cards.Add(Dm.text("[] 0", colour)); //kotahi decks have only one colour of each 0, weird huh?
            for (int k = 0; k <= 1; k++) //two of each colour of number
            {
                initial_cards.Add(Dm.text("[] skip", colour));
                initial_cards.Add(Dm.text("[] reverse", colour));
                initial_cards.Add(Dm.text("[] draw 2", colour));
                for (int i = 1; i <= 9; i++)
                    initial_cards.Add(Dm.text("[] []", colour, i));
            }
        }
        for (int k = 0; k <= 3; k++) //4 wilds and draw 4s
        {
            initial_cards.Add("Wildcard");
            initial_cards.Add("Draw 4");
        }
    }
}
