using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Tg.M2Appearance.P3.AppearanceFixtures;

namespace Anomaly.Core.Tests.Tg.M2Appearance.P3;

/// <summary>
/// The HUD state (D107): tg's screen objects made by tg's code, in the player's client.screen, which the replicator sends
/// privately. Expected values from the DM: hud.dm:85-393 and human.dm:4-35 (the HUD), screen_objects.dm:953-1036 (the doll),
/// carbon.dm:479-598 (the fullscreens and the health indicator), alert.dm:15-95 and :1201-1233 (alerts).
/// </summary>
public class HudTests
{
    private static mob_living_carbon_human_test_p3_body Player(int x = 4, int y = 4)
    {
        var human = Human(x, y, damageParts: true);
        human.client = Dm.@new<client_test_m1_client>();
        human.client.view = "15x15"; // tg's world.view (config VIEW), what a connected client has
        Assert.True(Dm.eq(TRUE, Waiting.NoSleep(human.create_mob_hud())));
        return human;
    }

    private static atom_movable_screen_healthdoll_human Doll(mob human)
        => (atom_movable_screen_healthdoll_human)human.hud_used.screen_objects[HUD_MOB_HEALTHDOLL];

    private static string DollState(mob human, string zone) => ((atom)Doll(human).limbs[zone]).icon_state;

    private static List<object> Screen(mob m) => Dm.each(m.client.screen).Cast<object>().ToList();

    [Fact]
    public void CreateMobHudShowsTheDollTheIndicatorAndTheWalkRunButtonInTheClientsScreen()
    {
        TgToyRoom room = TgToyRoom.Boot(seed: 61);
        room.In(() =>
        {
            DmWorldState.Current.NotTranslatedThrows = true;
            var human = Player();
            Assert.IsType<datum_hud_human>(human.hud_used);
            Assert.Same(human, human.hud_used.mymob);
            // datum/hud/New -> ui_style2icon(null): the first style, Midnight
            Assert.True(Dm.eq(Dm.resource("icons/hud/screen_midnight.dmi"), human.hud_used.ui_style));

            // show_hud is not called by create_mob_hud (mob/Login does it): call it as Login does
            Waiting.NoSleep(human.hud_used.show_hud(human.hud_used.hud_version));
            List<object> screen = Screen(human);
            Assert.DoesNotContain(null, screen);
            var doll = Doll(human);
            var health = (atom_movable_screen_healths)human.hud_used.screen_objects[HUD_MOB_HEALTH];
            var walk = (atom_movable_screen_mov_intent)human.hud_used.screen_objects[HUD_MOB_MOVE_INTENT];
            Assert.Contains(doll, screen);
            Assert.Contains(health, screen);
            Assert.Contains(walk, screen);
            Assert.Equal(ui_healthdoll, doll.screen_loc);
            Assert.Equal(ui_health, health.screen_loc);
            Assert.Equal(ui_movi, walk.screen_loc);
            Assert.Equal("running", walk.icon_state); // move_intent defaults to MOVE_INTENT_RUN
            // HUD_GROUP_INFO holds the doll and the indicator, HUD_GROUP_STATIC the walk/run button (human.dm:13, :32, :34)
            Assert.True(Dm.@in(doll, human.hud_used.screen_groups[HUD_GROUP_INFO]));
            Assert.True(Dm.@in(walk, human.hud_used.screen_groups[HUD_GROUP_STATIC]));
            // the doll's six limb atoms in its vis_contents, in get_all_limbs order, the chest a little above
            Assert.Equal(6f, doll.vis_contents.len);
            Assert.Equal(doll.layer + 0.05f, ((atom)doll.limbs[BODY_ZONE_CHEST]).layer);
            Assert.Equal("chest0", DollState(human, BODY_ZONE_CHEST)); // update_appearance in Initialize: undamaged
            Assert.Equal("health0", health.icon_state);
            DmWorldState.Current.NotTranslatedThrows = false;
        });
        Assert.Empty(room.RuntimeText);
    }

    [Theory]
    [InlineData(0f, "0")]
    [InlineData(1f, "1")]     // ceil(1/200 x 5) = 1
    [InlineData(40f, "1")]    // max/5 exactly: ceil(1) = 1
    [InlineData(41f, "2")]
    [InlineData(120f, "3")]
    [InlineData(199f, "5")]
    [InlineData(200f, "5")]
    [InlineData(500f, "5")]   // clamp(.., 0, 5)
    public void TheDollsChestStateIsCeilOfDamageOverMaxTimesFive(float brute, string state)
    {
        TgToyRoom room = TgToyRoom.Boot(seed: 62);
        room.In(() =>
        {
            DmWorldState.Current.NotTranslatedThrows = true;
            var human = Player();
            Part(human, BODY_ZONE_CHEST).brute_dam = brute; // the chest's max_damage is LIMB_MAX_HP_CORE, 200
            human.update_health_hud();
            Assert.Equal("chest" + state, DollState(human, BODY_ZONE_CHEST));
            Assert.Equal("head0", DollState(human, BODY_ZONE_HEAD));
            DmWorldState.Current.NotTranslatedThrows = false;
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void AMissingZoneIsSixADisabledOneSevenBurnCountsAndDeathIsDead()
    {
        TgToyRoom room = TgToyRoom.Boot(seed: 63);
        room.In(() =>
        {
            DmWorldState.Current.NotTranslatedThrows = true;
            var human = Player();
            Dm.remove(ref human.test_parts, Part(human, BODY_ZONE_HEAD));
            Part(human, BODY_ZONE_L_ARM).bodypart_disabled = TRUE;
            Part(human, BODY_ZONE_R_LEG).burn_dam = 25; // 25/50 x 5 = 2.5 -> 3
            human.update_health_hud();
            Assert.Equal("head6", DollState(human, BODY_ZONE_HEAD));
            Assert.Equal("l_arm7", DollState(human, BODY_ZONE_L_ARM));
            Assert.Equal("r_leg3", DollState(human, BODY_ZONE_R_LEG));

            human.stat = DEAD;
            human.update_health_hud();
            foreach (string zone in new[] { BODY_ZONE_L_ARM, BODY_ZONE_R_ARM, BODY_ZONE_HEAD, BODY_ZONE_L_LEG, BODY_ZONE_R_LEG, BODY_ZONE_CHEST })
                Assert.Equal(zone + "DEAD", DollState(human, zone));
            Assert.Equal("health7", ((atom)human.hud_used.screen_objects[HUD_MOB_HEALTH]).icon_state);
            DmWorldState.Current.NotTranslatedThrows = false;
        });
        Assert.Empty(room.RuntimeText);
    }

    [Theory]
    [InlineData(100f, "health0")]
    [InlineData(99f, "health1")]  // 6 - ceil(99 / 20)
    [InlineData(80f, "health2")]
    [InlineData(50f, "health3")]
    [InlineData(1f, "health5")]
    [InlineData(0f, "health6")]
    [InlineData(-40f, "health6")]
    public void TheHealthIndicatorFollowsCarbonsFormula(float health, string state)
    {
        TgToyRoom room = TgToyRoom.Boot(seed: 64);
        room.In(() =>
        {
            DmWorldState.Current.NotTranslatedThrows = true;
            var human = Player();
            human.health = health;
            human.update_health_hud();
            Assert.Equal(state, ((atom)human.hud_used.screen_objects[HUD_MOB_HEALTH]).icon_state);
            DmWorldState.Current.NotTranslatedThrows = false;
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void TheCritOxyAndBruteFullscreensFollowCarbonsThresholdsAndClearAfterTheirFade()
    {
        TgToyRoom room = TgToyRoom.Boot(seed: 65);
        mob_living_carbon_human_test_p3_body human = null;
        room.In(() =>
        {
            DmWorldState.Current.NotTranslatedThrows = true;
            human = Player();
            human.health = -15;          // crit_threshold 0: crit severity 1 (-20 to -10)
            human.stat = SOFT_CRIT;      // not HARD_CRIT: the critvision too, -16 to -12 -> 7
            human.oxyloss = 20;          // 10 to 20 -> 1 (the first range that matches)
            human.test_brute = 50;       // 45 to 70 -> 4
            human.update_damage_hud();
            string State(string category) => ((atom)human.screens[category]).icon_state;
            Assert.Equal("passage1", State("crit"));
            Assert.Equal("oxydamageoverlay7", State("critvision"));
            Assert.Equal("oxydamageoverlay1", State("oxy"));
            Assert.Equal("brutedamageoverlay4", State("brute"));
            foreach (string category in new[] { "crit", "critvision", "oxy", "brute" })
                Assert.Contains(human.screens[category], Screen(human));

            human.stat = HARD_CRIT;
            human.health = -100;         // -INFINITY to -95 -> 10; HARD_CRIT clears the critvision
            human.update_damage_hud();
            Assert.Equal("passage10", State("crit"));
            Assert.Null(human.screens["critvision"]);

            human.health = 100;
            human.stat = STABLE;
            human.oxyloss = 0;
            human.test_brute = 0;
            human.update_damage_hud();
            Assert.Equal(0f, human.screens.len); // clear_fullscreen drops them from the categories at once
            DmWorldState.Current.NotTranslatedThrows = false;
        });
        room.World.Step(30); // the 10 ds fade (clear_fullscreen's animated = 10, TIMER_CLIENT_TIME): 20 ticks, and a margin
        room.In(() => Assert.DoesNotContain(Screen(human), o => o is atom_movable_screen_fullscreen));
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void AlertsAppearInPlaceOrderAndClear()
    {
        TgToyRoom room = TgToyRoom.Boot(seed: 66);
        room.In(() =>
        {
            DmWorldState.Current.NotTranslatedThrows = true;
            var human = Player();
            var oxy = (atom_movable_screen_alert)human.throw_alert(ALERT_NOT_ENOUGH_OXYGEN, typeof(atom_movable_screen_alert_not_enough_oxy));
            var co2 = (atom_movable_screen_alert)human.throw_alert(ALERT_TOO_MUCH_CO2, typeof(atom_movable_screen_alert_too_much_co2));
            Assert.Same(oxy, human.alerts[ALERT_NOT_ENOUGH_OXYGEN]);
            Assert.Same(oxy, human.throw_alert(ALERT_NOT_ENOUGH_OXYGEN, typeof(atom_movable_screen_alert_not_enough_oxy))); // thrown already
            Assert.Equal("not_enough_oxy", oxy.icon_state);
            Assert.Same(human, oxy.owner);
            // get_ui_alert_placement(index): "EAST-1:28,CENTER+[6 - index]:[29 - (index * 2)]"
            Assert.Equal("EAST-1:28,CENTER+5:27", oxy.screen_loc);
            Assert.Equal("EAST-1:28,CENTER+4:25", co2.screen_loc);
            Assert.Contains(oxy, Screen(human));
            Assert.Contains(co2, Screen(human));
            // the slide-in animation ends on the identity matrix, which BYOND applies at once
            var m = (matrix)oxy.transform;
            Assert.Equal((1f, 0f, 0f, 0f, 1f, 0f), (m.a, m.b, m.c, m.d, m.e, m.f));
            Assert.True(Dm.eq(TRUE, human.has_alert(ALERT_NOT_ENOUGH_OXYGEN)));

            human.clear_alert(ALERT_NOT_ENOUGH_OXYGEN);
            Assert.False(Dm.eq(TRUE, human.has_alert(ALERT_NOT_ENOUGH_OXYGEN)));
            Assert.DoesNotContain(oxy, Screen(human));
            Assert.True(QDELETED(oxy));
            Assert.Equal("EAST-1:28,CENTER+5:27", co2.screen_loc); // reorganized into the first place
            DmWorldState.Current.NotTranslatedThrows = false;
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void TheSuccumbAlertTakesTheHudStyleAndShowsItsGhostOverlay()
    {
        TgToyRoom room = TgToyRoom.Boot(seed: 67);
        room.In(() =>
        {
            DmWorldState.Current.NotTranslatedThrows = true;
            var human = Player();
            // (alert.cs Initialize works around the lead's NotTranslated update_icon line, atom_appearance.dm:62: GAP(lead))
            var succumb = (atom_movable_screen_alert)human.throw_alert(ALERT_SUCCUMB, typeof(atom_movable_screen_alert_succumb));
            Assert.True(Dm.eq(human.hud_used.ui_style, succumb.icon)); // USER_HUD_STYLE_INHERIT
            var ghost = (IDmAppearance)succumb.overlays.CopyItems().Single();
            Assert.Equal("ghost", ghost.icon_state);
            Assert.True(Dm.eq(Dm.resource("icons/mob/simple/mob.dmi"), ghost.icon));
            Assert.Same(ghost, succumb.managed_overlays); // update_icon keeps a single overlay as the managed one (atom_appearance.dm:92)
            Assert.Equal((float)MOUSE_HAND_POINTER, succumb.mouse_over_pointer); // clickable_glow
            DmWorldState.Current.NotTranslatedThrows = false;
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void AnotherPlayersClientNeverGetsThisPlayersScreenObjects()
    {
        TgToyRoom room = TgToyRoom.Boot(seed: 68);
        room.In(() =>
        {
            DmWorldState.Current.NotTranslatedThrows = true;
            var alice = Player(4, 4);
            var bob = Player(6, 4);
            Waiting.NoSleep(alice.hud_used.show_hud(alice.hud_used.hud_version));
            Waiting.NoSleep(bob.hud_used.show_hud(bob.hud_used.hud_version));
            alice.throw_alert(ALERT_NOT_ENOUGH_OXYGEN, typeof(atom_movable_screen_alert_not_enough_oxy));
            alice.health = -15;
            alice.update_damage_hud();
            List<object> bobs = Screen(bob);
            foreach (object own in Screen(alice))
                Assert.DoesNotContain(own, bobs);
            DmWorldState.Current.NotTranslatedThrows = false;
        });
        Assert.Empty(room.RuntimeText);
    }
}
