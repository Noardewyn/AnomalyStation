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
/// Gaps package MO2 (D333): the MODsuit modules, in a booted toy room with Bob as a player wearing a sealed pre-equipped suit:
/// every pre-equipped suit carries its live modules (the seam keeps out only the ninja's); the engineering
/// suit's welding visor, radiation protection, head protector, magboots and tether; the atmospheric suit's t-ray and mister; the
/// medical suits' health analyzer, quick carry, injector and defibrillator; the security suits' magnetic harness, quick cuff, pepper
/// shoulders, megaphone, criminal capture, projectile dampener and the mirage grenade dispenser; the supply suits' GPS, ore bag,
/// drill, clamp and magnet; the clown suit's waddle and bike horn; the science modules (the reagent scanner, the anomaly-locked
/// anti-gravity module with and without its core); the maintenance modules (the paper dispenser, the tanner, the springlock); the chrono
/// suit's timeline modules (the eradication field, the time stop, the rewinder, the timeline jumper); the storage module of holding;
/// kinesis; the pathfinder's implant; the syndicate's modules (the anti slip, the energy shield, the flamethrower, the infiltrator's
/// programs, the chameleon and the Wraith cloak).
/// </summary>
public class ModsuitsMO2Tests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Make<T>(AudioRoom r, atom loc) where T : atom
    {
        T thing = r.In(() => Dm.@new<T>(loc));
        r.Step(2);
        return thing;
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item
    {
        T item = Make<T>(r, who.loc);
        r.In(() => Waiting.NoSleep(who.put_in_hands(item)));
        r.In(() => Assert.Same(item, who.get_active_held_item()));
        return item;
    }

    private static turf At(AudioRoom r, int x, int y) => r.In(() => locate(x, y, 1));

    // a suit of the given kind worn on Bob's back, deployed and activated (sealed)
    private static T Sealed<T>(AudioRoom r, mob_living_carbon_human bob) where T : obj_item_mod_control
    {
        var mod = Worn<T>(r, bob);
        r.Command(() => Waiting.Detach(mod.quick_deploy(bob)));
        r.Step(4);
        r.Command(() => Waiting.Detach(mod.toggle_activate(bob)));
        r.Step(12 * Second);
        r.In(() => Assert.True(Dm.truthy(mod.active), typeof(T).Name + " did not activate"));
        return mod;
    }

    private static T Worn<T>(AudioRoom r, mob_living_carbon_human bob) where T : obj_item_mod_control
    {
        var mod = Make<T>(r, bob.loc);
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(mod, mod.slot_flags))))); // the back, or the infiltrator's belt
        r.Step(2);
        r.In(() => Assert.Same(bob, mod.wearer));
        return mod;
    }

    private static T Module<T>(AudioRoom r, obj_item_mod_control mod) where T : obj_item_mod_module
    {
        T module = r.In(() => Dm.locate<T>(mod.modules));
        Assert.True(module != null, typeof(T).Name + " is not installed in " + mod.GetType().Name);
        return module;
    }

    // a loose module installed into the suit (with no complexity left over, the suit's own complexity limit is raised: the test
    // looks at the module, not at the suit's budget)
    private static T Install<T>(AudioRoom r, mob_living_carbon_human bob, obj_item_mod_control mod) where T : obj_item_mod_module
    {
        var module = Make<T>(r, bob.loc);
        r.In(() => mod.complexity_max = 99);
        r.Command(() => Waiting.Detach(mod.install(module, bob)));
        r.Step(2);
        r.In(() => Assert.True(Dm.@in(module, mod.modules), typeof(T).Name + " was not installed"));
        return module;
    }

    private static void Select(AudioRoom r, mob who, obj_item_mod_module module, int ticks = 4)
    {
        r.Command(() => Waiting.Detach(module.on_select(who)));
        r.Step(ticks);
    }

    private static void UseOn(AudioRoom r, obj_item_mod_module module, atom target, int ticks = 4)
    {
        r.Command(() => Waiting.Detach(module.on_select_use(target)));
        r.Step(ticks);
    }

    private static bool Heard(AudioRoom r, string file) => r.Sounds.Heard.Any(h => (h.File ?? "").Contains(file, StringComparison.Ordinal));

    [Fact]
    public void EveryPreEquippedSuitCarriesItsLiveModules()
    {
        var (r, feed, bob) = Room(9701);
        var missing = new List<string>();
        var inertKept = new List<string>();
        int suits = 0, modules = 0;
        foreach (Type suitType in r.In(() => Dm.each_cast<Type>(subtypesof(typeof(obj_item_mod_control_pre_equipped))).ToList()))
        {
            if (InertSeam.IsInertType(suitType))
                continue;
            var mod = r.In(() => Dm.@new(suitType, bob.loc) as obj_item_mod_control_pre_equipped);
            r.Step(2);
            suits++;
            r.In(() =>
            {
                var expected = Dm.each_cast<Type>(mod.applied_modules).Concat(Dm.each_cast<Type>(((datum_mod_theme)mod.theme).inbuilt_modules)).ToList();
                foreach (Type moduleType in expected)
                {
                    bool installed = Dm.each(mod.modules).Any(m => m.GetType() == moduleType);
                    if (InertSeam.IsInertType(moduleType))
                    {
                        if (installed)
                            inertKept.Add(suitType.Name + ": " + moduleType.Name);
                        continue;
                    }
                    if (!installed)
                        missing.Add(suitType.Name + ": " + moduleType.Name);
                }
                foreach (object module in Dm.each(mod.modules))
                {
                    modules++;
                    Assert.False(InertSeam.IsInertType(module.GetType()), suitType.Name + " holds the inert " + module.GetType().Name);
                }
            });
            r.In(() => qdel(mod));
            r.Step(2);
        }
        Assert.True(suits > 30, "only " + suits + " pre-equipped suits");
        Assert.True(missing.Count == 0, "live modules missing: " + string.Join(", ", missing));
        Assert.Empty(inertKept);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheEngineeringSuitsVisorRadiationShieldMagbootsAndTetherWork()
    {
        var (r, feed, bob) = Room(9702);
        var mod = Sealed<obj_item_mod_control_pre_equipped_engineering>(r, bob);
        var magboot = Module<obj_item_mod_module_magboot>(r, mod);
        var tether = Module<obj_item_mod_module_tether>(r, mod);
        r.In(() =>
        {
            Assert.Equal(FLASH_PROTECTION_WELDER_HYPER_SENSITIVE, Dm.num(((obj_item_clothing)bob.head).flash_protect)); // the welding visor
            Assert.True(HAS_TRAIT(bob, TRAIT_BYPASS_EARLY_IRRADIATED_CHECK)); // radiation protection
            Assert.True(HAS_TRAIT(bob.wear_suit, TRAIT_RADIATION_PROTECTED_CLOTHING));
            Assert.True(HAS_TRAIT(bob, TRAIT_HEAD_INJURY_BLOCKED)); // the head protector
            Assert.False(HAS_TRAIT(bob, TRAIT_NEGATES_GRAVITY));
        });

        Select(r, bob, magboot);
        r.In(() =>
        {
            Assert.True(Dm.truthy(magboot.active));
            Assert.True(HAS_TRAIT(bob, TRAIT_NEGATES_GRAVITY));
            Assert.True(HAS_TRAIT(bob, TRAIT_NO_SLIP_WATER));
        });
        Select(r, bob, magboot);
        r.In(() => Assert.False(HAS_TRAIT(bob, TRAIT_NEGATES_GRAVITY)));

        // the tether, fired at the wall: an anchor where it struck, and Bob tethered to it
        Select(r, bob, tether);
        r.In(() => Assert.True(Dm.truthy(tether.active)));
        UseOn(r, tether, At(r, 13, 4), Second);
        r.In(() =>
        {
            Assert.Single(Dm.each(bob.GetComponents(typeof(datum_component_tether))));
            Assert.True(HAS_TRAIT_FROM(bob, TRAIT_TETHER_ATTACHED, REF(tether)));
            Assert.Contains(Dm.each(world.contents), a => a is obj_item_tether_anchor);
        });
        Assert.True(Heard(r, "batonextend"));
        // the module's "cut tethers" button snaps it
        r.Command(() => Waiting.Detach(tether.configure_edit("cut_tethers", null)));
        r.Step(2);
        r.In(() => Assert.False(HAS_TRAIT_FROM(bob, TRAIT_TETHER_ATTACHED, REF(tether))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheAtmosphericSuitsTRayScansAndItsMisterSprays()
    {
        var (r, feed, bob) = Room(9703);
        var mod = Sealed<obj_item_mod_control_pre_equipped_atmospheric>(r, bob);
        var tray = Module<obj_item_mod_module_t_ray>(r, mod);
        var mister = Module<obj_item_mod_module_mister_atmos>(r, mod);
        Select(r, bob, tray);
        r.In(() => Assert.True(Dm.truthy(tray.active)));
        r.Step(5 * Second); // the suit's process runs the scan
        Select(r, bob, mister);
        r.In(() =>
        {
            Assert.True(Dm.truthy(mister.active));
            var nozzle = bob.get_active_held_item();
            Assert.Same(mister.device, nozzle);
            Assert.True(Dm.num(((datum_reagents)mister.reagents).total_volume) > 0, "the mister holds its water");
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMedicalModulesScanCarryInjectAndShock()
    {
        var (r, feed, bob) = Room(9704);
        var patient = r.Dummy(5, 4);
        var mod = Sealed<obj_item_mod_control_pre_equipped_medical>(r, bob);
        var analyzer = Module<obj_item_mod_module_health_analyzer>(r, mod);
        r.In(() => Assert.True(HAS_TRAIT(bob, TRAIT_QUICK_CARRY))); // the quick carry module
        Select(r, bob, analyzer);
        r.In(() => Assert.True(Dm.truthy(analyzer.active)));
        UseOn(r, analyzer, patient);
        Assert.True(feed.Chat.Any(c => c.Target == bob && (c.Html ?? "").Contains("Analyzing results for", StringComparison.Ordinal)), "the health scan");

        // the emergency medical suit's defibrillator extends its paddles, and its injector its syringe
        r.Command(() => Waiting.Detach(mod.toggle_activate(bob)));
        r.Step(12 * Second);
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(mod, force: TRUE)));
        r.Step(4);
        var emergency = Sealed<obj_item_mod_control_pre_equipped_emergency_medical>(r, bob);
        var defib = Module<obj_item_mod_module_defibrillator>(r, emergency);
        var injector = Module<obj_item_mod_module_injector>(r, emergency);
        Select(r, bob, defib);
        r.In(() =>
        {
            Assert.True(Dm.truthy(defib.active));
            Assert.IsAssignableFrom<obj_item_shockpaddles>(bob.get_active_held_item());
        });
        Select(r, bob, injector);
        r.In(() =>
        {
            Assert.True(Dm.truthy(injector.active));
            Assert.IsType<obj_item_reagent_containers_syringe_mod>(bob.get_active_held_item());
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSecurityModulesHarnessCuffAndDampen()
    {
        var (r, feed, bob) = Room(9705);
        var mod = Sealed<obj_item_mod_control_pre_equipped_security>(r, bob);
        Module<obj_item_mod_module_magnetic_harness>(r, mod);
        Module<obj_item_mod_module_pepper_shoulders>(r, mod);
        Module<obj_item_mod_module_criminalcapture>(r, mod);
        r.In(() => Assert.True(HAS_TRAIT(bob, TRAIT_FAST_CUFFING))); // the quick cuff module
        // the magnetic harness pulls a dropped gun back onto the suit
        var gun = Hold<obj_item_gun_energy_disabler>(r, bob);
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(gun)));
        r.Step(Second);
        r.In(() => Assert.Same(gun, bob.s_store));
        Assert.True(feed.Any("reattached"));

        // the safeguard suit: the megaphone and the projectile dampener's field
        r.Command(() => Waiting.Detach(mod.toggle_activate(bob)));
        r.Step(12 * Second);
        r.In(() =>
        {
            Waiting.NoSleep(bob.dropItemToGround(gun, force: TRUE));
            Waiting.NoSleep(bob.dropItemToGround(mod, force: TRUE));
        });
        r.Step(4);
        var safeguard = Sealed<obj_item_mod_control_pre_equipped_safeguard>(r, bob);
        var dampener = Module<obj_item_mod_module_projectile_dampener>(r, safeguard);
        var megaphone = Module<obj_item_mod_module_megaphone>(r, safeguard);
        Select(r, bob, dampener);
        r.In(() =>
        {
            Assert.True(Dm.truthy(dampener.active));
            Assert.NotNull(dampener.dampening_field);
        });
        Select(r, bob, megaphone);
        r.In(() => Assert.True(Dm.truthy(megaphone.active)));

        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMirageGrenadeDispenserArmsAGrenadeInHand()
    {
        var (r, feed, bob) = Room(9712);
        var mod = Sealed<obj_item_mod_control_pre_equipped_civilian>(r, bob);
        var dispenser = Install<obj_item_mod_module_dispenser_mirage>(r, bob, mod);
        r.In(() => Waiting.NoSleep(bob.drop_all_held_items()));
        Select(r, bob, dispenser);
        var grenade = r.In(() => bob.get_active_held_item() as obj_item_grenade_mirage);
        Assert.NotNull(grenade);
        r.In(() =>
        {
            Assert.True(Dm.truthy(grenade.active));
            Assert.Same(bob, grenade.thrower);
        });
        r.Step(10 * Second);
        r.In(() => Assert.True(QDELETED(grenade), "the grenade went off"));
        // tg bug kept: the mirage it makes dies as it is made (mock_as sets the illusion's health only when no health is given,
        // isnull(hp) the wrong way round, illusion.dm:76, so its health is 0), and the death timer it then sets stack_traces
        foreach (string runtime in r.Room.RuntimeText)
            Assert.Contains("addtimer called with a callback assigned to a qdeleted object", runtime);
        r.In(() => Assert.DoesNotContain(Dm.each(world.contents), a => a is mob_living_basic_illusion_mirage));
    }

    [Fact]
    public void TheMiningSuitsOreBagDrillAndTheLoadersClamp()
    {
        var (r, feed, bob) = Room(9706);
        var mod = Sealed<obj_item_mod_control_pre_equipped_mining>(r, bob);
        var gps = Module<obj_item_mod_module_gps>(r, mod);
        var orebag = Module<obj_item_mod_module_orebag>(r, mod);
        Module<obj_item_mod_module_drill>(r, mod);
        r.In(() => Assert.NotNull(gps.GetComponent(typeof(datum_component_gps_item))));
        // the ore bag picks up the ore Bob walks over
        var ore = Make<obj_item_stack_ore_iron>(r, At(r, 5, 4));
        r.In(() => Waiting.NoSleep(bob.Move(At(r, 5, 4), EAST)));
        r.Step(Second);
        r.In(() => Assert.Same(orebag, ore.loc));
        // the drill digs the rock its wearer bumps: Dan, with no player (DV348: a player's dig runs only while the left button is
        // held on the rock, which a bump does not do; a mob with no player ignores the hold, so this is tg's bump-mining)
        var dan = r.Dummy(5, 6);
        r.In(() => ADD_TRAIT(dan, TRAIT_ADVANCEDTOOLUSER, "test"));
        var danSuit = Sealed<obj_item_mod_control_pre_equipped_mining>(r, dan);
        var drill = Module<obj_item_mod_module_drill>(r, danSuit);
        r.In(() => Waiting.NoSleep(At(r, 7, 6).ChangeTurf(typeof(turf_closed_mineral))));
        r.Step(2);
        Select(r, dan, drill);
        r.In(() => Assert.True(Dm.truthy(drill.active)));
        r.In(() => Waiting.NoSleep(dan.Move(At(r, 6, 6), EAST)));
        r.Step(Second);
        r.In(() => Assert.Same(At(r, 6, 6), dan.loc));
        r.In(() => Waiting.NoSleep(dan.Move(At(r, 7, 6), EAST))); // a bump into the rock
        r.Step(8 * Second);
        r.In(() => Assert.False(At(r, 7, 6) is turf_closed_mineral, "the drill mined the rock"));

        // the loader's clamp picks up a crate and puts it down
        r.Command(() => Waiting.Detach(mod.toggle_activate(bob)));
        r.Step(12 * Second);
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(mod, force: TRUE)));
        r.Step(4);
        var loader = Sealed<obj_item_mod_control_pre_equipped_loader>(r, bob);
        var clamp = Module<obj_item_mod_module_clamp>(r, loader);
        Module<obj_item_mod_module_hydraulic>(r, loader);
        Module<obj_item_mod_module_magnet>(r, loader);
        var crate = Make<obj_structure_closet_crate>(r, r.In(() => get_step(bob, EAST) as turf));
        Select(r, bob, clamp);
        UseOn(r, clamp, crate, 3 * Second);
        r.In(() =>
        {
            Assert.True(Dm.@in(crate, clamp.stored_crates));
            Assert.Same(clamp, crate.loc);
        });
        UseOn(r, clamp, r.In(() => get_step(bob, EAST) as turf), 3 * Second);
        r.In(() =>
        {
            Assert.False(Dm.@in(crate, clamp.stored_crates));
            Assert.True(crate.loc is turf);
        });

        // the disposal selector sends its wearer to the destination picked in its configuration (until then, none: tg's text tag)
        var connector = Install<obj_item_mod_module_disposal_connector>(r, bob, loader);
        r.In(() =>
        {
            Assert.Equal(0f, connector.disposal_tag);
            Waiting.NoSleep(connector.configure_edit("disposal_tag", "Cargo Bay"));
            Assert.Equal("Cargo Bay", GLOB.TAGGERLOCATIONS[connector.disposal_tag]);
            var holder = Dm.@new<obj_structure_disposalholder>(bob.loc);
            connector.disposal_handling(bob, holder, null, TRUE);
            Assert.Equal(connector.disposal_tag, holder.destinationTag);
            qdel(holder);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheClownSuitWaddlesAndHonksAndTheScienceModulesWork()
    {
        var (r, feed, bob) = Room(9707);
        var mod = Sealed<obj_item_mod_control_pre_equipped_cosmohonk>(r, bob);
        Module<obj_item_mod_module_waddle>(r, mod);
        var horn = Module<obj_item_mod_module_bikehorn>(r, mod);
        r.In(() =>
        {
            Assert.True(HAS_TRAIT(bob, TRAIT_WADDLING));
            Assert.NotNull(bob.shoes.GetComponent(typeof(datum_component_squeak)));
        });
        Select(r, bob, horn);
        Assert.True(Heard(r, "bikehorn"));

        // the reagent scanner and the anomaly-locked anti-gravity module: refused without a core, weightless with one
        var scanner = Install<obj_item_mod_module_reagent_scanner>(r, bob, mod);
        Select(r, bob, scanner);
        r.In(() => Assert.True(HAS_TRAIT(bob, TRAIT_REAGENT_SCANNER)));
        var coreless = Install<obj_item_mod_module_anomaly_locked_antigrav>(r, bob, mod);
        Select(r, bob, coreless);
        r.In(() => Assert.False(Dm.truthy(coreless.active)));
        Assert.True(feed.Any("balloon: no core!"));
        Assert.Contains("You need to insert", r.In(() => string.Join("\n", Dm.each(Waiting.NoSleep(coreless.examine(bob))).Select(o => Dm.str(o)))));
        r.Command(() => Waiting.Detach(mod.uninstall(coreless)));
        r.Step(2);
        var antigrav = Install<obj_item_mod_module_anomaly_locked_antigrav_prebuilt>(r, bob, mod);
        // the prebuilt core (a gravitational anomaly core); tg's "-core" icon waits for the module's next icon update, as the core
        // goes in before the component listens to it
        r.In(() => Assert.IsType<obj_item_assembly_signaler_anomaly_grav>(((datum_component_anomaly_locked_module)antigrav.GetComponent(typeof(datum_component_anomaly_locked_module))).core));
        Select(r, bob, antigrav);
        r.In(() =>
        {
            Assert.True(Dm.truthy(antigrav.active));
            Assert.False(Dm.truthy(bob.has_gravity()), "the wearer is weightless");
        });
        Select(r, bob, antigrav);
        r.In(() => Assert.True(Dm.truthy(bob.has_gravity())));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMaintenanceModulesDispenseTanAndSpring()
    {
        var (r, feed, bob) = Room(9708);
        var mod = Sealed<obj_item_mod_control_pre_equipped_civilian>(r, bob);
        var dispenser = Install<obj_item_mod_module_paper_dispenser>(r, bob, mod);
        r.In(() => Waiting.NoSleep(bob.drop_all_held_items()));
        Select(r, bob, dispenser, 2 * Second); // a second's work
        r.In(() => Assert.IsType<obj_item_paper>(bob.get_active_held_item()));
        var tanner = Install<obj_item_mod_module_tanner>(r, bob, mod);
        Select(r, bob, tanner);
        Assert.True(Heard(r, "microwave-end"));

        // the springlock snaps shut on its wearer in a wet spray
        var springlock = Install<obj_item_mod_module_springlock>(r, bob, mod);
        r.In(() => bob.SEND_SIGNAL(COMSIG_ATOM_EXPOSE_REAGENTS, Dm.list(bob, Dm.list(), null, TOUCH, 10)));
        r.In(() => Assert.True(Dm.truthy(springlock.set_off)));
        Assert.True(feed.Any("makes an ominous click sound"), "the springlock's warning");
        r.Step(6 * Second);
        Assert.True(feed.Any("*SNAP*"));
        Assert.True(Heard(r, "springlock"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheChronoSuitErasesStopsTimeRewindsAndJumps()
    {
        var (r, feed, bob) = Room(9709);
        var victim = r.Dummy(7, 4);
        var mod = Sealed<obj_item_mod_control_pre_equipped_chrono>(r, bob);
        var tem = Module<obj_item_mod_module_tem>(r, mod);
        var timestopper = Module<obj_item_mod_module_timestopper>(r, mod);
        var rewinder = Module<obj_item_mod_module_rewinder>(r, mod);
        var jumper = Module<obj_item_mod_module_timeline_jumper>(r, mod);

        // the timestream eradication module's beam holds the victim in an eradication field that erases them
        Select(r, bob, tem);
        UseOn(r, tem, victim, Second);
        var field = r.In(() => victim.loc as obj_structure_chrono_field);
        Assert.NotNull(field);
        r.In(() =>
        {
            Assert.Same(field, tem.field);
            Assert.Single(Dm.each(field.underlays)); // DV540: the victim's own picture, fading out
        });
        r.Step(32 * Second); // tg's timetokill of 3 SECONDS counts down by the seconds of each process
        r.In(() => Assert.True(QDELETED(victim), "the victim was erased: " + string.Join("\n", r.Room.RuntimeText)));
        Select(r, bob, tem); // put the beam away

        // the timestopper freezes a bystander until Bob moves
        var bystander = r.Dummy(5, 5);
        Select(r, bob, timestopper);
        r.In(() =>
        {
            Assert.NotNull(timestopper.timestop);
            Assert.True(HAS_TRAIT_FROM(bystander, TRAIT_STASIS, TIMESTOP_TRAIT));
            Assert.False(HAS_TRAIT_FROM(bob, TRAIT_STASIS, TIMESTOP_TRAIT));
        });
        r.In(() => Waiting.NoSleep(bob.Move(At(r, 4, 5), NORTH)));
        r.Step(Second);
        r.In(() =>
        {
            Assert.Null(timestopper.timestop);
            Assert.False(HAS_TRAIT_FROM(bystander, TRAIT_STASIS, TIMESTOP_TRAIT));
        });

        // the rewinder pulls Bob back to where he set his anchor ten seconds ago
        turf anchor = r.In(() => get_turf(bob));
        Select(r, bob, rewinder);
        r.In(() => Assert.Single(Dm.each(bob.GetComponents(typeof(datum_component_dejavu_timeline)))));
        r.In(() => Waiting.NoSleep(bob.Move(At(r, 5, 5), EAST)));
        r.Step(11 * Second);
        r.In(() => Assert.Same(anchor, get_turf(bob)));
        Assert.True(feed.Any("Your suit rewinds"));

        // the timeline jumper phases Bob out and back in
        Select(r, bob, jumper);
        r.In(() => Assert.IsType<obj_effect_dummy_phased_mob_chrono>(bob.loc));
        r.Step(6 * Second);
        Select(r, bob, jumper);
        r.In(() => Assert.True(bob.loc is turf));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheHoldingKinesisAndPathfinderModulesWork()
    {
        var (r, feed, bob) = Room(9710);
        var mod = Sealed<obj_item_mod_control_pre_equipped_civilian>(r, bob);
        // a storage module of holding without its core cannot go in; the prebuilt one opens a bag of holding's pocket
        r.In(() => Dm.@new<obj_item_screwdriver>(bob.loc));
        r.Command(() => Waiting.Detach(mod.uninstall(Dm.locate<obj_item_mod_module_storage>(mod.modules))));
        r.Step(2);
        var coreless = Make<obj_item_mod_module_storage_holding>(r, bob.loc);
        r.Command(() => Waiting.Detach(mod.install(coreless, bob)));
        r.Step(2);
        r.In(() => Assert.False(Dm.@in(coreless, mod.modules)));
        Assert.True(feed.Any("balloon: no core!"));
        var holding = Install<obj_item_mod_module_storage_holding_prebuilt>(r, bob, mod);
        r.In(() =>
        {
            Assert.IsType<datum_storage_bag_of_holding>(holding.atom_storage);
            Assert.True(HAS_TRAIT(holding, TRAIT_BLUESPACE_INTERFERENCE));
            Assert.NotNull(mod.atom_storage);
        });

        // kinesis grabs a toolbox off the floor and throws it on the next use
        var kinesis = Install<obj_item_mod_module_anomaly_locked_kinesis_prebuilt>(r, bob, mod);
        var toolbox = Make<obj_item_storage_toolbox>(r, At(r, 6, 4));
        Select(r, bob, kinesis);
        UseOn(r, kinesis, toolbox, 2);
        r.In(() =>
        {
            Assert.Same(toolbox, kinesis.grabbed_atom);
            Assert.NotNull(kinesis.kinesis_beam);
            Assert.True(HAS_TRAIT_FROM(toolbox, TRAIT_NO_FLOATING_ANIM, REF(kinesis)));
        });
        r.Step(Second); // the grip holds while the suit processes (the first-person client sends no cursor turf, DV98)
        r.In(() => Assert.Same(toolbox, kinesis.grabbed_atom));
        UseOn(r, kinesis, toolbox, 2);
        r.In(() =>
        {
            Assert.Null(kinesis.grabbed_atom);
            Assert.False(HAS_TRAIT_FROM(toolbox, TRAIT_NO_FLOATING_ANIM, REF(kinesis)));
        });
        Assert.True(Heard(r, "repulse"));

        // the pathfinder implants its wearer, who gets the recall button
        Select(r, bob, kinesis);
        var pathfinder = Install<obj_item_mod_module_pathfinder>(r, bob, mod);
        Select(r, bob, pathfinder);
        r.In(() =>
        {
            Assert.False(Dm.truthy(pathfinder.implant_inside));
            Assert.Equal("pathfinder_empty", pathfinder.icon_state);
            Assert.Same(bob, pathfinder.implant.imp_in);
            Assert.Contains(Dm.each(bob.actions), a => a is datum_action_item_action_mod_recall);
        });
        Assert.True(feed.Any("balloon: implanted"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSyndicateModulesShieldBurnHideAndDisguise()
    {
        var (r, feed, bob) = Room(9711);
        var jumpsuit = Make<obj_item_clothing_under_color_black>(r, bob.loc); // the infiltrator suit goes on the belt, which needs a jumpsuit
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bob.equip_to_slot_if_possible(jumpsuit, ITEM_SLOT_ICLOTHING)))));
        var mod = Sealed<obj_item_mod_control_pre_equipped_infiltrator>(r, bob);
        Module<obj_item_mod_module_infiltrator>(r, mod);
        var demoralizer = Module<obj_item_mod_module_demoralizer>(r, mod);
        r.In(() =>
        {
            Assert.True(HAS_TRAIT(bob, TRAIT_UNKNOWN_APPEARANCE));
            Assert.True(HAS_TRAIT(bob, TRAIT_SILENT_FOOTSTEPS));
            Assert.True(HAS_TRAIT(mod, TRAIT_EXAMINE_SKIP));
            Assert.NotNull(demoralizer.demoralizer);
        });
        Install<obj_item_mod_module_noslip>(r, bob, mod);
        r.In(() => Assert.True(HAS_TRAIT(bob, TRAIT_NO_SLIP_WATER)));

        // the chameleon disguises the belt-worn suit as a belt and back
        var chameleon = Install<obj_item_mod_module_chameleon>(r, bob, mod);
        r.In(() => Assert.True(Dm.length(chameleon.possible_disguises) > 5, "the belts to look like"));
        r.Command(() => Waiting.Detach(mod.toggle_activate(bob))); // tg refuses a disguise while the suit is on
        r.Step(12 * Second);
        r.Command(() =>
        {
            chameleon.current_disguise = typeof(obj_item_storage_belt_utility);
            Waiting.Detach(chameleon.update_look());
        });
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal(Dm.initial(typeof(obj_item_storage_belt_utility), (obj_item p) => p.icon_state), mod.icon_state);
            Assert.Equal(Dm.initial(typeof(obj_item_storage_belt_utility), (obj_item p) => p.name), mod.name);
        });
        r.Command(() => Waiting.Detach(chameleon.return_look()));
        r.Step(2);
        r.In(() =>
        {
            Assert.Null(chameleon.current_disguise);
            Assert.Contains("infiltrator", mod.icon_state);
        });
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(mod, force: TRUE)));
        r.Step(4);

        // on a civilian suit: the energy shield goes up as a shielded component, the flamethrower fires, and the Wraith cloak's
        // stealth goes up with the suit and a bump discharges it
        var civilian = Worn<obj_item_mod_control_pre_equipped_civilian>(r, bob);
        Install<obj_item_mod_module_energy_shield>(r, bob, civilian);
        var flamethrower = Install<obj_item_mod_module_flamethrower>(r, bob, civilian);
        var wraith = Install<obj_item_mod_module_stealth_wraith>(r, bob, civilian);
        r.Command(() => Waiting.Detach(civilian.quick_deploy(bob)));
        r.Step(4);
        r.Command(() => Waiting.Detach(civilian.toggle_activate(bob)));
        r.Step(12 * Second);
        r.In(() =>
        {
            Assert.True(Dm.truthy(civilian.active));
            Assert.NotNull(civilian.GetComponent(typeof(datum_component_shielded)));
            Assert.True(Dm.truthy(wraith.stealth_active));
        });
        Select(r, bob, flamethrower);
        UseOn(r, flamethrower, At(r, 10, 4), 2);
        Assert.True(Heard(r, "flamethrower"));
        r.In(() => bob.SEND_SIGNAL(COMSIG_ATOM_BUMPED, Dm.list(bob, r.Dummy(5, 6))));
        r.Step(2);
        r.In(() => Assert.False(Dm.truthy(wraith.stealth_active)));
        Assert.True(feed.Any("gets discharged from contact"));
        r.AssertNoRuntimes();
    }
}
