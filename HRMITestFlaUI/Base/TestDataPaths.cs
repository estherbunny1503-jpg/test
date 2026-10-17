using System;
using System.IO;
using System.Linq;

namespace HRMITestFlaUI.Base
{
    /// <summary>
    /// ตำแหน่งไฟล์ข้อมูลทดสอบ (ไฟล์ Import ต่างๆ) — ค่าเริ่มต้น C:\TestData
    /// เปลี่ยนได้ด้วย environment variable HRMI_TEST_DATA_DIR
    /// </summary>
    public static class TestDataPaths
    {
        public static string Folder
        {
            get
            {
                string? env = Environment.GetEnvironmentVariable("HRMI_TEST_DATA_DIR");
                return !string.IsNullOrWhiteSpace(env) ? env.Trim().Trim('"') : @"C:\TestData";
            }
        }

        private static readonly string[] ImportExtensions = { ".xlsx", ".xlsm", ".xls", ".csv" };

        /* หาไฟล์ในโฟลเดอร์ข้อมูลทดสอบตามชื่อ (ไม่สนช่องว่าง/ขีด/ตัวพิมพ์ และนามสกุล) เช่น
           "ImportEmployeeWorkSchedule" ตรงกับ "Import Employee Work Schedule.xlsm" */
        public static string? Find(params string[] names)
        {
            if (!Directory.Exists(Folder)) return null;
            var wanted = names.Select(Normalize).ToHashSet();
            return Directory.EnumerateFiles(Folder)
                .Where(f => ImportExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .FirstOrDefault(f => wanted.Contains(Normalize(Path.GetFileNameWithoutExtension(f))));
        }

        private static string Normalize(string value) =>
            new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }
}
