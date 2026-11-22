using System.IO;
using System.Linq;
using Godot;
using ImGuiNET;
using Anomaly.Client.Dev;
using Anomaly.Client.Render;
using Anomaly.Client.Shared.Dev;
using Anomaly.Protocol;
using NVector2 = System.Numerics.Vector2;
using NVector4 = System.Numerics.Vector4;

namespace Anomaly.Client;

// The port status overlay (the owner, 2026-10-28; the Settings window's Developer tab): the balls over the things (PortStatusOverlay)
// and, for the thing or the turf under the crosshair, a window listing its tg procs, what is ported and what is not. It reads the
// translation tracker from the checkout.
public partial class GameScene
{
    // shortcut: read from the checkout's docs/, so an exported build has no overlay; copy the file in godot/tools/export-*.sh if testers need it
    private const string PortIndexPath = "../docs/ai/specs/generated/types-index.tsv";
    private const double PortRefreshSeconds = 0.1;
    private const int PortListedProcs = 12;

    private PortStatusOverlay _portOverlay;
    private PortIndex _portIndex;
    private string _portIndexError;
    private double _nextPortRefresh;

    private void UpdatePortStatus(double now)
    {
        bool on = _options.PortStatus && _feed.Map != null && LoadPortIndex();
        if (_portOverlay == null)
        {
            if (!on) return;
            _portOverlay = new PortStatusOverlay { Name = "PortStatus" };
            AddChild(_portOverlay);
        }
        _portOverlay.Visible = on;
        if (!on || now < _nextPortRefresh) return;
        _nextPortRefresh = now + PortRefreshSeconds;
        _portOverlay.Show(_portIndex, _feed.Entities, _cameraCell, _feed.OwnEntity);
    }

    private bool LoadPortIndex()
    {
        if (_portIndex != null) return true;
        if (_portIndexError != null) return false;
        string path = DataPaths.Find(PortIndexPath);
        try
        {
            using var reader = new StreamReader(path);
            _portIndex = PortIndex.Parse(reader);
            return true;
        }
        catch (IOException ex)
        {
            _portIndexError = "no translation tracker at " + path + ": " + ex.Message;
            GD.PushWarning("[anomaly] port status: " + _portIndexError);
            return false;
        }
    }

    // The thing under the crosshair, else the turf: its type's procs, the ones not ported first.
    private void DrawPortInspector(float s)
    {
        if (!_options.PortStatus) return;
        ImGui.SetNextWindowPos(new NVector2(90 * s, 10 * s), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowBgAlpha(0.8f);
        ImGuiWindowFlags flags = ImGuiWindowFlags.AlwaysAutoResize | (_devMenuOpen ? 0 : ImGuiWindowFlags.NoInputs);
        if (ImGui.Begin("Port status", flags))
            DrawPortReport();
        ImGui.End();
    }

    private void DrawPortReport()
    {
        if (_portIndex == null)
        {
            ImGui.TextWrapped(_portIndexError ?? "Loading the translation tracker...");
            return;
        }
        string name, path;
        if (_targetUnderCrosshair != 0 && _feed.Replica.TryGet(_targetUnderCrosshair, out ReplicaEntity e))
            (name, path) = (e.State.Name, e.TypePath);
        else if (_hit.IsHit && _hit.Kind is TargetKind.Turf)
            (name, path) = ("turf", _feed.Replica.TurfTypeAt(_hit.X, _hit.Y, _hit.Z));
        else
        {
            ImGui.TextDisabled("Aim at a thing or a turf");
            return;
        }
        PortReport report = _portIndex.Of(path);
        ImGui.TextUnformatted(name + "  " + report.Path);
        ImGui.TextColored(LevelColour(report.Level), report.Level switch
        {
            PortLevel.Green => "Ported: every proc of its own is translated",
            PortLevel.Yellow => "Partly ported",
            PortLevel.Red => report.State == "inert" ? "Inert: its procs do not run" : "Its main use is not ported",
            _ => "Not in the translation tracker",
        });
        ImGui.TextDisabled("state " + report.State + "; base types' procs not ported: " + report.BaseGaps);
        if (report.Own.Count == 0) return;
        ImGui.Separator();
        int listed = 0;
        foreach (PortProc p in report.Own.OrderBy(p => p.Ported).ThenBy(p => !PortIndex.KeyProcs.Contains(p.Name)))
        {
            if (listed++ == PortListedProcs)
            {
                ImGui.TextDisabled($"... {report.Own.Count - PortListedProcs} more");
                break;
            }
            NVector4 colour = p.Ported ? LevelColour(PortLevel.Green) : PortIndex.KeyProcs.Contains(p.Name) ? LevelColour(PortLevel.Red) : LevelColour(PortLevel.Yellow);
            ImGui.TextColored(colour, (p.Ported ? "+ " : "- ") + p.Name);
            ImGui.SameLine();
            ImGui.TextDisabled(p.Status + "  " + Path.GetFileName(p.Where));
        }
    }

    private static NVector4 LevelColour(PortLevel level) => level switch
    {
        PortLevel.Green => new NVector4(0.3f, 0.95f, 0.4f, 1f),
        PortLevel.Yellow => new NVector4(1f, 0.85f, 0.2f, 1f),
        PortLevel.Red => new NVector4(1f, 0.35f, 0.3f, 1f),
        _ => new NVector4(0.7f, 0.7f, 0.7f, 1f),
    };
}
