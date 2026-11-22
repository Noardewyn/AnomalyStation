// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/boxes_magazines/internal/_cylinder.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice C: the revolver cylinder, whole. The var blocks are generated (D111).

// tg: code/modules/projectiles/boxes_magazines/internal/_cylinder.dm:1
public partial class obj_item_ammo_box_magazine_internal_cylinder : obj_item_ammo_box_magazine_internal
{
    ///Here, we have to maintain the list size, to emulate a cylinder with several chambers, empty or otherwise.
    // tg: code/modules/projectiles/boxes_magazines/internal/_cylinder.dm:8
    public override void remove_from_stored_ammo(atom_movable gone = null)
    {
        for (int index = 1; index <= stored_ammo.Count; index++)
        {
            object bullet = stored_ammo[(float)index];
            if (gone == bullet)
            {
                stored_ammo[(float)index] = null;
                update_appearance();
                return;
            }
        }
    }

    // tg: code/modules/projectiles/boxes_magazines/internal/_cylinder.dm:16
    public override obj_item_ammo_casing get_round()
    {
        rotate();
        object casing = stored_ammo[1f];
        if (ispath(casing))
        {
            casing = Dm.@new(casing as Type, this);
            stored_ammo[1f] = casing;
        }
        return casing as obj_item_ammo_casing;
    }

    // tg: code/modules/projectiles/boxes_magazines/internal/_cylinder.dm:24
    public override obj_item_ammo_casing get_and_shuffle_round()
    {
        return get_round();
    }

    // tg: code/modules/projectiles/boxes_magazines/internal/_cylinder.dm:27
    public virtual void rotate()
    {
        object b = stored_ammo[1f];
        stored_ammo.Cut(1, 2);
        stored_ammo.Insert(0, b);
    }

    // tg: code/modules/projectiles/boxes_magazines/internal/_cylinder.dm:32
    public virtual void spin()
    {
        for (float i = 1, n = rand(0, max_ammo * 2); i <= n; i++)
            rotate();
    }

    // tg: code/modules/projectiles/boxes_magazines/internal/_cylinder.dm:36
    public override DmList ammo_list()
    {
        DmList no_nulls_ammo = base.ammo_list();
        list_clear_nulls(no_nulls_ammo);
        return no_nulls_ammo;
    }

    // tg: code/modules/projectiles/boxes_magazines/internal/_cylinder.dm:41
    public override object give_round(obj_item_ammo_casing R = null, float? replace_spent = null)
    {
        replace_spent ??= 0;
        if (R == null || !(caliber != null ? Dm.eq(caliber, R.caliber) : ammo_type == R.GetType()))
            return FALSE;

        for (int i = 1; i <= stored_ammo.Count; i++)
        {
            object bullet = stored_ammo[(float)i];
            if (bullet != null && (bullet is not obj_item_ammo_casing live || live.loaded_projectile != null))
                continue;

            // empty or spent
            stored_ammo[(float)i] = R;
            Waiting.NoSleep(R.forceMove(this));
            if (bullet != null)
                Waiting.NoSleep((bullet as obj_item_ammo_casing).forceMove(drop_location() as atom));
            return TRUE;
        }

        return FALSE;
    }

    // tg: code/modules/projectiles/boxes_magazines/internal/_cylinder.dm:58
    public override void top_off(Type load_type = null, float? starting = null)
    {
        starting ??= FALSE;
        if (Dm.truthy(starting)) // nulls don't exist when we're starting off
        {
            base.top_off(load_type, starting);
            return;
        }

        if (load_type == null)
            load_type = ammo_type;

        for (float i = 1; i <= max_ammo; i++)
        {
            if (!Dm.truthy(give_round(Dm.@new(load_type, this) as obj_item_ammo_casing)))
                break;
        }
        update_appearance();
    }
}
