namespace MasqueradeArk.Utilities
{
    /// <summary>
    /// 游戏常量定义
    /// </summary>
    public static class GameConstants
    {
        /// <summary>NPC 状态限制</summary>
        public const int MAX_HP = 100;
        public const int MIN_HP = 0;

        public const int MAX_HUNGER = 100;
        public const int MIN_HUNGER = 0;

        public const int MAX_STAMINA = 100;
        public const int MIN_STAMINA = 0;

        public const int MAX_STRESS = 100;
        public const int MIN_STRESS = 0;

        public const int MAX_INTEGRITY = 100;
        public const int MIN_INTEGRITY = -100;

        public const int MAX_SUSPICION = 100;
        public const int MIN_SUSPICION = 0;

        /// <summary>游戏规则参数</summary>
        public const int SUPPLIES_PER_SURVIVOR = 1;
        public const int STARVATION_HUNGER_THRESHOLD = 80;
        public const int STARVATION_HP_LOSS = 10;
        public const int STARVATION_STRESS_GAIN = 15;

        public const int MENTAL_BREAKDOWN_STRESS_THRESHOLD = 80;
        public const float MENTAL_BREAKDOWN_PROBABILITY = 0.3f;
        public const int MENTAL_BREAKDOWN_STRESS_REDUCTION = 30;

        public const int INFECTION_HP_LOSS_PER_DAY = 5;
        public const float INFECTION_DISCOVERY_PROBABILITY = 0.2f;
        public const int INFECTION_SUSPICION_GAIN_MIN = 5;
        public const int INFECTION_SUSPICION_GAIN_MAX = 15;

        public const float THEFT_BASE_PROBABILITY_MULTIPLIER = 0.5f;
        public const float THEFT_INTEGRITY_BONUS = 0.3f;

        public const int NO_SUPPLIES_HUNGER_GAIN = 20;
        public const int NO_SUPPLIES_STRESS_GAIN = 10;

        /// <summary>游戏流程参数</summary>
        public const int VICTORY_DAY_THRESHOLD = 30;
        public const int INITIAL_SUPPLIES = 50;
        public const int INITIAL_DEFENSE = 50;
        public const int INITIAL_DAY = 1;

        /// <summary>NPC 初始状态</summary>
        public const int INITIAL_NPC_HP = 100;
        public const int INITIAL_NPC_HUNGER = 0;
        public const int INITIAL_NPC_STAMINA = 100;
        public const int INITIAL_NPC_STRESS = 0;
        public const int INITIAL_NPC_INTEGRITY = 0;
        public const int INITIAL_NPC_SUSPICION = 0;

        /// <summary>秘密名称</summary>
        public const string SECRET_INFECTED = "Infected";
        public const string SECRET_THIEF = "Thief";
        public const string SECRET_TRAITOR = "Traitor";
        public const string SECRET_COWARD = "Coward";

        /// <summary>NPC 角色</summary>
        public const string ROLE_DOCTOR = "Doctor";
        public const string ROLE_MERCENARY = "Mercenary";
        public const string ROLE_ENGINEER = "Engineer";
        public const string ROLE_FARMER = "Farmer";
        public const string ROLE_PLAYER = "Player";
    }
}
