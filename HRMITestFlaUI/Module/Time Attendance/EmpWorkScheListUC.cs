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
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace HRMITestFlaUI.Module.Time_Attendance
{
    public class EmpWorkScheListUC
    {
        // =========================================================================================
        // หมวดที่ 1: ข้อมูลการทดสอบทั้ง 5 ขั้น (Test Data)
        // =========================================================================================

        // --- Data Records สำหรับใช้งานในแต่ละเฟส ---
        private sealed record SetScheduleItem(
            string Name,
            string Start,
            string End,
            string WorkType,
            string Pattern,
            int Progress,
            string ProgressMsg);
        private sealed record CopyItem(
            string Name,
            string Start,
            string End,
            string SourceEmployeeCode,
            int Progress,
            string ProgressMsg);
        private sealed record HolidayItem(
            string Name,
            string Start,
            string End,
            string Topic,
            string[] DaysToKeepChecked,
            int Progress,
            string ProgressMsg);
        private sealed record ImportItem(
            string Name,
            string Start,
            string End,
            int Progress,
            string ProgressMsg);
        private sealed record CancelItem(
            string Name,
            string Start,
            string End,
            string WorkGroupFrom, string WorkGroupTo,
            string OrgFrom, string OrgTo,
            string EmpGroupFrom, string EmpGroupTo,
            string[] EmpTypeChecks,
            string PositionFrom, string PositionTo,
            string LevelFrom, string LevelTo,
            string EmpCodeFrom, string EmpCodeTo,
            bool CancelWorkSchedule,
            bool CancelWorkHoliday,
            int Progress,
            string ProgressMsg);

        // [ขั้นที่ 1] ข้อมูลทดสอบกำหนดตารางการทำงานแบบรวม (Set Schedule)  Progress: 5 -> 25
        private static readonly SetScheduleItem[] Step1_SetScheduleCases =
        {
            new("TestCase_SetSchedule_General", "1/1/2569", "31/12/2569",
                "กำหนดตารางการทำงานแบบทั่วไป", "WSG001",
                15, "Set Schedule (1/2): WSG001..."),

            new("TestCase_SetSchedule_Rotation", "1/2/2569", "31/3/2569",
                "กำหนดตารางการทำงานแบบวนกะงาน", "WSG002",
                25, "Set Schedule (2/2): WSG002...")
        };

        // [ขั้นที่ 2] ข้อมูลทดสอบคัดลอกตารางการทำงาน (Copy Schedule)  Progress: 25 -> 40
        private const string SampleEmployeeCodeForCopyShortcut = "EMP0001";
        private static readonly CopyItem[] Step2_CopyCases =
        {
            new("TestCase_CopyScheduleShortcut", "3/1/2569", "3/12/2569",
                SampleEmployeeCodeForCopyShortcut, 40, "Copy Schedule Shortcut...")
        };

        // [ขั้นที่ 3] ข้อมูลทดสอบกำหนดวันหยุด (Holiday)  Progress: 40 -> 55
        private static readonly string[] HolidayDaysToKeepChecked = { "เสาร์", "อาทิตย์" };
        private static readonly HolidayItem[] Step3_HolidayCases =
        {
            new("TestCase_SetHoliday", "1/6/2569", "31/12/2569",
                "หยุดเพื่อไปหาสิ่งใหม่ๆ", HolidayDaysToKeepChecked, 55, "Set Holiday...")
        };

        // [ขั้นที่ 4] ข้อมูลทดสอบ Import ตารางการทำงาน (Import)  Progress: 55 -> 75
        private const string ImportFileName = "Import Employee Work Schedule";
        private static readonly string[] ImportFileExtensions = { ".xls", ".xlsx", ".xlsm" };
        private static readonly ImportItem[] Step4_ImportCases =
        {
            new("TestCase_Import_1", "01/01/2569", "31/12/2569", 66, "Import Employee Work Schedule...")
        };
        // คัดลอกตารางงานซ้ำหลัง Import
        private static readonly CopyItem[] Step4_CopyAfterImportCases =
        {
            new("TestCase_CopyScheduleShortcut", "3/1/2569", "3/12/2569",
                SampleEmployeeCodeForCopyShortcut, 75, "Copy Schedule Shortcut (after Import)...")
        };

        // [ขั้นที่ 5] ข้อมูลทดสอบยกเลิกตารางการทำงานแบบรวม (Cancel Schedule)  Progress: 75 -> 100
        private static readonly CancelItem[] Step5_CancelCases =
        {
            // EG-004: ติ๊ก "พนักงานรายชั่วโมง" + "พนักงานรายเหมา"
            new("TestCase_CancelSchedule_1", "1/9/2569", "31/12/2569",
                "NRM-M01", "OT-EVN02",
                "001", "ORG005",
                "EG-001", "EG-004",
                new[] { IdChkEmpTypeHourly, IdChkEmpTypeContract },
                "DW001", "POS005",
                "EL-001", "EL-005",
                "EMP00001", "EMP00004",
                true, true,
                87, "Cancel Schedule (1/2)..."),

            // EG-004: ติ๊ก "พนักงานรายวัน" + "พนักงานรายเดือน"
            new("TestCase_CancelSchedule_2", "3/8/2569", "3/9/2569",
                "NRM-M01", "OT-EVN02",
                "001", "ORG005",
                "EG-001", "EG-004",
                new[] { IdChkEmpTypeDaily, IdChkEmpTypeMonthly },
                "DW001", "POS005",
                "EL-001", "EL-005",
                "EMP00001", "EMP00004",
                false, true,
                100, "Cancel Schedule (2/2)...")
        };


        // =========================================================================================
        // หมวดที่ 2: ส่วนที่ขึ้นกับหน้าจอนั้นๆ (Screen-Specific Implementation)
        // **เมื่อเปลี่ยนไปทำหน้าจออื่น ต้องมาแก้ไขค่าและเมทอดในส่วนนี้ทั้งหมด**
        // =========================================================================================

        // 2.1 Locator & Mappings ประจำหน้าจอ
        private const string LogSource = "EmpWorkScheListUC";

        // ── เมนูหลัก ──
        private const string TimeAttendanceMenuName = "Time Attendance";
        private const string EmpWorkScheListMenuRowName = "treeListColumn row 2";
        private const string EmpWorkScheListMenuLabel = "บันทึกรูปแบบตารางการทำงาน";
        private const string MoreActionName = "More Action";
        private const string ConditionsTabName = "กำหนดเงื่อนไข";

        // ── ชื่อ Action ใน More Action ──
        private const string ActionSetScheduleForGroup = "กำหนดตารางการทำงานของพนักงานแบบรวม";
        private const string ActionImportSchedule = "Import ตารางการทำงานของพนักงาน";
        private const string ActionCancelScheduleForGroup = "ยกเลิกตารางการทำงานของพนักงานแบบรวม";

        // ── Automation ID ปุ่มของ Wizard ──
        private const string IdBtnNext = "devBtnNext";
        private const string IdBtnCancel = "devBtnCancel";
        private const string IdBtnShowEmp = "devBtnShowEmp";
        private const string IdBtnBrowse = "devBtnBrowse";
        private const string IdBtnEmployee = "devBtnEmployee";
        private const string IdBtnWorkSchedulePattern = "devBtnTemplateSche";
        private const string NextButtonName = "Next >";
        private const string SaveCompletedLabel = "บันทึกข้อมูลเสร็จสิ้น";

        // ── Automation ID ช่องกรอก / Radio / Checkbox ──
        private const string IdDateStart = "devDateStart";
        private const string IdDateEnd = "devDateEnd";
        private const string IdRdgAddWorkType = "devRdgAddWorkType";
        private const string IdRdgSetWorkSchedule = "devRdgWorkSche";
        private const string IdRdgCopyWorkSche = "devRdgCopyWorkSche";
        private const string IdRdgHolidaySche = "devRdgHolidaySche";
        private const string IdTxtHolidayTopic = "devtxtHoliday";
        private const string IdChkReplaceHoliday = "devChkReplaceByHoliday";
        private const string IdGcHolidaySche = "devGcHolidaySche";
        private const string IdChkCancelWorkHoliday = "devChkCancelWorkHoliday";
        private const string IdChkCancelSchedule = "devChkCancelSchedule";

        // ── Range control หน้า "ยกเลิกตารางการทำงานของพนักงานแบบรวม" (1 control มีปุ่ม จาก/ถึง ภายใน) ──
        private const string IdRangeWorkGroupCode = "rangeManagerTS";
        private const string IdRangeOrgUnit = "RangeMgOrgUnit";
        private const string IdRangeEmpGroup = "RangeMgEmpGroup";
        private const string IdRangePosition = "RangeMgPosition";
        private const string IdRangeEmpLevel = "RangeMgEmpLevel";
        private const string IdRangeEmployee = "RangeMgEmployee";

        // ── Dropdown ประเภทพนักงาน ──
        private const string IdChkComboEmpType = "ChkComboEditEmpType";
        private const string IdChkEmpTypeHourly = "devChkEmpTypeHourly";
        private const string IdChkEmpTypeContract = "devChkEmpTypeContract";
        private const string IdChkEmpTypeDaily = "devChkEmpTypeDaily";
        private const string IdChkEmpTypeMonthly = "devChkEmpTypeMonthly";

        // 2.2 ค่า Timeout / Delay ประจำหน้าจอ
        private const int MenuTransitionDelayMs = 400;
        private const int PageLoadDelayMs = 300;
        private const int WizardStepDelayMs = 300;
        private const int WizardProcessingDelayMs = 1200;
        private const int WizardProcessingMaxWaitMs = 75000;   // ระบบอาจประมวลผลตารางงานนานถึง ~1 นาทีก่อนปุ่ม Next/Finish พร้อม
        private const int WizardProcessingPollMs = 250;
        private const int TypingDelayMs = 15;
        private const int ToggleDelayMs = 80;
        private const int ScrollStepDelayMs = 100;
        private const int LookupPopupDelayMs = 200;
        private const int FieldBindRetryTimeoutMs = 15000;
        private const int FieldBindRetryPollMs = 500;
        private const int MaxMenuScrollSteps = 10;
        private const int MaxNextSteps = 20;                   // บาง flow มีหน้าตรวจสอบเพิ่ม 8 ครั้งไม่พอ
        private const int MaxOpenWizardAttempts = 3;

        // ── ชื่อปุ่มสำหรับจับคู่ปุ่ม Next/Finish (Exact ก่อน แล้วค่อย Contains) และชื่อที่ห้ามกด ──
        private static readonly string[] NextButtonNames = { "Next", "ถัดไป" };
        private static readonly string[] NextButtonExactNames = { "Next >", "ถัดไป >" };
        private static readonly string[] FinishButtonExactNames = { "Finish", "เสร็จสิ้น" };
        private static readonly string[] ForbiddenNavNames = { "Cancel", "ยกเลิก" };

        // ปุ่มข้างเคียงของแถบเลื่อนหน้า DevExpress GridControl (ใช้แยกปุ่ม "Next Page" ของตารางออกจากปุ่ม Next ของ wizard)
        private static readonly string[] GridNavigatorSiblingNames =
            { "First", "Previous Page", "Previous", "Next Page", "Last", "Append", "Delete", "Edit", "Update", "Cancel Edit" };

        /// <summary>
        /// ขั้นที่ 1: เปิดหน้าจอ บันทึกรูปแบบตารางการทำงาน (Progress 0 -> 5)
        /// </summary>
        public void NavigateToEmpWorkScheListUC()
        {
            const string stageName = "TestCase00_OpenEmpWorkScheList";
            TestProgressHelper.Report(2, "Opening Time Attendance -> Employee Work Schedule Menu...");

            if (!TryOpenEmpWorkScheMenu(stageName)) return;

            TestProgressHelper.Report(5, "Employee Work Schedule screen opened");
        }

        /// <summary>
        /// รันชุดทดสอบทั้งหมดตามลำดับ Test Step และสรุปผลรวม
        /// </summary>
        public void RunAllValidationTests()
        {
            TestLogger.LogInfo(LogSource, "เริ่มทดสอบหน้าจอ บันทึกรูปแบบตารางการทำงาน");

            RunSetScheduleTests();
            RunCopyScheduleTests();
            RunHolidayTests();
            RunImportTests();
            RunCancelScheduleTests();

            TestLogger.LogInfo(LogSource, $"สรุปผลรวมทั้งหมด: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");
        }

        /// <summary>
        /// ขั้นที่ 2: ทดสอบกำหนดตารางการทำงานแบบรวม ทั้งแบบทั่วไปและแบบวนกะงาน (Progress 5 -> 25)
        /// </summary>
        public void RunSetScheduleTests()
        {
            TestProgressHelper.Report(7, "Starting Set Schedule Tests...");

            foreach (var s in Step1_SetScheduleCases)
                RunScenario(s.Name, () => SetScheduleForGroup(s.Start, s.End, s.WorkType, s.Pattern), s.Progress, s.ProgressMsg);
        }

        /// <summary>
        /// ขั้นที่ 3: ทดสอบคัดลอกตารางการทำงานจากพนักงานต้นแบบ (Progress 25 -> 40)
        /// </summary>
        public void RunCopyScheduleTests()
        {
            TestProgressHelper.Report(27, "Starting Copy Schedule Tests...");

            foreach (var c in Step2_CopyCases)
                RunScenario(c.Name, () => CopyScheduleShortcut(c.Start, c.End, c.SourceEmployeeCode), c.Progress, c.ProgressMsg);
        }

        /// <summary>
        /// ขั้นที่ 4: ทดสอบกำหนดวันหยุดบริษัทแทนที่กะงาน (Progress 40 -> 55)
        /// </summary>
        public void RunHolidayTests()
        {
            TestProgressHelper.Report(42, "Starting Holiday Tests...");

            foreach (var h in Step3_HolidayCases)
                RunScenario(h.Name, () => SetHoliday(h.Start, h.End, h.Topic, h.DaysToKeepChecked), h.Progress, h.ProgressMsg);
        }

        /// <summary>
        /// ขั้นที่ 5: ทดสอบ Import ตารางการทำงานจากไฟล์ Excel แล้วคัดลอกตารางงานซ้ำ (Progress 55 -> 75)
        /// </summary>
        public void RunImportTests()
        {
            TestProgressHelper.Report(57, "Starting Import Tests...");

            foreach (var i in Step4_ImportCases)
                RunScenario(i.Name, () => ImportEmployeeWorkSchedule(i.Start, i.End), i.Progress, i.ProgressMsg);

            foreach (var c in Step4_CopyAfterImportCases)
                RunScenario(c.Name, () => CopyScheduleShortcut(c.Start, c.End, c.SourceEmployeeCode), c.Progress, c.ProgressMsg);
        }

        /// <summary>
        /// ขั้นที่ 6: ทดสอบยกเลิกตารางการทำงานแบบรวม (Progress 75 -> 100)
        /// </summary>
        public void RunCancelScheduleTests()
        {
            TestProgressHelper.Report(77, "Starting Cancel Schedule Tests...");

            foreach (var c in Step5_CancelCases)
            {
                RunScenario(c.Name,
                    () => CancelScheduleForGroup(
                        start: c.Start, end: c.End,
                        workGroupFrom: c.WorkGroupFrom, workGroupTo: c.WorkGroupTo,
                        orgFrom: c.OrgFrom, orgTo: c.OrgTo,
                        empGroupFrom: c.EmpGroupFrom, empGroupTo: c.EmpGroupTo,
                        empTypeChecks: c.EmpTypeChecks,
                        positionFrom: c.PositionFrom, positionTo: c.PositionTo,
                        levelFrom: c.LevelFrom, levelTo: c.LevelTo,
                        empCodeFrom: c.EmpCodeFrom, empCodeTo: c.EmpCodeTo,
                        cancelWorkSchedule: c.CancelWorkSchedule, cancelWorkHoliday: c.CancelWorkHoliday),
                    c.Progress, c.ProgressMsg);
            }
        }

        // --- Wizard Scenarios ประจำหน้าจอนี้ (ขั้นตอนของแต่ละ Test Case) ---

        /* กำหนดตารางการทำงานแบบรวม: กรอกช่วงวันที่ -> เลือกประเภท -> เลือกรูปแบบ -> แสดงพนักงาน -> Finish */
        private void SetScheduleForGroup(string start, string end, string workType, string patternCode)
        {
            OpenWizardAction(ActionSetScheduleForGroup, FindDateStartField);
            FillDateRange(start, end);

            SelectRadioButton(IdRdgSetWorkSchedule);
            SelectRadioGroupOption(IdRdgAddWorkType, workType);
            SelectLookupPattern(patternCode);

            ClickNextButton();
            ShowEmployeesAndFinish();
        }

        /* คัดลอกตารางการทำงานจากพนักงานต้นแบบไปยังพนักงานตามเงื่อนไข */
        private void CopyScheduleShortcut(string start, string end, string sourceEmployeeCode)
        {
            OpenWizardAction(ActionSetScheduleForGroup, FindDateStartField);
            FillDateRange(start, end);

            SelectRadioButton(IdRdgCopyWorkSche);
            SelectEmployeeFromLookup(sourceEmployeeCode);

            ClickNextButton();
            ShowEmployeesAndFinish();
        }

        /* กำหนดวันหยุดบริษัท: กรอกหัวข้อ -> ติ๊กแทนที่กะงาน -> เลือกวันหยุดในตาราง -> Finish */
        private void SetHoliday(string start, string end, string topic, string[] daysToKeepChecked)
        {
            OpenWizardAction(ActionSetScheduleForGroup, FindDateStartField);
            FillDateRange(start, end);

            SelectRadioButton(IdRdgHolidaySche);
            FillTextBoxRobust(IdTxtHolidayTopic, topic, labelHint: "หัวข้อ");
            ClickCheckbox(IdChkReplaceHoliday, "แทนที่กะงานด้วยวันหยุดบริษัท");
            SetHolidayDaysInGrid(IdGcHolidaySche, daysToKeepChecked);

            ClickNextButton();
            ShowEmployeesAndFinish();
        }

        /* Import ตารางการทำงาน: เลือกไฟล์ผ่าน Browse -> กรอกช่วงวันที่ -> Finish */
        private void ImportEmployeeWorkSchedule(string start, string end)
        {
            // หน้าจอ Import บางครั้งหา AutomationId 'devBtnBrowse' ไม่เจอ จึงยืนยันด้วยชื่อปุ่ม "Browse" ด้วย
            OpenWizardAction(ActionImportSchedule, FindBrowseButton);

            BrowseAndSelectFile();
            FillDateRange(start, end);

            ClickNextUntilFinish();
            ClickFinishButton();
        }

        /* ยกเลิกตารางการทำงานแบบรวม: กรอกช่วงวันที่ -> ติ๊กสิ่งที่ยกเลิก -> เลือกช่วงข้อมูลทุก Range -> Finish */
        private void CancelScheduleForGroup(
            string start, string end,
            string workGroupFrom, string workGroupTo,
            string orgFrom, string orgTo,
            string empGroupFrom, string empGroupTo,
            string[] empTypeChecks,
            string positionFrom, string positionTo,
            string levelFrom, string levelTo,
            string empCodeFrom, string empCodeTo,
            bool cancelWorkSchedule, bool cancelWorkHoliday)
        {
            OpenWizardAction(ActionCancelScheduleForGroup, FindDateStartField);
            FillDateRange(start, end);

            if (cancelWorkSchedule) ClickCheckbox(IdChkCancelSchedule, "ยกเลิกกะงาน");
            if (cancelWorkHoliday) ClickCheckbox(IdChkCancelWorkHoliday, "ยกเลิกวันหยุดกะ");

            // ไม่ติ๊ก "ยกเลิกกะงาน" -> ช่วงกลุ่มรูปแบบตารางงานถูกปิด ไม่ต้องเลือก
            if (cancelWorkSchedule)
                SelectRangeValue(IdRangeWorkGroupCode, workGroupFrom, workGroupTo);

            SelectRangeValue(IdRangeOrgUnit, orgFrom, orgTo);
            SelectRangeValue(IdRangeEmpGroup, empGroupFrom, empGroupTo);

            if (empTypeChecks != null && empTypeChecks.Length > 0)
                SelectEmployeeTypeDropdown(empTypeChecks);

            SelectRangeValue(IdRangePosition, positionFrom, positionTo);
            SelectRangeValue(IdRangeEmpLevel, levelFrom, levelTo);
            SelectRangeValue(IdRangeEmployee, empCodeFrom, empCodeTo);

            ClickNextUntilFinish();
            ClickFinishButton();
        }

        /* ขั้นตอนท้ายที่ใช้ร่วมกัน: เปิดแท็บเงื่อนไข -> กดแสดงข้อมูลพนักงาน -> กด Next จนถึง Finish */
        private void ShowEmployeesAndFinish()
        {
            EnsureConditionsTabActive();
            ClickShowEmployeeDataButton();
            ClickNextUntilFinish();
            ClickFinishButton();
        }

        // --- Navigation / More Action ประจำหน้าจอนี้ ---

        /* เปิดหน้าจอผ่านแท็บ Time Attendance และคลิกเมนู (เลื่อนรายการลงถ้ายังมองไม่เห็น) */
        private bool TryOpenEmpWorkScheMenu(string stage)
        {
            try
            {
                var tab = Retry(() =>
                        _mainWindow.FindFirstDescendant(cf => cf.ByName(TimeAttendanceMenuName).And(cf.ByControlType(ControlType.TabItem)))
                        ?? _mainWindow.FindFirstDescendant(cf => cf.ByName(TimeAttendanceMenuName)), 5000)
                    ?? throw new Exception($"หาเมนูหลัก '{TimeAttendanceMenuName}' ไม่เจอ");

                ClickCenter(tab);
                Thread.Sleep(MenuTransitionDelayMs);

                var menuRow = Retry(FindEmpWorkScheListMenuRow, 3000) ?? ScrollToMenuRow()
                    ?? throw new Exception($"หาเมนู '{EmpWorkScheListMenuLabel}' ({EmpWorkScheListMenuRowName}) ไม่เจอ");

                ClickCenter(menuRow);
                Thread.Sleep(PageLoadDelayMs);
                return true;
            }
            catch (Exception ex)
            {
                LogFailure(stage, $"เปิดหน้าจอไม่สำเร็จ: {ex.Message}");
                return false;
            }
        }

        private AutomationElement? FindEmpWorkScheListMenuRow() =>
            _mainWindow.FindFirstDescendant(cf => cf.ByName(EmpWorkScheListMenuRowName))
            ?? _mainWindow.FindFirstDescendant(cf => cf.ByName(EmpWorkScheListMenuLabel));

        /* กดปุ่ม Line Down เพื่อเลื่อนรายการเมนูลงจนเจอเมนูที่ต้องการ */
        private AutomationElement? ScrollToMenuRow()
        {
            var lineDown = _mainWindow.FindFirstDescendant(cf => cf.ByName("Line Down"));
            if (lineDown != null)
            {
                for (int step = 0; step < MaxMenuScrollSteps; step++)
                {
                    ClickCenter(lineDown);
                    var row = Retry(FindEmpWorkScheListMenuRow, ScrollStepDelayMs);
                    if (row != null) return row;
                }
            }
            return Retry(FindEmpWorkScheListMenuRow, 3000);
        }

        /* ตัวช่วยรัน Scenario: เคลียร์สถานะก่อน -> รัน -> Log ผล -> ปิด dialog ค้าง -> รายงาน Progress */
        private void RunScenario(string testName, Action step, int progress, string progressMessage)
        {
            // ต้องเริ่มจากหน้าหลักที่สะอาด ไม่งั้น dialog ค้างจากเคสก่อนจะทำให้เคสถัดไปพังต่อเป็นทอดๆ
            EnsureCleanStateBeforeScenario();
            try
            {
                step();
                LogSuccess(testName, "ทำสำเร็จตามขั้นตอนครบถ้วน");
            }
            catch (Exception ex)
            {
                LogFailure(testName, $"ไม่สำเร็จ: {DescribeException(ex)}");
            }
            finally
            {
                CloseAnyOpenWizardDialog();
                TestProgressHelper.Report(progress, progressMessage);
            }
        }

        /* เช็คว่ากลับสู่หน้าหลักแล้ว (มี More Action) ถ้ายังให้พยายามปิด dialog ค้างอีกรอบ */
        private void EnsureCleanStateBeforeScenario()
        {
            if (Retry(FindMoreActionMenu, 3000) != null) return;

            CloseAnyOpenWizardDialog();
            Retry(FindMoreActionMenu, 5000);
        }

        private AutomationElement? FindMoreActionMenu() =>
            _mainWindow.FindFirstDescendant(cf => cf.ByName(MoreActionName));

        /* เปิด wizard ผ่าน More Action และยืนยันว่าเปิดสำเร็จจริง (ลองใหม่ได้สูงสุด MaxOpenWizardAttempts ครั้ง) */
        private void OpenWizardAction(string actionName, Func<AutomationElement?> verifyOpened)
        {
            Exception? lastEx = null;

            for (int attempt = 1; attempt <= MaxOpenWizardAttempts; attempt++)
            {
                try
                {
                    ClickMoreAction();
                    ClickMoreActionItem(actionName);

                    if (Retry(verifyOpened, 6000) != null) return;
                    lastEx = new Exception($"เปิดหน้าจอ '{actionName}' แล้วแต่ไม่พบ element ที่คาดหวังภายในเวลาที่กำหนด");
                }
                catch (Exception ex) { lastEx = ex; }

                // มักเปิดไม่ติดเพราะมี dialog ก่อนหน้าค้างบังอยู่ -> ปิดก่อนลองใหม่
                CloseAnyOpenWizardDialog();
                Thread.Sleep(PageLoadDelayMs);
            }

            throw new Exception($"เปิดหน้าจอ '{actionName}' ไม่สำเร็จหลังจากลอง {MaxOpenWizardAttempts} ครั้ง " +
                                $"(ปุ่มที่เห็นตอนนี้: {DescribeVisibleButtons()}) | ล่าสุด: {lastEx?.Message}");
        }

        private void ClickMoreAction()
        {
            var menu = Retry(FindMoreActionMenu) ?? throw new Exception($"หาเมนู '{MoreActionName}' ไม่เจอ");
            ClickCenter(menu);
            Thread.Sleep(WizardStepDelayMs);
        }

        /* คลิกรายการย่อยใน More Action (อาจเป็น popup นอก main window จึงค้นจาก desktop ด้วย) */
        private void ClickMoreActionItem(string actionName)
        {
            var item = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName(actionName))
                                   ?? _automation.GetDesktop().FindFirstDescendant(cf => cf.ByName(actionName)))
                       ?? throw new Exception($"หาปุ่มเมนู '{actionName}' ใน More Action ไม่เจอ");
            ClickCenter(item);
            Thread.Sleep(WizardStepDelayMs);
        }

        /* ปิด wizard/dialog/popup ที่ค้างอยู่ทุกชั้นจนเหลือแค่หน้าหลัก */
        private void CloseAnyOpenWizardDialog()
        {
            // 1) หน้าจอ "บันทึกข้อมูลเสร็จสิ้น" มีแค่ปุ่ม Finish (ไม่มี Cancel) ต้องกด Finish ให้ปิดก่อน
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    var leftoverFinish = FindFinishButton();
                    if (leftoverFinish == null || !leftoverFinish.Properties.IsEnabled.ValueOrDefault) break;

                    SafeClickElement(leftoverFinish);
                    Thread.Sleep(PageLoadDelayMs);
                    if (FindFinishButton() == null) break;
                }
                catch { break; }
            }

            // 2) ไล่ปิดหน้าต่างอื่นของ process ทุกชั้น (กัน popup ย่อยค้างเป็น orphan window)
            for (int attempt = 0; attempt < 6; attempt++)
            {
                var extraWindows = GetAllTopLevelWindowsOfProcess()
                    .Where(w => !AutomationElementEquals(w, _mainWindow))
                    .ToList();
                if (extraWindows.Count == 0) break;

                bool closedAny = false;
                foreach (var win in extraWindows)
                {
                    try
                    {
                        closedAny |= TryCloseWindow(win);
                        Thread.Sleep(PageLoadDelayMs);
                        ClickConfirmDialogIfAny();
                    }
                    catch { }
                }

                if (!closedAny) break;
            }

            // 3) รอให้หน้าหลักพร้อม (มี More Action)
            try { Retry(FindMoreActionMenu, 5000); } catch { }
        }

        // --- Custom Mapping Logic ประจำหน้าจอนี้ ---

        private AutomationElement? FindShowEmployeeButton() =>
            FindDescendant(cf => cf.ByAutomationId(IdBtnShowEmp)) ?? FindDescendant(cf => cf.ByName("แสดงข้อมูลพนักงาน"));

        private AutomationElement? FindDateStartField() =>
            FindDescendant(cf => cf.ByAutomationId(IdDateStart));

        private AutomationElement? FindBrowseButton() =>
            FindDescendant(cf => cf.ByAutomationId(IdBtnBrowse)) ?? FindDescendant(cf => cf.ByName("Browse"));

        /* กรอกวันที่เริ่มต้นและสิ้นสุด */
        private void FillDateRange(string start, string end)
        {
            FillDateField(IdDateStart, start);
            FillDateField(IdDateEnd, end);
        }

        /* เปิดแท็บ "กำหนดเงื่อนไข" (ข้ามได้ถ้าปุ่มแสดงข้อมูลพนักงานพร้อมใช้อยู่แล้ว) */
        private void EnsureConditionsTabActive()
        {
            var employeeButton = Retry(FindShowEmployeeButton, 1500);
            if (employeeButton != null && IsUsableElement(employeeButton))
            {
                Thread.Sleep(ToggleDelayMs);
                return;
            }

            var tab = Retry(() => FindDescendant(cf => cf.ByName(ConditionsTabName).And(cf.ByControlType(ControlType.TabItem)))
                                  ?? FindDescendant(cf => cf.ByName(ConditionsTabName)), 5000)
                      ?? throw new Exception($"หาแท็บ '{ConditionsTabName}' ไม่เจอ");

            SafeClickElement(tab);
            Thread.Sleep(ToggleDelayMs);
        }

        /* คลิกปุ่ม "แสดงข้อมูลพนักงาน" */
        private void ClickShowEmployeeDataButton()
        {
            var button = Retry(FindShowEmployeeButton, 6000)
                ?? throw new Exception($"หาปุ่มแสดงข้อมูลพนักงาน ('{IdBtnShowEmp}') ไม่เจอ (ปุ่มที่เห็นตอนนี้: {DescribeVisibleButtons()})");

            WaitUntilEnabled(button, 2000);
            SafeClickElement(button);
            Thread.Sleep(WizardStepDelayMs);
        }

        /* ตั้งค่าวันหยุดใน GridControl: ติ๊กเฉพาะวันที่ระบุ และยกเลิกติ๊กวันอื่นทั้งหมด */
        private void SetHolidayDaysInGrid(string gridAutomationId, string[] daysToKeepChecked)
        {
            var grid = Retry(() => FindDescendant(cf => cf.ByAutomationId(gridAutomationId)), 6000)
                ?? throw new Exception($"หา GridControl วันหยุด '{gridAutomationId}' ไม่เจอ");

            var checkboxes = grid.FindAllDescendants(cf => cf.ByControlType(ControlType.CheckBox)).ToList();
            if (checkboxes.Count == 0)
                throw new Exception($"ไม่พบ Checkbox วันภายใน GridControl '{gridAutomationId}'");

            foreach (var checkboxEl in checkboxes)
            {
                // ข้าม element ค้างจาก wizard ก่อนหน้า (disabled / ไม่มีพิกัดบนจอ)
                if (!IsUsableElement(checkboxEl)) continue;

                bool shouldCheck = checkboxEl.Name != null
                    && daysToKeepChecked.Any(d => checkboxEl.Name.Contains(d, StringComparison.OrdinalIgnoreCase));

                var currentState = GetToggleState(checkboxEl);
                if (currentState.HasValue && currentState.Value == shouldCheck) continue;

                try
                {
                    if (checkboxEl.Patterns.Toggle.IsSupported)
                    {
                        checkboxEl.Patterns.Toggle.Pattern.Toggle();
                        continue;
                    }
                }
                catch { }

                if (shouldCheck) ClickCenter(checkboxEl);
            }

            Thread.Sleep(WizardStepDelayMs);
        }

        /* เปิด Dropdown ประเภทพนักงานแล้วติ๊กตามที่ระบุ (fallback ใช้คีย์บอร์ดถ้าหา checkbox ไม่เจอ) */
        private void SelectEmployeeTypeDropdown(string[] checkIds)
        {
            var dropdown = Retry(() => FindDescendant(cf => cf.ByAutomationId(IdChkComboEmpType))
                                       ?? _automation.GetDesktop().FindFirstDescendant(cf => cf.ByAutomationId(IdChkComboEmpType)), 5000)
                           ?? throw new Exception($"ไม่พบ Dropdown ประเภทพนักงาน '{IdChkComboEmpType}'");

            SafeClickElement(dropdown);
            Thread.Sleep(ToggleDelayMs);

            if (checkIds.Any(id => FindCheckboxAnywhere(id, null) == null))
            {
                SelectEmployeeTypesByKeyboard(checkIds);
                return;
            }

            foreach (var id in checkIds)
                ClickCheckbox(id, shouldBeChecked: true);

            PressKey(VirtualKeyShort.ESCAPE);
            Thread.Sleep(ToggleDelayMs);
        }

        /* ติ๊กประเภทพนักงานด้วยคีย์บอร์ด (Home -> Down ตามลำดับ -> Space) */
        private void SelectEmployeeTypesByKeyboard(string[] checkIds)
        {
            var indexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [IdChkEmpTypeHourly] = 0,
                [IdChkEmpTypeContract] = 1,
                [IdChkEmpTypeDaily] = 2,
                [IdChkEmpTypeMonthly] = 3
            };

            foreach (var id in checkIds)
            {
                if (!indexes.TryGetValue(id, out var index))
                    throw new Exception($"ไม่รู้ลำดับประเภทพนักงานสำหรับ '{id}'");

                PressKey(VirtualKeyShort.HOME);
                for (var i = 0; i < index; i++) PressKey(VirtualKeyShort.DOWN);
                PressKey(VirtualKeyShort.SPACE);
                Thread.Sleep(ToggleDelayMs);
            }

            PressKey(VirtualKeyShort.ESCAPE);
            Thread.Sleep(ToggleDelayMs);
        }

        /* เลือกรูปแบบตารางการทำงานจาก Pop-up lookup */
        private void SelectLookupPattern(string patternCode)
        {
            var container = Retry(() => FindDescendant(cf => cf.ByAutomationId(IdBtnWorkSchedulePattern)))
                ?? throw new Exception($"หาคอนโทรลเลือกรูปแบบตารางการทำงาน ({IdBtnWorkSchedulePattern}) ไม่เจอ");

            ClickCenter(container.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button)) ?? container);
            Thread.Sleep(LookupPopupDelayMs);

            AutomationElement? popup = null;
            try
            {
                var candidates = WaitForLookupPopups(5000);
                if (candidates.Count == 0) throw new Exception("เปิด Pop-up รูปแบบตารางการทำงานไม่สำเร็จ");

                var (row, foundIn) = FindPatternRow(candidates, patternCode, retryMs: 1000);
                if (row == null)
                {
                    TryFilterLookupPopup(patternCode);
                    (row, foundIn) = FindPatternRow(GetLookupPopupCandidates(), patternCode, retryMs: 0);
                }
                popup = foundIn;

                if (row != null) DoubleClickCenter(row);
                ClickPopupSelectButton(popup ?? GetTopLevelAncestor(row ?? _mainWindow));
                Thread.Sleep(WizardStepDelayMs);
            }
            catch
            {
                TryCloseLookupPopup(popup); // กัน popup ค้างเป็น orphan window
                throw;
            }
        }

        /* หาแถวรหัสรูปแบบในแต่ละ popup (ชื่อตรงก่อน ไม่เจอใช้ DataItem แถวแรก) */
        private (AutomationElement? Row, AutomationElement? Popup) FindPatternRow(IEnumerable<AutomationElement> popups, string code, int retryMs)
        {
            foreach (var cand in popups)
            {
                var exact = retryMs > 0
                    ? Retry(() => cand.FindFirstDescendant(cf => cf.ByName(code)), retryMs)
                    : cand.FindFirstDescendant(cf => cf.ByName(code));
                var row = exact ?? cand.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataItem));
                if (row != null) return (row, cand);
            }
            return (null, null);
        }

        /* เลือกพนักงานจาก Pop-up lookup รหัสพนักงาน */
        private void SelectEmployeeFromLookup(string employeeCode)
        {
            var btnLookup = Retry(() => FindDescendant(cf => cf.ByName("รหัสพนักงาน").And(cf.ByControlType(ControlType.Button)))
                                        ?? FindDescendant(cf => cf.ByAutomationId(IdBtnEmployee)), 6000)
                            ?? throw new Exception("หาปุ่มเปิด Lookup รหัสพนักงานไม่เจอ");

            ClickCenter(btnLookup);
            Thread.Sleep(LookupPopupDelayMs);

            AutomationElement? popup = null;
            try
            {
                var candidates = WaitForLookupPopups(8000);

                var row = Retry(() => FindLookupValueRow(employeeCode), 20000, 500);
                if (row == null)
                {
                    TryFilterLookupPopup(employeeCode);
                    row = Retry(() => FindLookupValueRow(employeeCode), 8000, 500);
                }

                if (row == null)
                    throw new Exception($"ไม่พบข้อมูลพนักงาน '{employeeCode}' ใน Pop-up (popup: {DescribeLookupPopups()} | แถวที่เห็น: {DescribeVisibleRows()})");

                popup = GetTopLevelAncestor(row) ?? candidates.FirstOrDefault();

                DoubleClickCenter(row);
                ClickPopupSelectButton(popup);
                Thread.Sleep(WizardStepDelayMs);
            }
            catch
            {
                TryCloseLookupPopup(popup);
                throw;
            }
        }

        /* เลือกค่า "จาก" (ปุ่มซ้ายสุด) และ "ถึง" (ปุ่มขวาสุด) ให้ Range control ตัวเดียว */
        private void SelectRangeValue(string rangeAutomationId, string fromValue, string toValue)
        {
            var container = Retry(() => FindDescendant(cf => cf.ByAutomationId(rangeAutomationId)), 8000)
                ?? throw new Exception($"หา Range control '{rangeAutomationId}' ไม่เจอ");

            PickValueFromLookupButton(() => GetRangeSideButton(container, rangeAutomationId, isFrom: true),
                fromValue, rangeAutomationId, "จาก");
            CloseLeftoverLookupPopups();

            // re-find เพราะ control อาจ re-render หลังเลือกค่า "จาก" จน element เดิม stale
            container = Retry(() => FindDescendant(cf => cf.ByAutomationId(rangeAutomationId)), 5000) ?? container;

            PickValueFromLookupButton(() => GetRangeSideButton(container, rangeAutomationId, isFrom: false),
                toValue, rangeAutomationId, "ถึง");
            CloseLeftoverLookupPopups();
        }

        /* กด Browse แล้วเลือกไฟล์ Import ในหน้าต่าง Open */
        private void BrowseAndSelectFile()
        {
            var btnBrowse = Retry(FindBrowseButton, 4000)
                ?? throw new Exception($"หาปุ่ม Browse ('{IdBtnBrowse}') ไม่เจอ (ปุ่มที่เห็นตอนนี้: {DescribeVisibleButtons()})");

            ClickCenter(btnBrowse);
            Thread.Sleep(LookupPopupDelayMs);

            var openDialog = Retry(() =>
                    _automation.GetDesktop().FindFirstChild(cf =>
                        cf.ByName("Open").Or(cf.ByName("เปิด")).And(cf.ByControlType(ControlType.Window)))
                    ?? _automation.GetDesktop().FindAllChildren(cf => cf.ByControlType(ControlType.Window))
                        .FirstOrDefault(w => w.FindFirstDescendant(cf => cf.ByName("File name:").Or(cf.ByName("ชื่อไฟล์:"))) != null),
                    8000)
                ?? throw new Exception("หาหน้าต่าง Open file ไม่เจอ");

            OpenExactImportFile(openDialog, ResolveImportFilePath());
        }

        /* วาง path ไฟล์ลงช่อง File name -> กด Open -> ยืนยันว่า dialog ปิดและกลับมาหน้า wizard */
        private void OpenExactImportFile(AutomationElement openDialog, string importFilePath)
        {
            if (!File.Exists(importFilePath))
                throw new FileNotFoundException("ไฟล์ Import ที่ค้นพบหายไปก่อนเปิด", importFilePath);

            var fileNameBox = Retry(() =>
                    openDialog.FindFirstDescendant(cf => cf.ByAutomationId("1148"))?.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit))
                    ?? openDialog.FindFirstDescendant(cf => cf.ByName("File name:").Or(cf.ByName("ชื่อไฟล์:")))
                        ?.Parent?.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit))
                    ?? openDialog.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit))
                        .OrderByDescending(e => e.BoundingRectangle.Width).FirstOrDefault(),
                    5000, 150)
                ?? throw new Exception("พบไฟล์จริงแล้ว แต่หาช่อง File name ในหน้าต่าง Open ไม่พบ");

            ClickCenter(fileNameBox);
            Thread.Sleep(100);
            SelectAllText();
            PasteText(importFilePath);
            Thread.Sleep(300);

            var openButton = Retry(() => openDialog.FindFirstDescendant(cf =>
                    cf.ByName("Open").Or(cf.ByName("&Open")).Or(cf.ByName("เปิด")).And(cf.ByControlType(ControlType.Button))),
                3000, 150);

            if (openButton != null && IsUsableElement(openButton))
            {
                ClickCenter(openButton);
                Thread.Sleep(250);
                // dialog บางแบบไม่รับคลิกขณะช่อง filename ยัง focus อยู่ -> Invoke + Enter ซ้ำ
                try { openButton.Patterns.Invoke.Pattern.Invoke(); } catch { }
            }
            PressKey(VirtualKeyShort.RETURN);

            // re-query desktop ทุกรอบ เพราะ dialog อาจสร้าง Window element ใหม่หลังกด Open
            bool closed = WaitUntil(() => _automation.GetDesktop().FindFirstChild(cf =>
                cf.ByName("Open").Or(cf.ByName("เปิด")).And(cf.ByControlType(ControlType.Window))) == null, 10000, 150);

            if (!closed)
                throw new Exception("กด Open แล้ว แต่หน้าต่าง Open ยังไม่ปิด จึงหยุดก่อนกรอกวันที่");

            if (Retry(FindDateStartField, 5000, 150) == null)
                throw new Exception("หน้าต่าง Open ปิดแล้ว แต่ไม่พบช่องวันที่เริ่มต้นหลัง Import");

            Thread.Sleep(PageLoadDelayMs);
        }

        /* ค้นหาไฟล์ Import (.xls/.xlsx/.xlsm) จากทุก Fixed drive ของเครื่อง */
        private static string ResolveImportFilePath()
        {
            var roots = DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
                .Select(d => d.RootDirectory.FullName);

            foreach (var root in roots)
            {
                foreach (var directory in EnumerateDirectoriesSafe(root))
                {
                    IEnumerable<string> files;
                    try { files = Directory.EnumerateFiles(directory); }
                    catch { continue; }

                    var file = files.FirstOrDefault(path =>
                    {
                        var name = Path.GetFileNameWithoutExtension(path).Replace("  ", " ", StringComparison.Ordinal).Trim();
                        return name.Equals(ImportFileName, StringComparison.OrdinalIgnoreCase)
                            && ImportFileExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
                    });

                    if (file != null) return file;
                }
            }

            throw new FileNotFoundException($"ไม่พบไฟล์ '{ImportFileName}' (.xls/.xlsx/.xlsm) ในทุกไดรฟ์ของเครื่อง", ImportFileName);
        }

        /* ไล่โฟลเดอร์แบบไม่ throw (ข้ามโฟลเดอร์ที่ไม่มีสิทธิ์และ ReparsePoint) */
        private static IEnumerable<string> EnumerateDirectoriesSafe(string root)
        {
            var pending = new Stack<string>();
            pending.Push(root);

            while (pending.Count > 0)
            {
                var current = pending.Pop();
                yield return current;

                string[] children;
                try { children = Directory.GetDirectories(current); }
                catch { continue; }

                foreach (var child in children)
                {
                    try
                    {
                        if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0)
                            pending.Push(child);
                    }
                    catch { }
                }
            }
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

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        public EmpWorkScheListUC(AutomationElement mainWindow, UIA3Automation automation)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

        // ── Wizard: ปิดหน้าต่าง / ยืนยัน dialog ──

        /* ปิดหน้าต่าง 1 บาน: กด Cancel ก่อน -> WindowPattern.Close -> คลิกปุ่ม X มุมขวาบนเป็นทางสุดท้าย */
        private bool TryCloseWindow(AutomationElement win)
        {
            var cancelBtn = win.FindFirstDescendant(cf => cf.ByAutomationId(IdBtnCancel))
                            ?? win.FindFirstDescendant(cf => cf.ByName("Cancel").Or(cf.ByName("ยกเลิก")));

            if (cancelBtn != null && cancelBtn.Properties.IsEnabled.ValueOrDefault)
            {
                SafeClickElement(cancelBtn);
                return true;
            }

            bool closed = false;
            try { win.Patterns.Window.PatternOrDefault?.Close(); closed = true; } catch { }

            Thread.Sleep(200);
            if (GetAllTopLevelWindowsOfProcess().Any(w => AutomationElementEquals(w, win)))
            {
                try
                {
                    var rect = win.BoundingRectangle;
                    if (!rect.IsEmpty && rect.Width > 0 && rect.Height > 0)
                    {
                        ForceClickAt(rect.Right - 15, rect.Top + 10);
                        closed = true;
                    }
                }
                catch { }
            }
            return closed;
        }

        /* กดยืนยัน (OK/Yes/ตกลง/ใช่) ถ้ามี MessageBox เด้งขึ้นมาหลังปิดหน้าต่าง */
        private void ClickConfirmDialogIfAny()
        {
            var confirmBtn = _automation.GetDesktop().FindFirstDescendant(cf =>
                cf.ByName("OK").Or(cf.ByName("Yes")).Or(cf.ByName("ตกลง")).Or(cf.ByName("ใช่"))
                  .And(cf.ByControlType(ControlType.Button)));
            if (confirmBtn == null) return;

            SafeClickElement(confirmBtn);
            Thread.Sleep(PageLoadDelayMs);
        }

        // ── Wizard Navigation Buttons (Next / Finish) ──
        //   ยึดชื่อที่มองเห็นเป็นหลัก เพราะ AutomationId อาจค้างจากหน้าก่อนจนเคยกดโดน Cancel แทน Next

        /* หาปุ่ม Next: AutomationId -> ชื่อตรงเป๊ะ -> ชื่อ Contains (ตัดปุ่มเลื่อนหน้าตารางทิ้ง) */
        private AutomationElement? FindNextButton()
        {
            var roots = GetSearchRoots();

            foreach (var root in roots)
            {
                var buttons = SafeFindAllButtons(root).Where(IsUsableElement).ToList();

                var byId = buttons.FirstOrDefault(b => SafeAutomationId(b) == IdBtnNext);
                if (byId != null && !NameLooksLike(byId, ForbiddenNavNames)) return byId;

                var exact = buttons.FirstOrDefault(b => NameEqualsAny(b, NextButtonExactNames) && !NameLooksLike(b, ForbiddenNavNames));
                if (exact != null) return exact;
            }

            foreach (var root in roots)
            {
                var loose = SafeFindAllButtons(root).Where(IsUsableElement)
                    .FirstOrDefault(b => NameLooksLike(b, NextButtonNames)
                                         && !NameLooksLike(b, ForbiddenNavNames)
                                         && !IsGridNavigatorButton(b));
                if (loose != null) return loose;
            }

            return null;
        }

        /* หาปุ่ม Finish ที่ใช้งานได้จริงบนหน้าปัจจุบัน */
        private AutomationElement? FindFinishButton()
        {
            foreach (var root in GetSearchRoots())
            {
                var btn = SafeFindAllButtons(root).Where(IsUsableElement)
                    .FirstOrDefault(b => NameEqualsAny(b, FinishButtonExactNames) && !NameLooksLike(b, ForbiddenNavNames));
                if (btn != null) return btn;
            }
            return null;
        }

        private bool IsFinishButtonPresent() =>
            FindFinishButton() != null || FindDescendant(cf => cf.ByName(SaveCompletedLabel)) != null;

        /* รอ (poll) จนกว่าปุ่ม Next หรือ Finish จะพร้อม เผื่อระบบกำลังประมวลผลนาน */
        private void WaitForNextOrFinishReady() =>
            WaitUntil(() => IsFinishButtonPresent() || FindNextButton() != null, WizardProcessingMaxWaitMs, WizardProcessingPollMs);

        /* คลิกปุ่ม Next ของ wizard (มีด่านกันกดผิดเป็น Cancel/Back) */
        private void ClickNextButton()
        {
            Thread.Sleep(WizardProcessingDelayMs);
            WaitForNextOrFinishReady();

            var button = Retry(FindNextButton, WizardProcessingMaxWaitMs)
                ?? throw new Exception($"หาปุ่ม Next ('{NextButtonName}' / '{IdBtnNext}') ที่ใช้งานได้ไม่เจอ " +
                                       $"(ปุ่มที่เห็นตอนนี้: {DescribeVisibleButtons()})");

            if (NameLooksLike(button, ForbiddenNavNames))
                throw new Exception($"ปุ่มที่หาเจอไม่ใช่ Next แต่เป็น '{SafeName(button)}' " +
                                    $"(AutomationId='{SafeAutomationId(button)}') — ยกเลิกการคลิกเพื่อกันกดผิดปุ่ม");

            WaitUntilEnabled(button, 3000);
            SafeClickElement(button);
            Thread.Sleep(WizardStepDelayMs);
        }

        /* กด Next ไปเรื่อยๆ จนเจอปุ่ม Finish (จำนวนหน้าของแต่ละ flow ไม่คงที่) */
        private void ClickNextUntilFinish(int maxSteps = MaxNextSteps)
        {
            for (int i = 0; i < maxSteps; i++)
            {
                Thread.Sleep(WizardProcessingDelayMs);
                WaitForNextOrFinishReady();

                if (IsFinishButtonPresent()) return;

                if (FindNextButton() == null)
                    throw new Exception($"อยู่หน้าที่ไม่มีทั้งปุ่ม Next และ Finish ที่กดได้ (รอบที่ {i + 1}) " +
                                        $"ปุ่มที่เห็น: {DescribeVisibleButtons()}");

                ClickNextButton();
            }

            if (!IsFinishButtonPresent())
                throw new Exception($"กดปุ่ม Next ครบ {maxSteps} ครั้งแล้วแต่ยังไม่เจอปุ่ม Finish " +
                                    $"(wizard อาจมีสเต็ปเพิ่มเกินคาด หรือหน้าปัจจุบันค้างอยู่) ปุ่มที่เห็น: {DescribeVisibleButtons()}");
        }

        /* รอหน้า "บันทึกข้อมูลเสร็จสิ้น" แล้วกดปุ่ม Finish */
        private void ClickFinishButton(int waitProcessingMs = WizardProcessingMaxWaitMs)
        {
            Retry(() => FindDescendant(cf => cf.ByName(SaveCompletedLabel))
                        ?? _automation.GetDesktop().FindFirstDescendant(cf => cf.ByName(SaveCompletedLabel)),
                  waitProcessingMs);

            var finishBtn = Retry(FindFinishButton, waitProcessingMs)
                ?? throw new Exception($"หาปุ่ม 'Finish' ไม่เจอ (ปุ่มที่เห็นตอนนี้: {DescribeVisibleButtons()})");

            WaitUntilEnabled(finishBtn, 5000);
            SafeClickElement(finishBtn);
            Thread.Sleep(PageLoadDelayMs);
        }

        // ── Field Input Helpers (Date / TextBox / Radio / Checkbox) ──

        /* กรอกวันที่ลงช่อง DateEdit (retry เผื่อช่องยังไม่ bind) */
        private void FillDateField(string automationId, string dateText)
        {
            RetryAction(() =>
            {
                var dateEdit = FindDescendant(cf => cf.ByAutomationId(automationId))
                    ?? throw new Exception($"หาช่องวันที่ '{automationId}' ไม่เจอ (ปุ่ม/หน้าจอที่เห็นตอนนี้: {DescribeVisibleButtons()})");

                WaitUntilEnabled(dateEdit, 3000);
                ReplaceTextAndTab(dateEdit, dateText);
            }, FieldBindRetryTimeoutMs, FieldBindRetryPollMs);

            Thread.Sleep(WizardStepDelayMs);
        }

        /* กรอกข้อความลง TextBox: หาจาก AutomationId -> Label -> Edit ตัวกว้างตัวแรก */
        private void FillTextBoxRobust(string automationId, string text, string? labelHint = null)
        {
            RetryAction(() =>
            {
                var editBox = FindDescendant(cf => cf.ByAutomationId(automationId))
                    ?? (labelHint != null
                        ? FindDescendant(cf => cf.ByName(labelHint))?.Parent?.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit))
                        : null)
                    ?? GetCurrentRoot().FindAllDescendants(cf => cf.ByControlType(ControlType.Edit)).FirstOrDefault(e => e.BoundingRectangle.Width > 150)
                    ?? throw new Exception($"หาช่องกรอก '{automationId}' / '{labelHint}' ไม่เจอ");

                ReplaceTextAndTab(editBox, text);
            }, FieldBindRetryTimeoutMs, FieldBindRetryPollMs);

            Thread.Sleep(TypingDelayMs);
        }

        /* คลิกช่อง -> Ctrl+A -> พิมพ์ข้อความใหม่ -> กด Tab เพื่อ commit ค่า */
        private void ReplaceTextAndTab(AutomationElement field, string text)
        {
            if (HasUsableBounds(field))
            {
                ClickCenter(field);
                Thread.Sleep(100);
            }

            SelectAllText();
            Keyboard.Type(text);
            Thread.Sleep(TypingDelayMs);
            PressKey(VirtualKeyShort.TAB);
        }

        /* คลิกเลือก Radio button ตาม AutomationId */
        private void SelectRadioButton(string automationId)
        {
            var container = Retry(() => FindDescendant(cf => cf.ByAutomationId(automationId)))
                ?? throw new Exception($"หา radio button '{automationId}' ไม่เจอ");

            var radio = container.AsRadioButton()
                ?? container.FindFirstDescendant(cf => cf.ByControlType(ControlType.RadioButton))?.AsRadioButton()
                ?? container;

            ClickCenter(radio);
            Thread.Sleep(ToggleDelayMs);
        }

        /* เลือกตัวเลือกในกลุ่ม Radio ตามข้อความที่แสดง */
        private void SelectRadioGroupOption(string groupAutomationId, string optionText)
        {
            var group = Retry(() => FindDescendant(cf => cf.ByAutomationId(groupAutomationId)))
                ?? throw new Exception($"หากลุ่ม radio '{groupAutomationId}' ไม่เจอ");

            var option = group.FindAllDescendants(cf => cf.ByControlType(ControlType.RadioButton))
                .FirstOrDefault(el => el.Name != null && el.Name.Contains(optionText, StringComparison.OrdinalIgnoreCase))
                ?? throw new Exception($"หาตัวเลือก '{optionText}' ในกลุ่ม radio '{groupAutomationId}' ไม่เจอ");

            ClickCenter(option);
            Thread.Sleep(ToggleDelayMs);
        }

        /* ติ๊ก/ยกเลิกติ๊ก Checkbox: เช็คสถานะก่อน -> Toggle pattern -> คลิกเมาส์เป็นทางสุดท้าย */
        private void ClickCheckbox(string automationId, string? fallbackName = null, bool shouldBeChecked = true)
        {
            var element = Retry(() => FindCheckboxAnywhere(automationId, fallbackName), 8000)
                ?? throw new Exception($"หา Checkbox '{automationId}' / '{fallbackName}' ไม่เจอ (ปุ่ม/หน้าจอที่เห็นตอนนี้: {DescribeVisibleButtons()})");

            WaitUntilEnabled(element, 2000);

            var currentState = GetToggleState(element);
            if (currentState.HasValue && currentState.Value == shouldBeChecked) return;

            try
            {
                if (element.Patterns.Toggle.IsSupported)
                {
                    element.Patterns.Toggle.Pattern.Toggle();
                    Thread.Sleep(ToggleDelayMs);
                    if (WaitForCheckboxState(automationId, fallbackName, shouldBeChecked)) return;
                }
            }
            catch { }

            if (HasUsableBounds(element)) ClickCenter(element);
            else element.Click();

            Thread.Sleep(ToggleDelayMs);
            if (!WaitForCheckboxState(automationId, fallbackName, shouldBeChecked))
                throw new Exception($"Checkbox '{automationId}' ไม่สามารถเปลี่ยนสถานะเป็น {(shouldBeChecked ? "ติ๊ก" : "ไม่ติ๊ก")} ได้");
        }

        private bool WaitForCheckboxState(string automationId, string? fallbackName, bool expected) =>
            WaitUntil(() => GetToggleState(FindCheckboxAnywhere(automationId, fallbackName)) == expected, 2000, 100);

        /* อ่านสถานะติ๊กของ Checkbox (null = อ่านไม่ได้) */
        private static bool? GetToggleState(AutomationElement? element)
        {
            try
            {
                if (element != null && element.Patterns.Toggle.IsSupported)
                    return element.Patterns.Toggle.Pattern.ToggleState.Value == ToggleState.On;
            }
            catch { }
            return null;
        }

        /* หา Checkbox จากทุกหน้าต่างที่เป็นไปได้ (active -> main -> desktop -> หน้าต่างอื่นของ process) */
        private AutomationElement? FindCheckboxAnywhere(string automationId, string? fallbackName)
        {
            AutomationElement? Find(AutomationElement root)
            {
                try
                {
                    return root.FindFirstDescendant(cf => cf.ByAutomationId(automationId))
                        ?? (fallbackName != null ? root.FindFirstDescendant(cf => cf.ByName(fallbackName)) : null);
                }
                catch { return null; }
            }

            var element = Find(GetCurrentRoot()) ?? Find(_mainWindow) ?? Find(_automation.GetDesktop());
            if (element != null) return element;

            try
            {
                foreach (var window in GetAllTopLevelWindowsOfProcess())
                {
                    element = Find(window);
                    if (element != null) return element;
                }
            }
            catch { }

            return null;
        }

        // ── Lookup Popup Helpers (รูปแบบตารางงาน / พนักงาน / Range จาก-ถึง) ──

        /* หาปุ่มฝั่ง "จาก"/"ถึง" ใน Range control โดยเรียงตามตำแหน่ง X จริงบนจอ */
        private AutomationElement GetRangeSideButton(AutomationElement container, string rangeAutomationId, bool isFrom)
        {
            var buttons = container.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                .Where(IsUsableElement)
                .OrderBy(b => b.BoundingRectangle.X)
                .ToList();

            if (buttons.Count < 2)
                throw new Exception($"Range control '{rangeAutomationId}' ไม่พบปุ่มเลือกค่า 'จาก'/'ถึง' ครบ " +
                                    $"(พบ {buttons.Count} ปุ่มที่ใช้งานได้: {DescribeButtons(container)})");

            return isFrom ? buttons.First() : buttons.Last();
        }

        /* เปิด popup เลือกค่าแล้วดับเบิลคลิกแถวข้อมูล (ลองคลิกเปิดได้ 2 ครั้ง + กรองค่าถ้ายังไม่เจอ) */
        private void PickValueFromLookupButton(Func<AutomationElement?> findButton, string value, string rangeAutomationId, string sideLabel)
        {
            AutomationElement? row = null;

            for (int attempt = 1; attempt <= 2 && row == null; attempt++)
            {
                CloseLeftoverLookupPopups();
                Thread.Sleep(attempt == 1 ? 0 : ToggleDelayMs);

                var button = findButton()
                    ?? throw new Exception($"หาปุ่มเลือกค่า '{sideLabel}' ของ '{rangeAutomationId}' ไม่เจอ (อาจถูก re-render หลังเลือกค่าอีกฝั่ง)");

                if (!HasUsableBounds(button))
                    throw new Exception($"ปุ่มเลือกค่า '{sideLabel}' ของ '{rangeAutomationId}' ไม่มีพิกัดให้คลิก");

                WaitUntilEnabled(button, 3000);
                ClickCenter(button);
                Thread.Sleep(LookupPopupDelayMs); // ให้ popup render ก่อนเริ่มสแกน

                row = Retry(() => FindLookupValueRow(value), attempt == 1 ? 15000 : 10000, 500);

                if (row == null && attempt == 1)
                {
                    TryFilterLookupPopup(value);
                    row = Retry(() => FindLookupValueRow(value), 8000, 500);
                }
            }

            if (row == null)
            {
                CloseLeftoverLookupPopups();
                throw new Exception($"ไม่พบค่า '{value}' ({sideLabel}) ใน Pop-up ของ '{rangeAutomationId}' " +
                                    $"(popup: {DescribeLookupPopups()} | แถวที่เห็น: {DescribeVisibleRows()})");
            }

            try
            {
                var popupRoot = GetTopLevelAncestor(row);
                DoubleClickCenter(row);
                ClickPopupSelectButton(popupRoot);
                Thread.Sleep(WizardStepDelayMs);
            }
            catch
            {
                CloseLeftoverLookupPopups();
                throw;
            }
        }

        /* รอจนกว่าจะมี popup lookup อย่างน้อย 1 อัน (คืนลิสต์ว่างถ้าหมดเวลา) */
        private List<AutomationElement> WaitForLookupPopups(int timeoutMs) =>
            Retry(() =>
            {
                var candidates = GetLookupPopupCandidates();
                return candidates.Count > 0 ? candidates : null;
            }, timeoutMs) ?? new List<AutomationElement>();

        /* รวบรวมหน้าต่าง/คอนโทรลของ process ที่น่าจะเป็น popup lookup (มีรายการข้อมูลอยู่ข้างใน) */
        private List<AutomationElement> GetLookupPopupCandidates()
        {
            var result = new List<AutomationElement>();
            int pid = MainProcessId;

            void TryAdd(AutomationElement? el)
            {
                if (el == null) return;
                try
                {
                    if (el.Properties.ProcessId.ValueOrDefault != pid) return;
                    if (AutomationElementEquals(el, _mainWindow)) return;
                    if (result.Any(r => AutomationElementEquals(r, el))) return;
                    if (HasDataList(el)) result.Add(el);
                }
                catch { }
            }

            // 1) desktop children
            try { foreach (var child in _automation.GetDesktop().FindAllChildren()) TryAdd(child); } catch { }

            // 2) top-level windows ของ process (popup DevExpress บางตัว enumerate ต่างจาก desktop children)
            try { foreach (var win in GetAllTopLevelWindowsOfProcess()) TryAdd(win); } catch { }

            // 3) popup ที่ฝังอยู่ใต้ main window: ไล่ parent ขึ้นไปหา container ที่เป็น Window/Pane
            try
            {
                var embedded = _mainWindow.FindAllDescendants(cf =>
                    cf.ByControlType(ControlType.DataItem).Or(cf.ByControlType(ControlType.ListItem)).Or(cf.ByControlType(ControlType.DataGrid)));

                foreach (var el in embedded)
                {
                    try
                    {
                        var parent = el.Parent;
                        while (parent != null && !AutomationElementEquals(parent, _mainWindow))
                        {
                            if (parent.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataItem)) != null
                                || parent.ControlType == ControlType.Window
                                || parent.ControlType == ControlType.Pane)
                            {
                                TryAdd(parent);
                                break;
                            }
                            parent = parent.Parent;
                        }
                    }
                    catch { }
                }
            }
            catch { }

            return result;
        }

        private static bool HasDataList(AutomationElement el) =>
            el.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataItem)) != null
            || el.FindFirstDescendant(cf => cf.ByControlType(ControlType.ListItem)) != null
            || el.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataGrid)) != null;

        /* หาแถวข้อมูลที่มีค่า value จากทุก popup (แม่นยำก่อน -> ผ่อน -> ค้นทุกหน้าต่างของ process) */
        private AutomationElement? FindLookupValueRow(string value)
        {
            var candidates = GetLookupPopupCandidates();

            foreach (var root in candidates) ExpandLookupTree(root);

            foreach (var root in candidates)
            {
                var row = FindValueRowPrecise(root, value);
                if (row != null) return row;
            }

            foreach (var root in candidates)
            {
                var row = FindValueRowLoose(root, value);
                if (row != null) return row;
            }

            // DevExpress tree lookup บางตัวเก็บค่าไว้ที่ Text/TreeItem ลูก ไม่ใช่ที่แถว DataItem
            try
            {
                var roots = GetAllTopLevelWindowsOfProcess()
                    .Where(w => !AutomationElementEquals(w, _mainWindow))
                    .Concat(new[] { GetCurrentRoot() });

                foreach (var root in roots)
                {
                    var match = root.FindAllDescendants().FirstOrDefault(el =>
                        HasUsableBounds(el) && (NameContains(el, value) || ValueContains(el, value)));
                    if (match != null) return GetDataRowAncestor(match) ?? match;
                }
            }
            catch { }

            return null;
        }

        /* กางทุก node ของ tree lookup ให้แถวลูกปรากฏ */
        private static void ExpandLookupTree(AutomationElement root)
        {
            try
            {
                foreach (var item in root.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem)))
                {
                    try
                    {
                        if (item.Patterns.ExpandCollapse.IsSupported
                            && item.Patterns.ExpandCollapse.Pattern.ExpandCollapseState != ExpandCollapseState.Expanded)
                            item.Patterns.ExpandCollapse.Pattern.Expand();
                    }
                    catch { }
                }
            }
            catch { }
        }

        /* จับคู่แบบแม่นยำ: ชื่อตรงตัว หรือแถว DataItem ที่ Name มีค่าอยู่ */
        private static AutomationElement? FindValueRowPrecise(AutomationElement root, string value)
        {
            try
            {
                var exact = root.FindFirstDescendant(cf => cf.ByName(value));
                if (exact != null && IsUsableElement(exact)) return exact;
            }
            catch { }

            try
            {
                return root.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem))
                    .FirstOrDefault(r => IsUsableElement(r) && NameContains(r, value));
            }
            catch { return null; }
        }

        /* จับคู่แบบผ่อน: element ใดก็ได้ที่ Name หรือ Value มีค่าอยู่ */
        private static AutomationElement? FindValueRowLoose(AutomationElement root, string value)
        {
            try
            {
                return root.FindAllDescendants().FirstOrDefault(el =>
                    IsUsableElement(el) && (NameContains(el, value) || ValueContains(el, value)));
            }
            catch { return null; }
        }

        /* ไล่ parent ขึ้นไปหาแถวข้อมูล (DataItem/ListItem/TreeItem) */
        private static AutomationElement? GetDataRowAncestor(AutomationElement element)
        {
            AutomationElement? current = element;
            for (var i = 0; i < 12 && current != null; i++)
            {
                try
                {
                    if (current.ControlType == ControlType.DataItem
                        || current.ControlType == ControlType.ListItem
                        || current.ControlType == ControlType.TreeItem)
                        return current;
                    current = current.Parent;
                }
                catch { break; }
            }
            return null;
        }

        /* พิมพ์ค่าลงช่องค้นหาใน popup (หรือใช้ incremental search ของ grid ถ้าไม่มีช่อง Edit) */
        private void TryFilterLookupPopup(string value)
        {
            foreach (var popup in GetLookupPopupCandidates())
            {
                var edit = popup.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit));
                if (edit != null)
                {
                    ClickCenter(edit);
                    PasteText(value);
                    return;
                }

                if (HasUsableBounds(popup))
                {
                    var focusElement = popup.FindFirstDescendant(cf =>
                        cf.ByControlType(ControlType.DataItem)
                          .Or(cf.ByControlType(ControlType.TreeItem))
                          .Or(cf.ByControlType(ControlType.ListItem))) ?? popup;
                    ClickCenter(focusElement);
                    PasteText(value);
                    Thread.Sleep(TypingDelayMs);
                    return;
                }
            }
        }

        /* คลิกปุ่มยืนยันใน popup (Select/เลือก/ตกลง) ถ้ามี */
        private void ClickPopupSelectButton(AutomationElement? popupRoot)
        {
            try
            {
                var selectBtn = popupRoot?.FindFirstDescendant(cf =>
                    cf.ByName("Select").Or(cf.ByName("เลือก")).Or(cf.ByName("ตกลง")));
                if (selectBtn != null && IsUsableElement(selectBtn)) ClickCenter(selectBtn);
            }
            catch { }
        }

        /* ปิด popup lookup ที่ค้างเมื่อเลือกค่าล้มเหลวกลางคัน (best-effort) */
        private void TryCloseLookupPopup(AutomationElement? popup)
        {
            if (popup == null) return;
            try
            {
                var cancelBtn = popup.FindFirstDescendant(cf => cf.ByName("Cancel").Or(cf.ByName("ยกเลิก")).Or(cf.ByName("Close")));
                if (cancelBtn != null && IsUsableElement(cancelBtn)) SafeClickElement(cancelBtn);
                else popup.Patterns.Window.PatternOrDefault?.Close();
                Thread.Sleep(PageLoadDelayMs);
            }
            catch
            {
                try { PressKey(VirtualKeyShort.ESCAPE); Thread.Sleep(PageLoadDelayMs); } catch { }
            }
        }

        /* ปิด popup lookup ที่ค้างทั้งหมด (ไม่แตะหน้าหลักและหน้าต่าง wizard) */
        private void CloseLeftoverLookupPopups()
        {
            foreach (var win in GetAllTopLevelWindowsOfProcess())
            {
                try
                {
                    if (AutomationElementEquals(win, _mainWindow) || LooksLikeWizardWindow(win)) continue;

                    var closeBtn = win.FindFirstDescendant(cf => cf.ByName("Cancel").Or(cf.ByName("ยกเลิก")).Or(cf.ByName("Close")));
                    if (closeBtn != null && IsUsableElement(closeBtn)) SafeClickElement(closeBtn);
                    else win.Patterns.Window.PatternOrDefault?.Close();

                    Thread.Sleep(PageLoadDelayMs);
                }
                catch { }
            }
        }

        /* เช็คว่าหน้าต่างนี้เป็น wizard (มีปุ่ม Next/Cancel ของ wizard) ไม่ใช่ popup lookup */
        private static bool LooksLikeWizardWindow(AutomationElement window)
        {
            try
            {
                return window.FindFirstDescendant(cf => cf.ByAutomationId(IdBtnNext)) != null
                    || window.FindFirstDescendant(cf => cf.ByAutomationId(IdBtnCancel)) != null
                    || window.FindFirstDescendant(cf => cf.ByName(NextButtonName)) != null;
            }
            catch { return false; }
        }

        // ── Window / Search Root Helpers ──
        //   wizard หลายหน้าเปิดเป็นหน้าต่างแยก (top-level Form) ไม่ใช่ลูกของ _mainWindow

        private int MainProcessId
        {
            get { try { return _mainWindow.Properties.ProcessId.ValueOrDefault; } catch { return 0; } }
        }

        private bool IsSameProcess(AutomationElement el)
        {
            try { return el.Properties.ProcessId.ValueOrDefault == MainProcessId; } catch { return false; }
        }

        /* หาหน้าต่างที่อยู่หน้าสุด (foreground) ในตอนนี้ */
        private AutomationElement? GetActiveTopLevelWindow()
        {
            try
            {
                var hwnd = GetForegroundWindow();
                return hwnd == IntPtr.Zero ? null : _automation.FromHandle(hwnd);
            }
            catch { return null; }
        }

        /* คืนจุดเริ่มค้นหา: หน้าต่าง active ถ้าเป็นของ process เดียวกัน ไม่งั้นใช้ _mainWindow */
        private AutomationElement GetCurrentRoot()
        {
            var active = GetActiveTopLevelWindow();
            return active != null && IsSameProcess(active) ? active : _mainWindow;
        }

        /* หา element โดยเริ่มจากหน้าต่าง active ก่อน แล้ว fallback ไป _mainWindow */
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

        /* รายชื่อหน้าต่างสำหรับค้นหาปุ่ม: active -> _mainWindow -> หน้าต่างอื่นของ process */
        private List<AutomationElement> GetSearchRoots()
        {
            var active = GetCurrentRoot();
            var roots = new List<AutomationElement> { active };
            if (!ReferenceEquals(active, _mainWindow)) roots.Add(_mainWindow);

            try
            {
                foreach (var window in _automation.GetDesktop().FindAllChildren(cf => cf.ByControlType(ControlType.Window)))
                    if (IsSameProcess(window)) roots.Add(window);
            }
            catch { }

            return roots;
        }

        /* รายชื่อหน้าต่างระดับบนสุดทั้งหมดของ process (หน้าต่าง active มาก่อน) */
        private List<AutomationElement> GetAllTopLevelWindowsOfProcess()
        {
            var result = new List<AutomationElement>();

            var active = GetActiveTopLevelWindow();
            if (active != null && IsSameProcess(active)) result.Add(active);

            try
            {
                foreach (var window in _automation.GetDesktop().FindAllChildren(cf => cf.ByControlType(ControlType.Window)))
                {
                    if (!IsSameProcess(window)) continue;
                    if (result.Any(r => AutomationElementEquals(r, window))) continue;
                    result.Add(window);
                }
            }
            catch { }

            return result;
        }

        /* หาหน้าต่าง (ControlType.Window) ที่ element นั้นอยู่ */
        private static AutomationElement? GetTopLevelAncestor(AutomationElement element)
        {
            var current = element;
            for (int i = 0; i < 25; i++)
            {
                try
                {
                    var parent = current.Parent;
                    if (parent == null) break;
                    if (parent.ControlType == ControlType.Window) return parent;
                    current = parent;
                }
                catch { break; }
            }
            return null;
        }

        private static bool AutomationElementEquals(AutomationElement a, AutomationElement b)
        {
            try { return a.Properties.NativeWindowHandle.ValueOrDefault == b.Properties.NativeWindowHandle.ValueOrDefault; }
            catch { return ReferenceEquals(a, b); }
        }

        // ── Click / Keyboard / Element Utilities ──

        private static Point GetCenter(Rectangle rect) =>
            new Point(rect.Left + (rect.Width / 2), rect.Top + (rect.Height / 2));

        /* คลิกซ้ายที่พิกัดด้วย Mouse ของ FlaUI */
        private static void ForceClickAt(int x, int y)
        {
            Mouse.MoveTo(new Point(x, y));
            Thread.Sleep(50);
            Mouse.Click(MouseButton.Left);
            Thread.Sleep(100);
        }

        private static void ClickCenter(AutomationElement element)
        {
            var c = GetCenter(element.BoundingRectangle);
            ForceClickAt(c.X, c.Y);
        }

        /* ดับเบิลคลิกกลาง element (แถวใน popup lookup ต้องดับเบิลคลิกถึงจะเลือกจริง) */
        private static void DoubleClickCenter(AutomationElement element)
        {
            var c = GetCenter(element.BoundingRectangle);
            Mouse.MoveTo(c);
            Thread.Sleep(50);
            Mouse.DoubleClick(MouseButton.Left);
            Thread.Sleep(WizardStepDelayMs);
        }

        /* คลิกอย่างปลอดภัย: รอพิกัดใช้ได้ก่อน -> element.Click() -> Invoke pattern เป็นทางสุดท้าย */
        private void SafeClickElement(AutomationElement element, int waitForRectMs = 3000)
        {
            if (WaitUntil(() => HasUsableBounds(element), waitForRectMs, 150))
            {
                ClickCenter(element);
                return;
            }

            try
            {
                element.Click();
            }
            catch (FlaUI.Core.Exceptions.NoClickablePointException)
            {
                try
                {
                    var invokePattern = element.Patterns.Invoke.PatternOrDefault;
                    if (invokePattern != null)
                    {
                        invokePattern.Invoke();
                        return;
                    }
                }
                catch { }

                throw new Exception($"ไม่สามารถคลิกปุ่ม '{SafeName(element)}' ได้ (หาตำแหน่งที่คลิกได้บนหน้าจอไม่เจอ)");
            }
        }

        private static void PressKey(VirtualKeyShort key)
        {
            Keyboard.Press(key);
            Keyboard.Release(key);
        }

        private static void SelectAllText()
        {
            Keyboard.Press(VirtualKeyShort.CONTROL);
            Keyboard.Press(VirtualKeyShort.KEY_A);
            Keyboard.Release(VirtualKeyShort.KEY_A);
            Keyboard.Release(VirtualKeyShort.CONTROL);
            Thread.Sleep(50);
        }

        /* วางข้อความผ่าน Clipboard (รองรับภาษาไทย/path ยาว) fallback เป็นการพิมพ์ */
        private static void PasteText(string text)
        {
            try
            {
                Clipboard.SetText(text);
                Keyboard.Press(VirtualKeyShort.CONTROL);
                Keyboard.Press(VirtualKeyShort.KEY_V);
                Keyboard.Release(VirtualKeyShort.KEY_V);
                Keyboard.Release(VirtualKeyShort.CONTROL);
            }
            catch
            {
                Keyboard.Type(text);
            }
        }

        /* เช็คว่า element ใช้งานได้จริง (มองเห็น, enable, มีพิกัดบนจอ) — กรอง element ค้างจากหน้าก่อน */
        private static bool IsUsableElement(AutomationElement element)
        {
            if (element == null) return false;
            try
            {
                if (element.Properties.IsOffscreen.IsSupported && element.Properties.IsOffscreen.Value) return false;
                if (element.Properties.IsEnabled.IsSupported && !element.Properties.IsEnabled.Value) return false;
                return HasUsableBounds(element);
            }
            catch { return false; }
        }

        private static bool HasUsableBounds(AutomationElement element)
        {
            try
            {
                var rect = element.BoundingRectangle;
                return !rect.IsEmpty && rect.Width > 0 && rect.Height > 0;
            }
            catch { return false; }
        }

        /* เช็คว่าปุ่มนี้อยู่ในแถบเลื่อนหน้าของ DevExpress GridControl (มีปุ่มข้างเคียงแบบ navigator ≥ 2 ปุ่ม) */
        private static bool IsGridNavigatorButton(AutomationElement button)
        {
            try
            {
                var parent = button.Parent;
                if (parent == null) return false;

                int hits = parent.FindAllChildren(cf => cf.ByControlType(ControlType.Button))
                    .Select(SafeName)
                    .Count(n => GridNavigatorSiblingNames.Any(g =>
                        string.Equals(n.Replace("&", "").Trim(), g, StringComparison.OrdinalIgnoreCase)));

                return hits >= 2;
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

        private static string CleanName(AutomationElement element) => SafeName(element).Replace("&", "").Trim();

        private static bool NameEqualsAny(AutomationElement element, string[] names)
        {
            var clean = CleanName(element);
            return clean.Length > 0 && names.Any(n => string.Equals(clean, n.Replace("&", "").Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private static bool NameLooksLike(AutomationElement element, string[] keywords)
        {
            var clean = CleanName(element);
            return clean.Length > 0 && keywords.Any(k => clean.Contains(k, StringComparison.OrdinalIgnoreCase));
        }

        private static bool NameContains(AutomationElement element, string value)
        {
            var name = SafeName(element);
            return !string.IsNullOrEmpty(name) && name.Contains(value, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ValueContains(AutomationElement element, string value)
        {
            try
            {
                return element.Patterns.Value.IsSupported
                    && (element.Patterns.Value.Pattern.Value.Value ?? "").Contains(value, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        // ── Diagnostics (ข้อความประกอบ error / debug) ──

        /* สรุปรายชื่อปุ่มที่เห็นบนหน้าปัจจุบัน ใช้แนบไปกับข้อความ error */
        private string DescribeVisibleButtons()
        {
            try
            {
                var names = SafeFindAllButtons(GetCurrentRoot())
                    .Where(IsUsableElement)
                    .Select(SafeName)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct()
                    .Take(15);
                return string.Join(" | ", names);
            }
            catch { return "(อ่านรายชื่อปุ่มไม่ได้)"; }
        }

        private string DescribeLookupPopups() => string.Join(" | ", GetLookupPopupCandidates().Select(SafeName));

        private string DescribeVisibleRows() => string.Join(" | ", GetLookupPopupCandidates()
            .SelectMany(p => p.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem)))
            .Select(SafeName).Where(n => !string.IsNullOrWhiteSpace(n)).Take(10));

        private static string DescribeButtons(AutomationElement root) => string.Join(" | ",
            root.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                .Select(SafeName).Where(n => !string.IsNullOrWhiteSpace(n)).Take(10));

        private static string DescribeException(Exception ex) =>
            ex.InnerException == null ? ex.Message : $"{ex.Message}: {ex.InnerException.Message}";

        /* Debug: พ่นรายชื่อปุ่มทั้งหมดบนหน้าต่าง active ลง log เช่น DumpButtons("ก่อนกด Next") */
        private void DumpButtons(string tag)
        {
            try
            {
                var root = GetCurrentRoot();
                var all = root.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
                var lines = all.Select(b =>
                {
                    string state = "";
                    try
                    {
                        var r = b.BoundingRectangle;
                        state = $"enabled={b.Properties.IsEnabled.ValueOrDefault}, offscreen={b.Properties.IsOffscreen.ValueOrDefault}, rect={r.X},{r.Y},{r.Width}x{r.Height}";
                    }
                    catch { }
                    return $"  - Name='{SafeName(b)}' Id='{SafeAutomationId(b)}' {state}";
                });

                TestLogger.LogInfo(LogSource, $"[{tag}] (root='{SafeName(root)}') พบปุ่มทั้งหมด {all.Length} ปุ่ม:\n{string.Join("\n", lines)}");
            }
            catch (Exception ex)
            {
                TestLogger.LogInfo(LogSource, $"[{tag}] อ่านรายชื่อปุ่มไม่ได้: {ex.Message}");
            }
        }

        // ── Logging & Retry Helpers ──

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

        /* รอจนกว่า element จะใช้งานได้ */
        private static bool WaitUntilEnabled(AutomationElement element, int timeoutMs = 3000) =>
            WaitUntil(() => IsUsableElement(element), timeoutMs, 200);

        /* ตัวช่วย Retry ค้นหา Element ซ้ำๆ จนกว่าจะเจอหรือหมดเวลา Timeout */
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

        /* ตัวช่วยรัน Action ซ้ำจนกว่าจะไม่ throw หรือหมดเวลา (โยน TimeoutException พร้อม error ล่าสุด) */
        private static void RetryAction(Action action, int timeoutMs = 8000, int pollMs = 400)
        {
            Exception? last = null;
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try { action(); return; }
                catch (Exception ex) { last = ex; Thread.Sleep(pollMs); }
            }
            throw new TimeoutException("UI action did not complete within the timeout", last);
        }
    }
}