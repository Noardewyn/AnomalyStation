// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/lavaland/cain_and_abel/dagger_throw.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file.

// tg: code/modules/mining/lavaland/cain_and_abel/dagger_throw.dm:4
public partial class obj_item_cain_and_abel : obj_item
{
    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_throw.dm:1-2 (file-local #defines)
    private const float THROW_CRYSTALS_COOLDOWN = 15 * SECONDS;
    private const float THROW_LAUNCH_COOLDOWN = 7 * SECONDS;

    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_throw.dm:22 (var/static/list/throw_options)
    public static DmList throw_options => TgState.Current.obj_item_cain_and_abel__on_thrown__throw_options ??= Dm.list(
        Dm.kv(THROW_MODE_LAUNCH, Dm.list(
            Dm.kv("cooldown", THROW_LAUNCH_COOLDOWN),
            Dm.kv("projectile", typeof(obj_projectile_dagger_launch)))),
        Dm.kv(THROW_MODE_CRYSTALS, Dm.list(
            Dm.kv("cooldown", THROW_CRYSTALS_COOLDOWN),
            Dm.kv("projectile", typeof(obj_projectile_dagger_crystal)))));

    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_throw.dm:4
    public override async DmTask<object> on_thrown(mob_living_carbon user = null, atom target = null)
    {
        if (!(throw_cooldown <= world.time)) // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline
        {
            user.balloon_alert(user, "on cooldown!");
            return null;
        }

        if (Dm.truthy(user.incapacitated) || HAS_TRAIT(user, TRAIT_NO_THROWING) || !isturf(user.loc) || user.buckled != null)
        {
            user.balloon_alert(user, "unable!");
            return null;
        }

        if (!Dm.truthy(check_wield(user)))
        {
            user.balloon_alert(user, "offhand busy!");
            return null;
        }

        if (Dm.num(get_dist(target, user)) > 9)
        {
            user.balloon_alert(user, "too far away!");
            return null;
        }

        DmList throw_settings = throw_options[throw_mode] as DmList;
        throw_cooldown = world.time + Dm.num(throw_settings["cooldown"]); // COOLDOWN_START, inline
        atom dagger = await user.fire_projectile(throw_settings["projectile"], target, Dm.resource("sound/items/weapons/fwoosh.ogg"), user) as atom;
        if (dagger == null)
            return null;

        set_dagger_icon(thrown: TRUE); //when we throw a dagger, we'll only be holding 1
        user.Beam(dagger, icon_state: "chain", icon: Dm.resource("icons/obj/mining_zones/artefacts.dmi"), maxdistance: 9, layer: BELOW_MOB_LAYER);
        RegisterSignal(dagger, COMSIG_QDELETING, nameof(reset_dagger_icon));
        RegisterSignal(dagger, COMSIG_PROJECTILE_SELF_ON_HIT, nameof(on_dagger_hit));
        return null;
    }

    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_throw.dm:44
    [SignalHandler]
    public virtual void on_dagger_hit(obj_projectile_dagger source = null, atom_movable firer = null, atom target = null, object Angle = null)
    {
        UnregisterSignal(source, Dm.list(COMSIG_QDELETING, COMSIG_PROJECTILE_SELF_ON_HIT));
        if (!ismob(loc))
            return;

        atom dagger_visual = source.dagger_effects(target);
        if (!QDELETED(dagger_visual))
        {
            RegisterSignal(dagger_visual, COMSIG_QDELETING, nameof(reset_dagger_icon));
            return;
        }

        set_dagger_icon(thrown: FALSE);
    }

    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_throw.dm:58
    public virtual void set_dagger_icon(float? thrown = null)
    {
        thrown ??= FALSE;
        dagger_thrown = Dm.num(thrown);
        update_dagger_icon();
        Waiting.NoSleep(update_inhand_icon());
    }

    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_throw.dm:63
    [SignalHandler]
    public virtual void reset_dagger_icon(datum source = null)
    {
        set_dagger_icon(thrown: FALSE);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mining/lavaland/cain_and_abel/dagger_throw.dm:22 (var/static/list/throw_options)
    internal DmList obj_item_cain_and_abel__on_thrown__throw_options;
}
