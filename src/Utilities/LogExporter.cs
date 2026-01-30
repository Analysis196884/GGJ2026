using Godot;
using System;
using System.Text;
using MasqueradeArk.Core;

namespace MasqueradeArk.Utilities
{
    [GlobalClass]
    public partial class LogExporter : Node
    {
        /// <summary>
        /// 导出事件日志为文本文件
        /// </summary>
        public bool ExportLogsToFile(GameState state, string filePath = "")
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    filePath = $"user://game_log_{timestamp}.txt";
                }

                var file = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Write);
                if (file == null)
                {
                    GD.PrintErr($"无法创建日志文件: {filePath}");
                    return false;
                }

                // 写入文件头
                var header = GenerateLogHeader(state);
                file.StoreString(header);

                // 写入事件日志
                var logs = state.GetEventLog();
                foreach (var log in logs)
                {
                    file.StoreString(log + "\n");
                }

                // 写入游戏统计
                var statistics = GenerateGameStatistics(state);
                file.StoreString(statistics);

                file.Close();
                GD.Print($"事件日志已导出到: {filePath}");
                return true;
            }
            catch (Exception e)
            {
                GD.PrintErr($"导出日志时发生错误: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 导出事件日志为JSON格式
        /// </summary>
        public bool ExportLogsToJson(GameState state, string filePath = "")
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    filePath = $"user://game_log_{timestamp}.json";
                }

                var jsonData = new Godot.Collections.Dictionary
                {
                    ["gameInfo"] = new Godot.Collections.Dictionary
                    {
                        ["day"] = state.Day,
                        ["supplies"] = state.Supplies,
                        ["defense"] = state.Defense,
                        ["survivorCount"] = state.GetSurvivorCount(),
                        ["aliveSurvivorCount"] = state.GetAliveSurvivorCount(),
                        ["exportTime"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    },
                    ["survivors"] = GenerateSurvivorData(state),
                    ["locations"] = GenerateLocationData(state),
                    ["eventLog"] = new Godot.Collections.Array()
                };

                // 手动添加事件日志
                var eventLogArray = jsonData["eventLog"].AsGodotArray();
                foreach (var log in state.GetEventLog())
                {
                    eventLogArray.Add(log);
                }

                var jsonString = Json.Stringify(jsonData);
                
                var file = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Write);
                if (file == null)
                {
                    GD.PrintErr($"无法创建JSON文件: {filePath}");
                    return false;
                }

                file.StoreString(jsonString);
                file.Close();
                
                GD.Print($"事件日志(JSON)已导出到: {filePath}");
                return true;
            }
            catch (Exception e)
            {
                GD.PrintErr($"导出JSON日志时发生错误: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 导出游戏存档摘要
        /// </summary>
        public bool ExportGameSummary(GameState state, string filePath = "")
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    filePath = $"user://game_summary_{timestamp}.md";
                }

                var file = Godot.FileAccess.Open(filePath, Godot.FileAccess.ModeFlags.Write);
                if (file == null)
                {
                    GD.PrintErr($"无法创建摘要文件: {filePath}");
                    return false;
                }

                var summary = GenerateMarkdownSummary(state);
                file.StoreString(summary);
                file.Close();
                
                GD.Print($"游戏摘要已导出到: {filePath}");
                return true;
            }
            catch (Exception e)
            {
                GD.PrintErr($"导出游戏摘要时发生错误: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 生成日志文件头
        /// </summary>
        private string GenerateLogHeader(GameState state)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=".PadLeft(50, '='));
            sb.AppendLine("        暴雪山庄避难所 - 游戏日志");
            sb.AppendLine("=".PadLeft(50, '='));
            sb.AppendLine($"导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"游戏天数: {state.Day}");
            sb.AppendLine($"剩余物资: {state.Supplies}");
            sb.AppendLine($"防御值: {state.Defense}");
            sb.AppendLine($"幸存者总数: {state.GetSurvivorCount()}");
            sb.AppendLine($"存活幸存者: {state.GetAliveSurvivorCount()}");
            sb.AppendLine("-".PadLeft(50, '-'));
            sb.AppendLine();
            return sb.ToString();
        }

        /// <summary>
        /// 生成游戏统计信息
        /// </summary>
        private string GenerateGameStatistics(GameState state)
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("-".PadLeft(50, '-'));
            sb.AppendLine("游戏统计");
            sb.AppendLine("-".PadLeft(50, '-'));
            
            // 幸存者统计
            foreach (var survivor in state.Survivors)
            {
                sb.AppendLine($"【{survivor.SurvivorName}】({survivor.Role})");
                sb.AppendLine($"  生命值: {survivor.Hp}/100");
                sb.AppendLine($"  饥饿度: {survivor.Hunger}/100");
                sb.AppendLine($"  压力值: {survivor.Stress}/100");
                sb.AppendLine($"  状态: {(survivor.Hp > 0 ? "存活" : "已死亡")}");
                sb.AppendLine();
            }

            // 场所统计
            sb.AppendLine("场所状况:");
            foreach (var location in state.Locations)
            {
                sb.AppendLine($"  {location.Name}: {(location.CanUse() ? "可用" : "不可用")} (损坏度: {location.DamageLevel}%)");
            }

            return sb.ToString();
        }

        /// <summary>
        /// 生成幸存者数据
        /// </summary>
        private Godot.Collections.Array GenerateSurvivorData(GameState state)
        {
            var survivorsArray = new Godot.Collections.Array();
            
            foreach (var survivor in state.Survivors)
            {
                var survivorData = new Godot.Collections.Dictionary
                {
                    ["name"] = survivor.SurvivorName,
                    ["role"] = survivor.Role,
                    ["description"] = survivor.Bio,
                    ["hp"] = survivor.Hp,
                    ["hunger"] = survivor.Hunger,
                    ["stress"] = survivor.Stress,
                    ["stamina"] = survivor.Stamina,
                    ["integrity"] = survivor.Integrity,
                    ["suspicion"] = survivor.Suspicion,
                    ["isAlive"] = survivor.Hp > 0
                };
                survivorsArray.Add(survivorData);
            }
            
            return survivorsArray;
        }

        /// <summary>
        /// 生成场所数据
        /// </summary>
        private Godot.Collections.Array GenerateLocationData(GameState state)
        {
            var locationsArray = new Godot.Collections.Array();
            
            foreach (var location in state.Locations)
            {
                var locationData = new Godot.Collections.Dictionary
                {
                    ["name"] = location.Name,
                    ["type"] = location.Type.ToString(),
                    ["description"] = location.Description,
                    ["capacity"] = location.Capacity,
                    ["isAvailable"] = location.IsAvailable,
                    ["damageLevel"] = location.DamageLevel,
                    ["canUse"] = location.CanUse(),
                    ["efficiency"] = location.GetEfficiency()
                };
                locationsArray.Add(locationData);
            }
            
            return locationsArray;
        }

        /// <summary>
        /// 生成Markdown格式的游戏摘要
        /// </summary>
        private string GenerateMarkdownSummary(GameState state)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("# 暴雪山庄避难所 - 游戏摘要");
            sb.AppendLine();
            sb.AppendLine($"**导出时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"**游戏天数**: {state.Day}");
            sb.AppendLine($"**游戏状态**: {(state.Day >= GameConstants.VICTORY_DAYS && state.GetAliveSurvivorCount() > 0 ? "胜利" : "进行中")}");
            sb.AppendLine();
            
            sb.AppendLine("## 资源状况");
            sb.AppendLine($"- 剩余物资: {state.Supplies}");
            sb.AppendLine($"- 防御值: {state.Defense}");
            sb.AppendLine();
            
            sb.AppendLine("## 幸存者状况");
            sb.AppendLine($"- 总数: {state.GetSurvivorCount()}");
            sb.AppendLine($"- 存活: {state.GetAliveSurvivorCount()}");
            sb.AppendLine();
            
            foreach (var survivor in state.Survivors)
            {
                sb.AppendLine($"### {survivor.SurvivorName} ({survivor.Role})");
                sb.AppendLine($"- **状态**: {(survivor.Hp > 0 ? "存活" : "已死亡")}");
                if (survivor.Hp > 0)
                {
                    sb.AppendLine($"- **生命值**: {survivor.Hp}/100");
                    sb.AppendLine($"- **饥饿度**: {survivor.Hunger}/100");
                    sb.AppendLine($"- **压力值**: {survivor.Stress}/100");
                }
                sb.AppendLine();
            }
            
            sb.AppendLine("## 场所状况");
            foreach (var location in state.Locations)
            {
                sb.AppendLine($"### {location.Name}");
                sb.AppendLine($"- **状态**: {(location.CanUse() ? "可用" : "不可用")}");
                sb.AppendLine($"- **损坏度**: {location.DamageLevel}%");
                sb.AppendLine($"- **效率**: {location.GetEfficiency():P}");
                sb.AppendLine();
            }
            
            sb.AppendLine("## 最近事件");
            var recentLogs = state.GetRecentLogs(10);
            foreach (var log in recentLogs)
            {
                sb.AppendLine($"- {log}");
            }
            
            return sb.ToString();
        }

        /// <summary>
        /// 获取用户目录路径
        /// </summary>
        public string GetUserDataPath()
        {
            return OS.GetUserDataDir();
        }

        /// <summary>
        /// 检查文件是否存在
        /// </summary>
        public bool FileExists(string filePath)
        {
            return Godot.FileAccess.FileExists(filePath);
        }
    }
}