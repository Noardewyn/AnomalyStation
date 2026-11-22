// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/markets/market_item.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4d (D298): the whole file, the market item and the purchase. Both declare item as object (the entry's typepath, or a one of a
// kind instance, rules 3.2), so their var blocks are hand-written. A one of a kind item's picture (tg's html_icon,
// icon2base64(getFlatIcon(item))) is not made: the server renders no icons (D104), so the uplink shows its name alone.

// tg: code/modules/cargo/markets/market_item.dm:1
public partial class datum_market_item : datum
{
    /// Name for the item entry used in the uplink.
    // tg: code/modules/cargo/markets/market_item.dm:3
    public object name;
    /// Description for the item entry used in the uplink.
    // tg: code/modules/cargo/markets/market_item.dm:5
    public object desc;
    /// The category this item belongs to, should be already declared in the market that this item is accessible in.
    // tg: code/modules/cargo/markets/market_item.dm:7
    public object category;
    /// "/datum/market"s that this item should be in, used by SSmarket on init.
    // tg: code/modules/cargo/markets/market_item.dm:9
    public DmList markets;
    /// Price for the item, if not set creates a price according to the *_min and *_max vars.
    // tg: code/modules/cargo/markets/market_item.dm:12
    public object price;
    /// How many of this type of item is available, if not set creates a price according to the *_min and *_max vars.
    // tg: code/modules/cargo/markets/market_item.dm:14
    public object stock;
    /// Path to or the item itself what this entry is for, this should be set even if you override spawn_item to spawn your item.
    // tg: code/modules/cargo/markets/market_item.dm:17  object (rules 3.2): a typepath or the item itself
    public object item;
    /// Used to exclude abstract/special paths from the unit test if the value matches the type itself.
    // tg: code/modules/cargo/markets/market_item.dm:20
    public Type abstract_path;
    /// Minimum price for the item if generated randomly.
    // tg: code/modules/cargo/markets/market_item.dm:23
    public float price_min = 0;
    /// Maximum price for the item if generated randomly.
    // tg: code/modules/cargo/markets/market_item.dm:25
    public float price_max = 0;
    /// Minimum amount that there should be of this item in the market if generated randomly. This defaults to 1 as most items will have it as 1.
    // tg: code/modules/cargo/markets/market_item.dm:27
    public float stock_min = 1;
    /// Maximum amount that there should be of this item in the market if generated randomly.
    // tg: code/modules/cargo/markets/market_item.dm:29
    public float stock_max = 0;
    /// Probability for this item to be available. Used by SSmarket on init.
    // tg: code/modules/cargo/markets/market_item.dm:31
    public object availability_prob;
    /// If set, this icon will be shown in the UI.
    // tg: code/modules/cargo/markets/market_item.dm:34
    public object html_icon;
    ///The identifier for the market item, generated on runtime and used to access them in the market categories.
    // tg: code/modules/cargo/markets/market_item.dm:37
    public object identifier;
    ///If set, these will override the shipment methods set by the market
    // tg: code/modules/cargo/markets/market_item.dm:40
    public DmList shipping_override;
    /// Can this item be restocked
    // tg: code/modules/cargo/markets/market_item.dm:43
    public float restockable = TRUE;

    // tg: code/modules/cargo/markets/market_item.dm:9
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        markets = Dm.list(typeof(datum_market_blackmarket));
    }

    // tg: code/modules/cargo/markets/market_item.dm:45
    public override async DmTask New(params object[] __extra)
    {
        if (price == null)
            price = rand(price_min, price_max);
        if (stock == null)
            stock = rand(stock_min, stock_max);
        identifier = Dm.text("[]", type);
    }

    ///For 'dynamic' market items generated on runtime, this proc is to be used to properly sets the item, especially if it's a hardref.
    // tg: code/modules/cargo/markets/market_item.dm:53
    public virtual void set_item(object path_or_ref = null)
    {
        //we're replacing the item to sell, and the old item is an instance!
        if (ismovable(item))
        {
            UnregisterSignal((datum)item, COMSIG_QDELETING);
            html_icon = null;
        }
        item = path_or_ref;
        identifier = Dm.text("[]", path_or_ref);
        if (ismovable(path_or_ref))
        {
            // html_icon = icon2base64(getFlatIcon(item, no_anim=TRUE)): the server renders no icons (D104)
            RegisterSignal((datum)item, COMSIG_QDELETING, nameof(on_item_del));
            identifier = Dm.text("[]", REF(this));
        }
    }

    // tg: code/modules/cargo/markets/market_item.dm:65
    public override object Destroy(float? force = null)
    {
        item = null;
        return base.Destroy(force);
    }

    // tg: code/modules/cargo/markets/market_item.dm:69
    public virtual void on_item_del(datum source = null)
    {
        qdel(this);
    }

    /// Used for spawning the wanted item, override if you need to do something special with the item.
    // tg: code/modules/cargo/markets/market_item.dm:74
    public virtual async DmTask<object> spawn_item(object loc = null, datum_market_purchase purchase = null)
    {
        SEND_SIGNAL(COMSIG_MARKET_ITEM_SPAWNED, Dm.list(this, purchase.uplink, purchase.method, loc));
        if (item is atom_movable return_item)
        {
            UnregisterSignal(return_item, COMSIG_QDELETING);
            return_item.visible_message(span_notice(I18n.T("[] vanishes...", return_item)));
            do_sparks(8, FALSE, return_item);
            if (loc == null)
                await return_item.moveToNullspace();
            else
                await return_item.forceMove(loc as atom);
            item = null;
            return return_item;
        }
        if (ispath(item))
            return Dm.@new((Type)item, loc);
        throw Dm.CRASH(Dm.text("Invalid item type for market item []", item ?? "null"), "code/modules/cargo/markets/market_item.dm", 91);
    }

    /**
     * Buys the item and makes SSmarket handle it.
     *
     * @param uplink The uplink that is buying the item.
     * @param buyer The mob that is buying the item.
     * @param shipping_method The shipping method used to get the market item onto the station.
     * @param legal_status The legal status of the market. Determines if the item to be spawned is contraband.
     */
    // tg: code/modules/cargo/markets/market_item.dm:100
    public virtual async DmTask<object> buy(obj_item_market_uplink uplink = null, mob buyer = null, object shipping_method = null, object legal_status = null)
    {
        // Sanity
        if (uplink == null || buyer == null)
            return FALSE;

        // This shouldn't be able to happen unless there was some manipulation or admin fuckery.
        if (item == null || Dm.num(stock) <= 0)
            return FALSE;

        // Alright, the item has been purchased.
        var purchase = Dm.@new<datum_market_purchase>(this, uplink, shipping_method, legal_status);

        // SSmarket takes care of the shipping.
        if (Dm.truthy(SSmarket.queue_item(purchase)))
        {
            stock = Dm.num(stock) - 1;
            buyer.log_message(Dm.text("has successfully purchased [] using [] for shipping.", name, shipping_method), LOG_ECON);
            return TRUE;
        }
        return FALSE;
    }
}

// This exists because it is easier to keep track of all the vars this way.
// tg: code/modules/cargo/markets/market_item.dm:122
public partial class datum_market_purchase : datum
{
    /// The entry being purchased.
    // tg: code/modules/cargo/markets/market_item.dm:124
    public datum_market_item entry;
    /// Instance of the item being sent, used by the market telepad
    // tg: code/modules/cargo/markets/market_item.dm:126
    public atom_movable item;
    /// The uplink where this purchase was done from.
    // tg: code/modules/cargo/markets/market_item.dm:128
    public obj_item_market_uplink uplink;
    /// Shipping method used to buy this item.
    // tg: code/modules/cargo/markets/market_item.dm:130
    public object method;
    /// Is this item considered contraband? If illegal, applies the contraband trait to the item when spawned.
    // tg: code/modules/cargo/markets/market_item.dm:132
    public object legallity;

    // tg: code/modules/cargo/markets/market_item.dm:134
    [DmParams("entry", "uplink", "method", "legal_status")]
    public override async DmTask New(params object[] __extra)
    {
        var entry = Dm.arg(__extra, 0) as datum_market_item;
        var uplink = Dm.arg(__extra, 1) as obj_item_market_uplink;
        object method = Dm.arg(__extra, 2);
        object legal_status = Dm.arg(__extra, 3);
        if (uplink == null || entry == null || !Dm.truthy(method))
            throw Dm.CRASH(Dm.text("[] created with a false value arg: (entry: [] - uplink: [] - method: [])", type, entry, uplink, method), "code/modules/cargo/markets/market_item.dm", 136);
        this.entry = entry;
        this.uplink = uplink;
        this.method = method;
        this.legallity = legal_status;
        RegisterSignal(entry, COMSIG_QDELETING, nameof(on_instance_del));
        RegisterSignal(uplink, COMSIG_QDELETING, nameof(on_instance_del));
        if (entry.item is atom_movable movable)
        {
            item = movable;
            RegisterSignal(movable, COMSIG_QDELETING, nameof(on_instance_del));
        }
    }

    // tg: code/modules/cargo/markets/market_item.dm:147
    public override object Destroy(float? force = null)
    {
        entry = null;
        uplink = null;
        Dm.remove(ref SSmarket.queued_purchases, this);
        return base.Destroy(force);
    }

    // tg: code/modules/cargo/markets/market_item.dm:153
    public virtual void on_instance_del(datum source = null)
    {
        if (QDELETED(this))
            return;
        // Uh oh, uplink or item is gone. We will just keep the money and you will not get your order.
        qdel(this);
    }

    /**
     * Proc that applies secondary effects to objects that are spawned via a market.
     *
     * @param spawned_item - Reference to the atom being spawned.
     * @param legal_status - Is this item considered legal? If illegal, will apply the contraband trait to the spawned item.
     */
    // tg: code/modules/cargo/markets/market_item.dm:166
    public virtual void post_purchase_effects(atom spawned_item = null)
    {
        if (!Dm.truthy(legallity) && isobj(spawned_item))
            ADD_TRAIT(spawned_item, TRAIT_CONTRABAND, INNATE_TRAIT);
    }
}
