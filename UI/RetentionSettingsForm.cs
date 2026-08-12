using System;
using System.Drawing;
using System.Windows.Forms;
using AppiumBuilder.Core;
using AppiumBuilder.Utils;

namespace AppiumBuilder.UI
{
    public sealed class RetentionSettingsForm : Form
    {
        private readonly NumericUpDown days = new();
        private readonly NumericUpDown sizeGb = new();

        public RetentionSettingsForm()
        {
            Text = "로그 보존 정책";
            Size = new Size(560, 360);
            MinimumSize = new Size(520, 340);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Globals.Bg;
            ForeColor = Globals.TextPrimary;
            Font = Globals.FontBody;
            AutoScaleMode = AutoScaleMode.Dpi;

            LogRetentionSettings settings = LogRetentionSettings.Load(Globals.LogFolder);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5, Padding = new Padding(20), BackColor = Globals.Bg };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            var title = new Label { Text = "로그 보존 정책", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = Globals.FontHeading, ForeColor = Globals.TextPrimary };
            root.Controls.Add(title, 0, 0);
            root.SetColumnSpan(title, 2);
            root.Controls.Add(new Label { Text = "보존 기간", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Globals.TextSecondary }, 0, 1);
            days.Minimum = 1; days.Maximum = 3650; days.Value = settings.retentionDays; days.Dock = DockStyle.Fill; days.Margin = new Padding(0, 7, 0, 7); root.Controls.Add(days, 1, 1);
            root.Controls.Add(new Label { Text = "최대 용량 (GB)", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Globals.TextSecondary }, 0, 2);
            sizeGb.Minimum = 0.1M; sizeGb.Maximum = 1000; sizeGb.DecimalPlaces = 1; sizeGb.Increment = 0.5M; sizeGb.Value = (decimal)settings.maxSizeGb; sizeGb.Dock = DockStyle.Fill; sizeGb.Margin = new Padding(0, 7, 0, 7); root.Controls.Add(sizeGb, 1, 2);
            var note = new Label { Text = "기준 이미지(baseline), 테스트 이력, API 키, 선택 기기 정보는 자동 정리 대상에서 제외됩니다.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Globals.TextMuted, Font = Globals.FontMuted, AutoEllipsis = false };
            root.Controls.Add(note, 0, 3);
            root.SetColumnSpan(note, 2);
            var save = new RoundedButton { Text = "저장 및 지금 정리", Dock = DockStyle.Fill, FillColor = Globals.Accent, HoverColor = Globals.AccentHover, PressedColor = Globals.AccentPressed, ForeColor = Color.White, BorderRadius = Globals.RadiusSm, TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0) };
            save.Click += (_, _) =>
            {
                var next = new LogRetentionSettings { retentionDays = (int)days.Value, maxSizeGb = (double)sizeGb.Value };
                next.Save(Globals.LogFolder);
                LogRetention.Cleanup(Globals.LogFolder, next.retentionDays, next.MaxBytes);
                DialogResult = DialogResult.OK;
                Close();
            };
            root.Controls.Add(save, 0, 4); root.SetColumnSpan(save, 2);
            Controls.Add(root);
        }
    }
}
