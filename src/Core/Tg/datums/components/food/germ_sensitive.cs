// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/food/germ_sensitive.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps VI1, D341): food and pills left on the floor for five seconds catch germs (a weakly infective floor
// disease) and look dirty; picking them up stops the countdown, cleaning, frying and grilling remove the germs. The var block is
// generated (D111).

// Don't eat off the floor or hold parent object with dirty hands, you'll get sick

public partial class datum_controller_global_vars
{
    /// Possible diseases
    // tg: code/datums/components/food/germ_sensitive.dm:7  GLOBAL_LIST_INIT(floor_diseases, list(...))
    public DmList floor_diseases;

    // tg: code/datums/components/food/germ_sensitive.dm:7
    public virtual void InitGlobalfloor_diseases()
    {
        floor_diseases = Dm.list(
            Dm.kv(typeof(datum_disease_nebula_nausea), 2),
            Dm.kv(typeof(datum_disease_gastritium), 2),
            Dm.kv(typeof(datum_disease_carpellosis), 1)
        );
        Dm.append(ref gvars_datum_init_order, nameof(floor_diseases));
    }
}

/// Makes items infective if left on floor, also sending corresponding signals to parent
// tg: code/datums/components/food/germ_sensitive.dm:14
public partial class datum_component_germ_sensitive : datum_component
{
    /// Time needed for bacteria to infect the parent object
    private const float GERM_EXPOSURE_DELAY = 5 * SECONDS; // Five-second rule

    // tg: code/datums/components/food/germ_sensitive.dm:20
    [DmParams("mapload")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object mapload = Dm.arg(__extra, 0);
        object dot = await base.Initialize(Dm.forward(__extra, mapload));
        if (!isobj(parent))
            return COMPONENT_INCOMPATIBLE;

        ADD_TRAIT(parent, TRAIT_GERM_SENSITIVE, REF(this));

        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(examine));
        RegisterSignal(parent, COMSIG_MOVABLE_MOVED, nameof(handle_movement));
        RegisterSignals(parent, Dm.list(COMSIG_COMPONENT_CLEAN_ACT, COMSIG_ITEM_FRIED, COMSIG_ITEM_BARBEQUE_GRILLED, COMSIG_ATOM_FIRE_ACT), nameof(delete_germs));

        RegisterSignals(parent, Dm.list(
            COMSIG_ITEM_DROPPED, //Dropped into the world
            COMSIG_ATOM_EXITED //Object exits a storage object (tables, boxes, etc)
        ),
        nameof(dropped));

        RegisterSignals(parent, Dm.list(
            COMSIG_ITEM_PICKUP, //Picked up by mob
            COMSIG_ATOM_ENTERED //Object enters a storage object (tables, boxes, etc.)
        ),
        nameof(picked_up));

        // Map spawned items are protected until moved
        if (!Dm.truthy(mapload))
            handle_movement();
        return dot;
    }

    // tg: code/datums/components/food/germ_sensitive.dm:46
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        await DmTask.CompletedTask;
        REMOVE_TRAIT(parent, TRAIT_GERM_SENSITIVE, REF(this));
        UnregisterSignal(parent, Dm.list(
            COMSIG_ATOM_ENTERED,
            COMSIG_ATOM_EXAMINE,
            COMSIG_ATOM_EXITED,
            COMSIG_COMPONENT_CLEAN_ACT,
            COMSIG_ITEM_FRIED,
            COMSIG_ITEM_BARBEQUE_GRILLED,
            COMSIG_ATOM_FIRE_ACT,
            COMSIG_ITEM_DROPPED,
            COMSIG_ITEM_PICKUP,
            COMSIG_MOVABLE_MOVED
        ));
    }

    // tg: code/datums/components/food/germ_sensitive.dm:61
    public override object Destroy(float? force = null)
    {
        remove_timer();
        return base.Destroy(force);
    }

    // tg: code/datums/components/food/germ_sensitive.dm:65
    public virtual void remove_timer()
    {
        if (!Dm.truthy(timer_id))
            return;
        deltimer(timer_id);
        timer_id = null;
    }

    // tg: code/datums/components/food/germ_sensitive.dm:71
    [SignalHandler]
    public virtual void handle_movement()
    {
        // SIGNAL_HANDLER

        obj parent_object = (obj)parent;
        turf_open open_turf = parent_object.loc as turf_open;

        // Is parent on valid open turf?
        if (!(open_turf is turf_open) || islava(open_turf) || isasteroidturf(open_turf) || !Dm.truthy(parent_object.has_gravity()))
        {
            remove_timer();
            if (Dm.truthy(parent._listen_lookup?[COMSIG_ATOM_GERM_UNEXPOSED]))
                parent._SendSignal(COMSIG_ATOM_GERM_UNEXPOSED, Dm.list(parent, this));
            return;
        }

        // Is parent on an elevated structure?
        foreach (atom_movable content in Dm.each_cast<atom_movable>(open_turf.contents))
        {
            if (Dm.truthy(GLOB.typecache_elevated_structures[content.type]))
            {
                remove_timer();
                if (Dm.truthy(parent._listen_lookup?[COMSIG_ATOM_GERM_UNEXPOSED]))
                    parent._SendSignal(COMSIG_ATOM_GERM_UNEXPOSED, Dm.list(parent, this));
                return;
            }
        }

        // Exposed to bacteria, start countdown until becoming infected
        timer_id = _addtimer(Dm.@new<datum_callback>(this, nameof(expose_to_germs)), GERM_EXPOSURE_DELAY, Dm.bitor(TIMER_STOPPABLE, TIMER_UNIQUE), file: "code/datums/components/food/germ_sensitive.dm", line: 92);
    }

    // tg: code/datums/components/food/germ_sensitive.dm:94
    [SignalHandler]
    public virtual void picked_up()
    {
        // SIGNAL_HANDLER
        if (Dm.truthy(parent._listen_lookup?[COMSIG_ATOM_GERM_UNEXPOSED]))
            parent._SendSignal(COMSIG_ATOM_GERM_UNEXPOSED, Dm.list(parent, this));
        remove_timer();
    }

    // tg: code/datums/components/food/germ_sensitive.dm:99
    [SignalHandler]
    public virtual void dropped()
    {
        // SIGNAL_HANDLER
        handle_movement();
    }

    // tg: code/datums/components/food/germ_sensitive.dm:103
    [SignalHandler]
    public virtual void examine(datum source = null, mob user = null, DmList examine_list = null)
    {
        // SIGNAL_HANDLER
        if (Dm.truthy(infective))
            Dm.append(examine_list, span_warning(I18n.T("[] looks dirty and not safe to consume.", parent)));
    }

    // tg: code/datums/components/food/germ_sensitive.dm:108
    public virtual async DmTask expose_to_germs()
    {
        // Admin spawned items are never exposed
        atom parent_atom = (atom)parent;
        if (Dm.truthy(Dm.bitand(parent_atom.flags_1, ADMIN_SPAWNED_1)))
            return;

        if (Dm.truthy(parent._listen_lookup?[COMSIG_ATOM_GERM_EXPOSED]))
            parent._SendSignal(COMSIG_ATOM_GERM_EXPOSED, Dm.list(parent, this));

        if (Dm.truthy(infective))
            return;
        infective = TRUE;

        Type random_disease = pick_weight(GLOB.floor_diseases) as Type;
        await parent.AddComponent(typeof(datum_component_infective), await Dm.new_async(random_disease), Dm.named("weak", TRUE));
    }

    // tg: code/datums/components/food/germ_sensitive.dm:123
    [SignalHandler]
    public virtual object delete_germs()
    {
        // SIGNAL_HANDLER

        object dot = NONE;

        if (Dm.truthy(infective))
        {
            infective = FALSE;
            qdel(parent.GetComponent(typeof(datum_component_infective)));
            return COMPONENT_CLEANED;
        }
        return dot;
    }
}
