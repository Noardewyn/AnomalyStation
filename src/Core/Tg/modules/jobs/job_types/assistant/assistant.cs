// Translated from tgstation (snapshot 1d99bd7f): code/modules/jobs/job_types/assistant/assistant.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: GLOB.colored_assistant and the assistant outfits (/datum/outfit/job/assistant, consistent and preview) with
// their pre_equip, give_holiday_hat (gaps R3), give_jumpsuit and post_equip (M2 F1: a joining player's random-colour jumpsuit), and
// the job's get_outfit with the Geared Assistants Pilot's gimmick loadouts (gaps R2); the job's vars are its generated var block.

// tg: code/modules/jobs/job_types/assistant/assistant.dm:1  GLOBAL_DATUM(colored_assistant, /datum/colored_assistant)
public partial class datum_controller_global_vars
{
    // tg: code/modules/jobs/job_types/assistant/assistant.dm:1  GLOBAL_DATUM(colored_assistant, /datum/colored_assistant)
    public datum_colored_assistant colored_assistant;

    // tg: code/modules/jobs/job_types/assistant/assistant.dm:1  (GLOBAL_UNMANAGED(colored_assistant), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalcolored_assistant()
    {
        return;
    }
}

/*
Assistant
*/
// tg: code/modules/jobs/job_types/assistant/assistant.dm:6
[DmPath("/datum/job/assistant")]
public partial class datum_job_assistant : datum_job
{
    // The job's var overrides (:7-39: title, positions, outfit, paycheck, ...) are its generated var block (D111).

    // tg: code/modules/jobs/job_types/assistant/assistant.dm:41  (gaps R2: the Geared Assistants Pilot station trait's loadouts)
    public override object get_outfit(object consistent = null)
    {
        if (Dm.truthy(consistent))
            return typeof(datum_outfit_job_assistant_preview);
        if (!HAS_TRAIT(SSstation, STATION_TRAIT_ASSISTANT_GIMMICKS))
            return base.get_outfit(consistent);

        if (!Dm.truthy(length(get_outfit__gimmicks)))
        {
            foreach (Type gimmick_outfit in Dm.each_cast<Type>(subtypesof(typeof(datum_outfit_job_assistant_gimmick))))
                get_outfit__gimmicks[gimmick_outfit] = Dm.initial(gimmick_outfit, (datum_outfit_job_assistant_gimmick p) => p.outfit_weight);
        }

        return pick_weight(get_outfit__gimmicks);
    }

    // tg: code/modules/jobs/job_types/assistant/assistant.dm:46  get_outfit's var/static/list/gimmicks = list() (a proc-level static: rules 10.6)
    private static ref DmList get_outfit__gimmicks => ref TgState.Current.datum_job_assistant__get_outfit__gimmicks;
}

public sealed partial class TgState
{
    // tg: code/modules/jobs/job_types/assistant/assistant.dm:46  get_outfit's var/static/list/gimmicks = list()
    internal DmList datum_job_assistant__get_outfit__gimmicks = Dm.list();
}

// tg: code/modules/jobs/job_types/assistant/assistant.dm:54
[DmPath("/datum/outfit/job/assistant")]
public partial class datum_outfit_job_assistant : datum_outfit_job
{
    // tg: code/modules/jobs/job_types/assistant/assistant.dm:55-58
    public datum_outfit_job_assistant()
    {
        name = JOB_ASSISTANT;
        jobtype = typeof(datum_job_assistant);
        id_trim = typeof(datum_id_trim_job_assistant);
        belt = typeof(obj_item_modular_computer_pda_crew_assistant);
    }

    // tg: code/modules/jobs/job_types/assistant/assistant.dm:60
    [DmParams("target")]
    public override async DmTask pre_equip(mob_living_carbon_human target = null, float? visuals_only = null)
    {
        await base.pre_equip(target, visuals_only);
        give_holiday_hat(target);
        give_jumpsuit(target);
    }

    // tg: code/modules/jobs/job_types/assistant/assistant.dm:65
    public virtual void give_holiday_hat(mob_living_carbon_human target = null)
    {
        foreach (object holidayname in Dm.each(GLOB.holidays)) // gaps R3
        {
            var holiday_today = (datum_holiday)GLOB.holidays[holidayname];
            Type special_hat = holiday_today.holiday_hat;
            if (Dm.truthy(prob(HOLIDAY_HAT_CHANCE)) && special_hat != null && head == null)
                head = special_hat;
        }
    }

    // tg: code/modules/jobs/job_types/assistant/assistant.dm:72
    public virtual void give_jumpsuit(mob_living_carbon_human target = null)
    {
        jumpsuit_number += 1;

        if (GLOB.colored_assistant == null)
        {
            object configured_type = get_configured_colored_assistant_type();
            GLOB.colored_assistant = Dm.@new((Type)configured_type) as datum_colored_assistant;
        }

        float index = Dm.mod(jumpsuit_number, GLOB.colored_assistant.jumpsuits.len) + 1;

        //We don't cache these, because they can delete on init
        //Too fragile, better to just eat the cost
        if (target.jumpsuit_style == PREF_SUIT)
            uniform = GLOB.colored_assistant.jumpsuits[index] as Type;
        else
            uniform = GLOB.colored_assistant.jumpskirts[index] as Type;
    }

    // tg: code/modules/jobs/job_types/assistant/assistant.dm:73  var/static/jumpsuit_number = 0 (a proc-level static: rules 10.6)
    private static ref float jumpsuit_number => ref TgState.Current.datum_outfit_job_assistant__give_jumpsuit__jumpsuit_number;
}

public sealed partial class TgState
{
    // tg: code/modules/jobs/job_types/assistant/assistant.dm:73  give_jumpsuit's var/static/jumpsuit_number = 0
    internal float datum_outfit_job_assistant__give_jumpsuit__jumpsuit_number = 0;
}

// tg: code/modules/jobs/job_types/assistant/assistant.dm:89
[DmPath("/datum/outfit/job/assistant/consistent")]
public partial class datum_outfit_job_assistant_consistent : datum_outfit_job_assistant
{
    // tg: code/modules/jobs/job_types/assistant/assistant.dm:90
    public datum_outfit_job_assistant_consistent()
    {
        name = "Assistant - Consistent";
    }

    // tg: code/modules/jobs/job_types/assistant/assistant.dm:92
    public override void give_holiday_hat(mob_living_carbon_human target = null)
    {
        return;
    }

    // tg: code/modules/jobs/job_types/assistant/assistant.dm:95
    public override void give_jumpsuit(mob_living_carbon_human target = null)
    {
        uniform = typeof(obj_item_clothing_under_color_grey);
    }

    // tg: code/modules/jobs/job_types/assistant/assistant.dm:98
    [DmParams("H", "visuals_only")]
    public override async DmTask post_equip(mob_living_carbon_human H = null, float? visuals_only = null)
    {
        await base.post_equip(H, visuals_only);

        // This outfit is used by the assets SS, which is ran before the atoms SS
        if (SSatoms.initialized == INITIALIZATION_INSSATOMS)
        {
            if (H.w_uniform != null)
                await H.w_uniform.update_greyscale();
            await H.update_worn_undersuit();
        }
    }
}

// tg: code/modules/jobs/job_types/assistant/assistant.dm:106
[DmPath("/datum/outfit/job/assistant/preview")]
public partial class datum_outfit_job_assistant_preview : datum_outfit_job_assistant
{
    // tg: code/modules/jobs/job_types/assistant/assistant.dm:107
    public datum_outfit_job_assistant_preview()
    {
        name = "Assistant - Preview";
    }

    // tg: code/modules/jobs/job_types/assistant/assistant.dm:109
    public override void give_jumpsuit(mob_living_carbon_human target = null)
    {
        if (target.jumpsuit_style == PREF_SUIT)
            uniform = typeof(obj_item_clothing_under_color_grey);
        else
            uniform = typeof(obj_item_clothing_under_color_jumpskirt_grey);
    }
}
