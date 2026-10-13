using System;
using System.IO;

namespace HRMITestFlaUI.Base
{
    public static class TestProgressHelper
    {
        private static string? _progressFilePath;

        // ไม่ยึดตามโมดูล: ทุกหน้าจอมีสเกลการทำงาน UI ตั้งแต่ 10% ถึง 85% เสมอ
        public static void InitSingleScreen(string screenName = "")
        {
            ReportSystem(100, $"Ready for testing");
        }

        // ปิดการคิดสัดส่วนแบบโมดูล ให้ทุกหน้าจอคิดเป็น 100% ของตัวเอง
        public static void SetScreenContext(int currentScreenIndex, int totalScreens, string screenName = "")
        {
            // ไม่ปรับฐานสเกล ให้ใช้สเกลหน้าจอปัจจุบันตรงๆ
        }

        public static void SetGlobalContext(int currentModuleIndex, int totalModules, int currentScreenIndex, int totalScreensInModule, string screenName)
        {
            // ไม่ปรับฐานสเกล
        }

        public static void ReportSystem(int percent, string message)
        {
            int val = Math.Clamp((int)Math.Round(percent * 0.10), 0, 10);
            WriteProgress(val, $"[System] {message}");
        }

        public static void Report(int screenStepPercent, string stepMessage)
        {
            // แปลงสเต็ป 0-100% ของหน้าจอนั้นๆ เข้าสู่ช่วง 10% - 85% โดยตรง
            double ratio = Math.Clamp(screenStepPercent / 100.0, 0.0, 1.0);
            int finalPercent = (int)Math.Round(10.0 + (ratio * 75.0));
            WriteProgress(finalPercent, stepMessage);
        }

        public static void ReportFinal(int percent, string message)
        {
            int val = Math.Clamp(percent, 85, 100);
            WriteProgress(val, message);
        }

        private static string GetProgressFilePath()
        {
            if (!string.IsNullOrEmpty(_progressFilePath))
                return _progressFilePath;

            string? envPath = Environment.GetEnvironmentVariable("HRMI_PROGRESS_FILE");
            if (!string.IsNullOrEmpty(envPath))
            {
                _progressFilePath = envPath;
                return _progressFilePath;
            }

            _progressFilePath = Path.Combine(Path.GetTempPath(), "hrmi_progress.txt");
            return _progressFilePath;
        }

        private static void WriteProgress(int percent, string message)
        {
            try
            {
                string path = GetProgressFilePath();
                using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                using var writer = new StreamWriter(fs);
                writer.Write($"{percent}:{message}");
                writer.Flush();
            }
            catch { }
        }
    }
}