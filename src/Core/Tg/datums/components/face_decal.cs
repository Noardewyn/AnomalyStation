// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/face_decal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G4b, D316: the cream pie's splat on a face). The var blocks are generated (D111): bodypart_overlay
// holds the overlay object (types tool, ObjectHoldingVars).

/**
 * Face decal component
 *
 * For when you have some dirt on your face
 */
// tg: code/datums/components/face_decal.dm:8
public partial class datum_component_face_decal : datum_component
{
    // tg: code/datums/components/face_decal.dm:23
    [DmParams("icon_state", "layers", "color")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        this.icon_state = Dm.arg(__extra, 0);
        this.layers = Dm.arg(__extra, 1);
        this.color = Dm.arg(__extra, 2);
        return null;
    }

    // tg: code/datums/components/face_decal.dm:28
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        normal_overlay = null;
        my_head = null;
        QDEL_NULL(ref bodypart_overlay);
        return dot;
    }

    // tg: code/datums/components/face_decal.dm:34
    public override async DmTask RegisterWithParent()
    {
        if (iscarbon(parent))
        {
            mob_living_carbon carbon_parent = (mob_living_carbon)parent;
            my_head = carbon_parent.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart;
            if (my_head == null) //just to be sure
            {
                qdel(this);
                return;
            }
            bodypart_overlay = Dm.@new<datum_bodypart_overlay_simple>();
            bodypart_overlay.overlay_flags = NONE;
            bodypart_overlay.set_layers(layers as DmList);
            bodypart_overlay.offset_location = UPPER_BODY;
            if (Dm.truthy(Dm.bitand(carbon_parent.bodyshape, BODYSHAPE_SNOUTED))) //stupid, but external organ bodytypes are not stored on the limb
                bodypart_overlay.icon_state = Dm.text("[]_lizard", icon_state);
            else if (Dm.truthy(Dm.bitand(my_head.bodyshape, BODYSHAPE_MONKEY)))
                bodypart_overlay.icon_state = Dm.text("[]_monkey", icon_state);
            else
                bodypart_overlay.icon_state = Dm.text("[]_human", icon_state);
            if (color != null)
                bodypart_overlay.draw_color = color;
            await my_head.add_bodypart_overlay(bodypart_overlay);
            RegisterSignals(my_head, Dm.list(COMSIG_BODYPART_REMOVED, COMSIG_QDELETING), nameof(lost_head));
        }
        else
        {
            normal_overlay = get_normal_overlay() as mutable_appearance;
            normal_overlay.color = color;
        }

        RegisterSignals(parent, Dm.list(
            COMSIG_COMPONENT_CLEAN_ACT,
            COMSIG_COMPONENT_CLEAN_FACE_ACT),
            nameof(clean_up));

        if (normal_overlay != null)
        {
            if (color != null)
                normal_overlay.color = color;
            atom atom_parent = (atom)parent;
            RegisterSignal(atom_parent, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(update_overlays));
            atom_parent.update_appearance();
        }
    }

    // tg: code/datums/components/face_decal.dm:73
    public virtual object get_normal_overlay()
    {
        return null;
    }

    // tg: code/datums/components/face_decal.dm:76
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(
            COMSIG_COMPONENT_CLEAN_ACT,
            COMSIG_COMPONENT_CLEAN_FACE_ACT));
        if (my_head != null)
        {
            if (bodypart_overlay != null)
            {
                await my_head.remove_bodypart_overlay(bodypart_overlay);
                QDEL_NULL(ref bodypart_overlay);
            }
            UnregisterSignal(my_head, Dm.list(COMSIG_BODYPART_REMOVED, COMSIG_QDELETING));
            my_head = null;
        }
        if (normal_overlay != null)
        {
            atom atom_parent = (atom)parent;
            UnregisterSignal(atom_parent, COMSIG_ATOM_UPDATE_OVERLAYS);
            atom_parent.update_appearance();
            normal_overlay = null;
        }
    }

    ///Callback to remove our decal
    // tg: code/datums/components/face_decal.dm:93
    [SignalHandler]
    public virtual object clean_up(datum source = null, object clean_types = null)
    {
        if (!Dm.truthy(Dm.bitand(clean_types, CLEAN_TYPE_BLOOD)))
            return NONE;

        qdel(this);
        return Dm.bitor(COMPONENT_CLEANED, COMPONENT_CLEANED_GAIN_XP);
    }

    /// Ensures normal_overlay overlay in case the mob is not a carbon
    // tg: code/datums/components/face_decal.dm:103
    [SignalHandler]
    public virtual void update_overlays(atom parent_atom = null, DmList overlays = null)
    {
        if (normal_overlay != null)
            Dm.append(ref overlays, normal_overlay);
    }

    /// Removes the decal when the head gets dismembered
    // tg: code/datums/components/face_decal.dm:110
    [SignalHandler]
    public virtual void lost_head(obj_item_bodypart source = null, mob_living_carbon owner = null, object dismembered = null)
    {
        qdel(this);
    }
}

public partial class datum_controller_global_vars
{
    // tg: code/datums/components/face_decal.dm:116  GLOBAL_LIST_INIT(splattable, zebra_typecacheof(list(...)))
    public DmList splattable;

    // tg: code/datums/components/face_decal.dm:116  (GLOBAL_MANAGED(splattable, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalsplattable()
    {
        splattable = zebra_typecacheof(Dm.list(
            Dm.kv(typeof(mob_living_carbon_human), "human"),
            Dm.kv(typeof(mob_living_basic_pet_dog_corgi), "corgi"),
            Dm.kv(typeof(mob_living_silicon_ai), "ai"))) as DmList;
        Dm.append(ref gvars_datum_init_order, "splattable");
    }
}

/// splat subtype, handling signals and mood logic
// tg: code/datums/components/face_decal.dm:122
public partial class datum_component_face_decal_splat : datum_component_face_decal
{
    // tg: code/datums/components/face_decal.dm:126
    [DmParams("icon_state", "layers", "color", "memory_type", "mood_event_type")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object memory_type = Dm.arg(__extra, 3) ?? typeof(datum_memory_witnessed_creampie);
        object mood_event_type = Dm.arg(__extra, 4) ?? typeof(datum_mood_event_creampie);
        if (!Dm.truthy(is_type_in_typecache(parent, GLOB.splattable)))
            return COMPONENT_INCOMPATIBLE;

        object dot = await base.Initialize(__extra);

        if (Dm.truthy(parent._listen_lookup?[COMSIG_MOB_HIT_BY_SPLAT]))
            parent._SendSignal(COMSIG_MOB_HIT_BY_SPLAT, Dm.list(parent, this));
        add_memory_in_range((atom)parent, 7, memory_type, Dm.named("protagonist", parent));
        this.mood_event_type = mood_event_type as System.Type;
        return dot;
    }

    // tg: code/datums/components/face_decal.dm:136
    public override object get_normal_overlay()
    {
        return mutable_appearance(Dm.resource("icons/mob/effects/face_decal.dmi"), Dm.text("[]_[]", icon_state, is_type_in_list(parent, GLOB.splattable, zebra: TRUE)));
    }

    // tg: code/datums/components/face_decal.dm:139
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        if (iscarbon(parent))
        {
            mob_living_carbon carbon_parent = (mob_living_carbon)parent;
            await carbon_parent.add_mood_event("splat", mood_event_type);
        }
    }

    // tg: code/datums/components/face_decal.dm:145
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        await base.UnregisterFromParent(__extra);
        if (iscarbon(parent))
        {
            mob_living_carbon carbon_parent = (mob_living_carbon)parent;
            carbon_parent.clear_mood_event("splat");
        }
    }
}
