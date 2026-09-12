# DadsVeinmine

DadsVeinmine provides whole-deposit vein mining through Valheim 1.0.12's native `MineRock` and `MineRock5` damage handlers.

Hold `Left Alt` while striking a mine rock or ore deposit with a valid pickaxe. DadsVeinmine processes every remaining section through Valheim's native mining handlers, breaking and removing the deposit as it advances. Each additional section consumes the pickaxe's normal per-use durability, and processing stops when the pickaxe reaches zero durability.

## Configuration

- Activation mode: `HoldKey`, `AlwaysOn`, or `Off`.
- Configurable activation shortcut.
- Sections processed per frame.
- Case-insensitive prefab exclusions.
- Optional progressive radius based on Pickaxes skill.
- Normal pickaxe durability cost for every mined section.
- Configurable additional stamina and Pickaxes skill costs.

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
