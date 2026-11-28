using System.Linq;
using System.Text;

namespace Anomaly.Client.Shared;

// Owner: M2 P8 (client play). Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).

/// <summary>
/// One console command as the client knows it: its verb and other names, who runs it, its syntax and a short description.
/// <paramref name="Usage"/> starts with the verb; upper-case words in it are placeholders (<see cref="ConsoleCatalog.Choices"/>
/// names the values of some), lower-case words and paths are typed as they are, <c>a|b</c> is one of them and <c>[...]</c>
/// may be left out.
/// </summary>
public sealed record ConsoleCommandInfo(string Verb, ConsoleRoute Route, string Usage, string Description, params string[] Aliases)
{
    /// <summary>The verb, then the other names.</summary>
    public IEnumerable<string> Names => Aliases.Prepend(Verb);
}

/// <summary>
/// The console's commands with their help (the owner's request, 2026-10-10: <c>help</c> with descriptions, and suggestions while
/// typing): the help text, the suggestions under the line and the completion Tab takes. The routing is still the server's
/// (DevCommandLine, ConsoleCommand; a server test keeps the verbs equal); this is what the client tells the player about them.
/// </summary>
public static class ConsoleCatalog
{
    /// <summary>The most suggestion lines shown at once; the rest scroll (the owner, 2026-11-02: every matching command is listed).</summary>
    public const int MaxSuggestions = 6;

    /// <summary>The help's syntax column: a longer syntax pushes only its own description.</summary>
    public const int UsageColumn = 30;

    /// <summary>The air alarm modes of <c>airmode</c> (DevCommandLine.AirModes; a server test keeps the two equal).</summary>
    public static readonly string[] AirModes = { "filtering", "contaminated", "draught", "refill", "cycle", "siphon", "panic", "off" };

    private static readonly string[] Items =
    {
        "/obj/item/card/id/advanced/debug", "/obj/item/crowbar", "/obj/item/wrench", "/obj/item/screwdriver", "/obj/item/weldingtool",
        "/obj/item/wirecutters", "/obj/item/multitool", "/obj/item/analyzer", "/obj/item/extinguisher", "/obj/item/flashlight",
        "/obj/item/tank/internals/oxygen", "/obj/item/clothing/mask/gas",
    };

    /// <summary>Every command, in the order the help lists them.</summary>
    public static readonly ConsoleCommandInfo[] Commands =
    {
        new("help", ConsoleRoute.Client, "help [VERB]", "this list, or one command in detail", "?"),
        new("clear", ConsoleRoute.Client, "clear", "empty the log"),
        new("ui", ConsoleRoute.Client, "ui hide|show|toggle", "hide or show the whole interface (the HUD, the chat, the windows, the minimap; this console and the menu stay); its key: Shift+F4"),

        new("damage", ConsoleRoute.Dev, "damage WHO KIND N [ZONE]", "hurt by N (0 to 1000), on a body part or spread"),
        new("heal", ConsoleRoute.Dev, "heal WHO [KIND]", "heal one kind of damage, or all four"),
        new("fullheal", ConsoleRoute.Dev, "fullheal WHO", "heal everything, status effects too, no revive (tg's admin heal)"),
        new("kill", ConsoleRoute.Dev, "kill WHO", "kill"),
        new("revive", ConsoleRoute.Dev, "revive WHO", "bring back to life, fully healed and standing (tg's admin rejuvenate); a ghost whose body was gibbed gets a new one"),
        new("knockdown", ConsoleRoute.Dev, "knockdown WHO S", "knock down for S seconds (0 to 60)"),
        new("stun", ConsoleRoute.Dev, "stun WHO S", "stun for S seconds (0 to 60)"),
        new("bleed", ConsoleRoute.Dev, "bleed WHO ZONE STACKS", "make a body part bleed (-30 to 30 stacks)"),
        new("blood", ConsoleRoute.Dev, "blood WHO ML", "set the blood volume (0 to 2000)"),
        new("dismember", ConsoleRoute.Dev, "dismember WHO ZONE", "cut off a limb or the head, or split the chest open (tg's dismember)"),
        new("gib", ConsoleRoute.Dev, "gib WHO [brain]", "burst a body into gibs, its organs and limbs flying (tg's admin Gib)"),
        new("wound", ConsoleRoute.Dev, "wound WHO ZONE blunt|slash|pierce|burn moderate|severe|critical", "give a body part a wound of that kind and severity"),
        new("gas", ConsoleRoute.Dev, "gas air|vacuum|MIX [X Y [Z]]", "set the air of one turf, or of every open turf but space"),
        new("analyze", ConsoleRoute.Dev, "analyze WHO", "health scan, shown in the analyzer panel"),
        new("mobinfo", ConsoleRoute.Dev, "mobinfo [WHO]", "health, damage, blood, organs, effects, traits and dev switches"),
        new("effects", ConsoleRoute.Dev, "effects [WHO]", "the status effects (buffs and debuffs) with their time left"),
        new("effect", ConsoleRoute.Dev, "effect WHO add TYPE [N]", "apply a status effect (N: seconds or stacks; tg's apply_status_effect)"),
        new("effect", ConsoleRoute.Dev, "effect WHO remove TYPE", "remove a status effect (tg's remove_status_effect)"),
        new("effect", ConsoleRoute.Dev, "effect types", "the status effect types the server has"),
        new("reagent", ConsoleRoute.Dev, "reagent WHO REAGENT N", "tg's Add reagent: N units of a /datum/reagent path or name (water) into the blood"),
        new("reagents", ConsoleRoute.Dev, "reagents [WHO]", "the reagents in the blood and the stomach, metabolizing or overdosed"),
        new("heart", ConsoleRoute.Dev, "heart WHO stop|start", "stop or restart a heart (tg's set_heartattack)"),
        new("breath", ConsoleRoute.Dev, "breath WHO N", "N breaths held (tg's losebreath)"),
        new("hallucinate", ConsoleRoute.Dev, "hallucinate WHO HALLUCINATION [SECONDS]", "a /datum/hallucination type once (tg's Give Hallucination); a number instead of HALLUCINATION gives the hallucination status effect for that many seconds"),
        new("spawn", ConsoleRoute.Dev, "spawn TYPE [X Y [Z [N [DIR]]]]", "N (1 to 100) of any /obj, /mob or /turf at your feet or X Y Z, facing DIR (tg's spawn panel)"),
        new("speedscale", ConsoleRoute.Dev, "speedscale F", "everyone's movement speed times F (up to 4)"),
        new("footstride", ConsoleRoute.Dev, "footstride run|walk TILES", "the tiles walked per footstep stride, a step every two (up to 4)"),
        new("movefeel", ConsoleRoute.Dev, "movefeel ACCEL DECEL BACK STRAFE", "ticks to full speed and to a stop (0 to 40), the speed lost going back and sideways (0 to 0.9); 0 0 0 0 is tg's"),
        new("minimapmobs", ConsoleRoute.Dev, "minimapmobs on|off", "the living mobs of your level on your minimap (players green, the rest red)"),
        new("autorespawn", ConsoleRoute.Dev, "autorespawn on|off [SECONDS]", "every dead player respawns after SECONDS (default 10) as a new crew member at a spawn point"),
        new("speed", ConsoleRoute.Dev, "speed WHO F", "one player's movement speed times F, over speedscale (up to 4)"),
        new("godmode", ConsoleRoute.Dev, "godmode WHO", "toggle taking no damage, stuns, stagger, fire or slowdowns"),
        new("pilot", ConsoleRoute.Dev, "pilot WHO", "climb into the mech next to you at once, or out of the one you are in"),
        new("fly", ConsoleRoute.Dev, "fly WHO", "toggle flying (no gravity, no wind push, no slowdowns)"),
        new("noclip", ConsoleRoute.Dev, "noclip WHO", "toggle moving through walls"),
        new("invuln", ConsoleRoute.Dev, "invuln WHO", "toggle invulnerability: godmode's immunities, and no one can push, shove, grab, strip, cuff or click you"),
        new("esp", ConsoleRoute.Dev, "esp WHO", "toggle seeing the ghosts (tg's Toggle Admin ESP)"),
        new("invisimin", ConsoleRoute.Dev, "invisimin WHO", "toggle ghost-like invisibility: only ghosts and esp see WHO (tg's Invisimin)"),
        new("god", ConsoleRoute.Dev, "god WHO", "fly, noclip and godmode together"),
        new("up", ConsoleRoute.Dev, "up WHO", "flying, a level up where open space lets you (with noclip through the floor) [PageUp]"),
        new("down", ConsoleRoute.Dev, "down WHO", "flying, a level down where open space lets you (with noclip through the floor) [PageDown]"),
        new("startnow", ConsoleRoute.Dev, "startnow", "start the round now, skipping the lobby's countdown (again before it starts: cancel)"),
        new("tram", ConsoleRoute.Dev, "tram [PLATFORM]", "call the tram to the platform (as its call button does; without a number: the next one on its line)"),
        new("shuttle", ConsoleRoute.Dev, "shuttle recall|call|dock", "call or recall the emergency shuttle (tg's admin Call/Cancel Shuttle; call norecall: not recallable; dock: it docks with the station at once)"),
        new("reboot", ConsoleRoute.Dev, "reboot [SECONDS]|now", "end the round and start the next one after SECONDS (tg's admin Reboot World); now: at once (tg's hard restart)"),
        new("changemap", ConsoleRoute.Dev, "changemap [MAP]", "tg's admin Change Map: the next round loads MAP (without one: lists the maps)"),
        new("give", ConsoleRoute.Dev, "give WHO ITEM", "a new item in the hands (the debug ID card opens every door)"),
        new("outfit", ConsoleRoute.Dev, "outfit [WHO] OUTFIT", "tg's Select equipment: wears a /datum/outfit (/datum/outfit/job/assistant)"),
        new("antag", ConsoleRoute.Dev, "antag [WHO] ANTAGONIST", "tg's traitor panel: makes the mind an antagonist (/datum/antagonist/traitor)"),
        new("robotize", ConsoleRoute.Dev, "robotize WHO", "tg's admin Make Cyborg: WHO becomes a cyborg and picks a model with the module button"),
        new("aiize", ConsoleRoute.Dev, "aiize WHO [here]", "tg's admin Make AI: WHO becomes an AI at an AI spawn (here: where WHO stands)"),
        new("possess", ConsoleRoute.Dev, "possess WHO [force]", "tg's admin Assume Direct Control: you play WHO, any mob (force: take it from its player, who becomes a ghost)"),
        new("unpossess", ConsoleRoute.Dev, "unpossess", "back to the body you left with possess (a ghost when there is none)"),
        new("givecontrol", ConsoleRoute.Dev, "givecontrol WHO PLAYER [delete] [force]", "tg's admin Give Direct Control: PLAYER plays WHO (delete: PLAYER's old mob)"),
        new("paiize", ConsoleRoute.Dev, "paiize WHO", "tg's admin Make pAI: a pAI card at WHO's feet and WHO's player the pAI in it"),
        new("trauma", ConsoleRoute.Dev, "trauma [WHO] TRAUMA", "tg's Give brain trauma: a lobotomy-resilient brain trauma (/datum/brain_trauma/mild/phobia/clowns)"),
        new("lazyload", ConsoleRoute.Dev, "lazyload [WHO] KEY", "tg's Load/Jump Lazy Template: loads it (LT_NINJAHOLDING) and moves WHO in"),
        new("nukecode", ConsoleRoute.Dev, "nukecode [CODE]", "sets every nuke's code (five digits; random when none) and shows it"),
        new("shoot", ConsoleRoute.Dev, "shoot [WHO] PROJECTILE|off", "clicks fire an /obj/projectile along the crosshair (before the guns)"),
        new("drop", ConsoleRoute.Dev, "drop WHO SLOT", "drops what is worn in SLOT (back, suit, head, mask, shoes, ...) to the floor"),
        new("gravity", ConsoleRoute.Dev, "gravity WHO on|off [now]", "the gravity generator's breaker (tg's pace, about 100 s; now: at once)"),
        new("breach", ConsoleRoute.Dev, "breach WHO|X Y [Z]", "the turf in front (or at X Y Z) becomes space"),
        new("patch", ConsoleRoute.Dev, "patch WHO|X Y [Z]", "closes a breach: what it removed comes back (else plating)"),
        new("valve", ConsoleRoute.Dev, "valve WHO|X Y [Z]", "open or close the valve of the canister in front"),
        new("airmode", ConsoleRoute.Dev, "airmode WHO MODE", "set the mode of the air alarm in WHO's area"),
        new("areapower", ConsoleRoute.Dev, "areapower X Y [Z] equip|light|environ|all on|off", "a power channel of the area at X Y Z, as its APC sets it: lights fall back on their emergency cells"),
        new("ignite", ConsoleRoute.Dev, "ignite WHO|X Y [Z]", "a spark on the turf in front (or at X Y Z): burns plasma, tritium or hydrogen"),
        new("explode", ConsoleRoute.Dev, "explode X Y [Z] DEV HEAVY LIGHT [FLASH FLAME]", "tg's admin Drop Bomb: an explosion at X Y Z with those ranges (no cap)"),
        new("emp", ConsoleRoute.Dev, "emp X Y [Z] HEAVY LIGHT", "tg's admin EM Pulse: an electromagnetic pulse at X Y Z, heavy within HEAVY tiles and light out to LIGHT (drains cells, trips doors, alarms and machines)"),
        new("recharge", ConsoleRoute.Dev, "recharge all|X Y [Z]", "fills the charge of the power storage unit (SMES) at X Y Z, or of every one: the station's stored power for long playtests"),
        new("irradiate", ConsoleRoute.Dev, "irradiate X Y [Z] INTENSITY [THRESHOLD]", "tg's radiation pulse at X Y Z out to INTENSITY tiles, stopped where the insulation falls to THRESHOLD (0.7 by default: a wall)"),
        new("zap", ConsoleRoute.Dev, "zap X Y [Z] POWER", "tg's tesla zap from X Y Z: a lightning bolt of POWER joules (at least 400000) that strikes a tesla coil first (power for the network), then a grounding rod, a mob, a machine or a structure"),
        new("smcrystal", ConsoleRoute.Dev, "smcrystal X Y [Z] status|power|damage [N]", "the supermatter crystal at X Y Z: sets its internal energy (MeV) or its damage (100 starts the countdown), or reports its energy, integrity, status and gas"),
        new("smdelam", ConsoleRoute.Dev, "smdelam X Y [Z] [cascade|singularity|tesla|explosive]", "tg's admin path: the supermatter crystal at X Y Z delaminates now, with that strategy or the one it would pick"),
        new("singularity", ConsoleRoute.Dev, "singularity X Y [Z [ENERGY]]", "a gravitational singularity at X Y Z (energy 600 by default: stage 3, it roams and eats)"),
        new("weather", ConsoleRoute.Dev, "weather NAME|stop [Z]", "tg's admin Run Weather: the weather NAME (rad_storm, rain_storm, snow_storm, sand_storm, ash_storm, ...) on level Z (yours), or stop it there"),
        new("delete", ConsoleRoute.Dev, "delete WHO|X Y [Z]", "delete a thing (WHO: #id), or scrape away the turf at X Y Z (tg's admin Delete)", "del"),
        new("mount", ConsoleRoute.Dev, "mount WHO X Y [Z] north|south|east|west", "hang a wall mount (WHO: #id) on that side of the wall at X Y, for the round"),
        new("rotate", ConsoleRoute.Dev, "rotate WHO [cw|ccw|north|south|east|west]", "turn a thing (WHO: #id) a quarter or to a way, for the round", "rot"),
        new("air", ConsoleRoute.Dev, "air add GAS MOLES X Y [Z [R]]", "add moles of a gas (below 0: remove) to the air within R tiles of X Y"),
        new("air", ConsoleRoute.Dev, "air remove GAS X Y [Z [R]]", "take all of a gas out of the air within R tiles of X Y"),
        new("air", ConsoleRoute.Dev, "air temp K X Y [Z [R]]", "set the air's temperature in kelvin within R tiles of X Y"),
        new("air", ConsoleRoute.Dev, "air set air|vacuum|MIX X Y [Z [R]]", "replace the air within R tiles of X Y"),
        new("dev-help", ConsoleRoute.Dev, "dev-help", "the server's own text on the dev commands"),

        new("spawn", ConsoleRoute.Server, "spawn TYPE [X Y [Z]]", "any /obj or /mob at your turf, or at X Y Z"),
        new("teleport", ConsoleRoute.Server, "teleport WHO X Y [Z]", "move a player, or any movable by #id (teleport WHO OTHER: onto a player)", "tp"),
        new("list", ConsoleRoute.Server, "list", "the players and their mobs", "who"),
        new("kick", ConsoleRoute.Server, "kick WHO [REASON]", "disconnect a player"),
        new("status", ConsoleRoute.Server, "status", "tick, players and the last performance window"),
        new("hash", ConsoleRoute.Server, "hash", "the world's state hash (slow)"),
        new("quit", ConsoleRoute.Server, "quit", "shut the server down cleanly", "exit", "shutdown"),
    };

    /// <summary>The values a placeholder can take, the first ones the likeliest (<c>me</c> for WHO); what Tab completes.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Choices = new Dictionary<string, string[]>
    {
        ["WHO"] = new[] { "me" },
        ["KIND"] = new[] { "brute", "burn", "tox", "oxy" },
        ["ZONE"] = new[] { "head", "chest", "l_arm", "r_arm", "l_leg", "r_leg" },
        ["MODE"] = AirModes,
        ["GAS"] = new[] { "plasma", "o2", "n2", "co2", "n2o", "water_vapor", "tritium", "hydrogen", "bz", "freon" },
        ["ITEM"] = Items,
        ["TYPE"] = Items.Concat(new[]
        {
            "/mob/living/carbon/human", "/obj/machinery/portable_atmospherics/canister/oxygen",
            "/obj/machinery/portable_atmospherics/canister/plasma",
        }).ToArray(),
        ["VERB"] = Commands.Select(c => c.Verb).Distinct().ToArray(),
    };

    /// <summary>What a few placeholders mean, for the help's last lines and <c>help VERB</c>.</summary>
    public static readonly IReadOnlyDictionary<string, string> Placeholders = new Dictionary<string, string>
    {
        ["WHO"] = "a player's key, a world id (#123) or me",
        ["X"] = "X Y [Z]: turf coordinates (Z is 1 when left out)",
        ["MIX"] = "gases and a temperature, as o2=22;n2=82;TEMP=293.15",
        ["S"] = "seconds",
        ["GAS"] = "tg's gas id: n2, o2, co2, plasma, n2o, water_vapor, tritium, hydrogen...",
        ["R"] = "a radius of 0 to 7 tiles (0: the one turf)",
    };

    /// <summary>The colors of the help and the suggestions (BBCode, the panel's RichTextLabel).</summary>
    public const string UsageColor = "#e8f4e8";
    public const string DescriptionColor = "#8fa88f";
    public const string HeaderColor = "#c8f0c8";
    public const string CurrentColor = "#ffffff";
    public const string OtherWordColor = "#9cb09c";

    /// <summary>The color of a route's label.</summary>
    public static string RouteColor(ConsoleRoute route) => route switch
    {
        ConsoleRoute.Client => "#8cc8ff",
        ConsoleRoute.Server => "#ff9c8c",
        _ => "#ffd27a",
    };

    /// <summary>The commands of one name (spawn has two: the dev one and the server console's), case-insensitive.</summary>
    public static IEnumerable<ConsoleCommandInfo> Named(string name)
        => Commands.Where(c => c.Names.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)));

    /// <summary>A text with BBCode's brackets escaped.</summary>
    public static string Escape(string text) => (text ?? string.Empty).Replace("[", "[lb]");

    private static string Color(string color, string text) => $"[color={color}]{Escape(text)}[/color]";

    private static string Label(ConsoleRoute route) => Color(RouteColor(route), DevConsoleModel.Label(route));

    /// <summary>The help's lines, each as (plain, markup): the three groups, each command's syntax and description.</summary>
    public static IEnumerable<(string Plain, string Markup)> Help()
    {
        yield return Line(("Console commands", HeaderColor), ("   Tab completes, Up/Down: history, PageUp/PageDown: scroll, help VERB: one command", DescriptionColor));
        foreach ((ConsoleRoute route, string about) in new[]
        {
            (ConsoleRoute.Client, "run here, never sent"),
            (ConsoleRoute.Dev, "run by the server when its DEV_COMMANDS is on"),
            (ConsoleRoute.Server, "the server console's own, when the server runs with --client-console"),
        })
        {
            yield return Line(("", null));
            yield return Line((DevConsoleModel.Label(route), RouteColor(route)), (" " + about, DescriptionColor));
            foreach (ConsoleCommandInfo c in Commands.Where(c => c.Route == route))
                yield return Line(("  " + Column(c.Usage), UsageColor), (c.Description + AliasNote(c), DescriptionColor));
        }
        yield return Line(("", null));
        foreach (string key in new[] { "WHO", "X" })
            yield return Line((key == "X" ? "" : key + ": ", HeaderColor), (Placeholders[key], DescriptionColor));
    }

    /// <summary><c>help VERB</c>: each command of that name with its syntax, description, other names and the values of its placeholders.</summary>
    public static IEnumerable<(string Plain, string Markup)> HelpFor(string verb)
    {
        ConsoleCommandInfo[] found = Named(verb).ToArray();
        if (found.Length == 0)
        {
            yield return Line(($"no command '{verb}'", DescriptionColor));
            foreach (var line in Suggestions(verb))
                yield return line;
            yield break;
        }
        foreach (ConsoleCommandInfo c in found)
        {
            yield return Line((DevConsoleModel.Label(c.Route) + " ", RouteColor(c.Route)), (c.Usage, UsageColor));
            yield return Line(("  " + c.Description + AliasNote(c), DescriptionColor));
            foreach (string token in Tokens(c.Usage).Skip(1).SelectMany(t => t.Split('|')).Distinct())
            {
                if (Placeholders.TryGetValue(token, out string meaning))
                    yield return Line(("  " + (token == "X" ? "" : token + ": "), HeaderColor), (meaning, DescriptionColor));
                else if (Choices.TryGetValue(token, out string[] values) && token != "VERB")
                    yield return Line(("  " + token + ": ", HeaderColor), (string.Join(", ", values) + (token is "ITEM" or "TYPE" ? ", or any other path" : ""), DescriptionColor));
            }
        }
    }

    private static string Column(string usage) => usage.Length < UsageColumn - 1 ? usage.PadRight(UsageColumn) : usage + "  ";

    private static string AliasNote(ConsoleCommandInfo c) => c.Aliases.Length == 0 ? "" : " (also " + string.Join(", ", c.Aliases) + ")";

    private static (string, string) Line(params (string Text, string Color)[] parts)
        => (string.Concat(parts.Select(p => p.Text)), string.Concat(parts.Select(p => p.Color == null ? Escape(p.Text) : Color(p.Color, p.Text))));

    /// <summary>The words of a usage, brackets dropped (<c>[X Y [Z]]</c> is X, Y, Z).</summary>
    private static string[] Tokens(string usage)
        => usage.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim('[', ']')).ToArray();

    private static bool IsPlaceholder(string word) => word.Length > 0 && word.All(ch => char.IsUpper(ch) || char.IsDigit(ch));

    /// <summary>The values a usage word can take: its literal alternatives and the placeholders' <see cref="Choices"/>.</summary>
    private static IEnumerable<string> ValuesOf(string token)
    {
        foreach (string alternative in token.Split('|'))
        {
            if (!IsPlaceholder(alternative))
                yield return alternative;
            else if (Choices.TryGetValue(alternative, out string[] values))
                foreach (string v in values)
                    yield return v;
        }
    }

    /// <summary>Splits a line into its words and the word being typed: its index and its typed part (empty after a space).</summary>
    private static (string[] Words, int Index, string Prefix) Parse(string line)
    {
        string text = (line ?? string.Empty).TrimStart();
        string[] words = text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        bool afterSpace = text.Length > 0 && char.IsWhiteSpace(text[^1]);
        if (words.Length == 0)
            return (words, 0, string.Empty);
        return afterSpace ? (words, words.Length, string.Empty) : (words, words.Length - 1, words[^1]);
    }

    /// <summary>
    /// The commands a line may be: while the verb is typed, those with a name that starts with it (an exact name first); after
    /// it, those of that name. None for an empty line.
    /// </summary>
    public static IReadOnlyList<ConsoleCommandInfo> Matches(string line)
    {
        (string[] words, int index, string prefix) = Parse(line);
        if (words.Length == 0)
            return Array.Empty<ConsoleCommandInfo>();
        if (index > 0)
            return Named(words[0]).ToArray();
        return Commands
            .Where(c => c.Names.Any(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(c => c.Names.Any(n => string.Equals(n, prefix, StringComparison.OrdinalIgnoreCase)) ? 0 : 1)
            .ToArray();
    }

    /// <summary>
    /// The rest of the word being typed that Tab adds (shown greyed after the caret): the first name of a matching command
    /// while the verb is typed, else the first value of the usage word at the caret that starts with the typed part. Empty
    /// when there is none, or the word is already whole.
    /// </summary>
    public static string Completion(string line)
    {
        (string[] words, int index, string prefix) = Parse(line);
        if (words.Length == 0)
            return string.Empty;
        IEnumerable<string> candidates = index == 0
            ? Commands.SelectMany(c => c.Names).Where(n => n != "?")
            : Named(words[0]).SelectMany(c => Tokens(c.Usage).Skip(index).Take(1)).SelectMany(ValuesOf);
        if (index == 0 && Commands.SelectMany(c => c.Names).Any(n => string.Equals(n, prefix, StringComparison.OrdinalIgnoreCase)))
            return string.Empty; // a whole verb ("god" is not completed to "godmode")
        string pick = candidates.FirstOrDefault(n => n.Length > prefix.Length && n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return pick == null ? string.Empty : pick.Substring(prefix.Length);
    }

    /// <summary>The line with <see cref="Completion"/> taken, and a space after it when the command takes another word.</summary>
    public static string Complete(string line)
    {
        string rest = Completion(line);
        if (rest.Length == 0)
            return line;
        string done = line + rest;
        (string[] words, int index, _) = Parse(done);
        bool more = Named(words[0]).Any(c => Tokens(c.Usage).Length > index + 1);
        return more ? done + " " : done;
    }

    /// <summary>
    /// The suggestion lines under the line, as (plain, markup): while the verb is typed, the matching commands with their
    /// descriptions (all of them; the panel scrolls past <see cref="MaxSuggestions"/>); after it, the command's syntax with the word at the caret
    /// highlighted, then that word's values (those the typed part can still become) and meaning. Empty for an empty line or an unknown verb.
    /// </summary>
    public static IReadOnlyList<(string Plain, string Markup)> Suggestions(string line)
    {
        (_, int index, string prefix) = Parse(line);
        IReadOnlyList<ConsoleCommandInfo> matches = Matches(line);
        var lines = new List<(string, string)>();
        if (index == 0)
        {
            foreach (ConsoleCommandInfo c in matches)
                lines.Add(Line((DevConsoleModel.Label(c.Route).PadRight(9), RouteColor(c.Route)), (Column(c.Usage), UsageColor), (c.Description, DescriptionColor)));
            return lines;
        }
        foreach (ConsoleCommandInfo c in matches)
        {
            string[] tokens = c.Usage.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var plain = new StringBuilder(DevConsoleModel.Label(c.Route)).Append(' ');
            var markup = new StringBuilder(Color(RouteColor(c.Route), DevConsoleModel.Label(c.Route) + " "));
            for (int i = 0; i < tokens.Length; i++)
            {
                string sep = i == 0 ? "" : " ";
                plain.Append(sep).Append(tokens[i]);
                markup.Append(sep).Append(i == index ? $"[b][color={CurrentColor}]{Escape(tokens[i])}[/color][/b]" : Color(OtherWordColor, tokens[i]));
            }
            plain.Append("   ").Append(c.Description);
            markup.Append("   ").Append(Color(DescriptionColor, c.Description));
            lines.Add((plain.ToString(), markup.ToString()));
            if (index >= tokens.Length)
                continue;
            string token = tokens[index].Trim('[', ']');
            string[] values = ValuesOf(token).Where(v => v != "me").Distinct().ToArray();
            string[] typed = values.Where(v => v.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (typed.Length > 0)
                values = typed; // the ones the typed part can still become
            if (values.Length > 0)
                lines.Add(Line(("  " + string.Join("  ", values.Take(8)) + (values.Length > 8 ? "  ..." : ""), DescriptionColor)));
            foreach (string word in token.Split('|').Where(IsPlaceholder))
                if (Placeholders.TryGetValue(word, out string meaning))
                    lines.Add(Line(("  " + (word == "X" ? "" : word + ": "), HeaderColor), (meaning, DescriptionColor)));
        }
        return lines;
    }
}
