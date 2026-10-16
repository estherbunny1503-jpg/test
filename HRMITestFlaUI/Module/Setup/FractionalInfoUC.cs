using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
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
    public class FractionalInfoUC
    {
        // =========================================================================================
        // หมวดที่ 1: ข้อมูลการทดสอบทั้ง 5 ขั้น (Test Data)
        // =========================================================================================

        // --- Data Records สำหรับใช้งานในแต่ละขั้นตอน ---
        private sealed record FractionalRecord(
            string Code,
            string NameTh,
            string NameEn,
            string Remark,
            Action<Window> ConfigureFormatTab);

        private sealed record ValidationItem(
            string TestCaseId,
            string Code,
            string NameTh,
            string SaveBtn,
            string[] ExpectedErrors);

        private sealed record FractionalEditItem(
            string TestCaseId,
            string ExistingCode,
            string NewCode,
            string NewNameTh,
            string NewNameEn,
            string NewRemark,
            Action<Window>? ConfigureFormatTab,
            string SaveButtonName,
            bool NeedsExplicitClose);

        private sealed record DeleteItem(
            string TargetCode,
            string DialogBtn,
            bool ExpectDeleted);

        // [ขั้นที่ 1] ข้อมูลทดสอบการสร้างใหม่ (Add / Navigate)
        private static readonly FractionalRecord[] Step1_AddCases =
        {
            new("XFCM-801", "ปัดทุก 25 สตางค์", "Rounding 25 Satang",
                "ข้อมูลจำลองทดสอบปัดเศษเงินแบบสี่ช่วง",
                form =>
                {
                    SwitchToFormatTab(form);
                    SelectRadio(form, IdRadioRoundMoney, RadioMoneyName);
                    FillGridRows(form, IdGridRoundMoney, GridMoneyLabel, new[] { ("1", "24", "1"), ("25", "49", "25") });
                    SwitchToGeneralTab(form);
                }),

            new("XFCM-802", "ปัดเศษเงินแบบฟิกซ์พิเศษ", "Custom Fixed Money",
                "ข้อมูลจำลองทดสอบการทำงานระบบปัดเศษคงที่",
                form =>
                {
                    SwitchToFormatTab(form);
                    SelectRadio(form, IdRadioRoundMoney, RadioMoneyName);
                    TickCheckbox(form, IdChkNoRoundMoney, ChkNoRoundMoneyName, tick: true);
                    SwitchToGeneralTab(form);
                }),

            new("XFCM-803", "ตัดเศษสตางค์ทิ้งทั้งหมด", "Ignore Satang Fraction",
                "ข้อมูลจำลองทดสอบกรณีจ่ายเงินแบบตัดเศษทิ้ง",
                form =>
                {
                    SwitchToFormatTab(form);
                    SelectRadio(form, IdRadioRoundMoney, RadioMoneyName);
                    FillGridRows(form, IdGridRoundMoney, GridMoneyLabel, new[] { ("1", "99", "1") });
                    SwitchToGeneralTab(form);
                }),

            new("XFCT-801", "ปัดเศษเวลาทุก 15 นาที", "Time Block 15 Mins",
                "ข้อมูลจำลองทดสอบปัดช่วงเวลาการทำงานเป็นราย 15 นาที",
                form =>
                {
                    SwitchToFormatTab(form);
                    SelectRadio(form, IdRadioRoundTime, RadioTimeName);
                    TickCheckbox(form, IdChkNoRoundSecond, ChkNoRoundSecondName, tick: true);
                    FillGridRows(form, IdGridRoundMinute, GridMinuteLabel, new[] { ("1", "14", "1"), ("15", "29", "15") });
                    SwitchToGeneralTab(form);
                }),

            new("XFCT-802", "ปัดเศษวินาทีทดสอบระบบ", "Second Fraction Test",
                "ข้อมูลจำลองตรวจสอบระบบการปัดเศษในระดับวินาที",
                form =>
                {
                    SwitchToFormatTab(form);
                    SelectRadio(form, IdRadioRoundTime, RadioTimeName);
                    TickCheckbox(form, IdChkNoRoundMinute, ChkNoRoundMinuteName, tick: true);
                    FillGridRows(form, IdGridRoundSecond, GridSecondLabel, new[] { ("1", "29", "1"), ("30", "59", "30") });
                    SwitchToGeneralTab(form);
                }),

            new("XFCT-803", "ไม่ปัดเศษคงที่", "Fixed Second Option",
                "ข้อมูลจำลองตรวจสอบระบบการปัดเศษคงที่",
                form =>
                {
                    SwitchToFormatTab(form);
                    SelectRadio(form, IdRadioRoundTime, RadioTimeName);
                    TickCheckbox(form, IdChkNoRoundSecond, ChkNoRoundSecondName, tick: true);
                    TickCheckbox(form, IdChkNoRoundMinute, ChkNoRoundMinuteName, tick: true);
                    SwitchToGeneralTab(form);
                })
        };

        // [ขั้นที่ 2] ข้อมูลทดสอบ Validation ตอนเพิ่ม (Add Validation)
        private static readonly ValidationItem[] Step2_AddValidationCases =
        {
            new("TestCase01_MissingCode", "", "ปัดเศษเงิน 50 สตางค์", "Save",
                new[] { "รหัสรูปแบบการปัดเศษ ไม่สามารถเป็นค่าว่าง" }),

            new("TestCase02_MissingName", "XFCT-001", "", "Save and New",
                new[] { "ชื่อรูปแบบการปัดเศษ ไม่สามารถเป็นค่าว่าง" }),

            new("TestCase03_MissingBoth", "", "", "Save and New",
                new[] { "รหัสรูปแบบการปัดเศษ ไม่สามารถเป็นค่าว่าง", "ชื่อรูปแบบการปัดเศษ ไม่สามารถเป็นค่าว่าง" }),

            new("TestCase05_DuplicateCode", "XFCM-801", "ปัดเศษ", "Save and New",
                new[] { "รหัสรูปแบบการปัดเศษ ห้ามซ้ำ" }),

            new("TestCase06_DuplicateName", "XFCM-804", "ไม่ปัดเศษคงที่", "Save and Close",
                new[] { "ชื่อรูปแบบการปัดเศษ ห้ามซ้ำ" })
        };

        // [ขั้นที่ 3] ข้อมูลทดสอบการแก้ไขข้อมูล (Edit Records)
        private static readonly FractionalEditItem[] Step3_EditCases =
        {
            new("TestCase07_EditRow1", "XFCM-801", "XFCM-EDIT1", "แก้ไขระบบปัดเศษเงินรอบที่ 1", "Edited Money Rounding v1",
                "แก้ไขข้อมูลทดสอบปัดเศษเงิน",
                form =>
                {
                    SwitchToFormatTab(form);
                    SelectRadio(form, IdRadioRoundMoney, RadioMoneyName);
                    TickCheckbox(form, IdChkNoRoundMoney, ChkNoRoundMoneyName, tick: true);
                    SwitchToGeneralTab(form);
                },
                "Save", true),

            new("TestCase08_EditRow2", "XFCM-802", "XFCT-EDIT2", "แก้ไขเปิดออปชันเวลาคงที่", "Edited Fixed Time Option",
                "แก้ไขข้อมูลทดสอบปัดเศษเวลา",
                form =>
                {
                    SwitchToFormatTab(form);
                    SelectRadio(form, IdRadioRoundTime, RadioTimeName);
                    TickCheckbox(form, IdChkNoRoundMinute, ChkNoRoundMinuteName, tick: true);
                    TickCheckbox(form, IdChkNoRoundSecond, ChkNoRoundSecondName, tick: true);
                    SwitchToGeneralTab(form);
                },
                "Save and Close", false)
        };

        // [ขั้นที่ 4] ข้อมูลทดสอบ Validation ตอนแก้ไข (Edit Validation)
        private const string EditTargetRow = "XFCT-801";
        private static readonly ValidationItem[] Step4_EditValidationCases =
        {
            new("TestCase09_EditValidation_MissingCode", "", "ปัดเศษเวลาทุก 15 นาที", "Save",
                new[] { "รหัสรูปแบบการปัดเศษ ไม่สามารถเป็นค่าว่าง" }),

            new("TestCase09_EditValidation_MissingName", "XFCT-801", "", "Save and New",
                new[] { "ชื่อรูปแบบการปัดเศษ ไม่สามารถเป็นค่าว่าง" }),

            new("TestCase09_EditValidation_MissingBoth", "", "", "Save",
                new[] { "รหัสรูปแบบการปัดเศษ ไม่สามารถเป็นค่าว่าง", "ชื่อรูปแบบการปัดเศษ ไม่สามารถเป็นค่าว่าง" }),

            new("TestCase09_EditValidation_DuplicateCode", "XFCM-804", "ปัดเศษเวลาทุก 15 นาที", "Save and New",
                new[] { "รหัสรูปแบบการปัดเศษ ห้ามซ้ำ" }),

            new("TestCase09_EditValidation_DuplicateName", "XFCM-805", "ไม่ปัดเศษคงที่", "Save and Close",
                new[] { "ชื่อรูปแบบการปัดเศษ ห้ามซ้ำ" })
        };

        // [ขั้นที่ 5] ข้อมูลทดสอบการลบข้อมูล (Delete Records)
        private static readonly DeleteItem[] Step5_DeleteCases =
        {
            new("XFCT-803", "Yes", true),
            new("XFCT-802", "No", false)
        };


        // =========================================================================================
        // หมวดที่ 2: ส่วนที่ขึ้นกับหน้าจอนั้นๆ (Screen-Specific Implementation)
        // **เมื่อเปลี่ยนไปทำหน้าจออื่น ต้องมาแก้ไขค่าและเมทอดในส่วนนี้ทั้งหมด**
        // =========================================================================================

        // 2.1 Locator & Mappings ประจำหน้าจอ
        private const string MenuName = "กำหนดรูปแบบการปัดเศษ";
        private const string ScreenLogName = "FractionalInfoUC";
        private const string FormId = "FractionalInfoUC";
        private const string FormTitle = "กำหนดรูปแบบการปัดเศษ";

        private const string GeneralTabId = "devTabPgGeneral";
        private const string IdTabHeaderGeneral = "devTabHeaderGeneral";
        private const string IdTabHeaderFormat = "devTabHeaderFormat";
        private const string TabFormatName = "กำหนดรูปแบบ";
        private const string TabGeneralName = "Genaral";

        // 2.2 ฟิลด์ Input ประจำหน้าจอ
        private const string IdFractCode = "devTxtFractCode";
        private const string IdFractName = "devTxtFractName";
        private const string IdFractNameEng = "devTxtFractNameEng";
        private const string IdRemark = "devMemoRemark";
        private const string IdRadioRoundMoney = "devRadioRoundMoney";
        private const string IdRadioRoundTime = "devRadioRoundTime";
        private const string IdChkNoRoundMoney = "devChkConstFractSatang";
        private const string IdChkNoRoundSecond = "devChkConstFractSecond";
        private const string IdChkNoRoundMinute = "devChkConstFractMinute";
        private const string IdGridRoundMoney = "devGridCtrlFractSatang";
        private const string IdGridRoundSecond = "devGridCtrlFractSecond";
        private const string IdGridRoundMinute = "devGridCtrlFractMinute";

        private const string RadioMoneyName = "กำหนดรูปแบบการปัดเศษเงิน";
        private const string RadioTimeName = "กำหนดรูปแบบการปัดเศษเวลา";
        private const string ChkNoRoundMoneyName = "ไม่ปัดเศษสตางค์ เศษมีค่าคงที่";
        private const string ChkNoRoundSecondName = "ไม่ปัดเศษวินาที เศษมีค่าคงที่";
        private const string ChkNoRoundMinuteName = "ไม่ปัดเศษนาที เศษมีค่าคงที่";
        private const string GridMoneyLabel = "รูปแบบการปัดเศษสตางค์";
        private const string GridSecondLabel = "รูปแบบการปัดเศษวินาที";
        private const string GridMinuteLabel = "รูปแบบการปัดเศษนาที";

        /// <summary>
        /// ขั้นที่ 1: เปิดหน้าจอและกรอกข้อมูลสร้างใหม่
        /// </summary>
        public void NavigateToFractionalInfoUC()
        {
            string stageName = "TestCase00_AddFractionalData";
            TestLogger.LogInfo(ScreenLogName, "เริ่มทดสอบหน้าจอ Fractional Info");

            if (!TryOpenFractionalMenu(stageName)) return;

            Window? form = null;
            for (int i = 0; i < Step1_AddCases.Length; i++)
            {
                var rec = Step1_AddCases[i];
                string testName = $"{stageName}_{rec.Code}";

                try
                {
                    DismissLingeringDialog();

                    form ??= GetOrOpenFractionalInfoForm();
                    WaitForFormReady(form);

                    var context = GetGeneralTab(form);
                    FillFractionalFields(context, rec.Code, rec.NameTh, rec.NameEn, rec.Remark);

                    try { rec.ConfigureFormatTab(form); }
                    catch (Exception fmtEx)
                    {
                        LogFailureCounted($"{testName}_FormatTab", fmtEx.Message);
                        try { SwitchToGeneralTab(form); } catch { }
                    }

                    string saveButtonName = i == 0 ? "Save"
                                          : i == Step1_AddCases.Length - 1 ? "Save and Close"
                                          : "Save and New";

                    if (!TrySaveAndHandleDuplicate(form, testName, rec.Code, saveButtonName))
                    {
                        form = null;
                        continue;
                    }

                    if (saveButtonName == "Save")
                    {
                        form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();
                        WaitForClosed(() => FindOpenFractionalInfoForm());
                        form = null;
                        WaitForMainWindowReady();
                        TriggerGridRefresh();
                        LogGridResult(testName, rec.Code);
                    }
                    else if (saveButtonName == "Save and Close")
                    {
                        WaitForClosed(() => FindOpenFractionalInfoForm(), 8000);
                        form = null;
                        WaitForMainWindowReady();
                        TriggerGridRefresh();
                        LogGridResult(testName, rec.Code);
                    }
                    else if (saveButtonName == "Save and New")
                    {
                        SwitchToGeneralTab(form);
                        WaitUntil(() =>
                        {
                            var txt = form.FindFirstDescendant(cf => cf.ByAutomationId(IdFractCode))?.AsTextBox();
                            return string.IsNullOrEmpty(txt?.Text);
                        }, 4000);
                        LogSuccessCounted(testName, $"'{rec.Code}' บันทึกสำเร็จ (Save and New)");
                    }
                }
                catch (Exception ex)
                {
                    LogFailureCounted(testName, $"'{rec.Code}' เพิ่มไม่สำเร็จ: {ex.Message}");
                    CloseAnyOpenModalForm();
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
            try
            {
                var form = OpenFractionalInfoForm();
                WaitForFormReady(form);
                var context = GetGeneralTab(form);

                // Run เคส 1, 2, 3
                FillAndSaveThenAssert(context, form, Step2_AddValidationCases[0].Code, Step2_AddValidationCases[0].NameTh, Step2_AddValidationCases[0].SaveBtn,
                    Step2_AddValidationCases[0].TestCaseId, Step2_AddValidationCases[0].ExpectedErrors);

                FillAndSaveThenAssert(context, form, Step2_AddValidationCases[1].Code, Step2_AddValidationCases[1].NameTh, Step2_AddValidationCases[1].SaveBtn,
                    Step2_AddValidationCases[1].TestCaseId, Step2_AddValidationCases[1].ExpectedErrors);

                FillAndSaveThenAssert(context, form, Step2_AddValidationCases[2].Code, Step2_AddValidationCases[2].NameTh, Step2_AddValidationCases[2].SaveBtn,
                    Step2_AddValidationCases[2].TestCaseId, Step2_AddValidationCases[2].ExpectedErrors);

                // เคสที่ 4 เฉพาะของหน้านี้: ตารางรูปแบบการปัดเศษสตางค์ว่าง
                SetTextDirectly(context.FindFirstDescendant(cf => cf.ByAutomationId(IdFractCode)), "XFCM-007");
                SetTextDirectly(context.FindFirstDescendant(cf => cf.ByAutomationId(IdFractName)), "ปัดเศษเวลาทดสอบเคส D");
                try
                {
                    SwitchToFormatTab(form);
                    SelectRadio(form, IdRadioRoundMoney, RadioMoneyName);
                    SwitchToGeneralTab(form);
                }
                catch { }

                ClickFormButton(form, "Save and New");
                AssertTaskResultWarning(form, "ปัดเศษสตางค์", "TestCase04_EmptyGrid");

                // Run เคส 5, 6
                FillAndSaveThenAssert(context, form, Step2_AddValidationCases[3].Code, Step2_AddValidationCases[3].NameTh, Step2_AddValidationCases[3].SaveBtn,
                    Step2_AddValidationCases[3].TestCaseId, Step2_AddValidationCases[3].ExpectedErrors);

                string dupNameCase = Step2_AddValidationCases[4].TestCaseId;
                FillAndSaveThenAssert(context, form, Step2_AddValidationCases[4].Code, Step2_AddValidationCases[4].NameTh, Step2_AddValidationCases[4].SaveBtn,
                    dupNameCase, Step2_AddValidationCases[4].ExpectedErrors);

                RunCloseDialogSequence(form, dupNameCase);
            }
            catch (Exception ex)
            {
                LogFailureCounted(stageName, $"แจ้งเตือนบางเคสไม่ผ่าน: {ex.Message}");
            }
        }

        /// <summary>
        /// ขั้นที่ 3: เลือกแก้ไขรายการในตาราง
        /// </summary>
        public void RunEditTests()
        {
            foreach (var item in Step3_EditCases)
            {
                EditFractionalRecord(item.TestCaseId, item.ExistingCode, item.NewCode, item.NewNameTh,
                    item.NewNameEn, item.NewRemark, item.ConfigureFormatTab, item.SaveButtonName, item.NeedsExplicitClose);
            }
        }

        /// <summary>
        /// ขั้นที่ 4: รัน Validation ตอนแก้ไขข้อมูล
        /// </summary>
        public void RunEditValidationTests()
        {
            string baseTestName = "TestCase09_EditValidation";

            try
            {
                WaitForClosed(() => FindOpenFractionalInfoForm());
                WaitForMainWindowReady();
                TriggerGridRefresh();

                Window form = ClickRowAndOpenEditForm(EditTargetRow, baseTestName);
                WaitForFormReady(form);
                var context = GetGeneralTab(form);

                FillAndSaveThenAssert(context, form, Step4_EditValidationCases[0].Code, Step4_EditValidationCases[0].NameTh, Step4_EditValidationCases[0].SaveBtn,
                    Step4_EditValidationCases[0].TestCaseId, Step4_EditValidationCases[0].ExpectedErrors);

                FillAndSaveThenAssert(context, form, Step4_EditValidationCases[1].Code, Step4_EditValidationCases[1].NameTh, Step4_EditValidationCases[1].SaveBtn,
                    Step4_EditValidationCases[1].TestCaseId, Step4_EditValidationCases[1].ExpectedErrors);

                FillAndSaveThenAssert(context, form, Step4_EditValidationCases[2].Code, Step4_EditValidationCases[2].NameTh, Step4_EditValidationCases[2].SaveBtn,
                    Step4_EditValidationCases[2].TestCaseId, Step4_EditValidationCases[2].ExpectedErrors);

                WaitUntil(() => FindTaskResultPanel(form, 500) == null, 1500);

                FillAndSaveThenAssert(context, form, Step4_EditValidationCases[3].Code, Step4_EditValidationCases[3].NameTh, Step4_EditValidationCases[3].SaveBtn,
                    Step4_EditValidationCases[3].TestCaseId, Step4_EditValidationCases[3].ExpectedErrors);

                string subCase = Step4_EditValidationCases[4].TestCaseId;
                FillAndSaveThenAssert(context, form, Step4_EditValidationCases[4].Code, Step4_EditValidationCases[4].NameTh, Step4_EditValidationCases[4].SaveBtn,
                    subCase, Step4_EditValidationCases[4].ExpectedErrors);

                RunCloseDialogSequence(form, subCase);

                LogSuccessCounted(baseTestName, "รัน Validation Edit ครบทุกเคสแล้ว");
            }
            catch (Exception ex)
            {
                LogFailureCounted(baseTestName, $"Validation Edit ไม่ผ่านบางเคส: {ex.Message}");
            }
        }

        /// <summary>
        /// ขั้นที่ 5: ทดสอบการลบรายการในตาราง
        /// </summary>
        public void RunDeleteTests()
        {
            string baseTestName = "TestCase10_Delete";

            WaitForMainWindowReady();
            TriggerGridRefresh();

            foreach (var testCase in Step5_DeleteCases)
            {
                RunDeleteCase(baseTestName, testCase.TargetCode, testCase.DialogBtn, testCase.ExpectDeleted);
            }

            LogSuccessCounted(baseTestName, "ทดสอบ Delete ครบทุกเคส");
            TestLogger.LogInfo(ScreenLogName, $"สรุปผลรวมทั้งหมด: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");
        }

        // --- Custom Mapping & Control Logic ประจำหน้าจอนี้ ---
        private void FillFractionalFields(AutomationElement context, string code, string nameTh, string nameEn, string remark)
        {
            SetTextDirectly(context.FindFirstDescendant(cf => cf.ByAutomationId(IdFractCode)), code);
            SetTextDirectly(context.FindFirstDescendant(cf => cf.ByAutomationId(IdFractName)), nameTh);
            SetTextDirectly(context.FindFirstDescendant(cf => cf.ByAutomationId(IdFractNameEng)), nameEn);
            SetTextDirectly(context.FindFirstDescendant(cf => cf.ByAutomationId(IdRemark)), remark);
        }

        private static void SwitchToFormatTab(Window form)
        {
            var tab = Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(IdTabHeaderFormat))
                               ?? form.FindFirstDescendant(cf => cf.ByName(TabFormatName)), 3000)
                      ?? throw new Exception($"หาแท็บ '{TabFormatName}' ไม่เจอ");
            ClickElementByMouse(tab);
            WaitUntil(() => form.FindFirstDescendant(cf => cf.ByAutomationId(IdRadioRoundMoney)) != null, 2500);
        }

        private static void SwitchToGeneralTab(Window form)
        {
            var tab = Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(IdTabHeaderGeneral))
                               ?? form.FindFirstDescendant(cf => cf.ByName(TabGeneralName)), 3000)
                      ?? throw new Exception($"หาแท็บ '{TabGeneralName}' ไม่เจอ");
            ClickElementByMouse(tab);
            WaitUntil(() => form.FindFirstDescendant(cf => cf.ByAutomationId(IdFractCode)) != null, 2500);
        }

        private static void SelectRadio(Window form, string radioId, string radioName)
        {
            var radio = Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(radioId))
                                  ?? form.FindFirstDescendant(cf => cf.ByName(radioName)), 3000)
                        ?? throw new Exception($"หา radio '{radioName}' ไม่เจอ");
            ClickElementByMouse(radio);
        }

        private static void TickCheckbox(Window form, string checkboxId, string checkboxName, bool tick)
        {
            var found = Retry(() =>
                form.FindFirstDescendant(cf => cf.ByAutomationId(checkboxId))
                ?? form.FindFirstDescendant(cf => cf.ByName(checkboxName))
                ?? form.FindFirstDescendant(cf => cf.ByControlType(ControlType.CheckBox).And(cf.ByName(checkboxName))), 3000)
                ?? throw new Exception($"หา checkbox '{checkboxName}' ไม่เจอ");

            bool CurrentlyTicked()
            {
                try
                {
                    var cb = found.AsCheckBox();
                    if (cb != null && cb.Patterns.Toggle.IsSupported)
                    {
                        return cb.ToggleState == FlaUI.Core.Definitions.ToggleState.On;
                    }
                    if (cb?.IsChecked != null) return cb.IsChecked == true;
                }
                catch { }

                try
                {
                    if (found.Patterns.Value.IsSupported)
                    {
                        string? v = found.Patterns.Value.Pattern.Value.Value?.Trim().ToLower();
                        return v == "1" || v == "true" || v == "checked";
                    }
                }
                catch { }

                return false;
            }

            if (CurrentlyTicked() == tick) return;

            try
            {
                if (found.Patterns.Toggle.IsSupported)
                {
                    found.Patterns.Toggle.Pattern.Toggle();
                    if (CurrentlyTicked() == tick) return;
                }
            }
            catch { }

            var rect = found.BoundingRectangle;
            if (!rect.IsEmpty)
            {
                var boxPoint = new Point((int)(rect.Left + 10), (int)(rect.Top + (rect.Height / 2)));
                Mouse.Click(boxPoint);
            }
            else
            {
                ClickElementByMouse(found);
            }

            if (CurrentlyTicked() != tick)
            {
                try
                {
                    found.Focus();
                    Keyboard.Press(VirtualKeyShort.SPACE);
                    Keyboard.Release(VirtualKeyShort.SPACE);
                }
                catch { }
            }
        }

        private static void FillGridRows(Window form, string gridId, string gridLabel, (string From, string To, string Value)[] rows)
        {
            if (rows.Length > 0 && rows[0].From == "0")
                throw new Exception($"ตาราง '{gridLabel}' ต้องเริ่มค่า 'ตั้งแต่' ที่ 1 เท่านั้น (ห้ามใช้ 0)");

            var grid = Retry(() =>
                form.FindFirstDescendant(cf => cf.ByAutomationId(gridId).And(cf.ByClassName("TcxGridSite")))
                ?? form.FindFirstDescendant(cf => cf.ByAutomationId(gridId).And(cf.ByControlType(ControlType.DataGrid)))
                ?? form.FindFirstDescendant(cf => cf.ByAutomationId(gridId)), 3000)
                ?? throw new Exception($"หาตาราง '{gridLabel}' ไม่พบ");

            var rect = grid.BoundingRectangle;
            var focusPoint = new Point((int)(rect.X + rect.Width / 2), (int)(rect.Y + rect.Height / 2));
            Mouse.Click(focusPoint);

            for (int i = 0; i < rows.Length; i++)
            {
                var (from, to, value) = rows[i];
                var addButton = grid.FindFirstDescendant(cf => cf.ByName("+"))
                                ?? grid.FindAllDescendants(cf => cf.ByControlType(ControlType.Button)).FirstOrDefault(b => b.Name == "+");

                if (addButton != null)
                {
                    ClickElementByMouse(addButton);
                }
                else
                {
                    grid.Focus();
                    Keyboard.Press(VirtualKeyShort.INSERT);
                }

                grid.Focus();
                Keyboard.Press(VirtualKeyShort.HOME);

                Keyboard.Type(from);
                Keyboard.Press(VirtualKeyShort.TAB);

                Keyboard.Type(to);
                Keyboard.Press(VirtualKeyShort.TAB);

                Keyboard.Type(value);
                Keyboard.Press(VirtualKeyShort.RETURN);
            }
        }

        private Window ClickRowAndOpenEditForm(string fractCodeToFind, string testName)
        {
            AutomationElement? row = FindRowInGrid(fractCodeToFind, timeoutMs: 6000);
            if (row == null)
            {
                TriggerGridRefresh();
                row = FindRowInGrid(fractCodeToFind, timeoutMs: 6000)
                      ?? throw new Exception($"หาแถวสำหรับ '{fractCodeToFind}' ไม่เจอ");
            }

            ClickElementByMouse(row);

            var editButton = Retry(() =>
            {
                var b = _mainWindow.FindFirstDescendant(cf => cf.ByName("Edit"));
                return (b != null && b.Properties.IsEnabled.ValueOrDefault) ? b : null;
            }, 3000) ?? throw new Exception("หาปุ่ม Edit ไม่เจอ");

            ClickElementByMouse(editButton);
            var form = Retry(FindOpenFractionalInfoForm, 6000) ?? throw new Exception("หาฟอร์ม FractionalInfoUC ไม่เจอ");
            WaitForFormReady(form);
            return form;
        }

        private void EditFractionalRecord(string testName, string existingCode, string newCode, string newNameTh,
            string newNameEn, string newRemark, Action<Window>? configureFormatTab, string saveButtonName, bool needsExplicitClose)
        {
            try
            {
                Window form = ClickRowAndOpenEditForm(existingCode, testName);
                WaitForFormReady(form);
                var context = GetGeneralTab(form);

                FillFractionalFields(context, newCode, newNameTh, newNameEn, newRemark);

                if (configureFormatTab != null)
                {
                    try { configureFormatTab(form); }
                    catch (Exception fmtEx)
                    {
                        LogFailureCounted($"{testName}_FormatTab", fmtEx.Message);
                        try { SwitchToGeneralTab(form); } catch { }
                    }
                }

                if (!TrySaveAndHandleDuplicate(form, testName, newCode, saveButtonName))
                    return;

                if (needsExplicitClose)
                    form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();

                WaitForClosed(() => FindOpenFractionalInfoForm(), 6000);
                WaitForMainWindowReady();
                TriggerGridRefresh();

                bool exists = VerifyRecordExistsInGrid(newCode, 3500);
                if (exists)
                    LogSuccessCounted(testName, $"แก้ไข '{existingCode}' เป็น '{newCode}' สำเร็จ (พบในตารางหลัก)");
                else
                    LogFailureCounted(testName, $"แก้ไข '{existingCode}' เป็น '{newCode}' แล้ว แต่ไม่พบในตารางหลัก");
            }
            catch (Exception ex)
            {
                LogFailureCounted(testName, $"แก้ไข '{existingCode}' ไม่สำเร็จ: {ex.Message}");
            }
        }

        private void DeleteRecordAndVerify(string fractCode, string confirmButton, string testName, bool expectDeleted)
        {
            var row = FindRowInGrid(fractCode, timeoutMs: 8000)
                      ?? throw new Exception($"หาแถวสำหรับลบ ({fractCode}) ไม่เจอ");

            WaitUntil(() => { try { row.Focus(); return true; } catch { return false; } }, 1000);
            ClickElementByMouse(row);

            var deleteButton = Retry(() =>
            {
                var b = _mainWindow.FindFirstDescendant(cf => cf.ByName("Delete"));
                return (b != null && b.Properties.IsEnabled.ValueOrDefault) ? b : null;
            }, 3000) ?? throw new Exception("หาปุ่ม Delete ไม่เจอ");
            ClickElementByMouse(deleteButton);

            Window? confirmDialog = Retry(() =>
                _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault()
                ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("คำเตือน"))?.AsWindow(),
                timeoutMs: 4000) ?? throw new Exception("กด Delete แล้วไม่พบ Pop-up ยืนยัน");

            ClickDialogButton(confirmDialog, confirmButton, "ยืนยันการลบ");
            WaitForClosed(() => confirmDialog);

            TriggerGridRefresh();

            bool stillExistsOnGrid = VerifyRecordExistsInGrid(fractCode, timeoutMs: 3000);
            bool hasSuccessMessage = FindTaskResultPanel(_mainWindow.AsWindow(), timeoutMs: 500)?
                .FindAllDescendants()
                .Any(e => !string.IsNullOrEmpty(e.Name) && e.Name.Contains("Deleted succeeded")) ?? false;

            if (expectDeleted)
            {
                if (stillExistsOnGrid && !hasSuccessMessage)
                {
                    string msg = $"'{fractCode}' ลบข้อมูลไม่สำเร็จ";
                    LogFailureCounted(testName, msg);
                    throw new Exception(msg);
                }
                LogSuccessCounted(testName, $"'{fractCode}' ถูกลบสำเร็จ");
            }
            else
            {
                if (!stillExistsOnGrid)
                {
                    string msg = $"'{fractCode}' ลบข้อมูลผิดพลาด";
                    LogFailureCounted(testName, msg);
                    throw new Exception(msg);
                }
                LogSuccessCounted(testName, $"'{fractCode}' ข้อมูลยังไม่ถูกลบ");
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
        private const int TimeoutMs = 8000;

        public FractionalInfoUC(AutomationElement mainWindow, UIA3Automation automation)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

        private bool TryOpenFractionalMenu(string stage)
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
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("treeListColumn row 7")), 4000)
                                 ?? throw new Exception("ไม่พบผลลัพธ์เมนูจากการค้นหา");

                menuResult.Focus();
                menuResult.Click();
                Keyboard.Press(VirtualKeyShort.ENTER);

                return WaitForMainWindowReady();
            }
            catch (Exception ex)
            {
                LogFailureCounted(stage, $"ค้นหาและเปิดหน้าจอไม่สำเร็จ: {ex.Message}");
                return false;
            }
        }

        private bool WaitForMainWindowReady()
        {
            return WaitUntil(() =>
            {
                var newBtn = _mainWindow.FindFirstDescendant(cf => cf.ByName("New"));
                var grid = GetMainDataGrid();

                bool isNewBtnReady = newBtn != null && newBtn.Properties.IsEnabled.ValueOrDefault && !newBtn.Properties.IsOffscreen.ValueOrDefault;
                bool isGridReady = grid != null && !grid.Properties.IsOffscreen.ValueOrDefault;

                return isNewBtnReady && isGridReady;
            }, TimeoutMs);
        }

        private bool WaitForFormReady(Window? form)
        {
            if (form == null) return false;
            return WaitUntil(() =>
            {
                if (!form.IsAvailable || form.Properties.IsOffscreen.ValueOrDefault) return false;
                var codeField = form.FindFirstDescendant(cf => cf.ByAutomationId(IdFractCode));
                return codeField != null && codeField.Properties.IsEnabled.ValueOrDefault && !codeField.Properties.IsOffscreen.ValueOrDefault;
            }, TimeoutMs);
        }

        private Window GetOrOpenFractionalInfoForm() =>
            Retry(FindOpenFractionalInfoForm, 1500) ?? OpenFractionalInfoForm();

        private Window OpenFractionalInfoForm()
        {
            CloseAnyOpenModalForm();
            var newBtn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("New")), 3000)
                         ?? throw new Exception("หาปุ่ม New ไม่เจอ");
            ClickElementByMouse(newBtn);
            var form = Retry(FindOpenFractionalInfoForm, 8000) ?? throw new Exception("หาฟอร์ม FractionalInfoUC ไม่เจอ");
            WaitForFormReady(form);
            return form;
        }

        private Window? FindOpenFractionalInfoForm() =>
            _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault(w => w.AutomationId == FormId || w.Name.Contains(FormTitle))
            ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByAutomationId(FormId).Or(cf.ByName(FormTitle)))?.AsWindow();

        private AutomationElement GetGeneralTab(Window form) =>
            Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(GeneralTabId)), 2000) ?? form;

        private void RunCloseDialogSequence(Window form, string testNamePrefix)
        {
            // 1. ทดสอบกด Close -> กด Yes (คาดหวัง: หน้าต่างไม่ปิด)
            string subCase = $"{testNamePrefix}_Close_Yes";
            ClickCloseAndHandleDialog(form, "Yes", subCase);
            AssertFormStillOpen(form, subCase);

            // 2. ทดสอบกด Close -> กด Cancel (คาดหวัง: หน้าต่างไม่ปิด)
            subCase = $"{testNamePrefix}_Close_Cancel";
            ClickCloseAndHandleDialog(form, "Cancel", subCase);
            AssertFormStillOpen(form, subCase);

            // 3. ทดสอบกด Close -> กด No (คาดหวัง: หน้าต่างปิดลง)
            subCase = $"{testNamePrefix}_Close_No";
            ClickCloseAndHandleDialog(form, "No", subCase);
            AssertFormClosed(form, subCase);
        }

        private void RunDeleteCase(string baseTestName, string fractCode, string confirmButton, bool expectDeleted)
        {
            string testName = $"{baseTestName}_{fractCode.Replace("-", "").Replace(" ", "")}_{confirmButton}";
            try
            {
                DeleteRecordAndVerify(fractCode, confirmButton, testName, expectDeleted);
            }
            catch (Exception ex)
            {
                LogFailureCounted(testName, $"ลบ '{fractCode}' ไม่สำเร็จ: {ex.Message}");
            }
        }

        private bool TrySaveAndHandleDuplicate(Window form, string testName, string fractCode, string saveButtonName)
        {
            ClickFormButton(form, saveButtonName);

            string? duplicateReason = TryDetectDuplicateWarning(form);
            if (duplicateReason == null) return true;

            LogFailureCounted(testName, $"'{fractCode}' {duplicateReason}");
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

        private void FillAndSaveThenAssert(AutomationElement context, Window form, string code, string name,
            string saveButtonName, string testName, params string[] expectedMessages)
        {
            const int maxAttempts = 2;
            Exception? lastError = null;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                SetTextDirectly(context.FindFirstDescendant(cf => cf.ByAutomationId(IdFractCode)), code);
                SetTextDirectly(context.FindFirstDescendant(cf => cf.ByAutomationId(IdFractName)), name);

                WaitUntil(() => { try { form.Focus(); return true; } catch { return false; } }, 1000);

                ClickFormButton(form, saveButtonName);

                try
                {
                    var foundTexts = new List<string>();
                    var sw = Stopwatch.StartNew();

                    while (sw.ElapsedMilliseconds < 5000)
                    {
                        var panel = FindTaskResultPanel(form, timeoutMs: 600);
                        if (panel != null)
                        {
                            var panelTexts = GetPanelTexts(panel).ToList();
                            foundTexts = expectedMessages.Where(m => panelTexts.Any(pt => pt.IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
                            if (foundTexts.Count == expectedMessages.Length)
                                break;
                        }
                        Thread.Sleep(100);
                    }

                    if (foundTexts.Count == expectedMessages.Length)
                    {
                        string formattedList = string.Join(",", expectedMessages.Select(m => $"'{m}'"));
                        LogSuccessCounted(testName, $"พบข้อความ: {formattedList}");
                        return;
                    }

                    throw new Exception($"พบข้อความแจ้งเตือนไม่ครบ: [{string.Join(", ", expectedMessages)}]");
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    if (attempt < maxAttempts)
                    {
                        Thread.Sleep(300);
                    }
                }
            }

            throw lastError ?? new Exception($"ไม่พบข้อความเตือน '{testName}'");
        }

        private void ClickFormButton(Window form, string buttonName, int timeoutMs = 4000)
        {
            var button = Retry(() => form.FindFirstDescendant(cf => cf.ByName(buttonName) ?? cf.ByName($"&{buttonName}")), timeoutMs)
                ?? throw new Exception($"หาปุ่ม '{buttonName}' บนฟอร์มไม่เจอ");
            ClickElementByMouse(button);
        }

        private string? TryDetectDuplicateWarning(Window form)
        {
            var taskResultPanel = FindTaskResultPanel(form, timeoutMs: 1500);
            if (taskResultPanel == null) return null;

            foreach (var text in GetPanelTexts(taskResultPanel))
            {
                if (text.Contains("รหัสรูปแบบการปัดเศษ ห้ามซ้ำ")) return "รหัสรูปแบบการปัดเศษซ้ำ";
                if (text.Contains("ชื่อรูปแบบการปัดเศษ ห้ามซ้ำ")) return "ชื่อรูปแบบการปัดเศษซ้ำ";
            }
            return null;
        }

        private AutomationElement? FindTaskResultPanel(Window? form, int timeoutMs = 3000) =>
            Retry(() =>
                (form != null ? form.FindFirstDescendant(cf => cf.ByName("Task Result").Or(cf.ByAutomationId("dockPanelTaskResult"))) : null)
                ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("Task Result").Or(cf.ByAutomationId("dockPanelTaskResult"))),
                timeoutMs);

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

        private void LogGridResult(string testName, string code)
        {
            if (VerifyRecordExistsInGrid(code)) LogSuccessCounted(testName, $"'{code}' เพิ่มสำเร็จ (พบในตารางหลัก)");
            else LogFailureCounted(testName, $"'{code}' ไม่พบในตารางหลักหลังบันทึก");
        }

        private void TriggerGridRefresh()
        {
            try
            {
                var findBtn = _mainWindow.FindFirstDescendant(cf => cf.ByName("Find").Or(cf.ByAutomationId("btnFind")));
                if (findBtn != null && findBtn.Properties.IsEnabled.ValueOrDefault)
                {
                    findBtn.Click();
                    WaitUntil(() =>
                    {
                        var dataGrid = GetMainDataGrid();
                        return dataGrid != null && !dataGrid.Properties.IsOffscreen.ValueOrDefault;
                    }, 3000);
                }
            }
            catch { }
        }

        private AutomationElement? GetMainDataGrid() =>
            _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataGrid))
            ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Table));

        private bool VerifyRecordExistsInGrid(string value, int timeoutMs = 6000) =>
            FindRowInGrid(value, timeoutMs) != null;

        private AutomationElement? FindRowInGrid(string value, int timeoutMs = 6000)
        {
            var dataGrid = GetMainDataGrid();
            if (dataGrid != null)
            {
                WaitUntil(() => { try { dataGrid.Focus(); return true; } catch { return false; } }, 1000);
                ClickElementByMouse(dataGrid);
            }

            var found = Retry(() => FindElementMatchingValue(value), 1500);
            if (found != null) return found;

            try { Keyboard.Press(VirtualKeyShort.HOME); } catch { }
            found = Retry(() => FindElementMatchingValue(value), 1200);
            if (found != null) return found;

            TriggerGridRefresh();

            try
            {
                if (dataGrid != null)
                {
                    dataGrid.Focus();
                    for (int step = 0; step < 5; step++)
                    {
                        Keyboard.Press(VirtualKeyShort.NEXT);
                        found = Retry(() => FindElementMatchingValue(value), 800);
                        if (found != null) return found;
                    }
                    Keyboard.Press(VirtualKeyShort.END);
                    found = Retry(() => FindElementMatchingValue(value), 1200);
                    if (found != null) return found;

                    Keyboard.Press(VirtualKeyShort.HOME);
                }
            }
            catch { }

            return Retry(() => FindElementMatchingValue(value), 2000);
        }

        private AutomationElement? FindElementMatchingValue(string value)
        {
            string cleanVal = value.Trim();
            foreach (var el in _mainWindow.FindAllDescendants())
            {
                if (!string.IsNullOrEmpty(el.Name) && (el.Name.Trim().Equals(cleanVal, StringComparison.OrdinalIgnoreCase) || el.Name.Trim().Contains(cleanVal)))
                    return el;
                try
                {
                    if (el.Patterns.Value.IsSupported)
                    {
                        var val = el.Patterns.Value.Pattern.Value.Value?.Trim();
                        if (!string.IsNullOrEmpty(val) && (val.Equals(cleanVal, StringComparison.OrdinalIgnoreCase) || val.Contains(cleanVal)))
                            return el;
                    }
                }
                catch { }
            }
            return null;
        }

        private void DismissLingeringDialog()
        {
            Window? lingering = Retry(() => _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault(), timeoutMs: 600);
            if (lingering == null) return;

            foreach (var buttonName in new[] { "No", "Cancel", "Yes" })
            {
                try
                {
                    ClickDialogButton(lingering, buttonName, "dialog ค้าง");
                    WaitForClosed(() => lingering);
                    return;
                }
                catch { }
            }
        }

        private void ClickCloseAndHandleDialog(Window form, string buttonToClick, string testName)
        {
            form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();

            Window? confirmDialog = Retry(() =>
                form.ModalWindows.FirstOrDefault()
                ?? _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault(),
                timeoutMs: 3000);

            if (confirmDialog == null) throw new Exception("กด Close แล้วไม่พบ Pop-up ยืนยัน");
            ClickDialogButton(confirmDialog, buttonToClick, "ยืนยัน");
            WaitForClosed(() => confirmDialog);
        }

        private void ClickDialogButton(Window dialog, string buttonName, string dialogDescription = "")
        {
            var btn = dialog.FindFirstDescendant(cf => cf.ByName(buttonName))?.AsButton()
                      ?? dialog.FindFirstDescendant(cf => cf.ByName($"&{buttonName}"))?.AsButton()
                      ?? dialog.FindFirstDescendant(cf => cf.ByName(buttonName == "Yes" ? "ใช่" : "ไม่ใช่"))?.AsButton()
                      ?? throw new Exception($"ไม่พบปุ่ม '{buttonName}' บน Pop-up {dialogDescription}");
            ClickElementByMouse(btn);
        }

        private void CloseAnyOpenModalForm()
        {
            try
            {
                var modal = _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault();
                if (modal != null)
                {
                    var closeBtn = modal.FindFirstDescendant(cf => cf.ByName("Close"));
                    if (closeBtn != null)
                    {
                        ClickElementByMouse(closeBtn);
                        WaitForClosed(() => modal, 2000);
                    }
                }
            }
            catch { }
        }

        private void AssertFormStillOpen(Window form, string testName)
        {
            bool isOpen = WaitUntil(() => !form.Properties.IsOffscreen.ValueOrDefault && form.IsAvailable, 2000);
            if (!isOpen) throw new Exception("ฟอร์มถูกปิด");
            LogSuccessCounted(testName, "ฟอร์มถูกเปิดอยู่");
        }

        private void AssertFormClosed(Window form, string testName)
        {
            bool isClosed = WaitForClosed(() => FindOpenFractionalInfoForm(), 3000);
            if (!isClosed) throw new Exception("ปิดฟอร์มไม่สำเร็จ");
            LogSuccessCounted(testName, "ฟอร์มถูกปิด");
        }

        private void AssertTaskResultWarning(Window form, string expectedWarningMessage, string testName)
        {
            bool foundMessage = false;
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < 5000)
            {
                try
                {
                    var taskResultPanel = FindTaskResultPanel(form, timeoutMs: 800);
                    if (taskResultPanel != null &&
                        GetPanelTexts(taskResultPanel).Any(t => t.IndexOf(expectedWarningMessage, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        foundMessage = true;
                        break;
                    }
                }
                catch { }
                Thread.Sleep(100);
            }

            if (!foundMessage) throw new Exception($"ไม่พบข้อความ: '{expectedWarningMessage}'");
            LogSuccessCounted(testName, $"พบข้อความ: '{expectedWarningMessage}'");
        }

        private static void SetTextDirectly(AutomationElement? element, string text)
        {
            if (element == null) return;

            if (element.Patterns.Value.IsSupported)
            {
                try
                {
                    element.Patterns.Value.Pattern.SetValue(text ?? "");
                    return;
                }
                catch { }
            }

            ClickElementByMouse(element);
            try { element.Focus(); } catch { }

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

        private static void ClickElementByMouse(AutomationElement element)
        {
            var rect = element.BoundingRectangle;
            if (rect.IsEmpty)
            {
                element.Focus();
                return;
            }
            Mouse.Click(new Point(rect.Left + (rect.Width / 2), rect.Top + (rect.Height / 2)));
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

        private static bool WaitUntil(Func<bool> condition, int timeoutMs = TimeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try { if (condition()) return true; } catch { }
                Thread.Sleep(50);
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
                Thread.Sleep(50);
            }
            return null;
        }
    }
}