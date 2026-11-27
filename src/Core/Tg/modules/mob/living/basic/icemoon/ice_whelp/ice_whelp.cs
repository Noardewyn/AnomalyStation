// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/icemoon/ice_whelp/ice_whelp.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


// The ash whelp is a var block (generated).

// tg: code/modules/mob/living/basic/icemoon/ice_whelp/ice_whelp.dm:1
public partial class mob_living_basic_mining_ice_whelp : mob_living_basic_mining
{
    // tg: code/modules/mob/living/basic/icemoon/ice_whelp/ice_whelp.dm:44
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NO_GLIDE, INNATE_TRAIT);

        Waiting.NoSleep(AddElement(typeof(datum_element_footstep), FOOTSTEP_MOB_HEAVY));
        grant_actions_by_list(innate_actions);
        return dot;
    }

    // tg: code/modules/mob/living/basic/icemoon/ice_whelp/ice_whelp.dm:51
    public override async DmTask<object> early_melee_attack(atom target = null, DmList modifiers = null, float? ignore_cooldown = null)
    {
        object dot = await base.early_melee_attack(target, modifiers, ignore_cooldown);
        if (Dm.truthy(dot))
            return dot;

        if (target is obj_structure_flora_rock_icy)
        {
            await create_sculpture(target);
            return BASIC_MOB_END_ATTACK_CHAIN_COOLDOWN;
        }

        if (!istype(target, type))
            return BASIC_MOB_CONTINUE_ATTACK_CHAIN;

        mob_living victim = target as mob_living;
        if (victim.stat != DEAD)
            return BASIC_MOB_CONTINUE_ATTACK_CHAIN;

        await cannibalize_victim(victim);
        return BASIC_MOB_END_ATTACK_CHAIN_COOLDOWN;
    }

    /// Carve a stone into a beautiful self-portrait
    // tg: code/modules/mob/living/basic/icemoon/ice_whelp/ice_whelp.dm:71
    public virtual async DmTask create_sculpture(atom target = null)
    {
        balloon_alert(this, "sculpting...");
        if (!Dm.truthy(await do_after(this, 5 * SECONDS, target: target)))
            return;
        obj_structure_statue_custom dragon_statue = Dm.@new<obj_structure_statue_custom>(get_turf(target));
        dragon_statue.set_visuals(this);
        dragon_statue.name = I18n.T("statue of []", this);
        dragon_statue.desc = I18n.T("Let this serve as a warning.");
        await dragon_statue.set_anchored(TRUE);
        qdel(target);
    }

    /// Gib and consume our fellow ice drakes
    // tg: code/modules/mob/living/basic/icemoon/ice_whelp/ice_whelp.dm:83
    public virtual async DmTask cannibalize_victim(mob_living target = null)
    {
        await start_pulling(target);
        balloon_alert(this, "devouring...");
        if (!Dm.truthy(await do_after(this, 5 * SECONDS, target)))
            return;
        await target.gib(DROP_ALL_REMAINS);
        await adjust_brute_loss(-1 * heal_on_cannibalize);
    }
}
