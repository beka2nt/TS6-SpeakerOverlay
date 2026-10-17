using System;
using System.Windows;

namespace TS6_SpeakerOverlay.Helpers
{
    public static class LanguageHelper
    {
        public sealed record LanguageOption(string Code, string NativeName);
        public static System.Collections.Generic.IReadOnlyList<LanguageOption> SupportedLanguages { get; } =
        [
            new("zh-CN", "简体中文"), new("en-US", "English"), new("ko-KR", "한국어"),
            new("de-DE", "Deutsch"), new("es-ES", "Español"), new("fr-FR", "Français"), new("ru-RU", "Русский")
        ];
        public static event Action? LanguageChanged;
        public static string CurrentLanguage { get; private set; } = "en-US";

        public static void SetLanguage(string cultureCode)
        {
            if (!System.Linq.Enumerable.Any(SupportedLanguages, language => language.Code == cultureCode))
                cultureCode = "en-US";
            var dict = new ResourceDictionary();
            string source = $"/TS6-SpeakerOverlay;component/Resources/Languages/{cultureCode}.xaml";
            
            try 
            {
                dict.Source = new Uri(source, UriKind.Relative);
            }
            catch
            {
                // 如果找不到文件，默认回滚到英文
                cultureCode = "en-US";
                dict.Source = new Uri("/TS6-SpeakerOverlay;component/Resources/Languages/en-US.xaml", UriKind.Relative);
            }

            // [修复] 显式指定 System.Windows.Application
            var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
            for (int index = dictionaries.Count - 1; index >= 0; index--)
                if (dictionaries[index].Source?.OriginalString.Contains("Resources/Languages/", StringComparison.Ordinal) == true)
                    dictionaries.RemoveAt(index);
            dictionaries.Add(dict);
            CurrentLanguage = cultureCode;
            LanguageChanged?.Invoke();
        }

        // 辅助方法：给非XAML代码（如托盘）获取字符串
        public static string GetString(string key)
        {
            // [修复] 显式指定 System.Windows.Application
            if (System.Windows.Application.Current.Resources.Contains(key))
            {
                return System.Windows.Application.Current.Resources[key] as string ?? key;
            }
            return key;
        }
    }
}
