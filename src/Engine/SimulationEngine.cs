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
        
        // 日志回调 - 用于同步日志到 UI
        public Action<string>? LogCallback { get; set; }

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

            // 5. 随机事件检查
            ProcessRandomEvents(state, events);

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
                    int trustDecrease = (int)(_rng.Randf() *
                        (GameConstants.MAX_TRUST_DECREASE - GameConstants.MIN_TRUST_DECREASE)) +
                        GameConstants.MIN_TRUST_DECREASE;
                    survivor.Trust -= trustDecrease;
                    
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
                LogCallback?.Invoke($"{survivorName} 被团队驱逐出去了...");
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
                case "trust":
                    survivor.Trust += delta;
                    break;
                case "stamina":
                    survivor.Stamina += delta;
                    break;
            }

            survivor.ClampValues();
        }

        /// <summary>
        /// 处理随机事件系统
        /// </summary>
        private void ProcessRandomEvents(GameState state, List<GameEvent> events)
        {
            // 基础随机事件概率
            if (_rng.Randf() < GameConstants.RANDOM_EVENT_CHANCE)
            {
                TriggerRandomEvent(state, events);
            }

            // 压力相关事件：当团队整体压力过高时增加概率
            float avgStress = CalculateAverageStress(state);
            if (avgStress > GameConstants.STRESS_BREAKDOWN_THRESHOLD && _rng.Randf() < GameConstants.STRESS_EVENT_CHANCE)
            {
                TriggerStressEvent(state, events);
            }

            // 物资短缺相关事件
            if (state.Supplies < 10 && _rng.Randf() < GameConstants.SUPPLY_EVENT_CHANCE)
            {
                TriggerSupplyEvent(state, events);
            }
            
            // 丧尸相关事件
            if (_rng.Randf() < GameConstants.ZOMBIE_EVENT_CHANCE)
            {
                TriggerZombieEvent(state, events);
            }
            
            // 偷窃事件（基于饥饿值）
            if (_rng.Randf() < GameConstants.THEFT_EVENT_CHANCE)
            {
                TriggerTheftEvent(state, events);
            }
            
            // 破坏事件（基于高压力）
            if (avgStress > 60 && _rng.Randf() < GameConstants.SABOTAGE_EVENT_CHANCE)
            {
                TriggerSabotageEvent(state, events);
            }
            
            // 拒绝事件（基于低信任度）
            TriggerRefuseEvent(state, events);
        }

        /// <summary>
        /// 触发一般随机事件
        /// </summary>
        private void TriggerRandomEvent(GameState state, List<GameEvent> events)
        {
            var randomEvents = new string[]
            {
                "发现了一些废弃的医疗用品",
                "外面传来了奇怪的声音",
                "基地的一扇窗户突然破了",
                "有人在夜里听到了远处的枪声",
                "发现了前人留下的求救信号"
            };

            var eventDescription = randomEvents[_rng.Randi() % randomEvents.Length];
            var evt = new GameEvent(GameEvent.EventType.Custom, state.Day, eventDescription);

            // 根据事件类型添加不同效果
            if (eventDescription.Contains("医疗用品"))
            {
                // 找到医生并恢复一些HP给团队
                foreach (var survivor in state.Survivors)
                {
                    if (survivor.Role == "Doctor" && survivor.Hp > 0)
                    {
                        foreach (var target in state.Survivors)
                        {
                            if (target.Hp > 0 && target.Hp < 100)
                            {
                                target.Hp += 10;
                            }
                        }
                        break;
                    }
                }
            }
            else if (eventDescription.Contains("奇怪的声音") || eventDescription.Contains("枪声"))
            {
                // 增加所有人的压力
                foreach (var survivor in state.Survivors)
                {
                    if (survivor.Hp > 0)
                    {
                        survivor.Stress += 5;
                    }
                }
            }

            events.Add(evt);
        }

        /// <summary>
        /// 触发压力相关事件
        /// </summary>
        private void TriggerStressEvent(GameState state, List<GameEvent> events)
        {
            var stressEvents = new string[]
            {
                "团队内部发生了激烈争吵",
                "有人开始质疑领导决策",
                "营地里弥漫着不安的气氛",
                "有人提议分散行动"
            };

            var eventDescription = stressEvents[_rng.Randi() % stressEvents.Length];
            var evt = new GameEvent(GameEvent.EventType.Custom, state.Day, eventDescription);

            // 随机降低一些人之间的信任度
            var survivors = new List<Survivor>();
            foreach (var s in state.Survivors)
            {
                if (s.Hp > 0) survivors.Add(s);
            }

            if (survivors.Count >= 2)
            {
                var survivor1 = survivors[(int)(_rng.Randi() % survivors.Count)];
                var survivor2 = survivors[(int)(_rng.Randi() % survivors.Count)];
                
                if (survivor1 != survivor2)
                {
                    survivor1.ModifyTrust(survivor2.SurvivorName, -10);
                    survivor2.ModifyTrust(survivor1.SurvivorName, -10);
                }
            }

            events.Add(evt);
        }

        /// <summary>
        /// 触发物资相关事件
        /// </summary>
        private void TriggerSupplyEvent(GameState state, List<GameEvent> events)
        {
            var supplyEvents = new string[]
            {
                "在废墟中找到了少量食物",
                "有人提议外出寻找补给",
                "讨论是否需要节约用度",
                "发现了一些可以利用的材料"
            };

            var eventDescription = supplyEvents[_rng.Randi() % supplyEvents.Length];
            var evt = new GameEvent(GameEvent.EventType.Custom, state.Day, eventDescription);

            if (eventDescription.Contains("找到了"))
            {
                // 小概率增加物资
                state.Supplies += (int)(_rng.Randi() % 5) + 1;
            }

            events.Add(evt);
        }

        /// <summary>
        /// 计算团队平均压力值
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

        /// <summary>
        /// 触发丧尸相关事件
        /// </summary>
        private void TriggerZombieEvent(GameState state, List<GameEvent> events)
        {
            var zombieEvents = new string[]
            {
                "丧尸群袭击庇护所",
                "发现丧尸踪迹",
                "庇护所围墙被破坏",
                "丧尸攻击幸存者"
            };

            var eventDescription = zombieEvents[_rng.Randi() % zombieEvents.Length];
            var evt = new GameEvent(GameEvent.EventType.Custom, state.Day, eventDescription);

            if (eventDescription.Contains("袭击庇护所"))
            {
                // 降低庇护所防御值
                state.Defense -= GameConstants.ZOMBIE_DEFENSE_DAMAGE;
                state.Defense = Math.Max(0, state.Defense);
                
                // 随机攻击幸存者
                var aliveSurvivors = new List<Survivor>();
                foreach (var s in state.Survivors)
                {
                    if (s.Hp > 0) aliveSurvivors.Add(s);
                }
                
                if (aliveSurvivors.Count > 0)
                {
                    var target = aliveSurvivors[(int)(_rng.Randi() % aliveSurvivors.Count)];
                    int damage = (int)(_rng.Randi() % (GameConstants.ZOMBIE_DAMAGE_MAX - GameConstants.ZOMBIE_DAMAGE_MIN)) + GameConstants.ZOMBIE_DAMAGE_MIN;
                    target.Hp -= damage;
                    
                    // 感染概率
                    if (_rng.Randf() < GameConstants.ZOMBIE_INFECTION_CHANCE)
                    {
                        target.AddSecret("Infected");
                        evt.SetContextValue("infected", target.SurvivorName);
                    }
                    
                    evt.AddInvolvedNpc(target.SurvivorName);
                }
            }
            else if (eventDescription.Contains("围墙被破坏"))
            {
                state.Defense -= GameConstants.ZOMBIE_DEFENSE_DAMAGE * 2;
                state.Defense = Math.Max(0, state.Defense);
            }

            events.Add(evt);
        }

        /// <summary>
        /// 触发偷窃事件
        /// </summary>
        private void TriggerTheftEvent(GameState state, List<GameEvent> events)
        {
            var hungriestSurvivor = GetMostHungrySurvivor(state);
            if (hungriestSurvivor != null && hungriestSurvivor.Hunger > 50 && state.Supplies > 0)
            {
                state.Supplies -= GameConstants.THEFT_AMOUNT;
                hungriestSurvivor.Hunger = Math.Max(0, hungriestSurvivor.Hunger - 20);
                
                var evt = new GameEvent(
                    GameEvent.EventType.SuppliesStolen,
                    state.Day,
                    $"发现物资被偷！"
                );
                evt.AddInvolvedNpc(hungriestSurvivor.SurvivorName);
                events.Add(evt);
            }
        }

        /// <summary>
        /// 触发破坏事件
        /// </summary>
        private void TriggerSabotageEvent(GameState state, List<GameEvent> events)
        {
            var stressedSurvivor = GetMostStressedSurvivor(state);
            if (stressedSurvivor != null && stressedSurvivor.Stress > 70)
            {
                // 随机破坏一个场所
                var availableLocations = new List<Location>();
                foreach (var loc in state.Locations)
                {
                    if (loc.CanUse()) availableLocations.Add(loc);
                }
                
                if (availableLocations.Count > 0)
                {
                    var targetLocation = availableLocations[(int)(_rng.Randi() % availableLocations.Count)];
                    targetLocation.Damage(30);
                    
                    var evt = new GameEvent(
                        GameEvent.EventType.Custom,
                        state.Day,
                        $"{stressedSurvivor.SurvivorName} 在愤怒中破坏了{targetLocation.Name}！"
                    );
                    evt.AddInvolvedNpc(stressedSurvivor.SurvivorName);
                    events.Add(evt);
                    
                    // 减少破坏者的压力
                    stressedSurvivor.Stress = Math.Max(0, stressedSurvivor.Stress - 20);
                }
            }
        }

        /// <summary>
        /// 触发拒绝事件 - 基于信任值
        /// </summary>
        private void TriggerRefuseEvent(GameState state, List<GameEvent> events)
        {
            var refuseActions = new string[]
            {
                "拒绝参与巡逻",
                "拒绝分享物资",
                "拒绝协助修理",
                "拒绝参加会议"
            };
            
            // 寻找对玩家信任度最低的幸存者
            Survivor lowTrustSurvivor = null;
            int lowestTrust = 100;
            
            foreach (var survivor in state.Survivors)
            {
                if (survivor.Hp > 0)
                {
                    int playerTrust = survivor.GetTrust(GameConstants.PLAYER_NAME);
                    if (playerTrust < lowestTrust)
                    {
                        lowestTrust = playerTrust;
                        lowTrustSurvivor = survivor;
                    }
                }
            }
            
            // 当信任度低于30%时触发拒绝事件
            if (lowTrustSurvivor != null && lowestTrust < 30)
            {
                var action = refuseActions[_rng.Randi() % refuseActions.Length];
                var evt = new GameEvent(
                    GameEvent.EventType.Custom,
                    state.Day,
                    $"{lowTrustSurvivor.SurvivorName} {action}（信任值: {lowestTrust}%）。"
                );
                evt.AddInvolvedNpc(lowTrustSurvivor.SurvivorName);
                events.Add(evt);
            }
        }

        /// <summary>
        /// 获取饥饿值最高的幸存者
        /// </summary>
        private Survivor GetMostHungrySurvivor(GameState state)
        {
            Survivor hungriest = null;
            int maxHunger = 0;
            
            foreach (var survivor in state.Survivors)
            {
                if (survivor.Hp > 0 && survivor.Hunger > maxHunger)
                {
                    maxHunger = survivor.Hunger;
                    hungriest = survivor;
                }
            }
            
            return hungriest;
        }

        /// <summary>
        /// 获取压力值最高的幸存者
        /// </summary>
        private Survivor GetMostStressedSurvivor(GameState state)
        {
            Survivor mostStressed = null;
            int maxStress = 0;
            
            foreach (var survivor in state.Survivors)
            {
                if (survivor.Hp > 0 && survivor.Stress > maxStress)
                {
                    maxStress = survivor.Stress;
                    mostStressed = survivor;
                }
            }
            
            return mostStressed;
        }
    }
}
