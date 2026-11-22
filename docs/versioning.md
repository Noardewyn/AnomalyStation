# Versioning and the changelog

How builds of the game are numbered, and how `CHANGELOG.md` is kept.

## The version

The whole game has one version: the core, the server, the client and the tools of a build all carry it. It lives in
[`Version.props`](../Version.props) at the repository root.

A version looks like `1.2.0-pre-alpha`: `MAJOR.MINOR.PATCH`, then the development phase.

| Part | When it changes | Example |
|---|---|---|
| phase | The project moves to its next phase: `pre-alpha`, then `alpha`, then `beta`. The numbers start again from `0.0.0` | `0.0.0-alpha` |
| MAJOR | The maintainers declare it after many big features. MINOR and PATCH go back to 0 | `2.0.0-pre-alpha` |
| MINOR | The release brings a new feature (a mechanic, a system, a job, a window, an event) or reworks an existing one. PATCH goes back to 0 | `1.2.0-pre-alpha` |
| PATCH | The release brings only bug fixes, config and tuning changes | `1.2.1-pre-alpha` |

A release takes the biggest kind among its changes: one new feature among many fixes makes it a MINOR. Versions compare by
phase first, then by number, so `1.3.2-pre-alpha` comes before `0.0.0-alpha`.

Older releases were numbered differently: `0.5.0` to `0.9.7` came before the pre-alpha, and `1.1.x-pre-alpha` releases
were all counted as patches.

## The version and the network protocol

A client and a server can play together only when they speak the same network protocol (`ProtocolVersion.Current`).
So two builds of one version must always speak one protocol, and any protocol change needs a new release.

`Version.props` records the protocol of its version (`AnomalyProtocolVersion`). While the code's protocol differs from it:

- `Anomaly.Protocol` builds with the warning `ANOMVER01`, so the build is not clean;
- the test `BuildInfoTests.TheVersionNamesTheProtocolItSpeaks` fails.

`tools/release.sh` fixes both: it writes the new protocol next to the new version and adds a `### Protocol` line to the
changelog. A commit that changes the protocol is therefore followed by a release.

## Telling builds apart

Every build is stamped with its version and git commit, and shows them as `1.2.0 (37558c67)`, or
`1.2.0 (37558c67, dirty)` when it was built from uncommitted changes:

- the client: in the top-right corner of every screen, in the window title and in the first line of its log;
- the server: in the first line it prints, together with the protocol version.

A bug report names its build by copying that line.

## The changelog

[`CHANGELOG.md`](../CHANGELOG.md) at the repository root, newest version first, in the
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) layout.

- A change that players can see, hear or do adds a line under `## [Unreleased]`, in the group that fits (`Added`,
  `Changed`, `Fixed`, `Removed`). Code that changes nothing in the game (refactors, tests, tools, docs) adds none.
- A line is written for players: what changed in the game, not which files. One line per feature, not per commit.
- Keep it short: one sentence, about 150 characters at most. A big feature lists its key parts ("Bitrunning: domains,
  netpods, avatars and reward crates"). The details and the reasons go in the commit message.

## Making a release

Releases are made by the maintainers:

```bash
tools/release.sh minor
```

`patch`, `minor` or `major` picks the next number (`1.1.28-pre-alpha` gives `1.1.29`, `1.2.0` or `2.0.0-pre-alpha`). A full
version (`tools/release.sh 0.0.0-alpha`) starts a new phase. The script:

1. checks that the new version is higher than the current one;
2. writes it and the current protocol version into `Version.props`;
3. moves the `Unreleased` lines of `CHANGELOG.md` under `## [1.2.0-pre-alpha] - <today>` and leaves an empty `Unreleased`;
4. commits both files as `Release 1.2.0-pre-alpha` and tags the commit `v1.2.0-pre-alpha`.

It pushes nothing: `git push --follow-tags` publishes the release. It refuses to run when `Unreleased` is empty, unless
the protocol changed.
