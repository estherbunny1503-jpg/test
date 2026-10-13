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

namespace HRMITestFlaUI.Module.Personnel
{
    public class EmployeeWelfareFundRateInfoUC
    {
        // =========================================================================================
        // หมวดที่ 1: ข้อมูลการทดสอบทั้ง 5 ขั้น (Test Data)
        // =========================================================================================

        // --- Data Records สำหรับใช้งานในแต่ละเฟส ---
        private sealed record AddItem(
            string Code,
            string Deduct,
            string StartDate,
            string EndDate,
            string Reason,
            string EmployerRate,
            string EmployeeRate,
            string[] Beneficiaries,
            string SaveBtn,
            bool NeedsManualClose);
        private sealed record ValidationItem(
            string TestCaseId,
            string? EmpCode,         // null = ไม่แตะช่องรหัสพนักงาน
            string StartDate,
            string EmployerRate,
            string? EmployeeRate,    // null = ไม่แตะช่องลูกจ้างหัก
            string Reason,
            string SaveBtn,
            int Progress,
            string ProgressMsg,
            string[] ExpectedErrors);
        private sealed record EditItem(
            string Name,
            string Code,
            string StartDate,
            string EmployerRate,
            string SaveBtn,
            bool NeedsManualClose);
        private sealed record DeleteItem(
            string TargetCode,
            string DialogBtn,
            bool ExpectDeleted,
            int Progress,
            string ProgressMsg);

        // ข้อความแจ้งเตือนที่คาดหวัง
        private const string WarnMissingEmployee = "รหัสพนักงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล";
        private const string WarnMissingStartDate = "วันที่เริ่มต้น ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล";
        private const string WarnMissingReason = "สาเหตุ ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล";
        private const string WarnMissingRate = "นายจ้างหักเงินสมทบ หรือลูกจ้างหักเงินสะสม ต้องมากกว่า 0 และห้ามว่าง";
        private const string ValidationReason = "ปรับอัตราเงินสมทบกองทุนสงเคราะห์";

        // [ขั้นที่ 1] ข้อมูลทดสอบการสร้างใหม่ (Add / Navigate)
        private static readonly AddItem[] Step1_AddCases =
        {
            new(
                "EMP00001",
                "หัก",
                "13/08/2569",
                "29/08/2569",
                "เปลี่ยนอัตราเงินสะสม",
                "3.00",
                "3.00",
                new[] { "นาง วรรณา ใจดี(ภรรยา)", "ด.ช. กิตติ ใจดี (บุตร)", "ด.ญ. กานดา ใจดี (บุตร)" },
                "Save",
                true),
            new(
                "EMP00002",
                "ไม่หัก",
                "01/01/2569",
                "31/12/2569",
                "ปรับปรุงข้อมูลสมาชิกกองทุน",
                "5.00",
                "5.00",
                new[] { "นาย นพดล ใจงาม (สามี)", "นาย วีระ ใจงาม(บุตร)", "นาย วินัย แสงทอง(พี่ชาย)" },
                "Save and New",
                false),
            new(
                "EMP00003",
                "หัก",
                "01/06/2569",
                "30/06/2570",
                "แก้ไขยอดเงินสะสมย้อนหลัง",
                "3.25",
                "3.25",
                new[] { "นาย ชัยชาญ สุขใจ (บิดา)", "นาง ชลิพร สุขใจ (มารดา)", "นาย ชาญชัย สุขใจ (น้องชาย)" },
                "Save and New",
                false),
            new(
                "EMP00004",
                "ไม่หัก",
                "01/09/2569",
                "31/12/2571",
                "เปลี่ยนอัตราการหักเงิน",
                "4.00",
                "2.00",
                new[] { "นาง พิมลพร เจริญยิ่ง (ผู้จัดการมรดก)", "มูลนิธิเพื่อการศึกษา (ผู้รับมอบ)", "โรงพยาบาลศิริราช (บริจาค)" },
                "Save and New",
                false),
            new(
                "EP0005",
                "หัก",
                "15/08/2569",
                "15/11/2569",
                "ปรับยอดจากข้อมูลเดิม",
                "2.50",
                "2.50",
                new[] { "นาง สมศรี วัฒนชัย(มารดา)", "นาย สมศักดิ์ วัฒนชัย (บิดา)", "น.ส. สมหญิง วัฒนชัย (น้องสาว)" },
                "Save and Close",
                false)
        };

        // [ขั้นที่ 2] ข้อมูลทดสอบ Validation ตอนเพิ่ม (Add Validation)
        private static readonly ValidationItem[] Step2_AddValidationCases =
        {
            new("TestCase01_MissingEmployeeCode", null, "15/11/2569", "3.00", null, ValidationReason, "Save", 25,
                "Validating: Missing Employee Code...", new[] { WarnMissingEmployee }),

            new("TestCase02_MissingStartDate", "EMP00006", "", "3.00", null, ValidationReason, "Save and New", 28,
                "Validating: Missing Start Date...", new[] { WarnMissingStartDate }),

            new("TestCase03_MissingReason", "EMP00007", "15/11/2568", "3.01", null, "", "Save and New", 31,
                "Validating: Missing Reason...", new[] { WarnMissingReason }),

            new("TestCase04_MissingRate", "EMP00001", "15/11/2569", "", null, ValidationReason, "Save and New", 37,
                "Validating: Missing Rate...", new[] { WarnMissingRate })
        };

        // [ขั้นที่ 3] ข้อมูลทดสอบการแก้ไขข้อมูล (Edit Records)
        private static readonly EditItem[] Step3_EditCases =
        {
            new(
                "TestCase08_EditEmp00001",
                "EMP00001",
                "19/08/2569",
                "1.00",
                "Save",
                true),
            new(
                "TestCase09_EditEmp00003",
                "EMP00003",
                "10/06/2569",
                "3.50",
                "Save and Close",
                false)
        };

        // [ขั้นที่ 4] ข้อมูลทดสอบ Validation ตอนแก้ไข (Edit Validation)
        private const string EditTargetRow = "EMP00002";
        private static readonly ValidationItem[] Step4_EditValidationCases =
        {
            new("TestCase10_EditValidation_MissingStartDate", null, "", "1.00", "3.00", ValidationReason, "Save", 62,
                "Validating Edit: Missing Start Date...", new[] { WarnMissingStartDate }),

            new("TestCase10_EditValidation_MissingRate", null, "13/08/2569", "", "", ValidationReason, "Save and New", 68,
                "Validating Edit: Missing Rate...", new[] { WarnMissingRate }),

            new("TestCase10_EditValidation_MissingReason", null, "10/08/2569", "1.00", "3.00", "", "Save and New", 74,
                "Validating Edit: Missing Reason...", new[] { WarnMissingReason })
        };

        // [ขั้นที่ 5] ข้อมูลทดสอบการลบข้อมูล (Delete Records)
        private static readonly DeleteItem[] Step5_DeleteCases =
        {
            new("EMP00001", "Yes", true, 92, "Delete Test: EMP00001 (confirmed delete)..."),
            new("EP0005", "No", false, 100, "Delete Test: EP0005 (cancelled delete)...")
        };


        // =========================================================================================
        // หมวดที่ 2: ส่วนที่ขึ้นกับหน้าจอนั้นๆ (Screen-Specific Implementation)
        // **เมื่อเปลี่ยนไปทำหน้าจออื่น ต้องมาแก้ไขค่าและเมทอดในส่วนนี้ทั้งหมด**
        // =========================================================================================

        // 2.1 Locator & Mappings ประจำหน้าจอ
        private const string MenuName = "บันทึกอัตราหักกองทุนสงเคราะห์ลูกจ้าง";
        private const string ScreenLogName = "EmployeeWelfareFundRateInfoUC";
        private const string FormId = "EmployeeWelfareFundRateInfoUC";
        private const string GeneralTabId = "devTabPgGeneral";
        private const string FirstObjectID = "devBtnEditEmp";

        // 2.2 ฟิลด์ Input ประจำหน้าจอ
        private const string EmpCodeFieldId = "devBtnEditEmp";            // รหัสพนักงาน (ปุ่ม ...)
        private const string DeductFieldId = "devRdgIsDeduct";            // หัก / ไม่หัก
        private const string StartDateFieldId = "devDateStartDate";       // วันที่เริ่มต้น
        private const string EndDateFieldId = "devDateEndDate";           // วันที่สิ้นสุด
        private const string ReasonFieldId = "devtxtReason";              // สาเหตุ
        private const string EmployerRateFieldId = "devSpinEmployerDeduct"; // นายจ้างหักเงินสมทบ ร้อยละ
        private const string EmployeeRateFieldId = "devSpinEmployeeDeduct"; // ลูกจ้างหักเงินสะสม ร้อยละ
        private static readonly string[] BeneficiaryFieldIds = { "devtxtBenefitsPerson1", "devtxtBenefitsPerson2", "devtxtBenefitsPerson3" };

        /// <summary>
        /// ขั้นที่ 1: เปิดหน้าจอและกรอกข้อมูลสร้างใหม่
        /// </summary>
        public void NavigateToEmployeeWelfareFundRateInfoUC()
        {
            const string stageName = "TestCase00_AddEmployeeWelfareFundRateInfo";
            TestLogger.LogInfo(ScreenLogName, $"เริ่มทดสอบหน้าจอ {MenuName}");
            TestProgressHelper.Report(2, $"Opening Personnel -> {MenuName} Menu via Search...");

            if (!TryOpenMenu(stageName, MenuName)) return;

            Window? form = null;
            for (int i = 0; i < Step1_AddCases.Length; i++)
            {
                var item = Step1_AddCases[i];
                string testName = $"{stageName}_{item.Code}";
                TestProgressHelper.Report(4 + (i * 4), $"Adding {MenuName} ({i + 1}/{Step1_AddCases.Length}): {item.Code} - {item.Reason}...");

                try
                {
                    form ??= GetOrOpenForm();
                    var tab = GetGeneralTab(form);
                    FillFormData(form, tab, item);

                    if (!TrySaveAndHandleDuplicate(form, testName, item.Code, item.SaveBtn))
                    {
                        form = null;
                        continue;
                    }

                    if (item.SaveBtn == "Save and New")
                    {
                        WaitUntil(() => string.IsNullOrEmpty(tab.FindFirstDescendant(cf => cf.ByAutomationId(FirstObjectID))?.AsTextBox().Text), 3000);
                        LogSuccess(testName, $"'{item.Code}' บันทึกสำเร็จ (Save and New)");
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
                    form = null;
                }
            }
        }

        /// <summary>
        /// ขั้นที่ 2: รัน Validation ตอนสร้างข้อมูลใหม่
        /// </summary>
        public void RunAllValidationTests()
        {
            const string stage = "TestCase01-07_Validation";
            TestProgressHelper.Report(22, $"Opening {MenuName} Form for Validation Tests...");

            Window form;
            try { form = OpenForm(); }
            catch (Exception ex) { LogFailure(stage, $"เปิดฟอร์มไม่สำเร็จ: {ex.Message}"); return; }

            var tab = GetGeneralTab(form);

            foreach (var testCase in Step2_AddValidationCases)
            {
                RunStep(testCase.TestCaseId,
                    () => FillValidationAndAssert(tab, form, testCase, testCase.TestCaseId),
                    testCase.Progress, testCase.ProgressMsg);
            }

            // เคสเสริมเฉพาะหน้าจอ: พฤติกรรมเมื่อปิด Dialog ข้อความเตือน
            const string dupName = "TestCase05-07";
            RunStep($"{dupName}_CloseSequence",
                () => RunCloseDialogSequence(form, dupName, WarnMissingRate),
                40, "Validating: Close Sequence...");
        }

        /// <summary>
        /// ขั้นที่ 3: เลือกแก้ไขรายการในตาราง
        /// </summary>
        public void RunEditTests()
        {
            TestProgressHelper.Report(42, "Starting Edit Operations...");

            for (int i = 0; i < Step3_EditCases.Length; i++)
            {
                var item = Step3_EditCases[i];
                try
                {
                    var form = ClickRowAndOpenEdit(item.Code);
                    var tab = GetGeneralTab(form);
                    SetDate(tab, StartDateFieldId, item.StartDate);
                    SetText(tab, EmployerRateFieldId, item.EmployerRate);

                    if (!TrySaveAndHandleDuplicate(form, item.Name, item.Code, item.SaveBtn)) continue;
                    if (item.NeedsManualClose) form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();

                    WaitForClosed(() => FindOpenForm());
                    RefreshGrid();

                    bool recordFound = VerifyRecordInGrid(item.Code, refresh: false);
                    if (recordFound)
                        LogSuccess(item.Name, $"แก้ไข '{item.Code}' สำเร็จ (พบข้อมูลที่แก้ไขในตารางหลัก)");
                    else
                        LogFailure(item.Name, $"แก้ไข '{item.Code}' แล้ว แต่ไม่พบข้อมูลที่อัปเดตในตารางหลัก");
                }
                catch (Exception ex)
                {
                    LogFailure(item.Name, $"แก้ไข '{item.Code}' ไม่สำเร็จ: {ex.Message}");
                }
                finally
                {
                    TestProgressHelper.Report(42 + ((i + 1) * 13 / Step3_EditCases.Length), $"Editing Record ({i + 1}/{Step3_EditCases.Length})...");
                }
            }
        }

        /// <summary>
        /// ขั้นที่ 4: รัน Validation ตอนแก้ไขข้อมูล
        /// </summary>
        public void RunEditValidationTests()
        {
            const string baseName = "TestCase10_EditValidation";
            TestProgressHelper.Report(58, "Opening Edit Form for Validation Tests...");

            Window form;
            try { form = ClickRowAndOpenEdit(EditTargetRow); }
            catch (Exception ex) { LogFailure(baseName, $"เปิดฟอร์ม Edit ไม่สำเร็จ: {ex.Message}"); return; }

            var tab = GetGeneralTab(form);

            foreach (var testCase in Step4_EditValidationCases)
            {
                RunStep(testCase.TestCaseId,
                    () => FillValidationAndAssert(tab, form, testCase, testCase.TestCaseId),
                    testCase.Progress, testCase.ProgressMsg);
            }

            const string dupSub = $"{baseName}_MissingReason";
            RunStep($"{dupSub}_CloseSequence",
                () => RunCloseDialogSequence(form, dupSub, WarnMissingReason),
                80, "Validating Edit: Missing Reason Close Sequence...");

            LogSuccess(baseName, "รัน Validation Edit ครบทุกเคสแล้ว");
        }

        /// <summary>
        /// ขั้นที่ 5: ทดสอบการลบรายการในตาราง
        /// </summary>
        public void RunDeleteTests()
        {
            const string baseName = "TestCase11_Delete";
            TestProgressHelper.Report(84, "Starting Delete Tests...");

            foreach (var testCase in Step5_DeleteCases)
            {
                RunDeleteCase(baseName, testCase.TargetCode, testCase.DialogBtn, testCase.ExpectDeleted);
                TestProgressHelper.Report(testCase.Progress, testCase.ProgressMsg);
            }

            LogSuccess(baseName, "ทดสอบ Delete ครบทุกเคส");
            TestLogger.LogInfo(ScreenLogName, $"สรุปผลรวมทั้งหมด: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");
        }

        // --- Custom Mapping Logic ประจำหน้าจอนี้ ---

        /* กรอกข้อมูลทุกช่อง (รหัสพนักงาน, หัก/ไม่หัก, วันที่, สาเหตุ, อัตรา, ผู้รับผลประโยชน์) */
        private void FillFormData(Window form, AutomationElement tab, AddItem item)
        {
            SelectEmployee(form, item.Code);
            SelectRadio(tab, DeductFieldId, item.Deduct);
            SetDate(tab, StartDateFieldId, item.StartDate);
            SetDate(tab, EndDateFieldId, item.EndDate);
            SetText(tab, ReasonFieldId, item.Reason);
            SetText(tab, EmployerRateFieldId, item.EmployerRate);
            SetText(tab, EmployeeRateFieldId, item.EmployeeRate);
            for (int b = 0; b < item.Beneficiaries.Length && b < BeneficiaryFieldIds.Length; b++)
                SetText(tab, BeneficiaryFieldIds[b], item.Beneficiaries[b]);
        }

        /* กรอกข้อมูลตามเคส แล้วกดบันทึกและตรวจข้อความ (EmpCode / EmployeeRate = null คือไม่แตะช่องนั้น) */
        private void FillValidationAndAssert(AutomationElement tab, Window form, ValidationItem tc, string testName)
        {
            if (tc.EmpCode != null) SelectEmployee(form, tc.EmpCode);
            SetDate(tab, StartDateFieldId, tc.StartDate);
            SetText(tab, EmployerRateFieldId, tc.EmployerRate);
            if (tc.EmployeeRate != null) SetText(tab, EmployeeRateFieldId, tc.EmployeeRate);
            SetText(tab, ReasonFieldId, tc.Reason);
            form.FindFirstDescendant(cf => cf.ByName(tc.SaveBtn))?.Click();
            AssertScreenTaskResult(form, tc.ExpectedErrors, testName);
        }

        private static string? DetectDuplicateMessage(IEnumerable<string> texts)
        {
            if (texts.Any(t => t.Contains("รหัสพนักงาน") && t.Contains("ซ้ำ"))) return "รหัสพนักงานซ้ำ";
            return null;
        }

        private AutomationElement GetGeneralTab(Window form) =>
            Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(GeneralTabId)), 3000) ?? form;

        private Window? FindOpenForm() =>
            _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault() ??
            _automation.GetDesktop().FindFirstChild(cf => cf.ByAutomationId(FormId))?.AsWindow();

        // --- การกรอกข้อมูลเฉพาะหน้าจอนี้ ---

        /* กรอกช่องวันที่ (คลิก → ลบค่าเดิม → พิมพ์ → Tab) ค่าว่าง = เคลียร์ช่อง
           รอช่อง enable ก่อน เพราะฟอร์ม Edit bind ข้อมูลเดิมช้า */
        private void SetDate(AutomationElement tab, string id, string dateText)
        {
            var el = Retry(() => tab.FindFirstDescendant(cf => cf.ByAutomationId(id)), 3000) ?? throw new Exception($"ไม่พบช่องวันที่ {id}");
            WaitUntil(() => el.Properties.IsEnabled.ValueOrDefault, 5000);
            TypeByKeyboard(el, dateText, extraBackspace: true);
        }

        /* พิมพ์ลงช่องด้วยคีย์บอร์ดจริง (คลิกฝั่งซ้ายของช่อง กันโดนปุ่มปฏิทินที่ขอบขวา) */
        private static void TypeByKeyboard(AutomationElement field, string text, bool extraBackspace = false)
        {
            var r = field.BoundingRectangle;
            Mouse.Click(new Point(r.Left + Math.Min(20, r.Width / 4), r.Top + r.Height / 2));

            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            Keyboard.Type(VirtualKeyShort.DELETE);
            if (extraBackspace)   // กัน mask ของช่องวันที่เหลือตัวอักษรค้าง
                for (int i = 0; i < 10; i++) Keyboard.Type(VirtualKeyShort.BACK);

            if (!string.IsNullOrEmpty(text)) Keyboard.Type(text);
            Keyboard.Type(VirtualKeyShort.TAB); // Tab ออกเพื่อ commit ค่า
        }

        /* เลือกตัวเลือกใน RadioGroup (หัก / ไม่หัก) */
        private void SelectRadio(AutomationElement tab, string groupId, string option)
        {
            var group = Retry(() => tab.FindFirstDescendant(cf => cf.ByAutomationId(groupId)), 3000) ?? throw new Exception($"ไม่พบ {groupId}");
            var radio = group.FindFirstDescendant(cf => cf.ByName(option)) ?? throw new Exception($"ไม่พบตัวเลือก '{option}'");

            if (radio.Patterns.SelectionItem.IsSupported)
            {
                radio.Patterns.SelectionItem.Pattern.Select();
                WaitUntil(() => radio.Patterns.SelectionItem.Pattern.IsSelected.ValueOrDefault, 2000);
            }
            else radio.Click();
        }

        /* เลือกรหัสพนักงาน: กด ... → หาแถวที่รหัสตรงใน Pop-up "ข้อมูลพนักงาน" → คลิก → กด Select
           (Pop-up เป็น Modal ของฟอร์ม และปุ่ม/แถวใน DevExpress ต้องคลิกด้วยเมาส์ที่ตำแหน่งจริง) */
        private void SelectEmployee(Window form, string code)
        {
            var control = Retry(() => GetGeneralTab(form).FindFirstDescendant(cf => cf.ByAutomationId(EmpCodeFieldId)), 5000)
                          ?? throw new Exception($"ไม่พบช่องรหัสพนักงาน {EmpCodeFieldId}");
            var lookupBtn = control.FindFirstDescendant(cf => cf.ByName("..."))
                            ?? control.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button))
                            ?? control;
            ClickByMouse(lookupBtn);

            var popup = Retry(() =>
            {
                foreach (var title in new[] { "ข้อมูลพนักงาน", "เลือกพนักงาน", "รายชื่อพนักงาน" })
                {
                    var w = _automation.GetDesktop().FindFirstChild(cf => cf.ByName(title))?.AsWindow();
                    if (w != null) return w;
                }
                return form.ModalWindows.FirstOrDefault()
                       ?? _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault(w => w.FindFirstDescendant(cf => cf.ByName("Select")) != null);
            }, 6000)
                        ?? throw new Exception("เปิด Pop-up เลือกพนักงานไม่สำเร็จ");

            try
            {
                WaitUntil(() => GetPopupRows(popup).Length > 0, 8000); // รอให้ตารางใน Pop-up โหลดแถว

                var row = FindEmployeeRow(popup, code)
                          ?? throw new Exception($"ไม่พบรหัส '{code}' ใน Pop-up เลือกพนักงาน");
                try { if (row.Patterns.ScrollItem.IsSupported) row.Patterns.ScrollItem.Pattern.ScrollIntoView(); } catch { }
                ClickByMouse(row);

                if (popup.IsAvailable && !popup.Properties.IsOffscreen.ValueOrDefault)
                {
                    var selectBtn = Retry(() => popup.FindFirstDescendant(cf => cf.ByName("Select"))
                                                ?? popup.FindFirstDescendant(cf => cf.ByAutomationId("btnSelect"))
                                                ?? popup.FindFirstDescendant(cf => cf.ByName("เลือก")), 3000);
                    if (selectBtn != null) ClickByMouse(selectBtn);
                    else Mouse.Click(new Point(popup.BoundingRectangle.Right - 75, popup.BoundingRectangle.Bottom - 25)); // ตำแหน่งปุ่ม Select มุมขวาล่าง
                }
                WaitForClosed(() => popup, 5000);
            }
            catch
            {
                // เลือกไม่สำเร็จ → ปิด Pop-up ก่อน ไม่งั้นจะบังฟอร์มให้เคสถัดไปพังตาม
                try
                {
                    var cancel = popup.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("Cancel")));
                    if (cancel != null) ClickByMouse(cancel); else popup.Close();
                }
                catch { try { Keyboard.Type(VirtualKeyShort.ESCAPE); } catch { } }
                throw;
            }
        }

        /* คลิกด้วยเมาส์ที่กึ่งกลาง element (ปุ่ม DevExpress บางตัว Focus ไม่ได้ ใช้ .Click() แล้วไม่ติด) */
        private static void ClickByMouse(AutomationElement el)
        {
            try
            {
                var r = el.BoundingRectangle;
                if (!r.IsEmpty)
                {
                    Mouse.Click(new Point(r.Left + r.Width / 2, r.Top + r.Height / 2));
                    return;
                }
            }
            catch { }

            if (el.Patterns.Invoke.IsSupported) el.Patterns.Invoke.Pattern.Invoke();
            else el.Click();
        }

        /* ดึงแถวของตารางใน Pop-up (DataItem หรือ ListItem) */
        private static AutomationElement[] GetPopupRows(AutomationElement popup)
        {
            try
            {
                var rows = popup.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem));
                return rows.Length > 0 ? rows : popup.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem));
            }
            catch { return Array.Empty<AutomationElement>(); }
        }

        /* หาแถวใน Pop-up ที่มีรหัสตรงกับ code (ไม่เจอก็กด Page Down ทีละหน้า) */
        private static AutomationElement? FindEmployeeRow(Window popup, string code)
        {
            AutomationElement? Match() => GetPopupRows(popup)
                .FirstOrDefault(r => string.Equals(ReadText(r), code, StringComparison.OrdinalIgnoreCase)
                                     || r.FindAllDescendants().Any(c => string.Equals(ReadText(c), code, StringComparison.OrdinalIgnoreCase)));

            var found = Match();
            var pageDown = popup.FindFirstDescendant(cf => cf.ByName("Page Down"));
            for (int i = 0; found == null && pageDown != null && i < 15; i++)
            {
                ClickByMouse(pageDown);
                WaitUntil(() => (found = Match()) != null, 500);
            }
            return found;
        }

        /* อ่านค่าจริงของ element (Value ก่อน แล้วค่อย Name) */
        private static string ReadText(AutomationElement e)
        {
            try { if (e.Patterns.Value.IsSupported && !string.IsNullOrWhiteSpace(e.Patterns.Value.Pattern.Value.Value)) return e.Patterns.Value.Pattern.Value.Value.Trim(); } catch { }
            try { return (e.Name ?? "").Trim(); } catch { return ""; }
        }

        /* ตรวจข้อความใน Task Result แบบหน้าจอนี้: เทียบแบบไม่สนช่องว่าง (ข้อความในโปรแกรมเว้นวรรคไม่ตรงกับ TestStep) */
        private void AssertScreenTaskResult(Window form, string[] msgs, string testName)
        {
            var pending = new HashSet<string>(msgs);
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < 5000 && pending.Count > 0)
            {
                var panel = FindTaskResultPanel(form, 500);
                if (panel != null)
                {
                    var texts = GetPanelTexts(panel).Select(Squash).ToList();
                    foreach (var m in pending.ToList())
                    {
                        if (texts.Any(t => t.Contains(Squash(m)))) pending.Remove(m);
                    }
                }
            }

            if (pending.Count > 0) throw new Exception($"ไม่พบข้อความ: '{string.Join("', '", pending)}'");
            LogSuccess(testName, $"พบข้อความ: {string.Join(",", msgs.Select(m => $"'{m}'"))}");
        }

        /* ตัดช่องว่างทั้งหมดออก */
        private static string Squash(string s) => new string(s.Where(c => !char.IsWhiteSpace(c)).ToArray());


        // =========================================================================================
        // หมวดที่ 3: ส่วนการทำงานกลางที่ตายตัว (Common / Reusable Helpers)
        // **ใช้งานร่วมกันได้ทุกหน้าจอ ไม่ต้องแก้ไขคำสั่งเมื่อเปลี่ยนหน้า**
        // =========================================================================================

        private readonly AutomationElement _mainWindow;
        private readonly UIA3Automation _automation;
        private int _totalSuccess = 0;
        private int _totalFail = 0;
        private const int TimeoutMs = 6000;

        public EmployeeWelfareFundRateInfoUC(AutomationElement mainWindow, UIA3Automation automation)
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
                    if (exists) { LogFailure(testName, $"'{code}' ลบข้อมูลไม่สำเร็จ"); throw new Exception(); }
                    LogSuccess(testName, $"'{code}' ถูกลบสำเร็จ");
                }
                else
                {
                    if (!exists) { LogFailure(testName, $"'{code}' ลบข้อมูลผิดพลาด"); throw new Exception(); }
                    LogSuccess(testName, $"'{code}' ข้อมูลยังไม่ถูกลบ");
                }
            }
            catch (Exception ex) when (!string.IsNullOrEmpty(ex.Message))
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

        private bool TrySaveAndHandleDuplicate(Window form, string testName, string code, string saveBtn)
        {
            var btn = form.FindFirstDescendant(cf => cf.ByName(saveBtn)) ?? throw new Exception($"ไม่พบปุ่ม {saveBtn}");
            btn.Click();

            if (testName.Contains("Validation") || testName.Contains("Duplicate")) return true;

            var panel = FindTaskResultPanel(form, 1500);
            if (panel == null) return true;

            string? dup = DetectDuplicateMessage(GetPanelTexts(panel));
            if (dup == null) return true;

            LogFailure(testName, $"'{code}' ข้อมูลซ้ำ: {dup}");
            try { ClickCloseAndHandleDialog(form, "No", $"{testName}_Close"); } catch { }
            return false;
        }

        private void RunCloseDialogSequence(Window form, string prefix, string expectedMsg)
        {
            string ySub = $"{prefix}_Close_Yes";
            ClickCloseAndHandleDialog(form, "Yes", ySub);
            AssertTaskResult(form, new[] { expectedMsg }, ySub, $"พบข้อความ: '{expectedMsg}' และ ฟอร์มถูกเปิดอยู่");
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
            if (!WaitUntil(() => form.Properties.IsOffscreen.ValueOrDefault || !form.IsAvailable, 3000))
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
            }
            return null;
        }
    }
}