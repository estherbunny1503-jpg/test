using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using HRMITestFlaUI.Base;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace HRMITestFlaUI.Module.Organization
{
    public class OrganizationUnitInfoUC
    {
        // =========================================================================================
        // หมวดที่ 1: ข้อมูลการทดสอบครบทั้ง 5 ขั้น (Test Data: Step 1 - Step 5)
        // =========================================================================================

        private sealed record OrgUnitAddItem(
            bool ClickNew,
            string Code,
            string Name,
            string NameEng,
            string OrgUnitType,
            string ParentOrgUnit,
            string Description,
            string Save);

        private sealed record ValidationItem(
            string TestCaseId,
            int Progress,
            string ProgressMsg,
            Action<OrganizationUnitInfoUC, AutomationElement, Window> Action,
            string[] ExpectedMessages);

        private sealed record OrgUnitEditItem(
            string TestCaseId,
            string OldCode,
            string NewCode,
            string NewName,
            string SaveButtonName);

        private sealed record DeleteItem(
            string TargetCode,
            string ConfirmButton,
            bool ExpectDeleted);

        // [ขั้นที่ 1] ข้อมูลทดสอบการสร้างใหม่ (Add / Navigate)
        private static readonly OrgUnitAddItem[] Step1_AddCases =
        {
            new(true,  "ORG001", "สำนักงานใหญ่",           "Head Office",            "001", "",    "หน่วยงานหลัก",          "SaveClose"),
            new(true,  "ORG002", "ฝ่ายทรัพยากรบุคคล",       "Human Resources",        "002", "001", "ดูแลบุคลากร",            "SaveNew"),
            new(false, "ORG003", "ฝ่ายบัญชีและการเงิน",     "Accounting & Finance",   "001", "",    "ดูแลบัญชีและการเงิน",      "SaveNew"),
            new(false, "ORG004", "ฝ่ายเทคโนโลยีสารสนเทศ",   "Information Technology", "002", "001", "ดูแลระบบ IT",           "SaveNew"),
            new(false, "ORG005", "ฝ่ายจัดซื้อ",             "Procurement",            "001", "",    "จัดซื้อวัสดุและอุปกรณ์",  "SaveAndClose")
        };

        // [ขั้นที่ 2] ข้อมูลทดสอบแจ้งเตือนตอนสร้างใหม่ (New Validation)
        private static readonly ValidationItem[] Step2_NewValidationCases =
        {
            new("TestCase01_MissingCode", 26, "Validating: Missing Code...",
                (page, ctx, form) =>
                {
                    page.ClearField(ctx, IdTxtOrgUnitCode);
                    page.FillTextBox(ctx, IdTxtOrgUnitName, "สำนักงานใหญ่");
                    page.SelectLookupValue(ctx, IdBtnOrgUnitType, "001", "ประเภทหน่วยงาน");
                    page.SafeClick(form, "Save");
                },
                new[] { "รหัสหน่วยงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล" }),

            new("TestCase02_MissingName", 29, "Validating: Missing Name...",
                (page, ctx, form) =>
                {
                    page.FillTextBox(ctx, IdTxtOrgUnitCode, "ORG001");
                    page.ClearField(ctx, IdTxtOrgUnitName);
                    page.SelectLookupValue(ctx, IdBtnOrgUnitType, "002", "ประเภทหน่วยงาน");
                    page.SelectLookupValue(ctx, IdBtnParentOrgUnit, "001", "หน่วยงานหลัก");
                    page.SafeClick(form, "Save and New");
                },
                new[] { "ชื่อหน่วยงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล" }),

            new("TestCase03_MissingAllFields", 32, "Validating: Missing All Fields...",
                (page, ctx, form) =>
                {
                    page.ClearField(ctx, IdTxtOrgUnitCode);
                    page.ClearField(ctx, IdTxtOrgUnitName);
                    page.ClearLookupValue(ctx, IdBtnOrgUnitType);
                    page.SafeClick(form, "Save and New");
                },
                new[] {
                    "รหัสหน่วยงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล",
                    "ชื่อหน่วยงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล",
                    "ประเภทหน่วยงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล"
                }),

            new("TestCase04_MissingParentOrg", 35, "Validating: Missing Parent Org...",
                (page, ctx, form) =>
                {
                    page.FillTextBox(ctx, IdTxtOrgUnitCode, "ORG006");
                    page.FillTextBox(ctx, IdTxtOrgUnitName, "สำนักงาน");
                    page.SelectLookupValue(ctx, IdBtnOrgUnitType, "002", "ประเภทหน่วยงาน");
                    page.ClearLookupValue(ctx, IdBtnParentOrgUnit);
                    page.SafeClick(form, "Save and New");
                },
                new[] { "หน่วยงานหลัก ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล" }),

            new("TestCase05_DuplicateCodeAndName", 38, "Validating: Duplicate Code and Name...",
                (page, ctx, form) =>
                {
                    page.FillTextBox(ctx, IdTxtOrgUnitCode, "ORG005");
                    page.FillTextBox(ctx, IdTxtOrgUnitName, "ฝ่ายจัดซื้อ");
                    page.SelectLookupValue(ctx, IdBtnOrgUnitType, "001", "ประเภทหน่วยงาน");
                    page.SafeClick(form, "Save and New");
                },
                new[] { "ห้ามซ้ำ กรุณาป้อนข้อมูล" })
        };

        // [ขั้นที่ 3] ข้อมูลทดสอบการแก้ไขข้อมูล (Edit Records)
        private static readonly OrgUnitEditItem[] Step3_EditCases =
        {
            new("TestCase03_EditData_ORG001_to_RP001", "ORG001", "RP001", "สำนักงานใหญ่มาก", "Save"),
            new("TestCase03_EditData_ORG002_to_RP002", "ORG002", "RP002", "ฝ่ายทรัพยากรบุคคลพิเศษ", "Save and Close")
        };

        // [ขั้นที่ 4] ข้อมูลทดสอบแจ้งเตือนตอนแก้ไขข้อมูล (Edit Validation)
        private static readonly ValidationItem[] Step4_EditValidationCases =
        {
            new("TestCase04_EditValidation_MissingCode", 64, "Validating Edit: Missing Code...",
                (page, ctx, form) =>
                {
                    form.Focus();
                    page.ClearField(ctx, IdTxtOrgUnitCode);
                    page.FillTextBox(ctx, IdTxtOrgUnitName, "สำนักงานย่อย");
                    page.SafeClick(form, "Save");
                },
                new[] { "รหัสหน่วยงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล" }),

            new("TestCase04_EditValidation_MissingName", 68, "Validating Edit: Missing Name...",
                (page, ctx, form) =>
                {
                    form.Focus();
                    page.FillTextBox(ctx, IdTxtOrgUnitCode, "ORG003");
                    page.ClearField(ctx, IdTxtOrgUnitName);
                    page.SafeClick(form, "Save");
                },
                new[] { "ชื่อหน่วยงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล" }),

            new("TestCase04_EditValidation_MissingBoth", 72, "Validating Edit: Missing Both...",
                (page, ctx, form) =>
                {
                    form.Focus();
                    page.ClearField(ctx, IdTxtOrgUnitCode);
                    page.ClearField(ctx, IdTxtOrgUnitName);
                    page.SafeClick(form, "Save");
                },
                new[] {
                    "รหัสหน่วยงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล",
                    "ชื่อหน่วยงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล"
                }),

            new("TestCase04_EditValidation_Duplicate", 76, "Validating Edit: Duplicate...",
                (page, ctx, form) =>
                {
                    form.Focus();
                    page.FillTextBox(ctx, IdTxtOrgUnitCode, "ORG003");
                    page.FillTextBox(ctx, IdTxtOrgUnitName, "ฝ่ายบัญชีและการเงิน");
                    page.SafeClick(form, "Save");
                },
                new[] { "ห้ามซ้ำ กรุณาป้อนข้อมูล" })
        };

        // [ขั้นที่ 5] ข้อมูลทดสอบการลบข้อมูล (Delete Records)
        private static readonly DeleteItem[] Step5_DeleteCases =
        {
            new("RP002", "Yes", true),
            new("RP001", "No", false)
        };


        // =========================================================================================
        // หมวดที่ 2: ส่วนที่ขึ้นกับหน้าจอนั้นๆ (Screen-Specific Implementation)
        // =========================================================================================

        private const string FormId = "OrganizationUnitInfoUC";
        private const string LogPageKey = "OrganizationUnitInfoUC";
        private const string GeneralTabId = "devTabPgGeneral";
        private const string MenuSearchKeyword = "บันทึกข้อมูลหน่วยงาน";

        private const string IdTxtOrgUnitCode = "devTxtOrgUnitCode";
        private const string IdTxtOrgUnitName = "devTxtOrgUnitName";
        private const string IdTxtOrgUnitNameEng = "devTxtOrgUnitNameEng";
        private const string IdBtnOrgUnitType = "devBtnOrgUnitType";
        private const string IdBtnParentOrgUnit = "devBtnParentOrgUnit";
        private const string IdMemoDescription = "devMemoDescription";

        public void NavigateToOrganizationUnitInfoUC()
        {
            const string stageName = "TestCase00_AddOrgUnitData";
            TestLogger.LogInfo(LogPageKey, "เริ่มทดสอบหน้าจอบันทึกข้อมูลหน่วยงาน");
            TestProgressHelper.Report(2, "Opening Organization -> Org Unit Menu via Search...");

            if (!TryOpenOrgUnitMenu(stageName)) return;

            Window? form = null;
            for (int i = 0; i < Step1_AddCases.Length; i++)
            {
                var data = Step1_AddCases[i];
                string testName = $"{stageName}_{data.Code}";
                TestProgressHelper.Report(4 + (i * 3), $"Adding Org Unit ({i + 1}/{Step1_AddCases.Length}): {data.Code} - {data.Name}...");

                try
                {
                    if (data.ClickNew || form == null || !form.IsAvailable)
                    {
                        form = OpenForm();
                    }

                    FillGeneralTab(form, data.Code, data.Name, data.NameEng, data.OrgUnitType, data.ParentOrgUnit, data.Description);

                    switch (data.Save)
                    {
                        case "SaveClose":
                            if (!TrySaveAndHandleDuplicate(form, testName, data.Code, "Save"))
                            {
                                form = null;
                                continue;
                            }
                            SafeCloseForm(form);
                            form = null;
                            LogSuccess(testName, $"'{data.Code}' บันทึก SaveClose สำเร็จ");
                            break;

                        case "SaveNew":
                            if (!TrySaveAndHandleDuplicate(form, testName, data.Code, "Save and New"))
                            {
                                form = null;
                                continue;
                            }
                            var codeInput = form.FindFirstDescendant(cf => cf.ByAutomationId(IdTxtOrgUnitCode))?.AsTextBox();
                            WaitUntil(() => string.IsNullOrEmpty(codeInput?.Text), 3000);
                            LogSuccess(testName, $"'{data.Code}' บันทึก Save and New สำเร็จ");
                            break;

                        case "SaveAndClose":
                            string saveBtn = SafeClick(form, "Save and Close") ? "Save and Close" : "Save";
                            if (saveBtn == "Save")
                            {
                                if (!TrySaveAndHandleDuplicate(form, testName, data.Code, "Save"))
                                {
                                    form = null;
                                    continue;
                                }
                                SafeCloseForm(form);
                            }
                            else
                            {
                                if (!CheckDuplicateWarningAfterSave(form, testName, data.Code))
                                {
                                    form = null;
                                    continue;
                                }
                                WaitForClosed(() => FindOpenForm());
                            }
                            form = null;
                            LogSuccess(testName, $"'{data.Code}' บันทึก SaveAndClose สำเร็จ");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    LogFailure(testName, $"'{data.Code}' เพิ่มไม่สำเร็จ: {ex.Message}");
                    if (form != null && form.IsAvailable) SafeCloseForm(form);
                    form = null;
                }
            }

            EnsureNoStrayModal($"{stageName}_Cleanup");
            RefreshGrid();

            foreach (var data in Step1_AddCases)
            {
                LogGridResult($"{stageName}_Verify_{data.Code}", data.Code);
            }
        }

        public void RunAllValidationTests()
        {
            const string stageName = "TestCase01-06_ValidationNew";
            TestProgressHelper.Report(22, "Opening Org Unit Form for Validation Tests...");
            EnsureNoStrayModal($"{stageName}_PreCheck");

            Window form;
            try { form = OpenForm(); }
            catch (Exception ex) { LogFailure(stageName, $"เปิดฟอร์มไม่สำเร็จ: {ex.Message}"); return; }

            var context = GetGeneralTab(form);

            foreach (var item in Step2_NewValidationCases)
            {
                RunStep(item.TestCaseId, () =>
                {
                    item.Action(this, context, form);
                    AssertTaskResult(form, item.ExpectedMessages, item.TestCaseId);
                    AssertFormOpen(form, item.TestCaseId);
                }, item.Progress, item.ProgressMsg);
            }

            RunStep("TestCase05_DuplicateCodeAndName_CloseSequence", () =>
                RunCloseDialogSequence(form, "TestCase05_DuplicateCodeAndName", "ห้ามซ้ำ กรุณาป้อนข้อมูล"),
                40, "Validating: Close Dialog Sequence...");
        }

        public void RunEditTests()
        {
            const string stageName = "TestCase03_EditData";
            TestProgressHelper.Report(42, "Starting Edit Operations...");
            EnsureNoStrayModal($"{stageName}_PreCheck");

            foreach (var edit in Step3_EditCases)
            {
                PerformEdit(edit.TestCaseId, edit.OldCode, edit.NewCode, edit.NewName, edit.SaveButtonName);
            }
        }

        public void RunEditValidationTests()
        {
            const string baseTestName = "TestCase04_EditValidation";
            TestProgressHelper.Report(58, "Opening Edit Form for Validation Tests...");
            EnsureNoStrayModal($"{baseTestName}_PreCheck");

            Window form;
            try { form = ClickRowAndOpenEdit("RP001"); }
            catch (Exception ex) { LogFailure(baseTestName, $"เปิดฟอร์ม Edit ไม่สำเร็จ: {ex.Message}"); return; }

            var context = GetGeneralTab(form);

            foreach (var item in Step4_EditValidationCases)
            {
                RunStep(item.TestCaseId, () =>
                {
                    item.Action(this, context, form);
                    AssertTaskResult(form, item.ExpectedMessages, item.TestCaseId);
                }, item.Progress, item.ProgressMsg);
            }

            RunStep("TestCase04_EditValidation_Duplicate_CloseSequence", () =>
                RunCloseDialogSequence(form, "TestCase04_EditValidation_Duplicate", "ห้ามซ้ำ กรุณาป้อนข้อมูล"),
                80, "Validating Edit: Close Dialog Sequence...");
        }

        public void RunDeleteTests()
        {
            const string baseTestName = "TestCase05_DeleteData";
            TestProgressHelper.Report(84, "Starting Delete Tests...");

            foreach (var item in Step5_DeleteCases)
            {
                RunDeleteCase(baseTestName, item.TargetCode, item.ConfirmButton, item.ExpectDeleted);
            }

            LogSuccess(baseTestName, "ทดสอบ Delete ครบทุกเคสแล้ว");
            TestLogger.LogInfo(LogPageKey, $"สรุปผลรวมทั้งหมด: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");
        }

        // --- Custom Mapping & Control Logic ประจำหน้าจอ ---
        private AutomationElement GetGeneralTab(Window form) =>
            form.FindFirstDescendant(cf => cf.ByAutomationId(GeneralTabId)) ?? form;

        private void FillGeneralTab(Window form, string code, string name, string nameEng, string orgUnitType = "", string parentOrgUnit = "", string description = "")
        {
            SwitchToTab(form, GeneralTabId, "General");
            var ctx = form.FindFirstDescendant(cf => cf.ByAutomationId(GeneralTabId)) ?? form;

            if (!string.IsNullOrEmpty(code)) FillTextBox(ctx, IdTxtOrgUnitCode, code);
            if (!string.IsNullOrEmpty(name)) FillTextBox(ctx, IdTxtOrgUnitName, name);
            if (!string.IsNullOrEmpty(nameEng)) FillTextBox(ctx, IdTxtOrgUnitNameEng, nameEng);

            if (!string.IsNullOrEmpty(orgUnitType))
                SelectLookupValue(ctx, IdBtnOrgUnitType, orgUnitType, "ประเภทหน่วยงาน");

            if (!string.IsNullOrEmpty(parentOrgUnit))
                SelectLookupValue(ctx, IdBtnParentOrgUnit, parentOrgUnit, "หน่วยงานหลัก");

            if (!string.IsNullOrEmpty(description)) FillTextBox(ctx, IdMemoDescription, description);
        }

        private bool TryOpenOrgUnitMenu(string stage)
        {
            try
            {
                var searchBox = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("baritemSearch"))
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)), 4000)
                              ?? throw new Exception("ไม่พบช่องค้นหา baritemSearch");

                searchBox.Focus();
                Thread.Sleep(200);

                string searchKeyword = MenuSearchKeyword;
                if (searchBox.Patterns.Value.IsSupported)
                    searchBox.Patterns.Value.Pattern.SetValue(searchKeyword);
                else
                    Keyboard.Type(searchKeyword);

                Thread.Sleep(300);

                var searchBtn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("barChkSearch"))
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("Find")), 4000)
                              ?? throw new Exception("ไม่พบปุ่มค้นหา barChkSearch");

                searchBtn.Click();

                var menuResult = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName(searchKeyword))
                                               ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("treeListColumn row 1"))
                                               ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("treeListColumn row 2")), 4000)
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

        private Window OpenForm()
        {
            SafeClickMainWindow("New");
            return GetOrgUnitFormWindow();
        }

        private Window? FindOpenForm() =>
            _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault() ??
            _automation.GetDesktop().FindFirstChild(cf => cf.ByAutomationId(FormId))?.AsWindow();

        private Window GetOrgUnitFormWindow()
        {
            var form = Retry(FindOpenForm, 8000) ?? throw new Exception("หาฟอร์ม OrganizationUnitInfoUC ไม่เจอ");
            WaitUntil(() =>
            {
                var txt = form.FindFirstDescendant(cf => cf.ByAutomationId(IdTxtOrgUnitCode));
                return txt != null && !txt.Properties.IsOffscreen.ValueOrDefault;
            }, 3000);
            return form;
        }

        private Window ClickRowAndOpenEdit(string code)
        {
            var row = Retry(() => FindGridRowByCodeInContainer(_mainWindow, code), 4000)
                      ?? throw new Exception($"หาแถว '{code}' ไม่เจอ");

            row.Click();

            var btn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("Edit")), 3000)
                      ?? throw new Exception("ไม่พบปุ่ม Edit");
            btn.Click();

            var form = GetOrgUnitFormWindow();
            WaitUntil(() =>
            {
                var txt = form.FindFirstDescendant(cf => cf.ByAutomationId(IdTxtOrgUnitCode))?.AsTextBox();
                return txt != null && !string.IsNullOrEmpty(txt.Text);
            }, 3000);

            return form;
        }

        private void PerformEdit(string testName, string existingCode, string newCode, string newName, string saveButtonName)
        {
            try
            {
                Window form = ClickRowAndOpenEdit(existingCode);
                var context = GetGeneralTab(form);

                FillTextBox(context, IdTxtOrgUnitCode, newCode);
                FillTextBox(context, IdTxtOrgUnitName, newName);

                if (saveButtonName == "Save and Close" && SafeClick(form, "Save and Close"))
                {
                    if (!CheckDuplicateWarningAfterSave(form, testName, newCode)) return;
                    SafeCloseForm(form);
                }
                else
                {
                    if (!TrySaveAndHandleDuplicate(form, testName, newCode, "Save")) return;
                    SafeCloseForm(form);
                }

                WaitForClosed(() => FindOpenForm());
                RefreshGrid();

                if (VerifyRecordInGrid(newCode, refresh: false))
                    LogSuccess(testName, $"แก้ไข '{existingCode}' เป็น '{newCode}' สำเร็จ");
                else
                    LogFailure(testName, $"ไม่พบ '{newCode}' ในตารางหลักหลังแก้ไข");
            }
            catch (Exception ex)
            {
                LogFailure(testName, $"แก้ไข '{existingCode}' ไม่สำเร็จ: {ex.Message}");
            }
        }

        private void RunDeleteCase(string baseTestName, string targetCode, string confirmButton, bool expectDeleted)
        {
            string testName = $"{baseTestName}_{targetCode}_{confirmButton}";
            try
            {
                RefreshGrid();

                var row = Retry(() => FindGridRowByCodeInContainer(_mainWindow, targetCode), 4000)
                          ?? throw new Exception($"หาแถวรหัส '{targetCode}' ในตารางหลักไม่เจอ");

                row.Click();

                var deleteBtn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("Delete")), 3000)
                                ?? throw new Exception("หาปุ่ม Delete ไม่เจอ");
                deleteBtn.Click();

                var dialog = Retry(() => _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault()
                                          ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("คำเตือน"))?.AsWindow(), 4000)
                             ?? throw new Exception("กด Delete แล้วไม่พบ Pop-up ยืนยัน");

                ClickDialogButton(dialog, confirmButton);
                WaitForClosed(() => dialog);

                RefreshGrid();
                bool stillExists = VerifyRecordInGrid(targetCode, timeoutMs: 3000, refresh: false);

                if (expectDeleted && stillExists) throw new Exception($"'{targetCode}' ยังค้างอยู่หลังลบ");
                if (!expectDeleted && !stillExists) throw new Exception($"'{targetCode}' หายไปทั้งที่ไม่ควรถูกลบ");

                LogSuccess(testName, expectDeleted ? $"ข้อมูล '{targetCode}' ถูกลบสำเร็จ" : $"ข้อมูล '{targetCode}' ยังอยู่ตามคาด");
            }
            catch (Exception ex)
            {
                LogFailure(testName, $"ลบรหัส '{targetCode}' ไม่สำเร็จ: {ex.Message}");
            }
        }

        private void RunCloseDialogSequence(Window form, string testNamePrefix, string expectedWarningMessage)
        {
            string subCase = $"{testNamePrefix}_Close_Yes";
            ClickCloseAndHandleDialog(form, "Yes");
            AssertTaskResult(form, new[] { expectedWarningMessage }, subCase, $"พบข้อความ: '{expectedWarningMessage}' และ ฟอร์มถูกเปิดอยู่");
            CheckFormOpenOnly(form);

            subCase = $"{testNamePrefix}_Close_Cancel";
            ClickCloseAndHandleDialog(form, "Cancel");
            AssertFormOpen(form, subCase);

            subCase = $"{testNamePrefix}_Close_No";
            ClickCloseAndHandleDialog(form, "No");
            AssertFormClosed(form, subCase);
        }

        private bool TrySaveAndHandleDuplicate(Window form, string testName, string code, string saveBtn)
        {
            SafeClick(form, saveBtn);

            if (testName.Contains("Validation")) return true;

            var panel = FindTaskResultPanel(form, 2000);
            if (panel == null) return true;

            string? dup = DetectDuplicateMessage(GetPanelTexts(panel));
            if (dup == null) return true;

            LogFailure(testName, $"'{code}' ข้อมูลซ้ำ: {dup}");
            try { ClickCloseAndHandleDialog(form, "No"); } catch { }
            return false;
        }

        private bool CheckDuplicateWarningAfterSave(Window form, string testName, string code)
        {
            var panel = FindTaskResultPanel(form, 2000);
            if (panel == null) return true;

            string? dup = DetectDuplicateMessage(GetPanelTexts(panel));
            if (dup == null) return true;

            LogFailure(testName, $"'{code}' ข้อมูลซ้ำ: {dup}");
            try { ClickCloseAndHandleDialog(form, "No"); } catch { }
            return false;
        }

        private static string? DetectDuplicateMessage(IEnumerable<string> texts)
        {
            if (texts.Any(t => t.Contains("รหัสหน่วยงาน ห้ามซ้ำ") || t.Contains("ห้ามซ้ำ"))) return "รหัสหรือข้อมูลซ้ำ";
            return null;
        }

        private void AssertTaskResult(Window form, string[] msgs, string testName, string? customMsg = null)
        {
            var pending = new HashSet<string>(msgs);
            var lastSeenTexts = new List<string>();
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < 5000 && pending.Count > 0)
            {
                var panel = FindTaskResultPanel(form, 500);
                if (panel != null)
                {
                    var texts = GetPanelTexts(panel).ToList();
                    lastSeenTexts = texts;
                    foreach (var m in pending.ToList())
                    {
                        string cleanM = m.Replace(" ", "");
                        if (texts.Any(t => t.Replace(" ", "").Contains(cleanM)))
                            pending.Remove(m);
                    }
                }
                if (pending.Count == 0) break;
                Thread.Sleep(100);
            }

            if (pending.Count > 0)
            {
                string actualDump = lastSeenTexts.Count > 0 ? string.Join(" | ", lastSeenTexts.Distinct()) : "(ไม่มีข้อความ)";
                throw new Exception($"ไม่พบข้อความ: '{string.Join("', '", pending)}' | ข้อความจริง: [{actualDump}]");
            }

            string logMsg = customMsg ?? $"พบข้อความ: {string.Join(",", msgs.Select(m => $"'{m}'"))}";
            LogSuccess(testName, logMsg);
        }

        private AutomationElement? FindTaskResultPanel(Window form, int timeoutMs) =>
            Retry(() => form.FindFirstDescendant(cf => cf.ByName("Task Result"))
                        ?? form.FindFirstDescendant(cf => cf.ByAutomationId("dockPanelTaskResult"))
                        ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("Task Result"))
                        ?? _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("dockPanelTaskResult")), timeoutMs);

        private static IEnumerable<string> GetPanelTexts(AutomationElement panel)
        {
            foreach (var el in panel.FindAllDescendants())
            {
                if (!string.IsNullOrEmpty(el.Name)) yield return el.Name;
                if (el.Patterns.Value.IsSupported && !string.IsNullOrEmpty(el.Patterns.Value.Pattern.Value.Value))
                    yield return el.Patterns.Value.Pattern.Value.Value;
            }
        }

        private void SelectLookupValue(AutomationElement context, string buttonAutomationId, string targetCode, string fieldLabel)
        {
            if (string.IsNullOrEmpty(targetCode)) return;

            var targetControl = context.FindFirstDescendant(cf => cf.ByAutomationId(buttonAutomationId))
                ?? context.FindFirstDescendant(cf => cf.ByName(fieldLabel))
                ?? throw new Exception($"หาฟิลด์ '{fieldLabel}' ไม่เจอ");

            targetControl.Focus();

            var innerBtn = targetControl.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button));
            if (innerBtn != null)
            {
                innerBtn.Click();
            }
            else
            {
                targetControl.Click();
                Keyboard.Type(VirtualKeyShort.F4);
            }

            if (!WaitUntil(IsLookupDialogOpen, 2000))
            {
                targetControl.Click();
                Keyboard.Type(VirtualKeyShort.F4);
            }

            var lookupDialog = Retry(() =>
            {
                var modal = _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault();
                if (modal != null) return modal;

                return _automation.GetDesktop().FindAllChildren(cf => cf.ByControlType(ControlType.Window))
                    .FirstOrDefault(w => !string.IsNullOrEmpty(w.Name) &&
                                         (w.Name.Contains("ข้อมูลประเภทหน่วยงาน") ||
                                          w.Name.Contains("ข้อมูลหน่วยงาน") ||
                                          w.Name.Contains("ข้อมูล")))?.AsWindow();
            }, timeoutMs: 4000) ?? throw new Exception($"ไม่พบ Pop-up Dialog ของ '{fieldLabel}'");

            lookupDialog.Focus();

            var row = FindGridRowByCodeInContainer(lookupDialog, targetCode);
            if (row != null)
            {
                row.Click();

                var selectButton = Retry(() =>
                    lookupDialog.FindFirstDescendant(cf => cf.ByName("button 'Select'"))
                    ?? lookupDialog.FindFirstDescendant(cf => cf.ByName("Select"))
                    ?? lookupDialog.FindFirstDescendant(cf => cf.ByName("button 'ตกลง'"))
                    ?? lookupDialog.FindFirstDescendant(cf => cf.ByName("ตกลง"))
                    ?? lookupDialog.FindFirstDescendant(cf => cf.ByAutomationId("btnSelect")), timeoutMs: 2000);

                if (selectButton != null)
                {
                    selectButton.Click();
                }
                else
                {
                    row.DoubleClick();
                }
                WaitForClosed(() => lookupDialog, 3000);
            }
        }

        private bool IsLookupDialogOpen()
        {
            var modal = _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault();
            if (modal != null) return true;

            var desktopWin = _automation.GetDesktop().FindAllChildren(cf => cf.ByControlType(ControlType.Window))
                .FirstOrDefault(w => !string.IsNullOrEmpty(w.Name) &&
                                    (w.Name.Contains("ข้อมูลประเภทหน่วยงาน") || w.Name.Contains("ข้อมูลหน่วยงาน")));
            return desktopWin != null;
        }

        private void ClearLookupValue(AutomationElement context, string buttonAutomationId)
        {
            var targetControl = context.FindFirstDescendant(cf => cf.ByAutomationId(buttonAutomationId));
            if (targetControl != null)
            {
                targetControl.Focus();
                Keyboard.Press(VirtualKeyShort.CONTROL);
                Keyboard.Type(VirtualKeyShort.KEY_A);
                Keyboard.Release(VirtualKeyShort.CONTROL);
                Keyboard.Type(VirtualKeyShort.DELETE);

                if (targetControl.Patterns.Value.IsSupported)
                {
                    try { targetControl.Patterns.Value.Pattern.SetValue(string.Empty); } catch { }
                }
            }
        }

        private void ClickCloseAndHandleDialog(Window form, string buttonToClick)
        {
            form.Focus();

            var closeBtn = form.FindFirstDescendant(cf => cf.ByName("Close"))
                        ?? form.FindFirstDescendant(cf => cf.ByName("&Close"))
                        ?? form.FindFirstDescendant(cf => cf.ByName("button 'Close'"));

            if (closeBtn != null) closeBtn.Click();
            else SafeClick(form, "Close");

            Window? confirmDialog = Retry(() =>
                form.ModalWindows.FirstOrDefault() ??
                _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault() ??
                _automation.GetDesktop().FindFirstChild(cf => cf.ByName("คำเตือน"))?.AsWindow() ??
                _automation.GetDesktop().FindFirstChild(cf => cf.ByName("Question"))?.AsWindow(), timeoutMs: 4000);

            if (confirmDialog == null) throw new Exception("กด Close แล้วไม่พบ Pop-up ยืนยันคำเตือน");

            ClickDialogButton(confirmDialog, buttonToClick);
            WaitForClosed(() => confirmDialog);
        }

        private void ClickDialogButton(AutomationElement dialog, string buttonName)
        {
            var btn = dialog.FindFirstDescendant(cf => cf.ByName(buttonName))
                   ?? dialog.FindFirstDescendant(cf => cf.ByName($"&{buttonName}"))
                   ?? dialog.FindFirstDescendant(cf => cf.ByName($"button '&{buttonName}'"))
                   ?? dialog.FindFirstDescendant(cf => cf.ByAutomationId($"btn{buttonName}"));

            if (btn == null)
            {
                var allButtons = dialog.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
                btn = allButtons.FirstOrDefault(b => b.Name != null && b.Name.Contains(buttonName, StringComparison.OrdinalIgnoreCase));
            }

            if (btn != null)
            {
                btn.Click();
            }
            else
            {
                char shortcutChar = buttonName.Trim().ToUpper()[0];
                Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, (VirtualKeyShort)Enum.Parse(typeof(VirtualKeyShort), $"KEY_{shortcutChar}"));
            }
        }

        private void SafeCloseForm(Window form)
        {
            SafeClick(form, "Close");
            Window? dialog = Retry(() =>
                form.ModalWindows.FirstOrDefault() ??
                _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault() ??
                _automation.GetDesktop().FindFirstChild(cf => cf.ByName("คำเตือน"))?.AsWindow() ??
                _automation.GetDesktop().FindFirstChild(cf => cf.ByName("Question"))?.AsWindow(), timeoutMs: 1500);

            if (dialog != null)
            {
                try { ClickDialogButton(dialog, "No"); }
                catch { _ = SafeClick(dialog, "No") || SafeClick(dialog, "&No") || SafeClick(dialog, "Cancel"); }
                WaitForClosed(() => dialog);
            }

            WaitForClosed(() => FindOpenForm());
        }

        private void SwitchToTab(Window form, string tabAutomationId, string tabName)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try { Keyboard.Type(VirtualKeyShort.ESCAPE); } catch { }

                var tab = Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(tabAutomationId))
                                       ?? form.FindFirstDescendant(cf => cf.ByName(tabName)), 2000);

                if (tab == null) continue;

                tab.Click();

                if (WaitUntil(() => IsTabSelected(tab), 1500)) return;
            }
        }

        private bool IsTabSelected(AutomationElement tab)
        {
            try
            {
                if (tab.Patterns.SelectionItem.IsSupported)
                    return tab.Patterns.SelectionItem.Pattern.IsSelected.Value;
            }
            catch { }
            return true;
        }

        private AutomationElement? FindGridRowByCodeInContainer(AutomationElement container, string code)
        {
            return Retry(() =>
            {
                var rows = container.FindAllDescendants(cf =>
                    cf.ByControlType(ControlType.DataItem)
                    .Or(cf.ByControlType(ControlType.ListItem))
                    .Or(cf.ByControlType(ControlType.Custom)));

                foreach (var r in rows)
                {
                    string name = r.Name ?? "";
                    string val = "";
                    try { if (r.Patterns.Value.IsSupported) val = r.Patterns.Value.Pattern.Value.ValueOrDefault ?? ""; } catch { }

                    if (name.Contains(code) || val.Contains(code)) return r;

                    var textChild = r.FindFirstDescendant(cf => cf.ByName(code));
                    if (textChild != null) return r;
                }

                var directMatch = container.FindFirstDescendant(cf => cf.ByName(code));
                return directMatch?.Parent ?? directMatch;
            }, timeoutMs: 4000);
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
                throw new Exception("ฟอร์มไม่ถูกปิด");
            LogSuccess(testName, "ฟอร์มถูกปิด");
        }


        // =========================================================================================
        // หมวดที่ 3: ส่วนการทำงานกลางที่ตายตัว (Common / Reusable Helpers)
        // =========================================================================================

        private readonly AutomationElement _mainWindow;
        private readonly UIA3Automation _automation;
        private int _totalSuccess = 0, _totalFail = 0;
        private const int TimeoutMs = 6000;

        public OrganizationUnitInfoUC(AutomationElement mainWindow, UIA3Automation automation)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

        private void RefreshGrid()
        {
            try { _mainWindow.FindFirstDescendant(cf => cf.ByName("Find"))?.Click(); } catch { }
            try
            {
                var g = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataGrid))
                                     ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Table)), 2000);
                g?.Focus();
                g?.Click();
            }
            catch { }
        }

        private bool VerifyRecordInGrid(string val, int timeoutMs = 5000, bool refresh = true)
        {
            if (refresh) RefreshGrid();
            return Retry(() => FindGridRowByCodeInContainer(_mainWindow, val), timeoutMs) != null;
        }

        private void LogGridResult(string testName, string code)
        {
            if (VerifyRecordInGrid(code)) LogSuccess(testName, $"'{code}' เพิ่มสำเร็จ (พบในตารางหลัก)");
            else LogFailure(testName, $"'{code}' ไม่พบในตารางหลักหลังบันทึก");
        }

        private void RunStep(string testName, Action act, int progress, string progressMessage)
        {
            try { act(); }
            catch (Exception ex) { LogFailure(testName, $"ไม่ผ่าน: {ex.Message}"); }
            TestProgressHelper.Report(progress, progressMessage);
        }

        private void LogSuccess(string testName, string message)
        {
            _totalSuccess++;
            TestLogger.LogSuccess(testName, message);
        }

        private void LogFailure(string testName, string message)
        {
            _totalFail++;
            TestLogger.LogFailure(testName, message);
        }

        private bool SafeClick(AutomationElement scope, string buttonName, int timeoutMs = 6000)
        {
            var btn = Retry(() => scope.FindFirstDescendant(cf => cf.ByName(buttonName)
                                                                ?? cf.ByName($"&{buttonName}")
                                                                ?? cf.ByAutomationId(buttonName)), timeoutMs);
            if (btn == null) return false;

            try
            {
                if (btn.Patterns.Invoke.IsSupported)
                {
                    btn.Patterns.Invoke.Pattern.Invoke();
                    return true;
                }
                btn.Click();
                return true;
            }
            catch { return false; }
        }

        private bool SafeClickMainWindow(string buttonName, int timeoutMs = 6000) => SafeClick(_mainWindow, buttonName, timeoutMs);

        private void FillTextBox(AutomationElement context, string automationId, string text)
        {
            var element = Retry(() => context.FindFirstDescendant(cf => cf.ByAutomationId(automationId)), timeoutMs: 4000)
                ?? throw new Exception($"หาช่องกรอก '{automationId}' ไม่เจอ");

            element.Focus();
            element.Click();
            Thread.Sleep(100);

            Keyboard.Press(VirtualKeyShort.CONTROL);
            Keyboard.Type(VirtualKeyShort.KEY_A);
            Keyboard.Release(VirtualKeyShort.CONTROL);
            Keyboard.Type(VirtualKeyShort.DELETE);
            Thread.Sleep(50);

            if (!string.IsNullOrEmpty(text))
            {
                if (element.Patterns.Value.IsSupported)
                {
                    try { element.Patterns.Value.Pattern.SetValue(text); }
                    catch { Keyboard.Type(text); }
                }
                else
                {
                    Keyboard.Type(text);
                }
            }

            Keyboard.Press(VirtualKeyShort.TAB);
            Thread.Sleep(100);
        }

        private void ClearField(AutomationElement context, string automationId)
        {
            var element = Retry(() => context.FindFirstDescendant(cf => cf.ByAutomationId(automationId)), timeoutMs: 3000);
            if (element != null)
            {
                try
                {
                    element.Focus();
                    element.Click();
                    Thread.Sleep(100);

                    Keyboard.Press(VirtualKeyShort.CONTROL);
                    Keyboard.Type(VirtualKeyShort.KEY_A);
                    Keyboard.Release(VirtualKeyShort.CONTROL);
                    Keyboard.Type(VirtualKeyShort.DELETE);

                    if (element.Patterns.Value.IsSupported)
                    {
                        try { element.Patterns.Value.Pattern.SetValue(string.Empty); } catch { }
                    }

                    Keyboard.Press(VirtualKeyShort.TAB);
                    Thread.Sleep(100);
                }
                catch { }
            }
        }

        public void EnsureNoStrayModal(string context)
        {
            try
            {
                var modals = _mainWindow.AsWindow()?.ModalWindows?.ToList() ?? new List<Window>();
                foreach (var modal in modals)
                {
                    string autoId = modal.Properties.AutomationId.ValueOrDefault ?? "";
                    string name = modal.Name ?? "";

                    if (autoId == FormId || name.Contains("บันทึกข้อมูลหน่วยงาน") || name.Contains("Prosoft HRMI")) continue;

                    if (name.Contains("คำเตือน") || name.Contains("Question"))
                    {
                        try { ClickDialogButton(modal, "No"); }
                        catch { _ = SafeClick(modal, "No") || SafeClick(modal, "&No") || SafeClick(modal, "Cancel"); }
                        WaitForClosed(() => modal, 2000);
                    }
                    else if (name.Contains("Task Result") || autoId.Contains("TaskResult"))
                    {
                        _ = SafeClick(modal, "Close") || SafeClick(modal, "OK");
                        WaitForClosed(() => modal, 2000);
                    }
                }
            }
            catch (Exception ex)
            {
                TestLogger.LogInfo(context, $"EnsureNoStrayModal: {ex.Message}");
            }
        }

        private static bool WaitUntil(Func<bool> condition, int timeoutMs = TimeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try { if (condition()) return true; } catch { }
                Thread.Sleep(80);
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
                Thread.Sleep(100);
            }
            return null;
        }
    }
}