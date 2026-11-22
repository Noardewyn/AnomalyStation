using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using Scenario = Anomaly.Server.Tests.Integration.M8ScenarioTests.Scenario;

namespace Anomaly.Server.Tests.Integration;

/// <summary>Records the occupant's cryoxadone ticks: tg's COMSIG_MOB_REAGENT_TICK comes just before the reagent's on_mob_life.</summary>
[DmPath("/datum/test_m8x_cryoxadone_probe")]
public class datum_test_m8x_cryoxadone_probe : datum
{
    public readonly List<(float Temperature, bool KnockedOut, float Ratio, float Seconds, float Brute, float Health)> Ticks = new();

    [SignalHandler]
    public object on_reagent_tick(object source = null, object chem = null, object seconds_per_tick = null, object metabolization_ratio = null)
    {
        if (chem is datum_reagent_medicine_cryoxadone && source is mob_living_carbon who)
            Ticks.Add((who.bodytemperature, HAS_TRAIT(who, TRAIT_KNOCKEDOUT), Dm.num(metabolization_ratio), Dm.num(seconds_per_tick), Dm.num(who.get_brute_loss()), Dm.num(who.health)));
        return null;
    }
}

/// <summary>
/// ROADMAP M8x's done-when (docs/ai/specs/cryo.md section 3; D214; the sleeper part dropped, D216) through the real server on MetaStation
/// with three real clients. Bob is hurt by dev commands (40 brute on the chest, 20 burn on the left arm, no wound) and stays conscious. Alice, in tg's
/// medical doctor outfit (the dev command <c>outfit</c>), in the Cryo room: wrenches the anaesthetic canister onto its port (81,107; Meta
/// maps it unwrenched, so the loop holds no gas before), takes a cryoxadone beaker from the table at 79,111 into the cell at 80,111,
/// opens it with an alt-click and drags Bob in (tg's mouse_drop_receive takes only an incapacitated carbon: he is stunned first); the
/// cell closes and turns on; then she turns the freezer at 79,107 on at its minimum 73.15 K in its window. Every SSair step his body
/// temperature follows tg's conduction formula (cryo.dm:470-477); every metabolism tick cryoxadone heals his brute by its formula at his
/// temperature (medicine_reagents.dm:154-170: none at or above T0C, twice while knocked out); healed, the cell plays cryo_warning.ogg,
/// turns off and ejects him. Alice and Carol see pod-on and the occupant visual and hear the loop; the Cryo window reaches Alice only.
/// A second test runs the treatment twice from a fresh boot and compares the state hashes.
/// </summary>
public class M8xScenarioTests(ITestOutputHelper output)
{
    private const int Second = 20;
    private const ulong Seed = 81;

    private static datum_gas_mixture Air(obj_machinery_cryo_cell cell) => (datum_gas_mixture)cell.internal_connector.gas_connector.airs[1];

    // The cryo room's set-up and the treatment, up to Bob's ejection; returns the cell.
    private static obj_machinery_cryo_cell TreatmentPart(Scenario s)
    {
        RealHeadlessWorld w = s.W;
        M8ScenarioTests.Actor alice = s.Alice, bob = s.Bob, carol = s.Carol;
        ITestOutputHelper output = s.Out;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => alice.P.Session.UiAct(window, action, parameters),
            SendClose = window => alice.P.Session.UiClose(window),
        };

        var cell = s.In(() => Dm.locate<obj_machinery_cryo_cell>(locate(80, 111, 1)));
        var freezer = s.In(() => Dm.locate<obj_machinery_atmospherics_components_unary_thermomachine_freezer>(locate(79, 107, 1)));
        var canister = s.In(() => Dm.locate<obj_machinery_portable_atmospherics_canister_anesthetic_mix>(locate(81, 107, 1)));
        var wrench = s.In(() => Dm.each_cast<atom>(locate(80, 107, 1).contents).OfType<obj_item_wrench>().OrderBy(t => t.dm_id).First());
        var beaker = s.In(() => Dm.each_cast<atom>(locate(79, 111, 1).contents).OfType<obj_item_reagent_containers_cup_beaker_cryoxadone>().OrderBy(b => b.dm_id).First());
        Assert.NotNull(cell);
        Assert.NotNull(freezer);
        Assert.NotNull(canister);

        // --- The doctor and the patient, by dev commands (D214)
        alice.P.Session.DevCommand("outfit Alice /datum/outfit/job/doctor");
        s.Run(1);
        Assert.IsType<obj_item_clothing_under_rank_medical_doctor>(s.In(() => alice.Mob.w_uniform));
        s.Do(() => Waiting.NoSleep(alice.Mob.drop_all_held_items()));
        // hits under tg's WOUND_MINIMUM_DAMAGE (_bodyparts.dm:776): no wound, so no determination healing the limb besides the cryoxadone
        for (int i = 0; i < 10; i++)
        {
            alice.P.Session.DevCommand("damage Bob brute 4 chest");
            s.W.Run(1);
            alice.P.Session.DevCommand("damage Bob burn 2 l_arm");
            s.W.Run(1);
        }
        s.Run(1);
        Assert.Empty(s.Wounds);
        output.WriteLine("hurt: " + s.State());
        Assert.Equal((float)STABLE, s.Stat);
        Assert.True(s.Brute > 0 && s.Burn > 0);
        // into the Cryo room (set-up teleports, as M8's scenario)
        s.Place(bob, s.In(() => locate(81, 110, 1)));
        s.Place(carol, s.In(() => locate(81, 109, 1)));
        s.Place(alice, s.In(() => locate(80, 108, 1)));

        // --- 1. The anaesthetic canister wrenched onto its port (portable_atmospherics.dm:280)
        s.Click(wrench);
        Assert.Same(wrench, s.Held());
        s.Click(canister, seconds: 1);
        Assert.NotNull(s.In(() => canister.connected_port));
        s.Key(KeybindingCode.DropItem);

        // --- 2. A cryoxadone beaker from the table into the cell
        s.Place(alice, s.In(() => locate(80, 110, 1)));
        s.Click(beaker);
        Assert.Same(beaker, s.Held());
        s.Click(cell);
        Assert.Same(beaker, s.In(() => cell.beaker));

        // --- 3. Alt-click opens the cell; Bob, stunned, dragged in: it closes and turns on (close_machine)
        s.Click(cell, modifiers: ClickModifiers.Alt);
        Assert.True(s.In(() => Dm.truthy(cell.state_open)));
        alice.P.Session.DevCommand("stun Bob 20");
        s.Run(0.5f);
        Assert.True(s.In(() => Dm.truthy(bob.Mob.incapacitated)));
        var probe = s.In(() => Dm.@new<datum_test_m8x_cryoxadone_probe>());
        s.Do(() => probe.RegisterSignal(bob.Mob, COMSIG_MOB_REAGENT_TICK, nameof(datum_test_m8x_cryoxadone_probe.on_reagent_tick)));
        Assert.True(alice.P.Session.Drop(new DragRelease(CryoGameplayTests.On(bob.Mob), CryoGameplayTests.On(cell), ClickButton.Left, ClickModifiers.None, 4)));
        s.Run(0.25f);
        Assert.Same(cell, s.In(() => bob.Mob.loc));
        Assert.False(s.In(() => Dm.truthy(cell.state_open)));
        Assert.True(s.In(() => Dm.truthy(cell.on)));
        s.Run(1);

        // the watchers: pod-on, the occupant visual holding Bob's body, the loop heard
        foreach (M8ScenarioTests.Actor who in new[] { alice, carol })
        {
            ReplicaWorld replica = who.P.Session.Replica;
            Assert.Equal("pod-on", CryoGameplayTests.StateOf(replica, cell.dm_id));
            ReplicaEntity visual = CryoGameplayTests.OccupantVisual(replica, cell.dm_id);
            Assert.NotNull(visual);
            Assert.Contains(CryoGameplayTests.States(replica, visual.State.Appearance), st => st != null && st.Contains("chest", StringComparison.Ordinal));
            Assert.Contains(who.Heard, h => (h.File ?? "").StartsWith("sound/machines/cryo/cryo_", StringComparison.Ordinal));
        }

        // --- 4. The Cryo window: Alice's session only (tgui is per session, D214)
        s.Click(cell);
        UiWindowState cryo = Assert.Single(alice.P.Session.Replica.UiWindows.Values, v => v.Interface == "Cryo");
        UiNode content = CryoGameplayTests.Shown(windows, alice.P.Session.Replica, cryo.Window);
        Assert.Equal(s.In(() => bob.Mob.name), CryoGameplayTests.ByKey(content, "name").Text);
        Assert.Contains("Cryoxadone", content.AllText());
        foreach (M8ScenarioTests.Actor other in new[] { bob, carol })
            Assert.DoesNotContain(other.P.Session.Replica.UiWindows.Values, v => v.Interface == "Cryo");
        alice.P.Session.UiClose(cryo.Window);

        // --- 5. The freezer on at its minimum, in its window. Turned on with the patient in (with it on before, the loop is below T0C
        // by the time he is in): his first cryoxadone ticks are at room temperature and heal nothing
        s.Run(6);
        s.Place(alice, s.In(() => locate(80, 108, 1)));
        s.Click(freezer);
        UiWindowState thermo = Assert.Single(alice.P.Session.Replica.UiWindows.Tgui());
        Assert.Equal("ThermoMachine", thermo.Interface);
        CryoGameplayTests.ByKey(CryoGameplayTests.Shown(windows, alice.P.Session.Replica, thermo.Window), "power").OnClick();
        s.Run(0.25f);
        CryoGameplayTests.ByKey(CryoGameplayTests.Shown(windows, alice.P.Session.Replica, thermo.Window), "min").OnClick();
        s.Run(0.25f);
        Assert.Equal(1f, s.In(() => freezer.on));
        Assert.Equal(73.15f, s.In(() => Dm.num(freezer.target_temperature)), 2);
        alice.P.Session.UiClose(thermo.Window);
        s.Run(0.25f);

        // --- 6. The treatment, tick by tick: every SSair step that moves his temperature is tg's conduction (cryo.dm:470-477), read back
        // from the step's result: heat = (T1 - T0) * C, the gas before it Tg' + heat / A, so T1 - T0 = f * (Tg - T0) * A / (C + A). A step
        // whose gas also gained or lost moles (his breath, after the cell's step in the tick) is skipped.
        (float Body, float Gas, float Capacity, float Moles) Sample() => s.In(() =>
        {
            datum_gas_mixture air = Air(cell);
            return (bob.Mob.bodytemperature, air.temperature, Dm.num(air.heat_capacity()), Dm.num(air.total_moles()));
        });
        var last = Sample();
        int conductionSteps = 0, ticks = 0;
        float worst = 0;
        for (; ticks < 300 * Second && s.In(() => bob.Mob.loc == cell); ticks++)
        {
            w.Run(1);
            var now = Sample();
            if (now.Body != last.Body && MathF.Abs(now.Moles - last.Moles) <= 1e-4f * last.Moles && s.In(() => bob.Mob.loc == cell))
            {
                float c = s.In(() => cell.heat_capacity);
                float heat = (now.Body - last.Body) * c;
                float gasBefore = now.Gas + heat / now.Capacity;
                float f = (1 - s.In(() => Dm.num(bob.Mob.get_cold_protection(gasBefore)))) * 0.1f + s.In(() => cell.conduction_coefficient);
                float expected = f * CALCULATE_CONDUCTION_ENERGY(gasBefore - last.Body, c, now.Capacity) / c;
                worst = MathF.Max(worst, MathF.Abs(expected - (now.Body - last.Body)));
                Assert.True(MathF.Abs(expected - (now.Body - last.Body)) <= 1e-3f,
                    $"tick {ticks}: body {last.Body} -> {now.Body} K, gas before {gasBefore} K (capacity {now.Capacity}): expected a change of {expected} K");
                conductionSteps++;
            }
            last = now;
            if (ticks % (10 * Second) == 0)
                output.WriteLine($"{ticks / Second} s: body {now.Body:0.0} K, gas {now.Gas:0.0} K, {now.Moles:0.###} mol, knocked out {s.In(() => HAS_TRAIT(bob.Mob, TRAIT_KNOCKEDOUT))}; " + s.State());
        }
        output.WriteLine($"ejected after {ticks / Second} s; {conductionSteps} conduction steps checked (worst {worst:0.######} K)");
        s.Measured["cryo"] = $"{ticks / Second} s in the cell, {conductionSteps} conduction steps";
        Assert.True(conductionSteps >= 20, $"only {conductionSteps} conduction steps checked");
        Assert.True(s.In(() => bob.Mob.loc is turf), "the healed patient is ejected (autoeject)");
        s.Run(0.5f); // the tick's sounds reach the clients
        Assert.Contains(alice.Heard, h => h.File == "sound/machines/cryo_warning.ogg");
        Assert.False(s.In(() => Dm.truthy(cell.on)));

        // --- 7. Cryoxadone, one metabolism tick to the next: brute only (the cold breath burns, lungs.dm:801) and while his health is
        // at most 80% (the N2O's sleep heals minor damage above it, debuffs.dm:314); each limb rounds its damage to DAMAGE_PRECISION
        int heals = 0, frozenOut = 0, doubled = 0;
        var t = probe.Ticks;
        foreach (var tick in t)
            output.WriteLine($"cryoxadone tick: {tick}");
        for (int i = 0; i + 1 < t.Count; i++)
        {
            float healed = t[i].Brute - t[i + 1].Brute;
            if (t[i].Temperature >= T0C)
            {
                Assert.Equal(0f, healed);
                frozenOut++;
                continue;
            }
            if (t[i + 1].Brute <= 0 || t[i + 1].Health > 0.8f * s.In(() => Dm.num(bob.Mob.maxHealth)))
                break; // the last heal is clamped by what was left; the sleep's own healing
            float power = (-0.00003f * t[i].Temperature * t[i].Temperature + 3) * (t[i].KnockedOut ? 2 : 1);
            float expected = 0.5f * power * t[i].Ratio * t[i].Seconds;
            Assert.True(MathF.Abs(expected - healed) <= DAMAGE_PRECISION / 2 + 1e-3f, $"metabolism tick {i} at {t[i].Temperature} K: healed {healed}, expected {expected}");
            heals++;
            if (t[i].KnockedOut)
                doubled++;
        }
        output.WriteLine($"cryoxadone: {t.Count} ticks, {frozenOut} at or above T0C (no healing), {heals} healing checked, {doubled} of them knocked out");
        Assert.True(frozenOut >= 1, "cryoxadone in his blood above T0C");
        Assert.True(heals >= 3, $"only {heals} healing ticks checked");
        Assert.True(doubled >= 1, "the N2O knocks him out: twice the power");
        Assert.Equal(0f, s.Brute);
        return cell;
    }

    [MetaServerFact]
    public void A_doctor_cools_a_hurt_man_in_the_cryo_cell_which_heals_him_by_tgs_formulas_and_ejects_him()
    {
        Scenario s = M8ScenarioTests.Start(output, Seed);
        using (s.W)
        {
            obj_machinery_cryo_cell cell = TreatmentPart(s);
            s.Run(1);
            Assert.Equal("pod-open", CryoGameplayTests.StateOf(s.Alice.P.Session.Replica, cell.dm_id));
            output.WriteLine("after: " + s.State());
            M8ScenarioTests.AssertClean(s);
        }
    }

    [MetaServerFact]
    public void The_cryo_treatment_run_twice_from_a_fresh_boot_ends_with_the_same_state_hash()
    {
        (ulong Hash, long Tick) Once()
        {
            Scenario s = M8ScenarioTests.Start(output, Seed);
            using (s.W)
            {
                TreatmentPart(s);
                return (s.W.World.ComputeStateHash(), s.W.Ticks);
            }
        }

        var first = Once();
        var second = Once();
        output.WriteLine($"first run: {first.Hash:x16} at tick {first.Tick}; second: {second.Hash:x16} at tick {second.Tick}");
        Assert.Equal(first.Tick, second.Tick);
        Assert.Equal(first.Hash, second.Hash);
    }
}
