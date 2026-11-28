using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// DEVIATION(DV697) (ui-overhaul.md section 12, slice 4E; protocol version 100's Interact): the world interact key, Space Station 14's
// ActivateItemInWorld in tg's terms: tg's left click with an empty hand on the thing under the crosshair, also while the active hand holds an
// item (tg has no such key: its click with an item in hand uses the item). Host code, not tg code: it runs the plain left click's part of tg's
// ClickOn (code/_onclick/click.dm:71) with the item left out.

public static class WorldInteract
{
    /// <summary>The key's own refusals (before tg's click gates): false on a mob while in combat mode, and on an item lying elsewhere while the active hand is full.</summary>
    public static bool Acts(mob_living user, atom target)
        => !(target is mob && Dm.truthy(user.combat_mode)) && !(target is obj_item item && user.get_active_held_item() != null && !Dm.truthy(user.is_holding(item)));

    /// <summary>
    /// The player pressed the world interact key on <paramref name="target"/>: only a connected client's own living mob; never on a mob while
    /// in combat mode (the key never hits), never on an item while the active hand is full (tg's pick-up would drop it at the feet). Runs
    /// detached with usr the mob, as BYOND runs a click.
    /// </summary>
    public static void Use(client player, atom target)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!player.dm_connected || player.gc_destroyed != null || player.mob is not mob_living user || QDELETED(user) || target == null || target.gc_destroyed != null)
            return;
        if (!Acts(user, target))
            return;
        World world = World.Current;
        object caller_usr = world.usr;
        try
        {
            world.usr = user;
            Waiting.Detach(UseAsync(user, target));
        }
        finally
        {
            world.usr = caller_usr;
        }
    }

    // tg: code/_onclick/click.dm:71 (mob/ClickOn): the gates of a plain left click, then UnarmedAttack where tg's click with an empty hand
    // reaches it; out of reach nothing (tg's RangedAttack of an empty hand is not a use)
    private static async DmTask UseAsync(mob_living user, atom A)
    {
        if (world.time <= user.next_click)
            return;
        user.next_click = world.time + 1;
        if (DevCommands.ShieldsFrom(user, A))
            return; // DEVIATION(DV258): the dev invulnerability
        if (Dm.truthy(await user.check_click_intercept(null, A)) || HAS_TRAIT(user, TRAIT_NO_TRANSFORM))
            return;
        DmList modifiers = Dm.list(Dm.kv(LEFT_CLICK, "1"), Dm.kv(BUTTON, LEFT_CLICK));
        if (Dm.truthy(INCAPACITATED_IGNORING(user, Dm.bitor(INCAPABLE_RESTRAINTS, INCAPABLE_STASIS))))
            return;
        user.face_atom(A);
        if (Dm.gt(user.next_move, world.time))
            return;
        if (Dm.truthy(A.IsObscured()))
            return;
        if (HAS_TRAIT(user, TRAIT_HANDS_BLOCKED))
        {
            user.changeNext_move(CLICK_CD_HANDCUFFED);
            await user.UnarmedAttack(A, await user.Adjacent(A), modifiers);
            return;
        }
        if (Dm.@in(A, user.DirectAccess()))
        {
            if (ismob(A))
                user.changeNext_move(CLICK_CD_MELEE);
            await user.UnarmedAttack(A, Dm.TRUE, modifiers);
            return;
        }
        if (!Dm.truthy(user.loc.AllowClick()))
            return;
        if (A is obj_item item_atom && Dm.truthy(Dm.bitand(item_atom.item_flags, IN_STORAGE)) && Dm.truthy(Dm.bitand(item_atom.loc.flags_1, HAS_DISASSOCIATED_STORAGE_1)))
            await user.UnarmedAttack(item_atom, Dm.TRUE, modifiers); // as tg's ClickOn, which goes on to the reach test after it
        if (!Dm.truthy(await A.IsReachableBy(user, null)))
            return;
        if (ismob(A))
            user.changeNext_move(CLICK_CD_MELEE);
        await user.UnarmedAttack(A, Dm.TRUE, modifiers);
    }
}
