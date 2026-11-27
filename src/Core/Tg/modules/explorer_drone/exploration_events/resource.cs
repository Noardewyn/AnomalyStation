// Translated from tgstation (snapshot 1d99bd7f): code/modules/explorer_drone/exploration_events/resource.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the resources a drone finds, which its tool retrieves after a delay into its cargo (an object or one of the
// adventure loot generators). The var blocks are generated (D111).

/// Simple event type that checks if you have a tool and after a retrieval delay adds loot to drone.
// tg: code/modules/explorer_drone/exploration_events/resource.dm:2
public partial class datum_exploration_event_simple_resource : datum_exploration_event_simple
{
    /// Description shown below image
    // tg: code/modules/explorer_drone/exploration_events/resource.dm:23
    public override object get_description(obj_item_exodrone drone = null)
    {
        object dot = base.get_description(drone);
        DmList desc_list = Dm.list(dot);
        if (!Dm.truthy(required_tool) || Dm.truthy(drone.has_tool(required_tool)))
            Dm.append(ref desc_list, has_tool_description);
        else
            Dm.append(ref desc_list, no_tool_description);
        return desc_list.Join("\n");
    }

    // tg: code/modules/explorer_drone/exploration_events/resource.dm:32
    public override object action_enabled(obj_item_exodrone drone = null)
    {
        return Dm.num(Dm.num(amount) > 0 && (!Dm.truthy(required_tool) || Dm.truthy(drone.has_tool(required_tool))));
    }

    // tg: code/modules/explorer_drone/exploration_events/resource.dm:35
    public override async DmTask fire(obj_item_exodrone drone = null)
    {
        if (!Dm.truthy(action_enabled(drone))) //someone used it up or we lost the tool while we were looking at ui
        {
            end(); // tg passes no drone here
            return;
        }
        amount--;
        if (delay > 0)
        {
            drone.set_busy(delay_message, delay);
            _addtimer(Dm.@new<datum_callback>(this, nameof(delay_finished), WEAKREF(drone)), delay, file: "code/modules/explorer_drone/exploration_events/resource.dm", line: 43);
        }
        else
            await finish_event(drone);
    }

    // tg: code/modules/explorer_drone/exploration_events/resource.dm:46
    public override object is_targetable()
    {
        return Dm.num(Dm.truthy(visited) && Dm.num(amount) > 0); ///Can go back if something is left.
    }

    // tg: code/modules/explorer_drone/exploration_events/resource.dm:49
    public virtual async DmTask delay_finished(datum_weakref drone_ref = null)
    {
        obj_item_exodrone drone = drone_ref.resolve() as obj_item_exodrone;
        if (QDELETED(drone)) //drone blown up in the meantime
            return;
        drone.unset_busy(EXODRONE_EXPLORATION);
        await finish_event(drone);
    }

    // tg: code/modules/explorer_drone/exploration_events/resource.dm:56
    public virtual async DmTask finish_event(obj_item_exodrone drone = null)
    {
        drone.drone_log__proc(success_log);
        await dispense_loot(drone);
        end(drone);
    }

    // tg: code/modules/explorer_drone/exploration_events/resource.dm:61
    public virtual async DmTask dispense_loot(obj_item_exodrone drone = null)
    {
        if (ispath(loot_type, typeof(datum_adventure_loot_generator)))
        {
            datum_adventure_loot_generator generator = await Dm.new_async(loot_type) as datum_adventure_loot_generator;
            await generator.transfer_loot(drone);
        }
        else
        {
            obj loot = await Dm.new_async(loot_type) as obj;
            await drone.try_transfer(loot);
        }
    }
}

// tg: code/modules/explorer_drone/exploration_events/resource.dm:237
public partial class datum_exploration_event_simple_resource_welded_locker : datum_exploration_event_simple_resource
{
    // tg: code/modules/explorer_drone/exploration_events/resource.dm:249
    public override async DmTask dispense_loot(obj_item_exodrone drone = null)
    {
        mob_living_carbon_human head_species_source = await Dm.new_async<mob_living_carbon_human>();
        head_species_source.set_species(typeof(datum_species_skeleton));
        head_species_source.real_name = "spaced locker victim";
        obj_item_bodypart_head skeleton_head = head_species_source.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
        await skeleton_head.drop_limb(FALSE);
        qdel(head_species_source);
        await drone.try_transfer(skeleton_head);
    }
}

// tg: code/modules/explorer_drone/exploration_events/resource.dm:273
public partial class datum_exploration_event_simple_resource_mineral_deposit : datum_exploration_event_simple_resource
{
    // tg: code/modules/explorer_drone/exploration_events/resource.dm:285  var/static/list/possible_materials
    public static ref DmList possible_materials => ref TgState.Current.datum_exploration_event_simple_resource_mineral_deposit__possible_materials;

    // tg: code/modules/explorer_drone/exploration_events/resource.dm:289
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        chosen_material_type = pick(possible_materials ??= Dm.list(typeof(datum_material_silver), typeof(datum_material_bananium), typeof(datum_material_pizza))); //only add materials with sheet type here
        datum_material chosen_mat = await SSmaterials.get_material(chosen_material_type) as datum_material;
        name = Dm.text("[] Deposit", chosen_mat.name);
        discovery_log = I18n.T("Discovered a sizeable [] deposit", chosen_mat.name);
        success_log = I18n.T("Extracted [].", chosen_mat.name);
        description = I18n.T("You locate a rich surface deposit of [].", chosen_mat.name);
    }

    // tg: code/modules/explorer_drone/exploration_events/resource.dm:298
    public override async DmTask dispense_loot(obj_item_exodrone drone = null)
    {
        datum_material chosen_mat = await SSmaterials.get_material(chosen_material_type) as datum_material;
        // tg passes the amount as the sheet's first argument, its loc (BYOND makes it at no loc and the stack keeps its own amount)
        obj loot = await Dm.new_async(chosen_mat.sheet_type, loot_amount) as obj;
        await drone.try_transfer(loot);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/explorer_drone/exploration_events/resource.dm:285  var/static/list/possible_materials
    internal DmList datum_exploration_event_simple_resource_mineral_deposit__possible_materials;
}
