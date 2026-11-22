// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/illusion/illusion.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps WP1 (D333): the file whole, for the Grey Tide spear (weaponry/melee/spear.dm). The illusion takes the look of the mob it
// mocks (its appearance, its facing; examined, it reads as that mob), joins that mob's factions and allies, chases the target it is
// given with its controller's simple hostile tree, and vanishes at the end of its life. tg's mock_as tests isnull() where it means
// !isnull() (hp, damage, replicate), so an illusion keeps its own 100 health, 5 damage and no replication whatever it is given:
// translated as written. The var block is generated (D111).

/// Pretty genericky mob that just manifests itself as any sort of input living mob, used in several ways
/// Has a basic AI that helps it attack targets that are assigned to it, but there's more flavor in the subtypes.
// tg: code/modules/mob/living/basic/illusion/illusion.dm:3
public partial class mob_living_basic_illusion : mob_living_basic
{
    // tg: code/modules/mob/living/basic/illusion/illusion.dm:31
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_HOSTILE_PRE_ATTACKINGTARGET, nameof(on_preattack));
        RegisterSignal(this, COMSIG_MOB_AFTER_APPLY_DAMAGE, nameof(on_damage_taken));
        return dot;
    }

    // tg: code/modules/mob/living/basic/illusion/illusion.dm:36
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        mob_living parent_mob = parent_mob_ref?.resolve() as mob_living;
        if (parent_mob != null)
            return await parent_mob.examine(user);
        return await base.examine(user, __extra);
    }

    /// Signal handler for when we attack something. Hook for replicating on standard behavior, with additional behavior on subtypes.
    // tg: code/modules/mob/living/basic/illusion/illusion.dm:43
    [SignalHandler]
    public virtual object on_preattack(mob_living source = null, atom attacked_target = null)
    {
        try_replicate();
        return null;
    }

    /// Signal handler for when we are attacked.
    // tg: code/modules/mob/living/basic/illusion/illusion.dm:48
    [SignalHandler]
    public virtual void on_damage_taken(mob_living source = null)
    {
        try_replicate();
    }

    /// Full setup for illusion mobs to lessen code duplication in the individual files.
    // tg: code/modules/mob/living/basic/illusion/illusion.dm:53
    public virtual async DmTask full_setup(mob_living original = null, mob_living target_mob = null, DmList faction_override = null, object life = null, object hp = null, object damage = null, object replicate = null)
    {
        life ??= 5 * SECONDS;
        hp ??= 100;
        damage ??= 0;
        replicate ??= 0;
        await mock_as(original, life, hp, damage, replicate);
        // SET_FACTION_AND_ALLIES_FROM(src, original) (__DEFINES/mobfactions.dm:119), inline
        set_faction(original.get_faction());
        set_allies(LAZYLISTDUPLICATE(original.allies));
        if (faction_override != null)
            set_faction(faction_override);
        set_target(target_mob);
    }

    /// Gives the illusion a target to focus on in whatever behavior it wants to engage as.
    // tg: code/modules/mob/living/basic/illusion/illusion.dm:61
    public virtual void set_target(mob_living target_mob = null)
    {
        if (target_mob == null)
            return;
        ((datum_ai_controller)ai_controller).set_blackboard_key(target_key, target_mob);
    }

    /// Does the actual work of setting up the illusion's appearance and some other functionality.
    // tg: code/modules/mob/living/basic/illusion/illusion.dm:67
    public virtual async DmTask mock_as(mob_living original = null, object life = null, object hp = null, object damage = null, object replicate = null)
    {
        if (QDELETED(original))
            return;

        parent_mob_ref = (datum_weakref)WEAKREF(original);
        appearance = original.appearance;
        await setDir(original.dir);

        if (hp == null)
        {
            maxHealth = Dm.num(hp);
            await updatehealth(); // re-cap health to new value
        }

        if (damage == null)
        {
            melee_damage_lower = Dm.num(damage);
            melee_damage_upper = Dm.num(damage);
        }

        if (replicate == null)
            multiply_chance = Dm.num(replicate);

        remove_faction(FACTION_NEUTRAL);
        transform = Dm.initial(this, (atom p) => p.transform);
        pixel_x = base_pixel_x;
        pixel_y = base_pixel_y;

        _addtimer(Dm.@new<datum_callback>(this, nameof(mob_living.death)), Dm.num(life), file: "code/modules/mob/living/basic/illusion/illusion.dm", line: 91);
    }

    /// See if we are able to replicate, and if so, do it.
    // tg: code/modules/mob/living/basic/illusion/illusion.dm:94
    public virtual void try_replicate()
    {
        if (prob(multiply_chance))
            Waiting.NoSleep(replicate());
    }

    /// Actually perform the replication of this illusion.
    // tg: code/modules/mob/living/basic/illusion/illusion.dm:99
    public virtual async DmTask replicate()
    {
        mob_living parent_mob = parent_mob_ref.resolve() as mob_living;
        if (QDELETED(parent_mob))
            return;
        mob_living_basic_illusion new_clone = Dm.@new<mob_living_basic_illusion>(loc);
        await new_clone.full_setup(
            parent_mob,
            target_mob: ((datum_ai_controller)ai_controller).blackboard[target_key] as mob_living,
            faction_override: faction,
            life: 8 * SECONDS,
            hp: health / 2,
            damage: melee_damage_upper,
            replicate: multiply_chance / 2);
    }
}
