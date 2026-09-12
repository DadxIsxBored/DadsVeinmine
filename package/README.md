# DadsVeinmine

Whole-deposit vein mining built for Valheim 1.0.12.

Hold `Left Alt` while striking a mine rock or ore deposit with a valid pickaxe. The complete deposit is processed unless the pickaxe reaches zero durability first. Every additional section consumes the pickaxe's normal per-use durability and is broken through Valheim's native mining handler as processing advances.

DadsVeinmine routes each section through Valheim's native mining handlers so normal tool-tier checks, network handling, node removal, effects, statistics, and item drops remain active. Activation, progressive mining radius, per-frame processing, prefab exclusions, stamina cost, and Pickaxes skill gain are configurable.

Requires BepInEx only. Do not run another vein-mining plugin at the same time.
