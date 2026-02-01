using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using System.Text.Json;
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
        public string ApiKey { get; set; } = "";

        [Export]
        public string Model { get; set; } = "deepseek-chat";

        [Export]
        public bool Simulate { get; set; } = false;

        private RandomNumberGenerator _rng = new();
        private HttpRequest _httpRequest = null!;
        private bool _isRequesting = false;
        private Queue<(string eventType, string eventDescription, GameState state, Action<string> callback)> _requestQueue = new();

        public LLMClient()
        {
            _rng.Randomize();
        }

        private void LoadApiKey()
        {
            // 优先从环境变量读取
            string envKey = System.Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
            if (!string.IsNullOrEmpty(envKey))
            {
                ApiKey = envKey;
                GD.Print("[LLMClient] API Key loaded from environment variable");
                return;
            }

            // 从配置文件读取
            string configPath = ProjectSettings.GlobalizePath("user://LLMAPI.cfg");
            if (System.IO.File.Exists(configPath))
            {
                try
                {
                    var lines = System.IO.File.ReadAllLines(configPath, Encoding.UTF8);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("key="))
                        {
                            ApiKey = trimmed.Substring(4);
                            GD.Print("[LLMClient] API Key loaded from config file");
                            return;
                        }
                    }
                }
                catch (Exception e)
                {
                    GD.PrintErr($"[LLMClient] Error reading config file: {e.Message}");
                }
            }

            GD.Print("[LLMClient] No API Key found, using simulation mode");
        }

        public override void _Ready()
        {
            base._Ready();
            _httpRequest = new HttpRequest();
            AddChild(_httpRequest);
            LoadApiKey();
        }

        private void CallDeepSeekApi(string eventType, string eventDescription, GameState state, Action<string> callback)
        {
            GD.Print("[LLMClient] Starting API call");
            if (string.IsNullOrEmpty(ApiEndpoint))
            {
                callback(GenerateSimulatedNarrative(eventType, eventDescription, state));
                return;
            }

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
            var headers = new string[]
            {
                "Authorization: Bearer " + ApiKey,
                "Content-Type: application/json"
            };

            void OnRequestCompleted(long result, long responseCode, string[] headers, byte[] body)
            {
                GD.Print($"[LLMClient] Request completed, result: {result}, responseCode: {responseCode}");
                _httpRequest.RequestCompleted -= OnRequestCompleted;

                if (result != (long)HttpRequest.Result.Success)
                {
                    GD.Print("[LLMClient] Request failed, using simulated text");
                    callback(GenerateSimulatedNarrative(eventType, eventDescription, state));
                    return;
                }

                if (responseCode != 200)
                {
                    GD.PrintErr($"[LLM API Error] {responseCode}: {System.Text.Encoding.UTF8.GetString(body)}");
                    callback(GenerateSimulatedNarrative(eventType, eventDescription, state));
                    return;
                }

                try
                {
                    string responseBody = System.Text.Encoding.UTF8.GetString(body);
                    GD.Print($"[LLMClient] Response body: {responseBody}");
                    using JsonDocument doc = JsonDocument.Parse(responseBody);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                    {
                        string content = choices[0].GetProperty("message").GetProperty("content").GetString()?.Trim() ?? "";
                        GD.Print($"[LLMClient] Extracted content: {content}");
                        callback(content);
                    }
                    else
                    {
                        GD.Print("[LLMClient] No choices, using simulated text");
                        callback(GenerateSimulatedNarrative(eventType, eventDescription, state));
                    }
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[LLMClient] 解析响应异常: {ex.Message}");
                    callback(GenerateSimulatedNarrative(eventType, eventDescription, state));
                }
            }

            _httpRequest.RequestCompleted += OnRequestCompleted;
            Error error = _httpRequest.Request(ApiEndpoint, headers, HttpClient.Method.Post, jsonBody);
            GD.Print($"[LLMClient] Request sent, error: {error}");

            if (error != Error.Ok)
            {
                GD.PrintErr($"[LLMClient] 请求失败: {error}");
                callback(GenerateSimulatedNarrative(eventType, eventDescription, state));
            }
        }

        /// <summary>
        /// 生成事件叙事文本（回调版本，避免阻塞）
        /// </summary>
        public void GenerateEventNarrative(string eventType, string eventDescription, GameState state, Action<string> callback)
        {
            if (!Enabled)
            {
                GD.Print($"[LLMClient] LLM 未启用，返回空字符串");
                callback("");
                return;
            }

            if (string.IsNullOrEmpty(ApiKey))
            {
                GD.Print($"[LLMClient] 模拟模式，为事件 {eventType} 生成模板文本");
                callback(GenerateSimulatedNarrative(eventType, eventDescription, state));
                return;
            }

            // 真实 API 调用
            GD.Print($"[LLMClient] 调用真实 API 为事件 {eventType} 生成叙事文本");
            if (_isRequesting)
            {
                _requestQueue.Enqueue((eventType, eventDescription, state, callback));
            }
            else
            {
                _isRequesting = true;
                CallDeepSeekApi(eventType, eventDescription, state, (result) =>
                {
                    callback(result);
                    _isRequesting = false;
                    ProcessNextRequest();
                });
            }
        }

        /// <summary>
        /// 处理队列中的下一个请求
        /// </summary>
        private void ProcessNextRequest()
        {
            if (_requestQueue.Count > 0)
            {
                var (eventType, eventDescription, state, callback) = _requestQueue.Dequeue();
                _isRequesting = true;
                CallDeepSeekApi(eventType, eventDescription, state, (result) =>
                {
                    callback(result);
                    _isRequesting = false;
                    ProcessNextRequest();
                });
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
        /// 生成玩家与NPC交互的响应（返回JSON字符串）
        /// </summary>
        public void GenerateInteractionResponse(Survivor npc, string playerInput, Action<string> callback)
        {
            if (!Enabled)
            {
                GD.Print($"[LLMClient] LLM 未启用，返回模拟JSON");
                callback(GenerateSimulatedInteractionResponse(npc, playerInput));
                return;
            }

            if (string.IsNullOrEmpty(ApiKey))
            {
                callback(GenerateSimulatedInteractionResponse(npc, playerInput));
                return;
            }

            // 真实 API 调用
            CallDeepSeekApiForInteraction(npc, playerInput, callback);
        }

        private void CallDeepSeekApiForInteraction(Survivor npc, string playerInput, Action<string> callback)
        {
            string systemPrompt = """
            你是一个末日生存游戏中具有个性的NPC。根据NPC的性格和当前状态，生成相应的回答。
            必须只返回JSON格式，不要任何其他文本。JSON格式：
            {
                "NarrativeText": "NPC的回应文本",
                "StressDelta": 压力值变化量（整数，如-10或+5）,
                "TrustDelta": 信任度变化量（整数）,
                "Mood": "情绪关键词如Angry, Neutral, Happy"
            }
            """;

            string userPrompt = $"""
            NPC信息：
            - 姓名：{npc.SurvivorName}
            - 角色：{npc.Role}
            - 背景：{npc.Bio}
            - 当前压力值：{npc.Stress}
            - 当前信任度：{npc.Trust}

            玩家输入：{playerInput}

            根据NPC性格和状态，生成对话文本，返回JSON。
            """;

            var requestBody = new
            {
                model = Model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                max_tokens = 300,
                temperature = 0.7
            };

            string jsonBody = JsonSerializer.Serialize(requestBody);
            var headers = new string[]
            {
                "Authorization: Bearer " + ApiKey,
                "Content-Type: application/json"
            };

            void OnRequestCompleted(long result, long responseCode, string[] headers, byte[] body)
            {
                _httpRequest.RequestCompleted -= OnRequestCompleted;

                if (result != (long)HttpRequest.Result.Success)
                {
                    GD.Print("[LLMClient] 交互请求失败，使用模拟JSON");
                    callback(GenerateSimulatedInteractionResponse(npc, playerInput));
                    return;
                }

                if (responseCode != 200)
                {
                    GD.PrintErr($"[LLM API Error] {responseCode}: {System.Text.Encoding.UTF8.GetString(body)}");
                    callback(GenerateSimulatedInteractionResponse(npc, playerInput));
                    return;
                }

                try
                {
                    string responseBody = System.Text.Encoding.UTF8.GetString(body);
                    using JsonDocument doc = JsonDocument.Parse(responseBody);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                    {
                        string content = choices[0].GetProperty("message").GetProperty("content").GetString()?.Trim() ?? "";
                        // 清理可能的Markdown标记
                        content = CleanJson(content);
                        callback(content);
                    }
                    else
                    {
                        GD.Print("[LLMClient] 无交互响应，使用模拟JSON");
                        callback(GenerateSimulatedInteractionResponse(npc, playerInput));
                    }
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[LLMClient] 解析交互响应异常: {ex.Message}");
                    callback(GenerateSimulatedInteractionResponse(npc, playerInput));
                }
            }

            _httpRequest.RequestCompleted += OnRequestCompleted;
            Error error = _httpRequest.Request(ApiEndpoint, headers, HttpClient.Method.Post, jsonBody);

            if (error != Error.Ok)
            {
                GD.PrintErr($"[LLMClient] 交互请求失败: {error}");
                callback(GenerateSimulatedInteractionResponse(npc, playerInput));
            }
        }

        /// <summary>
        /// 生成模拟交互响应JSON
        /// </summary>
        private string GenerateSimulatedInteractionResponse(Survivor npc, string playerInput)
        {
            var responses = new[]
            {
                "{\"NarrativeText\":\"我明白了。谢谢你的关心。\",\"StressDelta\":-5,\"TrustDelta\":10,\"IsSuccess\":true,\"Mood\":\"Grateful\"}",
                "{\"NarrativeText\":\"你说什么？我不明白。\",\"StressDelta\":2,\"TrustDelta\":-5,\"IsSuccess\":false,\"Mood\":\"Confused\"}",
                "{\"NarrativeText\":\"别烦我！我有自己的事。\",\"StressDelta\":10,\"TrustDelta\":-15,\"IsSuccess\":false,\"Mood\":\"Angry\"}",
                "{\"NarrativeText\":\"好吧，我会考虑的。\",\"StressDelta\":-2,\"TrustDelta\":5,\"IsSuccess\":true,\"Mood\":\"Neutral\"}"
            };
            return responses[_rng.Randi() % responses.Length];
        }

        /// <summary>
        /// 清理JSON字符串，去除可能的Markdown标记
        /// </summary>
        private string CleanJson(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;

            // 去除 ```json 和 ``` 标记
            raw = raw.Replace("```json", "").Replace("```", "").Trim();

            // 去除可能的其他Markdown
            if (raw.StartsWith("json\n"))
            {
                raw = raw.Substring(5);
            }

            return raw.Trim();
        }

        /// <summary>
        /// 生成日间摘要文本（回调版本）
        /// </summary>
        public void GenerateDaySummary(GameState state, Action<string> callback)
        {
            if (!Enabled)
            {
                callback("");
                return;
            }

            if (string.IsNullOrEmpty(ApiKey))
            {
                var summaries = new[]
                {
                    $"第 {state.Day} 天。我们还活着。物资还有 {state.Supplies} 单位。",
                    $"又活过了一天。{state.GetAliveSurvivorCount()} 个幸存者。{state.Supplies} 单位物资。",
                    $"日落时分。营地仍然站立。防御值：{state.Defense}。"
                };
                callback(summaries[_rng.Randi() % summaries.Length]);
                return;
            }

            // 真实 API 调用
            GD.Print($"[LLMClient] 调用真实 API 生成日间摘要");
            CallDeepSeekApiForSummary(state, callback);
        }

        private void CallDeepSeekApiForSummary(GameState state, Action<string> callback)
        {
            string prompt = $"""
当前游戏状态：
- 天数：{state.Day}
- 幸存者数量：{state.GetAliveSurvivorCount()}
- 物资：{state.Supplies}
- 防御：{state.Defense}

请生成一段简短的日间摘要，描述营地当天的情况，不超过80字。语气压抑、现实。
""";

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
            var headers = new string[]
            {
                "Authorization: Bearer " + ApiKey,
                "Content-Type: application/json"
            };

            void OnRequestCompleted(long result, long responseCode, string[] headers, byte[] body)
            {
                _httpRequest.RequestCompleted -= OnRequestCompleted;

                if (result != (long)HttpRequest.Result.Success)
                {
                    callback($"第 {state.Day} 天。幸存者 {state.GetAliveSurvivorCount()} 人，物资 {state.Supplies} 单位。");
                    return;
                }

                if (responseCode != 200)
                {
                    GD.PrintErr($"[LLM API Error] {responseCode}: {System.Text.Encoding.UTF8.GetString(body)}");
                    callback($"第 {state.Day} 天。幸存者 {state.GetAliveSurvivorCount()} 人，物资 {state.Supplies} 单位。");
                    return;
                }

                try
                {
                    string responseBody = System.Text.Encoding.UTF8.GetString(body);
                    using JsonDocument doc = JsonDocument.Parse(responseBody);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                    {
                        string content = choices[0].GetProperty("message").GetProperty("content").GetString()?.Trim() ?? "";
                        callback(content);
                    }
                    else
                    {
                        callback($"第 {state.Day} 天。幸存者 {state.GetAliveSurvivorCount()} 人，物资 {state.Supplies} 单位。");
                    }
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[LLMClient] 解析响应异常: {ex.Message}");
                    callback($"第 {state.Day} 天。幸存者 {state.GetAliveSurvivorCount()} 人，物资 {state.Supplies} 单位。");
                }
            }

            _httpRequest.RequestCompleted += OnRequestCompleted;
            Error error = _httpRequest.Request(ApiEndpoint, headers, HttpClient.Method.Post, jsonBody);

            if (error != Error.Ok)
            {
                GD.PrintErr($"[LLMClient] 请求失败: {error}");
                callback($"第 {state.Day} 天。幸存者 {state.GetAliveSurvivorCount()} 人，物资 {state.Supplies} 单位。");
            }
        }

        /// <summary>
        /// 生成LLM驱动的随机事件（回调版本）
        /// </summary>
        public void GenerateRandomEvent(GameState state, string eventHistorySummary, Action<string> callback)
        {
            if (!Enabled)
            {
                GD.Print($"[LLMClient] LLM 未启用，返回空字符串");
                callback("");
                return;
            }

            if (string.IsNullOrEmpty(ApiKey))
            {
                GD.Print($"[LLMClient] 模拟模式，生成模拟随机事件");
                callback(GenerateSimulatedRandomEvent(state));
                return;
            }

            // 真实 API 调用
            GD.Print($"[LLMClient] 调用真实 API 生成随机事件");
            CallDeepSeekApiForRandomEvent(state, eventHistorySummary, callback);
        }

        /// <summary>
        /// 调用DeepSeek API生成随机事件
        /// </summary>
        private void CallDeepSeekApiForRandomEvent(GameState state, string eventHistorySummary, Action<string> callback)
        {
            string systemPrompt = """
            你是一个末日生存游戏的Game Master（DM）。根据当前游戏状态和历史事件，生成一个新的随机事件。
            
            要求：
            1. 事件应该具有戏剧性和趣味性
            2. 事件应该与历史事件产生"蝴蝶效应"式的连锁关系
            3. 事件应该多样化，不要重复之前的模式
            4. 事件应该含蓄，不直接揭露真相，只提供线索
            5. 语气压抑、现实、符合《行尸走肉》风格
            6. 事件描述不超过100字
            7. 必须返回JSON格式：
            {
                "EventType": "事件类型（Custom/SuppliesFound/Conflict/Discovery/ZombieAttack等）",
                "Description": "事件描述文本",
                "InvolvedNpcs": ["涉及的NPC名字数组，可以为空"],
                "Effects": {
                    "SuppliesDelta": 物资变化量（整数，可正可负）,
                    "DefenseDelta": 防御变化量（整数）,
                    "NpcEffects": [
                        {
                            "NpcName": "NPC名字",
                            "HpDelta": 生命值变化,
                            "StressDelta": 压力值变化,
                            "TrustDelta": 信任度变化
                        }
                    ]
                }
            }
            """;

            // 构建NPC状态摘要
            var npcSummary = new List<string>();
            foreach (var survivor in state.Survivors)
            {
                if (survivor.Hp > 0)
                {
                    npcSummary.Add($"- {survivor.SurvivorName}（{survivor.Role}）：生命{survivor.Hp}，压力{survivor.Stress}，饥饿{survivor.Hunger}");
                }
            }

            string userPrompt = $"""
            当前游戏状态：
            - 天数：第 {state.Day} 天
            - 幸存者数量：{state.GetAliveSurvivorCount()} 人
            - 物资：{state.Supplies} 单位
            - 防御：{state.Defense}

            幸存者状态：
            {string.Join("\n", npcSummary)}

            历史事件摘要：
            {(string.IsNullOrEmpty(eventHistorySummary) ? "（暂无历史事件）" : eventHistorySummary)}

            请基于以上信息生成一个新的随机事件，要求具有连锁性、多样性和戏剧性。返回JSON格式。
            """;

            var requestBody = new
            {
                model = Model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                max_tokens = 500,
                temperature = 0.9  // 更高的温度以增加随机性和创意性
            };

            string jsonBody = JsonSerializer.Serialize(requestBody);
            var headers = new string[]
            {
                "Authorization: Bearer " + ApiKey,
                "Content-Type: application/json"
            };

            void OnRequestCompleted(long result, long responseCode, string[] headers, byte[] body)
            {
                _httpRequest.RequestCompleted -= OnRequestCompleted;

                if (result != (long)HttpRequest.Result.Success)
                {
                    GD.Print("[LLMClient] 随机事件请求失败，使用模拟事件");
                    callback(GenerateSimulatedRandomEvent(state));
                    return;
                }

                if (responseCode != 200)
                {
                    GD.PrintErr($"[LLM API Error] {responseCode}: {System.Text.Encoding.UTF8.GetString(body)}");
                    callback(GenerateSimulatedRandomEvent(state));
                    return;
                }

                try
                {
                    string responseBody = System.Text.Encoding.UTF8.GetString(body);
                    using JsonDocument doc = JsonDocument.Parse(responseBody);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                    {
                        string content = choices[0].GetProperty("message").GetProperty("content").GetString()?.Trim() ?? "";
                        // 清理可能的Markdown标记
                        content = CleanJson(content);
                        GD.Print($"[LLMClient] 生成的随机事件JSON: {content}");
                        callback(content);
                    }
                    else
                    {
                        GD.Print("[LLMClient] 无随机事件响应，使用模拟事件");
                        callback(GenerateSimulatedRandomEvent(state));
                    }
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[LLMClient] 解析随机事件响应异常: {ex.Message}");
                    callback(GenerateSimulatedRandomEvent(state));
                }
            }

            _httpRequest.RequestCompleted += OnRequestCompleted;
            Error error = _httpRequest.Request(ApiEndpoint, headers, HttpClient.Method.Post, jsonBody);

            if (error != Error.Ok)
            {
                GD.PrintErr($"[LLMClient] 随机事件请求失败: {error}");
                callback(GenerateSimulatedRandomEvent(state));
            }
        }

        /// <summary>
        /// 生成模拟随机事件JSON
        /// </summary>
        private string GenerateSimulatedRandomEvent(GameState state)
        {
            var events = new[]
            {
                "{\"EventType\":\"Discovery\",\"Description\":\"在废墟中发现了一些旧物资。\",\"InvolvedNpcs\":[],\"Effects\":{\"SuppliesDelta\":3,\"DefenseDelta\":0,\"NpcEffects\":[]}}",
                "{\"EventType\":\"Conflict\",\"Description\":\"团队内部发生了争吵。\",\"InvolvedNpcs\":[],\"Effects\":{\"SuppliesDelta\":0,\"DefenseDelta\":0,\"NpcEffects\":[]}}",
                "{\"EventType\":\"Custom\",\"Description\":\"远处传来了奇怪的声音。\",\"InvolvedNpcs\":[],\"Effects\":{\"SuppliesDelta\":0,\"DefenseDelta\":0,\"NpcEffects\":[]}}",
                "{\"EventType\":\"ZombieAttack\",\"Description\":\"丧尸在夜里靠近了庇护所。\",\"InvolvedNpcs\":[],\"Effects\":{\"SuppliesDelta\":0,\"DefenseDelta\":-3,\"NpcEffects\":[]}}",
                "{\"EventType\":\"Custom\",\"Description\":\"有人在做噩梦，整晚都在尖叫。\",\"InvolvedNpcs\":[],\"Effects\":{\"SuppliesDelta\":0,\"DefenseDelta\":0,\"NpcEffects\":[]}}"
            };
            return events[_rng.Randi() % events.Length];
        }
    }
}