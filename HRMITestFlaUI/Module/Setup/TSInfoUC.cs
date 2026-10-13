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
    public class TSInfoUC
    {
        // =========================================================================================
        // หมวดที่ 1: ข้อมูลการทดสอบทั้ง 5 ขั้น (Test Data)
        // =========================================================================================

        private sealed record ShiftAddItem(
            string Code,
            int Progress,
            string ProgressMsg,
            Action<TSInfoUC, Window> FillFormAction,
            string SaveMode);

        private sealed record ShiftEditItem(
            string TestCaseId,
            string TargetCode,
            Action<TSInfoUC, Window> EditAction,
            string SaveMode);

        private sealed record DeleteItem(
            string TestCaseId,
            string TargetCode,
            string DialogBtn,
            bool ExpectDeleted,
            string SuccessMsg,
            string FailureMsg);

        // [ขั้นที่ 1] ข้อมูลทดสอบการสร้างใหม่ (Add / Navigate)
        private static readonly ShiftAddItem[] Step1_AddCases =
        {
            // รายการที่ 1: กะงานปกติ -> NRM-M01 -> Save แล้วกด Close
            new(
                "NRM-M01", 10, "Adding Shift (1/7): NRM-M01...",
                (page, form) =>
                {
                    page.SelectTab(form, "General");
                    page.SelectDevExpressComboByValue(form, "devCbxShiftOT", "กะงานปกติ");
                    page.SetInputTextSafely(form, "devTxtShiftCode", "NRM-M01");
                    page.SetInputTextSafely(form, "devTxtTimeShiftName", "กะออฟฟิศมาตรฐาน");
                    page.SetInputTextSafely(form, "devTxtStarttime", "06:00");
                    page.SetInputTextSafely(form, "devTxtEndTime", "21:00");

                    page.SelectDevExpressComboByValue(form, "devCbxTime", "2");
                    page.SetInputTextSafely(form, "devSpinGap", "5");
                    page.SetRadioButton(form, "devRdgOpBreak1");

                    page.SetInputTextSafely(form, "devTxtTimeIn1", "08:00");
                    page.SetInputTextSafely(form, "devTxtLateTime1", "15");
                    page.SetInputTextSafely(form, "devTxtTimeOut1", "17:00");
                    page.SetInputTextSafely(form, "devTxtStartBreakTime1", "12:00");
                    page.SetInputTextSafely(form, "devTxtEndBreakTime1", "13:00");
                },
                "SaveAndClose"
            ),

            // รายการที่ 2: กะงานปกติ -> NRM-M02 (กะดึกข้ามคืน) -> Save and New
            new(
                "NRM-M02", 14, "Adding Shift (2/7): NRM-M02...",
                (page, form) =>
                {
                    page.SelectTab(form, "General");
                    page.SelectDevExpressComboByValue(form, "devCbxShiftOT", "กะงานปกติ");
                    page.SetInputTextSafely(form, "devTxtShiftCode", "NRM-M02");
                    page.SetInputTextSafely(form, "devTxtTimeShiftName", "กะดึกข้ามคืน");
                    page.SetInputTextSafely(form, "devTxtStarttime", "21:00");
                    page.SetInputTextSafely(form, "devTxtEndTime", "10:00");
                    page.ChangeEndTimeToNextDay(form);

                    page.SelectDevExpressComboByValue(form, "devCbxTime", "4");
                    page.SetInputTextSafely(form, "devSpinGap", "5");
                    page.SetRadioButton(form, "devRdgOpBreak1");

                    page.SetInputTextSafely(form, "devTxtTimeIn1", "23:00");
                    page.SetInputTextSafely(form, "devTxtLateTime1", "15");
                    page.SetInputTextSafely(form, "devTxtTimeOut1", "03:00");
                    page.ClickNextDayButtonBesideField(form, "devTxtTimeOut1");

                    page.SetInputTextSafely(form, "devTxtStartBreakTime1", "03:00");
                    page.ClickNextDayButtonBesideField(form, "devTxtStartBreakTime1");
                    page.SetInputTextSafely(form, "devTxtEndBreakTime1", "04:00");
                    page.ClickNextDayButtonBesideField(form, "devTxtEndBreakTime1");

                    page.SetInputTextSafely(form, "devTxtTimeIn2", "04:00");
                    page.ClickNextDayButtonBesideField(form, "devTxtTimeIn2");
                    page.SetInputTextSafely(form, "devTxtLateTime2", "5");
                    page.SetInputTextSafely(form, "devTxtTimeOut2", "08:00");
                    page.ClickNextDayButtonBesideField(form, "devTxtTimeOut2");
                    page.SetInputTextSafely(form, "devTxtBeforeTime2", "5");
                },
                "SaveAndNew"
            ),

            // รายการที่ 3: กะงานปกติ -> NRM-M03 (กะบ่าย) -> Save and Close
            new(
                "NRM-M03", 18, "Adding Shift (3/7): NRM-M03...",
                (page, form) =>
                {
                    page.SelectTab(form, "General");
                    page.SelectDevExpressComboByValue(form, "devCbxShiftOT", "กะงานปกติ");
                    page.SetInputTextSafely(form, "devTxtShiftCode", "NRM-M03");
                    page.SetInputTextSafely(form, "devTxtTimeShiftName", "กะบ่าย");
                    page.SetInputTextSafely(form, "devTxtStarttime", "12:30");
                    page.SetInputTextSafely(form, "devTxtEndTime", "01:00");
                    page.ChangeEndTimeToNextDay(form);

                    // เลือก 6 ครั้ง และรอให้คอนโทรลช่วง 3 พร้อมทำงานจริง
                    page.SelectDevExpressComboByValue(form, "devCbxTime", "6");
                    Keyboard.Press(VirtualKeyShort.TAB);
                    WaitUntil(() =>
                    {
                        var timeIn3 = form.FindFirstDescendant(cf => cf.ByAutomationId("devTxtTimeIn3"));
                        return timeIn3 != null && timeIn3.Properties.IsEnabled.ValueOrDefault;
                    }, 3000);

                    page.SetInputTextSafely(form, "devSpinGap", "5");
                    page.SetRadioButton(form, "devRdgOpBreak1");

                    // ช่วงที่ 1
                    page.SetInputTextSafely(form, "devTxtTimeIn1", "14:00");
                    page.SetInputTextSafely(form, "devTxtLateTime1", "10");
                    page.SetInputTextSafely(form, "devTxtTimeOut1", "18:00");
                    page.SetInputTextSafely(form, "devTxtBeforeTime1", "5");
                    page.SetInputTextSafely(form, "devTxtStartBreakTime1", "18:00");
                    page.SetInputTextSafely(form, "devTxtEndBreakTime1", "19:00");

                    // ช่วงที่ 2
                    page.SetInputTextSafely(form, "devTxtTimeIn2", "19:00");
                    page.SetInputTextSafely(form, "devTxtTimeOut2", "21:00");
                    page.SetInputTextSafely(form, "devTxtStartBreakTime2", "21:00");
                    page.ClearField(form, "devTxtEndBreakTime2");
                    page.SetInputTextSafely(form, "devTxtEndBreakTime2", "21:30");

                    // ช่วงที่ 3
                    page.SetInputTextSafely(form, "devTxtTimeIn3", "21:30");
                    page.SetInputTextSafely(form, "devTxtLateTime3", "5");
                    page.SetInputTextSafely(form, "devTxtTimeOut3", "23:00");
                    page.SetInputTextSafely(form, "devTxtBeforeTime3", "3");
                    page.ClearField(form, "devTxtStartBreakTime3");
                    page.ClearField(form, "devTxtEndBreakTime3");
                },
                "SaveAndClose"
            ),

            // รายการที่ 4: กะงานล่วงเวลา -> OT-EVN01 -> Save and Close
            new(
                "OT-EVN01", 22, "Adding Shift (4/7): OT-EVN01...",
                (page, form) =>
                {
                    page.SelectTab(form, "General");
                    page.SelectDevExpressComboByValue(form, "devCbxShiftOT", "กะงานล่วงเวลา");
                    page.SelectDevExpressComboByValue(form, "devCbxShiftOTType", "OT หลังเวลางาน");
                    page.SetInputTextSafely(form, "devTxtShiftCode", "OT-EVN01");
                    page.SelectInOutTypeByValue(form, "แยกจากกะปกติ");
                    page.SetInputTextSafely(form, "devTxtTimeShiftName", "กะ OT เย็น");

                    page.SelectDevExpressComboByValue(form, "devCbxTime", "2");
                    page.SetInputTextSafely(form, "devSpinGap", "5");
                    page.SetInputTextSafely(form, "devTxtTimeIn1", "17:00");
                    page.SetInputTextSafely(form, "devTxtTimeOut1", "23:00");
                    page.SetInputTextSafely(form, "devTxtStartBreakTime1", "20:00");
                    page.SetInputTextSafely(form, "devTxtEndBreakTime1", "21:00");

                    page.SelectTab(form, "Details");
                    page.FillDetailsTabForFixedHours(form, "06:00", "OT-001");
                },
                "SaveAndClose"
            ),

            // รายการที่ 5: กะงานล่วงเวลา -> OT-EVN02 -> Save and Close
            new(
                "OT-EVN02", 25, "Adding Shift (5/7): OT-EVN02...",
                (page, form) =>
                {
                    page.SelectTab(form, "General");
                    page.SelectDevExpressComboByValue(form, "devCbxShiftOT", "กะงานล่วงเวลา");
                    page.SelectDevExpressComboByValue(form, "devCbxShiftOTType", "กะ OT วันหยุด");
                    page.SetInputTextSafely(form, "devTxtShiftCode", "OT-EVN02");
                    page.SetInputTextSafely(form, "devTxtTimeShiftName", "กะ OT วันหยุดเต็มวัน");
                    page.SetInputTextSafely(form, "devTxtStarttime", "06:30");
                    page.SetInputTextSafely(form, "devTxtEndTime", "19:00");

                    page.SelectDevExpressComboByValue(form, "devCbxTime", "4");
                    page.SetInputTextSafely(form, "devSpinGap", "5");

                    page.SetInputTextSafely(form, "devTxtTimeIn1", "08:00");
                    page.SetInputTextSafely(form, "devTxtTimeOut1", "12:00");
                    page.SetInputTextSafely(form, "devTxtStartBreakTime1", "12:00");
                    page.SetInputTextSafely(form, "devTxtEndBreakTime1", "13:00");

                    page.SetInputTextSafely(form, "devTxtTimeIn2", "13:00");
                    page.SetInputTextSafely(form, "devTxtTimeOut2", "17:00");

                    page.SelectTab(form, "Details");
                    page.SelectLookupItemByCode(form, "devBtnDeductCode3", "OT-001");
                },
                "SaveAndClose"
            ),

            // รายการที่ 6: กะงานล่วงเวลา -> OT-EVN03 -> Save and Close
            new(
                "OT-EVN03", 28, "Adding Shift (6/7): OT-EVN03...",
                (page, form) =>
                {
                    page.SelectTab(form, "General");
                    page.SelectDevExpressComboByValue(form, "devCbxShiftOT", "กะงานล่วงเวลา");
                    page.SelectDevExpressComboByValue(form, "devCbxShiftOTType", "กะ OT ก่อนเวลางาน");
                    page.SetInputTextSafely(form, "devTxtShiftCode", "OT-EVN03");
                    page.SelectInOutTypeByValue(form, "แยกจากกะปกติ");
                    page.SetInputTextSafely(form, "devTxtTimeShiftName", "กะ OT ก่อนเวลางาน");

                    page.SetInputTextSafely(form, "devTxtStarttime", "03:00");
                    page.SetInputTextSafely(form, "devTxtEndTime", "07:00");

                    page.SelectDevExpressComboByValue(form, "devCbxTime", "2");
                    page.SetInputTextSafely(form, "devSpinGap", "5");
                    page.SetInputTextSafely(form, "devTxtTimeIn1", "04:00");
                    page.SetInputTextSafely(form, "devTxtTimeOut1", "06:00");

                    page.SelectTab(form, "Details");
                    page.SelectLookupItemByCode(form, "devBtnDeductCode3", "OT-001");
                },
                "SaveAndClose"
            ),

            // รายการที่ 7: กะงานล่วงเวลา -> OT-EVN04 -> Save and Close
            new(
                "OT-EVN04", 30, "Adding Shift (7/7): OT-EVN04...",
                (page, form) =>
                {
                    page.SelectTab(form, "General");
                    page.SelectDevExpressComboByValue(form, "devCbxShiftOT", "กะงานล่วงเวลา");
                    page.SelectDevExpressComboByValue(form, "devCbxShiftOTType", "กะ OT ระหว่างเวลางาน");
                    page.SetInputTextSafely(form, "devTxtShiftCode", "OT-EVN04");
                    page.SelectInOutTypeByValue(form, "รวมในกะปกติ");
                    page.SetInputTextSafely(form, "devTxtTimeShiftName", "กะ OT ระหว่างเวลางาน");

                    page.SetInputTextSafely(form, "devTxtStarttime", "11:00");
                    page.SetInputTextSafely(form, "devTxtEndTime", "14:00");

                    page.SelectDevExpressComboByValue(form, "devCbxTime", "2");
                    page.SetInputTextSafely(form, "devSpinGap", "5");
                    page.SetInputTextSafely(form, "devTxtTimeIn1", "12:00");
                    page.SetInputTextSafely(form, "devTxtTimeOut1", "13:00");

                    page.SelectTab(form, "Details");
                    page.SelectLookupItemByCode(form, "devBtnDeductCode3", "OT-001");
                },
                "SaveAndClose"
            )
        };

        // [ขั้นที่ 3] ข้อมูลทดสอบการแก้ไขข้อมูล (Edit Records)
        private static readonly ShiftEditItem[] Step3_EditCases =
        {
            new(
                "TestCase03_EditData_Record1_NRM-M01", "NRM-M01",
                (page, form) =>
                {
                    page.SelectTab(form, "General");
                    page.SetInputTextSafely(form, "devTxtShiftCode", "NRM-OFF-NEW");
                    page.SetInputTextSafely(form, "devTxtTimeShiftName", "กะสำนักงานใหญ่");
                    page.SetInputTextSafely(form, "devTxtStarttime", "06:00");
                    page.SetInputTextSafely(form, "devTxtEndTime", "20:00");

                    page.SelectDevExpressComboByValue(form, "devCbxTime", "4");
                    page.SetInputTextSafely(form, "devTxtTimeOut1", "12:00");
                    page.SetInputTextSafely(form, "devTxtTimeIn2", "13:00");
                    page.SetInputTextSafely(form, "devTxtTimeOut2", "17:00");
                },
                "SaveAndManualClose"
            ),
            new(
                "TestCase03_EditData_Record2_OT-EVN01", "OT-EVN01",
                (page, form) =>
                {
                    page.SelectTab(form, "General");
                    page.SelectInOutTypeByValue(form, "รวมในกะปกติ");
                    page.SetInputTextSafely(form, "devTxtTimeShiftName", "กะ OT รอบดึก");

                    // วิธีคำนวณเดิม = ตามค่าคงที่ (ไม่เปลี่ยน) แก้ชั่วโมง 06:00 -> 03:00 และเปลี่ยนรหัสรายได้ OT เป็น OT-002
                    page.SelectTab(form, "Details");
                    page.FillDetailsTabForFixedHours(form, "03:00", "OT-002", true);
                },
                "SaveAndClose"
            )
        };

        // [ขั้นที่ 5] ข้อมูลทดสอบการลบข้อมูล (Delete Records)
        private static readonly DeleteItem[] Step5_DeleteCases =
        {
            new("TestCase05_DeleteData_DeleteYes", "NRM-M03", "Yes", true,
                "ลบข้อมูล 'NRM-M03' (Delete -> Yes) สำเร็จ",
                "ข้อมูล 'NRM-M03' ยังคงปรากฏอยู่ในตารางหลังจากลบ"),

            new("TestCase05_DeleteData_DeleteNo", "OT-EVN02", "No", false,
                "ยกเลิกการลบข้อมูล 'OT-EVN02' (Delete -> No) ข้อมูลยังคงอยู่สำเร็จ",
                "ข้อมูล 'OT-EVN02' หายไปจากตารางแม้จะกด No")
        };


        // =========================================================================================
        // หมวดที่ 2: ส่วนที่ขึ้นกับหน้าจอนั้นๆ (Screen-Specific Implementation)
        // =========================================================================================

        private const string LogPageKey = "TSInfoUC";
        private const string MenuSearchKeyword = "บันทึกข้อมูลกะงาน";

        public void NavigateToTSInfoUC()
        {
            const string stageName = "TestCase01_AddNewShift";
            TestProgressHelper.Report(5, $"กำลังค้นหาและเปิดหน้าจอ '{MenuSearchKeyword}' จากช่องค้นหา...");

            if (!TryOpenShiftMenuBySearch(stageName, MenuSearchKeyword)) return;

            TestLogger.LogInfo(LogPageKey, "เริ่มทดสอบบันทึกข้อมูลกะงาน (7 รายการ)");

            try
            {
                foreach (var item in Step1_AddCases)
                {
                    TestProgressHelper.Report(item.Progress, item.ProgressMsg);
                    var form = GetOrOpenShiftForm();

                    item.FillFormAction(this, form);

                    if (item.SaveMode == "SaveAndClose")
                    {
                        ClickSaveAndClose(form);
                        LogAddResultFromGrid($"{stageName}_{item.Code}", item.Code);
                    }
                    else if (item.SaveMode == "SaveAndNew")
                    {
                        ClickButtonSafely(form, "Save and New");
                        WaitUntil(() =>
                        {
                            var codeInput = form.FindFirstDescendant(cf => cf.ByAutomationId("devTxtShiftCode"))?.AsTextBox();
                            return codeInput != null && string.IsNullOrEmpty(codeInput.Text);
                        }, 4000);
                        LogSuccessCounted($"{stageName}_{item.Code}", $"'{item.Code}' บันทึกสำเร็จ (Save and New)");
                    }
                }
            }
            catch (Exception ex)
            {
                LogFailureCounted(stageName, $"เพิ่มข้อมูลกะงานล้มเหลว: {ex.Message}");
            }
        }

        public void RunAllValidationTests()
        {
            const string stageName = "TestCase02_NewValidation";

            try
            {
                var form = GetOrOpenShiftForm();
                SelectTab(form, "General");

                // 2.1 ไม่กรอกรหัสกะงาน
                SelectDevExpressComboByValue(form, "devCbxShiftOT", "กะงานปกติ");
                ClearField(form, "devTxtShiftCode");
                SetInputTextSafely(form, "devTxtTimeShiftName", "กะออฟฟิศมาตรฐาน");
                ClearFields(form, "devTxtStarttime", "devTxtEndTime", "devTxtTimeIn1", "devTxtTimeOut1");

                ExpectAlert(form, "Save", "devBtnSave", "รหัสกะงาน ไม่สามารถเป็นค่าว่าง",
                    $"{stageName}_EmptyCode", "ตรวจสอบแจ้งเตือน 'รหัสกะงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล' สำเร็จ");

                // 2.2 ไม่กรอกชื่อกะงาน
                SetInputTextSafely(form, "devTxtShiftCode", "NRM-M01");
                ClearField(form, "devTxtTimeShiftName");

                ExpectAlert(form, "Save and New", null, "ชื่อกะงาน ไม่สามารถเป็นค่าว่าง",
                    $"{stageName}_EmptyName", "ตรวจสอบแจ้งเตือน 'ชื่อกะงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล' สำเร็จ");

                // 2.3 ไม่กรอกรอบเวลาเริ่มต้นและสิ้นสุด
                SetInputTextSafely(form, "devTxtShiftCode", "NRM-M01");
                SetInputTextSafely(form, "devTxtTimeShiftName", "กะออฟฟิศมาตรฐาน");
                ClearFields(form, "devTxtStarttime", "devTxtEndTime");

                ExpectAlert(form, "Save", "devBtnSave", "รอบเวลาเริ่มต้น ไม่สามารถเป็นค่าว่าง",
                    $"{stageName}_EmptyStartEndTime", "ตรวจสอบแจ้งเตือน 'รอบเวลาเริ่มต้น/สิ้นสุด ไม่สามารถเป็นค่าว่าง' สำเร็จ");

                // 2.4 กรอกรอบเวลา แต่ไม่กรอกเวลาเข้า/ออก(1)
                SelectDevExpressComboByValue(form, "devCbxTime", "2");
                SetInputTextSafely(form, "devTxtStarttime", "06:30");
                SetInputTextSafely(form, "devTxtEndTime", "19:00");
                ClearFields(form, "devTxtTimeIn1", "devTxtTimeOut1");

                ExpectAlert(form, "Save and New", null, "เวลาเข้า(1) ไม่สามารถเป็นค่าว่าง",
                    $"{stageName}_EmptyTimeInOut1", "ตรวจสอบแจ้งเตือน 'เวลาเข้า(1)/เวลาออก(1) ไม่สามารถเป็นค่าว่าง' สำเร็จ");

                // 2.5 กรอกครบแต่รหัสซ้ำ NRM-M01
                SetInputTextSafely(form, "devTxtTimeIn1", "08:00");
                SetInputTextSafely(form, "devTxtTimeOut1", "17:00");
                ClearFields(form, "devTxtStartBreakTime1", "devTxtEndBreakTime1");

                ClickSaveAndClose(form);
                HandleMessageBoxOk("รหัสกะงาน ห้ามซ้ำ");
                LogSuccessCounted($"{stageName}_DuplicateCode", "ตรวจสอบแจ้งเตือน 'รหัสกะงาน ห้ามซ้ำ กรุณาป้อนข้อมูล' สำเร็จ");

                // 2.6 ปิดหน้าจอ
                RunCloseDialogSequence(form, stageName, "รหัสกะงาน  ห้ามซ้ำ");
            }
            catch (Exception ex)
            {
                LogFailureCounted(stageName, $"ทดสอบแจ้งเตือน New ล้มเหลว: {ex.Message}");
            }
        }

        public void RunEditTests()
        {
            foreach (var item in Step3_EditCases)
            {
                try
                {
                    SelectRowByCodeInMainGrid(item.TargetCode);
                    var form = OpenEditForm();

                    item.EditAction(this, form);

                    if (item.SaveMode == "SaveAndManualClose")
                    {
                        ClickButtonSafely(form, "Save", "devBtnSave");
                        ClickFormClose(form);
                        WaitForClosed(() => FindOpenShiftForm(), 5000);
                        RefreshGrid();
                        LogSuccessCounted(item.TestCaseId, "แก้ไข NRM-M01 เป็น NRM-OFF-NEW (Save -> Close) สำเร็จ");
                    }
                    else if (item.SaveMode == "SaveAndClose")
                    {
                        ClickSaveAndCloseEnsureClosed(form);
                        LogSuccessCounted(item.TestCaseId, "แก้ไข OT-EVN01 (Save and Close) สำเร็จ");
                    }
                }
                catch (Exception ex)
                {
                    LogFailureCounted(item.TestCaseId, $"ทดสอบ Edit ล้มเหลว: {ex.Message}");
                    TryCloseOpenForm(); // ปิดฟอร์มที่ค้าง เพื่อให้เคสถัดไปทำงานต่อได้
                }
            }
        }

        public void RunEditValidationTests()
        {
            const string stageName = "TestCase04_EditValidation";

            try
            {
                SelectRowInMainGrid(0);
                var form = OpenEditForm();
                SelectTab(form, "General");

                // 4.1 ลบรหัสกะงานเดิมออก -> กด Save
                ClearField(form, "devTxtShiftCode");
                SetInputTextSafely(form, "devTxtTimeShiftName", "กะออฟฟิศมาตรฐาน");
                ClearFields(form, "devTxtStarttime", "devTxtEndTime", "devTxtTimeIn1", "devTxtTimeOut1");

                ExpectAlert(form, "Save", "devBtnSave", "รหัสกะงาน ไม่สามารถเป็นค่าว่าง",
                    $"{stageName}_EmptyCodeOnEdit", "ตรวจสอบแจ้งเตือน 'รหัสกะงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล' (Edit) สำเร็จ");

                // 4.2 กรอก NRM-M04 ลบชื่อกะงานเดิมออก -> กด Save and New
                SetInputTextSafely(form, "devTxtShiftCode", "NRM-M04");
                ClearField(form, "devTxtTimeShiftName");

                ExpectAlert(form, "Save and New", null, "ชื่อกะงาน ไม่สามารถเป็นค่าว่าง",
                    $"{stageName}_EmptyNameOnEdit", "ตรวจสอบแจ้งเตือน 'ชื่อกะงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล' (Edit) สำเร็จ");

                // 4.3 กรอก NRM-M04 และ กะออฟฟิศ ไม่กรอกรอบเวลา -> กด Save and New
                SetInputTextSafely(form, "devTxtTimeShiftName", "กะออฟฟิศ");
                ClearFields(form, "devTxtStarttime", "devTxtEndTime");

                ExpectAlert(form, "Save and New", null, "รอบเวลาเริ่มต้น ไม่สามารถเป็นค่าว่าง",
                    $"{stageName}_EmptyStartEndTimeOnEdit", "ตรวจสอบแจ้งเตือน 'รอบเวลาเริ่มต้น/สิ้นสุด ไม่สามารถเป็นค่าว่าง' (Edit) สำเร็จ");

                // 4.4 กรอกรอบเวลา ไม่กรอกเวลาเข้า/ออก(1) -> กด Save and New
                SelectDevExpressComboByValue(form, "devCbxTime", "2");
                SetInputTextSafely(form, "devTxtStarttime", "06:30");
                SetInputTextSafely(form, "devTxtEndTime", "19:00");
                ClearFields(form, "devTxtTimeIn1", "devTxtTimeOut1");

                ExpectAlert(form, "Save and New", null, "เวลาเข้า(1) ไม่สามารถเป็นค่าว่าง",
                    $"{stageName}_EmptyTimeInOut1OnEdit", "ตรวจสอบแจ้งเตือน 'เวลาเข้า(1)/เวลาออก(1) ไม่สามารถเป็นค่าว่าง' (Edit) สำเร็จ");

                // 4.5 กรอกรหัสซ้ำ NRM-M01 -> กด Save and New
                SetInputTextSafely(form, "devTxtShiftCode", "NRM-M01");
                SetInputTextSafely(form, "devTxtTimeShiftName", "กะออฟฟิศมาตรฐาน");
                SetInputTextSafely(form, "devTxtTimeIn1", "08:00");
                SetInputTextSafely(form, "devTxtTimeOut1", "17:00");
                ClearFields(form, "devTxtStartBreakTime1", "devTxtEndBreakTime1");

                ExpectAlert(form, "Save and New", null, "รหัสกะงาน ห้ามซ้ำ",
                    $"{stageName}_DuplicateCodeOnEdit", "ตรวจสอบแจ้งเตือน 'รหัสกะงาน ห้ามซ้ำ กรุณาป้อนข้อมูล' (Edit) สำเร็จ");

                // 4.6 ลำดับการทดสอบปิดหน้าจอ (Close -> Yes, Cancel, No)
                RunCloseDialogSequence(form, stageName, "รหัสกะงาน  ห้ามซ้ำ");
            }
            catch (Exception ex)
            {
                LogFailureCounted(stageName, $"ทดสอบแจ้งเตือน Edit ล้มเหลว: {ex.Message}");
            }
        }

        public void RunDeleteTests()
        {
            const string stageName = "TestCase05_DeleteData";

            try
            {
                foreach (var item in Step5_DeleteCases)
                {
                    SelectRowByCodeInMainGrid(item.TargetCode);
                    ClickToolbarButtonByName("Delete");
                    HandleMessageBoxConfirm(item.DialogBtn);
                    RefreshGrid();

                    bool exists = VerifyRecordExistsInGrid(item.TargetCode, 3000, false);
                    if (item.ExpectDeleted && !exists)
                    {
                        LogSuccessCounted(item.TestCaseId, item.SuccessMsg);
                    }
                    else if (!item.ExpectDeleted && exists)
                    {
                        LogSuccessCounted(item.TestCaseId, item.SuccessMsg);
                    }
                    else
                    {
                        LogFailureCounted(item.TestCaseId, item.FailureMsg);
                    }
                }
            }
            catch (Exception ex)
            {
                LogFailureCounted(stageName, $"ทดสอบ Delete ล้มเหลว: {ex.Message}");
            }

            TestLogger.LogInfo(LogPageKey, $"สรุปผลการทดสอบทั้งหมด: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");
        }

        // --- Custom Mapping & Control Logic ประจำหน้าจอ TSInfoUC ---
        private bool TryOpenShiftMenuBySearch(string stage, string searchKeyword)
        {
            try
            {
                var searchBox = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("baritemSearch"))
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)), 4000)
                              ?? throw new Exception("ไม่พบช่องค้นหา baritemSearch");

                WaitUntil(() => { try { searchBox.Focus(); return true; } catch { return false; } }, 1000);
                searchBox.Click();

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
                LogFailureCounted(stage, $"ค้นหาและเปิดหน้าจอไม่สำเร็จ: {ex.Message}");
                return false;
            }
        }

        private void FillDetailsTabForFixedHours(Window form, string hours, string wageCode, bool verify = false)
        {
            // 1) วิธีคำนวณ = ตามค่าคงที่  2) ชั่วโมง  3) รหัสรายได้ OT
            SelectCalcMethod(form, "ตามค่าคงที่");
            FillHoursField(form, hours);
            SelectLookupItemByCode(form, "devBtnDeductCode3", wageCode);

            // ตรวจซ้ำ: หลังเลือกรหัสรายได้ วิธีคำนวณ/ชั่วโมงอาจเปลี่ยน ให้ตั้งค่ากลับ
            string method = SelectCalcMethod(form, "ตามค่าคงที่");
            string hoursText = FillHoursField(form, hours);
            string codeText = ReadLookupText(form, "devBtnDeductCode3");

            if (!verify) return;

            var problems = new List<string>();
            if (!string.IsNullOrEmpty(method) && !method.Contains("ค่าคงที่"))
                problems.Add($"วิธีคำนวณ='{method}'");
            if (!string.IsNullOrEmpty(hoursText) && DigitsOnly(hoursText) != DigitsOnly(hours))
                problems.Add($"ชั่วโมง='{hoursText}' (ต้องการ {hours})");
            if (!string.IsNullOrEmpty(codeText) && !NormCode(codeText).Contains(NormCode(wageCode)))
                problems.Add($"รหัสรายได้ OT='{codeText}' (ต้องการ {wageCode})");

            if (problems.Count > 0)
                throw new Exception("ค่าบนหน้าจอ Details ไม่ตรง: " + string.Join(", ", problems));
        }

        /// <summary>ตัดช่องว่างและขีด เพื่อเทียบรหัส เช่น 'OT-002' กับ 'OT002'</summary>
        private static string NormCode(string? s) =>
            new string((s ?? "").Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray()).ToUpperInvariant();

        /// <summary>อ่านค่าที่แสดงในช่อง lookup (ปุ่ม ...) เช่น รหัสรายได้ OT</summary>
        private string ReadLookupText(Window form, string automationId)
        {
            try
            {
                var c = form.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
                if (c == null) return "";
                var t = ReadComboText(c);
                return string.Equals(t, automationId, StringComparison.OrdinalIgnoreCase) ? "" : t;
            }
            catch { return ""; }
        }

        /// <summary>
        /// กรอกช่องชั่วโมง (devtxtHours) ในโหมด "ตามค่าคงที่" แล้วคืนค่าที่อ่านได้จากช่อง
        /// ลำดับ: พิมพ์ปกติ -> ValuePattern -> พิมพ์เฉพาะตัวเลข (เผื่อ mask ใส่ ':' ให้เอง)
        /// </summary>
        private string FillHoursField(Window form, string hours)
        {
            var field = Retry(() => FindByIdIgnoreCase(form, "devtxtHours"), 3000);
            if (field == null)
            {
                Debug.WriteLine("ไม่พบช่องชั่วโมง (devtxtHours)");
                return "";
            }

            WaitUntil(() => field.Properties.IsEnabled.ValueOrDefault, 3000);

            string want = DigitsOnly(hours);
            bool Ok() => DigitsOnly(ReadComboText(field)) == want;
            if (Ok()) return ReadComboText(field); // ค่าถูกอยู่แล้ว

            ReplaceFieldText(field, hours);

            if (!Ok())
            {
                try
                {
                    field.Focus();
                    if (field.Patterns.Value.IsSupported)
                        field.Patterns.Value.Pattern.SetValue(hours);
                    Keyboard.Press(VirtualKeyShort.TAB);
                    Thread.Sleep(250);
                }
                catch { }
            }

            if (!Ok())
            {
                try
                {
                    ClickWithMouse(field);
                    Thread.Sleep(150);
                    Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                    Keyboard.Press(VirtualKeyShort.BACK);
                    Keyboard.Type(hours.Replace(":", ""));
                    Keyboard.Press(VirtualKeyShort.TAB);
                    Thread.Sleep(250);
                }
                catch { }
            }

            return ReadComboText(field);
        }

        /// <summary>
        /// เลือก "วิธีคำนวณ" ใน Details tab: "ตามค่าคงที่" (รายการแรก) หรือ "ตามข้อกำหนด OT" (รายการที่สอง)
        /// คืนค่าข้อความที่แสดงอยู่หลังเลือก (ว่างถ้าอ่านไม่ได้)
        /// </summary>
        private string SelectCalcMethod(Window form, string methodText)
        {
            int idx = methodText.Contains("ค่าคงที่") ? 0 : 1;
            string key = idx == 0 ? "ค่าคงที่" : "ข้อกำหนด";

            var lbl = form.FindFirstDescendant(cf => cf.ByName("วิธีคำนวณ"))
                   ?? form.FindAllDescendants().FirstOrDefault(e => !string.IsNullOrEmpty(e.Name) && e.Name.Trim() == "วิธีคำนวณ")
                   ?? form.FindAllDescendants().FirstOrDefault(e => !string.IsNullOrEmpty(e.Name) && e.Name.Contains("วิธีคำนวณ"));
            if (lbl == null) return "";

            var lr = lbl.BoundingRectangle;
            bool SameRow(AutomationElement e) => Math.Abs(e.BoundingRectangle.Y - lr.Y) < 20 && e.BoundingRectangle.X >= lr.Right - 5;

            // combo และปุ่มลูกศรที่อยู่แถวเดียวกับ label
            var combo = form.FindAllDescendants(cf => cf.ByControlType(ControlType.ComboBox))
                            .Where(c => c.BoundingRectangle.Width > 0 && SameRow(c) && !c.Properties.IsOffscreen.ValueOrDefault)
                            .OrderBy(c => c.BoundingRectangle.X)
                            .FirstOrDefault();

            var btnOpen = combo?.FindFirstDescendant(cf => cf.ByName("Open").Or(cf.ByControlType(ControlType.Button)))
                       ?? form.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                              .Where(b => b.Name == "Open" && SameRow(b))
                              .OrderBy(b => b.BoundingRectangle.X)
                              .FirstOrDefault();
            if (btnOpen == null) return "";

            // ตัวที่ใช้อ่านค่า: combo ถ้ามี ไม่งั้น Edit ที่อยู่ซ้ายของปุ่ม Open
            var reader = combo
                      ?? form.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit))
                             .Where(e => e.BoundingRectangle.Width > 0 && SameRow(e) &&
                                         e.BoundingRectangle.Right <= btnOpen.BoundingRectangle.Right + 2 &&
                                         e.BoundingRectangle.X < btnOpen.BoundingRectangle.X)
                             .OrderBy(e => e.BoundingRectangle.X)
                             .FirstOrDefault();

            string Current() => reader != null ? ReadComboText(reader) : "";
            bool IsSelected() => Current().Contains(key);
            bool Readable() => Current().Contains("ตาม"); // ทุกรายการขึ้นต้นด้วย 'ตาม'

            if (IsSelected()) return Current();

            PickFromPopup(
                openDropdown: () => { ClickWithMouse(btnOpen); Thread.Sleep(300); },
                isTargetItem: e =>
                {
                    if (string.IsNullOrEmpty(e.Name) || !e.Name.Contains(key)) return false;
                    var r = e.BoundingRectangle;
                    return r.Width > 0 && r.Height > 0 && r.Top >= lr.Y - 5;
                },
                idx: idx,
                isReadable: Readable,
                isSelected: IsSelected);

            Thread.Sleep(300); // รอให้ช่องที่เกี่ยวข้อง (ชั่วโมง/ขั้นต่ำ) เปลี่ยนสถานะ enabled
            return Current();
        }

        private void SelectLookupItemByCode(Window form, string lookupButtonId, string targetCode)
        {
            var lookupControl = Retry(() =>
                form.FindFirstDescendant(cf => cf.ByAutomationId(lookupButtonId))
                ?? form.FindFirstDescendant(cf => cf.ByName(lookupButtonId)), 3000)
                ?? throw new Exception($"หาคอนโทรล Lookup '{lookupButtonId}' ไม่เจอ");

            var buttons = lookupControl.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
            var btnDots = buttons.Length > 0 ? buttons[0] : null;

            if (btnDots != null)
            {
                ClickWithMouse(btnDots);
            }
            else
            {
                ClickWithMouse(lookupControl);
                Keyboard.Press(VirtualKeyShort.F4);
            }

            Window? lookupWindow = FindAnyLookupModal(form);
            if (lookupWindow == null)
            {
                ClickWithMouse(lookupControl);
                Keyboard.Press(VirtualKeyShort.F4);
                lookupWindow = FindAnyLookupModal(form);
            }

            if (lookupWindow == null)
            {
                throw new Exception($"ไม่พบหน้าต่าง Lookup Modal สำหรับ '{lookupButtonId}'");
            }

            var targetCell = Retry(() =>
            {
                return lookupWindow.FindAllDescendants().FirstOrDefault(el =>
                {
                    if (el == null) return false;
                    if (!string.IsNullOrEmpty(el.Name) && NormCode(el.Name) == NormCode(targetCode)) return true;

                    if (el.Patterns.Value.IsSupported)
                    {
                        string? val = el.Patterns.Value.Pattern.Value.ValueOrDefault;
                        if (!string.IsNullOrEmpty(val) && NormCode(val) == NormCode(targetCode)) return true;
                    }
                    return false;
                });
            }, 3000);

            targetCell ??= lookupWindow.FindAllDescendants().FirstOrDefault(el => !string.IsNullOrEmpty(el?.Name) && NormCode(el.Name).Contains(NormCode(targetCode)));

            if (targetCell != null)
            {
                ClickWithMouse(targetCell);
            }
            else
            {
                var grid = lookupWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataGrid))
                       ?? lookupWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Table));
                if (grid != null) ClickWithMouse(grid);
            }

            var btnSelect = lookupWindow.FindFirstDescendant(cf => cf.ByName("Select"))
                          ?? lookupWindow.FindFirstDescendant(cf => cf.ByName("&Select"))
                          ?? lookupWindow.FindFirstDescendant(cf => cf.ByName("OK"))
                          ?? lookupWindow.FindFirstDescendant(cf => cf.ByName("เลือก"));

            if (btnSelect != null) ClickWithMouse(btnSelect);
            else Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.KEY_S);

            WaitForClosed(() => lookupWindow, 3000);
        }

        private Window? FindAnyLookupModal(Window form)
        {
            return Retry(() =>
                form.ModalWindows.FirstOrDefault()
                ?? _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault()
                ?? _automation.GetDesktop().FindAllChildren()
                  .FirstOrDefault(e => e.ControlType == ControlType.Window &&
                                       !e.Name.Contains("Prosoft HRMI") &&
                                       !e.Name.Contains("Accessibility") &&
                                       (e.Name.Contains("กำหนดรายได้") || e.Name.Contains("รายหัก") || e.Name.Contains("Lookup")))?.AsWindow(),
                4000);
        }

        private void SelectTab(Window? form, string tabName)
        {
            ArgumentNullException.ThrowIfNull(form);

            bool Matches(string? name)
            {
                if (string.IsNullOrEmpty(name)) return false;
                name = name.Trim();
                return name.Equals(tabName, StringComparison.OrdinalIgnoreCase) ||
                       (tabName.Contains("OT") && name.Contains("OT")) ||
                       (tabName.Equals("General", StringComparison.OrdinalIgnoreCase) && name.Contains("ทั่วไป")) ||
                       (tabName.Equals("Details", StringComparison.OrdinalIgnoreCase) && name.Contains("รายละเอียด"));
            }

            var targetTab = Retry(() =>
                form.FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem)).FirstOrDefault(t => Matches(t.Name))
                ?? form.FindAllDescendants().FirstOrDefault(e =>
                       Matches(e.Name) &&
                       e.BoundingRectangle.Y < form.BoundingRectangle.Y + 220 &&
                       e.BoundingRectangle.Width > 20), 3000);

            if (targetTab == null) throw new Exception($"หา Tab ด้านบน '{tabName}' ไม่เจอ");
            ClickWithMouse(targetTab);
        }

        private void SelectInOutTypeByValue(Window form, string targetText)
        {
            var cbx = form.FindFirstDescendant(cf => cf.ByAutomationId("devCbxInOutType"))
                   ?? form.FindAllDescendants(cf => cf.ByControlType(ControlType.ComboBox)).FirstOrDefault(c =>
                   {
                       var parent = c.Parent;
                       return c.Name.Contains("วิธีการลงเวลา") || (parent != null && parent.Name.Contains("วิธีการลงเวลา"));
                   });
            if (cbx == null) return;

            OpenComboDropdown(cbx);

            // 'แยกจากกะปกติ' = รายการถัดไป (DOWN), 'รวมในกะปกติ' = รายการก่อนหน้า (UP)
            var move = targetText.Equals("แยกจากกะปกติ", StringComparison.OrdinalIgnoreCase)
                ? VirtualKeyShort.DOWN
                : VirtualKeyShort.UP;

            WaitUntil(() =>
            {
                try
                {
                    Keyboard.Press(move);
                    Keyboard.Press(VirtualKeyShort.ENTER);
                    Keyboard.Press(VirtualKeyShort.ESCAPE);
                    return true;
                }
                catch { return false; }
            }, 1000);
        }

        private void SelectDevExpressComboByValue(AutomationElement context, string automationId, string itemText)
        {
            var cbx = Retry(() => context.FindFirstDescendant(cf => cf.ByAutomationId(automationId)))
                   ?? Retry(() => context.FindAllDescendants(cf => cf.ByControlType(ControlType.ComboBox))
                                          .FirstOrDefault(c => c.Name.Contains(automationId) || c.AutomationId.Contains(automationId)));
            if (cbx == null) return;

            FocusSafely(cbx);

            // ช่อง "ลงเวลาทั้งหมด" (2 / 4 / 6 ครั้ง)
            if (automationId == "devCbxTime")
            {
                string timeVal = itemText.Contains("6") ? "6" : (itemText.Contains("4") ? "4" : "2");
                SelectTimeCountCombo(cbx, timeVal);
                return;
            }

            OpenComboDropdown(cbx);

            if (automationId == "devCbxShiftOT")
            {
                // item ใน popup ชื่อ 'Caption' (UIA หาด้วยชื่อไม่ได้) -> ใช้คีย์บอร์ด
                // UP x2 = ชนบนสุด (กะงานปกติ) ถ้าต้องการ กะงานล่วงเวลา ให้ DOWN อีก 1 ครั้ง
                Keyboard.Press(VirtualKeyShort.UP);
                Thread.Sleep(80);
                Keyboard.Press(VirtualKeyShort.UP);
                Thread.Sleep(80);
                if (itemText.Contains("กะงานล่วงเวลา"))
                {
                    Keyboard.Press(VirtualKeyShort.DOWN);
                    Thread.Sleep(120);
                }
                Keyboard.Press(VirtualKeyShort.ENTER);
                Thread.Sleep(150);
                Keyboard.Press(VirtualKeyShort.TAB);
            }
            else
            {
                Keyboard.Type(itemText);
                Keyboard.Press(VirtualKeyShort.ENTER);
                Keyboard.Press(VirtualKeyShort.TAB);
            }

            Thread.Sleep(300); // รอให้หน้าจออัปเดต
        }

        /// <summary>อ่านค่าข้อความที่แสดงอยู่ใน ComboBox (ลองหลายวิธี)</summary>
        private static string ReadComboText(AutomationElement cbx)
        {
            try
            {
                if (cbx.Patterns.Value.IsSupported)
                {
                    var v = cbx.Patterns.Value.Pattern.Value.ValueOrDefault;
                    if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
                }
            }
            catch { }

            try
            {
                var edit = cbx.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit));
                if (edit != null && edit.Patterns.Value.IsSupported)
                {
                    var v = edit.Patterns.Value.Pattern.Value.ValueOrDefault;
                    if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
                }
            }
            catch { }

            return cbx.Name ?? "";
        }

        /// <summary>เปิด Dropdown ของ DevExpress ComboBox ด้วยปุ่มลูกศร</summary>
        private void OpenComboDropdown(AutomationElement cbx)
        {
            var btnOpen = cbx.FindFirstDescendant(cf => cf.ByName("Open").Or(cf.ByControlType(ControlType.Button)));
            if (btnOpen != null)
            {
                ClickWithMouse(btnOpen);
            }
            else
            {
                var b = cbx.BoundingRectangle;
                Point arrowPt = new Point((int)(b.Right - 8), (int)(b.Y + (b.Height / 2)));
                Mouse.MoveTo(arrowPt);
                Mouse.Click(arrowPt);
            }
            Thread.Sleep(300);
        }

        /// <summary>
        /// เปิด popup แล้วเลือกรายการ
        /// วิธีที่ 1: คลิก list item ที่ isTargetItem ยอมรับ
        /// วิธีที่ 2 (สำรอง): ปิดแล้วเปิดใหม่ UP x3 (ชนบนสุด) + DOWN ตาม idx + ENTER
        ///   ใช้เมื่อหา item ไม่เจอ หรือ อ่านค่าได้แต่ยังไม่ตรง
        /// </summary>
        private void PickFromPopup(Action openDropdown, Func<AutomationElement, bool> isTargetItem, int idx,
                                   Func<bool> isReadable, Func<bool> isSelected)
        {
            openDropdown();

            var item = Retry(() =>
                _automation.GetDesktop()
                    .FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem))
                    .FirstOrDefault(isTargetItem), 1500);

            if (item != null)
            {
                ClickWithMouse(item);
                Thread.Sleep(250);
            }

            if (item == null || (isReadable() && !isSelected()))
            {
                Keyboard.Press(VirtualKeyShort.ESCAPE);
                Thread.Sleep(150);
                openDropdown();

                for (int i = 0; i < 3; i++) { Keyboard.Press(VirtualKeyShort.UP); Thread.Sleep(80); }
                for (int i = 0; i < idx; i++) { Keyboard.Press(VirtualKeyShort.DOWN); Thread.Sleep(120); }
                Keyboard.Press(VirtualKeyShort.ENTER);
                Thread.Sleep(250);
            }
        }

        /// <summary>เลือกค่า "ลงเวลาทั้งหมด" (2 / 4 / 6) จาก popup ของ combo</summary>
        private void SelectTimeCountCombo(AutomationElement cbx, string targetVal)
        {
            int idx = targetVal == "2" ? 0 : (targetVal == "4" ? 1 : 2);
            var cb = cbx.BoundingRectangle;

            PickFromPopup(
                openDropdown: () => OpenComboDropdown(cbx),
                isTargetItem: e =>
                {
                    if (e.Name != targetVal && e.Name != $"{targetVal} ครั้ง") return false;
                    var r = e.BoundingRectangle;
                    return r.Width > 0 && r.Height > 0 &&
                           r.Top >= cb.Bottom - 5 &&                 // อยู่ใต้กล่อง combo
                           r.Left < cb.Right && r.Right > cb.Left;   // ตรงแนวนอนกับ combo
                },
                idx: idx,
                isReadable: () => ReadComboText(cbx).Any(char.IsDigit),
                isSelected: () => ReadComboText(cbx).Trim().StartsWith(targetVal));

            // TAB เพื่อให้ฟอร์มโหลดช่วงเวลา (1), (2), (3)
            Keyboard.Press(VirtualKeyShort.TAB);
            Thread.Sleep(300);
        }

        private void ChangeEndTimeToNextDay(Window form) => ClickNextDayButton(form, "devTxtEndTime", 105);

        private void ClickNextDayButtonBesideField(Window form, string timeFieldId) => ClickNextDayButton(form, timeFieldId, 95);

        /// <summary>กดปุ่มวัน (วันเดียวกัน/วันถัดไป) ข้างช่องเวลา แล้วเลือก 'วันถัดไป' ด้วยคีย์บอร์ด</summary>
        private void ClickNextDayButton(Window form, string timeFieldId, int fallbackOffsetX)
        {
            var timeField = Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(timeFieldId)));
            if (timeField == null) return;

            var timeBounds = timeField.BoundingRectangle;
            var btnOpen = form.FindAllDescendants(cf => cf.ByControlType(ControlType.Button)).FirstOrDefault(b =>
            {
                var r = b.BoundingRectangle;
                return r.X >= timeBounds.Right - 5 &&
                       r.X <= timeBounds.Right + 260 &&
                       Math.Abs(r.Y - timeBounds.Y) < 18;
            });

            Point clickPt = btnOpen != null
                ? new Point((int)(btnOpen.BoundingRectangle.X + (btnOpen.BoundingRectangle.Width / 2)), (int)(btnOpen.BoundingRectangle.Y + (btnOpen.BoundingRectangle.Height / 2)))
                : new Point((int)(timeBounds.Right + fallbackOffsetX), (int)(timeBounds.Y + (timeBounds.Height / 2)));

            Mouse.MoveTo(clickPt);
            Mouse.Click(clickPt);

            WaitUntil(() =>
            {
                try
                {
                    Keyboard.Press(VirtualKeyShort.DOWN);
                    Keyboard.Press(VirtualKeyShort.ENTER);
                    Keyboard.Press(VirtualKeyShort.ESCAPE);
                    return true;
                }
                catch { return false; }
            }, 1000);
        }

        private Window GetOrOpenShiftForm()
        {
            var form = FindOpenShiftForm();
            if (form == null)
            {
                ClickToolbarButtonByName("New");
                form = Retry(FindOpenShiftForm, 8000);
            }

            if (form == null) throw new Exception("หาฟอร์มบันทึกข้อมูลกะงานไม่เจอหลังจากกดปุ่ม New");
            return form;
        }

        private Window OpenEditForm()
        {
            ClickToolbarButtonByName("Edit");
            var form = Retry(FindOpenShiftForm, 8000);
            if (form == null) throw new Exception("หาฟอร์มบันทึกข้อมูลกะงาน (Edit) ไม่เจอ");
            return form;
        }

        private void ClickToolbarButtonByName(string buttonName)
        {
            var pageScope = _mainWindow.FindFirstDescendant(cf => cf.ByName("บันทึกข้อมูลกะงาน").Or(cf.ByAutomationId("TSInfoUC")))
                         ?? _mainWindow;

            var btn = Retry(() =>
            {
                var allButtons = pageScope.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
                return allButtons.FirstOrDefault(e =>
                    !string.IsNullOrEmpty(e.Name) &&
                    (e.Name.Trim().Equals(buttonName, StringComparison.OrdinalIgnoreCase) ||
                     e.Name.Trim().Equals($"&{buttonName}", StringComparison.OrdinalIgnoreCase)));
            }, 3000) ?? throw new Exception($"หาปุ่ม '{buttonName}' บนแถบเครื่องมือไม่เจอ");

            ClickWithMouse(btn);
        }

        private Window? FindOpenShiftForm()
        {
            var modal = _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault();
            if (modal != null &&
                modal.Properties.NativeWindowHandle.ValueOrDefault != _mainWindow.Properties.NativeWindowHandle.ValueOrDefault &&
                IsShiftForm(modal))
            {
                return modal;
            }

            return _automation.GetDesktop().FindAllChildren()
                .FirstOrDefault(e => e.ControlType == ControlType.Window &&
                                     e.Properties.NativeWindowHandle.ValueOrDefault != _mainWindow.Properties.NativeWindowHandle.ValueOrDefault &&
                                     !string.IsNullOrEmpty(e.Name) &&
                                     (e.Name.Contains(": New") ||
                                      e.Name.Contains(": Edit") ||
                                      e.Name.StartsWith("บันทึกข้อมูลกะงาน :") ||
                                      (e.Name.Contains("กะงาน") && e.Name != "บันทึกข้อมูลกะงาน")))?.AsWindow();
        }

        private void RunCloseDialogSequence(Window form, string stageName, string expectedKeywordOnYes)
        {
            ClickFormClose(form);
            HandleMessageBoxConfirm("Yes", form);
            HandleMessageBoxOk(expectedKeywordOnYes);
            LogSuccessCounted($"{stageName}_CloseYes", "ทดสอบกด Close -> Yes สำเร็จ");

            ClickFormClose(form);
            HandleMessageBoxConfirm("Cancel", form);
            if (form.IsAvailable)
            {
                LogSuccessCounted($"{stageName}_CloseCancel", "ทดสอบกด Close -> Cancel หน้าต่างยังคงเปิดอยู่สำเร็จ");
            }

            ClickFormClose(form);
            HandleMessageBoxConfirm("No", form);
            WaitForClosed(() => FindOpenShiftForm());
            LogSuccessCounted($"{stageName}_CloseNo", "ทดสอบกด Close -> No ปิดหน้าจอสำเร็จ");
        }

        /// <summary>กดปุ่ม -> ตอบ message box แจ้งเตือน (OK) -> บันทึกผล PASS</summary>
        private void ExpectAlert(Window form, string buttonName, string? buttonId, string expectedKeyword,
                                 string testName, string successMessage)
        {
            ClickButtonSafely(form, buttonName, buttonId);
            HandleMessageBoxOk(expectedKeyword);
            LogSuccessCounted(testName, successMessage);
        }

        private void ClearFields(AutomationElement context, params string[] automationIds)
        {
            foreach (var id in automationIds) ClearField(context, id);
        }

        private void ClickButtonSafely(AutomationElement context, string buttonName, string? automationId = null)
        {
            var btn = context.FindFirstDescendant(cf => cf.ByName(buttonName))
                   ?? context.FindFirstDescendant(cf => cf.ByName($"&{buttonName}"))
                   ?? (automationId != null ? context.FindFirstDescendant(cf => cf.ByAutomationId(automationId)) : null)
                   ?? context.FindFirstDescendant(cf => cf.ByAutomationId(buttonName));

            if (btn == null) throw new Exception($"หาปุ่ม '{buttonName}' ไม่เจอ");
            ClickWithMouse(btn);
        }

        private static AutomationElement? FindSaveAndCloseButton(Window form) =>
            form.FindFirstDescendant(cf => cf.ByName("Save and Close"))
            ?? form.FindFirstDescendant(cf => cf.ByName("Save and &Close"))
            ?? form.FindFirstDescendant(cf => cf.ByName("บันทึกและปิด"))
            ?? form.FindFirstDescendant(cf => cf.ByAutomationId("Save and Close"))
            ?? form.FindFirstDescendant(cf => cf.ByAutomationId("devBtnSaveAndClose"));

        private void ClickSaveAndClose(Window form)
        {
            var btn = FindSaveAndCloseButton(form);

            if (btn != null)
            {
                ClickWithMouse(btn);
            }
            else
            {
                ClickButtonSafely(form, "Save", "devBtnSave");
                ClickFormClose(form);
            }

            var questionBox = Retry(() =>
                form.ModalWindows.FirstOrDefault()
                ?? _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault(), 1500);

            if (questionBox != null)
            {
                var btnYes = questionBox.FindFirstDescendant(cf => cf.ByName("Yes").Or(cf.ByName("&Yes")));
                if (btnYes != null) ClickWithMouse(btnYes);
                else Keyboard.Press(VirtualKeyShort.ENTER);
            }

            WaitForClosed(() => FindOpenShiftForm(), 5000);
            RefreshGrid();
        }

        private static bool IsShiftForm(AutomationElement w)
        {
            try
            {
                if (!string.IsNullOrEmpty(w.Name) && w.Name.Contains("กะงาน")) return true;
                return w.FindFirstDescendant(cf => cf.ByName("Save and Close").Or(cf.ByAutomationId("devBtnSave"))) != null;
            }
            catch { return false; }
        }

        private static bool IsFormClosed(AutomationElement form)
        {
            try { return !form.IsAvailable || form.Properties.IsOffscreen.ValueOrDefault; }
            catch { return true; }
        }

        /// <summary>หา message box ที่เด้งขึ้นมาทับฟอร์ม (ไม่นับตัวฟอร์มเอง)</summary>
        private Window? FindPopupOverForm(Window form)
        {
            try
            {
                var m = form.ModalWindows.FirstOrDefault();
                if (m != null) return m;

                var mm = _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault();
                if (mm != null &&
                    mm.Properties.NativeWindowHandle.ValueOrDefault != form.Properties.NativeWindowHandle.ValueOrDefault)
                    return mm;
            }
            catch { }
            return null;
        }

        private static string ReadWindowTexts(AutomationElement w)
        {
            try
            {
                var names = w.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
                             .Select(e => e.Name)
                             .Where(n => !string.IsNullOrWhiteSpace(n))
                             .Distinct();
                var t = string.Join(" ", names);
                return string.IsNullOrWhiteSpace(t) ? (w.Name ?? "") : t;
            }
            catch { return ""; }
        }

        /// <summary>อ่านข้อความจาก element หลายวิธี: Value pattern -> LegacyIAccessible -> ชื่อของลูก</summary>
        private static string GetElementText(AutomationElement e)
        {
            try
            {
                if (e.Patterns.Value.IsSupported)
                {
                    var v = e.Patterns.Value.Pattern.Value.ValueOrDefault;
                    if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
                }
            }
            catch { }

            try
            {
                if (e.Patterns.LegacyIAccessible.IsSupported)
                {
                    var v = e.Patterns.LegacyIAccessible.Pattern.Value.ValueOrDefault;
                    if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
                }
            }
            catch { }

            try
            {
                var names = e.FindAllDescendants()
                             .Select(c => c.Name)
                             .Where(n => !string.IsNullOrWhiteSpace(n) && !n.StartsWith("Description row"))
                             .Distinct();
                var t = string.Join(" ", names);
                if (!string.IsNullOrWhiteSpace(t)) return t.Trim();
            }
            catch { }

            return "";
        }

        /// <summary>อ่านข้อความแจ้งเตือนจากแผง Task Result (คอลัมน์ Description: เซลล์ชื่อ 'Description row N')</summary>
        private string ReadTaskResultText(AutomationElement scope)
        {
            try
            {
                var cells = scope.FindAllDescendants()
                                 .Where(e => !string.IsNullOrEmpty(e.Name) && e.Name.StartsWith("Description row"))
                                 .ToList();
                if (cells.Count == 0)
                    cells = _mainWindow.FindAllDescendants()
                                       .Where(e => !string.IsNullOrEmpty(e.Name) && e.Name.StartsWith("Description row"))
                                       .ToList();

                var msgs = cells.Select(c =>
                {
                    var t = GetElementText(c);
                    return string.IsNullOrEmpty(t) ? $"({c.Name}: อ่านข้อความไม่ได้)" : t;
                }).Distinct();

                return string.Join("; ", msgs);
            }
            catch { return ""; }
        }

        /// <summary>
        /// กด Save and Close แล้วตรวจว่าฟอร์มปิดจริง
        /// - มีคำถาม Yes/No -> กด Yes
        /// - มี message box แจ้งเตือน (validation) -> อ่านข้อความ กด OK แล้ว throw พร้อมข้อความนั้น
        /// - ฟอร์มไม่ปิดโดยไม่มี popup -> throw พร้อมข้อความจาก Task Result
        /// </summary>
        private void ClickSaveAndCloseEnsureClosed(Window form)
        {
            var btn = FindSaveAndCloseButton(form);
            if (btn == null) throw new Exception("หาปุ่ม Save and Close ไม่เจอ");

            ClickWithMouse(btn);

            for (int round = 0; round < 3; round++)
            {
                Window? popup = null;
                WaitUntil(() =>
                {
                    if (IsFormClosed(form)) return true;
                    popup = FindPopupOverForm(form);
                    return popup != null;
                }, 5000);

                if (IsFormClosed(form))
                {
                    RefreshGrid();
                    return;
                }

                if (popup == null) break;

                var yes = popup.FindFirstDescendant(cf => cf.ByName("Yes").Or(cf.ByName("&Yes")));
                if (yes != null)
                {
                    ClickWithMouse(yes);
                    WaitForClosed(() => popup, 2000);
                    continue;
                }

                string text = ReadWindowTexts(popup);
                var ok = popup.FindFirstDescendant(cf => cf.ByName("OK").Or(cf.ByName("ตกลง")));
                if (ok != null) ClickWithMouse(ok); else Keyboard.Press(VirtualKeyShort.ENTER);
                WaitForClosed(() => popup, 2000);

                throw new Exception($"Save and Close ไม่สำเร็จ มีแจ้งเตือน: {text}");
            }

            string task = ReadTaskResultText(form);
            throw new Exception("กด Save and Close แล้วฟอร์มยังไม่ปิด" +
                                (string.IsNullOrEmpty(task) ? "" : $" | Task Result: {task}"));
        }

        /// <summary>ปิดฟอร์ม/message box ที่ค้างอยู่ เพื่อให้เคสถัดไปทำงานต่อได้</summary>
        private void TryCloseOpenForm()
        {
            try
            {
                for (int i = 0; i < 5; i++)
                {
                    var w = FindPopupOverForm(FindOpenShiftForm() ?? _mainWindow.AsWindow()!) ?? FindOpenShiftForm();
                    if (w == null) return;

                    if (!IsShiftForm(w))
                    {
                        // message box: กด OK / No
                        var b = w.FindFirstDescendant(cf => cf.ByName("OK").Or(cf.ByName("ตกลง")).Or(cf.ByName("No")).Or(cf.ByName("&No")));
                        if (b != null) ClickWithMouse(b); else Keyboard.Press(VirtualKeyShort.ENTER);
                        Thread.Sleep(300);
                        continue;
                    }

                    ClickFormClose(w);
                    Thread.Sleep(400);
                }
            }
            catch { }
        }

        private void SelectRowByCodeInMainGrid(string targetCode)
        {
            RefreshGrid();

            var dataGrid = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataGrid))
                                     ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Table)), 3000);

            if (dataGrid != null)
            {
                var cell = Retry(() =>
                {
                    return dataGrid.FindAllDescendants().FirstOrDefault(d =>
                        !string.IsNullOrEmpty(d.Name) && d.Name.Trim().Equals(targetCode, StringComparison.OrdinalIgnoreCase))
                        ?? dataGrid.FindAllDescendants().FirstOrDefault(d =>
                        !string.IsNullOrEmpty(d.Name) && d.Name.Contains(targetCode));
                }, 3000);

                if (cell != null)
                {
                    ClickWithMouse(cell);
                    return;
                }
            }

            if (VerifyRecordExistsInGrid(targetCode, 3000, true))
            {
                var el = FindElementMatchingValue(targetCode);
                if (el != null)
                {
                    ClickWithMouse(el);
                    return;
                }
            }

            throw new Exception($"ไม่พบแถวที่มีรหัส '{targetCode}' ในตารางหลัก");
        }

        private void SelectRowInMainGrid(int rowIndex)
        {
            var dataGrid = _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataGrid))
                        ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Table));

            if (dataGrid != null)
            {
                var rows = dataGrid.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem).Or(cf.ByControlType(ControlType.ListItem)));
                if (rows.Length > 0)
                {
                    int targetIndex = Math.Min(rowIndex, rows.Length - 1);
                    ClickWithMouse(rows[targetIndex]);
                    return;
                }

                ClickWithMouse(dataGrid);
            }
        }

        private void SetInputTextSafely(AutomationElement context, string automationId, string text)
        {
            var el = Retry(() => context.FindFirstDescendant(cf => cf.ByAutomationId(automationId)))
                  ?? Retry(() => context.FindAllDescendants()
                                         .FirstOrDefault(e => string.Equals(e.AutomationId, automationId, StringComparison.OrdinalIgnoreCase)), 2000)
                  ?? Retry(() => context.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit))
                                         .FirstOrDefault(e => e.AutomationId.Contains(automationId, StringComparison.OrdinalIgnoreCase) ||
                                                              (e.Name != null && e.Name.Contains(automationId, StringComparison.OrdinalIgnoreCase))), 2000);

            // ช่องชื่อกะงานบางหน้าจอไม่มี AutomationId -> หาจาก label 'ชื่อกะงาน'
            if (el == null && automationId == "devTxtTimeShiftName")
            {
                var lbl = context.FindFirstDescendant(cf => cf.ByName("ชื่อกะงาน"));
                if (lbl != null)
                {
                    el = context.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit))
                                .FirstOrDefault(e => Math.Abs(e.BoundingRectangle.Y - lbl.BoundingRectangle.Y) < 15 &&
                                                     e.BoundingRectangle.X > lbl.BoundingRectangle.Right);
                }
            }

            if (el != null) ReplaceFieldText(el, text);
        }

        private void ClearField(AutomationElement context, string automationId)
        {
            var el = Retry(() => context.FindFirstDescendant(cf => cf.ByAutomationId(automationId)));
            if (el != null) ReplaceFieldText(el, "");
        }

        /// <summary>โฟกัส -> คลิก -> ล้างค่าเดิม -> พิมพ์ค่าใหม่ (ถ้ามี) -> TAB</summary>
        private void ReplaceFieldText(AutomationElement el, string text)
        {
            FocusSafely(el);
            ClickWithMouse(el);

            if (el.Patterns.Value.IsSupported)
            {
                try { el.Patterns.Value.Pattern.SetValue(""); } catch { }
            }
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            Keyboard.Press(VirtualKeyShort.BACK);
            Keyboard.Press(VirtualKeyShort.DELETE);

            if (!string.IsNullOrEmpty(text)) Keyboard.Type(text);
            Keyboard.Press(VirtualKeyShort.TAB);
        }

        private static void FocusSafely(AutomationElement el) =>
            WaitUntil(() => { try { el.Focus(); return true; } catch { return false; } }, 1000);

        private static AutomationElement? FindByIdIgnoreCase(AutomationElement root, string id) =>
            root.FindFirstDescendant(cf => cf.ByAutomationId(id))
            ?? root.FindAllDescendants().FirstOrDefault(e => string.Equals(e.AutomationId, id, StringComparison.OrdinalIgnoreCase));

        private static string DigitsOnly(string? t) =>
            new string((t ?? "").Where(char.IsDigit).ToArray()).TrimStart('0');

        private void SetRadioButton(AutomationElement context, string automationId)
        {
            var el = Retry(() => context.FindFirstDescendant(cf => cf.ByAutomationId(automationId)));
            if (el != null)
            {
                ClickWithMouse(el);
            }
        }

        private void ClickFormClose(Window form)
        {
            if (form == null || !form.IsAvailable) return;

            if (form.Properties.NativeWindowHandle.ValueOrDefault == _mainWindow.Properties.NativeWindowHandle.ValueOrDefault)
            {
                return;
            }

            var btnClose = form.FindFirstDescendant(cf => cf.ByName("Close"))
                        ?? form.FindFirstDescendant(cf => cf.ByName("&Close"))
                        ?? form.FindFirstDescendant(cf => cf.ByAutomationId("devBtnClose"))
                        ?? form.FindFirstDescendant(cf => cf.ByName("ปิด"));

            if (btnClose != null)
            {
                ClickWithMouse(btnClose);
            }
            else
            {
                try
                {
                    if (form.Patterns.Window.IsSupported) form.Patterns.Window.Pattern.Close();
                }
                catch { }
            }
        }

        private void HandleMessageBoxOk(string? expectedKeyword = null)
        {
            var msgBox = Retry(() =>
                _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault()
                ?? _automation.GetDesktop().FindAllChildren().FirstOrDefault(e => e.ControlType == ControlType.Window && (!string.IsNullOrEmpty(e.Name) && e.Name.Contains("Prosoft"))),
                3000);

            if (msgBox != null)
            {
                _ = expectedKeyword;
                var btnOk = msgBox.FindFirstDescendant(cf => cf.ByName("OK").Or(cf.ByName("ตกลง")))?.AsButton();
                if (btnOk != null) ClickWithMouse(btnOk);
                else Keyboard.Press(VirtualKeyShort.ENTER);
                WaitForClosed(() => msgBox, 2000);
            }
        }

        private void HandleMessageBoxConfirm(string buttonText, Window? parentForm = null)
        {
            string cleanText = buttonText.Replace("&", "").Trim();
            string altText = $"&{cleanText}";

            var msgBox = Retry(() =>
            {
                if (parentForm != null)
                {
                    var modal = parentForm.ModalWindows.FirstOrDefault();
                    if (modal != null) return modal;
                }

                var mainModal = _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault();
                if (mainModal != null) return mainModal;

                return _automation.GetDesktop().FindAllChildren()
                    .FirstOrDefault(e => e.ControlType == ControlType.Window &&
                                         (!string.IsNullOrEmpty(e.Name) &&
                                          (e.Name.Contains("คำเตือน") || e.Name.Contains("Question") || e.Name.Contains("Prosoft"))))?.AsWindow();
            }, 4000);

            if (msgBox != null)
            {
                var btn = msgBox.FindFirstDescendant(cf =>
                    cf.ByName(buttonText)
                    .Or(cf.ByName(cleanText))
                    .Or(cf.ByName(altText))
                    .Or(cf.ByAutomationId(cleanText))
                    .Or(cf.ByAutomationId(altText))
                );

                if (btn != null)
                {
                    ClickWithMouse(btn);
                }
                else
                {
                    if (cleanText.Equals("Yes", StringComparison.OrdinalIgnoreCase)) Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.KEY_Y);
                    else if (cleanText.Equals("No", StringComparison.OrdinalIgnoreCase)) Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.KEY_N);
                    else if (cleanText.Equals("Cancel", StringComparison.OrdinalIgnoreCase)) Keyboard.Press(VirtualKeyShort.ESCAPE);
                }
                WaitForClosed(() => msgBox, 2000);
            }
            else
            {
                throw new Exception($"ไม่พบหน้าต่างแจ้งเตือนคำเตือนสำหรับปุ่ม '{buttonText}'");
            }
        }

        private static void ClickWithMouse(AutomationElement? element)
        {
            if (element == null) return;

            try
            {
                var bounds = element.BoundingRectangle;
                if (!bounds.IsEmpty && bounds.Width > 0 && bounds.Height > 0)
                {
                    Point center = new Point((int)(bounds.X + (bounds.Width / 2)), (int)(bounds.Y + (bounds.Height / 2)));
                    Mouse.MoveTo(center);
                    Mouse.Click(center);
                    return;
                }
            }
            catch { }

            try { element.Focus(); } catch { }
            element.Click();
        }

        private void LogAddResultFromGrid(string testName, string code)
        {
            if (VerifyRecordExistsInGrid(code))
                LogSuccessCounted(testName, $"'{code}' เพิ่มสำเร็จ (พบในตารางหลัก)");
            else
                LogFailureCounted(testName, $"'{code}' ไม่พบในตารางหลักหลังบันทึก");
        }


        // =========================================================================================
        // หมวดที่ 3: ส่วนการทำงานกลางที่ตายตัว (Common / Reusable Helpers)
        // =========================================================================================

        private readonly AutomationElement _mainWindow;
        private readonly UIA3Automation _automation;
        private int _totalSuccess = 0;
        private int _totalFail = 0;
        private const int TimeoutMs = 6000;

        public TSInfoUC(AutomationElement mainWindow, UIA3Automation automation)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

        private void RefreshGrid()
        {
            try
            {
                var btnFind = _mainWindow.FindFirstDescendant(cf => cf.ByName("Find").Or(cf.ByAutomationId("btnFind")));
                if (btnFind != null) ClickWithMouse(btnFind);
            }
            catch { }
        }

        private bool VerifyRecordExistsInGrid(string value, int timeoutMs = 5000, bool refreshGrid = true)
        {
            if (refreshGrid) RefreshGrid();
            return Retry(() => FindElementMatchingValue(value), timeoutMs) != null;
        }

        private AutomationElement? FindElementMatchingValue(string value)
        {
            foreach (var el in _mainWindow.FindAllDescendants())
            {
                if (!string.IsNullOrEmpty(el.Name) && el.Name.Trim().Contains(value, StringComparison.OrdinalIgnoreCase))
                    return el;

                try
                {
                    if (el.Patterns.Value.IsSupported)
                    {
                        var val = el.Patterns.Value.Pattern.Value.Value;
                        if (!string.IsNullOrEmpty(val) && val.Trim().Contains(value, StringComparison.OrdinalIgnoreCase))
                            return el;
                    }
                }
                catch { }
            }
            return null;
        }

        public void LogSuccess(string testName, string message) => LogSuccessCounted(testName, message);
        public void LogFailure(string testName, string message) => LogFailureCounted(testName, message);

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
                Thread.Sleep(50); // กัน busy-loop กิน CPU
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
                Thread.Sleep(50); // กัน busy-loop กิน CPU
            }
            return null;
        }
    }
}