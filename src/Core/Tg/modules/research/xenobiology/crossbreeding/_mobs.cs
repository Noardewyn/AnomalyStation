// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/xenobiology/crossbreeding/_mobs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the burning black extract's slime transformation (its spell and the slime it turns its user into) and the
// chilling pink extract's slime corgi puppy (in slime_puppy.cs). The var blocks are generated (D111).

/*
Slimecrossing Mobs
	Mobs and effects added by the slimecrossing system.
	Collected here for clarity.
*/

/// Slime transformation power - from Burning Black
// tg: code/modules/research/xenobiology/crossbreeding/_mobs.dm:9
public partial class datum_action_cooldown_spell_shapeshift_slime_form : datum_action_cooldown_spell_shapeshift
{
    // tg: code/modules/research/xenobiology/crossbreeding/_mobs.dm:23
    public override object do_unshapeshift(mob_living caster = null)
    {
        object dot = base.do_unshapeshift(caster);
        if (!Dm.truthy(dot))
            return dot;

        if (Dm.truthy(remove_on_restore))
            qdel(this);
        return dot;
    }
}

/// Transformed slime - from Burning Black
// tg: code/modules/research/xenobiology/crossbreeding/_mobs.dm:35
public partial class mob_living_basic_slime_transformed_slime : mob_living_basic_slime
{
    // Just in case.
    // tg: code/modules/research/xenobiology/crossbreeding/_mobs.dm:35
    public override async DmTask<object> reproduce()
    {
        await DmTask.CompletedTask;
        to_chat(this, span_warning(I18n.T("I can't reproduce..."))); // Mood
        return null;
    }
}

// The slime corgi puppy of the chilling pink extract is translated in modules/mob/living/basic/pets/dog/slime_puppy.cs (Meta maps one).
