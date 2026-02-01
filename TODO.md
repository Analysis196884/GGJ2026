文件LLMClient.cs是我目前关于LLM动态生成内容的代码。接下来我想让你帮我针对其中的Prompt进行优化，要求：

1. LLM知道我的游戏设定/故事背景：
这是一个 LLM 驱动的生存与推理悬疑游戏:
在丧尸末日的暴雪封锁中，玩家作为避难所的管理者，领导 6–7 名幸存者在资源匮乏、信任崩塌的环境下求生。幸存者各自戴着面具，隐藏着秘密与动机，而真正的威胁往往来自屋内。
通过生存管理、社会关系判断与 LLM 动态生成的剧情线索，玩家必须在怀疑与信任之间做出抉择，决定避难所的命运。

2. 针对与NPC进行对话的API调用，LLM知道当前该NPC的各个属性值、性格、秘密，知道当前的避难所状态信息，并且想办法让NPC的对话变得生动随机，而不是过于严肃

3. 生成随机事件时，鼓励生成具有充分戏剧性、多样性、连贯性的事件，能够影响所有NPC的属性以及避难所的状态，例如“Tom 最近总是做噩梦，梦中会想起他离世的妻子”(Tom Stress 上升) -> "第 x 天，Tom的情绪爆发了，他打翻了桌上的酒瓶，酒水流到篝火中，房间开始烧了起来，好在大家即时行动将火焰快速熄灭，但是消耗了大量的水资源..." (避难所Defense、Supplies下降，所有NPCStress上升) -> ...

---------------------------------------------------------------
参考 Gemini 的回答：
这是一个非常棒的优化方向。为了让 LLM 真正理解你的“暴风雪山庄 + 丧尸末日 + 假面猜疑”的核心体验，我们需要在 Prompt 中通过 **System Instruction (系统指令)** 注入世界观，并通过 **Few-Shot (少样本提示)** 或 **Chain of Thought (思维链)** 引导它生成更有戏剧性的内容。

以下是针对 `LLMClient.cs` 的修改建议。我将重点优化 **NPC 交互** 和 **随机事件生成** 这两个核心方法的 Prompt。

### 1. 定义统一的游戏背景 (Game Lore)

首先，建议在类中定义一个常量字符串，存储你刚才描述的背景，这样可以确保所有 API 调用都共享同一个世界观。

```csharp
// 在 LLMClient 类内部添加
private const string GameLore = """
背景设定：
这是一个名为《假面方舟 (Masquerade Ark)》的生存推理游戏。
- 环境：丧尸末日，避难所被暴雪封锁，外界是零下 30 度的死寂与尸潮。
- 氛围：幽闭恐惧、资源匮乏、信任崩塌。类似于《The Walking Dead》结合《阿加莎·克里斯蒂》的风格。
- 核心冲突：幸存者们各自戴着“面具”，每个人都有不可告人的秘密（如隐瞒感染、私藏物资、复仇动机）。真正的威胁往往来自屋内的人心，而非屋外的丧尸。
""";
```

---

### 2. 优化 NPC 对话 Prompt (`CallDeepSeekApiForInteraction`)

**优化目标**：让 NPC 说话像“人”而不是“机器”。利用性格、秘密和当前状态（如极度饥饿或压力过大）来改变语调。

请替换 `CallDeepSeekApiForInteraction` 方法中的 Prompt 部分：

```csharp
private void CallDeepSeekApiForInteraction(Survivor npc, string playerInput, Action<string> callback)
{
    // 1. 构建 System Prompt：注入世界观 + 角色扮演指导
    string systemPrompt = $"""
{GameLore}

你的任务：
你现在需要完全扮演 NPC "{npc.SurvivorName}" 与玩家（管理者）对话。

角色扮演指导：
1. **不要** 像个 AI 助理一样回答。说话要口语化，可以使用俚语、结巴、反问。
2. **状态驱动**：
   - 如果压力 (Stress) > 70：语气要暴躁、神经质、多疑，或者因恐惧而颤抖。
   - 如果饥饿 (Hunger) > 70：语气虚弱，或者因饥饿而愤怒，话题总是不自觉绕到食物上。
   - 如果信任 (Trust) < 0：表现出冷漠、防备，甚至撒谎。
3. **守住秘密**：如果你有秘密（Secret），不要直接说出来！要通过闪烁其词、转移话题或过度防御来暗示。
4. **格式要求**：只返回 JSON，不要包含 Markdown 格式。
JSON 格式：
{{
    "NarrativeText": "你的口语回答（包含动作描写，如：'他避开了你的视线...'）",
    "StressDelta": 整数 (-10 ~ +10),
    "TrustDelta": 整数 (-10 ~ +10),
    "Mood": "情绪关键词 (如: Suspicious, Angry, Terrified, Grateful)"
}}
""";

    // 2. 构建 User Prompt：注入动态数据
    string userPrompt = $"""
当前 NPC 档案：
- 姓名：{npc.SurvivorName}
- 职业/角色：{npc.Role}
- 核心性格/背景：{npc.Bio}
- **不可告人的秘密**：{npc.Secret} (玩家不知道，你必须掩饰！)

当前状态：
- 压力值：{npc.Stress}/100
- 饥饿值：{npc.Hunger}/100
- 对玩家信任度：{npc.Trust} (-100 ~ 100)

玩家说："{playerInput}"

请生成回应。
""";

    // ... (后续的 requestBody 构建和发送逻辑保持不变)
}
```

---

### 3. 优化随机事件生成 Prompt (`CallDeepSeekApiForRandomEvent`)

**优化目标**：强调“蝴蝶效应”和“连贯性”。不仅要发生事情，还要基于之前的状态（例如某人压力高）来触发后续灾难。

请替换 `CallDeepSeekApiForRandomEvent` 方法中的 Prompt 部分：

```csharp
private void CallDeepSeekApiForRandomEvent(GameState state, string eventHistorySummary, Action<string> callback)
{
    // 1. 提取关键状态用于 Prompt (找出状态最差的 NPC 作为潜在爆发点)
    var mostStressed = state.Survivors.MaxBy(s => s.Stress);
    var mostHungry = state.Survivors.MaxBy(s => s.Hunger);
    
    // 2. 构建 System Prompt
    string systemPrompt = $"""
{GameLore}

你的任务：
作为游戏的“导演 (Game Master)”，生成一个突发的随机事件。

设计原则：
1. **蝴蝶效应**：不要凭空生成事件。请观察 NPC 当前的状态（尤其是高压力、高饥饿的 NPC），让事件成为他们状态恶化的后果。
   - *例子*：如果 Tom 压力高 -> Tom 梦游打翻了煤油灯 -> 火灾导致物资损失。
2. **戏剧性与多样性**：
   - 包含：心理崩溃、物资事故、社交冲突、外部入侵、设施故障、发现秘密。
   - 避免：单调的“发现物资”或“被丧尸攻击”。
3. **结果导向**：事件必须对游戏数值产生实际影响。

JSON 格式要求（严禁 Markdown）：
{{
    "EventType": "事件类型 (Crisis/Conflict/Accident/Atmosphere)",
    "Description": "一段 80-120 字的事件描述。侧重描写氛围、声音和角色的反应。",
    "InvolvedNpcs": ["涉及的 NPC 名字"],
    "Effects": {{
        "SuppliesDelta": 整数,
        "DefenseDelta": 整数,
        "NpcEffects": [
            {{ "NpcName": "名字", "HpDelta": 0, "StressDelta": 0, "TrustDelta": 0 }}
        ]
    }}
}}
""";

    // 3. 构建 User Prompt
    string userPrompt = $"""
当前游戏局势：
- 天数：第 {state.Day} 天
- 环境：暴雪肆虐，物资剩余 {state.Supplies} (若 < 3 则极度紧缺)

重点关注的 NPC (潜在爆发点)：
- 压力最高者：{mostStressed?.SurvivorName} (Stress: {mostStressed?.Stress}, Secret: {mostStressed?.Secret})
- 最饥饿者：{mostHungry?.SurvivorName} (Hunger: {mostHungry?.Hunger})

最近发生的 3 个历史事件（请确保新事件与此有一定连贯性）：
{(string.IsNullOrEmpty(eventHistorySummary) ? "无" : eventHistorySummary)}

请生成一个新的、具有冲击力的事件。
""";

    // ... (后续代码保持不变)
}
```

### 4. 优化日间摘要 (`CallDeepSeekApiForSummary`)

让摘要也带有“丧尸末日日记”的感觉。

```csharp
private void CallDeepSeekApiForSummary(GameState state, Action<string> callback)
{
    string prompt = $"""
{GameLore}

任务：写一段“管理者的日记摘要”。
当前状态：第 {state.Day} 天，幸存 {state.GetAliveSurvivorCount()} 人，物资 {state.Supplies}，防御 {state.Defense}。

要求：
- 语气：疲惫、冷峻、现实。
- 不要写成流水账，要描写一个具体的细节（例如：窗外的风雪声、某个人的眼神、食物的短缺感）。
- 结尾留下一丝悬念或不安。
- 字数：80 字以内。
""";
    
    // ... (User Role 可以留空，直接发 system role 或者合并)
}
```

### 为什么这样改？

1.  **Context Injection (上下文注入)**：之前 LLM 不知道你的游戏叫什么，也不知道“面具”这个核心设定。现在它知道每个人都有 Secret，生成的内容会更具阴谋感。
2.  **Specific Triggers (具体触发点)**：在随机事件中，我特意在 C# 代码里先找出了 `mostStressed` 和 `mostHungry` 的 NPC 传给 LLM。这样 LLM 就不需要瞎猜，而是直接针对“压力最大的 Tom”生成“Tom 发疯”的事件，这就是你想要的**连贯性**。
3.  **Show, Don't Tell (展示而非讲述)**：在对话 Prompt 中，我明确要求包含“动作描写”和“情绪关键词”，并根据数值调整语气。这能让 NPC 看起来是“活”的，而不是一个只会报状态的机器人。

应用这些修改后，你的游戏体验应该会从“随机的文字生成”转变为“有逻辑的互动叙事”。