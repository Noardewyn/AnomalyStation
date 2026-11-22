using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/MassSpec.tsx (gaps package P5). The window reads the
// mass spectrometer's ui_data (chem_mass_spec.dm:286: the mass range, the processing state and its time left, the input and output
// beakers' reagents with their mass, purity and type) and sends activate, the three sliders, eject1/2 and insert1/2. tg draws the
// spectrum as an SVG: the port draws it with the kit's Chart (each peak a filled triangle, the selected mass range a faint blue band,
// the axis numbers in a row under it and the absorbance scale beside it); the Dimmer of a running purification is a notice over the
// window's top. Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class MassSpec
{
    // tg: MassSpec.tsx:39
    private const double GRAPH_MAX_WIDTH = 1060;
    private const double GRAPH_MAX_HEIGHT = 250;

    // tg: MassSpec.tsx:42
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool processing = data.Truthy("processing");
        double lowerRange = data.Num("lowerRange"), upperRange = data.Num("upperRange");
        double graphUpperRange = data.Num("graphUpperRange"), eta = data.Num("eta"), peakHeight = data.Num("peakHeight");
        UiValue beaker1 = data.Get("beaker1"), beaker2 = data.Get("beaker2");
        bool hasBeaker1 = beaker1?.Truthy ?? false, hasBeaker2 = beaker2?.Truthy ?? false;
        bool hasBeakerInHand = data.Truthy("hasBeakerInHand");
        double centerValue = (lowerRange + upperRange) / 2;
        bool beaker1HasContents = hasBeaker1 && beaker1.Arr("contents").Count > 0;

        UiNode Insert(string act) => new UiNode(UiKind.Button)
        {
            Key = act,
            Icon = "eject",
            Tooltip = !hasBeakerInHand ? "You need to hold a container in your hand" : null,
            Text = "Insert",
            OnClick = () => c.Act(act),
        };
        UiNode BeakerButtons(UiValue beaker, bool has, string eject, string insert) => has
            ? new UiNode(UiKind.Fragment).With(
                new UiNode(UiKind.Box) { Key = "volume", Color = "label", Text = TguiFormat.JsNumber(beaker.Num("currentVolume")) + " / " + TguiFormat.JsNumber(beaker.Num("maxVolume")) + " units " },
                new UiNode(UiKind.Button) { Key = eject, Icon = "eject", Text = "Eject", OnClick = () => c.Act(eject) })
            : Insert(insert);

        // tg: MassSpec.tsx:65  the Start button and its tooltip
        string startTooltip = !beaker1HasContents ? "Missing input reagents!" : !hasBeaker2 ? "Missing an output beaker!" : eta <= 0 ? "No work to be done" : "Begin purifying";
        UiNode spectroscopy = new UiNode(UiKind.Section)
        {
            Key = "spectroscopy",
            Title = "Mass Spectroscopy",
            Buttons = new UiNode(UiKind.Button)
            {
                Key = "start",
                Icon = "power-off",
                Disabled = processing || eta <= 0 || !beaker1HasContents || !hasBeaker2,
                Tooltip = startTooltip,
                Text = "Start",
                OnClick = () => c.Act("activate"),
            },
        }.With(beaker1HasContents
            ? MassSpectroscopy(c, lowerRange, centerValue, upperRange, graphUpperRange, peakHeight, beaker1.Arr("contents"))
            : new UiNode(UiKind.Box) { Key = "please", Text = "Please insert an input beaker with reagents!" });

        UiNode input = new UiNode(UiKind.Section) { Key = "input", Title = "Input beaker", Buttons = BeakerButtons(beaker1, hasBeaker1, "eject1", "insert1") }.With(
            BeakerMassProfile(lowerRange, upperRange, hasBeaker1 ? beaker1 : null),
            beaker1HasContents ? new UiNode(UiKind.Box) { Key = "eta", Text = "Eta of selection: " + TguiFormat.JsNumber(TguiFormat.Round(eta, 0)) + " seconds" } : null);
        UiNode output = new UiNode(UiKind.Section) { Key = "output", Title = "Output beaker", Buttons = BeakerButtons(beaker2, hasBeaker2, "eject2", "insert2") }.With(
            BeakerMassProfile(lowerRange, upperRange, hasBeaker2 ? beaker2 : null));

        // tg: MassSpec.tsx:57  <Dimmer fontSize="32px"><Icon name="cog" spin /> Purifying... {round(eta, 0)}s</Dimmer>
        UiNode dimmer = processing
            ? new UiNode(UiKind.NoticeBox) { Key = "dimmer", FontSize = "32px" }.With(
                new UiNode(UiKind.Icon) { Key = "cog", Icon = "cog", Spin = true },
                new UiNode(UiKind.Box) { Key = "text", Text = " Purifying... " + TguiFormat.JsNumber(TguiFormat.Round(eta, 0)) + "s" })
            : null;

        // tg: MassSpec.tsx:55  <Window width={1050} height={650}><Window.Content scrollable>
        return c.Window(1050, 650, null, dimmer, spectroscopy, input, output);
    }

    // tg: MassSpec.tsx:177  BeakerMassProfile
    private static UiNode BeakerMassProfile(double lowerRange, double upperRange, UiValue beaker)
    {
        if (beaker == null)
            return new UiNode(UiKind.Box) { Key = "none", Color = "label", Text = "No beaker loaded." };
        IReadOnlyList<UiValue> contents = beaker.Arr("contents");
        if (contents.Count == 0)
            return new UiNode(UiKind.Box) { Key = "empty", Color = "label", Text = "Beaker is empty." };

        UiNode table = new UiNode(UiKind.Table) { Key = "profile" }.With(new UiNode(UiKind.TableRow) { Key = "head" }.With(
            new[] { "Reagent", "Mass", "Volume", "Purity", "Type", "Status" }.Select(h => new UiNode(UiKind.TableCell) { Key = h, Bold = true, Color = "label", Text = h }).ToArray()));
        foreach (UiValue reagent in contents)
        {
            double mass = reagent.Num("mass");
            bool selected = mass >= lowerRange && mass <= upperRange;
            string color = reagent.Str("type") == "Inverted" ? "#b60046" : "#3cf096";
            string cellColor = selected ? "green" : "default";
            table.With(new UiNode(UiKind.TableRow) { Key = "r:" + reagent.Str("name"), Candystripe = true }.With(
                new UiNode(UiKind.TableCell) { Key = "name", Color = cellColor, Text = reagent.Str("name") },
                new UiNode(UiKind.TableCell) { Key = "mass", Color = cellColor, Text = TguiFormat.JsNumber(mass) },
                new UiNode(UiKind.TableCell) { Key = "volume", Color = cellColor, Text = TguiFormat.JsNumber(reagent.Num("volume")) },
                new UiNode(UiKind.TableCell) { Key = "purity", Color = cellColor, Text = TguiFormat.JsNumber(reagent.Num("purity")) + "%" },
                new UiNode(UiKind.TableCell) { Key = "type", Color = color, Text = "▮" + reagent.Str("type") },
                new UiNode(UiKind.TableCell) { Key = "log", Text = reagent.Str("log") }));
        }
        return table;
    }

    // tg: MassSpec.tsx:241  MassSpectroscopy
    private static UiNode MassSpectroscopy(TguiContext c, double lowerRange, double centerValue, double upperRange, double graphUpperRange, double maxAbsorbance, IReadOnlyList<UiValue> reagentPeaks)
    {
        const double graphLowerRange = 0;
        double deltaRange = graphUpperRange - graphLowerRange;
        double graphIncrement = deltaRange * 0.2;
        double baseWidth = GRAPH_MAX_WIDTH - 123;

        // tg: :374  the peaks (a triangle 5 g each side of the mass, its height the volume) and the selected range's band
        UiNode chart = new UiNode(UiKind.Chart) { Key = "graph", Plain = true, Height = (GRAPH_MAX_HEIGHT * 0.85) + "px", Width = baseWidth + "px" };
        if (deltaRange > 0)
            chart.With(new UiNode(UiKind.ChartLine)
            {
                Key = "band",
                Data = new List<(double, double)> { (lowerRange, 0), (lowerRange, 1), (upperRange, 1), (upperRange, 0) },
                RangeX = (0, deltaRange),
                RangeY = (0, 1),
                FillColor = "rgba(0, 0, 255, 0.1)",
                StrokeColor = "rgba(0, 0, 255, 0)",
            });
        foreach (UiValue peak in reagentPeaks)
        {
            double mass = peak.Num("mass");
            string fill = peak.Str("type") == "Inverted" ? "rgba(182, 0, 70, 0.6)" : "rgba(60, 240, 150, 0.6)";
            chart.With(new UiNode(UiKind.ChartLine)
            {
                Key = "peak:" + peak.Str("name"),
                Data = new List<(double, double)> { (mass - 5, 0), (mass, peak.Num("volume")), (mass + 5, 0) },
                RangeX = (0, graphUpperRange),
                RangeY = (0, maxAbsorbance),
                FillColor = fill,
                StrokeColor = fill,
            });
        }

        // tg: :280  the x axis numbers, :330 the y axis numbers
        UiNode xAxis = new UiNode(UiKind.Stack) { Key = "xaxis", Justify = "space-between", Width = baseWidth + "px" }.With(
            new[] { graphLowerRange, TguiFormat.Round(graphLowerRange + graphIncrement, 1), TguiFormat.Round(graphLowerRange + graphIncrement * 2, 1), TguiFormat.Round(graphLowerRange + graphIncrement * 3, 1), TguiFormat.Round(graphLowerRange + graphIncrement * 4, 1), graphUpperRange }
                .Select((v, i) => new UiNode(UiKind.StackItem) { Key = "x" + i }.With(new UiNode(UiKind.Box) { Text = TguiFormat.JsNumber(v) })).ToArray());
        UiNode yAxis = new UiNode(UiKind.Stack) { Key = "yaxis", Vertical = true, Justify = "space-between", Height = (GRAPH_MAX_HEIGHT * 0.85) + "px" }.With(
            new[] { 1.0, 0.8, 0.6, 0.4, 0.2, 0.0 }.Select((f, i) => new UiNode(UiKind.StackItem) { Key = "y" + i }.With(new UiNode(UiKind.Box) { Text = TguiFormat.JsNumber(TguiFormat.Round(maxAbsorbance * f, 1)) })).ToArray());

        double step = baseWidth > 0 ? graphUpperRange / baseWidth : 1;
        string Fmt(double value) => TguiFormat.JsNumber(TguiFormat.Round(value, 2));
        return new UiNode(UiKind.Box) { Key = "spectro" }.With(
            new UiNode(UiKind.Stack) { Key = "graphRow" }.With(
                new UiNode(UiKind.StackItem).With(chart),
                new UiNode(UiKind.StackItem).With(yAxis),
                new UiNode(UiKind.StackItem).With(new UiNode(UiKind.Box) { Key = "au", Bold = true, Text = "Absorbance (AU)" })),
            xAxis,
            new UiNode(UiKind.Box) { Key = "massLabel", Bold = true, TextAlign = "center", Width = baseWidth + "px", Text = "Mass (G)" },
            // tg: :420  the sliders: the lower bound up to the centre, the upper from the centre, and the centre itself
            new UiNode(UiKind.Stack) { Key = "sliders", Width = baseWidth + "px" }.With(
                new UiNode(UiKind.StackItem) { Width = (graphUpperRange > 0 ? centerValue / graphUpperRange * baseWidth : 0) + "px" }.With(new UiNode(UiKind.Slider)
                {
                    Key = "left",
                    Step = step,
                    Format = Fmt,
                    Value = lowerRange,
                    MinValue = graphLowerRange,
                    MaxValue = centerValue,
                    OnChange = value => c.Act("leftSlider", ("value", value)),
                }),
                new UiNode(UiKind.StackItem) { Grow = 1 }.With(new UiNode(UiKind.Slider)
                {
                    Key = "right",
                    Step = step,
                    Format = Fmt,
                    Value = upperRange,
                    MinValue = centerValue,
                    MaxValue = graphUpperRange,
                    OnChange = value => c.Act("rightSlider", ("value", value)),
                })),
            new UiNode(UiKind.Slider)
            {
                Key = "center",
                Step = step,
                Value = centerValue,
                Format = Fmt,
                Width = baseWidth + "px",
                MinValue = graphLowerRange + 1,
                MaxValue = graphUpperRange - 1,
                OnChange = value => c.Act("centerSlider", ("value", value)),
            });
    }
}
