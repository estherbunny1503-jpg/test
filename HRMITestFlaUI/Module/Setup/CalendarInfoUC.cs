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

namespace HRMITestFlaUI.Pages.Setup
{
    public class CalendarInfoUC
    {
        // ── ตัวแปรสำหรับเก็บออบเจ็กต์หน้าต่างหลัก, ระบบ Automation และตัวนับผลการทดสอบ (Success/Fail) ──
        private readonly AutomationElement _mainWindow;
        private readonly UIA3Automation _automation;

        private int _totalSuccess = 0;
        private int _totalFail = 0;

        // ── ค่าคงที่สำหรับการตั้งค่า Timeout และข้อมูลการระบุตัวตนของฟอร์ม ──
        private const int TimeoutMs = 6000;
        private const string FormId = "CalendarInfoUC";
        private const string FormTitle = "กำหนดปฏิทินบริษัท";

        // ── ค่าคงที่ Automation ID และชื่อของคอนโทรลบนหน้าจอ ──
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

        /* Constructor สำหรับรับค่า MainWindow และ Automation Instance เข้ามาใช้งานภายในคลาส */
        public CalendarInfoUC(AutomationElement mainWindow, UIA3Automation automation)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

        // ── โครงสร้างข้อมูลจำลอง (Data Models) สำหรับใช้งานในแบบทดสอบ ──
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

        // -------------------------------------------------------------
        // * Navigation & Search System *
        // -------------------------------------------------------------
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

                // เช็คโดยตรงว่าหน้าจอแสดงปุ่ม New พร้อมใช้งาน
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
        // * Tab Navigation Helpers *
        // -------------------------------------------------------------
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

        // -------------------------------------------------------------
        // * ข้อ 1. ทดสอบการ New เพิ่มข้อมูล 7 รายการ *
        // -------------------------------------------------------------
        public void NavigateToCalendarInfoUC()
        {
            TestLogger.LogInfo("CalendarInfoUC", "เริ่มทดสอบหน้าจอ Calendar Setup Info");
            TestProgressHelper.Report(12, "Opening Setup -> Calendar Info Menu via Search...");

            string stageName = "TestCase00_AddCalendarData";
            if (!TryOpenCalendarMenu(stageName)) return;

            var items = BuildNewTestRecords();
            Window? form = null;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                string testName = $"{stageName}_{item.Code}";
                int currentProgress = 13 + (i * 2);
                TestProgressHelper.Report(currentProgress, $"Adding Calendar ({i + 1}/{items.Count}): {item.Code}...");

                try
                {
                    form ??= GetOrOpenCalendarForm();

                    // 1. กรอกข้อมูลทั่วไป
                    SwitchToGeneralTab(form);
                    FillCalendarBasicFields(form, item.Code, item.NameTh, item.NameEn, item.Remark);

                    // 2. สลับไปแท็บปฏิทินทำงานเพื่อตั้งค่ากำหนดวันแบบกลุ่ม
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

                    // 3. บันทึกข้อมูลตามลักษณะของแต่ละรายการ
                    string saveButtonName = item.SaveAction;
                    bool saveSuccess = TrySaveAndHandleDuplicate(form, testName, item.Code, saveButtonName);
                    if (!saveSuccess) { form = null; continue; }

                    // 4. จัดการสถานะฟอร์มตามลักษณะปุ่มบันทึก
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

        private static List<CalendarRecord> BuildNewTestRecords() => new()
        {
            new CalendarRecord(
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

            new CalendarRecord(
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

            new CalendarRecord(
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

            new CalendarRecord(
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

            new CalendarRecord(
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

            new CalendarRecord(
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

            new CalendarRecord(
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

        // -------------------------------------------------------------
        // * ข้อ 2. ทดสอบ แจ้งเตือนต่างๆ เมื่อ New *
        // -------------------------------------------------------------
        public void RunAllValidationTests()
        {
            string stageName = "TestCase01-08_Validation";
            TestProgressHelper.Report(30, "Opening Calendar Form for Validation Tests...");

            Window form;
            try { form = OpenCalendarForm(); }
            catch (Exception ex)
            {
                LogFailureCounted(stageName, $"เปิดฟอร์มไม่สำเร็จ: {ex.Message}");
                return;
            }

            SwitchToGeneralTab(form);

            // Validation 1
            TestProgressHelper.Report(33, "Validation 1/8: Missing Code & Details...");
            RunValidationStep("TestCase01_MissingCode", () =>
                FillAndSaveThenAssert(ref form, "", "วันหยุด", "Save",
                    "TestCase01_MissingCode",
                    "รหัสปฏิทิน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล",
                    "จะต้องกำหนดข้อมูลปฏิทินอย่างน้อย 1 รายการ"));

            // Validation 2
            TestProgressHelper.Report(36, "Validation 2/8: Missing Name (CAL-TEST-08)...");
            RunValidationStep("TestCase02_MissingName", () =>
                FillAndSaveThenAssert(ref form, "CAL-TEST-08", "", "Save and New",
                    "TestCase02_MissingName",
                    "ชื่อปฏิทิน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล",
                    "จะต้องกำหนดข้อมูลปฏิทินอย่างน้อย 1 รายการ"));

            // Validation 3
            TestProgressHelper.Report(39, "Validation 3/8: Missing Both Code & Name...");
            RunValidationStep("TestCase03_MissingBoth", () =>
                FillAndSaveThenAssert(ref form, "", "", "Save and New",
                    "TestCase03_MissingBoth",
                    "รหัสปฏิทิน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล",
                    "ชื่อปฏิทิน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล",
                    "จะต้องกำหนดข้อมูลปฏิทินอย่างน้อย 1 รายการ"));

            // Validation 4
            TestProgressHelper.Report(42, "Validation 4/8: Missing Calendar Details...");
            RunValidationStep("TestCase04_MissingCalendarDetails", () =>
                FillAndSaveThenAssert(ref form, "CAL-TEST-08", "วันหยุดทดสอบ", "Save and New",
                    "TestCase04_MissingCalendarDetails",
                    "จะต้องกำหนดข้อมูลปฏิทินอย่างน้อย 1 รายการ"));

            // Validation 5
            TestProgressHelper.Report(45, "Validation 5/8: Duplicate Code & Name Check...");
            RunValidationStep("TestCase05_DuplicateCodeAndName", () =>
                FillAndSaveThenAssert(ref form, "CAL-TEST-07", "ปี 2570", "Save and New",
                    "TestCase05_DuplicateCodeAndName",
                    "รหัสปฏิทิน ห้ามซ้ำ กรุณาป้อนข้อมูล",
                    "ชื่อปฏิทิน ห้ามซ้ำ กรุณาป้อนข้อมูล"));

            // Validation 6
            TestProgressHelper.Report(47, "Validation 6/8: Dialog Sequence (Close -> Yes)...");
            RunValidationStep("TestCase05_DuplicateCodeAndName_Close_Yes", () =>
            {
                ClickFormCloseButton(form);
                HandleConfirmDialogIfPresent("Yes", form);

                AssertTaskResultMultipleWarningsAndFormOpen(form, new[]
                {
                    "รหัสปฏิทิน ห้ามซ้ำ กรุณาป้อนข้อมูล",
                    "ชื่อปฏิทิน ห้ามซ้ำ กรุณาป้อนข้อมูล"
                }, "TestCase05_DuplicateCodeAndName_Close_Yes");
            });

            // Validation 7
            TestProgressHelper.Report(49, "Validation 7/8: Dialog Sequence (Close -> Cancel)...");
            RunValidationStep("TestCase05_DuplicateCodeAndName_Close_Cancel", () =>
            {
                ClickFormCloseButton(form);
                HandleConfirmDialogIfPresent("Cancel", form);

                AssertFormStillOpen(form, "TestCase05_DuplicateCodeAndName_Close_Cancel");
            });

            // Validation 8
            TestProgressHelper.Report(50, "Validation 8/8: Dialog Sequence (Close -> No)...");
            RunValidationStep("TestCase05_DuplicateCodeAndName_Close_No", () =>
            {
                ClickFormCloseButton(form);
                HandleConfirmDialogIfPresent("No", form);

                AssertFormClosed(form, "TestCase05_DuplicateCodeAndName_Close_No");
            });
        }

        // -------------------------------------------------------------
        // * ข้อ 3. ทดสอบ Edit *
        // -------------------------------------------------------------
        public void RunEditTests()
        {
            TestProgressHelper.Report(52, "Starting Edit Calendar Operations...");
            var results = new List<(string TestName, string ExistingCode, string NewCode, string NewName)>();

            RefreshGrid();

            // แก้ไขแถวที่ 1: CAL-TEST-01 -> CAL001
            TestProgressHelper.Report(55, "Editing Row 1 (CAL-TEST-01 -> CAL001)...");
            TryAdd(results, PerformEdit("TestCase06_EditRow1", existingCode: "CAL-TEST-01",
                newCode: "CAL001", newNameTh: "หยุดวันอาทิตย์", newNameEn: "Sunday Off",
                saveButtonName: "Save", needsExplicitClose: true));

            // แก้ไขแถวที่ 2: CAL-TEST-02 -> CAL002
            TestProgressHelper.Report(60, "Editing Row 2 (CAL-TEST-02 -> CAL002)...");
            TryAdd(results, PerformEdit("TestCase07_EditRow2", existingCode: "CAL-TEST-02",
                newCode: "CAL002", newNameTh: "หยุดวันเสาร์", newNameEn: "Saturday Off",
                saveButtonName: "Save and Close", needsExplicitClose: false));
        }

        // -------------------------------------------------------------
        // * ข้อ 4. ทดสอบ การแจ้งเตือน Edit *
        // -------------------------------------------------------------
        public void RunEditValidationTests()
        {
            string baseTestName = "TestCase08_EditValidation";
            TestProgressHelper.Report(66, "Opening Edit Form for Validation (CAL002)...");

            Window form;
            try
            {
                RefreshGrid();
                form = TryClickRowAndOpenEditForm("CAL002", baseTestName) ?? ClickRowAndOpenEditForm("CAL-TEST-02", baseTestName);
            }
            catch (Exception ex)
            {
                LogFailureCounted(baseTestName, $"เปิดฟอร์ม Edit ไม่สำเร็จ: {ex.Message}");
                return;
            }

            SwitchToGeneralTab(form);

            // Edit Validation 1: ลบรหัสออก ใส่ชื่อ "หยุดวันอาทิตย์" -> กด Save
            TestProgressHelper.Report(68, "Edit Validation 1/7: Missing Code...");
            RunValidationStep($"{baseTestName}_MissingCode", () =>
                FillAndSaveThenAssert(ref form, "", "หยุดวันอาทิตย์", "Save",
                    $"{baseTestName}_MissingCode", "รหัสปฏิทิน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล"));

            // Edit Validation 2: ใส่รหัส CAL002 ลบชื่อออก -> กด Save and New
            TestProgressHelper.Report(70, "Edit Validation 2/7: Missing Name...");
            RunValidationStep($"{baseTestName}_MissingName", () =>
                FillAndSaveThenAssert(ref form, "CAL002", "", "Save and New",
                    $"{baseTestName}_MissingName", "ชื่อปฏิทิน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล"));

            // Edit Validation 3: ลบทั้งคู่ -> กด Save
            TestProgressHelper.Report(72, "Edit Validation 3/7: Missing Both...");
            RunValidationStep($"{baseTestName}_MissingBoth", () =>
                FillAndSaveThenAssert(ref form, "", "", "Save",
                    $"{baseTestName}_MissingBoth",
                    "รหัสปฏิทิน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล",
                    "ชื่อปฏิทิน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล"));

            // Edit Validation 4: รหัสซ้ำ CAL-TEST-03, ชื่อซ้ำ ทำงานวัน จันทร์-ศุกร์ -> กด Save and New
            TestProgressHelper.Report(74, "Edit Validation 4/7: Duplicate Code & Name...");
            RunValidationStep($"{baseTestName}_DuplicateCodeAndName", () =>
                FillAndSaveThenAssert(ref form, "CAL-TEST-03", "ทำงานวัน จันทร์-ศุกร์", "Save and New",
                    $"{baseTestName}_DuplicateCodeAndName",
                    "รหัสปฏิทิน ห้ามซ้ำ กรุณาป้อนข้อมูล",
                    "ชื่อปฏิทิน ห้ามซ้ำ กรุณาป้อนข้อมูล"));

            // Edit Validation 5: Close -> Yes
            TestProgressHelper.Report(75, "Edit Validation 5/7: Dialog (Close -> Yes)...");
            RunValidationStep($"{baseTestName}_DuplicateCodeAndName_Close_Yes", () =>
            {
                ClickFormCloseButton(form);
                HandleConfirmDialogIfPresent("Yes", form);

                AssertTaskResultMultipleWarningsAndFormOpen(form, new[]
                {
                    "รหัสปฏิทิน ห้ามซ้ำ กรุณาป้อนข้อมูล",
                    "ชื่อปฏิทิน ห้ามซ้ำ กรุณาป้อนข้อมูล"
                }, $"{baseTestName}_DuplicateCodeAndName_Close_Yes");
            });

            // Edit Validation 6: Close -> Cancel
            TestProgressHelper.Report(76, "Edit Validation 6/7: Dialog (Close -> Cancel)...");
            RunValidationStep($"{baseTestName}_DuplicateCodeAndName_Close_Cancel", () =>
            {
                ClickFormCloseButton(form);
                HandleConfirmDialogIfPresent("Cancel", form);

                AssertFormStillOpen(form, $"{baseTestName}_DuplicateCodeAndName_Close_Cancel");
            });

            // Edit Validation 7: Close -> No
            TestProgressHelper.Report(77, "Edit Validation 7/7: Dialog (Close -> No)...");
            RunValidationStep($"{baseTestName}_DuplicateCodeAndName_Close_No", () =>
            {
                ClickFormCloseButton(form);
                HandleConfirmDialogIfPresent("No", form);

                AssertFormClosed(form, $"{baseTestName}_DuplicateCodeAndName_Close_No");
            });

            LogSuccessCounted(baseTestName, "รัน Validation Edit ครบทุกเคสแล้ว");
        }

        // -------------------------------------------------------------
        // * ข้อ 5. ทดสอบ Delete *
        // -------------------------------------------------------------
        public void RunDeleteTests()
        {
            string baseTestName = "TestCase09_Delete";

            RefreshGrid();

            // เคสที่ 1: ลบรายการ CAL-TEST-05 -> Yes
            TestProgressHelper.Report(80, "Testing Delete (CAL-TEST-05 - Yes Confirm)...");
            RunDeleteCaseByCode($"{baseTestName}_CALTEST05_Yes", "CAL-TEST-05", confirmButton: "Yes", expectDeleted: true);

            // เคสที่ 2: ลบรายการแรก (CAL001 หรือ CAL-TEST-01) -> No
            string firstRowCode = FindGridElement("CAL001") != null ? "CAL001" : "CAL-TEST-01";
            TestProgressHelper.Report(83, $"Testing Delete ({firstRowCode} First Row - No Action)...");
            RunDeleteCaseByCode($"{baseTestName}_{firstRowCode.Replace("-", "")}_No", firstRowCode, confirmButton: "No", expectDeleted: false);

            LogSuccessCounted(baseTestName, "ทดสอบ Delete ครบทุกเคส");
            TestProgressHelper.Report(85, "Calendar UI Tests Complete. Ready for SQL Compare.");
            TestLogger.LogInfo("CalendarInfoUC", $"สรุปผลรวมทั้งหมด: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");
        }

        public void RunAllTestSteps()
        {
            NavigateToCalendarInfoUC();
            RunAllValidationTests();
            RunEditTests();
            RunEditValidationTests();
            RunDeleteTests();
        }

        // -------------------------------------------------------------
        // * Core Helpers & Actions *
        // -------------------------------------------------------------
        private void FillCalendarBasicFields(AutomationElement context, string code, string nameTh, string nameEn, string remark)
        {
            FillTextBox(context, IdCalendarCode, code);
            FillTextBox(context, IdCalendarName, nameTh);
            FillTextBox(context, IdCalendarNameEng, nameEn);
            FillTextBox(context, IdCalendarRemark, remark);
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

        /// <summary>
        /// เซ็ตข้อความแบบ Hybrid ป้องกันข้อความไม่ลง และรองรับสตริงที่มีเว้นวรรค
        /// </summary>
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

            // ตรวจสอบว่าข้อความลงจริงหรือไม่ ถ้าไม่ลงให้ยิง Keyboard Native พิมพ์ซ้ำ
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

            // Tab ออกเสมอเพื่อ Trigger Event Validating
            Keyboard.Press(VirtualKeyShort.TAB);
            Keyboard.Release(VirtualKeyShort.TAB);
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

        private Window? TryClickRowAndOpenEditForm(string codeToFind, string testName)
        {
            try { return ClickRowAndOpenEditForm(codeToFind, testName); }
            catch { return null; }
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

        /// <summary>
        /// [ปรับปรุง] กรอก Code/Name แบบอ่านค่ากลับมายืนยัน, เก็บ baseline ของ Task Result ก่อนกด Save,
        /// แล้วรอให้พาเนลเปลี่ยนก่อนตรวจ เพื่อไม่ให้ไปอ่านข้อความค้างของเคสก่อนหน้า
        /// </summary>
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

        private static readonly HashSet<string> IgnoredTaskResultTexts = new(StringComparer.OrdinalIgnoreCase)
        {
            "Static", "Task Result", "Header Panel", "Column Headers", "Exception Severity Pict",
            "Exception Severity Picture", "ExceptionSeverityToolBar", "Data Panel", "Dock Top",
            "0 Errors", "0 Informations", "More Information", "Description", "Severity",
            "Message", "Details", "Type", "Source", "Date", "Time", "Column1", ""
        };

        private static bool ContainsThai(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text)
            {
                if (c >= '\u0E00' && c <= '\u0E7F') return true;
            }
            return false;
        }

        /// <summary>
        /// [เพิ่มใหม่] อ่านข้อความใน Task Result "รอบเดียว" (ไม่รอ ไม่วนซ้ำ) คืน list ว่างถ้าพาเนลยังไม่แสดง
        /// </summary>
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

        private List<string> GetTaskResultMessages(Window form, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                var m = ReadTaskResultOnce(form);
                if (m.Count > 0) return m;
            }

            return new List<string>();
        }

        private string? TryDetectAnyTaskResultWarning(Window form, int timeoutMs = 1500)
        {
            return GetTaskResultMessages(form, timeoutMs).FirstOrDefault();
        }

        private static void FillTextBox(AutomationElement context, string automationId, string value)
        {
            var el = context.FindFirstDescendant(cf => cf.ByAutomationId(automationId))
                  ?? (automationId == IdCalendarName ? context.FindFirstDescendant(cf => cf.ByAutomationId(IdCalendarNameAlt)) : null);

            if (el != null) SetTextDirectly(el, value);
        }

        /// <summary>
        /// [เพิ่มใหม่] อ่านค่าข้อความปัจจุบันของช่องกรอก
        /// </summary>
        private static string ReadText(AutomationElement el)
        {
            try { if (el.Patterns.Value.IsSupported) return el.Patterns.Value.Pattern.Value.ValueOrDefault ?? ""; } catch { }
            try { return el.AsTextBox()?.Text ?? ""; } catch { }
            return "";
        }

        /// <summary>
        /// [เพิ่มใหม่] กรอกข้อความแล้วอ่านกลับมาเทียบ ถ้าไม่ตรงจะลองกรอกซ้ำ (สูงสุด maxTry รอบ)
        /// ใช้เฉพาะใน FillAndSaveThenAssert เพื่อไม่กระทบขั้นตอนอื่น
        /// </summary>
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

        private AutomationElement? FindGridElement(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return null;

            return _mainWindow.FindAllDescendants().FirstOrDefault(el =>
                (!string.IsNullOrEmpty(el.Name) && el.Name.Trim().IndexOf(val, StringComparison.OrdinalIgnoreCase) >= 0) ||
                (el.Patterns.Value.IsSupported && el.Patterns.Value.Pattern.Value.Value != null &&
                 el.Patterns.Value.Pattern.Value.Value.Trim().IndexOf(val, StringComparison.OrdinalIgnoreCase) >= 0));
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

        private void RunValidationStep(string testName, Action stepAction)
        {
            try { stepAction(); }
            catch (Exception ex) { LogFailureCounted(testName, $"ไม่ผ่าน: {ex.Message}"); }
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

        private static void TryAdd<T>(List<T> list, T? item) where T : struct
        {
            if (item.HasValue) list.Add(item.Value);
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