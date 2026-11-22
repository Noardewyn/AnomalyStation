// Translated from tgstation (snapshot 1d99bd7f): code/modules/projectiles/projectile/bullets/foam_dart.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1a, D316: the Donksoft guns' darts, see ammunition/ballistic/foam.cs). The var blocks are generated (D111).

// tg: code/modules/projectiles/projectile/bullets/foam_dart.dm:1
public partial class obj_projectile_bullet_foam_dart : obj_projectile_bullet
{
    // tg: code/modules/projectiles/projectile/bullets/foam_dart.dm:24
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignals(this, Dm.list(COMSIG_PROJECTILE_ON_SPAWN_DROP, COMSIG_PROJECTILE_ON_SPAWN_EMBEDDED), nameof(handle_drop));
        return dot;
    }

    // tg: code/modules/projectiles/projectile/bullets/foam_dart.dm:28
    [SignalHandler]
    public virtual void handle_drop(datum source = null, obj_item_ammo_casing_foam_dart newcasing = null)
    {
        newcasing.modified = modified;
        newcasing.update_appearance();
        var newdart = (obj_projectile_bullet_foam_dart)newcasing.loaded_projectile;
        newdart.modified = modified;
        newdart.damage_type = damage_type;
        newdart.update_appearance();
    }
}
