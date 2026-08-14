using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AppiumBuilder.Core
{
    public sealed class TcGenerationGuide
    {
        public string Name { get; set; } = "기본 가이드";
        public string Rules { get; set; } = string.Empty;
        public string TemplateName { get; set; } = "기본 TC 양식";
        public List<string> TemplateColumns { get; set; } = LocalTestCaseTemplate.DefaultColumns.ToList();
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// 회사/프로젝트별 TC 작성 규칙을 로컬 PC에만 저장한다.
    /// </summary>
    public static class TcGenerationGuideStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public static string StoreFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AppiumBuilderReborn",
            "TC");

        public static string StorePath => Path.Combine(StoreFolder, "tc_generation_guides.json");

        public static IReadOnlyList<TcGenerationGuide> Load()
        {
            try
            {
                if (!File.Exists(StorePath)) return new[] { CreateDefault() };
                string json = File.ReadAllText(StorePath);
                List<TcGenerationGuide>? guides = JsonSerializer.Deserialize<List<TcGenerationGuide>>(json, JsonOptions);
                if (guides == null || guides.Count == 0) return new[] { CreateDefault() };

                return guides
                    .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                    .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToArray();
            }
            catch
            {
                return new[] { CreateDefault() };
            }
        }

        public static void SaveOrUpdate(
            string name,
            string rules,
            string? templateName = null,
            IReadOnlyList<string>? templateColumns = null)
        {
            name = (name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("가이드 이름을 입력해주세요.", nameof(name));

            var guides = Load().ToList();
            TcGenerationGuide? existing = guides.FirstOrDefault(x =>
                x.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));

            if (existing == null)
            {
                guides.Add(new TcGenerationGuide
                {
                    Name = name,
                    Rules = rules ?? string.Empty,
                    TemplateName = string.IsNullOrWhiteSpace(templateName) ? "기본 TC 양식" : templateName.Trim(),
                    TemplateColumns = (templateColumns == null || templateColumns.Count == 0 ? LocalTestCaseTemplate.DefaultColumns : templateColumns).ToList(),
                    UpdatedAtUtc = DateTime.UtcNow
                });
            }
            else
            {
                existing.Name = name;
                existing.Rules = rules ?? string.Empty;
                existing.TemplateName = string.IsNullOrWhiteSpace(templateName) ? existing.TemplateName : templateName.Trim();
                existing.TemplateColumns = (templateColumns == null || templateColumns.Count == 0 ? existing.TemplateColumns : templateColumns).ToList();
                existing.UpdatedAtUtc = DateTime.UtcNow;
            }

            Persist(guides);
        }

        public static bool Delete(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var guides = Load().ToList();
            int removed = guides.RemoveAll(x => x.Name.Equals(name.Trim(), StringComparison.CurrentCultureIgnoreCase));
            if (removed == 0) return false;
            if (guides.Count == 0) guides.Add(CreateDefault());
            Persist(guides);
            return true;
        }

        private static void Persist(IEnumerable<TcGenerationGuide> guides)
        {
            Directory.CreateDirectory(StoreFolder);
            string temp = StorePath + ".tmp";
            string json = JsonSerializer.Serialize(guides.OrderBy(x => x.Name).ToArray(), JsonOptions);
            File.WriteAllText(temp, json);
            File.Move(temp, StorePath, overwrite: true);
        }

        private static TcGenerationGuide CreateDefault() => new()
        {
            Name = "기본 가이드",
            TemplateName = "기본 TC 양식",
            TemplateColumns = LocalTestCaseTemplate.DefaultColumns.ToList(),
            Rules =
                "- 요구사항/기획서에 근거해서만 TC를 작성한다.\r\n" +
                "- 정상, 예외, 경계값 케이스를 기능에 맞게 포함한다.\r\n" +
                "- 사전조건은 실행 전에 필요한 상태를 구체적으로 작성한다.\r\n" +
                "- 테스트 절차는 번호형으로 실행 가능하게 작성한다.\r\n" +
                "- 기대결과는 관찰 가능한 결과로 명확하게 작성한다.\r\n" +
                "- 기획서에 없는 정책을 임의로 단정하지 말고 필요한 경우 확인 필요로 표시한다."
        };
    }
}
