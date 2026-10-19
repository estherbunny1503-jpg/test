using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using HRMITestFlaUI.Base;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace HRMITestFlaUI.Module.Time_Attendance
{
    public class EmpWorkScheListUC
    {
        // =========================================================================================
        // หมวดที่ 1: ข้อมูลการทดสอบ (Test Data)
        // ขั้นตอนอ้างอิง TestStep.ods ชีท "บันทึกรูปแบบตารางการทำงาน" — วันที่ในไฟล์เป็น พ.ศ. (ลบ 543)
        // =========================================================================================

        // --- Data Records สำหรับใช้งานในแต่ละเฟส ---
        private sealed record GroupScheduleItem(
            string Name,
            DateTime Start, DateTime End,
            string WorkType,
            string PatternCode,
            int Progress,
            string ProgressMsg);

        private sealed record PeriodItem(
            string Name,
            DateTime Start, DateTime End,
            int Progress,
            string ProgressMsg);

        private sealed record CopyEmployeeItem(
            string Name,
            DateTime Start, DateTime End,
            string EmployeeCode,
            int Progress,
            string ProgressMsg);

        private sealed record CancelGroupItem(
            string Name,
            DateTime Start, DateTime End,
            string? ShiftFrom, string? ShiftTo,       // null = ข้ามขั้นตอนนี้ (ไม่กรอกช่วงรหัสกะงาน)
            string OrgFrom, string OrgTo,
            string EmpGroupFrom, string EmpGroupTo,
            string[] EmpTypes,
            string PositionFrom, string PositionTo,
            string LevelFrom, string LevelTo,
            string EmpCodeFrom, string EmpCodeTo,
            bool CancelWorkSchedule,
            bool CancelWorkHoliday,
            int Progress,
            string ProgressMsg);

        // [TestCase01] กำหนดตารางการทำงานของพนักงานแบบรวม (แถว 7-8)
        private static readonly GroupScheduleItem[] Step1_SetScheduleCases =
        {
            new("TestCase01_SetScheduleForGroup_General",
                new DateTime(2026, 1, 1), new DateTime(2026, 12, 31),
                "กำหนดตารางการทำงานแบบทั่วไป",
                "WSG001",
                10, "Set Schedule For Group: General..."),
            new("TestCase01_SetScheduleForGroup_Rotation",
                new DateTime(2026, 2, 1), new DateTime(2026, 3, 31),
                "กำหนดตารางการทำงานแบบวนกะงาน",
                "WSG002",
                20, "Set Schedule For Group: Rotation...")
        };

        // [TestCase01] คัดลอกตารางการทำงานของพนักงาน (radio ในวิซาร์ดแบบรวม, แถว 11)
        private static readonly PeriodItem[] Step1_CopyShortcutCases =
        {
            new("TestCase01_CopyScheduleShortcut",
                new DateTime(2026, 1, 3), new DateTime(2026, 12, 3),
                30, "Copy Schedule Shortcut...")
        };

        // [TestCase01] กำหนดวันหยุด (แถว 14)
        private static readonly PeriodItem[] Step1_HolidayCases =
        {
            new("TestCase01_SetHoliday",
                new DateTime(2026, 6, 1), new DateTime(2026, 12, 31),
                40, "Set Holiday...")
        };

        // [TestCase02] Import ตารางการทำงานของพนักงาน (แถว 21)
        private static readonly PeriodItem[] Step2_ImportCases =
        {
            new("TestCase02_Import",
                new DateTime(2026, 5, 15), new DateTime(2026, 6, 15),
                55, "Import Employee Work Schedule...")
        };

        // [TestCase03] คัดลอกตารางการทำงานของพนักงาน: เลือกพนักงานในตารางก่อน แล้ว More Action (แถว 32)
        private static readonly CopyEmployeeItem[] Step3_CopyEmployeeCases =
        {
            new("TestCase03_CopyScheduleOfEmployee",
                new DateTime(2026, 1, 1), new DateTime(2026, 12, 31),
                "EMP00001",
                70, "Copy Schedule Of Employee...")
        };

        // [TestCase04] ยกเลิกตารางการทำงานของพนักงานแบบรวม (แถว 42-43)
        // TestStep: ช่วงหน่วยงาน "เลือก 001 ถึง ORG005"
        private const string FirstAvailableOrgUnitCode = "001";
        private static readonly CancelGroupItem[] Step4_CancelGroupCases =
        {
            new("TestCase04_CancelScheduleForGroup_1",
                new DateTime(2026, 9, 1), new DateTime(2026, 12, 31),
                "NRM-M01", "OT-EVN02",
                FirstAvailableOrgUnitCode, "ORG005",
                "EG-001", "EG-004",
                new[] { IdChkEmpTypeHourly, IdChkEmpTypeContract },
                "DW001", "POS005",
                "EL-001", "EL-005",
                "EMP00001", "EMP00004",
                true,
                true,
                85, "Cancel Schedule For Group (1)..."),
            new("TestCase04_CancelScheduleForGroup_2",
                new DateTime(2026, 8, 3), new DateTime(2026, 9, 3),
                null, null,                          // TestStep: ข้ามขั้นตอนนี้ไป
                FirstAvailableOrgUnitCode, "ORG005",
                "EG-001", "EG-004",
                new[] { IdChkEmpTypeDaily, IdChkEmpTypeMonthly },
                "DW001", "POS005",
                "EL-001", "EL-005",
                "EMP00001", "EMP00004",
                false,
                true,
                100, "Cancel Schedule For Group (2)...")
        };

        private const string SampleEmployeeCodeForCopyShortcut = "EMP00001"; // พนักงานที่เลือกในเคส CopyScheduleShortcut
        private const string HolidayTopicText = "หยุดเพื่อไปหาสิ่งใหม่ๆ";

        // จำนวนครั้งที่กด Next "หลังกดปุ่มแสดงข้อมูลพนักงาน" ก่อนกด Finish ตาม TestStep
        private const int NextClicksAfterShow_SetSchedule = 3;   // แถว 7-8   (K, L, M → Finish N)
        private const int NextClicksAfterShow_CopyShortcut = 2;  // แถว 11    (J, K    → Finish L)
        private const int NextClicksAfterShow_Holiday = 1;       // แถว 14    (K       → Finish L)
        private const int NextClicksAfterShow_CopyEmployee = 2;  // แถว 32    (H, I    → Finish J)
        // Import (แถว 21): ไม่มีขั้นแสดงข้อมูลพนักงาน — หลังกรอกวันที่กด Next 3 ครั้ง (F, G, H → Finish I)
        private const int NextClicks_Import = 3;

        // true  = จำนวน Next ต้องตรง TestStep เป๊ะ ไม่ตรงให้เคส FAIL
        // false = ถ้าวิซาร์ดจริงเหลือแค่ Finish ก่อนครบจำนวน ให้กด Finish แล้วบันทึกคำเตือนใน log (เคสไม่ FAIL)
        private static readonly bool StrictNextCount = false;
        // เวลารอสูงสุดให้ปุ่ม Next/Finish โผล่หลังกด Next (HRMI ประมวลผลหนักได้นานกว่า 10 วินาที)
        private const int NavButtonAppearTimeoutMs = 60000;


        // =========================================================================================
        // หมวดที่ 2: ส่วนที่ขึ้นกับหน้าจอนั้นๆ (Screen-Specific Implementation)
        // **เมื่อเปลี่ยนไปทำหน้าจออื่น ต้องมาแก้ไขค่าและเมทอดในส่วนนี้ทั้งหมด**
        // =========================================================================================

        // 2.1 Locator & Mappings ประจำหน้าจอ
        private const string ScreenLogName = "EmpWorkScheListUC";
        private const string MenuName = "บันทึกรูปแบบตารางการทำงาน";

        private const string MoreActionName = "More Action";
        private const string NextButtonName = "Next >";
        private const string FinishButtonName = "Finish";
        private const string ConditionsTabName = "กำหนดเงื่อนไข";

        // รายการใน More Action
        private const string ActionSetScheduleForGroup = "กำหนดตารางการทำงานของพนักงานแบบรวม";
        private const string ActionImportSchedule = "Import ตารางการทำงานของพนักงาน";
        private const string ActionCopyScheduleOfEmployee = "คัดลอกตารางการทำงานของพนักงาน";
        private const string ActionCancelScheduleForGroup = "ยกเลิกตารางการทำงานของพนักงานแบบรวม";

        // 2.2 ฟิลด์ / ปุ่ม / Radio / Checkbox ประจำหน้าจอ
        private const string IdDateStart = "devDateStart";
        private const string IdDateEnd = "devDateEnd";
        private const string IdRdgAddWorkType = "devRdgAddWorkType";
        private const string IdRdgSetWorkSchedule = "devRdgWorkSche";
        private const string IdRdgCopyWorkSche = "devRdgCopyWorkSche";
        private const string IdRdgHolidaySche = "devRdgHolidaySche";

        private const string IdBtnWorkSchedulePattern = "devBtnTemplateSche";
        private const string IdBtnHolidaySche = "devBtnHolidaySche";
        private const string IdBtnEmployee = "devBtnEmployee";
        private const string IdBtnBrowse = "devBtnBrowse";
        private const string IdBtnShowEmployeeData = "devBtnShowEmp";

        private const string IdTxtHolidayTopic = "devtxtHoliday";
        private const string IdChkReplaceHoliday = "devChkReplaceByHoliday";

        private static readonly string[] AllHolidayDayCheckboxIds =
        {
            "devChkMonHoliday", "devChkTueHoliday", "devChkWedHoliday", "devChkThurHoliday",
            "devChkFriHoliday", "devChkSatHoliday", "devChkSunHoliday"
        };

        // ── ปุ่ม "รีเช็คข้อมูลพนักงาน" ใต้ตารางแสดงข้อมูลพนักงาน ──
        // ใช้บังคับให้แอป sync dataset พนักงานให้เสร็จก่อนกด Next
        // (กัน NullReferenceException ที่ GenIDGroupAndGenerateDatetime ซึ่งพังเฉพาะตอนรันผ่าน automation)
        private const string BtnRecheckEmployeeDataName = "รีเช็คข้อมูลพนักงาน";

        // ── ช่วงข้อมูล (from-to) สำหรับ "ยกเลิกตารางการทำงานของพนักงานแบบรวม" ──
        // ตรวจกับ control จริงใน Prosoft.HRMi.Win.dll (frmWizEmpWorkScheDelGroup): ช่วงจาก-ถึงเป็น RangeManager
        // ข้างในมี devBtnEditBegin / devBtnEditEnd, ประเภทพนักงานเป็น CheckComboEdit (รายการเป็นชื่อภาษาไทย)
        private const string IdRangeShiftCode = "rangeManagerTS";                // ช่วงรหัสกะงาน
        private const string IdRangeOrgUnit = "RangeMgOrgUnit";                  // ช่วงหน่วยงาน
        private const string IdRangeEmpGroup = "RangeMgEmpGroup";                // ช่วงกลุ่มพนักงาน
        private const string IdRangePosition = "RangeMgPosition";                // ช่วงตำแหน่งงาน
        private const string IdRangeEmpLevel = "RangeMgEmpLevel";                // ช่วงระดับพนักงาน
        private const string IdRangeEmployee = "RangeMgEmployee";                // ช่วงรหัสพนักงาน
        private const string IdRangeBegin = "devBtnEditBegin";
        private const string IdRangeEnd = "devBtnEditEnd";
        private const string IdChkComboEmpType = "ChkComboEditEmpType";          // ประเภทพนักงาน (dropdown ติ๊กหลายรายการ)
        private const string IdChkComboEmpTypeEdit = "devCmbComboEdit";
        private const string IdChkEmpTypeHourly = "พนักงานรายชั่วโมง";
        private const string IdChkEmpTypeContract = "พนักงานรายเหมา";
        private const string IdChkEmpTypeDaily = "พนักงานรายวัน";
        private const string IdChkEmpTypeMonthly = "พนักงานรายเดือน";

        private const string IdChkCancelSchedule = "devChkDelShift";             // ผลที่คาดหวัง: ยกเลิกกะงาน
        private const string IdChkCancelWorkHoliday = "devChkDelHoliday";        // ผลที่คาดหวัง: ยกเลิกวันหยุดกะ

        // 2.3 ค่า Timeout / Delay ประจำหน้าจอ
        private const int PageLoadDelayMs = 300;
        private const int WizardStepDelayMs = 200;
        private const int TypingDelayMs = 50;
        private const int FieldBindRetryTimeoutMs = 15000;
        private const int FieldBindRetryPollMs = 250;

        private const int WizardButtonRetryTimeoutMs = 10000;
        private const int WizardStepSettleTimeoutMs = 6000;

        // เวลารอ "เผื่อประมวลผล" หลังกด Next/Finish — แอปต้องประมวลผลเบื้องหลัง
        // (สร้าง dataset พนักงาน / คำนวณตารางงาน) automation คลิกเร็วเกินไปจนแซงหน้าประมวลผล
        private const int WizardProcessingDelayMs = 2000;
        // รอพิเศษตอนคลิก Next ครั้งแรกหลังแสดงข้อมูลพนักงาน (GenIDGroupAndGenerateDatetime)
        private const int FirstNextExtraWaitMs = 120000;
        // เวลารอสูงสุดให้ HRMI ประมวลผลหลังกด Finish จนวิซาร์ดปิด
        private const int FinishProcessingTimeoutMs = 300000;

        /// <summary>
        /// ขั้นที่ 1: เปิดหน้าจอ บันทึกรูปแบบตารางการทำงาน
        /// </summary>
        public void NavigateToEmpWorkScheListUC()
        {
            TestLogger.LogInfo(ScreenLogName, $"เริ่มทดสอบหน้าจอ {MenuName}");
            TestProgressHelper.Report(2, $"Opening {MenuName} Menu via Search...");

            // เปิดหน้าจอด้วยช่องค้นหาเมนู แบบเดียวกับโมดูลก่อนหน้า (ไม่คลิกแถวเมนูตามลำดับ)
            MenuSearch.Open(_mainWindow, MenuName);
            Thread.Sleep(PageLoadDelayMs);
        }

        /// <summary>
        /// ขั้นที่ 2: รันทุกเคสตาม Test Step (TestCase01-04) แล้วสรุปผลรวม
        /// </summary>
        public void RunAllValidationTests()
        {
            _totalSuccess = 0;
            _totalFail = 0;

            int total = Step1_SetScheduleCases.Length + Step1_CopyShortcutCases.Length + Step1_HolidayCases.Length
                      + Step2_ImportCases.Length + Step3_CopyEmployeeCases.Length + Step4_CancelGroupCases.Length;
            TestLogger.LogInfo(ScreenLogName, $"เริ่มทดสอบ{MenuName} ({total} รายการ)");
            TestLogger.LogInfo(ScreenLogName, $"build=CompleteWizardExact-v2 StrictNextCount={StrictNextCount} NavTimeout={NavButtonAppearTimeoutMs / 1000}s");

            // ── TestCase01: กำหนดตารางการทำงานแบบรวม / คัดลอก (shortcut) / กำหนดวันหยุด ──
            foreach (var i in Step1_SetScheduleCases)
                RunScenarioStep(i.Name, () => SetScheduleForGroup(i.Start, i.End, i.WorkType, i.PatternCode),
                    i.Progress, i.ProgressMsg);
            foreach (var i in Step1_CopyShortcutCases)
                RunScenarioStep(i.Name, () => CopyScheduleShortcut(i.Start, i.End),
                    i.Progress, i.ProgressMsg);
            foreach (var i in Step1_HolidayCases)
                RunScenarioStep(i.Name, () => SetHoliday(i.Start, i.End),
                    i.Progress, i.ProgressMsg);

            // ── TestCase02: Import ตารางการทำงานของพนักงาน ──
            foreach (var i in Step2_ImportCases)
                RunScenarioStep(i.Name, () => ImportEmployeeWorkSchedule(i.Start, i.End),
                    i.Progress, i.ProgressMsg);

            // ── TestCase03: คัดลอกตารางการทำงานของพนักงาน ──
            foreach (var i in Step3_CopyEmployeeCases)
                RunScenarioStep(i.Name, () => CopyScheduleOfEmployee(i.Start, i.End, i.EmployeeCode),
                    i.Progress, i.ProgressMsg);

            // ── TestCase04: ยกเลิกตารางการทำงานของพนักงานแบบรวม ──
            foreach (var i in Step4_CancelGroupCases)
                RunScenarioStep(i.Name, () => CancelScheduleForGroup(
                    start: i.Start, end: i.End,
                    workGroupFrom: i.ShiftFrom, workGroupTo: i.ShiftTo,
                    orgFrom: i.OrgFrom, orgTo: i.OrgTo,
                    empGroupFrom: i.EmpGroupFrom, empGroupTo: i.EmpGroupTo,
                    empTypeChecks: i.EmpTypes,
                    positionFrom: i.PositionFrom, positionTo: i.PositionTo,
                    levelFrom: i.LevelFrom, levelTo: i.LevelTo,
                    empCodeFrom: i.EmpCodeFrom, empCodeTo: i.EmpCodeTo,
                    cancelWorkSchedule: i.CancelWorkSchedule, cancelWorkHoliday: i.CancelWorkHoliday),
                    i.Progress, i.ProgressMsg);

            LogTotalSummary();

            // การันตีว่าแอปถูกปิดจริงหลังรันเทสครบทุกเคส (ไม่แก้ BaseTest.cs เพราะใช้ร่วมกับทีม)
            // ถ้า BaseTest.cs ปิดแอปสำเร็จแล้ว เมธอดนี้จะไม่ทำอะไรเพิ่ม (เรียกซ้ำได้อย่างปลอดภัย)
            EnsureApplicationExits();
        }

        /// <summary>
        /// ขั้นที่ 3: แก้ไขข้อมูล (ยังไม่มี Test Step ใน Excel)
        /// </summary>
        public void RunEditTests() { }

        /// <summary>
        /// ขั้นที่ 4: Validation ตอนแก้ไขข้อมูล (ยังไม่มี Test Step ใน Excel)
        /// </summary>
        public void RunEditValidationTests() { }

        /// <summary>
        /// ขั้นที่ 5: ลบข้อมูล (ยังไม่มี Test Step ใน Excel)
        /// </summary>
        public void RunDeleteTests() { }

        // --- Custom Mapping Logic ประจำหน้าจอนี้ ---

        /// <summary>
        /// TestCase01: กำหนดตารางการทำงานของพนักงานแบบรวม (ทั่วไป / วนกะงาน) — TestStep แถว 7-8
        /// More Action → กรอกวันที่ → Radio → รูปแบบตารางงาน (Select) → Next → แสดงข้อมูลพนักงาน → Next ×3 → Finish
        /// </summary>
        private void SetScheduleForGroup(DateTime start, DateTime end, string workType, string patternCode)
        {
            OpenMoreActionWizard(ActionSetScheduleForGroup);
            FillDateRange(start, end);

            SelectRadioButton(_mainWindow, IdRdgSetWorkSchedule);
            SelectRadioGroupOption(_mainWindow, IdRdgAddWorkType, workType);
            SelectWorkSchedulePattern(patternCode);

            ClickWizardNavButton(NextButtonName);
            ShowEmployeesAndFinish(useConditionsTab: true, nextClicksAfterShow: NextClicksAfterShow_SetSchedule);
        }

        /// <summary>
        /// TestCase01: คัดลอกตารางการทำงานของพนักงาน (shortcut ผ่านวิซาร์ดแบบรวม + Radio "คัดลอก") — TestStep แถว 11
        /// ... → Next → แสดงข้อมูลพนักงาน → Next ×2 → Finish
        /// </summary>
        private void CopyScheduleShortcut(DateTime start, DateTime end)
        {
            OpenMoreActionWizard(ActionSetScheduleForGroup);
            FillDateRange(start, end);

            SelectRadioButton(_mainWindow, IdRdgCopyWorkSche);
            SelectEmployeeByCode(SampleEmployeeCodeForCopyShortcut);

            ClickWizardNavButton(NextButtonName);
            ShowEmployeesAndFinish(useConditionsTab: true, nextClicksAfterShow: NextClicksAfterShow_CopyShortcut);
        }

        /// <summary>
        /// TestCase01: กำหนดวันหยุด — TestStep แถว 14
        /// ... → Next → แสดงข้อมูลพนักงาน → Next ×1 → Finish
        /// </summary>
        private void SetHoliday(DateTime start, DateTime end)
        {
            OpenMoreActionWizard(ActionSetScheduleForGroup);
            FillDateRange(start, end);

            SelectRadioButton(_mainWindow, IdRdgHolidaySche);
            FillTextBox(_mainWindow, IdTxtHolidayTopic, HolidayTopicText);
            ClickCheckbox(IdChkReplaceHoliday);
            SetHolidayDays("devChkSatHoliday", "devChkSunHoliday");

            ClickWizardNavButton(NextButtonName);
            ShowEmployeesAndFinish(useConditionsTab: true, nextClicksAfterShow: NextClicksAfterShow_Holiday);
        }

        /// <summary>
        /// TestCase02: Import ตารางการทำงานของพนักงาน — TestStep แถว 21
        /// More Action → Browse เลือกไฟล์ → กรอกวันที่ → Next ×3 → Finish
        /// </summary>
        private void ImportEmployeeWorkSchedule(DateTime start, DateTime end, string? filePath = null)
        {
            OpenMoreActionWizard(ActionImportSchedule);

            filePath ??= ResolveImportFile();
            BrowseAndSelectFile(filePath);

            FillDateRange(start, end);
            CompleteWizardExact(NextClicks_Import);
        }

        /* หาไฟล์ Import ตามลำดับ: 1) ตัวแปรสภาพแวดล้อม HRMI_IMPORT_WORKSCHE_FILE
           2) TestDataPaths.Find ชื่อเดิม 3) ไฟล์ .xlsx/.xlsm ใน TestDataPaths.Folder ที่ชื่อมีคำว่า "import"
           ถ้าไม่เจอ จะแจ้งรายชื่อไฟล์ที่มีอยู่ในโฟลเดอร์ เพื่อให้ตั้งชื่อได้ถูก */
        private static string ResolveImportFile()
        {
            string? env = Environment.GetEnvironmentVariable("HRMI_IMPORT_WORKSCHE_FILE");
            if (!string.IsNullOrWhiteSpace(env) && File.Exists(env)) return env;

            var found = TestDataPaths.Find("Import Employee Work Schedule", "ImportEmployeeWorkSchedule");
            if (found != null) return found;

            string[] existing = Array.Empty<string>();
            try
            {
                existing = Directory.EnumerateFiles(TestDataPaths.Folder)
                    .Where(f => f.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".xlsm", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
            }
            catch { }

            var guess = existing.FirstOrDefault(f => Path.GetFileName(f).Contains("import", StringComparison.OrdinalIgnoreCase));
            if (guess != null) return guess;

            string listing = existing.Length == 0 ? "(ไม่มีไฟล์ Excel เลย)" : string.Join(", ", existing.Select(Path.GetFileName));
            throw new FileNotFoundException(
                $"ไม่พบไฟล์ Import ตารางการทำงาน ในโฟลเดอร์ {TestDataPaths.Folder}; ไฟล์ Excel ที่มี: {listing}; " +
                "วางไฟล์ชื่อ 'Import Employee Work Schedule.xlsx' หรือกำหนดตัวแปร HRMI_IMPORT_WORKSCHE_FILE เป็นพาธไฟล์");
        }

        /// <summary>
        /// TestCase03: คัดลอกตารางการทำงานของพนักงาน — TestStep แถว 32
        /// เลือกพนักงานในตารางหลักก่อน → More Action → กรอกวันที่ → Next → แสดงข้อมูลพนักงาน → Next ×2 → Finish
        /// </summary>
        private void CopyScheduleOfEmployee(DateTime start, DateTime end, string employeeCode)
        {
            SelectMainGridRow(employeeCode);
            OpenMoreActionWizard(ActionCopyScheduleOfEmployee);
            FillDateRange(start, end);

            ClickWizardNavButton(NextButtonName);
            ShowEmployeesAndFinish(useConditionsTab: false, nextClicksAfterShow: NextClicksAfterShow_CopyEmployee);
        }

        /// <summary>
        /// TestCase04: ยกเลิกตารางการทำงานของพนักงานแบบรวม — TestStep แถว 42-43
        /// รองรับ: ช่วงรหัสกะงาน / หน่วยงาน / กลุ่มพนักงาน / ประเภทพนักงาน (เลือกบางตัวจาก dropdown)
        /// / ตำแหน่งงาน / ระดับพนักงาน / รหัสพนักงาน / ผลที่คาดหวัง (ยกเลิกกะงาน และ/หรือ ยกเลิกวันหยุดกะ)
        /// </summary>
        private void CancelScheduleForGroup(
            DateTime start, DateTime end,
            string? workGroupFrom, string? workGroupTo, // null = ข้ามขั้นตอนนี้ (ไม่กรอกช่วงรหัสกะงาน)
            string orgFrom, string orgTo,
            string empGroupFrom, string empGroupTo,
            string[] empTypeChecks,
            string positionFrom, string positionTo,
            string levelFrom, string levelTo,
            string empCodeFrom, string empCodeTo,
            bool cancelWorkSchedule, bool cancelWorkHoliday)
        {
            OpenMoreActionWizard(ActionCancelScheduleForGroup);
            FillDateRange(start, end);

            // ช่องช่วงรหัสกะงานอาจยัง disabled จนกว่าจะระบุชนิดการยกเลิกก่อน
            // TestStep กำหนดให้เลือกผลที่คาดหวังก่อนกรอกช่วงข้อมูล
            SetCancelOptions(cancelWorkSchedule, cancelWorkHoliday);

            if (workGroupFrom != null && workGroupTo != null)
                SelectRange(IdRangeShiftCode, workGroupFrom, workGroupTo);   // ช่วงรหัสกะงาน
            SelectRange(IdRangeOrgUnit, orgFrom, orgTo);                     // ช่วงหน่วยงาน
            SelectRange(IdRangeEmpGroup, empGroupFrom, empGroupTo);          // ช่วงกลุ่มพนักงาน
            SelectEmployeeTypeDropdown(empTypeChecks);                       // ประเภทพนักงาน
            SelectRange(IdRangePosition, positionFrom, positionTo);          // ช่วงตำแหน่งงาน
            SelectRange(IdRangeEmpLevel, levelFrom, levelTo);                // ช่วงระดับพนักงาน
            SelectRange(IdRangeEmployee, empCodeFrom, empCodeTo);            // ช่วงรหัสพนักงาน

            // ตั้งผลที่คาดหวังอีกครั้งเป็นขั้นสุดท้ายก่อน Next (popup/dropdown ก่อนหน้าอาจทำให้ติ๊กหลุด)
            SetCancelOptions(cancelWorkSchedule, cancelWorkHoliday);

            // ถ้า HRMI ยังเตือน "กรุณากำหนด การยกเลิก อย่างใดอย่างหนึ่ง" ตอนกด Next: กด OK แล้วติ๊กใหม่ ลองได้อีก 2 รอบ
            for (int attempt = 0; ; attempt++)
            {
                ClickWizardNavButton(NextButtonName);
                DismissCancelOptionWarning();
                if (!IsStillOnCancelOptionsPage()) break;
                if (attempt >= 2) throw new Exception("HRMI เตือน \"กรุณากำหนด การยกเลิก อย่างใดอย่างหนึ่ง\" ซ้ำ — ติ๊กช่องยกเลิกไม่ติด");
                SetCancelOptions(cancelWorkSchedule, cancelWorkHoliday);
            }

            CompleteWizard();
        }

        // ── Flow ย่อยที่ใช้ซ้ำในหลายเคสของหน้าจอนี้ ──

        /* เปิดวิซาร์ดจาก More Action */
        private void OpenMoreActionWizard(string actionName)
        {
            Exception? last = null;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    DismissErrorDialogIfPresent();
                    WaitForHrmiIdle(30000);
                    ClickMoreAction();
                    ClickMoreActionItem(actionName);

                    // ตรวจว่าวิซาร์ดเปิดจริง (มีช่องวันที่เริ่มต้น) ก่อนไปขั้นถัดไป
                    if (RetryFind(() => FindById(_mainWindow, IdDateStart), timeoutMs: 8000, pollMs: 300) != null)
                    {
                        LogStep($"เปิดรายการ {actionName} แล้ว");
                        return;
                    }
                    last = new Exception($"กด '{actionName}' แล้ววิซาร์ดไม่เปิด (ครั้งที่ {attempt}); หน้าต่าง HRMI ที่พบ: {DescribeHrmiWindows()}");
                }
                catch (Exception ex) { last = ex; }

                // ปิด dropdown ของ More Action ที่อาจค้างอยู่ แล้วลองใหม่
                try { Keyboard.Press(VirtualKeyShort.ESCAPE); Keyboard.Release(VirtualKeyShort.ESCAPE); } catch { }
                Thread.Sleep(500);
            }
            throw last!;
        }

        private void FillDateRange(DateTime start, DateTime end)
        {
            FillDateField(_mainWindow, IdDateStart, start, useBuddhistYear: true);
            FillDateField(_mainWindow, IdDateEnd, end, useBuddhistYear: true);
            LogStep($"กำหนดช่วงวันที่ {start:dd/MM/yyyy} ถึง {end:dd/MM/yyyy} แล้ว");
        }

        /* แสดงข้อมูลพนักงาน → รีเช็ค → Next ตามจำนวนใน TestStep → Finish (รอพิเศษตอนคลิก Next ครั้งแรก) */
        private void ShowEmployeesAndFinish(bool useConditionsTab, int nextClicksAfterShow)
        {
            if (useConditionsTab) EnsureConditionsTabActive(); // TestStep: กดปุ่มแสดงข้อมูลพนักงานอย่างเดียว (ไม่ติ๊กเงื่อนไข)
            ClickWizardButton(IdBtnShowEmployeeData, fallbackName: "แสดงข้อมูลพนักงาน");
            LogStep("แสดงรายการพนักงานแล้ว");
            ClickRecheckEmployeeDataIfPresent();
            CompleteWizardExact(nextClicksAfterShow, firstNextClickExtraWaitMs: FirstNextExtraWaitMs);
        }

        private void ClickMoreAction()
        {
            RetryAction<object?>(() =>
            {
                var menuItem = _mainWindow.FindFirstDescendant(cf =>
                        cf.ByName(MoreActionName).And(cf.ByControlType(ControlType.MenuItem)))
                    ?? _mainWindow.FindFirstDescendant(cf => cf.ByName(MoreActionName))
                    ?? throw new Exception($"หาเมนู '{MoreActionName}' ไม่เจอ");
                WaitUntilEnabled(menuItem, timeoutMs: 2000);
                menuItem.Click();
                return null;
            }, timeoutMs: 5000, pollMs: 300);
            Thread.Sleep(WizardStepDelayMs);
        }

        private void ClickMoreActionItem(string actionName)
        {
            RetryAction<object?>(() =>
            {
                var item = _mainWindow.FindFirstDescendant(cf =>
                        cf.ByName(actionName).And(cf.ByControlType(ControlType.Button)))
                    ?? FindFirstInHrmi(cf =>
                        cf.ByName(actionName).And(cf.ByControlType(ControlType.Button)))
                    ?? throw new Exception($"หาปุ่มเมนู '{actionName}' ใน More Action ไม่เจอ");

                WaitUntilEnabled(item, timeoutMs: 2000);
                ClickElement(item);
                return null;
            }, timeoutMs: 5000, pollMs: 300);
            Thread.Sleep(WizardStepDelayMs);
        }

        private void EnsureConditionsTabActive()
        {
            try
            {
                var tab = _mainWindow.FindFirstDescendant(cf =>
                        cf.ByName(ConditionsTabName).And(cf.ByControlType(ControlType.TabItem)))
                    ?? _mainWindow.FindFirstDescendant(cf => cf.ByName(ConditionsTabName));

                if (tab == null) return;

                bool alreadySelected = false;
                try
                {
                    var tabPattern = tab.Patterns.SelectionItem;
                    if (tabPattern.IsSupported)
                        alreadySelected = tabPattern.Pattern.IsSelected;
                }
                catch { }

                if (!alreadySelected)
                {
                    ClickElement(tab);
                    Thread.Sleep(WizardStepDelayMs);
                }
            }
            catch { }
        }

        /* คลิกปุ่ม "รีเช็คข้อมูลพนักงาน" (ถ้ามี) เพื่อบังคับให้แอป sync dataset รายชื่อพนักงานก่อนกด Next
           ไม่ throw ถ้าไม่เจอปุ่ม เพราะบางหน้าจอ/บางเวอร์ชันอาจไม่มี */
        private void ClickRecheckEmployeeDataIfPresent()
        {
            try
            {
                var btn = _mainWindow.FindFirstDescendant(cf => cf.ByName(BtnRecheckEmployeeDataName))
                    ?? FindFirstInHrmi(cf => cf.ByName(BtnRecheckEmployeeDataName));

                if (btn == null)
                {
                    DiagLog("ClickRecheckEmployeeDataIfPresent: ไม่เจอปุ่ม 'รีเช็คข้อมูลพนักงาน' -> ข้าม");
                    Thread.Sleep(300);
                    return;
                }

                WaitUntilEnabled(btn, timeoutMs: 1500);
                ClickButtonRobust(btn);
                Thread.Sleep(300);
            }
            catch (Exception ex)
            {
                DiagLog($"ClickRecheckEmployeeDataIfPresent error: {DescribeException(ex)}");
                Thread.Sleep(300);
            }
        }

        private void SetHolidayDays(params string[] dayIdsToCheck)
        {
            var keep = new HashSet<string>(dayIdsToCheck, StringComparer.OrdinalIgnoreCase);
            foreach (var dayId in AllHolidayDayCheckboxIds)
                SetCheckboxState(dayId, keep.Contains(dayId));
            LogStep("กำหนดหัวข้อวันหยุดและเลือกวันเสาร์-อาทิตย์แล้ว");
        }

        /* คลิกเลือกแถวพนักงานในตารางหลักของหน้าจอ (หาเซลล์ที่ค่าตรงกับรหัส ไม่ใช่ช่องค้นหา) */
        private void SelectMainGridRow(string code)
        {
            var cell = RetryFind(() =>
                    _mainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem))
                        .FirstOrDefault(el =>
                        {
                            try
                            {
                                string v = el.Patterns.Value.IsSupported ? el.Patterns.Value.Pattern.Value.Value ?? "" : "";
                                return string.Equals(v.Trim(), code, StringComparison.OrdinalIgnoreCase)
                                       || string.Equals(el.Name?.Trim(), code, StringComparison.OrdinalIgnoreCase);
                            }
                            catch { return false; }
                        }),
                    timeoutMs: 6000)
                ?? throw new Exception($"หาแถวพนักงาน '{code}' ในตารางหลักไม่เจอ");

            ClickElement(cell);
            Thread.Sleep(300);
            LogStep($"เลือกพนักงาน {code} จากตารางแล้ว");
        }

        // ── Lookup Popup ของหน้าจอนี้ (Employee / Work Schedule Pattern / Holiday Calendar) ──
        private void SelectEmployeeByCode(string employeeCode) =>
            SelectFromPicker(IdBtnEmployee, employeeCode, "Dialog เลือกพนักงาน");

        private void SelectHolidayCalendar(string calendarCode) =>
            SelectFromPicker(IdBtnHolidaySche, calendarCode, "Dialog ปฏิทินวันหยุด");

        private void SelectWorkSchedulePattern(string patternCode) =>
            SelectFromPicker(IdBtnWorkSchedulePattern, patternCode, "Dialog เลือกรูปแบบตารางการทำงาน");

        // ── ตัวเลือกยกเลิก (TestCase04) ──
        private void SetCancelOptions(bool cancelWorkSchedule, bool cancelWorkHoliday)
        {
            // ตั้งทั้ง 2 ช่องให้ตรงชัดเจน (ช่องที่ไม่ต้องการ = เอาติ๊กออก) เผื่อวิซาร์ดจำค่าจากรอบก่อน
            SetCheckbox(IdChkCancelSchedule, cancelWorkSchedule, fallbackName: "ยกเลิกกะงาน");
            SetCheckbox(IdChkCancelWorkHoliday, cancelWorkHoliday, fallbackName: "ยกเลิกวันหยุดกะ");
            LogStep($"กำหนดตัวเลือกยกเลิกกะงาน={cancelWorkSchedule}, ยกเลิกวันหยุดกะ={cancelWorkHoliday} แล้ว");
        }

        /* ยังอยู่หน้าแรกของวิซาร์ดยกเลิก (ช่องติ๊กยกเลิกยังมองเห็น) = กด Next แล้วไม่ผ่าน */
        private bool IsStillOnCancelOptionsPage()
        {
            try
            {
                var chk = FindById(_mainWindow, IdChkCancelWorkHoliday) ?? FindFirstInHrmi(cf => cf.ByAutomationId(IdChkCancelWorkHoliday));
                return chk != null && !chk.IsOffscreen && !chk.BoundingRectangle.IsEmpty;
            }
            catch { return false; }
        }

        /* ถ้ามีกล่อง "กรุณากำหนด การยกเลิก" ของ HRMI ให้กด OK แล้วคืน true */
        private bool DismissCancelOptionWarning()
        {
            foreach (var w in FindHrmiWindows())
            {
                try
                {
                    if (w.FindFirstDescendant(cf => cf.ByName(NextButtonName)) != null) continue; // ตัววิซาร์ด
                    bool isWarning = w.FindAllDescendants().Any(e => { try { return (e.Name ?? "").Contains("กรุณากำหนด การยกเลิก"); } catch { return false; } });
                    if (!isWarning) continue;
                    if (!ClickDialogButton(w, "OK", 1000)) ClickDialogButton(w, "&OK", 500);
                    Thread.Sleep(300);
                    return true;
                }
                catch { }
            }
            return false;
        }

        // ── RangeManager (จาก-ถึง) ──

        /// <summary>
        /// เลือกค่า "จาก-ถึง" ใน RangeManager ตาม TestStep: กด "..." ของช่อง → เลือกแถวรหัสใน Pop-up → Select
        /// </summary>
        private void SelectRange(string rangeAutomationId, string fromValue, string toValue)
        {
            PickRangeValue(rangeAutomationId, IdRangeBegin, fromValue, useLastRowIfMissing: false);
            PickRangeValue(rangeAutomationId, IdRangeEnd, toValue, useLastRowIfMissing: true);
            LogStep($"กำหนดช่วง {rangeAutomationId}: {fromValue} ถึง {toValue} แล้ว");
        }

        private void PickRangeValue(string rangeAutomationId, string buttonEditId, string code, bool useLastRowIfMissing)
        {
            string label = $"{rangeAutomationId}.{buttonEditId}";
            var range = RetryFind(() => FindById(_mainWindow, rangeAutomationId), timeoutMs: 5000, pollMs: 250)
                ?? throw new Exception($"หาช่วงข้อมูล '{rangeAutomationId}' ไม่เจอ");
            var container = range.FindFirstDescendant(cf => cf.ByAutomationId(buttonEditId))
                ?? throw new Exception($"หาช่อง '{buttonEditId}' ใน '{rangeAutomationId}' ไม่เจอ");
            WaitUntilEnabled(container, timeoutMs: 3000);

            // จำหน้าต่าง HRMI ก่อนคลิก เพื่อระบุ popup ที่ถูกเปิดจากปุ่มนี้โดยตรง
            var windowsBeforeClick = FindHrmiWindows()
                .Select(w => { try { return w.Properties.NativeWindowHandle.ValueOrDefault; } catch { return IntPtr.Zero; } })
                .Where(h => h != IntPtr.Zero)
                .ToHashSet();

            var dotsButton = container.FindAllDescendants(cf => cf.ByControlType(ControlType.Button)).LastOrDefault();
            if (dotsButton != null) ClickElement(dotsButton);
            else
            {
                var r = container.BoundingRectangle;
                Mouse.Click(new System.Drawing.Point((int)r.Right - 9, (int)(r.Top + r.Height / 2)));
            }

            // ถ้าปุ่มลูกไม่เปิด popup ให้ลองคลิกตำแหน่งปุ่ม ... จริงที่ขอบขวาของ ButtonEdit
            AutomationElement? dialog = RetryFind(() => FindHrmiPickerDialog(windowsBeforeClick), timeoutMs: 2500, pollMs: 200);
            if (dialog == null)
            {
                var r = container.BoundingRectangle;
                Mouse.MoveTo(new System.Drawing.Point((int)r.Right - 9, (int)(r.Top + r.Height / 2)));
                Thread.Sleep(80);
                Mouse.Click(MouseButton.Left);
                dialog = RetryFind(() => FindHrmiPickerDialog(windowsBeforeClick), timeoutMs: 6000, pollMs: 200);
            }
            dialog ??= RetryFind(() => FindHrmiPickerDialog(), timeoutMs: 1000, pollMs: 200);
            if (dialog == null)
                throw new Exception($"กด '...' ของ {label} แล้วไม่พบ Pop-up เลือกข้อมูล; หน้าต่าง HRMI ที่พบ: {DescribeHrmiWindows()}");

            // บาง RangeManager เปิดหน้าต่างก่อนแล้วค่อย bind ตาราง: รอเซลล์ที่มีค่าจริงก่อนเลือกแถว
            var loadedCell = RetryFind(() => dialog.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem))
                .FirstOrDefault(c => { try { return c.Patterns.Value.IsSupported && !string.IsNullOrWhiteSpace(c.Patterns.Value.Pattern.Value.Value); } catch { return false; } }),
                timeoutMs: 15000, pollMs: 250);
            if (loadedCell == null)
                throw new Exception($"Pop-up ของ {label} เปิดแล้ว แต่ตารางไม่มีข้อมูลหลังรอ 15 วินาที");

            var row = FindRowByScanningVisibleElements(dialog, code);
            if (row == null)
            {
                // ไม่พบรหัส: ใช้แถวแรก (ช่อง "จาก") / แถวสุดท้าย (ช่อง "ถึง") ให้ช่วงครอบคลุมข้อมูลทั้งหมด
                row = FindEdgeRow(dialog, useLastRowIfMissing);
                if (row == null) throw new Exception($"ไม่พบรหัส '{code}' และ Pop-up ของ {label} ไม่มีข้อมูล");
            }
            SelectRowAndConfirm(dialog, row, $"Pop-up {label}");

            // กด Select ถ้ายังไม่ปิด
            if (RetryFind(() => FindHrmiPickerDialog(), timeoutMs: 600, pollMs: 200) is AutomationElement still)
            {
                var select = still.FindFirstDescendant(cf => cf.ByName("Select").And(cf.ByControlType(ControlType.Button)))
                             ?? still.FindFirstDescendant(cf => cf.ByName("Select"));
                if (select != null) ClickButtonRobust(select);
                if (RetryFind(() => FindHrmiPickerDialog(), timeoutMs: 2000, pollMs: 200) != null)
                    throw new Exception($"เลือกแถวแล้ว Pop-up ของ {label} ยังไม่ปิด");
            }
        }

        /* แถวแรก/แถวสุดท้ายของคอลัมน์แรกในตาราง Pop-up (เซลล์ชื่อ "<หัวคอลัมน์> row N") */
        private static AutomationElement? FindEdgeRow(AutomationElement dialog, bool last)
        {
            var rx = new System.Text.RegularExpressions.Regex(@"^(.*) row (\d+)$");
            var cells = dialog.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem))
                .Select(c => { string n = ""; try { n = c.Name ?? ""; } catch { } var m = rx.Match(n); return (c, m); })
                .Where(x => x.m.Success).ToList();
            if (cells.Count == 0) return null;
            string firstCol = cells[0].m.Groups[1].Value;
            var col = cells.Where(x => x.m.Groups[1].Value == firstCol).OrderBy(x => int.Parse(x.m.Groups[2].Value)).ToList();
            return (last ? col.Last() : col.First()).c;
        }

        // ── ประเภทพนักงาน (CheckComboEdit) ──

        /// <summary>
        /// เปิด dropdown "ประเภทพนักงาน" แล้วตั้งค่าให้ติ๊ก "เฉพาะ" รายการที่ระบุ
        /// (เคส 1: รายชั่วโมง+รายเหมา, เคส 2: รายวัน+รายเดือน) — ค่าเริ่มต้นติ๊กไว้ครบ 4 ประเภท
        /// </summary>
        private void SelectEmployeeTypeDropdown(string[] itemNames)
        {
            // ลำดับรายการใน dropdown ตามหน้าจอจริง
            string[] allTypes = { IdChkEmpTypeHourly, IdChkEmpTypeContract, IdChkEmpTypeDaily, IdChkEmpTypeMonthly };
            var wanted = new HashSet<string>(itemNames);

            AutomationElement? edit = null;
            RetryAction<object?>(() =>
            {
                var combo = FindById(_mainWindow, IdChkComboEmpType)
                    ?? throw new Exception($"หา dropdown ประเภทพนักงาน '{IdChkComboEmpType}' ไม่เจอ");
                WaitUntilEnabled(combo, timeoutMs: 2000);
                edit = combo.FindFirstDescendant(cf => cf.ByAutomationId(IdChkComboEmpTypeEdit)) ?? combo;
                var openBtn = edit.FindAllDescendants(cf => cf.ByControlType(ControlType.Button)).LastOrDefault();
                if (openBtn != null) ClickElement(openBtn);
                else
                {
                    var r = edit.BoundingRectangle; // ลูกศร ▼ อยู่ขอบขวาของช่อง
                    Mouse.Click(new System.Drawing.Point((int)r.Right - 8, (int)(r.Top + r.Height / 2)));
                }
                return null;
            }, timeoutMs: 5000, pollMs: 300);
            Thread.Sleep(WizardStepDelayMs);

            var items = FindEmployeeTypeItems(allTypes);
            if (items.Count == allTypes.Length)
            {
                foreach (var name in allTypes)
                    SetListItemChecked(items[name], wanted.Contains(name));
            }
            else
            {
                // หารายการด้วย UIA ไม่เจอ (DevExpress ไม่เปิดเผยรายการ) → คลิกช่องติ๊กตามตำแหน่งแถวใต้ช่อง dropdown
                // แถวแรกต่ำกว่ากึ่งกลางช่อง 23px, สูงแถวละ 17px, ช่องติ๊กห่างขอบซ้ายช่อง 12px
                // คลิกเฉพาะรายการที่ "ไม่" ต้องการ เพื่อเอาติ๊กออก
                var r = edit!.BoundingRectangle;
                int x = (int)r.Left + 12, y0 = (int)(r.Top + r.Height / 2) + 23;
                for (int i = 0; i < allTypes.Length; i++)
                {
                    if (wanted.Contains(allTypes[i])) continue;
                    Mouse.Click(new System.Drawing.Point(x, y0 + i * 17));
                    Thread.Sleep(200);
                }
            }

            // ปิด dropdown ด้วยการคลิกช่องเดิมซ้ำ (ESC อาจยกเลิกค่าที่ติ๊กไว้)
            try { var r = edit!.BoundingRectangle; Mouse.Click(new System.Drawing.Point((int)r.Left + 20, (int)(r.Top + r.Height / 2))); } catch { }
            Thread.Sleep(WizardStepDelayMs);

            // ตรวจผลจากข้อความในช่อง — ช่องแสดงเฉพาะ "รายการแรกที่ติ๊ก" จึงตรวจได้แค่ว่า
            // รายการแรกตรงกับที่ต้องการ และไม่มีรายการที่ไม่ต้องการโผล่
            string shown = "";
            try
            {
                var inner = edit!.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)) ?? edit;
                shown = inner.Patterns.Value.IsSupported ? inner.Patterns.Value.Pattern.Value.Value ?? "" : "";
            }
            catch { }
            if (shown.Length > 0)
            {
                string expectedFirst = allTypes.First(wanted.Contains);
                bool ok = shown.StartsWith(expectedFirst)
                          && !allTypes.Any(n => !wanted.Contains(n) && shown.Contains(n));
                if (!ok)
                    throw new Exception($"ตั้งค่าประเภทพนักงานไม่ตรง: ต้องการ [{string.Join(", ", itemNames)}] แต่ช่องแสดง '{shown}'");
            }
            LogStep("เลือกประเภทพนักงานตามกรณีทดสอบแล้ว");
        }

        /* หารายการใน dropdown ประเภทพนักงาน (อยู่ในหน้าต่าง popup ของ HRMI) — คืนเฉพาะที่เจอ */
        private Dictionary<string, AutomationElement> FindEmployeeTypeItems(string[] names)
        {
            var found = new Dictionary<string, AutomationElement>();
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 3000 && found.Count < names.Length)
            {
                foreach (var w in FindHrmiWindows())
                {
                    try
                    {
                        foreach (var el in w.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem)
                                     .Or(cf.ByControlType(ControlType.CheckBox)).Or(cf.ByControlType(ControlType.DataItem))))
                        {
                            string n = (el.Name ?? "").Trim();
                            var match = names.FirstOrDefault(x => n == x);
                            if (match != null && !found.ContainsKey(match)) found[match] = el;
                        }
                    }
                    catch { }
                }
                if (found.Count < names.Length) Thread.Sleep(200);
            }
            return found;
        }

        private static void SetListItemChecked(AutomationElement item, bool shouldCheck)
        {
            bool? Read()
            {
                try { if (item.Patterns.Toggle.IsSupported) return item.Patterns.Toggle.Pattern.ToggleState.Value == ToggleState.On; } catch { }
                try { if (item.Patterns.SelectionItem.IsSupported) return item.Patterns.SelectionItem.Pattern.IsSelected.Value; } catch { }
                return null;
            }

            var state = Read();
            if (state == shouldCheck) return;

            // คลิกที่ช่องติ๊กด้านซ้ายของรายการ
            var r = item.BoundingRectangle;
            Mouse.Click(new System.Drawing.Point((int)r.Left + 10, (int)(r.Top + r.Height / 2)));
            Thread.Sleep(200);
            if (Read() is bool now && now != shouldCheck) Keyboard.Type(VirtualKeyShort.SPACE);
        }

        // ── Scenario Runner ของหน้าจอนี้ (เทียบเท่า RunStep ของหน้า Bank) ──

        private void RunScenarioStep(string testName, Action step)
        {
            try
            {
                step();
                LogSuccess(testName, "ทำรายการตามขั้นตอนที่กำหนดสำเร็จครบถ้วน");
            }
            catch (Exception ex)
            {
                LogFailure(testName, $"ไม่สำเร็จ: {DescribeException(ex)} | หน้าต่าง HRMI ตอนล้ม: {DescribeHrmiWindows()}");
            }
            finally
            {
                CloseAnyOpenWizardDialog();
            }
        }

        private void RunScenarioStep(string testName, Action step, int progress, string progressMessage)
        {
            RunScenarioStep(testName, step);
            TestProgressHelper.Report(progress, progressMessage);
        }

        /* ปิดวิซาร์ดที่ค้างหลังจบเคส — กดเฉพาะปุ่มของหน้าต่าง HRMI ที่เป็นวิซาร์ดจริง
           (เดิมหาปุ่ม "Cancel" ที่ไหนก็ได้บน desktop เคยไปโดนปุ่มที่ไม่ใช่วิซาร์ด ทำให้ HRMI ค้างทั้งเคสถัดไป) */
        private void CloseAnyOpenWizardDialog()
        {
            try
            {
                DismissErrorDialogIfPresent();
                WaitForHrmiIdle();

                var wizard = FindHrmiWindows().FirstOrDefault(w =>
                    w.FindFirstDescendant(cf => cf.ByName(NextButtonName).Or(cf.ByName(FinishButtonName)).Or(cf.ByName("< Back"))) != null);
                if (wizard == null) return;

                if (!ClickDialogButton(wizard, "Cancel", waitEnabledMs: 500))
                {
                    // หน้าสรุปผล ("บันทึกข้อมูลเสร็จสิ้น") มีแต่ปุ่ม Finish ไม่มี Cancel
                    if (!ClickDialogButton(wizard, FinishButtonName, waitEnabledMs: 500)) return;
                }
                Thread.Sleep(PageLoadDelayMs);

                // หลังกด Cancel อาจมีกล่องยืนยันของ HRMI โผล่มา
                foreach (var box in FindHrmiWindows())
                {
                    if (ClickDialogButton(box, "Yes", waitEnabledMs: 500) || ClickDialogButton(box, "OK", waitEnabledMs: 500)) break;
                }

                // ถ้าต้องกด Finish เพื่อปิด HRMI จะประมวลผลต่อ — รอให้เสร็จก่อนเริ่มเคสถัดไป (กัน UIA timeout)
                WaitForHrmiIdle(120000);
            }
            catch (Exception ex)
            {
                DiagLog($"CloseAnyOpenWizardDialog error: {DescribeException(ex)}");
            }
        }

        // เก็บ output ให้กระชับ: แต่ละเคส log แค่ PASS/FAIL สุดท้าย
        private void LogStep(string message) { }


        // =========================================================================================
        // หมวดที่ 3: ส่วนการทำงานกลางที่ตายตัว (Common / Reusable Helpers)
        // **ใช้งานร่วมกันได้ทุกหน้าจอ ไม่ต้องแก้ไขคำสั่งเมื่อเปลี่ยนหน้า**
        // =========================================================================================

        private readonly AutomationElement _mainWindow;
        private readonly UIA3Automation _automation;
        private int _totalSuccess = 0;
        private int _totalFail = 0;

        public EmpWorkScheListUC(AutomationElement mainWindow, UIA3Automation automation)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

        // ── 3.1 HRMI Window Helpers ──

        /* หา control ตาม AutomationId: ค้นในวิซาร์ด/Pop-up ของ HRMI ก่อน (เร็ว) แล้วค่อยค้นใน context (หน้าหลักทั้งหน้า ช้า) */
        private AutomationElement? FindById(AutomationElement context, string automationId) =>
            (ReferenceEquals(context, _mainWindow) ? FindFirstInHrmi(cf => cf.ByAutomationId(automationId)) : null)
            ?? context.FindFirstDescendant(cf => cf.ByAutomationId(automationId));

        /* หาปุ่มนำทางของวิซาร์ด (Next/Finish): ค้นในหน้าต่างวิซาร์ดก่อน (เล็ก เร็ว) แล้วค่อยค้นทั้งหน้าหลัก */
        private AutomationElement? FindNavButton(string name) =>
            FindFirstInHrmi(cf => cf.ByName(name).And(cf.ByControlType(ControlType.Button)))
            ?? _mainWindow.FindFirstDescendant(cf => cf.ByName(name).And(cf.ByControlType(ControlType.Button)));

        /* หา element ในหน้าต่างของ HRMI เท่านั้น (แทนการค้นทั้ง desktop ซึ่งช้าและอาจเจอปุ่มชื่อเดียวกันของโปรแกรมอื่น) */
        private AutomationElement? FindFirstInHrmi(Func<FlaUI.Core.Conditions.ConditionFactory, FlaUI.Core.Conditions.ConditionBase> condition)
        {
            foreach (var w in FindHrmiWindows())
            {
                try { var el = w.FindFirstDescendant(condition); if (el != null) return el; } catch { }
            }
            return null;
        }

        /* หน้าต่าง popup ของ HRMI (วิซาร์ด / Pop-up เลือกข้อมูล / กล่องข้อความ / หน้าต่างประมวลผล / dropdown)
           ไม่รวมหน้าหลัก และไม่รวมหน้าจอที่เปิดเป็นแท็บใน HRMI (MDI) — แท็บมีตารางเหมือน Pop-up
           ถ้านับรวม เทสจะคิดว่า Pop-up ยังเปิดอยู่ตลอด */
        private IEnumerable<AutomationElement> FindHrmiWindows()
        {
            int pid = _mainWindow.Properties.ProcessId.ValueOrDefault;
            var mainHandle = _mainWindow.Properties.NativeWindowHandle.ValueOrDefault;

            static bool IsModal(AutomationElement w)
            {
                try { return w.Patterns.Window.IsSupported && w.Patterns.Window.Pattern.IsModal.ValueOrDefault; }
                catch { return false; }
            }
            // หน้าจอแท็บ (MDI) มี AutomationId เป็นตัวเลข ("1", "2") ส่วนวิซาร์ด/Pop-up ไม่มี — ใช้แยกโดยไม่ต้องค้นข้างใน
            static bool IsPopupLike(AutomationElement w)
            {
                try { return IsModal(w) || string.IsNullOrEmpty(w.AutomationId) || !w.AutomationId.All(char.IsDigit); }
                catch { return false; }
            }

            // หน้าต่างระดับบนของ HRMI + หน้าต่าง modal ที่อยู่ใต้หน้าหลัก (Pop-up อาจซ้อนใต้วิซาร์ดอีกชั้น)
            return _automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(pid))
                .Concat(_mainWindow.FindAllChildren(cf => cf.ByControlType(ControlType.Window)).Where(IsPopupLike)
                    .SelectMany(w => new[] { w }.Concat(SafeChildWindows(w))))
                .Where(w => { try { return w.Properties.NativeWindowHandle.ValueOrDefault != mainHandle; } catch { return false; } })
                .ToList();
        }

        private static IEnumerable<AutomationElement> SafeChildWindows(AutomationElement w)
        {
            try { return w.FindAllChildren(cf => cf.ByControlType(ControlType.Window)); }
            catch { return Array.Empty<AutomationElement>(); }
        }

        private string DescribeHrmiWindows()
        {
            try
            {
                var descriptions = FindHrmiWindows().Take(10).Select(w =>
                {
                    string name = "";
                    string id = "";
                    string handle = "";
                    try { name = w.Name ?? ""; } catch { }
                    try { id = w.AutomationId ?? ""; } catch { }
                    try { handle = w.Properties.NativeWindowHandle.ValueOrDefault.ToString(); } catch { }
                    return $"Name='{name}', AutomationId='{id}', HWND={handle}";
                });
                string result = string.Join(" | ", descriptions);
                return string.IsNullOrWhiteSpace(result) ? "(ไม่พบหน้าต่างย่อยของ HRMI)" : result;
            }
            catch (Exception ex) { return $"(อ่านหน้าต่าง HRMI ไม่สำเร็จ: {ex.Message})"; }
        }

        /* หลังกด Finish HRMI ประมวลผลต่อ (มีหน้าต่างประมวลผล) — รอให้เสร็จก่อนเริ่มเคสถัดไป */
        private void WaitForHrmiIdle(int timeoutMs = 180000)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try
                {
                    bool busy = FindHrmiWindows().Any(w =>
                        w.FindFirstDescendant(cf => cf.ByName(NextButtonName).Or(cf.ByName(FinishButtonName))) == null
                        && w.FindFirstDescendant(cf => cf.ByControlType(ControlType.ProgressBar)) != null);
                    if (!busy) return;
                }
                catch { }
                Thread.Sleep(300);
            }
        }

        // ── 3.2 Click Helpers (ไม่พึ่ง keyboard focus — รองรับ control DevExpress ที่ UIA3 รายงาน "Not Focusable") ──

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int BM_CLICK = 0x00F5;

        /* เรียก Invoke / DoDefaultAction แบบไม่รอผล: ถ้าปุ่มเปิดหน้าต่าง modal (วิซาร์ด, Pop-up "...", Open file)
           คำสั่งพวกนี้จะไม่ return จนกว่าหน้าต่างจะปิด → เทสค้าง
           รอแค่ waitMs ถ้ายังไม่จบถือว่ากดติดแล้ว ถ้า throw ทันที = กดไม่ติด */
        private static bool RunWithoutBlocking(Action action, int waitMs = 1500)
        {
            var task = Task.Run(action);
            try { task.Wait(waitMs); return true; }
            catch { return false; }
        }

        /* คลิกด้วยเมาส์จริงที่กึ่งกลาง element (ไม่ผ่าน UIA/COM จึงไม่ค้างแม้ปุ่มจะเปิดหน้าต่าง modal) */
        private static bool TryMouseClick(AutomationElement element)
        {
            try
            {
                // ไม่เช็ค IsOffscreen: เมนูใน More Action ของ DevExpress รายงาน IsOffscreen=True ทั้งที่มองเห็นอยู่
                var rect = element.BoundingRectangle;
                if (rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0) return false;
                Mouse.MoveTo(rect.Center());
                Thread.Sleep(80);
                Mouse.Click(MouseButton.Left);
                return true;
            }
            catch { return false; }
        }

        /* ส่ง BM_CLICK ตรงไปยัง native window handle (ระดับ HWND ไม่ผ่าน focus)
           ใช้ Post ไม่รอ: ถ้าปุ่มเปิดหน้าต่าง modal, SendMessage จะค้างจนหน้าต่างปิด */
        private static bool TryInvokeViaWinApi(AutomationElement element)
        {
            try
            {
                var handleProp = element.Properties.NativeWindowHandle;
                if (!handleProp.IsSupported) return false;

                IntPtr handle = handleProp.ValueOrDefault;
                if (handle == IntPtr.Zero) return false;

                PostMessage(handle, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                return true;
            }
            catch { return false; }
        }

        /* LegacyIAccessiblePattern.DoDefaultAction() (MSAA) — ไม่ต้องโฟกัส element */
        private static bool TryInvokeViaLegacyPattern(AutomationElement element)
        {
            try
            {
                if (!element.Patterns.LegacyIAccessible.IsSupported) return false;
                return RunWithoutBlocking(() => element.Patterns.LegacyIAccessible.Pattern.DoDefaultAction());
            }
            catch { return false; }
        }

        /* ลำดับการคลิก: เมาส์จริง → PostMessage → (ท้ายสุด) pattern แบบไม่รอผล
           ห้ามเริ่มด้วย Invoke / DoDefaultAction: ถ้าปุ่มเปิดหน้าต่าง modal คำสั่งจะค้างใน HRMI จนหน้าต่างปิด
           และทำให้คำสั่ง UIA ถัดไปทุกคำสั่ง timeout (อาการ "ค้างหน้าวิซาร์ด" + COMException 0x80131505) */
        private static void ClickElement(AutomationElement element)
        {
            bool isToggleOrRadio = false;
            try { isToggleOrRadio = !element.Patterns.Invoke.IsSupported && (element.Patterns.Toggle.IsSupported || element.Patterns.SelectionItem.IsSupported); } catch { }

            // checkbox / radio ไม่เปิดหน้าต่าง ใช้ pattern ได้ตามเดิม (แม่นกว่าคลิกพิกัด)
            if (isToggleOrRadio)
            {
                try
                {
                    if (element.Patterns.Toggle.IsSupported && RunWithoutBlocking(() => element.Patterns.Toggle.Pattern.Toggle())) return;
                    if (element.Patterns.SelectionItem.IsSupported && RunWithoutBlocking(() => element.Patterns.SelectionItem.Pattern.Select())) return;
                }
                catch { }
            }

            if (TryMouseClick(element)) return;
            if (TryInvokeViaWinApi(element)) return;
            try { if (element.Patterns.Invoke.IsSupported && RunWithoutBlocking(() => element.Patterns.Invoke.Pattern.Invoke())) return; } catch { }
            TryInvokeViaLegacyPattern(element);
        }

        /* เวอร์ชัน "Robust" สำหรับปุ่มนำทางวิซาร์ด (Next/Finish/แสดงข้อมูลพนักงาน ฯลฯ):
           เมาส์จริง → PostMessage → Invoke (ไม่รอผล) → LegacyIAccessible → element.Click() */
        private static void ClickButtonRobust(AutomationElement element)
        {
            if (TryMouseClick(element)) { Thread.Sleep(50); return; }
            if (TryInvokeViaWinApi(element)) { Thread.Sleep(50); return; }
            try { if (element.Patterns.Invoke.IsSupported && RunWithoutBlocking(() => element.Patterns.Invoke.Pattern.Invoke())) return; } catch { }
            if (TryInvokeViaLegacyPattern(element)) return;
            try { element.Click(); } catch { }
        }

        /* คลิกปุ่มมาตรฐานของ Dialog/Wizard ตามชื่อ (เช่น "Select", "Cancel", "OK", "Yes")
           หาเฉพาะใน context ที่ระบุ (ไม่ถอยไปหาทั้ง desktop: เคยไปกดปุ่มของหน้าต่างอื่น)
           ไม่ throw ถ้าไม่เจอ — คืน false ให้ผู้เรียกลองท่าอื่นต่อ */
        private bool ClickDialogButton(AutomationElement context, string buttonName, int waitEnabledMs = 2000)
        {
            try
            {
                var button = context.FindFirstDescendant(cf =>
                        cf.ByName(buttonName).And(cf.ByControlType(ControlType.Button)));

                DiagLog($"ClickDialogButton('{buttonName}') -> {DescribeElement(button, "found")}");
                if (button == null) return false;

                bool enabledInTime = WaitUntilEnabled(button, timeoutMs: waitEnabledMs);
                DiagLog($"  enabledInTime={enabledInTime} -> {DescribeElement(button, "before-click")}");
                if (!enabledInTime) return false;

                ClickButtonRobust(button);
                Thread.Sleep(150);
                DiagLog($"  หลังคลิก '{buttonName}' -> {DescribeElement(button, "after-click")}");
                return true;
            }
            catch (Exception ex)
            {
                DiagLog($"ClickDialogButton('{buttonName}') error: {DescribeException(ex)}");
                return false;
            }
        }

        /* ท่าสำรองสุดท้ายเมื่อหาปุ่ม Select/OK ด้วยชื่อไม่เจอเลย: คลิกพิกัดสัมพัทธ์จากมุมขวาล่างของ Dialog */
        private void ClickSelectByCoordinateOffset(AutomationElement dialog, int offsetXFromRight = 125, int offsetYFromBottom = 23)
        {
            try
            {
                var bounds = dialog.BoundingRectangle;
                if (bounds.IsEmpty)
                {
                    DiagLog("ClickSelectByCoordinateOffset: dialog.BoundingRectangle ว่างเปล่า -> ข้าม");
                    return;
                }

                var selectPoint = new System.Drawing.Point(bounds.Right - offsetXFromRight, bounds.Bottom - offsetYFromBottom);
                DiagLog($"ClickSelectByCoordinateOffset -> point=({selectPoint.X},{selectPoint.Y}) " +
                        $"dialogRect=[{bounds.X},{bounds.Y},{bounds.Width}x{bounds.Height}]");

                Mouse.MoveTo(selectPoint);
                Thread.Sleep(80);
                Mouse.Click(MouseButton.Left);
                Thread.Sleep(WizardStepDelayMs);
            }
            catch (Exception ex)
            {
                DiagLog($"ClickSelectByCoordinateOffset error: {DescribeException(ex)}");
            }
        }

        private void ClickButtonWithFallback(int timeoutMs, string notFoundMessage, params Func<AutomationElement?>[] finders)
        {
            var sw = Stopwatch.StartNew();
            int attempt = 0;

            RetryAction<object?>(() =>
            {
                attempt++;
                DiagLog($"ClickButtonWithFallback attempt #{attempt} (elapsed {sw.ElapsedMilliseconds}ms) — เป้าหมาย: {notFoundMessage}");

                AutomationElement? button = null;
                int finderIndex = -1;
                for (int i = 0; i < finders.Length; i++)
                {
                    AutomationElement? candidate = null;
                    try { candidate = finders[i](); } catch (Exception ex) { DiagLog($"  finder[{i}] throw: {ex.Message}"); }

                    DiagLog($"  finder[{i}] -> {DescribeElement(candidate, "candidate")}");

                    if (candidate != null)
                    {
                        // ข้าม element ที่ offscreen ไปก่อน เผื่อมีตัวซ้ำที่ยัง visible อยู่จริง
                        bool candidateOffscreen = false;
                        try { candidateOffscreen = candidate.Properties.IsOffscreen.IsSupported && candidate.Properties.IsOffscreen.Value; } catch { }

                        if (!candidateOffscreen)
                        {
                            button = candidate;
                            finderIndex = i;
                            break;
                        }
                        DiagLog($"  finder[{i}] IsOffscreen=true — ข้ามไปลอง finder ถัดไป");
                        button ??= candidate; // เก็บไว้เป็น fallback สุดท้าย
                    }
                }

                if (button == null)
                {
                    DiagLog($"  ไม่เจอปุ่มจาก finder ใดเลย ({finders.Length} finders) -> throw '{notFoundMessage}'");
                    throw new Exception(notFoundMessage);
                }

                DiagLog($"  ใช้ finder[{finderIndex}] -> {DescribeElement(button, "selected")}");

                bool enabledInTime = WaitUntilEnabled(button, timeoutMs: 2500, pollMs: 200);
                DiagLog($"  หลังรอ WaitUntilEnabled: enabledInTime={enabledInTime} -> {DescribeElement(button, "before-click")}");

                ClickButtonRobust(button);
                Thread.Sleep(100);
                DiagLog($"  หลังคลิก -> {DescribeElement(button, "after-click")}");

                return null;
            }, timeoutMs: Math.Max(timeoutMs, WizardButtonRetryTimeoutMs), pollMs: 300);
        }

        // ── 3.3 Input Helpers (Radio / Checkbox / Text / Date / Spin) ──

        private void SelectRadioButton(AutomationElement context, string automationId)
        {
            RetryAction<object?>(() =>
            {
                var container = FindById(context, automationId)
                    ?? throw new Exception($"หา radio button '{automationId}' ไม่เจอ");
                WaitUntilEnabled(container, timeoutMs: 2000);

                var radioElement = container.FindFirstDescendant(cf => cf.ByControlType(ControlType.RadioButton))
                    ?? container;

                ClickElement(radioElement);
                Thread.Sleep(150);

                if (!IsRadioChecked(radioElement))
                    TrySetRadioChecked(radioElement);
                return null;
            }, timeoutMs: 5000, pollMs: 300);
            Thread.Sleep(WizardStepDelayMs);
        }

        private void SelectRadioGroupOption(AutomationElement context, string groupAutomationId, string optionText)
        {
            RetryAction<object?>(() =>
            {
                var group = context.FindFirstDescendant(cf => cf.ByAutomationId(groupAutomationId))
                    ?? throw new Exception($"หากลุ่ม radio '{groupAutomationId}' ไม่เจอ");
                WaitUntilEnabled(group, timeoutMs: 2000);

                var option = group.FindAllDescendants(cf => cf.ByControlType(ControlType.RadioButton))
                        .FirstOrDefault(el => !string.IsNullOrEmpty(el.Name)
                            && el.Name.Contains(optionText, StringComparison.OrdinalIgnoreCase))
                    ?? throw new Exception($"หาตัวเลือก '{optionText}' ใน radio group '{groupAutomationId}' ไม่เจอ");

                if (!TrySetRadioChecked(option))
                    ClickElement(option);
                return null;
            }, timeoutMs: 5000, pollMs: 300);
            Thread.Sleep(WizardStepDelayMs);
        }

        private static bool IsRadioChecked(AutomationElement element)
        {
            try { return element.AsRadioButton()?.IsChecked == true; }
            catch { return false; }
        }

        private static bool TrySetRadioChecked(AutomationElement element)
        {
            try
            {
                var radio = element.AsRadioButton();
                if (radio == null) return false;
                radio.IsChecked = true;
                return true;
            }
            catch { return false; }
        }

        private void SetCheckboxState(string automationId, bool shouldCheck)
        {
            RetryAction<object?>(() =>
            {
                var element = FindById(_mainWindow, automationId);
                if (element == null) return null;

                WaitUntilEnabled(element, timeoutMs: 2000);

                bool current = false;
                try { current = element.AsCheckBox()?.IsChecked == true; } catch { }

                if (current != shouldCheck)
                    ClickElement(element);
                return null;
            }, timeoutMs: 5000, pollMs: 300);
            Thread.Sleep(WizardStepDelayMs);
        }

        /* ติ๊ก checkbox ให้ "ติ๊ก" จริง (CheckEdit ของ DevExpress)
           IsChecked = true (Toggle ผ่าน UIA) ไม่ commit ค่า → ลองทีละวิธีจนสถานะเป็นติ๊ก:
           คลิกเมาส์ที่ช่องติ๊ก → โฟกัสแล้วกด Space → DoDefaultAction
           อ่านสถานะจาก MSAA (LegacyIAccessible) เป็นหลัก เพราะ Toggle ของ DevExpress รายงานค่าไม่ตรง */
        private void ClickCheckbox(string automationId, string? fallbackName = null) => SetCheckbox(automationId, true, fallbackName);

        private void SetCheckbox(string automationId, bool shouldCheck, string? fallbackName = null)
        {
            AutomationElement FindBox() =>
                FindById(_mainWindow, automationId)
                ?? FindFirstInHrmi(cf => cf.ByAutomationId(automationId))
                ?? (fallbackName != null
                    ? (_mainWindow.FindFirstDescendant(cf => cf.ByName(fallbackName)) ?? FindFirstInHrmi(cf => cf.ByName(fallbackName)))
                    : null)
                ?? throw new Exception($"หา checkbox '{automationId}' ไม่เจอ");

            bool? IsChecked(AutomationElement el)
            {
                try
                {
                    if (el.Patterns.LegacyIAccessible.IsSupported)
                        return ((int)el.Patterns.LegacyIAccessible.Pattern.State.Value & 0x10) != 0; // STATE_SYSTEM_CHECKED
                }
                catch { }
                try { if (el.Patterns.Toggle.IsSupported) return el.Patterns.Toggle.Pattern.ToggleState.Value == ToggleState.On; } catch { }
                return null;
            }

            var box = FindBox();
            WaitUntilEnabled(box, timeoutMs: 3000);
            if (IsChecked(box) == shouldCheck) { Thread.Sleep(WizardStepDelayMs); return; }
            if (!shouldCheck && IsChecked(box) == null) return; // อ่านสถานะไม่ได้ — ไม่คลิกเอาติ๊กออก กันไปติ๊กเพิ่มโดยไม่ตั้งใจ

            var attempts = new Action[]
            {
                () => { var r = box.BoundingRectangle; Mouse.Click(new System.Drawing.Point((int)r.Left + 7, (int)(r.Top + r.Height / 2))); },
                () => { try { box.Focus(); } catch { } Thread.Sleep(100); Keyboard.Type(VirtualKeyShort.SPACE); },
                () => { if (box.Patterns.LegacyIAccessible.IsSupported) RunWithoutBlocking(() => box.Patterns.LegacyIAccessible.Pattern.DoDefaultAction()); },
                () => { var r = box.BoundingRectangle; Mouse.Click(new System.Drawing.Point((int)(r.Left + r.Width / 2), (int)(r.Top + r.Height / 2))); },
            };

            foreach (var attempt in attempts)
            {
                try { attempt(); } catch { }
                Thread.Sleep(250);
                box = FindBox();
                var state = IsChecked(box);
                if (state == shouldCheck) { Thread.Sleep(WizardStepDelayMs); return; }
                if (state == null) break; // อ่านสถานะไม่ได้ — คลิกครั้งเดียวพอ กันกดซ้ำจนติ๊กหลุด
            }

            if (IsChecked(box) == !shouldCheck)
                throw new Exception($"{(shouldCheck ? "ติ๊ก" : "เอาติ๊กออก")} checkbox '{fallbackName ?? automationId}' ไม่สำเร็จ (ลองคลิก, Space, DoDefaultAction แล้ว)");
            Thread.Sleep(WizardStepDelayMs);
        }

        private void FillTextBox(AutomationElement context, string automationId, string text)
        {
            RetryAction<object?>(() =>
            {
                var txt = FindById(context, automationId)?.AsTextBox()
                    ?? throw new Exception($"หาช่องกรอก '{automationId}' ไม่เจอ");
                if (!WaitUntilEnabled(txt, timeoutMs: 3000, pollMs: 300))
                    throw new Exception($"ช่องกรอก '{automationId}' ยัง disabled อยู่หลังรอ");
                txt.Focus();
                txt.Enter(text);
                return null;
            }, timeoutMs: FieldBindRetryTimeoutMs, pollMs: FieldBindRetryPollMs);

            Thread.Sleep(TypingDelayMs);
        }

        private void FillSpinControl(AutomationElement context, string automationId, string value)
        {
            RetryAction<object?>(() =>
            {
                var container = FindById(context, automationId);
                if (container == null) return null;

                WaitUntilEnabled(container, timeoutMs: 2000);
                var editBox = container.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)) ?? container;

                ClickElement(editBox);
                Thread.Sleep(100);
                SelectAllText();
                Keyboard.Type(value);
                Thread.Sleep(TypingDelayMs);
                PressTab();
                return null;
            }, timeoutMs: 3000, pollMs: 300);
        }

        private void FillDateField(AutomationElement context, string automationId, DateTime date, bool useBuddhistYear = false)
        {
            string year = useBuddhistYear ? (date.Year + 543).ToString() : date.Year.ToString();
            string formatted = $"{date.Day:D2}/{date.Month:D2}/{year}";

            RetryAction<object?>(() =>
            {
                var container = FindById(context, automationId)
                    ?? throw new Exception($"หาช่องวันที่ '{automationId}' ไม่เจอ; หน้าต่าง HRMI ที่พบ: {DescribeHrmiWindows()}");

                if (!WaitUntilEnabled(container, timeoutMs: 3000, pollMs: 300))
                    throw new Exception($"ช่องวันที่ '{automationId}' ยัง disabled อยู่หลังรอ");

                var editBox = container.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)) ?? container;

                ClickElement(editBox);
                Thread.Sleep(100);
                SelectAllText();
                Keyboard.Type(new string(formatted.Where(char.IsDigit).ToArray()));
                Thread.Sleep(TypingDelayMs);
                PressTab();
                return null;
            }, timeoutMs: FieldBindRetryTimeoutMs, pollMs: FieldBindRetryPollMs);
        }

        private static void SelectAllText()
        {
            Keyboard.Press(VirtualKeyShort.CONTROL);
            Keyboard.Press(VirtualKeyShort.KEY_A);
            Keyboard.Release(VirtualKeyShort.KEY_A);
            Keyboard.Release(VirtualKeyShort.CONTROL);
            Thread.Sleep(50);
        }

        private static void PressTab()
        {
            Keyboard.Press(VirtualKeyShort.TAB);
            Keyboard.Release(VirtualKeyShort.TAB);
        }

        private void BrowseAndSelectFile(string filePath)
        {
            ClickWizardButton(IdBtnBrowse, fallbackName: "Browse");
            Thread.Sleep(WizardStepDelayMs + 300);

            var openDialog = RetryFind(() =>
                    _automation.GetDesktop().FindFirstChild(cf => cf.ByName("Open"))?.AsWindow()
                    ?? _automation.GetDesktop().FindAllChildren(cf => cf.ByControlType(ControlType.Window))
                        .FirstOrDefault(w => w.Properties.ProcessId.IsSupported
                            && !ReferenceEquals(w, _mainWindow)
                            && (w.FindFirstDescendant(cf => cf.ByAutomationId("1148")) != null
                                || w.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)) != null)),
                timeoutMs: 5000, pollMs: 300)
                ?? throw new Exception("หาหน้าต่างเลือกไฟล์ (Open dialog) ไม่เจอ");

            var fileNameEdit = openDialog.FindFirstDescendant(cf => cf.ByAutomationId("1148"))?.AsTextBox()
                ?? openDialog.FindFirstDescendant(cf => cf.ByAutomationId("1001"))?.AsTextBox()
                ?? openDialog.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit))?.AsTextBox();

            if (fileNameEdit != null)
            {
                try
                {
                    fileNameEdit.Focus();
                    ClickElement(fileNameEdit);
                    Thread.Sleep(80);
                    SelectAllText();
                    Keyboard.Type(filePath);
                    Thread.Sleep(150);
                }
                catch { }
            }

            Keyboard.Press(VirtualKeyShort.RETURN);
            Keyboard.Release(VirtualKeyShort.RETURN);
            Thread.Sleep(WizardStepDelayMs);

            if (!openDialog.Properties.IsOffscreen.ValueOrDefault)
            {
                var openBtn = openDialog.FindFirstDescendant(cf => cf.ByAutomationId("1"))
                    ?? openDialog.FindFirstDescendant(cf => cf.ByName("Open").Or(cf.ByName("&Open")));

                if (openBtn != null)
                {
                    ClickElement(openBtn);
                    Thread.Sleep(WizardStepDelayMs);
                }
            }
            LogStep($"เลือกไฟล์นำเข้า {Path.GetFileName(filePath)} แล้ว");
        }

        // ── 3.4 Lookup Popup Helpers ──

        /* เลือกค่าจาก Pop-up ของช่อง ButtonEdit: กดปุ่ม "..." → หาหน้าต่าง Pop-up ของ HRMI → เลือกแถวที่รหัสตรง
           → ตรวจว่าช่องมีค่าแล้ว (ถ้าช่องยังว่าง HRMI จะพังตอนกด Next ด้วย NullReferenceException) */
        private void SelectFromPicker(string buttonEditId, string code, string dialogLabel)
        {
            var container = RetryFind(() => FindById(_mainWindow, buttonEditId), timeoutMs: 5000, pollMs: 300)
                ?? throw new Exception($"หาช่อง '{buttonEditId}' ไม่เจอ");
            WaitUntilEnabled(container, timeoutMs: 3000);

            // ต้องกดปุ่ม "..." (ปุ่มด้านขวาของช่อง) ไม่ใช่กลางช่อง — คลิกกลางช่องแค่วางเคอร์เซอร์ Pop-up ไม่เปิด
            var dotsButton = container.FindAllDescendants(cf => cf.ByControlType(ControlType.Button)).LastOrDefault();
            if (dotsButton != null) ClickElement(dotsButton);
            else
            {
                var r = container.BoundingRectangle;
                Mouse.Click(new System.Drawing.Point((int)r.Right - 9, (int)(r.Top + r.Height / 2)));
            }
            Thread.Sleep(WizardStepDelayMs + 300);

            var dialog = RetryFind(() => FindHrmiPickerDialog(), timeoutMs: 6000, pollMs: 300)
                ?? throw new Exception($"กด '...' ของ '{buttonEditId}' แล้วไม่พบ {dialogLabel}");

            var row = FindDialogRowByCode(dialog, code, dialogLabel);
            SelectRowAndConfirm(dialog, row, dialogLabel);

            var edit = container.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)) ?? container;
            bool filled = RetryFind(() =>
            {
                try
                {
                    string v = edit.Patterns.Value.IsSupported ? edit.Patterns.Value.Pattern.Value.Value ?? "" : edit.Name ?? "";
                    return v.Contains(code, StringComparison.OrdinalIgnoreCase) ? v : null;
                }
                catch { return null; }
            }, timeoutMs: 3000, pollMs: 200) != null;
            if (!filled)
                throw new Exception($"เลือก '{code}' จาก {dialogLabel} แล้ว แต่ช่อง '{buttonEditId}' ยังไม่มีค่า");
            LogStep($"เลือก '{code}' จาก {dialogLabel} แล้ว");
        }

        /* หาหน้าต่าง Pop-up เลือกข้อมูลของ HRMI (ของโปรเซส HRMI, ไม่ใช่หน้าหลัก และไม่ใช่วิซาร์ดที่มีปุ่ม Next)
           บาง lookup สร้างหน้าต่างและปุ่ม Select ก่อน bind grid จึงไม่บังคับว่าต้องมี DataItem ในจังหวะค้นหา */
        private AutomationElement? FindHrmiPickerDialog(HashSet<IntPtr>? existingWindowHandles = null)
        {
            var candidates = FindHrmiWindows(); // ไม่รวมหน้าจอแท็บ (MDI) ที่มีตารางเหมือน Pop-up

            foreach (var w in candidates)
            {
                try
                {
                    if (w.Properties.NativeWindowHandle.ValueOrDefault == _mainWindow.Properties.NativeWindowHandle.ValueOrDefault) continue;
                    if (w.FindFirstDescendant(cf => cf.ByName(NextButtonName)) != null) continue; // ตัววิซาร์ดเอง
                    var handle = w.Properties.NativeWindowHandle.ValueOrDefault;
                    if (existingWindowHandles != null && !existingWindowHandles.Contains(handle)) return w;
                    var selectButton = w.FindFirstDescendant(cf => cf.ByName("Select").And(cf.ByControlType(ControlType.Button)))
                                      ?? w.FindFirstDescendant(cf => cf.ByName("Select"));
                    var lookupGrid = w.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataGrid)
                                            .Or(cf.ByControlType(ControlType.Table))
                                            .Or(cf.ByControlType(ControlType.List))
                                            .Or(cf.ByControlType(ControlType.DataItem)));
                    if (selectButton == null && lookupGrid == null) continue; // popup ระบุได้ก่อนปุ่มหรือข้อมูลจะ bind ครบ
                    return w;
                }
                catch { }
            }
            return null;
        }

        private AutomationElement FindDialogRowByCode(AutomationElement dialog, string code, string dialogLabel, int timeoutMs = 6000)
        {
            var row = RetryFind(() => FindRowByScanningVisibleElements(dialog, code), timeoutMs: timeoutMs, pollMs: 300);

            if (row == null)
                throw new Exception($"หาแถวรายการรหัส '{code}' ใน {dialogLabel} ไม่เจอ");

            return row;
        }

        /* หาแถวที่รหัสตรงเท่านั้น (ไม่เลือก "row 0" แทนเมื่อหาไม่เจอ) */
        private AutomationElement? FindRowByScanningVisibleElements(AutomationElement dialog, string code)
        {
            bool Matches(AutomationElement el)
            {
                try
                {
                    if (string.Equals(el.Name?.Trim(), code, StringComparison.OrdinalIgnoreCase)) return true;
                    return el.Patterns.Value.IsSupported
                           && string.Equals(el.Patterns.Value.Pattern.Value.Value?.Trim(), code, StringComparison.OrdinalIgnoreCase);
                }
                catch { return false; }
            }

            return dialog.FindFirstDescendant(cf => cf.ByName(code))
                ?? dialog.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem)).FirstOrDefault(Matches);
        }

        /* dialog ปิดแล้วหรือยัง (ถ้าอ่าน property ไม่ได้ = element หายไป = ปิดแล้ว) */
        private static bool IsDialogClosed(AutomationElement dialog)
        {
            try { return !dialog.IsAvailable || dialog.Properties.IsOffscreen.ValueOrDefault; }
            catch { return true; }
        }

        /* เลือกแถวใน popup lookup แล้วปิด dialog:
           1) คลิกแถว "ครั้งเดียว" → 2) กดปุ่ม Select (&Select/ตกลง/เลือก) → 3) ENTER → 4) คลิกพิกัดปุ่ม Select
           ไม่ใช้ดับเบิลคลิก (เดิมคลิกแถวซ้ำหลายครั้งแทนที่จะกด Select) */
        private void SelectRowAndConfirm(AutomationElement dialog, AutomationElement row, string dialogLabel)
        {
            Thread.Sleep(300);
            DiagLog($"SelectRowAndConfirm[{dialogLabel}] เริ่ม -> {DescribeElement(row, "row")}");

            // 1. คลิกเลือกแถวข้อมูล "ครั้งเดียว"
            try
            {
                var rowRect = row.BoundingRectangle;
                if (!rowRect.IsEmpty)
                {
                    Mouse.MoveTo(rowRect.Center());
                    Thread.Sleep(100);
                    Mouse.Click(MouseButton.Left);
                    Thread.Sleep(WizardStepDelayMs);
                }
                else
                {
                    try { row.Focus(); } catch { }
                }
            }
            catch (Exception ex)
            {
                DiagLog($"SelectRowAndConfirm[{dialogLabel}] คลิกแถว error: {DescribeException(ex)}");
            }

            // 2. กดปุ่ม Select
            bool clickedSelectButton =
                   ClickDialogButton(dialog, "Select", waitEnabledMs: 1500)
                || ClickDialogButton(dialog, "&Select", waitEnabledMs: 500)
                || ClickDialogButton(dialog, "ตกลง", waitEnabledMs: 500)
                || ClickDialogButton(dialog, "เลือก", waitEnabledMs: 500);

            Thread.Sleep(WizardStepDelayMs);
            if (IsDialogClosed(dialog))
            {
                DiagLog($"SelectRowAndConfirm[{dialogLabel}] ปิดหลังกดปุ่ม Select (clicked={clickedSelectButton})");
                return;
            }

            // 3. ถ้ายังไม่ปิด ส่งปุ่ม ENTER
            try
            {
                Keyboard.Press(VirtualKeyShort.RETURN);
                Keyboard.Release(VirtualKeyShort.RETURN);
                Thread.Sleep(WizardStepDelayMs);
                if (IsDialogClosed(dialog)) return;
            }
            catch (Exception ex)
            {
                DiagLog($"SelectRowAndConfirm[{dialogLabel}] กด ENTER error: {DescribeException(ex)}");
            }

            // 4. ท่าสำรอง: คลิกพิกัดสัมพัทธ์ตรงตำแหน่งปุ่ม Select ด้านขวาล่างของ Dialog
            ClickSelectByCoordinateOffset(dialog);
            DiagLog($"SelectRowAndConfirm[{dialogLabel}] หลังคลิกพิกัดสำรอง -> dialog closed={IsDialogClosed(dialog)}");
        }

        // ── 3.5 Wizard Navigation Helpers ──

        private void ClickWizardButton(string automationId, int timeoutMs = WizardButtonRetryTimeoutMs, string? fallbackName = null)
        {
            ClickButtonWithFallback(
                timeoutMs,
                $"หาปุ่ม '{automationId}' ไม่เจอ",
                () => FindFirstInHrmi(cf => cf.ByAutomationId(automationId)), // ค้นในวิซาร์ดก่อน (เร็ว)
                () => fallbackName != null ? FindFirstInHrmi(cf => cf.ByName(fallbackName)) : null,
                () => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(automationId)),
                () => fallbackName != null ? _mainWindow.FindFirstDescendant(cf => cf.ByName(fallbackName)) : null
            );
            Thread.Sleep(WizardStepDelayMs);
        }

        private void ClickWizardNavButton(string buttonName, int timeoutMs = WizardButtonRetryTimeoutMs)
        {
            DismissErrorDialogIfPresent();

            ClickButtonWithFallback(
                timeoutMs,
                $"หาปุ่ม '{buttonName}' ไม่เจอ",
                () => FindFirstInHrmi(cf => cf.ByName(buttonName).And(cf.ByControlType(ControlType.Button))), // ค้นในวิซาร์ดก่อน (เร็ว)
                () => _mainWindow.FindFirstDescendant(cf => cf.ByName(buttonName).And(cf.ByControlType(ControlType.Button)))
            );

            // รอเผื่อประมวลผลจริงๆ ก่อนไปเช็คอะไรต่อ (ดู WizardProcessingDelayMs)
            DiagLog($"ClickWizardNavButton('{buttonName}'): รอ {WizardProcessingDelayMs}ms ให้แอปประมวลผลก่อน");
            WaitForWizardResponse(WizardProcessingDelayMs);

            WaitForWizardStepSettled();
            DismissErrorDialogIfPresent();
        }

        private void WaitForWizardStepSettled(int timeoutMs = WizardStepSettleTimeoutMs)
        {
            var sw = Stopwatch.StartNew();
            DiagLog($"WaitForWizardStepSettled เริ่มรอ (timeout {timeoutMs}ms)");
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                var nav = FindFirstInHrmi(cf => cf.ByName(NextButtonName).Or(cf.ByName(FinishButtonName)).And(cf.ByControlType(ControlType.Button)));
                if (nav != null)
                {
                    DiagLog($"WaitForWizardStepSettled เจอปุ่มนำทางแล้ว (elapsed {sw.ElapsedMilliseconds}ms): {DescribeElement(nav, "nav")}");
                    Thread.Sleep(250);
                    return;
                }
                Thread.Sleep(150);
            }
            DiagLog($"WaitForWizardStepSettled หมดเวลา ({timeoutMs}ms) โดยไม่เจอปุ่มนำทางเลย -> fallback sleep {PageLoadDelayMs}ms");
            Thread.Sleep(PageLoadDelayMs);
        }

        /// <summary>
        /// กด Next ไปเรื่อยๆ จนเจอ Finish แล้วกด Finish (ใช้กับ TestCase04 ที่ยังไม่ได้ตรวจจำนวน Next กับ TestStep)
        /// firstNextClickExtraWaitMs: รอพิเศษ (ค่าเริ่มต้น 0 = ไม่รอเพิ่ม) เฉพาะตอนคลิก "Next" ครั้งแรก (i == 0)
        /// — ใช้กับหน้า "เลือกช่วงข้อมูลพนักงาน" ที่แอปต้องประมวลผลสร้าง dataset พนักงาน
        /// (GenIDGroupAndGenerateDatetime) ใช้เวลานาน ถ้ารอไม่พอจะเจอ NullReferenceException
        /// </summary>
        private void CompleteWizard(int maxNextClicks = 10, int firstNextClickExtraWaitMs = 0)
        {
            for (int i = 0; i < maxNextClicks; i++)
            {
                DiagLog($"CompleteWizard loop #{i}");
                DismissErrorDialogIfPresent();

                var finishBtn = FindNavButton(FinishButtonName);
                DiagLog($"  {DescribeElement(finishBtn, "finishBtn")}");

                bool finishEnabled = false;
                try { finishEnabled = finishBtn != null && finishBtn.Properties.IsEnabled.ValueOrDefault; } catch { }

                if (finishBtn != null && finishEnabled)
                {
                    DiagLog("  finishBtn enabled -> คลิก Finish");
                    WaitUntilEnabled(finishBtn, timeoutMs: 2000);
                    ClickButtonRobust(finishBtn);

                    // รอเผื่อประมวลผลก่อนเช็ค error dialog — จุดนี้แอปมักพังด้วย NullReferenceException ถ้าเช็คเร็วเกินไป
                    DiagLog($"  รอ {WizardProcessingDelayMs}ms ให้แอปประมวลผลหลังคลิก Finish");
                    WaitForWizardResponse(WizardProcessingDelayMs);

                    DiagLog($"  หลังคลิก Finish -> {DescribeElement(finishBtn, "finishBtn-after")}");
                    DismissErrorDialogIfPresent();
                    EnsureWizardClosedAfterFinish();
                    return;
                }

                var nextBtn = FindNavButton(NextButtonName);
                DiagLog($"  {DescribeElement(nextBtn, "nextBtn")}");

                if (nextBtn == null)
                {
                    DiagLog("  nextBtn เป็น null รอบแรก -> เรียก WaitForWizardStepSettled เพื่อรอเพิ่ม");
                    WaitForWizardStepSettled(timeoutMs: WizardButtonRetryTimeoutMs);

                    nextBtn = FindNavButton(NextButtonName);
                    DiagLog($"  หลังรอเพิ่ม -> {DescribeElement(nextBtn, "nextBtn-retry")}");

                    if (nextBtn == null)
                    {
                        DiagLog("  ยังไม่เจอ nextBtn เลย -> throw");
                        throw new Exception("หาไม่เจอทั้งปุ่ม 'Next >' และ 'Finish'");
                    }
                }

                bool nextEnabledInTime = WaitUntilEnabled(nextBtn, timeoutMs: 2000);
                DiagLog($"  ก่อนคลิก Next: enabledInTime={nextEnabledInTime} -> {DescribeElement(nextBtn, "nextBtn-before-click")}");
                ClickButtonRobust(nextBtn);

                bool useExtra = i == 0 && firstNextClickExtraWaitMs > 0;
                int waitMs = useExtra ? firstNextClickExtraWaitMs : WizardProcessingDelayMs;
                DiagLog($"  รอ {waitMs}ms ให้แอปประมวลผลหลังคลิก Next (ครั้งที่ #{i}{(useExtra ? " - ใช้ extra wait" : "")})");
                WaitForWizardResponse(waitMs);

                DiagLog($"  หลังคลิก Next -> {DescribeElement(nextBtn, "nextBtn-after-click")}");
                WaitForWizardStepSettled();
                DismissErrorDialogIfPresent();
            }
        }

        /// <summary>
        /// กด Next ตามจำนวนครั้งที่ TestStep กำหนด (ไม่วนจนเจอ Finish) แล้วกด Finish
        /// ถ้าจำนวนหน้าของวิซาร์ดจริงไม่ตรงกับ TestStep (หา Next ไม่เจอ หรือ Finish ยัง disabled) จะ throw ให้เคส FAIL
        /// firstNextClickExtraWaitMs: รอพิเศษเฉพาะ Next ครั้งแรก (หลังแสดงข้อมูลพนักงาน)
        /// </summary>
        private void CompleteWizardExact(int nextClicks, int firstNextClickExtraWaitMs = 0)
        {
            for (int i = 0; i < nextClicks; i++)
            {
                DismissErrorDialogIfPresent();

                // รอให้ปุ่มนำทาง (Next หรือ Finish) โผล่ — หลังประมวลผลหนักอาจนานกว่า 10 วินาที
                var nav = RetryFind(() => FindNavButton(NextButtonName) ?? FindNavButton(FinishButtonName),
                    timeoutMs: NavButtonAppearTimeoutMs, pollMs: 300)
                    ?? throw new Exception($"รอ {NavButtonAppearTimeoutMs / 1000} วินาที ไม่พบทั้งปุ่ม Next และ Finish (ก่อน Next ครั้งที่ {i + 1}/{nextClicks}); หน้าต่าง HRMI ที่พบ: {DescribeHrmiWindows()}");

                var nextBtn = FindNavButton(NextButtonName);
                if (nextBtn == null)
                {
                    // เหลือแต่ Finish = วิซาร์ดจริงมีหน้าน้อยกว่า TestStep
                    string msg = $"วิซาร์ดเหลือปุ่ม Finish หลังกด Next {i}/{nextClicks} ครั้ง (TestStep ระบุ {nextClicks} ครั้ง)";
                    if (StrictNextCount) throw new Exception(msg);
                    TestLogger.LogInfo(ScreenLogName, "⚠ " + msg);
                    break;
                }

                WaitUntilEnabled(nextBtn, timeoutMs: 2000);
                ClickButtonRobust(nextBtn);

                int waitMs = (i == 0 && firstNextClickExtraWaitMs > 0) ? firstNextClickExtraWaitMs : WizardProcessingDelayMs;
                DiagLog($"CompleteWizardExact: Next {i + 1}/{nextClicks} -> รอ {waitMs}ms");
                WaitForWizardResponse(waitMs);
                WaitForWizardStepSettled();
                DismissErrorDialogIfPresent();
            }

            ClickFinishAndEnsureClosed();
        }

        /* กด Finish (ต้องมีและกดได้หลังกด Next ครบตาม TestStep) แล้วรอให้วิซาร์ดปิดจริง */
        private void ClickFinishAndEnsureClosed()
        {
            var finishBtn = RetryFind(() => FindNavButton(FinishButtonName), timeoutMs: NavButtonAppearTimeoutMs)
                ?? throw new Exception($"หาปุ่ม 'Finish' ไม่เจอหลังรอ {NavButtonAppearTimeoutMs / 1000} วินาที; หน้าต่าง HRMI ที่พบ: {DescribeHrmiWindows()}");

            if (!WaitUntilEnabled(finishBtn, timeoutMs: 10000))
                throw new Exception("ปุ่ม 'Finish' ยัง disabled หลังกด Next ครบตาม TestStep (จำนวนหน้าของวิซาร์ดอาจไม่ตรงกับ TestStep)");

            ClickButtonRobust(finishBtn);

            // รอเผื่อประมวลผลก่อนเช็ค error dialog
            DiagLog($"ClickFinishAndEnsureClosed: รอ {WizardProcessingDelayMs}ms ให้แอปประมวลผลหลังคลิก Finish");
            WaitForWizardResponse(WizardProcessingDelayMs);

            DismissErrorDialogIfPresent();
            EnsureWizardClosedAfterFinish();
        }

        /* กด Finish ซ้ำด้วยวิธีที่ต่างกันตามลำดับ (วนทุก 5 วิธี):
           0 เมาส์จริง → 1 DoDefaultAction (MSAA) → 2 โฟกัสหน้าต่าง + Enter → 3 คลิกพิกัดมุมขวาล่างของหน้าต่าง → 4 WM_CLOSE */
        private void RetryFinishClick(AutomationElement wizard, int method)
        {
            try
            {
                var finish = wizard.FindFirstDescendant(cf => cf.ByName(FinishButtonName).And(cf.ByControlType(ControlType.Button)))
                             ?? wizard.FindFirstDescendant(cf => cf.ByName(FinishButtonName));
                DiagLog($"RetryFinishClick method={method % 5} -> {DescribeElement(finish, "finish")}");

                switch (method % 5)
                {
                    case 0:
                        if (finish != null) ClickButtonRobust(finish);
                        break;
                    case 1:
                        if (finish != null) TryInvokeViaLegacyPattern(finish);
                        break;
                    case 2:
                        try { wizard.Focus(); } catch { }
                        Thread.Sleep(150);
                        Keyboard.Press(VirtualKeyShort.RETURN);
                        Keyboard.Release(VirtualKeyShort.RETURN);
                        break;
                    case 3:
                        ClickSelectByCoordinateOffset(wizard, offsetXFromRight: 46, offsetYFromBottom: 21);
                        break;
                    default:
                        var handle = wizard.Properties.NativeWindowHandle.ValueOrDefault;
                        if (handle != IntPtr.Zero) PostMessage(handle, 0x0010, IntPtr.Zero, IntPtr.Zero); // WM_CLOSE
                        break;
                }
                Thread.Sleep(WizardStepDelayMs);
            }
            catch (Exception ex)
            {
                DiagLog($"RetryFinishClick error: {DescribeException(ex)}");
            }
        }

        /* วิซาร์ดยังเปิดอยู่ไหม (มีปุ่ม Next/Finish ในหน้าต่าง popup ของ HRMI) */
        private bool IsWizardOpen()
        {
            try
            {
                return FindHrmiWindows().Any(w =>
                    w.FindFirstDescendant(cf => cf.ByName(NextButtonName).Or(cf.ByName(FinishButtonName))) != null);
            }
            catch { return false; }
        }

        /* หลังกด Finish ต้องแน่ใจว่าวิซาร์ดปิดจริง (เคยคลิกไม่ติด ทำให้วิซาร์ดค้างที่หน้า "บันทึกข้อมูลเสร็จสิ้น"
           แต่เคสยังขึ้น PASS และเคสถัดไปพัง) — ถ้ายังไม่ปิดให้กด Finish ซ้ำได้สูงสุด 3 ครั้ง */
        private void EnsureWizardClosedAfterFinish()
        {
            // กด Finish ไปแล้ว 1 ครั้ง: ถ้าวิซาร์ดยังไม่ปิด (หน้าสรุปผลค้าง = คลิกไม่ติด) ให้ลองกดซ้ำทุก 6 วินาทีด้วยวิธีต่างกัน
            // หน้าสรุป "บันทึกข้อมูลเสร็จสิ้น" บันทึกเสร็จแล้ว Finish แค่ปิดหน้าต่าง กดซ้ำไม่เป็นอันตราย
            // แต่ถ้ามี ProgressBar (HRMI กำลังประมวลผลอยู่) จะไม่กดซ้ำ
            int method = 0;
            long nextRetryAt = 6000;
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < FinishProcessingTimeoutMs)
            {
                var wizard = FindHrmiWindows().FirstOrDefault(w =>
                {
                    try { return w.FindFirstDescendant(cf => cf.ByName(NextButtonName).Or(cf.ByName(FinishButtonName))) != null; }
                    catch { return false; }
                });
                if (wizard == null) { WaitForHrmiIdle(); return; }

                DismissErrorDialogIfPresent(); // กล่องข้อความที่อาจเด้งระหว่างประมวลผล

                if (sw.ElapsedMilliseconds >= nextRetryAt)
                {
                    bool busy = false;
                    try { busy = FindHrmiWindows().Any(w => w.FindFirstDescendant(cf => cf.ByControlType(ControlType.ProgressBar)) != null); } catch { }
                    if (!busy)
                    {
                        RetryFinishClick(wizard, method++);
                        // ยังไม่ปิด → รอบถัดไป; ถ้าปิดแล้ว loop รอบหน้าจะ return เอง
                    }
                    nextRetryAt = sw.ElapsedMilliseconds + 6000;
                }
                Thread.Sleep(500);
            }
            throw new Exception($"กด Finish แล้วรอ {FinishProcessingTimeoutMs / 1000} วินาที วิซาร์ดยังไม่ปิด; หน้าต่าง HRMI ที่พบ: {DescribeHrmiWindows()}");
        }

        /* รอหลังกด Next/Finish แบบไม่เสียเวลาฟรี: จบทันทีที่หน้าวิซาร์ดพร้อม (ปุ่ม Next/Finish กดได้และมองเห็น),
           วิซาร์ดปิดไปแล้ว หรือมีกล่องข้อความของ HRMI เด้งขึ้นมา — maxMs คือเพดาน */
        private void WaitForWizardResponse(int maxMs)
        {
            Thread.Sleep(300); // ให้ HRMI เริ่มประมวลผลคลิกก่อน
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < maxMs)
            {
                try
                {
                    var windows = FindHrmiWindows().ToList();
                    if (windows.Count == 0) return; // วิซาร์ดปิดแล้ว
                    bool busy = windows.Any(w => w.FindFirstDescendant(cf => cf.ByControlType(ControlType.ProgressBar)) != null);
                    bool ready = !busy && windows.Any(w =>
                    {
                        var b = w.FindFirstDescendant(cf => cf.ByName(NextButtonName).Or(cf.ByName(FinishButtonName))
                                                            .Or(cf.ByName("OK")).Or(cf.ByName("&OK")).Or(cf.ByName("ตกลง")));
                        return b != null && b.IsEnabled && !b.IsOffscreen;
                    });
                    if (ready) return;
                }
                catch { }
                Thread.Sleep(250);
            }
        }

        private void DismissErrorDialogIfPresent()
        {
            try
            {
                var okButton = FindFirstInHrmi(cf => cf.ByAutomationId("btnOK"))
                    ?? FindFirstInHrmi(cf =>
                        cf.ByName("OK").Or(cf.ByName("&OK")).Or(cf.ByName("ตกลง")).And(cf.ByControlType(ControlType.Button)));

                if (okButton != null && okButton.Properties.IsEnabled.ValueOrDefault)
                {
                    ClickElement(okButton);
                    Thread.Sleep(PageLoadDelayMs);
                }
            }
            catch { }
        }

        // ── 3.6 Retry / Wait Helpers ──

        private static bool WaitUntilEnabled(AutomationElement element, int timeoutMs = 3000, int pollMs = 200)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try
                {
                    if (element.Properties.IsEnabled.IsSupported && element.Properties.IsEnabled.Value)
                        return true;
                }
                catch { }
                Thread.Sleep(pollMs);
            }
            return false;
        }

        private T? RetryFind<T>(Func<T?> finder, int timeoutMs = 6000, int pollMs = 300) where T : class
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try
                {
                    var result = finder();
                    if (result != null) return result;
                }
                catch { }
                Thread.Sleep(pollMs);
            }
            return null;
        }

        private T RetryAction<T>(Func<T> action, int timeoutMs = 8000, int pollMs = 400)
        {
            var sw = Stopwatch.StartNew();
            Exception? lastEx = null;
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try { return action(); }
                catch (Exception ex)
                {
                    lastEx = ex;
                    Thread.Sleep(pollMs);
                }
            }
            throw lastEx ?? new Exception("การกระทำไม่สำเร็จ (หมดเวลารอโดยไม่ทราบสาเหตุ)");
        }

        // ── 3.7 Logging ──

        private void LogSuccess(string testName, string message)
        {
            _totalSuccess++;
            TestLogger.LogCaseSuccess(testName, message);
        }

        private void LogFailure(string testName, string message)
        {
            _totalFail++;
            TestLogger.LogCaseFailure(testName, message);
        }

        /* แสดง Log สรุปผลรวมท้ายสุด */
        private void LogTotalSummary() =>
            TestLogger.LogInfo(ScreenLogName, $"สรุปผลการทดสอบทั้งหมด: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");

        // ── 3.8 Diagnostic Helpers ──
        // เปิด diagnostic logging เฉพาะตอนไล่ปัญหา (ปิดไว้ให้ output เหมือนโมดูลอื่น: PASS/FAIL/INFO)
        private const bool DiagnosticLoggingEnabled = false;

        private static void DiagLog(string message)
        {
            if (!DiagnosticLoggingEnabled) return;
            try
            {
                Debug.WriteLine($"[DIAG {DateTime.Now:HH:mm:ss.fff}] {message}");
                Console.WriteLine($"[DIAG {DateTime.Now:HH:mm:ss.fff}] {message}");
            }
            catch { }
        }

        private static string DescribeElement(AutomationElement? el, string label)
        {
            if (el == null) return $"{label}: null";
            try
            {
                bool isEnabled = false, isOffscreen = false;
                try { isEnabled = el.Properties.IsEnabled.IsSupported && el.Properties.IsEnabled.Value; } catch { }
                try { isOffscreen = el.Properties.IsOffscreen.IsSupported && el.Properties.IsOffscreen.Value; } catch { }
                var rect = el.BoundingRectangle;
                bool invokeSupported = false, isFocusable = false;
                try { invokeSupported = el.Patterns.Invoke.IsSupported; } catch { }
                try { isFocusable = el.Properties.IsKeyboardFocusable.IsSupported && el.Properties.IsKeyboardFocusable.Value; } catch { }
                return $"{label}: Name='{el.Name}' ControlType={el.ControlType} " +
                       $"IsEnabled={isEnabled} IsOffscreen={isOffscreen} IsKeyboardFocusable={isFocusable} " +
                       $"InvokeSupported={invokeSupported} Rect=[{rect.X},{rect.Y},{rect.Width}x{rect.Height}]";
            }
            catch (Exception ex)
            {
                return $"{label}: (error reading properties: {ex.Message})";
            }
        }

        /* แปลง Exception เป็นข้อความ log แบบละเอียด (type, HResult, ข้อความ, บรรทัดแรกของ stack trace) */
        private static string DescribeException(Exception? ex)
        {
            if (ex == null) return "(no exception)";
            try
            {
                var firstStackLine = string.IsNullOrWhiteSpace(ex.StackTrace)
                    ? "(no stack trace)"
                    : ex.StackTrace.Split('\n')[0].Trim();

                return $"{ex.GetType().Name} (HResult=0x{ex.HResult:X8}): {ex.Message} | at: {firstStackLine}";
            }
            catch
            {
                return ex.Message;
            }
        }

        // ── 3.9 Ensure Application Exit ──
        // ปัญหาเดิม: หลังรันเทสเสร็จ แอปบางครั้งค้าง ไม่ยอมปิด ("Application failed to exit")
        // วิธีทำงาน: 1) ปิดแบบสุภาพ (ปุ่ม "Exit Application" หรือ WM_CLOSE) 2) poll เช็คว่า process ปิดจริง
        // 3) ถ้าไม่ปิดตามเวลาที่กำหนด → Process.Kill(entireProcessTree: true)
        // เรียกซ้ำได้อย่างปลอดภัย: ถ้า process ปิดไปแล้วจะไม่ทำอะไรเพิ่ม

        private const int AppExitCloseTimeoutMs = 5000;
        private const int AppExitKillWaitMs = 3000;

        private void EnsureApplicationExits(int closeTimeoutMs = AppExitCloseTimeoutMs)
        {
            int? processId = null;
            try
            {
                if (_mainWindow.Properties.ProcessId.IsSupported)
                    processId = _mainWindow.Properties.ProcessId.Value;
            }
            catch (Exception ex)
            {
                DiagLog($"EnsureApplicationExits: อ่าน ProcessId ไม่ได้ - {DescribeException(ex)}");
            }

            if (processId == null)
            {
                DiagLog("EnsureApplicationExits: ไม่มี ProcessId ให้ตรวจสอบ -> ข้าม");
                return;
            }

            Process? process;
            try
            {
                process = Process.GetProcessById(processId.Value);
            }
            catch (ArgumentException)
            {
                DiagLog($"EnsureApplicationExits: process PID={processId} ไม่มีอยู่แล้ว (ปิดไปก่อนหน้านี้แล้ว)");
                return;
            }
            catch (Exception ex)
            {
                DiagLog($"EnsureApplicationExits: หา process PID={processId} ไม่ได้ - {DescribeException(ex)}");
                return;
            }

            if (process.HasExited)
            {
                DiagLog($"EnsureApplicationExits: process PID={processId} ปิดไปแล้ว");
                return;
            }

            // 1. ลองปิดแบบสุภาพก่อน
            try
            {
                var exitButton = _mainWindow.FindFirstDescendant(cf => cf.ByName("Exit Application"))
                    ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("ออกจากโปรแกรม"));

                if (exitButton != null)
                {
                    DiagLog("EnsureApplicationExits: เจอปุ่ม Exit Application -> คลิก");
                    ClickButtonRobust(exitButton);
                    Thread.Sleep(300);

                    // อาจมี dialog ยืนยันโผล่มา ("บันทึกก่อนออกไหม?" ฯลฯ) — กดเฉพาะในหน้าต่างของ HRMI
                    foreach (var box in FindHrmiWindows())
                    {
                        if (ClickDialogButton(box, "Yes", waitEnabledMs: 1000) || ClickDialogButton(box, "OK", waitEnabledMs: 500)
                            || ClickDialogButton(box, "&OK", waitEnabledMs: 500)) break;
                    }
                }
                else
                {
                    DiagLog("EnsureApplicationExits: ไม่เจอปุ่ม Exit Application -> ส่ง WM_CLOSE ไปยัง main window แทน");
                    TryCloseMainWindowViaWinApi();
                }
            }
            catch (Exception ex)
            {
                DiagLog($"EnsureApplicationExits: ปิดแบบสุภาพล้มเหลว - {DescribeException(ex)}");
            }

            // 2. รอเช็คจริงว่า process ปิดหรือยัง (poll ทุก 200ms)
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < closeTimeoutMs)
            {
                try
                {
                    process.Refresh();
                    if (process.HasExited)
                    {
                        DiagLog($"EnsureApplicationExits: process PID={processId} ปิดเรียบร้อยแบบปกติ (elapsed {sw.ElapsedMilliseconds}ms)");
                        return;
                    }
                }
                catch
                {
                    // อ่านสถานะ process ไม่ได้อีกต่อไป = process ปิดไปแล้ว
                    DiagLog($"EnsureApplicationExits: อ่านสถานะ process PID={processId} ไม่ได้อีกต่อไป ถือว่าปิดแล้ว");
                    return;
                }
                Thread.Sleep(200);
            }

            // 3. ยังไม่ปิดภายในเวลาที่กำหนด -> บังคับ Kill
            try
            {
                process.Refresh();
                if (!process.HasExited)
                {
                    DiagLog($"EnsureApplicationExits: process PID={processId} ไม่ปิดภายใน {closeTimeoutMs}ms -> บังคับ Kill(entireProcessTree: true)");
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(AppExitKillWaitMs);
                    DiagLog($"EnsureApplicationExits: หลัง Kill -> HasExited={process.HasExited}");
                }
            }
            catch (Exception ex)
            {
                DiagLog($"EnsureApplicationExits: Kill() ล้มเหลว - {DescribeException(ex)}");
            }
        }

        /* ส่ง WM_CLOSE ไปยัง native window handle ของ main window (ระดับ HWND) */
        private void TryCloseMainWindowViaWinApi()
        {
            try
            {
                var handleProp = _mainWindow.Properties.NativeWindowHandle;
                if (!handleProp.IsSupported) return;

                IntPtr handle = handleProp.ValueOrDefault;
                if (handle == IntPtr.Zero) return;

                const int WM_CLOSE = 0x0010;
                SendMessage(handle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                DiagLog($"TryCloseMainWindowViaWinApi error: {DescribeException(ex)}");
            }
        }
    }
}