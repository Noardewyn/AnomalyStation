using System.Linq;
using Anomaly.Client.Shared.Tgui.Interfaces.Common;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/CommunicationsConsole/ (index.tsx, Main.tsx,
// AlertButton.tsx, MessageModal.tsx, ChangingStatus.tsx, Messages.tsx, NoConnectionModal.tsx; M10 slice G5, D272; the shuttle
// purchase page BuyingShuttle.tsx and constants.ts since gaps CM1, D333). Engine-free (compiled into the tests too). The modals' local
// state is the window's (tg's useState); the message HTML (sanitizeText) shows as its text. Not here: Main.tsx's allied sectors (the
// cross-server messages: the console never offers a sector).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class CommunicationsConsole
{
    // tg: CommunicationsConsole/constants.ts:2
    private const string SWIPE_NEEDED = "SWIPE_NEEDED";

    // tg: CommunicationsConsole/constants.ts:4
    private const string EMAG_SHUTTLE_NOTICE = "This shuttle is deemed significantly dangerous to the crew, and is only supplied by the Syndicate.";

    // tg: CommunicationsConsole/types.ts:3  ShuttleState
    private const string MAIN = "main";
    private const string MESSAGES = "messages";
    private const string CHANGING_STATUS = "changing_status";
    private const string BUYING_SHUTTLE = "buying_shuttle";

    private static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];

    // tg: CommunicationsConsole/index.tsx:13
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool authenticated = data.Truthy("authenticated");
        string authorizeName = data.Str("authorizeName");
        bool canLogOut = data.Truthy("canLogOut");
        bool canRequestSafeCode = data.Truthy("canRequestSafeCode");
        bool emagged = data.Truthy("emagged");
        bool hasConnection = data.Truthy("hasConnection");
        string page = data.Str("page");
        double safeCodeDeliveryWait = data.Num("safeCodeDeliveryWait");
        c.WindowTheme = emagged ? "syndicate" : null;

        UiNode currentPage = page switch
        {
            CHANGING_STATUS => PageChangingStatus(c),
            MAIN => PageMain(c),
            MESSAGES => PageMessages(c),
            BUYING_SHUTTLE => PageBuyingShuttle(c),
            _ => new UiNode(UiKind.Box) { Key = "page", Text = "Page not implemented: " + page },
        };

        var content = new List<UiNode>();
        if (!hasConnection)
            content.Add(NoConnectionModal());
        if (canLogOut || !authenticated)
        {
            content.Add(new UiNode(UiKind.Section) { Key = "auth", Title = "Authentication" }.With(new UiNode(UiKind.Button)
            {
                Key = "toggleAuthentication",
                Icon = authenticated ? "sign-out-alt" : "sign-in-alt",
                Color = authenticated ? "bad" : "good",
                Text = authenticated ? "Log Out" + (string.IsNullOrEmpty(authorizeName) ? "" : $" ({authorizeName})") : "Log In",
                OnClick = () => c.Act("toggleAuthentication"),
            }));
        }
        if (canRequestSafeCode)
        {
            content.Add(new UiNode(UiKind.Section) { Key = "safecode", Title = "Emergency Safe Code" }.With(new UiNode(UiKind.Button)
            {
                Key = "requestSafeCodes",
                Icon = "key",
                Color = "good",
                Text = "Request Safe Code",
                OnClick = () => c.Act("requestSafeCodes"),
            }));
        }
        else if (safeCodeDeliveryWait != 0)
        {
            content.Add(new UiNode(UiKind.Section) { Key = "safecodewait", Title = "Emergency Safe Code Delivery", Color = "label" }.With(
                new UiNode(UiKind.Box) { Text = $"Drop pod to {data.Str("safeCodeDeliveryArea")} in {Math.Round(safeCodeDeliveryWait / 10)}s" }));
        }
        if (authenticated)
            content.Add(currentPage);

        return c.Window(400, 650, null, new UiNode(UiKind.Section) { Fill = true, Scrollable = true }.With(content));
    }

    // tg: CommunicationsConsole/Main.tsx:11
    private static UiNode PageMain(TguiContext c)
    {
        UiValue data = c.Data;
        string alertLevel = data.Str("alertLevel");
        int callShuttleReasonMinLength = (int)data.Num("callShuttleReasonMinLength");
        UiValue canBuyShuttles = data.Field("canBuyShuttles");
        bool canMakeAnnouncement = data.Truthy("canMakeAnnouncement");
        bool canMessageAssociates = data.Truthy("canMessageAssociates");
        bool canRecallShuttles = data.Truthy("canRecallShuttles");
        bool canRequestNuke = data.Truthy("canRequestNuke");
        bool canSetAlertLevel = data.Truthy("canSetAlertLevel");
        bool canToggleEmergencyAccess = data.Truthy("canToggleEmergencyAccess");
        bool emagged = data.Truthy("emagged");
        bool syndicate = data.Truthy("syndicate");
        bool emergencyAccess = data.Truthy("emergencyAccess");
        bool importantActionReady = data.Truthy("importantActionReady");
        bool shuttleCalled = data.Truthy("shuttleCalled");
        bool shuttleCalledPreviously = data.Truthy("shuttleCalledPreviously");
        UiValue canEvac = data.Field("shuttleCanEvacOrFailReason");
        bool canEvacNow = canEvac is { Kind: UiValueKind.Number } && canEvac.Number == 1;
        string shuttleLastCalled = data.Truthy("shuttleLastCalled") ? data.Str("shuttleLastCalled") : null;
        bool shuttleRecallable = data.Truthy("shuttleRecallable");

        bool callingShuttle = c.UseState("callingShuttle", false);
        bool messagingAssociates = c.UseState("messagingAssociates", false);
        bool requestingNukeCodes = c.UseState("requestingNukeCodes", false);
        string newAlertLevel = c.UseState("newAlertLevel", "");
        bool showAlertLevelConfirm = newAlertLevel != "" && newAlertLevel != alertLevel;

        var sections = new List<UiNode>();
        if (!syndicate)
        {
            var shuttle = new UiNode(UiKind.Section) { Key = "shuttle", Title = "Emergency Shuttle" };
            if (shuttleCalled)
            {
                shuttle.Children.Add(c.ButtonConfirm("recallShuttle", new UiNode(UiKind.Button)
                {
                    Icon = "space-shuttle",
                    Color = "bad",
                    Disabled = !canRecallShuttles || !shuttleRecallable,
                    Tooltip = canRecallShuttles && !shuttleRecallable
                        ? "It's too late for the emergency shuttle to be recalled."
                        : "You do not have permission to recall the emergency shuttle.",
                    Text = "Recall Emergency Shuttle",
                    OnClick = () => c.Act("recallShuttle"),
                }));
            }
            else
            {
                shuttle.Children.Add(new UiNode(UiKind.Button)
                {
                    Key = "callShuttle",
                    Icon = "space-shuttle",
                    Disabled = !canEvacNow,
                    Tooltip = !canEvacNow ? canEvac.AsStr() : null,
                    Text = "Call Emergency Shuttle",
                    OnClick = () => c.SetState("callingShuttle", true),
                });
            }
            if (shuttleCalledPreviously)
            {
                shuttle.Children.Add(shuttleLastCalled != null
                    ? new UiNode(UiKind.Box) { Key = "lastcalled", Text = "Most recent shuttle call/recall traced to: " + shuttleLastCalled }
                    : new UiNode(UiKind.Box) { Key = "lastcalled", Text = "Unable to trace most recent shuttle/recall signal." });
            }
            sections.Add(shuttle);
        }

        if (canSetAlertLevel)
        {
            sections.Add(new UiNode(UiKind.Section) { Key = "alert", Title = "Alert Level" }.With(
                new UiNode(UiKind.Stack) { Justify = "space-between" }.With(
                    new UiNode(UiKind.StackItem) { Key = "current" }.With(new UiNode(UiKind.Box) { Text = $"Currently on {Capitalize(alertLevel)} Alert" }),
                    new UiNode(UiKind.StackItem) { Key = "buttons" }.With(
                        AlertButton(c, "green", () => c.SetState("newAlertLevel", "green")),
                        AlertButton(c, "blue", () => c.SetState("newAlertLevel", "blue"))))));
        }

        var functions = new List<UiNode>();
        if (canMakeAnnouncement)
            functions.Add(new UiNode(UiKind.Button) { Key = "announce", Icon = "bullhorn", Text = "Make Priority Announcement", OnClick = () => c.Act("makePriorityAnnouncement") });
        if (canToggleEmergencyAccess)
        {
            functions.Add(c.ButtonConfirm("emergencyAccess", new UiNode(UiKind.Button)
            {
                Icon = "id-card-o",
                Color = emergencyAccess ? "bad" : null,
                Text = (emergencyAccess ? "Disable" : "Enable") + " Emergency Maintenance Access",
                OnClick = () => c.Act("toggleEmergencyAccess"),
            }));
        }
        if (!syndicate)
            functions.Add(new UiNode(UiKind.Button) { Key = "status", Icon = "desktop", Text = "Set Status Display", OnClick = () => c.Act("setState", ("state", CHANGING_STATUS)) });
        functions.Add(new UiNode(UiKind.Button) { Key = "messages", Icon = "envelope-o", Text = "Message List", OnClick = () => c.Act("setState", ("state", MESSAGES)) });
        if (canBuyShuttles != null && !(canBuyShuttles.Kind is UiValueKind.Number or UiValueKind.False && canBuyShuttles.AsNum() == 0))
        {
            bool canBuy = canBuyShuttles.AsNum() == 1 && canBuyShuttles.Kind != UiValueKind.String;
            functions.Add(new UiNode(UiKind.Button)
            {
                Key = "buy",
                Icon = "shopping-cart",
                Disabled = !canBuy,
                Tooltip = !canBuy ? canBuyShuttles.AsStr() : null, // canBuyShuttles is a string detailing the fail reason
                Text = "Purchase Shuttle",
                OnClick = () => c.Act("setState", ("state", BUYING_SHUTTLE)),
            });
        }
        if (canMessageAssociates)
            functions.Add(new UiNode(UiKind.Button) { Key = "associates", Icon = "comment-o", Disabled = !importantActionReady, Text = "Send message to " + (emagged ? "[UNKNOWN]" : "CentCom"), OnClick = () => c.SetState("messagingAssociates", true) });
        if (canRequestNuke)
            functions.Add(new UiNode(UiKind.Button) { Key = "nuke", Icon = "radiation", Disabled = !importantActionReady, Text = "Request Nuclear Authentication Codes", OnClick = () => c.SetState("requestingNukeCodes", true) });
        if (emagged && !syndicate)
            functions.Add(new UiNode(UiKind.Button) { Key = "restore", Icon = "undo", Text = "Restore Backup Routing Data", OnClick = () => c.Act("restoreBackupRoutingData") });
        sections.Add(new UiNode(UiKind.Section) { Key = "functions", Title = "Functions" }.With(new UiNode(UiKind.Stack) { Vertical = true }.With(functions)));

        if (canMessageAssociates && messagingAssociates)
        {
            sections.Add(MessageModal(c, "associatesModal",
                $"Message to transmit to {(emagged ? "[ABNORMAL ROUTING COORDINATES]" : "CentCom")} via quantum entanglement",
                "Please be aware that this process is very expensive, and abuse will lead to...termination. Transmission does not guarantee a response.",
                "bullhorn", "Send", null,
                () => c.SetState("messagingAssociates", false),
                message => { c.SetState("messagingAssociates", false); c.Act("messageAssociates", ("message", message)); }));
        }
        if (canRequestNuke && requestingNukeCodes)
        {
            sections.Add(MessageModal(c, "nukeModal",
                "Reason for requesting nuclear self-destruct codes",
                "Misuse of the nuclear request system will not be tolerated under any circumstances. Transmission does not guarantee a response.",
                "bomb", "Request Codes", null,
                () => c.SetState("requestingNukeCodes", false),
                reason => { c.SetState("requestingNukeCodes", false); c.Act("requestNukeCodes", ("reason", reason)); }));
        }
        if (callingShuttle)
        {
            sections.Add(MessageModal(c, "shuttleModal", "Nature of emergency", null, "space-shuttle", "Call Shuttle", callShuttleReasonMinLength,
                () => c.SetState("callingShuttle", false),
                reason => { c.SetState("callingShuttle", false); c.Act("callShuttle", ("reason", reason)); }));
        }
        if (canSetAlertLevel && showAlertLevelConfirm)
        {
            sections.Add(new UiNode(UiKind.Modal) { Key = "alertModal" }.With(
                new UiNode(UiKind.Stack) { Vertical = true, Width = "300px" }.With(
                    new UiNode(UiKind.StackItem) { Key = "title" }.With(new UiNode(UiKind.Box) { FontSize = "16px", TextAlign = "center", Text = "Swipe ID to confirm change" }),
                    new UiNode(UiKind.StackItem) { Key = "buttons" }.With(
                        new UiNode(UiKind.Button)
                        {
                            Key = "swipe",
                            Icon = "id-card-o",
                            Color = "good",
                            FontSize = "16px",
                            Text = "Swipe ID",
                            OnClick = () =>
                            {
                                c.Act("changeSecurityLevel", ("newSecurityLevel", newAlertLevel));
                                c.SetState("newAlertLevel", "");
                            },
                        },
                        new UiNode(UiKind.Button) { Key = "cancel", Icon = "times", Color = "bad", FontSize = "16px", Text = "Cancel", OnClick = () => c.SetState("newAlertLevel", "") }))));
        }
        // tg: Main.tsx:301-347  the allied sectors (canSendToSectors): the cross-server messages, never offered (no other server)
        return new UiNode(UiKind.Box) { Key = "page" }.With(sections);
    }

    // tg: CommunicationsConsole/AlertButton.tsx:13
    private static UiNode AlertButton(TguiContext c, string alertLevel, Action onClick)
    {
        UiValue data = c.Data;
        bool thisIsCurrent = data.Str("alertLevel") == alertLevel;
        string canSetAlertLevel = data.Str("canSetAlertLevel");
        return new UiNode(UiKind.Button)
        {
            Key = "alert:" + alertLevel,
            Icon = "exclamation-triangle",
            Color = thisIsCurrent ? "good" : null,
            Text = Capitalize(alertLevel),
            OnClick = () =>
            {
                if (thisIsCurrent)
                    return;
                if (canSetAlertLevel == SWIPE_NEEDED)
                    onClick();
                else
                    c.Act("changeSecurityLevel", ("newSecurityLevel", alertLevel));
            },
        };
    }

    // tg: CommunicationsConsole/MessageModal.tsx:18
    private static UiNode MessageModal(TguiContext c, string key, string label, string notice, string icon, string buttonText, int? minLength, Action onBack, Action<string> onSubmit)
    {
        int maxMessageLength = (int)c.Data.Num("maxMessageLength");
        string inputKey = key + ":input";
        string input = c.UseState(inputKey, "");
        bool longEnough = minLength == null || input.Length >= minLength;
        var column = new UiNode(UiKind.Stack) { Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "label" }.With(new UiNode(UiKind.Box) { FontSize = "16px", Wrap = true, Text = label + ":" }),
            new UiNode(UiKind.StackItem) { Key = "text" }.With(new UiNode(UiKind.TextArea)
            {
                Key = inputKey,
                Fluid = true,
                Height = 6,
                MaxLength = maxMessageLength,
                Tooltip = label,
                TextValue = input,
                OnInput = v => c.SetState(inputKey, v),
            }),
            new UiNode(UiKind.StackItem) { Key = "buttons" }.With(
                new UiNode(UiKind.Button)
                {
                    Key = "submit",
                    Icon = icon,
                    Color = "good",
                    Disabled = !longEnough,
                    Tooltip = !longEnough ? "You need a longer reason." : "",
                    Text = buttonText,
                    OnClick = () =>
                    {
                        string text = c.UseState(inputKey, "");
                        if (minLength == null || text.Length >= minLength)
                        {
                            c.SetState(inputKey, "");
                            onSubmit(text);
                        }
                    },
                },
                new UiNode(UiKind.Button) { Key = "back", Icon = "times", Color = "bad", Text = "Cancel", OnClick = onBack }));
        if (!string.IsNullOrEmpty(notice))
            column.Children.Add(new UiNode(UiKind.StackItem) { Key = "notice" }.With(new UiNode(UiKind.Box) { Wrap = true, Text = notice }));
        return new UiNode(UiKind.Modal) { Key = key }.With(column);
    }

    // tg: CommunicationsConsole/BuyingShuttle.tsx:8  sortShuttles: the emag-only shuttles first, then by their initial cost
    private static List<UiValue> SortShuttles(IReadOnlyList<UiValue> shuttles) =>
        shuttles.OrderBy(shuttle => !shuttle.Truthy("emagOnly")).ThenBy(shuttle => shuttle.Num("initial_cost")).ToList();

    // tg: CommunicationsConsole/BuyingShuttle.tsx:15
    private static UiNode PageBuyingShuttle(TguiContext c)
    {
        UiValue data = c.Data;
        double budget = data.Num("budget");
        var page = new UiNode(UiKind.Box) { Key = "page" }.With(
            new UiNode(UiKind.Section) { Key = "header" }.With(
                new UiNode(UiKind.Stack) { Fill = true, Align = "center", Justify = "space-between" }.With(
                    new UiNode(UiKind.StackItem) { Key = "back" }.With(new UiNode(UiKind.Button)
                    {
                        Key = "backButton",
                        Icon = "chevron-left",
                        Text = "Back",
                        OnClick = () => c.Act("setState", ("state", MAIN)),
                    }),
                    new UiNode(UiKind.StackItem) { Key = "budget" }.With(
                        new UiNode(UiKind.Box) { Key = "budgetText", Bold = true, Color = "good", Text = TguiFormat.JsNumber(budget) + " cr" },
                        new UiNode(UiKind.Icon) { Key = "coins", Icon = "coins", Color = "gold" }))));
        int index = 0;
        foreach (UiValue shuttle in SortShuttles(data.Arr("shuttles")))
            page.Children.Add(ShuttleCard(c, shuttle, index++));
        return page;
    }

    // tg: CommunicationsConsole/BuyingShuttle.tsx:51
    private static UiNode ShuttleCard(TguiContext c, UiValue shuttle, int index)
    {
        UiValue data = c.Data;
        double budget = data.Num("budget");
        string displayedCurrencyName = data.Str("displayed_currency_name");
        string displayedCurrencyFullName = data.Str("displayed_currency_full_name");
        bool emagged = data.Truthy("emagged");
        double creditCost = shuttle.Num("creditCost");
        bool departmentLocked = shuttle.Truthy("department_locked");
        bool emagOnly = shuttle.Truthy("emagOnly");
        string departmentName = shuttle.Str("department_name");

        bool isButtonDisabled = budget < creditCost;

        string buttonTooltip = null;
        if (budget < creditCost)
            buttonTooltip = $"You need {TguiFormat.JsNumber(creditCost - budget)} more {displayedCurrencyFullName}.";
        else if (departmentLocked)
            buttonTooltip = $"Requires {departmentName} Department to have the highest employees count.";
        else if (emagOnly)
            buttonTooltip = EMAG_SHUTTLE_NOTICE;

        string shuttleRef = shuttle.Str("ref");
        var card = new UiNode(UiKind.Section)
        {
            Key = "shuttle:" + index + ":" + shuttleRef,
            Title = shuttle.Str("name"),
            Buttons = new UiNode(UiKind.Button)
            {
                Key = "buy:" + shuttleRef,
                Color = departmentLocked ? "danger" : emagOnly ? "red" : "default",
                Disabled = isButtonDisabled,
                Tooltip = buttonTooltip,
                Text = departmentLocked ? "Locked" : emagOnly && !emagged ? "Buy" : $"{TguiFormat.JsNumber(creditCost)} {displayedCurrencyName}",
                OnClick = () => c.Act("purchaseShuttle", ("shuttle", shuttleRef)),
            },
        }.With(
            new UiNode(UiKind.Box) { Key = "description", Wrap = true, Text = shuttle.Str("description") },
            new UiNode(UiKind.Box) { Key = "occupancy", Color = "teal", FontSize = "10px", Text = "Occupancy Limit: " + shuttle.Str("occupancy_limit") });
        string prerequisites = shuttle.Str("prerequisites");
        if (!string.IsNullOrEmpty(prerequisites))
            card.Children.Add(new UiNode(UiKind.Box) { Key = "prerequisites", Color = "violet", FontSize = "10px", Bold = true, Wrap = true, Text = "Prerequisites: " + prerequisites });
        if (departmentLocked)
        {
            card.Children.Add(new UiNode(UiKind.Box)
            {
                Key = "locked",
                Color = "red",
                FontSize = "10px",
                Bold = true,
                Wrap = true,
                Text = $"This shuttle can only be purchased if {departmentName} Department has biggest number of employees!",
            });
        }
        return card;
    }

    // tg: CommunicationsConsole/ChangingStatus.tsx:7
    private static UiNode PageChangingStatus(TguiContext c) => new UiNode(UiKind.Box) { Key = "page" }.With(
        new UiNode(UiKind.Section) { Key = "back" }.With(new UiNode(UiKind.Button)
        {
            Key = "backButton",
            Icon = "chevron-left",
            Text = "Back",
            OnClick = () => c.Act("setState", ("state", MAIN)),
        }),
        StatusDisplayControls.Render(c));

    // tg: CommunicationsConsole/Messages.tsx:8
    private static UiNode PageMessages(TguiContext c)
    {
        IReadOnlyList<UiValue> messages = c.Data.Arr("messages");
        var children = new List<UiNode>
        {
            new UiNode(UiKind.Section) { Key = "back" }.With(new UiNode(UiKind.Button)
            {
                Key = "backButton",
                Icon = "chevron-left",
                Text = "Back",
                OnClick = () => c.Act("setState", ("state", MAIN)),
            }),
        };
        var messageElements = new List<UiNode>();
        for (int messageIndex = 0; messageIndex < messages.Count; messageIndex++)
        {
            UiValue message = messages[messageIndex];
            int index = messageIndex;
            IReadOnlyList<UiValue> possibleAnswers = message.Arr("possibleAnswers");
            int answered = (int)message.Num("answered");
            var section = new UiNode(UiKind.Section)
            {
                Key = "message:" + messageIndex,
                Title = message.Str("title"),
                Buttons = c.ButtonConfirm("delete:" + messageIndex, new UiNode(UiKind.Button)
                {
                    Icon = "trash",
                    Color = "red",
                    Text = "Delete",
                    // tg: messageIndex + 1 on the string key of Object.entries: "0" + 1 is "01", which text2num reads as 1
                    OnClick = () => c.Act("deleteMessage", ("message", index + "1")),
                }),
            }.With(new UiNode(UiKind.Box) { Key = "content", Wrap = true, Text = HtmlText.ToPlain(message.Str("content")) });
            if (possibleAnswers.Count > 0)
            {
                section.Children.Add(new UiNode(UiKind.Box) { Key = "answers" }.With(possibleAnswers.Select((answer, answerIndex) => new UiNode(UiKind.Button)
                {
                    Key = "answer:" + answerIndex,
                    Color = answered == answerIndex + 1 ? "good" : null,
                    Text = answer.AsStr(),
                    OnClick = answered != 0 ? null : () => c.Act("answerMessage", ("message", index + 1), ("answer", answerIndex + 1)),
                })));
            }
            messageElements.Add(section);
        }
        messageElements.Reverse();
        children.AddRange(messageElements);
        return new UiNode(UiKind.Box) { Key = "page" }.With(children);
    }

    // tg: CommunicationsConsole/NoConnectionModal.tsx:3
    private static UiNode NoConnectionModal() => new UiNode(UiKind.Modal) { Key = "noconnection" }.With(
        new UiNode(UiKind.Box) { Key = "icon", Color = "red", Icon = "wifi", FontSize = "48px", TextAlign = "center" },
        new UiNode(UiKind.Box) { Key = "text", FontSize = "16px", TextAlign = "center", Text = "A connection to the station cannot be established." });
}
