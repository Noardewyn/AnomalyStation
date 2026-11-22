using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Anomaly.Tools.Types;

/// <summary>
/// The hand-written parts of the Tg classes (types-converter.md 3.1): every C# file under <c>src/Core/Tg</c> except the
/// generator's own output (<c>Generated/Types/**</c>), read as syntax. It tells which DM types have a hand-written class,
/// which of those have a hand-written var block (an instance field or property, a parameterless constructor, a
/// <c>dm_init_vars</c> override or a <c>dm_tg_vars</c> body), and the member names each part declares.
/// </summary>
internal sealed class HandWritten
{
    /// <summary>One hand-written class, all its parts together.</summary>
    internal sealed class ClassParts
    {
        public string Name;
        public string Base;
        /// <summary>The DM path of its <c>[DmPath]</c>, or null when no hand-written part carries one.</summary>
        public string DmPath;
        public string DmPathFile;
        public readonly HashSet<string> Fields = new(StringComparer.Ordinal);
        public readonly HashSet<string> Properties = new(StringComparer.Ordinal);
        public readonly HashSet<string> Methods = new(StringComparer.Ordinal);
        public readonly HashSet<string> StaticMembers = new(StringComparer.Ordinal);
        public readonly HashSet<string> NestedTypes = new(StringComparer.Ordinal);
        public readonly List<string> Files = new();
        public bool HasConstructor;
        public bool HasInitVars;
        public bool HasTgVars;

        /// <summary>Every instance member name (fields, properties, methods, nested types): what a generated field must not clash with.</summary>
        public IEnumerable<string> AllNames => Fields.Concat(Properties).Concat(Methods).Concat(StaticMembers).Concat(NestedTypes);
    }

    public readonly Dictionary<string, ClassParts> Classes = new(StringComparer.Ordinal);

    /// <summary>The var names that translated code reads with <c>Dm.initial(x, (T p) =&gt; p.var)</c> (types-converter.md 9).</summary>
    public readonly SortedSet<string> InitialReads = new(StringComparer.Ordinal);

    private static readonly System.Text.RegularExpressions.Regex InitialRead = new(
        @"Dm\.initial\((?:[^;]*?)\(\s*[A-Za-z_][A-Za-z0-9_]*\s+([A-Za-z_][A-Za-z0-9_]*)\s*\)\s*=>\s*\1\.@?([A-Za-z_][A-Za-z0-9_]*)",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    public ClassParts ByPath(string dmPath) => _byPath.GetValueOrDefault(dmPath);

    private readonly Dictionary<string, ClassParts> _byPath = new(StringComparer.Ordinal);

    public static HandWritten Scan(string tgSourceRoot)
    {
        var hw = new HandWritten();
        string generated = Path.Combine(tgSourceRoot, "Generated", "Types") + Path.DirectorySeparatorChar;
        // The converter's own live-partial stubs are not hand-written parts either (M3 contracts, phase B: with them read
        // as hand-written, a stubbed generated-only class turned HandProcs and lost its base and [DmGeneratedOnly] on the
        // next run, so a second run differed from the first).
        string stubs = Path.Combine(tgSourceRoot, "Generated", "LivePartial") + Path.DirectorySeparatorChar;
        var parse = new CSharpParseOptions(LanguageVersion.Latest);
        foreach (string file in Directory.EnumerateFiles(tgSourceRoot, "*.cs", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
        {
            if (file.StartsWith(generated, StringComparison.Ordinal) || file.StartsWith(stubs, StringComparison.Ordinal)) continue;
            string rel = Path.GetRelativePath(tgSourceRoot, file).Replace('\\', '/');
            if (rel.StartsWith("bin/", StringComparison.Ordinal) || rel.StartsWith("obj/", StringComparison.Ordinal)) continue;
            string text = File.ReadAllText(file);
            foreach (System.Text.RegularExpressions.Match m in InitialRead.Matches(text)) hw.InitialReads.Add(m.Groups[2].Value);
            SyntaxNode root = CSharpSyntaxTree.ParseText(text, parse, file).GetRoot();
            foreach (ClassDeclarationSyntax cls in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (cls.Parent is ClassDeclarationSyntax) continue; // nested helper classes are not DM types
                hw.Add(cls, rel);
            }
        }
        foreach (ClassParts c in hw.Classes.Values)
            if (c.DmPath != null) hw._byPath[c.DmPath] = c;
        return hw;
    }

    private void Add(ClassDeclarationSyntax cls, string file)
    {
        string name = cls.Identifier.ValueText;
        if (!Classes.TryGetValue(name, out ClassParts parts)) Classes[name] = parts = new ClassParts { Name = name };
        parts.Files.Add(file);
        if (cls.BaseList != null && cls.BaseList.Types.Count > 0)
        {
            string b = cls.BaseList.Types[0].Type.ToString();
            if (!b.StartsWith('I') || b.Length < 2 || !char.IsUpper(b[1])) parts.Base ??= b;
        }
        foreach (AttributeSyntax a in cls.AttributeLists.SelectMany(l => l.Attributes))
        {
            string an = a.Name.ToString();
            if ((an == "DmPath" || an.EndsWith(".DmPath", StringComparison.Ordinal) || an == "DmPathAttribute")
                && a.ArgumentList?.Arguments.Count == 1 && a.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax lit)
            {
                parts.DmPath = lit.Token.ValueText;
                parts.DmPathFile = file;
            }
        }
        foreach (MemberDeclarationSyntax m in cls.Members)
        {
            bool isStatic = m.Modifiers.Any(SyntaxKind.StaticKeyword) || m.Modifiers.Any(SyntaxKind.ConstKeyword);
            switch (m)
            {
                case FieldDeclarationSyntax f:
                    foreach (VariableDeclaratorSyntax v in f.Declaration.Variables)
                        (isStatic ? parts.StaticMembers : parts.Fields).Add(v.Identifier.ValueText);
                    break;
                case PropertyDeclarationSyntax p:
                    (isStatic ? parts.StaticMembers : parts.Properties).Add(p.Identifier.ValueText);
                    break;
                case EventFieldDeclarationSyntax ev:
                    foreach (VariableDeclaratorSyntax v in ev.Declaration.Variables) parts.StaticMembers.Add(v.Identifier.ValueText);
                    break;
                case ConstructorDeclarationSyntax c:
                    if (!isStatic && c.ParameterList.Parameters.Count == 0) parts.HasConstructor = true;
                    break;
                case MethodDeclarationSyntax md:
                {
                    string mn = md.Identifier.ValueText;
                    (isStatic ? parts.StaticMembers : parts.Methods).Add(mn);
                    if (mn == "dm_init_vars" && md.Modifiers.Any(SyntaxKind.OverrideKeyword)) parts.HasInitVars = true;
                    if (mn == "dm_tg_vars" && (md.Body != null || md.ExpressionBody != null)) parts.HasTgVars = true;
                    break;
                }
                case BaseTypeDeclarationSyntax nested:
                    parts.NestedTypes.Add(nested.Identifier.ValueText);
                    break;
                case DelegateDeclarationSyntax d:
                    parts.NestedTypes.Add(d.Identifier.ValueText);
                    break;
            }
        }
    }
}
