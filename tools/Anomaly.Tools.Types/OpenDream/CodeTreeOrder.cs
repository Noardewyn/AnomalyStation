#if ANOMALY_OPENDREAM
using System.Reflection;
using DMCompiler.Compiler.DM.AST;

// A part of OpenDream's own DMCodeTree class, compiled into this tool with OpenDream's sources (they are not edited):
// it reads the code tree's nodes, which hold every var definition and override in parse order (tgstation.dme include
// order, then line order) with its source location. The object tree (DMObject) keeps vars in dictionaries filled in
// OpenDream's multi-pass order, so the declaration order of rules 10.2 comes from here (types-converter.md 2.3).
namespace DMCompiler.DM;

internal partial class DMCodeTree
{
    /// <summary>One var definition (<c>'v'</c>) or override (<c>'o'</c>) node, in parse order.</summary>
    internal readonly record struct AnomalyVarNode(DreamPath Owner, char Kind, string Name, Location Location, bool IsStatic);

    /// <summary>The var nodes of tg's code (not DMStandard's), in parse order per owning type.</summary>
    internal List<AnomalyVarNode> AnomalyVarNodes()
    {
        var nodes = new List<AnomalyVarNode>();
        AnomalyWalk(_root, DreamPath.Root, nodes);
        return nodes;
    }

    private static readonly FieldInfo AnomalyTypeField = typeof(ObjectNode).GetField("<type>P", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("OpenDream's ObjectNode changed: no captured 'type'");
    private static readonly FieldInfo AnomalyVarDefField = typeof(ObjectVarNode).GetField("<varDef>P", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("OpenDream's ObjectVarNode changed: no captured 'varDef'");
    private static readonly FieldInfo AnomalyOverrideField = typeof(ObjectVarOverrideNode).GetField("<varOverride>P", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("OpenDream's ObjectVarOverrideNode changed: no captured 'varOverride'");

    private static void AnomalyWalk(TypeNode node, DreamPath path, List<AnomalyVarNode> nodes)
    {
        foreach (INode child in node.Children)
        {
            switch (child)
            {
                case ObjectNode objectNode:
                    AnomalyWalk(objectNode, (DreamPath)AnomalyTypeField.GetValue(objectNode), nodes);
                    break;
                case ObjectVarNode varNode:
                {
                    var def = (DMASTObjectVarDefinition)AnomalyVarDefField.GetValue(varNode);
                    nodes.Add(new AnomalyVarNode(path, 'v', def.Name, def.Location, def.IsStatic));
                    break;
                }
                case ObjectVarOverrideNode overrideNode:
                {
                    var ov = (DMASTObjectVarOverride)AnomalyOverrideField.GetValue(overrideNode);
                    nodes.Add(new AnomalyVarNode(path, 'o', ov.VarName, ov.Location, false));
                    break;
                }
            }
        }
    }
}
#endif
