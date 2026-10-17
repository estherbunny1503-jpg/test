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
    public class EmpInfoUC
    {
        // =========================================================================================
        // หมวดที่ 1: ข้อมูลการทดสอบทั้ง 5 ขั้น (Test Data)
        // =========================================================================================

        // --- Data Records สำหรับใช้งานในแต่ละเฟส ---
        private sealed record AddItem(
            string Code,
            string CardNo,           // "" = ข้ามขั้นตอนเลือกหมายเลขบัตร
            string Title,
            string FirstName,
            string LastName,
            string Position,
            string Org,
            string EmpType,          // แท็บ การว่าจ้าง: ประเภทพนักงาน (Dropdown ▼)
            string EmpGroup,         // แท็บ การว่าจ้าง: กลุ่มพนักงาน (กด ... เลือก)
            string Level,            // แท็บ การว่าจ้าง: ระดับ (กด ... เลือก)
            string StartDate,
            string PayPeriod,
            string UserGroup,        // แท็บ กลุ่มผู้ใช้/รหัสผู้อนุมัติ: รหัสกลุ่มผู้ใช้ (devGridUserGroup)
            string Approver,         // แท็บ กลุ่มผู้ใช้/รหัสผู้อนุมัติ: รหัสผู้อนุมัติ — ติ๊ก Copy to Lower Level ก่อนเลือก ("" = ข้ามขั้นตอนนี้)
            string SaveBtn,
            bool NeedsManualClose,
            bool CancelPermission);  // Pop-up ชื่อเพอร์มิชชั่นหลังบันทึก: true = กด Cancel / false = กด ... → Select → OK
        private sealed record ValidationItem(
            string TestCaseId,
            string? Code,            // null = ไม่แตะช่อง, "" = ลบค่า (ใช้กับทุกช่องด้านล่าง)
            string? Title,
            string? FirstName,
            string? LastName,
            string? StartDate,
            string? Shift,
            string? PayPeriod,
            string SaveBtn,
            int Progress,
            string ProgressMsg,
            string[] ExpectedErrors);
        private sealed record EditItem(
            string Name,
            string OldCode,
            string NewCode,
            string PayPeriod,
            string SaveBtn,
            bool NeedsManualClose);
        private sealed record DeleteItem(
            string TargetCode,
            string DialogBtn,
            bool ExpectDeleted,
            int Progress,
            string ProgressMsg);

        // ข้อมูลที่ใช้ซ้ำหลายเคส
        private const string Shift = "NRM-OFF-NEW";
        // รูปแบบงวดการจ่ายตาม TestStep — ต้องมีชื่อนี้ในระบบก่อน (ไม่มีจะ FAIL พร้อมบอกรายการที่มีใน Pop-up)
        private const string PayOnce = "งวดการจ่ายเดือนละ 1 ครั้ง";
        private const string PayTwice = "งวดการจ่ายเดือนละ 2 ครั้ง";
        private const string Pay21To20 = "งวดการจ่ายวันที่ 21 เดือนก่อน-20เดือนปัจจุบัน";

        // [ขั้นที่ 1] ข้อมูลทดสอบการสร้างใหม่ (Add / Navigate) — TestStep แท็บ EmpInfoUC แถว 8-18
        // EMP00001-00002: ข้ามรหัสผู้อนุมัติ / EMP00007-00011: Pop-up ชื่อเพอร์มิชชั่นกด Cancel
        private static readonly AddItem[] Step1_AddCases =
        {
            new(
                "EMP00001",
                "EMP0001",
                "นาย",
                "กิตติพงษ์",
                "ใจดี",
                "H-001",
                "RP001",
                "พนักงานรายเดือน",
                "EG-001",
                "EL-001",
                "01/1/2569",
                PayOnce,
                "GRP003",
                "",
                "Save",
                true,
                false),
            new(
                "EMP00002",
                "EMP0002",
                "นางสาว",
                "พิมพ์ชนก",
                "แสงทอง",
                "P-001",
                "ORG003",
                "พนักงานรายวัน",
                "EG-002",
                "EL-002",
                "15/01/2569",
                PayTwice,
                "GRP004",
                "",
                "Save and New",
                false,
                false),
            new(
                "EMP00003",
                "EMP0003",
                "นาย",
                "ธนกร",
                "สุขใจ",
                "DW001",
                "ORG004",
                "พนักงานรายเดือน",
                "EG-003",
                "EL-003",
                "01/02/2569",
                PayOnce,
                "GRP005",
                "EMP00002",
                "Save and New",
                false,
                false),
            new(
                "EMP00004",
                "EMP0004",
                "นาย",
                "ภานุ",
                "แก้วมณี",
                "POS003",
                "ORG005",
                "พนักงานรายเหมา",
                "EG-004",
                "EL-004",
                "15/02/2569",
                PayTwice,
                "HRM01",
                "EMP00001",
                "Save and New",
                false,
                false),
            new(
                "EMP00005",
                "",
                "นางสาว",
                "ศิริพร",
                "วัฒนชัย",
                "POS004",
                "RP001",
                "พนักงานรายเดือน",
                "EG-001",
                "EL-005",
                "08/11/2566",
                PayOnce,
                "GRP004",
                "EMP00002",
                "Save and New",
                false,
                false),
            new(
                "EMP00006",
                "",
                "นางสาว",
                "ชลธิชา",
                "รัตนวงศ์",
                "POS005",
                "ORG003",
                "พนักงานรายชั่วโมง",
                "EG-002",
                "EL-001",
                "08/11/2542",
                PayTwice,
                "GRP005",
                "EMP00001",
                "Save and New",
                false,
                false),
            new(
                "EMP00007",
                "",
                "นางสาว",
                "สุภัสสรา",
                "วัฒนานนท์",
                "H-001",
                "ORG004",
                "พนักงานรายเดือน",
                "EG-003",
                "EL-002",
                "01/03/2569",
                PayOnce,
                "",
                "",
                "Save and New",
                false,
                true),
            new(
                "EMP00008",
                "",
                "นาย",
                "วรภัทร",
                "ศิริวัฒนา",
                "POS005",
                "ORG005",
                "พนักงานรายวัน",
                "EG-001",
                "EL-003",
                "01/03/2569",
                PayTwice,
                "GRP003",
                "EMP00001",
                "Save and New",
                false,
                true),
            new(
                "EMP00009",
                "",
                "นางสาว",
                "นภัสสร",
                "ธนโชติ",
                "DW001",
                "RP001",
                "พนักงานรายชั่วโมง",
                "EG-002",
                "EL-004",
                "15/03/2569",
                PayOnce,
                "GRP004",
                "EMP00002",
                "Save and New",
                false,
                true),
            new(
                "EMP00010",
                "",
                "นาย",
                "ธีรเดช",
                "พัฒนกุล",
                "POS003",
                "ORG003",
                "พนักงานรายเดือน",
                "EG-003",
                "EL-005",
                "21/04/2569",
                PayTwice,
                "GRP005",
                "EMP00001",
                "Save and New",
                false,
                true),
            new(
                "EMP00011",
                "",
                "นางสาว",
                "กานต์พิชชา",
                "รัตนวงศ์",
                "POS004",
                "ORG004",
                "พนักงานรายเหมา",
                "EG-004",
                "EL-001",
                "20/05/2569",
                PayOnce,
                "HRM01",
                "EMP00002",
                "Save and Close",
                false,
                true)
        };

        // [ขั้นที่ 2] ข้อมูลทดสอบ Validation ตอนเพิ่ม (Add Validation)
        private static readonly ValidationItem[] Step2_AddValidationCases =
        {
            new("TestCase01_MissingEmployeeCode", "", "นางสาว", "พิมพ์ชนก", "ศรีวัฒนา", "01/05/2569", Shift, PayTwice, "Save", 25,
                "Validating: Missing Employee Code...", new[] { "รหัสพนักงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล" }),

            new("TestCase02_MissingEmployeeName", "", null, "", "", "01/05/2569", null, Pay21To20, "Save and New", 29,
                "Validating: Missing Employee Name...", new[] { "ชื่อพนักงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล", "สัญชาติไทย ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล" }),

            new("TestCase03_MissingShiftAndPayPeriod", "EMP08", "นางสาว", "พิมพ์ชนก", "ศรีวัฒนา", "01/05/2569", "", "", "Save and New", 33,
                "Validating: Missing Shift and Pay Period...", new[] { "รหัสกะงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล", "งวดการจ่าย ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล" }),

            new("TestCase04_DuplicateEmployeeCode", "EMP00001", "นางสาว", "พิมพ์ชนก", "ศรีวัฒนา", "01/05/2569", Shift, Pay21To20, "Save and New", 37,
                "Validating: Duplicate Employee Code...", new[] { "รหัสพนักงาน ห้ามซ้ำ กรุณาป้อนข้อมูล" })
        };

        // [ขั้นที่ 3] ข้อมูลทดสอบการแก้ไขข้อมูล (Edit Records)
        private static readonly EditItem[] Step3_EditCases =
        {
            new(
                "TestCase06_EditEmp005",
                "EMP00005",
                "EP0005",
                PayTwice,
                "Save",
                true),
            new(
                "TestCase07_EditEmp006",
                "EMP00006",
                "EP0006",
                Pay21To20,
                "Save and Close",
                false)
        };

        // [ขั้นที่ 4] ข้อมูลทดสอบ Validation ตอนแก้ไข (Edit Validation)
        private const string EditTargetRow = "EMP00002";
        private static readonly ValidationItem[] Step4_EditValidationCases =
        {
            new("TestCase08_EditValidation_MissingEmployeeCode", "", null, null, null, null, null, null, "Save", 64,
                "Validating Edit: Missing Code...", new[] { "รหัสพนักงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล" }),

            new("TestCase08_EditValidation_MissingEmployeeName", "EMP06", null, "", "", null, null, null, "Save and New", 70,
                "Validating Edit: Missing Name...", new[] { "ชื่อพนักงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล", "สัญชาติไทย ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล" }),

            new("TestCase08_EditValidation_DuplicateEmployeeCode", "EMP00001", null, "สุภัสสรา", "วัฒนานนท์", null, null, null, "Save and New", 76,
                "Validating Edit: Duplicate Code...", new[] { "รหัสพนักงาน ห้ามซ้ำ กรุณาป้อนข้อมูล" })
        };

        // [ขั้นที่ 5] ข้อมูลทดสอบการลบข้อมูล (Delete Records)
        private static readonly DeleteItem[] Step5_DeleteCases =
        {
            new("EMP00007", "Yes", true, 92, "Delete Test: EMP00007 (confirmed delete)..."),
            new("EP0006", "No", false, 100, "Delete Test: EP0006 (cancelled delete)...")
        };


        // =========================================================================================
        // หมวดที่ 2: ส่วนที่ขึ้นกับหน้าจอนั้นๆ (Screen-Specific Implementation)
        // **เมื่อเปลี่ยนไปทำหน้าจออื่น ต้องมาแก้ไขค่าและเมทอดในส่วนนี้ทั้งหมด**
        // =========================================================================================

        // 2.1 Locator & Mappings ประจำหน้าจอ
        private const string MenuName = "บันทึกข้อมูลพนักงาน";
        private const string ScreenLogName = "EmpInfoUC";
        private const string GeneralTabId = "devTabPgGeneral";
        private const string FirstObjectID = "devTxtEmpCode";
        private const string TabCtrlId = "devTabCtrlEmpInfo";                       // ตัวควบคุมแท็บของฟอร์ม
        private const string EmploymentTabName = "การว่าจ้าง";
        private const string PaymentTabName = "การจ่ายเงิน";
        private const string UserGroupTabName = "กลุ่มผู้ใช้ / สิทธิ์อนุมัติ";   // ชื่อแท็บจริงในโปรแกรม (เดิมใส่ "กลุ่มผู้ใช้/รหัสผู้อนุมัติ" จึงหาไม่เจอ)
        private const int TaskResultTimeoutMs = 80000; // หน้านี้ตรวจข้อมูลก่อนบันทึกนาน ต้องรอข้อความใน Task Result นาน
        private const int EditFormOpenTimeoutMs = 15000; // [ใหม่] หน้านี้เปิดฟอร์ม Edit ช้า (เดิมรอ 5 วิ → "หาฟอร์มไม่เจอ")
        private const int DeleteWaitTimeoutMs = 20000;   // [ใหม่] เวลารอแถวหายจากตารางหลังกดลบ (กด Find ใหม่ทุกรอบ)

        // 2.2 ฟิลด์ Input ประจำหน้าจอ
        private const string CodeFieldId = "devTxtEmpCode";
        private const string CardNoFieldId = "devBtnCardNo";
        private const string TitleFieldId = "devLookUpTitle";
        private const string FirstNameFieldId = "devTxtFirstName";
        private const string LastNameFieldId = "devTxtLastName";
        private const string PositionFieldId = "devBtnEditPosition";
        private const string OrgFieldId = "devBtnEditOrgName";
        private const string StartDateFieldId = "devDtStartDate";
        private const string ShiftFieldId = "devBtnEditshift";
        private const string PayPeriodFieldId = "devBtnPeriod";

        // แท็บ การว่าจ้าง — 3 ช่องนี้ยังไม่ได้ยืนยัน AutomationId (ตั้งตามรูปแบบชื่อของหน้าจอนี้ไว้ก่อน)
        // ถ้า id ไม่ตรง จะหาจากป้ายชื่อช่องแทน และถ้ายังไม่เจอ log จะบอก id ของช่องที่เห็นในแท็บ → เอามาแก้ตรงนี้ได้เลย
        private const string EmpTypeFieldId = "devLookUpEmpType";     // ประเภทพนักงาน (Dropdown ▼)
        private const string EmpGroupFieldId = "devBtnEditEmpGroup";  // กลุ่มพนักงาน (กด ...)
        private const string LevelFieldId = "devBtnEditLevel";        // ระดับ (กด ...)
        private const string EmpTypeLabel = "ประเภทพนักงาน";
        private const string EmpGroupLabel = "กลุ่มพนักงาน";
        private const string LevelLabel = "ระดับ";

        // แท็บ กลุ่มผู้ใช้/รหัสผู้อนุมัติ — ตาราง + คำที่ใช้หาหัวคอลัมน์ (ไม่ต้องรู้ลำดับคอลัมน์ ใช้ตำแหน่ง X ของหัวคอลัมน์แทน)
        private const string UserGroupGridId = "devGridUserGroup";    // ตาราง รหัสกลุ่มผู้ใช้
        private const string SecurityGridId = "devGridSecurity";      // ตาราง รหัสผู้อนุมัติ
        private const string ColUserGroupKey = "รหัสกลุ่มผู้ใช้";      // ช่องในตาราง: ชื่อ "รหัสกลุ่มผู้ใช้ row 0"
        private const string ColApproverKey = "รหัสผู้อนุมัติ";        // ช่องในตาราง: ชื่อ "รหัสผู้อนุมัติ row 0" (แถวแรก = บันทึกขออนุมัติลา)
        private const string CopyLowerCheckName = "Copy to Lower Level";
        private const string CopyLowerCheckId = "devCheckCopy";       // ☑ Copy to Lower Level (checkbox แยก ไม่ได้อยู่ในตาราง)

        /// <summary>
        /// ขั้นที่ 1: เปิดหน้าจอและกรอกข้อมูลสร้างใหม่
        /// </summary>
        public void NavigateToEmpInfoUC()
        {
            const string stageName = "TestCase00_AddEmpInfo";
            TestLogger.LogInfo(ScreenLogName, $"เริ่มทดสอบหน้าจอ {MenuName}");
            TestProgressHelper.Report(2, $"Opening Personnel -> {MenuName} Menu via Search...");

            if (!TryOpenMenu(stageName, MenuName)) return;

            Window? form = null;
            for (int i = 0; i < Step1_AddCases.Length; i++)
            {
                var item = Step1_AddCases[i];
                string testName = $"{stageName}_{item.Code}";
                // เฉลี่ย Progress ให้อยู่ในช่วง 4 -> 20
                TestProgressHelper.Report(4 + (i * 16 / Step1_AddCases.Length), $"Adding {MenuName} ({i + 1}/{Step1_AddCases.Length}): {item.Code} - {item.FirstName}...");

                try
                {
                    form ??= GetOrOpenForm();
                    var tab = GetGeneralTab(form);
                    FillFormData(tab, form, item);

                    if (!TrySaveAndHandleDuplicate(form, testName, item.Code, item.SaveBtn))
                    {
                        form = null;
                        continue;
                    }
                    HandlePostSavePopup(form, item.CancelPermission); // เฉพาะหน้าจอนี้: Pop-up "กำหนดข้อมูลพนักงานเพิ่มเติม" หลังบันทึก

                    if (item.SaveBtn == "Save and New")
                    {
                        WaitUntil(() => string.IsNullOrEmpty(tab.FindFirstDescendant(cf => cf.ByAutomationId(FirstObjectID))?.AsTextBox().Text), 3000);
                        LogSuccess(testName, $"'{item.Code}' บันทึกสำเร็จ (Save and New)");
                        form = null; // เฉพาะหน้าจอนี้: Save and New เปิดฟอร์มใหม่ จึงต้องหาฟอร์มใหม่ทุกครั้ง
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
                    CloseFormWithoutSaving(form);   // [ใหม่] ไม่งั้นรายการถัดไปจะไปกรอกทับฟอร์มเดิมที่ค้างข้อมูลอยู่
                    form = null;
                }
            }
        }

        /* Method เดิมที่ชุดทดสอบอาจเรียกอยู่ — ทำงานเหมือน NavigateToEmpInfoUC */
        public void RunAddNewTests() => NavigateToEmpInfoUC();

        /// <summary>
        /// ขั้นที่ 2: รัน Validation ตอนสร้างข้อมูลใหม่
        /// </summary>
        public void RunAllValidationTests()
        {
            const string stage = "TestCase01-05_Validation";
            TestProgressHelper.Report(22, $"Opening {MenuName} Form for Validation Tests...");

            Window form;
            try { form = OpenForm(); }
            catch (Exception ex) { LogFailure(stage, $"เปิดฟอร์มไม่สำเร็จ: {ex.Message}"); return; }

            var tab = GetGeneralTab(form);

            foreach (var testCase in Step2_AddValidationCases)
            {
                RunStep(testCase.TestCaseId,
                    () =>
                    {
                        // [แก้] เคสก่อนล้มแล้วฟอร์มถูกปิดไป → เปิดฟอร์มใหม่ ไม่ให้เคสถัดไปล้มตามกันว่า "ไม่พบช่อง devTxtEmpCode"
                        HandlePostSavePopup(form, true);   // เคสก่อนบันทึกผ่านไปจริง → ปิด Pop-up เพอร์มิชชั่นที่ค้างก่อน
                        if (!IsFormUsable(form)) { form = FindOpenForm() ?? OpenForm(); tab = GetGeneralTab(form); }
                        FillValidationAndAssert(tab, form, testCase, testCase.TestCaseId);
                    },
                    testCase.Progress, testCase.ProgressMsg);
            }

            // เคสเสริมเฉพาะหน้าจอ: พฤติกรรมเมื่อปิด Dialog ข้อความเตือน
            const string dupName = "TestCase05_CloseSequence";
            RunStep(dupName,
                () => RunCloseDialogSequence(form, dupName, "รหัสพนักงาน ห้ามซ้ำ กรุณาป้อนข้อมูล"),
                40, "Validating: Duplicate Code Close Sequence...");
        }

        /// <summary>
        /// ขั้นที่ 3: เลือกแก้ไขรายการในตาราง
        /// </summary>
        public void RunEditTests()
        {
            TestProgressHelper.Report(42, "Starting Edit Operations...");
            CloseFormWithoutSaving(null);   // ฟอร์มจากขั้น Validation ที่อาจค้างอยู่ (ไม่งั้นกด Edit แล้ว "หาฟอร์มไม่เจอ")

            for (int i = 0; i < Step3_EditCases.Length; i++)
            {
                var item = Step3_EditCases[i];
                try
                {
                    var form = ClickRowAndOpenEdit(item.OldCode);
                    var tab = GetGeneralTab(form);
                    TypeText(tab, CodeFieldId, item.NewCode);
                    SelectTab(form, PaymentTabName);
                    SelectLookup(form, PayPeriodFieldId, item.PayPeriod);

                    if (!TrySaveAndHandleDuplicate(form, item.Name, item.NewCode, item.SaveBtn)) continue;
                    HandlePostSavePopup(form, false);
                    if (item.NeedsManualClose) form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();

                    WaitForClosed(() => FindOpenForm());
                    RefreshGrid();

                    bool recordFound = VerifyRecordInGrid(item.NewCode, refresh: false);
                    if (recordFound)
                        LogSuccess(item.Name, $"แก้ไข '{item.OldCode}' สำเร็จ (พบข้อมูลที่แก้ไขในตารางหลัก)");
                    else
                        LogFailure(item.Name, $"แก้ไข '{item.OldCode}' แล้ว แต่ไม่พบข้อมูลที่อัปเดตในตารางหลัก");
                }
                catch (Exception ex)
                {
                    LogFailure(item.Name, $"แก้ไข '{item.OldCode}' ไม่สำเร็จ: {ex.Message}");
                    // [ใหม่] ฟอร์มที่เปิดขึ้นมาช้า (หลังหมดเวลารอ) ต้องปิดทิ้ง ไม่งั้นเคสถัดไปจะหยิบฟอร์มนี้ไปแก้ผิดคน
                    CloseFormWithoutSaving(null);
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
            const string baseName = "TestCase08_EditValidation";
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

            // เฉพาะหน้าจอนี้: TestStep ให้กด Close แล้วกด No เท่านั้น (ไม่มี Yes / Cancel)
            string closeNo = $"{baseName}_Close_No";
            RunStep(closeNo,
                () =>
                {
                    ClickCloseAndHandleDialog(form, "No", closeNo);
                    AssertFormClosed(form, closeNo);
                },
                80, "Validating Edit: Close -> No...");

        }

        /// <summary>
        /// ขั้นที่ 5: ทดสอบการลบรายการในตาราง
        /// </summary>
        public void RunDeleteTests()
        {
            const string baseName = "TestCase09_Delete";
            TestProgressHelper.Report(84, "Starting Delete Tests...");

            foreach (var testCase in Step5_DeleteCases)
            {
                RunDeleteCase(baseName, testCase.TargetCode, testCase.DialogBtn, testCase.ExpectDeleted);
                TestProgressHelper.Report(testCase.Progress, testCase.ProgressMsg);
            }

            // [แก้] เดิม LogSuccess "ทดสอบ Delete ครบทุกเคส" เสมอ แม้ทุกเคสลบ FAIL → เหลือแค่บรรทัดสรุปผล
            TestLogger.LogInfo(ScreenLogName, $"สรุปผลการทดสอบทั้งหมด: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");
        }

        // --- Custom Mapping Logic ประจำหน้าจอนี้ ---

        /* กรอกข้อมูลพนักงานครบ 4 แท็บ (ทั่วไป, การว่าจ้าง, การจ่ายเงิน, กลุ่มผู้ใช้/รหัสผู้อนุมัติ) */
        private void FillFormData(AutomationElement tab, Window form, AddItem item)
        {
            TypeText(tab, CodeFieldId, item.Code);
            if (item.CardNo.Length > 0) SelectLookup(tab, CardNoFieldId, item.CardNo);
            SelectComboBoxItem(tab, TitleFieldId, item.Title);
            TypeText(tab, FirstNameFieldId, item.FirstName);
            TypeText(tab, LastNameFieldId, item.LastName);

            // แท็บ การว่าจ้าง: ตำแหน่ง → หน่วยงาน → ประเภทพนักงาน → กลุ่มพนักงาน → ระดับ → วันที่เริ่มต้น → ชม. การทำงานตามกะงาน (ลำดับตาม TestStep)
            SelectTab(form, EmploymentTabName);
            SelectLookup(form, PositionFieldId, item.Position);
            SelectLookup(form, OrgFieldId, item.Org);
            SelectDropdown(form, FindField(form, EmpTypeFieldId, EmpTypeLabel), EmpTypeLabel, item.EmpType);
            SelectLookupOn(FindField(form, EmpGroupFieldId, EmpGroupLabel), EmpGroupLabel, item.EmpGroup);
            SelectLookupOn(FindField(form, LevelFieldId, LevelLabel), LevelLabel, item.Level);
            TypeText(form, StartDateFieldId, item.StartDate);
            SelectLookup(form, ShiftFieldId, Shift);

            // แท็บ การจ่ายเงิน: รูปแบบงวดการจ่าย
            SelectTab(form, PaymentTabName);
            SelectLookup(form, PayPeriodFieldId, item.PayPeriod);

            // แท็บ กลุ่มผู้ใช้/รหัสผู้อนุมัติ: กด ... เลือกรหัสกลุ่มผู้ใช้ → ติ๊ก ☑ Copy to Lower Level (devCheckCopy) → กด ... เลือกรหัสผู้อนุมัติ
            // [แก้] TestStep EMP00007: รหัสกลุ่มผู้ใช้ และ รหัสผู้อนุมัติ "ข้ามขั้นตอนนี้ไป" ทั้งคู่ → ไม่ต้องเข้าแท็บนี้เลย
            //       เดิมส่งค่าว่างไปเลือกใน Pop-up → ค่าว่างตรงกับทุกแถว → ไปเลือกกลุ่มผู้ใช้แถวแรกให้ EMP00007 ทั้งที่ต้องข้าม
            if (item.UserGroup.Length > 0 || item.Approver.Length > 0)
            {
                SelectTab(form, UserGroupTabName);
                if (item.UserGroup.Length > 0)   // TestStep: EMP00007 ข้ามขั้นตอนรหัสกลุ่มผู้ใช้
                    SelectGridLookup(form, UserGroupGridId, ColUserGroupKey, item.UserGroup);
                if (item.Approver.Length > 0)    // TestStep: EMP00001-00002, EMP00007 ข้ามขั้นตอนรหัสผู้อนุมัติ
                {
                    TickCheckBox(form, CopyLowerCheckId);
                    SelectGridLookup(form, SecurityGridId, ColApproverKey, item.Approver);
                }
            }
        }

        /* หน้านี้มีหลายแท็บและหลายช่อง จึงรับข้อมูลทั้งเคส (null = ไม่แตะช่องนั้น, "" = ลบค่า) */
        private void FillValidationAndAssert(AutomationElement tab, Window form, ValidationItem tc, string testName)
        {
            // [แก้] เดิมเรียก GetGeneralTab แต่ไม่ใช้ค่าที่ได้ ใช้ tab ตัวเก่าที่หมดอายุไปแล้ว (เคสก่อนสลับไปแท็บอื่น)
            //       → "ไม่พบช่อง devTxtEmpCode" ทุกเคสหลังเคสแรก
            tab = GetGeneralTab(form);
            if (tc.Code != null) TypeText(tab, CodeFieldId, tc.Code);
            if (tc.Title != null) SelectComboBoxItem(tab, TitleFieldId, tc.Title);
            if (tc.FirstName != null) TypeText(tab, FirstNameFieldId, tc.FirstName);
            if (tc.LastName != null) TypeText(tab, LastNameFieldId, tc.LastName);

            if (tc.StartDate != null || tc.Shift != null)
            {
                SelectTab(form, EmploymentTabName);
                if (tc.StartDate != null) TypeText(form, StartDateFieldId, tc.StartDate);
                if (tc.Shift != null) SelectLookup(form, ShiftFieldId, tc.Shift);
            }
            if (tc.PayPeriod != null)
            {
                SelectTab(form, PaymentTabName);
                SelectLookup(form, PayPeriodFieldId, tc.PayPeriod);
            }

            form.FindFirstDescendant(cf => cf.ByName(tc.SaveBtn))?.Click();
            AssertScreenTaskResult(form, tc.ExpectedErrors, testName);
        }

        private static string? DetectDuplicateMessage(IEnumerable<string> texts)
        {
            if (texts.Any(t => t.Contains("รหัสพนักงาน ห้ามซ้ำ"))) return "รหัสพนักงานซ้ำ";
            return null;
        }

        /* ฟอร์มยังเปิดอยู่และมีช่องรหัสพนักงานให้กรอก */
        private static bool IsFormUsable(Window form)
        {
            try { return form.IsAvailable && form.FindFirstDescendant(cf => cf.ByAutomationId(FirstObjectID)) != null; }
            catch { return false; }
        }

        /* ปิดฟอร์มที่ค้างหลังเคสล้มเหลว: กด Close → No (ไม่บันทึก) */
        private void CloseFormWithoutSaving(Window? form)
        {
            try
            {
                form ??= FindOpenForm();
                if (form == null || !form.IsAvailable) return;
                try { ClickCloseAndHandleDialog(form, "No", "CloseFormWithoutSaving"); } catch { }
                WaitForClosed(() => FindOpenForm(), 3000);
            }
            catch { }
        }

        /* ถ้าแท็บอื่นแสดงอยู่ จะคลิกกลับมาที่แท็บแรกก่อน */
        private AutomationElement GetGeneralTab(Window form)
        {
            var page = Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(GeneralTabId)), 3000);
            if (page == null || page.Properties.IsOffscreen.ValueOrDefault)
            {
                // [แก้] เดิมคลิก TabItem ตัวแรกที่หาเจอ ซึ่งไม่ใช่แท็บ General (แท็บแถวบนคือ ค่าลดหย่อน บิดามารดา)
                //       แท็บ General เลยไม่ถูกเลือก → เคส Validation ถัดไป "ไม่พบช่อง devTxtEmpCode"
                SelectTab(form, "General");
                WaitUntil(() =>
                {
                    page = form.FindFirstDescendant(cf => cf.ByAutomationId(GeneralTabId));
                    return page != null && !page.Properties.IsOffscreen.ValueOrDefault;
                }, 3000);
            }
            return page ?? form;
        }

        /* ฟอร์มหน้านี้ไม่มี AutomationId ประจำ จึงหาจากชื่อหน้าต่าง */
        private Window? FindOpenForm() =>
            _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault() ??
            _automation.GetDesktop().FindAllChildren(cf => cf.ByControlType(ControlType.Window))
                .FirstOrDefault(w => (w.Name ?? "").Contains(": New") || (w.Name ?? "").Contains(": Edit")
                                     || (w.Name ?? "").StartsWith($"{MenuName} :"))?.AsWindow();

        // --- การกรอกข้อมูลเฉพาะหน้าจอนี้ (ช่อง DevExpress ต้องพิมพ์ด้วยคีย์บอร์ดจริง) ---

        /* พิมพ์ข้อความด้วยคีย์บอร์ด (ค่าว่าง = ลบข้อความ) แล้ว Tab ออกเพื่อ commit */
        private void TypeText(AutomationElement scope, string id, string text)
        {
            var field = Retry(() => scope.FindFirstDescendant(cf => cf.ByAutomationId(id)), 8000) ?? throw new Exception($"ไม่พบช่อง {id}");
            WaitUntil(() => field.Properties.IsEnabled.ValueOrDefault, 3000);

            ClickWithMouse(field);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            Keyboard.Type(VirtualKeyShort.DELETE);
            if (text.Length == 0)
                for (int i = 0; i < 10; i++) Keyboard.Type(VirtualKeyShort.BACK); // กัน mask ของช่องวันที่เหลือตัวอักษรค้าง
            else
                Keyboard.Type(text);

            WaitUntil(() => text.Length == 0 || GetValue(scope, id).Contains(text), 1500);
            Keyboard.Type(VirtualKeyShort.TAB);
        }

        /* อ่านค่าในช่องตาม Automation ID (อ่านจากช่อง Edit ด้านในถ้ามี) */
        private string GetValue(AutomationElement scope, string id)
        {
            try
            {
                var el = scope.FindFirstDescendant(cf => cf.ByAutomationId(id));
                if (el == null) return "";
                var edit = el.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)) ?? el;
                return (edit.Patterns.Value.IsSupported ? edit.Patterns.Value.Pattern.Value.Value : edit.Name) ?? "";
            }
            catch { return ""; }
        }

        /* สลับแท็บในฟอร์ม แล้วรอจนแท็บถูกเลือกจริง
           [แก้] เทียบชื่อแบบไม่สนช่องว่าง และถ้าไม่ตรงทั้งชื่อ ใช้คำหน้า "/" (เช่น "กลุ่มผู้ใช้") หาแทน
                 ถ้ายังไม่เจอ บอกชื่อแท็บที่มีอยู่จริงใน log */
        private void SelectTab(Window form, string tabName)
        {
            string full = Squash(tabName);
            string head = Squash(tabName.Split('/')[0]);

            AutomationElement? FindTab()
            {
                var tabs = form.FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem)).ToList();
                return tabs.FirstOrDefault(t => Squash(SafeName(t)) == full)
                       ?? tabs.FirstOrDefault(t => head.Length > 0 && Squash(SafeName(t)).StartsWith(head));
            }

            var tabItem = Retry(FindTab, 3000);
            if (tabItem == null)
            {
                string seen = string.Join(" | ", form.FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem))
                                                     .Select(SafeName).Where(n => n.Length > 0).Distinct());
                throw new Exception($"หา Tab '{tabName}' ไม่เจอ (แท็บที่มี: {seen})");
            }

            ClickWithMouse(tabItem);
            WaitUntil(() => !tabItem.Patterns.SelectionItem.IsSupported || tabItem.Patterns.SelectionItem.Pattern.IsSelected.Value, 3000);
        }

        /* เลือกค่าใน ComboBox (คำนำหน้าชื่อ) แล้วรอจนค่าแสดงในช่อง */
        private void SelectComboBoxItem(AutomationElement scope, string id, string itemText)
        {
            var combo = Retry(() => scope.FindFirstDescendant(cf => cf.ByAutomationId(id)), 8000) ?? throw new Exception($"ไม่พบช่อง {id}");
            ClickWithMouse(combo);

            // [แก้] เดิมค้น ListItem จากทั้ง Desktop (รวม Visual Studio / Edge ที่เปิดอยู่) — ช้ามากจนดูเหมือนค้าง
            //       ตอนนี้ค้นเฉพาะหน้าต่างของโปรแกรม HRMI (popup ของ dropdown + ฟอร์ม)
            var item = Retry(() => HrmiPopupsAndForm().SelectMany(w => w.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem)))
                                       .FirstOrDefault(li => (li.Name ?? "").Trim() == itemText), 4000);
            if (item != null) ClickWithMouse(item);
            else { Keyboard.Type(itemText); Keyboard.Type(VirtualKeyShort.RETURN); }

            WaitUntil(() => GetValue(scope, id).Contains(itemText), 2000);
        }

        /* เลือกข้อมูลจาก Pop-up Lookup ของช่อง: กด ... → รอ Pop-up → คลิกแถวที่ตรง → กด Select → รอ Pop-up ปิด (ค่าว่าง = ลบค่าในช่อง) */
        private void SelectLookup(AutomationElement scope, string id, string itemText)
        {
            var control = Retry(() => scope.FindFirstDescendant(cf => cf.ByAutomationId(id)), 5000) ?? throw new Exception($"ไม่พบช่อง {id}");

            if (itemText.Length == 0)
            {
                ClearLookupField(control, id);
                return;
            }

            SelectLookupOn(control, $"ช่อง {id}", itemText);
        }

        /* ลบค่าในช่องแบบ ButtonEdit ให้ว่างจริง
           [แก้] เดิมใช้ SetValue("") อย่างเดียว — DevExpress ล้างแค่ข้อความที่แสดง ค่าจริงยังอยู่
                 กด Save แล้วบันทึกผ่าน (ไม่มีแจ้งเตือน) → TestCase03 ไม่พบข้อความ และเกิดข้อมูล EMP08 ขึ้นจริง
           ตอนนี้: คลิกในช่องพิมพ์ → Ctrl+A → Delete → Tab ออก / ไม่ว่างค่อยกดปุ่ม × / ยังไม่ว่าง = FAIL บอกค่าที่ค้าง */
        private void ClearLookupField(AutomationElement control, string id)
        {
            bool IsEmpty() => FieldIsEmpty(control) != false;

            if (IsEmpty()) return;

            var edit = control.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)) ?? control;
            var r = edit.BoundingRectangle;
            Mouse.Click(new Point(r.Left + Math.Min(20, r.Width / 3), r.Top + r.Height / 2));
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            Keyboard.Type(VirtualKeyShort.DELETE);
            Keyboard.Type(VirtualKeyShort.TAB);
            if (WaitUntil(IsEmpty, 2000)) return;

            var clear = control.FindAllDescendants(cf => cf.ByControlType(ControlType.Button)).FirstOrDefault(b => IsShown(b) && IsClearButton(b));
            if (clear != null)
            {
                ClickWithMouse(clear);
                if (WaitUntil(IsEmpty, 2000)) return;
            }

            if (edit.Patterns.Value.IsSupported) { try { edit.Patterns.Value.Pattern.SetValue(""); } catch { } }
            if (!WaitUntil(IsEmpty, 1500))
                throw new Exception($"ลบค่าในช่อง {id} ไม่สำเร็จ (ค่าที่ค้าง: '{ReadFieldText(control)}')");
        }

        /* เหมือน SelectLookup แต่รับช่องที่หาไว้แล้ว (ใช้กับช่องที่หาจากป้ายชื่อ)
           [ใหม่] เลือกเสร็จแล้วตรวจว่าช่องมีค่าจริง (อ่านค่าไม่ได้ = ข้ามการตรวจ) */
        private void SelectLookupOn(AutomationElement control, string what, string itemText)
        {
            RunLookupPopup(what, itemText, attempt => ClickEllipsis(control, attempt));

            if (!WaitUntil(() => FieldIsEmpty(control) != true, 2000))
                throw new Exception($"เลือก '{itemText}' ใน{what}แล้ว แต่ช่องยังว่าง");
        }

        /* กดปุ่ม ... ของช่องแบบ ButtonEdit
           [แก้] เดิมคลิกปุ่มแรกที่หาเจอ ถ้าไม่เจอปุ่มก็คลิกกลางช่อง — ช่องที่มีปุ่ม × (ล้างค่า) ต่อท้าย เช่น รูปแบบงวดการจ่าย
                 จะไปโดนปุ่ม × หรือแค่คลิกลงในช่อง (เคอร์เซอร์กะพริบในช่อง) Pop-up จึงไม่เปิด
           ตอนนี้ไล่ตำแหน่งตามรอบที่ลอง:
             1) ปุ่มชื่อ ... หรือปุ่มที่ไม่ใช่ปุ่มล้างค่า (ซ้ายสุด)
             2) ถัดจากขอบขวาของช่องพิมพ์ (ปุ่มแรกหลังช่องพิมพ์ = ...) — กรณี DevExpress ไม่เปิดให้เห็นปุ่ม
             3) ขอบขวาสุดของช่อง — กรณีช่องมีปุ่ม ... อย่างเดียว */
        private void ClickEllipsis(AutomationElement control, int attempt)
        {
            var r = control.BoundingRectangle;
            int y = r.Top + r.Height / 2;
            var points = new List<Point>();

            var buttons = control.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                                 .Where(IsShown).OrderBy(b => b.BoundingRectangle.Left).ToList();
            var dots = buttons.FirstOrDefault(b => { var n = SafeName(b).Trim(); return n == "..." || n == "…" || n.Contains("Ellipsis", StringComparison.OrdinalIgnoreCase); })
                       ?? buttons.FirstOrDefault(b => !IsClearButton(b));
            if (dots != null)
            {
                var br = dots.BoundingRectangle;
                points.Add(new Point(br.Left + br.Width / 2, br.Top + br.Height / 2));
            }

            var edit = control.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit));
            if (edit != null && IsShown(edit) && edit.BoundingRectangle.Right < r.Right - 8)
                points.Add(new Point(edit.BoundingRectangle.Right + 8, y));

            points.Add(new Point(r.Right - 9, y));

            var distinct = points.Distinct().ToList();
            var p = distinct[Math.Min(attempt, distinct.Count) - 1];
            Mouse.Click(p);
        }

        private static bool IsClearButton(AutomationElement b)
        {
            var n = SafeName(b).Trim();
            return n == "×" || n == "x" || n == "X"
                   || n.Contains("Clear", StringComparison.OrdinalIgnoreCase) || n.Contains("Close", StringComparison.OrdinalIgnoreCase)
                   || n.Contains("Delete", StringComparison.OrdinalIgnoreCase) || n.Contains("ลบ") || n.Contains("ล้าง");
        }

        /* ช่องว่างหรือไม่: true = ว่าง, false = มีค่า, null = อ่านค่าไม่ได้ */
        private static bool? FieldIsEmpty(AutomationElement control)
        {
            try
            {
                var edit = control.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit));
                if (edit != null && edit.Patterns.Value.IsSupported)
                    return string.IsNullOrWhiteSpace(edit.Patterns.Value.Pattern.Value.Value);
                if (control.Patterns.Value.IsSupported)
                    return string.IsNullOrWhiteSpace(control.Patterns.Value.Pattern.Value.Value);
            }
            catch { }
            return null;
        }

        // --- ช่องที่ยังไม่รู้ AutomationId แน่ชัด: หาตาม id ก่อน ไม่เจอหาจากป้ายชื่อช่อง ---

        /* หาช่องกรอกตาม AutomationId — ไม่เจอ หาจากป้ายชื่อช่อง (label) แล้วเอาช่องที่อยู่ทางขวาในแถวเดียวกัน
           ไม่เจอทั้งคู่ → บอก id ของช่องที่เห็นอยู่ในแท็บ (เอาไปใส่ค่าคงที่ในหมวด 2.2 ได้เลย) */
        private AutomationElement FindField(Window form, string id, string label)
        {
            var byId = Retry(() => form.FindAllDescendants(cf => cf.ByAutomationId(id)).FirstOrDefault(IsShown), 2000);
            if (byId != null) return byId;

            var byLabel = Retry(() => FindFieldByLabel(form, label), 3000);
            if (byLabel != null) return byLabel;

            throw new Exception($"หาช่อง '{label}' ไม่เจอ (ไม่มี id '{id}' และหาจากป้ายชื่อไม่เจอ) — id ของช่องที่เห็นอยู่: {DescribeShownFieldIds(form)}");
        }

        /* ป้ายชื่อที่ข้อความตรงกับ label → ช่องกรอก (id ขึ้นต้นด้วย dev) ที่อยู่ใกล้ที่สุดทางขวา ในแถวเดียวกัน
           (ถ้าป้ายเป็นกรอบที่มีช่องกรอกอยู่ข้างใน ใช้ช่องข้างในนั้นเลย) */
        private static AutomationElement? FindFieldByLabel(Window form, string label)
        {
            string target = LabelKey(label);
            var shown = form.FindAllDescendants().Where(IsShown).ToList();

            // ช่องที่ผูกชื่อกับป้ายไว้แล้ว (Name ของช่อง = ข้อความป้าย) ใช้ได้เลย
            var named = shown.FirstOrDefault(e => IsFieldControl(e) && LabelKey(SafeName(e)) == target);
            if (named != null) return named;

            foreach (var lb in shown.Where(e => LabelKey(SafeName(e)) == target && !IsFieldControl(e)))
            {
                var inner = lb.FindAllDescendants().Where(IsShown).FirstOrDefault(IsFieldControl);
                if (inner != null) return inner;

                var lr = lb.BoundingRectangle;
                int cy = lr.Top + lr.Height / 2;
                var field = shown.Where(IsFieldControl)
                                 .Where(e =>
                                 {
                                     var r = e.BoundingRectangle;
                                     return r.Left >= lr.Right - 5 && r.Top <= cy && cy <= r.Bottom;
                                 })
                                 .OrderBy(e => e.BoundingRectangle.Left)
                                 .FirstOrDefault();
                if (field != null) return field;
            }
            return null;
        }

        /* ช่องกรอกของหน้าจอนี้: id ขึ้นต้นด้วย dev และไม่ใช่ป้ายข้อความ / แท็บ / ปุ่ม */
        private static bool IsFieldControl(AutomationElement e)
        {
            try
            {
                string id = e.Properties.AutomationId.ValueOrDefault ?? "";
                if (!id.StartsWith("dev", StringComparison.OrdinalIgnoreCase)) return false;
                if (id.StartsWith("devLbl", StringComparison.OrdinalIgnoreCase) || id.StartsWith("devLabel", StringComparison.OrdinalIgnoreCase)) return false;
                var type = e.Properties.ControlType.ValueOrDefault;
                return type != ControlType.Text && type != ControlType.TabItem && type != ControlType.Tab
                       && type != ControlType.Button && type != ControlType.Group && e.BoundingRectangle.Width >= 40;
            }
            catch { return false; }
        }

        private static string LabelKey(string s) =>
            new string((s ?? "").Where(c => !char.IsWhiteSpace(c) && c != ':' && c != '*').ToArray());

        private static bool IsShown(AutomationElement e)
        {
            try { return !e.Properties.IsOffscreen.ValueOrDefault && !e.BoundingRectangle.IsEmpty; }
            catch { return false; }
        }

        private static string SafeName(AutomationElement e)
        {
            try { return e.Name ?? ""; } catch { return ""; }
        }

        private static string DescribeShownFieldIds(Window form)
        {
            try
            {
                var ids = form.FindAllDescendants().Where(IsShown).Where(IsFieldControl)
                    .Select(e => $"{e.Properties.AutomationId.ValueOrDefault}({SafeName(e)})")
                    .Distinct().Take(40).ToList();
                return ids.Count == 0 ? "(ไม่เห็นช่องที่มี id)" : string.Join(", ", ids);
            }
            catch { return "(อ่านไม่ได้)"; }
        }

        /* เลือกค่าใน Dropdown (▼): คลิกลูกศรที่ขอบขวาของช่อง → คลิกรายการใน popup → รอจนค่าในช่องเปลี่ยนจริง
           ไม่เจอรายการ → ปิด popup แล้วเลื่อนตัวเลือกด้วยปุ่มลูกศรจนค่าตรง (วิธีเดียวกับประเภทบัตรในหน้า EmpCardInfoUC)
           เลือกไม่ติด → FAIL พร้อมบอกค่าที่อยู่ในช่องตอนนี้ */
        private void SelectDropdown(Window form, AutomationElement combo, string label, string itemText)
        {
            if (LabelKey(ReadFieldText(combo)) == LabelKey(itemText)) return;

            var r = combo.BoundingRectangle;
            Mouse.Click(new Point(r.Right - 9, r.Top + r.Height / 2));   // ลูกศร ▼

            var item = Retry(() => FindDropdownItem(form, itemText), 3000);
            if (item != null)
            {
                ClickWithMouse(item);
                if (WaitUntil(() => LabelKey(ReadFieldText(combo)) == LabelKey(itemText), 2000)) return;
            }

            // สำรอง: ปิด popup (ถ้าเปิดอยู่) แล้วเลื่อนตัวเลือกด้วยปุ่มลูกศรจนค่าในช่องตรง
            if (FindDropdownPopups(form).Any()) Keyboard.Type(VirtualKeyShort.ESCAPE);
            foreach (var key in new[] { VirtualKeyShort.DOWN, VirtualKeyShort.UP })
            {
                for (int i = 0; i < 8; i++)
                {
                    string before = ReadFieldText(combo);
                    Keyboard.Type(key);
                    WaitUntil(() => ReadFieldText(combo) != before, 500);

                    if (LabelKey(ReadFieldText(combo)) == LabelKey(itemText))
                    {
                        Keyboard.Type(VirtualKeyShort.TAB);
                        return;
                    }
                }
            }

            throw new Exception($"เลือก{label} '{itemText}' ไม่ติด (ค่าในช่องตอนนี้: '{ReadFieldText(combo)}')");
        }

        /* popup ของ dropdown = หน้าต่างของโปรแกรมที่ไม่ใช่ฟอร์มและหน้าจอหลัก */
        private IEnumerable<AutomationElement> FindDropdownPopups(Window form)
        {
            int pid = _mainWindow.Properties.ProcessId.ValueOrDefault;
            var formHandle = form.Properties.NativeWindowHandle.ValueOrDefault;
            var mainHandle = _mainWindow.Properties.NativeWindowHandle.ValueOrDefault;

            return _automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(pid))
                .Where(w => w.Properties.NativeWindowHandle.ValueOrDefault != formHandle
                         && w.Properties.NativeWindowHandle.ValueOrDefault != mainHandle
                         && IsShown(w));
        }

        private AutomationElement? FindDropdownItem(Window form, string text)
        {
            string target = LabelKey(text);
            foreach (var popup in FindDropdownPopups(form))
            {
                var el = popup.FindAllDescendants().FirstOrDefault(e => IsShown(e) && LabelKey(ReadItemText(e)) == target);
                if (el != null) return el;
            }
            return null;
        }

        /* ข้อความของรายการใน popup (Value ก่อน แล้วค่อย Name) */
        private static string ReadItemText(AutomationElement e)
        {
            try
            {
                if (e.Patterns.Value.IsSupported)
                {
                    var v = e.Patterns.Value.Pattern.Value.Value;
                    if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
                }
                return (e.Name ?? "").Trim();
            }
            catch { return ""; }
        }

        /* ค่าที่แสดงในช่อง: ช่อง Edit ข้างในก่อน → Value ของตัวเอง → Name */
        private static string ReadFieldText(AutomationElement e)
        {
            try
            {
                var edit = e.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit));
                if (edit != null && edit.Patterns.Value.IsSupported)
                {
                    var v = edit.Patterns.Value.Pattern.Value.Value;
                    if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
                }
                if (e.Patterns.Value.IsSupported)
                {
                    var v = e.Patterns.Value.Pattern.Value.Value;
                    if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
                }
                return (e.Name ?? "").Trim();
            }
            catch { return ""; }
        }

        /* ส่วนกลางของการเลือกจาก Pop-up: openPopup(attempt) = วิธีเปิด Pop-up (attempt = รอบที่ลอง 1-3)
           [แก้] เดิมหา Pop-up จากทุกหน้าต่างบน Desktop แล้วค้นปุ่ม Select ข้างในทั้งหมด (รวม Visual Studio / Edge)
                 ถ้า Pop-up ไม่เปิด แต่ละรอบใช้เวลานานมากจนดูเหมือนค้าง และอาจไปเจอปุ่ม Select ของโปรแกรมอื่น
                 → ตอนนี้หาเฉพาะหน้าต่างของโปรแกรม HRMI */
        private void RunLookupPopup(string what, string itemText, Action<int> openPopup)
        {
            Exception? lastEx = null;
            for (int attempt = 1; attempt <= 3; attempt++) // Pop-up ของหน้านี้บางครั้งเปิดไม่ติด ให้ลองซ้ำ (แต่ละรอบกดคนละตำแหน่ง)
            {
                Window? popup = null;
                try
                {
                    openPopup(attempt);

                    popup = Retry(FindLookupPopup, 6000)
                            ?? throw new Exception($"กด ... ของ{what} แล้ว Pop-up ไม่เปิด");

                    var row = FindLookupRow(popup, itemText);
                    if (row == null)
                    {
                        // [แก้] Pop-up เปิดแล้วแต่ไม่มีรายการนี้ → เปิดใหม่กี่รอบก็ไม่เจอ ไม่ต้องลองซ้ำ (เดิมเปิด-ปิดวนจนดูเหมือนค้าง)
                        //       ปิด Pop-up แล้ว FAIL ทันที พร้อมบอกรายการที่มีอยู่จริงในระบบ
                        string seen = DescribeLookupRows(popup);
                        CancelLookupPopup(popup);
                        throw new LookupItemNotFoundException($"ไม่พบ '{itemText}' ใน Pop-up ของ{what} (รายการที่มีในระบบ: {seen})");
                    }
                    ClickWithMouse(row);

                    var btnSelect = Retry(() => popup.FindFirstDescendant(cf => cf.ByName("Select"))
                                                ?? popup.FindFirstDescendant(cf => cf.ByName("เลือก")), 3000);
                    if (btnSelect != null) ClickWithMouse(btnSelect);
                    else Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.KEY_S);

                    if (!WaitForClosed(() => popup)) throw new Exception($"กด Select แล้ว Pop-up ของ{what} ไม่ปิด");
                    return;
                }
                catch (LookupItemNotFoundException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    if (popup != null) CancelLookupPopup(popup);
                }
            }
            throw lastEx ?? new Exception($"เลือกข้อมูลของ{what} ไม่สำเร็จ");
        }

        private sealed class LookupItemNotFoundException : Exception
        {
            public LookupItemNotFoundException(string message) : base(message) { }
        }

        /* รอตารางใน Pop-up โหลดข้อมูลเสร็จ (มีช่องที่มีข้อความ) แล้วหาแถวที่ตรง — ไม่เจอคืน null
           (โหลดเสร็จแล้วรออีกแค่ 1.5 วินาที ไม่วนรอจนครบเวลา) */
        private AutomationElement? FindLookupRow(Window popup, string itemText)
        {
            WaitUntil(() => LookupCellTexts(popup).Count > 0, 6000);
            return Retry(() => popup.FindAllDescendants().FirstOrDefault(el => MatchesLookupText(el, itemText)), 1500);
        }

        /* ข้อความในช่องของตารางใน Pop-up (ใช้เช็คว่าโหลดเสร็จ และบอกใน log ว่ามีรายการอะไรบ้าง) */
        private static List<string> LookupCellTexts(AutomationElement popup)
        {
            try
            {
                var grid = popup.FindFirstDescendant(cf => cf.ByControlType(ControlType.Table).Or(cf.ByControlType(ControlType.DataGrid))) ?? popup;
                return grid.FindAllDescendants()
                    .Select(e =>
                    {
                        try { return e.Patterns.Value.IsSupported ? (e.Patterns.Value.Pattern.Value.Value ?? "").Trim() : ""; }
                        catch { return ""; }
                    })
                    .Where(t => t.Length > 0)
                    .Distinct()
                    .ToList();
            }
            catch { return new List<string>(); }
        }

        private static string DescribeLookupRows(AutomationElement popup)
        {
            var texts = LookupCellTexts(popup).Take(12).ToList();
            return texts.Count == 0 ? "(อ่านรายการในตารางไม่ได้)" : string.Join(" | ", texts);
        }

        /* ปิด Pop-up เลือกข้อมูลด้วยปุ่ม Cancel (ไม่เจอปุ่มก็ปิดหน้าต่าง) */
        private static void CancelLookupPopup(Window popup)
        {
            try
            {
                if (!popup.IsAvailable) return;
                var cancel = popup.FindFirstDescendant(cf => cf.ByName("Cancel").And(cf.ByControlType(ControlType.Button)));
                if (cancel != null) ClickWithMouse(cancel); else popup.Close();
                WaitForClosed(() => popup);
            }
            catch { }
        }

        /* Pop-up เลือกข้อมูลที่เปิดอยู่: หน้าต่างที่ฟอร์มเปิดค้างไว้ (modal) ก่อน
           ไม่มีก็หาหน้าต่างของ HRMI ที่มีปุ่ม Select (ไม่ใช่ฟอร์มและหน้าจอหลัก) */
        private Window? FindLookupPopup()
        {
            var form = FindOpenForm();
            var modal = form?.ModalWindows.FirstOrDefault();
            if (modal != null) return modal;

            int pid = _mainWindow.Properties.ProcessId.ValueOrDefault;
            var formHandle = form?.Properties.NativeWindowHandle.ValueOrDefault ?? IntPtr.Zero;
            var mainHandle = _mainWindow.Properties.NativeWindowHandle.ValueOrDefault;

            return _automation.GetDesktop()
                .FindAllChildren(cf => cf.ByControlType(ControlType.Window).And(cf.ByProcessId(pid)))
                .Where(w => w.Properties.NativeWindowHandle.ValueOrDefault != formHandle
                         && w.Properties.NativeWindowHandle.ValueOrDefault != mainHandle)
                .FirstOrDefault(w => w.FindFirstDescendant(cf => cf.ByName("Select").Or(cf.ByName("เลือก"))) != null)?.AsWindow();
        }

        /* หน้าต่างของ HRMI ที่ dropdown ใช้แสดงรายการ: popup (หน้าต่างแยกของโปรแกรม) + ฟอร์มที่เปิดอยู่ — ไม่รวมหน้าจอหลัก */
        private IEnumerable<AutomationElement> HrmiPopupsAndForm()
        {
            var form = FindOpenForm();
            if (form == null) return Enumerable.Empty<AutomationElement>();
            return FindDropdownPopups(form).Append(form);
        }

        /* [ใหม่] รายชื่อหน้าต่างของ HRMI ที่เปิดอยู่ (ใส่ใน log ตอนหาฟอร์มไม่เจอ จะได้รู้ว่าค้างอยู่ที่หน้าต่างไหน) */
        private string DescribeAppWindows()
        {
            try
            {
                int pid = _mainWindow.Properties.ProcessId.ValueOrDefault;
                var tops = _automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(pid)).Select(w => $"'{SafeName(w)}'");
                var modals = _mainWindow.AsWindow()?.ModalWindows.Select(w => $"modal '{SafeName(w)}'") ?? Enumerable.Empty<string>();
                var all = tops.Concat(modals).ToList();
                return all.Count == 0 ? "(ไม่มี)" : string.Join(", ", all);
            }
            catch { return "(อ่านไม่ได้)"; }
        }

        // --- ตารางในแท็บ กลุ่มผู้ใช้/รหัสผู้อนุมัติ (DevExpress Grid) ---

        /* ตำแหน่งช่องในแถวที่ rowIndex ของคอลัมน์ที่หัวคอลัมน์มีคำว่า headerKey
           X = กึ่งกลางหัวคอลัมน์, Y = กึ่งกลางแถว (ถ้าตารางยังไม่มีแถว ใช้ตำแหน่งใต้หัวคอลัมน์ = แถวว่างสำหรับเพิ่มรายการ) */
        private (int X, int Y, int Right) GridCellPoint(AutomationElement grid, int rowIndex, string headerKey)
        {
            var headers = grid.FindAllDescendants(cf => cf.ByControlType(ControlType.Header).Or(cf.ByControlType(ControlType.HeaderItem)))
                              .Where(h => !h.BoundingRectangle.IsEmpty).ToList();
            var header = headers.FirstOrDefault(h => (h.Name ?? "").Contains(headerKey, StringComparison.OrdinalIgnoreCase))
                         ?? throw new Exception($"หาหัวคอลัมน์ '{headerKey}' ในตาราง ไม่เจอ (หัวคอลัมน์ที่เห็น: {string.Join(" | ", headers.Select(h => h.Name))})");
            var hr = header.BoundingRectangle;

            var rows = grid.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem))
                           .Where(r => !r.BoundingRectangle.IsEmpty).OrderBy(r => r.BoundingRectangle.Top).ToList();
            int y = rows.Count > rowIndex
                ? rows[rowIndex].BoundingRectangle.Top + rows[rowIndex].BoundingRectangle.Height / 2
                : hr.Bottom + 12 + rowIndex * 22;

            return (hr.Left + hr.Width / 2, y, hr.Right);
        }

        /* กด ... ในช่องของตาราง → เลือกค่าใน Pop-up (คลิกช่องให้เข้าโหมดแก้ไข ปุ่ม 'Editing control' จึงโผล่) */
        private void SelectGridLookup(Window form, string gridId, string headerKey, string itemText)
        {
            // [แก้] ใช้ช่องตามชื่อจริงที่ DevExpress ตั้งให้ ("<หัวคอลัมน์> row 0") แทนการคำนวณตำแหน่งจากหัวคอลัมน์
            string cellName = $"{headerKey} row 0";
            var cell = Retry(() => form.FindAllDescendants(cf => cf.ByName(cellName)).FirstOrDefault(IsShown), 5000)
                       ?? throw new Exception($"ไม่พบช่อง '{cellName}' ในตาราง {gridId}");

            RunLookupPopup($"ช่อง {cellName}", itemText, attempt =>
            {
                var r = cell.BoundingRectangle;
                int y = r.Top + r.Height / 2;

                if (attempt == 1)
                {
                    // คลิกช่องให้เข้าโหมดแก้ไข → ปุ่ม ... (Editing control) โผล่ที่ขอบขวาของช่อง → กดปุ่มนั้น
                    Mouse.Click(new Point(r.Left + r.Width / 3, y));
                    var btn = Retry(() => form.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                                              .FirstOrDefault(b => IsShown(b) && r.Contains(new Point(b.BoundingRectangle.Left + b.BoundingRectangle.Width / 2, y))), 1500);
                    if (btn != null) { ClickWithMouse(btn); return; }
                }
                Mouse.Click(new Point(r.Right - 9, y));   // ปุ่ม ... อยู่ขอบขวาของช่อง
            });
        }

        /* ติ๊ก ☑ checkbox ตาม AutomationId (devCheckCopy = Copy to Lower Level)
           [แก้] เดิมหาเป็นคอลัมน์ในตาราง devGridSecurity — จริงๆ เป็น checkbox แยก มี id ของตัวเอง
           - ติ๊กอยู่แล้ว → ไม่คลิกซ้ำ (คลิกซ้ำ = เอาติ๊กออก)
           - คลิกแล้วรอจนติ๊กติดจริง ถ้าไม่ติด (เช่นคลิกโดนข้อความข้างช่อง) กด Space ที่ช่องอีกครั้ง */
        private void TickCheckBox(AutomationElement scope, string id)
        {
            var found = Retry(() => scope.FindFirstDescendant(cf => cf.ByAutomationId(id))
                                    ?? scope.FindFirstDescendant(cf => cf.ByName(CopyLowerCheckName).And(cf.ByControlType(ControlType.CheckBox))), 5000)
                        ?? throw new Exception($"ไม่พบช่องติ๊ก {id} / '{CopyLowerCheckName}'");
            // บางรุ่นของ DevExpress ให้ id กับกรอบ แล้วตัวติ๊กจริงเป็น CheckBox ข้างใน
            var box = found.Patterns.Toggle.IsSupported
                ? found
                : found.FindFirstDescendant(cf => cf.ByControlType(ControlType.CheckBox)) ?? found;

            WaitUntil(() => box.Properties.IsEnabled.ValueOrDefault && !box.BoundingRectangle.IsEmpty, 3000);
            if (IsChecked(box) == true) return;

            ClickWithMouse(box);
            if (WaitUntil(() => IsChecked(box) != false, 2000)) return;   // ติ๊กติดแล้ว (หรืออ่านสถานะไม่ได้)

            Keyboard.Type(VirtualKeyShort.SPACE);
            if (!WaitUntil(() => IsChecked(box) != false, 2000))
                throw new Exception($"ติ๊กช่อง {id} (Copy to Lower Level) แล้ว แต่ยังไม่ถูกเลือก");
        }

        /* สถานะติ๊ก: อ่านจาก Toggle ก่อน ไม่ได้ค่อยดู LegacyIAccessible — คืน null ถ้าอ่านไม่ได้ */
        private static bool? IsChecked(AutomationElement box)
        {
            try
            {
                if (box.Patterns.Toggle.IsSupported)
                    return box.Patterns.Toggle.Pattern.ToggleState.Value == ToggleState.On;
            }
            catch { }
            try
            {
                if (box.Patterns.LegacyIAccessible.IsSupported)
                    return box.Patterns.LegacyIAccessible.Pattern.State.Value.HasFlag(AccessibilityState.STATE_SYSTEM_CHECKED);
            }
            catch { }
            return null;
        }

        /* เช็คว่าข้อความในแถวตรงกับค่าที่ต้องการ (ไม่สนช่องว่าง และรองรับชื่องวด 21-20 ที่แสดงไม่ครบ) */
        private static bool MatchesLookupText(AutomationElement el, string itemText)
        {
            // [ใหม่] ค่าว่างห้ามตรงกับแถวไหน (เดิม "".Contains("") = true → เลือกแถวแรกให้เอง)
            if (string.IsNullOrWhiteSpace(itemText)) return false;

            string text;
            try { text = el.Patterns.Value.IsSupported ? el.Patterns.Value.Pattern.Value.Value ?? "" : el.Name ?? ""; }
            catch { return false; }
            if (text.Length == 0) try { text = el.Name ?? ""; } catch { }
            if (text.Length == 0) return false;

            if (Squash(text).Contains(Squash(itemText))) return true;
            return itemText.Contains("21") && itemText.Contains("20") && text.Contains("21") && text.Contains("20");
        }

        /* Pop-up ที่เด้งหลังบันทึก
           - "กำหนดข้อมูลพนักงานเพิ่มเติม": เลือกเพอร์มิชชั่น (กด ... → Select) แล้วกด OK  (cancelPermission = true: TestStep ให้กด Cancel)
           - Pop-up อื่น: กด No / Cancel / OK ตามที่มี */
        private void HandlePostSavePopup(Window form, bool cancelPermission)
        {
            for (int guard = 0; guard < 3; guard++)
            {
                Window? popup = null;
                try
                {
                    popup = Retry(() => form.ModalWindows.FirstOrDefault()
                                        ?? _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault(w => !w.Equals(form)), 2500);
                }
                catch { }
                if (popup == null) return;

                bool isExtraInfo = popup.FindFirstDescendant(cf => cf.ByName("ชื่อเพอร์มิชชั่น")) != null
                                   || (popup.Name ?? "").Contains("กำหนดข้อมูลพนักงานเพิ่มเติม");

                if (isExtraInfo && !cancelPermission)
                {
                    try
                    {
                        var dots = popup.FindAllDescendants(cf => cf.ByControlType(ControlType.Button)).FirstOrDefault(b => b.Name == "..." || string.IsNullOrEmpty(b.Name));
                        if (dots != null)
                        {
                            ClickWithMouse(dots);
                            var perm = Retry(() => popup.ModalWindows.FirstOrDefault()
                                                   ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("Define Permission"))?.AsWindow(), 5000);
                            var sel = perm?.FindFirstDescendant(cf => cf.ByName("Select"));
                            if (perm != null && sel != null) { ClickWithMouse(sel); WaitForClosed(() => perm); }
                        }
                    }
                    catch { }
                    ClickDialogButton(popup, "OK");
                }
                else
                {
                    var names = cancelPermission ? new[] { "Cancel", "No", "Close", "OK" } : new[] { "No", "Cancel", "Close", "OK" };
                    var btn = names.Select(n => popup.FindFirstDescendant(cf => cf.ByName(n).And(cf.ByControlType(ControlType.Button))))
                                   .FirstOrDefault(b => b != null);
                    if (btn != null) ClickWithMouse(btn); else popup.Close();
                }

                WaitForClosed(() => popup);
            }
        }

        /* ตรวจข้อความใน Task Result แบบหน้าจอนี้: รอนาน (80 วินาที) และเทียบแบบไม่สนช่องว่าง */
        private void AssertScreenTaskResult(Window form, string[] msgs, string testName)
        {
            var pending = new HashSet<string>(msgs);
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < TaskResultTimeoutMs && pending.Count > 0)
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

        /* คลิกด้วยเมาส์ที่กึ่งกลาง element (ปุ่ม DevExpress บางตัว .Click() แล้วไม่ติด) */
        private static void ClickWithMouse(AutomationElement element)
        {
            var r = element.BoundingRectangle;
            if (!r.IsEmpty) Mouse.Click(new Point(r.X + r.Width / 2, r.Y + r.Height / 2));
            else element.Click();
        }

        /* ตัดช่องว่างทั้งหมดออก (ข้อความในโปรแกรมเว้นวรรคไม่ตรงกับ TestStep) */
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

        public EmpInfoUC(AutomationElement mainWindow, UIA3Automation automation)
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

        /* เปิดฟอร์ม Edit ของรหัสที่ระบุ
           [แก้] เดิมรอฟอร์มแค่ 5 วิ (หน้านี้เปิดช้า → "หาฟอร์มไม่เจอ") และไม่ตรวจว่าฟอร์มที่ได้เป็นรหัสไหน
                 ถ้าฟอร์มเปิดช้ากว่าเวลารอ เคสถัดไปจะหยิบฟอร์มที่ค้างไปแก้ผิดคน
           ตอนนี้: ปิดฟอร์มค้างก่อน → คลิกแถวด้วยเมาส์จริง → รอ 15 วิ (ไม่ขึ้นลองกดใหม่อีกรอบ)
                   → ตรวจว่ารหัสในฟอร์มตรงกับที่ต้องการ → ยังไม่เจอ log บอกหน้าต่างที่เห็นอยู่ */
        private Window ClickRowAndOpenEdit(string code)
        {
            CloseFormWithoutSaving(null);
            RefreshGrid();

            for (int attempt = 1; attempt <= 2; attempt++)
            {
                var row = Retry(() => FindGridElement(code, onlyShown: true), 4000)
                          ?? throw new Exception($"หาแถว '{code}' ไม่เจอ (หรืออยู่นอกจอ)");
                ClickWithMouse(row);

                var btn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("Edit")), 3000)
                          ?? throw new Exception("ไม่พบปุ่ม Edit");
                ClickWithMouse(btn);

                var form = Retry(FindOpenForm, EditFormOpenTimeoutMs);
                if (form == null) continue;

                WaitUntil(() => GetValue(form, FirstObjectID).Trim().Length > 0, 8000);
                string loaded = GetValue(form, FirstObjectID).Trim();
                if (!string.Equals(loaded, code, StringComparison.OrdinalIgnoreCase))
                {
                    CloseFormWithoutSaving(form);
                    throw new Exception($"ฟอร์ม Edit ที่เปิดขึ้นเป็นรหัส '{loaded}' ไม่ใช่ '{code}' (คลิกแถวไม่ติด)");
                }
                return form;
            }

            throw new Exception($"กด Edit ที่ '{code}' แล้วหาฟอร์มไม่เจอ (หน้าต่างที่เห็น: {DescribeAppWindows()})");
        }

        private void RunDeleteCase(string baseName, string code, string btn, bool expectDeleted)
        {
            string testName = $"{baseName}_{code}_{btn}";
            try
            {
                CloseFormWithoutSaving(null);
                RefreshGrid();

                // [แก้] เอาเฉพาะแถวที่แสดงบนจอ — แถวที่ไม่มีขนาด คลิกแล้วไม่โดน จะไปลบแถวที่ถูกเลือกอยู่แทน
                var elRow = Retry(() => FindGridElement(code, onlyShown: true), 4000)
                            ?? throw new Exception($"หาแถวรหัส '{code}' ในตารางไม่เจอ (หรืออยู่นอกจอ)");
                ClickWithMouse(elRow);   // คลิกเมาส์จริงที่แถวเดียว

                var delBtn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("Delete")), 3000)
                             ?? throw new Exception("หาปุ่ม Delete ไม่เจอ");
                delBtn.Click();

                var dlg = Retry(() => _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault()
                                      ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("คำเตือน"))?.AsWindow(), 5000)
                          ?? throw new Exception("กด Delete แล้วไม่พบ Pop-up ยืนยัน");
                AnswerDialog(dlg, btn);

                if (expectDeleted)
                {
                    // [แก้] เดิมกด Find ครั้งเดียวทันทีหลังกด Yes แล้วรอ 8 วิโดยไม่โหลดตารางใหม่
                    //       ถ้าการลบเสร็จช้ากว่าการกด Find ตารางจะค้างแถวเดิมตลอด → FAIL ทั้งที่ลบแล้ว
                    //       ตอนนี้: กด Find ใหม่ทุกรอบ จนแถวหาย (สูงสุด 20 วิ)
                    bool gone = false;
                    var sw = Stopwatch.StartNew();
                    while (sw.ElapsedMilliseconds < DeleteWaitTimeoutMs)
                    {
                        RefreshGrid();
                        if (!WaitUntil(() => FindGridElement(code) != null, 1500)) { gone = true; break; }
                    }

                    if (!gone)
                    {
                        string why = ReadAppMessage();
                        LogFailure(testName, $"'{code}' ลบข้อมูลไม่สำเร็จ (ยังอยู่ในตาราง){why}");
                        return;
                    }
                    LogSuccess(testName, $"'{code}' ถูกลบสำเร็จ");
                }
                else
                {
                    RefreshGrid();
                    if (!VerifyRecordInGrid(code, timeoutMs: 5000, refresh: false)) { LogFailure(testName, $"'{code}' ลบข้อมูลผิดพลาด (หายจากตาราง)"); return; }
                    LogSuccess(testName, $"'{code}' ข้อมูลยังไม่ถูกลบ");
                }
            }
            catch (Exception ex)
            {
                LogFailure(testName, $"ลบรหัส '{code}' ไม่สำเร็จ: {ex.Message}");
            }
        }

        /* กดปุ่มในกล่องยืนยัน (Yes/No ทั้งชื่ออังกฤษและไทย) ด้วยเมาส์จริง แล้วต้องเห็นกล่องปิด */
        private void AnswerDialog(Window dlg, string btn)
        {
            bool yes = btn.Equals("Yes", StringComparison.OrdinalIgnoreCase);
            string[] names = yes ? new[] { "Yes", "&Yes", "ใช่" } : new[] { "No", "&No", "ไม่ใช่" };
            var b = Retry(() => names.Select(n => dlg.FindFirstDescendant(cf => cf.ByName(n))).FirstOrDefault(x => x != null && !x.BoundingRectangle.IsEmpty), 2000);
            if (b != null) ClickWithMouse(b);
            else { try { dlg.SetForeground(); } catch { } Keyboard.Type(yes ? VirtualKeyShort.KEY_Y : VirtualKeyShort.KEY_N); }
            if (!WaitForClosed(() => dlg, 3000)) throw new Exception($"กด {btn} แล้วกล่องยืนยันยังไม่ปิด");
        }

        /* ข้อความแจ้งเตือนจากโปรแกรมหลังกดลบ (กล่องข้อความที่เด้ง / Task Result) — ใส่ท้าย log ให้รู้สาเหตุ */
        private string ReadAppMessage()
        {
            try
            {
                var box = _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault();
                if (box != null)
                {
                    string text = string.Join(" ", box.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
                                                      .Select(SafeName).Where(t => t.Length > 0));
                    var ok = box.FindFirstDescendant(cf => cf.ByName("OK").Or(cf.ByName("Close")));
                    if (ok != null) ClickWithMouse(ok);
                    if (text.Length > 0) return $" — โปรแกรมแจ้ง: {text}";
                }

                var panel = _mainWindow.FindFirstDescendant(cf => cf.ByName("Task Result").Or(cf.ByAutomationId("dockPanelTaskResult")));
                if (panel != null)
                {
                    var texts = GetPanelTexts(panel).Select(t => t.Trim()).Where(t => t.Length > 0 && t != "Task Result").Distinct().Take(5).ToList();
                    if (texts.Count > 0) return $" — Task Result: {string.Join(" | ", texts)}";
                }
            }
            catch { }
            return "";
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

        /* หาช่องในตารางหลักที่มีข้อความตรงกับ val
           [แก้] เดิมค้นจากทั้งหน้าจอหลัก (อาจเจอข้อความเดียวกันนอกตาราง) และไม่สนว่าแสดงบนจอหรือไม่
           ตอนนี้: ค้นในตาราง (DataGrid/Table) ที่แสดงอยู่ก่อน ไม่เจอตารางค่อยค้นทั้งหน้าจอ
                   onlyShown = true → เอาเฉพาะช่องที่มองเห็น (ใช้ตอนจะคลิก) */
        private AutomationElement? FindGridElement(string val, bool onlyShown = false)
        {
            var grid = _mainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.DataGrid).Or(cf.ByControlType(ControlType.Table)))
                                  .FirstOrDefault(IsShown);
            return (grid ?? _mainWindow).FindAllDescendants().FirstOrDefault(el =>
                (!onlyShown || IsShown(el)) &&
                ((!string.IsNullOrEmpty(el.Name) && string.Equals(el.Name.Trim(), val, StringComparison.OrdinalIgnoreCase)) ||
                 (el.Patterns.Value.IsSupported && string.Equals(el.Patterns.Value.Pattern.Value.Value?.Trim(), val, StringComparison.OrdinalIgnoreCase))));
        }

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