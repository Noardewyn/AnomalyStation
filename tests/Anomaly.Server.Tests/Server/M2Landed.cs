using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests;

// M2 P6's test helper. The M2 packages P1-P5 are written in parallel against frozen contracts, and until they merge their
// bodies throw (`NotImplementedException("M2 Pn")` for new logic, `Dm.NotTranslated` for tg skeletons). Tests of P6's own code
// use fixtures of their own (docs/ai/specs/m2-ownership.md "Bodies throw until their owner lands"); the few tests that need the
// real thing (a real tg human on a real capsule over the real codec) carry [M2Fact], which turns into a skipped test while a
// package they need has not landed and becomes a real test by itself once the merges are in, so nobody has to remember to
// switch it on. A skipped M2Fact therefore says "not verified yet", never "passed".

/// <summary>The packages a test needs to have landed.</summary>
[Flags]
internal enum M2Needs
{
    None = 0,

    /// <summary>P5: the version-2 codec and replica (MotionInput, Action, the private block, MapGrid version 2).</summary>
    Protocol = 1,

    /// <summary>P4: the capsule controller and the predictor (<c>MotionStep</c>, <c>MotionPredictor</c>, <c>InputClock</c>).</summary>
    Motion = 2,

    /// <summary>P4: the locomotion seam (the phase, registration, frames, state).</summary>
    Locomotion = 4,

    /// <summary>P4: the click entry point (<c>ClientSeam.Click</c>, <c>ClickOn</c>, <c>UnarmedAttack</c>).</summary>
    Click = 8,

    /// <summary>P1 and P2: the living core and the human (the damage procs, <c>Life</c>, status procs run; no skeleton left on the path).</summary>
    Medical = 16,

    /// <summary>Everything the server, the bots and the client use end to end.</summary>
    All = Protocol | Motion | Locomotion | Click | Medical,
}

/// <summary>What of M2's other packages has landed in this build (probes, evaluated once).</summary>
internal static class M2Landed
{
    private static bool? _protocol, _motion, _locomotion, _click, _medical;

    public static bool Protocol => _protocol ??= Probe(() =>
    {
        var message = new MotionInputMessage();
        message.Frames.Add(MotionInput.Idle(1, 0));
        Codec.Encode(message);
        _ = new ReplicaWorld().Own;
    });

    public static bool Motion => _motion ??= Probe(() => _ = new MotionPredictor(MotionState.AtTile(2, 2, 1)));

    public static bool Locomotion => _locomotion ??= Probe(() => LocomotionSeam.IsInstalled(new World(1)));

    public static bool Click => _click ??= Probe(() => ClientSeam.Click(null, null, ""));

    // The living core and the human: a human's damage proc runs instead of a skeleton's Dm.NotTranslated.
    public static bool Medical => _medical ??= ProbeMedical();

    /// <summary>True when every package of <paramref name="needs"/> has landed.</summary>
    public static bool Has(M2Needs needs)
        => (!needs.HasFlag(M2Needs.Protocol) || Protocol)
        && (!needs.HasFlag(M2Needs.Motion) || Motion)
        && (!needs.HasFlag(M2Needs.Locomotion) || Locomotion)
        && (!needs.HasFlag(M2Needs.Click) || Click)
        && (!needs.HasFlag(M2Needs.Medical) || Medical);

    /// <summary>The skip reason for <paramref name="needs"/> (null when they have all landed).</summary>
    public static string Missing(M2Needs needs)
    {
        var missing = new List<string>();
        if (needs.HasFlag(M2Needs.Protocol) && !Protocol) missing.Add("P5 (protocol v2)");
        if (needs.HasFlag(M2Needs.Motion) && !Motion) missing.Add("P4 (motion core)");
        if (needs.HasFlag(M2Needs.Locomotion) && !Locomotion) missing.Add("P4 (locomotion seam)");
        if (needs.HasFlag(M2Needs.Click) && !Click) missing.Add("P4 (click entry point)");
        if (needs.HasFlag(M2Needs.Medical) && !Medical) missing.Add("P1/P2 (living core, human)");
        return missing.Count == 0 ? null : "waiting for " + string.Join(", ", missing) + " to merge (docs/ai/specs/m2-ownership.md)";
    }

    // A probe passes unless it reaches a body that still throws NotImplementedException ("M2 Pn"); any other failure of the
    // probe call itself (a null argument checked by the real body, for instance) means the body is there.
    private static bool Probe(Action probe)
    {
        try
        {
            probe();
            return true;
        }
        catch (NotImplementedException)
        {
            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }

    private static bool ProbeMedical()
    {
        try
        {
            var world = new World(5);
            // tg's strings/ files, as the server mounts them (gaps P3: setupGenetics' speech mutations read them)
            if (TgStrings.Locate(AppContext.BaseDirectory, System.IO.Directory.GetCurrentDirectory()) is string tgRoot)
                TgStrings.Mount(DmWorldState.Of(world).Files, tgRoot);
            TgWorld.Boot(world, _ => TestRoom.Build(8, 8));
            using (world.Enter())
            {
                DmWorldState.Of(world).NotTranslatedThrows = true;
                var human = Dm.@new<mob_living_carbon_human>(Builtins.locate(4, 4, 1));
                Waiting.NoSleep(human.adjust_oxy_loss(1f));
                Waiting.NoSleep(human.death());
            }
            return true;
        }
        catch (DmNotTranslatedException e) when (e.What.Contains("(skeleton)", StringComparison.Ordinal))
        {
            return false;
        }
        catch (NotImplementedException)
        {
            return false;
        }
        catch (Exception)
        {
            return true; // something else failed on the way: the skeletons are gone, the test that needs it will say what
        }
    }
}

/// <summary>A fact that is skipped while a package it needs has not landed (see the file's comment).</summary>
internal sealed class M2FactAttribute : FactAttribute
{
    public M2FactAttribute(M2Needs needs)
    {
        string missing = M2Landed.Missing(needs);
        if (missing != null) Skip = missing;
    }
}

/// <summary>A theory that is skipped while a package it needs has not landed (see the file's comment).</summary>
internal sealed class M2TheoryAttribute : TheoryAttribute
{
    public M2TheoryAttribute(M2Needs needs)
    {
        string missing = M2Landed.Missing(needs);
        if (missing != null) Skip = missing;
    }
}
