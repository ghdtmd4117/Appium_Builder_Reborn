using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppiumBuilder.Core
{
    public sealed class AdbDeviceInfo
    {
        public string Serial { get; init; } = string.Empty;
        public string State { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public override string ToString() => string.IsNullOrWhiteSpace(Description) ? Serial : $"{Serial} · {Description}";
    }

    public static class AdbEngine
    {
        private static readonly object DeviceGate = new();
        private static string? selectedSerial;

        public static string? SelectedSerial
        {
            get { lock (DeviceGate) return selectedSerial; }
        }

        public static void SetSelectedSerial(string? serial)
        {
            lock (DeviceGate) selectedSerial = string.IsNullOrWhiteSpace(serial) ? null : serial.Trim();
        }

        public static Process? StartProcess(string fileName, string arguments, bool hidden = true, string? workingDirectory = null)
        {
            var startInfo = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = hidden
            };
            if (!string.IsNullOrWhiteSpace(workingDirectory)) startInfo.WorkingDirectory = workingDirectory;
            return Process.Start(startInfo);
        }

        public static Process? StartAdbProcess(string arguments, bool hidden = true)
        {
            return StartProcess("adb", BuildAdbArguments(arguments), hidden);
        }

        public static string RunCommand(string arguments, int timeoutMs = 10000)
        {
            return RunAdb(arguments, timeoutMs, useSelectedDevice: !IsGlobalCommand(arguments));
        }

        public static string RunGlobalCommand(string arguments, int timeoutMs = 10000)
        {
            return RunAdb(arguments, timeoutMs, useSelectedDevice: false);
        }

        public static Task<string> RunCommandAsync(string arguments, int timeoutMs = 10000)
            => Task.Run(() => RunCommand(arguments, timeoutMs));

        public static Task<string> RunGlobalCommandAsync(string arguments, int timeoutMs = 10000)
            => Task.Run(() => RunGlobalCommand(arguments, timeoutMs));

        public static IReadOnlyList<AdbDeviceInfo> GetDevices()
        {
            string result = RunGlobalCommand("devices -l", 5000);
            return ParseDevicesOutput(result);
        }

        public static IReadOnlyList<AdbDeviceInfo> ParseDevicesOutput(string result)
        {
            var devices = new List<AdbDeviceInfo>();
            foreach (string raw in (result ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = raw.Trim();
                if (line.Length == 0 ||
                    line.StartsWith("List of devices attached", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("* daemon", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // adb devices -l 은 ADB/OS 버전에 따라 serial/state 사이를 탭 또는 여러 공백으로 출력한다.
                string[] tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 2) continue;

                string serial = tokens[0].Trim();
                string state = tokens[1].Trim();
                if (serial.Length == 0 || state.Length == 0) continue;

                string description = string.Join(" ", tokens.Skip(2).Where(token => token.Contains(':')));
                devices.Add(new AdbDeviceInfo
                {
                    Serial = serial,
                    State = state,
                    Description = description
                });
            }
            return devices;
        }

        public static bool IsDeviceConnected()
        {
            IReadOnlyList<AdbDeviceInfo> devices = GetDevices().Where(d => d.State == "device").ToList();
            string? selected = SelectedSerial;
            if (!string.IsNullOrWhiteSpace(selected))
            {
                if (devices.Any(d => string.Equals(d.Serial, selected, StringComparison.OrdinalIgnoreCase))) return true;
                SetSelectedSerial(null);
            }
            if (devices.Count == 1)
            {
                SetSelectedSerial(devices[0].Serial);
                return true;
            }
            return false;
        }

        public static bool HasMultipleUsableDevices() => GetDevices().Count(d => d.State == "device") > 1;

        public static bool IsEndpointConnected(string endpoint)
        {
            return GetDevices().Any(d => string.Equals(d.Serial, endpoint, StringComparison.OrdinalIgnoreCase) && d.State == "device");
        }

        public static string BuildAdbArguments(string arguments)
        {
            if (IsGlobalCommand(arguments)) return arguments;
            string? serial = SelectedSerial;
            return string.IsNullOrWhiteSpace(serial) ? arguments : $"-s \"{serial}\" {arguments}";
        }

        private static bool IsGlobalCommand(string arguments)
        {
            string trimmed = arguments.TrimStart();
            string first = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
            return first.Equals("devices", StringComparison.OrdinalIgnoreCase) ||
                   first.Equals("connect", StringComparison.OrdinalIgnoreCase) ||
                   first.Equals("disconnect", StringComparison.OrdinalIgnoreCase) ||
                   first.Equals("start-server", StringComparison.OrdinalIgnoreCase) ||
                   first.Equals("kill-server", StringComparison.OrdinalIgnoreCase) ||
                   first.Equals("version", StringComparison.OrdinalIgnoreCase);
        }

        private static string RunAdb(string arguments, int timeoutMs, bool useSelectedDevice)
        {
            try
            {
                string finalArguments = useSelectedDevice ? BuildAdbArguments(arguments) : arguments;
                var startInfo = new ProcessStartInfo("adb", finalArguments)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("ADB 프로세스를 시작하지 못했습니다.");
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();

                if (!process.WaitForExit(timeoutMs))
                {
                    TryKill(process);
                    return $"ADB Timeout: {timeoutMs}ms 안에 명령이 끝나지 않았습니다.";
                }

                return outputTask.GetAwaiter().GetResult() + errorTask.GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                return "ADB Error: " + ex.Message;
            }
        }

        public static void TryKill(Process? process)
        {
            if (process == null) return;
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(3000);
                }
            }
            catch { }
            finally
            {
                try { process.Dispose(); } catch { }
            }
        }
    }
}
