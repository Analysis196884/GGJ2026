using Godot;
using System;

namespace MasqueradeArk.Core
{
    /// <summary>
    /// NarrativeActionResponse - LLM 决策结果的 DTO
    /// 用于接收 LLM 返回的 JSON 数据，驱动游戏数值变化
    /// </summary>
    [GlobalClass]
    public partial class NarrativeActionResponse : Resource
    {
        /// <summary>NPC 的回应文本</summary>
        [Export]
        public string NarrativeText { get; set; } = "";

        /// <summary>压力值变化量 (e.g., -10, +5)</summary>
        [Export]
        public int StressDelta { get; set; } = 0;

        /// <summary>信任度变化量</summary>
        [Export]
        public int TrustDelta { get; set; } = 0;

        /// <summary>玩家意图是否达成</summary>
        [Export]
        public bool IsSuccess { get; set; } = false;

        /// <summary>NPC 情绪关键词 (e.g., "Angry", "Neutral")</summary>
        [Export]
        public string Mood { get; set; } = "Neutral";

        public NarrativeActionResponse()
        {
        }

        public NarrativeActionResponse(string narrativeText, int stressDelta, int trustDelta, bool isSuccess, string mood)
        {
            NarrativeText = narrativeText;
            StressDelta = stressDelta;
            TrustDelta = trustDelta;
            IsSuccess = isSuccess;
            Mood = mood;
        }

        public override string ToString()
        {
            return $"NarrativeActionResponse: Text='{NarrativeText}', StressDelta={StressDelta}, TrustDelta={TrustDelta}, IsSuccess={IsSuccess}, Mood='{Mood}'";
        }
    }
}