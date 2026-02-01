# Project Masquerade Ark - 架构文档

## 系统架构概览

```
┌─────────────────────────────────────────────────────────┐
│                    Godot 4.x Engine                     │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  ┌──────────────────────────────────────────────────┐   │
│  │            UI 层 (UIManager)                     │   │
│  │  - 侧边栏 (Status + NPC Cards)                   │   │
│  │  - 主区域 (Narrative + Actions)                  │   │
│  │  - 选择按钮 (Dynamic Choices)                    │   │
│  └──────────────────────────────────────────────────┘   │
│                        ↑                                │
│                      信号                               │
│                        ↓                                │
│  ┌──────────────────────────────────────────────────┐   │
│  │        管理层 (GameManager)                      │   │
│  │  - 协调所有系统                                  │   │
│  │  - 管理游戏流程                                  │   │
│  │  - 处理用户输入                                  │   │
│  └──────────────────────────────────────────────────┘  │
│           ↓              ↓              ↓              │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐    │
│  │ 模拟引擎     │ │ 叙事引擎      │ │ 数据层        │    │
│  │(Simulation)  │ │(Narrative)   │ │(Resources)   │    │
│  └──────────────┘ └──────────────┘ └──────────────┘    │
│                                                        │
│  数值规则  →  事件生成  →  文本转化  →  UI 显示          │
│                                                        │
└────────────────────────────────────────────────────────┘
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
  │   ├── int: Stress, Integrity, Trust
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

---

### 3. 叙事层（Narrative Engine）

**文件**：`src/Engine/NarrativeEngine.cs`

**职责**：
- 事件转化：`GenerateEventNarrative(GameEvent, GameState)`
- 日间摘要：`GenerateDaySummary(GameState)`
- 游戏结局：`GenerateEndingNarrative(GameState, bool victory)`

**特点**：
- 模板化生成 + LLM API 集成
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
│ Trust :░░░░░░░░░░   │
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
    │ - 全员死亡? → 失败                │
    │ - 生存30天? → 胜利                │
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

**考虑LLM生成随机事件**：
```csharp
private GameEvent GenerateRandomEvent(GameState state)
{
    ... // 生成逻辑
}
```

### 3. 新增 NPC 角色

修改 `GameState.Initialize()`：

```csharp
var psychologist = new Survivor("James", "Psychologist", "心理学家...");
psychologist.SetTrust("Player", 45);
Survivors.Add(psychologist);
```

(是否考虑在游戏过程中动态生成/增加 NPC？)

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

## 图形化界面

当前游戏界面简洁，以文本/Button 为主。未来可考虑：
- 添加图标/头像
- 动画效果
- 事件触发音效
- 加入插图（传统方式/大模型生成）

**最后更新**：2026-01-29
**最新版本**：v0.0
