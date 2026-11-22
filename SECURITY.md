# Security policy

Anomaly Station is a multiplayer game: people run public servers and players connect to them. A security bug can hurt
players and hosts, so please report it privately, not in a public issue.

## Reporting a vulnerability

- **Preferred:** open a private report on GitHub: the repository's **Security** tab, then **Report a vulnerability**.
  Only the maintainers can see it.
- **Or:** write to a maintainer directly on our [Discord](https://discord.com/invite/3QYKDaWVX3). Don't post the details in
  public channels.

Please include:

- what the problem is and what an attacker can do with it;
- the steps to reproduce it (a server config, the commands or packets you sent);
- the version (the game's version is in the main menu and in `Version.props`) or the commit.

We will answer within a week, tell you whether we can reproduce it, and keep you updated until it is fixed. When the fix is
released we credit you in the changelog, unless you prefer to stay anonymous.

## What counts

Yes, please report:

- a way to crash or freeze a server, or make it use unbounded memory or CPU, from a game client;
- a way to log in as another player, or to get admin rights or dev commands you were not given;
- a way to run code, or read or write files, on a server or on another player's machine;
- leaks of players' tokens, addresses or other data.

Not security bugs (open a normal issue instead):

- gameplay exploits that stay inside the game's rules (duping items, breaking a mechanic);
- problems that need `DEV_COMMANDS` or `CLIENT_CONSOLE` turned on: they give players powerful commands on purpose;
- the game traffic not being encrypted: this is known, see [hosting.md](docs/hosting.md).

## Supported versions

Only the newest release and `main` get security fixes.
