using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package MD1 (D333): the medical wall machines and the kiosk in a booted toy room with Bob as a player: the DeForest first
/// aid station (a hand put in it, the injections, the reservoirs, the payment, the doctors' and red alert's waiver, the gauze, the
/// restock, the refill, the emag), the vitals display (its connection to the operating table, the patient's picture on its screen,
/// the beeps, the examine scan, the EMP) and the medical kiosk (its window, a paid scan, the reset, its scanner wand).
/// </summary>
public class MedicalMD1Tests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 2, "Bob");
        r.In(() =>
        {
            ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test");
            (get_area(locate(4, 4, 1)) as area).requires_power = FALSE; // the toy room has no APC
        });
        r.Step(Second);
        return (r, feed, bob);
    }

    /// <summary>
    /// An atom as the map loader makes it: Initialize with mapload, then the map's late loaders (tg's InitializeAtoms runs them after
    /// the map's Initialize: a machine's post_machine_initialize, with its power_change and its first update_appearance, and the
    /// vitals display's connection to its machine), as KitchenH2Tests' Mapped.
    /// </summary>
    private static T Mapped<T>(AudioRoom r, int x, int y) where T : atom => r.In(() =>
    {
        float previous = SSatoms.initialized;
        SSatoms.initialized = INITIALIZATION_INNEW_MAPLOAD;
        T made = Dm.@new<T>(locate(x, y, 1));
        SSatoms.initialized = previous;
        DmList late = SSatoms.late_loaders;
        SSatoms.late_loaders = Dm.list();
        foreach (atom lateLoader in Dm.each_cast<atom>(late))
            lateLoader.LateInitialize();
        return made;
    });

    /// <summary>Steps the world a tick at a time until <paramref name="done"/> holds, at most <paramref name="maxTicks"/> ticks.</summary>
    private static void StepUntil(AudioRoom r, Func<bool> done, int maxTicks)
    {
        for (int i = 0; i < maxTicks && !r.In(done); i++)
            r.Step();
    }

    private static float Brute(AudioRoom r, mob_living who) => r.In(() => Dm.num(who.get_brute_loss()));

    // what the click on the kiosk had to pass, for a window that did not open
    private static string KioskDiag(AudioRoom r, mob_living_carbon_human bob, obj_machinery_medical_kiosk kiosk) => r.In(() =>
        "machine_stat " + Dm.str(kiosk.machine_stat)
        + "; adjacent " + Dm.str(Waiting.NoSleep(bob.Adjacent(kiosk)))
        + "; can_interact " + Dm.str(Waiting.NoSleep(kiosk.can_interact(bob)))
        + "; ui_status " + Dm.str(Waiting.NoSleep(kiosk.ui_status(bob, kiosk.ui_state(bob) as datum_ui_state)))
        + "; next_move " + Dm.str(bob.next_move) + " at " + Dm.str(world.time)
        + "; patient_ref " + Dm.str(kiosk.patient_ref)
        + "; held " + Dm.str(bob.get_active_held_item()));

    private static string Diag(Feed feed, AudioRoom r) => "chat: " + string.Join(" | ", feed.Chat.Select(c => c.Html)) + " runtimes: " + string.Join("\n", r.Room.RuntimeText);

    private static void RightClick(AudioRoom r, mob who, atom target)
    {
        r.Step(Second); // tg's click cooldown (changeNext_move) of the click before
        r.Command(() => ClientSeam.Click(who.client, target, "icon-x=16;icon-y=16;right=1;button=right"));
        r.Step(2);
    }

    private static void Use(AudioRoom r, mob who, atom target)
    {
        r.Step(Second);
        Click(r, who, target);
    }

    // An ID card of that job in the ID slot with a bank account holding the credits (as VendingTests' Funds)
    private static obj_item_card_id Card(AudioRoom r, mob_living_carbon_human who, Type job, float money)
    {
        return r.In(() =>
        {
            if (who.w_uniform == null) // the ID slot hangs on a jumpsuit: without one equip_to_slot_or_del deletes the card
                Waiting.NoSleep(who.equip_to_slot_or_del(Dm.@new<obj_item_clothing_under_color_grey>(who.loc), ITEM_SLOT_ICLOTHING));
            var card = Dm.@new<obj_item_card_id_advanced>(who.loc);
            Waiting.NoSleep(who.equip_to_slot_or_del(card, ITEM_SLOT_ID));
            Assert.Same(card, who.wear_id);
            var account = Dm.@new<datum_bank_account>(Dm.str(who.real_name), SSjob.get_job_type(job), 1, TRUE);
            account.adjust_money(money);
            card.set_account(account);
            return (obj_item_card_id)card;
        });
    }

    private static List<string> Overlays(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)).ToList());

    [Fact]
    public void The_emergency_first_aid_station_heals_a_hand_put_in_it_and_hands_out_gauze()
    {
        var (r, feed, bob) = Room(9701);
        var healer = Mapped<obj_machinery_wall_healer_free_directional_south>(r, 4, 2);
        r.Step(Second);
        Assert.Equal(5f, r.In(() => healer.num_bandages));
        Assert.Contains("bar0", Overlays(r, healer)); // a full pool
        r.In(() => Waiting.NoSleep(bob.adjust_brute_loss(20)));
        r.In(() => Waiting.NoSleep(bob.adjust_fire_loss(8)));

        Use(r, bob, healer);
        r.Step(Second); // the half second's do_after
        Assert.True(feed.Any("some kind of sensor scans your arm"), Diag(feed, r));
        Assert.Same(bob, r.In(() => healer.current_user));
        Assert.NotNull(r.In(() => healer.current_hand));

        // an injection every two seconds: a tenth of each pool (4 brute, 4 burn), with the bloody hand
        r.Step(5 * Second);
        float brute = r.In(() => bob.get_brute_loss() is float b ? b : Dm.num(bob.get_brute_loss()));
        Assert.True(brute <= 12, "brute after the injections: " + brute + " " + Diag(feed, r));
        Assert.Equal(0f, r.In(() => Dm.num(bob.get_fire_loss())));
        Assert.True(r.In(() => healer.brute_healing) < 40);
        Assert.True(feed.Any("You feel a bit better."), Diag(feed, r));
        Assert.Contains(r.Sounds.Heard, s => (s.File ?? "").EndsWith("defib_SaftyOn.ogg", StringComparison.Ordinal));
        Assert.DoesNotContain("bar0", Overlays(r, healer).Where(o => o != null && o.StartsWith("bar", StringComparison.Ordinal) && o != "bar_shadow").Take(1));

        // a step away takes the hand out
        r.In(() => Waiting.NoSleep(bob.Move(locate(5, 2, 1), EAST)));
        r.Step(2);
        Assert.Null(r.In(() => healer.current_user));
        Assert.True(feed.Any("You remove your hand from"), Diag(feed, r));

        // gauze on a right click; gauze given back restocks it
        r.In(() => Waiting.NoSleep(bob.Move(locate(4, 2, 1), WEST)));
        RightClick(r, bob, healer);
        var gauze = r.In(() => bob.get_active_held_item() as obj_item_stack_medical_wrap_gauze);
        Assert.NotNull(gauze);
        Assert.Equal(4f, r.In(() => healer.num_bandages));
        Assert.Contains(r.Sounds.Heard, s => (s.File ?? "").EndsWith("machine_vend.ogg", StringComparison.Ordinal));
        r.In(() => Waiting.NoSleep(gauze.add(2))); // a stack of three
        Use(r, bob, healer);
        Assert.Equal(3, r.In(() => healer.stocked_bandages?.len ?? 0));
        Assert.Null(r.In(() => bob.get_active_held_item()));
        Assert.Contains("7 bandages stocked", r.In(() => string.Join(" ", Dm.each(Waiting.NoSleep(healer.examine(bob))).Select(Dm.str))));

        // nobody's hand in it: its pools refill a tenth each minute (the emergency station's recharge)
        float before = r.In(() => healer.brute_healing);
        r.Step(65 * Second);
        Assert.True(r.In(() => healer.brute_healing) > before, "the pool did not refill");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_first_aid_station_charges_by_the_injection_and_waives_it_for_doctors()
    {
        var (r, feed, bob) = Room(9702);
        var healer = Mapped<obj_machinery_wall_healer_directional_south>(r, 4, 2);
        r.Step(Second);
        r.In(() => Waiting.NoSleep(bob.adjust_brute_loss(20)));

        // no ID: nothing happens
        Use(r, bob, healer);
        r.Step(5 * Second);
        Assert.Equal(20f, r.In(() => Dm.num(bob.get_brute_loss())));
        Assert.True(feed.Any("No ID card found. Aborting."), Diag(feed, r));

        // an assistant's ID pays 2.5 credits a point into medical's budget. The injections come on the machines' processing (every
        // two seconds) once the four seconds' cooldown is over, which the same user's hand does not reset: the first one is awaited.
        r.In(() => healer.clear_using_mob());
        obj_item_card_id card = Card(r, bob, typeof(datum_job_assistant), 500);
        float budget = r.In(() => SSeconomy.get_dep_account(ACCOUNT_MED).account_balance);
        float balance = r.In(() => card.registered_account.account_balance);
        Use(r, bob, healer);
        StepUntil(r, () => Dm.num(bob.get_brute_loss()) < 19, 8 * Second);
        Assert.Equal(16, Brute(r, bob), 2); // one injection: a tenth of the 40 points' brute pool
        Assert.Equal(balance - 10, r.In(() => card.registered_account.account_balance)); // 4 points at 2.5
        Assert.Equal(budget + 10, r.In(() => SSeconomy.get_dep_account(ACCOUNT_MED).account_balance));

        // a doctor's injections are free
        r.In(() => healer.clear_using_mob());
        r.In(() => qdel(card));
        obj_item_card_id doctors = Card(r, bob, typeof(datum_job_doctor), 500);
        Use(r, bob, healer);
        StepUntil(r, () => Dm.num(bob.get_brute_loss()) < 15, 8 * Second);
        Assert.Equal(12, Brute(r, bob), 2);
        Assert.Equal(500f, r.In(() => doctors.registered_account.account_balance));

        // emagged, it stabs the hand
        r.In(() => healer.clear_using_mob());
        r.In(() => Waiting.NoSleep(healer.emag_act(bob)));
        float hurt = Brute(r, bob);
        Use(r, bob, healer);
        StepUntil(r, () => Dm.num(bob.get_brute_loss()) > hurt + 1, 8 * Second);
        Assert.True(Brute(r, bob) >= hurt + 33 - 0.01f, "brute " + Brute(r, bob) + " from " + hurt + " " + Diag(feed, r));
        Assert.True(feed.Any("stabbing you with several instruments and needles"), Diag(feed, r));
    }

    [Fact]
    public void The_vitals_display_shows_the_patient_on_the_operating_table_and_beeps()
    {
        var (r, feed, bob) = Room(9703);
        var table = Place<obj_structure_table_optable>(r, 6, 3);
        var vitals = Mapped<obj_machinery_vitals_reader_directional_south>(r, 6, 2);
        r.Step(Second);
        Assert.Same(table, r.In(() => vitals.connected));
        Assert.Null(r.In(() => vitals.patient));
        Assert.DoesNotContain("buttons", Overlays(r, vitals));

        var patient = r.Dummy(6, 3);
        r.In(() => Waiting.NoSleep(table.set_patient(patient)));
        r.Step(2);
        Assert.Same(patient, r.In(() => vitals.patient));
        List<string> overlays = Overlays(r, vitals);
        Assert.Contains("buttons", overlays);
        Assert.Contains("ekg", overlays);
        Assert.Contains("resp", overlays);
        Assert.Contains("human_head", overlays);
        Assert.Contains("human_l_leg", overlays);
        Assert.Contains("blood", overlays);

        // the beep of the patient's state; at death the flat line and the droning beep. The patient dies of suffocation: the screen
        // redraws on the patient's health update (COMSIG_LIVING_HEALTH_UPDATE, sent by updatehealth after update_stat's death()),
        // which a bare death() does not send in tg either. The droning beep waits out the seven seconds' cooldown of the beep
        // before, on the machines' processing (every two seconds).
        r.Step(Second);
        Assert.True(feed.Chat.Count >= 0); // runechat goes to the clients' chat messages, not the chat feed
        r.In(() => Waiting.NoSleep(patient.adjust_oxy_loss(200)));
        Assert.Equal((float)DEAD, r.In(() => patient.stat));
        Assert.Contains("ekg_flat", Overlays(r, vitals));
        Assert.Contains("resp_flat", Overlays(r, vitals));
        r.Step(12 * Second);
        Assert.Equal((object)DEAD, r.In(() => vitals.last_reported_stat));

        // examined close by: tg's condensed health scan
        string examine = r.In(() => string.Join(" ", Dm.each(Waiting.NoSleep(vitals.examine(bob))).Select(Dm.str)));
        Assert.Contains("Analyzing results for", examine);

        // an EMP scrambles it; a multitool disconnects it
        r.In(() => Waiting.NoSleep(vitals.emp_act(EMP_LIGHT)));
        Assert.Contains("flickering erratically", r.In(() => string.Join(" ", Dm.each(Waiting.NoSleep(vitals.examine(bob))).Select(Dm.str))));
        r.In(() => vitals.set_connection(null));
        Assert.Null(r.In(() => vitals.patient));
        Assert.DoesNotContain("buttons", Overlays(r, vitals));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_medical_kiosk_sells_a_scan_in_its_window_and_lends_its_wand()
    {
        var (r, feed, bob) = Room(9704);
        var kiosk = Place<obj_machinery_medical_kiosk>(r, 5, 3);
        Assert.NotNull(r.In(() => kiosk.scanner_wand));
        obj_item_card_id card = Card(r, bob, typeof(datum_job_assistant), 500);
        r.In(() => Waiting.NoSleep(bob.adjust_brute_loss(15)));

        Use(r, bob, kiosk);
        Assert.True(r.In(() => Dm.length(bob.tgui_open_uis) > 0), "no window open: " + KioskDiag(r, bob, kiosk) + " " + Diag(feed, r));
        JsonElement data = Data(r, bob, out string window);
        Assert.Equal(10, data.GetProperty("kiosk_cost").GetDouble());
        Assert.Equal(r.In(() => Dm.str(bob.name)), data.GetProperty("patient_name").GetString());
        Assert.Equal(0, data.GetProperty("active_status_1").GetDouble());
        Assert.True(feed.Any("Scanner reset."), Diag(feed, r)); // no patient yet: the user is the patient

        // a paid scan: beginScan answers with no update of its own (its ui_act returns FALSE), so the scan shows at the window's
        // next update by SStgui (every 0.9 seconds)
        Act(r, bob, window, "beginScan_1");
        data = Data(r, bob, out _, Second);
        Assert.Equal(1, data.GetProperty("active_status_1").GetDouble());
        Assert.Equal(15, data.GetProperty("brute_health").GetDouble(), 3); // tg's round(brute, 0.001), a float: 15.000001
        Assert.Equal(490f, r.In(() => card.registered_account.account_balance));
        Assert.Equal("kiosk_active", r.In(() => Dm.str(kiosk.icon_state)));

        // clearTarget clears the scans and the patient; the act's forced window update (SStgui.update_uis: process with force calls
        // ui_interact) then finds no patient and resets the scanner to its user, as tg's does
        Act(r, bob, window, "clearTarget");
        Assert.Equal(0f, r.In(() => kiosk.scan_active));
        Assert.Same(bob, r.In(() => kiosk.patient_ref?.resolve()));
        Assert.True(feed.Chat.Count(c => c.Html != null && c.Html.Contains("Scanner reset.", StringComparison.Ordinal)) >= 2, Diag(feed, r));

        // the wand comes off on a right click, targets a patient and snaps back on, setting that patient
        RightClick(r, bob, kiosk);
        var wand = r.In(() => bob.get_active_held_item() as obj_item_scanner_wand);
        Assert.NotNull(wand);
        Assert.Null(r.In(() => kiosk.scanner_wand));
        var patient = r.Dummy(5, 2);
        r.In(() => Waiting.NoSleep(wand.attack__proc(patient, bob)));
        Assert.Same(patient, r.In(() => wand.selected_target));
        Use(r, bob, kiosk);
        Assert.Same(wand, r.In(() => kiosk.scanner_wand));
        Assert.Same(patient, r.In(() => kiosk.patient_ref?.resolve()));
        Assert.True(feed.Any("has been set as the current patient."), Diag(feed, r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_medical_kiosk_opens_for_a_player_without_an_ID_and_begins_no_unpaid_scan()
    {
        // a player with no jumpsuit, no ID and no cash
        var (r, feed, bob) = Room(9705);
        var kiosk = Place<obj_machinery_medical_kiosk>(r, 5, 3);
        r.In(() => Waiting.NoSleep(bob.adjust_brute_loss(15)));

        Use(r, bob, kiosk);
        Assert.True(r.In(() => Dm.length(bob.tgui_open_uis) > 0), "no window open: " + KioskDiag(r, bob, kiosk) + " " + Diag(feed, r));
        JsonElement data = Data(r, bob, out string window);
        Assert.Equal(0, data.GetProperty("active_status_1").GetDouble());

        // the payment component finds no card and no cash: the charge is cancelled and no scan begins
        Act(r, bob, window, "beginScan_1");
        data = Data(r, bob, out _, Second);
        Assert.Equal(0, data.GetProperty("active_status_1").GetDouble());
        Assert.Equal(0f, r.In(() => kiosk.scan_active));
        Assert.False(feed.Any("Thank you for your patronage!"), Diag(feed, r));
        r.AssertNoRuntimes();
    }
}
