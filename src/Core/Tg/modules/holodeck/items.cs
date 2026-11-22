// Translated from tgstation (snapshot 1d99bd7f): code/modules/holodeck/items.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The holographic deck of cards only (syndicate G3d, D316: the cards); the holodeck and its other items are not ported.

// tg: code/modules/holodeck/items.dm:36
public partial class obj_item_toy_cards_deck_syndicate_holographic : obj_item_toy_cards_deck_syndicate
{
    // tg: code/modules/holodeck/items.dm:38
    [DmParams("mapload", "holodeck")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        holodeck = Dm.arg(__extra, 0) as obj_machinery_computer_holodeck;
        RegisterSignal(this, COMSIG_QDELETING, nameof(handle_card_delete));
        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/holodeck/items.dm:43
    public virtual void handle_card_delete(datum source = null)
    {
        //if any REAL cards have been inserted into the deck they are moved outside before destroying it
        foreach (obj_item_toy_singlecard card in Dm.each<obj_item_toy_singlecard>(card_atoms))
        {
            if (Dm.truthy(Dm.bitand(Dm.num(card.flags_1), HOLOGRAM_1)))
                continue;
            card_atoms.Remove(card);
            Waiting.NoSleep(card.forceMove(drop_location() as atom));
        }
    }
}
