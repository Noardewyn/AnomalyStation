// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/netpod/outfitting.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the outfits the NetpodOutfits window lists and the selection's check.

// tg: code/modules/bitrunning/netpod/outfitting.dm:1
public partial class obj_machinery_netpod : obj_machinery
{
    /// Creates a list of outfit entries for the UI.
    // tg: code/modules/bitrunning/netpod/outfitting.dm:2
    public virtual object make_outfit_collection(object identifier = null, DmList outfit_list = null)
    {
        DmList collection = Dm.list(
            Dm.kv("name", identifier),
            Dm.kv("outfits", Dm.list()));

        foreach (Type outfit in Dm.each<Type>(outfit_list))
        {
            string outfit_name = Dm.initial(outfit, (datum_outfit p) => p.name);
            if (findtext(outfit_name, "(") != 0 || findtext(outfit_name, "-") != 0) // No special variants please
                continue;

            DmList outfits = collection["outfits"] as DmList;
            Dm.append(ref outfits, Dm.list(Dm.list(Dm.kv("path", outfit), Dm.kv("name", outfit_name))));
            collection["outfits"] = outfits;
        }

        return Dm.list(collection);
    }

    /// Resolves a path to an outfit.
    // tg: code/modules/bitrunning/netpod/outfitting.dm:19
    public virtual object resolve_outfit(object text = null)
    {
        Type path = text2path(text as string);
        if (!ispath(path, typeof(datum_outfit)))
            return null;

        foreach (object wardrobe in Dm.each(cached_outfits))
        {
            foreach (object outfit in Dm.each((wardrobe as DmList)["outfits"]))
            {
                if (Dm.eq(path, (outfit as DmList)["path"]))
                    return path;
            }
        }

        message_admins(Dm.text("[]:[] attempted to select an unavailable outfit from a netpod", usr, usr?.ckey));
        return null;
    }
}
