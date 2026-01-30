using Godot;

namespace MasqueradeArk.Utilities
{
    /// <summary>
    /// 游戏常量和配置参数
    /// </summary>
    public static class GameConstants
    {
        // ===== 资源消耗 =====
        public const int DAILY_SUPPLIES_PER_SURVIVOR = 1;
        public const int STARVATION_HUNGER_INCREASE = 20;
        public const int STARVATION_STRESS_INCREASE = 10;
        
        // ===== 状态恶化 =====
        public const int HUNGER_CRITICAL_THRESHOLD = 80;
        public const int STRESS_BREAKDOWN_THRESHOLD = 80;
        public const int HUNGER_DAMAGE = 10;
        public const int HUNGER_STRESS_PENALTY = 15;
        public const float BREAKDOWN_CHANCE = 0.3f;
        public const int BREAKDOWN_STRESS_RELIEF = 30;
        
        // ===== 感染机制 =====
        public const int INFECTION_DAILY_DAMAGE = 5;
        public const float INFECTION_DETECTION_CHANCE = 0.2f;
        public const int MIN_SUSPICION_INCREASE = 5;
        public const int MAX_SUSPICION_INCREASE = 20;
        
        // ===== 偷窃机制 =====
        public const float HUNGER_THEFT_MULTIPLIER = 0.5f;
        public const float LOW_INTEGRITY_THEFT_BONUS = 0.3f;
        public const int THEFT_AMOUNT = 1;
        
        // ===== 初始值 =====
        public const int INITIAL_SUPPLIES = 50;
        public const int INITIAL_DEFENSE = 50;
        public const int INITIAL_HP = 100;
        public const int INITIAL_STAMINA = 100;
        public const int INITIAL_HUNGER = 0;
        public const int INITIAL_STRESS = 0;
        public const int INITIAL_INTEGRITY = 0;
        public const int INITIAL_SUSPICION = 0;
        public const int INITIAL_TRUST = 50;
        
        // ===== 游戏胜利条件 =====
        public const int VICTORY_DAYS = 30;
        
        // ===== 秘密分配概率 =====
        public const float INFECTED_SPAWN_CHANCE = 0.25f;      // 25% 概率被感染
        public const float THIEF_SPAWN_CHANCE = 0.15f;         // 15% 概率是小偷
        
        // ===== 属性范围 =====
        public const int MIN_ATTRIBUTE_VALUE = 0;
        public const int MAX_ATTRIBUTE_VALUE = 100;
        public const int MIN_INTEGRITY = -100;
        public const int MAX_INTEGRITY = 100;
        public const int MIN_TRUST = -100;
        public const int MAX_TRUST = 100;
        
        // ===== UI 配置 =====
        public const int MAX_LOG_ENTRIES = 1000;
        public const string PLAYER_NAME = "Player";
        
        // ===== 秘密类型 =====
        public static readonly string[] SECRET_TYPES = {
            "Infected",
            "Thief",
            "Paranoid",
            "Coward"
        };
        
        // ===== 幸存者角色 =====
        public static readonly string[] SURVIVOR_ROLES = {
            "Doctor",
            "Mercenary", 
            "Engineer",
            "Farmer",
            "Cook",
            "Mechanic",
            "Scout",
            "Teacher"
        };
        
        // ===== 调试选项 =====
        public const bool DEBUG_SHOW_SECRETS = true;
        public const bool DEBUG_VERBOSE_LOGGING = false;
    }
}
