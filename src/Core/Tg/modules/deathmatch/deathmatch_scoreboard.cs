// The port's own code (DV585; docs/ai/specs/deathmatch): the kills and deaths of a deathmatch map. Not a translation; tg's deathmatch
// has no scoreboard.
using System.Linq;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

[assembly: DmPortType(typeof(Anomaly.Core.Tg.datum_deathmatch_scoreboard))]

namespace Anomaly.Core.Tg;

/// <summary>
/// DEVIATION(DV585): the scoreboard of a map whose json has <c>"deathmatch": {"scoreboard": 1}</c>. It counts, by ckey and for the whole
/// round (a respawn keeps the counts), a death for each player whose mob dies and a kill for the player who killed it: the last player
/// who hurt it within <see cref="KillWindow"/>. tg sets <c>lastattackerckey</c> for melee and unarmed hits only; the scoreboard also
/// takes a projectile's firer (the pilot of a mech that fired it) and the player behind an explosion (a rocket's firer, the last to
/// touch a grenade). A death with no such player, or by one's own hand, is a death without a kill. A player holding the scoreboard key
/// sees the table on the screen: names, kills and deaths in three columns, sorted by kills.
/// </summary>
[DmPath("/datum/deathmatch_scoreboard")]
public partial class datum_deathmatch_scoreboard : datum
{
    /// <summary>How long a hit counts toward a kill, deciseconds.</summary>
    public const float KillWindow = 10 * SECONDS;

    public sealed class Score
    {
        public string Name;
        public int Kills;
        public int Deaths;
    }

    /// <summary>The players' scores by ckey.</summary>
    public readonly Dictionary<string, Score> Scores = new();

    private readonly Dictionary<mob_living, (string Ckey, double At)> _lastHit = new();
    private readonly Dictionary<client, atom_movable_screen_text[]> _shown = new(); // who holds the key: their three columns

    /// <summary>The scoreboard of the world's map, made once when its json asks for one (null otherwise).</summary>
    public static datum_deathmatch_scoreboard Start()
    {
        datum_map_config map = SSmapping?.current_map;
        if (map?.deathmatch == null || !Dm.truthy(map.deathmatch["scoreboard"]))
            return null;
        return map.scoreboard ??= Dm.@new<datum_deathmatch_scoreboard>();
    }

    /// <summary>The world's scoreboard, or null on a map without one.</summary>
    public static datum_deathmatch_scoreboard Current => SSmapping?.current_map?.scoreboard;

    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        RegisterSignal(SSdcs, COMSIG_GLOB_MOB_DEATH, nameof(on_death));
        RegisterSignal(SSdcs, COMSIG_GLOB_MOB_CREATED, nameof(on_mob_created));
        RegisterSignal(SSdcs, COMSIG_GLOB_EXPLOSION, nameof(on_explosion));
        foreach (mob_living living in Dm.each<mob_living>(GLOB.mob_living_list))
            watch(living);
    }

    private void watch(mob_living living)
    {
        RegisterSignal(living, COMSIG_ATOM_BULLET_ACT, nameof(on_bullet_act));
        RegisterSignal(living, COMSIG_MOB_APPLY_DAMAGE, nameof(on_damage));
        RegisterSignal(living, COMSIG_QDELETING, nameof(on_mob_gone));
    }

    [SignalHandler]
    public virtual void on_mob_created(datum source = null, mob created = null)
    {
        if (created is mob_living living)
            watch(living);
    }

    [SignalHandler]
    public virtual void on_mob_gone(datum source = null)
    {
        if (source is mob_living living)
            _lastHit.Remove(living);
    }

    // A projectile's hit: its firer (atom_act's bullet_act sends it before the damage).
    [SignalHandler]
    public virtual void on_bullet_act(atom source = null, obj_projectile proj = null)
    {
        if (source is mob_living living && AttackerCkey(proj?.firer) is string ckey)
            _lastHit[living] = (ckey, Dm.num(Dm.world.time));
    }

    // Any damage: tg's lastattackerckey of a melee or unarmed hit, set just before; taken and cleared, so a later fall or fire is no hit.
    [SignalHandler]
    public virtual void on_damage(mob_living source = null)
    {
        if (source == null || source.lastattackerckey is not string ckey || ckey.Length == 0)
            return;
        _lastHit[source] = (ckey, Dm.num(Dm.world.time));
        source.lastattackerckey = null;
    }

    // An explosion: the player behind it hits every mob in its reach (the blast's ex_acts come later, in SSexplosions' fire).
    [SignalHandler]
    public virtual void on_explosion(datum source = null, turf epicenter = null, object devastation_range = null, object heavy_impact_range = null, object light_impact_range = null,
        object took = null, object orig_dev_range = null, object orig_heavy_range = null, object orig_light_range = null, object explosion_cause = null)
    {
        if (epicenter == null || AttackerCkey(explosion_cause) is not string ckey)
            return;
        float reach = Math.Max(Dm.num(devastation_range), Math.Max(Dm.num(heavy_impact_range), Dm.num(light_impact_range)));
        double now = Dm.num(Dm.world.time);
        foreach (mob_living living in Dm.each<mob_living>(GLOB.mob_living_list))
        {
            if (get_turf(living) is turf at && Math.Abs(Dm.num(at.z) - Dm.num(epicenter.z)) <= 1 && Dm.num(get_dist(epicenter, at)) <= reach)
                _lastHit[living] = (ckey, now);
        }
    }

    [SignalHandler]
    public virtual void on_death(datum source = null, mob dead = null, object gibbed = null)
    {
        if (dead is not mob_living victim || PlayerCkey(victim) is not string ckey)
            return;
        on_damage(victim); // a melee killing blow that dealt no apply_damage
        ScoreOf(ckey, victim).Deaths++;
        if (_lastHit.Remove(victim, out var hit) && hit.Ckey != ckey && Dm.num(Dm.world.time) - hit.At <= KillWindow)
            ScoreOf(hit.Ckey, null).Kills++;
        Refresh();
    }

    private Score ScoreOf(string ckey, mob who)
    {
        if (!Scores.TryGetValue(ckey, out Score score))
            Scores[ckey] = score = new Score { Name = ckey };
        if (who?.key is string key && key.Length > 0)
            score.Name = key;
        return score;
    }

    // The player behind an attack: a mob's, a mech's pilot, a projectile's firer, or the last to touch a thing (a grenade).
    public static string AttackerCkey(object who, int depth = 0)
    {
        switch (who)
        {
            case mob m:
                return PlayerCkey(m);
            case obj_vehicle vehicle:
                foreach (mob occupant in Dm.each<mob>(vehicle.occupants))
                    if (PlayerCkey(occupant) is string pilot)
                        return pilot;
                return null;
            case obj_projectile proj when depth < 4:
                return AttackerCkey(proj.firer, depth + 1);
            case atom thing:
                return thing.fingerprintslast is string touched && touched.Length > 0 ? touched : null;
        }
        return null;
    }

    private static string PlayerCkey(mob m)
    {
        string ckey = m?.ckey;
        if (string.IsNullOrEmpty(ckey))
            ckey = Dm.ckey(m?.mind?.key);
        return string.IsNullOrEmpty(ckey) ? null : ckey;
    }

    /// <summary>The rows in the panel's order: the players who play now (with no score yet) and everyone who scored, by kills, then fewer deaths, then name.</summary>
    public List<Score> Rows()
    {
        var rows = new Dictionary<string, Score>(Scores);
        foreach (client c in Dm.each<client>(GLOB.clients))
        {
            string ckey = c.ckey;
            if (!string.IsNullOrEmpty(ckey) && !rows.ContainsKey(ckey))
                rows[ckey] = new Score { Name = c.key ?? ckey };
        }
        return rows.Values.OrderByDescending(s => s.Kills).ThenBy(s => s.Deaths).ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>The scoreboard key went down (show the table) or up (hide it).</summary>
    public void Hold(client player, bool down)
    {
        if (player == null)
            return;
        if (!down)
        {
            if (_shown.Remove(player, out atom_movable_screen_text[] columns))
                foreach (atom_movable_screen_text column in columns)
                {
                    player.screen.Remove(column);
                    qdel(column);
                }
            return;
        }
        if (!_shown.ContainsKey(player))
            _shown[player] = new[] { Dm.@new<atom_movable_screen_text>(), Dm.@new<atom_movable_screen_text>(), Dm.@new<atom_movable_screen_text>() };
        Draw(player, _shown[player]);
    }

    private void Refresh()
    {
        foreach (KeyValuePair<client, atom_movable_screen_text[]> shown in _shown.ToArray())
        {
            if (QDELETED(shown.Key) || !shown.Key.dm_connected)
                _shown.Remove(shown.Key);
            else
                Draw(shown.Key, shown.Value);
        }
    }

    // Three columns, so that the numbers line up whatever the names' widths: names on the left, kills and deaths centred.
    private void Draw(client player, atom_movable_screen_text[] columns)
    {
        List<Score> rows = Rows();
        var names = new System.Text.StringBuilder(Header(I18n.T("Player")));
        var kills = new System.Text.StringBuilder(Header(I18n.T("Kills")));
        var deaths = new System.Text.StringBuilder(Header(I18n.T("Deaths")));
        foreach (Score row in rows)
        {
            bool own = row.Name == player.key;
            names.Append("<br>").Append(Cell(html_encode(row.Name), own));
            kills.Append("<br>").Append(Cell(row.Kills.ToString(System.Globalization.CultureInfo.InvariantCulture), own));
            deaths.Append("<br>").Append(Cell(row.Deaths.ToString(System.Globalization.CultureInfo.InvariantCulture), own));
        }
        int height = 32 * (rows.Count + 2);
        screen_text(columns[0], Column(names.ToString(), "left"), "CENTER-5,NORTH-" + (rows.Count + 2), height, 224);
        screen_text(columns[1], Column(kills.ToString(), "center"), "CENTER+2,NORTH-" + (rows.Count + 2), height, 64);
        screen_text(columns[2], Column(deaths.ToString(), "center"), "CENTER+4,NORTH-" + (rows.Count + 2), height, 64);
        foreach (atom_movable_screen_text column in columns)
            if (!Dm.@in(column, player.screen))
                player.screen.Add(column);
    }

    private static string Header(string text) => "<span style='color: #ffd75e'>" + text + "</span>";

    private static string Cell(string text, bool own) => own ? "<span style='color: #8fe388'>" + text + "</span>" : text;

    /// <summary>A column's maptext: the countdown's Pixellari (DV580), outlined, its lines one tile apart.</summary>
    public static string Column(string lines, string align)
        => "<span style='font-family: \"Pixellari\"; font-size: 16pt; line-height: 32px; text-align: " + align + "; -dm-text-outline: 1px black'>" + lines + "</span>";
}

// DEVIATION(DV585): the map's scoreboard, made when the round starts (datum_deathmatch_scoreboard.Start)
public partial class datum_map_config
{
    public datum_deathmatch_scoreboard scoreboard;
}
