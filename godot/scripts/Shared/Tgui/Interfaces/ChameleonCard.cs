using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ChameleonCard.jsx (syndicate G1b, D316): the agent
// card's access theft (cards_ids.dm:1806): the stolen card's accesses not in the agent card's trim, to copy with its wildcards.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ChameleonCard
{
    // tg: ChameleonCard.jsx:5
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        static List<string> Refs(IReadOnlyList<UiValue> list) => list.Select(v => v.Text ?? v.Number.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();

        IReadOnlyList<UiValue> accesses = data.Arr("accesses");
        List<string> selectedList = Refs(data.Arr("selectedList"));
        List<string> trimAccess = Refs(data.Arr("trimAccess"));
        List<string> ourAccess = Refs(data.Arr("ourAccess"));
        List<string> theftAccess = Refs(data.Arr("theftAccess"));
        List<string> ourTrimAccess = Refs(data.Arr("ourTrimAccess"));
        bool showBasic = data.Truthy("showBasic");

        // tg: :22  the regions, without the accesses of our trim, with any access we hold or the stolen card holds
        var parsedAccess = new List<UiValue>();
        foreach (UiValue region in accesses)
        {
            var regionAccess = region.Arr("accesses").Where(access =>
            {
                string reference = access.Str("ref");
                // Snip everything that's part of our trim.
                if (ourTrimAccess.Contains(reference))
                    return false;
                // Add anything not part of our trim that's an access (assumed wildcard)
                // Also add any access on the ID card we're stealing from.
                return ourAccess.Contains(reference) || theftAccess.Contains(reference);
            }).ToList();
            if (regionAccess.Count > 0)
                parsedAccess.Add(UiValue.ObjectOf(("name", UiValue.Of(region.Str("name"))), ("accesses", UiValue.ListOf(regionAccess))));
        }

        // tg: :47  <Window width={500} height={620}>
        return c.Window(500, 620, null,
            AccessList.Render(c, parsedAccess, data.Get("wildcardSlots"), selectedList,
                (reference, wildcard) => c.Act("mod_access", ("access_target", reference), ("access_wildcard", wildcard)),
                trimAccess, data.Get("accessFlags"), data.Get("accessFlagNames"), data.Get("wildcardFlags"), null, showBasic));
    }
}
