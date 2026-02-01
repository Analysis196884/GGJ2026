using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using System.Text.Json;
using MasqueradeArk.Core;
using MasqueradeArk.Utilities;

namespace MasqueradeArk.Engine
{
    /// <summary>
    /// LLM 客户端 - 模拟或真实调用外部语言模型 API 生成叙事文本
    /// 默认使用模拟模式（返回模板文本），可通过配置启用真实 API 调用
    /// </summary>
    [GlobalClass]
    public partial class LLMClient : Node
    {
        /// <summary>
        /// 游戏背景设定 - 所有LLM调用都会使用此背景
        /// </summary>
        private const string GameLore = """
背景设定：
这是一个名为《假面方舟 (Masquerade Ark)》的生存推理游戏。
- 环境：丧尸末日，避难所被暴雪封锁，外界是零下 30 度的死寂与尸潮。
- 氛围：幽闭恐惧、资源匮乏、信任崩塌。类似于《The Walking Dead》结合《阿加莎·克里斯蒂》的风格。
- 核心冲突：幸存者们各自戴着"面具"，每个人都有不可告人的秘密（如隐瞒感染、私藏物资、复仇动机）。真正的威胁往往来自屋内的人心，而非屋外的丧尸。
""";

        [Export]
        public bool Enabled { get; set; } = false;

        [Export]
        public string ApiEndpoint { get; set; } = "https://api.deepseek.com/chat/completions";

        [Export]
        public string ApiKey { get; set; } = "";

        [Export]
        public string Model { get; set; } = "";

        [Export]
        public bool Simulate { get; set; } = false;

        private RandomNumberGenerator _rng = new();
        // 移除单个HttpRequest实例，改为每次请求时创建新实例
        // private HttpRequest _httpRequest = null!;
        // private bool _isRequesting = false;
        // private Queue<(string eventType, string eventDescription, GameState state, Action<string> callback)> _requestQueue = new();

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

            // 使用 Godot 的 ConfigFile 读取配置文件
            string configPath = ProjectSettings.GlobalizePath("user://LLMAPI.cfg");
            var config = new ConfigFile();
            Error err = config.Load(configPath);

            if (err == Error.Ok)
            {
                // section 可以为空字符串 ""，表示根级别
                string model = (string)config.GetValue("API", "model", "");
                string apiKey = (string)config.GetValue("API", "key", "");
                if (!string.IsNullOrEmpty(model) && !string.IsNullOrEmpty(apiKey))
                {
                    ApiKey = apiKey;
                    Model = model;
                    GD.Print("[LLMClient] API Key loaded from config file");
                    return;
                }
            }
            else
            {
                GD.PrintErr($"[LLMClient] Failed to load config file: {err}");
            }

            GD.Print("[LLMClient] No API Key found, using simulation mode");
        }

        public override void _Ready()
        {
            base._Ready();
            // HttpRequest现在在每次API调用时动态创建，不再使用单个全局实例
            LoadApiKey();
        }

        private void CallModelAPI(string eventType, string eventDescription, GameState state, Action<string> callback)
        {
            GD.Print("[LLMClient] Starting API call");
            if (string.IsNullOrEmpty(ApiEndpoint))
            {
                callback(GenerateSimulatedNarrative(eventType, eventDescription, state));
                return;
            }

            // 为每个请求创建独立的HttpRequest实例，避免并发冲突
            var httpRequest = new HttpRequest();
            AddChild(httpRequest);

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
                httpRequest.RequestCompleted -= OnRequestCompleted;
                
                // 请求完成后清理HttpRequest实例
                httpRequest.QueueFree();

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
                    using JsonDocument doc = JsonDocument.Parse(responseBody);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                    {
                        string content = choices[0].GetProperty("message").GetProperty("content").GetString()?.Trim() ?? "";
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

            httpRequest.RequestCompleted += OnRequestCompleted;
            Error error = httpRequest.Request(ApiEndpoint, headers, HttpClient.Method.Post, jsonBody);
            GD.Print($"[LLMClient] Request sent, error: {error}");

            if (error != Error.Ok)
            {
                GD.PrintErr($"[LLMClient] 请求失败: {error}");
                httpRequest.QueueFree();
                callback(GenerateSimulatedNarrative(eventType, eventDescription, state));
            }
        }

        /// <summary>
        /// 生成事件叙事文本（回调版本，避免阻塞）
        /// 每个调用都创建独立的HttpRequest实例，支持真正的并发
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

            // 真实 API 调用 - 每个调用都使用独立的HttpRequest，支持并发
            GD.Print($"[LLMClient] 调用真实 API 为事件 {eventType} 生成叙事文本");
            CallModelAPI(eventType, eventDescription, state, callback);
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
            CallModelAPIForInteraction(npc, playerInput, callback);
        }

        private void CallModelAPIForInteraction(Survivor npc, string playerInput, Action<string> callback)
        {
            // Create independent HttpRequest for this call
            var httpRequest = new HttpRequest();
            AddChild(httpRequest);

            // 获取NPC的秘密信息
            string secretsInfo = npc.Secrets.Length > 0
                ? string.Join(", ", npc.Secrets)
                : "无秘密";
            
            // 获取对玩家的信任度（使用GetTrust方法）
            int trustToPlayer = npc.GetTrust(GameConstants.PLAYER_NAME);

            // 1. 构建 System Prompt：注入世界观 + 角色扮演指导
            string systemPrompt = $$"""
{{GameLore}}

你的任务：
你现在需要完全扮演 NPC "{{npc.SurvivorName}}" 与玩家（管理者）对话。

角色扮演指导：
1. 说话要口语化，可以使用俚语、结巴、反问，带有情感，不需要过于严肃(例如不建议总是说"都世界末日了还想这些干嘛"，即使是世界末日，"人的特性"仍在)
2. **状态驱动**：
   - 如果压力 (Stress) > 70：语气要暴躁、神经质、多疑，或者因恐惧而颤抖。
   - 如果饥饿 (Hunger) > 70：语气虚弱，或者因饥饿而愤怒，话题总是不自觉绕到食物上。
   - 如果信任 (Trust) < 30：表现出冷漠、防备，甚至撒谎。
3. **守住秘密**：如果你有秘密（Secret），不要直接说出来！要通过闪烁其词、转移话题或过度防御来暗示。
4. **格式要求**：只返回 JSON，不要包含 Markdown 格式。
JSON 格式：
{
    "NarrativeText": "你的口语回答（可包含动作描写，如：'他避开了你的视线...'）",
    "StressDelta": 整数 (-10 ~ +10),
    "TrustDelta": 整数 (-10 ~ +10),
    "Mood": "情绪关键词 (如: Suspicious, Angry, Terrified, Grateful, Neutral)"
}
""";

            // 2. 构建 User Prompt：注入动态数据
            string userPrompt = $"""
当前 NPC 档案：
- 姓名：{npc.SurvivorName}
- 职业/角色：{npc.Role}
- 核心性格/背景：{npc.Bio}
- **不可告人的秘密**：{secretsInfo} (玩家不知道，你必须掩饰！)

当前状态：
- 生命值：{npc.Hp}/100
- 压力值：{npc.Stress}/100
- 饥饿值：{npc.Hunger}/100
- 对玩家信任度：{trustToPlayer}/100

玩家说："{playerInput}"

请生成回应。
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
                httpRequest.RequestCompleted -= OnRequestCompleted;
                httpRequest.QueueFree();

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

            httpRequest.RequestCompleted += OnRequestCompleted;
            Error error = httpRequest.Request(ApiEndpoint, headers, HttpClient.Method.Post, jsonBody);

            if (error != Error.Ok)
            {
                httpRequest.QueueFree();
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
            CallModelAPIForSummary(state, callback);
        }

        private void CallModelAPIForSummary(GameState state, Action<string> callback)
        {
            // Create independent HttpRequest for this call
            var httpRequest = new HttpRequest();
            AddChild(httpRequest);

            string systemPrompt = $$"""
{{GameLore}}

任务：写一段"管理者的日记摘要"。

要求：
- 语气：疲惫、冷峻、现实。
- 不要写成流水账，要描写一个具体的细节（例如：窗外的风雪声、某个人的眼神、食物的短缺感）。
- 结尾留下一丝悬念或不安。
- 字数：80 字以内。
""";

            string userPrompt = $$"""
当前状态：第 {{state.Day}} 天，幸存 {{state.GetAliveSurvivorCount()}} 人，物资 {{state.Supplies}}，防御 {{state.Defense}}。

请生成日记摘要。
""";

            var requestBody = new
            {
                model = Model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
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
                httpRequest.RequestCompleted -= OnRequestCompleted;
                httpRequest.QueueFree();

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

            httpRequest.RequestCompleted += OnRequestCompleted;
            Error error = httpRequest.Request(ApiEndpoint, headers, HttpClient.Method.Post, jsonBody);

            if (error != Error.Ok)
            {
                httpRequest.QueueFree();
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
            CallModelAPIForRandomEvent(state, eventHistorySummary, callback);
        }

        /// <summary>
        /// 调用DeepSeek API生成随机事件
        /// </summary>
        private void CallModelAPIForRandomEvent(GameState state, string eventHistorySummary, Action<string> callback)
        {
            // Create independent HttpRequest for this call
            var httpRequest = new HttpRequest();
            AddChild(httpRequest);

            // 1. 找出压力最高和最饥饿的NPC作为潜在爆发点
            Survivor mostStressed = null;
            Survivor mostHungry = null;
            int maxStress = -1;
            int maxHunger = -1;
            
            foreach (var survivor in state.Survivors)
            {
                if (survivor.Hp > 0)
                {
                    if (survivor.Stress > maxStress)
                    {
                        maxStress = survivor.Stress;
                        mostStressed = survivor;
                    }
                    if (survivor.Hunger > maxHunger)
                    {
                        maxHunger = survivor.Hunger;
                        mostHungry = survivor;
                    }
                }
            }
            
            // 获取秘密信息
            string stressedSecrets = mostStressed != null && mostStressed.Secrets.Length > 0
                ? string.Join(", ", mostStressed.Secrets)
                : "无";
            string hungrySecrets = mostHungry != null && mostHungry.Secrets.Length > 0
                ? string.Join(", ", mostHungry.Secrets)
                : "无";

            // 2. 构建 System Prompt
            string systemPrompt = $$"""
{{GameLore}}

你的任务：
作为游戏的"导演 (Game Master)"，生成一个突发的随机事件。

设计原则：
1. **蝴蝶效应**：不要凭空生成事件。请观察 NPC 当前的状态（尤其是高压力、高饥饿的 NPC），让事件成为他们状态恶化的后果。
   - *例子*：如果 Tom 压力高 -> Tom 梦游打翻了煤油灯 -> 火灾导致物资损失。
2. **戏剧性与多样性**：
   - 例如：心理崩溃、社交冲突、互相伤害、外部入侵、设施故障、发现秘密。
3. **结果导向**：事件必须对游戏数值产生实际影响。

JSON 格式要求（严禁 Markdown）：
{
    "EventType": "事件类型 (Crisis/Conflict/Accident/Atmosphere)",
    "Description": "一段 50 字左右的事件描述。要求事件的经过、NPC的反应能够让人一眼看清楚。",
    "InvolvedNpcs": ["涉及的 NPC 名字"],
    "Effects": {
        "SuppliesDelta": 整数,
        "DefenseDelta": 整数,
        "NpcEffects": [
            { "NpcName": "名字", "HpDelta": 0, "StressDelta": 0, "TrustDelta": 0 }
        ]
    }
}
注释：TrustDelta 是相对于玩家的信任度变化，如果事件中没有涉及玩家，则该值为 0。
""";

            // 3. 构建 User Prompt
            string userPrompt = $$"""
当前游戏局势：
- 天数：第 {{state.Day}} 天
- 环境：暴雪肆虐，物资剩余 {{state.Supplies}} {{(state.Supplies < 5 ? "(极度紧缺！)" : "")}}
- 防御：{{state.Defense}}

重点关注的 NPC (潜在爆发点)：
- 压力最高者：{{mostStressed?.SurvivorName ?? "无"}} (Stress: {{mostStressed?.Stress ?? 0}}, Secret: {{stressedSecrets}})
- 最饥饿者：{{mostHungry?.SurvivorName ?? "无"}} (Hunger: {{mostHungry?.Hunger ?? 0}}, Secret: {{hungrySecrets}})

最近发生的历史事件（请确保新事件与此有一定连贯性）：
{{(string.IsNullOrEmpty(eventHistorySummary) ? "无" : eventHistorySummary)}}

请生成一个新的、具有冲击力的事件。
""";

            // NPC状态摘要已经在userPrompt中通过mostStressed和mostHungry传递
            // 不再需要单独的npcSummary列表

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
                httpRequest.RequestCompleted -= OnRequestCompleted;
                httpRequest.QueueFree();

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

            httpRequest.RequestCompleted += OnRequestCompleted;
            Error error = httpRequest.Request(ApiEndpoint, headers, HttpClient.Method.Post, jsonBody);

            if (error != Error.Ok)
            {
                httpRequest.QueueFree();
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