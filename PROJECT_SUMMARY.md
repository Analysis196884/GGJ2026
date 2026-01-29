# Project Masquerade Ark - 项目总结

## 📋 项目概览

**项目名称**：Project Masquerade Ark（伪装方舟）  
**项目类型**：LLM 驱动的叙事型生存/推理悬疑游戏原型  
**开发引擎**：Godot 4.x (.NET/C#)  
**开发状态**：v0.1.0-alpha（原型阶段）  
**完成时间**：2026-01-29  

---

## 🎮 游戏设计

### 核心概念

《Masquerade Ark》是一款背景设定为"暴雪山庄模式丧尸末日"的生存推理游戏。玩家与多个 NPC 幸存者一起在资源稀缺、信息不对称的环境中生存。

**核心机制**：
- **秘密系统（Masquerade）**：每个 NPC 隐藏着秘密（被感染、小偷等），玩家只能通过观察线索推理
- **数值驱动叙事**：游戏逻辑生成数值事件 → 叙事引擎转化为情感化文本
- **完全解耦设计**：数据层、逻辑层、叙事层、UI 层完全独立

### 游戏流程

```
一个回合 (Day)：
  1. 玩家点击 "推进到下一天"
  2. SimulationEngine 计算：物资消耗、状态恶化、秘密事件
  3. NarrativeEngine 生成叙事文本（隐藏真相，仅暗示线索）
  4. UIManager 更新显示
  5. 重复或游戏结束
```

### 胜利/失败条件

- ✅ **胜利**：生存满 30 天
- ❌ **失败**：所有 NPC 全部死亡

---

## 📁 项目结构

```
AIeve/
├── src/                          # C# 源代码
│   ├── Core/                     # 数据结构层
│   │   ├── Survivor.cs           # NPC 角色数据
│   │   ├── GameState.cs          # 游戏世界状态
│   │   └── GameEvent.cs          # 事件系统
│   ├── Engine/                   # 逻辑引擎层
│   │   ├── SimulationEngine.cs   # 数值模拟（规则计算）
│   │   └── NarrativeEngine.cs    # 叙事生成（文本转化）
│   ├── Manager/                  # 协调管理层
│   │   └── GameManager.cs        # 核心协调器
│   ├── UI/                       # 用户界面层
│   │   └── UIManager.cs          # UI 管理与更新
│   └── Utilities/
│       └── GameConstants.cs      # 游戏常量定义
├── scenes/
│   └── Main.tscn                 # 主场景（自动初始化）
├── GUIDE.md                      # 项目设计指南（原始需求文档）
├── README.md                     # 完整项目文档
├── ARCHITECTURE.md               # 系统架构详解
├── QUICKSTART.md                 # 快速开始指南
├── PROJECT_SUMMARY.md            # 本文档
├── project.godot                 # Godot 项目配置
├── AIeve.csproj                  # C# 项目文件
└── .gitignore                    # Git 忽略规则
```

---

## 🛠️ 技术栈

| 层级 | 技术 | 用途 |
|------|------|------|
| **引擎** | Godot 4.x | 游戏开发引擎 |
| **语言** | C# (.NET 7) | 业务逻辑编程 |
| **数据** | Godot Resource | 可序列化的数据结构 |
| **模拟** | C# 纯逻辑 | 数值驱动游戏规则 |
| **叙事** | 模板化 + LLM 就绪 | 事件转化为文本 |
| **UI** | Godot Control | 完整 UI 系统 |

---

## 📦 核心组件

### 1. 数据层（Resource-based）

**`Survivor.cs`** - NPC 角色
- 基础属性：名字、角色、背景
- 生存属性：HP、饥饿度、精力
- 精神属性：压力、道德值、被怀疑程度
- 秘密集合：隐藏属性（被感染、小偷等）
- 关系网：与其他 NPC/玩家的信任度

**`GameState.cs`** - 游戏世界状态
- 当前天数、物资数量、防御值
- 所有 NPC 列表
- 事件日志

**`GameEvent.cs`** - 事件数据结构
- 事件类型（物资消耗、被盗、饥饿、崩溃、感染、死亡等）
- 涉及的 NPC
- 上下文信息

### 2. 逻辑层（SimulationEngine）

**职责**：实现所有游戏规则，完全独立于 UI

**核心方法**：
- `AdvanceDay()` - 推进一天，返回事件列表
- `ProcessSupplies()` - 物资消耗规则
- `UpdateSurvivorState()` - 状态恶化规则
- `ProcessMasqueradeEvents()` - 秘密事件（偷窃、感染）

**关键规则**：

```csharp
// 物资消耗
若 supplies > 0:
    supplies -= survivor_count
    所有 NPC hunger = 0
否则：
    所有 NPC hunger += 20, stress += 10

// 饥饿恶化
若 hunger > 80:
    hp -= 10, stress += 15

// 精神崩溃
若 stress > 80:
    30% 概率触发崩溃事件

// 偷窃判定
概率 = hunger * 0.5
若 integrity < 0: 概率 += 30%

// 感染恶化
若有 "Infected" 秘密:
    hp -= 5/天
    20% 概率被发现 (suspicion += 5-15)
```

### 3. 叙事层（NarrativeEngine）

**职责**：将冷硬数值转化为情感化、含蓄的文本

**核心方法**：
- `GenerateEventNarrative()` - 事件转化为文本 + 选择
- `GenerateDaySummary()` - 生成日间摘要
- `GenerateEndingNarrative()` - 生成游戏结局

**特点**：
- 隐藏真相，仅暗示线索
- 营造紧张压抑氛围
- 支持随机变化
- 预留 LLM API 集成点

**例子**：
```
事件：SuppliesStolen
叙事："物资库存不符。没有人承认。每个人的眼神都躲躲闪闪。"
选择：["继续调查", "保持警惕", "试图推理"]
```

### 4. UI 层（UIManager）

**职责**：完整的用户界面管理

**布局**：
- **左侧**：日期、物资、防御 + NPC 状态卡片列表
- **右侧**：事件日志 + 行动按钮 + 选择按钮

**关键功能**：
- 实时更新 NPC 状态栏（HP、Stress、Suspicion 等）
- 追加日志文本
- 动态生成选择按钮
- 调试模式显示秘密

**信号**：
- `NextDayPressed` → 下一天
- `MeetingPressed` → 召开会议
- `ChoiceSelected(int)` → 选择选项
- `PlayerInputSubmitted(string)` → 提交命令

### 5. 管理层（GameManager）

**职责**：协调所有系统的核心类

**核心流程**：
```
用户输入 → GameManager → SimulationEngine → NarrativeEngine → UIManager
```

**支持的调试命令**：
- `help` - 显示帮助
- `status` - 显示当前状态
- `secrets` - 显示所有秘密（调试）
- `damage <name>` - 伤害指定 NPC
- `infect <name>` - 感染指定 NPC

---

## 📊 实现统计

### 代码量

| 文件 | 行数 | 说明 |
|------|------|------|
| `Survivor.cs` | ~130 | NPC 数据结构 |
| `GameState.cs` | ~150 | 游戏状态管理 |
| `GameEvent.cs` | ~60 | 事件系统 |
| `SimulationEngine.cs` | ~250 | 模拟引擎 |
| `NarrativeEngine.cs` | ~200 | 叙事引擎 |
| `UIManager.cs` | ~350 | UI 系统 |
| `GameManager.cs` | ~300 | 游戏管理 |
| `GameConstants.cs` | ~80 | 常量定义 |
| **总计** | **~1520** | 核心源代码 |

### 文档量

| 文档 | 用途 |
|------|------|
| `GUIDE.md` | 原始设计文档（339 行） |
| `README.md` | 完整项目文档（~400 行） |
| `ARCHITECTURE.md` | 系统架构详解（~500 行） |
| `QUICKSTART.md` | 快速开始指南（~400 行） |

---

## ✅ 完成度清单

### 已实现

- [x] **数据层**：所有核心数据结构（Survivor、GameState、GameEvent）
- [x] **逻辑层**：完整的模拟引擎，包括所有规则
- [x] **叙事层**：叙事引擎框架 + 模板化实现
- [x] **UI 层**：完整的 Godot UI 系统
- [x] **管理层**：游戏协调管理器
- [x] **场景**：主场景配置
- [x] **项目配置**：Godot + C# 项目文件
- [x] **文档**：完整的设计、架构、快速开始文档
- [x] **调试系统**：控制台命令系统

### 待实现（v0.2+ 路线图）

- [ ] **LLM 集成**：OpenAI/Claude API 调用
- [ ] **音效系统**：背景音乐、效果音
- [ ] **美术资源**：角色立绘、背景等
- [ ] **存档系统**：完善的存档/读档功能
- [ ] **存档验证**：保存/加载的完整测试
- [ ] **多语言支持**：国际化（i18n）
- [ ] **性能优化**：字符串池、对象池等
- [ ] **单元测试**：完整的测试覆盖

---

## 🚀 快速开始

### 环境要求
- Godot 4.3+（支持 .NET）
- .NET 7 SDK

### 5 步快速启动
1. 打开 Godot，选择 `AIeve` 项目
2. 菜单 → **项目** → **.NET** → **生成 C# 解决方案**
3. 等待编译完成
4. 按 `F5` 运行游戏
5. 点击 "推进到下一天" 开始游戏

详见 [`QUICKSTART.md`](QUICKSTART.md)

---

## 🔧 扩展指南

### 添加新事件类型

1. 在 `GameEvent.cs` 添加事件类型
2. 在 `SimulationEngine` 触发事件
3. 在 `NarrativeEngine` 处理叙事

```csharp
case GameEvent.EventType.NewEvent:
    result.NarrativeText = "新的叙事文本...";
    result.Choices = [...];
    break;
```

### 修改游戏规则

编辑 `GameConstants.cs` 中的相关常量：

```csharp
public const int STARVATION_HUNGER_THRESHOLD = 80;  // 改为 90
public const float MENTAL_BREAKDOWN_PROBABILITY = 0.3f;  // 改为 0.2f
```

### 集成 LLM

修改 `NarrativeEngine.GenerateEventNarrative()`：

```csharp
var prompt = BuildLLMPrompt(evt, state);
var response = await _llmClient.CreateCompletion(prompt);
return response.Text;
```

详见 [`ARCHITECTURE.md`](ARCHITECTURE.md) 的"扩展点"部分

---

## 📖 文档导航

| 文档 | 受众 | 内容 |
|------|------|------|
| [`GUIDE.md`](GUIDE.md) | 设计师/PM | 原始需求与设计哲学 |
| [`README.md`](README.md) | 所有人 | 项目概览与完整文档 |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | 开发者 | 系统架构与详细设计 |
| [`QUICKSTART.md`](QUICKSTART.md) | 新手 | 快速启动与基本操作 |
| [`PROJECT_SUMMARY.md`](PROJECT_SUMMARY.md) | 管理者 | 项目总结与统计 |

---

## 🎯 设计哲学

> **真相被隐藏，只有压力、线索和猜疑浮出水面。**

### 三层分离

1. **Simulation 决定"发生了什么"**
   - 遵循严格的数值规则
   - 生成客观的数值事件

2. **Narrative 决定"玩家如何感知发生的事"**
   - 模糊真相，仅暗示线索
   - 营造氛围和紧张感

3. **UI 只负责呈现，不参与规则**
   - 完全解耦
   - 易于测试和扩展

---

## 💡 创新点

1. **完全解耦的架构**
   - 逻辑与 UI 完全独立
   - 易于单元测试
   - 易于移植到其他平台

2. **LLM-Ready 设计**
   - 预留 API 集成点
   - 可轻松替换模板为真实 LLM
   - 支持多种生成策略

3. **秘密系统**
   - 信息不对称的游戏体验
   - 推理与猜测的乐趣
   - 可重放性强

4. **事件驱动叙事**
   - 数值变化自动生成故事
   - 每次游戏都不同
   - 减少工作量

---

## 📞 项目信息

- **项目版本**：v0.1.0-alpha
- **Godot 版本**：4.3+
- **.NET 版本**：7.0+
- **开发语言**：C#
- **许可证**：[待定]
- **作者**：Masquerade Ark Development Team

---

## 🔗 快速链接

- [`QUICKSTART.md`](QUICKSTART.md) - 5 分钟快速开始
- [`ARCHITECTURE.md`](ARCHITECTURE.md) - 系统架构深度讲解
- [`README.md`](README.md) - 完整项目文档
- [`GUIDE.md`](GUIDE.md) - 原始设计指南

---

## 📝 更新日志

### v0.1.0-alpha（2026-01-29）
- ✅ 核心数据结构完成
- ✅ 模拟引擎全功能实现
- ✅ 叙事引擎框架搭建
- ✅ UI 系统完整实现
- ✅ 游戏管理器集成
- ✅ 项目配置与文档完成

### v0.2.0（计划中）
- [ ] LLM 集成
- [ ] 音效系统
- [ ] 美术资源
- [ ] 完善存档系统

---

**项目状态**：🟡 原型完成，待测试和优化

**最后更新**：2026-01-29 16:10

**下一步**：测试系统集成，修复 bug，优化性能
