// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/divine.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the sacrificial altar (alt-click gibs the creature buckled to it) and the healing fountain (a draught of godblood,
// then dry for three minutes), which Lavaland's fountain hall places. The var blocks are generated.

// tg: code/game/objects/structures/divine.dm:1
public partial class obj_structure_sacrificealtar : obj_structure
{
    // tg: code/game/objects/structures/divine.dm:10
    public override async DmTask<object> click_alt(mob user = null, params object[] __extra)
    {
        if (!Dm.truthy(has_buckled_mobs()))
            return CLICK_ACTION_BLOCKING;
        var buckled_mob = Dm.locate<mob_living>(buckled_mobs);
        if (buckled_mob == null)
            return CLICK_ACTION_BLOCKING;
        to_chat(user, span_notice(I18n.T("Invoking the sacred ritual, you sacrifice [].", buckled_mob)));
        buckled_mob.investigate_log("has been sacrificially gibbed on an altar.", INVESTIGATE_DEATHS);
        await buckled_mob.gib(DROP_ALL_REMAINS);
        message_admins(Dm.text("[] has sacrificed [] on the sacrificial altar at [].", ADMIN_LOOKUPFLW(user), key_name_admin(buckled_mob), AREACOORD(this)));
        return CLICK_ACTION_SUCCESS;
    }
}

// tg: code/game/objects/structures/divine.dm:22
public partial class obj_structure_healingfountain : obj_structure
{
    // tg: code/game/objects/structures/divine.dm:32
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (last_process + time_between_uses > world.time)
        {
            to_chat(user, span_notice(I18n.T("The fountain appears to be empty.")));
            return dot;
        }
        last_process = world.time;
        to_chat(user, span_notice(I18n.T("The water feels warm and soothing as you touch it. The fountain immediately dries up shortly afterwards.")));
        await ((datum_reagents)user.reagents).add_reagent(typeof(datum_reagent_medicine_omnizine_godblood), 20);
        update_appearance();
        _addtimer(Dm.@new<datum_callback>(this, nameof(update_appearance)), time_between_uses, file: "code/game/objects/structures/divine.dm", line: 43);
        return dot;
    }

    // tg: code/game/objects/structures/divine.dm:46
    public override DmTask<object> update_icon_state(params object[] __extra)
    {
        if (last_process + time_between_uses > world.time)
            icon_state = "fountain";
        else
            icon_state = "fountain-red";
        return base.update_icon_state(__extra);
    }
}
