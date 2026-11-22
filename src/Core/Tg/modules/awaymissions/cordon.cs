// Translated from tgstation (snapshot 1d99bd7f): code/modules/awaymissions/cordon.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice G1 (D272): the whole file, the border of tg's turf reservations (the shuttles' transit docks); attack_ghost of the
// secret cordon came with the ghost's click (E2, D287). rust_heretic_act of the cordon has no /atom root yet (the heretics).

/// Turf type that appears to be a world border, completely impassable and non-interactable to all physical (alive) entities.
// tg: code/modules/awaymissions/cordon.dm:2
[DmPath("/turf/cordon")]
public partial class turf_cordon : turf
{
}

/// This type of cordon will block ghosts from passing through it. Useful for stuff like Away Missions, where you feasibly want to block ghosts from entering to keep a certain map section a secret.
// tg: code/modules/awaymissions/cordon.dm:71
[DmPath("/turf/cordon/secret")]
public partial class turf_cordon_secret : turf_cordon
{
    // tg: code/modules/awaymissions/cordon.dm:74
    public override async DmTask<object> attack_ghost(mob_dead_observer user = null)
    {
        return FALSE;
    }
}

// tg: code/modules/awaymissions/cordon.dm:19 (interleaved types, rules 1.9; M5x slice 5, M10 slice G1)
public partial class turf_cordon : turf
{
    // tg: code/modules/awaymissions/cordon.dm:19
    public override void AfterChange(object flags = null, object oldType = null)
    {
        base.AfterChange(flags, oldType);
        Dm.remove(ref SSair.high_pressure_delta, this);
    }

    // tg: code/modules/awaymissions/cordon.dm:26
    [DmParams("acidpwr", "acid_volume", "acid_id")]
    public override async DmTask<object> acid_act(object acidpwr = null, object acid_volume = null, params object[] __extra)
    {
        return FALSE;
    }

    // tg: code/modules/awaymissions/cordon.dm:29
    public override async DmTask<object> Melt()
    {
        to_be_destroyed = FALSE;
        return this;
    }

    // tg: code/modules/awaymissions/cordon.dm:33
    public override async DmTask<object> singularity_act()
    {
        return FALSE;
    }

    // tg: code/modules/awaymissions/cordon.dm:36
    public override async DmTask<object> ScrapeAway(float? amount = null, object flags = null)
    {
        return this; // :devilcat:
    }

    // tg: code/modules/awaymissions/cordon.dm:39
    public override async DmTask<object> TerraformTurf(object path = null, object new_baseturf = null, object flags = null, params object[] __extra)
    {
        return null;
    }

    // tg: code/modules/awaymissions/cordon.dm:42
    public override async DmTask<object> bullet_act(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, float? blocked = null)
    {
        return BULLET_ACT_BLOCK; // SHOULD_CALL_PARENT(FALSE) // Fuck you
    }

    // tg: code/modules/awaymissions/cordon.dm:46
    public override async DmTask<object> Adjacent(atom neighbor = null, atom target = null, atom_movable mover = null, params object[] __extra)
    {
        return FALSE;
    }

    // tg: code/modules/awaymissions/cordon.dm:49
    public override object Bumped(atom_movable bumped_atom = null)
    {
        object dot = base.Bumped(bumped_atom);

        if (HAS_TRAIT(bumped_atom, TRAIT_FREE_HYPERSPACE_SOFTCORDON_MOVEMENT)) //we could feasibly reach the border, so just don't
            dump_in_space(bumped_atom);
        return dot;
    }
}

/// Area used in conjunction with the cordon turf to create a fully functioning world border.
// tg: code/modules/awaymissions/cordon.dm:56
public partial class area_misc_cordon : area_misc
{
    // tg: code/modules/awaymissions/cordon.dm:64
    public override async DmTask<object> Entered(atom_movable arrived = null, atom old_area = null, DmList old_locs = null)
    {
        object dot = await base.Entered(arrived, old_area, old_locs);
        foreach (mob_living enterer in Dm.each<mob_living>(arrived.get_all_contents_type(typeof(mob_living))))
        {
            to_chat(enterer, span_userdanger("This was a bad idea..."));
            await enterer.dust(just_ash: TRUE, drop_items: FALSE, force: TRUE);
        }
        return dot;
    }
}
