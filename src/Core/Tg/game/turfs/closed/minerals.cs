// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/closed/minerals.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps G2, D291): /turf/closed/mineral's own procs, so the plain rocks and the volcanic rocks (Meta's 9 tiles by the
// mining outpost's dock) are live: a pickaxe (or any mining tool) digs them out in four seconds to the turf under them (bumping
// into one with a pickaxe does not: DV354), weak rocks and fist miners dig by hand, explosions drill them by their hardness, and
// their ore drops. Lavaland L1 (D304) adds the ore veins and the random rocks (below). Not here (live-partial rows): the cyborgs',
// blobs' and mechs' digging (the aliens' since gaps X5, the hulks' with the MetaStation leftovers) (acid's melting is ported: silicons SI1c); the mining skill's speed and
// experience came with gaps SL1 (D333). The jungle's
// random rock and the biome-replacing rock keep their own procs and stay inert.

/**********************Mineral deposits**************************/

// tg: code/game/turfs/closed/minerals.dm:6
public partial class turf_closed_mineral : turf_closed
{
    // tg: code/game/turfs/closed/minerals.dm:1  #define MINING_MESSAGE_COOLDOWN 20; :2 #define DEFAULT_BORDER_DISTANCE -1

    // tg: code/game/turfs/closed/minerals.dm:53
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        add_large_wall_overlay(Dm.resource("icons/turf/mining.dmi"), wall_icon_state);
        return dot;
    }

    // Inlined version of the bump click element. way faster this way, the element's nice but it's too much overhead
    // tg: code/game/turfs/closed/minerals.dm:58  (set waitfor = FALSE, inherited from /atom/Bumped: the wrapper of waiting-model.md 5.4)
    public override object Bumped(atom_movable bumped_atom = null)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(Bumped__body(bumped_atom, dot), dot);
    }

    private async DmTask<object> Bumped__body(atom_movable bumped_atom, DotBox<object> dot)
    {
        dot.Value = base.Bumped(bumped_atom);
        if (!isliving(bumped_atom))
            return dot.Value;

        mob_living bumping = bumped_atom as mob_living;
        if (!ISADVANCEDTOOLUSER(bumping)) // Unadvanced tool users can't mine anyway (this is a lie). This just prevents message spam from attackby()
            return dot.Value;

        obj_item held_item = bumping.get_active_held_item() as obj_item;
        // !held_item exists to be nice to snow. the other bit is for pickaxes obviously
        if (!Dm.truthy(held_item))
            Waiting.Detach(bumping.ClickOn(this)); // INVOKE_ASYNC
        // DV354: tg's `else if(held_item.tool_behaviour == TOOL_MINING) item_interaction(...)` is dropped: walking into rock with a pick does not dig
        await DmTask.CompletedTask;
        return dot.Value;
    }

    // tg: code/game/turfs/closed/minerals.dm:178
    public virtual void change_ore(object ore_type = null, float? random = null)
    {
        random ??= TRUE;
        if (ispath(ore_type, typeof(obj_item_boulder)))
        {
            scan_state = "rock_boulder"; // Yes even the lowly boulder has a scan state
            spawned_boulder = typeof(obj_item_boulder_gulag);
            return;
        }

        if (Dm.truthy(random))
            mineral_amt = rand(1, 5);

        if (!ispath(ore_type, typeof(obj_item_stack_ore)))
            return;

        scan_state = (string)Dm.initial((System.Type)ore_type, "scan_state"); // If it has a scan_state, switch to it
        mineral_type = (System.Type)ore_type; // Everything else assumes that this is typed correctly so don't set it to non-ores thanks.
    }

    // tg: code/game/turfs/closed/minerals.dm:194
    public virtual void flash_scan()
    {
        obj_effect_temp_visual_mining_overlay scan_overlay = Dm.locate<obj_effect_temp_visual_mining_overlay>(this);
        if (scan_overlay != null)
        {
            deltimer(scan_overlay.timerid);
            scan_overlay.timerid = _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), scan_overlay), scan_overlay.duration, TIMER_STOPPABLE, file: "code/game/turfs/closed/minerals.dm", line: 198);
            animate(scan_overlay, Dm.named("alpha", 0), Dm.named("time", scan_overlay.duration), Dm.named("easing", scan_overlay.easing_style));
            return;
        }
        scan_overlay = Dm.@new<obj_effect_temp_visual_mining_overlay>(this);
        mutable_appearance scan_state_overlay = mutable_appearance(scan_icon, scan_state) as mutable_appearance;
        scan_state_overlay.pixel_x = 224;
        scan_state_overlay.pixel_y = 224;
        scan_overlay.add_overlay(scan_state_overlay);
    }

    // tg: code/game/turfs/closed/minerals.dm:207
    public override object set_smoothed_icon_state(object new_junction = null)
    {
        object old_junction = smoothing_junction;
        object dot = base.set_smoothed_icon_state(new_junction);
        if (!Dm.eq(new_junction, ALL_SMOOTHING_JUNCTIONS) || (old_junction != null && !Dm.eq(old_junction, new_junction)))
            update_appearance(UPDATE_OVERLAYS);
        return dot;
    }

    // tg: code/game/turfs/closed/minerals.dm:213
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (!Dm.eq(smoothing_junction, ALL_SMOOTHING_JUNCTIONS))
        {
            // DEVIATION(DV12): . += fast_emissive_blocker(src), the edge rock's emissive blocker
            Dm.Deferred("code/game/turfs/closed/minerals.dm:216", "update_overlays: fast_emissive_blocker of an edge rock (DEVIATION DV12)");
        }
        return dot;
    }

    // tg: code/game/turfs/closed/minerals.dm:218
    public override object get_smooth_underlay_icon(mutable_appearance underlay_appearance = null, turf asking_turf = null, object adjacency_dir = null)
    {
        if (Dm.truthy(turf_type))
        {
            underlay_appearance.icon = Dm.initial(turf_type, nameof(icon));
            underlay_appearance.icon_state = (string)Dm.initial(turf_type, nameof(icon_state));
            return TRUE;
        }
        return base.get_smooth_underlay_icon(underlay_appearance, asking_turf, adjacency_dir);
    }

    // tg: code/game/turfs/closed/minerals.dm:225
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.eq(tool.tool_behaviour, TOOL_MINING))
            return await base.item_interaction(user, tool, modifiers, __extra);

        return await manual_mine(user, tool);
    }

    ///Mining manually with a hand tool or something masquerading as one
    // tg: code/game/turfs/closed/minerals.dm:232
    public virtual async DmTask<object> manual_mine(mob_living user = null, obj_item tool = null, float? exp_multiplier = null)
    {
        exp_multiplier ??= 1;
        if (!ISADVANCEDTOOLUSER(user))
        {
            to_chat(user, span_warning(I18n.T("You don't have the dexterity to do this!")));
            return ITEM_INTERACT_BLOCKING;
        }

        if (!isturf(user.loc))
            return ITEM_INTERACT_BLOCKING;

        if (Dm.truthy(TIMER_COOLDOWN_RUNNING(this, REF(user)))) //prevents mining turfs in progress
            return ITEM_INTERACT_BLOCKING;

        // TIMER_COOLDOWN_START(src, REF(user), tool_mine_speed) (__DEFINES/cooldowns.dm:77, it expands addtimer: inline, rules 11.1 (f))
        LAZYSET(ref cooldowns, REF(user), _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.end_cooldown), this, REF(user)), tool_mine_speed, file: "code/game/turfs/closed/minerals.dm", line: 245));
        // TURF-IDENTITY: tg's src after the wait is the turf at this cell
        float cell_x = x, cell_y = y, cell_z = z;
        // DEVIATION(DV348): the digger holds the button, the tool strikes the rock over and over and the rock cracks
        float strike_delay = tool_mine_speed * tool.toolspeed / MINING_STRIKES;
        next_mining_strike = Dm.num(world.time) + strike_delay;
        datum_callback strike = Dm.@new<datum_callback>(this, nameof(mining_strike), user, tool, strike_delay);
        // the sphere's excavation digs on the bump, with no hand to hold the button (DV354)
        float hold = tool is obj_item_mod_module_drill { ballin: > 0 } ? NONE : datum_timed_action.DO_AFTER_HOLD;
        if (!Dm.truthy(await tool.use_tool(this, user, tool_mine_speed, volume: 50, extra_checks: strike, timed_action_flags: hold)))
        {
            TIMER_COOLDOWN_END(this, REF(user)); //if we fail we can start again immediately
            (locate(cell_x, cell_y, cell_z) as turf_closed_mineral)?.clear_mining_cracks();
            return ITEM_INTERACT_BLOCKING;
        }

        turf_closed_mineral src = locate(cell_x, cell_y, cell_z) as turf_closed_mineral;
        if (src == null) // ismineralturf (__DEFINES/is_helpers.dm:90)
            return ITEM_INTERACT_BLOCKING;

        await src.gets_drilled(user, exp_multiplier);
        SSblackbox.record_feedback("tally", "pick_used_mining", 1, tool.type);
        return ITEM_INTERACT_SUCCESS;
    }

    // DEVIATION(DV348): strikes a dig takes; the last lands as the rock breaks, so a rock shows one crack fewer
    private const int MINING_STRIKES = 5;

    // Where each crack lands on the face (tg pixels along it and up it). The picture fills x 4-25 and y 6-25 of its 32, so -4 to 6 and -6 to 6 keep it on the face.
    private static (int W, int Z) mining_crack_spot(int crack) => crack switch { 0 => (1, 0), 1 => (-4, 6), 2 => (6, 4), _ => (-3, -6) };

    /// DEVIATION(DV348): the rock's cracks while it is dug; they go when the digger stops
    public DmList mining_cracks;

    // shortcut: one pace per rock, so two diggers on one rock share their strikes; per digger if that ever shows
    public float next_mining_strike;

    /// DEVIATION(DV348): the tool's extra check, each tick of the dig: on the beat the digger swings, the tool sounds and the rock cracks on the digger's side.
    public virtual object mining_strike(mob_living user = null, obj_item tool = null, float strike_delay = 0)
    {
        if (Dm.num(world.time) < next_mining_strike)
            return TRUE;
        next_mining_strike = Dm.num(world.time) + strike_delay;
        Waiting.Detach(user.do_attack_animation(this, used_item: tool));
        tool.play_tool_sound(this, 50);
        int cracks = (int)Dm.num(LAZYLEN(mining_cracks));
        if (cracks >= MINING_STRIKES - 1)
            return TRUE;
        // tg's own crack (torn_wall.dm), drawn as a dent on the face its dir names
        mutable_appearance crack = mutable_appearance(Dm.resource("icons/turf/overlays.dmi"), "explodable", BULLET_HOLE_LAYER) as mutable_appearance;
        (crack.pixel_w, crack.pixel_z) = mining_crack_spot(cracks);
        crack.dir = Dm.num(get_dir(this, user));
        LAZYADD(ref mining_cracks, crack);
        add_overlay(crack);
        return TRUE;
    }

    /// DEVIATION(DV348): the dig stopped: the rock heals, as a dig in tg starts over.
    public virtual void clear_mining_cracks()
    {
        if (mining_cracks == null)
            return;
        cut_overlay(mining_cracks);
        mining_cracks = null;
    }

    // tg: code/game/turfs/closed/minerals.dm:255
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        bool mining_arms = HAS_TRAIT(user, TRAIT_FIST_MINING);
        if (!Dm.truthy(weak_turf) && !mining_arms)
            return await base.attack_hand(user, modifiers, __extra);
        turf user_turf = user.loc as turf;
        if (!isturf(user_turf))
            return null;
        if (Dm.truthy(TIMER_COOLDOWN_RUNNING(this, REF(user)))) //prevents mining turfs in progress
            return null;
        float mining_speed = mining_arms ? tool_mine_speed : hand_mine_speed;
        // TIMER_COOLDOWN_START(src, REF(user), mining_speed) (inline, rules 11.1 (f))
        LAZYSET(ref cooldowns, REF(user), _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.end_cooldown), this, REF(user)), mining_speed, file: "code/game/turfs/closed/minerals.dm", line: 264));
        object skill_modifier = (user.mind?.get_skill_modifier(typeof(datum_skill_mining), SKILL_SPEED_MODIFIER) is var or__1 && Dm.truthy(or__1) ? or__1 : 1);
        balloon_alert(user, I18n.T("pulling out pieces..."));
        // TURF-IDENTITY: tg's src after the wait is the turf at this cell
        float cell_x = x, cell_y = y, cell_z = z;
        if (!Dm.truthy(await do_after(user, Dm.mul(mining_speed, skill_modifier), target: this)))
        {
            TIMER_COOLDOWN_END(this, REF(user)); //if we fail we can start again immediately
            return null;
        }
        turf_closed_mineral src = locate(cell_x, cell_y, cell_z) as turf_closed_mineral;
        if (src != null) // ismineralturf
            await src.gets_drilled(user);
        return null;
    }

    // tg: code/game/turfs/closed/minerals.dm:278
    public virtual async DmTask gets_drilled(mob user = null, float? exp_multiplier = null, params object[] __extra)
    {
        exp_multiplier ??= 0;
        if (istype(user, typeof(mob)))
        {
            if (Dm.truthy(user._listen_lookup?[COMSIG_MOB_MINED]))
                user._SendSignal(COMSIG_MOB_MINED, Dm.list(user, this, exp_multiplier));
        }
        if (Dm.truthy(mineral_type) && (mineral_amt > 0))
        {
            Dm.@new(mineral_type, this, mineral_amt);
            SSblackbox.record_feedback("tally", "ore_mined", mineral_amt, mineral_type);
        }
        if (Dm.truthy(spawned_boulder))
        {
            obj_item_boulder wall_boulder = Dm.@new(spawned_boulder, this) as obj_item_boulder;
            wall_boulder.platform_lifespan = PLATFORM_LIFE_GULAG;
        }
        if (ishuman(user))
        {
            mob_living_carbon_human H = (mob_living_carbon_human)user;
            if (Dm.truthy(exp_multiplier))
            {
                // DEVIATION(DV538): H.mind?. (tg's H.mind. runtimes for a human without a mind, and the rock stays)
                if (Dm.truthy(mineral_type) && (mineral_amt > 0))
                    H.mind?.adjust_experience(typeof(datum_skill_mining), Dm.initial(mineral_type, (obj_item_stack_ore p) => p.mine_experience) * mineral_amt * Dm.num(exp_multiplier));
                else
                    H.mind?.adjust_experience(typeof(datum_skill_mining), 4 * Dm.num(exp_multiplier));
            }
        }

        foreach (obj_effect_temp_visual_mining_overlay M in Dm.each<obj_effect_temp_visual_mining_overlay>(this))
            qdel(M);

        float flags = NONE;
        System.Type old_type = type;
        if (Dm.truthy(defer_change)) // TODO: make the defer change var a var for any changeturf flag
            flags = CHANGETURF_DEFER_CHANGE;
        turf_open mined = await ScrapeAway(null, flags) as turf_open;
        // TURF-IDENTITY: tg's src after ScrapeAway is the turf it left (the callback and the sound are the new turf's)
        _addtimer(Dm.@new<datum_callback>(mined, nameof(turf.AfterChange), flags, old_type), 1, TIMER_UNIQUE, file: "code/game/turfs/closed/minerals.dm", line: 304);
        playsound(mined, Dm.resource("sound/effects/break_stone.ogg"), 50, TRUE); //beautiful destruction
        mined.update_visuals();
    }

    /// When the turf gets drilled from an AOE explosion
    /// Has a chance of not being drilled based on own hardness
    // tg: code/game/turfs/closed/minerals.dm:309
    public virtual async DmTask<object> drill_aoe(mob user = null, float? exp_multiplier = null)
    {
        exp_multiplier ??= 0;
        float speed_change = Dm.num(Dm.initial(typeof(turf_closed_mineral), nameof(tool_mine_speed))) / tool_mine_speed;
        // Probability scaling isn't linear to still mine somewhat reliably in dense rocks
        // Rocks with ores always get broken by AOE
        if (speed_change >= 1 || Dm.truthy(mineral_type) || Dm.truthy(spawned_boulder) || prob(100 * (1 - (1 - sqrt(speed_change)) * 1.5f)))
        {
            await gets_drilled(user, exp_multiplier);
            return null;
        }
        return null;
    }

    // tg: code/game/turfs/closed/minerals.dm:330
    public override async DmTask acid_melt()
    {
        await ScrapeAway();
    }

    // tg: code/game/turfs/closed/minerals.dm:333  (set waitfor = FALSE, inherited from /atom/ex_act: the wrapper of
    // waiting-model.md 5.4; gets_drilled may sleep)
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        DotBox<object> dot = new DotBox<object>();
        return Waiting.WaitforFalse(ex_act__body(severity, target, __extra, dot), dot);
    }

    private async DmTask<object> ex_act__body(object severity, object target, object[] __extra, DotBox<object> dot)
    {
        dot.Value = base.ex_act(severity, target, __extra);
        if (target == this)
        {
            await gets_drilled();
            return TRUE;
        }

        float hardness = Dm.num(Dm.initial(typeof(turf_closed_mineral), nameof(tool_mine_speed))) / tool_mine_speed;
        switch (Dm.num(severity))
        {
            case EXPLODE_DEVASTATE:
                await gets_drilled();
                break;
            case EXPLODE_HEAVY:
                if (prob(90 * hardness))
                    await gets_drilled();
                break;
            case EXPLODE_LIGHT:
                if (prob(75 * hardness))
                    await gets_drilled();
                break;
        }

        return TRUE;
    }
}

// Gaps J6 (D298): gibtonite: a struck deposit counts down and explodes unless a mining scanner defuses it, then it can be dug out.
// tg: code/game/turfs/closed/minerals.dm:1072
public partial class turf_closed_mineral_gibtonite : turf_closed_mineral
{
    // tg: code/game/turfs/closed/minerals.dm:1081
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        det_time = rand(8, 10); //So you don't know exactly when the hot potato will explode
        return base.Initialize(mapload, __extra);
    }

    // tg: code/game/turfs/closed/minerals.dm:1085
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        float previous_stage = stage;
        if (istype(tool, typeof(obj_item_goliath_infuser_hammer)) && Dm.eq(stage, GIBTONITE_ACTIVE))
        {
            user.visible_message(span_notice(I18n.T("[] digs [] to []...", user, tool, this)), span_notice(I18n.T("Your tendril hammer instictively digs and wraps around [] to stop it...", this)));
            defuse(user);
        }
        else if (istype(tool, typeof(obj_item_mining_scanner)) || istype(tool, typeof(obj_item_t_scanner_adv_mining_scanner)) && Dm.eq(stage, GIBTONITE_ACTIVE))
        {
            user.visible_message(span_notice(I18n.T("[] holds [] to []...", user, tool, this)), span_notice(I18n.T("You use [] to locate where to cut off the chain reaction and attempt to stop it...", tool)));
            defuse(user);
        }
        // TURF-IDENTITY: the drill may replace this turf (cell read before)
        object dot = await base.item_interaction(user, tool, modifiers, __extra);
        if (istype(tool, typeof(obj_item_clothing_gloves_gauntlets)) && Dm.eq(previous_stage, GIBTONITE_UNSTRUCK) && Dm.eq(stage, GIBTONITE_ACTIVE) && istype(user, typeof(mob_living)))
        {
            await user.Immobilize(0.5f * SECONDS);
            await user.throw_at(get_ranged_target_turf(this, get_dir(this, user), 5) as atom, range: 5, speed: 3, spin: FALSE);
            user.visible_message(span_danger(I18n.T("[] hit gibtonite with [], launching [] back!", user, tool.name, user.p_them())), span_danger(I18n.T("You've struck gibtonite! Your [] launched you back!", tool.name)));
        }
        return dot;
    }

    // tg: code/game/turfs/closed/minerals.dm:1099
    public virtual void explosive_reaction(mob user = null)
    {
        if (!Dm.eq(stage, GIBTONITE_UNSTRUCK))
            return;

        activated_overlay = mutable_appearance(Dm.resource("icons/turf/smoothrocks_overlays.dmi"), "rock_Gibtonite_inactive", ON_EDGED_TURF_LAYER) as mutable_appearance; //shows in gaps between pulses if there are any
        activated_overlay.pixel_x = 2;
        activated_overlay.pixel_y = 2;
        add_overlay(activated_overlay);
        name = "gibtonite deposit";
        desc = "An active gibtonite reserve. Run!";
        stage = GIBTONITE_ACTIVE;
        visible_message(span_danger(I18n.T("There's gibtonite inside! It's going to explode!")));

        object notify_admins = Dm.truthy(is_mining_level(z)) ? FALSE : TRUE;

        log_bomber(user, "has triggered a gibtonite deposit reaction via", this, null, notify_admins);

        Waiting.Detach(countdown(notify_admins));
    }

    // tg: code/game/turfs/closed/minerals.dm:1121  (set waitfor = FALSE: the callers detach it)
    public virtual async DmTask countdown(object notify_admins = null)
    {
        // TURF-IDENTITY: tg's src is this cell's turf; a drilled deposit stops being gibtonite and the loop ends
        while (!Dm.truthy(gc_destroyed_any()) && Dm.eq(stage, GIBTONITE_ACTIVE) && det_time > 0 && mineral_amt >= 1)
        {
            mutable_appearance boom_overlay = mutable_appearance(Dm.resource("icons/turf/smoothrocks_overlays.dmi"), "rock_Gibtonite_active", ON_EDGED_TURF_LAYER + 0.1f) as mutable_appearance;
            boom_overlay.pixel_x = 2;
            boom_overlay.pixel_y = 2;
            flick_overlay_view(boom_overlay, 0.5f * SECONDS); //makes the animation pulse one time per tick
            det_time--;
            await sleep(0.5f * SECONDS);
        }
        if (!Dm.truthy(gc_destroyed_any()))
        {
            if (Dm.eq(stage, GIBTONITE_ACTIVE) && det_time <= 0 && mineral_amt >= 1)
            {
                turf bombturf = get_turf(this);
                mineral_amt = 0;
                stage = GIBTONITE_DETONATE;
                await explosion(bombturf, devastation_range: 1, heavy_impact_range: 3, light_impact_range: 5, flame_range: 0, flash_range: 0, adminlog: Dm.num(notify_admins), explosion_cause: this);
            }
        }
    }

    // A turf is not deleted when it changes type: the loop tests that the cell still holds this deposit (tg's istype(src, gibtonite)).
    private bool gc_destroyed_any_value => locate(x, y, z) != this;
    private object gc_destroyed_any() => gc_destroyed_any_value ? TRUE : FALSE;

    // tg: code/game/turfs/closed/minerals.dm:1137
    public virtual void defuse(mob_living defuser = null)
    {
        if (!Dm.eq(stage, GIBTONITE_ACTIVE))
            return;
        cut_overlay(activated_overlay);
        activated_overlay.icon_state = "rock_Gibtonite_inactive";
        add_overlay(activated_overlay);
        desc = "An inactive gibtonite reserve. The ore can be extracted.";
        stage = GIBTONITE_STABLE;
        if (det_time < 0)
            det_time = 0;
        visible_message(span_notice(I18n.T("The chain reaction stopped! The gibtonite had [] reactions left till the explosion!", det_time)));
        if (defuser != null && Dm.truthy(defuser._listen_lookup?[COMSIG_LIVING_DEFUSED_GIBTONITE]))
            defuser._SendSignal(COMSIG_LIVING_DEFUSED_GIBTONITE, Dm.list(defuser, det_time));
    }

    // tg: code/game/turfs/closed/minerals.dm:1151
    public override async DmTask gets_drilled(mob user = null, float? exp_multiplier = null, params object[] __extra)
    {
        exp_multiplier ??= 0;
        if (istype(user, typeof(mob)))
        {
            if (Dm.truthy(user._listen_lookup?[COMSIG_MOB_MINED]))
                user._SendSignal(COMSIG_MOB_MINED, Dm.list(user, this, exp_multiplier));
        }

        if (Dm.eq(stage, GIBTONITE_UNSTRUCK) && mineral_amt >= 1) //Gibtonite deposit is activated
        {
            playsound(this, Dm.resource("sound/effects/hit_on_shattered_glass.ogg"), 50, TRUE);
            explosive_reaction(user);
            return;
        }
        if (Dm.eq(stage, GIBTONITE_ACTIVE) && mineral_amt >= 1) //Gibtonite deposit goes kaboom
        {
            turf bombturf = get_turf(this);
            mineral_amt = 0;
            stage = GIBTONITE_DETONATE;
            await explosion(bombturf, devastation_range: 1, heavy_impact_range: 2, light_impact_range: 5, flame_range: 0, flash_range: 0, adminlog: FALSE, explosion_cause: this);
        }
        if (Dm.eq(stage, GIBTONITE_STABLE)) //Gibtonite deposit is now benign and extractable. Depending on how close you were to it blowing up before defusing, you get better quality ore.
        {
            obj_item_gibtonite ore = Dm.@new<obj_item_gibtonite>(this);
            if (det_time <= 0)
            {
                ore.quality = GIBTONITE_QUALITY_HIGH;
                ore.icon_state = "gibtonite_3";
            }
            if (det_time >= 1 && det_time <= 2)
            {
                ore.quality = GIBTONITE_QUALITY_MEDIUM;
                ore.icon_state = "gibtonite_2";
            }
        }

        float flags = NONE;
        System.Type old_type = type;
        if (Dm.truthy(defer_change)) // TODO: make the defer change var a var for any changeturf flag
            flags = CHANGETURF_DEFER_CHANGE;
        turf_open mined = await ScrapeAway(null, flags) as turf_open;
        // TURF-IDENTITY: tg's src after ScrapeAway is the turf it left
        _addtimer(Dm.@new<datum_callback>(mined, nameof(turf.AfterChange), flags, old_type), 1, TIMER_UNIQUE, file: "code/game/turfs/closed/minerals.dm", line: 1178);
        mined.update_visuals();
    }
}

// Lavaland L1 (D304): the ore veins and the random rocks. Lavaland's cave biomes raise random volcanic, siderite and shale rock,
// which SSore_generation fills with ore by its distance to the open caves (calculate_rock_edges, randomize_ore); the other random
// rocks roll their ore when they initialize.
public partial class turf_closed_mineral
{
    // tg: code/game/turfs/closed/minerals.dm:74
    public virtual void spread_vein(object ore_type = null)
    {
        if (!ispath(ore_type, typeof(obj_item_stack_ore)))
        {
            change_ore(ore_type);
            return;
        }

        System.Type ore_path = (System.Type)ore_type;
        if (!Dm.truthy(Dm.initial(ore_path, "vein_type")))
        {
            change_ore(ore_type);
            return;
        }

        // No need to recalculate ore vein size lists every time we want to spawn one
        TgState state = TgState.Current;
        state.turf_closed_mineral_spread_vein__vein_sizes ??= Dm.list();
        DmList vein_sizes = state.turf_closed_mineral_spread_vein__vein_sizes;

        float min_vein_size = Dm.num(Dm.initial(ore_path, "min_vein_size"));
        float max_vein_size = Dm.num(Dm.initial(ore_path, "max_vein_size"));
        if (!Dm.truthy(vein_sizes[ore_path]))
        {
            DmList ore_sizes = Dm.list();
            // Larger veins are rarer by default
            for (float i = min_vein_size; i <= max_vein_size; i++)
                ore_sizes[Dm.text("[]", i)] = max_vein_size - i + 1;
            vein_sizes[ore_path] = ore_sizes;
        }

        float vein_size = Dm.num(Dm.text2num(pick_weight(vein_sizes[ore_path] as DmList)));

        switch ((string)Dm.initial(ore_path, "vein_type"))
        {
            case ORE_VEIN_CLUSTER:
                foreach (turf_closed_mineral rock in Dm.each<turf_closed_mineral>(range(vein_size, this)))
                {
                    if (Dm.truthy(rock.mineral_type) || istype(rock, typeof(turf_closed_mineral_gibtonite)))
                        continue;

                    float spread_prob = 100;
                    // Easiest way to check for different rock types
                    if (!Dm.eq(rock.base_icon_state, base_icon_state))
                        spread_prob = 50;
                    float dx = x - rock.x, dy = y - rock.y;
                    if (rock == this || prob(spread_prob * sqrt(max(0, 1 - sqrt(dx * dx + dy * dy) / vein_size))))
                        rock.change_ore(ore_path);
                }
                break;

            case ORE_VEIN_SCATTER:
            {
                DmList rocks = Dm.list();
                foreach (turf_closed_mineral rock in Dm.each<turf_closed_mineral>(range(vein_size, this)))
                {
                    if (!Dm.eq(rock.base_icon_state, base_icon_state) && prob(50))
                        continue;
                    if (!Dm.truthy(rock.mineral_type) && !istype(rock, typeof(turf_closed_mineral_gibtonite)))
                        rocks.Add(rock);
                }

                for (float i = 1, i__end = rand(min_vein_size * min_vein_size, max_vein_size * max_vein_size); i <= i__end; i++)
                {
                    if (pick_n_take(rocks) is turf_closed_mineral rock)
                    {
                        rock.change_ore(ore_path, FALSE);
                        rock.mineral_amt = 1;
                    }
                }
                break;
            }

            case ORE_VEIN_PLAIN:
            {
                DmList rocks = Dm.list();
                foreach (turf_closed_mineral rock in Dm.each<turf_closed_mineral>(range(vein_size, this)))
                {
                    if (!Dm.eq(rock.base_icon_state, base_icon_state) && prob(50))
                        continue;
                    if (!Dm.truthy(rock.mineral_type) && !istype(rock, typeof(turf_closed_mineral_gibtonite)))
                        rocks.Add(rock);
                }

                if (!Dm.truthy(length(rocks)))
                    return;

                turf first_end = pick(rocks) as turf;
                float first_dist = get_dist(this, first_end);
                float second_dist = vein_size - first_dist;
                turf second_end = locate(x - Dm.round((first_end.x - x) / first_dist * second_dist, 1), y - Dm.round((first_end.y - y) / first_dist * second_dist, 1), z);
                if (!istype(second_end, typeof(turf)))
                    second_end = this;

                rocks.Cut();
                foreach (turf_closed_mineral rock in Dm.each<turf_closed_mineral>(range(1, second_end)))
                {
                    if (!Dm.truthy(rock.mineral_type))
                        rocks.Add(rock);
                }

                if (!Dm.truthy(length(rocks)))
                    return;

                second_end = pick(rocks) as turf;
                foreach (turf_closed_mineral rock in Dm.each<turf_closed_mineral>(Dm.add(get_line(first_end, this), get_line(second_end, this))))
                {
                    rock.change_ore(ore_path);
                    if (rock == this || prob(100 / get_dist(this, rock)))
                    {
                        if (get_step(rock, Dm.num(pick(GLOB.cardinals))) is turf_closed_mineral other_rock && !Dm.truthy(other_rock.mineral_type))
                            other_rock.change_ore(ore_path);
                    }
                }
                break;
            }

            case ORE_VEIN_BRANCH:
                for (float branch = 1, branch__end = rand(3, 5); branch <= branch__end; branch++)
                {
                    DmList rocks = Dm.list();
                    foreach (turf_closed_mineral rock in Dm.each<turf_closed_mineral>(range(vein_size, this)))
                    {
                        if (!Dm.eq(rock.base_icon_state, base_icon_state) && prob(50))
                            continue;
                        if (!Dm.truthy(rock.mineral_type) && !istype(rock, typeof(turf_closed_mineral_gibtonite)))
                            rocks.Add(rock);
                    }

                    if (!Dm.truthy(length(rocks)))
                        continue;

                    foreach (turf_closed_mineral rock in Dm.each<turf_closed_mineral>(get_line(this, pick(rocks) as atom)))
                    {
                        if (!Dm.truthy(rock.mineral_type))
                            rock.change_ore(ore_path);
                    }
                }
                break;
        }
    }
}

public static partial class GlobalProcs
{
    // tg: code/game/turfs/closed/minerals.dm:353
    public static void calculate_rock_edges()
    {
        DmList cardinals = GLOB.cardinals.Copy(); // i'm sorry
        foreach (object mining_z in Dm.each<object>(SSmapping.levels_by_trait(ZTRAIT_MINING)))
        {
            DmList adjacent_minerals = Dm.list();
            foreach (turf_open open_turf in Dm.each<turf_open>(Z_TURFS(Dm.num(mining_z))))
            {
                foreach (object neighbour_dir in Dm.each<object>(cardinals))
                {
                    turf neighbour = get_step(open_turf, Dm.num(neighbour_dir));
                    if (!ismineralturf(neighbour))
                        continue;

                    turf_closed_mineral rock = (turf_closed_mineral)neighbour;
                    if (rock.open_turf_distance != MINERAL_DEFAULT_BORDER_DISTANCE)
                        continue;

                    rock.open_turf_distance = 1;
                    Dm.combine(ref adjacent_minerals, rock);
                    CHECK_TICK();
                }
            }

            float index = 1;
            while (index <= length(adjacent_minerals))
            {
                turf_closed_mineral rock = (turf_closed_mineral)adjacent_minerals[index];
                index += 1;
                foreach (object neighbour_dir in Dm.each<object>(cardinals))
                {
                    turf neighbour = get_step(rock, Dm.num(neighbour_dir));
                    if (!ismineralturf(neighbour))
                        continue;

                    turf_closed_mineral rock_neighbour = (turf_closed_mineral)neighbour;
                    // Don't need to check if we're closer because we're doing BFS fill
                    if (rock_neighbour.open_turf_distance == MINERAL_DEFAULT_BORDER_DISTANCE)
                    {
                        rock_neighbour.open_turf_distance = rock.open_turf_distance + 1;
                        adjacent_minerals.Add(rock_neighbour);
                    }
                    CHECK_TICK();
                }
            }
        }
    }

    private const float MINERAL_DEFAULT_BORDER_DISTANCE = -1; // tg: code/game/turfs/closed/minerals.dm:2  #define DEFAULT_BORDER_DISTANCE -1
}

// tg: code/game/turfs/closed/minerals.dm:390
public partial class turf_closed_mineral_random : turf_closed_mineral
{
    /// Returns a list of the chances for minerals to spawn.
    /// Will only run once, and will then be cached.
    // tg: code/game/turfs/closed/minerals.dm:404
    public virtual DmList mineral_chances()
    {
        return Dm.list(
            Dm.kv(typeof(obj_item_stack_ore_bananium), Dm.truthy(check_holidays(APRIL_FOOLS)) ? 3 : 0),
            Dm.kv(typeof(obj_item_stack_ore_bluespace_crystal), 1),
            Dm.kv(typeof(obj_item_stack_ore_diamond), 2),
            Dm.kv(typeof(obj_item_stack_ore_gold), 8),
            Dm.kv(typeof(obj_item_stack_ore_iron), 18), // Iron and plasma are this low due to how much they spread into veins
            Dm.kv(typeof(obj_item_stack_ore_plasma), 12),
            Dm.kv(typeof(obj_item_stack_ore_silver), 8),
            Dm.kv(typeof(obj_item_stack_ore_titanium), 8),
            Dm.kv(typeof(obj_item_stack_ore_uranium), 5),
            Dm.kv(typeof(turf_closed_mineral_gibtonite), 4));
    }

    // tg: code/game/turfs/closed/minerals.dm:418
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(exposure_based))
        {
            // a toy world runs no SSore_generation, so its exposure-based rock stays bare
            SSore_generation?.ore_turfs.Add(this);
            return dot;
        }

        TgState state = TgState.Current;
        state.turf_closed_mineral_random__mineral_chances_by_type ??= Dm.list();
        DmList mineral_chances_by_type = state.turf_closed_mineral_random__mineral_chances_by_type;
        DmList spawn_chance_list = mineral_chances_by_type[type] as DmList;
        if (spawn_chance_list == null)
        {
            mineral_chances_by_type[type] = expand_weights(mineral_chances());
            spawn_chance_list = mineral_chances_by_type[type] as DmList;
        }

        if (prob(mineral_chance))
            spawn_ore(pick(spawn_chance_list));
        return dot;
    }

    // tg: code/game/turfs/closed/minerals.dm:432
    public virtual void randomize_ore()
    {
        // #ifdef TESTING: the debug build's depth colours and maptext are not ported.
        TgState state = TgState.Current;
        state.turf_closed_mineral_random__mineral_chances_by_depth ??= Dm.list();
        state.turf_closed_mineral_random__spawn_chances_by_depth ??= Dm.list();

        DmList spawn_type_list = state.turf_closed_mineral_random__mineral_chances_by_depth[type] as DmList;
        if (spawn_type_list == null)
        {
            spawn_type_list = Dm.list();
            state.turf_closed_mineral_random__mineral_chances_by_depth[type] = spawn_type_list;
        }

        DmList ore_depth_chances = state.turf_closed_mineral_random__spawn_chances_by_depth[type] as DmList;
        if (ore_depth_chances == null)
        {
            ore_depth_chances = Dm.list();
            state.turf_closed_mineral_random__spawn_chances_by_depth[type] = ore_depth_chances;
        }

        string depth = Dm.text("[]", open_turf_distance);
        DmList spawn_chance_list = spawn_type_list[depth] as DmList;
        float ore_depth_chance = Dm.num(ore_depth_chances[depth]);
        if (spawn_chance_list == null)
        {
            spawn_chance_list = mineral_chances();
            float total_spawn_sum = 0;
            float base_spawn_sum = 0;
            float lowest_factor = 1;
            foreach (object ore_type in Dm.each<object>(spawn_chance_list))
            {
                if (!Dm.truthy(spawn_chance_list[ore_type]))
                    continue;
                base_spawn_sum += Dm.num(spawn_chance_list[ore_type]);
                if (!ispath(ore_type, typeof(obj_item_stack_ore)))
                {
                    total_spawn_sum += Dm.num(spawn_chance_list[ore_type]);
                    continue;
                }

                float depth_factor = 1 / (1 + abs(Dm.num(Dm.initial((System.Type)ore_type, "vein_distance")) - open_turf_distance));
                lowest_factor = clamp(ceil(1 / (Dm.num(spawn_chance_list[ore_type]) * depth_factor)), lowest_factor, ORE_CHANCE_PRECISION);
                spawn_chance_list[ore_type] = Dm.num(spawn_chance_list[ore_type]) * depth_factor;
                total_spawn_sum += Dm.num(spawn_chance_list[ore_type]);
            }

            foreach (object ore_type in Dm.each<object>(spawn_chance_list))
                spawn_chance_list[ore_type] = floor(Dm.num(spawn_chance_list[ore_type]) * lowest_factor);

            ore_depth_chance = mineral_chance * total_spawn_sum / base_spawn_sum;

            // LAZYSET(SSore_generation.ore_spread_probabilities[type], "[open_turf_distance]", ...) (__DEFINES/_lists.dm), expanded inline
            DmList spread = SSore_generation.ore_spread_probabilities[type] as DmList;
            if (spread == null)
                SSore_generation.ore_spread_probabilities[type] = spread = Dm.list();
            DmList info = spawn_chance_list.Copy();
            info["chance"] = ore_depth_chance;
            info["count"] = 0;
            spread[depth] = info;

            spawn_chance_list = expand_weights(spawn_chance_list) as DmList;

            spawn_type_list[depth] = spawn_chance_list;
            ore_depth_chances[depth] = ore_depth_chance;
        }

        DmList counted = ((DmList)SSore_generation.ore_spread_probabilities[type])[depth] as DmList;
        counted["count"] = Dm.num(counted["count"]) + 1;
        if (prob(ore_depth_chance))
            spawn_ore(pick(spawn_chance_list));
    }

    // tg: code/game/turfs/closed/minerals.dm:488
    public virtual void spawn_ore(object ore_path = null)
    {
        if (!Dm.truthy(SSore_generation.ores_generated[ore_path]))
            SSore_generation.ores_generated[ore_path] = 0;
        SSore_generation.ores_generated[ore_path] = Dm.num(SSore_generation.ores_generated[ore_path]) + 1;

        if (!ispath(ore_path, typeof(turf)))
        {
            change_ore(ore_path);
            if (!ispath(ore_path, typeof(obj_item_stack_ore)))
                return;
            if (prob(Dm.num(Dm.initial((System.Type)ore_path, "spread_chance"))))
                spread_vein(ore_path);
            return;
        }

        float stored_flags = 0;
        if (Dm.truthy(Dm.bitand(turf_flags, NO_RUINS)))
            stored_flags = Dm.bitor(stored_flags, NO_RUINS);
        // TURF-IDENTITY: tg's src becomes the new turf
        turf new_turf = Waiting.NoSleep(ChangeTurf((System.Type)ore_path, null, CHANGETURF_IGNORE_AIR)) as turf;
        new_turf.flags_1 = Dm.bitor(Dm.num(new_turf.flags_1), stored_flags);

        if (ismineralturf(new_turf))
        {
            new_turf.levelupdate();
            return;
        }

        turf_closed_mineral new_rock = (turf_closed_mineral)new_turf;
        new_rock.turf_type = turf_type;
        new_rock.levelupdate();

        if (Dm.truthy(new_rock.mineral_type) && !Dm.truthy(new_rock.mineral_amt))
            stack_trace(Dm.text("Mineral turf with mineral_amt being zero initialized at [], [], [] ([])", new_rock.x, new_rock.y, new_rock.z, get_area(new_rock)), "code/game/turfs/closed/minerals.dm", 521);
    }
}

// tg: code/game/turfs/closed/minerals.dm:528
public partial class turf_closed_mineral_random_high_chance : turf_closed_mineral_random
{
    public override DmList mineral_chances()
    {
        return Dm.list(
            Dm.kv(typeof(obj_item_stack_ore_bluespace_crystal), 20),
            Dm.kv(typeof(obj_item_stack_ore_diamond), 30),
            Dm.kv(typeof(obj_item_stack_ore_gold), 45),
            Dm.kv(typeof(obj_item_stack_ore_plasma), 50),
            Dm.kv(typeof(obj_item_stack_ore_silver), 50),
            Dm.kv(typeof(obj_item_stack_ore_titanium), 45),
            Dm.kv(typeof(obj_item_stack_ore_uranium), 35));
    }
}

// tg: code/game/turfs/closed/minerals.dm:547
public partial class turf_closed_mineral_random_high_chance_volcanic : turf_closed_mineral_random_high_chance
{
    public override DmList mineral_chances()
    {
        return Dm.list(
            Dm.kv(typeof(obj_item_stack_ore_bluespace_crystal), 1),
            Dm.kv(typeof(obj_item_stack_ore_diamond), 30),
            Dm.kv(typeof(obj_item_stack_ore_gold), 45),
            Dm.kv(typeof(obj_item_stack_ore_plasma), 50),
            Dm.kv(typeof(obj_item_stack_ore_silver), 50),
            Dm.kv(typeof(obj_item_stack_ore_titanium), 45),
            Dm.kv(typeof(obj_item_stack_ore_uranium), 35));
    }
}

// tg: code/game/turfs/closed/minerals.dm:562
public partial class turf_closed_mineral_random_low_chance : turf_closed_mineral_random
{
    public override DmList mineral_chances()
    {
        return Dm.list(
            Dm.kv(typeof(obj_item_stack_ore_bluespace_crystal), 1),
            Dm.kv(typeof(obj_item_stack_ore_diamond), 1),
            Dm.kv(typeof(obj_item_stack_ore_gold), 4),
            Dm.kv(typeof(obj_item_stack_ore_iron), 30),
            Dm.kv(typeof(obj_item_stack_ore_plasma), 10),
            Dm.kv(typeof(obj_item_stack_ore_silver), 6),
            Dm.kv(typeof(obj_item_stack_ore_titanium), 4),
            Dm.kv(typeof(obj_item_stack_ore_uranium), 2),
            Dm.kv(typeof(turf_closed_mineral_gibtonite), 2));
    }
}

//extremely low chance of rare ores, meant mostly for populating stations with large amounts of asteroid
// tg: code/game/turfs/closed/minerals.dm:580
public partial class turf_closed_mineral_random_stationside : turf_closed_mineral_random
{
    public override DmList mineral_chances()
    {
        return Dm.list(
            Dm.kv(typeof(obj_item_stack_ore_diamond), 1),
            Dm.kv(typeof(obj_item_stack_ore_gold), 3),
            Dm.kv(typeof(obj_item_stack_ore_iron), 30),
            Dm.kv(typeof(obj_item_stack_ore_plasma), 3),
            Dm.kv(typeof(obj_item_stack_ore_silver), 4),
            Dm.kv(typeof(obj_item_stack_ore_titanium), 5),
            Dm.kv(typeof(obj_item_stack_ore_uranium), 1));
    }
}

// tg: code/game/turfs/closed/minerals.dm:600
public partial class turf_closed_mineral_random_volcanic : turf_closed_mineral_random
{
    public override DmList mineral_chances()
    {
        return Dm.list(
            Dm.kv(typeof(obj_item_stack_ore_bluespace_crystal), 1),
            Dm.kv(typeof(obj_item_stack_ore_diamond), 2),
            Dm.kv(typeof(obj_item_stack_ore_gold), 8),
            Dm.kv(typeof(obj_item_stack_ore_iron), 16),
            Dm.kv(typeof(obj_item_stack_ore_plasma), 10),
            Dm.kv(typeof(obj_item_stack_ore_silver), 8),
            Dm.kv(typeof(obj_item_stack_ore_titanium), 11),
            Dm.kv(typeof(obj_item_stack_ore_uranium), 7),
            Dm.kv(typeof(turf_closed_mineral_gibtonite_volcanic), 2));
    }
}

// tg: code/game/turfs/closed/minerals.dm:629
public partial class turf_closed_mineral_random_volcanic_red_rock : turf_closed_mineral_random_volcanic
{
    public override DmList mineral_chances()
    {
        return Dm.list(
            // Cannot spawn these two
            // /obj/item/stack/ore/bluespace_crystal = 0,
            // /obj/item/stack/ore/diamond = 0,
            Dm.kv(typeof(obj_item_stack_ore_gold), 2),
            Dm.kv(typeof(obj_item_stack_ore_iron), 32), // Iron and plasma are this low due to how much they spread into veins
            Dm.kv(typeof(obj_item_stack_ore_plasma), 6),
            Dm.kv(typeof(obj_item_stack_ore_silver), 14),
            Dm.kv(typeof(obj_item_stack_ore_titanium), 25),
            Dm.kv(typeof(obj_item_stack_ore_uranium), 3),
            Dm.kv(typeof(turf_closed_mineral_gibtonite_volcanic_red_rock), 1));
    }
}

// tg: code/game/turfs/closed/minerals.dm:658
public partial class turf_closed_mineral_random_volcanic_shale : turf_closed_mineral_random_volcanic
{
    public override DmList mineral_chances()
    {
        return Dm.list(
            Dm.kv(typeof(obj_item_stack_ore_bluespace_crystal), 1.5f),
            Dm.kv(typeof(obj_item_stack_ore_diamond), 3),
            Dm.kv(typeof(obj_item_stack_ore_gold), 12),
            Dm.kv(typeof(obj_item_stack_ore_iron), 10), // Iron and plasma are this low due to how much they spread into veins
            Dm.kv(typeof(obj_item_stack_ore_plasma), 28),
            Dm.kv(typeof(obj_item_stack_ore_silver), 4),
            Dm.kv(typeof(obj_item_stack_ore_titanium), 8),
            Dm.kv(typeof(obj_item_stack_ore_uranium), 14),
            Dm.kv(typeof(turf_closed_mineral_gibtonite_volcanic_shale), 3));
    }
}

// tg: code/game/turfs/closed/minerals.dm:672
public partial class turf_closed_mineral_random_snow : turf_closed_mineral_random
{
    // tg: code/game/turfs/closed/minerals.dm:688
    public override void change_ore(object ore_type = null, float? random = null)
    {
        base.change_ore(ore_type, random ?? TRUE);
        if (Dm.truthy(mineral_type))
        {
            icon = Dm.resource("icons/turf/walls/icerock_wall.dmi");
            icon_state = "icerock_wall-0";
            base_icon_state = "icerock_wall";
        }
    }

    // tg: code/game/turfs/closed/minerals.dm:695
    public override DmList mineral_chances()
    {
        return Dm.list(
            Dm.kv(typeof(obj_item_stack_ore_bluespace_crystal), 1),
            Dm.kv(typeof(obj_item_stack_ore_diamond), 2),
            Dm.kv(typeof(obj_item_stack_ore_gold), 8),
            Dm.kv(typeof(obj_item_stack_ore_iron), 22),
            Dm.kv(typeof(obj_item_stack_ore_plasma), 12),
            Dm.kv(typeof(obj_item_stack_ore_silver), 8),
            Dm.kv(typeof(obj_item_stack_ore_titanium), 11),
            Dm.kv(typeof(obj_item_stack_ore_uranium), 5),
            Dm.kv(typeof(turf_closed_mineral_gibtonite_ice_icemoon), 2));
    }
}

// tg: code/game/turfs/closed/minerals.dm:713
public partial class turf_closed_mineral_random_snow_underground : turf_closed_mineral_random_snow
{
    // tg: code/game/turfs/closed/minerals.dm:717
    public override DmList mineral_chances()
    {
        return Dm.list(
            Dm.kv(typeof(obj_item_stack_ore_bananium), 1),
            Dm.kv(typeof(obj_item_stack_ore_bluespace_crystal), 2),
            Dm.kv(typeof(obj_item_stack_ore_diamond), 4),
            Dm.kv(typeof(obj_item_stack_ore_gold), 20),
            Dm.kv(typeof(obj_item_stack_ore_iron), 10), // Iron and plasma are this low due to how much they spread into veins,
            Dm.kv(typeof(obj_item_stack_ore_plasma), 14),
            Dm.kv(typeof(obj_item_stack_ore_silver), 24),
            Dm.kv(typeof(obj_item_stack_ore_titanium), 22),
            Dm.kv(typeof(obj_item_stack_ore_uranium), 10),
            Dm.kv(typeof(turf_closed_mineral_gibtonite_ice_icemoon), 2));
    }
}

// tg: code/game/turfs/closed/minerals.dm:731
public partial class turf_closed_mineral_random_snow_high_chance : turf_closed_mineral_random_snow
{
    // tg: code/game/turfs/closed/minerals.dm:734
    public override DmList mineral_chances()
    {
        return Dm.list(
            Dm.kv(typeof(obj_item_stack_ore_bluespace_crystal), 20),
            Dm.kv(typeof(obj_item_stack_ore_diamond), 30),
            Dm.kv(typeof(obj_item_stack_ore_gold), 45),
            Dm.kv(typeof(obj_item_stack_ore_plasma), 50),
            Dm.kv(typeof(obj_item_stack_ore_silver), 50),
            Dm.kv(typeof(obj_item_stack_ore_titanium), 45),
            Dm.kv(typeof(obj_item_stack_ore_uranium), 35));
    }
}

// tg: code/game/turfs/closed/minerals.dm:745
public partial class turf_closed_mineral_random_labormineral : turf_closed_mineral_random
{
    // tg: code/game/turfs/closed/minerals.dm:748
    public override DmList mineral_chances()
    {
        return Dm.list(
            Dm.kv(typeof(obj_item_boulder_gulag), 30),
            Dm.kv(typeof(obj_item_stack_ore_gold), 10),
            Dm.kv(typeof(obj_item_stack_ore_iron), 25),
            Dm.kv(typeof(obj_item_stack_ore_plasma), 20),
            Dm.kv(typeof(obj_item_stack_ore_silver), 20),
            Dm.kv(typeof(turf_closed_mineral_gibtonite_volcanic), 2));
    }
}

// Subtypes for mappers placing ores manually.
// tg: code/game/turfs/closed/minerals.dm:765
public partial class turf_closed_mineral_random_labormineral_ice : turf_closed_mineral_random_labormineral
{
    // tg: code/game/turfs/closed/minerals.dm:778
    public override DmList mineral_chances()
    {
        return Dm.list(
            Dm.kv(typeof(obj_item_boulder_gulag), 30),
            Dm.kv(typeof(obj_item_stack_ore_gold), 10),
            Dm.kv(typeof(obj_item_stack_ore_iron), 25),
            Dm.kv(typeof(obj_item_stack_ore_plasma), 20),
            Dm.kv(typeof(obj_item_stack_ore_silver), 20),
            Dm.kv(typeof(turf_closed_mineral_gibtonite_ice_icemoon), 2));
    }

    // tg: code/game/turfs/closed/minerals.dm:787
    public override void change_ore(object ore_type = null, float? random = null)
    {
        base.change_ore(ore_type, random ?? TRUE);
        if (Dm.truthy(mineral_type))
        {
            icon = Dm.resource("icons/turf/walls/icerock_wall.dmi");
            icon_state = "icerock_wall-0";
            base_icon_state = "icerock_wall";
            smoothing_flags = Dm.bitor(SMOOTH_BITMASK, SMOOTH_BORDER);
        }
    }
}

public sealed partial class TgState
{
    // tg: code/game/turfs/closed/minerals.dm:396-400  /turf/closed/mineral/random's var/static lists
    internal DmList turf_closed_mineral_random__mineral_chances_by_type;
    internal DmList turf_closed_mineral_random__mineral_chances_by_depth;
    internal DmList turf_closed_mineral_random__spawn_chances_by_depth;
    // tg: code/game/turfs/closed/minerals.dm:85  spread_vein's var/static/list/vein_sizes
    internal DmList turf_closed_mineral_spread_vein__vein_sizes;
}

/// Rock type which replaces itself with whichever rock would've been generated by the biome underneath
// tg: code/game/turfs/closed/minerals.dm:947  (the ruins' rock that takes the biome's about it)
public partial class turf_closed_mineral_volcanic_lava_land_surface_biome_replace : turf_closed_mineral_volcanic_lava_land_surface
{
    // tg: code/game/turfs/closed/minerals.dm:954
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // Just spawn a normal lavaland rock if we fail to get a mapgen, such as being spawned over lava
        Type supposed_type = typeof(turf_closed_mineral_volcanic_lava_land_surface);
        var cur_area = loc as area;
        var map_generator = cur_area?.get_generator();
        if (map_generator == null)
        {
            cur_area = GLOB.areas_by_type[default_area] as area;
            map_generator = cur_area?.get_generator();
        }

        if (map_generator is datum_map_generator_cave_generator cave_generator)
        {
            object biome = cave_generator.get_biome_for_turf(this);
            if (Dm.truthy(biome))
            {
                var generating_biome = (datum_biome)SSmapping.biomes[biome];
                supposed_type = generating_biome.closed_turf_type;
            }
        }

        float cur_flags = turf_flags;
        // TURF-IDENTITY: tg's src becomes the new turf
        var new_turf = Waiting.NoSleep(ChangeTurf(supposed_type, flags: Dm.bitor(CHANGETURF_FORCEOP, CHANGETURF_INHERIT_AIR))) as turf;
        if (Dm.truthy(Dm.bitand(cur_flags, NO_RUINS)))
            new_turf.turf_flags = Dm.bitor(new_turf.turf_flags, NO_RUINS);
        return dot;
    }
}

// tg: code/game/turfs/closed/minerals.dm:1232  (the very strong rock: gaps OD3 with the elephant graveyard's wasteland rock; only a master
// miner breaks it, gaps SL1)
public partial class turf_closed_mineral_strong : turf_closed_mineral
{
    // tg: code/game/turfs/closed/minerals.dm:1243
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!ishuman(user))
        {
            to_chat(usr, span_warning(I18n.T("Only a more advanced species could break a rock such as this one!")));
            return ITEM_INTERACT_BLOCKING;
        }

        // DM parse: (!user.mind?.get_skill_level(/datum/skill/mining)) >= SKILL_LEVEL_MASTER: tg's `!` binds before `>=`, so the test is
        // (0 or 1) >= SKILL_LEVEL_MASTER and never true; kept as tg's (D86): gets_drilled turns an apprentice away instead
        if (Dm.not(user.mind?.get_skill_level(typeof(datum_skill_mining))) >= SKILL_LEVEL_MASTER)
        {
            to_chat(usr, span_warning(I18n.T("The rock seems to be too strong to destroy. Maybe I can break it once I become a master miner.")));
            return ITEM_INTERACT_BLOCKING;
        }

        return await base.item_interaction(user, tool, modifiers, __extra);
    }

    // tg: code/game/turfs/closed/minerals.dm:1254
    public override async DmTask gets_drilled(mob user = null, float? exp_multiplier = null, params object[] __extra)
    {
        exp_multiplier ??= 0;
        if (istype(user, typeof(mob)))
        {
            if (Dm.truthy(user._listen_lookup?[COMSIG_MOB_MINED]))
                user._SendSignal(COMSIG_MOB_MINED, Dm.list(user, this, exp_multiplier));
        }

        if (!ishuman(user))
            return; // see attackby
        mob_living_carbon_human H = (mob_living_carbon_human)user;
        if (!Dm.ge(H.mind?.get_skill_level(typeof(datum_skill_mining)), SKILL_LEVEL_MASTER))
            return;
        drop_ores();
        Dm.Deferred("code/game/turfs/closed/minerals.dm:1263", "gets_drilled: H.client.give_award(/datum/award/achievement/skill/legendary_miner, H), the legendary miner achievement (achievements are owner-deferred, D333)");
        float flags = NONE;
        System.Type old_type = type;
        if (Dm.truthy(defer_change)) // TODO: make the defer change var a var for any changeturf flag
            flags = CHANGETURF_DEFER_CHANGE;
        turf_open mined = await ScrapeAway(null, flags) as turf_open;
        // TURF-IDENTITY: tg's src after ScrapeAway is the turf it left (the callback and the sound are the new turf's)
        _addtimer(Dm.@new<datum_callback>(mined, nameof(turf.AfterChange), flags, old_type), 1, TIMER_UNIQUE, file: "code/game/turfs/closed/minerals.dm", line: 1269);
        playsound(mined, Dm.resource("sound/effects/break_stone.ogg"), 50, TRUE); //beautiful destruction
        mined.update_visuals();
        H.mind?.adjust_experience(typeof(datum_skill_mining), 100); //yay!
    }

    // tg: code/game/turfs/closed/minerals.dm:1275
    public virtual void drop_ores()
    {
        if (prob(10))
            Dm.@new<obj_item_stack_sheet_mineral_mythril>(this, 5);
        else
            Dm.@new<obj_item_stack_sheet_mineral_adamantine>(this, 5);
    }

    // tg: code/game/turfs/closed/minerals.dm:1281
    public override async DmTask acid_melt()
    {
        return;
    }

    // tg: code/game/turfs/closed/minerals.dm:1284
    public override object ex_act(object severity = null, object target = null, params object[] __extra)
    {
        return FALSE;
    }
}

// The AI's and cyborgs' clicks (silicons SI2, D326)
// tg: code/game/turfs/closed/minerals.dm:274
public partial class turf_closed_mineral : turf_closed
{
    // tg: code/game/turfs/closed/minerals.dm:274
    public override async DmTask<object> attack_robot(mob user = null, object modifiers = null)
    {
        if (Dm.truthy(await user.Adjacent(this)))
            await attack_hand(user);
        return null;
    }
}

// tg: code/game/turfs/closed/minerals.dm:316  (interleaved types, rules 1.9; gaps X5: an alien digging through rock)
public partial class turf_closed_mineral : turf_closed
{
    // tg: code/game/turfs/closed/minerals.dm:316
    public override async DmTask<object> attack_alien(mob_living_carbon user = null, DmList modifiers = null)
    {
        balloon_alert(user, I18n.T("digging..."));
        playsound(this, Dm.resource("sound/effects/break_stone.ogg"), 50, TRUE);
        if (Dm.truthy(await do_after(user, tool_mine_speed, target: this)))
            await gets_drilled(user);
        return null;
    }
}

// tg: code/game/turfs/closed/minerals.dm:322  (interleaved types, rules 1.9; the MetaStation leftovers: a hulk punching through rock)
public partial class turf_closed_mineral : turf_closed
{
    // tg: code/game/turfs/closed/minerals.dm:322
    public override async DmTask<object> attack_hulk(mob_living_carbon_human H = null, params object[] __extra)
    {
        await base.attack_hulk(H, __extra);
        // tg's do_after sleeps inside the hulk's on_attack_hand, a signal handler: DM hands that caller the null of the sleep (it
        // returns NONE and the punch goes on as a plain one) while the dig carries on. The same here, without sleeping in the handler.
        Waiting.Detach(hulk_dig(H));
        return null;
    }

    // tg: code/game/turfs/closed/minerals.dm:324  (the rest of attack_hulk after its sleep)
    private async DmTask hulk_dig(mob_living_carbon_human H)
    {
        float cell_x = x, cell_y = y, cell_z = z;
        if (Dm.truthy(await do_after(H, tool_mine_speed * 1.25f, target: this)))
        {
            // TURF-IDENTITY: tg's src after the sleep is the turf now at this place (the same as the mining tool's above)
            turf_closed_mineral src = locate(cell_x, cell_y, cell_z) as turf_closed_mineral;
            if (src == null)
                return;
            playsound(src, Dm.resource("sound/effects/meteorimpact.ogg"), 100, TRUE);
            await H.say(pick(";RAAAAAAAARGH!", ";HNNNNNNNNNGGGGGGH!", ";GWAAAAAAAARRRHHH!", "NNNNNNNNGGGGGGGGHH!", ";AAAAAAARRRGH!"), forced: "hulk");
            await src.gets_drilled(H);
        }
    }
}
