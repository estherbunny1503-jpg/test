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
using System.Threading;

namespace HRMITestFlaUI.Module.Setup
{
    public class PosttoPayrollSetupInfoUC
    {
        // ── ตัวแปรสำหรับเก็บออบเจ็กต์หน้าต่างหลัก, ระบบ Automation และตัวนับผลการทดสอบ (Success/Fail) ──
        private readonly AutomationElement _mainWindow;
        private readonly UIA3Automation _automation;

        private int _totalSuccess = 0;
        private int _totalFail = 0;

        // ── ค่าคงที่สำหรับการตั้งค่า Timeout และข้อมูลการระบุตัวตนของฟอร์ม ──
        private const int TimeoutMs = 6000;
        private const string FormId = "PosttoPayrollSetupInfoUC";
        private const string FormTitle = "กำหนดรูปแบบการประมวลผลเข้าระบบเงินเดือน";

        private const string GeneralTabId = "devTabPgGeneral";

        // ── AutomationId ยืนยันแล้วจาก Properties panel ──
        private const string IdFormatName = "devTxtPPSName";               // "ชื่อรูปแบบประมวลผล"

        private const string IdChkPayDaily = "devChkDaily";                 // "รายวัน" (พนักงานรายวัน)
        private const string IdLookupDailyItem = "devBtnEmpDaily";          // เลือกรายการ IN-001 ค่าแรงรายวัน (ButtonEdit)

        private const string IdChkPayHourly = "devChkHoury";                // "รายชั่วโมง" (พนักงานรายชั่วโมง)
        private const string IdLookupHourlyItem = "devBtnEmpHoury";         // เลือกรายการ IN-002 ค่าแรงรายชั่วโมง (ButtonEdit)

        // ── กลุ่ม checkbox ที่ใช้ร่วมกัน ไม่ว่าจะเลือกรายวัน/รายชั่วโมง/ทั้งคู่ ──
        private const string IdChkMustPayHoliday = "devChkHolidayPayment";       // "ต้องการจ่ายค่าแรงสำหรับวันหยุดนักขัตฤกษ์"
        private const string IdHolidayDays = "devSpinDayPay";                    // จำนวนวัน (ไม่เกิน 15 วัน)

        private const string IdChkPayHolidayWage = "devchkIsPayLeave";          // "จ่ายค่าแรงวันหยุดนักขัตฤกษ์"
        private const string IdChkPayHolidayOvertime = "devChkPaySpecialHoliday"; // "จ่ายค่าแรงวันหยุดนักขัตฤกษ์เมื่อมีการทำงานล่วงเวลา"

        // ── ป้ายข้อความ Lookup ค่าแรงรายวัน / รายชั่วโมง ──
        private const string LabelDailyWageLookup = "ค่าแรงรายวัน(พนักงานรายวัน)";
        private const string LabelHourlyWageLookup = "ค่าแรงรายชั่วโมง(พนักงานรายชั่วโมง)";

        /* Constructor สำหรับรับค่า MainWindow และ Automation Instance เข้ามาใช้งานภายในคลาส */
        public PosttoPayrollSetupInfoUC(AutomationElement mainWindow, UIA3Automation automation)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

        private record WageFormatRecord(
            string Name,
            bool SelectDaily,
            bool SelectHourly,
            string HolidayDays = "15");

        // -------------------------------------------------------------
        // * Navigation & Search System *
        // -------------------------------------------------------------
        /* Method สำหรับค้นหาหน้าจอกำหนดรูปแบบการประมวลผลเข้าระบบเงินเดือนผ่านแถบค้นหาด้านบน (baritemSearch) */
        private bool TryOpenPosttoPayrollSetupInfoMenu(string stage)
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
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("treeListColumn row 21")), 4000)
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

        // -------------------------------------------------------------
        // * 1. ทดสอบการ New *
        // -------------------------------------------------------------
        public void NavigateToPosttoPayrollSetupInfoUC()
        {
            var items = new[]
            {
                new WageFormatRecord("รูปแบบประมวลผล 1", SelectDaily: true,  SelectHourly: false),
                new WageFormatRecord("รูปแบบประมวลผล 2", SelectDaily: false, SelectHourly: true),
                new WageFormatRecord("รูปแบบประมวลผล 3", SelectDaily: true,  SelectHourly: true),
                new WageFormatRecord("รูปแบบประมวลผล 4", SelectDaily: false, SelectHourly: false),
            };

            string stageName = "TestCase00_AddPosttoPayrollSetupInfo";
            TestLogger.LogInfo("PosttoPayrollSetupInfoUC", "เริ่มทดสอบหน้าจอ Post to Payroll Setup");

            if (!TryOpenPosttoPayrollSetupInfoMenu(stageName)) return;

            Window? form = null;

            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                string testName = $"{stageName}_{item.Name}";

                try
                {
                    form ??= GetOrOpenPosttoPayrollSetupInfoForm();
                    var context = GetGeneralTab(form);

                    FillWageFormatFields(context, item);

                    string saveButtonName = i == 0 ? "Save"
                                          : i == items.Length - 1 ? "Save and Close"
                                          : "Save and New";

                    if (!TrySaveAndHandleDuplicate(form, testName, item.Name, saveButtonName))
                    {
                        form = null;
                        continue;
                    }

                    switch (saveButtonName)
                    {
                        case "Save":
                            form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();
                            WaitForClosed(() => FindOpenPosttoPayrollSetupInfoForm());
                            form = null;
                            RefreshGrid();
                            if (VerifyRecordExistsInGrid(item.Name, refreshGrid: false))
                                LogSuccessCounted(testName, $"'{item.Name}' เพิ่มสำเร็จ (พบในตารางหลัก)");
                            else
                                LogFailureCounted(testName, $"'{item.Name}' ไม่พบในตารางหลักหลังบันทึก");
                            break;

                        case "Save and Close":
                            WaitForClosed(() => FindOpenPosttoPayrollSetupInfoForm());
                            form = null;
                            RefreshGrid();
                            if (VerifyRecordExistsInGrid(item.Name, refreshGrid: false))
                                LogSuccessCounted(testName, $"'{item.Name}' เพิ่มสำเร็จ (พบในตารางหลัก)");
                            else
                                LogFailureCounted(testName, $"'{item.Name}' ไม่พบในตารางหลักหลังบันทึก");
                            break;

                        case "Save and New":
                            WaitUntil(() =>
                            {
                                var txt = context.FindFirstDescendant(cf => cf.ByAutomationId(IdFormatName))?.AsTextBox();
                                return string.IsNullOrEmpty(txt?.Text);
                            }, 3000);
                            LogSuccessCounted(testName, $"'{item.Name}' บันทึกสำเร็จ (Save and New)");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    LogFailureCounted(testName, $"'{item.Name}' เพิ่มไม่สำเร็จ: {ex.Message}");
                    form = null;
                }
            }
        }

        // -------------------------------------------------------------
        // * 2. ทดสอบ แจ้งเตือนต่างๆ เมื่อ New *
        // -------------------------------------------------------------
        public void RunAllValidationTests()
        {
            string stageName = "TestCase01-06_Validation";

            Window form;
            try
            {
                form = OpenPosttoPayrollSetupInfoForm();
            }
            catch (Exception ex)
            {
                LogFailureCounted(stageName, $"เปิดฟอร์มไม่สำเร็จ: {ex.Message}");
                return;
            }

            var context = GetGeneralTab(form);

            // 2.1 New ไม่กรอกชื่อเลย -> Save -> "ชื่อรูปแบบการประมวลผล ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล"
            RunValidationStep("TestCase01_MissingName", () =>
            {
                FillTextBox(context, IdFormatName, "");
                TickCheckbox(context, IdChkMustPayHoliday, true);
                TickCheckbox(context, IdChkMustPayHoliday, false);
                ClickFormButton(form, "Save");
                AssertTaskResultWarning(form, "ชื่อรูปแบบการประมวลผล ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล", "TestCase01_MissingName");
            });

            // 2.2 กรอกชื่อ + ติ๊กต้องจ่ายค่าแรงวันหยุดนักขัตฤกษ์ แต่ไม่กรอกจำนวนวัน -> Save and New -> แจ้งเตือนจำนวนวันห้ามว่าง
            RunValidationStep("TestCase02_MissingHolidayDays", () =>
            {
                FillTextBox(context, IdFormatName, "รูปแบบประมวลผล 1");
                TickCheckbox(context, IdChkMustPayHoliday, true);
                FillTextBox(context, IdHolidayDays, "");
                ClickFormButton(form, "Save and New");
                AssertTaskResultWarning(form, "จำนวนวันจ่ายค่าแรงวันหยุดนักขัตฤกษ์ ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล", "TestCase02_MissingHolidayDays");
            });

            // 2.3 ติ๊กรายวัน แต่ไม่เลือกรายการ IN-001 -> Save and New -> แจ้งเตือนรหัสรายได้รายวันห้ามว่าง
            RunValidationStep("TestCase03_MissingDailyItem", () =>
            {
                FillTextBox(context, IdFormatName, "รูปแบบประมวลผล 1");
                TickCheckbox(context, IdChkMustPayHoliday, true);
                FillTextBox(context, IdHolidayDays, "10");
                TickCheckbox(context, IdChkPayDaily, true);
                ClickFormButton(form, "Save and New");
                AssertTaskResultWarning(form, "รหัสรายได้พนักงานรายวัน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล", "TestCase03_MissingDailyItem");
            });

            // 2.4 ติ๊กเพิ่ม "รายชั่วโมง" ต่อจากที่ "รายวัน" ยังติ๊กค้างมาจากขั้นก่อนหน้า
            RunValidationStep("TestCase05_MissingBothItems", () =>
            {
                FillTextBox(context, IdFormatName, "รูปแบบประมวลผล 1");
                TickCheckbox(context, IdChkMustPayHoliday, true);
                FillTextBox(context, IdHolidayDays, "10");
                TickCheckbox(context, IdChkPayDaily, true);
                TickCheckbox(context, IdChkPayHourly, true);
                ClickFormButton(form, "Save and New");
                AssertMultipleTaskResultWarnings(form, new[]
                {
                    "รหัสรายได้พนักงานรายวัน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล",
                    "รหัสรายได้พนักงานรายชั่วโมง ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล"
                }, "TestCase05_MissingBothItems");
            });

            // 2.6 New ชื่อซ้ำ "รูปแบบประมวลผล 2" -> Save and Close -> แจ้งเตือนชื่อซ้ำ
            string dupCase = "TestCase06_DuplicateName";
            RunValidationStep(dupCase, () =>
            {
                TickCheckbox(context, IdChkPayDaily, false);
                TickCheckbox(context, IdChkPayHourly, false);

                FillTextBox(context, IdFormatName, "รูปแบบประมวลผล 2");
                ClickFormButton(form, "Save and Close");
                AssertTaskResultWarning(form, "ชื่อรูปแบบการประมวลผล ห้ามซ้ำ กรุณาป้อนข้อมูล", dupCase);
                AssertFormStillOpen(form, dupCase);
            });

            // 2.7-2.9 กด Close -> Yes/Cancel/No บนฟอร์มเดิม
            RunValidationStep($"{dupCase}_CloseSequence", () =>
                RunCloseDialogSequence(form, dupCase, "ชื่อรูปแบบการประมวลผล ห้ามซ้ำ กรุณาป้อนข้อมูล"));
        }

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
            ClickCloseAndHandleDialog(form, "Yes", subCase);
            AssertWarningAndFormOpen(form, expectedWarningMessage, subCase);

            subCase = $"{testNamePrefix}_Close_Cancel";
            ClickCloseAndHandleDialog(form, "Cancel", subCase);
            AssertFormStillOpen(form, subCase);

            subCase = $"{testNamePrefix}_Close_No";
            ClickCloseAndHandleDialog(form, "No", subCase);
            AssertFormClosed(form, subCase);
        }

        // -------------------------------------------------------------
        // * 3. ทดสอบ Edit *
        // -------------------------------------------------------------
        public void RunEditTests()
        {
            PerformEdit("TestCase07_EditRow2", existingName: "รูปแบบประมวลผล 2",
                newName: "P001", editAction: (context) =>
                {
                    TickCheckbox(context, IdChkMustPayHoliday, false);
                },
                saveButtonName: "Save", needsExplicitClose: true);

            PerformEdit("TestCase08_EditRow3", existingName: "รูปแบบประมวลผล 3",
                newName: "P002", editAction: (context) =>
                {
                    TickCheckbox(context, IdChkPayHolidayWage, false);
                    TickCheckbox(context, IdChkPayHolidayOvertime, false);
                },
                saveButtonName: "Save and Close", needsExplicitClose: false);
        }

        private void PerformEdit(string testName, string existingName,
            string newName, Action<AutomationElement> editAction, string saveButtonName, bool needsExplicitClose)
        {
            try
            {
                Window form = ClickRowAndOpenEditForm(existingName, testName);
                var context = GetGeneralTab(form);

                FillTextBox(context, IdFormatName, newName);
                editAction(context);

                if (!TrySaveAndHandleDuplicate(form, testName, newName, saveButtonName))
                    return;

                if (needsExplicitClose)
                    form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();

                WaitForClosed(() => FindOpenPosttoPayrollSetupInfoForm());
                RefreshGrid();

                if (VerifyRecordExistsInGrid(newName, refreshGrid: false))
                    LogSuccessCounted(testName, $"แก้ไข '{existingName}' เป็น '{newName}' สำเร็จ (พบในตารางหลัก)");
                else
                    LogFailureCounted(testName, $"แก้ไข '{existingName}' เป็น '{newName}' แล้วแต่ไม่พบในตารางหลัก");
            }
            catch (Exception ex)
            {
                LogFailureCounted(testName, $"แก้ไข '{existingName}' ไม่สำเร็จ: {ex.Message}");
            }
        }

        // -------------------------------------------------------------
        // * 4. ทดสอบ การแจ้งเตือน Edit *
        // -------------------------------------------------------------
        public void RunEditValidationTests()
        {
            string baseTestName = "TestCase09_EditValidation";

            Window form;
            try
            {
                form = ClickRowAndOpenEditForm("รูปแบบประมวลผล 1", $"{baseTestName}_OpenForm");
            }
            catch (Exception ex)
            {
                LogFailureCounted($"{baseTestName}_OpenForm", $"เปิดฟอร์ม Edit ไม่สำเร็จ: {ex.Message}");
                return;
            }

            var context = GetGeneralTab(form);

            // 4.1 ลบชื่อรูปแบบประมวลผลออก -> Save -> แจ้งเตือนชื่อห้ามว่าง ไม่ปิดหน้าจอ
            RunValidationStep($"{baseTestName}_MissingName", () =>
            {
                FillTextBox(context, IdFormatName, "");
                ClickFormButton(form, "Save");
                AssertTaskResultWarning(form, "ชื่อรูปแบบการประมวลผล ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล", $"{baseTestName}_MissingName");
                AssertFormStillOpen(form, $"{baseTestName}_MissingName");
            });

            // 4.2 พิมพ์ชื่อซ้ำ -> ใช้ปุ่ม Save ในโหมดแก้ไขเพื่อให้การเตือนทำงานแน่นอน
            RunValidationStep($"{baseTestName}_DuplicateName", () =>
            {
                FillTextBox(context, IdFormatName, "รูปแบบประมวลผล 4");
                ClickFormButton(form, "Save");
                AssertTaskResultWarning(form, "ชื่อรูปแบบการประมวลผล ห้ามซ้ำ กรุณาป้อนข้อมูล", $"{baseTestName}_DuplicateName");
            });

            // 4.3 พิมพ์ "P001" + ติ๊กต้องการจ่ายค่าแรงวันหยุดนักขัตฤกษ์ แต่ไม่กรอกจำนวนวัน -> Save and New -> แจ้งเตือน
            RunValidationStep($"{baseTestName}_MissingHolidayDays", () =>
            {
                FillTextBox(context, IdFormatName, "P001");
                TickCheckbox(context, IdChkMustPayHoliday, true);
                FillTextBox(context, IdHolidayDays, "");
                ClickFormButton(form, "Save and New");
                AssertTaskResultWarning(form, "จำนวนวันจ่ายค่าแรงวันหยุดนักขัตฤกษ์ ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล", $"{baseTestName}_MissingHolidayDays");
            });

            // 4.4 ติ๊กรายวัน ไม่เลือกรายการ -> Save and New -> แจ้งเตือน
            RunValidationStep($"{baseTestName}_MissingDailyItem", () =>
            {
                FillTextBox(context, IdHolidayDays, "10");
                TickCheckbox(context, IdChkPayDaily, true);
                ClearLookupValue(context, IdLookupDailyItem);
                ClickFormButton(form, "Save and New");
                AssertTaskResultWarning(form, "รหัสรายได้พนักงานรายวัน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล", $"{baseTestName}_MissingDailyItem");
            });

            // 4.5 ติ๊กเพิ่มรายชั่วโมงด้วย -> Save and Close -> แจ้งเตือน
            string subCloseCase = $"{baseTestName}_DuplicateName";
            RunValidationStep($"{baseTestName}_MissingHourlyItem", () =>
            {
                TickCheckbox(context, IdChkPayHourly, true);
                ClickFormButton(form, "Save and Close");
                AssertTaskResultWarning(form, "รหัสรายได้พนักงานรายชั่วโมง ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล", $"{baseTestName}_MissingHourlyItem");
                AssertFormStillOpen(form, $"{baseTestName}_MissingHourlyItem");
            });

            // 4.6-4.8 กด Close -> Yes/Cancel/No บนฟอร์มเดิม
            RunValidationStep($"{subCloseCase}_CloseSequence", () =>
                RunCloseDialogSequence(form, subCloseCase, "รหัสรายได้พนักงานรายชั่วโมง ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล"));

            LogSuccessCounted(baseTestName, "รัน Validation Edit ครบทุกเคสแล้ว");
        }

        // -------------------------------------------------------------
        // * 5. ทดสอบ Delete *
        // -------------------------------------------------------------
        public void RunDeleteTests()
        {
            string baseTestName = "TestCase10_Delete";

            RunDeleteCase(baseTestName, name: "รูปแบบประมวลผล 4", confirmButton: "Yes", expectDeleted: true);
            RunDeleteCase(baseTestName, name: "P002", confirmButton: "No", expectDeleted: false);

            LogSuccessCounted(baseTestName, "ทดสอบ Delete ครบทุกเคส");
            TestLogger.LogInfo("PosttoPayrollSetupInfoUC",
                $"สรุปผลรวมทั้งหมด: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");
        }

        public void RunAllTestSteps()
        {
            NavigateToPosttoPayrollSetupInfoUC();
            RunAllValidationTests();
            RunEditTests();
            RunEditValidationTests();
            RunDeleteTests();
        }

        private void RunDeleteCase(string baseTestName, string name, string confirmButton, bool expectDeleted)
        {
            string testName = $"{baseTestName}_{name}_{confirmButton}";
            try
            {
                DeleteRecordAndVerify(name, confirmButton, testName, expectDeleted);
            }
            catch (Exception ex)
            {
                LogFailureCounted(testName, $"ลบ '{name}' ไม่สำเร็จ: {ex.Message}");
            }
        }

        // -------------------------------------------------------------
        // * Shared test-flow helpers *
        // -------------------------------------------------------------
        private bool TrySaveAndHandleDuplicate(Window form, string testName, string name, string saveButtonName)
        {
            ClickFormButton(form, saveButtonName);

            if (testName.Contains("Validation"))
                return true;

            string? duplicateReason = TryDetectDuplicateWarning(form);
            if (duplicateReason == null) return true;

            LogFailureCounted(testName, $"'{name}' ชื่อรูปแบบการประมวลผลซ้ำ: {duplicateReason}");
            try
            {
                ClickCloseAndHandleDialog(form, "No", $"{testName}_Close");
            }
            catch (Exception closeEx)
            {
                LogFailureCounted(testName, $"ปิดฟอร์มไม่สำเร็จ: {closeEx.Message}");
            }
            return false;
        }

        private void FillWageFormatFields(AutomationElement context, WageFormatRecord item)
        {
            FillTextBox(context, IdFormatName, item.Name);

            TickCheckbox(context, IdChkMustPayHoliday, true);
            FillTextBox(context, IdHolidayDays, item.HolidayDays);

            if (item.SelectDaily)
            {
                SelectLookupItemByLabel(context, LabelDailyWageLookup, "IN-001");
                TickCheckbox(context, IdChkPayDaily, true);
                SelectLookupItem(context, IdLookupDailyItem, "IN-001");
            }

            if (item.SelectHourly)
            {
                SelectLookupItemByLabel(context, LabelHourlyWageLookup, "IN-002");
                TickCheckbox(context, IdChkPayHourly, true);
                SelectLookupItem(context, IdLookupHourlyItem, "IN-002");
            }

            if (item.SelectDaily || item.SelectHourly)
            {
                TickCheckbox(context, IdChkPayHolidayWage, true);
                TickCheckbox(context, IdChkPayHolidayOvertime, true);
            }
        }

        private AutomationElement GetGeneralTab(Window form) =>
            form.FindFirstDescendant(cf => cf.ByAutomationId(GeneralTabId)) ?? form;

        private void ClickFormButton(Window form, string buttonName, int timeoutMs = 3000)
        {
            var button = Retry(() => form.FindFirstDescendant(cf => cf.ByName(buttonName)), timeoutMs)
                ?? throw new Exception($"หาปุ่ม '{buttonName}' บนฟอร์มไม่เจอ");
            button.Click();
        }

        private string? TryDetectDuplicateWarning(Window form)
        {
            var taskResultPanel = FindTaskResultPanel(form, timeoutMs: 1500);
            if (taskResultPanel == null) return null;

            foreach (var text in GetPanelTexts(taskResultPanel))
            {
                if (text.Contains("ชื่อรูปแบบการประมวลผล ห้ามซ้ำ")) return "ชื่อซ้ำ";
            }
            return null;
        }

        private AutomationElement? FindTaskResultPanel(Window form, int timeoutMs = 3000) =>
            Retry(() =>
                form.FindFirstDescendant(cf => cf.ByName("Task Result").Or(cf.ByAutomationId("dockPanelTaskResult")))
                ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("Task Result").Or(cf.ByAutomationId("dockPanelTaskResult"))),
                timeoutMs: timeoutMs);

        private static IEnumerable<string> GetPanelTexts(AutomationElement panel)
        {
            foreach (var el in panel.FindAllDescendants())
            {
                if (!string.IsNullOrEmpty(el.Name)) yield return el.Name;
                string? value = null;
                try { if (el.Patterns.Value.IsSupported) value = el.Patterns.Value.Pattern.Value.Value; }
                catch { }
                if (!string.IsNullOrEmpty(value)) yield return value;
            }
        }

        private void RefreshGrid()
        {
            try
            {
                var findBtn = _mainWindow.FindFirstDescendant(cf => cf.ByName("Find").Or(cf.ByAutomationId("btnFind")));
                findBtn?.Click();
            }
            catch { }
            try
            {
                var dataGrid = _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataGrid))
                               ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Table));
                if (dataGrid != null)
                {
                    WaitUntil(() => { try { dataGrid.Focus(); return true; } catch { return false; } }, 1000);
                    dataGrid.Click();
                }
            }
            catch { }
        }

        private bool VerifyRecordExistsInGrid(string value, int timeoutMs = 5000, bool refreshGrid = true)
        {
            if (refreshGrid) RefreshGrid();
            return Retry(() => FindElementMatchingValue(value), timeoutMs: timeoutMs) != null;
        }

        private AutomationElement? FindElementMatchingValue(string value)
        {
            foreach (var el in _mainWindow.FindAllDescendants())
            {
                if (!string.IsNullOrEmpty(el.Name) && string.Equals(el.Name.Trim(), value, StringComparison.OrdinalIgnoreCase))
                    return el;
                try
                {
                    if (el.Patterns.Value.IsSupported &&
                        string.Equals(el.Patterns.Value.Pattern.Value.Value.Trim(), value, StringComparison.OrdinalIgnoreCase))
                        return el;
                }
                catch { }
            }
            return null;
        }

        private Window GetOrOpenPosttoPayrollSetupInfoForm()
        {
            Window? form = Retry(FindOpenPosttoPayrollSetupInfoForm, 1500);
            if (form != null) return form;

            _mainWindow.FindFirstDescendant(cf => cf.ByName("New"))?.Click();
            form = Retry(FindOpenPosttoPayrollSetupInfoForm, timeoutMs: 8000);
            if (form == null) throw new Exception("หาฟอร์ม PosttoPayrollSetupInfoUC ไม่เจอ");
            WaitUntil(() => form.FindFirstDescendant(cf => cf.ByAutomationId(IdFormatName)) != null, 4000);
            return form;
        }

        private Window OpenPosttoPayrollSetupInfoForm()
        {
            _mainWindow.FindFirstDescendant(cf => cf.ByName("New"))?.Click();
            var form = Retry(FindOpenPosttoPayrollSetupInfoForm, timeoutMs: 8000) ?? throw new Exception("หาฟอร์ม PosttoPayrollSetupInfoUC ไม่เจอ");
            WaitUntil(() => form.FindFirstDescendant(cf => cf.ByAutomationId(IdFormatName)) != null, 4000);
            return form;
        }

        private Window? FindOpenPosttoPayrollSetupInfoForm() =>
            _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault()
            ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByAutomationId(FormId).Or(cf.ByName(FormTitle)))?.AsWindow();

        private Window ClickRowAndOpenEditForm(string nameToFind, string testName)
        {
            RefreshGrid();

            AutomationElement? row = Retry(() => FindElementMatchingValue(nameToFind), timeoutMs: 8000);
            if (row == null)
            {
                RefreshGrid();
                row = Retry(() => FindElementMatchingValue(nameToFind), timeoutMs: 8000);
            }

            if (row == null) throw new Exception($"หาแถวสำหรับ '{nameToFind}' ไม่เจอ");

            WaitUntil(() => { try { row.Focus(); return true; } catch { return false; } }, 1000);
            row.Click();

            var editButton = Retry(() =>
            {
                var b = _mainWindow.FindFirstDescendant(cf => cf.ByName("Edit"));
                return (b != null && b.Properties.IsEnabled.ValueOrDefault) ? b : null;
            }, 3000) ?? throw new Exception("หาปุ่ม Edit ไม่เจอ");

            editButton.Click();
            var editForm = Retry(FindOpenPosttoPayrollSetupInfoForm, 6000) ?? throw new Exception("หาฟอร์ม PosttoPayrollSetupInfoUC ไม่เจอ");
            WaitUntil(() => editForm.FindFirstDescendant(cf => cf.ByAutomationId(IdFormatName)) != null, 3000);
            return editForm;
        }

        private void DeleteRecordAndVerify(string name, string confirmButton, string testName, bool expectDeleted)
        {
            RefreshGrid();

            AutomationElement? row = Retry(() => FindElementMatchingValue(name), timeoutMs: 8000);
            if (row == null)
            {
                RefreshGrid();
                row = Retry(() => FindElementMatchingValue(name), timeoutMs: 8000);
            }

            if (row == null) throw new Exception($"หาแถวสำหรับลบ ({name}) ไม่เจอ");

            try { row.Focus(); } catch { }
            row.Click();

            var deleteButton = Retry(() =>
            {
                var b = _mainWindow.FindFirstDescendant(cf => cf.ByName("Delete"));
                return (b != null && b.Properties.IsEnabled.ValueOrDefault) ? b : null;
            }, 3000) ?? throw new Exception("หาปุ่ม Delete ไม่เจอ");
            deleteButton.Click();

            Window? confirmDialog = Retry(() =>
                _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault()
                ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("คำเตือน"))?.AsWindow(),
                timeoutMs: 4000);

            if (confirmDialog == null) throw new Exception("กด Delete แล้วไม่พบ Pop-up ยืนยัน");
            ClickDialogButton(confirmDialog, confirmButton, "ยืนยันการลบ");
            WaitForClosed(() => confirmDialog);

            bool stillExistsOnGrid = VerifyRecordExistsInGrid(name, timeoutMs: 3000, refreshGrid: false);
            bool hasSuccessMessage = FindTaskResultPanel(_mainWindow.AsWindow(), timeoutMs: 500)?
                .FindAllDescendants()
                .Any(e => !string.IsNullOrEmpty(e.Name) && e.Name.Contains("Deleted succeeded")) ?? false;

            if (expectDeleted)
            {
                if (stillExistsOnGrid && !hasSuccessMessage)
                {
                    string msg = $"'{name}' ลบข้อมูลไม่สำเร็จ";
                    LogFailureCounted(testName, msg);
                    throw new Exception(msg);
                }
                LogSuccessCounted(testName, $"'{name}' ถูกลบสำเร็จ");
            }
            else
            {
                if (!stillExistsOnGrid)
                {
                    string msg = $"'{name}' ลบข้อมูลผิดพลาด";
                    LogFailureCounted(testName, msg);
                    throw new Exception(msg);
                }
                LogSuccessCounted(testName, $"'{name}' ข้อมูลยังไม่ถูกลบ");
            }
        }

        private void ClickCloseAndHandleDialog(Window form, string buttonToClick, string testName)
        {
            form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();

            Window? confirmDialog = Retry(() =>
                form.ModalWindows.FirstOrDefault()
                ?? _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault()
                ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("คำเตือน"))?.AsWindow(),
                timeoutMs: 4000);

            if (confirmDialog == null) throw new Exception("กด Close แล้วไม่พบ Pop-up ยืนยัน");
            ClickDialogButton(confirmDialog, buttonToClick, "ยืนยัน");
            WaitForClosed(() => confirmDialog);
        }

        private void ClickDialogButton(Window dialog, string buttonName, string dialogDescription = "")
        {
            var btn = dialog.FindFirstDescendant(cf => cf.ByName(buttonName))?.AsButton()
                      ?? dialog.FindFirstDescendant(cf => cf.ByName($"&{buttonName}"))?.AsButton()
                      ?? throw new Exception($"ไม่พบปุ่ม '{buttonName}' บน Pop-up {dialogDescription}");
            btn.Click();
        }

        private void AssertFormStillOpen(Window form, string testName)
        {
            bool isOpen = WaitUntil(() => !form.Properties.IsOffscreen.ValueOrDefault && form.IsAvailable, 2000);
            if (!isOpen) throw new Exception("ฟอร์มถูกปิด");
            LogSuccessCounted(testName, "ฟอร์มถูกเปิดอยู่");
        }

        private void AssertFormClosed(Window form, string testName)
        {
            bool isClosed = WaitForClosed(() => FindOpenPosttoPayrollSetupInfoForm(), 3000);
            if (!isClosed) throw new Exception("ปิดฟอร์มไม่สำเร็จ");
            LogSuccessCounted(testName, "ฟอร์มถูกปิด");
        }

        private void AssertWarningAndFormOpen(Window form, string expectedWarningMessage, string testName)
        {
            bool foundMessage = false;
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < 5000)
            {
                try
                {
                    var taskResultPanel = FindTaskResultPanel(form, timeoutMs: 800);
                    if (taskResultPanel != null && GetPanelTexts(taskResultPanel).Any(t => t.Contains(expectedWarningMessage)))
                    {
                        foundMessage = true;
                        break;
                    }
                    if (GetPanelTexts(form).Any(t => t.Contains(expectedWarningMessage)))
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

        private void AssertMultipleTaskResultWarnings(Window form, string[] expectedMessages, string testName)
        {
            var pending = new HashSet<string>(expectedMessages);
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < 5000 && pending.Count > 0)
            {
                try
                {
                    var taskResultPanel = FindTaskResultPanel(form, timeoutMs: 600);
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

        private void AssertTaskResultWarning(Window form, string expectedWarningMessage, string testName)
        {
            bool foundMessage = false;
            var sw = Stopwatch.StartNew();
            List<string> lastSeenTexts = new();

            while (sw.ElapsedMilliseconds < 5000)
            {
                try
                {
                    var taskResultPanel = FindTaskResultPanel(form, timeoutMs: 800);
                    if (taskResultPanel != null)
                    {
                        var texts = GetPanelTexts(taskResultPanel).ToList();
                        if (texts.Count > 0) lastSeenTexts = texts;
                        if (texts.Any(t => t.Contains(expectedWarningMessage)))
                        {
                            foundMessage = true;
                            break;
                        }
                    }

                    if (GetPanelTexts(form).Any(t => t.Contains(expectedWarningMessage)))
                    {
                        foundMessage = true;
                        break;
                    }

                    var modal = form.ModalWindows.FirstOrDefault()
                        ?? _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault();
                    if (modal != null && GetPanelTexts(modal).Any(t => t.Contains(expectedWarningMessage)))
                    {
                        foundMessage = true;
                        break;
                    }

                    var warningPopup = _automation.GetDesktop().FindFirstChild(cf => cf.ByName("คำเตือน"))?.AsWindow();
                    if (warningPopup != null && GetPanelTexts(warningPopup).Any(t => t.Contains(expectedWarningMessage)))
                    {
                        foundMessage = true;
                        break;
                    }
                }
                catch { }
            }

            if (!foundMessage)
            {
                string seenDump = lastSeenTexts.Count > 0
                    ? string.Join(" | ", lastSeenTexts.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct())
                    : "(ไม่พบข้อความใดๆ ใน Task Result panel เลย)";
                throw new Exception($"ไม่พบข้อความ: '{expectedWarningMessage}' — ข้อความที่เจอจริงใน Task Result panel ตอนนั้น: [{seenDump}]");
            }
            LogSuccessCounted(testName, $"พบข้อความ: '{expectedWarningMessage}'");
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

        private void ClearLookupValue(AutomationElement context, string lookupId)
        {
            var lookup = context.FindFirstDescendant(cf => cf.ByAutomationId(lookupId))
                ?? throw new Exception($"หา lookup '{lookupId}' ไม่เจอ (Clear)");

            try
            {
                var txt = lookup.AsTextBox();
                if (txt != null && txt.Patterns.Value.IsSupported)
                {
                    txt.Focus();
                    txt.Enter("");
                    return;
                }
            }
            catch { }

            try
            {
                lookup.Focus();
                Keyboard.Press(VirtualKeyShort.CONTROL);
                Keyboard.Type("a");
                Keyboard.Release(VirtualKeyShort.CONTROL);
                Keyboard.Press(VirtualKeyShort.BACK);
                Keyboard.Press(VirtualKeyShort.DELETE);
                Keyboard.Press(VirtualKeyShort.TAB);
            }
            catch (Exception ex)
            {
                throw new Exception($"ล้างค่า lookup '{lookupId}' ไม่สำเร็จ: {ex.Message}");
            }
        }

        private void FillTextBox(AutomationElement context, string automationId, string text)
        {
            var txt = context.FindFirstDescendant(cf => cf.ByAutomationId(automationId))?.AsTextBox()
                ?? throw new Exception($"หาช่องกรอก '{automationId}' ไม่เจอ");

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

            if (!string.IsNullOrEmpty(text))
            {
                Keyboard.Type(text);
            }
        }

        private void TickCheckbox(AutomationElement context, string checkboxId, bool tick)
        {
            var found = context.FindFirstDescendant(cf => cf.ByAutomationId(checkboxId))
                ?? throw new Exception($"หา checkbox '{checkboxId}' ไม่เจอ");

            bool CurrentlyTicked()
            {
                var cb = found.AsCheckBox();
                if (cb?.IsChecked != null) return cb.IsChecked == true;
                return false;
            }

            if (CurrentlyTicked() == tick) return;

            try { found.Click(); } catch { }

            if (CurrentlyTicked() != tick)
            {
                try
                {
                    found.Focus();
                    Keyboard.Type(VirtualKeyShort.SPACE);
                }
                catch { }
            }
        }

        private void SelectLookupItem(AutomationElement context, string lookupId, string itemCode)
        {
            var lookup = context.FindFirstDescendant(cf => cf.ByAutomationId(lookupId))
                ?? throw new Exception($"หา lookup '{lookupId}' ไม่เจอ");

            OpenLookupPopupAndSelect(lookup, itemCode);
        }

        private void SelectLookupItemByLabel(AutomationElement context, string labelText, string? itemCode)
        {
            var label = context.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
                .FirstOrDefault(el => !string.IsNullOrEmpty(el.Name) && el.Name.Contains(labelText, StringComparison.OrdinalIgnoreCase))
                ?? throw new Exception($"หาป้ายข้อความ '{labelText}' ไม่เจอ");

            var labelRect = label.BoundingRectangle;

            var edit = context.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit))
                .Where(el => Math.Abs(el.BoundingRectangle.Top - labelRect.Top) < 20 && el.BoundingRectangle.Left >= labelRect.Left)
                .OrderBy(el => el.BoundingRectangle.Left)
                .FirstOrDefault()
                ?? throw new Exception($"หาช่องกรอกข้างป้าย '{labelText}' ไม่เจอ");

            var editRect = edit.BoundingRectangle;

            var ellipsisButton = context.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                .Where(b => Math.Abs(b.BoundingRectangle.Top - editRect.Top) < 15 && b.BoundingRectangle.Left >= editRect.Left)
                .OrderBy(b => b.BoundingRectangle.Left)
                .FirstOrDefault();

            OpenLookupPopupAndSelect(ellipsisButton ?? edit, itemCode);
        }

        private void OpenLookupPopupAndSelect(AutomationElement lookupOrButton, string? itemCode)
        {
            var ellipsisButton = lookupOrButton.Properties.ControlType.ValueOrDefault == ControlType.Button
                ? lookupOrButton
                : lookupOrButton.FindAllDescendants(cf => cf.ByControlType(ControlType.Button)).FirstOrDefault();

            try { lookupOrButton.Focus(); } catch { }

            if (ellipsisButton != null)
            {
                try
                {
                    Mouse.MoveTo(ellipsisButton.BoundingRectangle.Center());
                    Mouse.Click(MouseButton.Left);
                }
                catch
                {
                    try { ellipsisButton.Click(); } catch { lookupOrButton.Click(); }
                }
            }
            else
            {
                lookupOrButton.Click();
            }

            AutomationElement? popup = Retry(() =>
            {
                var modal = _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault();
                if (modal != null && modal.FindFirstDescendant(cf => cf.ByName("Select")) != null)
                    return (AutomationElement)modal;

                var desktop = _automation.GetDesktop();
                return desktop.FindAllChildren(cf => cf.ByControlType(ControlType.Window))
                    .FirstOrDefault(w => w.Properties.ProcessId.IsSupported
                        && !w.Equals(_mainWindow)
                        && w.FindFirstDescendant(cf => cf.ByName("Select")) != null);
            }, timeoutMs: 3000);

            AutomationElement searchRoot = popup ?? lookupOrButton;

            var selectButton = Retry(() => searchRoot.FindFirstDescendant(cf => cf.ByName("Select")), timeoutMs: 3000);

            if (selectButton != null)
            {
                try
                {
                    selectButton.Click();
                }
                catch
                {
                    try
                    {
                        Mouse.MoveTo(selectButton.BoundingRectangle.Center());
                        Mouse.Click(MouseButton.Left);
                    }
                    catch { }
                }
            }
            else
            {
                Func<AutomationElement, bool> rowMatches = itemCode == null
                    ? (_ => true)
                    : (row => row.FindAllDescendants()
                        .Any(cell => !string.IsNullOrEmpty(cell.Name) &&
                            cell.Name.Contains(itemCode, StringComparison.OrdinalIgnoreCase)));

                var matchingRow = Retry(() =>
                    searchRoot.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem))
                        .FirstOrDefault(rowMatches),
                    timeoutMs: 2000)
                    ?? Retry(() =>
                    searchRoot.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem))
                        .FirstOrDefault(rowMatches),
                    timeoutMs: 1000);

                if (matchingRow != null)
                {
                    try
                    {
                        Mouse.MoveTo(matchingRow.BoundingRectangle.Center());
                        Mouse.DoubleClick();
                    }
                    catch
                    {
                        try { matchingRow.Click(); } catch { }
                        Keyboard.Press(VirtualKeyShort.RETURN);
                    }
                }
                else if (itemCode != null)
                {
                    Keyboard.Type(itemCode);
                    Keyboard.Press(VirtualKeyShort.RETURN);
                }
            }

            if (popup != null)
            {
                WaitForClosed(() => popup);
            }
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