using System;
using System.Collections.Generic;
using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/LingMMITalk.tsx (gaps K1, D327): the changeling's view
// through its decoy brain in an MMI, from /datum/action/changeling/mmi_talk's ui_static_data (mmi_talk.dm:68): the map view around
// the MMI (the client's second view of the mmi_view map, UiKind.ByondUi, as the mech's view draws its map) and the message the decoy
// brain says. Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class LingMMITalk
{
    private const string MessageKey = "mmiMessage";

    // tg: LingMMITalk.tsx:11
    public static UiNode Render(TguiContext c)
    {
        string mmi_view = c.Data.Str("mmi_view");
        string mmiMessage = c.UseState(MessageKey, "");
        // tg: LingMMITalk.tsx:16  <Window title="Decoy Brain MMI View" height={360} width={360}>
        return c.Window(360, 360, "Decoy Brain MMI View",
            new UiNode(UiKind.Stack) { Key = "mmi", Vertical = true }.With(
                // tg: LingMMITalk.tsx:19  <ByondUi width="240px" height="240px" params={{ id: data.mmi_view, type: 'map' }} />
                new UiNode(UiKind.StackItem) { Key = "view", Align = "center" }.With(
                    new UiNode(UiKind.ByondUi) { Key = "map:" + mmi_view, MapRef = mmi_view, MapPicture = true, Width = "240px", Height = "240px" }),
                new UiNode(UiKind.StackItem) { Key = "talk" }.With(new UiNode(UiKind.Stack) { Key = "talk", Width = "100%" }.With(
                    // tg: LingMMITalk.tsx:30  <TextArea fluid height="60px" placeholder=... onChange={setmmiMessage} value={mmiMessage} />
                    new UiNode(UiKind.StackItem) { Key = "message", Width = "85%" }.With(new UiNode(UiKind.TextArea)
                    {
                        Key = "message", Fluid = true, Height = "60px", Wrap = true,
                        Tooltip = "Send a message to have our decoy brain speak.",
                        TextValue = mmiMessage,
                        OnInput = value => c.SetState(MessageKey, value),
                    }),
                    // tg: LingMMITalk.tsx:39
                    new UiNode(UiKind.StackItem) { Key = "send", Align = "center" }.With(new UiNode(UiKind.Button)
                    {
                        Key = "send", TextAlign = "center", Text = "Send",
                        OnClick = () =>
                        {
                            c.Act("send_mmi_message", ("message", mmiMessage));
                            c.SetState(MessageKey, "");
                        },
                    })))));
    }
}
