using System;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/PreferencesMenu (M10 slice F, D267: the lobby's
// character setup). Engine-free (compiled into the tests too). Translated: the window and its title (index.tsx), the character window's
// character slots and page buttons with the Character page (CharacterPreferences/index.tsx), and of the main page (MainPage.tsx) the
// gender button (GenderButton, its four genders and icons from preferences/gender.ts), the delete character button with its popup
// (DeleteCharacterPopup.tsx), the name input (names.tsx NameInput, with the real name's randomize from MultiNameInput) and the age
// (the non-contextual list's FeatureNumberInput, features/character_preferences/age.tsx); the Occupations page (JobsPage.tsx: the
// departments' job rows with their priority buttons and assigned characters, Show Characters' characters dragged onto the jobs,
// and the jobless role dropdown).
// M10x slice D4 (lobby.md 8): the main features (MainFeature's 48 px buttons, ChoicedSelection's searchable grid of the constant data's
// pictures, DV44: it opens beside the column on a click, where tg floats it), FeatureColorInput, and the two PreferenceLists with
// the inputs of the ported features (preferences/features: colour, dropdown, number, checkbox, RandomizationButton). M10x: the
// randomization buttons (RandomizationButton.tsx, getRandomization, useRandomToggleState, random_body's Randomize and Cancel,
// random_species) and the character preview (CharacterPreview's ByondUi map drawn as a 2D picture, with CharacterControls' Rotate
// and the Loadout page's two arrows under it, D284, DV210).
// Syndicate A3 (D316, revising D281's "not now" for this page): the Antagonists page (CharacterPreferences/AntagsPage.tsx with the
// antagonists of antagonists/antagonists/*.ts: Roundstart, Midround and Latejoin, each with Enable All and Disable All and the
// antagonists' round pictures; the choices are predicted on the client, as tg's, and sent as set_antags).
// Gaps SP1 (D338): the species page (CharacterPreferences/SpeciesPage.tsx: the species' 64 px pictures, DV44, their description,
// diet, perks, the preview and the lore) and CharacterControls' species button, which hides the gender for a species without sexes;
// the lizard's and the felinid's feature names (species_features.tsx).
// Not translated (not-translated.md 79): the other pages (loadout, quirks) and the game preferences window.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class PreferencesMenu
{
    // preferences/gender.ts
    private static readonly (string Gender, string Icon, string Text)[] Genders =
    {
        ("male", "mars", "He/Him"),
        ("female", "venus", "She/Her"),
        ("plural", "transgender", "They/Them"),
        ("neuter", "neuter", "It/Its"),
    };

    // PreferencesMenu/index.tsx PrefsWindowInner: the window by data.window (PrefsWindow.Character 0, Game 1, Keybindings 2)
    public static UiNode Render(TguiContext c)
    {
        int window = (int)c.Data.Num("window");
        string title = window switch { 1 => "Game Preferences", 2 => "Keybindings", _ => "Character Preferences" };
        UiNode content = window == 0
            ? CharacterWindow(c)
            : new UiNode(UiKind.NoticeBox) { Key = "game", Text = "The game preferences are not ported yet." };
        return c.Window(920, 770, title, content);
    }

    // CharacterPreferences/index.tsx CharacterPreferenceWindow: the page in useState (Page.Main first) and the PageButtons that set it
    private static UiNode CharacterWindow(TguiContext c)
    {
        string current = c.UseState("page", "Character");
        // PageButton's otherActivePages: the Character button stays selected on the species page
        UiNode Page(string text, bool ported) => new UiNode(UiKind.StackItem) { Key = "page:" + text, Grow = 1 }.With(new UiNode(UiKind.Button)
        {
            Text = text, Fluid = true, TextAlign = "center", Selected = current == text || text == "Character" && current == "Species", Disabled = !ported,
            Tooltip = ported ? null : "Not ported yet",
            OnClick = () => c.SetState("page", text),
        });
        var window = new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "profiles" }.With(CharacterProfiles(c)),
            new UiNode(UiKind.Divider) { Key = "profilesDivider" },
            new UiNode(UiKind.StackItem) { Key = "pages" }.With(new UiNode(UiKind.Stack) { Fill = true }.With(
                Page("Character", true), Page("Loadout", false), Page("Occupations", true), Page("Antagonists", true), Page("Quirks and Personality", false))),
            new UiNode(UiKind.Divider) { Key = "divider" },
            new UiNode(UiKind.StackItem) { Key = "content", Grow = 1 }.With(current switch
            {
                "Occupations" => JobsPage(c),
                "Antagonists" => AntagsPage(c),
                "Species" => SpeciesPage(c),
                _ => MainPage(c),
            }));
        if (c.Data.Get("read_only")?.Truthy == true)
        {
            ReadOnly(window);
            window.Children.Insert(0, new UiNode(UiKind.StackItem) { Key = "readOnly" }.With(new UiNode(UiKind.NoticeBox) { Text = "The character can be changed only in the lobby." }));
        }
        return window;
    }

    // DEVIATION(DV215): in the round the character is only looked at (D286): every input is disabled but the page buttons and the
    // preview's turns (Rotate and the arrows) and Job Clothes (the clothes worn now); the server refuses the edits too. Syndicate A3: the
    // Antagonists page stays open, as tg's (its choices are not the character's).
    private static void ReadOnly(UiNode window)
    {
        HashSet<UiNode> pages = window.Walk().Where(n => n.Key?.StartsWith("page:", StringComparison.Ordinal) == true || n.Key == "antags").SelectMany(n => n.Walk()).ToHashSet();
        foreach (UiNode n in window.Walk())
            if (n.Kind is UiKind.Button or UiKind.Dropdown or UiKind.Input or UiKind.NumberInput && !pages.Contains(n)
                && n.Icon is not ("chevron-left" or "chevron-right") && n.Tooltip != "Rotate" && n.Text != "Job Clothes")
                n.Disabled = true;
    }

    // CharacterProfiles: a button per save slot, the active one selected ("New Character" for an empty slot). tg's "Buy BYOND premium
    // for more slots!" line is not drawn: the port has no BYOND membership.
    private static UiNode CharacterProfiles(TguiContext c)
    {
        int active = (int)c.Data.Num("active_slot", 1) - 1;
        var profiles = c.Data.Get("character_profiles")?.Items ?? Array.Empty<UiValue>();
        return new UiNode(UiKind.Stack) { Justify = "center" }.With(profiles.Select((profile, slot) =>
            new UiNode(UiKind.StackItem) { Key = "slot:" + slot }.With(new UiNode(UiKind.Button)
            {
                Text = profile.Kind == UiValueKind.Null ? "New Character" : profile.Text, Selected = slot == active, Fluid = true,
                OnClick = () => c.Act("change_slot", ("slot", slot + 1)),
            })));
    }

    // types.ts character_profiles: the names of the filled slots
    private static string[] ProfileNames(TguiContext c)
        => (c.Data.Get("character_profiles")?.Items ?? Array.Empty<UiValue>()).Select(p => p.Kind == UiValueKind.Null ? null : p.Text).ToArray();

    // ---- AntagsPage.tsx (syndicate A3) ----

    // antagonists/base.ts Category and Antagonist: the key is the antag flag made lowercase without non-alphanumerics (serialize_antag_name)
    internal enum Category { Roundstart, Midround, Latejoin }

    internal sealed record Antagonist(string Key, string Name, Category Category, int? Priority, params string[] Description);

    // antagonists/antagonists/*.ts (each description's whitespace collapsed as the page shows it)
    internal static readonly Antagonist[] Antagonists =
    {
        new("abductor", "Abductor", Category.Midround, null, "Abductors are technologically advanced alien society set on cataloging all species in the system. Unfortunately for their subjects their methods are quite invasive.", "You and a partner will become the abductor scientist and agent duo. As an agent, abduct unassuming victims and bring them back to your UFO. As a scientist, scout out victims for your agent, keep them safe, and operate on whoever they bring back."),
        new("blob", "Blob", Category.Midround, null, "The blob infests the station and destroys everything in its path, including hull, fixtures, and creatures. Spread your mass, collect resources, and consume the entire station. Make sure to prepare your defenses, because the crew will be alerted to your presence!"),
        new("blobinfection", "Blob Infection", Category.Midround, null, "At any point in the middle of the shift, be strucken with an infection that will turn you into the terrifying blob.", "The blob infests the station and destroys everything in its path, including hull, fixtures, and creatures. Spread your mass, collect resources, and consume the entire station. Make sure to prepare your defenses, because the crew will be alerted to your presence!"),
        new("bloodbrother", "Blood Brother", Category.Roundstart, null, "Team up with other crew members as blood brothers to combine the strengths of your departments, break each other out of prison, and overwhelm the station."),
        new("bloodworm", "Blood Worm", Category.Roundstart, null, "Become a giant, parasitic blood worm. Start as a hatchling in a host, consume blood and conquer the entire station alongside your siblings!"),
        new("bloodworminfestation", "Blood Worm Infestation", Category.Midround, null, "Become a giant, parasitic blood worm. Start as a hatchling, take hosts, consume blood and conquer the entire station alongside your siblings!"),
        new("changeling", "Changeling", Category.Roundstart, null, "A highly intelligent alien predator that is capable of altering their shape to flawlessly resemble a human.", "Transform yourself or others into different identities, and buy from an arsenal of biological weaponry with the DNA you collect."),
        new("changelingmidround", "Space Changeling", Category.Midround, null, "A midround changeling does not receive a crew identity, instead arriving from space. This will be more difficult than being a round-start changeling!", "Transform yourself or others into different identities, and buy from an arsenal of biological weaponry with the DNA you collect."),
        new("clownoperative", "Clown Operative", Category.Roundstart, null, "Honk! You have been chosen, for better or worse to join the Syndicate Clown Operative strike team. Your mission, whether or not you choose to tickle it, is to honk Nanotrasen's most advanced research facility! That's right, you're going to Clown Station 13.", "Retrieve the nuclear authentication disk, use it to activate the nuclear fission explosive, and destroy the station."),
        new("clownoperativemidround", "Clown Ass-ailant", Category.Midround, null, "A form of clown operative that is offered to ghosts in the middle of the shift.", "Retrieve the nuclear authentication disk, use it to activate the nuclear fission explosive, and destroy the station."),
        new("cultist", "Cultist", Category.Roundstart, null, "The Geometer of Blood, Nar-Sie, has sent a number of her followers to Space Station 13. As a cultist, you have an abundance of cult magics at your disposal, something for all situations. You must work with your brethren to summon an avatar of your eldritch goddess!", "Armed with blood magic, convert crew members to the Blood Cult, sacrifice those who get in the way, and summon Nar-Sie."),
        new("fugitive", "Fugitive", Category.Midround, null, "Wherever you come from, you're being hunted. You have 10 minutes to prepare before fugitive hunters arrive and start hunting you and your friends down!"),
        new("glitch", "Glitch", Category.Midround, null, "The virtual domain is a dangerous place for bitrunners. Make it so.", "You are a short-term antagonist, a glitch in the system. Use martial arts \\ and lethal weaponry to terminate organics."),
        new("headrevolutionary", "Head Revolutionary", Category.Roundstart, null, "VIVA LA REVOLUTION!", "Armed with a flash, convert as many people to the revolution as you can. Kill or exile all heads of staff on the station."),
        new("heretic", "Heretic", Category.Roundstart, null, "Forgotten, devoured, gutted. Humanity has forgotten the eldritch forces of decay, but the mansus veil has weakened. We will make them taste fear again...", "Find hidden influences and sacrifice crew members to gain magical powers and ascend as one of several paths."),
        new("loneoperative", "Lone Operative", Category.Midround, null, "A solo nuclear operative that has a higher chance of spawning the longer the nuclear authentication disk stays in one place.", "Retrieve the nuclear authentication disk, use it to activate the nuclear fission explosive, and destroy the station."),
        new("malfai", "Malfunctioning AI", Category.Roundstart, null, "With a law zero to complete your objectives at all costs, combine your omnipotence and malfunction modules to wreak havoc across the station. Go delta to destroy the station and all those who opposed you."),
        new("malfaimidround", "Value Drifted AI", Category.Midround, null, "A form of malfunctioning AI that is given to existing AIs in the middle of the shift.", "With a law zero to complete your objectives at all costs, combine your omnipotence and malfunction modules to wreak havoc across the station. Go delta to destroy the station and all those who opposed you."),
        new("nightmare", "Nightmare", Category.Midround, null, "Use your light eater to break sources of light to survive and thrive. Jaunt through the darkness and seek your prey with night vision."),
        new("obsessed", "Obsessed", Category.Midround, null, "You're obsessed with someone! Your obsession may begin to notice their personal items are stolen and their coworkers have gone missing, but will they realize they are your next victim in time?"),
        new("operative", "Nuclear Operative", Category.Roundstart, null, "Congratulations, agent. You have been chosen to join the Syndicate Nuclear Operative strike team. Your mission, whether or not you choose to accept it, is to destroy Nanotrasen's most advanced research facility! That's right, you're going to Space Station 13.", "Retrieve the nuclear authentication disk, use it to activate the nuclear fission explosive, and destroy the station."),
        new("operativemidround", "Nuclear Assailant", Category.Midround, null, "A form of nuclear operative that is offered to ghosts in the middle of the shift.", "Retrieve the nuclear authentication disk, use it to activate the nuclear fission explosive, and destroy the station."),
        new("paradoxclone", "Paradox Clone", Category.Midround, null, "A freak time-space anomaly has teleported you into another reality! Now you have to find your counterpart and kill and replace them."),
        new("provocateur", "Provocateur", Category.Latejoin, null, "A form of head revolutionary that can activate when joining an ongoing shift.", "Armed with a flash, convert as many people to the revolution as you can. Kill or exile all heads of staff on the station."),
        new("revenant", "Revenant", Category.Midround, null, "Become the mysterious revenant. Break windows, overload lights, and eat the crew's life force, all while talking to your old community of disgruntled ghosts."),
        new("sentiencepotionspawn", "Sentient Creature", Category.Midround, null, "Either by cosmic happenstance, or due to crew's shenanigans, you have been given sentience!", "This is a blanket preference. The more benign ones include random human level intelligence events, the cargorilla, and creatures uplifted via sentience potions. The less friendly ones include the regal rat, and the boosted mining elite mobs."),
        new("spacedragon", "Space Dragon", Category.Midround, null, "Become a ferocious space dragon. Breathe fire, summon an army of space carps, crush walls, and terrorize the station."),
        new("spaceninja", "Space Ninja", Category.Midround, null, "The Spider Clan practice a sort of augmentation of human flesh in order to achieve a more perfect state of being and follow Postmodern Space Bushido.", "Become a conniving space ninja, equipped with a katana, gloves to hack into airlocks and APCs, a suit to make you go near-invisible, as well as a variety of abilities in your kit. Hack into arrest consoles to mark everyone as arrest, and even hack into communication consoles to summon more threats to cause chaos on the station!"),
        new("spy", "Spy", Category.Roundstart, null, "Your mission, should you choose to accept it: Infiltrate Space Station 13. Disguise yourself as a member of their crew and steal vital equipment. Should you be caught or killed, your employer will disavow any knowledge of your actions. Good luck agent.", "Complete Spy Bounties to earn rewards from your employer. Use these rewards to sow chaos and mischief!"),
        new("stowawaychangeling", "Stowaway Changeling", Category.Latejoin, null, "A Changeling that found its way onto the shuttle unbeknownst to the crewmembers on board.", "Transform yourself or others into different identities, and buy from an arsenal of biological weaponry with the DNA you collect."),
        new("syndicateinfiltrator", "Syndicate Infiltrator", Category.Latejoin, -1, "A form of traitor that can activate when joining an ongoing shift.", "Start with an uplink to purchase your gear and take on your sinister objectives. Ascend through the ranks and become an infamous legend."),
        new("syndicatesleeperagent", "Syndicate Sleeper Agent", Category.Midround, -1, "A form of traitor that can activate at any point in the middle of the shift.", "Start with an uplink to purchase your gear and take on your sinister objectives. Ascend through the ranks and become an infamous legend."),
        new("traitor", "Traitor", Category.Roundstart, -1, "An unpaid debt. A score to be settled. Maybe you were just in the wrong place at the wrong time. Whatever the reasons, you were selected to infiltrate Space Station 13.", "Start with an uplink to purchase your gear and take on your sinister objectives. Ascend through the ranks and become an infamous legend."),
        new("voidwalker", "Voidwalker", Category.Midround, null, "Move through space and pull people into the void. Declare solars an independent nation."),
        new("wizard", "Wizard", Category.Roundstart, null, "\"GREETINGS. WE'RE THE WIZARDS OF THE WIZARD'S FEDERATION.\"", "Choose between a variety of powerful spells in order to cause chaos among Space Station 13."),
        new("wizardmidround", "Wizard (Midround)", Category.Midround, null, "A form of wizard that is offered to ghosts in the middle of the shift.", "Choose between a variety of powerful spells in order to cause chaos among Space Station 13."),
        new("xenomorph", "Xenomorph", Category.Midround, null, "Become the extraterrestrial xenomorph. Start as a larva, and progress your way up the caste, including even the Queen!"),
    };

    // AntagsPage.tsx:24 binaryInsertAntag: each category sorted by `${priority}_${name}` (an unset priority is "undefined")
    internal static IEnumerable<Antagonist> AntagsOf(Category category)
        => Antagonists.Where(a => a.Category == category).OrderBy(a => (a.Priority?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "undefined") + "_" + a.Name, StringComparer.Ordinal);

    // AntagsPage.tsx:224 AntagsPage
    private static UiNode AntagsPage(TguiContext c)
        => new UiNode(UiKind.Box) { Key = "antags" }.With(
            AntagSelection(c, "Roundstart", AntagsOf(Category.Roundstart).ToArray()),
            AntagSelection(c, "Midround", AntagsOf(Category.Midround).ToArray()),
            AntagSelection(c, "Latejoin", AntagsOf(Category.Latejoin).ToArray()));

    // AntagsPage.tsx:53 AntagSelection: the section's predicted state starts as the server's selected_antags
    private static UiNode AntagSelection(TguiContext c, string name, Antagonist[] antagonists)
    {
        string stateKey = "antags:" + name;
        string[] predicted = c.UseState(stateKey, c.Data.Arr("selected_antags").Select(v => v.AsStr()).ToArray());
        var predictedState = new HashSet<string>(predicted, StringComparer.Ordinal);
        IReadOnlyList<UiValue> bans = c.Data.Arr("antag_bans");
        UiValue daysLeftData = c.Data.Get("antag_days_left");
        UiValue icons = c.Data.Get("antagonist_icons");

        void Toggle(string[] keys, bool toggled)
        {
            var newState = new HashSet<string>(predictedState, StringComparer.Ordinal);
            foreach (string key in keys)
            {
                if (toggled)
                    newState.Add(key);
                else
                    newState.Remove(key);
            }
            c.SetState(stateKey, newState.ToArray());
            c.Act("set_antags", ("antags", UiValue.ListOf(keys.Select(k => UiValue.Of(k)))), ("toggled", toggled));
        }

        string[] antagonistKeys = antagonists.Select(a => a.Key).ToArray();
        var flex = new UiNode(UiKind.Stack) { Key = "list", Wrap = true, Align = "flex-end" };
        foreach (Antagonist antagonist in antagonists)
        {
            bool isBanned = bans.Any(b => b.AsStr() == antagonist.Key);
            double daysLeft = daysLeftData?.Get(antagonist.Key)?.AsNum() ?? 0;
            string state = isBanned || daysLeft > 0 ? "banned" : predictedState.Contains(antagonist.Key) ? "on" : "off";
            UiValue picture = icons?.Get(antagonist.Key);
            // .antagonist-icon-parent: a round 96 px picture with the state's border (green on, red off, grey banned); the picture is
            // dimmed when off or banned
            var parent = new UiNode(UiKind.Button)
            {
                Key = "icon", Circular = true, Width = "96px", Height = "96px", Color = state switch { "on" => "green", "off" => "red", _ => "grey" },
                Tooltip = isBanned ? "You are banned from " + antagonist.Name + "." : string.Join("\n\n", antagonist.Description),
                Slashed = isBanned, Selected = state == "on",
                OnClick = () =>
                {
                    if (isBanned)
                        return;
                    Toggle(new[] { antagonist.Key }, !predictedState.Contains(antagonist.Key));
                },
            };
            if (picture != null && picture.Items.Count >= 2)
                parent.With(new UiNode(UiKind.Box) { Key = "picture", DmIcon = picture.Items[0].AsStr(), DmIconState = picture.Items[1].AsStr(), Size = 3, Opacity = state == "on" ? null : 0.5 });
            if (daysLeft > 0)
                parent.With(new UiNode(UiKind.Box) { Key = "days", Bold = false, Text = daysLeft.ToString(System.Globalization.CultureInfo.InvariantCulture) + " days left" });
            flex.With(new UiNode(UiKind.StackItem) { Key = "antag:" + antagonist.Key }.With(new UiNode(UiKind.Stack) { Vertical = true, Align = "center" }.With(
                new UiNode(UiKind.StackItem) { Key = "name", Width = "100px" }.With(new UiNode(UiKind.Box) { Bold = true, TextAlign = "center", Text = antagonist.Name }),
                new UiNode(UiKind.StackItem) { Key = "parent", Align = "center" }.With(parent))));
        }

        return new UiNode(UiKind.Section)
        {
            Key = "section:" + name, Title = name,
            Buttons = new UiNode(UiKind.Fragment).With(
                new UiNode(UiKind.Button) { Key = "enable", Color = "good", Text = "Enable All", OnClick = () => Toggle(antagonistKeys, true) },
                new UiNode(UiKind.Button) { Key = "disable", Color = "bad", Text = "Disable All", OnClick = () => Toggle(antagonistKeys, false) }),
        }.With(flex);
    }

    // ---- JobsPage.tsx ----

    // types.ts JobPriority and JoblessRole
    private const int PriorityLow = 1, PriorityMedium = 2, PriorityHigh = 3;
    private static readonly (int Value, string Text)[] JoblessRoles =
    {
        (1, "Join as {0} if unavailable"), (2, "Join as a random job if unavailable"), (3, "Return to lobby if unavailable"),
    };

    // JobsPage: the jobless role dropdown over three columns of departments, each column under its priority headers
    private static UiNode JobsPage(TguiContext c)
    {
        UiNode Column(string key, params string[] departments) => new UiNode(UiKind.StackItem) { Key = key, Grow = 1 }.With(
            new UiNode(UiKind.Stack) { Vertical = true }.With(new[] { PriorityHeaders() }.Concat(departments.Select(d => Department(c, d))).Where(n => n != null)));
        return new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "top" }.With(new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem) { Key = "characters", Grow = 1 }.With(CharacterSection(c)),
                new UiNode(UiKind.StackItem) { Key = "jobless", Width = "280px" }.With(JoblessRoleDropdown(c)))),
            new UiNode(UiKind.StackItem) { Key = "departments" }.With(new UiNode(UiKind.Stack) { Fill = true }.With(
                Column("left", "Engineering", "Science", "Silicon", "Assistant"),
                Column("middle", "Captain", "Service", "Cargo"),
                Column("right", "Security", "Medical"))));
    }

    // CharacterSection: Show Characters opens tg's menu of the filled slots, each one dragged onto a job's name (JobRow). The menu is
    // drawn in the page's flow, under the button, where tg floats it and hides it while a character is dragged.
    private static UiNode CharacterSection(TguiContext c)
    {
        bool open = c.UseState("charactersOpen", false);
        var section = new UiNode(UiKind.Stack) { Vertical = true }.With(new UiNode(UiKind.StackItem) { Key = "show" }.With(new UiNode(UiKind.Button)
        {
            Icon = "angle-down", Text = "Show Characters", OnClick = () => c.SetState("charactersOpen", !open),
        }));
        if (!open)
            return section;
        string[] profiles = ProfileNames(c);
        return section.With(new UiNode(UiKind.StackItem) { Key = "menu" }.With(new UiNode(UiKind.Section).With(
            new UiNode(UiKind.Stack) { Vertical = true }.With(
                new UiNode(UiKind.StackItem) { Key = "hint" }.With(new UiNode(UiKind.Box)
                {
                    TextAlign = "center", Wrap = true,
                    Text = "Drag a character to a job to load that character if you are selected for that job.",
                }),
                new UiNode(UiKind.StackItem) { Key = "profiles" }.With(new UiNode(UiKind.Stack).With(profiles
                    .Select((profile, index) => (profile, index))
                    .Where(p => p.profile != null)
                    .Select(p => new UiNode(UiKind.StackItem) { Key = "profile:" + p.index }.With(new UiNode(UiKind.Button)
                    {
                        Text = p.profile, Color = "transparent",
                        OnDragStart = () => c.SetState("dragging", p.index),
                        OnDragEnd = () => { c.SetState("dragging", -1); c.SetState("hoveringOver", ""); },
                    }))))))));
    }

    // PriorityHeaders (level: tg turns them 315 degrees over the buttons)
    private static UiNode PriorityHeaders() => new UiNode(UiKind.StackItem) { Key = "headers" }.With(new UiNode(UiKind.Stack).With(
        new[] { new UiNode(UiKind.StackItem) { Key = "grow", Grow = 1 } }.Concat(new[] { "Off", "Low", "Med", "High" }.Select(h =>
            new UiNode(UiKind.StackItem) { Key = "h:" + h, Width = "22px" }.With(new UiNode(UiKind.Box) { Text = h, Bold = true, FontSize = "10px", TextAlign = "center" })))));

    // JoblessRoleDropdown: the character's joblessrole preference
    private static UiNode JoblessRoleDropdown(TguiContext c)
    {
        string overflow = c.Data.Str("overflow_role", "");
        string[] options = JoblessRoles.Select(r => string.Format(System.Globalization.CultureInfo.InvariantCulture, r.Text, overflow)).ToArray();
        double selected = c.Data.Get("character_preferences")?.Get("misc")?.Num("joblessrole", 0) ?? 0;
        int at = Array.FindIndex(JoblessRoles, r => r.Value == (int)selected);
        return new UiNode(UiKind.Dropdown)
        {
            Key = "joblessrole", Options = options, TextValue = at >= 0 ? options[at] : null, Width = "100%",
            OnSelected = text =>
            {
                int i = Array.IndexOf(options, text);
                if (i >= 0)
                    c.Act("set_preference", ("preference", "joblessrole"), ("value", JoblessRoles[i].Value));
            },
        };
    }

    // Department: its jobs in jobs_sorted order, the head's row darker and bold (the stylesheet's .head: colors.bg)
    private static UiNode Department(TguiContext c, string name)
    {
        UiValue jobsData = c.Data.Get("constant_data")?.Get("jobs");
        UiValue department = jobsData?.Get("departments")?.Get(name);
        // This isn't necessarily a bug: a department can be removed without editing the UI.
        if (department == null)
            return null;
        string color = department.Str("color", "#808080");
        string head = department.Str("head", null);
        UiValue jobs = jobsData.Get("jobs");
        var rows = jobsData.Get("jobs_sorted")?.Items
            .Select(j => j.Text)
            .Where(j => jobs?.Get(j)?.Str("department", null) == name)
            .Select(j => JobRow(c, j, jobs.Get(j), j == head ? JobSelection.Darken(color, 15) : color, j == head));
        return new UiNode(UiKind.StackItem) { Key = "department:" + name }.With(new UiNode(UiKind.Stack) { Vertical = true }.With(rows ?? Enumerable.Empty<UiNode>()));
    }

    // JobRow: the job's name (its description as the tooltip) and its right side: the hours or days it still needs, Banned, or the
    // priority buttons
    private static UiNode JobRow(TguiContext c, string name, UiValue job, string background, bool isHead)
    {
        bool isOverflow = c.Data.Str("overflow_role", null) == name;
        UiValue preference = c.Data.Get("job_preferences")?.Items.FirstOrDefault(p => p.Str("job", null) == name);
        int priority = (int)(preference?.Num("priority", 0) ?? 0);
        UiValue experience = c.Data.Get("job_required_experience")?.Get(name);
        double daysLeft = c.Data.Get("job_days_left")?.Num(name, 0) ?? 0;
        bool banned = c.Data.Get("job_bans")?.Items.Any(b => b.Text == name) == true;

        UiNode Right(string text) => new UiNode(UiKind.Box) { Text = text, TextAlign = "right", Color = "black" };
        UiNode rightSide;
        if (experience != null)
            rightSide = Right(Math.Ceiling(experience.Num("required_playtime", 0) / 60) + "h as " + experience.Str("experience_type", ""));
        else if (daysLeft > 0)
            rightSide = Right(daysLeft + " day" + (daysLeft == 1 ? "" : "s") + " left");
        else if (banned)
            rightSide = Right("Banned");
        else
            rightSide = PriorityButtons(c, name, isOverflow, priority);

        // the name, where a dragged character is dropped (tg's onDrop: set_job_to_profile, the slot 1-indexed); lighter while a drag is
        // over it (the stylesheet's .job-name.hovered)
        int dragging = c.UseState("dragging", -1);
        bool hovered = c.UseState("hoveringOver", "") == name;
        var nameNode = new UiNode(UiKind.Box)
        {
            Text = name, Color = "black", Bold = isHead, Tooltip = job?.Str("description", null), FontSize = isHead && name == "Captain" ? "17px" : null,
            OnDragEnter = () => c.SetState("hoveringOver", name),
            OnDrop = () =>
            {
                if (dragging >= 0)
                    c.Act("set_job_to_profile", ("job", name), ("profile", dragging + 1));
                c.SetState("dragging", -1);
                c.SetState("hoveringOver", "");
            },
        };
        var nameColumn = new UiNode(UiKind.Stack) { Vertical = true }.With(new UiNode(UiKind.StackItem)
        {
            Key = "jobName", Background = hovered ? JobSelection.Lighten(background, 15) : null,
        }.With(nameNode));

        // the assigned character: "↳ name" and its times button
        double? slot = preference?.Get("assigned_profile_slot") is { Kind: not UiValueKind.Null } s ? s.Number : null;
        string[] profiles = ProfileNames(c);
        if (slot is double assigned && assigned >= 1 && assigned <= profiles.Length)
            nameColumn.With(new UiNode(UiKind.StackItem) { Key = "assigned" }.With(new UiNode(UiKind.Stack) { Align = "center" }.With(
                new UiNode(UiKind.StackItem) { Key = "profile" }.With(new UiNode(UiKind.Box) { Text = "↳ " + profiles[(int)assigned - 1], Color = "#4f4f4f" }),
                new UiNode(UiKind.StackItem) { Key = "unassign" }.With(new UiNode(UiKind.Button)
                {
                    Icon = "times", Color = "transparent", OnClick = () => c.Act("set_job_to_profile", ("job", name), ("profile", -1)),
                }))));

        return new UiNode(UiKind.StackItem) { Key = "job:" + name, Background = background }.With(new UiNode(UiKind.Stack) { Align = "center" }.With(
            new UiNode(UiKind.StackItem) { Key = "name", Grow = 1.5 }.With(nameColumn),
            new UiNode(UiKind.StackItem) { Key = "right", Grow = 1 }.With(rightSide)));
    }

    // PriorityButtons and PriorityButton: an 18px button per priority in its colour while set, white otherwise (Off and On for the
    // overflow role)
    private static UiNode PriorityButtons(TguiContext c, string name, bool isOverflow, int priority)
    {
        UiNode Button(string text, string color, bool enabled, int? level) => new UiNode(UiKind.StackItem) { Key = "p:" + text, Width = "22px" }.With(
            new UiNode(UiKind.Button)
            {
                Width = "18px", Height = "18px", Color = enabled ? color : "white", Tooltip = text, Circular = true, Slashed = text == "Off",
                OnClick = () => c.Act("set_job_preference", ("job", name), ("level", level)),
            });
        UiNode[] buttons = isOverflow
            ? new[] { Button("Off", "light-grey", priority == 0, null), Button("On", "green", priority != 0, PriorityHigh) }
            : new[]
            {
                Button("Off", "light-grey", priority == 0, null), Button("Low", "red", priority == PriorityLow, PriorityLow),
                Button("Medium", "yellow", priority == PriorityMedium, PriorityMedium), Button("High", "green", priority == PriorityHigh, PriorityHigh),
            };
        return new UiNode(UiKind.Stack) { Justify = "flex-end" }.With(buttons);
    }

    // CharacterPreferences/MainPage.tsx MainPage
    private static UiNode MainPage(TguiContext c)
    {
        UiValue prefs = c.Data.Get("character_preferences");
        string gender = prefs?.Get("misc")?.Str("gender", "male") ?? "male";
        string nameKey = c.Data.Str("name_to_use", "real_name");
        string name = prefs?.Get("names")?.Str(nameKey) ?? "";
        UiValue constants = c.Data.Get("constant_data");

        // CharacterControls: the gender button (GenderButton's floating menu of the four genders, drawn open)
        var genders = new UiNode(UiKind.Stack) { Key = "genders" }.With(Genders.Select(g => new UiNode(UiKind.StackItem) { Key = "gender:" + g.Gender }.With(
            new UiNode(UiKind.Button)
            {
                Icon = g.Icon, FontSize = "22px", Tooltip = g.Text, Selected = g.Gender == gender,
                OnClick = () => c.Act("set_preference", ("preference", "gender"), ("value", g.Gender)),
            })));

        // NameInput (names.tsx): the name, edited in place, and MultiNameInput's dice for the real name
        var nameRow = new UiNode(UiKind.Stack) { Key = "name", Align = "center" }.With(
            new UiNode(UiKind.StackItem) { Key = "edit" }.With(new UiNode(UiKind.Icon) { Icon = "edit", Color = "rgba(255, 255, 255, 0.5)" }),
            new UiNode(UiKind.StackItem) { Key = "input", Grow = 1 }.With(new UiNode(UiKind.Input)
            {
                Key = "name:" + nameKey, Fluid = true, TextValue = name, MaxLength = 42,
                OnCommit = value => c.Act("set_preference", ("preference", nameKey), ("value", value)),
            }),
            new UiNode(UiKind.StackItem) { Key = "dice" }.With(new UiNode(UiKind.Button)
            {
                Icon = "dice", Tooltip = "Randomize", OnClick = () => c.Act("randomize_name", ("preference", nameKey)),
            }));

        // CharacterControls: the species button opens the species page; the gender shows unless the species has no sexes
        // (currentSpeciesData.sexes); the delete button is open while more than one slot holds a character
        UiValue currentSpecies = constants?.Get("species")?.Get(prefs?.Get("misc")?.Str("species", null) ?? "");
        bool showGender = currentSpecies == null || currentSpecies.Truthy("sexes");
        bool deleteOpen = c.UseState("deleteCharacterPopupOpen", false);
        var controls = new UiNode(UiKind.Stack) { Key = "controlsRow" }.With(
            new UiNode(UiKind.StackItem) { Key = "rotate" }.With(new UiNode(UiKind.Button)
            {
                Icon = "undo", FontSize = "22px", Tooltip = "Rotate", OnClick = () => c.Act("rotate"),
            }),
            new UiNode(UiKind.StackItem) { Key = "species" }.With(new UiNode(UiKind.Button)
            {
                Icon = "paw", FontSize = "22px", Tooltip = "Species", OnClick = () => c.SetState("page", "Species"),
            }),
            new UiNode(UiKind.StackItem) { Key = "genders", Grow = 1 }.With(showGender ? genders : null),
            new UiNode(UiKind.StackItem) { Key = "delete" }.With(new UiNode(UiKind.Button)
            {
                Icon = "trash", FontSize = "22px", Color = "red", Tooltip = "Delete Character",
                Disabled = ProfileNames(c).Count(n => !string.IsNullOrEmpty(n)) <= 1,
                OnClick = () => c.SetState("deleteCharacterPopupOpen", true),
            }));

        var left = new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "controls" }.With(controls),
            new UiNode(UiKind.StackItem) { Key = "preview", Grow = 1 }.With(CharacterPreview(c, c.Data.Str("character_preview_view", null))),
            new UiNode(UiKind.StackItem) { Key = "nameinput" }.With(nameRow),
            new UiNode(UiKind.StackItem) { Key = "alternatenames" }.With(MultiNameInput(c, prefs?.Get("names"), constants?.Get("names")?.Get("types"))));

        // the main features: the clothing's and the features' choice buttons in a wrapping column; a pressed one shows its
        // ChoicedSelection beside the column, where tg floats it (the kit's Floating opens on hover, not on a click)
        string open = c.UseState<string>("feature", null);
        // getRandomization: a randomizable feature's setting while the body is random (or the random toggle is on)
        UiValue nonContextual = prefs?.Get("non_contextual");
        bool randomToggle = c.UseState("randomToggle", false);
        bool randomBodyEnabled = (nonContextual?.Get("random_body") is UiValue randomBody && randomBody.Number != RandomDisabled) || randomToggle;
        UiValue randomizable = constants?.Get("random")?.Get("randomizable");
        int? Randomization(string key) => randomBodyEnabled && randomizable != null && randomizable.Items.Any(i => i.Text == key)
            ? (int)(prefs?.Get("randomization")?.Num(key, RandomDisabled) ?? RandomDisabled) : null;
        var mainFeatures = new UiNode(UiKind.Stack) { Key = "mainFeatures", Vertical = true, Fill = true, Wrap = true };
        UiValue openCatalog = null;
        string openValue = null;
        foreach (KeyValuePair<string, UiValue> feature in (prefs?.Get("clothing")?.Fields ?? []).Concat(prefs?.Get("features")?.Fields ?? []))
        {
            UiValue catalog = constants?.Get(feature.Key);
            if (feature.Key == open)
            {
                openCatalog = catalog;
                openValue = feature.Value.Text;
            }
            mainFeatures.With(new UiNode(UiKind.StackItem) { Key = "feature:" + feature.Key }.With(catalog == null
                ? new UiNode(UiKind.Button) { Height = "4em", Width = "4em", Disabled = true } // skeleton button
                : MainFeature(c, feature.Key, feature.Value.Text, catalog, open, Randomization(feature.Key))));
        }

        UiNode selection = openCatalog == null ? null : new UiNode(UiKind.StackItem) { Key = "selection" }.With(
            ChoicedSelection(c, open, openCatalog, openValue, prefs?.Get("supplemental_features")));

        // the two PreferenceLists: the contextual (secondary) features and the non-contextual preferences; with a random body the
        // non-contextual list gains random_species (its value is the randomization's, read by its input), otherwise it loses
        // random_name (the server doesn't know whether the random toggle is on)
        var nonContextualFields = (nonContextual?.Fields ?? []).Where(f => randomBodyEnabled || f.Key != "random_name");
        if (randomBodyEnabled)
            nonContextualFields = nonContextualFields.Where(f => f.Key != "random_species").Append(new KeyValuePair<string, UiValue>("random_species", null));
        var lists = new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
            PreferenceList(c, "secondary", prefs?.Get("secondary_features")?.Fields ?? [], constants, gender, Randomization),
            PreferenceList(c, "nonContextual", nonContextualFields, constants, gender, Randomization));

        var page = new UiNode(UiKind.Stack) { Height = $"{ClothingSidebarRows * ClothingCellSize}px" }.With(
            new UiNode(UiKind.StackItem) { Key = "left", Width = "300px" }.With(left),
            new UiNode(UiKind.StackItem) { Key = "main" }.With(mainFeatures),
            selection,
            new UiNode(UiKind.StackItem) { Key = "right", Grow = 1 }.With(lists));
        return deleteOpen ? new UiNode(UiKind.Fragment).With(DeleteCharacterPopup(c, name), page) : page;
    }

    // names.tsx MultiNameInput: the alternate names by their group; tg's modal is a section opened under the name (the job names, the
    // religion's) with the same rows
    private static UiNode MultiNameInput(TguiContext c, UiValue names, UiValue types)
    {
        bool open = c.UseState("multiNameInputOpen", false);
        var box = new UiNode(UiKind.Box) { Key = "multinames" }.With(new UiNode(UiKind.Button)
        {
            Key = "alternate", Text = "Alternate names", Icon = "ellipsis-v", Fluid = true, Selected = open,
            OnClick = () => c.SetState("multiNameInputOpen", !open),
        });
        if (!open || types == null)
            return box;
        var list = new UiNode(UiKind.LabeledList);
        foreach (KeyValuePair<string, UiValue> type in types.Fields.Where(f => f.Key != "real_name").OrderBy(f => f.Value.Str("group", ""), StringComparer.Ordinal).ThenBy(f => f.Key, StringComparer.Ordinal))
        {
            string key = type.Key;
            var row = new UiNode(UiKind.Stack) { Key = "name:" + key }.With(new UiNode(UiKind.StackItem) { Key = "input", Grow = 1 }.With(new UiNode(UiKind.Input)
            {
                Key = "alternate:" + key, Fluid = true, TextValue = names?.Str(key) ?? "",
                OnCommit = value => c.Act("set_preference", ("preference", key), ("value", value)),
            }));
            if (type.Value.Truthy("can_randomize"))
                row.With(new UiNode(UiKind.StackItem) { Key = "dice" }.With(new UiNode(UiKind.Button)
                {
                    Icon = "dice", Tooltip = "Randomize", OnClick = () => c.Act("randomize_name", ("preference", key)),
                }));
            list.With(new UiNode(UiKind.LabeledItem) { Key = key, Label = type.Value.Str("explanation", key) }.With(row));
        }
        return box.With(new UiNode(UiKind.Section) { Key = "alternatesection", Title = "Alternate names" }.With(list));
    }

    // MainPage.tsx's sizes
    private const int ClothingCellSize = 48;
    private const int ClothingSidebarRows = 9;
    private const int ClothingSelectionCellSize = 48;
    private const double ClothingSelectionWidth = 5.4;
    private const double ClothingSelectionMultiplier = 5.2;

    // types.ts RandomSetting, and RandomizationButton.tsx's options
    private const int RandomAntagOnly = 1, RandomDisabled = 2, RandomEnabled = 3;
    private static readonly (int Value, string Text)[] RandomOptions =
    {
        (RandomDisabled, "Do not randomize"),
        (RandomEnabled, "Always randomize"),
        (RandomAntagOnly, "Randomize when antagonist"),
    };

    // preferences/features: the names of the ported character preferences (character_preferences/*.tsx, species_features.tsx,
    // randomization.tsx) and which input each takes (FeatureColorInput, FeatureDropdownInput, ...)
    private static readonly Dictionary<string, string> FeatureNames = new(StringComparer.Ordinal)
    {
        ["age"] = "Age",
        ["body_type"] = "Body type",
        ["skin_tone"] = "Skin tone",
        ["eye_color"] = "Eye color",
        ["feature_cat_ears"] = "Ears", // gaps SP1: species_features.tsx, FeatureDropdownInput
        ["feature_cat_tail"] = "Tail",
        ["feature_lizard_legs"] = "Legs",
        ["feature_lizard_spines"] = "Spines",
        ["feature_lizard_tail"] = "Tail",
        ["feature_mcolor"] = "Mutant color", // FeatureColorInput
        ["facial_hair_color"] = "Facial hair color",
        ["facial_hair_gradient"] = "Facial hair gradient",
        ["facial_hair_gradient_color"] = "Facial hair gradient color",
        ["hair_color"] = "Hair color",
        ["hair_gradient"] = "Hair gradient",
        ["hair_gradient_color"] = "Hair gradient color",
        ["underwear_color"] = "Underwear color",
        ["prefered_security_department"] = "Security department",
        ["prisoner_crime"] = "Prisoner crime",
        ["random_body"] = "Random body",
        ["random_hardcore"] = "Hardcore random",
        ["random_name"] = "Random name",
        ["random_species"] = "Random species",
        ["tts_voice"] = "Voice", // D307: tts_voice.tsx
        ["tts_voice_pitch"] = "Voice Pitch Adjustment",
        ["uplink_loc"] = "Uplink Spawn Location", // syndicate A2: uplink_loc.tsx, FeatureDropdownInput
    };

    private static bool IsColorFeature(string key) => key is "eye_color" or "facial_hair_color" or "facial_hair_gradient_color" or "hair_color"
        or "hair_gradient_color" or "underwear_color" or "feature_mcolor";

    // A choice's picture (the constant data's icons: list(resource, state), DV44) in a box of 32 px times size.
    private static UiNode Picture(UiValue catalog, string choice, double size)
    {
        UiValue icon = catalog?.Get("icons")?.Get(choice);
        if (icon == null || icon.Items.Count < 2)
            return null;
        return new UiNode(UiKind.Box) { Key = "picture", DmIcon = icon.Items[0].Text, DmIconState = icon.Items[1].Text ?? "", Size = size };
    }

    // MainFeature: a 48 px button of the current choice's picture that opens its ChoicedSelection; with a randomization, the picture
    // moves up and left (tg's translate -70 %, scale 1.1) and the RandomizationButton sits in the bottom right corner
    private static UiNode MainFeature(TguiContext c, string key, string value, UiValue catalog, string open, int? randomization)
    {
        var button = new UiNode(UiKind.Button)
        {
            Height = $"{ClothingCellSize}px", Width = $"{ClothingCellSize}px", Tooltip = catalog.Str("name"),
            OnClick = () => c.SetState<string>("feature", open == key ? null : key),
        };
        if (randomization is not int setting)
            return button.With(Picture(catalog, value, 1.3));
        UiNode picture = Picture(catalog, value, 1.1);
        if (picture != null)
            picture.Anchor = "top-left";
        UiNode dice = RandomizationButton(key, setting, v => SetRandomization(c, key, v));
        dice.Anchor = "bottom-right";
        return button.With(picture, dice);
    }

    // createSetRandomization
    private static void SetRandomization(TguiContext c, string preference, int setting) => c.Act("set_random_preference", ("preference", preference), ("value", setting));

    // RandomizationButton.tsx: tg's icon-only dropdown of the three settings, in the setting's colour
    private static UiNode RandomizationButton(string key, int value, Action<int> setValue) => new UiNode(UiKind.Dropdown)
    {
        Key = "random:" + key, Icon = "dice-d20", Options = RandomOptions.Select(o => o.Text).ToList(),
        Color = value switch { RandomAntagOnly => "orange", RandomDisabled => "red", _ => "green" },
        OnSelected = text => setValue(RandomOptions.First(o => o.Text == text).Value),
    };

    // CharacterPreview (CharacterPreview.tsx, a ByondUi map) drawn as a 2D picture (D284), with the Loadout page's two arrows under it
    // (rotate_dummy left and right, DV210) and its Job Clothes checkbox (LoadoutPreviewSection, toggle_job_clothes)
    private static UiNode CharacterPreview(TguiContext c, string mapRef)
    {
        if (string.IsNullOrEmpty(mapRef))
            return new UiNode(UiKind.Box);
        return new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "map", Grow = 1 }.With(new UiNode(UiKind.ByondUi)
            {
                Key = "map:" + mapRef, MapRef = mapRef, MapPicture = true, Width = "100%", Height = "100%",
            }),
            new UiNode(UiKind.StackItem) { Key = "arrows", Align = "center" }.With(new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem) { Key = "left" }.With(new UiNode(UiKind.Button)
                {
                    Icon = "chevron-left", OnClick = () => c.Act("rotate_dummy", ("dir", "left")),
                }),
                new UiNode(UiKind.StackItem) { Key = "right" }.With(new UiNode(UiKind.Button)
                {
                    Icon = "chevron-right", OnClick = () => c.Act("rotate_dummy", ("dir", "right")),
                }),
                new UiNode(UiKind.StackItem) { Key = "job_clothes" }.With(new UiNode(UiKind.Button)
                {
                    Text = "Job Clothes", Checkbox = c.Data.Get("job_clothes")?.Truthy == true, OnClick = () => c.Act("toggle_job_clothes"),
                }))));
    }

    // ChoicedSelection: the catalog's pictures in a searchable grid, the supplemental feature's colour button in the title
    private static UiNode ChoicedSelection(TguiContext c, string key, UiValue catalog, string selected, UiValue supplemental)
    {
        if (catalog.Get("icons") == null)
            return new UiNode(UiKind.Box) { Color = "red", Text = "Provided catalog had no icons!" };
        string search = c.UseState("featureSearch:" + key, "");
        string supplementalFeature = catalog.Get("supplemental_feature")?.Text;
        var grid = new UiNode(UiKind.Stack) { Key = "grid", Wrap = true };
        foreach (KeyValuePair<string, UiValue> entry in catalog.Get("icons").Fields)
        {
            if (search.Length > 0 && !entry.Key.Contains(search, StringComparison.OrdinalIgnoreCase))
                continue; // tgui's createSearch
            string choice = entry.Key;
            grid.With(new UiNode(UiKind.StackItem) { Key = "choice:" + choice }.With(new UiNode(UiKind.Button)
            {
                Height = $"{ClothingSelectionCellSize}px", Width = $"{ClothingSelectionCellSize}px", Selected = choice == selected, Tooltip = choice,
                OnClick = () => c.Act("set_preference", ("preference", key), ("value", choice)),
            }.With(Picture(catalog, choice, 0.8))));
        }
        string name = catalog.Str("name");
        return new UiNode(UiKind.Box)
        {
            Key = "choicedSelection",
            Height = $"{ClothingSelectionCellSize * ClothingSelectionMultiplier}px",
            Width = $"{ClothingSelectionCellSize * ClothingSelectionWidth}px",
        }.With(new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "head" }.With(new UiNode(UiKind.Section)
            {
                Fill = true, Title = "Select " + name.ToLowerInvariant(),
                Buttons = supplementalFeature == null ? null : ColorInput(c, supplementalFeature, supplemental?.Str(supplementalFeature), shrink: true),
            }.With(new UiNode(UiKind.Input) { Key = "search:" + key, Fluid = true, Tooltip = "Search...", TextValue = search, OnInput = v => c.SetState("featureSearch:" + key, v) })),
            new UiNode(UiKind.StackItem) { Key = "choices", Grow = 1 }.With(new UiNode(UiKind.Section) { Key = "choices:" + key, Fill = true, Scrollable = true }.With(grid))));
    }

    // FeatureColorInput (features/base.tsx): a button of the colour's square ("Change" beside it unless shrunk) that opens tg's picker
    private static UiNode ColorInput(TguiContext c, string key, string value, bool shrink = false)
    {
        string color = string.IsNullOrEmpty(value) ? "#ffffff" : value.StartsWith('#') ? value : "#" + value;
        var row = new UiNode(UiKind.Stack) { Align = "center", Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "square" }.With(new UiNode(UiKind.ColorBox) { Color = color, Width = "11px", Height = "11px" }),
            shrink ? null : new UiNode(UiKind.StackItem) { Key = "change" }.With(new UiNode(UiKind.Box) { Text = "Change" }));
        return new UiNode(UiKind.Button) { Key = "color:" + key, OnClick = () => c.Act("set_color_preference", ("preference", key)) }.With(row);
    }

    // PreferenceList: a labeled row per preference, sorted by its feature's name, with the feature's input (FeatureValueInput)
    private static UiNode PreferenceList(TguiContext c, string key, IEnumerable<KeyValuePair<string, UiValue>> preferences, UiValue constants, string gender, Func<string, int?> randomizations)
    {
        var list = new UiNode(UiKind.LabeledList) { Key = "list" };
        foreach (KeyValuePair<string, UiValue> preference in preferences.OrderBy(p => FeatureNames.TryGetValue(p.Key, out string n) ? n : "\uffff" + p.Key, StringComparer.Ordinal))
        {
            if (!FeatureNames.TryGetValue(preference.Key, out string name))
            {
                list.With(new UiNode(UiKind.Box) { Key = preference.Key, Bold = true, Text = $"Feature {preference.Key} is not recognized." });
                continue;
            }
            UiNode input = FeatureValueInput(c, preference.Key, preference.Value, constants?.Get(preference.Key), gender);
            string featureId = preference.Key;
            UiNode row = randomizations(featureId) is int randomSetting
                ? new UiNode(UiKind.Stack) { Fill = true }.With(
                    new UiNode(UiKind.StackItem) { Key = "random" }.With(RandomizationButton(featureId, randomSetting, v => SetRandomization(c, featureId, v))),
                    new UiNode(UiKind.StackItem) { Key = "input", Grow = 1 }.With(input))
                : input;
            list.With(new UiNode(UiKind.LabeledItem) { Key = preference.Key, Label = name }.With(row));
        }
        return new UiNode(UiKind.StackItem) { Key = key, Grow = 1 }.With(new UiNode(UiKind.Section) { Key = "list:" + key, Fill = true, Scrollable = true }.With(list));
    }

    // FeatureValueInput: the input of each ported feature
    private static UiNode FeatureValueInput(TguiContext c, string key, UiValue value, UiValue serverData, string gender)
    {
        void Set(object v) => c.Act("set_preference", ("preference", key), ("value", v));
        if (IsColorFeature(key))
            return ColorInput(c, key, value?.Text);
        switch (key)
        {
            case "age": // FeatureNumberInput: its minimum, maximum and step from the constant data
                return new UiNode(UiKind.NumberInput)
                {
                    Key = "age", CommitOnly = true, Value = value.Number,
                    MinValue = serverData?.Num("minimum", 18) ?? 18, MaxValue = serverData?.Num("maximum", 85) ?? 85, Step = serverData?.Num("step", 1) ?? 1,
                    OnChange = v => Set(v),
                };
            case "tts_voice_pitch": // tts_voice.tsx: FeatureSliderInput; D312 (the owner): arrows a step down and up within the bounds (D311: -5 to 5)
            {
                double min = serverData?.Num("minimum", -5) ?? -5, max = serverData?.Num("maximum", 5) ?? 5, step = serverData?.Num("step", 1) ?? 1;
                double pitch = Math.Clamp(value?.Number ?? 0, min, max);
                return new UiNode(UiKind.Stack) { Align = "center" }.With(
                    new UiNode(UiKind.StackItem) { Key = "down" }.With(new UiNode(UiKind.Button)
                    {
                        Icon = "chevron-left", Disabled = serverData == null || pitch <= min, OnClick = () => Set(Math.Max(min, pitch - step)),
                    }),
                    new UiNode(UiKind.StackItem) { Key = "value", Width = "32px" }.With(new UiNode(UiKind.Box) { Text = TguiFormat.JsNumber(pitch), TextAlign = "center" }),
                    new UiNode(UiKind.StackItem) { Key = "up" }.With(new UiNode(UiKind.Button)
                    {
                        Icon = "chevron-right", Disabled = serverData == null || pitch >= max, OnClick = () => Set(Math.Min(max, pitch + step)),
                    }));
            }
            case "tts_voice": // tts_voice.tsx FeatureTTSDropdownInput: the dropdown, then play and play as a robot (the blips are not ported, DV262)
            {
                // D311: a woman's voices for a female character, a man's for a male one, all for the others (the server's own check:
                // datum_controller_subsystem_tts.fits_gender)
                string prefix = gender switch { "male" => "Man ", "female" => "Woman ", _ => "" };
                List<string> voices = (serverData?.Get("choices")?.Items ?? []).Select(i => i.Text)
                    .Where(t => t != null && t.StartsWith(prefix, StringComparison.Ordinal)).ToList();
                return new UiNode(UiKind.Stack).With(
                    new UiNode(UiKind.StackItem) { Key = "dropdown", Grow = 1 }.With(new UiNode(UiKind.Dropdown)
                    {
                        Key = key, Options = voices, TextValue = value?.Text ?? "", Width = "100%", Disabled = serverData == null,
                        OnSelected = text => Set(text),
                    }),
                    new UiNode(UiKind.StackItem) { Key = "play" }.With(new UiNode(UiKind.Button) { Icon = "play", OnClick = () => c.Act("play_voice") }),
                    new UiNode(UiKind.StackItem) { Key = "robot" }.With(new UiNode(UiKind.Button) { Icon = "robot", OnClick = () => c.Act("play_voice_robot") }),
                    // D312: the port's radio preview
                    new UiNode(UiKind.StackItem) { Key = "radio" }.With(new UiNode(UiKind.Button) { Icon = "walkie-talkie", Tooltip = "Play over the radio", OnClick = () => c.Act("play_voice_radio") }));
            }
            case "random_hardcore": // CheckboxInput
                return new UiNode(UiKind.Button) { Key = key, Checkbox = value.Truthy, OnClick = () => Set(value.Truthy ? 0 : 1) };
            case "random_name": // randomization.tsx random_name: the RandomizationButton
                return RandomizationButton(key, (int)value.Number, v => Set(v));
            case "random_species": // randomization.tsx random_species: the species' randomization
                return RandomizationButton(key, (int)(c.Data.Get("character_preferences")?.Get("randomization")?.Num("species", RandomDisabled) ?? RandomDisabled),
                    v => SetRandomization(c, "species", v));
            case "random_body":
            {
                // randomization.tsx random_body: the RandomizationButton, then Randomize, which turns the random toggle on (every
                // feature's button shows) and asks again: Randomize (randomize_character) or Cancel
                bool toggle = c.UseState("randomToggle", false);
                var row = new UiNode(UiKind.Stack).With(new UiNode(UiKind.StackItem) { Key = "dice" }.With(RandomizationButton(key, (int)value.Number, v => Set(v))));
                if (toggle)
                    row.With(
                        new UiNode(UiKind.StackItem) { Key = "go" }.With(new UiNode(UiKind.Button)
                        {
                            Text = "Randomize", Color = "green",
                            OnClick = () =>
                            {
                                c.Act("randomize_character");
                                c.SetState("randomToggle", false);
                            },
                        }),
                        new UiNode(UiKind.StackItem) { Key = "cancel" }.With(new UiNode(UiKind.Button) { Text = "Cancel", Color = "red", OnClick = () => c.SetState("randomToggle", false) }));
                else
                    row.With(new UiNode(UiKind.StackItem) { Key = "ask" }.With(new UiNode(UiKind.Button) { Text = "Randomize", OnClick = () => c.SetState("randomToggle", true) }));
                return row;
            }
        }
        // FeatureDropdownInput (the gradients, the skin tone, and the body type without "Use gender" for a gender with no physique):
        // generateOptions, a choice's display name or its capitalised value
        UiValue names = serverData?.Get("display_names");
        string Display(string choice) => names?.Get(choice)?.Text is string n && n.Length > 0 ? n : Capitalize(choice);
        List<string> choices = (serverData?.Get("choices")?.Items ?? []).Select(i => i.Text).Where(t => t != null).ToList();
        if (key == "body_type" && gender is not ("male" or "female"))
            choices = choices.Where(v => v is "male" or "female").ToList();
        return new UiNode(UiKind.Dropdown)
        {
            Key = key, Options = choices.Select(Display).ToList(), TextValue = value?.Text == null ? "" : Display(value.Text), Width = "100%",
            Disabled = serverData == null,
            OnSelected = text => Set(choices.FirstOrDefault(v => Display(v) == text) ?? text),
        };
    }

    // ---- SpeciesPage.tsx (gaps SP1) ----

    // FOOD_ICONS and FOOD_NAMES: the diet's foods (types.ts Food) with an icon and a name
    private static readonly Dictionary<string, (string Icon, string Name)> Foods = new(StringComparer.Ordinal)
    {
        ["BUGS"] = ("bug", "Bugs"),
        ["CLOTH"] = ("tshirt", "Clothing"),
        ["DAIRY"] = ("cheese", "Dairy"),
        ["FRIED"] = ("bacon", "Fried food"),
        ["FRUIT"] = ("apple-alt", "Fruit"),
        ["GORE"] = ("skull", "Gore"),
        ["GRAIN"] = ("bread-slice", "Grain"),
        ["GROSS"] = ("trash", "Gross food"),
        ["JUNKFOOD"] = ("pizza-slice", "Junk food"),
        ["MEAT"] = ("hamburger", "Meat"),
        ["NUTS"] = ("seedling", "Nuts"),
        ["RAW"] = ("drumstick-bite", "Raw"),
        ["SEAFOOD"] = ("fish", "Seafood"),
        ["STONE"] = ("gem", "Rocks"),
        ["SUGAR"] = ("candy-cane", "Sugar"),
        ["TOXIC"] = ("biohazard", "Toxic food"),
        ["VEGETABLES"] = ("carrot", "Vegetables"),
    };

    // IGNORE_UNLESS_LIKED: foods shown only among the liked
    private static readonly HashSet<string> IgnoreUnlessLiked = new(StringComparer.Ordinal) { "BUGS", "CLOTH", "GROSS", "TOXIC" };

    // SpeciesPageInner: Go Back, the species' 64 px buttons (the human first), and the chosen species' section (its name, its diet in
    // the title row, Description and Features), its preview, and its Lore
    private static UiNode SpeciesPage(TguiContext c)
    {
        UiValue speciesData = c.Data.Get("constant_data")?.Get("species");
        if (speciesData == null) // LoadingScreen
            return new UiNode(UiKind.NoticeBox) { Key = "loading", Text = "Loading..." };
        UiValue icons = c.Data.Get("species_icons");
        string chosen = c.Data.Get("character_preferences")?.Get("misc")?.Str("species", null);

        // Humans are always the top of the list (tg swaps the human with the first entry)
        var species = speciesData.Fields.ToList();
        int humanIndex = species.FindIndex(s => s.Key == "human");
        if (humanIndex > 0)
            (species[0], species[humanIndex]) = (species[humanIndex], species[0]);

        var list = new UiNode(UiKind.Stack) { Key = "speciesList", Vertical = true };
        foreach (KeyValuePair<string, UiValue> entry in species)
        {
            string id = entry.Key;
            var button = new UiNode(UiKind.Button)
            {
                Key = "species:" + id, Selected = chosen == id, Tooltip = entry.Value.Str("name"), Width = "64px", Height = "64px",
                OnClick = () => c.Act("set_preference", ("preference", "species"), ("value", id)),
            };
            UiValue picture = icons?.Get(entry.Value.Str("icon", ""));
            if (picture != null && picture.Items.Count >= 2)
                button.With(new UiNode(UiKind.Box) { Key = "picture", DmIcon = picture.Items[0].AsStr(), DmIconState = picture.Items[1].AsStr(), Size = 2 });
            list.With(new UiNode(UiKind.StackItem) { Key = id }.With(button));
        }

        UiValue current = species.FirstOrDefault(s => s.Key == chosen).Value ?? species.FirstOrDefault().Value;
        var lore = new UiNode(UiKind.Box) { Key = "blockquote", Color = "label" };
        IReadOnlyList<UiValue> loreLines = current?.Get("lore")?.Items ?? [];
        for (int i = 0; i < loreLines.Count; i++)
            lore.With(new UiNode(UiKind.Box) { Key = "lore:" + i, Wrap = true, Text = loreLines[i].AsStr() + (i != loreLines.Count - 1 ? "\n\n" : "") });

        var details = new UiNode(UiKind.Stack) { Key = "details", Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "top" }.With(new UiNode(UiKind.Stack) { Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "about", Width = "70%" }.With(new UiNode(UiKind.Section)
                {
                    Key = "speciesSection", Title = current?.Str("name") ?? "",
                    // NOHUNGER species have no diet (diet = null), so we have nothing to show
                    Buttons = current?.Get("diet") is UiValue diet && diet.Kind != UiValueKind.Null ? Diet(diet) : null,
                }.With(
                    new UiNode(UiKind.Section) { Key = "description", Title = "Description" }.With(new UiNode(UiKind.Box) { Wrap = true, Text = current?.Str("desc") ?? "" }),
                    new UiNode(UiKind.Section) { Key = "features", Title = "Features" }.With(SpeciesPerks(current?.Get("perks"))))),
                new UiNode(UiKind.StackItem) { Key = "preview", Width = "30%" }.With(CharacterPreview(c, c.Data.Str("character_preview_view", null))))),
            new UiNode(UiKind.StackItem) { Key = "lore" }.With(new UiNode(UiKind.Section) { Key = "loreSection", Title = "Lore" }.With(lore)));

        return new UiNode(UiKind.Stack) { Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "back" }.With(new UiNode(UiKind.Button)
            {
                Icon = "arrow-left", Text = "Go Back", OnClick = () => c.SetState("page", "Character"),
            }),
            new UiNode(UiKind.StackItem) { Key = "body", Grow = 1 }.With(new UiNode(UiKind.Stack) { Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "list" }.With(new UiNode(UiKind.Section) { Key = "listBox", Fill = true, Scrollable = true }.With(list)),
                new UiNode(UiKind.StackItem) { Key = "current", Grow = 1 }.With(details))));
    }

    // Diet: the liked, disliked and toxic foods (the last two without IGNORE_UNLESS_LIKED's), each a row of icons with a tooltip
    private static UiNode Diet(UiValue diet)
    {
        UiNode FoodList(string key, string icon, string name, string color, bool filter)
        {
            List<string> food = (diet.Get(key)?.Items ?? []).Select(f => f.AsStr()).Where(f => f != null && (!filter || !IgnoreUnlessLiked.Contains(f))).ToList();
            if (food.Count == 0)
                return null;
            UiNode Content() => new UiNode(UiKind.Box) { Key = "tip:" + key }.With(
                new UiNode(UiKind.Box) { Key = "head", Bold = true }.With(new UiNode(UiKind.Icon) { Icon = icon }, new UiNode(UiKind.Box) { Text = " " + name }),
                new UiNode(UiKind.Divider) { Key = "rule", Rule = true },
                new UiNode(UiKind.Box) { Key = "names", Text = string.Join(", ", food.Where(Foods.ContainsKey).Select(f => Foods[f].Name)) });
            var icons = new UiNode(UiKind.Stack) { Key = "icons" }.With(food.Where(Foods.ContainsKey).Select(f =>
                new UiNode(UiKind.StackItem) { Key = f }.With(new UiNode(UiKind.Icon) { Icon = Foods[f].Icon, Color = color, Size = 1.4 })));
            return new UiNode(UiKind.StackItem) { Key = key }.With(new UiNode(UiKind.Floating) { Key = "diet:" + key, Content = Content }.With(icons));
        }
        return new UiNode(UiKind.Stack) { Key = "diet" }.With(
            FoodList("liked_food", "heart", "Liked food", "pink", false),
            FoodList("disliked_food", "thumbs-down", "Disliked food", "red", true),
            FoodList("toxic_food", "biohazard", "Toxic food", "olive", true));
    }

    // SpeciesPerks: the positive perks (green) left, the neutral (grey) and the negative (red) right, each a 32 px icon with its tooltip
    private static UiNode SpeciesPerks(UiValue perks)
    {
        UiNode Perks(string key, string background) => new UiNode(UiKind.Stack) { Key = key }.With((perks?.Get(key)?.Items ?? []).Select(perk =>
        {
            UiNode Content() => new UiNode(UiKind.Box) { Key = "tip" }.With(
                new UiNode(UiKind.Box) { Key = "name", Bold = true, Text = perk.Str("name") },
                new UiNode(UiKind.Divider) { Key = "rule", Rule = true },
                new UiNode(UiKind.Box) { Key = "desc", Wrap = true, Text = perk.Str("description") });
            return new UiNode(UiKind.StackItem) { Key = perk.Str("name") }.With(new UiNode(UiKind.Floating) { Key = "perk:" + perk.Str("name"), Content = Content }.With(
                new UiNode(UiKind.Box) { Key = "perk", Background = background, Width = "32px", Height = "32px", TextAlign = "center" }.With(
                    new UiNode(UiKind.Icon) { Icon = perk.Str("ui_icon"), Size = 1.5 })));
        }));
        return new UiNode(UiKind.Stack) { Key = "perks", Fill = true, Justify = "space-between" }.With(
            new UiNode(UiKind.StackItem) { Key = "positive" }.With(Perks("positive", "green")),
            Perks("neutral", "grey"),
            Perks("negative", "red"));
    }

    private static string Capitalize(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text[1..];

    // DeleteCharacterPopup.tsx: the modal that asks before remove_current_slot.
    // shortcut: no 3-second countdown on Delete (the kit redraws a window only on new data or state, so it could not tick); add one
    // when windows get a redraw timer.
    private static UiNode DeleteCharacterPopup(TguiContext c, string name)
    {
        void Close() => c.SetState("deleteCharacterPopupOpen", false);
        return new UiNode(UiKind.Modal) { Key = "deleteCharacter" }.With(new UiNode(UiKind.Stack) { Vertical = true, Align = "center" }.With(
            new UiNode(UiKind.StackItem) { Key = "wait" }.With(new UiNode(UiKind.Box) { Text = "Wait!", FontSize = "36px", TextAlign = "center" }),
            new UiNode(UiKind.StackItem) { Key = "text", Width = "300px" }.With(new UiNode(UiKind.Box)
            {
                Wrap = true, TextAlign = "center", Text = $"You're about to delete {name} forever. Are you sure you want to do this?",
            }),
            new UiNode(UiKind.StackItem) { Key = "buttons" }.With(new UiNode(UiKind.Stack).With(
                new UiNode(UiKind.StackItem) { Key = "delete" }.With(new UiNode(UiKind.Button)
                {
                    Text = "Delete", Color = "danger", Width = "80px",
                    OnClick = () => { c.Act("remove_current_slot"); Close(); },
                }),
                new UiNode(UiKind.StackItem) { Key = "keep" }.With(new UiNode(UiKind.Button) { Text = "No, don't delete", OnClick = Close })))));
    }
}
