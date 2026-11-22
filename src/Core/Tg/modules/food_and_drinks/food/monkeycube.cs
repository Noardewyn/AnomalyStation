// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/monkeycube.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C5); the var overrides are generated.

// tg: code/modules/food_and_drinks/food/monkeycube.dm:2
public partial class obj_item_food_monkeycube : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/monkeycube.dm:16
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_ITEM_IN_UNWRAPPED_TRAITOR_MAIL, nameof(on_mail_unwrap));
        return dot;
    }

    // tg: code/modules/food_and_drinks/food/monkeycube.dm:20
    public override async DmTask<object> attempt_pickup(mob_living user = null, float? skip_grav = null)
    {
        if (Dm.truthy(expanding))
            return FALSE;
        return await base.attempt_pickup(user, skip_grav);
    }

    // tg: code/modules/food_and_drinks/food/monkeycube.dm:25
    public virtual async DmTask Expand()
    {
        if (Dm.truthy(expanding))
            return;

        expanding = TRUE;

        if (ismob(loc))
        {
            mob holder = loc as mob;
            await holder.dropItemToGround(this);
        }

        mob spammer = get_mob_by_key(fingerprintslast);
        mob_living bananas = null;
        if (ispath(spawned_mob, typeof(mob_living_carbon_human_species_monkey)))
        {
            object cap = config.Get(typeof(datum_config_entry_number_monkeycap));
            if (Dm.num(LAZYLEN(datum_controller_subsystem_mobs.cubemonkeys)) < Dm.num(cap))
                bananas = Dm.@new(spawned_mob, drop_location(), TRUE) as mob_living; // tg's own: TRUE lands in the species slot, not cubespawned
            else if (spammer != null)
                to_chat(spammer, span_warning(I18n.T("Bluespace harmonics prevent the creation of more than [] monkeys on the station at one time!", cap)));
        }
        else
            bananas = Dm.@new(spawned_mob, drop_location()) as mob_living;

        if (!QDELETED(bananas))
        {
            ADD_TRAIT(bananas, TRAIT_SPAWNED_MOB, INNATE_TRAIT);
            bananas.set_faction(get_faction());
            bananas.set_allies(allies == null ? null : (allies as DmList).Copy()); // SET_FACTION_AND_ALLIES_FROM (__DEFINES/mobfactions.dm:119), inline

            visible_message(span_notice(I18n.T("[] expands!", this)));
            bananas.log_message(Dm.text("spawned via [], Last attached mob: [].", this, key_name(spammer)), LOG_ATTACK);

            object alpha_to = bananas.alpha;
            matrix scale_to = Dm.matrix(bananas.transform);
            bananas.alpha = 0;
            bananas.transform = ((matrix)bananas.transform).Scale(0.1f);
            animate(bananas, 0.5f * SECONDS, Dm.named("alpha", alpha_to), Dm.named("transform", scale_to), Dm.named("easing", Dm.bitor(QUAD_EASING, EASE_OUT)));
        }
        else if (spammer == null) // Visible message in case there are no fingerprints
        {
            visible_message(span_notice(I18n.T("[] fails to expand!", this)));
            return;
        }

        animate(this, 0.4f * SECONDS, Dm.named("alpha", 0), Dm.named("transform", ((matrix)transform).Scale(0)), Dm.named("easing", Dm.bitor(QUAD_EASING, EASE_IN)));
        // QDEL_IN(src, 0.5 SECONDS) (rules 11.1 (f))
        _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), 0.5f * SECONDS > GC_FILTER_QUEUE ? (object)WEAKREF(this) : this), 0.5f * SECONDS, file: "code/modules/food_and_drinks/food/monkeycube.dm", line: 64);
    }

    // tg: code/modules/food_and_drinks/food/monkeycube.dm:66
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is putting [] in [] mouth! It looks like [] trying to commit suicide!", user, this, user.p_their(), user.p_theyre())));
        object eating_success = await do_after(user, 1 * SECONDS, this);
        if (QDELETED(user)) //qdeletion: the nuclear option of self-harm
            return SHAME;
        if (!Dm.truthy(eating_success) || QDELETED(this)) //checks if src is gone or if they failed to wait for a second
        {
            user.visible_message(span_suicide(I18n.T("[] chickens out!", user)));
            return SHAME;
        }
        if (HAS_TRAIT(user, TRAIT_NOHUNGER)) //plasmamen don't have saliva/stomach acid
        {
            user.visible_message(span_suicide(I18n.T("[] realizes [] body won't activate []!", user, user.p_their(), this)), span_warning(I18n.T("Your body won't activate []...", this)));
            return SHAME;
        }
        playsound(user, Dm.resource("sound/items/eatfood.ogg"), rand(10, 50), TRUE);
        await user.temporarilyRemoveItemFromInventory(this); //removes from hands, keeps in M
        _addtimer(Dm.@new<datum_callback>(this, nameof(finish_suicide), user), 15, file: "code/modules/food_and_drinks/food/monkeycube.dm", line: 80); //you've eaten it, you can run now
        return MANUAL_SUICIDE;
    }

    // tg: code/modules/food_and_drinks/food/monkeycube.dm:83
    public virtual async DmTask finish_suicide(mob_living user = null) ///internal proc called by a monkeycube's suicide_act using a timer and callback. takes as argument the mob/living who activated the suicide
    {
        if (QDELETED(user) || QDELETED(this))
            return;
        if (loc != user) //how the hell did you manage this
        {
            to_chat(user, span_warning(I18n.T("Something happened to []...", this)));
            return;
        }
        await Expand();
        user.visible_message(span_danger(I18n.T("[]'s torso bursts open as a primate emerges!", user)));
        await user.gib(DROP_BRAIN | DROP_BODYPARTS | DROP_ITEMS); // just remove the organs
    }

    // tg: code/modules/food_and_drinks/food/monkeycube.dm:93
    public virtual object on_mail_unwrap(atom source = null, mob user = null, object letter = null)
    {
        to_chat(user, span_danger(I18n.T("As you open [], its contents rapidly expand!", letter)));
        Waiting.Detach(Expand());
        return COMPONENT_TRAITOR_MAIL_HANDLED;
    }
}

// tg: code/modules/food_and_drinks/food/monkeycube.dm:148
public partial class obj_item_food_monkeycube_random : obj_item_food_monkeycube
{
    // tg: code/modules/food_and_drinks/food/monkeycube.dm:155
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        spawned_mob = pick_weight(Dm.list(
            Dm.kv(typeof(mob_living_basic_bear), 4),
            Dm.kv(typeof(mob_living_basic_bear_snow), 1),
            Dm.kv(typeof(mob_living_basic_blankbody), 2),
            Dm.kv(typeof(mob_living_basic_blob_minion_blobbernaut), 2),
            Dm.kv(typeof(mob_living_basic_blob_minion_spore), 2),
            Dm.kv(typeof(mob_living_basic_carp), 4),
            Dm.kv(typeof(mob_living_basic_carp_mega), 1),
            Dm.kv(typeof(mob_living_basic_creature), 2),
            Dm.kv(typeof(mob_living_basic_eyeball), 1),
            Dm.kv(typeof(mob_living_basic_gorilla), 5),
            Dm.kv(typeof(mob_living_basic_migo), 2),
            Dm.kv(typeof(mob_living_basic_mining_basilisk), 5),
            Dm.kv(typeof(mob_living_basic_mining_lobstrosity), 1),
            Dm.kv(typeof(mob_living_basic_mining_lobstrosity_lava), 4),
            Dm.kv(typeof(mob_living_basic_mining_wolf), 4),
            Dm.kv(typeof(mob_living_basic_pet_cat_feral), 1),
            Dm.kv(typeof(mob_living_basic_spider_giant), 5),
            Dm.kv(typeof(mob_living_basic_spider_giant_hunter), 1),
            Dm.kv(typeof(mob_living_basic_spider_giant_tank), 1),
            Dm.kv(typeof(mob_living_basic_spider_giant_tarantula), 1),
            Dm.kv(typeof(mob_living_basic_spider_giant_viper), 1))) as Type;
        return dot;
    }
}
