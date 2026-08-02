using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace GameTranslator
{
    public static class GameTranslationGlossary
    {
        private static readonly Dictionary<string, string> SimplifiedChinese =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Continue", "继续" },
                { "Start Game", "开始游戏" },
                { "New Game", "新游戏" },
                { "Load Game", "读取游戏" },
                { "Save Game", "保存游戏" },
                { "Save", "保存" },
                { "Load", "读取" },
                { "Settings", "设置" },
                { "Options", "选项" },
                { "Confirm", "确认" },
                { "Accept", "接受" },
                { "Cancel", "取消" },
                { "Decline", "拒绝" },
                { "Yes", "是" },
                { "No", "否" },
                { "Back", "返回" },
                { "Close", "关闭" },
                { "Exit", "退出" },
                { "Quit", "退出" },
                { "Exit Game", "退出游戏" },
                { "Pause", "暂停" },
                { "Resume", "继续游戏" },
                { "Retry", "重试" },
                { "Game Over", "游戏结束" },
                { "Inventory", "背包" },
                { "Equipment", "装备" },
                { "Skills", "技能" },
                { "Quests", "任务" },
                { "Map", "地图" },
                { "Level", "等级" },
                { "Health", "生命值" },
                { "Mana", "法力值" },
                { "Attack", "攻击" },
                { "Defense", "防御" },
                { "Critical Hit", "暴击" },
                { "Cooldown", "冷却时间" },

                { "続ける", "继续" },
                { "ゲーム開始", "开始游戏" },
                { "ニューゲーム", "新游戏" },
                { "ロード", "读取" },
                { "セーブ", "保存" },
                { "設定", "设置" },
                { "オプション", "选项" },
                { "確認", "确认" },
                { "キャンセル", "取消" },
                { "はい", "是" },
                { "いいえ", "否" },
                { "戻る", "返回" },
                { "終了", "退出" },
                { "一時停止", "暂停" },
                { "再開", "继续游戏" },
                { "リトライ", "重试" },
                { "ゲームオーバー", "游戏结束" },
                { "持ち物", "背包" },
                { "装備", "装备" },
                { "スキル", "技能" },
                { "クエスト", "任务" },
                { "マップ", "地图" },

                { "계속", "继续" },
                { "게임 시작", "开始游戏" },
                { "새 게임", "新游戏" },
                { "불러오기", "读取" },
                { "저장", "保存" },
                { "설정", "设置" },
                { "옵션", "选项" },
                { "확인", "确认" },
                { "취소", "取消" },
                { "예", "是" },
                { "아니요", "否" },
                { "뒤로", "返回" },
                { "종료", "退出" },
                { "일시 정지", "暂停" },
                { "재개", "继续游戏" },
                { "다시 시도", "重试" },
                { "게임 오버", "游戏结束" },
                { "인벤토리", "背包" },
                { "장비", "装备" },
                { "스킬", "技能" },
                { "퀘스트", "任务" },
                { "지도", "地图" }
            };

        private static readonly string[] ProtectedAcronyms =
        {
            "DPS", "HP", "MP", "NPC", "FPS", "PVP", "PVE",
            "DLC", "EXP", "XP", "UI", "AOE", "DOT", "HOT"
        };

        public static bool TryTranslateText(
            string text,
            string targetLanguage,
            out string translated)
        {
            translated = "";
            if (!string.Equals(
                TranslationLanguages.Normalize(targetLanguage),
                "zh-Hans",
                StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var cleaned = TranslationText.CleanOcrText(text);
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                return false;
            }

            var lines = cleaned.Split(new[] { '\n' });
            var results = new List<string>();
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                string value;
                if (!SimplifiedChinese.TryGetValue(line, out value))
                {
                    return false;
                }
                results.Add(value);
            }

            translated = string.Join("\n", results.ToArray());
            return true;
        }

        public static string EnhanceResult(
            string sourceText,
            string translatedText,
            string targetLanguage)
        {
            if (string.IsNullOrWhiteSpace(translatedText))
            {
                return translatedText;
            }

            var result = translatedText;
            foreach (var acronym in ProtectedAcronyms)
            {
                if (!ContainsToken(sourceText, acronym))
                {
                    continue;
                }
                result = ReplaceToken(result, acronym, acronym);
            }

            if (ContainsToken(sourceText, "DPS"))
            {
                result = ReplaceToken(result, "D.P.S.", "DPS");
                result = ReplaceToken(result, "085", "DPS");
            }
            return result;
        }

        private static bool ContainsToken(string text, string token)
        {
            return Regex.IsMatch(
                text ?? "",
                "(?<![A-Za-z0-9])" + Regex.Escape(token)
                    + "(?![A-Za-z0-9])",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        private static string ReplaceToken(
            string text,
            string token,
            string replacement)
        {
            return Regex.Replace(
                text,
                "(?<![A-Za-z0-9])" + Regex.Escape(token)
                    + "(?![A-Za-z0-9])",
                replacement,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
    }
}
