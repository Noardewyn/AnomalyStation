// Anomaly Station code, not a translation of tg: the shots-left number of a held gun (DV401).
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// DEVIATION(DV401): a gun in a hand or in a storage shows how many shots it has left as a number on its picture, the way a stack shows its
// amount (stack.dm:117-128: maptext, set while held or stored, refreshed when the picture updates and when the gun moves). tg tells it only
// by examine and by the gun's own overlays.
public partial class obj_item_gun
{
    /// <summary>The shots left to show on the picture, null for none.</summary>
    public virtual float? dm_rounds_shown() => null;

    /// <summary>The most shots the gun holds as it is now (its magazine's or its cell's), null when it has no such number: shown after a slash.</summary>
    public virtual float? dm_rounds_max() => null;

    public void dm_show_rounds()
    {
        float? rounds = ismob(loc) || (loc as atom)?.atom_storage != null ? dm_rounds_shown() : null;
        maptext = rounds == null ? "" : MAPTEXT(dm_rounds_max() is float most ? Dm.text("<font color='white'>[]/[]</font>", rounds, most) : Dm.text("<font color='white'>[]</font>", rounds));
    }

    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        dm_show_rounds();
        return dot;
    }

    public override async DmTask<object> Moved(atom old_loc = null, object movement_dir = null, object forced = null, DmList old_locs = null, float? momentum_change = null)
    {
        object dot = await base.Moved(old_loc, movement_dir, forced, old_locs, momentum_change);
        dm_show_rounds();
        return dot;
    }
}

public partial class obj_item_gun_ballistic
{
    public override float? dm_rounds_shown() => get_ammo();

    // the magazine's rounds and the one in the chamber, where the chambered round leaves the magazine (chamber_round: not an open bolt's
    // nor a boltless internal magazine's, whose chambered round stays in it)
    public override float? dm_rounds_max() => magazine == null ? null
        : magazine.max_ammo + (bolt_type != BOLT_TYPE_OPEN && !(Dm.truthy(internal_magazine) && bolt_type == BOLT_TYPE_NO_BOLT) ? 1 : 0);
}

public partial class obj_item_gun_ballistic_revolver
{
    // a revolver's get_ammo counts the spent casings left in the cylinder (and its chambered round is one of the cylinder's): the live ones only
    public override float? dm_rounds_shown() => get_ammo(FALSE, FALSE);

    public override float? dm_rounds_max() => magazine?.max_ammo;
}

public partial class obj_item_gun_energy
{
    public override float? dm_rounds_shown() =>
        !QDELETED(cell) && ammo_type?[select] is obj_item_ammo_casing_energy shot && shot.e_cost > 0 ? Dm.floor(cell.charge / shot.e_cost) : null;

    public override float? dm_rounds_max() =>
        !QDELETED(cell) && ammo_type?[select] is obj_item_ammo_casing_energy shot && shot.e_cost > 0 ? Dm.floor(cell.maxcharge / shot.e_cost) : null;
}
