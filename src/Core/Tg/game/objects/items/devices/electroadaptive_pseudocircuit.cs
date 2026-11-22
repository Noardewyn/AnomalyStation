// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/devices/electroadaptive_pseudocircuit.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

//Used by engineering cyborgs in place of generic circuits.
// tg: code/game/objects/items/devices/electroadaptive_pseudocircuit.dm:5
public partial class obj_item_electroadaptive_pseudocircuit : obj_item
{
    // tg: code/game/objects/items/devices/electroadaptive_pseudocircuit.dm:1
    private const float MIN_ENERGY_COST = 0.01f * STANDARD_CELL_CHARGE;
    // tg: code/game/objects/items/devices/electroadaptive_pseudocircuit.dm:2
    private const float MAX_ENERGY_COST = 0.5f * STANDARD_CELL_CHARGE;

    // tg: code/game/objects/items/devices/electroadaptive_pseudocircuit.dm:21
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        maptext = MAPTEXT(circuits);
        return dot;
    }

    // tg: code/game/objects/items/devices/electroadaptive_pseudocircuit.dm:25
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (iscyborg(user))
        {
            Dm.append(ref dot, Dm.text("[]\n[]\n[]",
                span_notice(I18n.T("It has material for <b>[]</b> circuit[]. Use the pseudocircuit on existing circuits to gain material.", circuits, circuits == 1 ? "" : "s")),
                span_notice(I18n.T("Serves as a substitute for <b>fire/air alarm</b>, <b>firelock</b>, and <b>APC</b> electronics.")),
                span_notice(I18n.T("It can also be used on an APC with no power cell to <b>fabricate a low-capacity cell</b> at a high power cost."))));
        }
        return dot;
    }

    // tg: code/game/objects/items/devices/electroadaptive_pseudocircuit.dm:32
    public virtual async DmTask<object> adapt_circuit(mob_living_silicon_robot R = null, float? circuit_cost = null)
    {
        circuit_cost ??= 0;
        if (QDELETED(R) || !istype(R, typeof(mob_living_silicon_robot)))
            return null;
        if (R.cell == null)
        {
            to_chat(R, span_warning(I18n.T("You need a power cell installed for that.")));
            return null;
        }
        if (Dm.truthy(recharging))
        {
            to_chat(R, span_warning(I18n.T("[] needs some time to recharge first.", this)));
            return null;
        }
        if (!Dm.truthy(await ((obj_item_stock_parts_power_store)(object)R.cell).use(circuit_cost)))
        {
            to_chat(R, span_warning(I18n.T("You don't have the energy for that (you need [].)", display_energy(circuit_cost))));
            return null;
        }
        if (!Dm.truthy(circuits))
        {
            to_chat(R, span_warning(I18n.T("You need more material. Use [] on existing simple circuits to break them down.", this)));
            return null;
        }
        playsound(R, "sound/items/tools/rped.ogg", 50, TRUE);
        recharging = TRUE;
        circuits--;
        maptext = MAPTEXT(circuits);
        icon_state = Dm.text("[]_recharging", Dm.initial(this, (obj_item_electroadaptive_pseudocircuit p) => p.icon_state));
        float recharge_time = ((float)circuit_cost - MIN_ENERGY_COST) / (MAX_ENERGY_COST - MIN_ENERGY_COST);
        recharge_time = clamp(recharge_time, 0, 1);
        recharge_time = (5 * SECONDS) + (55 * SECONDS) * recharge_time; //anywhere between 5 seconds to 1 minute
        _addtimer(Dm.@new<datum_callback>(this, nameof(recharge)), ROUND_UP(recharge_time), file: "code/game/objects/items/devices/electroadaptive_pseudocircuit.dm", line: 55);
        return TRUE; //The actual circuit magic itself is done on a per-object basis
    }

    // tg: code/game/objects/items/devices/electroadaptive_pseudocircuit.dm:58
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        // tg: :12  var/static/recycleable_circuits (a type-level static, rules 10.6)
        TgState state = TgState.Current;
        state.obj_item_electroadaptive_pseudocircuit__recycleable_circuits ??= typecacheof(Dm.list(
            typeof(obj_item_electronics_firelock),
            typeof(obj_item_electronics_airalarm),
            typeof(obj_item_electronics_firealarm),
            typeof(obj_item_electronics_apc))) as DmList; //A typecache of circuits consumable for material
        if (!Dm.truthy(is_type_in_typecache(interacting_with, state.obj_item_electroadaptive_pseudocircuit__recycleable_circuits)))
            return NONE;
        circuits++;
        maptext = MAPTEXT(circuits);
        user.visible_message(
            span_notice(I18n.T("User breaks down [] with [].", interacting_with, this)),
            span_notice(I18n.T("You recycle [] into []. It now has material for <b>[]</b> circuits.", interacting_with, this, circuits)));
        playsound(user, "sound/items/deconstruct.ogg", 50, TRUE);
        qdel(interacting_with);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/items/devices/electroadaptive_pseudocircuit.dm:70
    public virtual void recharge()
    {
        playsound(this, "sound/machines/chime.ogg", 25, TRUE);
        recharging = FALSE;
        icon_state = Dm.initial(this, (obj_item_electroadaptive_pseudocircuit p) => p.icon_state);
    }
}

public sealed partial class TgState
{
    // tg: code/game/objects/items/devices/electroadaptive_pseudocircuit.dm:12
    internal DmList obj_item_electroadaptive_pseudocircuit__recycleable_circuits;
}
