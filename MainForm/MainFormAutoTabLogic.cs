using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using AppiumBuilder.Utils;

namespace AppiumBuilder
{
    public partial class MainForm
    {
        private void LstSteps_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            string rawText = lstSteps.Items[e.Index].ToString() ?? string.Empty;
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color bg = selected ? Globals.AccentSoft : (e.Index % 2 == 0 ? Globals.Surface : Globals.SurfaceAlt);
            using (var bgBrush = new SolidBrush(bg)) e.Graphics.FillRectangle(bgBrush, e.Bounds);

            (string action, string target, string description, Color accent) = ParseStepForGrid(rawText);
            int left = e.Bounds.X + 4;
            int numberWidth = 42;
            int actionWidth = Math.Min(104, Math.Max(82, e.Bounds.Width / 5));
            int remaining = Math.Max(120, e.Bounds.Width - numberWidth - actionWidth - 16);
            int targetWidth = (int)(remaining * 0.48F);
            int descriptionWidth = Math.Max(60, remaining - targetWidth);

            var numberRect = new Rectangle(left, e.Bounds.Y, numberWidth, e.Bounds.Height);
            var actionRect = new Rectangle(numberRect.Right, e.Bounds.Y, actionWidth, e.Bounds.Height);
            var targetRect = new Rectangle(actionRect.Right, e.Bounds.Y, targetWidth, e.Bounds.Height);
            var descriptionRect = new Rectangle(targetRect.Right, e.Bounds.Y, descriptionWidth, e.Bounds.Height);

            using (var accentBrush = new SolidBrush(Color.FromArgb(28, accent)))
                e.Graphics.FillEllipse(accentBrush, actionRect.X + 1, e.Bounds.Y + Math.Max(0, (e.Bounds.Height - 22) / 2), 22, 22);
            using (var accentPen = new Pen(accent, 1.5F))
                e.Graphics.DrawEllipse(accentPen, actionRect.X + 4, e.Bounds.Y + Math.Max(0, (e.Bounds.Height - 16) / 2), 16, 16);

            TextRenderer.DrawText(
                e.Graphics,
                (e.Index + 1).ToString(),
                Globals.FontMuted,
                numberRect,
                selected ? Globals.Accent : Globals.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(
                e.Graphics,
                action,
                Globals.FontSub,
                new Rectangle(actionRect.X + 28, actionRect.Y, Math.Max(1, actionRect.Width - 28), actionRect.Height),
                selected ? Globals.AccentText : Globals.TextPrimary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(
                e.Graphics,
                target,
                Globals.FontMuted,
                targetRect,
                Globals.TextSecondary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(
                e.Graphics,
                description,
                Globals.FontMuted,
                descriptionRect,
                Globals.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            using var divider = new Pen(Globals.Border, 1F);
            e.Graphics.DrawLine(divider, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            if (selected) e.DrawFocusRectangle();
        }

        private static (string Action, string Target, string Description, Color Accent) ParseStepForGrid(string raw)
        {
            Match locatorStep = Regex.Match(raw, @"^\[(Click|Input|OTP|Assert)\] \[(.+?)\] (.+?)(?: \| 값:? ?(.*))?$");
            if (locatorStep.Success)
            {
                string kind = locatorStep.Groups[1].Value;
                string locator = locatorStep.Groups[2].Value;
                string target = locatorStep.Groups[3].Value;
                string value = locatorStep.Groups[4].Success ? locatorStep.Groups[4].Value : string.Empty;
                return kind switch
                {
                    "Click" => ("클릭", $"{locator} · {target}", "요소 클릭", Globals.Accent),
                    "Input" => ("입력", $"{locator} · {target}", value.Length > 0 ? "텍스트 입력" : "값 입력", Globals.Success),
                    "OTP" => ("OTP", $"{locator} · {target}", "인증번호 추출", Globals.Warning),
                    "Assert" => ("검증", $"{locator} · {target}", value.Length > 0 ? $"값 검증 · {value}" : "요소 검증", Globals.Info),
                    _ => (kind, target, string.Empty, Globals.Accent)
                };
            }

            Match simple = Regex.Match(raw, @"^\[(.+?)\] ?(.*)$");
            if (!simple.Success) return ("단계", raw, string.Empty, Globals.TextMuted);
            string kindSimple = simple.Groups[1].Value;
            string tail = simple.Groups[2].Value;
            return kindSimple switch
            {
                "Tap" => ("좌표 클릭", tail, "화면 좌표 클릭", Globals.Accent),
                "Swipe" => ("스크롤", tail, "화면 스와이프", Globals.Accent),
                "Key" => ("기기 키", tail, "Android 키 입력", Globals.Info),
                "Sleep" => ("대기", tail, "지정 시간 대기", Globals.TextMuted),
                "SecurePad" => ("보안키패드", tail, "보안 입력", Globals.Warning),
                "PhysicalKey" => ("물리키패드", tail, "물리 키 입력", Globals.Warning),
                "Notification" => ("알림창", tail, "알림 패널 제어", Globals.Info),
                "ScreenAssert" => ("화면 검증", tail, "Visual 기준 비교", Globals.Success),
                "RunPython" => ("Python", Path.GetFileName(tail), "외부 스크립트 실행", Globals.Warning),
                _ => (kindSimple, tail, string.Empty, Globals.TextMuted)
            };
        }

        // 현재 Manual Builder 입력창 상태를 기반으로 스텝 문자열 생성
        private string? BuildStepRow()
        {
            string act = cmbAction.Text; string loc = cmbLocator.Text;
            string tgt = GetText(txtTarget); string val = GetText(txtValue); string x = GetText(txtX); string y = GetText(txtY);

            return act switch
            {
                "좌표 클릭(XY)" => $"[Tap] {x}, {y}",
                "스크롤(Swipe)" => $"[Swipe] 시작:{x},{y} -> 도착:{val}",
                "기기 키(Key)" => $"[Key] 코드: {tgt}",
                "대기(Sleep)" => $"[Sleep] {tgt} 초",
                "클릭(Click)" => $"[Click] [{loc}] {tgt}",
                "OTP 추출(OTP)" => $"[OTP] [{loc}] {tgt}",
                "보안키패드(SecurePad)" => $"[SecurePad] 값: {tgt}",
                "물리키패드(Keypad)" => $"[PhysicalKey] 값: {tgt}",
                "입력(SendKeys)" => $"[Input] [{loc}] {tgt} | 값: {val}",
                "알림창(Notification)" => $"[Notification] 동작: {tgt}",
                "요소 검증(Assert)" => $"[Assert] [{loc}] {tgt} | 값:{val}",
                "전체 화면 검증(ScreenAssert)" => $"[ScreenAssert] 값: {tgt}",
                _ => null
            };
        }

        // 기존 스텝 문자열을 다시 입력창들에 채워 넣는다 (수정 모드 진입 시)
        private void LoadRowIntoEditor(string row)
        {
            string act = "";
            string loc = "XPath", tgt = "", val = "", x = "", y = "";

            if (row.StartsWith("[Tap]")) { act = "좌표 클릭(XY)"; var p = row.Replace("[Tap] ", "").Split(','); x = p[0].Trim(); y = p.Length > 1 ? p[1].Trim() : ""; }
            else if (row.StartsWith("[Swipe]")) { act = "스크롤(Swipe)"; var p = row.Replace("[Swipe] 시작:", "").Split(new[] { "-> 도착:", "," }, StringSplitOptions.None); if (p.Length >= 4) { x = p[0].Trim(); y = p[1].Trim(); val = p[2].Trim() + "," + p[3].Trim(); } }
            else if (row.StartsWith("[Key]")) { act = "기기 키(Key)"; tgt = AfterColon(row); }
            else if (row.StartsWith("[Sleep]")) { act = "대기(Sleep)"; tgt = row.Split(' ')[1]; }
            else if (row.StartsWith("[SecurePad]")) { act = "보안키패드(SecurePad)"; tgt = AfterColon(row); }
            else if (row.StartsWith("[PhysicalKey]")) { act = "물리키패드(Keypad)"; tgt = AfterColon(row); }
            else if (row.StartsWith("[Notification]")) { act = "알림창(Notification)"; tgt = AfterColon(row); }
            else if (row.StartsWith("[ScreenAssert]")) { act = "전체 화면 검증(ScreenAssert)"; tgt = AfterColon(row); }
            else if (row.StartsWith("[Click]")) { act = "클릭(Click)"; (loc, tgt, _) = ParseLocTgtVal(row); }
            else if (row.StartsWith("[OTP]")) { act = "OTP 추출(OTP)"; (loc, tgt, _) = ParseLocTgtVal(row); }
            else if (row.StartsWith("[Input]")) { act = "입력(SendKeys)"; (loc, tgt, val) = ParseLocTgtVal(row); }
            else if (row.StartsWith("[Assert]")) { act = "요소 검증(Assert)"; (loc, tgt, val) = ParseLocTgtVal(row); }

            cmbAction.Text = act;
            cmbLocator.Text = loc == "ID" ? "ID" : loc == "XPath" ? "XPath" : "Accessibility ID";
            SetEditorText(txtTarget, tgt);
            SetEditorText(txtValue, val);
            SetEditorText(txtX, x);
            SetEditorText(txtY, y);
        }

        private void SetEditorText(TextBox t, string val)
        {
            if (string.IsNullOrEmpty(val)) { t.Text = t.Tag?.ToString() ?? ""; t.ForeColor = Globals.TextFaint; }
            else { t.Text = val; t.ForeColor = Globals.TextPrimary; }
        }

        private static string AfterColon(string row)
        {
            int idx = row.IndexOf(':');
            return idx >= 0 ? row.Substring(idx + 1).Trim() : "";
        }

        private static (string loc, string tgt, string val) ParseLocTgtVal(string row)
        {
            int locOpen = row.IndexOf('[', 1);
            if (locOpen < 0) return ("XPath", row, "");
            int locClose = row.IndexOf(']', locOpen);
            if (locClose < 0) return ("XPath", row, "");
            string loc = row.Substring(locOpen + 1, locClose - locOpen - 1);
            string rest = row.Substring(locClose + 1).Trim();
            string tgt = rest, val = "";
            int barIdx = rest.IndexOf('|');
            if (barIdx >= 0)
            {
                tgt = rest.Substring(0, barIdx).Trim();
                string afterBar = rest.Substring(barIdx + 1).Trim();
                int colonIdx = afterBar.IndexOf(':');
                val = colonIdx >= 0 ? afterBar.Substring(colonIdx + 1).Trim() : afterBar;
            }
            return (loc, tgt, val);
        }


        private static List<string> ReadScenarioSteps(string path)
        {
            var steps = new List<string>();
            foreach (string rawLine in File.ReadAllLines(path, Encoding.UTF8))
            {
                string line = rawLine.TrimStart('\uFEFF').Trim();
                if (line.Length == 0) continue;

                if (line.StartsWith("\"") && line.EndsWith("\"") && line.Length >= 2)
                    line = line.Substring(1, line.Length - 2).Replace("\"\"", "\"");

                if (line.Equals("Step", StringComparison.OrdinalIgnoreCase)) continue;
                steps.Add(line);
            }
            return steps;
        }

        private void ResetToAddMode()
        {
            editingIndex = -1;
            lstSteps.ClearSelected();
            btnAddStep.Visible = true;
            btnEditStep.Visible = false;
            btnCancelEdit.Visible = false;
            btnDelStep.Visible = false;
            RelayoutBuilderRef?.Invoke();
            pnlTabAuto.Refresh();
        }

        // 간단한 규칙 기반 한국어 명령 -> 스텝 변환. Gemini 호출이 실패하거나(네트워크 등) 기기 미연결일 때
        // btnAiAnalyze.Click(MainFormAutoTab.cs)에서 오프라인 폴백으로 실제 호출된다.
        // 실제 화면(XML) 기반 요소 매칭이 아니라 텍스트 매칭(XPath //*[@text=...])으로 최선 추정한다.
        private System.Collections.Generic.List<string> AnalyzePromptToSteps(string prompt)
        {
            var steps = new System.Collections.Generic.List<string>();
            var clauses = Regex.Split(prompt, "(?:하고|,|그리고|한\\s*다음|한\\s*후)");

            foreach (var raw in clauses)
            {
                string clause = raw.Trim();
                if (clause.Length == 0) continue;

                Match m;

                // "하단탭에서", "메인 화면에서" 같은 위치/문맥 설명은 버튼 이름이 아니므로 먼저 제거
                clause = Regex.Replace(clause, @"^.+?에서\s*", "");

                if (Regex.IsMatch(clause, "뒤로\\s*가기")) { steps.Add("[Key] 코드: 4"); continue; }

                m = Regex.Match(clause, @"(\d+)\s*초\s*(?:간)?\s*(?:대기|기다)");
                if (m.Success) { steps.Add($"[Sleep] {m.Groups[1].Value} 초"); continue; }

                m = Regex.Match(clause, @"(?:화면에\s*)?['""]?(.+?)['""]?\s*(?:이|가)?\s*(?:있는지\s*확인|보이는지\s*확인|나타나는지\s*확인|검증)");
                if (m.Success) { steps.Add($"[ScreenAssert] 값: {m.Groups[1].Value.Trim()}"); continue; }

                m = Regex.Match(clause, @"['""]?(.+?)['""]?\s*(?:입력창|필드)에\s*['""]?(.+?)['""]?\s*(?:을|를)?\s*입력");
                if (m.Success)
                {
                    string field = m.Groups[1].Value.Trim();
                    string text = m.Groups[2].Value.Trim();
                    steps.Add($"[Input] [XPath] //*[@text='{field}' or @content-desc='{field}' or @resource-id[contains(.,'{field}')]] | 값: {text}");
                    continue;
                }

                m = Regex.Match(clause, @"['""]?(.+?)['""]?\s*(?:클릭|누르)?\s*버튼\s*(?:을|를)?\s*(?:누르|클릭|터치)?");
                if (m.Success)
                {
                    string name = m.Groups[1].Value.Trim();
                    steps.Add($"[Click] [XPath] //*[@text='{name}' or @content-desc='{name}']");
                    continue;
                }
            }

            return steps;
        }
    }
}
