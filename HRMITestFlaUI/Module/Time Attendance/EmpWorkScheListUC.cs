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



        private readonly AutomationElement _mainWindow;
        private readonly UIA3Automation _automation;

        private int _totalSuccess = 0;
        private int _totalFail = 0;

        // ── Delay Constants ──
        private const int MenuTransitionDelayMs = 400;
        private const int PageLoadDelayMs = 600;
        private const int WizardStepDelayMs = 500;
        private const int ScrollStepDelayMs = 500;
        private const int TypingDelayMs = 100;
        private const int FieldBindRetryTimeoutMs = 15000;
        private const int FieldBindRetryPollMs = 500;

        private const int WizardButtonRetryTimeoutMs = 10000;
        private const int WizardStepSettleTimeoutMs = 6000;

        // ★ เพิ่มใหม่: เวลารอ "เผื่อประมวลผล" หลังกดปุ่ม Next/Finish โดยตรง — ก่อนจะไปเช็คอะไร
        // ต่อ (error dialog / ปุ่มถัดไป ฯลฯ) เลย เพราะแอปต้องใช้เวลาประมวลผลเบื้องหลัง
        // (สร้าง dataset พนักงาน / คำนวณตารางงาน) ซึ่งตอนทดสอบมือคนเรามีจังหวะหน่วงตามธรรมชาติ
        // อยู่แล้วจึงไม่เจอปัญหา แต่ automation คลิกเร็วเกินไปจนแซงหน้าประมวลผล ทำให้เจอ
        // NullReferenceException ที่ GenIDGroupAndGenerateDatetime
        private const int WizardProcessingDelayMs = 2000;

        // ── Menu Path & Navigation Constants ──
        private const string TimeAttendanceMenuName = "Time Attendance";
        private const string EmpWorkScheListMenuRowName = "treeListColumn row 2";
        private const string EmpWorkScheListMenuLabel = "บันทึกรูปแบบตารางการทำงาน";

        // ── AutomationId ของ Wizard / Controls ──
        private const string MoreActionName = "More Action";
        private const string IdDateStart = "devDateStart";
        private const string IdDateEnd = "devDateEnd";
        private const string IdRdgAddWorkType = "devRdgAddWorkType";

        private const string IdBtnWorkSchedulePattern = "devBtnTemplateSche";
        private const string IdBtnHolidaySche = "devBtnHolidaySche";
        private const string IdSpinWorkDays = "devSpinWorkDays";
        private const string IdSpinHolidayDays = "devSpinHolidayDays";

        private const string IdChkReplaceHoliday = "devChkReplaceByHoliday";

        private const string IdBtnBrowse = "devBtnBrowse";
        private const string IdBtnShowEmployeeData = "devBtnShowEmployeeData";

        private const string NextButtonName = "Next >";
        private const string FinishButtonName = "Finish";

        private const string IdRdgSetWorkSchedule = "devRdgWorkSche";
        private const string IdRdgCopyWorkSche = "devRdgCopyWorkSche";
        private const string IdRdgHolidaySche = "devRdgHolidaySche";

        private const string IdTxtHolidayTopic = "devtxtHoliday";
        private const string IdGridWorkConfigList = "devWorkConfigGridList";
        private const string IdBtnEmployee = "devBtnEmployee";
        private const string IdGridHolidaySche = "devGcHolidaySche";

        private static readonly string[] AllHolidayDayCheckboxIds =
        {
            "devChkMonHoliday", "devChkTueHoliday", "devChkWedHoliday", "devChkThurHoliday",
            "devChkFriHoliday", "devChkSatHoliday", "devChkSunHoliday"
        };

        // ── เงื่อนไขการเลือกช่วงข้อมูลพนักงาน (ตาม Test Step: หน่วยงาน/ตำแหน่งงาน/กลุ่มพนักงาน/ระดับพนักงาน) ──
        // *** โปรดตรวจสอบ AutomationId จริงด้วย Accessibility Insights / Inspect.exe ก่อนรันจริง ***
        // ชื่อด้านล่างนี้เดาตามรูปแบบการตั้งชื่อ (devChk...All) ที่ใช้อยู่แล้วในโค้ดส่วนอื่น
        // ถ้าไม่ตรง ให้แก้ค่า string เหล่านี้ให้ตรงกับหน้าจอจริงเท่านั้น ไม่ต้องแก้ logic ส่วนอื่น
        private const string IdChkOrgAll = "devChkOrgAll";
        private const string IdChkPositionAll = "devChkPositionAll";
        private const string IdChkEmpGroupAll = "devChkEmpGroupAll";           // TODO: ยืนยัน AutomationId "กลุ่มพนักงาน ทั้งหมด"
        private const string IdChkEmployeeLevelAll = "devChkEmployeeLevelAll"; // TODO: ยืนยัน AutomationId "ระดับพนักงาน ทั้งหมด"
        private const string IdChkEmployeeTypeAll = "devChkEmployeeTypeAll";   // ใช้เฉพาะเคสที่ต้องการ "ประเภทพนักงาน" ทั้งหมด (ไม่ใช้ในเคส 1,3 ตาม Test Step)

        // ── ช่วงข้อมูล (from-to) สำหรับ "ยกเลิกตารางการทำงานของพนักงานแบบรวม" ──
        // *** ต้องตรวจสอบ AutomationId จริงจากหน้าจอ ก่อนใช้งาน — เป็นการเดาตามรูปแบบเดิม ***
        private const string IdCboWorkGroupCodeFrom = "devCboWorkGroupCodeFrom"; // รหัสกะงาน (จาก)
        private const string IdCboWorkGroupCodeTo = "devCboWorkGroupCodeTo";     // รหัสกะงาน (ถึง)
        private const string IdCboOrgFrom = "devCboOrgFrom";                     // หน่วยงาน (จาก)
        private const string IdCboOrgTo = "devCboOrgTo";                         // หน่วยงาน (ถึง)
        private const string IdCboEmpGroupFrom = "devCboEmpGroupFrom";           // กลุ่มพนักงาน (จาก)
        private const string IdCboEmpGroupTo = "devCboEmpGroupTo";               // กลุ่มพนักงาน (ถึง)
        private const string IdDdlEmployeeType = "devDdlEmployeeType";           // ประเภทพนักงาน (dropdown ลูกศร)
        private const string IdChkEmpTypeHourly = "devChkEmpTypeHourly";         // พนักงานรายชั่วโมง
        private const string IdChkEmpTypeContract = "devChkEmpTypeContract";     // พนักงานรายเหมา
        private const string IdChkEmpTypeDaily = "devChkEmpTypeDaily";           // พนักงานรายวัน
        private const string IdChkEmpTypeMonthly = "devChkEmpTypeMonthly";       // พนักงานรายเดือน
        private const string IdCboPositionFrom = "devCboPositionFrom";           // ตำแหน่งงาน (จาก)
        private const string IdCboPositionTo = "devCboPositionTo";               // ตำแหน่งงาน (ถึง)
        private const string IdCboEmployeeLevelFrom = "devCboEmployeeLevelFrom"; // ระดับพนักงาน (จาก)
        private const string IdCboEmployeeLevelTo = "devCboEmployeeLevelTo";     // ระดับพนักงาน (ถึง)
        private const string IdCboEmployeeCodeFrom = "devCboEmployeeCodeFrom";   // รหัสพนักงาน (จาก)
        private const string IdCboEmployeeCodeTo = "devCboEmployeeCodeTo";       // รหัสพนักงาน (ถึง)
        private const string IdChkCancelWorkHoliday = "devChkCancelWorkHoliday"; // ผลที่คาดหวัง: ยกเลิกวันหยุดกะ

        private const string ActionSetScheduleForGroup = "กำหนดตารางการทำงานของพนักงานแบบรวม";
        private const string ActionImportSchedule = "Import ตารางการทำงานของพนักงาน";
        private const string ActionCopyScheduleOfEmployee = "คัดลอกตารางการทำงานของพนักงาน";
        private const string ActionCancelScheduleForGroup = "ยกเลิกตารางการทำงานของพนักงานแบบรวม";

        private const string ConditionsTabName = "กำหนดเงื่อนไข";
        private const string IdChkCancelSchedule = "devChkCancelSchedule";
        private const string SampleEmployeeCodeForCopyShortcut = "EMP0001";

        // ── ปุ่ม "รีเช็คข้อมูลพนักงาน" ที่อยู่ใต้ตารางแสดงข้อมูลพนักงาน ──
        // ★ เพิ่มใหม่: ใช้แก้ปัญหา NullReferenceException ที่ GenIDGroupAndGenerateDatetime
        // (ฝั่งแอป HRMI) ซึ่งพังเฉพาะตอนรันผ่าน automation แต่ไม่พังตอนทดสอบมือ — สันนิษฐานว่า
        // เป็น race condition: automation กด "Next" เร็วเกินไปก่อนแอปสร้าง dataset ภายใน
        // (tdsEmpWorkScheGen) จากรายชื่อพนักงานเสร็จ จึงบังคับคลิกปุ่มนี้ (ถ้ามีในหน้าจอ)
        // ก่อนไป Next เพื่อให้แอป sync ข้อมูลให้เสร็จก่อนเสมอ
        private const string BtnRecheckEmployeeDataName = "รีเช็คข้อมูลพนักงาน";

        //----
        // * 1. Constructor & Navigation *
        //----
        public EmpWorkScheListUC(AutomationElement mainWindow, UIA3Automation automation)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

        public void NavigateToEmpWorkScheListUC()
        {
            var timeAttendanceTab = RetryFind(() => _mainWindow.FindFirstDescendant(cf => cf.ByName(TimeAttendanceMenuName)), timeoutMs: 3000)
                ?? throw new Exception($"หาเมนูหลัก '{TimeAttendanceMenuName}' ไม่เจอ");

            timeAttendanceTab.Click();
            Thread.Sleep(MenuTransitionDelayMs);

            var menuRow = RetryFind(FindEmpWorkScheListMenuRow, timeoutMs: 2000);

            if (menuRow == null)
            {
                var lineDownButton = _mainWindow.FindFirstDescendant(cf => cf.ByName("Line Down"));
                if (lineDownButton != null)
                {
                    const int maxScrollSteps = 10;
                    for (int step = 0; step < maxScrollSteps && menuRow == null; step++)
                    {
                        try
                        {
                            Mouse.MoveTo(lineDownButton.BoundingRectangle.Center());
                            Mouse.Click(MouseButton.Left);
                        }
                        catch
                        {
                            try { lineDownButton.Click(); } catch { }
                        }

                        menuRow = RetryFind(FindEmpWorkScheListMenuRow, timeoutMs: ScrollStepDelayMs);
                    }
                }
                menuRow ??= RetryFind(FindEmpWorkScheListMenuRow, timeoutMs: 3000);
            }

            if (menuRow == null)
                throw new Exception($"หาเมนู '{EmpWorkScheListMenuLabel}' ({EmpWorkScheListMenuRowName}) ไม่เจอ");

            menuRow.Click();
            Thread.Sleep(PageLoadDelayMs);
        }

        private AutomationElement? FindEmpWorkScheListMenuRow()
        {
            return _mainWindow.FindFirstDescendant(cf => cf.ByName(EmpWorkScheListMenuRowName))
                ?? _mainWindow.FindFirstDescendant(cf => cf.ByName(EmpWorkScheListMenuLabel));
        }

        //----
        // * 2. Test Orchestration *
        //----
        public void RunAllValidationTests()
        {
            _totalSuccess = 0;
            _totalFail = 0;

            var scenarios = new (string TestName, Action Step)[]
            {
                // ── 1. ทดสอบ NEW กำหนดตารางการทำงานของพนักงานแบบรวม (Test Step แถว 7-8) ──
                ("TestCase01_SetScheduleForGroup_General",
                    () => SetScheduleForGroup(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31),
                        "กำหนดตารางการทำงานแบบทั่วไป", "WSG001")),
                ("TestCase01_SetScheduleForGroup_Rotation",
                    () => SetScheduleForGroup(new DateTime(2026, 2, 1), new DateTime(2026, 3, 31),
                        "กำหนดตารางการทำงานแบบวนกะงาน", "WSG002")),

                // ── 3. คัดลอกตารางการทำงานของพนักงาน (Test Step แถว 32-33) ──
                ("TestCase03_CopyScheduleOfEmployee_General",
                    () => CopyScheduleOfEmployee(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31),
                        "กำหนดตารางการทำงานแบบทั่วไป", "WSG001")),
                ("TestCase03_CopyScheduleOfEmployee_Rotation",
                    () => CopyScheduleOfEmployee(new DateTime(2026, 1, 2), new DateTime(2026, 12, 31),
                        "กำหนดตารางการทำงานแบบวนกะงาน", "WSG002")),

                // ── คัดลอกตารางการทำงานของพนักงาน (shortcut, แถว 39) ──
                ("TestCase01_CopyScheduleShortcut",
                    () => CopyScheduleShortcut(new DateTime(2026, 1, 3), new DateTime(2026, 12, 3))),

                // ── กำหนดวันหยุด (แถว 42) ──
                ("TestCase01_SetHoliday",
                    () => SetHoliday(new DateTime(2026, 6, 1), new DateTime(2026, 12, 31))),

                // ── 2. Import ตารางการทำงานของพนักงาน (Test Step แถว 21-23: 3 ชุดข้อมูล) ──
                ("TestCase02_Import_1",
                    () => ImportEmployeeWorkSchedule(new DateTime(2026, 5, 15), new DateTime(2026, 6, 15))),
                ("TestCase02_Import_2",
                    () => ImportEmployeeWorkSchedule(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31))),
                ("TestCase02_Import_3",
                    () => ImportEmployeeWorkSchedule(new DateTime(2026, 2, 2), new DateTime(2026, 3, 3))),

                // ── 4. ยกเลิกตารางการทำงานของพนักงานแบบรวม (Test Step แถว 49-50) ──
                ("TestCase04_CancelScheduleForGroup_1",
                    () => CancelScheduleForGroup(
                        start: new DateTime(2026, 9, 1), end: new DateTime(2026, 12, 31),
                        workGroupFrom: "GO001", workGroupTo: "OT-EVN02",
                        orgFrom: "ORG001", orgTo: "ORG005",
                        empGroupFrom: "EG-001", empGroupTo: "EG-004",
                        empTypeChecks: new[] { IdChkEmpTypeHourly, IdChkEmpTypeContract },
                        positionFrom: "H-001", positionTo: "P-001",
                        levelFrom: "EL-001", levelTo: "EL-005",
                        empCodeFrom: "EMP00001", empCodeTo: "EMP00004",
                        cancelWorkSchedule: true, cancelWorkHoliday: false)),
                ("TestCase04_CancelScheduleForGroup_2",
                    () => CancelScheduleForGroup(
                        start: new DateTime(2026, 9, 3), end: new DateTime(2026, 9, 3),
                        workGroupFrom: "GO001", workGroupTo: "OT-EVN03",
                        orgFrom: "ORG001", orgTo: "ORG006",
                        empGroupFrom: "EG-001", empGroupTo: "EG-004",
                        empTypeChecks: new[] { IdChkEmpTypeDaily, IdChkEmpTypeMonthly },
                        positionFrom: "H-001", positionTo: "P-001",
                        levelFrom: "EL-001", levelTo: "EL-005",
                        empCodeFrom: "EMP00001", empCodeTo: "EMP00004",
                        cancelWorkSchedule: false, cancelWorkHoliday: true)),
            };

            // แสดง Log เริ่มต้นหน้าจอ
            TestLogger.LogInfo("EmpWorkScheListUC", "เริ่มทดสอบหน้าจอ บันทึกรูปแบบตารางการทำงาน");

            foreach (var (testName, step) in scenarios)
            {
                RunScenarioStep(testName, step);
            }

            // แสดง Log สรุปผลรวมท้ายสุด
            TestLogger.LogInfo("EmpWorkScheListUC",
                $"สรุปผลรวมทั้งหมด: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");

            // ★ เพิ่มใหม่: การันตีว่าแอปถูกปิดจริงหลังรันเทสครบทุกเคส
            // ทำในไฟล์นี้เอง (ไม่แก้ BaseTest.cs เพราะใช้ร่วมกับเพื่อนร่วมทีม) เพื่อป้องกันปัญหา
            // "Application failed to exit" / หน้าจอค้าง ที่เคยเจอ — ถ้า BaseTest.cs ปิดแอปสำเร็จอยู่แล้ว
            // เมธอดนี้จะเจอว่า process ปิดไปแล้วและไม่ทำอะไรเพิ่ม (ปลอดภัย เรียกซ้ำได้)
            EnsureApplicationExits();
        }

        private void RunScenarioStep(string testName, Action step)
        {
            try
            {
                step();
                LogSuccessCounted(testName, "ทำสำเร็จตามขั้นตอน Wizard ครบทุกสเต็ป");
            }
            catch (Exception ex)
            {
                LogFailureCounted(testName, $"ไม่สำเร็จ: {DescribeException(ex)}");
            }
            finally
            {
                CloseAnyOpenWizardDialog();
            }
        }

        private void LogStep(string message) { }

        private void CloseAnyOpenWizardDialog()
        {
            try
            {
                DismissErrorDialogIfPresent();

                DiagLog("CloseAnyOpenWizardDialog: กำลังเช็คว่ามี Wizard ค้างอยู่หรือไม่ (หาปุ่ม Cancel)");

                // ปุ่ม Cancel ต้อง enable จริงถึงจะถือว่ามี wizard ค้างอยู่ — ถ้าไม่เจอหรือ disabled ให้ถือว่าไม่มีอะไรต้องปิด
                bool cancelled = ClickDialogButton(_mainWindow, "Cancel", waitEnabledMs: 500);
                if (!cancelled)
                {
                    DiagLog("CloseAnyOpenWizardDialog: ไม่มีปุ่ม Cancel ที่ enable อยู่ -> ไม่มี wizard ค้าง");
                    return;
                }

                Thread.Sleep(PageLoadDelayMs);

                // หลังกด Cancel มักมี dialog ยืนยัน (OK/&OK/Yes) โผล่มา ลองกดทีละปุ่มตามลำดับ
                if (!ClickDialogButton(_automation.GetDesktop(), "OK", waitEnabledMs: 1500)
                    && !ClickDialogButton(_automation.GetDesktop(), "&OK", waitEnabledMs: 500))
                {
                    ClickDialogButton(_automation.GetDesktop(), "Yes", waitEnabledMs: 500);
                }
            }
            catch (Exception ex)
            {
                DiagLog($"CloseAnyOpenWizardDialog error: {DescribeException(ex)}");
            }
        }

        public void RunEditTests() { }
        public void RunEditValidationTests() { }
        public void RunDeleteTests() { }

        /// <summary>
        /// TestCase01: กำหนดตารางการทำงานของพนักงานแบบรวม (ทั่วไป / วนกะงาน)
        /// ตาม Test Step แถว 7-8: More Action → กรอกวันที่ → Radio → รูปแบบตารางงาน (Select)
        /// → Next → เงื่อนไข 4 อย่าง (หน่วยงาน/ตำแหน่งงาน/กลุ่มพนักงาน/ระดับพนักงาน) → แสดงข้อมูลพนักงาน → Finish
        /// หมายเหตุ: ตัด SelectHolidayCalendar / SelectWorkConfigGridRow / SetHolidayDays / ReplaceHoliday
        /// / FillSpinControl ออก เนื่องจากไม่ปรากฏใน Test Step ของเคสนี้
        /// (ถ้าหน้าจอจริงบังคับให้กรอกฟิลด์เหล่านี้ก่อนกด Next ได้ ให้แจ้งเพื่อเพิ่มกลับเข้ามา)
        /// </summary>
        //----
        // * 3. Test Steps *
        //----
        private void SetScheduleForGroup(DateTime start, DateTime end, string workType, string patternCode)
        {
            ClickMoreAction();
            ClickMoreActionItem(ActionSetScheduleForGroup);

            FillDateField(_mainWindow, IdDateStart, start, useBuddhistYear: true);
            FillDateField(_mainWindow, IdDateEnd, end, useBuddhistYear: true);

            SelectRadioButton(_mainWindow, IdRdgSetWorkSchedule);
            SelectRadioGroupOption(_mainWindow, IdRdgAddWorkType, workType);

            SelectWorkSchedulePattern(patternCode);

            ClickWizardNavButton(NextButtonName);

            SelectEmployeeFilterConditions();
            ClickWizardButton(IdBtnShowEmployeeData, fallbackName: "แสดงข้อมูลพนักงาน");
            ClickRecheckEmployeeDataIfPresent();
            // ★ รอพิเศษ 1 นาที เฉพาะตอนคลิก Next ครั้งแรกหลังจากนี้ (ดูใน CompleteWizard)
            // เพราะเป็นจุดที่แอปต้องประมวลผลสร้าง dataset พนักงาน (GenIDGroupAndGenerateDatetime)
            CompleteWizard(firstNextClickExtraWaitMs: 60000);
        }

        /// <summary>
        /// TestCase01: คัดลอกตารางการทำงานของพนักงาน (shortcut ผ่าน "กำหนดตารางการทำงานของพนักงานแบบรวม"
        /// แล้วเลือก Radio "คัดลอกตารางการทำงานของพนักงาน") ตาม Test Step แถว 11/39
        /// </summary>
        private void CopyScheduleShortcut(DateTime start, DateTime end)
        {
            ClickMoreAction();
            ClickMoreActionItem(ActionSetScheduleForGroup);
            FillDateField(_mainWindow, IdDateStart, start, useBuddhistYear: true);
            FillDateField(_mainWindow, IdDateEnd, end, useBuddhistYear: true);
            SelectRadioButton(_mainWindow, IdRdgCopyWorkSche);
            SelectEmployeeByCode(SampleEmployeeCodeForCopyShortcut);
            ClickWizardNavButton(NextButtonName);

            EnsureConditionsTabActive();
            ClickWizardButton(IdBtnShowEmployeeData, fallbackName: "แสดงข้อมูลพนักงาน");
            ClickRecheckEmployeeDataIfPresent();
            // ★ รอพิเศษ 1 นาที เฉพาะตอนคลิก Next ครั้งแรกหลังจากนี้ (ดูใน CompleteWizard)
            // เพราะเป็นจุดที่แอปต้องประมวลผลสร้าง dataset พนักงาน (GenIDGroupAndGenerateDatetime)
            CompleteWizard(firstNextClickExtraWaitMs: 60000);
        }

        /// <summary>
        /// TestCase01: กำหนดวันหยุด ตาม Test Step แถว 14/42
        /// </summary>
        private void SetHoliday(DateTime start, DateTime end)
        {
            ClickMoreAction();
            ClickMoreActionItem(ActionSetScheduleForGroup);
            FillDateField(_mainWindow, IdDateStart, start, useBuddhistYear: true);
            FillDateField(_mainWindow, IdDateEnd, end, useBuddhistYear: true);
            SelectRadioButton(_mainWindow, IdRdgHolidaySche);

            FillTextBox(_mainWindow, IdTxtHolidayTopic, "หยุดเพื่อไปหาสิ่งใหม่ๆ");
            ClickCheckbox(IdChkReplaceHoliday);

            SetHolidayDays("devChkSatHoliday", "devChkSunHoliday");

            ClickWizardNavButton(NextButtonName);
            EnsureConditionsTabActive();
            ClickWizardButton(IdBtnShowEmployeeData, fallbackName: "แสดงข้อมูลพนักงาน");
            ClickRecheckEmployeeDataIfPresent();
            // ★ รอพิเศษ 1 นาที เฉพาะตอนคลิก Next ครั้งแรกหลังจากนี้ (ดูใน CompleteWizard)
            // เพราะเป็นจุดที่แอปต้องประมวลผลสร้าง dataset พนักงาน (GenIDGroupAndGenerateDatetime)
            CompleteWizard(firstNextClickExtraWaitMs: 60000);
        }

        /// <summary>
        /// TestCase02: Import ตารางการทำงานของพนักงาน — รับ start/end จากผู้เรียก
        /// เพื่อให้เรียกซ้ำได้ 3 ชุดตาม Test Step แถว 21-23 (path ไฟล์เดียวกันทั้ง 3 ชุด)
        /// </summary>
        private void ImportEmployeeWorkSchedule(DateTime start, DateTime end, string? filePath = null)
        {
            ClickMoreAction();
            ClickMoreActionItem(ActionImportSchedule);

            BrowseAndSelectFile(filePath ?? @"C:\TestData\ImportEmployeeWorkSchedule.xlsx");
            FillDateField(_mainWindow, IdDateStart, start, useBuddhistYear: true);
            FillDateField(_mainWindow, IdDateEnd, end, useBuddhistYear: true);
            CompleteWizard();
        }

        /// <summary>
        /// TestCase03: คัดลอกตารางการทำงานของพนักงาน ตาม Test Step แถว 32-33
        /// เพิ่ม SelectWorkSchedulePattern(patternCode) ที่ขาดไปในเวอร์ชันก่อนหน้า
        /// และรับ workType/patternCode เพื่อให้ครบทั้ง 2 เคส (ทั่วไป/WSG001, วนกะงาน/WSG002)
        /// </summary>
        private void CopyScheduleOfEmployee(DateTime start, DateTime end, string workType, string patternCode)
        {
            ClickMoreAction();
            ClickMoreActionItem(ActionCopyScheduleOfEmployee);
            FillDateField(_mainWindow, IdDateStart, start, useBuddhistYear: true);
            FillDateField(_mainWindow, IdDateEnd, end, useBuddhistYear: true);
            SelectRadioButton(_mainWindow, IdRdgSetWorkSchedule);
            SelectRadioGroupOption(_mainWindow, IdRdgAddWorkType, workType);
            SelectWorkSchedulePattern(patternCode);
            ClickWizardNavButton(NextButtonName);

            SelectEmployeeFilterConditions();
            ClickWizardButton(IdBtnShowEmployeeData, fallbackName: "แสดงข้อมูลพนักงาน");
            ClickRecheckEmployeeDataIfPresent();
            // ★ รอพิเศษ 1 นาที เฉพาะตอนคลิก Next ครั้งแรกหลังจากนี้ (ดูใน CompleteWizard)
            // เพราะเป็นจุดที่แอปต้องประมวลผลสร้าง dataset พนักงาน (GenIDGroupAndGenerateDatetime)
            CompleteWizard(firstNextClickExtraWaitMs: 60000);
        }

        /// <summary>
        /// TestCase04: ยกเลิกตารางการทำงานของพนักงานแบบรวม ตาม Test Step แถว 49-50
        /// *** เมธอดนี้ใช้ AutomationId ที่ "เดา" ไว้ในหมวด TODO ด้านบนของไฟล์ — ต้องตรวจสอบกับ
        /// Accessibility Insights / Inspect.exe ก่อนใช้งานจริง มิฉะนั้นจะหา element ไม่เจอ ***
        /// รองรับ: ช่วงรหัสกะงาน / ช่วงหน่วยงาน / ช่วงกลุ่มพนักงาน / ประเภทพนักงาน (เลือกบางตัวจาก dropdown)
        /// / ช่วงตำแหน่งงาน / ช่วงระดับพนักงาน / ช่วงรหัสพนักงาน / ผลที่คาดหวัง (ยกเลิกกะงาน และ/หรือ ยกเลิกวันหยุดกะ)
        /// </summary>
        private void CancelScheduleForGroup(
            DateTime start, DateTime end,
            string workGroupFrom, string workGroupTo,
            string orgFrom, string orgTo,
            string empGroupFrom, string empGroupTo,
            string[] empTypeChecks,
            string positionFrom, string positionTo,
            string levelFrom, string levelTo,
            string empCodeFrom, string empCodeTo,
            bool cancelWorkSchedule, bool cancelWorkHoliday)
        {
            ClickMoreAction();
            ClickMoreActionItem(ActionCancelScheduleForGroup);
            FillDateField(_mainWindow, IdDateStart, start, useBuddhistYear: true);
            FillDateField(_mainWindow, IdDateEnd, end, useBuddhistYear: true);

            // ผลที่คาดหวัง
            if (cancelWorkSchedule)
                ClickCheckbox(IdChkCancelSchedule, fallbackName: "ยกเลิกกะงาน");
            if (cancelWorkHoliday)
                ClickCheckbox(IdChkCancelWorkHoliday, fallbackName: "ยกเลิกวันหยุดกะ");

            // ช่วงรหัสกะงาน
            SelectComboRange(IdCboWorkGroupCodeFrom, IdCboWorkGroupCodeTo, workGroupFrom, workGroupTo);
            // ช่วงหน่วยงาน
            SelectComboRange(IdCboOrgFrom, IdCboOrgTo, orgFrom, orgTo);
            // ช่วงกลุ่มพนักงาน
            SelectComboRange(IdCboEmpGroupFrom, IdCboEmpGroupTo, empGroupFrom, empGroupTo);
            // ประเภทพนักงาน (คลิกลูกศร dropdown แล้วติ๊กเฉพาะรายการที่ระบุ)
            SelectEmployeeTypeDropdown(empTypeChecks);
            // ช่วงตำแหน่งงาน
            SelectComboRange(IdCboPositionFrom, IdCboPositionTo, positionFrom, positionTo);
            // ช่วงระดับพนักงาน
            SelectComboRange(IdCboEmployeeLevelFrom, IdCboEmployeeLevelTo, levelFrom, levelTo);
            // ช่วงรหัสพนักงาน
            SelectComboRange(IdCboEmployeeCodeFrom, IdCboEmployeeCodeTo, empCodeFrom, empCodeTo);

            CompleteWizard();
        }

        /// <summary>
        /// เลือกค่า "จาก-ถึง" ใน combo/dropdown สองช่อง (ใช้กับ ช่วงรหัสกะงาน/หน่วยงาน/กลุ่มพนักงาน/
        /// ตำแหน่งงาน/ระดับพนักงาน/รหัสพนักงาน) — พิมพ์ค่าลง Edit ภายใน combo แล้วกด Enter
        /// *** ปรับ logic ภายในให้ตรงกับพฤติกรรมจริงของ control (ComboBox / Popup List) หลังตรวจสอบ AutomationId ***
        /// </summary>
        private void SelectComboRange(string fromAutomationId, string toAutomationId, string fromValue, string toValue)
        {
            FillComboValue(fromAutomationId, fromValue);
            FillComboValue(toAutomationId, toValue);
        }

        private void FillComboValue(string automationId, string value)
        {
            RetryAction<object?>(() =>
            {
                var container = _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(automationId))
                    ?? throw new Exception($"หา combo '{automationId}' ไม่เจอ — ตรวจสอบ AutomationId ให้ตรงกับหน้าจอจริง");

                WaitUntilEnabled(container, timeoutMs: 2000);
                var editBox = container.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)) ?? container;

                ClickElement(editBox);
                Thread.Sleep(100);

                Keyboard.Press(VirtualKeyShort.CONTROL);
                Keyboard.Press(VirtualKeyShort.KEY_A);
                Keyboard.Release(VirtualKeyShort.KEY_A);
                Keyboard.Release(VirtualKeyShort.CONTROL);
                Thread.Sleep(50);

                Keyboard.Type(value);
                Thread.Sleep(TypingDelayMs);

                Keyboard.Press(VirtualKeyShort.TAB);
                Keyboard.Release(VirtualKeyShort.TAB);
                return null;
            }, timeoutMs: 5000, pollMs: 300);
            Thread.Sleep(WizardStepDelayMs);
        }

        /// <summary>
        /// คลิกลูกศร dropdown "ประเภทพนักงาน" แล้วติ๊กเฉพาะรายการที่ระบุใน checkIds
        /// (ตาม Test Step: บางเคสติ๊ก "รายชั่วโมง+รายเหมา", บางเคสติ๊ก "รายวัน+รายเดือน")
        /// </summary>
        private void SelectEmployeeTypeDropdown(string[] checkIds)
        {
            RetryAction<object?>(() =>
            {
                var dropdown = _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(IdDdlEmployeeType))
                    ?? throw new Exception($"หา dropdown ประเภทพนักงาน '{IdDdlEmployeeType}' ไม่เจอ");
                WaitUntilEnabled(dropdown, timeoutMs: 2000);
                ClickElement(dropdown);
                return null;
            }, timeoutMs: 5000, pollMs: 300);
            Thread.Sleep(WizardStepDelayMs);

            foreach (var checkId in checkIds)
            {
                SetCheckboxState(checkId, shouldCheck: true);
            }

            // ปิด dropdown หลังเลือกเสร็จ (คลิกที่ตัว dropdown อีกครั้ง หรือกด ESC)
            try
            {
                Keyboard.Press(VirtualKeyShort.ESCAPE);
                Keyboard.Release(VirtualKeyShort.ESCAPE);
            }
            catch { }
            Thread.Sleep(WizardStepDelayMs);
        }

        //----
        // * 4. Helper Methods — Element Interaction *
        //----
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
                    ?? _automation.GetDesktop().FindFirstDescendant(cf =>
                        cf.ByName(actionName).And(cf.ByControlType(ControlType.Button)))
                    ?? throw new Exception($"หาปุ่มเมนู '{actionName}' ใน More Action ไม่เจอ");

                WaitUntilEnabled(item, timeoutMs: 2000);
                ClickElement(item);
                return null;
            }, timeoutMs: 5000, pollMs: 300);
            Thread.Sleep(WizardStepDelayMs);
        }

        private void SelectRadioButton(AutomationElement context, string automationId)
        {
            RetryAction<object?>(() =>
            {
                var container = context.FindFirstDescendant(cf => cf.ByAutomationId(automationId))
                    ?? throw new Exception($"หา radio button '{automationId}' ไม่เจอ");
                WaitUntilEnabled(container, timeoutMs: 2000);

                var radioElement = container.FindFirstDescendant(cf => cf.ByControlType(ControlType.RadioButton))
                    ?? container;

                ClickElement(radioElement);
                Thread.Sleep(150);

                if (!IsRadioChecked(radioElement))
                {
                    TrySetRadioChecked(radioElement);
                }
                return null;
            }, timeoutMs: 5000, pollMs: 300);
            Thread.Sleep(WizardStepDelayMs);
        }

        private static bool IsRadioChecked(AutomationElement element)
        {
            try
            {
                var radio = element.AsRadioButton();
                return radio?.IsChecked == true;
            }
            catch { return false; }
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
                {
                    ClickElement(option);
                }
                return null;
            }, timeoutMs: 5000, pollMs: 300);
            Thread.Sleep(WizardStepDelayMs);
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

        // ══════════════════════════════════════════════════════════════════
        // ★ แก้ไข (ปุ่ม Wizard ถูกรายงานเป็น "Not Focusable"):
        // ปุ่มบางตัวใน Wizard นี้ (เช่น "แสดงข้อมูลพนักงาน", "Next >", "Finish")
        // เป็น control ของ DevExpress ที่ UIA3 รายงานว่า "Not Focusable" ทำให้
        // การคลิกผ่าน element.Click() ปกติ (ซึ่งอาศัยการโฟกัสก่อนคลิก) "คลิกไม่ติด"
        // เป็นบางครั้ง โดยเฉพาะเมื่อ Wizard เพิ่งเปลี่ยนสเต็ปและยัง render ไม่นิ่ง
        //
        // จึงเพิ่มช่องทางคลิกที่ "ไม่ต้องพึ่ง keyboard focus" เข้ามาก่อนคลิกด้วย
        // เมาส์/คีย์บอร์ดจริง (เรียงจากไม่รุกรานที่สุดไปหามากที่สุด):
        //   1) LegacyIAccessiblePattern.DoDefaultAction() — สั่งงานปุ่มผ่าน MSAA
        //      โดยตรง ไม่ผ่าน focus เลย ใช้ได้ผลดีกับ control ของ DevExpress
        //      จำนวนมากที่ถูก UIA3 รายงานว่า Not Focusable
        //   2) WM_LBUTTONDOWN/UP ผ่าน Win32 SendMessage ไปยัง native window handle
        //      โดยตรงที่ตำแหน่งกึ่งกลางปุ่ม — ทำงานที่ระดับ HWND จึงไม่ต้องพึ่ง focus
        //      หรือ keyboard input จำลองเลย
        // ถ้าทั้งสองท่านี้ใช้ไม่ได้ (ไม่รองรับ pattern/ไม่มี native handle) จึง fallback
        // ไปคลิกด้วยเมาส์จริงและ Invoke/Toggle/SelectionItem pattern แบบเดิม
        // ══════════════════════════════════════════════════════════════════

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int BM_CLICK = 0x00F5;

        /// <summary>
        /// สั่งคลิกปุ่มผ่าน LegacyIAccessiblePattern.DoDefaultAction() ซึ่งเป็นคนละ
        /// accessibility layer จาก UIA3 (เป็น MSAA) — control ที่ UIA3 รายงานว่า
        /// "Not Focusable" และปฏิเสธ InvokePattern มักยังรับคำสั่งผ่านทางนี้ได้ปกติ
        /// เพราะไม่ต้องอาศัยการโฟกัส element เลย
        /// </summary>
        private static bool TryInvokeViaLegacyPattern(AutomationElement element)
        {
            try
            {
                if (!element.Patterns.LegacyIAccessible.IsSupported) return false;
                element.Patterns.LegacyIAccessible.Pattern.DoDefaultAction();
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// สั่งคลิกปุ่มโดยส่ง Win32 message BM_CLICK ตรงไปยัง native window handle
        /// ของปุ่มนั้น (ถ้ามี) — ทำงานระดับ HWND โดยตรง ไม่ผ่าน UI Automation focus
        /// เลย เหมาะกับปุ่มที่ปฏิเสธ focus/keyboard input ผ่านช่องทางปกติ
        /// ถ้าปุ่มไม่มี native handle ให้ใช้ (เช่น custom control ที่ render เอง)
        /// จะ fallback ไปยิง WM_LBUTTONDOWN/UP ที่ตำแหน่งกึ่งกลางปุ่มแทน
        /// </summary>
        private static bool TryInvokeViaWinApi(AutomationElement element)
        {
            try
            {
                var handleProp = element.Properties.NativeWindowHandle;
                if (!handleProp.IsSupported) return false;

                IntPtr handle = handleProp.ValueOrDefault;
                if (handle == IntPtr.Zero) return false;

                SendMessage(handle, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// คลิกปุ่ม/element แบบพยายามให้ครบทุกช่องทางเท่าที่จะทำได้ก่อนถึงจะยอมแพ้
        /// ลำดับ: Invoke/Toggle/SelectionItem pattern (มาตรฐาน UIA3) → LegacyIAccessible
        /// DoDefaultAction (MSAA, ไม่ต้องพึ่ง focus) → WinAPI BM_CLICK (ระดับ HWND,
        /// ไม่ต้องพึ่ง focus) → element.Click() ปกติ → คลิกเมาส์จริงตามพิกัด
        /// (ท่าสำรองสุดท้ายเผื่อทุกอย่างข้างต้นใช้ไม่ได้เลย)
        /// </summary>
        private static void ClickElement(AutomationElement element)
        {
            try
            {
                if (element.Patterns.Invoke.IsSupported)
                {
                    element.Patterns.Invoke.Pattern.Invoke();
                    return;
                }
                if (element.Patterns.Toggle.IsSupported)
                {
                    element.Patterns.Toggle.Pattern.Toggle();
                    return;
                }
                if (element.Patterns.SelectionItem.IsSupported)
                {
                    element.Patterns.SelectionItem.Pattern.Select();
                    return;
                }
            }
            catch { }

            // ★ ปุ่ม "Not Focusable" มักหลุดมาถึงจุดนี้ (ไม่รองรับ pattern มาตรฐานข้างบน)
            // ลองทางที่ไม่ต้องพึ่ง focus ก่อนที่จะไปคลิกเมาส์จริง
            if (TryInvokeViaLegacyPattern(element)) return;
            if (TryInvokeViaWinApi(element)) return;

            try
            {
                element.Click();
            }
            catch
            {
                var rect = element.BoundingRectangle;
                if (!rect.IsEmpty && rect.Width > 0 && rect.Height > 0)
                {
                    Mouse.MoveTo(rect.Center());
                    Mouse.Click(MouseButton.Left);
                }
            }
        }

        /// <summary>
        /// เวอร์ชัน "Robust" สำหรับปุ่มนำทาง Wizard (Next/Finish/แสดงข้อมูลพนักงาน ฯลฯ)
        /// ★ แก้ไข: เดิมเริ่มจากคลิกเมาส์ตามพิกัดก่อนเป็นอันดับแรก ซึ่งอาศัยการที่ปุ่ม
        /// อยู่ในตำแหน่งที่คลิกได้จริงบนหน้าจอ — แต่ปุ่มที่ถูกรายงานว่า "Not Focusable"
        /// บางครั้งก็ไม่ตอบสนองต่อการคลิกเมาส์ปกติเช่นกัน (โดยเฉพาะช่วงที่ Wizard
        /// เพิ่งเปลี่ยนสเต็ปและ layout ยังไม่นิ่ง) จึงสลับมาลอง LegacyIAccessible กับ
        /// WinAPI BM_CLICK (ไม่ต้องพึ่ง focus/ตำแหน่งบนจอ) ก่อน แล้วค่อย fallback ไป
        /// คลิกเมาส์ตามพิกัดและ InvokePattern ตามลำดับเดิม
        /// </summary>
        private static void ClickButtonRobust(AutomationElement element)
        {
            if (TryInvokeViaLegacyPattern(element)) { Thread.Sleep(50); return; }
            if (TryInvokeViaWinApi(element)) { Thread.Sleep(50); return; }

            bool clicked = false;

            try
            {
                var rect = element.BoundingRectangle;
                if (!rect.IsEmpty && rect.Width > 0 && rect.Height > 0)
                {
                    Mouse.MoveTo(rect.Center());
                    Thread.Sleep(80);
                    Mouse.Click(MouseButton.Left);
                    clicked = true;
                }
            }
            catch { }

            if (!clicked)
            {
                try
                {
                    if (element.Patterns.Invoke.IsSupported)
                    {
                        element.Patterns.Invoke.Pattern.Invoke();
                        clicked = true;
                    }
                }
                catch { }
            }

            if (!clicked)
            {
                try { element.Click(); clicked = true; } catch { }
            }

            if (!clicked)
            {
                try
                {
                    if (element.Patterns.Invoke.IsSupported)
                        element.Patterns.Invoke.Pattern.Invoke();
                }
                catch { }
            }
        }

        //----
        // * 5. Diagnostic Helpers *
        //----
        // เปิด/ปิด diagnostic logging ตรงนี้ — เปิดไว้ก่อนเพื่อ debug ปัญหา Next/แสดงข้อมูลพนักงาน กดไม่ติด
        private const bool DiagnosticLoggingEnabled = true;

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

        /// <summary>
        /// แปลง Exception เป็นข้อความ log แบบละเอียด (ชื่อ type, HResult, ข้อความ, บรรทัดแรกของ stack trace)
        /// ใช้แทน ex.Message ตรงๆ ทุกจุดที่ log ความล้มเหลว จะได้เห็นสาเหตุจริงชัดขึ้น เช่น
        /// แยกออกว่าเป็น element ไม่ enable, timeout, หรือ element หาไม่เจอ
        /// </summary>
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

        /// <summary>
        /// คลิกปุ่มมาตรฐานของ Dialog/Wizard ตามชื่อ (เช่น "Select", "Cancel", "OK", "Yes")
        /// — หาใน context ที่ระบุก่อน แล้วค่อย fallback ไปหาทั้ง Desktop, รอ enabled ก่อนคลิก,
        /// log ทุกขั้นตอนแบบเดียวกับ ClickButtonWithFallback และไม่ throw ถ้าไม่เจอ (คืนค่า false ให้ผู้เรียกไปลองท่าอื่นต่อ)
        /// </summary>
        private bool ClickDialogButton(AutomationElement context, string buttonName, int waitEnabledMs = 2000)
        {
            try
            {
                var button = context.FindFirstDescendant(cf =>
                        cf.ByName(buttonName).And(cf.ByControlType(ControlType.Button)))
                    ?? _automation.GetDesktop().FindFirstDescendant(cf =>
                        cf.ByName(buttonName).And(cf.ByControlType(ControlType.Button)));

                DiagLog($"ClickDialogButton('{buttonName}') -> {DescribeElement(button, "found")}");

                if (button == null)
                    return false;

                bool enabledInTime = WaitUntilEnabled(button, timeoutMs: waitEnabledMs);
                DiagLog($"  enabledInTime={enabledInTime} -> {DescribeElement(button, "before-click")}");

                if (!enabledInTime)
                    return false;

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

        /// <summary>
        /// ท่าสำรองสุดท้ายเมื่อหาปุ่ม Select/OK ด้วยชื่อไม่เจอเลย: คลิกตำแหน่งพิกัดสัมพัทธ์
        /// จากมุมขวาล่างของ Dialog (ตำแหน่งที่ปุ่ม Select มักอยู่ในหน้าจอชุดนี้)
        /// </summary>
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
                        else
                        {
                            DiagLog($"  finder[{i}] เจอ element แต่ IsOffscreen=true — ข้ามไปลอง finder ถัดไป");
                            button ??= candidate; // เก็บไว้เป็น fallback สุดท้ายถ้าไม่เจอตัวอื่นที่ไม่ offscreen เลย
                        }
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

        private void EnsureConditionsTabActive()
        {
            try
            {
                var tab = _mainWindow.FindFirstDescendant(cf =>
                        cf.ByName(ConditionsTabName).And(cf.ByControlType(ControlType.TabItem)))
                    ?? _mainWindow.FindFirstDescendant(cf => cf.ByName(ConditionsTabName));

                if (tab == null)
                    return;

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

        private void SetHolidayDays(params string[] dayIdsToCheck)
        {
            var keep = new HashSet<string>(dayIdsToCheck, StringComparer.OrdinalIgnoreCase);
            foreach (var dayId in AllHolidayDayCheckboxIds)
            {
                SetCheckboxState(dayId, keep.Contains(dayId));
            }
        }

        private void SetCheckboxState(string automationId, bool shouldCheck)
        {
            RetryAction<object?>(() =>
            {
                var element = _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
                if (element == null) return null;

                WaitUntilEnabled(element, timeoutMs: 2000);

                bool current = false;
                try { current = element.AsCheckBox()?.IsChecked == true; } catch { }

                if (current != shouldCheck)
                {
                    ClickElement(element);
                }
                return null;
            }, timeoutMs: 5000, pollMs: 300);
            Thread.Sleep(WizardStepDelayMs);
        }

        private void SelectWorkConfigGridRow(AutomationElement context, string gridAutomationId, string codeText)
        {
            RetryAction<object?>(() =>
            {
                var grid = context.FindFirstDescendant(cf => cf.ByAutomationId(gridAutomationId))
                    ?? throw new Exception($"หา grid '{gridAutomationId}' ไม่เจอ");
                WaitUntilEnabled(grid, timeoutMs: 2000);

                var row = grid.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem))
                        .FirstOrDefault(el => !string.IsNullOrEmpty(el.Name)
                            && el.Name.Contains(codeText, StringComparison.OrdinalIgnoreCase))
                    ?? grid.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
                        .FirstOrDefault(el => !string.IsNullOrEmpty(el.Name)
                            && el.Name.Contains(codeText, StringComparison.OrdinalIgnoreCase));

                if (row == null)
                    throw new Exception($"หาแถวรหัสกะงาน '{codeText}' ใน grid '{gridAutomationId}' ไม่เจอ");

                ClickElement(row);
                return null;
            }, timeoutMs: 5000, pollMs: 300);
            Thread.Sleep(WizardStepDelayMs);
        }

        /// <summary>
        /// เลือกแถวข้อมูลใน popup lookup (พนักงาน/รูปแบบตารางงาน/ปฏิทินวันหยุด) แล้วปิด dialog
        /// ไล่ลองทีละท่าตามลำดับ พร้อม log สถานะ dialog (IsOffscreen) หลังทุกท่า เพื่อดูว่าท่าไหนเป็นตัวที่ปิดสำเร็จจริง:
        /// 1) ดับเบิลคลิกที่แถว → 2) กด ENTER ที่แถว → 3) กดปุ่ม 'Select/&Select/ตกลง/เลือก' ผ่าน ClickDialogButton
        /// → 4) ท่าสำรอง คลิกพิกัดสัมพัทธ์ผ่าน ClickSelectByCoordinateOffset
        /// </summary>
        private void SelectRowAndConfirm(AutomationElement dialog, AutomationElement row, string dialogLabel)
        {
            Thread.Sleep(300);
            DiagLog($"SelectRowAndConfirm[{dialogLabel}] เริ่ม -> {DescribeElement(row, "row")}");

            // 1. คลิกเลือกแถวข้อมูล และดับเบิลคลิกเพื่อเลือกและปิด Dialog ทันที
            try
            {
                var rowRect = row.BoundingRectangle;
                if (!rowRect.IsEmpty)
                {
                    Mouse.MoveTo(rowRect.Center());
                    Thread.Sleep(100);
                    Mouse.Click(MouseButton.Left);
                    Thread.Sleep(100);
                    Mouse.DoubleClick(MouseButton.Left);
                    Thread.Sleep(WizardStepDelayMs);

                    bool closedAfterDoubleClick = dialog.Properties.IsOffscreen.ValueOrDefault;
                    DiagLog($"SelectRowAndConfirm[{dialogLabel}] หลังดับเบิลคลิกแถว -> dialog closed={closedAfterDoubleClick}");
                    if (closedAfterDoubleClick)
                        return;
                }
            }
            catch (Exception ex)
            {
                DiagLog($"SelectRowAndConfirm[{dialogLabel}] ดับเบิลคลิกแถว error: {DescribeException(ex)}");
            }

            // 2. ถ้ายังไม่ปิด ส่งปุ่ม ENTER ที่แถวข้อมูล
            try
            {
                row.Focus();
                Thread.Sleep(50);
                Keyboard.Press(VirtualKeyShort.RETURN);
                Keyboard.Release(VirtualKeyShort.RETURN);
                Thread.Sleep(WizardStepDelayMs);

                bool closedAfterEnter = dialog.Properties.IsOffscreen.ValueOrDefault;
                DiagLog($"SelectRowAndConfirm[{dialogLabel}] หลังกด ENTER -> dialog closed={closedAfterEnter}");
                if (closedAfterEnter)
                    return;
            }
            catch (Exception ex)
            {
                DiagLog($"SelectRowAndConfirm[{dialogLabel}] กด ENTER error: {DescribeException(ex)}");
            }

            // 3. หาปุ่ม 'Select' / '&Select' / 'ตกลง' / 'เลือก' แล้วคลิกผ่าน ClickDialogButton
            //    (มี wait-enabled + log ในตัวอยู่แล้ว เหมือนปุ่ม Next/Finish)
            bool clickedSelectButton =
                   ClickDialogButton(dialog, "Select", waitEnabledMs: 800)
                || ClickDialogButton(dialog, "&Select", waitEnabledMs: 500)
                || ClickDialogButton(dialog, "ตกลง", waitEnabledMs: 500)
                || ClickDialogButton(dialog, "เลือก", waitEnabledMs: 500);

            if (clickedSelectButton)
            {
                bool closedAfterSelectButton = dialog.Properties.IsOffscreen.ValueOrDefault;
                DiagLog($"SelectRowAndConfirm[{dialogLabel}] หลังกดปุ่ม Select/เลือก -> dialog closed={closedAfterSelectButton}");
                if (closedAfterSelectButton)
                    return;
            }
            else
            {
                DiagLog($"SelectRowAndConfirm[{dialogLabel}] ไม่เจอปุ่ม Select/&Select/ตกลง/เลือก ที่ enable เลย -> ไปท่าสำรอง (พิกัด)");
            }

            // 4. ท่าสำรอง: ยิงคลิกพิกัดสัมพัทธ์ตรงตำแหน่งปุ่ม Select ด้านขวาล่างของ Dialog
            ClickSelectByCoordinateOffset(dialog);

            bool closedAfterCoordinateClick = false;
            try { closedAfterCoordinateClick = dialog.Properties.IsOffscreen.ValueOrDefault; } catch { }
            DiagLog($"SelectRowAndConfirm[{dialogLabel}] หลังคลิกพิกัดสำรอง -> dialog closed={closedAfterCoordinateClick}");
        }

        //----
        // * 6. Lookup Popup Dialogs (Employee / Work Schedule Pattern / Holiday Calendar) *
        //----
        private void SelectEmployeeByCode(string employeeCode)
        {
            ClickWizardButton(IdBtnEmployee);
            Thread.Sleep(WizardStepDelayMs + 300);

            var employeeDialog = RetryFind(() =>
                _automation.GetDesktop().FindFirstChild(cf => cf.ByControlType(ControlType.Window))
                ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Window)),
                timeoutMs: 4000) ?? throw new Exception("หา Dialog เลือกพนักงานไม่เจอ");

            var row = FindDialogRowByCode(employeeDialog, employeeCode, "Dialog เลือกพนักงาน");
            SelectRowAndConfirm(employeeDialog, row, "Dialog เลือกพนักงาน");
        }

        private void SelectHolidayCalendar(string calendarCode)
        {
            ClickWizardButton(IdBtnHolidaySche);
            Thread.Sleep(WizardStepDelayMs + 300);

            var calendarDialog = RetryFind(() =>
                _automation.GetDesktop().FindFirstChild(cf => cf.ByControlType(ControlType.Window))
                ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Window)),
                timeoutMs: 4000) ?? throw new Exception("หา Dialog เลือกปฏิทินวันหยุดไม่เจอ");

            var row = FindDialogRowByCode(calendarDialog, calendarCode, "Dialog ปฏิทินวันหยุด");
            SelectRowAndConfirm(calendarDialog, row, "Dialog ปฏิทินวันหยุด");
        }

        private void SelectWorkSchedulePattern(string patternCode)
        {
            var container = RetryFind(() => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(IdBtnWorkSchedulePattern)),
                timeoutMs: 5000, pollMs: 300)
                ?? throw new Exception($"หา container '...' ของ 'รูปแบบตารางการทำงาน' ({IdBtnWorkSchedulePattern}) ไม่เจอ");

            var btn = container.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button)) ?? container;
            ClickElement(btn);

            Thread.Sleep(WizardStepDelayMs + 300);

            var patternDialog = RetryFind(() =>
                _automation.GetDesktop().FindFirstChild(cf => cf.ByControlType(ControlType.Window))
                ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Window)),
                timeoutMs: 4000) ?? throw new Exception("หา Dialog เลือกรูปแบบตารางการทำงานไม่เจอ");

            var row = FindDialogRowByCode(patternDialog, patternCode, "Dialog เลือกรูปแบบตารางการทำงาน");
            SelectRowAndConfirm(patternDialog, row, "Dialog เลือกรูปแบบตารางการทำงาน");
        }

        private void ClickWizardButton(string automationId, int timeoutMs = WizardButtonRetryTimeoutMs, string? fallbackName = null)
        {
            ClickButtonWithFallback(
                timeoutMs,
                $"หาปุ่ม '{automationId}' ไม่เจอ",
                () => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(automationId)),
                () => _automation.GetDesktop().FindFirstDescendant(cf => cf.ByAutomationId(automationId)),
                () => fallbackName != null ? _mainWindow.FindFirstDescendant(cf => cf.ByName(fallbackName)) : null,
                () => fallbackName != null ? _automation.GetDesktop().FindFirstDescendant(cf => cf.ByName(fallbackName)) : null
            );
            Thread.Sleep(WizardStepDelayMs);
        }

        private void ClickWizardNavButton(string buttonName, int timeoutMs = WizardButtonRetryTimeoutMs)
        {
            DismissErrorDialogIfPresent();

            ClickButtonWithFallback(
                timeoutMs,
                $"หาปุ่ม '{buttonName}' ไม่เจอ",
                () => _mainWindow.FindFirstDescendant(cf => cf.ByName(buttonName).And(cf.ByControlType(ControlType.Button))),
                () => _automation.GetDesktop().FindFirstDescendant(cf => cf.ByName(buttonName).And(cf.ByControlType(ControlType.Button)))
            );

            // ★ เพิ่มใหม่: รอเผื่อประมวลผลจริงๆ ก่อนไปเช็คอะไรต่อ (ดู WizardProcessingDelayMs ด้านบน)
            DiagLog($"ClickWizardNavButton('{buttonName}'): รอ {WizardProcessingDelayMs}ms ให้แอปประมวลผลก่อน");
            Thread.Sleep(WizardProcessingDelayMs);

            WaitForWizardStepSettled();
            DismissErrorDialogIfPresent();
        }

        private void WaitForWizardStepSettled(int timeoutMs = WizardStepSettleTimeoutMs)
        {
            var sw = Stopwatch.StartNew();
            DiagLog($"WaitForWizardStepSettled เริ่มรอ (timeout {timeoutMs}ms)");
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                var nextInMain = _mainWindow.FindFirstDescendant(cf => cf.ByName(NextButtonName).And(cf.ByControlType(ControlType.Button)));
                var finishInMain = _mainWindow.FindFirstDescendant(cf => cf.ByName(FinishButtonName).And(cf.ByControlType(ControlType.Button)));
                var nextInDesktop = _automation.GetDesktop().FindFirstDescendant(cf => cf.ByName(NextButtonName).And(cf.ByControlType(ControlType.Button)));
                var finishInDesktop = _automation.GetDesktop().FindFirstDescendant(cf => cf.ByName(FinishButtonName).And(cf.ByControlType(ControlType.Button)));

                bool hasNav = nextInMain != null || finishInMain != null || nextInDesktop != null || finishInDesktop != null;

                if (hasNav)
                {
                    DiagLog($"WaitForWizardStepSettled เจอปุ่มนำทางแล้ว (elapsed {sw.ElapsedMilliseconds}ms): " +
                            $"{DescribeElement(nextInMain, "Next@main")} | {DescribeElement(finishInMain, "Finish@main")} | " +
                            $"{DescribeElement(nextInDesktop, "Next@desktop")} | {DescribeElement(finishInDesktop, "Finish@desktop")}");
                    Thread.Sleep(250);
                    return;
                }
                Thread.Sleep(150);
            }
            DiagLog($"WaitForWizardStepSettled หมดเวลา ({timeoutMs}ms) โดยไม่เจอปุ่มนำทางเลย -> fallback sleep {PageLoadDelayMs}ms");
            Thread.Sleep(PageLoadDelayMs);
        }

        /// <summary>
        /// ★ เพิ่มพารามิเตอร์ใหม่ firstNextClickExtraWaitMs: รอพิเศษ (ค่าเริ่มต้น 0 = ไม่รอเพิ่ม)
        /// เฉพาะตอนคลิก "Next" ครั้งแรกของ wizard นี้เท่านั้น (i == 0) — ใช้สำหรับหน้าจอ
        /// "เลือกช่วงข้อมูลพนักงาน" ที่พอกด Next แล้วแอปต้องประมวลผลสร้าง dataset พนักงาน
        /// (GenIDGroupAndGenerateDatetime) ซึ่งใช้เวลานาน ถ้าไม่รอพอจะเจอ NullReferenceException
        /// ตั้งเป็นค่าน้อยๆ (หรือ 0) สำหรับ wizard อื่นที่ไม่ต้องรอนาน จะได้ไม่ทำให้ทั้งชุดเทสช้าเกินจำเป็น
        /// </summary>
        private void CompleteWizard(int maxNextClicks = 10, int firstNextClickExtraWaitMs = 0)
        {
            for (int i = 0; i < maxNextClicks; i++)
            {
                DiagLog($"CompleteWizard loop #{i}");
                DismissErrorDialogIfPresent();

                var finishBtn = _mainWindow.FindFirstDescendant(cf =>
                        cf.ByName(FinishButtonName).And(cf.ByControlType(ControlType.Button)))
                    ?? _mainWindow.FindFirstDescendant(cf =>
                        cf.ByName("เสร็จสิ้น").And(cf.ByControlType(ControlType.Button)))
                    ?? _automation.GetDesktop().FindFirstDescendant(cf =>
                        cf.ByName(FinishButtonName).And(cf.ByControlType(ControlType.Button)))
                    ?? _automation.GetDesktop().FindFirstDescendant(cf =>
                        cf.ByName("เสร็จสิ้น").And(cf.ByControlType(ControlType.Button)));

                DiagLog($"  {DescribeElement(finishBtn, "finishBtn")}");

                bool finishEnabled = false;
                try { finishEnabled = finishBtn != null && finishBtn.Properties.IsEnabled.ValueOrDefault; } catch { }

                if (finishBtn != null && finishEnabled)
                {
                    DiagLog("  finishBtn enabled -> คลิก Finish");
                    WaitUntilEnabled(finishBtn, timeoutMs: 2000);
                    ClickButtonRobust(finishBtn);

                    // ★ เพิ่มใหม่: รอเผื่อประมวลผลจริงๆ ก่อนเช็ค error dialog — จุดนี้แหละที่แอปมักพัง
                    // ด้วย NullReferenceException ถ้าเราเช็คเร็วเกินไป
                    DiagLog($"  รอ {WizardProcessingDelayMs}ms ให้แอปประมวลผลหลังคลิก Finish");
                    Thread.Sleep(WizardProcessingDelayMs);

                    DiagLog($"  หลังคลิก Finish -> {DescribeElement(finishBtn, "finishBtn-after")}");
                    DismissErrorDialogIfPresent();
                    return;
                }

                var nextBtn = _mainWindow.FindFirstDescendant(cf =>
                        cf.ByName(NextButtonName).And(cf.ByControlType(ControlType.Button)))
                    ?? _automation.GetDesktop().FindFirstDescendant(cf =>
                        cf.ByName(NextButtonName).And(cf.ByControlType(ControlType.Button)));

                DiagLog($"  {DescribeElement(nextBtn, "nextBtn")}");

                if (nextBtn == null)
                {
                    DiagLog("  nextBtn เป็น null รอบแรก -> เรียก WaitForWizardStepSettled เพื่อรอเพิ่ม");
                    WaitForWizardStepSettled(timeoutMs: WizardButtonRetryTimeoutMs);

                    nextBtn = _mainWindow.FindFirstDescendant(cf =>
                            cf.ByName(NextButtonName).And(cf.ByControlType(ControlType.Button)))
                        ?? _automation.GetDesktop().FindFirstDescendant(cf =>
                            cf.ByName(NextButtonName).And(cf.ByControlType(ControlType.Button)));

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

                // ★ เพิ่มใหม่: ถ้าเป็นการคลิก Next ครั้งแรก (i==0) และมีการระบุ firstNextClickExtraWaitMs
                // ไว้ ให้รอพิเศษยาวกว่าปกติ (เช่น 60 วินาที) เพราะจุดนี้คือจุดที่แอปต้องประมวลผล
                // สร้าง dataset พนักงานซึ่งใช้เวลานาน — ก่อนจะไปเช็ค error dialog ใดๆ เลย
                int waitMs = (i == 0 && firstNextClickExtraWaitMs > 0) ? firstNextClickExtraWaitMs : WizardProcessingDelayMs;
                DiagLog($"  รอ {waitMs}ms ให้แอปประมวลผลหลังคลิก Next (ครั้งที่ #{i}{(i == 0 && firstNextClickExtraWaitMs > 0 ? " - ใช้ extra wait" : "")})");
                Thread.Sleep(waitMs);

                DiagLog($"  หลังคลิก Next -> {DescribeElement(nextBtn, "nextBtn-after-click")}");
                WaitForWizardStepSettled();
                DismissErrorDialogIfPresent();
            }
        }

        private void DismissErrorDialogIfPresent()
        {
            try
            {
                var okButton = _automation.GetDesktop().FindFirstDescendant(cf => cf.ByAutomationId("btnOK"))
                    ?? _automation.GetDesktop().FindFirstDescendant(cf =>
                        cf.ByName("OK").Or(cf.ByName("&OK")).Or(cf.ByName("ตกลง")).And(cf.ByControlType(ControlType.Button)));

                if (okButton != null && okButton.Properties.IsEnabled.ValueOrDefault)
                {
                    ClickElement(okButton);
                    Thread.Sleep(PageLoadDelayMs);
                }
            }
            catch { }
        }

        private void ClickCheckbox(string automationId, string? fallbackName = null)
        {
            RetryAction<object?>(() =>
            {
                var element = _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(automationId))
                    ?? _automation.GetDesktop().FindFirstDescendant(cf => cf.ByAutomationId(automationId))
                    ?? (fallbackName != null
                        ? (_mainWindow.FindFirstDescendant(cf => cf.ByName(fallbackName))
                            ?? _automation.GetDesktop().FindFirstDescendant(cf => cf.ByName(fallbackName)))
                        : null)
                    ?? throw new Exception($"หา checkbox '{automationId}' ไม่เจอ");
                WaitUntilEnabled(element, timeoutMs: 2000);

                try
                {
                    var checkbox = element.AsCheckBox();
                    if (checkbox != null)
                    {
                        checkbox.IsChecked = true;
                        return null;
                    }
                }
                catch { }

                ClickElement(element);
                return null;
            }, timeoutMs: 5000, pollMs: 300);
            Thread.Sleep(WizardStepDelayMs);
        }

        /// <summary>
        /// ติ๊กเงื่อนไขการเลือกช่วงข้อมูลพนักงาน ตาม Test Step: หน่วยงาน / ตำแหน่งงาน / กลุ่มพนักงาน / ระดับพนักงาน
        /// (แก้จากเดิมที่ติ๊ก org/position/employeeType ให้ตรงกับ 4 รายการที่ระบุใน Test Step แทน)
        /// *** IdChkEmpGroupAll และ IdChkEmployeeLevelAll เป็น AutomationId ที่เดาไว้ ต้องตรวจสอบก่อนใช้งานจริง ***
        /// </summary>
        private void SelectEmployeeFilterConditions()
        {
            EnsureConditionsTabActive();
            ClickCheckbox(IdChkOrgAll);
            ClickCheckbox(IdChkPositionAll);
            ClickCheckbox(IdChkEmpGroupAll);
            ClickCheckbox(IdChkEmployeeLevelAll);
        }

        /// <summary>
        /// ★ เพิ่มใหม่: คลิกปุ่ม "รีเช็คข้อมูลพนักงาน" (ถ้ามีในหน้าจอ) เพื่อบังคับให้แอปสร้าง/sync
        /// dataset รายชื่อพนักงานภายในให้เสร็จสมบูรณ์ก่อนกด Next — ป้องกัน NullReferenceException
        /// ที่ GenIDGroupAndGenerateDatetime ซึ่งเกิดจาก race condition ตอน automation กด Next
        /// เร็วเกินไปหลังกด "แสดงข้อมูลพนักงาน" (ปัญหานี้ไม่เกิดตอนทดสอบมือเพราะมีจังหวะหน่วงตามธรรมชาติ)
        /// ไม่ throw ถ้าไม่เจอปุ่ม เพราะบางหน้าจอ/บางเวอร์ชันอาจไม่มีปุ่มนี้
        /// </summary>
        private void ClickRecheckEmployeeDataIfPresent()
        {
            try
            {
                var btn = _mainWindow.FindFirstDescendant(cf => cf.ByName(BtnRecheckEmployeeDataName))
                    ?? _automation.GetDesktop().FindFirstDescendant(cf => cf.ByName(BtnRecheckEmployeeDataName));

                if (btn == null)
                {
                    DiagLog("ClickRecheckEmployeeDataIfPresent: ไม่เจอปุ่ม 'รีเช็คข้อมูลพนักงาน' -> ข้าม (รอ buffer เฉยๆ)");
                    Thread.Sleep(800);
                    return;
                }

                DiagLog("ClickRecheckEmployeeDataIfPresent: เจอปุ่ม 'รีเช็คข้อมูลพนักงาน' -> คลิกเพื่อบังคับ sync ข้อมูลก่อนกด Next");
                WaitUntilEnabled(btn, timeoutMs: 1500);
                ClickButtonRobust(btn);
                Thread.Sleep(800);
            }
            catch (Exception ex)
            {
                DiagLog($"ClickRecheckEmployeeDataIfPresent error: {DescribeException(ex)}");
                Thread.Sleep(800);
            }
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

                    Keyboard.Press(VirtualKeyShort.CONTROL);
                    Keyboard.Press(VirtualKeyShort.KEY_A);
                    Keyboard.Release(VirtualKeyShort.KEY_A);
                    Keyboard.Release(VirtualKeyShort.CONTROL);
                    Thread.Sleep(50);

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
        }

        private AutomationElement FindDialogRowByCode(AutomationElement dialog, string code, string dialogLabel, int timeoutMs = 6000)
        {
            var row = RetryFind(() => FindRowByScanningVisibleElements(dialog, code), timeoutMs: timeoutMs, pollMs: 300);

            if (row == null)
                throw new Exception($"หาแถวรายการรหัส '{code}' ใน {dialogLabel} ไม่เจอ");

            return row;
        }

        private AutomationElement? FindRowByScanningVisibleElements(AutomationElement dialog, string code)
        {
            return dialog.FindFirstDescendant(cf => cf.ByName(code).Or(cf.ByAutomationId(code)))
                ?? dialog.FindFirstDescendant(cf => cf.ByName("รหัสตารางการทำงาน row 0"))
                ?? dialog.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem))
                    .FirstOrDefault(el => !string.IsNullOrEmpty(el.Name)
                        && (el.Name.Contains(code, StringComparison.OrdinalIgnoreCase) || el.Name.Contains("row 0", StringComparison.OrdinalIgnoreCase)))
                ?? dialog.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
                    .FirstOrDefault(el => !string.IsNullOrEmpty(el.Name)
                        && el.Name.Contains(code, StringComparison.OrdinalIgnoreCase));
        }

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

        //----
        // * 7. Generic Retry / Wait Helpers *
        //----
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
                try
                {
                    return action();
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    Thread.Sleep(pollMs);
                }
            }
            throw lastEx ?? new Exception("การกระทำไม่สำเร็จ (หมดเวลารอโดยไม่ทราบสาเหตุ)");
        }

        private void FillTextBox(AutomationElement context, string automationId, string text)
        {
            RetryAction<object?>(() =>
            {
                var txt = context.FindFirstDescendant(cf => cf.ByAutomationId(automationId))?.AsTextBox()
                    ?? throw new Exception($"หาช่องกรอก '{automationId}' ไม่เจอ");
                bool enabledInTime = WaitUntilEnabled(txt, timeoutMs: 3000, pollMs: 300);
                if (!enabledInTime)
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
                var container = context.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
                if (container == null) return null;

                WaitUntilEnabled(container, timeoutMs: 2000);
                var editBox = container.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)) ?? container;

                ClickElement(editBox);
                Thread.Sleep(100);

                Keyboard.Press(VirtualKeyShort.CONTROL);
                Keyboard.Press(VirtualKeyShort.KEY_A);
                Keyboard.Release(VirtualKeyShort.KEY_A);
                Keyboard.Release(VirtualKeyShort.CONTROL);
                Thread.Sleep(50);

                Keyboard.Type(value);
                Thread.Sleep(TypingDelayMs);

                Keyboard.Press(VirtualKeyShort.TAB);
                Keyboard.Release(VirtualKeyShort.TAB);
                return null;
            }, timeoutMs: 3000, pollMs: 300);
        }

        private void FillDateField(AutomationElement context, string automationId, DateTime date, bool useBuddhistYear = false)
        {
            string year = useBuddhistYear
                ? (date.Year + 543).ToString()
                : date.Year.ToString();
            string formatted = $"{date.Day:D2}/{date.Month:D2}/{year}";

            RetryAction<object?>(() =>
            {
                var container = context.FindFirstDescendant(cf => cf.ByAutomationId(automationId))
                    ?? throw new Exception($"หาช่องวันที่ '{automationId}' ไม่เจอ");

                bool enabledInTime = WaitUntilEnabled(container, timeoutMs: 3000, pollMs: 300);
                if (!enabledInTime)
                    throw new Exception($"ช่องวันที่ '{automationId}' ยัง disabled อยู่หลังรอ");

                var editBox = container.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)) ?? container;

                ClickElement(editBox);
                Thread.Sleep(100);

                Keyboard.Press(VirtualKeyShort.CONTROL);
                Keyboard.Press(VirtualKeyShort.KEY_A);
                Keyboard.Release(VirtualKeyShort.KEY_A);
                Keyboard.Release(VirtualKeyShort.CONTROL);
                Thread.Sleep(50);

                Keyboard.Type(new string(formatted.Where(char.IsDigit).ToArray()));
                Thread.Sleep(TypingDelayMs);

                Keyboard.Press(VirtualKeyShort.TAB);
                Keyboard.Release(VirtualKeyShort.TAB);
                return null;
            }, timeoutMs: FieldBindRetryTimeoutMs, pollMs: FieldBindRetryPollMs);
        }

        //----
        // * 8. Logging Counters *
        //----
        private void LogSuccessCounted(string testName, string message)
        {
            _totalSuccess++;
            TestLogger.LogSuccess(testName, message);
        }

        private void LogFailureCounted(string testName, string message)
        {
            _totalFail++;
            TestLogger.LogFailure(testName, message);
        }

        // ══════════════════════════════════════════════════════════════════
        // ★ เพิ่มใหม่ — * 9. Ensure Application Exit *
        // ปัญหาเดิม: หลังรันเทสเสร็จ แอปบางครั้งค้าง ไม่ยอมปิด ("Application failed to exit")
        // เพราะ BaseTest.cs (ที่ใช้ร่วมกับเพื่อนร่วมทีม) เรียก app.Close() แค่ "ขอปิด" โดยไม่เช็ค
        // ผลจริง — จึงเพิ่มการันตีการปิดแอปไว้ในไฟล์นี้แทน โดยไม่แตะ BaseTest.cs เลย
        //
        // วิธีทำงาน: ใช้ _mainWindow ที่มีอยู่แล้วเพื่อหา ProcessId ของแอป แล้ว
        //   1) ลองปิดแบบสุภาพก่อน (คลิกปุ่ม "Exit Application" ถ้ามี หรือส่ง WM_CLOSE ไปยัง
        //      native window handle โดยตรง)
        //   2) รอเช็คจริงว่า process ปิดหรือยัง (poll ทุก 200ms)
        //   3) ถ้าไม่ปิดภายในเวลาที่กำหนด -> บังคับ Process.Kill(entireProcessTree: true) ทันที
        //
        // ปลอดภัยเรียกซ้ำได้: ถ้า BaseTest.cs ปิดแอปสำเร็จไปแล้ว เมธอดนี้จะเจอว่า
        // process ปิดไปแล้วและไม่ทำอะไรเพิ่ม
        // ══════════════════════════════════════════════════════════════════

        private const int AppExitCloseTimeoutMs = 5000;
        private const int AppExitKillWaitMs = 3000;

        /// <summary>
        /// การันตีว่าแอปพลิเคชันถูกปิดจริงหลังรันเทส — ปิดแบบสุภาพก่อน ถ้าไม่สำเร็จภายในเวลาที่
        /// กำหนดจะบังคับ Kill process ทันที เพื่อไม่ให้ค้างเหมือนที่เจอปัญหา "Application failed to exit"
        /// </summary>
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

            // 1. ลองปิดแบบสุภาพก่อน — คลิกปุ่ม "Exit Application" ถ้ามี ไม่งั้นส่ง WM_CLOSE ตรงไปยัง window
            try
            {
                var exitButton = _mainWindow.FindFirstDescendant(cf => cf.ByName("Exit Application"))
                    ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("ออกจากโปรแกรม"));

                if (exitButton != null)
                {
                    DiagLog("EnsureApplicationExits: เจอปุ่ม Exit Application -> คลิก");
                    ClickButtonRobust(exitButton);
                    Thread.Sleep(300);

                    // อาจมี dialog ยืนยันโผล่มา ("บันทึกก่อนออกไหม?" ฯลฯ) ลองกดตามลำดับ
                    ClickDialogButton(_automation.GetDesktop(), "Yes", waitEnabledMs: 1000);
                    ClickDialogButton(_automation.GetDesktop(), "OK", waitEnabledMs: 500);
                    ClickDialogButton(_automation.GetDesktop(), "&OK", waitEnabledMs: 500);
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

            // 2. รอเช็คจริงว่า process ปิดหรือยัง (poll ทุก 200ms ภายในไทม์เอาต์ที่กำหนด)
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

            // 3. ยังไม่ปิดภายในเวลาที่กำหนด -> บังคับ Kill ทันที ไม่ปล่อยให้ค้าง
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

        /// <summary>
        /// ส่ง WM_CLOSE ไปยัง native window handle ของ main window โดยตรง (ระดับ HWND)
        /// ใช้เป็นทางเลือกเมื่อไม่เจอปุ่ม "Exit Application" บนหน้าจอ
        /// </summary>
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