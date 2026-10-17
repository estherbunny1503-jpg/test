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
    /// ทดสอบหน้าจอ "บันทึกแก้ไขปรับปรุงเวลา (Manual)" (FixTimeManualWinUC)
    /// ขั้นตอนอ้างอิง TestStep.xlsx (ชีท "บันทึกแก้ไขปรับปรุงเวลา Manual") แถว 6, 7, 8
    /// </summary>
    public class FixTimeManualWinUC
    {
        // =========================================================================================
        // หมวดที่ 1: ข้อมูลการทดสอบ (Test Data)
        // =========================================================================================

        // --- Data Records สำหรับใช้งานในแต่ละเฟส ---
        private sealed record FixTimeItem(
            string Name,
            bool Enabled,
            bool Navigate,
            string DateOption,
            string OrgFrom, string OrgTo,
            string PositionFrom, string PositionTo,
            string[] EmpTypes,
            string[] ShiftTypes,
            string ShiftFrom, string ShiftTo,
            string Status,
            string SaveButton);

        // [ขั้นที่ 1] ข้อมูลทดสอบบันทึกแก้ไข/ปรับปรุงเวลา — TestStep.ods มีแถวเดียว (แถว 6) = TestCase_FixTimeManual_3 ด้านล่าง; 1-2 ปิดไว้  Progress: 5 -> 100
        // Enabled = false จะถูกข้าม / Navigate = false คือทำต่อจาก Save and New ของแถวก่อนหน้า
        private static readonly FixTimeItem[] Step1_FixTimeCases =
        {
            // แถว 6
            new("TestCase_FixTimeManual_1", false, true,
                "Yesterday",
                "ORG001", "ORG005",
                "DW001", "POS005",
                new[] { "พนักงานรายชั่วโมง", "พนักงานรายเหมา" },
                new[] { "กะงานปกติ" },
                "NRM-M01", "NRM-OFF-NEW",
                "ปกติ", "Save and New"),

            // แถว 7
            new("TestCase_FixTimeManual_2", false, true,
                "This Year",
                "ORG001", "ORG005",
                "DW001", "POS005",
                new[] { "พนักงานรายวัน", "พนักงานรายเดือน" },
                new[] { "กะงานล่วงเวลา" },
                "OT-EVN01", "OT-EVN02",
                "ขาดงาน", "Save and New"),

            // แถว 8
            new("TestCase_FixTimeManual_3", true, false,
                "This Year",
                "ORG001", "ORG005",
                "DW001", "POS005",
                new[] { "พนักงานรายชั่วโมง", "พนักงานรายเหมา", "พนักงานรายวัน", "พนักงานรายเดือน" },
                new[] { "กะงานปกติ", "กะงานล่วงเวลา" },
                "NRM-M01", "OT-EVN02", // ตาม TestStep.ods (เดิม NRM-M02)
                "ทุกสถานะ", "Save and Close")
        };


        // =========================================================================================
        // หมวดที่ 2: ส่วนที่ขึ้นกับหน้าจอนั้นๆ (Screen-Specific Implementation)
        // **เมื่อเปลี่ยนไปทำหน้าจออื่น ต้องมาแก้ไขค่าและเมทอดในส่วนนี้ทั้งหมด**
        // =========================================================================================

        // 2.1 Locator & Mappings ประจำหน้าจอ
        private const string LogSource = "FixTimeManualWinUC";

        // ── เมนูหลัก ──
        private const string TimeAttendanceMenuName = "Time Attendance";
        private const string FixTimeMenuRowName = "treeListColumn row 8";
        private const string FixTimeMenuLabel = "บันทึกแก้ไข/ปรับปรุงเวลา (Manual)";

        // ── Automation ID ──
        private const string IdDateOption = "comboDateOption";
        private const string IdOrgUnit = "rangeManagerOrgUnit";
        private const string IdPosition = "RangeMgPosition";
        private const string IdEmpType = "ChkComboEditEmpType";
        private const string IdTimeShiftType = "ChkComboEditTimeShiftType";
        private const string IdTimeShift = "rangeManagerTS";
        private const string IdStatus = "devRdgStatus";
        private const string IdBtnRetrieve = "devBtnRetrieve";

        // 2.2 ค่า Timeout / Delay ประจำหน้าจอ
        private const int MenuTransitionDelayMs = 150;
        private const int PageLoadDelayMs = 300;
        private const int StepDelayMs = 300;
        private const int PopupDelayMs = 300;
        private const int ToggleDelayMs = 250;
        private const int RetrieveMaxWaitMs = 30000;
        private const int LookupMaxWaitMs = 90000;   // lookup บางตัว (รหัสกะงาน) โหลดนาน
        private const int MaxMenuScrollSteps = 10;

        /// <summary>
        /// ขั้นที่ 1: เปิดหน้าจอ บันทึกแก้ไข/ปรับปรุงเวลา (Manual) (Progress 0 -> 5)
        /// </summary>
        public void NavigateToFixTimeManualWinUC()
        {
            TestProgressHelper.Report(2, "Opening Time Attendance -> Fix Time Manual...");
            if (!TryOpenMenu("NavigateToFixTimeManualWinUC")) return;
            TestProgressHelper.Report(5, "Fix Time Manual screen opened");
        }

        /// <summary>
        /// ขั้นที่ 2: ทดสอบบันทึกแก้ไข/ปรับปรุงเวลา — รันแถวที่เปิดใช้ (Enabled) ตามลำดับ Test Step แล้วสรุปผลรวม (Progress 5 -> 100)
        /// แถว 7 และ 8 ทำต่อจาก Save and New ของแถวก่อนหน้า (Navigate = false)
        /// </summary>
        public void RunAllValidationTests()
        {
            TestLogger.LogInfo(LogSource, "เริ่มทดสอบหน้าจอ บันทึกแก้ไขปรับปรุงเวลา Manual");

            var active = Step1_FixTimeCases.Where(r => r.Enabled).ToList();
            for (int i = 0; i < active.Count; i++)
            {
                var r = active[i];
                int from = 5 + (i * 95 / active.Count), to = 5 + ((i + 1) * 95 / active.Count);
                int P(int step) => from + ((to - from) * step / 10);   // 10 ขั้นตอนต่อแถว

                RunScenario(r.Name, r.Navigate, () =>
                {
                    Do("รูปแบบวันที่", () => SelectDropdown(IdDateOption, r.DateOption), P(1));
                    Do("รหัสหน่วยงาน", () => SelectRange(IdOrgUnit, r.OrgFrom, r.OrgTo), P(2));
                    Do("ตำแหน่งงาน", () => SelectRange(IdPosition, r.PositionFrom, r.PositionTo), P(3));
                    Do("ประเภทพนักงาน", () => CheckItems(IdEmpType, r.EmpTypes), P(4));
                    Do("ประเภทกะงาน", () => CheckItems(IdTimeShiftType, r.ShiftTypes), P(5));
                    Do("รหัสกะงาน", () => SelectRange(IdTimeShift, r.ShiftFrom, r.ShiftTo), P(6));
                    Do("สถานะข้อมูล", () => SelectStatus(r.Status), P(7));
                    Do("แสดงรายการข้อมูล", ClickRetrieve, P(8));
                    Do($"กด {r.SaveButton}", () => ClickByName(r.SaveButton), P(10));
                });
            }

            TestLogger.LogInfo(LogSource, $"สรุปผลรวมทั้งหมด: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");
        }

        // --- Custom Mapping Logic ประจำหน้าจอนี้ ---

        /* รัน 1 แถว: เปิดหน้าจอถ้ายังไม่เปิด -> ทำทุกขั้นตอน -> ถ้ามีขั้นตอนพลาดถือว่าแถวนั้น FAIL */
        private void RunScenario(string testName, bool navigate, Action step)
        {
            _activeTestName = testName;
            try
            {
                if (navigate && FindFormRoot() == null) OpenMenu();

                _stepErrors.Clear();
                step();
                if (_stepErrors.Count > 0) throw new Exception(string.Join(" || ", _stepErrors));

                LogSuccess(testName, "ทำสำเร็จตามขั้นตอนครบถ้วน");
            }
            catch (Exception ex) { LogFailure(testName, $"ไม่สำเร็จ: {DescribeException(ex)}"); }
        }

        /* เปิดหน้าจอแบบไม่ throw (Log FAIL แล้วคืน false) */
        private bool TryOpenMenu(string stage)
        {
            try { OpenMenu(); return true; }
            catch (Exception ex) { LogFailure(stage, $"ไม่สำเร็จ: {DescribeException(ex)}"); return false; }
        }

        /* คลิกแท็บ Time Attendance -> เมนูแถว 8 แล้วยืนยันว่าหน้าจอเปิดจริง */
        private void OpenMenu()
        {
            // เปิดหน้าจอด้วยช่องค้นหาเมนู แบบเดียวกับโมดูลก่อนหน้า (ไม่คลิกแถวเมนูตามลำดับ)
            MenuSearch.Open(_mainWindow, FixTimeMenuLabel);
            Thread.Sleep(PageLoadDelayMs);

            if (Retry(FindFormRoot, 8000) == null)
                throw new Exception($"คลิกเมนูแล้ว แต่หน้าจอ '{FixTimeMenuLabel}' ไม่เปิดขึ้น");
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
                    var row = Retry(FindFixTimeMenuRow, 500);
                    if (row != null) return row;
                }
            }
            return Retry(FindFixTimeMenuRow, 3000);
        }

        /* หาแถวเมนูจากข้อความก่อน ถ้าไม่เจอใช้ 'treeListColumn row 8' (ต้องมีข้อความ (Manual) กันคลิกผิดแถว) */
        private AutomationElement? FindFixTimeMenuRow()
        {
            var byLabel = _mainWindow.FindAllDescendants()
                .FirstOrDefault(e => TextMatches(e, FixTimeMenuLabel) && HasArea(e) && !IsFormTitle(e));
            if (byLabel != null) return byLabel;

            var byRow = _mainWindow.FindFirstDescendant(cf => cf.ByName(FixTimeMenuRowName));
            if (byRow == null || !HasArea(byRow)) return null;

            string value = "";
            try { value = byRow.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault ?? ""; } catch { }
            return value.Contains("(Manual)") ? byRow : null;
        }

        /* ฟอร์ม Manual — ขอบเขตค้นหา กันไปกดปุ่มของหน้าจออื่นที่เปิดค้าง
           เลือกจากชื่อหน้าต่างก่อน ไม่เจอค่อยใช้หน้าต่างที่มีปุ่ม แสดงรายการข้อมูล */
        private AutomationElement? FindFormRoot()
        {
            var candidates = new List<AutomationElement>();
            try
            {
                int pid = _mainWindow.Properties.ProcessId.ValueOrDefault;
                candidates.AddRange(_automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(pid)).Where(w => !w.Equals(_mainWindow)));
            }
            catch { }
            try { candidates.AddRange(_mainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Window))); } catch { }

            return candidates.FirstOrDefault(w => IsManualTitle(SafeName(w)) && HasArea(w))
                ?? candidates.FirstOrDefault(w =>
                {
                    try { return HasArea(w) && w.FindFirstDescendant(cf => cf.ByAutomationId(IdBtnRetrieve)) != null; }
                    catch { return false; }
                });
        }

        private static bool IsManualTitle(string name)
        {
            var clean = (name ?? "").Replace(" ", "");
            return clean.Contains("ปรับปรุงเวลา") && clean.Contains("(Manual)");
        }

        /* หา control ตาม AutomationId ในฟอร์ม Manual */
        private AutomationElement FindById(string automationId) =>
            Retry(() => FindInForm(cf => cf.ByAutomationId(automationId)), 8000)
            ?? throw new Exception($"หา control '{automationId}' ไม่เจอ");

        /* หาในฟอร์ม Manual ก่อน ถ้าไม่เจอฟอร์ม (เช่นหลังกด Close) ค่อยหาทั้งหน้าจอ */
        private AutomationElement? FindInForm(Func<FlaUI.Core.Conditions.ConditionFactory, FlaUI.Core.Conditions.ConditionBase> condition)
        {
            var form = FindFormRoot();
            if (form != null)
            {
                try
                {
                    var found = form.FindFirstDescendant(condition);
                    if (found != null) return found;
                }
                catch { }
            }
            return FindDescendant(condition);
        }

        /* กดปุ่มตามชื่อ (Save and New / Save and Close ฯลฯ) — ชื่อต้องตรงทั้งคำ */
        private void ClickByName(string name)
        {
            CloseLookupPopupIfOpen();
            var element = Retry(() => FindInForm(cf => cf.ByName(name, PropertyConditionFlags.IgnoreCase)), 8000)
                ?? throw new Exception($"หาปุ่ม '{name}' ไม่เจอ (ปุ่มที่เห็นตอนนี้: {DescribeVisibleButtons()})");

            WaitUntilEnabled(element, 3000);
            SafeClickElement(element);
            Thread.Sleep(PageLoadDelayMs);
        }

        /* เลือกสถานะข้อมูล (radio ใน devRdgStatus) */
        private void SelectStatus(string itemText)
        {
            var group = FindById(IdStatus);
            var radio = Retry(() => group.FindAllDescendants().FirstOrDefault(e => TextMatches(e, itemText) && HasArea(e)), 5000)
                ?? throw new Exception($"หาตัวเลือกสถานะ '{itemText}' ใน '{IdStatus}' ไม่เจอ");

            ClickCenter(radio);
            Thread.Sleep(ToggleDelayMs);
        }

        /* กด ... เลือก <from> ถึง กด ... เลือก <to> */
        private void SelectRange(string automationId, string fromCode, string toCode)
        {
            PickLookup(automationId, 0, fromCode);
            PickLookup(automationId, 1, toCode);
        }

        /* กดปุ่ม ... ลำดับที่ระบุ (เรียงซ้ายไปขวา: 0 = จาก, 1 = ถึง) แล้วดับเบิลคลิกรหัสใน lookup */
        private void PickLookup(string automationId, int buttonIndex, string code)
        {
            var range = FindById(automationId);

            var buttons = Retry(() =>
            {
                var list = range.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                    .Where(IsUsableButton).OrderBy(b => b.BoundingRectangle.Left).ToList();
                return list.Count > buttonIndex ? list : null;
            }, 5000)
                ?? throw new Exception($"หาปุ่ม '...' ลำดับที่ {buttonIndex + 1} ใน '{automationId}' ไม่เจอ");

            ClickCenter(buttons[buttonIndex]);
            Thread.Sleep(PopupDelayMs);

            // รอแค่จนตารางใน popup โหลดข้อมูลเสร็จ (lookup บางตัวโหลดนาน) แล้วค่อยหารหัส
            // เดิมรอหารหัสนานสุด 90 วินาที ถ้ารหัสไม่มีในข้อมูล (เช่น ORG001 ที่ถูกเปลี่ยนเป็น RP001) จะเสียเวลาฟรีทั้ง 90 วินาที
            Retry(() => FindFirstPopupDataCell(), LookupMaxWaitMs, 300);
            var cell = Retry(() => FindInPopup(code), 1500, 300);
            if (cell == null)
            {
                // รหัสตาม TestStep ไม่มีในข้อมูล → ใช้แถวแรก (ช่อง "จาก") / แถวสุดท้าย (ช่อง "ถึง") ให้ช่วงครอบคลุมข้อมูลทั้งหมด
                var edge = FindEdgePopupRow(last: buttonIndex == 1);
                if (edge != null)
                {
                    string used = ""; try { used = edge.Patterns.Value.Pattern.Value.Value ?? ""; } catch { }
                    TestLogger.LogInfo(LogSource, $"ไม่พบรหัส '{code}' ใน '{automationId}' — ใช้ '{used}' ({(buttonIndex == 1 ? "แถวสุดท้าย" : "แถวแรก")}) แทน");
                    cell = edge;
                }
            }
            if (cell == null)
            {
                var popupInfo = DescribePopupState();
                CancelLookup();
                var buttonInfo = string.Join(", ", buttons.Select((b, i) => $"[{i}] '{SafeName(b)}' id='{SafeAutomationId(b)}'"));
                throw new Exception($"หารหัส '{code}' ในหน้าต่างเลือกข้อมูลของ '{automationId}' ไม่เจอ | ปุ่มใน range: {buttonInfo} | {popupInfo}");
            }

            DoubleClickCenter(cell);
        }

        /* กดปุ่ม แสดงรายการข้อมูล แล้วรอจนปุ่มกลับมาใช้งานได้ */
        private void ClickRetrieve()
        {
            CloseLookupPopupIfOpen();
            var btn = FindById(IdBtnRetrieve);
            WaitUntilEnabled(btn, RetrieveMaxWaitMs);
            SafeClickElement(btn);
            Thread.Sleep(PageLoadDelayMs);
            WaitUntilEnabled(btn, RetrieveMaxWaitMs);
        }


        // =========================================================================================
        // หมวดที่ 3: ส่วนการทำงานกลางที่ตายตัว (Common / Reusable Helpers)
        // **ใช้งานร่วมกันได้ทุกหน้าจอ ไม่ต้องแก้ไขคำสั่งเมื่อเปลี่ยนหน้า**
        // =========================================================================================

        private readonly AutomationElement _mainWindow;
        private readonly UIA3Automation _automation;
        private int _totalSuccess = 0, _totalFail = 0;
        private string _activeTestName = "TestCase_FixTimeManual";
        private const int TimeoutMs = 6000;
        private const int PollMs = 300;

        // ขั้นตอนที่ไม่สำเร็จในแถวปัจจุบัน (ทำขั้นตอนถัดไปต่อจนครบ แล้วค่อยสรุปว่าแถวนั้น FAIL)
        private readonly List<string> _stepErrors = new List<string>();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        public FixTimeManualWinUC(AutomationElement mainWindow, UIA3Automation automation)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

        // ── Step Runner ──

        /* ทำขั้นตอนหนึ่ง ถ้าไม่สำเร็จให้จดไว้แล้วทำขั้นตอนถัดไปต่อ + รายงาน Progress */
        private void Do(string stepName, Action action, int progress)
        {
            try
            {
                action();
                TestLogger.LogSuccess($"{_activeTestName}_{stepName}", $"สำเร็จ: {stepName}");
            }
            catch (Exception ex)
            {
                _stepErrors.Add($"{stepName}: {ex.Message}");
                TestLogger.LogFailure($"{_activeTestName}_{stepName}", $"ไม่สำเร็จ: {ex.Message}");
            }
            TestProgressHelper.Report(progress, stepName);
        }

        // ── Dropdown / CheckItems Helpers ──

        /* คลิกลูกศร ▼ ที่ขอบขวาของช่อง (คลิกครั้งเดียว popup เปิด) */
        private void OpenDropdown(string automationId)
        {
            var control = FindById(automationId);
            WaitUntilEnabled(control, 3000);
            var rect = control.BoundingRectangle;
            ClickAt(new Point(rect.Right - 9, rect.Top + rect.Height / 2));
            Thread.Sleep(PopupDelayMs);
        }

        /* เลือกค่าใน Dropdown: คลิกจาก popup ก่อน ถ้าไม่เห็นรายการให้ใช้ลูกศรขึ้น/ลงจนค่าตรง */
        private void SelectDropdown(string automationId, string itemText)
        {
            OpenDropdown(automationId);

            var item = Retry(() => FindInPopup(itemText), 2000);
            if (item != null)
            {
                ClickCenter(item);
                Thread.Sleep(StepDelayMs);
                if (ValueEquals(FindById(automationId), itemText)) return;
            }

            PressKey(VirtualKeyShort.ESCAPE);
            Thread.Sleep(200);

            var control = FindById(automationId);
            var seen = new List<string>();
            if (ValueEquals(control, itemText)) return;

            foreach (var key in new[] { VirtualKeyShort.DOWN, VirtualKeyShort.UP })
            {
                for (int i = 0; i < 40; i++)
                {
                    PressKey(key);
                    Thread.Sleep(150);

                    var current = GetControlValue(control);
                    if (!seen.Contains(current)) seen.Add(current);
                    if (ValueEquals(control, itemText))
                    {
                        Thread.Sleep(StepDelayMs);
                        return;
                    }
                }
            }

            throw new Exception($"หาตัวเลือก '{itemText}' ใน '{automationId}' ไม่เจอ " +
                                $"(ค่าที่เลื่อนผ่าน: {string.Join(" | ", seen.Where(v => v.Length > 0))})");
        }

        /* ติ๊กหลายรายการใน ChkComboEdit — คลิกแต่ละตัวครั้งเดียว (คลิกซ้ำ = เอาติ๊กออก) แล้วยืนยัน + Tab */
        private void CheckItems(string automationId, params string[] items)
        {
            var toClick = items.Where(i => !EditorHasItem(automationId, i)).ToList();
            if (toClick.Count == 0) return;

            foreach (var itemText in toClick)
            {
                if (!IsPopupOpen(automationId)) OpenDropdown(automationId);   // คลิก ▼ ตอน popup เปิดอยู่ = ปิด popup

                var item = Retry(() => FindVisiblePopupItem(itemText), 3000)
                    ?? throw new Exception($"หาตัวเลือก '{itemText}' ใน '{automationId}' ไม่เจอ");
                ClickCenter(item);
                Thread.Sleep(400);
            }

            CommitCheckedPopup(automationId);
            LeaveField();
            // ไม่ตัดสิน PASS/FAIL จากค่าที่อ่านได้ เพราะ ChkComboEdit ส่งค่าให้ UIA แค่ตัวแรก
        }

        /* ปิด popup แบบบันทึกค่า: กด OK ถ้ามี ไม่งั้นกด Enter */
        private void CommitCheckedPopup(string automationId)
        {
            if (!IsPopupOpen(automationId))
            {
                Thread.Sleep(StepDelayMs);
                return;
            }

            AutomationElement? ok = null;
            foreach (var window in GetPopupWindows())
            {
                try
                {
                    ok = window.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                        .FirstOrDefault(b => (TextMatches(b, "OK") || TextMatches(b, "ตกลง")) && HasArea(b));
                }
                catch { }
                if (ok != null) break;
            }

            if (ok != null) ClickCenter(ok);
            else PressKey(VirtualKeyShort.RETURN);
            Thread.Sleep(StepDelayMs);
        }

        /* เช็คว่าช่องมีรายการนี้ติ๊กไว้แล้วหรือยัง (อ่านจากหลายแหล่งเพราะบางแหล่งค้างค่าเก่า) */
        private bool EditorHasItem(string automationId, string itemText) =>
            GetCheckedSources(FindById(automationId))
                .SelectMany(v => v.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(v => v.Trim())
                .Any(v => string.Equals(v, itemText, StringComparison.OrdinalIgnoreCase));

        private static List<string> GetCheckedSources(AutomationElement control)
        {
            var result = new List<string>();
            void Add(Func<string?> read)
            {
                try
                {
                    var v = read()?.Trim();
                    if (!string.IsNullOrEmpty(v)) result.Add(v);
                }
                catch { }
            }

            Add(() => control.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault);
            Add(() => control.Patterns.LegacyIAccessible.PatternOrDefault?.Value.ValueOrDefault);
            Add(() => control.Name);
            try
            {
                foreach (var edit in control.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit)))
                {
                    Add(() => edit.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault);
                    Add(() => edit.Patterns.LegacyIAccessible.PatternOrDefault?.Value.ValueOrDefault);
                    Add(() => edit.Name);
                }
            }
            catch { }
            return result.Distinct().ToList();
        }

        /* popup ของช่องยังเปิดอยู่หรือไม่ (ExpandCollapse ถ้ามี ไม่งั้นดูว่ายังเห็น checkbox ใน popup) */
        private bool IsPopupOpen(string automationId)
        {
            try
            {
                var ec = FindById(automationId).Patterns.ExpandCollapse.PatternOrDefault;
                if (ec != null) return ec.ExpandCollapseState.ValueOrDefault == ExpandCollapseState.Expanded;
            }
            catch { }

            try
            {
                return GetPopupWindows().Any(w =>
                {
                    try { return IsVisible(w) && w.FindAllDescendants(cf => cf.ByControlType(ControlType.CheckBox)).Any(HasArea); }
                    catch { return false; }
                });
            }
            catch { return false; }
        }

        /* หา checkbox ใน popup ที่มองเห็นอยู่จริง */
        private AutomationElement? FindVisiblePopupItem(string text)
        {
            foreach (var window in GetPopupWindows())
            {
                try
                {
                    var item = window.FindAllDescendants(cf => cf.ByControlType(ControlType.CheckBox))
                        .FirstOrDefault(e => TextMatches(e, text) && HasArea(e));
                    if (item != null) return IsVisible(item) ? item : null;
                }
                catch { }
            }
            return null;
        }

        // ── Popup Helpers ──

        /* หน้าต่าง popup/dropdown/lookup ของโปรแกรม (ไม่รวมฟอร์ม Manual เพื่อไม่ให้ไปเจอค่าในช่องกรอก) */
        private IEnumerable<AutomationElement> GetPopupWindows()
        {
            var form = FindFormRoot();
            var result = new List<AutomationElement>();
            try
            {
                int pid = _mainWindow.Properties.ProcessId.ValueOrDefault;
                result.AddRange(_automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(pid)).Where(w => !w.Equals(_mainWindow)));
            }
            catch { }
            try { result.AddRange(_mainWindow.FindAllChildren(cf => cf.ByControlType(ControlType.Window))); } catch { }
            if (form != null)
            {
                try { result.AddRange(form.FindAllChildren(cf => cf.ByControlType(ControlType.Window))); } catch { }
            }
            return result.Where(w => form == null || !w.Equals(form)).ToList();
        }

        /* ถ้าหน้าต่าง lookup (มี Select + Cancel) ยังค้าง ให้กด Cancel ปิดก่อนทำขั้นตอนถัดไป */
        private void CloseLookupPopupIfOpen()
        {
            try
            {
                var lookup = GetPopupWindows().FirstOrDefault(w =>
                {
                    try
                    {
                        return HasArea(w)
                            && w.FindFirstDescendant(cf => cf.ByName("Select")) != null
                            && w.FindFirstDescendant(cf => cf.ByName("Cancel").Or(cf.ByName("ยกเลิก"))) != null;
                    }
                    catch { return false; }
                });
                if (lookup == null) return;

                var cancel = lookup.FindFirstDescendant(cf => cf.ByName("Cancel").Or(cf.ByName("ยกเลิก")));
                if (cancel != null && HasArea(cancel))
                {
                    ClickCenter(cancel);
                    Thread.Sleep(StepDelayMs);
                }
            }
            catch { }
        }

        /* หารหัสไม่เจอ ให้กด Cancel (หรือ Esc) ปิดหน้าต่างเลือกข้อมูล */
        private void CancelLookup()
        {
            try
            {
                var cancel = GetPopupWindows()
                    .Select(w => { try { return w.FindFirstDescendant(cf => cf.ByName("Cancel").Or(cf.ByName("ยกเลิก"))); } catch { return null; } })
                    .FirstOrDefault(b => b != null && HasArea(b));

                if (cancel != null) ClickCenter(cancel);
                else PressKey(VirtualKeyShort.ESCAPE);
                Thread.Sleep(StepDelayMs);
            }
            catch { }
        }

        /* เซลล์ข้อมูลแรกในตาราง popup ที่มีค่าแล้ว (ใช้รอให้ตารางโหลดเสร็จ) */
        private AutomationElement? FindFirstPopupDataCell()
        {
            foreach (var window in GetPopupWindows())
            {
                try
                {
                    var c = window.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem))
                        .FirstOrDefault(e => { try { return e.Patterns.Value.IsSupported && !string.IsNullOrWhiteSpace(e.Patterns.Value.Pattern.Value.Value); } catch { return false; } });
                    if (c != null) return c;
                }
                catch { }
            }
            return null;
        }

        /* แถวแรก/แถวสุดท้ายของคอลัมน์แรกในตาราง popup (เซลล์ชื่อ "<หัวคอลัมน์> row N") */
        private AutomationElement? FindEdgePopupRow(bool last)
        {
            var rx = new System.Text.RegularExpressions.Regex(@"^(.*) row (\d+)$");
            foreach (var window in GetPopupWindows())
            {
                try
                {
                    var cells = window.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem))
                        .Select(c => { string n = ""; try { n = c.Name ?? ""; } catch { } return (c, m: rx.Match(n)); })
                        .Where(x => x.m.Success).ToList();
                    if (cells.Count == 0) continue;
                    string firstCol = cells[0].m.Groups[1].Value;
                    var col = cells.Where(x => x.m.Groups[1].Value == firstCol).OrderBy(x => int.Parse(x.m.Groups[2].Value)).ToList();
                    return (last ? col.Last() : col.First()).c;
                }
                catch { }
            }
            return null;
        }

        /* หาข้อความใน popup ที่แยกออกมาก่อน แล้วค่อยหาใน tree หน้าจอหลัก (ข้ามช่อง Edit/ComboBox) */
        private AutomationElement? FindInPopup(string text)
        {
            foreach (var window in GetPopupWindows())
            {
                try
                {
                    var match = window.FindAllDescendants().FirstOrDefault(e => TextMatches(e, text) && HasArea(e));
                    if (match != null) return match;
                }
                catch { }
            }

            try { return _mainWindow.FindAllDescendants().FirstOrDefault(e => !IsInputBox(e) && TextMatches(e, text) && HasArea(e)); }
            catch { return null; }
        }

        /* สรุปสิ่งที่เห็นใน popup ตอนหาไม่เจอ (ใส่ใน log) */
        private string DescribePopupState()
        {
            try
            {
                var windows = GetPopupWindows().ToList();
                var windowNames = windows.Select(w => $"'{SafeName(w)}'").DefaultIfEmpty("(ไม่มี)");
                var texts = windows
                    .SelectMany(w => { try { return w.FindAllDescendants(); } catch { return Array.Empty<AutomationElement>(); } })
                    .Select(e =>
                    {
                        string name = SafeName(e), value = "";
                        try { value = e.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault ?? ""; } catch { }
                        return string.IsNullOrWhiteSpace(value) ? name : $"{name}={value}";
                    })
                    .Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().Take(20);
                return $"popup ที่เจอ: {string.Join(", ", windowNames)} | ข้อความใน popup: {string.Join(" | ", texts)}";
            }
            catch { return "(อ่านข้อมูล popup ไม่ได้)"; }
        }

        // ── Window / Search Root Helpers ──

        private AutomationElement? GetActiveTopLevelWindow()
        {
            try
            {
                var hwnd = GetForegroundWindow();
                return hwnd == IntPtr.Zero ? null : _automation.FromHandle(hwnd);
            }
            catch { return null; }
        }

        /* จุดเริ่มค้นหา: หน้าต่าง active ถ้าเป็นของ process เดียวกัน ไม่งั้นใช้ _mainWindow */
        private AutomationElement GetCurrentRoot()
        {
            try
            {
                var active = GetActiveTopLevelWindow();
                if (active != null && active.Properties.ProcessId.ValueOrDefault == _mainWindow.Properties.ProcessId.ValueOrDefault)
                    return active;
            }
            catch { }
            return _mainWindow;
        }

        private AutomationElement? FindDescendant(Func<FlaUI.Core.Conditions.ConditionFactory, FlaUI.Core.Conditions.ConditionBase> condition)
        {
            AutomationElement? found = null;
            var root = GetCurrentRoot();
            try { found = root.FindFirstDescendant(condition); } catch { }
            if (found == null && !ReferenceEquals(root, _mainWindow))
            {
                try { found = _mainWindow.FindFirstDescendant(condition); } catch { }
            }
            return found;
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

        private static void DoubleClickCenter(AutomationElement e)
        {
            Mouse.MoveTo(GetCenter(e.BoundingRectangle));
            Thread.Sleep(50);
            Mouse.DoubleClick(MouseButton.Left);
            Thread.Sleep(StepDelayMs);
        }

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

        /* กด Tab ออกจากช่อง ให้โปรแกรมอัปเดตค่า */
        private static void LeaveField()
        {
            PressKey(VirtualKeyShort.TAB);
            Thread.Sleep(400);
        }

        private static string GetControlValue(AutomationElement control)
        {
            try
            {
                var vp = control.Patterns.Value.PatternOrDefault;
                if (vp != null) return (vp.Value.ValueOrDefault ?? "").Trim();

                var evp = control.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit))?.Patterns.Value.PatternOrDefault;
                if (evp != null) return (evp.Value.ValueOrDefault ?? "").Trim();

                return (control.Name ?? "").Trim();
            }
            catch { return ""; }
        }

        private static bool ValueEquals(AutomationElement control, string text) =>
            string.Equals(GetControlValue(control), text, StringComparison.OrdinalIgnoreCase);

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

        /* กันไปเจอหัวข้อหน้าต่างฟอร์ม (ชื่อเดียวกับเมนู) แทนแถวเมนู */
        private static bool IsFormTitle(AutomationElement e)
        {
            try
            {
                var type = e.Properties.ControlType.ValueOrDefault;
                return type == ControlType.Window || type == ControlType.TitleBar || type == ControlType.Pane;
            }
            catch { return false; }
        }

        private static bool IsInputBox(AutomationElement e)
        {
            try
            {
                var type = e.Properties.ControlType.ValueOrDefault;
                return type == ControlType.Edit || type == ControlType.ComboBox;
            }
            catch { return true; }
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

        private static bool IsUsableButton(AutomationElement element)
        {
            try
            {
                if (element.Properties.IsEnabled.IsSupported && !element.Properties.IsEnabled.Value) return false;
                return IsVisible(element);
            }
            catch { return false; }
        }

        private static List<AutomationElement> SafeFindAllButtons(AutomationElement root)
        {
            try { return root.FindAllDescendants(cf => cf.ByControlType(ControlType.Button)).ToList(); }
            catch { return new List<AutomationElement>(); }
        }

        private static string SafeName(AutomationElement element)
        {
            try { return element?.Name ?? ""; } catch { return ""; }
        }

        private static string SafeAutomationId(AutomationElement element)
        {
            try { return element?.Properties.AutomationId.ValueOrDefault ?? ""; } catch { return ""; }
        }

        // ── Diagnostics, Logging & Retry Helpers ──

        private string DescribeVisibleButtons()
        {
            try
            {
                return string.Join(" | ", SafeFindAllButtons(GetCurrentRoot()).Where(IsUsableButton)
                    .Select(SafeName).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().Take(15));
            }
            catch { return "(อ่านรายชื่อปุ่มไม่ได้)"; }
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
