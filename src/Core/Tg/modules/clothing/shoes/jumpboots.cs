// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/shoes/jumpboots.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps CL1 (D333): the whole file (the jump boots' dash through the air with tg's sound and their recharge; their pockets; the jet
// boots' active state). The var blocks are generated (D111).

// tg: code/modules/clothing/shoes/jumpboots.dm:1
public partial class obj_item_clothing_shoes_bhop : obj_item_clothing_shoes
{
    // tg: code/modules/clothing/shoes/jumpboots.dm:18
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);

        Waiting.NoSleep(create_storage(storage_type: typeof(datum_storage_pockets_shoes)));
        return dot;
    }

    // tg: code/modules/clothing/shoes/jumpboots.dm:23
    [DmParams("user", "action")]
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        if (!isliving(user))
            return null;

        if (recharging_time > world.time)
        {
            to_chat(user, span_warning(I18n.T("The boot's internal propulsion needs to recharge still!")));
            return null;
        }

        atom target = get_edge_target_turf(user, user.dir) as atom; //gets the user's direction

        ADD_TRAIT(user, TRAIT_MOVE_FLOATING, LEAPING_TRAIT); //Throwing itself doesn't protect mobs against lava (because gulag).
        if (Dm.truthy(await user.throw_at(target, jumpdistance, jumpspeed, spin: FALSE, diagonals_first: TRUE, callback: TRAIT_CALLBACK_REMOVE(user, TRAIT_MOVE_FLOATING, LEAPING_TRAIT))))
        {
            playsound(this, Dm.resource("sound/effects/stealthoff.ogg"), 50, TRUE, TRUE);
            user.visible_message(span_warning(I18n.T("[] dashes forward into the air!", usr)));
            recharging_time = world.time + recharging_rate;
        }
        else
        {
            REMOVE_TRAIT(user, TRAIT_MOVE_FLOATING, LEAPING_TRAIT);
            to_chat(user, span_warning(I18n.T("Something prevents you from dashing forward!")));
        }
        return null;
    }
}

// tg: code/modules/clothing/shoes/jumpboots.dm:52
public partial class obj_item_clothing_shoes_bhop_rocket_jet : obj_item_clothing_shoes_bhop_rocket
{
    // tg: code/modules/clothing/shoes/jumpboots.dm:60
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        if (Dm.truthy(flight_active))
            icon_state = "jetboots_active";
        else
            icon_state = "jetboots";
        return await base.update_icon_state(__extra);
    }
}
