using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Tgui.Interfaces;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/CameraConsole.tsx (M10 slice D, D268: the security
// camera console). Engine-free (compiled into the tests too). The ByondUi map is the client's second 3D view from the active camera
// (UiKind.ByondUi); the "Track Person" button (can_spy: SyndEye's and the other spying programs') came with the person tracking
// (silicons SI4).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class CameraConsole
{
    /// <summary>tg's <c>prevNextCamera</c>: the previous and next camera refs around the active one, or none.</summary>
    // tg: CameraConsole.tsx:33
    public static (string Prev, string Next) PrevNextCamera(IReadOnlyList<UiValue> cameras, UiValue activeCamera)
    {
        if (activeCamera == null || activeCamera.IsNull || cameras.Count < 2)
            return (null, null);

        string active = activeCamera.Str("ref");
        int index = -1;
        for (int i = 0; i < cameras.Count; i++)
            if (cameras[i].Str("ref") == active)
            {
                index = i;
                break;
            }

        if (index == -1) // Current camera is not in the list
            return (cameras[^1].Str("ref"), cameras[0].Str("ref"));
        if (index == 0) // First camera
            return cameras.Count == 2 ? (cameras[1].Str("ref"), cameras[1].Str("ref")) : (cameras[^1].Str("ref"), cameras[index + 1].Str("ref"));
        if (index == cameras.Count - 1) // Last camera
            return cameras.Count == 2 ? (cameras[0].Str("ref"), cameras[0].Str("ref")) : (cameras[index - 1].Str("ref"), cameras[0].Str("ref"));
        return (cameras[index - 1].Str("ref"), cameras[index + 1].Str("ref")); // Middle camera
    }

    /// <summary>tg's <c>selectCameras</c>: the named cameras matching the search, sorted by name.</summary>
    // tg: CameraConsole.tsx:72
    public static List<UiValue> SelectCameras(IReadOnlyList<UiValue> cameras, string searchText = "")
    {
        IEnumerable<UiValue> queried = cameras.Where(camera => !string.IsNullOrEmpty(camera.Str("name")));
        if (!string.IsNullOrEmpty(searchText))
        {
            Func<UiValue, bool> testSearch = TguiFormat.CreateSearch<UiValue>(searchText, camera => camera.Str("name"));
            queried = queried.Where(testSearch);
        }
        return queried.OrderBy(c => c.Str("name"), StringComparer.Ordinal).ToList();
    }

    // tg: CameraConsole.tsx:86
    public static UiNode Render(TguiContext c) => c.Window(850, 708, null, Content(c));

    /// <summary>tg's <c>CameraContent</c>: the camera list and the map view, shared with NtosSecurEye.</summary>
    // tg: CameraConsole.tsx:94
    public static UiNode Content(TguiContext c)
    {
        string searchText = c.UseState("searchText", "");
        return new UiNode(UiKind.Stack) { Fill = true }.With(
            new UiNode(UiKind.StackItem) { Key = "selector", Grow = 1 }.With(CameraSelector(c, searchText)),
            new UiNode(UiKind.StackItem) { Key = "controls", Grow = 3 }.With(CameraControls(c, searchText)));
    }

    // tg: CameraConsole.tsx:115
    private static UiNode CameraSelector(TguiContext c, string searchText)
    {
        UiValue activeCamera = c.Data.Get("activeCamera");
        List<UiValue> cameras = SelectCameras(c.Data.Get("cameras")?.Items ?? Array.Empty<UiValue>(), searchText);
        string active = activeCamera == null || activeCamera.IsNull ? null : activeCamera.Str("ref");
        return new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "search" }.With(new UiNode(UiKind.Input)
            {
                Key = "searchText",
                AutoFocus = true,
                Fluid = true,
                Tooltip = "Search for a camera",
                TextValue = searchText,
                OnInput = v => c.SetState("searchText", v),
            }),
            new UiNode(UiKind.StackItem) { Key = "list", Grow = 1 }.With(
                new UiNode(UiKind.Section) { Fill = true, Scrollable = true }.With(cameras.Select(camera => new UiNode(UiKind.Button)
                {
                    Key = "camera:" + camera.Str("ref"),
                    Text = camera.Str("name"),
                    Tooltip = camera.Str("name"),
                    Fluid = true,
                    Color = "transparent",
                    Selected = active == camera.Str("ref"),
                    OnClick = () => c.Act("switch_camera", ("camera", camera.Str("ref"))),
                }))));
    }

    // tg: CameraConsole.tsx:163
    private static UiNode CameraControls(TguiContext c, string searchText)
    {
        UiValue activeCamera = c.Data.Get("activeCamera");
        string mapRef = c.Data.Str("mapRef");
        List<UiValue> cameras = SelectCameras(c.Data.Get("cameras")?.Items ?? Array.Empty<UiValue>(), searchText);
        (string prevCamera, string nextCamera) = PrevNextCamera(cameras, activeCamera);
        bool live = activeCamera != null && !activeCamera.IsNull && activeCamera.Truthy("status");

        return new UiNode(UiKind.Section) { Fill = true }.With(new UiNode(UiKind.Stack) { Fill = true, Vertical = true }.With(
            new UiNode(UiKind.StackItem) { Key = "bar" }.With(new UiNode(UiKind.Stack) { Fill = true }.With(
                new UiNode(UiKind.StackItem) { Key = "notice", Grow = 1 }.With(live
                    ? new UiNode(UiKind.NoticeBox) { Color = "info", Text = activeCamera.Str("name") }
                    : new UiNode(UiKind.NoticeBox) { Color = "danger", Text = "No input signal" }),
                // tg: CameraConsole.tsx:181
                new UiNode(UiKind.StackItem) { Key = "spy" }.With(c.Data.Truthy("can_spy")
                    ? new UiNode(UiKind.Button) { Key = "track", Icon = "magnifying-glass", Tooltip = "Track Person", OnClick = () => c.Act("start_tracking") }
                    : null),
                new UiNode(UiKind.StackItem) { Key = "prev" }.With(new UiNode(UiKind.Button)
                {
                    Key = "prevCamera",
                    Icon = "chevron-left",
                    Disabled = prevCamera == null,
                    OnClick = () => c.Act("switch_camera", ("camera", prevCamera)),
                }),
                new UiNode(UiKind.StackItem) { Key = "next" }.With(new UiNode(UiKind.Button)
                {
                    Key = "nextCamera",
                    Icon = "chevron-right",
                    Disabled = nextCamera == null,
                    OnClick = () => c.Act("switch_camera", ("camera", nextCamera)),
                }))),
            new UiNode(UiKind.StackItem) { Key = "map", Grow = 1 }.With(new UiNode(UiKind.ByondUi)
            {
                Key = "map:" + mapRef,
                MapRef = mapRef,
                MapFrom = live ? activeCamera.Str("ref") : null,
                Width = "100%",
                Height = "100%",
            })));
    }
}
