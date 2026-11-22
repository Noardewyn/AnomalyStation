// Translated from tgstation (snapshot 1d99bd7f): code/modules/jobs/job_types/mime.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps J2 (D298): the job, the outfit and the Guide to Dank Mimery.

// tg: code/modules/jobs/job_types/mime.dm:1
public partial class datum_job_mime : datum_job
{
    // tg: code/modules/jobs/job_types/mime.dm:39
    public override async DmTask after_spawn(mob_living spawned = null, client player_client = null)
    {
        if (ishuman(spawned))
            await spawned.apply_pref_name(typeof(datum_preference_name_mime), player_client);
        await base.after_spawn(spawned, player_client);
    }
}

// tg: code/modules/jobs/job_types/mime.dm:45
public partial class datum_outfit_job_mime : datum_outfit_job
{
    // tg: code/modules/jobs/job_types/mime.dm:69
    public override async DmTask post_equip(mob_living_carbon_human user = null, float? visuals_only = null)
    {
        await base.post_equip(user, visuals_only);

        if (Dm.truthy(visuals_only))
            return;

        // Start our mime out with a vow of silence and the ability to break (or make) it
        if (user.mind != null)
        {
            var vow = await Dm.new_async<datum_action_cooldown_spell_vow_of_silence>(user.mind);
            await vow.Grant(user);
        }

        ADD_TRAIT(user, TRAIT_MIME_FAN, INNATE_TRAIT);
    }
}

// tg: code/modules/jobs/job_types/mime.dm:83
public partial class obj_item_book_granter_action_spell_mime_mimery : obj_item_book_granter_action_spell_mime
{
    // tg: code/modules/jobs/job_types/mime.dm:88
    public override async DmTask<object> on_reading_start(mob_living user = null)
    {
        DmList spell_icons = Dm.list();
        DmList name_to_spell = Dm.list();
        foreach (Type type in new[] { typeof(datum_action_cooldown_spell_conjure_invisible_wall), typeof(datum_action_cooldown_spell_conjure_invisible_chair), typeof(datum_action_cooldown_spell_conjure_item_invisible_box) })
        {
            string spell_name = Dm.text("[]", Dm.initial(type, "name"));
            if (Dm.locate_type(type, user.actions) == null)
                spell_icons[spell_name] = image(icon: Dm.initial(type, "button_icon"), icon_state: Dm.initial(type, "button_icon_state"));
            name_to_spell[spell_name] = type;
        }
        object picked_spell = await show_radial_menu(user, this, spell_icons, custom_check: Dm.@new<datum_callback>(this, nameof(check_menu), user), radius: 36, require_near: TRUE);
        if (!Dm.truthy(picked_spell))
            return FALSE;
        granted_action = name_to_spell[picked_spell] as Type;
        return TRUE;
    }

    // tg: code/modules/jobs/job_types/mime.dm:101
    public override async DmTask on_reading_finished(mob_living user = null)
    {
        // Gives the user a vow ability too, if they don't already have one
        var vow = Dm.locate<datum_action_cooldown_spell_vow_of_silence>(user.actions) as datum_action_cooldown_spell_vow_of_silence;
        if (vow == null && user.mind != null)
        {
            vow = await Dm.new_async<datum_action_cooldown_spell_vow_of_silence>(user.mind);
            await vow.Grant(user);
        }
        datum_action new_action = await Dm.new_async(granted_action, (object)user.mind ?? user) as datum_action;
        await new_action.Grant(user);
        to_chat(user, span_warning(I18n.T("The book disappears into thin air.")));
        qdel(this);
    }

    // tg: code/modules/jobs/job_types/mime.dm:112
    public override object can_learn(mob_living user = null)
    {
        foreach (Type type in new[] { typeof(datum_action_cooldown_spell_conjure_invisible_wall), typeof(datum_action_cooldown_spell_conjure_invisible_chair), typeof(datum_action_cooldown_spell_conjure_item_invisible_box) })
        {
            if (Dm.locate_type(type, user.actions) == null)
                return TRUE;
        }
        to_chat(user, span_warning(I18n.T("You already know the secrets of mimery!")));
        return FALSE;
    }

    /**
     * Checks if we are allowed to interact with a radial menu
     *
     * Arguments:
     * * user The human mob interacting with the menu
     */
    // tg: code/modules/jobs/job_types/mime.dm:125
    public virtual object check_menu(mob_living_carbon_human user = null)
    {
        if (user == null)
            return FALSE;
        if (!Dm.truthy(user.is_holding(this)))
            return FALSE;
        if (Dm.truthy(user.incapacitated))
            return FALSE;
        if (user.mind == null)
            return FALSE;
        return TRUE;
    }
}
