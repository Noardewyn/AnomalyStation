using Godot;

namespace Anomaly.Client.Editor;

/// <summary>
/// The map editor's own settings (docs/ai/specs/map-editor.md 5.4): what a mapper arranged, kept between runs in a file of the user's data
/// folder (<c>user://map_editor.cfg</c>), or in the file <c>--editor-settings=PATH</c> names (a scripted check's own, so that it leaves the
/// mapper's alone). A file that is not there, or a value that is not of its kind, gives the default.
/// </summary>
public sealed class MapEditorSettings
{
    private const string DefaultPath = "user://map_editor.cfg";

    private readonly ConfigFile _file = new();
    private readonly string _path;

    public MapEditorSettings(string path)
    {
        _path = string.IsNullOrWhiteSpace(path) ? DefaultPath : path;
        _file.Load(_path); // (a file that cannot be read leaves the settings empty: the defaults)
    }

    public bool Has(string section, string key) => _file.HasSectionKey(section, key);

    public int Int(string section, string key, int fallback)
        => _file.GetValue(section, key, fallback) is { VariantType: Variant.Type.Int } value ? value.AsInt32() : fallback;

    public bool Bool(string section, string key, bool fallback)
        => _file.GetValue(section, key, fallback) is { VariantType: Variant.Type.Bool } value ? value.AsBool() : fallback;

    public string Text(string section, string key)
        => _file.GetValue(section, key, string.Empty) is { VariantType: Variant.Type.String } value ? value.AsString() : string.Empty;

    public string[] Texts(string section, string key)
        => _file.GetValue(section, key, Array.Empty<string>()) is { VariantType: Variant.Type.PackedStringArray } value ? value.AsStringArray() : Array.Empty<string>();

    public void Set(string section, string key, Variant value) => _file.SetValue(section, key, value);

    /// <summary>Every text of a section by its key (the keys a mapper changed, each function's by its id); empty when the file has no such section.</summary>
    public Dictionary<string, string> Section(string section)
    {
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!_file.HasSection(section))
            return texts;
        foreach (string key in _file.GetSectionKeys(section))
            texts[key] = Text(section, key);
        return texts;
    }

    /// <summary>A section holds these texts and nothing else.</summary>
    public void SetSection(string section, IReadOnlyDictionary<string, string> texts)
    {
        if (_file.HasSection(section))
            _file.EraseSection(section);
        foreach ((string key, string text) in texts)
            _file.SetValue(section, key, text);
    }

    /// <summary>Writes the file; a file that cannot be written is said in the log and the settings stay for this run.</summary>
    public void Save()
    {
        Error error = _file.Save(_path);
        if (error != Error.Ok)
            GD.PrintErr($"[anomaly] editor: the settings were not saved to {_path}: {error}");
    }
}
