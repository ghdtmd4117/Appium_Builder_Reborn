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
                Timeout = TimeSpan.FromSeconds(90)
            };
        }

        public static bool IsLoopbackEndpoint(string endpoint)
        {
            return Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                && Uri.IsLoopback(uri);
        }

        public async Task<IReadOnlyList<LocalTestCase>> GenerateWithOllamaAsync(
            string endpoint,
            string model,
            string requirement,
            IReadOnlyList<string> templateColumns,
            CancellationToken cancellationToken = default)
        {
            if (!IsLoopbackEndpoint(endpoint))
                throw new InvalidOperationException("보안 정책상 로컬 TC 생성기는 localhost/127.0.0.1/::1 주소에만 연결할 수 있습니다.");
            if (string.IsNullOrWhiteSpace(model))
                throw new InvalidOperationException("로컬 모델명을 입력해주세요.");
            if (string.IsNullOrWhiteSpace(requirement))
                throw new InvalidOperationException("TC를 만들 요구사항을 입력해주세요.");

            var baseUri = new Uri(endpoint.EndsWith('/') ? endpoint : endpoint + "/", UriKind.Absolute);
            var requestUri = new Uri(baseUri, "api/chat");
            if (!Uri.IsLoopback(requestUri))
                throw new InvalidOperationException("로컬 전용 보안 검증에 실패했습니다.");

            string system =
                "You are a QA test-case designer running inside a local-only desktop application. " +
                "Return JSON only. Never ask to upload files or contact an external service. " +
                "Create concise, executable test cases that include positive, negative, and boundary coverage when relevant.";

            string user =
                "요구사항:\n" + requirement.Trim() + "\n\n" +
                "업무 양식 컬럼:\n" + string.Join(", ", templateColumns) + "\n\n" +
                "다음 JSON 배열 형식으로 3~8개의 TC를 생성하세요:\n" +
                "[{\"id\":\"TC-001\",\"title\":\"...\",\"preconditions\":\"...\",\"steps\":\"1. ...\\n2. ...\",\"expectedResult\":\"...\",\"priority\":\"P1\",\"type\":\"Positive\"}]";

            var payload = new
            {
                model = model.Trim(),
                stream = false,
                format = "json",
                messages = new object[]
                {
                    new { role = "system", content = system },
                    new { role = "user", content = user }
                },
                options = new { temperature = 0.2 }
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

            LocalLlmCaseDto[]? items = JsonSerializer.Deserialize<LocalLlmCaseDto[]>(modelText,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (items == null || items.Length == 0)
                throw new InvalidDataException("로컬 모델이 TC JSON을 반환하지 않았습니다.");

            return items.Take(12).Select((item, index) => new LocalTestCase
            {
                Id = string.IsNullOrWhiteSpace(item.Id) ? $"TC-{index + 1:000}" : item.Id.Trim(),
                Title = item.Title?.Trim() ?? string.Empty,
                Preconditions = item.Preconditions?.Trim() ?? string.Empty,
                Steps = item.Steps?.Trim() ?? string.Empty,
                ExpectedResult = item.ExpectedResult?.Trim() ?? string.Empty,
                Priority = string.IsNullOrWhiteSpace(item.Priority) ? "P2" : item.Priority.Trim(),
                Type = string.IsNullOrWhiteSpace(item.Type) ? "Positive" : item.Type.Trim()
            }).Where(x => !string.IsNullOrWhiteSpace(x.Title)).ToArray();
        }

        public void Dispose() => _client.Dispose();

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
