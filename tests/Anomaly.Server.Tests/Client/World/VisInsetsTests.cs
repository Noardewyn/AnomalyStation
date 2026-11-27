using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>
/// M8x slice C (D210): a visual helper (tg's cryo occupant) is drawn into its holder's picture as one more overlay at the pixel offsets
/// its chain gives it now (VisInsets), without changing the run's tables.
/// </summary>
public class VisInsetsTests
{
    private sealed class Source : IAppearanceSource
    {
        public readonly Dictionary<TableRef, AppearanceEntry> Entries = new();
        public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry) => Entries.TryGetValue(reference, out entry);
        public string ResolveString(TableRef reference) => "s" + reference.Index;
        public string ResolveResource(TableRef reference) => "r" + reference.Index;
    }

    [Fact]
    public void The_holder_gets_the_helper_as_its_last_overlay_at_the_given_offsets_and_the_tables_stay_as_they_were()
    {
        var source = new Source();
        TableRef cover = new(3, false), cell = new(1, false), occupant = new(2, false);
        var cellEntry = new AppearanceEntry { Layer = 3f };
        cellEntry.Overlays.Add(cover);
        source.Entries[cell] = cellEntry;
        source.Entries[cover] = new AppearanceEntry { Layer = 4.3f };
        source.Entries[occupant] = new AppearanceEntry { Layer = 4.1f, PixelY = 22 };
        var insets = new VisInsets(source);

        TableRef low = insets.With(cell, occupant, 0, 22), high = insets.With(cell, occupant, 0, 24);
        Assert.NotEqual(cell, low);
        Assert.NotEqual(low, high);
        Assert.Equal(low, insets.With(cell, occupant, 0, 22)); // the same look again: the same copy, so the pictures' caches hold
        Assert.True(insets.TryGetAppearance(high, out AppearanceEntry composed));
        Assert.Equal(new[] { cover }, composed.Overlays.Take(1));
        Assert.Equal(2, composed.OverlayCount);
        Assert.True(insets.TryGetAppearance(composed.Overlays[1], out AppearanceEntry moved));
        Assert.Equal(24, moved.PixelY);
        Assert.Equal(4.1f, moved.Layer); // sorted among the holder's overlays by its own layer: under the cover

        Assert.Single(cellEntry.Overlays); // the run's entries are not changed
        Assert.Equal(22, source.Entries[occupant].PixelY);
        Assert.Equal(cell, insets.With(cell, new TableRef(9, false), 0, 22)); // an unknown helper: the holder as it is
    }

    [Fact]
    public void A_fading_helper_is_drawn_at_the_alpha_its_chain_gives_it_now()
    {
        var source = new Source();
        TableRef bed = new(1, false), mattress = new(2, false);
        source.Entries[bed] = new AppearanceEntry { Layer = 2.9f };
        source.Entries[mattress] = new AppearanceEntry { Layer = 2.9f, Alpha = 0 };
        var insets = new VisInsets(source);

        TableRef half = insets.With(bed, mattress, 0, 0, 128), full = insets.With(bed, mattress, 0, 0, 255);
        Assert.NotEqual(half, full);
        Assert.Equal(half, insets.With(bed, mattress, 0, 0, 128));
        Assert.True(insets.TryGetAppearance(half, out AppearanceEntry composed));
        Assert.True(insets.TryGetAppearance(composed.Overlays[^1], out AppearanceEntry moved));
        Assert.Equal(128, moved.Alpha);
        Assert.Equal(0, source.Entries[mattress].Alpha); // the run's entry keeps the chain's end
    }

    [Fact]
    public void An_aquarium_fish_is_drawn_where_its_chain_swims_it_and_turned_the_way_it_swims()
    {
        var source = new Source();
        TableRef tank = new(1, false), fish = new(2, false);
        source.Entries[tank] = new AppearanceEntry { Layer = 3f };
        source.Entries[fish] = new AppearanceEntry { Layer = 3.03f, PixelW = 4, PixelZ = -2 };
        var insets = new VisInsets(source);

        float[] flipped = { -1, 0, 0, 0, 1, 0 };
        TableRef left = insets.With(tank, fish, 0, 0, 255, null, 7, 1), right = insets.With(tank, fish, 0, 0, 255, flipped, 7, 1);
        Assert.NotEqual(left, right); // the turn is a look of its own
        Assert.Equal(right, insets.With(tank, fish, 0, 0, 255, new float[] { -1, 0, 0, 0, 1, 0 }, 7, 1));
        Assert.True(insets.TryGetAppearance(right, out AppearanceEntry composed));
        Assert.True(insets.TryGetAppearance(composed.Overlays[^1], out AppearanceEntry moved));
        Assert.Equal((7, 1), ((int)moved.PixelW, (int)moved.PixelZ));
        Assert.Equal(flipped, moved.Transform);
        Assert.True(insets.TryGetAppearance(left, out AppearanceEntry plain));
        Assert.True(insets.TryGetAppearance(plain.Overlays[^1], out AppearanceEntry unturned));
        Assert.Null(unturned.Transform);
        // without pixel_w and pixel_z the helper keeps its own (the cryo occupant)
        Assert.True(insets.TryGetAppearance(insets.With(tank, fish, 0, 0), out AppearanceEntry still));
        Assert.True(insets.TryGetAppearance(still.Overlays[^1], out AppearanceEntry own));
        Assert.Equal((4, -2), ((int)own.PixelW, (int)own.PixelZ));
        Assert.Equal(4, source.Entries[fish].PixelW); // the run's entry is not changed
    }
}
