// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/decals/cleanable/food.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the salt pile translated (gaps RG1: the table salt poured on a floor leaves it; the snails stop at it). The tomato
// smudge's can_bloodcrawl_in and the squid ink's Initialize are left. The var blocks are generated (D111).

// tg: code/game/objects/effects/decals/cleanable/food.dm:32
public partial class obj_effect_decal_cleanable_food_salt : obj_effect_decal_cleanable_food
{
    // tg: code/game/objects/effects/decals/cleanable/food.dm:36  var/static/list/loc_connections (a type-level static, rules 10.6)
    public static ref DmList loc_connections => ref TgState.Current.obj_effect_decal_cleanable_food_salt__loc_connections;

    // tg: code/game/objects/effects/decals/cleanable/food.dm:40
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), loc_connections));
        return dot;
    }

    // tg: code/game/objects/effects/decals/cleanable/food.dm:44
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = await base.CanAllowThrough(mover, border_dir);
        if (Dm.truthy(is_species(mover, typeof(datum_species_snail))))
            return FALSE;
        return dot;
    }

    // tg: code/game/objects/effects/decals/cleanable/food.dm:49
    [DmParams("AM")]
    public override object Bumped(atom_movable bumped_atom = null)
    {
        object dot = base.Bumped(bumped_atom);
        if (Dm.truthy(is_species(bumped_atom, typeof(datum_species_snail))))
            to_chat(bumped_atom, span_danger(I18n.T("Your path is obstructed by [].", span_phobia(I18n.T("salt")))));
        return dot;
    }

    // tg: code/game/objects/effects/decals/cleanable/food.dm:54
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable AM = null)
    {
        if (!isliving(AM))
            return;

        if (iscarbon(AM))
        {
            mob_living_carbon C = (mob_living_carbon)AM;
            if (C.move_intent == MOVE_INTENT_WALK)
                return;
        }

        safepasses--;
        if (safepasses <= 0 && !QDELETED(this))
            qdel(this);
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/effects/decals/cleanable/food.dm:36  var/static/list/loc_connections
    internal DmList obj_effect_decal_cleanable_food_salt__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(obj_effect_decal_cleanable_food_salt.on_entered)));
}
