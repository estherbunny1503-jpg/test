using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.IO;

namespace HRMITestFlaUI.Base
{
    public static class TestLogger
    {
        private const string SheetName = "Test Log";
        private const string SummarySheetName = "SQL Compare & Summary"; // ปรับชื่อแท็บให้สื่อความหมายชัดเจนขึ้น

        private static readonly string[] Headers =
            { "No", "Timestamp", "Module", "หน้าจอ", "Status", "Test Name", "Message", "Elapsed (s)" };
        private static readonly double[] ColumnWidths = { 6, 22, 14, 26, 10, 40, 70, 14 };

        private static readonly Dictionary<string, (string Icon, string Fill, string FontColor)> StatusStyles = new()
        {
            ["PASS"] = ("✅", "#E2EFDA", "#375623"),
            ["FAIL"] = ("❌", "#FCE4D6", "#C00000"),
            ["INFO"] = ("ℹ️", "#DDEBF7", "#2E75B6"),
        };

        private static readonly string LogFolder;
        private static readonly string LogFilePath;

        private static readonly object _lock = new object();
        private static readonly DateTime _startTime = DateTime.Now;
        private static int _rowIndex = 2;

        private static string _module = "";
        private static string _screen = "";

        private static int _passCount = 0;
        private static int _failCount = 0;

        static TestLogger()
        {
            string? envLogDir = Environment.GetEnvironmentVariable("HRMI_LOG_DIR");
            string? scope = Environment.GetEnvironmentVariable("HRMI_TEST_SCOPE");

            if (!string.IsNullOrWhiteSpace(envLogDir))
            {
                LogFolder = envLogDir.Trim().Trim('"');
                // ปรับให้เหลือเฉพาะชื่อหน้าจอ เช่น BankInfoUC.xlsx
                string fileName = !string.IsNullOrWhiteSpace(scope)
                    ? $"{scope.Trim()}.xlsx"
                    : $"TestRun_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                LogFilePath = Path.Combine(LogFolder, fileName);
            }
            else
            {
                // ใช้ไดรฟ์ D: ถ้ามี ถ้าไม่มีให้เก็บไว้ใน Documents แทน
                LogFolder = Directory.Exists(@"D:\")
                    ? @"D:\TestResults\Logs"
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TestResults", "Logs");
                LogFilePath = Path.Combine(LogFolder, $"TestRun_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }

            Directory.CreateDirectory(LogFolder);
            InitExcel();
        }

        public static void SetContext(string module)
        {
            _module = module;
            _screen = "";
        }

        public static void SetScreen(string screen) => _screen = screen;

        public static void LogSuccess(string testName, string message) => Write("PASS", testName, message);
        public static void LogFailure(string testName, string message) => Write("FAIL", testName, message);
        public static void LogInfo(string testName, string message) => Write("INFO", testName, message);

        public static string GetCurrentLogFilePath() => LogFilePath;

        private static void InitExcel()
        {
            lock (_lock)
            {
                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add(SheetName);

                for (int i = 0; i < Headers.Length; i++)
                {
                    var cell = ws.Cell(1, i + 1);
                    cell.Value = Headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2E75B6");
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }

                for (int i = 0; i < ColumnWidths.Length; i++)
                    ws.Column(i + 1).Width = ColumnWidths[i];

                ws.SheetView.FreezeRows(1);
                wb.SaveAs(LogFilePath);
            }
        }

        private static void Write(string status, string testName, string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            double elapsed = Math.Round((DateTime.Now - _startTime).TotalSeconds, 2);
            var style = StatusStyles[status];

            lock (_lock)
            {
                if (status == "PASS") _passCount++;
                else if (status == "FAIL") _failCount++;

                try
                {
                    string? summaryOutPath = Environment.GetEnvironmentVariable("HRMI_SUMMARY_OUT");
                    if (!string.IsNullOrWhiteSpace(summaryOutPath))
                    {
                        File.WriteAllText(summaryOutPath, $"{_passCount}|{_failCount}");
                    }
                }
                catch { }

                Console.WriteLine($"{timestamp} | [{style.Icon} {status}] [{testName}] {message}");

                using var wb = new XLWorkbook(LogFilePath);
                var ws = wb.Worksheet(SheetName);

                WriteRow(ws, _rowIndex, timestamp, status, testName, message, elapsed);
                StyleRow(ws, _rowIndex, style);

                wb.SaveAs(LogFilePath);
                _rowIndex++;
            }
        }

        private static void WriteRow(IXLWorksheet ws, int row, string timestamp, string status, string testName, string message, double elapsed)
        {
            object[] values = { row - 1, timestamp, _module, _screen, status, testName, message, elapsed };
            for (int i = 0; i < values.Length; i++)
                ws.Cell(row, i + 1).Value = XLCellValue.FromObject(values[i]);
        }

        private static void StyleRow(IXLWorksheet ws, int row, (string Icon, string Fill, string FontColor) style)
        {
            var statusCell = ws.Cell(row, 5);
            statusCell.Style.Font.Bold = true;
            statusCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            statusCell.Style.Font.FontColor = XLColor.FromHtml(style.FontColor);

            var rowRange = ws.Range(row, 1, row, Headers.Length);
            rowRange.Style.Fill.BackgroundColor = XLColor.FromHtml(style.Fill);
            rowRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            rowRange.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        }

        /// <summary>
        /// รวมผลการรัน Test, SQL Query ที่ใช้ และ Data Comparison ไว้ในแท็บที่สองของไฟล์เดียว
        /// </summary>
        public static void AppendSummaryAndSqlComparison(string summaryInfo, string sqlCompareCsvPath, string? executedSqlQuery = null)
        {
            lock (_lock)
            {
                if (!File.Exists(LogFilePath)) return;

                using var wb = new XLWorkbook(LogFilePath);

                // ป้องกันกรณีแท็บชื่อซ้ำ
                if (wb.Worksheets.Contains(SummarySheetName))
                    wb.Worksheets.Delete(SummarySheetName);

                var ws = wb.Worksheets.Add(SummarySheetName);
                int currentRow = 1;

                // [SECTION 1] TEST SUMMARY
                ws.Cell(currentRow, 1).Value = "[SECTION 1] TEST EXECUTION SUMMARY";
                ws.Cell(currentRow, 1).Style.Font.Bold = true;
                ws.Cell(currentRow, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#DDEBF7");
                currentRow++;

                var summaryLines = summaryInfo.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                foreach (var line in summaryLines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Split(new[] { ',' }, 2);
                    ws.Cell(currentRow, 1).Value = parts[0].Trim();
                    if (parts.Length > 1) ws.Cell(currentRow, 2).Value = parts[1].Trim();
                    currentRow++;
                }

                currentRow += 2;

                // [SECTION 2] EXECUTED SQL QUERY (สำหรับ Dev ไปรันตรวจสอบต่อ)
                ws.Cell(currentRow, 1).Value = "[SECTION 2] EXECUTED SQL QUERY";
                ws.Cell(currentRow, 1).Style.Font.Bold = true;
                ws.Cell(currentRow, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#DDEBF7");
                currentRow++;

                if (!string.IsNullOrWhiteSpace(executedSqlQuery))
                {
                    var sqlCell = ws.Cell(currentRow, 1);
                    sqlCell.Value = executedSqlQuery.Trim();
                    sqlCell.Style.Font.FontName = "Consolas";
                    sqlCell.Style.Alignment.WrapText = true;
                    currentRow++;
                }
                else
                {
                    ws.Cell(currentRow, 1).Value = "-- No explicit SQL query provided --";
                    currentRow++;
                }

                currentRow += 2;

                // [SECTION 3] SQL DATA COMPARISON RESULT
                ws.Cell(currentRow, 1).Value = "[SECTION 3] SQL DATA COMPARISON RESULT";
                ws.Cell(currentRow, 1).Style.Font.Bold = true;
                ws.Cell(currentRow, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#DDEBF7");
                currentRow++;

                if (File.Exists(sqlCompareCsvPath))
                {
                    var csvLines = File.ReadAllLines(sqlCompareCsvPath);
                    bool isHeader = true;
                    foreach (var line in csvLines)
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("---")) continue;
                        var cols = line.Split(',');
                        for (int colIdx = 0; colIdx < cols.Length; colIdx++)
                        {
                            var cell = ws.Cell(currentRow, colIdx + 1);
                            cell.Value = cols[colIdx].Trim();
                            if (isHeader)
                            {
                                cell.Style.Font.Bold = true;
                                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");
                            }
                        }
                        isHeader = false;
                        currentRow++;
                    }
                }
                else
                {
                    ws.Cell(currentRow, 1).Value = "No SQL Comparison CSV found.";
                }

                ws.Columns().AdjustToContents();
                wb.SaveAs(LogFilePath);
            }
        }
    }
}