// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/phased_mob.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C6). The position indicator is an image shown to the jaunter's client alone: the 3D client's view follows the holder (reset_perspective).

// tg: code/game/objects/effects/phased_mob.dm:1
public partial class obj_effect_dummy_phased_mob : obj_effect_dummy
{
    // tg: code/game/objects/effects/phased_mob.dm:21
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        atom_movable new_jaunter = Dm.arg(__extra, 0) as atom_movable;
        if (new_jaunter != null)
            Waiting.NoSleep(set_jaunter(new_jaunter));
        return dot;
    }

    /// Sets new_jaunter as our jaunter, forcemoves them into our contents
    // tg: code/game/objects/effects/phased_mob.dm:27
    public virtual async DmTask set_jaunter(atom_movable new_jaunter = null)
    {
        jaunter = new_jaunter;
        await jaunter.forceMove(this);
        if (jaunter is not mob mob_jaunter)
            return;
        position_indicator = image(phased_mob_icon, this, phased_mob_icon_state, ABOVE_LIGHTING_PLANE) as image;
        position_indicator.appearance_flags = Dm.bitor(position_indicator.appearance_flags, RESET_ALPHA);
        RegisterSignal(mob_jaunter, COMSIG_MOB_LOGIN, nameof(show_client_image));
        RegisterSignal(mob_jaunter, COMSIG_MOB_STATCHANGE, nameof(on_stat_change));
        await mob_jaunter.reset_perspective(this);
        show_client_image(mob_jaunter);
    }

    /// Displays our position indicator to a client
    // tg: code/game/objects/effects/phased_mob.dm:42
    [SignalHandler]
    public virtual void show_client_image(mob show_to = null)
    {
        if (show_to.client != null)
        {
            DmList images = show_to.client.images;
            Dm.combine(ref images, position_indicator);
        }
    }

    // tg: code/game/objects/effects/phased_mob.dm:46
    public override object Destroy(float? force = null)
    {
        jaunter = null; // If a mob was left in the jaunter on qdel, they'll be dumped into nullspace
        position_indicator = null;
        return base.Destroy(force);
    }

    /// Removes jaunter from our phased mob
    // tg: code/game/objects/effects/phased_mob.dm:52
    public virtual async DmTask eject_jaunter()
    {
        if (jaunter == null)
            return; // This is weird but it can happen if the jaunt is gibbed by an arriving shuttle
        turf eject_spot = get_turf(this);
        if (eject_spot == null) //You're in nullspace you clown!
            return;

        area destination_area = get_area(eject_spot) as area;
        if (Dm.truthy(Dm.bitand(destination_area.area_flags, NOTELEPORT)))
        {
            // this ONLY happens if someone uses a phasing effect
            // to try to land in a NOTELEPORT zone after it is created, AKA trying to exploit.
            if (jaunter is mob_living living_cheaterson)
            {
                to_chat(living_cheaterson, span_userdanger("This area has a heavy universal force occupying it, and you are scattered to the cosmos!"));
                if (ishuman(living_cheaterson))
                {
                    shake_camera(living_cheaterson, 20, 1);
                    _addtimer(Dm.@new<datum_callback>(living_cheaterson, nameof(mob_living_carbon.vomit)), 2 * SECONDS, file: "code/game/objects/effects/phased_mob.dm", line: 68);
                }
                await jaunter.forceMove(find_safe_turf(z) as atom);
            }
        }
        else
            await jaunter.forceMove(eject_spot);
        qdel(this);
    }

    // tg: code/game/objects/effects/phased_mob.dm:74
    public override async DmTask<object> Exited(atom_movable gone = null, object direction = null)
    {
        object dot = await base.Exited(gone, direction);
        if (gone == jaunter)
        {
            UnregisterSignal(jaunter, COMSIG_MOB_STATCHANGE);
            UnregisterSignal(jaunter, COMSIG_MOB_LOGIN);
            if (Dm.truthy(_listen_lookup?[COMSIG_MOB_EJECTED_FROM_JAUNT]))
                _SendSignal(COMSIG_MOB_EJECTED_FROM_JAUNT, Dm.list(this, jaunter));
            jaunter = null;
        }
        return dot;
    }

    // tg: code/game/objects/effects/phased_mob.dm:82
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        return FALSE;
    }

    // tg: code/game/objects/effects/phased_mob.dm:85
    public override async DmTask<object> projectile_hit(obj_projectile hitting_projectile = null, object def_zone = null, float? piercing_hit = null, object blocked = null)
    {
        return BULLET_ACT_FORCE_PIERCE;
    }

    // tg: code/game/objects/effects/phased_mob.dm:88
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        turf newloc = await phased_check(user, direction) as turf;
        if (newloc == null)
            return null;

        if (Dm.@in(direction, GLOB.alldirs))
            await setDir(direction);
        await forceMove(newloc);
        return null;
    }

    /// Checks if the conditions are valid to be able to phase. Returns a turf destination if positive.
    // tg: code/game/objects/effects/phased_mob.dm:98
    public virtual async DmTask<object> phased_check(mob_living user = null, object direction = null)
    {
        if (movedelay > world.time || !Dm.truthy(direction))
            return null;
        // shortcut: get_step_multiz's UP and DOWN need multi-z (one level on the maps run today), upgrade with multi-z
        turf newloc = get_step(this, Dm.num(direction)) as turf;
        if (newloc == null)
            return null;
        area destination_area = newloc.loc as area;
        movedelay = world.time + movespeed;

        if (Dm.truthy(Dm.bitand(!Dm.truthy(_listen_lookup?[COMSIG_MOB_PHASED_CHECK]) ? NONE : _SendSignal(COMSIG_MOB_PHASED_CHECK, Dm.list(this, user, newloc)), COMPONENT_BLOCK_PHASED_MOVE)))
            return null;

        if (Dm.truthy(Dm.bitand(newloc.turf_flags, NOJAUNT)))
        {
            to_chat(user, span_warning("Some strange aura is blocking the way."));
            return null;
        }
        if (Dm.truthy(Dm.bitand(destination_area.area_flags, NOTELEPORT)) || Dm.truthy(SSmapping.level_trait(newloc.z, ZTRAIT_NOPHASE)))
        {
            to_chat(user, span_danger("Some dull, universal force is blocking the way. Its overwhelmingly oppressive force feels dangerous."));
            return null;
        }

        return newloc;
    }

    /// Signal proc for COMSIG_MOB_STATCHANGE, to throw us out of the jaunt if we lose consciousness.
    // tg: code/game/objects/effects/phased_mob.dm:123
    [SignalHandler]
    public virtual void on_stat_change(mob_living source = null, object new_stat = null, object old_stat = null)
    {
        if (source == jaunter && IS_UNCONSCIOUS_OR_CRIT(source))
            Waiting.Detach(eject_jaunter());
    }
}

// tg: code/game/objects/effects/effects.dm:51
public partial class obj_effect_dummy : obj_effect
{
    // tg: code/game/objects/effects/effects.dm:58
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        return null;
    }

    // tg: code/game/objects/effects/effects.dm:61
    public override async DmTask<object> singularity_act()
    {
        return null;
    }
}
