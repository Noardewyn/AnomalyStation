// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/open/ice.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The ice sheet (Lavaland's winter biodome): permanently slippery, and a shovel or a pick digs a fishing hole in it (a
// fishing spot). The var blocks are generated.

// tg: code/game/turfs/open/ice.dm:2
public partial class turf_open_misc_ice : turf_open_misc
{
    // tg: code/game/turfs/open/ice.dm:19 (var/static/list/tool_screentips)
    private static DmList tool_screentips => TgState.Current.turf_open_misc_ice__tool_screentips ??= Dm.list(
        Dm.kv(TOOL_SHOVEL, Dm.list(Dm.kv(SCREENTIP_CONTEXT_LMB, "Dig fishing hole"))),
        Dm.kv(TOOL_MINING, Dm.list(Dm.kv(SCREENTIP_CONTEXT_LMB, "Dig fishing hole"))));

    // tg: code/game/turfs/open/ice.dm:28
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(MakeSlippery(TURF_WET_PERMAFROST, float.PositiveInfinity, 0, float.PositiveInfinity, TRUE, FALSE));
        if (Dm.truthy(can_make_hole))
            Waiting.NoSleep(AddElement(typeof(datum_element_contextual_screentip_tools), tool_screentips));
        return dot;
    }

    // tg: code/game/turfs/open/ice.dm:34
    public override object break_tile() => null;

    // tg: code/game/turfs/open/ice.dm:37
    public override object burn_tile() => null;

    // tg: code/game/turfs/open/ice.dm:40
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        if (Dm.truthy(can_make_hole))
            Dm.append(ref dot, span_info(I18n.T("You could use a [] or a [] to dig a fishing hole here.", EXAMINE_HINT("shovel"), EXAMINE_HINT("pick"))));
        return dot;
    }

    // tg: code/game/turfs/open/ice.dm:45
    [DmParams("animal", "modifiers")]
    public override async DmTask<object> attack_animal(mob user = null, DmList modifiers = null)
    {
        object dot = await base.attack_animal(user, modifiers);
        if (HAS_TRAIT(user, TRAIT_MOB_CAN_DIG))
            await dig_hole(user as mob_living);
        return dot;
    }

    // tg: code/game/turfs/open/ice.dm:50
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.eq(tool.tool_behaviour, TOOL_SHOVEL) && !Dm.eq(tool.tool_behaviour, TOOL_MINING))
            return await base.item_interaction(user, tool, modifiers, __extra);

        return Dm.truthy(await dig_hole(user)) ? ITEM_INTERACT_SUCCESS : NONE;
    }

    // tg: code/game/turfs/open/ice.dm:56
    public virtual async DmTask<object> dig_hole(mob_living user = null)
    {
        if (!Dm.truthy(can_make_hole))
            return FALSE;
        balloon_alert(user, I18n.T("digging..."));
        playsound(this, Dm.resource("sound/effects/shovel_dig.ogg"), 50, TRUE);
        if (!Dm.truthy(await do_after(user, 5 * SECONDS, this)))
            return FALSE;
        balloon_alert(user, I18n.T("dug hole"));
        spawn_hole();
        return TRUE;
    }

    // tg: code/game/turfs/open/ice.dm:67
    public virtual void spawn_hole()
    {
        Waiting.NoSleep(AddComponent(typeof(datum_component_fishing_spot), GLOB.preset_fish_sources[typeof(datum_fish_source_ice_fishing)]));
        ADD_TRAIT(this, TRAIT_CATCH_AND_RELEASE, INNATE_TRAIT);
        add_overlay(mutable_appearance(Dm.resource("icons/turf/overlays.dmi"), "ice_hole"));
        can_make_hole = FALSE;
        RemoveElement(typeof(datum_element_contextual_screentip_tools), tool_screentips);
        Dm.mask(ref flags_1, Dm.bitnot(HAS_CONTEXTUAL_SCREENTIPS_1));
    }
}

public sealed partial class TgState
{
    internal DmList turf_open_misc_ice__tool_screentips;
}
