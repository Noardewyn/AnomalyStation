// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/ordnance/_scipaper.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the scientific papers that the NT Frontier app writes from an ordnance record and publishes to a techweb (their
// sigmoid gains, tiers and sponsors), with the explosive and gaseous kinds that check a record is not published twice; the
// scientific partners (SSresearch makes them; their boosts discount the nodes). The var blocks are generated (D111).

/// Scientific paper datum for retrieval and re-reading. A lot of the variables are there for fluff & flavor.
// tg: code/modules/research/ordnance/_scipaper.dm:2
public partial class datum_scientific_paper : datum
{
    // tg: code/modules/research/ordnance/_scipaper.dm:25
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        set_amount();
    }

    /**
     * Calculate the gains of an experiment.
     * Gain calculation follows a sigmoid curve.
     * f(x) = L / (1+e^(-k(x-xo)))
     * L is the upper limit. This should be the gain variable * 2.
     * k is the steepness.
     * x0 is the midpoint.
     * x is our tracked variable.
     * Returns the expected value of that tier.
     */
    // tg: code/modules/research/ordnance/_scipaper.dm:39
    public virtual object calculate_gains(object calculated_tier = null)
    {
        if (experiment_path == null || !Dm.truthy(tracked_variable))
            return FALSE;

        datum_experiment_ordnance initialized_experiment = Dm.locate_type(experiment_path, SSresearch.ordnance_experiments) as datum_experiment_ordnance;
        object gain = initialized_experiment.gain[calculated_tier];
        object target_amount = initialized_experiment.target_amount[calculated_tier];
        /// Steepness is calculated so that f(x=0) is always 1.
        float steepness = log(Dm.num(gain) * 2 - 1) / Dm.num(target_amount);
        float calculated_gain = Dm.num(gain) * 2 / (1 + Dm.pow(NUM_E, -steepness * (Dm.num(tracked_variable) - Dm.num(target_amount))));
        return calculated_gain;
    }

    /// Determine which tier can we publish at. Lower limit for an allowed tier is 10% of gain. Empty list if none are allowed.
    // tg: code/modules/research/ordnance/_scipaper.dm:52
    public virtual object calculate_tier()
    {
        DmList allowed_tiers = Dm.list();
        if (experiment_path == null || !Dm.truthy(tracked_variable))
            return allowed_tiers;
        datum_experiment_ordnance initialized_experiment = Dm.locate_type(experiment_path, SSresearch.ordnance_experiments) as datum_experiment_ordnance;
        float each_tier__end = min(length(initialized_experiment.target_amount), length(initialized_experiment.gain));
        for (float each_tier = 1; each_tier <= each_tier__end; each_tier++)
        {
            object calculated_gain = calculate_gains(each_tier);
            if (Dm.num(calculated_gain) > 0.1f * Dm.num(initialized_experiment.gain[each_tier]))
                Dm.append(ref allowed_tiers, each_tier);
        }
        return allowed_tiers;
    }

    /**
     * Experiment -> Tier -> Gains
     * Experiment -> Partners -> Gains
     * Changing anything in the chain means those following it should be recounted.
     */

    /**
     * Used when assigning an experiment to a specific paper.
     * Failing to provide a proper path, a tracked variable, or a correct data should null every non-fluff data.
     * Implement this in the children procs.
     */
    // tg: code/modules/research/ordnance/_scipaper.dm:74
    public virtual void set_experiment(object ex_path = null, object variable = null, object data = null)
    {
        return;
    }

    /// Sets a tier for us. Nulls the tier when called without args.  Re-counts the amount.
    // tg: code/modules/research/ordnance/_scipaper.dm:78
    public virtual void set_tier(object assigned_tier = null)
    {
        tier = null;
        if (Dm.truthy(assigned_tier) && Dm.@in(assigned_tier, calculate_tier()))
            tier = assigned_tier;
        set_amount();
    }

    /** Sets a specific partner for our paper. Partners give money and boost points.
     * Partners exist separately from experiments and wont need to be reset every time something changes.
     */
    // tg: code/modules/research/ordnance/_scipaper.dm:87
    public virtual void set_partner(object new_partner = null)
    {
        partner_path = null;
        if (ispath(new_partner, typeof(datum_scientific_partner)))
        {
            datum_scientific_partner partner = Dm.locate_type(new_partner as Type, SSresearch.scientific_partners) as datum_scientific_partner;
            if (Dm.@in(experiment_path, partner.accepted_experiments))
                partner_path = new_partner as Type;
        }
        set_amount();
    }

    /** Does the counting for gains.
     * Call this whenever experiments/tier/partners are changed.
     * Handled automatically, calling this proc externally shouldn't be necessary.
     * Also doubles as an initialization for the gains list.
     */
    // tg: code/modules/research/ordnance/_scipaper.dm:100
    public virtual object set_amount()
    {
        gains = Dm.list(Dm.kv(SCIPAPER_COOPERATION_INDEX, 0), Dm.kv(SCIPAPER_FUNDING_INDEX, 0));
        if (!Dm.truthy(tier) || experiment_path == null || !Dm.truthy(tracked_variable))
            return FALSE;
        object gain = calculate_gains(tier);
        foreach (object gain_index in Dm.each(gains))
        {
            gains[gain_index] = gain;
            if (partner_path == null)
                continue;
            datum_scientific_partner partner = Dm.locate_type(partner_path, SSresearch.scientific_partners) as datum_scientific_partner;
            gains[gain_index] = Dm.num(gains[gain_index]) * Dm.num(partner.multipliers[gain_index]);
        }
        return null;
    }

    /** Fully check if our paper have all the required variables, and prevent duplicate papers being published in the same tier.
     * Things to check: tier, gain, and partner here. ex_path and record datums in subtypes.
     */
    // tg: code/modules/research/ordnance/_scipaper.dm:115
    public virtual object allowed_to_publish(datum_techweb techweb_to_check = null)
    {
        if (!Dm.truthy(tier) || gains == null || partner_path == null || Dm.@in(0, gains))
            return FALSE;
        return Dm.not((techweb_to_check.published_papers[experiment_path] as DmList)[tier]);
    }

    // tg: code/modules/research/ordnance/_scipaper.dm:120
    public virtual void publish_paper(datum_techweb techweb_to_publish = null)
    {
        autofill();
        (techweb_to_publish.published_papers[experiment_path] as DmList)[tier] = this;
        techweb_to_publish.scientific_cooperation[partner_path] = Dm.num(techweb_to_publish.scientific_cooperation[partner_path]) + Dm.num(gains[SCIPAPER_COOPERATION_INDEX]);
        if (istype(techweb_to_publish, typeof(datum_techweb_science)))
        {
            datum_bank_account dept_budget = SSeconomy.get_dep_account(ACCOUNT_SCI) as datum_bank_account;
            if (Dm.truthy(dept_budget))
                dept_budget.adjust_money(Dm.num(gains[SCIPAPER_FUNDING_INDEX]) * SCIPAPER_GAIN_TO_MONEY);
        }
    }

    /**
     * Clones into a new paper type.
     * Important (non-fluff) variables will be carried over and should be cleaned with set_experiment by whoever is calling this.
     *
     * clone_into your own typepath will be like a normal clone.
     *
     * If you want to subtype this, do it in a way that doesn't mess with the type change.
     */
    // tg: code/modules/research/ordnance/_scipaper.dm:137
    public virtual object clone_into(object typepath = null)
    {
        datum_scientific_paper new_paper = Dm.@new((Type)typepath) as datum_scientific_paper;
        new_paper.title = title;
        new_paper.author = author;
        new_paper.et_alia = et_alia;
        new_paper.@abstract = @abstract;
        new_paper.gains = gains;
        new_paper.experiment_path = experiment_path;
        new_paper.tracked_variable = tracked_variable;
        new_paper.tier = tier;
        new_paper.partner_path = partner_path;
        return new_paper;
    }

    /// Returns the formatted, readable gist of our paper in a list.
    // tg: code/modules/research/ordnance/_scipaper.dm:151
    public virtual object return_gist()
    {
        DmList gist = Dm.list();
        DmList transcripted_gains = Dm.list(SCIPAPER_COOPERATION_INDEX, SCIPAPER_FUNDING_INDEX);
        foreach (object gain_index in Dm.each(transcripted_gains))
        {
            if (gains == null)
            {
                transcripted_gains[gain_index] = "None";
                continue;
            }
            float sw__1 = round(Dm.num(gains[gain_index]));
            if (Dm.in_to(sw__1, -INFINITY, 0))
                transcripted_gains[gain_index] = "None";
            else if (Dm.in_to(sw__1, 1, 24))
                transcripted_gains[gain_index] = "Little";
            else if (Dm.in_to(sw__1, 25, 49))
                transcripted_gains[gain_index] = "Moderate";
            else if (Dm.in_to(sw__1, 50, 99))
                transcripted_gains[gain_index] = "Significant";
            else if (Dm.in_to(sw__1, 100, INFINITY))
                transcripted_gains[gain_index] = "Huge";
            else
                transcripted_gains[gain_index] = "Undefined";
        }
        gist["gains"] = transcripted_gains;
        gist["title"] = title;
        gist["author"] = author;
        gist["etAlia"] = et_alia;
        gist["abstract"] = @abstract;
        gist["experimentName"] = experiment_path == null ? null : Dm.initial(experiment_path, (datum_experiment_ordnance p) => p.name);
        gist["tier"] = tier;
        gist["partner"] = partner_path == null ? null : Dm.initial(partner_path, (datum_scientific_partner p) => p.name);
        return gist;
    }

    // tg: code/modules/research/ordnance/_scipaper.dm:181
    public virtual void autofill()
    {
        if (!Dm.truthy(title))
            title = I18n.T("On [] - []", Dm.initial(experiment_path, (datum_experiment_ordnance p) => p.name), tier);
        if (!Dm.truthy(author))
        {
            author = I18n.T("Unknown");
            et_alia = FALSE;
        }
        if (!Dm.truthy(@abstract))
            @abstract = I18n.T("Published on [] (PT: [])", server_timestamp(ic_time: TRUE), round_timestamp());
    }
}

// tg: code/modules/research/ordnance/_scipaper.dm:190
public partial class datum_scientific_paper_explosive : datum_scientific_paper
{
    /// Check if our explosion has already been published and whether the experiment path is correct or not.
    // tg: code/modules/research/ordnance/_scipaper.dm:198
    public override object allowed_to_publish(datum_techweb techweb_to_check = null)
    {
        if (!ispath(experiment_path, typeof(datum_experiment_ordnance_explosive)))
            return FALSE;
        if (!istype(explosion_record, typeof(datum_data_tachyon_record)))
            return FALSE;
        foreach ((object experiment_path, object papers) in Dm.pairs(techweb_to_check.published_papers))
        {
            foreach (datum_scientific_paper_explosive paper in Dm.each<datum_scientific_paper_explosive>(papers))
            {
                datum_data_tachyon_record record_to_check = paper.explosion_record;
                if (Dm.eq(explosion_record.explosion_identifier, record_to_check.explosion_identifier))
                    return FALSE;
            }
        }
        return base.allowed_to_publish(techweb_to_check);
    }

    // tg: code/modules/research/ordnance/_scipaper.dm:210
    public override void set_experiment(object ex_path = null, object variable = null, object data = null)
    {
        if (!ispath(ex_path, typeof(datum_experiment_ordnance_explosive)) || !Dm.truthy(variable) || !istype(data, typeof(datum_data_tachyon_record)))
        {
            experiment_path = null;
            tracked_variable = null;
            explosion_record = null;
        }
        else
        {
            experiment_path = ex_path as Type;
            tracked_variable = variable;
            explosion_record = data as datum_data_tachyon_record;
        }

        set_tier();
        set_partner();
    }

    // tg: code/modules/research/ordnance/_scipaper.dm:223
    public override object clone_into(object typepath = null)
    {
        if (!Dm.eq(typepath, type))
            return base.clone_into(typepath);

        datum_scientific_paper_explosive new_paper = base.clone_into(typepath) as datum_scientific_paper_explosive;
        new_paper.explosion_record = explosion_record;
        return new_paper;
    }
}

// tg: code/modules/research/ordnance/_scipaper.dm:231
public partial class datum_scientific_paper_gaseous : datum_scientific_paper
{
    /**
     * Check if our record datum is a duplicate or no.
     * No index number is necessary because compressor records cant be replicated.
     * Also checks the experiment path.
     */
    // tg: code/modules/research/ordnance/_scipaper.dm:239
    public override object allowed_to_publish(datum_techweb techweb_to_check = null)
    {
        if (!ispath(experiment_path, typeof(datum_experiment_ordnance_gaseous)))
            return FALSE;
        if (!istype(compressor_record, typeof(datum_data_compressor_record)))
            return FALSE;
        foreach ((object experiment_path, object papers) in Dm.pairs(techweb_to_check.published_papers))
        {
            foreach (datum_scientific_paper_gaseous paper in Dm.each<datum_scientific_paper_gaseous>(papers))
            {
                if (compressor_record == paper.compressor_record)
                    return FALSE;
            }
        }
        return base.allowed_to_publish(techweb_to_check);
    }

    // tg: code/modules/research/ordnance/_scipaper.dm:250
    public override void set_experiment(object ex_path = null, object variable = null, object data = null)
    {
        if (!ispath(ex_path, typeof(datum_experiment_ordnance_gaseous)) || !istype(data, typeof(datum_data_compressor_record)) || !Dm.truthy(variable))
        {
            experiment_path = null;
            tracked_variable = null;
            compressor_record = null;
        }
        else
        {
            experiment_path = ex_path as Type;
            tracked_variable = variable;
            compressor_record = data as datum_data_compressor_record;
        }

        set_tier();
        set_partner();
    }

    // tg: code/modules/research/ordnance/_scipaper.dm:263
    public override object clone_into(object typepath = null)
    {
        if (!Dm.eq(typepath, type))
            return base.clone_into(typepath);

        datum_scientific_paper_gaseous new_paper = base.clone_into(typepath) as datum_scientific_paper_gaseous;
        new_paper.compressor_record = compressor_record;
        return new_paper;
    }
}

/// Various informations on companies/scientific programs/journals etc that the players can sign on to.
// tg: code/modules/research/ordnance/_scipaper.dm:272
public partial class datum_scientific_partner : datum
{
    // tg: code/modules/research/ordnance/_scipaper.dm:284
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        // Convey boosts to their associated nodes so that they can then be passed
        // to techweb UIs as static data.
        foreach (object node_path in Dm.each(boostable_nodes))
        {
            object discount_amount = boostable_nodes[node_path];
            datum_techweb_node node = SSresearch.techweb_nodes[node_path] as datum_techweb_node;
            LAZYSET(ref node.discount_boosts, TECHWEB_POINT_TYPE_GENERIC, discount_amount);
        }
    }

    // tg: code/modules/research/ordnance/_scipaper.dm:292
    public virtual object purchase_boost(datum_techweb purchasing_techweb = null, datum_techweb_node node = null)
    {
        object possible_boost = allowed_to_boost(purchasing_techweb, node.type);
        if (!Dm.truthy(possible_boost))
            return FALSE;
        object boost_amount = boostable_nodes[node.type];

        purchasing_techweb.boost_techweb_node(node, Dm.list(Dm.kv(TECHWEB_POINT_TYPE_GENERIC, boost_amount)));
        purchasing_techweb.scientific_cooperation[type] = Dm.num(purchasing_techweb.scientific_cooperation[type]) - Dm.num(boost_amount) * SCIENTIFIC_COOPERATION_PURCHASE_MULTIPLIER;
        if (Dm.eq(possible_boost, SCIPAPER_ALREADY_BOUGHT)) // Refund the original price
        {
            purchasing_techweb.adjust_points(TECHWEB_POINT_TYPE_GENERIC, boost_amount);
            return SCIPAPER_ALREADY_BOUGHT;
        }
        return TRUE;
    }

    // tg: code/modules/research/ordnance/_scipaper.dm:305
    public virtual object allowed_to_boost(datum_techweb purchasing_techweb = null, object node_path = null)
    {
        datum_techweb_node boosting_node = SSresearch.techweb_nodes[node_path] as datum_techweb_node;
        object boost_amount = boostable_nodes[node_path];

        if (Dm.num(purchasing_techweb.scientific_cooperation[type]) < Dm.num(boost_amount) * SCIENTIFIC_COOPERATION_PURCHASE_MULTIPLIER) // Too expensive
            return FALSE;
        if (Dm.truthy(purchasing_techweb.boosted_nodes[node_path]) && Dm.num(boosting_node.discount_boosts[TECHWEB_POINT_TYPE_GENERIC]) >= Dm.num(boost_amount)) // Already bought or we have a bigger discount
            return FALSE;
        if (Dm.truthy(purchasing_techweb.researched_nodes[node_path]))
            return SCIPAPER_ALREADY_BOUGHT;
        if (!Dm.truthy((purchasing_techweb.get_available_nodes() as DmList)[node_path])) // Not currently available
            return FALSE;
        return TRUE;
    }
}
