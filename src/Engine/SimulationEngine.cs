using Godot;
using System;
using System.Collections.Generic;
using MasqueradeArk.Core;
using MasqueradeArk.Utilities;

namespace MasqueradeArk.Engine
{
    /// <summary>
    /// 模拟引擎 - 处理游戏逻辑与数值计算
    /// </summary>
    [GlobalClass]
    public partial class SimulationEngine : Node
    {
        private RandomNumberGenerator _rng = new();

        public SimulationEngine()
        {
            _rng.Randomize();
        }

        /// <summary>
        /// 推进一天，更新所有游戏状态
        /// </summary>
        public List<GameEvent> AdvanceDay(GameState state)
        {
            var events = new List<GameEvent>();

            // 1. 处理物资消耗
            ProcessSupplies(state, events);

            // 2. 更新所有幸存者状态
            foreach (var survivor in state.Survivors)
            {
                UpdateSurvivorState(state, survivor, events);
            }

            // 3. 检查秘密事件
            ProcessMasqueradeEvents(state, events);

            // 4. 推进天数
            state.Day++;

            return events;
        }

        /// <summary>
        /// 处理物资消耗规则
        /// </summary>
        private void ProcessSupplies(GameState state, List<GameEvent> events)
        {
            int survivorCount = state.GetAliveSurvivorCount();

            if (state.Supplies >= survivorCount * GameConstants.DAILY_SUPPLIES_PER_SURVIVOR)
            {
                // 物资充足：消耗配额
                int consumption = survivorCount * GameConstants.DAILY_SUPPLIES_PER_SURVIVOR;
                state.Supplies -= consumption;
                
                // 重置饥饿度
                foreach (var survivor in state.Survivors)
                {
                    if (survivor.Hp > 0)
                    {
                        survivor.Hunger = 0;
                    }
                }

                var evt = new GameEvent(
                    GameEvent.EventType.SuppliesConsumed,
                    state.Day,
                    $"消耗了 {consumption} 单位物资。"
                );
                events.Add(evt);
            }
            else
            {
                // 物资不足：增加饥饿与压力
                foreach (var survivor in state.Survivors)
                {
                    if (survivor.Hp > 0)
                    {
                        survivor.Hunger += GameConstants.STARVATION_HUNGER_INCREASE;
                        survivor.Stress += GameConstants.STARVATION_STRESS_INCREASE;
                    }
                }

                var evt = new GameEvent(
                    GameEvent.EventType.Starvation,
                    state.Day,
                    "物资耗尽。幸存者开始挨饿..."
                );
                events.Add(evt);
            }
        }

        /// <summary>
        /// 更新单个幸存者的状态
        /// </summary>
        private void UpdateSurvivorState(GameState state, Survivor survivor, List<GameEvent> events)
        {
            // 感染恶化
            if (survivor.HasSecret("Infected"))
            {
                survivor.Hp -= GameConstants.INFECTION_DAILY_DAMAGE;

                // 检查是否被目击异常
                if (_rng.Randf() < GameConstants.INFECTION_DETECTION_CHANCE)
                {
                    int suspicionIncrease = (int)(_rng.Randf() *
                        (GameConstants.MAX_SUSPICION_INCREASE - GameConstants.MIN_SUSPICION_INCREASE)) +
                        GameConstants.MIN_SUSPICION_INCREASE;
                    survivor.Suspicion += suspicionIncrease;
                    
                    var evt = new GameEvent(
                        GameEvent.EventType.InfectionDetected,
                        state.Day,
                        $"{survivor.SurvivorName} 出现了异常迹象..."
                    );
                    evt.AddInvolvedNpc(survivor.SurvivorName);
                    events.Add(evt);
                }
            }

            // 饥饿恶化规则
            if (survivor.Hunger > GameConstants.HUNGER_CRITICAL_THRESHOLD)
            {
                survivor.Hp -= GameConstants.HUNGER_DAMAGE;
                survivor.Stress += GameConstants.HUNGER_STRESS_PENALTY;

                var evt = new GameEvent(
                    GameEvent.EventType.Starvation,
                    state.Day,
                    $"{survivor.SurvivorName} 因严重饥饿而虚弱..."
                );
                evt.AddInvolvedNpc(survivor.SurvivorName);
                events.Add(evt);
            }

            // 精神崩溃规则
            if (survivor.Stress > GameConstants.STRESS_BREAKDOWN_THRESHOLD)
            {
                if (_rng.Randf() < GameConstants.BREAKDOWN_CHANCE)
                {
                    var evt = new GameEvent(
                        GameEvent.EventType.MentalBreakdown,
                        state.Day,
                        $"{survivor.SurvivorName} 精神崩溃了！"
                    );
                    evt.AddInvolvedNpc(survivor.SurvivorName);
                    events.Add(evt);

                    // 崩溃后压力减少
                    survivor.Stress = Math.Max(0, survivor.Stress - GameConstants.BREAKDOWN_STRESS_RELIEF);
                }
            }

            // 检查死亡
            if (survivor.Hp <= 0)
            {
                var evt = new GameEvent(
                    GameEvent.EventType.Death,
                    state.Day,
                    $"{survivor.SurvivorName} 已经去世..."
                );
                evt.AddInvolvedNpc(survivor.SurvivorName);
                events.Add(evt);
            }

            survivor.ClampValues();
        }

        /// <summary>
        /// 处理秘密事件（偷窃、感染等）
        /// </summary>
        private void ProcessMasqueradeEvents(GameState state, List<GameEvent> events)
        {
            // 遍历每个幸存者，检查是否会偷窃
            foreach (var survivor in state.Survivors)
            {
                if (survivor.Hp <= 0)
                    continue;

                // 偷窃判定
                float theftChance = survivor.Hunger * GameConstants.HUNGER_THEFT_MULTIPLIER / 100f;
                if (survivor.Integrity < 0)
                {
                    theftChance += GameConstants.LOW_INTEGRITY_THEFT_BONUS;
                }

                if (_rng.Randf() < theftChance && state.Supplies > 0)
                {
                    state.Supplies -= GameConstants.THEFT_AMOUNT;
                    var evt = new GameEvent(
                        GameEvent.EventType.SuppliesStolen,
                        state.Day,
                        "发现物资被盗！但没有线索指向罪犯..."
                    );
                    // 不记录是谁偷的 - 这是秘密
                    events.Add(evt);
                }
            }
        }

        /// <summary>
        /// 为新创建的幸存者随机分配秘密
        /// </summary>
        public void AssignRandomSecrets(Survivor survivor)
        {
            // 感染概率检查
            if (_rng.Randf() < GameConstants.INFECTED_SPAWN_CHANCE)
            {
                survivor.AddSecret("Infected");
                GD.Print($"[SimulationEngine] {survivor.SurvivorName} 被分配了秘密：Infected");
            }

            // 小偷概率检查
            if (_rng.Randf() < GameConstants.THIEF_SPAWN_CHANCE)
            {
                survivor.AddSecret("Thief");
                survivor.Integrity -= 20; // 小偷道德值更低
                GD.Print($"[SimulationEngine] {survivor.SurvivorName} 被分配了秘密：Thief");
            }
        }

        /// <summary>
        /// 批量为所有幸存者分配随机秘密
        /// </summary>
        public void AssignRandomSecretsToAll(GameState state)
        {
            foreach (var survivor in state.Survivors)
            {
                AssignRandomSecrets(survivor);
            }
        }

        /// <summary>
        /// 手动投票决策 - 驱逐幸存者
        /// </summary>
        public void ExileSurvivor(GameState state, string survivorName)
        {
            var survivor = state.GetSurvivor(survivorName);
            if (survivor != null)
            {
                survivor.Hp = 0;
                state.AppendLog($"{survivorName} 被团队驱逐出去了...");
            }
        }

        /// <summary>
        /// 手动赋予幸存者秘密
        /// </summary>
        public void SetSecret(Survivor survivor, string secretName)
        {
            survivor.AddSecret(secretName);
        }

        /// <summary>
        /// 修改幸存者的基础属性（用于调试或特殊事件）
        /// </summary>
        public void ModifySurvivor(Survivor survivor, string attribute, int delta)
        {
            switch (attribute.ToLower())
            {
                case "hp":
                    survivor.Hp += delta;
                    break;
                case "hunger":
                    survivor.Hunger += delta;
                    break;
                case "stress":
                    survivor.Stress += delta;
                    break;
                case "integrity":
                    survivor.Integrity += delta;
                    break;
                case "suspicion":
                    survivor.Suspicion += delta;
                    break;
                case "stamina":
                    survivor.Stamina += delta;
                    break;
            }

            survivor.ClampValues();
        }
    }
}
