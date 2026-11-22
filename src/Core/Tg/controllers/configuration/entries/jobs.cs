// Translated from tgstation (snapshot 1d99bd7f): code/controllers/configuration/entries/jobs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// SKELETON: holds the [DmPath] of /datum/controller/subsystem/job, which jobs.dm declares first in .dme order (rules 2.3),
// and the proc of jobs.dm that configuration.dm calls (Load, :115-116: on a config reload after SSjob initialized);
// M4a P5 translated generate_config_singletons for SSjob's Initialize.
// Owned by the atoms package (E) in wave 3 with the job stub (controllers/subsystem/job.cs); jobs.dm is translated with the
// jobs in M10 (W3.0 contracts).

// tg: code/controllers/configuration/entries/jobs.dm:4
[DmPath("/datum/controller/subsystem/job")]
public partial class datum_controller_subsystem_job : datum_controller_subsystem
{
    /// Returns an associated list of all of the job config types that we have in the codebase.
    // tg: code/controllers/configuration/entries/jobs.dm:4  (M4a P5: SSjob's Initialize)
    public virtual object generate_config_singletons()
    {
        DmList returnable_list = Dm.list();
        foreach (Type config_datum in Dm.each(subtypesof(typeof(datum_job_config_type))))
            returnable_list[Dm.initial(config_datum, (datum_job_config_type p) => p.name)] = Dm.@new(config_datum);

        return returnable_list;
    }

    /// Sets all of the job datum configurable values to what they've been set to in the config file, jobconfig.toml.
    // tg: code/controllers/configuration/entries/jobs.dm:12
    public virtual void load_jobs_from_config(float? silent = null)
    {
        silent ??= FALSE;
        if (!Dm.truthy(length(job_config_datum_singletons)))
        {
            stack_trace("SSjob tried to load jobs from config, but the config singletons were not initialized! Likely tried to load jobs before SSjob was initialized.", "code/controllers/configuration/entries/jobs.dm", 14);
            return;
        }

        if (Dm.truthy(legacy_mode))
        {
            legacy_load();
            return;
        }

        // var/list/job_config = rustg_read_toml_file("[global.config.directory]/jobconfig.toml") and each job's config values (:22-34):
        // DEVIATION(DV181), the TOML is not read (validate_job_config); only the legacy jobs.txt sets the jobs
    }

    /// Operates the legacy jobs.txt parser to load jobs from the old config system.
    // tg: code/controllers/configuration/entries/jobs.dm:37
    public virtual void legacy_load()
    {
        object jobsfile = Dm.file(Dm.text("[]/jobs.txt", config.directory));
        if (!Dm.truthy(fexists(jobsfile))) // sanity with a trace
        {
            stack_trace("Despite SSconfig setting SSjob.legacy_mode to TRUE, jobs.txt was not found in the config directory! Something has gone terribly wrong!", "code/controllers/configuration/entries/jobs.dm", 40);
            return;
        }
        string jobstext = file2text(jobsfile);
        foreach (datum_job occupation in Dm.each<datum_job>(joinable_occupations))
        {
            // var/regex/parser = new("[occupation.title]=(-1|\\d+),(-1|\\d+)"); parser.Find(jobstext): the regex (a shim gap) is its
            // pattern: the title, "=", and two numbers that are -1 or digits, separated by a comma
            (object total, object spawn) = LegacyJobPositions(jobstext, Dm.str(occupation.title));
            if (total == null)
                continue; // DEVIATION(DV181): a job jobs.txt does not list keeps its defaults (tg's failed Find leaves parser.group null: a runtime)
            occupation.total_positions = Dm.num(text2num(total));
            occupation.spawn_positions = Dm.num(text2num(spawn));
        }
    }

    // The match of tg's "[title]=(-1|\d+),(-1|\d+)" in jobs.txt: its two groups, or nulls when no line matches.
    private static (object, object) LegacyJobPositions(string text, string title)
    {
        static int Number(string s, int at)
        {
            if (at < s.Length && s[at] == '-')
                return at + 1 < s.Length && s[at + 1] == '1' ? at + 2 : -1;
            int end = at;
            while (end < s.Length && char.IsAsciiDigit(s[end]))
                end++;
            return end > at ? end : -1;
        }
        for (int i = text.IndexOf(title + "=", StringComparison.Ordinal); i >= 0; i = text.IndexOf(title + "=", i + 1, StringComparison.Ordinal))
        {
            int first = i + title.Length + 1;
            int firstEnd = Number(text, first);
            if (firstEnd < 0 || firstEnd >= text.Length || text[firstEnd] != ',')
                continue;
            int secondEnd = Number(text, firstEnd + 1);
            if (secondEnd < 0)
                continue;
            return (text[first..firstEnd], text[(firstEnd + 1)..secondEnd]);
        }
        return (null, null);
    }
}
