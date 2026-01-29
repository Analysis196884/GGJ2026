using Godot;
using System;
using System.Collections.Generic;
using MasqueradeArk.Core;

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
            int survivorCount = state.GetSurvivorCount();

            if (state.Supplies > 0)
            {
                // 物资充足：消耗 1 单位/幸存者
                state.Supplies -= survivorCount;
                if (state.Supplies < 0)
                    state.Supplies = 0;

                var evt = new GameEvent(
                    GameEvent.EventType.SuppliesConsumed,
                    state.Day,
                    $"消耗了 {survivorCount} 单位物资。"
                );
                events.Add(evt);
            }
            else
            {
                // 物资不足：增加饥饿与压力
                foreach (var survivor in state.Survivors)
                {
                    survivor.Hunger += 20;
                    survivor.Stress += 10;
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
                survivor.Hp -= 5;

                // 20% 概率被目击异常
                if (_rng.Randf() < 0.2f)
                {
                    survivor.Suspicion += (int)(_rng.Randf() * 15) + 5;
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
            if (survivor.Hunger > 80)
            {
                survivor.Hp -= 10;
                survivor.Stress += 15;

                var evt = new GameEvent(
                    GameEvent.EventType.Starvation,
                    state.Day,
                    $"{survivor.SurvivorName} 因严重饥饿而虚弱..."
                );
                evt.AddInvolvedNpc(survivor.SurvivorName);
                events.Add(evt);
            }

            // 精神崩溃规则
            if (survivor.Stress > 80)
            {
                if (_rng.Randf() < 0.3f)
                {
                    var evt = new GameEvent(
                        GameEvent.EventType.MentalBreakdown,
                        state.Day,
                        $"{survivor.SurvivorName} 精神崩溃了！"
                    );
                    evt.AddInvolvedNpc(survivor.SurvivorName);
                    events.Add(evt);

                    // 崩溃后压力减少
                    survivor.Stress = Math.Max(0, survivor.Stress - 30);
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
                float theftChance = survivor.Hunger * 0.5f / 100f;
                if (survivor.Integrity < 0)
                {
                    theftChance += 0.3f; // +30%
                }

                if (_rng.Randf() < theftChance && state.Supplies > 0)
                {
                    state.Supplies -= 1;
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
