using System.Text;

namespace Anomaly.Tools.Types;

internal sealed partial class Generator
{
    /// <summary>The icon files that are not one tile (<see cref="IconSizes"/>), for the port's <c>IconSizes.Of</c>.</summary>
    private void EmitIconSizes(Outputs outputs)
    {
        var sb = new StringBuilder();
        sb.Append(Header("the sizes of tg's icon files that are not 32 x 32 pixels (their DMI headers)"));
        sb.Append("namespace Anomaly.Core.Tg;\n\n");
        sb.Append("public static partial class IconSizes\n{\n");
        sb.Append("    private static readonly System.Collections.Frozen.FrozenDictionary<string, (int Width, int Height)> Sizes = System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(new System.Collections.Generic.Dictionary<string, (int Width, int Height)>(System.StringComparer.Ordinal)\n    {\n");
        foreach (var (path, (w, h)) in _model.IconSizes ?? new())
            sb.Append($"        [\"{path}\"] = ({w}, {h}),\n");
        sb.Append("    }, System.StringComparer.Ordinal);\n}\n");
        outputs.Files[TypesDir + "/IconSizes.g.cs"] = sb.ToString();
    }
}
