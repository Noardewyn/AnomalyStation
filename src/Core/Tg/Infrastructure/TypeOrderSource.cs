using Anomaly.Core.DmShim;

// The DM object-tree order of the type registry (docs/ai/specs/object-model.md section 9): the generated
// src/Core/Tg/Generated/TypeOrder.g.cs and ProcOrder.g.cs (tools/coloring), named here explicitly so that the shim,
// which cannot reference this assembly, does not depend on class names. Without this attribute the registry has no
// order source (types sort in a path-ordered preorder).
[assembly: DmTypeOrder(typeof(Anomaly.Core.Tg.TypeOrder), typeof(Anomaly.Core.Tg.ProcOrder))]
