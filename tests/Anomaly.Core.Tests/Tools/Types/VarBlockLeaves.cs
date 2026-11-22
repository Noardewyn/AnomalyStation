using Anomaly.Core.Tg;

namespace Anomaly.Core.Tests.Tools.Types;

/// <summary>
/// The hand-written classes that had no var block at the M3 start (M2 skeletons and procs-only classes) get tg's var block
/// from the types converter (rules 10.2, docs/ai/specs/types-converter.md 3.2). With tg's data their creation now reaches a
/// leaf that a later package translates: a light (<c>update_light</c>, M3), materials (<c>initialize_materials</c>, M4a),
/// an atom colour (M3), an item action (the M2 P2 skeleton), a visual organ (M9), a door's emissive blocker (DV12). The
/// creation tests of M2 (create_and_destroy, the lifecycle and table tests) expect exactly this leaf for exactly these
/// types: a type that stops reaching its leaf fails too, so its row is removed when the leaf is translated.
/// GAP(M3 P1 -> lead): rows owned by the packages that translate each leaf (P3 lights and doors, P4 tables and colours, M4a).
/// The M3 contracts' phase B added the door, window, grille, railing, flap and table subtypes whose classes got the M3 skeletons;
/// M3 P4's inert switch removed them again (inert types skip the leaves).
/// </summary>
internal static class VarBlockLeaves
{
    public static readonly IReadOnlyDictionary<Type, string> Leaves = new Dictionary<Type, string>
    {
        // M3 P3 made initialize_materials (atom_materials.dm:32) a deferred leaf until M4a (the lead's assignment of
        // 2026-10-08): the fishing rod, meat slab, ID card, tank, health analyzer, radio, PDA and table rows are gone.
        // M4c slice 1 made add_item_action (items.dm:316) a deferred leaf until M4c slice 3: the tank, duffel bag and RCL rows
        // are gone (removed in M4c slice 2).
        // M3 P4 translated add_atom_colour (game/atom/atom_color.dm): the pipe cleaner coil, thermoplastic and blood rows are gone.
        // M6 slice 5 listed the xeno and larva gib spawners, whose fake alien (gibspawner.dm:24) the port did not have; gaps X5
        // made the aliens live: their rows are gone (the robot spawner's fake silicon came with the bots, gaps C3).
        // M3 P3 translated update_light (lighting_atom.dm:44) and deferred the EMISSIVE_BLOCK_UNIQUE blocker (DV12): the
        // megafauna and door rows are gone.
        // M3 contracts (phase B) listed the door, window, grille, railing, flap and table subtypes whose classes got the M3
        // skeletons here (initialize_materials, EMISSIVE_BLOCK_UNIQUE). M3 P4 switched the inert seam on: those types are
        // inert (D112) and their init skips both leaves (types-converter.md 7), so their rows are gone. A package that makes
        // one of them live adds its row back while its leaf is untranslated.
        // Gaps P4a: the coins are live; a mythril coin's material gives an item fantasy affixes (basemats.dm:541,
        // /datum/component/fantasy), a not-translated leaf until the fantasy affixes.
        [typeof(obj_item_coin_mythril)] = "fantasy affixes",
        // Gaps SK1: the mythril sheet is live with its recipes, and its material reaches the same leaf
        [typeof(obj_item_stack_sheet_mineral_mythril)] = "fantasy affixes",
    };

    /// <summary>
    /// The types whose bad Initialize call is tg's own, which tg's InitAtom records the same way (atoms_initializing_EXPENSIVE.dm:42)
    /// and tg's create_and_destroy does not check.
    /// </summary>
    public static readonly IReadOnlyDictionary<Type, string> TgOwnBadInitializeCalls = new Dictionary<Type, string>
    {
        // gaps S3: made without an employee's name the contract returns INITIALIZE_HINT_QDEL before its ..() (contract.dm:12)
        [typeof(obj_item_paper_employment_contract)] = "BAD_INIT_DIDNT_INIT",
        // syndicate G3a: the crash below stops the kit's Initialize
        [typeof(obj_item_storage_box_syndie_kit_induction_kit_clown)] = "BAD_INIT_NO_HINT",
        // gaps GU1: made without its backpack, the laser gatling gun returns INITIALIZE_HINT_QDEL before its ..() (laser_gatling.dm:105)
        [typeof(obj_item_gun_energy_minigun)] = "BAD_INIT_DIDNT_INIT",
        // syndicate N1c: made without the console that creates it, a remote camera eye returns INITIALIZE_HINT_QDEL before its ..() (remote.dm:35)
        [typeof(mob_eye_camera_remote)] = "BAD_INIT_DIDNT_INIT",
        // silicons SI4: so does the holocall's eye (holocall.dm:1), made without the holopad a call is answered at
        [typeof(mob_eye_camera_remote_holo)] = "BAD_INIT_DIDNT_INIT",
        // gaps OD2: made without a board (the plain flatpack, whose subtypes and map edits name one), a flatpack returns
        // INITIALIZE_HINT_QDEL before its ..() (flatpacks.dm:18-19, tg's "how")
        [typeof(obj_item_flatpack)] = "BAD_INIT_DIDNT_INIT",
        // gaps BR1: made without the domain it was completed in, the decrypted curiosity returns INITIALIZE_HINT_QDEL before its ..() (lockbox.dm:337-339)
        [typeof(obj_item_storage_lockbox_bitrunning_decrypted)] = "BAD_INIT_DIDNT_INIT",
    };

    /// <summary>The runtimes that are tg's own stack traces of a type, and the text that names them.</summary>
    public static readonly IReadOnlyDictionary<Type, string> TgOwnRuntimes = new Dictionary<Type, string>
    {
        // syndicate G3a: the circus kit's toy pistol loads riot darts heavier than its magazine's metal, so the magazine's material
        // count goes negative (_box_magazine.dm:54, atom_materials.dm:46; ImplantKitTests skips it too)
        [typeof(obj_item_storage_box_syndie_kit_induction_kit_clown)] = "materials with non-positive values",
    };

    /// <summary>
    /// SSatoms records a type whose Initialize reached a leaf (and so returned no hint) in <c>BadInitializeCalls</c>: only
    /// the listed types may be there.
    /// </summary>
    public static void AssertOnlyListedBadInitializeCalls(Anomaly.Core.DmShim.DmList bad)
    {
        var unlisted = new List<object>();
        foreach (object t in Anomaly.Core.DmShim.Dm.each(bad))
            if (!(t is Type type && (Leaves.ContainsKey(type) || TgOwnBadInitializeCalls.ContainsKey(type)))) unlisted.Add(t);
        Assert.True(unlisted.Count == 0, "bad Initialize calls of types that VarBlockLeaves does not list: " + string.Join(", ", unlisted));
    }

    /// <summary>The runtimes of creating <paramref name="type"/> that are not its listed leaf, plus a note when the listed leaf did not happen.</summary>
    public static List<string> Unexpected(Type type, IReadOnlyList<string> runtimes)
    {
        if (TgOwnRuntimes.TryGetValue(type, out string own))
            runtimes = runtimes.Where(r => !r.Contains(own, StringComparison.Ordinal)).ToList();
        bool listed = Leaves.TryGetValue(type, out string leaf);
        var unexpected = runtimes.Where(r => !(listed && r.Contains(leaf, StringComparison.Ordinal) && r.Contains("not translated yet", StringComparison.Ordinal)))
            .Select(r => type.Name + ": " + r.Split('\n')[0]).ToList();
        if (listed && !runtimes.Any(r => r.Contains(leaf, StringComparison.Ordinal)))
            unexpected.Add(type.Name + ": no longer reaches its listed leaf " + leaf + " (remove its row from VarBlockLeaves)");
        return unexpected;
    }
}
