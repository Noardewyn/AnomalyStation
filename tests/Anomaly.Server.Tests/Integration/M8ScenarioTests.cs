using System.Text.RegularExpressions;
using Anomaly.Client.Shared;
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

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// ROADMAP M8's demo check (docs/ai/specs/medicine.md, Closing) through the real server on MetaStation with real clients: Bob is given
/// by dev commands a severe slash on the left arm (bleeding), a moderate burn wound and 60 burn on the chest (tg's <c>apply_damage</c> rolls
/// the wound deeper: third degree), losebreath 20 and 60 oxygen damage (suffocation) and a stopped heart (<c>heart Bob stop</c>), which puts
/// him in hard crit. Alice, in tg's paramedic
/// outfit (the dev command <c>outfit</c>) and alone, uses only MetaStation's things taken in medbay: she scans him with a health
/// analyzer (only her client gets the numbers, D40), restarts his heart with a loaded defibrillator hooked into medbay's wall mount,
/// gives him CPR, the regular medkit's epinephrine medipen, its gauze then its suture on the arm, its mesh on the chest, and the
/// oxygen kit's salbutamol medipen and a convermol syringe. He ends conscious, with no wound bleeding, and breathes normally for
/// 60 seconds (losebreath 0, his oxygen damage not rising). A second test runs the treatment twice from a fresh boot and compares
/// the state hashes.
/// </summary>
public class M8ScenarioTests(ITestOutputHelper output)
{
    private const int Second = 20;
    private const ulong Seed = 88;

    internal sealed class Actor
    {
        public RealHeadlessWorld.Player P;
        public mob_living_carbon_human Mob;
        public readonly List<string> Texts = new();
        public readonly List<SoundEvent> Heard = new();
    }

    internal sealed class Scenario
    {
        public RealHeadlessWorld W;
        public Actor Alice, Bob, Carol;
        public obj_item_defibrillator Defib;
        public obj_machinery_defibrillator_mount Mount;
        public turf Station, Patient;
        public int BootRuntimes;
        public readonly List<string> Runtimes = new();
        public ITestOutputHelper Out;
        public readonly Dictionary<string, string> Measured = new();

        public T In<T>(Func<T> f)
        {
            using (W.World.Enter())
                return f();
        }

        public void Do(Action a)
        {
            using (W.World.Enter())
                a();
        }

        public void Run(float seconds) => W.Run((int)(seconds * Second));

        public Actor Join(string name)
        {
            var a = new Actor { P = W.Join(name) };
            a.Mob = (mob_living_carbon_human)W.MobOf(a.P);
            a.P.Client.ServerTextReceived += t => a.Texts.Add(t.Html);
            a.P.Session.Replica.SoundsReceived += events => a.Heard.AddRange(events);
            return a;
        }

        public void Click(atom a, AimZone zone = AimZone.None, ClickButton button = ClickButton.Left, float seconds = 0.5f, ClickModifiers modifiers = ClickModifiers.None)
        {
            Do(() => Alice.Mob.next_click = -1);
            Assert.True(Alice.P.Session.Click(new ClickTarget(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16, zone), button, modifiers));
            Run(seconds);
        }

        public void ClickScreen(atom a, float seconds = 0.5f)
        {
            Assert.True(Alice.P.Session.Click(new ClickTarget(TargetKind.Screen, a.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
            Run(seconds);
        }

        public void Key(KeybindingCode code, float seconds = 0.3f)
        {
            Alice.P.Session.Keybinding(code);
            Run(seconds);
        }

        public obj_item Held() => In(() => Alice.Mob.get_active_held_item() as obj_item);

        // A teleport (a set-up step: walking through medbay would only add ticks).
        public void Place(Actor a, turf t)
        {
            Do(() => Waiting.NoSleep(a.Mob.forceMove(t)));
            Run(0.3f);
        }

        public float Health => In(() => Dm.num(Bob.Mob.health));
        public float Oxy => In(() => Dm.num(Bob.Mob.get_oxy_loss()));
        public float Burn => In(() => Dm.num(Bob.Mob.get_fire_loss()));
        public float Brute => In(() => Dm.num(Bob.Mob.get_brute_loss()));
        public float Stat => In(() => Dm.num(Bob.Mob.stat));
        public List<datum_wound> Wounds => In(() => Dm.each<datum_wound>(Bob.Mob.all_wounds).ToList());
        public float Flow(datum_wound w) => In(() => Dm.num(w.blood_flow));

        public string State() => In(() => $"health {Dm.num(Bob.Mob.health):0.#}, stat {Dm.num(Bob.Mob.stat)}, brute {Dm.num(Bob.Mob.get_brute_loss()):0.#}, burn {Dm.num(Bob.Mob.get_fire_loss()):0.#}, oxy {Dm.num(Bob.Mob.get_oxy_loss()):0.#}, losebreath {Bob.Mob.losebreath:0.#}, "
            + $"heart {(Dm.truthy(Bob.Mob.undergoing_cardiac_arrest()) ? "stopped" : "beating")}, blood {Dm.num(Bob.Mob.blood_volume):0}, wounds [{string.Join(", ", Dm.each<datum_wound>(Bob.Mob.all_wounds).Select(w => $"{Dm.str(w.name)} {Dm.num(w.blood_flow):0.##}"))}], "
            + $"reagents [{string.Join(", ", Dm.each<datum_reagent>(((datum_reagents)Bob.Mob.reagents).reagent_list).Select(r => $"{Dm.str(r.name)} {Dm.num(r.volume):0.#}"))}]");
    }

    internal static bool Clear(turf t) => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

    // The first thing of exactly the type (by id) lying in a medical area of the station.
    private static T InMedbay<T>() where T : atom_movable
    {
        for (int x = 1; x <= Dm.num(world.maxx); x++)
            for (int y = 1; y <= Dm.num(world.maxy); y++)
            {
                turf t = locate(x, y, 1);
                if (t == null || get_area(t) is not area_station_medical)
                    continue;
                T found = Dm.each_cast<atom>(t.contents).OfType<T>().Where(a => a.GetType() == typeof(T)).OrderBy(a => a.dm_id).FirstOrDefault();
                if (found != null)
                    return found;
            }
        return null;
    }

    private static turf Beside(atom a, turf not = null, atom_movable mover = null)
        => new[] { NORTH, SOUTH, EAST, WEST, NORTHEAST, NORTHWEST, SOUTHEAST, SOUTHWEST }.Select(d => get_step(a, d) as turf)
            .FirstOrDefault(t => t != null && t != not && Clear(t) && Dm.truthy(Waiting.NoSleep(t.Adjacent(a, a, mover)))); // reachable from there (tg's Adjacent with the mover): not behind a window

    // MetaStation booted with the seed, Alice, Bob and Carol joined; runtimes after the boot are collected.
    internal static Scenario Start(ITestOutputHelper output, ulong seed)
    {
        var s = new Scenario { Out = output };
        s.W = new RealHeadlessWorld(new LoopbackOptions { Seed = seed, TimeoutSteps = 60000 }, MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        s.Alice = s.Join("Alice");
        s.Bob = s.Join("Bob");
        s.Carol = s.Join("Carol");
        s.Run(1);
        s.BootRuntimes = s.W.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        Action<Exception> previous = s.W.World.RuntimeErrorSink;
        s.W.World.RuntimeErrorSink = e => { s.Runtimes.Add(e.ToString()); previous?.Invoke(e); };
        return s;
    }

    private static Scenario Boot(ITestOutputHelper output)
    {
        Scenario s = Start(output, Seed);

        // medbay's first loaded defibrillator and the wall mount nearest to it with a clear tile beside it for the patient
        s.Do(() =>
        {
            s.Defib = InMedbay<obj_item_defibrillator_loaded>();
            Assert.NotNull(s.Defib);
            foreach (obj_machinery_defibrillator_mount mount in Dm.each<obj_machinery_defibrillator_mount>(Dm.world).OrderBy(m => Dm.num(get_dist(s.Defib, m))).ThenBy(m => m.dm_id))
            {
                turf at = (turf)mount.loc;
                turf patient = new[] { NORTH, SOUTH, EAST, WEST }.Select(d => get_step(at, d) as turf).FirstOrDefault(t => t != null && Clear(t));
                if (patient != null && Clear(at))
                {
                    (s.Mount, s.Station, s.Patient) = (mount, at, patient);
                    break;
                }
            }
        });
        Assert.NotNull(s.Mount);
        output.WriteLine(s.In(() => $"the defib at {s.Defib.x},{s.Defib.y} in {Dm.str(((area)get_area(s.Defib)).name)}; the mount at {s.Mount.x},{s.Mount.y} in {Dm.str(((area)get_area(s.Mount)).name)}; Bob on {s.Patient.x},{s.Patient.y}"));
        return s;
    }

    // Alice gets her paramedic's outfit and fetches medbay's things to the mount: the defibrillator hooked into it, the two medkits
    // dropped at her feet, the health analyzer kept in hand. Carol stands by in the room.
    private static void Prepare(Scenario s)
    {
        Actor alice = s.Alice;
        alice.P.Session.DevCommand("outfit Alice /datum/outfit/job/paramedic");
        s.Run(1);
        Assert.IsType<obj_item_storage_belt_medical_paramedic>(s.In(() => alice.Mob.belt));
        Assert.IsType<obj_item_clothing_under_rank_medical_paramedic>(s.In(() => alice.Mob.w_uniform));
        s.Do(() => Waiting.NoSleep(alice.Mob.drop_all_held_items()));
        s.Place(s.Bob, s.Patient);
        s.Place(s.Carol, s.In(() => Beside(s.Patient, s.Station)));

        void Fetch(atom thing)
        {
            turf stand = s.In(() => Beside(thing, mover: alice.Mob));
            if (stand == null)
            {
                // behind a counter (medbay storage's tables): in front of one of the counter's windoors, which her ID opens with a click
                bool reached = false;
                foreach (obj_machinery_door_window windoor in s.In(() => Dm.each<obj_machinery_door_window>(range(2, thing)).Where(d => Clear(get_step(d, d.dir) as turf)).OrderBy(d => d.dm_id).ToList()))
                {
                    s.Place(alice, s.In(() => get_step(windoor, windoor.dir) as turf));
                    s.Click(windoor, seconds: 1.5f);
                    Assert.False(s.In(() => Dm.truthy(windoor.density)), "the windoor opens for the paramedic");
                    if (reached = s.In(() => Dm.truthy(Waiting.NoSleep(alice.Mob.Adjacent(thing)))))
                        break;
                }
                Assert.True(reached, "she reaches over the counter");
            }
            else
                s.Place(alice, stand);
            s.Run(1);
            s.Click(thing);
            Assert.Same(thing, s.Held());
            s.Place(alice, s.Station);
        }

        Fetch(s.Defib);
        s.Click(s.Mount);
        Assert.Same(s.Mount, s.In(() => s.Defib.loc));
        foreach (obj_item_storage_medkit kit in new obj_item_storage_medkit[] { s.In(InMedbay<obj_item_storage_medkit_regular>), s.In(InMedbay<obj_item_storage_medkit_o2>) })
        {
            Assert.NotNull(kit);
            s.Out.WriteLine(s.In(() => $"{Dm.str(kit.name)} at {kit.x},{kit.y} in {Dm.str(((area)get_area(kit)).name)}: {string.Join(", ", Dm.each_cast<atom>(kit.contents).Select(a => a.GetType().Name))}"));
            Fetch(kit);
            s.Key(KeybindingCode.DropItem);
            Assert.Same(s.Station, s.In(() => kit.loc));
        }
        obj_item_healthanalyzer analyzer = s.In(InMedbay<obj_item_healthanalyzer>);
        Assert.NotNull(analyzer);
        Fetch(analyzer);
        s.Do(() => alice.Mob.face_atom(s.Bob.Mob));
    }

    // Takes a thing of the type out of the medkit lying at Alice's feet: the kit picked up, swapped to the other hand, opened by a click
    // and the thing clicked into the free hand. The kit stays in the other hand.
    internal static T FromKit<T>(Scenario s, obj_item_storage_medkit kit) where T : obj_item
    {
        if (s.In(() => kit.loc) != s.Alice.Mob)
        {
            s.Click(kit);
            Assert.Same(kit, s.Held());
            s.Key(KeybindingCode.SwapHands);
        }
        if (!s.In(() => Dm.truthy(Dm.@in(s.Alice.Mob, kit.atom_storage.is_using))))
            s.ClickScreen(kit);
        T item = s.In(() => Dm.each(kit.contents).OfType<T>().Where(i => i.GetType() == typeof(T)).OrderBy(i => i.dm_id).First());
        s.ClickScreen(item);
        Assert.Same(item, s.Held());
        return item;
    }

    internal static void PutKitDown(Scenario s, obj_item_storage_medkit kit)
    {
        s.Key(KeybindingCode.DropItem); // the used thing
        s.Key(KeybindingCode.SwapHands);
        Assert.Same(kit, s.Held());
        s.Key(KeybindingCode.DropItem);
    }

    // The emergency and its treatment.
    private static void TreatmentPart(Scenario s)
    {
        Actor alice = s.Alice, bob = s.Bob;
        ITestOutputHelper output = s.Out;
        Prepare(s);
        obj_item_storage_medkit_regular regular = s.In(() => Dm.each(s.Station.contents).OfType<obj_item_storage_medkit_regular>().Single());
        obj_item_storage_medkit_o2 o2 = s.In(() => Dm.each(s.Station.contents).OfType<obj_item_storage_medkit_o2>().Single());

        // --- The emergency, by dev commands
        foreach (string line in new[] { "wound Bob l_arm slash severe", "wound Bob chest burn moderate", "damage Bob burn 60 chest", "breath Bob 20", "damage Bob oxy 60", "heart Bob stop" })
        {
            alice.P.Session.DevCommand(line);
            s.W.Run(1);
        }
        s.Run(2);
        output.WriteLine("the emergency: " + s.State());
        Assert.True(s.In(() => Dm.truthy(bob.Mob.undergoing_cardiac_arrest())), "the heart has stopped");
        Assert.Contains(s.Wounds, w => w is datum_wound_slash && s.In(() => Dm.str(w.limb.body_zone)) == BODY_ZONE_L_ARM && s.Flow(w) > 0);
        Assert.Contains(s.Wounds, w => w is datum_wound_burn && s.In(() => Dm.str(w.limb.body_zone)) == BODY_ZONE_CHEST);
        Assert.True(s.Health <= s.In(() => Dm.num(bob.Mob.hardcrit_threshold)), "Bob is in hard crit");
        Assert.True(s.Stat == HARD_CRIT, "and out cold");

        // --- 1. The scan: the exact numbers in Alice's chat only (D40)
        alice.Texts.Clear();
        bob.Texts.Clear();
        s.Carol.Texts.Clear();
        (float burnAt, float oxyAt) = (s.Burn, s.Oxy);
        s.Click(bob.Mob, AimZone.Chest, seconds: 0.2f);
        string scan = alice.Texts.LastOrDefault(t => t.Contains("boxed_message blue_box", StringComparison.Ordinal));
        Assert.NotNull(scan);
        output.WriteLine(HtmlText.ToPlain(scan));
        float Shown(string colour) => float.Parse(Regex.Match(scan, $"<font color='{colour}'><b>(-?[0-9.]+)</b></font>").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        (float burnShown, float oxyShown) = (Shown("#ff9933"), Shown("#33ccff"));
        Assert.Contains(burnShown, new[] { MathF.Ceiling(burnAt), MathF.Ceiling(s.Burn) });
        Assert.Contains(oxyShown, new[] { MathF.Ceiling(oxyAt), MathF.Ceiling(s.Oxy) });
        Assert.Contains("left arm", HtmlText.ToPlain(scan), StringComparison.OrdinalIgnoreCase);
        s.Measured["scan"] = $"burn {burnShown}, oxygen {oxyShown}";
        foreach (Actor other in new[] { bob, s.Carol })
            Assert.DoesNotContain(other.Texts, t => t.Contains("boxed_message", StringComparison.Ordinal) || t.Contains("% healthy", StringComparison.Ordinal) || t.Contains("Analyzing results", StringComparison.Ordinal));
        Assert.Contains(alice.Heard, h => h.File == "sound/items/healthanalyzer.ogg");
        s.Key(KeybindingCode.DropItem);

        // --- 2. The defibrillator: the paddles off the wall, wielded (Z), a click on his chest; 3 + 2 seconds
        s.Click(s.Mount);
        Assert.Same(s.Defib.paddles, s.Held());
        s.Key(KeybindingCode.ActivateInhand);
        Assert.True(s.In(() => HAS_TRAIT(s.Defib.paddles, TRAIT_WIELDED)), "Z wields the paddles");
        s.Click(bob.Mob, AimZone.Chest, seconds: 5.5f);
        output.WriteLine("after the defibrillator: " + s.State());
        Assert.False(s.In(() => Dm.truthy(bob.Mob.undergoing_cardiac_arrest())), "the heart beats again");
        Assert.Contains(alice.Texts, t => t.Contains("pings: Patient's heart is now beating again.", StringComparison.Ordinal));
        Assert.NotEqual((float)DEAD, s.Stat);
        s.Key(KeybindingCode.DropItem); // the paddles snap back into the unit
        Assert.Same(s.Defib, s.In(() => s.Defib.paddles.loc));

        // --- 3. CPR with an empty hand: a pump takes 7 oxygen damage off
        float oxyBefore = s.Oxy;
        s.Click(bob.Mob, AimZone.Head, seconds: 3.5f);
        Assert.Contains(alice.Texts, t => t.Contains("You perform CPR on", StringComparison.Ordinal));
        output.WriteLine($"after CPR ({oxyBefore:0.#} oxygen damage before): " + s.State());

        // --- 4. The regular medkit's epinephrine medipen
        var pen = FromKit<obj_item_reagent_containers_hypospray_medipen>(s, regular);
        s.Click(bob.Mob, AimZone.Chest);
        Assert.Equal("medipen0", s.In(() => Dm.str(pen.icon_state)));
        Assert.True(s.In(() => Dm.truthy(bob.Mob.has_reagent(typeof(datum_reagent_medicine_epinephrine)))), "epinephrine in his blood");
        s.Key(KeybindingCode.DropItem);

        // --- 5. Gauze, then the suture on the left arm
        datum_wound_slash slash = s.Wounds.OfType<datum_wound_slash>().Single();
        float flow = s.Flow(slash);
        FromKit<obj_item_stack_medical_wrap_gauze>(s, regular);
        s.Click(bob.Mob, AimZone.LeftArm, seconds: 3); // other_delay 2 s
        Assert.NotNull(s.In(() => ((obj_item_bodypart)bob.Mob.get_bodypart(BODY_ZONE_L_ARM)).applied_items?[LIMB_ITEM_GAUZE]));
        s.Key(KeybindingCode.DropItem);
        FromKit<obj_item_stack_medical_suture>(s, regular);
        // tg's suture loop treats one wound until it closes or demotes (a severe cut becomes a moderate one); she clicks again for the next
        List<datum_wound> ArmBleeds() => s.Wounds.Where(w => s.In(() => Dm.str(w.limb.body_zone)) == BODY_ZONE_L_ARM && s.Flow(w) > 0).ToList();
        int clicks = 0;
        for (int i = 0; i < 40 && ArmBleeds().Count > 0; i++)
        {
            if (!s.In(() => Dm.each_cast<object>(alice.Mob.do_afters).Any()))
            {
                s.Click(bob.Mob, AimZone.LeftArm, seconds: 0.5f);
                clicks++;
            }
            s.Run(0.5f);
        }
        s.Measured["suture"] = $"the slash's flow {flow:0.##} -> 0 by {clicks} suture clicks ({string.Join(", ", s.Wounds.Select(w => s.In(() => Dm.str(w.name))))} left)";
        output.WriteLine("after the suture: " + s.State());
        Assert.Contains(alice.Heard, h => (h.File ?? "").StartsWith("sound/items/suture/", StringComparison.Ordinal));
        s.Key(KeybindingCode.DropItem);

        // --- 6. The mesh on the chest, torn open in the hand first (Z)
        float burn = s.Burn;
        FromKit<obj_item_stack_medical_mesh>(s, regular);
        s.Key(KeybindingCode.ActivateInhand);
        s.Click(bob.Mob, AimZone.Chest, seconds: 1);
        for (int i = 0; i < 30 && s.In(() => Dm.num(((obj_item_bodypart)bob.Mob.get_bodypart(BODY_ZONE_CHEST)).burn_dam)) > 0 && s.Held() is obj_item_stack_medical_mesh; i++)
            s.Run(1);
        s.Measured["mesh"] = $"burn {burn:0.#} -> {s.Burn:0.#}";
        output.WriteLine("after the mesh: " + s.State());
        Assert.True(s.Burn < burn, "the mesh heals the burn");
        Assert.Contains(alice.Heard, h => (h.File ?? "").StartsWith("sound/items/regenerative_mesh/", StringComparison.Ordinal));
        PutKitDown(s, regular);

        // --- 7. The oxygen kit: the salbutamol medipen and a convermol syringe
        FromKit<obj_item_reagent_containers_hypospray_medipen_salbutamol>(s, o2);
        s.Click(bob.Mob, AimZone.Chest);
        Assert.True(s.In(() => Dm.truthy(bob.Mob.has_reagent(typeof(datum_reagent_medicine_salbutamol)))), "salbutamol in his blood");
        s.Key(KeybindingCode.DropItem);
        FromKit<obj_item_reagent_containers_syringe_convermol>(s, o2);
        s.Click(bob.Mob, AimZone.Chest, seconds: 3.5f);
        Assert.True(s.In(() => Dm.truthy(bob.Mob.has_reagent(typeof(datum_reagent_medicine_c2_convermol)))), "convermol in his blood");
        PutKitDown(s, o2);

        // He comes round as the medicines work
        int waited = 0;
        for (; waited < 120 && (s.Stat != STABLE || s.In(() => bob.Mob.losebreath) > 0); waited++)
            s.Run(1);
        s.Measured["recovery"] = $"{waited} s after the last injection";
        output.WriteLine($"recovered after {waited} s: " + s.State());
    }

    internal static void AssertClean(Scenario s)
    {
        foreach (string r in s.Runtimes)
            s.Out.WriteLine("RUNTIME " + r[..Math.Min(r.Length, 1500)]);
        Assert.True(s.Runtimes.Count == 0, string.Join("\n", s.Runtimes.Take(3)));
        Assert.Equal(s.BootRuntimes, s.W.World.RuntimeErrorCount);
    }

    [MetaServerFact]
    public void A_paramedic_alone_brings_a_bleeding_burned_suffocating_man_in_cardiac_arrest_out_of_crit_with_medbays_things()
    {
        Scenario s = Boot(output);
        using (s.W)
        {
            TreatmentPart(s);
            Actor bob = s.Bob;
            Assert.Equal((float)STABLE, s.Stat);
            Assert.All(s.Wounds, w => Assert.Equal(0f, s.Flow(w)));
            Assert.Equal(0f, s.In(() => bob.Mob.losebreath));

            // breathing normally for 60 seconds: conscious, nothing bleeding, losebreath 0, the oxygen damage never rising
            float oxy = s.Oxy, blood = s.In(() => Dm.num(bob.Mob.blood_volume));
            for (int i = 0; i < 60; i++)
            {
                s.Run(1);
                Assert.Equal((float)STABLE, s.Stat);
                Assert.Equal(0f, s.In(() => bob.Mob.losebreath));
                Assert.True(s.Oxy <= oxy, $"the oxygen damage rose from {oxy} to {s.Oxy} at second {i + 1}");
                oxy = s.Oxy;
                Assert.All(s.Wounds, w => Assert.Equal(0f, s.Flow(w)));
                Assert.Equal(0f, s.In(() => Dm.num(bob.Mob.get_bleed_rate())));
            }
            output.WriteLine("60 seconds on: " + s.State());
            Assert.True(s.In(() => Dm.num(bob.Mob.blood_volume)) >= blood, "no blood lost");
            foreach (KeyValuePair<string, string> m in s.Measured)
                output.WriteLine($"{m.Key}: {m.Value}");
            AssertClean(s);
        }
    }

    [MetaServerFact]
    public void The_treatment_run_twice_from_a_fresh_boot_ends_with_the_same_state_hash()
    {
        (ulong Hash, long Tick) Once()
        {
            Scenario s = Boot(output);
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
