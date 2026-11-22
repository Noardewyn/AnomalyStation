namespace Anomaly.Core.DmShim;

// Owner: presentation. M6 slice 7 (D155): the filters list (BYOND ref "filters"; OpenDream DreamFilterList): an atom's or an
// image's filters, where a text index or a removed text names a filter.
public sealed partial class DmList
{
    // True for a filters list (Dm.filter_list): the indexer and the removal take a filter's name.
    private bool _isFilters;

    /// <summary>Whether this is a filters list (<see cref="Dm.filter_list"/>).</summary>
    public bool IsFilters => _isFilters;

    internal static DmList NewFilters(params dm_filter[] filters)
    {
        var made = new DmList(filters.Length, false) { _isFilters = true };
        foreach (dm_filter f in filters)
            made.AppendItem(f);
        return made;
    }

    // filters[name]: the first filter with that name (BYOND ref "filters": "filters[name]").
    private dm_filter FilterNamed(string name)
    {
        for (int i = 0; i < _count; i++)
            if (_items[i] is dm_filter f && f.name == name) return f;
        return null;
    }

    // filters -= name: removes the filters with that name (OpenDream DreamFilterList.RemoveValue); whether one was removed.
    private bool RemoveFiltersNamed(string name)
    {
        bool removed = false;
        for (int i = _count - 1; i >= 0; i--)
        {
            if (_items[i] is not dm_filter f || f.name != name) continue;
            RemoveRange(i, 1);
            removed = true;
        }
        return removed;
    }
}
