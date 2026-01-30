using Godot;
using System.Collections.Generic;
using MasqueradeArk.Core;
using MasqueradeArk.Utilities;

namespace MasqueradeArk.Manager
{
    [GlobalClass]
    public partial class TaskManager : Node
    {
        // 任务类型
        public enum TaskType
        {
            Patrol,         // 巡逻
            RepairFacility, // 修理设施
            ScavengeSupply, // 搜寻物资
            GuardDuty,      // 站岗
            MaintainEquip,  // 维护装备
            SocialSupport   // 心理支持
        }

        // 任务状态
        public enum TaskStatus
        {
            Available,      // 可用
            InProgress,     // 进行中
            Completed,      // 已完成
            Failed          // 失败
        }

        // 任务数据结构
        public class Task
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public TaskType Type { get; set; }
            public TaskStatus Status { get; set; }
            public string AssignedSurvivor { get; set; }
            public int Duration { get; set; } // 持续天数
            public int Progress { get; set; } // 进度
            public Dictionary<string, object> Requirements { get; set; }
            public Dictionary<string, object> Rewards { get; set; }

            public Task()
            {
                Requirements = new Dictionary<string, object>();
                Rewards = new Dictionary<string, object>();
                Status = TaskStatus.Available;
                Progress = 0;
            }
        }

        private List<Task> _activeTasks = new List<Task>();
        private RandomNumberGenerator _rng = new RandomNumberGenerator();

        public override void _Ready()
        {
            _rng.Randomize();
        }

        /// <summary>
        /// 生成可用任务
        /// </summary>
        public List<Task> GenerateAvailableTasks(GameState state)
        {
            var tasks = new List<Task>();

            // 巡逻任务
            if (state.Defense < GameConstants.INITIAL_DEFENSE)
            {
                tasks.Add(new Task
                {
                    Id = "patrol_" + state.Day,
                    Name = "庇护所巡逻",
                    Description = "派遣幸存者巡逻庇护所周围，提高防御值",
                    Type = TaskType.Patrol,
                    Duration = 1,
                    Requirements = new Dictionary<string, object> { { "minStamina", 50 } },
                    Rewards = new Dictionary<string, object> { { "defenseBonus", GameConstants.PATROL_DEFENSE_BONUS } }
                });
            }

            // 修理任务
            foreach (var location in state.Locations)
            {
                if (location.DamageLevel > 0)
                {
                    tasks.Add(new Task
                    {
                        Id = "repair_" + location.Name + "_" + state.Day,
                        Name = $"修理{location.Name}",
                        Description = $"修理损坏的{location.Name}，恢复其功能",
                        Type = TaskType.RepairFacility,
                        Duration = 2,
                        Requirements = new Dictionary<string, object> { { "targetLocation", location.Name } },
                        Rewards = new Dictionary<string, object> { { "repairAmount", GameConstants.REPAIR_EFFICIENCY } }
                    });
                }
            }

            // 搜寻物资任务
            if (state.Supplies < 20)
            {
                tasks.Add(new Task
                {
                    Id = "scavenge_" + state.Day,
                    Name = "搜寻物资",
                    Description = "外出寻找食物和用品",
                    Type = TaskType.ScavengeSupply,
                    Duration = 1,
                    Requirements = new Dictionary<string, object> { { "minStamina", 60 } },
                    Rewards = new Dictionary<string, object> { { "supplies", _rng.RandiRange(3, 8) } }
                });
            }

            // 站岗任务
            tasks.Add(new Task
            {
                Id = "guard_" + state.Day,
                Name = "站岗值守",
                Description = "在关键位置站岗，防范威胁",
                Type = TaskType.GuardDuty,
                Duration = 1,
                Requirements = new Dictionary<string, object> { { "minStamina", 40 } },
                Rewards = new Dictionary<string, object> { { "stressReduction", 5 } }
            });

            // 心理支持任务
            if (CalculateAverageStress(state) > 50)
            {
                tasks.Add(new Task
                {
                    Id = "support_" + state.Day,
                    Name = "心理支持",
                    Description = "帮助其他幸存者缓解心理压力",
                    Type = TaskType.SocialSupport,
                    Duration = 1,
                    Requirements = new Dictionary<string, object> { { "minIntegrity", 20 } },
                    Rewards = new Dictionary<string, object> { { "teamStressReduction", 10 } }
                });
            }

            return tasks;
        }

        /// <summary>
        /// 分配任务给幸存者
        /// </summary>
        public bool AssignTask(GameState state, string taskId, string survivorName)
        {
            var availableTasks = GenerateAvailableTasks(state);
            var task = availableTasks.Find(t => t.Id == taskId);
            var survivor = state.GetSurvivor(survivorName);

            if (task == null || survivor == null || survivor.Hp <= 0)
            {
                return false;
            }

            // 检查任务需求
            if (!CheckTaskRequirements(task, survivor))
            {
                state.AppendLog($"{survivorName} 不满足 {task.Name} 的要求。");
                return false;
            }

            // 检查幸存者是否信任玩家（拒绝事件）
            if (survivor.GetTrust(GameConstants.PLAYER_NAME) < 20)
            {
                if (_rng.Randf() < GameConstants.REFUSE_EVENT_CHANCE)
                {
                    state.AppendLog($"{survivorName} 拒绝执行 {task.Name}，因为不信任你的决策。");
                    return false;
                }
            }

            task.AssignedSurvivor = survivorName;
            task.Status = TaskStatus.InProgress;
            _activeTasks.Add(task);

            state.AppendLog($"{survivorName} 开始执行任务：{task.Name}");
            return true;
        }

        /// <summary>
        /// 处理进行中的任务
        /// </summary>
        public List<GameEvent> ProcessActiveTasks(GameState state)
        {
            var events = new List<GameEvent>();
            var completedTasks = new List<Task>();

            foreach (var task in _activeTasks)
            {
                var survivor = state.GetSurvivor(task.AssignedSurvivor);
                if (survivor == null || survivor.Hp <= 0)
                {
                    task.Status = TaskStatus.Failed;
                    completedTasks.Add(task);
                    continue;
                }

                task.Progress++;
                
                if (task.Progress >= task.Duration)
                {
                    // 任务完成
                    task.Status = TaskStatus.Completed;
                    ApplyTaskRewards(state, task, survivor, events);
                    completedTasks.Add(task);
                }
                else
                {
                    // 任务进行中，消耗体力
                    survivor.Stamina = Mathf.Max(0, survivor.Stamina - 10);
                }
            }

            // 移除已完成的任务
            foreach (var completedTask in completedTasks)
            {
                _activeTasks.Remove(completedTask);
            }

            return events;
        }

        /// <summary>
        /// 检查任务需求
        /// </summary>
        private bool CheckTaskRequirements(Task task, Survivor survivor)
        {
            foreach (var requirement in task.Requirements)
            {
                switch (requirement.Key)
                {
                    case "minStamina":
                        if (survivor.Stamina < (int)requirement.Value)
                            return false;
                        break;
                    case "minIntegrity":
                        if (survivor.Integrity < (int)requirement.Value)
                            return false;
                        break;
                    case "role":
                        if (survivor.Role != (string)requirement.Value)
                            return false;
                        break;
                }
            }
            return true;
        }

        /// <summary>
        /// 应用任务奖励
        /// </summary>
        private void ApplyTaskRewards(GameState state, Task task, Survivor survivor, List<GameEvent> events)
        {
            string rewardText = $"{survivor.SurvivorName} 完成了任务：{task.Name}。";

            foreach (var reward in task.Rewards)
            {
                switch (reward.Key)
                {
                    case "defenseBonus":
                        int defenseBonus = (int)reward.Value;
                        if (survivor.Role == "Mercenary")
                            defenseBonus = (int)(defenseBonus * 1.5f); // 雇佣兵巡逻效果更好
                        state.Defense += defenseBonus;
                        rewardText += $" 庇护所防御值增加 {defenseBonus}。";
                        break;

                    case "repairAmount":
                        var locationName = task.Requirements["targetLocation"] as string;
                        var location = state.GetLocation(locationName);
                        if (location != null)
                        {
                            int repairAmount = (int)reward.Value;
                            if (survivor.Role == "Engineer")
                                repairAmount = (int)(repairAmount * 1.5f); // 工程师修理效果更好
                            location.Repair(repairAmount);
                            rewardText += $" {location.Name} 修复了 {repairAmount} 点损坏。";
                        }
                        break;

                    case "supplies":
                        int supplies = (int)reward.Value;
                        if (survivor.Role == "Scout")
                            supplies = (int)(supplies * 1.2f); // 侦察员搜寻效果更好
                        state.Supplies += supplies;
                        rewardText += $" 获得了 {supplies} 单位物资。";
                        break;

                    case "stressReduction":
                        int stressReduction = (int)reward.Value;
                        survivor.Stress = Mathf.Max(0, survivor.Stress - stressReduction);
                        rewardText += $" 减少了 {stressReduction} 点压力。";
                        break;

                    case "teamStressReduction":
                        int teamStressReduction = (int)reward.Value;
                        foreach (var teamMember in state.Survivors)
                        {
                            if (teamMember.Hp > 0)
                            {
                                teamMember.Stress = Mathf.Max(0, teamMember.Stress - teamStressReduction);
                            }
                        }
                        rewardText += $" 团队整体压力减少了 {teamStressReduction}。";
                        break;
                }
            }

            var evt = new GameEvent(GameEvent.EventType.Custom, state.Day, rewardText);
            evt.AddInvolvedNpc(survivor.SurvivorName);
            events.Add(evt);

            state.AppendLog(rewardText);
        }

        /// <summary>
        /// 获取当前活跃任务
        /// </summary>
        public List<Task> GetActiveTasks()
        {
            return new List<Task>(_activeTasks);
        }

        /// <summary>
        /// 计算平均压力
        /// </summary>
        private float CalculateAverageStress(GameState state)
        {
            int totalStress = 0;
            int aliveCount = 0;

            foreach (var survivor in state.Survivors)
            {
                if (survivor.Hp > 0)
                {
                    totalStress += survivor.Stress;
                    aliveCount++;
                }
            }

            return aliveCount > 0 ? (float)totalStress / aliveCount : 0f;
        }
    }
}