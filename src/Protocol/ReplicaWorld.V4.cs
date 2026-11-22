namespace Anomaly.Protocol;

// Owner: the API is the M4a contracts step's (frozen, docs/ai/specs/m4a-ownership.md); the bodies are M4a P6's. The private
// state of version 4 (MessagesV4.cs) as the client's HUD, windows and view-model (M4a P7) and the bots read it. Every
// member reads after Apply on the caller's thread, like the version-2 members. The screen entries' groups ride on
// ScreenEntry.Group (the version-2 Screen, ScreenEntryChanged and ScreenEntryRemoved carry them).

public sealed partial class ReplicaWorld
{
    private readonly Dictionary<long, ImageEntry> _images = new();
    private readonly List<ImageEntry> _imagesTouched = new();
    private readonly List<long> _imagesGone = new();

    /// <summary>The own client's private images now (<c>client.images</c>, <see cref="ImageEntry"/>), by id. Applied from <see cref="OwnBlock.ImagesAdded"/>, <see cref="OwnBlock.ImagesChanged"/> and <see cref="OwnBlock.ImagesRemoved"/>.</summary>
    public IReadOnlyDictionary<long, ImageEntry> Images => _images;

    private Action<ImageEntry> _imageChangedHandlers;

    /// <summary>Raised after a message added or changed a private image (once per image and message, after the message is applied).</summary>
    public event Action<ImageEntry> ImageChanged
    {
        add => _imageChangedHandlers += value;
        remove => _imageChangedHandlers -= value;
    }

    private Action<long> _imageRemovedHandlers;

    /// <summary>Raised after a message removed a private image: its id.</summary>
    public event Action<long> ImageRemoved
    {
        add => _imageRemovedHandlers += value;
        remove => _imageRemovedHandlers -= value;
    }

    /// <summary>The screen entries of one HUD group (<see cref="HudGroup"/>), in increasing id order (tg's <c>show_contents</c> adds a storage's cells, its close button and its items, whose <c>screen_loc</c>s are tg's <c>orient_storage</c> layout).</summary>
    public IReadOnlyList<ScreenEntry> ScreenGroup(byte group)
    {
        var list = new List<ScreenEntry>();
        foreach (ScreenEntry entry in _screen.Values)
            if (entry.Group == group)
                list.Add(entry);
        list.Sort(static (a, b) => a.Id.CompareTo(b.Id));
        return list;
    }

    private static void ValidateSnapshotImages(OwnBlock own, TableCounts counts)
    {
        if (own.ImagesChanged.Count != 0 || own.ImagesRemoved.Count != 0)
            throw Bad("a Snapshot's private block lists images as changed or removed");
        if (own.ImagesAdded.Count > Limits.MaxPrivateImages)
            throw Bad("a Snapshot lists more than " + Limits.MaxPrivateImages + " private images");
        foreach (ImageEntry image in own.ImagesAdded)
        {
            TableRef a = image.Appearance;
            if ((a.IsNone && !image.Override) || a.Index > (a.Private ? counts.PrivateAppearances : counts.Appearances))
                throw Bad("private image " + image.Id + " names an appearance the table does not have");
        }
    }

    private void CheckImage(in ImageEntry image)
    {
        TableRef a = image.Appearance;
        // Protocol 35 permits an empty override image: it hides its host instead of drawing an appearance.
        if ((a.IsNone && !image.Override) || a.Index > (a.Private ? _privateAppearances.Count : _appearances.Count))
            throw Bad("private image " + image.Id + " names an appearance the table does not have");
    }

    private void ApplyImages(OwnBlock block, bool isSnapshot)
    {
        _imagesTouched.Clear();
        _imagesGone.Clear();
        if (isSnapshot)
        {
            if (block.ImagesChanged.Count != 0 || block.ImagesRemoved.Count != 0)
                throw Bad("a Snapshot's private block lists images as changed or removed");
            foreach (long id in _images.Keys)
                _imagesGone.Add(id);
            _images.Clear();
        }

        if ((long)_images.Count + block.ImagesAdded.Count - block.ImagesRemoved.Count > Limits.MaxPrivateImages)
            throw Bad("the client.images would hold more than " + Limits.MaxPrivateImages + " images");
        foreach (ImageEntry image in block.ImagesAdded)
        {
            CheckImage(image);
            if (_images.ContainsKey(image.Id))
                throw Bad("private image " + image.Id + " is added twice");
            _images[image.Id] = image;
            _imagesTouched.Add(image);
            _imagesGone.Remove(image.Id);
        }

        foreach (ImageEntry image in block.ImagesChanged)
        {
            CheckImage(image);
            if (!_images.ContainsKey(image.Id))
                throw Bad("private image " + image.Id + " changes and does not exist");
            _images[image.Id] = image;
            _imagesTouched.Add(image);
        }

        foreach (long id in block.ImagesRemoved)
        {
            if (!_images.Remove(id))
                throw Bad("private image " + id + " is removed and does not exist");
            _imagesGone.Add(id);
        }

        SyncImageVisuals();
    }

    // Version 30 (DV75): an image on a tile (tg's T-ray pulse) is drawn like a short-lived visual on that tile; an image on an entity too (M8 slice F). The replica makes one
    // ReplicaEntity for it, announced by the Spawned and Despawned events only (it is in no table of entities: no bot or test
    // counts it), with the negated image id as its id, the image's appearance and the tile. A progress bar is no such visual: the HUD
    // draws it (the bars' icon states are "prog_bar_NN", FeedbackModels.cs).
    private readonly Dictionary<long, ReplicaEntity> _imageVisuals = new();

    private const string ProgressBarStatePrefix = "prog_bar_";

    private void SyncImageVisuals()
    {
        foreach (long id in _imagesGone)
            DespawnImageVisual(id);
        foreach (ImageEntry image in _imagesTouched)
        {
            DespawnImageVisual(image.Id);
            // M8 slice F (D185): an image that hangs on an entity and is no bar (tg's data HUD images on a mob: the health bar, the status, the
            // job and implant icons) is a visual drawn on that entity (VisHost), for this viewer alone as the private block is
            // Version 35 (DV114): an override image (BYOND's image.override) may draw nothing; it replaces the picture of the entity it hangs on.
            AppearanceEntry look = default;
            bool hasLook = !image.Appearance.IsNone && TryGetAppearance(image.Appearance, out look);
            if ((image.TurfX == 0 && image.AttachedTo == 0) || (!hasLook && !image.Override))
                continue;
            string state = hasLook ? ResolveString(look.IconState) : null;
            if (state != null && state.StartsWith(ProgressBarStatePrefix, StringComparison.Ordinal))
                continue;
            var visual = new EntityState(-image.Id, 0, image.TurfX, image.TurfY, image.TurfZ, !hasLook || look.Dir == 0 ? (byte)2 : look.Dir, null, 0, _tick)
            {
                Appearance = image.Appearance,
                IsVisual = true,
                VisHost = image.TurfX == 0 ? image.AttachedTo : 0,
            };
            var entity = new ReplicaEntity(this, "/image", visual, _tick) { IsOverride = image.Override };
            _imageVisuals[image.Id] = entity;
            _spawnedScratch.Add(entity);
        }
    }

    private void DespawnImageVisual(long imageId)
    {
        if (_imageVisuals.Remove(imageId, out ReplicaEntity old))
            _despawnedScratch.Add(old);
    }

    // Raised once the whole message is applied (called by RaiseV2).
    private void RaiseImages()
    {
        if (_imagesTouched.Count == 0 && _imagesGone.Count == 0)
            return;
        ImageEntry[] touched = _imagesTouched.ToArray();
        long[] gone = _imagesGone.ToArray();
        _imagesTouched.Clear();
        _imagesGone.Clear();
        foreach (long id in gone)
            _imageRemovedHandlers?.Invoke(id);
        foreach (ImageEntry image in touched)
            _imageChangedHandlers?.Invoke(image);
    }
}

public sealed partial class ReplicaEntity
{
    /// <summary>Version 35 (DV114): this is a private image with BYOND's override flag (<see cref="ImageEntry.Override"/>): the client draws it in place of the picture of the entity it hangs on (<see cref="VisHost"/>).</summary>
    public bool IsOverride { get; internal set; }

    /// <summary>The entity's flags (<see cref="NetField.Flags"/>).</summary>
    public EntityFlags Flags => _state.Flags;

    /// <summary>True while the entity is in flight (<see cref="EntityFlags.Thrown"/>: D127's arc and spin).</summary>
    public bool IsThrown => (_state.Flags & EntityFlags.Thrown) != 0;

    /// <summary>tg showed its attack visual on the entity this tick (<see cref="EntityFlags.Hit"/>): the client's hit burst.</summary>
    public bool WasHit => (_state.Flags & EntityFlags.Hit) != 0;

    /// <summary>The BYOND direction of an attack lunge this tick, 0 for none.</summary>
    public byte LungeDir => (_state.Flags & EntityFlags.Lunge) != 0 ? (byte)((byte)_state.Flags >> 4) : (byte)0;

    /// <summary>Version 8: the appearance of the entity's last BYOND <c>flick</c> (<see cref="NetField.Flick"/>), none when it never flicked.</summary>
    public TableRef FlickAppearance => _state.FlickAppearance;

    /// <summary>Version 8: the server tick the last flick started in (0: none). A new flick changes it.</summary>
    public long FlickTick => _state.FlickTick;

    /// <summary>Version 9: the atom's running <c>animate()</c> chains (<see cref="NetField.Animation"/>), null for none.</summary>
    public EntityAnimation Animation => _state.Animation;

    /// <summary>Version 10: a short-lived visual drawn on another atom (tg's <c>flick_overlay_view</c>, <see cref="NetField.VisHost"/>).</summary>
    public bool IsVisual => _state.IsVisual;

    /// <summary>Version 10: the entity a visual is drawn on (0: the turf it stands on, or no visual).</summary>
    public long VisHost => _state.VisHost;

    /// <summary>Version 34: the shot's 3D line (<see cref="NetField.Shot"/>), null for an entity that is no shot.</summary>
    public ShotState? Shot => _state.Shot;
}
