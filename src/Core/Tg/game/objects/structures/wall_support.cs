// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/wall_support.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The M5 leftovers (2026-10-16): /obj/structure/wall_support is live, the whole file. Rods on a wall build it (walls.dm:248,
// translated in M5 slice 4) and the wall-mounted things stay on the wall while it is taken apart (atom_mounted.dm:52, :76,
// translated with the wall-mounted frames, D148). The var block is generated.

/**
 * Wall supports are designed to help you safely deconstruct walls, without fear
 * of all wall-mounted equipment like APCs or light fixtures falling off.
 * All of the logic for handling wall-mounted equipment is in `atom_mounted.dm`
 * (/datum/component/atom_mounted)
**/
// tg: code/game/objects/structures/wall_support.dm:7
public partial class obj_structure_wall_support : obj_structure
{
    // tg: code/game/objects/structures/wall_support.dm:26
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        register_context();
        return dot;
    }

    // tg: code/game/objects/structures/wall_support.dm:30
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(Dm.bitand(resistance_flags, INDESTRUCTIBLE)))
            return dot;
        if (Dm.truthy(anchored))
            Dm.append(ref dot, span_notice(I18n.T("It's secured in place with []. The rods look like they could be [] through.", EXAMINE_HINT(I18n.T("screws")), EXAMINE_HINT(I18n.T("cut")))));
        else
            Dm.append(ref dot, span_notice(I18n.T("The anchoring screws are []. The rods look like they could be [] through.", EXAMINE_HINT(I18n.T("unscrewed")), EXAMINE_HINT(I18n.T("cut")))));
        return dot;
    }

    // tg: code/game/objects/structures/wall_support.dm:39
    public override object add_context(atom source = null, DmList context = null, obj_item held_item = null, mob user = null)
    {
        object dot = base.add_context(source, context, held_item, user);
        if (Dm.eq(held_item?.tool_behaviour, TOOL_WIRECUTTER))
        {
            context[SCREENTIP_CONTEXT_RMB] = "Deconstruct";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        if (Dm.eq(held_item?.tool_behaviour, TOOL_SCREWDRIVER))
        {
            context[SCREENTIP_CONTEXT_LMB] = Dm.truthy(anchored) ? "Unanchor" : "Anchor";
            return CONTEXTUAL_SCREENTIP_SET;
        }
        return dot;
    }

    // tg: code/game/objects/structures/wall_support.dm:49
    public override async DmTask<object> wirecutter_act_secondary(mob_living user = null, obj_item tool = null)
    {
        // tg: code/game/objects/structures/wall_support.dm:50
        add_fingerprint(user);
        tool.play_tool_sound(this, 100);
        await deconstruct(TRUE);
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/wall_support.dm:55
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        if (!isturf(loc))
            return FALSE;
        // tg: code/game/objects/structures/wall_support.dm:58
        add_fingerprint(user);
        if (!Dm.truthy(await tool.use_tool(this, user, 0, volume: 100)))
            return FALSE;
        await set_anchored(Dm.not(anchored));
        user.visible_message(span_notice(I18n.T("[] [] [].", user, Dm.truthy(anchored) ? "fastens" : "unfastens", this)),
            span_notice(Dm.truthy(anchored) ? I18n.T("You fasten [] to the floor.", this) : I18n.T("You unfasten [] from the floor.", this)));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/objects/structures/wall_support.dm:66
    public override object play_attack_sound(object damage_amount = null, string damage_type = null, object damage_flag = null, params object[] __extra)
        => obj_structure_grille.grille_play_attack_sound(this, damage_amount, damage_type);

    // tg: code/game/objects/structures/wall_support.dm:69
    public override async DmTask atom_deconstruct(float? disassembled = null)
    {
        atom rods = Dm.@new(rods_type, drop_location(), rods_amount) as atom; // var/obj/rods
        // tg: code/game/objects/structures/wall_support.dm:71
        transfer_fingerprints_to(rods);
    }

    // tg: code/game/objects/structures/wall_support.dm:73
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        object dot = await base.atom_break(damage_flag);
        if (Dm.truthy(broken))
            return dot;
        atom_integrity = 20;
        broken = TRUE;
        rods_amount = 1;
        atom dropped_rods = Dm.@new(rods_type, drop_location(), rods_amount) as atom; // var/obj/item/dropped_rods
        // tg: code/game/objects/structures/wall_support.dm:81
        transfer_fingerprints_to(dropped_rods);
        update_appearance();
        return dot;
    }

    // tg: code/game/objects/structures/wall_support.dm:84 (tg adds no atmos_sensitive element, so nothing calls these two)
    public override object should_atmos_process(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        return Dm.num(Dm.gt(exposed_temperature, T0C + 1500) && !Dm.truthy(broken));
    }

    // tg: code/game/objects/structures/wall_support.dm:87
    public override async DmTask<object> atmos_expose(datum_gas_mixture air = null, object exposed_temperature = null)
    {
        await take_damage(1, BURN, 0, 0);
        return null;
    }

}
