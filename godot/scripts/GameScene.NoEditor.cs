#if !ANOMALY_EDITOR
using System;
using Godot;
using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Client;

// The game's own build has no map editor (AnomalyEditor, docs/ai/specs/map-editor.md 5.13): GameScene.Editor*.cs are left out, and the
// members of theirs that the game's code reads stand here, inert: EditorOn is false, so none of the rest is reached.
public partial class GameScene
{
    private enum EditorCamera { Fly }

    private const float EditorSmallPartRange = 0f;
    private readonly IDisposable _playStarting = null, _playJoining = null;
    private Func<float, float, int, bool> _editorInSight;

    private static bool EditorOn => false;
    private static int _editorLevel => 0;
    private static bool _editorTop => false;
    private static bool _editorLooking => false;
    private static EditorCamera _editorCamera => EditorCamera.Fly;
    private static bool EditorUnder => false;
    private static int EditorHideAbove => 0;
    private static (float R, float G, float B) EditorLightFloor => default;

    private static bool EditorHeld(string action) => false;
    private static Vector3 EditorFlyKeys() => Vector3.Zero;
    private static bool EditorKeysFree() => false;
    private static bool EditorInput(InputEvent @event) => false;
    private static bool EditorCommand(string command) => false;
    private static bool EditorLevelShown(int z) => true;
    private static float EditorSunAzimuth(float world) => world;
    private static MapLook EditorMapLook() => null;
    private static void EditorTypeChosen() { }
    private static void UpdateEditor() { }
    private static void StartEditor(App app, ClientArgs args) { }
    private static void StartPlayed(App app) { }
    private static void StopPlay(string notice) { }
}
#endif
