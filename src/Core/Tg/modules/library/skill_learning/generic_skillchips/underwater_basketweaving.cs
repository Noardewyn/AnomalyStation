// Translated from tgstation (snapshot 1d99bd7f): code/modules/library/skill_learning/generic_skillchips/underwater_basketweaving.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SL1 (D333): the whole file. The Basketsoft 3000 chip teaches the underwater basket recipes while it is active. The var block is
// generated (D111).

// tg: code/modules/library/skill_learning/generic_skillchips/underwater_basketweaving.dm:2
public partial class obj_item_skillchip_basketweaving : obj_item_skillchip
{
    // tg: code/modules/library/skill_learning/generic_skillchips/underwater_basketweaving.dm:11
    public override object has_mob_incompatibility(mob_living_carbon target = null)
    {
        object dot = base.has_mob_incompatibility(target);
        if (Dm.truthy(dot))
            return dot;

        if (!Dm.truthy(target.mind))
            return I18n.T("Target incapable of learning recipe.");
        return dot;
    }

    // tg: code/modules/library/skill_learning/generic_skillchips/underwater_basketweaving.dm:19
    public override async DmTask on_activate(mob_living_carbon user = null, float? silent = null)
    {
        silent ??= FALSE;
        await base.on_activate(user, silent);
        if (!Dm.truthy(user.mind))
            return;
        learn_recipes(user);
        //Prevent the skill from being transferred via mindswap. What an edge case
        RegisterSignal(user.mind, COMSIG_MIND_TRANSFERRED, nameof(forget_recipes));
        RegisterSignal(user, COMSIG_MOB_MIND_TRANSFERRED_INTO, nameof(learn_recipes));
    }

    // tg: code/modules/library/skill_learning/generic_skillchips/underwater_basketweaving.dm:28
    [SignalHandler]
    public virtual void learn_recipes(mob source = null)
    {
        foreach (object recipe_type in Dm.each(typesof(typeof(datum_crafting_recipe_underwater_basket))))
            source.mind.teach_crafting_recipe(recipe_type);
    }

    // tg: code/modules/library/skill_learning/generic_skillchips/underwater_basketweaving.dm:33
    [SignalHandler]
    public virtual void forget_recipes(datum_mind source = null, mob previous_body = null)
    {
        foreach (object recipe_type in Dm.each(typesof(typeof(datum_crafting_recipe_underwater_basket))))
            source.forget_crafting_recipe(recipe_type);
    }

    // tg: code/modules/library/skill_learning/generic_skillchips/underwater_basketweaving.dm:38
    public override async DmTask on_deactivate(mob_living_carbon user = null, float? silent = null)
    {
        silent ??= FALSE;
        if (Dm.truthy(user.mind))
        {
            forget_recipes(user.mind);
            UnregisterSignal(user.mind, COMSIG_MIND_TRANSFERRED);
            UnregisterSignal(user, COMSIG_MOB_MIND_TRANSFERRED_INTO);
        }
        await base.on_deactivate(user, silent);
    }
}
