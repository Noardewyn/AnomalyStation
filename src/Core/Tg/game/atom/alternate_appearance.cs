// Translated from tgstation (snapshot 1d99bd7f): code/game/atom/alternate_appearance.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M8 slice G, D187): the atom's procs and the alternate appearance HUD with the basic one and the one-person kinds (the invisible
// floating eyes and dust of the hallucinations: an override image shown to everyone but the seer). Marked leaves, not-translated.md section 56:
// the copy of the target's overlays (AA_MATCH_TARGET_OVERLAYS), the ghost version (basic/everyone) and the kinds for AIs, observers,
// cultists, antagonists and heretics (their roles: antagonists, M10); the blessed tiles' came with gaps RG3. The generated var `image` of the basic kind is a /type
// (the types tool read every `image = /path` of tg as a typepath var), so the image is `alt_image` here.

public partial class datum_controller_global_vars
{
    // tg: code/game/atom/alternate_appearance.dm:1  GLOBAL_LIST_EMPTY(active_alternate_appearances)
    public DmList active_alternate_appearances;

    // tg: code/game/atom/alternate_appearance.dm:1  (GLOBAL_MANAGED(active_alternate_appearances, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalactive_alternate_appearances()
    {
        active_alternate_appearances = Dm.list();
        Dm.append(ref gvars_datum_init_order, "active_alternate_appearances");
    }
}

// tg: code/game/atom/alternate_appearance.dm:3
public partial class atom : datum
{
    // tg: code/game/atom/alternate_appearance.dm:4
    public DmList alternate_appearances;
}

// tg: code/game/atom/alternate_appearance.dm:5
public partial class atom : datum
{
    // tg: code/game/atom/alternate_appearance.dm:5
    public virtual void remove_alt_appearance(object key = null)
    {
        if (Dm.truthy(alternate_appearances))
        {
            foreach (object K in Dm.each(alternate_appearances.Copy()))
            {
                datum_atom_hud_alternate_appearance AA = alternate_appearances[K] as datum_atom_hud_alternate_appearance;
                if (Dm.eq(AA.appearance_key, key))
                {
                    AA.remove_atom_from_hud(this);
                    break;
                }
            }
        }
    }

    // tg: code/game/atom/alternate_appearance.dm:14
    [DmParams("type", "key")]
    public virtual object add_alt_appearance(object type = null, object key = null, params object[] __extra)
    {
        if (!Dm.truthy(type) || !Dm.truthy(key))
            return null;
        if (Dm.truthy(alternate_appearances) && Dm.truthy(alternate_appearances[key]))
            return null;
        if (!ispath(type as Type, typeof(datum_atom_hud_alternate_appearance)))
            throw Dm.CRASH(Dm.text("Invalid type passed in: []", type), "code/game/atom/alternate_appearance.dm", 20);

        DmList arguments = Dm.args(type, key, __extra).Copy(2);
        return Dm.@new(type as Type, Dm.arglist(arguments));
    }
}

// tg: code/game/atom/alternate_appearance.dm:25
[DmPath("/datum/atom_hud/alternate_appearance")]
public partial class datum_atom_hud_alternate_appearance : datum_atom_hud
{
    // tg: code/game/atom/alternate_appearance.dm:29
    [DmParams("key")]
    public override async DmTask New(params object[] __extra)
    {
        object key = Dm.arg(__extra, 0);
        // We use hud_icons to register our hud, so we need to do this before the parent call
        appearance_key = key;
        hud_icons = Dm.list(appearance_key);
        await base.New(__extra);

        Dm.append(GLOB.active_alternate_appearances, this);

        foreach (object mob in Dm.each(GLOB.player_list.Copy()))
            apply_to_new_mob(mob as mob);
    }

    // tg: code/game/atom/alternate_appearance.dm:41
    public override object Destroy(float? force = null)
    {
        Dm.remove(GLOB.active_alternate_appearances, this);
        return base.Destroy(force);
    }

    /// Wrapper for applying this alt hud to the passed mob (if they should see it)
    // tg: code/game/atom/alternate_appearance.dm:46
    public virtual object apply_to_new_mob(mob applying_to = null)
    {
        if (Dm.truthy(mobShouldSee(applying_to)))
        {
            if (!Dm.truthy(hud_users_all_z_levels[applying_to]))
                show_to(applying_to);
            return TRUE;
        }
        return FALSE;
    }

    /// Checks if the passed mob should be seeing this hud
    // tg: code/game/atom/alternate_appearance.dm:54
    public virtual object mobShouldSee(mob M = null)
    {
        return FALSE;
    }

    // tg: code/game/atom/alternate_appearance.dm:57
    public override void show_to(mob new_viewer = null)
    {
        base.show_to(new_viewer);
        if (new_viewer == null)
            return;
        track_mob(new_viewer);
    }

    /// Registers some signals to track the mob's state to determine if they should be seeing the hud still
    // tg: code/game/atom/alternate_appearance.dm:64
    public virtual void track_mob(mob new_viewer = null)
    {
        return;
    }

    // tg: code/game/atom/alternate_appearance.dm:67
    public override void hide_from(mob former_viewer = null, float? absolute = null)
    {
        base.hide_from(former_viewer, absolute);
        if (former_viewer == null || Dm.num(hud_atoms_all_z_levels[former_viewer]) >= 1)
            return;
        untrack_mob(former_viewer);
    }

    /// Unregisters the signals that were tracking the mob's state
    // tg: code/game/atom/alternate_appearance.dm:74
    public virtual void untrack_mob(mob former_viewer = null)
    {
        return;
    }

    // tg: code/game/atom/alternate_appearance.dm:77
    [SignalHandler]
    public virtual void check_hud(mob source = null)
    {
        // Attempt to re-apply the hud entirely
        if (!Dm.truthy(apply_to_new_mob(source)))
        {
            // If that failed, probably shouldn't be seeing it at all, so nuke it
            hide_from(source, absolute: TRUE);
        }
    }

    // tg: code/game/atom/alternate_appearance.dm:84
    public override object add_atom_to_hud(atom A = null, params object[] __extra)
    {
        object dot = base.add_atom_to_hud(A, __extra);
        if (Dm.truthy(dot))
        {
            LAZYINITLIST(ref A.alternate_appearances);
            A.alternate_appearances[appearance_key] = this;
        }
        return dot;
    }

    // tg: code/game/atom/alternate_appearance.dm:90
    public override object remove_atom_from_hud(atom A = null)
    {
        object dot = base.remove_atom_from_hud(A);
        if (Dm.truthy(dot))
            LAZYREMOVE(ref A.alternate_appearances, appearance_key);
        return dot;
    }

    // tg: code/game/atom/alternate_appearance.dm:95
    public virtual void copy_overlays(atom other = null, object cut_old = null)
    {
        return;
    }
}

//an alternate appearance that attaches a single image to a single atom
// tg: code/game/atom/alternate_appearance.dm:98
public partial class datum_atom_hud_alternate_appearance_basic : datum_atom_hud_alternate_appearance
{
    /// The image this appearance attaches (the generated `image` var is a /type, see the file's header)
    internal image alt_image;

    // tg: code/game/atom/alternate_appearance.dm:109
    [DmParams("key", "I", "options")]
    public override async DmTask New(params object[] __extra)
    {
        image I = Dm.arg<image>(__extra, 1);
        float options = Dm.arg(__extra, 2) != null ? Dm.num(Dm.arg(__extra, 2)) : AA_TARGET_SEE_APPEARANCE;
        signals_registering = (DmList)string_list(signals_registering);
        await base.New(__extra);
        transfer_overlays = Dm.bitand(options, AA_MATCH_TARGET_OVERLAYS);
        alt_image = I;
        target = (atom)I.loc;
        LAZYADD(ref target.update_on_z, alt_image);
        if (Dm.truthy(transfer_overlays))
            Dm.Deferred("code/game/atom/alternate_appearance.dm:117", "New: I.copy_overlays(target), the copy of the target's overlays (AA_MATCH_TARGET_OVERLAYS; the alternate appearance's overlay copy, M10+)");

        add_atom_to_hud(target);
        target.set_hud_image_active(appearance_key, exclusive_hud: this);

        if (Dm.truthy(Dm.bitand(options, AA_TARGET_SEE_APPEARANCE)) && ismob(target))
            show_to((mob)target);
        if (Dm.truthy(add_ghost_version))
            Dm.Deferred("code/game/atom/alternate_appearance.dm:126", "New: the ghost version of an alternate appearance (basic/observers, ghosts' sight of it, M10+)");
    }

    // tg: code/game/atom/alternate_appearance.dm:133
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        LAZYREMOVE(ref target.update_on_z, alt_image);
        alt_image = null;
        target = null;
        if (ghost_appearance != null)
            qdel(ghost_appearance);
        ghost_appearance = null;
        return dot;
    }

    // tg: code/game/atom/alternate_appearance.dm:141
    public override void track_mob(mob new_viewer = null)
    {
        RegisterSignals(new_viewer, signals_registering, nameof(check_hud), @override: TRUE);
    }

    // tg: code/game/atom/alternate_appearance.dm:144
    public override void untrack_mob(mob former_viewer = null)
    {
        UnregisterSignal(former_viewer, signals_registering);
    }

    // tg: code/game/atom/alternate_appearance.dm:147
    public override object add_atom_to_hud(atom A = null, params object[] __extra)
    {
        LAZYINITLIST(ref A.hud_list);
        A.hud_list[appearance_key] = alt_image;
        return base.add_atom_to_hud(A, __extra);
    }

    // tg: code/game/atom/alternate_appearance.dm:151
    public override object remove_atom_from_hud(atom A = null)
    {
        object dot = base.remove_atom_from_hud(A);
        LAZYREMOVE(ref A.hud_list, appearance_key);
        A.set_hud_image_inactive(appearance_key);
        if (Dm.truthy(dot) && !QDELETED(this))
            qdel(this);
        return dot;
    }

    // tg: code/game/atom/alternate_appearance.dm:158
    public override void copy_overlays(atom other = null, object cut_old = null)
    {
        Dm.Deferred("code/game/atom/alternate_appearance.dm:159", "copy_overlays: image.copy_overlays(other, cut_old) (the overlay copy, M10+)");
    }
}

// Syndicate G3c (D316): the kind shown to everyone alive (the cardboard cutout's disguise). add_ghost_version is TRUE: the ghost
// version is the leaf of basic's New above.
// tg: code/game/atom/alternate_appearance.dm:162
public partial class datum_atom_hud_alternate_appearance_basic_everyone : datum_atom_hud_alternate_appearance_basic
{
    // tg: code/game/atom/alternate_appearance.dm:165
    public override object mobShouldSee(mob M = null)
    {
        return isdead(M) ? FALSE : TRUE;
    }
}

// Gaps CL1 (D333): the kind only silicons see (the cardborg suit's robot look).
// tg: code/game/atom/alternate_appearance.dm:168
public partial class datum_atom_hud_alternate_appearance_basic_silicons : datum_atom_hud_alternate_appearance_basic
{
    // tg: code/game/atom/alternate_appearance.dm:170
    public override object mobShouldSee(mob M = null)
    {
        if (issilicon(M))
            return TRUE;
        return FALSE;
    }
}

// The kind only AIs see (the circuits' B.O.R.I.S. container's tracking mark).
// tg: code/game/atom/alternate_appearance.dm:175
public partial class datum_atom_hud_alternate_appearance_basic_ais : datum_atom_hud_alternate_appearance_basic
{
    // tg: code/game/atom/alternate_appearance.dm:177
    public override object mobShouldSee(mob M = null)
    {
        return Dm.num(isAI(M));
    }
}

// Gaps RG3 (D339): the blessed tiles' glow, seen by whoever has the trait to see them (the chaplain's and the wizard's).
// tg: code/game/atom/alternate_appearance.dm:194
public partial class datum_atom_hud_alternate_appearance_basic_blessed_aware : datum_atom_hud_alternate_appearance_basic
{
    // tg: code/game/atom/alternate_appearance.dm:203
    public override object mobShouldSee(mob viewing_mob = null)
    {
        if (viewing_mob.mind == null)
            return FALSE;
        if (HAS_MIND_TRAIT(viewing_mob, TRAIT_SEE_BLESSED_TILES))
            return TRUE;
        return FALSE;
    }
}

/// Only shows the image to one person
// tg: code/game/atom/alternate_appearance.dm:206
public partial class datum_atom_hud_alternate_appearance_basic_one_person : datum_atom_hud_alternate_appearance_basic
{
    // tg: code/game/atom/alternate_appearance.dm:211
    public override object mobShouldSee(mob M = null)
    {
        return Dm.num(M == seer);
    }

    // tg: code/game/atom/alternate_appearance.dm:214
    [DmParams("key", "I", "options", "seer")]
    public override async DmTask New(params object[] __extra)
    {
        this.seer = Dm.arg<mob>(__extra, 3);
        // options = NONE when omitted (tg's default of the one-person kind)
        object[] forwarded = Dm.forward(__extra, Dm.arg(__extra, 0), Dm.arg(__extra, 1), Dm.arg(__extra, 2) ?? NONE);
        await base.New(forwarded);
    }
}

/// Shows the image to everyone but one person
// tg: code/game/atom/alternate_appearance.dm:219
public partial class datum_atom_hud_alternate_appearance_basic_one_person_reversed : datum_atom_hud_alternate_appearance_basic_one_person
{
    // tg: code/game/atom/alternate_appearance.dm:221
    public override object mobShouldSee(mob M = null)
    {
        return Dm.num(M != seer);
    }
}
