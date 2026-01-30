using Godot;
using System.Collections.Generic;

namespace MasqueradeArk.Core
{
    [GlobalClass]
    public partial class Location : Resource
    {
        [Export] public string Name { get; set; }
        [Export] public string Description { get; set; }
        [Export] public LocationType Type { get; set; }
        [Export] public int Capacity { get; set; }
        [Export] public bool IsAvailable { get; set; }
        [Export] public int DamageLevel { get; set; } // 0 = 完好, 100 = 完全损坏
        public Dictionary<string, object> Properties { get; set; }

        public enum LocationType
        {
            MedicalWard,    // 医疗区
            RecreationRoom, // 棋牌室
            StorageRoom,    // 储藏室
            RestArea,       // 休息区
            MainHall        // 主大厅
        }

        public Location()
        {
            Properties = new Dictionary<string, object>();
            IsAvailable = true;
            DamageLevel = 0;
        }

        public Location(string name, string description, LocationType type, int capacity)
        {
            Name = name;
            Description = description;
            Type = type;
            Capacity = capacity;
            Properties = new Dictionary<string, object>();
            IsAvailable = true;
            DamageLevel = 0;
        }

        // 获取场所效率（基于损坏程度）
        public float GetEfficiency()
        {
            return Mathf.Max(0.1f, (100 - DamageLevel) / 100.0f);
        }

        // 修复场所
        public void Repair(int repairAmount)
        {
            DamageLevel = Mathf.Max(0, DamageLevel - repairAmount);
        }

        // 损坏场所
        public void Damage(int damageAmount)
        {
            DamageLevel = Mathf.Min(100, DamageLevel + damageAmount);
            if (DamageLevel >= 80)
            {
                IsAvailable = false;
            }
        }

        // 检查是否可用
        public bool CanUse()
        {
            return IsAvailable && DamageLevel < 80;
        }

        // 获取属性值
        public T GetProperty<T>(string key, T defaultValue = default(T))
        {
            if (Properties.ContainsKey(key))
            {
                return (T)Properties[key];
            }
            return defaultValue;
        }

        // 设置属性值
        public void SetProperty(string key, object value)
        {
            Properties[key] = value;
        }

        public override string ToString()
        {
            string status = CanUse() ? "可用" : "不可用";
            return $"{Name} ({Type}) - {status} (损坏度: {DamageLevel}%)";
        }
    }
}