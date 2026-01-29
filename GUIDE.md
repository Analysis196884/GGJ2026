# Project Masquerade Ark – Development Guide (Godot 4.x / C#)

**Project Masquerade Ark** 是一个由 **LLM 驱动的叙事型生存 / 推理悬疑游戏原型**。
游戏背景为 **暴雪山庄模式的丧尸末日**，整体风格参考《The Walking Dead》。

技术栈说明：

* **引擎**：Godot 4.x (.NET / C#)
* **数据层**：Godot `Resource`
* **逻辑层**：纯 C#（无 UI 依赖）
* **叙事层**：LLM 作为 Game Master（DM）
* **UI 层**：Godot Scene + Control 节点（无 Python / Streamlit）

---

## Step 1 — Core Data Structures (Godot Resources)

本项目采用 **Godot Resource 作为核心数据载体**，以支持：

* 在编辑器中可视化编辑
* 可序列化保存 / 载入
* 与 UI、模拟、叙事层解耦

### 1. NPC Class (`Survivor`)

`Survivor` 表示一个幸存者 NPC，继承自 `Resource`。

**基础属性**：

* `name` (string)：角色姓名
* `role` (string)：角色定位（Doctor / Mercenary 等）
* `bio` (string)：角色背景描述

**生存属性（0–100）**：

* `hp`：生命值
* `hunger`：饥饿度（初始为 0）
* `stamina`：精力

**精神属性**：

* `stress` (0–100)：压力值，越高越容易精神崩溃
* `integrity` (-100 ~ 100)：道德值，决定是否会突破底线
* `suspicion` (0–100)：被团队怀疑的程度

**秘密（Masquerade Core Mechanic）**：

* `secrets`：隐藏属性集合
  例如：

  * `"Infected"`（被感染）
  * `"Thief"`（偷窃者）

这些信息 **默认不向玩家显示**，只被模拟系统与叙事系统使用。

**关系网（Relationships）**：

* `relationships`：字典结构

  * Key：NPC 名字或 `"Player"`
  * Value：信任度（Trust Score）

示例：

```text
Player -> 50
Bob -> -10
```

---

### 2. Game State Class (`GameState`)

`GameState` 表示整个游戏世界在某一时刻的状态，同样继承自 `Resource`。

包含字段：

* `day`：当前天数
* `supplies`：公共物资数量
* `defense`：基地防御值
* `survivors`：所有 `Survivor` 实例的集合
* `event_log`：已发生的事件文本记录（供 UI / Debug / 存档使用）

---

### 3. 设计原则

* `Survivor` 与 `GameState` **不包含复杂逻辑**
* 所有规则运算由 Simulation Engine 处理
* Resource 只负责：

  * 数据结构
  * 状态存储
  * 编辑器友好性

---

## Step 2 — Core Simulation Loop (Simulation Engine)

本阶段实现游戏的 **数值驱动模拟系统**，完全独立于 UI。

### Simulation Engine 职责

* 接收 `GameState`
* 推进时间（按“天”）
* 更新 NPC 状态
* 触发隐藏事件（Masquerade）

核心入口方法概念：

```text
AdvanceDay(GameState state)
```

---

### 1. 资源消耗规则

* 每天默认每个 NPC 消耗 1 单位物资
* 当前原型阶段 **不区分手动分配**

规则：

* 若 `supplies > 0`

  * 每名 NPC：

    * `hunger = 0`
  * `supplies -= survivor_count`

* 若 `supplies <= 0`

  * 每名 NPC：

    * `hunger += 20`
    * `stress += 10`

设计意图：

> 饥饿是压力和犯罪行为的主要驱动力。

---

### 2. 状态恶化规则

* 当 `hunger > 80`

  * `hp -= 10`
  * `stress += 15`

* 当 `stress > 80`

  * 有 30% 概率触发 **精神崩溃事件（Breakdown）**
  * 该事件以 **信号 / Event 数据** 形式返回，供叙事系统使用

---

### 3. Masquerade Mechanic（秘密检定）

这是本项目的核心系统之一。

#### 偷窃判定（Theft）

对每个 NPC，每天进行一次独立判定：

* 基础概率：

  ```
  hunger * 0.5
  ```
* 若 `integrity < 0`

  * 额外 +30%

若判定成功：

* `supplies -= 1`
* 触发事件 `"Supplies Stolen"`
* **不记录是谁偷的**
* 仅作为“匿名事件”传递给叙事系统

设计意图：

> 玩家只能看到“结果”和“线索”，而非真相。

---

#### 感染判定（Infected）

若 NPC 拥有 `"Infected"` 秘密：

* 每天：

  * `hp -= 5`
* 每天有 20% 概率：

  * 被其他人目击异常（如咳嗽）
  * `suspicion += X`

---

## Step 3 — Narrative Engine (LLM as Game Master)

本阶段引入 **LLM 作为 Game Master（DM）**。

Narrative Engine 的职责是：

* 接收 **数值事件**
* 将“冷冰冰的规则结果”转化为 **情绪化、含蓄、带线索的文本叙事**

---

### 核心功能概念

```text
GenerateEventNarrative(
  event_type,
  involved_npcs,
  context_summary
)
```

---

### System Prompt 设计原则

Narrative Engine 内部嵌入固定 System Prompt，约束 LLM 行为：

**角色定位**：

* 你是一名《行尸走肉》风格的残酷编剧
* 语气压抑、冷静、现实、不煽情

**任务**：

* 根据输入的 JSON（NPC 状态、数值变化、事件类型）
* 生成一段剧情描述

**限制**：

* 不超过 100 字
* 聚焦：

  * 微表情
  * 潜台词
  * 团队紧张气氛
* 若事件为 `"Theft"`：

  * 禁止直接点名
  * 只能描写线索与异常

---

### 输出结构

Narrative Engine 返回结构化结果：

* `narrative_text`：剧情文本
* `choices`（可选）：

  * 2–3 个玩家可选行动，用于后续分支

---

## Step 4 — Godot UI Layer (Replacing Streamlit)

**本项目不使用 Streamlit 或 Python UI。**

所有 UI 均由 **Godot Control 系统** 实现。

---

### UI 架构概览

* **左侧面板（Sidebar）**

  * Day X
  * Supplies / Defense
  * NPC 状态卡片列表

    * Name
    * HP Bar
    * Stress Bar
    * Suspicion Bar
  * Debug 模式下显示 NPC 秘密

* **主区域**

  * 顶部：剧情 / 事件日志（RichTextLabel）
  * 中部：行动区

    * Next Day
    * Hold Meeting
    * 玩家输入框
  * 底部：决策按钮区（由 Narrative Engine 动态生成）

---

### UIManager 职责

UIManager 是 UI 与逻辑之间的桥梁：

* `UpdateUI(GameState)`

  * 刷新所有显示状态
* `AppendLog(string text)`

  * 将新剧情追加到日志（可带打字机效果）

---

### 信号与流程

核心流程：

```text
[Next Day Button]
      ↓
GameManager.AdvanceDay()
      ↓
SimulationEngine 计算结果
      ↓
NarrativeEngine.Generate()
      ↓
UIManager.AppendLog()
```

所有系统通过 **Godot Signal 解耦通信**。

---

## 总体设计哲学

* **Simulation 决定“发生了什么”**
* **Narrative 决定“玩家如何感知发生的事”**
* **UI 只负责呈现，不参与规则**

> 真相被隐藏，只有压力、线索和猜疑浮出水面。
> 这就是 Masquerade Ark 的核心体验。
