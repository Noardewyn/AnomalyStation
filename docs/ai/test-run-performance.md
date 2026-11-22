# Why the test runs are as fast as they are

Moved from docs/testing.md (2026-11-22): the history of the test suites' speed and memory tuning. The guide for running the tests is docs/testing.md.

- A Meta boot no longer spends 24 s in SSmapping's first fire: the reserved turfs move into the world's area by `loc` instead of
  through a handed-out `contents` list of that area, which the engine reconciled whole (every space turf) at each coordinate read.

- The test projects use the server GC (`ServerGarbageCollection`, not concurrent): the parallel tests share one heap of many
  worlds, and the state hash (`World.ComputeStateHash`) forces a full collection, which the workstation GC did on one thread while
  every test waited (the quick Core run took 14 minutes with it, 5 with the server GC).
- The Meta worlds wait for memory: `MetaWorldGate` (Server: at most the GC's available memory over 3 GB at once,
  `ANOMALY_META_PARALLEL` overrides it) and `MetaMemoryGate` (Core: a boot starts while 4 GB of physical memory stay free). Without
  them xUnit's parallel classes booted up to 32 Metas at once and the machine swapped.
- `SlowMetaFact` tests (two full boots of Meta) run only with `ANOMALY_SLOW=1`.
- The quick run caps each test host's GC heap at 16 GB (`DOTNET_GCHeapHardLimit=0x400000000` in `tools/test-quick.sh`; set the
  variable yourself to change it). Uncapped, the server GC keeps as much heap as the machine lets it: on 32 cores and 63 GB the
  Server tests reached 22-27 GB and the Core tests 12 GB at once, and the machine ran out of memory; capped at 8 GB, the Server tests
  peaked at about 8.7 GB and passed, about 10% slower (2026-11-20), but the Core tests ran out of memory, and a Meta boot broken
  by it hung the run for hours; the cap is 16 GB since 2026-11-22. The full run is not capped: its Meta worlds need about 3 GB each.

Always build first and check that the build succeeded: `dotnet test --no-build` on a failed build runs the old binaries.
