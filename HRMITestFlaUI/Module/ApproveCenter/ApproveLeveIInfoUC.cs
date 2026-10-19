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

namespace HRMITestFlaUI.Pages.ApproveCenter
{
    public class ApproveLevelInfoUC
    {
        // =========================================================================================
        // หมวดที่ 1: ข้อมูลการทดสอบทั้ง 5 ขั้น (Test Data)
        // =========================================================================================

        // --- Data Records สำหรับใช้งานในแต่ละเฟส ---
        private sealed record AddItem(
            string Code,
            string NameTh,
            string Menu,
            string EmpCode,
            string Email,
            string[] CheckIds,
            string SaveBtn,
            bool NeedsManualClose);
        private sealed record ValidationItem(
            string TestCaseId,
            string Code,
            string NameTh,
            string? Menu,
            string? EmpCode,
            string? Email,
            string SaveBtn,
            int Progress,
            string ProgressMsg,
            string[] ExpectedErrors);
        private sealed record EditItem(
            string Name,
            string TargetCode,
            string[] UncheckIds,
            string SaveBtn,
            bool NeedsManualClose);
        private sealed record DeleteItem(
            string TargetCode,
            string DialogBtn,
            bool ExpectDeleted,
            int Progress,
            string ProgressMsg);

        // [ขั้นที่ 1] ข้อมูลทดสอบการสร้างใหม่ (Add / Navigate)
        private static readonly AddItem[] Step1_AddCases =
        {
            new(
                "APR001",
                "อนุมัติลา",
                "บันทึกขออนุมัติลา",
                "EMP00001",
                "approver01@samplecompany.example",
                new[] { ChkWaitDoc, ChkToApprover, ChkToEmpReq },
                "Save",
                true),
            new(
                "APR002",
                "อนุมัติลาแบบกลุ่ม",
                "บันทึกขออนุมัติลาแบบกลุ่ม",
                "EMP00002",
                "approver02@samplecompany.example",
                new[] { ChkNotApprove, ChkNotApproveAll },
                "Save and New",
                false),
            new(
                "APR003",
                "ปรับตำแหน่งและเงินเดือน",
                "บันทึกปรับตำแหน่งและเงินเดือน",
                "EMP00003",
                "manager@samplecompany.example",
                new[] { ChkLastApprove },
                "Save and New",
                false),
            new(
                "APR004",
                "โอนย้ายสาขา",
                "บันทึกโอนย้าย/โยกย้ายสาขา",
                "EMP00004",
                "hrmanager@samplecompany.example",
                new[] { ChkLastApproveAll },
                "Save and Close",
                false)
        };

        // [ขั้นที่ 2] ข้อมูลทดสอบ Validation ตอนเพิ่ม (Add Validation)
        // หมายเหตุ: Menu / EmpCode / Email = null หมายถึง "ไม่เลือก / ไม่กรอก"
        private const string ValidEmail = "pimchanok.approver@example.com";
        private static readonly ValidationItem[] Step2_AddValidationCases =
        {
            new("TestCase01_MissingFlowCode", "", "อนุมัติลา",
                "บันทึกขออนุมัติลา", "EMP00001", ValidEmail, "Save", 25,
                "Validating: Missing Flow Code...",
                new[] { "รหัสผังการอนุมัติ ไม่สามารถเป็นค่าว่าง" + EditValidationSuffix }),

            new("TestCase02_MissingNameAndMenu", "APR005", "",
                null, "EMP00001", ValidEmail, "Save and New", 29,
                "Validating: Missing Flow Name and Menu...",
                new[] { "ชื่อผังการอนุมัติ ไม่สามารถเป็นค่าว่าง" + EditValidationSuffix, "ชื่อเมนู ไม่สามารถเป็นค่าว่าง" + EditValidationSuffix }),

            new("TestCase03_MissingEmail", "APR005", "อนุมัติลา",
                "บันทึกขออนุมัติลา", "EMP00001", null, "Save and New", 33,
                "Validating: Missing Email...",
                new[] { "อีเมลล์ ไม่สามารถเป็นค่าว่าง" + EditValidationSuffix }),

            new("TestCase04_DuplicateFlowCode", "APR001", "อนุมัติลา",
                "บันทึกขออนุมัติลา", "EMP00001", ValidEmail, "Save and Close", 37,
                "Validating: Duplicate Flow Code...",
                new[] { "รหัสผังการอนุมัติ ห้ามซ้ำ" + EditValidationSuffix })
        };

        // [ขั้นที่ 3] ข้อมูลทดสอบการแก้ไขข้อมูล (Edit Records) : เอาติ๊กส่งอีเมลออก
        private static readonly EditItem[] Step3_EditCases =
        {
            new(
                "TestCase05_EditRow_EMP00001",
                "APR001",
                new[] { ChkWaitDoc, ChkToApprover, ChkToEmpReq },
                "Save",
                true),
            new(
                "TestCase06_EditRow_EMP00002",
                "APR002",
                new[] { ChkNotApprove, ChkNotApproveAll },
                "Save and Close",
                false)
        };

        // [ขั้นที่ 4] ข้อมูลทดสอบ Validation ตอนแก้ไข (Edit Validation)
        // list เรียงจากใหม่ไปเก่า -> "แถวแรก" = APR004
        private const string EditTargetRow = "APR004";
        private const string EditValidationSuffix = " กรุณาป้อนข้อมูล";
        private const string Step4_DupCode = "APR001";
        private const string Step4_DupName = "อนุมัติลา";
        private static readonly ValidationItem[] Step4_EditValidationCases =
        {
            new("TestCase07_EditValidation_MissingCodeAndName", "", "",
                null, null, null, "Save", 62,
                "Validating Edit: Missing Code and Name...",
                new[] { "รหัสผังการอนุมัติ ไม่สามารถเป็นค่าว่าง" + EditValidationSuffix, "ชื่อผังการอนุมัติ ไม่สามารถเป็นค่าว่าง" + EditValidationSuffix })
        };

        // [ขั้นที่ 5] ข้อมูลทดสอบการลบข้อมูล (Delete Records)
        // APR001 = EMP00001 (แถวสุดท้ายของ list) / APR004 = EMP00004 (แถวแรกของ list)
        private static readonly DeleteItem[] Step5_DeleteCases =
        {
            new("APR001", "Yes", true, 92, "Delete Test: APR001 (confirmed delete)..."),
            new("APR004", "No", false, 100, "Delete Test: APR004 (cancelled delete)...")
        };


        // =========================================================================================
        // หมวดที่ 2: ส่วนที่ขึ้นกับหน้าจอนั้นๆ (Screen-Specific Implementation)
        // **เมื่อเปลี่ยนไปทำหน้าจออื่น ต้องมาแก้ไขค่าและเมทอดในส่วนนี้ทั้งหมด**
        // =========================================================================================

        // 2.1 Locator & Mappings ประจำหน้าจอ
        private const string MenuName = "บันทึกลำดับขั้นการอนุมัติ";
        private const string ScreenLogName = "ApproveLevelInfoPage";
        private const string FormId = "ApproveLevelInfoUC";
        private const string GeneralTabId = "devTabPgGeneral";          // ถ้าไม่พบ จะ fallback ไปใช้ form
        private const string FirstObjectID = "devtxtFlowApproveCode";
        private const string ApproveCenterMenuName = "Approve Center";
        private const double MenuPanelWidthRatio = 0.25;      // แผงเมนูซ้ายกว้างไม่เกิน 25% ของหน้าต่างหลัก

        // 2.2 ฟิลด์ Input ประจำหน้าจอ
        private const string CodeFieldId = "devtxtFlowApproveCode";
        private const string NameThFieldId = "devtxtFlowApproveName";
        private const string MenuFieldId = "devbtnMenuName";
        private const string GridId = "devGridApproveLevel";

        // Checkbox การส่งอีเมลแจ้งเตือน
        private const string ChkWaitDoc = "devChkSendEmailWaitDoc";               // แจ้งเตือนผู้อนุมัติลำดับถัดไปเมื่อเอกสารรออนุมัติ
        private const string ChkToApprover = "devChkSendEmailToApprover";         // กรณีผู้อนุมัติแทน แจ้งผลให้ผู้อนุมัติทราบ
        private const string ChkToEmpReq = "devChkSendEmailToEmpReq";             // แจ้งผลอนุมัติให้ผู้ขออนุมัติ
        private const string ChkNotApprove = "devChkSendEmailNotApprove";         // ไม่อนุมัติ -> ผู้อนุมัติลำดับขั้นก่อนหน้าทั้งหมด
        private const string ChkNotApproveAll = "devChkSendEmailNotApproveAll";   // ไม่อนุมัติ -> ผู้อนุมัติลำดับขั้นสุดท้าย
        private const string ChkLastApprove = "devChkSendEmailLastApprove";       // อนุมัติ -> ผู้อนุมัติลำดับขั้นก่อนหน้าทั้งหมด
        private const string ChkLastApproveAll = "devChkSendEmailLastApproveAll"; // อนุมัติ -> ผู้อนุมัติลำดับขั้นสุดท้าย

        // 2.3 ลำดับคอลัมน์ในตาราง (0-based)
        private const int ColApproverCode = 1;   // รหัสผู้อนุมัติ (มีปุ่ม ... เป็น 'Editing control')
        private const int ColEmailCustom = 6;    // Radio "กำหนดเอง"
        private const int ColEmailAddress = 7;   // ที่อยู่อีเมล

        /// <summary>
        /// ขั้นที่ 1: เปิดหน้าจอและกรอกข้อมูลสร้างใหม่
        /// </summary>
        public void NavigateToApproveLevelInfoUC()
        {
            const string stageName = "TestCase00_AddApproveLevelData";
            TestLogger.LogInfo(ScreenLogName, $"เริ่มทดสอบหน้าจอ {MenuName}");
            TestProgressHelper.Report(2, $"Opening Approve Center -> {MenuName} Menu via Search...");

            if (!TryOpenMenu(stageName, MenuName)) return;

            // คืนข้อมูลหน้านี้ให้สะอาดก่อนเริ่ม: ลบแถวทดสอบ (APR001-APR005) ที่ค้างจากรอบก่อน กันขึ้นข้อมูลซ้ำ
            CleanupLeftoverTestData();

            Window? form = null;
            for (int i = 0; i < Step1_AddCases.Length; i++)
            {
                var item = Step1_AddCases[i];
                string testName = $"{stageName}_{item.Code}";
                TestProgressHelper.Report(4 + (i * 4), $"Adding {MenuName} ({i + 1}/{Step1_AddCases.Length}): {item.Code} - {item.NameTh}...");

                try
                {
                    form ??= GetOrOpenForm();
                    var tab = GetGeneralTab(form);
                    FillFormData(form, tab, item.Code, item.NameTh, item.Menu, item.EmpCode, item.Email);
                    foreach (var id in item.CheckIds) SetCheck(form, id, true);

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
                    // ปิดฟอร์มที่ค้างอยู่ เพื่อให้เคสถัดไป (เช่น APR004) เริ่มบนฟอร์มใหม่ ไม่ต่อทับสถานะของเคสที่พลาด
                    try { DiscardForm(FindOpenForm()); } catch { }
                }
            }
        }

        /// <summary>
        /// ขั้นที่ 2: รัน Validation ตอนสร้างข้อมูลใหม่
        /// </summary>
        public void RunAllValidationTests()
        {
            const string stage = "TestCase01-04_Validation";
            TestProgressHelper.Report(22, $"Opening {MenuName} Form for Validation Tests...");

            Window form;
            try
            {
                // Previous Save and Close operations can leave a non-modal editor behind.
                // Reuse a real Approve Level editor; otherwise clear stale dialogs before New.
                form = Retry(FindOpenForm, 1000) ?? OpenForm();
            }
            catch (Exception ex) { LogFailure(stage, $"เปิดฟอร์มไม่สำเร็จ: {ex.Message}"); return; }

            var tab = GetGeneralTab(form);

            foreach (var testCase in Step2_AddValidationCases)
            {
                RunStep(testCase.TestCaseId,
                    () => FillAddValidationAndAssert(tab, form, testCase),
                    testCase.Progress, testCase.ProgressMsg);
            }

            // เคสเสริมเฉพาะหน้าจอ: พฤติกรรมเมื่อปิด Dialog ข้อความเตือน (Yes / Cancel / No)
            const string dupName = "TestCase04_DuplicateFlowCode";
            RunStep($"{dupName}_CloseSequence",
                () => RunCloseDialogSequence(form, dupName, "รหัสผังการอนุมัติ ห้ามซ้ำ"),
                40, "Validating: Duplicate Code Close Sequence...");
        }

        /// <summary>
        /// ขั้นที่ 3: เลือกแก้ไขรายการในตาราง (เอาติ๊กส่งอีเมลออก)
        /// </summary>
        public void RunEditTests()
        {
            TestProgressHelper.Report(42, "Starting Edit Operations...");

            for (int i = 0; i < Step3_EditCases.Length; i++)
            {
                var item = Step3_EditCases[i];
                try
                {
                    var form = ClickRowAndOpenEdit(item.TargetCode);
                    foreach (var id in item.UncheckIds) SetCheck(form, id, false);

                    if (!TrySaveAndHandleDuplicate(form, item.Name, item.TargetCode, item.SaveBtn)) continue;
                    if (item.NeedsManualClose) form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();

                    WaitForClosed(() => FindOpenForm());
                    RefreshGrid();

                    // เปิดกลับมาตรวจว่าค่าที่ติ๊กออกถูกบันทึกจริง
                    bool saved = VerifyChecksOff(item.TargetCode, item.UncheckIds);
                    if (saved)
                        LogSuccess(item.Name, $"แก้ไข '{item.TargetCode}' สำเร็จ (ติ๊กออกครบและบันทึกแล้ว)");
                    else
                        LogFailure(item.Name, $"แก้ไข '{item.TargetCode}' แล้ว แต่ค่าติ๊กไม่ถูกบันทึก");
                }
                catch (Exception ex)
                {
                    LogFailure(item.Name, $"แก้ไข '{item.TargetCode}' ไม่สำเร็จ: {ex.Message}");
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
            const string baseName = "TestCase07_EditValidation";
            TestProgressHelper.Report(58, "Opening Edit Form for Validation Tests...");

            Window form;
            try { form = ClickRowAndOpenEdit(EditTargetRow); }
            catch (Exception ex) { LogFailure(baseName, $"เปิดฟอร์ม Edit ไม่สำเร็จ: {ex.Message}"); return; }

            var tab = GetGeneralTab(form);

            foreach (var testCase in Step4_EditValidationCases)
            {
                RunStep(testCase.TestCaseId,
                    () => FillValidationAndAssert(tab, form, testCase.Code, testCase.NameTh, testCase.SaveBtn, testCase.TestCaseId, testCase.ExpectedErrors),
                    testCase.Progress, testCase.ProgressMsg);
            }

            // กรอก APR001 / อนุมัติลา (ซ้ำ) แล้วทดสอบ Close -> Yes / Cancel / No
            const string dupSub = $"{baseName}_DuplicateCode";
            RunStep($"{dupSub}_CloseSequence", () =>
            {
                TypeText(tab, CodeFieldId, Step4_DupCode);   // พิมพ์ด้วยคีย์บอร์ด เพื่อให้โปรแกรมรู้ว่าฟอร์มถูกแก้ไข (จึงถาม Pop-up ตอน Close)
                TypeText(tab, NameThFieldId, Step4_DupName);
                RunCloseDialogSequence(form, dupSub, "รหัสผังการอนุมัติ ห้ามซ้ำ" + EditValidationSuffix);
            }, 80, "Validating Edit: Duplicate Code Close Sequence...");

            LogSuccess(baseName, "รัน Validation Edit ครบทุกเคสแล้ว");
        }

        /// <summary>
        /// ขั้นที่ 5: ทดสอบการลบรายการในตาราง
        /// </summary>
        public void RunDeleteTests()
        {
            const string baseName = "TestCase08_Delete";
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

        /// <summary>กรอกรหัส/ชื่อผัง, เลือกเมนู, เลือกผู้อนุมัติแถวที่ 1, เลือก Radio "กำหนดเอง", กรอกอีเมล</summary>
        private void FillFormData(Window form, AutomationElement tab, string code, string nameTh, string? menu, string? empCode, string? email)
        {
            SetText(tab, CodeFieldId, code);
            SetText(tab, NameThFieldId, nameTh);

            // เหมือนหน้า BANK: ค่าว่าง = เคลียร์ช่องนั้นในฟอร์มเดิม (ใช้ฟอร์มเดียวต่อเนื่อง)
            if (!string.IsNullOrEmpty(menu)) SelectMenu(form, menu);
            else { try { SetText(form, MenuFieldId, ""); } catch { } }

            if (!string.IsNullOrEmpty(empCode)) SelectApprover(form, 0, empCode);

            ClickGridCell(form, 0, ColEmailCustom);          // Radio "กำหนดเอง"
            SetGridCellText(form, 0, ColEmailAddress, email ?? "");
        }

        /// <summary>Validation ตอน Add: กรอกครบทุกช่องตามเคส แล้วกด Save และตรวจข้อความ</summary>
        private void FillAddValidationAndAssert(AutomationElement tab, Window form, ValidationItem tc)
        {
            FillFormData(form, tab, tc.Code, tc.NameTh, tc.Menu, tc.EmpCode, tc.Email);
            (Retry(() => form.FindFirstDescendant(cf => cf.ByName(tc.SaveBtn)), 3000) ?? throw new Exception($"ไม่พบปุ่ม {tc.SaveBtn}")).Click();
            AssertTaskResult(form, tc.ExpectedErrors, tc.TestCaseId);
        }

        /// <summary>Validation ตอน Edit: แก้เฉพาะรหัส + ชื่อผัง</summary>
        private void FillValidationAndAssert(AutomationElement tab, Window form, string code, string name, string saveBtn, string testName, params string[] msgs)
        {
            SetText(tab, CodeFieldId, code);
            SetText(tab, NameThFieldId, name);
            (Retry(() => form.FindFirstDescendant(cf => cf.ByName(saveBtn)), 3000) ?? throw new Exception($"ไม่พบปุ่ม {saveBtn}")).Click();
            AssertTaskResult(form, msgs, testName);
        }

        private static string? DetectDuplicateMessage(IEnumerable<string> texts)
        {
            if (texts.Any(t => t.Contains("รหัสผังการอนุมัติ ห้ามซ้ำ"))) return "รหัสซ้ำ";
            if (texts.Any(t => t.Contains("ชื่อผังการอนุมัติ ห้ามซ้ำ"))) return "ชื่อซ้ำ";
            return null;
        }

        private AutomationElement GetGeneralTab(Window form) =>
            Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(GeneralTabId)), 3000) ?? form;

        private Window? FindOpenForm()
        {
            try
            {
                var modal = _mainWindow.AsWindow()?.ModalWindows
                    .FirstOrDefault(w => w.FindFirstDescendant(cf => cf.ByAutomationId(CodeFieldId)) != null);
                if (modal != null) return modal;
            }
            catch { }

            try
            {
                var named = _automation.GetDesktop().FindFirstChild(cf => cf.ByAutomationId(FormId))?.AsWindow();
                if (named?.FindFirstDescendant(cf => cf.ByAutomationId(CodeFieldId)) != null) return named;
            }
            catch { }
            return FindFormByCodeField();
        }

        /// <summary>สำรอง: หาหน้าต่างของโปรแกรมที่มีช่องรหัสผังการอนุมัติ (เผื่อฟอร์มไม่ได้ถูกแสดงเป็น modal)</summary>
        private Window? FindFormByCodeField()
        {
            try
            {
                int pid = _mainWindow.Properties.ProcessId.ValueOrDefault;
                var windows = _automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(pid)).ToList();
                windows.AddRange(_mainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Window)));
                foreach (var w in windows)
                {
                    try
                    {
                        if (!w.BoundingRectangle.IsEmpty && w.FindFirstDescendant(cf => cf.ByAutomationId(CodeFieldId)) != null)
                            return w.AsWindow();
                    }
                    catch { }
                }
            }
            catch { }
            return null;
        }

        // ---- Checkbox ----
        private void SetCheck(Window form, string id, bool value)
        {
            var el = Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(id)), 3000)
                     ?? throw new Exception($"ไม่พบ Checkbox {id}");
            var cb = el.AsCheckBox();
            if ((cb.IsChecked == true) == value) return;
            try { cb.Toggle(); } catch { cb.Click(); }
        }

        private bool VerifyChecksOff(string code, string[] ids)
        {
            var form = ClickRowAndOpenEdit(code);
            bool allOff = ids.All(id =>
            {
                var el = form.FindFirstDescendant(cf => cf.ByAutomationId(id));
                return el != null && el.AsCheckBox().IsChecked != true;
            });
            DiscardForm(form);
            return allOff;
        }

        // ---- ชื่อเมนู (ButtonEdit + Lookup) ----
        /// <summary>เทียบข้อความโดยไม่สนเว้นวรรค/ตัวพิมพ์ (ชื่อเมนูจริงอาจเป็น "โอนย้าย / โยกย้าย" แต่ TestStep เขียนติดกัน)</summary>
        private static bool SameText(string? a, string? b) =>
            string.Equals(new string((a ?? "").Where(c => !char.IsWhiteSpace(c)).ToArray()),
                          new string((b ?? "").Where(c => !char.IsWhiteSpace(c)).ToArray()),
                          StringComparison.OrdinalIgnoreCase);

        private void SelectMenu(Window form, string menu)
        {
            var field = Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(MenuFieldId)), 3000)
                        ?? throw new Exception($"ไม่พบช่อง {MenuFieldId}");
            var btn = field.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button));
            if (btn != null) btn.Click();
            else
            {
                var r = field.BoundingRectangle;                       // คลิกที่ปุ่ม ... ด้านขวาสุดของช่อง
                Mouse.Click(new Point(r.Right - 8, r.Top + r.Height / 2));
            }
            PickFromLookup(form, menu);

            // ตรวจว่าช่องแสดงเมนูที่เลือกจริง (กันกรณีเลือกพลาดแล้วค่าเดิมค้างอยู่โดยเทสไม่รู้ตัว)
            string actual = "";
            WaitUntil(() =>
            {
                try
                {
                    actual = field.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault
                             ?? field.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit))?.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault
                             ?? "";
                    return SameText(actual, menu);
                }
                catch { return false; }
            }, 2500);
            if (!SameText(actual, menu))
                throw new Exception($"เลือกเมนู '{menu}' แล้ว แต่ช่องชื่อเมนูแสดง '{actual}'");
        }

        // ---- ผู้อนุมัติ (ปุ่ม ... ในเซลล์ รหัสผู้อนุมัติ) ----
        private void SelectApprover(Window form, int row, string empCode)
        {
            var cell = GetGridCell(form, row, ColApproverCode);

            // ถ้าเซลล์มีรหัสผู้อนุมัติที่ต้องการอยู่แล้ว (เช่น เคส Validation ที่ใช้ฟอร์มเดิมต่อเนื่อง) ไม่ต้องเลือกซ้ำ
            try
            {
                string current = cell.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault ?? cell.Name ?? "";
                if (string.Equals(current.Trim(), empCode, StringComparison.OrdinalIgnoreCase)) return;
            }
            catch { }

            cell.Click();                                              // เข้า edit mode ให้ปุ่ม ... โผล่ในเซลล์
            System.Threading.Thread.Sleep(300);
            var grid = GetGrid(form);
            var cellRect = cell.BoundingRectangle;

            // ปุ่ม ... อยู่ทางขวาของเซลล์ที่กำลังแก้ (ชื่ออาจไม่ใช่ 'Editing control') → หาจากตำแหน่ง
            var btn = Retry(() => grid.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                .Where(b =>
                {
                    try
                    {
                        var r = b.BoundingRectangle;
                        if (r.IsEmpty) return false;
                        int cy = r.Top + r.Height / 2;
                        return cy >= cellRect.Top && cy <= cellRect.Bottom && r.Left >= cellRect.Left && r.Right <= cellRect.Right + 4;
                    }
                    catch { return false; }
                })
                .OrderByDescending(b => b.BoundingRectangle.Left).FirstOrDefault(), 2000);

            if (btn != null) btn.Click();
            else Mouse.Click(new Point(cellRect.Right - 10, cellRect.Top + cellRect.Height / 2));   // คลิกที่ตำแหน่งปุ่ม ... ขอบขวาเซลล์
            PickFromLookup(form, empCode);
        }

        /// <summary>หน้าต่างเลือกข้อมูล: ดับเบิลคลิกรายการที่ตรงกับข้อความ</summary>
        private void PickFromLookup(Window form, string text)
        {
            long formHandle = form.Properties.NativeWindowHandle.ValueOrDefault;
            var dlg = Retry(() => form.ModalWindows.FirstOrDefault()
                                  ?? _mainWindow.AsWindow()?.ModalWindows
                                       .FirstOrDefault(w => w.Properties.NativeWindowHandle.ValueOrDefault != formHandle)
                                  ?? FindLookupByButtons(formHandle), 8000)
                      ?? throw new Exception("ไม่พบหน้าต่างเลือกข้อมูล");

            // ข้อความของเซลล์อยู่ใน Value (Name เป็นชื่อโครงสร้างเช่น "ชื่อเมนู row 3") จึงเช็คทั้งสอง และต้องตรงทั้งข้อความ
            // (กัน "บันทึกขออนุมัติลา" ไปตรงกับ "บันทึกขออนุมัติลาแบบกลุ่ม")
            AutomationElement? FindItem() => dlg.FindAllDescendants().FirstOrDefault(e =>
            {
                try
                {
                    if (e.BoundingRectangle.IsEmpty) return false;
                    // ไม่นับช่องกรอก/ปุ่ม (เช่น ช่อง Look for ที่เพิ่งพิมพ์ข้อความนี้ลงไป) ให้นับเฉพาะเซลล์ในตาราง
                    var type = e.Properties.ControlType.ValueOrDefault;
                    if (type == ControlType.Edit || type == ControlType.ComboBox || type == ControlType.Button
                        || type == ControlType.Window || type == ControlType.TitleBar || type == ControlType.Text) return false;
                    if (SameText(e.Name, text)) return true;
                    var v = e.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault;
                    return SameText(v, text);
                }
                catch { return false; }
            });

            var item = Retry(FindItem, 3000);
            if (item == null) item = ScrollDialogToFind(dlg, FindItem);
            if (item == null)
            {
                // รายการอาจอยู่นอกจอ (ตารางเลื่อน) → พิมพ์ค้นในช่อง Look for แล้วกด Find
                try
                {
                    var lookFor = dlg.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit))
                        .Where(e => !e.BoundingRectangle.IsEmpty).OrderByDescending(e => e.BoundingRectangle.Width).FirstOrDefault();
                    if (lookFor != null && lookFor.Patterns.Value.IsSupported)
                    {
                        lookFor.Patterns.Value.Pattern.SetValue(text);
                        dlg.FindFirstDescendant(cf => cf.ByName("Find"))?.Click();
                    }
                }
                catch { }
                item = Retry(FindItem, 4000);
            }
            if (item == null && System.Text.RegularExpressions.Regex.IsMatch(text, @"^EMP\d+$"))
            {
                // รหัสผู้อนุมัติตาม TestStep ไม่มีในรายการ (รายการมาจากหน้าสิทธิ์การอนุมัติ ซึ่งมีผู้อนุมัติไม่ครบ)
                // → ล้างช่อง Look for แล้วใช้ผู้อนุมัติที่มีอยู่แทน โดยวนตามลำดับเลขรหัส
                try
                {
                    var lookFor = dlg.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit))
                        .Where(e => !e.BoundingRectangle.IsEmpty).OrderByDescending(e => e.BoundingRectangle.Width).FirstOrDefault();
                    lookFor?.Patterns.Value.PatternOrDefault?.SetValue("");
                    dlg.FindFirstDescendant(cf => cf.ByName("Find"))?.Click();
                }
                catch { }

                List<AutomationElement> available = new();
                Retry(() =>
                {
                    available = dlg.FindAllDescendants()
                        .Where(e =>
                        {
                            try
                            {
                                if (e.BoundingRectangle.IsEmpty) return false;
                                var v = e.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault ?? e.Name ?? "";
                                return System.Text.RegularExpressions.Regex.IsMatch(v.Trim(), @"^EMP\d+$");
                            }
                            catch { return false; }
                        })
                        .OrderBy(e => e.BoundingRectangle.Top).ThenBy(e => e.BoundingRectangle.Left).ToList();
                    return available.Count > 0 ? available : null;
                }, 4000);

                if (available.Count > 0)
                {
                    int number = int.Parse(System.Text.RegularExpressions.Regex.Match(text, @"\d+").Value);
                    item = available[(Math.Max(number, 1) - 1) % available.Count];
                    string used = item.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault ?? item.Name ?? "";
                    Debug.WriteLine($"ไม่พบผู้อนุมัติ '{text}' ในรายการ (มี {available.Count} คน) ใช้ '{used}' แทน");
                }
            }
            if (item == null)
            {
                // ต้องปิด dialog ที่ค้างเสมอ ไม่งั้นเคสถัดไปจะพิมพ์ข้อมูลลงใน dialog นี้
                try { (dlg.FindFirstDescendant(cf => cf.ByName("Cancel")) ?? dlg.FindFirstDescendant(cf => cf.ByName("ยกเลิก")))?.Click(); } catch { }
                WaitForClosed(() => dlg, 2000);
                throw new Exception($"ไม่พบรายการ '{text}' ในหน้าต่างเลือกข้อมูล");
            }
            // คลิกเลือกแถวที่ต้องการก่อน แล้วกด Select (ห้ามกด Select ทันทีโดยไม่เลือก เพราะจะได้แถวแรกที่ไฮไลต์อยู่)
            item.Click();
            System.Threading.Thread.Sleep(300);
            var select = dlg.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("Select").Or(cf.ByName("เลือก"))))
                         ?? dlg.FindFirstDescendant(cf => cf.ByName("OK"));
            if (select != null) select.Click(); else item.DoubleClick();

            // ถ้ากด Select แล้วหน้าต่างยังไม่ปิด ลองดับเบิลคลิกรายการเดิมอีกครั้ง
            if (!WaitForClosed(() => dlg, 2000))
            {
                try { item.DoubleClick(); } catch { }
                WaitForClosed(() => dlg);
            }
        }

        /// <summary>ตารางใน dialog แสดงเฉพาะแถวที่อยู่บนจอ → เลื่อนลงทีละน้อยแล้วหาใหม่ จนเจอหรือเลื่อนสุด (เนื้อหาไม่เปลี่ยน)</summary>
        private AutomationElement? ScrollDialogToFind(Window dlg, Func<AutomationElement?> find)
        {
            var grid = dlg.FindAllDescendants(cf => cf.ByControlType(ControlType.Table)
                    .Or(cf.ByControlType(ControlType.DataGrid)).Or(cf.ByControlType(ControlType.List)))
                .Where(g => { try { return !g.BoundingRectangle.IsEmpty; } catch { return false; } })
                .OrderByDescending(g => g.BoundingRectangle.Width * g.BoundingRectangle.Height).FirstOrDefault();
            if (grid == null) return null;

            var r = grid.BoundingRectangle;
            var center = new Point(r.Left + r.Width / 2, r.Top + r.Height / 2);
            string previous = "";
            for (int i = 0; i < 20; i++)
            {
                Mouse.MoveTo(center);
                Mouse.Scroll(-3);
                System.Threading.Thread.Sleep(250);

                var found = find();
                if (found != null) return found;

                string signature = string.Join("|", grid.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem))
                    .Select(e => e.Name ?? "").Take(60));
                if (signature == previous) break;   // เลื่อนสุดแล้ว
                previous = signature;
            }
            return null;
        }

        /// <summary>หา dialog เลือกข้อมูลที่ไม่ได้เป็น modal: หน้าต่างของโปรแกรมที่มีปุ่ม Select และ Cancel (ไม่ใช่ฟอร์มหลักของหน้าจอ)</summary>
        private Window? FindLookupByButtons(long formHandle)
        {
            var windows = new List<AutomationElement>();
            try
            {
                int pid = _mainWindow.Properties.ProcessId.ValueOrDefault;
                windows.AddRange(_automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(pid)));
            }
            catch { }
            try { windows.AddRange(_mainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Window))); } catch { }

            foreach (var w in windows.Distinct())
            {
                try
                {
                    if (w.Properties.NativeWindowHandle.ValueOrDefault == formHandle) continue;
                    if (w.BoundingRectangle.IsEmpty) continue;
                    if (w.FindFirstDescendant(cf => cf.ByAutomationId(CodeFieldId)) != null) continue;   // ฟอร์มหน้าจอเอง
                    var buttons = w.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
                    bool hasSelect = buttons.Any(b => b.Name == "Select" || b.Name == "เลือก");
                    bool hasCancel = buttons.Any(b => b.Name == "Cancel" || b.Name == "ยกเลิก");
                    if (hasSelect && hasCancel) return w.AsWindow();
                }
                catch { }
            }
            return null;
        }

        // ---- Grid ผู้อนุมัติ ----
        private AutomationElement GetGrid(Window form) =>
            Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(GridId)), 3000)
            ?? throw new Exception($"ไม่พบตาราง {GridId}");

        private AutomationElement GetGridCell(Window form, int row, int col)
        {
            var grid = GetGrid(form);

            // เซลล์ของ grid นี้เป็น DataItem ชื่อ "<ชื่อคอลัมน์> row N" (ไม่ได้เป็นลูกของ element แถว)
            // → รวมเซลล์ที่เลขแถวตรงกัน แล้วเรียงซ้ายไปขวาเป็นลำดับคอลัมน์
            var suffix = new System.Text.RegularExpressions.Regex($@"\brow {row}$");
            List<AutomationElement> cells = new();
            Retry(() =>
            {
                cells = grid.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem))
                    .Where(e => { try { return suffix.IsMatch(e.Name ?? "") && !e.BoundingRectangle.IsEmpty; } catch { return false; } })
                    .OrderBy(e => e.BoundingRectangle.Left).ToList();
                return cells.Count > col ? cells : null;
            }, 3000);

            if (cells.Count <= col)
            {
                // สำรอง: โครงสร้างแบบแถวที่มีเซลล์เป็นลูก
                var rowElements = grid.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem));
                if (rowElements.Length > row)
                {
                    var children = rowElements[row].FindAllChildren();
                    if (col < children.Length) return children[col];
                }
                var names = string.Join(", ", grid.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem)).Take(12).Select(e => $"'{e.Name}'"));
                throw new Exception($"แถวที่ {row + 1} มีเพียง {cells.Count} เซลล์ (ต้องการคอลัมน์ {col + 1}); DataItem ที่เห็น: {names}");
            }
            return cells[col];
        }

        private void ClickGridCell(Window form, int row, int col) => GetGridCell(form, row, col).Click();

        private void SetGridCellText(Window form, int row, int col, string text)
        {
            GetGridCell(form, row, col).Click();
            Keyboard.Press(VirtualKeyShort.CONTROL);
            Keyboard.Type("a");
            Keyboard.Release(VirtualKeyShort.CONTROL);
            Keyboard.Press(VirtualKeyShort.DELETE);            // เคลียร์ค่าเดิมก่อนเสมอ
            if (!string.IsNullOrEmpty(text)) Keyboard.Type(text);
            Keyboard.Press(VirtualKeyShort.ENTER);
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

        public ApproveLevelInfoUC(AutomationElement mainWindow, UIA3Automation automation)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

        private Window GetOrOpenForm() => Retry(FindOpenForm, 1500) ?? OpenForm();

        /// <summary>กรอกข้อความด้วยคีย์บอร์ดจริง (Ctrl+A แล้วพิมพ์) ต่างจาก SetText ที่ตั้งค่าผ่าน API ซึ่งโปรแกรมอาจไม่นับว่าฟอร์มถูกแก้ไข</summary>
        private void TypeText(AutomationElement parent, string id, string text)
        {
            var txt = Retry(() => parent.FindFirstDescendant(cf => cf.ByAutomationId(id)), 3000)
                      ?? throw new Exception($"ไม่พบช่อง {id}");

            string Current()
            {
                try { return txt.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault ?? ""; } catch { return ""; }
            }

            // โปรแกรมช้าอาจรับตัวอักษรไม่ครบ → พิมพ์แล้วตรวจค่าจริง ถ้าไม่ตรงลองใหม่ (สูงสุด 3 ครั้ง)
            for (int attempt = 0; attempt < 3; attempt++)
            {
                txt.Click();
                System.Threading.Thread.Sleep(200);
                Keyboard.Press(VirtualKeyShort.CONTROL);
                Keyboard.Type("a");
                Keyboard.Release(VirtualKeyShort.CONTROL);
                Keyboard.Type(text);
                if (WaitUntil(() => Current().Trim() == text, 2000)) return;
            }
            throw new Exception($"พิมพ์ '{text}' ลงช่อง {id} แล้ว ค่าในช่องคือ '{Current()}'");
        }

        private Window OpenForm()
        {
            Window? form = null;
            for (int attempt = 0; attempt < 3 && form == null; attempt++)
            {
                // หลังรีเฟรชตาราง/ปิดฟอร์มก่อนหน้า โปรแกรมอาจยังยุ่ง → รอจนปุ่ม New มองเห็นและกดได้จริงก่อนคลิก
                AutomationElement? newBtn = null;
                WaitUntil(() =>
                {
                    newBtn = _mainWindow.FindFirstDescendant(cf => cf.ByName("New"));
                    return newBtn != null && newBtn.Properties.IsEnabled.ValueOrDefault && !newBtn.Properties.IsOffscreen.ValueOrDefault;
                }, 20000);
                if (newBtn == null) throw new Exception("หาปุ่ม New ไม่เจอ");
                newBtn.Click();
                form = Retry(FindOpenForm, 8000);
            }
            if (form == null) throw new Exception("หาฟอร์มไม่เจอ");
            WaitUntil(() => form.FindFirstDescendant(cf => cf.ByAutomationId(FirstObjectID)) != null, 4000);
            return form;
        }

        /// <summary>ปิดฟอร์มทิ้ง (Close -> No) เพื่อเริ่มเคสถัดไปบนฟอร์มใหม่</summary>
        private void DiscardForm(Window? form)
        {
            if (form == null) return;
            try { ClickCloseAndHandleDialog(form, "No", "DiscardForm"); }
            catch
            {
                // ไม่มี Pop-up ถามยืนยัน (ไม่มีการแก้ไข) -> ปิดตรง ๆ
                try { form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click(); } catch { }
            }
            WaitForClosed(() => FindOpenForm());
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

        // รหัสผังที่เทสนี้สร้างขึ้น (เฉพาะรหัสเหล่านี้เท่านั้นที่จะถูกลบตอนเคลียร์ข้อมูลค้าง)
        private static readonly string[] TestDataCodes = { "APR001", "APR002", "APR003", "APR004", "APR005" };

        /// <summary>ลบแถว APR001-APR005 ที่ค้างอยู่ในตาราง (คลิกแถว -> Delete -> Yes) ให้ข้อมูลหน้านี้กลับสู่สภาพก่อนทดสอบ</summary>
        private void CleanupLeftoverTestData()
        {
            int deleted = 0;
            foreach (var code in TestDataCodes)
            {
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        RefreshGrid();
                        var row = Retry(() => FindGridElement(code), 1500);
                        if (row == null) break;   // ไม่มีข้อมูลค้างของรหัสนี้

                        WaitUntil(() => { try { row.Focus(); return true; } catch { return false; } }, 1000);
                        row.Click();

                        var delBtn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("Delete")), 3000)
                                     ?? throw new Exception("หาปุ่ม Delete ไม่เจอ");
                        delBtn.Click();

                        var dlg = Retry(() => _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault()
                                              ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("คำเตือน"))?.AsWindow(), 4000)
                                  ?? throw new Exception("กด Delete แล้วไม่พบ Pop-up ยืนยัน");
                        ClickDialogButton(dlg, "Yes");
                        WaitForClosed(() => dlg);
                        deleted++;
                        Debug.WriteLine($"เคลียร์ข้อมูลค้างจากรอบก่อน: ลบ '{code}'");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"เคลียร์ข้อมูลค้าง '{code}' ไม่สำเร็จ: {ex.Message}");
                        break;
                    }
                }
            }
            if (deleted == 0) Debug.WriteLine("ไม่มีข้อมูลทดสอบค้างจากรอบก่อน");
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

        /// <summary>วิธีเดียวกับหน้า BANK: พิมพ์ชื่อเมนูในช่องค้นหา แล้วเลือกผลลัพธ์</summary>
        private bool OpenMenuBySearch(string searchKeyword)
        {
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
        }

        /// <summary>ถอยไปเปิดแบบหน้า PrivilegetoApproveInfoUC: คลิกแท็บ Approve Center แล้วคลิกแถวเมนูในแผงซ้าย</summary>
        private bool OpenMenuByApproveCenterTab(string menuName)
        {
            var tab = Retry(() =>
                _mainWindow.FindFirstDescendant(cf => cf.ByName(ApproveCenterMenuName).And(cf.ByControlType(ControlType.TabItem)))
                ?? _mainWindow.FindFirstDescendant(cf => cf.ByName(ApproveCenterMenuName)), 5000)
                ?? throw new Exception($"หาเมนูหลัก '{ApproveCenterMenuName}' ไม่เจอ");
            tab.Click();

            var main = _mainWindow.BoundingRectangle;
            int limitX = main.Left + (int)(main.Width * MenuPanelWidthRatio);   // เมนูอยู่แผงซ้ายเท่านั้น

            var row = Retry(() => _mainWindow.FindAllDescendants().FirstOrDefault(e =>
            {
                var r = e.BoundingRectangle;
                if (r.IsEmpty || r.Width <= 0 || r.Height <= 0) return false;
                if (r.Left + r.Width / 2 > limitX) return false;
                var type = e.Properties.ControlType.ValueOrDefault;
                if (type == ControlType.TabItem || type == ControlType.Window || type == ControlType.TitleBar) return false;
                return string.Equals(e.Name?.Trim(), menuName, StringComparison.OrdinalIgnoreCase)
                    || (e.Patterns.Value.IsSupported
                        && string.Equals(e.Patterns.Value.Pattern.Value.Value?.Trim(), menuName, StringComparison.OrdinalIgnoreCase));
            }), 5000)
                ?? throw new Exception($"หาเมนู '{menuName}' ไม่เจอในแผงเมนูด้านซ้าย");

            bool NewReady() =>
                _mainWindow.FindFirstDescendant(cf => cf.ByName("New")) is { } b
                && b.Properties.IsEnabled.ValueOrDefault && !b.Properties.IsOffscreen.ValueOrDefault;

            row.Click();
            if (WaitUntil(NewReady, 3000)) return true;

            row.DoubleClick();                                  // บางเมนูของ DevExpress ต้องดับเบิลคลิก
            return WaitUntil(NewReady, 6000);
        }

        /// <summary>เปิดหน้าจอ: ค้นหาด้วยชื่อเมนู (แบบหน้า BANK) ก่อน ถ้าไม่สำเร็จค่อยเปิดจากแท็บ Approve Center</summary>
        private bool TryOpenMenu(string stage, string searchKeyword)
        {
            string searchError;
            try
            {
                if (OpenMenuBySearch(searchKeyword)) return true;
                searchError = "เปิดแล้วไม่พบปุ่ม New";
            }
            catch (Exception ex) { searchError = ex.Message; }

            try
            {
                if (OpenMenuByApproveCenterTab(searchKeyword)) return true;
                LogFailure(stage, $"เปิดหน้าจอไม่สำเร็จ: ค้นหาด้วยชื่อ ({searchError}) | เปิดจากแท็บ {ApproveCenterMenuName}: ไม่พบปุ่ม New");
            }
            catch (Exception ex)
            {
                LogFailure(stage, $"เปิดหน้าจอไม่สำเร็จ: ค้นหาด้วยชื่อ ({searchError}) | เปิดจากแท็บ {ApproveCenterMenuName}: {ex.Message}");
            }
            return false;
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

        private AutomationElement? FindGridElement(string val)
        {
            var grid = _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(GridId));
            var scope = grid?.FindAllDescendants() ?? _mainWindow.FindAllDescendants();
            return scope.FirstOrDefault(el =>
                (!string.IsNullOrEmpty(el.Name) && string.Equals(el.Name.Trim(), val, StringComparison.OrdinalIgnoreCase)) ||
                (el.Patterns.Value.IsSupported && string.Equals(el.Patterns.Value.Pattern.Value.Value?.Trim(), val, StringComparison.OrdinalIgnoreCase)));
        }

        private bool TrySaveAndHandleDuplicate(Window form, string testName, string code, string saveBtn)
        {
            var btn = form.FindFirstDescendant(cf => cf.ByName(saveBtn)) ?? throw new Exception($"ไม่พบปุ่ม {saveBtn}");
            try { btn.Click(); }
            catch (Exception ex) when (ex is TimeoutException || ex is System.Runtime.InteropServices.COMException)
            {
                // ตอนกด Save โปรแกรมอาจประมวลผลหนักจนไม่ตอบ UIA (Operation timed out) ทั้งที่การกดสำเร็จแล้ว
                // → รอให้โปรแกรมกลับมาตอบสนอง แล้วตรวจผลต่อ (Save and New/Close ตรวจสถานะฟอร์มหลังจากนี้เอง)
                Debug.WriteLine($"กด {saveBtn} แล้วโปรแกรมตอบสนองช้า ({ex.Message}) รอแล้วตรวจผลต่อ");
                System.Threading.Thread.Sleep(3000);
            }

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
