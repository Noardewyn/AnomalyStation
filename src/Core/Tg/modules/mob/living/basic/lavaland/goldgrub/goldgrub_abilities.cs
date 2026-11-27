// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/lavaland/goldgrub/goldgrub_abilities.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


// tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub_abilities.dm:1
public partial class datum_action_cooldown_mob_cooldown_spit_ore : datum_action_cooldown_mob_cooldown
{
    // tg's override calls no parent (goldgrub_abilities.dm:7).
    // tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub_abilities.dm:7
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        if (is_jaunting(owner))
        {
            if (Dm.truthy(feedback))
                owner.balloon_alert(owner, "currently underground!");
            return FALSE;
        }

        if (Dm.length(owner.contents) == 0)
        {
            if (Dm.truthy(feedback))
                owner.balloon_alert(owner, "no ores to spit!");
            return FALSE;
        }
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub_abilities.dm:19
    public override async DmTask<object> Activate(atom target = null)
    {
        mob_living_basic_mining_goldgrub grub_owner = owner as mob_living_basic_mining_goldgrub;
        grub_owner.barf_contents();
        StartCooldown();
        return TRUE;
    }
}

// tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub_abilities.dm:25
public partial class datum_action_cooldown_mob_cooldown_burrow : datum_action_cooldown_mob_cooldown
{
    // tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub_abilities.dm:31
    public override async DmTask<object> IsAvailable(float? feedback = null)
    {
        object dot = await base.IsAvailable(feedback);
        if (!Dm.truthy(dot))
            return FALSE;
        turf location = get_turf(owner) as turf;

        if (!isasteroidturf(location) && !ismineralturf(location))
        {
            if (Dm.truthy(feedback))
                owner.balloon_alert(owner, "available only on mining floor or wall!");
            return FALSE;
        }

        return TRUE;
    }

    // tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub_abilities.dm:44
    public override async DmTask<object> Activate(atom target = null)
    {
        obj_effect_dummy_phased_mob_grub_burrow holder = null;
        turf current_loc = get_turf(owner) as turf;
        mob_living grub = owner as mob_living;

        if (!Dm.truthy(await do_after(owner, 2.5f * SECONDS, target: current_loc, extra_checks: Dm.@new<datum_callback>(this, nameof(health_check), grub.health))))
            return null;

        if (get_turf(owner) != current_loc)
        {
            to_chat(owner, span_warning(I18n.T("Action cancelled, as you moved while reappearing.")));
            return null;
        }

        if (!is_jaunting(owner))
        {
            owner.visible_message(span_danger(I18n.T("[] buries into the ground, vanishing from sight!", owner)));
            playsound(get_turf(owner), Dm.resource("sound/effects/break_stone.ogg"), 50, TRUE, -1);
            holder = Dm.@new<obj_effect_dummy_phased_mob_grub_burrow>(current_loc, owner);
            return TRUE;
        }

        holder = owner.loc as obj_effect_dummy_phased_mob_grub_burrow;
        await holder.eject_jaunter();
        holder = null;
        owner.visible_message(span_danger(I18n.T("[] emerges from the ground!", owner)));

        if (ismineralturf(current_loc))
        {
            turf_closed_mineral mineral_turf = current_loc as turf_closed_mineral;
            await mineral_turf.gets_drilled(owner);
        }

        playsound(current_loc, Dm.resource("sound/effects/break_stone.ogg"), 50, TRUE, -1);
        StartCooldown();
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub_abilities.dm:75
    public virtual object health_check(object health = null)
    {
        mob_living grub = owner as mob_living;
        return grub.health >= Dm.num(health) ? TRUE : FALSE;
    }
}

// tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub_abilities.dm:81
public partial class obj_effect_dummy_phased_mob_grub_burrow : obj_effect_dummy_phased_mob
{
    // tg: code/modules/mob/living/basic/lavaland/goldgrub/goldgrub_abilities.dm:81
    public override async DmTask<object> phased_check(mob_living user = null, object direction = null)
    {
        object dot = await base.phased_check(user, direction);

        if (!Dm.truthy(dot))
            return dot;

        if (!ismineralturf(dot) && !isasteroidturf(dot))
        {
            to_chat(user, span_warning(I18n.T("You cannot dig through this floor!")));
            return null;
        }
        return dot;
    }
}
