# Running the tests

The tests use xunit and live in `tests/`:

| Project | What it tests |
|---|---|
| `Anomaly.Core.Tests` | the simulation core and the code translated from tg, including tg's own unit tests |
| `Anomaly.Server.Tests` | the server, the network protocol, the client's shared code, and gameplay tests that drive a real server |
| `Anomaly.Analyzers.Tests` | the project's Roslyn analyzers |

Always build first and check that the build succeeded: `dotnet test --no-build` after a failed build runs the old binaries.

## Quick and full runs

Most of the test time goes to tests that boot the full MetaStation map. Each such boot takes tens of seconds and about
2-3 GB of memory. These tests carry the trait `Category=Meta` (their attributes are `MetaFact`, `SlowMetaFact`,
`MetaTgFact` in Core and `MetaServerFact`, `MetaServerTheory` in Server), so a run can leave them out.

| Run | Command | When |
|---|---|---|
| quick | `tools/test-quick.sh` (the same as `dotnet test AnomalyStation.sln --filter "Category!=Meta"`) | before every commit or pull request |
| one area | `dotnet test tests/Anomaly.Server.Tests --filter "FullyQualifiedName~Admin"` | while you work |
| full | `dotnet test AnomalyStation.sln` | before a release or after a large change |

On a 32-core machine the quick run takes about 5 minutes for Core and 2 for Server; the full run about 6 for Core and 10
for Server. If your change touches what the Meta tests cover (the map, the server's boot, a mechanic whose gameplay test
runs on MetaStation), run those Meta tests by name too.

`SlowMetaFact` tests (two full boots of MetaStation each) run only with `ANOMALY_SLOW=1`.

## Memory

The tests run in parallel, and the Meta tests need a lot of memory:

- **The quick run** caps each test process at 16 GB (`DOTNET_GCHeapHardLimit` in `tools/test-quick.sh`). It needs a
  machine with **32 GB of RAM**. With 16 GB, run one project or one area at a time instead.
- **The full run** has no cap and needs about 3 GB for each MetaStation it boots at once. The test helpers start a new one
  only while enough memory is free (`ANOMALY_META_PARALLEL` sets how many Server tests may boot one at once).

If a run is killed or hangs when memory runs out, set a lower cap yourself, for example
`DOTNET_GCHeapHardLimit=0x200000000` (8 GB), or filter the run down to the area you changed.

## Determinism checks

- `dotnet run --project tools/Anomaly.Tools.ToyWorld -c Release -- --twice` runs the toy world twice and compares the
  state hashes.
- On the server, `--record run.log` records a run and `--replay run.log` replays it and checks the final hash.
