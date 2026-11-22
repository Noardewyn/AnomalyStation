using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Gaps package S1 (D291) through the real server on MetaStation: a starving assistant finds the energy bar mapped on the station, picks it
/// up and eats it with clicks on himself (edible.dm: a bite a second until it is gone, then its wrapper, food_trash.dm, in his hand). The
/// client gets tg's eating message and eatfood.ogg, and his hunger display (screen_objects.dm:1152, the burger and its bar) changes: the
/// starving state's bar is redrawn fuller once the food is in his stomach.
/// </summary>
public class EatingGameplayTests
{
    private const int Second = 20;

    private readonly ITestOutputHelper _output;

    public EatingGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private sealed class Tables(ReplicaWorld replica) : IAppearanceSource
    {
        public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry) => replica.TryGetAppearance(reference, out entry);
        public string ResolveString(TableRef reference) => replica.ResolveString(reference);
        public string ResolveResource(TableRef reference) => replica.ResolveResource(reference);
    }

    [MetaServerFact]
    public void A_starving_player_eats_an_energy_bar_found_on_the_station_and_his_hunger_display_changes()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 61, TimeoutSteps = 6000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        long bootRuntimes = w.World.RuntimeErrorCount;
        RealHeadlessWorld.Player p = w.Join("Assistant");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        var chat = new List<ServerText>();
        p.Client.ServerTextReceived += chat.Add;

        // the station's energy bar (MetaStation.dmm), lying on its turf, edible
        obj_item_food_energybar bar = In(w, () =>
        {
            int z = (int)me.z;
            for (int x = 1; x <= Dm.world.maxx; x++)
                for (int y = 1; y <= Dm.world.maxy; y++)
                    if (Dm.locate<obj_item_food_energybar>(locate(x, y, z)) is obj_item_food_energybar found)
                        return found;
            return null;
        });
        Assert.NotNull(bar);
        Assert.NotNull(In(w, () => bar.GetComponent(typeof(datum_component_edible))));

        var display = In(w, () => me.hud_used.screen_objects[HUD_MOB_HUNGER] as atom_movable_screen_hunger);
        Assert.NotNull(display);
        using (w.World.Enter())
        {
            Waiting.NoSleep(me.set_nutrition(40));
            display.update_hunger_bar();
            Waiting.NoSleep(me.forceMove(bar.loc as atom));
        }
        w.Run(2 * Second);
        Assert.Equal(0f, In(w, () => Dm.num(display.state))); // HUNGER_STATE_STARVING
        Assert.True(replica.Screen.TryGetValue(display.dm_id, out ScreenEntry starving), "the hunger display reached the client");
        Assert.True(replica.TryGetAppearance(starving.Appearance, out AppearanceEntry look));
        Assert.Equal("hungerbar", replica.ResolveString(look.IconState));
        Assert.NotEmpty(look.Underlays); // the burger beside the bar
        Assert.NotEmpty(look.Overlays); // the bar itself (its vis_contents, flattened)

        // pick the bar up, then eat it with clicks on himself
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, bar.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Same(bar, In(w, () => me.get_active_held_item()));
        chat.Clear();
        heard.Clear();
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Entity, me.dm_id, 0, 0, 0, 16, 16, AimZone.Head), ClickButton.Left, ClickModifiers.None));
        w.Run(5 * Second);
        _output.WriteLine(string.Join("\n", chat.Select(c => c.Html)));

        Assert.True(In(w, () => QDELETED(bar)), "the bar is eaten");
        Assert.Contains(chat, c => c.Html.Contains("hungrily", StringComparison.Ordinal) && c.Html.Contains("energy bars", StringComparison.Ordinal));
        Assert.Contains(chat, c => c.Html.Contains("There is nothing left of", StringComparison.Ordinal));
        Assert.Contains(chat, c => c.Html.Contains("What the hell was that thing?!", StringComparison.Ordinal)); // energy bars are TOXIC to a human tongue (taste.dm)
        Assert.Contains(heard, h => h.File == "sound/items/eatfood.ogg");
        Assert.IsType<obj_item_trash_energybar>(In(w, () => me.get_active_held_item())); // the wrapper (food_trash.dm)
        Assert.True(In(w, () => Dm.num(((datum_reagents)((obj_item_organ_stomach)me.get_organ_slot(ORGAN_SLOT_STOMACH)).reagents).get_reagent_amount(typeof(datum_reagent_consumable_nutriment)))) > 0);

        // the display's bar on the client: a new picture, fuller than the starving one
        w.Run(Second);
        Assert.True(replica.Screen.TryGetValue(display.dm_id, out ScreenEntry fed));
        Assert.NotEqual(starving.Appearance, fed.Appearance);
        string iconRoot = Anomaly.Server.Tests.M3Maps.I1d.MetaPictureTests.IconRoot();
        if (iconRoot != null)
        {
            var compositor = new AppearanceCompositor(new IconLibrary(new DirectoryIconFiles(iconRoot)));
            var tables = new Tables(replica);
            Assert.True(compositor.TryCompose(tables, starving.Appearance, ByondDir.South, 0, out ComposedRaster before), string.Join("; ", compositor.Library.Problems));
            Assert.True(compositor.TryCompose(tables, fed.Appearance, ByondDir.South, 0, out ComposedRaster after), string.Join("; ", compositor.Library.Problems));
            int drawn = 0, changed = 0;
            for (int i = 0; i < before.Image.Pixels.Length; i += 4)
            {
                if (after.Image.Pixels[i + 3] != 0)
                    drawn++;
                if (before.Image.Pixels[i] != after.Image.Pixels[i] || before.Image.Pixels[i + 1] != after.Image.Pixels[i + 1] || before.Image.Pixels[i + 3] != after.Image.Pixels[i + 3])
                    changed++;
            }
            _output.WriteLine($"the display: {drawn} pixels drawn, {changed} changed by the meal");
            Assert.True(drawn > 50, "the display is drawn");
            Assert.True(changed > 5, "the bar is redrawn");
        }
        Assert.True(bootRuntimes == w.World.RuntimeErrorCount, w.Err.ToString());
    }
}
