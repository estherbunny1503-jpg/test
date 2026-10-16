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
using System.Runtime.InteropServices;
using System.Threading;

namespace HRMITestFlaUI.Module.Organization
{
    public class OrgInfoUC
    {
        // =========================================================================================
        // หมวดที่ 1: ข้อมูลการทดสอบครบทั้ง 5 ขั้น (Test Data: Step 1 - Step 5)
        // =========================================================================================

        private sealed record OrgAddItem(
            string Code,
            string Name,
            string NameEng,
            string Seq,
            string TaxBranch,
            string TaxId,
            string Date,
            string Era,
            string Address);

        private sealed record ValidationItem(
            string TestCaseId,
            int Progress,
            string ProgressMsg,
            Action<OrgInfoUC, Window> Action,
            string[] ExpectedMessages);

        private sealed record OrgEditItem(
            string TestCaseId,
            string OldCode,
            string NewCode,
            string NameTh,
            string SaveBtn,
            bool NeedsManualClose);

        private sealed record DeleteItem(
            string TargetCode,
            string ConfirmButton,
            bool ExpectDeleted);

        // [ขั้นที่ 1] ข้อมูลทดสอบการสร้างใหม่ (Add / Navigate)
        private static readonly OrgAddItem[] Step1_AddCases =
        {
            new("S002", "บริษัท ตัวอย่าง (สาขาเชียงใหม่) จำกัด",  "Sample Company (Chiang Mai Branch) Co., Ltd.",   "1", "00001", "0105569000012", "31/07/2569", "พุทธศักราช", "123/45 อาคารเทสติ้ง ทาวเวอร์"),
            new("S003", "บริษัท ตัวอย่าง (สาขากระบี่)จำกัด",      "Sample Company (Krabi Branch) Co., Ltd.",             "2", "00002", "0105569000013", "1/1/2569",   "พุทธศักราช", "88/99 ถนนมหาราช "),
            new("S004", "บริษัท ตัวอย่าง (สาขาเชียงราย) จำกัด",   "Sample Company (Chiang Rai Branch) Co., Ltd.",      "3", "00003", "0105569000014", "1/2/2569",   "พุทธศักราช", "45/1 ถนนซุเปอร์ไฮเวย์"),
            new("S005", "บริษัท ตัวอย่าง (สาขาภูเก็ต) จำกัด",      "Sample Company (Phuket Branch) Co., Ltd.",          "4", "00004", "0105569000015", "15/03/2569", "พุทธศักราช", "74/5 ถนนเทพกระษัตริ์"),
            new("S006", "บริษัท ตัวอย่าง (สาขาขอนแก่น) จำกัด",    "Sample Company (Khon Kaen Branch) Co., Ltd.",      "5", "00005", "0105569000011", "31/07/2569", "พุทธศักราช", "555/12 ถนนมิตรภาพ")
        };

        // [ขั้นที่ 2] ข้อมูลทดสอบแจ้งเตือนตอนสร้างใหม่ (New Validation)
        private static readonly ValidationItem[] Step2_NewValidationCases =
        {
            // 2.1 ไม่กรอกรหัส + ไม่กรอกชื่อ + กรอกที่อยู่ -> กด Save
            new("TestCase01_MissingCodeAndName", 26, "Validating: Missing Code and Name...",
                (page, form) => page.FillSaveAssert(form, "", "", "123/45 อาคารเทสติ้ง ทาวเวอร์", "Save", "TestCase01_MissingCodeAndName",
                    "รหัสองค์กร ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล", "ชื่อองค์กร ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล"),
                new[] { "รหัสองค์กร ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล", "ชื่อองค์กร ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล" }),

            // 2.2 กรอกรหัส S002 (ซ้ำ) + กรอกชื่อ S002 (ซ้ำ) + กรอกที่อยู่ -> กด Save and Close
            new("TestCase02_DuplicateCodeAndName", 30, "Validating: Duplicate Code and Name...",
                (page, form) =>
                {
                    page.FillSaveAssert(form, "S002", "บริษัท ตัวอย่าง (สาขาเชียงใหม่) จำกัด", "123/45 อาคารเทสติ้ง ทาวเวอร์", "Save and Close", "TestCase02_DuplicateCodeAndName",
                        "รหัสองค์กร ห้ามซ้ำ กรุณาป้อนข้อมูล");
                    page.AssertFormOpen(form, "TestCase02_DuplicateCodeAndName");
                },
                new[] { "รหัสองค์กร ห้ามซ้ำ กรุณาป้อนข้อมูล" })
        };

        // [ขั้นที่ 3] ข้อมูลทดสอบการแก้ไขข้อมูล (Edit Records)
        private static readonly OrgEditItem[] Step3_EditCases =
        {
            new("TestCase09_EditRow1", "S002", "SAL001", "บริษัท ตัวอย่าง (สาขาเชียงใหม่)", "Save", true),
            new("TestCase10_EditRow2", "S003", "SAL002", "บริษัท ตัวอย่าง (สาขากระบี่)", "Save and Close", false)
        };

        // [ขั้นที่ 4] ข้อมูลทดสอบแจ้งเตือนตอนแก้ไขข้อมูล (Edit Validation)
        private static readonly ValidationItem[] Step4_EditValidationCases =
        {
            // 4.1 ลบรหัสองค์กร + ลบชื่อ + กรอกที่อยู่ -> กด Save
            new("TestCase11_Edit_MissingCode", 64, "Validating Edit: Missing Code and Name...",
                (page, form) => page.FillSaveAssert(form, "", "", "123/45 อาคารเทสติ้ง ทาวเวอร์", "Save", "TestCase11_Edit_MissingCode",
                    "รหัสองค์กร ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล", "ชื่อองค์กร ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล"),
                new[] { "รหัสองค์กร ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล", "ชื่อองค์กร ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล" }),

            // 4.4 กรอกรหัสซ้ำ S004 + กรอกชื่อซ้ำ -> Close -> Yes
            new("TestCase14_Edit_Duplicate_CloseThenYes", 72, "Validating Edit: Duplicate Code Close Yes...",
                (page, form) =>
                {
                    page.SwitchToTab(form, GeneralTabId, "General");
                    var ctx = form.FindFirstDescendant(cf => cf.ByAutomationId(GeneralTabId)) ?? form;
                    page.FillTextBox(ctx, IdTxtOrgCode, "S004");
                    page.FillTextBox(ctx, IdTxtOrgName, "บริษัท ตัวอย่าง (สาขาเชียงราย) จำกัด");
                    page.FillAddressTab(form, "123/45 อาคารเทสติ้ง ทาวเวอร์");

                    page.ClickCloseAndHandleDialog(form, "Yes", "TestCase14_Edit_Duplicate_CloseThenYes");
                    page.AssertTaskResult(form, new[] { "รหัสองค์กร ห้ามซ้ำ กรุณาป้อนข้อมูล" }, "TestCase14_Edit_Duplicate_CloseThenYes", "พบข้อความ: 'รหัสองค์กร ห้ามซ้ำ กรุณาป้อนข้อมูล' และ ฟอร์มถูกเปิดอยู่");
                    page.CheckFormOpenOnly(form);
                },
                new[] { "รหัสองค์กร ห้ามซ้ำ กรุณาป้อนข้อมูล" })
        };

        // [ขั้นที่ 5] ข้อมูลทดสอบการลบข้อมูล (Delete Records)
        private static readonly DeleteItem[] Step5_DeleteCases =
        {
            new("S004", "Yes", true),
            new("S005", "No",  false)
        };


        // =========================================================================================
        // หมวดที่ 2: ส่วนที่ขึ้นกับหน้าจอนั้นๆ (Screen-Specific Implementation)
        // =========================================================================================

        private const string FormId = "OrgInfoUC";
        private const string LogPageKey = "OrgInfoUC";
        private const string GeneralTabId = "devTabPgGeneral";
        private const string AddressTabId = "devTabPgAddress";
        private const string MenuSearchKeyword = "บันทึกข้อมูลองค์กร";

        // Controls AutomationId — General Tab
        private const string IdTxtOrgCode = "devtxtOrgCode";
        private const string IdTxtOrgName = "devtxtOrgName";
        private const string IdTxtOrgNameEng = "devtxtOrgNameEng";
        private const string IdTxtOrgSeq = "devtxtBranchListNo";
        private const string IdTxtTaxBranchNo = "devtxtTaxBranchNo";
        private const string IdTxtTaxId = "devtxtTaxNumber";
        private const string IdDateEstablished = "devDateCreateOrgDay";
        private const string IdCboEra = "devlookUpEditEra";

        // Controls AutomationId — Address Tab
        private const string IdTxtOrgAddressTh = "devtxtOrgAddressTh";

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
        private const int WM_SETTEXT = 0x000C;

        public void NavigateToOrgInfoUC()
        {
            const string stageName = "TestCase00_AddOrgData";
            TestLogger.LogInfo(LogPageKey, "เริ่มทดสอบหน้าจอบันทึกข้อมูลองค์กร");
            TestProgressHelper.Report(2, "Opening Organization -> Org Info Menu via Search...");

            if (!TryOpenOrgMenu(stageName)) return;

            Window? form = null;
            for (int i = 0; i < Step1_AddCases.Length; i++)
            {
                var data = Step1_AddCases[i];
                string testName = $"{stageName}_{data.Code}";
                TestProgressHelper.Report(4 + (i * 3), $"Adding Org ({i + 1}/{Step1_AddCases.Length}): {data.Code} - {data.Name}...");

                try
                {
                    form ??= GetOrOpenForm();

                    FillGeneralTab(form, data.Code, data.Name, data.NameEng, data.Seq, data.TaxBranch, data.TaxId, data.Date, data.Era);
                    FillAddressTab(form, data.Address);

                    string saveButtonName = (i == Step1_AddCases.Length - 1) ? "Save and Close" : "Save and New";
                    if (!TrySaveAndHandleDuplicate(form, testName, data.Code, saveButtonName))
                    {
                        form = null;
                        continue;
                    }

                    if (saveButtonName == "Save and New")
                    {
                        var codeInput = form.FindFirstDescendant(cf => cf.ByAutomationId(IdTxtOrgCode))?.AsTextBox();
                        WaitUntil(() => string.IsNullOrEmpty(codeInput?.Text), 3000);
                        LogSuccess(testName, $"'{data.Code}' บันทึกสำเร็จ (Save and New)");
                    }
                    else
                    {
                        WaitForClosed(() => FindOpenForm());
                        form = null;
                        LogGridResult(testName, data.Code);
                    }
                }
                catch (Exception ex)
                {
                    LogFailure(testName, $"'{data.Code}' เพิ่มไม่สำเร็จ: {ex.Message}");
                    form = null;
                }
            }
        }

        public void RunAllValidationTests()
        {
            const string stage = "TestCase01-05_Validation";
            TestProgressHelper.Report(22, "Opening Org Info Form for Validation Tests...");

            Window form;
            try { form = OpenForm(); }
            catch (Exception ex) { LogFailure(stage, $"เปิดฟอร์มไม่สำเร็จ: {ex.Message}"); return; }

            foreach (var item in Step2_NewValidationCases)
            {
                RunStep(item.TestCaseId, () => item.Action(this, form), item.Progress, item.ProgressMsg);
            }

            // 2.3 - 2.5: Close Dialog Sequence (Yes, Cancel, No)
            RunStep("TestCase03_CloseThenYes",
                () =>
                {
                    ClickCloseAndHandleDialog(form, "Yes", "TestCase03_CloseThenYes");
                    AssertTaskResult(form, new[] { "รหัสองค์กร ห้ามซ้ำ กรุณาป้อนข้อมูล" }, "TestCase03_CloseThenYes", "พบข้อความ: 'รหัสองค์กร ห้ามซ้ำ กรุณาป้อนข้อมูล' และ ฟอร์มถูกเปิดอยู่");
                    CheckFormOpenOnly(form);
                },
                34, "Validating: Close Then Yes...");

            RunStep("TestCase04_CloseThenCancel",
                () =>
                {
                    ClickCloseAndHandleDialog(form, "Cancel", "TestCase04_CloseThenCancel");
                    AssertFormOpen(form, "TestCase04_CloseThenCancel");
                },
                37, "Validating: Close Then Cancel...");

            RunStep("TestCase05_CloseThenNo",
                () =>
                {
                    ClickCloseAndHandleDialog(form, "No", "TestCase05_CloseThenNo");
                    AssertFormClosed(form, "TestCase05_CloseThenNo");
                },
                40, "Validating: Close Then No...");
        }

        public void RunEditTests()
        {
            TestProgressHelper.Report(42, "Starting Edit Operations...");

            for (int i = 0; i < Step3_EditCases.Length; i++)
            {
                var e = Step3_EditCases[i];
                try
                {
                    var form = ClickRowAndOpenEdit(e.OldCode);
                    SwitchToTab(form, GeneralTabId, "General");
                    var ctx = form.FindFirstDescendant(cf => cf.ByAutomationId(GeneralTabId)) ?? form;

                    FillTextBox(ctx, IdTxtOrgCode, e.NewCode);
                    FillTextBox(ctx, IdTxtOrgName, e.NameTh);

                    if (!TrySaveAndHandleDuplicate(form, e.TestCaseId, e.NewCode, e.SaveBtn)) continue;
                    if (e.NeedsManualClose) form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();

                    WaitForClosed(() => FindOpenForm());
                    RefreshGrid();

                    if (VerifyRecordInGrid(e.NewCode, refresh: false))
                        LogSuccess(e.TestCaseId, $"แก้ไข '{e.OldCode}' เป็น '{e.NewCode}' สำเร็จ (พบในตารางหลัก)");
                    else
                        LogFailure(e.TestCaseId, $"แก้ไข '{e.OldCode}' เป็น '{e.NewCode}' แล้วแต่ไม่พบในตารางหลัก");
                }
                catch (Exception ex) { LogFailure(e.TestCaseId, $"แก้ไข '{e.OldCode}' ไม่สำเร็จ: {ex.Message}"); }
                finally
                {
                    TestProgressHelper.Report(42 + ((i + 1) * 13 / Step3_EditCases.Length), $"Editing Org Record ({i + 1}/{Step3_EditCases.Length})...");
                }
            }
        }

        public void RunEditValidationTests()
        {
            const string baseName = "TestCase11-16_EditValidation";
            TestProgressHelper.Report(58, "Opening Edit Form for Validation Tests...");

            Window form;
            try { form = ClickRowAndOpenEdit("SAL002"); }
            catch (Exception ex) { LogFailure(baseName, $"เปิดฟอร์ม Edit ไม่สำเร็จ: {ex.Message}"); return; }

            foreach (var item in Step4_EditValidationCases)
            {
                RunStep(item.TestCaseId, () => item.Action(this, form), item.Progress, item.ProgressMsg);
            }

            // 4.5: Close -> Cancel
            RunStep("TestCase15_Edit_CloseThenCancel",
                () =>
                {
                    ClickCloseAndHandleDialog(form, "Cancel", "TestCase15_Edit_CloseThenCancel");
                    AssertFormOpen(form, "TestCase15_Edit_CloseThenCancel");
                },
                76, "Validating Edit: Close Cancel...");

            // 4.6: Close -> No
            RunStep("TestCase16_Edit_CloseThenNo",
                () =>
                {
                    ClickCloseAndHandleDialog(form, "No", "TestCase16_Edit_CloseThenNo");
                    AssertFormClosed(form, "TestCase16_Edit_CloseThenNo");
                },
                80, "Validating Edit: Close No...");

            LogSuccess(baseName, "รัน Validation Edit ครบทุกเคสแล้ว");
        }

        public void RunDeleteTests()
        {
            const string baseName = "TestCase16-17_Delete";
            TestProgressHelper.Report(84, "Starting Delete Tests...");

            foreach (var item in Step5_DeleteCases)
            {
                RunDeleteCase(baseName, item.TargetCode, item.ConfirmButton, item.ExpectDeleted);
            }

            LogSuccess(baseName, "ทดสอบ Delete ครบทุกเคสแล้ว");
            TestLogger.LogInfo(LogPageKey, $"สรุปผลรวมทั้งหมด: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");
        }

        // --- Custom Mapping & Control Logic ประจำหน้าจอ ---
        private void FillGeneralTab(Window form, string code, string name, string nameEng, string seq, string taxBranch, string taxId, string date, string era)
        {
            SwitchToTab(form, GeneralTabId, "General");
            var ctx = form.FindFirstDescendant(cf => cf.ByAutomationId(GeneralTabId)) ?? form;

            FillTextBox(ctx, IdTxtOrgCode, code ?? "");
            FillTextBox(ctx, IdTxtOrgName, name ?? "");
            if (nameEng != null) FillTextBox(ctx, IdTxtOrgNameEng, nameEng);
            if (seq != null) FillTextBox(ctx, IdTxtOrgSeq, seq);
            if (taxBranch != null) FillTextBox(ctx, IdTxtTaxBranchNo, taxBranch);
            if (taxId != null) FillTextBox(ctx, IdTxtTaxId, taxId);
            if (date != null) FillTextBox(ctx, IdDateEstablished, date);
            if (!string.IsNullOrEmpty(era))
            {
                var combo = ctx.FindFirstDescendant(cf => cf.ByAutomationId(IdCboEra))?.AsComboBox();
                combo?.Select(era);
            }
        }

        private void FillAddressTab(Window form, string address)
        {
            SwitchToTab(form, AddressTabId, "Address");

            var txtElement = Retry(() => FindAddressTextBoxRobust(form), 6000)
                             ?? throw new Exception("หาช่องกรอก 'ชื่อที่อยู่' (devtxtOrgAddressTh) ในแท็บ Address ไม่เจอ");

            SetTextRobust(txtElement, address);

            string expected = address ?? string.Empty;
            bool matched = WaitUntil(() =>
            {
                string? current = TryReadValue(txtElement);
                return string.Equals((current ?? string.Empty).Trim(), expected.Trim(), StringComparison.Ordinal);
            }, 1000);

            if (!matched)
            {
                SetTextRobust(txtElement, address ?? string.Empty, forceKeyboard: true);
                string? finalValue = TryReadValue(txtElement);

                if (!string.Equals((finalValue ?? string.Empty).Trim(), expected.Trim(), StringComparison.Ordinal))
                {
                    throw new Exception($"ตั้งค่าช่อง 'ชื่อที่อยู่' ไม่สำเร็จ (ต้องการ: '{expected}', ได้จริง: '{finalValue}')");
                }
            }
        }

        private AutomationElement? FindAddressTextBoxRobust(Window form)
        {
            try
            {
                var byId = form.FindFirstDescendant(cf => cf.ByAutomationId(IdTxtOrgAddressTh));
                if (byId != null && !byId.Properties.IsOffscreen.ValueOrDefault) return byId;
            }
            catch { }

            List<AutomationElement> allDescendants;
            try { allDescendants = form.FindAllDescendants().ToList(); }
            catch { return null; }

            foreach (var el in allDescendants)
            {
                string? autoId = null;
                try { autoId = el.Properties.AutomationId.ValueOrDefault; } catch { }
                if (!string.IsNullOrEmpty(autoId) && autoId.IndexOf("OrgAddressTh", StringComparison.OrdinalIgnoreCase) >= 0)
                    return el;
            }

            for (int i = 0; i < allDescendants.Count; i++)
            {
                string? name = null;
                try { name = allDescendants[i].Name; } catch { }
                if (!string.Equals(name, "ชื่อที่อยู่", StringComparison.Ordinal)) continue;

                for (int j = i + 1; j < Math.Min(i + 4, allDescendants.Count); j++)
                {
                    var candidate = allDescendants[j];
                    bool supportsValue = false;
                    try { supportsValue = candidate.Patterns.Value.IsSupported; } catch { }
                    if (supportsValue) return candidate;
                }
            }

            return null;
        }

        private void SetTextRobust(AutomationElement element, string? text, bool forceKeyboard = false)
        {
            string target = text ?? string.Empty;

            if (!forceKeyboard)
            {
                if (TrySetValueViaPattern(element, target)) return;
                if (TrySetValueViaLegacyPattern(element, target)) return;
                if (TrySetValueViaWinApi(element, target)) return;
            }

            try { element.Click(); } catch { }

            try
            {
                Keyboard.Press(VirtualKeyShort.CONTROL);
                Keyboard.Type(VirtualKeyShort.KEY_A);
                Keyboard.Release(VirtualKeyShort.CONTROL);
                Keyboard.Type(VirtualKeyShort.DELETE);

                if (!string.IsNullOrEmpty(target))
                {
                    Keyboard.Type(target);
                }
            }
            catch { }
        }

        private bool TrySetValueViaLegacyPattern(AutomationElement element, string text)
        {
            try
            {
                if (!element.Patterns.LegacyIAccessible.IsSupported) return false;
                element.Patterns.LegacyIAccessible.Pattern.SetValue(text ?? string.Empty);
                string? current = TryReadValue(element);
                return string.Equals((current ?? string.Empty).Trim(), (text ?? string.Empty).Trim(), StringComparison.Ordinal);
            }
            catch { return false; }
        }

        private bool TrySetValueViaWinApi(AutomationElement element, string text)
        {
            try
            {
                var handleProp = element.Properties.NativeWindowHandle;
                if (!handleProp.IsSupported) return false;
                IntPtr handle = handleProp.ValueOrDefault;
                if (handle == IntPtr.Zero) return false;

                SendMessage(handle, WM_SETTEXT, IntPtr.Zero, text ?? string.Empty);
                string? current = TryReadValue(element);
                return string.Equals((current ?? string.Empty).Trim(), (text ?? string.Empty).Trim(), StringComparison.Ordinal);
            }
            catch { return false; }
        }

        private bool TrySetValueViaPattern(AutomationElement element, string text)
        {
            try
            {
                if (!element.Patterns.Value.IsSupported) return false;
                element.Patterns.Value.Pattern.SetValue(text ?? string.Empty);
                string? current = element.Patterns.Value.Pattern.Value.Value;
                return string.Equals((current ?? string.Empty).Trim(), (text ?? string.Empty).Trim(), StringComparison.Ordinal);
            }
            catch { return false; }
        }

        private string? TryReadValue(AutomationElement element)
        {
            try
            {
                if (element.Patterns.Value.IsSupported)
                    return element.Patterns.Value.Pattern.Value.Value;
            }
            catch { }

            try { return element.AsTextBox()?.Text; } catch { }
            return null;
        }

        private bool TryOpenOrgMenu(string stage)
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
            var btnNew = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("New")), 3000)
                         ?? throw new Exception("หาปุ่ม New ไม่พบ");
            btnNew.Click();

            var form = Retry(FindOpenForm, 8000) ?? throw new Exception("หาฟอร์มไม่เจอ");

            WaitUntil(() =>
            {
                var txt = form.FindFirstDescendant(cf => cf.ByAutomationId(IdTxtOrgCode));
                return txt != null && !txt.Properties.IsOffscreen.ValueOrDefault;
            }, 3000);

            return form;
        }

        private Window GetOrOpenForm() => Retry(FindOpenForm, 1000) ?? OpenForm();

        private Window? FindOpenForm() =>
            _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault() ??
            _automation.GetDesktop().FindFirstChild(cf => cf.ByAutomationId(FormId))?.AsWindow();

        private Window ClickRowAndOpenEdit(string code)
        {
            var row = Retry(() => FindGridElement(code), 4000)
                      ?? throw new Exception($"หาแถว '{code}' ไม่เจอ");
            try { row.Focus(); } catch { }
            row.Click();

            var btn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("Edit")), 3000)
                      ?? throw new Exception("ไม่พบปุ่ม Edit");
            btn.Click();

            var form = Retry(FindOpenForm, 8000) ?? throw new Exception("หาฟอร์มไม่เจอ");

            WaitUntil(() =>
            {
                var txt = form.FindFirstDescendant(cf => cf.ByAutomationId(IdTxtOrgCode))?.AsTextBox();
                return txt != null && !string.IsNullOrEmpty(txt.Text);
            }, 3000);

            return form;
        }

        private bool TrySaveAndHandleDuplicate(Window form, string testName, string code, string saveBtn)
        {
            var btn = form.FindFirstDescendant(cf => cf.ByName(saveBtn)) ?? throw new Exception($"ไม่พบปุ่ม {saveBtn}");
            btn.Click();

            if (testName.Contains("Validation")) return true;

            var panel = FindTaskResultPanel(form, 2000);
            if (panel == null) return true;

            string? dup = DetectDuplicateMessage(GetPanelTexts(panel));
            if (dup == null) return true;

            LogFailure(testName, $"'{code}' ข้อมูลซ้ำ: {dup}");
            try { ClickCloseAndHandleDialog(form, "No", $"{testName}_Close"); } catch { }
            return false;
        }

        private static string? DetectDuplicateMessage(IEnumerable<string> texts)
        {
            if (texts.Any(t => t.Contains("รหัสองค์กร ห้ามซ้ำ"))) return "รหัสองค์กรซ้ำ";
            if (texts.Any(t => t.Contains("ชื่อองค์กร ห้ามซ้ำ"))) return "ชื่อองค์กรซ้ำ";
            return null;
        }

        private void FillSaveAssert(Window form, string code, string name, string address, string saveBtn, string testName, params string[] msgs)
        {
            SwitchToTab(form, GeneralTabId, "General");
            var ctx = form.FindFirstDescendant(cf => cf.ByAutomationId(GeneralTabId)) ?? form;
            FillTextBox(ctx, IdTxtOrgCode, code);
            FillTextBox(ctx, IdTxtOrgName, name);
            FillAddressTab(form, address);
            SwitchToTab(form, GeneralTabId, "General");

            form.FindFirstDescendant(cf => cf.ByName(saveBtn))?.Click();
            AssertTaskResult(form, msgs, testName);
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

        private void ClickCloseAndHandleDialog(Window form, string buttonToClick, string testName)
        {
            form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();

            var dialog = Retry(() => form.ModalWindows.FirstOrDefault()
                                     ?? _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault()
                                     ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("คำเตือน"))?.AsWindow(), 4000)
                         ?? throw new Exception("กด Close แล้วไม่พบ Pop-up");

            ClickDialogButton(dialog, buttonToClick);
            WaitForClosed(() => dialog);
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
                throw new Exception("ฟอร์มไม่ถูกปิด");
            LogSuccess(testName, "ฟอร์มถูกปิด");
        }

        private void RunDeleteCase(string baseName, string code, string btn, bool expectDeleted)
        {
            string testName = $"{baseName}_{code}";
            try
            {
                RefreshGrid();

                var elRow = Retry(() => FindGridElement(code), 4000)
                            ?? throw new Exception($"หาแถวรหัส '{code}' ในตารางไม่เจอ");

                try { elRow.Focus(); } catch { }
                elRow.Click();

                var delBtn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("Delete")), 3000)
                             ?? throw new Exception("หาปุ่ม Delete ไม่เจอ");
                delBtn.Click();

                var dlg = Retry(() => _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault()
                                      ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("คำเตือน"))?.AsWindow(), 4000)
                          ?? throw new Exception("กด Delete แล้วไม่พบ Pop-up ยืนยัน");

                ClickDialogButton(dlg, btn);
                WaitForClosed(() => dlg);

                RefreshGrid();
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


        // =========================================================================================
        // หมวดที่ 3: ส่วนการทำงานกลางที่ตายตัว (Common / Reusable Helpers)
        // =========================================================================================

        private readonly AutomationElement _mainWindow;
        private readonly UIA3Automation _automation;
        private int _totalSuccess = 0, _totalFail = 0;
        private const int TimeoutMs = 6000;

        public OrgInfoUC(AutomationElement mainWindow, UIA3Automation automation)
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
            return Retry(() => FindGridElement(val), timeoutMs) != null;
        }

        private AutomationElement? FindGridElement(string val)
        {
            var grid = _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataGrid))
                       ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Table));
            var searchContext = grid ?? _mainWindow;

            return searchContext.FindAllDescendants().FirstOrDefault(el =>
                (!string.IsNullOrEmpty(el.Name) && string.Equals(el.Name.Trim(), val, StringComparison.OrdinalIgnoreCase)) ||
                (el.Patterns.Value.IsSupported && string.Equals(el.Patterns.Value.Pattern.Value.Value?.Trim(), val, StringComparison.OrdinalIgnoreCase)));
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

        private void FillTextBox(AutomationElement context, string automationId, string text)
        {
            var txt = Retry(() => context.FindFirstDescendant(cf => cf.ByAutomationId(automationId))?.AsTextBox(), 3000)
                      ?? throw new Exception($"หาช่องกรอก '{automationId}' ไม่เจอ");
            txt.Focus();
            txt.Text = string.Empty;
            if (!string.IsNullOrEmpty(text))
            {
                txt.Enter(text);
            }
        }

        public void EnsureNoStrayModal(string contextName)
        {
            try
            {
                var modal = _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault()
                            ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("คำเตือน"))?.AsWindow();
                if (modal != null)
                {
                    var closeBtn = modal.FindFirstDescendant(cf => cf.ByName("Close") ?? cf.ByName("No") ?? cf.ByName("Cancel"));
                    closeBtn?.Click();
                    WaitForClosed(() => modal, 2000);
                }
            }
            catch { }
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