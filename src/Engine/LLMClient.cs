using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using System.Text.Json;
using System.Net.Http;
using MasqueradeArk.Core;

namespace MasqueradeArk.Engine
{
    /// <summary>
    /// LLM 客户端 - 模拟或真实调用外部语言模型 API 生成叙事文本
    /// 默认使用模拟模式（返回模板文本），可通过配置启用真实 API 调用
    /// </summary>
    [GlobalClass]
    public partial class LLMClient : Node
    {
        [Export]
        public bool Enabled { get; set; } = false;

        [Export]
        public string ApiEndpoint { get; set; } = "https://api.deepseek.com/chat/completions";

        [Export]
        public string ApiKey { get; set; } = "sk-286f5550258c4d579e4232d3c17fe3ff";

        [Export]
        public string Model { get; set; } = "deepseek-chat";

        [Export]
        public bool Simulate { get; set; } = true;

        private RandomNumberGenerator _rng = new();
        private HttpRequest _httpRequest;

        public LLMClient()
        {
            _rng.Randomize();
        }

        public override void _Ready()
        {
            base._Ready();
            _httpRequest = new HttpRequest();
            AddChild(_httpRequest);
        }

        /// <summary>
        /// 调用真实的 DeepSeek API 生成文本
        /// </summary>
        private async Task<string> CallDeepSeekApi(string prompt)
        {
            using (var httpClient = new System.Net.Http.HttpClient())
            {
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiKey);
                httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                var requestBody = new
                {
                    model = Model,
                    messages = new[]
                    {
                        new { role = "system", content = "你是一名《行尸走肉》风格的编剧。请根据提供的事件信息生成一段含蓄、压抑、现实的叙事文本，不超过100字。不要直接揭露真相，只提供线索和氛围描写。" },
                        new { role = "user", content = prompt }
                    },
                    max_tokens = 200,
                    temperature = 0.7
                };

                string jsonBody = JsonSerializer.Serialize(requestBody);
                var content = new System.Net.Http.StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");

                try
                {
                    var response = await httpClient.PostAsync(ApiEndpoint, content);
                    if (response.IsSuccessStatusCode)
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();
                        using JsonDocument doc = JsonDocument.Parse(responseBody);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                        {
                            var firstChoice = choices[0];
                            if (firstChoice.TryGetProperty("message", out var message) &&
                                message.TryGetProperty("content", out var contentProp))
                            {
                                return contentProp.GetString()?.Trim() ?? "";
                            }
                        }
                    }
                    else
                    {
                        GD.PrintErr($"[LLMClient] API 返回错误代码：{(int)response.StatusCode}");
                        GD.PrintErr($"响应体：{await response.Content.ReadAsStringAsync()}");
                    }
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[LLMClient] 请求失败：{ex.Message}");
                }
            }

            return "";
        }

        /// <summary>
        /// 生成事件叙事文本（异步）
        /// </summary>
        public async Task<string> GenerateEventNarrativeAsync(string eventType, string eventDescription, GameState state)
        {
            if (!Enabled)
            {
                GD.Print($"[LLMClient] LLM 未启用，返回空字符串");
                return "";
            }

            if (Simulate || string.IsNullOrEmpty(ApiKey))
            {
                GD.Print($"[LLMClient] 模拟模式，为事件 {eventType} 生成模板文本");
                return GenerateSimulatedNarrative(eventType, eventDescription, state);
            }

            // 真实 API 调用
            GD.Print($"[LLMClient] 调用真实 API 为事件 {eventType} 生成叙事文本");
            try
            {
                // 构建提示
                string prompt = $"""
事件类型：{eventType}
事件描述：{eventDescription}
当前游戏状态：
- 天数：{state.Day}
- 幸存者数量：{state.GetAliveSurvivorCount()}
- 物资：{state.Supplies}
- 防御：{state.Defense}

请根据以上信息生成一段含蓄、压抑、现实的叙事文本，不超过100字。不要直接揭露真相，只提供线索和氛围描写。
""";
                var result = await CallDeepSeekApi(prompt);
                if (!string.IsNullOrEmpty(result))
                {
                    return result;
                }
                else
                {
                    GD.PrintErr("[LLMClient] API 返回空结果，使用模拟文本");
                    return GenerateSimulatedNarrative(eventType, eventDescription, state);
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[LLMClient] API 调用异常：{ex.Message}");
                return GenerateSimulatedNarrative(eventType, eventDescription, state);
            }
        }

        /// <summary>
        /// 生成模拟叙事文本（基于预定义模板）
        /// </summary>
        private string GenerateSimulatedNarrative(string eventType, string eventDescription, GameState state)
        {
            // 根据事件类型返回不同的模板文本
            var templates = new Dictionary<string, string[]>
            {
                ["SuppliesStolen"] = new[]
                {
                    "物资库存不符。每个人的眼神都躲躲闪闪。",
                    "又发现物资少了。有人窃窃私语。",
                    "物资被盗。沉默笼罩了营地。信任在一点点瓦解。"
                },
                ["InfectionDetected"] = new[]
                {
                    "有人在夜里咳嗽个不停。那声音...听起来不太对劲。",
                    "皮肤有些异常。没人敢靠得太近。",
                    "最近很少和人说话。总是躲在阴暗的角落里。"
                },
                ["MentalBreakdown"] = new[]
                {
                    "突然尖叫了一声。眼神里充满了恐惧和疯狂。",
                    "在地上打滚，喃喃自语。无人敢靠近。",
                    "重复同一句话，一遍又一遍。压抑的氛围笼罩了整个营地。"
                },
                ["Starvation"] = new[]
                {
                    "食物快没了。人们开始互相监视。",
                    "饥饿感弥漫在营地里。有人开始做梦。"
                },
                ["Death"] = new[]
                {
                    "最后还是没有挺过来。尸体被埋在了营地外。",
                    "停止了呼吸。沉默笼罩了整个营地。",
                    "走了。留下的只有空洞和遗憾。"
                }
            };

            if (templates.TryGetValue(eventType, out var eventTemplates) && eventTemplates.Length > 0)
            {
                return eventTemplates[_rng.Randi() % eventTemplates.Length];
            }

            // 默认返回事件描述
            return eventDescription;
        }

        /// <summary>
        /// 生成日间摘要文本（异步）
        /// </summary>
        public async Task<string> GenerateDaySummaryAsync(GameState state)
        {
            if (!Enabled)
                return "";

            if (Simulate || string.IsNullOrEmpty(ApiKey))
            {
                var summaries = new[]
                {
                    $"第 {state.Day} 天。我们还活着。物资还有 {state.Supplies} 单位。",
                    $"又活过了一天。{state.GetAliveSurvivorCount()} 个幸存者。{state.Supplies} 单位物资。",
                    $"日落时分。营地仍然站立。防御值：{state.Defense}。"
                };
                return summaries[_rng.Randi() % summaries.Length];
            }

            // 真实 API 调用
            GD.Print($"[LLMClient] 调用真实 API 生成日间摘要");
            try
            {
                string prompt = $"""
当前游戏状态：
- 天数：{state.Day}
- 幸存者数量：{state.GetAliveSurvivorCount()}
- 物资：{state.Supplies}
- 防御：{state.Defense}

请生成一段简短的日间摘要，描述营地当天的情况，不超过80字。语气压抑、现实。
""";
                var result = await CallDeepSeekApi(prompt);
                if (!string.IsNullOrEmpty(result))
                {
                    return result;
                }
                else
                {
                    GD.PrintErr("[LLMClient] API 返回空结果，使用模拟文本");
                    return $"第 {state.Day} 天。幸存者 {state.GetAliveSurvivorCount()} 人，物资 {state.Supplies} 单位。";
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[LLMClient] API 调用异常：{ex.Message}");
                return $"第 {state.Day} 天。幸存者 {state.GetAliveSurvivorCount()} 人，物资 {state.Supplies} 单位。";
            }
        }
    }
}