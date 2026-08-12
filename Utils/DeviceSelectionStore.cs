using System;
using System.IO;
using System.Text;
using AppiumBuilder.Core;

namespace AppiumBuilder.Utils
{
    public static class DeviceSelectionStore
    {
        private static string PathForSelection => Path.Combine(Globals.LogFolder, "selected_device.txt");

        public static void Restore()
        {
            try
            {
                if (!File.Exists(PathForSelection)) return;
                string serial = File.ReadAllText(PathForSelection, Encoding.UTF8).Trim();
                if (!string.IsNullOrWhiteSpace(serial)) AdbEngine.SetSelectedSerial(serial);
            }
            catch { }
        }

        public static void Save(string? serial)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(serial))
                {
                    if (File.Exists(PathForSelection)) File.Delete(PathForSelection);
                    return;
                }
                File.WriteAllText(PathForSelection, serial.Trim(), new UTF8Encoding(false));
            }
            catch { }
        }
    }
}
