// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/cat_house.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C2): the cat's tree looks for one.

// tg: code/game/objects/structures/cat_house.dm:2
public partial class obj_structure_cat_house : obj_structure
{
    // tg: code/game/objects/structures/cat_house.dm:12
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_ATOM_ATTACK_BASIC_MOB, nameof(enter_home));
        return dot;
    }

    // tg: code/game/objects/structures/cat_house.dm:16
    [SignalHandler]
    public virtual void enter_home(datum source = null, mob_living attacker = null)
    {
        if (resident_cat == null && attacker is mob_living_basic_pet_cat)
        {
            Waiting.NoSleep(attacker.forceMove(this));
            return;
        }
        if (resident_cat == attacker)
            Waiting.NoSleep(attacker.forceMove(drop_location() as atom));
    }

    // tg: code/game/objects/structures/cat_house.dm:25
    public override async DmTask<object> Entered(atom_movable mover = null, atom old_loc = null, DmList old_locs = null)
    {
        object dot = await base.Entered(mover, old_loc, old_locs);
        if (mover is not mob_living_basic_pet_cat)
            return dot;
        resident_cat = mover as mob_living;
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/game/objects/structures/cat_house.dm:32
    public override async DmTask<object> Exited(atom_movable mover = null, object direction = null)
    {
        object dot = await base.Exited(mover, direction);
        if (mover != resident_cat)
            return dot;
        resident_cat = null;
        update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/game/objects/structures/cat_house.dm:39
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (resident_cat == null)
            return dot;
        image cat_icon = Dm.image(resident_cat.icon, icon_state: resident_cat.icon_state, layer: LOW_ITEM_LAYER);
        cat_icon.transform = ((matrix)cat_icon.transform).Scale(0.7f, 0.7f);
        cat_icon.pixel_w = 0;
        cat_icon.pixel_z = -9;
        Dm.append(ref dot, cat_icon);
        return dot;
    }
}
