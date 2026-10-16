using FlaUI.Core;
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

namespace HRMITestFlaUI.Pages.Setup
{
    public class PermissionGroupInfoUC
    {
        // =========================================================================================
        // หมวดที่ 1: ข้อมูลการทดสอบทั้ง 5 ขั้น (Test Data)
        // =========================================================================================

        // --- Data Records สำหรับใช้งานในแต่ละขั้นตอน ---
        private sealed record PermissionGroupAddItem(
            string Code,
            string NameTh,
            string NameEn,
            string Remark);

        private sealed record ValidationItem(
            string TestCaseId,
            string Code,
            string NameTh,
            string SaveBtn,
            int TimeoutMs,
            string[] ExpectedErrors);

        private sealed record PermissionGroupEditItem(
            string TestCaseId,
            string OldCode,
            string NewCode,
            string NameTh,
            string NameEn,
            string Remark,
            string SaveBtn,
            bool NeedsClose);

        private sealed record DeleteItem(
            string TargetCode,
            string DialogBtn,
            bool ExpectDeleted);

        // [ขั้นที่ 1] ข้อมูลทดสอบการสร้างใหม่ (Add / Navigate)
        private static readonly PermissionGroupAddItem[] Step1_AddCases =
        {
            new("GRP001", "ผู้ดูแลระบบสูงสุด", "Super Administrator", "กลุ่มผู้ดูแลระบบทั้งหมด"),
            new("GRP002", "เจ้าหน้าที่ฝ่ายบุคคล", "HR Officer", "กลุ่มผู้ใช้งานสำหรับฝ่ายบุคคล"),
            new("GRP003", "ผู้จัดการฝ่าย", "Department Manager", "กลุ่มผู้จัดการ"),
            new("GRP004", "พนักงานทั่วไป", "General Employee", "กลุ่มพนักงานทั่วไป"),
            new("GRP005", "เจ้าหน้าที่ฝ่ายบัญชี", "Accounting Officer", "กลุ่มผู้ใช้งานสำหรับฝ่ายบัญชีและการเงิน")
        };

        // [ขั้นที่ 2] ข้อมูลทดสอบ Validation ตอนเพิ่ม (Add Validation)
        private const string CodeRequiredMsg = "รหัสกลุ่มผู้ใช้ ไม่สามารถเป็นค่าว่าง";
        private const string NameRequiredMsg = "ชื่อกลุ่มผู้ใช้ ไม่สามารถเป็นค่าว่าง";
        private const string CodeDuplicateMsg = "รหัสกลุ่มผู้ใช้ ห้ามซ้ำ";
        private const string NameDuplicateMsg = "ชื่อกลุ่มผู้ใช้ ห้ามซ้ำ";

        private static readonly ValidationItem[] Step2_AddValidationCases =
        {
            new("TestCase01_MissingGroupCode", "", "ผู้ดูแลระบบสูงสุด", "Save", 4000,
                new[] { CodeRequiredMsg }),

            new("TestCase02_MissingGroupName", "GRP006", "", "Save and New", 4000,
                new[] { NameRequiredMsg }),

            new("TestCase03_MissingCodeAndName", "", "", "Save and New", 4000,
                new[] { CodeRequiredMsg, NameRequiredMsg }),

            new("TestCase04_DuplicateGroupCode", "GRP002", "ผู้ดูแลทั่วไป", "Save", 4000,
                new[] { CodeDuplicateMsg }),

            new("TestCase05_DuplicateGroupName", "GRP006", "ผู้จัดการฝ่าย", "Save and Close", 5000,
                new[] { NameDuplicateMsg })
        };

        // [ขั้นที่ 3] ข้อมูลทดสอบการแก้ไขข้อมูล (Edit Records)
        private static readonly PermissionGroupEditItem[] Step3_EditCases =
        {
            new("TestCase06_EditRow1", "GRP001", "HRM01", "ผู้จัดการฝ่ายทรัพยากรบุคคล", "HR Manager", "แก้ไขกลุ่มผู้ดูแล", "Save", true),
            new("TestCase07_EditRow2", "GRP002", "ACC01", "ผู้ตรวจสอบบัญชี", "Auditor", "แก้ไขกลุ่ม HR", "Save and Close", false)
        };

        // [ขั้นที่ 4] ข้อมูลทดสอบ Validation ตอนแก้ไข (Edit Validation)
        private const string EditTargetRow = "HRM01";
        private const string RequiredSuffix = " กรุณาป้อนข้อมูล";

        private static readonly ValidationItem[] Step4_EditValidationCases =
        {
            new("TestCase08_EditValidation_MissingCode", "", "ผู้จัดการฝ่ายทรัพยากรบุคคล", "Save", 4000,
                new[] { $"{CodeRequiredMsg}{RequiredSuffix}" }),

            new("TestCase08_EditValidation_MissingName", "HRM01", "", "Save", 4000,
                new[] { $"{NameRequiredMsg}{RequiredSuffix}" }),

            new("TestCase08_EditValidation_MissingBoth", "", "", "Save", 4000,
                new[] { $"{CodeRequiredMsg}{RequiredSuffix}", $"{NameRequiredMsg}{RequiredSuffix}" }),

            new("TestCase08_EditValidation_DuplicateCode", "ACC01", "ผู้จัดการฝ่ายทรัพยากรบุคคล", "Save", 4000,
                new[] { $"{CodeDuplicateMsg}{RequiredSuffix}" }),

            new("TestCase08_EditValidation_DuplicateName", "HRM01", "ผู้จัดการฝ่าย", "Save and Close", 5000,
                new[] { $"{NameDuplicateMsg}{RequiredSuffix}" })
        };

        // [ขั้นที่ 5] ข้อมูลทดสอบการลบข้อมูล (Delete Records)
        private static readonly DeleteItem[] Step5_DeleteCases =
        {
            new("ACC01", "Yes", true),
            new("HRM01", "No", false)
        };


        // =========================================================================================
        // หมวดที่ 2: ส่วนที่ขึ้นกับหน้าจอนั้นๆ (Screen-Specific Implementation)
        // **เมื่อเปลี่ยนไปทำหน้าจออื่น ต้องมาแก้ไขค่าและเมทอดในส่วนนี้ทั้งหมด**
        // =========================================================================================

        // 2.1 Locator & Mappings ประจำหน้าจอ
        private const string ModuleName = "Setup";
        private const string ScreenNameTh = "กำหนดกลุ่มผู้ใช้";
        private const string PageName = "PermissionGroupInfoPage";
        private const string FormId = "emPermissionGroupInfoUC";
        private const string GeneralTabId = "devTabPgGeneral";

        // 2.2 ฟิลด์ Input ประจำหน้าจอ
        private const string IdGroupCode = "devTxtPermissionGroupCode";
        private const string IdGroupName = "devTxtPermissionGroupName";
        private const string IdGroupNameEng = "devTxtPermissionGroupNameEng";
        private const string IdRemark = "devMemoRemark";

        public void RunFullTestSuite()
        {
            _totalSuccess = 0;
            _totalFail = 0;

            TestLogger.SetContext(ModuleName);
            TestLogger.SetScreen(ScreenNameTh);

            NavigateToPermissionGroupInfoUC();
            RunAllValidationTests();
            RunEditTests();
            RunEditValidationTests();
            RunDeleteTests();
        }

        /// <summary>
        /// ขั้นที่ 1: เปิดหน้าจอและกรอกข้อมูลสร้างใหม่
        /// </summary>
        public void NavigateToPermissionGroupInfoUC()
        {
            string stage = "TestCase00_AddPermissionGroupData";
            TestLogger.LogInfo(PageName, "เริ่มทดสอบหน้าจอ Permission Group Setup Info");
            TestProgressHelper.Report(12, "Opening Setup -> Permission Group Info Menu via Search...");

            if (!TryOpenPermissionGroupMenu(stage)) return;

            Window? form = null;
            for (int i = 0; i < Step1_AddCases.Length; i++)
            {
                var g = Step1_AddCases[i];
                string testName = $"{stage}_{g.Code}";
                TestProgressHelper.Report(14 + (i * 3), $"Adding Permission Group ({i + 1}/{Step1_AddCases.Length}): {g.Code} - {g.NameTh}...");

                try
                {
                    form ??= GetOrOpenForm();
                    var tab = GetGeneralTab(form);
                    FillPermissionGroupFields(tab, g.Code, g.NameTh, g.NameEn, g.Remark);

                    string saveBtn = i == 0 ? "Save" : (i == Step1_AddCases.Length - 1 ? "Save and Close" : "Save and New");
                    if (!TrySaveAndHandleDuplicate(form, testName, g.Code, saveBtn))
                    {
                        form = null;
                        continue;
                    }

                    if (saveBtn == "Save and New")
                    {
                        WaitUntil(() => string.IsNullOrEmpty(tab.FindFirstDescendant(cf => cf.ByAutomationId(IdGroupCode))?.AsTextBox()?.Text), 3000);
                        LogSuccess(testName, $"'{g.Code}' บันทึกสำเร็จ (Save and New)");
                    }
                    else
                    {
                        if (saveBtn == "Save") form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();
                        WaitForClosed(() => FindOpenForm());
                        form = null;
                        LogGridResult(testName, g.Code);
                    }
                }
                catch (Exception ex)
                {
                    LogFailure(testName, $"'{g.Code}' เพิ่มไม่สำเร็จ: {ex.Message}");
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
            TestProgressHelper.Report(30, "Opening Permission Group Info Form for Validation Tests...");

            Window form;
            try { form = OpenForm(); }
            catch (Exception ex) { LogFailure(stage, $"เปิดฟอร์มไม่สำเร็จ: {ex.Message}"); return; }

            var tab = GetGeneralTab(form);

            foreach (var testCase in Step2_AddValidationCases.Take(4))
            {
                RunStep(testCase.TestCaseId, () =>
                    FillSaveAssert(tab, form, testCase.Code, testCase.NameTh, testCase.SaveBtn,
                        testCase.TestCaseId, testCase.TimeoutMs, testCase.ExpectedErrors));
            }

            var dupCase = Step2_AddValidationCases[4];
            RunStep(dupCase.TestCaseId, () =>
                FillSaveAssert(tab, form, dupCase.Code, dupCase.NameTh, dupCase.SaveBtn,
                    dupCase.TestCaseId, dupCase.TimeoutMs, dupCase.ExpectedErrors));

            RunCloseDialogSequence(form, dupCase.TestCaseId, NameDuplicateMsg);
        }

        /// <summary>
        /// ขั้นที่ 3: เลือกแก้ไขรายการในตาราง
        /// </summary>
        public void RunEditTests()
        {
            TestProgressHelper.Report(50, "Starting Edit Operations...");

            foreach (var e in Step3_EditCases)
            {
                try
                {
                    var form = ClickRowAndOpenEdit(e.OldCode);
                    var tab = GetGeneralTab(form);
                    FillPermissionGroupFields(tab, e.NewCode, e.NameTh, e.NameEn, e.Remark);

                    if (!TrySaveAndHandleDuplicate(form, e.TestCaseId, e.NewCode, e.SaveBtn)) continue;
                    if (e.NeedsClose) form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();

                    WaitForClosed(() => FindOpenForm());
                    RefreshGrid();

                    if (VerifyRecordInGrid(e.NewCode, refresh: false))
                        LogSuccess(e.TestCaseId, $"แก้ไข '{e.OldCode}' เป็น '{e.NewCode}' สำเร็จ (พบในตารางหลัก)");
                    else
                        LogFailure(e.TestCaseId, $"แก้ไข '{e.OldCode}' เป็น '{e.NewCode}' แล้วแต่ไม่พบในตารางหลัก");
                }
                catch (Exception ex)
                {
                    LogFailure(e.TestCaseId, $"แก้ไข '{e.OldCode}' ไม่สำเร็จ: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// ขั้นที่ 4: รัน Validation ตอนแก้ไขข้อมูล
        /// </summary>
        public void RunEditValidationTests()
        {
            const string baseTest = "TestCase08_EditValidation";

            Window form;
            try { form = ClickRowAndOpenEdit(EditTargetRow); }
            catch (Exception ex) { LogFailure(baseTest, $"เปิดฟอร์ม Edit ไม่สำเร็จ: {ex.Message}"); return; }

            var tab = GetGeneralTab(form);

            foreach (var testCase in Step4_EditValidationCases.Take(4))
            {
                RunStep(testCase.TestCaseId, () =>
                    FillSaveAssert(tab, form, testCase.Code, testCase.NameTh, testCase.SaveBtn,
                        testCase.TestCaseId, testCase.TimeoutMs, testCase.ExpectedErrors));
            }

            var dupEditCase = Step4_EditValidationCases[4];
            RunStep(dupEditCase.TestCaseId, () =>
                FillSaveAssert(tab, form, dupEditCase.Code, dupEditCase.NameTh, dupEditCase.SaveBtn,
                    dupEditCase.TestCaseId, dupEditCase.TimeoutMs, dupEditCase.ExpectedErrors));

            RunCloseDialogSequence(form, dupEditCase.TestCaseId, $"{NameDuplicateMsg}{RequiredSuffix}");

            LogSuccess(baseTest, "รัน Validation Edit ครบทุกเคสแล้ว");
        }

        /// <summary>
        /// ขั้นที่ 5: ทดสอบการลบรายการในตาราง
        /// </summary>
        public void RunDeleteTests()
        {
            const string baseTest = "TestCase09_Delete";

            foreach (var testCase in Step5_DeleteCases)
            {
                RunDeleteCase(baseTest, testCase.TargetCode, testCase.DialogBtn, testCase.ExpectDeleted);
            }

            var form = FindOpenForm();
            if (form != null)
            {
                form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();
                WaitForClosed(() => FindOpenForm(), 3000);
            }

            LogSuccess(baseTest, "ทดสอบ Delete ครบทุกเคส");
            TestLogger.LogInfo(PageName, $"สรุปผลรวมทั้งหมด: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");
        }

        // --- Custom Mapping & Control Logic ประจำหน้าจอ PermissionGroupInfoUC ---
        private void FillPermissionGroupFields(AutomationElement tab, string code, string nameTh, string nameEn, string remark)
        {
            SetText(tab, IdGroupCode, code);
            SetText(tab, IdGroupName, nameTh);
            SetText(tab, IdGroupNameEng, nameEn);
            SetText(tab, IdRemark, remark);
        }

        private Window OpenForm()
        {
            var newBtn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("New")), 3000)
                         ?? throw new Exception("หาปุ่ม New ไม่เจอ");
            newBtn.Click();

            var form = Retry(FindOpenForm, 8000) ?? throw new Exception("หาฟอร์มไม่เจอ");
            WaitUntil(() => form.FindFirstDescendant(cf => cf.ByAutomationId(IdGroupCode)) != null, 4000);
            return form;
        }

        private Window ClickRowAndOpenEdit(string code)
        {
            RefreshGrid();
            var row = Retry(() => FindElementMatchingValue(code), timeoutMs: 4000)
                    ?? throw new Exception($"หาแถว '{code}' ไม่เจอ");

            WaitUntil(() => { try { row.Focus(); return true; } catch { return false; } }, 1000);
            row.Click();

            var btn = Retry(() =>
            {
                var b = _mainWindow.FindFirstDescendant(cf => cf.ByName("Edit"));
                return (b != null && b.Properties.IsEnabled.ValueOrDefault) ? b : null;
            }, 3000) ?? throw new Exception("ไม่พบปุ่ม Edit");
            btn.Click();
            var form = Retry(FindOpenForm, 6000) ?? throw new Exception("หาฟอร์มไม่เจอ");
            WaitUntil(() => form.FindFirstDescendant(cf => cf.ByAutomationId(IdGroupCode)) != null, 3000);
            return form;
        }

        private void RunDeleteCase(string baseTest, string code, string btn, bool expectDeleted)
        {
            string testName = $"{baseTest}_{code}_{btn}";
            try
            {
                RefreshGrid();
                var elRow = Retry(() => FindElementMatchingValue(code), timeoutMs: 4000)
                            ?? throw new Exception($"หาแถวสำหรับลบ ({code}) ไม่เจอในตาราง");

                WaitUntil(() => { try { elRow.Focus(); return true; } catch { return false; } }, 1000);
                elRow.Click();

                var delBtn = Retry(() =>
                {
                    var b = _mainWindow.FindFirstDescendant(cf => cf.ByName("Delete"));
                    return (b != null && b.Properties.IsEnabled.ValueOrDefault) ? b : null;
                }, 3000) ?? throw new Exception("หาปุ่ม Delete ไม่เจอ");
                delBtn.Click();

                var dlg = Retry(() => _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault()
                                    ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("คำเตือน"))?.AsWindow(), 4000)
                        ?? throw new Exception("กด Delete แล้วไม่พบ Pop-up ยืนยัน");

                ClickDialogButton(dlg, btn);
                WaitForClosed(() => dlg);

                bool exists = VerifyRecordInGrid(code, timeoutMs: 3000, refresh: false);
                if (expectDeleted)
                {
                    if (exists) { LogFailure(testName, $"'{code}' ลบข้อมูลไม่สำเร็จ"); throw new Exception($"'{code}' ยังคงอยู่หลังกดลบ"); }
                    LogSuccess(testName, $"'{code}' ถูกลบสำเร็จ");
                }
                else
                {
                    if (!exists) { LogFailure(testName, $"'{code}' ลบข้อมูลผิดพลาด"); throw new Exception($"'{code}' หายไปทั้งที่กดยกเลิก (No)"); }
                    LogSuccess(testName, $"'{code}' ข้อมูลยังไม่ถูกลบ");
                }
            }
            catch (Exception ex) { LogFailure(testName, $"ลบ '{code}' ไม่สำเร็จ: {ex.Message}"); }
        }

        private bool TrySaveAndHandleDuplicate(Window form, string testName, string code, string saveBtn)
        {
            var btn = form.FindFirstDescendant(cf => cf.ByName(saveBtn)) ?? throw new Exception($"ไม่พบปุ่ม {saveBtn}");
            btn.Click();

            if (testName.Contains("Validation") || testName.Contains("Duplicate")) return true;

            var panel = FindTaskResultPanel(form, 1500);
            if (panel == null) return true;

            var texts = GetPanelTexts(panel);
            string? dup = texts.Any(t => t.Contains(CodeDuplicateMsg)) ? "รหัสกลุ่มผู้ใช้ซ้ำ" :
                          texts.Any(t => t.Contains(NameDuplicateMsg)) ? "ชื่อกลุ่มผู้ใช้ซ้ำ" : null;

            if (dup == null) return true;

            LogFailure(testName, $"'{code}' รหัสกลุ่มผู้ใช้ซ้ำ/ชื่อกลุ่มผู้ใช้ซ้ำ: {dup}");
            try { ClickCloseAndHandleDialog(form, "No", $"{testName}_Close"); } catch { }
            return false;
        }

        private void FillSaveAssert(AutomationElement tab, Window form, string code, string name, string saveBtn, string testName, int timeoutMs, params string[] msgs)
        {
            SetText(tab, IdGroupCode, code);
            SetText(tab, IdGroupName, name);

            WaitUntil(() => { try { form.Focus(); return true; } catch { return false; } }, 1000);

            form.FindFirstDescendant(cf => cf.ByName(saveBtn))?.Click();
            AssertTaskResultCustom(form, msgs, testName, timeoutMs);
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

        public PermissionGroupInfoUC(AutomationElement mainWindow, UIA3Automation automation)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

        private bool TryOpenPermissionGroupMenu(string stage)
        {
            try
            {
                var searchBox = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("baritemSearch"))
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)), 4000)
                                ?? throw new Exception("ไม่พบช่องค้นหา baritemSearch");

                WaitUntil(() => { try { searchBox.Focus(); return true; } catch { return false; } }, 1000);
                string searchKeyword = ScreenNameTh;
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

        private Window GetOrOpenForm() => Retry(FindOpenForm, 1500) ?? OpenForm();

        private Window? FindOpenForm() =>
            _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault(w => w.AutomationId == FormId || w.Name.Contains(ScreenNameTh)) ??
            _automation.GetDesktop().FindFirstChild(cf => cf.ByAutomationId(FormId).Or(cf.ByName(ScreenNameTh)))?.AsWindow();

        private AutomationElement GetGeneralTab(Window form) =>
            Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(GeneralTabId)), 3000) ?? form;

        private void SetText(AutomationElement tab, string id, string text)
        {
            var el = Retry(() => tab.FindFirstDescendant(cf => cf.ByAutomationId(id)), 3000) ?? throw new Exception($"ไม่พบช่อง {id}");

            WaitUntil(() => { try { el.Focus(); return true; } catch { return false; } }, 1000);

            if (el.Patterns.Value.IsSupported)
            {
                try
                {
                    el.Patterns.Value.Pattern.SetValue(text ?? "");
                    return;
                }
                catch { }
            }

            el.Click();

            Keyboard.Press(VirtualKeyShort.CONTROL);
            Keyboard.Type("a");
            Keyboard.Release(VirtualKeyShort.CONTROL);
            Keyboard.Press(VirtualKeyShort.BACK);
            Keyboard.Press(VirtualKeyShort.DELETE);

            if (!string.IsNullOrEmpty(text))
            {
                Keyboard.Type(text);
            }
        }

        private void RunCloseDialogSequence(Window form, string prefix, string expectedMsg)
        {
            string ySub = $"{prefix}_Close_Yes";
            ClickCloseAndHandleDialog(form, "Yes", ySub);
            AssertWarningAndFormOpen(form, expectedMsg, ySub, 4000);

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
                                   ?? dlg.FindFirstDescendant(cf => cf.ByName($"&{name}"))?.AsButton()
                                   ?? dlg.FindFirstDescendant(cf => cf.ByName(name == "Yes" ? "ใช่" : "ไม่ใช่"))?.AsButton(), 2000)
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
            if (!WaitForClosed(() => FindOpenForm(), 3000))
                throw new Exception("ปิดฟอร์มไม่สำเร็จ");
            LogSuccess(testName, "ฟอร์มถูกปิด");
        }

        private void AssertWarningAndFormOpen(Window form, string expectedMsg, string testName, int timeoutMs)
        {
            bool foundMessage = false;
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                var panel = FindTaskResultPanel(form, 600);
                if (panel != null)
                {
                    var texts = GetPanelTexts(panel).ToList();
                    if (texts.Any(t => t.Contains(expectedMsg) || (expectedMsg.Contains(" ") && t.Contains(expectedMsg.Split(' ')[0]))))
                    {
                        foundMessage = true;
                        break;
                    }
                }
            }

            bool isOpen = WaitUntil(() => !form.Properties.IsOffscreen.ValueOrDefault && form.IsAvailable, 2000);
            if (!foundMessage || !isOpen)
                throw new Exception($"ตรวจสอบไม่ผ่าน (พบข้อความ: {foundMessage}, ฟอร์มเปิดอยู่: {isOpen})");

            LogSuccess(testName, $"พบข้อความ: '{expectedMsg}' และ ฟอร์มถูกเปิดอยู่");
        }

        private void AssertTaskResultCustom(Window form, string[] msgs, string testName, int timeoutMs)
        {
            var pending = new HashSet<string>(msgs);
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < timeoutMs && pending.Count > 0)
            {
                var panel = FindTaskResultPanel(form, 600);
                if (panel != null)
                {
                    var texts = GetPanelTexts(panel).ToList();
                    foreach (var m in pending.ToList())
                    {
                        if (texts.Any(t => t.Contains(m) || (m.Contains(" ") && t.Contains(m.Split(' ')[0]))))
                            pending.Remove(m);
                    }
                }
                if (pending.Count == 0) break;
            }

            if (pending.Count > 0) throw new Exception($"ไม่พบข้อความ: '{string.Join("', '", pending)}'");

            string formattedList = string.Join(",", msgs.Select(m => $"'{m}'"));
            LogSuccess(testName, $"พบข้อความ: {formattedList}");
        }

        private AutomationElement? FindTaskResultPanel(Window form, int timeoutMs) =>
            Retry(() => form.FindFirstDescendant(cf => cf.ByName("Task Result").Or(cf.ByAutomationId("dockPanelTaskResult")))
                       ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("Task Result").Or(cf.ByAutomationId("dockPanelTaskResult"))), timeoutMs);

        private static IEnumerable<string> GetPanelTexts(AutomationElement panel)
        {
            foreach (var el in panel.FindAllDescendants())
            {
                if (!string.IsNullOrEmpty(el.Name)) yield return el.Name;
                string? value = null;
                try { if (el.Patterns.Value.IsSupported) value = el.Patterns.Value.Pattern.Value.Value; } catch { }
                if (!string.IsNullOrEmpty(value)) yield return value;
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
            return Retry(() => _mainWindow.FindAllDescendants().FirstOrDefault(el =>
                (!string.IsNullOrEmpty(el.Name) && string.Equals(el.Name.Trim(), val, StringComparison.OrdinalIgnoreCase)) ||
                (el.Patterns.Value.IsSupported && string.Equals(el.Patterns.Value.Pattern.Value.Value?.Trim(), val, StringComparison.OrdinalIgnoreCase))), timeoutMs) != null;
        }

        private AutomationElement? FindElementMatchingValue(string value)
        {
            foreach (var el in _mainWindow.FindAllDescendants())
            {
                if (!string.IsNullOrEmpty(el.Name) && el.Name.Contains(value))
                    return el;
                try
                {
                    if (el.Patterns.Value.IsSupported && el.Patterns.Value.Pattern.Value.Value.Contains(value))
                        return el;
                }
                catch { }
            }
            return null;
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

        private static T? Retry<T>(Func<T?> finder, int timeoutMs = TimeoutMs) where T : class
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
    }
}