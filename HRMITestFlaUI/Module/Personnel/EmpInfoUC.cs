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

namespace HRMITestFlaUI.Module.Personnel
{
    public class EmpInfoUC
    {
        // =========================================================================================
        // หมวดที่ 1: ข้อมูลการทดสอบทั้ง 5 ขั้น (Test Data)
        // =========================================================================================

        // --- Data Records สำหรับใช้งานในแต่ละเฟส ---
        private sealed record AddItem(
            string Code,
            string CardNo,           // "" = ข้ามขั้นตอนเลือกหมายเลขบัตร
            string Title,
            string FirstName,
            string LastName,
            string Position,
            string Org,
            string StartDate,
            string PayPeriod,
            string SaveBtn,
            bool NeedsManualClose);
        private sealed record ValidationItem(
            string TestCaseId,
            string? Code,            // null = ไม่แตะช่อง, "" = ลบค่า (ใช้กับทุกช่องด้านล่าง)
            string? Title,
            string? FirstName,
            string? LastName,
            string? StartDate,
            string? Shift,
            string? PayPeriod,
            string SaveBtn,
            int Progress,
            string ProgressMsg,
            string[] ExpectedErrors);
        private sealed record EditItem(
            string Name,
            string OldCode,
            string NewCode,
            string PayPeriod,
            string SaveBtn,
            bool NeedsManualClose);
        private sealed record DeleteItem(
            string TargetCode,
            string DialogBtn,
            bool ExpectDeleted,
            int Progress,
            string ProgressMsg);

        // ข้อมูลที่ใช้ซ้ำหลายเคส
        private const string Shift = "NRM-M01";
        private const string PayTwice = "งวดการจ่ายเดือนละ 2 ครั้ง";
        private const string Pay21To20 = "งวดการจ่ายวันที่ 21 เดือนก่อน-20เดือนปัจจุบัน";

        // [ขั้นที่ 1] ข้อมูลทดสอบการสร้างใหม่ (Add / Navigate)
        private static readonly AddItem[] Step1_AddCases =
        {
            new(
                "EMP00001",
                "EMP0001",
                "นาย",
                "กิตติพงษ์",
                "ใจดี",
                "H-001",
                "001",
                "01/1/2569",
                PayTwice,
                "Save",
                true),
            new(
                "EMP00002",
                "EMP0002",
                "นางสาว",
                "พิมพ์ชนก",
                "แสงทอง",
                "P-001",
                "002",
                "15/01/2569",
                Pay21To20,
                "Save and New",
                false),
            new(
                "EMP00003",
                "EMP0003",
                "นาย",
                "ธนกร",
                "สุขใจ",
                "H-001",
                "001",
                "01/02/2569",
                PayTwice,
                "Save and New",
                false),
            new(
                "EMP00004",
                "EMP0004",
                "นาย",
                "ภานุ",
                "แก้วมณี",
                "P-001",
                "002",
                "15/02/2569",
                Pay21To20,
                "Save and New",
                false),
            new(
                "EMP00005",
                "",
                "นางสาว",
                "ศิริพร",
                "วัฒนชัย",
                "H-001",
                "001",
                "08/11/2566",
                PayTwice,
                "Save and New",
                false),
            new(
                "EMP00006",
                "",
                "นางสาว",
                "ชลธิชา",
                "รัตนวงศ์",
                "P-001",
                "002",
                "08/11/2542",
                Pay21To20,
                "Save and New",
                false),
            new(
                "EMP00007",
                "",
                "นางสาว",
                "สุภัสสรา",
                "วัฒนานนท์",
                "H-001",
                "001",
                "01/03/2569",
                PayTwice,
                "Save and Close",
                false)
        };

        // [ขั้นที่ 2] ข้อมูลทดสอบ Validation ตอนเพิ่ม (Add Validation)
        private static readonly ValidationItem[] Step2_AddValidationCases =
        {
            new("TestCase01_MissingEmployeeCode", "", "นางสาว", "พิมพ์ชนก", "ศรีวัฒนา", "01/05/2569", Shift, PayTwice, "Save", 25,
                "Validating: Missing Employee Code...", new[] { "รหัสพนักงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล" }),

            new("TestCase02_MissingEmployeeName", "", null, "", "", "01/05/2569", null, Pay21To20, "Save and New", 29,
                "Validating: Missing Employee Name...", new[] { "ชื่อพนักงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล", "สัญชาติไทย ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล" }),

            new("TestCase03_MissingShiftAndPayPeriod", "EMP08", "นางสาว", "พิมพ์ชนก", "ศรีวัฒนา", "01/05/2569", "", "", "Save and New", 33,
                "Validating: Missing Shift and Pay Period...", new[] { "รหัสกะงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล", "งวดการจ่าย ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล" }),

            new("TestCase04_DuplicateEmployeeCode", "EMP00001", "นางสาว", "พิมพ์ชนก", "ศรีวัฒนา", "01/05/2569", Shift, Pay21To20, "Save and New", 37,
                "Validating: Duplicate Employee Code...", new[] { "รหัสพนักงาน ห้ามซ้ำ กรุณาป้อนข้อมูล" })
        };

        // [ขั้นที่ 3] ข้อมูลทดสอบการแก้ไขข้อมูล (Edit Records)
        private static readonly EditItem[] Step3_EditCases =
        {
            new(
                "TestCase06_EditEmp005",
                "EMP00005",
                "EP0005",
                PayTwice,
                "Save",
                true),
            new(
                "TestCase07_EditEmp006",
                "EMP00006",
                "EP0006",
                Pay21To20,
                "Save and Close",
                false)
        };

        // [ขั้นที่ 4] ข้อมูลทดสอบ Validation ตอนแก้ไข (Edit Validation)
        private const string EditTargetRow = "EMP00002";
        private static readonly ValidationItem[] Step4_EditValidationCases =
        {
            new("TestCase08_EditValidation_MissingEmployeeCode", "", null, null, null, null, null, null, "Save", 64,
                "Validating Edit: Missing Code...", new[] { "รหัสพนักงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล" }),

            new("TestCase08_EditValidation_MissingEmployeeName", "EMP06", null, "", "", null, null, null, "Save and New", 70,
                "Validating Edit: Missing Name...", new[] { "ชื่อพนักงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล", "สัญชาติไทย ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล" }),

            new("TestCase08_EditValidation_DuplicateEmployeeCode", "EMP00001", null, "สุภัสสรา", "วัฒนานนท์", null, null, null, "Save and New", 76,
                "Validating Edit: Duplicate Code...", new[] { "รหัสพนักงาน ห้ามซ้ำ กรุณาป้อนข้อมูล" })
        };

        // [ขั้นที่ 5] ข้อมูลทดสอบการลบข้อมูล (Delete Records)
        private static readonly DeleteItem[] Step5_DeleteCases =
        {
            new("EMP00007", "Yes", true, 92, "Delete Test: EMP00007 (confirmed delete)..."),
            new("EP0006", "No", false, 100, "Delete Test: EP0006 (cancelled delete)...")
        };


        // =========================================================================================
        // หมวดที่ 2: ส่วนที่ขึ้นกับหน้าจอนั้นๆ (Screen-Specific Implementation)
        // **เมื่อเปลี่ยนไปทำหน้าจออื่น ต้องมาแก้ไขค่าและเมทอดในส่วนนี้ทั้งหมด**
        // =========================================================================================

        // 2.1 Locator & Mappings ประจำหน้าจอ
        private const string MenuName = "บันทึกข้อมูลพนักงาน";
        private const string ScreenLogName = "EmpInfoUC";
        private const string GeneralTabId = "devTabPgGeneral";
        private const string FirstObjectID = "devTxtEmpCode";
        private const string EmploymentTabName = "การว่าจ้าง";
        private const string PaymentTabName = "การจ่ายเงิน";
        private const int TaskResultTimeoutMs = 80000; // หน้านี้ตรวจข้อมูลก่อนบันทึกนาน ต้องรอข้อความใน Task Result นาน

        // 2.2 ฟิลด์ Input ประจำหน้าจอ
        private const string CodeFieldId = "devTxtEmpCode";
        private const string CardNoFieldId = "devBtnCardNo";
        private const string TitleFieldId = "devLookUpTitle";
        private const string FirstNameFieldId = "devTxtFirstName";
        private const string LastNameFieldId = "devTxtLastName";
        private const string PositionFieldId = "devBtnEditPosition";
        private const string OrgFieldId = "devBtnEditOrgName";
        private const string StartDateFieldId = "devDtStartDate";
        private const string ShiftFieldId = "devBtnEditshift";
        private const string PayPeriodFieldId = "devBtnPeriod";

        /// <summary>
        /// ขั้นที่ 1: เปิดหน้าจอและกรอกข้อมูลสร้างใหม่
        /// </summary>
        public void NavigateToEmpInfoUC()
        {
            const string stageName = "TestCase00_AddEmpInfo";
            TestLogger.LogInfo(ScreenLogName, $"เริ่มทดสอบหน้าจอ {MenuName}");
            TestProgressHelper.Report(2, $"Opening Personnel -> {MenuName} Menu via Search...");

            if (!TryOpenMenu(stageName, MenuName)) return;

            Window? form = null;
            for (int i = 0; i < Step1_AddCases.Length; i++)
            {
                var item = Step1_AddCases[i];
                string testName = $"{stageName}_{item.Code}";
                // หน้านี้มี 7 รายการ จึงเฉลี่ย Progress ให้อยู่ในช่วง 4 -> 20
                TestProgressHelper.Report(4 + (i * 16 / Step1_AddCases.Length), $"Adding {MenuName} ({i + 1}/{Step1_AddCases.Length}): {item.Code} - {item.FirstName}...");

                try
                {
                    form ??= GetOrOpenForm();
                    var tab = GetGeneralTab(form);
                    FillFormData(tab, form, item);

                    if (!TrySaveAndHandleDuplicate(form, testName, item.Code, item.SaveBtn))
                    {
                        form = null;
                        continue;
                    }
                    HandlePostSavePopup(form, item.Code); // เฉพาะหน้าจอนี้: Pop-up "กำหนดข้อมูลพนักงานเพิ่มเติม" หลังบันทึก

                    if (item.SaveBtn == "Save and New")
                    {
                        WaitUntil(() => string.IsNullOrEmpty(tab.FindFirstDescendant(cf => cf.ByAutomationId(FirstObjectID))?.AsTextBox().Text), 3000);
                        LogSuccess(testName, $"'{item.Code}' บันทึกสำเร็จ (Save and New)");
                        form = null; // เฉพาะหน้าจอนี้: Save and New เปิดฟอร์มใหม่ จึงต้องหาฟอร์มใหม่ทุกครั้ง
                    }
                    else
                    {
                        if (item.NeedsManualClose) form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();
                        WaitForClosed(() => FindOpenForm());
                        form = null;
                        LogGridResult(testName, item.Code);
                    }
                }
                catch (Exception ex)
                {
                    LogFailure(testName, $"'{item.Code}' เพิ่มไม่สำเร็จ: {ex.Message}");
                    form = null;
                }
            }
        }

        /* Method เดิมที่ชุดทดสอบอาจเรียกอยู่ — ทำงานเหมือน NavigateToEmpInfoUC */
        public void RunAddNewTests() => NavigateToEmpInfoUC();

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
            const string dupName = "TestCase05_CloseSequence";
            RunStep(dupName,
                () => RunCloseDialogSequence(form, dupName, "รหัสพนักงาน ห้ามซ้ำ กรุณาป้อนข้อมูล"),
                40, "Validating: Duplicate Code Close Sequence...");
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
                    var form = ClickRowAndOpenEdit(item.OldCode);
                    var tab = GetGeneralTab(form);
                    TypeText(tab, CodeFieldId, item.NewCode);
                    SelectTab(form, PaymentTabName);
                    SelectLookup(form, PayPeriodFieldId, item.PayPeriod);

                    if (!TrySaveAndHandleDuplicate(form, item.Name, item.NewCode, item.SaveBtn)) continue;
                    HandlePostSavePopup(form, item.NewCode);
                    if (item.NeedsManualClose) form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();

                    WaitForClosed(() => FindOpenForm());
                    RefreshGrid();

                    bool recordFound = VerifyRecordInGrid(item.NewCode, refresh: false);
                    if (recordFound)
                        LogSuccess(item.Name, $"แก้ไข '{item.OldCode}' สำเร็จ (พบข้อมูลที่แก้ไขในตารางหลัก)");
                    else
                        LogFailure(item.Name, $"แก้ไข '{item.OldCode}' แล้ว แต่ไม่พบข้อมูลที่อัปเดตในตารางหลัก");
                }
                catch (Exception ex)
                {
                    LogFailure(item.Name, $"แก้ไข '{item.OldCode}' ไม่สำเร็จ: {ex.Message}");
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
            const string baseName = "TestCase08_EditValidation";
            TestProgressHelper.Report(58, "Opening Edit Form for Validation Tests...");

            Window form;
            try { form = ClickRowAndOpenEdit(EditTargetRow); }
            catch (Exception ex) { LogFailure(baseName, $"เปิดฟอร์ม Edit ไม่สำเร็จ: {ex.Message}"); return; }

            var tab = GetGeneralTab(form);

            foreach (var testCase in Step4_EditValidationCases)
            {
                RunStep(testCase.TestCaseId,
                    () => FillValidationAndAssert(tab, form, testCase, testCase.TestCaseId),
                    testCase.Progress, testCase.ProgressMsg);
            }

            // เฉพาะหน้าจอนี้: TestStep ให้กด Close แล้วกด No เท่านั้น (ไม่มี Yes / Cancel)
            string closeNo = $"{baseName}_Close_No";
            RunStep(closeNo,
                () =>
                {
                    ClickCloseAndHandleDialog(form, "No", closeNo);
                    AssertFormClosed(form, closeNo);
                },
                80, "Validating Edit: Close -> No...");

            LogSuccess(baseName, "รัน Validation Edit ครบทุกเคสแล้ว");
        }

        /// <summary>
        /// ขั้นที่ 5: ทดสอบการลบรายการในตาราง
        /// </summary>
        public void RunDeleteTests()
        {
            const string baseName = "TestCase09_Delete";
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

        /* กรอกข้อมูลพนักงานครบ 3 แท็บ (ทั่วไป, การว่าจ้าง, การจ่ายเงิน) */
        private void FillFormData(AutomationElement tab, Window form, AddItem item)
        {
            TypeText(tab, CodeFieldId, item.Code);
            if (item.CardNo.Length > 0) SelectLookup(tab, CardNoFieldId, item.CardNo);
            SelectComboBoxItem(tab, TitleFieldId, item.Title);
            TypeText(tab, FirstNameFieldId, item.FirstName);
            TypeText(tab, LastNameFieldId, item.LastName);

            SelectTab(form, EmploymentTabName);
            SelectLookup(form, PositionFieldId, item.Position);
            SelectLookup(form, OrgFieldId, item.Org);
            TypeText(form, StartDateFieldId, item.StartDate);
            SelectLookup(form, ShiftFieldId, Shift);

            SelectTab(form, PaymentTabName);
            SelectLookup(form, PayPeriodFieldId, item.PayPeriod);
        }

        /* หน้านี้มีหลายแท็บและหลายช่อง จึงรับข้อมูลทั้งเคส (null = ไม่แตะช่องนั้น, "" = ลบค่า) */
        private void FillValidationAndAssert(AutomationElement tab, Window form, ValidationItem tc, string testName)
        {
            GetGeneralTab(form);
            if (tc.Code != null) TypeText(tab, CodeFieldId, tc.Code);
            if (tc.Title != null) SelectComboBoxItem(tab, TitleFieldId, tc.Title);
            if (tc.FirstName != null) TypeText(tab, FirstNameFieldId, tc.FirstName);
            if (tc.LastName != null) TypeText(tab, LastNameFieldId, tc.LastName);

            if (tc.StartDate != null || tc.Shift != null)
            {
                SelectTab(form, EmploymentTabName);
                if (tc.StartDate != null) TypeText(form, StartDateFieldId, tc.StartDate);
                if (tc.Shift != null) SelectLookup(form, ShiftFieldId, tc.Shift);
            }
            if (tc.PayPeriod != null)
            {
                SelectTab(form, PaymentTabName);
                SelectLookup(form, PayPeriodFieldId, tc.PayPeriod);
            }

            form.FindFirstDescendant(cf => cf.ByName(tc.SaveBtn))?.Click();
            AssertScreenTaskResult(form, tc.ExpectedErrors, testName);
        }

        private static string? DetectDuplicateMessage(IEnumerable<string> texts)
        {
            if (texts.Any(t => t.Contains("รหัสพนักงาน ห้ามซ้ำ"))) return "รหัสพนักงานซ้ำ";
            return null;
        }

        /* ถ้าแท็บอื่นแสดงอยู่ จะคลิกกลับมาที่แท็บแรกก่อน */
        private AutomationElement GetGeneralTab(Window form)
        {
            var page = Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(GeneralTabId)), 3000);
            if (page != null && page.Properties.IsOffscreen.ValueOrDefault)
            {
                var firstTab = form.FindFirstDescendant(cf => cf.ByControlType(ControlType.TabItem));
                if (firstTab != null) ClickWithMouse(firstTab);
                WaitUntil(() => !page.Properties.IsOffscreen.ValueOrDefault, 3000);
            }
            return page ?? form;
        }

        /* ฟอร์มหน้านี้ไม่มี AutomationId ประจำ จึงหาจากชื่อหน้าต่าง */
        private Window? FindOpenForm() =>
            _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault() ??
            _automation.GetDesktop().FindAllChildren(cf => cf.ByControlType(ControlType.Window))
                .FirstOrDefault(w => (w.Name ?? "").Contains(": New") || (w.Name ?? "").Contains(": Edit")
                                     || (w.Name ?? "").StartsWith($"{MenuName} :"))?.AsWindow();

        // --- การกรอกข้อมูลเฉพาะหน้าจอนี้ (ช่อง DevExpress ต้องพิมพ์ด้วยคีย์บอร์ดจริง) ---

        /* พิมพ์ข้อความด้วยคีย์บอร์ด (ค่าว่าง = ลบข้อความ) แล้ว Tab ออกเพื่อ commit */
        private void TypeText(AutomationElement scope, string id, string text)
        {
            var field = Retry(() => scope.FindFirstDescendant(cf => cf.ByAutomationId(id)), 8000) ?? throw new Exception($"ไม่พบช่อง {id}");
            WaitUntil(() => field.Properties.IsEnabled.ValueOrDefault, 3000);

            ClickWithMouse(field);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            Keyboard.Type(VirtualKeyShort.DELETE);
            if (text.Length == 0)
                for (int i = 0; i < 10; i++) Keyboard.Type(VirtualKeyShort.BACK); // กัน mask ของช่องวันที่เหลือตัวอักษรค้าง
            else
                Keyboard.Type(text);

            WaitUntil(() => text.Length == 0 || GetValue(scope, id).Contains(text), 1500);
            Keyboard.Type(VirtualKeyShort.TAB);
        }

        /* อ่านค่าในช่องตาม Automation ID (อ่านจากช่อง Edit ด้านในถ้ามี) */
        private string GetValue(AutomationElement scope, string id)
        {
            try
            {
                var el = scope.FindFirstDescendant(cf => cf.ByAutomationId(id));
                if (el == null) return "";
                var edit = el.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)) ?? el;
                return (edit.Patterns.Value.IsSupported ? edit.Patterns.Value.Pattern.Value.Value : edit.Name) ?? "";
            }
            catch { return ""; }
        }

        /* สลับแท็บในฟอร์ม แล้วรอจนแท็บถูกเลือกจริง */
        private void SelectTab(Window form, string tabName)
        {
            var tabItem = Retry(() => form.FindFirstDescendant(cf => cf.ByName(tabName).And(cf.ByControlType(ControlType.TabItem)))
                                      ?? form.FindFirstDescendant(cf => cf.ByName(tabName)), 3000)
                          ?? throw new Exception($"หา Tab '{tabName}' ไม่เจอ");
            ClickWithMouse(tabItem);
            WaitUntil(() => !tabItem.Patterns.SelectionItem.IsSupported || tabItem.Patterns.SelectionItem.Pattern.IsSelected.Value, 3000);
        }

        /* เลือกค่าใน ComboBox (คำนำหน้าชื่อ) แล้วรอจนค่าแสดงในช่อง */
        private void SelectComboBoxItem(AutomationElement scope, string id, string itemText)
        {
            var combo = Retry(() => scope.FindFirstDescendant(cf => cf.ByAutomationId(id)), 8000) ?? throw new Exception($"ไม่พบช่อง {id}");
            ClickWithMouse(combo);

            var item = Retry(() => _automation.GetDesktop().FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem))
                                       .FirstOrDefault(li => (li.Name ?? "").Trim() == itemText), 4000);
            if (item != null) ClickWithMouse(item);
            else { Keyboard.Type(itemText); Keyboard.Type(VirtualKeyShort.RETURN); }

            WaitUntil(() => GetValue(scope, id).Contains(itemText), 2000);
        }

        /* เลือกข้อมูลจาก Pop-up Lookup: กด ... → รอ Pop-up → คลิกแถวที่ตรง → กด Select → รอ Pop-up ปิด (ค่าว่าง = ลบค่าในช่อง) */
        private void SelectLookup(AutomationElement scope, string id, string itemText)
        {
            var control = Retry(() => scope.FindFirstDescendant(cf => cf.ByAutomationId(id)), 5000) ?? throw new Exception($"ไม่พบช่อง {id}");

            if (itemText.Length == 0)
            {
                var edit = control.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)) ?? control;
                if (edit.Patterns.Value.IsSupported) edit.Patterns.Value.Pattern.SetValue("");
                WaitUntil(() => GetValue(scope, id).Length == 0, 2000);
                return;
            }

            Exception? lastEx = null;
            for (int attempt = 1; attempt <= 2; attempt++) // Pop-up ของหน้านี้บางครั้งเปิดไม่ติด ให้ลองซ้ำอีก 1 ครั้ง
            {
                Window? popup = null;
                try
                {
                    var btnDots = control.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button)) ?? control;
                    ClickWithMouse(btnDots);

                    popup = Retry(() => FindOpenForm()?.ModalWindows.FirstOrDefault()
                                        ?? _automation.GetDesktop().FindAllChildren(cf => cf.ByControlType(ControlType.Window))
                                               .FirstOrDefault(w => w.FindFirstDescendant(cf => cf.ByName("Select")) != null)?.AsWindow(), 6000)
                            ?? throw new Exception($"กด ... ของช่อง {id} แล้ว Pop-up ไม่เปิด");

                    var row = Retry(() => popup.FindAllDescendants().FirstOrDefault(el => MatchesLookupText(el, itemText)), 6000)
                              ?? throw new Exception($"ไม่พบ '{itemText}' ใน Pop-up ของช่อง {id}");
                    ClickWithMouse(row);

                    var btnSelect = Retry(() => popup.FindFirstDescendant(cf => cf.ByName("Select"))
                                                ?? popup.FindFirstDescendant(cf => cf.ByName("เลือก")), 3000);
                    if (btnSelect != null) ClickWithMouse(btnSelect);
                    else Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.KEY_S);

                    if (!WaitForClosed(() => popup)) throw new Exception($"กด Select แล้ว Pop-up ของช่อง {id} ไม่ปิด");
                    return;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    if (popup != null && popup.IsAvailable)
                    {
                        var cancel = popup.FindFirstDescendant(cf => cf.ByName("Cancel").And(cf.ByControlType(ControlType.Button)));
                        if (cancel != null) ClickWithMouse(cancel); else popup.Close();
                        WaitForClosed(() => popup);
                    }
                }
            }
            throw lastEx ?? new Exception($"เลือกข้อมูลช่อง {id} ไม่สำเร็จ");
        }

        /* เช็คว่าข้อความในแถวตรงกับค่าที่ต้องการ (ไม่สนช่องว่าง และรองรับชื่องวด 21-20 ที่แสดงไม่ครบ) */
        private static bool MatchesLookupText(AutomationElement el, string itemText)
        {
            string text;
            try { text = el.Patterns.Value.IsSupported ? el.Patterns.Value.Pattern.Value.Value ?? "" : el.Name ?? ""; }
            catch { return false; }
            if (text.Length == 0) try { text = el.Name ?? ""; } catch { }
            if (text.Length == 0) return false;

            if (Squash(text).Contains(Squash(itemText))) return true;
            return itemText.Contains("21") && itemText.Contains("20") && text.Contains("21") && text.Contains("20");
        }

        /* Pop-up ที่เด้งหลังบันทึก
           - "กำหนดข้อมูลพนักงานเพิ่มเติม": เลือกเพอร์มิชชั่น (กด ... → Select) แล้วกด OK  (EMP00007: TestStep ให้กด Cancel)
           - Pop-up อื่น: กด No / Cancel / OK ตามที่มี */
        private void HandlePostSavePopup(Window form, string code)
        {
            for (int guard = 0; guard < 3; guard++)
            {
                Window? popup = null;
                try
                {
                    popup = Retry(() => form.ModalWindows.FirstOrDefault()
                                        ?? _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault(w => !w.Equals(form)), 2500);
                }
                catch { }
                if (popup == null) return;

                bool isExtraInfo = popup.FindFirstDescendant(cf => cf.ByName("ชื่อเพอร์มิชชั่น")) != null
                                   || (popup.Name ?? "").Contains("กำหนดข้อมูลพนักงานเพิ่มเติม");

                if (isExtraInfo && code != "EMP00007")
                {
                    try
                    {
                        var dots = popup.FindAllDescendants(cf => cf.ByControlType(ControlType.Button)).FirstOrDefault(b => b.Name == "..." || string.IsNullOrEmpty(b.Name));
                        if (dots != null)
                        {
                            ClickWithMouse(dots);
                            var perm = Retry(() => popup.ModalWindows.FirstOrDefault()
                                                   ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("Define Permission"))?.AsWindow(), 5000);
                            var sel = perm?.FindFirstDescendant(cf => cf.ByName("Select"));
                            if (perm != null && sel != null) { ClickWithMouse(sel); WaitForClosed(() => perm); }
                        }
                    }
                    catch { }
                    ClickDialogButton(popup, "OK");
                }
                else
                {
                    var names = code == "EMP00007" ? new[] { "Cancel", "No", "Close", "OK" } : new[] { "No", "Cancel", "Close", "OK" };
                    var btn = names.Select(n => popup.FindFirstDescendant(cf => cf.ByName(n).And(cf.ByControlType(ControlType.Button))))
                                   .FirstOrDefault(b => b != null);
                    if (btn != null) ClickWithMouse(btn); else popup.Close();
                }

                WaitForClosed(() => popup);
            }
        }

        /* ตรวจข้อความใน Task Result แบบหน้าจอนี้: รอนาน (80 วินาที) และเทียบแบบไม่สนช่องว่าง */
        private void AssertScreenTaskResult(Window form, string[] msgs, string testName)
        {
            var pending = new HashSet<string>(msgs);
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < TaskResultTimeoutMs && pending.Count > 0)
            {
                var panel = FindTaskResultPanel(form, 500);
                if (panel != null)
                {
                    var texts = GetPanelTexts(panel).Select(Squash).ToList();
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
        private static void ClickWithMouse(AutomationElement element)
        {
            var r = element.BoundingRectangle;
            if (!r.IsEmpty) Mouse.Click(new Point(r.X + r.Width / 2, r.Y + r.Height / 2));
            else element.Click();
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

        public EmpInfoUC(AutomationElement mainWindow, UIA3Automation automation)
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
            if (!WaitUntil(() => form.Properties.IsOffscreen.ValueOrDefault || !form.IsAvailable, 3000))
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