// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/secbot/secbot_subtypes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3).

// tg: code/modules/mob/living/basic/bots/secbot/secbot_subtypes.dm:2
public partial class mob_living_basic_bot_secbot_beepsky : mob_living_basic_bot_secbot
{
    // tg: code/modules/mob/living/basic/bots/secbot/secbot_subtypes.dm:61
    public override async DmTask explode()
    {
        atom current_location = drop_location() as atom;
        Dm.@new<obj_item_stock_parts_power_store_cell_potato>(current_location);
        obj_item_reagent_containers_cup_glass_drinkingglass_shotglass drinking_oil = Dm.@new<obj_item_reagent_containers_cup_glass_drinkingglass_shotglass>(current_location);
        // the shot glass is inert until the bar's glasses (section 107): it has no reagents yet
        if (drinking_oil.reagents is datum_reagents oil_reagents)
            await oil_reagents.add_reagent(typeof(datum_reagent_consumable_ethanol_whiskey), 15);
        await base.explode();
    }
}

// tg: code/modules/mob/living/basic/bots/secbot/secbot_subtypes.dm:9
public partial class mob_living_basic_bot_secbot_beepsky_officer : mob_living_basic_bot_secbot_beepsky
{
    // tg: code/modules/mob/living/basic/bots/secbot/secbot_subtypes.dm:14
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // Beepsky hates people scanning them
        RegisterSignal(this, COMSIG_MOVABLE_SPY_STEALING, nameof(on_spy_scan));
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/secbot/secbot_subtypes.dm:19
    [SignalHandler]
    public virtual void on_spy_scan(datum source = null, mob user = null)
    {
        (ai_controller as datum_ai_controller)?.insert_blackboard_key_lazylist(BB_BASIC_MOB_RETALIATE_LIST, user);
    }
}

// tg: code/modules/mob/living/basic/bots/secbot/secbot_subtypes.dm:36
public partial class mob_living_basic_bot_secbot_beepsky_jr : mob_living_basic_bot_secbot_beepsky
{
    // tg: code/modules/mob/living/basic/bots/secbot/secbot_subtypes.dm:41
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(update_transform(0.8f));
        return dot;
    }
}
