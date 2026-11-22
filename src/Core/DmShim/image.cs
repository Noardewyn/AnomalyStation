namespace Anomaly.Core.DmShim;

/// <summary>
/// The BYOND <c>/image</c> object (BYOND ref "image objects", "vars (image)"; OpenDream DMStandard/Types/Image.dm): a
/// virtual image attached to an atom (<see cref="loc"/>), a plain data object that tg fills in for HUD icons and
/// overlays and that the presentation seam reads. A builtin value type: no world id, never registered, hashed by its
/// fields (object-model.md sections 3 and 8). Created by the builtin <see cref="Dm.image"/> or
/// <c>Dm.@new&lt;image&gt;(...)</c> ("The image() instruction is simply a short-hand for new/image()").
/// </summary>
/// <remarks>
/// Minimal (lead decision D3, wave 4): only the builtin vars tg reads at M0; the M2 contracts added the rest of the
/// appearance vars that <see cref="IDmAppearance"/> names and tg's M2 appearance code sets (<c>pixel_w</c>,
/// <c>pixel_z</c>, <c>transform</c>, <c>blend_mode</c>, <c>overlays</c>, <c>underlays</c>, <c>name</c>, <c>desc</c>,
/// <c>maptext</c>, <c>mouse_opacity</c>, <c>render_source</c>, <c>render_target</c>, <c>vis_flags</c>, <c>filters</c>). Field defaults are BYOND's (OpenDream's
/// Image.dm, which follows the reference): layer <c>FLOAT_LAYER</c>, plane <c>FLOAT_PLANE</c>, alpha 255, dir
/// <c>SOUTH</c> (the default appearance's direction), everything else 0 or null. The class lives in the shim, like
/// <see cref="sound"/> and <see cref="matrix"/>: a class named <c>image</c> in the namespace of translated code would
/// hide the bare builtin <c>image(...)</c> (C# finds a type of the current namespace before a <c>using static</c>
/// member), and tg's procs on <c>/image</c> become C# extension members in Tg when they are translated
/// (docs/ai/specs/shim-ownership.md).
/// </remarks>
[DmPath("/image")]
public class image : IDmAppearance
{
    /// <summary>The icon: a resource (<see cref="DmResource"/>), or text where tg passes text.</summary>
    public object icon;

    /// <summary>The icon state. Default null.</summary>
    public string icon_state;

    /// <summary>The atom the image is attached to, or null. Assigning it does not move anything (move-chain.md hazard 13).</summary>
    public object loc;

    /// <summary>The drawing layer. Default <c>FLOAT_LAYER</c> (-1).</summary>
    public float layer = Dm.FLOAT_LAYER;

    /// <summary>The plane. Default <c>FLOAT_PLANE</c> (-32767).</summary>
    public float plane = Dm.FLOAT_PLANE;

    /// <summary>The direction. Default <c>SOUTH</c>.</summary>
    public float dir = Dm.SOUTH;

    /// <summary>Horizontal pixel offset. Default 0.</summary>
    public float pixel_x;

    /// <summary>Vertical pixel offset. Default 0.</summary>
    public float pixel_y;

    /// <summary>Appearance flags. Default 0.</summary>
    public float appearance_flags;

    /// <summary>Opacity, 0 to 255. Default 255.</summary>
    public float alpha = 255;

    /// <summary>A color text or a color matrix. Default null.</summary>
    public object color;

    /// <summary>Invisibility level. Default 0.</summary>
    public float invisibility;

    /// <summary>1 to replace the appearance of <see cref="loc"/> for viewers who see the image. Default 0.</summary>
    public float @override;

    // ---- M2 contracts: the rest of the appearance (BYOND ref "vars (image)"; OpenDream DMStandard/Types/Image.dm) ----

    /// <summary>Horizontal pixel offset that also moves the image's place in the map's layering. Default 0.</summary>
    public float pixel_w;

    /// <summary>Vertical pixel offset, as <see cref="pixel_w"/>. Default 0.</summary>
    public float pixel_z;

    /// <summary>
    /// The transformation (BYOND ref <c>/atom/var/transform</c>, which images share: "When you read this var, you will get a copy
    /// of the current transformation"). A read gives a new <see cref="matrix"/>, the identity when the image has none, so
    /// <c>image.transform.Scale(0.75)</c> changes nothing and <c>image.transform *= 0.4</c> scales the identity (tg's
    /// <c>do_item_attack_animation</c>, items.dm:1596); an assignment stores a copy of a matrix, or null for none. The stored
    /// value (null by default) is what <see cref="IDmAppearance.transform"/> reports (M4v slice 3; the field was read raw before).
    /// </summary>
    public object transform
    {
        get => _transform is matrix m ? Dm.matrix(m) : Dm.matrix();
        set => _transform = value is matrix m ? Dm.matrix(m) : value;
    }

    private object _transform;

    /// <summary>How the image is blended (<c>BLEND_DEFAULT</c> 0, <c>BLEND_OVERLAY</c>, <c>BLEND_ADD</c>, ...). Default 0.</summary>
    public float blend_mode;

    /// <summary>The image's own overlays (BYOND: a list, like an atom's). Default an empty list.</summary>
    public DmList overlays = new DmList();

    /// <summary>The image's own underlays. Default an empty list.</summary>
    public DmList underlays = new DmList();

    /// <summary>
    /// The objects drawn as part of the image (BYOND ref <c>/image/var/vis_contents</c>; silicons SI4: tg's AI detector HUD puts its
    /// overlay object in each of its images). Default an empty list. The replicator flattens them into the image's overlays.
    /// </summary>
    public DmList vis_contents = new DmList();

    /// <summary>The name. Default null.</summary>
    public string name;

    /// <summary>The description. Default null.</summary>
    public string desc;

    /// <summary>The maptext. Default null.</summary>
    public string maptext;

    /// <summary>BYOND <c>maptext_width</c>. Default 32.</summary>
    public float maptext_width = 32;

    /// <summary>BYOND <c>maptext_height</c>. Default 32.</summary>
    public float maptext_height = 32;

    /// <summary>BYOND <c>maptext_x</c>. Default 0.</summary>
    public float maptext_x;

    /// <summary>BYOND <c>maptext_y</c>. Default 0.</summary>
    public float maptext_y;

    /// <summary>BYOND <c>mouse_opacity</c>. Default 1.</summary>
    public float mouse_opacity = 1;

    /// <summary>BYOND <c>render_source</c>. Default null.</summary>
    public object render_source;

    /// <summary>BYOND <c>render_target</c>. Default null.</summary>
    public object render_target;

    /// <summary>BYOND <c>vis_flags</c>. Default 0.</summary>
    public float vis_flags;

    private DmList _filters;

    /// <summary>
    /// tg's <c>/datum/var/filter_data</c> (datum.dm:59) as an image holds it: BYOND's images are datums, and tg's filter procs
    /// (<c>add_filter</c> and the rest, run on images by Anomaly.Core.Tg's image_filter_procs; M6 slice 7b, D156) keep their state here.
    /// </summary>
    public DmList filter_data;

    /// <summary>tg's <c>/datum/var/filter_cache</c> (datum.dm:63) as an image holds it (see <see cref="filter_data"/>).</summary>
    public DmList filter_cache;

    /// <summary>BYOND <c>filters</c>. Default null (no filters); an assignment keeps a filters list (<see cref="Dm.filter_list"/>; M6 slice 7, D155).</summary>
    public object filters
    {
        get => _filters;
        set => _filters = Dm.filter_list(value, this);
    }

    /// <summary>
    /// BYOND <c>appearance</c> (BYOND ref <c>/image/var/appearance</c>, as <c>/atom/var/appearance</c>): assigning an appearance (an image, a
    /// mutable appearance, an atom or an appearance snapshot) sets every appearance var of the image from it and keeps its <see cref="loc"/>
    /// (tg's <c>t_ray_scan</c>, t_scanner.dm:59, shows an object's copy at its turf). A read gives a snapshot of the image.
    /// </summary>
    public object appearance
    {
        get => DmAppearance.Of(this);
        set
        {
            if (value is not IDmAppearance source)
                throw new DmRuntimeException("appearance: expected an image, a mutable appearance, an atom or an appearance");
            icon = source.icon;
            icon_state = source.icon_state;
            dir = source.dir;
            layer = source.layer;
            plane = source.plane;
            alpha = source.alpha;
            color = source.color;
            pixel_x = source.pixel_x;
            pixel_y = source.pixel_y;
            pixel_w = source.pixel_w;
            pixel_z = source.pixel_z;
            transform = source.transform;
            appearance_flags = source.appearance_flags;
            blend_mode = source.blend_mode;
            invisibility = source.invisibility;
            overlays = source.overlays?.Copy();
            underlays = source.underlays?.Copy();
            if (value is image { name: not null } named)
                name = named.name;
        }
    }

    // ---- IDmAppearance (the fields above, read-only) ----

    object IDmAppearance.icon => icon;
    string IDmAppearance.icon_state => icon_state;
    float IDmAppearance.dir => dir;
    float IDmAppearance.layer => layer;
    float IDmAppearance.plane => plane;
    float IDmAppearance.alpha => alpha;
    object IDmAppearance.color => color;
    string IDmAppearance.maptext => maptext;
    float IDmAppearance.maptext_width => maptext_width;
    float IDmAppearance.maptext_height => maptext_height;
    float IDmAppearance.maptext_x => maptext_x;
    float IDmAppearance.maptext_y => maptext_y;
    object IDmAppearance.filters => filters;
    object IDmAppearance.render_target => render_target;
    float IDmAppearance.pixel_x => pixel_x;
    float IDmAppearance.pixel_y => pixel_y;
    float IDmAppearance.pixel_w => pixel_w;
    float IDmAppearance.pixel_z => pixel_z;
    object IDmAppearance.transform => _transform;
    float IDmAppearance.appearance_flags => appearance_flags;
    float IDmAppearance.blend_mode => blend_mode;
    float IDmAppearance.invisibility => invisibility;
    DmList IDmAppearance.overlays => overlays ?? new DmList();
    DmList IDmAppearance.underlays => underlays ?? new DmList();
}
