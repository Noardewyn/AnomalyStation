// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/illusion/illusion_subtypes.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps WP1 (D333): the file whole, with the illusion (illusion.dm). The shover shoves before it hits (tg's disarm returns nothing,
// so its "no attack" never comes and it hits too: translated as written); the escaping illusion flees its target, strikes back at
// whoever hurts it and never at the mob it mocks; its retaliating kind fights back as it goes; the mirage stands still and sparks
// when it fades. The var blocks are generated (D111).

/// instead of hitting with heavy object, we shove 'em
// tg: code/modules/mob/living/basic/illusion/illusion_subtypes.dm:2
public partial class mob_living_basic_illusion_shover : mob_living_basic_illusion
{
    // tg: code/modules/mob/living/basic/illusion/illusion_subtypes.dm:4
    [SignalHandler]
    public override object on_preattack(mob_living source = null, atom attacked_target = null)
    {
        base.on_preattack(source, attacked_target);
        Waiting.NoSleep(disarm(attacked_target as mob_living));
        return null; // if(disarm(attacked_target)) return COMPONENT_HOSTILE_NO_ATTACK: tg's disarm returns null
    }
}

/// designed to run away as fast as possible
// tg: code/modules/mob/living/basic/illusion/illusion_subtypes.dm:10
public partial class mob_living_basic_illusion_escape : mob_living_basic_illusion
{
    // tg: code/modules/mob/living/basic/illusion/illusion_subtypes.dm:14
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_ai_retaliate)));
        return dot;
    }

    // tg: code/modules/mob/living/basic/illusion/illusion_subtypes.dm:22
    [SignalHandler]
    public override object on_preattack(mob_living source = null, atom attacked_target = null)
    {
        base.on_preattack(source, attacked_target);
        mob_living parent_mob = parent_mob_ref?.resolve() as mob_living;
        if (attacked_target == parent_mob) // we don't want to get our wires crossed and attack our owner ever.
            return COMPONENT_HOSTILE_NO_ATTACK;
        return null;
    }
}

/// a mirage with a little more flair. not meant to move just to look cool
// tg: code/modules/mob/living/basic/illusion/illusion_subtypes.dm:29
public partial class mob_living_basic_illusion_mirage : mob_living_basic_illusion
{
    // tg: code/modules/mob/living/basic/illusion/illusion_subtypes.dm:33
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        do_sparks(rand(3, 6), FALSE, this);
        return await base.death(gibbed, __extra);
    }
}
