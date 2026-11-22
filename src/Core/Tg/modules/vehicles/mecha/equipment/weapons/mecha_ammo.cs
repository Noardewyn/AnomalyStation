// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/mecha/equipment/weapons/mecha_ammo.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/vehicles/mecha/equipment/weapons/mecha_ammo.dm:1
public partial class obj_item_mecha_ammo : obj_item
{
    // tg: code/modules/vehicles/mecha/equipment/weapons/mecha_ammo.dm:16
    public override object update_name(float? updates = null)
    {
        object dot = base.update_name(updates);
        name = Dm.text("[][]", Dm.truthy(rounds) ? null : "empty ", Dm.initial(this, "name"));
        return dot;
    }

    // tg: code/modules/vehicles/mecha/equipment/weapons/mecha_ammo.dm:20
    public override object update_desc(float? updates = null)
    {
        object dot = base.update_desc(updates);
        desc = Dm.truthy(rounds) ? Dm.initial(this, "desc") as string : "An exosuit ammuniton box that has since been emptied. It can be safely folded for recycling.";
        return dot;
    }

    // tg: code/modules/vehicles/mecha/equipment/weapons/mecha_ammo.dm:24
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.truthy(rounds) ? Dm.initial(this, "icon_state") as string : Dm.text("[]_e", Dm.initial(this, "icon_state"));
        return await base.update_icon_state(__extra);
    }

    // tg: code/modules/vehicles/mecha/equipment/weapons/mecha_ammo.dm:28
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(rounds))
        {
            to_chat(user, span_warning(I18n.T("You cannot flatten the ammo box until it's empty!")));
            return null;
        }

        to_chat(user, span_notice(I18n.T("You fold [] flat.", this)));
        obj_item trash = Dm.@new<obj_item_stack_sheet_iron>(user.loc);
        qdel(this);
        await user.put_in_hands(trash);
        return null;
    }

    // tg: code/modules/vehicles/mecha/equipment/weapons/mecha_ammo.dm:39
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(rounds))
            Dm.append(ref dot, I18n.T("There [] [] [][] left.", Dm.num(rounds) > 1 ? "are" : "is", rounds, ammo_type, Dm.num(rounds) > 1 ? "s" : ""));
        else
            Dm.append(ref dot, span_notice(I18n.T("Use in-hand to fold it into a sheet of iron.")));
        return dot;
    }
}
