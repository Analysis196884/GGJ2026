# Project Masquerade Ark - 架构文档

## 系统架构概览

```
┌─────────────────────────────────────────────────────────┐
│                    Godot 4.x Engine                     │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  ┌──────────────────────────────────────────────────┐  │
│  │            UI 层 (UIManager)                     │  │
│  │  - 侧边栏 (Status + NPC Cards)                   │  │
│  │  - 主区域 (Narrative + Actions)                 │  │
│  │  - 选择按钮 (Dynamic Choices)                   │  │
│  └──────────────────────────────────────────────────┘  │
│                        ↑                                │
│                      信号                              │
│                        ↓                                │
│  ┌──────────────────────────────────────────────────┐  │
│  │        管理层 (GameManager)                      │  │
│  │  - 协调所有系统                                  │  │
│  │  - 管理游戏流程                                  │  │
│  │  - 处理用户输入                                  │  │
│  └──────────────────────────────────────────────────┘  │
│           ↓              ↓              ↓               │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐   │
│  │ 模拟引擎     │ │ 叙事引擎     │ │ 数据层       │   │
│  │(Simulation)  │ │(Narrative)   │ │(Resources)   │   │
│  └──────────────┘ └──────────────┘ └──────────────┘   │
│                                                         │
│  数值规则  →  事件生成  →  文本转化  →  UI 显示       │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

---

## 分层设计

### 1. 数据层（Resource-based）

**文件**：
- `src/Core/Survivor.cs`
- `src/Core/GameState.cs`
- `src/Core/GameEvent.cs`

**特点**：
- 继承 Godot `Resource`
- 可在编辑器中编辑
- 支持序列化（保存/加载）
- 不包含业务逻辑

**依赖关系**：

```
GameState
  ├── Survivor[]
  │   ├── string: SurvivorName
  │   ├── string: Role
  │   ├── int: Hp, Hunger, Stamina
  │   ├── int: Stress, Integrity, Suspicion
  │   ├── string[]: Secrets
  │   └── Dict<string, int>: Relationships
  ├── int: Day, Supplies, Defense
  └── List<string>: EventLog

GameEvent
  ├── EventType: Type
  ├── int: Day
  ├── List<string>: InvolvedNpcs
  └── Dict<string, object>: Context
```

---

### 2. 逻辑层（Simulation Engine）

**文件**：`src/Engine/SimulationEngine.cs`

**职责**：
- 每天推进：`AdvanceDay(GameState)`
- 物资消耗：`ProcessSupplies()`
- 状态更新：`UpdateSurvivorState()`
- 秘密事件：`ProcessMasqueradeEvents()`

**核心算法**：

#### 物资消耗规则
```csharp
if (state.Supplies > 0)
{
    state.Supplies -= survivor_count;
    // 所有 NPC: hunger = 0
}
else
{
    // 所有 NPC: hunger += 20, stress += 10
}
```

#### 状态恶化规则
```csharp
if (survivor.Hunger > 80)
{
    survivor.Hp -= 10;
    survivor.Stress += 15;
}

if (survivor.Stress > 80)
{
    if (Random() < 0.3f)  // 30% 概率
        TriggerMentalBreakdown();
}
```

#### 秘密判定规则
```csharp
// 偷窃判定
float theft_chance = hunger * 0.5f;
if (integrity < 0) theft_chance += 0.3f;

// 感染恶化
if (HasSecret("Infected"))
{
    hp -= 5;
    if (Random() < 0.2f)  // 20% 概率
        suspicion += Random(5, 15);
}
```

**返回值**：`List<GameEvent>`

---

### 3. 叙事层（Narrative Engine）

**文件**：`src/Engine/NarrativeEngine.cs`

**职责**：
- 事件转化：`GenerateEventNarrative(GameEvent, GameState)`
- 日间摘要：`GenerateDaySummary(GameState)`
- 游戏结局：`GenerateEndingNarrative(GameState, bool victory)`

**处理流程**：

```csharp
public NarrativeResult GenerateEventNarrative(GameEvent evt, GameState state)
{
    switch (evt.Type)
    {
        case EventType.SuppliesStolen:
            // 隐藏真凶，仅暗示线索
            return new NarrativeResult
            {
                NarrativeText = "物资库存不符。没有人承认。每个人的眼神都躲躲闪闪。",
                Choices = ["继续调查", "保持警惕", "试图推理"]
            };
        
        case EventType.InfectionDetected:
            // 暗示异常
            return new NarrativeResult
            {
                NarrativeText = $"{npc.Name} 在夜里咳嗽个不停。那声音...听起来不太对劲。",
                Choices = ["质问此人", "私下交谈", "装作未察觉"]
            };
        
        // ... 更多事件类型
    }
}
```

**特点**：
- 模板化生成（当前实现）
- 预留 LLM API 集成点
- 隐藏真相，仅暗示线索
- 营造紧张压抑氛围

---

### 4. UI 层（UIManager）

**文件**：`src/UI/UIManager.cs`

**职责**：
- UI 渲染：`UpdateUI(GameState)`
- 日志管理：`AppendLog(string)`
- 选择显示：`ShowChoices(string[])`
- 按钮控制：`SetActionButtonsEnabled(bool)`

**UI 组件**：

```
Main (Control)
├── HBoxContainer (main_container)
│   ├── VBoxContainer (sidebar)
│   │   ├── Label (day_label)
│   │   ├── Label (supplies_label)
│   │   ├── Label (defense_label)
│   │   ├── HSeparator
│   │   └── ScrollContainer
│   │       └── VBoxContainer (survivor_cards)
│   │           └── [PanelContainer × N]
│   │
│   └── VBoxContainer (main_area)
│       ├── RichTextLabel (event_log)
│       ├── HSeparator
│       ├── VBoxContainer (action_area)
│       │   ├── Button (next_day_button)
│       │   ├── Button (meeting_button)
│       │   ├── Label
│       │   ├── LineEdit (player_input)
│       │   ├── HSeparator
│       │   └── HBoxContainer (choices_container)
```

**NPC 卡片结构**：

```
┌─────────────────────┐
│ Sarah (Doctor)      │
│ [秘密] (调试模式)    │
│ HP:    ████░░░░░░   │
│ Stress:░░░░░░░░░░   │
│ Suspic:░░░░░░░░░░   │
│ Hunger:░░░░░░░░░░   │
└─────────────────────┘
```

**信号**：
- `NextDayPressed` - 按下"下一天"按钮
- `MeetingPressed` - 按下"会议"按钮
- `ChoiceSelected(int)` - 选择了选项
- `PlayerInputSubmitted(string)` - 提交了输入

---

### 5. 管理层（GameManager）

**文件**：`src/Manager/GameManager.cs`

**职责**：
- 初始化系统
- 协调流程
- 处理用户输入
- 检查游戏结束

**核心流程**：

```
┌─────────────────────────────────┐
│  用户按下 "Next Day" 按钮       │
└──────────────┬──────────────────┘
               ↓
    ┌──────────────────────────┐
    │ GameManager.OnNextDay()  │
    └──────────────┬───────────┘
                   ↓
    ┌──────────────────────────────────┐
    │ SimulationEngine.AdvanceDay()    │
    │ → List<GameEvent> events         │
    └──────────────┬───────────────────┘
                   ↓
    ┌──────────────────────────────────┐
    │ for each event in events:        │
    │   NarrativeEngine.Generate()     │
    │   → NarrativeResult              │
    │   UIManager.AppendLog()          │
    │   UIManager.ShowChoices()        │
    └──────────────┬───────────────────┘
                   ↓
    ┌──────────────────────────────────┐
    │ UIManager.UpdateUI()             │
    │ (刷新所有 NPC 状态卡片)          │
    └──────────────┬───────────────────┘
                   ↓
    ┌──────────────────────────────────┐
    │ GameManager.CheckGameOver()      │
    │ - 全员死亡? → 失败               │
    │ - 生存30天? → 胜利               │
    └──────────────────────────────────┘
```

**命令处理**：

```csharp
ProcessCommand(string command)
├── "help" → 显示帮助
├── "status" → 显示当前状态
├── "secrets" → 显示所有秘密（调试）
├── "damage <name>" → 伤害 NPC
└── "infect <name>" → 感染 NPC
```

---

## 信号流

```
UIManager
   │
   ├─→ NextDayPressed
   │   └─→ GameManager.OnNextDayPressed()
   │       └─→ GameManager.EmitSignal(GameStateUpdated)
   │
   ├─→ MeetingPressed
   │   └─→ GameManager.OnMeetingPressed()
   │
   ├─→ ChoiceSelected(int)
   │   └─→ GameManager.OnChoiceSelected(int)
   │
   └─→ PlayerInputSubmitted(string)
       └─→ GameManager.OnPlayerInputSubmitted(string)
           └─→ GameManager.ProcessCommand(string)

GameManager
   │
   ├─→ GameStateUpdated
   │   └─→ (UI 已更新)
   │
   └─→ GameOver(bool victory)
       └─→ (显示结局)
```

---

## 数据流

### 单个回合的数据变化

```
Day 1:
  Supplies: 50
  Survivors: [Sarah, Jake, Lisa, Tom]

                ↓ SimulationEngine.AdvanceDay()

Day 2:
  ✓ ProcessSupplies()
    - Supplies: 46 (消耗 4)
    - All Hunger: 0
  
  ✓ UpdateSurvivorState()
    - Sarah: Stress 5 → 8 (自然增长)
    - Jake: Hunger 0, 检查偷窃
      → Random(0, 50) = 42 > 25? YES
      → Steals 1 supply
      → Event: SuppliesStolen
  
  ✓ ProcessMasqueradeEvents()
    - Lisa: HasSecret("Infected")
      → Hp: 100 → 95
      → Random(0, 1) = 0.15 < 0.2? YES
      → Suspicion: 10 → 18
      → Event: InfectionDetected

Events Generated:
  1. SuppliesConsumed: "消耗了 4 单位物资。"
  2. SuppliesStolen: "发现物资被盗！但没有线索指向罪犯..."
  3. InfectionDetected: "Lisa 在夜里咳嗽个不停..."

                ↓ NarrativeEngine.Generate()

Narratives:
  1. "又是无聊的一天。物资被分配下去了。"
  2. "物资库存不符。每个人的眼神都躲躲闪闪。"
     Choices: ["继续调查", "保持警惕", "试图推理"]
  3. "Lisa 在夜里咳嗽个不停。那声音...听起来不太对劲。"
     Choices: ["质问此人", "私下交谈", "装作未察觉"]

                ↓ UIManager.UpdateUI()

UI 更新:
  - DayLabel: "Day 2"
  - SuppliesLabel: "Supplies: 45"
  - NPC Cards: 刷新所有属性条
  - EventLog: 追加所有文本
  - Choices: 显示选项按钮
```

---

## 常数与规则

详见 [`src/Utilities/GameConstants.cs`](src/Utilities/GameConstants.cs)

**关键参数**：

```csharp
// 物资规则
const int SUPPLIES_PER_SURVIVOR = 1;

// 饥饿恶化
const int STARVATION_HUNGER_THRESHOLD = 80;
const int STARVATION_HP_LOSS = 10;

// 精神崩溃
const int MENTAL_BREAKDOWN_STRESS_THRESHOLD = 80;
const float MENTAL_BREAKDOWN_PROBABILITY = 0.3f;

// 感染
const int INFECTION_HP_LOSS_PER_DAY = 5;
const float INFECTION_DISCOVERY_PROBABILITY = 0.2f;

// 偷窃
const float THEFT_BASE_PROBABILITY_MULTIPLIER = 0.5f;
const float THEFT_INTEGRITY_BONUS = 0.3f;

// 游戏流程
const int VICTORY_DAY_THRESHOLD = 30;
const int INITIAL_SUPPLIES = 50;
```

---

## 扩展点

### 1. LLM 集成

修改 `NarrativeEngine.GenerateEventNarrative()` 使用 API：

```csharp
private async Task<string> GenerateWithLLM(GameEvent evt)
{
    var prompt = $"""
    你是一名《行尸走肉》风格的编剧。
    事件：{evt.Type}
    描述：{evt.Description}
    
    用不超过 100 字生成含蓄的叙事文本。
    """;
    
    var response = await _llmClient.CreateChatCompletion(prompt);
    return response.Choices[0].Message.Content;
}
```

### 2. 自定义事件

在 `GameEvent.EventType` 中添加：

```csharp
public enum EventType
{
    // ... 现有
    ResourceExchange,      // 物资交换
    Betrayal,             // 背叛
    Discovery,            // 发现秘密
    Alliance,             // 结盟
}
```

### 3. 新增 NPC 角色

修改 `GameState.Initialize()`：

```csharp
var psychologist = new Survivor("James", "Psychologist", "心理学家...");
psychologist.SetTrust("Player", 45);
Survivors.Add(psychologist);
```

### 4. 自定义游戏规则

在 `SimulationEngine` 中添加新方法：

```csharp
private void ProcessCustomRule(GameState state, Survivor survivor)
{
    // 自定义逻辑
}
```

---

## 性能考虑

### 优化建议

1. **事件缓存**：避免每次生成新对象
2. **UI 批量更新**：不要逐个刷新卡片
3. **字符串池**：重用常见字符串
4. **异步叙事**：长文本生成放在后台

---

## 测试策略

### 单元测试

```csharp
// SimulationEngine 测试
[Test]
public void Test_PhysicalDeterioratesWithHunger()
{
    var survivor = new Survivor("Test", "Test", "");
    survivor.Hunger = 85;
    
    var engine = new SimulationEngine();
    engine.UpdateSurvivorState(gameState, survivor, new List<GameEvent>());
    
    Assert.Less(survivor.Hp, 100);
}

// NarrativeEngine 测试
[Test]
public void Test_NarrativeVariesWithEventType()
{
    var engine = new NarrativeEngine();
    var evt1 = new GameEvent(GameEvent.EventType.SuppliesStolen, 1, "");
    var evt2 = new GameEvent(GameEvent.EventType.Starvation, 1, "");
    
    var result1 = engine.GenerateEventNarrative(evt1, gameState);
    var result2 = engine.GenerateEventNarrative(evt2, gameState);
    
    Assert.AreNotEqual(result1.NarrativeText, result2.NarrativeText);
}
```

---

## 已知限制与 TODO

- [ ] LLM 集成（当前为模板）
- [ ] 音效与音乐
- [ ] 美术资源
- [ ] 存档系统完善
- [ ] 网络多人（未规划）
- [ ] 翻译系统（当前仅中文）

---

**最后更新**：2026-01-29
**架构版本**：v1.0
