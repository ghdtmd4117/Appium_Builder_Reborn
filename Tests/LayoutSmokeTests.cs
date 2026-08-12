using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Forms;
using AppiumBuilder;
using AppiumBuilder.UI;
using AppiumBuilder.Utils;
using Xunit;

namespace AppiumBuilder.Tests;

public sealed class LayoutSmokeTests
{
    [Fact]
    public void MainTabs_DoNotClipTextOrCollapseInteractiveControls_AtSupportedWindowSizes()
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "AppiumBuilderTests", Guid.NewGuid().ToString("N"));
            string previousLogFolder = Globals.LogFolder;
            string previousScenarioFolder = Globals.ScenarioFolder;
            try
            {
                Globals.LogFolder = tempRoot;
                Globals.ScenarioFolder = Path.Combine(tempRoot, "Scenarios");
                using var form = new MainForm
                {
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(-32000, -32000),
                    ShowInTaskbar = false
                };

                // Control.Visible은 부모 Form이 실제로 표시되어야 활성 탭의 자식 컨트롤도 true가 된다.
                // 테스트 창은 화면 밖에 표시하여 사용자를 방해하지 않으면서 실제 WinForms 레이아웃을 검증한다.
                form.Show();
                Application.DoEvents();
                foreach (Size size in new[]
                {
                    new Size(1024, 680),
                    new Size(1152, 720),
                    new Size(1366, 820),
                    new Size(1600, 900),
                    new Size(1920, 1080)
                })
                {
                    form.Size = size;
                    foreach (string tabName in new[] { "Home", "Log", "Util", "Auto" })
                    {
                        ActivateTab(form, tabName);
                        form.PerformLayout();
                        Application.DoEvents();

                        var sidebar = (Panel?)typeof(MainForm).GetField("pnlSidebar", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(form);
                        Assert.NotNull(sidebar);
                        if (size.Width <= 1152)
                            Assert.True(sidebar!.Width < 120, $"Responsive sidebar should collapse at {size.Width}px, actual={sidebar.Width}px");

                        foreach (Label label in Descendants(form).OfType<Label>().Where(l => l.Visible && !string.IsNullOrWhiteSpace(l.Text)))
                        {
                            // 상태/오류 라벨처럼 긴 텍스트는 AutoEllipsis로 수평 축약할 수 있지만
                            // 세로 높이 자체가 한 줄보다 작아지는 것은 허용하지 않는다.
                            Size preferred = label.GetPreferredSize(new Size(Math.Max(1, label.Width), 0));
                            int minimumUsefulHeight = label.Text.Contains('\n') || label.Text.Contains('\r')
                                ? Math.Min(preferred.Height, 54)
                                : Math.Min(preferred.Height, 28);
                            Assert.True(
                                label.Height + 4 >= minimumUsefulHeight,
                                $"Label clipping suspect: '{label.Text}' ({label.Width}x{label.Height}, preferred {preferred})");
                        }

                        foreach (RoundedButton button in Descendants(form).OfType<RoundedButton>().Where(b => b.Visible))
                        {
                            Assert.True(button.Height >= 30, $"Button too short: '{button.Text}' ({button.Width}x{button.Height})");
                            if (!string.IsNullOrWhiteSpace(button.Text) && (button.Text.Contains('\n') || button.Text.Contains('\r')))
                            {
                                Assert.True(button.Height >= 48, $"Multiline button too short: '{button.Text}' ({button.Width}x{button.Height})");
                            }
                            else if (!string.IsNullOrWhiteSpace(button.Text))
                            {
                                Size measured = TextRenderer.MeasureText(
                                    button.Text,
                                    button.Font,
                                    new Size(int.MaxValue, Math.Max(1, button.Height)),
                                    TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                                int iconWidth = string.IsNullOrWhiteSpace(button.IconName) ? 0 : Math.Max(12, button.IconSize);
                                int iconGap = iconWidth > 0 ? Math.Max(0, button.IconGap) : 0;
                                int required = measured.Width + iconWidth + iconGap + (Math.Max(0, button.HorizontalPadding) * 2);
                                Assert.True(
                                    button.Width + 2 >= required,
                                    $"Button text clipping suspect: '{button.Text}' ({button.Width}px, requires about {required}px at actual font)");
                            }
                        }

                        foreach (TextBox input in Descendants(form).OfType<TextBox>().Where(t => t.Visible && !t.Multiline))
                            Assert.True(input.Height >= 30, $"Input too short: '{input.Text}' ({input.Width}x{input.Height})");

                        foreach (ComboBox combo in Descendants(form).OfType<ComboBox>().Where(c => c.Visible))
                            Assert.True(combo.Height >= 28, $"ComboBox too short: '{combo.Text}' ({combo.Width}x{combo.Height})");

                        if (tabName == "Auto")
                        {
                            Type mainType = typeof(MainForm);
                            Panel? autoPanel = (Panel?)mainType.GetField("pnlTabAuto", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(form);
                            RoundedButton? serverButton = (RoundedButton?)mainType.GetField("btnAppiumServerToggle", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(form);
                            RoundedButton? terminalButton = (RoundedButton?)mainType.GetField("btnAppiumTerminal", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(form);
                            Label? appiumHint = autoPanel == null
                                ? null
                                : Descendants(autoPanel).OfType<Label>()
                                    .FirstOrDefault(l => l.Text.StartsWith("Appium Server가 실행 중이어야", StringComparison.Ordinal));

                            Assert.NotNull(autoPanel);
                            Assert.NotNull(appiumHint);
                            Assert.NotNull(serverButton);
                            Assert.NotNull(terminalButton);
                            Assert.True(autoPanel!.Visible, "Appium Bot tab should be visible after activation.");
                            Assert.True(appiumHint!.Visible, "Appium server hint should be visible on the active Appium Bot tab.");
                            Assert.True(serverButton!.Visible, "Appium server toggle button should be visible on the active Appium Bot tab.");
                            Assert.True(terminalButton!.Visible, "Appium terminal button should be visible on the active Appium Bot tab.");

                            Rectangle hintScreen = appiumHint.RectangleToScreen(appiumHint.ClientRectangle);
                            Rectangle serverScreen = serverButton.RectangleToScreen(serverButton.ClientRectangle);
                            Rectangle terminalScreen = terminalButton.RectangleToScreen(terminalButton.ClientRectangle);

                            Assert.True(hintScreen.Top >= Math.Min(serverScreen.Bottom, terminalScreen.Bottom) - 2,
                                "Appium server hint must be placed on a second row below the server controls.");
                            Assert.True(Math.Abs(serverScreen.Top - terminalScreen.Top) <= 3,
                                "Appium server control buttons must share the same top alignment.");
                            Assert.True(Math.Abs(serverScreen.Height - terminalScreen.Height) <= 3,
                                "Appium server control buttons must share the same height.");
                        }
                    }
                }
                form.Close();
            }
            catch (Exception ex) { captured = ex; }
            finally
            {
                Globals.LogFolder = previousLogFolder;
                Globals.ScenarioFolder = previousScenarioFolder;
                try { Directory.Delete(tempRoot, true); } catch { }
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (captured != null) ExceptionDispatchInfo.Capture(captured).Throw();
    }

    private static void ActivateTab(MainForm form, string suffix)
    {
        Type type = typeof(MainForm);
        var panel = (Panel?)type.GetField("pnlTab" + suffix, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(form);
        var button = (Control?)type.GetField("btnTab" + suffix, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(form);
        MethodInfo? switchTab = type.GetMethod("SwitchTab", BindingFlags.Instance | BindingFlags.NonPublic);
        if (panel != null && button != null && switchTab != null) switchTab.Invoke(form, new object[] { panel, button });
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control nested in Descendants(child)) yield return nested;
        }
    }
}
