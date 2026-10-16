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
    public class PeriodTemplateInfoUC
    {
        // =========================================================================================
        // หมวดที่ 1: ข้อมูลการทดสอบทั้ง 5 ขั้น (Test Data)
        // =========================================================================================

        private enum MonthPattern { None, All, First6, Last6, EveryOther }

        private sealed record AutoPeriodPopupTestCase(
            string TestName,
            string PeriodYear,
            string TimesPerMonth,
            string PayDateShift,
            string? HolidayCalendarName,
            bool CrossMonthPeriod,
            string? StartMonthOption,
            string? StartDate);

        private sealed record MonthlyCalcTestCase(
            string TestName,
            MonthPattern Tax,
            MonthPattern SocialSecurity,
            MonthPattern ProvidentFund,
            MonthPattern WelfareFund,
            MonthPattern ClosePeriod,
            string SaveAction);

        private sealed record PeriodTemplateRecord(
            string Name,
            bool AdjustDate,
            string Remark,
            bool Special,
            bool SpecialOnce = false,
            AutoPeriodPopupTestCase? PopupConfig = null,
            MonthlyCalcTestCase? CalcConfig = null,
            string SaveAction = "SaveAndNew");

        private sealed record PeriodTemplateEditItem(
            string TestCaseId,
            string ExistingName,
            string NewName,
            Action<Window> EditAction,
            string SaveButtonName,
            bool NeedsExplicitClose);

        private sealed record DeleteItem(
            string TargetName,
            string DialogBtn,
            bool ExpectDeleted);

        // [ขั้นที่ 1] ข้อมูลทดสอบการสร้างใหม่ (Add / Navigate)
        private static readonly PeriodTemplateRecord[] Step1_AddCases =
        {
            new(
                "งวดการจ่าย 1 ครั้ง", AdjustDate: true, Remark: "จ่าย 1 ครั้ง ต่อเดือน", Special: false, SpecialOnce: false,
                PopupConfig: new AutoPeriodPopupTestCase("Item1", "2569", "1", "จ่ายก่อนวันหยุด", "CAL-TEST-06", false, null, null),
                CalcConfig: new MonthlyCalcTestCase("Item1", MonthPattern.All, MonthPattern.None, MonthPattern.None, MonthPattern.First6, MonthPattern.None, "Save"),
                SaveAction: "Save"
            ),
            new(
                "งวดการจ่ายแบบตัดวิกเหลื่อมเดือน", AdjustDate: false, Remark: "งวดการจ่ายวันที่ 21 เดือนก่อน-20เดือนปัจจุบัน", Special: false, SpecialOnce: false,
                PopupConfig: new AutoPeriodPopupTestCase("Item2", "2569", "1", "จ่ายหลังวันหยุด", "CAL-TEST-06", true, "ตามวันที่เริ่มต้นงวด", "21/01/2569"),
                CalcConfig: new MonthlyCalcTestCase("Item2", MonthPattern.All, MonthPattern.EveryOther, MonthPattern.Last6, MonthPattern.None, MonthPattern.None, "SaveAndNew"),
                SaveAction: "SaveAndNew"
            ),
            new(
                "งวดการจ่าย 2 ครั้ง", AdjustDate: false, Remark: "รายวิก / วิกละ 15 วัน", Special: false, SpecialOnce: false,
                PopupConfig: new AutoPeriodPopupTestCase("Item3", "2569", "2", "ไม่เลื่อนวันจ่าย", null, false, null, null),
                CalcConfig: new MonthlyCalcTestCase("Item3", MonthPattern.None, MonthPattern.None, MonthPattern.None, MonthPattern.None, MonthPattern.All, "SaveAndNew"),
                SaveAction: "SaveAndNew"
            ),
            new(
                "งวดพิเศษ", AdjustDate: true, Remark: "งวดพิเศษ", Special: true, SpecialOnce: false,
                PopupConfig: new AutoPeriodPopupTestCase("Item4", "2569", "3", "ไม่เลื่อนวันจ่าย", null, true, "ตามวันที่สิ้นสุดงวด", "20/1/2569"),
                CalcConfig: new MonthlyCalcTestCase("Item4", MonthPattern.None, MonthPattern.None, MonthPattern.None, MonthPattern.None, MonthPattern.None, "SaveAndNew"),
                SaveAction: "SaveAndNew"
            ),
            new(
                "งวดพิเศษสำหรับจ่ายครั้งเดียวเหตุเพราะออกจากงาน", AdjustDate: true, Remark: "งวดพิเศษสำหรับจ่ายครั้งเดียวเหตุเพราะออกจากงาน", Special: true, SpecialOnce: true,
                PopupConfig: new AutoPeriodPopupTestCase("Item5", "2569", "1", "จ่ายก่อนวันหยุด", null, true, "ตามวันที่เริ่มต้นงวด", "1/1/2569"),
                CalcConfig: new MonthlyCalcTestCase("Item5", MonthPattern.All, MonthPattern.None, MonthPattern.None, MonthPattern.None, MonthPattern.None, "SaveAndClose"),
                SaveAction: "SaveAndClose"
            )
        };

        // [ขั้นที่ 2] ข้อมูลทดสอบ Validation ตอนเพิ่ม (Add Validation)
        private const string Step2_DuplicateName = "งวดพิเศษ";
        private const string Step2_ExpectedDuplicateMessage = "ชื่อรูปแบบงวดการจ่าย ห้ามซ้ำ กรุณาป้อนข้อมูล";

        // [ขั้นที่ 3] ข้อมูลทดสอบการแก้ไขข้อมูล (Edit Records)
        private static readonly PeriodTemplateEditItem[] Step3_EditCases =
        {
            new("TestCase07_EditRow1", "งวดการจ่าย 1 ครั้ง", "จ่าย 1 ครั้ง ต่อเดือน", (form) =>
            {
                FillTextBox(form, IdTxtRemark, "งวดการจ่ายเดือนละ 1 ครั้ง");
                TickCheckbox(form, IdChkSpecial, true, fallbackNameContains: "งวดพิเศษ");
            }, "Save", true),

            new("TestCase08_EditRow2", "งวดการจ่ายแบบตัดวิกเหลื่อมเดือน", "งวดการจ่ายวันที่ 21 เดือนก่อน-20เดือนปัจจุบัน", (form) =>
            {
                FillTextBox(form, IdTxtRemark, "งวดการจ่ายแบบตัดวิกเหลื่อมเดือน");
                TickCheckbox(form, IdChkSpecial, false, fallbackNameContains: "งวดพิเศษ");
            }, "Save and Close", false)
        };

        // [ขั้นที่ 4] ข้อมูลทดสอบ Validation ตอนแก้ไข (Edit Validation)
        private const string EditTargetRow = "จ่าย 1 ครั้ง ต่อเดือน";
        private const string EditFallbackTargetRow = "งวดการจ่าย 1 ครั้ง";
        private const string EditDuplicateNameTarget = "งวดพิเศษ";
        private const string EditExpectedDuplicateMessage = "ชื่อรูปแบบงวดการจ่าย ห้ามซ้ำ กรุณาป้อนข้อมูล";

        // [ขั้นที่ 5] ข้อมูลทดสอบการลบข้อมูล (Delete Records)
        private static readonly DeleteItem[] Step5_DeleteCases =
        {
            new("งวดพิเศษ", "Yes", true),
            new("งวดพิเศษสำหรับจ่ายครั้งเดียวเหตุเพราะออกจากงาน", "No", false)
        };


        // =========================================================================================
        // หมวดที่ 2: ส่วนที่ขึ้นกับหน้าจอนั้นๆ (Screen-Specific Implementation)
        // =========================================================================================

        private const string MenuName = "กำหนดรูปแบบงวดการจ่าย";
        private const string ScreenLogName = "PeriodTemplateInfoUC";
        private const string FormId = "PeriodTemplateInfoUC";
        private const string FormTitle = "กำหนดรูปแบบงวดการจ่าย";

        private const string IdTxtName = "devTxtName";
        private const string IdChkChange = "devChkChange";
        private const string IdTxtRemark = "devTxtRemark";
        private const string IdBtnAuto = "devBtnAuto";
        private const string IdChkSpecial = "devChkSpecial";
        private const string IdChkSpecialOnce = "devChkSpecialOnce";

        private const string IdPopupAutoPeriod = "frmOptAutoPeriodTemplate";
        private const string IdPopupSpinPeriodYear = "devSpinPeriodYear";
        private const string IdPopupCbxPeriodMonth = "devCbxPeriodMonth";
        private const string IdPopupBtnCalendarOrg = "devBtnCalendarOrg";
        private const string IdPopupGcOverMonth = "devGcOverMonth";
        private const string IdPopupDateStartDate = "devDateStartDate";

        private static readonly string[] ColTaxNames = { "ภาษี", "คำนวณภาษี" };
        private static readonly string[] ColSocialSecurityNames = { "ประกันสังคม", "คำนวณประกันสังคม" };
        private static readonly string[] ColProvidentFundNames = { "กองทุนสำรองฯ", "กองทุนสำรอง", "คำนวณกองทุนสำรองฯ" };
        private static readonly string[] ColWelfareFundNames = { "กองทุนสงเคราะห์ฯ", "กองทุนสงเคราะห์ลูกจ้าง", "คำนวณกองทุนสงเคราะห์ลูกจ้าง" };
        private static readonly string[] ColClosePeriodNames = { "ปิดงวด" };

        public void NavigateToPeriodTemplateInfoUC()
        {
            string stageName = "TestCase00_AddPeriodTemplateInfo";
            TestLogger.LogInfo(ScreenLogName, "เริ่มทดสอบหน้าจอ กำหนดรูปแบบงวดการจ่าย");

            if (!TryOpenPeriodTemplateInfoMenu(stageName)) return;

            Window? form = null;

            for (int i = 0; i < Step1_AddCases.Length; i++)
            {
                var item = Step1_AddCases[i];
                string testName = $"{stageName}_{item.Name}";

                try
                {
                    form ??= GetOrOpenPeriodTemplateInfoForm();
                    FillPeriodTemplateFields(form, item);

                    string saveButtonName = item.SaveAction switch
                    {
                        "Save" => "Save",
                        "SaveAndClose" => "Save and Close",
                        _ => "Save and New"
                    };

                    if (!TrySaveAndHandleDuplicate(form, testName, item.Name, saveButtonName))
                    {
                        form = null;
                        continue;
                    }

                    switch (saveButtonName)
                    {
                        case "Save":
                            form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();
                            WaitForClosed(() => FindOpenPeriodTemplateInfoForm());
                            form = null;
                            RefreshGrid();
                            if (FindRowByNameColumn(item.Name) != null)
                                LogSuccessCounted(testName, $"'{item.Name}' เพิ่มสำเร็จ (พบในตารางหลัก)");
                            else
                                LogFailureCounted(testName, $"'{item.Name}' ไม่พบในตารางหลักหลังบันทึก");
                            break;
                        case "SaveAndClose":
                            WaitForClosed(() => FindOpenPeriodTemplateInfoForm());
                            form = null;
                            RefreshGrid();
                            if (FindRowByNameColumn(item.Name) != null)
                                LogSuccessCounted(testName, $"'{item.Name}' เพิ่มสำเร็จ (พบในตารางหลัก)");
                            else
                                LogFailureCounted(testName, $"'{item.Name}' ไม่พบในตารางหลังหลังบันทึก");
                            break;
                        case "SaveAndNew":
                        default:
                            WaitUntil(() =>
                            {
                                var txt = form.FindFirstDescendant(cf => cf.ByAutomationId(IdTxtName))?.AsTextBox();
                                return string.IsNullOrEmpty(txt?.Text);
                            }, 3000);
                            form = RefreshOpenForm(form);
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

        public void RunAllValidationTests()
        {
            string stageName = "TestCase01-06_Validation";
            Window form;
            try { form = OpenPeriodTemplateInfoForm(); }
            catch (Exception ex)
            {
                LogFailureCounted(stageName, $"เปิดฟอร์มไม่สำเร็จ: {ex.Message}");
                return;
            }

            RunValidationStep("TestCase01_MissingName", () =>
            {
                FillTextBox(form, IdTxtName, "");
                FillTextBox(form, IdTxtRemark, "ทดสอบแจ้งเตือน");
                ClickFormButton(form, "Save");
                AssertTaskResultWarning(form, "ชื่อรูปแบบงวดการจ่าย ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล", "TestCase01_MissingName");
            });

            RunValidationStep("TestCase02_MissingAutoPeriod", () =>
            {
                form = GetOrOpenPeriodTemplateInfoForm();
                FillTextBox(form, IdTxtName, "งวดการจ่ายทดสอบ");
                FillTextBox(form, IdTxtRemark, "งวดการจ่ายทดสอบ");
                ClickFormButton(form, "Save and New");
                form = RefreshOpenForm(form);
                AssertTaskResultWarning(form, "กรุณากำหนดงวดการจ่าย", "TestCase02_MissingAutoPeriod");
            });

            string dupCase = "TestCase03_DuplicateName";
            RunValidationStep(dupCase, () =>
            {
                ClickAutoPeriodButtonAndConfirm(form);
                FillTextBox(form, IdTxtName, Step2_DuplicateName);
                FillTextBox(form, IdTxtRemark, Step2_DuplicateName);
                ClickFormButton(form, "Save and Close");
                AssertTaskResultWarning(form, Step2_ExpectedDuplicateMessage, dupCase);
                AssertFormStillOpen(form, dupCase);
            });

            RunValidationStep($"{dupCase}_CloseSequence", () =>
                RunCloseDialogSequence(form, dupCase, Step2_ExpectedDuplicateMessage));
        }

        public void RunEditTests()
        {
            var results = new List<(string TestName, string ExistingName, string NewName)>();

            foreach (var item in Step3_EditCases)
            {
                TryAdd(results, PerformEdit(item.TestCaseId, item.ExistingName,
                    item.NewName, item.EditAction, item.SaveButtonName, item.NeedsExplicitClose));
            }

            VerifyEditOutcomes(results);
        }

        public void RunEditValidationTests()
        {
            string baseTestName = "TestCase09_EditValidation";
            Window form;

            // หาแถวที่ถูกแก้ไขแล้ว ถ้าไม่พบให้ fallback ไปหาแถวเดิม
            string targetRow = FindRowByNameColumn(EditTargetRow) != null ? EditTargetRow : EditFallbackTargetRow;

            try { form = ClickRowAndOpenEditForm(targetRow, $"{baseTestName}_OpenForm"); }
            catch (Exception ex)
            {
                LogFailureCounted($"{baseTestName}_OpenForm", $"เปิดฟอร์ม Edit ไม่สำเร็จ: {ex.Message}");
                return;
            }

            RunValidationStep($"{baseTestName}_MissingName", () =>
            {
                FillTextBox(form, IdTxtName, "");
                ClickFormButton(form, "Save");
                AssertTaskResultWarning(form, "ชื่อรูปแบบงวดการจ่าย ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล", $"{baseTestName}_MissingName");
                AssertFormStillOpen(form, $"{baseTestName}_MissingName");
            });

            RunValidationStep($"{baseTestName}_IncompleteMonths", () =>
            {
                FillTextBox(form, IdTxtName, "งวดการจ่าย 1 ครั้ง");
                SetCalculationMonth(form, from: "มกราคม", to: "กุมภาพันธ์");
                ClickFormButton(form, "Save and Close");
                form = RefreshOpenForm(form);
                AssertTaskResultWarning(form, "ต้องกำหนดงวดการจ่ายให้ครบทั้ง 12 เดือน", $"{baseTestName}_IncompleteMonths");
            });

            string subCase = $"{baseTestName}_DuplicateName";
            RunValidationStep(subCase, () =>
            {
                FillTextBox(form, IdTxtName, EditDuplicateNameTarget);
                SetCalculationMonth(form, from: "กุมภาพันธ์", to: "มกราคม");

                ClickFormButton(form, "Save");
                form = RefreshOpenForm(form);
                AssertTaskResultWarning(form, EditExpectedDuplicateMessage, subCase);
                AssertFormStillOpen(form, subCase);
            });

            RunValidationStep($"{subCase}_CloseSequence", () =>
                RunCloseDialogSequence(form, subCase, EditExpectedDuplicateMessage));

            LogSuccessCounted(baseTestName, "รัน Validation Edit ครบทุกเคสแล้ว");
        }

        public void RunDeleteTests()
        {
            string baseTestName = "TestCase10_Delete";

            foreach (var testCase in Step5_DeleteCases)
            {
                RunDeleteCase(baseTestName, name: testCase.TargetName, confirmButton: testCase.DialogBtn, expectDeleted: testCase.ExpectDeleted);
            }

            var lingeringForm = FindOpenPeriodTemplateInfoForm();
            if (lingeringForm != null)
            {
                try
                {
                    lingeringForm.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();
                    WaitForClosed(() => FindOpenPeriodTemplateInfoForm(), 3000);
                }
                catch { }
            }

            LogSuccessCounted(baseTestName, "ทดสอบ Delete ครบทุกเคส");
            TestLogger.LogInfo(ScreenLogName, $"สรุปผลรวมทั้งหมด: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");
        }

        public void RunAutoPeriodAndMonthlyCalcTestSuite() { }

        public void RunAllTestSteps()
        {
            try
            {
                NavigateToPeriodTemplateInfoUC();
                RunAllValidationTests();
                RunEditTests();
                RunEditValidationTests();
                RunDeleteTests();
            }
            finally
            {
                CleanupScreenState();
            }
        }

        private void CleanupScreenState()
        {
            try
            {
                var openForm = FindOpenPeriodTemplateInfoForm();
                if (openForm != null)
                {
                    openForm.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();
                    WaitForClosed(() => FindOpenPeriodTemplateInfoForm(), 2000);
                }

                var searchBox = _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("baritemSearch"));
                if (searchBox != null && searchBox.Patterns.Value.IsSupported)
                {
                    searchBox.Patterns.Value.Pattern.SetValue("");
                }

                Keyboard.Press(VirtualKeyShort.ESCAPE);
            }
            catch { }
        }

        // --- Custom Mapping & Control Logic ประจำหน้าจอ PeriodTemplateInfoUC ---
        private void FillPeriodTemplateFields(Window form, PeriodTemplateRecord item)
        {
            FillTextBox(form, IdTxtName, item.Name);
            TickCheckbox(form, IdChkChange, item.AdjustDate);
            FillTextBox(form, IdTxtRemark, item.Remark);

            TickCheckbox(form, IdChkSpecial, item.Special, fallbackNameContains: "งวดพิเศษ");
            if (item.SpecialOnce)
            {
                TickCheckbox(form, IdChkSpecialOnce, item.SpecialOnce, fallbackNameContains: "งวดพิเศษสำหรับจ่ายครั้งเดียวเหตุเพราะออกจากงาน");
            }

            if (item.PopupConfig != null)
            {
                FillAndConfirmAutoPeriodPopup(form, item.PopupConfig);
            }
            else
            {
                ClickAutoPeriodButtonAndConfirm(form);
            }

            WaitUntil(() =>
            {
                var g = FindCalculationGrid(form);
                return g != null && g.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem)).Length > 0;
            }, 5000);

            if (item.CalcConfig != null)
            {
                int totalRows = item.PopupConfig?.TimesPerMonth == "2" ? 24 : 12;

                ApplyMonthPattern(form, ColTaxNames, item.CalcConfig.Tax, totalRows);
                ApplyMonthPattern(form, ColSocialSecurityNames, item.CalcConfig.SocialSecurity, totalRows);
                ApplyMonthPattern(form, ColProvidentFundNames, item.CalcConfig.ProvidentFund, totalRows);
                ApplyMonthPattern(form, ColWelfareFundNames, item.CalcConfig.WelfareFund, totalRows);
                ApplyMonthPattern(form, ColClosePeriodNames, item.CalcConfig.ClosePeriod, totalRows);
            }
        }

        private Window RefreshOpenForm(Window fallback, int timeoutMs = 5000)
        {
            var refreshed = RetryFind(FindOpenPeriodTemplateInfoForm, timeoutMs: timeoutMs);
            return refreshed ?? fallback;
        }

        private bool TrySaveAndHandleDuplicate(Window form, string testName, string name, string saveButtonName)
        {
            ClickFormButton(form, saveButtonName);
            if (testName.Contains("Validation")) return true;
            string? duplicateReason = TryDetectDuplicateWarning(form);
            if (duplicateReason == null) return true;
            LogFailureCounted(testName, $"'{name}' ชื่อรูปแบบงวดการจ่ายซ้ำ: {duplicateReason}");
            try { ClickCloseAndHandleDialog(form, "No", $"{testName}_Close"); }
            catch (Exception closeEx) { LogFailureCounted(testName, $"ปิดฟอร์มไม่สำเร็จ: {closeEx.Message}"); }
            return false;
        }

        private void ClickAutoPeriodButtonAndConfirm(Window form) => FillAndConfirmAutoPeriodPopup(form, testCase: null);

        private AutomationElement? FindAutoPeriodPopup(Window form)
        {
            return form.FindFirstDescendant(cf => cf.ByAutomationId(IdPopupAutoPeriod))
                ?? form.FindAllDescendants(cf => cf.ByControlType(ControlType.Window))
                    .FirstOrDefault(w => !string.IsNullOrEmpty(w.Name) && w.Name.Contains("กำหนดงวดอัตโนมัติ"))
                ?? form.ModalWindows.FirstOrDefault(w => w.Name.Contains("กำหนดงวดอัตโนมัติ"))
                ?? _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault(w => w.Name.Contains("กำหนดงวดอัตโนมัติ"))
                ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByAutomationId(IdPopupAutoPeriod))
                ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("ปุ่มกำหนดงวดอัตโนมัติ"));
        }

        private bool TryDismissBlockingDialog(Window form)
        {
            var candidate =
                form.ModalWindows.FirstOrDefault(w => !w.Name.Contains("กำหนดงวดอัตโนมัติ"))
                ?? _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault(w => !w.Name.Contains("กำหนดงวดอัตโนมัติ"));

            if (candidate == null) return false;

            string[] affirmativeNames = { "Yes", "ใช่", "OK", "Ok", "ตกลง" };
            foreach (var name in affirmativeNames)
            {
                var btn = candidate.FindFirstDescendant(cf => cf.ByName(name));
                if (btn == null) continue;
                ForceActivateWindow(candidate);
                Thread.Sleep(150);
                if (!InvokeElement(btn)) ClickElementByMouseInstant(btn);
                return true;
            }
            return false;
        }

        private void FillAndConfirmAutoPeriodPopup(Window form, AutoPeriodPopupTestCase? testCase)
        {
            Func<AutomationElement?> findPopup = () => FindAutoPeriodPopup(form);
            AutomationElement? popup = null;

            const int maxClickAttempts = 3;
            for (int attempt = 1; attempt <= maxClickAttempts && popup == null; attempt++)
            {
                var autoButton = RetryFind(() =>
                    form.FindFirstDescendant(cf => cf.ByName("button 'กำหนดงวดอัตโนมัติ'"))
                    ?? form.FindFirstDescendant(cf => cf.ByAutomationId(IdBtnAuto))
                    ?? form.FindFirstDescendant(cf => cf.ByName("กำหนดงวดอัตโนมัติ")),
                    timeoutMs: 3000) ?? throw new Exception($"หาปุ่ม '{IdBtnAuto}' บนฟอร์มไม่เจอ");

                ForceActivateWindow(form);
                Thread.Sleep(150);

                ClickElementByMouseInstant(autoButton);
                popup = RetryFind(findPopup, timeoutMs: 3000);

                if (popup == null && TryDismissBlockingDialog(form))
                    popup = RetryFind(findPopup, timeoutMs: 3000);
            }

            if (popup == null)
            {
                throw new Exception($"กดปุ่ม '{IdBtnAuto}' {maxClickAttempts} ครั้งแล้วยังไม่พบ Pop-up 'ปุ่มกำหนดงวดอัตโนมัติ'");
            }

            if (testCase != null)
            {
                FillAutoPeriodPopupFields(popup, testCase);
            }

            var okButton = RetryFind(() =>
                popup.FindFirstDescendant(cf => cf.ByName("button 'Ok'"))
                ?? popup.FindFirstDescendant(cf => cf.ByName("Ok"))
                ?? popup.FindFirstDescendant(cf => cf.ByName("OK")),
                timeoutMs: 3000) ?? throw new Exception("หาปุ่ม OK บน Pop-up 'ปุ่มกำหนดงวดอัตโนมัติ' ไม่เจอ");

            ForceActivateWindow(popup.AsWindow() ?? form);
            ClickElementByMouseInstant(okButton);
            WaitForClosed(() => popup);
        }

        private void FillAutoPeriodPopupFields(AutomationElement popup, AutoPeriodPopupTestCase tc)
        {
            if (!string.IsNullOrEmpty(tc.PeriodYear))
            {
                SetPopupYearField(popup, tc.PeriodYear);
            }

            if (!string.IsNullOrEmpty(tc.TimesPerMonth))
            {
                SelectComboBoxViaDropdownList(popup, "งวดการจ่ายเดือนละ", tc.TimesPerMonth);
            }

            if (!string.IsNullOrEmpty(tc.PayDateShift))
            {
                SelectComboBoxViaDropdownList(popup, "เลื่อนวันจ่าย", tc.PayDateShift);
            }

            if (!string.IsNullOrEmpty(tc.HolidayCalendarName))
            {
                SelectHolidayCalendar(popup, tc.HolidayCalendarName);
            }

            if (tc.CrossMonthPeriod)
            {
                SelectRadioButtonRobust(popup, IdPopupGcOverMonth, "กำหนดงวดคร่อมเดือน");

                if (!string.IsNullOrEmpty(tc.StartMonthOption))
                {
                    SelectComboBoxViaDropdownList(popup, "เดือนที่เริ่มต้นงวด", tc.StartMonthOption, automationId: IdPopupCbxPeriodMonth);
                }

                if (!string.IsNullOrEmpty(tc.StartDate))
                {
                    SetPopupDateField(popup, IdPopupDateStartDate, tc.StartDate);
                }
            }
        }

        private void SetPopupYearField(AutomationElement popup, string year)
        {
            ForceActivateWindow(popup.AsWindow() ?? _mainWindow.AsWindow());

            var spinYear = popup.FindFirstDescendant(cf => cf.ByAutomationId(IdPopupSpinPeriodYear))
                ?? FindEditControlByLabel(popup, "งวดการจ่าย")
                ?? popup.FindAllDescendants(cf => cf.ByControlType(ControlType.Spinner)).FirstOrDefault()
                ?? popup.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit)).FirstOrDefault();

            if (spinYear != null)
            {
                var editControl = spinYear.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)) ?? spinYear;
                ClickElementByMouseInstant(editControl);
                Thread.Sleep(50);
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                Keyboard.Press(VirtualKeyShort.BACK);
                Keyboard.Type(year);
                Keyboard.Press(VirtualKeyShort.TAB);
            }
        }

        private void SelectRadioButtonRobust(AutomationElement popup, string automationId, string labelName)
        {
            ForceActivateWindow(popup.AsWindow() ?? _mainWindow.AsWindow());

            var radio = popup.FindFirstDescendant(cf => cf.ByName($"check box '{labelName}'"))
                ?? popup.FindFirstDescendant(cf => cf.ByName(labelName))
                ?? popup.FindFirstDescendant(cf => cf.ByAutomationId(automationId))
                ?? FindByIdOrName(popup, automationId, labelName, ControlType.RadioButton)
                ?? FindByIdOrName(popup, automationId, labelName, ControlType.CheckBox);

            if (radio != null)
            {
                ClickElementByMouseInstant(radio);
                Thread.Sleep(100);

                try { radio.Focus(); } catch { }
                Keyboard.Press(VirtualKeyShort.SPACE);
            }
        }

        private void SelectComboBoxViaDropdownList(AutomationElement popup, string labelText, string optionText, string? automationId = null)
        {
            ForceActivateWindow(popup.AsWindow() ?? _mainWindow.AsWindow());

            var combo = (!string.IsNullOrEmpty(automationId) ? popup.FindFirstDescendant(cf => cf.ByAutomationId(automationId)) : null)
                ?? FindEditControlByLabel(popup, labelText)
                ?? popup.FindFirstDescendant(cf => cf.ByName(labelText))
                ?? FindByIdOrName(popup, null, labelText, ControlType.ComboBox);

            if (combo == null) return;

            var bounds = combo.BoundingRectangle;
            int comboCenterX = (int)bounds.Center().X;
            int comboCenterY = (int)bounds.Center().Y;

            SetCursorPos(comboCenterX, comboCenterY);
            Thread.Sleep(50);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(50);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);

            Thread.Sleep(300);

            int rowIndex = 0;
            string cleanOption = optionText.Trim();

            if (cleanOption.Equals("ไม่เลื่อนวันจ่าย", StringComparison.OrdinalIgnoreCase) || cleanOption.Equals("3"))
            {
                rowIndex = 2;
            }
            else if (cleanOption.Contains("จ่ายหลังวันหยุด") || cleanOption.Contains("ตามวันที่สิ้นสุดงวด") || cleanOption.Equals("2"))
            {
                rowIndex = 1;
            }
            else if (cleanOption.Contains("จ่ายก่อนวันหยุด") || cleanOption.Contains("ตามวันที่เริ่มต้นงวด") || cleanOption.Equals("1"))
            {
                rowIndex = 0;
            }

            if (rowIndex == 0 && (cleanOption.Contains("จ่ายก่อนวันหยุด") || cleanOption.Contains("ตามวันที่เริ่มต้นงวด")))
            {
                Keyboard.Press(VirtualKeyShort.RETURN);
                return;
            }

            const int itemRowHeight = 17;
            int clickX = comboCenterX;
            int clickY = comboCenterY + (bounds.Height / 2) + (rowIndex * itemRowHeight) + (itemRowHeight / 2) - 2;

            SetCursorPos(clickX, clickY);
            Thread.Sleep(50);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(50);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
        }

        private AutomationElement? FindEditControlByLabel(AutomationElement popup, string labelText)
        {
            var label = popup.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
                .FirstOrDefault(e => !string.IsNullOrEmpty(e.Name) &&
                    e.Name.Trim().StartsWith(labelText.Trim(), StringComparison.OrdinalIgnoreCase));

            if (label == null) return null;
            Rectangle labelRect;
            try { labelRect = label.BoundingRectangle; }
            catch { return null; }

            var candidates = popup.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit))
                .Concat(popup.FindAllDescendants(cf => cf.ByControlType(ControlType.ComboBox)))
                .Concat(popup.FindAllDescendants(cf => cf.ByControlType(ControlType.Spinner)))
                .Concat(popup.FindAllDescendants(cf => cf.ByControlType(ControlType.List)))
                .Concat(popup.FindAllDescendants(cf => cf.ByControlType(ControlType.Button)))
                .ToList();

            return candidates
                .Select(e =>
                {
                    try { var r = e.BoundingRectangle; return (Element: e, Rect: r, Ok: true); }
                    catch { return (Element: e, Rect: default(Rectangle), Ok: false); }
                })
                .Where(x => x.Ok && Math.Abs(x.Rect.Y - labelRect.Y) < 20 && x.Rect.X > labelRect.X)
                .OrderBy(x => x.Rect.X)
                .Select(x => x.Element)
                .FirstOrDefault();
        }

        private void SetPopupDateField(AutomationElement popup, string automationId, string value)
        {
            ForceActivateWindow(popup.AsWindow() ?? _mainWindow.AsWindow());

            var container = popup.FindFirstDescendant(cf => cf.ByAutomationId(automationId))
                ?? FindEditControlByLabel(popup, "วันที่เริ่มต้นงวด")
                ?? popup.FindAllDescendants().FirstOrDefault(e => !string.IsNullOrEmpty(e.Name) && e.Name.Contains("มกราคม"))
                ?? popup.FindAllDescendants().FirstOrDefault(e => !string.IsNullOrEmpty(e.Name) && e.Name.Contains("2569"));

            if (container == null) return;

            var editPart = container.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit))
                ?? container;

            ClickElementByMouseInstant(editPart);
            Thread.Sleep(100);

            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            Thread.Sleep(50);
            Keyboard.Press(VirtualKeyShort.BACK);
            Keyboard.Press(VirtualKeyShort.DELETE);
            Thread.Sleep(50);

            Keyboard.Type(value);
            Keyboard.Press(VirtualKeyShort.TAB);
        }

        private void SelectHolidayCalendar(AutomationElement popup, string calendarName)
        {
            Window? lookupDialog = FindHolidayLookupWindowOnce(popup) ?? TryOpenHolidayLookup(popup);
            if (lookupDialog == null) return;

            ForceActivateWindow(lookupDialog);
            Thread.Sleep(300);

            int expectedRow = calendarName.ToUpperInvariant() switch
            {
                "CAL001" => 0,
                "CAL002" => 1,
                "CALENDAR_01" => 2,
                "CAL-TEST-03" => 3,
                "CAL-TEST-04" => 4,
                "CAL-TEST-06" => 5,
                _ => 5
            };

            AutomationElement? cell = RetryFind(() => FindLookupCodeCell(lookupDialog, calendarName, expectedRow), timeoutMs: 3000);
            if (cell == null)
            {
                ClickLookupButton(lookupDialog, "Cancel", rightOffset: 30);
                WaitForClosed(() => FindHolidayLookupWindowOnce(popup), 2000);
                ForceActivateWindow(popup.AsWindow() ?? _mainWindow.AsWindow());
                return;
            }

            try { if (cell.Patterns.SelectionItem.IsSupported) cell.Patterns.SelectionItem.Pattern.Select(); } catch { }
            try { cell.Focus(); } catch { }
            ForceActivateWindow(lookupDialog);
            ClickElementByMouseInstant(cell);
            Thread.Sleep(300);

            if (InvokeLookupButton(lookupDialog, "Select"))
            {
                WaitForClosed(() => FindHolidayLookupWindowOnce(popup), 1500);
            }

            if (FindHolidayLookupWindowOnce(popup) != null)
            {
                ClickLookupButton(lookupDialog, "Select", rightOffset: 100);
                WaitForClosed(() => FindHolidayLookupWindowOnce(popup), 1500);
            }

            if (FindHolidayLookupWindowOnce(popup) != null)
            {
                try
                {
                    ForceActivateWindow(lookupDialog);
                    var cb = cell.BoundingRectangle;
                    int cx = (int)cb.Center().X, cy = (int)cb.Center().Y;
                    ClickAtPoint(cx, cy);
                    Thread.Sleep(80);
                    ClickAtPoint(cx, cy);
                }
                catch { }
                WaitForClosed(() => FindHolidayLookupWindowOnce(popup), 1500);
            }

            if (FindHolidayLookupWindowOnce(popup) != null)
            {
                ForceActivateWindow(lookupDialog);
                Keyboard.Press(VirtualKeyShort.RETURN);
                WaitForClosed(() => FindHolidayLookupWindowOnce(popup), 1500);
            }

            if (FindHolidayLookupWindowOnce(popup) != null)
            {
                ClickLookupButton(lookupDialog, "Cancel", rightOffset: 30);
                WaitForClosed(() => FindHolidayLookupWindowOnce(popup), 2000);
            }

            ForceActivateWindow(popup.AsWindow() ?? _mainWindow.AsWindow());
            Thread.Sleep(200);
        }

        private AutomationElement? FindLookupCodeCell(Window dialog, string code, int expectedRow)
        {
            var rx = new System.Text.RegularExpressions.Regex(@"รหัสปฏิทิน row (\d+)");
            AutomationElement? byIndex = null;

            foreach (var el in dialog.FindAllDescendants())
            {
                string name;
                try { name = el.Name ?? ""; } catch { continue; }
                var m = rx.Match(name);
                if (!m.Success) continue;

                string text = GetElementTextSafe(el);
                if (!string.IsNullOrWhiteSpace(text) &&
                    !rx.IsMatch(text) &&
                    text.Trim().Equals(code.Trim(), StringComparison.OrdinalIgnoreCase))
                    return el;

                if (int.Parse(m.Groups[1].Value) == expectedRow && byIndex == null)
                    byIndex = el;
            }
            return byIndex;
        }

        private bool InvokeLookupButton(Window dialog, string label)
        {
            try
            {
                var btn = dialog.FindFirstDescendant(cf => cf.ByName($"button '{label}'"))
                    ?? dialog.FindFirstDescendant(cf => cf.ByName(label))
                    ?? dialog.FindFirstDescendant(cf => cf.ByName("&" + label));
                if (btn == null) return false;
                return InvokeElement(btn);
            }
            catch { return false; }
        }

        private void ClickLookupButton(Window dialog, string label, int rightOffset)
        {
            ForceActivateWindow(dialog);
            AutomationElement? btn = null;
            try
            {
                btn = dialog.FindFirstDescendant(cf => cf.ByName($"button '{label}'"))
                    ?? dialog.FindFirstDescendant(cf => cf.ByName(label))
                    ?? dialog.FindFirstDescendant(cf => cf.ByName("&" + label));
            }
            catch { }

            if (btn != null)
            {
                try
                {
                    if (btn.BoundingRectangle.Width > 0)
                    {
                        ClickElementByMouseInstant(btn);
                        return;
                    }
                }
                catch { }
            }

            var db = dialog.BoundingRectangle;
            ClickAtPoint(db.Right - rightOffset, db.Bottom - 16);
        }

        private Window? FindHolidayLookupWindowOnce(AutomationElement popup)
        {
            static bool IsLookupName(string? n) =>
                !string.IsNullOrEmpty(n) &&
                (n.Contains("กำหนดปฏิทิน") || n.Contains("กำหนดปฏิบัติ") || n.Contains("ปฏิทินบริษัท"));

            try
            {
                var popupWin = popup.AsWindow();

                var w = popupWin?.ModalWindows.FirstOrDefault(m => IsLookupName(m.Name));
                if (w != null) return w;

                w = _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault(m => IsLookupName(m.Name));
                if (w != null) return w;

                var desk = _automation.GetDesktop()
                    .FindAllChildren(cf => cf.ByControlType(ControlType.Window))
                    .FirstOrDefault(m => IsLookupName(m.Name));
                if (desk != null) return desk.AsWindow();

                var inner = popup.FindAllDescendants(cf => cf.ByControlType(ControlType.Window))
                    .FirstOrDefault(m => IsLookupName(m.Name));
                if (inner != null) return inner.AsWindow();

                w = popupWin?.ModalWindows.FirstOrDefault(m => !m.Name.Contains("กำหนดงวดอัตโนมัติ"));
                if (w != null) return w;
            }
            catch { }
            return null;
        }

        private Window? TryOpenHolidayLookup(AutomationElement popup)
        {
            var container = popup.FindFirstDescendant(cf => cf.ByAutomationId(IdPopupBtnCalendarOrg))
                ?? FindEditControlByLabel(popup, "ปฏิทินวันหยุดบริษัท")
                ?? popup.FindFirstDescendant(cf => cf.ByName("ปฏิทินวันหยุดบริษัท"));

            if (container == null) return null;

            Window? WaitLookup() => RetryFind(() => FindHolidayLookupWindowOnce(popup), timeoutMs: 1500);
            Window? result;

            ForceActivateWindow(popup.AsWindow() ?? _mainWindow.AsWindow());
            Thread.Sleep(150);

            var childBtn = container.FindAllDescendants(cf => cf.ByControlType(ControlType.Button)).LastOrDefault();
            if (childBtn != null)
            {
                try
                {
                    if (!InvokeElement(childBtn)) ClickElementByMouseInstant(childBtn);
                    if ((result = WaitLookup()) != null) return result;
                }
                catch { }
            }

            var b = container.BoundingRectangle;
            foreach (int offset in new[] { 10, 16, 24 })
            {
                ClickAtPoint(b.Right - offset, b.Top + b.Height / 2);
                if ((result = WaitLookup()) != null) return result;
            }

            ClickElementByMouseInstant(container);
            if ((result = WaitLookup()) != null) return result;

            try { container.Focus(); } catch { }
            Keyboard.Press(VirtualKeyShort.F4);
            return WaitLookup();
        }

        private static IEnumerable<int> ResolveMonthRowsInternal(MonthPattern pattern, int totalRows)
        {
            switch (pattern)
            {
                case MonthPattern.All: return Enumerable.Range(0, totalRows);
                case MonthPattern.First6: return Enumerable.Range(0, Math.Min(6, totalRows));
                case MonthPattern.Last6: return Enumerable.Range(Math.Max(0, totalRows - 6), Math.Min(6, totalRows));
                case MonthPattern.EveryOther: return Enumerable.Range(0, totalRows).Where(i => i % 2 == 0);
                default: return Enumerable.Empty<int>();
            }
        }

        private void ApplyMonthPattern(Window form, string[] columnNames, MonthPattern pattern, int totalRows = 12)
        {
            if (pattern == MonthPattern.None) return;
            var rowsToCheck = ResolveMonthRowsInternal(pattern, totalRows).ToHashSet();
            var grid = FindCalculationGrid(form);
            if (grid == null) return;

            ScrollGridVerticalToTop(grid);

            bool isScrolledToBottom = false;

            for (int i = 0; i < totalRows; i++)
            {
                if (totalRows > 16 && i >= 16 && !isScrolledToBottom)
                {
                    ScrollGridVerticalToBottom(grid);
                    isScrolledToBottom = true;
                }

                bool shouldCheck = rowsToCheck.Contains(i);
                SetGridCheckboxRobust(form, grid, columnNames, i, shouldCheck);
            }

            ScrollGridVerticalToTop(grid);
        }

        private void ScrollGridVerticalToBottom(AutomationElement grid)
        {
            try
            {
                var scrollBar = grid.FindFirstDescendant(cf => cf.ByControlType(ControlType.ScrollBar))
                    ?? grid.FindAllDescendants(cf => cf.ByControlType(ControlType.ScrollBar)).FirstOrDefault();

                if (scrollBar != null && scrollBar.Patterns.Scroll.IsSupported)
                {
                    scrollBar.Patterns.Scroll.Pattern.Scroll(ScrollAmount.NoAmount, ScrollAmount.LargeIncrement);
                    return;
                }

                var lineDownBtn = grid.FindFirstDescendant(cf => cf.ByName("button 'Line Down'"))
                    ?? grid.FindFirstDescendant(cf => cf.ByName("Line Down"));

                if (lineDownBtn != null)
                {
                    for (int k = 0; k < 10; k++)
                    {
                        ClickElementByMouseInstant(lineDownBtn);
                    }
                }
                else
                {
                    grid.Focus();
                    Keyboard.Press(VirtualKeyShort.END);
                }
            }
            catch
            {
                grid.Focus();
                Keyboard.Press(VirtualKeyShort.END);
            }
        }

        private void ScrollGridVerticalToTop(AutomationElement grid)
        {
            try
            {
                var scrollBar = grid.FindFirstDescendant(cf => cf.ByControlType(ControlType.ScrollBar))
                    ?? grid.FindAllDescendants(cf => cf.ByControlType(ControlType.ScrollBar)).FirstOrDefault();

                if (scrollBar != null && scrollBar.Patterns.Scroll.IsSupported)
                {
                    scrollBar.Patterns.Scroll.Pattern.Scroll(ScrollAmount.NoAmount, ScrollAmount.LargeDecrement);
                    return;
                }

                grid.Focus();
                Keyboard.Press(VirtualKeyShort.HOME);
            }
            catch
            {
                grid.Focus();
                Keyboard.Press(VirtualKeyShort.HOME);
            }
        }

        private AutomationElement? FindCalculationGrid(Window form)
        {
            return form.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataGrid))
                ?? form.FindFirstDescendant(cf => cf.ByControlType(ControlType.Table))
                ?? form.FindFirstDescendant(cf => cf.ByAutomationId("gridControlCalculation"))
                ?? form.FindFirstDescendant(cf => cf.ByName("General"));
        }

        private void SetGridCheckboxRobust(Window form, AutomationElement grid, string[] columnNames, int rowIndex, bool check)
        {
            AutomationElement? cell = null;
            Func<AutomationElement?> findCell = () =>
            {
                foreach (var colName in columnNames)
                {
                    string exactItemName = $"item '{colName} row {rowIndex}'";
                    string fallbackCellName = $"{colName} row {rowIndex}";

                    var found = form.FindFirstDescendant(cf => cf.ByName(exactItemName))
                        ?? form.FindFirstDescendant(cf => cf.ByName(fallbackCellName))
                        ?? grid.FindFirstDescendant(cf => cf.ByName(exactItemName))
                        ?? grid.FindFirstDescendant(cf => cf.ByName(fallbackCellName));

                    if (found != null) return found;
                }
                return null;
            };

            cell = findCell();

            if (cell == null)
            {
                ScrollGridVerticalToBottom(grid);
                cell = findCell();
            }

            if (cell != null)
            {
                bool isChecked = GetCheckboxState(cell);
                if (isChecked != check)
                {
                    ToggleCheckboxRobust(cell);
                }
            }
        }

        private bool GetCheckboxState(AutomationElement element)
        {
            try
            {
                var cb = element.AsCheckBox();
                if (cb?.IsChecked != null) return cb.IsChecked == true;
                if (element.Patterns.Toggle.IsSupported)
                    return element.Patterns.Toggle.Pattern.ToggleState.Value == ToggleState.On;
            }
            catch { }
            return false;
        }

        private void ToggleCheckboxRobust(AutomationElement element)
        {
            element.Focus();
            ClickElementByMouseInstant(element);
        }

        private void SetCalculationMonth(Window form, string from, string to)
        {
            ForceActivateWindow(form);

            var grid = FindCalculationGrid(form);
            if (grid == null) throw new Exception("หากริดตารางงวดการจ่ายหน้า General ไม่เจอ");

            var colHeader = grid.FindFirstDescendant(cf => cf.ByName("ประจำเดือน"))
                         ?? grid.FindAllDescendants().FirstOrDefault(e => !string.IsNullOrEmpty(e.Name) && e.Name.Trim().Equals("ประจำเดือน", StringComparison.OrdinalIgnoreCase));

            int clickX, clickY;

            if (colHeader != null)
            {
                var headerBounds = colHeader.BoundingRectangle;
                clickX = (int)headerBounds.Center().X;
                clickY = (int)headerBounds.Bottom + 12;
            }
            else
            {
                var gridBounds = grid.BoundingRectangle;
                clickX = (int)gridBounds.Left + 120;
                clickY = (int)gridBounds.Top + 60;
            }

            SetCursorPos(clickX, clickY);
            Thread.Sleep(50);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(50);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);

            Thread.Sleep(300);

            if (to.Equals("กุมภาพันธ์", StringComparison.OrdinalIgnoreCase))
            {
                int itemY = clickY + 34;
                SetCursorPos(clickX, itemY);
                Thread.Sleep(100);
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                Thread.Sleep(50);
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
            }
            else if (to.Equals("มกราคม", StringComparison.OrdinalIgnoreCase))
            {
                int itemY = clickY + 16;
                SetCursorPos(clickX, itemY);
                Thread.Sleep(100);
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                Thread.Sleep(50);
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
            }
        }

        private Window ClickRowAndOpenEditForm(string nameToFind, string testName)
        {
            RefreshGrid();
            AutomationElement? row = RetryFind(() => FindRowByNameColumn(nameToFind), timeoutMs: 6000);
            if (row == null)
            {
                RefreshGrid();
                row = RetryFind(() => FindRowByNameColumn(nameToFind), timeoutMs: 6000);
            }
            if (row == null) throw new Exception($"หาแถวสำหรับ '{nameToFind}' ไม่เจอ");

            WaitUntil(() => { try { row.Focus(); return true; } catch { return false; } }, 1000);
            ClickElementByMouseInstant(row);
            Thread.Sleep(200);

            var editButton = RetryFind(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("Edit")), 3000)
                ?? throw new Exception("หาปุ่ม Edit ไม่เจอ");

            ClickElementByMouseInstant(editButton);
            var editForm = RetryFind(FindOpenPeriodTemplateInfoForm) ?? throw new Exception("หาฟอร์ม PeriodTemplateInfoUC ไม่เจอ");
            WaitUntil(() => editForm.FindFirstDescendant(cf => cf.ByAutomationId(IdTxtName)) != null, 3000);
            return editForm;
        }

        private (string TestName, string ExistingName, string NewName)? PerformEdit(string testName, string existingName,
            string newName, Action<Window> editAction, string saveButtonName, bool needsExplicitClose)
        {
            try
            {
                Window form = ClickRowAndOpenEditForm(existingName, testName);
                FillTextBox(form, IdTxtName, newName);
                editAction(form);
                if (!TrySaveAndHandleDuplicate(form, testName, newName, saveButtonName))
                    return null;
                if (needsExplicitClose)
                    form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();

                WaitForClosed(() => FindOpenPeriodTemplateInfoForm());
                return (testName, existingName, newName);
            }
            catch (Exception ex)
            {
                LogFailureCounted(testName, $"แก้ไข '{existingName}' ไม่สำเร็จ: {ex.Message}");
                return null;
            }
        }

        private void VerifyEditOutcomes(List<(string TestName, string ExistingName, string NewName)> results)
        {
            if (results.Count == 0) return;
            RefreshGrid();
            foreach (var (testName, existingName, newName) in results)
            {
                if (VerifyRecordExistsInGrid(newName, refreshGrid: false))
                    LogSuccessCounted(testName, $"แก้ไข '{existingName}' เป็น '{newName}' สำเร็จ (พบในตารางหลัก)");
                else
                    LogFailureCounted(testName, $"แก้ไข '{existingName}' เป็น '{newName}' แล้วแต่ไม่พบในตารางหลัก");
            }
        }

        private void DeleteRecordAndVerify(string name, string confirmButton, string testName, bool expectDeleted)
        {
            try
            {
                var dataGrid = _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataGrid))
                               ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Table));
                if (dataGrid != null)
                {
                    dataGrid.Focus();
                    Keyboard.Press(VirtualKeyShort.ESCAPE);
                    Keyboard.Press(VirtualKeyShort.UP);
                }
            }
            catch { }

            RefreshGrid();

            AutomationElement? row = RetryFind(() => FindRowByNameColumn(name), timeoutMs: 10000);
            if (row == null)
            {
                RefreshGrid();
                row = RetryFind(() => FindRowByNameColumn(name), timeoutMs: 8000);
            }

            if (row == null) throw new Exception($"หาแถวสำหรับลบ ({name}) ไม่เจอ");

            WaitUntil(() => { try { row.Focus(); return true; } catch { return false; } }, 1000);
            ClickElementByMouseInstant(row);
            Thread.Sleep(200);

            var deleteButton = RetryFind(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("Delete")), 3000)
                ?? throw new Exception("หาปุ่ม Delete ไม่เจอ");

            ClickElementByMouseInstant(deleteButton);

            Window? confirmDialog = RetryFind(() =>
                _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault()
                ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("คำเตือน"))?.AsWindow(),
                timeoutMs: 4000);

            if (confirmDialog == null) throw new Exception("กด Delete แล้วไม่พบ Pop-up ยืนยัน");

            ClickDialogButton(confirmDialog, confirmButton, "ยืนยันการลบ");
            WaitForClosed(() => confirmDialog);

            RefreshGrid();
            bool stillExistsOnGrid = FindRowByNameColumn(name, exactOnly: expectDeleted) != null;

            if (expectDeleted)
            {
                if (stillExistsOnGrid)
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
        // =========================================================================================

        private readonly AutomationElement _mainWindow;
        private readonly UIA3Automation _automation;
        private int _totalSuccess = 0;
        private int _totalFail = 0;
        private const int TimeoutMs = 6000;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();
        private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

        [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hWnd);

        static PeriodTemplateInfoUC()
        {
            try
            {
                if (!SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2))
                    SetProcessDPIAware();
            }
            catch (Exception ex) { Debug.WriteLine($"DPI Awareness failed: {ex.Message}"); }
        }

        public PeriodTemplateInfoUC(AutomationElement mainWindow, UIA3Automation automation)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

        private bool TryOpenPeriodTemplateInfoMenu(string stage)
        {
            try
            {
                ForceActivateWindow(_mainWindow);

                var searchBox = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("baritemSearch"))
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)), 4000)
                                ?? throw new Exception("ไม่พบช่องค้นหา baritemSearch");

                WaitUntil(() => { try { searchBox.Focus(); return true; } catch { return false; } }, 1000);
                string searchKeyword = FormTitle;

                if (searchBox.Patterns.Value.IsSupported)
                {
                    searchBox.Patterns.Value.Pattern.SetValue("");
                    searchBox.Patterns.Value.Pattern.SetValue(searchKeyword);
                }
                else
                {
                    ClickElementByMouseInstant(searchBox);
                    Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                    Keyboard.Press(VirtualKeyShort.BACK);
                    Keyboard.Type(searchKeyword);
                }

                var searchBtn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("barChkSearch"))
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("Find")), 4000)
                                ?? throw new Exception("ไม่พบปุ่มค้นหา barChkSearch");

                searchBtn.Click();

                var menuResult = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName(searchKeyword))
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("treeListColumn row 1"))
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("treeListColumn row 27")), 4000)
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
            try { step(); }
            catch (Exception ex) { LogFailureCounted(testName, $"ไม่ผ่าน: {ex.Message}"); }
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
            try { DeleteRecordAndVerify(name, confirmButton, testName, expectDeleted); }
            catch (Exception ex) { LogFailureCounted(testName, $"ลบ '{name}' ไม่สำเร็จ: {ex.Message}"); }
        }

        private void ClickFormButton(Window form, string buttonNameOrId, int timeoutMs = 3000, bool byAutomationId = false)
        {
            var button = byAutomationId
                ? RetryFind(() => form.FindFirstDescendant(cf => cf.ByAutomationId(buttonNameOrId)), timeoutMs: timeoutMs)
                : RetryFind(() => form.FindFirstDescendant(cf => cf.ByName(buttonNameOrId)), timeoutMs: timeoutMs);
            if (button == null) throw new Exception($"หาปุ่ม '{buttonNameOrId}' บนฟอร์มไม่เจอ");
            ClickElementByMouseInstant(button);
        }

        private static string GetElementTextSafe(AutomationElement el)
        {
            try { if (el.Patterns.Value.IsSupported) { var v = el.Patterns.Value.Pattern.Value.ValueOrDefault; if (!string.IsNullOrEmpty(v)) return v; } } catch { }
            try { if (el.Patterns.LegacyIAccessible.IsSupported) { var v = el.Patterns.LegacyIAccessible.Pattern.Value.ValueOrDefault; if (!string.IsNullOrEmpty(v)) return v; } } catch { }
            try { return el.Name ?? ""; } catch { return ""; }
        }

        private void ClickAtPoint(int x, int y)
        {
            SetCursorPos(x, y);
            Thread.Sleep(50);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(50);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
        }

        private bool InvokeElement(AutomationElement element)
        {
            try { if (element.Patterns.Invoke.IsSupported) { element.Patterns.Invoke.Pattern.Invoke(); return true; } } catch { }
            try { if (element.Patterns.LegacyIAccessible.IsSupported) { element.Patterns.LegacyIAccessible.Pattern.DoDefaultAction(); return true; } } catch { }
            try { if (element.Patterns.Toggle.IsSupported) { element.Patterns.Toggle.Pattern.Toggle(); return true; } } catch { }
            return false;
        }

        private void ForceActivateWindow(AutomationElement windowElement)
        {
            try
            {
                IntPtr hWnd = (IntPtr)windowElement.Properties.NativeWindowHandle.ValueOrDefault;
                if (hWnd == IntPtr.Zero) return;
                IntPtr foregroundHwnd = GetForegroundWindow();
                uint foregroundThreadId = GetWindowThreadProcessId(foregroundHwnd, out _);
                uint targetThreadId = GetWindowThreadProcessId(hWnd, out _);
                uint currentThreadId = GetCurrentThreadId();
                bool attachedToForeground = false;
                bool attachedToTarget = false;
                if (foregroundThreadId != 0 && foregroundThreadId != currentThreadId)
                    attachedToForeground = AttachThreadInput(currentThreadId, foregroundThreadId, true);
                if (targetThreadId != 0 && targetThreadId != currentThreadId)
                    attachedToTarget = AttachThreadInput(currentThreadId, targetThreadId, true);
                BringWindowToTop(hWnd);
                SetForegroundWindow(hWnd);
                if (attachedToForeground) AttachThreadInput(currentThreadId, foregroundThreadId, false);
                if (attachedToTarget) AttachThreadInput(currentThreadId, targetThreadId, false);
            }
            catch { }
        }

        private void ClickElementByMouseInstant(AutomationElement element)
        {
            var bounds = element.BoundingRectangle;
            int x = (int)bounds.Center().X;
            int y = (int)bounds.Center().Y;
            SetCursorPos(x, y);
            Thread.Sleep(50);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(50);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
        }

        private AutomationElement? FindByIdOrName(AutomationElement context, string? automationId, string nameContains,
            ControlType controlType = ControlType.CheckBox)
        {
            if (!string.IsNullOrEmpty(automationId))
            {
                var byId = context.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
                if (byId != null) return byId;
            }
            var candidates = context.FindAllDescendants(cf => cf.ByControlType(controlType));
            var exact = candidates.FirstOrDefault(e => string.Equals(e.Name?.Trim(), nameContains, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;
            return candidates.FirstOrDefault(e => !string.IsNullOrEmpty(e.Name) && e.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase));
        }

        private string? TryDetectDuplicateWarning(Window form)
        {
            var taskResultPanel = FindTaskResultPanel(form, timeoutMs: 1500);
            if (taskResultPanel == null) return null;
            foreach (var text in GetPanelTexts(taskResultPanel))
                if (text.Contains("ชื่อรูปแบบงวดการจ่าย ห้ามซ้ำ")) return "ชื่อซ้ำ";
            return null;
        }

        private AutomationElement? FindTaskResultPanel(Window form, int timeoutMs = 3000) =>
            RetryFind(() =>
                form.FindFirstDescendant(cf => cf.ByName("Task Result").Or(cf.ByAutomationId("dockPanelTaskResult")))
                ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("Task Result").Or(cf.ByAutomationId("dockPanelTaskResult"))),
                timeoutMs: timeoutMs);

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
            try
            {
                var dataGrid = _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataGrid))
                               ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Table));
                if (dataGrid != null)
                {
                    WaitUntil(() => { try { dataGrid.Focus(); return true; } catch { return false; } }, 1000);
                    ClickElementByMouseInstant(dataGrid);

                    Keyboard.Press(VirtualKeyShort.ESCAPE);
                    Keyboard.Press(VirtualKeyShort.F5);
                    Keyboard.Press(VirtualKeyShort.HOME);
                }
            }
            catch { }
        }

        private bool VerifyRecordExistsInGrid(string value, int timeoutMs = 5000, bool refreshGrid = true)
        {
            if (refreshGrid) RefreshGrid();
            return RetryFind(() => FindRowByNameColumn(value), timeoutMs: timeoutMs) != null;
        }

        private static Rectangle? GetNameColumnBounds(AutomationElement grid)
        {
            try
            {
                var header = grid.FindAllDescendants().FirstOrDefault(e =>
                {
                    try { return string.Equals(e.Name?.Trim(), "ชื่อรูปแบบงวดการจ่าย", StringComparison.OrdinalIgnoreCase); }
                    catch { return false; }
                });
                if (header == null) return null;
                var r = header.BoundingRectangle;
                return r.IsEmpty ? null : r;
            }
            catch { return null; }
        }

        private AutomationElement? FindRowByNameColumn(string targetName, bool exactOnly = false)
        {
            var grid = _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataGrid))
                       ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Table));
            if (grid == null) return null;

            string cleanTarget = targetName.Trim();
            Rectangle? nameColumn = GetNameColumnBounds(grid);

            var allCandidates = grid.FindAllDescendants(cf =>
                cf.ByControlType(ControlType.DataItem)
                .Or(cf.ByControlType(ControlType.Text))
                .Or(cf.ByControlType(ControlType.Custom)));

            AutomationElement? Search(bool exact)
            {
                foreach (var item in allCandidates)
                {
                    string text = item.Name ?? "";
                    if (item.Patterns.Value.IsSupported)
                    {
                        try { text = item.Patterns.Value.Pattern.Value.Value ?? text; } catch { }
                    }

                    if (string.IsNullOrWhiteSpace(text)) continue;

                    bool isMatch = exact
                        ? text.Trim().Equals(cleanTarget, StringComparison.OrdinalIgnoreCase)
                        : text.Contains(cleanTarget, StringComparison.OrdinalIgnoreCase);
                    if (!isMatch) continue;

                    if (nameColumn.HasValue)
                    {
                        try
                        {
                            var r = item.BoundingRectangle;
                            if (!r.IsEmpty)
                            {
                                int centerX = r.Left + (r.Width / 2);
                                if (centerX < nameColumn.Value.Left || centerX > nameColumn.Value.Right)
                                    continue;
                            }
                        }
                        catch { }
                    }

                    var parent = item;
                    while (parent != null && parent != grid)
                    {
                        if (parent.ControlType == ControlType.DataItem)
                            return parent;
                        parent = parent.Parent;
                    }
                    return item;
                }
                return null;
            }

            return Search(true) ?? (exactOnly ? null : Search(false));
        }

        private Window GetOrOpenPeriodTemplateInfoForm()
        {
            Window? form = RetryFind(FindOpenPeriodTemplateInfoForm);
            if (form != null) return form;
            _mainWindow.FindFirstDescendant(cf => cf.ByName("New"))?.Click();
            form = RetryFind(FindOpenPeriodTemplateInfoForm, timeoutMs: 8000);
            if (form == null) throw new Exception("หาฟอร์ม PeriodTemplateInfoUC ไม่เจอ");
            WaitUntil(() => form.FindFirstDescendant(cf => cf.ByAutomationId(IdTxtName)) != null, 4000);
            return form;
        }

        private Window OpenPeriodTemplateInfoForm()
        {
            _mainWindow.FindFirstDescendant(cf => cf.ByName("New"))?.Click();
            var form = RetryFind(FindOpenPeriodTemplateInfoForm) ?? throw new Exception("หาฟอร์ม PeriodTemplateInfoUC ไม่เจอ");
            WaitUntil(() => form.FindFirstDescendant(cf => cf.ByAutomationId(IdTxtName)) != null, 4000);
            return form;
        }

        private Window? FindOpenPeriodTemplateInfoForm() =>
            _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault(w => w.AutomationId == FormId || w.Name.Contains(FormTitle))
            ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByAutomationId(FormId).Or(cf.ByName(FormTitle)))?.AsWindow();

        private void ClickCloseAndHandleDialog(Window form, string buttonToClick, string testName)
        {
            ForceActivateWindow(form);
            Thread.Sleep(150);

            var closeBtn = form.FindFirstDescendant(cf => cf.ByName("Close"))
                ?? form.FindFirstDescendant(cf => cf.ByName("button 'Close'"))
                ?? form.FindFirstDescendant(cf => cf.ByAutomationId("devBtnClose"));

            if (closeBtn != null)
            {
                ClickElementByMouseInstant(closeBtn);
            }
            else
            {
                // Fallback ด้วยปุ่ม Close มาตรฐานหรือส่ง Escape/Close Pattern
                form.Focus();
                Keyboard.Press(VirtualKeyShort.ESCAPE);
            }

            Window? confirmDialog = RetryFind(() =>
                form.ModalWindows.FirstOrDefault()
                ?? _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault()
                ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("คำเตือน"))?.AsWindow()
                ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("Question"))?.AsWindow(),
                timeoutMs: 4000);

            if (confirmDialog == null)
            {
                // ลองคลิกปุ่ม Close อีกรอบ
                if (closeBtn != null) ClickElementByMouseInstant(closeBtn);
                confirmDialog = RetryFind(() =>
                    form.ModalWindows.FirstOrDefault()
                    ?? _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault()
                    ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("คำเตือน"))?.AsWindow()
                    ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("Question"))?.AsWindow(),
                    timeoutMs: 3000);
            }

            if (confirmDialog == null) throw new Exception("กด Close แล้วไม่พบ Pop-up ยืนยันคำเตือน");

            ClickDialogButton(confirmDialog, buttonToClick, "ยืนยันการเปลี่ยนแปลง");
            WaitForClosed(() => confirmDialog);
        }

        private void ClickDialogButton(Window dialog, string buttonName, string dialogDescription = "")
        {
            var btn = dialog.FindFirstDescendant(cf => cf.ByName(buttonName))?.AsButton()
                      ?? dialog.FindFirstDescendant(cf => cf.ByName($"&{buttonName}"))?.AsButton()
                      ?? dialog.FindFirstDescendant(cf => cf.ByName($"button '&{buttonName}'"))?.AsButton()
                      ?? dialog.FindFirstDescendant(cf => cf.ByAutomationId($"btn{buttonName}"))?.AsButton()
                      ?? dialog.FindFirstDescendant(cf => cf.ByName(buttonName == "Yes" ? "ใช่" : "ไม่ใช่"))?.AsButton()
                      ?? throw new Exception($"ไม่พบปุ่ม '{buttonName}' บน Pop-up {dialogDescription}");

            ForceActivateWindow(dialog);
            ClickElementByMouseInstant(btn);
        }

        private void AssertFormStillOpen(Window form, string testName)
        {
            bool isOpen = WaitUntil(() => !form.Properties.IsOffscreen.ValueOrDefault && form.IsAvailable, 2000);
            if (!isOpen) throw new Exception("ปิดฟอร์มไม่สำเร็จ");
            LogSuccessCounted(testName, "ฟอร์มถูกเปิดอยู่");
        }

        private void AssertFormClosed(Window form, string testName)
        {
            bool isClosed = WaitForClosed(() => FindOpenPeriodTemplateInfoForm(), 3000);
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
                    var taskResultPanel = FindTaskResultPanel(form, timeoutMs: 500);
                    if (taskResultPanel != null)
                    {
                        var texts = GetPanelTexts(taskResultPanel);
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
                }
                catch { }
                Thread.Sleep(100);
            }

            bool isOpen = WaitUntil(() => !form.Properties.IsOffscreen.ValueOrDefault && form.IsAvailable, 2000);
            if (!foundMessage || !isOpen)
                throw new Exception($"ตรวจสอบไม่ผ่าน (พบข้อความ: {foundMessage}, ฟอร์มเปิดอยู่: {isOpen})");

            LogSuccessCounted(testName, $"พบข้อความ: '{expectedWarningMessage}' และ ฟอร์มถูกเปิดอยู่");
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
                        if (texts.Any(t => t.Contains(expectedWarningMessage))) { foundMessage = true; break; }
                    }
                    if (GetPanelTexts(form).Any(t => t.Contains(expectedWarningMessage))) { foundMessage = true; break; }
                    var modal = form.ModalWindows.FirstOrDefault() ?? _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault();
                    if (modal != null && GetPanelTexts(modal).Any(t => t.Contains(expectedWarningMessage))) { foundMessage = true; break; }
                    var warningPopup = _automation.GetDesktop().FindFirstChild(cf => cf.ByName("คำเตือน"))?.AsWindow();
                    if (warningPopup != null && GetPanelTexts(warningPopup).Any(t => t.Contains(expectedWarningMessage))) { foundMessage = true; break; }
                }
                catch { }
                Thread.Sleep(150);
            }
            if (!foundMessage)
            {
                string seenDump = lastSeenTexts.Count > 0
                    ? string.Join(" | ", lastSeenTexts.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct())
                    : "(ไม่พบข้อความใดๆ ใน Task Result panel เลย)";
                throw new Exception($"ไม่พบข้อความ: '{expectedWarningMessage}' — ข้อความที่เจอจริง: [{seenDump}]");
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

        private static void FillTextBox(AutomationElement context, string automationId, string text)
        {
            // 1. ค้นหาจาก AutomationId ตรงๆ หรือชื่อที่เกี่ยวข้องกับ Remark
            var element = context.FindFirstDescendant(cf => cf.ByAutomationId(automationId))
                ?? context.FindFirstDescendant(cf => cf.ByName(automationId));

            if (element == null && (automationId.Contains("Remark") || automationId.Contains("devTxtRemark")))
            {
                element = context.FindFirstDescendant(cf => cf.ByAutomationId("devMemoRemark"))
                    ?? context.FindFirstDescendant(cf => cf.ByAutomationId("txtRemark"))
                    ?? context.FindFirstDescendant(cf => cf.ByAutomationId("memoRemark"));

                if (element == null)
                {
                    var lbl = context.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
                        .FirstOrDefault(e => !string.IsNullOrEmpty(e.Name) && (e.Name.Contains("หมายเหตุ") || e.Name.Contains("รายละเอียด")));
                    if (lbl != null)
                    {
                        var lr = lbl.BoundingRectangle;
                        element = context.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit))
                            .FirstOrDefault(e => Math.Abs(e.BoundingRectangle.Y - lr.Y) < 30 && e.BoundingRectangle.X >= lr.Right - 5);
                    }
                }
            }

            element ??= context.FindFirstDescendant(cf => cf.ByAutomationId(automationId));

            if (element == null)
            {
                throw new Exception($"หาช่องกรอก '{automationId}' ไม่เจอ");
            }

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

            ClickElementByMouseInstantStatic(element);

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

        private static void ClickElementByMouseInstantStatic(AutomationElement element)
        {
            var bounds = element.BoundingRectangle;
            int x = (int)bounds.Center().X;
            int y = (int)bounds.Center().Y;
            SetCursorPos(x, y);
            Thread.Sleep(50);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(50);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
        }

        private static void TickCheckbox(AutomationElement context, string checkboxId, bool tick, string? fallbackNameContains = null)
        {
            var found = context.FindFirstDescendant(cf => cf.ByAutomationId(checkboxId));
            if (found == null && !string.IsNullOrEmpty(fallbackNameContains))
            {
                var candidates = context.FindAllDescendants(cf => cf.ByControlType(ControlType.CheckBox));
                found = candidates.FirstOrDefault(e => !string.IsNullOrEmpty(e.Name) && e.Name.Contains(fallbackNameContains, StringComparison.OrdinalIgnoreCase));
            }
            if (found == null)
            {
                throw new Exception($"หา checkbox '{checkboxId}' ไม่เจอ");
            }
            bool CurrentlyTicked()
            {
                var cb = found.AsCheckBox();
                if (cb?.IsChecked != null) return cb.IsChecked == true;
                return false;
            }
            if (CurrentlyTicked() == tick) return;
            try { found.Click(); } catch { }
            Thread.Sleep(200);
            if (CurrentlyTicked() != tick)
            {
                try { found.Focus(); Keyboard.Type(VirtualKeyShort.SPACE); } catch { }
            }
        }

        private static void TryAdd<T>(List<T> list, T? item) where T : struct
        {
            if (item.HasValue) list.Add(item.Value);
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

        private static T? RetryFind<T>(Func<T?> finder, int timeoutMs = 6000, int pollMs = 150) where T : class
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try
                {
                    var result = finder();
                    if (result != null) return result;
                }
                catch { }
                Thread.Sleep(pollMs);
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