// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/holiday/easter.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R3 (D327): the whole file: the Easter events (the egg hunt's announcement and the rabbits' release), the surprise eggs and
// their prizes. The var blocks (the baskets, the bunny suit, the foods and their recipes) are generated (D111).

// tg: code/modules/events/holiday/easter.dm:11
public partial class datum_round_event_easter : datum_round_event
{
    // tg: code/modules/events/holiday/easter.dm:11
    public override async DmTask announce__proc(object fake = null)
    {
        priority_announce(pick(I18n.T("Hip-hop into Easter!"), I18n.T("Find some Bunny's stash!"), I18n.T("Today is National 'Hunt a Wabbit' Day."), I18n.T("Be kind, give Chocolate Eggs!")));
        await DmTask.CompletedTask;
    }
}

// tg: code/modules/events/holiday/easter.dm:25
public partial class datum_round_event_rabbitrelease : datum_round_event
{
    // tg: code/modules/events/holiday/easter.dm:25
    public override async DmTask announce__proc(object fake = null)
    {
        priority_announce(I18n.T("Unidentified furry objects detected coming aboard []. Beware of Adorable-ness.", station_name()), I18n.T("Fluffy Alert"), ANNOUNCER_ALIENS);
        await DmTask.CompletedTask;
    }

    // tg: code/modules/events/holiday/easter.dm:29
    public override async DmTask<object> start()
    {
        foreach (obj_effect_landmark_event_spawn spawn_point in Dm.each_cast<obj_effect_landmark_event_spawn>(GLOB.generic_event_spawns)) //Common public bunnies
        {
            if (Dm.truthy(prob(35)))
                Dm.@new<mob_living_basic_rabbit_easter>(spawn_point.loc);
            CHECK_TICK();
        }

        foreach (obj_effect_landmark_event_spawn spawn_point in Dm.each_cast<obj_effect_landmark_event_spawn>(GLOB.generic_maintenance_landmarks)) // The rare maint bunnies
        {
            if (Dm.truthy(prob(15)))
                Dm.@new<mob_living_basic_rabbit_easter>(spawn_point.loc);
            CHECK_TICK();
        }

        foreach (obj_effect_landmark_carpspawn spawn_point in Dm.each<obj_effect_landmark_carpspawn>(GLOB.landmarks_list)) // The rare space bunnies
        {
            if (Dm.truthy(prob(15)))
                Dm.@new<mob_living_basic_rabbit_easter_space>(spawn_point.loc);
            CHECK_TICK();
        }
        await DmTask.CompletedTask;
        return null;
    }
}

//Egg prizes and egg spawns!
// tg: code/modules/events/holiday/easter.dm:84
public partial class obj_item_surprise_egg : obj_item
{
    // tg: code/modules/events/holiday/easter.dm:96
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        object eggcolor = pick("blue", "green", "mime", "orange", "purple", "rainbow", "red", "yellow");
        icon_state = Dm.text("egg-[]", eggcolor);
        return dot;
    }

    // tg: code/modules/events/holiday/easter.dm:101
    public virtual async DmTask dispensePrize(turf where = null)
    {
        object won = pick(dispensePrize__prize_list);
        Dm.@new((Type)won, where);
        Dm.@new<obj_item_food_chocolateegg>(where);
        await DmTask.CompletedTask;
    }

    // tg: code/modules/events/holiday/easter.dm:102  var/static/list/prize_list (a proc-level static, rules 10.6)
    private static ref DmList dispensePrize__prize_list => ref TgState.Current.obj_item_surprise_egg__dispensePrize__prize_list;

    // tg: code/modules/events/holiday/easter.dm:124
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        await base.attack_self(user, modifiers, __extra);
        to_chat(user, span_notice(I18n.T("You unwrap [] and find a prize inside!", this)));
        await dispensePrize(get_turf(this));
        qdel(this);
        return null;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/events/holiday/easter.dm:102  dispensePrize's var/static/list/prize_list = list(...) + subtypesof(/obj/item/toy/mecha)
    internal DmList obj_item_surprise_egg__dispensePrize__prize_list = (DmList)Dm.add(Dm.list(typeof(obj_item_clothing_head_costume_bunnyhead),
        typeof(obj_item_clothing_suit_costume_bunnysuit),
        typeof(obj_item_storage_backpack_satchel_bunnysatchel),
        typeof(obj_item_food_grown_carrotlike_carrot),
        typeof(obj_item_toy_balloon),
        typeof(obj_item_toy_gun),
        typeof(obj_item_toy_sword),
        typeof(obj_item_toy_talking_ai),
        typeof(obj_item_toy_talking_owl),
        typeof(obj_item_toy_talking_griffin),
        typeof(obj_item_toy_minimeteor),
        typeof(obj_item_toy_clockwork_watch),
        typeof(obj_item_toy_toy_xeno),
        typeof(obj_item_toy_foamblade),
        typeof(obj_item_toy_plush_carpplushie),
        typeof(obj_item_toy_redbutton),
        typeof(obj_item_toy_windup_toolbox),
        typeof(obj_item_clothing_head_collectable_rabbitears)
        ), subtypesof(typeof(obj_item_toy_mecha)));
}
