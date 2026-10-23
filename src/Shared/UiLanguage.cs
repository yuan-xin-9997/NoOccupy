using System;
using System.Globalization;
using System.Threading;
using Microsoft.Win32;

namespace NoOccupy.Localization
{
    /// <summary>界面语言。数值同时用作语言下拉框的索引。</summary>
    internal enum UiLanguage
    {
        Chinese = 0,
        English = 1,
        Japanese = 2,
        Korean = 3
    }

    /// <summary>
    /// 主程序与安装器共用的语言设置。保存在当前用户的注册表中，
    /// 因此安装器选择的语言会被安装后的主程序直接沿用。
    /// </summary>
    internal static class LanguageSettings
    {
        internal const string RegistryPath = @"Software\NoOccupy";
        internal const string RegistryValueName = "Language";

        internal static readonly string[] NativeNames =
        {
            "中文",
            "English",
            "日本語",
            "한국어"
        };

        internal static readonly string[] MenuKeyPaths =
        {
            @"Software\Classes\*\shell\NoOccupy",
            @"Software\Classes\Directory\shell\NoOccupy",
            @"Software\Classes\Directory\Background\shell\NoOccupy"
        };

        internal const string UninstallKeyPath =
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\NoOccupy";

        private static readonly object Sync = new object();
        private static bool _loaded;
        private static UiLanguage _current;

        internal static UiLanguage Current
        {
            get
            {
                EnsureLoaded();
                return _current;
            }
        }

        internal static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            lock (Sync)
            {
                if (_loaded)
                {
                    return;
                }

                UiLanguage stored;
                _current = TryRead(out stored) ? stored : DetectSystemLanguage();
                ApplyCulture(_current);
                _loaded = true;
            }
        }

        internal static bool TryParse(string value, out UiLanguage language)
        {
            language = UiLanguage.English;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string text = value.Trim();
            int separator = text.IndexOfAny(new[] { '-', '_' });
            if (separator > 0)
            {
                text = text.Substring(0, separator);
            }

            switch (text.ToLowerInvariant())
            {
                case "zh":
                    language = UiLanguage.Chinese;
                    return true;
                case "en":
                    language = UiLanguage.English;
                    return true;
                case "ja":
                    language = UiLanguage.Japanese;
                    return true;
                case "ko":
                    language = UiLanguage.Korean;
                    return true;
                default:
                    return false;
            }
        }

        internal static UiLanguage DetectSystemLanguage()
        {
            try
            {
                string name = CultureInfo.CurrentUICulture == null
                    ? string.Empty
                    : CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                UiLanguage language;
                if (TryParse(name, out language))
                {
                    return language;
                }
            }
            catch
            {
                // 无法读取系统语言时回退到英文。
            }

            return UiLanguage.English;
        }

        internal static void Save(UiLanguage language)
        {
            _current = language;
            _loaded = true;
            ApplyCulture(language);

            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    if (key != null)
                    {
                        key.SetValue(RegistryValueName, GetCode(language), RegistryValueKind.String);
                    }
                }
            }
            catch
            {
                // 语言设置写入失败不应影响程序使用。
            }
        }

        internal static string GetCode(UiLanguage language)
        {
            switch (language)
            {
                case UiLanguage.Chinese: return "zh-CN";
                case UiLanguage.Japanese: return "ja-JP";
                case UiLanguage.Korean: return "ko-KR";
                default: return "en-US";
            }
        }

        internal static string GetCultureName(UiLanguage language)
        {
            return GetCode(language);
        }

        /// <summary>
        /// 把右键菜单和卸载列表显示名更新为当前语言；未安装时自动跳过。
        /// </summary>
        internal static void UpdateContextMenuLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            foreach (string path in MenuKeyPaths)
            {
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(path, true))
                    {
                        if (key != null)
                        {
                            key.SetValue(null, label, RegistryValueKind.String);
                        }
                    }
                }
                catch
                {
                    // 没有管理员权限或菜单项不存在时忽略。
                }
            }

            UpdateUninstallDisplayName(label);
        }

        private static void UpdateUninstallDisplayName(string label)
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(UninstallKeyPath, true))
                {
                    if (key == null)
                    {
                        return;
                    }

                    string displayName = label + " (NoOccupy)";
                    string current = key.GetValue("DisplayName") as string;
                    if (!string.Equals(current, displayName, StringComparison.Ordinal))
                    {
                        key.SetValue("DisplayName", displayName, RegistryValueKind.String);
                    }
                }
            }
            catch
            {
                // 没有管理员权限或卸载项不存在时忽略。
            }
        }

        private static bool TryRead(out UiLanguage language)
        {
            language = UiLanguage.English;

            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                {
                    if (key != null)
                    {
                        return TryParse(key.GetValue(RegistryValueName) as string, out language);
                    }
                }
            }
            catch
            {
                // 注册表不可用时按系统语言处理。
            }

            return false;
        }

        private static void ApplyCulture(UiLanguage language)
        {
            try
            {
                CultureInfo culture = CultureInfo.GetCultureInfo(GetCultureName(language));
                Thread.CurrentThread.CurrentUICulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;
            }
            catch
            {
                // 缺少对应文化信息时保留系统设置。
            }
        }
    }
}
