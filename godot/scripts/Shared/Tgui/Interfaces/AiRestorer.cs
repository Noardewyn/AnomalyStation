using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/AiRestorer.tsx and NtosAiRestorer.tsx (silicons SI4,
// D326): the AI system integrity restorer console and the NtOS AI Manager & Restorer.
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class AiRestorer
{
    private static readonly (string, double, double)[] IntegrityRanges =
    {
        ("good", 70, double.PositiveInfinity),
        ("average", 50, 70),
        ("bad", double.NegativeInfinity, 50),
    };

    // tg: AiRestorer.tsx:24
    public static UiNode Render(TguiContext c) => c.Window(370, 360, null, Content(c));

    // tg: NtosAiRestorer.tsx:4
    public static UiNode RenderNtos(TguiContext c) => NtosWindow.Render(c, 370, 400, null, Content(c));

    // tg: AiRestorer.tsx:34
    public static UiNode Content(TguiContext c)
    {
        UiValue data = c.Data;
        bool aiPresent = data.Truthy("AI_present"), isDead = data.Truthy("isDead"), restoring = data.Truthy("restoring"), ejectable = data.Truthy("ejectable");
        string error = data.Str("error"), name = data.Str("name");
        IReadOnlyList<UiValue> laws = data.Get("laws")?.Items ?? Array.Empty<UiValue>();
        return new UiNode(UiKind.Fragment).With(
            !string.IsNullOrEmpty(error) ? new UiNode(UiKind.NoticeBox) { Key = "error", TextAlign = "center", Text = error } : null,
            ejectable ? new UiNode(UiKind.Button) { Key = "eject", Fluid = true, Icon = "eject", Text = aiPresent ? name : "----------", Disabled = !aiPresent, OnClick = () => c.Act("PRG_eject") } : null,
            aiPresent
                ? new UiNode(UiKind.Section)
                {
                    Key = "status",
                    Title = ejectable ? "System Status" : name,
                    Buttons = new UiNode(UiKind.Box) { Bold = true, Color = isDead ? "bad" : "good", Text = isDead ? "Nonfunctional" : "Functional" },
                }.With(
                    new UiNode(UiKind.LabeledList).With(
                        new UiNode(UiKind.LabeledItem) { Label = "Integrity" }.With(
                            new UiNode(UiKind.ProgressBar) { Value = data.Num("health"), MinValue = 0, MaxValue = 100, Ranges = IntegrityRanges })),
                    restoring ? new UiNode(UiKind.Box) { Key = "restoring", Bold = true, TextAlign = "center", FontSize = "20px", Color = "good", Text = "RECONSTRUCTION IN PROGRESS" } : null,
                    new UiNode(UiKind.Button) { Key = "reconstruct", Fluid = true, Icon = "plus", Text = "Begin Reconstruction", Disabled = restoring, OnClick = () => c.Act("PRG_beginReconstruction") },
                    new UiNode(UiKind.Section) { Key = "laws", Title = "Laws" }.With(
                        laws.Select(law => new UiNode(UiKind.Box) { Key = law.AsStr(), Text = law.AsStr() })))
                : null);
    }
}
