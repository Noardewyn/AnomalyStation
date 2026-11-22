// Engine ("BYOND builtin") part of tg's /atom (docs/ai/translation-rules.md 1.1; docs/ai/specs/object-model.md sections 1.1,
// 2 and 4; docs/ai/specs/move-chain.md E1, E3, E4, E5; docs/ai/specs/presentation-seam.md section 5). Not translated from a tg
// file: BYOND itself declares /atom, its builtin vars and its builtin procs. tg's own vars and procs of /atom are in the
// mirror files of the .dm files that declare them (game/atom/_atom.cs, ...).
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;

namespace Anomaly.Core.Tg;

// RULE-GAP: as for /datum (Builtin/datum.builtin.cs), the engine part of a BYOND builtin type carries its [DmPath].
[DmPath("/atom")]
public partial class atom : datum, IDmAtom, IDmListOwner, IDmAppearance
{
    // ---- location (move-chain.md E1, E3, E5) ----
    //
    // The engine keeps two records of what an atom holds: the DM list _contents, which translated code reads and edits
    // with list operators (rules 4.3: Dm.append(A.contents, x), A.contents = L), and its own list _held of the same atoms
    // in the same order. Every engine move (a loc assignment, the silent placement of a new atom, a turf replacement)
    // changes both. An edit through the DM list is a move in BYOND ("contents += X" is "X.loc = src", move-chain.md E1),
    // and the DmList tells its owner after every write (DmList.dm_owner, IDmListOwner), possibly in the middle of a list
    // operation; so a write by DM code only marks the atom (ListChanged), and every engine access (loc, x, y, z,
    // contents, the map builtins) first applies the marked atoms' edits as raw moves (MapGrid.Sync,
    // dm_reconcile_contents). A list edit therefore takes effect before anything can observe it through the engine,
    // through whatever reference it was made. Two edits between engine accesses are applied as their net difference:
    // `L += X; L -= X` leaves X where it was, where BYOND leaves it in nullspace.

    // The container (BYOND loc); hashed through this backing field (object-model.md section 8).
    private atom _loc;

    // BYOND contents: never null, the same list object for the atom's lifetime.
    private readonly DmList _contents = new DmList();

    // The engine's own record of _contents (same atoms, same order): an ordered set with an index (M3 P2, the
    // area-membership index: an area's turfs are added and removed one at a time, Builtin/HeldSet.cs); null until the atom
    // first holds something. Engine bookkeeping, not DM state.
    [NotHashed]
    private HeldSet _held;

    // M3 P2: what DM code did to _contents since this atom was marked (MapGrid.MarkHandedOut). While every write was the
    // insertion of one item (an append: tg's `area.contents += turf`, `contents.Add(x)`), the count of those writes;
    // -1 once any other write happened. The reconcile then checks the appended items and applies them without rebuilding
    // the whole list (dm_reconcile_contents).
    [NotHashed]
    private int _dm_appends;

    // Set while this atom's contents list has been edited by DM code since the last MapGrid.Sync.
    [NotHashed]
    internal bool dm_contents_handed_out;

    // Set while the engine itself writes _contents (its writes keep _held in step and need no reconcile).
    [NotHashed]
    private bool _dm_engine_writing;

    /// <summary>IDmListOwner: DM code wrote this atom's contents list; the next engine access applies it (MapGrid.Sync).</summary>
    void IDmListOwner.ListChanged(DmList list)
    {
        if (_dm_engine_writing || !ReferenceEquals(list, _contents)) return;
        if (!dm_contents_handed_out) _dm_appends = 0;
        if (_dm_appends >= 0)
            _dm_appends = list.Count == (_held?.Count ?? 0) + _dm_appends + 1 ? _dm_appends + 1 : -1;
        dm_engine()?.MarkHandedOut(this);
    }

    /// <summary>
    /// BYOND <c>loc</c>: the container. Setting it is the raw, silent move of move-chain.md E1: the atom leaves the old
    /// container's <c>contents</c> (the others keep their order), is appended to the new one's, and nothing is called
    /// (no Enter, Exit, Entered, Exited, Cross, Moved, no signal). A cyclic assignment (the atom itself, or an atom inside
    /// it) is refused silently, as BYOND does (tg's doMove detects it by <c>loc == oldloc</c>, atoms_movable.dm:1193);
    /// null puts it in nullspace (x = y = z = 0). An area's loc is always null; a turf's loc is its area and changes only to
    /// another area (BYOND ref <c>/atom/var/loc</c>, <c>/turf</c>). A movable given an area with turfs on the map lands on the
    /// area's first turf (OpenDream's SetLoc; syndicate G4c: tg's supply pods wait in CentCom's holding area, where a client mob
    /// entering one must find a turf above the pod); an area off the map is a room the movable enters directly (BYOND ref /area).
    /// </summary>
    public atom loc
    {
        get
        {
            dm_engine()?.Sync();
            return _loc;
        }
        set => dm_set_loc(value);
    }

    /// <summary>
    /// BYOND <c>x</c>: a turf's own coordinate (0 off the map); a movable's is its turf's when it stands directly on one,
    /// 0 inside another atom or in nullspace (move-chain.md E1: use get_turf); an area's is that of its turf with the
    /// lowest z, then y, then x (BYOND ref <c>/atom/var/x</c>). Assigning it moves a movable raw to that turf of its z
    /// level when the turf exists (BYOND: "You may assign the coordinates of movable objects"); a turf's and an area's
    /// coordinates cannot be assigned.
    /// </summary>
    public float x
    {
        get => dm_coord(0);
        set => dm_set_coord(0, value);
    }

    /// <summary>BYOND <c>y</c> (see <see cref="x"/>).</summary>
    public float y
    {
        get => dm_coord(1);
        set => dm_set_coord(1, value);
    }

    /// <summary>BYOND <c>z</c> (see <see cref="x"/>).</summary>
    public float z
    {
        get => dm_coord(2);
        set => dm_set_coord(2, value);
    }

    /// <summary>
    /// BYOND <c>contents</c>: engine-maintained and never null, in arrival order (move-chain.md E1). List operators on it
    /// move atoms: <c>contents += X</c> is <c>X.loc = src</c>, <c>contents -= X</c> is <c>X.loc = null</c>, and assigning a
    /// list makes it the contents (atoms not in it leave to nullspace). A turf only ever joins an area, an area never
    /// joins anything, and a cyclic addition is refused, as for <see cref="loc"/>. An area's contents hold its turfs (a
    /// movable given a mapped area lands on its first turf, see <see cref="loc"/>; an off-map area holds movables as a room).
    /// </summary>
    public DmList contents
    {
        get
        {
            MapGrid grid = dm_engine();
            if (grid != null)
            {
                grid.Sync();
                _contents.dm_owner ??= this; // from now on DM code can write it (see above)
            }
            return _contents;
        }
        set
        {
            MapGrid grid = dm_engine();
            grid?.Sync();
            if (!ReferenceEquals(value, _contents))
            {
                _dm_engine_writing = true;
                _contents.Cut();
                if (value != null) _contents.Add(value); // a list argument adds its items in order
                _dm_engine_writing = false;
            }
            if (grid != null) dm_reconcile_contents();
        }
    }

    // ---- plain builtin vars (object-model.md section 2; defaults: BYOND ref, OpenDream DMStandard/Types/Atoms/_Atom.dm) ----

    private string _name;
    /// <summary>
    /// BYOND <c>name</c>. Its default is "the name of the object type with underscores converted to spaces" (BYOND ref
    /// <c>/atom/var/name</c>): the builtin parts of /atom/movable, /turf and /area set it from the runtime type in their
    /// constructors, before any tg override (an ancestor's explicit name wins, as in OpenDream's DreamObjectTree).
    /// <para>
    /// An engine property since M2 (docs/ai/specs/replication.md section 5.2; object-model.md section 2): BYOND's name is an
    /// appearance var, and tg's humans rename themselves (<c>update_visible_name</c>), so an assignment to a registered
    /// atom notifies <c>Changed(Name)</c> and the replication hint sink like the other appearance vars. The value is kept
    /// in <c>_name</c>, which the state hash reads as it reads <c>_tag</c>.
    /// </para>
    /// </summary>
    public string name
    {
        get => _name;
        set
        {
            _name = value;
            dm_appearance_changed(AppearanceAspect.Name);
        }
    }
    /// <summary>BYOND <c>desc</c>.</summary>
    public string desc;
    /// <summary>BYOND <c>gender</c>: <c>MALE</c>, <c>FEMALE</c>, <c>NEUTER</c> (the default) or <c>PLURAL</c>.</summary>
    public string gender = NEUTER;
    /// <summary>BYOND <c>text</c>: the text-mode symbol (text mode is not ported; BYOND's default, the name's first letter, is not computed).</summary>
    public string text;
    /// <summary>BYOND <c>suffix</c>.</summary>
    public string suffix;
    /// <summary>BYOND <c>density</c> (default 0; 1 for mobs, set by the /mob engine part).</summary>
    public float density;
    /// <summary>BYOND <c>luminosity</c> (default 0; 1 for areas, set by the /area engine part).</summary>
    public float luminosity;
    /// <summary>BYOND <c>mouse_opacity</c> (default 1).</summary>
    public float mouse_opacity = 1;
    /// <summary>BYOND <c>infra_luminosity</c>.</summary>
    public float infra_luminosity;
    /// <summary>
    /// BYOND <c>mouse_drag_pointer</c> (default <c>MOUSE_INACTIVE_POINTER</c>, 0; BYOND ref
    /// <c>/atom/var/mouse_drag_pointer</c>): how the mouse looks while dragging the atom, a purely visual hint (W4.0:
    /// tg's /mob overrides it, mob_defines.dm:16).
    /// </summary>
    public object mouse_drag_pointer = MOUSE_INACTIVE_POINTER;
    /// <summary>BYOND <c>mouse_over_pointer</c> (default <c>MOUSE_INACTIVE_POINTER</c>, 0): how the mouse looks held over the atom, a purely visual hint (M2 contracts: tg's screen objects set it).</summary>
    public object mouse_over_pointer = MOUSE_INACTIVE_POINTER;
    /// <summary>BYOND <c>mouse_drop_pointer</c> (default <c>MOUSE_ACTIVE_POINTER</c>, 1): how the mouse looks dragging the atom over a drop zone, a visual hint (M2 contracts).</summary>
    public object mouse_drop_pointer = MOUSE_ACTIVE_POINTER;
    /// <summary>BYOND <c>mouse_drop_zone</c> (default 0): whether the atom shows as a site to drop things on, "a purely visual effect" (M2 contracts: tg's carbons set it).</summary>
    public float mouse_drop_zone;

    // ---- visibility (presentation-seam.md section 5: VisibilityChanged) ----

    private float _invisibility;
    /// <summary>BYOND <c>invisibility</c>; an assignment to a registered atom notifies <c>VisibilityChanged</c>.</summary>
    public float invisibility
    {
        get => _invisibility;
        set
        {
            _invisibility = value;
            dm_visibility_changed();
        }
    }

    private float _opacity;
    /// <summary>BYOND <c>opacity</c>; an assignment to a registered atom notifies <c>VisibilityChanged</c>.</summary>
    public float opacity
    {
        get => _opacity;
        set
        {
            _opacity = value;
            dm_visibility_changed();
        }
    }

    // ---- appearance (presentation-seam.md section 5: an assignment to a registered atom notifies Changed) ----

    private object _icon;
    /// <summary>BYOND <c>icon</c>.</summary>
    public object icon
    {
        get => _icon;
        set
        {
            _icon = value;
            dm_appearance_changed(AppearanceAspect.Icon);
        }
    }

    private string _icon_state;
    /// <summary>BYOND <c>icon_state</c>.</summary>
    public string icon_state
    {
        get => _icon_state;
        set
        {
            _icon_state = value;
            dm_appearance_changed(AppearanceAspect.IconState);
        }
    }

    private string _maptext;
    /// <summary>BYOND <c>maptext</c>.</summary>
    public string maptext
    {
        get => _maptext;
        set
        {
            _maptext = value;
            dm_appearance_changed(AppearanceAspect.Maptext);
        }
    }

    private float _dir = SOUTH;
    /// <summary>BYOND <c>dir</c> (default <c>SOUTH</c>). BYOND ignores a set to 0 or less (OpenDream AtomManager.cs: "Ignore any sets <= 0"):
    /// tg's <c>dir = NONE</c> on a type (disposal pipes) leaves it SOUTH, which its maps rely on.</summary>
    public float dir
    {
        get => _dir;
        set
        {
            if (!(value > 0))
                return;
            _dir = value;
            dm_appearance_changed(AppearanceAspect.Dir);
        }
    }

    private float _layer = 2;
    /// <summary>BYOND <c>layer</c> (default 2 for /atom; 1 for areas, 2 for turfs, 3 for objs, 4 for mobs, set by their engine parts).</summary>
    public float layer
    {
        get => _layer;
        set
        {
            _layer = value;
            dm_appearance_changed(AppearanceAspect.Layer);
        }
    }

    private float _plane;
    /// <summary>BYOND <c>plane</c>.</summary>
    public float plane
    {
        get => _plane;
        set
        {
            _plane = value;
            dm_appearance_changed(AppearanceAspect.Plane);
        }
    }

    private float _alpha = 255;
    /// <summary>BYOND <c>alpha</c> (default 255).</summary>
    public float alpha
    {
        get => _alpha;
        set
        {
            _alpha = value;
            dm_appearance_changed(AppearanceAspect.Alpha);
        }
    }

    private float _pixel_x;
    /// <summary>BYOND <c>pixel_x</c>.</summary>
    public float pixel_x
    {
        get => _pixel_x;
        set
        {
            _pixel_x = value;
            dm_appearance_changed(AppearanceAspect.Pixel);
        }
    }

    private float _pixel_y;
    /// <summary>BYOND <c>pixel_y</c>.</summary>
    public float pixel_y
    {
        get => _pixel_y;
        set
        {
            _pixel_y = value;
            dm_appearance_changed(AppearanceAspect.Pixel);
        }
    }

    private float _pixel_w;
    /// <summary>BYOND <c>pixel_w</c>.</summary>
    public float pixel_w
    {
        get => _pixel_w;
        set
        {
            _pixel_w = value;
            dm_appearance_changed(AppearanceAspect.Pixel);
        }
    }

    private float _pixel_z;
    /// <summary>BYOND <c>pixel_z</c>.</summary>
    public float pixel_z
    {
        get => _pixel_z;
        set
        {
            _pixel_z = value;
            dm_appearance_changed(AppearanceAspect.Pixel);
        }
    }

    private float _appearance_flags;
    /// <summary>BYOND <c>appearance_flags</c>.</summary>
    public float appearance_flags
    {
        get => _appearance_flags;
        set
        {
            _appearance_flags = value;
            dm_appearance_changed(AppearanceAspect.AppearanceFlags);
        }
    }

    private float _blend_mode;
    /// <summary>BYOND <c>blend_mode</c>.</summary>
    public float blend_mode
    {
        get => _blend_mode;
        set
        {
            _blend_mode = value;
            dm_appearance_changed(AppearanceAspect.BlendMode);
        }
    }

    private float _maptext_width = 32;
    /// <summary>BYOND <c>maptext_width</c> (default 32, one icon width).</summary>
    public float maptext_width
    {
        get => _maptext_width;
        set
        {
            _maptext_width = value;
            dm_appearance_changed(AppearanceAspect.Maptext);
        }
    }

    private float _maptext_height = 32;
    /// <summary>BYOND <c>maptext_height</c> (default 32, one icon height).</summary>
    public float maptext_height
    {
        get => _maptext_height;
        set
        {
            _maptext_height = value;
            dm_appearance_changed(AppearanceAspect.Maptext);
        }
    }

    private float _maptext_x;
    /// <summary>BYOND <c>maptext_x</c>.</summary>
    public float maptext_x
    {
        get => _maptext_x;
        set
        {
            _maptext_x = value;
            dm_appearance_changed(AppearanceAspect.Maptext);
        }
    }

    private float _maptext_y;
    /// <summary>BYOND <c>maptext_y</c>.</summary>
    public float maptext_y
    {
        get => _maptext_y;
        set
        {
            _maptext_y = value;
            dm_appearance_changed(AppearanceAspect.Maptext);
        }
    }

    private float _vis_flags;
    /// <summary>BYOND <c>vis_flags</c> (how the atom is drawn in another atom's vis_contents: reported as <c>AppearanceFlags</c>).</summary>
    public float vis_flags
    {
        get => _vis_flags;
        set
        {
            _vis_flags = value;
            dm_appearance_changed(AppearanceAspect.AppearanceFlags);
        }
    }

    private object _color;
    /// <summary>BYOND <c>color</c>.</summary>
    public object color
    {
        get => _color;
        set
        {
            _color = value;
            dm_appearance_changed(AppearanceAspect.Color);
        }
    }

    private object _transform;
    /// <summary>
    /// BYOND <c>transform</c> (BYOND ref <c>/atom/var/transform</c>, M2 contracts): "Whenever you read the atom.transform var,
    /// you will get a copy of the atom's current transformation. Whenever you assign a value to the var, you will update the
    /// transformation." So a read gives a new <c>/matrix</c> (changing it changes nothing until it is assigned back, tg's
    /// <c>var/matrix/M = transform; M.Turn(90); transform = M</c>), an assignment stores a copy of the matrix, null means no
    /// transformation, and a list of six numbers is the matrix <c>a b c d e f</c>; any other value is a runtime error. An
    /// atom with no transformation reads the identity matrix (the var's default is <c>matrix()</c>; M2 integration, P2's
    /// SHIM-GAP); the stored value stays null.
    /// </summary>
    public object transform
    {
        get => _transform is matrix m ? Dm.matrix(m) : Dm.matrix();
        set
        {
            _transform = value switch
            {
                null => null,
                matrix m => Dm.matrix(m),
                DmList list when list.Count == 6 => Dm.matrix(Dm.num(list[1]), Dm.num(list[2]), Dm.num(list[3]), Dm.num(list[4]), Dm.num(list[5]), Dm.num(list[6])),
                _ => throw new DmRuntimeException("transform: expected a matrix, a list of six numbers or null"),
            };
            dm_appearance_changed(AppearanceAspect.Transform);
        }
    }

    private object _filters;
    /// <summary>BYOND <c>filters</c>: null for none, else a filters list (<see cref="Dm.filter_list"/>; M6 slice 7, D155).</summary>
    public object filters
    {
        get => _filters;
        set
        {
            _filters = Dm.filter_list(value, this);
            dm_appearance_changed(AppearanceAspect.Filters);
        }
    }

    private object _render_target;
    /// <summary>BYOND <c>render_target</c>.</summary>
    public object render_target
    {
        get => _render_target;
        set
        {
            _render_target = value;
            dm_appearance_changed(AppearanceAspect.Render);
        }
    }

    private object _render_source;
    /// <summary>BYOND <c>render_source</c>.</summary>
    public object render_source
    {
        get => _render_source;
        set
        {
            _render_source = value;
            dm_appearance_changed(AppearanceAspect.Render);
        }
    }

    // ---- engine-owned lists (never null; assigning null clears, assigning a list replaces the contents) ----

    private readonly DmList _overlays = new DmList();
    /// <summary>
    /// BYOND <c>overlays</c>: never null; assigning null clears it (tg's <c>cut_overlays</c>), assigning a list replaces its
    /// items, and the assignment notifies <c>Changed(Overlays)</c>. A mutation through list operators changes the list
    /// in place (rules 4.3) but notifies nothing (SHIM-GAP above: the notification is only a replication hint,
    /// presentation-seam.md section 1 rule 5).
    /// </summary>
    public DmList overlays
    {
        get => _overlays;
        set => dm_replace_engine_list(_overlays, value, AppearanceAspect.Overlays);
    }

    private readonly DmList _underlays = new DmList();
    /// <summary>BYOND <c>underlays</c> (as <see cref="overlays"/>).</summary>
    public DmList underlays
    {
        get => _underlays;
        set => dm_replace_engine_list(_underlays, value, AppearanceAspect.Underlays);
    }

    private readonly DmList _vis_contents = new DmList();
    /// <summary>BYOND <c>vis_contents</c> (as <see cref="overlays"/>).</summary>
    public DmList vis_contents
    {
        get => _vis_contents;
        set => dm_replace_engine_list(_vis_contents, value, AppearanceAspect.VisContents);
    }

    // BYOND vis_locs and appearance: Builtin/atom.vis_locs.builtin.cs (M4v slice 3).

    // BYOND verbs: not translated until the command layer (rules 7.10; object-model.md section 2).

    // ---- IDmAtom (object-model.md section 1.1): contents is the property above ----

    string IDmAtom.name => name;

    string IDmAtom.gender => gender;

    int IDmAtom.dm_world_order => this is mob ? 0 : this is area ? 2 : this is turf ? 3 : 1;

    // ---- IDmAppearance (M2 contracts): the appearance vars above, read-only, for the server's appearance interning ----

    object IDmAppearance.icon => icon;
    string IDmAppearance.icon_state => icon_state;
    float IDmAppearance.dir => dir;
    float IDmAppearance.layer => layer;
    float IDmAppearance.plane => plane;
    float IDmAppearance.alpha => alpha;
    object IDmAppearance.color => color;
    float IDmAppearance.pixel_x => pixel_x;
    float IDmAppearance.pixel_y => pixel_y;
    float IDmAppearance.pixel_w => pixel_w;
    float IDmAppearance.pixel_z => pixel_z;
    object IDmAppearance.transform => _transform; // the stored matrix itself: readers outside the world copy nothing (and change nothing)
    float IDmAppearance.appearance_flags => appearance_flags;
    float IDmAppearance.blend_mode => blend_mode;
    float IDmAppearance.invisibility => invisibility;
    DmList IDmAppearance.overlays => overlays;
    DmList IDmAppearance.underlays => underlays;

    // ---- builtin procs that tg overrides (docs/ai/specs/generated/slots.tsv, root "builtin"; move-chain.md E4) ----

    /// <summary>
    /// BYOND <c>MouseExited(location, control, params)</c>: called when the mouse leaves the atom; the builtin does nothing
    /// (BYOND ref <c>/atom/proc/MouseExited</c>). M4c slice 2 declares the root for the space suit display
    /// (<c>screen_objects.dm:487</c>); the host calls it when the pointer leaves the atom (ClientSeam.Hover and Unhover, protocol
    /// version 14, M4c slice 3).
    /// </summary>
    public virtual void MouseExited(object location = null, object control = null, object @params = null)
    {
    }

    /// <summary>
    /// BYOND <c>MouseDrag(over_object, src_location, over_location, src_control, over_control, params)</c>: called on the dragged atom
    /// while the mouse moves with a button held; the builtin does nothing (BYOND ref <c>/atom/proc/MouseDrag</c>). tg overrides it on the
    /// action buttons (action_button.dm:79, not declared yet) and the admin light buttons. Protocol version 17: the host calls it through
    /// tg's <c>/client/MouseDrag</c> once at the drop (ClientSeam.Drop).
    /// </summary>
    public virtual async DmTask MouseDrag(atom over_object = null, object src_location = null, object over_location = null, object src_control = null, object over_control = null, object @params = null)
    {
    }

    /// <summary>
    /// BYOND <c>Enter(O, oldloc)</c>: "Areas, objs, and mobs will always permit anything to enter by default"; a turf
    /// permits when it and everything on it let O cross (BYOND ref <c>/atom/proc/Enter</c>; OpenDream
    /// DMStandard/Types/Atoms/Turf.dm). tg replaces /turf/Enter (game/turfs/turf.dm:434, "Do not call ..()"), so the
    /// turf branch is reached only by a turf type of the port's own tests.
    /// </summary>
    public virtual async DmTask<object> Enter(atom_movable O = null, atom oldloc = null)
    {
        if (this is not turf)
            return TRUE;
        if (!Dm.truthy(await Cross(O)))
            return FALSE;
        foreach (atom content in Dm.each<atom>(this))
        {
            if (!Dm.truthy(await content.Cross(O)))
                return FALSE;
        }
        return TRUE;
    }

    /// <summary>
    /// BYOND <c>Cross(O)</c>: "Allow overlap unless both atoms are dense" (BYOND ref <c>/atom/proc/Cross</c>). BYOND's
    /// exception for two mobs of the same <c>group</c> does not apply: tg never puts mobs in the builtin group list
    /// (move-chain.md E4). tg replaces it on /atom/movable (atoms_movable.dm:934).
    /// </summary>
    public virtual DmTask<object> Cross(atom_movable O = null)
    {
        bool both_dense = Dm.truthy(density) && O != null && Dm.truthy(O.density);
        return DmTask<object>.FromResult(both_dense ? FALSE : TRUE);
    }

    /// <summary>BYOND <c>Crossed(O)</c>: no default action; the engine never calls it (tg's Move rewrite, move-chain.md E4).</summary>
    public virtual void Crossed(atom_movable O = null, params object[] __extra)
    {
    }

    /// <summary>BYOND <c>Uncross(O)</c>: allows O to get away (returns 1); the engine never calls it (move-chain.md E4).</summary>
    public virtual object Uncross(atom_movable O = null)
    {
        return TRUE;
    }

    /// <summary>BYOND <c>Uncrossed(O)</c>: no default action; the engine never calls it (move-chain.md E4).</summary>
    public virtual void Uncrossed(atom_movable O = null)
    {
    }

    // ---- creation (object-model.md section 4, step 5) ----

    /// <summary>
    /// The pre-New hook: the var initializers (<c>dm_init_vars</c>, through <c>datum</c>), then the silent placement at
    /// the initial location, so that "by the time New() is called, the object has already been created at the specified
    /// location" (BYOND ref <c>/atom/proc/New</c>) and nothing fires: no Enter, Entered or signal (BYOND ref Entered:
    /// "The same goes for creation or deletion of an object at a location"; move-chain.md E1).
    /// </summary>
    public override void dm_pre_new(DmArgs args)
    {
        base.dm_pre_new(args);
        dm_place_new(args.InitialLocation);
    }

    /// <summary>
    /// Places a new atom at its initial location before New: a movable is put raw into the atom it was given (a non-atom
    /// leaves it in nullspace). /turf and /area replace this (a new turf replaces the turf at that location, a new area
    /// takes the turf it was given).
    /// </summary>
    private protected virtual void dm_place_new(object initial_loc)
    {
        if (initial_loc is atom where)
            dm_set_loc(where);
    }

    // ---- the engine (not DM names; the dm_ prefix marks port machinery, like dm_pre_new) ----

    // The map engine of the ambient world for a registered atom; null for a prototype or an object still in its
    // constructor (object-model.md section 2: their setters only store) and outside a world scope.
    internal MapGrid dm_engine()
    {
        if (dm_id == 0) return null;
        World world = World.CurrentOrNull;
        return world == null ? null : MapGrid.Of(world);
    }

    /// <summary>The raw move of <see cref="loc"/>; refused moves change nothing and raise nothing.</summary>
    internal void dm_set_loc(atom value)
    {
        MapGrid grid = dm_engine();
        grid?.Sync();
        // BYOND puts a movable on a mapped area's first turf; turfs and areas are tested first (a turf joins its area at every map load)
        if (this is not turf && this is not area && value is area place && place.dm_lowest_turf() is turf first)
            value = first;
        if (ReferenceEquals(value, _loc)) return;
        if (this is area) return;               // BYOND ref /atom/var/loc: "For areas, this value is always null"
        if (this is turf && value == null) return; // BYOND ref /turf: "Turfs cannot be moved"; its loc is always an area
        if (value != null && !value.dm_can_hold(this)) return;
        _loc?.dm_forget(this);
        _loc = value;
        value?.dm_keep(this);
        grid?.HintMoved(this); // a replication hint (presentation-seam.md rule 5): only a candidate, never a value
    }

    // Whether this atom may contain `thing` (move-chain.md E1; BYOND ref /atom/var/loc and /atom/var/contents): an area
    // is contained by nothing, a turf only by an area, and a movable by any atom except itself and the atoms inside it.
    internal bool dm_can_hold(atom thing)
    {
        if (thing is area) return false;
        if (thing is turf) return this is area;
        for (atom container = this; container != null; container = container._loc)
        {
            if (ReferenceEquals(container, thing)) return false;
        }
        return true;
    }

    // `thing` arrives: appended at the end of the contents (arrival order, move-chain.md E1).
    private void dm_keep(atom thing)
    {
        _dm_engine_writing = true;
        _contents.Add(thing);
        _dm_engine_writing = false;
        (_held ??= new HeldSet()).Add(thing);
    }

    // `thing` leaves: the others keep their order.
    private void dm_forget(atom thing)
    {
        // M3 P2: while DM code has not edited the list since the last sync, the list is the held set in the same order, so
        // the atom's only occurrence is at its held position (no scan of an area's tens of thousands of turfs).
        int index = dm_contents_handed_out || _held == null ? -1 : _held.IndexOf(thing);
        _dm_engine_writing = true;
        if (index >= 0 && index < _contents.Count && ReferenceEquals(_contents[index + 1], thing))
            _contents.Cut(index + 1, index + 2);
        else
            _contents.Remove(thing);
        _dm_engine_writing = false;
        _held?.Remove(thing);
    }

    /// <summary>
    /// Applies an edit made through the DM contents list as raw moves (the engine-list workaround described at the top):
    /// an atom the list gained moves in (leaving its old container), an atom it lost goes to nullspace, a second copy of an
    /// atom already inside, a non-atom (reported as a runtime) and a refused atom (a cyclic addition, a turf into a
    /// non-area, an area) are dropped from the list. A turf an area's list lost stays in the area (a turf always has an
    /// area). The list keeps the order the edit gave it.
    /// </summary>
    internal void dm_reconcile_contents()
    {
        DmList list = _contents;
        int count = list.Count;
        HeldSet held = _held;
        int appends = _dm_appends;
        _dm_appends = 0;
        if (appends > 0 && dm_reconcile_appends(appends)) return;
        if (count == (held?.Count ?? 0))
        {
            int same = 0;
            if (held != null)
            {
                foreach (atom kept_thing in held)
                {
                    if (!ReferenceEquals(list[same + 1], kept_thing)) break;
                    same++;
                }
            }
            if (same == count) return;
        }

        var kept = new List<atom>(count);
        var seen = new HashSet<atom>(ReferenceEqualityComparer.Instance);
        List<atom> arrivals = null;
        bool rewrite = false;
        for (int i = 1; i <= count; i++)
        {
            object item = list[i];
            if (item is not atom thing)
            {
                rewrite = true;
                if (item != null)
                    World.Current.ReportRuntime(new DmRuntimeException(Dm.text("bad contents item: [] cannot be inside []", item, DmTypes.PathOf(GetType()))));
                continue;
            }
            if (seen.Contains(thing))
            {
                rewrite = true; // already inside: a second copy changes nothing ("X.loc = its own loc")
                continue;
            }
            if (!ReferenceEquals(thing._loc, this))
            {
                if (!dm_can_hold(thing))
                {
                    rewrite = true;
                    continue;
                }
                (arrivals ??= new List<atom>()).Add(thing);
            }
            seen.Add(thing);
            kept.Add(thing);
        }
        MapGrid grid = dm_engine();
        if (held != null)
        {
            foreach (atom gone in held)
            {
                if (seen.Contains(gone) || !ReferenceEquals(gone._loc, this)) continue;
                if (gone is turf)
                {
                    kept.Add(gone);
                    seen.Add(gone);
                    rewrite = true;
                    continue;
                }
                gone._loc = null;
                grid?.HintMoved(gone);
            }
        }
        if (arrivals != null)
        {
            foreach (atom thing in arrivals)
            {
                thing._loc?.dm_forget(thing);
                thing._loc = this;
                grid?.HintMoved(thing);
            }
        }
        if (rewrite)
        {
            _dm_engine_writing = true;
            list.Cut();
            foreach (atom thing in kept) list.Add(thing);
            _dm_engine_writing = false;
        }
        if (kept.Count == 0)
        {
            _held?.Clear();
        }
        else
        {
            held = new HeldSet();
            foreach (atom thing in kept) held.Add(thing);
            _held = held;
        }
    }

    // The reconcile's fast path (M3 P2), when every DM write since the mark was one insertion: the list is the held atoms in
    // order with `appends` items inserted, and it is exactly the held atoms followed by those items when its last `appends`
    // items are atoms, none held and no two the same (a held atom can only leave the first positions through an insertion
    // before it). Then the result of the general reconcile is: each of them moves in, in list order, and nothing leaves.
    // An appended copy of an atom already inside (tg's `area.contents += turf` of a turf the area holds: the map loader's
    // template tiles of the area they replace, the syndicate base's space on its reserved level) changes nothing and is
    // dropped, as the general path drops a second copy, without rebuilding a list of a whole level's turfs for it.
    // Anything else (a non-atom, a duplicate new item, a refused atom, an insertion among the held atoms) is the general path's.
    private bool dm_reconcile_appends(int appends)
    {
        DmList list = _contents;
        int count = list.Count;
        int heldCount = _held?.Count ?? 0;
        if (count != heldCount + appends) return false;
        HashSet<atom> seen = appends > 8 ? new HashSet<atom>(ReferenceEqualityComparer.Instance) : null;
        List<int> already = null;
        for (int i = heldCount + 1; i <= count; i++)
        {
            if (list[i] is not atom thing)
                return false;
            if (ReferenceEquals(thing._loc, this) && _held != null && _held.Contains(thing))
            {
                // a second copy only while the held atoms still stand first, in order (an insertion among them pushes the last
                // one here instead, and that is the general path's)
                int held_at = _held.IndexOf(thing);
                if (held_at < 0 || held_at >= heldCount || !ReferenceEquals(list[held_at + 1], thing))
                    return false;
                (already ??= new List<int>()).Add(i);
                continue;
            }
            if (ReferenceEquals(thing._loc, this) || (_held != null && _held.Contains(thing)) || !dm_can_hold(thing))
                return false;
            if (seen != null)
            {
                if (!seen.Add(thing)) return false;
                continue;
            }
            for (int j = heldCount + 1; j < i; j++)
            {
                if (ReferenceEquals(list[j], thing)) return false;
            }
        }
        if (already != null)
        {
            _dm_engine_writing = true;
            for (int k = already.Count - 1; k >= 0; k--)
                list.Cut(already[k], already[k] + 1);
            _dm_engine_writing = false;
            count = list.Count;
        }
        MapGrid grid = dm_engine();
        _held ??= new HeldSet();
        for (int i = heldCount + 1; i <= count; i++)
        {
            var thing = (atom)list[i];
            thing._loc?.dm_forget(thing);
            thing._loc = this;
            _held.Add(thing);
            grid?.HintMoved(thing);
        }
        return true;
    }

    // A turf's replacement takes its place in this area's contents (the same position).
    internal void dm_swap_held(atom old_thing, atom new_thing)
    {
        // M3 P2: the held position is the list position while DM code has not edited the list since the last sync.
        int position = dm_contents_handed_out || _held == null ? -1 : _held.IndexOf(old_thing);
        float index = position >= 0 && position < _contents.Count && ReferenceEquals(_contents[position + 1], old_thing)
            ? position + 1
            : _contents.Find(old_thing);
        if (index == 0)
        {
            dm_keep(new_thing);
        }
        else
        {
            _dm_engine_writing = true;
            _contents[index] = new_thing;
            _dm_engine_writing = false;
            _held.Replace(old_thing, new_thing);
        }
        new_thing._loc = this;
        old_thing._loc = null;
        MapGrid grid = dm_engine();
        grid?.HintMoved(new_thing);
        grid?.HintMoved(old_thing);
    }

    // Moves everything this atom holds into `other`, in order, raw (a replaced turf's movables, scope R16).
    internal void dm_move_contents_to(atom other)
    {
        if (_held == null || _held.Count == 0) return;
        HeldSet moving = _held;
        _held = null;
        _dm_engine_writing = true;
        _contents.Cut();
        _dm_engine_writing = false;
        MapGrid grid = dm_engine();
        foreach (atom thing in moving)
        {
            thing._loc = other;
            other.dm_keep(thing);
            grid?.HintMoved(thing);
        }
    }

    // The atoms this atom holds, in order (engine reads; empty when none).
    internal IReadOnlyList<atom> dm_held => (IReadOnlyList<atom>)_held ?? Array.Empty<atom>();

    // The raw container, without the engine's synchronization (the engine itself, after a Sync).
    internal atom dm_loc_raw => _loc;

    private float dm_coord(int axis)
    {
        dm_engine()?.Sync();
        return dm_coord_raw(axis);
    }

    /// <summary>The coordinate on one axis (0 x, 1 y, 2 z) without synchronizing: a movable's is its turf's when it stands on one.</summary>
    private protected virtual float dm_coord_raw(int axis) => _loc is turf on ? on.dm_coord_raw(axis) : 0;

    // Assigning x, y or z moves a movable raw to the turf at the new coordinates of its own z level, when it stands on
    // a turf and that turf exists (BYOND ref /atom/var/x); a turf's and an area's coordinates are fixed.
    private void dm_set_coord(int axis, float value)
    {
        if (this is turf || this is area) return;
        MapGrid grid = dm_engine();
        if (grid == null) return;
        grid.Sync();
        if (_loc is not turf on) return;
        float nx = axis == 0 ? value : on.dm_coord_raw(0);
        float ny = axis == 1 ? value : on.dm_coord_raw(1);
        float nz = axis == 2 ? value : on.dm_coord_raw(2);
        turf destination = grid.TurfAt(nx, ny, nz);
        if (destination != null) dm_set_loc(destination);
    }

    // An assignment to an engine-owned list: null clears it, a list replaces its items, anything else becomes its only
    // item; the list object itself never changes.
    private void dm_replace_engine_list(DmList target, DmList value, AppearanceAspect aspect)
    {
        if (!ReferenceEquals(value, target))
        {
            target.Cut();
            if (value != null) target.Add(value);
        }
        dm_appearance_changed(aspect);
    }

    // presentation-seam.md section 5: a registered atom's appearance var was assigned (a dirty hint, rule 5); the same
    // call is forwarded to the replication hint sink when a host installed one (M1).
    private protected void dm_appearance_changed(AppearanceAspect aspect)
    {
        if (dm_id == 0) return;
        World world = World.CurrentOrNull;
        if (world == null) return;
        PresentationSinks sinks = PresentationSinks.Of(world);
        sinks.Appearance.Changed(this, aspect);
        sinks.Replication?.AppearanceChanged(this, aspect);
    }

    private void dm_visibility_changed()
    {
        if (dm_id == 0) return;
        World world = World.CurrentOrNull;
        if (world == null) return;
        PresentationSinks sinks = PresentationSinks.Of(world);
        sinks.Appearance.VisibilityChanged(this);
        sinks.Replication?.VisibilityChanged(this);
    }

    /// <summary>
    /// BYOND's default <c>name</c> of a type: the last element of its DM path with underscores turned into spaces
    /// (<c>/obj/item/toy_gun</c> gives "toy gun"; BYOND ref <c>/atom/var/name</c>). A C# class without <c>[DmPath]</c> (a
    /// test fixture base) takes its nearest registered ancestor's.
    /// </summary>
    internal static string dm_type_name(Type type)
    {
        for (Type level = type; level != null; level = level.BaseType)
        {
            string path = DmTypes.PathOf(level);
            if (path == null) continue;
            return path.Substring(path.LastIndexOf('/') + 1).Replace('_', ' ');
        }
        return null;
    }
}
