using System;
using System.Windows;

namespace TS6_SpeakerOverlay.Helpers
{
    public static class LanguageHelper
    {
        public static void SetLanguage(string cultureCode)
        {
            var dict = new ResourceDictionary();
            string source = $"Resources/Languages/{cultureCode}.xaml";
            
            try 
            {
                dict.Source = new Uri(source, UriKind.Relative);
            }
            catch
            {
                // 如果找不到文件，默认回滚到英文
                dict.Source = new Uri("Resources/Languages/en-US.xaml", UriKind.Relative);
            }

            // [修复] 显式指定 System.Windows.Application
            System.Windows.Application.Current.Resources.MergedDictionaries.Clear();
            System.Windows.Application.Current.Resources.MergedDictionaries.Add(dict);
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