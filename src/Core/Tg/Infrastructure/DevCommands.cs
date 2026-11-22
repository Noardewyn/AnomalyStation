using System.Collections.Frozen;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Owners (docs/ai/specs/m2-ownership.md): the contract is the M2 contracts step's and frozen; M2 P6 implements the bodies on
// tg's own procs (M2 P1, P2 and P3 translate them) and parses the command lines on the host. Host code, not tg code: an
// admin tool in the spirit of tg's own admin verbs (D73), which call tg's procs the same way.

/// <summary>A damage type of the dev commands (tg's <c>BRUTE</c>, <c>BURN</c>, <c>TOX</c>, <c>OXY</c>).</summary>
public enum DevDamageType : byte
{
    Brute = 0,
    Burn = 1,
    Tox = 2,
    Oxy = 3,
}

/// <summary>
/// The M2 dev commands (scope 2k, D73; from the console, and from a client's dev console when the server's
/// <c>DEV_COMMANDS</c> is on, Q9). Each runs inside the world in the command phase, with <c>usr</c> the requester's mob
/// (the console: none), and calls exactly the tg proc the table names, so the effects, the timings (crit and death at tg's
/// pace) and the messages are tg's. Each returns a short line for the requester (the host sends it as <c>ServerText</c>),
/// or null when tg's own messages say it all. A target that is not of the needed type returns an explaining line and does
/// nothing. Syntax on the host (P6): <c>damage &lt;who&gt; &lt;brute|burn|tox|oxy&gt; &lt;n&gt; [zone]</c>, <c>heal &lt;who&gt; [type]</c>,
/// <c>fullheal &lt;who&gt;</c>, <c>kill &lt;who&gt;</c>, <c>revive &lt;who&gt;</c>, <c>knockdown &lt;who&gt; &lt;s&gt;</c>, <c>stun &lt;who&gt; &lt;s&gt;</c>,
/// <c>bleed &lt;who&gt; &lt;zone&gt; &lt;stacks&gt;</c>, <c>blood &lt;who&gt; &lt;ml&gt;</c>, <c>gas &lt;preset|o2=..;n2=..&gt; [x y [z]]</c>,
/// <c>analyze &lt;who&gt;</c>, <c>spawn TYPE [x y [z [n [dir]]]]</c>, <c>delete &lt;who&gt;|x y [z]</c>, <c>air add|remove|temp|set ... x y [z [r]]</c>, <c>speedscale &lt;f&gt;</c>, <c>godmode &lt;who&gt;</c>, <c>fly &lt;who&gt;</c>, <c>noclip &lt;who&gt;</c>, <c>god &lt;who&gt;</c>,
/// <c>breach &lt;who&gt;|x y [z]</c>, <c>patch &lt;who&gt;|x y [z]</c>;
/// <c>&lt;who&gt;</c> is a player's key, a world id, or <c>me</c>.
/// </summary>
public static class DevCommands
{
    /// <summary>
    /// <c>damage</c>: with a zone, tg's <c>apply_damage(amount, type, zone)</c> on that bodypart; without one,
    /// <c>adjust_brute_loss</c>/<c>adjust_fire_loss</c>/<c>adjust_tox_loss</c>/<c>adjust_oxy_loss(amount)</c> (pinned: brute
    /// through <c>take_overall_damage</c> would spread over the parts, scope 2e).
    /// </summary>
    public static string Damage(mob_living target, DevDamageType type, float amount, string zone = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (zone != null)
        {
            // tg: code/modules/mob/living/damage_procs.dm:20 apply_damage(damage, damagetype, def_zone)
            Waiting.Detach(target.apply_damage(amount, TypeText(type), zone));
        }
        else
        {
            switch (type)
            {
                case DevDamageType.Brute: Waiting.Detach(target.adjust_brute_loss(amount)); break;
                case DevDamageType.Burn: Waiting.Detach(target.adjust_fire_loss(amount)); break;
                case DevDamageType.Tox: Waiting.Detach(target.adjust_tox_loss(amount)); break;
                case DevDamageType.Oxy: Waiting.Detach(target.adjust_oxy_loss(amount)); break;
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }
        return Inv($"{Label(target)}: {amount:0.##} {type.ToString().ToLowerInvariant()} damage{(zone != null ? " to the " + zone : "")}; {Health(target)}");
    }

    /// <summary><c>heal</c>: <c>adjust_*_loss(-get_*_loss())</c> of the type, or of all four without one.</summary>
    public static string Heal(mob_living target, DevDamageType? type = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (type is null or DevDamageType.Brute) Waiting.Detach(target.adjust_brute_loss(-Dm.num(target.get_brute_loss())));
        if (type is null or DevDamageType.Burn) Waiting.Detach(target.adjust_fire_loss(-Dm.num(target.get_fire_loss())));
        if (type is null or DevDamageType.Tox) Waiting.Detach(target.adjust_tox_loss(-Dm.num(target.get_tox_loss())));
        if (type is null or DevDamageType.Oxy) Waiting.Detach(target.adjust_oxy_loss(-Dm.num(target.get_oxy_loss())));
        return Inv($"{Label(target)}: healed {(type.HasValue ? type.Value.ToString().ToLowerInvariant() : "all damage")}; {Health(target)}");
    }

    /// <summary>
    /// <c>fullheal</c>: tg's admin heal without the revival, <c>fully_heal(ADMIN_HEAL_ALL)</c> (the owner, 2026-10-15: the
    /// console heal heals everything). tg's admin Rejuvenate is <c>revive(ADMIN_HEAL_ALL)</c> (admin/verbs/debug.dm:475), whose
    /// heal is <c>fully_heal(ADMIN_HEAL_ALL)</c>: <c>HEAL_ALL</c> leaves out <c>HEAL_ADMIN</c> (the nutrition, the suicide
    /// trait) and <c>HEAL_RESTRAINTS</c> (cuffs), mobs.dm:1035. The status effects go through tg's own
    /// <c>COMSIG_LIVING_POST_FULLY_HEAL</c> (each with <c>remove_on_fullheal</c>; their <c>on_remove</c> takes their movespeed
    /// modifiers and traits). It heals and does not revive (tg's): a dead target is told to use <c>revive</c>. Godmode is
    /// lifted around the heal (<see cref="WithoutGodmode"/>).
    /// </summary>
    public static string FullHeal(mob_living target)
    {
        ArgumentNullException.ThrowIfNull(target);
        WithoutGodmode(target, () => Waiting.Detach(target.fully_heal(ADMIN_HEAL_ALL)));
        string dead = Dm.num(target.stat) == DEAD ? " (still dead: fully_heal does not revive, use revive)" : string.Empty;
        return Inv($"{Label(target)}: fully healed; {Health(target)}{dead}");
    }

    // The dev menu's heals under the dev godmode (the owner's play test, 2026-10-13): tg's updatehealth and update_stat return
    // early under TRAIT_GODMODE, so a revive left the mob at its old health in hard crit for good. The admin trait is taken off
    // for the heal and put back, so tg's procs recompute the health and the stat as they do without it.
    private static void WithoutGodmode(mob_living target, Action heal)
    {
        bool god = HAS_TRAIT_FROM(target, TRAIT_GODMODE, ADMIN_TRAIT);
        if (god)
            REMOVE_TRAIT(target, TRAIT_GODMODE, ADMIN_TRAIT);
        try
        {
            heal();
        }
        finally
        {
            if (god)
                ADD_TRAIT(target, TRAIT_GODMODE, ADMIN_TRAIT);
        }
    }

    /// <summary><c>kill</c>: tg's <c>death()</c>.</summary>
    public static string Kill(mob_living target)
    {
        ArgumentNullException.ThrowIfNull(target);
        Waiting.Detach(target.death());
        return Inv($"{Label(target)}: death(); {Health(target)}");
    }

    /// <summary>
    /// <c>revive</c>: tg's admin rejuvenate, <c>revive(ADMIN_HEAL_ALL)</c> (admin/verbs/debug.dm:475: the heal of everything
    /// and the getting up at once), with godmode lifted around it (<see cref="WithoutGodmode"/>).
    /// </summary>
    public static string Revive(mob_living target)
    {
        ArgumentNullException.ThrowIfNull(target);
        WithoutGodmode(target, () => Waiting.Detach(target.revive(ADMIN_HEAL_ALL)));
        return Inv($"{Label(target)}: revive(); {Health(target)}");
    }

    /// <summary>
    /// <c>revive</c> of a ghost (the owner, 2026-10-26): its body is revived (and the ghost put back into it); a ghost whose body is
    /// gone (gibbed) gets a new human on its turf, named as it was, and its mind moves in (tg's admin respawn).
    /// </summary>
    public static string ReviveGhost(mob_dead_observer ghost)
    {
        ArgumentNullException.ThrowIfNull(ghost);
        if (ghost.mind?.current is mob_living body && !QDELETED(body) && body != (object)ghost)
        {
            WithoutGodmode(body, () => Waiting.Detach(body.revive(ADMIN_HEAL_ALL, force_grab_ghost: Dm.TRUE)));
            return Inv($"{Label(body)}: revive(); {Health(body)}");
        }
        turf at = get_turf(ghost);
        if (at == null) return "revive: the ghost is nowhere";
        mob_living_carbon_human human = SpawnHuman(at);
        string name = ghost.real_name as string;
        if (!string.IsNullOrEmpty(name))
            Waiting.NoSleep(human.fully_replace_character_name(human.real_name, name));
        if (ghost.mind != null)
            Waiting.NoSleep(ghost.mind.transfer_to(human, force_key_move: Dm.TRUE));
        else
            human.ckey = ghost.ckey;
        return Inv($"{Label(human)}: a new body");
    }

    /// <summary><c>knockdown</c>: tg's <c>Knockdown(seconds SECONDS)</c>.</summary>
    public static string Knockdown(mob_living target, float seconds)
    {
        ArgumentNullException.ThrowIfNull(target);
        Waiting.Detach(target.Knockdown(seconds * SECONDS));
        return Inv($"{Label(target)}: knocked down for {seconds:0.##} s");
    }

    /// <summary><c>stun</c>: tg's <c>Stun(seconds SECONDS)</c>.</summary>
    public static string Stun(mob_living target, float seconds)
    {
        ArgumentNullException.ThrowIfNull(target);
        Waiting.Detach(target.Stun(seconds * SECONDS));
        return Inv($"{Label(target)}: stunned for {seconds:0.##} s");
    }

    /// <summary><c>bleed</c>: the zone's bodypart's <c>adjustBleedStacks(stacks)</c>.</summary>
    public static string Bleed(mob_living_carbon target, string zone, float stacks)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.get_bodypart(zone) is not obj_item_bodypart part)
            return Inv($"{Label(target)} has no {zone}");
        part.adjustBleedStacks(stacks);
        return Inv($"{Label(target)}: {zone} bleed stacks adjusted by {stacks:0.##}");
    }

    /// <summary>
    /// <c>dismember</c> (M6 slice 4): tg's <c>dismember()</c> of the zone's bodypart, as a limb destroyer's strike calls it
    /// (carbon_defense.dm:205): the chest is split open and spills its organs, a limb or the head drops off and is thrown. tg's
    /// proc refuses under godmode, <c>TRAIT_NODISMEMBER</c> or an unremovable part.
    /// </summary>
    public static string Dismember(mob_living_carbon target, string zone)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.get_bodypart(zone) is not obj_item_bodypart part)
            return Inv($"{Label(target)} has no {zone}");
        object done = Waiting.Detach(part.dismember());
        return Dm.truthy(done) ? Inv($"{Label(target)}: {zone} dismembered") : Inv($"{Label(target)}: the {zone} was not dismembered (godmode, TRAIT_NODISMEMBER or an unremovable part)");
    }

    /// <summary>
    /// <c>gib</c> (M6 slice 5): tg's admin Gib verb (adminfun.dm:44): <c>gib(DROP_ALL_REMAINS)</c> when the brain drops (tg's "Drop
    /// a brain?" Yes), <c>gib(DROP_ORGANS|DROP_BODYPARTS)</c> otherwise. tg's verb also gibs an observer into generic gibs; the dev
    /// commands' WHO is a living mob.
    /// </summary>
    public static string Gib(mob_living target, bool brain)
    {
        ArgumentNullException.ThrowIfNull(target);
        string label = Label(target);
        Waiting.Detach(target.gib(brain ? DROP_ALL_REMAINS : Dm.bitor(DROP_ORGANS, DROP_BODYPARTS)));
        return Inv($"{label} gibbed");
    }

    /// <summary>
    /// <c>wound</c> (M6 slice 4): tg's <c>cause_wound_of_type_and_severity(wounding, part, severity)</c> on the zone's bodypart
    /// (the wound of that kind and severity the limb can have, or an upgrade of the one it has).
    /// </summary>
    public static string Wound(mob_living_carbon target, string zone, string wounding, int severity)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.get_bodypart(zone) is not obj_item_bodypart part)
            return Inv($"{Label(target)} has no {zone}");
        object made = Waiting.Detach(target.cause_wound_of_type_and_severity(wounding, part, severity));
        return made is datum_wound wound
            ? Inv($"{Label(target)}: {zone} has {Dm.str(wound.name)} ({wound.GetType().Name})")
            : Inv($"{Label(target)}: no {wounding} wound of severity {severity} for the {zone}");
    }

    /// <summary><c>blood</c>: tg's <c>set_blood_volume(volume)</c>.</summary>
    public static string Blood(mob_living target, float volume)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.set_blood_volume(volume);
        return Inv($"{Label(target)}: blood volume set to {volume:0.##} ml");
    }

    /// <summary>
    /// <c>gas</c>: replaces the air of one open turf, or of every open turf of the map but space without coordinates (tg's
    /// space air is immutable), with the mixture tg's <c>SSair.parse_gas_string</c> makes of <paramref name="mix"/> (a preset
    /// such as <c>OPENTURF_DEFAULT_ATMOS</c>'s name, or <c>o2=22;n2=82;TEMP=293.15</c>); the turf's gas overlays follow at once
    /// (tg's <c>update_visuals</c>).
    /// </summary>
    public static string Gas(string mix, int? x = null, int? y = null, int? z = null)
    {
        ArgumentNullException.ThrowIfNull(mix);
        var turfs = new List<turf_open>();
        if (x.HasValue && y.HasValue)
        {
            if (locate(x.Value, y.Value, z ?? 1) is not turf_open one) return Inv($"({x},{y},{z ?? 1}) is not an open turf");
            turfs.Add(one);
        }
        else
        {
            foreach (turf t in Dm.each<turf>(block(1, 1, 1, Dm.world.maxx, Dm.world.maxy, Dm.world.maxz)))
            {
                if (t is turf_open open && open is not turf_open_space && !Dm.truthy(open.blocks_air)) turfs.Add(open);
            }
        }
        int made = 0;
        foreach (turf_open t in turfs)
        {
            // tg: controllers/subsystem/air.dm:779 parse_gas_string(gas_string)
            t.air = SSair.parse_gas_string(mix) as datum_gas_mixture;
            t.update_visuals();
            // M4b slice 1: the turf's air moves now, so the changed turf wakes up (tg's add_to_active, air.dm:475)
            SSair.add_to_active(t);
            made++;
        }
        return Inv($"gas: {made} open turf(s) now hold '{mix}'");
    }

    /// <summary>
    /// <c>analyze</c>: tg's <c>healthscan(user, target, SCANNER_VERBOSE, SCANPOWER_BASIC, tochat = TRUE)</c>; its HTML
    /// reaches the requester only (<c>to_chat</c> of <paramref name="user"/>, D40). The console (no user) gets the HTML as the
    /// returned line instead.
    /// </summary>
    public static string Analyze(mob user, mob_living target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (user != null)
        {
            healthscan(user, target, SCANNER_VERBOSE, SCANPOWER_BASIC, Dm.TRUE); // to_chat(user, ...): the sink routes it to the requester's session
            return null;
        }
        // No requester: healthscan wants a user (it reads user.incapacitated), so the scan is made as the target and returned.
        object report = healthscan(target, target, SCANNER_VERBOSE, SCANPOWER_BASIC, Dm.FALSE);
        return report is string html ? PlainText(html) : Inv($"{Label(target)}: the scan returned nothing (the target cannot scan)");
    }

    /// <summary><c>spawn /mob/living/carbon/human</c>: a human with no client at <paramref name="location"/> (<c>new /mob/living/carbon/human(turf)</c>), the console's spawn path, named and dressed by <see cref="CrewStandIn.Join"/>.</summary>
    public static mob_living_carbon_human SpawnHuman(turf location)
    {
        ArgumentNullException.ThrowIfNull(location);
        var human = Dm.@new<mob_living_carbon_human>(location);
        Dm.combine(ref human.flags_1, ADMIN_SPAWNED_1);
        // M2 (F1): stands in for the job system (M9/M10): the spawned human is named and dressed as a joining crew member
        // (CrewStandIn: tg's random name and the assistant outfit's random-colour jumpsuit), so the dev command's human
        // looks like a player's.
        CrewStandIn.Join(human);
        return human;
    }

    /// <summary>
    /// The owner (2026-10-13, the dev menu's spawn panel): <c>spawn TYPE [X Y [Z [N [DIR]]]]</c> is tg's spawn panel's
    /// <c>spawn_atom</c> at a location (code/modules/admin/spawn_panel/spawn_handling.dm:16): a turf type is built on top of
    /// the turf (<c>place_on_top</c>, with its air on an open turf, DV60; once: the amount has no meaning for a turf), anything else is <c>new TYPE(target)</c>
    /// <paramref name="amount"/> times, <c>ADMIN_SPAWNED_1</c>, and <c>setDir(dir)</c> when a dir is given (0: the type's
    /// own). A human is named and dressed by <see cref="SpawnHuman"/>. Returns what was made and a line for the requester.
    /// </summary>
    public static List<atom> Spawn(Type type, turf target, int amount, byte dir, out string line)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(target);
        var made = new List<atom>();
        string path = DmTypes.PathOf(type);
        string where = Inv($"({Dm.str(target.x)},{Dm.str(target.y)},{Dm.str(target.z)})"); // before a replaced turf leaves the map
        if (typeof(turf).IsAssignableFrom(type))
        {
            // tg: original_turf.ChangeTurf(atom_to_spawn.type)
            // DEVIATION(DV60): a turf is built on top of the one there (place_on_top, tg's construction), so the delete tool's
            // ScrapeAway gives that turf back; space, a spawn of space and the same type keep tg's ChangeTurf.
            float tx = Dm.num(target.x), ty = Dm.num(target.y), tz = Dm.num(target.z);
            if (target is turf_open_space || typeof(turf_open_space).IsAssignableFrom(type) || type == typeof(turf_baseturf_bottom) || type == target.GetType())
                Waiting.Detach(target.ChangeTurf(type));
            else
                Waiting.Detach(target.place_on_top(type, isopenturf(target) ? CHANGETURF_INHERIT_AIR : null));
            if (locate(tx, ty, tz) is turf created)
                made.Add(created);
            line = Inv($"spawn: {where} is {path} now");
            return made;
        }
        for (int i = 0; i < amount; i++)
        {
            atom created;
            if (type == typeof(mob_living_carbon_human))
            {
                created = SpawnHuman(target);
            }
            else
            {
                created = (atom)Dm.@new(type, target);
                if (QDELETED(created)) break; // tg: if(QDELETED(created_atom)) return
                Dm.combine(ref created.flags_1, ADMIN_SPAWNED_1);
            }
            if (dir != 0 && !QDELETED(created))
                Waiting.Detach(created.setDir((float)dir));
            if (!QDELETED(created))
                made.Add(created);
        }
        if (made.Count == 1 && made[0] is mob_living_carbon_human human)
            line = Inv($"spawned a human #{human.dm_id} at {where}");
        else if (made.Count == 1)
            line = Inv($"spawned {path} #{made[0].dm_id} at {where} ({NameOf(made[0])})"); // the console's spawn's words
        else
            line = Inv($"spawned {made.Count} x {path} at {where}{(made.Count < amount ? " (the rest deleted themselves)" : "")}");
        return made;
    }

    /// <summary>
    /// The owner (2026-10-13, the dev menu's delete tool): <c>delete</c> is tg's admin Delete (<c>/client/proc/admin_delete</c>,
    /// code/modules/admin/view_variables/admin_delete.dm:1) without its confirmation (the dev menu's click is it):
    /// <c>COMSIG_ADMIN_DELETING</c>, then a turf's <c>ScrapeAway()</c> (a wall becomes its baseturf, a floor its plating) or
    /// a thing's <c>qdel</c>. The host refuses a player's mob before it gets here.
    /// </summary>
    public static string Delete(atom target)
    {
        ArgumentNullException.ThrowIfNull(target);
        string name = NameOf(target);
        turf at = get_turf(target) as turf;
        string where = at == null ? "in nullspace" : Inv($"at ({Dm.str(at.x)},{Dm.str(at.y)},{Dm.str(at.z)})");
        // tg: SEND_SIGNAL(D, COMSIG_ADMIN_DELETING, src), with the deleting admin's client: the dev console has none
        if (Dm.truthy(target._listen_lookup?[COMSIG_ADMIN_DELETING]))
            target._SendSignal(COMSIG_ADMIN_DELETING, Dm.list(target, null));
        if (target is turf t)
        {
            float x = Dm.num(t.x), y = Dm.num(t.y), z = Dm.num(t.z); // read before: the turf that replaces it is another object
            Waiting.Detach(t.ScrapeAway());
            turf now = locate(x, y, z) as turf;
            // the dev tool's hole is a breach: the air around it is linked to it (above and below too) and woken at once, or a room at rest
            // beside a floor scraped to space would not notice it (tg's NORMAL_TURF rebuild wakes nothing)
            if (now is turf_open open)
            {
                open.immediate_calculate_adjacent_turfs();
                foreach (turf near in Dm.each<turf>(open.atmos_adjacent_turfs))
                    SSair.add_to_active(near);
            }
            return Inv($"delete: {name} {where} scraped away{(now != null ? " (now " + NameOf(now) + ")" : "")}");
        }
        qdel(target);
        return Inv($"delete: {name} {where} deleted");
    }

    /// <summary>
    /// The owner (2026-10-13, the dev menu's gas tools): <c>air</c> changes the air of every open turf within
    /// <paramref name="radius"/> of <paramref name="centre"/> on its level but space (tg's space air is immutable) and the
    /// turfs that block air, through tg's gas_mixture procs: <c>add</c> is <c>adjust_gas(gas, moles)</c> (below 0,
    /// <c>remove_specific(gas, -moles)</c>, so a gas never goes negative), <c>remove</c> is <c>set_gas(gas, 0)</c>,
    /// <c>temp</c> is <c>set_temperature(kelvin)</c>, <c>set</c> is the air of <see cref="Gas"/> (<c>parse_gas_string</c>).
    /// <paramref name="arg"/> is tg's gas id (<c>add</c>, <c>remove</c>: <c>gas_id2path</c>) or the gas string (<c>set</c>).
    /// The turfs' overlays follow (<c>update_visuals</c>) and they wake up (<c>add_to_active</c>).
    /// </summary>
    public static string Air(string op, string arg, float number, turf centre, int radius)
    {
        ArgumentNullException.ThrowIfNull(op);
        ArgumentNullException.ThrowIfNull(centre);
        Type gas = null;
        if (op is "add" or "remove")
        {
            // tg: gas_types.dm:33 gas_id2path(id)
            gas = gas_id2path(arg) as Type;
            if (gas == null) return Inv($"air: tg has no gas '{arg}'");
        }
        else if (op is not ("temp" or "set"))
        {
            return "air: unknown operation " + op;
        }
        int cx = (int)Dm.num(centre.x), cy = (int)Dm.num(centre.y), cz = (int)Dm.num(centre.z);
        int changed = 0;
        foreach (turf t in Dm.each<turf>(block(Math.Max(1, cx - radius), Math.Max(1, cy - radius), cz,
            Math.Min((int)Dm.num(Dm.world.maxx), cx + radius), Math.Min((int)Dm.num(Dm.world.maxy), cy + radius), cz)))
        {
            if (t is not turf_open open || open is turf_open_space || Dm.truthy(open.blocks_air) || open.air == null) continue;
            datum_gas_mixture air = open.air;
            switch (op)
            {
                case "add":
                    if (number > 0)
                        air.adjust_gas(gas, number); // tg: gas_mixture.dm:173
                    else
                        air.remove_specific(gas, -number); // tg: gas_mixture.dm:243
                    break;
                case "remove":
                    air.set_gas(gas, 0); // tg: gas_mixture.dm:164 (garbage_collect drops it)
                    break;
                case "temp":
                    air.set_temperature(number); // tg: gas_mixture.dm:168
                    break;
                case "set":
                    // tg: controllers/subsystem/air.dm:779 parse_gas_string(gas_string)
                    open.air = SSair.parse_gas_string(arg) as datum_gas_mixture;
                    break;
            }
            open.update_visuals();
            SSair.add_to_active(open);
            changed++;
        }
        string what = op switch
        {
            "add" => number > 0 ? Inv($"{number:0.###} mol of {arg} added") : Inv($"{-number:0.###} mol of {arg} removed"),
            "remove" => Inv($"all {arg} removed"),
            "temp" => Inv($"the air is {number:0.##} K"),
            _ => Inv($"the air is '{arg}'"),
        };
        return Inv($"air: {what} on {changed} open turf(s) within {radius} of ({cx},{cy},{cz})");
    }

    /// <summary><c>speedscale</c>: the world's capsule speed scale (D106; <see cref="LocomotionSeam.SetSpeedScale"/>).</summary>
    public static string SpeedScale(float scale)
    {
        LocomotionSeam.SetSpeedScale(World.Current, scale);
        return Inv($"capsule speed scale is now {scale:0.###}");
    }

    /// <summary><c>footstride run|walk TILES</c>: the tiles walked per footstep stride (DV233; <see cref="LocomotionSeam.SetFootstride"/>).</summary>
    public static string Footstride(bool walk, float tiles)
    {
        LocomotionSeam.SetFootstride(World.Current, walk, tiles);
        return Inv($"a {(walk ? "walking" : "running")} footstep stride is now {tiles:0.##} tiles (a step every {tiles * 2:0.##})");
    }

    /// <summary><c>speed WHO F</c>: one capsule's speed times F over the world's scale (<see cref="LocomotionSeam.SetOwnSpeed"/>).</summary>
    public static string Speed(mob_living target, float multiplier)
        => LocomotionSeam.SetOwnSpeed(target, multiplier)
            ? Inv($"{target.name}'s speed is now x{multiplier:0.##}")
            : Inv($"{target.name} has no capsule to speed up");

    /// <summary>DV211: the port's projectiles fly this many times tg's speed (the owner, 2026-10-28: tg's 12.5 tiles a second is slow in first person; 2026-11-20: 30% faster again, 3.9).</summary>
    public const float DefaultProjectileSpeedScale = 3.9f;

    /// <summary>The largest <c>projspeed</c>: tg's <c>max_pixels_per_tick</c> (5 tiles) caps a tick's flight anyway.</summary>
    public const float MaxProjectileSpeedScale = 8f;

    /// <summary><c>projspeed</c>: tg's own debug knob, <c>SSprojectiles.pixels_per_decisecond</c> (projectiles.dm:20), at <paramref name="scale"/> times its 32 pixels.</summary>
    public static string ProjectileSpeed(float scale)
    {
        SSprojectiles.pixels_per_decisecond = ICON_SIZE_ALL * scale;
        return Inv($"projectiles fly at {scale:0.###} times tg's speed");
    }

    /// <summary>
    /// M4a (D130; the contract of the M4a contracts step, M4a P5 implements it): <c>give WHO TYPE</c> is tg's admin spawn of
    /// an item into a mob's hands: <c>new TYPE(get_turf(target))</c>, then tg's <c>target.put_in_hands(item)</c> (which drops it
    /// on the turf when the hands are full, as tg's does). <paramref name="typePath"/> is a DM path the parser checked for
    /// syntax; a path tg does not have, or one that is not an <c>/obj/item</c>, is refused with a line and creates nothing.
    /// Returns the line for the requester (the item's name and world id, and whether it reached a hand). tg's own debug items
    /// cover the walks: <c>give me /obj/item/card/id/advanced/debug</c> (<c>/datum/id_trim/admin</c>, all access).
    /// </summary>
    public static string Give(mob_living target, string typePath)
    {
        ArgumentNullException.ThrowIfNull(target);
        Type type = DmTypes.FromPath(typePath);
        if (type == null)
            return Inv($"give: tg has no type {typePath}; nothing created");
        if (!typeof(obj_item).IsAssignableFrom(type))
            return Inv($"give: {typePath} is not an /obj/item; nothing created");
        // tg's admin spawn: new TYPE(get_turf(target)), then put_in_hands (it drops the item on the turf when the hands are full)
        var item = (obj_item)Dm.@new(type, get_turf(target));
        bool held = Dm.truthy(Waiting.NoSleep(target.put_in_hands(item)));
        return Inv($"{Label(target)}: {Dm.str(item.name)} #{item.dm_id} {(held ? "in hand" : "on the floor (the hands are full)")}");
    }

    /// <summary>
    /// M7x slice A: <c>outfit WHO TYPE</c> is tg's admin "Select equipment" (<c>admin_apply_outfit</c>, then <c>equipOutfit</c>): the worn
    /// things are deleted and the outfit's items are made and worn (the job outfits: <c>/datum/outfit/job/security</c>). <paramref name="typePath"/> is a DM path the parser
    /// checked for syntax; a path tg does not have, one that is no <c>/datum/outfit</c>, or a target that is no human is refused.
    /// </summary>
    public static string Outfit(mob_living target, string typePath)
    {
        ArgumentNullException.ThrowIfNull(target);
        Type type = DmTypes.FromPath(typePath);
        if (type == null || !typeof(datum_outfit).IsAssignableFrom(type))
            return Inv($"outfit: {typePath} is no /datum/outfit type of tg's");
        if (target is not mob_living_carbon_human human)
            return Inv($"outfit: {Label(target)} is not a human");
        // selectequipment.dm:222-228: the worn things go first (the pockets too: a dev command has no "Pocket Items" dialog, so "Delete Them"),
        // then the brain's skillchips
        foreach (obj_item item in Dm.each<obj_item>(human.get_equipped_items(INCLUDE_POCKETS)).ToList())
            qdel(item);
        if (human.get_organ_slot(ORGAN_SLOT_BRAIN) is obj_item_organ_brain brain)
            Waiting.NoSleep(brain.destroy_all_skillchips());
        bool equipped = Dm.truthy(Waiting.NoSleep(human.equipOutfit(type)));
        return Inv($"{Label(target)}: {typePath} {(equipped ? "equipped" : "refused by tg")}");
    }

    /// <summary>
    /// Syndicate X1a (D316): <c>lazyload WHO KEY</c> is tg's admin verb Load/Jump Lazy Template: SSmapping loads the template of KEY and
    /// WHO is moved into it (onto a holding facility spot for the ninja den).
    /// </summary>
    public static string LazyLoad(mob_living target, string key)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (GLOB.lazy_templates?[key] is not datum_lazy_template)
            return Inv($"lazyload: no lazy template of the key {key}");
        // admin_verbs.dm:664 jumps to the reservation's bottom left; the dev command puts a holding facility's mob on a holding spot
        Waiting.Detach(LoadAndMove(target, key));
        return Inv($"{Label(target)}: loading the lazy template {key}");
    }

    private static async DmTask LoadAndMove(mob_living target, string key)
    {
        var reservation = await SSmapping.lazy_load_template(key) as datum_turf_reservation;
        if (reservation == null)
            return;
        atom spot = Dm.eq(key, LAZY_TEMPLATE_KEY_NINJA_HOLDING_FACILITY) && GLOB.holdingfacility.len > 0
            ? Dm.pick(GLOB.holdingfacility) as atom
            : reservation.bottom_left_turfs[1] as atom;
        await target.forceMove(spot);
    }

    /// <summary>
    /// Syndicate X1a (D316): <c>antag WHO TYPE</c> is tg's traitor panel making a mind an antagonist (<c>add_antag_datum</c>): the
    /// contractor's and the later syndicate stages' playtests. A path tg does not have, one that is no <c>/datum/antagonist</c>, or a
    /// target without a mind is refused.
    /// </summary>
    public static string Antag(mob_living target, string typePath)
    {
        ArgumentNullException.ThrowIfNull(target);
        Type type = DmTypes.FromPath(typePath);
        if (type == null || !typeof(datum_antagonist).IsAssignableFrom(type))
            return Inv($"antag: {typePath} is no /datum/antagonist type of tg's");
        if (target.mind == null)
            return Inv($"antag: {Label(target)} has no mind");
        object made = Waiting.NoSleep(target.mind.add_antag_datum(type));
        return Inv($"{Label(target)}: {typePath} {(made != null ? "added" : "refused by tg")}");
    }

    /// <summary>Syndicate N3b (D316): <c>trauma [WHO] TYPE</c> is tg's admin "Give brain trauma": WHO gains a lobotomy-resilient brain
    /// trauma of TYPE (a <c>/datum/brain_trauma</c> path), as the admin verb does (<c>gain_trauma(type, TRAUMA_RESILIENCE_LOBOTOMY)</c>).</summary>
    public static string Trauma(mob_living target, string typePath)
    {
        ArgumentNullException.ThrowIfNull(target);
        Type type = DmTypes.FromPath(typePath);
        if (type == null || !typeof(datum_brain_trauma).IsAssignableFrom(type))
            return Inv($"trauma: {typePath} is no /datum/brain_trauma type of tg's");
        if (target is not mob_living_carbon carbon)
            return Inv($"trauma: {Label(target)} is not a carbon");
        object made = Waiting.NoSleep(carbon.gain_trauma(type, TRAUMA_RESILIENCE_LOBOTOMY));
        return Inv($"{Label(target)}: {typePath} {(made != null ? "gained" : "refused by tg")}");
    }

    /// <summary>
    /// Silicons SI2 (D326): <c>robotize WHO</c> is tg's admin "Make Cyborg" (admin/verbs/debug.dm:16): a silicon is refused ("They are already a
    /// cyborg."), anyone else is turned into a cyborg by <c>Robotize</c> (INVOKE_ASYNC), who then picks a model with the module button.
    /// </summary>
    public static string Robotize(mob_living target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (issilicon(target))
            return Inv($"robotize: {Label(target)} is already a cyborg");
        log_admin(Dm.text("[] has been robotized from the dev console.", key_name(target)));
        Waiting.Detach(target.Robotize());
        return Inv($"{Label(target)}: robotized");
    }

    /// <summary>Silicons SI6 (D326): <c>paiize WHO</c> is tg's admin "Make pAI" (pai/debug.dm:1): a card at WHO's feet, WHO's player its pAI.</summary>
    public static string Paiize(mob_living target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (ispAI(target))
            return Inv($"paiize: {Label(target)} is already a pAI");
        if (target.key == null)
            return Inv($"paiize: {Label(target)} has no player");
        log_admin(Dm.text("[] made [] a pAI from the dev console.", "dev", key_name(target)));
        // tg: code/modules/pai/debug.dm:20-26 (the admin verb's name prompt answered with WHO's name)
        var card = Dm.@new<obj_item_pai_card>(get_turf(target));
        var pai = Dm.@new<mob_living_silicon_pai>(card);
        pai.name = Dm.str(target.real_name);
        pai.real_name = pai.name;
        pai.PossessByPlayer(target.key);
        card.set_personality(pai);
        return Inv($"{Label(target)}: made a pAI");
    }

    /// <summary>
    /// Silicons SI4 (D326): <c>aiize WHO [here]</c> is tg's admin "Make AI" (admin/topic.dm:595): an AI is refused ("That's already an AI."),
    /// anyone else becomes an AI by <c>AIize(client, move)</c> (INVOKE_ASYNC), at an AI spawn landmark when <paramref name="move"/>.
    /// </summary>
    public static string Aiize(mob_living target, bool move)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (isAI(target))
            return Inv($"aiize: {Label(target)} is already an AI");
        log_admin(Dm.text("[] AIized [] from the dev console.", "dev", key_name(target)));
        Waiting.Detach(target.AIize(target.client, move ? Dm.TRUE : Dm.FALSE));
        return Inv($"{Label(target)}: AIized");
    }

    /// <summary>
    /// D346 (the owner, 2026-11-21: take over any body, an NPC, a creature or a robot): <c>possess WHO [force]</c> is tg's admin "Assume
    /// Direct Control" (admin/verbs/debug.dm:166). <paramref name="admin"/>'s player moves into <paramref name="target"/> by tg's
    /// <c>PossessByPlayer</c>; a player already in it is made a ghost that cannot go back (<c>ghostize(FALSE)</c>), which tg asks about
    /// first ("Are you sure you wish to assume control of it?") and the command takes as <paramref name="force"/>; an admin who was a
    /// ghost loses the ghost (tg's <c>qdel(adminmob)</c>), an admin in a body leaves it standing without a player. The mob's AI stops
    /// while a player is in it and starts again when the player leaves (tg's ai_controller, <c>continue_processing_when_client</c>).
    /// </summary>
    public static string AssumeDirectControl(mob admin, mob target, bool force)
    {
        ArgumentNullException.ThrowIfNull(admin);
        ArgumentNullException.ThrowIfNull(target);
        if (ReferenceEquals(admin, target))
            return "possess: that is your own mob";
        if (target is mob_dead_new_player)
            return "possess: a lobby player is not in the world";
        string key = admin.key;
        if (string.IsNullOrEmpty(key))
            return "possess: you have no key";
        // tg: :168-172
        if (target.ckey != null && !force)
            return Inv($"possess: {NameOf(target)} is being controlled by {target.key}; add force to take it ({target.key} will be made a ghost)");
        if (QDELETED(target))
            return "The target mob no longer exists.";
        // tg: :174-184
        message_admins(span_adminnotice(Dm.text("[] assumed direct control of [].", key_name_admin(admin), target)));
        log_admin(Dm.text("[] assumed direct control of [].", key_name(admin), target));
        if (target.ckey != null)
            target.ghostize(Dm.FALSE);
        target.PossessByPlayer(key);
        target.client?.init_verbs();
        if (isobserver(admin))
            qdel(admin);
        return "You assume direct control of " + NameOf(target) + ".";
    }

    /// <summary>
    /// D346: <c>givecontrol WHO PLAYER [delete] [force]</c> is tg's admin "Give Direct Control" (admin/verbs/debug.dm:187): the player of
    /// <paramref name="newkey"/> moves into <paramref name="target"/> (tg's <c>M.ckey = newkey.key</c>). A player already in the target is made a
    /// ghost (tg asks first; the command takes <paramref name="force"/>); the new player's old mob is deleted when it was a ghost, or when
    /// <paramref name="deleteOld"/> answers tg's "Do you want to delete [newkey]'s old mob?".
    /// </summary>
    public static string GiveDirectControl(object user, mob target, client newkey, bool deleteOld, bool force)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(newkey);
        if (target is mob_dead_new_player)
            return "givecontrol: a lobby player is not in the world";
        mob oldmob = newkey.mob;
        if (ReferenceEquals(oldmob, target))
            return Inv($"givecontrol: {newkey.key} already plays {NameOf(target)}");
        // tg: :191-205
        if (target.ckey != null && !force)
            return Inv($"givecontrol: {NameOf(target)} is being controlled by {target.key}; add force to give it away ({target.key} will be made a ghost)");
        bool delmob = isobserver(oldmob) || deleteOld;
        if (QDELETED(target))
            return "The target mob no longer exists, aborting.";
        if (target.ckey != null)
            target.ghostize(Dm.FALSE);
        // tg: :206-211
        target.ckey = newkey.key;
        target.client?.init_verbs();
        if (delmob && oldmob != null)
            qdel(oldmob);
        message_admins(span_adminnotice(Dm.text("[] gave away direct control of [] to [].", key_name_admin(user), target, newkey)));
        log_admin(Dm.text("[] gave away direct control of [] to [].", key_name(user), target, newkey));
        return Inv($"{newkey.key} now controls {NameOf(target)}{(delmob ? " (the old mob deleted)" : "")}");
    }

    /// <summary>
    /// M7x slice A, before the guns (a dev hook for the tests and playtests of aimed shots): <c>shoot WHO TYPE</c> makes WHO's clicks fire a
    /// projectile of TYPE (an <c>/obj/projectile</c> path) with the click's params, as a gun's casing does (<see cref="FireAimed"/>);
    /// <c>shoot WHO off</c> stops it. The player's mob needs a client.
    /// </summary>
    public static string Shoot(mob_living target, string typePath)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.client == null)
            return Inv($"shoot: {Label(target)} has no client");
        if (typePath == null)
        {
            target.client.dm_dev_shot = null;
            return Inv($"{Label(target)}: clicks no longer shoot");
        }
        Type type = DmTypes.FromPath(typePath);
        if (type == null || !typeof(obj_projectile).IsAssignableFrom(type))
            return Inv($"shoot: {typePath} is no /obj/projectile type of tg's");
        target.client.dm_dev_shot = type;
        return Inv($"{Label(target)}: clicks shoot {typePath}");
    }

    /// <summary>
    /// The dev hook of <see cref="Shoot"/>: <paramref name="firer"/> fires a projectile of <paramref name="type"/> at <paramref name="target"/>
    /// with the click's <paramref name="params"/>, as tg's <c>fire_casing</c> does it (<c>ammunition/_firing.dm</c>: <c>ready_proj</c>'s
    /// firer, zone and prone targeting, <c>throw_proj</c>'s point-blank target, <c>aim_projectile(target, user, params2list(params))</c>,
    /// <c>fire(null, direct_target)</c>), without a gun or a casing. Detached, as a click's verb.
    /// </summary>
    public static void FireAimed(mob firer, atom target, Type type, string @params)
    {
        ArgumentNullException.ThrowIfNull(firer);
        turf curloc = get_turf(firer), targloc = get_turf(target);
        if (curloc == null || targloc == null)
            return;
        var bullet = (obj_projectile)Dm.@new(type, curloc);
        bullet.original = target;
        bullet.firer = firer;
        bullet.fired_from = firer;
        bullet.hit_prone_targets = Dm.num((firer as mob_living)?.combat_mode);
        bullet.def_zone = firer.zone_selected;
        Waiting.Detach(FireAimed(bullet, curloc, targloc, target, firer, @params));
    }

    private static async DmTask FireAimed(obj_projectile bullet, turf curloc, turf targloc, atom target, mob firer, string @params)
    {
        atom direct_target = Dm.truthy(await curloc.Adjacent(targloc, targloc, bullet)) ? target : null;
        await bullet.aim_projectile(target, firer, Dm.params2list(@params), 0);
        if (QDELETED(bullet))
            return;
        await bullet.fire(null, direct_target);
    }

    /// <summary>The slot names of <see cref="Drop"/> and tg's slot flags.</summary>
    public static readonly System.Collections.Frozen.FrozenDictionary<string, float> DropSlots = new Dictionary<string, float>(StringComparer.Ordinal)
    {
        ["back"] = ITEM_SLOT_BACK, ["suit"] = ITEM_SLOT_OCLOTHING, ["uniform"] = ITEM_SLOT_ICLOTHING, ["head"] = ITEM_SLOT_HEAD,
        ["mask"] = ITEM_SLOT_MASK, ["eyes"] = ITEM_SLOT_EYES, ["ears"] = ITEM_SLOT_EARS, ["gloves"] = ITEM_SLOT_GLOVES,
        ["shoes"] = ITEM_SLOT_FEET, ["belt"] = ITEM_SLOT_BELT, ["id"] = ITEM_SLOT_ID, ["neck"] = ITEM_SLOT_NECK,
        ["suitstorage"] = ITEM_SLOT_SUITSTORE, ["lpocket"] = ITEM_SLOT_LPOCKET, ["rpocket"] = ITEM_SLOT_RPOCKET,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// The owner's play test (2026-10-15: a backpack could not be taken off before drag and drop is ported): <c>drop WHO SLOT</c>
    /// drops what WHO wears in SLOT to the floor through tg's <c>dropItemToGround(get_item_by_slot(slot))</c>, as an admin's
    /// strip does (the item's own dropped/unequipped procs run).
    /// </summary>
    public static string Drop(mob_living target, string slot)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!DropSlots.TryGetValue(slot ?? "", out float flag))
            return Inv($"drop: '{slot}' is not a slot ({string.Join(", ", DropSlots.Keys)})");
        if (target.get_item_by_slot(flag) is not obj_item item)
            return Inv($"{Label(target)}: nothing in {slot}");
        bool dropped = Dm.truthy(Waiting.NoSleep(target.dropItemToGround(item)));
        return Inv($"{Label(target)}: {Dm.str(item.name)} {(dropped ? "dropped to the floor" : "could not be dropped (tg refused: e.g. TRAIT_NODROP)")}");
    }

    /// <summary>
    /// The owner's play test (2026-10-15): <c>gravity WHO on|off [now]</c> flips the main breaker of the station gravity generator
    /// of WHO's level as its control panel's button does (tg's <c>gentoggle</c>, gravitygenerator.dm:304: <c>breaker</c>, then
    /// <c>set_power</c>): the generator charges up or down by 2 a machine tick (:376) and enables or disables the level's
    /// gravity at 100 or 0, with tg's empulse sounds, so gravity goes off after about 100 s as in tg. <c>now</c> (a dev
    /// shortcut, not tg's) also sets the charge to its end and calls tg's <c>enable</c> or <c>disable</c> at once. (A bare
    /// <c>disable</c> without the breaker lasted a second: the next <c>set_power</c> charged it up again.)
    /// </summary>
    public static string Gravity(mob_living target, bool on, bool now = false)
    {
        ArgumentNullException.ThrowIfNull(target);
        obj_machinery_gravity_generator_main found = null;
        foreach (obj_machinery_gravity_generator_main gen in Dm.each<obj_machinery_gravity_generator_main>(Dm.world))
        {
            if (Dm.num(gen.z) == Dm.num(target.z))
            {
                found = gen;
                break;
            }
        }
        if (found == null)
            return Inv($"gravity: no gravity generator on level {Dm.str(target.z)}");
        // tg: code/modules/power/gravitygenerator.dm:304 (ui_act "gentoggle")
        found.breaker = on ? Dm.TRUE : Dm.FALSE;
        found.set_power();
        string where = Inv($"({Dm.str(found.x)},{Dm.str(found.y)},{Dm.str(found.z)})");
        if (!now)
            return Inv($"gravity: the gravity generator at {where}: breaker {(on ? "on, charging up" : "off, discharging")} (tg's pace: gravity {(on ? "returns" : "goes")} when the charge reaches {(on ? 100 : 0)}, about {Dm.num(on ? 100 - Dm.num(found.charge_count) : Dm.num(found.charge_count)):0} at 2 a machine tick)");
        found.charge_count = on ? 100 : 0;
        if (on)
            Waiting.NoSleep(found.enable());
        else
            Waiting.NoSleep(found.disable());
        return Inv($"gravity: the gravity generator at {where} is {(on ? "on" : "off")} now");
    }

    /// <summary>
    /// M7 lights slice: <c>areapower X Y [Z] equip|light|environ|all on|off</c> sets a power channel of the area at a turf the way
    /// its APC does (apc_power_proc.dm:32-41: <c>area.power_light = ...</c>, <c>power_equip</c>, <c>power_environ</c>, then
    /// <c>area.power_change()</c>, which tells every machine of the area). Until the APC slice nothing else turns a channel off:
    /// the lights of the area go to their emergency cells (or back to normal), and a machine that needs the channel loses it.
    /// An area that needs no power has no APC and nothing to switch (<c>requires_power</c>).
    /// </summary>
    public static string AreaPower(turf spot, string channel, bool on)
    {
        ArgumentNullException.ThrowIfNull(spot);
        area where = get_area(spot) as area;
        string place = Inv($"({Dm.str(spot.x)},{Dm.str(spot.y)},{Dm.str(spot.z)})");
        if (where == null)
            return $"areapower: {place} is in no area";
        if (!Dm.truthy(where.requires_power) || where is area_space)
            return $"areapower: {Dm.str(where.name)} at {place} needs no power (requires_power is off), so it has no APC to switch";
        float state = on ? Dm.TRUE : Dm.FALSE;
        switch (channel)
        {
            case "equip": where.power_equip = state; break;
            case "light": where.power_light = state; break;
            case "environ": where.power_environ = state; break;
            case "all":
                where.power_light = state;
                where.power_equip = state;
                where.power_environ = state;
                break;
            default:
                return $"areapower: '{channel}' is not a power channel (equip, light, environ, all)";
        }
        where.power_change(); // tg: code/modules/power/apc/apc_power_proc.dm:41
        return Inv($"areapower: {Dm.str(where.name)} at {place}: {channel} {(on ? "on" : "off")} (equip {(Dm.truthy(where.power_equip) ? "on" : "off")}, light {(Dm.truthy(where.power_light) ? "on" : "off")}, environ {(Dm.truthy(where.power_environ) ? "on" : "off")})");
    }

    /// <summary>
    /// The owner (2026-11-21, mech guns): <c>pilot WHO</c>, the target climbs into a free mech on or next to its tile as tg's
    /// <c>mob_try_enter</c> does after its wait (<c>mob_enter</c>, then <c>moved_inside</c>), or climbs out of the mech it is in (<c>mob_exit</c>).
    /// </summary>
    public static string Pilot(mob_living target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.loc is obj_vehicle_sealed_mecha inside)
        {
            Waiting.NoSleep(inside.mob_exit(target));
            return Inv($"{Label(target)}: out of {inside.name}");
        }
        foreach (obj_vehicle_sealed_mecha mech in Dm.each<obj_vehicle_sealed_mecha>(range(1, target)))
        {
            if (mech.gc_destroyed != null || Dm.num(LAZYLEN(mech.occupants)) >= Dm.num(mech.max_occupants))
                continue;
            Waiting.NoSleep(mech.mob_enter(target)); // tg's mob_try_enter after its do_after: mob_enter, then moved_inside
            Waiting.NoSleep(mech.moved_inside(target));
            return Inv($"{Label(target)}: pilots {mech.name}");
        }
        return Inv($"pilot: no free mech by {Label(target)}");
    }

    /// <summary>
    /// <c>godmode</c>: toggles tg's <c>TRAIT_GODMODE</c> on the target (<c>ADD_TRAIT</c>/<c>REMOVE_TRAIT</c> with the admin
    /// source). The owner (2026-10-15): no status effect and no outside slowdown under the dev godmode. tg's godmode already
    /// stops damage, the stun family and sleep (<c>check_stun_immunity</c>, <c>Sleeping</c>: status_procs.dm:13, :397) and
    /// the breath (<c>check_breath</c>, _lungs.dm:640); tg's own immunity traits add what it leaves: <see cref="GodmodeTraits"/>
    /// (no stagger, no daze, no catching fire, no blast) and <c>ignore_slowdown(ADMIN_TRAIT)</c> (TRAIT_IGNORESLOWDOWN: only the
    /// <c>IGNORE_NOSLOW</c> modifiers and the speedups count, human_movement.dm:1). Off takes them all away.
    /// </summary>
    public static string GodMode(mob_living target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (HAS_TRAIT_FROM(target, TRAIT_GODMODE, ADMIN_TRAIT))
        {
            REMOVE_TRAIT(target, TRAIT_GODMODE, ADMIN_TRAIT);
            target.remove_traits(GodmodeTraits(), GodmodeSource);
            SyncIgnoreSlowdown(target);
            return Inv($"{Label(target)}: godmode off");
        }
        ADD_TRAIT(target, TRAIT_GODMODE, ADMIN_TRAIT);
        target.add_traits(GodmodeTraits(), GodmodeSource);
        SyncIgnoreSlowdown(target);
        return Inv($"{Label(target)}: godmode on");
    }

    /// <summary>
    /// The immunity traits the dev godmode adds beside <c>TRAIT_GODMODE</c>, each tg's guard of a status effect that the
    /// godmode does not stop: <c>TRAIT_NO_STAGGER</c> (staggered.dm:17), <c>TRAIT_NO_SIDE_KICK</c> (dazed, staggered.dm:66)
    /// and <c>TRAIT_NOFIRE</c> (fire_stacks.dm:245, <c>ignite</c>); the owner (2026-10-16): <c>TRAIT_BOMBIMMUNE</c>, which tg's
    /// <c>EX_ACT</c> checks (explosions.dm:26), since a devastating blast gibs a mob whatever its godmode (human_defense.dm:288).
    /// </summary>
    public static DmList GodmodeTraits() => Dm.list(TRAIT_NO_STAGGER, TRAIT_NO_SIDE_KICK, TRAIT_NOFIRE, TRAIT_BOMBIMMUNE,
        TRAIT_SHOCKIMMUNE, TRAIT_TESLA_SHOCKIMMUNE, TRAIT_MADNESS_IMMUNE); // the owner (2026-11-02): no shocks, no hallucination pulses

    /// <summary>
    /// True when the dev godmode or invulnerability keeps every status effect off the mob (the owner, 2026-11-02: "no effects
    /// under godmode"); the dev commands' own effects (<see cref="Effect"/>, <see cref="Hallucinate"/>) still apply.
    /// </summary>
    public static bool RefusesStatusEffects(mob_living target)
        => !HAS_TRAIT(target, TRAIT_DEV_EFFECT) && (HAS_TRAIT_FROM(target, TRAIT_GODMODE, ADMIN_TRAIT) || HAS_TRAIT(target, TRAIT_DEV_INVULNERABLE));

    // Marks the target while a dev command applies its effect (a trait: world state, not a static).
    private const string TRAIT_DEV_EFFECT = "anomaly_dev_effect";

    private static T DevEffect<T>(mob_living target, Func<T> apply)
    {
        ADD_TRAIT(target, TRAIT_DEV_EFFECT, ADMIN_TRAIT);
        try { return apply(); }
        finally { REMOVE_TRAIT(target, TRAIT_DEV_EFFECT, ADMIN_TRAIT); }
    }

    /// <summary>
    /// <c>esp</c> (the owner, 2026-11-02: see the ghosts): tg's Toggle Admin ESP (admin/verbs/admin_verbs.dm:94) for a living mob, the
    /// <c>admin_esp</c> status effect (see_invisible up to <c>SEE_INVISIBLE_ADMIN</c>) and <c>TRAIT_ADMIN_ESP</c>; again: off.
    /// </summary>
    public static string Esp(mob_living target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (HAS_TRAIT(target, TRAIT_ADMIN_ESP))
        {
            target.remove_status_effect(typeof(datum_status_effect_admin_esp));
            REMOVE_TRAIT(target, TRAIT_ADMIN_ESP, ADMIN_TRAIT);
            return Inv($"{Label(target)}: esp off (no ghosts seen)");
        }
        DevEffect(target, () => Waiting.NoSleep(target.apply_status_effect(typeof(datum_status_effect_admin_esp))));
        ADD_TRAIT(target, TRAIT_ADMIN_ESP, ADMIN_TRAIT);
        return Inv($"{Label(target)}: esp on (the ghosts are seen)");
    }

    /// <summary>The alpha of an invisimin mob (the owner, 2026-11-20: "the model is see-through"; tg sets 0 only for an observer).</summary>
    public const int InvisiminAlpha = 128;

    /// <summary>
    /// <c>invisimin</c> (the owner, 2026-11-20: invisible as a ghost): tg's Invisimin (admin/admin_verbs.dm:62). DEVIATION(DV576):
    /// the invisibility is <c>INVISIBILITY_OBSERVER</c>, not <c>INVISIBILITY_ADMIN</c>, so ghosts and esp see the mob as they see a ghost
    /// (the replicated observer layer ends at 60), and the living mob is drawn at <see cref="InvisiminAlpha"/>; again: off.
    /// </summary>
    public static string Invisimin(mob_living target)
    {
        ArgumentNullException.ThrowIfNull(target);
        DmList traits = Dm.list(TRAIT_INVISIMIN, TRAIT_ORBITING_FORBIDDEN, TRAIT_MOVE_PHASING, TRAIT_PIERCEIMMUNE, TRAIT_INVISIBLE_TO_CAMERA);
        if (HAS_TRAIT(target, TRAIT_INVISIMIN))
        {
            target.remove_traits(traits, ADMIN_TRAIT);
            target.add_to_all_human_data_huds();
            target.RemoveInvisibility(INVISIBILITY_SOURCE_INVISIMIN);
            target.alpha = Dm.num(Dm.initial(target, (atom p) => p.alpha));
            return Inv($"{Label(target)}: invisimin off (seen again)");
        }
        target.add_traits(traits, ADMIN_TRAIT);
        target.remove_from_all_data_huds();
        target.SetInvisibility(INVISIBILITY_OBSERVER, INVISIBILITY_SOURCE_INVISIMIN, INVISIBILITY_PRIORITY_ADMIN);
        target.alpha = InvisiminAlpha;
        QDEL_NULL(ref target.orbiters);
        return Inv($"{Label(target)}: invisimin on (only ghosts and esp see you)");
    }

    /// <summary>The dev invulnerability's trait (no tg trait: the port's own).</summary>
    public const string TRAIT_DEV_INVULNERABLE = "anomaly_dev_invulnerable";

    /// <summary>The trait source of the invulnerability's godmode traits.</summary>
    public const string InvulnerableSource = ADMIN_TRAIT + " (invulnerable)";

    /// <summary>
    /// <c>invuln</c> (the owner, 2026-11-02): the godmode's traits from a source of its own, tg's <c>TRAIT_PUSHIMMUNE</c> (no bump
    /// push, no swap) and the port's <see cref="TRAIT_DEV_INVULNERABLE"/>, which refuses every other mob's click and drag on the mob
    /// and on what it holds or wears (<see cref="ShieldsFrom"/>): no shove, grab, strip, cuffs or disarm. Off takes them away.
    /// </summary>
    public static string Invulnerable(mob_living target)
    {
        ArgumentNullException.ThrowIfNull(target);
        DmList traits = GodmodeTraits();
        traits.Add(TRAIT_GODMODE);
        traits.Add(TRAIT_PUSHIMMUNE);
        if (HAS_TRAIT(target, TRAIT_DEV_INVULNERABLE))
        {
            REMOVE_TRAIT(target, TRAIT_DEV_INVULNERABLE, InvulnerableSource);
            target.remove_traits(traits, InvulnerableSource);
            return Inv($"{Label(target)}: invulnerable off");
        }
        ADD_TRAIT(target, TRAIT_DEV_INVULNERABLE, InvulnerableSource);
        target.add_traits(traits, InvulnerableSource);
        return Inv($"{Label(target)}: invulnerable on");
    }

    /// <summary>
    /// True when a click or drag of <paramref name="user"/> on <paramref name="thing"/> is refused: the thing is an invulnerable mob
    /// other than the user, or is held or worn by one.
    /// </summary>
    // DEVIATION(DV258): the dev invulnerability refuses others' clicks and drags on the mob
    public static bool ShieldsFrom(mob user, atom thing)
    {
        atom holder = thing is mob ? thing : thing?.loc as mob;
        return holder is mob_living shielded && !ReferenceEquals(shielded, user) && HAS_TRAIT(shielded, TRAIT_DEV_INVULNERABLE);
    }

    /// <summary>
    /// The trait sources of the godmode's and the fly's extra traits: tg's <c>ADMIN_TRAIT</c> with the switch's name (tg keys
    /// a trait's sources by text; a source per switch, so turning one switch off leaves the other's traits).
    /// </summary>
    public const string GodmodeSource = ADMIN_TRAIT + " (godmode)", FlySource = ADMIN_TRAIT + " (fly)";

    // TRAIT_IGNORESLOWDOWN from ADMIN_TRAIT while the fly or the godmode is on, through tg's ignore_slowdown and
    // unignore_slowdown (status_procs.dm:606-613), which also recompute the speed (update_movespeed).
    private static void SyncIgnoreSlowdown(mob_living target)
    {
        bool wanted = HAS_TRAIT_FROM(target, TRAIT_GODMODE, ADMIN_TRAIT) || HAS_TRAIT_FROM(target, TRAIT_MOVE_FLYING, ADMIN_TRAIT);
        bool has = HAS_TRAIT_FROM(target, TRAIT_IGNORESLOWDOWN, ADMIN_TRAIT);
        if (wanted && !has)
            target.ignore_slowdown(ADMIN_TRAIT);
        else if (!wanted && has)
            target.unignore_slowdown(ADMIN_TRAIT);
    }

    /// <summary>
    /// <c>fly</c>: toggles tg's <c>TRAIT_MOVE_FLYING</c> with the admin source (the movetype handler sets <c>FLYING</c>, which
    /// <c>Process_Spacemove</c> lets move without gravity). The owner (2026-10-15): gravity and the air do not move the flyer
    /// and other things' slowdowns do not act on it: tg's <c>TRAIT_NEGATES_GRAVITY</c> (the magboots' trait: on a turf with
    /// ground the mob ignores gravity, <c>on_negate_gravity</c>, init_signals.dm:293, so the space wind leaves it,
    /// <c>mob_negates_gravity</c> in living.dm:1113) and <c>ignore_slowdown(ADMIN_TRAIT)</c>. Off takes them away.
    /// </summary>
    public static string Fly(mob_living target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (HAS_TRAIT_FROM(target, TRAIT_MOVE_FLYING, ADMIN_TRAIT))
        {
            REMOVE_TRAIT(target, TRAIT_MOVE_FLYING, ADMIN_TRAIT);
            REMOVE_TRAIT(target, TRAIT_NEGATES_GRAVITY, FlySource);
            SyncIgnoreSlowdown(target);
            return Inv($"{Label(target)}: fly off");
        }
        ADD_TRAIT(target, TRAIT_MOVE_FLYING, ADMIN_TRAIT);
        REMOVE_TRAIT(target, TRAIT_ON_CLIMBABLE, ELEMENT_TRAIT(typeof(datum_element_climb_walkable)));
        ADD_TRAIT(target, TRAIT_NEGATES_GRAVITY, FlySource);
        SyncIgnoreSlowdown(target);
        return Inv($"{Label(target)}: fly on");
    }

    /// <summary><c>noclip</c>: toggles tg's <c>incorporeal_move</c> between <c>INCORPOREAL_MOVE_BASIC</c> and off (an admin's var edit in tg; the capsule then passes walls, DV29).</summary>
    public static string NoClip(mob_living target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (Dm.eq(target.incorporeal_move, INCORPOREAL_MOVE_BASIC))
        {
            target.incorporeal_move = 0;
            return Inv($"{Label(target)}: noclip off");
        }
        target.incorporeal_move = INCORPOREAL_MOVE_BASIC;
        return Inv($"{Label(target)}: noclip on");
    }

    /// <summary>
    /// M10 slice F: <c>startnow</c> is tg's admin Start Now (<c>admin/verbs/server.dm:106</c>), without its rights check and its
    /// confirmation (the dev console is the operator's): the round starts at the ticker's next fire (<c>start_immediately</c>);
    /// a second use before then cancels it and the countdown goes to 3 minutes. The line replaces tg's admin messages.
    /// </summary>
    /// <summary>
    /// The tram port: <c>tram [PLATFORM]</c> asks SStransport to send the map's tram to platform <paramref name="platform"/> (0: the platform
    /// nearest along its line that it does not stand at), as its call button does (tram_machinery.dm:73: COMSIG_TRANSPORT_REQUEST), from
    /// its cabinet; the answer is the request's response.
    /// </summary>
    public static string Tram(int platform)
    {
        datum_transport_controller_linear_tram tram = Dm.each<datum_transport_controller_linear_tram>((DmList)SStransport.transports_by_type?[TRANSPORT_TYPE_TRAM]).FirstOrDefault();
        if (tram == null)
            return "this map has no tram";
        if (tram.paired_cabinet == null)
            return "the tram has no controls cabinet: it takes no requests";
        if (platform == 0)
        {
            obj_effect_landmark_transport_nav_beacon_tram_platform next = null;
            foreach (obj_effect_landmark_transport_nav_beacon_tram_platform p in Dm.each<obj_effect_landmark_transport_nav_beacon_tram_platform>((DmList)SStransport.nav_beacons?[tram.specific_transport_id]))
                if (p != tram.idle_platform && tram.nav_beacon != null && (next == null || get_dist(tram.nav_beacon, p) < get_dist(tram.nav_beacon, next)))
                    next = p;
            if (next == null)
                return "the tram's line has no other platform";
            platform = (int)Dm.num(next.platform_code);
        }
        if (Dm.truthy(tram.controller_active))
            return "the tram is busy (on its way or about to leave): it takes no request now";
        SStransport.incoming_request(tram.paired_cabinet, tram.specific_transport_id, platform);
        return Dm.truthy(tram.controller_active) ? "the tram leaves for platform " + platform
            : "the tram did not take the request for platform " + platform + " (no such platform, already there, or out of service)";
    }

    public static string StartNow()
    {
        DmList waiting_states = Dm.list(GAME_STATE_PREGAME, GAME_STATE_STARTUP);
        if (!Dm.@in(SSticker.current_state, waiting_states))
            return "The game has already started!";

        if (Dm.truthy(SSticker.start_immediately))
        {
            SSticker.start_immediately = Dm.FALSE;
            SSticker.SetTimeLeft(3 * 60 * 10); // 3 MINUTES
            to_chat(Dm.world, span_big(span_notice("The game will start in 3 minutes.")));
            Dm.output_to(Dm.world, Dm.sound("sound/announcer/default/attention.ogg")); // SEND_SOUND (DIRECT_OUTPUT, rules 12)
            return "immediate game start cancelled: the game will start in 3 minutes";
        }

        SSticker.start_immediately = Dm.TRUE;
        if (Dm.eq(SSticker.current_state, GAME_STATE_STARTUP))
            return "The server is still setting up, but the round will be started as soon as possible.";
        return "the round starts now";
    }

    /// <summary>
    /// M10 slice J: <c>reboot [SECONDS]</c> is tg's admin Reboot World with its regular restart (<c>admin/verbs/server.dm:25</c>,
    /// REGULAR_RESTART and REGULAR_RESTART_DELAYED), without its confirmation and the TGS event: the ticker's <c>Reboot</c> after the
    /// delay, then the world reboots into the next round.
    /// </summary>
    public static string Reboot(float seconds, mob user, bool now = false)
    {
        string by = user?.client?.key ?? "the server console";
        if (now)
        {
            // tg's Hard Restart (No Delay/Feedback Reason), admin/verbs/server.dm:62: world.Reboot() with no countdown (the owner, 2026-11-02)
            to_chat(Dm.world, Dm.text("World reboot - Initiated by [].", by));
            Waiting.Detach(Dm.world.Reboot());
            return "the world reboots now";
        }
        Waiting.Detach(SSticker.Reboot("Initiated by " + by + ".", "admin reboot - by " + by, seconds * 10f));
        return Inv($"the world reboots in {seconds:0.#} s");
    }

    /// <summary>
    /// M10 slice G5 (D273): <c>shuttle call [norecall]</c> is tg's admin Call Shuttle (<c>admin/verbs/adminshuttle.dm:33</c>) and
    /// <c>shuttle recall</c> its Cancel Shuttle (:50), without the rights check and the confirmation (the dev console is the
    /// operator's); the line replaces tg's admin messages. A called shuttle flies with tg's timers, announcements and recall rules.
    /// </summary>
    /// <summary>
    /// Syndicate N1a (D316): <c>nukecode [CODE]</c> sets every nuclear bomb's code (tg's codes are set by the operatives' team, the junk
    /// mail or an admin): CODE, or a random one (tg's random_nukecode) when none is given.
    /// </summary>
    public static string NukeCode(string code)
    {
        code ??= random_nukecode();
        int count = 0;
        foreach (obj_machinery_nuclearbomb nuke in Dm.each<obj_machinery_nuclearbomb>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_nuclearbomb))))
        {
            nuke.r_code = code;
            count++;
        }
        return count == 0 ? "nukecode: there is no nuke" : Inv($"nukecode: {count} nuke{(count == 1 ? "" : "s")} set to {code}");
    }

    public static string Shuttle(string mode, mob user)
    {
        obj_docking_port_mobile_emergency emergency = SSshuttle.emergency;
        if (emergency == null)
            return "shuttle: there is no emergency shuttle";
        if (datum_controller_subsystem_shuttle.EMERGENCY_AT_LEAST_DOCKED())
            return $"shuttle: the emergency shuttle is past its call ({emergency.getModeStr()})";
        if (mode == "recall")
        {
            if (!Dm.truthy(Waiting.NoSleep(SSshuttle.cancel_evac(user, hide_origin: Dm.TRUE)))) // handles the shuttle set unrecallable
                return "shuttle: it cannot be recalled now";
            log_admin(Dm.text("[] admin-recalled the emergency shuttle.", key_name(user)));
            return "the emergency shuttle is recalled";
        }
        if (mode == "dock")
        {
            // the owner (2026-11-02): the shuttle docks now. It is called if it was not, and its call timer runs out, so the next
            // SSshuttle fire's check() docks it with tg's announcement and dock timer (emergency.dm:166)
            if (emergency.mode != SHUTTLE_CALL)
                emergency.request();
            if (emergency.mode != SHUTTLE_CALL)
                return $"shuttle: the emergency shuttle cannot be called ({emergency.getModeStr()})";
            emergency.setTimer(0);
            log_admin(Dm.text("[] admin-docked the emergency shuttle.", key_name(user)));
            return "the emergency shuttle docks with the station";
        }
        bool noRecall = mode == "call norecall";
        if (noRecall)
        {
            SSshuttle.admin_emergency_no_recall = Dm.TRUE;
            emergency.mode = SHUTTLE_IDLE;
        }
        emergency.request();
        // as a crew call does (SSshuttle.requestEvac, shuttle.dm:416): the status displays start showing the timer
        if (SSradio.return_frequency(FREQ_STATUS_DISPLAYS) is datum_radio_frequency displays)
            displays.post_signal(null, Dm.@new<datum_signal>(Dm.list(Dm.kv("command", "update"))));
        log_admin(Dm.text("[] admin-called the emergency shuttle.", key_name(user)));
        return noRecall ? "the emergency shuttle is called (non-recallable)" : "the emergency shuttle is called";
    }

    /// <summary><c>shuttle time</c>: tg's admin timers (<c>SSshuttle.emergency_call_time</c> and its kin, and the shuttle's running timer).</summary>
    public static string ShuttleTime(int seconds)
    {
        if (SSshuttle.emergency == null)
            return "shuttle: there is no emergency shuttle";
        SSshuttle.emergency_call_time = SSshuttle.emergency_dock_time = SSshuttle.emergency_escape_time = seconds * 10;
        if (Dm.truthy(SSshuttle.emergency.timer))
            SSshuttle.emergency.setTimer(seconds * 10);
        return $"the emergency shuttle's times are {seconds} s";
    }

    /// <summary><c>god</c>: fly, noclip and godmode on together; all three off when all three are on.</summary>
    /// <summary>
    /// <c>up WHO</c> / <c>down WHO</c>: tg's Move Upwards and Move Downwards keys (keybinding/movement.dm zlevel_upwards: <c>mob.up()</c>, <c>down()</c>), which a flying
    /// mob (the dev fly) or one on a ladder uses to change level (the owner, 2026-11-10: fly from one level to another). With noclip (incorporeal) it passes the
    /// floors at once, as tg's ghost does (observer_movement.dm: ZMOVE_IGNORE_OBSTACLES).
    /// </summary>
    public static string Level(mob_living target, bool up)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (Dm.truthy(target.incorporeal_move))
        {
            turf start = get_turf(target);
            if ((up ? GET_TURF_ABOVE(start) : GET_TURF_BELOW(start)) is not turf next)
                return Inv($"{Label(target)}: no level {(up ? "above" : "below")}");
            Waiting.Detach(target.zMove(up ? Dm.UP : Dm.DOWN, next, Dm.bitor(Dm.bitor(ZMOVE_FLIGHT_FLAGS, ZMOVE_FEEDBACK), ZMOVE_IGNORE_OBSTACLES)));
            return Inv($"{Label(target)}: {(up ? "up" : "down")} to level {next.z}");
        }
        Waiting.Detach(up ? target.up() : target.down());
        return Inv($"{Label(target)}: moving {(up ? "up" : "down")}");
    }

    public static string God(mob_living target)
    {
        ArgumentNullException.ThrowIfNull(target);
        bool fly = HAS_TRAIT_FROM(target, TRAIT_MOVE_FLYING, ADMIN_TRAIT);
        bool noclip = Dm.eq(target.incorporeal_move, INCORPOREAL_MOVE_BASIC);
        bool god = HAS_TRAIT_FROM(target, TRAIT_GODMODE, ADMIN_TRAIT);
        bool off = fly && noclip && god;
        if (fly == off)
            Fly(target);
        if (noclip == off)
            NoClip(target);
        if (god == off)
            GodMode(target);
        return Inv($"{Label(target)}: god {(off ? "off (fly, noclip and godmode off)" : "on (fly, noclip and godmode on)")}");
    }

    /// <summary>
    /// M4b slice 1 (D134, the owner's dev breach): <c>breach</c> opens a turf to space. Every dense object on it that stops
    /// the air (a window, a door: <c>can_atmos_pass</c> other than <c>ATMOS_PASS_YES</c>) is deleted (tg's <c>qdel</c>), then the
    /// turf becomes space through tg's <c>ChangeTurf(/turf/open/space)</c> (a wall or a floor alike); tg's LINDA then empties
    /// the room into it, with the space wind.
    /// </summary>
    public static string Breach(turf target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target is turf_open_space)
            return Inv($"breach: ({Dm.str(target.x)},{Dm.str(target.y)},{Dm.str(target.z)}) is already space");
        int removed = 0;
        var gone = new List<(Type Type, float Dir)>();
        foreach (obj thing in Dm.each<obj>(target.contents))
        {
            if (Dm.truthy(thing.density) && thing.can_atmos_pass != ATMOS_PASS_YES)
            {
                gone.Add((thing.GetType(), Dm.num(thing.dir)));
                qdel(thing);
                removed++;
            }
        }
        string was = Dm.str(target.name);
        string where = Inv($"({Dm.str(target.x)},{Dm.str(target.y)},{Dm.str(target.z)})"); // before the replaced turf leaves the map
        Breaches.Of(World.CurrentOrNull)[((int)target.x, (int)target.y, (int)target.z)] = (target.GetType(), gone);
        Waiting.Detach(target.ChangeTurf(typeof(turf_open_space)));
        return Inv($"breach: {was} at {where} is space now ({removed} air-tight object(s) removed)");
    }

    /// <summary>
    /// M4b slice 3: <c>ignite</c> is a spark on <paramref name="target"/>: tg's <c>/obj/effect/particle_effect/sparks</c> calls
    /// <c>location.hotspot_expose(1000, 100)</c> (effects_sparks.dm:94), which starts a hotspot when the tile's air has at least
    /// 0.5 moles of oxygen and of plasma, tritium or hydrogen (LINDA_fire.dm:23).
    /// </summary>
    public static string Ignite(turf target)
    {
        ArgumentNullException.ThrowIfNull(target);
        string where = Inv($"({Dm.str(target.x)},{Dm.str(target.y)},{Dm.str(target.z)})");
        if (target is not turf_open open)
            return Inv($"ignite: {Dm.str(target.name)} at {where} is not an open turf");
        open.hotspot_expose(1000, 100);
        return open.active_hotspot != null
            ? Inv($"ignite: {Dm.str(target.name)} at {where} is burning")
            : Inv($"ignite: a spark at {where}, but nothing caught fire (it needs oxygen and plasma, tritium or hydrogen)");
    }

    /// <summary>
    /// M5x slice 1 (D149): <c>explode</c> is tg's admin "Drop Bomb" with a custom bomb (modules/admin/admin_verbs.dm:263-279):
    /// <c>explosion(epicenter, devastation_range, heavy_impact_range, light_impact_range, flash_range, adminlog = TRUE,
    /// ignorecap = TRUE, explosion_cause = user.mob)</c> at <paramref name="epicenter"/>, with a flame range as well (tg's
    /// default, the light range, when none is given; the flash range's default is the devastation range). The blast is
    /// processed by SSexplosions at its next fire (the next tick); the reply names the turfs the propagation queued.
    /// </summary>
    public static string Explode(turf epicenter, float devastation, float heavy, float light, float? flash = null, float? flame = null, mob cause = null)
    {
        ArgumentNullException.ThrowIfNull(epicenter);
        string where = Inv($"({Dm.str(epicenter.x)},{Dm.str(epicenter.y)},{Dm.str(epicenter.z)})");
        if (SSexplosions == null)
            return "explode: the explosions subsystem is not running";
        float low = SSexplosions.lowturf.len, med = SSexplosions.medturf.len, high = SSexplosions.highturf.len;
        Waiting.Detach(explosion(epicenter, devastation_range: devastation, heavy_impact_range: heavy, light_impact_range: light,
            flame_range: flame, flash_range: flash, adminlog: Dm.TRUE, ignorecap: Dm.TRUE, explosion_cause: cause));
        return Inv($"explode: ({Dm.str(devastation)}, {Dm.str(heavy)}, {Dm.str(light)}) at {where}: {SSexplosions.highturf.len - high} devastated, {SSexplosions.medturf.len - med} heavy and {SSexplosions.lowturf.len - low} light turfs queued");
    }

    /// <summary>
    /// M7 EMP slice: <c>emp</c> is tg's admin "EM Pulse" verb (modules/admin/verbs/adminfun.dm:29-42): <c>empulse(originator,
    /// heavy, light)</c> at <paramref name="epicenter"/>, only when either range is not 0 (the verb's own check, :38). The pulse
    /// makes the EMP effect at the epicenter and calls <c>emp_act</c> on every atom within the light range (heavy within the heavy
    /// range, a coin toss on its edge), as tg's does.
    /// </summary>
    public static string Emp(turf epicenter, float heavy, float light)
    {
        ArgumentNullException.ThrowIfNull(epicenter);
        string where = Inv($"({Dm.str(epicenter.x)},{Dm.str(epicenter.y)},{Dm.str(epicenter.z)})");
        if (heavy == 0f && light == 0f)
            return Inv($"emp: both ranges are 0, nothing at {where}");
        Waiting.Detach(empulse(epicenter, heavy, light));
        return Inv($"emp: heavy {Dm.str(heavy)}, light {Dm.str(light)} at {where}");
    }

    /// <summary>
    /// The M7 zaps slice: <c>zap X Y [Z] POWER</c> is tg's <c>tesla_zap</c> (modules/power/tesla/energy_ball.dm:204) from
    /// <paramref name="source"/> with tg's default range (3) and cutoff (400 kJ) and the flags <c>ZAP_DEFAULT_FLAGS |
    /// ZAP_GENERATES_POWER</c>: the mob damage, stun and object damage of tg's default bolt, which a tesla coil also turns into
    /// power as the supermatter's bolts do (coil.dm:94; tg has no admin verb for it). It reports the beams the chain drew.
    /// </summary>
    public static string Zap(turf source, float power)
    {
        ArgumentNullException.ThrowIfNull(source);
        string where = Inv($"({Dm.str(source.x)},{Dm.str(source.y)},{Dm.str(source.z)})");
        if (power < 4e5f)
            return Inv($"zap: {Dm.str(power)} J is below tg's cutoff of 400000 J, nothing at {where}");
        long mark = Dm.each<datum_beam>().Select(b => b.dm_id).DefaultIfEmpty(0L).Max();
        Waiting.Detach(tesla_zap(source, 3, power, zap_flags: Dm.bitor(ZAP_DEFAULT_FLAGS, ZAP_GENERATES_POWER)));
        List<datum_beam> beams = Dm.each<datum_beam>().Where(b => b.dm_id > mark && b.target != null).ToList();
        string struck = string.Join(", ", beams.Select(b => Dm.str(b.target.name)));
        return beams.Count == 0
            ? Inv($"zap: {Dm.str(power)} J at {where}, nothing in range")
            : Inv($"zap: {Dm.str(power)} J at {where}, {beams.Count} bolt{(beams.Count == 1 ? "" : "s")}: {struck}");
    }

    /// <summary>
    /// The M7 supermatter slice: <c>smcrystal X Y [Z] status|power|damage [N]</c> on the supermatter crystal at
    /// <paramref name="spot"/>: <c>power</c> sets its internal energy in MeV (logging its first activation, as everything that
    /// powers it must, supermatter.dm:735), <c>damage</c> its damage (100 is the explosion point: the countdown starts at its next
    /// cycle), as tg's admins edit them in view variables; <c>status</c> reports its energy, integrity, status, strategy and gas.
    /// </summary>
    public static string SmCrystal(turf spot, string mode, float amount)
    {
        ArgumentNullException.ThrowIfNull(spot);
        string where = Inv($"({Dm.str(spot.x)},{Dm.str(spot.y)},{Dm.str(spot.z)})");
        obj_machinery_power_supermatter_crystal sm = Dm.locate<obj_machinery_power_supermatter_crystal>(spot);
        if (sm == null)
            return Inv($"smcrystal: no supermatter crystal at {where}");
        if (mode == "power")
        {
            sm.log_activation("a dev command");
            sm.internal_energy = amount;
        }
        else if (mode == "damage")
            sm.damage = amount;
        datum_gas_mixture gas = sm.absorbed_gasmix;
        string mix = gas == null ? "none" : Inv($"{Dm.str(gas.total_moles())} mol at {Dm.str(gas.temperature)} K");
        return Inv($"smcrystal: {Dm.str(sm.name)} at {where}: {Dm.str(sm.internal_energy)} MeV, integrity {Dm.str(Dm.round(Dm.num(sm.get_integrity_percent()), 0.01f))}%, status {Dm.str(sm.get_status())}, {Dm.str(sm.delamination_strategy?.name)}, heat limit {Dm.str(sm.temp_limit)} K, absorbed gas {mix}");
    }

    /// <summary>
    /// The M7 delamination slice (G6): <c>smdelam X Y [Z] [cascade|singularity|tesla|explosive]</c> delaminates the supermatter
    /// crystal at <paramref name="spot"/> at once, as tg's admins do: a named strategy is forced first with
    /// <c>set_delam(SM_DELAM_PRIO_IN_GAME, ...)</c> (the admin secret's cascade, secrets.dm:685), then <c>force_delam()</c>, the bluespace
    /// artillery's handler (supermatter.dm:555), runs the strategy's <c>delaminate</c>. <paramref name="strategy"/> null keeps the
    /// crystal's own. A crystal already counting down is left to its countdown.
    /// </summary>
    public static string SmDelam(turf spot, string strategy)
    {
        ArgumentNullException.ThrowIfNull(spot);
        string where = Inv($"({Dm.str(spot.x)},{Dm.str(spot.y)},{Dm.str(spot.z)})");
        obj_machinery_power_supermatter_crystal sm = Dm.locate<obj_machinery_power_supermatter_crystal>(spot);
        if (sm == null)
            return Inv($"smdelam: no supermatter crystal at {where}");
        // tg's count_down has no QDELETED check after its sleeps (supermatter.dm:604-637): forced during a countdown, the
        // delamination would run again on the deleted crystal when the countdown ends, whose null turf effect_demoralize reads
        if (Dm.truthy(sm.final_countdown))
            return Inv($"smdelam: {Dm.str(sm.name)} at {where} is already counting down to its delamination");
        Type path = strategy switch
        {
            null => null,
            "cascade" => typeof(datum_sm_delam_cascade),
            "singularity" => typeof(datum_sm_delam_singularity),
            "tesla" => typeof(datum_sm_delam_tesla),
            "explosive" => typeof(datum_sm_delam_explosive),
            _ => throw new ArgumentException(Inv($"unknown delamination strategy '{strategy}'"), nameof(strategy)),
        };
        if (path != null)
            sm.set_delam(SM_DELAM_PRIO_IN_GAME, path);
        string name = Dm.str(sm.delamination_strategy?.GetType().Name.Replace("datum_sm_delam_", ""));
        sm.force_delam();
        return Inv($"smdelam: {Dm.str(sm.name)} at {where} delaminates ({name})");
    }

    /// <summary>
    /// M7 slice 2 (D162): <c>recharge X Y [Z]</c> / <c>recharge all</c> fills the charge of the power storage units (the SMES) at a
    /// turf, or of every one, for long playtests: tg's own <c>adjust_charge</c> of the unit's total capacity (smes.dm:177), which
    /// fills each megacell in turn, and the unit's overlays (smes.dm:140) follow. <paramref name="spot"/> null is every unit.
    /// </summary>
    public static string Recharge(turf spot)
    {
        string where = spot == null ? "everywhere" : Inv($"at ({Dm.str(spot.x)},{Dm.str(spot.y)},{Dm.str(spot.z)})");
        int units = 0;
        float stored = 0;
        foreach (obj_machinery_power_smes smes in Dm.each_cast<obj_machinery_power_smes>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_power_smes))))
        {
            if (spot != null && get_turf(smes) != spot)
                continue;
            Waiting.NoSleep(smes.adjust_charge(smes.total_capacity));
            smes.update_appearance(UPDATE_OVERLAYS);
            units++;
            stored += Dm.num(smes.total_charge());
        }
        if (units == 0)
            return Inv($"recharge: no power storage unit {where}");
        return Inv($"recharge: {units} power storage unit{(units == 1 ? "" : "s")} {where} full, {Dm.str(stored / 1000000f)} MJ stored");
    }

    /// <summary>
    /// M7 radiation slice: <c>irradiate</c> calls tg's <c>radiation_pulse(epicenter, max_range, threshold)</c>
    /// (__HELPERS/radiation.dm:25) with tg's default chance (DEFAULT_RADIATION_CHANCE) and no minimum exposure time. SSradiation
    /// processes the pulse at its next fire (every 0.5 s): every human and item out to <paramref name="maxRange"/> tiles whose line to
    /// the epicenter keeps its insulation above <paramref name="threshold"/> may be irradiated, by tg's chance falling off with the
    /// distance and the insulation.
    /// </summary>
    public static string Irradiate(turf epicenter, float maxRange, float threshold)
    {
        ArgumentNullException.ThrowIfNull(epicenter);
        string where = Inv($"({Dm.str(epicenter.x)},{Dm.str(epicenter.y)},{Dm.str(epicenter.z)})");
        if (!Dm.truthy(radiation_pulse(epicenter, maxRange, threshold)))
            return Inv($"irradiate: the radiation subsystem is not running, nothing at {where}");
        return Inv($"irradiate: a pulse of range {Dm.str(maxRange)}, threshold {Dm.str(threshold)} at {where} ({SSradiation.processing.len} pulses queued)");
    }

    /// <summary>
    /// M5x slice 5 (D149, D150): <c>singularity</c> makes tg's gravitational singularity at <paramref name="target"/> with
    /// <paramref name="energy"/> as its starting energy (<c>new /obj/singularity(target, energy)</c>: its Initialize's
    /// starting_energy, singularity.dm:58). The owner's default is 600 (stage 3: it roams and eats); it grows as tg's does while it
    /// eats, without a cap.
    /// </summary>
    /// <summary>
    /// Gaps E2a, tg's admin verb Run Weather (<c>SSweather.run_weather</c>): the weather whose type ends in <paramref name="name"/> (or whose
    /// name, spaces as underscores, is it) on level <paramref name="z"/>; <c>stop</c> winds down every weather running there.
    /// </summary>
    public static string Weather(string name, int z)
    {
        if (SSweather == null)
            return "weather: no weather subsystem";
        if (name == "stop")
        {
            int stopped = 0;
            foreach (datum_weather running in Dm.each<datum_weather>(SSweather.processing.Copy()))
            {
                if (Dm.@in(z, running.impacted_z_levels) && Dm.truthy(running.wind_down()))
                    stopped++;
            }
            return Inv($"weather: {stopped} weather(s) winding down on level {z}");
        }
        foreach (Type type in Dm.each<Type>(valid_subtypesof(typeof(datum_weather))))
        {
            string path = type.GetCustomAttributes(typeof(DmPathAttribute), false) is [DmPathAttribute p, ..] ? p.Path : "";
            string leaf = path[(path.LastIndexOf('/') + 1)..];
            string title = (Dm.initial(type, (datum_weather w) => w.name) ?? "").Replace(' ', '_');
            if (leaf != name && title != name)
                continue;
            datum_weather made = SSweather.run_weather(type, z);
            return made == null ? Inv($"weather: {name} did not start") : Inv($"weather: {Dm.str(made.name)} on level {z}, {Dm.str(made.impacted_areas.len)} areas, stage {Dm.str(made.stage)}");
        }
        return Inv($"weather: no weather named {name}");
    }

    public static string Singularity(turf target, float energy)
    {
        ArgumentNullException.ThrowIfNull(target);
        string where = Inv($"({Dm.str(target.x)},{Dm.str(target.y)},{Dm.str(target.z)})");
        var made = Dm.@new(typeof(obj_singularity), target, energy) as obj_singularity;
        if (made == null || QDELETED(made))
            return Inv($"singularity: none at {where}");
        Dm.combine(ref made.flags_1, ADMIN_SPAWNED_1);
        // tg's current_size is the stage's picture size in tiles (STAGE_THREE is 5, gravity.dm): the stage number is (size + 1) / 2
        float size = Dm.num(made.current_size);
        return Inv($"singularity: at {where} with {Dm.str(made.energy)} energy, stage {(size + 1) / 2} ({size} x {size} tiles)");
    }

    /// <summary>
    /// M4b slice 1: <c>patch</c> closes a breach. A turf that <c>breach</c> opened gets back what it was (the owner, 2026-10-15):
    /// tg's <c>ChangeTurf</c> to its old turf type, and the air-tight objects the breach deleted (a window, a door) are made
    /// again with their directions. Any other space turf becomes plating through tg's <c>ChangeTurf(/turf/open/floor/plating)</c>.
    /// The new tile's air is averaged with its neighbours' (tg's <c>Assimilate_Air</c>) and the room fills again from around it.
    /// </summary>
    public static string Patch(turf target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target is not turf_open_space)
            return Inv($"patch: {Dm.str(target.name)} at ({Dm.str(target.x)},{Dm.str(target.y)},{Dm.str(target.z)}) is not space");
        string where = Inv($"({Dm.str(target.x)},{Dm.str(target.y)},{Dm.str(target.z)})"); // before the replaced turf leaves the map
        var key = ((int)target.x, (int)target.y, (int)target.z);
        Dictionary<(int, int, int), (Type Turf, List<(Type Type, float Dir)> Objects)> breaches = Breaches.Of(World.CurrentOrNull);
        if (!breaches.Remove(key, out var was))
        {
            Waiting.Detach(target.ChangeTurf(typeof(turf_open_floor_plating)));
            return Inv($"patch: {where} is plating now");
        }
        Waiting.Detach(target.ChangeTurf(was.Turf));
        if (locate(key.Item1, key.Item2, key.Item3) is not turf back)
            return Inv($"patch: {where} could not be restored");
        foreach ((Type type, float dir) in was.Objects)
        {
            var made = (atom)Dm.@new(type, back);
            if (!QDELETED(made) && dir != 0)
                Waiting.Detach(made.setDir(dir));
        }
        return Inv($"patch: {where} is {Dm.str(back.name)} again ({was.Objects.Count} object(s) put back)");
    }

    // What each breach removed, by tile (per world: World.GetExtension, no static state), for patch to put back.
    private static class Breaches
    {
        private sealed class Holder
        {
            internal readonly Dictionary<(int, int, int), (Type Turf, List<(Type Type, float Dir)> Objects)> Tiles = new();
        }

        internal static Dictionary<(int, int, int), (Type Turf, List<(Type Type, float Dir)> Objects)> Of(World world)
            => world.GetExtension(static _ => new Holder()).Tiles;
    }

    /// <summary>
    /// M4b slice 2 (the canisters' tgui window is M9): <c>valve</c> toggles the valve of the canister on
    /// <paramref name="target"/> through tg's <c>toggle_valve</c>, as the window's valve button does: an open valve releases
    /// the canister's gas into the turf (or a held tank) at its release pressure (tg's <c>release_gas_to</c>).
    /// </summary>
    public static string Valve(turf target, mob user)
    {
        ArgumentNullException.ThrowIfNull(target);
        string where = Inv($"({Dm.str(target.x)},{Dm.str(target.y)},{Dm.str(target.z)})");
        obj_machinery_portable_atmospherics_canister can = Dm.locate<obj_machinery_portable_atmospherics_canister>(target);
        if (can == null)
            return "valve: no canister at " + where;
        can.toggle_valve(user);
        return Inv($"valve: {Dm.str(can.name)} at {where} {(Dm.truthy(can.valve_open) ? "opened" : "closed")} ({Dm.num(can.air_contents.return_pressure()):0} kPa inside, releases at {can.release_pressure:0} kPa)");
    }

    /// <summary>
    /// M4b slice 2 (the air alarms' tgui window is M9): <c>airmode</c> selects a mode on the air alarm of
    /// <paramref name="who"/>'s area through tg's <c>select_mode</c>, as the window's mode list does: the mode's
    /// <c>apply</c> sets the area's air vents and scrubbers (siphon: the vents off, the scrubbers siphoning; filtering: tg's
    /// defaults). <paramref name="mode"/> is one of the short names of the dev command line's <c>AirModes</c>.
    /// </summary>
    public static string AirMode(mob_living who, string mode)
    {
        ArgumentNullException.ThrowIfNull(who);
        Type path = mode switch
        {
            "filtering" => typeof(datum_air_alarm_mode_filtering),
            "contaminated" => typeof(datum_air_alarm_mode_contaminated),
            "draught" => typeof(datum_air_alarm_mode_draught),
            "refill" => typeof(datum_air_alarm_mode_refill),
            "cycle" => typeof(datum_air_alarm_mode_cycle),
            "siphon" => typeof(datum_air_alarm_mode_siphon),
            "panic" => typeof(datum_air_alarm_mode_panic_siphon),
            "off" => typeof(datum_air_alarm_mode_off),
            _ => null,
        };
        if (path == null)
            return "airmode: unknown mode " + mode;
        area place = get_area(who) as area;
        obj_machinery_airalarm alarm = null;
        foreach (obj_machinery_airalarm a in Dm.each_cast<obj_machinery_airalarm>(GLOB.air_alarms))
        {
            if (a.my_area == place)
            {
                alarm = a;
                break;
            }
        }
        if (alarm == null)
            return "airmode: no air alarm in " + Dm.str(place?.name);
        alarm.select_mode(who, path);
        if (alarm.selected_mode?.GetType() != path)
            return Inv($"airmode: {Dm.str(alarm.name)} refused {mode}");
        return Inv($"airmode: {Dm.str(alarm.name)} set to {Dm.str(alarm.selected_mode.name)} ({alarm.my_area.air_vents?.len ?? 0} vents, {alarm.my_area.air_scrubbers?.len ?? 0} scrubbers)");
    }

    /// <summary>The turf <paramref name="who"/> faces (<c>get_step(who, who.dir)</c>), the turf of a <c>breach WHO</c> or <c>patch WHO</c>.</summary>
    public static turf FacedTurf(mob who)
    {
        ArgumentNullException.ThrowIfNull(who);
        return get_step(who, who.dir) as turf;
    }

    // tg's damage type texts (code/__DEFINES/combat.dm:7-31).
    private static string TypeText(DevDamageType type) => type switch
    {
        DevDamageType.Brute => BRUTE,
        DevDamageType.Burn => BURN,
        DevDamageType.Tox => TOX,
        DevDamageType.Oxy => OXY,
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    /// <summary>One row of <see cref="MobInfo"/>: a section, a name and a value.</summary>
    public readonly record struct InfoRow(string Section, string Key, string Value);

    /// <summary>
    /// <c>mobinfo</c> (the owner, 2026-10-13: "all the health stats and characteristics of the selected player"; D140): the
    /// mob's numbers as tg's own vars and procs give them: the stat and health, the six damage types, the blood, the body,
    /// the bodyparts and organs of a carbon, the status effects with their time left, and the traits. A row that cannot be
    /// read (a proc not translated yet) says so instead of stopping the rest.
    /// </summary>
    public static List<InfoRow> MobInfo(mob_living target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var rows = new List<InfoRow>();
        void Row(string section, string key, Func<string> value)
        {
            string text;
            try
            {
                text = value();
            }
            catch (Exception e)
            {
                text = "? (" + e.GetType().Name + ")";
            }
            rows.Add(new InfoRow(section, key, text));
        }
        string N(object v) => Dm.num(v).ToString("0.##", CultureInfo.InvariantCulture);

        Row("Mob", "name", () => NameOf(target));
        Row("Mob", "id", () => "#" + target.dm_id.ToString(CultureInfo.InvariantCulture));
        Row("Mob", "type", () => DmTypes.PathOf(target.GetType()));
        Row("Mob", "ckey", () => Dm.str(target.ckey));
        Row("Mob", "at", () => get_turf(target) is turf t ? Inv($"({Dm.str(t.x)}, {Dm.str(t.y)}, {Dm.str(t.z)})") : "nullspace");
        Row("Mob", "stat", () => StatName(Dm.num(target.stat)));
        Row("Mob", "health", () => N(target.health) + " / " + N(target.maxHealth) + Inv($" (crit at {HEALTH_THRESHOLD_CRIT}, dead at {HEALTH_THRESHOLD_DEAD})"));
        Row("Mob", "body position", () => Dm.num(target.body_position) == LYING_DOWN ? "lying down" : "standing up");
        Row("Mob", "move intent", () => Dm.str(target.move_intent));
        Row("Mob", "combat mode", () => Dm.truthy(target.combat_mode) ? "on" : "off");
        // the dev switches the panel shows as check boxes (godmode, fly and noclip as the dev commands set them)
        Row("Dev", "godmode", () => HAS_TRAIT_FROM(target, TRAIT_GODMODE, ADMIN_TRAIT) ? "on" : "off");
        Row("Dev", "fly", () => HAS_TRAIT_FROM(target, TRAIT_MOVE_FLYING, ADMIN_TRAIT) ? "on" : "off");
        Row("Dev", "noclip", () => Dm.eq(target.incorporeal_move, INCORPOREAL_MOVE_BASIC) ? "on" : "off");
        Row("Dev", "esp", () => HAS_TRAIT(target, TRAIT_ADMIN_ESP) ? "on" : "off");
        Row("Dev", "invuln", () => HAS_TRAIT(target, TRAIT_DEV_INVULNERABLE) ? "on" : "off");
        Row("Dev", "invisimin", () => HAS_TRAIT(target, TRAIT_INVISIMIN) ? "on" : "off");

        Row("Damage", "brute", () => N(target.get_brute_loss()));
        Row("Damage", "burn", () => N(target.get_fire_loss()));
        Row("Damage", "toxin", () => N(target.get_tox_loss()));
        Row("Damage", "oxygen", () => N(target.get_oxy_loss()));
        Row("Damage", "stamina", () => N(target.get_stamina_loss()));
        Row("Damage", "organs", () => N(target.get_organ_loss(null)));

        Row("Blood", "volume", () => N(target.blood_volume) + Inv($" (normal {BLOOD_VOLUME_NORMAL}, safe {BLOOD_VOLUME_SAFE}, bad {BLOOD_VOLUME_BAD})"));
        Row("Blood", "bleed rate", () => N(target.get_bleed_rate()));

        Row("Body", "temperature", () => N(target.bodytemperature) + Inv($" K (normal {BODYTEMP_NORMAL})"));
        Row("Body", "nutrition", () => N(target.nutrition));
        Row("Body", "fire stacks", () => N(target.fire_stacks) + (Dm.truthy(target.on_fire) ? ", on fire" : ""));

        if (target is mob_living_carbon carbon)
        {
            foreach (obj_item_bodypart part in Dm.each<obj_item_bodypart>(carbon.bodyparts))
            {
                obj_item_bodypart limb = part;
                Row("Bodyparts", Dm.str(limb.body_zone), () => Inv($"brute {N(limb.brute_dam)}, burn {N(limb.burn_dam)} of {N(limb.max_damage)}")
                    + (Dm.truthy(limb.bodypart_disabled) ? ", disabled" : "")
                    + (limb.wounds is DmList w && w.len > 0 ? Inv($", {w.len} wound(s)") : ""));
            }
            foreach (obj_item_organ organ in Dm.each<obj_item_organ>(carbon.organs))
            {
                obj_item_organ o = organ;
                Row("Organs", NameOf(o), () => Inv($"{N(o.damage)} / {N(o.maxHealth)}") + ((((int)Dm.num(o.organ_flags)) & ORGAN_FAILING) != 0 ? ", failing" : ""));
            }
        }

        foreach (datum_status_effect effect in Dm.each<datum_status_effect>(target.status_effects))
        {
            datum_status_effect e = effect;
            // the key is the type (the panel's remove button names it), the value the id and the time left
            Row("Status effects", DmTypes.PathOf(e.GetType()), () => EffectText(e));
        }

        Row("Movement", "slowdown", () => N(target.cached_multiplicative_slowdown));
        Row("Movement", "modifiers", () =>
        {
            var ids = new List<string>();
            foreach (object id in Dm.each(target.movespeed_modification))
                ids.Add(Dm.str(id is Type t ? DmTypes.PathOf(t) : id));
            return ids.Count == 0 ? "none" : string.Join(", ", ids);
        });

        Row("Traits", "traits", () =>
        {
            var names = new List<string>();
            if (target._status_traits is DmList traits)
                foreach (object key in Dm.each(traits))
                    names.Add(Dm.str(key));
            names.Sort(StringComparer.Ordinal);
            return names.Count == 0 ? "none" : string.Join(", ", names);
        });

        // the add field's list: the status effect types live in the port (the paths below /datum/status_effect/)
        Row("Effect types", "live", () => string.Join(" ", LiveStatusEffects().Select(ShortEffectPath)));
        return rows;
    }

    // ---- the status effects (the owner, 2026-10-15: see, add and remove a player's status effects in the dev menu) ----

    private const string EffectRoot = "/datum/status_effect/";

    /// <summary>
    /// The status effect types a dev command may apply: every <c>/datum/status_effect</c> subtype whose class is live (not
    /// <c>[DmInert]</c>: all its tg procs are declared) and whose <c>id</c> is not tg's <c>STATUS_EFFECT_ID_ABSTRACT</c> (an
    /// abstract parent, tg's status_effect_validity unit test skips them), but for tg's unit test fixture
    /// <c>/datum/status_effect/unit_test_tick_counter</c> (code/modules/unit_tests/status_effect_validity.dm:86, compiled under
    /// <c>UNIT_TESTS</c> only; the port has it in its tests). In the type tree's order.
    /// </summary>
    public static List<Type> LiveStatusEffects()
    {
        var live = new List<Type>();
        foreach (Type type in DmTypes.Subtypes(typeof(datum_status_effect), includeRoot: false))
        {
            if (InertSeam.IsInertType(type) || DmTypes.PathOf(type) == "/datum/status_effect/unit_test_tick_counter")
                continue;
            if (Dm.eq(Dm.initial(type, (datum_status_effect p) => p.id), STATUS_EFFECT_ID_ABSTRACT))
                continue;
            live.Add(type);
        }
        return live;
    }

    /// <summary>A status effect type's path below <c>/datum/status_effect/</c> (<c>incapacitating/stun</c>).</summary>
    public static string ShortEffectPath(Type type)
    {
        string path = DmTypes.PathOf(type);
        return path.StartsWith(EffectRoot, StringComparison.Ordinal) ? path.Substring(EffectRoot.Length) : path;
    }

    /// <summary>
    /// The live status effect type that <paramref name="name"/> names: its full path, its path below
    /// <c>/datum/status_effect/</c>, or its <c>id</c> when one live type alone has it. Null with <paramref name="problem"/>
    /// set otherwise.
    /// </summary>
    public static Type FindStatusEffect(string name, out string problem)
    {
        problem = null;
        string trimmed = (name ?? string.Empty).Trim();
        List<Type> live = LiveStatusEffects();
        foreach (Type type in live)
        {
            if (DmTypes.PathOf(type) == trimmed || ShortEffectPath(type) == trimmed.TrimStart('/'))
                return type;
        }
        List<Type> byId = live.Where(t => Dm.str(Dm.initial(t, (datum_status_effect p) => p.id)) == trimmed).ToList();
        if (byId.Count == 1)
            return byId[0];
        problem = byId.Count > 1
            ? Inv($"effect: the id {trimmed} is shared by {string.Join(", ", byId.Select(ShortEffectPath))}; name the type")
            : Inv($"effect: {trimmed} is no status effect the port has live (effect types lists them)");
        return null;
    }

    /// <summary><c>effects WHO</c>: the target's status effects (tg's <c>status_effects</c>), each with its time left.</summary>
    public static string Effects(mob_living target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var lines = new List<string>();
        foreach (datum_status_effect e in Dm.each<datum_status_effect>(target.status_effects))
            lines.Add("  " + ShortEffectPath(e.GetType()) + "  " + EffectText(e));
        return lines.Count == 0
            ? Inv($"{Label(target)} has no status effects")
            : Inv($"{Label(target)}: {lines.Count} status effect(s)\n") + string.Join("\n", lines);
    }

    /// <summary>
    /// M8 slice A (D195): the reagent type that <paramref name="name"/> names: a valid <c>/datum/reagent</c> path, or a name as
    /// tg's <c>get_chem_id</c> matches it (ckeys: <c>rat_spit</c> and <c>ratspit</c> are "Rat Spit"). Null with
    /// <paramref name="problem"/> set otherwise.
    /// </summary>
    public static Type FindReagent(string name, out string problem)
    {
        problem = null;
        string trimmed = (name ?? string.Empty).Trim();
        Type found = trimmed.StartsWith('/') ? DmTypes.FromPath(trimmed) : get_chem_id(trimmed.Replace('_', ' ')) as Type;
        if (found != null && Dm.@in(found, valid_subtypesof(typeof(datum_reagent))))
            return found;
        problem = Inv($"reagent: {trimmed} is no reagent (a /datum/reagent path or a reagent's name)");
        return null;
    }

    /// <summary>
    /// M8 slice A (D195): <c>reagent WHO TYPE N</c>, tg's admin "Add reagent" of view variables
    /// (<c>reagents.add_reagent(chosen_id, amount)</c>): into the target's own holder, its blood.
    /// </summary>
    public static string AddReagent(mob_living target, Type reagent, float units)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(reagent);
        if (target.reagents is not datum_reagents holder)
            return Inv($"{Label(target)} has no reagents holder");
        object added = Waiting.Detach(holder.add_reagent(reagent, units));
        return Inv($"{Label(target)}: {Dm.num(added):0.##}u of {Dm.str(Dm.initial(reagent, (datum_reagent p) => p.name))} added ({holder.total_volume:0.##}u in the blood)");
    }

    /// <summary>M8 slice E (D195): <c>heart WHO stop|start</c>: tg's <c>set_heartattack(stop)</c> of a carbon.</summary>
    public static string Heart(mob_living target, bool stop)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target is not mob_living_carbon carbon)
            return Inv($"{Label(target)} is not a carbon: it has no heart to stop");
        carbon.set_heartattack(stop ? 1 : 0);
        return Inv($"{Label(target)}: set_heartattack({(stop ? "TRUE" : "FALSE")}); cardiac arrest {(Dm.truthy(carbon.undergoing_cardiac_arrest()) ? "yes" : "no")}");
    }

    /// <summary>M8 slice E (D195): <c>breath WHO N</c>: the target's <c>losebreath</c> set to N (tg's admin view variables).</summary>
    public static string Breath(mob_living target, float breaths)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.losebreath = breaths;
        return Inv($"{Label(target)}: losebreath {breaths:0.##}");
    }

    /// <summary>M8 slice G (D195): <c>hallucinate WHO TYPE [SECONDS]</c>: tg's admin "Give Hallucination" (<c>cause_hallucination</c>) of one type, or
    /// (a null <paramref name="typePath"/>) the hallucination status effect for <paramref name="seconds"/>.</summary>
    public static string Hallucinate(mob_living target, string typePath, float seconds)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (typePath == null)
        {
            DevEffect(target, () => { Waiting.Detach(target.adjust_hallucinations(seconds * SECONDS)); return 0; });
            return Inv($"{Label(target)}: hallucinating for {seconds:0.##} s more");
        }
        Type found = null;
        foreach (Type type in Dm.each_cast<Type>(Dm.typesof(typeof(datum_hallucination))))
            if (DmTypes.PathOf(type) == typePath)
                found = type;
        if (found == null)
            return Inv($"hallucinate: {typePath} is no hallucination type");
        if (Dm.initial(found, (datum_hallucination p) => p.abstract_hallucination_parent) == found)
            return Inv($"hallucinate: {typePath} is an abstract group; name one of its subtypes");
        object made = float.IsNaN(seconds)
            ? Waiting.Detach(target.cause_hallucination(found, "admin forced"))
            : Waiting.Detach(target.cause_hallucination(found, "admin forced", Dm.named("duration", seconds * SECONDS)));
        return made == null
            ? Inv($"{Label(target)}: the hallucination {typePath} could not run (it may be invalid with this mob, or has no effect)")
            : Inv($"{Label(target)}: hallucination {typePath}");
    }

    /// <summary>M8 slice A (D195): <c>reagents WHO</c>: the reagents in the target's blood and stomach.</summary>
    public static string Reagents(mob_living target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var lines = new List<string>();
        void List(string where, datum_reagents holder)
        {
            foreach (datum_reagent r in Dm.each_cast<datum_reagent>(holder?.reagent_list))
            {
                string state = (Dm.truthy(r.overdosed) ? " overdosed" : string.Empty) + (Dm.truthy(r.metabolizing) ? " metabolizing" : string.Empty);
                lines.Add(Inv($"  {where}: {Dm.str(r.name)} {Dm.num(r.volume):0.###}u{state}"));
            }
        }
        List("blood", target.reagents as datum_reagents);
        if (target is mob_living_carbon carbon && carbon.get_organ_slot(ORGAN_SLOT_STOMACH) is obj_item_organ_stomach stomach)
            List("stomach", stomach.reagents as datum_reagents);
        return lines.Count == 0
            ? Inv($"{Label(target)} has no reagents")
            : Inv($"{Label(target)}: {lines.Count} reagent(s)\n") + string.Join("\n", lines);
    }

    /// <summary><c>effect types</c>: the status effect types live in the port (<see cref="LiveStatusEffects"/>).</summary>
    public static string EffectTypes()
        => "live status effects: " + string.Join(" ", LiveStatusEffects().Select(ShortEffectPath));

    /// <summary>
    /// <c>effect WHO add TYPE [N]</c>: tg's <c>apply_status_effect(TYPE, ...)</c>, with tg's own creation arguments: a grouped
    /// effect gets the admin source (<c>ADMIN_TRAIT</c>; tg's grouped effects need a source, grouped_effect.dm:15); an effect
    /// whose <c>on_creation</c> takes a duration (<c>duration</c>, <c>set_duration</c>: the stun family, stagger, daze,
    /// temporary blindness) gets <paramref name="number"/> seconds as it (tg's <c>Stun(amount)</c> passes its amount so);
    /// one that takes stacks (<c>new_stacks</c>: fire and wet stacks) gets <paramref name="number"/> stacks. Without a number
    /// the effect takes tg's defaults (an incapacitating effect without a duration lasts until removed). The effect's own
    /// <c>on_apply</c> may refuse (a dead mob, an immunity trait), and a unique effect is not applied twice.
    /// </summary>
    public static string AddEffect(mob_living target, Type type, float? number = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(type);
        var args = new List<object>();
        string firstParam = CreationParam(type);
        if (typeof(datum_status_effect_grouped).IsAssignableFrom(type))
            args.Add(ADMIN_TRAIT);
        else if (number.HasValue && firstParam is "duration" or "set_duration")
            args.Add(number.Value * SECONDS);
        else if (number.HasValue && firstParam == "new_stacks")
            args.Add(number.Value);
        string ignored = number.HasValue && args.Count == 0 ? Inv($" (it takes no duration or stacks: {number.Value:0.##} ignored)") : string.Empty;
        // tg: code/datums/status_effects/_status_effect_helpers.dm:13 apply_status_effect(new_effect, ...)
        datum_status_effect made = DevEffect(target, () => Waiting.NoSleep(target.apply_status_effect(type, args.ToArray()))) as datum_status_effect;
        string path = ShortEffectPath(type);
        if (made == null && args.Count == 1 && Dm.eq(args[0], ADMIN_TRAIT) && target.has_status_effect(type) is datum_status_effect_grouped joined && Dm.@in(ADMIN_TRAIT, joined.sources))
            return Inv($"{Label(target)}: {path} applied ({EffectText(joined)})"); // a grouped effect on already: tg added the source to it (grouped_effect.dm:21)
        if (made == null)
        {
            datum_status_effect had = target.has_status_effect(type) as datum_status_effect;
            return had != null
                ? Inv($"{Label(target)}: {path} is on already ({EffectText(had)}){ignored}")
                : Inv($"{Label(target)}: {path} was not applied (the effect refused this mob){ignored}");
        }
        return Inv($"{Label(target)}: {path} applied ({EffectText(made)}){ignored}");
    }

    /// <summary>
    /// <c>effect WHO remove TYPE</c>: tg's <c>remove_status_effect(TYPE)</c>; a grouped effect is removed with each of its
    /// sources (tg's grouped <c>before_remove(source)</c> keeps the effect while a source is left, grouped_effect.dm:48).
    /// </summary>
    public static string RemoveEffect(mob_living target, Type type)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(type);
        bool removed = false;
        if (target.has_status_effect(type) is datum_status_effect_grouped grouped)
        {
            foreach (object source in Dm.each(grouped.sources.Copy()))
                removed |= Dm.truthy(target.remove_status_effect(type, source)); // tg: _status_effect_helpers.dm:55
        }
        else
        {
            removed = Dm.truthy(target.remove_status_effect(type)); // tg: _status_effect_helpers.dm:55
        }
        return Inv($"{Label(target)}: {ShortEffectPath(type)} {(removed ? "removed" : "was not on")}");
    }

    // The DM name of on_creation's first argument after new_owner at the type (its nearest declared on_creation's DmParams).
    private static string CreationParam(Type type)
    {
        for (Type t = type; t != null; t = t.BaseType)
        {
            System.Reflection.MethodInfo declared = t.GetMethod(nameof(datum_status_effect.on_creation), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
            if (declared?.GetCustomAttributes(typeof(DmParamsAttribute), false).FirstOrDefault() is DmParamsAttribute p)
                return p.Names.Count > 1 ? p.Names[1] : null;
        }
        return null;
    }

    // An effect's id and time left (tg's duration counts down in process), and a grouped effect's sources.
    private static string EffectText(datum_status_effect e)
    {
        string time = Dm.num(e.duration) == STATUS_EFFECT_PERMANENT
            ? "permanent"
            : (Math.Max(0f, Dm.num(e.duration)) / 10f).ToString("0.#", CultureInfo.InvariantCulture) + " s left";
        string sources = e is datum_status_effect_grouped g && g.sources is DmList list && list.len > 0
            ? " from " + string.Join(", ", Dm.each(list).Select(x => Dm.str(x)))
            : string.Empty;
        return Dm.str(e.id) + ": " + time + sources;
    }

    private static string StatName(float stat) => stat switch
    {
        STABLE => "conscious",
        SOFT_CRIT => "soft crit",
        HARD_CRIT => "hard crit",
        DEAD => "dead",
        _ => Dm.str(stat),
    };

    private static string Label(mob_living m) => Dm.str(m.name);

    // DM's text of an atom inside a sentence: its name without the \proper and \improper markers, "the " before an improper one.
    private static string NameOf(atom a) => Dm.text(" []", a).TrimStart();

    private static string Health(mob_living m) => Inv($"health {Dm.str(m.health)}/{Dm.str(m.maxHealth)}");

    private static string Inv(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);

    // tg's report is HTML (spans, <br>, a table); the console shows its text.
    private static string PlainText(string html)
    {
        string text = Regex.Replace(html.Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase).Replace("</tr>", "\n", StringComparison.OrdinalIgnoreCase), "<[^>]*>", "");
        return System.Net.WebUtility.HtmlDecode(text).Trim();
    }
}
