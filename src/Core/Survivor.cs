using Godot;
using System;
using System.Collections.Generic;

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
        public int Suspicion { get; set; } = 0; // 0–100

        /// <summary>秘密集合 (例如: "Infected", "Thief")</summary>
        [Export]
        public string[] Secrets { get; set; } = [];

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
            Suspicion = 0;
            Secrets = [];
        }

        /// <summary>获取与指定 NPC 的信任度</summary>
        public int GetTrust(string npcName)
        {
            return _relationships.TryGetValue(npcName, out var trust) ? trust : 0;
        }

        /// <summary>设置与指定 NPC 的信任度</summary>
        public void SetTrust(string npcName, int trustScore)
        {
            _relationships[npcName] = Mathf.Clamp(trustScore, -100, 100);
        }

        /// <summary>增加信任度</summary>
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
            Suspicion = Mathf.Clamp(Suspicion, 0, 100);
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
                Suspicion = Suspicion,
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
