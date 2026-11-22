// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/fish/_fish.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// SKELETON: completed in wave 3 or later (with its module). X3: examine and the health warnings it reads.
// Only the members that the list, sort and comparator helpers (code/__HELPERS/_lists.dm, cmp.dm, sorts/) name;
// bodies are Dm.NotTranslated until then.

// tg: code/modules/fishing/fish/_fish.dm:19
[DmPath("/obj/item/fish")]
public partial class obj_item_fish : obj_item
{
    // tg: code/modules/fishing/fish/_fish.dm:64
    public string required_fluid_type = AQUARIUM_FLUID_FRESHWATER;

    // tg: code/modules/fishing/fish/_fish.dm:552  (X3)
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(catcher_name) && Dm.truthy(catch_date))
            Dm.append(ref dot, span_boldnicegreen(I18n.T("Caught by [] on [].", catcher_name, catch_date)));

        if (HAS_MIND_TRAIT(user, TRAIT_EXAMINE_FISH) || HAS_TRAIT(loc, TRAIT_EXAMINE_FISH))
        {
            Dm.append(ref dot, span_notice(I18n.T("[] [] cm long.", p_Theyre(), size)));
            Dm.append(ref dot, span_notice(I18n.T("[] weigh[] [] [].", p_They(), p_s(), weight, span_tooltip(I18n.T("the standard unit of measurement for space age fish"), I18n.T("kiloclam")))));

            if (HAS_TRAIT(this, TRAIT_FISH_GENEGUNNED))
                Dm.append(ref dot, span_warning(I18n.T("[] been edited by a fish genegun. []'ll die if edited again.", p_Theyve(), p_They())));
        }

        Dm.append(ref dot, get_health_warnings(user, always_deep: FALSE));

        if (HAS_TRAIT(this, TRAIT_FISHING_BAIT))
            Dm.append(ref dot, span_smallnoticeital(I18n.T("[] can be used as a fishing bait.", p_They())));

        if (Dm.truthy(bites_amount))
            Dm.append(ref dot, span_warning(I18n.T("[] been bitten by someone.", p_Theyve())));
        return dot;
    }

    // tg: code/modules/fishing/fish/_fish.dm:572  (X3)
    public virtual object get_health_warnings(mob user = null, object always_deep = null)
    {
        always_deep ??= FALSE;
        object dot = null;
        if (!HAS_MIND_TRAIT(user, TRAIT_EXAMINE_DEEPER_FISH) && !Dm.truthy(always_deep))
            return dot;
        if (Dm.eq(status, FISH_DEAD))
            return span_deadsay(I18n.T("[] [].", p_Theyre(), HAS_MIND_TRAIT(user, TRAIT_NAIVE) ? I18n.T("taking the big snooze") : I18n.T("dead")));

        DmList warnings = Dm.list();
        if (Dm.truthy(get_starvation_mult()))
            Dm.append(ref warnings, I18n.T("starving"));
        // tg: if(!HAS_TRAIT(src, TRAIT_FISH_STASIS) && !proper_environment()) warnings += "drowning"
        if (!HAS_TRAIT(this, TRAIT_FISH_STASIS))
            Dm.Deferred("code/modules/fishing/fish/_fish.dm:581", "get_health_warnings: the drowning warning (proper_environment reads SSfishing's safe turfs and the aquariums: fishing)");

        float health_ratio = Dm.num(get_health_percentage());
        if (health_ratio >= 0 && health_ratio <= 0.25f)
            Dm.append(ref warnings, I18n.T("dying"));
        else if (health_ratio >= 0.25f && health_ratio <= 0.5f)
            Dm.append(ref warnings, I18n.T("very unhealthy"));
        else if (health_ratio >= 0.5f && health_ratio <= 0.75f)
            Dm.append(ref warnings, I18n.T("unhealthy"));
        else if (health_ratio >= 0.75f && health_ratio <= 0.9f)
            Dm.append(ref warnings, I18n.T("mostly healthy"));

        if (Dm.truthy(Dm.length(warnings)))
            Dm.append(ref dot, span_warning(I18n.T("[] [].", p_Theyre(), english_list(warnings))));

        return dot;
    }

    ///Returns the value for hunger ranging from 0 to the cap (by default 1)
    // tg: code/modules/fishing/fish/_fish.dm:917  (X3: the examine's starving warning)
    public virtual object get_hunger(object cap = null)
    {
        cap ??= FISH_STARVING_THRESHOLD;
        float dot = clamp((Dm.num(world.time) - Dm.num(last_feeding)) / feeding_frequency, 0, Dm.num(cap));
        if (HAS_TRAIT(this, TRAIT_FISH_NO_HUNGER))
            return min(dot, FISH_STARVING_THRESHOLD * 0.2f);
        return dot;
    }

    // tg: code/modules/fishing/fish/_fish.dm:922  (X3: the examine's starving warning)
    public virtual object get_starvation_mult()
    {
        float hunger = Dm.num(get_hunger(cap: FISH_STARVING_THRESHOLD * 2));
        return hunger >= FISH_STARVING_THRESHOLD ? hunger : 0;
    }

    /// returns a value between 0 and 1 representing how much integrity the fish has before dying (atom_break)
    // tg: code/modules/fishing/fish/_fish.dm:1297  (X3: the examine's health warnings)
    public virtual object get_health_percentage()
    {
        float max_health = Dm.num(max_integrity) * (1 - integrity_failure);
        float death_thres = Dm.num(max_integrity) - max_health;
        return CLAMP01((Dm.num(get_integrity()) - death_thres) / max_health);
    }
}

// tg: code/modules/fishing/fish/_fish.dm:9 (interleaved types, rules 1.9; gaps P4c: the fish bounties read it)
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/fishing/fish/_fish.dm:9  GLOBAL_LIST_INIT(fish_compatible_fluid_types, list(...))
    public DmList fish_compatible_fluid_types;

    // tg: code/modules/fishing/fish/_fish.dm:9  (GLOBAL_MANAGED(fish_compatible_fluid_types, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalfish_compatible_fluid_types()
    {
        fish_compatible_fluid_types = Dm.list(
            Dm.kv(AQUARIUM_FLUID_ANY_WATER, Dm.list(AQUARIUM_FLUID_SALTWATER, AQUARIUM_FLUID_FRESHWATER, AQUARIUM_FLUID_SULPHWATEVER)),
            Dm.kv(AQUARIUM_FLUID_ANADROMOUS, Dm.list(AQUARIUM_FLUID_SALTWATER, AQUARIUM_FLUID_FRESHWATER)),
            Dm.kv(AQUARIUM_FLUID_SALTWATER, Dm.list(AQUARIUM_FLUID_SALTWATER)),
            Dm.kv(AQUARIUM_FLUID_FRESHWATER, Dm.list(AQUARIUM_FLUID_FRESHWATER)),
            Dm.kv(AQUARIUM_FLUID_SULPHWATEVER, Dm.list(AQUARIUM_FLUID_SULPHWATEVER)),
            Dm.kv(AQUARIUM_FLUID_AIR, Dm.list(AQUARIUM_FLUID_AIR)));
        Dm.append(ref gvars_datum_init_order, "fish_compatible_fluid_types");
    }
}

