// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/guardian/guardian_types/ranged.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The syndicate campaign X2 (D316): the whole file. The var blocks are generated.

/// A ranged guardian can fling shards of glass at people very very quickly. It can also enter a long-range scouting mode.
// tg: code/modules/mob/living/basic/guardian/guardian_types/ranged.dm:2
public partial class mob_living_basic_guardian_ranged : mob_living_basic_guardian
{
    // tg: code/modules/mob/living/basic/guardian/guardian_types/ranged.dm:19
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(
            typeof(datum_component_ranged_attacks),
            Dm.named("projectile_type", typeof(obj_projectile_guardian)),
            Dm.named("projectile_sound", Dm.resource("sound/effects/hit_on_shattered_glass.ogg")),
            Dm.named("cooldown_time", 0.1f * SECONDS)
        ));
        Waiting.NoSleep(AddComponent(typeof(datum_component_ranged_mob_full_auto), Dm.named("autofire_shot_delay", 0.1f * SECONDS)));
        datum_action_cooldown_mob_cooldown_guardian_alarm_snare snare = Dm.@new<datum_action_cooldown_mob_cooldown_guardian_alarm_snare>(this);
        Waiting.NoSleep(snare.Grant(this));
        return dot;
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/ranged.dm:31
    public override async DmTask toggle_modes()
    {
        if (Dm.truthy(is_deployed()) && summoner != null)
        {
            balloon_alert(this, "must not be manifested!");
            return;
        }
        if (Dm.truthy(has_status_effect(typeof(datum_status_effect_guardian_scout_mode))))
        {
            remove_status_effect(typeof(datum_status_effect_guardian_scout_mode));
            return;
        }
        await apply_status_effect(typeof(datum_status_effect_guardian_scout_mode));
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/ranged.dm:40
    public override void toggle_light()
    {
        string msg;
        float sw__1 = current_sight_mode;
        if (sw__1 == LIGHTING_CUTOFF_VISIBLE)
        {
            lighting_cutoff_red = 10;
            lighting_cutoff_green = 10;
            lighting_cutoff_blue = 15;
            current_sight_mode = LIGHTING_CUTOFF_MEDIUM;
            msg = "You activate your night vision.";
        }
        else if (sw__1 == LIGHTING_CUTOFF_MEDIUM)
        {
            lighting_cutoff_red = 25;
            lighting_cutoff_green = 25;
            lighting_cutoff_blue = 35;
            current_sight_mode = LIGHTING_CUTOFF_HIGH;
            msg = "You increase your night vision.";
        }
        else if (sw__1 == LIGHTING_CUTOFF_HIGH)
        {
            // tg never moves on from the high mode (current_sight_mode stays LIGHTING_CUTOFF_HIGH): kept
            lighting_cutoff_red = 35;
            lighting_cutoff_green = 35;
            lighting_cutoff_blue = 50;
            msg = "You maximize your night vision.";
        }
        else
        {
            lighting_cutoff_red = 0;
            lighting_cutoff_green = 0;
            lighting_cutoff_blue = 0;
            current_sight_mode = LIGHTING_CUTOFF_VISIBLE;
            msg = "You deactivate your night vision.";
        }
        update_sight();
        to_chat(this, span_notice(msg));
    }
}

/// Become an incorporeal scout
// tg: code/modules/mob/living/basic/guardian/guardian_types/ranged.dm:70
public partial class datum_status_effect_guardian_scout_mode : datum_status_effect
{
    // tg: code/modules/mob/living/basic/guardian/guardian_types/ranged.dm:74
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        animate(owner, Dm.named("alpha", 45), Dm.named("time", 0.5f * SECONDS));
        RegisterSignal(owner, COMSIG_GUARDIAN_MANIFESTED, nameof(on_manifest));
        RegisterSignal(owner, COMSIG_GUARDIAN_RECALLED, nameof(on_recall));
        RegisterSignal(owner, COMSIG_MOB_CLICKON, nameof(on_click));
        RegisterSignal(owner, COMSIG_BASICMOB_PRE_ATTACK_RANGED, nameof(on_ranged_attack));

        mob_living_basic_guardian guardian_mob = (mob_living_basic_guardian)owner;
        guardian_mob.unleash();
        to_chat(owner, span_bolddanger("You enter scouting mode."));
        await DmTask.CompletedTask;
        return TRUE;
    }

    // tg: code/modules/mob/living/basic/guardian/guardian_types/ranged.dm:86
    public override async DmTask<object> on_remove()
    {
        animate(owner, Dm.named("alpha", Dm.initial(owner, (atom p) => p.alpha)), Dm.named("time", 0.5f * SECONDS));
        UnregisterSignal(owner, Dm.list(
            COMSIG_BASICMOB_PRE_ATTACK_RANGED,
            COMSIG_GUARDIAN_MANIFESTED,
            COMSIG_GUARDIAN_RECALLED,
            COMSIG_MOB_CLICKON
        ));
        to_chat(owner, span_bolddanger("You return to your normal mode."));
        mob_living_basic_guardian guardian_mob = (mob_living_basic_guardian)owner;
        await guardian_mob.leash_to(owner, guardian_mob.summoner);
        return null;
    }

    /// Restore incorporeal move when we become corporeal, yes I know that suonds silly
    // tg: code/modules/mob/living/basic/guardian/guardian_types/ranged.dm:99
    [SignalHandler]
    public virtual void on_manifest()
    {
        owner.incorporeal_move = INCORPOREAL_MOVE_BASIC;
    }

    /// Stop having incorporeal move when we recall so that we can't move
    // tg: code/modules/mob/living/basic/guardian/guardian_types/ranged.dm:104
    [SignalHandler]
    public virtual void on_recall()
    {
        owner.incorporeal_move = FALSE;
    }

    /// While this is active we can't click anything
    // tg: code/modules/mob/living/basic/guardian/guardian_types/ranged.dm:109
    [SignalHandler]
    public virtual object on_click()
    {
        return COMSIG_MOB_CANCEL_CLICKON;
    }

    /// We can't do any ranged attacks while in scout mode.
    // tg: code/modules/mob/living/basic/guardian/guardian_types/ranged.dm:114
    [SignalHandler]
    public virtual object on_ranged_attack()
    {
        owner.balloon_alert(owner, "need to be in ranged mode!");
        return COMPONENT_CANCEL_RANGED_ATTACK;
    }
}

/// Place an invisible trap which alerts the guardian when it is crossed
// tg: code/modules/mob/living/basic/guardian/guardian_types/ranged.dm:120
public partial class datum_action_cooldown_mob_cooldown_guardian_alarm_snare : datum_action_cooldown_mob_cooldown
{
    // tg: code/modules/mob/living/basic/guardian/guardian_types/ranged.dm:136
    public override async DmTask<object> Activate(atom target = null)
    {
        StartCooldown(360 * SECONDS);

        if (length(placed_snares) >= maximum_snares)
        {
            object picked_snare = await tgui_input_list(owner, "Choose a snare to replace.", "Remove Snare", sort_names(placed_snares) as DmList);
            if (picked_snare == null)
                return FALSE;
            qdel(picked_snare);
        }
        if (length(placed_snares) >= maximum_snares)
        {
            StartCooldown(0);
            return FALSE;
        }

        owner.balloon_alert(owner, "snare deployed"); // We need feedback because they are invisible
        turf snare_loc = get_turf(owner) as turf;
        obj_effect_abstract_surveillance_snare new_snare = Dm.@new<obj_effect_abstract_surveillance_snare>(snare_loc, owner);
        new_snare.assign_owner(owner as mob_living);
        RegisterSignal(new_snare, COMSIG_QDELETING, nameof(on_snare_deleted));
        Dm.append(placed_snares, new_snare);

        StartCooldown();
        return TRUE;
    }

    /// When a snare is deleted remove it from tracking
    // tg: code/modules/mob/living/basic/guardian/guardian_types/ranged.dm:159
    [SignalHandler]
    public virtual void on_snare_deleted(atom snare = null)
    {
        Dm.remove(placed_snares, snare);
    }
}


/// An invisible marker placed by a ranged guardian, alerts the owner when crossed
// tg: code/modules/mob/living/basic/guardian/guardian_types/ranged.dm:165
public partial class obj_effect_abstract_surveillance_snare : obj_effect_abstract
{
    // tg: code/modules/mob/living/basic/guardian/guardian_types/ranged.dm:175  var/static/list/loc_connections
    private static DmList loc_connections => TgState.Current.obj_effect_abstract_surveillance_snare__loc_connections ??= Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(on_entered)));

    // tg: code/modules/mob/living/basic/guardian/guardian_types/ranged.dm:172
    [DmParams("mapload", "spawning_guardian")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload);
        name = Dm.text("[] snare ([])", get_area(this), rand(1, 1000));
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), loc_connections));
        return dot;
    }

    /// Set up crossed notification
    // tg: code/modules/mob/living/basic/guardian/guardian_types/ranged.dm:179
    public virtual void assign_owner(mob_living new_owner = null)
    {
        if (new_owner == null)
        {
            qdel(this);
            return;
        }
        owner = new_owner;
        RegisterSignal(owner, COMSIG_QDELETING, nameof(owner_destroyed));
    }

    /// When crossed notify our owner
    // tg: code/modules/mob/living/basic/guardian/guardian_types/ranged.dm:187
    [SignalHandler]
    public virtual void on_entered(atom source = null, object crossed_object = null)
    {
        if (owner == null)
        {
            qdel(this);
            return;
        }
        if (!isliving(crossed_object) || crossed_object == owner)
            return;
        mob_living_basic_guardian guardian_owner = owner as mob_living_basic_guardian;
        // tg reads guardian_owner.shares_summoner() on any owner (a runtime in tg when the owner is no guardian): guarded
        if (isguardian(owner) && crossed_object == guardian_owner.summoner || (guardian_owner != null && Dm.truthy(guardian_owner.shares_summoner(crossed_object as mob_living_basic_guardian))))
            return;

        string send_message = span_bolddanger(Dm.text("[] has crossed [].", crossed_object, name));
        if (!isguardian(owner) || guardian_owner.summoner == null)
        {
            to_chat(owner, send_message);
            return;
        }

        to_chat(guardian_owner.summoner, send_message);
        DmList guardians = guardian_owner.summoner.get_all_linked_holoparasites();
        foreach (object guardian in Dm.each(guardians))
            to_chat(guardian, send_message);
    }

    /// If the person who placed us doesn't exist we might as well die
    // tg: code/modules/mob/living/basic/guardian/guardian_types/ranged.dm:209
    [SignalHandler]
    public virtual void owner_destroyed()
    {
        owner = null;
        qdel(this);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/basic/guardian/guardian_types/ranged.dm:175  var/static/list/loc_connections
    internal DmList obj_effect_abstract_surveillance_snare__loc_connections;
}
