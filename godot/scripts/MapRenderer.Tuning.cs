using System.IO;
using System.Linq;
using Anomaly.Client.Render;
using Anomaly.Client.Shared.Models;
using Anomaly.Client.Shared.World;

namespace Anomaly.Client;

// The dev menu's Tools tab: a model's scale and offset changed live (the owner, 2026-11-06), then saved into the catalog.
public partial class MapRenderer
{
    /// <summary>The catalog file the models were read from.</summary>
    public static string ModelCatalogPath => Path.Combine(ModelLibrary.Root, ModelCatalog.FileName);

    /// <summary>The catalog entry that draws entity <paramref name="entityId"/> on the map; null when a picture draws it.</summary>
    public ModelEntry ModelOf(long entityId) => _records.TryGetValue(entityId, out StaticRecord record) ? record.Model : null;

    /// <summary>
    /// The pictures of <paramref name="type"/> and its subtypes are planned again with the world looks as they are now (WorldLooks, the dev menu's
    /// world things): one tuned to stand or lie, its size and its move. One the batch no longer draws is handed to the entity layer, as a moved one is.
    /// </summary>
    public void Retune(string type)
    {
        foreach (StaticRecord record in _records.Values.Where(r => r.Placed.Shape == RenderKind.Billboard && PropKinds.Is(r.Entity.TypePath, type)).ToList())
        {
            OnDespawned(record.Entity);
            if (!OnSpawned(record.Entity))
            {
                HandedOver++;
                PropMoved?.Invoke(record.Entity);
            }
        }
    }

    /// <summary>Everything <paramref name="entry"/> draws is built again from the entry as it is now (its scale and offset changed).</summary>
    public void Refit(ModelEntry entry)
    {
        if (_models == null)
            return;
        _models.Forget(entry.Id);
        foreach (StaticRecord record in _records.Values.Where(r => ReferenceEquals(r.Model, entry)).ToList())
            Rebuild(record);
    }
}
