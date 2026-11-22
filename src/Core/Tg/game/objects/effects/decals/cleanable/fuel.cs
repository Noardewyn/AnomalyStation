// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/decals/cleanable/fuel.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps E1a (D327): the whole file, the pool of fuel (a major vent clog's filth): a flame, a burning mob, a spark, a hot thrown item or
// a hit sets it alight, a few hotspots one after another, and it lights the pools around it. The var blocks are generated (D111).

// tg: code/game/objects/effects/decals/cleanable/fuel.dm:1
public partial class obj_effect_decal_cleanable_fuel_pool : obj_effect_decal_cleanable
{
    // tg: code/game/objects/effects/decals/cleanable/fuel.dm:15  var/static/list/loc_connections (a type-level static, rules 10.6)
    public static ref DmList loc_connections => ref TgState.Current.obj_effect_decal_cleanable_fuel_pool__loc_connections;

    // tg: code/game/objects/effects/decals/cleanable/fuel.dm:20
    [DmParams("mapload", "burn_stacks")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object burn_stacks = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        if (Dm.eq(dot, INITIALIZE_HINT_QDEL))
            return dot;
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), loc_connections));
        foreach (obj_effect_decal_cleanable_fuel_pool pool in Dm.each<obj_effect_decal_cleanable_fuel_pool>(get_turf(this))) //Can't use locate because we also belong to that turf
        {
            if (pool == this)
                continue;
            pool.burn_amount = Math.Max(Math.Min(pool.burn_amount + Dm.num(burn_stacks), 10), 1);
            return INITIALIZE_HINT_QDEL;
        }

        if (Dm.truthy(burn_stacks))
            burn_amount = Math.Max(Math.Min(Dm.num(burn_stacks), 10), 1);

        return INITIALIZE_HINT_LATELOAD;
    }

    // Just in case of fires, do this after mapload.
    // tg: code/game/objects/effects/decals/cleanable/fuel.dm:37
    public override void LateInitialize(params object[] __extra)
    {
        // We don't want to burn down the create_and_destroy test area (#ifndef UNIT_TESTS: the port has no tg unit tests)
        RegisterSignal(this, COMSIG_ATOM_TOUCHED_SPARKS, nameof(ignition_trigger));
    }

    // tg: code/game/objects/effects/decals/cleanable/fuel.dm:43
    public override async DmTask<object> fire_act(object exposed_temperature = null, object exposed_volume = null)
    {
        object dot = await base.fire_act(exposed_temperature, exposed_volume);
        ignite();
        return dot;
    }

    /**
     * Ignites the fuel pool. This should be the only way to ignite fuel pools.
     */
    // tg: code/game/objects/effects/decals/cleanable/fuel.dm:50
    public virtual void ignite()
    {
        if (Dm.truthy(burning))
            return;
        burning = TRUE;
        burn_process();
    }

    /**
     * Spends 1 burn_amount and spawns a hotspot. If burn_amount is equal to 0, deletes the fuel pool.
     * Else, queues another call of this proc upon hotspot getting deleted and ignites other fuel pools around itself after 0.5 seconds.
     * THIS SHOULD NOT BE CALLED DIRECTLY.
     */
    // tg: code/game/objects/effects/decals/cleanable/fuel.dm:61
    [SignalHandler]
    public virtual void burn_process()
    {
        burn_amount -= 1;
        var hotspot = Dm.@new(hotspot_type, get_turf(this)) as obj_effect_hotspot;
        _addtimer(Dm.@new<datum_callback>(this, nameof(ignite_others)), 0.5f * SECONDS, file: "code/game/objects/effects/decals/cleanable/fuel.dm", line: 66);

        if (!Dm.truthy(burn_amount))
        {
            qdel(this);
            return;
        }

        RegisterSignal(hotspot, COMSIG_QDELETING, nameof(burn_process));
    }

    /**
     * Ignites other oil pools around itself.
     */
    // tg: code/game/objects/effects/decals/cleanable/fuel.dm:77
    public virtual void ignite_others()
    {
        foreach (obj_effect_decal_cleanable_fuel_pool oil in Dm.each<obj_effect_decal_cleanable_fuel_pool>(range(1, get_turf(this))))
            oil.ignite();
    }

    // tg: code/game/objects/effects/decals/cleanable/fuel.dm:81
    public override async DmTask<object> bullet_act(obj_projectile hit_proj = null, object def_zone = null, float? piercing_hit = null, float? blocked = null)
    {
        object dot = await base.bullet_act(hit_proj, def_zone, piercing_hit, blocked);
        if (hit_proj.damage > 0)
        {
            ignite();
            log_combat(hit_proj.firer as atom, this, Dm.text("used [] to ignite", hit_proj));
        }
        return dot;
    }

    // tg: code/game/objects/effects/decals/cleanable/fuel.dm:87
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(await tool.ignition_effect(this, user)))
            return await base.item_interaction(user, tool, modifiers, __extra);
        ignite();
        log_combat(user, this, Dm.text("used [] to ignite", tool));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/effects/decals/cleanable/fuel.dm:94
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable entered_atom = null)
    {
        if (!Dm.truthy(entered_atom.throwing)) // don't light from things being thrown over us, we handle that somewhere else
            ignition_trigger(source: this, enflammable_atom: entered_atom);
    }

    // tg: code/game/objects/effects/decals/cleanable/fuel.dm:100
    [SignalHandler]
    public virtual void ignition_trigger(datum source = null, atom_movable enflammable_atom = null)
    {
        if (isitem(enflammable_atom))
        {
            var enflamed_item = (obj_item)enflammable_atom;
            if (Dm.num(enflamed_item.get_temperature()) >= FIRE_MINIMUM_TEMPERATURE_TO_EXIST)
                ignite();
            return;
        }
        else if (isliving(enflammable_atom))
        {
            var enflamed_liver = (mob_living)enflammable_atom;
            if (Dm.truthy(enflamed_liver.on_fire))
                ignite();
        }
        else if (enflammable_atom is obj_effect_particle_effect_sparks)
            ignite();
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/effects/decals/cleanable/fuel.dm:15  var/static/list/loc_connections
    internal DmList obj_effect_decal_cleanable_fuel_pool__loc_connections = Dm.list(
        Dm.kv(COMSIG_TURF_MOVABLE_THROW_LANDED, nameof(obj_effect_decal_cleanable_fuel_pool.ignition_trigger)),
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(obj_effect_decal_cleanable_fuel_pool.on_entered)));
}
