// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/farm_animals/sheep.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the admin var edit (a row) (gaps package C4).

// tg: code/modules/mob/living/basic/farm_animals/sheep.dm:2
public partial class mob_living_basic_sheep : mob_living_basic
{
    // tg: code/modules/mob/living/basic/farm_animals/sheep.dm:28
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_mob_harvest), typeof(obj_item_razor), typeof(obj_item_food_grown_grass), typeof(obj_item_stack_sheet_cotton_wool), "soft wool", 10f, 3 * MINUTES, 30 * SECONDS, 5 * SECONDS, Dm.resource("sound/items/handling/surgery/scalpel1.ogg")));
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), CELL_LINE_TABLE_SHEEP, CELL_VIRUS_TABLE_GENERIC_MOB, 1f, 5f));
        RegisterSignal(this, COMSIG_LIVING_CULT_SACRIFICED, nameof(on_sacrificed));
        return dot;
    }

    // tg: code/modules/mob/living/basic/farm_animals/sheep.dm:46
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (stat == DEAD)
            return dot;
        if (Dm.truthy(cult_converted))
            Dm.append(ref dot, "hat");
        return dot;
    }

    ///signal called on the sheep being sacrificed by a cult
    // tg: code/modules/mob/living/basic/farm_animals/sheep.dm:54
    [SignalHandler]
    public virtual object on_sacrificed(datum source = null, DmList invokers = null)
    {
        if (Dm.truthy(cult_converted))
        {
            foreach (object cultist in Dm.each(invokers))
                to_chat(cultist, span_cult_italic(Dm.text("[] has already been sacrificed!", this)));
            return Dm.bitor(STOP_SACRIFICE, SILENCE_SACRIFICE_MESSAGE);
        }

        foreach (object cultist in Dm.each(invokers))
            to_chat(cultist, span_cult_italic("This feels a bit too cliché, don't you think?"));

        cult_converted = TRUE;
        Waiting.Detach(say("BAAAAAAAAH!"));
        update_appearance(UPDATE_ICON);
        return STOP_SACRIFICE;
    }
}
