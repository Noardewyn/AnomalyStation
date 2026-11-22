// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/implants/implant_mindshield.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M8 slice F, D186). The mind signals (conversion, resisting) have no listener before antagonists (M10).

// tg: code/game/objects/items/implants/implant_mindshield.dm:1
public partial class obj_item_implant_mindshield : obj_item_implant
{
    // tg: code/game/objects/items/implants/implant_mindshield.dm:12
    [DmParams("target", "user", "silent", "force")]
    public override async DmTask<object> implant(mob_living target = null, mob user = null, float? silent = null, float? force = null, params object[] __extra)
    {
        silent ??= FALSE;
        force ??= FALSE;
        object dot = await base.implant(target, user, silent, force, __extra);
        if (!Dm.truthy(dot))
            return FALSE;
        if (Dm.truthy(target.mind))
        {
            if (Dm.truthy(Dm.bitand(!Dm.truthy(target.mind._listen_lookup?[COMSIG_PRE_MINDSHIELD_IMPLANT]) ? NONE : target.mind._SendSignal(COMSIG_PRE_MINDSHIELD_IMPLANT, Dm.list(target.mind, user)), COMPONENT_MINDSHIELD_RESISTED)))
            {
                if (!Dm.truthy(silent))
                    target.visible_message(span_warning(I18n.T("[] seems to resist the implant!", target)), span_warning(I18n.T("You feel something interfering with your mental conditioning, but you resist it!")));
                await removed(target, TRUE);
                qdel(this);
                return TRUE;
            }
            if (Dm.truthy(Dm.bitand(!Dm.truthy(target.mind._listen_lookup?[COMSIG_MINDSHIELD_IMPLANTED]) ? NONE : target.mind._SendSignal(COMSIG_MINDSHIELD_IMPLANTED, Dm.list(target.mind, user)), COMPONENT_MINDSHIELD_DECONVERTED)))
            {
                if (Dm.truthy(prob(1)) || Dm.truthy(check_holidays(APRIL_FOOLS)))
                    await target.say(I18n.T("I'm out! I quit! Whose kidneys are these?"), forced: "They're out! They quit! Whose kidneys do they have?");
            }
        }

        target.add_traits(Dm.list(TRAIT_MINDSHIELD, TRAIT_UNCONVERTABLE), IMPLANT_TRAIT);
        target.sec_hud_set_implants();
        if (!Dm.truthy(silent))
            to_chat(target, span_notice(I18n.T("You feel a sense of peace and security. You are now protected from brainwashing.")));
        return TRUE;
    }

    // tg: code/game/objects/items/implants/implant_mindshield.dm:33
    public override async DmTask<object> removed(mob_living target = null, float? silent = null, float? special = null)
    {
        silent ??= FALSE;
        special ??= FALSE;
        object dot = await base.removed(target, silent, special);
        if (!Dm.truthy(dot))
            return FALSE;
        if (isliving(target))
        {
            mob_living L = target;
            target.remove_traits(Dm.list(TRAIT_MINDSHIELD, TRAIT_UNCONVERTABLE), IMPLANT_TRAIT);
            L.sec_hud_set_implants();
        }
        if (target.stat != DEAD && !Dm.truthy(silent))
            to_chat(target, span_boldnotice(I18n.T("Your mind suddenly feels terribly vulnerable. You are no longer safe from brainwashing.")));
        return TRUE;
    }
}
