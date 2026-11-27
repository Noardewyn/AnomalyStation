// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/bounties/assistant.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4c (D298): the whole file; the assistant bounties are var blocks (generated) but the toolbox's and the fish's procs. The fish
// bounties read the fish and their cases.

// tg: code/modules/cargo/bounties/assistant.dm:43
public partial class datum_bounty_item_assistant_toolbox : datum_bounty_item_assistant
{
    /// List of tools that we want to see sorted into a toolbox
    // tg: code/modules/cargo/bounties/assistant.dm:49  var/static/list/static_packing_list (a type static, rules 10.6)
    private static DmList static_packing_list => TgState.Current.datum_bounty_item_assistant_toolbox__static_packing_list ??= Dm.list(
        typeof(obj_item_screwdriver),
        typeof(obj_item_wrench),
        typeof(obj_item_weldingtool),
        typeof(obj_item_crowbar),
        typeof(obj_item_analyzer),
        typeof(obj_item_wirecutters));

    // tg: code/modules/cargo/bounties/assistant.dm:58
    public override object applies_to(obj shipped = null)
    {
        DmList packing_list = static_packing_list.Copy();
        foreach (obj item_contents in Dm.each_cast<obj>(shipped.contents.Copy()))
        {
            foreach (object match_type in Dm.each(packing_list.Copy()))
            {
                if (Dm.istype(item_contents, match_type as Type))
                {
                    Dm.remove(ref packing_list, match_type);
                    break;
                }
            }
            if (!Dm.truthy(length(packing_list)))
                return base.applies_to(shipped);
        }
        return FALSE;
    }

    // tg: code/modules/cargo/bounties/assistant.dm:69
    public override async DmTask<object> ship(obj shipped = null)
    {
        object dot = await base.ship(shipped);
        foreach (obj @object in Dm.each_cast<obj>(shipped.contents.Copy()))
        {
            if (!Dm.truthy(is_type_in_list(@object, static_packing_list)))
                await @object.forceMove(shipped.drop_location() as atom);
        }
        return dot;
    }
}

// tg: code/modules/cargo/bounties/assistant.dm:233
public partial class datum_bounty_item_assistant_fish : datum_bounty_item_assistant
{
    // tg: code/modules/cargo/bounties/assistant.dm:242
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        shipping_penalty = Dm.num(reward) * 0.5f / required_count;
    }

    // tg: code/modules/cargo/bounties/assistant.dm:246
    public override object applies_to(obj shipped = null)
    {
        object dot = base.applies_to(shipped);
        if (!Dm.truthy(dot))
            return dot;
        var fishie = shipped as obj_item_fish;
        if (shipped is obj_item_storage_fish_case)
        {
            fishie = Dm.locate<obj_item_fish>(shipped);
            if (fishie == null || !Dm.truthy(is_type_in_typecache(fishie, wanted_types)))
                return FALSE;
        }
        return can_ship_fish(fishie);
    }

    // tg: code/modules/cargo/bounties/assistant.dm:257
    public virtual object can_ship_fish(obj_item_fish fishie = null) => TRUE;

    // tg: code/modules/cargo/bounties/assistant.dm:260
    public override async DmTask<object> ship(obj shipped = null)
    {
        object dot = await base.ship(shipped);
        if (!Dm.truthy(dot))
            return dot;
        var fishie = shipped as obj_item_fish;
        if (shipped is obj_item_storage_fish_case)
            fishie = Dm.locate<obj_item_fish>(shipped);
        if (Dm.eq(fishie.status, FISH_DEAD) || HAS_TRAIT(fishie, TRAIT_FISH_LOW_PRICE))
            reward = Dm.num(reward) - Dm.num(shipping_penalty);
        return dot;
    }
}

// tg: code/modules/cargo/bounties/assistant.dm:271
public partial class datum_bounty_item_assistant_fish_fluid : datum_bounty_item_assistant_fish
{
    // tg: code/modules/cargo/bounties/assistant.dm:276
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        fluid_type = pick(AQUARIUM_FLUID_FRESHWATER, AQUARIUM_FLUID_SALTWATER, AQUARIUM_FLUID_SULPHWATEVER);
        name = I18n.T("[] Fish", fluid_type);
        description = I18n.T("We need [] fish to populate our aquariums with. Fishes that are dead or bought from cargo will only be paid half as much.", LOWER_TEXT(fluid_type));
    }

    // tg: code/modules/cargo/bounties/assistant.dm:282
    public override object can_ship_fish(obj_item_fish fishie = null)
        => Dm.@in(fluid_type, GLOB.fish_compatible_fluid_types[fishie.required_fluid_type]) ? TRUE : FALSE;
}

public sealed partial class TgState
{
    // tg: code/modules/cargo/bounties/assistant.dm:49  var/static/list/static_packing_list
    internal DmList datum_bounty_item_assistant_toolbox__static_packing_list;
}
