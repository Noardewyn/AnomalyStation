// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/mecha/mecha_helpers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/vehicles/mecha/mecha_helpers.dm:1
public partial class obj_vehicle_sealed_mecha : obj_vehicle_sealed
{
    ///////////////////////
    ///// Power stuff /////
    ///////////////////////
    // tg: code/modules/vehicles/mecha/mecha_helpers.dm:4
    public virtual bool has_charge(object amount = null) => Dm.num(get_charge()) >= Dm.num(amount);

    // tg: code/modules/vehicles/mecha/mecha_helpers.dm:7
    public virtual object get_charge() => cell?.charge;

    // tg: code/modules/vehicles/mecha/mecha_helpers.dm:10
    public virtual object use_energy(object amount = null)
    {
        object output = Waiting.NoSleep(cell.use(amount));
        if (Dm.truthy(output))
            diag_hud_set_mechcell();
        return output;
    }

    // tg: code/modules/vehicles/mecha/mecha_helpers.dm:15
    public virtual object give_power(object amount = null)
    {
        if (get_charge() != null)
        {
            Waiting.NoSleep(cell.give(amount));
            return TRUE;
        }
        return FALSE;
    }

    //////////////////////
    ///// Ammo stuff /////
    //////////////////////

    ///Max the ammo stored in all ballistic weapons for this mech
    // tg: code/modules/vehicles/mecha/mecha_helpers.dm:26
    public virtual void max_ammo()
    {
        foreach (obj_item I in Dm.each<obj_item>(flat_equipment))
        {
            if (istype(I, typeof(obj_item_mecha_parts_mecha_equipment_weapon_ballistic)))
            {
                obj_item_mecha_parts_mecha_equipment_weapon_ballistic gun = I as obj_item_mecha_parts_mecha_equipment_weapon_ballistic;
                gun.projectiles_cache = gun.projectiles_cache_max;
            }
        }
    }

    ///Called when the mecha with an MMI in it tries opening a door.
    // tg: code/modules/vehicles/mecha/mecha_helpers.dm:33
    public virtual void retrieve_access(datum source = null, DmList player_access = null)
    {
        Dm.append(player_access, accesses);
    }
}
