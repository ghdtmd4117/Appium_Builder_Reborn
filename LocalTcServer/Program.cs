using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AppiumBuilder.Core;
using Microsoft.AspNetCore.Http.Features;

const int Port = 7788;
const long MaxRequestBytes = 350L * 1024 * 1024;
const int MaxFilesPerCategory = 30;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("LOCAL_TC_SERVER_URLS") ?? $"http://0.0.0.0:{Port}");
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = MaxRequestBytes;
    options.ValueLengthLimit = 4 * 1024 * 1024;
});
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = MaxRequestBytes);

var app = builder.Build();
var queue = new SemaphoreSlim(1, 1);
int waitingRequests = 0;
string accessToken = ServerTokenStore.GetOrCreateToken();

string configuredModel = (Environment.GetEnvironmentVariable("LOCAL_TC_MODEL") ?? "qwen3-vl:4b").Trim();
if (LocalAiRuntimeManager.GetModelOption(configuredModel) == null)
    configuredModel = "qwen3-vl:4b";
if (!string.Equals(LocalAiRuntimeManager.SelectedModel, configuredModel, StringComparison.OrdinalIgnoreCase))
    LocalAiRuntimeManager.SetSelectedModel(configuredModel);

app.Use(async (context, next) =>
{
    if (context.Request.Path == "/" || context.Request.Path == "/api/ping")
    {
        await next();
        return;
    }

    string supplied = context.Request.Headers["X-Local-TC-Token"].ToString();
    if (!ServerTokenStore.FixedTimeEquals(accessToken, supplied))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { error = "Local TC Server 인증 토큰이 올바르지 않습니다." });
        return;
    }
    await next();
});

app.MapGet("/", () => Results.Text(
    "Appium Builder Reborn · Local TC Server\n" +
    "이 서버는 사내 네트워크에서 Local TC Studio AI 연산을 대신 처리합니다.\n" +
    "상태 확인: /api/health"));

app.MapGet("/api/ping", () => Results.Json(new { ok = true, service = "Local TC Server" }));

app.MapGet("/api/health", async (CancellationToken ct) =>
{
    LocalAiRuntimeManager.Status status = await LocalAiRuntimeManager.GetStatusAsync(ct);
    LocalAiRuntimeManager.ModelOption? option = LocalAiRuntimeManager.GetModelOption(status.SelectedModel);
    return Results.Json(new LocalTcServerHealth
    {
        Ready = status.Ready,
        Model = status.SelectedModel,
        ModelDisplayName = option?.DisplayName ?? status.SelectedModel,
        Message = status.Ready ? "AI 서버 준비됨" : BuildStatusMessage(status),
        QueueDepth = Math.Max(0, Volatile.Read(ref waitingRequests))
    });
});

app.MapPost("/api/learn", async (HttpRequest request, CancellationToken ct) =>
{
    Interlocked.Increment(ref waitingRequests);
    await queue.WaitAsync(ct);
    Interlocked.Decrement(ref waitingRequests);
    string tempRoot = CreateTempRoot();
    try
    {
        await EnsureServerAiReadyAsync(ct);
        IFormCollection form = await request.ReadFormAsync(ct);
        string manualRules = form["manualRules"].ToString();
        IFormFile[] exampleFiles = form.Files.GetFiles("examples").Take(MaxFilesPerCategory).ToArray();
        IFormFile[] documentFiles = form.Files.GetFiles("documents").Take(MaxFilesPerCategory).ToArray();

        if (string.IsNullOrWhiteSpace(manualRules) && exampleFiles.Length == 0 && documentFiles.Length == 0)
            return Results.BadRequest(new { error = "작성 규칙 또는 학습 파일을 한 개 이상 보내주세요." });

        List<string> examplePaths = await SaveFilesAsync(exampleFiles, Path.Combine(tempRoot, "examples"), ct);
        List<string> documentPaths = await SaveFilesAsync(documentFiles, Path.Combine(tempRoot, "documents"), ct);

        TcExampleSet[] examples = examplePaths.Select(LocalTestCaseEngine.ReadExampleSet).ToArray();
        var docs = new List<LocalPlanningDocument>();
        foreach (string path in documentPaths)
            docs.Add(await LocalPlanningDocumentReader.ReadAsync(path, ct));

        using var client = new LocalOnlyLlmClient();
        TcLearningDigest digest = await client.LearnProfileAsync(
            LocalAiRuntimeManager.Endpoint,
            LocalAiRuntimeManager.SelectedModel,
            manualRules,
            examples,
            docs,
            ct);

        var result = new RemoteLearningResult
        {
            Digest = digest,
            RepresentativeExamples = LocalTestCaseEngine.BuildRepresentativeExamples(examples, digest.Columns),
            SourceNames = exampleFiles.Select(x => "기존 TC · " + SafeFileName(x.FileName))
                .Concat(documentFiles.Select(x => "자료 · " + SafeFileName(x.FileName)))
                .ToList()
        };
        return Results.Json(result);
    }
    catch (Exception ex)
    {
        return Results.Problem(title: "프로필 학습 실패", detail: FriendlyError(ex), statusCode: 500);
    }
    finally
    {
        queue.Release();
        TryDeleteDirectory(tempRoot);
    }
});

app.MapPost("/api/generate", async (HttpRequest request, CancellationToken ct) =>
{
    Interlocked.Increment(ref waitingRequests);
    await queue.WaitAsync(ct);
    Interlocked.Decrement(ref waitingRequests);
    string tempRoot = CreateTempRoot();
    try
    {
        await EnsureServerAiReadyAsync(ct);
        IFormCollection form = await request.ReadFormAsync(ct);
        string requirement = form["requirement"].ToString();
        string profileJson = form["profileJson"].ToString();
        TcLearningProfile profile = JsonSerializer.Deserialize<TcLearningProfile>(profileJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidDataException("학습 프로필 JSON을 읽지 못했습니다.");

        IFormFile[] documentFiles = form.Files.GetFiles("documents").Take(MaxFilesPerCategory).ToArray();
        if (string.IsNullOrWhiteSpace(requirement) && documentFiles.Length == 0)
            return Results.BadRequest(new { error = "TC 설명 또는 기획서/이미지를 한 개 이상 보내주세요." });

        List<string> documentPaths = await SaveFilesAsync(documentFiles, Path.Combine(tempRoot, "documents"), ct);
        var docs = new List<LocalPlanningDocument>();
        foreach (string path in documentPaths)
            docs.Add(await LocalPlanningDocumentReader.ReadAsync(path, ct));

        using var client = new LocalOnlyLlmClient();
        GeneratedTcBatch generated = await client.GenerateWithOllamaAsync(
            LocalAiRuntimeManager.Endpoint,
            LocalAiRuntimeManager.SelectedModel,
            requirement,
            profile,
            docs,
            ct);

        return Results.Json(new
        {
            columns = generated.Columns,
            cases = generated.Cases.Select(x => x.Fields).ToArray()
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(title: "TC 생성 실패", detail: FriendlyError(ex), statusCode: 500);
    }
    finally
    {
        queue.Release();
        TryDeleteDirectory(tempRoot);
    }
});

app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.WriteLine();
    Console.WriteLine("============================================================");
    Console.WriteLine(" Appium Builder Reborn · Local TC Server");
    Console.WriteLine($" Listen : {Environment.GetEnvironmentVariable("LOCAL_TC_SERVER_URLS") ?? $"http://0.0.0.0:{Port}"}");
    Console.WriteLine($" Model  : {LocalAiRuntimeManager.SelectedModel}");
    Console.WriteLine($" Token  : {accessToken}");
    Console.WriteLine(" ※ 이 Token을 Local TC Studio의 '사내 AI 서버' 설정에 입력하세요.");
    Console.WriteLine(" ※ 기본 HTTP는 신뢰된 사내 LAN에서만 사용하세요. 민감 환경은 HTTPS reverse proxy를 권장합니다.");
    Console.WriteLine("============================================================");
    Console.WriteLine();

    _ = Task.Run(async () =>
    {
        try
        {
            var progress = new Progress<LocalAiRuntimeManager.ProgressInfo>(p =>
                Console.WriteLine($"[{p.Stage}] {p.Detail}{(p.Percent.HasValue ? $" · {p.Percent}%" : string.Empty)}"));
            var ready = await LocalAiRuntimeManager.EnsureReadyAsync(progress);
            Console.WriteLine(ready.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine("AI 준비 실패: " + FriendlyError(ex));
        }
    });
});

await app.RunAsync();

static async Task EnsureServerAiReadyAsync(CancellationToken ct)
{
    LocalAiRuntimeManager.Status status = await LocalAiRuntimeManager.GetStatusAsync(ct);
    if (status.Ready) return;
    var result = await LocalAiRuntimeManager.EnsureReadyAsync(cancellationToken: ct);
    if (!result.Success) throw new InvalidOperationException(result.Message);
}

static string BuildStatusMessage(LocalAiRuntimeManager.Status status)
{
    if (!status.ModelSelected) return "서버 모델 선택 필요";
    if (!status.RuntimeAvailable && !status.ServerRunning) return "Ollama runtime 준비 중/필요";
    if (!status.ServerRunning) return "Ollama 서버 시작 필요";
    if (!status.ModelAvailable) return "선택 모델 다운로드/로드 필요";
    return "AI 준비 확인 중";
}

static string CreateTempRoot()
{
    string path = Path.Combine(Path.GetTempPath(), "AppiumBuilderReborn", "LocalTcServer", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(path);
    return path;
}

static async Task<List<string>> SaveFilesAsync(IEnumerable<IFormFile> files, string folder, CancellationToken ct)
{
    Directory.CreateDirectory(folder);
    var result = new List<string>();
    int index = 0;
    foreach (IFormFile file in files)
    {
        ct.ThrowIfCancellationRequested();
        if (file.Length <= 0) continue;
        string name = SafeFileName(file.FileName);
        string path = Path.Combine(folder, $"{++index:D2}_{name}");
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, useAsync: true);
        await file.CopyToAsync(output, ct);
        result.Add(path);
    }
    return result;
}

static string SafeFileName(string fileName)
{
    string value = Path.GetFileName(fileName ?? string.Empty);
    foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
    return string.IsNullOrWhiteSpace(value) ? "upload.bin" : value;
}

static void TryDeleteDirectory(string path)
{
    try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch { }
}

static string FriendlyError(Exception ex)
{
    string message = ex.Message;
    if (message.Contains("allocate", StringComparison.OrdinalIgnoreCase)
        || message.Contains("CPU_REPACK", StringComparison.OrdinalIgnoreCase)
        || message.Contains("out of memory", StringComparison.OrdinalIgnoreCase))
        return "AI 서버 메모리가 부족합니다. 서버에서 더 작은 모델을 사용하거나 다른 프로그램의 메모리 사용량을 줄여주세요.";
    return message;
}

static class ServerTokenStore
{
    public static string TokenPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AppiumBuilderReborn",
        "LocalTcServer",
        "server-token.txt");

    public static string GetOrCreateToken()
    {
        string? fromEnv = Environment.GetEnvironmentVariable("LOCAL_TC_SERVER_TOKEN")?.Trim();
        if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv;

        try
        {
            if (File.Exists(TokenPath))
            {
                string existing = File.ReadAllText(TokenPath, Encoding.UTF8).Trim();
                if (existing.Length >= 24) return existing;
            }
        }
        catch { }

        Directory.CreateDirectory(Path.GetDirectoryName(TokenPath)!);
        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        File.WriteAllText(TokenPath, token, new UTF8Encoding(false));
        return token;
    }

    public static bool FixedTimeEquals(string expected, string actual)
    {
        if (string.IsNullOrWhiteSpace(actual)) return false;
        byte[] a = Encoding.UTF8.GetBytes(expected);
        byte[] b = Encoding.UTF8.GetBytes(actual.Trim());
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
}
