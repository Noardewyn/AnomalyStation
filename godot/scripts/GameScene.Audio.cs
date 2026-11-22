using System.Globalization;
using ImGuiNET;
using Anomaly.Client.Audio;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Audio;
using Anomaly.Protocol;

namespace Anomaly.Client;

// M4b audio slice 3 (apps.md 2.20): the sound player under the game scene, and the dev menu's Audio tab (its settings are
// dev.audio_* lines of the options file, DevMenuOptions).
public partial class GameScene
{
    private SoundPlayer _sound;

    private void BuildAudio()
    {
        _sound = new SoundPlayer { Name = "Sound" };
        AddChild(_sound);
        _sound.Bind(_feed.Replica, () => _rig.Camera);
        ApplyPlayAudio();
        _sound.Listen(); // after the volumes: the lobby track kept from the join tick fades in to the Settings' music volume
    }

    // The Settings' Audio tab (D296, DV226).
    private void ApplyPlayAudio()
    {
        if (_sound == null || _options == null) return;
        _sound.SetVolumes(_options.MasterVolumePercent / 100f, _options.EffectsVolumePercent / 100f, _options.AmbienceVolumePercent / 100f,
            _options.ShipHumVolumePercent / 100f, _options.MusicVolumePercent / 100f, _options.InterfaceVolumePercent / 100f);
        _sound.Reverb = _options.Reverb;
    }

    private void ApplyAudioOptions(DevMenuOptions dev)
    {
        if (_sound == null) return;
        _sound.MasterVolume = dev.AudioVolumePercent / 100f;
        _sound.Muted = dev.AudioMute;
        _sound.OcclusionFactor = dev.AudioOcclusionFactor;
        _sound.CutoffHz = dev.AudioLowPassHz;
        _sound.MuffleThreshold = dev.AudioMuffleThreshold;
        _sound.ReverbWetScale = dev.AudioReverbWet;
    }

    private void SyncAudioOptions(DevMenuOptions dev)
    {
        if (_sound == null) return;
        dev.AudioVolumePercent = Math.Clamp((int)MathF.Round(_sound.MasterVolume * 100f), 0, 100);
        dev.AudioMute = _sound.Muted;
        dev.AudioOcclusionFactor = _sound.OcclusionFactor;
        dev.AudioLowPassHz = _sound.CutoffHz;
        dev.AudioMuffleThreshold = _sound.MuffleThreshold;
        dev.AudioReverbWet = _sound.ReverbWetScale;
    }

    private void DrawAudioTab()
    {
        if (_sound?.Mixer == null)
        {
            ImGui.TextDisabled("No sound player.");
            return;
        }
        CultureInfo c = CultureInfo.InvariantCulture;
        if (ImGui.CollapsingHeader("Mix", ImGuiTreeNodeFlags.DefaultOpen))
        {
            int volume = (int)MathF.Round(_sound.MasterVolume * 100f);
            if (ImGui.SliderInt("Master volume", ref volume, 0, 100, "%d %%"))
                _sound.MasterVolume = volume / 100f;
            bool mute = _sound.Muted;
            if (ImGui.Checkbox("Mute", ref mute))
                _sound.Muted = mute;
            float k = _sound.OcclusionFactor;
            if (ImGui.SliderFloat("Occlusion factor k", ref k, 0f, 1f, "%.2f"))
                _sound.OcclusionFactor = k;
            ImGui.TextDisabled("volume = tg's x (1 - k x occlusion), DV39");
            float cutoff = _sound.CutoffHz;
            if (ImGui.SliderFloat("Low-pass cutoff", ref cutoff, 100f, 20000f, "%.0f Hz", ImGuiSliderFlags.Logarithmic))
                _sound.CutoffHz = cutoff;
            float threshold = _sound.MuffleThreshold;
            if (ImGui.SliderFloat("Muffle threshold", ref threshold, 0f, 1f, "%.2f"))
                _sound.MuffleThreshold = threshold;
            ImGui.TextDisabled("max(muffle, occlusion) at or above it: the low-pass bus (DV37, DV39)");
            bool reverb = _sound.Reverb;
            if (ImGui.Checkbox("Area reverb", ref reverb))
                _sound.Reverb = reverb;
            float wet = _sound.ReverbWetScale;
            if (ImGui.SliderFloat("Reverb wet scale", ref wet, 0f, 2f, "%.2f"))
                _sound.ReverbWetScale = wet;
            ImGui.TextDisabled("tg's sound_environment presets as Godot reverbs, wet x this (D296)");
        }
        SoundMixer m = _sound.Mixer;
        SoundLibrary lib = _sound.Library;
        ImGui.TextDisabled(string.Create(c,
            $"events {m.Events}, voices started {_sound.VoicesStarted}, active {m.ActiveVoices}/{m.PoolSize(VoicePool.Positional) + m.PoolSize(VoicePool.Centred)}, stolen {m.Stolen}, missing {m.MissingPlays}"));
        ImGui.TextDisabled(string.Create(c,
            $"files loaded {lib.LoadedFiles}, cached {lib.CachedFiles} ({lib.CachedBytes / 1048576.0:0.0} MB), missing {lib.MissingFiles}, index {lib.IndexCount}"));
        if (ImGui.CollapsingHeader("Last 16 events", ImGuiTreeNodeFlags.DefaultOpen)
            && ImGui.BeginTable("##soundlog", 8, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Borders))
        {
            ImGui.TableSetupColumn("kind");
            ImGui.TableSetupColumn("ch");
            ImGui.TableSetupColumn("file");
            ImGui.TableSetupColumn("vol");
            ImGui.TableSetupColumn("muf/occ");
            ImGui.TableSetupColumn("dist");
            ImGui.TableSetupColumn("env");
            ImGui.TableSetupColumn("voice");
            ImGui.TableHeadersRow();
            foreach (SoundLogEntry e in m.Log)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(e.Kind switch { SoundKind.Play => "play", SoundKind.Update => "update", _ => "stop" });
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(e.Channel.ToString(c));
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(ShortSoundName(e.File));
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(e.Kind == SoundKind.Stop ? "-" : e.Volume.ToString("0.#", c));
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(e.Kind == SoundKind.Stop ? "-" : string.Create(c, $"{e.Muffle:0.00}/{e.Occlusion:0.00}"));
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(!e.Positional ? "own" : e.Distance < 0 ? "?" : e.Distance.ToString("0.0", c));
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(e.Kind == SoundKind.Stop ? "-" : ReverbPresets.Name(e.Environment));
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(e.Outcome ?? string.Empty);
            }
            ImGui.EndTable();
        }
    }

    // sound/machines/airlock/airlock.ogg -> machines/airlock/airlock
    private static string ShortSoundName(string file)
    {
        if (string.IsNullOrEmpty(file)) return "-";
        string s = file.StartsWith("sound/", StringComparison.Ordinal) ? file.Substring(6) : file;
        return s.EndsWith(".ogg", StringComparison.Ordinal) ? s.Substring(0, s.Length - 4) : s;
    }
}
