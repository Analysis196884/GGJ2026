# 快速开始指南 - Project Masquerade Ark

## 5 分钟快速入门

### 环境要求

- **Godot 4.3+** （支持 .NET）
- **.NET 7 SDK 或更高版本**
- **操作系统**：Windows / macOS / Linux

### 第一步：打开项目

1. 启动 Godot 编辑器
2. 选择 **导入** → 选择 `Masquerade Ark` 文件夹
3. 在项目选择器中选择 `Masquerade Ark` 项目
4. 点击 **打开**

### 第二步：生成 C# 项目

1. 等待 Godot 索引 C# 文件（可能需要 10-30 秒）
2. 上方菜单 → **项目** → **.NET** → **生成 C# 解决方案**
3. 编辑器会加载所有代码

### 第三步：运行游戏

1. 按 **F5** 或点击上方的"播放"按钮
2. 或在 **运行** 菜单中选择"运行项目"

### 第四步：游戏操作

```
界面说明：
┌──────────────────────────────────┐
│        左侧: 状态与 NPC 列表      │
│        右侧: 叙事文本与按钮       │
└──────────────────────────────────┘

主要按钮：
📅 推进到下一天    - 进行一个回合
🏛️  召开会议       - 讨论与投票
💬 输入框          - 输入调试命令
```

---

## 游戏快速体验

### 基础流程

1. **查看初始状态**
   - 左侧显示 4 个初始幸存者
   - 右侧显示欢迎信息

2. **点击 "推进到下一天"**
   - 模拟系统计算新的一天
   - 显示发生的事件
   - 更新所有 NPC 状态

3. **观察变化**
   - HP、Hunger、Stress 等属性更新
   - 可能出现物资被盗、感染迹象等事件
   - 文本日志记录所有事件

4. **作出决策**
   - 某些事件会提供选择按钮
   - 选择你的反应方式
   - 继续游戏

5. **重复**直到游戏结束或胜利

---

## 调试命令

在右侧输入框输入以下命令（调试模式）：

### 查看帮助
```
help
```

输出所有可用命令。

### 查看当前状态
```
status
```

显示：
```
Day 5 - Supplies:30 Defense:50 Survivors:4
```

### 显示所有秘密 ⚠️
```
secrets
```

显示每个 NPC 隐藏的秘密（仅调试模式）

### 伤害指定 NPC
```
damage Sarah
```

对指定 NPC 造成 20 点伤害（调试用）

### 感染指定 NPC
```
infect Jake
```

给指定 NPC 添加 "Infected" 秘密（调试用）

---

## 关键概念速览

### NPC 属性

| 属性 | 范围 | 含义 |
|------|------|------|
| **HP** | 0-100 | 生命值，≤0 时死亡 |
| **Hunger** | 0-100 | 饥饿度，>80 时伤害 HP |
| **Stress** | 0-100 | 压力值，>80 时可能崩溃 |
| **Trust**  | 0-100 | 对玩家的信任值 |
| **Integrity** | -100~100 | 道德值，影响偷窃概率 |

### 秘密类型

- **Infected** - 被感染（每天 HP-5，可能被发现）
- **Thief** - 小偷（高偷窃概率）
- **Traitor** - 背叛者
- **Coward** - 懦夫

### 事件类型

- 🛒 **SuppliesConsumed** - 物资消耗
- 🚨 **SuppliesStolen** - 物资被盗（谍报线索）
- 😫 **Starvation** - 饥饿恶化
- 💔 **MentalBreakdown** - 精神崩溃
- 🤒 **InfectionDetected** - 感染迹象
- ⚠️ **Death** - 死亡

---

## 游戏目标

### 胜利条件
✅ 生存满 30 天 → **胜利**

### 失败条件
❌ 所有 NPC 全部死亡 → **失败**

### 关键策略
1. **物资管理** - 保持充足供应
2. **信息收集** - 通过观察发现秘密
3. **人际关系** - 维护信任度
4. **投票决策** - 召开会议驱逐威胁者

---

## 常见问题

### Q1: 游戏启动很慢
**A:** 第一次运行时 Godot 需要编译 C# 代码，这可能需要 30 秒。之后会快得多。

### Q2: 为什么看不到 NPC 秘密？
**A:** 调试模式下才能看到。右侧的 NPC 卡片会显示秘密（红色文本）。

### Q3: 如何快速进行多天？
**A:** 
- 多次点击 "推进到下一天" 按钮
- 或创建脚本自动化

### Q4: 物资为什么总是减少？
**A:** 每天消耗 = 幸存者数量。例如 4 个 NPC 每天消耗 4 单位。

### Q5: 怎样避免全员饿死？
**A:** 
- 保持物资 > 消耗
- 定期检查供应状态
- 考虑驱逐一些 NPC 减少消耗

### Q6: 输入命令后没反应？
**A:** 
- 确保已按 Enter
- 命令名称小写
- 某些命令需要调试模式

---

## 进阶玩法

### 修改初始状态

编辑 [`src/Core/GameState.cs`](src/Core/GameState.cs) 的 `Initialize()` 方法：

```csharp
public void Initialize()
{
    // 修改初始幸存者
    var doctor = new Survivor("Sarah", "Doctor", "一位经验丰富的医生");
    doctor.AddSecret("Infected");  // 给医生添加秘密
    
    Survivors.Add(doctor);
    
    // 修改初始物资
    Supplies = 100;  // 改为 100
    Defense = 75;
}
```

### 调整游戏难度

编辑 [`src/Utilities/GameConstants.cs`](src/Utilities/GameConstants.cs)：

```csharp
// 降低难度
public const int STARVATION_HUNGER_THRESHOLD = 90;  // 改为 90（更高）
public const float MENTAL_BREAKDOWN_PROBABILITY = 0.15f;  // 改为 15%

// 提高难度
public const int VICTORY_DAY_THRESHOLD = 50;  // 改为 50 天（需要更长生存）
public const int INITIAL_SUPPLIES = 30;  // 改为 30（更少物资）
```

### 添加新 NPC

在 [`src/Core/GameState.cs`](src/Core/GameState.cs) 的 `Initialize()` 中：

```csharp
var scavenger = new Survivor("Alex", "Scavenger", "经验丰富的掠夺者");
scavenger.SetTrust("Player", 45);
scavenger.AddSecret("Thief");  // 秘密：小偷
Survivors.Add(scavenger);
```

---

## 项目文件导航

| 文件/目录 | 用途 | 修改频率 |
|---------|------|--------|
| [`src/Core/`](src/Core/) | 数据结构 | 低 |
| [`src/Engine/`](src/Engine/) | 游戏逻辑 | 中 |
| [`src/UI/`](src/UI/) | 界面管理 | 中 |
| [`src/Manager/`](src/Manager/) | 核心协调 | 低 |
| [`src/Utilities/`](src/Utilities/) | 常量定义 | 中 |
| [`scenes/`](scenes/) | 场景文件 | 低 |
| [`GUIDE.md`](GUIDE.md) | 设计文档 | 无 |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | 架构说明 | 无 |

---

## 后续步骤

### 深入理解
1. 阅读 [`GUIDE.md`](GUIDE.md) - 了解设计理念
2. 阅读 [`ARCHITECTURE.md`](ARCHITECTURE.md) - 理解系统设计
3. 阅读 [`README.md`](README.md) - 获取完整信息

### 自定义与扩展
1. 添加新的事件类型
2. 修改游戏规则
3. 集成 LLM API
4. 添加音效和美术

### 调试与测试
1. 使用调试命令测试各种情景
2. 添加单元测试
3. 性能分析与优化

---

## 快捷键

| 快捷键 | 功能 |
|--------|------|
| `F5` | 运行游戏 |
| `F6` | 暂停游戏 |
| `F7` | 停止游戏 |
| `F8` | 调试脚本 |
| `Ctrl+Shift+D` | 打开 Mono 调试器 |

---

## 获取帮助

### 查看日志
打开 Godot 编辑器底部的 **输出** 面板，查看 `GD.Print()` 输出的调试信息。

### 常见错误

**错误：Assembly not found**
- 解决：菜单 → **项目** → **.NET** → **生成 C# 解决方案**

**错误：Script not found**
- 解决：检查脚本路径是否正确，重启 Godot

**错误：NPC 卡片不显示**
- 解决：检查 `UIManager` 是否正确挂载在场景中

---

## 下一章：阅读架构文档

完成快速开始后，建议阅读 [`ARCHITECTURE.md`](ARCHITECTURE.md) 深入理解系统设计。

---

**版本**：v0.1.0-alpha
**最后更新**：2026-01-29
