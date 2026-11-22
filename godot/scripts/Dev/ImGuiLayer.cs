using System.Runtime.InteropServices;
using Godot;
using ImGuiNET;
using NVector2 = System.Numerics.Vector2;

namespace Anomaly.Client.Dev;

// The dev menu's immediate-mode UI (the owner, 2026-10-13: "a separate dev menu on imgui"): Dear ImGui through the ImGui.NET
// package (MIT; its native cimgui library ships in the package), drawn by this small Godot backend: one canvas item per
// draw command under a canvas layer above the HUD, clipped to the command's rectangle, the font atlas an ImageTexture. The
// layer draws every frame (windows pinned open stay on screen); it takes the mouse only while Capture is on (the dev menu
// open, the mouse free), and the keyboard only while one of its text fields is typed in. Other textures (the spawn panel's
// icons) are registered by id and drawn without filtering (tg's pixel art).

/// <summary>Dear ImGui on a Godot canvas layer: <see cref="Build"/> runs between ImGui's NewFrame and Render each frame.</summary>
public partial class ImGuiLayer : CanvasLayer
{
    private const int FontTextureId = 1;

    private IntPtr _context;
    private ImageTexture _fontTexture;
    private readonly Dictionary<IntPtr, Texture2D> _textures = new();
    private readonly List<Rect2> _windowRects = new();
    private bool _popupOpen;
    private long _nextTextureId = FontTextureId + 1;
    private Rid _root;
    private readonly List<Rid> _items = new();
    private float _fontScale;
    private float _pendingScale = 1f;

    // Scratch arrays of the conversion, grown as needed.
    private Vector2[] _points = Array.Empty<Vector2>();
    private Vector2[] _uvs = Array.Empty<Vector2>();
    private Color[] _colors = Array.Empty<Color>();

    /// <summary>Builds the frame's windows (between NewFrame and Render).</summary>
    public event Action Build;

    /// <summary>
    /// ImGui takes the mouse and the keys over its windows (the dev menu is open and the mouse is free). While it is off, ImGui
    /// holds no key or button (see <see cref="_Process"/>).
    /// </summary>
    public bool Capture
    {
        get => _capture;
        set
        {
            _capture = value;
            if (!value) WantsText = false;
        }
    }

    private bool _capture;

    // ANOMALY_IMGUI_TRACE=1: the input state on every change (a debugging aid)
    private static readonly bool Trace = System.Environment.GetEnvironmentVariable("ANOMALY_IMGUI_TRACE") == "1";
    private string _lastTrace;

    /// <summary>A text field of ImGui is being typed in: the keyboard is ImGui's, the game reads no keys (not even held ones).</summary>
    public bool WantsText { get; private set; }

    /// <summary>The mouse is over one of ImGui's windows (or dragging one) this frame: a click there is ImGui's, not the world's.</summary>
    public bool WantsMouse { get; private set; }

    /// <summary>True when <paramref name="screen"/> is inside what one of ImGui's windows drew last frame (the bounds of its draw list).</summary>
    public bool IsOverWindow(Vector2 screen)
    {
        foreach (Rect2 r in _windowRects)
        {
            if (r.HasPoint(screen)) return true;
        }
        return false;
    }

    /// <summary>Registers a texture for <c>ImGui.Image</c> and the draw lists' <c>AddImage</c>; the id stays valid until <see cref="RemoveTexture"/>.</summary>
    public IntPtr AddTexture(Texture2D texture)
    {
        var id = (IntPtr)_nextTextureId++;
        _textures[id] = texture;
        return id;
    }

    /// <summary>Forgets a texture of <see cref="AddTexture"/>.</summary>
    public void RemoveTexture(IntPtr id) => _textures.Remove(id);

    /// <summary>The size of the UI (the font's pixel size over ImGui's 13 and the style's sizes); applied before the next frame.</summary>
    public float UiScale
    {
        get => _pendingScale;
        set => _pendingScale = Math.Clamp(value, 0.75f, 4f);
    }

    public override void _Ready()
    {
        Layer = 100;
        Name = "ImGui";
        _context = ImGui.CreateContext();
        ImGui.SetCurrentContext(_context);
        ImGuiIOPtr io = ImGui.GetIO();
        unsafe
        {
            // D282: the dev menu's windows keep their place and size in the user folder (none for a scripted run)
            io.NativePtr->IniFilename = App.Instance?.Args.Automated != false
                ? null
                : (byte*)System.Runtime.InteropServices.Marshal.StringToCoTaskMemUTF8(ProjectSettings.GlobalizePath("user://imgui.ini")); // kept for the program's life
        }
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
        // no keyboard navigation: a focused window would claim every key, and the game's keys (F8, Esc, walking) must pass
        ImGui.StyleColorsDark();
        _root = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(_root, GetCanvas());
    }

    public override void _ExitTree()
    {
        foreach (Rid item in _items)
            RenderingServer.FreeRid(item);
        _items.Clear();
        if (_root.IsValid)
            RenderingServer.FreeRid(_root);
        if (_context != IntPtr.Zero)
        {
            ImGui.DestroyContext(_context);
            _context = IntPtr.Zero;
        }
    }

    public override void _Process(double delta)
    {
        using var frameProbe = Anomaly.Client.Dev.FrameProbe.Scope("ImGuiLayer._Process");
        if (_context == IntPtr.Zero) return;
        ImGui.SetCurrentContext(_context);
        if (_pendingScale != _fontScale)
            Rescale(_pendingScale);
        ImGuiIOPtr io = ImGui.GetIO();
        Vector2 size = GetViewport().GetVisibleRect().Size;
        io.DisplaySize = new NVector2(size.X, size.Y);
        io.DisplayFramebufferScale = NVector2.One;
        io.DeltaTime = Math.Max((float)delta, 1e-4f);
        if (Capture)
        {
            Vector2 mouse = GetViewport().GetMousePosition();
            io.AddMousePosEvent(mouse.X, mouse.Y);
        }
        else
        {
            io.AddMousePosEvent(-float.MaxValue, -float.MaxValue); // the mouse is the game's: no hover, no click
        }
        io.MouseDrawCursor = false;
        ImGui.NewFrame();
        if (!Capture)
        {
            // the keys and buttons go to the game: their releases never reach ImGui, so whatever it holds is forgotten, after
            // NewFrame has applied the queued events (the Esc that closed the menu is still queued when the menu closes; held,
            // it cancelled the next text field at once: the owner, 2026-10-14, "the search takes no focus the second time")
            io.ClearInputKeys();
            io.ClearInputMouse();
        }
        WantsMouse = Capture && io.WantCaptureMouse;
        try
        {
            Build?.Invoke();
            _popupOpen = ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel);
            WantsText = Capture && io.WantTextInput;
            if (Trace)
            {
                string state = $"cap={Capture} text={io.WantTextInput} active={ImGui.IsAnyItemActive()} focusedWin={ImGui.IsWindowFocused(ImGuiFocusedFlags.AnyWindow)} hoverWin={ImGui.IsWindowHovered(ImGuiHoveredFlags.AnyWindow)} mouse0={ImGui.IsMouseDown(ImGuiMouseButton.Left)} esc={ImGui.IsKeyDown(ImGuiKey.Escape)} popup={_popupOpen} wantMouse={io.WantCaptureMouse} pos={io.MousePos.X:0},{io.MousePos.Y:0}";
                if (state != _lastTrace) { GD.Print("[imgui] " + state); _lastTrace = state; }
            }
        }
        finally
        {
            ImGui.Render();
        }
        Draw(ImGui.GetDrawData());
    }

    public override void _Input(InputEvent e)
    {
        if (!Capture || _context == IntPtr.Zero) return;
        ImGui.SetCurrentContext(_context);
        ImGuiIOPtr io = ImGui.GetIO();
        bool handled = false;
        switch (e)
        {
            case InputEventMouseMotion motion:
                io.AddMousePosEvent(motion.Position.X, motion.Position.Y);
                handled = io.WantCaptureMouse && IsOverWindow(motion.Position);
                break;
            case InputEventMouseButton button:
                // the click where it happened (a scripted click is at the centre, wherever the mouse is), then the button
                io.AddMousePosEvent(button.Position.X, button.Position.Y);
                switch (button.ButtonIndex)
                {
                    case MouseButton.Left: io.AddMouseButtonEvent(0, button.Pressed); break;
                    case MouseButton.Right: io.AddMouseButtonEvent(1, button.Pressed); break;
                    case MouseButton.Middle: io.AddMouseButtonEvent(2, button.Pressed); break;
                    case MouseButton.WheelUp: if (button.Pressed) io.AddMouseWheelEvent(0, button.Factor > 0 ? button.Factor : 1); break;
                    case MouseButton.WheelDown: if (button.Pressed) io.AddMouseWheelEvent(0, -(button.Factor > 0 ? button.Factor : 1)); break;
                    case MouseButton.WheelLeft: if (button.Pressed) io.AddMouseWheelEvent(button.Factor > 0 ? button.Factor : 1, 0); break;
                    case MouseButton.WheelRight: if (button.Pressed) io.AddMouseWheelEvent(-(button.Factor > 0 ? button.Factor : 1), 0); break;
                }
                // over a window, or a release while ImGui holds the mouse (a drag that left the window): ImGui's
                // a press while a popup is open closes it (ImGui's), as a click outside a dropdown does
                handled = IsOverWindow(button.Position) || (!button.Pressed && io.WantCaptureMouse) || (button.Pressed && _popupOpen);
                break;
            case InputEventPanGesture pan:
                // the trackpad's two-finger scroll on macOS (no wheel buttons): ImGui's wheel, a notch per ~4 units
                io.AddMousePosEvent(pan.Position.X, pan.Position.Y);
                io.AddMouseWheelEvent(-pan.Delta.X * 0.25f, -pan.Delta.Y * 0.25f);
                handled = IsOverWindow(pan.Position);
                break;
            case InputEventKey key:
                io.AddKeyEvent(ImGuiKey.ModCtrl, key.CtrlPressed);
                io.AddKeyEvent(ImGuiKey.ModShift, key.ShiftPressed);
                io.AddKeyEvent(ImGuiKey.ModAlt, key.AltPressed);
                io.AddKeyEvent(ImGuiKey.ModSuper, key.MetaPressed);
                ImGuiKey mapped = Map(key.Keycode);
                if (mapped != ImGuiKey.None)
                    io.AddKeyEvent(mapped, key.Pressed);
                if (key.Pressed && key.Unicode >= 32 && key.Unicode != 127)
                    io.AddInputCharacter((uint)key.Unicode);
                // only while a text field is being typed in, and the Esc that closes an open dropdown (not the whole menu)
                handled = io.WantTextInput || (_popupOpen && key.Keycode == Key.Escape);
                break;
        }
        if (Trace && e is InputEventMouseButton or InputEventKey)
            GD.Print($"[imgui] event {e.AsText()} pressed={e.IsPressed()} handled={handled} over={(e is InputEventMouseButton mb ? IsOverWindow(mb.Position) : false)}");
        if (handled)
            GetViewport().SetInputAsHandled();
    }

    // The font atlas at 13 px times the scale, and the style's sizes scaled from the dark style's.
    private void Rescale(float scale)
    {
        _fontScale = scale;
        ImGuiIOPtr io = ImGui.GetIO();
        io.Fonts.Clear();
        unsafe
        {
            ImFontConfigPtr config = ImGuiNative.ImFontConfig_ImFontConfig();
            config.SizePixels = 13f * scale;
            config.OversampleH = 2;
            config.OversampleV = 2;
            io.Fonts.AddFontDefault(config);
            config.Destroy();
        }
        io.Fonts.GetTexDataAsRGBA32(out IntPtr pixels, out int width, out int height, out int bytesPerPixel);
        var data = new byte[width * height * bytesPerPixel];
        Marshal.Copy(pixels, data, 0, data.Length);
        Image image = Image.CreateFromData(width, height, false, Image.Format.Rgba8, data);
        _fontTexture = ImageTexture.CreateFromImage(image);
        io.Fonts.SetTexID(FontTextureId);
        io.Fonts.ClearTexData();
        ImGuiStylePtr style = ImGui.GetStyle();
        ImGui.StyleColorsDark();
        unsafe
        {
            // the style's sizes from a fresh default, scaled
            ImGuiStyle* fresh = ImGuiNative.ImGuiStyle_ImGuiStyle();
            *style.NativePtr = *fresh;
            ImGuiNative.ImGuiStyle_destroy(fresh);
        }
        ImGui.StyleColorsDark();
        style.Colors[(int)ImGuiCol.WindowBg].W = 0.98f; // the debug HUD's text behind the menu does not show through
        style.WindowRounding = 4f;
        style.FrameRounding = 3f;
        style.ScaleAllSizes(scale);
    }

    // One canvas item per draw command, in order (a later item draws over an earlier one), clipped to its rectangle.
    private unsafe void Draw(ImDrawDataPtr data)
    {
        int used = 0;
        _windowRects.Clear();
        for (int n = 0; n < data.CmdListsCount; n++)
        {
            ImDrawListPtr list = data.CmdLists[n];
            // the window's bounds: where its vertices are, within what its commands may draw in (a root window's frame is
            // clipped to the whole screen, so the clip rectangles alone would cover it; the background list has no vertices)
            Rect2 drawable = default;
            for (int c = 0; c < list.CmdBuffer.Size; c++)
            {
                var r = list.CmdBuffer[c].ClipRect;
                if (r.Z <= r.X || r.W <= r.Y) continue;
                var rect = new Rect2(r.X, r.Y, r.Z - r.X, r.W - r.Y);
                drawable = drawable.Size == Vector2.Zero ? rect : drawable.Merge(rect);
            }
            if (list.VtxBuffer.Size > 0 && drawable.Size != Vector2.Zero)
            {
                var first = (ImDrawVert*)list.VtxBuffer.Data;
                float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                for (int i = 0; i < list.VtxBuffer.Size; i++)
                {
                    NVector2 p = first[i].pos;
                    minX = Math.Min(minX, p.X);
                    minY = Math.Min(minY, p.Y);
                    maxX = Math.Max(maxX, p.X);
                    maxY = Math.Max(maxY, p.Y);
                }
                Rect2 bounds = new Rect2(minX, minY, maxX - minX, maxY - minY).Intersection(drawable);
                if (bounds.Size.X > 0 && bounds.Size.Y > 0)
                    _windowRects.Add(bounds);
            }
            int vertexCount = list.VtxBuffer.Size;
            if (_points.Length < vertexCount)
            {
                int grow = Math.Max(vertexCount, _points.Length * 2);
                _points = new Vector2[grow];
                _uvs = new Vector2[grow];
                _colors = new Color[grow];
            }
            var vertices = (ImDrawVert*)list.VtxBuffer.Data;
            for (int i = 0; i < vertexCount; i++)
            {
                ImDrawVert v = vertices[i];
                _points[i] = new Vector2(v.pos.X, v.pos.Y);
                _uvs[i] = new Vector2(v.uv.X, v.uv.Y);
                uint c = v.col;
                _colors[i] = new Color((c & 0xFF) / 255f, ((c >> 8) & 0xFF) / 255f, ((c >> 16) & 0xFF) / 255f, ((c >> 24) & 0xFF) / 255f);
            }
            var indices = (ushort*)list.IdxBuffer.Data;
            for (int c = 0; c < list.CmdBuffer.Size; c++)
            {
                ImDrawCmdPtr cmd = list.CmdBuffer[c];
                if (cmd.UserCallback != IntPtr.Zero || cmd.ElemCount == 0) continue;
                // The command's vertices only (Godot copies the arrays it is given), re-indexed from 0.
                int first = int.MaxValue, last = -1;
                for (uint k = 0; k < cmd.ElemCount; k++)
                {
                    int index = indices[cmd.IdxOffset + k] + (int)cmd.VtxOffset;
                    if (index < first) first = index;
                    if (index > last) last = index;
                }
                int count = last - first + 1;
                var cmdIndices = new int[cmd.ElemCount];
                for (uint k = 0; k < cmd.ElemCount; k++)
                    cmdIndices[k] = indices[cmd.IdxOffset + k] + (int)cmd.VtxOffset - first;
                var points = new Vector2[count];
                var uvs = new Vector2[count];
                var colors = new Color[count];
                Array.Copy(_points, first, points, 0, count);
                Array.Copy(_uvs, first, uvs, 0, count);
                Array.Copy(_colors, first, colors, 0, count);

                Rid item = ItemAt(used);
                RenderingServer.CanvasItemSetDrawIndex(item, used++);
                RenderingServer.CanvasItemClear(item);
                RenderingServer.CanvasItemSetVisible(item, true);
                var clip = new Rect2(cmd.ClipRect.X, cmd.ClipRect.Y, cmd.ClipRect.Z - cmd.ClipRect.X, cmd.ClipRect.W - cmd.ClipRect.Y);
                RenderingServer.CanvasItemSetCustomRect(item, true, clip);
                RenderingServer.CanvasItemSetClip(item, true);
                Rid texture = default;
                bool font = cmd.TextureId == FontTextureId;
                if (font && _fontTexture != null)
                    texture = _fontTexture.GetRid();
                else if (!font && _textures.TryGetValue(cmd.TextureId, out Texture2D image))
                    texture = image.GetRid();
                RenderingServer.CanvasItemSetDefaultTextureFilter(item, font ? RenderingServer.CanvasItemTextureFilter.Linear : RenderingServer.CanvasItemTextureFilter.Nearest);
                RenderingServer.CanvasItemAddTriangleArray(item, cmdIndices, points, colors, uvs, null, null, texture);
            }
        }
        for (int i = used; i < _items.Count; i++)
        {
            RenderingServer.CanvasItemClear(_items[i]);
            RenderingServer.CanvasItemSetVisible(_items[i], false);
        }
    }

    private Rid ItemAt(int index)
    {
        while (_items.Count <= index)
        {
            Rid item = RenderingServer.CanvasItemCreate();
            RenderingServer.CanvasItemSetParent(item, _root);
            _items.Add(item);
        }
        return _items[index];
    }

    private static ImGuiKey Map(Key key) => key switch
    {
        Key.Tab => ImGuiKey.Tab,
        Key.Left => ImGuiKey.LeftArrow,
        Key.Right => ImGuiKey.RightArrow,
        Key.Up => ImGuiKey.UpArrow,
        Key.Down => ImGuiKey.DownArrow,
        Key.Pageup => ImGuiKey.PageUp,
        Key.Pagedown => ImGuiKey.PageDown,
        Key.Home => ImGuiKey.Home,
        Key.End => ImGuiKey.End,
        Key.Insert => ImGuiKey.Insert,
        Key.Delete => ImGuiKey.Delete,
        Key.Backspace => ImGuiKey.Backspace,
        Key.Space => ImGuiKey.Space,
        Key.Enter => ImGuiKey.Enter,
        Key.KpEnter => ImGuiKey.KeypadEnter,
        Key.Escape => ImGuiKey.Escape,
        Key.A => ImGuiKey.A,
        Key.C => ImGuiKey.C,
        Key.V => ImGuiKey.V,
        Key.X => ImGuiKey.X,
        Key.Y => ImGuiKey.Y,
        Key.Z => ImGuiKey.Z,
        _ => ImGuiKey.None,
    };
}
