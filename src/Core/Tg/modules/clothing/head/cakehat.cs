// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/head/cakehat.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CL1 (D333): the whole file (the birthday cake hat lit like a hard hat's lamp: it burns what it hits and heats the air around
// it; the energy cake's saber sounds). The var blocks are generated (D111).

// tg: code/modules/clothing/head/cakehat.dm:1
public partial class obj_item_clothing_head_utility_hardhat_cakehat : obj_item_clothing_head_utility_hardhat
{
    // tg: code/modules/clothing/head/cakehat.dm:31
    [DmParams()]
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        atom location = loc;
        if (ishuman(location))
        {
            mob_living_carbon_human wearer = (mob_living_carbon_human)location;
            if (Dm.truthy(wearer.is_holding(this)) || wearer.head == this)
                location = wearer.loc;
        }

        if (isturf(location))
            ((turf)location).hotspot_expose(700, 1);
        return null;
    }

    // tg: code/modules/clothing/head/cakehat.dm:41
    public override async DmTask turn_on(mob user = null)
    {
        await base.turn_on(user);
        force = Dm.num(force_on);
        throwforce = Dm.num(throwforce_on);
        damtype = damtype_on;
        hitsound = hitsound_on;
        START_PROCESSING(SSobj, this);
    }

    // tg: code/modules/clothing/head/cakehat.dm:49
    public override async DmTask turn_off(mob user = null)
    {
        await base.turn_off(user);
        force = 0;
        throwforce = 0;
        damtype = BRUTE;
        hitsound = hitsound_off;
        STOP_PROCESSING(SSobj, this);
    }

    // tg: code/modules/clothing/head/cakehat.dm:57
    public override object get_temperature()
    {
        return on * heat;
    }
}

// tg: code/modules/clothing/head/cakehat.dm:60
public partial class obj_item_clothing_head_utility_hardhat_cakehat_energycake : obj_item_clothing_head_utility_hardhat_cakehat
{
    // tg: code/modules/clothing/head/cakehat.dm:74
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        //the compiled icon state is how it appears when it's on.
        //That's how we want it to show on orbies (little virtual PDA pets).
        //However we should reset their appearance on runtime.
        update_appearance(UPDATE_ICON_STATE);
        return dot;
    }

    // tg: code/modules/clothing/head/cakehat.dm:81
    public override async DmTask turn_on(mob user = null)
    {
        playsound(this, Dm.resource("sound/items/weapons/saberon.ogg"), 5, TRUE);
        to_chat(user, span_warning(I18n.T("You turn on \\the [].", this)));
        await base.turn_on(user);
    }

    // tg: code/modules/clothing/head/cakehat.dm:86
    public override async DmTask turn_off(mob user = null)
    {
        playsound(this, Dm.resource("sound/items/weapons/saberoff.ogg"), 5, TRUE);
        to_chat(user, span_warning(I18n.T("You turn off \\the [].", this)));
        await base.turn_off(user);
    }
}
