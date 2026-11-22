using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg;
using Anomaly.Core.Tests.Tools;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M2Appearance.F1;

/// <summary>
/// M2 F1: a joining player's jumpsuit and name, through tg's own procs. The host's join stand-in (<see cref="CrewStandIn"/>)
/// names the human with <c>fully_replace_character_name(null, generate_random_mob_name(TRUE))</c> and dresses it with
/// <c>equipOutfit(/datum/outfit/job/assistant)</c>: the assistant outfit's <c>give_jumpsuit</c> takes
/// <c>/obj/item/clothing/under/color/random</c> from <c>GLOB.colored_assistant</c> (<c>/datum/colored_assistant/random</c>,
/// the default when the config's <c>GREY_ASSISTANTS</c> is off), whose Initialize equips a random colour jumpsuit in
/// <c>ITEM_SLOT_ICLOTHING</c>; <c>update_worn_undersuit</c> draws it on <c>UNIFORM_LAYER</c> with its GAGS worn icon.
/// </summary>
public class JumpsuitAndNameTests
{
    // The colour jumpsuits get_random_jumpsuit picks from (color.dm:28-36): subtypesof(color) less the jumpskirts, the random
    // ones, the ancient grey and the ghost's black.
    private static readonly Type[] Pickable =
    {
        typeof(obj_item_clothing_under_color_black), typeof(obj_item_clothing_under_color_grey),
        typeof(obj_item_clothing_under_color_blue), typeof(obj_item_clothing_under_color_green),
        typeof(obj_item_clothing_under_color_orange), typeof(obj_item_clothing_under_color_pink),
        typeof(obj_item_clothing_under_color_red), typeof(obj_item_clothing_under_color_white),
        typeof(obj_item_clothing_under_color_yellow), typeof(obj_item_clothing_under_color_darkblue),
        typeof(obj_item_clothing_under_color_teal), typeof(obj_item_clothing_under_color_lightpurple),
        typeof(obj_item_clothing_under_color_darkgreen), typeof(obj_item_clothing_under_color_lightbrown),
        typeof(obj_item_clothing_under_color_brown), typeof(obj_item_clothing_under_color_maroon),
        typeof(obj_item_clothing_under_color_rainbow),
    };

    internal static TgToyRoom Room(ulong seed) => TgToyRoom.Boot(seed: seed, configure: w =>
    {
        if (TgClone.Root != null)
            TgStrings.Mount(DmWorldState.Of(w).Files, TgClone.Root);
    });

    internal static void NoRuntimes(TgToyRoom room)
        => Assert.True(!room.RuntimeText.Any(), string.Join("\n----\n", room.RuntimeText.Select(t => t.Length > 1500 ? t.Substring(0, 1500) : t)));

    internal static mob_living_carbon_human Crew(int x, int y)
    {
        var human = Dm.@new<mob_living_carbon_human>(TgToyRoom.T(x, y));
        CrewStandIn.Join(human);
        return human;
    }

    [TgFact]
    public void ASpawnedPlayerWearsAColourJumpsuitDrawnOnTheUniformLayerWithItsGagsIcon()
    {
        TgToyRoom room = Room(seed: 7);
        room.In(() =>
        {
            mob_living_carbon_human human = Crew(4, 4);

            Assert.True(human.w_uniform is obj_item_clothing_under_color, $"w_uniform is {human.w_uniform?.type}");
            Assert.Contains(human.w_uniform.type, Pickable);
            Assert.Same(human, human.w_uniform.loc);
            Assert.True(Dm.truthy(Dm.bitand(human.w_uniform.item_flags, IN_INVENTORY)));
            Assert.Same(human.w_uniform, human.get_item_by_slot(ITEM_SLOT_ICLOTHING));
            // the random jumpsuit qdel'd itself (INITIALIZE_HINT_QDEL): no other jumpsuit is in the human
            Assert.Single(Dm.each<obj_item_clothing_under>(human.contents));

            var suit = (obj_item_clothing_under)human.w_uniform;
            if (suit.type != typeof(obj_item_clothing_under_color_rainbow))
            {
                // tg's GAGS worn icon of the jumpsuit (items.dm:397-398) and the post-init icon state (atoms_initializing:138)
                string colours = (string)suit.greyscale_colors;
                Assert.Equal("gags:/datum/greyscale_config/jumpsuit/worn|" + colours, suit.worn_icon);
                Assert.Equal("gags:/datum/greyscale_config/jumpsuit|" + colours, suit.icon);
                Assert.Equal("jumpsuit", suit.icon_state);
                // the worn overlay sits on UNIFORM_LAYER: tg's -layer index (build_worn_icon, human_update_icons.dm:607)
                var overlay = (mutable_appearance)human.overlays_standing[UNIFORM_LAYER];
                Assert.NotNull(overlay);
                Assert.Equal("gags:/datum/greyscale_config/jumpsuit/worn|" + colours, overlay.icon);
                Assert.Equal("jumpsuit", overlay.icon_state);
                Assert.Equal(-UNIFORM_LAYER, overlay.layer);
                // and it is one of the human's overlays (apply_overlay, carbon_update_icons.dm)
                Assert.Contains(Dm.each(human.overlays).Cast<IDmAppearance>(),
                    a => Equals(a.icon, "gags:/datum/greyscale_config/jumpsuit/worn|" + colours) && a.icon_state == "jumpsuit" && Dm.eq(a.layer, -UNIFORM_LAYER));
            }
        });
        NoRuntimes(room);
    }

    [TgFact]
    public void TheSameSeedGivesTheSameColoursAndOtherSeedsGiveOthers()
    {
        string[] Run(ulong seed)
        {
            TgToyRoom room = Room(seed);
            string[] colours = room.In(() => Enumerable.Range(0, 4)
                .Select(i => Crew(3 + i, 4))
                .Select(h => Dm.str(h.w_uniform.type) + " " + h.real_name)
                .ToArray());
            NoRuntimes(room);
            return colours;
        }

        string[] a = Run(11);
        Assert.Equal(a, Run(11));
        var seen = new HashSet<string>();
        foreach (ulong seed in new ulong[] { 11, 12, 13, 14 })
            foreach (string suit in Run(seed))
                seen.Add(suit.Split(' ')[0]);
        Assert.True(seen.Count >= 3, $"four seeds of four crew gave only {string.Join(", ", seen)}");
    }

    [TgFact]
    public void NameAndRealNameComeFromTgsListsAndTheFaceShowsTheRealName()
    {
        TgToyRoom room = Room(seed: 5);
        room.In(() =>
        {
            mob_living_carbon_human human = Crew(4, 4);
            string real = (string)human.real_name;
            Assert.NotEqual("Unknown", real);
            Assert.Equal(real, human.name);
            string[] parts = real.Split(' ');
            Assert.Equal(2, parts.Length);
            // default_name (_language.dm:129): capitalize(pick(first names of the gender)) + " " + capitalize(pick(last names))
            DmList firsts = human.gender == Dm.FEMALE ? GLOB.first_names_female : GLOB.first_names_male;
            Assert.Contains(Dm.each(firsts).Cast<object>(), n => (string)capitalize(n) == parts[0]);
            Assert.Contains(Dm.each(GLOB.last_names).Cast<object>(), n => (string)capitalize(n) == parts[1]);
            // carbon fully_replace_character_name (carbon.dm:405): the dna and the head keep the real name
            Assert.Equal(real, human.dna.real_name);
            Assert.Equal(real, ((obj_item_bodypart_head)human.get_bodypart(BODY_ZONE_HEAD)).real_name);
            // get_visible_name: the face is visible and there is no ID card, so the face name, the real name
            Assert.Equal(real, human.get_visible_name());
            Assert.Equal(real, human.get_face_name());
            human.update_visible_name();
            Assert.Equal(real, human.name);
        });
        NoRuntimes(room);
    }

    [TgFact]
    public void AHiddenFaceShowsUnknown()
    {
        TgToyRoom room = Room(seed: 5);
        room.In(() =>
        {
            mob_living_carbon_human human = Crew(4, 4);
            ADD_TRAIT(human, TRAIT_UNKNOWN_APPEARANCE, "test");
            Assert.Equal("Unknown", human.get_visible_name());
            human.update_visible_name();
            Assert.Equal("Unknown", human.name);
        });
    }
}
