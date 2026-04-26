# Friendly PMCs Reborn

Ground-up SPT 4.x rewrite of the FriendlyPMC squad system for single-player Escape from Tarkov.

This branch focuses on persistent squad ownership, raid followers that actually survive across sessions, Messenger-based roster management, in-raid command control, and follower inventory handling through Tarkov's own social UI.

## Current scope

- persistent follower roster records on the server side
- autojoin raid followers for managed squad members
- Messenger Squad Manager chat commands for roster administration
- same-side in-raid recruitment into the persistent squad flow
- direct follower command hotkeys for follow, hold, combat, and recovery
- follower profile opening and inventory management from the social UI
- follower gear transfer and slot-based equipment control

## Repository layout

- `client-spt4/FriendlyPMC.CoreFollowers`
  SPT4 client plugin source.
- `server-spt4/FriendlyPMC.Server`
  SPT4 server mod source.
- `client-spt4/FriendlyPMC.Version.props`
  shared version metadata for both sides.
- `ModDescription.html`
  end-user feature and installation summary intended for release pages.

## Build requirements

- .NET 9 SDK
- SPT 4.x local install
- BigBrain installed in the target SPT client environment

The client project expects local EFT/SPT reference paths through `client-spt4/Directory.Build.props`.

1. Copy `client-spt4/Directory.Build.props.example` to `client-spt4/Directory.Build.props`
2. Update the paths for your local SPT install

## Build

```powershell
dotnet build .\client-spt4\FriendlyPMC.CoreFollowers\FriendlyPMC.CoreFollowers.csproj -c Release
dotnet build .\server-spt4\FriendlyPMC.Server\FriendlyPMC.Server.csproj -c Release
```

Client output:

- `client-spt4/FriendlyPMC.CoreFollowers/bin/Release/netstandard2.1`

Server output:

- `server-spt4/FriendlyPMC.Server/bin/Release/FriendlyPMC.Server`

## Install

Client:

- copy the built `FriendlyPMC.CoreFollowers` artifacts into `BepInEx/plugins/FriendlyPMC.CoreFollowers`

Server:

- copy the built `FriendlyPMC.Server` output into `user/mods/FriendlyPMC.Server`

Release ZIP layout:

- `BepInEx/plugins/FriendlyPMC.CoreFollowers/FriendlyPMC.CoreFollowers.dll`
- `BepInEx/plugins/FriendlyPMC.CoreFollowers/FriendlyPMC.CoreFollowers.deps.json`
- `user/mods/FriendlyPMC.Server/FriendlyPMC.Server.dll`
- `user/mods/FriendlyPMC.Server/FriendlyPMC.Server.deps.json`

## End-user reference

`ModDescription.html` is the current source of truth for player-facing feature scope, installation notes, and compatibility guidance.
