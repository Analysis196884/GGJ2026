# Project Masquerade Ark

**LLM 驱动的叙事型生存 / 推理悬疑游戏**

## 项目概述

《Masquerade Ark》是一款由 **Godot 4.x (.NET/C#)** 开发的叙事型生存游戏。游戏背景为"暴雪山庄模式的丧尸末日"，玩家需要在资源稀缺、信息不对称的环境中生存和推理。

### 核心特性

- **秘密机制（Masquerade）**：NPC 隐藏秘密，玩家只能通过观察线索推理
- **数值驱动叙事**：模拟系统生成数值事件，叙事引擎将其转化为情感化文本
- **LLM 整合就绪**：预留 API 接口，可集成 OpenAI/Claude 等模型
- **完全解耦设计**：数据层、逻辑层、叙事层、UI 层独立运作

---

## 项目结构

```
AIeve/
├── src/
│   ├── Core/
│   │   ├── Survivor.cs          # NPC 数据结构
│   │   ├── GameState.cs         # 游戏世界状态
│   │   └── GameEvent.cs         # 事件系统
│   ├── Engine/
│   │   ├── SimulationEngine.cs  # 数值模拟引擎
│   │   └── NarrativeEngine.cs   # 叙事生成引擎
│   ├── Manager/
│   │   └── GameManager.cs       # 核心协调管理器
│   └── UI/
│       └── UIManager.cs         # UI 管理系统
├── scenes/
│   └── Main.tscn                # 主场景
├── project.godot                # Godot 项目配置
├── AIeve.csproj                 # C# 项目文件
├── GUIDE.md                     # 项目设计指南
└── README.md                    # 本文件
```

---

## 技术栈

| 层次 | 技术 | 说明 |
|------|------|------|
| **引擎** | Godot 4.x | 游戏开发引擎 |
| **语言** | C# (.NET 7) | 主要编程语言 |
| **数据** | Godot Resource | 可序列化的游戏资源 |
| **逻辑** | SimulationEngine | 数值驱动，UI 独立 |
| **叙事** | NarrativeEngine | 模板化 + LLM 就绪 |
| **UI** | Godot Control | 完整的 UI 系统 |

---

## 核心系统设计

### 1. 数据结构层（Resource-based）

#### [`Survivor.cs`](src/Core/Survivor.cs) - NPC 角色

```csharp
public class Survivor : Resource
{
	// 基础属性
	public string SurvivorName;
	public string Role;              // Doctor, Mercenary, Engineer, etc.
	
	// 生存属性 (0-100)
	public int Hp;
	public int Hunger;
	public int Stamina;
	
	// 精神属性
	public int Stress;               // 压力值 (0-100)
	public int Integrity;            // 道德值 (-100~100)
	public int Trust;            	 // 对玩家的信任程度 (0-100)
	
	// 秘密集合
	public string[] Secrets;         // "Infected", "Thief", etc.
	
	// 关系网
	public Dictionary<string, int> Relationships;
}
```

#### [`GameState.cs`](src/Core/GameState.cs) - 世界状态

```csharp
public class GameState : Resource
{
	public int Day;
	public int Supplies;             // 公共物资
	public int Defense;              // 基地防御值
	public Survivor[] Survivors;
	public List<string> EventLog;
}
```

---

### 2. 模拟引擎（SimulationEngine）

**职责**：处理所有游戏规则和数值计算

#### 资源消耗规则

```
若 supplies > 0:
	每名 NPC: hunger = 0
	supplies -= survivor_count

若 supplies <= 0:
	每名 NPC: hunger += 20, stress += 10
```

#### 秘密事件

**偷窃判定**：
```
基础概率 = hunger * 0.5
若 integrity < 0: 概率 += 30%
```

**感染恶化**：
```
若有 "Infected" 秘密:
	每天 hp -= 5
	20% 概率被发现
```

#### 精神状态

```
若 hunger > 80:  hp -= 10, stress += 15
若 stress > 80:  30% 概率精神崩溃
```

---

### 3. 叙事引擎（NarrativeEngine）

**职责**：将冷硬数据转化为情感化叙事

支持的事件类型：
- `SuppliesConsumed` → 日常消耗叙事
- `SuppliesStolen` → 物资被盗线索
- `Starvation` → 饥饿恶化场景
- `MentalBreakdown` → 精神崩溃场景
- `InfectionDetected` → 异常迹象线索
- `Death` → 死亡悼词

**特点**：
- 隐藏真相，仅暗示线索
- 营造紧张压抑氛围
- 预留 LLM API 集成点

---

### 4. UI 管理器（UIManager）

**布局结构**：

```
┌─────────────────────────────────┐
│         Godot 窗口              │
├──────────────┬──────────────────┤
│              │                  │
│   侧边栏     │    主区域        │
│              │                  │
│  Day X       │  事件日志        │
│  Supplies    │  (RichTextLabel) │
│  Defense     │                  │
│              │  ─────────────   │
│ NPC 卡片     │                  │
│ ┌─────────┐ │  行动按钮        │
│ │Name: Dr │ │ [Next Day]       │
│ │HP ████░ │ │ [Meeting]        │
│ │Stress░░ │ │                  │
│ |Trust ░░ │ │  选择按钮        │
│ └─────────┘ │ [Choice 1] ...   │
│             │                  │
└─────────────┴──────────────────┘
```

**关键功能**：
- 实时更新 NPC 状态栏
- 日志追加与打字机效果
- 动态生成选择按钮
- 调试模式显示秘密

---

### 5. 游戏管理器（GameManager）

**核心流程**：

```
[玩家点击 Next Day]
		↓
GameManager.AdvanceDay()
		↓
SimulationEngine.AdvanceDay()  ← 数值计算
		↓
for each event:
	NarrativeEngine.Generate()  ← 叙事生成
	UIManager.AppendLog()       ← 显示文本
	UIManager.ShowChoices()     ← 展示选项
		↓
UIManager.UpdateUI()           ← 刷新所有状态
		↓
CheckGameOver()
```

**支持的命令**（调试）：
```
help                    # 显示帮助
status                  # 显示当前状态
secrets                 # 显示所有秘密
damage <name>           # 伤害指定幸存者
infect <name>           # 感染指定幸存者
```

---

## 快速开始

### 前置条件

- Godot 4.3+（启用 .NET 支持）
- .NET 7 SDK

### 安装与运行

1. **克隆项目**
   ```bash
   git clone <repo-url> AIeve
   cd AIeve
   ```

2. **打开 Godot**
   ```bash
   godot
   ```
   选择 `AIeve` 文件夹作为项目

3. **运行游戏**
   在 Godot 编辑器中按 `F5` 或点击"播放"按钮

4. **查看日志**
   打开 Godot 底部的"输出"面板查看调试信息

---

## 游戏流程

### 单个回合（Day）

1. **玩家点击 "推进到下一天"**
2. **模拟系统计算**：
   - 物资消耗
   - NPC 状态更新
   - 秘密事件（偷窃、感染）
3. **叙事系统生成**：
   - 将数值结果转化为文本
   - 可能展示选择选项
4. **UI 更新**：
   - 显示叙事文本
   - 刷新 NPC 状态栏
5. **检查胜利/失败**：
   - 全员死亡 → 失败
   - 生存 30 天 → 胜利

### 会议系统

玩家可以召开会议，进行：
- 讨论与指控
- 投票驱逐 NPC
- 交换信息

---

## 扩展指南

### 集成 LLM

修改 [`NarrativeEngine.cs`](src/Engine/NarrativeEngine.cs) 的 `GenerateEventNarrative()` 方法：

```csharp
private async Task<string> GenerateWithLLM(GameEvent evt, GameState state)
{
	var prompt = BuildPrompt(evt, state);
	var response = await _llmClient.CreateCompletion(prompt);
	return response.Text;
}
```

### 自定义事件

在 [`GameEvent.cs`](src/Core/GameEvent.cs) 中添加新事件类型：

```csharp
public enum EventType
{
	// ... 现有事件
	CustomEvent,  // 新事件
}
```

然后在 [`NarrativeEngine.cs`](src/Engine/NarrativeEngine.cs) 中处理：

```csharp
case GameEvent.EventType.CustomEvent:
	result.NarrativeText = GenerateCustomNarrative(evt);
	break;
```

### 修改游戏规则

编辑 [`SimulationEngine.cs`](src/Engine/SimulationEngine.cs) 中的相关方法：
- `ProcessSupplies()` - 改变物资消耗规则
- `UpdateSurvivorState()` - 修改状态恶化逻辑
- `ProcessMasqueradeEvents()` - 调整秘密事件概率

---

## 文件约定

- **C# 源代码**：`src/` 目录，按功能分类
- **Godot 场景**：`scenes/` 目录，`.tscn` 格式
- **资源文件**：`assets/` 目录（图片、音频等）
- **配置文件**：项目根目录

---

## 调试技巧

### 启用调试信息

在 Godot 编辑器中，打开"输出"面板查看 `GD.Print()` 输出。

### 命令行运行

```bash
# 无 UI 调试模式
godot --headless --debug

# 运行特定场景
godot scenes/Main.tscn
```

### 查看事件日志

在游戏中输入 `status` 查看当前状态：

```
Day 5 - Supplies:30 Defense:50 Survivors:4
```

---

## 设计哲学

> **真相被隐藏，只有压力、线索和猜疑浮出水面。**

- **Simulation 决定"发生了什么"** ← 遵循规则
- **Narrative 决定"玩家如何感知发生的事"** ← 模糊真相
- **UI 只负责呈现，不参与规则** ← 完全解耦

---

## 更新日志

### v0.1.0（初版）
- [x] 核心数据结构实现
- [x] 模拟引擎完成
- [x] 叙事引擎框架搭建
- [x] UI 系统实现
- [x] 游戏管理器集成
- [x] LLM 集成
- [ ] 音效与美术（待实现）
- [ ] 存档系统完善（待实现）

---

**最后更新**：2026-02-01

**项目版本**：v0.1.0-alpha
