using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AppiumBuilder.Core;
using AppiumBuilder.Utils;

namespace AppiumBuilder.UI
{
    public sealed class EnvironmentDiagnosticsForm : Form
    {
        private readonly TableLayoutPanel results = new();
        private readonly ComboBox deviceCombo = new();
        private readonly RoundedButton refreshButton = new();
        private readonly Label summary = new();
        private IReadOnlyList<AdbDeviceInfo> devices = Array.Empty<AdbDeviceInfo>();

        public EnvironmentDiagnosticsForm()
        {
            Text = "환경 건강검진 · Appium Builder Reborn";
            Size = new Size(820, 650);
            MinimumSize = new Size(760, 580);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Globals.Bg;
            ForeColor = Globals.TextPrimary;
            Font = Globals.FontBody;
            AutoScaleMode = AutoScaleMode.Dpi;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(22),
                BackColor = Globals.Bg
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

            var title = new Label
            {
                Text = "환경 건강검진",
                Dock = DockStyle.Fill,
                Font = Globals.FontTitle,
                ForeColor = Globals.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            };
            var titleHost = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Globals.Bg };
            titleHost.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            titleHost.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            titleHost.Controls.Add(title, 0, 0);
            titleHost.Controls.Add(new Label
            {
                Text = "ADB · Python · Appium · UiAutomator2 · OpenCV · 저장 경로를 실행 전에 점검합니다.",
                Dock = DockStyle.Fill,
                Font = Globals.FontMuted,
                ForeColor = Globals.TextMuted,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 1);
            root.Controls.Add(titleHost, 0, 0);

            var deviceCard = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                FillColor = Globals.Surface,
                BorderColor = Globals.Border,
                BorderThickness = 1,
                BorderRadius = Globals.Radius,
                Padding = new Padding(14)
            };
            var deviceGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Color.Transparent };
            deviceGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            deviceGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            deviceGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            deviceGrid.Controls.Add(new Label
            {
                Text = "테스트 기기",
                Dock = DockStyle.Fill,
                Font = Globals.FontSub,
                ForeColor = Globals.TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            deviceCombo.Dock = DockStyle.Fill;
            deviceCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            deviceCombo.BackColor = Globals.SurfaceAlt;
            deviceCombo.ForeColor = Globals.TextPrimary;
            deviceCombo.FlatStyle = FlatStyle.Flat;
            deviceCombo.Margin = new Padding(0, 4, 10, 4);
            deviceCombo.SelectedIndexChanged += (_, _) =>
            {
                if (deviceCombo.SelectedItem is AdbDeviceInfo info)
                {
                    AdbEngine.SetSelectedSerial(info.Serial);
                    DeviceSelectionStore.Save(info.Serial);
                }
            };
            deviceGrid.Controls.Add(deviceCombo, 1, 0);
            refreshButton.Text = "다시 검사";
            refreshButton.IconName = "tools";
            refreshButton.FillColor = Globals.Accent;
            refreshButton.HoverColor = Globals.AccentHover;
            refreshButton.PressedColor = Globals.AccentPressed;
            refreshButton.ForeColor = Color.White;
            refreshButton.IconColor = Color.White;
            refreshButton.BorderThickness = 0;
            refreshButton.BorderRadius = Globals.RadiusSm;
            refreshButton.Dock = DockStyle.Fill;
            refreshButton.Margin = new Padding(0, 4, 0, 4);
            refreshButton.Click += async (_, _) => await RunChecksAsync();
            deviceGrid.Controls.Add(refreshButton, 2, 0);
            deviceCard.Controls.Add(deviceGrid);
            root.Controls.Add(deviceCard, 0, 1);

            results.Dock = DockStyle.Fill;
            results.ColumnCount = 1;
            results.AutoScroll = true;
            results.BackColor = Globals.Bg;
            results.Padding = new Padding(0, 10, 0, 8);
            root.Controls.Add(results, 0, 2);

            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Globals.Bg };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            summary.Dock = DockStyle.Fill;
            summary.Font = Globals.FontSub;
            summary.ForeColor = Globals.TextMuted;
            summary.TextAlign = ContentAlignment.MiddleLeft;
            footer.Controls.Add(summary, 0, 0);
            var retentionButton = new RoundedButton { Text = "보존 정책", Dock = DockStyle.Fill, Margin = new Padding(8, 4, 0, 4), FillColor = Globals.SurfaceAlt, HoverColor = Globals.SurfaceRaised, PressedColor = Globals.SurfaceAlt, ForeColor = Globals.TextPrimary, BorderColor = Globals.Border, BorderThickness = 1, BorderRadius = Globals.RadiusSm, TextAlign = ContentAlignment.MiddleCenter };
            retentionButton.Click += (_, _) => { using var dialog = new RetentionSettingsForm(); dialog.ShowDialog(this); };
            footer.Controls.Add(retentionButton, 1, 0);
            root.Controls.Add(footer, 0, 3);
            Controls.Add(root);

            Shown += async (_, _) => await RunChecksAsync();
        }

        private async Task RunChecksAsync()
        {
            refreshButton.Enabled = false;
            results.SuspendLayout();
            results.Controls.Clear();
            results.RowStyles.Clear();
            results.RowCount = 0;
            summary.Text = "검사 중...";

            devices = await Task.Run(() => AdbEngine.GetDevices());
            string? selected = AdbEngine.SelectedSerial;
            if (!string.IsNullOrWhiteSpace(selected) && !devices.Any(d => d.State == "device" && string.Equals(d.Serial, selected, StringComparison.OrdinalIgnoreCase)))
            {
                AdbEngine.SetSelectedSerial(null);
                DeviceSelectionStore.Save(null);
                selected = null;
            }
            deviceCombo.BeginUpdate();
            deviceCombo.Items.Clear();
            foreach (var device in devices.Where(d => d.State == "device")) deviceCombo.Items.Add(device);
            if (deviceCombo.Items.Count > 0)
            {
                int index = Enumerable.Range(0, deviceCombo.Items.Count)
                    .FirstOrDefault(i => deviceCombo.Items[i] is AdbDeviceInfo d && d.Serial == selected);
                deviceCombo.SelectedIndex = index >= 0 && index < deviceCombo.Items.Count ? index : 0;
            }
            deviceCombo.EndUpdate();

            var checks = new List<(string Name, bool Ok, string Detail)>();
            string adbVersion = await AdbEngine.RunGlobalCommandAsync("version", 5000);
            checks.Add(("ADB", !adbVersion.StartsWith("ADB Error", StringComparison.OrdinalIgnoreCase) && !adbVersion.StartsWith("ADB Timeout", StringComparison.OrdinalIgnoreCase), Short(adbVersion)));
            int usable = devices.Count(d => d.State == "device");
            checks.Add(("Android 기기", usable == 1 || (usable > 1 && !string.IsNullOrWhiteSpace(AdbEngine.SelectedSerial)), usable == 0 ? "연결된 기기 없음" : usable == 1 ? devices.First(d => d.State == "device").Serial : $"{usable}대 연결 · 선택: {AdbEngine.SelectedSerial ?? "필요"}"));
            checks.Add(await CheckProcessAsync("Python 3", "python", "--version"));
            checks.Add(await CheckProcessAsync("Appium CLI", "appium", "--version"));
            checks.Add(await CheckAppiumServerAsync());
            checks.Add(await CheckUiAutomator2Async());
            checks.Add(await CheckPythonImportAsync("Appium Python Client", "appium"));
            checks.Add(await CheckPythonImportAsync("Selenium", "selenium"));
            checks.Add(await CheckPythonImportAsync("OpenCV", "cv2"));
            checks.Add(await CheckPythonImportAsync("Pytest", "pytest"));
            checks.Add(("로그 폴더", CheckLogFolder(out string folderDetail), folderDetail));
            LogRetentionSettings retention = LogRetentionSettings.Load(Globals.LogFolder);
            checks.Add(("로그 보존 정책", true, $"{retention.retentionDays}일 · 최대 {retention.maxSizeGb:0.0} GB"));

            foreach (var check in checks) AddResult(check.Name, check.Ok, check.Detail);
            int pass = checks.Count(c => c.Ok);
            summary.Text = pass == checks.Count
                ? $"전체 {checks.Count}개 항목 정상 · 실행 준비 완료"
                : $"{pass}/{checks.Count} 정상 · 실패 항목을 먼저 해결하세요.";
            summary.ForeColor = pass == checks.Count ? Globals.Success : Globals.Warning;
            results.ResumeLayout();
            refreshButton.Enabled = true;
        }

        private void AddResult(string name, bool ok, string detail)
        {
            int rowIndex = results.RowCount++;
            results.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            var card = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 8),
                FillColor = Globals.Surface,
                BorderColor = Globals.Border,
                BorderThickness = 1,
                BorderRadius = Globals.RadiusSm
            };
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Padding = new Padding(14, 0, 14, 0), BackColor = Color.Transparent };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 24));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grid.Controls.Add(new Label { Text = ok ? "●" : "●", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = ok ? Globals.Success : Globals.Danger, Font = Globals.FontSub }, 0, 0);
            grid.Controls.Add(new Label { Text = name, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Globals.TextPrimary, Font = Globals.FontSub }, 1, 0);
            grid.Controls.Add(new Label { Text = detail, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Globals.TextMuted, Font = Globals.FontMuted, AutoEllipsis = true }, 2, 0);
            card.Controls.Add(grid);
            results.Controls.Add(card, 0, rowIndex);
        }

        private static async Task<(string Name, bool Ok, string Detail)> CheckProcessAsync(string name, string file, string args)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo(file, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                    using var p = Process.Start(psi);
                    if (p == null) return (name, false, "프로세스를 시작하지 못함");
                    Task<string> stdout = p.StandardOutput.ReadToEndAsync();
                    Task<string> stderr = p.StandardError.ReadToEndAsync();
                    if (!p.WaitForExit(6000)) { try { p.Kill(true); } catch { } return (name, false, "응답 시간 초과"); }
                    string output = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
                    return (name, p.ExitCode == 0, Short(output));
                }
                catch (Exception ex) { return (name, false, ex.Message); }
            });
        }


        private static async Task<(string Name, bool Ok, string Detail)> CheckAppiumServerAsync()
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1.5) };
            foreach (string url in new[] { "http://127.0.0.1:4723/status", "http://127.0.0.1:4723/wd/hub/status" })
            {
                try
                {
                    using HttpResponseMessage response = await client.GetAsync(url);
                    if (response.IsSuccessStatusCode) return ("Appium Server", true, url);
                }
                catch { }
            }
            return ("Appium Server", false, "127.0.0.1:4723 미응답 · 봇 실행 전에 Appium 서버를 시작하세요.");
        }

        private static async Task<(string Name, bool Ok, string Detail)> CheckUiAutomator2Async()
        {
            var result = await CheckProcessAsync("UiAutomator2 Driver", "appium", "driver list --installed");
            bool installed = result.Ok && result.Detail.Contains("uiautomator2", StringComparison.OrdinalIgnoreCase);
            return ("UiAutomator2 Driver", installed, installed ? result.Detail : "설치되지 않음 · appium driver install uiautomator2");
        }
        private static Task<(string Name, bool Ok, string Detail)> CheckPythonImportAsync(string name, string module)
            => CheckProcessAsync(name, "python", $"-c \"import {module}; print(getattr({module}, '__version__', 'installed'))\"");

        private static bool CheckLogFolder(out string detail)
        {
            try
            {
                System.IO.Directory.CreateDirectory(Globals.LogFolder);
                string probe = System.IO.Path.Combine(Globals.LogFolder, ".healthcheck.tmp");
                System.IO.File.WriteAllText(probe, DateTime.Now.ToString("O"), Encoding.UTF8);
                System.IO.File.Delete(probe);
                detail = Globals.LogFolder;
                return true;
            }
            catch (Exception ex) { detail = ex.Message; return false; }
        }

        private static string Short(string value)
        {
            string line = (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            return line.Length > 150 ? line[..150] + "…" : line;
        }
    }
}
