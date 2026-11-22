// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/secway.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SE1 (D333, the security gear): the whole file: the secway, ridden with the security key (its riding component in
// riding_vehicle.cs), smoking while broken, welded back to health, jammed by a banana stuffed into its tailpipe, exploding when
// destroyed, and its riders hit by 60% of the bullets fired at it. The var blocks are generated (D111).

// tg: code/modules/vehicles/secway.dm:1
public partial class obj_vehicle_ridden_secway : obj_vehicle_ridden
{
    // tg: code/modules/vehicles/secway.dm:20
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_ridable), typeof(datum_component_riding_vehicle_secway)));
        return dot;
    }

    // tg: code/modules/vehicles/secway.dm:24
    public override async DmTask<object> atom_break(object damage_flag = null)
    {
        START_PROCESSING(SSobj, this);
        return await base.atom_break(damage_flag);
    }

    // tg: code/modules/vehicles/secway.dm:28
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.num(atom_integrity) >= integrity_failure * max_integrity)
            return PROCESS_KILL;
        if (SPT_PROB(10, seconds_per_tick))
            return null;
        Waiting.NoSleep(do_smoke(0, this, this));
        return null;
    }

    // tg: code/modules/vehicles/secway.dm:35
    public override async DmTask<object> welder_act(mob_living user = null, obj_item tool = null)
    {
        if (Dm.truthy(user.combat_mode))
            return NONE;

        if (Dm.truthy(DOING_INTERACTION(user, this)))
        {
            balloon_alert(user, I18n.T("you're already repairing it!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (Dm.num(atom_integrity) >= max_integrity)
        {
            balloon_alert(user, I18n.T("it's not damaged!"));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!Dm.truthy(tool.tool_start_check(user, amount: 1, heat_required: HIGH_TEMPERATURE_REQUIRED)))
            return ITEM_INTERACT_BLOCKING;

        user.balloon_alert_to_viewers(I18n.T("started welding []", this), I18n.T("started repairing []", this));
        audible_message(span_hear(I18n.T("You hear welding.")));
        bool did_the_thing = false;
        while (Dm.num(atom_integrity) < max_integrity)
        {
            if (Dm.truthy(await tool.use_tool(this, user, 2.5f * SECONDS, volume: 50)))
            {
                did_the_thing = true;
                atom_integrity = Dm.num(atom_integrity) + Dm.min(10, max_integrity - Dm.num(atom_integrity));
                audible_message(span_hear(I18n.T("You hear welding.")));
            }
            else
                break;
        }

        if (did_the_thing)
        {
            user.balloon_alert_to_viewers(I18n.T("[] repaired []", Dm.num(atom_integrity) >= max_integrity ? I18n.T("fully") : I18n.T("partially"), this));
            return ITEM_INTERACT_SUCCESS;
        }

        user.balloon_alert_to_viewers(I18n.T("stopped welding []", this), I18n.T("interrupted the repair!"));
        return ITEM_INTERACT_BLOCKING;
    }

    // tg: code/modules/vehicles/secway.dm:67
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.item_interaction(user, tool, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (tool is not obj_item_food_grown_banana)
            return NONE;
        // ignore the occupants because they're presumably too distracted to notice the guy stuffing fruit into their vehicle's exhaust. do segways have exhausts? they do now!
        user.visible_message(span_warning(I18n.T("[] begins stuffing [] into []'s tailpipe.", user, tool, this)), span_warning(I18n.T("You begin stuffing [] into []'s tailpipe...", tool, this)), ignored_mobs: occupants);
        if (!Dm.truthy(await do_after(user, 3 * SECONDS, this)))
            return ITEM_INTERACT_BLOCKING;
        if (!Dm.truthy(await user.transferItemToLoc(tool, this)))
            return ITEM_INTERACT_BLOCKING;
        user.visible_message(span_warning(I18n.T("[] stuffs [] into []'s tailpipe.", user, tool, this)), span_warning(I18n.T("You stuff [] into []'s tailpipe.", tool, this)), ignored_mobs: occupants);
        eddie_murphy = (obj_item_food_grown_banana)tool;
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/modules/vehicles/secway.dm:83
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        if (eddie_murphy == null)
            return await base.attack_hand(user, modifiers, __extra);
        user.visible_message(span_warning(I18n.T("[] begins cleaning [] out of [].", user, eddie_murphy, this)), span_warning(I18n.T("You begin cleaning [] out of []...", eddie_murphy, this)));
        if (!Dm.truthy(await do_after(user, 6 * SECONDS, target: this)))
            return await base.attack_hand(user, modifiers, __extra);
        user.visible_message(span_warning(I18n.T("[] cleans [] out of [].", user, eddie_murphy, this)), span_warning(I18n.T("You manage to get [] out of [].", eddie_murphy, this)));
        await eddie_murphy.forceMove(drop_location() as atom);
        eddie_murphy = null;
        return null;
    }

    // tg: code/modules/vehicles/secway.dm:94
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (eddie_murphy != null)
            Dm.append(ref dot, span_warning(I18n.T("Something appears to be stuck in its exhaust...")));
        return dot;
    }

    // tg: code/modules/vehicles/secway.dm:99
    public override async DmTask atom_destruction(object damage_flag = null)
    {
        await explosion(this, devastation_range: -1, light_impact_range: 2, flame_range: 3, flash_range: 4);
        await base.atom_destruction(damage_flag);
    }

    // tg: code/modules/vehicles/secway.dm:103
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }

    //bullets will have a 60% chance to hit any riders
    // tg: code/modules/vehicles/secway.dm:108
    public override async DmTask<object> projectile_hit(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, object blocked = null)
    {
        if (!Dm.truthy(buckled_mobs) || prob(40))
            return await base.projectile_hit(hitting_projectile, def_zone, piercing_hit, blocked);
        foreach (mob rider in Dm.each<mob>(buckled_mobs))
            return await rider.projectile_hit(hitting_projectile);
        return await base.projectile_hit(hitting_projectile, def_zone, piercing_hit, blocked);
    }
}
