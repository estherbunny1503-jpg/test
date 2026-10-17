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
        private string _activeTestName = "TestCase_EmpWorkScheListUC";
        private int _stepLogIndex;

        // ── Delay Constants ──
        private const int MenuTransitionDelayMs = 150;
        private const int PageLoadDelayMs = 300;
        private const int WizardStepDelayMs = 200;
        private const int ScrollStepDelayMs = 250;
        private const int TypingDelayMs = 50;
        private const int FieldBindRetryTimeoutMs = 15000;
        private const int FieldBindRetryPollMs = 250;

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

        private const string IdChkReplaceHoliday = "devChkReplaceByHoliday";

        private const string IdBtnBrowse = "devBtnBrowse";
        private const string IdBtnShowEmployeeData = "devBtnShowEmp";

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
        private const string IdChkCancelWorkHoliday = "devChkDelHoliday";        // ผลที่คาดหวัง: ยกเลิกวันหยุดกะ

        private const string ActionSetScheduleForGroup = "กำหนดตารางการทำงานของพนักงานแบบรวม";
        private const string ActionImportSchedule = "Import ตารางการทำงานของพนักงาน";
        private const string ActionCopyScheduleOfEmployee = "คัดลอกตารางการทำงานของพนักงาน";
        private const string ActionCancelScheduleForGroup = "ยกเลิกตารางการทำงานของพนักงานแบบรวม";

        private const string ConditionsTabName = "กำหนดเงื่อนไข";
        private const string IdChkCancelSchedule = "devChkDelShift";               // ผลที่คาดหวัง: ยกเลิกกะงาน
        private const string SampleEmployeeCodeForCopyShortcut = "EMP00001"; // เดิมพิมพ์ผิดเป็น EMP0001 (ไม่มีพนักงานรหัสนี้)

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
            // เปิดหน้าจอด้วยช่องค้นหาเมนู แบบเดียวกับโมดูลก่อนหน้า (ไม่คลิกแถวเมนูตามลำดับ)
            MenuSearch.Open(_mainWindow, EmpWorkScheListMenuLabel);
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

            // เรียงและกำหนดค่าตาม TestStep.ods ชีท "บันทึกรูปแบบตารางการทำงาน" — วันที่ในไฟล์เป็น พ.ศ. (ลบ 543)
            var scenarios = new (string TestName, Action Step)[]
            {
                // ── 1. ทดสอบ NEW กำหนดตารางการทำงานของพนักงานแบบรวม (แถว 7-8) ──
                ("TestCase01_SetScheduleForGroup_General",
                    () => SetScheduleForGroup(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31),
                        "กำหนดตารางการทำงานแบบทั่วไป", "WSG001")),
                ("TestCase01_SetScheduleForGroup_Rotation",
                    () => SetScheduleForGroup(new DateTime(2026, 2, 1), new DateTime(2026, 3, 31),
                        "กำหนดตารางการทำงานแบบวนกะงาน", "WSG002")),

                // ── 1. คัดลอกตารางการทำงานของพนักงาน (radio ในวิซาร์ดแบบรวม, แถว 11) ──
                ("TestCase01_CopyScheduleShortcut",
                    () => CopyScheduleShortcut(new DateTime(2026, 1, 3), new DateTime(2026, 12, 3))),

                // ── 1. กำหนดวันหยุด (แถว 14) ──
                ("TestCase01_SetHoliday",
                    () => SetHoliday(new DateTime(2026, 6, 1), new DateTime(2026, 12, 31))),

                // ── 2. Import ตารางการทำงานของพนักงาน (แถว 21: ชุดเดียว) ──
                ("TestCase02_Import",
                    () => ImportEmployeeWorkSchedule(new DateTime(2026, 5, 15), new DateTime(2026, 6, 15))),

                // ── 3. คัดลอกตารางการทำงานของพนักงาน: เลือก EMP00001 ในตารางก่อน แล้ว More Action (แถว 32) ──
                ("TestCase03_CopyScheduleOfEmployee",
                    () => CopyScheduleOfEmployee(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), "EMP00001")),

                // ── 4. ยกเลิกตารางการทำงานของพนักงานแบบรวม (แถว 42-43) ──
                ("TestCase04_CancelScheduleForGroup_1",
                    () => CancelScheduleForGroup(
                        start: new DateTime(2026, 9, 1), end: new DateTime(2026, 12, 31),
                        workGroupFrom: "NRM-M01", workGroupTo: "OT-EVN02",
                        orgFrom: "001", orgTo: "ORG005",
                        empGroupFrom: "EG-001", empGroupTo: "EG-004",
                        empTypeChecks: new[] { IdChkEmpTypeHourly, IdChkEmpTypeContract },
                        positionFrom: "DW001", positionTo: "POS005",
                        levelFrom: "EL-001", levelTo: "EL-005",
                        empCodeFrom: "EMP00001", empCodeTo: "EMP00004",
                        cancelWorkSchedule: true, cancelWorkHoliday: true)),
                ("TestCase04_CancelScheduleForGroup_2",
                    () => CancelScheduleForGroup(
                        start: new DateTime(2026, 8, 3), end: new DateTime(2026, 9, 3),
                        workGroupFrom: null, workGroupTo: null, // TestStep: ข้ามขั้นตอนนี้ไป
                        orgFrom: "001", orgTo: "ORG005",
                        empGroupFrom: "EG-001", empGroupTo: "EG-004",
                        empTypeChecks: new[] { IdChkEmpTypeDaily, IdChkEmpTypeMonthly },
                        positionFrom: "DW001", positionTo: "POS005",
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
            _activeTestName = testName;
            _stepLogIndex = 0;
            try
            {
                step();
                LogSuccessCounted(testName, "ทำรายการตามขั้นตอนที่กำหนดสำเร็จครบถ้วน");
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

        private void LogStep(string message) =>
            TestLogger.LogSuccess($"{_activeTestName}_Step{++_stepLogIndex:00}", message);

        /* ปิดวิซาร์ดที่ค้างหลังจบเคส — กดเฉพาะปุ่มของหน้าต่าง HRMI ที่เป็นวิซาร์ดจริง
           (เดิมหาปุ่มชื่อ "Cancel" ที่ไหนก็ได้บน desktop แล้วกด เคยไปโดนปุ่มที่ไม่ใช่วิซาร์ด ทำให้ HRMI ค้างทั้งเคสถัดไป) */
        private void CloseAnyOpenWizardDialog()
        {
            try
            {
                DismissErrorDialogIfPresent();
                WaitForHrmiIdle();

                var wizard = FindHrmiWindows().FirstOrDefault(w =>
                    w.FindFirstDescendant(cf => cf.ByName(NextButtonName).Or(cf.ByName(FinishButtonName)).Or(cf.ByName("< Back"))) != null);
                if (wizard == null) return;

                if (!ClickDialogButton(wizard, "Cancel", waitEnabledMs: 500)) return;
                Thread.Sleep(PageLoadDelayMs);

                // หลังกด Cancel อาจมีกล่องยืนยันของ HRMI โผล่มา
                foreach (var box in FindHrmiWindows())
                {
                    if (ClickDialogButton(box, "Yes", waitEnabledMs: 500) || ClickDialogButton(box, "OK", waitEnabledMs: 500)) break;
                }
            }
            catch (Exception ex)
            {
                DiagLog($"CloseAnyOpenWizardDialog error: {DescribeException(ex)}");
            }
        }

        /* หา control ตาม AutomationId: ค้นในวิซาร์ด/Pop-up ของ HRMI ก่อน (เร็ว) แล้วค่อยค้นใน context (หน้าหลักทั้งหน้า ช้า) */
        private AutomationElement? FindById(AutomationElement context, string automationId) =>
            (ReferenceEquals(context, _mainWindow) ? FindFirstInHrmi(cf => cf.ByAutomationId(automationId)) : null)
            ?? context.FindFirstDescendant(cf => cf.ByAutomationId(automationId));

        /* หาปุ่มนำทางของวิซาร์ด (Next/Finish): ค้นในหน้าต่างวิซาร์ดก่อน (เล็ก เร็ว) แล้วค่อยค้นทั้งหน้าหลัก
           — หน้าหลักมีตารางพนักงานทั้งหน้า ค้นทีละครั้งเป็นวินาที และเดิมค้นหลายรอบต่อขั้น ทำให้ทั้งเทสช้ามาก */
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
           ไม่รวมหน้าหลัก และไม่รวมหน้าจอที่เปิดเป็นแท็บใน HRMI (MDI เช่น "บันทึกรูปแบบตารางการทำงาน")
           — หน้าจอแท็บมีตารางเหมือน Pop-up ถ้านับรวม เทสจะคิดว่า Pop-up ยังเปิดอยู่ตลอด */
        private IEnumerable<AutomationElement> FindHrmiWindows()
        {
            int pid = _mainWindow.Properties.ProcessId.ValueOrDefault;
            var mainHandle = _mainWindow.Properties.NativeWindowHandle.ValueOrDefault;

            static bool IsModal(AutomationElement w)
            {
                try { return w.Patterns.Window.IsSupported && w.Patterns.Window.Pattern.IsModal.ValueOrDefault; }
                catch { return false; }
            }
            // หน้าจอแท็บ (MDI) มี AutomationId เป็นตัวเลข ("1", "2") ส่วนวิซาร์ด/Pop-up ไม่มี — ใช้แยกแบบไม่ต้องค้นข้างใน
            // (เดิมค้นปุ่ม Next ในทุกหน้าต่าง รวมหน้าจอแท็บที่มีตารางพนักงานทั้งหน้า ช้ามากและทำทุกครั้งที่หาปุ่ม)
            static bool IsPopupLike(AutomationElement w)
            {
                try { return IsModal(w) || string.IsNullOrEmpty(w.AutomationId) || !w.AutomationId.All(char.IsDigit); }
                catch { return false; }
            }

            // หน้าต่างระดับบนของ HRMI (popup / dropdown ไม่ใช่ modal แต่อยู่ระดับบน) + หน้าต่าง modal ที่อยู่ใต้หน้าหลัก
            return _automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(pid))
                .Concat(_mainWindow.FindAllChildren(cf => cf.ByControlType(ControlType.Window)).Where(IsPopupLike)
                    .SelectMany(w => new[] { w }.Concat(SafeChildWindows(w)))) // Pop-up อาจซ้อนอยู่ใต้วิซาร์ด (ลงไปอีกชั้นเดียว)
                .Where(w => { try { return w.Properties.NativeWindowHandle.ValueOrDefault != mainHandle; } catch { return false; } })
                .ToList();
        }

        private static IEnumerable<AutomationElement> SafeChildWindows(AutomationElement w)
        {
            try { return w.FindAllChildren(cf => cf.ByControlType(ControlType.Window)); }
            catch { return Array.Empty<AutomationElement>(); }
        }

        /* หลังกด Finish HRMI ประมวลผลต่อ (มีหน้าต่างประมวลผล) — รอให้เสร็จก่อนเริ่มเคสถัดไป ห้ามไปกดปุ่มใดๆ ระหว่างนี้ */
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
            LogStep("เปิดรายการกำหนดตารางการทำงานของพนักงานแบบรวมแล้ว");

            FillDateField(_mainWindow, IdDateStart, start, useBuddhistYear: true);
            FillDateField(_mainWindow, IdDateEnd, end, useBuddhistYear: true);
            LogStep($"กำหนดช่วงวันที่ {start:dd/MM/yyyy} ถึง {end:dd/MM/yyyy} แล้ว");

            SelectRadioButton(_mainWindow, IdRdgSetWorkSchedule);
            SelectRadioGroupOption(_mainWindow, IdRdgAddWorkType, workType);

            SelectWorkSchedulePattern(patternCode);
            LogStep($"เลือกรูปแบบตารางการทำงาน {patternCode} แล้ว");

            ClickWizardNavButton(NextButtonName);
            LogStep("ไปยังหน้ากำหนดเงื่อนไขแล้ว");

            EnsureConditionsTabActive(); // TestStep: กดปุ่มแสดงข้อมูลพนักงานอย่างเดียว (ไม่ติ๊กเงื่อนไข)
            ClickWizardButton(IdBtnShowEmployeeData, fallbackName: "แสดงข้อมูลพนักงาน");
            LogStep("แสดงรายการพนักงานตามเงื่อนไขแล้ว");
            ClickRecheckEmployeeDataIfPresent();
            // ★ รอพิเศษ 1 นาที เฉพาะตอนคลิก Next ครั้งแรกหลังจากนี้ (ดูใน CompleteWizard)
            // เพราะเป็นจุดที่แอปต้องประมวลผลสร้าง dataset พนักงาน (GenIDGroupAndGenerateDatetime)
            CompleteWizard(firstNextClickExtraWaitMs: 120000);
            LogStep("บันทึกการกำหนดตารางการทำงานเสร็จแล้ว");
        }

        /// <summary>
        /// TestCase01: คัดลอกตารางการทำงานของพนักงาน (shortcut ผ่าน "กำหนดตารางการทำงานของพนักงานแบบรวม"
        /// แล้วเลือก Radio "คัดลอกตารางการทำงานของพนักงาน") ตาม TestStep แถว 11
        /// </summary>
        private void CopyScheduleShortcut(DateTime start, DateTime end)
        {
            ClickMoreAction();
            ClickMoreActionItem(ActionSetScheduleForGroup);
            LogStep("เปิดรายการคัดลอกตารางการทำงานแล้ว");
            FillDateField(_mainWindow, IdDateStart, start, useBuddhistYear: true);
            FillDateField(_mainWindow, IdDateEnd, end, useBuddhistYear: true);
            LogStep($"กำหนดช่วงวันที่ {start:dd/MM/yyyy} ถึง {end:dd/MM/yyyy} แล้ว");
            SelectRadioButton(_mainWindow, IdRdgCopyWorkSche);
            SelectEmployeeByCode(SampleEmployeeCodeForCopyShortcut);
            LogStep($"เลือกพนักงาน {SampleEmployeeCodeForCopyShortcut} แล้ว");
            ClickWizardNavButton(NextButtonName);

            EnsureConditionsTabActive();
            ClickWizardButton(IdBtnShowEmployeeData, fallbackName: "แสดงข้อมูลพนักงาน");
            LogStep("แสดงรายการพนักงานแล้ว");
            ClickRecheckEmployeeDataIfPresent();
            // ★ รอพิเศษ 1 นาที เฉพาะตอนคลิก Next ครั้งแรกหลังจากนี้ (ดูใน CompleteWizard)
            // เพราะเป็นจุดที่แอปต้องประมวลผลสร้าง dataset พนักงาน (GenIDGroupAndGenerateDatetime)
            CompleteWizard(firstNextClickExtraWaitMs: 120000);
            LogStep("บันทึกการคัดลอกตารางการทำงานเสร็จแล้ว");
        }

        /// <summary>
        /// TestCase01: กำหนดวันหยุด ตาม TestStep แถว 14
        /// </summary>
        private void SetHoliday(DateTime start, DateTime end)
        {
            ClickMoreAction();
            ClickMoreActionItem(ActionSetScheduleForGroup);
            LogStep("เปิดรายการกำหนดวันหยุดแล้ว");
            FillDateField(_mainWindow, IdDateStart, start, useBuddhistYear: true);
            FillDateField(_mainWindow, IdDateEnd, end, useBuddhistYear: true);
            LogStep($"กำหนดช่วงวันที่วันหยุด {start:dd/MM/yyyy} ถึง {end:dd/MM/yyyy} แล้ว");
            SelectRadioButton(_mainWindow, IdRdgHolidaySche);

            FillTextBox(_mainWindow, IdTxtHolidayTopic, "หยุดเพื่อไปหาสิ่งใหม่ๆ");
            ClickCheckbox(IdChkReplaceHoliday);

            SetHolidayDays("devChkSatHoliday", "devChkSunHoliday");
            LogStep("กำหนดหัวข้อวันหยุดและเลือกวันเสาร์-อาทิตย์แล้ว");

            ClickWizardNavButton(NextButtonName);
            EnsureConditionsTabActive();
            ClickWizardButton(IdBtnShowEmployeeData, fallbackName: "แสดงข้อมูลพนักงาน");
            LogStep("แสดงรายการพนักงานสำหรับวันหยุดแล้ว");
            ClickRecheckEmployeeDataIfPresent();
            // ★ รอพิเศษ 1 นาที เฉพาะตอนคลิก Next ครั้งแรกหลังจากนี้ (ดูใน CompleteWizard)
            // เพราะเป็นจุดที่แอปต้องประมวลผลสร้าง dataset พนักงาน (GenIDGroupAndGenerateDatetime)
            CompleteWizard(firstNextClickExtraWaitMs: 120000);
            LogStep("บันทึกวันหยุดเสร็จแล้ว");
        }

        /// <summary>
        /// TestCase02: Import ตารางการทำงานของพนักงาน — รับ start/end จากผู้เรียก
        /// ตาม TestStep แถว 21 (ชุดเดียว)
        /// </summary>
        private void ImportEmployeeWorkSchedule(DateTime start, DateTime end, string? filePath = null)
        {
            ClickMoreAction();
            ClickMoreActionItem(ActionImportSchedule);
            LogStep("เปิดรายการ Import ตารางการทำงานแล้ว");

            filePath ??= TestDataPaths.Find("Import Employee Work Schedule", "ImportEmployeeWorkSchedule")
                ?? throw new FileNotFoundException($"ไม่พบไฟล์ 'Import Employee Work Schedule' (.xlsx/.xlsm) ในโฟลเดอร์ {TestDataPaths.Folder}");
            BrowseAndSelectFile(filePath);
            LogStep($"เลือกไฟล์นำเข้า {Path.GetFileName(filePath)} แล้ว");
            FillDateField(_mainWindow, IdDateStart, start, useBuddhistYear: true);
            FillDateField(_mainWindow, IdDateEnd, end, useBuddhistYear: true);
            LogStep($"กำหนดช่วงวันที่ {start:dd/MM/yyyy} ถึง {end:dd/MM/yyyy} แล้ว");
            CompleteWizard();
            LogStep("Import ตารางการทำงานเสร็จแล้ว");
        }

        /// <summary>
        /// TestCase03: คัดลอกตารางการทำงานของพนักงาน ตาม TestStep แถว 32
        /// เลือกพนักงานในตารางหลักก่อน → More Action → กรอกวันที่ → Next → แสดงข้อมูลพนักงาน → Next → Next → Finish
        /// (TestStep ไม่มีขั้นเลือกประเภท/รูปแบบตารางงาน หรือเงื่อนไขกรองพนักงาน)
        /// </summary>
        private void CopyScheduleOfEmployee(DateTime start, DateTime end, string employeeCode)
        {
            SelectMainGridRow(employeeCode);
            LogStep($"เลือกพนักงาน {employeeCode} จากตารางแล้ว");
            ClickMoreAction();
            ClickMoreActionItem(ActionCopyScheduleOfEmployee);
            LogStep("เปิดรายการคัดลอกตารางของพนักงานแล้ว");
            FillDateField(_mainWindow, IdDateStart, start, useBuddhistYear: true);
            FillDateField(_mainWindow, IdDateEnd, end, useBuddhistYear: true);
            LogStep($"กำหนดช่วงวันที่ {start:dd/MM/yyyy} ถึง {end:dd/MM/yyyy} แล้ว");
            ClickWizardNavButton(NextButtonName);

            ClickWizardButton(IdBtnShowEmployeeData, fallbackName: "แสดงข้อมูลพนักงาน");
            LogStep("แสดงรายการพนักงานสำหรับคัดลอกตารางแล้ว");
            ClickRecheckEmployeeDataIfPresent();
            // ★ รอพิเศษ 1 นาที เฉพาะตอนคลิก Next ครั้งแรกหลังจากนี้ (ดูใน CompleteWizard)
            // เพราะเป็นจุดที่แอปต้องประมวลผลสร้าง dataset พนักงาน (GenIDGroupAndGenerateDatetime)
            CompleteWizard(firstNextClickExtraWaitMs: 120000);
            LogStep("คัดลอกตารางการทำงานเสร็จแล้ว");
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
        }

        /// <summary>
        /// TestCase04: ยกเลิกตารางการทำงานของพนักงานแบบรวม ตาม TestStep แถว 42-43
        /// *** เมธอดนี้ใช้ AutomationId ที่ "เดา" ไว้ในหมวด TODO ด้านบนของไฟล์ — ต้องตรวจสอบกับ
        /// Accessibility Insights / Inspect.exe ก่อนใช้งานจริง มิฉะนั้นจะหา element ไม่เจอ ***
        /// รองรับ: ช่วงรหัสกะงาน / ช่วงหน่วยงาน / ช่วงกลุ่มพนักงาน / ประเภทพนักงาน (เลือกบางตัวจาก dropdown)
        /// / ช่วงตำแหน่งงาน / ช่วงระดับพนักงาน / ช่วงรหัสพนักงาน / ผลที่คาดหวัง (ยกเลิกกะงาน และ/หรือ ยกเลิกวันหยุดกะ)
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
            ClickMoreAction();
            ClickMoreActionItem(ActionCancelScheduleForGroup);
            LogStep("เปิดรายการยกเลิกตารางการทำงานแบบรวมแล้ว");
            FillDateField(_mainWindow, IdDateStart, start, useBuddhistYear: true);
            FillDateField(_mainWindow, IdDateEnd, end, useBuddhistYear: true);
            LogStep($"กำหนดช่วงวันที่ {start:dd/MM/yyyy} ถึง {end:dd/MM/yyyy} แล้ว");

            // ช่องช่วงรหัสกะงานอาจยัง disabled จนกว่าจะระบุชนิดการยกเลิกก่อน
            // TestStep กำหนดให้เลือกผลที่คาดหวังก่อนกรอกช่วงข้อมูล จึงตั้งค่าตรงนี้ก่อนเปิด picker
            SetCancelOptions(cancelWorkSchedule, cancelWorkHoliday);
            LogStep($"กำหนดตัวเลือกยกเลิกกะงาน={cancelWorkSchedule}, ยกเลิกวันหยุดกะ={cancelWorkHoliday} แล้ว");

            // ช่วงรหัสกะงาน
            if (workGroupFrom != null && workGroupTo != null)
            {
                SelectRange(IdRangeShiftCode, workGroupFrom, workGroupTo);
                LogStep($"กำหนดช่วงรหัสกะงาน {workGroupFrom} ถึง {workGroupTo} แล้ว");
            }
            // ช่วงหน่วยงาน
            SelectRange(IdRangeOrgUnit, orgFrom, orgTo);
            LogStep($"กำหนดช่วงหน่วยงาน {orgFrom} ถึง {orgTo} แล้ว");
            // ช่วงกลุ่มพนักงาน
            SelectRange(IdRangeEmpGroup, empGroupFrom, empGroupTo);
            LogStep($"กำหนดช่วงกลุ่มพนักงาน {empGroupFrom} ถึง {empGroupTo} แล้ว");
            // ประเภทพนักงาน (คลิกลูกศร dropdown แล้วติ๊กเฉพาะรายการที่ระบุ)
            SelectEmployeeTypeDropdown(empTypeChecks);
            LogStep("เลือกประเภทพนักงานตามกรณีทดสอบแล้ว");
            // ช่วงตำแหน่งงาน
            SelectRange(IdRangePosition, positionFrom, positionTo);
            LogStep($"กำหนดช่วงตำแหน่ง {positionFrom} ถึง {positionTo} แล้ว");
            // ช่วงระดับพนักงาน
            SelectRange(IdRangeEmpLevel, levelFrom, levelTo);
            LogStep($"กำหนดช่วงระดับพนักงาน {levelFrom} ถึง {levelTo} แล้ว");
            // ช่วงรหัสพนักงาน
            SelectRange(IdRangeEmployee, empCodeFrom, empCodeTo);
            LogStep($"กำหนดช่วงรหัสพนักงาน {empCodeFrom} ถึง {empCodeTo} แล้ว");

            // ผลที่คาดหวัง — ตั้งค่าเป็นขั้นสุดท้ายก่อน Next (ขั้นอื่นมี popup/dropdown ที่อาจทำให้ติ๊กหลุด)
            // และตั้งทั้ง 2 ช่องให้ตรงชัดเจน (ช่องที่ไม่ต้องการ = เอาติ๊กออก) เผื่อวิซาร์ดจำค่าจากรอบก่อน
            SetCancelOptions(cancelWorkSchedule, cancelWorkHoliday);
            LogStep($"กำหนดตัวเลือกยกเลิกกะงาน={cancelWorkSchedule}, ยกเลิกวันหยุดกะ={cancelWorkHoliday} แล้ว");

            // ถ้า HRMI ยังเตือน "กรุณากำหนด การยกเลิก อย่างใดอย่างหนึ่ง" ตอนกด Next: กด OK แล้วติ๊กใหม่ ลองได้อีก 2 รอบ
            for (int attempt = 0; ; attempt++)
            {
                ClickWizardNavButton(NextButtonName); // (กล่องเตือนอาจถูกกด OK ไปแล้วใน ClickWizardNavButton)
                DismissCancelOptionWarning();
                if (!IsStillOnCancelOptionsPage()) break;
                if (attempt >= 2) throw new Exception("HRMI เตือน \"กรุณากำหนด การยกเลิก อย่างใดอย่างหนึ่ง\" ซ้ำ — ติ๊กช่องยกเลิกไม่ติด");
                SetCancelOptions(cancelWorkSchedule, cancelWorkHoliday);
            }

            CompleteWizard();
        }

        private void SetCancelOptions(bool cancelWorkSchedule, bool cancelWorkHoliday)
        {
            SetCheckbox(IdChkCancelSchedule, cancelWorkSchedule, fallbackName: "ยกเลิกกะงาน");
            SetCheckbox(IdChkCancelWorkHoliday, cancelWorkHoliday, fallbackName: "ยกเลิกวันหยุดกะ");
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

        /// <summary>
        /// เลือกค่า "จาก-ถึง" ใน RangeManager (ช่วงรหัสกะงาน/หน่วยงาน/กลุ่มพนักงาน/ตำแหน่งงาน/ระดับพนักงาน/รหัสพนักงาน)
        /// ตาม TestStep: กด "..." ของช่อง → เลือกแถวรหัสใน Pop-up → Select
        /// (เดิมพิมพ์รหัสแล้วกด Tab ถ้ารหัสไม่ตรง HRMI จะเปิด Pop-up ค้างไว้เอง แล้วเทสไปกดอย่างอื่นต่อ)
        /// </summary>
        private void SelectRange(string rangeAutomationId, string fromValue, string toValue)
        {
            PickRangeValue(rangeAutomationId, IdRangeBegin, fromValue, useLastRowIfMissing: false);
            PickRangeValue(rangeAutomationId, IdRangeEnd, toValue, useLastRowIfMissing: true);
        }

        private void PickRangeValue(string rangeAutomationId, string buttonEditId, string code, bool useLastRowIfMissing)
        {
            string label = $"{rangeAutomationId}.{buttonEditId}";
            TestLogger.LogInfo(_activeTestName, $"กำลังเปิดตัวเลือก {label} เพื่อเลือกรหัส '{code}'");
            var range = RetryFind(() => FindById(_mainWindow, rangeAutomationId), timeoutMs: 5000, pollMs: 250)
                ?? throw new Exception($"หาช่วงข้อมูล '{rangeAutomationId}' ไม่เจอ");
            var container = range.FindFirstDescendant(cf => cf.ByAutomationId(buttonEditId))
                ?? throw new Exception($"หาช่อง '{buttonEditId}' ใน '{rangeAutomationId}' ไม่เจอ");
            WaitUntilEnabled(container, timeoutMs: 3000);

            // จำหน้าต่าง HRMI ก่อนคลิก เพื่อระบุ popup ที่ถูกเปิดจากปุ่มนี้โดยตรง แม้ grid ยังไม่มี
            // DataItem หรือปุ่ม Select ยังไม่ถูก expose ผ่าน UIA
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
            LogStep($"เปิด popup เลือกข้อมูลของ {label} แล้ว");

            // บาง RangeManager เปิดหน้าต่างเลือกข้อมูลก่อน แล้วค่อย bind ตาราง (โดยเฉพาะช่วงรหัสกะงาน)
            // อย่าถือว่าพบ popup แล้วแปลว่าข้อมูลพร้อม: รอเซลล์ที่มีค่าจริงก่อนค้นหารหัส/เลือกแถว
            var loadedCell = RetryFind(() => dialog.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem))
                .FirstOrDefault(c => { try { return c.Patterns.Value.IsSupported && !string.IsNullOrWhiteSpace(c.Patterns.Value.Pattern.Value.Value); } catch { return false; } }),
                timeoutMs: 15000, pollMs: 250);
            if (loadedCell == null)
                throw new Exception($"Pop-up ของ {label} เปิดแล้ว แต่ตารางไม่มีข้อมูลหลังรอ 15 วินาที");

            var row = FindRowByScanningVisibleElements(dialog, code);
            if (row == null)
            {
                // รหัสตาม TestStep ไม่มีในข้อมูล (เช่น "001" ในหน่วยงาน) → ใช้แถวแรก (ช่อง "จาก") / แถวสุดท้าย (ช่อง "ถึง")
                // เพื่อให้ช่วงครอบคลุมข้อมูลทั้งหมด และบันทึกไว้ใน log ให้รู้ว่าแทนค่า
                row = FindEdgeRow(dialog, useLastRowIfMissing);
                if (row == null) throw new Exception($"ไม่พบรหัส '{code}' และ Pop-up ของ {label} ไม่มีข้อมูล");
                string used = ""; try { used = row.Patterns.Value.Pattern.Value.Value ?? ""; } catch { }
                TestLogger.LogInfo("EmpWorkScheListUC", $"ไม่พบรหัส '{code}' ใน {label} — ใช้ '{used}' ({(useLastRowIfMissing ? "แถวสุดท้าย" : "แถวแรก")}) แทน");
            }
            SelectRowAndConfirm(dialog, row, $"Pop-up {label}");
            LogStep($"เลือก '{code}' ใน {label} แล้ว");

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

        private void FillComboValue(string automationId, string value, string? parentAutomationId = null)
        {
            RetryAction<object?>(() =>
            {
                AutomationElement scope = _mainWindow;
                if (parentAutomationId != null)
                    scope = FindById(_mainWindow, parentAutomationId)
                        ?? throw new Exception($"หาช่วงข้อมูล '{parentAutomationId}' ไม่เจอ");

                var container = scope.FindFirstDescendant(cf => cf.ByAutomationId(automationId))
                    ?? throw new Exception($"หาช่อง '{automationId}'{(parentAutomationId != null ? $" ใน '{parentAutomationId}'" : "")} ไม่เจอ");

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
        /// เปิด dropdown "ประเภทพนักงาน" (CheckComboEdit) แล้วตั้งค่าให้ติ๊ก "เฉพาะ" รายการที่ระบุ
        /// (ตาม TestStep: เคส 1 ติ๊ก "รายชั่วโมง+รายเหมา", เคส 2 ติ๊ก "รายวัน+รายเดือน")
        /// ค่าเริ่มต้นของหน้าจอติ๊กไว้ครบทั้ง 4 ประเภท จึงต้องเอาติ๊กรายการอื่นออกด้วย
        /// </summary>
        private void SelectEmployeeTypeDropdown(string[] itemNames)
        {
            // ลำดับรายการใน dropdown ตามหน้าจอจริง (ใช้ตอนหารายการด้วย UIA ไม่เจอ แล้วต้องคลิกตามตำแหน่งแถว)
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
                // จากหน้าจอจริง: แถวแรกอยู่ต่ำกว่ากึ่งกลางช่อง 23px, สูงแถวละ 17px, ช่องติ๊กห่างขอบซ้ายช่อง 12px
                // ค่าเริ่มต้นติ๊กครบทุกรายการ → คลิกเฉพาะรายการที่ "ไม่" ต้องการ เพื่อเอาติ๊กออก
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

            // ตรวจผลจากข้อความในช่อง (แสดงรายการที่ติ๊กไว้คั่นด้วยจุลภาค) ถ้าอ่านค่าได้
            string shown = "";
            try
            {
                var inner = edit!.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)) ?? edit;
                shown = inner.Patterns.Value.IsSupported ? inner.Patterns.Value.Pattern.Value.Value ?? "" : "";
            }
            catch { }
            // ช่องนี้แสดงเฉพาะ "รายการแรกที่ติ๊ก" (ติ๊กครบ 4 ก็แสดงแค่ "พนักงานรายชั่วโมง") จึงตรวจได้แค่ว่า
            // รายการแรกที่ติ๊กตรงกับรายการแรกที่ต้องการ (= รายการก่อนหน้านั้นถูกเอาติ๊กออกแล้ว) และไม่มีรายการที่ไม่ต้องการโผล่
            if (shown.Length > 0)
            {
                string expectedFirst = allTypes.First(wanted.Contains);
                bool ok = shown.StartsWith(expectedFirst)
                          && !allTypes.Any(n => !wanted.Contains(n) && shown.Contains(n));
                if (!ok)
                    throw new Exception($"ตั้งค่าประเภทพนักงานไม่ตรง: ต้องการ [{string.Join(", ", itemNames)}] แต่ช่องแสดง '{shown}'");
            }
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
                    ?? FindFirstInHrmi(cf =>
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
                var container = FindById(context, automationId)
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

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        /* เรียก Invoke / DoDefaultAction แบบไม่รอผล: ถ้าปุ่มเปิดหน้าต่าง modal (วิซาร์ด, Pop-up "...", Open file)
           คำสั่งพวกนี้จะไม่ return จนกว่าหน้าต่างจะปิด → เทสค้างรอตลอด (อาการ "ค้างหน้าวิซาร์ดนานมาก")
           รอแค่ waitMs ถ้ายังไม่จบถือว่ากดติดแล้ว (เธรดเบื้องหลังจะจบเองตอนหน้าต่างปิด) ถ้า throw ทันที = กดไม่ติด */
        private static bool RunWithoutBlocking(Action action, int waitMs = 1500)
        {
            var task = Task.Run(action);
            try { task.Wait(waitMs); return true; }
            catch { return false; }
        }

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
                return RunWithoutBlocking(() => element.Patterns.LegacyIAccessible.Pattern.DoDefaultAction());
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

                PostMessage(handle, BM_CLICK, IntPtr.Zero, IntPtr.Zero); // Post ไม่รอ: ถ้าปุ่มเปิดหน้าต่าง modal, SendMessage จะค้างจนหน้าต่างปิด
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
        /* คลิกด้วยเมาส์จริงที่กึ่งกลาง element (ไม่ผ่าน UIA/COM จึงไม่ค้างแม้ปุ่มจะเปิดหน้าต่าง modal) */
        private static bool TryMouseClick(AutomationElement element)
        {
            try
            {
                // ไม่เช็ค IsOffscreen: เมนูใน More Action ของ DevExpress รายงาน IsOffscreen=True ทั้งที่มองเห็นอยู่
                // (ถ้าเช็ค จะข้ามไปใช้ Invoke ซึ่งเปิดวิซาร์ดแล้วค้าง HRMI ไว้ทั้งรอบ — ตรวจแล้วกับหน้าจอจริง)
                var rect = element.BoundingRectangle;
                if (rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0) return false;
                Mouse.MoveTo(rect.Center());
                Thread.Sleep(80);
                Mouse.Click(MouseButton.Left);
                return true;
            }
            catch { return false; }
        }

        /* ลำดับการคลิก: เมาส์จริง → PostMessage → (ท้ายสุด) pattern แบบไม่รอผล
           ห้ามเริ่มด้วย Invoke / DoDefaultAction: ถ้าปุ่มเปิดหน้าต่าง modal (วิซาร์ดจาก More Action, Pop-up "...",
           Open file, หน้าต่าง progress) คำสั่งจะค้างใน HRMI จนหน้าต่างปิด และทำให้คำสั่ง UIA ถัดไปทุกคำสั่ง timeout
           (อาการ "ค้างหน้าวิซาร์ด" + COMException 0x80131505) */
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
            // เมาส์จริงก่อนเสมอ (ดูเหตุผลที่ ClickElement) แล้วค่อย PostMessage / pattern แบบไม่รอผล
            if (TryMouseClick(element)) { Thread.Sleep(50); return; }
            if (TryInvokeViaWinApi(element)) { Thread.Sleep(50); return; }
            try { if (element.Patterns.Invoke.IsSupported && RunWithoutBlocking(() => element.Patterns.Invoke.Pattern.Invoke())) return; } catch { }
            if (TryInvokeViaLegacyPattern(element)) return;
            try { element.Click(); } catch { }
        }

        //----
        // * 5. Diagnostic Helpers *
        //----
        // เปิด/ปิด diagnostic logging ตรงนี้ — เปิดไว้ก่อนเพื่อ debug ปัญหา Next/แสดงข้อมูลพนักงาน กดไม่ติด
        private const bool DiagnosticLoggingEnabled = false; // ปิดไว้ให้ output เหมือนโมดูลอื่น (มีแค่ PASS/FAIL/INFO) — เปิดเฉพาะตอนไล่ปัญหา

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
                        cf.ByName(buttonName).And(cf.ByControlType(ControlType.Button))); // หาเฉพาะใน context ที่ระบุ (ไม่ถอยไปหาทั้ง desktop: เคยไปกดปุ่มของหน้าต่างอื่น)

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
                var element = FindById(_mainWindow, automationId);
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
        private void SelectEmployeeByCode(string employeeCode) =>
            SelectFromPicker(IdBtnEmployee, employeeCode, "Dialog เลือกพนักงาน");

        private void SelectHolidayCalendar(string calendarCode) =>
            SelectFromPicker(IdBtnHolidaySche, calendarCode, "Dialog ปฏิทินวันหยุด");

        private void SelectWorkSchedulePattern(string patternCode) =>
            SelectFromPicker(IdBtnWorkSchedulePattern, patternCode, "Dialog เลือกรูปแบบตารางการทำงาน");

        /* เลือกค่าจาก Pop-up ของช่อง ButtonEdit: กดปุ่ม "..." ของช่องนั้น → หาหน้าต่าง Pop-up ของ HRMI → เลือกแถวที่รหัสตรง
           → ตรวจว่าช่องมีค่าแล้ว (ถ้าช่องยังว่าง HRMI จะพังตอนกด Next ด้วย NullReferenceException ที่ GenIDGroupAndGenerateDatetime) */
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
        }

        /* หาหน้าต่าง Pop-up เลือกข้อมูลของ HRMI (เป็นของโปรเซส HRMI, ไม่ใช่หน้าหลัก และไม่ใช่วิซาร์ดที่มีปุ่ม Next)
           บาง lookup สร้างหน้าต่างและปุ่ม Select ก่อน bind grid; จึงอย่าบังคับว่าต้องมี DataItem ในจังหวะค้นหา */
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

            // ★ เพิ่มใหม่: รอเผื่อประมวลผลจริงๆ ก่อนไปเช็คอะไรต่อ (ดู WizardProcessingDelayMs ด้านบน)
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
                var nextInMain = FindFirstInHrmi(cf => cf.ByName(NextButtonName).Or(cf.ByName(FinishButtonName)).And(cf.ByControlType(ControlType.Button)));
                AutomationElement? finishInMain = null, nextInDesktop = null, finishInDesktop = null;

                bool hasNav = nextInMain != null;

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

                var finishBtn = FindNavButton(FinishButtonName);

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
                    WaitForWizardResponse(WizardProcessingDelayMs);

                    DiagLog($"  หลังคลิก Finish -> {DescribeElement(finishBtn, "finishBtn-after")}");
                    DismissErrorDialogIfPresent();
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

                // ★ เพิ่มใหม่: ถ้าเป็นการคลิก Next ครั้งแรก (i==0) และมีการระบุ firstNextClickExtraWaitMs
                // ไว้ ให้รอพิเศษยาวกว่าปกติ (เช่น 60 วินาที) เพราะจุดนี้คือจุดที่แอปต้องประมวลผล
                // สร้าง dataset พนักงานซึ่งใช้เวลานาน — ก่อนจะไปเช็ค error dialog ใดๆ เลย
                int waitMs = (i == 0 && firstNextClickExtraWaitMs > 0) ? firstNextClickExtraWaitMs : WizardProcessingDelayMs;
                DiagLog($"  รอ {waitMs}ms ให้แอปประมวลผลหลังคลิก Next (ครั้งที่ #{i}{(i == 0 && firstNextClickExtraWaitMs > 0 ? " - ใช้ extra wait" : "")})");
                WaitForWizardResponse(waitMs);

                DiagLog($"  หลังคลิก Next -> {DescribeElement(nextBtn, "nextBtn-after-click")}");
                WaitForWizardStepSettled();
                DismissErrorDialogIfPresent();
            }
        }

        /* รอหลังกด Next/Finish แบบไม่เสียเวลาฟรี: จบทันทีที่หน้าวิซาร์ดพร้อม (ปุ่ม Next/Finish กดได้และมองเห็น),
           วิซาร์ดปิดไปแล้ว หรือมีกล่องข้อความของ HRMI เด้งขึ้นมา — maxMs คือเพดาน (เดิมรอเต็มเวลาทุกครั้ง 2 วินาที / 60 วินาที) */
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

        /* ติ๊ก checkbox ให้ "ติ๊ก" จริง (CheckEdit ของ DevExpress)
           เดิมใช้ IsChecked = true (Toggle ผ่าน UIA) ซึ่งไม่ commit ค่า → HRMI เตือน "กรุณากำหนด การยกเลิก อย่างใดอย่างหนึ่ง"
           ลองทีละวิธีจนสถานะเป็นติ๊ก: คลิกเมาส์ที่ช่องติ๊ก → โฟกัสแล้วกด Space → DoDefaultAction
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

        /// <summary>
        /// ติ๊กเงื่อนไขการเลือกช่วงข้อมูลพนักงาน ตาม Test Step: หน่วยงาน / ตำแหน่งงาน / กลุ่มพนักงาน / ระดับพนักงาน
        /// (แก้จากเดิมที่ติ๊ก org/position/employeeType ให้ตรงกับ 4 รายการที่ระบุใน Test Step แทน)
        /// *** IdChkEmpGroupAll และ IdChkEmployeeLevelAll เป็น AutomationId ที่เดาไว้ ต้องตรวจสอบก่อนใช้งานจริง ***
        /// </summary>
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
                    ?? FindFirstInHrmi(cf => cf.ByName(BtnRecheckEmployeeDataName));

                if (btn == null)
                {
                    DiagLog("ClickRecheckEmployeeDataIfPresent: ไม่เจอปุ่ม 'รีเช็คข้อมูลพนักงาน' -> ข้าม (รอ buffer เฉยๆ)");
                    Thread.Sleep(300);
                    return;
                }

                DiagLog("ClickRecheckEmployeeDataIfPresent: เจอปุ่ม 'รีเช็คข้อมูลพนักงาน' -> คลิกเพื่อบังคับ sync ข้อมูลก่อนกด Next");
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

        /* หาแถวที่รหัสตรงเท่านั้น (เดิมถ้าหาไม่เจอจะเลือก "row 0" ซึ่งได้แถวแรกที่อาจไม่ใช่รหัสที่ต้องการ) */
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
                var txt = FindById(context, automationId)?.AsTextBox()
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
                var container = FindById(context, automationId);
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
                var container = FindById(context, automationId)
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
