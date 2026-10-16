using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using global::HRMITestFlaUI.Base;
using HRMITestFlaUI.Base;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace HRMITestFlaUI.Module.Time_Attendance
{
    /// <summary>
    /// ทดสอบหน้าจอ "กำหนดรูปแบบกลุ่มตารางการทำงาน" (TemplateEmpScheInfoUC)
    /// เพิ่ม 5 ข้อมูล / Validation ตอนเพิ่ม / แก้ไข / Validation ตอนแก้ไข / ลบ
    /// ตาม TestStep.xlsx (sheet: TemplateEmpScheInfoUC)
    /// </summary>
    public class TemplateEmpScheInfoUC
    {
        // =========================================================================================
        // หมวดที่ 1: ข้อมูลการทดสอบทั้ง 5 ขั้น (Test Data)
        // =========================================================================================

        private sealed record AddItem(
            string Code,
            string NameTh,
            string NameEn,
            string Remark,
            string FormatShift,      // ข้อความของ radio: กะงานทั่วไป / กะงานแบบวนกะ
            string CalendarCode,     // รหัสปฏิทินวันหยุด ("" = ไม่เลือก)
            string ShiftCode,        // รหัสกะงานที่ใส่ในตาราง ("" = ไม่กรอก)
            string WorkDays,         // จำนวนวันทำงาน ("" = ข้ามขั้นตอนนี้ไป)
            string DaysOff,          // จำนวนวันหยุด ("" = ข้ามขั้นตอนนี้ไป)
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
            string SaveBtn,
            bool NeedsManualClose);
        private sealed record DeleteItem(
            string TargetCode,
            string DialogBtn,
            bool ExpectDeleted,
            int Progress,
            string ProgressMsg);

        private const string FormatGeneral = "กะงานทั่วไป";
        private const string FormatRotation = "กะงานแบบวนกะ";

        // ข้อความแจ้งเตือนที่คาดหวัง (ใช้ร่วมกันทั้งตอนเพิ่ม / แก้ไข / กด Close แล้วกด Yes)
        private static readonly string[] RequiredFieldErrors =
        {
            "ชื่อตารางการทำงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล",
            "ปฏิทินวันหยุด ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล",
            "รหัสกะงาน ต้องมีอย่างน้อย 1 รายการ"
        };

        // [ขั้นที่ 1] เพิ่มข้อมูล 5 รายการ
        // กรอกรหัส, ชื่อ (ไทย/Eng), เลือกปฏิทินวันหยุด, กรอกรายละเอียด, เลือกรูปแบบกะ,
        // กด ... เลือกรหัสกะงาน, กรอกจำนวนวันทำงาน, กรอกจำนวนวันหยุด, กดบันทึก
        private static readonly AddItem[] Step1_AddCases =
        {
            new("WSG001", "กะงานแบบทั่วไป", "Standard Work Shift",
                "ทำงานตามวันและเวลาทำงานปกติที่กำหนด",
                FormatGeneral, "CAL-TEST-06", "NRM-OFF-NEW", "", "", "Save", true),         // กด Save แล้วกด Close

            new("WSG002", "กะงานแบบวนกะ", "Rotating Work Shift",
                "ทำงานตามกะที่กำหนด เช่น กะเช้า กะบ่าย และกะกลางคืน",
                FormatRotation, "CAL-TEST-06", "NRM-OFF-NEW", "5", "5", "Save and New", false),

            new("WSG003", "กะงานแบบกำหนดเอง", "Custom Work Schedule",
                "กำหนดวันและเวลาทำงานตามความต้องการของหน่วยงาน",
                FormatGeneral, "CAL-TEST-06", "NRM-OFF-NEW", "", "", "Save and New", false),

            new("WSG004", "กำหนดรอบเวลาทำงาน", "Work Schedule Cycle",
                "กำหนดรอบวันทำงานและวันหยุดของพนักงาน",
                FormatRotation, "CAL-TEST-07", "NRM-OFF-NEW", "10", "5", "Save and New", false),

            new("WSG005", "วันหยุดตามตาราง", "Scheduled Days Off",
                "กำหนดวันหยุดให้สอดคล้องกับตารางการทำงาน",
                FormatGeneral, "CAL-TEST-07", "NRM-OFF-NEW", "", "", "Save and Close", false)
        };

        // [ขั้นที่ 2] Validation ตอนเพิ่ม: กรอกแค่รหัส ไม่กรอกชื่อ ไม่เลือกปฏิทิน ไม่เลือกรหัสกะงาน แล้วกด Save and Close
        private static readonly ValidationItem[] Step2_AddValidationCases =
        {
            new("TestCase01_RequiredFields", "W0001", "", "Save and Close", 30,
                "Validating: Required Fields...", RequiredFieldErrors)
        };

        // [ขั้นที่ 3] แก้ไขข้อมูล (แก้เฉพาะรหัสและชื่อ)
        private static readonly EditItem[] Step3_EditCases =
        {
            new("TestCase02_EditWSG004", "WSG004", "W0001", "กำหนดเวลาทำงานปกติ", "Save", true),                       // Save แล้วกด Close
            new("TestCase03_EditWSG005", "WSG005", "W0002", "วันหยุดหมุนเวียนตามรอบการทำงาน", "Save and Close", false)
        };

        // [ขั้นที่ 4] Validation ตอนแก้ไข: ลบชื่อ ลบปฏิทินวันหยุด ลบรหัสกะงาน แล้วกด Save and Close
        // ใน Excel เขียนว่า Edit WSG005 แต่ขั้นที่ 3 เปลี่ยนรหัสเป็น W0002 ไปแล้ว จึงต้องค้นหาด้วยรหัสใหม่
        private const string EditValidationTarget = "W0002";

        // [ขั้นที่ 5] ลบข้อมูล (WSG005 ถูกเปลี่ยนเป็น W0002 ในขั้นที่ 3)
        private static readonly DeleteItem[] Step5_DeleteCases =
        {
            new("W0002", "Yes", true, 92, "Delete Test: W0002 (ex-WSG005, confirmed delete)..."),
            new("WSG003", "No", false, 100, "Delete Test: WSG003 (cancelled delete)...")
        };


        // =========================================================================================
        // หมวดที่ 2: ส่วนที่ขึ้นกับหน้าจอนั้นๆ (Screen-Specific Implementation)
        // =========================================================================================

        private const string MenuName = "กำหนดรูปแบบกลุ่มตารางการทำงาน";
        private const string ScreenLogName = "TemplateEmpScheInfoPage";
        private const string FormId = "TemplateEmpScheInfoUC";
        private const string GeneralTabId = "devTabPgGeneral";   // ไม่เจอ tab นี้จะ fallback ไปใช้ฟอร์มทั้งหน้า
        private const string FirstObjectID = "devtxtTemplateEmpScheCode";

        private const string CodeFieldId = "devtxtTemplateEmpScheCode";
        private const string NameThFieldId = "devtxtTemplateEmpScheName";
        private const string NameEnFieldId = "devtxtTemplateEmpScheNameEng";
        private const string RemarkFieldId = "devMemoRemark";
        private const string CalendarButtonId = "devbtnCalendar";
        private const string FormatShiftGroupId = "devRdgFormatShift";
        private const string ShiftGridId = "devGridTemplateScheInfo";
        private const string ShiftCellName = "รหัสกะงาน row 0";
        // หัวคอลัมน์ในตารางกะงาน (ยืนยันจากหน้าจอจริงแล้ว) — DevExpress ตั้งชื่อเซลล์เป็น "<หัวคอลัมน์> row N"
        private const string ShiftHeader = "รหัสกะงาน";
        private const string WorkDaysHeader = "จำนวนวันทำงาน";
        private const string DaysOffHeader = "จำนวนวันหยุด";

        /// <summary>
        /// ขั้นที่ 1: เปิดหน้าจอและเพิ่มข้อมูล 5 รายการ
        /// </summary>
        public void NavigateToTemplateEmpScheInfoUC()
        {
            const string stageName = "TestCase00_AddTemplateEmpSche";
            TestLogger.LogInfo(ScreenLogName, $"เริ่มทดสอบหน้าจอ {MenuName}");
            TestProgressHelper.Report(2, $"Opening Setup -> {MenuName} Menu via Search...");

            if (!TryOpenMenu(stageName, MenuName)) return;

            Window? form = null;
            for (int i = 0; i < Step1_AddCases.Length; i++)
            {
                var item = Step1_AddCases[i];
                string testName = $"{stageName}_{item.Code}";
                TestProgressHelper.Report(4 + (i * 3), $"Adding {MenuName} ({i + 1}/{Step1_AddCases.Length}): {item.Code} - {item.NameTh}...");

                try
                {
                    form ??= GetOrOpenForm();
                    var tab = GetGeneralTab(form);
                    FillFormData(tab, item.Code, item.NameTh, item.NameEn, item.Remark, item.FormatShift, item.CalendarCode, item.ShiftCode, item.WorkDays, item.DaysOff);

                    if (!TrySaveAndHandleDuplicate(form, testName, item.Code, item.SaveBtn))
                    {
                        form = null;
                        continue;
                    }

                    if (item.SaveBtn == "Save and New")
                    {
                        // ต้องเห็นฟอร์มถูกเคลียร์จริงก่อนถึงจะนับว่าบันทึกสำเร็จ (เดิมไม่เช็คผล WaitUntil จึงขึ้น PASS เสมอ)
                        if (WaitUntil(IsFormCleared, 8000))
                        {
                            LogSuccess(testName, $"'{item.Code}' บันทึกสำเร็จ (Save and New)");
                        }
                        else
                        {
                            LogFailure(testName, $"'{item.Code}' กด Save and New แล้วฟอร์มไม่ถูกเคลียร์ (บันทึกไม่สำเร็จ)");
                            AbortForm(form);
                            form = null;
                        }
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
                    AbortForm(form);
                    form = null;
                }
            }
        }

        /// <summary>
        /// ขั้นที่ 2: Validation ตอนเพิ่ม + ทดสอบการกด Close (Yes / Cancel / No)
        /// </summary>
        public void RunAllValidationTests()
        {
            const string stage = "TestCase01_Validation";
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

            // กด Close แล้วกด Yes -> ยังพบข้อความเตือนและไม่ปิดหน้าจอ / Cancel -> ไม่ปิด / No -> ปิดหน้าจอ
            const string closeBase = "TestCase01_RequiredFields";
            RunStep($"{closeBase}_CloseSequence",
                () => RunCloseDialogSequence(form, closeBase, RequiredFieldErrors),
                40, "Validating: Close Sequence (Yes/Cancel/No)...");
        }

        /// <summary>
        /// ขั้นที่ 3: แก้ไขรหัส + ชื่อตารางการทำงาน
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
                    SetText(tab, CodeFieldId, item.NewCode);
                    SetText(tab, NameThFieldId, item.NameTh);

                    if (!TrySaveAndHandleDuplicate(form, item.Name, item.NewCode, item.SaveBtn)) continue;
                    if (item.NeedsManualClose) form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();

                    WaitForClosed(() => FindOpenForm());
                    RefreshGrid();

                    bool recordFound = VerifyRecordInGrid(item.NewCode, refresh: false);
                    if (recordFound)
                        LogSuccess(item.Name, $"แก้ไข '{item.OldCode}' -> '{item.NewCode}' สำเร็จ (พบข้อมูลที่แก้ไขในตารางหลัก)");
                    else
                        LogFailure(item.Name, $"แก้ไข '{item.OldCode}' แล้ว แต่ไม่พบ '{item.NewCode}' ในตารางหลัก");
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
        /// ขั้นที่ 4: Validation ตอนแก้ไข (ลบชื่อ / ปฏิทินวันหยุด / รหัสกะงาน แล้วกดบันทึก)
        /// </summary>
        public void RunEditValidationTests()
        {
            const string baseName = "TestCase04_EditValidation";
            TestProgressHelper.Report(58, "Opening Edit Form for Validation Tests...");

            Window form;
            try { form = ClickRowAndOpenEdit(EditValidationTarget); }
            catch (Exception ex) { LogFailure(baseName, $"เปิดฟอร์ม Edit ไม่สำเร็จ: {ex.Message}"); return; }

            var tab = GetGeneralTab(form);
            int failBefore = _totalFail;

            RunStep($"{baseName}_ClearRequiredFields",
                () =>
                {
                    SetText(tab, NameThFieldId, "");
                    ClearCalendar(tab);
                    ClearShiftGrid(tab);
                    EnsureRequiredFieldsCleared(tab); // ล้างค่าไม่ออกให้ FAIL ตรงนี้ ไม่กดบันทึกทับข้อมูลจริง
                    form.FindFirstDescendant(cf => cf.ByName("Save and Close"))?.Click();
                    AssertTaskResult(form, RequiredFieldErrors, $"{baseName}_ClearRequiredFields");
                },
                70, "Validating Edit: Clear Name / Calendar / Shift...");

            if (_totalFail > failBefore)
            {
                // เคสก่อนหน้าไม่ผ่าน = ไม่มีข้อความเตือนให้ทดสอบ Close ต่อ → บันทึก FAIL พร้อมเหตุผล แล้วปิดฟอร์มทิ้ง
                LogFailure($"{baseName}_CloseSequence", "ข้าม: เคส ClearRequiredFields ไม่ผ่าน จึงทดสอบลำดับ Close ต่อไม่ได้");
                AbortForm(form);
                TestProgressHelper.Report(80, "Validating Edit: Close Sequence skipped...");
            }
            else
            {
                RunStep($"{baseName}_CloseSequence",
                    () => RunCloseDialogSequence(form, baseName, RequiredFieldErrors),
                    80, "Validating Edit: Close Sequence (Yes/Cancel/No)...");
            }

            LogStageSummary(baseName, failBefore, "รัน Validation Edit ครบทุกเคสแล้ว");
        }

        /// <summary>
        /// ขั้นที่ 5: ลบรายการในตาราง
        /// </summary>
        public void RunDeleteTests()
        {
            const string baseName = "TestCase05_Delete";
            TestProgressHelper.Report(84, "Starting Delete Tests...");
            int failBefore = _totalFail;

            foreach (var testCase in Step5_DeleteCases)
            {
                RunDeleteCase(baseName, testCase.TargetCode, testCase.DialogBtn, testCase.ExpectDeleted);
                TestProgressHelper.Report(testCase.Progress, testCase.ProgressMsg);
            }

            LogStageSummary(baseName, failBefore, "ทดสอบ Delete ครบทุกเคส");
            TestLogger.LogInfo(ScreenLogName, $"สรุปผลรวมทั้งหมด: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");
        }

        // --- Custom Mapping Logic ประจำหน้าจอนี้ ---

        /* กรอกข้อมูลทั้งฟอร์ม: ข้อความ -> รูปแบบกะงาน (radio) -> ปฏิทินวันหยุด -> รหัสกะงาน -> จำนวนวันทำงาน -> จำนวนวันหยุด
           ต้องเลือก radio ก่อนเลือกรหัสกะงาน เพราะรายการใน popup อาจเปลี่ยนตามรูปแบบกะ
           จำนวนวันทำงาน / วันหยุด เป็นค่าว่าง = ข้ามขั้นตอนนี้ไป (ตาม TestStep) */
        private void FillFormData(AutomationElement tab, string code, string nameTh, string nameEn, string remark,
                                  string formatShift, string calendarCode, string shiftCode, string workDays, string daysOff)
        {
            SetText(tab, CodeFieldId, code);
            SetText(tab, NameThFieldId, nameTh);
            SetText(tab, NameEnFieldId, nameEn);
            SetText(tab, RemarkFieldId, remark);

            if (!string.IsNullOrEmpty(formatShift)) SelectFormatShift(tab, formatShift);
            if (!string.IsNullOrEmpty(calendarCode)) PickCalendar(tab, calendarCode);
            if (!string.IsNullOrEmpty(shiftCode)) FillShiftGrid(tab, shiftCode);
            if (!string.IsNullOrEmpty(workDays) || !string.IsNullOrEmpty(daysOff))
            {
                // กรอกในแถวเดียวกับรหัสกะงานที่เพิ่งเลือก (ไม่ fix ว่าเป็นแถวแรก เพราะตารางอาจมีแถวว่างอยู่ก่อน)
                int row = FindShiftRowIndex(tab, shiftCode);
                if (!string.IsNullOrEmpty(workDays)) FillGridNumberCell(tab, $"{WorkDaysHeader} row {row}", workDays, "จำนวนวันทำงาน");
                if (!string.IsNullOrEmpty(daysOff)) FillGridNumberCell(tab, $"{DaysOffHeader} row {row}", daysOff, "จำนวนวันหยุด");
            }
        }

        private void SelectFormatShift(AutomationElement tab, string optionText)
        {
            var group = Retry(() => tab.FindFirstDescendant(cf => cf.ByAutomationId(FormatShiftGroupId)), 3000)
                        ?? throw new Exception($"ไม่พบกลุ่ม radio {FormatShiftGroupId}");

            var option = group.FindAllDescendants(cf => cf.ByControlType(ControlType.RadioButton))
                             .FirstOrDefault(r => !string.IsNullOrEmpty(r.Name) && r.Name.Contains(optionText, StringComparison.OrdinalIgnoreCase))
                         ?? group.FindAllDescendants().FirstOrDefault(el => !string.IsNullOrEmpty(el.Name) && el.Name.Contains(optionText, StringComparison.OrdinalIgnoreCase))
                         ?? throw new Exception($"ไม่พบตัวเลือก '{optionText}' ใน {FormatShiftGroupId}");

            option.Click();
            Thread.Sleep(250);
        }

        /* กดปุ่ม "..." ของคอนโทรล lookup แล้วเลือกรหัสที่ต้องการใน popup
           1) หาแถวที่ตรงรหัส (เทียบทั้ง Name และ Value เพราะ DevExpress ตั้ง Name ของ cell เป็น "<ชื่อคอลัมน์> row N")
           2) คลิกแถว แล้วกดปุ่ม Select (ถ้าไม่เจอปุ่มจะดับเบิลคลิกแทน)
           3) อ่านค่าที่คอนโทรลกลับมาเทียบกับรหัสที่ต้องการ ถ้าไม่ตรงให้ throw (กันกรณีได้แถวแรกที่ถูก highlight ไว้ตั้งต้น)
           หาแถวไม่เจอ: dump ข้อความใน popup ลง log แล้ว Cancel ปิด popup แล้ว throw */
        private void PickFromLookup(AutomationElement control, string code, string description,
                                    Func<AutomationElement?>? reread = null)
        {
            ClickLookupButton(control);

            // รอ popup เปิด (เห็นปุ่ม Select)
            if (Retry(() => FindPopupButton("Select", "&Select", "เลือก"), 5000) == null)
            {
                DumpWindows();
                // เผื่อ popup เปิดอยู่แต่โปรแกรมมองไม่เห็น: กด ESC ปิดไว้ก่อน ไม่ให้ค้างไปกระทบเคสถัดไป
                try { Keyboard.Press(VirtualKeyShort.ESCAPE); Thread.Sleep(500); } catch { }
                throw new Exception($"กด ... แล้วไม่พบ popup เลือก{description}");
            }
            Thread.Sleep(300);

            // ไม่ต้องค้นหา: คลิกแถวที่ตรงกับรหัสใน Test Step โดยตรง แล้วกด Select
            var item = Retry(() => FindInPopup(code), 5000, 300);
            if (item == null)
            {
                DumpPopup(description, code);
                CancelPopup();
                throw new Exception($"ไม่พบ{description} '{code}' ใน popup (ตรวจว่ามีรหัสนี้อยู่ในข้อมูลหลักจริงหรือไม่)");
            }

            item.Click();
            Thread.Sleep(300);

            var selectBtn = FindPopupButton("Select", "&Select", "เลือก");
            if (selectBtn != null) selectBtn.Click();
            else item.DoubleClick();

            // popup ต้องปิด ถ้าไม่ปิดให้ Cancel แล้ว throw (กัน popup ค้างจนคีย์ที่พิมพ์ถัดไปไหลเข้าช่อง Look for)
            if (!WaitUntil(() => FindPopupButton("Select", "&Select", "เลือก") == null, 3000))
            {
                CancelPopup();
                throw new Exception($"เลือก{description} '{code}' แล้ว popup ไม่ปิด");
            }
            Thread.Sleep(500);

            if (reread != null)
            {
                string actual = ReadText(reread());
                if (!string.IsNullOrWhiteSpace(actual) && !actual.Contains(code, StringComparison.OrdinalIgnoreCase))
                    throw new Exception($"เลือก{description}แล้ว แต่ค่าในช่องเป็น '{actual}' ไม่ใช่ '{code}'");
                if (string.IsNullOrWhiteSpace(actual))
                    TestLogger.LogInfo(ScreenLogName, $"อ่านค่า{description}กลับมาตรวจไม่ได้ (ช่องว่างหรืออ่าน Value ไม่ได้) กรุณาดูหน้าจอประกอบ");
            }
        }

        private AutomationElement? FindPopupButton(params string[] names)
        {
            int pid = _mainWindow.Properties.ProcessId.ValueOrDefault;
            foreach (var win in GetPopupWindows(pid))
            {
                foreach (var n in names)
                {
                    var b = win.FindFirstDescendant(cf => cf.ByName(n));
                    if (b != null && !b.Properties.IsOffscreen.ValueOrDefault) return b;
                }
            }
            return null;
        }

        private static string ReadText(AutomationElement? el)
        {
            if (el == null) return "";
            try
            {
                if (el.Patterns.Value.IsSupported)
                {
                    var v = el.Patterns.Value.Pattern.Value.Value;
                    if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
                }
                foreach (var edit in el.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit)))
                {
                    if (!edit.Patterns.Value.IsSupported) continue;
                    var v = edit.Patterns.Value.Pattern.Value.Value;
                    if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
                }
            }
            catch { }
            return "";
        }

        /* เขียน Name / Value ของ element ใน popup ลง log เพื่อดูว่าโปรแกรมมองเห็นอะไรบ้าง */
        private void DumpPopup(string description, string code)
        {
            try
            {
                int pid = _mainWindow.Properties.ProcessId.ValueOrDefault;
                var lines = new List<string>();
                foreach (var win in GetPopupWindows(pid))
                {
                    lines.Add($"[window] '{win.Name}'");
                    foreach (var el in win.FindAllDescendants().Take(80))
                    {
                        string val = "";
                        try { if (el.Patterns.Value.IsSupported) val = el.Patterns.Value.Pattern.Value.Value ?? ""; } catch { }
                        if (!string.IsNullOrEmpty(el.Name) || !string.IsNullOrEmpty(val))
                            lines.Add($"  {el.ControlType} name='{el.Name}' value='{val}'");
                    }
                }
                TestLogger.LogInfo(ScreenLogName, $"หา{description} '{code}' ใน popup ไม่เจอ ข้อมูลที่เห็น:\n" + string.Join("\n", lines));
            }
            catch { }
        }

        /* ปุ่ม "..." ของ DevExpress เป็น Not Focusable จึงต้องคลิกเมาส์ที่พิกัดจริง */
        private static void ClickLookupButton(AutomationElement control)
        {
            var btn = control.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                             .Where(b => !b.Properties.IsOffscreen.ValueOrDefault && b.BoundingRectangle.Width > 0)
                             .OrderBy(b => b.BoundingRectangle.X)
                             .LastOrDefault();

            var rect = btn?.BoundingRectangle ?? control.BoundingRectangle;
            int x = btn != null ? rect.Left + rect.Width / 2 : rect.Right - 10;
            int y = rect.Top + rect.Height / 2;

            Mouse.MoveTo(new System.Drawing.Point(x, y));
            Thread.Sleep(80);
            Mouse.Click(MouseButton.Left);
        }

        private void PickCalendar(AutomationElement tab, string calendarCode)
        {
            var control = Retry(() => tab.FindFirstDescendant(cf => cf.ByAutomationId(CalendarButtonId)), 3000)
                          ?? throw new Exception($"ไม่พบปุ่ม {CalendarButtonId}");

            PickFromLookup(control, calendarCode, "รหัสปฏิทิน",
                () => tab.FindFirstDescendant(cf => cf.ByAutomationId(CalendarButtonId)));
        }

        /* เลือกรหัสกะงานของแถวแรกในตาราง devGridTemplateScheInfo ผ่านปุ่ม "..." ในเซลล์ */
        private void FillShiftGrid(AutomationElement tab, string shiftCode)
        {
            var grid = Retry(() => tab.FindFirstDescendant(cf => cf.ByAutomationId(ShiftGridId)), 3000)
                       ?? throw new Exception($"ไม่พบตาราง {ShiftGridId}");

            var cell = Retry(() => grid.FindFirstDescendant(cf => cf.ByName(ShiftCellName)), 3000)
                       ?? throw new Exception($"ไม่พบช่องรหัสกะงานแถวแรกใน {ShiftGridId}");

            cell.Click();               // โฟกัสเซลล์ก่อน ให้ปุ่ม "..." แสดงขึ้นมา
            Thread.Sleep(300);

            PickFromLookup(cell, shiftCode, "รหัสกะงาน",
                () => grid.FindFirstDescendant(cf => cf.ByName(ShiftCellName)));
        }

        /* หาเลขแถวในตารางกะงานที่มีรหัสกะงานตรงกับ shiftCode (อ่านจากเซลล์ "รหัสกะงาน row N")
           หาไม่เจอ / อ่านค่าไม่ได้ → ใช้แถวแรก (row 0) */
        private int FindShiftRowIndex(AutomationElement tab, string shiftCode)
        {
            var grid = Retry(() => tab.FindFirstDescendant(cf => cf.ByAutomationId(ShiftGridId)), 3000)
                       ?? throw new Exception($"ไม่พบตาราง {ShiftGridId}");

            string prefix = $"{ShiftHeader} row ";
            foreach (var cell in grid.FindAllDescendants().Where(el => (el.Name ?? "").StartsWith(prefix)))
            {
                if (ReadCellValue(cell).Contains(shiftCode, StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(cell.Name.Substring(prefix.Length).Trim(), out int row))
                    return row;
            }
            return 0;
        }

        /* กรอกตัวเลขลงเซลล์ในตารางกะงาน (จำนวนวันทำงาน / จำนวนวันหยุด) แบบเดียวกับคนกรอก
           ลองทีละวิธี แล้วอ่านค่าในเซลล์กลับมาตรวจทุกครั้ง ได้ค่าตรงเมื่อไหร่ก็หยุด:
             วิธีที่ 1: คลิกเซลล์ให้เปิดช่องแก้ไข → ใส่ค่าลงช่องแก้ไขที่โฟกัสอยู่ → Tab เพื่อ commit
             วิธีที่ 2: คลิกเซลล์ให้เปิดช่องแก้ไข → Ctrl+A → พิมพ์ด้วยคีย์บอร์ด → Tab
             วิธีที่ 3: ตั้งค่าผ่าน ValuePattern ของเซลล์
             วิธีที่ 4: ตั้งค่าผ่าน LegacyIAccessible ของเซลล์
           ไม่ติดทุกวิธี: เขียนสถานะของเซลล์ / ช่องที่โฟกัสอยู่ลง log แล้ว throw
           หาเซลล์ไม่เจอ: เขียนรายชื่อเซลล์ที่เห็นในตารางลง log แล้ว throw */
        private void FillGridNumberCell(AutomationElement tab, string cellName, string value, string description)
        {
            var grid = Retry(() => tab.FindFirstDescendant(cf => cf.ByAutomationId(ShiftGridId)), 3000)
                       ?? throw new Exception($"ไม่พบตาราง {ShiftGridId}");

            AutomationElement? FindCell() => grid.FindFirstDescendant(cf => cf.ByName(cellName));
            bool IsFilled() => WaitUntil(() => SameNumber(ReadCellValue(FindCell()), value), 1500);

            var cell = Retry(FindCell, 3000);
            if (cell == null)
            {
                DumpGridCells(grid);
                throw new Exception($"ไม่พบช่อง{description} ('{cellName}') ใน {ShiftGridId} (ดูรายชื่อเซลล์ใน log)");
            }

            // วิธีที่ 1: ใส่ค่าลงช่องแก้ไข (in-place editor) ที่เปิดขึ้นมาในเซลล์
            var editor = OpenCellEditor(FindCell() ?? cell);
            if (editor != null)
            {
                try
                {
                    if (editor.Patterns.Value.IsSupported && !editor.Patterns.Value.Pattern.IsReadOnly.ValueOrDefault)
                        editor.Patterns.Value.Pattern.SetValue(value);
                    else
                    {
                        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                        Keyboard.Type(value);
                    }
                    Keyboard.Type(VirtualKeyShort.TAB);
                    if (IsFilled()) return;
                }
                catch { }
            }

            // วิธีที่ 2: พิมพ์ด้วยคีย์บอร์ด
            OpenCellEditor(FindCell() ?? cell);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            Keyboard.Type(value);
            Keyboard.Type(VirtualKeyShort.TAB);
            if (IsFilled()) return;

            // วิธีที่ 3: ValuePattern ของเซลล์
            try
            {
                var c = FindCell();
                if (c != null && c.Patterns.Value.IsSupported)
                {
                    c.Patterns.Value.Pattern.SetValue(value);
                    if (IsFilled()) return;
                }
            }
            catch { }

            // วิธีที่ 4: LegacyIAccessible ของเซลล์
            try
            {
                var c = FindCell();
                if (c != null && c.Patterns.LegacyIAccessible.IsSupported)
                {
                    c.Patterns.LegacyIAccessible.Pattern.SetValue(value);
                    if (IsFilled()) return;
                }
            }
            catch { }

            DumpCellState(FindCell(), cellName);
            throw new Exception($"กรอก{description} '{value}' ไม่ติด ค่าในช่องยังเป็น '{ReadCellValue(FindCell())}' (ลองครบ 4 วิธีแล้ว ดูสถานะเซลล์ใน log)");
        }

        /* เปิดช่องแก้ไขของเซลล์: คลิกครั้งแรกเพื่อโฟกัส → รอเกินเวลาดับเบิลคลิก → คลิกซ้ำเพื่อเปิด editor (ยังไม่เปิดก็กด F2)
           คืนช่องแก้ไขที่โฟกัสอยู่และอยู่ในพื้นที่ของเซลล์ (หาไม่เจอคืน null) */
        private AutomationElement? OpenCellEditor(AutomationElement cell)
        {
            var r = cell.BoundingRectangle;
            var center = new System.Drawing.Point(r.Left + r.Width / 2, r.Top + r.Height / 2);

            Mouse.Click(center);
            Thread.Sleep(600);              // เว้นให้เกินเวลาดับเบิลคลิก กันไปเปิดหน้าจออื่นจาก double-click
            Mouse.Click(center);
            Thread.Sleep(300);

            var editor = FocusedEditorIn(r);
            if (editor != null) return editor;

            Keyboard.Type(VirtualKeyShort.F2);
            Thread.Sleep(300);
            return FocusedEditorIn(r);
        }

        /* ช่องที่กำลังโฟกัสอยู่ ถ้าเป็นช่องกรอก (Edit / Spinner) และอยู่ในพื้นที่ของเซลล์ */
        private AutomationElement? FocusedEditorIn(System.Drawing.Rectangle cellRect)
        {
            try
            {
                var f = _automation.FocusedElement();
                if (f == null) return null;
                if (f.ControlType != ControlType.Edit && f.ControlType != ControlType.Spinner) return null;
                var fr = f.BoundingRectangle;
                var mid = new System.Drawing.Point(fr.Left + fr.Width / 2, fr.Top + fr.Height / 2);
                return cellRect.Contains(mid) ? f : null;
            }
            catch { return null; }
        }

        /* เขียนสถานะของเซลล์และช่องที่โฟกัสอยู่ลง log (ใช้ตอนกรอกไม่ติด) */
        private void DumpCellState(AutomationElement? cell, string cellName)
        {
            try
            {
                var lines = new List<string> { $"เซลล์ '{cellName}': {(cell == null ? "หาไม่เจอ" : "เจอ")}" };
                if (cell != null)
                {
                    lines.Add($"  ControlType={cell.ControlType} Enabled={cell.Properties.IsEnabled.ValueOrDefault} Rect={cell.BoundingRectangle}");
                    lines.Add($"  ValuePattern={cell.Patterns.Value.IsSupported}" +
                              (cell.Patterns.Value.IsSupported ? $" ReadOnly={cell.Patterns.Value.Pattern.IsReadOnly.ValueOrDefault} Value='{cell.Patterns.Value.Pattern.Value.ValueOrDefault}'" : ""));
                    lines.Add($"  Legacy={cell.Patterns.LegacyIAccessible.IsSupported}" +
                              (cell.Patterns.LegacyIAccessible.IsSupported ? $" Value='{cell.Patterns.LegacyIAccessible.Pattern.Value.ValueOrDefault}' State={cell.Patterns.LegacyIAccessible.Pattern.State.ValueOrDefault}" : ""));
                }
                var f = _automation.FocusedElement();
                if (f != null)
                    lines.Add($"ช่องที่โฟกัสอยู่: ControlType={f.ControlType} Name='{f.Name}' Id='{f.Properties.AutomationId.ValueOrDefault}' Rect={f.BoundingRectangle}");
                TestLogger.LogInfo(ScreenLogName, string.Join("\n", lines));
            }
            catch { }
        }

        /* อ่านค่าจริงของเซลล์ในตาราง (Value → LegacyIAccessible.Value) */
        private static string ReadCellValue(AutomationElement? cell)
        {
            if (cell == null) return "";
            try { if (cell.Patterns.Value.IsSupported && !string.IsNullOrWhiteSpace(cell.Patterns.Value.Pattern.Value.Value)) return cell.Patterns.Value.Pattern.Value.Value.Trim(); } catch { }
            try { if (cell.Patterns.LegacyIAccessible.IsSupported && !string.IsNullOrWhiteSpace(cell.Patterns.LegacyIAccessible.Pattern.Value.Value)) return cell.Patterns.LegacyIAccessible.Pattern.Value.Value.Trim(); } catch { }
            return "";
        }

        /* เทียบค่าตัวเลข (ช่องตัวเลขอาจแสดงเป็น 5 / 5.00 / 5.0) */
        private static bool SameNumber(string actual, string expected) =>
            actual.Trim() == expected.Trim()
            || (decimal.TryParse(actual.Trim(), out var a) && decimal.TryParse(expected.Trim(), out var e) && a == e);

        /* เขียนชื่อเซลล์ทั้งหมดในตารางกะงานลง log (ใช้หาชื่อเซลล์ที่ถูกต้องเมื่อหาไม่เจอ) */
        private void DumpGridCells(AutomationElement grid)
        {
            try
            {
                var names = grid.FindAllDescendants()
                                .Select(el => el.Name)
                                .Where(n => !string.IsNullOrEmpty(n))
                                .Distinct()
                                .Take(60);
                TestLogger.LogInfo(ScreenLogName, $"เซลล์ที่เห็นใน {ShiftGridId}: " + string.Join(" | ", names));
            }
            catch { }
        }

        /* ลบค่าในช่องปฏิทินวันหยุด (ใช้ตอนทดสอบ Validation ตอนแก้ไข)
           ต้องให้ว่างทั้งรหัสปฏิทิน (ช่องที่มีปุ่ม ...) และชื่อปฏิทิน (ช่องสีเทาทางขวา)
           เดิมใช้ ValuePattern.SetValue("") ซึ่งล้างแค่ข้อความที่แสดง แต่ค่าปฏิทินจริงยังอยู่ → บันทึกแล้วไม่ขึ้นข้อความเตือนปฏิทิน
           ลองทีละวิธี และตรวจหลังทุกวิธี:
             วิธีที่ 1: คลิกในช่อง (ฝั่งซ้าย ไม่โดนปุ่ม ...) → Ctrl+A → Delete → Tab เพื่อ commit ค่า
             วิธีที่ 2: คลิกในช่อง → End → Shift+Home → Backspace → Tab
             วิธีที่ 3: ตั้งค่าว่างผ่าน ValuePattern ของช่องข้อความด้านใน → Tab
             วิธีที่ 4: กดปุ่ม ... แล้วหาปุ่มล้างค่าใน popup (Clear / ล้าง) ถ้าไม่มีให้ Cancel ปิด popup
           ไม่ออกทุกวิธี: เขียนค่าที่ค้างลง log (EnsureRequiredFieldsCleared จะ FAIL ต่อเอง) */
        private void ClearCalendar(AutomationElement tab)
        {
            AutomationElement? Control() => tab.FindFirstDescendant(cf => cf.ByAutomationId(CalendarButtonId));
            bool IsCleared() => WaitUntil(() => string.IsNullOrWhiteSpace(ReadCalendarLeftover(tab)), 1500);

            var control = Retry(Control, 3000) ?? throw new Exception($"ไม่พบช่อง {CalendarButtonId}");
            if (string.IsNullOrWhiteSpace(ReadCalendarLeftover(tab))) return;

            // วิธีที่ 1: Ctrl+A → Delete → Tab
            ClickCalendarText(Control() ?? control);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            Keyboard.Type(VirtualKeyShort.DELETE);
            Keyboard.Type(VirtualKeyShort.TAB);
            if (IsCleared()) return;

            // วิธีที่ 2: End → Shift+Home → Backspace → Tab
            ClickCalendarText(Control() ?? control);
            Keyboard.Type(VirtualKeyShort.END);
            Keyboard.TypeSimultaneously(VirtualKeyShort.SHIFT, VirtualKeyShort.HOME);
            Keyboard.Type(VirtualKeyShort.BACK);
            Keyboard.Type(VirtualKeyShort.TAB);
            if (IsCleared()) return;

            // วิธีที่ 3: ValuePattern ของช่องข้อความด้านใน แล้ว Tab ออกเพื่อ commit
            try
            {
                var c = Control() ?? control;
                var edit = c.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)) ?? c;
                if (edit.Patterns.Value.IsSupported)
                {
                    ClickCalendarText(c);
                    edit.Patterns.Value.Pattern.SetValue("");
                    Keyboard.Type(VirtualKeyShort.TAB);
                    if (IsCleared()) return;
                }
            }
            catch { }

            // วิธีที่ 4: ปุ่มล้างค่าใน popup ของปุ่ม ...
            try
            {
                ClickLookupButton(Control() ?? control);
                if (Retry(() => FindPopupButton("Select", "&Select", "เลือก"), 5000) != null)
                {
                    var clearBtn = FindPopupButton("Clear", "&Clear", "ล้าง", "ล้างค่า", "ล้างข้อมูล");
                    if (clearBtn != null)
                    {
                        clearBtn.Click();
                        WaitUntil(() => FindPopupButton("Select", "&Select", "เลือก") == null, 3000);
                        if (IsCleared()) return;
                    }
                    else CancelPopup();
                }
            }
            catch { }

            TestLogger.LogInfo(ScreenLogName, $"ล้างปฏิทินวันหยุดไม่ออก (ลองครบ 4 วิธีแล้ว) ค่าที่ยังค้าง: {ReadCalendarLeftover(tab)}");
            DumpCellState(Control(), CalendarButtonId);
        }

        /* อ่านค่าปฏิทินวันหยุดที่ยังค้างอยู่ ทั้งรหัส (ช่องที่มีปุ่ม ...) และชื่อปฏิทิน (ช่องสีเทาทางขวา) — ว่างหมด = คืน "" */
        private string ReadCalendarLeftover(AutomationElement tab)
        {
            var control = tab.FindFirstDescendant(cf => cf.ByAutomationId(CalendarButtonId));
            if (control == null) return "";

            var parts = new List<string>();
            string code = ReadText(control);
            if (!string.IsNullOrWhiteSpace(code)) parts.Add($"รหัส='{code}'");

            string name = ReadText(FindCalendarNameBox(tab, control));
            if (!string.IsNullOrWhiteSpace(name)) parts.Add($"ชื่อ='{name}'");

            return string.Join(", ", parts);
        }

        /* หาช่องชื่อปฏิทิน: ช่องข้อความที่อยู่ติดทางขวาของช่องรหัสปฏิทิน ในแถวเดียวกัน */
        private static AutomationElement? FindCalendarNameBox(AutomationElement tab, AutomationElement control)
        {
            try
            {
                var r = control.BoundingRectangle;
                if (r.IsEmpty) return null;
                int midY = r.Top + r.Height / 2;

                return tab.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit))
                          .Where(e =>
                          {
                              var b = e.BoundingRectangle;
                              return !b.IsEmpty && b.Left >= r.Right - 2 && b.Left - r.Right < 60
                                     && b.Top <= midY && b.Bottom >= midY;
                          })
                          .OrderBy(e => e.BoundingRectangle.Left)
                          .FirstOrDefault();
            }
            catch { return null; }
        }

        /* คลิกในส่วนข้อความของช่องปฏิทิน (ฝั่งซ้าย) ไม่ให้โดนปุ่ม ... ที่ขอบขวา */
        private static void ClickCalendarText(AutomationElement control)
        {
            var r = control.BoundingRectangle;
            Mouse.Click(new System.Drawing.Point(r.Left + Math.Min(30, r.Width / 4), r.Top + r.Height / 2));
            Thread.Sleep(250);
        }

        /* ลบรหัสกะงานออกจากตาราง (ใช้ตอนทดสอบ Validation ตอนแก้ไข)
           ลองทีละวิธี และตรวจหลังทุกวิธีว่าแถวแรกไม่มีรหัสกะงานเหลือแล้ว:
             วิธีที่ 1: คลิกหัวแถว (ช่องเลขแถวด้านซ้าย) ให้โฟกัสแถวโดยไม่เปิดช่องแก้ไข → Ctrl+Delete → ตอบ Yes ถ้ามี Pop-up ยืนยัน
                        (เดิมคลิกที่ช่องรหัสกะงาน ทำให้ช่องแก้ไขเปิด Ctrl+Delete เลยไปลบข้อความแทนที่จะลบแถว
                         และเดิมหา Pop-up ยืนยันผิดหน้าต่าง ทำให้ Pop-up ค้าง)
             วิธีที่ 2: กดปุ่ม Remove (−) ที่แถบเลื่อนข้อมูลใต้ตาราง
             วิธีที่ 3: ลบข้อความในช่องรหัสกะงานแทนการลบแถว
           ไม่ออกทุกวิธี: เขียนสถานะเซลล์ + รายชื่อเซลล์ในตารางลง log (EnsureRequiredFieldsCleared จะ FAIL ต่อเอง) */
        private void ClearShiftGrid(AutomationElement tab)
        {
            var grid = Retry(() => tab.FindFirstDescendant(cf => cf.ByAutomationId(ShiftGridId)), 3000)
                       ?? throw new Exception($"ไม่พบตาราง {ShiftGridId}");

            AutomationElement? ShiftCell() => grid.FindFirstDescendant(cf => cf.ByName(ShiftCellName));
            bool IsEmptyNow() { var c = ShiftCell(); return c == null || string.IsNullOrWhiteSpace(ReadCellValue(c)); }
            bool IsCleared() => WaitUntil(IsEmptyNow, 1500);

            if (IsEmptyNow()) return;

            // วิธีที่ 1: โฟกัสแถวจากหัวแถว → Ctrl+Delete (ลบทีละแถว เผื่อมีหลายแถว)
            for (int i = 0; i < 5; i++)
            {
                string before = ReadCellValue(ShiftCell());
                if (!FocusShiftRow(ShiftCell())) break;
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.DELETE);
                AnswerConfirmDialog();

                if (IsCleared()) return;
                if (ReadCellValue(ShiftCell()) == before) break;   // ไม่มีอะไรเปลี่ยน = วิธีนี้ใช้ไม่ได้
            }

            // วิธีที่ 2: ปุ่ม Remove (−) ที่แถบเลื่อนข้อมูลใต้ตาราง
            var removeBtn = grid.FindAllDescendants()
                                .FirstOrDefault(el => new[] { "Remove", "Delete", "ลบ" }.Contains((el.Name ?? "").Trim()));
            if (removeBtn != null)
            {
                for (int i = 0; i < 5; i++)
                {
                    string before = ReadCellValue(ShiftCell());
                    FocusShiftRow(ShiftCell());
                    var r = removeBtn.BoundingRectangle;
                    Mouse.Click(new System.Drawing.Point(r.Left + r.Width / 2, r.Top + r.Height / 2));
                    AnswerConfirmDialog();

                    if (IsCleared()) return;
                    if (ReadCellValue(ShiftCell()) == before) break;
                }
            }

            // วิธีที่ 3: ลบข้อความในช่องรหัสกะงาน
            var cell = ShiftCell();
            if (cell != null)
            {
                var editor = OpenCellEditor(cell);
                try
                {
                    if (editor != null && editor.Patterns.Value.IsSupported && !editor.Patterns.Value.Pattern.IsReadOnly.ValueOrDefault)
                        editor.Patterns.Value.Pattern.SetValue("");
                    else
                    {
                        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                        Keyboard.Type(VirtualKeyShort.DELETE);
                    }
                    Keyboard.Type(VirtualKeyShort.TAB);
                }
                catch { }
                if (IsCleared()) return;

                try
                {
                    var c = ShiftCell();
                    if (c != null && c.Patterns.Value.IsSupported)
                    {
                        c.Patterns.Value.Pattern.SetValue("");
                        if (IsCleared()) return;
                    }
                }
                catch { }
            }

            DumpCellState(ShiftCell(), ShiftCellName);
            DumpGridCells(grid);
        }

        /* คลิกหัวแถว (ช่องเลขแถวทางซ้ายของคอลัมน์แรก) เพื่อเลือกแถวโดยไม่เปิดช่องแก้ไข */
        private static bool FocusShiftRow(AutomationElement? shiftCell)
        {
            if (shiftCell == null) return false;
            var r = shiftCell.BoundingRectangle;
            if (r.IsEmpty) return false;
            Mouse.Click(new System.Drawing.Point(r.Left - 10, r.Top + r.Height / 2));
            Thread.Sleep(300);
            return true;
        }

        /* ตอบ Yes ใน Pop-up ยืนยันการลบแถว (Pop-up เป็นลูกของฟอร์ม ไม่ใช่ของหน้าต่างหลัก) — ไม่มี Pop-up ก็ข้าม */
        private void AnswerConfirmDialog()
        {
            try
            {
                var form = FindOpenForm();
                var dlg = Retry(() => form?.ModalWindows.FirstOrDefault()
                                      ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("คำเตือน"))?.AsWindow()
                                      ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("Confirm"))?.AsWindow(), 1500);
                if (dlg == null) return;

                foreach (var n in new[] { "Yes", "ใช่", "OK" })
                {
                    var b = dlg.FindFirstDescendant(cf => cf.ByName(n)) ?? dlg.FindFirstDescendant(cf => cf.ByName($"&{n}"));
                    if (b == null) continue;
                    b.Click();
                    WaitForClosed(() => dlg, 3000);
                    return;
                }
            }
            catch { }
        }

        /* ตรวจว่าช่องชื่อ / ปฏิทินวันหยุด / รหัสกะงาน ว่างจริงก่อนกดบันทึก
           ถ้ายังมีค่าค้าง → throw บอกว่าช่องไหนล้างไม่ออก (กันกด Save and Close แล้วบันทึกทับข้อมูลเดิมโดยไม่รู้ตัว) */
        private void EnsureRequiredFieldsCleared(AutomationElement tab)
        {
            var leftovers = new List<string>();

            string name = ReadText(tab.FindFirstDescendant(cf => cf.ByAutomationId(NameThFieldId)));
            if (!string.IsNullOrWhiteSpace(name)) leftovers.Add($"ชื่อ='{name}'");

            string calendar = ReadCalendarLeftover(tab);   // ตรวจทั้งรหัสและชื่อปฏิทิน (เดิมตรวจแค่ข้อความที่แสดง จึงผ่านทั้งที่ยังไม่ว่าง)
            if (!string.IsNullOrWhiteSpace(calendar)) leftovers.Add($"ปฏิทินวันหยุด {calendar}");

            var cell = tab.FindFirstDescendant(cf => cf.ByAutomationId(ShiftGridId))?.FindFirstDescendant(cf => cf.ByName(ShiftCellName));
            string shift = cell == null ? "" : GetCellText(cell);
            if (!string.IsNullOrWhiteSpace(shift) && shift != ShiftCellName) leftovers.Add($"รหัสกะงาน='{shift}'");

            if (leftovers.Count > 0)
                throw new Exception($"ล้างค่าไม่สำเร็จ ยังมีค่าค้างอยู่: {string.Join(", ", leftovers)}");
        }

        private static string GetCellText(AutomationElement cell)
        {
            try
            {
                if (cell.Patterns.Value.IsSupported) return cell.Patterns.Value.Pattern.Value.Value ?? "";
            }
            catch { }
            return cell.Name ?? "";
        }

        private AutomationElement? FindInPopup(string text)
        {
            int pid = _mainWindow.Properties.ProcessId.ValueOrDefault;
            foreach (var win in GetPopupWindows(pid))
            {
                var hit = win.FindAllDescendants().FirstOrDefault(el => MatchesText(el, text));
                if (hit != null) return hit;
            }
            return null;
        }

        /* เทียบทั้ง Name และ Value (DevExpress cell: Name = "<คอลัมน์> row N", Value = ค่าจริง) */
        private static bool MatchesText(AutomationElement el, string text)
        {
            try
            {
                if (el.Properties.IsOffscreen.ValueOrDefault) return false;

                if (!string.IsNullOrEmpty(el.Name) &&
                    string.Equals(el.Name.Trim(), text, StringComparison.OrdinalIgnoreCase)) return true;

                if (el.Patterns.Value.IsSupported &&
                    string.Equals(el.Patterns.Value.Pattern.Value.Value?.Trim(), text, StringComparison.OrdinalIgnoreCase)) return true;

                if (el.Patterns.LegacyIAccessible.IsSupported)
                {
                    var legacy = el.Patterns.LegacyIAccessible.Pattern;
                    if (string.Equals(legacy.Value.ValueOrDefault?.Trim(), text, StringComparison.OrdinalIgnoreCase)) return true;
                    if (string.Equals(legacy.Name.ValueOrDefault?.Trim(), text, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            catch { }
            return false;
        }

        /* หา popup ของ lookup (เช่น "กำหนดปฏิทินบริษัท", "ข้อมูลกะงาน")
           popup เป็น modal ลูกของฟอร์ม/หน้าต่างหลัก UIA จึงเก็บไว้ใต้ฟอร์ม ไม่ได้อยู่ใน desktop โดยตรง
           วิธี: หาปุ่ม Select ที่มองเห็นอยู่ ในหน้าต่างหลักและหน้าต่าง top-level ทั้งหมดของ process
           แล้วใช้ Window ที่เป็นแม่ของปุ่มนั้นเป็น popup */
        private IEnumerable<AutomationElement> GetPopupWindows(int pid)
        {
            var result = new List<AutomationElement>();

            var roots = new List<AutomationElement> { _mainWindow };
            try
            {
                roots.AddRange(_automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(pid))
                                          .Where(w => !w.Equals(_mainWindow)));
            }
            catch { }

            foreach (var root in roots)
            {
                List<AutomationElement> buttons;
                try
                {
                    buttons = root.FindAllDescendants(cf => cf.ByName("Select").Or(cf.ByName("เลือก")))
                                  .Where(b => !b.Properties.IsOffscreen.ValueOrDefault)
                                  .ToList();
                }
                catch { continue; }

                foreach (var b in buttons)
                {
                    var win = GetOwnerWindow(b) ?? root;
                    if (!result.Any(r => r.Equals(win))) result.Add(win);
                }
            }

            return result;
        }

        private static AutomationElement? GetOwnerWindow(AutomationElement el)
        {
            try
            {
                var cur = el.Parent;
                while (cur != null)
                {
                    if (cur.ControlType == ControlType.Window) return cur;
                    cur = cur.Parent;
                }
            }
            catch { }
            return null;
        }

        /* เขียนโครงสร้างหน้าต่างที่โปรแกรมมองเห็นลง log (ใช้ตอนหา popup ไม่เจอ) */
        private void DumpWindows()
        {
            try
            {
                int pid = _mainWindow.Properties.ProcessId.ValueOrDefault;
                var lines = new List<string>();

                void Add(string prefix, AutomationElement w)
                {
                    string id = "";
                    try { id = w.Properties.AutomationId.ValueOrDefault ?? ""; } catch { }
                    lines.Add($"{prefix}{w.ControlType} name='{w.Name}' id='{id}'");
                }

                foreach (var w in _automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(pid))) Add("[desktop] ", w);
                foreach (var w in _mainWindow.FindAllChildren()) Add("[main child] ", w);

                var form = FindOpenForm();
                if (form != null) foreach (var w in form.FindAllChildren()) Add("[form child] ", w);

                foreach (var b in _mainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                                             .Where(b => !b.Properties.IsOffscreen.ValueOrDefault).Take(60))
                    Add("[button] ", b);

                TestLogger.LogInfo(ScreenLogName, "หา popup ไม่เจอ โครงสร้างหน้าต่างที่เห็น:\n" + string.Join("\n", lines));
            }
            catch { }
        }

        private static bool IsFormWindow(AutomationElement w)
        {
            try { return w.Properties.AutomationId.ValueOrDefault == FormId; }
            catch { return false; }
        }

        /* ปิดฟอร์มทิ้งโดยไม่บันทึก ใช้เมื่อเคสพังกลางทาง ไม่ให้ฟอร์ม/popup ที่ค้างไปกระทบเคสถัดไป */
        private void AbortForm(Window? form)
        {
            try
            {
                int pid = _mainWindow.Properties.ProcessId.ValueOrDefault;
                if (GetPopupWindows(pid).Any()) CancelPopup();   // ปิด popup ก่อน (ถ้ามี)
                if (form == null) return;

                try { ClickCloseAndHandleDialog(form, "No", "AbortForm"); } catch { }
                WaitForClosed(() => form, 3000);
            }
            catch { }
        }

        private void CancelPopup()
        {
            try
            {
                int pid = _mainWindow.Properties.ProcessId.ValueOrDefault;
                var cancel = GetPopupWindows(pid)
                    .Select(w => w.FindFirstDescendant(cf => cf.ByName("Cancel").Or(cf.ByName("ยกเลิก"))))
                    .FirstOrDefault(b => b != null);
                if (cancel != null) cancel.Click();
                else Keyboard.Press(VirtualKeyShort.ESCAPE);
                Thread.Sleep(600);
            }
            catch { }
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
            if (texts.Any(t => t.Contains("รหัสตารางการทำงาน ห้ามซ้ำ"))) return "รหัสซ้ำ";
            if (texts.Any(t => t.Contains("ชื่อตารางการทำงาน ห้ามซ้ำ"))) return "ชื่อซ้ำ";
            return null;
        }

        private AutomationElement GetGeneralTab(Window form) =>
            Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(GeneralTabId)), 3000) ?? form;

        private Window? FindOpenForm() =>
            _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault() ??
            _automation.GetDesktop().FindFirstChild(cf => cf.ByAutomationId(FormId))?.AsWindow();


        // =========================================================================================
        // หมวดที่ 3: ส่วนการทำงานกลางที่ตายตัว (Common / Reusable Helpers)
        // =========================================================================================

        private readonly AutomationElement _mainWindow;
        private readonly UIA3Automation _automation;
        private int _totalSuccess = 0;
        private int _totalFail = 0;
        private const int TimeoutMs = 6000;

        public TemplateEmpScheInfoUC(AutomationElement mainWindow, UIA3Automation automation)
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
                    if (exists) { LogFailure(testName, $"'{code}' ลบข้อมูลไม่สำเร็จ"); return; }
                    LogSuccess(testName, $"'{code}' ถูกลบสำเร็จ");
                }
                else
                {
                    if (!exists) { LogFailure(testName, $"'{code}' ลบข้อมูลผิดพลาด"); return; }
                    LogSuccess(testName, $"'{code}' ข้อมูลยังไม่ถูกลบ");
                }
            }
            catch (Exception ex)
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

        /* กดบันทึก แล้วตรวจ Task Result ว่ามีข้อความ Error ใหม่หรือไม่ (ไม่ใช่แค่ข้อมูลซ้ำ)
           - เก็บข้อความที่มีอยู่ก่อนกด ไว้ตัดทิ้ง กันข้อความเก่าจากเคสก่อนหน้ามาทำให้ FAIL ผิด
           - เจอ Error → บันทึก FAIL พร้อมข้อความจริง แล้วปิดฟอร์มด้วย No */
        private bool TrySaveAndHandleDuplicate(Window form, string testName, string code, string saveBtn)
        {
            var before = ReadTaskResultTexts(form, 300);

            var btn = form.FindFirstDescendant(cf => cf.ByName(saveBtn)) ?? throw new Exception($"ไม่พบปุ่ม {saveBtn}");
            btn.Click();

            if (testName.Contains("Validation") || testName.Contains("Duplicate")) return true;

            string? error = null;
            WaitUntil(() =>
            {
                var fresh = ReadTaskResultTexts(form, 300).Where(t => !before.Contains(t)).ToList();
                error = DetectDuplicateMessage(fresh) is string dup ? $"ข้อมูลซ้ำ: {dup}" : DetectSaveError(fresh);
                return error != null;
            }, 2000);

            if (error == null) return true;

            LogFailure(testName, $"'{code}' บันทึกไม่สำเร็จ: {error}");
            try { ClickCloseAndHandleDialog(form, "No", $"{testName}_Close"); } catch { }
            return false;
        }

        /* อ่านข้อความทั้งหมดใน Task Result (ฟอร์มปิดไปแล้ว / ไม่มี panel = คืนชุดว่าง) */
        private HashSet<string> ReadTaskResultTexts(Window form, int timeoutMs)
        {
            try
            {
                if (!form.IsAvailable) return new HashSet<string>();
                var panel = FindTaskResultPanel(form, timeoutMs);
                return panel == null ? new HashSet<string>() : new HashSet<string>(GetPanelTexts(panel));
            }
            catch { return new HashSet<string>(); }
        }

        /* หาข้อความ Error ทั่วไปของโปรแกรม (ห้ามว่าง / ห้ามซ้ำ / ต้องมีอย่างน้อย ฯลฯ) */
        private static string? DetectSaveError(IEnumerable<string> texts) =>
            texts.FirstOrDefault(t => t.Contains("ไม่สามารถ") || t.Contains("ห้ามซ้ำ") || t.Contains("ต้องมีอย่างน้อย")
                                      || t.Contains("กรุณาป้อน") || t.Contains("ไม่ถูกต้อง") || t.Contains("ผิดพลาด"));

        /* ฟอร์มถูกเคลียร์แล้วหรือยัง (ใช้ยืนยันว่า Save and New บันทึกสำเร็จจริง) — หาฟอร์มใหม่ทุกครั้ง กัน element เก่าหมดอายุ */
        private bool IsFormCleared()
        {
            var el = FindOpenForm()?.FindFirstDescendant(cf => cf.ByAutomationId(FirstObjectID));
            return el != null && string.IsNullOrWhiteSpace(el.AsTextBox().Text);
        }

        /* สรุปผลของขั้น: PASS เฉพาะเมื่อเคสย่อยในขั้นนี้ไม่มี FAIL เลย */
        private void LogStageSummary(string testName, int failBefore, string passMsg)
        {
            int failed = _totalFail - failBefore;
            if (failed == 0) LogSuccess(testName, passMsg);
            else LogFailure(testName, $"มีเคสย่อยไม่ผ่าน {failed} รายการ (ดูรายการ FAIL ด้านบน)");
        }

        /* ให้ Test Method เอาไป Assert ได้ — ไม่งั้น Test Explorer จะขึ้นผ่านทุกครั้ง แม้ใน log มี FAIL */
        public int TotalSuccess => _totalSuccess;
        public int TotalFail => _totalFail;

        /* กด Close แล้วกด Yes -> ยังมีข้อความเตือนและฟอร์มไม่ปิด / Cancel -> ไม่ปิด / No -> ปิดหน้าจอ */
        private void RunCloseDialogSequence(Window form, string prefix, string[] expectedMsgs)
        {
            string ySub = $"{prefix}_Close_Yes";
            ClickCloseAndHandleDialog(form, "Yes", ySub);
            AssertTaskResult(form, expectedMsgs, ySub, $"พบข้อความเตือนครบ {expectedMsgs.Length} รายการ และฟอร์มยังเปิดอยู่");
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

        private static bool WaitUntil(Func<bool> condition, int timeoutMs = TimeoutMs, int pollMs = 100)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try { if (condition()) return true; } catch { }
                Thread.Sleep(pollMs);
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

        private T? Retry<T>(Func<T?> finder, int timeoutMs = TimeoutMs, int pollMs = 100) where T : class
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
                Thread.Sleep(pollMs);
            }
            return null;
        }
    }
}