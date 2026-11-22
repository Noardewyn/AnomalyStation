// Translated from tgstation (snapshot 1d99bd7f): code/modules/events/holiday/halloween.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R3 (D327): the whole file: the Halloween event (a treat bag in every backpack, Ian under a bedsheet, Poly's ghost), the
// spooky cookies and the trick-or-treat bag. The var blocks are generated (D111).

// tg: code/modules/events/holiday/halloween.dm:11
public partial class datum_round_event_spooky : datum_round_event
{
    // tg: code/modules/events/holiday/halloween.dm:11
    public override async DmTask<object> start()
    {
        await base.start();
        foreach (object i in Dm.each(GLOB.human_list))
        {
            var H = (mob_living_carbon_human)i;
            var b = Dm.locate<obj_item_storage_backpack>(H.contents);
            if (b != null)
                Dm.@new<obj_item_storage_spooky>(b);
        }

        foreach (mob_living_basic_pet_dog_corgi_ian Ian in Dm.each<mob_living_basic_pet_dog_corgi_ian>(GLOB.mob_living_list))
            await Ian.place_on_head(Dm.@new<obj_item_bedsheet>(Ian));
        foreach (mob_living_basic_parrot_poly bird in Dm.each<mob_living_basic_parrot_poly>(GLOB.mob_living_list))
        {
            Dm.@new<mob_living_basic_parrot_poly_ghost>(bird.loc);
            qdel(bird);
        }
        return null;
    }

    // tg: code/modules/events/holiday/halloween.dm:25
    public override async DmTask announce__proc(object fake = null)
    {
        priority_announce(pick(I18n.T("RATTLE ME BONES!"), I18n.T("THE RIDE NEVER ENDS!"), I18n.T("A SKELETON POPS OUT!"), I18n.T("SPOOKY SCARY SKELETONS!"), I18n.T("CREWMEMBERS BEWARE, YOU'RE IN FOR A SCARE!")), I18n.T("THE CALL IS COMING FROM INSIDE THE HOUSE"));
        await DmTask.CompletedTask;
    }
}

//spooky foods (you can't actually make these when it's not halloween)
// tg: code/modules/events/holiday/halloween.dm:29
public partial class obj_item_food_cookie_sugar_spookyskull : obj_item_food_cookie_sugar
{
    // tg: code/modules/events/holiday/halloween.dm:36
    [DmParams("mapload", "seasonal_changes")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        // Changes default parameter of seasonal_changes to FALSE, pass to parent
        object seasonal_changes = Dm.arg(__extra, 0) ?? FALSE;
        return base.Initialize(mapload, seasonal_changes);
    }
}

// tg: code/modules/events/holiday/halloween.dm:40
public partial class obj_item_food_cookie_sugar_spookycoffin : obj_item_food_cookie_sugar
{
    // tg: code/modules/events/holiday/halloween.dm:47
    [DmParams("mapload", "seasonal_changes")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        // Changes default parameter of seasonal_changes to FALSE, pass to parent
        object seasonal_changes = Dm.arg(__extra, 0) ?? FALSE;
        return base.Initialize(mapload, seasonal_changes);
    }
}

//spooky items

// tg: code/modules/events/holiday/halloween.dm:53
public partial class obj_item_storage_spooky : obj_item_storage
{
    // tg: code/modules/events/holiday/halloween.dm:59
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        for (float distrobuteinbag = 0; distrobuteinbag <= 5; distrobuteinbag++)
        {
            object type = pick(typeof(obj_item_food_cookie_sugar_spookyskull),
                typeof(obj_item_food_cookie_sugar_spookycoffin),
                typeof(obj_item_food_candy_corn),
                typeof(obj_item_food_candy),
                typeof(obj_item_food_candiedapple),
                typeof(obj_item_food_chocolatebar),
                typeof(obj_item_organ_brain)); // OH GOD THIS ISN'T CANDY!
            Dm.@new((Type)type, this);
        }
        return dot;
    }
}
