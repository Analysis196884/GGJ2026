using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
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

        /// <summary>
        /// 设置 LLM 客户端（由 GameManager 调用）
        /// </summary>
        public void SetLLMClient(LLMClient client)
        {
            _llmClient = client;
        }

        public override void _Ready()
        {
        	base._Ready();
        	// 将自己添加到组中，便于其他节点查找
        	AddToGroup("NarrativeEngine");
        	
        	// 如果还没有设置 LLMClient，尝试从场景树查找
        	if (_llmClient == null)
        	{
        		var parent = GetParent();
        		if (parent != null)
        		{
        			// 通过类型查找 LLMClient（不依赖节点名称）
        			foreach (var child in parent.GetChildren())
        			{
        				if (child is LLMClient llm)
        				{
        					_llmClient = llm;
        					break;
        				}
        			}
        			if (_llmClient == null)
        			{
        				GD.Print("[NarrativeEngine] 未找到 LLMClient 实例");
        			}
        		}
        	}
        	
        	// 如果仍未找到，创建默认实例（但应该由 GameManager 提供）
        	if (_llmClient == null)
        	{
        		_llmClient = new LLMClient();
        		_llmClient.Enabled = true;  // 启用，但使用模拟模式（因为没有 API 密钥）
        		AddChild(_llmClient);
        	}
        }

        /// <summary>
        /// 生成事件的叙事文本（回调版本）
        /// </summary>
        public void GenerateEventNarrative(GameEvent gameEvent, GameState state, Action<NarrativeResult> callback)
        {
            var result = new NarrativeResult
            {
                NarrativeText = "",
                Choices = []
            };

            // 如果 LLM 客户端启用，尝试使用 LLM 生成（包括模拟模式）
            if (_llmClient != null && _llmClient.Enabled)
            {
                try
                {
                    _llmClient.GenerateEventNarrative(gameEvent.Type.ToString(), gameEvent.Description, state, (llmText) =>
                    {
                        if (!string.IsNullOrEmpty(llmText))
                        {
                            result.NarrativeText = llmText;
                            // 保留原有选择（可根据需要调整）
                            SetChoicesByEventType(gameEvent.Type, ref result);
                            callback(result);
                        }
                        else
                        {
                            // 回退到模板
                            GenerateFallbackNarrative(gameEvent, state, result, callback);
                        }
                    });
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[NarrativeEngine] LLM 生成失败：{ex.Message}");
                    // 回退到模板
                    GenerateFallbackNarrative(gameEvent, state, result, callback);
                }
            }
            else
            {
                GD.Print("[NarrativeEngine] LLM 客户端未启用或未找到，使用模板生成");
                GenerateFallbackNarrative(gameEvent, state, result, callback);
            }
        }

        private void GenerateFallbackNarrative(GameEvent gameEvent, GameState state, NarrativeResult result, Action<NarrativeResult> callback)
        {
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

            callback(result);
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
            var tcs = new TaskCompletionSource<NarrativeResult>();
            GenerateEventNarrative(gameEvent, state, (result) => tcs.SetResult(result));
            // 等待结果，但设置超时以避免无限等待
            var timeoutTask = Task.Delay(35000); // 35秒超时
            var completedTask = Task.WhenAny(tcs.Task, timeoutTask).Result;
            if (completedTask == timeoutTask)
            {
                GD.PrintErr("[NarrativeEngine] 生成叙事超时，使用默认文本");
                return new NarrativeResult { NarrativeText = gameEvent.Description, Choices = [] };
            }
            return tcs.Task.Result;
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
            var summary = $"第 {state.Day} 天。我们还活着。物资还有 {state.Supplies} 单位。";
            return summary;
        }

        /// <summary>
        /// 处理玩家与NPC的交互（回调版本）
        /// </summary>
        public void ProcessPlayerInteraction(Survivor npc, string playerInput, Action<NarrativeActionResponse> callback)
        {
            GD.Print($"[NarrativeEngine] ProcessPlayerInteraction 开始: {npc.SurvivorName}, 输入: {playerInput}");
            // 检查是否为命令（以"/"开头）
            if (playerInput.StartsWith("/"))
            {
                GD.Print("[NarrativeEngine] 检测到命令输入，跳过交互处理");
                callback(new NarrativeActionResponse("", 0, 0, false, "Neutral"));
                return;
            }

            // 调用LLM生成响应
            _llmClient.GenerateInteractionResponse(npc, playerInput, (jsonResponse) =>
            {
                GD.Print($"[NarrativeEngine] 收到 LLM 响应: {jsonResponse}");
                try
                {
                    // 解析JSON
                    var response = JsonSerializer.Deserialize<NarrativeActionResponse>(jsonResponse);
                    if (response == null)
                    {
                        throw new Exception("反序列化失败");
                    }

                    // 应用数值变化
                    ApplyInteractionChanges(npc, response);

                    GD.Print($"[NarrativeEngine] 交互处理完成: {response}");
                    callback(response);
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[NarrativeEngine] 解析交互响应失败: {ex.Message}, JSON: {jsonResponse}");
                    // 返回默认响应
                    var defaultResponse = new NarrativeActionResponse("我不太明白你在说什么。", 0, 0, false, "Confused");
                    callback(defaultResponse);
                }
            });
        }

        /// <summary>
        /// 处理玩家与NPC的交互（同步版本）
        /// </summary>
        public NarrativeActionResponse ProcessPlayerInteraction(Survivor npc, string playerInput)
        {
            var tcs = new TaskCompletionSource<NarrativeActionResponse>();
            ProcessPlayerInteraction(npc, playerInput, (result) => tcs.SetResult(result));

            // 设置超时
            var timeoutTask = Task.Delay(30000); // 30秒超时
            var completedTask = Task.WhenAny(tcs.Task, timeoutTask).Result;
            if (completedTask == timeoutTask)
            {
                GD.PrintErr("[NarrativeEngine] 交互处理超时，返回默认响应");
                return new NarrativeActionResponse("处理超时，请重试。", 0, 0, false, "Neutral");
            }
            return tcs.Task.Result;
        }

        /// <summary>
        /// 应用交互产生的数值变化
        /// </summary>
        private void ApplyInteractionChanges(Survivor npc, NarrativeActionResponse response)
        {
            // 修改压力值
            npc.Stress += response.StressDelta;
            npc.Stress = Mathf.Clamp(npc.Stress, 0, 100);

            // 修改信任度（使用Player作为键）
            npc.ModifyTrust("Player", response.TrustDelta);

            GD.Print($"[NarrativeEngine] 应用交互变化 - {npc.SurvivorName}: Stress {npc.Stress - response.StressDelta} -> {npc.Stress}, Trust {npc.GetTrust("Player") - response.TrustDelta} -> {npc.GetTrust("Player")}");
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
