using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using HRMITestFlaUI.Base;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;

namespace HRMITestFlaUI.Module.Time_Attendance
{
    /// <summary>
    /// ทดสอบหน้าจอ "ประมวลผลจากระบบเวลาเข้าสู่ระบบเงินเดือน (แบบแจกแจง)" (FrmPPprocessTreeWinUC)
    /// ขั้นตอนอ้างอิง TestStep.xlsx (ชีท "เวลาเข้าเงินเดือน (แจกแจง)") แถว 6
    /// </summary>
    public class FrmPPprocessTreeWinUC
    {
        // =========================================================================================
        // หมวดที่ 1: ข้อมูลการทดสอบ (Test Data)
        // =========================================================================================

        // --- Data Records สำหรับใช้งานในแต่ละเฟส ---
        private sealed record ProcessItem(
            string Name,
            bool Navigate,
            string ProcessCode,
            string ParentNode,
            string MonthNode,
            string EmployeeCode,
            int ProgressCode,
            int ProgressTree,
            int ProgressEmployee,
            int ProgressProcess,
            int ProgressConfirm,
            int ProgressClose);

        // [ขั้นที่ 1] ข้อมูลทดสอบประมวลผลเวลาเข้าเงินเดือน แบบแจกแจง (Test Step แถว 6)  Progress: 5 -> 100
        private static readonly ProcessItem[] Step1_ProcessCases =
        {
            new(
                "TestCase_PPprocessTreeWinUC_1",
                true,
                "PTS202609-0001",
                "รูปแบบการจ่ายเงินเดือนปกติ",
                "เดือน ธันวาคม - 31/12/2570",
                "EMP00002",
                20, 40, 55, 65, 95, 100)
        };


        // =========================================================================================
        // หมวดที่ 2: ส่วนที่ขึ้นกับหน้าจอนั้นๆ (Screen-Specific Implementation)
        // **เมื่อเปลี่ยนไปทำหน้าจออื่น ต้องมาแก้ไขค่าและเมทอดในส่วนนี้ทั้งหมด**
        // =========================================================================================

        // 2.1 Locator & Mappings ประจำหน้าจอ
        private const string LogSource = "FrmPPprocessTreeWinUC";

        // ── เมนูหลัก ──
        private const string TimeAttendanceMenuName = "Time Attendance";
        private const string MenuRowName = "treeListColumn row 12";
        private const string MenuBaseLabel = "ประมวลผลจากระบบเวลาเข้าสู่ระบบเงินเดือน";   // แถว 11 ชื่อนี้ตรงตัว / แถว 12 ยาวกว่า
        private const string MenuLabel = "ประมวลผลจากระบบเวลาเข้าสู่ระบบเงินเดือน (แบบแจกแจง)";

        // ── Automation ID ──
        private const string IdProcessCode = "devBtnPosttoPayrollCode";   // รหัสรูปแบบการประมวลผล (ปุ่ม ... ขอบขวา)
        private const string IdBtnProcess = "devBtnProcess";
        private const string IdBtnClose = "devBtnClose";

        // 2.2 ค่า Timeout / Delay ประจำหน้าจอ
        private const int MenuTransitionDelayMs = 400;
        private const int PageLoadDelayMs = 800;
        private const int StepDelayMs = 800;
        private const int PopupDelayMs = 600;
        private const int EmployeeLoadMaxWaitMs = 20000;
        private const int ProcessMaxWaitMs = 10 * 60 * 1000;   // รอประมวลผลได้นานสุด 10 นาที
        private const int MaxMenuScrollSteps = 10;

        /// <summary>
        /// ขั้นที่ 1: เปิดหน้าจอ ประมวลผลเวลาเข้าเงินเดือน (แบบแจกแจง) (Progress 0 -> 5)
        /// </summary>
        public void NavigateToPPprocessTreeWinUC()
        {
            TestProgressHelper.Report(2, "Opening Time Attendance -> Post to Payroll (Tree)...");
            if (!TryOpenMenu("NavigateToPPprocessTreeWinUC")) return;
            TestProgressHelper.Report(5, "Post to Payroll (Tree) screen opened");
        }

        /// <summary>
        /// ขั้นที่ 2: ทดสอบประมวลผลเวลาเข้าเงินเดือน แบบแจกแจง — รันทุกแถวของ Test Step แล้วสรุปผลรวม (Progress 5 -> 100)
        /// </summary>
        public void RunAllValidationTests()
        {
            TestLogger.LogInfo(LogSource, "เริ่มทดสอบหน้าจอ ประมวลผลจากระบบเวลาเข้าสู่ระบบเงินเดือน (แบบแจกแจง)");

            foreach (var item in Step1_ProcessCases)
                RunScenario(item.Name, item.Navigate, () => RunProcessCase(item));

            TestLogger.LogInfo(LogSource, $"สรุปผลรวมทั้งหมด: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");
        }

        // --- Custom Mapping Logic ประจำหน้าจอนี้ ---

        /* แถว 6: เลือกรหัสรูปแบบ -> เลือกงวด/เดือนที่แถบซ้าย -> คลิกพนักงาน -> Process -> OK -> Close */
        private void RunProcessCase(ProcessItem item)
        {
            Step($"รหัสรูปแบบการประมวลผล: เลือก {item.ProcessCode}",
                () => PickProcessCode(item.ProcessCode), item.ProgressCode);

            Step($"แถบซ้าย: กด {item.ParentNode} / คลิกเดือน {item.MonthNode}",
                () => ClickTreeNode(item.ParentNode, item.MonthNode), item.ProgressTree);

            Step($"คลิกรหัสพนักงาน {item.EmployeeCode}",
                () => ClickEmployee(item.EmployeeCode), item.ProgressEmployee);

            Step("กดปุ่ม Process", ClickProcess, item.ProgressProcess);
            Step("รอประมวลผล แล้วกด OK", () => ClickDialogButton(ProcessMaxWaitMs, "OK", "ตกลง"), item.ProgressConfirm);
            Do("กด Close", ClickCloseButton, item.ProgressClose);
        }

        /* รัน 1 แถว: เปิดหน้าจอถ้ายังไม่เปิด -> ทำทุกขั้นตอน -> ถ้ามีขั้นตอนพลาดถือว่าแถวนั้น FAIL */
        private void RunScenario(string testName, bool navigate, Action step)
        {
            try
            {
                if (navigate && FindFormRoot() == null) OpenMenu();

                _stepErrors.Clear();
                step();
                if (_stepErrors.Count > 0) throw new Exception(string.Join(" || ", _stepErrors));

                LogSuccess(testName, "ทำสำเร็จตามขั้นตอนครบถ้วน");
            }
            catch (Exception ex)
            {
                CancelLookupIfOpen();
                LogFailure(testName, $"ไม่สำเร็จ: {DescribeException(ex)}");
            }
        }

        /* เปิดหน้าจอแบบไม่ throw (Log FAIL แล้วคืน false) */
        private bool TryOpenMenu(string stage)
        {
            try { OpenMenu(); return true; }
            catch (Exception ex) { LogFailure(stage, $"ไม่สำเร็จ: {DescribeException(ex)}"); return false; }
        }

        /* คลิกแท็บ Time Attendance -> เมนูแถว 12 แล้วยืนยันว่าหน้าจอเปิดจริง */
        private void OpenMenu()
        {
            var tab = Retry(() =>
                    _mainWindow.FindFirstDescendant(cf => cf.ByName(TimeAttendanceMenuName).And(cf.ByControlType(ControlType.TabItem)))
                    ?? _mainWindow.FindFirstDescendant(cf => cf.ByName(TimeAttendanceMenuName)), 5000)
                ?? throw new Exception($"หาเมนูหลัก '{TimeAttendanceMenuName}' ไม่เจอ");

            ClickCenter(tab);
            Thread.Sleep(MenuTransitionDelayMs);

            var menuRow = Retry(FindMenuRow, 3000) ?? ScrollToMenuRow()
                ?? throw new Exception($"หาเมนู '{MenuLabel}' ('{MenuRowName}') ไม่เจอ");

            ClickCenter(menuRow);
            Thread.Sleep(PageLoadDelayMs);

            if (Retry(FindFormRoot, 8000) == null)
                throw new Exception($"คลิกเมนูแล้ว แต่หน้าจอ '{MenuLabel}' ไม่เปิดขึ้น");
        }

        /* กด Line Down เลื่อนรายการเมนูลงจนเจอเมนูที่ต้องการ */
        private AutomationElement? ScrollToMenuRow()
        {
            var lineDown = _mainWindow.FindFirstDescendant(cf => cf.ByName("Line Down"));
            if (lineDown != null)
            {
                for (int i = 0; i < MaxMenuScrollSteps; i++)
                {
                    ClickCenter(lineDown);
                    var row = Retry(FindMenuRow, 500);
                    if (row != null) return row;
                }
            }
            return Retry(FindMenuRow, 3000);
        }

        /* หาแถวเมนู 'treeListColumn row 12' (ใช้เลย ยกเว้นข้อความตรงกับเมนูแถว 11)
           ถ้าไม่เจอ หาจากข้อความที่ขึ้นต้นเหมือนแถว 11 แต่ยาวกว่า */
        private AutomationElement? FindMenuRow()
        {
            var baseLabel = Normalize(MenuBaseLabel);

            var byRow = _mainWindow.FindFirstDescendant(cf => cf.ByName(MenuRowName));
            if (byRow != null && HasArea(byRow))
            {
                string value = "";
                try { value = byRow.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault ?? ""; } catch { }
                if (Normalize(value) != baseLabel) return byRow;
            }

            return _mainWindow.FindAllDescendants()
                .Where(e => HasArea(e) && !IsFormTitle(e))
                .FirstOrDefault(e =>
                {
                    var t = Normalize(ReadText(e));
                    return t.StartsWith(baseLabel) && t.Length > baseLabel.Length;
                });
        }

        /* หน้าต่างประมวลผล (แบบแจกแจง) — จำจากช่อง รหัสรูปแบบการประมวลผล */
        private AutomationElement? FindFormRoot() => GetAppWindows().FirstOrDefault(w => HasId(w, IdProcessCode));

        /* หน้าต่างหลักของหน้าจอ — ไม่นับเป็น popup */
        private static bool IsMainForm(AutomationElement w) => HasId(w, IdProcessCode);

        /* หา control ในหน้าจอประมวลผลตาม AutomationId (เฉพาะตัวที่มองเห็น) */
        private AutomationElement FindInForm(string automationId)
        {
            var form = FindFormRoot() ?? throw new Exception("หน้าจอประมวลผล (แบบแจกแจง) ไม่ได้เปิดอยู่");
            return Retry(() => form.FindAllDescendants(cf => cf.ByAutomationId(automationId)).FirstOrDefault(IsVisible), 8000)
                ?? throw new Exception($"หา control '{automationId}' ไม่เจอ");
        }

        /* รหัสรูปแบบการประมวลผล: กด ... (ขอบขวาของช่อง) -> เลือก <code> -> กด Select */
        private void PickProcessCode(string code)
        {
            try
            {
                CancelLookupIfOpen();
                var control = FindInForm(IdProcessCode);
                WaitUntilEnabled(control, 3000);

                var rect = control.BoundingRectangle;
                ClickAt(new Point(rect.Right - 9, rect.Top + rect.Height / 2));
                Thread.Sleep(PopupDelayMs);

                if (Retry(GetLookupWindow, 8000) == null)
                    throw new Exception("กด ... แล้ว หน้าต่างเลือกรหัสรูปแบบการประมวลผลไม่เปิด");

                SelectLookupRow(code);
                ClickLookupSelect();
            }
            catch
            {
                CancelLookupIfOpen();
                throw;
            }
        }

        /* ตาราง/tree ในหน้าจอประมวลผล เรียงจากใหญ่ไปเล็ก */
        private List<AutomationElement> FormGrids()
        {
            var form = FindFormRoot() ?? throw new Exception("หน้าจอประมวลผล (แบบแจกแจง) ไม่ได้เปิดอยู่");
            try
            {
                return form.FindAllDescendants(cf => cf.ByControlType(ControlType.Table)
                        .Or(cf.ByControlType(ControlType.DataGrid))
                        .Or(cf.ByControlType(ControlType.Tree))
                        .Or(cf.ByControlType(ControlType.List)))
                    .Where(IsVisible)
                    .OrderByDescending(g => g.BoundingRectangle.Width * g.BoundingRectangle.Height)
                    .ToList();
            }
            catch { return new List<AutomationElement>(); }
        }

        /* tree แถบซ้าย: ตารางที่มี node ปี (เช่น 2570) หรือชื่อรูปแบบการจ่าย */
        private AutomationElement FindLeftTree()
        {
            return Retry(() => FormGrids().FirstOrDefault(g =>
            {
                var snap = TakeSnapshot(g, keepWide: true);
                return snap.Cells.Any(c => IsYearText(c.Value) || IsYearText(c.Name) || c.Value.Contains("รูปแบบ") || c.Value.Contains("งวด"));
            }), 8000) ?? throw new Exception("หา tree แถบซ้ายไม่เจอ");
        }

        /* แถบซ้าย: กด <parent> แล้วคลิก <child>
           tree เป็น ปี -> รูปแบบงวดการจ่าย -> เดือน และ node ปีบางอันหุบอยู่ จึงขยายปีที่หุบทีละปีจนเจอ parent
           ข้อความเดือนในชีท "เดือน ธันวาคม - ..." บนจอเป็น "(12)ธันวาคม - ..." จึงเทียบแบบ Contains */
        private void ClickTreeNode(string parentText, string childText)
        {
            var tree = FindLeftTree();

            var parentKey = Normalize(parentText);
            var childKey = Normalize(childText.Trim().StartsWith("เดือน") ? childText.Trim().Substring("เดือน".Length) : childText);

            bool IsParent(GridCell c) => Normalize(CellText(c)) == parentKey;
            bool IsChild(GridCell c) => Normalize(CellText(c)).Contains(childKey);

            // 1) ยังไม่เห็น parent -> ขยาย node ปีที่หุบอยู่ทีละปี
            var (snap, parent) = ScrollFind(tree, s => s.Cells.FirstOrDefault(IsParent), maxMs: 5000, keepWide: true);
            var triedYears = new HashSet<string>();

            while (parent == null)
            {
                var ordered = snap.Cells.Where(c => CellText(c).Trim().Length > 0).OrderBy(c => c.CY).ToList();
                bool IsCollapsedYear(GridCell c)
                {
                    var next = ordered.FirstOrDefault(o => o.CY > c.CY + c.Rect.Height / 2);
                    return next == null || IsYearText(CellText(next));
                }

                var year = ordered.Where(c => IsYearText(CellText(c)) && !triedYears.Contains(CellText(c).Trim()))
                                  .FirstOrDefault(IsCollapsedYear);
                if (year == null) break;

                var yearText = CellText(year).Trim();
                triedYears.Add(yearText);
                parent = ExpandYearAndFind(tree, yearText, IsParent, out snap);
            }

            if (parent == null)
                throw new Exception($"หา '{parentText}' ที่แถบซ้ายไม่เจอ (ที่เห็น: {DescribeCells(snap)})");

            // 2) กด parent แล้วขยาย เพื่อให้เห็นเดือน
            ClickAt(new Point(parent.CX, parent.CY));
            Thread.Sleep(StepDelayMs);

            var (afterSnap, child) = ScrollFind(tree, s => s.Cells.FirstOrDefault(IsChild), maxMs: 3000, keepWide: true);
            if (child == null)
            {
                (afterSnap, parent) = ScrollFind(tree, s => s.Cells.FirstOrDefault(IsParent), maxMs: 5000, keepWide: true);
                if (parent != null && !TryExpandByPattern(parent)) ExpandByKeyboard(parent);
                (afterSnap, child) = ScrollFind(tree, s => s.Cells.FirstOrDefault(IsChild), maxMs: 15000, keepWide: true);
            }

            if (child == null)
                throw new Exception($"กด '{parentText}' แล้ว หา '{childText}' ไม่เจอ (ที่เห็น: {DescribeCells(afterSnap)})");

            // 3) คลิกเดือน
            ClickAt(new Point(child.CX, child.CY));
            Thread.Sleep(PageLoadDelayMs);
        }

        /* ขยาย node ปีแล้วหา parent — ลองทีละวิธี:
           ExpandCollapse -> คลิก + ลูกศรขวา -> ปุ่ม + -> ดับเบิลคลิก -> คลิกปุ่ม +/- หน้า node */
        private GridCell? ExpandYearAndFind(AutomationElement tree, string yearText, Func<GridCell, bool> isParent, out GridSnapshot snap)
        {
            GridCell? FindYear(GridSnapshot s) => s.Cells.FirstOrDefault(c => CellText(c).Trim() == yearText);

            var attempts = new List<Action<GridCell>>
            {
                y => TryExpandByPattern(y),
                y => ExpandByKeyboard(y),
                y => PressOn(y, VirtualKeyShort.ADD),
                y =>
                {
                    Mouse.MoveTo(new Point(y.CX, y.CY));
                    Thread.Sleep(50);
                    Mouse.DoubleClick(MouseButton.Left);
                    Thread.Sleep(StepDelayMs);
                },
                y => { ClickAt(new Point(y.Rect.Left - 10, y.CY)); Thread.Sleep(StepDelayMs); },
            };

            var (s0, _) = ScrollFind(tree, s => FindYear(s), maxMs: 5000, keepWide: true);
            snap = s0;
            int countBefore = snap.Cells.Count;

            foreach (var attempt in attempts)
            {
                var (sy, year) = ScrollFind(tree, s => FindYear(s), maxMs: 5000, keepWide: true);
                if (year == null) { snap = sy; return null; }

                attempt(year);

                var (after, parent) = ScrollFind(tree, s => s.Cells.FirstOrDefault(isParent), maxMs: 8000, keepWide: true);
                snap = after;
                if (parent != null) return parent;

                // ปีนี้ขยายแล้ว (มีรายการเพิ่มขึ้น) แต่ไม่มี parent -> ไม่ต้องลองวิธีอื่นกับปีนี้
                if (after.Cells.Count > countBefore) return null;
            }
            return null;
        }

        /* ขยาย node ด้วย ExpandCollapse ของ node (หรือ parent สูงสุด 3 ชั้น) — true ถ้าสั่งขยาย node ที่หุบอยู่ได้ */
        private bool TryExpandByPattern(GridCell cell)
        {
            var el = cell.Element;
            try
            {
                var walker = _automation.TreeWalkerFactory.GetControlViewWalker();
                for (int level = 0; level < 3 && el != null; level++)
                {
                    var ec = el.Patterns.ExpandCollapse.PatternOrDefault;
                    if (ec != null)
                    {
                        var state = ec.ExpandCollapseState.ValueOrDefault;
                        if (state == ExpandCollapseState.Collapsed || state == ExpandCollapseState.PartiallyExpanded)
                        {
                            ec.Expand();
                            Thread.Sleep(400);
                            return true;
                        }
                        return false;   // ขยายอยู่แล้ว หรือเป็น node ปลาย
                    }
                    el = walker.GetParent(el);
                }
            }
            catch { }
            return false;
        }

        /* ขยาย node ด้วยคีย์บอร์ด: คลิก node แล้วกดลูกศรขวา */
        private static void ExpandByKeyboard(GridCell cell) => PressOn(cell, VirtualKeyShort.RIGHT);

        /* คลิก node แล้วกดปุ่มที่ระบุ */
        private static void PressOn(GridCell cell, VirtualKeyShort key)
        {
            ClickAt(new Point(cell.CX, cell.CY));
            Thread.Sleep(300);
            PressKey(key);
            Thread.Sleep(StepDelayMs);
        }

        /* คลิกรหัสพนักงาน — ตารางพนักงานโหลดหลังคลิกเดือน จึงรอได้ถึง 20 วินาที */
        private void ClickEmployee(string code)
        {
            var sw = Stopwatch.StartNew();
            (Rectangle Rect, List<string> Seen) result = (Rectangle.Empty, new List<string>());

            while (sw.ElapsedMilliseconds < EmployeeLoadMaxWaitMs)
            {
                result = FindTextAnywhere(code);
                if (!result.Rect.IsEmpty) break;
                Thread.Sleep(1000);
            }

            if (result.Rect.IsEmpty)
            {
                var seen = result.Seen.Count == 0 ? "(ไม่มีข้อความที่ขึ้นต้นด้วย EMP ในหน้าจอ)" : string.Join(" | ", result.Seen.Take(20));
                throw new Exception($"หารหัสพนักงาน '{code}' ไม่เจอ (รหัสที่เห็นในหน้าจอ: {seen})");
            }

            ClickAt(GetCenter(result.Rect));
            Thread.Sleep(StepDelayMs);
        }

        /* อ่านทุก element ในหน้าจอครั้งเดียว (CacheRequest) แล้วหาตัวที่ข้อความตรงและมองเห็น
           คืนตำแหน่ง (Empty ถ้าไม่เจอ) และข้อความที่ขึ้นต้นเหมือนกัน (ไว้ใส่ใน log) */
        private (Rectangle Rect, List<string> Seen) FindTextAnywhere(string text)
        {
            var form = FindFormRoot() ?? throw new Exception("หน้าจอประมวลผล (แบบแจกแจง) ไม่ได้เปิดอยู่");
            var lib = _automation.PropertyLibrary;
            var target = Normalize(text);
            var prefix = new string(target.TakeWhile(char.IsLetter).ToArray());   // เช่น "EMP"
            var seen = new List<string>();
            var formRect = form.BoundingRectangle;

            var cr = new CacheRequest { TreeScope = TreeScope.Element };
            cr.Add(lib.Element.Name);
            cr.Add(lib.Element.BoundingRectangle);
            cr.Add(lib.Element.IsOffscreen);
            cr.Add(lib.Value.Value);
            cr.Add(lib.LegacyIAccessible.Value);
            cr.Add(lib.LegacyIAccessible.Name);

            using (cr.Activate())
            {
                foreach (var e in form.FindAllDescendants())
                {
                    var fe = e.FrameworkAutomationElement;
                    if (!fe.TryGetPropertyValue<Rectangle>(lib.Element.BoundingRectangle, out var rect)
                        || rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0
                        || !formRect.Contains(GetCenter(rect)))
                        continue;
                    if (fe.TryGetPropertyValue<bool>(lib.Element.IsOffscreen, out var off) && off) continue;

                    var texts = new List<string>();
                    if (fe.TryGetPropertyValue<string>(lib.Element.Name, out var n)) texts.Add(n ?? "");
                    if (fe.TryGetPropertyValue<string>(lib.Value.Value, out var v)) texts.Add(v ?? "");
                    if (fe.TryGetPropertyValue<string>(lib.LegacyIAccessible.Value, out var lv)) texts.Add(lv ?? "");
                    if (fe.TryGetPropertyValue<string>(lib.LegacyIAccessible.Name, out var ln)) texts.Add(ln ?? "");

                    foreach (var t in texts.Select(Normalize).Where(t => t.Length > 0))
                    {
                        if (t == target) return (rect, seen);
                        if (prefix.Length > 0 && t.StartsWith(prefix) && !seen.Contains(t)) seen.Add(t);
                    }
                }
            }
            return (Rectangle.Empty, seen);
        }

        /* กดปุ่ม Process */
        private void ClickProcess()
        {
            CancelLookupIfOpen();
            var btn = FindInForm(IdBtnProcess);
            if (!WaitUntilEnabled(btn, 5000))
                throw new Exception("ปุ่ม Process กดไม่ได้ (ไม่ enable)");

            SafeClickElement(btn);
            Thread.Sleep(PageLoadDelayMs);
        }

        /* กดปุ่ม Close (devBtnClose) แล้วรอจนหน้าจอปิด */
        private void ClickCloseButton()
        {
            var btn = FindInForm(IdBtnClose);
            WaitUntilEnabled(btn, 5000);
            SafeClickElement(btn);

            if (!WaitUntil(() => FindFormRoot() == null, 8000, 300))
                throw new Exception("กด Close แล้ว แต่หน้าจอประมวลผลยังไม่ปิด (อาจมีกล่องข้อความถามอยู่)");
            Thread.Sleep(PageLoadDelayMs);
        }

        /* ข้อความที่เป็นปี เช่น 2569 / 2570 (node ชั้นบนสุดของแถบซ้าย) */
        private static bool IsYearText(string text) => System.Text.RegularExpressions.Regex.IsMatch(text.Trim(), @"^\d{4}$");

        /* ข้อความในตารางที่ใช้เทียบ: ใช้ Value ก่อน ถ้าว่างใช้ Name */
        private static string CellText(GridCell c) => c.Value.Trim().Length > 0 ? c.Value : c.Name;

        /* ตรวจว่า element เป็นหัวข้อ/กรอบของหน้าจอ (ไม่ใช่แถวเมนู) */
        private static bool IsFormTitle(AutomationElement e)
        {
            try
            {
                var type = e.Properties.ControlType.ValueOrDefault;
                return type == ControlType.Window || type == ControlType.TitleBar || type == ControlType.Pane;
            }
            catch { return false; }
        }


        // =========================================================================================
        // หมวดที่ 3: ส่วนการทำงานกลางที่ตายตัว (Common / Reusable Helpers)
        // **ใช้งานร่วมกันได้ทุกหน้าจอ ไม่ต้องแก้ไขคำสั่งเมื่อเปลี่ยนหน้า**
        // =========================================================================================

        private readonly AutomationElement _mainWindow;
        private readonly UIA3Automation _automation;
        private int _totalSuccess = 0, _totalFail = 0;
        private const int TimeoutMs = 6000;
        private const int PollMs = 300;

        // ขั้นตอนที่ไม่สำเร็จในแถวปัจจุบัน (ทำขั้นตอนถัดไปต่อจนครบ แล้วค่อยสรุปว่าแถวนั้น FAIL)
        private readonly List<string> _stepErrors = new List<string>();

        private const string LookupSelectName = "Select";
        private const string LookupCancelName = "Cancel";

        // ชื่อหน้าต่างกล่องข้อความที่รู้จัก (กล่องหลังกด Process ชื่อ "ตรวจสอบ")
        private static readonly string[] MessageBoxTitles = { "ตรวจสอบ", "ข้อความ", "Information", "Warning", "Exclamation", "Error" };

        public FrmPPprocessTreeWinUC(AutomationElement mainWindow, UIA3Automation automation)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

        // ── Step Runner (Do / Step) ──

        /* ทำขั้นตอนหนึ่ง ถ้าไม่สำเร็จให้จดไว้แล้วทำขั้นตอนถัดไปต่อ + รายงาน Progress */
        private void Do(string stepName, Action action, int progress)
        {
            try { action(); }
            catch (Exception ex) { _stepErrors.Add($"{stepName}: {ex.Message}"); }
            TestProgressHelper.Report(progress, stepName);
        }

        /* ขั้นตอนที่ถ้าไม่สำเร็จจะทำต่อไม่ได้ — หยุดแถวนี้ทันที */
        private void Step(string stepName, Action action, int progress)
        {
            try { action(); }
            catch (Exception ex)
            {
                _stepErrors.Add($"{stepName}: {ex.Message}");
                throw new Exception(string.Join(" || ", _stepErrors));
            }
            TestProgressHelper.Report(progress, stepName);
        }

        // ── Window Helpers ──

        /* หน้าต่างทั้งหมดของโปรแกรม (ยกเว้นหน้าจอหลัก) ทั้งแบบลอยและแบบอยู่ในหน้าจอหลัก */
        private List<AutomationElement> GetAppWindows()
        {
            var windows = new List<AutomationElement>();
            try
            {
                int pid = _mainWindow.Properties.ProcessId.ValueOrDefault;
                windows.AddRange(_automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(pid)).Where(w => !w.Equals(_mainWindow)));
            }
            catch { }
            try { windows.AddRange(_mainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Window))); } catch { }
            return windows.Where(HasArea).ToList();
        }

        // ── Lookup Window Helpers ──

        /* หน้าต่างเลือกข้อมูลที่เปิดอยู่ — จำจากการมีปุ่ม Select และ Cancel */
        private AutomationElement? GetLookupWindow()
        {
            var candidates = GetAppWindows();
            var form = FindFormRoot();
            if (form != null)
            {
                try { candidates.AddRange(form.FindAllDescendants(cf => cf.ByControlType(ControlType.Window))); } catch { }
            }

            return candidates.Where(w => !IsMainForm(w))
                .FirstOrDefault(w => FindButtonIn(w, LookupSelectName) != null && FindButtonIn(w, LookupCancelName) != null);
        }

        private static AutomationElement? FindButtonIn(AutomationElement root, string name)
        {
            try
            {
                return root.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                    .FirstOrDefault(b => TextMatches(b, name) && IsVisible(b));
            }
            catch { return null; }
        }

        /* ตารางที่ใหญ่ที่สุดในหน้าต่างเลือกข้อมูล */
        private static AutomationElement GetLookupGrid(AutomationElement lookup)
        {
            try
            {
                return lookup.FindAllDescendants(cf => cf.ByControlType(ControlType.Table).Or(cf.ByControlType(ControlType.DataGrid)))
                    .Where(HasArea)
                    .OrderByDescending(g => g.BoundingRectangle.Width * g.BoundingRectangle.Height)
                    .FirstOrDefault() ?? lookup;
            }
            catch { return lookup; }
        }

        /* คลิกแถวที่มี <code> ในหน้าต่างเลือกข้อมูล */
        private void SelectLookupRow(string code)
        {
            var lookup = GetLookupWindow() ?? throw new Exception("หน้าต่างเลือกข้อมูลไม่ได้เปิดอยู่");
            var (snap, cell) = ScrollFind(GetLookupGrid(lookup), s => s.Cells.FirstOrDefault(c => c.Is(code)));
            if (cell == null)
                throw new Exception($"หารหัส '{code}' ในหน้าต่างเลือกข้อมูลไม่เจอ (ข้อความในตารางที่เห็นล่าสุด: {DescribeCells(snap)})");

            ClickAt(new Point(cell.CX, cell.CY));
            Thread.Sleep(StepDelayMs);
        }

        /* กด Select ในหน้าต่างเลือกข้อมูล แล้วรอจนหน้าต่างปิด */
        private void ClickLookupSelect()
        {
            var lookup = GetLookupWindow() ?? throw new Exception("หน้าต่างเลือกข้อมูลไม่ได้เปิดอยู่");
            var btn = FindButtonIn(lookup, LookupSelectName) ?? throw new Exception("หาปุ่ม Select ไม่เจอ");

            SafeClickElement(btn);

            if (!WaitUntil(() => GetLookupWindow() == null, 8000, 300))
                throw new Exception("กด Select แล้ว แต่หน้าต่างเลือกข้อมูลยังไม่ปิด");
            Thread.Sleep(StepDelayMs);
        }

        /* ถ้าหน้าต่างเลือกข้อมูลยังเปิดค้าง ให้กด Cancel (หรือ Esc) ปิดก่อน */
        private void CancelLookupIfOpen()
        {
            try
            {
                var lookup = GetLookupWindow();
                if (lookup == null) return;

                var cancel = FindButtonIn(lookup, LookupCancelName);
                if (cancel != null) ClickCenter(cancel);
                else PressKey(VirtualKeyShort.ESCAPE);
                Thread.Sleep(StepDelayMs);
            }
            catch { }
        }

        // ── Grid Snapshot Helpers ──
        //   ตาราง DevExpress มีหลายร้อยช่อง อ่านทีละ property จะช้ามาก
        //   จึงอ่านทุกช่องครั้งเดียวด้วย CacheRequest แล้วค่อยหาในข้อมูลที่อ่านมา

        private sealed class GridCell
        {
            public string Name = "";
            public string Value = "";
            public Rectangle Rect;
            public bool? Checked;
            public AutomationElement? Element;   // ไว้สั่งขยาย node ของ tree
            public int CX => Rect.Left + Rect.Width / 2;
            public int CY => Rect.Top + Rect.Height / 2;

            public bool Is(string text) =>
                string.Equals(Name.Trim(), text, StringComparison.OrdinalIgnoreCase)
                || string.Equals(Value.Trim(), text, StringComparison.OrdinalIgnoreCase);
        }

        private sealed class GridSnapshot
        {
            public Rectangle GridRect;
            public List<GridCell> Headers = new List<GridCell>();
            public List<GridCell> Cells = new List<GridCell>();   // ช่องข้อมูลในกรอบตาราง ใต้หัวคอลัมน์
        }

        /* อ่านตารางแบบ cache ก่อน ถ้าใช้ไม่ได้อ่านแบบปกติ (ช้ากว่า) */
        private GridSnapshot TakeSnapshot(AutomationElement grid, bool keepWide = false)
        {
            try { return ReadSnapshot(grid, useCache: true, keepWide); }
            catch { return ReadSnapshot(grid, useCache: false, keepWide); }
        }

        private GridSnapshot ReadSnapshot(AutomationElement grid, bool useCache, bool keepWide)
        {
            var lib = _automation.PropertyLibrary;
            var snap = new GridSnapshot();
            try { snap.GridRect = grid.BoundingRectangle; } catch { }

            IDisposable? cacheScope = null;
            if (useCache)
            {
                var cr = new CacheRequest { TreeScope = TreeScope.Element };
                cr.Add(lib.Element.Name);
                cr.Add(lib.Element.BoundingRectangle);
                cr.Add(lib.Element.ControlType);
                cr.Add(lib.Value.Value);
                cr.Add(lib.Toggle.ToggleState);
                cr.Add(lib.LegacyIAccessible.Value);
                cr.Add(lib.LegacyIAccessible.State);
                cacheScope = cr.Activate();
            }

            var all = new List<GridCell>();
            try
            {
                foreach (var e in grid.FindAllDescendants())
                {
                    var fe = e.FrameworkAutomationElement;
                    if (!fe.TryGetPropertyValue<Rectangle>(lib.Element.BoundingRectangle, out var rect)
                        || rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0)
                        continue;

                    var cell = new GridCell { Rect = rect, Element = e };
                    if (fe.TryGetPropertyValue<string>(lib.Element.Name, out var name)) cell.Name = name ?? "";
                    if (fe.TryGetPropertyValue<string>(lib.Value.Value, out var value)) cell.Value = value ?? "";
                    cell.Checked = ReadCheckedCached(e, cell.Value);

                    bool isHeader = fe.TryGetPropertyValue<ControlType>(lib.Element.ControlType, out var type)
                                    && (type == ControlType.Header || type == ControlType.HeaderItem);
                    if (isHeader) snap.Headers.Add(cell);
                    else all.Add(cell);
                }
            }
            finally { cacheScope?.Dispose(); }

            // keepWide: แถวของ tree กว้างเต็มรายการ จึงไม่ตัดช่องที่กว้างทิ้ง
            var area = snap.GridRect;
            int headerBottom = snap.Headers.Where(h => area.Contains(new Point(h.CX, h.CY)))
                                           .Select(h => h.Rect.Bottom).DefaultIfEmpty(area.Top).Max();
            snap.Cells = all.Where(c =>
                    area.Contains(new Point(c.CX, c.CY))
                    && c.CY > headerBottom
                    && (keepWide || c.Rect.Width < area.Width * 0.9))
                .ToList();
            return snap;
        }

        /* อ่านสถานะติ๊กจากข้อมูลที่ cache ไว้ — true/false หรือ null ถ้าไม่ใช่ช่องติ๊ก */
        private bool? ReadCheckedCached(AutomationElement e, string value)
        {
            var fe = e.FrameworkAutomationElement;
            var lib = _automation.PropertyLibrary;
            try
            {
                if (fe.TryGetPropertyValue<ToggleState>(lib.Toggle.ToggleState, out var ts)) return ts == ToggleState.On;
            }
            catch { }

            var texts = new List<string> { value };
            try
            {
                if (fe.TryGetPropertyValue<string>(lib.LegacyIAccessible.Value, out var lv)) texts.Add(lv ?? "");
            }
            catch { }

            foreach (var t in texts.Select(t => (t ?? "").Trim()))
            {
                if (t.Equals("Checked", StringComparison.OrdinalIgnoreCase) || t.Equals("True", StringComparison.OrdinalIgnoreCase)) return true;
                if (t.Equals("Unchecked", StringComparison.OrdinalIgnoreCase) || t.Equals("False", StringComparison.OrdinalIgnoreCase)) return false;
            }
            return null;
        }

        /* หาในตาราง ถ้าไม่เจอ: เลื่อนขึ้นบนสุด แล้วเลื่อนลงทีละนิด จนเจอ/ถึงท้ายตาราง/หมดเวลา */
        private (GridSnapshot Last, T? Found) ScrollFind<T>(AutomationElement grid, Func<GridSnapshot, T?> match,
                                                            int maxMs = 60000, bool keepWide = false)
            where T : class
        {
            var sw = Stopwatch.StartNew();
            var snap = TakeSnapshot(grid, keepWide);
            var found = match(snap);
            if (found != null) return (snap, found);

            var center = GetCenter(snap.GridRect);
            Mouse.MoveTo(center);
            Mouse.Scroll(50);   // ขึ้นบนสุด
            Thread.Sleep(400);

            string? prevSignature = null;
            while (sw.ElapsedMilliseconds < maxMs)
            {
                snap = TakeSnapshot(grid, keepWide);
                found = match(snap);
                if (found != null) return (snap, found);

                var signature = string.Join("|", snap.Cells.Select(c => $"{c.CY}:{c.Value}"));
                if (signature == prevSignature) break;   // เลื่อนแล้วไม่เปลี่ยน = ถึงท้ายตาราง
                prevSignature = signature;

                Mouse.MoveTo(center);
                Mouse.Scroll(-3);
                Thread.Sleep(300);
            }
            return (snap, null);
        }

        /* ข้อความในตารางที่เห็นล่าสุด (ใส่ใน log ตอนหาไม่เจอ) */
        private static string DescribeCells(GridSnapshot snap)
        {
            var texts = snap.Cells.Select(c => c.Value.Trim())
                .Where(t => t.Length > 0 && t != "Checked" && t != "Unchecked")
                .Distinct().Take(30).ToList();
            return texts.Count == 0 ? "(อ่านข้อความในตารางไม่ได้)" : string.Join(" | ", texts);
        }

        // ── Message Box Helpers ──

        /* รอกล่องข้อความ (เช่น "ตรวจสอบ" หลังกด Process) แล้วกดปุ่มตามชื่อ
           ถ้าเจอกล่องแต่ UIA มองไม่เห็นปุ่ม จะโฟกัสกล่องแล้วกด Enter แทน */
        private void ClickDialogButton(int timeoutMs, params string[] buttonNames)
        {
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                var btn = FindButtonByNameFast(buttonNames);
                if (btn != null)
                {
                    PressDialogButton(btn);
                    return;
                }

                var dialog = FindMessageBoxWindow();
                if (dialog != null)
                {
                    for (int attempt = 0; attempt < 2; attempt++)
                    {
                        PressEnterOn(dialog);
                        if (WaitDialogClosed(dialog)) return;
                    }
                    throw new Exception($"เจอกล่องข้อความ '{SafeName(dialog)}' แต่กด '{string.Join("/", buttonNames)}' แล้วกล่องไม่ปิด");
                }

                Thread.Sleep(1000);
            }

            throw new Exception($"รอเกิน {timeoutMs / 60000} นาที แล้วยังไม่มีกล่องข้อความ/ปุ่ม '{string.Join("/", buttonNames)}' ขึ้น " +
                                $"(หน้าต่างที่เห็น: {DescribeAppWindows()})");
        }

        /* เงื่อนไขชื่อปุ่ม ทั้งแบบปกติและแบบมี & นำหน้า (เช่น OK และ &OK) */
        private static FlaUI.Core.Conditions.ConditionBase ButtonNameCondition(FlaUI.Core.Conditions.ConditionFactory cf, string[] names)
        {
            FlaUI.Core.Conditions.ConditionBase? nameCond = null;
            foreach (var n in names.SelectMany(n => new[] { n, "&" + n }).Distinct())
            {
                var c = cf.ByName(n);
                nameCond = nameCond == null ? c : nameCond.Or(c);
            }
            return cf.ByControlType(ControlType.Button).And(nameCond!);
        }

        /* หาปุ่มตามชื่อ: หน้าต่างลอยของโปรแกรมก่อน แล้วค่อยหน้าจอหลัก */
        private AutomationElement? FindButtonByNameFast(string[] names)
        {
            var roots = new List<AutomationElement>();
            try
            {
                int pid = _mainWindow.Properties.ProcessId.ValueOrDefault;
                roots.AddRange(_automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(pid)).Where(w => !w.Equals(_mainWindow)));
            }
            catch { }
            roots.Add(_mainWindow);

            foreach (var root in roots)
            {
                try
                {
                    var btn = root.FindAllDescendants(cf => ButtonNameCondition(cf, names)).FirstOrDefault(HasArea);
                    if (btn != null) return btn;
                }
                catch { }
            }
            return null;
        }

        /* หน้าต่างกล่องข้อความ ทั้งแบบลอยและแบบซ้อนในหน้าจอหลัก */
        private AutomationElement? FindMessageBoxWindow()
        {
            try
            {
                int pid = _mainWindow.Properties.ProcessId.ValueOrDefault;
                var top = _automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(pid))
                    .FirstOrDefault(w => IsMessageBox(w) && HasArea(w));
                if (top != null) return top;
            }
            catch { }

            try
            {
                return _mainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Window))
                    .FirstOrDefault(w => IsMessageBox(w) && HasArea(w));
            }
            catch { return null; }
        }

        /* คลิกปุ่มในกล่องข้อความ ถ้ากล่องไม่ปิด ลอง Invoke และกด Enter ตามลำดับ */
        private void PressDialogButton(AutomationElement btn)
        {
            AutomationElement? dialog = null;
            try
            {
                var walker = _automation.TreeWalkerFactory.GetControlViewWalker();
                var parent = walker.GetParent(btn);
                while (parent != null && parent.Properties.ControlType.ValueOrDefault != ControlType.Window)
                    parent = walker.GetParent(parent);
                dialog = parent;
            }
            catch { }

            SafeClickElement(btn);
            if (dialog == null || WaitDialogClosed(dialog)) { Thread.Sleep(PageLoadDelayMs); return; }

            try { btn.Patterns.Invoke.PatternOrDefault?.Invoke(); } catch { }
            if (WaitDialogClosed(dialog)) return;

            PressEnterOn(dialog);
            if (WaitDialogClosed(dialog)) return;

            throw new Exception($"กดปุ่ม '{SafeName(btn)}' แล้ว แต่กล่องข้อความ '{SafeName(dialog)}' ยังไม่ปิด");
        }

        private static bool IsMessageBox(AutomationElement window)
        {
            var name = SafeName(window).Trim();
            return MessageBoxTitles.Any(t => string.Equals(name, t, StringComparison.OrdinalIgnoreCase));
        }

        /* โฟกัสกล่องข้อความแล้วกด Enter */
        private static void PressEnterOn(AutomationElement dialog)
        {
            try { dialog.Focus(); } catch { }
            try { dialog.AsWindow().SetForeground(); } catch { }
            Thread.Sleep(200);
            PressKey(VirtualKeyShort.RETURN);
        }

        /* รอจนกล่องข้อความปิด — คืน true ถ้าปิดแล้ว */
        private static bool WaitDialogClosed(AutomationElement dialog, int timeoutMs = 3000)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                Thread.Sleep(300);
                if (!IsVisible(dialog))   // ปิดแล้ว หรืออ่าน property ไม่ได้
                {
                    Thread.Sleep(PageLoadDelayMs);
                    return true;
                }
            }
            return false;
        }

        // ── Click / Keyboard / Element Utilities ──

        private static Point GetCenter(Rectangle rect) => new Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);

        private static void ClickAt(Point p)
        {
            Mouse.MoveTo(p);
            Thread.Sleep(50);
            Mouse.Click(MouseButton.Left);
            Thread.Sleep(100);
        }

        private static void ClickCenter(AutomationElement e) => ClickAt(GetCenter(e.BoundingRectangle));

        /* คลิกอย่างปลอดภัย: รอพิกัดใช้ได้ -> element.Click() -> Invoke pattern */
        private static void SafeClickElement(AutomationElement element, int waitForRectMs = 3000)
        {
            if (WaitUntil(() => HasArea(element), waitForRectMs, 150))
            {
                ClickCenter(element);
                return;
            }

            try { element.Click(); }
            catch (FlaUI.Core.Exceptions.NoClickablePointException)
            {
                try
                {
                    var invoke = element.Patterns.Invoke.PatternOrDefault;
                    if (invoke != null) { invoke.Invoke(); return; }
                }
                catch { }
                throw new Exception($"ไม่สามารถคลิก '{SafeName(element)}' ได้ (หาตำแหน่งที่คลิกได้บนหน้าจอไม่เจอ)");
            }
        }

        private static void PressKey(VirtualKeyShort key)
        {
            Keyboard.Press(key);
            Keyboard.Release(key);
        }

        private static bool TextMatches(AutomationElement e, string text)
        {
            try
            {
                if (string.Equals(e.Name?.Trim(), text, StringComparison.OrdinalIgnoreCase)) return true;
                var vp = e.Patterns.Value.PatternOrDefault;
                return vp != null && string.Equals(vp.Value.ValueOrDefault?.Trim(), text, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static string ReadText(AutomationElement e)
        {
            try
            {
                var name = e.Name ?? "";
                return name.Length > 0 ? name : e.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault ?? "";
            }
            catch { return ""; }
        }

        private static string Normalize(string? s) => new string((s ?? "").Where(c => !char.IsWhiteSpace(c)).ToArray());

        private static bool HasId(AutomationElement w, string automationId)
        {
            try { return w.FindFirstDescendant(cf => cf.ByAutomationId(automationId)) != null; }
            catch { return false; }
        }

        private static bool HasArea(AutomationElement e)
        {
            try { var r = e.BoundingRectangle; return !r.IsEmpty && r.Width > 0 && r.Height > 0; }
            catch { return false; }
        }

        private static bool IsVisible(AutomationElement e)
        {
            try
            {
                if (e.Properties.IsOffscreen.IsSupported && e.Properties.IsOffscreen.Value) return false;
                return HasArea(e);
            }
            catch { return false; }
        }

        private static string SafeName(AutomationElement element)
        {
            try { return element?.Name ?? ""; } catch { return ""; }
        }

        // ── Diagnostics, Logging & Retry Helpers ──

        private string DescribeAppWindows()
        {
            try
            {
                var names = GetAppWindows().Select(w => $"'{SafeName(w)}'").Distinct().Take(10).ToList();
                return names.Count == 0 ? "(ไม่มี)" : string.Join(", ", names);
            }
            catch { return "(อ่านไม่ได้)"; }
        }

        private static string DescribeException(Exception ex)
        {
            string typeName = ex.GetType().FullName ?? ex.GetType().Name;
            string msg = string.IsNullOrWhiteSpace(ex.Message) ? "(ไม่มีข้อความ)" : ex.Message;
            string? firstStack = ex.StackTrace?.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
            return $"[{typeName}] {msg} | At: {firstStack}";
        }

        private void LogSuccess(string testName, string msg)
        {
            _totalSuccess++;
            TestLogger.LogSuccess(testName, msg);
        }

        private void LogFailure(string testName, string msg)
        {
            _totalFail++;
            TestLogger.LogFailure(testName, msg);
        }

        /* ตัวช่วยวนเช็คเงื่อนไขจนเป็นจริงหรือหมดเวลา */
        private static bool WaitUntil(Func<bool> condition, int timeoutMs = TimeoutMs, int pollMs = 100)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try { if (condition()) return true; } catch { }
                Thread.Sleep(pollMs);
            }
            return false;
        }

        /* รอจนกว่า element จะ enable */
        private static bool WaitUntilEnabled(AutomationElement element, int timeoutMs = 3000) =>
            WaitUntil(() => element.Properties.IsEnabled.IsSupported && element.Properties.IsEnabled.Value, timeoutMs, 200);

        /* ตัวช่วย Retry ค้นหา Element ซ้ำๆ จนกว่าจะเจอหรือหมดเวลา */
        private static T? Retry<T>(Func<T?> finder, int timeoutMs = TimeoutMs, int pollMs = PollMs) where T : class
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try
                {
                    var res = finder();
                    if (res != null) return res;
                }
                catch { }
                Thread.Sleep(pollMs);
            }
            return null;
        }
    }
}