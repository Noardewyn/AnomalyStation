// Translated from tgstation (snapshot 1d99bd7f): code/modules/uplink/uplink_purchase_log.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A2 (D316): the whole file. The purchase entry's picture (icon2base64html, a base64 image the server renders) is a
// deferred leaf with the chat icons (not-translated.md section 131); the report shows the entries' names and costs. The var blocks
// are generated.

public partial class datum_controller_global_vars
{
    // tg: code/modules/uplink/uplink_purchase_log.dm:1  GLOBAL_LIST(uplink_purchase_logs_by_key) //assoc key = /datum/uplink_purchase_log
    public DmList uplink_purchase_logs_by_key;

    // tg: code/modules/uplink/uplink_purchase_log.dm:1  (GLOBAL_UNMANAGED(uplink_purchase_logs_by_key), __DEFINES/__globals.dm:11)
    public virtual void InitGlobaluplink_purchase_logs_by_key()
    {
        return;
    }
}

// tg: code/modules/uplink/uplink_purchase_log.dm:3
public partial class datum_uplink_purchase_log : datum
{
    // tg: code/modules/uplink/uplink_purchase_log.dm:8
    [DmParams("_owner", "_parent")]
    public override async DmTask New(params object[] __extra)
    {
        object _owner = Dm.arg(__extra, 0);
        datum_component_uplink _parent = Dm.arg(__extra, 1) as datum_component_uplink;
        owner = _owner;
        LAZYINITLIST(ref GLOB.uplink_purchase_logs_by_key);
        if (Dm.truthy(owner))
        {
            if (Dm.truthy(GLOB.uplink_purchase_logs_by_key[owner]))
            {
                stack_trace(Dm.text("WARNING: DUPLICATE PURCHASE LOGS DETECTED. [] [] []", _owner, _parent, _parent?.type), "code/modules/uplink/uplink_purchase_log.dm", 13);
                MergeWithAndDel(GLOB.uplink_purchase_logs_by_key[owner] as datum_uplink_purchase_log);
            }
            GLOB.uplink_purchase_logs_by_key[owner] = this;
        }
        purchase_log = Dm.list();
    }

    // tg: code/modules/uplink/uplink_purchase_log.dm:18
    public override object Destroy(float? force = null)
    {
        purchase_log = null;
        if (GLOB.uplink_purchase_logs_by_key[owner] == this)
            Dm.remove(GLOB.uplink_purchase_logs_by_key, owner);
        return base.Destroy(force);
    }

    // tg: code/modules/uplink/uplink_purchase_log.dm:24
    public virtual object MergeWithAndDel(datum_uplink_purchase_log other = null)
    {
        if (other == null)
            return null;
        object dot = Dm.num(Dm.eq(owner, other.owner));
        if (!Dm.truthy(dot))
            return dot;
        foreach (object hash in Dm.each(other.purchase_log))
        {
            if (!Dm.truthy(purchase_log[hash]))
                purchase_log[hash] = other.purchase_log[hash];
            else
            {
                datum_uplink_purchase_entry UPE = purchase_log[hash] as datum_uplink_purchase_entry;
                datum_uplink_purchase_entry UPE_O = other.purchase_log[hash] as datum_uplink_purchase_entry;
                UPE.amount_purchased += UPE_O.amount_purchased;
            }
        }
        qdel(other);
        return dot;
    }

    // tg: code/modules/uplink/uplink_purchase_log.dm:40
    public virtual object TotalTelecrystalsSpent()
    {
        return total_spent;
    }

    // tg: code/modules/uplink/uplink_purchase_log.dm:43  #define FORMAT_COST(uplink_entry) (file-local, #undef at :52)
    private static object FORMAT_COST(datum_uplink_purchase_entry uplink_entry) =>
        Dm.truthy(uplink_entry.spent_cost) ? Dm.text("[] TC", uplink_entry.spent_cost) : (Dm.truthy(uplink_entry.base_cost) ? "Free" : "Surplus");

    // tg: code/modules/uplink/uplink_purchase_log.dm:45
    public virtual object generate_render(float? show_key = null)
    {
        show_key ??= TRUE;
        string dot = "";
        foreach (object hash in Dm.each(purchase_log))
        {
            datum_uplink_purchase_entry UPE = purchase_log[hash] as datum_uplink_purchase_entry;
            dot += Dm.text("<span class='tooltip_container'>\\[[][]<span class='tooltip_hover'><b>[]</b><br>[]<br>[]</span>[]\\]</span>",
                UPE.icon_b64, Dm.truthy(show_key) ? Dm.text("([])", owner) : "", UPE.name, FORMAT_COST(UPE), UPE.desc,
                UPE.amount_purchased > 1 ? Dm.text("x[]", UPE.amount_purchased) : "");
        }
        return dot;
    }

    /// Logs that an uplink item was purchased
    // tg: code/modules/uplink/uplink_purchase_log.dm:55
    public virtual void log_purchase(atom spawned_item = null, datum_uplink_item uplink_item = null, object spent_cost = null)
    {
        object hash = hash_purchase(uplink_item, spent_cost);
        datum_uplink_purchase_entry purchase_entry = purchase_log[hash] as datum_uplink_purchase_entry;
        if (purchase_entry == null)
        {
            purchase_entry = Dm.@new<datum_uplink_purchase_entry>();
            purchase_entry.set_item(spawned_item as obj_item);
            purchase_entry.name = uplink_item.name;
            purchase_entry.desc = uplink_item.desc;
            purchase_entry.base_cost = Dm.initial(uplink_item, (datum_uplink_item p) => p.cost);
            purchase_entry.spent_cost = spent_cost;
            purchase_log[hash] = purchase_entry;
        }

        purchase_entry.amount_purchased += 1;
        total_spent += Dm.num(spent_cost);
    }

    /// Used to add custom items into the purchase log
    // tg: code/modules/uplink/uplink_purchase_log.dm:71
    public virtual void log_purchase_custom(datum_uplink_purchase_entry custom_entry = null)
    {
        purchase_log[REF(custom_entry)] = custom_entry;
        total_spent += Dm.num(custom_entry.spent_cost);
    }

    // tg: code/modules/uplink/uplink_purchase_log.dm:75
    public virtual object hash_purchase(datum_uplink_item uplink_item = null, object spent_cost = null)
    {
        return Dm.text("[]|[]|[]|[]", uplink_item.type, uplink_item.name, uplink_item.cost, spent_cost);
    }
}

// tg: code/modules/uplink/uplink_purchase_log.dm:77
public partial class datum_uplink_purchase_entry : datum
{
    // tg: code/modules/uplink/uplink_purchase_log.dm:86
    public virtual void set_item(obj_item to_set = null)
    {
        path = to_set.type;
        // icon_b64 = "[icon2base64html(to_set)]": the item's picture rendered to a base64 image (__HELPERS/icons.dm, not declared); the
        // server renders no pictures (D104) and the 3D client's chat is text: the chat icons
        Dm.Deferred("code/modules/uplink/uplink_purchase_log.dm:88", "set_item: icon2base64html, the purchase's picture in the round-end report (the chat icons)");
        icon_b64 = "";
    }
}
