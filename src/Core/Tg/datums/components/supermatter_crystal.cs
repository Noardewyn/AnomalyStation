// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/supermatter_crystal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The M7 supermatter slice (G5, D163): the whole file, the crystal's touch: a hand, a paw, an item, a bump or a fall dusts or
// consumes, and what it consumed feeds the crystal's power and damage. The logs, admin messages and the witnesses' memories
// are deferred (M10), and so is Poly's power surge event (the random events, M10). The var block is generated (D111).

// tg: code/datums/components/supermatter_crystal.dm:1
public partial class datum_component_supermatter_crystal : datum_component
{
    // A whitelist of items that can interact with the SM without dusting the user
    // tg: code/datums/components/supermatter_crystal.dm:8  var/static/list/sm_item_whitelist (a type-level static, rules 10.6)
    public static ref DmList sm_item_whitelist => ref TgState.Current.datum_component_supermatter_crystal__sm_item_whitelist;

    // tg: code/datums/components/supermatter_crystal.dm:13
    [DmParams("tool_act_callback", "consume_callback")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        datum_callback tool_act_callback = Dm.arg(__extra, 0) as datum_callback;
        datum_callback consume_callback = Dm.arg(__extra, 1) as datum_callback;

        RegisterSignal(parent, COMSIG_ATOM_BLOB_ACT, nameof(blob_hit));
        RegisterSignal(parent, COMSIG_ATOM_ATTACK_PAW, nameof(paw_hit));
        RegisterSignal(parent, COMSIG_ATOM_ATTACK_ANIMAL, nameof(animal_hit));
        RegisterSignal(parent, COMSIG_ATOM_HULK_ATTACK, nameof(hulk_hit));
        RegisterSignal(parent, COMSIG_LIVING_UNARMED_ATTACK, nameof(unarmed_hit));
        RegisterSignal(parent, COMSIG_ATOM_ATTACK_HAND, nameof(hand_hit));
        RegisterSignal(parent, COMSIG_ATOM_ATTACKBY, nameof(attackby_hit));
        RegisterSignal(parent, COMSIG_ATOM_TOOL_ACT(TOOL_WRENCH), nameof(tool_hit));
        RegisterSignal(parent, COMSIG_ATOM_SECONDARY_TOOL_ACT(TOOL_WRENCH), nameof(tool_hit));
        RegisterSignal(parent, COMSIG_ATOM_BUMPED, nameof(bumped_hit));
        RegisterSignal(parent, COMSIG_ATOM_INTERCEPT_Z_FALL, nameof(intercept_z_fall));
        RegisterSignal(parent, COMSIG_ATOM_ON_Z_IMPACT, nameof(on_z_impact));

        this.tool_act_callback = tool_act_callback;
        this.consume_callback = consume_callback;
        return null;
    }

    // tg: code/datums/components/supermatter_crystal.dm:31
    public override object Destroy(float? force = null)
    {
        tool_act_callback = null;
        consume_callback = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/supermatter_crystal.dm:36
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        DmList signals_to_remove = Dm.list(
            COMSIG_ATOM_BLOB_ACT,
            COMSIG_ATOM_ATTACK_PAW,
            COMSIG_ATOM_ATTACK_ANIMAL,
            COMSIG_ATOM_HULK_ATTACK,
            COMSIG_LIVING_UNARMED_ATTACK,
            COMSIG_ATOM_ATTACK_HAND,
            COMSIG_ATOM_ATTACKBY,
            COMSIG_ATOM_TOOL_ACT(TOOL_WRENCH),
            COMSIG_ATOM_SECONDARY_TOOL_ACT(TOOL_WRENCH),
            COMSIG_ATOM_BUMPED,
            COMSIG_ATOM_INTERCEPT_Z_FALL,
            COMSIG_ATOM_ON_Z_IMPACT
        );

        UnregisterSignal(parent, signals_to_remove);
    }

    // tg: code/datums/components/supermatter_crystal.dm:54
    [SignalHandler]
    public virtual void blob_hit(datum source = null, obj_structure_blob blob = null)
    {
        atom atom_source = (atom)source;
        if (!Dm.truthy(blob) || isspaceturf(atom_source)) //does nothing in space
            return;
        playsound(get_turf(atom_source), Dm.resource("sound/effects/supermatter.ogg"), 50, TRUE);
        Waiting.NoSleep(consume_returns(damage_increase: Dm.num(blob.get_integrity()) * 0.05f));
        if (Dm.num(blob.get_integrity()) > 100)
        {
            blob.visible_message(span_danger(I18n.T("\\The [] strikes at \\the [] and flinches away!", blob, atom_source)),
                span_hear(I18n.T("You hear a loud crack as you are washed with a wave of heat.")));
            Waiting.NoSleep(blob.take_damage(100, BURN));
        }
        else
        {
            blob.visible_message(span_danger(I18n.T("\\The [] strikes at \\the [] and rapidly flashes to ash.", blob, atom_source)),
                span_hear(I18n.T("You hear a loud crack as you are washed with a wave of heat.")));
            Waiting.NoSleep(consume(atom_source, blob));
        }
    }

    // tg: code/datums/components/supermatter_crystal.dm:70
    [SignalHandler]
    public virtual void paw_hit(datum source = null, mob user = null, DmList modifiers = null)
    {
        if (isliving(user))
        {
            mob_living living_mob = (mob_living)user;
            if (Dm.truthy(living_mob.incorporeal_move) || HAS_TRAIT(living_mob, TRAIT_GODMODE))
                return;
        }
        if (isalien(user))
        {
            Waiting.NoSleep(dust_mob(source, user as mob_living, cause: "alien attack"));
            return;
        }
        Waiting.NoSleep(dust_mob(source, user as mob_living, cause: "monkey attack"));
    }

    // tg: code/datums/components/supermatter_crystal.dm:81
    [SignalHandler]
    public virtual void animal_hit(datum source = null, mob_living_simple_animal user = null, DmList modifiers = null)
    {
        if (Dm.truthy(user.incorporeal_move) || HAS_TRAIT(user, TRAIT_GODMODE))
            return;
        atom atom_source = (atom)source;
        object murder = null;
        if (!Dm.truthy(user.melee_damage_upper) && !Dm.truthy(user.melee_damage_lower))
            murder = user.friendly_verb_continuous;
        else
            murder = user.attack_verb_continuous;
        Waiting.NoSleep(dust_mob(source, user,
            span_danger(I18n.T("[] unwisely [] [], and [] body burns brilliantly before flashing into ash!", user, murder, atom_source, user.p_their())),
            span_userdanger(I18n.T("You unwisely touch [], and your vision glows brightly as your body crumbles to dust. Oops.", atom_source)),
            "simple animal attack"));
    }

    // tg: code/datums/components/supermatter_crystal.dm:96
    [SignalHandler]
    public virtual void hulk_hit(datum source = null, mob user = null)
    {
        Waiting.NoSleep(dust_mob(source, user as mob_living, cause: "hulk attack"));
    }

    // tg: code/datums/components/supermatter_crystal.dm:100
    [SignalHandler]
    public virtual void unarmed_hit(datum source = null, mob user = null, DmList modifiers = null)
    {
        if (isliving(user))
        {
            mob_living living_mob = (mob_living)user;
            if (Dm.truthy(living_mob.incorporeal_move) || HAS_TRAIT(living_mob, TRAIT_GODMODE))
                return;
        }
        atom atom_source = (atom)source;
        if (iscyborg(user) && Dm.truthy(Waiting.NoSleep(atom_source.Adjacent(user))))
        {
            Waiting.NoSleep(dust_mob(source, user as mob_living, cause: "cyborg attack"));
            return;
        }
        if (iscameramob(user))
            return;
        if (islarva(user))
        {
            Waiting.NoSleep(dust_mob(source, user as mob_living, cause: "larva attack"));
            return;
        }
    }

    // tg: code/datums/components/supermatter_crystal.dm:116
    [SignalHandler]
    public virtual void hand_hit(datum source = null, mob_living user = null, DmList modifiers = null)
    {
        if (Dm.truthy(user.incorporeal_move) || HAS_TRAIT(user, TRAIT_GODMODE))
            return;
        if (!Dm.eq(user.zone_selected, BODY_ZONE_PRECISE_MOUTH))
        {
            Waiting.NoSleep(dust_mob(source, user, cause: "hand"));
            return;
        }
        atom atom_source = (atom)source;
        if (!Dm.truthy(user.is_mouth_covered()))
        {
            if (Dm.truthy(user.combat_mode))
            {
                Waiting.NoSleep(dust_mob(source, user,
                    span_danger(I18n.T("As [] tries to take a bite out of [] everything goes silent before [] body starts to glow and burst into flames before flashing to ash.", user, atom_source, user.p_their())),
                    span_userdanger(I18n.T("You try to take a bite out of [], but find [] far too hard to get anywhere before everything starts burning and your ears fill with ringing!", atom_source, p_them())),
                    "attempted bite"
                ));
                return;
            }

            obj_item_organ_tongue licking_tongue = user.get_organ_slot(ORGAN_SLOT_TONGUE) as obj_item_organ_tongue;
            if (Dm.truthy(licking_tongue))
            {
                Waiting.NoSleep(dust_mob(source, user,
                    span_danger(I18n.T("As [] hesitantly leans in and licks [] everything goes silent before [] body starts to glow and burst into flames before flashing to ash!", user, atom_source, user.p_their())),
                    span_userdanger(I18n.T("You tentatively lick [], but you can't figure out what it tastes like before everything starts burning and your ears fill with ringing!", atom_source)),
                    "attempted lick"
                ));
                return;
            }
        }

        obj_item_bodypart_head forehead = user.get_bodypart(BODY_ZONE_HEAD) as obj_item_bodypart_head;
        if (Dm.truthy(forehead))
        {
            Waiting.NoSleep(dust_mob(source, user,
                span_danger(I18n.T("As []'s forehead bumps into [], inducing a resonance... Everything goes silent before [] [] flashes to ash!", user, atom_source, user.p_their(), forehead)),
                span_userdanger(I18n.T("You feel your forehead bump into [] and everything suddenly goes silent. As your head fills with ringing you come to realize that that was not a wise decision.", atom_source)),
                "failed lick"
            ));
            return;
        }

        Waiting.NoSleep(dust_mob(source, user,
            span_danger(I18n.T("[] leans in and tries to lick [], inducing a resonance... [] body starts to glow and burst into flames before flashing into dust!", user, atom_source, user.p_their())),
            span_userdanger(I18n.T("You lean in and try to lick []. Everything starts burning and all you can hear is ringing. Your last thought is \"That was not a wise decision.\"", atom_source)),
            "failed lick"
        ));
    }

    // tg: code/datums/components/supermatter_crystal.dm:157
    [SignalHandler]
    public virtual object attackby_hit(datum source = null, obj_item item = null, mob_living user = null, object @params = null)
    {
        atom atom_source = (atom)source;
        if (!(item is obj_item) || Dm.truthy(Dm.bitand(item.item_flags, ABSTRACT)) || !(user is mob_living))
            return null;
        // (the type-level static's typecacheof, made on the first use: rules 10.6's ??= form; it moves no world id)
        sm_item_whitelist ??= typecacheof(Dm.list(
            typeof(obj_item_melee_roastingstick),
            typeof(obj_item_toy_crayon_spraycan)
        )) as DmList;
        if (Dm.truthy(is_type_in_typecache(item, sm_item_whitelist)))
            return FALSE;
        if (item is obj_item_cigarette)
        {
            obj_item_cigarette cig = (obj_item_cigarette)item;
            float clumsy = Dm.num(HAS_TRAIT(user, TRAIT_CLUMSY));
            if (Dm.truthy(clumsy))
            {
                obj_item_bodypart dust_arm = user.get_active_hand() as obj_item_bodypart;
                Waiting.NoSleep(dust_arm.dismember());
                user.visible_message(span_danger(I18n.T("The [] flashes out of existence on contact with \\the [], resonating with a horrible sound...", item, atom_source)),
                    span_danger(I18n.T("Oops! The [] flashes out of existence on contact with \\the [], taking your arm with it! That was clumsy of you!", item, atom_source)));
                playsound(atom_source, Dm.resource("sound/effects/supermatter.ogg"), 150, TRUE);
                Waiting.NoSleep(consume(atom_source, dust_arm));
                qdel(item);
                return null;
            }
            if (Dm.truthy(user.combat_mode) || !Dm.truthy(Waiting.NoSleep(cig.attempt_light((mob_living)user, (obj_item)item, ""))))
            {
                user.visible_message(span_danger(I18n.T("A hideous sound echoes as [] is ashed out on contact with \\the []. That didn't seem like a good idea...", item, atom_source)));
                playsound(atom_source, Dm.resource("sound/effects/supermatter.ogg"), 150, TRUE);
                Waiting.NoSleep(consume(atom_source, item));
                radiation_pulse(atom_source, max_range: 3, threshold: 0.1f, chance: 50);
                return null;
            }
            user.visible_message(span_danger(I18n.T("As [] lights their [] on \\the [], silence fills the room...", user, item, atom_source)),
                span_danger(I18n.T("Time seems to slow to a crawl as you touch \\the [] with \\the [].</span>\n<span class='notice'>\\The [] flashes alight with an eerie energy as you nonchalantly lift your hand away from \\the []. Damn.", atom_source, item, item, atom_source)));
            playsound(atom_source, Dm.resource("sound/effects/supermatter.ogg"), 50, TRUE);
            radiation_pulse(atom_source, max_range: 1, threshold: 0, chance: 100);
            return null;
        }

        if (Dm.truthy(Waiting.NoSleep(user.dropItemToGround(item))))
        {
            user.visible_message(span_danger(I18n.T("As [] touches \\the [] with \\a [], silence fills the room...", user, atom_source, item)),
                span_userdanger(I18n.T("You touch \\the [] with \\the [], and everything suddenly goes silent.</span>\n<span class='notice'>\\The [] flashes into dust as you flinch away from \\the [].", atom_source, item, item, atom_source)),
                span_hear(I18n.T("Everything suddenly goes silent.")));
            user.investigate_log(Dm.text("has been attacked ([]) by []", item, key_name(user)), INVESTIGATE_ENGINE);
            Waiting.NoSleep(consume(atom_source, item));
            playsound(get_turf(atom_source), Dm.resource("sound/effects/supermatter.ogg"), 50, TRUE);

            radiation_pulse(atom_source, max_range: 3, threshold: 0.1f, chance: 50);
            return null;
        }

        if (Dm.truthy(Waiting.NoSleep(atom_source.Adjacent(user)))) //if the item is stuck to the person, kill the person too instead of eating just the item.
        {
            if (Dm.truthy(user.incorporeal_move) || HAS_TRAIT(user, TRAIT_GODMODE))
                return null;
            string vis_msg = span_danger(I18n.T("[] reaches out and touches [] with [], inducing a resonance... [] starts to glow briefly before the light continues up to []'s body. [] burst[] into flames before flashing into dust!", user, atom_source, item, item, user, user.p_They(), user.p_s()));
            string mob_msg = span_userdanger(I18n.T("You reach out and touch [] with []. Everything starts burning and all you can hear is ringing. Your last thought is \"That was not a wise decision.\"", atom_source, item));
            Waiting.NoSleep(dust_mob(source, user, vis_msg, mob_msg));
        }
        return null;
    }

    // tg: code/datums/components/supermatter_crystal.dm:207
    [SignalHandler]
    public virtual object tool_hit(datum source = null, mob user = null, obj_item tool = null)
    {
        if (Dm.truthy(tool_act_callback))
        {
            Waiting.NoSleep(tool_act_callback.Invoke(user, tool));
            return ITEM_INTERACT_BLOCKING;
        }
        attackby_hit(source, tool, user as mob_living);
        return null;
    }

    // tg: code/datums/components/supermatter_crystal.dm:214
    [SignalHandler]
    public virtual void bumped_hit(datum source = null, atom_movable hit_object = null)
    {
        if (isliving(hit_object))
        {
            mob_living hit_mob = (mob_living)hit_object;
            if (Dm.truthy(hit_mob.incorporeal_move) || HAS_TRAIT(hit_mob, TRAIT_GODMODE))
                return;
        }
        atom atom_source = (atom)source;
        obj_machinery_power_supermatter_crystal our_supermatter = parent as obj_machinery_power_supermatter_crystal; // Why is this a component?
        if (our_supermatter is obj_machinery_power_supermatter_crystal)
            our_supermatter.log_activation(who: hit_object);
        if (isliving(hit_object))
        {
            hit_object.visible_message(span_danger(I18n.T("\\The [] slams into \\the [] inducing a resonance... [] body starts to glow and burst into flames before flashing into dust!", hit_object, atom_source, hit_object.p_their())),
                span_userdanger(I18n.T("You slam into \\the [] as your ears are filled with unearthly ringing. Your last thought is \"Oh, fuck.\"", atom_source)),
                span_hear(I18n.T("You hear an unearthly noise as a wave of heat washes over you.")));
        }
        else if (isobj(hit_object) && !iseffect(hit_object))
        {
            hit_object.visible_message(span_danger(I18n.T("\\The [] smacks into \\the [] and rapidly flashes to ash.", hit_object, atom_source)), null,
                span_hear(I18n.T("You hear a loud crack as you are washed with a wave of heat.")));
        }
        else
            return;

        playsound(get_turf(atom_source), Dm.resource("sound/effects/supermatter.ogg"), 50, TRUE);
        Waiting.NoSleep(consume(atom_source, hit_object));
    }

    // tg: code/datums/components/supermatter_crystal.dm:237
    [SignalHandler]
    public virtual object intercept_z_fall(datum source = null, DmList falling_movables = null, object levels = null)
    {
        foreach (atom_movable hit_object in Dm.each_cast<atom_movable>(falling_movables))
        {
            if (parent == hit_object)
                return null;

            bumped_hit(parent, hit_object);
        }
        return Dm.bitor(FALL_INTERCEPTED, FALL_NO_MESSAGE);
    }

    // tg: code/datums/components/supermatter_crystal.dm:246
    [SignalHandler]
    public virtual void on_z_impact(datum source = null, turf impacted_turf = null, object levels = null)
    {
        atom atom_source = (atom)source;

        foreach (mob_living poor_target in Dm.each<mob_living>(impacted_turf))
        {
            Waiting.NoSleep(consume(atom_source, poor_target));
            playsound(get_turf(atom_source), Dm.resource("sound/effects/supermatter.ogg"), 50, TRUE);
            poor_target.visible_message(span_danger(I18n.T("\\The [] slams into \\the [] out of nowhere inducing a resonance... [] body starts to glow and burst into flames before flashing into dust!", atom_source, poor_target, poor_target.p_their())),
                span_userdanger(I18n.T("\\The [] slams into you out of nowhere as your ears are filled with unearthly ringing. Your last thought is \"The fuck.\"", atom_source)),
                span_hear(I18n.T("You hear an unearthly noise as a wave of heat washes over you.")));
        }

        foreach (atom_movable hit_object in Dm.each_cast<atom_movable>(impacted_turf))
        {
            if (parent == hit_object)
                return;

            if (iseffect(hit_object))
                continue;

            Waiting.NoSleep(consume(atom_source, hit_object));
            playsound(get_turf(atom_source), Dm.resource("sound/effects/supermatter.ogg"), 50, TRUE);
            atom_source.visible_message(span_danger(I18n.T("\\The [], smacks into the plating out of nowhere, reducing everything below to ash.", atom_source)), null,
                span_hear(I18n.T("You hear a loud crack as you are washed with a wave of heat.")));
        }
    }

    // tg: code/datums/components/supermatter_crystal.dm:270
    public virtual async DmTask dust_mob(datum source = null, mob_living nom = null, object vis_msg = null, object mob_msg = null, object cause = null)
    {
        if (Dm.truthy(nom.incorporeal_move) || HAS_TRAIT(nom, TRAIT_GODMODE)) //try to keep supermatter sliver's + hemostat's dust conditions in sync with this too
            return;
        atom atom_source = (atom)source;
        if (!Dm.truthy(vis_msg))
            vis_msg = span_danger(I18n.T("[] reaches out and touches [], inducing a resonance... [] body starts to glow and burst into flames before flashing into dust!", nom, atom_source, nom.p_their()));
        if (!Dm.truthy(mob_msg))
            mob_msg = span_userdanger(I18n.T("You reach out and touch []. Everything starts burning and all you can hear is ringing. Your last thought is \"That was not a wise decision.\"", atom_source));
        if (!Dm.truthy(cause))
            cause = "contact";
        nom.visible_message(vis_msg, mob_msg, span_hear(I18n.T("You hear an unearthly noise as a wave of heat washes over you.")));
        atom_source.investigate_log(Dm.text("has been attacked ([]) by []", cause, key_name(nom)), INVESTIGATE_ENGINE);
        add_memory_in_range(atom_source, 7, typeof(datum_memory_witness_supermatter_dusting), Dm.named("protagonist", nom), Dm.named("antagonist", atom_source));
        playsound(get_turf(atom_source), Dm.resource("sound/effects/supermatter.ogg"), 50, TRUE);
        await consume(atom_source, nom);
    }

    // tg: code/datums/components/supermatter_crystal.dm:286
    public virtual async DmTask consume(atom source = null, atom_movable consumed_object = null)
    {
        if (Dm.truthy(Dm.bitand(consumed_object.flags_1, SUPERMATTER_IGNORES_1)))
            return;
        if (HAS_TRAIT(consumed_object, TRAIT_GODMODE))
            return;

        atom atom_source = source;
        if (Dm.truthy(consumed_object._listen_lookup?[COMSIG_SUPERMATTER_CONSUMED]))
            consumed_object._SendSignal(COMSIG_SUPERMATTER_CONSUMED, Dm.list(consumed_object, atom_source));

        float object_size = 0;
        float matter_increase = 0;
        float damage_increase = 0;
        float radiation_range = 6;
        float effects_calculated = FALSE;

        if (isliving(consumed_object))
        {
            mob_living consumed_mob = (mob_living)consumed_object;
            object_size = Dm.num(consumed_mob.mob_size) + 2;
            message_admins(Dm.text("[] has consumed [] [].", atom_source, key_name_admin(consumed_mob), ADMIN_JMP(atom_source)));
            atom_source.investigate_log(Dm.text("has consumed [].", key_name(consumed_mob)), INVESTIGATE_ENGINE);
            consumed_mob.investigate_log(Dm.text("has been dusted by [].", atom_source), INVESTIGATE_DEATHS);
            if (consumed_mob is mob_living_basic_parrot_poly) // Dusting Poly creates a power surge
            {
                await force_event(typeof(datum_round_event_control_supermatter_surge_poly), "Poly's revenge");
                notify_ghosts(
                    I18n.T("[] has been dusted by []!", consumed_mob.real_name, atom_source),
                    source: atom_source,
                    header: I18n.T("Polytechnical Difficulties"));
            }
            await consumed_mob.dust(force: TRUE);
            matter_increase += 100 * object_size * 2;
            if (is_clown_job(consumed_mob.mind?.assigned_role))
                damage_increase += rand(-30, 30) * 2; // HONK
            effects_calculated = TRUE;
        }
        else if (isobj(consumed_object))
        {
            if (!iseffect(consumed_object))
            {
                string suspicion = "";
                if (Dm.truthy(consumed_object.fingerprintslast))
                {
                    suspicion = Dm.text("last touched by []", consumed_object.fingerprintslast);
                    message_admins(Dm.text("[] has consumed [], [] [].", atom_source, consumed_object, suspicion, ADMIN_JMP(atom_source)));
                }
                atom_source.investigate_log(Dm.text("has consumed [] - [].", consumed_object, suspicion), INVESTIGATE_ENGINE);
            }

            float is_nuke = FALSE;
            if (consumed_object.type == typeof(obj_item_nuke_core)) // No subtypes, the supermatter sliver shouldn't trigger this
                is_nuke = TRUE;
            else if (consumed_object is obj_machinery_nuclearbomb)
            {
                obj_machinery_nuclearbomb bomb = (obj_machinery_nuclearbomb)consumed_object;
                is_nuke = Dm.num(Dm.truthy(bomb.core));
            }

            if (Dm.truthy(is_nuke))
            {
                object_size = 10;
                radiation_range *= 2;
                matter_increase += 10000;
                damage_increase += 110;
                effects_calculated = TRUE;
            }

            qdel(consumed_object);
        }

        if (!iseffect(consumed_object) && !Dm.truthy(effects_calculated))
        {
            if (isitem(consumed_object))
            {
                obj_item consumed_item = (obj_item)consumed_object;
                object_size = Dm.num(consumed_item.w_class);
                matter_increase += 70 * object_size;
            }
            else
                matter_increase += min(0.5f * Dm.num(consumed_object.max_integrity), 1000);
        }

        //Some poor sod got eaten, go ahead and irradiate people nearby.
        radiation_pulse(atom_source, max_range: radiation_range, threshold: 1.2f / max(object_size, 1), chance: 10 * object_size);
        foreach (mob_living near_mob in Dm.each<mob_living>(range(10)))
        {
            atom_source.investigate_log(Dm.text("has irradiated [] after consuming [].", key_name(near_mob), consumed_object), INVESTIGATE_ENGINE);
            if (HAS_TRAIT(near_mob, TRAIT_RADIMMUNE) || issilicon(near_mob))
                continue;
            if (ishuman(near_mob) && Dm.truthy(SSradiation.wearing_rad_protected_clothing((mob_living_carbon_human)near_mob)))
                continue;
            if (Dm.@in(near_mob, view()))
            {
                near_mob.show_message(span_danger(I18n.T("As \\the [] slowly stops resonating, you find your skin covered in new radiation burns.", atom_source)), MSG_VISUAL,
                    span_danger(I18n.T("The unearthly ringing subsides and you find your skin covered in new radiation burns.")), MSG_AUDIBLE);
            }
            else
                near_mob.show_message(span_hear(I18n.T("An unearthly ringing fills your ears, and you find your skin covered in new radiation burns.")), MSG_AUDIBLE);
        }
        await consume_returns(matter_increase, damage_increase);
        obj_machinery_power_supermatter_crystal our_crystal = parent as obj_machinery_power_supermatter_crystal;
        if (our_crystal is obj_machinery_power_supermatter_crystal)
            our_crystal.log_activation(who: consumed_object);
    }

    // tg: code/datums/components/supermatter_crystal.dm:369
    public virtual async DmTask consume_returns(float? matter_increase = null, float? damage_increase = null)
    {
        matter_increase ??= 0;
        damage_increase ??= 0;
        if (Dm.truthy(consume_callback))
            await consume_callback.Invoke(matter_increase, damage_increase);
    }
}

public sealed partial class TgState
{
    // tg: code/datums/components/supermatter_crystal.dm:8  var/static/list/sm_item_whitelist = typecacheof(list(...)) (made on the first use)
    internal DmList datum_component_supermatter_crystal__sm_item_whitelist;
}
