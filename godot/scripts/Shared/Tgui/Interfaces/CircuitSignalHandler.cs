using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/CircuitSignalHandler.tsx: the admin window that
// registers a signal id for the signal handler component, with its responses (bitflags) and parameters (port types). The lists
// being written live in the window's local state, as the class component's state does in tg. Engine-free (compiled into the tests
// too).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class CircuitSignalHandler
{
    private sealed class Entry
    {
        public string Name;
        public string Option;
        public int Bitflag;
    }

    // tg: CircuitSignalHandler.tsx:52  this.bitflags[1 << i] = `Flag ${i + 1}`
    private static string FlagName(int bitflag)
    {
        for (int i = 0; i < 24; i++)
            if (1 << i == bitflag)
                return $"Flag {i + 1}";
        return null;
    }

    // tg: CircuitSignalHandler.tsx:59
    public static UiNode Render(TguiContext c)
    {
        UiValue data = c.Data;
        IReadOnlyList<string> global_port_types = data.Arr("global_port_types").Select(t => t.AsStr()).ToList();
        string signal_id = c.UseState("signal_id", "signal_id");
        bool global = c.UseState("global", false);
        List<Entry> responseList = c.UseState("responseList", new List<Entry>());
        List<Entry> parameterList = c.UseState("parameterList", new List<Entry>());
        void Changed()
        {
            c.SetState("responseList", responseList);
            c.SetState("parameterList", parameterList);
        }

        UiNode AddButton(string key, string text, System.Action onClick) =>
            new UiNode(UiKind.StackItem) { Key = key }.With(new UiNode(UiKind.Button)
            {
                Key = key,
                Fluid = true,
                Text = text,
                Color = "good",
                Icon = "plus",
                OnClick = onClick,
            });

        UiNode responses = new UiNode(UiKind.Section) { Key = "responses", Title = "Responses", Fill = true, Scrollable = true }.With(
            new UiNode(UiKind.Stack) { Key = "list", Vertical = true }.With(
                responseList.Select((val, index) => EntryNode(c, "response" + index, val.Name, FlagName(val.Bitflag), null,
                        onRemove: () => { responseList.RemoveAt(index); Changed(); },
                        onChange: value => { val.Name = value; Changed(); },
                        onSetOption: null))
                    .Append(AddButton("add_response", "Add Response", () =>
                    {
                        // tg's Object.keys of the bitflag map: the flags in order, the next unused one by the list's length
                        int count = responseList.Count;
                        responseList.Add(new Entry { Name = "Response", Bitflag = count < 24 ? 1 << count : 0 });
                        Changed();
                    }))));

        UiNode parameters = new UiNode(UiKind.Section) { Key = "parameters", Title = "Parameters", Fill = true, Scrollable = true }.With(
            new UiNode(UiKind.Stack) { Key = "list", Vertical = true }.With(
                parameterList.Select((val, index) => EntryNode(c, "parameter" + index, val.Name, val.Option, global_port_types,
                        onRemove: () => { parameterList.RemoveAt(index); Changed(); },
                        onChange: value => { val.Name = value; Changed(); },
                        onSetOption: type => { val.Option = type; Changed(); }))
                    .Append(AddButton("add_parameter", "Add Parameter", () =>
                    {
                        parameterList.Add(new Entry { Name = "Parameter", Option = global_port_types.FirstOrDefault() });
                        Changed();
                    }))));

        // <Window width={600} height={300}>
        return c.Window(600, 300, null, new UiNode(UiKind.Stack) { Key = "root", Vertical = true, Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "id" }.With(new UiNode(UiKind.Stack) { Key = "id", Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "signal_id", Grow = 1 }.With(new UiNode(UiKind.Input)
                {
                    Key = "signal_id",
                    Tooltip = "Signal ID",
                    TextValue = signal_id,
                    Fluid = true,
                    OnInput = value => c.SetState("signal_id", value),
                }),
                new UiNode(UiKind.StackItem) { Key = "global" }.With(new UiNode(UiKind.Button)
                {
                    Key = "global",
                    Checkbox = global,
                    Text = "Global",
                    OnClick = () => c.SetState("global", !global),
                }))),
            new UiNode(UiKind.StackItem) { Key = "lists", Grow = 1 }.With(new UiNode(UiKind.Stack) { Key = "lists", Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "responses", Grow = 1 }.With(responses),
                new UiNode(UiKind.StackItem) { Key = "parameters", Grow = 1 }.With(parameters))),
            new UiNode(UiKind.StackItem) { Key = "submit" }.With(new UiNode(UiKind.Button)
            {
                Key = "submit",
                Text = "Submit",
                TextAlign = "center",
                Fluid = true,
                OnClick = () => c.Act("add_new_id",
                    ("signal_id", signal_id),
                    ("responses", UiValue.ListOf(responseList.Select(r => UiValue.ObjectOf(
                        ("name", UiValue.Of(r.Name)), ("bitflag", UiValue.Of(r.Bitflag)))))),
                    ("parameters", UiValue.ListOf(parameterList.Select(p => UiValue.ObjectOf(
                        ("name", UiValue.Of(p.Name)), ("datatype", UiValue.Of(p.Option)))))),
                    ("global", global)),
            })));
    }

    // tg: CircuitSignalHandler.tsx:198  Entry
    private static UiNode EntryNode(TguiContext c, string key, string name, string current_option, IReadOnlyList<string> options,
        System.Action onRemove, System.Action<string> onChange, System.Action<string> onSetOption)
    {
        options ??= System.Array.Empty<string>();
        UiNode option = options.Count > 0
            ? new UiNode(UiKind.Dropdown) { Key = "option", TextValue = current_option, Options = options, OnSelected = onSetOption }
            : new UiNode(UiKind.Box) { Key = "option", TextAlign = "center", Text = current_option };
        return new UiNode(UiKind.StackItem) { Key = key }.With(new UiNode(UiKind.Stack) { Key = "entry" }.With(
            new UiNode(UiKind.StackItem) { Key = "name", Grow = 1 }.With(new UiNode(UiKind.Input)
            {
                Key = "name",
                Tooltip = "Name",
                TextValue = name,
                Fluid = true,
                OnInput = onChange,
            }),
            new UiNode(UiKind.StackItem) { Key = "option" }.With(option),
            new UiNode(UiKind.StackItem) { Key = "remove" }.With(new UiNode(UiKind.Button)
            {
                Key = "remove",
                Icon = "times",
                Color = "red",
                OnClick = onRemove,
            })));
    }
}
