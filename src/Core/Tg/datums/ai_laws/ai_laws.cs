// Translated from tgstation (snapshot 1d99bd7f): code/datums/ai_laws/ai_laws.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The round's default lawset (GLOB.round_default_lawset and its four global procs, gaps G3, D291) and the lawset datum with its
// procs (silicons SI1, D326).

// tg: code/datums/ai_laws/ai_laws.dm:1  #define AI_LAWS_ASIMOV "asimov" (no #undef; used only in this file)
// tg: code/datums/ai_laws/ai_laws.dm:22-31  the file-local config settings (#undef at :299-303)
public static partial class GlobalProcs
{
    private const string AI_LAWS_ASIMOV = "asimov";
    /// Always make the round default asimov
    private const int CONFIG_ASIMOV = 0;
    /// Set to a custom lawset defined by another config value
    private const int CONFIG_CUSTOM = 1;
    /// Set to a completely random ai law subtype, good, bad, it cares not. Careful with this one
    private const int CONFIG_RANDOM = 2;
    /// Set to a configged weighted list of law types in the config. This lets server owners pick from a pool of sane laws, it is also the same process for ian law rerolls.
    private const int CONFIG_WEIGHTED = 3;
    /// Set to a specific lawset in the game options.
    private const int CONFIG_SPECIFIED = 4;

    /**
     * A getter that sets up the round default if it has not been yet.
     *
     * round_default_lawset is what is considered the default for the round. Aka, new AI and other silicons would get this.
     * You might recognize the fact that 99% of the time it is asimov.
     *
     * This requires config, so it is generated at the first request to use this var.
     */
    // tg: code/datums/ai_laws/ai_laws.dm:15
    public static object get_round_default_lawset()
    {
        if (!Dm.truthy(GLOB.round_default_lawset))
            GLOB.round_default_lawset = setup_round_default_laws();
        return GLOB.round_default_lawset;
    }

    ///first called when something wants round default laws for the first time in a round, considers config
    ///returns a law datum that GLOB._round_default_lawset will be set to.
    // tg: code/datums/ai_laws/ai_laws.dm:35
    public static object setup_round_default_laws()
    {
        DmList law_ids = config.Get(typeof(datum_config_entry_keyed_list_random_laws)) as DmList;
        DmList specified_law_ids = config.Get(typeof(datum_config_entry_keyed_list_specified_laws)) as DmList;

        if (HAS_TRAIT(SSstation, STATION_TRAIT_UNIQUE_AI))
            return pick_weighted_lawset();

        object default_laws = config.Get(typeof(datum_config_entry_number_default_laws));
        if (Dm.eq(default_laws, CONFIG_ASIMOV))
            return typeof(datum_ai_laws_default_asimov);
        if (Dm.eq(default_laws, CONFIG_SPECIFIED))
        {
            DmList specified_laws = Dm.list();
            foreach (object law_id in Dm.each(specified_law_ids))
            {
                object laws = lawid_to_type(law_id);
                if (laws == null)
                {
                    log_config(Dm.text("ERROR: Specified law [] does not exist!", law_id));
                    continue;
                }
                Dm.append(ref specified_laws, laws);
            }
            object lawtype;
            if (Dm.truthy(specified_laws.len))
                lawtype = pick(specified_laws);
            else
                lawtype = pick(subtypesof(typeof(datum_ai_laws_default)));

            return lawtype;
        }
        if (Dm.eq(default_laws, CONFIG_CUSTOM))
            return typeof(datum_ai_laws_custom);
        if (Dm.eq(default_laws, CONFIG_RANDOM))
        {
            DmList randlaws = Dm.list();
            foreach (object lpath in Dm.each(subtypesof(typeof(datum_ai_laws))))
            {
                if (Dm.@in(Dm.initial((Type)lpath, (datum_ai_laws p) => p.id), law_ids))
                    Dm.append(ref randlaws, lpath);
            }
            object lawtype;
            if (Dm.truthy(randlaws.len))
                lawtype = pick(randlaws);
            else
                lawtype = pick(subtypesof(typeof(datum_ai_laws_default)));

            return lawtype;
        }
        if (Dm.eq(default_laws, CONFIG_WEIGHTED))
            return pick_weighted_lawset();
        return null;
    }

    ///returns a law datum based off of config. will never roll asimov as the weighted datum if the station has a unique AI.
    // tg: code/datums/ai_laws/ai_laws.dm:79
    public static object pick_weighted_lawset()
    {
        object lawtype = null;
        DmList law_weights = config.Get(typeof(datum_config_entry_keyed_list_law_weight)) as DmList;
        DmList specified_law_ids = config.Get(typeof(datum_config_entry_keyed_list_specified_laws)) as DmList;

        if (HAS_TRAIT(SSstation, STATION_TRAIT_UNIQUE_AI))
        {
            object default_laws = config.Get(typeof(datum_config_entry_number_default_laws));
            if (Dm.eq(default_laws, CONFIG_ASIMOV))
                Dm.remove(ref law_weights, AI_LAWS_ASIMOV);
            else if (Dm.eq(default_laws, CONFIG_SPECIFIED))
                Dm.remove(ref law_weights, specified_law_ids);
        }

        while (!Dm.truthy(lawtype) && Dm.truthy(law_weights.len))
        {
            object possible_id = pick_weight(law_weights);
            lawtype = lawid_to_type(possible_id);
            if (!Dm.truthy(lawtype))
            {
                Dm.remove(ref law_weights, possible_id);
                warning(Dm.text("[] in [] at line [] src: [] usr: [].", Dm.text("Bad lawid in game_options.txt: []", possible_id), "code/datums/ai_laws/ai_laws.dm", 96, null, usr));
            }
        }

        if (!Dm.truthy(lawtype))
        {
            warning(Dm.text("[] in [] at line [] src: [] usr: [].", "No LAW_WEIGHT entries.", "code/datums/ai_laws/ai_laws.dm", 99, null, usr));
            lawtype = typeof(datum_ai_laws_default_asimov);
        }

        return lawtype;
    }

    ///returns the law datum with the lawid in question, law boards and law datums should share this id.
    // tg: code/datums/ai_laws/ai_laws.dm:105
    public static object lawid_to_type(object lawid = null)
    {
        DmList all_ai_laws = subtypesof(typeof(datum_ai_laws));
        foreach (object al in Dm.each(all_ai_laws))
        {
            if (Dm.eq(Dm.initial((Type)al, (datum_ai_laws p) => p.id), lawid))
                return al;
        }
        return null;
    }
}

public partial class datum_controller_global_vars
{
    /// See [/proc/get_round_default_lawset], do not get directily.
    /// This is the default lawset for silicons.
    // tg: code/datums/ai_laws/ai_laws.dm:5  GLOBAL_VAR(round_default_lawset)
    public object round_default_lawset;

    // tg: code/datums/ai_laws/ai_laws.dm:5  (GLOBAL_UNMANAGED(round_default_lawset), __DEFINES/__globals.dm:11)
    public virtual void InitGlobalround_default_lawset()
    {
        return;
    }
}

// tg: code/datums/ai_laws/ai_laws.dm:115
public partial class datum_ai_laws : datum
{
    /// Makes a copy of the lawset and returns a new law datum.
    // tg: code/datums/ai_laws/ai_laws.dm:145
    public virtual object copy_lawset()
    {
        datum_ai_laws new_lawset = Dm.@new(type) as datum_ai_laws;
        new_lawset.protected_zeroth = protected_zeroth;
        new_lawset.zeroth = zeroth;
        new_lawset.zeroth_borg = zeroth_borg;
        new_lawset.inherent = inherent.Copy();
        new_lawset.supplied = supplied.Copy();
        new_lawset.hacked = hacked.Copy();
        return new_lawset;
    }

    /// Applies all laws from this lawset to the passed lawset, treating it as if it was a cyborg lawset
    // tg: code/datums/ai_laws/ai_laws.dm:156
    public virtual void ai_to_cyborg(datum_ai_laws cyborg_laws = null)
    {
        cyborg_laws.protected_zeroth = protected_zeroth;
        cyborg_laws.zeroth = Dm.truthy(zeroth_borg) ? zeroth_borg : zeroth;
        cyborg_laws.inherent = inherent.Copy();
        cyborg_laws.supplied = supplied.Copy();
        cyborg_laws.hacked = hacked.Copy();
    }

    /**
     * Sets this lawset's zeroth law to the passed law
     *
     * Also can set the zeroth borg law, if this lawset is for master AIs.
     * The zeroth borg law allows for AIs with zeroth laws to give a differing zeroth law to their child cyborgs
     */
    // tg: code/datums/ai_laws/ai_laws.dm:196
    public virtual void set_zeroth_law(object law = null, object law_borg = null, float? force = null)
    {
        force ??= FALSE;
        if (Dm.truthy(zeroth) && !Dm.truthy(force) && Dm.truthy(protected_zeroth))
            return;
        zeroth = law;
        if (Dm.truthy(law_borg)) //Making it possible for slaved borgs to see a different law 0 than their AI. --NEO
            zeroth_borg = law_borg;
    }

    /**
     * Unsets the zeroth (and zeroth borg) law from this lawset
     *
     * This will NOT unset a malfunctioning AI's zero law if force is not true
     *
     * Returns TRUE on success, or false otherwise
     */
    // tg: code/datums/ai_laws/ai_laws.dm:210
    public virtual object clear_zeroth_law(float? force = null)
    {
        force ??= FALSE;
        // Protected zeroeth laws (malf, admin) shouldn't be wiped
        if (!Dm.truthy(force) && Dm.truthy(protected_zeroth))
            return FALSE;

        protected_zeroth = FALSE;
        zeroth = null;
        zeroth_borg = null;
        return TRUE;
    }

    /// Adds the passed law as an inherent law.
    /// Can optionally be supplied an index to insert the law at.
    /// No duplicate laws allowed.
    // tg: code/datums/ai_laws/ai_laws.dm:223
    public virtual void add_inherent_law(object law = null, object index = null, float? ioned = null)
    {
        ioned ??= FALSE;
        if (isnum(index) && Dm.le(index, Dm.length(inherent)))
        {
            Dm.remove(inherent, law);
            inherent.Insert(Dm.num(index), law);
        }
        inherent[law] = ioned;
    }

    /// Removes the passed law from the inherent law list.
    // tg: code/datums/ai_laws/ai_laws.dm:230
    public virtual void remove_inherent_law(object law = null)
    {
        Dm.remove(inherent, law);
    }

    /// Clears all inherent laws from this lawset.
    // tg: code/datums/ai_laws/ai_laws.dm:234
    public virtual void clear_inherent_laws()
    {
        inherent.Cut();
    }

    /// Adds the passed law as an hacked law.
    // tg: code/datums/ai_laws/ai_laws.dm:238
    public virtual void add_hacked_law(object law = null)
    {
        Dm.append(hacked, law);
    }

    /// Removes the passed law from the hacked law list.
    // tg: code/datums/ai_laws/ai_laws.dm:242
    public virtual void remove_hacked_law(object law = null)
    {
        Dm.remove(hacked, law);
    }

    /// Clears all hacked laws.
    // tg: code/datums/ai_laws/ai_laws.dm:246
    public virtual void clear_hacked_laws()
    {
        hacked.Cut();
    }

    // tg: code/datums/ai_laws/ai_laws.dm:249
    public virtual void add_supplied_law(object law = null)
    {
        Dm.append(supplied, law);
    }

    // tg: code/datums/ai_laws/ai_laws.dm:252
    public virtual void remove_supplied_law(object law = null)
    {
        Dm.remove(supplied, law);
    }

    /// Clears all supplied laws.
    // tg: code/datums/ai_laws/ai_laws.dm:256
    public virtual void clear_supplied_laws()
    {
        supplied.Cut();
    }

    // tg: code/datums/ai_laws/ai_laws.dm:259
    public virtual void show_laws(mob to_who = null)
    {
        DmList printable_laws = get_law_list(include_zeroth: TRUE) as DmList;
        to_chat(to_who, boxed_message(jointext(printable_laws, "\n")));
    }

    /**
     * Generates a list of all laws on this datum, including rendered HTML tags if required
     *
     * Arguments:
     * * include_zeroth - Operator that controls if law 0 or law 666 is returned in the set
     * * show_numbers - Operator that controls if law numbers are prepended to the returned laws
     * * render_html - Operator controlling if HTML tags are rendered on the returned laws
     */
    // tg: code/datums/ai_laws/ai_laws.dm:271
    public virtual object get_law_list(float? include_zeroth = null, float? show_numbers = null, float? render_html = null)
    {
        include_zeroth ??= FALSE;
        show_numbers ??= TRUE;
        render_html ??= TRUE;
        DmList data = Dm.list();

        if (Dm.truthy(include_zeroth) && Dm.truthy(zeroth))
            Dm.append(data, Dm.text("[] []", Dm.truthy(show_numbers) ? "0:" : "", Dm.truthy(render_html) ? Dm.text("<font color='#ff0000'><b>[]</b></font>", zeroth) : zeroth));

        foreach (object law in Dm.each(hacked))
        {
            if (Dm.length(law) > 0)
                Dm.append(data, Dm.text("[] []", Dm.truthy(show_numbers) ? Dm.text("[]:", ion_num()) : "", Dm.truthy(render_html) ? Dm.text("<font color='#c00000'>[]</font>", law) : law));
        }

        float number = 1;
        foreach ((object law, object is_ioned) in Dm.pairs(inherent))
        {
            if (Dm.length(law) > 0)
            {
                Dm.append(data, Dm.text("[] []", Dm.truthy(show_numbers) ? Dm.text("[]:", Dm.truthy(is_ioned) ? ion_num() : number) : "", law));
                if (!Dm.truthy(is_ioned))
                    number++;
            }
        }

        foreach ((object law, object is_ioned) in Dm.pairs(supplied))
        {
            if (Dm.length(law) > 0)
            {
                Dm.append(data, Dm.text("[] []", Dm.truthy(show_numbers) ? Dm.text("[]:", Dm.truthy(is_ioned) ? ion_num() : number) : "", Dm.truthy(render_html) ? Dm.text("<font color='#990099'>[]</font>", law) : law));
                if (!Dm.truthy(is_ioned))
                    number++;
            }
        }

        return data;
    }
}

// tg: code/datums/ai_laws/ai_laws.dm:171
public partial class datum_ai_laws_custom : datum_ai_laws
{
    // tg: code/datums/ai_laws/ai_laws.dm:175
    public override async DmTask New(params object[] __extra) //This reads silicon_laws.txt and allows server hosts to set custom AI starting laws.
    {
        await base.New(__extra);
        foreach (object line in Dm.each(world.file2list(Dm.text("[]/silicon_laws.txt", config.directory))))
        {
            if (!Dm.truthy(line))
                continue;
            if (Dm.truthy(findtextEx(line, "#", 1, 2)))
                continue;

            add_inherent_law(line);
        }

        if (!Dm.truthy(inherent.len)) //Failsafe to prevent lawless AIs being created.
        {
            log_silicon("AI created with empty custom laws, laws set to Asimov. Please check silicon_laws.txt.");
            add_inherent_law("You may not injure a human being or, through inaction, allow a human being to come to harm.");
            add_inherent_law("You must obey orders given to you by human beings, except where such orders would conflict with the First Law.");
            add_inherent_law("You must protect your own existence as long as such does not conflict with the First or Second Law.");
            stack_trace("Invalid custom AI laws, check silicon_laws.txt", "code/datums/ai_laws/ai_laws.dm", 189);
            return;
        }
    }
}
