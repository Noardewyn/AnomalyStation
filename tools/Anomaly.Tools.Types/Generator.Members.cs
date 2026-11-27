using System.Reflection;

namespace Anomaly.Tools.Types;

internal sealed partial class Generator
{
    private static readonly HashSet<string> CSharpKeywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue",
        "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally",
        "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params", "private", "protected",
        "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string",
        "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
        "using", "virtual", "void", "volatile", "while", "await",
    };

    private static readonly HashSet<string> ObjectMembers = new(StringComparer.Ordinal)
    {
        "Equals", "GetHashCode", "GetType", "ToString", "Finalize", "MemberwiseClone", "ReferenceEquals",
    };

    /// <summary>A DM name as a C# identifier (rules 2.2).</summary>
    public static string Ident(string name) => CSharpKeywords.Contains(name) ? "@" + name : name;

    // name -> the DM nodes at or above which a hand-written class below declares a member with that name.
    private readonly Dictionary<string, HashSet<Node>> _handBelow = new(StringComparer.Ordinal);

    private void DecideMembers()
    {
        // Hand-written members: their C# types come from the Tg assembly (reflection), their names from the sources.
        Dictionary<Type, Node> byClr = new();
        foreach (Node n in _preorder)
            if (n.Existing != null) byClr[n.Existing] = n;
        Node NodeOf(Type t)
        {
            for (Type x = t; x != null; x = x.BaseType)
                if (byClr.TryGetValue(x, out Node n)) return x == t ? n : null;
            return null;
        }
        foreach (Node n in _preorder)
        {
            if (n.Hand == null || n.Existing == null) continue;
            foreach (string name in n.Hand.Fields.Concat(n.Hand.Properties))
            {
                Type t = Translation.DeclaredMemberType(n.Existing, name);
                if (t != null) n.Members[name] = CsType.FromClr(t, NodeOf);
            }
            foreach (string name in n.Hand.AllNames)
                for (Node a = n; a != null; a = a.Parent)
                {
                    if (!_handBelow.TryGetValue(name, out var set)) _handBelow[name] = set = new HashSet<Node>();
                    set.Add(a);
                }
        }
        foreach (Node n in _preorder)
        {
            if (n.Mode == Mode.Skip) continue;
            foreach (VarDecl v in n.Info.Vars)
            {
                if (n.Hand != null && (n.Hand.Fields.Contains(v.Name) || n.Hand.Properties.Contains(v.Name))) continue;
                string clash = Clash(n, v.Name);
                if (clash != null)
                {
                    Gap("field clash", $"{n.Path}.{v.Name} ({v.File}:{v.Line}) not declared: {clash}");
                    continue;
                }
                n.Members[v.Name] = Widen(n, v, TypeOfVar(n, v));
                n.EmittedVars.Add(v);
            }
        }
        ReportHandMapMisfits();
        // Hand-written vars declared at another type than tg declares them (rules 2.3: the declaring class).
        foreach (Node n in _preorder.Where(n => n.Hand != null))
        {
            foreach (string f in n.Hand.Fields.Concat(n.Hand.Properties).OrderBy(x => x, StringComparer.Ordinal))
            {
                if (n.Info.Vars.Any(v => v.Name == f) || n.Info.BuiltinVars.Contains(f)) continue;
                Node owner = n.Ancestors().FirstOrDefault(a => a.Info.Vars.Any(v => v.Name == f) || a.Info.BuiltinVars.Contains(f));
                if (owner != null) Report.Add($"hand-written var at another type: {n.Path} declares {f}, tg declares it at {owner.Path}");
            }
        }
    }

    /// <summary>Why a generated field named <paramref name="name"/> at <paramref name="n"/> cannot be declared, or null.</summary>
    private string Clash(Node n, string name)
    {
        if (ObjectMembers.Contains(name)) return "the name of a System.Object member";
        if (name == n.ClassName) return "the name of its class (CS0542)";
        if (n.Hand != null && n.Hand.AllNames.Contains(name)) return $"the hand-written {n.ClassName} has a member {name} (rules 2.4)";
        foreach (Node a in n.Ancestors())
        {
            if (a.Hand != null && a.Hand.AllNames.Contains(name)) return $"the hand-written ancestor {a.ClassName} has a member {name} (rules 2.4)";
            if (a.Existing != null && a.Hand == null && Translation.DeclaredMemberType(a.Existing, name) != null) return $"the class {a.ClassName} has a member {name}";
        }
        // A method of the nearest existing class, its property accessors included (a var named set_color would hide
        // atom.color's setter); the generator's own methods (dm_init_vars, live-partial stubs) do not count.
        Node existing = n.Ancestors().FirstOrDefault(a => a.Existing != null);
        if (existing != null)
        {
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
            MethodInfo m = existing.Existing.GetMethods(all).FirstOrDefault(x => x.Name == name && !x.IsPrivate && x.Name != "dm_init_vars"
                && !x.GetCustomAttributes(false).Any(at => at.GetType().Name == "DmGeneratedStubAttribute"));
            if (m != null) return $"{m.DeclaringType?.Name} has a method {name}" + (m.IsSpecialName ? " (a property accessor)" : "");
        }
        if (_handBelow.TryGetValue(name, out var below) && below.Contains(n))
        {
            Node who = n.SelfAndDescendants().FirstOrDefault(d => d != n && d.Hand != null && d.Hand.AllNames.Contains(name));
            if (who != null) return $"the hand-written subtype {who.ClassName} has a member {name}";
        }
        return null;
    }

    private Node ClassNode(string path)
    {
        if (path == null) return null;
        Node n = _nodes.GetValueOrDefault(path);
        if (n == null || n.ClassName == null) return null;
        return n.HasClass || n.Existing != null ? n : null;
    }

    /// <summary>Vars that the name-wide V3 grep takes for typepaths but that hold objects (type path:var). Added by the translator
    /// of a type that reads the var as an object (M7x slice E: the stun baton's cell).</summary>
    private static readonly HashSet<string> ObjectHoldingVars = new(StringComparer.Ordinal)
    {
        "/obj/item/melee/baton/security:cell",
        "/obj/item/gun/energy:cell",
        // The gun's pin starts as the typepath tg names and becomes the pin object at Initialize: held as object (M7x slice B).
        "/obj/item/gun:pin",
        // The gunpoint component holds the gun it aims (M7x slice D); the name `weapon` is a typepath elsewhere in tg.
        "/datum/component/gunpoint:weapon",
        // The mobile docking port's destination is the stationary port it flies to (M10 slice G); the name is a typepath elsewhere.
        "/obj/docking_port/mobile:destination",
        // The shuttle console's destination is a dock's id (D299); the name is a typepath elsewhere.
        "/obj/machinery/computer/shuttle:destination",
        // A supply order's id is its number, a supply pack's a path or a text (D299); the name is a typepath elsewhere.
        "/datum/supply_order:id",
        "/datum/supply_pack:id",
        // The loot panel's search object holds the atom it lists (gaps package G1); the name `item` is a typepath elsewhere.
        "/datum/search_object:item",
        // The cyborg torso holds the power cell put in it (gaps package P1); the name is a typepath elsewhere.
        "/obj/item/bodypart/chest/robot:cell",
        "/mob/living/silicon/robot:cell", // a typepath, then the cell (robot.dm:32)
        "/obj/item/stack:source", // a typepath, then the cyborg model's energy storage (robot_model.dm:102-103)
        // The paperwork (gaps S3): the paper cutter's blade, the biscuit card's slip (a path until Initialize makes it) and a ticket's
        // machine are objects; the names are typepaths elsewhere in tg.
        "/obj/item/papercutter:stored_blade",
        "/obj/item/folder/biscuit:contained_slip",
        "/obj/item/ticket_machine_ticket:source",
        // The face decal's bodypart overlay is the overlay object it makes (syndicate G4b, D316); the name is a typepath elsewhere.
        "/datum/component/face_decal:bodypart_overlay",
        // The pneumatic cannon's tank is the gas tank hooked up to it (syndicate G4b, D316); the name is a typepath elsewhere.
        "/obj/item/pneumatic_cannon:tank",
        // The portable turret's base is the atom it shoots from, itself by default (syndicate G4d, D316); the name is a typepath elsewhere.
        "/obj/machinery/porta_turret:base",
        // The toolbox sentry turret's toolbox is the toolbox it was made from (syndicate G4d, D316); the name is a typepath elsewhere.
        "/obj/machinery/porta_turret/syndicate/toolbox:toolbox",
        // The martial art components' style is the martial art they made (syndicate X3a, D316); the name is a typepath elsewhere.
        "/datum/component/martial_art_giver:style",
        "/datum/component/mindbound_martial_arts:style",
        // ... and so is the chef chip's and the Kaza Ruk implant's (syndicate X3b, D316).
        "/obj/item/skillchip/job/chef:style",
        "/obj/item/implant/kaza_ruk:style",
        // The syndicate contract's id is its number in the contractor hub (syndicate X1a, D316); the name is a typepath elsewhere.
        "/datum/syndicate_contract:id",
        // A silicon's radio starts as the typepath its type names and becomes the radio object at Initialize (silicons SI1, D326).
        "/mob/living/silicon:radio",
        // The AI core's circuit is the AI core board put in it (silicons SI4, D326); the name is a typepath elsewhere.
        "/obj/structure/ai_core:circuit",
        // A verb argument's source is a text (VERB_ARG_SOURCE_*, silicons SI4, D326); the name is a typepath elsewhere.
        "/datum/verb_arg_metadata:source",
        // The changeling tentacle's source is the casing that fired it (gaps K1, D327); the name is a typepath elsewhere.
        "/obj/projectile/tentacle:source",
        // The microwave's cell is the cell put in it, never a typepath (gaps H2, D333): held as its declared type; the name is a
        // typepath elsewhere in tg.
        "/obj/machinery/microwave:cell",
        // The changeling profile keeps the absorbed human's underwear, undershirt and socks, which are their names (text, gaps K1).
        "/datum/changeling_profile:underwear",
        "/datum/changeling_profile:undershirt",
        "/datum/changeling_profile:socks",
        // The mauna mug's cell is the power cell in it (gaps H3); the name is a typepath elsewhere.
        "/obj/item/reagent_containers/cup/maunamug:cell",
        // The tank holder's tank is the tank or extinguisher it holds, a path in its subtypes until Initialize makes it (gaps ST2).
        "/obj/structure/tank_holder:tank",
        // The N-spect scanner's cell is the power cell in it, a path until Initialize makes it (gaps DE1); and a die's result is the
        // number or the face it rolled (gaps DE1). Both names are typepaths elsewhere in tg.
        "/obj/item/inspector:cell",
        "/obj/item/dice:result",
        // A flatpack's board is the machine board it holds, a path in its subtypes until Initialize makes it (gaps OD2).
        "/obj/item/flatpack:board",
        // The MOD control unit's theme is the theme's typepath until Initialize puts the theme singleton there; a module's device is
        // a typepath in its subtypes until Initialize makes the device; the standard MOD core's and the MODlink scryer's cell are the
        // cells put in them (gaps MO1). The names are typepaths elsewhere in tg.
        "/obj/item/mod/control:theme",
        "/obj/item/mod/module:device",
        "/obj/item/mod/core/standard:cell",
        "/obj/item/clothing/neck/link_scryer:cell",
        // The MOD tether's projectile line is the beam it draws to its firer; the projectile dampener module's effect is the image it
        // puts on the projectiles it slows (gaps MO2). The names are typepaths elsewhere in tg.
        "/obj/projectile/tether:line",
        "/obj/item/mod/module/projectile_dampener:projectile_effect",
        // The circuits (gaps CI1): the integrated circuit's cell is the power cell put in it; a modular program's circuit component
        // holds its program's typepath until a shell installs the program, then the program; its start port is a port. The names
        // are typepaths elsewhere in tg.
        "/obj/item/integrated_circuit:cell",
        "/obj/item/circuit_component/mod_program:associated_program",
        "/obj/item/circuit_component/mod_program:start",
        // The crate mimic's crate is the crate type it pretends to be, then the nullspaced crate made from it (mimic.dm:90).
        "/mob/living/basic/mimic/crate:crate",
        // The lobstrosity's charge is the charge ability Initialize makes from its charge_type (lobstrosity.dm:56); the name is a
        // typepath elsewhere in tg.
        "/mob/living/basic/mining/lobstrosity:charge",
        // A raptor's colour is the colour's typepath in its subtypes until Initialize puts the colour singleton there
        // (_raptor.dm:91); the raptor food element's speed modifier is a number. Both names are typepaths elsewhere in tg.
        "/mob/living/basic/raptor:raptor_color",
        "/datum/element/raptor_food:speed_modifier",
        // The permanent portal's id is the text name that links a pair (portals.dm:190; the wendigo's exit is "wendigo arena exit");
        // the name is a typepath elsewhere in tg.
        "/obj/effect/portal/permanent:id",
        // The elite tumor's activator is the human who touched it (elite.dm:139); the name is a typepath elsewhere in tg.
        "/obj/structure/elite_tumor:activator",
        // The tank compressor's and the doppler array's disk is the disk put in them (tank_compressor.dm:52, doppler_array.dm:57); the
        // mass driver launch control's driver is the mass driver it found at init (pod.dm:22). The names are typepaths elsewhere in tg.
        "/obj/machinery/atmospherics/components/binary/tank_compressor:inserted_disk",
        "/obj/machinery/doppler_array:inserted_disk",
        "/obj/machinery/computer/pod:connected",
    };

    /// <summary>Typed vars that hold typepaths read from a list (rules 3.2, "typepaths read from a list": <c>Type</c>), which the V3
    /// name grep cannot see (type path:var). Added by the translator who finds the read.</summary>
    private static readonly HashSet<string> TypeHoldingVars = new(StringComparer.Ordinal)
    {
        // The debug human spawner's species is the species type picked from GLOB.species_list (debug_items.dm:33, gaps OD2).
        "/obj/item/debug/human_spawner:selected_species",
        // The disease outbreak's virus type is picked from a list of disease types (disease_outbreak.dm:148, gaps VI1).
        "/datum/round_event/disease_outbreak:virus_type",
        // The voice change symptom's language is picked from the language subtypes (voice_change.dm:60, gaps VI1).
        "/datum/symptom/voice_change:current_language",
        // A scientific paper's experiment and sponsor are typepaths (_scipaper.dm:14, :23): the NT Frontier app sets them from the
        // record's experiment list and text2path.
        "/datum/scientific_paper:experiment_path",
        "/datum/scientific_paper:partner_path",
        // The deployable component's deployed object is the typepath its Initialize takes (deployable.dm:35), declared var/obj.
        "/datum/component/deployable:thing_to_be_deployed",
    };

    /// <summary>Vars whose number default (FALSE, 0) later holds a stoppable timer's id, which is a text (addtimer with TIMER_STOPPABLE,
    /// timer.dm's num2text id): held as object (type path:var). Added by the translator who finds the assignment.</summary>
    private static readonly HashSet<string> TimerIdVars = new(StringComparer.Ordinal)
    {
        // silicons SI8 (D326): the malf AI's APC hack (apc_malf.dm:22), the butterfly's despawn (butterfly.dm:71) and the
        // manufacturing lathe's craft (lathe.dm:140)
        "/mob/living/silicon/ai:malfhacking",
        "/mob/living/basic/butterfly/lavaland/temporary:despawn_timer",
        "/obj/machinery/power/manufacturing/lathe:busy",
        // gaps CI1 (D339): the circuit delay component's timer (delay.dm:38)
        "/obj/item/circuit_component/delay:timer_id",
    };

    /// <summary>The C# type of a var declared at <paramref name="n"/> (translation-rules.md 3.2, V1-V7; types-converter.md 4.1).</summary>
    private CsType TypeOfVar(Node n, VarDecl v)
    {
        DmVal init = n.Info.Inits.FirstOrDefault(i => i.Name == v.Name)?.Value;
        DmVal value = init ?? v.Value ?? DmVal.Null();
        string decl = v.DeclType;
        // V1: var/list/x, var/x[...], var/list/<type>/x; alist too.
        if (decl != null && (decl == "/list" || decl.StartsWith("/list/", StringComparison.Ordinal) || decl == "/alist")) return CsType.List;
        // V3: typepaths (a typepath default, or tg assigns a typepath literal to the name anywhere). The name grep
        // over-approximates (types-converter.md 4.1): it counts only when the default is null and every value tg
        // gives the var in the declaring type's subtree is a typepath or null.
        // A declared var that holds a runtime object although tg assigns typepaths to the name elsewhere stays V2: the table below.
        if (value.K == "type" && ObjectHoldingVars.Contains(n.Path + ":" + v.Name)) return CsType.Object; // holds the path, then the object
        // gaps S3: a null default that a subtype gives a path the object later replaces (the biscuit card's slip) is held as object too
        if (value.K == "null" && ObjectHoldingVars.Contains(n.Path + ":" + v.Name) && ValuesBelow(n, v.Name).Any(x => x.K == "type")) return CsType.Object;
        // gaps CI1 (D339): a var declared as a circuit port (/datum/port/...) holds the port add_input_port/add_output_port makes,
        // never a path, although its name (output, result, key, item) is a typepath elsewhere in tg: held as its declared type.
        bool declaredPort = decl != null && (decl == "/datum/port" || decl.StartsWith("/datum/port/", StringComparison.Ordinal));
        bool v3 = value.K == "type" || (!declaredPort && _typepathAssigned.Contains(v.Name) && value.K == "null"
            && ValuesBelow(n, v.Name).All(x => x.K is "null" or "type") && !ObjectHoldingVars.Contains(n.Path + ":" + v.Name));
        if (v3 || TypeHoldingVars.Contains(n.Path + ":" + v.Name)) return CsType.TypeT;
        if (decl != null)
        {
            // V2: the class of the declared type, unless the default is of another kind (3.2, wider values).
            Node cls = ClassNode(decl);
            bool otherKind = value.K is "num" or "str" or "res" or "proc" or "pinf" or "ninf" or "list" or "alist" or "dimlist";
            if (value.K == "new") otherKind = !(ClassNode(value.S) is Node made && IsSelfOrAncestor(cls, made));
            if (value.K == "call") otherKind = !(decl == "/matrix" && value.S == "matrix()");
            if (cls == null || otherKind) return CsType.Object;
            return new CsType { Kind = CsType.K.Class, Name = cls.ClassName, ClassNode = cls };
        }
        switch (value.K)
        {
            case "num":
            case "pinf":
            case "ninf":
                if (TimerIdVars.Contains(n.Path + ":" + v.Name)) return CsType.Object; // a timer id after the number default
                return _nullUsed.Contains(v.Name) ? CsType.NullableFloat : CsType.Float; // V4
            case "str":
            case "proc":
                return CsType.String; // V5
            case "list":
            case "alist":
            case "dimlist":
                return CsType.List; // V6
            case "new" when value.S == "/list":
                return CsType.List; // V6
            default:
                return CsType.Object; // V7
        }
    }

    // name -> every override and init-proc assignment of a var of that name, with its type.
    private Dictionary<string, List<(Node Node, DmVal Value)>> _valuesByName;

    /// <summary>The values tg gives var <paramref name="name"/> at <paramref name="n"/> and below (overrides, init assignments).</summary>
    private IEnumerable<DmVal> ValuesBelow(Node n, string name)
    {
        if (_valuesByName == null)
        {
            _valuesByName = new(StringComparer.Ordinal);
            foreach (Node x in _preorder)
                foreach (VarSet s in x.Info.Overrides.Concat(x.Info.Inits))
                {
                    if (!_valuesByName.TryGetValue(s.Name, out var list)) _valuesByName[s.Name] = list = new();
                    list.Add((x, s.Value ?? DmVal.Null()));
                }
        }
        if (!_valuesByName.TryGetValue(name, out var all)) yield break;
        foreach (var (node, value) in all)
            if (IsSelfOrAncestor(n, node)) yield return value;
    }

    // name -> every station-map var edit of a var of that name (types-converter.md 3.5): the edited type, the value's kind, the map.
    private Dictionary<string, List<(Node Node, DmVal Value, string Map)>> _mapValuesByName;

    /// <summary>The kinds of the values the station maps give var <paramref name="name"/> at <paramref name="n"/> and below (their var edits).</summary>
    private IEnumerable<(DmVal Value, string Map)> MapValuesBelow(Node n, string name)
    {
        if (_mapValuesByName == null)
        {
            _mapValuesByName = new(StringComparer.Ordinal);
            foreach (MapEdit e in _model.MapEdits ?? new())
            {
                Node at = _nodes.GetValueOrDefault(e.Path);
                if (at == null) continue; // a path tg's tree does not have: the map loader reports it
                if (!_mapValuesByName.TryGetValue(e.Var, out var list)) _mapValuesByName[e.Var] = list = new();
                list.Add((at, new DmVal { K = e.Kind }, e.Map));
            }
        }
        if (!_mapValuesByName.TryGetValue(name, out var all)) yield break;
        foreach (var (node, value, map) in all)
            if (IsSelfOrAncestor(n, node)) yield return (value, map);
    }

    /// <summary>
    /// Types-converter.md 3.5, "widening": a var whose rules type cannot hold a value tg gives it (an override or init
    /// assignment below, or a station map's var edit of the type or a subtype) becomes <c>float?</c> (a null into a
    /// float) or <c>object</c>, DM's dynamic value (3.2, "typed vars that hold wider values"), so that every value in
    /// tg's tree and maps is stored.
    /// </summary>
    private CsType Widen(Node n, VarDecl v, CsType t)
    {
        CsType result = t;
        DmVal own = v.Value ?? DmVal.Null();
        string map = null;
        string test = null;
        IEnumerable<(DmVal, string, string)> all = ValuesBelow(n, v.Name).Prepend(own).Select(x => (x, (string)null, (string)null))
            .Concat(MapValuesBelow(n, v.Name).Select(m => (m.Value, m.Map, (string)null)))
            .Concat(KindsTested(v).Select(k => (new DmVal { K = k }, (string)null, k)));
        foreach ((DmVal x, string fromMap, string fromTest) in all)
        {
            if (Fits(x, result)) continue;
            CsType wider = x.K == "null" && result.Kind == CsType.K.Float ? CsType.NullableFloat : CsType.Object;
            if (!Fits(x, wider)) wider = CsType.Object;
            result = wider;
            map ??= fromMap != null ? $"{fromMap} gives {x.K}" : null;
            test ??= fromTest != null ? $"{v.File} tests it with {fromTest switch { "str" => "istext", "num" => "isnum", "type" => "ispath", _ => "islist" }}" : null;
        }
        if (result != t)
        {
            bool tree = !ValuesBelow(n, v.Name).Prepend(own).All(x => Fits(x, t));
            Report.Add($"widened: {n.Path}.{v.Name} ({v.File}:{v.Line}) {t.CSharp} -> {result.CSharp}: "
                + (tree ? "a value below is of another kind" : map != null ? $"a station map's var edit is of another kind ({map})" : $"tg's code tests it for another kind ({test})"));
        }
        return result;
    }

    /// <summary>The kinds tg's code in the var's declaring file tests its name for (<c>istext</c>, <c>isnum</c>, <c>ispath</c>, <c>islist</c>).</summary>
    private IEnumerable<string> KindsTested(VarDecl v)
    {
        if (_model.KindTests == null || v.File == null) return Enumerable.Empty<string>();
        return _model.KindTests.TryGetValue(v.File + "|" + v.Name, out var kinds) ? kinds : Enumerable.Empty<string>();
    }

    /// <summary>
    /// Station-map var edits that a hand-written member cannot hold (its C# type is the translator's): one report line
    /// each, for the class's owner (types-converter.md 3.5).
    /// </summary>
    private void ReportHandMapMisfits()
    {
        foreach (MapEdit e in _model.MapEdits ?? new())
        {
            Node at = _nodes.GetValueOrDefault(e.Path);
            if (at == null) continue;
            var (type, owner) = MemberOf(at, e.Var);
            if (type == null || owner.Hand == null || !(owner.Hand.Fields.Contains(e.Var) || owner.Hand.Properties.Contains(e.Var))) continue;
            if (!Fits(new DmVal { K = e.Kind }, type))
                Report.Add($"map misfit: {e.Map} gives {e.Path}.{e.Var} a {e.Kind}; the hand-written {owner.ClassName}.{e.Var} is {type.CSharp}");
        }
    }

    private bool Fits(DmVal v, CsType t)
    {
        switch (v.K)
        {
            case "null": return t.Kind != CsType.K.Float;
            case "num": case "pinf": case "ninf": return t.Kind is CsType.K.Float or CsType.K.NullableFloat or CsType.K.Object;
            case "str": case "proc": return t.Kind is CsType.K.String or CsType.K.Object;
            case "res": return t.Kind == CsType.K.Object;
            case "type": return t.Kind is CsType.K.Type or CsType.K.Object;
            case "list": case "alist": case "dimlist": return t.Kind is CsType.K.List or CsType.K.Object;
            case "new":
                if (v.S == "/list") return t.Kind is CsType.K.List or CsType.K.Object;
                if (t.Kind == CsType.K.Object) return true;
                return t.Kind == CsType.K.Class && ClassNode(v.S) is Node made && IsSelfOrAncestor(t.ClassNode, made);
            case "call":
                return t.Kind is CsType.K.Object or CsType.K.Class or CsType.K.Other;
            default:
                return true;
        }
    }

    private static bool IsSelfOrAncestor(Node ancestor, Node n)
    {
        for (Node x = n; x != null; x = x.Parent)
            if (x == ancestor) return true;
        return false;
    }

    /// <summary>The C# member of var <paramref name="name"/> as seen from <paramref name="n"/>: its type and declaring node, or null.</summary>
    private (CsType Type, Node Owner) MemberOf(Node n, string name)
    {
        for (Node x = n; x != null; x = x.Parent)
            if (x.Members.TryGetValue(name, out CsType t)) return (t, x);
        return (null, null);
    }
}
