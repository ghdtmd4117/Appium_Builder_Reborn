using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace AppiumBuilder.Core
{
    /// <summary>
    /// Appium Builder Reborn이 시작한 Appium Server의 수명주기를 관리한다.
    /// 외부에서 이미 실행 중인 서버는 감지하되 임의로 종료하지 않는다.
    /// </summary>
    public static class AppiumServerManager
    {
        public const string Host = "127.0.0.1";
        public const int Port = 4723;
        public const string RootStatusUrl = "http://127.0.0.1:4723/status";
        public const string HubStatusUrl = "http://127.0.0.1:4723/wd/hub/status";
        public const string DisplayEndpoint = "127.0.0.1:4723";

        private static readonly object Sync = new();
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMilliseconds(900) };
        private static Process? ownedTerminalProcess;

        public static bool OwnsRunningServer
        {
            get
            {
                lock (Sync)
                    return ownedTerminalProcess != null && !HasExitedSafe(ownedTerminalProcess);
            }
        }

        public static string LaunchCommand => "appium --address 127.0.0.1 --port 4723 --base-path /wd/hub";

        public static async Task<bool> IsServerRunningAsync(CancellationToken cancellationToken = default)
        {
            foreach (string url in new[] { RootStatusUrl, HubStatusUrl })
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    using HttpResponseMessage response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode) return true;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    // 다른 base-path를 이어서 확인한다.
                }
            }
            return false;
        }

        public static bool IsServerRunning()
        {
            try { return IsServerRunningAsync().GetAwaiter().GetResult(); }
            catch { return false; }
        }

        public static bool TryFindAppiumCli(out string path)
        {
            path = string.Empty;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "where.exe",
                    Arguments = "appium",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using Process? process = Process.Start(psi);
                if (process == null) return false;
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(2500);
                path = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .FirstOrDefault(File.Exists) ?? string.Empty;
                return !string.IsNullOrWhiteSpace(path);
            }
            catch
            {
                return false;
            }
        }

        public static async Task<(bool Success, string Message)> StartVisibleAsync(CancellationToken cancellationToken = default)
        {
            if (await IsServerRunningAsync(cancellationToken).ConfigureAwait(false))
                return (true, OwnsRunningServer ? "Appium 서버가 이미 실행 중입니다." : "외부 Appium 서버가 이미 실행 중입니다.");

            if (!TryFindAppiumCli(out string cliPath))
                return (false, "Appium CLI를 찾지 못했습니다. CMD에서 'appium --version'이 실행되는지 확인해주세요.");

            lock (Sync)
            {
                if (ownedTerminalProcess != null && !HasExitedSafe(ownedTerminalProcess))
                    return (false, "Appium 터미널이 이미 시작 중입니다. 잠시 후 다시 확인해주세요.");

                string title = "Appium Server - Appium Builder Reborn";
                string arguments = $"/D /K \"title {title} && {LaunchCommand}\"";
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = arguments,
                    WorkingDirectory = Path.GetDirectoryName(cliPath) ?? Environment.CurrentDirectory,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal
                };

                ownedTerminalProcess = Process.Start(psi);
                if (ownedTerminalProcess == null)
                    return (false, "Appium 서버 터미널을 시작하지 못했습니다.");
            }

            bool healthy = await WaitUntilHealthyAsync(TimeSpan.FromSeconds(12), cancellationToken).ConfigureAwait(false);
            if (healthy) return (true, "Appium 서버가 실행되었습니다.");

            return (false, "Appium 터미널은 열렸지만 127.0.0.1:4723에서 서버 응답을 확인하지 못했습니다. 열린 터미널의 오류 메시지를 확인해주세요.");
        }

        public static async Task<bool> WaitUntilHealthyAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            DateTime deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await IsServerRunningAsync(cancellationToken).ConfigureAwait(false)) return true;
                await Task.Delay(350, cancellationToken).ConfigureAwait(false);
            }
            return false;
        }

        public static bool StopOwnedServer(out string message)
        {
            Process? process;
            lock (Sync) process = ownedTerminalProcess;

            if (process == null || HasExitedSafe(process))
            {
                lock (Sync) ownedTerminalProcess = null;
                message = IsServerRunning()
                    ? "현재 Appium 서버는 외부에서 실행된 서버라 Appium Builder가 종료하지 않습니다."
                    : "Appium Builder가 시작한 서버가 없습니다.";
                return false;
            }

            try
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(3000);
                lock (Sync) ownedTerminalProcess = null;
                message = "Appium Builder가 시작한 Appium 서버를 종료했습니다.";
                return true;
            }
            catch (Exception ex)
            {
                message = "Appium 서버 종료 실패: " + ex.Message;
                return false;
            }
        }

        public static bool ShowTerminal(out string message)
        {
            Process? process;
            lock (Sync) process = ownedTerminalProcess;

            if (process != null && !HasExitedSafe(process))
            {
                try
                {
                    process.Refresh();
                    IntPtr handle = process.MainWindowHandle;
                    if (handle != IntPtr.Zero)
                    {
                        ShowWindow(handle, 9); // SW_RESTORE
                        SetForegroundWindow(handle);
                        message = "Appium 서버 터미널을 표시했습니다.";
                        return true;
                    }
                }
                catch { }
            }

            try
            {
                string state = IsServerRunning()
                    ? "echo Appium Server is already running on 127.0.0.1:4723."
                    : "echo Appium Server is not running. Use the 'Server Start' button in Appium Builder.";
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/D /K \"title Appium Terminal - Appium Builder Reborn && {state} && echo. && appium --version\"",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal
                });
                message = "Appium 터미널을 열었습니다.";
                return true;
            }
            catch (Exception ex)
            {
                message = "터미널 열기 실패: " + ex.Message;
                return false;
            }
        }

        private static bool HasExitedSafe(Process process)
        {
            try { return process.HasExited; }
            catch { return true; }
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }
}
