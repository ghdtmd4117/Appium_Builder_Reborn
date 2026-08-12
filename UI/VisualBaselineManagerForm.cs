using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using AppiumBuilder.Core;
using AppiumBuilder.Utils;

namespace AppiumBuilder.UI
{
    public sealed class VisualBaselineManagerForm : Form
    {
        private readonly string testSetPath;
        private readonly ComboBox scenarios = new();
        private readonly ListBox steps = new();
        private readonly NumericUpDown threshold = new();
        private readonly DataGridView masks = new();
        private VisualAssertConfig config = new();
        private string currentScenarioFolder = string.Empty;

        public VisualBaselineManagerForm(string testSetPath, string? preferredScenario = null)
        {
            this.testSetPath = testSetPath;
            Text = "Visual Assert 기준 화면 관리자";
            Size = new Size(900, 650);
            MinimumSize = new Size(820, 580);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Globals.Bg;
            ForeColor = Globals.TextPrimary;
            Font = Globals.FontBody;
            AutoScaleMode = AutoScaleMode.Dpi;

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(18), BackColor = Globals.Bg };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

            root.Controls.Add(new Label { Text = "시나리오", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = Globals.FontSub, ForeColor = Globals.TextSecondary }, 0, 0);
            scenarios.Dock = DockStyle.Fill;
            scenarios.DropDownStyle = ComboBoxStyle.DropDownList;
            scenarios.FlatStyle = FlatStyle.Flat;
            scenarios.BackColor = Globals.SurfaceAlt;
            scenarios.ForeColor = Globals.TextPrimary;
            scenarios.Margin = new Padding(0, 8, 0, 8);
            scenarios.SelectedIndexChanged += (_, _) => LoadScenario();
            root.Controls.Add(scenarios, 1, 0);

            var left = new RoundedPanel { Dock = DockStyle.Fill, FillColor = Globals.Surface, BorderColor = Globals.Border, BorderThickness = 1, BorderRadius = Globals.Radius, Padding = new Padding(12) };
            var leftGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.Transparent };
            leftGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            leftGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            leftGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            leftGrid.Controls.Add(new Label { Text = "기준 화면 STEP", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = Globals.FontSub, ForeColor = Globals.TextPrimary }, 0, 0);
            steps.Dock = DockStyle.Fill;
            steps.BackColor = Globals.SurfaceAlt;
            steps.ForeColor = Globals.TextPrimary;
            steps.BorderStyle = BorderStyle.None;
            steps.SelectedIndexChanged += (_, _) => LoadStepConfig();
            leftGrid.Controls.Add(steps, 0, 1);
            var openFolder = Button("기준 화면 폴더 열기", Globals.SurfaceAlt);
            openFolder.Click += (_, _) => OpenBaselineFolder();
            leftGrid.Controls.Add(openFolder, 0, 2);
            left.Controls.Add(leftGrid);
            root.Controls.Add(left, 0, 1);

            var right = new RoundedPanel { Dock = DockStyle.Fill, FillColor = Globals.Surface, BorderColor = Globals.Border, BorderThickness = 1, BorderRadius = Globals.Radius, Padding = new Padding(14), Margin = new Padding(12, 0, 0, 0) };
            var rightGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, BackColor = Color.Transparent };
            rightGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            rightGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            rightGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            rightGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            rightGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            rightGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            rightGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            rightGrid.Controls.Add(new Label { Text = "일치율 기준(%)", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = Globals.FontSub, ForeColor = Globals.TextSecondary }, 0, 0);
            threshold.Minimum = 0; threshold.Maximum = 100; threshold.DecimalPlaces = 1; threshold.Increment = 0.5M; threshold.Value = 95; threshold.Dock = DockStyle.Left; threshold.Width = 140;
            rightGrid.Controls.Add(threshold, 0, 1);
            rightGrid.Controls.Add(new Label { Text = "동적 영역 Mask · x/y/width/height는 0~1 비율 또는 실제 px 값", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = Globals.FontMuted, ForeColor = Globals.TextMuted }, 0, 2);
            masks.Dock = DockStyle.Fill;
            masks.BackgroundColor = Globals.SurfaceAlt;
            masks.GridColor = Globals.Border;
            masks.BorderStyle = BorderStyle.None;
            masks.ForeColor = Globals.TextPrimary;
            masks.DefaultCellStyle.BackColor = Globals.SurfaceAlt;
            masks.DefaultCellStyle.ForeColor = Globals.TextPrimary;
            masks.DefaultCellStyle.SelectionBackColor = Globals.AccentSoft;
            masks.ColumnHeadersDefaultCellStyle.BackColor = Globals.SurfaceRaised;
            masks.ColumnHeadersDefaultCellStyle.ForeColor = Globals.TextSecondary;
            masks.EnableHeadersVisualStyles = false;
            masks.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            foreach (string name in new[] { "x", "y", "width", "height" }) masks.Columns.Add(name, name);
            rightGrid.Controls.Add(masks, 0, 3);
            rightGrid.Controls.Add(new Label { Text = "Mask 영역은 비교에서 제외됩니다. 시간·배터리·광고·동적 배너 등에 사용하세요.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = Globals.FontMuted, ForeColor = Globals.TextFaint }, 0, 4);
            var replace = Button("선택 STEP 기준 이미지 교체", Globals.SurfaceAlt);
            replace.Click += (_, _) => ReplaceBaseline();
            rightGrid.Controls.Add(replace, 0, 5);
            var delete = Button("선택 STEP 기준 이미지 삭제", Globals.DangerSoft); delete.ForeColor = Globals.Danger; delete.IconColor = Globals.Danger;
            delete.Click += (_, _) => DeleteBaseline();
            rightGrid.Controls.Add(delete, 0, 6);
            right.Controls.Add(rightGrid);
            root.Controls.Add(right, 1, 1);

            var close = Button("닫기", Globals.SurfaceAlt); close.Click += (_, _) => Close();
            var save = Button("설정 저장", Globals.Accent); save.ForeColor = Color.White; save.IconColor = Color.White; save.Click += (_, _) => SaveConfig();
            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Globals.Bg };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            footer.Controls.Add(close, 1, 0); footer.Controls.Add(save, 2, 0);
            root.Controls.Add(footer, 0, 2); root.SetColumnSpan(footer, 2);
            Controls.Add(root);
            LoadScenarios(preferredScenario);
        }

        private RoundedButton Button(string text, Color fill) => new()
        {
            Text = text, Dock = DockStyle.Fill, Margin = new Padding(4), FillColor = fill,
            HoverColor = fill == Globals.Accent ? Globals.AccentHover : Globals.SurfaceRaised,
            PressedColor = fill == Globals.Accent ? Globals.AccentPressed : Globals.SurfaceAlt,
            ForeColor = Globals.TextPrimary, BorderColor = Globals.Border, BorderThickness = fill == Globals.Accent ? 0 : 1,
            BorderRadius = Globals.RadiusSm, TextAlign = ContentAlignment.MiddleCenter
        };

        private void LoadScenarios(string? preferred)
        {
            Directory.CreateDirectory(testSetPath);
            var dirs = Directory.GetDirectories(testSetPath).Where(d => !Path.GetFileName(d).StartsWith("_", StringComparison.Ordinal)).OrderBy(Path.GetFileName).ToList();
            foreach (string d in dirs) scenarios.Items.Add(Path.GetFileName(d));
            if (scenarios.Items.Count == 0) return;
            int idx = !string.IsNullOrWhiteSpace(preferred) ? scenarios.Items.IndexOf(preferred) : -1;
            scenarios.SelectedIndex = idx >= 0 ? idx : 0;
        }

        private void LoadScenario()
        {
            if (scenarios.SelectedItem == null) return;
            currentScenarioFolder = Path.Combine(testSetPath, scenarios.SelectedItem.ToString()!);
            config = VisualAssertConfig.Load(currentScenarioFolder);
            threshold.Value = (decimal)Math.Max(0, Math.Min(100, config.defaultThreshold));
            steps.Items.Clear();
            string baseline = Path.Combine(currentScenarioFolder, "baseline");
            if (Directory.Exists(baseline))
                foreach (string file in Directory.GetFiles(baseline, "step_*.png").OrderBy(Path.GetFileName)) steps.Items.Add(Path.GetFileNameWithoutExtension(file));
            if (steps.Items.Count > 0) steps.SelectedIndex = 0;
            else masks.Rows.Clear();
        }

        private string? StepKey()
        {
            if (steps.SelectedItem == null) return null;
            string raw = steps.SelectedItem.ToString() ?? string.Empty;
            if (raw.StartsWith("step_", StringComparison.OrdinalIgnoreCase) && int.TryParse(raw[5..], out int n)) return n.ToString();
            return raw;
        }

        private void LoadStepConfig()
        {
            masks.Rows.Clear();
            string? key = StepKey(); if (key == null) return;
            if (config.steps.TryGetValue(key, out VisualStepConfig? step))
            {
                threshold.Value = (decimal)Math.Max(0, Math.Min(100, step.threshold ?? config.defaultThreshold));
                foreach (var mask in step.masks) masks.Rows.Add(mask.x, mask.y, mask.width, mask.height);
            }
            else threshold.Value = (decimal)config.defaultThreshold;
        }

        private void SaveConfig()
        {
            if (string.IsNullOrWhiteSpace(currentScenarioFolder)) return;
            string? key = StepKey();
            if (key == null) config.defaultThreshold = (double)threshold.Value;
            else
            {
                var step = config.steps.TryGetValue(key, out VisualStepConfig? existing) ? existing : new VisualStepConfig();
                step.threshold = (double)threshold.Value;
                step.masks = new List<VisualMaskRect>();
                foreach (DataGridViewRow row in masks.Rows)
                {
                    if (row.IsNewRow) continue;
                    if (TryCell(row, 0, out double x) && TryCell(row, 1, out double y) && TryCell(row, 2, out double w) && TryCell(row, 3, out double h) && w > 0 && h > 0)
                        step.masks.Add(new VisualMaskRect { x = x, y = y, width = w, height = h });
                }
                config.steps[key] = step;
            }
            config.Save(currentScenarioFolder);
            MessageBox.Show("Visual Assert 설정을 저장했습니다.", "저장 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static bool TryCell(DataGridViewRow row, int i, out double value) => double.TryParse(Convert.ToString(row.Cells[i].Value), out value);
        private void OpenBaselineFolder() { if (string.IsNullOrWhiteSpace(currentScenarioFolder)) return; string dir = Path.Combine(currentScenarioFolder, "baseline"); Directory.CreateDirectory(dir); Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true }); }
        private void ReplaceBaseline() { string? key = StepKey(); if (key == null) return; using var dlg = new OpenFileDialog { Filter = "PNG 이미지|*.png" }; if (dlg.ShowDialog(this) != DialogResult.OK) return; string dir = Path.Combine(currentScenarioFolder, "baseline"); Directory.CreateDirectory(dir); File.Copy(dlg.FileName, Path.Combine(dir, $"step_{int.Parse(key):000}.png"), true); LoadScenario(); }
        private void DeleteBaseline() { string? key = StepKey(); if (key == null) return; if (MessageBox.Show("선택한 기준 이미지를 삭제할까요? 다음 실행에서 새 기준 이미지가 생성됩니다.", "기준 이미지 삭제", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return; string dir = Path.Combine(currentScenarioFolder, "baseline"); foreach (string ext in new[] { ".png", ".xml", ".json" }) { string path = Path.Combine(dir, $"step_{int.Parse(key):000}{ext}"); if (File.Exists(path)) File.Delete(path); } LoadScenario(); }
    }
}
