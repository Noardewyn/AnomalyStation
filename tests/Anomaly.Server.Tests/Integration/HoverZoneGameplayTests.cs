using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Package T, slice T4b (protocol 100 also): the hover carries the body zone looked at. Through a real server and clients: the server selects it as tg's
/// <c>zone_selected</c> without tg's log (<c>set_selected_zone(..., should_log = FALSE)</c>) before tg's hover, so the hints of the same mob follow the
/// look (carbon_context's Headpat on the head, Hug on the chest); a zone on a thing that is no carbon changes nothing; while any of tg's do-afters runs
/// for the looker the zone stays (the gun at the mouth keeps the mouth and fires), a click still sets it, and the look applies again after; a burst of
/// hovers is held to the hover bucket; the client paces zone changes and the last one is the server's; and a hover's zone replays to the same world.
/// </summary>
public class HoverZoneGameplayTests
{
    private const int Second = 20;
    private const string ZoneLogSite = "code/_onclick/hud/screen_objects/screen_objects.dm:829"; // set_selected_zone's log_manual_zone_selected_update

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void In(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static ClickTarget On(atom target, AimZone zone) => new(TargetKind.Entity, target.dm_id, 0, 0, 0, 16, 20, zone);

    // Alice and Bob on neighbouring tiles, Alice's hands empty, out of combat mode.
    private static (RealHeadlessWorld W, RealHeadlessWorld.Player Alice, mob_living_carbon_human AliceMob, mob_living_carbon_human BobMob) Pair(ulong seed)
    {
        var w = new RealHeadlessWorld(new LoopbackOptions { Seed = seed, TimeoutSteps = 9000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        In(w, () =>
        {
            turf start = get_turf(aliceMob);
            Waiting.NoSleep(bobMob.forceMove(locate(Dm.num(start.x) + 1, Dm.num(start.y), Dm.num(start.z))));
        });
        w.Run(Second);
        Assert.Null(In(w, () => aliceMob.get_active_held_item()));
        Assert.False(In(w, () => Dm.truthy(aliceMob.combat_mode)));
        return (w, alice, aliceMob, bobMob);
    }

    private static ContextHintsMessage LastHintsOn(List<ContextHintsMessage> hints, atom target) => hints.LastOrDefault(h => h.Target == target.dm_id);

    private static string LeftClickLabel(ContextHintsMessage hints)
        => hints?.Hints.FirstOrDefault(h => h.Button == ClickButton.Left && h.Modifiers == ClickModifiers.None).Label;

    [Fact]
    public void The_look_moves_the_selected_zone_without_a_log_line_and_the_hints_of_the_same_mob_follow_it()
    {
        var (w, alice, aliceMob, bobMob) = Pair(61);
        using (w)
        {
            var hints = new List<ContextHintsMessage>();
            alice.Client.ContextHintsReceived += hints.Add;
            Assert.DoesNotContain(ZoneLogSite, In(w, () => DmWorldState.Of(w.World).DeferredSites));

            Assert.True(alice.Session.Hover(On(bobMob, AimZone.Head), w.Now));
            w.Run(4);
            Assert.Equal(BODY_ZONE_HEAD, In(w, () => aliceMob.zone_selected));
            Assert.Equal("Headpat", LeftClickLabel(LastHintsOn(hints, bobMob)));

            // the same mob, the chest: sent again (the zone changed), tg's hover of the same atom builds the hints again
            int sent = alice.Session.HoversSent;
            Assert.True(alice.Session.Hover(On(bobMob, AimZone.Chest), w.Now));
            Assert.Equal(sent + 1, alice.Session.HoversSent);
            w.Run(4);
            Assert.Equal(BODY_ZONE_CHEST, In(w, () => aliceMob.zone_selected));
            Assert.Equal("Hug", LeftClickLabel(LastHintsOn(hints, bobMob)));

            // the eyes: tg's check_zone reads them as the head again
            Assert.True(alice.Session.Hover(On(bobMob, AimZone.Eyes), w.Now));
            w.Run(4);
            Assert.Equal(BODY_ZONE_PRECISE_EYES, In(w, () => aliceMob.zone_selected));
            Assert.Equal("Headpat", LeftClickLabel(LastHintsOn(hints, bobMob)));

            // the same zone again: nothing is sent, until the repeat (DV691: PlaySession.RepeatHoverInterval after the last hover) sends it again
            Assert.False(alice.Session.Hover(On(bobMob, AimZone.Eyes), w.Now));
            Assert.False(alice.Session.Hover(On(bobMob, AimZone.Eyes) with { IconY = 22 }, w.Now + 0.1));
            Assert.True(alice.Session.Hover(On(bobMob, AimZone.Eyes) with { IconY = 22 }, w.Now + 1));

            // a look is no choice of the player's: tg's should_log = FALSE, so set_selected_zone never reached its log
            Assert.DoesNotContain(ZoneLogSite, In(w, () => DmWorldState.Of(w.World).DeferredSites));
            Assert.Contains(w.Host.Log.Commands, c => c.Kind == CommandKind.Action && c.Action == ActionKind.Hover && c.AimZone == AimZone.Chest);

            // the positive control: a click that changes the zone goes through the same zone selector with tg's log, which does reach the site
            Assert.True(alice.Session.Click(On(bobMob, AimZone.LeftArm), ClickButton.Left, ClickModifiers.None));
            w.Run(4);
            Assert.Equal(BODY_ZONE_L_ARM, In(w, () => aliceMob.zone_selected));
            Assert.Contains(ZoneLogSite, In(w, () => DmWorldState.Of(w.World).DeferredSites));
        }
    }

    [Fact]
    public void Looking_at_ones_own_body_selects_the_zone_and_a_swing_at_it_hits_that_part()
    {
        // Slice T5b: the own body looked down at is a target as anyone's: a hover on one's own mob carries the zone and the server selects it,
        // with one's own hints; a toolbox swung at it in combat mode hits oneself on the part clicked (tg's attacked_by never misses oneself)
        var (w, alice, aliceMob, _) = Pair(66);
        using (w)
        {
            var hints = new List<ContextHintsMessage>();
            alice.Client.ContextHintsReceived += hints.Add;
            var texts = new List<string>();
            alice.Client.ServerTextReceived += t => texts.Add(t.Html);
            Assert.True(alice.Session.Hover(On(aliceMob, AimZone.LeftLeg), w.Now));
            w.Run(4);
            Assert.Equal(BODY_ZONE_L_LEG, In(w, () => aliceMob.zone_selected));
            Assert.NotNull(LastHintsOn(hints, aliceMob));

            w.Host.EnqueueConsoleLine("give Alice /obj/item/storage/toolbox/mechanical");
            alice.Session.ToggleCombatMode();
            w.Run(10);
            var arm = In(w, () => (obj_item_bodypart)aliceMob.get_bodypart(BODY_ZONE_R_ARM));
            float before = In(w, () => Dm.num(arm.brute_dam));
            Assert.True(alice.Session.Click(On(aliceMob, AimZone.RightArm), ClickButton.Left, ClickModifiers.None));
            w.Run(Second);
            Assert.Equal(BODY_ZONE_R_ARM, In(w, () => aliceMob.zone_selected));
            Assert.True(In(w, () => Dm.num(arm.brute_dam)) > before, "the right arm took the hit");
            Assert.Contains(texts, t => t.Contains("yourself in the right arm", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void A_zone_on_a_thing_that_is_no_carbon_leaves_the_selected_zone_alone()
    {
        var (w, alice, aliceMob, bobMob) = Pair(62);
        using (w)
        {
            Assert.True(alice.Session.Hover(On(bobMob, AimZone.LeftLeg), w.Now));
            w.Run(4);
            Assert.Equal(BODY_ZONE_L_LEG, In(w, () => aliceMob.zone_selected));
            var pen = In(w, () => Dm.@new<obj_item_pen>(get_turf(bobMob)));
            var sloth = In(w, () => (mob_living)DevCommands.Spawn(typeof(mob_living_basic_sloth_citrus), get_turf(bobMob), 1, 8, out _).Single());
            w.Run(2);
            int applied = w.Host.SessionOf(1).HoversApplied;
            foreach (atom thing in new atom[] { pen, sloth })
            {
                // the client sends no zone for these (AimZoneAt: no body parts); a forged one is applied as a plain hover
                alice.Client.SendAction(new ActionMessage(alice.Session.Prediction.NewestSeq, ActionKind.Hover, thing.dm_id, ClickButton.None, ClickModifiers.None, null)
                    { TargetKind = TargetKind.Entity, AimZone = AimZone.Head });
                w.Run(4);
                Assert.Equal(BODY_ZONE_L_LEG, In(w, () => aliceMob.zone_selected));
            }
            Assert.Equal(applied + 2, w.Host.SessionOf(1).HoversApplied);
            Assert.Equal(0, w.Host.SessionOf(1).Strikes);
        }
    }

    [Fact]
    public void During_a_do_after_the_look_keeps_the_zone_the_gun_at_the_mouth_fires_and_after_it_the_look_applies_again()
    {
        var (w, alice, aliceMob, bobMob) = Pair(63);
        using (w)
        {
            var texts = new List<string>();
            alice.Client.ServerTextReceived += t => texts.Add(t.Html);
            In(w, () => DevCommands.Give(aliceMob, "/obj/item/gun/energy/laser"));
            w.Run(Second);
            var gun = In(w, () => aliceMob.get_active_held_item() as obj_item_gun);
            Assert.NotNull(gun);
            float beam = In(w, () => Dm.num(gun.chambered.loaded_projectile.damage));

            // tg's handle_suicide from the next tile: a 12 second do-after. The click carries the camera's aim at the head, as a first-person client's
            // does, so the shot's zone is the part its line crosses (DV95), not tg's distance roll
            Assert.True(alice.Session.Click(On(bobMob, AimZone.Mouth), ClickButton.Left, ClickModifiers.None, GunInMouthGameplayTests.AtHead(w, alice, bobMob)));
            w.Run(Second);
            Assert.Equal(BODY_ZONE_PRECISE_MOUTH, In(w, () => aliceMob.zone_selected));
            Assert.True(In(w, () => LAZYLEN(aliceMob.do_afters)) > 0);

            // the player looks at the head, the chest, a leg during the wait: the hovers run, the zone stays the mouth
            int applied = w.Host.SessionOf(1).HoversApplied;
            foreach (AimZone look in new[] { AimZone.Head, AimZone.Chest, AimZone.LeftLeg })
            {
                Assert.True(alice.Session.Hover(On(bobMob, look), w.Now));
                w.Run(Second);
                Assert.Equal(BODY_ZONE_PRECISE_MOUTH, In(w, () => aliceMob.zone_selected));
            }
            Assert.Equal(applied + 3, w.Host.SessionOf(1).HoversApplied);
            Assert.DoesNotContain(texts, t => t.Contains("decided to spare", StringComparison.Ordinal));

            // the do-after ends as if the player had not looked away: the trigger, five times the beam on the head
            var hints = new List<ContextHintsMessage>();
            alice.Client.ContextHintsReceived += hints.Add;
            float landed = 0f;
            w.Run(12 * Second, _ =>
            {
                if (landed == 0f)
                    landed = In(w, () => Dm.num(((obj_item_bodypart)bobMob.get_bodypart(BODY_ZONE_HEAD)).burn_dam));
            });
            Assert.Contains(texts, t => t.Contains("pulls the trigger!", StringComparison.Ordinal));
            Assert.Equal(5 * beam, landed, 2);
            Assert.Equal(0f, In(w, () => LAZYLEN(aliceMob.do_afters)));

            // fix round 1: once the do-after is over the server applies the last look itself (the crosshair resting on the leg sends nothing more),
            // and tg's hover of Bob runs again, so fresh hints went out
            Assert.Equal(BODY_ZONE_L_LEG, In(w, () => aliceMob.zone_selected));
            Assert.Contains(hints, h => h.Target == bobMob.dm_id);
            Assert.True(alice.Session.Hover(On(bobMob, AimZone.Chest), w.Now));
            w.Run(4);
            Assert.Equal(BODY_ZONE_CHEST, In(w, () => aliceMob.zone_selected));
        }
    }

    [Fact]
    public void A_click_during_a_do_after_still_sets_the_zone()
    {
        var (w, alice, aliceMob, bobMob) = Pair(64);
        using (w)
        {
            var texts = new List<string>();
            alice.Client.ServerTextReceived += t => texts.Add(t.Html);
            In(w, () => DevCommands.Give(aliceMob, "/obj/item/gun/energy/laser"));
            w.Run(Second);
            Assert.True(alice.Session.Click(On(bobMob, AimZone.Mouth), ClickButton.Left, ClickModifiers.None));
            w.Run(Second);
            Assert.True(In(w, () => LAZYLEN(aliceMob.do_afters)) > 0);
            // a hover is ignored, a click is not (T4a's rule): the zone moves off the mouth, so tg's handle_suicide spares him at the end
            Assert.True(alice.Session.Hover(On(bobMob, AimZone.Head), w.Now));
            w.Run(4);
            Assert.Equal(BODY_ZONE_PRECISE_MOUTH, In(w, () => aliceMob.zone_selected));
            Assert.True(alice.Session.Click(On(bobMob, AimZone.Chest), ClickButton.Left, ClickModifiers.None));
            w.Run(4);
            Assert.Equal(BODY_ZONE_CHEST, In(w, () => aliceMob.zone_selected));
            w.Run(12 * Second);
            Assert.Contains(texts, t => t.Contains("decided to spare", StringComparison.Ordinal));
            Assert.DoesNotContain(texts, t => t.Contains("pulls the trigger", StringComparison.Ordinal));
            // the click cleared the look skipped before it: the head is not applied after the do-after
            Assert.Equal(0f, In(w, () => LAZYLEN(aliceMob.do_afters)));
            Assert.Equal(BODY_ZONE_CHEST, In(w, () => aliceMob.zone_selected));
        }
    }

    // Fix round 1: tg's own do_after of the looker with a target (as a surgery step's), started in the world with empty hands so the hints differ by zone.
    private static void StartDoAfter(RealHeadlessWorld w, mob_living_carbon_human user, atom target, int deciseconds)
    {
        In(w, () => Waiting.Detach(do_after(user, deciseconds, target)));
        Assert.True(In(w, () => LAZYLEN(user.do_afters)) > 0);
    }

    [Fact]
    public void A_look_skipped_during_a_do_after_is_applied_when_it_ends_with_fresh_hints()
    {
        var (w, alice, aliceMob, bobMob) = Pair(67);
        using (w)
        {
            var hints = new List<ContextHintsMessage>();
            alice.Client.ContextHintsReceived += hints.Add;
            Assert.True(alice.Session.Hover(On(bobMob, AimZone.Head), w.Now));
            w.Run(4);
            Assert.Equal("Headpat", LeftClickLabel(LastHintsOn(hints, bobMob)));

            StartDoAfter(w, aliceMob, bobMob, 50);
            Assert.True(alice.Session.Hover(On(bobMob, AimZone.Chest), w.Now));
            w.Run(4);
            Assert.Equal(BODY_ZONE_HEAD, In(w, () => aliceMob.zone_selected)); // frozen
            Assert.Equal("Headpat", LeftClickLabel(LastHintsOn(hints, bobMob)));

            // the do-after ends with nothing more sent: within two ticks the look is the zone and the hints say so
            int sent = alice.Session.HoversSent;
            long endTick = 0;
            w.Run(6 * Second, tick =>
            {
                if (endTick == 0 && In(w, () => LAZYLEN(aliceMob.do_afters)) == 0)
                    endTick = tick;
            });
            Assert.NotEqual(0L, endTick);
            Assert.Equal(sent, alice.Session.HoversSent);
            Assert.Equal(BODY_ZONE_CHEST, In(w, () => aliceMob.zone_selected));
            Assert.Equal("Hug", LeftClickLabel(LastHintsOn(hints, bobMob)));
            Assert.DoesNotContain(ZoneLogSite, In(w, () => DmWorldState.Of(w.World).DeferredSites));

            // and how soon: a second do-after, the head looked at, watched tick by tick
            StartDoAfter(w, aliceMob, bobMob, 20);
            Assert.True(alice.Session.Hover(On(bobMob, AimZone.Head), w.Now));
            long ended = 0, applied = 0;
            w.Run(4 * Second, tick =>
            {
                if (ended == 0 && In(w, () => LAZYLEN(aliceMob.do_afters)) == 0)
                    ended = tick;
                if (applied == 0 && In(w, () => aliceMob.zone_selected) == BODY_ZONE_HEAD)
                    applied = tick;
            });
            Assert.NotEqual(0L, ended);
            Assert.InRange(applied - ended, 0L, 2L);
            Assert.Equal("Headpat", LeftClickLabel(LastHintsOn(hints, bobMob)));
        }
    }

    [Fact]
    public void An_unhover_during_a_do_after_clears_the_skipped_look()
    {
        var (w, alice, aliceMob, bobMob) = Pair(68);
        using (w)
        {
            Assert.True(alice.Session.Hover(On(bobMob, AimZone.Head), w.Now));
            w.Run(4);
            StartDoAfter(w, aliceMob, bobMob, 50);
            Assert.True(alice.Session.Hover(On(bobMob, AimZone.Chest), w.Now));
            w.Run(4);
            Assert.True(alice.Session.Hover(ClickTarget.None, w.Now));
            Assert.Equal(1, alice.Session.UnhoversSent);
            w.Run(6 * Second);
            Assert.Equal(0f, In(w, () => LAZYLEN(aliceMob.do_afters)));
            Assert.Equal(BODY_ZONE_HEAD, In(w, () => aliceMob.zone_selected));
        }
    }

    [Fact]
    public void A_skipped_look_at_a_body_deleted_or_gone_from_view_is_dropped_without_a_runtime()
    {
        var (w, alice, aliceMob, bobMob) = Pair(69);
        using (w)
        {
            Assert.True(alice.Session.Hover(On(bobMob, AimZone.Head), w.Now));
            w.Run(4);
            int runtimes = w.World.RuntimeErrorCount;
            foreach (bool delete in new[] { true, false })
            {
                var monkey = In(w, () => (mob_living_carbon_human)DevCommands.Spawn(typeof(mob_living_carbon_human_species_monkey), get_step(aliceMob, Dm.WEST) as turf, 1, 8, out _).Single());
                In(w, () => qdel(monkey.ai_controller as datum));
                w.Run(2);
                StartDoAfter(w, aliceMob, bobMob, 50);
                Assert.True(alice.Session.Hover(On(monkey, AimZone.Chest), w.Now));
                w.Run(4);
                Assert.Equal(BODY_ZONE_HEAD, In(w, () => aliceMob.zone_selected));
                if (delete)
                    In(w, () => qdel(monkey));
                else
                {
                    // out of Alice's sight: above her see_invisible, which a hover's target check refuses as BYOND shows nothing there
                    In(w, () => monkey.invisibility = INVISIBILITY_ABSTRACT);
                    Assert.True(In(w, () => Dm.num(monkey.invisibility) > Dm.num(aliceMob.see_invisible)));
                }
                w.Run(6 * Second);
                Assert.Equal(0f, In(w, () => LAZYLEN(aliceMob.do_afters)));
                Assert.Equal(BODY_ZONE_HEAD, In(w, () => aliceMob.zone_selected));
            }
            Assert.Equal(runtimes, w.World.RuntimeErrorCount);
        }
    }

    // Fix round 1 (F2, the manager's decision): in cursor mode a hover carries no zone; through the crosshair a hover on an entity's pixel does.
    [Theory]
    [InlineData(TargetKind.Entity, (byte)16, false, true)]
    [InlineData(TargetKind.Entity, (byte)16, true, false)]
    [InlineData(TargetKind.Entity, (byte)0, false, false)]
    [InlineData(TargetKind.Turf, (byte)16, false, false)]
    [InlineData(TargetKind.Screen, (byte)16, false, false)]
    public void A_hover_carries_the_zone_through_the_crosshair_only(TargetKind kind, byte iconX, bool cursorMode, bool carries)
        => Assert.Equal(carries, PlaySession.HoverCarriesZone(new ClickTarget(kind, kind == TargetKind.Turf ? 0 : 5, 1, 1, 1, iconX, 20), cursorMode));

    [Fact]
    public void A_burst_of_zone_changing_hovers_is_held_to_the_hover_bucket()
    {
        var (w, alice, aliceMob, bobMob) = Pair(65);
        using (w)
        {
            w.Run(Second); // the hover bucket full again (its burst)
            Session s = w.Host.SessionOf(1);
            int applied = s.HoversApplied, dropped = s.HoversDropped, strikes = s.Strikes, limited = s.RateLimited;
            int bucket = ActionLimits.Hover.Burst;
            int burst = bucket + 7;
            for (int i = 0; i < burst; i++)
                alice.Client.SendAction(new ActionMessage(alice.Session.Prediction.NewestSeq, ActionKind.Hover, bobMob.dm_id, ClickButton.None, ClickModifiers.None, null)
                    { TargetKind = TargetKind.Entity, AimZone = i % 2 == 0 ? AimZone.Head : AimZone.Chest });
            w.Run(4);
            // at most the bucket's burst passes, one hover a tick runs, the rest are dropped silently: no strike, no rate-limit count
            Assert.Equal(burst, s.HoversApplied - applied + s.HoversDropped - dropped);
            Assert.InRange(s.HoversApplied - applied, 1, bucket);
            Assert.True(s.HoversDropped - dropped >= burst - bucket);
            Assert.Equal((strikes, limited), (s.Strikes, s.RateLimited));
            Assert.Equal(SessionState.Joined, s.State);
            Assert.Contains(In(w, () => aliceMob.zone_selected), new[] { BODY_ZONE_HEAD, BODY_ZONE_CHEST });
        }
    }

    [Fact]
    public void The_client_paces_a_sweep_over_a_body_and_the_server_ends_on_the_last_zone()
    {
        var (w, alice, aliceMob, bobMob) = Pair(66);
        using (w)
        {
            // within the zone interval a new zone waits; going back to the server's zone sends nothing
            double t = w.Now;
            Assert.True(alice.Session.Hover(On(bobMob, AimZone.Head), t));
            int sent = alice.Session.HoversSent;
            Assert.False(alice.Session.Hover(On(bobMob, AimZone.Chest), t + 0.05));
            Assert.False(alice.Session.Hover(On(bobMob, AimZone.Head), t + 0.10));
            Assert.False(alice.Session.Hover(On(bobMob, AimZone.Head), t + 0.15));
            Assert.Equal(sent, alice.Session.HoversSent);
            Assert.False(alice.Session.Hover(On(bobMob, AimZone.Chest), t + 0.20));
            Assert.True(alice.Session.Hover(On(bobMob, AimZone.Chest), t + 0.30));
            Assert.Equal(sent + 1, alice.Session.HoversSent);
            w.Run(Second);

            // fix round 1 (F3): a sweep far longer than the server's bucket lasts (its burst of ActionLimits.Hover.Burst at a refill of PerSecond):
            // a new zone every client frame (four frames a tick) for ten seconds, then the crosshair rests on the right leg
            Session s = w.Host.SessionOf(1);
            int start = alice.Session.HoversSent, dropped = s.HoversDropped;
            AimZone[] sweep = { AimZone.Head, AimZone.Eyes, AimZone.Mouth, AimZone.Chest, AimZone.Groin, AimZone.LeftArm, AimZone.RightArm, AimZone.LeftLeg };
            int frame = 0;
            const int seconds = 10;
            w.Run(seconds * Second, _ =>
            {
                for (int f = 0; f < 4; f++)
                    alice.Session.Hover(On(bobMob, sweep[frame++ % sweep.Length]), w.Now + f * RealHeadlessWorld.TickSeconds / 4);
            });
            int swept = alice.Session.HoversSent - start;
            Assert.True(1 / PlaySession.ZoneHoverInterval <= ActionLimits.Hover.PerSecond); // the client's zone rate is within the server's refill
            Assert.InRange(swept, seconds * 6, (int)(seconds / PlaySession.ZoneHoverInterval) + 1); // at most 8 a second
            w.Run(Second, _ => alice.Session.Hover(On(bobMob, AimZone.RightLeg), w.Now));
            Assert.Equal(BODY_ZONE_R_LEG, In(w, () => aliceMob.zone_selected));
            Assert.Equal(dropped, s.HoversDropped); // the server's bucket dropped none of them
            Assert.Equal(AimZone.RightLeg, w.Host.Log.Commands.Last(c => c.Kind == CommandKind.Action && c.Action == ActionKind.Hover).AimZone);
        }
    }

    [Fact]
    public void A_hovers_zone_is_recorded_and_replays_to_the_same_hash_and_zone()
    {
        CommandLog log;
        ulong hash;
        long end;
        using (var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 9, TimeoutSteps = 400 }))
        {
            RealHeadlessWorld.Player alice = w.Join("Alice");
            RealHeadlessWorld.Player bob = w.Join("Bob");
            mob_living aliceMob = w.MobOf(alice);
            mob_living bobMob = w.MobOf(bob);
            w.Run(10);
            w.Host.EnqueueConsoleLine("teleport Alice 6 6");
            w.Host.EnqueueConsoleLine("teleport Bob 7 6");
            w.Run(10);
            // a hover without a zone (a turf), then the groin and the right arm of Bob
            Assert.True(alice.Session.Hover(new ClickTarget(TargetKind.Turf, 0, 6, 7, (int)In(w, () => Dm.num(aliceMob.z))), w.Now));
            w.Run(5);
            foreach (AimZone zone in new[] { AimZone.Groin, AimZone.RightArm })
            {
                Assert.True(alice.Session.Hover(On(bobMob, zone), w.Now));
                w.Run(5);
            }
            Assert.Equal(BODY_ZONE_R_ARM, In(w, () => aliceMob.zone_selected));
            w.Host.RequestStop();
            for (int i = 0; i < 10 && !w.Host.Finished; i++)
                w.Run(1);
            Assert.True(w.Host.Finished);
            (hash, end) = (w.Host.FinalHash, w.Host.Tick);
            log = CommandLog.Parse(w.Host.Log.ToText(), out string error);
            Assert.Null(error);
        }
        Assert.Equal(new[] { AimZone.None, AimZone.Groin, AimZone.RightArm },
            log.Commands.Where(c => c.Kind == CommandKind.Action && c.Action == ActionKind.Hover).Select(c => c.AimZone));

        using var replay = new ServerHost(log.ReplayConfig(), replay: log, strictNotTranslated: true);
        SoundLengths.Install(replay.World, RigSoundLengths.Lengths);
        RunResult result = replay.Run(FastForwardPacer.Instance);
        Assert.Equal(end, result.Tick);
        Assert.Equal(hash, result.FinalHash);
        using (replay.World.Enter())
            Assert.Equal(BODY_ZONE_R_ARM, (replay.World.Objects.TryGet(replay.AvatarOf("Alice"), out object o) ? o as mob : null)?.zone_selected);
    }

    // Fix round 1 (F1): the catch-up comes from recorded commands and the world alone (the gun given by a dev command, the click at the mouth, a look
    // at the head during tg's 12 second do-after), so the replay applies it at the same tick: the same hash, the head selected in both.
    [Fact]
    public void A_look_caught_up_after_a_do_after_replays_to_the_same_hash_and_zone()
    {
        CommandLog log;
        ulong hash;
        long end;
        using (var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 9, TimeoutSteps = 9000 }))
        {
            RealHeadlessWorld.Player alice = w.Join("Alice");
            RealHeadlessWorld.Player bob = w.Join("Bob");
            var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
            mob_living bobMob = w.MobOf(bob);
            w.Run(10);
            w.Host.EnqueueConsoleLine("teleport Alice 6 6");
            w.Host.EnqueueConsoleLine("teleport Bob 7 6");
            alice.Session.DevCommand("give Alice /obj/item/gun/energy/laser");
            w.Run(Second);
            Assert.NotNull(In(w, () => aliceMob.get_active_held_item() as obj_item_gun));
            Assert.True(alice.Session.Click(On(bobMob, AimZone.Mouth), ClickButton.Left, ClickModifiers.None));
            w.Run(Second);
            Assert.True(In(w, () => LAZYLEN(aliceMob.do_afters)) > 0);
            Assert.True(alice.Session.Hover(On(bobMob, AimZone.Head), w.Now));
            w.Run(Second);
            Assert.Equal(BODY_ZONE_PRECISE_MOUTH, In(w, () => aliceMob.zone_selected));
            w.Run(12 * Second);
            Assert.Equal(0f, In(w, () => LAZYLEN(aliceMob.do_afters)));
            Assert.Equal(BODY_ZONE_HEAD, In(w, () => aliceMob.zone_selected)); // caught up
            w.Host.RequestStop();
            for (int i = 0; i < 10 && !w.Host.Finished; i++)
                w.Run(1);
            Assert.True(w.Host.Finished);
            (hash, end) = (w.Host.FinalHash, w.Host.Tick);
            log = CommandLog.Parse(w.Host.Log.ToText(), out string error);
            Assert.Null(error);
        }

        using var replay = new ServerHost(log.ReplayConfig(), replay: log, strictNotTranslated: true);
        SoundLengths.Install(replay.World, RigSoundLengths.Lengths);
        RunResult result = replay.Run(FastForwardPacer.Instance);
        Assert.Equal(end, result.Tick);
        Assert.Equal(hash, result.FinalHash);
        using (replay.World.Enter())
            Assert.Equal(BODY_ZONE_HEAD, (replay.World.Objects.TryGet(replay.AvatarOf("Alice"), out object o) ? o as mob : null)?.zone_selected);
    }
}
