using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/ExperimentConfigure.tsx (gaps RD3, D333: the experiment
// handler's window of the experi-scanner, the destructive scanner and the operating computer), with its styles
// (styles/interfaces/ExperimentConfigure.scss). Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
// Its Experiment panel and TechwebServer list are public, as tg exports them: the operating computer's ExperimentView and the Techweb
// window's nodes draw them too. Leaves: the experiment's title button and its tag and hint sit in one row (tg places the tag
// absolutely over the button's right end); a selected panel's border colour and a completed stage's line-through are not drawn (the
// stage is dimmed as tg's 0.4 opacity).

public static class ExperimentConfigure
{
    // styles/interfaces/ExperimentConfigure.scss: var(--color-primary) of tgui-core's default theme, var(--color-black)
    private const string Primary = "#4972a1", Black = "#000000";

    // tg: ExperimentConfigure.tsx:48  ExperimentStageRow (a stage is [type, description, value, altValue])
    private static UiNode ExperimentStageRow(UiValue stage, int index)
    {
        IReadOnlyList<UiValue> items = stage?.Kind == UiValueKind.Array ? stage.Items : Array.Empty<UiValue>();
        string type = items.Count > 0 ? items[0].AsStr() : "";
        string description = items.Count > 1 ? items[1].AsStr() : "";
        UiValue value = items.Count > 2 ? items[2] : null;
        UiValue altValue = items.Count > 3 ? items[3] : null;

        // Determine completion based on type of stage
        bool completion = false;
        switch (type)
        {
            case "bool":
            case "detail":
                completion = value?.Truthy ?? false;
                break;
            case "integer":
                completion = value != null && altValue != null && value.Kind == altValue.Kind && value.AsNum() == altValue.AsNum();
                break;
            case "float":
                completion = value.AsNum() >= 1;
                break;
        }

        // tg: ExperimentConfigure.tsx:76
        var indicator = new UiNode(UiKind.TableCell) { Key = "indicator", Color = completion ? "good" : "bad", Bold = true, TextAlign = "center" };
        if (type == "bool")
            indicator.With(new UiNode(UiKind.Icon) { Key = "check", Icon = (value?.Truthy ?? false) ? "check" : "times", Color = completion ? "good" : "bad" });
        else if (type == "integer")
            indicator.Text = TguiFormat.JsNumber(value.AsNum()) + "/" + TguiFormat.JsNumber(altValue.AsNum());
        else if (type == "float")
            indicator.Text = TguiFormat.JsNumber(value.AsNum() * 100) + "%";
        else if (type == "detail")
            indicator.Text = "⤷";

        return new UiNode(UiKind.TableRow) { Key = "stage" + index }.With(
            indicator,
            new UiNode(UiKind.TableCell) { Key = "description" }.With(
                new UiNode(UiKind.Box) { Key = "description", Text = description, Wrap = true, Opacity = completion ? 0.4 : null }));
    }

    /// <summary>
    /// tg's <c>TechwebServer</c> (ExperimentConfigure.tsx:93): one panel per techweb with its name, the Connect or Disconnect button (when
    /// <paramref name="canSelect"/>) and the servers that keep it reachable.
    /// </summary>
    // tg: ExperimentConfigure.tsx:93
    public static UiNode TechwebServer(TguiContext c, IEnumerable<UiValue> techwebs, bool canSelect = true)
    {
        var list = new UiNode(UiKind.Fragment);
        int index = 0;
        foreach (UiValue server in techwebs)
        {
            bool selected = server.Truthy("selected");
            string serverRef = server.Str("ref");
            var header = new UiNode(UiKind.Stack) { Key = "header", Align = "center", Justify = "space-between" }.With(
                new UiNode(UiKind.StackItem) { Key = "name", Grow = 1, Text = server.Str("web_id") + " / " + server.Str("web_org"), FontSize = "18px" });
            if (canSelect)
            {
                header.With(new UiNode(UiKind.StackItem) { Key = "connect" }.With(new UiNode(UiKind.Button)
                {
                    Key = "connect",
                    Text = selected ? "Disconnect" : "Connect",
                    Color = selected ? "good" : null,
                    Background = selected ? null : "#00000066", // rgba(0, 0, 0, 0.4)
                    OnClick = () =>
                    {
                        if (selected)
                            c.Act("clear_server");
                        else
                            c.Act("select_server", ("ref", serverRef));
                    },
                }));
            }
            var servers = new UiNode(UiKind.Stack) { Key = "servers", Vertical = true };
            int serverIndex = 0;
            foreach (UiValue individualServer in server.Arr("all_servers"))
                servers.With(new UiNode(UiKind.StackItem) { Key = "server" + serverIndex++, Text = individualServer.AsStr() });

            list.With(new UiNode(UiKind.Box) { Key = "web" + index++, Background = Black }.With(
                new UiNode(UiKind.Box) { Key = "header", Background = Primary }.With(header),
                new UiNode(UiKind.Box) { Key = "content" }.With(
                    new UiNode(UiKind.Box) { Key = "intro", Text = "Connectivity to this web is maintained by the following servers...", Wrap = true },
                    servers)));
        }
        return list;
    }

    /// <summary>
    /// tg's <c>Experiment</c> (ExperimentConfigure.tsx:178): one experiment's panel from its <c>to_ui_data</c> (name, tag, performance
    /// hint, description, progress stages), its title the button that selects or clears it; <paramref name="children"/> go where tg
    /// renders <c>props.children</c>, between the description and the stages.
    /// </summary>
    public static UiNode Experiment(TguiContext c, UiValue exp, UiNode children = null) => Experiment(c, exp, children, true);

    /// <summary>tg's <c>Experiment</c> with its <c>can_select</c> prop: false shows the title as a plain bar (the operating computer).</summary>
    // tg: ExperimentConfigure.tsx:178
    public static UiNode Experiment(TguiContext c, UiValue exp, UiNode children, bool canSelect)
    {
        string name = exp.Str("name");
        string description = exp.Str("description");
        string tag = exp.Str("tag");
        bool selected = exp.Truthy("selected");
        string performanceHint = exp.Str("performance_hint");
        string expRef = exp.Str("ref");
        IReadOnlyList<UiValue> progress = exp.Arr("progress");

        // tg: ExperimentConfigure.tsx:144  ExperimentTitleElement
        UiNode title;
        if (!canSelect)
            title = new UiNode(UiKind.Box) { Key = "title", Background = Primary, Bold = true, Text = name };
        else
        {
            title = new UiNode(UiKind.Button)
            {
                Key = "title",
                Fluid = true,
                Bold = true,
                Selected = selected,
                Text = name,
                OnClick = () =>
                {
                    if (selected)
                        c.Act("clear_experiment");
                    else
                        c.Act("select_experiment", ("ref", expRef));
                },
            };
        }

        var stages = new UiNode(UiKind.Table) { Key = "stages" };
        for (int i = 0; i < progress.Count; i++)
            stages.With(ExperimentStageRow(progress[i], i));

        return new UiNode(UiKind.Box) { Key = "exp:" + expRef, Background = Black }.With(
            new UiNode(UiKind.Stack) { Key = "titlebar", Align = "center" }.With(
                new UiNode(UiKind.StackItem) { Key = "title", Grow = 1 }.With(title),
                new UiNode(UiKind.StackItem) { Key = "tag", Text = tag, Color = "rgba(255, 255, 255, 0.5)" },
                new UiNode(UiKind.StackItem) { Key = "hint" }.With(
                    new UiNode(UiKind.Icon) { Key = "hint", Icon = "question-circle", Color = "rgba(255, 255, 255, 0.5)", Tooltip = performanceHint })),
            new UiNode(UiKind.Box) { Key = "content" }.With(
                new UiNode(UiKind.Box) { Key = "description", Text = description, Wrap = true },
                children,
                stages));
    }

    // tg: ExperimentConfigure.tsx:224
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        bool alwaysActive = data.Truthy("always_active");
        bool hasStartCallback = data.Truthy("has_start_callback");

        IReadOnlyList<UiValue> techwebs = data.Arr("techwebs");

        // tg: ExperimentConfigure.tsx:230  data.experiments.sort((a, b) => a.name.localeCompare(b.name))
        List<UiValue> experiments = data.Arr("experiments").OrderBy(e => e.Str("name"), StringComparer.CurrentCultureIgnoreCase).ToList();

        // Group servers together by web
        var webs = new List<(string WebId, List<UiValue> Techwebs)>();
        foreach (UiValue x in techwebs)
        {
            if (x.Field("web_id") is { IsNull: false })
            {
                string webId = x.Str("web_id");
                int found = webs.FindIndex(w => w.WebId == webId);
                if (found < 0)
                    webs.Add((webId, new List<UiValue> { x }));
                else
                    webs[found].Techwebs.Add(x);
            }
        }

        string textContent;
        if (experiments.Count == 0)
            textContent = "No experiments found on this web";
        else if (alwaysActive)
            textContent = "This device is configured to attempt to perform all available experiments, so no further configuration is necessary.";
        else
            textContent = "Select one of the following experiments...";

        var servers = new UiNode(UiKind.Section) { Key = "servers", Title = "Servers" }.With(
            new UiNode(UiKind.Box) { Key = "intro", Color = "label", Text = webs.Count > 0 ? "Please select a techweb to connect to..." : "Found no servers connected to a techweb!" });
        foreach ((string webId, List<UiValue> webTechwebs) in webs)
            servers.With(new UiNode(UiKind.Box) { Key = "web:" + webId }.With(TechwebServer(c, webTechwebs)));

        var column = new UiNode(UiKind.Stack) { Key = "column", Vertical = true };
        if (techwebs.Any(e => e.Truthy("selected")))
        {
            var section = new UiNode(UiKind.Section) { Key = "experiments", Title = "Experiments" }.With(
                new UiNode(UiKind.Box) { Key = "text", Color = "label", Text = textContent, Wrap = true });
            foreach (UiValue exp in experiments)
                section.With(Experiment(c, exp));
            column.With(new UiNode(UiKind.StackItem) { Key = "experiments" }.With(section));
        }

        if (hasStartCallback)
        {
            column.With(new UiNode(UiKind.StackItem) { Key = "perform" }.With(new UiNode(UiKind.Button)
            {
                Key = "perform",
                Fluid = true,
                TextAlign = "center",
                Disabled = !experiments.Any(e => e.Truthy("selected")),
                Icon = "flask",
                Text = "Perform Experiment",
                OnClick = () => c.Act("start_experiment_callback"),
            }));
        }

        // tg: ExperimentConfigure.tsx:256  <Window width={600} height={735}> <Window.Content scrollable>: the content in the window's own area
        return c.Window(600, 735, null, servers, column);
    }
}
