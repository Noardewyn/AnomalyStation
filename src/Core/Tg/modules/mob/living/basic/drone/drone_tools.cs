// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/drone/drone_tools.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI7 (D326): the whole file (the built-in tools are var blocks, generated). The drone's tool storage.

// tg: code/modules/mob/living/basic/drone/drone_tools.dm:1
public partial class obj_item_storage_drone_tools : obj_item_storage
{
    // tg: code/modules/mob/living/basic/drone/drone_tools.dm:10
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NODROP, ABSTRACT_ITEM_TRAIT);
        return dot;
    }

    // tg: code/modules/mob/living/basic/drone/drone_tools.dm:15
    public override async DmTask PopulateContents()
    {
        DmList builtintools = Dm.list();
        builtintools.Add(Dm.@new<obj_item_crowbar_drone>(this));
        builtintools.Add(Dm.@new<obj_item_screwdriver_drone>(this));
        builtintools.Add(Dm.@new<obj_item_wrench_drone>(this));
        builtintools.Add(Dm.@new<obj_item_weldingtool_drone>(this));
        builtintools.Add(Dm.@new<obj_item_wirecutters_drone>(this));
        builtintools.Add(Dm.@new<obj_item_multitool_drone>(this));
        builtintools.Add(Dm.@new<obj_item_pipe_dispenser_drone>(this));
        builtintools.Add(Dm.@new<obj_item_t_scanner_drone>(this));
        builtintools.Add(Dm.@new<obj_item_analyzer_drone>(this));
        builtintools.Add(Dm.@new<obj_item_soap_drone>(this));

        foreach (obj_item tool in Dm.each<obj_item>(builtintools))
            await tool.AddComponent(typeof(datum_component_holderloving), this);
    }
}

// tg: code/modules/mob/living/basic/drone/drone_tools.dm:44
public partial class obj_item_screwdriver_drone : obj_item_screwdriver
{
    // tg: code/modules/mob/living/basic/drone/drone_tools.dm:56
    public override async DmTask<object> separate_worn_overlays(mutable_appearance standing = null, mutable_appearance draw_target = null, float? isinhands = null, object icon_file = null, float? bodyshape = null)
    {
        DmList dot = await base.separate_worn_overlays(standing, draw_target, isinhands, icon_file, bodyshape) as DmList ?? Dm.list();
        if (!Dm.truthy(isinhands))
            return dot;
        dot.Add(mutable_appearance(icon_file, "screwdriver_head", appearance_flags: RESET_COLOR));
        return dot;
    }
}
