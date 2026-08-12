using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using AppiumBuilder.Utils;

namespace AppiumBuilder.UI
{
    public sealed class ScenarioVersionManagerForm : Form
    {
        private sealed class VersionTarget
        {
            public string Display { get; init; } = string.Empty;
            public string TargetPath { get; init; } = string.Empty;
            public string VersionsDir { get; init; } = string.Empty;
            public override string ToString() => Display;
        }

        private readonly ComboBox scenarios = new();
        private readonly ListBox versions = new();
        private VersionTarget? current;

        public ScenarioVersionManagerForm(string testSetPath, string csvPath, string? preferredScenario = null)
        {
            Text = "시나리오 버전 관리";
            Size = new Size(800, 560);
            MinimumSize = new Size(720, 500);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Globals.Bg;
            ForeColor = Globals.TextPrimary;
            Font = Globals.FontBody;
            AutoScaleMode = AutoScaleMode.Dpi;

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(18), BackColor = Globals.Bg };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.Controls.Add(new Label { Text = "시나리오 버전 관리", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = Globals.FontTitle, ForeColor = Globals.TextPrimary }, 0, 0);
            scenarios.Dock = DockStyle.Fill; scenarios.DropDownStyle = ComboBoxStyle.DropDownList; scenarios.FlatStyle = FlatStyle.Flat; scenarios.BackColor = Globals.SurfaceAlt; scenarios.ForeColor = Globals.TextPrimary; scenarios.Margin = new Padding(0, 6, 0, 6);
            scenarios.SelectedIndexChanged += (_, _) => LoadVersions();
            root.Controls.Add(scenarios, 0, 1);
            versions.Dock = DockStyle.Fill; versions.BackColor = Globals.Surface; versions.ForeColor = Globals.TextPrimary; versions.BorderStyle = BorderStyle.FixedSingle;
            root.Controls.Add(versions, 0, 2);
            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = Globals.Bg };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            var open = Btn("버전 폴더 열기", Globals.SurfaceAlt); open.Click += (_, _) => OpenFolder();
            var restore = Btn("선택 버전 복원", Globals.Accent); restore.ForeColor = Color.White; restore.Click += (_, _) => Restore();
            var close = Btn("닫기", Globals.SurfaceAlt); close.Click += (_, _) => Close();
            footer.Controls.Add(open, 1, 0); footer.Controls.Add(restore, 2, 0); footer.Controls.Add(close, 3, 0); root.Controls.Add(footer, 0, 3);
            Controls.Add(root);

            Directory.CreateDirectory(testSetPath);
            Directory.CreateDirectory(csvPath);
            foreach (string dir in Directory.GetDirectories(testSetPath).OrderBy(Path.GetFileName))
            {
                if (Path.GetFileName(dir).StartsWith("_", StringComparison.Ordinal)) continue;
                scenarios.Items.Add(new VersionTarget
                {
                    Display = "SET · " + Path.GetFileName(dir),
                    TargetPath = Path.Combine(dir, "scenario.csv"),
                    VersionsDir = Path.Combine(dir, ".versions")
                });
            }
            foreach (string csv in Directory.GetFiles(csvPath, "*.csv").OrderBy(Path.GetFileName))
            {
                string name = Path.GetFileNameWithoutExtension(csv);
                scenarios.Items.Add(new VersionTarget
                {
                    Display = "CSV · " + name,
                    TargetPath = csv,
                    VersionsDir = Path.Combine(csvPath, ".versions", name)
                });
            }
            if (scenarios.Items.Count > 0)
            {
                int idx = -1;
                if (!string.IsNullOrWhiteSpace(preferredScenario))
                {
                    for (int i = 0; i < scenarios.Items.Count; i++)
                        if (scenarios.Items[i] is VersionTarget target && target.Display.EndsWith(" · " + preferredScenario, StringComparison.OrdinalIgnoreCase)) { idx = i; break; }
                }
                scenarios.SelectedIndex = idx >= 0 ? idx : 0;
            }
        }

        private RoundedButton Btn(string text, Color fill) => new() { Text = text, Dock = DockStyle.Fill, Margin = new Padding(4), FillColor = fill, HoverColor = fill == Globals.Accent ? Globals.AccentHover : Globals.SurfaceRaised, PressedColor = Globals.SurfaceAlt, ForeColor = Globals.TextPrimary, BorderColor = Globals.Border, BorderThickness = fill == Globals.Accent ? 0 : 1, BorderRadius = Globals.RadiusSm, TextAlign = ContentAlignment.MiddleCenter };
        private void LoadVersions() { versions.Items.Clear(); current = scenarios.SelectedItem as VersionTarget; if (current == null || !Directory.Exists(current.VersionsDir)) return; foreach (string file in Directory.GetFiles(current.VersionsDir, "*.csv").OrderByDescending(Path.GetFileName)) versions.Items.Add(file); }
        private void OpenFolder() { if (current == null) return; Directory.CreateDirectory(current.VersionsDir); Process.Start(new ProcessStartInfo("explorer.exe", current.VersionsDir) { UseShellExecute = true }); }
        private void Restore() { if (current == null || versions.SelectedItem is not string file) return; if (MessageBox.Show("선택한 버전으로 시나리오를 복원할까요? 현재 파일은 복원 전에 다시 백업됩니다.", "버전 복원", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return; Directory.CreateDirectory(current.VersionsDir); if (File.Exists(current.TargetPath)) File.Copy(current.TargetPath, Path.Combine(current.VersionsDir, DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_before_restore.csv"), true); Directory.CreateDirectory(Path.GetDirectoryName(current.TargetPath)!); File.Copy(file, current.TargetPath, true); MessageBox.Show("복원 완료"); }
    }
}
