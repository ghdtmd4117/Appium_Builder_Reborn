using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using AppiumBuilder.Core;
using AppiumBuilder.Utils;

namespace AppiumBuilder
{
    public partial class MainForm
    {
        private int lastAiConfidence;
        private string lastAiSummary = string.Empty;
        private IReadOnlyList<string> lastAiWarnings = Array.Empty<string>();

        private string GetOrAskGeminiKey()
        {
            string encryptedPath = Path.Combine(Globals.LogFolder, "gemini_key.dat");
            string legacyPlaintextPath = Path.Combine(Globals.LogFolder, "gemini_key.txt");

            try
            {
                if (File.Exists(encryptedPath))
                {
                    string protectedValue = File.ReadAllText(encryptedPath, Encoding.UTF8).Trim();
                    if (!string.IsNullOrWhiteSpace(protectedValue))
                        return SecretStore.UnprotectFromBase64(protectedValue).Trim();
                }

                if (File.Exists(legacyPlaintextPath))
                {
                    string legacyValue = File.ReadAllText(legacyPlaintextPath, Encoding.UTF8).Trim();
                    if (!string.IsNullOrWhiteSpace(legacyValue))
                    {
                        File.WriteAllText(encryptedPath, SecretStore.ProtectToBase64(legacyValue), Encoding.UTF8);
                        File.Delete(legacyPlaintextPath);
                        return legacyValue;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("저장된 Gemini API 키를 읽지 못했습니다. 다시 입력해주세요.\n" + ex.Message, "API 키 복호화 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            string input = ShowInputDialog(
                "Gemini API 키를 입력하세요.\n(입력값은 Windows 계정에 암호화되어 저장됩니다.)",
                "Gemini API 키 설정");
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            string trimmed = input.Trim();
            File.WriteAllText(encryptedPath, SecretStore.ProtectToBase64(trimmed), Encoding.UTF8);
            return trimmed;
        }

        private sealed class GeminiStepDto
        {
            public string action { get; set; } = string.Empty;
            public string locatorType { get; set; } = "XPath";
            public string target { get; set; } = string.Empty;
            public string value { get; set; } = string.Empty;
            public int seconds { get; set; }
        }

        private sealed class GeminiPlanDto
        {
            public int confidence { get; set; }
            public string summary { get; set; } = string.Empty;
            public List<string> warnings { get; set; } = new();
            public List<GeminiStepDto> steps { get; set; } = new();
        }

        private sealed class AiUiContext
        {
            public string CompactText { get; set; } = string.Empty;
            public HashSet<string> ResourceIds { get; } = new(StringComparer.Ordinal);
            public HashSet<string> AccessibilityLabels { get; } = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> VisibleTexts { get; } = new(StringComparer.OrdinalIgnoreCase);
        }

        private string? GeminiStepToRow(GeminiStepDto step)
        {
            string locator = step.locatorType switch
            {
                "ID" => "ID",
                "AccessibilityID" => "Accessibility ID",
                "Accessibility ID" => "Accessibility ID",
                _ => "XPath"
            };

            return step.action switch
            {
                "Click" => $"[Click] [{locator}] {step.target}",
                "Input" => $"[Input] [{locator}] {step.target} | 값: {step.value}",
                "Sleep" => $"[Sleep] {step.seconds} 초",
                "Assert" => $"[Assert] [{locator}] {step.target} | 값:{step.value}",
                "ScreenAssert" => $"[ScreenAssert] 값: {step.value}",
                _ => null
            };
        }

        private static string RedactUiDumpForAi(string xml)
        {
            if (string.IsNullOrEmpty(xml)) return xml;
            string redacted = xml;
            redacted = Regex.Replace(redacted, @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", "[REDACTED_EMAIL]", RegexOptions.IgnoreCase);
            redacted = Regex.Replace(redacted, @"(?<!\d)(?:\+?82[- ]?)?0?1[016789][- ]?\d{3,4}[- ]?\d{4}(?!\d)", "[REDACTED_PHONE]");
            redacted = Regex.Replace(redacted, @"(?<![A-Za-z0-9])\d{8,}(?![A-Za-z0-9])", "[REDACTED_NUMBER]");
            return redacted;
        }

        private static string SafeUiValue(string? value, int maxLength = 160)
        {
            string text = (value ?? string.Empty)
                .Replace("\r", " ", StringComparison.Ordinal)
                .Replace("\n", " ", StringComparison.Ordinal)
                .Replace("|", "/", StringComparison.Ordinal)
                .Trim();
            while (text.Contains("  ", StringComparison.Ordinal)) text = text.Replace("  ", " ", StringComparison.Ordinal);
            return text.Length > maxLength ? text[..maxLength] + "…" : text;
        }

        private static AiUiContext BuildCompactUiContext(string rawXml)
        {
            string xml = RedactUiDumpForAi(rawXml);
            XDocument document = XDocument.Parse(xml, LoadOptions.None);
            var context = new AiUiContext();

            var nodes = document.Descendants("node")
                .Select(node => new
                {
                    Id = SafeUiValue((string?)node.Attribute("resource-id")),
                    Text = SafeUiValue((string?)node.Attribute("text")),
                    Desc = SafeUiValue((string?)node.Attribute("content-desc")),
                    Class = SafeUiValue((string?)node.Attribute("class"), 100),
                    Bounds = SafeUiValue((string?)node.Attribute("bounds"), 80),
                    Package = SafeUiValue((string?)node.Attribute("package"), 100),
                    Clickable = string.Equals((string?)node.Attribute("clickable"), "true", StringComparison.OrdinalIgnoreCase),
                    Enabled = !string.Equals((string?)node.Attribute("enabled"), "false", StringComparison.OrdinalIgnoreCase),
                    Focusable = string.Equals((string?)node.Attribute("focusable"), "true", StringComparison.OrdinalIgnoreCase),
                    Scrollable = string.Equals((string?)node.Attribute("scrollable"), "true", StringComparison.OrdinalIgnoreCase)
                })
                .Where(x => x.Enabled && (x.Clickable || x.Focusable || x.Scrollable || x.Id.Length > 0 || x.Text.Length > 0 || x.Desc.Length > 0))
                .OrderByDescending(x => x.Clickable || x.Focusable)
                .ThenByDescending(x => x.Id.Length > 0)
                .Take(120)
                .ToList();

            foreach (var node in nodes)
            {
                if (node.Id.Length > 0) context.ResourceIds.Add(node.Id);
                if (node.Text.Length > 0) context.VisibleTexts.Add(node.Text);
                if (node.Desc.Length > 0) context.AccessibilityLabels.Add(node.Desc);
            }

            var sb = new StringBuilder();
            string package = nodes.Select(x => x.Package).FirstOrDefault(x => x.Length > 0) ?? "unknown";
            sb.AppendLine($"package={package}");
            sb.AppendLine($"visible_nodes={nodes.Count}");
            sb.AppendLine("locator_priority=resource-id > content-desc > text XPath");

            int index = 1;
            foreach (var node in nodes)
            {
                sb.Append('#').Append(index++).Append(' ');
                if (node.Id.Length > 0) sb.Append("id=").Append(node.Id).Append(" | ");
                if (node.Text.Length > 0) sb.Append("text=").Append(node.Text).Append(" | ");
                if (node.Desc.Length > 0) sb.Append("desc=").Append(node.Desc).Append(" | ");
                sb.Append("class=").Append(node.Class);
                sb.Append(" | clickable=").Append(node.Clickable ? "true" : "false");
                if (node.Focusable) sb.Append(" | focusable=true");
                if (node.Scrollable) sb.Append(" | scrollable=true");
                if (node.Bounds.Length > 0) sb.Append(" | bounds=").Append(node.Bounds);
                sb.AppendLine();
            }

            context.CompactText = sb.ToString();
            return context;
        }

        private static string BuildAiSystemInstruction()
        {
            return
                "당신은 Android QA 자동화를 설계하는 Senior Appium Engineer입니다. " +
                "사용자의 문장을 단순히 액션으로 번역하지 말고, 현재 화면의 실제 UI 요소와 기존 시나리오 문맥을 근거로 안전하고 재현 가능한 다음 단계를 설계하세요.\n\n" +
                "필수 규칙:\n" +
                "1. CURRENT UI CONTEXT에 없는 요소를 현재 화면의 첫 조작 대상으로 발명하지 마세요.\n" +
                "2. locator 우선순위는 resource-id(ID) > content-desc(AccessibilityID) > text 기반 XPath 입니다.\n" +
                "3. resource-id가 있으면 반드시 전체 resource-id를 사용하세요.\n" +
                "4. XPath는 정확 일치보다 contains(@text,...) 또는 contains(@content-desc,...)를 우선 사용하세요.\n" +
                "5. 좌표 클릭은 사용자가 명시적으로 좌표를 요구하지 않는 한 생성하지 마세요.\n" +
                "6. Sleep은 화면 전환/비동기 로딩에 꼭 필요한 경우만 1~3초로 사용하세요.\n" +
                "7. 기존 시나리오에 이미 있는 단계를 중복 생성하지 마세요.\n" +
                "8. 현재 화면 이후의 미래 화면 locator를 추측하지 마세요. 여러 화면이 필요한 요청이면 현재 화면에서 확실히 수행 가능한 단계까지만 만들고 warnings에 '다음 화면에서 AI 재분석 필요'를 남기세요.\n" +
                "9. 테스트 데이터는 실제 개인정보 대신 명백한 QA용 합성 데이터를 사용하세요.\n" +
                "10. Assert 요청이 있으면 사용자가 확인하려는 결과를 검증하는 단계를 마지막에 배치하세요.\n" +
                "11. 결과는 제공된 JSON schema만 따르세요.";
        }

        private async Task<List<string>?> CallGeminiForSteps(
            string apiKey,
            string prompt,
            string rawUiDump,
            IReadOnlyList<string> existingSteps,
            string deviceContext)
        {
            AiUiContext uiContext = BuildCompactUiContext(rawUiDump);
            string scenarioContext = existingSteps.Count == 0
                ? "(기존 단계 없음)"
                : string.Join("\n", existingSteps.TakeLast(15).Select((step, index) => $"{index + 1}. {step}"));

            string userContext =
                "[DEVICE]\n" + deviceContext + "\n\n" +
                "[EXISTING SCENARIO]\n" + scenarioContext + "\n\n" +
                "[CURRENT UI CONTEXT]\n" + uiContext.CompactText + "\n" +
                "[USER REQUEST]\n" + prompt.Trim();

            object responseSchema = new
            {
                type = "object",
                properties = new
                {
                    confidence = new { type = "integer", description = "0~100 사이의 계획 신뢰도" },
                    summary = new { type = "string", description = "계획을 한 문장으로 요약" },
                    warnings = new
                    {
                        type = "array",
                        items = new { type = "string" },
                        description = "화면 밖 요소, 재분석 필요 등 주의사항"
                    },
                    steps = new
                    {
                        type = "array",
                        items = new
                        {
                            type = "object",
                            properties = new
                            {
                                action = new { type = "string", description = "Click, Input, Sleep, Assert, ScreenAssert 중 하나" },
                                locatorType = new { type = "string", description = "ID, AccessibilityID, XPath 중 하나" },
                                target = new { type = "string", description = "locator target. Sleep/ScreenAssert면 빈 문자열" },
                                value = new { type = "string", description = "Input/Assert/ScreenAssert 값. 없으면 빈 문자열" },
                                seconds = new { type = "integer", description = "Sleep 초. 아니면 0" }
                            },
                            required = new[] { "action", "locatorType", "target", "value", "seconds" }
                        }
                    }
                },
                required = new[] { "confidence", "summary", "warnings", "steps" }
            };

            var requestBody = new
            {
                systemInstruction = new
                {
                    parts = new object[] { new { text = BuildAiSystemInstruction() } }
                },
                contents = new object[]
                {
                    new
                    {
                        role = "user",
                        parts = new object[] { new { text = userContext } }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.10,
                    topP = 0.80,
                    maxOutputTokens = 4096,
                    responseMimeType = "application/json",
                    responseSchema
                }
            };

            string json = JsonSerializer.Serialize(requestBody);
            const string url = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            using HttpResponseMessage response = await geminiHttp.SendAsync(request, timeout.Token);
            string responseText = await response.Content.ReadAsStringAsync(timeout.Token);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Gemini API 오류 ({(int)response.StatusCode}): {TrimAiMessage(responseText)}");

            string modelText = ExtractGeminiText(responseText);
            GeminiPlanDto plan = ParseGeminiPlan(modelText);
            plan.confidence = Math.Clamp(plan.confidence, 0, 100);
            plan.summary = SafeUiValue(plan.summary, 220);
            plan.warnings = plan.warnings.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => SafeUiValue(x, 220)).Take(8).ToList();

            List<string> rows = NormalizePlanSteps(plan.steps);
            ValidateFirstActionAgainstCurrentUi(plan, uiContext);

            lastAiConfidence = plan.confidence;
            lastAiSummary = string.IsNullOrWhiteSpace(plan.summary) ? "현재 화면 기반 자동화 계획" : plan.summary;
            lastAiWarnings = plan.warnings.ToArray();
            return rows;
        }

        private async Task RunSmartAiAssistantAsync()
        {
            string prompt = GetText(txtAiPrompt);
            if (string.IsNullOrWhiteSpace(prompt))
            {
                MessageBox.Show("명령을 입력해주세요.");
                return;
            }

            btnAiAnalyze.Enabled = false;
            string originalText = btnAiAnalyze.Text;
            btnAiAnalyze.Text = "분석 중...";
            lastAiConfidence = 0;
            lastAiSummary = string.Empty;
            lastAiWarnings = Array.Empty<string>();

            try
            {
                List<string>? steps = null;
                bool usedFallback = false;
                string onlineFailure = string.Empty;

                if (await Task.Run(AdbEngine.IsDeviceConnected))
                {
                    string apiKey = GetOrAskGeminiKey();
                    if (!string.IsNullOrWhiteSpace(apiKey))
                    {
                        string dumpPath = Path.Combine(SysPath, "window_dump_ai.xml");
                        try
                        {
                            await AdbEngine.RunCommandAsync("shell uiautomator dump /sdcard/window_dump_ai.xml", 10000);
                            await AdbEngine.RunCommandAsync($"pull /sdcard/window_dump_ai.xml \"{dumpPath}\"", 15000);
                            _ = AdbEngine.RunCommandAsync("shell rm /sdcard/window_dump_ai.xml", 5000);

                            if (File.Exists(dumpPath))
                            {
                                string dump = await File.ReadAllTextAsync(dumpPath, Encoding.UTF8);
                                IReadOnlyList<string> existingSteps = lstSteps.Items
                                    .Cast<object>()
                                    .Select(item => item?.ToString() ?? string.Empty)
                                    .Where(item => !string.IsNullOrWhiteSpace(item))
                                    .ToArray();
                                string deviceContext = $"model={SafeUiValue(lastDeviceModel, 80)}; android={SafeUiValue(lastDeviceOs, 40)}";
                                steps = await CallGeminiForSteps(apiKey, prompt, dump, existingSteps, deviceContext);
                            }
                        }
                        catch (Exception ex)
                        {
                            onlineFailure = ex.Message;
                            steps = null;
                        }
                        finally
                        {
                            try { if (File.Exists(dumpPath)) File.Delete(dumpPath); } catch { }
                        }
                    }
                }

                if (steps == null || steps.Count == 0)
                {
                    steps = AnalyzePromptToSteps(prompt);
                    usedFallback = steps.Count > 0;
                }
                if (steps == null || steps.Count == 0)
                {
                    string detail = string.IsNullOrWhiteSpace(onlineFailure)
                        ? "문장에서 실행 가능한 동작을 인식하지 못했습니다."
                        : "AI 분석에 실패했고 규칙 기반 분석에서도 동작을 만들지 못했습니다.\n\n" + TrimAiMessage(onlineFailure);
                    MessageBox.Show(detail, "AI 분석 결과", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var existing = new HashSet<string>(lstSteps.Items.Cast<object>().Select(item => item?.ToString() ?? string.Empty), StringComparer.Ordinal);
                int added = 0;
                foreach (string step in steps)
                {
                    if (existing.Add(step))
                    {
                        lstSteps.Items.Add(step);
                        added++;
                    }
                }

                txtAiPrompt.Text = txtAiPrompt.Tag?.ToString() ?? string.Empty;
                txtAiPrompt.ForeColor = Globals.TextFaint;

                if (usedFallback)
                {
                    lblStatusMsg.Text = string.IsNullOrWhiteSpace(onlineFailure)
                        ? $"상태: 오프라인 규칙 기반 분석으로 {added}단계를 추가했습니다."
                        : $"상태: Gemini 분석 실패 → 오프라인 규칙 기반으로 {added}단계를 추가했습니다.";
                    lblStatusMsg.ForeColor = Globals.Warning;
                    if (!string.IsNullOrWhiteSpace(onlineFailure))
                        AppendLiveLog("[AI] Gemini 분석 실패: " + TrimAiMessage(onlineFailure), Globals.Warning);
                }
                else
                {
                    string warningText = lastAiWarnings.Count > 0 ? $" · 주의 {lastAiWarnings.Count}건" : string.Empty;
                    lblStatusMsg.Text = $"상태: AI 계획 {added}단계 추가 · 신뢰도 {lastAiConfidence}%{warningText}";
                    lblStatusMsg.ForeColor = lastAiConfidence >= 70 ? Globals.Success : Globals.Warning;
                    AppendLiveLog($"[AI] {lastAiSummary} · confidence={lastAiConfidence}%", Globals.Info);
                    foreach (string warning in lastAiWarnings)
                        AppendLiveLog("[AI 경고] " + warning, Globals.Warning);
                }
            }
            finally
            {
                btnAiAnalyze.Enabled = true;
                btnAiAnalyze.Text = originalText;
            }
        }

        private static string ExtractGeminiText(string responseText)
        {
            using JsonDocument document = JsonDocument.Parse(responseText);
            if (!document.RootElement.TryGetProperty("candidates", out JsonElement candidates) || candidates.GetArrayLength() == 0)
                throw new InvalidDataException("Gemini 응답에 candidates가 없습니다.");

            JsonElement candidate = candidates[0];
            if (!candidate.TryGetProperty("content", out JsonElement content) || !content.TryGetProperty("parts", out JsonElement parts))
                throw new InvalidDataException("Gemini 응답에 content.parts가 없습니다.");

            var sb = new StringBuilder();
            foreach (JsonElement part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out JsonElement text)) sb.Append(text.GetString());
            }
            string result = sb.ToString().Trim();
            if (string.IsNullOrWhiteSpace(result)) throw new InvalidDataException("Gemini가 빈 응답을 반환했습니다.");
            return result;
        }

        private static GeminiPlanDto ParseGeminiPlan(string modelText)
        {
            string text = modelText.Trim();
            if (text.StartsWith("```", StringComparison.Ordinal))
            {
                int firstLine = text.IndexOf('\n');
                if (firstLine >= 0) text = text[(firstLine + 1)..];
                int fence = text.LastIndexOf("```", StringComparison.Ordinal);
                if (fence >= 0) text = text[..fence];
            }

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            try
            {
                GeminiPlanDto? plan = JsonSerializer.Deserialize<GeminiPlanDto>(text, options);
                if (plan != null) return plan;
            }
            catch (JsonException)
            {
                // 이전 버전의 배열 응답도 한 번 수용해 마이그레이션 중 오류를 줄인다.
            }

            try
            {
                List<GeminiStepDto>? legacy = JsonSerializer.Deserialize<List<GeminiStepDto>>(text, options);
                if (legacy != null)
                {
                    return new GeminiPlanDto
                    {
                        confidence = 55,
                        summary = "Legacy JSON 응답을 호환 모드로 변환했습니다.",
                        warnings = new List<string> { "구조화된 계획 메타데이터가 없어 신뢰도를 보수적으로 설정했습니다." },
                        steps = legacy
                    };
                }
            }
            catch (JsonException)
            {
                // 아래의 명확한 오류로 통일한다.
            }

            throw new InvalidDataException("Gemini 구조화 응답을 해석하지 못했습니다: " + TrimAiMessage(text));
        }

        private List<string> NormalizePlanSteps(IEnumerable<GeminiStepDto> source)
        {
            var rows = new List<string>();
            foreach (GeminiStepDto step in source.Take(20))
            {
                step.action = NormalizeAction(step.action);
                step.locatorType = NormalizeLocator(step.locatorType);
                step.target = SafeUiValue(step.target, 500);
                step.value = SafeUiValue(step.value, 500);

                if (step.action == "Sleep")
                {
                    step.seconds = Math.Clamp(step.seconds <= 0 ? 1 : step.seconds, 1, 10);
                }
                else if (step.action is "Click" or "Input" or "Assert")
                {
                    if (string.IsNullOrWhiteSpace(step.target)) continue;
                }

                string? row = GeminiStepToRow(step);
                if (row == null || (rows.Count > 0 && string.Equals(rows[^1], row, StringComparison.Ordinal))) continue;
                rows.Add(row);
            }
            return rows;
        }

        private static string NormalizeAction(string action)
        {
            return (action ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "click" or "tap" => "Click",
                "input" or "sendkeys" or "type" => "Input",
                "sleep" or "wait" => "Sleep",
                "assert" or "verify" => "Assert",
                "screenassert" or "screen_assert" => "ScreenAssert",
                _ => string.Empty
            };
        }

        private static string NormalizeLocator(string locator)
        {
            string value = (locator ?? string.Empty).Trim().Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
            return value switch
            {
                "id" or "resourceid" or "resource-id" => "ID",
                "accessibilityid" or "contentdesc" or "content-desc" => "AccessibilityID",
                _ => "XPath"
            };
        }

        private static void ValidateFirstActionAgainstCurrentUi(GeminiPlanDto plan, AiUiContext context)
        {
            GeminiStepDto? first = plan.steps.FirstOrDefault(step => NormalizeAction(step.action) is "Click" or "Input" or "Assert");
            if (first == null) return;

            string locator = NormalizeLocator(first.locatorType);
            string target = SafeUiValue(first.target, 500);
            bool grounded = true;

            if (locator == "ID") grounded = context.ResourceIds.Contains(target);
            else if (locator == "AccessibilityID") grounded = context.AccessibilityLabels.Contains(target);
            else
            {
                Match match = Regex.Match(target, @"contains\(@(?:text|content-desc),\s*['\"](?<value>[^'\"]+)['\"]\)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string token = match.Groups["value"].Value;
                    grounded = context.VisibleTexts.Any(text => text.Contains(token, StringComparison.OrdinalIgnoreCase))
                        || context.AccessibilityLabels.Any(text => text.Contains(token, StringComparison.OrdinalIgnoreCase));
                }
            }

            if (!grounded)
            {
                plan.confidence = Math.Max(0, plan.confidence - 25);
                plan.warnings.Add("첫 조작 대상이 현재 UI Dump에서 직접 확인되지 않아 실행 전 locator 확인이 필요합니다.");
            }
        }

        private static string TrimAiMessage(string value)
        {
            string text = (value ?? string.Empty).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
            return text.Length > 500 ? text[..500] + "..." : text;
        }
    }
}
