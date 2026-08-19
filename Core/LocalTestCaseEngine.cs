using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AppiumBuilder.Core
{
    public sealed class DynamicTestCase
    {
        public Dictionary<string, string> Fields { get; set; } = new(StringComparer.CurrentCultureIgnoreCase);

        public string GetValue(string column)
        {
            if (Fields.TryGetValue(column, out string? value)) return value ?? string.Empty;
            KeyValuePair<string, string> match = Fields.FirstOrDefault(x =>
                x.Key.Equals(column, StringComparison.CurrentCultureIgnoreCase));
            return match.Key == null ? string.Empty : match.Value ?? string.Empty;
        }
    }

    public sealed class TcExampleSet
    {
        public string SourcePath { get; init; } = string.Empty;
        public string FileName { get; init; } = string.Empty;
        public IReadOnlyList<string> Columns { get; init; } = Array.Empty<string>();
        public IReadOnlyList<Dictionary<string, string>> Rows { get; init; } = Array.Empty<Dictionary<string, string>>();
        public int TotalRowCount { get; init; }

        public string DisplaySummary => $"{FileName} · 컬럼 {Columns.Count}개 · 예시 {Rows.Count}행 / 전체 {TotalRowCount}행";
    }

    public sealed class TcLearningDigest
    {
        public List<string> Columns { get; set; } = new();
        public string RuleSummary { get; set; } = string.Empty;
        public string StyleGuide { get; set; } = string.Empty;
        public string CoverageGuide { get; set; } = string.Empty;
        public List<string> Warnings { get; set; } = new();
    }

    public sealed class GeneratedTcBatch
    {
        public IReadOnlyList<string> Columns { get; init; } = Array.Empty<string>();
        public IReadOnlyList<DynamicTestCase> Cases { get; init; } = Array.Empty<DynamicTestCase>();
    }

    public static class LocalTestCaseEngine
    {
        private const int MaxExampleRows = 24;
        private const int MaxExampleCellChars = 4000;

        public static TcExampleSet ReadExampleSet(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("TC 예시 파일 경로가 비어 있습니다.", nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException("TC 예시 파일을 찾을 수 없습니다.", path);

            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".csv" => ReadCsvExample(path),
                ".xlsx" => ReadXlsxExample(path),
                _ => throw new NotSupportedException("기존 TC 학습 파일은 CSV 또는 XLSX 형식을 지원합니다.")
            };
        }

        public static void ExportCsv(string path, IReadOnlyList<string> columns, IEnumerable<DynamicTestCase> cases)
        {
            ArgumentNullException.ThrowIfNull(columns);
            ArgumentNullException.ThrowIfNull(cases);
            if (columns.Count == 0) throw new InvalidOperationException("내보낼 TC 컬럼이 없습니다.");

            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", columns.Select(EscapeCsv)));
            foreach (DynamicTestCase testCase in cases)
            {
                IEnumerable<string> row = columns.Select(testCase.GetValue);
                sb.AppendLine(string.Join(",", row.Select(EscapeCsv)));
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        public static IReadOnlyList<string> ChooseCanonicalColumns(IEnumerable<TcExampleSet> examples)
        {
            List<TcExampleSet> sets = (examples ?? Array.Empty<TcExampleSet>())
                .Where(x => x.Columns.Count > 0)
                .ToList();
            if (sets.Count == 0) return Array.Empty<string>();

            return sets
                .GroupBy(x => string.Join("\u001F", x.Columns.Select(c => c.Trim())), StringComparer.CurrentCultureIgnoreCase)
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => g.Sum(x => x.TotalRowCount))
                .First()
                .First()
                .Columns
                .ToArray();
        }

        public static List<Dictionary<string, string>> BuildRepresentativeExamples(
            IEnumerable<TcExampleSet> exampleSets,
            IReadOnlyList<string> canonicalColumns,
            int maxRows = 8)
        {
            var result = new List<Dictionary<string, string>>();
            foreach (TcExampleSet set in exampleSets ?? Array.Empty<TcExampleSet>())
            {
                foreach (Dictionary<string, string> source in set.Rows)
                {
                    var row = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);
                    IEnumerable<string> columns = canonicalColumns.Count > 0 ? canonicalColumns : set.Columns;
                    foreach (string column in columns)
                    {
                        if (TryGetCaseInsensitive(source, column, out string value))
                            row[column] = Limit(value, 1800);
                    }
                    if (row.Count > 0) result.Add(row);
                    if (result.Count >= maxRows) return result;
                }
            }
            return result;
        }

        internal static string EscapeCsv(string? value)
        {
            string safe = value ?? string.Empty;
            if (!safe.Contains(',') && !safe.Contains('"') && !safe.Contains('\r') && !safe.Contains('\n')) return safe;
            return '"' + safe.Replace("\"", "\"\"") + '"';
        }

        private static TcExampleSet ReadCsvExample(string path)
        {
            string content = File.ReadAllText(path, Encoding.UTF8);
            List<List<string>> table = ParseCsvDocument(content);
            if (table.Count == 0) throw new InvalidDataException("CSV에서 TC 데이터를 찾지 못했습니다.");

            List<string> columns = NormalizeColumns(table[0]);
            if (columns.Count == 0) throw new InvalidDataException("CSV 첫 행에서 컬럼명을 찾지 못했습니다.");

            List<Dictionary<string, string>> rows = BuildRows(columns, table.Skip(1));
            return new TcExampleSet
            {
                SourcePath = path,
                FileName = Path.GetFileName(path),
                Columns = columns,
                Rows = rows.Take(MaxExampleRows).ToArray(),
                TotalRowCount = rows.Count
            };
        }

        private static TcExampleSet ReadXlsxExample(string path)
        {
            using ZipArchive archive = ZipFile.OpenRead(path);
            List<string> sharedStrings = ReadSharedStrings(archive);
            ZipArchiveEntry? sheet = archive.GetEntry("xl/worksheets/sheet1.xml")
                ?? archive.Entries.FirstOrDefault(x => x.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase)
                    && x.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
            if (sheet == null) throw new InvalidDataException("XLSX에서 첫 Worksheet를 찾지 못했습니다.");

            using Stream stream = sheet.Open();
            XDocument doc = XDocument.Load(stream);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            List<List<string>> table = new();

            foreach (XElement rowElement in doc.Descendants(ns + "row"))
            {
                var cells = new SortedDictionary<int, string>();
                foreach (XElement cell in rowElement.Elements(ns + "c"))
                {
                    string reference = (string?)cell.Attribute("r") ?? string.Empty;
                    int index = ColumnIndexFromReference(reference);
                    if (index < 0) continue;
                    cells[index] = ReadXlsxCell(cell, ns, sharedStrings);
                }
                if (cells.Count == 0) continue;
                int max = cells.Keys.Max();
                var row = new List<string>(Enumerable.Repeat(string.Empty, max + 1));
                foreach (KeyValuePair<int, string> cell in cells) row[cell.Key] = cell.Value;
                table.Add(row);
            }

            if (table.Count == 0) throw new InvalidDataException("XLSX 첫 Worksheet에서 TC 데이터를 찾지 못했습니다.");
            List<string> columns = NormalizeColumns(table[0]);
            if (columns.Count == 0) throw new InvalidDataException("XLSX 첫 행에서 컬럼명을 찾지 못했습니다.");

            List<Dictionary<string, string>> rows = BuildRows(columns, table.Skip(1));
            return new TcExampleSet
            {
                SourcePath = path,
                FileName = Path.GetFileName(path),
                Columns = columns,
                Rows = rows.Take(MaxExampleRows).ToArray(),
                TotalRowCount = rows.Count
            };
        }

        private static List<Dictionary<string, string>> BuildRows(List<string> columns, IEnumerable<List<string>> sourceRows)
        {
            var rows = new List<Dictionary<string, string>>();
            foreach (List<string> source in sourceRows)
            {
                if (source.All(string.IsNullOrWhiteSpace)) continue;
                var row = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);
                for (int i = 0; i < columns.Count; i++)
                {
                    string value = i < source.Count ? Limit(source[i], MaxExampleCellChars) : string.Empty;
                    row[columns[i]] = value;
                }
                rows.Add(row);
            }
            return rows;
        }

        private static List<string> NormalizeColumns(IReadOnlyList<string> raw)
        {
            var columns = new List<string>();
            for (int i = 0; i < raw.Count; i++)
            {
                string value = (raw[i] ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(value)) value = $"Column_{i + 1}";
                string unique = value;
                int suffix = 2;
                while (columns.Any(x => x.Equals(unique, StringComparison.CurrentCultureIgnoreCase)))
                    unique = value + "_" + suffix++;
                columns.Add(unique);
                if (columns.Count >= 40) break;
            }
            while (columns.Count > 0 && columns[^1].StartsWith("Column_", StringComparison.Ordinal) && raw.Count > columns.Count - 1 && string.IsNullOrWhiteSpace(raw[columns.Count - 1]))
                columns.RemoveAt(columns.Count - 1);
            return columns;
        }

        private static List<List<string>> ParseCsvDocument(string content)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;
            string text = content ?? string.Empty;

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch == '"')
                {
                    if (quoted && i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else quoted = !quoted;
                    continue;
                }
                if (ch == ',' && !quoted)
                {
                    row.Add(cell.ToString());
                    cell.Clear();
                    continue;
                }
                if ((ch == '\r' || ch == '\n') && !quoted)
                {
                    if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    row.Add(cell.ToString());
                    cell.Clear();
                    if (row.Any(x => !string.IsNullOrWhiteSpace(x))) rows.Add(row);
                    row = new List<string>();
                    continue;
                }
                cell.Append(ch);
            }

            if (cell.Length > 0 || row.Count > 0)
            {
                row.Add(cell.ToString());
                if (row.Any(x => !string.IsNullOrWhiteSpace(x))) rows.Add(row);
            }
            return rows;
        }

        private static List<string> ReadSharedStrings(ZipArchive archive)
        {
            ZipArchiveEntry? entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null) return new List<string>();
            using Stream stream = entry.Open();
            XDocument doc = XDocument.Load(stream);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            return doc.Descendants(ns + "si")
                .Select(si => string.Concat(si.Descendants(ns + "t").Select(t => t.Value)))
                .ToList();
        }

        private static string ReadXlsxCell(XElement cell, XNamespace ns, IReadOnlyList<string> sharedStrings)
        {
            string type = (string?)cell.Attribute("t") ?? string.Empty;
            if (type == "inlineStr") return string.Concat(cell.Descendants(ns + "t").Select(x => x.Value));
            string raw = cell.Element(ns + "v")?.Value ?? string.Empty;
            if (type == "s" && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index)
                && index >= 0 && index < sharedStrings.Count)
                return sharedStrings[index];
            return raw;
        }

        private static int ColumnIndexFromReference(string reference)
        {
            int value = 0;
            bool found = false;
            foreach (char ch in reference)
            {
                if (!char.IsLetter(ch)) break;
                found = true;
                value = value * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
            }
            return found ? value - 1 : -1;
        }

        private static bool TryGetCaseInsensitive(Dictionary<string, string> source, string key, out string value)
        {
            if (source.TryGetValue(key, out string? exact))
            {
                value = exact ?? string.Empty;
                return true;
            }
            KeyValuePair<string, string> match = source.FirstOrDefault(x => x.Key.Equals(key, StringComparison.CurrentCultureIgnoreCase));
            if (match.Key != null)
            {
                value = match.Value ?? string.Empty;
                return true;
            }
            value = string.Empty;
            return false;
        }

        private static string Limit(string? value, int max)
        {
            string text = value ?? string.Empty;
            return text.Length <= max ? text : text[..max];
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
            _client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(8) };
        }

        public static bool IsLoopbackEndpoint(string endpoint)
        {
            return Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                && uri.IsLoopback;
        }

        public async Task<TcLearningDigest> LearnProfileAsync(
            string endpoint,
            string model,
            string manualRules,
            IReadOnlyList<TcExampleSet> exampleSets,
            IReadOnlyList<LocalPlanningDocument> learningDocuments,
            CancellationToken cancellationToken = default)
        {
            ValidateEndpointAndModel(endpoint, model);
            exampleSets ??= Array.Empty<TcExampleSet>();
            learningDocuments ??= Array.Empty<LocalPlanningDocument>();
            if (string.IsNullOrWhiteSpace(manualRules) && exampleSets.Count == 0 && learningDocuments.Count == 0)
                throw new InvalidOperationException("직접 작성 규칙 또는 학습 자료를 한 개 이상 추가해주세요.");

            IReadOnlyList<string> canonicalColumns = LocalTestCaseEngine.ChooseCanonicalColumns(exampleSets);
            string examplesText = BuildExamplePrompt(exampleSets);
            string documentText = BuildDocumentPrompt(learningDocuments);
            string columnInstruction = canonicalColumns.Count > 0
                ? "기존 TC에서 관찰된 컬럼은 다음과 같다. 컬럼명과 순서를 그대로 유지해야 한다: " + string.Join(" | ", canonicalColumns)
                : "고정 컬럼을 가정하지 말고, 학습 자료에서 실제 출력 구조를 추론하라.";

            string system =
                "You are a senior QA methodology analyst running in a strictly local-only desktop application. " +
                "Your job is to learn a project's test-case writing conventions from user-provided local examples, guides, planning documents and images. " +
                "Do not invent company rules. Do not assume any universal TC schema. Return JSON only.";

            string user =
                "[사용자가 직접 설명한 작성 규칙]\n" + Empty(manualRules, "(없음)") + "\n\n" +
                "[기존 작성 TC 예시]\n" + examplesText + "\n\n" +
                "[TC 작성 가이드/관련 기획서/이미지에서 로컬 추출한 내용]\n" + documentText + "\n\n" +
                "[스키마 규칙]\n" + columnInstruction + "\n\n" +
                "분석할 항목:\n" +
                "1. 실제 컬럼명과 컬럼 순서\n" +
                "2. 문장 톤, 용어, 번호/기호/개행 방식, 상세 수준\n" +
                "3. TC 분리 기준, 정상/예외/경계/상태/권한 등의 커버리지 습관\n" +
                "4. 반드시 지켜야 할 규칙과 하지 말아야 할 표현\n" +
                "5. 서로 충돌하거나 확실하지 않은 규칙은 warnings에 기록\n\n" +
                "다음 JSON 객체만 반환:\n" +
                "{\"columns\":[\"실제 컬럼명\"],\"ruleSummary\":\"...\",\"styleGuide\":\"...\",\"coverageGuide\":\"...\",\"warnings\":[\"...\"]}";

            string modelText = await SendChatAsync(endpoint, model, system, user, CollectImages(learningDocuments), cancellationToken).ConfigureAwait(false);
            TcLearningDigest digest = DeserializeLearningDigest(modelText);
            if (canonicalColumns.Count > 0) digest.Columns = canonicalColumns.ToList();
            digest.Columns = DistinctColumns(digest.Columns);
            digest.Warnings ??= new List<string>();

            if (exampleSets.Select(x => string.Join("\u001F", x.Columns)).Distinct(StringComparer.CurrentCultureIgnoreCase).Count() > 1)
                digest.Warnings.Add("학습한 기존 TC 파일들의 컬럼 구조가 서로 달라 가장 많이 관찰된 구조를 대표 스키마로 사용합니다.");

            return digest;
        }

        public async Task<GeneratedTcBatch> GenerateWithOllamaAsync(
            string endpoint,
            string model,
            string requirement,
            TcLearningProfile profile,
            IReadOnlyList<LocalPlanningDocument> documents,
            CancellationToken cancellationToken = default)
        {
            ValidateEndpointAndModel(endpoint, model);
            ArgumentNullException.ThrowIfNull(profile);
            documents ??= Array.Empty<LocalPlanningDocument>();
            if (string.IsNullOrWhiteSpace(requirement) && documents.Count == 0)
                throw new InvalidOperationException("이번 TC 생성에 사용할 요구사항 또는 기획서/이미지를 추가해주세요.");

            string columnsText = profile.LearnedColumns.Count > 0
                ? string.Join(" | ", profile.LearnedColumns)
                : "(고정 컬럼 없음 - 학습 프로필과 예시에서 적절한 컬럼을 결정)";
            string examplesText = profile.RepresentativeExamples.Count == 0
                ? "(저장된 대표 TC 예시 없음)"
                : JsonSerializer.Serialize(profile.RepresentativeExamples);

            string system =
                "You are a senior QA test-case designer running inside a strictly local-only Windows desktop application. " +
                "The project's learned TC profile is authoritative. Never force a universal ID/title/precondition/steps/expected/priority/type schema. " +
                "Match the learned columns, terminology, tone, detail and formatting. Use only the supplied local material. Return JSON only.";

            string user =
                "[프로젝트 프로필]\n" + profile.Name + "\n\n" +
                "[사용자 직접 규칙]\n" + Empty(profile.ManualRules, "(없음)") + "\n\n" +
                "[학습된 필수 컬럼/순서]\n" + columnsText + "\n\n" +
                "[학습된 규칙 요약]\n" + Empty(profile.LearnedRuleSummary, "(없음)") + "\n\n" +
                "[학습된 문장/표현 스타일]\n" + Empty(profile.LearnedStyleGuide, "(없음)") + "\n\n" +
                "[학습된 커버리지 기준]\n" + Empty(profile.LearnedCoverageGuide, "(없음)") + "\n\n" +
                "[대표 기존 TC 예시 - 형식과 표현을 모방]\n" + examplesText + "\n\n" +
                "[이번 추가 요구사항/메모]\n" + Empty(requirement, "(없음 - 기획서를 기준으로 생성)") + "\n\n" +
                "[이번 기획서/이미지에서 로컬 추출한 내용]\n" + BuildDocumentPrompt(documents) + "\n\n" +
                "생성 원칙:\n" +
                "1. 기존 프로필의 컬럼이 있으면 이름과 순서를 정확히 유지한다. 새 고정 컬럼을 임의로 추가하지 않는다.\n" +
                "2. 기존 TC 예시의 말투, 용어, 상세 수준, 줄바꿈/번호 표현을 최대한 동일하게 맞춘다.\n" +
                "3. 이번 기획서의 기능/조건/상태/예외/화면 정보를 근거로 TC를 만든다.\n" +
                "4. 자료에 없는 정책은 사실처럼 만들지 않는다.\n" +
                "5. 중복 TC를 만들지 않는다.\n" +
                "6. 컬럼이 학습되지 않았다면 이번 프로필/자료에 맞는 구조를 스스로 제안하되 특정 표준 7컬럼을 기본값으로 사용하지 않는다.\n\n" +
                "다음 JSON 객체만 반환:\n" +
                "{\"columns\":[\"컬럼1\",\"컬럼2\"],\"cases\":[{\"fields\":{\"컬럼1\":\"값\",\"컬럼2\":\"값\"}}]}";

            string modelText = await SendChatAsync(endpoint, model, system, user, CollectImages(documents), cancellationToken).ConfigureAwait(false);
            GeneratedTcBatch batch = DeserializeGeneratedBatch(modelText, profile.LearnedColumns);
            if (batch.Columns.Count == 0 || batch.Cases.Count == 0)
                throw new InvalidDataException("로컬 모델이 동적 TC 구조를 반환하지 않았습니다.");
            return batch;
        }

        public void Dispose() => _client.Dispose();

        private async Task<string> SendChatAsync(
            string endpoint,
            string model,
            string system,
            string user,
            string[] images,
            CancellationToken cancellationToken)
        {
            var baseUri = new Uri(endpoint.EndsWith('/') ? endpoint : endpoint + "/", UriKind.Absolute);
            var requestUri = new Uri(baseUri, "api/chat");
            if (!requestUri.IsLoopback) throw new InvalidOperationException("로컬 전용 보안 검증에 실패했습니다.");

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
                options = new { temperature = 0.12, num_ctx = 32768 }
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
            return doc.RootElement.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
        }

        private static TcLearningDigest DeserializeLearningDigest(string modelText)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            TcLearningDigest? digest = JsonSerializer.Deserialize<TcLearningDigest>(CleanJson(modelText), options);
            return digest ?? throw new InvalidDataException("로컬 모델이 학습 프로필 JSON을 반환하지 않았습니다.");
        }

        private static GeneratedTcBatch DeserializeGeneratedBatch(string modelText, IReadOnlyList<string> learnedColumns)
        {
            string text = CleanJson(modelText);
            using JsonDocument doc = JsonDocument.Parse(text);
            JsonElement root = doc.RootElement;
            JsonElement casesElement;
            List<string> columns = new();

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("columns", out JsonElement cols) && cols.ValueKind == JsonValueKind.Array)
                    columns.AddRange(cols.EnumerateArray().Select(x => x.GetString() ?? string.Empty));
                if (!root.TryGetProperty("cases", out casesElement) || casesElement.ValueKind != JsonValueKind.Array)
                    throw new InvalidDataException("로컬 모델 응답에 cases 배열이 없습니다.");
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                casesElement = root;
            }
            else throw new InvalidDataException("로컬 모델의 TC JSON 형식을 해석하지 못했습니다.");

            if (learnedColumns.Count > 0) columns = learnedColumns.ToList();
            columns = DistinctColumns(columns);
            bool inferColumnsFromRows = columns.Count == 0;
            var rows = new List<DynamicTestCase>();

            foreach (JsonElement item in casesElement.EnumerateArray().Take(80))
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                JsonElement fieldsElement = item;
                if (item.TryGetProperty("fields", out JsonElement fields) && fields.ValueKind == JsonValueKind.Object)
                    fieldsElement = fields;

                var map = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);
                foreach (JsonProperty property in fieldsElement.EnumerateObject())
                {
                    if (property.NameEquals("fields")) continue;
                    string value = JsonValueToText(property.Value);
                    map[property.Name] = NormalizeMultiline(value);
                    if (inferColumnsFromRows && !columns.Any(x => x.Equals(property.Name, StringComparison.CurrentCultureIgnoreCase)))
                        columns.Add(property.Name);
                }
                if (map.Count > 0) rows.Add(new DynamicTestCase { Fields = map });
            }

            if (columns.Count == 0 && rows.Count > 0) columns = rows[0].Fields.Keys.ToList();
            columns = DistinctColumns(columns);
            return new GeneratedTcBatch { Columns = columns, Cases = rows };
        }

        private static string BuildExamplePrompt(IReadOnlyList<TcExampleSet> sets)
        {
            if (sets == null || sets.Count == 0) return "(기존 TC 예시 없음)";
            var sb = new StringBuilder();
            foreach (TcExampleSet set in sets.Take(6))
            {
                sb.AppendLine($"--- {set.FileName} / columns: {string.Join(" | ", set.Columns)} / total rows: {set.TotalRowCount} ---");
                foreach (Dictionary<string, string> row in set.Rows.Take(8))
                    sb.AppendLine(JsonSerializer.Serialize(row));
            }
            return sb.ToString().Trim();
        }

        private static string BuildDocumentPrompt(IReadOnlyList<LocalPlanningDocument> documents)
        {
            if (documents == null || documents.Count == 0) return "(첨부 문서 없음)";
            var sb = new StringBuilder();
            foreach (LocalPlanningDocument document in documents)
            {
                if (sb.Length >= MaxCombinedDocumentTextChars) break;
                sb.AppendLine($"\n--- {document.FileName} / {document.Kind} / 단위 {document.UnitCount} / 이미지 {document.Images.Count}개 ---");
                if (!string.IsNullOrWhiteSpace(document.Warning)) sb.AppendLine("[추출 참고] " + document.Warning);
                if (string.IsNullOrWhiteSpace(document.ExtractedText)) sb.AppendLine("(텍스트 없음 - 첨부 이미지가 있으면 Vision 입력으로 분석)");
                else AppendLimited(sb, document.ExtractedText, MaxCombinedDocumentTextChars);
            }
            return sb.ToString().Trim();
        }

        private static string[] CollectImages(IReadOnlyList<LocalPlanningDocument> documents)
        {
            return (documents ?? Array.Empty<LocalPlanningDocument>())
                .SelectMany(d => d.Images ?? Array.Empty<LocalDocumentImage>())
                .Where(x => x.Bytes is { Length: > 0 } && x.Bytes.Length <= MaxSingleVisionImageBytes)
                .Take(MaxVisionImages)
                .Select(x => Convert.ToBase64String(x.Bytes))
                .ToArray();
        }

        private static void ValidateEndpointAndModel(string endpoint, string model)
        {
            if (!IsLoopbackEndpoint(endpoint))
                throw new InvalidOperationException("보안 정책상 로컬 TC 기능은 localhost/127.0.0.1/::1 주소에만 연결할 수 있습니다.");
            if (string.IsNullOrWhiteSpace(model)) throw new InvalidOperationException("로컬 모델명이 설정되지 않았습니다.");
        }

        private static List<string> DistinctColumns(IEnumerable<string>? source)
        {
            var result = new List<string>();
            foreach (string raw in source ?? Array.Empty<string>())
            {
                string value = (raw ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(value)) continue;
                if (result.Any(x => x.Equals(value, StringComparison.CurrentCultureIgnoreCase))) continue;
                result.Add(value);
                if (result.Count >= 40) break;
            }
            return result;
        }

        private static string JsonValueToText(JsonElement value)
        {
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.Null => string.Empty,
                JsonValueKind.Array => string.Join("\n", value.EnumerateArray().Select(JsonValueToText)),
                JsonValueKind.Object => value.GetRawText(),
                _ => value.ToString()
            };
        }

        private static string CleanJson(string value)
        {
            string text = (value ?? string.Empty).Trim();
            if (text.StartsWith("```", StringComparison.Ordinal))
            {
                int firstNewline = text.IndexOf('\n');
                if (firstNewline >= 0) text = text[(firstNewline + 1)..];
                int lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
                if (lastFence >= 0) text = text[..lastFence];
            }
            return text.Trim();
        }

        private static string NormalizeMultiline(string? value)
        {
            return (value ?? string.Empty).Replace("\r\n", "\n").Replace("\n", "\r\n").Trim();
        }

        private static string Empty(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

        private static void AppendLimited(StringBuilder builder, string value, int maxChars)
        {
            if (builder.Length >= maxChars || string.IsNullOrEmpty(value)) return;
            int available = maxChars - builder.Length;
            if (value.Length <= available) builder.AppendLine(value);
            else builder.Append(value.AsSpan(0, available));
        }

        private static string TrimForMessage(string value)
        {
            string text = (value ?? string.Empty).Trim();
            return text.Length > 500 ? text[..500] + "..." : text;
        }
    }
}
