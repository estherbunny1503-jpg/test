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

namespace HRMITestFlaUI.Pages.Personnel
{
    public class ResignationInfoUC
    {
        // =========================================================================================
        // หมวดที่ 1: ข้อมูลการทดสอบทั้ง 5 ขั้น (Test Data)
        // =========================================================================================

        // --- Data Records สำหรับใช้งานในแต่ละเฟส ---
        // ค่า Checkbox: null = ไม่แตะต้อง (ปล่อยตามค่า default ของฟอร์ม), true = ติ๊ก, false = ไม่ติ๊ก
        private sealed record AddItem(
            string DocumentDate,
            string EffectDate,
            string LastWorkDate,
            string ResignationType,
            string Reason,
            string EmployeeCode,
            bool? CalcLastPayment,
            bool? CalcMonthSalary,
            bool? CalcByWorkedDays,
            bool? CalcIncomeNotLastPeriod,
            bool? CalcIncomeLastPeriod,
            bool? CalcIncomeOccasional,
            string? IncomeOccasionalAtDate,
            bool? SocialInsuranceNextPeriod,
            string SaveBtn,
            bool NeedsManualClose);
        private sealed record ValidationItem(
            string TestCaseId,
            bool ClearDocNo,         // true = เคลียร์ช่องเลขที่เอกสารก่อน
            string? Reason,          // null = ไม่แตะช่องนั้น
            string? EmpCode,         // null = ไม่แตะ, "" = เคลียร์ช่องพนักงาน
            string SaveBtn,
            int Progress,
            string ProgressMsg,
            string[] ExpectedErrors);
        private sealed record EditItem(
            string Name,
            string Code,
            string CheckBoxId,
            string SaveBtn,
            bool NeedsManualClose);
        private sealed record DeleteItem(
            string TargetCode,
            string DialogBtn,
            bool ExpectDeleted,
            int Progress,
            string ProgressMsg);

        // ข้อความแจ้งเตือนที่คาดหวัง
        private const string WarnMissingReason = "เหตุผล ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล";
        private const string WarnMissingEmployee = "พนักงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล";
        private const string WarnMissingDocNo = "เลขที่เอกสาร ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล";

        // [ขั้นที่ 1] ข้อมูลทดสอบการสร้างใหม่ (Add / Navigate)
        private static readonly AddItem[] Step1_AddCases =
        {
            new("01/08/2570", "01/09/2570", "31/08/2570", "ลาออก", "ไม่ผ่านการทดลองงาน", "EMP00001",
                CalcLastPayment: null, CalcMonthSalary: false, CalcByWorkedDays: null, CalcIncomeNotLastPeriod: null,
                CalcIncomeLastPeriod: null, CalcIncomeOccasional: false, IncomeOccasionalAtDate: "31/08/2570", SocialInsuranceNextPeriod: false,
                SaveBtn: "Save", NeedsManualClose: true),

            new("02/06/2570", "01/07/2570", "30/06/2570", "เลิกจ้าง", "เนื่องจากต้องโทษทางวินัย", "EMP00002",
                CalcLastPayment: false, CalcMonthSalary: null, CalcByWorkedDays: null, CalcIncomeNotLastPeriod: null,
                CalcIncomeLastPeriod: null, CalcIncomeOccasional: null, IncomeOccasionalAtDate: "30/06/2570", SocialInsuranceNextPeriod: null,
                SaveBtn: "Save and New", NeedsManualClose: false),

            new("05/05/2570", "01/06/2570", "31/05/2570", "ถูกให้ออกหรือไล่ออก", "กระทำความผิดทางอาญาโดยเจตนาแก่นายจ้าง", "EMP00003",
                CalcLastPayment: null, CalcMonthSalary: null, CalcByWorkedDays: null, CalcIncomeNotLastPeriod: null,
                CalcIncomeLastPeriod: null, CalcIncomeOccasional: false, IncomeOccasionalAtDate: "31/05/2570", SocialInsuranceNextPeriod: false,
                SaveBtn: "Save and New", NeedsManualClose: false),

            new("31/05/2570", "01/06/2570", "31/05/2570", "ถึงแก่กรรม", "สิ้นสุดระยะเวลาการจ้างงาน", "EMP00004",
                CalcLastPayment: false, CalcMonthSalary: false, CalcByWorkedDays: false, CalcIncomeNotLastPeriod: false,
                CalcIncomeLastPeriod: false, CalcIncomeOccasional: true, IncomeOccasionalAtDate: "01/06/2570", SocialInsuranceNextPeriod: false,
                SaveBtn: "Save and Close", NeedsManualClose: false)
        };

        // [ขั้นที่ 2] ข้อมูลทดสอบ Validation ตอนเพิ่ม (Add Validation)
        private static readonly ValidationItem[] Step2_AddValidationCases =
        {
            new("TestCase01_MissingReason", false, null, "EP0005", "Save", 27,
                "Validating: Missing Reason...", new[] { WarnMissingReason }),

            new("TestCase02_MissingEmployee", false, "ไม่ผ่านการทดลองงาน", "", "Save and New", 33,
                "Validating: Missing Employee...", new[] { WarnMissingEmployee })
        };

        // [ขั้นที่ 3] ข้อมูลทดสอบการแก้ไขข้อมูล (Edit Records)
        private static readonly EditItem[] Step3_EditCases =
        {
            // EMP00001 -> ติ๊ก "นำส่งเงินสมทบประกันสังคมในงวดถัดไป" -> Save -> Close
            new(
                "TestCase08_EditEmp001",
                "EMP00001",
                ChkSocialInsuranceFieldId,
                "Save",
                true),
            // EMP00002 -> ติ๊ก "คำนวณค่าจ้างงวดสุดท้าย" -> Save and Close
            new(
                "TestCase09_EditEmp002",
                "EMP00002",
                ChkCalcLastPaymentFieldId,
                "Save and Close",
                false)
        };

        // [ขั้นที่ 4] ข้อมูลทดสอบ Validation ตอนแก้ไข (Edit Validation) — TestStep ให้เปิดแถวแรกของตาราง
        private static readonly ValidationItem[] Step4_EditValidationCases =
        {
            new("TestCase10_EditValidation_ClearDocNoAndEmployee", true, null, "", "Save", 68,
                "Validating Edit: Clear Doc No and Employee...", new[] { WarnMissingDocNo, WarnMissingEmployee })
        };

        // [ขั้นที่ 5] ข้อมูลทดสอบการลบข้อมูล (Delete Records)
        private static readonly DeleteItem[] Step5_DeleteCases =
        {
            new("EMP00001", "Yes", true, 92, "Delete Test: EMP00001 (confirmed delete)..."),
            new("EMP00004", "No", false, 100, "Delete Test: EMP00004 (cancelled delete)...")
        };


        // =========================================================================================
        // หมวดที่ 2: ส่วนที่ขึ้นกับหน้าจอนั้นๆ (Screen-Specific Implementation)
        // **เมื่อเปลี่ยนไปทำหน้าจออื่น ต้องมาแก้ไขค่าและเมทอดในส่วนนี้ทั้งหมด**
        // =========================================================================================

        // 2.1 Locator & Mappings ประจำหน้าจอ
        private const string MenuName = "บันทึกพ้นสภาพความเป็นพนักงาน";
        private const string ScreenLogName = "ResignationInfoUC";
        private const string FormId = "ResignationInfoUC";
        private const string GeneralTabId = "devTabPgGeneral";
        private const string FirstObjectID = "devBtnEmp"; // ช่องพนักงาน (ถูกเคลียร์หลัง Save and New)
        private const string CalcTabName = "การคำนวณค่าจ้าง";
        private const int SaveTimeoutMs = 80000; // หน้านี้คำนวณค่าจ้างเบื้องหลังหลังบันทึก ใช้เวลานาน

        // 2.2 ฟิลด์ Input ประจำหน้าจอ
        private const string DocNoFieldId = "devBtnResignationNo";                         // เลขที่เอกสาร
        private const string DocDateFieldId = "devDateResignationDate";                    // วันที่เอกสาร
        private const string EffectDateFieldId = "devDateEffectDate";                      // วันที่มีผลบังคับใช้
        private const string LastWorkDateFieldId = "devDateResignEffectDate";              // วันที่ทำงานวันสุดท้าย
        private const string ResignationTypeFieldId = "devLookUpResignationType";          // ประเภทการพ้นสภาพ
        private const string ReasonFieldId = "devLookUpReason";                            // เหตุผล
        private const string EmployeeFieldId = "devBtnEmp";                                // พนักงาน (ปุ่ม ...)

        private const string ChkCalcLastPaymentFieldId = "devChkIsCalcLastPayment";                // คำนวณค่าจ้างงวดสุดท้าย
        private const string ChkCalMoneyMonthFieldId = "devChkCalMoneyMonth";                      // คำนวณเงินเดือน
        private const string RdgCalcLastPaymentTypeFieldId = "devRdgCalcLastPaymentType";          // ตามจำนวนวันที่ทำงาน (radio group)
        private const string RdgOptionByWorkedDays = "ตามจำนวนวันที่ทำงาน";
        private const string ChkIncomeNotLastPeriodFieldId = "devChkIsCalcIncomeNotLastPeriod";    // คำนวณรายได้-รายหักไม่ประจำงวดสุดท้าย
        private const string ChkIncomeLastPeriodFieldId = "devChkIsCalcIncomeLastPeriod";          // คำนวณรายได้-รายหักประจำงวดสุดท้าย
        private const string ChkIncomeOccasionalFieldId = "devChkIsCalcIncomeOccasional";          // คำนวณรายได้ไม่ประจำคงเหลือ สิ้นสุด ณ วันที่
        private const string IncomeOccasionalAtFieldId = "devDateCalcIncomeOccasionalAt";          // วันที่ของช่องด้านบน
        private const string ChkSocialInsuranceFieldId = "devChkSocialInsurance";                  // นำส่งเงินสมทบประกันสังคมในงวดถัดไป

        /// <summary>
        /// ขั้นที่ 1: เปิดหน้าจอและกรอกข้อมูลสร้างใหม่
        /// </summary>
        public void NavigateToResignationInfoUC()
        {
            const string stageName = "TestCase00_AddResignationInfo";
            TestLogger.LogInfo(ScreenLogName, $"เริ่มทดสอบหน้าจอ {MenuName}");
            TestProgressHelper.Report(2, $"Opening Personnel -> {MenuName} Menu...");

            // เฉพาะหน้าจอนี้: TestStep ให้เปิดผ่านเมนูหลัก Personnel (ไม่ใช่ช่องค้นหา)
            if (!TryOpenScreenMenu(stageName)) return;

            Window? form = null;
            for (int i = 0; i < Step1_AddCases.Length; i++)
            {
                var item = Step1_AddCases[i];
                string testName = $"{stageName}_{item.EmployeeCode}";
                // หน้านี้มี 4 รายการ จึงเฉลี่ย Progress ให้อยู่ในช่วง 4 -> 20
                TestProgressHelper.Report(4 + (i * 16 / Step1_AddCases.Length), $"Adding {MenuName} ({i + 1}/{Step1_AddCases.Length}): {item.EmployeeCode} - {item.ResignationType}...");

                try
                {
                    form ??= GetOrOpenForm();
                    var tab = GetGeneralTab(form);
                    FillFormData(form, item);

                    if (!TrySaveAndHandleDuplicate(form, testName, item.EmployeeCode, item.SaveBtn))
                    {
                        form = null;
                        continue;
                    }
                    WaitForSaveDone(form, item.SaveBtn); // เฉพาะหน้าจอนี้: รอการคำนวณเบื้องหลังหลังบันทึก (สูงสุด 80 วินาที)

                    if (item.SaveBtn == "Save and New")
                    {
                        WaitUntil(() => string.IsNullOrEmpty(tab.FindFirstDescendant(cf => cf.ByAutomationId(FirstObjectID))?.AsTextBox().Text), 3000);
                        LogSuccess(testName, $"'{item.EmployeeCode}' บันทึกสำเร็จ (Save and New)");
                    }
                    else
                    {
                        if (item.NeedsManualClose) form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();
                        WaitForClosed(() => FindOpenForm());
                        form = null;
                        LogGridResult(testName, item.EmployeeCode);
                    }
                }
                catch (Exception ex)
                {
                    LogFailure(testName, $"'{item.EmployeeCode}' เพิ่มไม่สำเร็จ: {ex.Message}");
                    form = null;
                }
            }
        }

        /// <summary>
        /// ขั้นที่ 2: รัน Validation ตอนสร้างข้อมูลใหม่
        /// </summary>
        public void RunAllValidationTests()
        {
            const string stage = "TestCase01-05_Validation";
            TestProgressHelper.Report(22, $"Opening {MenuName} Form for Validation Tests...");

            Window form;
            try { form = OpenForm(); }
            catch (Exception ex) { LogFailure(stage, $"เปิดฟอร์มไม่สำเร็จ: {ex.Message}"); return; }

            var tab = GetGeneralTab(form);

            foreach (var testCase in Step2_AddValidationCases)
            {
                RunStep(testCase.TestCaseId,
                    () => FillValidationAndAssert(tab, form, testCase, testCase.TestCaseId),
                    testCase.Progress, testCase.ProgressMsg);
            }

            // เคสเสริมเฉพาะหน้าจอ: พฤติกรรมเมื่อปิด Dialog ข้อความเตือน
            const string dupName = "TestCase03-05_CloseSequence";
            RunStep(dupName,
                () => RunCloseDialogSequence(form, dupName, WarnMissingEmployee),
                40, "Validating: Close Sequence...");
        }

        /// <summary>
        /// ขั้นที่ 3: เลือกแก้ไขรายการในตาราง
        /// </summary>
        public void RunEditTests()
        {
            TestProgressHelper.Report(42, "Starting Edit Operations...");

            for (int i = 0; i < Step3_EditCases.Length; i++)
            {
                var item = Step3_EditCases[i];
                try
                {
                    var form = ClickRowAndOpenEdit(item.Code);
                    var tab = GetGeneralTab(form);
                    SwitchToCalcTab(form);
                    ToggleCheckBox(form, item.CheckBoxId, true);

                    if (!TrySaveAndHandleDuplicate(form, item.Name, item.Code, item.SaveBtn)) continue;
                    WaitForSaveDone(form, item.SaveBtn); // เฉพาะหน้าจอนี้
                    if (item.NeedsManualClose) form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();

                    WaitForClosed(() => FindOpenForm());
                    RefreshGrid();

                    bool recordFound = VerifyRecordInGrid(item.Code, refresh: false);
                    if (recordFound)
                        LogSuccess(item.Name, $"แก้ไข '{item.Code}' สำเร็จ (พบข้อมูลที่แก้ไขในตารางหลัก)");
                    else
                        LogFailure(item.Name, $"แก้ไข '{item.Code}' แล้ว แต่ไม่พบข้อมูลที่อัปเดตในตารางหลัก");
                }
                catch (Exception ex)
                {
                    LogFailure(item.Name, $"แก้ไข '{item.Code}' ไม่สำเร็จ: {ex.Message}");
                }
                finally
                {
                    TestProgressHelper.Report(42 + ((i + 1) * 13 / Step3_EditCases.Length), $"Editing Record ({i + 1}/{Step3_EditCases.Length})...");
                }
            }
        }

        /// <summary>
        /// ขั้นที่ 4: รัน Validation ตอนแก้ไขข้อมูล
        /// </summary>
        public void RunEditValidationTests()
        {
            const string baseName = "TestCase10_EditValidation";
            TestProgressHelper.Report(58, "Opening Edit Form for Validation Tests...");

            Window form;
            try { form = ClickFirstRowAndOpenEdit(); } // เฉพาะหน้าจอนี้: TestStep ให้เปิดแถวแรกของตาราง
            catch (Exception ex) { LogFailure(baseName, $"เปิดฟอร์ม Edit ไม่สำเร็จ: {ex.Message}"); return; }

            var tab = GetGeneralTab(form);

            foreach (var testCase in Step4_EditValidationCases)
            {
                RunStep(testCase.TestCaseId,
                    () => FillValidationAndAssert(tab, form, testCase, testCase.TestCaseId),
                    testCase.Progress, testCase.ProgressMsg);
            }

            RunStep($"{baseName}_CloseSequence",
                () => RunCloseDialogSequence(form, baseName, WarnMissingEmployee),
                80, "Validating Edit: Close Sequence...");

            LogSuccess(baseName, "รัน Validation Edit ครบทุกเคสแล้ว");
        }

        /// <summary>
        /// ขั้นที่ 5: ทดสอบการลบรายการในตาราง
        /// </summary>
        public void RunDeleteTests()
        {
            const string baseName = "TestCase11_Delete";
            TestProgressHelper.Report(84, "Starting Delete Tests...");

            foreach (var testCase in Step5_DeleteCases)
            {
                RunDeleteCase(baseName, testCase.TargetCode, testCase.DialogBtn, testCase.ExpectDeleted);
                TestProgressHelper.Report(testCase.Progress, testCase.ProgressMsg);
            }

            LogSuccess(baseName, "ทดสอบ Delete ครบทุกเคส");
            TestLogger.LogInfo(ScreenLogName, $"สรุปผลรวมทั้งหมด: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");
        }

        // --- Custom Mapping Logic ประจำหน้าจอนี้ ---

        /* กรอกข้อมูลพ้นสภาพ (แท็บหลัก → แท็บการคำนวณค่าจ้าง) */
        private void FillFormData(Window form, AddItem item)
        {
            SetDate(form, DocDateFieldId, item.DocumentDate);
            SetDate(form, EffectDateFieldId, item.EffectDate);
            SetDate(form, LastWorkDateFieldId, item.LastWorkDate);

            SelectComboBoxItem(form, ResignationTypeFieldId, item.ResignationType);
            SelectComboBoxItem(form, ReasonFieldId, item.Reason);
            SelectEmployee(form, item.EmployeeCode);

            SwitchToCalcTab(form);

            if (item.CalcLastPayment.HasValue) ToggleCheckBox(form, ChkCalcLastPaymentFieldId, item.CalcLastPayment.Value);
            if (item.CalcMonthSalary.HasValue) ToggleCheckBox(form, ChkCalMoneyMonthFieldId, item.CalcMonthSalary.Value);
            if (item.CalcByWorkedDays == true) SelectRadio(form, RdgCalcLastPaymentTypeFieldId, RdgOptionByWorkedDays);
            if (item.CalcIncomeNotLastPeriod.HasValue) ToggleCheckBox(form, ChkIncomeNotLastPeriodFieldId, item.CalcIncomeNotLastPeriod.Value);
            if (item.CalcIncomeLastPeriod.HasValue) ToggleCheckBox(form, ChkIncomeLastPeriodFieldId, item.CalcIncomeLastPeriod.Value);

            bool wantsOccasional = item.CalcIncomeOccasional ?? false;
            if (item.CalcIncomeOccasional.HasValue) ToggleCheckBox(form, ChkIncomeOccasionalFieldId, wantsOccasional);

            // ช่องวันที่จะ enable เฉพาะตอนติ๊ก → ติ๊กชั่วคราวเพื่อกรอก แล้วคืนสถานะตาม Test Step
            if (!string.IsNullOrEmpty(item.IncomeOccasionalAtDate))
            {
                if (!wantsOccasional) ToggleCheckBox(form, ChkIncomeOccasionalFieldId, true);
                SetDate(form, IncomeOccasionalAtFieldId, item.IncomeOccasionalAtDate);
                if (!wantsOccasional) ToggleCheckBox(form, ChkIncomeOccasionalFieldId, false);
            }

            if (item.SocialInsuranceNextPeriod.HasValue) ToggleCheckBox(form, ChkSocialInsuranceFieldId, item.SocialInsuranceNextPeriod.Value);
        }

        /* กรอกข้อมูลตามเคส แล้วกดบันทึกและตรวจข้อความ (Reason/EmpCode = null คือไม่แตะ, EmpCode "" คือเคลียร์ช่องพนักงาน) */
        private void FillValidationAndAssert(AutomationElement tab, Window form, ValidationItem tc, string testName)
        {
            if (tc.ClearDocNo) ClearField(form, DocNoFieldId);
            if (tc.Reason != null) SelectComboBoxItem(form, ReasonFieldId, tc.Reason);
            if (tc.EmpCode == "") ClearField(form, EmployeeFieldId);
            else if (tc.EmpCode != null) SelectEmployee(form, tc.EmpCode);
            form.FindFirstDescendant(cf => cf.ByName(tc.SaveBtn))?.Click();
            AssertScreenTaskResult(form, tc.ExpectedErrors, testName);
        }

        private static string? DetectDuplicateMessage(IEnumerable<string> texts)
        {
            if (texts.Any(t => t.Contains("พนักงาน") && t.Contains("ซ้ำ"))) return "รหัสพนักงานซ้ำ";
            return null;
        }

        /* หน้านี้ฟอร์มโหลดช้า: รอจนช่องเลขที่เอกสาร enable (ฟอร์ม bind ข้อมูลเสร็จ) แล้วคืนแท็บ General */
        private AutomationElement GetGeneralTab(Window form)
        {
            var docNo = Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(DocNoFieldId)), 5000);
            if (docNo != null) WaitUntil(() => docNo.Properties.IsEnabled.ValueOrDefault, 15000);
            return Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(GeneralTabId)), 3000) ?? form;
        }

        private Window? FindOpenForm() =>
            _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault() ??
            _automation.GetDesktop().FindFirstChild(cf => cf.ByAutomationId(FormId))?.AsWindow();

        // --- การเปิดหน้าจอ / ฟอร์ม เฉพาะหน้าจอนี้ ---

        /* เปิดหน้าจอผ่านเมนูหลัก Personnel แล้วคลิกแถวเมนูของหน้าจอนี้ (ถ้าแถวยังไม่แสดงจะเลื่อน Line Down ทีละขั้นจนเจอ) */
        private bool TryOpenScreenMenu(string stage)
        {
            try
            {
                var personnelTab = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("Personnel")), 3000)
                                   ?? throw new Exception("หาเมนูหลัก Personnel ไม่เจอ");
                ClickByMouse(personnelTab);

                var menuRow = Retry(FindMenuRow, 2000);
                var lineDown = _mainWindow.FindFirstDescendant(cf => cf.ByName("Line Down"));
                for (int step = 0; menuRow == null && lineDown != null && step < 10; step++)
                {
                    ClickByMouse(lineDown);
                    menuRow = Retry(FindMenuRow, 500);
                }
                menuRow ??= Retry(FindMenuRow, 3000) ?? throw new Exception($"หาเมนู '{MenuName}' ไม่เจอ");
                ClickByMouse(menuRow);

                var btnNew = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("New")), 6000)
                             ?? throw new Exception("เปิดหน้าจอแล้วไม่พบปุ่ม New");
                WaitUntil(() => btnNew.Properties.IsEnabled.ValueOrDefault, 3000);
                return true;
            }
            catch (Exception ex)
            {
                LogFailure(stage, $"เปิดเมนูไม่สำเร็จ: {ex.Message}");
                return false;
            }
        }

        /* หาแถวเมนูของหน้าจอนี้ในแผงเมนู Personnel */
        private AutomationElement? FindMenuRow() =>
            _mainWindow.FindFirstDescendant(cf => cf.ByName("treeListColumn row 10"))
            ?? _mainWindow.FindFirstDescendant(cf => cf.ByName(MenuName));

        /* เปิดฟอร์ม Edit จากแถวแรกของตารางหลัก */
        private Window ClickFirstRowAndOpenEdit()
        {
            RefreshGrid();
            var row = Retry(() => GetGridRows(_mainWindow).FirstOrDefault(), 8000) ?? throw new Exception("หาแถวแรกของตารางไม่เจอ");
            row.Click();

            var btn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("Edit")), 3000)
                      ?? throw new Exception("ไม่พบปุ่ม Edit");
            btn.Click();

            var form = Retry(FindOpenForm, 5000) ?? throw new Exception("หาฟอร์มไม่เจอ");
            WaitUntil(() => form.FindFirstDescendant(cf => cf.ByAutomationId(FirstObjectID)) != null, 3000);
            return form;
        }

        /* หลังกดบันทึก รองานคำนวณเบื้องหลังให้เสร็จ (สูงสุด 80 วินาที)
           - Save and Close: รอฟอร์มปิด / Save and New: รอช่องพนักงานถูกเคลียร์
           - Save: รอปุ่ม Close กดได้ แล้วปิด Pop-up "บันทึกสำเร็จ" (ปุ่ม OK) ถ้ามี */
        private void WaitForSaveDone(Window form, string saveBtn)
        {
            if (saveBtn == "Save and Close")
            {
                WaitForClosed(() => FindOpenForm(), SaveTimeoutMs);
                return;
            }
            if (saveBtn == "Save and New")
            {
                WaitUntil(() => string.IsNullOrEmpty(ReadText(form.FindFirstDescendant(cf => cf.ByAutomationId(EmployeeFieldId)))), SaveTimeoutMs);
                return;
            }

            var btnClose = Retry(() => form.FindFirstDescendant(cf => cf.ByName("Close")), 3000);
            WaitUntil(() => btnClose?.Properties.IsEnabled.ValueOrDefault == true, SaveTimeoutMs);

            var info = Retry(() => form.ModalWindows.FirstOrDefault(), 1500);
            var ok = info?.FindFirstDescendant(cf => cf.ByName("OK").Or(cf.ByName("ตกลง")));
            if (info != null && ok != null) { ok.Click(); WaitForClosed(() => info); }
        }

        // --- การกรอกข้อมูลเฉพาะหน้าจอนี้ ---

        /* กรอกช่องวันที่ (คลิก → ลบค่าเดิม → พิมพ์ → Tab) ค่าว่าง = เคลียร์ช่อง */
        private void SetDate(AutomationElement scope, string id, string dateText)
        {
            var el = Retry(() => scope.FindFirstDescendant(cf => cf.ByAutomationId(id)), 3000) ?? throw new Exception($"ไม่พบช่องวันที่ {id}");
            if (!WaitUntil(() => el.Properties.IsEnabled.ValueOrDefault, 3000)) throw new Exception($"ช่องวันที่ '{id}' ยัง disabled อยู่");
            TypeByKeyboard(el, dateText, extraBackspace: true);
        }

        /* เคลียร์ค่าในช่อง (เลขที่เอกสาร / พนักงาน) ด้วยคีย์บอร์ด */
        private void ClearField(Window form, string id)
        {
            var el = Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(id)), 5000) ?? throw new Exception($"หาช่อง '{id}' ไม่เจอ");
            if (!WaitUntil(() => el.Properties.IsEnabled.ValueOrDefault, 3000)) return;
            TypeByKeyboard(el, "", extraBackspace: true);
            WaitUntil(() => ReadText(el) == "", 2000);
        }

        /* พิมพ์ลงช่องด้วยคีย์บอร์ดจริง (คลิกฝั่งซ้ายของช่อง กันโดนปุ่มที่ขอบขวา) */
        private static void TypeByKeyboard(AutomationElement field, string text, bool extraBackspace = false)
        {
            var r = field.BoundingRectangle;
            Mouse.Click(new Point(r.Left + Math.Min(20, r.Width / 4), r.Top + r.Height / 2));

            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            Keyboard.Type(VirtualKeyShort.DELETE);
            if (extraBackspace)   // กัน mask ของช่องวันที่เหลือตัวอักษรค้าง
                for (int i = 0; i < 10; i++) Keyboard.Type(VirtualKeyShort.BACK);

            if (!string.IsNullOrEmpty(text)) Keyboard.Type(text);
            Keyboard.Type(VirtualKeyShort.TAB); // Tab ออกเพื่อ commit ค่า
        }

        /* เลือกรายการใน ComboBox (ประเภทการพ้นสภาพ / เหตุผล) แล้วรอจนค่าในช่องเปลี่ยนจริง */
        private void SelectComboBoxItem(Window form, string comboId, string itemText)
        {
            var combo = Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(comboId)), 10000)
                        ?? throw new Exception($"หา combobox '{comboId}' ไม่เจอ");

            ClickByMouse(combo);
            var item = Retry(() => _automation.GetDesktop().FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem))
                                       .FirstOrDefault(el => ReadText(el) == itemText), 3000);

            if (item != null) ClickByMouse(item);
            else { Keyboard.Type(itemText); Keyboard.Type(VirtualKeyShort.RETURN); }

            WaitUntil(() => ReadText(combo).Contains(itemText), 3000);
        }

        /* สลับไปแท็บการคำนวณค่าจ้าง แล้วรอจนแท็บถูกเลือกจริง (ถ้าหาแท็บไม่เจอถือว่าฟิลด์แสดงอยู่แล้ว) */
        private void SwitchToCalcTab(Window form)
        {
            var tab = Retry(() => form.FindFirstDescendant(cf => cf.ByName(CalcTabName).And(cf.ByControlType(ControlType.TabItem)))
                                  ?? form.FindFirstDescendant(cf => cf.ByName(CalcTabName)), 3000);
            if (tab == null) return;

            if (tab.Patterns.SelectionItem.IsSupported)
            {
                tab.Patterns.SelectionItem.Pattern.Select();
                WaitUntil(() => tab.Patterns.SelectionItem.Pattern.IsSelected.ValueOrDefault, 3000);
            }
            else ClickByMouse(tab);
        }

        /* ติ๊ก/ปลดติ๊ก CheckBox แล้วรอจนสถานะเปลี่ยนจริง */
        private void ToggleCheckBox(Window form, string id, bool shouldBeChecked)
        {
            var chk = Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(id))?.AsCheckBox(), 5000)
                      ?? throw new Exception($"หา checkbox '{id}' ไม่เจอ");
            if (!WaitUntil(() => chk.Properties.IsEnabled.ValueOrDefault, 3000)) return;
            if (chk.IsChecked == shouldBeChecked) return;

            if (chk.Patterns.Toggle.IsSupported) chk.Patterns.Toggle.Pattern.Toggle();
            else ClickByMouse(chk);
            WaitUntil(() => chk.IsChecked == shouldBeChecked, 2000);
        }

        /* เลือกตัวเลือกใน RadioGroup แล้วรอจนถูกเลือกจริง */
        private void SelectRadio(Window form, string groupId, string option)
        {
            var group = Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(groupId)), 5000) ?? throw new Exception($"ไม่พบ {groupId}");
            var radio = group.FindFirstDescendant(cf => cf.ByName(option)) ?? throw new Exception($"ไม่พบตัวเลือก '{option}'");

            if (radio.Patterns.SelectionItem.IsSupported)
            {
                radio.Patterns.SelectionItem.Pattern.Select();
                WaitUntil(() => radio.Patterns.SelectionItem.Pattern.IsSelected.ValueOrDefault, 2000);
            }
            else ClickByMouse(radio);
        }

        /* เลือกพนักงาน: กด ... → รอตารางใน Pop-up โหลด → หาแถว (เลื่อน Page Down ได้) → ดับเบิลคลิก/Select → รอ Pop-up ปิด */
        private void SelectEmployee(Window form, string code)
        {
            var control = Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(EmployeeFieldId)), 5000)
                          ?? throw new Exception($"หาคอนโทรล 'พนักงาน' ({EmployeeFieldId}) ไม่เจอ");
            var lookupBtn = control.FindFirstDescendant(cf => cf.ByName("..."))
                            ?? control.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button))
                            ?? control;
            ClickByMouse(lookupBtn);

            var popup = Retry(() =>
            {
                foreach (var title in new[] { "เลือกพนักงาน", "ข้อมูลพนักงาน", "รายชื่อพนักงาน" })
                {
                    var w = _automation.GetDesktop().FindFirstChild(cf => cf.ByName(title))?.AsWindow();
                    if (w != null) return w;
                }
                return form.ModalWindows.FirstOrDefault();
            }) ?? throw new Exception("เปิด Pop-up เลือกพนักงานไม่สำเร็จ");

            try
            {
                WaitUntil(() => GetGridRows(popup).Length > 0, 8000);

                var row = FindRowByScrolling(popup, code)
                          ?? throw new Exception($"ไม่พบรหัส '{code}' ใน Pop-up เลือกพนักงาน");
                var r = row.BoundingRectangle;
                Mouse.DoubleClick(new Point(r.Left + r.Width / 2, r.Top + r.Height / 2));

                // ดับเบิลคลิกแล้ว Pop-up ยังไม่ปิด → กด Select
                if (!WaitForClosed(() => popup, 1500))
                {
                    var selectBtn = popup.FindFirstDescendant(cf => cf.ByName("Select").Or(cf.ByName("ตกลง")).Or(cf.ByName("เลือก")));
                    if (selectBtn != null) ClickByMouse(selectBtn);
                    else Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.KEY_S);
                }
                WaitForClosed(() => popup, 5000);
            }
            catch
            {
                // เลือกไม่สำเร็จ → ปิด Pop-up ก่อน ไม่งั้นจะบังฟอร์มให้เคสถัดไปพังตาม
                try { popup.FindFirstDescendant(cf => cf.ByName("Cancel").Or(cf.ByName("ยกเลิก")))?.Click(); WaitForClosed(() => popup, 3000); } catch { }
                throw;
            }
        }

        /* ดึงแถวของตาราง (DataItem หรือ ListItem) */
        private static AutomationElement[] GetGridRows(AutomationElement scope)
        {
            try
            {
                var rows = scope.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem));
                return rows.Length > 0 ? rows : scope.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem));
            }
            catch { return Array.Empty<AutomationElement>(); }
        }

        /* หาแถวในตารางที่มีรหัสตรงกับ code (ตาราง virtualized: ไม่เจอก็กด Page Down ทีละหน้าแล้วรอแถวใหม่โหลด) */
        private static AutomationElement? FindRowByScrolling(AutomationElement scope, string code)
        {
            AutomationElement? Match() => GetGridRows(scope)
                .FirstOrDefault(r => ReadText(r) == code || r.FindAllDescendants().Any(c => ReadText(c) == code));

            var found = Match();
            var pageDown = scope.FindFirstDescendant(cf => cf.ByName("Page Down"));
            for (int i = 0; found == null && pageDown != null && i < 15; i++)
            {
                ClickByMouse(pageDown);
                WaitUntil(() => (found = Match()) != null, 500);
            }
            return found;
        }

        /* ตรวจข้อความใน Task Result แบบหน้าจอนี้: รอนาน (80 วินาที), เทียบแบบไม่สนช่องว่าง และรวมข้อความยาวที่ถูกแยกหลาย element */
        private void AssertScreenTaskResult(Window form, string[] msgs, string testName)
        {
            var pending = new HashSet<string>(msgs);
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < SaveTimeoutMs && pending.Count > 0)
            {
                var panel = FindTaskResultPanel(form, 500);
                if (panel != null)
                {
                    var texts = GetPanelTexts(panel).Select(Squash).ToList();
                    texts.Add(string.Concat(texts));
                    foreach (var m in pending.ToList())
                    {
                        if (texts.Any(t => t.Contains(Squash(m)))) pending.Remove(m);
                    }
                }
            }

            if (pending.Count > 0) throw new Exception($"ไม่พบข้อความ: '{string.Join("', '", pending)}'");
            LogSuccess(testName, $"พบข้อความ: {string.Join(",", msgs.Select(m => $"'{m}'"))}");
        }

        /* คลิกด้วยเมาส์ที่กึ่งกลาง element (ปุ่ม DevExpress บางตัว .Click() แล้วไม่ติด) */
        private static void ClickByMouse(AutomationElement el)
        {
            var r = el.BoundingRectangle;
            if (!r.IsEmpty) Mouse.Click(new Point(r.Left + r.Width / 2, r.Top + r.Height / 2));
            else el.Click();
        }

        /* อ่านค่าจริงของ element (Value → LegacyIAccessible.Value → Name) */
        private static string ReadText(AutomationElement? e)
        {
            if (e == null) return "";
            try { if (e.Patterns.Value.IsSupported && !string.IsNullOrWhiteSpace(e.Patterns.Value.Pattern.Value.Value)) return e.Patterns.Value.Pattern.Value.Value.Trim(); } catch { }
            try { if (e.Patterns.LegacyIAccessible.IsSupported && !string.IsNullOrWhiteSpace(e.Patterns.LegacyIAccessible.Pattern.Value.Value)) return e.Patterns.LegacyIAccessible.Pattern.Value.Value.Trim(); } catch { }
            try { return (e.Name ?? "").Trim(); } catch { return ""; }
        }

        /* ตัดช่องว่างทั้งหมดออก (ข้อความในโปรแกรมเว้นวรรคไม่ตรงกับ TestStep) */
        private static string Squash(string s) => new string(s.Where(c => !char.IsWhiteSpace(c)).ToArray());


        // =========================================================================================
        // หมวดที่ 3: ส่วนการทำงานกลางที่ตายตัว (Common / Reusable Helpers)
        // **ใช้งานร่วมกันได้ทุกหน้าจอ ไม่ต้องแก้ไขคำสั่งเมื่อเปลี่ยนหน้า**
        // =========================================================================================

        private readonly AutomationElement _mainWindow;
        private readonly UIA3Automation _automation;
        private int _totalSuccess = 0;
        private int _totalFail = 0;
        private const int TimeoutMs = 6000;

        public ResignationInfoUC(AutomationElement mainWindow, UIA3Automation automation)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

        private Window GetOrOpenForm() => Retry(FindOpenForm, 1500) ?? OpenForm();

        private Window OpenForm()
        {
            var newBtn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("New")), 3000)
                         ?? throw new Exception("หาปุ่ม New ไม่เจอ");
            newBtn.Click();

            var form = Retry(FindOpenForm, 8000) ?? throw new Exception("หาฟอร์มไม่เจอ");
            WaitUntil(() => form.FindFirstDescendant(cf => cf.ByAutomationId(FirstObjectID)) != null, 4000);
            return form;
        }

        private Window ClickRowAndOpenEdit(string code)
        {
            RefreshGrid();

            var row = Retry(() => FindGridElement(code), 4000)
                      ?? throw new Exception($"หาแถว '{code}' ไม่เจอ");

            WaitUntil(() => { try { row.Focus(); return true; } catch { return false; } }, 1000);
            row.Click();

            var btn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("Edit")), 3000)
                      ?? throw new Exception("ไม่พบปุ่ม Edit");
            btn.Click();

            var form = Retry(FindOpenForm, 5000) ?? throw new Exception("หาฟอร์มไม่เจอ");
            WaitUntil(() => form.FindFirstDescendant(cf => cf.ByAutomationId(FirstObjectID)) != null, 3000);
            return form;
        }

        private void RunDeleteCase(string baseName, string code, string btn, bool expectDeleted)
        {
            string testName = $"{baseName}_{code}_{btn}";
            try
            {
                RefreshGrid();

                var elRow = Retry(() => FindGridElement(code), 4000)
                            ?? throw new Exception($"หาแถวรหัส '{code}' ในตารางไม่เจอ");

                WaitUntil(() => { try { elRow.Focus(); return true; } catch { return false; } }, 1000);
                elRow.Click();

                var delBtn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("Delete")))
                             ?? throw new Exception("หาปุ่ม Delete ไม่เจอ");
                delBtn.Click();

                var dlg = Retry(() => _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault()
                                      ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("คำเตือน"))?.AsWindow())
                          ?? throw new Exception("กด Delete แล้วไม่พบ Pop-up ยืนยัน");

                ClickDialogButton(dlg, btn);
                WaitForClosed(() => dlg);

                bool exists = VerifyRecordInGrid(code, timeoutMs: 3000, refresh: false);
                if (expectDeleted)
                {
                    if (exists) { LogFailure(testName, $"'{code}' ลบข้อมูลไม่สำเร็จ"); throw new Exception(); }
                    LogSuccess(testName, $"'{code}' ถูกลบสำเร็จ");
                }
                else
                {
                    if (!exists) { LogFailure(testName, $"'{code}' ลบข้อมูลผิดพลาด"); throw new Exception(); }
                    LogSuccess(testName, $"'{code}' ข้อมูลยังไม่ถูกลบ");
                }
            }
            catch (Exception ex) when (!string.IsNullOrEmpty(ex.Message))
            {
                LogFailure(testName, $"ลบรหัส '{code}' ไม่สำเร็จ: {ex.Message}");
            }
        }

        private bool TryOpenMenu(string stage, string searchKeyword)
        {
            try
            {
                var searchBox = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("baritemSearch"))
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)), 4000)
                              ?? throw new Exception("ไม่พบช่องค้นหา baritemSearch");

                WaitUntil(() => { try { searchBox.Focus(); return true; } catch { return false; } }, 1000);

                if (searchBox.Patterns.Value.IsSupported)
                    searchBox.Patterns.Value.Pattern.SetValue(searchKeyword);
                else
                    Keyboard.Type(searchKeyword);

                var searchBtn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("barChkSearch"))
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("Find")), 4000)
                              ?? throw new Exception("ไม่พบปุ่มค้นหา barChkSearch");

                searchBtn.Click();

                var menuResult = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName(searchKeyword))
                                               ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("treeListColumn row 1")), 4000)
                               ?? throw new Exception("ไม่พบผลลัพธ์เมนูจากการค้นหา");

                menuResult.Focus();
                menuResult.Click();
                Keyboard.Press(VirtualKeyShort.ENTER);

                return WaitUntil(() =>
                {
                    var newBtn = _mainWindow.FindFirstDescendant(cf => cf.ByName("New"));
                    return newBtn != null && newBtn.Properties.IsEnabled.ValueOrDefault && !newBtn.Properties.IsOffscreen.ValueOrDefault;
                }, 6000);
            }
            catch (Exception ex)
            {
                LogFailure(stage, $"ค้นหาและเปิดหน้าจอไม่สำเร็จ: {ex.Message}");
                return false;
            }
        }

        private void SetText(AutomationElement parent, string id, string text)
        {
            var txt = Retry(() => parent.FindFirstDescendant(cf => cf.ByAutomationId(id))?.AsTextBox(), 3000)
                      ?? throw new Exception($"ไม่พบช่อง {id}");
            WaitUntil(() => { try { txt.Focus(); return true; } catch { return false; } }, 1000);

            if (txt.Patterns.Value.IsSupported)
            {
                try
                {
                    txt.Patterns.Value.Pattern.SetValue(text ?? "");
                    return;
                }
                catch { }
            }

            txt.Click();
            Keyboard.Press(VirtualKeyShort.CONTROL);
            Keyboard.Type("a");
            Keyboard.Release(VirtualKeyShort.CONTROL);
            Keyboard.Press(VirtualKeyShort.BACK);
            Keyboard.Press(VirtualKeyShort.DELETE);

            if (!string.IsNullOrEmpty(text)) Keyboard.Type(text);
        }

        private AutomationElement? FindGridElement(string val) =>
            _mainWindow.FindAllDescendants().FirstOrDefault(el =>
                (!string.IsNullOrEmpty(el.Name) && string.Equals(el.Name.Trim(), val, StringComparison.OrdinalIgnoreCase)) ||
                (el.Patterns.Value.IsSupported && string.Equals(el.Patterns.Value.Pattern.Value.Value?.Trim(), val, StringComparison.OrdinalIgnoreCase)));

        private bool TrySaveAndHandleDuplicate(Window form, string testName, string code, string saveBtn)
        {
            var btn = form.FindFirstDescendant(cf => cf.ByName(saveBtn)) ?? throw new Exception($"ไม่พบปุ่ม {saveBtn}");
            btn.Click();

            if (testName.Contains("Validation") || testName.Contains("Duplicate")) return true;

            var panel = FindTaskResultPanel(form, 1500);
            if (panel == null) return true;

            string? dup = DetectDuplicateMessage(GetPanelTexts(panel));
            if (dup == null) return true;

            LogFailure(testName, $"'{code}' ข้อมูลซ้ำ: {dup}");
            try { ClickCloseAndHandleDialog(form, "No", $"{testName}_Close"); } catch { }
            return false;
        }

        private void RunCloseDialogSequence(Window form, string prefix, string expectedMsg)
        {
            string ySub = $"{prefix}_Close_Yes";
            ClickCloseAndHandleDialog(form, "Yes", ySub);
            AssertTaskResult(form, new[] { expectedMsg }, ySub, $"พบข้อความ: '{expectedMsg}' และ ฟอร์มถูกเปิดอยู่");
            CheckFormOpenOnly(form);

            string cSub = $"{prefix}_Close_Cancel";
            ClickCloseAndHandleDialog(form, "Cancel", cSub);
            AssertFormOpen(form, cSub);

            string nSub = $"{prefix}_Close_No";
            ClickCloseAndHandleDialog(form, "No", nSub);
            AssertFormClosed(form, nSub);
        }

        private void ClickCloseAndHandleDialog(Window form, string btnName, string testName)
        {
            form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();
            var dlg = Retry(() => form.ModalWindows.FirstOrDefault()
                                   ?? _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault()
                                   ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("คำเตือน"))?.AsWindow(), 4000)
                      ?? throw new Exception("ไม่พบ Pop-up ยืนยัน");
            ClickDialogButton(dlg, btnName);
            WaitForClosed(() => dlg);
        }

        private void ClickDialogButton(Window dlg, string name)
        {
            var btn = Retry(() => dlg.FindFirstDescendant(cf => cf.ByName(name))?.AsButton()
                                   ?? dlg.FindFirstDescendant(cf => cf.ByName($"&{name}"))?.AsButton(), 2000)
                      ?? throw new Exception($"ไม่พบปุ่ม '{name}'");
            btn.Click();
        }

        private void CheckFormOpenOnly(Window form)
        {
            if (!WaitUntil(() => !form.Properties.IsOffscreen.ValueOrDefault && form.IsAvailable, 2000))
                throw new Exception("ฟอร์มถูกปิด");
        }

        private void AssertFormOpen(Window form, string testName)
        {
            CheckFormOpenOnly(form);
            LogSuccess(testName, "ฟอร์มถูกเปิดอยู่");
        }

        private void AssertFormClosed(Window form, string testName)
        {
            // ค้นหาฟอร์มใหม่แทนการอ่าน property ของ element เดิม (ซึ่งจะ throw หลังฟอร์มถูกปิด)
            if (!WaitForClosed(() => FindOpenForm(), 3000))
                throw new Exception("ปิดฟอร์มไม่สำเร็จ");
            LogSuccess(testName, "ฟอร์มถูกปิด");
        }

        private void AssertTaskResult(Window form, string[] msgs, string testName, string? customMsg = null)
        {
            var pending = new HashSet<string>(msgs);
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < 5000 && pending.Count > 0)
            {
                var panel = FindTaskResultPanel(form, 300);
                if (panel != null)
                {
                    var texts = GetPanelTexts(panel).ToList();
                    foreach (var m in pending.ToList())
                    {
                        if (texts.Any(t => t.Contains(m))) pending.Remove(m);
                    }
                }
                if (pending.Count == 0) break;
            }

            if (pending.Count > 0) throw new Exception($"ไม่พบข้อความ: '{string.Join("', '", pending)}'");

            string logMsg = customMsg ?? $"พบข้อความ: {string.Join(",", msgs.Select(m => $"'{m}'"))}";
            LogSuccess(testName, logMsg);
        }

        private AutomationElement? FindTaskResultPanel(Window form, int timeoutMs) =>
            Retry(() => form.FindFirstDescendant(cf => cf.ByName("Task Result"))
                       ?? form.FindFirstDescendant(cf => cf.ByAutomationId("dockPanelTaskResult"))
                       ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("Task Result"))
                       ?? _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("dockPanelTaskResult")), timeoutMs);

        private IEnumerable<string> GetPanelTexts(AutomationElement panel)
        {
            foreach (var el in panel.FindAllDescendants())
            {
                if (!string.IsNullOrEmpty(el.Name)) yield return el.Name;
                if (el.Patterns.Value.IsSupported && !string.IsNullOrEmpty(el.Patterns.Value.Pattern.Value.Value))
                    yield return el.Patterns.Value.Pattern.Value.Value;
            }
        }

        private void RefreshGrid()
        {
            try { _mainWindow.FindFirstDescendant(cf => cf.ByName("Find"))?.Click(); } catch { }
            try
            {
                var g = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataGrid))
                                    ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Table)), 2000);
                WaitUntil(() => { try { g?.Focus(); return true; } catch { return false; } }, 1000);
                g?.Click();
            }
            catch { }
        }

        private bool VerifyRecordInGrid(string val, int timeoutMs = 5000, bool refresh = true)
        {
            if (refresh) RefreshGrid();
            return Retry(() => FindGridElement(val), timeoutMs) != null;
        }

        private void LogGridResult(string testName, string code)
        {
            if (VerifyRecordInGrid(code)) LogSuccess(testName, $"'{code}' เพิ่มสำเร็จ (พบในตารางหลัก)");
            else LogFailure(testName, $"'{code}' ไม่พบในตารางหลักหลังบันทึก");
        }

        private void RunStep(string testName, Action act)
        {
            try { act(); }
            catch (Exception ex) { LogFailure(testName, $"ไม่ผ่าน: {ex.Message}"); }
        }

        private void RunStep(string testName, Action act, int progress, string progressMessage)
        {
            RunStep(testName, act);
            TestProgressHelper.Report(progress, progressMessage);
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

        private static bool WaitUntil(Func<bool> condition, int timeoutMs = TimeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try { if (condition()) return true; } catch { }
            }
            return false;
        }

        private static bool WaitForClosed(Func<AutomationElement?> finder, int timeoutMs = TimeoutMs) =>
            WaitUntil(() =>
            {
                try
                {
                    var el = finder();
                    return el == null || !el.IsAvailable || el.Properties.IsOffscreen.ValueOrDefault;
                }
                catch { return true; }
            }, timeoutMs);

        private T? Retry<T>(Func<T?> finder, int timeoutMs = TimeoutMs) where T : class
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
            }
            return null;
        }
    }
}