using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package DE1 on the converted MetaStation: its tape recorders and tape, N-spect scanners, gavels and gavel blocks, red phones,
/// pillows, skub, dice and dice bags, shooting targets and Cargonia's banner boot live (on the floor, on the tables, in the lockers
/// and the boxes): the recorders hold their tapes, the dice show the face they rolled, the dice bags hold their set, the pillows
/// picked a pattern, the scanners hold a cell, with no runtime from the files the package translated.
/// </summary>
public class DevicesDE1MetaTests : IClassFixture<MetaWorldFixture>
{
    private readonly MetaWorldFixture _meta;
    private readonly ITestOutputHelper _output;

    public DevicesDE1MetaTests(MetaWorldFixture meta, ITestOutputHelper output)
    {
        _meta = meta;
        _output = output;
    }

    private static readonly string[] Files =
    {
        "taperecorder", "inspector", "courtroom", "pillow", "skub", "dice", "shooting_range", "religion", "melee/misc", "kneejerk",
        "bumpattack", "item_killsound", "death_sound", "replace_sound", "material_slots/generic", "claymore",
    };

    // what a turf holds, and what that holds, all the way down (the lockers, the boxes, the bags)
    private static IEnumerable<atom> Walk(IEnumerable<atom> things)
    {
        foreach (atom thing in things)
        {
            yield return thing;
            foreach (atom inner in Walk(Dm.each<atom>(thing.contents)))
                yield return inner;
        }
    }

    [MetaFact]
    public void Metas_devices_and_household_odds_boot_live()
    {
        using (_meta.World.Enter())
        {
            var counts = new Dictionary<string, int>();
            int inert = 0, tapeless = 0, faceless = 0, shortBags = 0, plainPillows = 0, cellless = 0;
            for (int y = 1; y <= 255; y++)
            {
                for (int x = 1; x <= 255; x++)
                {
                    foreach (atom thing in Walk(locate(x, y, 1).dm_held))
                    {
                        string kind = thing switch
                        {
                            obj_item_taperecorder => "tape recorder",
                            obj_item_tape => "tape",
                            obj_item_inspector => "inspector",
                            obj_item_gavelhammer => "gavel",
                            obj_item_gavelblock => "gavel block",
                            obj_item_phone => "phone",
                            obj_item_pillow => "pillow",
                            obj_item_skub => "skub",
                            obj_item_storage_dice => "dice bag",
                            obj_item_dice => "die",
                            obj_item_target => "target",
                            obj_item_banner => "banner",
                            _ => null,
                        };
                        if (kind == null)
                            continue;
                        counts[kind] = counts.GetValueOrDefault(kind) + 1;
                        if (InertSeam.IsInert(thing))
                            inert++;
                        switch (thing)
                        {
                            case obj_item_taperecorder recorder when recorder is not obj_item_taperecorder_empty && recorder.mytape == null:
                                tapeless++;
                                break;
                            case obj_item_dice die when die is not (obj_item_dice_d100 or obj_item_dice_eightbd20 or obj_item_dice_fourdd6): // (their update_icon_blocker keeps the face off)
                                if (!Dm.truthy(die.result) || !Dm.each(die.overlays).Any(o => o is IDmAppearance a && a.icon_state == Dm.text("[]-[]", die.icon_state, die.result)))
                                    faceless++;
                                break;
                            case obj_item_storage_dice bag when Dm.each<obj_item_dice>(bag.contents).Count() < 5:
                                shortBags++;
                                break;
                            case obj_item_pillow pillow when pillow.variation < 1 || pillow.variation > 4 || pillow.icon_state != Dm.text("pillow_[]_t", pillow.variation):
                                plainPillows++;
                                break;
                            case obj_item_inspector inspector when inspector.cell is not obj_item_stock_parts_power_store_cell:
                                cellless++;
                                break;
                        }
                    }
                }
            }
            var runtimes = _meta.Log.Runtimes.Where(e => Files.Any(f => (e.ToString() ?? "").Contains(f, StringComparison.Ordinal))).ToList();
            _output.WriteLine("Meta: " + string.Join(", ", counts.OrderBy(c => c.Key).Select(c => c.Value + " " + c.Key))
                + $"; {inert} inert, {tapeless} recorders without a tape, {faceless} dice without their face, {shortBags} short dice bags,"
                + $" {plainPillows} pillows without a pattern, {cellless} scanners without a cell; DE1 runtimes {runtimes.Count}");
            foreach (var group in runtimes.GroupBy(e => e.Message.Length > 140 ? e.Message[..140] : e.Message))
                _output.WriteLine($"  runtime x{group.Count()}: {group.Key}");
            Assert.True(counts.GetValueOrDefault("tape recorder") >= 5, "too few tape recorders");
            Assert.True(counts.GetValueOrDefault("tape") >= counts.GetValueOrDefault("tape recorder"), "too few tapes");
            Assert.True(counts.GetValueOrDefault("phone") >= 5, "too few red phones");
            Assert.True(counts.GetValueOrDefault("pillow") >= 3, "too few pillows");
            Assert.True(counts.GetValueOrDefault("target") >= 10, "too few shooting targets");
            Assert.True(counts.GetValueOrDefault("die") >= 10, "too few dice");
            foreach (string one in new[] { "inspector", "gavel", "gavel block", "skub", "dice bag", "banner" })
                Assert.True(counts.GetValueOrDefault(one) >= 1, "no " + one);
            Assert.Equal(0, inert);
            Assert.Equal(0, tapeless);
            Assert.Equal(0, faceless);
            Assert.Equal(0, shortBags);
            Assert.Equal(0, plainPillows);
            Assert.Equal(0, cellless);
            Assert.Empty(runtimes);
        }
    }
}
