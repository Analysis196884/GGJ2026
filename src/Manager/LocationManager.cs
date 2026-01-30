using Godot;
using System.Collections.Generic;
using MasqueradeArk.Core;
using MasqueradeArk.Utilities;

namespace MasqueradeArk.Manager
{
    [GlobalClass]
    public partial class LocationManager : Node
    {
        // 场所行动类型
        public enum LocationAction
        {
            UseMedicalWard,     // 使用医疗区
            UseRecreationRoom,  // 使用棋牌室
            UseRestArea,        // 使用休息区
            CheckStorageRoom    // 查看储藏室
        }

        /// <summary>
        /// 使用场所
        /// </summary>
        public bool UseLocation(GameState state, Survivor survivor, Location.LocationType locationType)
        {
            var location = state.GetLocation(locationType);
            if (location == null || !location.CanUse())
            {
                return false;
            }

            switch (locationType)
            {
                case Location.LocationType.MedicalWard:
                    return UseMedicalWard(state, survivor, location);
                case Location.LocationType.RecreationRoom:
                    return UseRecreationRoom(state, survivor, location);
                case Location.LocationType.RestArea:
                    return UseRestArea(state, survivor, location);
                case Location.LocationType.StorageRoom:
                    return CheckStorageRoom(state, survivor, location);
                default:
                    return false;
            }
        }

        /// <summary>
        /// 使用医疗区 - 治疗生命值
        /// </summary>
        private bool UseMedicalWard(GameState state, Survivor survivor, Location location)
        {
            if (survivor.Hp >= GameConstants.MAX_ATTRIBUTE_VALUE)
            {
                state.AppendLog($"{survivor.SurvivorName} 不需要治疗。");
                return false;
            }

            int healAmount = (int)(GameConstants.MEDICAL_WARD_HEAL * location.GetEfficiency());
            survivor.Hp = Mathf.Min(GameConstants.MAX_ATTRIBUTE_VALUE, survivor.Hp + healAmount);
            
            state.AppendLog($"{survivor.SurvivorName} 在医疗区接受治疗，恢复了 {healAmount} 点生命值。");
            return true;
        }

        /// <summary>
        /// 使用棋牌室 - 减少压力
        /// </summary>
        private bool UseRecreationRoom(GameState state, Survivor survivor, Location location)
        {
            if (survivor.Stress <= GameConstants.MIN_ATTRIBUTE_VALUE)
            {
                state.AppendLog($"{survivor.SurvivorName} 并不感到压力。");
                return false;
            }

            int stressReduction = (int)(GameConstants.RECREATION_STRESS_REDUCE * location.GetEfficiency());
            survivor.Stress = Mathf.Max(GameConstants.MIN_ATTRIBUTE_VALUE, survivor.Stress - stressReduction);
            
            state.AppendLog($"{survivor.SurvivorName} 在棋牌室放松娱乐，减少了 {stressReduction} 点压力。");
            return true;
        }

        /// <summary>
        /// 使用休息区 - 增加与其他幸存者的信任度
        /// </summary>
        private bool UseRestArea(GameState state, Survivor survivor, Location location)
        {
            bool hasInteraction = false;
            int trustIncrease = (int)(GameConstants.REST_AREA_TRUST_INCREASE * location.GetEfficiency());

            foreach (var otherSurvivor in state.Survivors)
            {
                if (otherSurvivor.SurvivorName != survivor.SurvivorName && otherSurvivor.Hp > 0)
                {
                    survivor.ModifyTrust(otherSurvivor.SurvivorName, trustIncrease);
                    otherSurvivor.ModifyTrust(survivor.SurvivorName, trustIncrease);
                    hasInteraction = true;
                }
            }

            if (hasInteraction)
            {
                state.AppendLog($"{survivor.SurvivorName} 在休息区与其他幸存者社交，增加了 {trustIncrease} 点信任度。");
                return true;
            }
            else
            {
                state.AppendLog($"{survivor.SurvivorName} 在休息区休息，但没有其他人可以交流。");
                return false;
            }
        }

        /// <summary>
        /// 查看储藏室 - 显示物资状况
        /// </summary>
        private bool CheckStorageRoom(GameState state, Survivor survivor, Location location)
        {
            if (!location.CanUse())
            {
                state.AppendLog($"{survivor.SurvivorName} 无法进入储藏室，可能已经损坏。");
                return false;
            }

            state.AppendLog($"{survivor.SurvivorName} 检查了储藏室，当前物资: {state.Supplies} 单位。");
            return true;
        }

        /// <summary>
        /// 修理场所
        /// </summary>
        public bool RepairLocation(GameState state, Survivor survivor, Location.LocationType locationType)
        {
            var location = state.GetLocation(locationType);
            if (location == null)
            {
                return false;
            }

            if (location.DamageLevel == 0)
            {
                state.AppendLog($"{location.Name} 状态良好，不需要修理。");
                return false;
            }

            // 工程师修理效果更好
            int repairEfficiency = GameConstants.REPAIR_EFFICIENCY;
            if (survivor.Role == "Engineer")
            {
                repairEfficiency = (int)(repairEfficiency * 1.5f);
            }

            location.Repair(repairEfficiency);
            
            if (location.DamageLevel < 80)
            {
                location.IsAvailable = true;
            }

            state.AppendLog($"{survivor.SurvivorName} 修理了{location.Name}，修复了 {repairEfficiency} 点损坏。");
            return true;
        }

        /// <summary>
        /// 获取可用的场所行动
        /// </summary>
        public List<string> GetAvailableLocationActions(GameState state)
        {
            var actions = new List<string>();
            
            foreach (var location in state.Locations)
            {
                if (location.CanUse())
                {
                    switch (location.Type)
                    {
                        case Location.LocationType.MedicalWard:
                            actions.Add("使用医疗区");
                            break;
                        case Location.LocationType.RecreationRoom:
                            actions.Add("使用棋牌室");
                            break;
                        case Location.LocationType.RestArea:
                            actions.Add("使用休息区");
                            break;
                        case Location.LocationType.StorageRoom:
                            actions.Add("查看储藏室");
                            break;
                    }
                }
            }

            // 添加修理选项
            foreach (var location in state.Locations)
            {
                if (location.DamageLevel > 0)
                {
                    actions.Add($"修理{location.Name}");
                }
            }

            return actions;
        }

        /// <summary>
        /// 执行场所行动
        /// </summary>
        public bool ExecuteLocationAction(GameState state, Survivor survivor, string actionName)
        {
            switch (actionName)
            {
                case "使用医疗区":
                    return UseLocation(state, survivor, Location.LocationType.MedicalWard);
                case "使用棋牌室":
                    return UseLocation(state, survivor, Location.LocationType.RecreationRoom);
                case "使用休息区":
                    return UseLocation(state, survivor, Location.LocationType.RestArea);
                case "查看储藏室":
                    return UseLocation(state, survivor, Location.LocationType.StorageRoom);
                default:
                    // 检查是否是修理行动
                    if (actionName.StartsWith("修理"))
                    {
                        string locationName = actionName.Substring(2);
                        var location = state.GetLocation(locationName);
                        if (location != null)
                        {
                            return RepairLocation(state, survivor, location.Type);
                        }
                    }
                    return false;
            }
        }
    }
}