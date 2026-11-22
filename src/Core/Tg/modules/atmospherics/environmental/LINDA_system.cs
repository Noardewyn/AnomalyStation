// Translated from tgstation (snapshot 1d99bd7f): code/modules/atmospherics/environmental/LINDA_system.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Translated (M4b slice 1, D134: the air that moves; before it, W4.0 contracts: the /atom var and proc can_atmos_pass and a
// deferred air_update_turf). The var overrides of /turf (:15) and /turf/open (:18) are in the constructors of
// game/turfs/turf.cs (dm_tg_vars) and game/turfs/open/_open.cs (rules 10.2: one place per type).

// tg: code/modules/atmospherics/environmental/LINDA_system.dm:1
public partial class atom : datum
{
    ///Check if atmos can pass in this atom (ATMOS_PASS_YES, ATMOS_PASS_NO, ATMOS_PASS_DENSITY, ATMOS_PASS_PROC)
    // tg: code/modules/atmospherics/environmental/LINDA_system.dm:3
    public float can_atmos_pass = ATMOS_PASS_YES;

    // tg: code/modules/atmospherics/environmental/LINDA_system.dm:5  (the proc of the same name as the var: rules 2.4)
    public virtual object can_atmos_pass__proc(turf target_turf = null, float? vertical = null)
    {
        vertical ??= FALSE;
        switch (can_atmos_pass)
        {
            case ATMOS_PASS_PROC:
                return ATMOS_PASS_YES;
            case ATMOS_PASS_DENSITY:
                return Dm.not(density);
            default:
                return can_atmos_pass;
        }
    }
}

// tg: code/modules/atmospherics/environmental/LINDA_system.dm:20  (interleaved types, rules 1.9)
public partial class turf_open : turf
{
    ///Do NOT use this to see if 2 turfs are connected, it mutates state, and we cache that info anyhow.
    ///Use TURFS_CAN_SHARE or TURF_SHARES depending on your usecase
    // tg: code/modules/atmospherics/environmental/LINDA_system.dm:22
    public override object can_atmos_pass__proc(turf target_turf = null, float? vertical = null)
    {
        vertical ??= FALSE;
        object can_pass = TRUE;
        float direction = Dm.num(Dm.truthy(vertical) ? get_dir_multiz(this, target_turf) : get_dir(this, target_turf));
        float opposite_direction = REVERSE_DIR(direction);
        if (Dm.truthy(vertical) && !(Dm.truthy(zAirOut(direction, target_turf)) && Dm.truthy(target_turf.zAirIn(direction, this))))
            can_pass = FALSE;
        if (Dm.truthy(blocks_air) || Dm.truthy(target_turf.blocks_air))
            can_pass = FALSE;
        //This path is a bit weird, if we're just checking with ourselves no sense asking objects on the turf
        if (target_turf == this)
            return can_pass;

        //Can't just return if canpass is false here, we need to set superconductivity
        foreach (obj checked_object in Dm.each<obj>(Dm.add(contents, target_turf.contents)))
        {
            turf other = (checked_object.loc == this ? target_turf : this);
            if (Dm.truthy(CANATMOSPASS(checked_object, other, Dm.num(vertical))))
                continue;
            can_pass = FALSE;
            //the direction and open/closed are already checked on can_atmos_pass() so there are no arguments
            if (Dm.truthy(checked_object.block_superconductivity()))
            {
                Dm.combine(ref atmos_supeconductivity, direction);
                Dm.combine(ref target_turf.atmos_supeconductivity, opposite_direction);
                return FALSE; //no need to keep going, we got all we asked (Is this even faster? fuck you it's soul)
            }
        }

        //Superconductivity is a bitfield of directions we can't conduct with
        //Yes this is really weird. Fuck you
        Dm.mask(ref atmos_supeconductivity, Dm.bitnot(direction));
        Dm.mask(ref target_turf.atmos_supeconductivity, Dm.bitnot(opposite_direction));

        return can_pass;
    }
}

// tg: code/modules/atmospherics/environmental/LINDA_system.dm:53  (interleaved types, rules 1.9)
public partial class atom_movable : atom
{
    // tg: code/modules/atmospherics/environmental/LINDA_system.dm:53
    public virtual object block_superconductivity() // objects that block air and don't let superconductivity act
    {
        return FALSE;
    }
}

// tg: code/modules/atmospherics/environmental/LINDA_system.dm:59  (interleaved types, rules 1.9)
public partial class turf : atom
{
    /// This proc is a more deeply optimized version of immediate_calculate_adjacent_turfs
    /// It contains dumbshit, and also stuff I just can't do at runtime
    /// If you're not editing behavior, just read that proc. It's less bad
    // tg: code/modules/atmospherics/environmental/LINDA_system.dm:59
    public virtual void init_immediate_calculate_adjacent_turfs()
    {
        //Basic optimization, if we can't share why bother asking other people ya feel?
        // You know it's gonna be stupid when they include a unit test in the atmos code
        // Yes, inlining the string concat does save 0.1 seconds
        // :64-67  #ifdef UNIT_TESTS ASSERT(UP == 16) ASSERT(DOWN == 32) #endif: inactive in production (rules 1.7)
        LAZYINITLIST(ref this.atmos_adjacent_turfs);
        DmList atmos_adjacent_turfs = this.atmos_adjacent_turfs;
        object canpass = CANATMOSPASS(this, this, FALSE);
        // I am essentially inlineing two get_dir_multizs here, because they're way too slow on their own. I'm sorry brother
        DmList z_traits = SSmapping.multiz_levels[z] as DmList;
        foreach (object direction_value in Dm.each(GLOB.cardinals_multiz))
        {
            float direction = Dm.num(direction_value);
            // Yes this is a reimplementation of get_step_mutliz. It's faster tho. fuck you
            // Oh also yes UP and DOWN do just point to +1 and -1 and not z offsets
            // Multiz is shitcode welcome home
            turf current_turf = (Dm.truthy(Dm.bitand(direction, Dm.bitor(UP, DOWN))) ?
                (Dm.truthy(Dm.bitand(direction, UP)) ?
                    (Dm.truthy(z_traits[Z_LEVEL_UP]) ?
                        (get_step(locate(x, y, z + 1), NONE) as turf) :
                    null) :
                    (Dm.truthy(z_traits[Z_LEVEL_DOWN]) ?
                        (get_step(locate(x, y, z - 1), NONE) as turf) :
                    null)) :
                (get_step(this, direction) as turf));
            if (!isopenturf(current_turf)) // not interested in you brother
                continue;
            // The assumption is that ONLY DURING INIT if two tiles have the same cycle, there's no way canpass(a->b) will be different then canpass(b->a), so this is faster
            // Saves like 1.2 seconds
            // Note: current cycle here goes DOWN as we sleep. this is to ensure we can use the >= logic in the first step of process_cell
            // It's not a massive thing, and I'm sorry for the cursed code, but it be this way
            if (current_turf.current_cycle <= current_cycle)
                continue;

            //Can you and me form a deeper relationship, or is this just a passing wind
            // (direction & (UP | DOWN)) is just "is this vertical" by the by
            if (Dm.truthy(canpass) && Dm.truthy(CANATMOSPASS(current_turf, this, Dm.bitand(direction, Dm.bitor(UP, DOWN)))) && !(Dm.truthy(blocks_air) || Dm.truthy(current_turf.blocks_air)))
            {
                LAZYINITLIST(ref current_turf.atmos_adjacent_turfs);
                atmos_adjacent_turfs[current_turf] = TRUE;
                current_turf.atmos_adjacent_turfs[this] = TRUE;
            }
            else
            {
                Dm.remove(ref atmos_adjacent_turfs, current_turf);
                if (Dm.truthy(current_turf.atmos_adjacent_turfs))
                    Dm.remove(ref current_turf.atmos_adjacent_turfs, this);
                UNSETEMPTY(ref current_turf.atmos_adjacent_turfs);
            }
            if (Dm.truthy(current_turf._listen_lookup?[COMSIG_TURF_CALCULATED_ADJACENT_ATMOS]))
                current_turf._SendSignal(COMSIG_TURF_CALCULATED_ADJACENT_ATMOS, Dm.list(current_turf));
        }

        UNSETEMPTY(ref atmos_adjacent_turfs);
        this.atmos_adjacent_turfs = atmos_adjacent_turfs;
        if (Dm.truthy(_listen_lookup?[COMSIG_TURF_CALCULATED_ADJACENT_ATMOS]))
            _SendSignal(COMSIG_TURF_CALCULATED_ADJACENT_ATMOS, Dm.list(this));
    }

    // tg: code/modules/atmospherics/environmental/LINDA_system.dm:104
    public virtual void immediate_calculate_adjacent_turfs()
    {
        LAZYINITLIST(ref this.atmos_adjacent_turfs);
        DmList atmos_adjacent_turfs = this.atmos_adjacent_turfs;
        object canpass = CANATMOSPASS(this, this, FALSE);
        foreach (object direction_value in Dm.each(GLOB.cardinals_multiz))
        {
            float direction = Dm.num(direction_value);
            turf current_turf = get_step_multiz(this, direction) as turf;
            if (!isopenturf(current_turf)) // not interested in you brother
                continue;

            //Can you and me form a deeper relationship, or is this just a passing wind
            // (direction & (UP | DOWN)) is just "is this vertical" by the by
            if (Dm.truthy(canpass) && Dm.truthy(CANATMOSPASS(current_turf, this, Dm.bitand(direction, Dm.bitor(UP, DOWN)))) && !(Dm.truthy(blocks_air) || Dm.truthy(current_turf.blocks_air)))
            {
                LAZYINITLIST(ref current_turf.atmos_adjacent_turfs);
                atmos_adjacent_turfs[current_turf] = TRUE;
                current_turf.atmos_adjacent_turfs[this] = TRUE;
            }
            else
            {
                Dm.remove(ref atmos_adjacent_turfs, current_turf);
                if (Dm.truthy(current_turf.atmos_adjacent_turfs))
                    Dm.remove(ref current_turf.atmos_adjacent_turfs, this);
                UNSETEMPTY(ref current_turf.atmos_adjacent_turfs);
            }
            if (Dm.truthy(current_turf._listen_lookup?[COMSIG_TURF_CALCULATED_ADJACENT_ATMOS]))
                current_turf._SendSignal(COMSIG_TURF_CALCULATED_ADJACENT_ATMOS, Dm.list(current_turf));
        }

        UNSETEMPTY(ref atmos_adjacent_turfs);
        this.atmos_adjacent_turfs = atmos_adjacent_turfs;
        if (Dm.truthy(_listen_lookup?[COMSIG_TURF_CALCULATED_ADJACENT_ATMOS]))
            _SendSignal(COMSIG_TURF_CALCULATED_ADJACENT_ATMOS, Dm.list(this));
    }

    /**
     * returns a list of adjacent turfs that can share air with this one.
     * alldir includes adjacent diagonal tiles that can share
     * air with both of the related adjacent cardinal tiles
    **/
    // tg: code/modules/atmospherics/environmental/LINDA_system.dm:136
    public virtual object get_atmos_adjacent_turfs(float? alldir = null)
    {
        alldir ??= 0;
        DmList adjacent_turfs;
        if (Dm.truthy(atmos_adjacent_turfs))
            adjacent_turfs = atmos_adjacent_turfs.Copy();
        else
            adjacent_turfs = Dm.list();

        if (!Dm.truthy(alldir))
            return adjacent_turfs;

        turf current_location = this;

        foreach (object direction in Dm.each(GLOB.diagonals_multiz))
        {
            float matching_directions = 0;
            turf checked_turf = get_step_multiz(current_location, direction) as turf;
            if (checked_turf == null)
                continue;

            foreach (object check_direction in Dm.each(GLOB.cardinals_multiz))
            {
                turf secondary_turf = get_step(checked_turf, Dm.num(check_direction)) as turf;
                if (!Dm.truthy(checked_turf.atmos_adjacent_turfs) || !Dm.truthy(checked_turf.atmos_adjacent_turfs[secondary_turf]))
                    continue;

                if (Dm.truthy(adjacent_turfs[secondary_turf]))
                    matching_directions++;

                if (matching_directions >= 2)
                {
                    Dm.append(ref adjacent_turfs, checked_turf);
                    break;
                }
            }
        }

        return adjacent_turfs;
    }
}

// tg: code/modules/atmospherics/environmental/LINDA_system.dm:166  (interleaved types, rules 1.9)
public partial class atom : datum
{
    // tg: code/modules/atmospherics/environmental/LINDA_system.dm:166
    public virtual void air_update_turf(float? update = null, float? remove = null)
    {
        update ??= FALSE;
        remove ??= FALSE;
        if (!Dm.truthy(SSair.initialized)) // I'm sorry for polutting user code, I'll do 10 hail giacom's
            return;
        turf local_turf = get_turf(loc);
        if (local_turf == null)
            return;
        local_turf.air_update_turf(update, remove);
    }

    // tg: code/modules/atmospherics/environmental/LINDA_system.dm:203
    public virtual void atmos_spawn_air(object text = null) //because a lot of people loves to copy paste awful code lets just make an easy proc to spawn your plasma fires
    {
        turf_open local_turf = get_turf(this) as turf_open;
        if (local_turf == null)
            return;
        local_turf.atmos_spawn_air(text);
    }
}

// tg: code/modules/atmospherics/environmental/LINDA_system.dm:184  (interleaved types, rules 1.9)
public partial class turf : atom
{
    /**
     * A helper proc for dealing with atmos changes
     *
     * Ok so this thing is pretty much used as a catch all for all the situations someone might wanna change something
     * About a turfs atmos. It's real clunky, and someone needs to clean it up, but not today.
     * Arguments:
     * * update - Has the state of the structures in the world changed? If so, update our adjacent atmos turf list, if not, don't.
     * * remove - Are you removing an active turf (Read wall), or adding one
    */
    // tg: code/modules/atmospherics/environmental/LINDA_system.dm:184
    public override void air_update_turf(float? update = null, float? remove = null)
    {
        update ??= FALSE;
        remove ??= FALSE;
        if (!Dm.truthy(SSair.initialized)) // I'm sorry for polutting user code, I'll do 10 hail giacom's
            return;
        if (Dm.truthy(update))
            immediate_calculate_adjacent_turfs();
        if (Dm.truthy(remove))
            SSair.remove_from_active(this);
        else
            SSair.add_to_active(this);
    }
}

// tg: code/modules/atmospherics/environmental/LINDA_system.dm:198  (interleaved types, rules 1.9)
public partial class atom_movable : atom
{
    // tg: code/modules/atmospherics/environmental/LINDA_system.dm:198
    public virtual void move_update_air(turf target_turf = null)
    {
        if (isturf(target_turf))
            target_turf.air_update_turf(TRUE, FALSE); //You're empty now
        air_update_turf(TRUE, TRUE); //You aren't
    }
}

// tg: code/modules/atmospherics/environmental/LINDA_system.dm:209  (interleaved types, rules 1.9)
public partial class turf_open : turf
{
    // tg: code/modules/atmospherics/environmental/LINDA_system.dm:209
    public override void atmos_spawn_air(object text = null)
    {
        if (!Dm.truthy(text) || !Dm.truthy(air))
            return;

        datum_gas_mixture turf_mixture = SSair.parse_gas_string(text, typeof(datum_gas_mixture_turf)) as datum_gas_mixture;

        air.merge(turf_mixture);
        archive();
        SSair.add_to_active(this);
    }
}
