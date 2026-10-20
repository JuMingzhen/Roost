using System;
using System.Collections.Generic;

namespace Roost.Core
{
    // 一个全局快捷键：修饰键（与 RegisterHotKey 的 MOD_* 取值相同）+ 一个按键的虚拟键码。
    public sealed class HotKey
    {
        public HotKey(uint modifiers, int key)
        {
            Modifiers = modifiers;
            Key = key;
        }

        public uint Modifiers { get; private set; }
        public int Key { get; private set; }

        public override bool Equals(object obj)
        {
            HotKey other = obj as HotKey;
            return other != null && other.Modifiers == Modifiers && other.Key == Key;
        }

        public override int GetHashCode()
        {
            return (int)Modifiers * 397 ^ Key;
        }
    }

    // 快捷键在设置里存成文字（"Ctrl+Alt+H"），界面上显示为 "Ctrl+Alt+H" / "Ctrl+Alt+空格"（PRD 6.2、12）。
    // 只支持字母、数字、F1–F12 和空格，且必须带 Ctrl、Alt 或 Win：只按 Shift 加字母会打断正常打字。
    public static class HotKeyRules
    {
        public const uint Alt = 0x0001;
        public const uint Control = 0x0002;
        public const uint Shift = 0x0004;
        public const uint Win = 0x0008;

        public const string DefaultToggle = "Ctrl+Alt+H";
        public const string DefaultTalk = "Ctrl+Alt+Space";

        private const int SpaceKey = 0x20;
        private const int F1Key = 0x70;

        public static bool TryParse(string text, out HotKey hotKey)
        {
            hotKey = null;
            if (string.IsNullOrWhiteSpace(text)) return false;
            uint modifiers = 0;
            int key = 0;
            foreach (string raw in text.Split('+'))
            {
                string part = raw.Trim().ToUpperInvariant();
                uint modifier = ModifierFromName(part);
                if (modifier != 0)
                {
                    modifiers |= modifier;
                    continue;
                }
                int parsed = KeyFromName(part);
                if (parsed == 0 || key != 0) return false;
                key = parsed;
            }
            HotKey candidate = new HotKey(modifiers, key);
            if (Problem(candidate, null) != null) return false;
            hotKey = candidate;
            return true;
        }

        // 不能用作快捷键时返回原因，可以用时返回 null。other 是另一个快捷键，两者不能相同。
        public static string Problem(HotKey hotKey, HotKey other)
        {
            if (KeyName(hotKey.Key) == null) return "只能用字母、数字、F1–F12 或空格。";
            if ((hotKey.Modifiers & (Control | Alt | Win)) == 0) return "要同时按住 Ctrl、Alt 或 Win 中的至少一个。";
            if (hotKey.Equals(other)) return "和另一个快捷键重复了。";
            return null;
        }

        // 保存到设置里的写法，修饰键按固定顺序排列。
        public static string Format(HotKey hotKey)
        {
            List<string> parts = ModifierNames(hotKey.Modifiers);
            parts.Add(KeyName(hotKey.Key));
            return string.Join("+", parts.ToArray());
        }

        // 界面上的写法：空格键写成「空格」。
        public static string Display(HotKey hotKey)
        {
            List<string> parts = ModifierNames(hotKey.Modifiers);
            parts.Add(hotKey.Key == SpaceKey ? "空格" : KeyName(hotKey.Key));
            return string.Join("+", parts.ToArray());
        }

        // 设置里的两个快捷键：不合法的换回默认值；两者相同时都换回默认值。
        public static void Normalize(RoostSettings settings)
        {
            HotKey toggle, talk;
            if (!TryParse(settings.ToggleHotKey, out toggle)) TryParse(DefaultToggle, out toggle);
            if (!TryParse(settings.TalkHotKey, out talk)) TryParse(DefaultTalk, out talk);
            if (toggle.Equals(talk))
            {
                TryParse(DefaultToggle, out toggle);
                TryParse(DefaultTalk, out talk);
            }
            settings.ToggleHotKey = Format(toggle);
            settings.TalkHotKey = Format(talk);
        }

        private static List<string> ModifierNames(uint modifiers)
        {
            List<string> parts = new List<string>();
            if ((modifiers & Control) != 0) parts.Add("Ctrl");
            if ((modifiers & Alt) != 0) parts.Add("Alt");
            if ((modifiers & Shift) != 0) parts.Add("Shift");
            if ((modifiers & Win) != 0) parts.Add("Win");
            return parts;
        }

        private static uint ModifierFromName(string name)
        {
            switch (name)
            {
                case "CTRL":
                case "CONTROL":
                    return Control;
                case "ALT":
                    return Alt;
                case "SHIFT":
                    return Shift;
                case "WIN":
                    return Win;
                default:
                    return 0;
            }
        }

        private static int KeyFromName(string name)
        {
            if (name == "SPACE" || name == "空格") return SpaceKey;
            if (name.Length == 1 && ((name[0] >= 'A' && name[0] <= 'Z') || (name[0] >= '0' && name[0] <= '9'))) return name[0];
            int number;
            if (name.Length >= 2 && name[0] == 'F' && int.TryParse(name.Substring(1), out number) && number >= 1 && number <= 12 && name.Substring(1) == number.ToString())
                return F1Key + number - 1;
            return 0;
        }

        // 支持的按键返回名字，不支持的返回 null。
        private static string KeyName(int key)
        {
            if (key == SpaceKey) return "Space";
            if ((key >= 'A' && key <= 'Z') || (key >= '0' && key <= '9')) return ((char)key).ToString();
            if (key >= F1Key && key < F1Key + 12) return "F" + (key - F1Key + 1);
            return null;
        }
    }
}
