# TODO: Implement LLM Decision & Interaction System

目标：将 LLM 从单纯的“叙事生成器”升级为“游戏逻辑裁判”。LLM 需要解析玩家输入，返回 JSON 格式 的数据来驱动游戏数值变化（信任度、压力值、成功/失败判定）。
1. 定义数据契约 (Data Contract)
我们需要一个强类型的 C# 类来接收 LLM 的决策结果。

创建 NarrativeActionResponse.cs:
这是一个 DTO (Data Transfer Object)。
属性:
NarrativeText (string): NPC 的回应文本。
StressDelta (int): 压力值变化量 (e.g., -10, +5)。
TrustDelta (int): 信任度变化量。
IsSuccess (bool): 玩家意图是否达成。
Mood (string): NPC 情绪关键词 (e.g., "Angry", "Neutral").

2. 实现交互逻辑 (Narrative Logic)
在 NarrativeManager.cs 中实现核心的交互处理函数。

实现 ProcessPlayerInteraction(Survivor npc, string playerInput):
Input: 目标 NPC 对象，玩家输入的文本。(注意要和以“/”开头的命令区分开)
System Prompt: 编写提示词，强制要求 LLM 只返回 JSON，并根据 NPC 性格 (Bio) 和状态 (Stress) 判定玩家输入的说服效果。
User Prompt: 注入 NPC 当前状态数据和玩家文本。
Output: 调用 LLMClient.GenerateTextAsync。

实现 JSON 解析与应用:
增加 CleanJson(string raw) 辅助函数，去除可能存在的 Markdown 标记 (json ...)。
反序列化 JSON 到 NarrativeActionResponse。
关键逻辑: 根据解析出的 Delta 值，直接修改 npc.Stress 和 npc.Relationships。
3. UI 交互接入 (UI Implementation)
创建一个弹窗让玩家输入对话。

创建交互弹窗 (InteractionDialog):
包含: Label (显示 NPC 名字), TextEdit (玩家输入), Button (发送)。
逻辑: 点击发送 -> 禁用按钮 -> 调用 NarrativeManager.ProcessPlayerInteraction -> 等待结果。

反馈展示:
收到结果后，用飘字或弹窗显示 NPC 的回应 (NarrativeText)。
视觉反馈: 如果 IsSuccess 为 true，播放成功音效/绿色特效；否则播放失败音效/红色特效。
刷新主界面的 NPC 状态条 (观察数值变化)。