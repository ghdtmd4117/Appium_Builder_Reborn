using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AppiumBuilder.Core
{
    public sealed class TcLearningProfile
    {
        public string Name { get; set; } = "기본 프로필";
        public string ManualRules { get; set; } = string.Empty;
        public List<string> LearnedColumns { get; set; } = new();
        public string LearnedRuleSummary { get; set; } = string.Empty;
        public string LearnedStyleGuide { get; set; } = string.Empty;
        public string LearnedCoverageGuide { get; set; } = string.Empty;
        public List<string> LearnedWarnings { get; set; } = new();
        public List<string> LearningSourceNames { get; set; } = new();
        public List<Dictionary<string, string>> RepresentativeExamples { get; set; } = new();
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public bool HasLearning => LearnedColumns.Count > 0
            || !string.IsNullOrWhiteSpace(LearnedRuleSummary)
            || !string.IsNullOrWhiteSpace(LearnedStyleGuide)
            || RepresentativeExamples.Count > 0;
    }

    /// <summary>
    /// 회사/팀/프로젝트별 TC 작성 방식을 로컬 PC에만 저장한다.
    /// 모델 파라미터를 재학습하는 대신, 학습 자료에서 추출한 스키마/규칙/스타일/예시를
    /// 프로젝트 프로필로 누적하고 TC 생성 때마다 로컬 모델에 컨텍스트로 제공한다.
    /// </summary>
    public static class TcLearningProfileStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private static readonly string[] LegacyDefaultColumns =
        {
            "TC ID", "제목", "사전조건", "테스트 절차", "기대결과", "우선순위", "유형"
        };

        public static string StoreFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AppiumBuilderReborn",
            "TC");

        public static string StorePath => Path.Combine(StoreFolder, "tc_learning_profiles.json");
        private static string LegacyStorePath => Path.Combine(StoreFolder, "tc_generation_guides.json");

        public static IReadOnlyList<TcLearningProfile> Load()
        {
            try
            {
                if (File.Exists(StorePath))
                {
                    string json = File.ReadAllText(StorePath);
                    List<TcLearningProfile>? profiles = JsonSerializer.Deserialize<List<TcLearningProfile>>(json, JsonOptions);
                    if (profiles is { Count: > 0 })
                    {
                        return profiles
                            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                            .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                            .ToArray();
                    }
                }

                IReadOnlyList<TcLearningProfile> migrated = TryMigrateLegacy();
                if (migrated.Count > 0)
                {
                    Persist(migrated);
                    return migrated;
                }
            }
            catch
            {
                // 손상된 프로필은 기본 프로필로 안전하게 시작한다.
            }

            return new[] { CreateDefault() };
        }

        public static void SaveOrUpdate(TcLearningProfile profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            string name = (profile.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("프로필 이름을 입력해주세요.", nameof(profile));

            var profiles = Load().ToList();
            int index = profiles.FindIndex(x => x.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));

            profile.Name = name;
            profile.ManualRules ??= string.Empty;
            profile.LearnedColumns = DistinctColumns(profile.LearnedColumns);
            profile.LearnedWarnings ??= new List<string>();
            profile.LearningSourceNames = (profile.LearningSourceNames ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .Take(50)
                .ToList();
            profile.RepresentativeExamples = NormalizeExamples(profile.RepresentativeExamples, profile.LearnedColumns);
            profile.UpdatedAtUtc = DateTime.UtcNow;

            if (index >= 0) profiles[index] = profile;
            else profiles.Add(profile);

            Persist(profiles);
        }

        public static bool Delete(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var profiles = Load().ToList();
            int removed = profiles.RemoveAll(x => x.Name.Equals(name.Trim(), StringComparison.CurrentCultureIgnoreCase));
            if (removed == 0) return false;
            if (profiles.Count == 0) profiles.Add(CreateDefault());
            Persist(profiles);
            return true;
        }

        public static TcLearningProfile CreateDefault() => new()
        {
            Name = "기본 프로필",
            ManualRules =
                "- 제공된 기획서/예시 TC/직접 입력 규칙을 가장 우선해서 따른다.\r\n" +
                "- 자료에 없는 정책, 수치, 화면 동작을 임의로 확정하지 않는다.\r\n" +
                "- 기존 TC 예시가 있으면 컬럼명, 문장 톤, 상세 수준, 표기 방식을 최대한 동일하게 맞춘다."
        };

        private static IReadOnlyList<TcLearningProfile> TryMigrateLegacy()
        {
            if (!File.Exists(LegacyStorePath)) return Array.Empty<TcLearningProfile>();
            try
            {
                string json = File.ReadAllText(LegacyStorePath);
                List<LegacyGuide>? legacy = JsonSerializer.Deserialize<List<LegacyGuide>>(json, JsonOptions);
                if (legacy == null || legacy.Count == 0) return Array.Empty<TcLearningProfile>();

                return legacy
                    .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                    .Select(x => new TcLearningProfile
                    {
                        Name = x.Name.Trim(),
                        ManualRules = x.Rules ?? string.Empty,
                        // 이전 기본 7컬럼은 더 이상 강제하지 않는다. 사용자가 만든 커스텀 컬럼만 마이그레이션한다.
                        LearnedColumns = IsLegacyDefault(x.TemplateColumns)
                            ? new List<string>()
                            : DistinctColumns(x.TemplateColumns),
                        LearnedRuleSummary = "이전 TC 생성 가이드에서 마이그레이션됨",
                        LearningSourceNames = new List<string> { "Legacy TC Guide" },
                        UpdatedAtUtc = DateTime.UtcNow
                    })
                    .ToArray();
            }
            catch
            {
                return Array.Empty<TcLearningProfile>();
            }
        }

        private static bool IsLegacyDefault(IReadOnlyList<string>? columns)
        {
            if (columns == null || columns.Count != LegacyDefaultColumns.Length) return false;
            return columns.Select(x => x.Trim()).SequenceEqual(LegacyDefaultColumns, StringComparer.CurrentCultureIgnoreCase);
        }

        private static List<string> DistinctColumns(IEnumerable<string>? columns)
        {
            var result = new List<string>();
            if (columns == null) return result;
            foreach (string column in columns)
            {
                string value = (column ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(value)) continue;
                if (result.Any(x => x.Equals(value, StringComparison.CurrentCultureIgnoreCase))) continue;
                result.Add(value);
                if (result.Count >= 40) break;
            }
            return result;
        }

        private static List<Dictionary<string, string>> NormalizeExamples(
            IEnumerable<Dictionary<string, string>>? examples,
            IReadOnlyList<string> columns)
        {
            var result = new List<Dictionary<string, string>>();
            if (examples == null) return result;

            foreach (Dictionary<string, string> example in examples.Take(8))
            {
                var row = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);
                IEnumerable<string> keys = columns.Count > 0 ? columns : example.Keys;
                foreach (string key in keys)
                {
                    if (example.TryGetValue(key, out string? value))
                        row[key] = Limit(value, 2000);
                }
                if (row.Count > 0) result.Add(row);
            }
            return result;
        }

        private static string Limit(string? value, int max)
        {
            string text = value ?? string.Empty;
            return text.Length <= max ? text : text[..max];
        }

        private static void Persist(IEnumerable<TcLearningProfile> profiles)
        {
            Directory.CreateDirectory(StoreFolder);
            string temp = StorePath + ".tmp";
            string json = JsonSerializer.Serialize(profiles.OrderBy(x => x.Name).ToArray(), JsonOptions);
            File.WriteAllText(temp, json);
            File.Move(temp, StorePath, overwrite: true);
        }

        private sealed class LegacyGuide
        {
            public string Name { get; set; } = string.Empty;
            public string Rules { get; set; } = string.Empty;
            public string TemplateName { get; set; } = string.Empty;
            public List<string> TemplateColumns { get; set; } = new();
        }
    }
}
