// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/wardrobe.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: an inert stub subsystem (lead decision W3 G2) with its vars and provide_type translated (M2 F1: an outfit takes
// its items from it, EQUIP_OUTFIT_ITEM, datums/outfit.dm:162). tg's subsystem preloads the items of the preloaded outfits
// (the job outfits) while players sit in the lobby (Initialize, load_outfits, the stock queue, fire); with the stub nothing
// is stocked, canon_minimum stays empty and provide_type makes every item on request, as tg does for a type it does not
// stock. The stock, the queue, recycling and the stat entry are translated with inventory (M4a).
// STUB(M4a): translated in M4a (inventory).

/// This subsystem strives to make loading large amounts of select objects as smooth at execution as possible
/// It preloads a set of types to store, and caches them until requested
/// Doesn't catch everything mind, this is intentional. There's many types that expect to either
/// A: Not sit in a list for 2 hours, or B: have extra context passed into them, or for their parent to be their location
/// You should absolutely not spam this system, it will break things in new and wonderful ways
/// S close enough for government work though.
// tg: code/controllers/subsystem/wardrobe.dm:8 SUBSYSTEM_DEF(wardrobe)
[DmPath("/datum/controller/subsystem/wardrobe")]
public partial class datum_controller_subsystem_wardrobe : datum_controller_subsystem
{
    // tg: code/controllers/subsystem/wardrobe.dm:9-16
    public datum_controller_subsystem_wardrobe()
    {
        name = "Wardrobe";
        wait = 10; // This is more like a queue then anything else
        // STUB(M4a): SS_NO_INIT | SS_NO_FIRE instead of tg's SS_BACKGROUND until the stock is translated.
        ss_flags = Dm.bitor(SS_NO_INIT, SS_NO_FIRE);
        runlevels = Dm.bitor(RUNLEVEL_LOBBY, RUNLEVELS_DEFAULT); // We're going to fill up our cache while players sit in the lobby
    }

    /// How much to cache outfit items
    /// Multiplier, 2 would mean cache enough items to stock 1 of each preloaded order twice, etc
    // tg: code/controllers/subsystem/wardrobe.dm:19
    public float cache_intensity = 2;
    /// How many more then the template of a type are we allowed to have before we delete applicants?
    // tg: code/controllers/subsystem/wardrobe.dm:21
    public float overflow_lienency = 2;
    /// List of type -> list(insertion callback, removal callback) callbacks for insertion/removal to use.
    /// Set in setup_callbacks, used in canonization.
    // tg: code/controllers/subsystem/wardrobe.dm:24
    public DmList initial_callbacks;
    /// Canonical list of types required to fill all preloaded stocks once.
    /// Type -> list(count, last inspection timestamp, call on insert, call on removal)
    // tg: code/controllers/subsystem/wardrobe.dm:27
    public DmList canon_minimum;
    /// List of types to load. Type -> count //(I'd do a list of lists but this needs to be refillable)
    // tg: code/controllers/subsystem/wardrobe.dm:29
    public DmList order_list;
    /// List of lists. Contains our preloaded atoms. Type -> list(last inspect time, list(instances))
    // tg: code/controllers/subsystem/wardrobe.dm:31
    public DmList preloaded_stock;
    /// The last time we inspected our stock
    // tg: code/controllers/subsystem/wardrobe.dm:33
    public float last_inspect_time = 0;
    /// How often to inspect our stock, in deciseconds
    // tg: code/controllers/subsystem/wardrobe.dm:35
    public float inspect_delay = 30 * SECONDS;
    /// What we're currently doing
    // tg: code/controllers/subsystem/wardrobe.dm:37
    public float current_task = SSWARDROBE_STOCK;
    /// How many times we've had to generate a stock item on request
    // tg: code/controllers/subsystem/wardrobe.dm:39
    public float stock_miss = 0;
    /// How many times we've successfully returned a cached item
    // tg: code/controllers/subsystem/wardrobe.dm:41
    public float stock_hit = 0;
    /// How many items would we make just by loading the master list once?
    // tg: code/controllers/subsystem/wardrobe.dm:43
    public float one_go_master = 0;
    // Hit/Miss Tracking
    // tg: code/controllers/subsystem/wardrobe.dm:45
    public DmList hit_map;
    // tg: code/controllers/subsystem/wardrobe.dm:46
    public DmList miss_map;
    // tg: code/controllers/subsystem/wardrobe.dm:48  var/static/list/recycle_blacklist = typecacheof(list(/obj/item/mod/control/pre_equipped))
    // (only recycling into the stock reads the list, which is not translated, M4a; the MOD suit type is live since gaps MO1)

    // tg: code/controllers/subsystem/wardrobe.dm:12, :24-46  the object-creating defaults (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        dependencies = Dm.list(
            typeof(datum_controller_subsystem_atoms),
            typeof(datum_controller_subsystem_mapping));
        initial_callbacks = Dm.list();
        canon_minimum = Dm.list();
        order_list = Dm.list();
        preloaded_stock = Dm.list();
        hit_map = Dm.list();
        miss_map = Dm.list();
    }

    // The New() that SUBSYSTEM_DEF(wardrobe) defines (__DEFINES/MC.dm:93-96), with NEW_SS_GLOBAL(SSwardrobe)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/wardrobe.dm:8
    public override async DmTask New(params object[] __extra)
    {
        if (SSwardrobe != this)
        {
            if (SSwardrobe is datum_controller_subsystem_wardrobe)
            {
                Recover();
                qdel(SSwardrobe);
            }
            SSwardrobe = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/wardrobe.dm:271
    public virtual async DmTask<object> provide_type(Type requested_type = null, atom_movable location = null)
    {
        atom_movable requested_object = null;
        if (!Dm.truthy(canon_minimum[requested_type]))
            return await Dm.new_async(requested_type, location);

        DmList stock_info = preloaded_stock[requested_type] as DmList;
        if (!Dm.truthy(stock_info))
        {
            stock_miss++;
            Dm.inc_at(miss_map, requested_type);
            requested_object = await Dm.new_async(requested_type, location) as atom_movable;
            return requested_object;
        }

        // The stocked branch (:285-308: take the last stocked item, its removal callback, the hit tracking and the requeue):
        // nothing is stocked while the stub does not initialize (M4a).
        Dm.NotTranslated("code/controllers/subsystem/wardrobe.dm:285", "provide_type: an item from the preloaded stock (the wardrobe's stock, M4a)");
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/wardrobe.dm:8  SUBSYSTEM_DEF(wardrobe): GLOBAL_REAL(SSwardrobe, /datum/controller/subsystem/wardrobe)
    internal datum_controller_subsystem_wardrobe SSwardrobe;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_wardrobe SSwardrobe => ref TgState.Current.SSwardrobe;
}
