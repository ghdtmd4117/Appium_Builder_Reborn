using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AppiumBuilder.Core
{
    public sealed class LocalTestCase
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Preconditions { get; set; } = string.Empty;
        public string Steps { get; set; } = string.Empty;
        public string ExpectedResult { get; set; } = string.Empty;
        public string Priority { get; set; } = "P1";
        public string Type { get; set; } = "Positive";
    }

    public sealed class LocalTestCaseTemplate
    {
        public string Name { get; init; } = "기본 TC 양식";
        public IReadOnlyList<string> Columns { get; init; } = DefaultColumns;

        public static IReadOnlyList<string> DefaultColumns { get; } = new[]
        {
            "TC ID", "제목", "사전조건", "테스트 절차", "기대결과", "우선순위", "유형"
        };

        public static LocalTestCaseTemplate FromCsvHeader(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("양식 경로가 비어 있습니다.", nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException("CSV 양식을 찾을 수 없습니다.", path);

            string? firstLine = File.ReadLines(path, Encoding.UTF8).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(firstLine)) throw new InvalidDataException("CSV 첫 행에 컬럼명이 없습니다.");

            string[] columns = ParseCsvLine(firstLine).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            if (columns.Length == 0) throw new InvalidDataException("CSV 컬럼을 해석하지 못했습니다.");

            return new LocalTestCaseTemplate
            {
                Name = Path.GetFileName(path),
                Columns = columns
            };
        }

        internal static string[] ParseCsvLine(string line)
        {
            var values = new List<string>();
            var current = new StringBuilder();
            bool quoted = false;

            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (ch == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = !quoted;
                    }
                }
                else if (ch == ',' && !quoted)
                {
                    values.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(ch);
                }
            }

            values.Add(current.ToString().Trim());
            return values.ToArray();
        }
    }

    public static class LocalTestCaseEngine
    {
        public static IReadOnlyList<LocalTestCase> BuildRuleBasedDraft(string requirement)
        {
            string subject = NormalizeRequirement(requirement);
            if (string.IsNullOrWhiteSpace(subject)) return Array.Empty<LocalTestCase>();

            return new[]
            {
                new LocalTestCase
                {
                    Id = "TC-001",
                    Title = $"{subject} - 정상 흐름",
                    Preconditions = "테스트 대상 기능에 접근 가능한 상태이며 기본 테스트 데이터가 준비되어 있다.",
                    Steps = $"1. {subject} 기능에 진입한다.\r\n2. 유효한 조건과 값을 입력한다.\r\n3. 실행/확인 동작을 수행한다.",
                    ExpectedResult = "기능이 정상 처리되고 사용자가 기대한 완료 상태 또는 결과가 표시된다.",
                    Priority = "P1",
                    Type = "Positive"
                },
                new LocalTestCase
                {
                    Id = "TC-002",
                    Title = $"{subject} - 필수값/오류 처리",
                    Preconditions = "테스트 대상 기능에 접근 가능한 상태이다.",
                    Steps = $"1. {subject} 기능에 진입한다.\r\n2. 필수값을 비우거나 유효하지 않은 값을 입력한다.\r\n3. 실행/확인 동작을 수행한다.",
                    ExpectedResult = "잘못된 요청이 처리되지 않고 사용자가 이해할 수 있는 오류 또는 검증 메시지가 표시된다.",
                    Priority = "P1",
                    Type = "Negative"
                },
                new LocalTestCase
                {
                    Id = "TC-003",
                    Title = $"{subject} - 경계값/반복 동작",
                    Preconditions = "테스트 대상 기능에 접근 가능한 상태이며 경계값 테스트가 가능한 데이터가 준비되어 있다.",
                    Steps = $"1. {subject} 기능에 진입한다.\r\n2. 최소/최대/빈 값 등 경계 조건을 적용한다.\r\n3. 같은 동작을 반복 수행하고 상태 변화를 확인한다.",
                    ExpectedResult = "경계 조건에서도 비정상 종료나 데이터 훼손 없이 정의된 정책대로 처리된다.",
                    Priority = "P2",
                    Type = "Boundary"
                }
            };
        }

        public static void ExportCsv(string path, LocalTestCaseTemplate template, IEnumerable<LocalTestCase> cases)
        {
            ArgumentNullException.ThrowIfNull(template);
            ArgumentNullException.ThrowIfNull(cases);

            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", template.Columns.Select(EscapeCsv)));

            foreach (LocalTestCase testCase in cases)
            {
                IEnumerable<string> row = template.Columns.Select(column => ResolveColumnValue(testCase, column));
                sb.AppendLine(string.Join(",", row.Select(EscapeCsv)));
            }

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        internal static string ResolveColumnValue(LocalTestCase testCase, string column)
        {
            string key = NormalizeColumn(column);
            if (key is "id" or "tcid" or "testcaseid" or "테스트케이스id" or "테스트id") return testCase.Id;
            if (key.Contains("title") || key.Contains("제목") || key is "testcase" or "테스트케이스" or "테스트항목") return testCase.Title;
            if (key.Contains("precondition") || key.Contains("사전조건") || key.Contains("선행조건")) return testCase.Preconditions;
            if (key.Contains("step") || key.Contains("procedure") || key.Contains("테스트절차") || key.Contains("수행절차") || key.Contains("절차")) return testCase.Steps;
            if (key.Contains("expected") || key.Contains("expect") || key.Contains("기대결과") || key.Contains("예상결과")) return testCase.ExpectedResult;
            if (key.Contains("priority") || key.Contains("우선순위") || key.Contains("중요도")) return testCase.Priority;
            if (key.Contains("type") || key.Contains("유형") || key.Contains("구분")) return testCase.Type;
            return string.Empty;
        }

        private static string NormalizeRequirement(string requirement)
        {
            string value = (requirement ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            while (value.Contains("  ", StringComparison.Ordinal)) value = value.Replace("  ", " ", StringComparison.Ordinal);
            return value.Length > 80 ? value[..80] + "…" : value;
        }

        private static string NormalizeColumn(string value)
        {
            return new string((value ?? string.Empty)
                .Where(ch => !char.IsWhiteSpace(ch) && ch is not '_' and not '-' and not '(' and not ')' and not '[' and not ']')
                .Select(char.ToLowerInvariant)
                .ToArray());
        }

        private static string EscapeCsv(string value)
        {
            string safe = value ?? string.Empty;
            if (!safe.Contains(',') && !safe.Contains('"') && !safe.Contains('\r') && !safe.Contains('\n')) return safe;
            return '"' + safe.Replace("\"", "\"\"") + '"';
        }
    }

    public sealed class LocalOnlyLlmClient : IDisposable
    {
        private const int MaxCombinedDocumentTextChars = 120_000;
        private const int MaxVisionImages = 10;
        private const int MaxSingleVisionImageBytes = 12 * 1024 * 1024;

        private readonly HttpClient _client;

        public LocalOnlyLlmClient()
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                UseProxy = false
            };
            _client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMinutes(8)
            };
        }

        public static bool IsLoopbackEndpoint(string endpoint)
        {
            return Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                && uri.IsLoopback;
        }

        public Task<IReadOnlyList<LocalTestCase>> GenerateWithOllamaAsync(
            string endpoint,
            string model,
            string requirement,
            IReadOnlyList<string> templateColumns,
            CancellationToken cancellationToken = default)
        {
            return GenerateWithOllamaAsync(
                endpoint,
                model,
                requirement,
                string.Empty,
                templateColumns,
                Array.Empty<LocalPlanningDocument>(),
                cancellationToken);
        }

        public async Task<IReadOnlyList<LocalTestCase>> GenerateWithOllamaAsync(
            string endpoint,
            string model,
            string requirement,
            string generationGuide,
            IReadOnlyList<string> templateColumns,
            IReadOnlyList<LocalPlanningDocument> documents,
            CancellationToken cancellationToken = default)
        {
            if (!IsLoopbackEndpoint(endpoint))
                throw new InvalidOperationException("보안 정책상 로컬 TC 생성기는 localhost/127.0.0.1/::1 주소에만 연결할 수 있습니다.");
            if (string.IsNullOrWhiteSpace(model))
                throw new InvalidOperationException("로컬 모델명이 설정되지 않았습니다.");
            if (string.IsNullOrWhiteSpace(requirement) && (documents == null || documents.Count == 0))
                throw new InvalidOperationException("요구사항을 입력하거나 기획서 파일을 추가해주세요.");

            documents ??= Array.Empty<LocalPlanningDocument>();
            templateColumns ??= LocalTestCaseTemplate.DefaultColumns;

            var baseUri = new Uri(endpoint.EndsWith('/') ? endpoint : endpoint + "/", UriKind.Absolute);
            var requestUri = new Uri(baseUri, "api/chat");
            if (!requestUri.IsLoopback)
                throw new InvalidOperationException("로컬 전용 보안 검증에 실패했습니다.");

            string system =
                "You are a senior QA test-case designer running inside a strictly local-only Windows desktop application. " +
                "Use only the supplied requirement, planning documents, images and TC generation guide. " +
                "Never request external uploads, web searches, cloud APIs, or assumptions that are not grounded in the supplied material. " +
                "If a policy is not specified, express it as a verification point instead of inventing a product rule. " +
                "Return JSON only. Create practical, executable test cases with positive, negative, boundary, state, permission, error, and recovery coverage when relevant. " +
                "Follow the user's TC generation guide over your default style.";

            string documentText = BuildDocumentPrompt(documents);
            string guideText = string.IsNullOrWhiteSpace(generationGuide)
                ? "(별도 가이드 없음 - 기본 QA 작성 원칙 적용)"
                : generationGuide.Trim();
            string requirementText = string.IsNullOrWhiteSpace(requirement)
                ? "(추가 요구사항 없음 - 첨부 기획서를 기준으로 작성)"
                : requirement.Trim();

            string user =
                "[TC 생성 가이드 - 반드시 우선 적용]\n" + guideText + "\n\n" +
                "[추가 요구사항 / 메모]\n" + requirementText + "\n\n" +
                "[첨부 기획서에서 로컬 추출한 내용]\n" + documentText + "\n\n" +
                "[업무 TC 양식 컬럼]\n" + string.Join(", ", templateColumns) + "\n\n" +
                "작성 규칙:\n" +
                "1. 기획서의 기능/화면/상태/조건/예외를 먼저 파악하고 테스트 포인트로 변환한다.\n" +
                "2. 이미지가 첨부되었다면 화면의 버튼, 입력 영역, 문구, 팝업, 상태 변화 등 시각 정보도 근거로 사용한다.\n" +
                "3. 중복 TC를 만들지 않는다.\n" +
                "4. 각 TC는 혼자 실행 가능한 수준으로 사전조건/절차/기대결과를 구체적으로 쓴다.\n" +
                "5. 자료에 없는 정책/값을 임의로 확정하지 않는다.\n" +
                "6. 필요하면 5~30개의 TC를 작성한다. 복잡한 기획서는 더 많이 작성해도 된다.\n\n" +
                "다음 JSON 배열 형식만 반환:\n" +
                "[{\"id\":\"TC-001\",\"title\":\"...\",\"preconditions\":\"...\",\"steps\":\"1. ...\\n2. ...\",\"expectedResult\":\"...\",\"priority\":\"P1\",\"type\":\"Positive\"}]";

            string[] images = documents
                .SelectMany(d => d.Images ?? Array.Empty<LocalDocumentImage>())
                .Where(x => x.Bytes is { Length: > 0 } && x.Bytes.Length <= MaxSingleVisionImageBytes)
                .Take(MaxVisionImages)
                .Select(x => Convert.ToBase64String(x.Bytes))
                .ToArray();

            object userMessage = images.Length > 0
                ? new { role = "user", content = user, images }
                : new { role = "user", content = user };

            var payload = new
            {
                model = model.Trim(),
                stream = false,
                format = "json",
                think = false,
                messages = new object[]
                {
                    new { role = "system", content = system },
                    userMessage
                },
                options = new
                {
                    temperature = 0.15,
                    num_ctx = 32768
                }
            };

            string json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await _client.PostAsync(requestUri, content, cancellationToken).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.StatusCode is HttpStatusCode.Moved or HttpStatusCode.Redirect or HttpStatusCode.RedirectMethod or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
                throw new InvalidOperationException("로컬 모델 서버가 Redirect를 반환해 보안상 요청을 중단했습니다.");
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"로컬 모델 호출 실패 ({(int)response.StatusCode}): {TrimForMessage(body)}");

            using JsonDocument doc = JsonDocument.Parse(body);
            string modelText = doc.RootElement
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;

            LocalLlmCaseDto[]? items = DeserializeCases(modelText);
            if (items == null || items.Length == 0)
                throw new InvalidDataException("로컬 모델이 TC JSON을 반환하지 않았습니다.");

            return items.Take(40).Select((item, index) => new LocalTestCase
            {
                Id = string.IsNullOrWhiteSpace(item.Id) ? $"TC-{index + 1:000}" : item.Id.Trim(),
                Title = item.Title?.Trim() ?? string.Empty,
                Preconditions = item.Preconditions?.Trim() ?? string.Empty,
                Steps = NormalizeMultiline(item.Steps),
                ExpectedResult = item.ExpectedResult?.Trim() ?? string.Empty,
                Priority = string.IsNullOrWhiteSpace(item.Priority) ? "P2" : item.Priority.Trim(),
                Type = string.IsNullOrWhiteSpace(item.Type) ? "Positive" : item.Type.Trim()
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Title))
            .GroupBy(x => NormalizeCaseKey(x.Title), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToArray();
        }

        public void Dispose() => _client.Dispose();

        private static string BuildDocumentPrompt(IReadOnlyList<LocalPlanningDocument> documents)
        {
            if (documents == null || documents.Count == 0)
                return "(첨부 기획서 없음)";

            var sb = new StringBuilder();
            foreach (LocalPlanningDocument document in documents)
            {
                if (sb.Length >= MaxCombinedDocumentTextChars) break;
                sb.AppendLine($"\n--- {document.FileName} / {document.Kind} / 단위 {document.UnitCount} / 이미지 {document.Images.Count}개 ---");
                if (!string.IsNullOrWhiteSpace(document.Warning))
                    sb.AppendLine("[추출 참고] " + document.Warning);
                if (string.IsNullOrWhiteSpace(document.ExtractedText))
                    sb.AppendLine("(텍스트 없음 - 첨부 이미지가 있으면 Vision 입력으로 분석)");
                else
                    AppendLimited(sb, document.ExtractedText, MaxCombinedDocumentTextChars);
            }
            return sb.ToString().Trim();
        }

        private static void AppendLimited(StringBuilder builder, string value, int maxChars)
        {
            if (builder.Length >= maxChars || string.IsNullOrEmpty(value)) return;
            int available = maxChars - builder.Length;
            if (value.Length <= available) builder.AppendLine(value);
            else builder.Append(value.AsSpan(0, available));
        }

        private static LocalLlmCaseDto[]? DeserializeCases(string modelText)
        {
            string text = (modelText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text)) return null;

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            try
            {
                return JsonSerializer.Deserialize<LocalLlmCaseDto[]>(text, options);
            }
            catch (JsonException)
            {
                // 일부 모델은 {"cases":[...]} 형태로 감싸기도 하므로 로컬에서 한 번 더 수용한다.
                using JsonDocument wrapper = JsonDocument.Parse(text);
                if (wrapper.RootElement.ValueKind == JsonValueKind.Object
                    && wrapper.RootElement.TryGetProperty("cases", out JsonElement cases)
                    && cases.ValueKind == JsonValueKind.Array)
                {
                    return JsonSerializer.Deserialize<LocalLlmCaseDto[]>(cases.GetRawText(), options);
                }
                throw;
            }
        }

        private static string NormalizeMultiline(string? value)
        {
            return (value ?? string.Empty).Replace("\r\n", "\n").Replace("\n", "\r\n").Trim();
        }

        private static string NormalizeCaseKey(string value)
        {
            return new string((value ?? string.Empty)
                .Where(ch => !char.IsWhiteSpace(ch) && !char.IsPunctuation(ch))
                .Select(char.ToLowerInvariant)
                .ToArray());
        }

        private static string TrimForMessage(string value)
        {
            string text = (value ?? string.Empty).Trim();
            return text.Length > 500 ? text[..500] + "..." : text;
        }

        private sealed class LocalLlmCaseDto
        {
            public string Id { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Preconditions { get; set; } = string.Empty;
            public string Steps { get; set; } = string.Empty;
            public string ExpectedResult { get; set; } = string.Empty;
            public string Priority { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
        }
    }
}
