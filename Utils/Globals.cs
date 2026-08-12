using System;
using System.Drawing;
using System.IO;

using AppiumBuilder.Core;

namespace AppiumBuilder.Utils
{
    /// <summary>
    /// Appium Builder Reborn 전역 디자인 시스템.
    /// Soft Blue Office: 밝은 블루-화이트 표면, 선명한 정보 위계, 절제된 블루 포인트를 사용한다.
    /// </summary>
    public static class Globals
    {
        public static string LogFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "ADB_Logs");

        public static string ScenarioFolder = Path.Combine(LogFolder, "Scenarios");

        // ===== Soft Blue Office surfaces =====
        public static readonly Color Bg = Color.FromArgb(245, 247, 252);            // #F5F7FC
        public static readonly Color Sidebar = Color.FromArgb(248, 250, 252);       // #F8FAFC
        public static readonly Color Surface = Color.FromArgb(255, 255, 255);       // #FFFFFF
        public static readonly Color SurfaceAlt = Color.FromArgb(248, 250, 252);    // #F8FAFC
        public static readonly Color SurfaceRaised = Color.FromArgb(241, 245, 249); // #F1F5F9
        public static readonly Color Border = Color.FromArgb(226, 232, 240);        // #E2E8F0
        public static readonly Color BorderStrong = Color.FromArgb(203, 213, 225);  // #CBD5E1

        // ===== Brand / Accent =====
        public static readonly Color Accent = Color.FromArgb(37, 99, 235);          // #2563EB
        public static readonly Color AccentHover = Color.FromArgb(29, 78, 216);     // #1D4ED8
        public static readonly Color AccentPressed = Color.FromArgb(30, 64, 175);   // #1E40AF
        public static readonly Color AccentSoft = Color.FromArgb(234, 242, 255);    // #EAF2FF
        public static readonly Color AccentText = Color.FromArgb(37, 99, 235);      // #2563EB

        // ===== Text =====
        public static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);      // #0F172A
        public static readonly Color TextSecondary = Color.FromArgb(51, 65, 85);   // #334155
        public static readonly Color TextMuted = Color.FromArgb(100, 116, 139);    // #64748B
        public static readonly Color TextFaint = Color.FromArgb(148, 163, 184);     // #94A3B8

        // ===== Semantic colors =====
        public static readonly Color Success = Color.FromArgb(22, 163, 74);         // #16A34A
        public static readonly Color Warning = Color.FromArgb(245, 158, 11);        // #F59E0B
        public static readonly Color Danger = Color.FromArgb(239, 68, 68);          // #EF4444
        public static readonly Color Info = Color.FromArgb(37, 99, 235);            // #2563EB
        public static readonly Color SuccessSoft = Color.FromArgb(236, 253, 243);   // #ECFDF3
        public static readonly Color WarningSoft = Color.FromArgb(255, 247, 230);   // #FFF7E6
        public static readonly Color DangerSoft = Color.FromArgb(255, 241, 242);    // #FFF1F2
        public static readonly Color InfoSoft = Color.FromArgb(239, 246, 255);      // #EFF6FF

        // ===== Sidebar aliases =====
        public static readonly Color SidebarBorder = Border;
        public static readonly Color SidebarActive = AccentSoft;
        public static readonly Color SidebarTextActive = Accent;
        public static readonly Color SidebarTextMuted = TextSecondary;

        // ===== Console =====
        // 라이트 모드에서도 로그 레벨 색상이 잘 보이도록 콘솔 자체를 밝게 유지한다.
        public static readonly Color ConsoleBg = Color.FromArgb(255, 255, 255);     // #FFFFFF
        public static readonly Color ConsoleLine = Color.FromArgb(226, 232, 240);   // #E2E8F0

        // ===== Typography =====
        public static Font FontPageTitle => new Font("Malgun Gothic", 18F, FontStyle.Bold);
        public static Font FontTitle => new Font("Malgun Gothic", 16F, FontStyle.Bold);
        public static Font FontHeading => new Font("Malgun Gothic", 12F, FontStyle.Bold);
        public static Font FontSub => new Font("Malgun Gothic", 9.5F, FontStyle.Bold);
        public static Font FontBody => new Font("Malgun Gothic", 9.5F, FontStyle.Regular);
        public static Font FontMuted => new Font("Malgun Gothic", 8.5F, FontStyle.Regular);
        public static Font FontMono => new Font("Consolas", 9.5F, FontStyle.Regular);
        public static Font FontStat => new Font("Malgun Gothic", 18F, FontStyle.Bold);

        // ===== Geometry =====
        public const int Radius = 10;
        public const int RadiusSm = 8;
        public const int RadiusXs = 6;
        public const int SidebarWidth = 200;
        public const int ContentPadding = 20;
        public const int FooterHeight = 30;
        public const int MenuHeight = 42;
        public const int ControlHeight = 40;
        public const int ButtonHeight = 40;
        public const int PrimaryButtonHeight = 44;
        public const int PageHeaderHeight = 88;
        public const int SectionGap = 12;

        public static void InitFolders()
        {
            Directory.CreateDirectory(LogFolder);
            Directory.CreateDirectory(ScenarioFolder);
            LogRetentionSettings retention = LogRetentionSettings.Load(LogFolder);
            LogRetention.Cleanup(LogFolder, retention.retentionDays, retention.MaxBytes);
        }
    }
}
