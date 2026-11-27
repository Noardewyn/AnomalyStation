using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using System.Text.Json;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// MetaStation's last small pieces: the blobstart, generic maintenance and security equipment landmarks fill their lists and the
/// spare security lockers spawn on them past five officers (landmarks.dm, job.dm); the engine's radiation shutters shield only
/// when closed (shutters.dm); the outlet injector's and the passive gate's AtmosPump windows; the volume pump's overclock; the
/// chem master's board dial; the iced abductor's corpse; the medical wrench's examine; the contraband poster's trait.
/// </summary>
public class MetaLeftoversTests
{
    private static AudioRoom Room(ulong seed) => new AudioRoom(16, 11, seed: seed);

    [Fact]
    public void TheLandmarksFillTheirListsAndDeleteThemselves()
    {
        AudioRoom r = Room(15101);
        var blob = Place<obj_effect_landmark_blobstart>(r, 3, 3);
        var maint = Place<obj_effect_landmark_generic_maintenance_landmark>(r, 4, 3);
        var sec = Place<obj_effect_landmark_secequipment>(r, 5, 3);
        r.In(() =>
        {
            Assert.Contains(locate(3, 3, 1), Dm.each(GLOB.blobstart).ToList());
            Assert.Contains(locate(4, 3, 1), Dm.each(GLOB.generic_maintenance_landmarks).ToList());
            Assert.Contains(locate(5, 3, 1), Dm.each(GLOB.secequipment).ToList());
            Assert.True(QDELETED(blob) && QDELETED(maint) && QDELETED(sec));
        });
    }

    [Fact]
    public void EveryOfficerPastFiveGetsASpareSecurityLockerUntilTheLandmarksRunOut()
    {
        AudioRoom r = Room(15102);
        foreach (int x in new[] { 3, 4, 5 })
            Place<obj_effect_landmark_secequipment>(r, x, 3);
        r.In(() =>
        {
            var officer = SSjob.get_job(JOB_SECURITY_OFFICER) as datum_job;
            officer.spawn_positions = 7;
            SSjob.setup_officer_positions();
            // seven officers: two spare lockers, the third landmark is left
            Assert.Equal(7f, officer.total_positions);
            Assert.NotNull(Dm.locate<obj_structure_closet_secure_closet_security_sec>(locate(3, 3, 1)));
            Assert.NotNull(Dm.locate<obj_structure_closet_secure_closet_security_sec>(locate(4, 3, 1)));
            Assert.Null(Dm.locate<obj_structure_closet_secure_closet_security_sec>(locate(5, 3, 1)));
            Assert.Equal(1, GLOB.secequipment.len);

            officer.spawn_positions = 12;
            SSjob.setup_officer_positions();
            // twelve want seven more; only one landmark is left
            Assert.NotNull(Dm.locate<obj_structure_closet_secure_closet_security_sec>(locate(5, 3, 1)));
            Assert.Equal(0, GLOB.secequipment.len);
        });
    }

    [Fact]
    public void TheRadiationShuttersInsulateOnlyWhileClosed()
    {
        AudioRoom r = Room(15103);
        var shutters = Place<obj_machinery_door_poddoor_shutters_radiation>(r, 6, 5);
        Assert.Equal(RAD_EXTREME_INSULATION, r.In(() => shutters.rad_insulation));
        r.In(() => Waiting.Detach(shutters.open()));
        r.Step(3 * Second);
        r.In(() =>
        {
            Assert.False(Dm.truthy(shutters.density));
            Assert.Equal((float)RAD_NO_INSULATION, shutters.rad_insulation);
        });
        r.In(() => Waiting.Detach(shutters.close()));
        r.Step(3 * Second);
        r.In(() =>
        {
            Assert.True(Dm.truthy(shutters.density));
            Assert.Equal(RAD_EXTREME_INSULATION, shutters.rad_insulation);
        });
        var preopen = Place<obj_machinery_door_poddoor_shutters_radiation_preopen>(r, 8, 5);
        Assert.Equal((float)RAD_NO_INSULATION, r.In(() => preopen.rad_insulation));
    }

    [Fact]
    public void TheOutletInjectorsWindowTurnsItOnAndSetsItsRate()
    {
        AudioRoom r = Room(15104);
        mob_living_carbon_human bob = r.Join(5, 5, "Bob");
        var injector = Place<obj_machinery_atmospherics_components_unary_outlet_injector>(r, 6, 5);
        Take(bob);
        r.Command(() => Waiting.Detach(injector.ui_interact(bob)));
        r.Step(4);
        JsonElement payload = LastFullUpdate(Take(bob), out string window);
        Assert.Equal("AtmosPump", InterfaceOf(payload));
        Assert.Equal(50, payload.GetProperty("data").GetProperty("rate").GetDouble());
        Assert.Equal(MAX_TRANSFER_RATE, payload.GetProperty("data").GetProperty("max_rate").GetDouble());

        Act(r, bob, window, "rate", ("rate", "max"));
        Act(r, bob, window, "power");
        r.In(() =>
        {
            Assert.Equal((float)MAX_TRANSFER_RATE, injector.volume_rate);
            Assert.True(Dm.truthy(injector.on));
        });
        Act(r, bob, window, "rate", ("rate", "75"));
        Assert.Equal(75f, r.In(() => injector.volume_rate));
    }

    [Fact]
    public void ThePassiveGatesWindowSetsItsPressureWithinItsLimit()
    {
        AudioRoom r = Room(15105);
        mob_living_carbon_human bob = r.Join(5, 5, "Bob");
        var gate = Place<obj_machinery_atmospherics_components_binary_passive_gate>(r, 6, 5);
        Take(bob);
        r.Command(() => Waiting.Detach(gate.ui_interact(bob)));
        r.Step(4);
        JsonElement payload = LastFullUpdate(Take(bob), out string window);
        Assert.Equal("AtmosPump", InterfaceOf(payload));
        Assert.Equal(MAX_OUTPUT_PRESSURE, payload.GetProperty("data").GetProperty("max_pressure").GetDouble());

        Act(r, bob, window, "pressure", ("pressure", "300"));
        Act(r, bob, window, "power");
        r.In(() =>
        {
            Assert.Equal(300f, gate.target_pressure);
            Assert.True(Dm.truthy(gate.on));
        });
        // tg clamps to a hundred atmospheres, not to the window's maximum
        Act(r, bob, window, "pressure", ("pressure", "max"));
        Assert.Equal(MAX_OUTPUT_PRESSURE, r.In(() => gate.target_pressure));
        Act(r, bob, window, "pressure", ("pressure", "99999"));
        Assert.Equal(ONE_ATMOSPHERE * 100, r.In(() => gate.target_pressure), 3);
    }

    [Fact]
    public void AMultitoolOverclocksTheVolumePumpAndBack()
    {
        AudioRoom r = Room(15106);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(5, 5, "Bob");
        var pump = Place<obj_machinery_atmospherics_components_binary_volume_pump>(r, 6, 5);
        r.In(() =>
        {
            var multitool = Dm.@new<obj_item_multitool>(bob.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(pump.multitool_act(bob, multitool))));
            Assert.True(Dm.truthy(pump.overclocked));
            Assert.True(feed.Any("disable its pressure limits"));
            Waiting.NoSleep(pump.multitool_act(bob, multitool));
            Assert.False(Dm.truthy(pump.overclocked));
            Assert.True(feed.Any("turn its limiters back on"));
        });
    }

    [Fact]
    public void AScrewdriverTurnsTheChemMastersBoardIntoACondiMastersAndBack()
    {
        AudioRoom r = Room(15107);
        mob_living_carbon_human bob = r.Join(5, 5, "Bob");
        r.In(() =>
        {
            var board = Dm.@new<obj_item_circuitboard_machine_chem_master>(bob.loc);
            var screwdriver = Dm.@new<obj_item_screwdriver>(bob.loc);
            Waiting.NoSleep(board.screwdriver_act(bob, screwdriver));
            Assert.Equal(typeof(obj_machinery_chem_master_condimaster), board.build_path);
            Assert.Equal("CondiMaster 3000", Dm.str(board.name));
            Waiting.NoSleep(board.screwdriver_act(bob, screwdriver));
            Assert.Equal(typeof(obj_machinery_chem_master), board.build_path);
            Assert.Equal("ChemMaster 3000", Dm.str(board.name));
        });
    }

    [Fact]
    public void BreakingTheIcedAbductorLeavesTheAbductorsBody()
    {
        AudioRoom r = Room(15108);
        var ice = Place<obj_structure_fluff_iced_abductor>(r, 6, 5);
        r.In(() => qdel(ice));
        r.Step(Second);
        r.In(() =>
        {
            var body = Dm.locate<mob_living_carbon_human>(locate(6, 5, 1));
            Assert.NotNull(body);
            Assert.Equal(DEAD, body.stat);
        });
    }

    [Fact]
    public void TheMedicalWrenchRemembersItsSuiciderAndThePosterIsContraband()
    {
        AudioRoom r = Room(15109);
        mob_living_carbon_human bob = r.Join(5, 5, "Bob");
        r.In(() =>
        {
            var wrench = Dm.@new<obj_item_wrench_medical>(bob.loc);
            var lines = Waiting.NoSleep(wrench.examine(bob)) as DmList;
            Assert.DoesNotContain(Dm.each(lines).ToList(), l => Dm.str(l).Contains("reminds you of"));
            wrench.suicider = "Carl";
            lines = Waiting.NoSleep(wrench.examine(bob)) as DmList;
            Assert.Contains(Dm.each(lines).ToList(), l => Dm.str(l).Contains("For some reason, it reminds you of Carl."));

            var poster = Dm.@new<obj_item_poster_random_contraband>(bob.loc);
            Assert.True(HAS_TRAIT(poster, TRAIT_CONTRABAND));
            var garbage = Dm.@new<obj_effect_decal_cleanable_garbage>(locate(7, 5, 1));
            Assert.False(QDELETED(garbage));
        });
    }
}
