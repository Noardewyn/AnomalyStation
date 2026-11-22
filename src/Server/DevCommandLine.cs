using System.Globalization;
using System.Text;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server;

// M2 P6 (scope 2k, D73, Q9). The syntax of the dev commands, parsed on the host before anything is queued: the console types
// them, and a player's dev console sends them as ActionKind.DevCommand when the server's DEV_COMMANDS is on. A line that does
// not parse never becomes a command. The bodies are Anomaly.Core.Tg.DevCommands (tg's own procs).

/// <summary>The dev commands (scope 2k's table).</summary>
public enum DevVerb : byte
{
    Damage,
    Heal,
    FullHeal,
    Kill,
    Revive,
    Knockdown,
    Stun,
    Bleed,
    Blood,
    Gas,
    Analyze,
    Spawn,
    SpeedScale,

    /// <summary>The owner (2026-10-30, the dev menu's Player tab): <c>speed WHO F</c>, one mob's speed times F over the world's <c>speedscale</c>.</summary>
    Speed,
    GodMode,

    /// <summary>The owner (2026-11-21, mech guns): <c>pilot WHO</c>, the target climbs into the mech on or next to its tile (tg's mob_enter and moved_inside, without the climb's wait), or out of the mech it is in.</summary>
    Pilot,

    /// <summary>M4a (D130): <c>give WHO TYPE</c>, tg's admin spawn of an <c>/obj/item</c> type into the target's hands (<c>new TYPE(turf)</c>, then <c>put_in_hands</c>). It replaced M3's <c>access</c> (DV23, retired by M4a P5).</summary>
    Give,
    /// <summary>The owner (2026-10-15): <c>drop WHO SLOT</c>, tg's dropItemToGround of the item in a worn slot.</summary>
    Drop,
    /// <summary>The owner (2026-10-15): <c>gravity WHO on|off</c>, tg's enable/disable of the gravity generator on WHO's level.</summary>
    Gravity,

    /// <summary>The owner's play test (2026-10-09): <c>fly WHO</c> toggles tg's <c>TRAIT_MOVE_FLYING</c> (the <c>FLYING</c> movement type: no gravity needed, no slipping).</summary>
    Fly,

    /// <summary>The owner's play test (2026-10-09): <c>noclip WHO</c> toggles tg's <c>incorporeal_move</c> <c>INCORPOREAL_MOVE_BASIC</c> (through walls, DV29).</summary>
    NoClip,

    /// <summary>The owner (2026-11-02): <c>invuln WHO</c> toggles the dev invulnerability (godmode's traits, no push, no other mob's click or drag on it).</summary>
    Invulnerable,

    /// <summary>The owner (2026-11-02): <c>esp WHO</c>, tg's Toggle Admin ESP: WHO sees the ghosts.</summary>
    Esp,

    /// <summary>The owner (2026-11-20): <c>invisimin WHO</c>, tg's Invisimin: WHO is seen only by ghosts and esp, drawn see-through.</summary>
    Invisimin,

    /// <summary>The owner's play test (2026-10-09): <c>god WHO</c> turns fly, noclip and godmode on together, or off when all three are on.</summary>
    God,

    /// <summary>The owner (2026-11-10): <c>up WHO</c>, tg's Move Upwards key (<c>mob.up()</c>): a flyer or a climber goes up a level; with noclip through the floor.</summary>
    Up,

    /// <summary>The owner (2026-11-10): <c>down WHO</c>, tg's Move Downwards key (<c>mob.down()</c>).</summary>
    Down,

    /// <summary>M10 slice F: <c>startnow</c> is tg's admin Start Now: the round starts at once, or a second use cancels it.</summary>
    StartNow,

    /// <summary>The tram port: <c>tram [PLATFORM]</c> calls the tram to a platform (tg's call button request): the next one along its line by default.</summary>
    Tram,

    /// <summary>M10 slice G5 (D273): <c>shuttle call [norecall]|recall</c> is tg's admin Call Shuttle and Cancel Shuttle: the emergency shuttle is called or recalled.</summary>
    Shuttle,

    /// <summary>M10 slice J: <c>reboot [SECONDS]</c> is tg's admin Reboot World (its regular restart, with a delay): the round ends and the server starts the next one.</summary>
    Reboot,

    /// <summary><c>changemap [MAP]</c> is tg's admin Change Map: the next round loads MAP; without one, the maps.</summary>
    ChangeMap,

    /// <summary>M4b slice 1 (D134): <c>breach WHO|X Y [Z]</c> opens the turf WHO faces, or the turf at X Y Z, to space.</summary>
    Breach,

    /// <summary>M4b slice 1 (D134): <c>patch WHO|X Y [Z]</c> turns a space turf back into plating.</summary>
    Patch,

    /// <summary>M4b slice 2: <c>valve WHO|X Y [Z]</c> toggles the valve of the canister WHO faces (or at X Y Z), the tgui window's button (M9).</summary>
    Valve,

    /// <summary>M4b slice 2: <c>airmode WHO MODE</c> selects a mode of the air alarm of WHO's area, the tgui window's mode list (M9).</summary>
    AirMode,

    /// <summary>M4b slice 3: <c>ignite WHO|X Y [Z]</c> exposes the turf WHO faces (or at X Y Z) to a spark's heat, tg's
    /// <c>hotspot_expose(1000, 100)</c> of <c>/obj/effect/particle_effect/sparks</c>: a fire starts where there is fuel and oxygen.</summary>
    Ignite,

    /// <summary>M5x slice 1 (D149): <c>explode X Y [Z] DEV HEAVY LIGHT [FLASH FLAME]</c> is tg's admin "Drop Bomb" with a custom
    /// bomb (admin_verbs.dm:263-279): <c>explosion()</c> at the turf with those ranges, the cap ignored; FLASH and FLAME default
    /// to tg's (the devastation range and the light range).</summary>
    Explode,

    /// <summary>M5x slice 5 (D149, D150): <c>singularity X Y [Z [ENERGY]]</c> makes tg's gravitational singularity at the turf with
    /// that starting energy (600 by default: stage 3, which roams and eats; tg's own default is 50), as <c>new /obj/singularity(loc, energy)</c>.</summary>
    Singularity,

    /// <summary>Gaps E2a: <c>weather NAME|stop [Z]</c> is tg's admin verb Run Weather: SSweather runs the weather NAME (its type's last
    /// path part, <c>rad_storm</c>, or its name with underscores, <c>radiation_storm</c>) on level Z (the requester's by default);
    /// <c>stop</c> winds down every weather there.</summary>
    Weather,

    /// <summary>
    /// The owner (2026-10-15, the dev menu's rotate tool, DV56): <c>rotate WHO|ID [cw|ccw|north|south|east|west]</c> turns a thing a quarter (clockwise by
    /// default) or to a way with tg's <c>setDir</c>, for the round (the map editor turns what a map keeps).
    /// </summary>
    Rotate,

    /// <summary>
    /// The owner (2026-10-30, the dev menu's mount tool, DV56): <c>mount WHO|ID X Y [Z] north|south|east|west</c> hangs a wall mount on that side of the wall at X Y;
    /// for the round (the map editor hangs what a map keeps).
    /// </summary>
    Mount,

    /// <summary>The owner (2026-10-13, the dev menu's delete tool): <c>delete WHO|ID | delete X Y [Z]</c> is tg's admin Delete
    /// (<c>admin_delete</c>): <c>qdel</c> of a thing, <c>ScrapeAway</c> of a turf.</summary>
    Delete,

    /// <summary>The owner (2026-10-13, the dev menu's gas tools): <c>air add GAS MOLES|remove GAS|temp K|set MIX X Y [Z [R]]</c>
    /// changes the air of the open turfs within R of X Y through tg's gas_mixture procs (<c>adjust_gas</c>, <c>set_gas</c>,
    /// <c>set_temperature</c>, <c>parse_gas_string</c>).</summary>
    Air,

    /// <summary>D140 (the owner, 2026-10-13, the dev menu's player panel): <c>mobinfo [WHO]</c> (WHO defaults to <c>me</c>): the
    /// mob's health, damage, blood, body, bodyparts, organs, status effects, traits and dev switches, and the online players
    /// (<c>DevCommands.MobInfo</c>); a player gets it as a <c>dev_mob</c> ServerText for the panel, the console as lines.</summary>
    MobInfo,

    /// <summary>The owner (2026-10-15, the dev menu's status effects): <c>effects [WHO]</c> lists WHO's status effects (tg's
    /// <c>status_effects</c>) with their time left (<c>DevCommands.Effects</c>).</summary>
    Effects,

    /// <summary>The owner (2026-10-15): <c>effect WHO add TYPE [N]</c> is tg's <c>apply_status_effect</c> (N: seconds of a
    /// duration, or stacks), <c>effect WHO remove TYPE</c> tg's <c>remove_status_effect</c>, <c>effect types</c> the status
    /// effect types live in the port (<c>DevCommands.AddEffect</c>, <c>RemoveEffect</c>, <c>EffectTypes</c>).</summary>
    Effect,

    /// <summary>M7x slice A: <c>outfit [WHO] TYPE</c> is tg's admin "Select equipment" (<c>equipOutfit</c>) of a <c>/datum/outfit</c> type (WHO defaults to <c>me</c>).</summary>
    Outfit,

    /// <summary>Syndicate X1a (D316): <c>antag [WHO] TYPE</c> is tg's traitor panel making WHO's mind an antagonist (<c>add_antag_datum</c>) of a <c>/datum/antagonist</c> type (WHO defaults to <c>me</c>).</summary>
    Antag,

    /// <summary>Syndicate N3b (D316): <c>trauma [WHO] TYPE</c> is tg's admin "Give brain trauma" (<c>gain_trauma</c>, lobotomy-resilient) of a
    /// <c>/datum/brain_trauma</c> type (WHO defaults to <c>me</c>).</summary>
    Trauma,

    /// <summary>Silicons SI2 (D326): <c>robotize WHO</c> is tg's admin "Make Cyborg" (<c>Robotize</c>, admin/verbs/debug.dm:16).</summary>
    Robotize,

    /// <summary>Silicons SI4 (D326): <c>aiize WHO [here]</c> is tg's admin "Make AI" (<c>AIize</c>, admin/topic.dm:595): WHO becomes an AI at
    /// an AI spawn landmark, or where WHO stands with <c>here</c> (tg's "Move new AI to AI spawn location?" answered No).</summary>
    Aiize,

    /// <summary>Silicons SI6 (D326): <c>paiize WHO</c> is tg's admin "Make pAI" (pai/debug.dm:1): a pAI card on WHO's turf and WHO's player the
    /// pAI in it, named after WHO.</summary>
    Paiize,

    /// <summary>Syndicate X1a (D316): <c>lazyload [WHO] KEY</c> is tg's admin verb Load/Jump Lazy Template: SSmapping loads the lazy template of KEY
    /// (LT_NINJAHOLDING: the holding facility) and WHO is moved into it.</summary>
    LazyLoad,

    /// <summary>Syndicate N1a (D316): <c>nukecode [CODE]</c> is tg's admin setting of the nukes' codes (the operatives' team and the junk
    /// mail set them in a round): every nuclear bomb's code becomes CODE (five digits), or a random one when none is given, and is shown.</summary>
    NukeCode,

    /// <summary>M7x slice A, before the guns: <c>shoot [WHO] TYPE|off</c> makes WHO's clicks fire an <c>/obj/projectile</c> of TYPE with the click's aim (<c>DevCommands.Shoot</c>).</summary>
    Shoot,

    /// <summary>M6 slice 4: <c>dismember WHO ZONE</c> is tg's <c>dismember()</c> of the zone's bodypart (a chest is split open, its
    /// organs spilled; a limb or the head falls off and is thrown).</summary>
    Dismember,

    /// <summary>M6 slice 4: <c>wound WHO ZONE blunt|slash|pierce|burn moderate|severe|critical</c> is tg's
    /// <c>cause_wound_of_type_and_severity(WOUND_*, bodypart, WOUND_SEVERITY_*)</c>: the wound of that kind and severity the limb can
    /// have (a bone wound, a cut, a hole, a burn).</summary>
    Wound,

    /// <summary>M6 slice 5: <c>gib WHO [brain]</c> is tg's admin Gib verb (adminfun.dm:44): <c>gib(DROP_ALL_REMAINS)</c> with
    /// <c>brain</c> (tg's "Drop a brain?" Yes), <c>gib(DROP_ORGANS|DROP_BODYPARTS)</c> without (No): the body bursts into gibs, its
    /// organs and limbs fly apart, the player becomes a ghost.</summary>
    Gib,

    /// <summary>M7 lights slice: <c>areapower X Y [Z] equip|light|environ|all on|off</c> sets the power channel of the area at that turf as tg's APC does
    /// (<c>area.power_light = ...</c>, then <c>area.power_change()</c>, apc_power_proc.dm:32-41), so the machines of the area lose or regain it.</summary>
    AreaPower,

    /// <summary>M7 EMP slice: <c>emp X Y [Z] HEAVY LIGHT</c> is tg's admin "EM Pulse" verb (adminfun.dm:29-42): <c>empulse(turf, heavy, light)</c>
    /// at X Y Z, a heavy pulse within HEAVY tiles and a light one out to LIGHT.</summary>
    Emp,

    /// <summary>M7 slice 2 (D162): <c>recharge X Y [Z]</c> or <c>recharge all</c> fills the charge of the power storage units (SMES) at that
    /// turf, or of every one, so that a long playtest does not run the station dark (tg's <c>adjust_charge</c> of the unit's capacity).</summary>
    Recharge,

    /// <summary>M7 radiation slice: <c>irradiate X Y [Z] INTENSITY [THRESHOLD]</c> is tg's <c>radiation_pulse(turf, INTENSITY, THRESHOLD)</c>
    /// (__HELPERS/radiation.dm:25) at X Y Z: the pulse reaches INTENSITY tiles (tg's max_range) with tg's default chance, and stops
    /// where the insulation on its way falls to THRESHOLD (0.7 by default, tg's radioactive emitter's RAD_MEDIUM_INSULATION: one
    /// wall stops it).</summary>
    Irradiate,
    /// <summary>The M7 zaps slice: <c>zap X Y [Z] POWER</c> is tg's <c>tesla_zap(turf, 3, POWER, zap_flags = ZAP_DEFAULT_FLAGS |
    /// ZAP_GENERATES_POWER)</c> (energy_ball.dm:204) from the turf at X Y Z: a lightning bolt of POWER joules that strikes a tesla
    /// coil first (which stores its share for the network), then a grounding rod, a living mob, a machine or a structure, and goes on
    /// from there; below tg's cutoff of 400 kJ it does nothing.</summary>
    Zap,

    /// <summary>The M7 supermatter slice: <c>smcrystal X Y [Z] status|power|damage [N]</c> sets the internal energy (MeV) or the
    /// damage of the supermatter crystal at X Y Z, or reports its state (tg's admins edit these vars in view variables).</summary>
    SmCrystal,

    /// <summary>The M7 delamination slice: <c>smdelam X Y [Z] [cascade|singularity|tesla|explosive]</c> delaminates the supermatter
    /// crystal at X Y Z at once (tg's admin path: set_delam with the named strategy, then force_delam).</summary>
    SmDelam,

    /// <summary>M8 slice A (D195): <c>reagent WHO TYPE N</c> is tg's admin "Add reagent" (view variables): <c>reagents.add_reagent(TYPE, N)</c>
    /// into WHO's body (the blood); TYPE is a <c>/datum/reagent</c> path or a reagent's name as tg's <c>get_chem_id</c> reads it
    /// (<c>water</c>, <c>rat_spit</c>).</summary>
    Reagent,

    /// <summary>M8 slice A (D195): <c>reagents [WHO]</c> lists the reagents in WHO's body and stomach, with their volume and whether
    /// they are metabolizing or overdosed (<c>DevCommands.Reagents</c>).</summary>
    Reagents,

    /// <summary>M8 slice E (D195): <c>heart WHO stop|start</c> is tg's <c>set_heartattack(TRUE|FALSE)</c> on a carbon (life.dm:557): a stopped
    /// heart is cardiac arrest (no blood flow, oxygen damage and crit as tg's <c>handle_heart</c>), which the defibrillator's paddles
    /// restart.</summary>
    Heart,

    /// <summary>M8 slice E (D195): <c>breath WHO N</c> sets WHO's <c>losebreath</c> to N (tg's admin view variables): the breaths WHO
    /// misses, one a Life tick (0 to 100).</summary>
    Breath,

    /// <summary>M8 slice G (D195): <c>hallucinate WHO TYPE [SECONDS]</c> is tg's admin "Give Hallucination" (<c>admin_give_hallucination</c>): WHO gets
    /// that <c>/datum/hallucination</c> type once (with <c>duration</c> SECONDS where the type takes one); <c>hallucinate WHO SECONDS</c> gives the
    /// hallucination status effect for that long, from which tg picks its random ones.</summary>
    Hallucinate,
    Ceiling,

    /// <summary>The owner (2026-10-28, DV211): <c>projspeed F</c> sets tg's own debug knob <c>SSprojectiles.pixels_per_decisecond</c> to F times
    /// tg's 32: every projectile flies F times as fast (2 by default in the port).</summary>
    ProjectileSpeed,

    /// <summary>The owner (2026-10-31, DV233): <c>footstride run|walk TILES</c>, the tiles walked per footstep stride (a step every two).</summary>
    Footstride,

    /// <summary>The owner (2026-11-03, DV263): <c>movefeel ACCEL DECEL BACK STRAFE</c>, the walking capsules' speed ramp and slower directions (<c>Ranges</c>).</summary>
    MoveFeel,

    /// <summary>D315 (the owner, 2026-11-03): <c>minimapmobs on|off</c>, the dev menu's trackers: the living mobs of the requester's level on
    /// its minimap (a session's view, nothing in the world).</summary>
    MinimapMobs,

    /// <summary>DV580 (the owner, 2026-11-21): <c>autorespawn on|off [SECONDS]</c>, every dead player comes back after SECONDS (10 by default) as a
    /// new crew member at the next spawn point, with a countdown on the screen (tg's Respawn verb, <c>abandon_mob</c>, taken for them).</summary>
    AutoRespawn,

    /// <summary>D346 (the owner, 2026-11-21): <c>possess WHO [force]</c> is tg's admin "Assume Direct Control" (admin/verbs/debug.dm:166): the
    /// requester's player moves into WHO, any mob in the world (an NPC, a creature, a robot); a mob another player plays needs <c>force</c>
    /// (that player becomes a ghost).</summary>
    Possess,

    /// <summary>D346: <c>unpossess</c> takes the requester back to the body they left with <c>possess</c> (or to a ghost, when they came as a
    /// ghost or the body is gone); the mob left behind gets its AI back. The port's own: tg's admin ghosts out instead.</summary>
    Unpossess,

    /// <summary>D346: <c>givecontrol WHO PLAYER [delete] [force]</c> is tg's admin "Give Direct Control" (admin/verbs/debug.dm:187): PLAYER moves
    /// into WHO; <c>delete</c> deletes PLAYER's old mob (a ghost always is), <c>force</c> takes WHO from the player in it.</summary>
    GiveControl,
}

/// <summary>A parsed dev command; the fields a verb does not use are default.</summary>
/// <param name="Verb">Which command.</param>
/// <param name="Who">The target: a player's key, a world id (<c>#123</c> or <c>123</c>) or <c>me</c>; null for the verbs without one.</param>
/// <param name="Type">A damage type (<c>damage</c>, <c>heal</c>).</param>
/// <param name="Number">The amount: damage, seconds, stacks, millilitres, the speed scale.</param>
/// <param name="Zone">A body zone (<c>damage</c>, <c>bleed</c>, <c>dismember</c>, <c>wound</c>): tg's <c>BODY_ZONE_*</c> text.</param>
/// <param name="Mix">The gas string of <c>gas</c> (a preset already turned into tg's string).</param>
/// <param name="X">The turf x of <c>gas</c> and <c>spawn</c>, or null.</param>
/// <param name="Y">The turf y.</param>
/// <param name="Z">The turf z (1 when coordinates were given without one).</param>
/// <param name="TypePath">M4a: the DM type path of <c>give</c> (<c>/obj/item/...</c>, syntax checked; whether tg has the type is the command's check).</param>
/// <param name="Mode">M4b slice 2: the air alarm mode of <c>airmode</c> (one of <see cref="DevCommandLine.AirModes"/>); the operation of <c>air</c> (add, remove, temp, set).</param>
/// <param name="Count">The amount of <c>spawn</c> (1 to tg's <c>ADMIN_SPAWN_CAP</c>).</param>
/// <param name="Dir">The dir of <c>spawn</c> (0: the type's own).</param>
/// <param name="Radius">The radius of <c>air</c> (0: the one turf; the square of 2R + 1 turfs a side).</param>
/// <param name="Ranges">M5x slice 1: the ranges of <c>explode</c>: devastation, heavy, light, flash and flame (NaN: tg's default); the heavy and light ranges of <c>emp</c>.</param>
/// <remarks><c>effect</c>: <c>Mode</c> is add, remove or types, <c>TypePath</c> the status effect (a path, a path below
/// /datum/status_effect/ or an id), <c>Number</c> its N or NaN without one. <c>wound</c>: <c>Mode</c> is tg's wounding type
/// (<c>WOUND_BLUNT</c> and the others), <c>Number</c> the severity (<c>WOUND_SEVERITY_MODERATE</c> to <c>WOUND_SEVERITY_CRITICAL</c>).</remarks>
public sealed record DevCommand(DevVerb Verb, string Who = null, DevDamageType? Type = null, float Number = 0f, string Zone = null,
    string Mix = null, int? X = null, int? Y = null, int? Z = null, string TypePath = null, string Mode = null, int Count = 1, byte Dir = 0,
    int Radius = 0, float[] Ranges = null);

/// <summary>The dev command syntax: <see cref="TryParse"/> and the help text.</summary>
public static class DevCommandLine
{
    /// <summary>The longest line accepted (the protocol's cap for a dev command).</summary>
    public const int MaxLineBytes = Limits.MaxDevCommandBytes;

    /// <summary>The largest amount of one <c>damage</c> command.</summary>
    public const float MaxDamage = 1000f;

    /// <summary>DV580: the longest <c>autorespawn</c> delay (ten minutes).</summary>
    public const float MaxAutoRespawnSeconds = 600f;

    /// <summary>The longest <c>gas</c> mix string.</summary>
    public const int MaxMixChars = 120;

    /// <summary>tg's <c>ADMIN_SPAWN_CAP</c> (code/__DEFINES/admin.dm:150): the most atoms of one spawn.</summary>
    public const int MaxSpawnCount = 100;

    /// <summary>M5x slice 5 (D150): the dev singularity's starting energy when none is given (stage 3: 500 to 999 roams and eats).</summary>
    public const float DefaultSingularityEnergy = 600f;

    /// <summary>M5x slice 5: the largest starting energy of the dev singularity (stage 5 is 2000 and up; it grows without a cap).</summary>
    public const float MaxSingularityEnergy = 100000f;

    /// <summary>M5x slice 1: the largest range of <c>explode</c> (tg's admin bomb asks for any number; 50 tiles covers the
    /// station-sized blasts a test needs and keeps one command from taking the whole map).</summary>
    public const float MaxExplosionRange = 50f;

    /// <summary>The M7 supermatter slice: the largest <c>smcrystal power</c> in MeV (tg's crystal past 9000 is critical) and
    /// <c>smcrystal damage</c> (its explosion point is 100).</summary>
    public const float MaxSmCrystalPower = 100000f, MaxSmCrystalDamage = 100f;

    /// <summary>The M7 zaps slice: the strongest <c>zap</c> in joules (a supermatter's strongest bolts are a few hundred megajoules).</summary>
    public const float MaxZapPower = 1e10f;

    /// <summary>M7 EMP slice: the largest range of <c>emp</c> (tg's admin EM Pulse asks for any number; the same cap as <c>explode</c>).</summary>
    public const float MaxEmpRange = MaxExplosionRange;

    /// <summary>M7 radiation slice: the largest INTENSITY (max range) of <c>irradiate</c> (the same cap as <c>explode</c>).</summary>
    public const float MaxIrradiateRange = MaxExplosionRange;

    /// <summary>M7 radiation slice: the THRESHOLD of <c>irradiate</c> when none is given (tg's radioactive emitter's default,
    /// RAD_MEDIUM_INSULATION, radioactive_emitter.dm:21: what one common wall lets through).</summary>
    public const float DefaultIrradiateThreshold = Defines.RAD_MEDIUM_INSULATION;

    /// <summary>The most moles one <c>air add</c> adds or removes on a turf.</summary>
    public const float MaxAirMoles = 100000f;

    /// <summary>The hottest <c>air temp</c>.</summary>
    public const float MaxAirTemperature = 100000f;

    /// <summary>The widest <c>air</c> radius (the gas overlay's own).</summary>
    public const int MaxAirRadius = Limits.DevGasProbeRadius;

    /// <summary>M8 slice A: the largest N of <c>reagent</c> (a carbon's holder takes 1000 units, carbon.dm:97).</summary>
    public const float MaxReagentUnits = 1000f;

    /// <summary>M8 slice E: the largest N of <c>breath</c>.</summary>
    public const float MaxLoseBreath = 100f;

    /// <summary>The largest N of <c>effect add</c> (seconds of a duration, or stacks).</summary>
    public const float MaxEffectNumber = 600f;

    /// <summary>The <c>air</c> operations.</summary>
    public static readonly string[] AirOps = { "add", "remove", "temp", "set" };

    /// <summary>The help text of the dev commands.</summary>
    public static readonly string Help =
        "Dev commands (docs/ai/specs/apps.md 1.6a); WHO is a key, a world id or me:\n"
        + "  damage WHO brute|burn|tox|oxy N [ZONE]   tg's apply_damage with a zone, else adjust_*_loss (N from 0 to 1000)\n"
        + "  heal WHO [brute|burn|tox|oxy]            adjust_*_loss(-loss) of the type, or all four\n"
        + "  fullheal WHO                             tg's admin heal without the revival: fully_heal(ADMIN_HEAL_ALL)\n"
        + "  kill WHO | revive WHO                    death() | revive(ADMIN_HEAL_ALL)\n"
        + "  knockdown WHO S | stun WHO S             Knockdown | Stun for S seconds (0 to 60)\n"
        + "  bleed WHO ZONE STACKS                    the bodypart's adjustBleedStacks (-30 to 30)\n"
        + "  blood WHO ML                             set_blood_volume (0 to 2000)\n"
        + "  dismember WHO ZONE                       tg's dismember() of the zone's bodypart (a chest is split open)\n"
        + "  wound WHO ZONE blunt|slash|pierce|burn moderate|severe|critical  tg's cause_wound_of_type_and_severity\n"
        + "  gib WHO [brain]                          tg's admin Gib: the body bursts, its organs and limbs fly (brain: the head too)\n"
        + "  gas air|vacuum|MIX [X Y [Z]]             the air of one turf, or of every open turf but space, from tg's parse_gas_string (o2=22;n2=82;TEMP=293.15)\n"
        + "  analyze WHO                              tg's healthscan, verbose, to you only\n"
        + "  spawn TYPE [X Y [Z [N [DIR]]]]           tg's spawn panel: N (1 to 100) of an /obj, /mob or /turf type at X Y Z (else at your feet), facing DIR (0: the type's own)\n"
        + "  speedscale F                             the capsule speed scale (above 0, at most " + ServerConfig.MaxMoveSpeedScale.ToString(CultureInfo.InvariantCulture) + ")\n"
        + "  footstride run|walk TILES                the tiles walked per footstep stride, a step every two (above 0, at most 4)\n"
        + "  movefeel ACCEL DECEL BACK STRAFE         ticks to full speed and to a stop (0 to 40), the speed lost going back and sideways (0 to 0.9); 0 0 0 0 is tg's\n"
        + "  minimapmobs on|off                       the living mobs of your level on your minimap (players green, the rest red)\n"
        + "  autorespawn on|off [SECONDS]             every dead player respawns after SECONDS (default 10) as a new crew member at a spawn point\n"
        + "  speed WHO F                              WHO's own speed times F over the speed scale (above 0, at most " + ServerConfig.MaxMoveSpeedScale.ToString(CultureInfo.InvariantCulture) + ")\n"
        + "  godmode WHO                              toggles tg's TRAIT_GODMODE, with tg's no-stagger, no-daze, no-fire and ignore-slowdown traits\n"
        + "  fly WHO                                  toggles tg's flying (TRAIT_MOVE_FLYING), negated gravity (no wind push) and ignored slowdowns\n"
        + "  noclip WHO                               toggles tg's incorporeal movement (through walls)\n"
        + "  invuln WHO                               toggles invulnerability: godmode's immunities, no push, and no one else can click or drag WHO (no shove, grab, strip, cuffs)\n"
        + "  esp WHO                                  toggles tg's Admin ESP: WHO sees the ghosts\n"
        + "  invisimin WHO                            toggles tg's Invisimin: only ghosts and esp see WHO\n"
        + "  god WHO                                  fly, noclip and godmode on together (off when all three are on)\n"
        + "  up WHO | down WHO                        tg's Move Upwards / Downwards [PageUp, PageDown]: flying, a level up or down (with noclip through the floor)\n"
        + "  startnow                                 tg's admin Start Now: the round starts at once (again before then: cancels, 3 minutes)\n"
        + "  tram [PLATFORM]                          calls the tram to the platform (tg's call button request; default the next one on its line)\n"
        + "  shuttle call [norecall]|recall           tg's admin Call/Cancel Shuttle: the emergency shuttle is called or recalled\n"
        + "  shuttle dock                             the emergency shuttle is called if it was not and docks with the station at once\n"
        + "  shuttle time SECONDS                     tg's admin shuttle timers: the call, dock and escape times, and the running timer\n"
        + "  reboot [SECONDS]                         tg's admin Reboot World: the world reboots after SECONDS (default 1) into the next round\n"
        + "  reboot now                               tg's Hard Restart: the world reboots at once, with no countdown\n"
        + "  changemap [MAP]                          tg's admin Change Map: the next round loads MAP (without one: the maps)\n"
        + "  give WHO TYPE                            a new item of tg's TYPE (/obj/item/...) in WHO's hands (D130; /obj/item/card/id/advanced/debug: all access)\n"
        + "  drop WHO SLOT                            drops what WHO wears in SLOT (back, suit, head, mask, ...) to the floor (tg's dropItemToGround)\n"
        + "  outfit [WHO] TYPE                        tg's admin Select equipment: equipOutfit of a /datum/outfit type (/datum/outfit/job/security)\n"
        + "  antag [WHO] TYPE                         tg's traitor panel: makes WHO's mind an antagonist of TYPE (/datum/antagonist/traitor)\n"
        + "  robotize WHO                             tg's admin Make Cyborg: WHO becomes a cyborg (Robotize)\n"
        + "  aiize WHO [here]                         tg's admin Make AI: WHO becomes an AI at an AI spawn (here: where WHO stands)\n"
        + "  paiize WHO                               tg's admin Make pAI: a pAI card at WHO's feet and WHO's player the pAI in it\n"
        + "  possess WHO [force]                      tg's admin Assume Direct Control: you play WHO, any mob (force: take it from its player)\n"
        + "  unpossess                                back to the body you left with possess (a ghost when there is none)\n"
        + "  givecontrol WHO PLAYER [delete] [force]  tg's admin Give Direct Control: PLAYER plays WHO (delete: PLAYER's old mob)\n"
        + "  lazyload [WHO] KEY                       tg's Load/Jump Lazy Template: loads KEY's template (LT_NINJAHOLDING) and moves WHO into it\n"
        + "  nukecode [CODE]                          sets every nuke's code (five digits; random when none) and shows it\n"
        + "  shoot [WHO] TYPE|off                     WHO's clicks fire an /obj/projectile of TYPE along the crosshair (before the guns; off: clicks again)\n"
        + "  gravity WHO on|off [now]                 the gravity generator's breaker on WHO's level (tg's pace; now: at once)\n"
        + "  breach WHO | breach X Y [Z]              the turf WHO faces (or at X Y Z) becomes space, its windows and doors removed\n"
        + "  patch WHO | patch X Y [Z]                a space turf WHO faces (or at X Y Z) becomes plating\n"
        + "  valve WHO | valve X Y [Z]                toggles the valve of the canister WHO faces (or at X Y Z)\n"
        + "  areapower X Y [Z] equip|light|environ|all on|off  a power channel of the area at X Y Z, as its APC sets it (lights go to their emergency cells)\n"
        + "  recharge X Y [Z] | recharge all          fills the charge of the SMES at X Y Z (or of every one): the station's stored power for long playtests\n"
        + "  airmode WHO MODE                         the air alarm of WHO's area: filtering, contaminated, draught, refill, cycle, siphon, panic or off\n"
        + "  ignite WHO | ignite X Y [Z]              a spark on the turf WHO faces (or at X Y Z): burns plasma, tritium or hydrogen\n"
        + "  explode X Y [Z] DEV HEAVY LIGHT [FLASH FLAME]  tg's admin Drop Bomb: explosion() at X Y Z with those ranges (no cap)\n"
        + "  emp X Y [Z] HEAVY LIGHT                  tg's admin EM Pulse: empulse() at X Y Z, a heavy pulse within HEAVY tiles and a light one out to LIGHT\n"
        + "  irradiate X Y [Z] INTENSITY [THRESHOLD]  tg's radiation_pulse() at X Y Z out to INTENSITY tiles, stopped where insulation falls to THRESHOLD (0.7: a wall)\n"
        + "  zap X Y [Z] POWER                        tg's tesla_zap from X Y Z: a bolt of POWER joules (at least 400000) striking a tesla coil, rod, mob, machine or structure\n"
        + "  smcrystal X Y [Z] status|power|damage [N]  the supermatter crystal at X Y Z: sets its internal energy (MeV) or damage, or reports its state\n"
        + "  smdelam X Y [Z] [cascade|singularity|tesla|explosive]  tg's admin path: the supermatter crystal at X Y Z delaminates now (with that strategy)\n"
        + "  singularity X Y [Z [ENERGY]]  a gravitational singularity at X Y Z with that energy (600 by default: stage 3)\n"
        + "  weather NAME|stop [Z]                    tg's Run Weather: the weather NAME (rad_storm, rain_storm, snow_storm, sand_storm, ash_storm, ...) on level Z (yours), or stop it\n"
        + "  delete WHO|ID | delete X Y [Z]           tg's admin Delete: qdel of a thing (not a player's mob), ScrapeAway of a turf\n"
        + "  rotate WHO|ID [cw|ccw|north|south|east|west]  turns a thing a quarter (cw) or to a way (setDir), for the round\n"
        + "  mount WHO|ID X Y [Z] north|south|east|west  hangs a wall mount on that side of the wall at X Y, for the round\n"
        + "  mobinfo [WHO]                            WHO's health, damage, blood, organs, status effects, traits and dev switches\n"
        + "  effects [WHO]                            WHO's status effects with their time left\n"
        + "  effect WHO add TYPE [N]                  tg's apply_status_effect (TYPE: incapacitating/stun, staggered, an id...; N: seconds or stacks)\n"
        + "  effect WHO remove TYPE | effect types    tg's remove_status_effect | the status effect types live in the port\n"
        + "  reagent WHO TYPE N                       tg's admin Add reagent: N units (0 to 1000) of a /datum/reagent path or a reagent's name into WHO's blood\n"
        + "  reagents [WHO]                           the reagents in WHO's blood and stomach, metabolizing or overdosed\n"
        + "  heart WHO stop|start                     tg's set_heartattack: stops WHO's heart (cardiac arrest) or starts it\n"
        + "  breath WHO N                             WHO's losebreath (0 to 100): the breaths WHO misses, one a Life tick\n"
        + "  air add GAS MOLES X Y [Z [R]]            adds (or, below 0, removes) moles of tg's gas id (plasma, o2, n2, co2, n2o, water_vapor...)\n"
        + "  air remove GAS X Y [Z [R]]               all of one gas out\n"
        + "  air temp K X Y [Z [R]]                   the air's temperature in kelvin\n"
        + "  air set air|vacuum|MIX X Y [Z [R]]       the air from tg's parse_gas_string; R: every open turf within R (0 to 7) of X Y\n"
        + "ZONE is head, chest, l_arm, r_arm, l_leg or r_leg.";

    /// <summary>M4b slice 2: the <c>airmode</c> modes, tg's /datum/air_alarm_mode subtypes by short names (the emag-only flood
    /// and vent siphon, which tg's select_mode refuses on a normal alarm, are not offered).</summary>
    public static readonly string[] AirModes = { "filtering", "contaminated", "draught", "refill", "cycle", "siphon", "panic", "off" };

    /// <summary>The wound types of <c>wound</c>: tg's wounding types (code/__DEFINES/wounds.dm).</summary>
    public static readonly IReadOnlyDictionary<string, string> WoundTypes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["blunt"] = Defines.WOUND_BLUNT, ["slash"] = Defines.WOUND_SLASH, ["pierce"] = Defines.WOUND_PIERCE, ["burn"] = Defines.WOUND_BURN,
    };

    /// <summary>The severities of <c>wound</c>: tg's <c>WOUND_SEVERITY_*</c> a limb's wound can have.</summary>
    public static readonly IReadOnlyDictionary<string, int> WoundSeverities = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["moderate"] = Defines.WOUND_SEVERITY_MODERATE, ["severe"] = Defines.WOUND_SEVERITY_SEVERE, ["critical"] = Defines.WOUND_SEVERITY_CRITICAL,
    };

    private static readonly string[] Zones = { Defines.BODY_ZONE_HEAD, Defines.BODY_ZONE_CHEST, Defines.BODY_ZONE_L_ARM, Defines.BODY_ZONE_R_ARM, Defines.BODY_ZONE_L_LEG, Defines.BODY_ZONE_R_LEG };

    /// <summary>True when <paramref name="word"/> is the first word of a dev command (case-insensitive).</summary>
    public static bool IsDevVerb(string word) => word != null && TryVerb(word, out _);

    /// <summary>
    /// Parses a dev command line; false with <paramref name="error"/> (a line for the requester) when the verb is unknown or
    /// an argument is missing, out of range or not of its form. Never throws.
    /// </summary>
    public static bool TryParse(string line, out DevCommand command, out string error)
    {
        command = null;
        error = null;
        if (line == null) { error = "no line"; return false; }
        if (Encoding.UTF8.GetByteCount(line) > MaxLineBytes) { error = $"the line is longer than {MaxLineBytes} bytes"; return false; }
        foreach (char c in line)
        {
            if (char.IsControl(c) && c != '\t') { error = "the line has control characters"; return false; }
        }
        string[] w = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        if (w.Length == 0) { error = "empty line"; return false; }
        if (!TryVerb(w[0], out DevVerb verb)) { error = $"unknown dev command '{w[0]}'"; return false; }
        string usage;
        switch (verb)
        {
            case DevVerb.Ceiling:
            {
                usage = "usage: ceiling none|lattice|catwalk|scrape|break|fix|/turf/open/floor/... X Y Z [WIDTH HEIGHT]";
                if (w.Length != 5 && w.Length != 7) return Fail(usage, out error);
                bool turf = w[1].StartsWith("/turf/open/floor", StringComparison.Ordinal) && w[1].Length <= Anomaly.Core.Motion.CeilingState.MaxTurfLength;
                if (!turf && w[1] is not ("none" or "lattice" or "catwalk" or "scrape" or "break" or "fix")) return Fail(usage, out error);
                if (!int.TryParse(w[2], out int cx) || !int.TryParse(w[3], out int cy) || !int.TryParse(w[4], out int cz)
                    || cx < 1 || cy < 1 || cz < 1 || cx > Limits.MaxMapSize || cy > Limits.MaxMapSize || cz > Limits.MaxMapLevels) return Fail(usage, out error);
                int width = 1, height = 1;
                if (w.Length == 7 && (!int.TryParse(w[5], out width) || !int.TryParse(w[6], out height)
                    || width < 1 || height < 1 || width > 16 || height > 16)) return Fail(usage, out error);
                command = new DevCommand(verb, X: cx, Y: cy, Z: cz, Mode: turf ? "turf" : w[1], Count: width, Radius: height, TypePath: turf ? w[1] : null);
                return true;
            }
            case DevVerb.Damage:
            {
                usage = "usage: damage WHO brute|burn|tox|oxy N [ZONE]";
                if (w.Length != 4 && w.Length != 5) return Fail(usage, out error);
                if (!TryType(w[2], out DevDamageType type)) return Fail($"'{w[2]}' is not a damage type (brute, burn, tox, oxy)", out error);
                if (!TryNumber(w[3], out float n) || n <= 0f || n > MaxDamage) return Fail($"damage must be above 0 and at most {MaxDamage:0}, got '{w[3]}'", out error);
                string zone = null;
                if (w.Length == 5 && !TryZone(w[4], out zone, out error)) return false;
                command = new DevCommand(verb, w[1], type, n, zone);
                return true;
            }
            case DevVerb.Heal:
            {
                usage = "usage: heal WHO [brute|burn|tox|oxy]";
                if (w.Length != 2 && w.Length != 3) return Fail(usage, out error);
                DevDamageType? type = null;
                if (w.Length == 3)
                {
                    if (!TryType(w[2], out DevDamageType t)) return Fail($"'{w[2]}' is not a damage type (brute, burn, tox, oxy)", out error);
                    type = t;
                }
                command = new DevCommand(verb, w[1], type);
                return true;
            }
            case DevVerb.MobInfo:
                if (w.Length > 2) return Fail("usage: mobinfo [WHO]", out error);
                command = new DevCommand(verb, w.Length == 2 ? w[1] : "me");
                return true;
            case DevVerb.Effects:
                if (w.Length > 2) return Fail("usage: effects [WHO]", out error);
                command = new DevCommand(verb, w.Length == 2 ? w[1] : "me");
                return true;
            case DevVerb.Effect:
            {
                usage = "usage: effect WHO add TYPE [N] | effect WHO remove TYPE | effect types";
                if (w.Length == 2 && w[1].Equals("types", StringComparison.OrdinalIgnoreCase))
                {
                    command = new DevCommand(verb, Mode: "types", Number: float.NaN);
                    return true;
                }
                if (w.Length < 4) return Fail(usage, out error);
                string op = w[2].ToLowerInvariant();
                if (op is not ("add" or "remove")) return Fail(usage, out error);
                if ((op == "remove" && w.Length != 4) || w.Length > 5) return Fail(usage, out error);
                if (!IsEffectName(w[3])) return Fail($"'{w[3]}' is not a status effect type or id (letters, digits, _ and /)", out error);
                float n = float.NaN;
                if (w.Length == 5 && (!TryNumber(w[4], out n) || n <= 0f || n > MaxEffectNumber))
                    return Fail(string.Create(CultureInfo.InvariantCulture, $"N (seconds or stacks) must be above 0 and at most {MaxEffectNumber}, got '{w[4]}'"), out error);
                command = new DevCommand(verb, w[1], Number: n, TypePath: w[3], Mode: op);
                return true;
            }
            case DevVerb.Reagents:
                if (w.Length > 2) return Fail("usage: reagents [WHO]", out error);
                command = new DevCommand(verb, w.Length == 2 ? w[1] : "me");
                return true;
            case DevVerb.Reagent:
            {
                if (w.Length != 4) return Fail("usage: reagent WHO TYPE N", out error);
                if (!IsTypePath(w[2], "/datum/reagent") && !IsReagentName(w[2])) return Fail($"'{w[2]}' is not a reagent type path (/datum/reagent/...) or name (letters, digits and _)", out error);
                if (!TryNumber(w[3], out float units) || units <= 0f || units > MaxReagentUnits) return Fail($"units must be above 0 and at most {MaxReagentUnits:0}, got '{w[3]}'", out error);
                command = new DevCommand(verb, w[1], Number: units, TypePath: w[2]);
                return true;
            }
            case DevVerb.Heart:
            {
                if (w.Length != 3 || w[2].ToLowerInvariant() is not ("stop" or "start")) return Fail("usage: heart WHO stop|start", out error);
                command = new DevCommand(verb, w[1], Mode: w[2].ToLowerInvariant());
                return true;
            }
            case DevVerb.Hallucinate:
            {
                if (w.Length is < 3 or > 4) return Fail("usage: hallucinate WHO TYPE [SECONDS]  or  hallucinate WHO SECONDS", out error);
                if (w.Length == 3 && TryNumber(w[2], out float status))
                {
                    if (status <= 0f || status > MaxEffectNumber) return Fail(string.Create(CultureInfo.InvariantCulture, $"SECONDS must be above 0 and at most {MaxEffectNumber}, got '{w[2]}'"), out error);
                    command = new DevCommand(verb, w[1], Number: status);
                    return true;
                }
                if (!IsTypePath(w[2], "/datum/hallucination")) return Fail($"'{w[2]}' is not a hallucination type path (/datum/hallucination/...)", out error);
                float seconds = float.NaN;
                if (w.Length == 4 && (!TryNumber(w[3], out seconds) || seconds <= 0f || seconds > MaxEffectNumber))
                    return Fail(string.Create(CultureInfo.InvariantCulture, $"SECONDS must be above 0 and at most {MaxEffectNumber}, got '{w[3]}'"), out error);
                command = new DevCommand(verb, w[1], Number: seconds, TypePath: w[2]);
                return true;
            }
            case DevVerb.Breath:
            {
                if (w.Length != 3) return Fail("usage: breath WHO N", out error);
                if (!TryNumber(w[2], out float breaths) || breaths < 0f || breaths > MaxLoseBreath) return Fail($"N must be from 0 to {MaxLoseBreath:0}, got '{w[2]}'", out error);
                command = new DevCommand(verb, w[1], Number: breaths);
                return true;
            }
            case DevVerb.FullHeal:
            case DevVerb.Kill:
            case DevVerb.Revive:
            case DevVerb.Robotize:
            case DevVerb.Paiize:
            case DevVerb.Analyze:
            case DevVerb.GodMode:
            case DevVerb.Pilot:
            case DevVerb.Fly:
            case DevVerb.NoClip:
            case DevVerb.Invulnerable:
            case DevVerb.Esp:
            case DevVerb.Invisimin:
            case DevVerb.God:
            case DevVerb.Up:
            case DevVerb.Down:
                if (w.Length != 2) return Fail($"usage: {w[0].ToLowerInvariant()} WHO", out error);
                command = new DevCommand(verb, w[1]);
                return true;
            case DevVerb.Knockdown:
            case DevVerb.Stun:
            {
                if (w.Length != 3) return Fail($"usage: {w[0].ToLowerInvariant()} WHO SECONDS", out error);
                if (!TryNumber(w[2], out float s) || s <= 0f || s > 60f) return Fail($"seconds must be above 0 and at most 60, got '{w[2]}'", out error);
                command = new DevCommand(verb, w[1], Number: s);
                return true;
            }
            case DevVerb.Bleed:
            {
                if (w.Length != 4) return Fail("usage: bleed WHO ZONE STACKS", out error);
                if (!TryZone(w[2], out string zone, out error)) return false;
                if (!TryNumber(w[3], out float stacks) || stacks == 0f || Math.Abs(stacks) > 30f) return Fail($"stacks must be from -30 to 30 and not 0, got '{w[3]}'", out error);
                command = new DevCommand(verb, w[1], Number: stacks, Zone: zone);
                return true;
            }
            case DevVerb.Dismember:
            {
                if (w.Length != 3) return Fail("usage: dismember WHO ZONE", out error);
                if (!TryZone(w[2], out string zone, out error)) return false;
                command = new DevCommand(verb, w[1], Zone: zone);
                return true;
            }
            case DevVerb.Gib:
            {
                if (w.Length < 2 || w.Length > 3 || (w.Length == 3 && !string.Equals(w[2], "brain", StringComparison.OrdinalIgnoreCase)))
                    return Fail("usage: gib WHO [brain]", out error);
                command = new DevCommand(verb, w[1], Mode: w.Length == 3 ? "brain" : null);
                return true;
            }
            case DevVerb.Wound:
            {
                usage = "usage: wound WHO ZONE blunt|slash|pierce|burn moderate|severe|critical";
                if (w.Length != 5) return Fail(usage, out error);
                if (!TryZone(w[2], out string zone, out error)) return false;
                if (!WoundTypes.TryGetValue(w[3].ToLowerInvariant(), out string wounding)) return Fail($"'{w[3]}' is not a wound type (blunt, slash, pierce, burn)", out error);
                if (!WoundSeverities.TryGetValue(w[4].ToLowerInvariant(), out int severity)) return Fail($"'{w[4]}' is not a wound severity (moderate, severe, critical)", out error);
                command = new DevCommand(verb, w[1], Number: severity, Zone: zone, Mode: wounding);
                return true;
            }
            case DevVerb.Blood:
            {
                if (w.Length != 3) return Fail("usage: blood WHO ML", out error);
                if (!TryNumber(w[2], out float ml) || ml < 0f || ml > 2000f) return Fail($"millilitres must be from 0 to 2000, got '{w[2]}'", out error);
                command = new DevCommand(verb, w[1], Number: ml);
                return true;
            }
            case DevVerb.Gas:
            {
                usage = "usage: gas air|vacuum|MIX [X Y [Z]]";
                if (w.Length != 2 && w.Length != 4 && w.Length != 5) return Fail(usage, out error);
                if (!TryMix(w[1], out string mix, out error)) return false;
                int? x = null, y = null, z = null;
                if (w.Length > 2 && !TryCoordinates(w, 2, out x, out y, out z, out error)) return false;
                command = new DevCommand(verb, Mix: mix, X: x, Y: y, Z: z);
                return true;
            }
            case DevVerb.AirMode:
            {
                if (w.Length != 3) return Fail("usage: airmode WHO MODE", out error);
                string mode = w[2].ToLowerInvariant();
                if (Array.IndexOf(AirModes, mode) < 0) return Fail($"'{w[2]}' is not an air alarm mode ({string.Join(", ", AirModes)})", out error);
                command = new DevCommand(verb, w[1], Mode: mode);
                return true;
            }
            case DevVerb.Breach:
            case DevVerb.Patch:
            case DevVerb.Valve:
            case DevVerb.Ignite:
            {
                usage = $"usage: {w[0].ToLowerInvariant()} WHO | {w[0].ToLowerInvariant()} X Y [Z]";
                if (w.Length == 2)
                {
                    command = new DevCommand(verb, w[1]);
                    return true;
                }
                if (w.Length != 3 && w.Length != 4) return Fail(usage, out error);
                if (!TryCoordinates(w, 1, out int? x, out int? y, out int? z, out error)) return false;
                command = new DevCommand(verb, X: x, Y: y, Z: z);
                return true;
            }
            case DevVerb.Explode:
            {
                // X Y [Z] DEV HEAVY LIGHT [FLASH FLAME]: 5 to 8 numbers, the count tells whether Z and FLASH FLAME are given
                usage = "usage: explode X Y [Z] DEV HEAVY LIGHT [FLASH FLAME]";
                int numbers = w.Length - 1;
                if (numbers < 5 || numbers > 8) return Fail(usage, out error);
                bool withZ = numbers == 6 || numbers == 8;
                bool withExtra = numbers >= 7;
                string[] coordinates = withZ ? new[] { w[0], w[1], w[2], w[3] } : new[] { w[0], w[1], w[2] };
                if (!TryCoordinates(coordinates, 1, out int? x, out int? y, out int? z, out error)) return false;
                int first = withZ ? 4 : 3;
                var ranges = new float[5] { 0f, 0f, 0f, float.NaN, float.NaN };
                for (int i = 0; i < (withExtra ? 5 : 3); i++)
                {
                    if (!TryNumber(w[first + i], out float range) || range < 0f || range > MaxExplosionRange)
                        return Fail($"a range is a number from 0 to {MaxExplosionRange}, got '{w[first + i]}'", out error);
                    ranges[i] = range;
                }
                command = new DevCommand(verb, X: x, Y: y, Z: z, Ranges: ranges);
                return true;
            }
            case DevVerb.Recharge:
            {
                // all | X Y [Z]
                usage = "usage: recharge X Y [Z] | recharge all";
                if (w.Length == 2 && w[1].ToLowerInvariant() == "all")
                {
                    command = new DevCommand(verb, Mode: "all");
                    return true;
                }
                if (w.Length != 3 && w.Length != 4) return Fail(usage, out error);
                if (!TryCoordinates(w, 1, out int? x, out int? y, out int? z, out error)) return false;
                command = new DevCommand(verb, X: x, Y: y, Z: z);
                return true;
            }
            case DevVerb.Emp:
            {
                // X Y [Z] HEAVY LIGHT: 4 or 5 numbers, the count tells whether Z is given
                usage = "usage: emp X Y [Z] HEAVY LIGHT";
                int numbers = w.Length - 1;
                if (numbers != 4 && numbers != 5) return Fail(usage, out error);
                bool withZ = numbers == 5;
                string[] coordinates = withZ ? new[] { w[0], w[1], w[2], w[3] } : new[] { w[0], w[1], w[2] };
                if (!TryCoordinates(coordinates, 1, out int? x, out int? y, out int? z, out error)) return false;
                int first = withZ ? 4 : 3;
                var ranges = new float[2];
                for (int i = 0; i < 2; i++)
                {
                    if (!TryNumber(w[first + i], out float range) || range < 0f || range > MaxEmpRange)
                        return Fail($"a range is a number from 0 to {MaxEmpRange}, got '{w[first + i]}'", out error);
                    ranges[i] = range;
                }
                command = new DevCommand(verb, X: x, Y: y, Z: z, Ranges: ranges);
                return true;
            }
            case DevVerb.Irradiate:
            {
                // X Y [Z] INTENSITY [THRESHOLD]: 3 to 5 numbers; with 4, a last number under 1 is the THRESHOLD (a threshold is under 1:
                // at 1 nothing passes), else it is X Y Z INTENSITY
                usage = "usage: irradiate X Y [Z] INTENSITY [THRESHOLD]";
                int numbers = w.Length - 1;
                if (numbers < 3 || numbers > 5) return Fail(usage, out error);
                bool lastIsThreshold = numbers == 5 || (numbers == 4 && TryNumber(w[4], out float last) && last < 1f);
                bool withZ = numbers - (lastIsThreshold ? 1 : 0) == 4;
                string[] coordinates = withZ ? new[] { w[0], w[1], w[2], w[3] } : new[] { w[0], w[1], w[2] };
                if (!TryCoordinates(coordinates, 1, out int? x, out int? y, out int? z, out error)) return false;
                int first = withZ ? 4 : 3;
                if (!TryNumber(w[first], out float intensity) || intensity < 0f || intensity > MaxIrradiateRange || intensity != MathF.Floor(intensity))
                    return Fail($"INTENSITY is a whole number of tiles from 0 to {MaxIrradiateRange}, got '{w[first]}'", out error);
                float threshold = DefaultIrradiateThreshold;
                if (lastIsThreshold && (!TryNumber(w[first + 1], out threshold) || threshold < 0f || threshold >= 1f))
                    return Fail($"THRESHOLD is a number from 0 to under 1, got '{w[first + 1]}'", out error);
                command = new DevCommand(verb, X: x, Y: y, Z: z, Ranges: new[] { intensity, threshold });
                return true;
            }
            case DevVerb.Zap:
            {
                // X Y [Z] POWER: 3 or 4 numbers, the count tells whether Z is given
                usage = "usage: zap X Y [Z] POWER";
                int numbers = w.Length - 1;
                if (numbers != 3 && numbers != 4) return Fail(usage, out error);
                string[] coordinates = numbers == 4 ? new[] { w[0], w[1], w[2], w[3] } : new[] { w[0], w[1], w[2] };
                if (!TryCoordinates(coordinates, 1, out int? x, out int? y, out int? z, out error)) return false;
                if (!TryNumber(w[numbers], out float power) || power <= 0f || power > MaxZapPower)
                    return Fail($"the power is a number of joules above 0 and at most {MaxZapPower:0}, got '{w[numbers]}'", out error);
                command = new DevCommand(verb, X: x, Y: y, Z: z, Number: power);
                return true;
            }
            case DevVerb.SmCrystal:
            {
                // X Y [Z] status|power|damage [N]: the word after 2 or 3 numbers, N for power and damage
                usage = "usage: smcrystal X Y [Z] status|power|damage [N]";
                int word = Array.FindIndex(w, 1, a => a is "power" or "damage" or "status");
                if (word != 3 && word != 4) return Fail(usage, out error);
                string mode = w[word];
                if (w.Length != word + (mode == "status" ? 1 : 2)) return Fail(usage, out error);
                if (!TryCoordinates(w[0..word], 1, out int? x, out int? y, out int? z, out error)) return false;
                float amount = 0f;
                float max = mode == "power" ? MaxSmCrystalPower : MaxSmCrystalDamage;
                if (mode != "status" && (!TryNumber(w[word + 1], out amount) || amount < 0f || amount > max))
                    return Fail($"N is a number from 0 to {max}, got '{w[word + 1]}'", out error);
                command = new DevCommand(verb, X: x, Y: y, Z: z, Mode: mode, Number: amount);
                return true;
            }
            case DevVerb.SmDelam:
            {
                // X Y [Z] [STRATEGY]: 2 or 3 numbers, then an optional strategy word
                usage = "usage: smdelam X Y [Z] [cascade|singularity|tesla|explosive]";
                string strategy = w.Length > 0 && w[^1] is "cascade" or "singularity" or "tesla" or "explosive" ? w[^1] : null;
                int numbers = w.Length - (strategy == null ? 0 : 1); // w[0] is the verb
                if (numbers != 3 && numbers != 4) return Fail(usage, out error);
                if (!TryCoordinates(w[0..numbers], 1, out int? x, out int? y, out int? z, out error)) return false;
                command = new DevCommand(verb, X: x, Y: y, Z: z, Mode: strategy);
                return true;
            }
            case DevVerb.Singularity:
            {
                // X Y [Z [ENERGY]]: 2 to 4 numbers
                usage = "usage: singularity X Y [Z [ENERGY]]";
                if (w.Length < 3 || w.Length > 5) return Fail(usage, out error);
                string[] coordinates = w.Length >= 4 ? new[] { w[0], w[1], w[2], w[3] } : new[] { w[0], w[1], w[2] };
                if (!TryCoordinates(coordinates, 1, out int? x, out int? y, out int? z, out error)) return false;
                float energy = DefaultSingularityEnergy;
                if (w.Length == 5 && (!TryNumber(w[4], out energy) || energy < 1f || energy > MaxSingularityEnergy))
                    return Fail($"the energy is a number from 1 to {MaxSingularityEnergy}, got '{w[4]}'", out error);
                command = new DevCommand(verb, X: x, Y: y, Z: z, Ranges: new[] { energy });
                return true;
            }
            case DevVerb.Weather:
            {
                // NAME|stop [Z]
                if (w.Length != 2 && w.Length != 3) return Fail("usage: weather NAME|stop [Z]", out error);
                int? level = null;
                if (w.Length == 3)
                {
                    if (!TryNumber(w[2], out float zf) || zf < 1f || zf > Limits.MaxMapLevels)
                        return Fail($"Z is a level from 1 to {Limits.MaxMapLevels}, got '{w[2]}'", out error);
                    level = (int)zf;
                }
                command = new DevCommand(verb, Mode: w[1].ToLowerInvariant(), Z: level);
                return true;
            }
            case DevVerb.LazyLoad:
            {
                // syndicate X1a: [WHO] KEY
                if (w.Length != 2 && w.Length != 3) return Fail("usage: lazyload [WHO] KEY", out error);
                command = new DevCommand(verb, w.Length == 3 ? w[1] : "me", Mode: w[^1]);
                return true;
            }
            case DevVerb.Trauma:
            {
                // syndicate N3b: [WHO] TYPE; the type's kind is DevCommands' check
                if (w.Length != 2 && w.Length != 3) return Fail("usage: trauma [WHO] TYPE", out error);
                if (!IsTypePath(w[^1], "/datum/brain_trauma")) return Fail($"'{w[^1]}' is not a type path (/datum/brain_trauma/...)", out error);
                command = new DevCommand(verb, w.Length == 3 ? w[1] : "me", TypePath: w[^1]);
                return true;
            }
            case DevVerb.Antag:
            {
                // syndicate X1a: [WHO] TYPE; the type's kind is DevCommands' check
                if (w.Length != 2 && w.Length != 3) return Fail("usage: antag [WHO] TYPE", out error);
                if (!IsTypePath(w[^1], "/datum/antagonist")) return Fail($"'{w[^1]}' is not a type path (/datum/antagonist/...)", out error);
                command = new DevCommand(verb, w.Length == 3 ? w[1] : "me", TypePath: w[^1]);
                return true;
            }
            case DevVerb.Outfit:
            case DevVerb.Shoot:
            {
                // M7x slice A: [WHO] TYPE; the type's kind is DevCommands' check
                string name = verb == DevVerb.Outfit ? "outfit" : "shoot";
                if (w.Length != 2 && w.Length != 3) return Fail("usage: " + name + " [WHO] " + (verb == DevVerb.Outfit ? "TYPE" : "TYPE|off"), out error);
                string type = w[^1];
                bool off = verb == DevVerb.Shoot && string.Equals(type, "off", StringComparison.OrdinalIgnoreCase);
                if (!off && !IsTypePath(type, verb == DevVerb.Outfit ? "/datum/outfit" : "/obj/projectile"))
                    return Fail($"'{type}' is not a type path ({(verb == DevVerb.Outfit ? "/datum/outfit" : "/obj/projectile")}/...)", out error);
                command = new DevCommand(verb, w.Length == 3 ? w[1] : "me", TypePath: off ? null : type);
                return true;
            }
            case DevVerb.Give:
            {
                // M4a contracts (D130): the syntax; DevCommands.Give (M4a P5) resolves the type and refuses what is not an /obj/item.
                if (w.Length != 3) return Fail("usage: give WHO TYPE", out error);
                if (!IsItemPath(w[2])) return Fail($"'{w[2]}' is not an item type path (/obj/item/...)", out error);
                command = new DevCommand(verb, w[1], TypePath: w[2]);
                return true;
            }
            case DevVerb.Drop:
            {
                if (w.Length != 3) return Fail("usage: drop WHO SLOT", out error);
                string slot = w[2].ToLowerInvariant();
                if (!Anomaly.Core.Tg.DevCommands.DropSlots.ContainsKey(slot)) return Fail($"'{w[2]}' is not a slot ({string.Join(", ", Anomaly.Core.Tg.DevCommands.DropSlots.Keys)})", out error);
                command = new DevCommand(verb, w[1], Mode: slot);
                return true;
            }
            case DevVerb.Aiize:
            {
                if (w.Length != 2 && w.Length != 3) return Fail("usage: aiize WHO [here]", out error);
                if (w.Length == 3 && w[2].ToLowerInvariant() != "here") return Fail("usage: aiize WHO [here]", out error);
                command = new DevCommand(verb, w[1], Mode: w.Length == 3 ? "here" : "move");
                return true;
            }
            case DevVerb.Gravity:
            {
                if (w.Length != 3 && w.Length != 4) return Fail("usage: gravity WHO on|off [now]", out error);
                string state = w[2].ToLowerInvariant();
                if (state != "on" && state != "off") return Fail("usage: gravity WHO on|off [now]", out error);
                if (w.Length == 4 && w[3].ToLowerInvariant() != "now") return Fail("usage: gravity WHO on|off [now]", out error);
                command = new DevCommand(verb, w[1], Mode: w.Length == 4 ? state + " now" : state);
                return true;
            }
            case DevVerb.AreaPower:
            {
                // X Y [Z] CHANNEL on|off: 5 or 6 words
                usage = "usage: areapower X Y [Z] equip|light|environ|all on|off";
                if (w.Length != 5 && w.Length != 6) return Fail(usage, out error);
                string[] coordinates = w.Length == 6 ? new[] { w[0], w[1], w[2], w[3] } : new[] { w[0], w[1], w[2] };
                if (!TryCoordinates(coordinates, 1, out int? x, out int? y, out int? z, out error)) return false;
                string channel = w[w.Length - 2].ToLowerInvariant();
                string state = w[w.Length - 1].ToLowerInvariant();
                if (channel is not ("equip" or "light" or "environ" or "all")) return Fail($"'{w[w.Length - 2]}' is not a power channel (equip, light, environ, all)", out error);
                if (state is not ("on" or "off")) return Fail(usage, out error);
                command = new DevCommand(verb, X: x, Y: y, Z: z, Mode: channel + " " + state);
                return true;
            }
            case DevVerb.Spawn:
            {
                // tg's spawn panel (spawn_handling.dm:16 spawn_atom): an /obj, /turf or /mob type, an amount, a dir
                usage = "usage: spawn TYPE [X Y [Z [N [DIR]]]]";
                if (w.Length < 2 || w.Length == 3 || w.Length > 7) return Fail(usage, out error);
                if (!IsTypePath(w[1], "/obj", "/mob", "/turf")) return Fail($"'{w[1]}' is not an /obj, /mob or /turf type path", out error);
                int? x = null, y = null, z = null;
                if (w.Length > 2 && !TryCoordinates(w, 2, out x, out y, out z, out error)) return false;
                int count = 1;
                if (w.Length > 5 && (!int.TryParse(w[5], NumberStyles.None, CultureInfo.InvariantCulture, out count) || count < 1 || count > MaxSpawnCount))
                    return Fail($"the amount is 1 to {MaxSpawnCount}, got '{w[5]}'", out error);
                byte dir = 0;
                if (w.Length > 6 && (!byte.TryParse(w[6], NumberStyles.None, CultureInfo.InvariantCulture, out dir) || dir is not (0 or 1 or 2 or 4 or 8 or 5 or 6 or 9 or 10)))
                    return Fail($"the dir is 0 (the type's own) or one of 1, 2, 4, 8 (north, south, east, west), 5, 6, 9 or 10, got '{w[6]}'", out error);
                command = new DevCommand(verb, X: x, Y: y, Z: z, TypePath: w[1], Count: count, Dir: dir);
                return true;
            }
            case DevVerb.Rotate:
            {
                usage = "usage: rotate WHO|ID [cw|ccw|north|south|east|west]";
                if (w.Length is < 2 or > 3) return Fail(usage, out error);
                string turn = w.Length == 3 ? w[2].ToLowerInvariant() : "cw";
                if (turn is not ("cw" or "ccw" or "north" or "south" or "east" or "west")) return Fail(usage, out error);
                command = new DevCommand(verb, w[1], Mode: turn);
                return true;
            }
            case DevVerb.Mount:
            {
                usage = "usage: mount WHO|ID X Y [Z] north|south|east|west";
                if (w.Length is < 5 or > 6) return Fail(usage, out error);
                string side = w[^1].ToLowerInvariant();
                int? level = null;
                if (w.Length == 6)
                {
                    if (!int.TryParse(w[4], NumberStyles.None, CultureInfo.InvariantCulture, out int mz)) return Fail(usage, out error);
                    level = mz;
                }
                if (side is not ("north" or "south" or "east" or "west")
                    || !int.TryParse(w[2], NumberStyles.None, CultureInfo.InvariantCulture, out int mx)
                    || !int.TryParse(w[3], NumberStyles.None, CultureInfo.InvariantCulture, out int my))
                    return Fail(usage, out error);
                command = new DevCommand(verb, w[1], X: mx, Y: my, Z: level, Mode: side);
                return true;
            }
            case DevVerb.Delete:
            {
                usage = "usage: delete WHO|ID | delete X Y [Z]";
                if (w.Length == 2)
                {
                    command = new DevCommand(verb, w[1]);
                    return true;
                }
                if (w.Length != 3 && w.Length != 4) return Fail(usage, out error);
                if (!TryCoordinates(w, 1, out int? x, out int? y, out int? z, out error)) return false;
                command = new DevCommand(verb, X: x, Y: y, Z: z);
                return true;
            }
            case DevVerb.Air:
            {
                usage = "usage: air add GAS MOLES|remove GAS|temp K|set MIX X Y [Z [R]]";
                if (w.Length < 2) return Fail(usage, out error);
                string op = w[1].ToLowerInvariant();
                if (Array.IndexOf(AirOps, op) < 0) return Fail($"'{w[1]}' is not an air operation ({string.Join(", ", AirOps)})", out error);
                int first = op == "add" ? 4 : 3; // the X
                if (w.Length < first + 2 || w.Length > first + 4) return Fail(usage, out error);
                string arg = null;
                float number = 0f;
                switch (op)
                {
                    case "add":
                        if (!IsGasId(w[2])) return Fail($"'{w[2]}' is not a gas id (letters, digits and _)", out error);
                        if (!TryNumber(w[3], out number) || number == 0f || Math.Abs(number) > MaxAirMoles)
                            return Fail(string.Create(CultureInfo.InvariantCulture, $"the moles are from -{MaxAirMoles} to {MaxAirMoles} and not 0, got '{w[3]}'"), out error);
                        arg = w[2].ToLowerInvariant();
                        break;
                    case "remove":
                        if (!IsGasId(w[2])) return Fail($"'{w[2]}' is not a gas id (letters, digits and _)", out error);
                        arg = w[2].ToLowerInvariant();
                        break;
                    case "temp":
                        if (!TryNumber(w[2], out number) || number < Defines.TCMB || number > MaxAirTemperature)
                            return Fail(string.Create(CultureInfo.InvariantCulture, $"the temperature is {Defines.TCMB} to {MaxAirTemperature} K, got '{w[2]}'"), out error);
                        break;
                    case "set":
                        if (!TryMix(w[2], out arg, out error)) return false;
                        break;
                }
                if (!TryCoordinates(w, first, out int? x, out int? y, out int? z, out error)) return false;
                int radius = 0;
                if (w.Length == first + 4 && (!int.TryParse(w[first + 3], NumberStyles.None, CultureInfo.InvariantCulture, out radius) || radius > MaxAirRadius))
                    return Fail($"the radius is 0 to {MaxAirRadius}, got '{w[first + 3]}'", out error);
                command = new DevCommand(verb, Mix: arg, Number: number, X: x, Y: y, Z: z, Mode: op, Radius: radius);
                return true;
            }
            case DevVerb.SpeedScale:
            {
                if (w.Length != 2) return Fail("usage: speedscale F", out error);
                if (!TryNumber(w[1], out float f) || f <= 0f || f > ServerConfig.MaxMoveSpeedScale)
                    return Fail(string.Create(CultureInfo.InvariantCulture, $"the scale must be above 0 and at most {ServerConfig.MaxMoveSpeedScale}, got '{w[1]}'"), out error);
                command = new DevCommand(verb, Number: f);
                return true;
            }
            case DevVerb.Speed:
            {
                if (w.Length != 3) return Fail("usage: speed WHO F", out error);
                if (!TryNumber(w[2], out float f) || f <= 0f || f > ServerConfig.MaxMoveSpeedScale)
                    return Fail(string.Create(CultureInfo.InvariantCulture, $"the speed must be above 0 and at most {ServerConfig.MaxMoveSpeedScale}, got '{w[2]}'"), out error);
                command = new DevCommand(verb, w[1], Number: f);
                return true;
            }
            case DevVerb.ProjectileSpeed:
            {
                if (w.Length != 2) return Fail("usage: projspeed F", out error);
                if (!TryNumber(w[1], out float f) || f <= 0f || f > DevCommands.MaxProjectileSpeedScale)
                    return Fail(string.Create(CultureInfo.InvariantCulture, $"the scale must be above 0 and at most {DevCommands.MaxProjectileSpeedScale}, got '{w[1]}'"), out error);
                command = new DevCommand(verb, Number: f);
                return true;
            }
            case DevVerb.AutoRespawn:
            {
                if (w.Length is < 2 or > 3 || w[1] is not ("on" or "off")) return Fail("usage: autorespawn on|off [SECONDS]", out error);
                float seconds = -1f; // none: the delay stays
                if (w.Length == 3 && (!TryNumber(w[2], out seconds) || seconds < 0f || seconds > MaxAutoRespawnSeconds))
                    return Fail(string.Create(CultureInfo.InvariantCulture, $"the delay is 0 to {MaxAutoRespawnSeconds} seconds, got '{w[2]}'"), out error);
                command = new DevCommand(verb, Mode: w[1], Number: seconds);
                return true;
            }
            case DevVerb.Possess:
            {
                if (w.Length is < 2 or > 3 || (w.Length == 3 && w[2].ToLowerInvariant() != "force")) return Fail("usage: possess WHO [force]", out error);
                command = new DevCommand(verb, w[1], Mode: w.Length == 3 ? "force" : null);
                return true;
            }
            case DevVerb.Unpossess:
                if (w.Length != 1) return Fail("usage: unpossess", out error);
                command = new DevCommand(verb);
                return true;
            case DevVerb.GiveControl:
            {
                usage = "usage: givecontrol WHO PLAYER [delete] [force]";
                if (w.Length is < 3 or > 5) return Fail(usage, out error);
                bool delete = false, force = false;
                for (int i = 3; i < w.Length; i++)
                {
                    string flag = w[i].ToLowerInvariant();
                    if (flag == "delete" && !delete) delete = true;
                    else if (flag == "force" && !force) force = true;
                    else return Fail(usage, out error);
                }
                // Mode: the player to put in control; Count: 1 with delete; Zone: "force" with force
                command = new DevCommand(verb, w[1], Mode: w[2], Count: delete ? 1 : 0, Zone: force ? "force" : null);
                return true;
            }
            case DevVerb.MinimapMobs:
                if (w.Length != 2 || w[1] is not ("on" or "off")) return Fail("usage: minimapmobs on|off", out error);
                command = new DevCommand(verb, Mode: w[1]);
                return true;
            case DevVerb.MoveFeel:
            {
                if (w.Length != 5) return Fail("usage: movefeel ACCEL DECEL BACK STRAFE", out error);
                if (!short.TryParse(w[1], NumberStyles.None, CultureInfo.InvariantCulture, out short accel) || !short.TryParse(w[2], NumberStyles.None, CultureInfo.InvariantCulture, out short decel)
                    || !TryNumber(w[3], out float back) || !TryNumber(w[4], out float strafe) || !new MoveFeel(accel, decel, back, strafe).IsValid)
                    return Fail(string.Create(CultureInfo.InvariantCulture, $"the ramps are whole ticks from 0 to {MoveFeel.MaxTicks}, the slow-downs 0 to {MoveFeel.MaxSlow}"), out error);
                command = new DevCommand(verb, Ranges: new[] { (float)accel, decel, back, strafe });
                return true;
            }
            case DevVerb.Footstride:
            {
                if (w.Length != 3 || w[1] is not ("run" or "walk")) return Fail("usage: footstride run|walk TILES", out error);
                if (!TryNumber(w[2], out float f) || f <= 0f || f > LocomotionSeam.MaxFootstride)
                    return Fail(string.Create(CultureInfo.InvariantCulture, $"the stride must be above 0 and at most {LocomotionSeam.MaxFootstride} tiles, got '{w[2]}'"), out error);
                command = new DevCommand(verb, Number: f, Mode: w[1]);
                return true;
            }
            case DevVerb.NukeCode:
                // syndicate N1a: [CODE], five digits
                if (w.Length > 2) return Fail("usage: nukecode [CODE]", out error);
                if (w.Length == 2 && (w[1].Length != 5 || !System.Linq.Enumerable.All(w[1], char.IsAsciiDigit))) return Fail($"the code is five digits, got '{w[1]}'", out error);
                command = new DevCommand(verb, Mode: w.Length == 2 ? w[1] : null);
                return true;
            case DevVerb.ChangeMap:
                if (w.Length > 2) return Fail("usage: changemap [MAP]", out error);
                command = new DevCommand(verb, Mode: w.Length == 2 ? w[1] : null);
                return true;
            case DevVerb.StartNow:
                if (w.Length != 1) return Fail("usage: startnow", out error);
                command = new DevCommand(verb);
                return true;
            case DevVerb.Tram:
            {
                float platform = 0f;
                if (w.Length > 2 || (w.Length == 2 && (!TryNumber(w[1], out platform) || platform < 1f || platform > 99f || platform != MathF.Floor(platform))))
                    return Fail("usage: tram [PLATFORM] (a platform's number, 1 to 99)", out error);
                command = new DevCommand(verb, Number: platform);
                return true;
            }
            case DevVerb.Reboot:
            {
                if (w.Length == 2 && w[1].Equals("now", StringComparison.OrdinalIgnoreCase))
                {
                    command = new DevCommand(verb, Mode: "now"); // tg's hard restart
                    return true;
                }
                float seconds = 1f; // tg's regular restart
                if (w.Length > 2 || (w.Length == 2 && (!TryNumber(w[1], out seconds) || seconds < 1f || seconds > 600f)))
                    return Fail("usage: reboot [SECONDS] (1 to 600) | reboot now", out error);
                command = new DevCommand(verb, Number: seconds);
                return true;
            }
            case DevVerb.Shuttle:
            {
                string mode = string.Join(' ', w, 1, w.Length - 1).ToLowerInvariant();
                if (w.Length == 3 && w[1].Equals("time", StringComparison.OrdinalIgnoreCase) && int.TryParse(w[2], out int seconds) && seconds > 0)
                {
                    command = new DevCommand(verb, Mode: "time", X: seconds);
                    return true;
                }
                if (mode != "call" && mode != "call norecall" && mode != "recall" && mode != "dock") return Fail("usage: shuttle call [norecall]|recall|dock|time SECONDS", out error);
                command = new DevCommand(verb, Mode: mode);
                return true;
            }
        }
        return Fail("unknown dev command", out error);
    }

    // A status effect's name: a type path (full or below /datum/status_effect/) or an id: letters, digits, '_' and '/'.
    private static bool IsEffectName(string word)
    {
        if (word.Length == 0 || word.Length > Limits.MaxTypePathBytes) return false;
        foreach (char c in word)
            if (!(char.IsAsciiLetterOrDigit(c) || c is '_' or '/')) return false;
        return true;
    }

    // M8 slice A: a reagent's name as get_chem_id compares it (ckey: letters and digits; '_' for a space): at most 64.
    private static bool IsReagentName(string word)
    {
        if (word.Length == 0 || word.Length > 64) return false;
        foreach (char c in word)
            if (!(char.IsAsciiLetterOrDigit(c) || c == '_')) return false;
        return true;
    }

    // M4a: a DM type path under /obj/item: "/obj/item" or "/obj/item/" and segments of letters, digits and '_'.
    private static bool IsItemPath(string word) => IsTypePath(word, "/obj/item");

    // A gas id of tg's (GAS_PLASMA "plasma", GAS_WATER_VAPOR "water_vapor"): letters, digits and '_', at most 32.
    private static bool IsGasId(string word)
    {
        if (word.Length == 0 || word.Length > 32) return false;
        foreach (char c in word)
            if (!(char.IsAsciiLetterOrDigit(c) || c == '_')) return false;
        return true;
    }

    // A DM type path under one of the roots ("/obj" or "/obj/..."), its segments of letters, digits and '_'.
    private static bool IsTypePath(string word, params string[] roots)
    {
        if (word.Length > Limits.MaxTypePathBytes) return false;
        bool under = false;
        foreach (string root in roots)
            under |= word == root || word.StartsWith(root + "/", StringComparison.Ordinal);
        if (!under)
            return false;
        foreach (string segment in word.Substring(1).Split('/'))
        {
            if (segment.Length == 0) return false;
            foreach (char c in segment)
                if (!(c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_')) return false;
        }
        return true;
    }

    private static bool Fail(string text, out string error)
    {
        error = text;
        return false;
    }

    private static bool TryVerb(string word, out DevVerb verb)
    {
        switch (word.ToLowerInvariant())
        {
            case "damage": verb = DevVerb.Damage; return true;
            case "heal": verb = DevVerb.Heal; return true;
            case "fullheal": verb = DevVerb.FullHeal; return true;
            case "kill": verb = DevVerb.Kill; return true;
            case "revive": verb = DevVerb.Revive; return true;
            case "knockdown": verb = DevVerb.Knockdown; return true;
            case "stun": verb = DevVerb.Stun; return true;
            case "bleed": verb = DevVerb.Bleed; return true;
            case "blood": verb = DevVerb.Blood; return true;
            case "dismember": verb = DevVerb.Dismember; return true;
            case "wound": verb = DevVerb.Wound; return true;
            case "gib": verb = DevVerb.Gib; return true;
            case "gas": verb = DevVerb.Gas; return true;
            case "analyze": verb = DevVerb.Analyze; return true;
            case "spawn": verb = DevVerb.Spawn; return true;
            case "speedscale": verb = DevVerb.SpeedScale; return true;
            case "speed": verb = DevVerb.Speed; return true;
            case "projspeed": verb = DevVerb.ProjectileSpeed; return true;
            case "footstride": verb = DevVerb.Footstride; return true;
            case "movefeel": verb = DevVerb.MoveFeel; return true;
            case "minimapmobs": verb = DevVerb.MinimapMobs; return true;
            case "autorespawn": verb = DevVerb.AutoRespawn; return true;
            case "possess": verb = DevVerb.Possess; return true;
            case "unpossess": verb = DevVerb.Unpossess; return true;
            case "givecontrol": verb = DevVerb.GiveControl; return true;
            case "godmode": verb = DevVerb.GodMode; return true;
            case "pilot": verb = DevVerb.Pilot; return true;
            case "fly": verb = DevVerb.Fly; return true;
            case "noclip": verb = DevVerb.NoClip; return true;
            case "invuln": verb = DevVerb.Invulnerable; return true;
            case "esp": verb = DevVerb.Esp; return true;
            case "invisimin": verb = DevVerb.Invisimin; return true;
            case "god": verb = DevVerb.God; return true;
            case "up": verb = DevVerb.Up; return true;
            case "down": verb = DevVerb.Down; return true;
            case "startnow": verb = DevVerb.StartNow; return true;
            case "tram": verb = DevVerb.Tram; return true;
            case "nukecode": verb = DevVerb.NukeCode; return true;
            case "shuttle": verb = DevVerb.Shuttle; return true;
            case "reboot": verb = DevVerb.Reboot; return true;
            case "changemap": verb = DevVerb.ChangeMap; return true;
            case "give": verb = DevVerb.Give; return true;
            case "outfit": verb = DevVerb.Outfit; return true;
            case "antag": verb = DevVerb.Antag; return true;
            case "trauma": verb = DevVerb.Trauma; return true;
            case "robotize": verb = DevVerb.Robotize; return true;
            case "aiize": verb = DevVerb.Aiize; return true;
            case "paiize": verb = DevVerb.Paiize; return true;
            case "lazyload": verb = DevVerb.LazyLoad; return true;
            case "shoot": verb = DevVerb.Shoot; return true;
            case "drop": verb = DevVerb.Drop; return true;
            case "gravity": verb = DevVerb.Gravity; return true;
            case "breach": verb = DevVerb.Breach; return true;
            case "patch": verb = DevVerb.Patch; return true;
            case "valve": verb = DevVerb.Valve; return true;
            case "airmode": verb = DevVerb.AirMode; return true;
            case "areapower": verb = DevVerb.AreaPower; return true;
            case "ignite": verb = DevVerb.Ignite; return true;
            case "explode": verb = DevVerb.Explode; return true;
            case "emp": verb = DevVerb.Emp; return true;
            case "recharge": verb = DevVerb.Recharge; return true;
            case "irradiate": verb = DevVerb.Irradiate; return true;
            case "zap": verb = DevVerb.Zap; return true;
            case "smcrystal": verb = DevVerb.SmCrystal; return true;
            case "smdelam": verb = DevVerb.SmDelam; return true;
            case "singularity": verb = DevVerb.Singularity; return true;
            case "weather": verb = DevVerb.Weather; return true;
            case "delete": case "del": verb = DevVerb.Delete; return true;
            case "ceiling": verb = DevVerb.Ceiling; return true;
            case "rotate": case "rot": verb = DevVerb.Rotate; return true;
            case "mount": verb = DevVerb.Mount; return true;
            case "mobinfo": verb = DevVerb.MobInfo; return true;
            case "air": verb = DevVerb.Air; return true;
            case "effects": verb = DevVerb.Effects; return true;
            case "effect": verb = DevVerb.Effect; return true;
            case "reagent": verb = DevVerb.Reagent; return true;
            case "reagents": verb = DevVerb.Reagents; return true;
            case "heart": verb = DevVerb.Heart; return true;
            case "breath": verb = DevVerb.Breath; return true;
            case "hallucinate": verb = DevVerb.Hallucinate; return true;
            default: verb = default; return false;
        }
    }

    private static bool TryType(string word, out DevDamageType type)
    {
        switch (word.ToLowerInvariant())
        {
            case "brute": type = DevDamageType.Brute; return true;
            case "burn": case "fire": type = DevDamageType.Burn; return true;
            case "tox": case "toxin": type = DevDamageType.Tox; return true;
            case "oxy": case "oxygen": type = DevDamageType.Oxy; return true;
            default: type = default; return false;
        }
    }

    private static bool TryZone(string word, out string zone, out string error)
    {
        zone = null;
        error = null;
        string z = word.ToLowerInvariant();
        foreach (string known in Zones)
        {
            if (known == z)
            {
                zone = known;
                return true;
            }
        }
        error = $"'{word}' is not a body zone ({string.Join(", ", Zones)})";
        return false;
    }

    private static bool TryNumber(string word, out float value)
        => float.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && float.IsFinite(value);

    // A preset name, or a gas string of tg's parse_gas_string form ("o2=22;n2=82;TEMP=293.15"): letters, digits and "_=;.-" only.
    private static bool TryMix(string word, out string mix, out string error)
    {
        error = null;
        switch (word.ToLowerInvariant())
        {
            case "air": mix = Defines.OPENTURF_DEFAULT_ATMOS; return true;
            case "vacuum": case "space": case "airless": mix = Defines.AIRLESS_ATMOS; return true;
        }
        mix = word;
        if (word.Length > MaxMixChars) { error = $"the gas string is longer than {MaxMixChars} characters"; return false; }
        foreach (char c in word)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c is '_' or '=' or ';' or '.' or '-'))
            {
                error = $"the gas string may hold only letters, digits and _ = ; . - (got '{c}')";
                return false;
            }
        }
        return true;
    }

    private static bool TryCoordinates(string[] w, int first, out int? x, out int? y, out int? z, out string error)
    {
        x = y = z = null;
        error = null;
        if (!Coordinate(w[first], out int cx) || !Coordinate(w[first + 1], out int cy)) return Fail($"'{w[first]} {w[first + 1]}' is not a map position (1 to {Limits.MaxMapSize})", out error);
        int cz = 1;
        if (w.Length > first + 2 && !int.TryParse(w[first + 2], NumberStyles.None, CultureInfo.InvariantCulture, out cz)) return Fail($"'{w[first + 2]}' is not a z level", out error);
        if (cz < 1 || cz > Limits.MaxMapLevels) return Fail($"the z level is 1 to {Limits.MaxMapLevels}", out error);
        x = cx;
        y = cy;
        z = cz;
        return true;
    }

    private static bool Coordinate(string word, out int value)
        => int.TryParse(word, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= 1 && value <= Limits.MaxMapSize;
}
