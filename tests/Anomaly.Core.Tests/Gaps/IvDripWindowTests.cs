using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package G1: the IV drip's window (iv_drip.dm:63-135) in a booted toy room. A click on a drip with an empty hand opens tg's
/// "IVDrip" window with the static rate limits and the data of the drip (the container and its volume and colour, the mode, the patient);
/// its acts change the rate (clamped and rounded to the step), flip the mode, eject the container and detach the needle; the saline
/// drip has its own storage, so there is nothing to eject.
/// </summary>
public class IvDripWindowTests
{
    private static (AudioRoom R, mob_living_carbon_human Alice, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(12, 9, seed: seed);
        r.In(() =>
        {
            for (int x = 2; x <= 11; x++)
            {
                for (int y = 2; y <= 8; y++)
                    Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_floor_iron), Dm.list(typeof(turf_open_space), typeof(turf_open_floor_plating))));
            }
        });
        r.Step(Second);
        mob_living_carbon_human alice = r.Join(5, 5, "Alice");
        mob_living_carbon_human bob = r.Dummy(6, 5);
        return (r, alice, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item => r.In(() =>
    {
        var item = Dm.@new<T>(who.loc);
        Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_active_hand(item))));
        return item;
    });

    private static string OpenWindow(AudioRoom r, mob who, atom drip, out JsonElement payload)
    {
        Take(who);
        Click(r, who, drip);
        payload = LastUpdate(Take(who), out string window);
        Assert.Equal("IVDrip", InterfaceOf(payload));
        return window;
    }

    [Fact]
    public void AClickOpensTheWindowWithTgsStaticLimitsAndTheDripsData()
    {
        var (r, alice, bob) = Room(9801);
        var drip = Place<obj_machinery_iv_drip>(r, 5, 6);
        var pack = Hold<obj_item_reagent_containers_blood_o_minus>(r, alice);
        Click(r, alice, drip);
        r.In(() => Assert.Same(pack, drip.reagent_container));
        r.Step(Second);

        string window = OpenWindow(r, alice, drip, out JsonElement payload);
        Assert.Equal(UI_INTERACTIVE, payload.GetProperty("config").GetProperty("status").GetInt32());
        JsonElement statics = payload.GetProperty("static_data");
        Assert.Equal((0.01, 5, 0), (statics.GetProperty("transferStep").GetDouble(), statics.GetProperty("maxTransferRate").GetDouble(), statics.GetProperty("minTransferRate").GetDouble()));
        JsonElement data = payload.GetProperty("data");
        Assert.Equal((0, 1, 1, 1, 1, 0), (data.GetProperty("hasInternalStorage").GetInt32(), data.GetProperty("hasContainer").GetInt32(), data.GetProperty("canRemoveContainer").GetInt32(),
            data.GetProperty("mode").GetInt32(), data.GetProperty("canDraw").GetInt32(), data.GetProperty("hasObjectAttached").GetInt32()));
        Assert.Equal(5, data.GetProperty("transferRate").GetDouble());
        float volume = r.In(() => Dm.num(((datum_reagents)pack.reagents).total_volume));
        Assert.Equal(volume, data.GetProperty("containerCurrentVolume").GetSingle(), 2);
        Assert.Equal(r.In(() => Dm.num(((datum_reagents)pack.reagents).maximum_volume)), data.GetProperty("containerMaxVolume").GetSingle(), 2);
        Assert.Equal("#AA1010", data.GetProperty("containerReagentColor").GetString()); // blood's colour, mixed by tg's mix_color_from_reagents
        Assert.False(data.TryGetProperty("objectName", out _));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheWindowsActsChangeTheRateTheModeAndDetachAndEject()
    {
        var (r, alice, bob) = Room(9802);
        var drip = Place<obj_machinery_iv_drip>(r, 5, 6);
        var pack = Hold<obj_item_reagent_containers_blood_o_minus>(r, alice);
        Click(r, alice, drip);
        r.Step(Second);
        string window = OpenWindow(r, alice, drip, out _);

        // the rate is clamped to 0..5 and rounded to a hundredth
        Act(r, alice, window, "changeRate", ("rate", 2.345));
        r.In(() => Assert.Equal(2.35f, drip.transfer_rate, 3));
        Assert.Equal(2.35, Data(r, alice, out _).GetProperty("transferRate").GetDouble(), 3);
        Act(r, alice, window, "changeRate", ("rate", 9));
        r.In(() => Assert.Equal(5f, drip.transfer_rate));
        Act(r, alice, window, "changeRate", ("rate", 0));
        r.In(() => Assert.Equal(0f, drip.transfer_rate));
        Take(alice);

        // the needle on Bob: the window names him, the direction button can draw blood from a mob
        r.Command(() => ClientSeam.Drop(alice.client, drip, bob, "icon-x=16;icon-y=16;left=1;button=left", "icon-x=16;icon-y=16;left=1;button=left", 2));
        r.Step(Second + 2);
        r.In(() => Assert.Same(bob, drip.attachment.attached_to));
        r.Step(Second); // SStgui's fire sends the window's data again
        JsonElement data = LastUpdate(Take(alice), out _).GetProperty("data");
        Assert.Equal((1, 1), (data.GetProperty("hasObjectAttached").GetInt32(), data.GetProperty("canDraw").GetInt32()));
        Assert.Equal(r.In(() => (string)bob.name), data.GetProperty("objectName").GetString());

        // "Draining": the mode flips to taking blood (the Toggle Mode verb, run as the client's call)
        Act(r, alice, window, "changeMode");
        r.In(() => Assert.Equal(IV_TAKING, drip.mode));
        Act(r, alice, window, "changeMode");
        r.In(() => Assert.Equal(IV_INJECTING, drip.mode));

        Act(r, alice, window, "detach");
        r.In(() => Assert.Null(drip.attachment));
        Act(r, alice, window, "eject");
        r.Step(4);
        r.In(() =>
        {
            Assert.Null(drip.reagent_container);
            Assert.NotSame(drip, pack.loc);
        });
        data = Data(r, alice, out _);
        Assert.Equal(0, data.GetProperty("hasContainer").GetInt32());
        Assert.False(data.TryGetProperty("containerCurrentVolume", out _));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASalineDripHasItsOwnStorageSoThereIsNothingToEject()
    {
        var (r, alice, _) = Room(9803);
        var drip = Place<obj_machinery_iv_drip_saline>(r, 5, 6);
        string window = OpenWindow(r, alice, drip, out JsonElement payload);
        JsonElement data = payload.GetProperty("data");
        Assert.Equal((1, 0, 0), (data.GetProperty("hasInternalStorage").GetInt32(), data.GetProperty("hasContainer").GetInt32(), data.GetProperty("canRemoveContainer").GetInt32()));
        Assert.True(data.GetProperty("containerMaxVolume").GetSingle() > 0);
        Assert.True(data.GetProperty("containerCurrentVolume").GetSingle() > 0, "the saline drip starts with its saline");
        Act(r, alice, window, "eject");
        r.In(() => Assert.NotNull(drip.reagents));
        r.AssertNoRuntimes();
    }
}
