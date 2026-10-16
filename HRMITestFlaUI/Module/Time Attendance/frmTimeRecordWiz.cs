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
    /// ทดสอบหน้าจอ "ประมวลผลเวลาการทำงาน" (frmTimeRecordWiz) — Wizard กด Next ไปทีละหน้า
    /// ขั้นตอนอ้างอิง TestStep.xlsx (ชีท "ประมวลผลเวลาการทำงาน") แถว 6
    /// </summary>
    internal class FrmTimeRecordWiz
    {
        // ── ตัวแปรสำหรับเก็บออบเจ็กต์หน้าต่างหลัก, ระบบ Automation และตัวนับผลการทดสอบ (Success/Fail) ──
        private readonly AutomationElement _mainWindow;
        private readonly UIA3Automation _automation;
        private int _totalSuccess = 0, _totalFail = 0;

        // ขั้นตอนที่ไม่สำเร็จในแถวปัจจุบัน (ทำขั้นตอนถัดไปต่อจนครบ แล้วค่อยสรุปว่าแถวนั้น FAIL)
        private readonly List<string> _stepErrors = new List<string>();

        private const string LogSource = "FrmTimeRecordWiz";

        // ── ค่าคงที่สำหรับ Timeout / Delay ──
        private const int TimeoutMs = 6000;
        private const int PollMs = 300;
        private const int MenuTransitionDelayMs = 400;
        private const int PageLoadDelayMs = 800;
        private const int StepDelayMs = 800;
        private const int PopupDelayMs = 600;
        private const int ToggleDelayMs = 250;
        private const int RetrieveMaxWaitMs = 30000;
        private const int LookupMaxWaitMs = 90000;             // lookup บางตัว (รหัสกะงาน) โหลดนาน
        private const int ProcessMaxWaitMs = 10 * 60 * 1000;   // รอประมวลผลได้นานสุด 10 นาที
        private const int MaxMenuScrollSteps = 10;

        // ── เมนูหลัก ──
        private const string TimeAttendanceMenuName = "Time Attendance";
        private const string MenuRowName = "treeListColumn row 7";
        private const string MenuLabel = "ประมวลผลเวลาการทำงาน";

        // ── Automation ID หน้า 1 (Wizard) ──
        private const string IdStartDate = "devDtStartDate";
        private const string IdEndDate = "devDtEndDate";
        private const string IdBtnNext = "devBtnNext";

        // ── Automation ID หน้าสุดท้าย (หน้าต่าง Manual — ชื่อเดียวกับ FixTimeManualWinUC) ──
        private const string IdOrgUnit = "rangeManagerOrgUnit";
        private const string IdPosition = "RangeMgPosition";
        private const string IdEmpType = "ChkComboEditEmpType";
        private const string IdDateOption = "comboDateOption";
        private const string IdTimeShiftType = "ChkComboEditTimeShiftType";
        private const string IdTimeShift = "rangeManagerTS";
        private const string IdStatus = "devRdgStatus";
        private const string IdBtnRetrieve = "devBtnRetrieve";

        private static readonly string[][] ThaiMonthNames =
        {
            new[] { "มกราคม", "ม.ค." }, new[] { "กุมภาพันธ์", "ก.พ." }, new[] { "มีนาคม", "มี.ค." },
            new[] { "เมษายน", "เม.ย." }, new[] { "พฤษภาคม", "พ.ค." }, new[] { "มิถุนายน", "มิ.ย." },
            new[] { "กรกฎาคม", "ก.ค." }, new[] { "สิงหาคม", "ส.ค." }, new[] { "กันยายน", "ก.ย." },
            new[] { "ตุลาคม", "ต.ค." }, new[] { "พฤศจิกายน", "พ.ย." }, new[] { "ธันวาคม", "ธ.ค." },
        };

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        /* Constructor สำหรับรับค่า MainWindow และ Automation Instance เข้ามาใช้งานภายในคลาส */
        public FrmTimeRecordWiz(AutomationElement mainWindow, UIA3Automation automation)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

        // -------------------------------------------------------------
        // 1. เปิดหน้าจอ ประมวลผลเวลาการทำงาน (Navigation)
        //    สัดส่วน Progress ของเมธอดนี้: 0 -> 5
        // -------------------------------------------------------------
        /* Method นำทางไปยังหน้าจอผ่านแท็บ Time Attendance -> เมนูแถวที่ 7 */
        public void NavigateToTimeRecordWiz()
        {
            TestProgressHelper.Report(2, "Opening Time Attendance -> Time Record Wizard...");
            if (!TryOpenMenu("NavigateToTimeRecordWiz")) return;
            TestProgressHelper.Report(5, "Time Record Wizard opened");
        }

        // -------------------------------------------------------------
        // 2. ทดสอบประมวลผลเวลาการทำงาน (Test Step Rows)
        //    สัดส่วน Progress ของเมธอดนี้: 5 -> 100
        // -------------------------------------------------------------
        /* Method รันทุกแถวของ Test Step แล้วสรุปผลรวม */
        public void RunAllValidationTests()
        {
            TestLogger.LogInfo(LogSource, "เริ่มทดสอบหน้าจอ ประมวลผลเวลาการทำงาน");

            var scenarios = new[]
            {
                new { Name = "TestCase_TimeRecordWiz_1", Navigate = true, Run = (Action)Step_Row6 }
            };

            foreach (var s in scenarios)
                RunScenario(s.Name, s.Navigate, s.Run);

            TestLogger.LogInfo(LogSource, $"สรุปผลรวมทั้งหมด: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");
        }

        /* แถว 6: กรอกวันที่ -> Next 3 ครั้ง -> รอประมวลผล กด Yes -> กรอกเงื่อนไขหน้า Manual -> ปิด -> Finish */
        private void Step_Row6()
        {
            Do("วันที่เริ่มต้น", () => SetDate(IdStartDate, "1/01/2569"), 10);
            Do("วันที่สิ้นสุด", () => SetDate(IdEndDate, "31/12/2569"), 15);

            // เปลี่ยนหน้า Wizard ไม่ได้ = ทำต่อไม่ได้ -> หยุดแถวนี้
            Step("กด Next ครั้งที่ 1", ClickNext, 20);
            Step("กด Next ครั้งที่ 2", ClickNext, 25);
            Step("กด Next ครั้งที่ 3", ClickNext, 30);
            Step("รอประมวลผล แล้วกด Yes", WaitProcessAndClickYes, 45);

            Do("รูปแบบวันที่", () => SelectDropdown(IdDateOption, "This Year"), 50);
            Do("รหัสหน่วยงาน", () => SelectRange(IdOrgUnit, "ORG001", "ORG005"), 57);
            Do("ตำแหน่งงาน", () => SelectRange(IdPosition, "DW001", "POS005"), 64);
            Do("ประเภทพนักงาน", () => CheckItems(IdEmpType, "พนักงานรายชั่วโมง", "พนักงานรายเหมา", "พนักงานรายวัน", "พนักงานรายเดือน"), 70);
            Do("ประเภทกะงาน", () => CheckItems(IdTimeShiftType, "กะงานปกติ", "กะงานล่วงเวลา"), 76);
            Do("รหัสกะงาน", () => SelectRange(IdTimeShift, "NRM-M02", "OT-EVN02"), 82);
            Do("สถานะข้อมูล", () => SelectStatus("ทุกสถานะ"), 87);
            Do("แสดงรายการข้อมูล", ClickRetrieve, 92);
            Do("กด กากบาท (ปิดหน้าต่าง Manual)", CloseManualWindow, 96);
            Do("กด Finish", ClickFinish, 100);
        }

        // -------------------------------------------------------------
        // Scenario Runner (Do / Step)
        // -------------------------------------------------------------

        /* Method รัน 1 แถว: เปิดหน้าจอถ้ายังไม่เปิด -> ทำทุกขั้นตอน -> ถ้ามีขั้นตอนพลาดถือว่าแถวนั้น FAIL */
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
            catch (Exception ex) { LogFailure(testName, $"ไม่สำเร็จ: {DescribeException(ex)}"); }
        }

        /* Method ทำขั้นตอนหนึ่ง ถ้าไม่สำเร็จให้จดไว้แล้วทำขั้นตอนถัดไปต่อ + รายงาน Progress */
        private void Do(string stepName, Action action, int progress)
        {
            try { action(); }
            catch (Exception ex) { _stepErrors.Add($"{stepName}: {ex.Message}"); }
            TestProgressHelper.Report(progress, stepName);
        }

        /* Method ขั้นตอนที่ถ้าไม่สำเร็จจะทำต่อไม่ได้ (เช่น เปลี่ยนหน้า Wizard) — หยุดแถวนี้ทันที */
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

        // -------------------------------------------------------------
        // UI Helpers & Core Navigation
        // -------------------------------------------------------------

        /* Method เปิดหน้าจอแบบไม่ throw (Log FAIL แล้วคืน false) */
        private bool TryOpenMenu(string stage)
        {
            try { OpenMenu(); return true; }
            catch (Exception ex) { LogFailure(stage, $"ไม่สำเร็จ: {DescribeException(ex)}"); return false; }
        }

        /* Method คลิกแท็บ Time Attendance -> เมนูแถว 7 แล้วยืนยันว่าหน้าจอเปิดจริง */
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

        /* Method กด Line Down เลื่อนรายการเมนูลงจนเจอเมนูที่ต้องการ */
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

        /* Method หาแถวเมนู 'treeListColumn row 7' (เช็คข้อความกันคลิกผิดแถว) ถ้าไม่เจอหาจากข้อความเมนู */
        private AutomationElement? FindMenuRow()
        {
            var target = Normalize(MenuLabel);

            var byRow = _mainWindow.FindFirstDescendant(cf => cf.ByName(MenuRowName));
            if (byRow != null && HasArea(byRow))
            {
                string value = "";
                try { value = byRow.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault ?? ""; } catch { }
                if (value.Length == 0 || Normalize(value).Contains(target)) return byRow;
            }

            var tree = _mainWindow.FindAllDescendants().Where(e => HasArea(e) && !IsFormTitle(e)).ToList();
            return tree.FirstOrDefault(e => Normalize(ReadText(e)) == target)
                ?? tree.FirstOrDefault(e => Normalize(ReadText(e)).Contains(target));
        }

        // -------------------------------------------------------------
        // Window Helpers (Wizard / Manual / Popup)
        // -------------------------------------------------------------

        /* Method หน้าต่างทั้งหมดของโปรแกรม (ยกเว้นหน้าจอหลัก) ทั้งแบบลอยและแบบอยู่ในหน้าจอหลัก */
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

        /* หน้าต่าง Wizard (มีปุ่ม Next) */
        private AutomationElement? FindWizardRoot() => GetAppWindows().FirstOrDefault(w => HasId(w, IdBtnNext));

        /* หน้าต่าง Manual ที่เปิดหลังกด Yes (มีปุ่ม แสดงรายการข้อมูล) */
        private AutomationElement? FindManualRoot() => GetAppWindows().FirstOrDefault(w => HasId(w, IdBtnRetrieve));

        /* Method ฟอร์มที่กำลังทำงาน: หลังกด Yes เป็นหน้าต่าง Manual ก่อนหน้านั้นเป็น Wizard */
        private AutomationElement? FindFormRoot()
        {
            var windows = GetAppWindows();
            return windows.FirstOrDefault(w => HasId(w, IdBtnRetrieve))
                ?? windows.FirstOrDefault(w => HasId(w, IdBtnNext))
                ?? windows.FirstOrDefault(w => Normalize(SafeName(w)).Contains(Normalize(MenuLabel)));
        }

        /* หน้าต่างหลักของหน้าจอ (Wizard / Manual) — ไม่นับเป็น popup */
        private static bool IsMainForm(AutomationElement w) => HasId(w, IdBtnNext) || HasId(w, IdBtnRetrieve);

        /* Method หน้าต่าง popup/dropdown/lookup ของโปรแกรม (ไม่รวม Wizard/Manual กันเข้าใจผิดว่า dropdown เปิดอยู่) */
        private IEnumerable<AutomationElement> GetPopupWindows()
        {
            var form = FindFormRoot();
            var result = GetAppWindows();
            if (form != null)
            {
                try { result.AddRange(form.FindAllChildren(cf => cf.ByControlType(ControlType.Window))); } catch { }
            }
            return result.Where(w => !IsMainForm(w)).ToList();
        }

        // -------------------------------------------------------------
        // Wizard Helpers (Date / Next / Yes / Close / Finish)
        // -------------------------------------------------------------

        /* Method กรอกวันที่ลง DateEdit (Ctrl+A -> พิมพ์ทับ -> Tab) แล้วเทียบ วัน/เดือน/ปี เป็นตัวเลข */
        private void SetDate(string automationId, string dateText)
        {
            var control = FindById(automationId);
            WaitUntilEnabled(control, 3000);
            var edit = control.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)) ?? control;

            ClickCenter(edit);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            Thread.Sleep(100);
            Keyboard.Type(dateText);
            Thread.Sleep(200);
            LeaveField();

            var actual = GetControlValue(FindById(automationId));
            if (!SameDate(actual, dateText))
                throw new Exception($"กรอกวันที่ '{dateText}' ใน '{automationId}' ไม่ติด (ค่าในช่องตอนนี้: '{actual}')");
        }

        /* Method เทียบวันที่ (รองรับ 1/01/2569, 01/01/2569, 01 มกราคม 2569) — แปลงไม่ได้ถือว่าผ่าน */
        private static bool SameDate(string actual, string expected)
        {
            var a = ParseDate(actual);
            var e = ParseDate(expected);
            return a == null || e == null || a.Value == e.Value;
        }

        /* Method แปลงข้อความวันที่เป็น (วัน, เดือน, ปี) — แปลงชื่อเดือนไทยเป็นเลขเดือนก่อน */
        private static (int Day, int Month, int Year)? ParseDate(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            for (int i = 0; i < ThaiMonthNames.Length; i++)
            {
                var name = ThaiMonthNames[i].FirstOrDefault(text.Contains);
                if (name != null)
                {
                    text = text.Replace(name, $"/{i + 1}/");
                    break;
                }
            }

            var nums = System.Text.RegularExpressions.Regex.Matches(text, @"\d+")
                .Cast<System.Text.RegularExpressions.Match>().Select(m => int.Parse(m.Value)).ToList();
            if (nums.Count < 3) return null;
            return (nums[0], nums[1], nums[2]);
        }

        /* Method กดปุ่ม Next (devBtnNext) แล้วรอหน้าถัดไปโหลด */
        private void ClickNext()
        {
            CloseLookupPopupIfOpen();
            var btn = FindById(IdBtnNext);
            if (!WaitUntilEnabled(btn, RetrieveMaxWaitMs))
                throw new Exception("ปุ่ม Next กดไม่ได้ (ไม่ enable)");
            SafeClickElement(btn);
            Thread.Sleep(PageLoadDelayMs);
        }

        /* Method รอจนประมวลผลเสร็จและมีกล่องถาม Yes ขึ้น แล้วกด Yes */
        private void WaitProcessAndClickYes()
        {
            var yes = Retry(FindYesButton, ProcessMaxWaitMs, 1000)
                ?? throw new Exception($"รอประมวลผลเกิน {ProcessMaxWaitMs / 60000} นาที แล้วยังไม่มีปุ่ม Yes ขึ้น");
            SafeClickElement(yes);
            Thread.Sleep(PageLoadDelayMs);
        }

        /* Method หาปุ่ม Yes/ใช่ ใน popup ก่อน แล้วค่อยหาในหน้าต่าง active */
        private AutomationElement? FindYesButton()
        {
            bool IsYes(AutomationElement b) =>
                (TextMatches(b, "Yes") || TextMatches(b, "&Yes") || TextMatches(b, "ใช่")) && IsVisible(b);

            foreach (var window in GetPopupWindows())
            {
                try
                {
                    var b = window.FindAllDescendants(cf => cf.ByControlType(ControlType.Button)).FirstOrDefault(IsYes);
                    if (b != null) return b;
                }
                catch { }
            }
            try { return SafeFindAllButtons(GetCurrentRoot()).FirstOrDefault(IsYes); }
            catch { return null; }
        }

        /* Method กด กากบาท ปิดหน้าต่าง Manual (หาเฉพาะในแถบชื่อของหน้าต่างนี้ กันปิดโปรแกรมหลัก) แล้วรอจนปิด */
        private void CloseManualWindow()
        {
            CloseLookupPopupIfOpen();
            var manual = FindManualRoot() ?? throw new Exception("หาหน้าต่าง Manual ไม่เจอ (อาจถูกปิดไปแล้ว)");

            AutomationElement? closeBtn = null;
            try
            {
                var titleBar = manual.FindFirstDescendant(cf => cf.ByControlType(ControlType.TitleBar));
                closeBtn = (titleBar ?? manual).FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                    .FirstOrDefault(b => (TextMatches(b, "Close") || TextMatches(b, "ปิด")) && IsVisible(b));
            }
            catch { }

            if (closeBtn != null) SafeClickElement(closeBtn);
            else (manual.Patterns.Window.PatternOrDefault ?? throw new Exception("หาปุ่มกากบาทของหน้าต่าง Manual ไม่เจอ")).Close();

            if (!WaitUntil(() => FindManualRoot() == null, 8000, 300))
                throw new Exception("กดกากบาทแล้ว แต่หน้าต่าง Manual ยังไม่ปิด (อาจมีกล่องข้อความถามอยู่)");
            Thread.Sleep(PageLoadDelayMs);
        }

        /* Method กด Finish — หาในหน้าต่าง Manual ก่อน แล้ว Wizard และหน้าต่างอื่นของโปรแกรม */
        private void ClickFinish()
        {
            CloseLookupPopupIfOpen();
            bool IsFinish(AutomationElement b) => (TextMatches(b, "Finish") || TextMatches(b, "&Finish")) && IsVisible(b);

            var btn = Retry(() =>
            {
                foreach (var root in new[] { FindManualRoot(), FindWizardRoot() }.Concat(GetAppWindows()))
                {
                    if (root == null) continue;
                    try
                    {
                        var b = root.FindAllDescendants(cf => cf.ByControlType(ControlType.Button)).FirstOrDefault(IsFinish);
                        if (b != null) return b;
                    }
                    catch { }
                }
                return null;
            }, 8000) ?? throw new Exception($"หาปุ่ม 'Finish' ไม่เจอ (ปุ่มที่เห็นตอนนี้: {DescribeVisibleButtons()})");

            WaitUntilEnabled(btn, 3000);
            SafeClickElement(btn);
            Thread.Sleep(PageLoadDelayMs);
        }

        // -------------------------------------------------------------
        // Field Helpers (Status / Dropdown / CheckItems / Range / Retrieve)
        // -------------------------------------------------------------

        /* Method หา control ตาม AutomationId — เลือกตัวที่มองเห็น (Wizard มี control ซ้ำในหน้าที่ซ่อนไว้) */
        private AutomationElement FindById(string automationId)
        {
            return Retry(() =>
            {
                var form = FindFormRoot();
                if (form != null)
                {
                    try
                    {
                        var visible = form.FindAllDescendants(cf => cf.ByAutomationId(automationId)).FirstOrDefault(IsVisible);
                        if (visible != null) return visible;
                    }
                    catch { }
                }
                return FindInForm(cf => cf.ByAutomationId(automationId));
            }, 8000) ?? throw new Exception($"หา control '{automationId}' ไม่เจอ");
        }

        /* Method หาในฟอร์มก่อน ถ้าไม่เจอฟอร์มค่อยหาทั้งหน้าจอ */
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

        /* Method เลือกสถานะข้อมูล (radio ใน devRdgStatus) */
        private void SelectStatus(string itemText)
        {
            var group = FindById(IdStatus);
            var radio = Retry(() => group.FindAllDescendants().FirstOrDefault(e => TextMatches(e, itemText) && HasArea(e)), 5000)
                ?? throw new Exception($"หาตัวเลือกสถานะ '{itemText}' ใน '{IdStatus}' ไม่เจอ");

            ClickCenter(radio);
            Thread.Sleep(ToggleDelayMs);
        }

        /* Method คลิกลูกศร ▼ ที่ขอบขวาของช่อง (คลิกครั้งเดียว popup เปิด) */
        private void OpenDropdown(string automationId)
        {
            var control = FindById(automationId);
            WaitUntilEnabled(control, 3000);
            var rect = control.BoundingRectangle;
            ClickAt(new Point(rect.Right - 9, rect.Top + rect.Height / 2));
            Thread.Sleep(PopupDelayMs);
        }

        /* Method เลือกค่าใน Dropdown: คลิกจาก popup ก่อน ถ้าไม่เห็นรายการให้ใช้ลูกศรขึ้น/ลงจนค่าตรง */
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

        /* Method ติ๊กหลายรายการใน ChkComboEdit — คลิกแต่ละตัวครั้งเดียว (คลิกซ้ำ = เอาติ๊กออก) แล้วยืนยัน + Tab */
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

        /* Method ปิด popup แบบบันทึกค่า: กด OK ถ้ามี ไม่งั้นกด Enter */
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

        /* Method เช็คว่าช่องมีรายการนี้ติ๊กไว้แล้วหรือยัง (อ่านจากหลายแหล่งเพราะบางแหล่งค้างค่าเก่า) */
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

        /* Method popup ของช่องยังเปิดอยู่หรือไม่ (ExpandCollapse ถ้ามี ไม่งั้นดูว่ายังเห็น checkbox ใน popup) */
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

        /* Method หา checkbox ใน popup ที่มองเห็นอยู่จริง */
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

        /* Method กด ... เลือก <from> ถึง กด ... เลือก <to> */
        private void SelectRange(string automationId, string fromCode, string toCode)
        {
            PickLookup(automationId, 0, fromCode);
            PickLookup(automationId, 1, toCode);
        }

        /* Method กดปุ่ม ... ลำดับที่ระบุ (เรียงซ้ายไปขวา: 0 = จาก, 1 = ถึง) แล้วดับเบิลคลิกรหัสใน lookup */
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

            var cell = Retry(() => FindInPopup(code), LookupMaxWaitMs, 500);
            if (cell == null)
            {
                var popupInfo = DescribePopupState();
                CancelLookup();
                var buttonInfo = string.Join(", ", buttons.Select((b, i) => $"[{i}] '{SafeName(b)}' id='{SafeAutomationId(b)}'"));
                throw new Exception($"หารหัส '{code}' ในหน้าต่างเลือกข้อมูลของ '{automationId}' ไม่เจอ | ปุ่มใน range: {buttonInfo} | {popupInfo}");
            }

            DoubleClickCenter(cell);
        }

        /* Method กดปุ่ม แสดงรายการข้อมูล แล้วรอจนปุ่มกลับมาใช้งานได้ */
        private void ClickRetrieve()
        {
            CloseLookupPopupIfOpen();
            var btn = FindById(IdBtnRetrieve);
            WaitUntilEnabled(btn, RetrieveMaxWaitMs);
            SafeClickElement(btn);
            Thread.Sleep(PageLoadDelayMs);
            WaitUntilEnabled(btn, RetrieveMaxWaitMs);
        }

        // -------------------------------------------------------------
        // Popup Helpers
        // -------------------------------------------------------------

        /* Method ถ้าหน้าต่าง lookup (มี Select + Cancel) ยังค้าง ให้กด Cancel ปิดก่อนทำขั้นตอนถัดไป */
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

        /* Method หารหัสไม่เจอ ให้กด Cancel (หรือ Esc) ปิดหน้าต่างเลือกข้อมูล */
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

        /* Method หาข้อความใน popup ที่แยกออกมาก่อน แล้วค่อยหาใน tree หน้าจอหลัก (ข้ามช่อง Edit/ComboBox) */
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

        /* Method สรุปสิ่งที่เห็นใน popup ตอนหาไม่เจอ (ใส่ใน log) */
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

        // -------------------------------------------------------------
        // Element Utilities
        // -------------------------------------------------------------

        private AutomationElement? GetActiveTopLevelWindow()
        {
            try
            {
                var hwnd = GetForegroundWindow();
                return hwnd == IntPtr.Zero ? null : _automation.FromHandle(hwnd);
            }
            catch { return null; }
        }

        /* Method จุดเริ่มค้นหา: หน้าต่าง active ถ้าเป็นของ process เดียวกัน ไม่งั้นใช้ _mainWindow */
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

        /* Method คลิกอย่างปลอดภัย: รอพิกัดใช้ได้ -> element.Click() -> Invoke pattern */
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

        /* Method กด Tab ออกจากช่อง ให้โปรแกรมอัปเดตค่า */
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

        // -------------------------------------------------------------
        // Diagnostics, Logging & Retry Helpers
        // -------------------------------------------------------------

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

        /* Method บันทึกผลเทสเมื่อสำเร็จ (เพิ่มจำนวน Success) */
        private void LogSuccess(string testName, string msg)
        {
            _totalSuccess++;
            TestLogger.LogSuccess(testName, msg);
        }

        /* Method บันทึกผลเทสเมื่อล้มเหลว (เพิ่มจำนวน Fail) */
        private void LogFailure(string testName, string msg)
        {
            _totalFail++;
            TestLogger.LogFailure(testName, msg);
        }

        /* Method ตัวช่วยวนเช็คเงื่อนไขจนเป็นจริงหรือหมดเวลา */
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

        /* Method รอจนกว่า element จะ enable */
        private static bool WaitUntilEnabled(AutomationElement element, int timeoutMs = 3000) =>
            WaitUntil(() => element.Properties.IsEnabled.IsSupported && element.Properties.IsEnabled.Value, timeoutMs, 200);

        /* Method ตัวช่วย Retry ค้นหา Element ซ้ำๆ จนกว่าจะเจอหรือหมดเวลา */
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
