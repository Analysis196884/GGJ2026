using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MasqueradeArk.Core;

namespace MasqueradeArk.Engine
{
    /// <summary>
    /// 叙事引擎 - 使用 LLM 将数值事件转化为情感化的文本叙事
    /// 本原型使用预定义的模板，实际可集成 LLM API
    /// </summary>
    [GlobalClass]
    public partial class NarrativeEngine : Node
    {
        private RandomNumberGenerator _rng = new();
        private LLMClient _llmClient;

        public struct NarrativeResult
        {
            public string NarrativeText;
            public string[] Choices;
        }

        public NarrativeEngine()
        {
            _rng.Randomize();
        }

        public override void _Ready()
        {
            base._Ready();
            // 尝试从场景树中获取 LLMClient，如果不存在则创建默认实例
            _llmClient = GetNodeOrNull<LLMClient>("../LLMClient");
            if (_llmClient == null)
            {
                _llmClient = new LLMClient();
                AddChild(_llmClient);
                GD.Print("[NarrativeEngine] 未找到 LLMClient，已创建默认实例");
            }
        }

        /// <summary>
        /// 生成事件的叙事文本（异步）
        /// </summary>
        public async Task<NarrativeResult> GenerateEventNarrativeAsync(GameEvent gameEvent, GameState state)
        {
            var result = new NarrativeResult
            {
                NarrativeText = "",
                Choices = []
            };

            // 如果 LLM 客户端启用且未模拟，尝试使用 LLM 生成
            if (_llmClient != null && _llmClient.Enabled && !_llmClient.Simulate)
            {
                try
                {
                    string llmText = await _llmClient.GenerateEventNarrativeAsync(gameEvent.Type.ToString(), gameEvent.Description, state);
                    if (!string.IsNullOrEmpty(llmText))
                    {
                        result.NarrativeText = llmText;
                        // 保留原有选择（可根据需要调整）
                        SetChoicesByEventType(gameEvent.Type, ref result);
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[NarrativeEngine] LLM 生成失败：{ex.Message}");
                    // 回退到模板
                }
            }

            // 使用模板生成
            switch (gameEvent.Type)
            {
                case GameEvent.EventType.SuppliesConsumed:
                    result.NarrativeText = GenerateSuppliesNarrative(gameEvent, state);
                    break;

                case GameEvent.EventType.SuppliesStolen:
                    result.NarrativeText = GenerateTheftNarrative(gameEvent, state);
                    result.Choices = new string[] { "继续调查", "保持警惕", "试图推理" };
                    break;

                case GameEvent.EventType.Starvation:
                    result.NarrativeText = GenerateStarvationNarrative(gameEvent, state);
                    break;

                case GameEvent.EventType.MentalBreakdown:
                    result.NarrativeText = GenerateMentalBreakdownNarrative(gameEvent, state);
                    result.Choices = new string[] { "安慰他们", "保持距离", "表示关切" };
                    break;

                case GameEvent.EventType.InfectionDetected:
                    result.NarrativeText = GenerateInfectionNarrative(gameEvent, state);
                    result.Choices = new string[] { "质问此人", "私下交谈", "装作未察觉" };
                    break;

                case GameEvent.EventType.Death:
                    result.NarrativeText = GenerateDeathNarrative(gameEvent, state);
                    break;

                case GameEvent.EventType.Illness:
                    result.NarrativeText = GenerateIllnessNarrative(gameEvent, state);
                    break;

                default:
                    result.NarrativeText = gameEvent.Description;
                    break;
            }

            return result;
        }

        /// <summary>
        /// 根据事件类型设置选项（用于LLM生成时保留选项）
        /// </summary>
        private void SetChoicesByEventType(GameEvent.EventType eventType, ref NarrativeResult result)
        {
            switch (eventType)
            {
                case GameEvent.EventType.SuppliesStolen:
                    result.Choices = new string[] { "继续调查", "保持警惕", "试图推理" };
                    break;
                case GameEvent.EventType.MentalBreakdown:
                    result.Choices = new string[] { "安慰他们", "保持距离", "表示关切" };
                    break;
                case GameEvent.EventType.InfectionDetected:
                    result.Choices = new string[] { "质问此人", "私下交谈", "装作未察觉" };
                    break;
                default:
                    result.Choices = [];
                    break;
            }
        }

        /// <summary>
        /// 生成事件的叙事文本（同步兼容版本）
        /// </summary>
        public NarrativeResult GenerateEventNarrative(GameEvent gameEvent, GameState state)
        {
            // 同步调用异步方法（可能阻塞，仅用于兼容）
            var task = GenerateEventNarrativeAsync(gameEvent, state);
            task.Wait();
            return task.Result;
        }

        private string GenerateSuppliesNarrative(GameEvent evt, GameState state)
        {
            var templates = new[]
            {
                "又是无聊的一天。物资被分配下去了。",
                "配给完毕。每个人都得到了应有的份额。",
                "物资库存还算充足。暂时不用担心..."
            };
            return templates[_rng.Randi() % templates.Length];
        }

        private string GenerateTheftNarrative(GameEvent evt, GameState state)
        {
            var templates = new[]
            {
                "发现了缺少的物资。似乎有人在半夜里动了歪脑筋。房间里弥漫着紧张的气氛。",
                "又发现物资少了。这已经是这个月第二次了。有人窃窃私语。",
                "物资库存不符。没有人承认。每个人的眼神都躲躲闪闪。",
                "又是物资被盗。沉默笼罩了营地。信任在一点点瓦解。"
            };
            return templates[_rng.Randi() % templates.Length];
        }

        private string GenerateStarvationNarrative(GameEvent evt, GameState state)
        {
            if (evt.InvolvedNpcs.Count > 0)
            {
                var npcName = evt.InvolvedNpcs[0];
                var templates = new[]
                {
                    $"{npcName} 看起来很虚弱。眼神变得呆滞。",
                    $"{npcName} 在角落里蜷缩成一团。呼吸变得沉重。",
                    $"{npcName} 拒绝进食。声音沙哑而微弱。"
                };
                return templates[_rng.Randi() % templates.Length];
            }

            var stateTemplates = new[]
            {
                "食物快没了。人们开始互相监视。",
                "饥饿感弥漫在营地里。有人开始做梦。"
            };
            return stateTemplates[_rng.Randi() % stateTemplates.Length];
        }

        private string GenerateMentalBreakdownNarrative(GameEvent evt, GameState state)
        {
            if (evt.InvolvedNpcs.Count > 0)
            {
                var npcName = evt.InvolvedNpcs[0];
                var templates = new[]
                {
                    $"{npcName} 突然尖叫了一声。眼神里充满了恐惧和疯狂。",
                    $"{npcName} 在地上打滚，喃喃自语。无人敢靠近。",
                    $"{npcName} 重复同一句话，一遍又一遍。压抑的氛围笼罩了整个营地。"
                };
                return templates[_rng.Randi() % templates.Length];
            }

            return "有人的理智线崩断了。营地陷入了一片混乱。";
        }

        private string GenerateInfectionNarrative(GameEvent evt, GameState state)
        {
            if (evt.InvolvedNpcs.Count > 0)
            {
                var npcName = evt.InvolvedNpcs[0];
                var templates = new[]
                {
                    $"{npcName} 在夜里咳嗽个不停。那声音...听起来不太对劲。",
                    $"{npcName} 的皮肤有些异常。没人敢靠得太近。",
                    $"{npcName} 最近很少和人说话。总是躲在阴暗的角落里。"
                };
                return templates[_rng.Randi() % templates.Length];
            }

            return "营地里弥漫着一种不祥的气息。";
        }

        private string GenerateDeathNarrative(GameEvent evt, GameState state)
        {
            if (evt.InvolvedNpcs.Count > 0)
            {
                var npcName = evt.InvolvedNpcs[0];
                var templates = new[]
                {
                    $"{npcName} 最后还是没有挺过来。尸体被埋在了营地外。",
                    $"{npcName} 停止了呼吸。沉默笼罩了整个营地。",
                    $"{npcName} 走了。留下的只有空洞和遗憾。"
                };
                return templates[_rng.Randi() % templates.Length];
            }

            return "又失去了一个同伴。";
        }

        private string GenerateIllnessNarrative(GameEvent evt, GameState state)
        {
            if (evt.InvolvedNpcs.Count > 0)
            {
                var npcName = evt.InvolvedNpcs[0];
                return $"{npcName} 生病了。所有人都保持距离。";
            }

            return "有人病倒了。";
        }

        /// <summary>
        /// 生成日间状态摘要
        /// </summary>
        public string GenerateDaySummary(GameState state)
        {
            var summaries = new[]
            {
                $"第 {state.Day} 天。我们还活着。物资还有 {state.Supplies} 单位。",
                $"又活过了一天。{state.GetAliveSurvivorCount()} 个幸存者。{state.Supplies} 单位物资。",
                $"日落时分。营地仍然站立。防御值：{state.Defense}。"
            };
            return summaries[_rng.Randi() % summaries.Length];
        }

        /// <summary>
        /// 生成游戏结束信息
        /// </summary>
        public string GenerateEndingNarrative(GameState state, bool victory)
        {
            if (victory)
            {
                return $"你们熬过了 {state.Day} 天。{state.GetAliveSurvivorCount()} 个幸存者最终走出了黑暗。故事未完待续...";
            }
            else
            {
                return $"在第 {state.Day} 天，最后的声音消失了。一切都结束了。";
            }
        }
    }
}
