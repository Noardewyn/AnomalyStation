using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Boot;

/// <summary>
/// GLOB, /datum/controller/global_vars (code/controllers/globals.dm), written from the DM source: New registers it and runs
/// Initialize, which calls every InitGlobal proc of typesof(/datum/controller/global_vars/proc) in that order (the generated
/// ProcOrder, filtered to the translated procs, DV9), records the managed globals in gvars_datum_init_order and ends with
/// make_datum_reference_lists.
/// </summary>
public class GlobalVarsTests
{
    private static string[] Names(object list) => Dm.each(list).Cast<object>().Select(x => (string)x).ToArray();

    // GLOB's New queues QDEL_IN(exclude_these, 0), an SStimer timer: BootPieces creates SStimer first, as Master/New does.
    private static BootPieces WorldWithNewGlob(out datum_controller_global_vars glob)
    {
        var w = new BootPieces(notTranslatedThrows: false, createGlob: false);
        glob = Dm.@new<datum_controller_global_vars>();
        return w;
    }

    [Fact]
    public void NewRegistersGlobAndRunsTheInitGlobalProcsInProcOrder()
    {
        using BootPieces w = WorldWithNewGlob(out datum_controller_global_vars glob);

        Assert.Same(glob, GLOB);
        Assert.Equal("Global Variables", glob.name);
        string[] order = Names(glob.gvars_datum_init_order);
        // The managed globals of the boot package's files, each recorded once.
        foreach (string name in new[] { "clients", "admins", "emote_list", "areas_by_type", "game_version", "changelog_hash", "hub_visibility", "MAX_EX_DEVESTATION_RANGE", "DYN_EX_SCALE", "last_maptick_time", "AdminProcCallCount", "sortInstance" })
            Assert.Single(order, name);
        // GLOBAL_VAR's initializer is unmanaged (__DEFINES/__globals.dm:11): it records nothing.
        foreach (string name in new[] { "restart_counter", "host", "station_name", "AdminProcCaller", "LastAdminCalledProc" })
            Assert.DoesNotContain(name, order);
        // In the order of typesof(/datum/controller/global_vars/proc), the generated compile order.
        List<string> procOrder = ProcOrder.ProcsOf("/datum/controller/global_vars").Select(p => p["InitGlobal".Length..]).ToList();
        Assert.All(order, name => Assert.Contains(name, procOrder));
        Assert.Equal(order.OrderBy(procOrder.IndexOf).ToArray(), order);
        // The values the initializers set.
        Assert.Equal(0, glob.clients.Count);
        Assert.Equal(0, glob.areas_by_type.Count);
        Assert.Equal("/tg/Station 13", glob.game_version);
        Assert.Equal(14f, glob.MAX_EX_LIGHT_RANGE);
        Assert.Equal(0.5f, glob.DYN_EX_SCALE);
        Assert.Null(glob.restart_counter);
    }

    [Fact]
    public void ProtectedGlobalsAreListed()
    {
        using BootPieces w = WorldWithNewGlob(out _);
        DmList protectedVars = datum_controller_global_vars.gvars_datum_protected_varlist;
        foreach (string name in new[] { "gvars_datum_protected_varlist", "admins", "AdminProcCaller", "AdminProcCallCount", "LastAdminCalledTargetRef", "LastAdminCalledTarget", "LastAdminCalledProc", "MAX_EX_DEVESTATION_RANGE", "MAX_EX_FLAME_RANGE" })
            Assert.True(Dm.eq(protectedVars[name], TRUE), name);
        Assert.Null(protectedVars["clients"]);
        Assert.Null(protectedVars["DYN_EX_SCALE"]);
    }

    [Fact]
    public void NewCreatesTheScratchControllerAfterGlobSoThatIdsMatchTg()
    {
        using BootPieces w = WorldWithNewGlob(out datum_controller_global_vars glob);
        // var/datum/controller/exclude_these = new (controllers/globals.dm:16): the next id after GLOB's, before any datum
        // the InitGlobal procs create (sortInstance).
        Assert.True(w.World.Objects.TryGet(glob.dm_id + 1, out object scratch));
        Assert.Equal(typeof(datum_controller), scratch.GetType());
        Assert.True(GLOB.sortInstance.dm_id > glob.dm_id + 1);
    }

    [Fact]
    public void ASecondControllerCrashes()
    {
        using BootPieces w = WorldWithNewGlob(out datum_controller_global_vars glob);
        var e = Assert.Throws<DmCrashException>(() => Dm.@new<datum_controller_global_vars>());
        Assert.Equal("Multiple instances of global variable controller created", e.Message);
        Assert.Equal(13, e.DmLine);
        Assert.Same(glob, GLOB);
    }

    [Fact]
    public void DestroyAsksToBeLeftToTheGarbageCollector()
    {
        using BootPieces w = WorldWithNewGlob(out datum_controller_global_vars glob);
        Assert.True(Dm.eq(glob.Destroy(), QDEL_HINT_IWILLGC));
    }

    [Fact]
    public void MakeDatumReferenceListsRunsItsDeferredCallees()
    {
        using BootPieces w = WorldWithNewGlob(out _);
        IReadOnlyList<string> sites = DmWorldState.Of(w.World).DeferredSites;
        Assert.Equal(new[]
        {
            // W4.0: the deferred initializers of the GLOB lists of mobs.dm ran first; M2 P2 translated the blood types and species
            // lists (mobs.dm:72, :83, :89) and made GLOB.meta_gas_info, whose gas overlays came with the gas visuals (M4b
            // slice 3; they were a deferred leaf, gas_types.dm:25); gaps P3 translated the DNA blocks' (mobs.dm:154, :163).
            // M5 slice 2: the stack recipe lists (GLOB.metal_recipes, ...) are built in GLOB order; a recipe with a coloured
            // result leaves its base64 picture out (stack_recipe.dm:67). init_crafting_recipes (global_lists.dm:14) is
            // translated.
            "code/game/objects/items/stacks/stack_recipe.dm:67",
            "code/_globalvars/lists/keybindings.dm:2",
            // M9 slice B: init_emote_list (mobs.dm:125) is translated
            // M5 slice 8: init_crafting_recipes_atoms (global_lists.dm:104) is translated
        }, sites);
    }

    [Fact]
    public void NoAdminProcCallIsInProgressAtBoot()
    {
        using var w = new BootPieces();
        Assert.True(Dm.eq(IsAdminAdvancedProcCall(), FALSE));
        GLOB.AdminProcCaller = "someckey"; // no usr: usr?.client?.ckey is null
        Assert.True(Dm.eq(IsAdminAdvancedProcCall(), FALSE));
    }

    // M6 slice 6: an InitGlobal proc is run by its DM name (InitGlobal + the global's name, __DEFINES/__globals.dm), so a C#
    // name that is not in the generated ProcOrder is never called and its global stays null (InitGloball_body_zones did so).
    [Fact]
    public void EveryTranslatedInitGlobalProcHasItsDmName()
    {
        var dmNames = ProcOrder.Procs["/datum/controller/global_vars"].ToHashSet(StringComparer.Ordinal);
        string[] unknown = typeof(datum_controller_global_vars)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .Where(n => n.StartsWith("InitGlobal", StringComparison.Ordinal))
            .Select(n => n.EndsWith("__d0", StringComparison.Ordinal) ? n[..^"__d0".Length] : n)
            .Where(n => !dmNames.Contains(n))
            .Distinct()
            .ToArray();
        Assert.Empty(unknown);
    }
}
