using System;
using System.Drawing;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;
using AppiumBuilder.Utils;
using AppiumBuilder.Core;
using AppiumBuilder.UI;

namespace AppiumBuilder
{
    public partial class MainForm
    {
        private void SetupBaseUI()
        {
            DoubleBuffered = true;
            Text = "Appium Builder Reborn";
            MinimumSize = new Size(1024, 680);
            Size = new Size(1600, 960);
            BackColor = Globals.Bg;
            ForeColor = Globals.TextPrimary;
            Font = Globals.FontBody;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;

            // ===== 하단 상태바 =====
            var pnlFooter = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Globals.Sidebar
            };
            var footerTopBorder = new Panel
            {
                Height = 1,
                Dock = DockStyle.Top,
                BackColor = Globals.SidebarBorder
            };
            var footerAccent = new RoundedPanel
            {
                Size = new Size(6, 6),
                Location = new Point(Globals.SidebarWidth + 18, 12),
                FillColor = Globals.Accent,
                BorderRadius = 3,
                BorderThickness = 0
            };
            lblStatusMsg = new Label
            {
                Text = "상태: Appium Builder를 시작합니다.",
                Location = new Point(Globals.SidebarWidth + 32, 6),
                Font = Globals.FontMuted,
                ForeColor = Globals.TextMuted,
                AutoSize = false,
                Size = new Size(900, 20),
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleLeft
            };
            pnlFooter.Controls.Add(footerTopBorder);
            pnlFooter.Controls.Add(footerAccent);
            pnlFooter.Controls.Add(lblStatusMsg);

            // ===== 초기 연결 화면 =====
            pnlConnect = new DoubleBufferedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Globals.Bg,
                Visible = true
            };

            var connCenterGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 3,
                BackColor = Globals.Bg,
                Padding = new Padding(20)
            };
            connCenterGrid.ColumnStyles.Add(ColPct(100));
            connCenterGrid.ColumnStyles.Add(ColAbs(540));
            connCenterGrid.ColumnStyles.Add(ColPct(100));
            connCenterGrid.RowStyles.Add(Pct(100));
            connCenterGrid.RowStyles.Add(Abs(600));
            connCenterGrid.RowStyles.Add(Pct(100));

            var pnlConnCard = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                FillColor = Globals.Surface,
                BorderColor = Globals.Border,
                BorderThickness = 1,
                BorderRadius = Globals.Radius,
                Margin = new Padding(0)
            };

            var brandIcon = new IconGlyph
            {
                IconName = "appium",
                IconColor = Globals.Accent,
                Location = new Point(36, 28),
                Size = new Size(32, 32)
            };
            var lblProduct = new Label
            {
                Text = "Appium Builder Reborn",
                Location = new Point(82, 25),
                Size = new Size(276, 28),
                Font = Globals.FontHeading,
                ForeColor = Globals.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            };
            var lblProductMeta = new Label
            {
                Text = "Android · Appium · ADB 기반 QA 자동화",
                Location = new Point(82, 54),
                Size = new Size(276, 22),
                Font = Globals.FontMuted,
                ForeColor = Globals.TextMuted,
                TextAlign = ContentAlignment.MiddleLeft
            };
            var btnLocalTcEntry = CreateModernButton(
                "로컬 TC",
                Globals.SurfaceAlt,
                370,
                36,
                134,
                36,
                "archive");
            btnLocalTcEntry.ForeColor = Globals.Accent;
            btnLocalTcEntry.IconColor = Globals.Accent;
            btnLocalTcEntry.BorderColor = Globals.Border;
            btnLocalTcEntry.BorderThickness = 1;
            btnLocalTcEntry.Click += (_, _) =>
            {
                using var form = new LocalTestCaseBuilderForm();
                form.ShowDialog(this);
            };
            var connDivider = new Panel
            {
                Location = new Point(36, 94),
                Size = new Size(468, 1),
                BackColor = Globals.Border
            };
            var lblConnTitle = new Label
            {
                Text = "디바이스를 연결하세요",
                Location = new Point(36, 116),
                Size = new Size(468, 36),
                Font = Globals.FontTitle,
                ForeColor = Globals.TextPrimary,
                TextAlign = ContentAlignment.MiddleCenter
            };
            var lblConnDescription = new Label
            {
                Text = "자동화 테스트를 시작하려면 연결 방식을 선택해 주세요.",
                Location = new Point(36, 154),
                Size = new Size(468, 24),
                Font = Globals.FontMuted,
                ForeColor = Globals.TextSecondary,
                TextAlign = ContentAlignment.MiddleCenter
            };

            // 장식보다 기능을 앞세운 단순한 기기 연결 다이어그램
            var visualCard = new RoundedPanel
            {
                Location = new Point(36, 190),
                Size = new Size(468, 88),
                FillColor = Globals.SurfaceAlt,
                BorderColor = Globals.Border,
                BorderThickness = 1,
                BorderRadius = Globals.RadiusSm
            };
            var monitorIcon = new IconGlyph
            {
                IconName = "monitor",
                IconColor = Globals.TextMuted,
                Location = new Point(98, 24),
                Size = new Size(44, 44)
            };
            var phoneIcon = new IconGlyph
            {
                IconName = "phone",
                IconColor = Globals.TextMuted,
                Location = new Point(326, 20),
                Size = new Size(36, 50)
            };
            var connectionLineLeft = new Panel
            {
                Location = new Point(151, 43),
                Size = new Size(69, 1),
                BackColor = Globals.BorderStrong
            };
            var connectionLineRight = new Panel
            {
                Location = new Point(248, 43),
                Size = new Size(69, 1),
                BackColor = Globals.BorderStrong
            };
            var modeIcon = new IconGlyph
            {
                IconName = "usb",
                IconColor = Globals.Accent,
                Location = new Point(223, 31),
                Size = new Size(22, 22)
            };
            visualCard.Controls.AddRange(new Control[]
            {
                monitorIcon,
                connectionLineLeft,
                modeIcon,
                connectionLineRight,
                phoneIcon
            });

            var btnWired = CreateModernButton(
                "유선 연결 (USB 케이블)",
                Globals.AccentSoft,
                36,
                298,
                226,
                44,
                "usb");
            btnWired.BorderColor = Globals.Accent;
            btnWired.BorderThickness = 1;
            btnWired.IconColor = Globals.AccentText;

            var btnWireless = CreateModernButton(
                "무선 연결 (Wi-Fi / IP)",
                Globals.SurfaceAlt,
                278,
                298,
                226,
                44,
                "wifi");
            btnWireless.BorderColor = Globals.Border;
            btnWireless.BorderThickness = 1;

            var pnlWirelessFields = new RoundedPanel
            {
                Location = new Point(36, 358),
                Size = new Size(468, 76),
                FillColor = Globals.SurfaceAlt,
                BorderColor = Globals.Border,
                BorderThickness = 1,
                BorderRadius = Globals.RadiusSm,
                Visible = false
            };
            var lblWirelessInput = new Label
            {
                Text = "IP 주소로 연결",
                Location = new Point(12, 8),
                Size = new Size(180, 18),
                Font = Globals.FontMuted,
                ForeColor = Globals.TextSecondary
            };
            var ipHost = new ModernTextBoxHost
            {
                Location = new Point(12, 31),
                Size = new Size(316, Globals.ControlHeight),
                FillColor = Globals.Bg,
                BorderColor = Globals.Border,
                FocusBorderColor = Globals.Accent,
                BorderRadius = Globals.RadiusSm
            };
            txtIp = ipHost.Input;
            txtIp.Text = "192.168.0.10";
            txtIp.ForeColor = Globals.TextPrimary;

            var portHost = new ModernTextBoxHost
            {
                Location = new Point(338, 31),
                Size = new Size(118, Globals.ControlHeight),
                FillColor = Globals.Bg,
                BorderColor = Globals.Border,
                FocusBorderColor = Globals.Accent,
                BorderRadius = Globals.RadiusSm
            };
            txtPort = portHost.Input;
            txtPort.Text = "5555";
            txtPort.ForeColor = Globals.TextPrimary;
            txtPort.TextAlign = HorizontalAlignment.Center;
            pnlWirelessFields.Controls.AddRange(new Control[] { lblWirelessInput, ipHost, portHost });

            btnIpConn = CreateModernButton(
                "유선 연결 확인",
                Globals.Accent,
                36,
                370,
                468,
                Globals.PrimaryButtonHeight,
                "usb");
            btnIpConn.HoverColor = Globals.AccentHover;
            btnIpConn.PressedColor = Globals.AccentPressed;

            var lblConnectionError = new Label
            {
                Text = string.Empty,
                Location = new Point(36, 424),
                Size = new Size(468, 48),
                Font = Globals.FontMuted,
                ForeColor = Globals.Danger,
                TextAlign = ContentAlignment.TopLeft,
                AutoEllipsis = false
            };
            var helperIcon = new IconGlyph
            {
                IconName = "info",
                IconColor = Globals.TextMuted,
                Location = new Point(36, 566),
                Size = new Size(16, 16)
            };
            var lblConnectionHelper = new Label
            {
                Text = "연결이 확인되면 홈 화면으로 이동하고 상태 감시를 시작합니다.",
                Location = new Point(60, 558),
                Size = new Size(444, 24),
                Font = Globals.FontMuted,
                ForeColor = Globals.TextMuted,
                TextAlign = ContentAlignment.MiddleLeft
            };

            bool useWireless = false;

            void ClearConnectionError() => lblConnectionError.Text = string.Empty;

            void ApplyConnectionMode(bool wireless)
            {
                useWireless = wireless;
                ClearConnectionError();
                pnlWirelessFields.Visible = wireless;
                modeIcon.IconName = wireless ? "wifi" : "usb";
                modeIcon.Invalidate();

                btnWired.FillColor = wireless ? Globals.SurfaceAlt : Globals.AccentSoft;
                btnWired.BorderColor = wireless ? Globals.Border : Globals.Accent;
                btnWired.ForeColor = Globals.TextPrimary;
                btnWired.IconColor = wireless ? Globals.TextSecondary : Globals.AccentText;

                btnWireless.FillColor = wireless ? Globals.AccentSoft : Globals.SurfaceAlt;
                btnWireless.BorderColor = wireless ? Globals.Accent : Globals.Border;
                btnWireless.ForeColor = Globals.TextPrimary;
                btnWireless.IconColor = wireless ? Globals.AccentText : Globals.TextSecondary;

                btnIpConn.Text = wireless ? "무선으로 접속" : "유선 연결 확인";
                btnIpConn.IconName = wireless ? "wifi" : "usb";
                btnIpConn.Top = wireless ? 452 : 372;
                lblConnectionError.Top = wireless ? 500 : 424;

                btnWired.Invalidate();
                btnWireless.Invalidate();
                btnIpConn.Invalidate();
            }

            void EnterMainWorkspace()
            {
                pnlConnect.Visible = false;
                pnlMain.Visible = true;
                statusTimer?.Start();
                if (pnlTabHome != null && btnTabHome != null)
                    SwitchTab(pnlTabHome, btnTabHome);
            }

            btnWired.Click += (_, _) => ApplyConnectionMode(false);
            btnWireless.Click += (_, _) => ApplyConnectionMode(true);
            txtIp.KeyDown += (_, e) =>
            {
                if (useWireless && e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    btnIpConn.PerformClick();
                }
            };
            txtPort.KeyDown += (_, e) =>
            {
                if (useWireless && e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    btnIpConn.PerformClick();
                }
            };

            btnIpConn.Click += async (_, _) =>
            {
                ClearConnectionError();
                string originalText = btnIpConn.Text;
                string? originalIcon = btnIpConn.IconName;
                btnIpConn.Enabled = false;
                btnIpConn.Text = "연결 확인 중...";
                btnIpConn.IconName = null;
                btnIpConn.Invalidate();

                try
                {
                    if (!useWireless)
                    {
                        bool connected = await Task.Run(AdbEngine.IsDeviceConnected);
                        if (connected)
                        {
                            EnterMainWorkspace();
                            return;
                        }

                        var detectedDevices = await Task.Run(() => AdbEngine.GetDevices());
                        int usableDevices = detectedDevices.Count(device =>
                            string.Equals(device.State, "device", StringComparison.OrdinalIgnoreCase));

                        if (usableDevices > 1)
                        {
                            using var diagnostics = new EnvironmentDiagnosticsForm();
                            diagnostics.ShowDialog(this);
                            if (await Task.Run(AdbEngine.IsDeviceConnected))
                            {
                                EnterMainWorkspace();
                                return;
                            }
                            lblConnectionError.Text = "여러 기기 중 테스트할 기기를 선택해야 합니다.";
                        }
                        else
                        {
                            var unauthorized = detectedDevices.FirstOrDefault(device =>
                                string.Equals(device.State, "unauthorized", StringComparison.OrdinalIgnoreCase));
                            var offline = detectedDevices.FirstOrDefault(device =>
                                string.Equals(device.State, "offline", StringComparison.OrdinalIgnoreCase));

                            if (unauthorized != null)
                                lblConnectionError.Text = "USB 기기는 감지됐지만 인증되지 않았습니다. 휴대폰의 'USB 디버깅을 허용하시겠습니까?' 창에서 허용을 눌러 주세요.";
                            else if (offline != null)
                                lblConnectionError.Text = "USB 기기는 감지됐지만 ADB 상태가 offline입니다. 케이블을 다시 연결하거나 ADB 서버를 재시작해 주세요.";
                            else
                                lblConnectionError.Text = "USB 디바이스를 찾지 못했습니다. USB 디버깅, USB 데이터 연결 및 ADB 드라이버 상태를 확인해 주세요.";
                        }
                        return;
                    }

                    string ip = txtIp.Text.Trim();
                    string portText = txtPort.Text.Trim();
                    if (!IPAddress.TryParse(ip, out _) ||
                        !int.TryParse(portText, out int port) ||
                        port < 1 || port > 65535)
                    {
                        lblConnectionError.Text = "올바른 IP 주소와 1~65535 범위의 포트를 입력해 주세요.";
                        return;
                    }

                    string endpoint = $"{ip}:{port}";
                    string result = await Task.Run(() =>
                        AdbEngine.RunCommand($"connect {endpoint}", 10000));
                    bool connectedToEndpoint = await Task.Run(() =>
                        AdbEngine.IsEndpointConnected(endpoint));

                    if (connectedToEndpoint)
                    {
                        AdbEngine.SetSelectedSerial(endpoint);
                        DeviceSelectionStore.Save(endpoint);
                        EnterMainWorkspace();
                        return;
                    }

                    string detail = string.IsNullOrWhiteSpace(result)
                        ? "ADB에서 응답을 받지 못했습니다."
                        : result.Trim();
                    lblConnectionError.Text = "무선 연결에 실패했습니다. " + detail;
                }
                catch (Exception ex)
                {
                    lblConnectionError.Text = "연결 확인 중 오류가 발생했습니다. " + ex.Message;
                }
                finally
                {
                    btnIpConn.Enabled = true;
                    btnIpConn.Text = originalText;
                    btnIpConn.IconName = originalIcon;
                    btnIpConn.Invalidate();
                }
            };

            pnlConnCard.Controls.AddRange(new Control[]
            {
                brandIcon,
                lblProduct,
                lblProductMeta,
                btnLocalTcEntry,
                connDivider,
                lblConnTitle,
                lblConnDescription,
                visualCard,
                btnWired,
                btnWireless,
                pnlWirelessFields,
                btnIpConn,
                lblConnectionError,
                helperIcon,
                lblConnectionHelper
            });
            connCenterGrid.Controls.Add(pnlConnCard, 1, 1);
            pnlConnect.Controls.Add(connCenterGrid);

            // ===== 메인 화면 =====
            pnlMain = new DoubleBufferedPanel
            {
                Dock = DockStyle.Fill,
                Visible = false,
                BackColor = Globals.Bg
            };
            pnlSidebar = new DoubleBufferedPanel
            {
                Width = Globals.SidebarWidth,
                Dock = DockStyle.Left,
                BackColor = Globals.Sidebar
            };

            var sidebarGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Globals.Sidebar
            };
            sidebarGrid.RowStyles.Add(Abs(82));
            sidebarGrid.RowStyles.Add(Pct(100));
            sidebarGrid.RowStyles.Add(Abs(154));
            sidebarGrid.ColumnStyles.Add(ColPct(100));

            var brandRow = new Panel { Dock = DockStyle.Fill, BackColor = Globals.Sidebar };
            var sidebarBrandIcon = new IconGlyph
            {
                IconName = "appium",
                IconColor = Globals.Accent,
                Location = new Point(18, 23),
                Size = new Size(26, 26)
            };
            var lblBrand = new Label
            {
                Text = "Appium Builder\nReborn",
                ForeColor = Globals.TextPrimary,
                Font = new Font("Malgun Gothic", 10.5F, FontStyle.Bold),
                Location = new Point(50, 15),
                Size = new Size(140, 46),
                TextAlign = ContentAlignment.MiddleLeft
            };
            var lblBrandSub = new Label
            {
                Text = string.Empty,
                Visible = false,
                Location = new Point(0, 0),
                Size = Size.Empty
            };
            brandRow.Controls.Add(sidebarBrandIcon);
            brandRow.Controls.Add(lblBrand);
            brandRow.Controls.Add(lblBrandSub);

            var navPanel = new Panel { Dock = DockStyle.Fill, BackColor = Globals.Sidebar };
            navIndicator = new RoundedPanel
            {
                Size = new Size(3, Globals.MenuHeight),
                Location = new Point(0, 0),
                FillColor = Globals.Accent,
                BorderRadius = 1,
                BorderThickness = 0
            };
            btnTabHome = CreateMenuButton("홈", "home", 0);
            btnTabLog = CreateMenuButton("로그/미디어", "terminal", 48);
            btnTabUtil = CreateMenuButton("유틸리티", "tools", 96);
            btnTabAuto = CreateMenuButton("Appium 봇", "appium", 144);
            var btnLocalTc = CreateMenuButton("로컬 TC", "archive", 192);
            btnLocalTc.Click += (_, _) =>
            {
                using var form = new LocalTestCaseBuilderForm();
                form.ShowDialog(this);
            };
            navPanel.Controls.AddRange(new Control[]
            {
                btnTabHome,
                btnTabLog,
                btnTabUtil,
                btnTabAuto,
                btnLocalTc,
                navIndicator
            });
            navIndicator.BringToFront();

            var footerPanel = new Panel { Dock = DockStyle.Fill, BackColor = Globals.Sidebar };
            var footerDivider = new Panel
            {
                Location = new Point(16, 0),
                Size = new Size(168, 1),
                BackColor = Globals.SidebarBorder
            };
            var dotConn = Dot(Globals.Danger, 7);
            dotConn.Location = new Point(18, 18);
            var lblConnLabel = new Label
            {
                Text = "기기 확인 중...",
                Location = new Point(34, 10),
                Font = Globals.FontMuted,
                ForeColor = Globals.SidebarTextMuted,
                AutoSize = true
            };
            lblSideModel = new Label
            {
                Text = "상태를 가져오는 중...",
                Location = new Point(18, 36),
                Font = Globals.FontMuted,
                ForeColor = Globals.SidebarTextMuted,
                AutoSize = false,
                Size = new Size(166, 40),
                AutoEllipsis = true
            };

            var btnBack = new RoundedButton
            {
                Text = "연결 해제",
                IconName = "disconnect",
                IconColor = Globals.SidebarTextMuted,
                Location = new Point(16, 84),
                Size = new Size(168, 40),
                FillColor = Globals.Sidebar,
                HoverColor = Globals.SurfaceAlt,
                PressedColor = Globals.SurfaceRaised,
                BorderColor = Globals.SidebarBorder,
                BorderThickness = 1,
                ForeColor = Globals.SidebarTextMuted,
                Font = Globals.FontMuted,
                BorderRadius = Globals.RadiusSm,
                TextAlign = ContentAlignment.MiddleLeft,
                HorizontalPadding = 12
            };
            btnBack.Click += (_, _) =>
            {
                string? selected = AdbEngine.SelectedSerial;
                if (!string.IsNullOrWhiteSpace(selected) && selected.Contains(':'))
                    _ = Task.Run(() => AdbEngine.RunGlobalCommand($"disconnect \"{selected}\"", 5000));
                AdbEngine.SetSelectedSerial(null);
                DeviceSelectionStore.Save(null);
                pnlConnect.Visible = true;
                pnlMain.Visible = false;
                statusTimer?.Stop();
                ApplyConnectionMode(false);
            };
            footerPanel.Controls.AddRange(new Control[]
            {
                footerDivider,
                dotConn,
                lblConnLabel,
                lblSideModel,
                btnBack
            });

            sidebarGrid.Controls.Add(brandRow, 0, 0);
            sidebarGrid.Controls.Add(navPanel, 0, 1);
            sidebarGrid.Controls.Add(footerPanel, 0, 2);
            pnlSidebar.Controls.Add(sidebarGrid);

            pnlContent = new DoubleBufferedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Globals.Bg
            };
            var sidebarRightBorder = new Panel
            {
                Width = 1,
                Dock = DockStyle.Right,
                BackColor = Globals.SidebarBorder
            };
            pnlSidebar.Controls.Add(sidebarRightBorder);
            sidebarRightBorder.BringToFront();

            pnlMain.Controls.Add(pnlContent);
            pnlMain.Controls.Add(pnlSidebar);

            var workspaceHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Globals.Bg,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            workspaceHost.Controls.Add(pnlConnect);
            workspaceHost.Controls.Add(pnlMain);

            var appRoot = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Globals.Bg,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            appRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            appRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            appRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, Globals.FooterHeight));
            appRoot.Controls.Add(workspaceHost, 0, 0);
            appRoot.Controls.Add(pnlFooter, 0, 1);
            Controls.Add(appRoot);

            // R9 Responsive UI: 1280px 미만에서는 사이드바가 아이콘 전용 모드로 접힌다.
            RegisterResponsiveShell(
                sidebarBrandIcon,
                lblBrand,
                footerDivider,
                dotConn,
                lblConnLabel,
                btnBack,
                footerAccent);
        }

        internal class IconGlyph : Control
        {
            private string _iconName = string.Empty;
            private Color _iconColor = Globals.TextSecondary;

            public string IconName
            {
                get => _iconName;
                set { _iconName = value; Invalidate(); }
            }

            public Color IconColor
            {
                get => _iconColor;
                set { _iconColor = value; Invalidate(); }
            }

            public IconGlyph()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                float size = Math.Min(Width, Height) * 0.72f;
                if (size < 8f) size = Math.Min(Width, Height);
                var rect = new RectangleF((Width - size) / 2f, (Height - size) / 2f, size, size);
                LineIcons.Draw(e.Graphics, IconName, rect, IconColor);
            }
        }
    }
}
