// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/crates_lockers/crates/critter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// gaps ST3 (D333): the file whole. The critter crate breathes from its own emergency oxygen tank (dropped when the crate goes), draws
// its door open or shut over a body that keeps its icon state; the stasis crate's one-use seal breaks on its first opening with
// sparks, and a creature that leaves it leaves its stasis. The tank var is hand-written: the types converter typed it Type (from
// its bare `new`), so its line is deleted from the generated block, and with it hand mode takes the generated constructor here.

// tg: code/game/objects/structures/crates_lockers/crates/critter.dm:1
public partial class obj_structure_closet_crate_critter : obj_structure_closet_crate
{
    public obj_item_tank_internals_emergency_oxygen tank; // tg: code/game/objects/structures/crates_lockers/crates/critter.dm:22

    // tg: code/game/objects/structures/crates_lockers/crates/critter.dm:2  (the generated constructor, kept here: hand mode, see above)
    public obj_structure_closet_crate_critter()
    {
        name = "critter crate";
        desc = "A crate designed for safe transport of animals. It has an oxygen tank for safe transport in space.";
        icon_state = "crittercrate";
        base_icon_state = "crittercrate";
        horizontal = 0;
        allow_objects = 0;
        breakout_time = 600;
        material_drop = typeof(obj_item_stack_sheet_mineral_wood);
        material_drop_amount = 4;
        delivery_icon = "deliverybox";
        open_sound = Dm.resource("sound/machines/closet/wooden_closet_open.ogg");
        close_sound = Dm.resource("sound/machines/closet/wooden_closet_close.ogg");
        open_sound_volume = 25;
        close_sound_volume = 50;
        contents_pressure_protection = 0.8f;
        can_install_electronics = 0;
        elevation = 21;
        elevation_open = 0;
        can_weld_shut = 0;
    }

    // tg: code/game/objects/structures/crates_lockers/crates/critter.dm:24
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        tank = Dm.@new<obj_item_tank_internals_emergency_oxygen>();
        return dot;
    }

    // tg: code/game/objects/structures/crates_lockers/crates/critter.dm:28
    public override object Destroy(float? force = null)
    {
        turf T = get_turf(this);
        if (tank != null)
        {
            Waiting.NoSleep(tank.forceMove(T));
            tank = null;
        }

        return base.Destroy(force);
    }

    // tg: code/game/objects/structures/crates_lockers/crates/critter.dm:36  (SHOULD_CALL_PARENT(FALSE): the body keeps its icon state)
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        return null;
    }

    // tg: code/game/objects/structures/crates_lockers/crates/critter.dm:40
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList ?? Dm.list();
        if (Dm.truthy(opened))
        {
            Dm.append(dot, "crittercrate_door_open");
            return dot;
        }

        Dm.append(dot, "crittercrate_door");
        if (Dm.truthy(manifest))
            Dm.append(dot, "manifest");
        return dot;
    }

    // tg: code/game/objects/structures/crates_lockers/crates/critter.dm:50
    public override async DmTask<object> return_air()
    {
        if (tank != null)
            return await tank.return_air();
        else
            return await loc.return_air();
    }

    // tg: code/game/objects/structures/crates_lockers/crates/critter.dm:56
    public override object return_analyzable_air()
    {
        if (tank != null)
            return tank.return_analyzable_air();
        else
            return null;
    }
}

// tg: code/game/objects/structures/crates_lockers/crates/critter.dm:63
public partial class obj_structure_closet_crate_critter_stasis : obj_structure_closet_crate_critter
{
    // tg: code/game/objects/structures/crates_lockers/crates/critter.dm:67
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (HAS_TRAIT(gone, TRAIT_STASIS))
            remove_stasis(gone as mob_living);
        return dot;
    }

    // tg: code/game/objects/structures/crates_lockers/crates/critter.dm:72
    public virtual void remove_stasis(mob_living target)
    {
        target.remove_status_effect(typeof(datum_status_effect_grouped_stasis), STASIS_CRATE_EFFECT);
    }

    // tg: code/game/objects/structures/crates_lockers/crates/critter.dm:75
    public override async DmTask after_open(mob_living user = null, float? force = null)
    {
        await base.after_open(user, force);
        if (!Dm.truthy(stasis_sealed))
            return;
        stasis_sealed = FALSE;
        do_sparks(3, FALSE, this);
        to_chat(user, span_warning(I18n.T("[] one-use stasis mechanism has been triggered! It will not work again.", this)));
        paint_jobs = null;
    }
}
