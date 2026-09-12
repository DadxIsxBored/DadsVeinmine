# DadsVeinmine

DadsVeinmine provides whole-deposit vein mining through Valheim 1.0.12's native `MineRock` and `MineRock5` damage handlers.

Hold `Left Alt` while striking a mine rock or ore deposit with a valid pickaxe. DadsVeinmine processes the deposit's remaining sections while Valheim retains control of tool-tier checks, section health, network ownership, effects, statistics, and normal item drops.

## Configuration

- Activation mode: `HoldKey`, `AlwaysOn`, or `Off`.
- Configurable activation shortcut.
- Maximum sections per strike.
- Sections processed per frame.
- Case-insensitive prefab exclusions.
- Optional progressive radius based on Pickaxes skill.
- Configurable additional durability, stamina, and Pickaxes skill costs.

Configuration path: `BepInEx/config/com.dadisbored.dadsveinmine.cfg`.

## Installation

Install BepInExPack for Valheim, then place `DadsVeinmine.dll` in `BepInEx/plugins/DadsVeinmine/`.

## Compatibility

- Built against Valheim `1.0.12`.
- Requires BepInEx only.
- Client-side mining controller.
- Do not run another vein-mining plugin at the same time.

## Prior work

The feature scope was checked against Veinmine by WiseHorror and its later maintenance by Azumatt. DadsVeinmine uses its own Valheim 1.0.12 implementation, identifiers, configuration, package, and original artwork.
