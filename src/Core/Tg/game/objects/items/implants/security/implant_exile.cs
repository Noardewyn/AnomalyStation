// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/implants/security/implant_exile.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps DV2 (D333): the file whole. The exile implant is a var block (the gateway and the mining shuttle read it); its
// anti-teleportation kind also grounds its holder (TRAIT_NO_TELEPORT). The var blocks are generated (D111).

//Exile implants will allow you to use the station gate, but not return home.
//This will allow security to exile badguys/for badguys to exile their kill targets

///Used to help the staff of the space hotel resist the urge to use the space hotel's incredibly alluring roundstart teleporter to ignore their flavor/greeting text and come to the station.
// tg: code/game/objects/items/implants/security/implant_exile.dm:23
public partial class obj_item_implant_exile_noteleport : obj_item_implant_exile
{
    // tg: code/game/objects/items/implants/security/implant_exile.dm:39
    public override async DmTask<object> implant(mob_living target = null, mob user = null, float? silent = null, float? force = null, params object[] __extra)
    {
        silent ??= FALSE;
        force ??= FALSE;
        object dot = await base.implant(target, user, silent, force, __extra);
        if (!Dm.truthy(dot) || !isliving(target))
            return FALSE;
        mob_living living_target = target;
        ADD_TRAIT(living_target, TRAIT_NO_TELEPORT, IMPLANT_TRAIT);
        return TRUE;
    }

    // tg: code/game/objects/items/implants/security/implant_exile.dm:47
    public override async DmTask<object> removed(mob_living source = null, float? silent = null, float? special = null)
    {
        mob_living target = source;
        silent ??= FALSE;
        special ??= FALSE;
        object dot = await base.removed(target, silent, special);
        if (!Dm.truthy(dot) || !isliving(target))
            return FALSE;
        mob_living living_target = target;
        REMOVE_TRAIT(living_target, TRAIT_NO_TELEPORT, IMPLANT_TRAIT);
        return TRUE;
    }
}
