using Godot;
using System;
using System.Collections.Generic;
using MasqueradeArk.Utilities;

namespace MasqueradeArk.Core
{
	/// <summary>
	/// GameState 表示整个游戏世界在某一时刻的状态
	/// </summary>
	[GlobalClass]
	public partial class GameState : Resource
	{
		/// <summary>当前天数</summary>
		[Export]
		public int Day { get; set; } = 1;

		/// <summary>公共物资数量</summary>
		[Export]
		public int Supplies { get; set; } = 50;

		/// <summary>基地防御值</summary>
		[Export]
		public int Defense { get; set; } = 50;

		/// <summary>所有 Survivor 实例的集合</summary>
		[Export]
		public Godot.Collections.Array<Survivor> Survivors { get; set; } = new Godot.Collections.Array<Survivor>();

		/// <summary>场所系统</summary>
		[Export]
		public Godot.Collections.Array<Location> Locations { get; set; } = new Godot.Collections.Array<Location>();

		/// <summary>事件日志 (供 UI / Debug / 存档使用)</summary>
		private List<string> _eventLog = [];

		public GameState()
		{
		}

		/// <summary>初始化游戏状态（添加初始幸存者）</summary>
		public void Initialize()
		{
			Survivors.Clear();

			// 创建初始幸存者
			var doctor = new Survivor("Sarah", "Doctor", "一位经验丰富的医生，致力于救治幸存者。");
			var mercenary = new Survivor("Jake", "Mercenary", "退役特种兵，擅长战斗和侦察。");
			var engineer = new Survivor("Lisa", "Engineer", "聪慧的工程师，能维护基地设施。");
			var farmer = new Survivor("Tom", "Farmer", "前农场主，懂得生存技能。");

			// 初始化相互关系
			doctor.SetTrust("Jake", 30);
			doctor.SetTrust("Lisa", 50);
			doctor.SetTrust("Tom", 40);
			doctor.SetTrust(GameConstants.PLAYER_NAME, GameConstants.INITIAL_TRUST);

			mercenary.SetTrust("Sarah", 20);
			mercenary.SetTrust("Lisa", 10);
			mercenary.SetTrust("Tom", 35);
			mercenary.SetTrust(GameConstants.PLAYER_NAME, 40);

			engineer.SetTrust("Sarah", 60);
			engineer.SetTrust("Jake", 15);
			engineer.SetTrust("Tom", 55);
			engineer.SetTrust(GameConstants.PLAYER_NAME, 45);

			farmer.SetTrust("Sarah", 50);
			farmer.SetTrust("Jake", 25);
			farmer.SetTrust("Lisa", 60);
			farmer.SetTrust(GameConstants.PLAYER_NAME, 55);

			Survivors.Add(doctor);
			Survivors.Add(mercenary);
			Survivors.Add(engineer);
			Survivors.Add(farmer);

			Day = 1;
			Supplies = GameConstants.INITIAL_SUPPLIES;
			Defense = GameConstants.INITIAL_DEFENSE;
			_eventLog.Clear();
			
			// 初始化场所
			InitializeLocations();
			
			AppendLog("欢迎来到废墟。你和幸存者们开始了新的一天。");
		}

		/// <summary>初始化场所</summary>
		private void InitializeLocations()
		{
			Locations.Clear();
			
			// 创建各种场所
			var medicalWard = new Location("医疗区", "可以治疗伤患，恢复生命值", Location.LocationType.MedicalWard, 4);
			var recreationRoom = new Location("棋牌室", "休闲娱乐场所，可以减少压力", Location.LocationType.RecreationRoom, 6);
			var storageRoom = new Location("储藏室", "存储物资的地方", Location.LocationType.StorageRoom, 8);
			var restArea = new Location("休息区", "社交互动场所，增加信任度", Location.LocationType.RestArea, 8);
			var mainHall = new Location("主大厅", "幸存者们聚集的主要场所", Location.LocationType.MainHall, 12);
			
			Locations.Add(medicalWard);
			Locations.Add(recreationRoom);
			Locations.Add(storageRoom);
			Locations.Add(restArea);
			Locations.Add(mainHall);
		}

		/// <summary>向事件日志添加记录</summary>
		public void AppendLog(string eventText)
		{
			_eventLog.Add($"[Day {Day}] {eventText}");
		}

		/// <summary>获取事件日志</summary>
		public string[] GetEventLog()
		{
			return _eventLog.ToArray();
		}

		/// <summary>获取最后 N 条日志</summary>
		public string[] GetRecentLogs(int count)
		{
			var startIndex = Math.Max(0, _eventLog.Count - count);
			return _eventLog.GetRange(startIndex, _eventLog.Count - startIndex).ToArray();
		}

		/// <summary>清空事件日志</summary>
		public void ClearLog()
		{
			_eventLog.Clear();
		}

		/// <summary>获取幸存者总数</summary>
		public int GetSurvivorCount()
		{
			return Survivors.Count;
		}

		/// <summary>获取指定名字的幸存者</summary>
		public Survivor GetSurvivor(string name)
		{
			foreach (var survivor in Survivors)
			{
				if (survivor.SurvivorName == name)
					return survivor;
			}
			return null;
		}

		/// <summary>所有幸存者是否都存活</summary>
		public bool AllSurvivorsAlive()
		{
			foreach (var survivor in Survivors)
			{
				if (survivor.Hp <= 0)
					return false;
			}
			return true;
		}

		/// <summary>获取存活的幸存者数量</summary>
		public int GetAliveSurvivorCount()
		{
			int count = 0;
			foreach (var survivor in Survivors)
			{
				if (survivor.Hp > 0)
					count++;
			}
			return count;
		}

		/// <summary>获取指定类型的场所</summary>
		public Location GetLocation(Location.LocationType locationType)
		{
			foreach (var location in Locations)
			{
				if (location.Type == locationType)
					return location;
			}
			return null;
		}

		/// <summary>获取指定名称的场所</summary>
		public Location GetLocation(string locationName)
		{
			foreach (var location in Locations)
			{
				if (location.Name == locationName)
					return location;
			}
			return null;
		}

		/// <summary>获取所有可用的场所</summary>
		public List<Location> GetAvailableLocations()
		{
			var availableLocations = new List<Location>();
			foreach (var location in Locations)
			{
				if (location.CanUse())
					availableLocations.Add(location);
			}
			return availableLocations;
		}

		/// <summary>创建深拷贝</summary>
		public GameState Clone()
		{
			var clone = new GameState
			{
				Day = Day,
				Supplies = Supplies,
				Defense = Defense
			};

			foreach (var survivor in Survivors)
			{
				clone.Survivors.Add(survivor.Clone());
			}

			foreach (var location in Locations)
			{
				clone.Locations.Add(location);
			}

			foreach (var log in _eventLog)
			{
				clone._eventLog.Add(log);
			}

			return clone;
		}

		public override string ToString()
		{
			return $"Day {Day} - Supplies:{Supplies} Defense:{Defense} Survivors:{GetSurvivorCount()}";
		}
	}
}
