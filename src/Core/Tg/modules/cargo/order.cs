// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/order.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P4b, D327; DV237 ended): the shipping manifest with its errors (a wrong station name, missing listed contents,
// missing shipped items) and its stamps, the requisition form, and the supply order with its cost (the coupon's discount, the private
// purchase's tenth more), its crate, the grouped goody crate and the material orders. The manifest declares order_id as object (a
// goody crate's manifest lists its orders' numbers as text, rules 3.2), so its var block is hand-written; the others are generated.

// tg: code/modules/cargo/order.dm:12
public partial class obj_item_paper_fluff_jobs_cargo_manifest : obj_item_paper_fluff_jobs_cargo
{
    // tg: code/modules/cargo/order.dm:14
    public float order_cost = 0;
    // tg: code/modules/cargo/order.dm:15  object (rules 3.2): an order's number, or a goody crate's "#1 #2 " text
    public object order_id = 0f;
    // tg: code/modules/cargo/order.dm:16
    public float errors = 0;

    // tg: code/modules/cargo/order.dm:13
    public obj_item_paper_fluff_jobs_cargo_manifest()
    {
        can_become_message_in_bottle = FALSE;
    }

    /// The chance for a manifest or crate to be created with errors
    // tg: code/modules/cargo/order.dm:2  (file-local, #undef at :229)
    internal const int MANIFEST_ERROR_CHANCE = 5;
    /// Determines if the station name will be incorrect on the manifest
    // tg: code/modules/cargo/order.dm:6  (#undef at :230)
    internal const int MANIFEST_ERROR_NAME = 1 << 0;
    /// Determines if contents will be deleted from the manifest but still be present in the crate
    // tg: code/modules/cargo/order.dm:8  (#undef at :231)
    internal const int MANIFEST_ERROR_CONTENTS = 1 << 1;
    /// Determines if contents will be deleted from the crate but still be present in the manifest
    // tg: code/modules/cargo/order.dm:10  (#undef at :232)
    internal const int MANIFEST_ERROR_ITEM = 1 << 2;

    // tg: code/modules/cargo/order.dm:21
    [DmParams("mapload", "id", "cost", "manifest_can_fail")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object id = Dm.arg(__extra, 0);
        object cost = Dm.arg(__extra, 1);
        object manifest_can_fail = Dm.arg(__extra, 2) ?? TRUE;
        object dot = base.Initialize(mapload);
        order_id = id;
        order_cost = Dm.num(cost);
        if (!Dm.truthy(manifest_can_fail))
            return dot;

        if (prob(MANIFEST_ERROR_CHANCE) && (world.time - Dm.num(SSticker.round_start_time) > STATION_RENAME_TIME_LIMIT)) //Too confusing if station name gets changed
        {
            errors = Dm.bitor(errors, MANIFEST_ERROR_NAME);
            investigate_log(Dm.text("Supply order #[] generated a manifest with an incorrect station name.", order_id), INVESTIGATE_CARGO);
        }
        if (prob(MANIFEST_ERROR_CHANCE))
        {
            errors = Dm.bitor(errors, MANIFEST_ERROR_CONTENTS);
            investigate_log(Dm.text("Supply order #[] generated a manifest missing listed contents.", order_id), INVESTIGATE_CARGO);
        }
        else if (prob(MANIFEST_ERROR_CHANCE)) //Content and item errors could remove the same items, so only one at a time
        {
            errors = Dm.bitor(errors, MANIFEST_ERROR_ITEM);
            investigate_log(Dm.text("Supply order #[] generated with incorrect contents shipped.", order_id), INVESTIGATE_CARGO);
        }
        return dot;
    }

    // tg: code/modules/cargo/order.dm:38
    public virtual bool is_approved()
    {
        return LAZYLEN(stamp_cache) > 0 && !is_denied();
    }

    // tg: code/modules/cargo/order.dm:41
    public virtual bool is_denied()
    {
        return LAZYLEN(stamp_cache) > 0 && Dm.@in("stamp-deny", stamp_cache);
    }
}

// tg: code/modules/cargo/order.dm:44
public partial class datum_supply_order : datum
{
    // tg: code/modules/cargo/order.dm:64
    [DmParams("pack", "orderer", "orderer_rank", "orderer_ckey", "reason", "paying_account", "department_destination", "coupon", "charge_on_purchase", "manifest_can_fail", "cost_type", "can_be_cancelled")]
    public override async DmTask New(params object[] __extra)
    {
        id = SSshuttle.order_number++;
        cost_type = Dm.arg(__extra, 10) ?? MONEY_SYMBOL;
        pack = Dm.arg<datum_supply_pack>(__extra, 0);
        orderer = Dm.arg(__extra, 1);
        orderer_rank = Dm.arg(__extra, 2);
        orderer_ckey = Dm.arg(__extra, 3);
        reason = Dm.arg(__extra, 4);
        paying_account = Dm.arg(__extra, 5) as datum_bank_account;
        department_destination = Dm.arg(__extra, 6);
        applied_coupon = Dm.arg(__extra, 7) as obj_item_coupon;
        charge_on_purchase = Dm.num(Dm.arg(__extra, 8) ?? TRUE);
        manifest_can_fail = Dm.num(Dm.arg(__extra, 9) ?? TRUE);
        can_be_cancelled = Dm.num(Dm.arg(__extra, 11) ?? TRUE);
    }

    // tg: code/modules/cargo/order.dm:92
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref applied_coupon);
        return base.Destroy(force);
    }

    //returns the total cost of this order. Its not the total price paid by cargo but the total value of this order
    // tg: code/modules/cargo/order.dm:97
    public virtual float get_final_cost()
    {
        float cost = pack.get_cost();
        if (applied_coupon != null) //apply discount price
            cost *= 1 - Dm.num(applied_coupon.discount_pct_off);
        if (Dm.truthy(paying_account?.add_to_accounts) && !Dm.truthy(Dm.bitand(pack.order_flags, ORDER_GOODY))) //privately purchased and not a goody means 1.1x the cost
            cost *= 1.1f;
        return round(cost);
    }

    // tg: code/modules/cargo/order.dm:105
    public virtual obj_item_paper_requisition generateRequisition(turf T = null)
    {
        var requisition_paper = Dm.@new<obj_item_paper_requisition>(T);

        requisition_paper.name = I18n.T("requisition form - #[] ([])", id, pack.name);
        string requisition_text = I18n.T("<h2>[] Supply Requisition</h2>", station_name());
        requisition_text += "<hr/>";
        requisition_text += I18n.T("Order #[]<br/>", id);
        requisition_text += I18n.T("Time of Order: []<br/>", UNDERLINED_HTML_TEXT(Dm.text("[]", server_timestamp(ic_time: TRUE)), I18n.T("Shift Time: []", round_timestamp())));
        requisition_text += I18n.T("Item: []<br/>", pack.name);
        requisition_text += I18n.T("Access Restrictions: []<br/>", SSid_access.get_access_desc(pack.access));
        requisition_text += I18n.T("Requested by: []<br/>", orderer);
        if (paying_account != null)
            requisition_text += I18n.T("Paid by: []<br/>", paying_account.account_holder);
        requisition_text += I18n.T("Rank: []<br/>", orderer_rank);
        requisition_text += I18n.T("Comment: []<br/>", reason);

        requisition_paper.add_raw_text(requisition_text, advanced_html: TRUE);
        requisition_paper.update_appearance();
        return requisition_paper;
    }

    //generates-the-manifests.
    // tg: code/modules/cargo/order.dm:125
    public virtual obj_item_paper_fluff_jobs_cargo_manifest generateManifest(obj container = null, object owner = null, object packname = null, object cost = null)
    {
        var manifest_paper = Dm.@new<obj_item_paper_fluff_jobs_cargo_manifest>(null, id, cost, manifest_can_fail);

        object station_name = Dm.truthy(Dm.bitand(manifest_paper.errors, obj_item_paper_fluff_jobs_cargo_manifest.MANIFEST_ERROR_NAME)) ? new_station_name() : GlobalProcs.station_name();

        manifest_paper.name = I18n.T("shipping manifest - []", Dm.truthy(packname) ? Dm.text("#[] ([])", id, pack.name) : I18n.T("(Grouped Item Crate)"));

        string manifest_text = I18n.T("<h2>[] Shipping Manifest</h2>", command_name());
        manifest_text += "<hr/>";
        if (Dm.truthy(owner) && !Dm.eq(owner, "Cargo"))
        {
            manifest_text += I18n.T("Direct purchase from []<br/>", owner);
            manifest_paper.name += I18n.T(" - Purchased by []", owner);
        }
        manifest_text += I18n.T("Order[]: []<br/>", Dm.truthy(packname) ? "" : "s", id);
        manifest_text += I18n.T("Destination: []<br/>", station_name);
        if (Dm.truthy(packname))
            manifest_text += I18n.T("Item: []<br/>", packname);
        manifest_text += I18n.T("Contents: <br/>");
        manifest_text += "<ul>";
        DmList container_contents = Dm.list(); // Associative list with the format (item_name = nº of occurrences, ...)
        foreach (atom_movable stuff in Dm.each_cast<atom_movable>(Dm.sub(container.contents, manifest_paper) as DmList))
        {
            if (stuff is obj_item_stack thing)
            {
                container_contents[thing.singular_name] = Dm.add(container_contents[thing.singular_name], thing.amount);
                continue;
            }
            container_contents[stuff.name] = Dm.add(container_contents[stuff.name], 1);
        }
        if (Dm.truthy(Dm.bitand(manifest_paper.errors, obj_item_paper_fluff_jobs_cargo_manifest.MANIFEST_ERROR_CONTENTS)) && container_contents != null)
        {
            if (HAS_TRAIT(container, TRAIT_NO_MANIFEST_CONTENTS_ERROR))
                manifest_paper.errors = Dm.bitand(manifest_paper.errors, Dm.bitnot(obj_item_paper_fluff_jobs_cargo_manifest.MANIFEST_ERROR_CONTENTS));
            else
            {
                for (int iteration = 1; iteration <= rand(1, round(container.contents.len * 0.5f)); iteration++) // Remove anywhere from one to half of the items
                {
                    object missing_item = pick(container_contents);
                    container_contents[missing_item] = Dm.sub(container_contents[missing_item], 1);
                    if (!Dm.truthy(container_contents[missing_item])) // To avoid 0s and negative values on the manifest
                        container_contents.Remove(missing_item);
                }
            }
        }

        foreach (object item in Dm.each(container_contents))
            manifest_text += Dm.text("<li> [] [][]</li>", container_contents[item], item, Dm.eq(container_contents[item], 1) ? "" : plural_s(item));
        manifest_text += "</ul>";
        manifest_text += I18n.T("<h4>Stamp below to confirm receipt of goods:</h4>");

        manifest_paper.add_raw_text(manifest_text);

        if (Dm.truthy(Dm.bitand(manifest_paper.errors, obj_item_paper_fluff_jobs_cargo_manifest.MANIFEST_ERROR_ITEM)))
        {
            if (HAS_TRAIT(container, TRAIT_NO_MISSING_ITEM_ERROR))
                manifest_paper.errors = Dm.bitand(manifest_paper.errors, Dm.bitnot(obj_item_paper_fluff_jobs_cargo_manifest.MANIFEST_ERROR_ITEM));
            else
            {
                float lost = max(round(container.contents.len / 10f), 1);
                while (--lost >= 0)
                    qdel(pick(container.contents));
            }
        }

        manifest_paper.update_appearance();
        Waiting.NoSleep(manifest_paper.forceMove(container));

        if (container is obj_structure_closet_crate crate)
        {
            crate.manifest = WEAKREF(manifest_paper) as datum_weakref;
            crate.update_appearance();
        }

        return manifest_paper;
    }

    // tg: code/modules/cargo/order.dm:185
    public virtual async DmTask<object> generate(atom A = null)
    {
        object account_holder;
        if (paying_account != null)
            account_holder = paying_account.account_holder;
        else
            account_holder = "Cargo";
        var crate = await pack.generate(A, paying_account, Dm.initial(pack, (datum_supply_pack p) => p.storage_override)) as obj;
        if (crate == null)
            return null; // the empty pack makes no crate
        if (Dm.truthy(Dm.bitand(pack.order_flags, ORDER_CONTRABAND)))
        {
            foreach (atom_movable item_within in Dm.each<atom_movable>(crate.get_all_contents()))
                ADD_TRAIT(item_within, TRAIT_CONTRABAND, INNATE_TRAIT);
        }
        if (Dm.truthy(department_destination))
            await crate.AddElement(typeof(datum_element_deliver_first), department_destination, pack.cost);
        generateManifest(crate, account_holder, pack, pack.cost);
        return crate;
    }

    // tg: code/modules/cargo/order.dm:200
    public virtual void generateCombo(object miscbox = null, object misc_own = null, object misc_contents = null, object misc_cost = null)
    {
        foreach (object I in Dm.each(misc_contents))
            Dm.@new(I as Type, miscbox);
        generateManifest(miscbox as obj, misc_own, "", misc_cost);
    }

    // tg: code/modules/cargo/order.dm:206
    public virtual void append_order(DmList new_contents = null, object cost_increase = null)
    {
        foreach (object i in Dm.each(new_contents))
        {
            if (Dm.truthy(pack.contains[i]))
                pack.contains[i] = Dm.add(pack.contains[i], new_contents[i]);
            else
            {
                Dm.append(ref pack.contains, i);
                pack.contains[i] = new_contents[i];
            }
        }
        pack.cost += Dm.num(cost_increase);
    }
}

/// Custom type of order who's supply pack can be safely deleted
// tg: code/modules/cargo/order.dm:216
public partial class datum_supply_order_disposable : datum_supply_order
{
    // tg: code/modules/cargo/order.dm:216
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref pack);
        return base.Destroy(force);
    }
}

/// Custom material order to append cargo crate value to the final order cost
// tg: code/modules/cargo/order.dm:221
public partial class datum_supply_order_disposable_materials : datum_supply_order_disposable
{
    // tg: code/modules/cargo/order.dm:221
    public override float get_final_cost()
    {
        return base.get_final_cost() + CARGO_CRATE_VALUE;
    }

    /// Custom material order to append cargo crate value to the final manifest cost
    // tg: code/modules/cargo/order.dm:225
    public override obj_item_paper_fluff_jobs_cargo_manifest generateManifest(obj container = null, object owner = null, object packname = null, object cost = null)
    {
        cost = Dm.add(cost, CARGO_CRATE_VALUE);
        return base.generateManifest(container, owner, packname, cost);
    }
}
