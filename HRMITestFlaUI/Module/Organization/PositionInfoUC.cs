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
    public class PositionInfoUC
    {
        // =========================================================================================
        // หมวดที่ 1: ข้อมูลการทดสอบทั้ง 5 ขั้น (Test Data)
        // =========================================================================================

        // --- Data Records สำหรับใช้งานในแต่ละเฟส ---
        private sealed record PositionAddItem(
            bool ClickNew,
            string Code,
            string Name,
            string NameEng,
            string ReportTo,
            string Description,
            string Save);

        private sealed record PositionEditItem(
            string TestCaseId,
            string OldCode,
            string NewCode,
            string NewName,
            string SaveButtonName,
            int Progress,
            string ProgressMsg);

        private sealed record DeleteItem(
            string TargetCode,
            string ConfirmButton,
            bool ExpectDeleted,
            int Progress,
            string ProgressMsg);

        // [ขั้นที่ 1] ข้อมูลทดสอบการสร้างใหม่ (Add / Navigate)
        private static readonly PositionAddItem[] Step1_AddCases =
        {
            new(
                true,
                "H-001",
                "หัวหน้างาน",
                "Supervisor",
                "",
                "",
                "SaveClose"),
            new(
                true,
                "P-001",
                "พนักงานทั่วไป",
                "General staff",
                "",
                "",
                "SaveNew"),
            new(
                true,
                "POS001",
                "เจ้าหน้าที่ทรัพยากรบุคคล",
                "Human Resources Officer",
                "H-001",
                "ดูแลงานด้านบุคคลและเอกสารพนักงาน",
                "SaveNew"),
            new(
                true,
                "POS002",
                "เจ้าหน้าที่บัญชี",
                "Accounting Officer",
                "P-001",
                "รับผิดชอบงานบัญชีและการเงิน",
                "SaveNew"),
            new(
                false,
                "POS003",
                "โปรแกรมเมอร์",
                "Programmer",
                "H-001",
                "พัฒนาและปรับปรุงระบบ",
                "SaveNew"),
            new(
                false,
                "POS004",
                "เจ้าหน้าที่จัดซื้อ",
                "Purchasing Officer",
                "P-001",
                "ดำเนินการจัดซื้อสินค้าและบริการ",
                "SaveNew"),
            new(
                false,
                "POS005",
                "เจ้าหน้าที่คลังสินค้า",
                "Warehouse Officer",
                "H-001",
                "ดูแลการรับเข้า-จ่ายออกสินค้า",
                "SaveAndClose")
        };

        // [ขั้นที่ 3] ข้อมูลทดสอบการแก้ไขข้อมูล (Edit Records)
        private static readonly PositionEditItem[] Step3_EditCases =
        {
            new(
                "TestCase03_EditData_POS001_to_DW001",
                "POS001",
                "DW001",
                "เจ้าหน้าที่ขาย",
                "Save",
                48,
                "Edited POS001 to DW001..."),
            new(
                "TestCase03_EditData_POS002_to_DW002",
                "POS002",
                "DW002",
                "เจ้าหน้าที่บริการลูกค้า",
                "Save and Close",
                55,
                "Edited POS002 to DW002...")
        };

        // [ขั้นที่ 5] ข้อมูลทดสอบการลบข้อมูล (Delete Records)
        private static readonly DeleteItem[] Step5_DeleteCases =
        {
            new("DW002", "Yes", true, 92, "Delete Test: DW002 (Yes)..."),
            new("DW001", "No", false, 100, "Delete Test: DW001 (No)...")
        };


        // =========================================================================================
        // หมวดที่ 2: ส่วนที่ขึ้นกับหน้าจอนั้นๆ (Screen-Specific Implementation)
        // **เมื่อเปลี่ยนไปทำหน้าจออื่น ต้องมาแก้ไขค่าและเมทอดในส่วนนี้ทั้งหมด**
        // =========================================================================================

        // 2.1 Locator & Mappings ประจำหน้าจอ
        private const string FormId = "PositionInfoUC";
        private const string LogPageKey = "PositionInfoUC";
        private const string GeneralTabId = "devTabPgGeneral";
        private const string MenuSearchKeyword = "บันทึกข้อมูลตำแหน่ง";

        // 2.2 Controls AutomationId ประจำหน้าจอ
        private const string IdTxtPositionCode = "devtxtPositionCode";
        private const string IdTxtPositionName = "devtxtPositionName";
        private const string IdTxtPositionNameEng = "devtxtPositionNameEng";
        private const string IdBtnReportTo = "devbtnEditReportTo";
        private const string IdMemoPosition = "devMemoPosition";

        /// <summary>
        /// ขั้นที่ 1: เปิดหน้าจอและกรอกข้อมูลสร้างใหม่
        /// </summary>
        public void NavigateToPositionInfoUC()
        {
            const string stageName = "TestCase00_AddPositionData";
            TestLogger.LogInfo(LogPageKey, "เริ่มทดสอบหน้าจอบันทึกข้อมูลตำแหน่ง");
            TestProgressHelper.Report(2, "Opening Organization -> Position Info Menu via Search...");

            if (!TryOpenPositionMenu(stageName)) return;

            Window? form = null;

            for (int i = 0; i < Step1_AddCases.Length; i++)
            {
                var data = Step1_AddCases[i];
                string testName = $"{stageName}_{data.Code}";
                TestProgressHelper.Report(4 + (i * 3), $"Adding Position ({i + 1}/{Step1_AddCases.Length}): {data.Code} - {data.Name}...");

                try
                {
                    if (data.ClickNew || form == null || !form.IsAvailable)
                    {
                        form = OpenForm();
                    }

                    FillGeneralTab(form, data.Code, data.Name, data.NameEng, data.ReportTo, data.Description);

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
                            var codeInput = form.FindFirstDescendant(cf => cf.ByAutomationId(IdTxtPositionCode))?.AsTextBox();
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

        /// <summary>
        /// ขั้นที่ 2: รัน Validation ตอนสร้างข้อมูลใหม่
        /// </summary>
        public void RunAllValidationTests()
        {
            const string stageName = "TestCase01-06_ValidationNew";
            TestProgressHelper.Report(22, "Opening Position Form for Validation Tests...");
            EnsureNoStrayModal($"{stageName}_PreCheck");

            Window form;
            try { form = OpenForm(); }
            catch (Exception ex) { LogFailure(stageName, $"เปิดฟอร์มไม่สำเร็จ: {ex.Message}"); return; }

            var context = GetGeneralTab(form);

            RunStep("TestCase01_MissingCode", () =>
            {
                ClearField(context, IdTxtPositionCode);
                FillTextBox(context, IdTxtPositionName, "เจ้าหน้าที่ทรัพยากรบุคคล");

                SafeClick(form, "Save");
                AssertTaskResult(form, new[] { "รหัสตำแหน่ง ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล" }, "TestCase01_MissingCode");
                AssertFormOpen(form, "TestCase01_MissingCode");
            }, 26, "Validating: Missing Code...");

            RunStep("TestCase02_MissingName", () =>
            {
                FillTextBox(context, IdTxtPositionCode, "POS001");
                ClearField(context, IdTxtPositionName);

                SafeClick(form, "Save and New");
                AssertTaskResult(form, new[] { "ชื่อตำแหน่ง ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล" }, "TestCase02_MissingName");
                AssertFormOpen(form, "TestCase02_MissingName");
            }, 29, "Validating: Missing Name...");

            RunStep("TestCase03_MissingAllFields", () =>
            {
                ClearField(context, IdTxtPositionCode);
                ClearField(context, IdTxtPositionName);

                SafeClick(form, "Save and New");
                AssertTaskResult(form, new[] {
                    "รหัสตำแหน่ง ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล",
                    "ชื่อตำแหน่ง ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล"
                }, "TestCase03_MissingAllFields");
                AssertFormOpen(form, "TestCase03_MissingAllFields");
            }, 32, "Validating: Missing All Fields...");

            RunStep("TestCase04_DuplicateCode", () =>
            {
                FillTextBox(context, IdTxtPositionCode, "POS005");
                FillTextBox(context, IdTxtPositionName, "เจ้าหน้าที่คลังสินค้า");

                SafeClick(form, "Save and New");
                AssertTaskResult(form, new[] { "รหัสตำแหน่ง ห้ามซ้ำ กรุณาป้อนข้อมูล" }, "TestCase04_DuplicateCode");
                AssertFormOpen(form, "TestCase04_DuplicateCode");
            }, 35, "Validating: Duplicate Code...");

            const string subCase = "TestCase05_DuplicateName";
            RunStep(subCase, () =>
            {
                FillTextBox(context, IdTxtPositionCode, "POS006");
                FillTextBox(context, IdTxtPositionName, "เจ้าหน้าที่คลังสินค้า");

                SafeClick(form, "Save and New");
                AssertTaskResult(form, new[] { "ชื่อตำแหน่ง ห้ามซ้ำ กรุณาป้อนข้อมูล" }, subCase);
                AssertFormOpen(form, subCase);
            }, 38, "Validating: Duplicate Name...");

            RunStep($"{subCase}_CloseSequence", () =>
                RunCloseDialogSequence(form, subCase, "ชื่อตำแหน่ง ห้ามซ้ำ กรุณาป้อนข้อมูล"),
                40, "Validating: Close Dialog Sequence...");
        }

        /// <summary>
        /// ขั้นที่ 3: เลือกแก้ไขรายการในตาราง
        /// </summary>
        public void RunEditTests()
        {
            const string stageName = "TestCase03_EditData";
            TestProgressHelper.Report(42, "Starting Edit Operations...");
            EnsureNoStrayModal($"{stageName}_PreCheck");

            foreach (var edit in Step3_EditCases)
            {
                PerformEdit(edit.TestCaseId, edit.OldCode, edit.NewCode, edit.NewName, edit.SaveButtonName);
                TestProgressHelper.Report(edit.Progress, edit.ProgressMsg);
            }
        }

        /// <summary>
        /// ขั้นที่ 4: รัน Validation ตอนแก้ไขข้อมูล
        /// </summary>
        public void RunEditValidationTests()
        {
            const string baseTestName = "TestCase04_EditValidation";
            TestProgressHelper.Report(58, "Opening Edit Form for Validation Tests...");
            EnsureNoStrayModal($"{baseTestName}_PreCheck");

            Window form;
            try { form = ClickRowAndOpenEdit("DW001"); }
            catch (Exception ex) { LogFailure(baseTestName, $"เปิดฟอร์ม Edit ไม่สำเร็จ: {ex.Message}"); return; }

            var context = GetGeneralTab(form);

            // 4.1 Missing Code: ลบรหัสออก -> กด Save
            RunStep($"{baseTestName}_MissingCode", () =>
            {
                form.Focus();
                ClearField(context, IdTxtPositionCode);
                FillTextBox(context, IdTxtPositionName, "วิศวกร");

                SafeClick(form, "Save");
                AssertTaskResult(form, new[] { "รหัสตำแหน่ง ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล" }, $"{baseTestName}_MissingCode");
            }, 64, "Validating Edit: Missing Code...");

            // 4.2 Missing Name: ใส่รหัส POS003 + ลบชื่อออก -> กด Save
            RunStep($"{baseTestName}_MissingName", () =>
            {
                form.Focus();
                FillTextBox(context, IdTxtPositionCode, "POS003");
                ClearField(context, IdTxtPositionName);

                if (!SafeClick(form, "Save and New")) SafeClick(form, "Save");

                AssertTaskResult(form, new[] { "ชื่อตำแหน่ง ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล" }, $"{baseTestName}_MissingName");
            }, 68, "Validating Edit: Missing Name...");

            // 4.3 Missing Both: ลบทั้งรหัสและชื่อออก -> กด Save
            RunStep($"{baseTestName}_MissingBoth", () =>
            {
                form.Focus();
                ClearField(context, IdTxtPositionCode);
                ClearField(context, IdTxtPositionName);

                SafeClick(form, "Save");
                AssertTaskResult(form, new[] {
                    "รหัสตำแหน่ง ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล",
                    "ชื่อตำแหน่ง ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล"
                }, $"{baseTestName}_MissingBoth");
            }, 72, "Validating Edit: Missing Both...");

            // 4.4 Duplicate Code: กรอกรหัสซ้ำ -> กด Save
            RunStep($"{baseTestName}_DuplicateCode", () =>
            {
                form.Focus();
                FillTextBox(context, IdTxtPositionCode, "POS003");
                FillTextBox(context, IdTxtPositionName, "โปรแกรมเมอร์");

                if (!SafeClick(form, "Save and New")) SafeClick(form, "Save");

                AssertTaskResult(form, new[] { "รหัสตำแหน่ง ห้ามซ้ำ กรุณาป้อนข้อมูล" }, $"{baseTestName}_DuplicateCode");
            }, 76, "Validating Edit: Duplicate Code...");

            // 4.5 Duplicate Name: กรอกชื่อซ้ำ -> กด Save
            const string subCase = $"{baseTestName}_DuplicateName";
            RunStep(subCase, () =>
            {
                form.Focus();
                ClearField(context, IdTxtPositionCode);
                ClearField(context, IdTxtPositionName);

                FillTextBox(context, IdTxtPositionCode, "POS006");
                FillTextBox(context, IdTxtPositionName, "โปรแกรมเมอร์");

                if (!SafeClick(form, "Save and New")) SafeClick(form, "Save");

                AssertTaskResult(form, new[] { "ชื่อตำแหน่ง ห้ามซ้ำ กรุณาป้อนข้อมูล" }, subCase);
                AssertFormOpen(form, subCase);
            }, 80, "Validating Edit: Duplicate Name...");

            // 4.6 Close Dialog Sequence
            RunStep($"{subCase}_CloseSequence", () =>
                RunCloseDialogSequence(form, subCase, "ชื่อตำแหน่ง ห้ามซ้ำ กรุณาป้อนข้อมูล"),
                84, "Validating Edit: Close Dialog Sequence...");
        }

        /// <summary>
        /// ขั้นที่ 5: ทดสอบการลบรายการในตาราง
        /// </summary>
        public void RunDeleteTests()
        {
            const string baseTestName = "TestCase05_DeleteData";
            TestProgressHelper.Report(84, "Starting Delete Tests...");

            foreach (var item in Step5_DeleteCases)
            {
                RunDeleteCase(baseTestName, item.TargetCode, item.ConfirmButton, item.ExpectDeleted);
                TestProgressHelper.Report(item.Progress, item.ProgressMsg);
            }

            LogSuccess(baseTestName, "ทดสอบ Delete ครบทุกเคสแล้ว");
            TestLogger.LogInfo(LogPageKey, $"สรุปผลรวมทั้งหมด: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");
        }

        // --- Custom Mapping Logic ประจำหน้าจอนี้ ---
        private AutomationElement GetGeneralTab(Window form) =>
            form.FindFirstDescendant(cf => cf.ByAutomationId(GeneralTabId)) ?? form;

        private void FillGeneralTab(Window form, string code, string name, string nameEng, string reportTo = "", string description = "")
        {
            SwitchToTab(form, GeneralTabId, "General");
            var ctx = form.FindFirstDescendant(cf => cf.ByAutomationId(GeneralTabId)) ?? form;

            if (!string.IsNullOrEmpty(code)) FillTextBox(ctx, IdTxtPositionCode, code);
            if (!string.IsNullOrEmpty(name)) FillTextBox(ctx, IdTxtPositionName, name);
            if (!string.IsNullOrEmpty(nameEng)) FillTextBox(ctx, IdTxtPositionNameEng, nameEng);

            if (!string.IsNullOrEmpty(reportTo))
                SelectLookupValue(ctx, IdBtnReportTo, reportTo, "ตำแหน่งหัวหน้างาน");

            if (!string.IsNullOrEmpty(description)) FillTextBox(ctx, IdMemoPosition, description);
        }

        private Window OpenForm()
        {
            SafeClickMainWindow("New");
            return GetPositionFormWindow();
        }

        private Window? FindOpenForm() =>
            _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault() ??
            _automation.GetDesktop().FindFirstChild(cf => cf.ByAutomationId(FormId))?.AsWindow();

        private Window GetPositionFormWindow()
        {
            var form = Retry(FindOpenForm, 8000) ?? throw new Exception("หาฟอร์ม PositionInfoUC ไม่เจอ");
            WaitUntil(() =>
            {
                var txt = form.FindFirstDescendant(cf => cf.ByAutomationId(IdTxtPositionCode));
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

            var form = GetPositionFormWindow();
            WaitUntil(() =>
            {
                var txt = form.FindFirstDescendant(cf => cf.ByAutomationId(IdTxtPositionCode))?.AsTextBox();
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

                FillTextBox(context, IdTxtPositionCode, newCode);
                FillTextBox(context, IdTxtPositionName, newName);

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

        private bool TryOpenPositionMenu(string stage)
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

                Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("New")), 6000);
                return true;
            }
            catch (Exception ex)
            {
                LogFailure(stage, $"ค้นหาและเปิดหน้าจอไม่สำเร็จ: {ex.Message}");
                return false;
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
                                         (w.Name.Contains("ข้อมูลตำแหน่ง") || w.Name.Contains("ข้อมูล")));
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
                .FirstOrDefault(w => !string.IsNullOrEmpty(w.Name) && w.Name.Contains("ข้อมูลตำแหน่ง"));
            return desktopWin != null;
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
            if (texts.Any(t => t.Contains("รหัสตำแหน่ง ห้ามซ้ำ") || t.Contains("ห้ามซ้ำ"))) return "รหัสหรือข้อมูลซ้ำ";
            return null;
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


        // =========================================================================================
        // หมวดที่ 3: ส่วนการทำงานกลางที่ตายตัว (Common / Reusable Helpers)
        // **ใช้งานร่วมกันได้ทุกหน้าจอ ไม่ต้องแก้ไขคำสั่งเมื่อเปลี่ยนหน้า**
        // =========================================================================================

        private readonly AutomationElement _mainWindow;
        private readonly UIA3Automation _automation;
        private int _totalSuccess = 0;
        private int _totalFail = 0;
        private const int TimeoutMs = 6000;

        public PositionInfoUC(AutomationElement mainWindow, UIA3Automation automation)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
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

                    if (autoId == FormId || name.Contains("บันทึกข้อมูลตำแหน่ง") || name.Contains("Prosoft HRMI")) continue;

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