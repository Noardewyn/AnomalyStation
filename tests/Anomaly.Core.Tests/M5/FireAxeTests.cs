using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M5;

/// <summary>
/// M5 slice 5: tg's fire axe (weaponry/melee/fireaxe.dm), the two handed component (datums/components/twohanded.dm) and the
/// fire axe cabinet (structures/fireaxe.dm) in a booted toy room: wielding with both hands (force 5 to 24, the "fireaxe1"
/// icon state, the offhand, the name), unwielding by use in hand and by dropping; a wielded axe destroys a window and a grille
/// in one hit (afterattack's atom_destruction) with tg's debris, an unwielded one does not; the cabinet's lock, its glass
/// broken by hits, the axe taken and put back, the glass repaired, the multitool unlock, its overlays and sounds.
/// </summary>
public class FireAxeTests
{
    private const int Second = 20;
    private const string Left = "left=1;icon-x=16;icon-y=16";

    private static T Place<T>(AudioRoom r, int x, int y) where T : atom => r.In(() => Dm.@new<T>(locate(x, y, 1)));

    private static List<T> On<T>(AudioRoom r, int x, int y) => r.In(() => Dm.each_cast<atom>(locate(x, y, 1).contents).OfType<T>().ToList());

    private static void Click(AudioRoom r, mob who, atom target, string @params = Left, float seconds = 1)
    {
        r.Command(() => ClientSeam.Click(who.client, target, @params));
        r.Step((int)(seconds * Second));
    }

    private static void EmptyHands(AudioRoom r, mob_living_carbon_human who)
        => r.In(() => { foreach (object held in Dm.each(who.held_items.Copy())) if (held is obj_item o) qdel(o); });

    private static obj_item_fireaxe Axe(AudioRoom r, mob_living_carbon_human who)
    {
        EmptyHands(r, who);
        return r.In(() =>
        {
            var axe = Dm.@new<obj_item_fireaxe>(get_turf(who));
            Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_hands(axe))));
            return axe;
        });
    }

    private static void UseInHand(AudioRoom r, mob_living_carbon_human who)
    {
        // the activate_inhand keybinding (Z): mob/mode, the held item's attack_self
        r.Command(() => ClientSeam.Keybinding(who.client, 4, true));
        r.Command(() => ClientSeam.Keybinding(who.client, 4, false));
        r.Step(2);
    }

    private static List<string> Overlays(AudioRoom r, atom a)
        => r.In(() => Dm.each(a.managed_overlays is DmList l ? l : Dm.list(a.managed_overlays)).Select(Describe).Where(s => s != null).ToList());

    private static string Describe(object o) => o switch
    {
        string s => s,
        mutable_appearance m => m.icon_state,
        DmAppearance d => d.icon_state,
        null => null,
        _ => o.GetType().Name + ":" + o,
    };

    [Fact]
    public void WieldingTakesBothHandsAndRaisesTheForce()
    {
        var r = new AudioRoom(10, 10, seed: 5601);
        mob_living_carbon_human bob = r.Join(4, 5, "Bob");
        r.Step(Second);
        var axe = Axe(r, bob);
        r.In(() =>
        {
            Assert.Equal(5f, axe.force);
            Assert.Equal("fireaxe0", axe.icon_state);
            Assert.False(HAS_TRAIT(axe, TRAIT_WIELDED));
        });

        UseInHand(r, bob); // attack_self: COMSIG_ITEM_ATTACK_SELF, on_attack_self (twohanded.dm:198)
        r.In(() =>
        {
            Assert.True(HAS_TRAIT(axe, TRAIT_WIELDED));
            Assert.Equal(24f, axe.force);
            Assert.Equal("fireaxe1", axe.icon_state); // icon_wielded = "[base_icon_state]1" (fireaxe.dm:50)
            Assert.Equal("fire axe (Wielded)", axe.name);
            var offhand = bob.get_inactive_held_item() as obj_item_offhand;
            Assert.NotNull(offhand);
            Assert.Equal("fire axe (Wielded) - offhand", offhand.name);
        });

        UseInHand(r, bob);
        r.In(() =>
        {
            Assert.False(HAS_TRAIT(axe, TRAIT_WIELDED));
            Assert.Equal(5f, axe.force);
            Assert.Equal("fireaxe0", axe.icon_state);
            Assert.Equal("fire axe", axe.name);
            Assert.Null(bob.get_inactive_held_item());
        });

        // wielded, then dropped: unwielded, the offhand gone (on_drop, on_moved)
        UseInHand(r, bob);
        Assert.True(r.In(() => HAS_TRAIT(axe, TRAIT_WIELDED)));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(axe)));
        r.Step();
        r.In(() =>
        {
            Assert.False(HAS_TRAIT(axe, TRAIT_WIELDED));
            Assert.Equal(5f, axe.force);
            Assert.Empty(Dm.each(bob.held_items).OfType<obj_item>());
        });

        // something in the other hand: no wield (twohanded.dm:228)
        r.In(() => Waiting.NoSleep(bob.put_in_hands(axe)));
        r.In(() => Waiting.NoSleep(bob.put_in_inactive_hand(Dm.@new<obj_item_screwdriver>(get_turf(bob)))));
        UseInHand(r, bob);
        Assert.False(r.In(() => HAS_TRAIT(axe, TRAIT_WIELDED)));
        r.AssertNoRuntimes();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AWieldedAxeDestroysAWindowInOneHitAndAnUnwieldedOneDoesNot(bool reinforced)
    {
        var r = new AudioRoom(10, 10, seed: 5602);
        mob_living_carbon_human bob = r.Join(4, 5, "Bob");
        obj_structure_window window = reinforced ? Place<obj_structure_window_reinforced_fulltile>(r, 5, 5) : Place<obj_structure_window_fulltile>(r, 5, 5);
        Place<obj_structure_grille>(r, 5, 5);
        r.Step(Second);
        var axe = Axe(r, bob);
        r.In(() => bob.set_combat_mode(TRUE));

        float max = r.In(() => window.max_integrity);
        Click(r, bob, window);
        Assert.False(r.In(() => QDELETED(window)), "an unwielded axe only damages the window");
        Assert.True(r.In(() => Dm.num(window.get_integrity())) > max * 0.8f);

        UseInHand(r, bob);
        r.Sounds.Heard.Clear();
        Click(r, bob, window);
        Assert.True(r.In(() => QDELETED(window)), "a wielded axe's afterattack destroys the window (fireaxe.dm:68)");
        // spawn_debris (window.dm:357): two shards of a full-tile window, the glass debris, a reinforced window's two rods
        Assert.Equal(2, On<obj_item_shard>(r, 5, 5).Count);
        Assert.Single(On<obj_effect_decal_cleanable_glass>(r, 5, 5));
        if (reinforced)
            Assert.Equal(2f, r.In(() => Dm.num(Assert.Single(On<obj_item_stack_rods>(r, 5, 5)).get_amount())));
        else
            Assert.Empty(On<obj_item_stack_rods>(r, 5, 5));
        Assert.Single(On<obj_structure_grille>(r, 5, 5)); // the grille stays
        Assert.Contains(r.Sounds.Heard, h => ReferenceEquals(h.Target, bob) && h.File != null && h.File.StartsWith("sound/effects/glass/glassbr", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AWieldedAxeDestroysAGrilleInOneHit()
    {
        var r = new AudioRoom(10, 10, seed: 5603);
        mob_living_carbon_human bob = r.Join(4, 5, "Bob");
        var grille = Place<obj_structure_grille>(r, 5, 5);
        r.Step(Second);
        Axe(r, bob);
        r.In(() => bob.set_combat_mode(TRUE));
        Click(r, bob, grille);
        Assert.False(r.In(() => QDELETED(grille)));
        UseInHand(r, bob);
        Click(r, bob, grille);
        Assert.True(r.In(() => QDELETED(grille)));
        // the first hit may have broken nothing; the destruction deconstructs it into its rods (grille.dm:327)
        float rods = r.In(() => On<obj_item_stack_rods>(r, 5, 5).Sum(s => Dm.num(s.get_amount())));
        Assert.Equal(2f, rods);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheCabinetIsLockedItsGlassBreaksTheAxeComesOutAndGoesBackAndTheGlassIsRepaired()
    {
        var r = new AudioRoom(10, 10, seed: 5604);
        mob_living_carbon_human bob = r.Join(4, 5, "Bob");
        var cabinet = Place<obj_structure_fireaxecabinet>(r, 5, 5);
        r.Step(Second);
        var axe = r.In(() => cabinet.held_item_value as obj_item_fireaxe);
        Assert.NotNull(axe);
        Assert.Contains("axe", Overlays(r, cabinet));
        Assert.Contains("glass", Overlays(r, cabinet));
        Assert.Contains("locked", Overlays(r, cabinet));

        // locked: the hand does not open it (toggle_open, fireaxe.dm:224)
        EmptyHands(r, bob);
        Click(r, bob, cabinet);
        Assert.False(r.In(() => Dm.truthy(cabinet.open)));
        Assert.Same(axe, r.In(() => cabinet.held_item_value));

        // hits crack the glass (glass1..3) and break it at a third of its integrity: glassbr3, two shards
        var box = r.In(() =>
        {
            var b = Dm.@new<obj_item_storage_toolbox_mechanical>(get_turf(bob));
            Waiting.NoSleep(bob.put_in_hands(b));
            bob.set_combat_mode(TRUE);
            return b;
        });
        r.Sounds.Heard.Clear();
        var seen = new List<string>();
        for (int i = 0; i < 60 && !r.In(() => Dm.truthy(cabinet.broken)); i++)
        {
            Click(r, bob, cabinet);
            string glass = Overlays(r, cabinet).FirstOrDefault(s => s.StartsWith("glass", StringComparison.Ordinal));
            if (glass != null && (seen.Count == 0 || seen[^1] != glass))
                seen.Add(glass);
        }
        Assert.True(r.In(() => Dm.truthy(cabinet.broken)));
        Assert.Equal(new[] { "glass", "glass1", "glass2", "glass3", "glass4" }, seen.SkipWhile(s => s == "glass").Prepend("glass").ToArray());
        Assert.Contains(r.Sounds.Heard, h => ReferenceEquals(h.Target, bob) && h.File == "sound/effects/glass/glasshit.ogg");
        Assert.Contains(r.Sounds.Heard, h => ReferenceEquals(h.Target, bob) && h.File == "sound/effects/glass/glassbr3.ogg");
        Assert.Equal(2, On<obj_item_shard>(r, 5, 5).Count);

        // the hand takes the axe from the broken cabinet
        r.In(() => bob.set_combat_mode(FALSE));
        EmptyHands(r, bob);
        Click(r, bob, cabinet);
        Assert.Same(axe, r.In(() => bob.get_active_held_item()));
        Assert.Null(r.In(() => cabinet.held_item_value));
        Assert.DoesNotContain("axe", Overlays(r, cabinet));

        // wielded it does not go back; unwielded it does
        UseInHand(r, bob);
        Click(r, bob, cabinet);
        Assert.Null(r.In(() => cabinet.held_item_value));
        UseInHand(r, bob);
        Click(r, bob, cabinet);
        Assert.Same(axe, r.In(() => cabinet.held_item_value));
        Assert.Contains("axe", Overlays(r, cabinet));

        // two glass sheets repair the glass in 2 s (fireaxe.dm:56)
        r.In(() =>
        {
            var glass = Dm.@new<obj_item_stack_sheet_glass>(get_turf(bob), 5);
            Waiting.NoSleep(bob.put_in_hands(glass));
        });
        Click(r, bob, cabinet, Left, 3);
        r.In(() =>
        {
            Assert.False(Dm.truthy(cabinet.broken));
            Assert.Equal(cabinet.max_integrity, Dm.num(cabinet.get_integrity()));
        });
        Assert.Contains("glass", Overlays(r, cabinet));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AMultitoolUnlocksTheCabinetAndTheHandOpensAndClosesIt()
    {
        var r = new AudioRoom(10, 10, seed: 5605);
        mob_living_carbon_human bob = r.Join(4, 5, "Bob");
        var cabinet = Place<obj_structure_fireaxecabinet>(r, 5, 5);
        r.Step(Second);
        EmptyHands(r, bob);
        r.In(() => Waiting.NoSleep(bob.put_in_hands(Dm.@new<obj_item_multitool>(get_turf(bob)))));
        r.Sounds.Heard.Clear();
        Click(r, bob, cabinet, Left, 3); // toggle_lock: 2 s (fireaxe.dm:216)
        Assert.False(r.In(() => Dm.truthy(cabinet.locked)));
        Assert.Contains("unlocked", Overlays(r, cabinet));
        Assert.Contains(r.Sounds.Heard, h => ReferenceEquals(h.Target, bob) && h.File == "sound/machines/locktoggle.ogg");

        EmptyHands(r, bob);
        r.Sounds.Heard.Clear();
        Click(r, bob, cabinet); // opens
        Assert.True(r.In(() => Dm.truthy(cabinet.open)));
        Assert.Contains("glass_raised", Overlays(r, cabinet));
        Assert.Contains(r.Sounds.Heard, h => ReferenceEquals(h.Target, bob) && h.File == "sound/machines/click.ogg");
        Click(r, bob, cabinet); // takes the axe
        Assert.IsType<obj_item_fireaxe>(r.In(() => bob.get_active_held_item()));
        r.In(() => bob.swap_hand());
        Click(r, bob, cabinet, "right=1;button=right;icon-x=16;icon-y=16"); // attack_hand_secondary closes it
        Assert.False(r.In(() => Dm.truthy(cabinet.open)));
        r.AssertNoRuntimes();
    }
}
