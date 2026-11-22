using System.Linq;
using System.Globalization;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server;

// The dev commands (scope 2k, D73, Q9): the console types them (always allowed), and a player's dev console sends them as
// ActionKind.DevCommand when DEV_COMMANDS is on. Each runs in the command phase with usr the requester's mob and calls tg's own
// proc through Anomaly.Core.Tg.DevCommands; the reply goes to the requester only (the console's output, or the session's
// ServerText), and tg's log_admin records who used it.
public sealed partial class ServerHost
{
    /// <summary>The most mobs players may spawn from their dev consoles over a run (the console's own spawn is not capped).</summary>
    public const int MaxPlayerDevSpawns = 100;

    private int _devSpawns;

    // From a player: already parsed and rate-limited on the network side, so this is the world half. A line that is no dev
    // command is a console command the network side let through (CLIENT_CONSOLE, 1.6b); the choice depends on the line only,
    // so a replay applies the recorded action the same way without the option.
    private void ApplyDevAction(Session s, string text)
    {
        mob requester = MobOf(s);
        if (!DevCommandLine.TryParse(text, out DevCommand dev, out string error))
        {
            if (ConsoleCommand.TryParse(text, out ConsoleCommand command, out _) && command.IsWorldCommand && command.Verb is not (ConsoleVerb.Dev or ConsoleVerb.Quit))
            {
                ApplyConsoleFrom(s, command);
                return;
            }
            ReplyTo(s, error);
            return;
        }
        if (dev.Verb == DevVerb.MobInfo)
        {
            SendMobInfo(s, dev, requester);
            return;
        }
        if (dev.Verb == DevVerb.MinimapMobs)
        {
            s.MinimapMobs = dev.Mode == "on"; // D315: a view of this session's, nothing in the world
            ReplyTo(s, s.MinimapMobs ? "Your minimap shows the living mobs of your level." : "Your minimap shows no mobs.");
            return;
        }
        string reply = RunDev(dev, text, requester);
        if (dev.Verb is DevVerb.Effects or DevVerb.Reagents && reply != null)
            ReplyLines(s, reply); // a list: a line per message, as the help
        else
            ReplyTo(s, reply);
    }

    /// <summary>The ServerText type of a <c>mobinfo</c> answer (the dev menu's player panel takes it, not the chat; D140).</summary>
    public const string MobInfoTextType = "dev_mob";

    // D140: mobinfo, read only and asked twice a second by the dev menu: no admin log line. The answer is one ServerText of
    // the type above: "section<TAB>key<TAB>value" lines, the online players first (section "players", key "#id").
    private void SendMobInfo(Session s, in DevCommand dev, mob requester)
    {
        mob_living target = ResolveTarget(dev.Who, requester, out string problem);
        var text = new System.Text.StringBuilder();
        foreach (Session other in _sessions)
        {
            if (!other.InWorld || MobOf(other) is not mob m) continue;
            text.Append("players\t#").Append(m.dm_id.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(Clean(other.Key)).Append(m is mob_dead_observer ? " (ghost)" : "").Append('\n');
        }
        // DV580: the server's auto-respawn, for the panel's switch (the world's, whoever the target is)
        text.Append("Respawn\tauto\t").Append(_autoRespawn ? "on" : "off").Append('\n');
        text.Append("Respawn\tdelay\t").Append(_autoRespawnSeconds.ToString("0.#", CultureInfo.InvariantCulture)).Append('\n');
        AppendPossessRows(text, s, requester); // D346: the way back and the mobs round the requester
        if (target == null && (string.Equals(dev.Who, "me", StringComparison.OrdinalIgnoreCase) ? requester : FindMovable(dev.Who, out _)) is mob_dead_observer ghost)
        {
            // the owner (2026-11-02): a ghost without a body still gets its commands (revive, the teleports)
            text.Append("Ghost\tbody\tnone\n");
            text.Append("Ghost\tname\t").Append(Clean(Dm.str(ghost.real_name))).Append('\n');
        }
        else if (target == null)
            text.Append("error\t\t").Append(Clean(problem)).Append('\n');
        else
        {
            try
            {
                foreach (DevCommands.InfoRow row in DevCommands.MobInfo(target))
                    text.Append(Clean(row.Section)).Append('\t').Append(Clean(row.Key)).Append('\t').Append(Clean(row.Value)).Append('\n');
            }
            catch (Exception e)
            {
                World.ReportRuntime(e);
                text.Append("error\t\t").Append(Clean("mobinfo failed: " + e.GetType().Name)).Append('\n');
            }
        }
        s.Outbox.Enqueue(new ServerText(MobInfoTextType, Truncate(System.Net.WebUtility.HtmlEncode(text.ToString()), Limits.MaxServerTextBytes)));
    }

    // A field of a mobinfo line: no tabs or line breaks, at most 1000 characters.
    private static string Clean(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        string flat = value.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
        return flat.Length > 1000 ? flat.Substring(0, 1000) + "..." : flat;
    }

    private static void ReplyLines(Session s, string text)
    {
        foreach (string line in text.Split('\n'))
            ReplyTo(s, line); // a line per message: the console and the chat show each, and none hits MaxServerTextBytes
    }

    // From the console: a recorded Console command, requester none.
    private void ApplyDevConsole(in ConsoleCommand command)
    {
        if (!DevCommandLine.TryParse(command.Line, out DevCommand dev, out string error))
        {
            _out.WriteLine("console: " + error);
            return;
        }
        string reply = RunDev(dev, command.Line, null);
        if (reply != null) _out.WriteLine(reply);
    }

    private string RunDev(DevCommand dev, string line, mob requester)
    {
        string who = requester != null ? Dm.str(key_name(requester)) : ConsoleName;
        try
        {
            string reply = RunDevVerb(dev, requester);
            log_admin(Dm.text("[] used the dev command: []", who, line));
            return reply;
        }
        catch (Exception e)
        {
            World.ReportRuntime(e); // a failing proc is a runtime of the command, as for every command; the requester hears of it
            return "dev command failed: " + e.GetType().Name + ": " + e.Message;
        }
    }

    private string RunDevVerb(DevCommand dev, mob requester)
    {
        switch (dev.Verb)
        {
            case DevVerb.Ceiling:
                return EditCeilings(dev);
            case DevVerb.MobInfo:
            {
                // from the console: the rows as lines
                mob_living info = ResolveTarget(dev.Who, requester, out string missing);
                if (info == null) return missing;
                return string.Join("\n", DevCommands.MobInfo(info).Select(r => r.Section + " | " + r.Key + ": " + r.Value));
            }
            case DevVerb.Possess:
                return Possess(dev, requester);
            case DevVerb.Unpossess:
                return Unpossess(requester);
            case DevVerb.GiveControl:
                return GiveControl(dev, requester);
            case DevVerb.Effect when dev.Mode == "types":
                return DevCommands.EffectTypes();
            case DevVerb.SpeedScale:
                _locomotion.SetSpeedScale(World, dev.Number); // the host's own seam (DevCommands.SpeedScale is the same call on the real one)
                return string.Create(CultureInfo.InvariantCulture, $"capsule speed scale is now {dev.Number:0.###}");
            case DevVerb.ProjectileSpeed:
                return DevCommands.ProjectileSpeed(dev.Number);
            case DevVerb.Footstride:
                return DevCommands.Footstride(dev.Mode == "walk", dev.Number);
            case DevVerb.MinimapMobs:
                return "minimapmobs is a player's minimap view: the console has none";
            case DevVerb.AutoRespawn:
                return SetAutoRespawn(dev);
            case DevVerb.MoveFeel:
            {
                var feel = new Anomaly.Core.Motion.MoveFeel((short)dev.Ranges[0], (short)dev.Ranges[1], dev.Ranges[2], dev.Ranges[3]);
                _locomotion.SetFeel(World, feel);
                return string.Create(CultureInfo.InvariantCulture, $"movement feel: full speed in {feel.AccelTicks} ticks, a stop in {feel.DecelTicks}, back x{1f - feel.BackSlow:0.##}, sideways x{1f - feel.StrafeSlow:0.##}");
            }
            case DevVerb.Gas:
                return DevCommands.Gas(dev.Mix, dev.X, dev.Y, dev.Z);
            case DevVerb.StartNow:
                return DevCommands.StartNow();
            case DevVerb.Tram:
                return DevCommands.Tram((int)dev.Number);
            case DevVerb.NukeCode:
                return DevCommands.NukeCode(dev.Mode);
            case DevVerb.Shuttle:
                return dev.Mode == "time" ? DevCommands.ShuttleTime(dev.X.Value) : DevCommands.Shuttle(dev.Mode, requester);
            case DevVerb.Reboot:
                return DevCommands.Reboot(dev.Number, requester, dev.Mode == "now");
            case DevVerb.ChangeMap:
                return ChangeMap(dev.Mode, requester);
            case DevVerb.Breach:
            case DevVerb.Patch:
            case DevVerb.Valve:
            case DevVerb.Ignite:
            {
                string name = dev.Verb == DevVerb.Breach ? "breach" : dev.Verb == DevVerb.Patch ? "patch" : dev.Verb == DevVerb.Valve ? "valve" : "ignite";
                turf spot;
                mob_living who = null;
                if (dev.X.HasValue && dev.Y.HasValue)
                {
                    spot = locate(dev.X.Value, dev.Y.Value, dev.Z ?? 1);
                    if (spot == null) return name + ": invalid coordinates";
                }
                else
                {
                    who = ResolveTarget(dev.Who, requester, out string missing);
                    if (who == null) return missing;
                    spot = DevCommands.FacedTurf(who);
                    if (spot == null) return name + ": " + Dm.str(who.name) + " faces the edge of the map";
                }
                return dev.Verb == DevVerb.Breach ? DevCommands.Breach(spot)
                    : dev.Verb == DevVerb.Patch ? DevCommands.Patch(spot)
                    : dev.Verb == DevVerb.Ignite ? DevCommands.Ignite(spot)
                    : DevCommands.Valve(spot, who ?? requester);
            }
            case DevVerb.AreaPower:
            {
                turf spot = locate(dev.X.Value, dev.Y.Value, dev.Z ?? 1);
                if (spot == null) return "areapower: invalid coordinates";
                string[] words = dev.Mode.Split(' ');
                return DevCommands.AreaPower(spot, words[0], words[1] == "on");
            }
            case DevVerb.Singularity:
            {
                turf spot = locate(dev.X.Value, dev.Y.Value, dev.Z ?? 1);
                if (spot == null) return "singularity: invalid coordinates";
                return DevCommands.Singularity(spot, dev.Ranges[0]);
            }
            case DevVerb.Weather:
            {
                int level = dev.Z ?? (requester != null && get_turf(requester) is turf here ? (int)here.z : 1);
                return DevCommands.Weather(dev.Mode, level);
            }
            case DevVerb.Explode:
            {
                turf spot = locate(dev.X.Value, dev.Y.Value, dev.Z ?? 1);
                if (spot == null) return "explode: invalid coordinates";
                float[] r = dev.Ranges;
                return DevCommands.Explode(spot, r[0], r[1], r[2], float.IsNaN(r[3]) ? null : r[3], float.IsNaN(r[4]) ? null : r[4], requester);
            }
            case DevVerb.Emp:
            {
                turf spot = locate(dev.X.Value, dev.Y.Value, dev.Z ?? 1);
                if (spot == null) return "emp: invalid coordinates";
                return DevCommands.Emp(spot, dev.Ranges[0], dev.Ranges[1]);
            }
            case DevVerb.Irradiate:
            {
                turf spot = locate(dev.X.Value, dev.Y.Value, dev.Z ?? 1);
                if (spot == null) return "irradiate: invalid coordinates";
                return DevCommands.Irradiate(spot, dev.Ranges[0], dev.Ranges[1]);
            }
            case DevVerb.Recharge:
            {
                if (dev.Mode == "all")
                    return DevCommands.Recharge(null);
                turf spot = locate(dev.X.Value, dev.Y.Value, dev.Z ?? 1);
                if (spot == null) return "recharge: invalid coordinates";
                return DevCommands.Recharge(spot);
            }
            case DevVerb.Zap:
            {
                turf spot = locate(dev.X.Value, dev.Y.Value, dev.Z ?? 1);
                if (spot == null) return "zap: invalid coordinates";
                return DevCommands.Zap(spot, dev.Number);
            }
            case DevVerb.SmCrystal:
            {
                turf spot = locate(dev.X.Value, dev.Y.Value, dev.Z ?? 1);
                if (spot == null) return "smcrystal: invalid coordinates";
                return DevCommands.SmCrystal(spot, dev.Mode, dev.Number);
            }
            case DevVerb.SmDelam:
            {
                turf spot = locate(dev.X.Value, dev.Y.Value, dev.Z ?? 1);
                if (spot == null) return "smdelam: invalid coordinates";
                return DevCommands.SmDelam(spot, dev.Mode);
            }
            case DevVerb.AirMode:
            {
                mob_living who = ResolveTarget(dev.Who, requester, out string missing);
                if (who == null) return missing;
                return DevCommands.AirMode(who, dev.Mode);
            }
            case DevVerb.Spawn:
            {
                // tg's spawn panel (spawn_handling.dm:16). A player's dev console may fill the room with mobs, each with a
                // body of organs: their number is capped (the console's own spawn is not). The count is derived from the
                // applied commands, so a replay keeps it.
                Type type = DmTypes.FromPath(dev.TypePath);
                if (type == null || !(typeof(obj).IsAssignableFrom(type) || typeof(mob).IsAssignableFrom(type) || typeof(turf).IsAssignableFrom(type)))
                    return $"spawn: tg has no /obj, /mob or /turf type {dev.TypePath}; nothing created";
                bool mobs = typeof(mob).IsAssignableFrom(type);
                if (mobs && requester != null && _devSpawns + dev.Count > MaxPlayerDevSpawns)
                    return $"spawn: the limit of {MaxPlayerDevSpawns} mobs spawned from dev consoles is reached ({_devSpawns} so far)";
                turf where;
                if (dev.X.HasValue && dev.Y.HasValue)
                {
                    where = locate(dev.X.Value, dev.Y.Value, dev.Z ?? 1);
                }
                else if (requester != null)
                {
                    where = get_turf(requester) as turf; // tg's "Current location"
                }
                else
                {
                    SpawnPoint p = SpawnPoints[0];
                    where = locate(p.X, p.Y, p.Z);
                }
                if (where == null) return "spawn: invalid coordinates";
                List<atom> made = DevCommands.Spawn(type, where, dev.Count, dev.Dir, out string line);
                foreach (atom a in made)
                {
                    if (a is mob) _devSpawns += requester != null ? 1 : 0;
                    // a living mob is a capsule like a player's: a dummy with no client and no frames stands still, solid to the players' capsules (D105)
                    if (a is mob_living living && !_locomotion.IsRegistered(living))
                        _locomotion.Register(living);
                }
                return line;
            }
            case DevVerb.Rotate:
            {
                atom_movable thing = string.Equals(dev.Who, "me", StringComparison.OrdinalIgnoreCase) ? requester : FindMovable(dev.Who, out _);
                if (thing == null) return $"rotate: no player or thing '{dev.Who}'";
                int now = (int)Dm.num(thing.dir);
                int dir = dev.Mode switch
                {
                    "north" => (int)Dm.NORTH, "south" => (int)Dm.SOUTH, "east" => (int)Dm.EAST, "west" => (int)Dm.WEST,
                    _ => DevTurns.Turn(now, dev.Mode != "ccw"),
                };
                Waiting.NoSleep(thing.setDir((float)dir));
                return string.Create(CultureInfo.InvariantCulture, $"rotate: {Dm.str(thing.name)} #{thing.dm_id} faces {DirWord(dir)}");
            }
            case DevVerb.Mount:
            {
                if (FindMovable(dev.Who, out _) is not obj thing || thing.loc is not turf from)
                    return $"mount: no thing on the map '{dev.Who}'";
                // Its terminal stays under the old tile and its area is what it powers.
                if (thing is obj_machinery_power_apc)
                    return "mount: an APC is tied to its terminal and its area and is not moved";
                int z = dev.Z ?? (int)Dm.num(thing.z);
                (int dx, int dy, int face, int back) = dev.Mode switch
                {
                    "north" => (0, 1, (int)Dm.NORTH, (int)Dm.SOUTH),
                    "south" => (0, -1, (int)Dm.SOUTH, (int)Dm.NORTH),
                    "east" => (1, 0, (int)Dm.EAST, (int)Dm.WEST),
                    _ => (-1, 0, (int)Dm.WEST, (int)Dm.EAST),
                };
                if (locate(dev.X.Value, dev.Y.Value, z) is not turf_closed wall)
                    return $"mount: ({dev.X},{dev.Y},{z}) is not a wall";
                if (locate(dev.X.Value + dx, dev.Y.Value + dy, z) is not turf floor || floor is turf_closed)
                    return $"mount: the {dev.Mode} side of ({dev.X},{dev.Y},{z}) is not open";
                // As it hung before: in the wall cell, facing out; or on the floor cell, its dir and its offset toward the wall (tg's directional helpers).
                bool inWall = from is turf_closed;
                int reach = (int)Math.Max(Math.Abs(thing.pixel_x), Math.Abs(thing.pixel_y));
                if (inWall || reach < 16)
                    reach = 0;
                turf to = inWall ? wall : floor;
                int dir = inWall ? face : back;
                int pixelX = -dx * reach, pixelY = -dy * reach;
                DevTurns.Mount(thing, to, pixelX, pixelY, dir);
                return string.Create(CultureInfo.InvariantCulture, $"mount: {Dm.str(thing.name)} #{thing.dm_id} hangs on the {dev.Mode} side of ({dev.X},{dev.Y},{z})");
            }
            case DevVerb.Delete:
            {
                atom victim;
                if (dev.X.HasValue && dev.Y.HasValue)
                {
                    victim = locate(dev.X.Value, dev.Y.Value, dev.Z ?? 1);
                    if (victim == null) return "delete: invalid coordinates";
                }
                else
                {
                    atom_movable found = string.Equals(dev.Who, "me", StringComparison.OrdinalIgnoreCase) ? requester : FindMovable(dev.Who, out _);
                    if (found == null) return $"delete: no player or thing '{dev.Who}'";
                    if (found is mob m && (m.client != null || m == requester || IsPlayersMob(m)))
                        return $"delete: {Dm.str(m.name)} is a player's mob";
                    victim = found;
                }
                return DevCommands.Delete(victim);
            }
            case DevVerb.Air:
            {
                turf centre = locate(dev.X.Value, dev.Y.Value, dev.Z ?? 1);
                if (centre == null) return "air: invalid coordinates";
                return DevCommands.Air(dev.Mode, dev.Mix, dev.Number, centre, dev.Radius);
            }
        }

        if (dev.Verb == DevVerb.Revive && (string.Equals(dev.Who, "me", StringComparison.OrdinalIgnoreCase) ? requester : FindMovable(dev.Who, out _)) is mob_dead_observer ghost)
            return DevCommands.ReviveGhost(ghost);
        mob_living target = ResolveTarget(dev.Who, requester, out string problem);
        if (target == null) return problem;
        switch (dev.Verb)
        {
            case DevVerb.Damage: return DevCommands.Damage(target, dev.Type.GetValueOrDefault(), dev.Number, dev.Zone);
            case DevVerb.Heal: return DevCommands.Heal(target, dev.Type);
            case DevVerb.FullHeal: return DevCommands.FullHeal(target);
            case DevVerb.Kill: return DevCommands.Kill(target);
            case DevVerb.Revive: return DevCommands.Revive(target);
            case DevVerb.Knockdown: return DevCommands.Knockdown(target, dev.Number);
            case DevVerb.Stun: return DevCommands.Stun(target, dev.Number);
            case DevVerb.Blood: return DevCommands.Blood(target, dev.Number);
            case DevVerb.Speed: return DevCommands.Speed(target, dev.Number);
            case DevVerb.GodMode: return DevCommands.GodMode(target);
            case DevVerb.Pilot: return DevCommands.Pilot(target);
            case DevVerb.Fly: return DevCommands.Fly(target);
            case DevVerb.NoClip: return DevCommands.NoClip(target);
            case DevVerb.Invulnerable: return DevCommands.Invulnerable(target);
            case DevVerb.Esp: return DevCommands.Esp(target);
            case DevVerb.Invisimin: return DevCommands.Invisimin(target);
            case DevVerb.God: return DevCommands.God(target);
            case DevVerb.Up: return DevCommands.Level(target, up: true);
            case DevVerb.Down: return DevCommands.Level(target, up: false);
            case DevVerb.Analyze: return DevCommands.Analyze(requester, target);
            case DevVerb.Give: return DevCommands.Give(target, dev.TypePath);
            case DevVerb.Outfit: return DevCommands.Outfit(target, dev.TypePath);
            case DevVerb.Antag: return DevCommands.Antag(target, dev.TypePath);
            case DevVerb.Trauma: return DevCommands.Trauma(target, dev.TypePath);
            case DevVerb.Robotize: return DevCommands.Robotize(target);
            case DevVerb.Aiize: return DevCommands.Aiize(target, dev.Mode != "here");
            case DevVerb.Paiize: return DevCommands.Paiize(target);
            case DevVerb.LazyLoad: return DevCommands.LazyLoad(target, dev.Mode);
            case DevVerb.Shoot: return DevCommands.Shoot(target, dev.TypePath);
            case DevVerb.Drop: return DevCommands.Drop(target, dev.Mode);
            case DevVerb.Gravity: return DevCommands.Gravity(target, dev.Mode.StartsWith("on", StringComparison.Ordinal), dev.Mode.EndsWith(" now", StringComparison.Ordinal));
            case DevVerb.Effects: return DevCommands.Effects(target);
            case DevVerb.Reagents: return DevCommands.Reagents(target);
            case DevVerb.Heart: return DevCommands.Heart(target, dev.Mode == "stop");
            case DevVerb.Breath: return DevCommands.Breath(target, dev.Number);
            case DevVerb.Hallucinate: return DevCommands.Hallucinate(target, dev.TypePath, dev.Number);
            case DevVerb.Reagent:
            {
                Type reagent = DevCommands.FindReagent(dev.TypePath, out string unknown);
                return reagent == null ? unknown : DevCommands.AddReagent(target, reagent, dev.Number);
            }
            case DevVerb.Effect:
            {
                Type effect = DevCommands.FindStatusEffect(dev.TypePath, out string unknown);
                if (effect == null) return unknown;
                return dev.Mode == "add"
                    ? DevCommands.AddEffect(target, effect, float.IsNaN(dev.Number) ? null : dev.Number)
                    : DevCommands.RemoveEffect(target, effect);
            }
            case DevVerb.Bleed:
                return target is mob_living_carbon carbon
                    ? DevCommands.Bleed(carbon, dev.Zone, dev.Number)
                    : "bleed: " + Dm.str(target.name) + " is not a carbon (it has no bodyparts)";
            case DevVerb.Dismember:
                return target is mob_living_carbon limbed
                    ? DevCommands.Dismember(limbed, dev.Zone)
                    : "dismember: " + Dm.str(target.name) + " is not a carbon (it has no bodyparts)";
            case DevVerb.Wound:
                return target is mob_living_carbon wounded
                    ? DevCommands.Wound(wounded, dev.Zone, dev.Mode, (int)dev.Number)
                    : "wound: " + Dm.str(target.name) + " is not a carbon (it has no bodyparts)";
            case DevVerb.Gib:
                return DevCommands.Gib(target, dev.Mode == "brain");
            default:
                return "unknown dev command";
        }
    }

    // WHO: me (the requester's mob), a player's key (ckey match), or a world id; it must be a living mob.
    private mob_living ResolveTarget(string who, mob requester, out string problem)
    {
        problem = null;
        atom_movable found;
        if (string.Equals(who, "me", StringComparison.OrdinalIgnoreCase))
        {
            if (requester == null)
            {
                problem = "the console has no mob; name a player or a world id";
                return null;
            }
            found = requester;
        }
        else
        {
            found = FindMovable(who, out _);
        }
        if (found is mob_living living) return living;
        // the owner (2026-11-02): a ghost's numbers and commands are its body's, while it has one
        if (found is mob_dead_observer ghost && ghost.mind?.current is mob_living body && !QDELETED(body)) return body;
        problem = found == null ? $"no player or mob '{who}'" : found is mob_dead_observer ? $"'{who}' is a ghost without a body" : $"'{who}' is not a living mob";
        return null;
    }

    // A mob some session plays (connected or not: a player who left keeps the body), never deleted by a dev command.
    private bool IsPlayersMob(mob m)
    {
        foreach (Session s in _sessions)
        {
            if (ReferenceEquals(MobOf(s), m)) return true;
        }
        return false;
    }

    // A reply to a player: ServerText on its Control channel once it is Joined (HTML-encoded: the line is plain text).
    private static void ReplyTo(Session s, string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        s.Outbox.Enqueue(new ServerText(null, Truncate(System.Net.WebUtility.HtmlEncode(text), Limits.MaxServerTextBytes)));
    }

    // The word of a cardinal BYOND dir.
    private static string DirWord(int dir) => dir switch { 1 => "north", 2 => "south", 4 => "east", 8 => "west", _ => dir.ToString(CultureInfo.InvariantCulture) };
}
