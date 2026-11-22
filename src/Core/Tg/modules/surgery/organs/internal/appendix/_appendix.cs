// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/organs/internal/appendix/_appendix.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The appendix's on_life with appendicitis, its insertion hooks and on_fully_heal (M2 P2, plan.md Q7: tg's random appendicitis roll; in
// this snapshot appendicitis is the organ's own inflammation, no disease datum); the ghost notification since gaps E1a. Gaps OG1: the
// grinding, the inflamed name and sprite.

// tg: code/modules/surgery/organs/internal/appendix/_appendix.dm:5
[DmPath("/obj/item/organ/appendix")]
public partial class obj_item_organ_appendix : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/appendix/_appendix.dm:6-18
    public obj_item_organ_appendix()
    {
        name = "appendix";
        icon_state = "appendix";
        base_icon_state = "appendix";

        zone = BODY_ZONE_PRECISE_GROIN;
        slot = ORGAN_SLOT_APPENDIX;
        healing_factor = STANDARD_ORGAN_HEALING;
        decay_factor = STANDARD_ORGAN_DECAY;

        now_failing = span_warning("An explosion of pain erupts in your lower right abdomen!");
        now_fixed = span_info("The pain in your abdomen has subsided.");
        visual = FALSE;
    }

    // tg: code/modules/surgery/organs/internal/appendix/_appendix.dm:12  the object-creating defaults (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        food_reagents = Dm.list(Dm.kv(typeof(datum_reagent_consumable_nutriment_organ_tissue), 5), Dm.kv(typeof(datum_reagent_toxin_bad_food), 5));
    }

    // tg: code/modules/surgery/organs/internal/appendix/_appendix.dm:20
    public float inflamation_stage = 0;
}

// tg: code/modules/surgery/organs/internal/appendix/_appendix.dm:22 (gaps OG1)
public partial class obj_item_organ_appendix : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/appendix/_appendix.dm:22
    public override async DmTask<object> grind_results()
    {
        await DmTask.CompletedTask;
        return Dm.list(Dm.kv(typeof(datum_reagent_toxin_bad_food), 5));
    }

    // tg: code/modules/surgery/organs/internal/appendix/_appendix.dm:25
    public override object update_name(float? updates = null)
    {
        object dot = base.update_name(updates);
        name = Dm.text("[][]", Dm.truthy(inflamation_stage) ? I18n.T("inflamed ") : null, Dm.initial(this, (obj_item_organ_appendix p) => p.name));
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/appendix/_appendix.dm:29
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[][]", base_icon_state, Dm.truthy(inflamation_stage) ? "inflamed" : "");
        return await base.update_icon_state(__extra);
    }
}

// tg: code/modules/surgery/organs/internal/appendix/_appendix.dm:33 (interleaved types, rules 1.9)
public partial class obj_item_organ_appendix : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/appendix/_appendix.dm:2  #define APPENDICITIS_PROB 100 * (0.1 * (1 / 25) / 3600) (file-local, #undef at the end)
    private const float APPENDICITIS_PROB = 100 * (0.1f * (1f / 25) / 3600);
    // tg: code/modules/surgery/organs/internal/appendix/_appendix.dm:3  #define INFLAMATION_ADVANCEMENT_PROB 2 (file-local)
    private const int INFLAMATION_ADVANCEMENT_PROB = 2;

    // tg: code/modules/surgery/organs/internal/appendix/_appendix.dm:33
    public override async DmTask on_life(object seconds_per_tick = null)
    {
        await base.on_life(seconds_per_tick);
        if (owner == null)
            return;

        if (Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)))
            // forced to ensure people don't use it to gain tox as slime person
            await owner.adjust_tox_loss(2 * Dm.num(seconds_per_tick), forced: TRUE);
        else if (Dm.truthy(inflamation_stage))
            await inflamation(seconds_per_tick);
        else if (SPT_PROB(APPENDICITIS_PROB, seconds_per_tick) && !HAS_TRAIT(owner, TRAIT_TEMPORARY_BODY))
            become_inflamed();
    }
}

// tg: code/modules/surgery/organs/internal/appendix/_appendix.dm:46
public partial class obj_item_organ_appendix : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/appendix/_appendix.dm:46
    public virtual void become_inflamed()
    {
        inflamation_stage = 1;
        update_appearance();
        if (owner == null)
            return;
        ADD_TRAIT(owner, TRAIT_DISEASELIKE_SEVERITY_MEDIUM, type);
        owner.med_hud_set_status();
        RegisterSignal(owner, COMSIG_LIVING_POST_FULLY_HEAL, nameof(on_fully_heal));
        if (owner.client == null)
            return;
        notify_ghosts(
            I18n.T("[] has developed spontaneous appendicitis!", owner.real_name),
            source: owner,
            header: I18n.T("Whoa, Sick!"));
    }

    // tg: code/modules/surgery/organs/internal/appendix/_appendix.dm:62
    public virtual async DmTask inflamation(object seconds_per_tick = null)
    {
        mob_living_carbon organ_owner = owner;
        if (inflamation_stage < 3 && SPT_PROB(INFLAMATION_ADVANCEMENT_PROB, seconds_per_tick))
            inflamation_stage += 1;

        float sw__1 = inflamation_stage;
        switch (sw__1)
        {
            case 1:
                if (SPT_PROB(2.5f, seconds_per_tick))
                {
                    await organ_owner.emote("cough");
                }
                break;
            case 2:
                if (SPT_PROB(1.5f, seconds_per_tick))
                {
                    to_chat(organ_owner, span_warning(I18n.T("You feel a stabbing pain in your abdomen!")));
                    await organ_owner.adjust_organ_loss(ORGAN_SLOT_APPENDIX, 5);
                    await organ_owner.Stun(rand(40, 60));
                    await organ_owner.adjust_tox_loss(1, forced: TRUE);
                }
                break;
            case 3:
                if (SPT_PROB(0.5f, seconds_per_tick))
                {
                    await organ_owner.vomit(VOMIT_CATEGORY_DEFAULT, lost_nutrition: 95);
                    await organ_owner.adjust_organ_loss(ORGAN_SLOT_APPENDIX, 15);
                }
                break;
        }
    }

    // tg: code/modules/surgery/organs/internal/appendix/_appendix.dm:109
    [SignalHandler]
    public virtual void on_fully_heal(datum source = null, object heal_flags = null)
    {
        if (!Dm.truthy(Dm.bitand(heal_flags, HEAL_ORGANS)))
            return;

        inflamation_stage = 0;
        update_appearance();
        UnregisterSignal(owner, COMSIG_LIVING_POST_FULLY_HEAL);
        REMOVE_TRAIT(owner, TRAIT_DISEASELIKE_SEVERITY_MEDIUM, type);
        owner.med_hud_set_status();
    }
}

// tg: code/modules/surgery/organs/internal/appendix/_appendix.dm:92 (interleaved types, rules 1.9)
public partial class obj_item_organ_appendix : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/appendix/_appendix.dm:92
    public override object get_availability(datum_species owner_species = null, mob_living owner_mob = null)
    {
        return owner_species.mutantappendix;
    }

    // tg: code/modules/surgery/organs/internal/appendix/_appendix.dm:95
    [DmParams("organ_owner")]
    public override async DmTask on_mob_remove(object organ_owner = null, float? special = null, object movement_flags = null)
    {
        await base.on_mob_remove(organ_owner, special, movement_flags);
        UnregisterSignal(organ_owner as datum, COMSIG_LIVING_POST_FULLY_HEAL);
        REMOVE_TRAIT(organ_owner, TRAIT_DISEASELIKE_SEVERITY_MEDIUM, type);
        (organ_owner as mob_living_carbon).med_hud_set_status();
    }

    // tg: code/modules/surgery/organs/internal/appendix/_appendix.dm:101
    [DmParams("organ_owner")]
    public override async DmTask<object> on_mob_insert(mob_living_carbon organ_owner = null, float? special = null, object movement_flags = null)
    {
        object dot = null;
        dot = await base.on_mob_insert(organ_owner, special, movement_flags);
        if (!Dm.truthy(inflamation_stage))
            return dot;
        ADD_TRAIT(organ_owner, TRAIT_DISEASELIKE_SEVERITY_MEDIUM, type);
        organ_owner.med_hud_set_status();
        RegisterSignal(organ_owner, COMSIG_LIVING_POST_FULLY_HEAL, nameof(on_fully_heal));
        return dot;
    }
}

// tg: code/modules/surgery/organs/internal/appendix/_appendix.dm:82 (M6 slice 2: the self-check's organ lines)
public partial class obj_item_organ_appendix : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/appendix/_appendix.dm:82
    public override object feel_for_damage(object self_aware = null)
    {
        float effective_stage = floor(inflamation_stage + (Dm.num(damage) / maxHealth));
        if (effective_stage == 1)
            return span_warning(I18n.T("Your [] feels a little off.", Dm.truthy(self_aware) ? I18n.T("appendix") : I18n.T("lower abdomen")));
        if (effective_stage == 2)
            return span_warning(I18n.T("Your [] feels sore.", Dm.truthy(self_aware) ? I18n.T("appendix") : I18n.T("lower right abdomen")));
        if (3 <= effective_stage)
            return span_boldwarning(I18n.T("Your [] feels like it's on fire!", Dm.truthy(self_aware) ? I18n.T("appendix") : I18n.T("lower right abdomen")));
        return null;
    }
}

// tg: code/modules/surgery/organs/internal/appendix/_appendix.dm:121 (interleaved types, rules 1.9; M8 slice D)
public partial class obj_item_organ_appendix : obj_item_organ
{
    // tg: code/modules/surgery/organs/internal/appendix/_appendix.dm:121
    public override object get_status_text(object scanpower = null, object add_tooltips = null, float? colored = null)
    {
        if (!Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)) && Dm.truthy(inflamation_stage))
            return conditional_tooltip(I18n.T("<font color='#ff9933'>Inflamed</font>"), I18n.T("Remove surgically."), add_tooltips);
        return base.get_status_text(scanpower, add_tooltips, colored);
    }
}

// tg: code/modules/surgery/organs/internal/appendix/_appendix.dm:126 (gaps P3b: the plant infusion)
public partial class obj_item_organ_appendix_pod : obj_item_organ_appendix
{
    // tg: code/modules/surgery/organs/internal/appendix/_appendix.dm:132
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        // this could be anything... anything. still useless though
        name = Dm.str(pick(I18n.T("pod endoplasmic reticulum"), I18n.T("pod golgi apparatus"), I18n.T("pod plastid"), I18n.T("pod vesicle")));
        return dot;
    }

    // tg: code/modules/surgery/organs/internal/appendix/_appendix.dm:137
    public override void become_inflamed()
    {
        return;
    }
}
