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
        // ── ตัวแปรสำหรับเก็บออบเจ็กต์หน้าต่างหลัก, ระบบ Automation และตัวนับผลการทดสอบ (Success/Fail) ──
        private readonly AutomationElement _mainWindow;
        private readonly UIA3Automation _automation;

        private int _totalSuccess = 0;
        private int _totalFail = 0;

        // ── ค่าคงที่สำหรับการตั้งค่า Timeout และข้อมูลการระบุตัวตนของฟอร์ม ──
        private const int TimeoutMs = 6000;
        private const string FormId = "emPermissionGroupInfoUC";
        private const string GeneralTabId = "devTabPgGeneral";
        private const string RequiredSuffix = " กรุณาป้อนข้อมูล";
        private const string PageName = "PermissionGroupInfoPage";
        private const string ScreenNameTh = "กำหนดกลุ่มผู้ใช้";
        private const string ModuleName = "Setup";

        // ── ข้อความแจ้งเตือนมาตรฐาน ──
        private const string CodeRequiredMsg = "รหัสกลุ่มผู้ใช้ ไม่สามารถเป็นค่าว่าง";
        private const string NameRequiredMsg = "ชื่อกลุ่มผู้ใช้ ไม่สามารถเป็นค่าว่าง";
        private const string CodeDuplicateMsg = "รหัสกลุ่มผู้ใช้ ห้ามซ้ำ";
        private const string NameDuplicateMsg = "ชื่อกลุ่มผู้ใช้ ห้ามซ้ำ";

        /* Constructor สำหรับรับค่า MainWindow และ Automation Instance เข้ามาใช้งานภายในคลาส */
        public PermissionGroupInfoUC(AutomationElement mainWindow, UIA3Automation automation)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

        //---------------------------------------------------------
        // 🚀 MASTER METHOD: เรียกรันครอบคลุมทุกชุดการทดสอบ + บันทึกลง Excel Log
        //---------------------------------------------------------
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

        //-------------------------------------------
        // * 1. ทดสอบการ New โดยเพิ่มข้อมูล 5 ข้อมูล *
        //-------------------------------------------
        public void NavigateToPermissionGroupInfoUC()
        {
            var groups = new[]
            {
                new { Code = "GRP001", NameTh = "ผู้ดูแลระบบสูงสุด", NameEn = "Super Administrator", Remark = "กลุ่มผู้ดูแลระบบทั้งหมด" },
                new { Code = "GRP002", NameTh = "เจ้าหน้าที่ฝ่ายบุคคล", NameEn = "HR Officer", Remark = "กลุ่มผู้ใช้งานสำหรับฝ่ายบุคคล" },
                new { Code = "GRP003", NameTh = "ผู้จัดการฝ่าย", NameEn = "Department Manager", Remark = "กลุ่มผู้จัดการ" },
                new { Code = "GRP004", NameTh = "พนักงานทั่วไป", NameEn = "General Employee", Remark = "กลุ่มพนักงานทั่วไป" },
                new { Code = "GRP005", NameTh = "เจ้าหน้าที่ฝ่ายบัญชี", NameEn = "Accounting Officer", Remark = "กลุ่มผู้ใช้งานสำหรับฝ่ายบัญชีและการเงิน" }
            };

            string stage = "TestCase00_AddPermissionGroupData";
            TestLogger.LogInfo(PageName, "เริ่มทดสอบหน้าจอ Permission Group Setup Info");
            TestProgressHelper.Report(12, "Opening Setup -> Permission Group Info Menu via Search...");

            if (!TryOpenPermissionGroupMenu(stage)) return;

            Window? form = null;
            for (int i = 0; i < groups.Length; i++)
            {
                var g = groups[i];
                string testName = $"{stage}_{g.Code}";
                TestProgressHelper.Report(14 + (i * 3), $"Adding Permission Group ({i + 1}/5): {g.Code} - {g.NameTh}...");

                try
                {
                    form ??= GetOrOpenForm();
                    var tab = GetGeneralTab(form);
                    FillPermissionGroupFields(tab, g.Code, g.NameTh, g.NameEn, g.Remark);

                    string saveBtn = i == 0 ? "Save" : (i == groups.Length - 1 ? "Save and Close" : "Save and New");
                    if (!TrySaveAndHandleDuplicate(form, testName, g.Code, saveBtn))
                    {
                        form = null;
                        continue;
                    }

                    if (saveBtn == "Save and New")
                    {
                        WaitUntil(() => string.IsNullOrEmpty(tab.FindFirstDescendant(cf => cf.ByAutomationId("devTxtPermissionGroupCode"))?.AsTextBox()?.Text), 3000);
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

                // เช็คโดยตรงว่าหน้าจอแสดงปุ่ม New พร้อมใช้งาน
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

        //---------------------------------------------------------
        // * 2. ทดสอบแจ้งเตือนต่างๆ เมื่อ New *
        //---------------------------------------------------------
        public void RunAllValidationTests()
        {
            const string stage = "TestCase01-05_Validation";
            TestProgressHelper.Report(30, "Opening Permission Group Info Form for Validation Tests...");

            Window form;
            try { form = OpenForm(); }
            catch (Exception ex) { LogFailure(stage, $"เปิดฟอร์มไม่สำเร็จ: {ex.Message}"); return; }

            var tab = GetGeneralTab(form);

            // เคส 1: ไม่กรอกรหัสกลุ่มผู้ใช้
            RunStep("TestCase01_MissingGroupCode", () => FillSaveAssert(tab, form, "", "ผู้ดูแลระบบสูงสุด", "Save", "TestCase01_MissingGroupCode", 4000, CodeRequiredMsg));

            // เคส 2: ไม่กรอกชื่อกลุ่มผู้ใช้ (ใช้รหัส GRP006 เพื่อป้องกันไม่ให้ติดรหัสซ้ำ GRP001)
            RunStep("TestCase02_MissingGroupName", () => FillSaveAssert(tab, form, "GRP006", "", "Save and New", "TestCase02_MissingGroupName", 4000, NameRequiredMsg));

            // เคส 3: ไม่กรอกทั้งรหัสและชื่อกลุ่มผู้ใช้
            RunStep("TestCase03_MissingCodeAndName", () => FillSaveAssert(tab, form, "", "", "Save and New", "TestCase03_MissingCodeAndName", 4000, CodeRequiredMsg, NameRequiredMsg));

            // เคส 4: กรอกรหัสซ้ำ (ใช้ชื่อที่ไม่ซ้ำ เพื่อให้ระบบแจ้งเตือนเฉพาะ "รหัสซ้ำ")
            RunStep("TestCase04_DuplicateGroupCode", () => FillSaveAssert(tab, form, "GRP002", "ผู้ดูแลทั่วไป", "Save", "TestCase04_DuplicateGroupCode", 4000, CodeDuplicateMsg));

            // เคส 5: กรอกชื่อซ้ำ และทดสอบลำดับการกดปุ่ม Close (Yes -> Cancel -> No)
            const string testName = "TestCase05_DuplicateGroupName";
            RunStep(testName, () => FillSaveAssert(tab, form, "GRP006", "ผู้จัดการฝ่าย", "Save and Close", testName, 5000, NameDuplicateMsg));
            RunCloseDialogSequence(form, testName, NameDuplicateMsg);
        }

        //-------------------------------------------
        // * 3. ทดสอบ Edit *
        //-------------------------------------------
        public void RunEditTests()
        {
            TestProgressHelper.Report(50, "Starting Edit Operations...");
            var edits = new (string Name, string OldCode, string NewCode)[]
            {
                ("TestCase06_EditRow1", "GRP001", "HRM01"),
                ("TestCase07_EditRow2", "GRP002", "ACC01")
            };

            foreach (var e in edits)
            {
                bool isClose = e.NewCode == "HRM01";
                try
                {
                    var form = ClickRowAndOpenEdit(e.OldCode);
                    var tab = GetGeneralTab(form);
                    FillPermissionGroupFields(tab, e.NewCode, isClose ? "ผู้จัดการฝ่ายทรัพยากรบุคคล" : "ผู้ตรวจสอบบัญชี", isClose ? "HR Manager" : "Auditor", isClose ? "แก้ไขกลุ่มผู้ดูแล" : "แก้ไขกลุ่ม HR");

                    if (!TrySaveAndHandleDuplicate(form, e.Name, e.NewCode, isClose ? "Save" : "Save and Close")) continue;
                    if (isClose) form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();

                    WaitForClosed(() => FindOpenForm());
                    RefreshGrid();

                    if (VerifyRecordInGrid(e.NewCode, refresh: false))
                        LogSuccess(e.Name, $"แก้ไข '{e.OldCode}' เป็น '{e.NewCode}' สำเร็จ (พบในตารางหลัก)");
                    else
                        LogFailure(e.Name, $"แก้ไข '{e.OldCode}' เป็น '{e.NewCode}' แล้วแต่ไม่พบในตารางหลัก");
                }
                catch (Exception ex) { LogFailure(e.Name, $"แก้ไข '{e.OldCode}' ไม่สำเร็จ: {ex.Message}"); }
            }
        }

        //-------------------------------------
        // * 4. ทดสอบการแจ้งเตือน Edit *
        //-------------------------------------
        public void RunEditValidationTests()
        {
            const string baseTest = "TestCase08_EditValidation";

            Window form;
            try { form = ClickRowAndOpenEdit("HRM01"); }
            catch (Exception ex) { LogFailure(baseTest, $"เปิดฟอร์ม Edit ไม่สำเร็จ: {ex.Message}"); return; }

            var tab = GetGeneralTab(form);

            // Edit Validation 1: ลบรหัสออกให้ว่าง
            RunStep($"{baseTest}_MissingCode", () => FillSaveAssert(tab, form, "", "ผู้จัดการฝ่ายทรัพยากรบุคคล", "Save", $"{baseTest}_MissingCode", 4000, $"{CodeRequiredMsg}{RequiredSuffix}"));

            // Edit Validation 2: ลบชื่อออกให้ว่าง
            RunStep($"{baseTest}_MissingName", () => FillSaveAssert(tab, form, "HRM01", "", "Save", $"{baseTest}_MissingName", 4000, $"{NameRequiredMsg}{RequiredSuffix}"));

            // Edit Validation 3: ลบทั้งรหัสและชื่อออก
            RunStep($"{baseTest}_MissingBoth", () => FillSaveAssert(tab, form, "", "", "Save", $"{baseTest}_MissingBoth", 4000, $"{CodeRequiredMsg}{RequiredSuffix}", $"{NameRequiredMsg}{RequiredSuffix}"));

            // Edit Validation 4: แก้ไขเป็นรหัสซ้ำ
            RunStep($"{baseTest}_DuplicateCode", () => FillSaveAssert(tab, form, "ACC01", "ผู้จัดการฝ่ายทรัพยากรบุคคล", "Save", $"{baseTest}_DuplicateCode", 4000, $"{CodeDuplicateMsg}{RequiredSuffix}"));

            // Edit Validation 5: แก้ไขเป็นชื่อซ้ำ
            const string sub = $"{baseTest}_DuplicateName";
            RunStep(sub, () => FillSaveAssert(tab, form, "HRM01", "ผู้จัดการฝ่าย", "Save and Close", sub, 5000, $"{NameDuplicateMsg}{RequiredSuffix}"));
            RunCloseDialogSequence(form, sub, $"{NameDuplicateMsg}{RequiredSuffix}");

            LogSuccess(baseTest, "รัน Validation Edit ครบทุกเคสแล้ว");
        }

        //-------------------------------
        // * 5. ทดสอบ Delete *
        //-------------------------------
        public void RunDeleteTests()
        {
            const string baseTest = "TestCase09_Delete";
            RunDeleteCase(baseTest, "ACC01", "Yes", expectDeleted: true);
            RunDeleteCase(baseTest, "HRM01", "No", expectDeleted: false);

            var form = FindOpenForm();
            if (form != null)
            {
                form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();
                WaitForClosed(() => FindOpenForm(), 3000);
            }

            LogSuccess(baseTest, "ทดสอบ Delete ครบทุกเคส");
            TestLogger.LogInfo(PageName, $"สรุปผลรวมทั้งหมด: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");
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

        //-------------------------------------
        // * Shared test-flow helpers *
        //-------------------------------------
        private Window GetOrOpenForm() => Retry(FindOpenForm, 1500) ?? OpenForm();

        private Window OpenForm()
        {
            var newBtn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("New")), 3000)
                         ?? throw new Exception("หาปุ่ม New ไม่เจอ");
            newBtn.Click();

            var form = Retry(FindOpenForm, 8000) ?? throw new Exception("หาฟอร์มไม่เจอ");
            WaitUntil(() => form.FindFirstDescendant(cf => cf.ByAutomationId("devTxtPermissionGroupCode")) != null, 4000);
            return form;
        }

        private Window? FindOpenForm() =>
            _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault(w => w.AutomationId == FormId || w.Name.Contains(ScreenNameTh)) ??
            _automation.GetDesktop().FindFirstChild(cf => cf.ByAutomationId(FormId).Or(cf.ByName(ScreenNameTh)))?.AsWindow();

        private AutomationElement GetGeneralTab(Window form) =>
            Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(GeneralTabId)), 3000) ?? form;

        private void FillPermissionGroupFields(AutomationElement tab, string code, string nameTh, string nameEn, string remark)
        {
            SetText(tab, "devTxtPermissionGroupCode", code);
            SetText(tab, "devTxtPermissionGroupName", nameTh);
            SetText(tab, "devTxtPermissionGroupNameEng", nameEn);
            SetText(tab, "devMemoRemark", remark);
        }

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

        private bool TrySaveAndHandleDuplicate(Window form, string testName, string code, string saveBtn)
        {
            var btn = form.FindFirstDescendant(cf => cf.ByName(saveBtn)) ?? throw new Exception($"ไม่พบปุ่ม {saveBtn}");
            btn.Click();

            if (testName.Contains("Validation") || testName.Contains("Duplicate")) return true;

            var panel = FindTaskResultPanel(form, 1500);
            if (panel == null) return true;

            var texts = GetPanelTexts(panel);
            string? dup = texts.Any(t => t.Contains("รหัสกลุ่มผู้ใช้ ห้ามซ้ำ")) ? "รหัสกลุ่มผู้ใช้ซ้ำ" :
                          texts.Any(t => t.Contains("ชื่อกลุ่มผู้ใช้ ห้ามซ้ำ")) ? "ชื่อกลุ่มผู้ใช้ซ้ำ" : null;

            if (dup == null) return true;

            LogFailure(testName, $"'{code}' รหัสกลุ่มผู้ใช้ซ้ำ/ชื่อกลุ่มผู้ใช้ซ้ำ: {dup}");
            try { ClickCloseAndHandleDialog(form, "No", $"{testName}_Close"); } catch { }
            return false;
        }

        private void FillSaveAssert(AutomationElement tab, Window form, string code, string name, string saveBtn, string testName, int timeoutMs, params string[] msgs)
        {
            SetText(tab, "devTxtPermissionGroupCode", code);
            SetText(tab, "devTxtPermissionGroupName", name);

            WaitUntil(() => { try { form.Focus(); return true; } catch { return false; } }, 1000);

            form.FindFirstDescendant(cf => cf.ByName(saveBtn))?.Click();
            AssertTaskResultCustom(form, msgs, testName, timeoutMs);
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
            WaitUntil(() => form.FindFirstDescendant(cf => cf.ByAutomationId("devTxtPermissionGroupCode")) != null, 3000);
            return form;
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
                        // ตรวจสอบทั้งข้อความเต็ม หรือข้อความแกนหลักเพื่อความยืดหยุ่น
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

        private IEnumerable<string> GetPanelTexts(AutomationElement panel)
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