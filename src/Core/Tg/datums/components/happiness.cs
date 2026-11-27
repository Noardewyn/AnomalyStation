// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/happiness.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/*
 * A component that allows mobs to have happiness levels
 */
// tg: code/datums/components/happiness.dm:8
public partial class datum_component_happiness : datum_component
{
    // tg: code/datums/components/happiness.dm:1  #define INSPECT_TIMER 10 SECONDS (file-local, #undef at :172)
    private const float INSPECT_TIMER = 10 * SECONDS;
    // tg: code/datums/components/happiness.dm:2  #define PET_COOLDOWN 10 SECONDS (file-local, #undef at :173)
    private const float PET_COOLDOWN = 10 * SECONDS;
    // tg: code/datums/components/happiness.dm:3  #define GROOM_COOLDOWN 30 SECONDS (file-local, #undef at :174)
    private const float GROOM_COOLDOWN = 30 * SECONDS;

    // tg: code/datums/components/happiness.dm:34
    [DmParams("maximum_happiness", "blackboard_key", "on_groom_change", "on_eat_change", "on_petted_change", "callback_percentages", "happiness_callback")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object maximum_happiness = Dm.arg(__extra, 0);
        object blackboard_key = Dm.arg(__extra, 1);
        object on_groom_change = Dm.arg(__extra, 2);
        object on_eat_change = Dm.arg(__extra, 3);
        object on_petted_change = Dm.arg(__extra, 4);
        DmList callback_percentages = Dm.arg<DmList>(__extra, 5);
        datum_callback happiness_callback = Dm.arg<datum_callback>(__extra, 6);
        maximum_happiness ??= 400;
        blackboard_key ??= BB_BASIC_HAPPINESS;
        on_groom_change ??= 200;
        on_eat_change ??= 300;
        on_petted_change ??= 30;
        callback_percentages ??= Dm.list(0, 25, 50, 75, 100);
        if (!isliving(parent))
            return COMPONENT_INCOMPATIBLE;

        this.maximum_happiness = maximum_happiness;
        this.blackboard_key = blackboard_key;
        this.on_groom_change = on_groom_change;
        this.on_petted_change = on_petted_change;
        this.on_eat_change = on_eat_change;
        this.happiness_callback = happiness_callback;
        this.callback_percentages = callback_percentages;

        ADD_TRAIT(parent, TRAIT_SUBTREE_REQUIRED_OPERATIONAL_DATUM, type);
        return null;
    }

    // tg: code/datums/components/happiness.dm:48
    public override async DmTask RegisterWithParent()
    {
        if (Dm.truthy(on_petted_change))
            RegisterSignal(parent, COMSIG_ATOM_ATTACK_HAND, nameof(on_petted));
        if (Dm.truthy(on_groom_change))
            RegisterSignal(parent, COMSIG_COMPONENT_CLEAN_ACT, nameof(on_clean));
        if (Dm.truthy(on_eat_change))
            RegisterSignal(parent, COMSIG_MOB_ATE, nameof(on_eat));
        RegisterSignal(parent, COMSIG_SHIFT_CLICKED_ON, nameof(view_happiness));
    }

    // tg: code/datums/components/happiness.dm:58
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_HOSTILE_PRE_ATTACKINGTARGET, COMSIG_COMPONENT_CLEAN_ACT, COMSIG_MOB_ATE));
        happiness_callback = null;
    }

    // tg: code/datums/components/happiness.dm:62
    [SignalHandler]
    public virtual void on_eat(datum source = null)
    {
        change_happiness_level(on_eat_change);
    }

    // tg: code/datums/components/happiness.dm:67
    [SignalHandler]
    public virtual object on_clean(mob_living source = null)
    {
        if (!(groom_cooldown <= world.time)) // COOLDOWN_FINISHED(src, groom_cooldown)
            return null;

        mob_living living_parent = parent as mob_living;
        if (IS_UNCONSCIOUS_OR_CRIT(living_parent))
            return NONE;

        groom_cooldown = world.time + GROOM_COOLDOWN; // COOLDOWN_START(src, groom_cooldown, GROOM_COOLDOWN)
        change_happiness_level(on_groom_change);
        return Dm.bitor(COMPONENT_CLEANED, COMPONENT_CLEANED_GAIN_XP);
    }

    // tg: code/datums/components/happiness.dm:80
    [SignalHandler]
    public virtual void on_petted(datum source = null, mob_living petter = null, DmList modifiers = null)
    {
        if (!Dm.truthy(LAZYACCESS(modifiers, LEFT_CLICK)) || Dm.truthy(petter.combat_mode))
            return;

        mob_living living_parent = parent as mob_living;
        if (IS_UNCONSCIOUS_OR_CRIT(living_parent))
            return;

        pet_animal();
    }

    // tg: code/datums/components/happiness.dm:91
    public virtual void pet_animal()
    {
        if (!(pet_cooldown <= world.time)) // COOLDOWN_FINISHED(src, pet_cooldown)
            return;
        change_happiness_level(on_petted_change);
        pet_cooldown = world.time + PET_COOLDOWN; // COOLDOWN_START(src, pet_cooldown, PET_COOLDOWN)
    }

    // tg: code/datums/components/happiness.dm:98
    public virtual void change_happiness_level(object amount = null)
    {
        object old_happiness = happiness_level;
        happiness_level = clamp(Dm.num(happiness_level) + Dm.num(amount), 0, Dm.num(maximum_happiness));
        float happiness_percentage = Dm.num(happiness_level) / Dm.num(maximum_happiness);
        float old_percentage = Dm.num(old_happiness) / Dm.num(maximum_happiness);

        object old_threshold = 0;
        object new_threshold = 0;
        foreach (object check_percentage in Dm.each(callback_percentages))
        {
            if (Dm.num(check_percentage) <= old_percentage)
                old_threshold = check_percentage;
            if (Dm.num(check_percentage) <= happiness_percentage)
                new_threshold = check_percentage;
        }

        if (!Dm.eq(old_threshold, new_threshold))
        {
            if (Dm.truthy(parent._listen_lookup?[COMSIG_MOB_HAPPINESS_CHANGE]))
                parent._SendSignal(COMSIG_MOB_HAPPINESS_CHANGE, Dm.list(parent, happiness_percentage));
            if (happiness_callback != null)
                Waiting.NoSleep(happiness_callback.Invoke(happiness_percentage));
        }

        mob_living living_parent = parent as mob_living;
        (living_parent.ai_controller as datum_ai_controller)?.set_blackboard_key(blackboard_key, happiness_percentage);

        if (!HAS_TRAIT(parent, TRAIT_MOB_HIDE_HAPPINESS) && Dm.num(amount) > 0)
        {
            Dm.@new<obj_effect_temp_visual_heart>(living_parent.loc);
            living_parent.spin(spintime: 2 * SECONDS, speed: 1);
        }

        if (Dm.num(happiness_level) > 0)
            START_PROCESSING(SSprocessing, this);
    }

    // tg: code/datums/components/happiness.dm:126
    public virtual void view_happiness(mob_living source = null, mob_living clicker = null)
    {
        if (HAS_TRAIT(source, TRAIT_MOB_HIDE_HAPPINESS) || clicker == null || !(happiness_inspect <= world.time) || !Dm.truthy(Waiting.NoSleep(source.IsReachableBy(clicker))))
            return;
        float y_position = Dm.num(source.get_cached_height()) + 1;
        obj_effect_overlay_happiness_overlay hearts = Dm.@new<obj_effect_overlay_happiness_overlay>();
        hearts.pixel_w -= source.base_pixel_x + source.base_pixel_w;
        hearts.pixel_y = y_position;
        hearts.set_hearts(Dm.num(happiness_level) / Dm.num(maximum_happiness));
        Dm.append(source.vis_contents, hearts);
        happiness_inspect = world.time + INSPECT_TIMER; // COOLDOWN_START(src, happiness_inspect, INSPECT_TIMER)
    }

    // tg: code/datums/components/happiness.dm:137
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        if (Dm.num(happiness_level) <= 0)
            return PROCESS_KILL;
        mob_living living_parent = parent as mob_living;
        float modifier = Dm.truthy((living_parent.ai_controller as datum_ai_controller)?.blackboard[BB_BASIC_DEPRESSED]) ? 2 : 1;
        change_happiness_level(-modifier);
        return null;
    }
}

// tg: code/datums/components/happiness.dm:144
public partial class obj_effect_overlay_happiness_overlay : obj_effect_overlay
{
    // tg: code/datums/components/happiness.dm:154
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // QDEL_IN(src, 5 SECONDS), inline (rules 11.1 (f))
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), 5 * SECONDS > GC_FILTER_QUEUE ? (object)WEAKREF(this) : this), 5 * SECONDS, file: "code/datums/components/happiness.dm", line: 156);
        return dot;
    }

    // tg: code/datums/components/happiness.dm:158
    public virtual void set_hearts(object happiness_percentage = null)
    {
        hearts_percentage = happiness_percentage;
        update_appearance(UPDATE_OVERLAYS);
    }

    // tg: code/datums/components/happiness.dm:162
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        DmList heart_positions = TgState.Current.obj_effect_overlay_happiness_overlay__update_overlays__heart_positions ??= Dm.list(-13, -5, 3, 11);
        float display_amount = round(Dm.length(heart_positions) * Dm.num(hearts_percentage), 1);
        for (float index = 1, index__end = Dm.length(heart_positions); index <= index__end; index++)
        {
            string heart_icon_state = display_amount >= index ? "full_heart" : "empty_heart";
            mutable_appearance display_icon = mutable_appearance(icon: heart_icon, icon_state: heart_icon_state, layer: ABOVE_HUD_PLANE) as mutable_appearance;
            display_icon.pixel_x = Dm.num(heart_positions[index]);
            Dm.append(ref dot, display_icon);
        }
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/datums/components/happiness.dm:164 (var/static/list/heart_positions)
    internal DmList obj_effect_overlay_happiness_overlay__update_overlays__heart_positions;
}
