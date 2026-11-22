namespace Anomaly.Tools.Types;

/// <summary>The tracker's totals (types-converter.md 6): read from the checked-in types-index.tsv, no clone needed.</summary>
internal static class Summary
{
    public static int Run(string repo, TextWriter log)
    {
        string file = Path.Combine(repo, Generator.IndexPath);
        if (!File.Exists(file)) throw new ToolException($"no {Generator.IndexPath} under {repo}");
        var all = new Totals();
        var meta = new Totals();
        var globals = new Totals();
        var metaTypes = new HashSet<string>(StringComparer.Ordinal);
        var handInert = new List<string>();
        foreach (string line in File.ReadLines(file))
        {
            if (line.Length == 0 || line[0] == '#') continue;
            string[] f = line.Split('\t');
            if (f[0] == "type")
            {
                bool inMeta = f[7] == "1";
                if (inMeta) metaTypes.Add(f[1]);
                all.AddType(f[4]);
                if (inMeta) meta.AddType(f[4]);
                if (f[6] == "1" && f[4] == "inert") handInert.Add(f[1]);
            }
            else if (f[0] == "proc")
            {
                if (f[1] == "/") { globals.AddProc(f[5], f[6]); continue; }
                all.AddProc(f[5], f[6]);
                if (metaTypes.Contains(f[1])) meta.AddProc(f[5], f[6]);
            }
        }
        log.WriteLine("types-index.tsv totals (types-converter.md 6)");
        all.Print(log, "all types");
        meta.Print(log, "Meta closure (MetaStation's types and their ancestors)");
        log.WriteLine($"  global procs: {globals.Procs}: {globals.Yes} declared ({globals.Full} full, {globals.Partial} partial, {globals.Deferred} deferred, {globals.Skeleton} skeletons), {globals.No} not declared");
        log.WriteLine($"  hand-written but inert (types-converter.md 4.2): {handInert.Count}" + (handInert.Count > 0 ? ": " + string.Join(", ", handInert.Take(20)) : ""));
        return 0;
    }

    private sealed class Totals
    {
        public int Types, Base, Live, Inert, None, Procs, Yes, Stub, No, Full, Partial, Deferred, Skeleton;

        public void AddType(string state)
        {
            Types++;
            switch (state)
            {
                case "base": Base++; break;
                case "live": Live++; break;
                case "inert": Inert++; break;
                default: None++; break;
            }
        }

        public void AddProc(string translated, string body)
        {
            Procs++;
            switch (translated)
            {
                case "yes": Yes++; break;
                case "stub": Stub++; break;
                default: No++; break;
            }
            switch (body)
            {
                case "full": Full++; break;
                case "partial": Partial++; break;
                case "deferred": Deferred++; break;
                case "skeleton": Skeleton++; break;
            }
        }

        public void Print(TextWriter log, string what)
        {
            log.WriteLine($"  {what}: {Types} types: {Base + Live} live ({Base} live base, {Live} live by the rule), {Inert} inert, {None} without a class");
            log.WriteLine($"    procs: {Procs}: {Yes} declared ({Full} translated, {Partial} partly translated, {Deferred} deferred leaves, {Skeleton} NotTranslated skeletons), {Stub} live-partial stubs, {No} not declared");
        }
    }
}
