1. 修正信任值系统：
   - 将 Survivor 的 Trust 属性与GetTrust("Player") 方法对齐
   - 在 LLMClient.cs 中，修改 CallDeepSeekApiForInteraction 或其他关联方法，确保 Trust 值（以及其它的属性值）被正确修改

2. 添加大模型生成随机事件机制：
   - 在 SimulationEngine.cs 中，创建 GenerateRandomEventUsingLLM(GameState state) 方法
   - 该方法调用 LLMClient.cs 中的大模型 API，传递当前 GameState 和 NPC 状态，获取生成的随机事件（异步调用）
   - 将生成的事件添加到 GameState 的事件日志中，并更新相关 NPC/庇护所 状态
   - 进行上下文管理，将历史事件的压缩总结传递给大模型，鼓励生成“蝴蝶效应式”连锁事件，鼓励事件的多样性、戏剧性、趣味性
