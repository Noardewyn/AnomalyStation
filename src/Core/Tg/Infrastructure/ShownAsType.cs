namespace Anomaly.Core.Tg;

// DEVIATION(DV376): a host seam, not tg code. BYOND draws an atom by its appearance alone, so tg's chameleon projector disguises its
// holder by giving a dummy the scanned thing's appearance; the 3D client also picks a thing's model and its scale (an item's, a
// structure's) by its type, so a dummy drawn as its own type (/obj/effect/dummy/chameleon) would be the scanned picture at a tile's size
// on a billboard. The replicator sends the type the atom names instead (the clients draw it as that thing; everything else about it,
// its name, look and density, is its own).

/// <summary>An atom the clients draw as another type: the chameleon projector's dummy, drawn as the thing it copied.</summary>
public interface IShownAsType
{
    /// <summary>The type the clients draw it as; null for its own.</summary>
    Type ShownType { get; }
}
