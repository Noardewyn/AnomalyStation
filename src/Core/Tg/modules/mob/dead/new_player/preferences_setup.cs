// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/dead/new_player/preferences_setup.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M10 slice F, D267): the character's randomisation; M10x slice E1: the highest priority job and the preview appearance.
// Not translated: the hardcore random setup (the quirks, SSquirks).

public partial class datum_preferences : datum
{
    /// Fully randomizes everything in the character.
    // tg: code/modules/mob/dead/new_player/preferences_setup.dm:2
    public virtual void randomise_appearance_prefs(object randomize_flags = null)
    {
        randomize_flags ??= ALL;
        string tree_key = Dm.text("character[]", default_slot);
        if (!Dm.@in(tree_key, savefile.get_entry()))
            savefile.set_entry(tree_key, Dm.list());
        foreach (datum_preference preference in Dm.each<datum_preference>(get_preferences_in_priority_order()))
        {
            if (!Dm.truthy(preference.included_in_randomization_flags(randomize_flags)))
                continue;

            if (Dm.truthy(preference.is_randomizable()))
                write_preference(preference, preference.create_random_value(this));
        }
    }

    /// Randomizes the character according to preferences.
    // tg: code/modules/mob/dead/new_player/preferences_setup.dm:14
    public virtual void apply_character_randomization_prefs(object antag_override = null)
    {
        antag_override ??= FALSE;
        object random_body = read_preference(typeof(datum_preference_choiced_random_body));
        if (Dm.eq(random_body, RANDOM_ANTAG_ONLY))
        {
            if (!Dm.truthy(antag_override))
                return;
        }
        else if (Dm.eq(random_body, RANDOM_DISABLED))
            return;

        foreach (datum_preference preference in Dm.each<datum_preference>(get_preferences_in_priority_order()))
        {
            if (Dm.truthy(should_randomize(preference, antag_override)))
                write_preference(preference, preference.create_random_value(this));
        }
    }

    /// Returns what job is marked as highest
    // tg: code/modules/mob/dead/new_player/preferences_setup.dm:83
    public virtual datum_job get_highest_priority_job()
    {
        datum_job preview_job = null;
        float highest_pref = 0;

        foreach (object job in Dm.each(job_preferences))
        {
            object priority = job_preferences[job];
            if (Dm.gt(priority, highest_pref))
            {
                preview_job = SSjob.get_job(job) as datum_job;
                highest_pref = Dm.num(priority);
            }
        }

        return preview_job;
    }

    // tg: code/modules/mob/dead/new_player/preferences_setup.dm:95
    public virtual async DmTask<object> render_new_preview_appearance(mob_living_carbon_human_dummy mannequin = null, object show_job_clothes = null)
    {
        show_job_clothes ??= TRUE;
        datum_job no_job = SSjob.get_job_type(typeof(datum_job_unassigned)) as datum_job;
        datum_job preview_job = get_highest_priority_job() ?? no_job;

        if (preview_job != null)
        {
            // Silicons only need a very basic preview since there is no customization for them.
            if (preview_job is datum_job_ai)
            {
                // image('icons/mob/silicon/ai.dmi', icon_state = resolve_ai_icon(read_preference(/datum/preference/choiced/ai_core_display)), dir = SOUTH):
                // GAP(P2), resolve_ai_icon and the ai_core_display preference are not translated (the silicons are closed, D274): tg's default core
                return Dm.image(icon: Dm.resource("icons/mob/silicon/ai.dmi"), icon_state: "ai", dir: SOUTH);
            }
            if (preview_job is datum_job_cyborg)
                return Dm.image(icon: Dm.resource("icons/mob/silicon/robots.dmi"), icon_state: "robot", dir: SOUTH);
        }

        // Set up the dummy for its photoshoot
        apply_prefs_to(mannequin, TRUE);

        // DEVIATION(DV215): in the round the Job Clothes checkbox shows what the character wears now, copied onto the mannequin
        mob_living_carbon_human wearer = parent?.mob as mob_living_carbon_human;
        mannequin.job = preview_job.title;
        await mannequin.dress_up_as_job(
            equipping: Dm.truthy(show_job_clothes) && wearer == null ? preview_job : no_job,
            visual_only: TRUE,
            player_client: parent,
            consistent: TRUE);
        if (Dm.truthy(show_job_clothes) && wearer != null)
        {
            // shortcut: a fresh item of each worn type, so a worn item's own state (its colours, a rolled jumpsuit) is not copied
            foreach (obj_item worn in Dm.each<obj_item>(wearer.get_equipped_items()))
                await mannequin.equip_to_slot_or_del(await SSwardrobe.provide_type(worn.type, mannequin) as obj_item, wearer.get_slot_by_item(worn), TRUE, indirect_action: TRUE);
        }

        // Apply visual quirks: if(SSquirks?.initialized), the quirks are not translated (SSquirks): none to apply

        // Height is applied universally once to save on filters: mannequin.apply_height(mannequin, ENTIRE_BODY); the port's apply_height
        // takes an image, and a mannequin is of medium height (apply_height returns at once for it)
        return mannequin.appearance;
    }
}
