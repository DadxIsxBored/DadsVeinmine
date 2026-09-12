using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace DadsVeinmine;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInProcess("valheim.exe")]
public sealed class DadsVeinminePlugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.dadisbored.dadsveinmine";
    public const string PluginName = "DadsVeinmine";
    public const string PluginVersion = "1.0.0";

    internal enum ActivationModes
    {
        HoldKey,
        AlwaysOn,
        Off
    }

    internal static DadsVeinminePlugin Instance = null!;
    internal static ConfigEntry<ActivationModes> ActivationMode = null!;
    internal static ConfigEntry<KeyboardShortcut> ActivationKey = null!;
    internal static ConfigEntry<int> MaximumSections = null!;
    internal static ConfigEntry<int> SectionsPerFrame = null!;
    internal static ConfigEntry<string> ExcludedPrefabFragments = null!;
    internal static ConfigEntry<bool> ProgressiveMode = null!;
    internal static ConfigEntry<float> ProgressiveRadiusMultiplier = null!;
    internal static ConfigEntry<float> MinimumProgressiveRadius = null!;
    internal static ConfigEntry<bool> AdditionalDurabilityCost = null!;
    internal static ConfigEntry<float> DurabilityCostMultiplier = null!;
    internal static ConfigEntry<float> AdditionalStaminaPerSection = null!;
    internal static ConfigEntry<float> ExtraSkillGainMultiplier = null!;

    private readonly List<MiningJob> _jobs = new();
    private readonly HashSet<int> _activeTargets = new();
    private Harmony? _harmony;

    private static readonly FieldInfo MineRockHitAreasField = AccessTools.Field(typeof(MineRock), "m_hitAreas");
    private static readonly FieldInfo MineRockViewField = AccessTools.Field(typeof(MineRock), "m_nview");
    private static readonly FieldInfo MineRock5HitAreasField = AccessTools.Field(typeof(MineRock5), "m_hitAreas");
    private static readonly FieldInfo MineRock5ViewField = AccessTools.Field(typeof(MineRock5), "m_nview");
    private static readonly MethodInfo MineRock5SetupCollidersMethod = AccessTools.Method(typeof(MineRock5), "SetupColliders");
    private static readonly MethodInfo MineRock5LoadHealthMethod = AccessTools.Method(typeof(MineRock5), "LoadHealth");
    private static readonly Type MineRock5HitAreaType = AccessTools.Inner(typeof(MineRock5), "HitArea");
    private static readonly FieldInfo MineRock5AreaColliderField = AccessTools.Field(MineRock5HitAreaType, "m_collider");
    private static readonly FieldInfo MineRock5AreaHealthField = AccessTools.Field(MineRock5HitAreaType, "m_health");

    private void Awake()
    {
        Instance = this;

        ActivationMode = Config.Bind(
            "1 - Activation",
            "Mode",
            ActivationModes.HoldKey,
            "HoldKey requires the configured key, AlwaysOn activates on every valid pickaxe hit, and Off disables vein mining.");
        ActivationKey = Config.Bind(
            "1 - Activation",
            "Hold Key",
            new KeyboardShortcut(KeyCode.LeftAlt),
            "Key held while striking a mine rock or ore deposit in HoldKey mode.");

        MaximumSections = Config.Bind(
            "2 - Mining",
            "Maximum Sections",
            256,
            new ConfigDescription(
                "Maximum rock sections processed from one strike.",
                new AcceptableValueRange<int>(1, 2048)));
        SectionsPerFrame = Config.Bind(
            "2 - Mining",
            "Sections Per Frame",
            8,
            new ConfigDescription(
                "Maximum sections sent through Valheim's native mining handler per frame.",
                new AcceptableValueRange<int>(1, 64)));
        ExcludedPrefabFragments = Config.Bind(
            "2 - Mining",
            "Excluded Prefab Fragments",
            string.Empty,
            "Comma-separated prefab-name fragments that DadsVeinmine must ignore. Matching is case-insensitive.");

        ProgressiveMode = Config.Bind(
            "3 - Progressive Mining",
            "Enabled",
            false,
            "Limit vein mining to a radius derived from the player's Pickaxes skill.");
        ProgressiveRadiusMultiplier = Config.Bind(
            "3 - Progressive Mining",
            "Skill Radius Multiplier",
            0.1f,
            new ConfigDescription(
                "Pickaxes skill is multiplied by this value to determine mining radius.",
                new AcceptableValueRange<float>(0.01f, 10f)));
        MinimumProgressiveRadius = Config.Bind(
            "3 - Progressive Mining",
            "Minimum Radius",
            1f,
            new ConfigDescription(
                "Minimum radius used while progressive mining is enabled.",
                new AcceptableValueRange<float>(0.1f, 100f)));

        AdditionalDurabilityCost = Config.Bind(
            "4 - Costs",
            "Additional Durability Cost",
            true,
            "Charge pickaxe durability for every section after the first.");
        DurabilityCostMultiplier = Config.Bind(
            "4 - Costs",
            "Durability Cost Multiplier",
            1f,
            new ConfigDescription(
                "Multiplier applied to normal pickaxe durability drain for additional sections.",
                new AcceptableValueRange<float>(0f, 100f)));
        AdditionalStaminaPerSection = Config.Bind(
            "4 - Costs",
            "Additional Stamina Per Section",
            0f,
            new ConfigDescription(
                "Stamina charged for every section after the first. Zero disables the additional stamina cost.",
                new AcceptableValueRange<float>(0f, 1000f)));
        ExtraSkillGainMultiplier = Config.Bind(
            "4 - Costs",
            "Extra Pickaxes Skill Gain",
            0.2f,
            new ConfigDescription(
                "Pickaxes skill gain applied for every section after the first. Zero disables additional skill gain.",
                new AcceptableValueRange<float>(0f, 10f)));

        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll(typeof(DadsVeinminePlugin).Assembly);
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded for Valheim 1.0.12.");
    }

    private void Update()
    {
        for (int jobIndex = _jobs.Count - 1; jobIndex >= 0; jobIndex--)
        {
            MiningJob job = _jobs[jobIndex];
            int workCount = Mathf.Clamp(SectionsPerFrame.Value, 1, 64);

            while (workCount-- > 0 && job.NextTarget < job.Targets.Count)
            {
                if (!ProcessNextTarget(job))
                {
                    job.NextTarget = job.Targets.Count;
                    break;
                }
            }

            if (job.NextTarget >= job.Targets.Count)
            {
                _activeTargets.Remove(job.TargetInstanceId);
                _jobs.RemoveAt(jobIndex);
            }
        }
    }

    private bool ProcessNextTarget(MiningJob job)
    {
        if (job.View == null || !job.View.IsValid() || job.Player == null || job.Player.IsDead())
        {
            return false;
        }

        MiningTarget target = job.Targets[job.NextTarget];
        bool additionalSection = job.NextTarget > 0;
        if (additionalSection)
        {
            if (AdditionalDurabilityCost.Value && job.Weapon.m_shared.m_useDurability)
            {
                float durabilityCost = job.Weapon.m_shared.m_useDurabilityDrain *
                                       Mathf.Max(0f, DurabilityCostMultiplier.Value);
                if (job.Weapon.m_durability <= 0f)
                {
                    return false;
                }
                job.Weapon.m_durability = Mathf.Max(0f, job.Weapon.m_durability - durabilityCost);
            }

            float staminaCost = Mathf.Max(0f, AdditionalStaminaPerSection.Value);
            if (staminaCost > 0f)
            {
                if (!job.Player.HaveStamina(staminaCost))
                {
                    return false;
                }
                job.Player.UseStamina(staminaCost);
            }

            float skillGain = Mathf.Max(0f, ExtraSkillGainMultiplier.Value);
            if (skillGain > 0f)
            {
                job.Player.RaiseSkill(Skills.SkillType.Pickaxes, skillGain);
            }
        }

        HitData poweredHit = job.SourceHit.Clone();
        poweredHit.m_hitCollider = target.Collider;
        poweredHit.m_point = target.Point;
        poweredHit.m_damage.m_pickaxe = Mathf.Max(poweredHit.m_damage.m_pickaxe, 1_000_000f);
        job.View.InvokeRPC(job.RpcName, poweredHit, target.AreaIndex);
        job.NextTarget++;
        return true;
    }

    internal bool QueueMineRock(MineRock rock, HitData hit)
    {
        if (!TryGetMiningContext(rock, hit, rock.m_minToolTier, out Player player, out ItemDrop.ItemData weapon))
        {
            return false;
        }

        ZNetView? view = MineRockViewField.GetValue(rock) as ZNetView;
        Collider[]? hitAreas = MineRockHitAreasField.GetValue(rock) as Collider[];
        if (view == null || !view.IsValid() || view.GetZDO() == null || hitAreas == null)
        {
            return false;
        }

        int instanceId = rock.GetInstanceID();
        if (_activeTargets.Contains(instanceId))
        {
            return true;
        }

        List<MiningTarget> targets = new();
        float maximumDistance = GetMaximumDistance(player);
        for (int index = 0; index < hitAreas.Length; index++)
        {
            Collider area = hitAreas[index];
            if (area == null ||
                view.GetZDO().GetFloat("Health" + index, rock.GetHealth()) <= 0f)
            {
                continue;
            }

            Vector3 point = area.bounds.center;
            if (Vector3.Distance(hit.m_point, point) <= maximumDistance)
            {
                targets.Add(new MiningTarget(index, area, point));
            }
        }

        return QueueJob(instanceId, view, "Hit", hit, player, weapon, targets);
    }

    internal bool QueueMineRock5(MineRock5 rock, HitData hit)
    {
        if (!TryGetMiningContext(rock, hit, rock.m_minToolTier, out Player player, out ItemDrop.ItemData weapon))
        {
            return false;
        }

        MineRock5SetupCollidersMethod.Invoke(rock, null);
        MineRock5LoadHealthMethod.Invoke(rock, null);
        ZNetView? view = MineRock5ViewField.GetValue(rock) as ZNetView;
        IList? hitAreas = MineRock5HitAreasField.GetValue(rock) as IList;
        if (view == null || !view.IsValid() || hitAreas == null)
        {
            return false;
        }

        int instanceId = rock.GetInstanceID();
        if (_activeTargets.Contains(instanceId))
        {
            return true;
        }

        List<MiningTarget> targets = new();
        float maximumDistance = GetMaximumDistance(player);
        for (int index = 0; index < hitAreas.Count; index++)
        {
            object? area = hitAreas[index];
            if (area == null)
            {
                continue;
            }

            Collider? collider = MineRock5AreaColliderField.GetValue(area) as Collider;
            float health = (float)MineRock5AreaHealthField.GetValue(area);
            if (collider == null || health <= 0f)
            {
                continue;
            }

            Vector3 point = collider.bounds.center;
            if (Vector3.Distance(hit.m_point, point) <= maximumDistance)
            {
                targets.Add(new MiningTarget(index, collider, point));
            }
        }

        return QueueJob(instanceId, view, "RPC_Damage", hit, player, weapon, targets);
    }

    private bool QueueJob(
        int instanceId,
        ZNetView view,
        string rpcName,
        HitData hit,
        Player player,
        ItemDrop.ItemData weapon,
        List<MiningTarget> targets)
    {
        if (view == null || !view.IsValid() || targets.Count == 0)
        {
            return false;
        }

        int maximum = Mathf.Clamp(MaximumSections.Value, 1, 2048);
        List<MiningTarget> orderedTargets = targets
            .OrderBy(target => (target.Point - hit.m_point).sqrMagnitude)
            .Take(maximum)
            .ToList();

        _activeTargets.Add(instanceId);
        _jobs.Add(new MiningJob(instanceId, view, rpcName, hit.Clone(), player, weapon, orderedTargets));
        return true;
    }

    private static bool TryGetMiningContext(
        Component target,
        HitData hit,
        int minimumToolTier,
        out Player player,
        out ItemDrop.ItemData weapon)
    {
        player = null!;
        weapon = null!;

        if (!ActivationHeld() || hit == null || target == null || IsExcluded(target))
        {
            return false;
        }

        Player? attackingPlayer = hit.GetAttacker() as Player;
        if (attackingPlayer == null || attackingPlayer != Player.m_localPlayer)
        {
            return false;
        }

        player = attackingPlayer;
        ItemDrop.ItemData? currentWeapon = player.GetCurrentWeapon();
        if (currentWeapon == null)
        {
            return false;
        }

        weapon = currentWeapon;
        return weapon.GetDamage().m_pickaxe > 0f &&
               hit.m_damage.m_pickaxe > 0f &&
               hit.m_toolTier >= minimumToolTier;
    }

    private static bool ActivationHeld()
    {
        switch (ActivationMode.Value)
        {
            case ActivationModes.AlwaysOn:
                return true;
            case ActivationModes.Off:
                return false;
            default:
                KeyboardShortcut shortcut = ActivationKey.Value;
                if (shortcut.MainKey == KeyCode.None || !Input.GetKey(shortcut.MainKey))
                {
                    return false;
                }
                return shortcut.Modifiers.All(Input.GetKey);
        }
    }

    private static bool IsExcluded(Component target)
    {
        string prefabName = Utils.GetPrefabName(target.gameObject);
        return ExcludedPrefabFragments.Value
            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(fragment => fragment.Trim())
            .Any(fragment => fragment.Length > 0 &&
                             prefabName.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static float GetMaximumDistance(Player player)
    {
        if (!ProgressiveMode.Value)
        {
            return float.PositiveInfinity;
        }

        float skill = player.GetSkills().GetSkillLevel(Skills.SkillType.Pickaxes);
        return Mathf.Max(
            Mathf.Max(0.1f, MinimumProgressiveRadius.Value),
            skill * Mathf.Max(0.01f, ProgressiveRadiusMultiplier.Value));
    }

    private void OnDestroy()
    {
        _jobs.Clear();
        _activeTargets.Clear();
        _harmony?.UnpatchSelf();
    }
}

internal sealed class MiningJob
{
    internal readonly int TargetInstanceId;
    internal readonly ZNetView View;
    internal readonly string RpcName;
    internal readonly HitData SourceHit;
    internal readonly Player Player;
    internal readonly ItemDrop.ItemData Weapon;
    internal readonly List<MiningTarget> Targets;
    internal int NextTarget;

    internal MiningJob(
        int targetInstanceId,
        ZNetView view,
        string rpcName,
        HitData sourceHit,
        Player player,
        ItemDrop.ItemData weapon,
        List<MiningTarget> targets)
    {
        TargetInstanceId = targetInstanceId;
        View = view;
        RpcName = rpcName;
        SourceHit = sourceHit;
        Player = player;
        Weapon = weapon;
        Targets = targets;
    }
}

internal readonly struct MiningTarget
{
    internal readonly int AreaIndex;
    internal readonly Collider Collider;
    internal readonly Vector3 Point;

    internal MiningTarget(int areaIndex, Collider collider, Vector3 point)
    {
        AreaIndex = areaIndex;
        Collider = collider;
        Point = point;
    }
}
