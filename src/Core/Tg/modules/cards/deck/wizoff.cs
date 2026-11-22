// Translated from tgstation (snapshot 1d99bd7f): code/modules/cards/deck/wizoff.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G3d, D316).

//It's Wiz-Off, the wizard themed card game! It's modular too, in case you might want to make it Syndie, Sec and Clown themed or something stupid like that.
// tg: code/modules/cards/deck/wizoff.dm:2
public partial class obj_item_toy_cards_deck_wizoff : obj_item_toy_cards_deck
{
    // tg: code/modules/cards/deck/wizoff.dm:9
    public override void initialize_cards()
    {
        object card_list = strings("wizoff.json", "wizard");
        initial_cards.Add(Dm.@new<datum_deck_card_of_type>(typeof(obj_item_toy_singlecard_wizoff_ruleset))); // ruleset should be the top card
        foreach (object card in Dm.each(card_list))
            initial_cards.Add(card);
    }
}

// tg: code/modules/cards/deck/wizoff.dm:15
public partial class obj_item_toy_singlecard_wizoff_ruleset : obj_item_toy_singlecard
{
    // tg: code/modules/cards/deck/wizoff.dm:22
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        dot.Add(span_notice(I18n.T("Remember the rules of Wiz-Off!")));
        dot.Add(span_info(I18n.T("Each player draws 5 cards.")));
        dot.Add(span_info(I18n.T("There are five rounds. Each round, a player selects a card to play, and the winner is selected based on the following rules:")));
        dot.Add(span_info(I18n.T("Defensive beats Offensive!")));
        dot.Add(span_info(I18n.T("Offensive beats Utility!")));
        dot.Add(span_info(I18n.T("Utility beats Defensive!")));
        dot.Add(span_info(I18n.T("If both players play the same type of spell, the higher number wins!")));
        dot.Add(span_info(I18n.T("The player who wins the most of the 5 rounds wins the game!")));
        dot.Add(span_notice(I18n.T("Now get ready to battle for the fate of the universe: Wiz-Off!")));
        return dot;
    }
}
