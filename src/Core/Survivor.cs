using Godot;
using System;
using System.Collections.Generic;
using MasqueradeArk.Utilities;

namespace MasqueradeArk.Core
{
    /// <summary>
    /// Survivor 表示一个幸存者 NPC，继承自 Resource
    /// </summary>
    [GlobalClass]
    public partial class Survivor : Resource
    {
        /// <summary>基础属性</summary>
        [Export]
        public string SurvivorName { get; set; } = "";

        [Export]
        public string Role { get; set; } = "";

        [Export]
        public string Bio { get; set; } = "";

        /// <summary>生存属性 (0–100)</summary>
        [Export]
        public int Hp { get; set; } = 100;

        [Export]
        public int Hunger { get; set; } = 0;

        [Export]
        public int Stamina { get; set; } = 100;

        /// <summary>精神属性</summary>
        [Export]
        public int Stress { get; set; } = 0;

        [Export]
        public int Integrity { get; set; } = 0; // -100 ~ 100

        [Export]
        public int Trust { get; set; } = 100; // 0–100 (100表示完全信任)

        /// <summary>秘密集合 (例如: "Infected", "Thief")</summary>
        [Export]
        public string[] Secrets { get; set; } = new string[] { };

        /// <summary>关系网 (Key: NPC 名字或 "Player", Value: 信任度)</summary>
        private Dictionary<string, int> _relationships = [];

        public Survivor()
        {
        }

        public Survivor(string name, string role, string bio)
        {
            SurvivorName = name;
            Role = role;
            Bio = bio;
            Hp = 100;
            Hunger = 0;
            Stamina = 100;
            Stress = 0;
            Integrity = 0;
            Trust = 100;
            Secrets = new string[] { };
        }

        /// <summary>获取与指定 NPC 或玩家的信任度</summary>
        public int GetTrust(string npcName)
        {
            // 如果是玩家，直接返回Trust属性
            if (npcName == GameConstants.PLAYER_NAME)
            {
                return Trust;
            }
            
            // 如果有专门的关系记录，使用关系记录
            if (_relationships.TryGetValue(npcName, out var trust))
            {
                return trust;
            }
            
            // 否则返回通用信任值（默认初始值）
            return Trust;
        }

        /// <summary>设置与指定 NPC 或玩家的信任度</summary>
        public void SetTrust(string npcName, int trustScore)
        {
            trustScore = Mathf.Clamp(trustScore, 0, 100);
            
            // 如果是玩家，直接设置Trust属性
            if (npcName == GameConstants.PLAYER_NAME)
            {
                Trust = trustScore;
            }
            else
            {
                // 否则设置关系字典
                _relationships[npcName] = trustScore;
            }
        }

        /// <summary>修改与指定 NPC 或玩家的信任度</summary>
        public void ModifyTrust(string npcName, int delta)
        {
            var current = GetTrust(npcName);
            SetTrust(npcName, current + delta);
        }

        /// <summary>检查是否拥有指定秘密</summary>
        public bool HasSecret(string secretName)
        {
            return System.Array.Exists(Secrets, s => s == secretName);
        }

        /// <summary>添加秘密</summary>
        public void AddSecret(string secretName)
        {
            if (!HasSecret(secretName))
            {
                var newSecrets = new List<string>(Secrets) { secretName };
                Secrets = newSecrets.ToArray();
            }
        }

        /// <summary>移除秘密</summary>
        public void RemoveSecret(string secretName)
        {
            var newSecrets = new List<string>(Secrets);
            newSecrets.RemoveAll(s => s == secretName);
            Secrets = newSecrets.ToArray();
        }

        /// <summary>约束数值范围</summary>
        public void ClampValues()
        {
            Hp = Mathf.Clamp(Hp, 0, 100);
            Hunger = Mathf.Clamp(Hunger, 0, 100);
            Stamina = Mathf.Clamp(Stamina, 0, 100);
            Stress = Mathf.Clamp(Stress, 0, 100);
            Integrity = Mathf.Clamp(Integrity, -100, 100);
            Trust = Mathf.Clamp(Trust, 0, 100);
        }

        /// <summary>创建深拷贝</summary>
        public Survivor Clone()
        {
            var clone = new Survivor
            {
                SurvivorName = SurvivorName,
                Role = Role,
                Bio = Bio,
                Hp = Hp,
                Hunger = Hunger,
                Stamina = Stamina,
                Stress = Stress,
                Integrity = Integrity,
                Trust = Trust,
                Secrets = (string[])Secrets.Clone()
            };

            foreach (var kvp in _relationships)
            {
                clone._relationships[kvp.Key] = kvp.Value;
            }

            return clone;
        }

        public override string ToString()
        {
            return $"{SurvivorName} ({Role}) - HP:{Hp} Hunger:{Hunger} Stress:{Stress}";
        }
    }
}
