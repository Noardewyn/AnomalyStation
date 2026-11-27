using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M9 slice G's examine and identity through the real server on MetaStation. Bob shift-clicks Alice: tg's examine of a human, "This is
/// Alice!" with what she wears and holds and her ID as a link; he follows the link and reads her ID card up close (tg's see_id); a
/// second look within a second is tg's closer look (her age). Hurt, she shows tg's bruise line; with a medical HUD he gets tg's medical
/// analysis and its evaluation link. Masked and without her ID and PDA she is "Unknown" to his examine, her speech (DV173) and her emote. Knocked
/// out, Bob sees no names (tg's see_no_names); blinded, he feels around for her (tg's blind examine).
/// </summary>
public class ExamineGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static string Lines(IEnumerable<ServerText> lines) => string.Join(" | ", lines.Select(t => t.Html));

    [MetaServerFact]
    public void Bob_examines_Alice_reads_her_ID_and_knows_her_only_as_Unknown_when_she_is_masked()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 63, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var her = (mob_living_carbon_human)w.MobOf(alice);
        var him = (mob_living_carbon_human)w.MobOf(bob);
        var bobChat = new List<ServerText>();
        bob.Client.ServerTextReceived += bobChat.Add;
        (int sx, int sy, int z) = In(w, () => ((int)her.x, (int)her.y, (int)her.z));
        using (w.World.Enter())
            Waiting.NoSleep(him.forceMove(Builtins.locate(sx - 1, sy, z)));
        w.Run(2 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount;
        string aliceName = In(w, () => Dm.str(her.real_name));

        string Examine(int wait = Second)
        {
            bobChat.Clear();
            Assert.True(bob.Session.Click(her.dm_id, ClickButton.Left, ClickModifiers.Shift));
            w.Run(wait);
            ServerText box = bobChat.LastOrDefault(t => t.Html.Contains("boxed_message", StringComparison.Ordinal));
            Assert.True(box.Html != null, "no examine box: " + Lines(bobChat) + "\n" + w.Err);
            return box.Html;
        }

        // ---- 1. The examine: her name, her jumpsuit, the crowbar in her hand, her ID as tg's see_id link.
        using (w.World.Enter())
            Assert.True(Dm.truthy(Waiting.NoSleep(her.put_in_hands(Dm.@new<obj_item_crowbar>(get_turf(her))))));
        string box = Examine();
        output.WriteLine("examine: " + box);
        Assert.Contains("This is <em>" + aliceName + "</em>", box, StringComparison.Ordinal);
        Assert.Contains("wearing", box, StringComparison.Ordinal);
        Assert.Contains("holding", box, StringComparison.Ordinal);
        Assert.Contains("crowbar", box, StringComparison.Ordinal);
        var links = new List<ChatLink>();
        HtmlText.ToPlain(box, links);
        ChatLink see = links.Single(l => l.Href.Contains("see_id=1", StringComparison.Ordinal));

        // ---- 2. The ID card read up close.
        bobChat.Clear();
        Assert.True(bob.Session.Topic(see.Href.Replace("&amp;", "&", StringComparison.Ordinal)));
        w.Run(Second);
        output.WriteLine("ID: " + Lines(bobChat));
        Assert.Contains(bobChat, t => t.Html.Contains("ID card</em>", StringComparison.Ordinal) && t.Html.Contains("Name: " + aliceName, StringComparison.Ordinal));

        // ---- 3. The closer look: tg's examine_more inside EXAMINE_MORE_WINDOW (her age).
        // the first examine's clear (RECENT_EXAMINE_MAX_WINDOW, 2 s, mob.dm:588) takes any entry of hers off when it fires
        // (clear_from_recent_examines, mob.dm:684): it fires before this pair of examines
        w.Run(Second);
        Examine(4); // tg's client/Click drops a click within a decisecond of the last
        bobChat.Clear();
        Assert.True(bob.Session.Click(her.dm_id, ClickButton.Left, ClickModifiers.Shift));
        w.Run(Second);
        output.WriteLine("closer: " + Lines(bobChat));
        Assert.Contains(bobChat, t => t.Html.Contains("to be", StringComparison.Ordinal) && t.Html.Contains("appear", StringComparison.Ordinal));
        w.Run(2 * Second);

        // ---- 4. Hurt: tg's damage line; with a medical HUD, tg's medical analysis and its evaluation link.
        using (w.World.Enter())
        {
            Waiting.NoSleep(her.apply_damage(15, BRUTE, BODY_ZONE_CHEST));
            if (him.glasses != null)
                qdel(him.glasses);
            Assert.True(Dm.truthy(Waiting.NoSleep(him.equip_to_slot_or_del(Dm.@new<obj_item_clothing_glasses_hud_health>(get_turf(him)), ITEM_SLOT_EYES))));
        }
        w.Run(Second);
        box = Examine();
        output.WriteLine("hurt, with the HUD: " + box);
        Assert.Contains("minor", box, StringComparison.Ordinal);
        Assert.Contains("Medical Analysis", box, StringComparison.Ordinal);
        links.Clear();
        HtmlText.ToPlain(box, links);
        ChatLink evaluation = links.Single(l => l.Href.Contains("evaluation=1", StringComparison.Ordinal));
        bobChat.Clear();
        Assert.True(bob.Session.Topic(evaluation.Href.Replace("&amp;", "&", StringComparison.Ordinal)));
        w.Run(Second);
        Assert.True(bobChat.Any(t => t.Html.Contains("Physical trauma analysis", StringComparison.Ordinal)), Lines(bobChat));
        Assert.True(bobChat.Any(t => t.Html.Contains("appears to have received minor physical injuries.", StringComparison.Ordinal)), Lines(bobChat));

        // ---- 5. Masked, without her ID: "Unknown" to his examine, her speech (DV173) and her emote.
        using (w.World.Enter())
        {
            if (her.wear_mask != null)
                qdel(her.wear_mask);
            Assert.True(Dm.truthy(Waiting.NoSleep(her.equip_to_slot_or_del(Dm.@new<obj_item_clothing_mask_gas>(get_turf(her)), ITEM_SLOT_MASK))));
            if (her.wear_id != null)
                qdel(her.wear_id);
            if (her.belt != null)
                qdel(her.belt); // her PDA, named after her as tg's

        }
        w.Run(Second);
        Assert.Equal("Unknown", In(w, () => Dm.str(her.name)));
        box = Examine();
        output.WriteLine("masked: " + box);
        Assert.Contains("This is <em>Unknown</em>", box, StringComparison.Ordinal);
        Assert.DoesNotContain(aliceName, box, StringComparison.Ordinal);
        bobChat.Clear();
        alice.Session.Say(new SayOut(SayTypes.Entry, SayChannels.Say, "Who am I?", false));
        w.Run(2 * Second);
        ServerText said = bobChat.LastOrDefault(t => t.Html.Contains("Who am I?", StringComparison.Ordinal));
        Assert.True(said.Html != null, Lines(bobChat));
        output.WriteLine("masked speech: " + said.Html);
        Assert.Contains("Unknown", said.Html, StringComparison.Ordinal);
        Assert.DoesNotContain(aliceName, said.Html, StringComparison.Ordinal);
        bobChat.Clear();
        Assert.True(alice.Session.Emote("wave"));
        w.Run(Second);
        Assert.True(bobChat.Any(t => t.Html.Contains("<b>Unknown</b> waves", StringComparison.Ordinal)), Lines(bobChat));

        // ---- 6. Knocked out, Bob sees no names (tg's see_no_names through the knocked out effect).
        using (w.World.Enter())
        {
            qdel(her.wear_mask);
            Waiting.NoSleep(him.Sleeping(10 * SECONDS));
        }
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(him.has_status_effect(typeof(datum_status_effect_grouped_see_no_names)))));
        Assert.Equal("Unknown", In(w, () => Dm.str(her.get_examine_name(him))));
        using (w.World.Enter())
            Waiting.NoSleep(him.SetSleeping(0));
        w.Run(2 * Second);
        Assert.False(In(w, () => Dm.truthy(him.has_status_effect(typeof(datum_status_effect_grouped_see_no_names)))));
        Assert.Equal(aliceName, In(w, () => Dm.str(her.get_examine_name(him))));

        // ---- 7. Blinded, he feels around for her before the examine (tg's blind examine).
        using (w.World.Enter())
        {
            qdel(him.glasses);
            Waiting.NoSleep(him.become_blind("test"));
            Waiting.NoSleep(him.drop_all_held_items());
        }
        w.Run(Second);
        box = Examine(6 * Second);
        Assert.True(bobChat.Any(t => t.Html.Contains("You start feeling around for something...", StringComparison.Ordinal)), Lines(bobChat));
        Assert.Contains("This is <em>" + aliceName + "</em>", box, StringComparison.Ordinal);

        Assert.True(bootRuntimes == w.World.RuntimeErrorCount, w.Err.ToString());
    }
}
