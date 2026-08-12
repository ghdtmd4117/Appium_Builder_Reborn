using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AppiumBuilder.Core;
using AppiumBuilder.Utils;

namespace AppiumBuilder.UI
{
    public sealed class TestHistoryViewerForm : Form
    {
        private readonly string _logFolder;
        private readonly DataGridView _grid;
        private readonly TextBox _search;
        private readonly ComboBox _status;
        private readonly Label _summary;
        private List<TestRunRecord> _all = new();

        public TestHistoryViewerForm(string logFolder)
        {
            _logFolder = logFolder;
            Text = "전체 실행 이력";
            Size = new Size(1080, 680);
            MinimumSize = new Size(860, 560);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Globals.Bg;
            ForeColor = Globals.TextPrimary;
            Font = Globals.FontBody;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(18),
                BackColor = Globals.Bg
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var title = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent };
            title.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            title.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
            var titleText = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent };
            titleText.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            titleText.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            titleText.Controls.Add(new Label { Text = "전체 실행 이력", Dock = DockStyle.Fill, Font = Globals.FontTitle, ForeColor = Globals.TextPrimary, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            titleText.Controls.Add(new Label { Text = "시나리오별 실행 결과, 기기, 소요 시간과 실패 내용을 검색합니다.", Dock = DockStyle.Fill, Font = Globals.FontMuted, ForeColor = Globals.TextMuted, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
            title.Controls.Add(titleText, 0, 0);
            _summary = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = Globals.FontSub, ForeColor = Globals.Accent };
            title.Controls.Add(_summary, 1, 0);
            root.Controls.Add(title, 0, 0);

            var filter = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Color.Transparent };
            filter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            filter.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            filter.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            _search = new ModernTextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 7, 8, 7), BackColor = Globals.SurfaceAlt, ForeColor = Globals.TextPrimary, BorderColor = Globals.Border, FocusBorderColor = Globals.Accent, Font = Globals.FontBody, PlaceholderText = "시나리오 / 기기 / 오류 검색" };
            _status = new ComboBox { Dock = DockStyle.Fill, Margin = new Padding(0, 7, 8, 7), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Globals.Surface, ForeColor = Globals.TextPrimary, FlatStyle = FlatStyle.Flat };
            _status.Items.AddRange(new object[] { "전체", "PASS", "FAIL", "STOPPED", "SKIPPED" });
            _status.SelectedIndex = 0;
            var refresh = new RoundedButton { Dock = DockStyle.Fill, Margin = new Padding(0, 7, 0, 7), Text = "새로고침", FillColor = Globals.Surface, HoverColor = Globals.SurfaceRaised, PressedColor = Globals.SurfaceAlt, ForeColor = Globals.Accent, BorderColor = Globals.Border, BorderThickness = 1, BorderRadius = Globals.RadiusSm, TextAlign = ContentAlignment.MiddleCenter };
            refresh.FlatAppearance.BorderColor = Globals.Border;
            filter.Controls.Add(_search, 0, 0);
            filter.Controls.Add(_status, 1, 0);
            filter.Controls.Add(refresh, 2, 0);
            root.Controls.Add(filter, 0, 1);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Globals.Surface,
                BorderStyle = BorderStyle.FixedSingle,
                GridColor = Globals.Border,
                EnableHeadersVisualStyles = false,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Globals.SurfaceAlt, ForeColor = Globals.TextMuted, Font = Globals.FontSub, SelectionBackColor = Globals.SurfaceAlt, SelectionForeColor = Globals.TextMuted, Padding = new Padding(6) },
                DefaultCellStyle = new DataGridViewCellStyle { BackColor = Globals.Surface, ForeColor = Globals.TextPrimary, SelectionBackColor = Globals.AccentSoft, SelectionForeColor = Globals.TextPrimary, Font = Globals.FontBody, Padding = new Padding(5) },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Globals.SurfaceAlt },
                RowHeadersVisible = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
                RowTemplate = { Height = 38 }
            };
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "실행 시간", DataPropertyName = "Time", Width = 150 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "시나리오 / 테스트명", DataPropertyName = "Scenario", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 150 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "디바이스", DataPropertyName = "Device", Width = 180 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "결과", DataPropertyName = "Status", Width = 90 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "소요 시간", DataPropertyName = "Duration", Width = 100 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "단계", DataPropertyName = "Steps", Width = 70 });
            root.Controls.Add(_grid, 0, 2);
            Controls.Add(root);

            _search.TextChanged += (_, _) => ApplyFilter();
            _status.SelectedIndexChanged += (_, _) => ApplyFilter();
            refresh.Click += (_, _) => Reload();
            _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) ShowDetails(e.RowIndex); };
            Shown += (_, _) => Reload();
        }

        private void Reload()
        {
            _all = TestHistoryStore.Load(_logFolder).OrderByDescending(GetTime).ToList();
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string q = _search.Text.Trim();
            string status = _status.SelectedItem?.ToString() ?? "전체";
            var filtered = _all.Where(r =>
            {
                string rs = GetStatus(r);
                if (status != "전체" && rs != status) return false;
                if (q.Length == 0) return true;
                string hay = string.Join(" ", r.scenario, r.deviceModel, r.osVersion, r.failMessage, rs);
                return hay.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
            }).ToList();

            _grid.DataSource = filtered.Select(r => new HistoryRow
            {
                Record = r,
                Time = GetTime(r) == DateTime.MinValue ? "-" : GetTime(r).ToString("yyyy-MM-dd HH:mm:ss"),
                Scenario = r.scenario,
                Device = string.IsNullOrWhiteSpace(r.deviceModel) ? "-" : r.deviceModel + (string.IsNullOrWhiteSpace(r.osVersion) ? "" : " (" + r.osVersion + ")"),
                Status = GetStatus(r),
                Duration = FormatDuration(r.durationMs),
                Steps = r.totalSteps.ToString("N0")
            }).ToList();
            _summary.Text = $"{filtered.Count:N0}건 / 전체 {_all.Count:N0}건";
        }

        private void ShowDetails(int rowIndex)
        {
            if (_grid.Rows[rowIndex].DataBoundItem is not HistoryRow row) return;
            TestRunRecord r = row.Record;
            string text = $"시나리오: {r.scenario}\n결과: {GetStatus(r)}\n실행 시간: {row.Time}\n기기: {row.Device}\n단계: {r.totalSteps:N0}\n소요 시간: {row.Duration}";
            TestStepRecord? failed = r.steps?.FirstOrDefault(s => string.Equals(s.status, "FAIL", StringComparison.OrdinalIgnoreCase) || string.Equals(s.status, "STOPPED", StringComparison.OrdinalIgnoreCase));
            if (failed != null)
            {
                text += $"\n\n실패 단계: #{failed.index} · {failed.raw}";
                if (!string.IsNullOrWhiteSpace(failed.message)) text += "\n오류: " + failed.message;
                if (!string.IsNullOrWhiteSpace(failed.artifactFolder)) text += "\nArtifact: " + failed.artifactFolder;
            }
            if (!string.IsNullOrWhiteSpace(r.failMessage)) text += "\n\n실패 메시지:\n" + r.failMessage;
            MessageBox.Show(this, text, "실행 상세", MessageBoxButtons.OK, GetStatus(r) == "PASS" ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        private static string GetStatus(TestRunRecord record)
        {
            if (!string.IsNullOrWhiteSpace(record.status)) return record.status.Trim().ToUpperInvariant();
            return record.pass ? "PASS" : "FAIL";
        }

        private static DateTime GetTime(TestRunRecord record)
        {
            if (DateTime.TryParse(record.timestamp, out DateTime t)) return t.ToLocalTime();
            if (DateTime.TryParse(record.startedAt, out DateTime s)) return s.ToLocalTime();
            return DateTime.MinValue;
        }

        private static string FormatDuration(long ms)
        {
            if (ms <= 0) return "-";
            TimeSpan t = TimeSpan.FromMilliseconds(ms);
            return t.TotalHours >= 1 ? $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}" : $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";
        }

        private sealed class HistoryRow
        {
            public TestRunRecord Record { get; set; } = null!;
            public string Time { get; set; } = string.Empty;
            public string Scenario { get; set; } = string.Empty;
            public string Device { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string Duration { get; set; } = string.Empty;
            public string Steps { get; set; } = string.Empty;
        }
    }
}
