using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;
using System.Windows.Forms;
using AppiumBuilder.Utils;

namespace AppiumBuilder
{
    public partial class MainForm
    {
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
            if (string.IsNullOrWhiteSpace(input)) return "";

            string trimmed = input.Trim();
            File.WriteAllText(encryptedPath, SecretStore.ProtectToBase64(trimmed), Encoding.UTF8);
            return trimmed;
        }

        private class GeminiStepDto
        {
            public string action { get; set; } = "";
            public string locatorType { get; set; } = "XPath";
            public string target { get; set; } = "";
            public string value { get; set; } = "";
            public int seconds { get; set; }
        }

        private string? GeminiStepToRow(GeminiStepDto d)
        {
            string loc = d.locatorType == "ID" ? "ID" : d.locatorType == "AccessibilityID" ? "Accessibility ID" : "XPath";
            return d.action switch
            {
                "Click" => $"[Click] [{loc}] {d.target}",
                "Input" => $"[Input] [{loc}] {d.target} | 값: {d.value}",
                "Sleep" => $"[Sleep] {d.seconds} 초",
                "Assert" => $"[Assert] [{loc}] {d.target} | 값:{d.value}",
                "ScreenAssert" => $"[ScreenAssert] 값: {d.value}",
                _ => null
            };
        }


        private static string RedactUiDumpForAi(string xml)
        {
            if (string.IsNullOrEmpty(xml)) return xml;
            string redacted = xml;
            // 이메일
            redacted = Regex.Replace(redacted, @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", "[REDACTED_EMAIL]", RegexOptions.IgnoreCase);
            // 한국 휴대전화/일반 전화번호
            redacted = Regex.Replace(redacted, @"(?<!\d)(?:\+?82[- ]?)?0?1[016789][- ]?\d{3,4}[- ]?\d{4}(?!\d)", "[REDACTED_PHONE]");
            // 계좌/인증번호처럼 긴 숫자 덩어리. resource-id의 짧은 숫자에는 영향이 없도록 8자리 이상만 처리.
            redacted = Regex.Replace(redacted, @"(?<![A-Za-z0-9])\d{8,}(?![A-Za-z0-9])", "[REDACTED_NUMBER]");
            return redacted;
        }
        private async Task<System.Collections.Generic.List<string>?> CallGeminiForSteps(string apiKey, string prompt, string uiDump)
        {
            string instruction =
                "당신은 Android UI 자동화 어시스턴트입니다. 아래는 현재 화면의 UI 계층 구조(XML)입니다.\n\n" +
                "[UI DUMP]\n" + uiDump + "\n\n" +
                "사용자 명령: \"" + prompt + "\"\n\n" +
                "위 UI 구조에 실제로 존재하는 요소의 resource-id 또는 text/content-desc 값을 사용해서, " +
                "사용자 명령을 수행하기 위한 단계를 JSON 배열로만 출력하세요. 코드블록이나 다른 설명 없이 순수 JSON 배열만 출력하세요.\n\n" +
                "각 항목은 다음 형식 중 하나입니다:\n" +
                "{\"action\":\"Click\",\"locatorType\":\"ID\",\"target\":\"resource-id 값\"}\n" +
                "{\"action\":\"Click\",\"locatorType\":\"XPath\",\"target\":\"//*[@text='버튼텍스트']\"}\n" +
                "{\"action\":\"Input\",\"locatorType\":\"ID\",\"target\":\"resource-id\",\"value\":\"입력할 텍스트\"}\n" +
                "{\"action\":\"Sleep\",\"seconds\":2}\n" +
                "{\"action\":\"Assert\",\"locatorType\":\"XPath\",\"target\":\"...\",\"value\":\"기대 텍스트\"}\n\n" +
                "resource-id가 존재하면 반드시 locatorType을 ID로 하고 target에 전체 resource-id(패키지명 포함)를 넣으세요. " +
                "resource-id가 없으면 XPath로 //*[contains(@text,'...')] 또는 //*[contains(@content-desc,'...')] 형태를 사용하세요 (정확히 일치하는 = 대신 반드시 contains를 쓰세요). " +
                "content-desc나 text 값 안에 줄바꿈이 포함된 경우, 줄바꿈 이전의 핵심 단어만 사용하세요 (예: '실시간 요금 안내\\n탭 3개 중 2번째' → '실시간 요금 안내'만 사용).";

            var requestBody = new { contents = new object[] { new { parts = new object[] { new { text = instruction } } } } };
            string json = JsonSerializer.Serialize(requestBody);

            // 모델명은 구글이 수시로 바꿀 수 있습니다. 404/모델 없음 오류가 뜨면 이 문자열만 최신 모델명으로 교체하세요.
            string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";

            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await geminiHttp.PostAsync(url, content);
            string respText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Gemini API 오류 ({(int)response.StatusCode}): {respText}");

            using var doc = JsonDocument.Parse(respText);
            if (!doc.RootElement.TryGetProperty("candidates", out var candidatesEl))
                throw new Exception("Gemini 응답에 candidates가 없습니다. 원본: " + respText);

            var firstCandidate = candidatesEl.EnumerateArray().First();
            var firstPart = firstCandidate.GetProperty("content").GetProperty("parts").EnumerateArray().First();
            string modelText = firstPart.GetProperty("text").GetString() ?? "";

            modelText = modelText.Trim();
            if (modelText.StartsWith("```"))
            {
                int nl = modelText.IndexOf('\n');
                if (nl >= 0) modelText = modelText.Substring(nl + 1);
                int fence = modelText.LastIndexOf("```");
                if (fence >= 0) modelText = modelText.Substring(0, fence);
            }

            System.Collections.Generic.List<GeminiStepDto>? dtoList;
            try
            {
                dtoList = JsonSerializer.Deserialize<System.Collections.Generic.List<GeminiStepDto>>(
                    modelText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception)
            {
                throw new Exception("Gemini 응답을 해석하지 못했습니다. 원본 응답: " +
                    (modelText.Length > 300 ? modelText.Substring(0, 300) + "..." : modelText));
            }

            if (dtoList == null) return null;
            var rows = new System.Collections.Generic.List<string>();
            foreach (var d in dtoList)
            {
                if (!string.IsNullOrEmpty(d.target))
                {
                    int nlIdx = d.target.IndexOfAny(new[] { '\n', '\r' });
                    if (nlIdx >= 0) d.target = d.target.Substring(0, nlIdx).Trim();
                }
                string? row = GeminiStepToRow(d);
                if (row != null) rows.Add(row);
            }
            return rows;
        }
    }
}
