using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/NtosSupermatter.tsx (the engineering package E2, D287:
// NT CIMS, the engineering consoles' supermatter monitor). Engine-free (compiled into the tests too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class NtosSupermatter
{
    private const string ActiveKey = "activeUID";

    // Each Details or Back mounts the list or a crystal's SupermatterContent afresh in React, with fresh local state.
    private const string MountKey = "mount";

    // tg: NtosSupermatter.tsx:9
    public static UiNode Render(TguiContext c)
    {
        IReadOnlyList<UiValue> smData = c.Data.Arr("sm_data");
        UiValue gasMetadata = c.Data.Get("gas_metadata");
        UiValue focusUid = c.Data.Get("focus_uid");
        double activeUID = c.UseState(ActiveKey, 0.0);
        int mount = c.UseState(MountKey, 0);
        UiValue activeSM = smData.FirstOrDefault(sm => sm.Num("uid") == activeUID);

        void SetActiveUID(double uid)
        {
            c.SetState(MountKey, mount + 1);
            c.SetState(ActiveKey, uid);
        }

        UiNode content;
        if (activeSM != null)
        {
            content = Supermatter.Content(c, activeSM, gasMetadata,
                new UiNode(UiKind.Button) { Key = "back", Icon = "arrow-left", Text = "Back", OnClick = () => SetActiveUID(0) },
                "sm" + mount + ":");
        }
        else
        {
            var table = new UiNode(UiKind.Table) { Key = "sms" };
            foreach (UiValue sm in smData)
            {
                double uid = sm.Num("uid");
                bool focused = focusUid != null && focusUid.Kind == UiValueKind.Number && focusUid.AsNum() == uid;
                // tg: NtosSupermatter.tsx:44  the cells' collapsing (shrink to their content) is not drawn
                table.With(new UiNode(UiKind.TableRow) { Key = "sm:" + TguiFormat.JsNumber(uid) }.With(
                    new UiNode(UiKind.TableCell) { Key = "name", Text = $"{TguiFormat.JsNumber(uid)}. {sm.Str("area_name")}" },
                    new UiNode(UiKind.TableCell) { Key = "label", Color = "label", Text = "Integrity:" },
                    new UiNode(UiKind.TableCell) { Key = "cell:integrity", Width = "120px" }.With(new UiNode(UiKind.ProgressBar)
                    {
                        Key = "integrity",
                        Value = sm.Num("integrity") / 100,
                        Ranges = new[] { ("good", 0.9, double.PositiveInfinity), ("average", 0.5, 0.9), ("bad", double.NegativeInfinity, 0.5) },
                    }),
                    new UiNode(UiKind.TableCell) { Key = "cell:focus" }.With(new UiNode(UiKind.Button)
                    {
                        Key = "focus",
                        Icon = "bell",
                        Color = focused ? "yellow" : null,
                        OnClick = () => c.Act("PRG_focus", ("focus_uid", uid)),
                    }),
                    new UiNode(UiKind.TableCell) { Key = "cell:details" }.With(new UiNode(UiKind.Button)
                    {
                        Key = "details",
                        Text = "Details",
                        OnClick = () => SetActiveUID(uid),
                    })));
            }
            // tg: NtosSupermatter.tsx:32
            content = new UiNode(UiKind.Section)
            {
                Key = "detected",
                Title = "Detected Supermatters",
                Buttons = new UiNode(UiKind.Button) { Key = "refresh", Icon = "sync", Text = "Refresh", OnClick = () => c.Act("PRG_refresh") },
            }.With(table);
        }
        return NtosWindow.Render(c, 700, 400, null, content);
    }
}
