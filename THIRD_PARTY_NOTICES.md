# Third-party references and attribution

## Mono.Cecil

- Project: https://github.com/jbevain/cecil
- Package: https://www.nuget.org/packages/Mono.Cecil/0.11.6
- Version: 0.11.6, MIT licensed by its own authors.
- This source release does **not** bundle its DLL or package. `fetch-deps` downloads the official NuGet package and verifies pinned SHA-256 values.
- Preserve its upstream license if you separately redistribute the dependency.

## RoninOnis/BongoCat-auto — historical reference, not bundled

- Link: https://github.com/RoninOnis/BongoCat-auto
- Checked: 2026-11-03.
- Its public README describes a Bongo Cat auto-chest approach. The displayed root file listing contained README, Python code and images; no standalone LICENSE file was visible during this check.
- We have **not established permission to redistribute its source, images, DLL or PDB releases**. This repository therefore links and credits it only. It does not copy or bundle those materials.
- A public GitHub repository or a fork is not by itself permission to relicense its contents. See https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/licensing-a-repository .
- This link is background reading, **not an endorsement of an unreviewed executable or old DLL**. The reference project's global keyboard script does not meet this project's foreground-isolation design.
- Before bundling any upstream material, obtain an applicable explicit license or author permission. Do not label upstream content MIT merely because a badge or a fork suggests it.

## Games

Bongo Cat and TapTapLoot, their names, game binaries and assets belong to their respective owners. No original or modified game DLL, PDB, save, Steam account data or screenshot is distributed here. Users must supply their own local game installation. This repository's MIT license does not override game or platform terms and does not promise account-policy approval.

## Optional chest module in this repository

`src/patchers/PreparePatch.cs` is the locally developed patch generator used in this project. It is not the reference author's downloadable game DLL. It generates a local hook that checks the game's existing ready/affordability conditions and invokes its original `OnClick` path, with a retry interval and result logging. The optional module is built from the user's own local game assembly; generated game files remain private in `.local/`.
