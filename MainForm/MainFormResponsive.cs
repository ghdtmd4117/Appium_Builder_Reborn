using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AppiumBuilder.UI;
using AppiumBuilder.Utils;

namespace AppiumBuilder
{
    public partial class MainForm
    {
        private IconGlyph? responsiveSidebarBrandIcon;
        private Label? responsiveSidebarBrandLabel;
        private Panel? responsiveSidebarFooterDivider;
        private RoundedPanel? responsiveSidebarConnectionDot;
        private Label? responsiveSidebarConnectionLabel;
        private RoundedButton? responsiveSidebarBackButton;
        private RoundedPanel? responsiveFooterAccent;
        private bool responsiveSidebarCompact;
        private int responsiveShellDpi = -1;

        /// <summary>
        /// 96 DPI 기준 논리 픽셀을 현재 모니터 DPI에 맞춘다.
        /// Resize 이벤트에서 RowStyle을 다시 설정할 때 WinForms AutoScale과 따로 놀지 않게 하기 위한 값이다.
        /// </summary>
        private int Ui(int logicalPixels)
        {
            int dpi = DeviceDpi <= 0 ? 96 : DeviceDpi;
            return Math.Max(1, (int)Math.Round(logicalPixels * dpi / 96d));
        }

        private void RegisterResponsiveShell(
            IconGlyph brandIcon,
            Label brandLabel,
            Panel footerDivider,
            RoundedPanel connectionDot,
            Label connectionLabel,
            RoundedButton backButton,
            RoundedPanel footerAccent)
        {
            responsiveSidebarBrandIcon = brandIcon;
            responsiveSidebarBrandLabel = brandLabel;
            responsiveSidebarFooterDivider = footerDivider;
            responsiveSidebarConnectionDot = connectionDot;
            responsiveSidebarConnectionLabel = connectionLabel;
            responsiveSidebarBackButton = backButton;
            responsiveFooterAccent = footerAccent;

            SizeChanged += (_, _) => ApplyResponsiveShellLayout();
            Shown += (_, _) => ApplyResponsiveShellLayout();
            DpiChanged += (_, _) => BeginInvoke(new Action(ApplyResponsiveShellLayout));
            ApplyResponsiveShellLayout();
        }

        /// <summary>
        /// 창이 좁아질 때 사이드바를 아이콘 전용 모드로 접어 실제 작업 공간을 확보한다.
        /// 1280 논리 px 이상에서는 기존 전체 사이드바를 사용한다.
        /// </summary>
        private void ApplyResponsiveShellLayout()
        {
            if (pnlSidebar == null || pnlSidebar.IsDisposed || btnTabHome == null) return;

            bool compact = ClientSize.Width < Ui(1280);
            bool dpiChanged = responsiveShellDpi != DeviceDpi;
            if (responsiveSidebarCompact != compact || dpiChanged)
            {
                responsiveSidebarCompact = compact;
                responsiveShellDpi = DeviceDpi;
                pnlSidebar.SuspendLayout();

                int sidebarWidth = compact ? Ui(76) : Ui(Globals.SidebarWidth);
                pnlSidebar.Width = sidebarWidth;

                if (responsiveSidebarBrandIcon != null)
                {
                    responsiveSidebarBrandIcon.Size = new Size(Ui(26), Ui(26));
                    responsiveSidebarBrandIcon.Location = compact
                        ? new Point((sidebarWidth - responsiveSidebarBrandIcon.Width) / 2, Ui(23))
                        : new Point(Ui(18), Ui(23));
                }

                if (responsiveSidebarBrandLabel != null)
                    responsiveSidebarBrandLabel.Visible = !compact;

                ConfigureMenuButton(btnTabHome, compact, "홈", "home", 0);
                ConfigureMenuButton(btnTabLog, compact, "로그/미디어", "terminal", 48);
                ConfigureMenuButton(btnTabUtil, compact, "유틸리티", "tools", 96);
                ConfigureMenuButton(btnTabAuto, compact, "Appium 봇", "appium", 144);

                if (navIndicator != null)
                {
                    navIndicator.Width = Ui(3);
                    navIndicator.Height = Ui(Globals.MenuHeight);
                    navIndicator.Left = 0;
                }

                if (responsiveSidebarFooterDivider != null)
                {
                    responsiveSidebarFooterDivider.Left = Ui(16);
                    responsiveSidebarFooterDivider.Width = compact ? Ui(44) : Ui(168);
                }

                if (responsiveSidebarConnectionDot != null)
                    responsiveSidebarConnectionDot.Location = compact
                        ? new Point((sidebarWidth - Ui(7)) / 2, Ui(18))
                        : new Point(Ui(18), Ui(18));

                if (responsiveSidebarConnectionLabel != null)
                    responsiveSidebarConnectionLabel.Visible = !compact;
                if (lblSideModel != null)
                    lblSideModel.Visible = !compact;

                if (responsiveSidebarBackButton != null)
                {
                    responsiveSidebarBackButton.Text = compact ? string.Empty : "연결 해제";
                    responsiveSidebarBackButton.TextAlign = compact ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft;
                    responsiveSidebarBackButton.HorizontalPadding = compact ? 0 : Ui(12);
                    responsiveSidebarBackButton.Location = compact
                        ? new Point(Ui(12), Ui(84))
                        : new Point(Ui(16), Ui(84));
                    responsiveSidebarBackButton.Size = compact
                        ? new Size(Ui(52), Ui(40))
                        : new Size(Ui(168), Ui(40));
                }

                pnlSidebar.ResumeLayout(true);
            }

            // 하단 상태바도 접힌 사이드바 폭에 맞춰 항상 남은 공간을 사용한다.
            int currentSidebarWidth = pnlSidebar.Width;
            if (responsiveFooterAccent != null)
                responsiveFooterAccent.Location = new Point(currentSidebarWidth + Ui(18), Ui(12));
            if (lblStatusMsg != null)
            {
                lblStatusMsg.Left = currentSidebarWidth + Ui(32);
                lblStatusMsg.Top = Ui(6);
                lblStatusMsg.Width = Math.Max(Ui(220), ClientSize.Width - lblStatusMsg.Left - Ui(18));
                lblStatusMsg.Height = Ui(20);
            }
        }

        private void ConfigureMenuButton(RoundedButton button, bool compact, string fullText, string icon, int logicalTop)
        {
            if (button == null) return;
            button.Text = compact ? string.Empty : fullText;
            button.IconName = icon;
            button.Location = new Point(Ui(8), Ui(logicalTop));
            button.Size = compact
                ? new Size(Ui(60), Ui(Globals.MenuHeight))
                : new Size(Math.Max(Ui(140), pnlSidebar.Width - Ui(16)), Ui(Globals.MenuHeight));
            button.TextAlign = compact ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft;
            button.HorizontalPadding = compact ? 0 : Ui(14);
            button.IconSize = compact ? Ui(19) : Ui(17);
        }

        /// <summary>
        /// 같은 카드 묶음을 창 폭에 따라 N열로 재배치한다. Control 인스턴스는 그대로 유지하므로 이벤트/상태가 보존된다.
        /// </summary>
        private void ReflowEqualGrid(TableLayoutPanel grid, int columns, IReadOnlyList<Control> controls)
        {
            columns = Math.Max(1, columns);
            int rows = Math.Max(1, (int)Math.Ceiling(controls.Count / (double)columns));

            grid.SuspendLayout();
            grid.Controls.Clear();
            grid.ColumnStyles.Clear();
            grid.RowStyles.Clear();
            grid.ColumnCount = columns;
            grid.RowCount = rows;

            for (int column = 0; column < columns; column++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
            for (int row = 0; row < rows; row++)
                grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));

            for (int i = 0; i < controls.Count; i++)
                grid.Controls.Add(controls[i], i % columns, i / columns);

            grid.ResumeLayout(true);
        }

        private void ReflowWeightedGrid(TableLayoutPanel grid, IReadOnlyList<Control> controls, params float[] columnWeights)
        {
            if (columnWeights == null || columnWeights.Length == 0)
            {
                ReflowEqualGrid(grid, Math.Max(1, controls.Count), controls);
                return;
            }

            float total = columnWeights.Sum();
            if (total <= 0f) total = columnWeights.Length;
            grid.SuspendLayout();
            grid.Controls.Clear();
            grid.ColumnStyles.Clear();
            grid.RowStyles.Clear();
            grid.ColumnCount = columnWeights.Length;
            grid.RowCount = 1;
            foreach (float weight in columnWeights)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, Math.Max(0.01f, weight) * 100f / total));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            for (int i = 0; i < controls.Count && i < columnWeights.Length; i++)
                grid.Controls.Add(controls[i], i, 0);
            grid.ResumeLayout(true);
        }

        private void SetAbsoluteRow(TableLayoutPanel table, int index, int logicalHeight)
        {
            if (index < 0 || index >= table.RowStyles.Count) return;
            table.RowStyles[index].SizeType = SizeType.Absolute;
            table.RowStyles[index].Height = Ui(logicalHeight);
        }

        private void SetPercentRow(TableLayoutPanel table, int index, float percent = 100f)
        {
            if (index < 0 || index >= table.RowStyles.Count) return;
            table.RowStyles[index].SizeType = SizeType.Percent;
            table.RowStyles[index].Height = percent;
        }

        private void SetResponsivePageMode(Panel page, TableLayoutPanel root, bool scrollMode, int logicalContentHeight)
        {
            page.AutoScroll = true;
            root.AutoSize = false;
            if (scrollMode)
            {
                root.Dock = DockStyle.Top;
                root.Height = Ui(logicalContentHeight);
                root.MinimumSize = new Size(0, Ui(logicalContentHeight));
            }
            else
            {
                root.MinimumSize = Size.Empty;
                root.Dock = DockStyle.Fill;
            }
        }
    }
}
