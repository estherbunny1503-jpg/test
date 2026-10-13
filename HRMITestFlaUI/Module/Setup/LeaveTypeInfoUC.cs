using FlaUI.Core;
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
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace HRMITestFlaUI.Module.Setup
{
    public class LeaveTypeInfoUC
    {
        // ── ตัวแปรสำหรับเก็บออบเจ็กต์หน้าต่างหลัก, ระบบ Automation และตัวนับผลการทดสอบ (Success/Fail) ──
        private readonly AutomationElement _mainWindow;
        private readonly UIA3Automation _automation;

        private int _totalSuccess = 0;
        private int _totalFail = 0;

        // ── ค่าคงที่สำหรับการตั้งค่า Timeout และข้อมูลการระบุตัวตนของฟอร์ม ──
        private const int TimeoutMs = 6000;
        private const string FormId = "LeaveTypeInfoUC";
        private const string FormTitle = "กำหนดประเภทการลา";

        // ── Controls AutomationId ─────────────────────────────────────
        // 1. แถบ General
        private const string IdTabGeneral = "devTabPgGeneral";
        private const string IdTxtLeaveTypeCode = "devTxtLeaveTypeCode";
        private const string IdTxtLeaveTypeName = "devTxtLeaveTypeName";
        private const string IdTxtLeaveTypeNameEng = "devTxtLeaveTypeNameEng";
        private const string IdLookEditLeaveGroupType = "devLookEditLeaveGroupType";
        private const string IdSpinLeaveDay = "devSpinLeaveDay";
        private const string IdMemoRemark = "devMemoRemark";
        private const string IdFormLeaveTypeInfoUC = "LeaveTypeInfoUC";

        // 2. แถบ ข้อกำหนดการลา
        private const string IdTabRules = "devTabPgDeduct";
        private const string IdChkDeduct = "devChkDeduct";
        private const string IdChkDeductMonth = "devChkDeductMonth";
        private const string IdChkDeductDaily = "devchkDeductDaliy";
        private const string IdBtnEditSetMonthlyDeduct = "devBtnEditSetMonthlyDeduct";
        private const string IdChkDecMonthlyAddLeave = "devChkDecMonthlyAddLeave";
        private const string IdChkPayReturn = "devChkPayReturn";

        // 3. แถบ เงื่อนไขการตรวจสอบ
        private const string IdTabCondition = "devTabPgCondition";
        private const string IdChkProrate = "devChkProrate";
        private const string IdRdgNotCondition = "devRdgNotCondition";
        private const string IdSpinMonth = "devSpinMonth";
        private const string IdSpinLeave = "devSpinLeave";
        private const string IdRdgCondition = "devRdgCondition";

        private static readonly string[] PreExistingLeaveCodes = new[] { "L-001", "L-002" };

        // ── Win32 P/Invoke ─────────────────────────────────────────────
        [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;

        [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);

        public LeaveTypeInfoUC(AutomationElement mainWindow, UIA3Automation automation)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

        private AutomationElement GetGeneralTab(Window form) =>
            form.FindFirstDescendant(cf => cf.ByAutomationId(IdTabGeneral)) ?? form;

        // -------------------------------------------------------------
        // * Navigation & Search System *
        // -------------------------------------------------------------
        /* Method สำหรับค้นหาหน้าจอกำหนดประเภทการลาผ่านแถบค้นหาด้านบน (baritemSearch) */
        private bool TryOpenLeaveTypeMenu(string stage)
        {
            try
            {
                // 1. ค้นหาช่องค้นหา baritemSearch
                var searchBox = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("baritemSearch"))
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)), 4000)
                                ?? throw new Exception("ไม่พบช่องค้นหา baritemSearch");

                WaitUntil(() => { try { searchBox.Focus(); return true; } catch { return false; } }, 1000);
                string searchKeyword = FormTitle;

                if (searchBox.Patterns.Value.IsSupported)
                    searchBox.Patterns.Value.Pattern.SetValue(searchKeyword);
                else
                    Keyboard.Type(searchKeyword);

                // 2. คลิกปุ่มค้นหา barChkSearch
                var searchBtn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("barChkSearch"))
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("Find")), 4000)
                                ?? throw new Exception("ไม่พบปุ่มค้นหา barChkSearch");

                searchBtn.Click();

                // 3. รอและเลือกผลลัพธ์เมนู
                var menuResult = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName(searchKeyword))
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("treeListColumn row 1"))
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("treeListColumn row 19")), 4000)
                                 ?? throw new Exception("ไม่พบผลลัพธ์เมนูจากการค้นหา");

                menuResult.Focus();
                menuResult.Click();
                Keyboard.Press(VirtualKeyShort.ENTER);

                // 4. รอให้ปุ่ม New แสดงบนหน้าจอเพื่อยืนยันว่าเข้าหน้าจอเรียบร้อยแล้ว
                return WaitUntil(() =>
                {
                    var newBtn = _mainWindow.FindFirstDescendant(cf => cf.ByName("New"));
                    return newBtn != null && newBtn.Properties.IsEnabled.ValueOrDefault && !newBtn.Properties.IsOffscreen.ValueOrDefault;
                }, TimeoutMs);
            }
            catch (Exception ex)
            {
                LogFailureCounted(stage, $"ค้นหาและเปิดหน้าจอไม่สำเร็จ: {ex.Message}");
                return false;
            }
        }

        // ---------------------------------------------------------------
        // 1. ทดสอบการ New โดยเพิ่มข้อมูล 5 รายการ
        // ---------------------------------------------------------------
        public void NavigateToLeaveTypeInfoUC()
        {
            string stageName = "TestCase00_AddLeaveTypeData";
            TestLogger.LogInfo("LeaveTypeInfoUC", "เริ่มทดสอบหน้าจอกำหนดประเภทการลา");

            if (!TryOpenLeaveTypeMenu(stageName)) return;

            var dataList = new[]
            {
                new { ClickNew = true,  Code = "LV001", Name = "ลากิจส่วนตัว",       NameEng = "Personal Business Leave", Group = "ลากิจ",     Days = "6",  Remark = "ลาเพื่อดำเนินธุระส่วนตัว", Save = "SaveClose" },
                new { ClickNew = true,  Code = "LV002", Name = "ลาป่วยทั่วไป",      NameEng = "Sick Leave",              Group = "ลาป่วย",     Days = "30", Remark = "ลาป่วยทั่วไป",              Save = "SaveNew" },
                new { ClickNew = false, Code = "LV003", Name = "ลาพักผ่อนประจำปี",  NameEng = "Annual Leave",            Group = "ลาพักร้อน",  Days = "6",  Remark = "ลาพักผ่อนประจำปี",         Save = "SaveNew" },
                new { ClickNew = false, Code = "LV004", Name = "ลาคลอดบุตร",       NameEng = "Maternity Leave",         Group = "ลาคลอด",    Days = "98", Remark = "ลาเพื่อคลอดบุตร",          Save = "SaveNew" },
                new { ClickNew = false, Code = "LV005", Name = "ลาเพื่อสมรส",       NameEng = "Study Leave",             Group = "ลาอื่น ๆ",   Days = "5",  Remark = "ลาเพื่อเข้าพิธีสมรส",         Save = "SaveAndClose" }
            };

            Window? form = null;

            foreach (var data in dataList)
            {
                string testName = $"{stageName}_{data.Code}";
                try
                {
                    if (data.ClickNew || form == null || !form.IsAvailable)
                    {
                        SafeClickMainWindow("New");
                        form = GetLeaveTypeFormWindow();
                    }

                    FillGeneralTab(form, data.Code, data.Name, data.NameEng, data.Group, data.Days, data.Remark);

                    switch (data.Save)
                    {
                        case "SaveClose":
                            SafeClick(form, "Save");
                            SafeCloseForm(form);
                            WaitForClosed(() => FindOpenLeaveTypeForm());
                            form = null;
                            if (VerifyRecordExistsInGrid(data.Code))
                                LogSuccessCounted(testName, $"'{data.Code}' เพิ่มสำเร็จ (พบในตารางหลัก)");
                            else
                                LogFailureCounted(testName, $"'{data.Code}' ไม่พบในตารางหลักหลังบันทึก");
                            break;

                        case "SaveNew":
                            SafeClick(form, "Save and New");
                            var ctx = GetGeneralTab(form);
                            WaitUntil(() =>
                            {
                                var txt = ctx.FindFirstDescendant(cf => cf.ByAutomationId(IdTxtLeaveTypeCode))?.AsTextBox();
                                return string.IsNullOrEmpty(txt?.Text);
                            }, 3000);
                            LogSuccessCounted(testName, $"'{data.Code}' บันทึกสำเร็จ (Save and New)");
                            break;

                        case "SaveAndClose":
                            if (!SafeClick(form, "Save and Close"))
                            {
                                SafeClick(form, "Save");
                                SafeCloseForm(form);
                            }
                            WaitForClosed(() => FindOpenLeaveTypeForm());
                            form = null;
                            if (VerifyRecordExistsInGrid(data.Code))
                                LogSuccessCounted(testName, $"'{data.Code}' เพิ่มสำเร็จ (พบในตารางหลัก)");
                            else
                                LogFailureCounted(testName, $"'{data.Code}' ไม่พบในตารางหลักหลังบันทึก");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    LogFailureCounted(testName, $"'{data.Code}' เพิ่มไม่สำเร็จ: {ex.Message}");
                    if (form != null && form.IsAvailable) SafeCloseForm(form);
                    form = null;
                }
            }

            EnsureNoStrayModal($"{stageName}_Cleanup");
        }

        // ---------------------------------------------------------------
        // 2. ทดสอบ แจ้งเตือนต่างๆ เมื่อ New
        // ---------------------------------------------------------------
        public void RunAllValidationTests()
        {
            string stageName = "TestCase01-06_ValidationNew";
            EnsureNoStrayModal($"{stageName}_PreCheck");

            Window form;
            try
            {
                form = OpenLeaveTypeInfoForm();
            }
            catch (Exception ex)
            {
                LogFailureCounted(stageName, $"เปิดฟอร์มไม่สำเร็จ: {ex.Message}");
                return;
            }

            var context = GetGeneralTab(form);

            // [เคส 1] ไม่กรอกรหัส + กรอกชื่อ "ลากิจส่วนตัว" -> กด Save
            RunValidationStep("TestCase01_MissingCode", () =>
            {
                FillAndSaveThenAssert(context, form, "", "ลากิจส่วนตัว", "Save",
                    "TestCase01_MissingCode", "รหัสประเภทการลา ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล");
            });

            // [เคส 2] LV001 + ไม่กรอกชื่อ -> กด Save and New
            RunValidationStep("TestCase02_MissingName", () =>
            {
                FillAndSaveThenAssert(context, form, "LV001", "", "Save and New",
                    "TestCase02_MissingName", "ชื่อประเภทการลา ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล");
            });

            // [เคส 3] ไม่กรอกรหัส + ไม่กรอกชื่อ -> กด Save and New
            RunValidationStep("TestCase03_MissingAllFields", () =>
            {
                FillAndSaveThenAssert(context, form, "", "", "Save and New",
                    "TestCase03_MissingAllFields",
                    "รหัสประเภทการลา ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล",
                    "ชื่อประเภทการลา ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล");
            });

            // [เคส 4] รหัส/ชื่อซ้ำ: LV005 + ลาเพื่อสมรส -> กด Save and New
            string subCase = "TestCase04_DuplicateCodeAndName";
            RunValidationStep(subCase, () =>
            {
                FillAndSaveThenAssert(context, form, "LV005", "ลาเพื่อสมรส", "Save and New",
                    subCase, "รหัสประเภทการลา ห้ามซ้ำ กรุณาป้อนข้อมูล");
            });

            // [เคส 5] ลำดับการปิดหน้าจอหลังแจ้งเตือนซ้ำ (Close Sequence)
            RunValidationStep($"{subCase}_CloseSequence", () =>
                RunCloseDialogSequence(form, subCase, "รหัสประเภทการลา ห้ามซ้ำ กรุณาป้อนข้อมูล"));
        }

        // ---------------------------------------------------------------
        // 3. ทดสอบ Edit
        // ---------------------------------------------------------------
        public void RunEditTests()
        {
            string stageName = "TestCase03_EditData";
            EnsureNoStrayModal($"{stageName}_PreCheck");

            PerformEdit("TestCase06_EditRow1", existingCode: "LV001", newCode: "TU001", newName: "ลากิจไปทำธุระ", saveButtonName: "Save");
            PerformEdit("TestCase07_EditRow2", existingCode: "LV002", newCode: "TU002", newName: "ลาป่วยไม่สบาย", saveButtonName: "Save and Close");
        }

        private void PerformEdit(string testName, string existingCode, string newCode, string newName, string saveButtonName)
        {
            try
            {
                Window form = ClickRowAndOpenEditForm(existingCode, testName);
                var context = GetGeneralTab(form);

                FillTextBox(context, IdTxtLeaveTypeCode, newCode);
                FillTextBox(context, IdTxtLeaveTypeName, newName);

                if (saveButtonName == "Save and Close" && SafeClick(form, "Save and Close"))
                {
                    SafeCloseForm(form);
                }
                else
                {
                    SafeClick(form, "Save");
                    SafeCloseForm(form);
                }

                WaitForClosed(() => FindOpenLeaveTypeForm());

                if (VerifyRecordExistsInGrid(newCode))
                    LogSuccessCounted(testName, $"แก้ไข '{existingCode}' เป็น '{newCode}' สำเร็จ (พบในตารางหลัก)");
                else
                    LogFailureCounted(testName, $"แก้ไข '{existingCode}' เป็น '{newCode}' แล้ว แต่ไม่พบในตารางหลัก");
            }
            catch (Exception ex)
            {
                LogFailureCounted(testName, $"แก้ไข '{existingCode}' ไม่สำเร็จ: {ex.Message}");
            }
        }

        // ---------------------------------------------------------------
        // 4. ทดสอบ การแจ้งเตือน Edit
        // ---------------------------------------------------------------
        public void RunEditValidationTests()
        {
            string baseTestName = "TestCase08_EditValidation";
            EnsureNoStrayModal($"{baseTestName}_PreCheck");

            Window form;
            try
            {
                form = ClickRowAndOpenEditForm("LV004", baseTestName);
            }
            catch (Exception ex)
            {
                LogFailureCounted(baseTestName, $"เปิดฟอร์ม Edit ไม่สำเร็จ: {ex.Message}");
                return;
            }

            var context = GetGeneralTab(form);

            // 4.1 ลบรหัสประเภทการลา -> ชื่อ "ลาพักผ่อน" -> กด Save
            RunValidationStep($"{baseTestName}_MissingCode", () =>
                FillAndSaveThenAssert(context, form, "", "ลาพักผ่อน", "Save",
                    $"{baseTestName}_MissingCode", "รหัสประเภทการลา ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล"));

            // 4.2 กรอกรหัสเดิม LV004 -> ลบชื่อประเภทการลาออก -> ใช้ Save เพื่อให้ Validation ทำงานแน่นอนในโหมด Edit
            RunValidationStep($"{baseTestName}_MissingName", () =>
                FillAndSaveThenAssert(context, form, "LV004", "", "Save",
                    $"{baseTestName}_MissingName", "ชื่อประเภทการลา ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล"));

            // 4.3 ลบรหัสเดิมออก -> ลบชื่อเดิมออก -> กด Save
            RunValidationStep($"{baseTestName}_MissingBoth", () =>
                FillAndSaveThenAssert(context, form, "", "", "Save",
                    $"{baseTestName}_MissingBoth",
                    "รหัสประเภทการลา ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล",
                    "ชื่อประเภทการลา ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล"));

            // 4.4 กรอกรหัสซ้ำ LV005 -> กรอกชื่อซ้ำ "ลาเพื่อสมรส" -> กด Save and New
            string subCase = $"{baseTestName}_DuplicateCodeAndName";
            RunValidationStep(subCase, () =>
            {
                ClearField(context, IdTxtLeaveTypeCode);
                ClearField(context, IdTxtLeaveTypeName);

                FillAndSaveThenAssert(context, form, "LV005", "ลาเพื่อสมรส", "Save and New",
                    subCase, "รหัสประเภทการลา ห้ามซ้ำ กรุณาป้อนข้อมูล");
            });

            // 4.5 ลำดับการปิดหน้าจอหลังเตือนซ้ำ (Close Sequence)
            RunValidationStep($"{subCase}_CloseSequence", () =>
                RunCloseDialogSequence(form, subCase, "รหัสประเภทการลา ห้ามซ้ำ กรุณาป้อนข้อมูล"));

            LogSuccessCounted(baseTestName, "รัน Validation Edit ครบทุกเคสแล้ว");
        }

        // ---------------------------------------------------------------
        // 5. ทดสอบ Delete
        // ---------------------------------------------------------------
        public void RunDeleteTests()
        {
            string baseTestName = "TestCase09_Delete";

            // คลิกข้อมูลแถวสุดท้าย (TU002) -> กด Delete -> ตอบ Yes (ลบสำเร็จ)
            RunDeleteCase(baseTestName, targetCode: "TU002", confirmButton: "Yes", expectDeleted: true);

            // คลิกข้อมูลแถวแรก (TU001) -> กด Delete -> ตอบ No (ข้อมูลยังคงเดิม)
            RunDeleteCase(baseTestName, targetCode: "TU001", confirmButton: "No", expectDeleted: false);

            LogSuccessCounted(baseTestName, "ทดสอบ Delete ครบทุกเคส");
            TestLogger.LogInfo("LeaveTypeInfoUC", $"สรุปผลรวมทั้งหมด: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");
        }

        private void RunDeleteCase(string baseTestName, string targetCode, string confirmButton, bool expectDeleted)
        {
            string testName = $"{baseTestName}_{targetCode}_{confirmButton}";
            try
            {
                DeleteRowByCodeAndVerify(targetCode, confirmButton, testName, expectDeleted);
            }
            catch (Exception ex)
            {
                LogFailureCounted(testName, $"ลบรหัส '{targetCode}' ไม่สำเร็จ: {ex.Message}");
            }
        }

        // ---------------------------------------------------------------
        // Shared Test-Flow Helpers
        // ---------------------------------------------------------------
        private void RunValidationStep(string testName, Action step)
        {
            try
            {
                step();
            }
            catch (Exception ex)
            {
                LogFailureCounted(testName, $"ไม่ผ่าน: {ex.Message}");
            }
        }

        private void RunCloseDialogSequence(Window form, string testNamePrefix, string expectedWarningMessage)
        {
            string subCase = $"{testNamePrefix}_Close_Yes";
            ClickCloseAndHandleDialog(form, "Yes");
            AssertWarningAndFormOpen(form, expectedWarningMessage, subCase);

            subCase = $"{testNamePrefix}_Close_Cancel";
            ClickCloseAndHandleDialog(form, "Cancel");
            AssertFormStillOpen(form, subCase);

            subCase = $"{testNamePrefix}_Close_No";
            ClickCloseAndHandleDialog(form, "No");
            AssertFormClosed(form, subCase);
        }

        private void FillAndSaveThenAssert(AutomationElement context, Window form, string code, string name,
            string saveButtonName, string testName, params string[] expectedMessages)
        {
            FillTextBox(context, IdTxtLeaveTypeCode, code);
            FillTextBox(context, IdTxtLeaveTypeName, name);

            WaitUntil(() => { try { form.Focus(); return true; } catch { return false; } }, 1000);

            SafeClick(form, saveButtonName);

            var pending = new HashSet<string>(expectedMessages);
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < 5000 && pending.Count > 0)
            {
                try
                {
                    var taskResultPanel = form.FindFirstDescendant(cf => cf.ByName("Task Result").Or(cf.ByAutomationId("dockPanelTaskResult")))
                        ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("Task Result").Or(cf.ByAutomationId("dockPanelTaskResult")));

                    var allTexts = new List<string>();
                    if (taskResultPanel != null) allTexts.AddRange(GetPanelTexts(taskResultPanel));
                    allTexts.AddRange(GetPanelTexts(form));

                    foreach (var m in pending.ToList())
                    {
                        if (allTexts.Any(t => t.Contains(m) || (m.Contains(" ") && t.Contains(m.Split(' ')[0]))))
                            pending.Remove(m);
                    }
                }
                catch { }

                if (pending.Count == 0) break;
            }

            if (pending.Count > 0)
                throw new Exception($"ไม่พบข้อความแจ้งเตือน: '{string.Join("', '", pending)}'");

            string formattedList = string.Join(",", expectedMessages.Select(m => $"'{m}'"));
            LogSuccessCounted(testName, $"พบข้อความ: {formattedList}");
        }

        private void AssertWarningAndFormOpen(Window form, string expectedWarningMessage, string testName)
        {
            bool foundMessage = false;
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < 4000)
            {
                try
                {
                    var taskResultPanel = form.FindFirstDescendant(cf => cf.ByName("Task Result").Or(cf.ByAutomationId("dockPanelTaskResult")))
                        ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("Task Result").Or(cf.ByAutomationId("dockPanelTaskResult")));

                    var allTexts = new List<string>();
                    if (taskResultPanel != null) allTexts.AddRange(GetPanelTexts(taskResultPanel));
                    allTexts.AddRange(GetPanelTexts(form));

                    if (allTexts.Any(t => t.Contains(expectedWarningMessage) || (expectedWarningMessage.Contains(" ") && t.Contains(expectedWarningMessage.Split(' ')[0]))))
                    {
                        foundMessage = true;
                        break;
                    }
                }
                catch { }
            }

            bool isOpen = WaitUntil(() => !form.Properties.IsOffscreen.ValueOrDefault && form.IsAvailable, 2000);
            if (!foundMessage || !isOpen)
                throw new Exception($"ตรวจสอบไม่ผ่าน (พบข้อความ: {foundMessage}, ฟอร์มเปิดอยู่: {isOpen})");

            LogSuccessCounted(testName, $"พบข้อความ: '{expectedWarningMessage}' และ ฟอร์มถูกเปิดอยู่");
        }

        private void FillGeneralTab(Window form, string code, string name, string nameEng, string group = "", string days = "", string remark = "")
        {
            var tab = Retry(() =>
                form.FindFirstDescendant(cf => cf.ByAutomationId(IdTabGeneral)) ??
                form.FindFirstDescendant(cf => cf.ByName("General")), 3000);
            try { tab?.Click(); } catch { }

            WaitUntil(() => form.FindFirstDescendant(cf => cf.ByAutomationId(IdTxtLeaveTypeCode)) != null, 2500);

            var ctx = form.FindFirstDescendant(cf => cf.ByAutomationId(IdTabGeneral)) ?? form;

            if (!string.IsNullOrEmpty(code)) FillTextBox(ctx, IdTxtLeaveTypeCode, code);
            if (!string.IsNullOrEmpty(name)) FillTextBox(ctx, IdTxtLeaveTypeName, name);
            if (!string.IsNullOrEmpty(nameEng)) FillTextBox(ctx, IdTxtLeaveTypeNameEng, nameEng);
            if (!string.IsNullOrEmpty(group)) SelectComboBoxValue(ctx, IdLookEditLeaveGroupType, group);
            if (!string.IsNullOrEmpty(days)) FillTextBox(ctx, IdSpinLeaveDay, days);
            if (!string.IsNullOrEmpty(remark)) FillTextBox(ctx, IdMemoRemark, remark);
        }

        private void SelectComboBoxValue(AutomationElement context, string automationId, string valueText)
        {
            var combo = Retry(() => context.FindFirstDescendant(cf => cf.ByAutomationId(automationId)), timeoutMs: 3000);
            if (combo == null) return;

            try
            {
                combo.Focus();
                var bounds = combo.BoundingRectangle;
                int clickX = (int)bounds.Right - 10;
                int clickY = (int)bounds.Center().Y;

                SetCursorPos(clickX, clickY);
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);

                Keyboard.Type(valueText);
                Keyboard.Press(VirtualKeyShort.RETURN);
            }
            catch { }
        }

        private Window OpenLeaveTypeInfoForm()
        {
            SafeClickMainWindow("New");
            return GetLeaveTypeFormWindow();
        }

        private Window GetLeaveTypeFormWindow()
        {
            Window? form = Retry(() =>
                _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault(w => w.AutomationId == IdFormLeaveTypeInfoUC || w.Name.Contains(FormTitle)) ??
                _automation.GetDesktop().FindFirstChild(cf => cf.ByAutomationId(IdFormLeaveTypeInfoUC).Or(cf.ByName(FormTitle)))?.AsWindow(), 8000);
            if (form == null) throw new Exception("หาฟอร์ม LeaveTypeInfoUC ไม่เจอ");
            WaitUntil(() => form.FindFirstDescendant(cf => cf.ByAutomationId(IdTxtLeaveTypeCode)) != null, 4000);
            return form;
        }

        private Window? FindOpenLeaveTypeForm() =>
            _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault(w => w.AutomationId == IdFormLeaveTypeInfoUC || w.Name.Contains(FormTitle))
            ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByAutomationId(IdFormLeaveTypeInfoUC).Or(cf.ByName(FormTitle)))?.AsWindow();

        private Window ClickRowAndOpenEditForm(string codeToFind, string testName)
        {
            AutomationElement? row = FindGridRowByCodeInContainer(_mainWindow, codeToFind)
                ?? throw new Exception($"หาแถว '{codeToFind}' ไม่เจอ");

            WaitUntil(() => { try { row.Focus(); return true; } catch { return false; } }, 1000);
            row.Click();

            var editBtn = Retry(() =>
            {
                var b = _mainWindow.FindFirstDescendant(cf => cf.ByName("Edit"));
                return (b != null && b.Properties.IsEnabled.ValueOrDefault) ? b : null;
            }, 3000) ?? throw new Exception("หาปุ่ม Edit ไม่เจอ");
            editBtn.Click();

            var form = GetLeaveTypeFormWindow();
            WaitUntil(() => form.FindFirstDescendant(cf => cf.ByAutomationId(IdTxtLeaveTypeCode)) != null, 3000);
            return form;
        }

        private void DeleteRowByCodeAndVerify(string targetCode, string confirmButton, string testName, bool expectDeleted)
        {
            AutomationElement? row = FindGridRowByCodeInContainer(_mainWindow, targetCode)
                ?? throw new Exception($"หาแถวรหัส '{targetCode}' ในตารางหลักไม่เจอ");

            WaitUntil(() => { try { row.Focus(); return true; } catch { return false; } }, 1000);
            ClickElementByMouseInstant(row);

            var deleteBtn = Retry(() =>
            {
                var b = _mainWindow.FindFirstDescendant(cf => cf.ByName("Delete"));
                return (b != null && b.Properties.IsEnabled.ValueOrDefault) ? b : null;
            }, 3000) ?? throw new Exception("หาปุ่ม Delete ไม่เจอ");
            deleteBtn.Click();

            Window dialog = Retry(() =>
                _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault() ??
                _automation.GetDesktop().FindFirstChild(cf => cf.ByName("คำเตือน"))?.AsWindow(), timeoutMs: 4000)
                ?? throw new Exception("กด Delete แล้วไม่พบ Pop-up ยืนยัน");

            ClickDialogButton(dialog, confirmButton);
            WaitForClosed(() => dialog);

            bool stillExists = VerifyRecordExistsInGrid(targetCode, timeoutMs: 3000);
            if (expectDeleted && stillExists) throw new Exception($"'{targetCode}' ลบข้อมูลไม่สำเร็จ");
            if (!expectDeleted && !stillExists) throw new Exception($"'{targetCode}' ลบข้อมูลผิดพลาด");

            LogSuccessCounted(testName, expectDeleted ? $"'{targetCode}' ถูกลบสำเร็จ" : $"'{targetCode}' ข้อมูลยังไม่ถูกลบ");
        }

        // ---------------------------------------------------------------
        // Dialog Helpers
        // ---------------------------------------------------------------
        private void ClickCloseAndHandleDialog(Window form, string buttonToClick)
        {
            ForceActivateWindow(form);

            var closeBtn = form.FindFirstDescendant(cf => cf.ByName("Close"))
                        ?? form.FindFirstDescendant(cf => cf.ByName("&Close"))
                        ?? form.FindFirstDescendant(cf => cf.ByName("button 'Close'"));

            if (closeBtn != null) ClickElementByMouseInstant(closeBtn);
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
            ForceActivateWindow(dialog.AsWindow() ?? _mainWindow.AsWindow());

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
                ClickElementByMouseInstant(btn);
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

            WaitForClosed(() => FindOpenLeaveTypeForm());
        }

        // ---------------------------------------------------------------
        // Grid & Element Assertion Helpers
        // ---------------------------------------------------------------
        private bool VerifyRecordExistsInGrid(string value, int timeoutMs = 5000)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;

            string targetClean = value.Trim().Replace(" ", "");

            bool IsMatch(string? rawText)
            {
                if (string.IsNullOrEmpty(rawText)) return false;
                string clean = rawText.Trim().Replace(" ", "");
                return clean.Equals(targetClean, StringComparison.OrdinalIgnoreCase) ||
                       clean.Contains(targetClean, StringComparison.OrdinalIgnoreCase);
            }

            return Retry(() =>
            {
                var grid = _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataGrid))
                           ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Table))
                           ?? _mainWindow;

                foreach (var el in grid.FindAllDescendants())
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(el.Name) && el.Name.Contains("Task Result")) continue;

                        if (IsMatch(el.Name)) return el;
                        if (el.Patterns.Value.IsSupported && IsMatch(el.Patterns.Value.Pattern.Value.ValueOrDefault)) return el;
                        if (el.Patterns.LegacyIAccessible.IsSupported)
                        {
                            if (IsMatch(el.Patterns.LegacyIAccessible.Pattern.Name.ValueOrDefault)) return el;
                            if (IsMatch(el.Patterns.LegacyIAccessible.Pattern.Value.ValueOrDefault)) return el;
                        }
                    }
                    catch { }
                }
                return null;
            }, timeoutMs: timeoutMs) != null;
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

        private static IEnumerable<string> GetPanelTexts(AutomationElement panel)
        {
            foreach (var el in panel.FindAllDescendants())
            {
                if (!string.IsNullOrEmpty(el.Name)) yield return el.Name;
                string? value = null;
                try { if (el.Patterns.Value.IsSupported) value = el.Patterns.Value.Pattern.Value.ValueOrDefault; } catch { }
                if (!string.IsNullOrEmpty(value)) yield return value;
            }
        }

        private void AssertFormStillOpen(Window form, string testName)
        {
            bool isOpen = WaitUntil(() => !form.Properties.IsOffscreen.ValueOrDefault && form.IsAvailable, 2000);
            if (!isOpen) throw new Exception("ฟอร์มถูกปิด");
            LogSuccessCounted(testName, "ฟอร์มถูกเปิดอยู่");
        }

        private void AssertFormClosed(Window form, string testName)
        {
            bool isClosed = WaitForClosed(() => FindOpenLeaveTypeForm(), 3000);
            if (!isClosed) throw new Exception("ปิดฟอร์มไม่สำเร็จ");
            LogSuccessCounted(testName, "ฟอร์มถูกปิด");
        }

        private bool SafeClick(AutomationElement scope, string buttonName, int timeoutMs = 6000)
        {
            var btn = Retry(() => scope.FindFirstDescendant(cf => cf.ByName(buttonName) ?? cf.ByAutomationId(buttonName)), timeoutMs);
            if (btn == null) return false;

            try { btn.Click(); return true; }
            catch (COMException ex) when (ex.HResult == unchecked((int)0x80131505)) { return true; }
            catch { return false; }
        }

        private bool SafeClickMainWindow(string buttonName, int timeoutMs = 6000) => SafeClick(_mainWindow, buttonName, timeoutMs);

        private void FillTextBox(AutomationElement context, string automationId, string text)
        {
            var element = Retry(() => context.FindFirstDescendant(cf => cf.ByAutomationId(automationId)), timeoutMs: 4000)
                ?? throw new Exception($"หาช่องกรอก '{automationId}' ไม่เจอ");

            WaitUntil(() => { try { element.Focus(); return true; } catch { return false; } }, 1000);

            if (element.Patterns.Value.IsSupported)
            {
                try
                {
                    element.Patterns.Value.Pattern.SetValue(text ?? "");
                    return;
                }
                catch { }
            }

            ClickElementByMouseInstant(element);

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

        private void ClearField(AutomationElement context, string automationId)
        {
            var element = Retry(() => context.FindFirstDescendant(cf => cf.ByAutomationId(automationId)), timeoutMs: 3000);
            if (element != null)
            {
                FillTextBox(context, automationId, "");
            }
        }

        private void ClickElementByMouseInstant(AutomationElement element)
        {
            var bounds = element.BoundingRectangle;
            int x = (int)bounds.Center().X;
            int y = (int)bounds.Center().Y;
            SetCursorPos(x, y);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
        }

        private void ForceActivateWindow(AutomationElement windowElement)
        {
            try
            {
                IntPtr hWnd = (IntPtr)windowElement.Properties.NativeWindowHandle.ValueOrDefault;
                if (hWnd != IntPtr.Zero) { BringWindowToTop(hWnd); SetForegroundWindow(hWnd); }
            }
            catch { }
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

                    if (autoId == IdFormLeaveTypeInfoUC || name.Contains("กำหนดประเภทการลา") || name.Contains("Prosoft HRMI")) continue;

                    if (name.Contains("คำเตือน") || name.Contains("Question"))
                    {
                        try { ClickDialogButton(modal, "No"); }
                        catch { _ = SafeClick(modal, "No") || SafeClick(modal, "&No") || SafeClick(modal, "Cancel"); }
                        WaitForClosed(() => modal);
                    }
                    else if (name.Contains("Task Result") || autoId.Contains("TaskResult"))
                    {
                        _ = SafeClick(modal, "Close") || SafeClick(modal, "OK");
                        WaitForClosed(() => modal);
                    }
                }
            }
            catch (Exception ex)
            {
                TestLogger.LogInfo(context, $"EnsureNoStrayModal: {ex.Message}");
            }
        }

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

        private static T? Retry<T>(Func<T?> fn, int timeoutMs = TimeoutMs) where T : class
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try { var r = fn(); if (r != null) return r; } catch { }
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