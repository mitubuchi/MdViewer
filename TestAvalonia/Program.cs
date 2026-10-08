/*
    File Name : Program.cs
    Programmer: Keiji Mitsubuchi

    2026.10.08  created
                MdAvalonia / SvgAvalonia / ImgAvalonia の確認台（TestWpf の Avalonia 版）。
                画面は XAML を使わずコードで組む（C# 7.3 のまま、生成コードに頼らないため）。

    ■ Usage:
        dotnet run --project TestAvalonia                 サンプルの Markdown を表示
        dotnet run --project TestAvalonia -- report.md    ファイルを開く（.md / .svg / 画像・動画）
 */
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace TestAvalonia
{
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .StartWithClassicDesktopLifetime(args);
        }
    }

    internal class App : Application
    {
        public override void Initialize()
        {
            // 確認台なので、ライブラリの既定の見た目（白地）に合わせて明るいテーマに固定する
            RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
            Styles.Add(new FluentTheme());
        }

        public override void OnFrameworkInitializationCompleted()
        {
            var desktop = ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            if (desktop != null)
            {
                var window = new MainWindow();
                desktop.MainWindow = window;
                if (desktop.Args != null && desktop.Args.Length > 0) window.Open(desktop.Args[0]);
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}
