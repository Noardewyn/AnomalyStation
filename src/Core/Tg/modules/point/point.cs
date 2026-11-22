// Translated from tgstation (snapshot 1d99bd7f): code/modules/point/point.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M8z slice D (D234, D237): the whole file. The arrow is a temp visual the clients draw over everything, sliding to the pointed thing
// (D237); a thing in the pointer's own inventory gets the thought bubble over his head. The pointed verb (GAME_VERB_CONTEXT) is
// translated as examinate is (rules 7.10's exception): its body and the context-target call; the verb panel stays M9.

public partial class atom_movable : atom
{
    // tg: code/modules/point/point.dm:1  #define POINT_TIME (2.5 SECONDS) (file-local, #undef at the end)
    private const float POINT_TIME = 2.5f * SECONDS;

    /**
     * Point at an atom
     *
     * Intended to enable and standardise the pointing animation for all atoms
     *
     * Not intended as a replacement for the mob verb
     */
    // tg: code/modules/point/point.dm:10
    public virtual async DmTask<object> point_at(atom pointed_atom = null, float? intentional = null, Type pointer_type = null)
    {
        intentional ??= FALSE;
        pointer_type ??= typeof(obj_effect_temp_visual_point);
        if (!isturf(loc))
            return FALSE;

        if (Dm.@in(pointed_atom, this))
        {
            create_point_bubble(pointed_atom);
            return FALSE;
        }

        turf tile = get_turf(pointed_atom) as turf;
        if (tile == null)
            return FALSE;

        turf our_tile = get_turf(this) as turf;
        obj visual = Dm.@new(pointer_type, our_tile, invisibility) as obj;

        if (Dm.truthy(_listen_lookup?[COMSIG_MOVABLE_POINTED]))
            _SendSignal(COMSIG_MOVABLE_POINTED, Dm.list(this, pointed_atom, visual, intentional));

        animate(visual, Dm.named("pixel_x", (tile.x - our_tile.x) * ICON_SIZE_X + pointed_atom.pixel_x), Dm.named("pixel_y", (tile.y - our_tile.y) * ICON_SIZE_Y + pointed_atom.pixel_y), Dm.named("time", 1.7f), Dm.named("easing", EASE_OUT));
        return visual;
    }

    // tg: code/modules/point/point.dm:35
    public virtual void create_point_bubble(atom pointed_atom = null)
    {
        mutable_appearance thought_bubble = mutable_appearance(
            Dm.resource("icons/effects/effects.dmi"),
            "thought_bubble",
            offset_spokesman: this,
            plane: POINT_PLANE,
            appearance_flags: KEEP_APART) as mutable_appearance;

        mutable_appearance pointed_atom_appearance = Dm.@new<mutable_appearance>(pointed_atom.appearance);
        pointed_atom_appearance.blend_mode = BLEND_INSET_OVERLAY;
        pointed_atom_appearance.plane = FLOAT_PLANE;
        pointed_atom_appearance.layer = FLOAT_LAYER;
        pointed_atom_appearance.pixel_x = 0;
        pointed_atom_appearance.pixel_y = 0;
        Dm.append(thought_bubble.overlays, pointed_atom_appearance);
        pointed_atom_appearance.remove_filter(HOVER_OUTLINE_FILTER);

        thought_bubble.pixel_w = 16;
        thought_bubble.pixel_z = 32;
        thought_bubble.alpha = 200;

        mutable_appearance point_visual = mutable_appearance(
            Dm.resource("icons/hud/screen_gen.dmi"),
            "arrow") as mutable_appearance;

        Dm.append(thought_bubble.overlays, point_visual);

        add_overlay(thought_bubble);
        LAZYADD(ref update_overlays_on_z, thought_bubble);
        _addtimer(Dm.@new<datum_callback>(this, nameof(clear_point_bubble), thought_bubble), POINT_TIME, file: "code/modules/point/point.dm", line: 64);
    }

    // tg: code/modules/point/point.dm:66
    public virtual void clear_point_bubble(mutable_appearance thought_bubble = null)
    {
        LAZYREMOVE(ref update_overlays_on_z, thought_bubble);
        cut_overlay(thought_bubble);
    }
}

// tg: code/modules/point/point.dm:70  (the var block is generated)
public partial class obj_effect_temp_visual_point : obj_effect_temp_visual
{
    // tg: code/modules/point/point.dm:77
    [DmParams("mapload", "set_invis")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object set_invis = Dm.arg(__extra, 0) ?? 0;
        object dot = base.Initialize(mapload, __extra);
        atom old_loc = loc;
        Waiting.NoSleep(abstract_move(get_turf(this) as atom));
        pixel_x = old_loc.pixel_x;
        pixel_y = old_loc.pixel_y;
        SetInvisibility(set_invis);
        return dot;
    }
}

// tg: code/modules/point/point.dm:85  (the var block is generated)
public partial class obj_effect_temp_visual_point_holo : obj_effect_temp_visual_point
{
    // tg: code/modules/point/point.dm:88
    [DmParams("mapload", "set_invis")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        makeHologram();
        return dot;
    }
}

public partial class mob : atom_movable
{
    // tg: code/modules/point/point.dm:29
    public override async DmTask<object> point_at(atom pointed_atom = null, float? intentional = null, Type pointer_type = null)
    {
        object dot = await base.point_at(pointed_atom, intentional, pointer_type);
        if (Dm.truthy(dot))
        {
            PointGeometry.Mark(this, pointed_atom, dot); // DEVIATION(DV232): the arrow on the point the aim met
            face_atom(pointed_atom);
        }
        return dot;
    }

    /**
     * Point at an atom
     *
     * mob verbs are faster than object verbs. See
     * [this byond forum post](https://secure.byond.com/forum/?post=1326139&page=2#comment8198716)
     * for why this isn't atom/verb/pointed()
     *
     * note: ghosts can point, this is intended
     *
     * visible_message will handle invisibility properly
     *
     * overridden here and in /mob/dead/observer for different point span classes and sanity checks
     */
    // tg: code/modules/point/point.dm:107  GAME_VERB_CONTEXT(/mob, pointed, "Point To", "", null, /atom): the verb's body (the body proc
    // /mob/proc/__gvb_pointed(list/structured_args), __DEFINES/verb.dm:58); the verb itself (pointed) is below.
    public virtual async DmTask<object> __gvb_pointed(DmList structured_args = null)
    {
        // VERB_ARG_TYPED(A, VERB_ARG_TYPE_MOB | VERB_ARG_TYPE_OBJ | VERB_ARG_TYPE_TURF, VERB_ARG_SOURCE_VIEW, /atom) (__DEFINES/verb.dm:183):
        // the static registration of the argument for the verb panel is left out (rules 10.6, 7.10)
        atom A = structured_args?["A"] as atom;

        if (A == null || A is obj_effect_temp_visual_point || isarea(A))
            return FALSE;
        // DEFAULT_QUEUE_OR_CALL_VERB(VERB_CALLBACK(src, PROC_REF(_pointed), A)) (__DEFINES/verb_manager.dm:32, :25-28): the macro builds its
        // callback argument twice, once to queue and once to call.
        if (!Dm.truthy(_queue_verb(Dm.@new<datum_callback_verb_callback>(this, nameof(_pointed), A), VERB_DEFAULT_QUEUE_THRESHOLD, null)))
            Dm.@new<datum_callback_verb_callback>(this, nameof(_pointed), A).InvokeAsync();
        return null;
    }

    // tg: code/modules/point/point.dm:107  the verb /mob/verb/pointed(atom/__context_target), as examinate (mob.cs): the context
    // target's call of the body; the verb typed in the command bar with its argument chosen by the verb manager is M9's.
    public virtual async DmTask<object> pointed(atom __context_target = null, params object[] __extra)
    {
        if (!Dm.truthy(__context_target))
        {
            Dm.NotTranslated("code/modules/point/point.dm:107", "pointed: the verb without a context target (the verb manager, M10+)");
            return null;
        }
        return await __gvb_pointed(Dm.list(Dm.kv("A", __context_target)));
    }

    /// possibly delayed verb that finishes the pointing process starting in [/mob/verb/pointed()].
    /// either called immediately or in the tick after pointed() was called, as per the [DEFAULT_QUEUE_OR_CALL_VERB()] macro
    // tg: code/modules/point/point.dm:117
    public virtual async DmTask<object> _pointed(atom pointing_at = null)
    {
        if (Dm.truthy(client)) //Clientless mobs can just go ahead and point
        {
            atom atom_to_view_verify = pointing_at;
            if (ismovable(pointing_at))
            {
                atom_movable pointed_movable = (atom_movable)pointing_at;
                if (HAS_TRAIT(pointed_movable, TRAIT_SKIP_BASIC_REACH_CHECK) || Dm.truthy(pointing_at.loc.IsContainedAtomAccessible(pointing_at, this)))
                    atom_to_view_verify = pointed_movable.loc;
            }

            // DEVIATION(DV393): or seen on another level of the stack, through open space
            if (!Dm.@in(atom_to_view_verify, view(client.view, this)) && !PointGeometry.SeenThroughLevels(this, atom_to_view_verify, client.view))
                return FALSE;
        }
        if (iscarbon(this)) // special interactions for carbons
        {
            mob_living_carbon our_carbon = (mob_living_carbon)this;
            if (our_carbon.usable_hands <= 0 || Dm.truthy(Dm.bitand(incapacitated, INCAPABLE_RESTRAINTS)) || HAS_TRAIT(this, TRAIT_HANDS_BLOCKED))
            {
                if (!Dm.truthy(TIMER_COOLDOWN_RUNNING(this, "point_verb_emote_cooldown"))) // TIMER_COOLDOWN_FINISHED (__DEFINES/cooldowns.dm)
                {
                    await our_carbon.emote(Dm.text("point []", pointing_at));
                }
                else
                {
                    to_chat(this, span_warning(I18n.T("You need to wait before pointing again!")));
                    return FALSE;
                }
            }
        }
        await point_at(pointing_at, TRUE);

        return TRUE;
    }
}

public partial class mob_living : mob
{
    //same as above
    // tg: code/modules/mob/living/living.dm:545
    public override async DmTask<object> pointed(atom __context_target = null, params object[] __extra)
    {
        if (Dm.truthy(INCAPACITATED_IGNORING(this, INCAPABLE_RESTRAINTS)))
            return FALSE;

        return await base.pointed(__context_target, __extra);
    }

    // tg: code/modules/mob/living/living.dm:551
    public override async DmTask<object> _pointed(atom pointing_at = null)
    {
        if (!Dm.truthy(await base._pointed(pointing_at)))
            return FALSE;
        log_message(Dm.text("points at []", pointing_at), LOG_EMOTE);
        visible_message(span_infoplain(I18n.T("[] points at [].", span_name(Dm.text("[]", this)), pointing_at)), span_notice(I18n.T("You point at [].", pointing_at)));
        return null;
    }
}
