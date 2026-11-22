// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/dcs/helpers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/__DEFINES/dcs/helpers.dm:5
/// Used to trigger signals and call procs registered for that signal
/// The datum hosting the signal is automatically added as the first argument
/// Returns a bitfield gathered from all registered procs
/// Arguments given here are packaged in a list and given to _SendSignal
// SEND_SIGNAL(target, sigtype, arguments...) is expanded inline at every use (rules 11.1 (e)): its arguments are
// evaluated only when a listener exists. As a value:
//     (!Dm.truthy(target._listen_lookup?[sigtype]) ? NONE : target._SendSignal(sigtype, Dm.list(target, arguments...)))
// as a statement:
//     if (Dm.truthy(target._listen_lookup?[sigtype]))
//         target._SendSignal(sigtype, Dm.list(target, arguments...));
// with target and sigtype bound to temps first unless they are plain reads (rules 4.12).

// tg: code/__DEFINES/dcs/helpers.dm:7
// SEND_GLOBAL_SIGNAL(sigtype, arguments...) is SEND_SIGNAL(SSdcs, sigtype, arguments...), expanded inline the same way
// (SSdcs: controllers/subsystem/dcs.dm, translated by the dcs package).

// tg: code/__DEFINES/dcs/helpers.dm:11
/// Signifies that this proc is used to handle signals.
/// Every proc you pass to RegisterSignal must have this.
// SIGNAL_HANDLER is the attribute [SignalHandler] (rules 7.9).

public partial class datum
{
    // AddElement, RemoveElement, AddComponent, AddComponentFrom and LoadComponent expand to a call on the caller's src
    // or on an explicit receiver (parent.AddComponent(...) is parent._AddComponent(list(...))): non-virtual instance
    // methods of datum in this mirror file (rules 7.3, 11.1). Their value is the value of the call they wrap: a DmTask
    // for the suspending slots (await, or Waiting.NoSleep in synchronous code).

    /// A wrapper for _AddElement that allows us to pretend we're using normal named arguments
    // tg: code/__DEFINES/dcs/helpers.dm:14
    public DmTask AddElement(params object[] arguments) => _AddElement(DcsArgumentList(arguments));

    /// A wrapper for _RemoveElement that allows us to pretend we're using normal named arguments
    // tg: code/__DEFINES/dcs/helpers.dm:16
    public void RemoveElement(params object[] arguments) => _RemoveElement(DcsArgumentList(arguments));

    /// A wrapper for _AddComponent that allows us to pretend we're using normal named arguments
    // tg: code/__DEFINES/dcs/helpers.dm:19
    public DmTask<object> AddComponent(params object[] arguments) => _AddComponent(DcsArgumentList(arguments));

    /// A wrapper for _AddComonent that passes in a source.
    /// Necessary if dupe_mode is set to COMPONENT_DUPE_SOURCES.
    // tg: code/__DEFINES/dcs/helpers.dm:23
    public DmTask<object> AddComponentFrom(object source, params object[] arguments) => _AddComponent(DcsArgumentList(arguments), source);

    /// A wrapper for _LoadComponent that allows us to pretend we're using normal named arguments
    // tg: code/__DEFINES/dcs/helpers.dm:26
    public DmTask<object> LoadComponent(params object[] arguments) => _LoadComponent(DcsArgumentList(arguments));

    // list(##arguments) of the five wrappers (and of the memory macros, __HELPERS/memory_helpers.cs): the positional arguments in order, each named argument (Dm.named, rules
    // 7.3) as an entry with a text key, as DM's list() makes of a named argument; a list argument stays one item.
    internal static DmList DcsArgumentList(object[] arguments)
    {
        arguments ??= new object[] { null }; // a null params array is one null argument (shim-ownership.md)
        var items = new object[arguments.Length];
        for (int i = 0; i < arguments.Length; i++)
            items[i] = arguments[i] is DmNamedArg named ? Dm.kv(named.Name, named.Value) : arguments[i];
        return Dm.list(items);
    }
}
