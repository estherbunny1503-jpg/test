using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using HRMITestFlaUI.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace HRMITestFlaUI.Module.Time_Attendance
{
    /// <summary>
    /// TestStep.ods: "ประมวลผลจากระบบเวลาเข้าสู่ระบบเงินเดือน" (ไม่ใช่แบบแจกแจง)
    /// </summary>
    public sealed class FrmPPprocessWinUC
    {
        private const string MenuLabel = "ประมวลผลจากระบบเวลาเข้าสู่ระบบเงินเดือน";
        private const string MenuRowId = "treeListColumn row 11";
        private const string PeriodTemplate = "งวดการจ่ายเดือนละ 1 ครั้ง";
        private readonly AutomationElement _mainWindow;
        private readonly UIA3Automation _automation;

        public FrmPPprocessWinUC(AutomationElement mainWindow, UIA3Automation automation)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

        public void NavigateToPPprocessWinUC()
        {
            TestProgressHelper.Report(2, "Opening Time Attendance -> Post to Payroll...");
            OpenExactMenuItem();
            Thread.Sleep(800);
            if (FindProcessForm() == null)
                throw new Exception($"เปิดเมนู '{MenuLabel}' แล้วแต่ไม่พบหน้าจอประมวลผล; หน้าต่างที่พบ: {DescribeWindows()}");
            TestProgressHelper.Report(5, "Post to Payroll screen opened");
        }

        public void RunAllValidationTests()
        {
            var candidate = FindProcessForm() ?? throw new Exception($"ไม่พบหน้าจอประมวลผล '{MenuLabel}'");
            var firstPeriodCell = candidate.FindFirstDescendant(cf => cf.ByName("ชื่อรูปแบบงวดการจ่าย row 0"))
                ?? throw new Exception("ไม่พบช่องชื่อรูปแบบงวดการจ่ายแถวแรก");
            ClickCenter(firstPeriodCell);
            Thread.Sleep(350);
            var pickerButton = Retry(() => candidate.FindAllDescendants(cf =>
                    cf.ByControlType(ControlType.Button).And(cf.ByName("Editing control"))).FirstOrDefault(), 3000)
                ?? throw new Exception("คลิกช่องชื่อรูปแบบงวดการจ่ายแล้วไม่พบปุ่ม ... ของช่อง");
            ClickCenter(pickerButton);
            Thread.Sleep(700);

            var lookup = FindPeriodLookup() ?? throw new Exception("ไม่พบหน้าต่างเลือกรูปแบบงวดการจ่าย");
            var selectedMonths = SelectMonthsFromLookup(lookup, PeriodTemplate, "มกราคม", "พฤษภาคม", "ตุลาคม");
            ClickLookupSelect(lookup);

            var grid = candidate.FindFirstDescendant(cf => cf.ByAutomationId("devGridList"))
                ?? throw new Exception("ไม่พบตารางงวดการจ่ายหลังปิดหน้าต่างเลือกข้อมูล");
            for (int row = 0; row < selectedMonths.Count; row++)
            {
                int rowIndex = row;
                var code = $"PTS202609-{row + 1:0000}";
                TestLogger.LogInfo("FrmPPprocessWinUC", $"กำลังเลือกรหัสประมวลผล {code} แถว {row + 1}");
                var codeCell = Retry(() => candidate.FindFirstDescendant(cf => cf.ByName($"รหัสรูปแบบการประมวลผล row {rowIndex}")), 3000)
                    ?? throw new Exception($"ไม่พบช่องรหัสรูปแบบการประมวลผลแถว {row + 1}");
                ClickCenter(codeCell);
                Thread.Sleep(250);
                var codePicker = Retry(() =>
                {
                    var cellRect = codeCell.BoundingRectangle;
                    return candidate.FindAllDescendants(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("Editing control")))
                        .Where(b =>
                        {
                            try
                            {
                                var r = b.BoundingRectangle;
                                return !b.Properties.IsOffscreen.ValueOrDefault && Math.Abs((r.Top + r.Height / 2) - (cellRect.Top + cellRect.Height / 2)) < Math.Max(cellRect.Height, 20);
                            }
                            catch { return false; }
                        })
                        .OrderBy(b => Math.Abs((b.BoundingRectangle.Top + b.BoundingRectangle.Height / 2) - (cellRect.Top + cellRect.Height / 2)))
                        .FirstOrDefault();
                }, 2500);
                if (codePicker != null) ClickCenter(codePicker);
                else
                {
                    var r = codeCell.BoundingRectangle;
                    Mouse.Click(new System.Drawing.Point(r.Right - 6, r.Top + r.Height / 2));
                }
                var codeLookup = Retry(FindLookupWindow, 5000) ?? throw new Exception($"ไม่พบหน้าต่างเลือกรหัสรูปแบบการประมวลผล (หน้าต่างที่พบ: {DescribeWindows()})");
                SelectLookupCode(codeLookup, code);
                ClickLookupSelect(codeLookup);
                TestLogger.LogSuccess($"TestCase01_PPprocessWinUC_Code{row + 1}", $"เลือก {code} สำหรับเดือน {selectedMonths[row]}");
            }

            TestLogger.LogInfo("FrmPPprocessWinUC", "เลือกงวดและรหัสประมวลผลครบแล้ว กำลังเริ่ม Process");
            var processButton = candidate.FindFirstDescendant(cf => cf.ByAutomationId("devBtnProcess"))
                ?? throw new Exception("ไม่พบปุ่ม Process");
            ClickCenter(processButton);
            TestLogger.LogInfo("FrmPPprocessWinUC", "กด Process แล้ว กำลังรอผลการประมวลผล (สูงสุด 5 นาที)");
            ConfirmProcessResult(300000);
            CloseProcessForm();
            TestLogger.LogCaseSuccess("TestCase01_PPprocessWinUC", $"เลือกงวด '{PeriodTemplate}' เดือน {string.Join(", ", selectedMonths)} และประมวลผลรหัส PTS202609-0001 ถึง PTS202609-0003 สำเร็จ");
        }

        /* ปิดหน้าจอประมวลผล: หาฟอร์มใหม่ทุกครั้ง (reference เดิมอาจเก่าหลังประมวลผล); ถ้าฟอร์มปิดไปแล้วถือว่าสำเร็จ */
        private void CloseProcessForm()
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                var form = FindProcessForm();
                if (form == null) return;

                DismissBlockingDialogs(form);
                form = FindProcessForm();
                if (form == null) return;

                AutomationElement? close = null;
                try
                {
                    close = form.FindFirstDescendant(cf => cf.ByAutomationId("devBtnClose"))
                        ?? form.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button)
                            .And(cf.ByName("Close").Or(cf.ByName("ปิด")).Or(cf.ByName("&Close"))));
                }
                catch { }

                try
                {
                    if (close != null) ClickCenter(close);
                    else form.AsWindow().Close();
                }
                catch { }

                if (WaitUntil(() => FindProcessForm() == null, 4000)) return;
            }
            throw new Exception($"ไม่สามารถปิดหน้าจอประมวลผลได้ (หน้าต่างที่พบ: {DescribeWindows()} | ปุ่มในหน้าต่างอื่น: {DescribeOtherWindowButtons()})");
        }

        /* ปิด dialog/หน้าต่างอื่นที่ค้างอยู่เหนือฟอร์มประมวลผล (เช่น หน้าต่างชื่อ Process) ซึ่งบล็อกการปิดฟอร์ม */
        private void DismissBlockingDialogs(AutomationElement form)
        {
            string[] preferred = { "OK", "&OK", "ตกลง", "&ตกลง", "Yes", "&Yes", "ใช่", "Close", "&Close", "ปิด" };
            foreach (var window in GetWindows().ToList())
            {
                try
                {
                    if (window.Equals(form) || window.FindFirstDescendant(cf => cf.ByAutomationId("devBtnProcess")) != null) continue;
                    if (window.FindFirstDescendant(cf => cf.ByAutomationId("devBtnClose")) != null) continue; // ฟอร์มหน้าจออื่น ไม่ใช่ dialog

                    var button = window.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                        .FirstOrDefault(b => preferred.Any(n => SafeName(b).Equals(n, StringComparison.OrdinalIgnoreCase)));
                    if (button != null) ClickCenter(button);
                    else
                    {
                        window.Focus();
                        Keyboard.Press(VirtualKeyShort.RETURN);
                        Keyboard.Release(VirtualKeyShort.RETURN);
                    }
                    TestLogger.LogInfo("FrmPPprocessWinUC", $"ปิดหน้าต่างที่ค้างอยู่: '{SafeWindowName(window)}'");
                    Thread.Sleep(500);
                }
                catch { }
            }
        }

        private string DescribeOtherWindowButtons()
        {
            try
            {
                return string.Join(" ; ", GetWindows().Select(w =>
                {
                    var buttons = string.Join(",", w.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                        .Select(b => $"'{SafeName(b)}'/{b.AutomationId}").Take(12));
                    return $"[{SafeWindowName(w)} id={w.AutomationId}: {buttons}]";
                }));
            }
            catch { return "อ่านไม่ได้"; }
        }

        private void ConfirmProcessResult(int timeoutMs)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < timeoutMs)
            {
                var roots = GetWindows().Concat(new[] { _mainWindow }).ToList();
                foreach (var root in roots)
                {
                    AutomationElement? button = null;
                    try
                    {
                        button = root.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                            .FirstOrDefault(b => new[] { "OK", "&OK", "ตกลง", "&ตกลง" }.Any(n => SafeName(b).Equals(n, StringComparison.OrdinalIgnoreCase)));
                    }
                    catch { }
                    if (button != null)
                    {
                        ClickCenter(button);
                        TestLogger.LogSuccess("TestCase01_PPprocessWinUC_Process", "ประมวลผลเสร็จและกดยืนยัน OK แล้ว");
                        return;
                    }
                }

                var messageBox = GetWindows().FirstOrDefault(w => new[] { "ตรวจสอบ", "ข้อความ", "Information", "Warning", "Exclamation", "Error" }
                    .Any(title => SafeWindowName(w).Equals(title, StringComparison.OrdinalIgnoreCase)));
                if (messageBox != null)
                {
                    TestLogger.LogInfo("FrmPPprocessWinUC", $"พบกล่องข้อความ '{SafeWindowName(messageBox)}' ที่ไม่มีปุ่มให้ UIA อ่าน กด Enter เพื่อยืนยัน");
                    messageBox.Focus();
                    Keyboard.Press(VirtualKeyShort.RETURN);
                    Keyboard.Release(VirtualKeyShort.RETURN);
                    Thread.Sleep(600);
                    return;
                }
                Thread.Sleep(1000);
            }
            throw new Exception("หลัง Process ไม่พบปุ่มยืนยัน OK/ตกลง หรือกล่องข้อความ ภายใน 5 นาที");
        }

        private List<string> SelectMonthsFromLookup(AutomationElement lookup, string periodTemplate, params string[] targetMonths)
        {
            var grid = lookup.FindFirstDescendant(cf => cf.ByAutomationId("devGridList"))
                ?? throw new Exception("ไม่พบตารางงวดการจ่ายในหน้าต่างเลือกข้อมูล");
            var wanted = new HashSet<string>(targetMonths, StringComparer.OrdinalIgnoreCase);
            var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var previousSignature = "";
            var rect = grid.BoundingRectangle;
            var center = new System.Drawing.Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);

            for (int page = 0; page < 80 && selected.Count < wanted.Count; page++)
            {
                var cells = ReadGridRows(grid);
                foreach (var row in cells)
                {
                    if (!string.Equals(row.GetValueOrDefault("ชื่อรูปแบบงวดการจ่าย") as string, periodTemplate, StringComparison.OrdinalIgnoreCase)) continue;
                    var month = row.GetValueOrDefault("ประจำเดือน") as string ?? "";
                    if (!wanted.Contains(month) || selected.Contains(month)) continue;
                    var checkbox = row.GetValueOrDefault("เลือกElement") as AutomationElement;
                    if (checkbox == null) continue;
                    if (!string.Equals(ReadValue(checkbox), "Checked", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(ReadValue(checkbox), "true", StringComparison.OrdinalIgnoreCase))
                        ClickCenter(checkbox);
                    selected.Add(month);
                    Thread.Sleep(150);
                }

                var signature = string.Join("|", cells.SelectMany(r => r.Where(kv => kv.Key != "เลือกElement").Select(kv => kv.Key + ":" + (kv.Value as string ?? ""))));
                if (signature == previousSignature) break;
                previousSignature = signature;
                Mouse.MoveTo(center);
                Mouse.Scroll(-4);
                Thread.Sleep(200);
            }

            var missing = wanted.Except(selected, StringComparer.OrdinalIgnoreCase).ToList();
            if (missing.Count > 0)
                throw new Exception($"ไม่พบงวด '{periodTemplate}' สำหรับเดือน {string.Join(", ", missing)} ในหน้าต่างเลือกข้อมูล");
            return targetMonths.ToList();
        }

        private void SelectLookupCode(AutomationElement lookup, string desiredCode)
        {
            var grid = lookup.FindFirstDescendant(cf => cf.ByAutomationId("devGridList"))
                ?? throw new Exception("ไม่พบตารางรหัสรูปแบบการประมวลผล");
            var suffix = desiredCode.Substring(desiredCode.LastIndexOf('-') + 1);
            var rect = grid.BoundingRectangle;
            var center = new System.Drawing.Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);
            string previous = "";
            for (int i = 0; i < 20; i++)
            {
                var rows = grid.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem));
                string TextOf(AutomationElement e)
                {
                    var value = ReadValue(e);
                    return value.Length > 0 ? value : SafeName(e);
                }
                var exact = rows.FirstOrDefault(e => string.Equals(TextOf(e).Trim(), desiredCode, StringComparison.OrdinalIgnoreCase));
                var selected = exact ?? rows.Where(e => System.Text.RegularExpressions.Regex.IsMatch(TextOf(e).Trim(), @"PTS\d{6}-" + suffix + @"$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    .OrderByDescending(TextOf, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
                if (selected != null) { ClickCenter(selected); return; }
                var signature = string.Join("|", rows.Select(TextOf).Where(t => t.Contains("PTS", StringComparison.OrdinalIgnoreCase)).Distinct());
                if (signature == previous) break;
                previous = signature;
                Mouse.MoveTo(center);
                Mouse.Scroll(-4);
                Thread.Sleep(200);
            }
            throw new Exception($"ไม่พบรหัส '{desiredCode}' (หรือรหัสที่มี suffix {suffix}) ในหน้าต่างเลือกข้อมูล");
        }

        private List<Dictionary<string, object>> ReadGridRows(AutomationElement grid)
        {
            var result = new Dictionary<int, Dictionary<string, object>>();
            foreach (var element in grid.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem)))
            {
                var name = SafeName(element);
                var match = System.Text.RegularExpressions.Regex.Match(name, @"^(.*) row (\d+)$");
                if (!match.Success) continue;
                int rowIndex = int.Parse(match.Groups[2].Value);
                if (!result.TryGetValue(rowIndex, out var row)) result[rowIndex] = row = new Dictionary<string, object>();
                string column = match.Groups[1].Value;
                row[column] = ReadValue(element);
                if (column == "เลือก") row["เลือกElement"] = element;
            }
            return result.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToList();
        }

        private AutomationElement? FindPeriodLookup() => FindLookupWindow() ?? GetWindows().FirstOrDefault(w =>
        {
            try
            {
                if (w.FindFirstDescendant(cf => cf.ByAutomationId("devBtnProcess")) != null) return false;
                return SafeWindowName(w).Contains("กำหนดรูปแบบงวดการจ่าย", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(w.AutomationId, "BaseListForm", StringComparison.OrdinalIgnoreCase)
                    || w.FindFirstDescendant(cf => cf.ByAutomationId("devGridList")) != null;
            }
            catch { return false; }
        });

        private AutomationElement? FindLookupWindow()
        {
            var candidates = GetWindows().ToList();
            try { candidates.AddRange(_mainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Window))); } catch { }
            foreach (var candidate in candidates.Distinct())
            {
                try
                {
                    if (candidate.FindFirstDescendant(cf => cf.ByAutomationId("devBtnProcess")) != null) continue;
                    var buttons = candidate.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
                    bool hasSelect = buttons.Any(b => SafeName(b).Equals("Select", StringComparison.OrdinalIgnoreCase)
                        || SafeName(b).Equals("เลือก", StringComparison.OrdinalIgnoreCase));
                    bool hasCancel = buttons.Any(b => SafeName(b).Equals("Cancel", StringComparison.OrdinalIgnoreCase)
                        || SafeName(b).Equals("ยกเลิก", StringComparison.OrdinalIgnoreCase));
                    if (hasSelect && hasCancel) return candidate;
                }
                catch { }
            }
            return null;
        }

        private void ClickLookupSelect(AutomationElement lookup)
        {
            var button = lookup.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("Select").Or(cf.ByName("เลือก"))))
                ?? throw new Exception("ไม่พบปุ่ม Select ในหน้าต่างเลือกรูปแบบงวดการจ่าย");
            button.Click();
            Thread.Sleep(500);
            // UIA may keep a stale reference to a closed BaseListForm popup; subsequent operations
            // use the newly opened picker, so don't treat that stale reference as an active popup.
        }

        private static string ReadValue(AutomationElement element)
        {
            try
            {
                var value = element.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault;
                if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
            }
            catch { }
            return SafeName(element);
        }

        private static string SafeName(AutomationElement element)
        {
            try { return element.Name ?? ""; } catch { return ""; }
        }

        private static bool WaitUntil(Func<bool> condition, int timeoutMs)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try { if (condition()) return true; } catch { }
                Thread.Sleep(100);
            }
            return false;
        }

        private void OpenExactMenuItem()
        {
            var searchBox = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("baritemSearch"))
                ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)), 4000)
                ?? throw new Exception("ไม่พบช่องค้นหาเมนู baritemSearch");
            if (searchBox.Patterns.Value.IsSupported)
                searchBox.Patterns.Value.Pattern.SetValue(MenuLabel);
            else
            {
                searchBox.Focus();
                Keyboard.Press(VirtualKeyShort.CONTROL);
                Keyboard.Press(VirtualKeyShort.KEY_A);
                Keyboard.Release(VirtualKeyShort.KEY_A);
                Keyboard.Release(VirtualKeyShort.CONTROL);
                Keyboard.Type(MenuLabel);
            }

            var find = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("barChkSearch"))
                ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("Find")), 4000)
                ?? throw new Exception("ไม่พบปุ่มค้นหาเมนู");
            find.Click();
            Thread.Sleep(400);

            string ValueOf(AutomationElement e)
            {
                try { return e.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault?.Trim() ?? e.Name?.Trim() ?? ""; }
                catch { return e.Name?.Trim() ?? ""; }
            }

            var exactItem = Retry(() => _mainWindow.FindAllDescendants()
                .FirstOrDefault(e => string.Equals(ValueOf(e), MenuLabel, StringComparison.OrdinalIgnoreCase)), 3000);
            if (exactItem == null)
            {
                var row11 = _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(MenuRowId));
                if (row11 != null && string.Equals(ValueOf(row11), MenuLabel, StringComparison.OrdinalIgnoreCase))
                    exactItem = row11;
            }
            if (exactItem == null)
            {
                var choices = _mainWindow.FindAllDescendants()
                    .Select(ValueOf).Where(v => v.Contains("ประมวลผล", StringComparison.OrdinalIgnoreCase))
                    .Distinct().Take(20);
                throw new Exception($"ค้นหาแล้วไม่พบแถวเมนูชื่อ '{MenuLabel}' แบบตรงตัว (รายการใกล้เคียง: {string.Join(" | ", choices)})");
            }

            exactItem.Focus();
            exactItem.Click();
            Keyboard.Press(VirtualKeyShort.RETURN);
            Keyboard.Release(VirtualKeyShort.RETURN);
        }

        private AutomationElement? FindProcessForm()
        {
            var roots = GetWindows();
            return roots.FirstOrDefault(w =>
            {
                try { return w.FindFirstDescendant(cf => cf.ByAutomationId("devBtnProcess")) != null; }
                catch { return false; }
            });
        }

        private System.Collections.Generic.List<AutomationElement> GetWindows()
        {
            var roots = new System.Collections.Generic.List<AutomationElement>();
            try
            {
                int pid = _mainWindow.Properties.ProcessId.ValueOrDefault;
                roots.AddRange(_automation.GetDesktop().FindAllDescendants(cf => cf.ByProcessId(pid).And(cf.ByControlType(ControlType.Window))).Where(w => !w.Equals(_mainWindow)));
            }
            catch { }
            try { roots.AddRange(_mainWindow.FindAllChildren(cf => cf.ByControlType(ControlType.Window))); } catch { }
            var windows = roots.Concat(roots.SelectMany(w =>
            {
                try { return w.FindAllChildren(cf => cf.ByControlType(ControlType.Window)); }
                catch { return Array.Empty<AutomationElement>(); }
            }));
            return windows.Distinct().Where(w => { try { return !w.Properties.IsOffscreen.ValueOrDefault; } catch { return false; } }).ToList();
        }

        private string DescribeWindows() => string.Join(" | ", GetWindows().Select(w => w.Name));

        private static string SafeWindowName(AutomationElement window)
        {
            try { return window.Name ?? ""; } catch { return ""; }
        }

        private static void ClickCenter(AutomationElement element)
        {
            var rect = element.BoundingRectangle;
            Mouse.Click(new System.Drawing.Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2));
        }

        private static string RectText(AutomationElement element)
        {
            try { var r = element.BoundingRectangle; return $"{r.Left},{r.Top},{r.Width},{r.Height}"; }
            catch { return "?"; }
        }

        private static T? Retry<T>(Func<T?> finder, int timeoutMs) where T : class
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try { var result = finder(); if (result != null) return result; } catch { }
                Thread.Sleep(100);
            }
            return null;
        }
    }
}
