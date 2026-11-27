using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Every newly live recreation type made and deleted in a toy room: the arcade cabinets, the slot machines and their board, the
/// roulette wheel and its beacon, the holodeck console, its effects and holographic items, the trading cards, packs, decks, binder,
/// card holders, buttons and panel. Each is live, initializes, is deleted cleanly, and leaves no runtime but the slot machine's listed
/// fantasy leaf of its mythril coin (VarBlockLeaves).
/// </summary>
public class RecreationSweepTests
{
    private static readonly Type[] Roots =
    {
        typeof(obj_machinery_computer_arcade), typeof(obj_machinery_computer_slot_machine), typeof(obj_item_circuitboard_computer_slot_machine),
        typeof(obj_machinery_roulette), typeof(obj_item_roulette_wheel_beacon), typeof(obj_machinery_computer_holodeck),
        typeof(obj_effect_holodeck_effect), typeof(obj_item_melee_energy_sword_holographic), typeof(obj_item_toy_dodgeball),
        typeof(obj_machinery_readybutton), typeof(obj_item_orion_ship), typeof(obj_item_tcgcard), typeof(obj_item_tcgcard_deck),
        typeof(obj_item_cardpack), typeof(obj_item_storage_card_binder), typeof(obj_machinery_trading_card_holder),
        typeof(obj_machinery_trading_card_button), typeof(obj_effect_trading_card_panel),
    };

    [Fact]
    public void Every_recreation_type_is_live_and_is_made_and_deleted_cleanly()
    {
        var r = new AudioRoom(13, 11, seed: 14350);
        r.In(() => (get_area(locate(6, 6, 1)) as area).requires_power = FALSE);
        var types = r.In(() => Roots.SelectMany(root => Dm.each_cast<Type>(typesof(root)))
            .Where(t => !Dm.eq(Dm.initial(t, (datum p) => p.abstract_type), t)).Distinct().ToList());
        Assert.True(types.Count >= 50, "the recreation types: " + types.Count); // 51 at the snapshot
        var made = new List<atom>();
        foreach (Type type in types)
        {
            atom thing = r.In(() => (atom)Dm.@new(type, locate(6, 6, 1)));
            Assert.False(InertSeam.IsInert(thing), type.Name + " is live");
            made.Add(thing);
            r.Step(2);
        }
        r.Step(Second);
        r.In(() =>
        {
            foreach (atom thing in made)
                if (!QDELETED(thing))
                    qdel(thing);
        });
        r.Step(2 * Second);
        Assert.All(made, thing => Assert.True(QDELETED(thing), thing.GetType().Name + " was deleted"));
        Assert.Empty(Anomaly.Core.Tests.Tools.Types.VarBlockLeaves.Unexpected(typeof(obj_item_coin_mythril), r.Room.RuntimeText.ToList()));
    }
}
