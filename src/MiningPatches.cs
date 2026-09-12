using HarmonyLib;

namespace DadsVeinmine;

[HarmonyPatch(typeof(MineRock), nameof(MineRock.Damage))]
internal static class MineRockDamagePatch
{
    private static bool Prefix(MineRock __instance, HitData hit)
    {
        return !DadsVeinminePlugin.Instance.QueueMineRock(__instance, hit);
    }
}

[HarmonyPatch(typeof(MineRock5), nameof(MineRock5.Damage))]
internal static class MineRock5DamagePatch
{
    private static bool Prefix(MineRock5 __instance, HitData hit)
    {
        return !DadsVeinminePlugin.Instance.QueueMineRock5(__instance, hit);
    }
}
