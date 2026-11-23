using System.Linq;
using System.Globalization;
using Godot;
using ImGuiNET;
using Anomaly.Client.Dev;
using Anomaly.Client.Hud;
using Anomaly.Client.Render;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.Light;
using Anomaly.Protocol;
using NVector2 = System.Numerics.Vector2;
using NVector3 = System.Numerics.Vector3;
using NVector4 = System.Numerics.Vector4;

namespace Anomaly.Client;

// The dev menu (the owner, 2026-10-13; docs/ai/specs/apps.md 2.17 and 2.18): an ImGui window on the | key (D137; F8 before) with the spawn panel, the
// delete tool and the gas tools (GameScene.DevTools.cs), the gas overlay (the air of every cell around the player from the
// server, DEV_COMMANDS only), the cell inspector, the gas haze switch, view switches and a few dev commands. While it is open the mouse is free and ImGui takes the clicks on its windows; the inspector stays on
// screen while the overlay is on. The gas haze itself (GasLayer, tg's gas overlays) is the game's, not a dev view.
public partial class GameScene
{
    private const double ProbeSeconds = 0.5;

    private ImGuiLayer _imgui;
    private CellCoordinates _cellCoords;
    private bool _cellCoordsOn;
    private double _nextCoordsRefresh;
    private GasOverlay _gasOverlay;
    private GasLayer _gasLayer;
    private AirflowLayer _airflowLayer;
    private bool _devMenuOpen;
    private bool _gasOverlayOn;
    private double _nextProbeAt;
    private int _overlayModeIndex;
    private int _overlayGasIndex;
    private float _projectileSpeed = 3.9f; // the server's default (DV211)
    private float _runStep = 1.43f, _walkStep = 1.43f; // the server's default footstep (DV233: two strides of 183/256 tile)
    private static readonly string[] OverlayModes = { "Mix of gases", "One gas", "Pressure", "Temperature" };

    private void BuildDevUi(IconLibrary icons)
    {
        _gasLayer = new GasLayer { Name = "Gas" };
        AddChild(_gasLayer);
        _gasLayer.Bind(_feed.Replica, icons);
        _airflowLayer = new AirflowLayer { Name = "Airflow" };
        AddChild(_airflowLayer);
        _gasOverlay = new GasOverlay { Visible = false };
        AddChild(_gasOverlay);
        _cellCoords = new CellCoordinates { Name = "CellCoordinates", Visible = false };
        AddChild(_cellCoords);
        _cellCoords.Skip = (x, y, z) => _map.IsWallCell(x, y, z);
        _playerMarkers = new PlayerMarkers { Name = "PlayerMarkers" };
        AddChild(_playerMarkers);
        ApplyDevOptions(_options.Dev); // the saved settings first: the ANOMALY_ variables below (scripted shots) win
        _airflowLayer.Bind(_feed.Replica, _gasLayer);
        // ANOMALY_CELL_COORDS=1: the cell coordinates on from the start (scripted shots)
        if (System.Environment.GetEnvironmentVariable("ANOMALY_CELL_COORDS") == "1") _cellCoordsOn = true;
        // ANOMALY_PLAYER_MARKERS=1: the player markers on from the start (scripted shots)
        if (System.Environment.GetEnvironmentVariable("ANOMALY_PLAYER_MARKERS") == "1") SetPlayerMarkers(true);
        _imgui = new ImGuiLayer();
        AddChild(_imgui);
        _imgui.UiScale = _options.DevUiScale;
        _imgui.Build += DrawDevUi;
        BuildDevTools(icons);
        _feed.DevGasReportReceived += OnGasReport;
        // ANOMALY_GAS_OVERLAY=mix|gas:ID|pressure|temperature: the overlay on from the start (for scripted shots; apps.md 2.17)
        string start = System.Environment.GetEnvironmentVariable("ANOMALY_GAS_OVERLAY");
        if (!string.IsNullOrEmpty(start))
        {
            _gasOverlayOn = true;
            _gasOverlay.Visible = true;
            if (start.StartsWith("gas:", StringComparison.Ordinal))
            {
                _gasOverlay.Mode = GasOverlayMode.OneGas;
                _gasOverlay.Gas = start.Substring(4);
            }
            else
            {
                _gasOverlay.Mode = start switch { "pressure" => GasOverlayMode.Pressure, "temperature" => GasOverlayMode.Temperature, _ => GasOverlayMode.Mix };
            }
            _overlayModeIndex = (int)_gasOverlay.Mode;
        }
        // ANOMALY_DEV_MENU=spawn[:SEARCH]|gas|overlay|audio|view|commands: the menu open on that tab from the start (scripted shots)
        string menu = System.Environment.GetEnvironmentVariable("ANOMALY_DEV_MENU");
        if (!string.IsNullOrEmpty(menu))
        {
            int colon = menu.IndexOf(':');
            _startTab = colon < 0 ? menu : menu.Substring(0, colon);
            if (colon >= 0) _spawnQuery = menu.Substring(colon + 1);
            CallDeferred(nameof(OpenDevMenuAtStart));
        }
    }

    private string _startTab;
    private bool _devOptionsDirty;

    // The dev menu's saved settings (the owner, 2026-10-14; DevMenuOptions) onto the layers and the menu's state.
    // D315: the server sends the trackers while its dev command is on (dev commands or an admin's debug right)
    private void SetMinimapMobs(bool on)
    {
        _minimapMobs = on;
        SendMobsFeed();
    }

    // the player markers in the world read the same feed
    private void SetPlayerMarkers(bool on)
    {
        _playerMarkersOn = on;
        SendMobsFeed();
    }

    private void SendMobsFeed() => _session.DevCommand(_minimapMobs || _playerMarkersOn ? "minimapmobs on" : "minimapmobs off");

    private void ApplyDevOptions(DevMenuOptions dev)
    {
        _airflowLayer.Enabled = dev.Airflow;
        _airflowLayer.MaxStreams = dev.AirflowMaxStreams;
        _airflowLayer.MaxPerHole = dev.AirflowMaxPerHole;
        _airflowLayer.MaxPerVent = dev.AirflowMaxPerVent;
        _airflowLayer.SpawnDistance = dev.AirflowSpawnDistance;
        _airflowLayer.ShowDebug = dev.AirflowDebug;
        _gasLayer.Visible = dev.GasHaze;
        _gasOverlayOn = dev.GasOverlay;
        _gasOverlay.Visible = dev.GasOverlay;
        _gasOverlay.Mode = (GasOverlayMode)Math.Max(0, Array.IndexOf(DevMenuOptions.OverlayModes, dev.GasOverlayMode));
        _overlayModeIndex = (int)_gasOverlay.Mode;
        _gasOverlay.Gas = dev.GasOverlayGas;
        _gasOverlay.Opacity = dev.GasOverlayOpacityPercent / 100f;
        _gasOverlay.Labels = dev.GasOverlayLabels;
        _gasOverlay.LabelRadius = dev.GasOverlayLabelRadius;
        _cellCoordsOn = dev.CellCoordinates;
        _cellCoords.Radius = dev.CellCoordinatesRadius;
        _ceiling = dev.Ceiling;
        _fullbright = dev.Fullbright;
        _minimapMobs = dev.MinimapMobs;
        _playerMarkersOn = dev.PlayerMarkers;
        if (dev.MinimapMobs || dev.PlayerMarkers)
            SendMobsFeed(); // saved on: asked again at each join
        _names |= dev.NamesOverlay;
        _spawnMapping = dev.SpawnMapping;
        _spawnAbstract = dev.SpawnAbstract;
        _spawnAmount = dev.SpawnAmount;
        _spawnStick = dev.SpawnStick;
        _spawnFavouritesOnly = dev.SpawnFavouritesOnly;
        for (int i = 0; i < dev.SpawnFavourites.Length; i++) // (the editor's fourth list, the areas, is not the dev menu's to keep)
        {
            _favourites[i].Clear();
            _favourites[i].UnionWith(DevMenuOptions.SplitPaths(dev.SpawnFavourites[i]));
        }
        _favouritesVersion++;
        _gasMoles = dev.GasMoles;
        _gasKelvin = dev.GasKelvin;
        _gasRadius = dev.GasRadius;
        ApplyAudioOptions(dev);
    }

    // The menu's state as settings; when they differ from the saved ones, they become them and are saved once no control is
    // held (a slider being dragged changes them every frame).
    private void SyncDevOptions()
    {
        DevMenuOptions dev = _options.Dev.Clone();
        dev.Airflow = _airflowLayer.Enabled;
        dev.AirflowMaxStreams = _airflowLayer.MaxStreams;
        dev.AirflowMaxPerHole = _airflowLayer.MaxPerHole;
        dev.AirflowMaxPerVent = _airflowLayer.MaxPerVent;
        dev.AirflowSpawnDistance = _airflowLayer.SpawnDistance;
        dev.AirflowDebug = _airflowLayer.ShowDebug;
        dev.GasHaze = _gasLayer.Visible;
        dev.GasOverlay = _gasOverlayOn;
        dev.GasOverlayMode = DevMenuOptions.OverlayModes[Math.Clamp((int)_gasOverlay.Mode, 0, DevMenuOptions.OverlayModes.Length - 1)];
        if (!string.IsNullOrEmpty(_gasOverlay.Gas) && _gasOverlay.Gas.Length <= 48) dev.GasOverlayGas = _gasOverlay.Gas;
        dev.GasOverlayOpacityPercent = Math.Clamp((int)MathF.Round(_gasOverlay.Opacity * 100f), 10, 100);
        dev.GasOverlayLabels = _gasOverlay.Labels;
        dev.GasOverlayLabelRadius = _gasOverlay.LabelRadius;
        dev.CellCoordinates = _cellCoordsOn;
        dev.CellCoordinatesRadius = _cellCoords.Radius;
        dev.Ceiling = _ceiling;
        dev.Fullbright = _fullbright;
        dev.MinimapMobs = _minimapMobs;
        dev.PlayerMarkers = _playerMarkersOn;
        dev.NamesOverlay = _names;
        dev.SpawnMapping = _spawnMapping;
        dev.SpawnAbstract = _spawnAbstract;
        dev.SpawnAmount = _spawnAmount;
        dev.SpawnStick = _spawnStick;
        dev.SpawnFavouritesOnly = _spawnFavouritesOnly;
        for (int i = 0; i < dev.SpawnFavourites.Length; i++)
            dev.SpawnFavourites[i] = DevMenuOptions.JoinPaths(_favourites[i]);
        dev.GasMoles = _gasMoles;
        dev.GasKelvin = _gasKelvin;
        dev.GasRadius = _gasRadius;
        SyncAudioOptions(dev);
        if (dev.ToText() == _options.Dev.ToText()) return;
        _options.Dev = dev;
        _devOptionsDirty = true;
    }

    private void SaveDevOptionsIfDone(bool controlHeld)
    {
        if (!_devOptionsDirty || controlHeld) return;
        _devOptionsDirty = false;
        SaveOptions();
    }

    private void OpenDevMenuAtStart() => SetDevMenu(true);

    // The tab item's flags: the start tab selected on the first frame only.
    private ImGuiTabItemFlags TabFlags(string name)
    {
        if (_startTab == null || !string.Equals(_startTab, name, StringComparison.OrdinalIgnoreCase)) return ImGuiTabItemFlags.None;
        return ImGuiTabItemFlags.SetSelected;
    }

    private void TeardownDev()
    {
        if (_feed != null)
            _feed.DevGasReportReceived -= OnGasReport;
        if (_imgui != null)
            _imgui.Build -= DrawDevUi;
    }

    private bool DevMenuOpen => _devMenuOpen;

    // The owner (2026-11-10): the open dev menu has the mouse and the keys, or (Tab) the game has them and the menu stays drawn behind, until Tab again.
    private bool _devMenuFocused;

    /// <summary>The dev menu is open and has the mouse: its windows and tools take the clicks, the pointer is free.</summary>
    private bool DevMenuHasMouse => _devMenuOpen && _devMenuFocused;

    // Tab with the dev menu open: the mouse and the keys go to the game, or back to the menu.
    private void ToggleDevMenuFocus()
    {
        _devMenuFocused = !_devMenuFocused;
        _imgui.Capture = _devMenuFocused;
        if (!_devMenuFocused)
        {
            SyncDevOptions(); // a control still held when the menu let go
            SaveDevOptionsIfDone(false);
        }
    }

    // D322: the dev menu is an admin's: the server says who may open it; a local world (the fake server, the showcase) has no admins to ask
    private bool DevAccessAllowed => _feed is not GameClient client || client.DevAccessAllowed == true;

    private void ToggleDevMenu()
    {
        if (_devMenuOpen || DevAccessAllowed)
            SetDevMenu(!_devMenuOpen);
        else
            GD.Print("[anomaly] dev menu refused: this player is not an admin here");
    }

    private void SetDevMenu(bool open)
    {
        _devMenuOpen = open;
        _devMenuFocused = open; // a menu opened has the mouse
        _imgui.Capture = open;
        if (open)
            EnsureCatalog();
        else
        {
            SyncDevOptions(); // a control still held when the menu closed
            SaveDevOptionsIfDone(false);
        }
    }

    private void OnGasReport(DevGasReport report)
    {
        if (_gasOverlayOn)
            _gasOverlay.Take(report, App.NowSeconds);
    }

    // Twice a second while the overlay is on: the air around the camera's cell.
    private (int X, int Y, int Z) _cameraCell;

    private void UpdateDev(double now, bool hasOwn, RenderPose pose)
    {
        if (_rig.Camera == null) return;
        _cameraCell = CameraCell(hasOwn, pose);
        UpdateDevTools(hasOwn, pose);
        UpdateCellCoords(now);
        _playerMarkers.Show(_playerMarkersOn ? _feed.MinimapMobs : null, pose.X, pose.Y, GetProcessDeltaTime());
        UpdateMobInfo(now);
        UpdatePortStatus(now);
        if (!_gasOverlayOn || now < _nextProbeAt) return;
        _nextProbeAt = now + ProbeSeconds;
        (int x, int y, int z) = _cameraCell;
        if (_feed.Map != null && x >= 1 && y >= 1 && x <= _feed.Map.Width && y <= _feed.Map.Height)
            _session.DevGasProbe(x, y, z);
    }

    // The labels follow the camera's cell; once a second they are made again (a wall built or taken away).
    private void UpdateCellCoords(double now)
    {
        _cellCoords.Visible = _cellCoordsOn && _feed.Map != null;
        if (!_cellCoords.Visible) return;
        if (now >= _nextCoordsRefresh)
        {
            _nextCoordsRefresh = now + 1.0;
            _cellCoords.Refresh();
        }
        _cellCoords.MapSize = (_feed.Map.Width, _feed.Map.Height);
        _cellCoords.Show(_cameraCell);
    }

    private (int X, int Y, int Z) CameraCell(bool hasOwn, RenderPose pose)
    {
        Vector3 eye = _rig.Camera.GlobalPosition;
        int x = (int)Math.Floor(eye.X / TileSpace.TileSize) + 1;
        int y = (int)Math.Floor(-eye.Z / TileSpace.TileSize) + 1;
        // the free camera is on the level it flies in (the owner, 2026-11-10: from one level to another), the other views on the own mob's
        // (the map editor's camera looks at the level that is edited, however high it flies)
        int z = EditorOn ? _editorLevel : hasOwn && _rig.Mode != CameraMode.Free ? pose.Z : Math.Clamp((int)Math.Floor(eye.Y / TileSpace.LevelHeight) + 1, 1, Math.Max(1, _feed.Map?.Levels ?? 1));
        return (x, y, z);
    }

    // ---- the windows ----

    private void DrawDevUi()
    {
        float s = _imgui.UiScale;
        if (_devMenuOpen && _feed is GameClient { DevAccessAllowed: false })
            SetDevMenu(false); // the rights are gone
        if (_devMenuOpen)
        {
            Vector2 screen = GetViewport().GetVisibleRect().Size;
            ImGui.SetNextWindowPos(new NVector2(16 * s, 60 * s), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new NVector2(Math.Min(480 * s, screen.X - 32 * s), Math.Min(720 * s, screen.Y - 76 * s)), ImGuiCond.FirstUseEver);
            bool open = true;
            if (!_devMenuFocused)
                ImGui.SetNextWindowBgAlpha(0.55f); // the game has the mouse: the menu stands back
            string focus = _devMenuFocused ? KeyLabel(PlayAction.CursorMode) + ": back to the game" : KeyLabel(PlayAction.CursorMode) + ": back to the menu";
            if (ImGui.Begin("Dev menu  (" + KeyLabel(PlayAction.DevMenu) + ", " + focus + ")###devmenu", ref open))
                DrawMenu();
            ImGui.End();
            SyncDevOptions();
            SaveDevOptionsIfDone(ImGui.IsAnyItemActive());
            if (!open)
                SetDevMenu(false);
            else
                DrawToolTooltip();
        }
        if (_gasOverlayOn)
            DrawInspector(s);
        DrawPortInspector(s);
    }

    private void DrawMenu()
    {
        DrawToolBar();
        ImGui.Separator();
        if (!ImGui.BeginTabBar("##devtabs"))
            return;
        if (BeginTab("Spawn"))
        {
            DrawSpawnTab();
            ImGui.EndTabItem();
        }
        if (BeginTab("Gas"))
        {
            DrawGasTab();
            ImGui.EndTabItem();
        }
        if (BeginTab("Player"))
        {
            DrawPlayerTab();
            ImGui.EndTabItem();
        }
        if (BeginTab("Overlay"))
        {
            DrawOverlayTab();
            ImGui.EndTabItem();
        }
        if (BeginTab("Audio"))
        {
            DrawAudioTab();
            ImGui.EndTabItem();
        }
        if (BeginTab("View"))
        {
            DrawViewTab();
            ImGui.EndTabItem();
        }
        if (BeginTab("Tools"))
        {
            DrawToolsTab();
            ImGui.EndTabItem();
        }
        if (BeginTab("Stats"))
        {
            DrawStatsTab();
            ImGui.EndTabItem();
        }
        if (BeginTab("Keys"))
        {
            DrawKeysTab();
            ImGui.EndTabItem();
        }
        if (BeginTab("Commands"))
        {
            DrawCommandsTab();
            ImGui.EndTabItem();
        }
        ImGui.EndTabBar();
        _startTab = null;
    }

    // ImGui.NET has no BeginTabItem with flags and no p_open (which would draw a close button): the native call with a null one.
    private unsafe bool BeginTab(string name)
    {
        byte[] label = System.Text.Encoding.UTF8.GetBytes(name + "\0");
        fixed (byte* text = label)
            return ImGuiNative.igBeginTabItem(text, null, TabFlags(name)) != 0;
    }

    private void DrawOverlayTab()
    {
        if (ImGui.CollapsingHeader("Gas overlay", ImGuiTreeNodeFlags.DefaultOpen))
        {
            bool on = _gasOverlayOn;
            if (ImGui.Checkbox("Show the air of each cell", ref on))
            {
                _gasOverlayOn = on;
                _gasOverlay.Visible = on;
                _nextProbeAt = 0;
                if (!on) _gasOverlay.Clear();
            }
            ImGui.TextDisabled("Asks the server twice a second (DEV_COMMANDS)");
            if (ImGui.Combo("Tiles", ref _overlayModeIndex, OverlayModes, OverlayModes.Length))
            {
                _gasOverlay.Mode = (GasOverlayMode)_overlayModeIndex;
                _gasOverlay.Refresh();
            }
            DevGasReport report = _gasOverlay.Report;
            if (_gasOverlay.Mode == GasOverlayMode.OneGas)
            {
                string[] ids = report == null ? new[] { _gasOverlay.Gas } : GasIds(report);
                _overlayGasIndex = Math.Max(0, Array.IndexOf(ids, _gasOverlay.Gas));
                if (ImGui.Combo("Gas", ref _overlayGasIndex, ids, ids.Length))
                {
                    _gasOverlay.Gas = ids[_overlayGasIndex];
                    _gasOverlay.Refresh();
                }
            }
            float opacity = _gasOverlay.Opacity;
            if (ImGui.SliderFloat("Opacity", ref opacity, 0.1f, 1f))
            {
                _gasOverlay.Opacity = opacity;
                _gasOverlay.Refresh();
            }
            bool labels = _gasOverlay.Labels;
            if (ImGui.Checkbox("Labels", ref labels))
            {
                _gasOverlay.Labels = labels;
                _gasOverlay.Refresh();
            }
            int radius = _gasOverlay.LabelRadius;
            if (ImGui.SliderInt("Label radius", ref radius, 0, Limits.DevGasProbeRadius))
            {
                _gasOverlay.LabelRadius = radius;
                _gasOverlay.Refresh();
            }
            if (report != null)
                ImGui.TextDisabled(string.Create(CultureInfo.InvariantCulture,
                    $"{report.Width}x{report.Height} cells from ({report.X0}, {report.Y0}, {report.Z}), {App.NowSeconds - _gasOverlay.ReportAt:0.0} s ago"));
            else if (_gasOverlayOn)
                ImGui.TextDisabled("No answer yet (are the server's dev commands on?)");
            DrawLegend(report);
        }

        if (ImGui.CollapsingHeader("Gas haze (tg's gas overlays)", ImGuiTreeNodeFlags.DefaultOpen))
        {
            bool haze = _gasLayer.Visible;
            if (ImGui.Checkbox("Draw the haze", ref haze))
                _gasLayer.Visible = haze;
            ImGui.TextDisabled($"{_gasLayer.CellCount} cells show gas");
        }

        if (ImGui.CollapsingHeader("Airflow streams (D141)", ImGuiTreeNodeFlags.DefaultOpen))
        {
            AirflowLayer air = _airflowLayer;
            bool on = air.Enabled;
            if (ImGui.Checkbox("Draw the streams", ref on))
                air.Enabled = on;
            int total = air.MaxStreams;
            if (ImGui.SliderInt("Max streams", ref total, 0, 2000))
                air.MaxStreams = total;
            int perHole = air.MaxPerHole;
            if (ImGui.SliderInt("Max per hole", ref perHole, 1, 1000))
                air.MaxPerHole = perHole;
            int perVent = air.MaxPerVent;
            if (ImGui.SliderInt("Max per vent", ref perVent, 1, 500))
                air.MaxPerVent = perVent;
            int reach = air.SpawnDistance;
            if (ImGui.SliderInt("Spawn distance", ref reach, 2, 64, "%d tiles"))
                air.SpawnDistance = reach;
            bool debug = air.ShowDebug;
            if (ImGui.Checkbox("Flow arrows, hole faces, vents (green out, blue in)", ref debug))
                air.ShowDebug = debug;
            ImGui.TextDisabled(string.Create(CultureInfo.InvariantCulture,
                $"{air.FlowCellCount} cells flow, {air.VentCount} vents move air, {air.HoleCount} holes and vents ({air.ActiveHoles} near and in view), {air.StreamCount} streams"));
        }
    }

    private float _modelGlow = 0.5f, _lampGlow = 4f; // project.godot's anomaly_model_glow and anomaly_lamp_glow

    private void DrawViewTab()
    {
        ImGui.Checkbox("Ceiling (F2)", ref _ceiling);
        ImGui.Checkbox("Fullbright (every surface lit)", ref _fullbright);
        bool mobs = _minimapMobs;
        if (ImGui.Checkbox("Minimap: living mobs (players green, the rest red)", ref mobs))
            SetMinimapMobs(mobs);
        bool markers = _playerMarkersOn;
        if (ImGui.Checkbox("Markers over every player of the level (through walls)", ref markers))
            SetPlayerMarkers(markers);
        ImGui.Checkbox("Names overlay (" + KeyLabel(PlayAction.Names) + ")", ref _names);
        if (_light != null)
        {
            // D288: the Godot lights or, for comparison, the old light grid
            bool dynamic = _light.Dynamic;
            if (ImGui.Checkbox("Godot lights (off: the old light grid)", ref dynamic))
                _light.Dynamic = dynamic;
            if (dynamic)
            {
                float lamp = _light.LampStrength, sun = _light.SunStrength, sky = _light.SkyGain;
                if (ImGui.SliderFloat("Lamp energy per power", ref lamp, 0f, 8f, "%.2f"))
                    _light.LampStrength = lamp;
                if (ImGui.SliderFloat("Sun energy", ref sun, 0f, 6f, "%.2f"))
                    _light.SunStrength = sun;
                if (ImGui.SliderFloat("Stars and planet (ambient cube)", ref sky, 0f, 4f, "%.2f"))
                    _light.SkyGain = sky;
                float fill = _light.FillShare;
                if (ImGui.SliderFloat("Bounce (share of the light grid)", ref fill, 0f, 1f, "%.2f"))
                    _light.FillShare = fill;
                ImGui.SliderFloat("Flashlight idle sway (deg)", ref FlashlightSway.IdleDegrees, 0f, 3f, "%.2f");
                ImGui.SliderFloat("Flashlight turn sway (s, <0 lags)", ref FlashlightSway.TurnSeconds, -0.15f, 0.15f, "%.3f");
                ImGui.SliderFloat("Flashlight turn sway max (deg)", ref FlashlightSway.TurnMaxDegrees, 0f, 20f, "%.1f");
                ImGui.SliderFloat("Flashlight walk sway (deg)", ref FlashlightSway.WalkDegrees, 0f, 6f, "%.2f");
                ImGui.TextDisabled(string.Create(CultureInfo.InvariantCulture, $"{_light.LampCount} lamps drawn, {_light.ShadowedLightCount} with shadows"));
            }
            float strength = _light.Strength, cap = _light.Cap;
            if (ImGui.SliderFloat("Light strength", ref strength, 0f, 4f, "%.2f"))
                _light.Strength = strength;
            if (ImGui.SliderFloat("Light cap (0: off)", ref cap, 0f, 3f, "%.2f"))
                _light.Cap = cap;
            if (ImGui.SliderFloat("Model light layer glow", ref _modelGlow, 0f, 4f, "%.2f"))
                RenderingServer.GlobalShaderParameterSet("anomaly_model_glow", _modelGlow);
            if (ImGui.SliderFloat("Lamp glow", ref _lampGlow, 0f, 4f, "%.2f"))
                RenderingServer.GlobalShaderParameterSet("anomaly_lamp_glow", _lampGlow);
            float ambient = _light.Ambient.X;
            if (ImGui.SliderFloat("Light in darkness (ambient)", ref ambient, 0f, 0.3f, "%.3f", ImGuiSliderFlags.Logarithmic))
                _light.Ambient = Vector3.One * ambient;
            ImGui.TextDisabled(string.Create(CultureInfo.InvariantCulture, $"vision light floor (mesons, night vision): {_lightFloor.R:0.###}, {_lightFloor.G:0.###}, {_lightFloor.B:0.###}"));
        }
        ImGui.Checkbox("Cell coordinates on the floor", ref _cellCoordsOn);
        if (_cellCoordsOn)
        {
            int radius = _cellCoords.Radius;
            if (ImGui.SliderInt("Coordinates radius", ref radius, 0, CellCoordinates.MaxRadius))
                _cellCoords.Radius = radius;
            (int cx, int cy, int cz) = _cameraCell;
            ImGui.TextDisabled(string.Create(CultureInfo.InvariantCulture, $"x,y of each floor cell (north up); you are at ({cx}, {cy}, {cz})"));
        }
        StatsCornerCheckbox();
        bool full = DisplayServer.WindowGetMode() is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;
        if (ImGui.Checkbox("Fullscreen (" + KeyLabel(PlayAction.Fullscreen) + ")", ref full))
            ToggleFullscreen();
        bool changed = false;
        bool playerNames = _options.PlayerNames, tile = _options.HighlightTile, thing = _options.HighlightThing, fov = _options.ThirdPersonFov;
        changed |= ImGui.Checkbox("Names over players", ref playerNames);
        changed |= ImGui.Checkbox("Third person: shade what the mob cannot see", ref fov);
        changed |= ImGui.Checkbox("Outline the tile under the crosshair", ref tile);
        changed |= ImGui.Checkbox("Outline the thing under the crosshair", ref thing);
        if (changed)
        {
            _options.PlayerNames = playerNames;
            _options.HighlightTile = tile;
            _options.HighlightThing = thing;
            _options.ThirdPersonFov = fov;
            OnOptionsChanged();
        }
    }

    private void DrawCommandsTab()
    {
        ImGui.TextDisabled("Sent as from the console (`); the switches of any player are in the Player tab");
        ImGui.SeparatorText("You");
        if (ImGui.Button("Heal me")) OnConsoleSubmitted("heal me");
        ImGui.SameLine();
        if (ImGui.Button("Full heal me")) OnConsoleSubmitted("fullheal me");
        ImGui.SameLine();
        if (ImGui.Button("Revive me")) OnConsoleSubmitted("revive me");
        ImGui.SameLine();
        if (ImGui.Button("Kill me")) OnConsoleSubmitted("kill me");
        if (ImGui.Button("Analyze me")) OnConsoleSubmitted("analyze me");
        ImGui.SameLine();
        if (ImGui.Button("God (fly, noclip, godmode)")) OnConsoleSubmitted("god me");
        ImGui.SameLine();
        if (ImGui.Button("All-access ID")) OnConsoleSubmitted("give me /obj/item/card/id/advanced/debug");
        // x1 is the server's default scale (DV70: 0.5, half of tg's speed)
        if (ImGui.Button("Run speed x1")) OnConsoleSubmitted("speedscale 0.5");
        ImGui.SameLine();
        if (ImGui.Button("x2")) OnConsoleSubmitted("speedscale 1");
        ImGui.SameLine();
        if (ImGui.Button("x4")) OnConsoleSubmitted("speedscale 2");
        // DV211: the server's projectile speed (projspeed), sent when the slider is let go; the client does not read it back
        ImGui.SliderFloat("Projectile speed (x tg)", ref _projectileSpeed, 0.25f, 8f, "%.2f");
        if (ImGui.IsItemDeactivatedAfterEdit())
            OnConsoleSubmitted(string.Create(CultureInfo.InvariantCulture, $"projspeed {_projectileSpeed:0.##}"));
        // DV233: the server's footstep strides (footstride), tiles walked per step (two strides), sent when a slider is let go
        ImGui.SliderFloat("Run step (tiles)", ref _runStep, 0.25f, 4f, "%.2f");
        if (ImGui.IsItemDeactivatedAfterEdit())
            OnConsoleSubmitted(string.Create(CultureInfo.InvariantCulture, $"footstride run {_runStep / 2:0.###}"));
        ImGui.SliderFloat("Walk step (tiles)", ref _walkStep, 0.25f, 4f, "%.2f");
        if (ImGui.IsItemDeactivatedAfterEdit())
            OnConsoleSubmitted(string.Create(CultureInfo.InvariantCulture, $"footstride walk {_walkStep / 2:0.###}"));
        ImGui.SeparatorText("The world");
        if (ImGui.Button("Ignite in front")) OnConsoleSubmitted("ignite me");
        ImGui.SameLine();
        if (ImGui.Button("Breach in front")) OnConsoleSubmitted("breach me");
        if (ImGui.Button("Air everywhere")) OnConsoleSubmitted("gas air");
        ImGui.SameLine();
        if (ImGui.Button("Lean plasma everywhere")) OnConsoleSubmitted("gas o2=22;n2=82;plasma=5;TEMP=293.15");
        if (ImGui.Button("Steam everywhere")) OnConsoleSubmitted("gas o2=22;n2=82;water_vapor=10;TEMP=293.15");

        ImGui.Separator();
        float scale = _imgui.UiScale;
        if (ImGui.SliderFloat("UI scale", ref scale, 0.75f, 4f, "%.2f"))
            _imgui.UiScale = scale;
    }

    // ---- D137: the debug HUD's numbers and keys ----

    private void DrawStatsTab()
    {
        if (ImGui.CollapsingHeader("Connection", ImGuiTreeNodeFlags.DefaultOpen))
            Lines(ConnectionLines());
        if (ImGui.CollapsingHeader("Prediction", ImGuiTreeNodeFlags.DefaultOpen))
            Lines(PredictionLines());
        if (ImGui.CollapsingHeader("You", ImGuiTreeNodeFlags.DefaultOpen))
            Lines(PlayerLines());
        if (ImGui.CollapsingHeader("Server", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (_lastServerText.Length > 0)
                ImGui.TextWrapped(_lastServerText);
            else
                ImGui.TextDisabled("Nothing from the server yet");
        }
        ImGui.Separator();
        StatsCornerCheckbox();
    }

    private void StatsCornerCheckbox()
    {
        bool show = _options.ShowStats;
        if (ImGui.Checkbox("FPS and server stats at the top right (" + KeyLabel(PlayAction.DebugHud) + ")", ref show))
        {
            _options.ShowStats = show;
            SaveOptions();
        }
    }

    private static void Lines(IEnumerable<string> lines)
    {
        foreach (string line in lines)
            ImGui.TextUnformatted(line);
    }

    private void DrawKeysTab()
    {
        if (Input.MouseMode != Input.MouseModeEnum.Captured && !App.Instance.Args.Automated)
            ImGui.TextDisabled("Click the world to capture the mouse; Esc releases it");
        ImGui.TextDisabled("Rebind in the options (" + KeyLabel(PlayAction.Options) + ")");
        if (!ImGui.BeginTable("##keys", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
            return;
        foreach (PlayAction action in Enum.GetValues<PlayAction>())
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.TextUnformatted(KeyLabel(action));
            ImGui.TableSetColumnIndex(1);
            ImGui.TextUnformatted(KeyBindings.TitleOf(action));
        }
        ImGui.EndTable();
    }

    // The keys of an action as a player reads them (Backslash is the | key).
    private string KeyLabel(PlayAction action)
    {
        IReadOnlyList<string> keys = _options.Bindings.KeysOf(action);
        if (keys.Count == 0) return "unbound";
        return string.Join(", ", keys.Select(k => k switch
        {
            "Backslash" => "|",
            "Quoteleft" => "`",
            "MouseLeft" => "LMB",
            "MouseRight" => "RMB",
            "MouseMiddle" => "MMB",
            _ => k,
        }));
    }

    private void DrawLegend(DevGasReport report)
    {
        switch (_gasOverlay.Mode)
        {
            case GasOverlayMode.Pressure:
                foreach (float kpa in new[] { 0f, 40f, 101.3f, 160f, 300f })
                    Swatch(GasOverlay.PressureColor(kpa), kpa.ToString("0.#", CultureInfo.InvariantCulture) + " kPa");
                break;
            case GasOverlayMode.Temperature:
                foreach (float k in new[] { 100f, 200f, 293.15f, 373.15f, 1000f, 5000f })
                    Swatch(GasOverlay.TemperatureColor(k), string.Create(CultureInfo.InvariantCulture, $"{k:0} K ({k - 273.15f:0} C)"));
                break;
            default:
                if (report == null) break;
                foreach (DevGasInfo gas in report.Gases)
                    Swatch(GasOverlay.Rgb(gas.Rgb), gas.Id + "  " + gas.Name);
                break;
        }
    }

    private void Swatch(Color color, string label)
    {
        float h = ImGui.GetTextLineHeight();
        ImGui.ColorButton("##" + label, new NVector4(color.R, color.G, color.B, 1f), ImGuiColorEditFlags.NoTooltip, new NVector2(h, h));
        ImGui.SameLine();
        ImGui.TextUnformatted(label);
    }

    // The cell under the crosshair: the server's air and the gas the replica shows there.
    private void DrawInspector(float s)
    {
        Vector2 size = GetViewport().GetVisibleRect().Size;
        ImGui.SetNextWindowPos(new NVector2(size.X - 16 * s, 80 * s), ImGuiCond.FirstUseEver, new NVector2(1, 0));
        ImGui.SetNextWindowSize(new NVector2(300 * s, 0), ImGuiCond.FirstUseEver);
        ImGuiWindowFlags flags = ImGuiWindowFlags.AlwaysAutoResize | (_devMenuOpen ? 0 : ImGuiWindowFlags.NoInputs);
        ImGui.SetNextWindowBgAlpha(0.8f);
        if (ImGui.Begin("Cell", flags))
        {
            {
                // the cell under the crosshair, else the camera's own
                (int x, int y, int z) = _hit.IsHit ? (_hit.X, _hit.Y, _hit.Z) : _cameraCell;
                ImGui.Text($"({x}, {y}, {z})  {_feed.Replica.TurfTypeAt(x, y, z) ?? "-"}");
                DevGasReport report = _gasOverlay.Report;
                DevGasCell cell = report != null && report.Z == z ? report.At(x, y) : default;
                switch (cell.Kind)
                {
                    case DevCellKind.Open:
                    case DevCellKind.Space:
                        DrawAir(report, cell);
                        break;
                    case DevCellKind.Closed:
                        ImGui.TextDisabled("A closed turf: no air");
                        break;
                    default:
                        ImGui.TextDisabled("Outside the probed area");
                        break;
                }
                GasLevel[] shown = _feed.Replica.GasAt(x, y, z);
                if (shown.Length > 0)
                {
                    ImGui.Separator();
                    ImGui.TextDisabled("tg's gas overlays here:");
                    foreach (GasLevel g in shown)
                    {
                        string kind = g.Kind <= _feed.Replica.GasKinds.Count ? _feed.Replica.GasKinds[g.Kind - 1] : "?";
                        ImGui.Text(string.Create(CultureInfo.InvariantCulture, $"  {kind} level {g.Level} (alpha {GasVisual.Alpha(g.Level):0})"));
                    }
                }
            }
        }
        ImGui.End();
    }

    private static void DrawAir(DevGasReport report, DevGasCell cell)
    {
        ImGui.Text(string.Create(CultureInfo.InvariantCulture, $"{cell.Pressure:0.00} kPa   {cell.Temperature:0.0} K ({cell.Temperature - 273.15f:0.0} C)"));
        float total = 0;
        foreach (DevGasAmount amount in cell.Moles)
            total += amount.Moles;
        ImGui.Text(string.Create(CultureInfo.InvariantCulture, $"{total:0.###} mol{(cell.Kind == DevCellKind.Space ? "  (space)" : "")}"));
        if (cell.Moles.Length == 0) return;
        if (ImGui.BeginTable("gases", 3, ImGuiTableFlags.SizingFixedFit))
        {
            var sorted = new List<DevGasAmount>(cell.Moles);
            sorted.Sort((a, b) => b.Moles.CompareTo(a.Moles));
            foreach (DevGasAmount amount in sorted)
            {
                DevGasInfo gas = report.Gases[amount.Gas];
                Color c = GasOverlay.Rgb(gas.Rgb);
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.TextColored(new NVector4(c.R, c.G, c.B, 1f), gas.Id);
                ImGui.TableSetColumnIndex(1);
                ImGui.Text(amount.Moles.ToString("0.###", CultureInfo.InvariantCulture) + " mol");
                ImGui.TableSetColumnIndex(2);
                ImGui.Text((total > 0 ? amount.Moles / total * 100 : 0).ToString("0.#", CultureInfo.InvariantCulture) + " %");
            }
            ImGui.EndTable();
        }
    }

    private static string[] GasIds(DevGasReport report)
    {
        var ids = new string[report.Gases.Count];
        for (int i = 0; i < ids.Length; i++)
            ids[i] = report.Gases[i].Id;
        return ids.Length == 0 ? new[] { "plasma" } : ids;
    }
}
