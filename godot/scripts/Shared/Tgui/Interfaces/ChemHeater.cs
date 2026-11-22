using System.Linq;
using Anomaly.Client.Shared.Tgui.Interfaces.Common;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ChemHeater.tsx (gaps package P5). The window reads the
// reaction chamber's ui_data (chem_heater.dm:206: the target and current temperature, the power, the buffers, the active reactions
// with their danger and quality) and sends power, temperature, disp_vol, acidBuffer, basicBuffer, eject and insert. The kit has no
// CSS transform: the overheat thermometer is drawn at its plain size; tgui-core's AnimatedNumber renders as its text. Original
// copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class ChemHeater
{
    // tg: constants.ts:44  COLORS.reagent.acidicbuffer, basicbuffer
    private const string AcidicBufferColor = "#fbc314";
    private const string BasicBufferColor = "#3853a4";

    // tg: ChemHeater.tsx:56  ReactionDisplay (exported: the window's reaction table)
    public static UiNode ReactionDisplay(UiValue beaker, double isFlashing, IReadOnlyList<UiValue> activeReactions, bool highQualityDisplay, bool highDangerDisplay)
    {
        double ph = beaker.Num("pH");
        UiNode buttons = new UiNode(UiKind.Stack) { Key = "ph", Align = "center" }.With(
            new UiNode(UiKind.StackItem) { Color = "label" }.With(new UiNode(UiKind.Box) { Text = "pH: " + TguiFormat.JsNumber(TguiFormat.Round(ph, 3)) }),
            new UiNode(UiKind.StackItem).With(new UiNode(UiKind.RoundGauge)
            {
                Key = "gauge",
                Size = 1.6,
                Value = ph,
                MinValue = 0,
                MaxValue = 14,
                AlertAfter = isFlashing,
                Format = _ => "",
                Ranges = new[]
                {
                    ("red", -0.22, 1.5), ("orange", 1.5, 3.0), ("yellow", 3.0, 4.5), ("olive", 4.5, 5.0), ("good", 5.0, 6.0),
                    ("green", 6.0, 8.5), ("teal", 8.5, 9.5), ("blue", 9.5, 11.0), ("purple", 11.0, 12.5), ("violet", 12.5, 14.0),
                },
            }));

        UiNode section = new UiNode(UiKind.Section) { Key = "reactions", Title = "Reactions", Buttons = buttons };
        if (activeReactions.Count == 0)
            return section.With(new UiNode(UiKind.Box) { Key = "none", Color = "label", Text = "No active reactions." });

        UiNode table = new UiNode(UiKind.Table) { Key = "table" }.With(new UiNode(UiKind.TableRow) { Key = "head" }.With(
            new UiNode(UiKind.TableCell) { Key = "r", Bold = true, Color = "label", Text = "Reaction" },
            new UiNode(UiKind.TableCell) { Key = "s", Bold = true, Color = "label", Text = !highQualityDisplay ? "Status" : "Reaction quality" },
            new UiNode(UiKind.TableCell) { Key = "t", Bold = true, Color = "label", Text = "Target" }));
        int row = 0;
        foreach (UiValue reaction in activeReactions)
        {
            bool danger = reaction.Truthy("danger"), overheat = reaction.Truthy("overheat");
            string target = TguiFormat.JsNumber(reaction.Num("targetVol")) + "u";
            UiNode status = !highQualityDisplay
                ? new UiNode(UiKind.Icon) { Key = "icon", Icon = danger ? "exclamation-triangle" : "spinner", Color = danger ? "red" : null, Spin = !danger }
                : new UiNode(UiKind.RoundGauge)
                {
                    Key = "quality",
                    Size = 1.3,
                    Value = reaction.Num("quality"),
                    MinValue = 0,
                    MaxValue = 1,
                    AlertAfter = reaction.Num("purityAlert"),
                    Format = _ => "",
                    Ranges = new[]
                    {
                        ("red", 0.0, reaction.Num("minPure")), ("orange", reaction.Num("minPure"), reaction.Num("inverse")),
                        ("yellow", reaction.Num("inverse"), 0.8), ("green", 0.8, 1.0),
                    },
                };
            UiNode targetCell = new UiNode(UiKind.TableCell) { Key = "target", Width = "100px" };
            if (highDangerDisplay)
            {
                if (overheat)
                    targetCell.With(new UiNode(UiKind.Icon) { Key = "hot", Icon = "thermometer-full", Color = "red" });
                targetCell.With(new UiNode(UiKind.ProgressBar)
                {
                    Key = "progress",
                    Value = reaction.Num("reactedVol"),
                    MinValue = 0,
                    MaxValue = reaction.Num("targetVol"),
                    TextAlign = "center",
                    Width = 8,
                    Color = overheat ? "red" : "label",
                    Text = target,
                });
            }
            else
                targetCell.With(new UiNode(UiKind.Box) { Key = "vol", Color = danger ? "red" : null, Text = target });
            // tg: :129  <Table.Row key="reactions">: every row has the same key; the port numbers them
            table.With(new UiNode(UiKind.TableRow) { Key = "reaction:" + row++ }.With(
                new UiNode(UiKind.TableCell) { Key = "name", Width = "60px", Color = danger ? "red" : null, Text = reaction.Str("name") },
                new UiNode(UiKind.TableCell) { Key = "status", Width = "100px" }.With(status),
                targetCell));
        }
        return section.With(table);
    }

    // tg: ChemHeater.tsx:200
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool isActive = data.Truthy("isActive");
        UiValue beaker = data.Get("beaker");
        bool isBeakerLoaded = beaker != null && beaker.Kind != UiValueKind.Null;
        double acidicBufferVol = data.Num("acidicBufferVol"), basicBufferVol = data.Num("basicBufferVol");
        double upgradeLevel = data.Num("upgradeLevel");

        UiNode Inject(string key, string act, bool disabled) => new UiNode(UiKind.Button)
        {
            Key = key,
            Icon = "syringe",
            Disabled = disabled,
            Tooltip = "Inject",
            OnClick = () => c.Act(act, ("target", 1)),
        };
        UiNode Draw(string key, string act, bool disabled) => new UiNode(UiKind.Button)
        {
            Key = key,
            Icon = "upload",
            Tooltip = "Draw all",
            Disabled = disabled,
            OnClick = () => c.Act(act, ("target", -100)),
        };

        UiNode controls = new UiNode(UiKind.Section)
        {
            Key = "controls",
            Title = "Controls",
            Buttons = new UiNode(UiKind.Button)
            {
                Key = "power",
                Icon = isActive ? "power-off" : "times",
                Selected = isActive,
                Text = isActive ? "On" : "Off",
                OnClick = () => c.Act("power"),
            },
        }.With(new UiNode(UiKind.Table) { Key = "table" }.With(
            new UiNode(UiKind.TableRow) { Key = "head" }.With(
                new UiNode(UiKind.TableCell) { Key = "heat", Bold = true, Color = "label", Text = "Heat" },
                new UiNode(UiKind.TableCell) { Key = "blank1" },
                new UiNode(UiKind.TableCell) { Key = "buffers", Bold = true, Color = "label", Text = "Buffers" },
                new UiNode(UiKind.TableCell) { Key = "blank2" },
                new UiNode(UiKind.TableCell) { Key = "vol" }.With(new UiNode(UiKind.NumberInput)
                {
                    Key = "dispVol",
                    Width = "45px",
                    Unit = "u",
                    Step = 1,
                    StepPixelSize = 3,
                    Value = data.Num("dispenseVolume"),
                    MinValue = 1,
                    MaxValue = 10,
                    OnChange = value => c.Act("disp_vol", ("target", value)),
                })),
            new UiNode(UiKind.TableRow) { Key = "acid" }.With(
                new UiNode(UiKind.TableCell) { Key = "label", Color = "label", Text = "Target:" },
                new UiNode(UiKind.TableCell) { Key = "target" }.With(new UiNode(UiKind.NumberInput)
                {
                    Key = "targetTemp",
                    Width = "65px",
                    Unit = "K",
                    Step = 10,
                    StepPixelSize = 3,
                    Value = TguiFormat.Round(data.Num("targetTemp"), 0.1),
                    MinValue = 0,
                    MaxValue = 1000,
                    OnChange = value => c.Act("temperature", ("target", value)),
                }),
                new UiNode(UiKind.TableCell) { Key = "acidic", Color = "label", Text = "Acidic:" },
                new UiNode(UiKind.TableCell) { Key = "inject" }.With(Inject("injectAcid", "acidBuffer", acidicBufferVol == 0)),
                new UiNode(UiKind.TableCell) { Key = "amount", Color = AcidicBufferColor, TextAlign = "center", Text = TguiFormat.JsNumber(acidicBufferVol) + "u" },
                new UiNode(UiKind.TableCell) { Key = "draw" }.With(Draw("drawAcid", "acidBuffer", acidicBufferVol == 100))),
            new UiNode(UiKind.TableRow) { Key = "base" }.With(
                new UiNode(UiKind.TableCell) { Key = "label", Color = "label", Text = "Reading:" },
                new UiNode(UiKind.TableCell) { Key = "readingCell" }.With(new UiNode(UiKind.Box)
                {
                    Key = "reading",
                    Width = "60px",
                    TextAlign = "right",
                    Text = isBeakerLoaded ? TguiFormat.ToFixed(data.Num("currentTemp")) + " K" : "—",
                }),
                new UiNode(UiKind.TableCell) { Key = "basic", Color = "label", Text = "Basic:" },
                new UiNode(UiKind.TableCell) { Key = "inject" }.With(Inject("injectBase", "basicBuffer", basicBufferVol == 0)),
                new UiNode(UiKind.TableCell) { Key = "amount", Color = BasicBufferColor, TextAlign = "center", Text = TguiFormat.JsNumber(basicBufferVol) + "u" },
                new UiNode(UiKind.TableCell) { Key = "draw" }.With(Draw("drawBase", "basicBuffer", basicBufferVol == 100)))));

        // tg: ChemHeater.tsx:218  <Window width={350} height={350}><Window.Content scrollable>
        return c.Window(350, 350, null,
            controls,
            isBeakerLoaded ? ReactionDisplay(beaker, data.Num("isFlashing"), data.Arr("activeReactions"), upgradeLevel >= 4, upgradeLevel >= 2) : null,
            BeakerDisplay.SectionDisplay(c, isBeakerLoaded ? beaker : null, showPH: false, showInsertButton: true, hasBeakerInHand: data.Truthy("hasBeakerInHand")));
    }
}
