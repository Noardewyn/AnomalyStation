// Translated from tgstation (snapshot 1d99bd7f): code/modules/library/skill_learning/job_skillchips/chef.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (the chef's Close Quarters Cooking came with the martial arts, syndicate X3b, D316). The other job chips are var
// blocks (their traits).

// tg: code/modules/library/skill_learning/job_skillchips/chef.dm:1
public partial class obj_item_skillchip_job_chef : obj_item_skillchip_job
{
    // tg: code/modules/library/skill_learning/job_skillchips/chef.dm:13
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        var chef_style = Dm.@new<datum_martial_art_cqc_under_siege>(this);
        style = chef_style;
        chef_style.refresh_valid_areas();
        return dot;
    }

    // tg: code/modules/library/skill_learning/job_skillchips/chef.dm:19
    public override async DmTask on_activate(mob_living_carbon user = null, float? silent = null)
    {
        silent ??= FALSE;
        await base.on_activate(user, silent);
        await ((datum_martial_art_cqc_under_siege)style).teach(user);
    }

    // tg: code/modules/library/skill_learning/job_skillchips/chef.dm:23
    public override async DmTask on_deactivate(mob_living_carbon user = null, float? silent = null)
    {
        silent ??= FALSE;
        await ((datum_martial_art_cqc_under_siege)style).unlearn(user);
        await base.on_deactivate(user, silent);
    }
}
