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

namespace HRMITestFlaUI.Pages.Setup
{
    public class BankInfoUC
    {
        // =========================================================================================
        // หมวดที่ 1: ข้อมูลการทดสอบทั้ง 5 ขั้น (Test Data)
        // =========================================================================================

        // --- Data Records สำหรับใช้งานในแต่ละเฟส ---
        private sealed record AddItem(
            string Code,
            string NameTh,
            string NameEn,
            string Short,
            string SaveBtn,
            bool NeedsManualClose);
        private sealed record ValidationItem(
            string TestCaseId,
            string Code,
            string NameTh,
            string SaveBtn,
            int Progress,
            string ProgressMsg,
            string[] ExpectedErrors);
        private sealed record EditItem(
            string Name,
            string OldCode,
            string NewCode,
            string NameTh,
            string NameEn,
            string Short,
            string SaveBtn,
            bool NeedsManualClose);
        private sealed record DeleteItem(
            string TargetCode,
            string DialogBtn,
            bool ExpectDeleted,
            int Progress,
            string ProgressMsg);

        // [ขั้นที่ 1] ข้อมูลทดสอบการสร้างใหม่ (Add / Navigate)
        private static readonly AddItem[] Step1_AddCases =
        {
            new(
                "BANK001",
                "ธนาคารกรุงศรีอยุธยา",
                "Bank of Ayudhya",
                "BAY",
                "Save",
                true),
            new(
                "BANK002",
                "ธนาคารกรุงเทพ",
                "Bangkok Bank",
                "BBL",
                "Save and New",
                false),
            new(
                "BANK003",
                "ธนาคารมิซูโฮะ",
                "MIZUHO CORPORATE BANK",
                "LTD",
                "Save and New",
                false),
            new(
                "BANK004",
                "ธนาคารกรุงไทย",
                "Krung Thai Bank",
                "KTB",
                "Save and New",
                false),
            new(
                "BANK005",
                "ธนาคารซูมิโตโม",
                "Sumitomo Mitsui Banking Corporation ",
                "SMBC",
                "Save and Close",
                false)
        };

        // [ขั้นที่ 2] ข้อมูลทดสอบ Validation ตอนเพิ่ม (Add Validation)
        private static readonly ValidationItem[] Step2_AddValidationCases =
        {
            new("TestCase01_MissingBankCode", "", "ธนาคารกรุงศรีอยุธยา", "Save", 25,
                "Validating: Missing Bank Code...", new[] { "รหัสธนาคาร ไม่สามารถเป็นค่าว่าง" }),

            new("TestCase02_MissingBankName", "BANK002", "", "Save and New", 28,
                "Validating: Missing Bank Name...", new[] { "ชื่อธนาคาร ไม่สามารถเป็นค่าว่าง" }),

            new("TestCase03_MissingBankCodeAndName", "", "", "Save and New", 31,
                "Validating: Missing Code and Name...", new[] { "รหัสธนาคาร ไม่สามารถเป็นค่าว่าง", "ชื่อธนาคาร ไม่สามารถเป็นค่าว่าง" }),

            new("TestCase04_DuplicateBankCode", "BANK001", "ธนาคารกรุงศรีอยุธยา", "Save", 34,
                "Validating: Duplicate Bank Code...", new[] { "รหัสธนาคาร ห้ามซ้ำ" }),

            new("TestCase05_DuplicateBankName", "BANK006", "ธนาคารเอเชีย", "Save and Close", 37,
                "Validating: Duplicate Bank Name...", new[] { "ชื่อธนาคาร ห้ามซ้ำ" })
        };

        // [ขั้นที่ 3] ข้อมูลทดสอบการแก้ไขข้อมูล (Edit Records)
        private static readonly EditItem[] Step3_EditCases =
        {
            new(
                "TestCase06_EditRow1",
                "BANK001",
                "BK01",
                "ธนาคารนครหลวงไทย",
                "Siam City Bank",
                "SCIB",
                "Save",
                true),
            new(
                "TestCase07_EditRow2",
                "BANK002",
                "BK02",
                "ธนาคารธนชาต",
                "Thanachart Bank",
                "TBANK",
                "Save and Close",
                false)
        };

        // [ขั้นที่ 4] ข้อมูลทดสอบ Validation ตอนแก้ไข (Edit Validation)
        private const string EditTargetRow = "BK01";
        private const string EditValidationSuffix = " กรุณาป้อนข้อมูล";
        private static readonly ValidationItem[] Step4_EditValidationCases =
        {
            new("TestCase08_EditValidation_MissingCode", "", "ธนาคารกรุงศรีอยุธยา", "Save", 62,
                "Validating Edit: Missing Code...", new[] { "รหัสธนาคาร ไม่สามารถเป็นค่าว่าง" + EditValidationSuffix }),

            new("TestCase08_EditValidation_MissingName", "BK01", "", "Save and New", 66,
                "Validating Edit: Missing Name...", new[] { "ชื่อธนาคาร ไม่สามารถเป็นค่าว่าง" + EditValidationSuffix }),

            new("TestCase08_EditValidation_MissingBoth", "", "", "Save", 70,
                "Validating Edit: Missing Both...", new[] { "รหัสธนาคาร ไม่สามารถเป็นค่าว่าง" + EditValidationSuffix, "ชื่อธนาคาร ไม่สามารถเป็นค่าว่าง" + EditValidationSuffix }),

            new("TestCase08_EditValidation_DuplicateCode", "BK02", "ธนาคารธนชาต", "Save and New", 74,
                "Validating Edit: Duplicate Code...", new[] { "รหัสธนาคาร ห้ามซ้ำ" + EditValidationSuffix }),

            new("TestCase08_EditValidation_DuplicateName", "BK03", "ธนาคารมิซูโฮะ", "Save and Close", 78,
                "Validating Edit: Duplicate Name...", new[] { "ชื่อธนาคาร ห้ามซ้ำ" + EditValidationSuffix })
        };

        // [ขั้นที่ 5] ข้อมูลทดสอบการลบข้อมูล (Delete Records)
        private static readonly DeleteItem[] Step5_DeleteCases =
        {
            new("BK02", "Yes", true, 92, "Delete Test: BK02 (confirmed delete)..."),
            new("BK01", "No", false, 100, "Delete Test: BK01 (cancelled delete)...")
        };


        // =========================================================================================
        // หมวดที่ 2: ส่วนที่ขึ้นกับหน้าจอนั้นๆ (Screen-Specific Implementation)
        // **เมื่อเปลี่ยนไปทำหน้าจออื่น ต้องมาแก้ไขค่าและเมทอดในส่วนนี้ทั้งหมด**
        // =========================================================================================

        // 2.1 Locator & Mappings ประจำหน้าจอ
        private const string MenuName = "กำหนดรหัสธนาคาร";
        private const string ScreenLogName = "BankSetupInfoPage";
        private const string FormId = "BankInfoUC";
        private const string GeneralTabId = "devTabPgGeneral";
        private const string FirstObjectID = "devtxtBankCode";

        // 2.2 ฟิลด์ Input ประจำหน้าจอ
        private const string CodeFieldId = "devtxtBankCode";
        private const string NameThFieldId = "devtxtBankName";
        private const string NameEnFieldId = "devtxtBankNameEng";
        private const string ShortNameFieldId = "devtxtBankShort";

        /// <summary>
        /// ขั้นที่ 1: เปิดหน้าจอและกรอกข้อมูลสร้างใหม่
        /// </summary>
        public void NavigateToBankInfoUC()
        {
            const string stageName = "TestCase00_AddBankData";
            TestLogger.LogInfo(ScreenLogName, $"เริ่มทดสอบหน้าจอ {MenuName}");
            TestProgressHelper.Report(2, $"Opening Setup -> {MenuName} Menu via Search...");

            if (!TryOpenMenu(stageName, MenuName)) return;

            Window? form = null;
            for (int i = 0; i < Step1_AddCases.Length; i++)
            {
                var item = Step1_AddCases[i];
                string testName = $"{stageName}_{item.Code}";
                TestProgressHelper.Report(4 + (i * 4), $"Adding {MenuName} ({i + 1}/{Step1_AddCases.Length}): {item.Code} - {item.NameTh}...");

                try
                {
                    form ??= GetOrOpenForm();
                    var tab = GetGeneralTab(form);
                    FillFormData(tab, item.Code, item.NameTh, item.NameEn, item.Short);

                    if (!TrySaveAndHandleDuplicate(form, testName, item.Code, item.SaveBtn))
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
            const string stage = "TestCase01-05_Validation";
            TestProgressHelper.Report(22, $"Opening {MenuName} Form for Validation Tests...");

            Window form;
            try { form = OpenForm(); }
            catch (Exception ex) { LogFailure(stage, $"เปิดฟอร์มไม่สำเร็จ: {ex.Message}"); return; }

            var tab = GetGeneralTab(form);

            foreach (var testCase in Step2_AddValidationCases)
            {
                RunStep(testCase.TestCaseId,
                    () => FillValidationAndAssert(tab, form, testCase.Code, testCase.NameTh, testCase.SaveBtn, testCase.TestCaseId, testCase.ExpectedErrors),
                    testCase.Progress, testCase.ProgressMsg);
            }

            // เคสเสริมเฉพาะหน้าจอ: พฤติกรรมเมื่อปิด Dialog ข้อความเตือน
            const string dupName = "TestCase05_DuplicateBankName";
            RunStep($"{dupName}_CloseSequence",
                () => RunCloseDialogSequence(form, dupName, "ชื่อธนาคาร ห้ามซ้ำ"),
                40, "Validating: Duplicate Name Close Sequence...");
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
                    FillFormData(tab, item.NewCode, item.NameTh, item.NameEn, item.Short);

                    if (!TrySaveAndHandleDuplicate(form, item.Name, item.NewCode, item.SaveBtn)) continue;
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
                    () => FillValidationAndAssert(tab, form, testCase.Code, testCase.NameTh, testCase.SaveBtn, testCase.TestCaseId, testCase.ExpectedErrors),
                    testCase.Progress, testCase.ProgressMsg);
            }

            const string dupSub = $"{baseName}_DuplicateName";
            RunStep($"{dupSub}_CloseSequence",
                () => RunCloseDialogSequence(form, dupSub, "ชื่อธนาคาร ห้ามซ้ำ" + EditValidationSuffix),
                80, "Validating Edit: Duplicate Name Close Sequence...");

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
        private void FillFormData(AutomationElement tab, string code, string nameTh, string nameEn, string sName)
        {
            SetText(tab, CodeFieldId, code);
            SetText(tab, NameThFieldId, nameTh);
            SetText(tab, NameEnFieldId, nameEn);
            SetText(tab, ShortNameFieldId, sName);
        }

        private void FillValidationAndAssert(AutomationElement tab, Window form, string code, string name, string saveBtn, string testName, params string[] msgs)
        {
            SetText(tab, CodeFieldId, code);
            SetText(tab, NameThFieldId, name);
            form.FindFirstDescendant(cf => cf.ByName(saveBtn))?.Click();
            AssertTaskResult(form, msgs, testName);
        }

        private static string? DetectDuplicateMessage(IEnumerable<string> texts)
        {
            if (texts.Any(t => t.Contains("รหัสบัญชี ห้ามซ้ำ") || t.Contains("รหัสธนาคาร ห้ามซ้ำ"))) return "รหัสซ้ำ";
            if (texts.Any(t => t.Contains("ชื่อบัญชี ห้ามซ้ำ") || t.Contains("ชื่อธนาคาร ห้ามซ้ำ"))) return "ชื่อซ้ำ";
            return null;
        }

        private AutomationElement GetGeneralTab(Window form) =>
            Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(GeneralTabId)), 3000) ?? form;

        private Window? FindOpenForm() =>
            _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault() ??
            _automation.GetDesktop().FindFirstChild(cf => cf.ByAutomationId(FormId))?.AsWindow();


        // =========================================================================================
        // หมวดที่ 3: ส่วนการทำงานกลางที่ตายตัว (Common / Reusable Helpers)
        // **ใช้งานร่วมกันได้ทุกหน้าจอ ไม่ต้องแก้ไขคำสั่งเมื่อเปลี่ยนหน้า**
        // =========================================================================================

        private readonly AutomationElement _mainWindow;
        private readonly UIA3Automation _automation;
        private int _totalSuccess = 0;
        private int _totalFail = 0;
        private const int TimeoutMs = 6000;

        public BankInfoUC(AutomationElement mainWindow, UIA3Automation automation)
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