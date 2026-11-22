using System.Globalization;
using System.Linq;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server;

// DEVIATION(DV580) (the owner, 2026-11-21): the dev menu's auto-respawn. While it is on, a player whose mob dies (or who is a ghost
// of a dead or lost body) sees "Respawning in N seconds" on the screen, and when the delay is over the host takes tg's Respawn verb
// for them (abandon_mob, mob.dm:814, past its config and delay checks: a new /mob/dead/new_player gets the key) and late-joins that new
// player as a direct join does (DirectJoins: tg's overflow job at the host's next spawn point). tg has the verb only: the player clicks
// it, after the config's respawn_delay. The countdown is tg's /atom/movable/screen/text made by tg's screen_text (game.dm:73), as
// the round end's reboot timer is a maptext on the screen. A revived mob stops the countdown; a ghost that started as an observer, a
// ghost whose body still lives (an admin ghost) and a player with no client wait for nothing. The world half (D31): it reads the
// sessions' Client only, and runs in the command phase, so a replay respawns at the same ticks. The mode is the world's (its next
// round starts with it off), as the other dev knobs are.
public sealed partial class ServerHost
{
    /// <summary>DV580: the auto-respawn delay when none was given, seconds.</summary>
    public const float DefaultAutoRespawnSeconds = 10f;

    private bool _autoRespawn;
    private bool _mapRespawnRead; // DV583: the world's map json was read for its deathmatch settings
    private float _autoRespawnSeconds = DefaultAutoRespawnSeconds;
    private readonly Dictionary<int, PendingRespawn> _respawns = new(); // session ordinal -> its countdown

    private sealed class PendingRespawn
    {
        public double DiedAt;                  // world.time the death was seen
        public atom_movable_screen_text Text;  // the countdown on the player's screen
        public int Shown = -1;                 // the seconds the text shows
    }

    /// <summary>DV580: whether the auto-respawn is on, and its delay in seconds.</summary>
    public (bool On, float Seconds) AutoRespawn => (_autoRespawn, _autoRespawnSeconds);

    // The autorespawn dev command (from the console or a player): on or off, and the delay when one is given.
    private string SetAutoRespawn(in DevCommand dev)
    {
        _autoRespawn = dev.Mode == "on";
        if (dev.Number >= 0f)
            _autoRespawnSeconds = dev.Number;
        if (!_autoRespawn)
            ClearRespawns();
        return _autoRespawn
            ? string.Create(CultureInfo.InvariantCulture, $"auto-respawn on: a dead player respawns after {_autoRespawnSeconds:0.#} seconds")
            : "auto-respawn off";
    }

    // Each command phase: the countdowns of the dead players, and the respawns that are due.
    private void AutoRespawns(World world)
    {
        if (!_mapRespawnRead)
            ReadMapDeathmatch();
        if (!_autoRespawn)
            return;
        foreach (Session s in _sessions)
        {
            client c = s.Client;
            mob m = c?.mob;
            if (m == null || QDELETED(m) || !WaitsToRespawn(m))
            {
                DropRespawn(s.Ordinal);
                continue;
            }
            if (!_respawns.TryGetValue(s.Ordinal, out PendingRespawn pending))
                _respawns[s.Ordinal] = pending = new PendingRespawn { DiedAt = Dm.num(Dm.world.time) };
            double left = pending.DiedAt + _autoRespawnSeconds * 10.0 - Dm.num(Dm.world.time); // deciseconds
            if (left <= 0)
            {
                Respawn(world, s, c, m);
                continue;
            }
            ShowCountdown(c, pending, (int)Math.Ceiling(left / 10.0));
        }
    }

    // A dead mob, or a ghost of a body that died or is gone (not one that started as an observer, nor an admin ghost of a live body).
    private static bool WaitsToRespawn(mob m)
    {
        if (m is mob_living living)
            return Dm.num(living.stat) == DEAD;
        if (m is not mob_dead_observer ghost || Dm.truthy(ghost.started_as_observer))
            return false;
        mob body = ghost.mind?.current;
        return body == null || QDELETED(body) || body == ghost || (body is mob_living b && Dm.num(b.stat) == DEAD);
    }

    // The countdown's text on the player's screen (again when a new HUD dropped it): tg's screen_text, a line above the crosshair.
    private static void ShowCountdown(client c, PendingRespawn pending, int seconds)
    {
        if (pending.Text == null || QDELETED(pending.Text))
        {
            pending.Text = Dm.@new<atom_movable_screen_text>();
            pending.Shown = -1;
        }
        if (pending.Shown != seconds)
        {
            pending.Shown = seconds;
            string line = string.Create(CultureInfo.InvariantCulture, $"Respawning in {seconds} second{(seconds == 1 ? "" : "s")}");
            screen_text(pending.Text, CountdownMarkup(line), "CENTER-7,CENTER+2", 64, 480);
        }
        if (!Dm.@in(pending.Text, c.screen))
            c.screen.Add(pending.Text);
    }

    /// <summary>DV580: the countdown's maptext inside tg's MAPTEXT span: the reboot timer's Pixellari, larger, centred.</summary>
    public static string CountdownMarkup(string line)
        => "<span style='font-family: \"Pixellari\"; font-size: 24pt; text-align: center; -dm-text-outline: 1px black'>" + line + "</span>";

    // tg's abandon_mob (mob.dm:845-858) past its checks, then the new player's late join as a direct join's.
    private void Respawn(World world, Session s, client c, mob old)
    {
        DropRespawn(s.Ordinal);
        world.usr = old;
        try
        {
            old.log_message("was respawned by the dev auto-respawn.", LOG_GAME);
            to_chat(old, span_boldnotice(I18n.T("Please roleplay correctly!")));
            c.screen.Cut();
            c.screen.Add(c.@void);
            mob_dead_new_player player = Dm.@new<mob_dead_new_player>();
            player.PossessByPlayer(old.key);
            if (!_directJoins.Contains(s))
                _directJoins.Add(s); // DirectJoins, later in this command phase
        }
        finally
        {
            world.usr = null;
        }
    }

    private void DropRespawn(int ordinal)
    {
        if (!_respawns.Remove(ordinal, out PendingRespawn pending) || pending.Text == null)
            return;
        foreach (Session s in _sessions)
        {
            if (s.Ordinal == ordinal)
                s.Client?.screen.Remove(pending.Text);
        }
        qdel(pending.Text);
    }

    private void ClearRespawns()
    {
        foreach (int ordinal in _respawns.Keys.ToArray())
            DropRespawn(ordinal);
    }

    // DEVIATION(DV583): a deathmatch map (its json's "deathmatch": {"autorespawn_seconds": N}, docs/ai/specs/deathmatch) starts its
    // round with the auto-respawn on, after N seconds; the dev command still turns it off or changes the delay. With "scoreboard"
    // the round keeps its kills and deaths (DV585).
    private void ReadMapDeathmatch()
    {
        _mapRespawnRead = true;
        datum_deathmatch_scoreboard.Start();
        object seconds = SSmapping?.current_map?.deathmatch?["autorespawn_seconds"];
        if (!Dm.isnum(seconds) || Dm.num(seconds) < 0)
            return;
        _autoRespawn = true;
        _autoRespawnSeconds = Dm.num(seconds);
    }

    // The old world's: its countdowns go with it, and the next round starts with the mode off (or its map's, DV583).
    private void ForgetRespawns()
    {
        _mapRespawnRead = false;
        _respawns.Clear();
        _autoRespawn = false;
        _autoRespawnSeconds = DefaultAutoRespawnSeconds;
    }
}
