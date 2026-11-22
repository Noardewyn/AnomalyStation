// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/shoe_footstep.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M4c slice 2: the magboots' rigstep footsteps). The var block is generated.

/// Basically the squeak component but stripped down for only shoes
/// In other words it replaces the mob's default footstep sounds with the ones provided on all surfaces
// tg: code/datums/components/shoe_footstep.dm:3
public partial class datum_component_shoe_footstep : datum_component
{
    // tg: code/datums/components/shoe_footstep.dm:24
    [DmParams("sounds", "volume", "chance_per_play", "steps_per_play", "extrarange", "falloff_exponent", "falloff_distance")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        DmList sounds = Dm.arg(__extra, 0) as DmList;
        object volume = Dm.arg(__extra, 1) ?? 50;
        object chance_per_play = Dm.arg(__extra, 2) ?? 100;
        object steps_per_play = Dm.arg(__extra, 3) ?? 1;
        object extrarange = Dm.arg(__extra, 4) ?? MEDIUM_RANGE_SOUND_EXTRARANGE;
        object falloff_exponent = Dm.arg(__extra, 5) ?? SOUND_FALLOFF_EXPONENT;
        object falloff_distance = Dm.arg(__extra, 6) ?? SOUND_DEFAULT_FALLOFF_DISTANCE;
        if (!istype(parent, typeof(obj_item_clothing_shoes)))
            return COMPONENT_INCOMPATIBLE;
        if (!Dm.truthy(length(sounds)) || Dm.num(volume) <= 0 || Dm.num(chance_per_play) <= 0 || Dm.num(steps_per_play) < 0)
            return COMPONENT_INCOMPATIBLE;
        this.sounds = sounds;
        // src.can_tape = can_tape (:38): can_tape is not a parameter of Initialize, so tg assigns the var to itself (no effect).
        this.volume = volume;
        this.chance_per_play = chance_per_play;
        this.steps_per_play = steps_per_play;
        this.extrarange = extrarange;
        this.falloff_exponent = falloff_exponent;
        this.falloff_distance = falloff_distance;
        return null;
    }

    // tg: code/datums/components/shoe_footstep.dm:46
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_SHOES_STEP_ACTION, nameof(stepped));
        RegisterSignal(parent, COMSIG_ITEM_EQUIPPED, nameof(equipped));
        RegisterSignal(parent, COMSIG_ITEM_DROPPED, nameof(dropped));

        obj_item parent_object = parent as obj_item;
        mob_living wearer = parent_object.loc as mob_living;
        if (wearer is mob_living && Dm.truthy(Dm.bitand(wearer.get_slot_by_item(parent_object), parent_object.slot_flags)))
            ADD_TRAIT(wearer, TRAIT_SILENT_FOOTSTEPS, REF(this));
    }

    // tg: code/datums/components/shoe_footstep.dm:56
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(
            COMSIG_SHOES_STEP_ACTION,
            COMSIG_ITEM_EQUIPPED,
            COMSIG_ITEM_DROPPED,
            COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM));
        obj_item parent_object = parent as obj_item;
        if (ismob(parent_object.loc))
            REMOVE_TRAIT(parent_object.loc, TRAIT_SILENT_FOOTSTEPS, REF(this));
    }

    // tg: code/datums/components/shoe_footstep.dm:67
    [SignalHandler]
    public virtual void stepped(obj_item_clothing_shoes source = null)
    {
        mob_living_carbon_human owner = source.loc as mob_living_carbon_human;
        if (SHOULD_ATOM_DISABLE_FOOTSTEPS(owner) || SHOULD_MOB_DISABLE_FOOTSTEPS(owner))
            return;
        if (HAS_TRAIT_NOT_FROM(owner, TRAIT_SILENT_FOOTSTEPS, REF(this)))
            return; // a second source of silent footsteps silences us! the plot thickens

        if (steps < Dm.num(steps_per_play))
        {
            steps++;
            return;
        }

        steps = 0;
        if (prob(Dm.num(chance_per_play)))
        {
            playsound(
                source: source,
                soundin: pick(sounds),
                vol: volume,
                vary: TRUE,
                extrarange: extrarange,
                falloff_exponent: Dm.num_or_null(falloff_exponent),
                falloff_distance: Dm.num_or_null(falloff_distance));
        }
    }

    // tg: code/datums/components/shoe_footstep.dm:92
    [SignalHandler]
    public virtual void equipped(obj_item source = null, mob user = null, object slot = null)
    {
        if (Dm.truthy(Dm.bitand(slot, source.slot_flags)))
            ADD_TRAIT(user, TRAIT_SILENT_FOOTSTEPS, REF(this));
    }

    // tg: code/datums/components/shoe_footstep.dm:97
    [SignalHandler]
    public virtual void dropped(obj_item source = null, mob user = null, object silent = null)
    {
        REMOVE_TRAIT(user, TRAIT_SILENT_FOOTSTEPS, REF(this));
    }
}
