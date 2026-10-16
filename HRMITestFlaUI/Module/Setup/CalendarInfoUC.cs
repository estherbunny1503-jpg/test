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

namespace HRMITestFlaUI.Pages.Setup
{
    public class CalendarInfoUC
    {
        // =========================================================================================
        // หมวดที่ 1: ข้อมูลการทดสอบทั้ง 5 ขั้น (Test Data)
        // =========================================================================================

        // --- Data Records สำหรับใช้งานในแต่ละขั้นตอน ---
        public sealed class DaysSelection
        {
            public bool Mon { get; set; } = true;
            public bool Tue { get; set; } = true;
            public bool Wed { get; set; } = true;
            public bool Thu { get; set; } = true;
            public bool Fri { get; set; } = true;
            public bool Sat { get; set; } = true;
            public bool Sun { get; set; } = true;
        }

        private sealed record GroupScheduleConfig(
            string DayType,
            string Title,
            string Detail,
            string StartDate,
            string EndDate,
            int RadioOptionIndex,
            string IntervalValue,
            DaysSelection? Days,
            bool SelectAllHolidays);

        private sealed record CalendarRecord(
            string Code,
            string NameTh,
            string NameEn,
            string Remark,
            List<GroupScheduleConfig> Schedules,
            string SaveAction);

        private sealed record ValidationItem(
            string TestCaseId,
            string Code,
            string NameTh,
            string SaveBtn,
            int Progress,
            string ProgressMsg,
            string[] ExpectedErrors);

        private sealed record CalendarEditItem(
            string TestCaseId,
            string ExistingCode,
            string NewCode,
            string NewNameTh,
            string NewNameEn,
            string SaveButtonName,
            bool NeedsExplicitClose,
            int Progress,
            string ProgressMsg);

        private sealed record DeleteItem(
            string TargetCode,
            string DialogBtn,
            bool ExpectDeleted,
            int Progress,
            string ProgressMsg);

        // [ขั้นที่ 1] ข้อมูลทดสอบการสร้างใหม่ (Add / Navigate)
        private static readonly CalendarRecord[] Step1_AddCases =
        {
            new(
                "CAL-TEST-01", "หยุดทุกวันอาทิตย์", "Day off every Sunday", "หยุดวันอาทิตย์",
                new List<GroupScheduleConfig>
                {
                    new(
                        "วันหยุด", "วันหยุด", "วันหยุด", "1/1/2569", "31/12/2569",
                        2, "1",
                        new DaysSelection { Mon = false, Tue = false, Wed = false, Thu = false, Fri = false, Sat = false, Sun = true },
                        true
                    )
                },
                "Save"),

            new(
                "CAL-TEST-02", "หยุุดทุกวันเสาร์", "Day off every Saturday", "หยุดวันเสาร์",
                new List<GroupScheduleConfig>
                {
                    new(
                        "วันหยุด", "วันหยุด", "วันหยุด", "1/1/2569", "31/12/2569",
                        2, "1",
                        new DaysSelection { Mon = false, Tue = false, Wed = false, Thu = false, Fri = false, Sat = true, Sun = false },
                        true
                    )
                },
                "Save and New"),

            new(
                "CAL-TEST-03", "ทำงานวัน จันทร์-ศุกร์", "Working days: Monday - Friday", "ทำงานวัน จันทร์-ศุกร์",
                new List<GroupScheduleConfig>
                {
                    new(
                        "วันทำงาน", "วันทำงาน", "วันทำงาน", "1/1/2569", "31/12/2569",
                        2, "5",
                        new DaysSelection { Mon = true, Tue = true, Wed = true, Thu = true, Fri = true, Sat = false, Sun = false },
                        true
                    )
                },
                "Save and New"),

            new(
                "CAL-TEST-04", "วันหยุดประจำปี", "Company Holidays", "หยุดประจำปี",
                new List<GroupScheduleConfig>
                {
                    new(
                        "วันหยุดประจำปี", "วันหยุดประจำปี", "วันหยุดประจำปี", "1/1/2569", "31/12/2569",
                        3, "28",
                        null,
                        true
                    )
                },
                "Save and New"),

            new(
                "CAL-TEST-05", "หยุุดทุกวันจันทร์", "Day off every Monday", "หยุดวันจันทร์",
                new List<GroupScheduleConfig>
                {
                    new(
                        "วันหยุด", "วันหยุด", "วันหยุด", "1/1/2569", "31/12/2569",
                        2, "1",
                        new DaysSelection { Mon = true, Tue = false, Wed = false, Thu = false, Fri = false, Sat = false, Sun = false },
                        true
                    )
                },
                "Save and New"),

            new(
                "CAL-TEST-06", "ปี 2569", "twenty-five sixty-nine", "ปฏิทินประจำปี 2569",
                new List<GroupScheduleConfig>
                {
                    new(
                        "วันทำงาน", "ทำงานปกติ", "ทำงานปกติ", "1/1/2569", "31/12/2569",
                        2, "1",
                        new DaysSelection { Mon = true, Tue = true, Wed = true, Thu = true, Fri = true, Sat = false, Sun = false },
                        true
                    ),
                    new(
                        "วันหยุด", "หยุด", "หยุด", "1/1/2569", "31/12/2569",
                        2, "1",
                        new DaysSelection { Mon = false, Tue = false, Wed = false, Thu = false, Fri = false, Sat = true, Sun = true },
                        true
                    )
                },
                "Save and Close"),

            new(
                "CAL-TEST-07", "ปี 2570", "twenty-five seventy", "ปฏิทินประจำปี 2570",
                new List<GroupScheduleConfig>
                {
                    new(
                        "วันทำงาน", "ทำงานปกติ", "ทำงานปกติ", "1/1/2570", "31/12/2570",
                        2, "1",
                        new DaysSelection { Mon = true, Tue = true, Wed = true, Thu = true, Fri = true, Sat = false, Sun = false },
                        true
                    ),
                    new(
                        "วันหยุด", "หยุด", "หยุด", "1/1/2570", "31/12/2570",
                        2, "1",
                        new DaysSelection { Mon = false, Tue = false, Wed = false, Thu = false, Fri = false, Sat = true, Sun = true },
                        true
                    )
                },
                "Save and Close")
        };

        // [ขั้นที่ 2] ข้อมูลทดสอบ Validation ตอนเพิ่ม (Add Validation)
        private static readonly ValidationItem[] Step2_AddValidationCases =
        {
            new("TestCase01_MissingCode", "", "วันหยุด", "Save", 33,
                "Validation 1/8: Missing Code & Details...",
                new[] { "รหัสปฏิทิน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล", "จะต้องกำหนดข้อมูลปฏิทินอย่างน้อย 1 รายการ" }),

            new("TestCase02_MissingName", "CAL-TEST-08", "", "Save and New", 36,
                "Validation 2/8: Missing Name (CAL-TEST-08)...",
                new[] { "ชื่อปฏิทิน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล", "จะต้องกำหนดข้อมูลปฏิทินอย่างน้อย 1 รายการ" }),

            new("TestCase03_MissingBoth", "", "", "Save and New", 39,
                "Validation 3/8: Missing Both Code & Name...",
                new[] { "รหัสปฏิทิน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล", "ชื่อปฏิทิน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล", "จะต้องกำหนดข้อมูลปฏิทินอย่างน้อย 1 รายการ" }),

            new("TestCase04_MissingCalendarDetails", "CAL-TEST-08", "วันหยุดทดสอบ", "Save and New", 42,
                "Validation 4/8: Missing Calendar Details...",
                new[] { "จะต้องกำหนดข้อมูลปฏิทินอย่างน้อย 1 รายการ" }),

            new("TestCase05_DuplicateCodeAndName", "CAL-TEST-07", "ปี 2570", "Save and New", 45,
                "Validation 5/8: Duplicate Code & Name Check...",
                new[] { "รหัสปฏิทิน ห้ามซ้ำ กรุณาป้อนข้อมูล", "ชื่อปฏิทิน ห้ามซ้ำ กรุณาป้อนข้อมูล" })
        };

        // [ขั้นที่ 3] ข้อมูลทดสอบการแก้ไขข้อมูล (Edit Records)
        private static readonly CalendarEditItem[] Step3_EditCases =
        {
            new(
                "TestCase06_EditRow1",
                "CAL-TEST-01", "CAL001",
                "หยุดวันอาทิตย์", "Sunday Off",
                "Save",
                true,
                55,
                "Editing Row 1 (CAL-TEST-01 -> CAL001)..."
                ),
            new(
                "TestCase07_EditRow2",
                "CAL-TEST-02",
                "CAL002",
                "หยุดวันเสาร์",
                "Saturday Off",
                "Save and Close",
                false,
                60,
                "Editing Row 2 (CAL-TEST-02 -> CAL002)..."
                )
        };

        // [ขั้นที่ 4] ข้อมูลทดสอบ Validation ตอนแก้ไข (Edit Validation)
        private const string EditTargetRow = "CAL002";
        private static readonly ValidationItem[] Step4_EditValidationCases =
        {
            new("TestCase08_EditValidation_MissingCode", "", "หยุดวันอาทิตย์", "Save", 68,
                "Edit Validation 1/7: Missing Code...",
                new[] { "รหัสปฏิทิน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล" }),

            new("TestCase08_EditValidation_MissingName", "CAL002", "", "Save and New", 70,
                "Edit Validation 2/7: Missing Name...",
                new[] { "ชื่อปฏิทิน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล" }),

            new("TestCase08_EditValidation_MissingBoth", "", "", "Save", 72,
                "Edit Validation 3/7: Missing Both...",
                new[] { "รหัสปฏิทิน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล", "ชื่อปฏิทิน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล" }),

            new("TestCase08_EditValidation_DuplicateCodeAndName", "CAL-TEST-03", "ทำงานวัน จันทร์-ศุกร์", "Save and New", 74,
                "Edit Validation 4/7: Duplicate Code & Name...",
                new[] { "รหัสปฏิทิน ห้ามซ้ำ กรุณาป้อนข้อมูล", "ชื่อปฏิทิน ห้ามซ้ำ กรุณาป้อนข้อมูล" })
        };

        // [ขั้นที่ 5] ข้อมูลทดสอบการลบข้อมูล (Delete Records)
        private static readonly DeleteItem[] Step5_DeleteCases =
        {
            new("CAL-TEST-05", "Yes", true, 80, "Testing Delete (CAL-TEST-05 - Yes Confirm)..."),
            new("CAL001", "No", false, 83, "Testing Delete (CAL001 First Row - No Action)...")
        };


        // =========================================================================================
        // หมวดที่ 2: ส่วนที่ขึ้นกับหน้าจอนั้นๆ (Screen-Specific Implementation)
        // **เมื่อเปลี่ยนไปทำหน้าจออื่น ต้องมาแก้ไขค่าและเมทอดในส่วนนี้ทั้งหมด**
        // =========================================================================================

        // 2.1 Locator & Mappings ประจำหน้าจอ
        private const string MenuName = "กำหนดปฏิทินบริษัท";
        private const string ScreenLogName = "CalendarInfoUC";
        private const string FormId = "CalendarInfoUC";
        private const string FormTitle = "กำหนดปฏิทินบริษัท";

        private const string IdTabHeaderGeneral = "devTabPgGeneral";
        private const string IdTabHeaderCalendar = "devTabPgCalendar";
        private const string TabGeneralName = "General";
        private const string TabCalendarName = "ปฏิทินทำงาน";

        private const string IdCalendarCode = "devTxtCalendarCode";
        private const string IdCalendarName = "devtxtCalendarName";
        private const string IdCalendarNameAlt = "devTxtCalendarName";
        private const string IdCalendarNameEng = "devTxtCalendarNameEng";
        private const string IdCalendarRemark = "devMemoCalendarRemark";
        private const string IdBtnWeekend = "devBtnWeekend";
        private const string IdChkSelectAllHolidays = "devChkSelectAllHolidays";

        /// <summary>
        /// ขั้นที่ 1: เปิดหน้าจอและกรอกข้อมูลสร้างใหม่
        /// </summary>
        public void NavigateToCalendarInfoUC()
        {
            TestLogger.LogInfo(ScreenLogName, "เริ่มทดสอบหน้าจอ Calendar Setup Info");
            TestProgressHelper.Report(12, $"Opening Setup -> {MenuName} Menu via Search...");

            const string stageName = "TestCase00_AddCalendarData";
            if (!TryOpenCalendarMenu(stageName)) return;

            Window? form = null;
            for (int i = 0; i < Step1_AddCases.Length; i++)
            {
                var item = Step1_AddCases[i];
                string testName = $"{stageName}_{item.Code}";
                int currentProgress = 13 + (i * 2);
                TestProgressHelper.Report(currentProgress, $"Adding Calendar ({i + 1}/{Step1_AddCases.Length}): {item.Code}...");

                try
                {
                    form ??= GetOrOpenCalendarForm();

                    SwitchToGeneralTab(form);
                    FillCalendarBasicFields(form, item.Code, item.NameTh, item.NameEn, item.Remark);

                    SwitchToCalendarTab(form);
                    foreach (var schedule in item.Schedules)
                    {
                        var btnWeekend = Retry(() =>
                            form.FindFirstDescendant(cf => cf.ByAutomationId(IdBtnWeekend))
                            ?? form.FindFirstDescendant(cf => cf.ByName("กำหนดปฏิทินแบบกลุ่ม")), 4000)
                            ?? throw new Exception("หาปุ่ม 'กำหนดปฏิทินแบบกลุ่ม' ไม่เจอ");

                        ClickElementByMouse(btnWeekend);

                        Window groupPopup = Retry(() => FindGroupSetupPopupWindow(form), 5000)
                            ?? throw new Exception("เปิด Pop-up 'กำหนดปฏิทินแบบกลุ่ม' ไม่สำเร็จ");

                        FillGroupCalendarPopup(groupPopup, schedule);

                        var btnOk = groupPopup.FindFirstDescendant(cf => cf.ByName("OK")).AsButton()
                                   ?? groupPopup.FindFirstDescendant(cf => cf.ByName("ตกลง")).AsButton();

                        if (btnOk != null) ClickElementByMouse(btnOk);
                        else Keyboard.Press(VirtualKeyShort.ENTER);

                        WaitForClosed(() => groupPopup);
                    }

                    string saveButtonName = item.SaveAction;
                    bool saveSuccess = TrySaveAndHandleDuplicate(form, testName, item.Code, saveButtonName);
                    if (!saveSuccess) { form = null; continue; }

                    if (saveButtonName == "Save")
                    {
                        ClickFormCloseButton(form);
                        WaitForClosed(() => FindOpenCalendarForm());
                        form = null;
                        RefreshGrid();
                        if (FindGridElement(item.Code) != null)
                            LogSuccessCounted(testName, $"'{item.Code}' เพิ่มสำเร็จ (พบในตารางหลัก)");
                        else
                            LogFailureCounted(testName, $"'{item.Code}' ไม่พบในตารางหลักหลังบันทึก");
                    }
                    else if (saveButtonName == "Save and Close")
                    {
                        WaitForClosed(() => FindOpenCalendarForm());
                        form = null;
                        RefreshGrid();
                        if (FindGridElement(item.Code) != null)
                            LogSuccessCounted(testName, $"'{item.Code}' เพิ่มสำเร็จ (พบในตารางหลัก)");
                        else
                            LogFailureCounted(testName, $"'{item.Code}' ไม่พบในตารางหลักหลังบันทึก");
                    }
                    else if (saveButtonName == "Save and New")
                    {
                        SwitchToGeneralTab(form);
                        WaitUntil(() =>
                        {
                            var txt = form.FindFirstDescendant(cf => cf.ByAutomationId(IdCalendarCode))?.AsTextBox();
                            return string.IsNullOrEmpty(txt?.Text);
                        }, 3000);
                        LogSuccessCounted(testName, $"'{item.Code}' บันทึกสำเร็จ (Save and New)");
                    }
                }
                catch (Exception ex)
                {
                    LogFailureCounted(testName, $"'{item.Code}' เพิ่มไม่สำเร็จ: {ex.Message}");
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
            const string stageName = "TestCase01-08_Validation";
            TestProgressHelper.Report(30, "Opening Calendar Form for Validation Tests...");

            Window form;
            try { form = OpenCalendarForm(); }
            catch (Exception ex)
            {
                LogFailureCounted(stageName, $"เปิดฟอร์มไม่สำเร็จ: {ex.Message}");
                return;
            }

            SwitchToGeneralTab(form);

            foreach (var testCase in Step2_AddValidationCases)
            {
                TestProgressHelper.Report(testCase.Progress, testCase.ProgressMsg);
                RunValidationStep(testCase.TestCaseId, () =>
                    FillAndSaveThenAssert(ref form, testCase.Code, testCase.NameTh, testCase.SaveBtn,
                        testCase.TestCaseId, testCase.ExpectedErrors));
            }

            const string dupCloseBase = "TestCase05_DuplicateCodeAndName";
            string[] dupMessages = { "รหัสปฏิทิน ห้ามซ้ำ กรุณาป้อนข้อมูล", "ชื่อปฏิทิน ห้ามซ้ำ กรุณาป้อนข้อมูล" };

            TestProgressHelper.Report(47, "Validation 6/8: Dialog Sequence (Close -> Yes)...");
            RunValidationStep($"{dupCloseBase}_Close_Yes", () =>
            {
                ClickFormCloseButton(form);
                HandleConfirmDialogIfPresent("Yes", form);
                AssertTaskResultMultipleWarningsAndFormOpen(form, dupMessages, $"{dupCloseBase}_Close_Yes");
            });

            TestProgressHelper.Report(49, "Validation 7/8: Dialog Sequence (Close -> Cancel)...");
            RunValidationStep($"{dupCloseBase}_Close_Cancel", () =>
            {
                ClickFormCloseButton(form);
                HandleConfirmDialogIfPresent("Cancel", form);
                AssertFormStillOpen(form, $"{dupCloseBase}_Close_Cancel");
            });

            TestProgressHelper.Report(50, "Validation 8/8: Dialog Sequence (Close -> No)...");
            RunValidationStep($"{dupCloseBase}_Close_No", () =>
            {
                ClickFormCloseButton(form);
                HandleConfirmDialogIfPresent("No", form);
                AssertFormClosed(form, $"{dupCloseBase}_Close_No");
            });
        }

        /// <summary>
        /// ขั้นที่ 3: เลือกแก้ไขรายการในตาราง
        /// </summary>
        public void RunEditTests()
        {
            TestProgressHelper.Report(52, "Starting Edit Calendar Operations...");
            RefreshGrid();

            foreach (var item in Step3_EditCases)
            {
                TestProgressHelper.Report(item.Progress, item.ProgressMsg);
                PerformEdit(item.TestCaseId, item.ExistingCode, item.NewCode, item.NewNameTh, item.NewNameEn,
                    item.SaveButtonName, item.NeedsExplicitClose);
            }
        }

        /// <summary>
        /// ขั้นที่ 4: รัน Validation ตอนแก้ไขข้อมูล
        /// </summary>
        public void RunEditValidationTests()
        {
            const string baseTestName = "TestCase08_EditValidation";
            TestProgressHelper.Report(66, $"Opening Edit Form for Validation ({EditTargetRow})...");

            Window form;
            try
            {
                RefreshGrid();
                form = ClickRowAndOpenEditForm(EditTargetRow, baseTestName);
            }
            catch (Exception ex)
            {
                LogFailureCounted(baseTestName, $"เปิดฟอร์ม Edit ไม่สำเร็จ: {ex.Message}");
                return;
            }

            SwitchToGeneralTab(form);

            foreach (var testCase in Step4_EditValidationCases)
            {
                TestProgressHelper.Report(testCase.Progress, testCase.ProgressMsg);
                RunValidationStep(testCase.TestCaseId, () =>
                    FillAndSaveThenAssert(ref form, testCase.Code, testCase.NameTh, testCase.SaveBtn,
                        testCase.TestCaseId, testCase.ExpectedErrors));
            }

            const string dupEditCloseBase = $"{baseTestName}_DuplicateCodeAndName";
            string[] editDupMessages = { "รหัสปฏิทิน ห้ามซ้ำ กรุณาป้อนข้อมูล", "ชื่อปฏิทิน ห้ามซ้ำ กรุณาป้อนข้อมูล" };

            TestProgressHelper.Report(75, "Edit Validation 5/7: Dialog (Close -> Yes)...");
            RunValidationStep($"{dupEditCloseBase}_Close_Yes", () =>
            {
                ClickFormCloseButton(form);
                HandleConfirmDialogIfPresent("Yes", form);
                AssertTaskResultMultipleWarningsAndFormOpen(form, editDupMessages, $"{dupEditCloseBase}_Close_Yes");
            });

            TestProgressHelper.Report(76, "Edit Validation 6/7: Dialog (Close -> Cancel)...");
            RunValidationStep($"{dupEditCloseBase}_Close_Cancel", () =>
            {
                ClickFormCloseButton(form);
                HandleConfirmDialogIfPresent("Cancel", form);
                AssertFormStillOpen(form, $"{dupEditCloseBase}_Close_Cancel");
            });

            TestProgressHelper.Report(77, "Edit Validation 7/7: Dialog (Close -> No)...");
            RunValidationStep($"{dupEditCloseBase}_Close_No", () =>
            {
                ClickFormCloseButton(form);
                HandleConfirmDialogIfPresent("No", form);
                AssertFormClosed(form, $"{dupEditCloseBase}_Close_No");
            });

            LogSuccessCounted(baseTestName, "รัน Validation Edit ครบทุกเคสแล้ว");
        }

        /// <summary>
        /// ขั้นที่ 5: ทดสอบการลบรายการในตาราง
        /// </summary>
        public void RunDeleteTests()
        {
            const string baseTestName = "TestCase09_Delete";
            RefreshGrid();

            foreach (var testCase in Step5_DeleteCases)
            {
                TestProgressHelper.Report(testCase.Progress, testCase.ProgressMsg);
                RunDeleteCaseByCode($"{baseTestName}_{testCase.TargetCode.Replace("-", "")}_{testCase.DialogBtn}",
                    testCase.TargetCode, confirmButton: testCase.DialogBtn, expectDeleted: testCase.ExpectDeleted);
            }

            LogSuccessCounted(baseTestName, "ทดสอบ Delete ครบทุกเคส");
            TestProgressHelper.Report(85, "Calendar UI Tests Complete. Ready for SQL Compare.");
            TestLogger.LogInfo(ScreenLogName, $"สรุปผลรวมทั้งหมด: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");
        }

        public void RunAllTestSteps()
        {
            NavigateToCalendarInfoUC();
            RunAllValidationTests();
            RunEditTests();
            RunEditValidationTests();
            RunDeleteTests();
        }

        // --- Custom Mapping & Control Logic ประจำหน้าจอ CalendarInfoUC ---
        private void FillCalendarBasicFields(AutomationElement context, string code, string nameTh, string nameEn, string remark)
        {
            FillTextBox(context, IdCalendarCode, code);
            FillTextBox(context, IdCalendarName, nameTh);
            FillTextBox(context, IdCalendarNameEng, nameEn);
            FillTextBox(context, IdCalendarRemark, remark);
        }

        private void SwitchToGeneralTab(Window form)
        {
            var tab = Retry(() =>
                form.FindFirstDescendant(cf => cf.ByAutomationId(IdTabHeaderGeneral))
                ?? form.FindFirstDescendant(cf => cf.ByName(TabGeneralName))
                ?? form.FindFirstDescendant(cf => cf.ByName("ข้อมูลทั่วไป"))
                ?? form.FindFirstDescendant(cf => cf.ByControlType(ControlType.TabItem).And(cf.ByName(TabGeneralName))), 3000);

            if (tab != null)
            {
                if (tab.Patterns.SelectionItem.IsSupported)
                {
                    try { tab.Patterns.SelectionItem.Pattern.Select(); } catch { ClickElementByMouse(tab); }
                }
                else
                {
                    ClickElementByMouse(tab);
                }
            }

            WaitUntil(() => form.FindFirstDescendant(cf => cf.ByAutomationId(IdCalendarCode)) != null, 2500);
        }

        private void SwitchToCalendarTab(Window form)
        {
            var tab = Retry(() =>
                form.FindFirstDescendant(cf => cf.ByAutomationId(IdTabHeaderCalendar))
                ?? form.FindFirstDescendant(cf => cf.ByName(TabCalendarName))
                ?? form.FindFirstDescendant(cf => cf.ByControlType(ControlType.TabItem).And(cf.ByName(TabCalendarName)))
                ?? form.FindFirstDescendant(cf => cf.ByControlType(ControlType.Text).And(cf.ByName(TabCalendarName)))
                ?? form.FindFirstDescendant(cf => cf.ByName("ปฏิทินทำงาน").Or(cf.ByName("ปฏิทิน"))), 3000);

            if (tab != null)
            {
                if (tab.Patterns.SelectionItem.IsSupported)
                {
                    try { tab.Patterns.SelectionItem.Pattern.Select(); } catch { ClickElementByMouse(tab); }
                }
                else
                {
                    ClickElementByMouse(tab);
                }
            }
            else
            {
                form.Focus();
                Keyboard.Press(VirtualKeyShort.CONTROL);
                Keyboard.Press(VirtualKeyShort.TAB);
                Keyboard.Release(VirtualKeyShort.TAB);
                Keyboard.Release(VirtualKeyShort.CONTROL);
            }

            WaitUntil(() => form.FindFirstDescendant(cf => cf.ByAutomationId(IdBtnWeekend)) != null ||
                            form.FindFirstDescendant(cf => cf.ByName("กำหนดปฏิทินแบบกลุ่ม")) != null, 3000);
        }

        private void FillGroupCalendarPopup(Window popup, GroupScheduleConfig r)
        {
            SelectDayTypeByTyping(popup, r.DayType);

            string titleValue = string.IsNullOrWhiteSpace(r.Title) ? "วันหยุด" : r.Title;
            ClickAndTypeTextByLabel(popup, "หัวข้อ", titleValue, offsetX: 150);

            string remarkValue = string.IsNullOrWhiteSpace(r.Detail) ? "รายละเอียดเพิ่มเติม" : r.Detail;
            ClickAndTypeTextByLabel(popup, "รายละเอียด", remarkValue, offsetX: 150);

            SetStartAndEndDatesDirectly(popup, r.StartDate, r.EndDate);

            if (r.RadioOptionIndex == 3)
            {
                var monthLabel = popup.FindFirstDescendant(cf => cf.ByName("ของทุกเดือน"));
                if (monthLabel != null && !monthLabel.BoundingRectangle.IsEmpty)
                {
                    var rect = monthLabel.BoundingRectangle;
                    int centerY = rect.Top + (rect.Height / 2);

                    int radioX = rect.Left - 260;
                    Mouse.Click(new Point(radioX, centerY));

                    Keyboard.Press(VirtualKeyShort.SPACE);
                    Keyboard.Release(VirtualKeyShort.SPACE);

                    int editX = rect.Left - 100;
                    Mouse.Click(new Point(editX, centerY));

                    string monthDaysValue = string.IsNullOrWhiteSpace(r.IntervalValue) ? "28" : r.IntervalValue;

                    Keyboard.Press(VirtualKeyShort.CONTROL);
                    Keyboard.Type("a");
                    Keyboard.Release(VirtualKeyShort.CONTROL);
                    Keyboard.Press(VirtualKeyShort.BACK);
                    Keyboard.Press(VirtualKeyShort.DELETE);

                    Keyboard.Type(monthDaysValue);
                }
            }
            else if (r.RadioOptionIndex == 2)
            {
                SelectRadioOptionByPosition(popup, 2);
                string interval = string.IsNullOrWhiteSpace(r.IntervalValue) ? "1" : r.IntervalValue;
                ClickAndTypeTextByLabel(popup, "สัปดาห์ โดยกำหนดให้ทุก", interval, offsetX: -60);

                if (r.Days != null)
                {
                    TickCheckboxByLabel(popup, "จันทร์", r.Days.Mon);
                    TickCheckboxByLabel(popup, "อังคาร", r.Days.Tue);
                    TickCheckboxByLabel(popup, "พุธ", r.Days.Wed);
                    TickCheckboxByLabel(popup, "พฤหัสบดี", r.Days.Thu);
                    TickCheckboxByLabel(popup, "ศุกร์", r.Days.Fri);
                    TickCheckboxByLabel(popup, "เสาร์", r.Days.Sat);
                    TickCheckboxByLabel(popup, "อาทิตย์", r.Days.Sun);
                }
            }
            else
            {
                SelectRadioOptionByPosition(popup, 1);
                string interval = string.IsNullOrWhiteSpace(r.IntervalValue) ? "1" : r.IntervalValue;
                ClickAndTypeTextByLabel(popup, "วัน", interval, offsetX: -60);
            }

            if (r.SelectAllHolidays)
            {
                TickCheckboxInPopup(popup, IdChkSelectAllHolidays, true, "เลือกทั้งหมด");
            }
        }

        private void SetStartAndEndDatesDirectly(Window popup, string startDate, string endDate)
        {
            var remarkLabel = popup.FindFirstDescendant(cf => cf.ByName("รายละเอียด"));
            if (remarkLabel != null && !remarkLabel.BoundingRectangle.IsEmpty)
            {
                var rRect = remarkLabel.BoundingRectangle;

                Mouse.Click(new Point((int)rRect.Right + 150, (int)rRect.Top + ((int)rRect.Height / 2)));

                Keyboard.Press(VirtualKeyShort.TAB);
                Keyboard.Release(VirtualKeyShort.TAB);

                TypeDevExpressDate(startDate);

                Keyboard.Press(VirtualKeyShort.TAB);
                Keyboard.Release(VirtualKeyShort.TAB);

                TypeDevExpressDate(endDate);
            }
        }

        private Window? FindGroupSetupPopupWindow(Window form)
        {
            return Retry(() =>
                form.ModalWindows.FirstOrDefault(w => w.Name.Contains("กำหนดปฏิทินแบบกลุ่ม"))
                ?? _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault(w => w.Name.Contains("กำหนดปฏิทินแบบกลุ่ม"))
                ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("กำหนดปฏิทินแบบกลุ่ม"))?.AsWindow(),
                timeoutMs: 4000);
        }

        private Window GetOrOpenCalendarForm() => Retry(FindOpenCalendarForm, 1500) ?? OpenCalendarForm();

        private Window OpenCalendarForm()
        {
            CloseAnyOpenModalForm();
            var newBtn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("New")), 3000)
                         ?? throw new Exception("หาปุ่ม New ไม่เจอ");

            ClickElementByMouse(newBtn);
            var form = Retry(FindOpenCalendarForm, 8000) ?? throw new Exception("หาฟอร์ม กำหนดปฏิทินบริษัท ไม่เจอ");
            WaitUntil(() => form.FindFirstDescendant(cf => cf.ByAutomationId(IdCalendarCode)) != null, 4000);
            return form;
        }

        private Window? FindOpenCalendarForm()
        {
            var modal = _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault(w =>
                w.AutomationId == FormId ||
                w.Name.Contains(FormTitle));

            if (modal != null) return modal;

            return _automation.GetDesktop().FindFirstChild(cf =>
                cf.ByAutomationId(FormId)
                .Or(cf.ByName(FormTitle)))?.AsWindow();
        }

        private Window ClickRowAndOpenEditForm(string codeToFind, string testName)
        {
            AutomationElement? row = Retry(() => FindGridElement(codeToFind), timeoutMs: 4000);
            if (row == null)
            {
                RefreshGrid();
                row = Retry(() => FindGridElement(codeToFind), timeoutMs: 4000)
                      ?? throw new Exception($"หาแถว '{codeToFind}' ไม่เจอ");
            }

            WaitUntil(() => { try { row.Focus(); return true; } catch { return false; } }, 1000);
            ClickElementByMouse(row);

            var editButton = Retry(() =>
            {
                var b = _mainWindow.FindFirstDescendant(cf => cf.ByName("Edit"));
                return (b != null && b.Properties.IsEnabled.ValueOrDefault) ? b : null;
            }, 3000) ?? throw new Exception("หาปุ่ม Edit ไม่เจอ");

            ClickElementByMouse(editButton);
            var form = Retry(FindOpenCalendarForm, 6000) ?? throw new Exception("หาฟอร์ม ไม่เจอ");
            WaitUntil(() => form.FindFirstDescendant(cf => cf.ByAutomationId(IdCalendarCode)) != null, 3000);
            return form;
        }

        private (string TestName, string ExistingCode, string NewCode, string NewName)? PerformEdit(
            string testName, string existingCode, string newCode, string newNameTh, string newNameEn,
            string saveButtonName, bool needsExplicitClose)
        {
            try
            {
                var form = ClickRowAndOpenEditForm(existingCode, testName);
                SwitchToGeneralTab(form);

                FillTextBox(form, IdCalendarCode, newCode);
                FillTextBox(form, IdCalendarName, newNameTh);
                FillTextBox(form, IdCalendarNameEng, newNameEn);

                ClickFormButton(form, saveButtonName);

                string? saveWarning = TryDetectAnyTaskResultWarning(form, timeoutMs: 1200);
                if (saveWarning != null)
                {
                    LogFailureCounted(testName, $"แก้ไข {existingCode} เป็น {newCode} ไม่สำเร็จ: {saveWarning}");
                    try { ClickCloseAndHandleDialog(form, "No", $"{testName}_Close"); } catch { }
                    return null;
                }

                if (needsExplicitClose)
                {
                    ClickFormCloseButton(form);
                    HandleConfirmDialogIfPresent("Yes", form);
                    WaitForClosed(() => FindOpenCalendarForm());
                }
                else
                {
                    WaitForClosed(() => FindOpenCalendarForm());
                }

                RefreshGrid();
                bool found = Retry(() => FindGridElement(newCode), timeoutMs: 3000) != null;
                if (found)
                    LogSuccessCounted(testName, $"แก้ไข '{existingCode}' เป็น '{newCode}' สำเร็จ (พบในตารางหลัก)");
                else
                    LogFailureCounted(testName, $"แก้ไข '{existingCode}' เป็น '{newCode}' แล้วแต่ไม่พบในตารางหลัก");

                return (testName, existingCode, newCode, newNameTh);
            }
            catch (Exception ex)
            {
                LogFailureCounted(testName, $"แก้ไข '{existingCode}' ไม่สำเร็จ: {ex.Message}");
                return null;
            }
        }

        private static void FillTextBox(AutomationElement context, string automationId, string value)
        {
            var el = context.FindFirstDescendant(cf => cf.ByAutomationId(automationId))
                  ?? (automationId == IdCalendarName ? context.FindFirstDescendant(cf => cf.ByAutomationId(IdCalendarNameAlt)) : null);

            if (el != null) SetTextDirectly(el, value);
        }

        private static void FillTextBoxVerified(AutomationElement context, string automationId, string value, int maxTry = 3)
        {
            for (int i = 0; i < maxTry; i++)
            {
                FillTextBox(context, automationId, value ?? "");

                var el = context.FindFirstDescendant(cf => cf.ByAutomationId(automationId))
                      ?? (automationId == IdCalendarName ? context.FindFirstDescendant(cf => cf.ByAutomationId(IdCalendarNameAlt)) : null);
                if (el == null) return;

                if (ReadText(el).Trim() == (value ?? "").Trim()) return;
            }
        }

        private static string ReadText(AutomationElement el)
        {
            try { if (el.Patterns.Value.IsSupported) return el.Patterns.Value.Pattern.Value.ValueOrDefault ?? ""; } catch { }
            try { return el.AsTextBox()?.Text ?? ""; } catch { }
            return "";
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

        private static readonly HashSet<string> IgnoredTaskResultTexts = new(StringComparer.OrdinalIgnoreCase)
        {
            "Static", "Task Result", "Header Panel", "Column Headers", "Exception Severity Pict",
            "Exception Severity Picture", "ExceptionSeverityToolBar", "Data Panel", "Dock Top",
            "0 Errors", "0 Informations", "More Information", "Description", "Severity",
            "Message", "Details", "Type", "Source", "Date", "Time", "Column1", ""
        };

        public CalendarInfoUC(AutomationElement mainWindow, UIA3Automation automation)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

        private bool TryOpenCalendarMenu(string stage)
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
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("treeListColumn row 22")), 4000)
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

        private void FillAndSaveThenAssert(ref Window form, string code, string name,
            string saveButtonName, string testName, params string[] expectedMessages)
        {
            if (form == null || !form.IsAvailable || (form.Properties.IsOffscreen.IsSupported && form.Properties.IsOffscreen.ValueOrDefault))
            {
                form = FindOpenCalendarForm() ?? OpenCalendarForm();
            }

            SwitchToGeneralTab(form);

            FillTextBoxVerified(form, IdCalendarCode, code);
            FillTextBoxVerified(form, IdCalendarName, name);

            var baseline = ReadTaskResultOnce(form);

            ClickFormButton(form, saveButtonName);

            Window formLocal = form;
            WaitUntil(() => !ReadTaskResultOnce(formLocal).OrderBy(x => x).SequenceEqual(baseline.OrderBy(x => x)), 2000);

            var pending = new HashSet<string>(expectedMessages);
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < 5000 && pending.Count > 0)
            {
                var currentTexts = ReadTaskResultOnce(form);
                foreach (var expected in pending.ToList())
                {
                    string cleanExpected = expected.Trim(' ', ';', '\r', '\n');
                    if (currentTexts.Any(m => m.Trim(' ', ';').Contains(cleanExpected) || cleanExpected.Contains(m.Trim(' ', ';'))))
                    {
                        pending.Remove(expected);
                    }
                }
                if (pending.Count == 0) break;
            }

            if (pending.Count > 0)
            {
                var lastSeen = ReadTaskResultOnce(form);
                string foundList = lastSeen.Count > 0 ? string.Join(" | ", lastSeen) : "(ไม่พบข้อความใดๆ ใน Task Result)";
                throw new Exception($"ไม่พบข้อความแจ้งเตือน: '{string.Join("', '", pending)}' (ข้อความที่เจอจริง: {foundList})");
            }

            string formattedList = string.Join(",", expectedMessages.Select(m => $"'{m}'"));
            LogSuccessCounted(testName, $"พบข้อความ: {formattedList}");

            var currentOpen = FindOpenCalendarForm();
            if (currentOpen != null)
            {
                form = currentOpen;
            }
        }

        private bool TrySaveAndHandleDuplicate(Window form, string testName, string code, string saveButtonName)
        {
            ClickFormButton(form, saveButtonName);

            if (testName.Contains("Validation")) return true;

            string? warningReason = TryDetectAnyTaskResultWarning(form, timeoutMs: 1500);
            if (warningReason == null) return true;

            LogFailureCounted(testName, $"'{code}' บันทึกไม่สำเร็จ: {warningReason}");
            try { ClickCloseAndHandleDialog(form, "No", $"{testName}_Close"); } catch { }
            return false;
        }

        private void RunDeleteCaseByCode(string testName, string codeToTarget, string confirmButton, bool expectDeleted)
        {
            try
            {
                RefreshGrid();

                var targetRow = Retry(() => FindGridElement(codeToTarget), timeoutMs: 3000)
                    ?? throw new Exception($"ไม่พบรายการ '{codeToTarget}' บน Grid");

                WaitUntil(() => { try { targetRow.Focus(); return true; } catch { return false; } }, 1000);
                ClickElementByMouse(targetRow);

                var deleteBtn = Retry(() =>
                {
                    var b = _mainWindow.FindFirstDescendant(cf => cf.ByName("Delete"));
                    return (b != null && b.Properties.IsEnabled.ValueOrDefault) ? b : null;
                }, timeoutMs: 3000) ?? throw new Exception("หาปุ่ม Delete ไม่เจอ");

                ClickElementByMouse(deleteBtn);

                HandleConfirmDialogIfPresent(confirmButton, _mainWindow.AsWindow()!);

                bool stillExists = Retry(() => FindGridElement(codeToTarget), timeoutMs: 3000) != null;

                if (expectDeleted && !stillExists)
                {
                    LogSuccessCounted(testName, $"'{codeToTarget}' ถูกลบสำเร็จ");
                }
                else if (!expectDeleted && stillExists)
                {
                    LogSuccessCounted(testName, $"'{codeToTarget}' ข้อมูลยังไม่ถูกลบ");
                }
                else
                {
                    LogFailureCounted(testName, $"ผลการลบ '{codeToTarget}' ไม่ตรงตามคาด (expectDeleted={expectDeleted}, stillExists={stillExists})");
                }
            }
            catch (Exception ex)
            {
                LogFailureCounted(testName, $"ลบ '{codeToTarget}' ไม่สำเร็จ: {ex.Message}");
            }
        }

        private static void SetTextDirectly(AutomationElement? element, string text)
        {
            if (element == null) return;

            try
            {
                var txt = element.AsTextBox();
                if (txt != null)
                {
                    txt.Focus();
                    txt.Text = text ?? "";
                }
            }
            catch { }

            var currentVal = element.Patterns.Value.IsSupported ? element.Patterns.Value.Pattern.Value.ValueOrDefault : "";
            if (!string.IsNullOrEmpty(text) && currentVal != text)
            {
                ClickElementByMouse(element);
                element.Focus();

                Keyboard.Press(VirtualKeyShort.CONTROL);
                Keyboard.Type("a");
                Keyboard.Release(VirtualKeyShort.CONTROL);
                Keyboard.Press(VirtualKeyShort.BACK);
                Keyboard.Press(VirtualKeyShort.DELETE);

                Keyboard.Type(text);
            }
            else if (string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(currentVal))
            {
                ClickElementByMouse(element);
                element.Focus();

                Keyboard.Press(VirtualKeyShort.CONTROL);
                Keyboard.Type("a");
                Keyboard.Release(VirtualKeyShort.CONTROL);
                Keyboard.Press(VirtualKeyShort.BACK);
                Keyboard.Press(VirtualKeyShort.DELETE);
            }

            Keyboard.Press(VirtualKeyShort.TAB);
            Keyboard.Release(VirtualKeyShort.TAB);
        }

        private static void TypeDevExpressDate(string dateText)
        {
            if (string.IsNullOrWhiteSpace(dateText)) return;

            Keyboard.Press(VirtualKeyShort.CONTROL);
            Keyboard.Type("a");
            Keyboard.Release(VirtualKeyShort.CONTROL);
            Keyboard.Press(VirtualKeyShort.BACK);
            Keyboard.Press(VirtualKeyShort.DELETE);

            Keyboard.Press(VirtualKeyShort.HOME);
            Keyboard.Release(VirtualKeyShort.HOME);

            Keyboard.Type(dateText);

            Keyboard.Press(VirtualKeyShort.ENTER);
            Keyboard.Release(VirtualKeyShort.ENTER);
        }

        private static void SelectDayTypeByTyping(Window popup, string text)
        {
            var lbl = popup.FindFirstDescendant(cf => cf.ByName("ประเภทวัน"));
            if (lbl != null)
            {
                var rect = lbl.BoundingRectangle;
                Mouse.Click(new Point(rect.Right + 150, rect.Top + (rect.Height / 2)));

                Keyboard.Type(text);
                Keyboard.Press(VirtualKeyShort.ENTER);
                Keyboard.Release(VirtualKeyShort.ENTER);
            }
        }

        private static void SelectRadioOptionByPosition(Window popup, int targetOptionIndex)
        {
            string targetAnchorText = targetOptionIndex switch
            {
                1 => "วัน",
                2 => "สัปดาห์ โดยกำหนดให้ทุก",
                3 => "ของทุกเดือน",
                _ => "สัปดาห์ โดยกำหนดให้ทุก"
            };

            var targetLabel = popup.FindFirstDescendant(cf => cf.ByName(targetAnchorText));

            if (targetLabel != null && !targetLabel.BoundingRectangle.IsEmpty)
            {
                var rect = targetLabel.BoundingRectangle;
                int clickY = rect.Top + (rect.Height / 2);

                int clickX = targetOptionIndex switch
                {
                    1 => rect.Left - 130,
                    2 => rect.Left - 180,
                    3 => rect.Left - 260,
                    _ => rect.Left - 180
                };

                Mouse.Click(new Point(clickX, clickY));
            }
        }

        private static void TickCheckboxByLabel(Window popup, string dayName, bool targetState)
        {
            var chk = popup.FindFirstDescendant(cf => cf.ByName(dayName))
                   ?? popup.FindFirstDescendant(cf => cf.ByControlType(ControlType.CheckBox).And(cf.ByName(dayName)));

            if (chk == null) return;

            bool currentState = chk.Patterns.Toggle.IsSupported
                ? chk.Patterns.Toggle.Pattern.ToggleState.Value == ToggleState.On
                : (chk.AsCheckBox()?.IsChecked ?? false);

            if (currentState != targetState)
            {
                ClickElementByMouse(chk);
            }
        }

        private static void TickCheckboxInPopup(Window popup, string automationId, bool targetState, string fallbackName)
        {
            var chk = popup.FindFirstDescendant(cf => cf.ByAutomationId(automationId))
                   ?? popup.FindFirstDescendant(cf => cf.ByName(fallbackName));
            if (chk == null) return;

            bool currentState = chk.Patterns.Toggle.IsSupported
                ? chk.Patterns.Toggle.Pattern.ToggleState.Value == ToggleState.On
                : (chk.AsCheckBox()?.IsChecked ?? false);

            if (currentState != targetState)
            {
                ClickElementByMouse(chk);
            }
        }

        private static void ClickAndTypeTextByLabel(AutomationElement parent, string labelText, string textToType, int offsetX = 100, int offsetY = 0)
        {
            var label = parent.FindFirstDescendant(cf => cf.ByName(labelText));
            if (label == null) return;

            var rect = label.BoundingRectangle;
            if (rect.IsEmpty) return;

            int clickX = rect.Right + offsetX;
            int clickY = rect.Top + (rect.Height / 2) + offsetY;

            Mouse.Click(new Point(clickX, clickY));

            Keyboard.Press(VirtualKeyShort.CONTROL);
            Keyboard.Type("a");
            Keyboard.Release(VirtualKeyShort.CONTROL);
            Keyboard.Press(VirtualKeyShort.BACK);
            Keyboard.Press(VirtualKeyShort.DELETE);

            Keyboard.Type(textToType);
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

        private static void ClickFormButton(Window form, string buttonName, int timeoutMs = 3000)
        {
            var button = Retry(() => form.FindFirstDescendant(cf => cf.ByName(buttonName)), timeoutMs: timeoutMs)
                ?? throw new Exception($"หาปุ่ม '{buttonName}' ไม่เจอ");
            ClickElementByMouse(button);
        }

        private static void ClickFormCloseButton(Window form)
        {
            var closeBtn = form.FindAllDescendants(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("Close")))
                               .FirstOrDefault(b => b.Properties.IsEnabled.ValueOrDefault)
                           ?? form.FindFirstDescendant(cf => cf.ByName("Close"));

            if (closeBtn == null) throw new Exception("หาปุ่ม Close บนฟอร์มหลักไม่เจอ");
            ClickElementByMouse(closeBtn);
        }

        private List<string> ReadTaskResultOnce(Window form)
        {
            var messages = new List<string>();

            var taskResult = _mainWindow.FindFirstDescendant(cf => cf.ByName("Task Result"))
                          ?? form.FindFirstDescendant(cf => cf.ByName("Task Result"))
                          ?? _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("dockPanelTaskResult"))
                          ?? form.FindFirstDescendant(cf => cf.ByAutomationId("dockPanelTaskResult"));

            if (taskResult == null ||
                (taskResult.Properties.IsOffscreen.IsSupported && taskResult.Properties.IsOffscreen.ValueOrDefault) ||
                taskResult.BoundingRectangle.IsEmpty)
                return messages;

            try
            {
                foreach (var el in taskResult.FindAllDescendants())
                {
                    if (!string.IsNullOrEmpty(el.Name) && ContainsThai(el.Name) && !IgnoredTaskResultTexts.Contains(el.Name.Trim()))
                        messages.Add(el.Name.Trim());

                    if (el.Patterns.Value.IsSupported && !string.IsNullOrEmpty(el.Patterns.Value.Pattern.Value.Value))
                    {
                        string v = el.Patterns.Value.Pattern.Value.Value.Trim();
                        if (ContainsThai(v) && !IgnoredTaskResultTexts.Contains(v))
                            messages.Add(v);
                    }
                }
            }
            catch { }

            return messages.Distinct().ToList();
        }

        private string? TryDetectAnyTaskResultWarning(Window form, int timeoutMs = 1500)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                var m = ReadTaskResultOnce(form);
                if (m.Count > 0) return m.FirstOrDefault();
            }
            return null;
        }

        private static bool ContainsThai(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text)
            {
                if (c >= '\u0E00' && c <= '\u0E7F') return true;
            }
            return false;
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

        private void ClickCloseAndHandleDialog(Window form, string dialogChoice, string testName)
        {
            ClickFormCloseButton(form);
            HandleConfirmDialogIfPresent(dialogChoice, form);
        }

        private void HandleConfirmDialogIfPresent(string choice, Window form)
        {
            var dialog = Retry(() =>
                form.ModalWindows.FirstOrDefault()
                ?? form.FindFirstDescendant(cf => cf.ByControlType(ControlType.Window))?.AsWindow()
                ?? _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault()
                ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("คำเตือน"))?.AsWindow(),
                timeoutMs: 3000);

            if (dialog != null)
            {
                var btn = FindButtonByLabel(dialog, choice);
                if (btn != null)
                {
                    ClickElementByMouse(btn);
                    WaitForClosed(() => dialog);
                    return;
                }

                dialog.Focus();
                if (choice.Equals("No", StringComparison.OrdinalIgnoreCase))
                {
                    Keyboard.Type("n");
                    Keyboard.Press(VirtualKeyShort.ESCAPE);
                }
                else if (choice.Equals("Yes", StringComparison.OrdinalIgnoreCase))
                {
                    Keyboard.Type("y");
                    Keyboard.Press(VirtualKeyShort.ENTER);
                }
                else
                {
                    Keyboard.Press(VirtualKeyShort.ESCAPE);
                }

                WaitForClosed(() => dialog);
            }
        }

        private static AutomationElement? FindButtonByLabel(AutomationElement scope, string label)
        {
            return scope.FindFirstDescendant(cf => cf.ByName(label))
                ?? scope.FindFirstDescendant(cf => cf.ByName("&" + label))
                ?? scope.FindFirstDescendant(cf => cf.ByName("ไม่ใช่"))
                ?? scope.FindFirstDescendant(cf => cf.ByName("ใช่"))
                ?? scope.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                        .FirstOrDefault(b => (b.Name ?? "").Replace("&", "").Trim()
                            .Equals(label, StringComparison.OrdinalIgnoreCase));
        }

        private void AssertTaskResultMultipleWarningsAndFormOpen(Window form, string[] expectedMessages, string testName)
        {
            var pending = new HashSet<string>(expectedMessages);
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < 5000 && pending.Count > 0)
            {
                var currentTexts = ReadTaskResultOnce(form);
                foreach (var expected in pending.ToList())
                {
                    string cleanExpected = expected.Trim(' ', ';', '\r', '\n');
                    if (currentTexts.Any(m => m.Trim(' ', ';').Contains(cleanExpected) || cleanExpected.Contains(m.Trim(' ', ';'))))
                    {
                        pending.Remove(expected);
                    }
                }
                if (pending.Count == 0) break;
            }

            bool isOpen = WaitUntil(() => !form.Properties.IsOffscreen.ValueOrDefault && form.IsAvailable, 2000);
            if (pending.Count > 0 || !isOpen)
            {
                var lastSeen = ReadTaskResultOnce(form);
                string foundList = lastSeen.Count > 0 ? string.Join(" | ", lastSeen) : "(ไม่พบข้อความใดๆ ใน Task Result)";
                throw new Exception($"ตรวจสอบไม่ผ่าน:'{string.Join("', '", expectedMessages)}' (พบจริง: {foundList}, ฟอร์มเปิดอยู่: {isOpen})");
            }

            string formattedList = string.Join(",", expectedMessages.Select(m => $"'{m}'"));
            LogSuccessCounted(testName, $"พบข้อความ: {formattedList} และ ฟอร์มถูกเปิดอยู่");
        }

        private void AssertFormStillOpen(Window form, string testName)
        {
            bool isOpen = WaitUntil(() => !form.Properties.IsOffscreen.ValueOrDefault && form.IsAvailable, 2000);
            if (isOpen) LogSuccessCounted(testName, "ฟอร์มถูกเปิดอยู่");
            else LogFailureCounted(testName, "ฟอร์มถูกปิด");
        }

        private void AssertFormClosed(Window form, string testName)
        {
            bool closed = WaitForClosed(() => FindOpenCalendarForm(), 3000);
            if (closed) LogSuccessCounted(testName, "ฟอร์มถูกปิด");
            else LogFailureCounted(testName, "ปิดฟอร์มไม่สำเร็จ");
        }

        private AutomationElement? FindGridElement(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return null;

            return _mainWindow.FindAllDescendants().FirstOrDefault(el =>
                (!string.IsNullOrEmpty(el.Name) && el.Name.Trim().IndexOf(val, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (el.Patterns.Value.IsSupported && el.Patterns.Value.Pattern.Value.Value != null &&
                 el.Patterns.Value.Pattern.Value.Value.Trim().IndexOf(val, StringComparison.OrdinalIgnoreCase) >= 0));
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

        private void RunValidationStep(string testName, Action stepAction)
        {
            try { stepAction(); }
            catch (Exception ex) { LogFailureCounted(testName, $"ไม่ผ่าน: {ex.Message}"); }
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
            }
            return null;
        }
    }
}