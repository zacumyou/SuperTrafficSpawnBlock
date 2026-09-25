# Super Traffic Spawn Block

[Subscribe on Paradox Mods](https://mods.paradoxplaza.com/mods/160650/Windows) · [Support forum](https://forum.paradoxplaza.com/forum/threads/super-traffic-spawn-block.1942707/)

Add traffic where you want it. Send it out of the city or to a sink block.
Both custom block assets are included.

## How to use

Open **Roads > Maintenance**. You will find two blocks.
Place a **Super Traffic Spawn Block** next to a road. Its vehicles leave the city by default. Your road network needs an outside connection.
A **Super Traffic Sink Block** does nothing on its own. It only removes vehicles sent by this mod.
Place both blocks. Select the spawn block. Choose a sink in **Traffic destination**.
You can choose more than one sink. Each selected destination has the same chance. Two destinations get a 50% chance each.
You can place several spawn blocks. Each has its own settings.

## Traffic amount

New blocks start at **2x**. Choose **1x, 2x, 5x, 10x, 20x or 50x** in the block panel.
**High rates can put a heavy load on your system. Start low.**
Spawning pauses when vehicles cannot leave. It resumes when the queue clears.
Heavy vehicles are off by default. You can enable them in the mod options.

## Translations

This mod supports **I18n Everywhere**. It is listed as a required mod.
You can help translate the mod. See **TRANSLATING.md** in the source repository.
English and Korean are included.

## Bug reports

Please report errors in the linked **Paradox Forum** thread.
Include your game version, mod version, steps, a screenshot and the mod log.

## Required mod

[I18n Everywhere](https://mods.paradoxplaza.com/mods/75426/Windows)

## Build

Install the official CS2 modding toolchain, Node.js 18+ and the .NET 9 SDK for the standalone tests. Keep the toolchain-provided CSII environment variables. Do not copy game DLLs into this repository.

```powershell
cd ui
npm ci
cd ..
./build.ps1
```

Output: `local/SuperTrafficSpawnBlock`. It includes both block assets. `install.ps1` installs this output locally while the game is closed. Do not enable both a local copy and a subscribed copy.

## Save compatibility

Legacy block identities and per-block settings are retained. Do not remove the mod while you still need its placed blocks. Make a backup of your city before removing mods.

## License and contributions

See [LICENSE](LICENSE) and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
See [translation instructions](TRANSLATING.md). Keep pull requests focused. Describe the change and how you tested it. Do not upload game DLLs, private logs, credentials or decompiled game code.
