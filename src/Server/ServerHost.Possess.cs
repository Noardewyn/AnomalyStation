using System.Globalization;
using System.Linq;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server;

// D346 (the owner, 2026-11-21): an admin takes over any body from the dev menu, an NPC, a creature or a robot, and plays it. possess is tg's
// Assume Direct Control and givecontrol tg's Give Direct Control (DevCommands); unpossess, the way back to the body the admin left, is the
// port's own (tg's admin ghosts out and re-enters the body). The session follows the player into the new mob through ClientMobChanged
// (the capsule, the HUD, the view), and the mob's AI pauses while a player is in it (tg's ai_controller).
public sealed partial class ServerHost
{
    /// <summary>The most mobs the dev menu's nearby list shows.</summary>
    public const int MaxNearbyMobs = 30; // about 3 KB of the answer's 16

    /// <summary>How far round the requester the nearby list looks, in tiles (on its level).</summary>
    public const int NearbyMobRange = 12;

    // ckey -> the world id of the body its player left with possess (0: they came as a ghost with no body to go back to). The first body
    // over a chain of possessions is kept, so unpossess goes back to the player's own; cleared with the world (ResetForNewWorld).
    private readonly Dictionary<string, long> _possessReturn = new(StringComparer.Ordinal);

    /// <summary>D346: the world id of the body <paramref name="key"/>'s player goes back to with <c>unpossess</c>; null when they possess nothing.</summary>
    public long? PossessReturnOf(string key) => _possessReturn.TryGetValue(Dm.ckey(key) ?? "", out long id) ? id : null;

    private string Possess(DevCommand dev, mob requester)
    {
        if (requester == null)
            return "possess: the console has no player; givecontrol WHO PLAYER puts a player in a mob";
        atom_movable found = string.Equals(dev.Who, "me", StringComparison.OrdinalIgnoreCase) ? requester : FindMovable(dev.Who, out _);
        if (found is not mob target)
            return found == null ? $"possess: no player or mob '{dev.Who}'" : $"possess: {Dm.str(found.name)} is not a mob";
        string ckey = Dm.ckey(requester.key);
        long back = BodyToGoBackTo(requester);
        string reply = DevCommands.AssumeDirectControl(requester, target, dev.Mode == "force");
        if (string.IsNullOrEmpty(ckey) || Dm.ckey(target.ckey) != ckey)
            return reply; // refused
        if (!_possessReturn.ContainsKey(ckey))
            _possessReturn[ckey] = back;
        if (_possessReturn[ckey] == target.dm_id)
            _possessReturn.Remove(ckey); // back in the own body by possess: nothing to return to
        return reply;
    }

    // The body a player in requester goes back to: the requester itself when it is a body, a ghost's body it can re-enter, else none (0).
    private static long BodyToGoBackTo(mob requester)
    {
        if (requester is mob_living)
            return requester.dm_id;
        if (requester is mob_dead_observer ghost && Dm.truthy(ghost.can_reenter_corpse) && ghost.mind?.current is mob_living body && !QDELETED(body))
            return body.dm_id;
        return 0;
    }

    // DEVIATION(DV581): tg has no way back; its admin ghosts out of the mob and re-enters the old body.
    private string Unpossess(mob requester)
    {
        if (requester == null)
            return "unpossess: the console has no player";
        string ckey = Dm.ckey(requester.key);
        if (string.IsNullOrEmpty(ckey) || !_possessReturn.Remove(ckey, out long id))
            return "unpossess: you possess nothing (possess WHO takes a mob over)";
        string key = requester.key;
        if (id != 0 && World.Objects.TryGet(id, out object o) && o is mob body && !QDELETED(body))
        {
            if (ReferenceEquals(body, requester))
                return "unpossess: you are in your own body";
            if (body.ckey == null)
            {
                log_admin(Dm.text("[] went back from [] to [].", key_name(requester), requester, body));
                body.PossessByPlayer(key);
                body.client?.init_verbs();
                return "You return to " + Dm.text("[]", body) + ".";
            }
            // someone else took the body meanwhile: out as a ghost, as tg's admin would leave
            log_admin(Dm.text("[] left [] as a ghost: their body is played by [].", key_name(requester), requester, body.key));
            requester.ghostize(Dm.FALSE);
            return Dm.text("Your body is played by []; you are a ghost.", body.key);
        }
        if (requester is mob_dead_observer)
            return "unpossess: you are a ghost already";
        log_admin(Dm.text("[] left [] as a ghost.", key_name(requester), requester));
        requester.ghostize(Dm.FALSE);
        return "Your body is gone; you are a ghost.";
    }

    private string GiveControl(DevCommand dev, mob requester)
    {
        atom_movable found = string.Equals(dev.Who, "me", StringComparison.OrdinalIgnoreCase) ? requester : FindMovable(dev.Who, out _);
        if (found is not mob target)
            return found == null ? $"givecontrol: no player or mob '{dev.Who}'" : $"givecontrol: {Dm.str(found.name)} is not a mob";
        string ckey = Dm.ckey(dev.Mode);
        Session player = _sessions.FirstOrDefault(s => s.Ckey == ckey && s.Client != null);
        if (player == null)
            return $"givecontrol: no online player '{dev.Mode}'";
        object user = requester != null ? requester : ConsoleName;
        return DevCommands.GiveDirectControl(user, target, player.Client, deleteOld: dev.Count == 1, force: dev.Zone == "force");
    }

    // D346: the mobinfo answer's rows for the dev menu's possess list: the requester's way back ("Possess\treturn\tNAME", "ghost", or none) and the
    // mobs within NearbyMobRange tiles of its level, nearest first ("nearby\t#ID\tNAME|TYPE|PLAYER|DISTANCE|DEAD", a '|' in a field made '/').
    private void AppendPossessRows(System.Text.StringBuilder text, Session s, mob requester)
    {
        if (PossessReturnOf(s.Key) is long back)
        {
            string name = back != 0 && World.Objects.TryGet(back, out object o) && o is mob body && !QDELETED(body) ? Clean(Dm.str(body.name)) : "ghost";
            text.Append("Possess\treturn\t").Append(name).Append('\n');
        }
        if (TurfOf(requester) is not turf here)
            return;
        var near = new List<(int Distance, mob_living Mob)>();
        foreach (mob m in Dm.each<mob>(TgState.Of(World).GLOB?.mob_list))
        {
            if (m is not mob_living living || ReferenceEquals(m, requester) || QDELETED(m) || TurfOf(m) is not turf at || at.z != here.z)
                continue;
            int d = (int)Math.Max(Math.Abs(at.x - here.x), Math.Abs(at.y - here.y));
            if (d <= NearbyMobRange)
                near.Add((d, living));
        }
        foreach ((int d, mob_living m) in near.OrderBy(n => n.Distance).ThenBy(n => n.Mob.dm_id).Take(MaxNearbyMobs))
        {
            const char unit = '|';
            text.Append("nearby\t#").Append(m.dm_id.ToString(CultureInfo.InvariantCulture)).Append('\t')
                .Append(Field(Dm.str(m.name))).Append(unit)
                .Append(Field(DmTypes.PathOf(m.GetType()) ?? m.GetType().Name)).Append(unit)
                .Append(Field(m.ckey != null ? m.key : "")).Append(unit)
                .Append(d.ToString(CultureInfo.InvariantCulture)).Append(unit)
                .Append(m.stat == Defines.DEAD ? "dead" : "").Append('\n');
        }
    }

    private static string Field(string value) => Clean(value).Replace('|', '/');
}
