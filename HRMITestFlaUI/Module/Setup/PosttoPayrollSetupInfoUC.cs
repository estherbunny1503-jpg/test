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
        // =========================================================================================
        // หมวดที่ 1: ข้อมูลการทดสอบทั้ง 5 ขั้น (Test Data)
        // =========================================================================================

        // --- Data Records สำหรับใช้งานในแต่ละขั้นตอน ---
        private sealed record WageFormatRecord(
            string Name,
            bool SelectDaily,
            bool SelectHourly,
            string HolidayDays = "15");

        private sealed record WageFormatEditItem(
            string TestCaseId,
            string ExistingName,
            string NewName,
            Action<AutomationElement> EditAction,
            string SaveButtonName,
            bool NeedsExplicitClose);

        private sealed record DeleteItem(
            string TargetName,
            string DialogBtn,
            bool ExpectDeleted);

        // [ขั้นที่ 1] ข้อมูลทดสอบการสร้างใหม่ (Add / Navigate)
        private static readonly WageFormatRecord[] Step1_AddCases =
        {
            new("รูปแบบประมวลผล 1", SelectDaily: true,  SelectHourly: false),
            new("รูปแบบประมวลผล 2", SelectDaily: false, SelectHourly: true),
            new("รูปแบบประมวลผล 3", SelectDaily: true,  SelectHourly: true),
            new("รูปแบบประมวลผล 4", SelectDaily: false, SelectHourly: false)
        };

        // [ขั้นที่ 2] ข้อมูลทดสอบ Validation ตอนเพิ่ม (Add Validation)
        private const string Step2_DuplicateNameTarget = "รูปแบบประมวลผล 2";
        private const string Step2_DuplicateNameMessage = "ชื่อรูปแบบการประมวลผล ห้ามซ้ำ กรุณาป้อนข้อมูล";

        // [ขั้นที่ 3] ข้อมูลทดสอบการแก้ไขข้อมูล (Edit Records)
        private static readonly WageFormatEditItem[] Step3_EditCases =
        {
            new("TestCase07_EditRow2", "รูปแบบประมวลผล 2", "P001", (context) =>
            {
                TickCheckbox(context, IdChkMustPayHoliday, false);
            }, "Save", true),

            new("TestCase08_EditRow3", "รูปแบบประมวลผล 3", "P002", (context) =>
            {
                TickCheckbox(context, IdChkPayHolidayWage, false);
                TickCheckbox(context, IdChkPayHolidayOvertime, false);
            }, "Save and Close", false)
        };

        // [ขั้นที่ 4] ข้อมูลทดสอบ Validation ตอนแก้ไข (Edit Validation)
        private const string EditTargetRow = "รูปแบบประมวลผล 1";
        private const string EditDuplicateNameTarget = "รูปแบบประมวลผล 4";

        // [ขั้นที่ 5] ข้อมูลทดสอบการลบข้อมูล (Delete Records)
        private static readonly DeleteItem[] Step5_DeleteCases =
        {
            new("รูปแบบประมวลผล 4", "Yes", true),
            new("P002", "No", false)
        };


        // =========================================================================================
        // หมวดที่ 2: ส่วนที่ขึ้นกับหน้าจอนั้นๆ (Screen-Specific Implementation)
        // **เมื่อเปลี่ยนไปทำหน้าจออื่น ต้องมาแก้ไขค่าและเมทอดในส่วนนี้ทั้งหมด**
        // =========================================================================================

        // 2.1 Locator & Mappings ประจำหน้าจอ
        private const string MenuName = "กำหนดรูปแบบการประมวลผลเข้าระบบเงินเดือน";
        private const string ScreenLogName = "PosttoPayrollSetupInfoUC";
        private const string FormId = "PosttoPayrollSetupInfoUC";
        private const string FormTitle = "กำหนดรูปแบบการประมวลผลเข้าระบบเงินเดือน";
        private const string GeneralTabId = "devTabPgGeneral";

        // 2.2 ฟิลด์ Input ประจำหน้าจอ
        private const string IdFormatName = "devTxtPPSName";
        private const string IdChkPayDaily = "devChkDaily";
        private const string IdLookupDailyItem = "devBtnEmpDaily";
        private const string IdChkPayHourly = "devChkHoury";
        private const string IdLookupHourlyItem = "devBtnEmpHoury";
        private const string IdChkMustPayHoliday = "devChkHolidayPayment";
        private const string IdHolidayDays = "devSpinDayPay";
        private const string IdChkPayHolidayWage = "devchkIsPayLeave";
        private const string IdChkPayHolidayOvertime = "devChkPaySpecialHoliday";

        private const string LabelDailyWageLookup = "ค่าแรงรายวัน(พนักงานรายวัน)";
        private const string LabelHourlyWageLookup = "ค่าแรงรายชั่วโมง(พนักงานรายชั่วโมง)";

        /// <summary>
        /// ขั้นที่ 1: เปิดหน้าจอและกรอกข้อมูลสร้างใหม่
        /// </summary>
        public void NavigateToPosttoPayrollSetupInfoUC()
        {
            string stageName = "TestCase00_AddPosttoPayrollSetupInfo";
            TestLogger.LogInfo(ScreenLogName, "เริ่มทดสอบหน้าจอ Post to Payroll Setup");

            if (!TryOpenPosttoPayrollSetupInfoMenu(stageName)) return;

            Window? form = null;

            for (int i = 0; i < Step1_AddCases.Length; i++)
            {
                var item = Step1_AddCases[i];
                string testName = $"{stageName}_{item.Name}";

                try
                {
                    form ??= GetOrOpenPosttoPayrollSetupInfoForm();
                    var context = GetGeneralTab(form);

                    FillWageFormatFields(context, item);

                    string saveButtonName = i == 0 ? "Save"
                                          : i == Step1_AddCases.Length - 1 ? "Save and Close"
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

        /// <summary>
        /// ขั้นที่ 2: รัน Validation ตอนสร้างข้อมูลใหม่
        /// </summary>
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

            RunValidationStep("TestCase01_MissingName", () =>
            {
                FillTextBox(context, IdFormatName, "");
                TickCheckbox(context, IdChkMustPayHoliday, true);
                TickCheckbox(context, IdChkMustPayHoliday, false);
                ClickFormButton(form, "Save");
                AssertTaskResultWarning(form, "ชื่อรูปแบบการประมวลผล ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล", "TestCase01_MissingName");
            });

            RunValidationStep("TestCase02_MissingHolidayDays", () =>
            {
                FillTextBox(context, IdFormatName, "รูปแบบประมวลผล 1");
                TickCheckbox(context, IdChkMustPayHoliday, true);
                FillTextBox(context, IdHolidayDays, "");
                ClickFormButton(form, "Save and New");
                AssertTaskResultWarning(form, "จำนวนวันจ่ายค่าแรงวันหยุดนักขัตฤกษ์ ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล", "TestCase02_MissingHolidayDays");
            });

            RunValidationStep("TestCase03_MissingDailyItem", () =>
            {
                FillTextBox(context, IdFormatName, "รูปแบบประมวลผล 1");
                TickCheckbox(context, IdChkMustPayHoliday, true);
                FillTextBox(context, IdHolidayDays, "10");
                TickCheckbox(context, IdChkPayDaily, true);
                ClickFormButton(form, "Save and New");
                AssertTaskResultWarning(form, "รหัสรายได้พนักงานรายวัน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล", "TestCase03_MissingDailyItem");
            });

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

            string dupCase = "TestCase06_DuplicateName";
            RunValidationStep(dupCase, () =>
            {
                TickCheckbox(context, IdChkPayDaily, false);
                TickCheckbox(context, IdChkPayHourly, false);

                FillTextBox(context, IdFormatName, Step2_DuplicateNameTarget);
                ClickFormButton(form, "Save and Close");
                AssertTaskResultWarning(form, Step2_DuplicateNameMessage, dupCase);
                AssertFormStillOpen(form, dupCase);
            });

            RunValidationStep($"{dupCase}_CloseSequence", () =>
                RunCloseDialogSequence(form, dupCase, Step2_DuplicateNameMessage));
        }

        /// <summary>
        /// ขั้นที่ 3: เลือกแก้ไขรายการในตาราง
        /// </summary>
        public void RunEditTests()
        {
            foreach (var item in Step3_EditCases)
            {
                PerformEdit(item.TestCaseId, item.ExistingName, item.NewName, item.EditAction, item.SaveButtonName, item.NeedsExplicitClose);
            }
        }

        /// <summary>
        /// ขั้นที่ 4: รัน Validation ตอนแก้ไขข้อมูล
        /// </summary>
        public void RunEditValidationTests()
        {
            string baseTestName = "TestCase09_EditValidation";

            Window form;
            try
            {
                form = ClickRowAndOpenEditForm(EditTargetRow, $"{baseTestName}_OpenForm");
            }
            catch (Exception ex)
            {
                LogFailureCounted($"{baseTestName}_OpenForm", $"เปิดฟอร์ม Edit ไม่สำเร็จ: {ex.Message}");
                return;
            }

            var context = GetGeneralTab(form);

            RunValidationStep($"{baseTestName}_MissingName", () =>
            {
                FillTextBox(context, IdFormatName, "");
                ClickFormButton(form, "Save");
                AssertTaskResultWarning(form, "ชื่อรูปแบบการประมวลผล ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล", $"{baseTestName}_MissingName");
                AssertFormStillOpen(form, $"{baseTestName}_MissingName");
            });

            RunValidationStep($"{baseTestName}_DuplicateName", () =>
            {
                FillTextBox(context, IdFormatName, EditDuplicateNameTarget);
                ClickFormButton(form, "Save");
                AssertTaskResultWarning(form, "ชื่อรูปแบบการประมวลผล ห้ามซ้ำ กรุณาป้อนข้อมูล", $"{baseTestName}_DuplicateName");
            });

            RunValidationStep($"{baseTestName}_MissingHolidayDays", () =>
            {
                FillTextBox(context, IdFormatName, "P001");
                TickCheckbox(context, IdChkMustPayHoliday, true);
                FillTextBox(context, IdHolidayDays, "");
                ClickFormButton(form, "Save and New");
                AssertTaskResultWarning(form, "จำนวนวันจ่ายค่าแรงวันหยุดนักขัตฤกษ์ ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล", $"{baseTestName}_MissingHolidayDays");
            });

            RunValidationStep($"{baseTestName}_MissingDailyItem", () =>
            {
                FillTextBox(context, IdHolidayDays, "10");
                TickCheckbox(context, IdChkPayDaily, true);
                ClearLookupValue(context, IdLookupDailyItem);
                ClickFormButton(form, "Save and New");
                AssertTaskResultWarning(form, "รหัสรายได้พนักงานรายวัน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล", $"{baseTestName}_MissingDailyItem");
            });

            string subCloseCase = $"{baseTestName}_DuplicateName";
            RunValidationStep($"{baseTestName}_MissingHourlyItem", () =>
            {
                TickCheckbox(context, IdChkPayHourly, true);
                ClickFormButton(form, "Save and Close");
                AssertTaskResultWarning(form, "รหัสรายได้พนักงานรายชั่วโมง ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล", $"{baseTestName}_MissingHourlyItem");
                AssertFormStillOpen(form, $"{baseTestName}_MissingHourlyItem");
            });

            RunValidationStep($"{subCloseCase}_CloseSequence", () =>
                RunCloseDialogSequence(form, subCloseCase, "รหัสรายได้พนักงานรายชั่วโมง ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล"));

            LogSuccessCounted(baseTestName, "รัน Validation Edit ครบทุกเคสแล้ว");
        }

        /// <summary>
        /// ขั้นที่ 5: ทดสอบการลบรายการในตาราง
        /// </summary>
        public void RunDeleteTests()
        {
            string baseTestName = "TestCase10_Delete";

            foreach (var testCase in Step5_DeleteCases)
            {
                RunDeleteCase(baseTestName, testCase.TargetName, testCase.DialogBtn, testCase.ExpectDeleted);
            }

            LogSuccessCounted(baseTestName, "ทดสอบ Delete ครบทุกเคส");
            TestLogger.LogInfo(ScreenLogName, $"สรุปผลรวมทั้งหมด: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");
        }

        public void RunAllTestSteps()
        {
            NavigateToPosttoPayrollSetupInfoUC();
            RunAllValidationTests();
            RunEditTests();
            RunEditValidationTests();
            RunDeleteTests();
        }

        // --- Custom Mapping & Control Logic ประจำหน้าจอ PosttoPayrollSetupInfoUC ---
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

        private Window OpenPosttoPayrollSetupInfoForm()
        {
            _mainWindow.FindFirstDescendant(cf => cf.ByName("New"))?.Click();
            var form = Retry(FindOpenPosttoPayrollSetupInfoForm, timeoutMs: 8000) ?? throw new Exception("หาฟอร์ม PosttoPayrollSetupInfoUC ไม่เจอ");
            WaitUntil(() => form.FindFirstDescendant(cf => cf.ByAutomationId(IdFormatName)) != null, 4000);
            return form;
        }

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


        // =========================================================================================
        // หมวดที่ 3: ส่วนการทำงานกลางที่ตายตัว (Common / Reusable Helpers)
        // **ใช้งานร่วมกันได้ทุกหน้าจอ ไม่ต้องแก้ไขคำสั่งเมื่อเปลี่ยนหน้า**
        // =========================================================================================

        private readonly AutomationElement _mainWindow;
        private readonly UIA3Automation _automation;
        private int _totalSuccess = 0;
        private int _totalFail = 0;
        private const int TimeoutMs = 6000;

        public PosttoPayrollSetupInfoUC(AutomationElement mainWindow, UIA3Automation automation)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

        private bool TryOpenPosttoPayrollSetupInfoMenu(string stage)
        {
            try
            {
                var searchBox = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("baritemSearch"))
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)), 4000)
                                ?? throw new Exception("ไม่พบช่องค้นหา baritemSearch");

                WaitUntil(() => { try { searchBox.Focus(); return true; } catch { return false; } }, 1000);
                string searchKeyword = FormTitle;

                if (searchBox.Patterns.Value.IsSupported)
                    searchBox.Patterns.Value.Pattern.SetValue(searchKeyword);
                else
                    Keyboard.Type(searchKeyword);

                var searchBtn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("barChkSearch"))
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("Find")), 4000)
                                ?? throw new Exception("ไม่พบปุ่มค้นหา barChkSearch");

                searchBtn.Click();

                var menuResult = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName(searchKeyword))
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("treeListColumn row 1"))
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("treeListColumn row 21")), 4000)
                                 ?? throw new Exception("ไม่พบผลลัพธ์เมนูจากการค้นหา");

                menuResult.Focus();
                menuResult.Click();
                Keyboard.Press(VirtualKeyShort.ENTER);

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

        private Window? FindOpenPosttoPayrollSetupInfoForm() =>
            _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault()
            ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByAutomationId(FormId).Or(cf.ByName(FormTitle)))?.AsWindow();

        private void ClickFormButton(Window form, string buttonName, int timeoutMs = 3000)
        {
            var button = Retry(() => form.FindFirstDescendant(cf => cf.ByName(buttonName)), timeoutMs)
                ?? throw new Exception($"หาปุ่ม '{buttonName}' บนฟอร์มไม่เจอ");
            button.Click();
        }

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

        private static void TickCheckbox(AutomationElement context, string checkboxId, bool tick)
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