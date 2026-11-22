// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/ai_core_display.dm, ai_emote_display.dm,
// ai_hologram_display.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole files (silicons SI4, D326): the AI's preferred core display, status display face and hologram. The var blocks are
// generated (D111).

/// What to show on the AI screen
// tg: code/modules/client/preferences/ai_core_display.dm:2
public partial class datum_preference_choiced_ai_core_display : datum_preference_choiced
{
    // tg: code/modules/client/preferences/ai_core_display.dm:8
    public override DmList init_possible_values()
    {
        return Dm.sub(GLOB.ai_core_display_screens, "Portrait") as DmList;
    }

    // tg: code/modules/client/preferences/ai_core_display.dm:11
    public override object icon_for(object value = null)
    {
        if (Dm.eq(value, "Random"))
            return Dm.icon(Dm.resource("icons/mob/silicon/ai.dmi"), "questionmark"); // uni_icon: the port's /icon (universal_icon.cs)
        else
            return Dm.icon(Dm.resource("icons/mob/silicon/ai.dmi"), resolve_ai_icon_sync(value)); // uni_icon: the port's /icon (universal_icon.cs)
    }

    // tg: code/modules/client/preferences/ai_core_display.dm:17
    public override object is_accessible(datum_preferences preferences = null)
    {
        return (Dm.truthy(base.is_accessible(preferences)) && Dm.truthy(highest_priority_job_is(preferences, typeof(datum_job_ai)))) ? TRUE : FALSE;
    }

    // tg: code/modules/client/preferences/ai_core_display.dm:20
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        return;
    }
}

/// What to show on the AI monitor
// tg: code/modules/client/preferences/ai_emote_display.dm:2
public partial class datum_preference_choiced_ai_emote_display : datum_preference_choiced
{
    // tg: code/modules/client/preferences/ai_emote_display.dm:9
    public override DmList init_possible_values()
    {
        if (!Dm.truthy(length(GLOB.ai_status_display_all_options)))
            init_ai_status_display_options();
        return assoc_to_keys(GLOB.ai_status_display_all_options) as DmList;
    }

    // tg: code/modules/client/preferences/ai_emote_display.dm:14
    public override object icon_for(object value = null)
    {
        if (Dm.eq(value, "Random"))
            return Dm.icon(Dm.resource("icons/mob/silicon/ai.dmi"), "questionmark"); // uni_icon: the port's /icon (universal_icon.cs)
        else
        {
            if (!Dm.truthy(length(GLOB.ai_status_display_all_options)))
                init_ai_status_display_options();
            return Dm.icon(Dm.resource("icons/obj/machines/status_display.dmi"), GLOB.ai_status_display_all_options[value]); // uni_icon: the port's /icon (universal_icon.cs)
        }
    }

    // tg: code/modules/client/preferences/ai_emote_display.dm:22
    public override object is_accessible(datum_preferences preferences = null)
    {
        return (Dm.truthy(base.is_accessible(preferences)) && Dm.truthy(highest_priority_job_is(preferences, typeof(datum_job_ai)))) ? TRUE : FALSE;
    }

    // tg: code/modules/client/preferences/ai_emote_display.dm:25
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        return;
    }
}

/// What to show on the AI hologram
// tg: code/modules/client/preferences/ai_hologram_display.dm:2
public partial class datum_preference_choiced_ai_hologram_display : datum_preference_choiced
{
    // tg: code/modules/client/preferences/ai_hologram_display.dm:9
    public override DmList init_possible_values()
    {
        return Dm.add(assoc_to_keys(GLOB.ai_hologram_icons), "Random") as DmList;
    }

    // tg: code/modules/client/preferences/ai_hologram_display.dm:12
    public override object icon_for(object value = null)
    {
        if (Dm.eq(value, "Random"))
            return Dm.icon(Dm.resource("icons/mob/silicon/ai.dmi"), "questionmark"); // uni_icon: the port's /icon (universal_icon.cs)
        else
            return Dm.icon(GLOB.ai_hologram_icons[value], GLOB.ai_hologram_icon_state[value]); // uni_icon: the port's /icon (universal_icon.cs)
    }

    // tg: code/modules/client/preferences/ai_hologram_display.dm:18
    public override object is_accessible(datum_preferences preferences = null)
    {
        return (Dm.truthy(base.is_accessible(preferences)) && Dm.truthy(highest_priority_job_is(preferences, typeof(datum_job_ai)))) ? TRUE : FALSE;
    }

    // tg: code/modules/client/preferences/ai_hologram_display.dm:21
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        return;
    }
}
