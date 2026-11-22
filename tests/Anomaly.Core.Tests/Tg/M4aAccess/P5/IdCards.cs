using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M4aAccess.P5;

/// <summary>
/// M4a P5: tg's access comes from an ID card held or worn (COMSIG_MOB_RETRIEVE_ACCESS, cards_ids.dm:182-193), so the tests
/// that set a mob's access (M3's dev access list, DV23, retired) put a card with those accesses in its hand. A retro
/// /obj/item/card/id has no trim, so it needs no SSid_access: it fits the toy rooms. Call inside the world.
/// </summary>
public static class IdCards
{
    /// <summary>
    /// A new retro ID card with <paramref name="accesses"/> (tg's access values) in <paramref name="who"/>'s hands, in place
    /// of any card the hands held (so a second call sets the access, as M3's dev command did).
    /// </summary>
    public static obj_item_card_id Hold(mob_living who, params string[] accesses)
    {
        foreach (object held in Dm.each(who.held_items.Copy()))
        {
            if (held is obj_item_card_id old)
                qdel(old);
        }
        var card = Dm.@new<obj_item_card_id>(get_turf(who));
        var list = Dm.list();
        foreach (string access in accesses)
            Dm.combine(ref list, access);
        card.access = list;
        Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_hands(card))), "the card reaches a hand");
        return card;
    }

    /// <summary>A card with every access of the game (tg's REGION_ACCESS_ALL_GLOBAL, the debug card's) in the hands.</summary>
    public static obj_item_card_id HoldAll(mob_living who)
    {
        var all = new List<string>();
        foreach (object access in Dm.each(GlobalProcs.REGION_ACCESS_ALL_GLOBAL()))
            all.Add((string)access);
        return Hold(who, all.ToArray());
    }
}
