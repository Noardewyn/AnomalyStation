// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/explosions.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5x slice 1 (explosions, D149): the whole file but the admin verb check_bomb_impacts (:89, admin tooling, M10) and its timer
// target wipe_color_and_text (:149), and the MC stat panel line stat_entry (:47, stat panels, rules 13). The admin logs, the
// blackbox record and the debug line of propagate_blastwave are deferred leaves (admin tooling, M10). The var block is
// generated (D111). Determinism: every list is processed in insertion order (the spiral of prepare_explosion_turfs, each turf's
// contents), and all randomness is tg's prob/rand on the world's RNG; REALTIMEOFDAY and the cost_* averages only feed logs and
// the stat panel (the virtual tick usage, DV3).

// tg: code/controllers/subsystem/explosions.dm:8 SUBSYSTEM_DEF(explosions)
public partial class datum_controller_subsystem_explosions : datum_controller_subsystem
{
    // tg: code/controllers/subsystem/explosions.dm:1  #define EXPLOSION_THROW_SPEED 4 (file-local, #undef at :784)
    private const float EXPLOSION_THROW_SPEED = 4;
    // tg: code/controllers/subsystem/explosions.dm:2  #define EXPLOSION_BLOCK_LIGHT 2.5 (file-local, #undef at :785)
    private const float EXPLOSION_BLOCK_LIGHT = 2.5f;
    // tg: code/controllers/subsystem/explosions.dm:3  #define EXPLOSION_BLOCK_HEAVY 1.5 (file-local, #undef at :786)
    private const float EXPLOSION_BLOCK_HEAVY = 1.5f;
    // tg: code/controllers/subsystem/explosions.dm:4  #define EXPLOSION_BLOCK_DEV 1 (file-local, #undef at :787)
    private const float EXPLOSION_BLOCK_DEV = 1;

    // Explosion SFX defines...
    /// The probability that a quaking explosion will make the station creak per unit. Maths!
    // tg: code/controllers/subsystem/explosions.dm:482  #define QUAKE_CREAK_PROB 30 (file-local, #undef at :573)
    private const float QUAKE_CREAK_PROB = 30;
    /// The probability that an echoing explosion will make the station creak per unit.
    // tg: code/controllers/subsystem/explosions.dm:484  #define ECHO_CREAK_PROB 5
    private const float ECHO_CREAK_PROB = 5;
    /// Time taken for the hull to begin to creak after an explosion, if applicable.
    // tg: code/controllers/subsystem/explosions.dm:486  #define CREAK_DELAY (5 SECONDS)
    private const float CREAK_DELAY = (5 * SECONDS);
    /// Lower limit for far explosion SFX volume.
    // tg: code/controllers/subsystem/explosions.dm:488  #define FAR_LOWER 40
    private const float FAR_LOWER = 40;
    /// Upper limit for far explosion SFX volume.
    // tg: code/controllers/subsystem/explosions.dm:490  #define FAR_UPPER 60
    private const float FAR_UPPER = 60;
    /// The probability that a distant explosion SFX will be a far explosion sound rather than an echo. (0-100)
    // tg: code/controllers/subsystem/explosions.dm:492  #define FAR_SOUND_PROB 75
    private const float FAR_SOUND_PROB = 75;
    /// The upper limit on screenshake amplitude for nearby explosions.
    // tg: code/controllers/subsystem/explosions.dm:494  #define NEAR_SHAKE_CAP 5
    private const float NEAR_SHAKE_CAP = 5;
    /// The upper limit on screenshake amplifude for distant explosions.
    // tg: code/controllers/subsystem/explosions.dm:496  #define FAR_SHAKE_CAP 1.5
    private const float FAR_SHAKE_CAP = 1.5f;
    /// The duration of the screenshake for nearby explosions.
    // tg: code/controllers/subsystem/explosions.dm:498  #define NEAR_SHAKE_DURATION (1.5 SECONDS)
    private const float NEAR_SHAKE_DURATION = (1.5f * SECONDS);
    /// The duration of the screenshake for distant explosions.
    // tg: code/controllers/subsystem/explosions.dm:500  #define FAR_SHAKE_DURATION (1 SECONDS)
    private const float FAR_SHAKE_DURATION = (1 * SECONDS);
    /// The lower limit for the randomly selected hull creaking frequency.
    // tg: code/controllers/subsystem/explosions.dm:502  #define FREQ_LOWER 25
    private const float FREQ_LOWER = 25;
    /// The upper limit for the randomly selected hull creaking frequency.
    // tg: code/controllers/subsystem/explosions.dm:504  #define FREQ_UPPER 40
    private const float FREQ_UPPER = 40;

    // The New() that SUBSYSTEM_DEF(explosions) defines (__DEFINES/MC.dm:93-96), with NEW_SS_GLOBAL(SSexplosions)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/explosions.dm:8
    public override async DmTask New(params object[] __extra)
    {
        if (SSexplosions != this)
        {
            if (SSexplosions is datum_controller_subsystem_explosions)
            {
                Recover();
                qdel(SSexplosions);
            }
            SSexplosions = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/explosions.dm:47
    public override object stat_entry(object msg = null)
    {
        Dm.Deferred("code/controllers/subsystem/explosions.dm:47", "stat_entry: the MC stat panel line (stat panels, translation-rules.md 13)");
        return null;
    }

    // tg: code/controllers/subsystem/explosions.dm:77
    public virtual object is_exploding()
    {
        return (Dm.truthy(lowturf.len) || Dm.truthy(medturf.len) || Dm.truthy(highturf.len) || Dm.truthy(flameturf.len) || Dm.truthy(throwturf.len) || Dm.truthy(held_throwturf.len) || Dm.truthy(low_mov_atom.len) || Dm.truthy(med_mov_atom.len) || Dm.truthy(high_mov_atom.len)) ? TRUE : FALSE;
    }

    // tg: code/controllers/subsystem/explosions.dm:80
    public virtual void wipe_turf(turf T = null)
    {
        Dm.remove(ref lowturf, T);
        Dm.remove(ref medturf, T);
        Dm.remove(ref highturf, T);
        Dm.remove(ref flameturf, T);
        Dm.remove(ref throwturf, T);
        Dm.remove(ref held_throwturf, T);
    }

    // tg: code/controllers/subsystem/explosions.dm:89  ADMIN_VERB(check_bomb_impacts, ...) and :149 /proc/wipe_color_and_text:
    // not translated, the admin verbs (admin tooling, M10).

    /**
     * Makes a given atom explode. Now on the explosions subsystem!
     *
     * Arguments:
     * - [origin][/atom]: The atom that's exploding.
     * - devastation_range: The range at which the effects of the explosion are at their strongest.
     * - heavy_impact_range: The range at which the effects of the explosion are relatively severe.
     * - light_impact_range: The range at which the effects of the explosion are relatively weak.
     * - flash_range: The range at which the explosion flashes people.
     * - adminlog: Whether to log the explosion/report it to the administration.
     * - ignorecap: Whether to ignore the relevant bombcap. Defaults to FALSE.
     * - flame_range: The range at which the explosion should produce hotspots.
     * - silent: Whether to generate/execute sound effects.
     * - smoke: Whether to generate a smoke cloud provided the explosion is powerful enough to warrant it.
     * - protect_epicenter: Whether to leave the epicenter turf unaffected by the explosion
     * - explosion_cause: [Optional] The atom that caused the explosion, when different to the origin. Used for logging.
     * - explosion_direction: The angle in which the explosion is pointed (for directional explosions.)
     * - explosion_arc: The angle of the arc covered by a directional explosion (if 360 the explosion is non-directional.)
     */
    // tg: code/controllers/subsystem/explosions.dm:236
    public virtual async DmTask explode(atom origin = null, float? devastation_range = null, float? heavy_impact_range = null, float? light_impact_range = null, object flame_range = null, object flash_range = null, float? adminlog = null, float? ignorecap = null, float? silent = null, float? smoke = null, float? protect_epicenter = null, object explosion_cause = null, float? explosion_direction = null, float? explosion_arc = null)
    {
        devastation_range ??= 0;
        heavy_impact_range ??= 0;
        light_impact_range ??= 0;
        adminlog ??= TRUE;
        ignorecap ??= FALSE;
        silent ??= FALSE;
        smoke ??= FALSE;
        protect_epicenter ??= FALSE;
        explosion_direction ??= 0;
        explosion_arc ??= 360;
        DmList arguments = Dm.list(
            Dm.kv(EXARG_KEY_ORIGIN, origin),
            Dm.kv(EXARG_KEY_DEV_RANGE, devastation_range),
            Dm.kv(EXARG_KEY_HEAVY_RANGE, heavy_impact_range),
            Dm.kv(EXARG_KEY_LIGHT_RANGE, light_impact_range),
            Dm.kv(EXARG_KEY_FLAME_RANGE, flame_range),
            Dm.kv(EXARG_KEY_FLASH_RANGE, flash_range),
            Dm.kv(EXARG_KEY_ADMIN_LOG, adminlog),
            Dm.kv(EXARG_KEY_IGNORE_CAP, ignorecap),
            Dm.kv(EXARG_KEY_SILENT, silent),
            Dm.kv(EXARG_KEY_SMOKE, smoke),
            Dm.kv(EXARG_KEY_PROTECT_EPICENTER, protect_epicenter),
            Dm.kv(EXARG_KEY_EXPLOSION_CAUSE, Dm.truthy(explosion_cause) ? explosion_cause : origin),
            Dm.kv(EXARG_KEY_EXPLOSION_DIRECTION, explosion_direction),
            Dm.kv(EXARG_KEY_EXPLOSION_ARC, explosion_arc)
        );
        atom location = isturf(origin) ? origin : origin.loc;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(origin._listen_lookup?[COMSIG_ATOM_EXPLODE]) ? NONE : origin._SendSignal(COMSIG_ATOM_EXPLODE, Dm.list(origin, arguments))), COMSIG_CANCEL_EXPLOSION)))
            return; // Signals are incompatible with `arglist(...)` so we can't actually use that for these. Additionally,

        while (Dm.truthy(location))
        {
            atom next_loc = location.loc;
            if (Dm.truthy(Dm.bitand((!Dm.truthy(location._listen_lookup?[COMSIG_ATOM_INTERNAL_EXPLOSION]) ? NONE : location._SendSignal(COMSIG_ATOM_INTERNAL_EXPLOSION, Dm.list(location, arguments))), COMSIG_CANCEL_EXPLOSION)))
                return;
            if (isturf(location))
                break;
            location = next_loc;
        }

        if (!Dm.truthy(location))
            return;

        area epicenter_area = get_area(location) as area;
        if (Dm.truthy(Dm.bitand((!Dm.truthy(epicenter_area._listen_lookup?[COMSIG_AREA_INTERNAL_EXPLOSION]) ? NONE : epicenter_area._SendSignal(COMSIG_AREA_INTERNAL_EXPLOSION, Dm.list(epicenter_area, arguments))), COMSIG_CANCEL_EXPLOSION)))
            return;

        Dm.remove(ref arguments, EXARG_KEY_ORIGIN);

        // propagate_blastwave(arglist(list(location) + arguments)): the location, then the list's keys as named arguments (the
        // keys are propagate_blastwave's parameter names, explosions.dm:16-48 of __DEFINES), with the values the signals left.
        await propagate_blastwave(location,
            devastation_range: arguments[EXARG_KEY_DEV_RANGE],
            heavy_impact_range: arguments[EXARG_KEY_HEAVY_RANGE],
            light_impact_range: arguments[EXARG_KEY_LIGHT_RANGE],
            flame_range: arguments[EXARG_KEY_FLAME_RANGE],
            flash_range: arguments[EXARG_KEY_FLASH_RANGE],
            adminlog: arguments[EXARG_KEY_ADMIN_LOG],
            ignorecap: arguments[EXARG_KEY_IGNORE_CAP],
            silent: arguments[EXARG_KEY_SILENT],
            smoke: arguments[EXARG_KEY_SMOKE],
            protect_epicenter: arguments[EXARG_KEY_PROTECT_EPICENTER],
            explosion_cause: arguments[EXARG_KEY_EXPLOSION_CAUSE],
            explosion_direction: arguments[EXARG_KEY_EXPLOSION_DIRECTION],
            explosion_arc: arguments[EXARG_KEY_EXPLOSION_ARC]);
    }

    /**
     * Handles the effects of an explosion originating from a given point.
     *
     * Primarily handles popagating the blastwave of the explosion to the relevant turfs.
     * Also handles the fireball from the explosion.
     * Also handles the smoke cloud from the explosion.
     * Also handles sfx and screenshake.
     */
    // tg: code/controllers/subsystem/explosions.dm:300
    public virtual async DmTask propagate_blastwave(atom epicenter = null, object devastation_range = null, object heavy_impact_range = null, object light_impact_range = null, object flame_range = null, object flash_range = null, object adminlog = null, object ignorecap = null, object silent = null, object smoke = null, object protect_epicenter = null, object explosion_cause = null, object explosion_direction = null, object explosion_arc = null)
    {
        epicenter = get_turf(epicenter);
        if (!Dm.truthy(epicenter))
            return;
        bool top = TopBlast.Take(epicenter as turf); // DEVIATION(DV578): it goes off under the cell's top

        if (flame_range == null) // isnull(flame_range)
            flame_range = light_impact_range;
        if (flash_range == null) // isnull(flash_range)
            flash_range = devastation_range;

        // Archive the uncapped explosion for the doppler array
        object orig_dev_range = devastation_range;
        object orig_heavy_range = heavy_impact_range;
        object orig_light_range = light_impact_range;

        object orig_max_distance = max(devastation_range, heavy_impact_range, light_impact_range, flame_range, flash_range);

        //Zlevel specific bomb cap multiplier
        object cap_multiplier = SSmapping.level_trait(epicenter.z, ZTRAIT_BOMBCAP_MULTIPLIER);
        if (cap_multiplier == null) // isnull(cap_multiplier)
            cap_multiplier = 1;

        if (!Dm.truthy(ignorecap))
        {
            devastation_range = min(GLOB.MAX_EX_DEVESTATION_RANGE * Dm.num(cap_multiplier), devastation_range);
            heavy_impact_range = min(GLOB.MAX_EX_HEAVY_RANGE * Dm.num(cap_multiplier), heavy_impact_range);
            light_impact_range = min(GLOB.MAX_EX_LIGHT_RANGE * Dm.num(cap_multiplier), light_impact_range);
            flash_range = min(GLOB.MAX_EX_FLASH_RANGE * Dm.num(cap_multiplier), flash_range);
            flame_range = min(GLOB.MAX_EX_FLAME_RANGE * Dm.num(cap_multiplier), flame_range);
        }

        float max_range = Dm.num(max(devastation_range, heavy_impact_range, light_impact_range, flame_range));
        float started_at = REALTIMEOFDAY();

        // Now begins a bit of a logic train to find out whodunnit.
        object who_did_it = "N/A";
        object who_did_it_game_log = "N/A";

        // Projectiles have special handling. They rely on a firer var and not fingerprints. Check special cases for firer being
        // mecha, mob or an object such as the gun itself. Handle each uniquely.
        if (explosion_cause is obj_projectile fired_projectile)
        {
            if (fired_projectile.firer is obj_vehicle_sealed_mecha firing_mecha)
            {
                DmList drivers = firing_mecha.return_occupants();
                if (Dm.truthy(length(drivers)))
                {
                    who_did_it = "\\[Mecha drivers:";
                    who_did_it_game_log = "\\[Mecha drivers:";
                    foreach (mob driver in Dm.each<mob>(drivers))
                    {
                        who_did_it = Dm.text("[] []", who_did_it, ADMIN_LOOKUPFLW(driver));
                        who_did_it_game_log = Dm.text(" []", key_name(driver)); // tg: = rather than +=, kept
                    }
                    who_did_it = Dm.text("[]\\]", who_did_it);
                    who_did_it_game_log = Dm.text("[]\\]", who_did_it_game_log);
                }
            }
            else if (ismob(fired_projectile.firer))
            {
                who_did_it = Dm.text("\\[Projectile firer: []\\]", ADMIN_LOOKUPFLW(fired_projectile.firer));
                who_did_it_game_log = Dm.text("\\[Projectile firer: []\\]", key_name(fired_projectile.firer));
            }
            else
            {
                who_did_it = Dm.text("\\[Projectile firer: []\\]", ADMIN_LOOKUPFLW((fired_projectile.firer as atom)?.fingerprintslast));
                who_did_it_game_log = Dm.text("\\[Projectile firer: []\\]", key_name((fired_projectile.firer as atom)?.fingerprintslast));
            }
        }
        // Otherwise if the explosion cause is an atom, try get the fingerprints.
        else if (explosion_cause is atom cause_atom) // istype(explosion_cause): tg's text causes (v8_engine.dm:188) are not atoms
        {
            who_did_it = ADMIN_LOOKUPFLW(cause_atom.fingerprintslast);
            who_did_it_game_log = key_name(cause_atom.fingerprintslast);
        }

        if (Dm.truthy(adminlog))
        {
            message_admins(Dm.text("Explosion with size (Devast: [], Heavy: [], Light: [], Flame: [], Flash: []) in []. Possible cause: []. Last fingerprints: [].", devastation_range, heavy_impact_range, light_impact_range, flame_range, flash_range, ADMIN_VERBOSEJMP(epicenter), explosion_cause, who_did_it));
            log_game(Dm.text("Explosion with size ([], [], [], [], []) in [].  Possible cause: []. Last fingerprints: [].", devastation_range, heavy_impact_range, light_impact_range, flame_range, flash_range, loc_name(epicenter), explosion_cause, who_did_it_game_log));
        }

        float x0 = epicenter.x;
        float y0 = epicenter.y;
        float z0 = epicenter.z;
        area areatype = get_area(epicenter) as area;
        // tg: :375  SSblackbox.record_feedback("associative", "explosion", 1, list(...)): the round's statistics (M10)
        Dm.Deferred("code/controllers/subsystem/explosions.dm:375", "propagate_blastwave: SSblackbox.record_feedback of the explosion (round statistics, M10+)");

        // Play sounds; we want sounds to be different depending on distance so we will manually do it ourselves.
        // Stereo users will also hear the direction of the explosion!

        // Calculate far explosion sound range. Only allow the sound effect for heavy/devastating explosions.
        // 3/7/14 will calculate to 80 + 35

        float far_dist = 0;
        far_dist += Dm.num(heavy_impact_range) * 15;
        far_dist += Dm.num(devastation_range) * 20;

        if (!Dm.truthy(silent))
            shake_the_room(epicenter as turf, orig_max_distance, far_dist, devastation_range, heavy_impact_range);

        // DEVIATION(DV245): tg's own test is heavy_impact_range > 1, which leaves C-4 (0, 0, 3) with no blast to see
        if (Dm.num(devastation_range) > 0 || Dm.num(heavy_impact_range) > 0 || Dm.num(light_impact_range) > 0)
        {
            Type effect_type = typeof(datum_effect_system_explosion);
            if (Dm.truthy(smoke))
                effect_type = typeof(datum_effect_system_explosion_smoke);
            // DEVIATION(DV245): a directional blast (a launcher's backblast) is drawn a tile out on its way, behind the shooter, not on them
            atom blast_at = epicenter;
            if (Dm.num(explosion_arc) < 360 && epicenter is turf from && get_turf_in_angle(Dm.num(explosion_direction), from, 1) is turf behind && behind is not turf_closed)
                blast_at = behind;
            datum_effect_system_explosion system = Dm.@new(effect_type, blast_at) as datum_effect_system_explosion;
            await system.start();
            if (top)
            {
                foreach (obj_effect_explosion drawn in Dm.each<obj_effect_explosion>(blast_at))
                    TopBlast.Mark(drawn);
            }
            scatter_blasts(epicenter as turf, max(Dm.num(devastation_range), Dm.num(heavy_impact_range), Dm.num(light_impact_range)), Dm.num(explosion_direction), Dm.num(explosion_arc), top);
        }

        //flash mobs
        if (Dm.truthy(flash_range))
        {
            foreach (mob_living L in Dm.each<mob_living>(viewers(flash_range, epicenter)))
                await L.flash_act();
        }

        DmList affected_turfs = prepare_explosion_turfs(max_range, epicenter as turf, protect_epicenter, explosion_direction, explosion_arc) as DmList;

        object reactionary = config.Get(typeof(datum_config_entry_flag_reactionary_explosions));
        // this list is setup in the form position -> block for that position
        // we assert that turfs will be processed closed to farthest, so we can build this as we go along
        // This is gonna be an array, index'd by turfs
        DmList cached_exp_block = Dm.list();
        DmList held_throwturf = this.held_throwturf;

        //lists are guaranteed to contain at least 1 turf at this point
        //we presuppose that we'll be iterating away from the epicenter
        foreach (turf explode in Dm.each_cast<turf>(affected_turfs))
        {
            float our_x = explode.x;
            float our_y = explode.y;
            float dist = CHEAP_HYPOTENUSE(our_x, our_y, x0, y0);
            // DEVIATION(DV395): a turf on another level of the stack is as far as its level is besides
            if (explode.z != z0)
                dist = MathF.Sqrt(dist * dist + (explode.z - z0) * (explode.z - z0));
            float block = 0;
            // Using this pattern, block will flow out from blocking turfs, essentially caching the recursion
            // This is safe because if get_step_towards is ever anything but caridnally off, it'll do a diagonal move
            // So we always sample from a "loop" closer
            // It's kind of behaviorly unimpressive but that's a problem for the future
            if (Dm.truthy(reactionary))
            {
                if (explode == epicenter)
                    cached_exp_block[explode] = explode.explosive_resistance;
                else
                {
                    // DEVIATION(DV395): on another level the step goes toward the epicenter on all three axes (a shell closer), so the floors between the levels shield
                    object toward = explode.z == z0 ? get_step_towards(explode, epicenter)
                        : locate(explode.x - Math.Sign(explode.x - x0), explode.y - Math.Sign(explode.y - y0), explode.z - Math.Sign(explode.z - z0));
                    object our_block = cached_exp_block[toward];
                    block += Dm.num(our_block);
                    cached_exp_block[explode] = Dm.add(our_block, explode.explosive_resistance);
                }
            }

            float severity = EXPLODE_NONE;
            if (dist + (block * EXPLOSION_BLOCK_DEV) < Dm.num(devastation_range))
                severity = EXPLODE_DEVASTATE;
            else if (dist + (block * EXPLOSION_BLOCK_HEAVY) < Dm.num(heavy_impact_range))
                severity = EXPLODE_HEAVY;
            else if (dist + (block * EXPLOSION_BLOCK_LIGHT) < Dm.num(light_impact_range))
                severity = EXPLODE_LIGHT;

            if (explode == epicenter) // Ensures explosives detonating from bags trigger other explosives in that bag
            {
                DmList items = Dm.list();
                foreach (atom holder in Dm.each_cast<atom>(explode))
                {
                    if (Dm.truthy(length(holder.contents)) && !Dm.truthy(Dm.bitand(holder.flags_1, PREVENT_CONTENTS_EXPLOSION_1))) //The atom/contents_explosion() proc returns null if the contents ex_acting has been handled by the atom, and TRUE if it hasn't.
                        Dm.append(ref items, holder.get_all_contents(ignore_flag_1: PREVENT_CONTENTS_EXPLOSION_1));
                    if (isliving(holder))
                        Dm.remove(ref items, holder); //Stops mobs from taking double damage from explosions originating from them/their turf, such as from projectiles
                }
                switch (severity)
                {
                    case EXPLODE_DEVASTATE:
                        Dm.append(ref SSexplosions.high_mov_atom, items);
                        break;
                    case EXPLODE_HEAVY:
                        Dm.append(ref SSexplosions.med_mov_atom, items);
                        break;
                    case EXPLODE_LIGHT:
                        Dm.append(ref SSexplosions.low_mov_atom, items);
                        break;
                }
            }
            switch (severity)
            {
                case EXPLODE_DEVASTATE:
                    Dm.append(ref SSexplosions.highturf, explode);
                    break;
                case EXPLODE_HEAVY:
                    Dm.append(ref SSexplosions.medturf, explode);
                    break;
                case EXPLODE_LIGHT:
                    Dm.append(ref SSexplosions.lowturf, explode);
                    break;
            }

            if (prob(40) && dist < Dm.num(flame_range) && !isspaceturf(explode) && !Dm.truthy(explode.density))
                Dm.append(ref flameturf, explode);

            //--- THROW ITEMS AROUND ---
            if (Dm.truthy(explode.explosion_throw_details))
            {
                DmList throwingturf = explode.explosion_throw_details;
                if (Dm.num(throwingturf[1]) < max_range - dist)
                {
                    throwingturf[1] = max_range - dist;
                    throwingturf[2] = epicenter;
                    throwingturf[3] = max_range;
                    throwingturf[4] = severity;
                }
            }
            else
            {
                explode.explosion_throw_details = Dm.list(max_range - dist, epicenter, max_range, severity);
                Dm.append(held_throwturf, explode);
            }
        }

        // DEVIATION(DV578): the floor over a top blast's epicentre (the level over the cell) takes the epicentre's severity, as the cell's own
        // ceiling does through its turf's ex_act
        if (top && TopBlast.FloorOver(epicenter as turf) is turf over)
        {
            float at_top = Dm.num(devastation_range) > 0 ? EXPLODE_DEVASTATE : Dm.num(heavy_impact_range) > 0 ? EXPLODE_HEAVY : Dm.num(light_impact_range) > 0 ? EXPLODE_LIGHT : EXPLODE_NONE;
            if (at_top != EXPLODE_NONE)
            {
                Dm.remove(ref SSexplosions.highturf, over);
                Dm.remove(ref SSexplosions.medturf, over);
                Dm.remove(ref SSexplosions.lowturf, over);
                switch (at_top)
                {
                    case EXPLODE_DEVASTATE: Dm.append(ref SSexplosions.highturf, over); break;
                    case EXPLODE_HEAVY: Dm.append(ref SSexplosions.medturf, over); break;
                    default: Dm.append(ref SSexplosions.lowturf, over); break;
                }
            }
        }

        float took = (REALTIMEOFDAY() - started_at) / 10;

        //You need to press the DebugGame verb to see these now....they were getting annoying and we've collected a fair bit of data. Just -test- changes to explosion code using this please so we can compare
        // debug_world(...) (__DEFINES/chat.dm:34), expanded inline (it reads GLOB): GLOB.debugging_enabled is set only by the
        // admins' DebugGame verb (admin tooling, M10), so the world chat line is never reached.
        if (Dm.truthy(GLOB.debugging_enabled))
            Dm.Deferred("code/controllers/subsystem/explosions.dm:470", "propagate_blastwave: debug_world, the explosion's timing line in the world chat (the admins' DebugGame verb, M10+)");

        explosion_index += 1;

        // SEND_GLOBAL_SIGNAL(COMSIG_GLOB_EXPLOSION, ...) (__DEFINES/dcs/helpers.dm:7), expanded inline (rules 11.1 (e))
        if (Dm.truthy(SSdcs._listen_lookup?[COMSIG_GLOB_EXPLOSION]))
            SSdcs._SendSignal(COMSIG_GLOB_EXPLOSION, Dm.list(SSdcs, epicenter, devastation_range, heavy_impact_range, light_impact_range, took, orig_dev_range, orig_heavy_range, orig_light_range, explosion_cause, explosion_index));
    }

    // DEVIATION(DV245): a big explosion shows blasts all over its radius, smaller the farther out, instead of tg's one on the epicentre (the owner, 2026-11-01);
    // a directional one (a backblast) only within its arc, as thick, and never on its epicentre (the shooter)
    private void scatter_blasts(turf epicenter, float radius, float direction = 0, float arc = 360, bool top = false)
    {
        if (epicenter == null || radius < 4)
            return;
        bool directional = arc < 360;
        int blasts = (int)min(radius * radius / 2, 40);
        if (directional)
            blasts = (int)(blasts * 360 / max(arc, 1));
        for (int i = 0; i < blasts; i++)
        {
            turf at = locate(Dm.num(epicenter.x) + rand(-radius, radius), Dm.num(epicenter.y) + rand(-radius, radius), epicenter.z) as turf;
            float distance = at == null ? radius + 1 : get_dist(epicenter, at);
            if (distance > radius || at is turf_closed)
                continue;
            if (directional && (at == epicenter || Math.Abs(Dm.num(closer_angle_difference(direction, get_angle(epicenter, at)))) > arc / 2))
                continue;
            var blast = Dm.@new<obj_effect_explosion>(at);
            blast.transform = new matrix().Scale(1 - 0.6f * distance / radius);
            if (top)
                TopBlast.Mark(blast); // DV578: drawn under the ceiling with the blast
        }
    }

    private const float SHAKE_SCALE = 0.3f; // DEVIATION(DV245): a blast shakes a first-person view 0.3 as hard as tg's top-down one (the owner, 2026-11-01: half; 2026-11-18: 40 % less)

    /**
     * Handles the sfx and screenshake caused by an explosion.
     *
     * Arguments:
     * - [epicenter][/turf]: The location of the explosion.
     * - near_distance: How close to the explosion you need to be to get the full effect of the explosion.
     * - far_distance: How close to the explosion you need to be to hear more than echos.
     * - quake_factor: Main scaling factor for screenshake.
     * - echo_factor: Whether to make the explosion echo off of very distant parts of the station.
     * - creaking: Whether to make the station creak. Autoset if null.
     * - [near_sound][/sound]: The sound that plays if you are close to the explosion.
     * - [far_sound][/sound]: The sound that plays if you are far from the explosion.
     * - [echo_sound][/sound]: The sound that plays as echos for the explosion.
     * - [creaking_sound][/sound]: The sound that plays when the station creaks during the explosion.
     * - [hull_creaking_sound][/sound]: The sound that plays when the station creaks after the explosion.
     */
    // tg: code/controllers/subsystem/explosions.dm:522
    public virtual void shake_the_room(turf epicenter = null, object near_distance = null, object far_distance = null, object quake_factor = null, object echo_factor = null, object creaking = null, sound near_sound = null, sound far_sound = null, sound echo_sound = null, sound creaking_sound = null, object hull_creaking_sound = null)
    {
        near_sound ??= Dm.sound(get_sfx(SFX_EXPLOSION));
        far_sound ??= Dm.sound(Dm.resource("sound/effects/explosion/explosionfar.ogg"));
        echo_sound ??= Dm.sound(Dm.resource("sound/effects/explosion/explosion_distant.ogg"));
        creaking_sound ??= Dm.sound(get_sfx(SFX_EXPLOSION_CREAKING));
        hull_creaking_sound ??= Dm.sound(get_sfx(SFX_HULL_CREAKING));
        object frequency = get_rand_frequency();
        float blast_z = epicenter.z;
        area epicenter_area = get_area(epicenter) as area;
        if (creaking == null) // Autoset creaking.
        {
            object on_station = SSmapping.level_trait(epicenter.z, ZTRAIT_STATION);
            if (Dm.truthy(on_station) && prob((Dm.num(quake_factor) * QUAKE_CREAK_PROB) + (Dm.num(echo_factor) * ECHO_CREAK_PROB))) // Huge explosions are near guaranteed to make the station creak and whine, smaller ones might.
                creaking = TRUE; // prob over 100 always returns true
            else
                creaking = FALSE;
        }

        foreach (mob listener in Dm.each_cast<mob>(GLOB.player_list))
        {
            turf listener_turf = get_turf(listener);
            if (!Dm.truthy(listener_turf) || listener_turf.z != blast_z)
                continue;

            float distance = get_dist(epicenter, listener_turf);
            if (epicenter == listener_turf)
                distance = 0;
            float base_shake_amount = sqrt(Dm.num(near_distance) / (distance + 1));

            // SHIM-GAP: world.view ("15x15" in tg's world.dm) reads in BYOND as the number 7 (dm_view_read)
            if (distance <= round(Dm.num(near_distance) + Dm.num(dm_view_read(world.view)) - 2, 1)) // If you are close enough to see the effects of the explosion first-hand (ignoring walls)
            {
                listener.playsound_local(epicenter, null, 100, TRUE, frequency, sound_to_use: near_sound);
                if (base_shake_amount > 0)
                    shake_camera(listener, NEAR_SHAKE_DURATION, clamp(base_shake_amount, 0, NEAR_SHAKE_CAP) * SHAKE_SCALE);
            }

            else if (distance < Dm.num(far_distance)) // You can hear a far explosion if you are outside the blast radius. Small explosions shouldn't be heard throughout the station.
            {
                float far_volume = clamp(Dm.num(far_distance) / 2, FAR_LOWER, FAR_UPPER);
                if (Dm.truthy(creaking))
                    listener.playsound_local(epicenter, null, far_volume, TRUE, frequency, sound_to_use: creaking_sound, distance_multiplier: 0);
                else if (prob(FAR_SOUND_PROB)) // Sound variety during meteor storm/tesloose/other bad event
                    listener.playsound_local(epicenter, null, far_volume, TRUE, frequency, sound_to_use: far_sound, distance_multiplier: 0);
                else
                    listener.playsound_local(epicenter, null, far_volume, TRUE, frequency, sound_to_use: echo_sound, distance_multiplier: 0);

                if (Dm.truthy(base_shake_amount) || Dm.truthy(quake_factor))
                {
                    base_shake_amount = max(base_shake_amount, Dm.num(quake_factor) * 3, 0); // Devastating explosions rock the station and ground
                    shake_camera(listener, FAR_SHAKE_DURATION, min(base_shake_amount, FAR_SHAKE_CAP) * SHAKE_SCALE);
                }
            }

            else if (!isspaceturf(listener_turf) && !(!Dm.@in(epicenter_area.type, GLOB.the_station_areas) && Dm.truthy(SSmapping.is_planetary())) && Dm.truthy(echo_factor)) // Big enough explosions echo through the hull. Except on planetary maps if the epicenter is not on the station's area.
            {
                float echo_volume;
                if (Dm.truthy(quake_factor))
                {
                    echo_volume = 60;
                    shake_camera(listener, FAR_SHAKE_DURATION, clamp(Dm.num(quake_factor) / 4, 0, FAR_SHAKE_CAP) * SHAKE_SCALE);
                }
                else
                    echo_volume = 40;
                listener.playsound_local(epicenter, null, echo_volume, TRUE, frequency, sound_to_use: echo_sound, distance_multiplier: 0);
            }

            if (Dm.truthy(creaking)) // 5 seconds after the bang, the station begins to creak
                _addtimer(Dm.@new<datum_callback>(listener, nameof(mob.playsound_local), epicenter, null, rand(FREQ_LOWER, FREQ_UPPER), TRUE, frequency, null, null, FALSE, hull_creaking_sound, 0), CREAK_DELAY, file: "code/controllers/subsystem/explosions.dm", line: 570);
        }
    }

    // tg: code/controllers/subsystem/explosions.dm:662
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        resumed ??= 0;
        if (!Dm.truthy(is_exploding()))
            return;
        float timer;
        datum_controller_master.current_ticklimit = TICK_LIMIT_RUNNING(); //force using the entire tick if we need it.

        if (Dm.eq(currentpart, SSEXPLOSIONS_TURFS))
        {
            currentpart = SSEXPLOSIONS_MOVABLES;

            timer = TICK_USAGE_REAL();
            DmList low_turf = lowturf;
            lowturf = Dm.list();
            foreach (object thing in Dm.each(low_turf))
            {
                turf turf_thing = thing as turf;
                await EX_ACT(turf_thing, EXPLODE_LIGHT);
            }
            cost_lowturf = MC_AVERAGE(cost_lowturf, TICK_DELTA_TO_MS(TICK_USAGE_REAL() - timer));

            timer = TICK_USAGE_REAL();
            DmList med_turf = medturf;
            medturf = Dm.list();
            foreach (object thing in Dm.each(med_turf))
            {
                turf turf_thing = thing as turf;
                await EX_ACT(turf_thing, EXPLODE_HEAVY);
            }
            cost_medturf = MC_AVERAGE(cost_medturf, TICK_DELTA_TO_MS(TICK_USAGE_REAL() - timer));

            timer = TICK_USAGE_REAL();
            DmList high_turf = highturf;
            highturf = Dm.list();
            foreach (object thing in Dm.each(high_turf))
            {
                turf turf_thing = thing as turf;
                await EX_ACT(turf_thing, EXPLODE_DEVASTATE);
            }
            cost_highturf = MC_AVERAGE(cost_highturf, TICK_DELTA_TO_MS(TICK_USAGE_REAL() - timer));

            timer = TICK_USAGE_REAL();
            DmList flame_turf = flameturf;
            flameturf = Dm.list();
            foreach (turf T in Dm.each<turf>(flame_turf))
                Dm.@new<obj_effect_hotspot>(T); //Mostly for ambience!
            cost_flameturf = MC_AVERAGE(cost_flameturf, TICK_DELTA_TO_MS(TICK_USAGE_REAL() - timer));

            if (Dm.truthy(low_turf.len) || Dm.truthy(med_turf.len) || Dm.truthy(high_turf.len))
                Master.laggy_byond_map_update_incoming();
        }

        if (Dm.eq(currentpart, SSEXPLOSIONS_MOVABLES))
        {
            currentpart = SSEXPLOSIONS_THROWS;

            timer = TICK_USAGE_REAL();
            DmList local_high_mov_atom = high_mov_atom;
            high_mov_atom = Dm.list();
            foreach (object thing in Dm.each(local_high_mov_atom))
            {
                atom_movable movable_thing = thing as atom_movable;
                if (QDELETED(movable_thing))
                    continue;
                await EX_ACT(movable_thing, EXPLODE_DEVASTATE);
            }
            cost_high_mov_atom = MC_AVERAGE(cost_high_mov_atom, TICK_DELTA_TO_MS(TICK_USAGE_REAL() - timer));

            timer = TICK_USAGE_REAL();
            DmList local_med_mov_atom = med_mov_atom;
            med_mov_atom = Dm.list();
            foreach (object thing in Dm.each(local_med_mov_atom))
            {
                atom_movable movable_thing = thing as atom_movable;
                if (QDELETED(movable_thing))
                    continue;
                await EX_ACT(movable_thing, EXPLODE_HEAVY);
            }
            cost_med_mov_atom = MC_AVERAGE(cost_med_mov_atom, TICK_DELTA_TO_MS(TICK_USAGE_REAL() - timer));

            timer = TICK_USAGE_REAL();
            DmList local_low_mov_atom = low_mov_atom;
            low_mov_atom = Dm.list();
            foreach (object thing in Dm.each(local_low_mov_atom))
            {
                atom_movable movable_thing = thing as atom_movable;
                if (QDELETED(movable_thing))
                    continue;
                await EX_ACT(movable_thing, EXPLODE_LIGHT);
            }
            cost_low_mov_atom = MC_AVERAGE(cost_low_mov_atom, TICK_DELTA_TO_MS(TICK_USAGE_REAL() - timer));

            /// Throwing only becomes acceptable after the explosions process, so we don't miss stuff that explosions GENERATE
            throwturf = held_throwturf;
            held_throwturf = Dm.list();
        }

        if (Dm.eq(currentpart, SSEXPLOSIONS_THROWS))
        {
            currentpart = SSEXPLOSIONS_TURFS;
            timer = TICK_USAGE_REAL();
            DmList throw_turf = throwturf;
            throwturf = Dm.list();
            foreach (object thing in Dm.each(throw_turf))
            {
                if (!Dm.truthy(thing))
                    continue;
                turf explode = thing as turf;
                DmList details = explode.explosion_throw_details;
                explode.explosion_throw_details = null;
                if (length(details) != 4)
                    continue;
                float throw_range = Dm.num(details[1]);
                turf center = details[2] as turf;
                float max_range = Dm.num(details[3]);
                object severity = details[4];
                float required_resist = 0;
                switch (Dm.num(severity))
                {
                    case EXPLODE_DEVASTATE: // The jump from "very strong" to "overpowering" is intentional, more stuff should be thrown by point-blank explosions
                        required_resist = MOVE_FORCE_OVERPOWERING;
                        break;
                    case EXPLODE_HEAVY:
                        required_resist = MOVE_FORCE_VERY_STRONG;
                        break;
                    case EXPLODE_LIGHT:
                        required_resist = MOVE_FORCE_STRONG;
                        break;
                }
                foreach (atom_movable movable in Dm.each_cast<atom_movable>(explode))
                {
                    if (QDELETED(movable))
                        continue;
                    if (!Dm.truthy(movable.anchored) && movable.move_resist < required_resist)
                    {
                        // We want to have our distance matter, but we do want to bias to a lot of throw, for the vibe
                        float atom_throw_range = rand(throw_range, max_range) + max_range * 0.3f;
                        turf throw_at = get_ranged_target_turf_direct(movable, center, atom_throw_range, 180) as turf; // Throw 180 degrees away from the explosion source
                        await movable.throw_at(throw_at, atom_throw_range, EXPLOSION_THROW_SPEED, quickstart: FALSE);
                    }
                }
            }
            cost_throwturf = MC_AVERAGE(cost_throwturf, TICK_DELTA_TO_MS(TICK_USAGE_REAL() - timer));
        }

        currentpart = SSEXPLOSIONS_TURFS;
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/explosions.dm:8  SUBSYSTEM_DEF(explosions): GLOBAL_REAL(SSexplosions, /datum/controller/subsystem/explosions)
    internal datum_controller_subsystem_explosions SSexplosions;
}

// GLOBAL_LIST_EMPTY(explosions) (__DEFINES/__globals.dm:51), expanded inline (rules 10.6, 11.1 (b))
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/controllers/subsystem/explosions.dm:6
    public DmList explosions;

    // tg: code/controllers/subsystem/explosions.dm:6  (GLOBAL_MANAGED(explosions, list()), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalexplosions()
    {
        explosions = Dm.list();
        Dm.append(ref gvars_datum_init_order, "explosions");
    }
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_explosions SSexplosions => ref TgState.Current.SSexplosions;

    /**
     * Using default dyn_ex scale:
     *
     * 100 explosion power is a (5, 10, 20) explosion.
     * 75 explosion power is a (4, 8, 17) explosion.
     * 50 explosion power is a (3, 7, 14) explosion.
     * 25 explosion power is a (2, 5, 10) explosion.
     * 10 explosion power is a (1, 3, 6) explosion.
     * 5 explosion power is a (0, 1, 3) explosion.
     * 1 explosion power is a (0, 0, 1) explosion.
     *
     * The easy formula for defaults is sqrt(power * 2) * (0.25 | 0.5 | 1)
     */
    // tg: code/controllers/subsystem/explosions.dm:185  (epicenter widened to /atom, Appendix A: a ruptured tank passes itself)
    public static async DmTask dyn_explosion(atom epicenter = null, object power = null, float? flame_range = null, object flash_range = null, float? adminlog = null, float? ignorecap = null, float? silent = null, float? smoke = null, atom explosion_cause = null)
    {
        flame_range ??= 0;
        adminlog ??= TRUE;
        ignorecap ??= TRUE;
        silent ??= FALSE;
        smoke ??= TRUE;
        if (!Dm.truthy(power))
            return;
        float range = 0;
        range = round(Dm.num(Dm.pow(2 * Dm.num(power), GLOB.DYN_EX_SCALE)));
        await explosion(epicenter, devastation_range: round(range * 0.25f), heavy_impact_range: round(range * 0.5f), light_impact_range: round(range), flame_range: flame_range == null ? null : flame_range * range, flash_range: flash_range == null ? null : Dm.mul(flash_range, range), adminlog: adminlog, ignorecap: ignorecap, silent: silent, smoke: smoke, explosion_cause: explosion_cause);
    }

    /**
     * Makes a given atom explode.
     *
     * Arguments: as /datum/controller/subsystem/explosions/proc/explode
     */
    // tg: code/controllers/subsystem/explosions.dm:213
    public static async DmTask<object> explosion(atom origin = null, float? devastation_range = null, float? heavy_impact_range = null, float? light_impact_range = null, object flame_range = null, object flash_range = null, float? adminlog = null, float? ignorecap = null, float? silent = null, float? smoke = null, float? protect_epicenter = null, object explosion_cause = null, float? explosion_direction = null, float? explosion_arc = null, params object[] __extra)
    {
        devastation_range ??= 0;
        heavy_impact_range ??= 0;
        light_impact_range ??= 0;
        adminlog ??= TRUE;
        ignorecap ??= FALSE;
        silent ??= FALSE;
        smoke ??= FALSE;
        protect_epicenter ??= FALSE;
        explosion_direction ??= 0;
        explosion_arc ??= 360;
        object dot = null;
        origin = TopBlast.Remap(origin); // DEVIATION(DV578): a blast at the top a shot hit goes off under it, in the cell
        // . = SSexplosions.explode(arglist(args)): explode returns nothing
        await SSexplosions.explode(origin, devastation_range, heavy_impact_range, light_impact_range, flame_range, flash_range, adminlog, ignorecap, silent, smoke, protect_epicenter, explosion_cause, explosion_direction, explosion_arc);
        return dot;
    }

    /// Returns a list of turfs in X range from the epicenter
    /// Returns in a unique order, spiraling outwards
    /// This is done to ensure our progressive cache of blast resistance is always valid
    /// This is quite fast
    // tg: code/controllers/subsystem/explosions.dm:590
    public static object prepare_explosion_turfs(object range = null, turf epicenter = null, object protect_epicenter = null, object explosion_direction = null, object explosion_arc = null)
    {
        DmList outlist = Dm.list();
        DmList candidates = Dm.list();
        // Add in the center if it's not protected
        if (!Dm.truthy(protect_epicenter))
            Dm.append(ref outlist, epicenter);

        float our_x = epicenter.x;
        float our_y = epicenter.y;
        float our_z = epicenter.z;

        float max_x = world.maxx;
        float max_y = world.maxy;

        // Work out the angles to explode between
        float first_angle_limit = WRAP(Dm.num(explosion_direction) - Dm.num(explosion_arc) * 0.5f, 0, 360);
        float second_angle_limit = WRAP(Dm.num(explosion_direction) + Dm.num(explosion_arc) * 0.5f, 0, 360);

        // Get everything in the right order
        float lower_angle_limit = 0;
        float upper_angle_limit = 0;
        float do_directional = 0;
        float reverse_angle = 0;

        // Work out which case we're in
        if (first_angle_limit == second_angle_limit) // CASE A: FULL CIRCLE
            do_directional = FALSE;
        else if (first_angle_limit < second_angle_limit) // CASE B: When the arc does not cross 0 degrees
        {
            lower_angle_limit = first_angle_limit;
            upper_angle_limit = second_angle_limit;
            do_directional = TRUE;
            reverse_angle = FALSE;
        }
        else if (first_angle_limit > second_angle_limit) // CASE C: When the arc crosses 0 degrees
        {
            lower_angle_limit = second_angle_limit;
            upper_angle_limit = first_angle_limit;
            do_directional = TRUE;
            reverse_angle = TRUE;
        }

        // DEVIATION(DV395): the blast reaches the linked levels of the stack too, each level a tile away (a 2 m level, a 2 m tile): the shell at distance i
        // is the ring i of the epicenter's level and of every linked level less than i away, and the whole square of radius i on the levels i away
        List<int> levels = LinkedLevels.Around(epicenter, (int)Dm.num(range));
        for (float i = 1, i__end = Dm.num(range); i <= i__end; i++)
        {
            float lowest_x = our_x - i;
            float lowest_y = our_y - i;
            float highest_x = our_x + i;
            float highest_y = our_y + i;
            void Ring(float z)
            {
                // top left to one before top right
                if (highest_y <= max_y)
                {
                    Dm.append(ref candidates, block(
                        lowest_x, highest_y, z,
                        highest_x - 1, highest_y, z
                    ));
                }
                // top right to one before bottom right
                if (highest_x <= max_x)
                {
                    Dm.append(ref candidates, block(
                        highest_x, highest_y, z,
                        highest_x, lowest_y + 1, z
                    ));
                }
                // bottom right to one before bottom left
                if (lowest_y >= 1)
                {
                    Dm.append(ref candidates, block(
                        highest_x, lowest_y, z,
                        lowest_x + 1, lowest_y, z
                    ));
                }
                // bottom left to one before top left
                if (lowest_x >= 1)
                {
                    Dm.append(ref candidates, block(
                        lowest_x, lowest_y, z,
                        lowest_x, highest_y - 1, z
                    ));
                }
            }
            Ring(our_z);
            foreach (int dz in levels)
            {
                if (Math.Abs(dz) < i)
                    Ring(our_z + dz);
                else if (Math.Abs(dz) == i)
                    Dm.append(ref candidates, block(Math.Max(1, lowest_x), Math.Max(1, lowest_y), our_z + dz, Math.Min(max_x, highest_x), Math.Min(max_y, highest_y), our_z + dz));
            }
        }

        if (!Dm.truthy(do_directional))
            Dm.append(ref outlist, candidates);
        else
        {
            foreach (turf candidate in Dm.each_cast<turf>(candidates))
            {
                float angle = Dm.num(get_angle(epicenter, candidate));
                // ISINRANGE(angle, lower_angle_limit, upper_angle_limit) (__DEFINES/maths.dm:89), expanded inline (rules 11.1 (e))
                if (Dm.truthy(Dm.bitxor((lower_angle_limit <= angle && angle <= upper_angle_limit) ? TRUE : FALSE, reverse_angle)))
                    Dm.append(ref outlist, candidate);
            }
        }
        return outlist;
    }
}
