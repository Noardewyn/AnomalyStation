// Translated from tgstation (snapshot 1d99bd7f): code/datums/storage/subtypes/others/bag_of_holding.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SR1 (D333): the file whole: the bag of holding's storage and the rift of a bag of holding put into another (the alert, the
// gibbed user, the reality tear). Deferred leaf: the achievement. The var block is generated (D111).

// tg: code/datums/storage/subtypes/others/bag_of_holding.dm:1
public partial class datum_storage_bag_of_holding : datum_storage
{
    // tg: code/datums/storage/subtypes/others/bag_of_holding.dm:7
    public override object attempt_insert(obj_item to_insert = null, mob user = null, float? @override = null, float? force = null, float? messages = null)
    {
        // tg passes /obj/item/mod/control and /obj/item/mod/module/storage as typecacheof's only_root_path and ignore_root_path: both
        // are true, so the cache holds the bag of holding's own path only (and the loop below finds no modsuit)
        DmList matching = typecache_filter_list(to_insert.get_all_contents() as DmList, typecacheof(typeof(obj_item_storage_backpack_holding), TRUE, TRUE) as DmList) as DmList;
        Dm.remove(ref matching, parent);
        Dm.remove(ref matching, real_location);
        foreach (obj_item_mod modsuit_or_module in Dm.each<obj_item_mod>(matching.Copy()))
        {
            datum_storage storage = modsuit_or_module.atom_storage;
            if (storage is datum_storage_bag_of_holding && storage.real_location == storage.parent)
                continue;
            Dm.remove(ref matching, modsuit_or_module);
        }

        if ((to_insert.atom_storage is datum_storage_bag_of_holding || Dm.truthy(Dm.length(matching))) && Dm.truthy(can_insert(to_insert, user)))
        {
            Waiting.Detach(recursive_insertion(to_insert, user as mob_living));
            return FALSE;
        }

        return base.attempt_insert(to_insert, user, @override, force, messages);
    }

    // tg: code/datums/storage/subtypes/others/bag_of_holding.dm:23
    public virtual async DmTask recursive_insertion(obj_item to_insert = null, mob_living user = null)
    {
        if (Dm.truthy(await confirm_recursive_insertion(to_insert, user)))
            await create_rift(to_insert, user);
    }

    // tg: code/datums/storage/subtypes/others/bag_of_holding.dm:27
    public virtual async DmTask<object> confirm_recursive_insertion(obj_item to_insert = null, mob_living user = null)
    {
        area bag_area = get_area(user) as area;
        object safety = await tgui_alert(user, I18n.T("Doing this will have extremely dire consequences for the station and its crew. Be sure you know what you're doing."), I18n.T("Put in []?", to_insert.name), Dm.list(I18n.T("Proceed"), I18n.T("Abort")));
        return Dm.eq(safety, I18n.T("Proceed"))
            && !QDELETED(to_insert)
            && !QDELETED(parent)
            && !QDELETED(real_location)
            && !QDELETED(user)
            && Dm.truthy(await user.can_perform_action(parent, NEED_DEXTERITY))
            && Dm.truthy(can_insert(to_insert, user))
            && !Dm.truthy(Dm.bitand(bag_area.area_flags, NO_BOH)) ? TRUE : FALSE;
    }

    // tg: code/datums/storage/subtypes/others/bag_of_holding.dm:39
    public virtual async DmTask create_rift(obj_item inserted = null, mob_living user = null)
    {
        turf rift_loc = get_turf(parent);
        user.visible_message(
            span_userdanger(I18n.T("The Bluespace interfaces of the two devices catastrophically malfunction!")),
            span_danger(I18n.T("The Bluespace interfaces of the two devices catastrophically malfunction!")));

        message_admins(Dm.text("[] detonated a bag of holding at [].", ADMIN_LOOKUPFLW(user), ADMIN_VERBOSEJMP(rift_loc)));
        user.log_message(Dm.text("detonated a bag of holding at [].", loc_name(rift_loc)), LOG_ATTACK, color: "red");

        Dm.Deferred("code/datums/storage/subtypes/others/bag_of_holding.dm:48", "create_rift: user.client?.give_award(/datum/award/achievement/jobs/bag_of_holding, user): the achievements (D332)");
        user.investigate_log("has been gibbed by a bag of holding recursive insertion.", INVESTIGATE_DEATHS);
        await user.gib();
        obj_reality_tear tear = Dm.@new<obj_reality_tear>(rift_loc);
        await tear.start_disaster();
        qdel(inserted);
        qdel(parent);
    }
}
