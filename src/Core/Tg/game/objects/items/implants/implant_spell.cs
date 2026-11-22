// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/implants/implant_spell.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps OG1): the spell implant, which grants its spell on implantation (tg's own implanter and case give the inert base
// with no spell). The var blocks are generated (D111).

// tg: code/game/objects/items/implants/implant_spell.dm:1
public partial class obj_item_implant_spell : obj_item_implant
{
    // tg: code/game/objects/items/implants/implant_spell.dm:23
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (spell_type == null)
            return dot;

        spell_to_give = Dm.@new(spell_type, this) as datum_action_cooldown_spell;

        if (Dm.truthy(make_robeless) && Dm.truthy(Dm.bitand(spell_to_give.spell_requirements, SPELL_REQUIRES_WIZARD_GARB)))
            spell_to_give.spell_requirements = Dm.num(Dm.bitand(spell_to_give.spell_requirements, ~SPELL_REQUIRES_WIZARD_GARB));

        implant_info = I18n.T("Automatically activates upon implantation. Allows an implantee to cast [], []", spell_to_give, Dm.truthy(make_robeless) ? I18n.T(", without needing appropriate wizard garb") : I18n.T(" if dressed in appropriate garb")) + ".";
        return dot;
    }

    // tg: code/game/objects/items/implants/implant_spell.dm:36
    public override object Destroy(float? force = null)
    {
        QDEL_NULL(ref spell_to_give);
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/implants/implant_spell.dm:40
    public override async DmTask<object> implant(mob_living target = null, mob user = null, float? silent = null, float? force = null, params object[] __extra)
    {
        silent ??= FALSE;
        force ??= FALSE;
        object dot = await base.implant(target, user, silent, force, __extra);
        if (!Dm.truthy(dot))
            return dot;

        if (spell_to_give == null)
            return FALSE;

        await spell_to_give.Grant(target);
        return TRUE;
    }

    // tg: code/game/objects/items/implants/implant_spell.dm:51
    public override async DmTask<object> removed(mob_living source = null, float? silent = null, float? special = null)
    {
        silent ??= FALSE;
        special ??= 0;
        object dot = await base.removed(source, silent, special);
        if (!Dm.truthy(dot))
            return FALSE;

        if (spell_to_give != null)
        {
            await spell_to_give.Remove(source);
            if (source.stat != DEAD && !Dm.truthy(silent))
                to_chat(source, span_boldnotice(I18n.T("The knowledge of how to cast [] slips out from your mind.", spell_to_give)));
        }
        return TRUE;
    }
}
