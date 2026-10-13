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
    public class PromoteInfoUC
    {
        // =========================================================================================
        // หมวดที่ 1: ข้อมูลการทดสอบทั้ง 5 ขั้น (Test Data)
        // =========================================================================================

        // --- Data Records สำหรับใช้งานในแต่ละเฟส ---
        private sealed record AddItem(
            string Code,
            string PromoteDate,
            string EffectDate,
            string Amount,
            string Level,
            string Position,
            string Org,
            string SaveBtn,
            bool NeedsManualClose);
        private sealed record ValidationItem(
            string TestCaseId,
            string Code,             // "" = ลบพนักงานออก
            string? PromoteDate,     // null = ไม่แตะ, "" = ลบวันที่
            string? EffectDate,
            string? Amount,          // ประเภทการปรับ: null = ไม่ติ๊ก
            string? Level,
            string? Position,
            string? Org,
            string SaveBtn,
            int Progress,
            string ProgressMsg,
            string[] ExpectedErrors);
        private sealed record EditItem(
            string Name,
            string Code,
            string? Amount,          // null = ไม่ติ๊กประเภทการปรับนั้น
            string? Position,
            string? Org,
            string SaveBtn,
            bool NeedsManualClose);
        private sealed record DeleteItem(
            string TargetCode,
            string DialogBtn,
            bool ExpectDeleted,
            int Progress,
            string ProgressMsg);

        // ข้อความแจ้งเตือนที่คาดหวัง (ใช้คำสำคัญ เพราะข้อความเต็มมีเลขที่เอกสารที่เปลี่ยนทุกครั้ง)
        private const string MsgEmployeeEmpty = "พนักงาน ไม่สามารถเป็นค่าว่าง";
        private const string MsgNoPromoteType = "ยังไม่ได้เลือก";

        // [ขั้นที่ 1] ข้อมูลทดสอบการสร้างใหม่ (Add / Navigate) — ติ๊กครบทั้ง 3 ประเภทการปรับ
        private static readonly AddItem[] Step1_AddCases =
        {
            new(
                "EMP00001",
                "01/01/2570",
                "31/01/2570",
                "100",
                "EL-001",
                "H-001",
                "ORG001",
                "Save",
                true),
            new(
                "EMP00002",
                "01/02/2570",
                "20/02/2570",
                "1000",
                "EL-002",
                "P-001",
                "ORG003",
                "Save and New",
                false),
            new(
                "EMP00003",
                "01/03/2570",
                "31/03/2570",
                "50",
                "EL-003",
                "H-001",
                "ORG003",
                "Save and New",
                false),
            new(
                "EMP00004",
                "01/04/2570",
                "01/05/2570",
                "100",
                "EL-004",
                "P-001",
                "ORG004",
                "Save and New",
                false),
            new(
                "EP0005",
                "20/05/2570",
                "25/07/2570",
                "100",
                "EL-005",
                "H-001",
                "ORG005",
                "Save and Close",
                false)
        };

        // [ขั้นที่ 2] ข้อมูลทดสอบ Validation ตอนเพิ่ม (Add Validation)
        private static readonly ValidationItem[] Step2_AddValidationCases =
        {
            new("TestCase01_EmptyDates", "EP0006", "", "", "100", null, null, null, "Save", 26,
                "Validating: Empty Dates...", new[] { "วันที่" }),

            new("TestCase02_NoPromoteType", "EP0006", null, null, null, null, null, null, "Save", 30,
                "Validating: No Promote Type...", new[] { MsgNoPromoteType }),

            new("TestCase03_EmptyEmployee", "", "15/07/2570", "31/12/2570", "100", null, null, null, "Save and New", 34,
                "Validating: Empty Employee...", new[] { MsgEmployeeEmpty })
        };

        // [ขั้นที่ 3] ข้อมูลทดสอบการแก้ไขข้อมูล (Edit Records)
        private static readonly EditItem[] Step3_EditCases =
        {
            new(
                "TestCase07_EditEmp00001",
                "EMP00001",
                "100",
                null,
                null,
                "Save",
                true),
            new(
                "TestCase08_EditEmp00002",
                "EMP00002",
                null,
                "P-001",
                "RP001",
                "Save and Close",
                false)
        };

        // [ขั้นที่ 4] ข้อมูลทดสอบ Validation ตอนแก้ไข (Edit Validation)
        private const string EditTargetRow = "EMP00001";
        private static readonly ValidationItem[] Step4_EditValidationCases =
        {
            // ลบพนักงานออก + ติ๊กครบทุกประเภท → Save → แจ้งเตือนพนักงานว่าง
            new("TestCase09_EditValidation_EmptyEmployee", "", null, null, "100", "EL-001", "H-001", "001", "Save", 66,
                "Validating Edit: Empty Employee...", new[] { MsgEmployeeEmpty }),

            // เลือก EP0006 + ปลดติ๊กออกทั้งหมด → Save and New → แจ้งเตือนไม่ได้เลือกประเภทการปรับ
            new("TestCase09_EditValidation_NoPromoteType", "EP0006", null, null, null, null, null, null, "Save and New", 72,
                "Validating Edit: No Promote Type...", new[] { MsgNoPromoteType })
        };

        // [ขั้นที่ 5] ข้อมูลทดสอบการลบข้อมูล (Delete Records)
        private static readonly DeleteItem[] Step5_DeleteCases =
        {
            new("EP0005", "Yes", true, 92, "Delete Test: EP0005 (confirmed delete)..."),
            new("EMP00001", "No", false, 100, "Delete Test: EMP00001 (cancelled delete)...")
        };


        // =========================================================================================
        // หมวดที่ 2: ส่วนที่ขึ้นกับหน้าจอนั้นๆ (Screen-Specific Implementation)
        // **เมื่อเปลี่ยนไปทำหน้าจออื่น ต้องมาแก้ไขค่าและเมทอดในส่วนนี้ทั้งหมด**
        // =========================================================================================

        // 2.1 Locator & Mappings ประจำหน้าจอ
        private const string MenuName = "บันทึกปรับตำแหน่งและเงินเดือน";
        private const string ScreenLogName = "PromoteInfoUC";
        private const string GeneralTabName = "General";
        private const string DetailTabName = "รายละเอียดการปรับฯ";
        private const string FirstObjectID = "devBtnEmp"; // ช่องพนักงาน (ถูกเคลียร์หลัง Save and New)

        // 2.2 ฟิลด์ Input ประจำหน้าจอ
        private const string PromoteDateFieldId = "devDtPromoteDate";
        private const string EffectDateFieldId = "devDtEffectDate";
        private const string EmployeeFieldId = "devBtnEmp";
        private const string ChkSalaryFieldId = "devChkIsSalaryPromote";
        private const string AdjustAmountFieldId = "devTxtAdjustAmnt";
        private const string ChkLevelFieldId = "devChkIsEmpLevelPromote";
        private const string NewLevelFieldId = "devBtnNewLevelName";
        private const string ChkPositionFieldId = "devChkIsPositionPromote";
        private const string NewPositionFieldId = "devBtnPositionAfterPromote";
        private const string NewOrgFieldId = "devBtnEditOrgName";

        /// <summary>
        /// ขั้นที่ 1: เปิดหน้าจอและกรอกข้อมูลสร้างใหม่
        /// </summary>
        public void NavigateToPromoteInfoUC()
        {
            const string stageName = "TestCase00_AddPromoteData";
            TestLogger.LogInfo(ScreenLogName, $"เริ่มทดสอบหน้าจอ {MenuName}");
            TestProgressHelper.Report(2, $"Opening Personnel -> {MenuName} Menu via Search...");

            if (!TryOpenMenu(stageName, MenuName)) return;

            Window? form = null;
            for (int i = 0; i < Step1_AddCases.Length; i++)
            {
                var item = Step1_AddCases[i];
                string testName = $"{stageName}_{item.Code}";
                TestProgressHelper.Report(4 + (i * 4), $"Adding {MenuName} ({i + 1}/{Step1_AddCases.Length}): {item.Code}...");

                try
                {
                    form ??= GetOrOpenForm();
                    var tab = GetGeneralTab(form);
                    FillFormData(form, item);

                    if (!TrySaveAndHandleDuplicate(form, testName, item.Code, item.SaveBtn))
                    {
                        form = null;
                        continue;
                    }
                    // เฉพาะหน้าจอนี้: ผลการบันทึกบางกรณีแจ้งเป็นกล่องข้อความ ต้องกด OK และตรวจว่าเป็นข้อผิดพลาดหรือไม่
                    if (!HandleSaveMessageBox(form, testName, item.Code, item.SaveBtn))
                    {
                        form = null;
                        continue;
                    }

                    if (item.SaveBtn == "Save and New")
                    {
                        WaitUntil(() => string.IsNullOrEmpty(tab.FindFirstDescendant(cf => cf.ByAutomationId(FirstObjectID))?.AsTextBox().Text), 3000);
                        LogSuccess(testName, $"'{item.Code}' บันทึกสำเร็จ (Save and New)");
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

        /// <summary>
        /// ขั้นที่ 2: รัน Validation ตอนสร้างข้อมูลใหม่
        /// </summary>
        public void RunAllValidationTests()
        {
            const string stage = "TestCase01-06_Validation";
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
            const string dupName = "TestCase04-06";
            RunStep($"{dupName}_CloseSequence",
                () => RunCloseDialogSequence(form, dupName, MsgEmployeeEmpty),
                40, "Validating: Empty Employee Close Sequence...");
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
                    SetPromoteDetails(form, item.Amount, null, item.Position, item.Org);

                    if (!TrySaveAndHandleDuplicate(form, item.Name, item.Code, item.SaveBtn)) continue;
                    if (!HandleSaveMessageBox(form, item.Name, item.Code, item.SaveBtn)) continue; // เฉพาะหน้าจอนี้
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
            const string baseName = "TestCase09_EditValidation";
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

            const string dupSub = $"{baseName}_NoPromoteType";
            RunStep($"{dupSub}_CloseSequence",
                () => RunCloseDialogSequence(form, dupSub, MsgNoPromoteType),
                80, "Validating Edit: No Promote Type Close Sequence...");

            LogSuccess(baseName, "รัน Validation Edit ครบทุกเคสแล้ว");
        }

        /// <summary>
        /// ขั้นที่ 5: ทดสอบการลบรายการในตาราง
        /// </summary>
        public void RunDeleteTests()
        {
            const string baseName = "TestCase10_Delete";
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

        /* กรอกวันที่เสนอปรับ, วันที่มีผล, พนักงาน แล้วกรอกแท็บรายละเอียดการปรับฯ */
        private void FillFormData(Window form, AddItem item)
        {
            SelectTab(form, GeneralTabName);
            TypeText(form, PromoteDateFieldId, item.PromoteDate);
            TypeText(form, EffectDateFieldId, item.EffectDate);
            SelectLookup(form, EmployeeFieldId, item.Code);
            SetPromoteDetails(form, item.Amount, item.Level, item.Position, item.Org);
        }

        /* กรอกแท็บรายละเอียดการปรับฯ: ค่าที่ส่งเป็น null = ไม่ติ๊กประเภทนั้น และล้างช่องที่เกี่ยวข้อง */
        private void SetPromoteDetails(Window form, string? amount, string? level, string? position, string? org)
        {
            SelectTab(form, DetailTabName);

            SetCheckBox(form, ChkSalaryFieldId, amount != null);
            TypeText(form, AdjustAmountFieldId, amount ?? "");

            SetCheckBox(form, ChkLevelFieldId, level != null);
            SelectLookup(form, NewLevelFieldId, level ?? "");

            SetCheckBox(form, ChkPositionFieldId, position != null);
            SelectLookup(form, NewPositionFieldId, position ?? "");
            SelectLookup(form, NewOrgFieldId, position != null ? org ?? "" : "");
        }

        /* กรอกข้อมูลตามเคส แล้วกดบันทึกและตรวจข้อความ (code "" = ลบพนักงาน / วันที่ null = ไม่แตะ / ประเภทการปรับ null = ไม่ติ๊ก) */
        private void FillValidationAndAssert(AutomationElement tab, Window form, ValidationItem tc, string testName)
        {
            SelectTab(form, GeneralTabName);
            if (tc.PromoteDate != null) TypeText(form, PromoteDateFieldId, tc.PromoteDate);
            if (tc.EffectDate != null) TypeText(form, EffectDateFieldId, tc.EffectDate);
            SelectLookup(form, EmployeeFieldId, tc.Code);
            SetPromoteDetails(form, tc.Amount, tc.Level, tc.Position, tc.Org);

            form.FindFirstDescendant(cf => cf.ByName(tc.SaveBtn))?.Click();
            AssertScreenTaskResult(form, tc.ExpectedErrors, testName);
        }

        /* หน้านี้ไม่มีกรณีรหัสซ้ำ (เลขที่เอกสารระบบออกให้อัตโนมัติ) */
        private static string? DetectDuplicateMessage(IEnumerable<string> texts) => null;

        /* หน้านี้ไม่มี AutomationId ของแท็บ จึงคลิกแท็บ General แล้วใช้ตัวฟอร์มเป็นขอบเขตค้นหา */
        private AutomationElement GetGeneralTab(Window form)
        {
            try { SelectTab(form, GeneralTabName); } catch { }
            return form;
        }

        /* ฟอร์มหน้านี้ไม่มี AutomationId ประจำ จึงหาจากชื่อหน้าต่าง */
        private Window? FindOpenForm() =>
            _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault() ??
            _automation.GetDesktop().FindAllChildren(cf => cf.ByControlType(ControlType.Window))
                .FirstOrDefault(w => (w.Name ?? "").Contains(MenuName))?.AsWindow();

        // --- การกรอกข้อมูลเฉพาะหน้าจอนี้ (ช่อง DevExpress ต้องพิมพ์ด้วยคีย์บอร์ดจริง) ---

        /* พิมพ์ข้อความด้วยคีย์บอร์ด (ค่าว่าง = ลบข้อความ) แล้ว Tab ออกเพื่อ commit */
        private void TypeText(AutomationElement scope, string id, string text)
        {
            var field = Retry(() => scope.FindFirstDescendant(cf => cf.ByAutomationId(id)), 3000) ?? throw new Exception($"ไม่พบช่อง {id}");

            ClickWithMouse(field);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            Keyboard.Type(VirtualKeyShort.BACK);
            if (text.Length > 0) Keyboard.Type(text);

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
            var tabItem = Retry(() => form.FindFirstDescendant(cf => cf.ByName(tabName))
                                      ?? form.FindFirstDescendant(cf => cf.ByAutomationId(tabName)), 3000)
                          ?? throw new Exception($"หา Tab '{tabName}' ไม่เจอ");
            ClickWithMouse(tabItem);
            WaitUntil(() => !tabItem.Patterns.SelectionItem.IsSupported || tabItem.Patterns.SelectionItem.Pattern.IsSelected.Value, 3000);
        }

        /* ติ๊ก / ปลดติ๊ก CheckBox ให้ตรงกับสถานะที่ต้องการ แล้วรอจนสถานะเปลี่ยนจริง */
        private void SetCheckBox(AutomationElement scope, string id, bool isChecked)
        {
            var chk = Retry(() => scope.FindFirstDescendant(cf => cf.ByAutomationId(id))?.AsCheckBox(), 3000)
                      ?? throw new Exception($"ไม่พบ CheckBox {id}");
            if (chk.Patterns.Toggle.IsSupported && chk.IsChecked == isChecked) return;

            ClickWithMouse(chk);
            if (chk.Patterns.Toggle.IsSupported) WaitUntil(() => chk.IsChecked == isChecked, 2000);
        }

        /* เลือกข้อมูลจาก Pop-up Lookup: กด ... → รอ Pop-up → คลิกแถวที่รหัสตรง → กด Select → รอ Pop-up ปิด (ค่าว่าง = ลบค่าในช่อง) */
        private void SelectLookup(Window form, string id, string code)
        {
            var control = Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(id)), 3000) ?? throw new Exception($"ไม่พบช่อง {id}");
            var buttons = control.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));

            if (code.Length == 0)
            {
                // ปุ่มที่ 2 ข้างช่องคือปุ่มล้างค่า ถ้าไม่มีให้ Ctrl+A → Delete
                if (buttons.Length > 1) ClickWithMouse(buttons[1]);
                else
                {
                    ClickWithMouse(control);
                    Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                    Keyboard.Type(VirtualKeyShort.DELETE);
                }
                WaitUntil(() => GetValue(form, id).Length == 0, 1500);
                return;
            }

            if (buttons.Length > 0) ClickWithMouse(buttons[0]);
            else { ClickWithMouse(control); Keyboard.Type(VirtualKeyShort.F4); }

            var popup = Retry(() => FindLookupPopup(form), 4000);
            if (popup == null)
            {
                // กด ... แล้วไม่เปิด → ลองเปิดด้วย F4 อีกครั้ง
                ClickWithMouse(control);
                Keyboard.Type(VirtualKeyShort.F4);
                popup = Retry(() => FindLookupPopup(form), 4000) ?? throw new Exception($"ไม่พบหน้าต่าง Lookup สำหรับ '{id}'");
            }

            var cell = Retry(() => popup.FindAllDescendants().FirstOrDefault(el =>
                           string.Equals(el.Name?.Trim(), code, StringComparison.OrdinalIgnoreCase) ||
                           (el.Patterns.Value.IsSupported && string.Equals(el.Patterns.Value.Pattern.Value.Value?.Trim(), code, StringComparison.OrdinalIgnoreCase))), 3000)
                       ?? popup.FindAllDescendants().FirstOrDefault(el => (el.Name ?? "").Contains(code))
                       ?? throw new Exception($"ไม่พบ '{code}' ใน Pop-up ของช่อง {id}");
            ClickWithMouse(cell);

            var btnSelect = popup.FindFirstDescendant(cf => cf.ByName("Select"))
                            ?? popup.FindFirstDescendant(cf => cf.ByName("&Select"))
                            ?? popup.FindFirstDescendant(cf => cf.ByName("OK"))
                            ?? popup.FindFirstDescendant(cf => cf.ByName("เลือก"));
            if (btnSelect != null) ClickWithMouse(btnSelect);
            else Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.KEY_S);

            if (!WaitForClosed(() => popup)) throw new Exception($"กด Select แล้ว Pop-up ของช่อง {id} ไม่ปิด");
        }

        /* หา Pop-up Lookup ที่เปิดอยู่ (Modal ของฟอร์ม / หน้าหลัก หรือหน้าต่างบน Desktop ที่มีปุ่ม Select) */
        private Window? FindLookupPopup(Window form) =>
            FindMessageBox(form)
            ?? _automation.GetDesktop().FindAllChildren(cf => cf.ByControlType(ControlType.Window))
                   .FirstOrDefault(w => w.FindFirstDescendant(cf => cf.ByName("Select")) != null)?.AsWindow();

        /* หากล่องข้อความที่เด้งจากฟอร์ม (เป็น Modal ของฟอร์ม หรือ Modal ของหน้าหลักที่ไม่ใช่ตัวฟอร์มเอง) */
        private Window? FindMessageBox(Window form)
        {
            try
            {
                return (form.IsAvailable ? form.ModalWindows.FirstOrDefault() : null)
                       ?? _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault(w => !w.Equals(form));
            }
            catch { return null; }
        }

        /* หลังกดบันทึก: รอจนหน้าจอตอบสนอง แล้วถ้ามีกล่องข้อความให้กด OK
           - กล่องที่เป็นข้อผิดพลาด = บันทึกไม่ผ่าน (ปิดฟอร์มด้วย No แล้วคืน false) / กล่องแจ้งข้อมูลทั่วไป = ไปต่อ */
        private bool HandleSaveMessageBox(Window form, string testName, string code, string saveBtn)
        {
            WaitUntil(() => !form.IsAvailable || form.Properties.IsOffscreen.ValueOrDefault
                            || (saveBtn == "Save and New" && string.IsNullOrEmpty(GetValue(form, EmployeeFieldId)))
                            || FindMessageBox(form) != null, 8000);

            var box = FindMessageBox(form);
            if (box == null) return true;

            string boxText = string.Join(" ", GetPanelTexts(box)).Trim();
            try { ClickDialogButton(box, "OK"); } catch { Keyboard.Type(VirtualKeyShort.ENTER); }
            WaitForClosed(() => box);

            if (boxText.Contains("กรุณา") || boxText.Contains("ไม่สามารถ") || boxText.Contains("ผิดพลาด"))
            {
                LogFailure(testName, $"'{code}' บันทึกไม่สำเร็จ: {boxText}");
                try { ClickCloseAndHandleDialog(form, "No", $"{testName}_Close"); } catch { }
                return false;
            }
            return true;
        }

        /* ตรวจข้อความแบบหน้าจอนี้: อ่านทั้ง Task Result และกล่องข้อความ (เทียบแบบไม่สนช่องว่าง) แล้วกด OK ปิดกล่องข้อความ */
        private void AssertScreenTaskResult(Window form, string[] msgs, string testName)
        {
            var pending = new HashSet<string>(msgs);
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < 5000 && pending.Count > 0)
            {
                var texts = new List<string>();
                var panel = FindTaskResultPanel(form, 500);
                if (panel != null) texts.AddRange(GetPanelTexts(panel));
                var box = FindMessageBox(form);
                if (box != null) texts.AddRange(GetPanelTexts(box));

                foreach (var m in pending.ToList())
                {
                    if (texts.Any(t => Squash(t).Contains(Squash(m)))) pending.Remove(m);
                }
            }

            // ปิดกล่องข้อความทุกกรณี กันบังฟอร์มให้เคสถัดไปพังตาม
            var leftover = FindMessageBox(form);
            if (leftover != null)
            {
                try { ClickDialogButton(leftover, "OK"); } catch { Keyboard.Type(VirtualKeyShort.ENTER); }
                WaitForClosed(() => leftover);
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

        public PromoteInfoUC(AutomationElement mainWindow, UIA3Automation automation)
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