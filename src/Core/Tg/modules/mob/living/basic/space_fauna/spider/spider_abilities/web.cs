// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/spider/spider_abilities/web.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps P3, D327, with the webbing mutation): spinning a web (web.dm:1-80) and its geneticists' variant; the nurse spider's sealer
// (web.dm:82-94, gaps E3, D327: the Spider Clan gift's shuttle loan). The spiders' other web abilities come with the spiders. The var blocks are generated (D111).

// tg: code/modules/mob/living/basic/space_fauna/spider/spider_abilities/web.dm:2
public partial class datum_action_cooldown_mob_cooldown_lay_web : datum_action_cooldown_mob_cooldown
{
    // tg: code/modules/mob/living/basic/space_fauna/spider/spider_abilities/web.dm:16
    public override async DmTask<object> Grant(mob grant_to = null, params object[] __extra)
    {
        object dot = await base.Grant(grant_to, __extra);
        if (owner == null)
            return dot;
        ADD_TRAIT(owner, TRAIT_WEB_WEAVER, REF(this));
        RegisterSignals(owner, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_DO_AFTER_BEGAN, COMSIG_DO_AFTER_ENDED), nameof(update_status_on_signal));
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/spider/spider_abilities/web.dm:23
    public override async DmTask Remove(mob removed_from = null)
    {
        await base.Remove(removed_from);
        REMOVE_TRAIT(removed_from, TRAIT_WEB_WEAVER, REF(this));
        UnregisterSignal(removed_from, Dm.list(COMSIG_MOVABLE_MOVED, COMSIG_DO_AFTER_BEGAN, COMSIG_DO_AFTER_ENDED));
    }

    // tg: code/modules/mob/living/basic/space_fauna/spider/spider_abilities/web.dm:28
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        feedback ??= FALSE;
        object dot = await base.IsAvailable(feedback);
        if (!Dm.truthy(dot))
            return FALSE;
        if (Dm.truthy(DOING_INTERACTION(owner, DOAFTER_SOURCE_SPIDER)))
        {
            if (Dm.truthy(feedback))
                owner.balloon_alert(owner, I18n.T("busy!"));
            return FALSE;
        }
        if (!isturf(owner.loc))
        {
            if (Dm.truthy(feedback))
                owner.balloon_alert(owner, I18n.T("invalid location!"));
            return FALSE;
        }
        if (HAS_TRAIT(owner.loc, TRAIT_SPINNING_WEB_TURF))
        {
            if (Dm.truthy(feedback))
                owner.balloon_alert(owner, I18n.T("already being webbed!"));
            return FALSE;
        }
        if (Dm.truthy(obstructed_by_other_web()))
        {
            if (Dm.truthy(feedback))
                owner.balloon_alert(owner, I18n.T("already webbed!"));
            return FALSE;
        }
        return TRUE;
    }

    /// Returns true if there's a web we can't put stuff on in our turf
    // tg: code/modules/mob/living/basic/space_fauna/spider/spider_abilities/web.dm:51
    public virtual object obstructed_by_other_web()
    {
        return Dm.locate_type(typeof(obj_structure_spider_stickyweb), get_turf(owner)) != null ? TRUE : FALSE;
    }

    // tg: code/modules/mob/living/basic/space_fauna/spider/spider_abilities/web.dm:54
    public override async DmTask<object> Activate(atom target = null)
    {
        object dot = await base.Activate(target);
        turf spider_turf = get_turf(owner);
        obj_structure_spider_stickyweb web = Dm.locate_type(typeof(obj_structure_spider_stickyweb), spider_turf) as obj_structure_spider_stickyweb;
        if (web != null)
            owner.balloon_alert_to_viewers(I18n.T("sealing web..."));
        else
            owner.balloon_alert_to_viewers(I18n.T("spinning web..."));
        ADD_TRAIT(spider_turf, TRAIT_SPINNING_WEB_TURF, REF(this));
        if (Dm.truthy(await do_after(owner, webbing_time, target: spider_turf, interaction_key: DOAFTER_SOURCE_SPIDER)) && owner.loc == spider_turf)
            plant_web(spider_turf, web);
        else
            owner?.balloon_alert(owner, I18n.T("interrupted!")); // Null check because we might have been interrupted via being disintegrated
        REMOVE_TRAIT(spider_turf, TRAIT_SPINNING_WEB_TURF, REF(this));
        await build_all_button_icons();
        return dot;
    }

    /// Creates a web in the current turf
    // tg: code/modules/mob/living/basic/space_fauna/spider/spider_abilities/web.dm:71
    public virtual void plant_web(turf target_turf = null, obj_structure_spider_stickyweb existing_web = null)
    {
        Dm.@new<obj_structure_spider_stickyweb>(target_turf);
    }
}

/// Variant for genetics, created webs only allow the creator passage
// tg: code/modules/mob/living/basic/space_fauna/spider/spider_abilities/web.dm:75
public partial class datum_action_cooldown_mob_cooldown_lay_web_genetic : datum_action_cooldown_mob_cooldown_lay_web
{
    // tg: code/modules/mob/living/basic/space_fauna/spider/spider_abilities/web.dm:79
    public override void plant_web(turf target_turf = null, obj_structure_spider_stickyweb existing_web = null)
    {
        Dm.@new<obj_structure_spider_stickyweb_genetic>(target_turf, owner);
    }
}

/// Variant which allows webs to be stacked into walls
// tg: code/modules/mob/living/basic/space_fauna/spider/spider_abilities/web.dm:83
public partial class datum_action_cooldown_mob_cooldown_lay_web_sealer : datum_action_cooldown_mob_cooldown_lay_web
{
    // tg: code/modules/mob/living/basic/space_fauna/spider/spider_abilities/web.dm:86
    public override void plant_web(turf target_turf = null, obj_structure_spider_stickyweb existing_web = null)
    {
        if (existing_web != null)
        {
            qdel(existing_web);
            Dm.@new<obj_structure_spider_stickyweb_sealed>(target_turf);
            return;
        }
        Dm.@new<obj_structure_spider_stickyweb>(target_turf);
    }

    // tg: code/modules/mob/living/basic/space_fauna/spider/spider_abilities/web.dm:93
    public override object obstructed_by_other_web()
    {
        return Dm.locate<obj_structure_spider_stickyweb_sealed>(get_turf(owner)) != null ? TRUE : FALSE;
    }
}
