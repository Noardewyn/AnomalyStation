// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/thermite.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps RG3 (D339): the whole file (thermite on a wall: its overlay and examine line, lit by a hot item or a fire of 1922 K, the
// fake fire and the welder's sound, the melt after up to 20 seconds, the seared hand, washed off by cleaning). The var block is
// generated (D111); the type-level statics are TgState fields (rules 10.6).

// tg: code/datums/components/thermite.dm:1
public partial class datum_component_thermite : datum_component
{
    /// Default thermite overlay, do not touch
    // tg: code/datums/components/thermite.dm:10  var/static/mutable_appearance/default_thermite_overlay (rules 10.6)
    public static mutable_appearance default_thermite_overlay => TgState.Current.datum_component_thermite__default_thermite_overlay ??= (mutable_appearance)mutable_appearance(Dm.resource("icons/effects/effects.dmi"), "thermite");

    /// Blacklist of turfs that cannot have thermite on it
    // tg: code/datums/components/thermite.dm:19  var/static/list/blacklist
    public static DmList blacklist => TgState.Current.datum_component_thermite__blacklist ??= typecacheof(Dm.list(
        typeof(turf_open_lava),
        typeof(turf_open_space),
        typeof(turf_open_water),
        typeof(turf_open_chasm)
    )) as DmList;

    /// List of turfs that are immune to thermite
    // tg: code/datums/components/thermite.dm:26  var/static/list/immunelist
    public static DmList immunelist => TgState.Current.datum_component_thermite__immunelist ??= typecacheof(Dm.list(
        typeof(turf_closed_wall_mineral_diamond),
        typeof(turf_closed_indestructible),
        typeof(turf_open_indestructible)
    )) as DmList;

    /// List of turfs that take extra thermite to burn through
    // tg: code/datums/components/thermite.dm:32  var/static/list/resistlist
    public static DmList resistlist => TgState.Current.datum_component_thermite__resistlist ??= typecacheof(Dm.list(
        typeof(turf_closed_wall_r_wall)
    )) as DmList;

    // tg: code/datums/components/thermite.dm:36
    [DmParams("amount", "thermite_overlay")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object amount = Dm.arg(__extra, 0) ?? 50;
        object thermite_overlay = Dm.arg(__extra, 1) ?? default_thermite_overlay;
        if (!isturf(parent))
            return COMPONENT_INCOMPATIBLE;
        //not actually incompatible, but not valid
        if (Dm.truthy(blacklist[parent.type]))
        {
            qdel(this);
            return null;
        }

        if (Dm.truthy(immunelist[parent.type]))
            this.amount = 0; //Yeah the overlay can still go on it and be cleaned but you arent burning down a diamond wall
        else
        {
            this.amount = amount;
            if (Dm.truthy(resistlist[parent.type]))
                burn_require = 50;
            else
                burn_require = 30;
        }

        this.thermite_overlay = thermite_overlay;
        return null;
    }

    // tg: code/datums/components/thermite.dm:55
    public override object Destroy(float? force = null)
    {
        thermite_overlay = null;
        if (Dm.truthy(burn_timer))
        {
            deltimer(burn_timer);
            burn_timer = null;
        }
        if (burn_callback != null)
            burn_callback = null;
        if (fakefire != null)
            QDEL_NULL(ref fakefire);
        return base.Destroy(force);
    }

    // tg: code/datums/components/thermite.dm:66
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_ATOM_ATTACKBY, nameof(attackby_react));
        RegisterSignal(parent, COMSIG_ATOM_ATTACK_HAND, nameof(on_attack_hand));
        RegisterSignal(parent, COMSIG_ATOM_EXAMINE, nameof(on_examine));
        RegisterSignal(parent, COMSIG_ATOM_FIRE_ACT, nameof(on_fire_act));
        RegisterSignal(parent, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(on_update_overlays));
        RegisterSignal(parent, COMSIG_COMPONENT_CLEAN_ACT, nameof(clean_react));
        RegisterSignal(parent, COMSIG_QDELETING, nameof(parent_qdeleting)); //probably necessary because turfs are wack
        turf turf_parent = (turf)parent;
        turf_parent.update_appearance();
    }

    // tg: code/datums/components/thermite.dm:77
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(
            COMSIG_ATOM_ATTACKBY,
            COMSIG_ATOM_ATTACK_HAND,
            COMSIG_ATOM_EXAMINE,
            COMSIG_ATOM_FIRE_ACT,
            COMSIG_ATOM_UPDATE_OVERLAYS,
            COMSIG_COMPONENT_CLEAN_ACT,
            COMSIG_QDELETING
        ));
        turf turf_parent = (turf)parent;
        turf_parent.update_appearance();
    }

    // tg: code/datums/components/thermite.dm:90
    [DmParams("new_comp", "i_am_original", "amount")]
    public override async DmTask InheritComponent(datum_component C = null, object i_am_original = null, params object[] __extra)
    {
        object amount = Dm.arg(__extra, 0);
        if (!Dm.truthy(i_am_original))
            return;
        this.amount = Dm.add(this.amount, amount);
        if (Dm.truthy(burn_timer)) // prevent people from skipping a longer timer
        {
            deltimer(burn_timer);
            burn_timer = _addtimer(burn_callback, min(Dm.num(amount) * 0.35f * SECONDS, 20 * SECONDS), TIMER_STOPPABLE, file: "code/datums/components/thermite.dm", line: 96);
        }
    }

    /// Alerts the user that this turf is, in fact, covered with thermite.
    // tg: code/datums/components/thermite.dm:99
    [SignalHandler]
    public virtual void on_examine(turf source = null, mob user = null, DmList examine_list = null)
    {
        Dm.append(ref examine_list, span_warning(I18n.T("[] covered in thermite.", source.p_Theyre())));
    }

    /// Used to maintain the thermite overlay on the parent [/turf].
    // tg: code/datums/components/thermite.dm:105
    [SignalHandler]
    public virtual void on_update_overlays(turf parent_turf = null, DmList overlays = null)
    {
        if (Dm.truthy(thermite_overlay))
            Dm.append(ref overlays, thermite_overlay);
    }

    /**
     * Used to begin the thermite burning process
     *
     * Arguments:
     * * mob/user - The user igniting the thermite
     */
    // tg: code/datums/components/thermite.dm:117
    public virtual void thermite_melt(mob user = null)
    {
        turf parent_turf = (turf)parent;
        playsound(parent_turf, Dm.resource("sound/items/tools/welder.ogg"), 100, TRUE);
        fakefire = Dm.@new<obj_effect_overlay_thermite>(parent_turf);
        burn_callback = Dm.@new<datum_callback>(this, nameof(burn_parent), user);
        burn_timer = _addtimer(burn_callback, min(Dm.num(amount) * 0.35f * SECONDS, 20 * SECONDS), TIMER_STOPPABLE, file: "code/datums/components/thermite.dm", line: 122);
        //unregister everything related to burning
        UnregisterSignal(parent, Dm.list(COMSIG_COMPONENT_CLEAN_ACT, COMSIG_ATOM_ATTACKBY, COMSIG_ATOM_FIRE_ACT));
    }

    /**
     * Used to actually melt parent
     *
     * Arguments:
     * * mob/user - The user that ignited the thermite
     */
    // tg: code/datums/components/thermite.dm:132
    public virtual async DmTask burn_parent(mob user = null)
    {
        turf parent_turf = (turf)parent;
        if (fakefire != null)
            QDEL_NULL(ref fakefire);
        if (user != null)
            parent_turf.add_hiddenprint(user);
        if (Dm.num(amount) >= Dm.num(burn_require))
        {
            parent_turf = await parent_turf.Melt() as turf;
            parent_turf.burn_tile();
        }
        burn_timer = null;
        qdel(this);
    }

    /**
     * Wash reaction, used to clean off thermite from parent
     */
    // tg: code/datums/components/thermite.dm:147
    [SignalHandler]
    public virtual object clean_react(datum source = null, object strength = null)
    {
        object dot = NONE;

        //Thermite is just some loose powder, you could probably clean it with your hands
        qdel(this);

        return COMPONENT_CLEANED;
    }

    /**
     * fire_act reaction, has to be the correct temperature
     *
     * Arguments:
     * * datum/source - The source of the flame
     * * exposed_temperature - The temperature of the flame hitting the thermite
     * * exposed_volume - The volume of the flame
     */
    // tg: code/datums/components/thermite.dm:165
    [SignalHandler]
    public virtual void on_fire_act(datum source = null, object exposed_temperature = null, object exposed_volume = null)
    {
        // This is roughly the real life requirement to ignite thermite
        // (honestly not really sure what the point of this is, considering a god damn lighter can ignite this)
        if (Dm.num(exposed_temperature) >= 1922)
            thermite_melt();
    }

    /// Handles searing the hand of anyone who tries to touch parent without protection, while burning
    // tg: code/datums/components/thermite.dm:174
    [SignalHandler]
    public virtual object on_attack_hand(atom source = null, mob_living_carbon user = null, params object[] __extra)
    {
        //not burning
        if (fakefire == null)
            return NONE;

        if (!iscarbon(user) || Dm.truthy(user.can_touch_burning(source)))
            return NONE;

        Waiting.NoSleep(user.apply_damage(5, BURN, user.get_active_hand()));
        to_chat(user, span_userdanger(I18n.T("The ignited thermite on [] burns your hand!", source)));
        Waiting.Detach(user.emote("scream")); // INVOKE_ASYNC(user, TYPE_PROC_REF(/mob, emote), "scream")
        playsound(source, SFX_SEAR, 50, TRUE);
        return COMPONENT_CANCEL_ATTACK_CHAIN;
    }

    /**
     * attackby reaction, ignites the thermite if its a flame creating object
     *
     * Arguments:
     * * datum/source - The source of the attack
     * * obj/item/thing - Item being attacked by
     * * mob/user - The user behind the attack
     * * params - params
     */
    // tg: code/datums/components/thermite.dm:199
    [SignalHandler]
    public virtual void attackby_react(datum source = null, obj_item thing = null, mob user = null, object @params = null)
    {
        if (Dm.num(thing.get_temperature()) >= FIRE_MINIMUM_TEMPERATURE_TO_EXIST)
            thermite_melt(user);
    }

    /// Signal handler for COMSIG_QDELETING, necessary because turfs can be weird with qdel()
    // tg: code/datums/components/thermite.dm:206
    [SignalHandler]
    public virtual void parent_qdeleting(datum source = null, params object[] __extra)
    {
        if (!QDELING(this))
            qdel(this);
    }
}

public sealed partial class TgState
{
    // tg: code/datums/components/thermite.dm:10  /datum/component/thermite's var/static default_thermite_overlay
    internal mutable_appearance datum_component_thermite__default_thermite_overlay;
    // tg: code/datums/components/thermite.dm:19  /datum/component/thermite's var/static/list/blacklist
    internal DmList datum_component_thermite__blacklist;
    // tg: code/datums/components/thermite.dm:26  /datum/component/thermite's var/static/list/immunelist
    internal DmList datum_component_thermite__immunelist;
    // tg: code/datums/components/thermite.dm:32  /datum/component/thermite's var/static/list/resistlist
    internal DmList datum_component_thermite__resistlist;
}
