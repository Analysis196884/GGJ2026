using Godot;
using System;
using System.Collections.Generic;

namespace MasqueradeArk.Core
{
	/// <summary>
	/// 游戏事件数据结构
	/// </summary>
	[GlobalClass]
	public partial class GameEvent : Resource
	{
		/// <summary>事件类型</summary>
		public enum EventType
		{
			SuppliesConsumed,      // 物资消耗
			SuppliesStolen,        // 物资被盗
			Starvation,            // 饥饿恶化
			MentalBreakdown,       // 精神崩溃
			InfectionDetected,     // 感染迹象
			Illness,               // 疾病
			Death,                 // 死亡
			Custom                 // 自定义事件
		}

		public EventType Type { get; set; }
		public int Day { get; set; }
		public List<string> InvolvedNpcs { get; set; } = new List<string>();
		public Dictionary<string, object> Context { get; set; } = new Dictionary<string, object>();
		public string Description { get; set; } = "";

		public GameEvent()
		{
		}

		public GameEvent(EventType type, int day, string description)
		{
			Type = type;
			Day = day;
			Description = description;
		}

		public void AddInvolvedNpc(string npcName)
		{
			if (!InvolvedNpcs.Contains(npcName))
			{
				InvolvedNpcs.Add(npcName);
			}
		}

		public void SetContextValue(string key, object value)
		{
			Context[key] = value;
		}

		public object GetContextValue(string key, object defaultValue = null)
		{
			return Context.TryGetValue(key, out var value) ? value : defaultValue;
		}

		public override string ToString()
		{
			return $"[Day {Day}] {Type}: {Description}";
		}
	}
}
