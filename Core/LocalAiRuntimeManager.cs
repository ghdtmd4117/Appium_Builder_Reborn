using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AppiumBuilder.Core
{
    /// <summary>
    /// Appium Builder Reborn이 사용하는 로컬 AI(Ollama)의 준비/실행 상태를 관리한다.
    /// - 설치 프로그램 없이 standalone Ollama runtime을 앱에서 직접 내려받아 사용한다.
    /// - 사용자가 Qwen3-VL 2B/4B 중 설치 모델을 직접 선택한다.
    /// - TC 내용은 localhost API에만 전달한다.
    /// - 앱이 시작한 Ollama process만 앱 종료 시 정리한다.
    /// </summary>
    public static class LocalAiRuntimeManager
    {
        public const string Host = "127.0.0.1";
        public const int Port = 11434;
        public const string Endpoint = "http://127.0.0.1:11434";
        public const string DefaultModel = "qwen3-vl:2b";

        public sealed record ModelOption(string Id, string DisplayName, string DownloadSize, string Description);

        public static IReadOnlyList<ModelOption> SupportedModels { get; } = new[]
        {
            new ModelOption(
                "qwen3-vl:2b",
                "Qwen3-VL 2B · 경량",
                "약 1.9GB",
                "메모리 부담이 적은 Vision 모델 · 일반적인 TC/기획서/이미지 분석에 권장"),
            new ModelOption(
                "qwen3-vl:4b",
                "Qwen3-VL 4B · 고품질",
                "약 3.3GB",
                "더 높은 분석 품질을 우선하는 Vision 모델 · 충분한 메모리 여유가 필요")
        };

        // 재현 가능성과 supply-chain 검증을 위해 앱 버전에서 검증된 Ollama release를 고정한다.
        public const string OllamaVersion = "v0.32.5";
        public const string RuntimeFileName = "ollama-windows-amd64.zip";
        public const long RuntimeDownloadBytes = 1_457_824_795L;
        public const string RuntimeSha256 = "7c941ae084569d298062d29f8139163a3187c76dbca0479c70d085e78fd8c7bb";
        public static readonly Uri RuntimeDownloadUri = new(
            $"https://github.com/ollama/ollama/releases/download/{OllamaVersion}/{RuntimeFileName}");

        private static readonly object Sync = new();
        private static readonly SemaphoreSlim PrepareGate = new(1, 1);
        private static readonly HttpClient LocalHttp = new(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false
        })
        {
            Timeout = TimeSpan.FromSeconds(2)
        };

        private static readonly HttpClient DownloadHttp = new(new HttpClientHandler
        {
            AllowAutoRedirect = true
        })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        private static readonly HttpClient LocalLongHttp = new(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false
        })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        private static Process? ownedServerProcess;

        public static string RuntimeRoot => Path.Combine(AppContext.BaseDirectory, "Runtime", "Ollama");
        public static string BundledExecutablePath => Path.Combine(RuntimeRoot, "ollama.exe");
        public static string ModelsRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AppiumBuilderReborn",
            "Ollama",
            "models");
        public static string SettingsFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AppiumBuilderReborn",
            "TC");
        public static string SelectedModelPath => Path.Combine(SettingsFolder, "selected-model.txt");

        public static string SelectedModel => LoadSelectedModel();
        public static bool HasSelectedModel => File.Exists(SelectedModelPath) && GetModelOption(LoadSelectedModel()) != null;

        public static ModelOption? GetModelOption(string? model)
        {
            if (string.IsNullOrWhiteSpace(model)) return null;
            return SupportedModels.FirstOrDefault(x => x.Id.Equals(model.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public static void SetSelectedModel(string model)
        {
            ModelOption? option = GetModelOption(model);
            if (option == null) throw new ArgumentException("지원하지 않는 로컬 AI 모델입니다.", nameof(model));
            Directory.CreateDirectory(SettingsFolder);
            File.WriteAllText(SelectedModelPath, option.Id, Encoding.UTF8);
        }

        private static string LoadSelectedModel()
        {
            try
            {
                if (!File.Exists(SelectedModelPath)) return string.Empty;
                string value = File.ReadAllText(SelectedModelPath, Encoding.UTF8).Trim();
                return GetModelOption(value)?.Id ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static bool OwnsRunningServer
        {
            get
            {
                lock (Sync)
                    return ownedServerProcess != null && !HasExitedSafe(ownedServerProcess);
            }
        }

        public sealed record Status(
            bool RuntimeAvailable,
            bool ServerRunning,
            bool ModelAvailable,
            bool OwnsServer,
            string RuntimePath,
            string SelectedModel,
            bool ModelSelected)
        {
            public bool Ready => ModelSelected && ServerRunning && ModelAvailable;
            public bool NeedsRuntimeDownload => !RuntimeAvailable;
            public bool NeedsModelDownload => ModelSelected && ServerRunning && !ModelAvailable;
        }

        public sealed record ProgressInfo(string Stage, string Detail, int? Percent = null);

        public static async Task<Status> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            string runtimePath = FindRuntimeExecutable() ?? string.Empty;
            bool runtimeAvailable = !string.IsNullOrWhiteSpace(runtimePath);
            bool serverRunning = await IsServerRunningAsync(cancellationToken).ConfigureAwait(false);
            string selectedModel = SelectedModel;
            bool modelSelected = !string.IsNullOrWhiteSpace(selectedModel);
            bool modelAvailable = false;

            if (serverRunning && modelSelected)
                modelAvailable = await HasModelAsync(selectedModel, cancellationToken).ConfigureAwait(false);

            return new Status(runtimeAvailable, serverRunning, modelAvailable, OwnsRunningServer, runtimePath, selectedModel, modelSelected);
        }

        /// <summary>
        /// 네트워크 다운로드 없이 이미 준비된 runtime이 있으면 자동으로 서버만 시작한다.
        /// 첫 실행 PC에서는 아무것도 다운로드하지 않고 즉시 반환한다.
        /// </summary>
        public static async Task TryAutoStartAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (await IsServerRunningAsync(cancellationToken).ConfigureAwait(false)) return;
                string? runtime = FindRuntimeExecutable();
                if (string.IsNullOrWhiteSpace(runtime)) return;
                await StartServerAsync(runtime, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // 자동 시작 실패는 TC Builder 화면에서 상태로 다시 안내한다.
            }
        }

        public static async Task<(bool Success, string Message)> EnsureReadyAsync(
            IProgress<ProgressInfo>? progress = null,
            CancellationToken cancellationToken = default)
        {
            await PrepareGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!OperatingSystem.IsWindows())
                    return (false, "내장 로컬 AI는 Windows에서만 지원합니다.");

                if (!Environment.Is64BitOperatingSystem)
                    return (false, "내장 Ollama runtime은 64-bit Windows가 필요합니다.");

                string model = SelectedModel;
                ModelOption? option = GetModelOption(model);
                if (option == null)
                    return (false, "설치할 로컬 AI 모델을 먼저 선택해주세요.");

                Directory.CreateDirectory(ModelsRoot);

                string? runtime = FindRuntimeExecutable();
                bool serverRunning = await IsServerRunningAsync(cancellationToken).ConfigureAwait(false);

                // 이미 사용자가 실행한 Ollama가 있으면 그대로 사용하고, 없을 때만 embedded runtime을 준비한다.
                if (!serverRunning)
                {
                    if (string.IsNullOrWhiteSpace(runtime))
                    {
                        progress?.Report(new ProgressInfo("runtime", "Ollama runtime 다운로드 준비", 0));
                        runtime = await DownloadAndInstallRuntimeAsync(progress, cancellationToken).ConfigureAwait(false);
                    }

                    progress?.Report(new ProgressInfo("server", "로컬 AI 엔진 시작 중"));
                    bool started = await StartServerAsync(runtime, cancellationToken).ConfigureAwait(false);
                    if (!started)
                        return (false, "Ollama runtime은 준비됐지만 로컬 AI 서버를 시작하지 못했습니다.");
                }

                if (!await HasModelAsync(model, cancellationToken).ConfigureAwait(false))
                {
                    progress?.Report(new ProgressInfo(
                        "model",
                        $"{option.DisplayName} 모델 다운로드 중 · 최초 1회 {option.DownloadSize}"));

                    bool pulled = await PullModelAsync(model, cancellationToken).ConfigureAwait(false);
                    if (!pulled)
                        return (false, $"{option.DisplayName} 모델 다운로드에 실패했습니다.");
                }

                bool ready = await IsServerRunningAsync(cancellationToken).ConfigureAwait(false)
                    && await HasModelAsync(model, cancellationToken).ConfigureAwait(false);

                if (!ready)
                    return (false, "로컬 AI 준비 확인에 실패했습니다.");

                progress?.Report(new ProgressInfo("ready", $"로컬 AI 준비 완료 · {option.DisplayName}", 100));
                return (true, $"로컬 AI 준비 완료 · {option.DisplayName}");
            }
            catch (OperationCanceledException)
            {
                return (false, "로컬 AI 준비가 취소되었습니다.");
            }
            catch (Exception ex)
            {
                return (false, "로컬 AI 준비 실패: " + ex.Message);
            }
            finally
            {
                PrepareGate.Release();
            }
        }

        public static async Task<bool> IsServerRunningAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint + "/api/tags");
                using HttpResponseMessage response = await LocalHttp.SendAsync(request, cancellationToken).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> HasModelAsync(string model, CancellationToken cancellationToken = default)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint + "/api/tags");
                using HttpResponseMessage response = await LocalHttp.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) return false;

                string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                using JsonDocument doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("models", out JsonElement models) || models.ValueKind != JsonValueKind.Array)
                    return false;

                foreach (JsonElement item in models.EnumerateArray())
                {
                    string name = item.TryGetProperty("name", out JsonElement nameEl) ? nameEl.GetString() ?? string.Empty : string.Empty;
                    string modelName = item.TryGetProperty("model", out JsonElement modelEl) ? modelEl.GetString() ?? string.Empty : string.Empty;
                    if (ModelMatches(name, model) || ModelMatches(modelName, model)) return true;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                return false;
            }
            return false;
        }

        public static bool StopOwnedServer(out string message)
        {
            Process? process;
            lock (Sync) process = ownedServerProcess;

            if (process == null || HasExitedSafe(process))
            {
                lock (Sync) ownedServerProcess = null;
                message = "Appium Builder가 시작한 로컬 AI 서버가 없습니다.";
                return false;
            }

            try
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(3000);
                lock (Sync) ownedServerProcess = null;
                message = "Appium Builder가 시작한 로컬 AI 서버를 종료했습니다.";
                return true;
            }
            catch (Exception ex)
            {
                message = "로컬 AI 서버 종료 실패: " + ex.Message;
                return false;
            }
        }

        private static string? FindRuntimeExecutable()
        {
            if (File.Exists(BundledExecutablePath)) return BundledExecutablePath;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "where.exe",
                    Arguments = "ollama.exe",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using Process? process = Process.Start(psi);
                if (process == null) return null;
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(2000);
                return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .FirstOrDefault(File.Exists);
            }
            catch
            {
                return null;
            }
        }

        private static async Task<string> DownloadAndInstallRuntimeAsync(
            IProgress<ProgressInfo>? progress,
            CancellationToken cancellationToken)
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "AppiumBuilderReborn", "OllamaBootstrap");
            string zipPath = Path.Combine(tempRoot, RuntimeFileName);
            string extractRoot = Path.Combine(tempRoot, "extract");

            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
            Directory.CreateDirectory(tempRoot);

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, RuntimeDownloadUri);
                request.Headers.UserAgent.ParseAdd("AppiumBuilderReborn/1.0");
                using HttpResponseMessage response = await DownloadHttp.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                long total = response.Content.Headers.ContentLength ?? RuntimeDownloadBytes;
                await using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

                // Windows에서 FileShare.None으로 열린 출력 스트림을 SHA 검사/압축 해제 단계가 다시 열면
                // 자기 자신과 파일 잠금 충돌이 난다. 다운로드 스트림을 블록 안에서 완전히 닫은 뒤 다음 단계로 진행한다.
                await using (var output = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, useAsync: true))
                {
                    byte[] buffer = new byte[1024 * 1024];
                    long received = 0;
                    int read;
                    while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                        received += read;
                        int percent = total > 0 ? (int)Math.Clamp(received * 100L / total, 0, 100) : 0;
                        progress?.Report(new ProgressInfo(
                            "runtime",
                            $"Ollama runtime 다운로드 · {FormatBytes(received)} / {FormatBytes(total)}",
                            percent));
                    }

                    await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                progress?.Report(new ProgressInfo("verify", "Ollama runtime 무결성 확인 중"));
                string actualHash = await ComputeSha256Async(zipPath, cancellationToken).ConfigureAwait(false);
                if (!actualHash.Equals(RuntimeSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Ollama runtime SHA-256 검증에 실패했습니다.");

                progress?.Report(new ProgressInfo("extract", "Ollama runtime 압축 해제 중"));
                Directory.CreateDirectory(extractRoot);
                ZipFile.ExtractToDirectory(zipPath, extractRoot, overwriteFiles: true);

                string? extractedExe = Directory.EnumerateFiles(extractRoot, "ollama.exe", SearchOption.AllDirectories).FirstOrDefault();
                if (string.IsNullOrWhiteSpace(extractedExe))
                    throw new InvalidDataException("Ollama runtime에서 ollama.exe를 찾지 못했습니다.");

                string sourceRoot = Path.GetDirectoryName(extractedExe)!;
                string runtimeParent = Path.GetDirectoryName(RuntimeRoot)!;
                Directory.CreateDirectory(runtimeParent);
                if (Directory.Exists(RuntimeRoot)) Directory.Delete(RuntimeRoot, recursive: true);

                CopyDirectory(sourceRoot, RuntimeRoot);

                if (!File.Exists(BundledExecutablePath))
                    throw new InvalidDataException("Ollama runtime 설치 확인에 실패했습니다.");

                return BundledExecutablePath;
            }
            finally
            {
                try { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true); } catch { }
            }
        }

        private static async Task<bool> StartServerAsync(string runtime, CancellationToken cancellationToken)
        {
            if (await IsServerRunningAsync(cancellationToken).ConfigureAwait(false)) return true;

            lock (Sync)
            {
                if (ownedServerProcess != null && !HasExitedSafe(ownedServerProcess))
                    return true;

                Directory.CreateDirectory(ModelsRoot);
                var psi = CreateOllamaProcessInfo(runtime, "serve");
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                ownedServerProcess = Process.Start(psi);
                if (ownedServerProcess == null) return false;
            }

            DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(18);
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await IsServerRunningAsync(cancellationToken).ConfigureAwait(false)) return true;
                await Task.Delay(350, cancellationToken).ConfigureAwait(false);
            }
            return false;
        }

        private static async Task<bool> PullModelAsync(string model, CancellationToken cancellationToken)
        {
            string body = JsonSerializer.Serialize(new { name = model, stream = false });
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint + "/api/pull") { Content = content };
            using HttpResponseMessage response = await LocalLongHttp.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }

        private static ProcessStartInfo CreateOllamaProcessInfo(string runtime, string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = runtime,
                Arguments = arguments,
                WorkingDirectory = Path.GetDirectoryName(runtime) ?? AppContext.BaseDirectory,
                UseShellExecute = false
            };
            psi.Environment["OLLAMA_HOST"] = Host + ":" + Port;
            psi.Environment["OLLAMA_MODELS"] = ModelsRoot;
            psi.Environment["OLLAMA_NO_CLOUD"] = "1";
            psi.Environment["OLLAMA_KEEP_ALIVE"] = "5m";
            return psi;
        }

        private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
        {
            using SHA256 sha = SHA256.Create();
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, useAsync: true);
            byte[] hash = await sha.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static bool ModelMatches(string candidate, string requested)
        {
            if (string.IsNullOrWhiteSpace(candidate)) return false;
            if (candidate.Equals(requested, StringComparison.OrdinalIgnoreCase)) return true;
            if (!requested.Contains(':') && candidate.Equals(requested + ":latest", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string FormatBytes(long bytes)
        {
            const double gb = 1024d * 1024d * 1024d;
            const double mb = 1024d * 1024d;
            if (bytes >= gb) return $"{bytes / gb:0.0}GB";
            return $"{bytes / mb:0}MB";
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(source, directory);
                Directory.CreateDirectory(Path.Combine(destination, relative));
            }
            foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(source, file);
                string target = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, overwrite: true);
            }
        }

        private static bool HasExitedSafe(Process process)
        {
            try { return process.HasExited; }
            catch { return true; }
        }
    }
}
