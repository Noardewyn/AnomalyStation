// Translated from tgstation (snapshot 1d99bd7f): code/modules/library/skill_learning/generic_skillchips/matrix_taunt.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SL1 (D333): the whole file. The BULLET_DODGER chip: a taunt makes the user unhittable by projectiles for a moment, at a
// stamina cost. The var block is generated (D111).

// tg: code/modules/library/skill_learning/generic_skillchips/matrix_taunt.dm:3
public partial class obj_item_skillchip_matrix_taunt : obj_item_skillchip
{
    // tg: code/modules/library/skill_learning/generic_skillchips/matrix_taunt.dm:1  #define TAUNT_STAMINA_COST 19 (file-local, #undef at :39)
    private const float TAUNT_STAMINA_COST = 19;

    // tg: code/modules/library/skill_learning/generic_skillchips/matrix_taunt.dm:13
    public override async DmTask on_activate(mob_living_carbon user = null, float? silent = null)
    {
        silent ??= FALSE;
        await base.on_activate(user, silent);
        RegisterSignal(user, COMSIG_MOB_EMOTED("taunt"), nameof(on_taunt));
        RegisterSignal(user, COMSIG_MOB_PRE_EMOTED, nameof(check_if_we_can_taunt));
    }

    // tg: code/modules/library/skill_learning/generic_skillchips/matrix_taunt.dm:18
    public override async DmTask on_deactivate(mob_living_carbon user = null, float? silent = null)
    {
        silent ??= FALSE;
        UnregisterSignal(user, Dm.list(COMSIG_MOB_EMOTED("taunt"), COMSIG_MOB_PRE_EMOTED));
        await base.on_deactivate(user, silent);
    }

    ///Prevent players from stamcritting from INTENTIONAL flips. 1.4s of bullet immunity isn't worth several secs of stun.
    // tg: code/modules/library/skill_learning/generic_skillchips/matrix_taunt.dm:23
    [SignalHandler]
    public virtual object check_if_we_can_taunt(mob_living source = null, object key = null, object @params = null, object type_override = null, object intentional = null, datum_emote emote = null)
    {
        if (!Dm.eq(key, "taunt") || !Dm.truthy(intentional))
            return null;
        if ((source.maxHealth - (Dm.num(source.get_stamina_loss()) + TAUNT_STAMINA_COST)) <= Dm.num(source.crit_threshold))
        {
            source.balloon_alert(source, I18n.T("too tired!"));
            return COMPONENT_CANT_EMOTE;
        }
        return null;
    }

    // tg: code/modules/library/skill_learning/generic_skillchips/matrix_taunt.dm:31
    [SignalHandler]
    public virtual void on_taunt(mob_living source = null)
    {
        if (HAS_TRAIT_FROM(source, TRAIT_UNHITTABLE_BY_PROJECTILES, SKILLCHIP_TRAIT))
            return;
        ADD_TRAIT(source, TRAIT_UNHITTABLE_BY_PROJECTILES, SKILLCHIP_TRAIT);
        Waiting.NoSleep(source.adjust_stamina_loss(TAUNT_STAMINA_COST));
        _addtimer(TRAIT_CALLBACK_REMOVE(source, TRAIT_UNHITTABLE_BY_PROJECTILES, SKILLCHIP_TRAIT), TAUNT_EMOTE_DURATION * 1.5f, file: "code/modules/library/skill_learning/generic_skillchips/matrix_taunt.dm", line: 37);
    }
}
