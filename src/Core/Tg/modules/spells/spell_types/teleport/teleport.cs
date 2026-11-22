// Translated from tgstation (snapshot 1d99bd7f): code/modules/spells/spell_types/teleport/teleport.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps R3 (D327): the whole file: the wizard's teleport scroll's checks (the wizard's and Santa's teleports are var blocks only,
// generated, D111).

/// Used by the wizard's teleport scroll
// tg: code/modules/spells/spell_types/teleport/teleport.dm:32
public partial class datum_action_cooldown_spell_teleport_area_teleport_wizard_scroll : datum_action_cooldown_spell_teleport_area_teleport_wizard
{
    // tg: code/modules/spells/spell_types/teleport/teleport.dm:42
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        return Dm.truthy(await base.IsAvailable(feedback)) && Dm.truthy(owner.is_holding(target as obj_item)) ? TRUE : FALSE;
    }

    // tg: code/modules/spells/spell_types/teleport/teleport.dm:45
    public override async DmTask<object> before_cast(atom cast_on = null)
    {
        object dot = await base.before_cast(cast_on);
        if (Dm.truthy(Dm.bitand(dot, SPELL_CANCEL_CAST)))
            return dot;

        var caster = cast_on as mob_living_carbon;
        if (Dm.truthy(caster.incapacitated) || !Dm.truthy(caster.is_holding(target as obj_item)))
            return Dm.bitor(dot, SPELL_CANCEL_CAST);
        return dot;
    }
}
