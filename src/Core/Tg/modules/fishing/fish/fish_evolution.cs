// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/fish/fish_evolution.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the fish evolutions, the new kinds an offspring or a growing fish may turn into, with their conditions. The
// var blocks are generated (D111).

// tg: code/modules/fishing/fish/fish_evolution.dm:2 and :4 (interleaved types, rules 1.9)
public partial class datum_controller_global_vars : datum_controller
{
    ///A global list of fish evolutions, which are singletons.
    // tg: code/modules/fishing/fish/fish_evolution.dm:2  GLOBAL_LIST_INIT(fish_evolutions, init_subtypes_w_path_keys(/datum/fish_evolution, list()))
    public DmList fish_evolutions;

    ///A list of fish evolution types, each having an associated list containing all fish types that have it.
    // tg: code/modules/fishing/fish/fish_evolution.dm:4  GLOBAL_LIST_EMPTY(fishes_by_fish_evolution)
    public DmList fishes_by_fish_evolution;

    // tg: code/modules/fishing/fish/fish_evolution.dm:2  (GLOBAL_MANAGED, __DEFINES/__globals.dm:5)
    public virtual async DmTask InitGlobalfish_evolutions()
    {
        fish_evolutions = await init_subtypes_w_path_keys(typeof(datum_fish_evolution), Dm.list()) as DmList;
        Dm.append(ref gvars_datum_init_order, "fish_evolutions");
    }

    // tg: code/modules/fishing/fish/fish_evolution.dm:4  (GLOBAL_MANAGED, __DEFINES/__globals.dm:5)
    public virtual void InitGlobalfishes_by_fish_evolution()
    {
        fishes_by_fish_evolution = Dm.list();
        Dm.append(ref gvars_datum_init_order, "fishes_by_fish_evolution");
    }
}

/**
 * Fish evolution datums
 *
 * If present in a fish's evolution_types list, and other conditions are met in check_conditions()
 * then there's a chance the offspring may be of a new type rather than the same as its source or mate (if any).
 */
// tg: code/modules/fishing/fish/fish_evolution.dm:12
public partial class datum_fish_evolution : datum
{
    // tg: code/modules/fishing/fish/fish_evolution.dm:34
    [ShouldCallParent]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (!ispath(new_fish_type, typeof(obj_item_fish)))
            stack_trace(Dm.text("[] instantiated with a new fish type of []. That's not a fish, hun, things will break.", type, new_fish_type), "code/modules/fishing/fish/fish_evolution.dm", 38);
        if (!Dm.truthy(name))
            name = full_capitalize(Dm.initial(new_fish_type, (atom p) => p.name));
    }

    /**
     * The main proc that checks whether this can happen or not.
     * Keep in mind the mate and aquarium arguments may be null if
     * the fish is self-reproducing or this evolution is a result of a fish_growth component
     */
    // tg: code/modules/fishing/fish/fish_evolution.dm:46
    [ShouldCallParent]
    public virtual async DmTask<object> check_conditions(obj_item_fish source = null, obj_item_fish mate = null, atom_movable aquarium = null)
    {
        //the fish don't reproduce outside of aquariums but can still grow there, so we just check if the temperature is right.
        if (aquarium == null)
            return await source.proper_environment(required_temperature_min, required_temperature_max);
        if (Dm.truthy(Dm.bitand(!Dm.truthy(aquarium._listen_lookup?[COMSIG_AQUARIUM_CHECK_EVOLUTION_CONDITIONS]) ? NONE : aquarium._SendSignal(COMSIG_AQUARIUM_CHECK_EVOLUTION_CONDITIONS, Dm.list(aquarium, source, mate, this)), COMPONENT_ALLOW_EVOLUTION)))
            return TRUE;
        return FALSE;
    }

    ///This is called when the evolution is set as the result type of a fish_growth component
    // tg: code/modules/fishing/fish/fish_evolution.dm:56
    [SignalHandler, ShouldCallParent]
    public virtual object growth_checks(obj_item_fish source = null, object seconds_per_tick = null, object growth = null)
    {
        if (Dm.num(source.get_health_percentage()) < 0.5f)
            return COMPONENT_DONT_GROW;
        if (Dm.num(source.get_hunger()) >= 0.5f) //too hungry to grow
            return COMPONENT_DONT_GROW;
        if (HAS_TRAIT(source.loc, TRAIT_STOP_FISH_REPRODUCTION_AND_GROWTH)) //the aquarium has breeding disabled
            return COMPONENT_DONT_GROW;
        if (!Dm.truthy(Waiting.NoSleep(check_conditions(source))))
            return COMPONENT_DONT_GROW;
        return null;
    }

    ///Called by the fish analyzer right click function. Returns a text string used as tooltip.
    // tg: code/modules/fishing/fish/fish_evolution.dm:69
    public virtual object get_evolution_tooltip()
    {
        string dot = "";
        if (required_temperature_min > 0 || required_temperature_max < INFINITY)
        {
            string temp_reqs = "";
            if (required_temperature_min == 0)
                temp_reqs = I18n.T("below []", required_temperature_max);
            else if (required_temperature_max == INFINITY)
                temp_reqs = I18n.T("above []", required_temperature_min);
            else
                temp_reqs = I18n.T("of [] to []", required_temperature_min, required_temperature_max);
            dot = I18n.T("An aquarium temperature []K is required.", temp_reqs);
        }

        if (Dm.truthy(conditions_note))
            dot += Dm.text(" []", conditions_note);
        return dot;
    }
}

// tg: code/modules/fishing/fish/fish_evolution.dm:86
public partial class datum_fish_evolution_lubefish : datum_fish_evolution
{
    // tg: code/modules/fishing/fish/fish_evolution.dm:91
    public override async DmTask<object> check_conditions(obj_item_fish source = null, obj_item_fish mate = null, atom_movable aquarium = null)
    {
        if (!HAS_TRAIT(source, TRAIT_FISH_FED_LUBE))
            return FALSE;
        return await base.check_conditions(source, mate, aquarium);
    }
}

// tg: code/modules/fishing/fish/fish_evolution.dm:102
public partial class datum_fish_evolution_mastodon : datum_fish_evolution
{
    // tg: code/modules/fishing/fish/fish_evolution.dm:110
    public override async DmTask<object> check_conditions(obj_item_fish source = null, obj_item_fish mate = null, atom_movable aquarium = null)
    {
        if ((Dm.num(source.size) < 120 || Dm.num(source.weight) < 3000) || (mate != null && (Dm.num(mate.size) < 120 || Dm.num(mate.weight) < 3000)))
            return FALSE;
        return await base.check_conditions(source, mate, aquarium);
    }
}

// tg: code/modules/fishing/fish/fish_evolution.dm:132
public partial class datum_fish_evolution_chainsawfish : datum_fish_evolution
{
    // tg: code/modules/fishing/fish/fish_evolution.dm:138
    public override async DmTask<object> check_conditions(obj_item_fish source = null, obj_item_fish mate = null, atom_movable aquarium = null)
    {
        float double_avg_size = Dm.initial(typeof(obj_item_fish_goldfish), (obj_item_fish p) => p.average_size) * 2;
        float double_avg_weight = Dm.initial(typeof(obj_item_fish_goldfish), (obj_item_fish p) => p.average_weight) * 2;
        if (Dm.num(source.size) >= double_avg_size && Dm.num(source.weight) >= double_avg_weight && Dm.@in(typeof(datum_fish_trait_territorial), source.fish_traits))
            return await base.check_conditions(source, mate, aquarium);
        return FALSE;
    }
}

// tg: code/modules/fishing/fish/fish_evolution.dm:145
public partial class datum_fish_evolution_armored_pike : datum_fish_evolution
{
    // tg: code/modules/fishing/fish/fish_evolution.dm:150
    public override async DmTask<object> check_conditions(obj_item_fish source = null, obj_item_fish mate = null, atom_movable aquarium = null)
    {
        if (HAS_TRAIT(source, TRAIT_FISH_STINGER))
            return await base.check_conditions(source, mate, aquarium);
        return FALSE;
    }
}

// Kept as tg has it: the conditions are declared on /datum/fish_evolution/nessiefish, a type no one uses (the nessie evolution
// is /datum/fish_evolution/nessie), and the second check compares the size with the fritterish's average weight.
// tg: code/modules/fishing/fish/fish_evolution.dm:166
public partial class datum_fish_evolution_nessiefish : datum_fish_evolution
{
    // tg: code/modules/fishing/fish/fish_evolution.dm:166
    public override async DmTask<object> check_conditions(obj_item_fish source = null, obj_item_fish mate = null, atom_movable aquarium = null)
    {
        if (Dm.num(source.size) >= (Dm.initial(typeof(obj_item_fish_fryish_fritterish), (obj_item_fish p) => p.average_size) * 1.5f) && Dm.num(source.size) >= (Dm.initial(typeof(obj_item_fish_fryish_fritterish), (obj_item_fish p) => p.average_weight) * 1.5f))
            return await base.check_conditions(source, mate, aquarium);
        return FALSE;
    }
}

// tg: code/modules/fishing/fish/fish_evolution.dm:171
public partial class datum_fish_evolution_moonfish : datum_fish_evolution
{
    // tg: code/modules/fishing/fish/fish_evolution.dm:176
    public override async DmTask<object> check_conditions(obj_item_fish source = null, obj_item_fish mate = null, atom_movable aquarium = null)
    {
        if (Dm.num(source.size) < (Dm.initial(typeof(obj_item_fish_moonfish_dwarf), (obj_item_fish p) => p.average_size) * 1.5f) && Dm.num(source.size) < (Dm.initial(typeof(obj_item_fish_moonfish_dwarf), (obj_item_fish p) => p.average_weight) * 1.5f))
            return FALSE;
        if (mate != null && (Dm.num(mate.size) < (Dm.initial(typeof(obj_item_fish_moonfish), (obj_item_fish p) => p.average_size) * 1.3f) && Dm.num(mate.size) < (Dm.initial(typeof(obj_item_fish_moonfish), (obj_item_fish p) => p.average_weight) * 1.3f)))
            return FALSE;
        return await base.check_conditions(source, mate, aquarium);
    }
}

// tg: code/modules/fishing/fish/fish_evolution.dm:183
public partial class datum_fish_evolution_dwarf_moonfish : datum_fish_evolution
{
    // tg: code/modules/fishing/fish/fish_evolution.dm:188
    public override async DmTask<object> check_conditions(obj_item_fish source = null, obj_item_fish mate = null, atom_movable aquarium = null)
    {
        if (Dm.num(source.size) > (Dm.initial(typeof(obj_item_fish_moonfish), (obj_item_fish p) => p.average_size) * 0.66f) && Dm.num(source.size) > (Dm.initial(typeof(obj_item_fish_moonfish), (obj_item_fish p) => p.average_weight) * 0.66f))
            return FALSE;
        if (mate != null && (Dm.num(mate.size) > (Dm.initial(typeof(obj_item_fish_moonfish), (obj_item_fish p) => p.average_size) * 0.7f) && Dm.num(mate.size) > (Dm.initial(typeof(obj_item_fish_moonfish), (obj_item_fish p) => p.average_weight) * 0.7f)))
            return FALSE;
        return await base.check_conditions(source, mate, aquarium);
    }
}
