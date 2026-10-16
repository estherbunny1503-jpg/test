using FlaUI.Core;
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
using System.Runtime.InteropServices;
namespace HRMITestFlaUI.Module.Approve_Center
{
    public class PrivilegetoApproveInfoUC
    {
        // =========================================================================================
        // หมวดที่ 1: ข้อมูลการทดสอบ (Test Data)
        // [แก้] ตัดขั้นตอน New (เพิ่มข้อมูล) และ แจ้งเตือนตอน Edit ออก
        //       เหลือ: แจ้งเตือนตอน New → แก้ไขข้อมูล → ลบ
        //       ข้อมูลสิทธิ์อนุมัติของ EMP00001 / EMP00002 มีอยู่แล้วจากหน้า บันทึกข้อมูลพนักงาน (ติ๊ก Copy to Lower Level)
        //       จึงไม่ต้องเพิ่มใหม่ และไม่ลบข้อมูลที่มีอยู่ก่อนเริ่มทดสอบ
        // =========================================================================================

        // --- Data Records สำหรับใช้งานในแต่ละเฟส ---
        private sealed record ValidationItem(
            string TestCaseId,
            string ApproverCode,     // รหัสผู้อนุมัติที่เลือกในฟอร์ม New
            string SaveBtn,
            int Progress,
            string ProgressMsg,
            string[] ExpectedErrors);
        private sealed record EditItem(
            string Name,
            string Code,
            string? Screen,          // หน้าจอของแถวที่จะแก้ — null = แถวไหนของรหัสนี้ก็ได้ (แถวแรกที่เจอ)
            string Emp,              // รหัสพนักงานที่ต้องเหลืออยู่หลังแก้ไข
            bool KeepInSameLookup,   // true = ลบให้เหลือแค่ Emp / false = ลบทั้งหมด แล้วกด ... ติ๊ก Emp ใหม่
            string SaveBtn,
            bool NeedsManualClose);
        private sealed record DeleteItem(
            string TargetCode,
            bool PickLastRow,        // [แก้] true = แถวสุดท้ายของรหัสนี้ / false = แถวแรก (TestStep: Yes ลบแถวสุดท้าย, No แถวแรกต้องอยู่เหมือนเดิม)
            string DialogBtn,
            bool ExpectDeleted,
            int Progress,
            string ProgressMsg);

        private const string? AnyScreen = null;   // [ใหม่] เลือกแถวไหนของรหัสนั้นก็ได้ (EMP00001 ไม่มีแถวของหน้าจอ บันทึกขออนุมัติลา)

        /* ข้อความหน้าจอสำหรับ log */
        private static string ScreenText(string? screen) => screen ?? "แถวแรกที่เจอ";

        // [ขั้นที่ 2] ข้อมูลทดสอบ Validation ตอนเพิ่ม (Add Validation)
        // กด New → เลือกรหัสผู้อนุมัติ EMP00001, ไม่เลือกรหัสพนักงาน, กด Save → แจ้งเตือน → กด Close
        private static readonly ValidationItem[] Step2_AddValidationCases =
        {
            new("TestCase02_NewValidation_EmptyEmployee", "EMP00001", "Save", 30,
                "Validating: Missing Employee...", new[] { "กรุณาเลือกหน้าจอและพนักงานที่ต้องการกำหนด" })
        };

        // [ขั้นที่ 3] ข้อมูลทดสอบการแก้ไขข้อมูล (Edit Records) — TestStep ข้อ 3 (แถว 32-33)
        private static readonly EditItem[] Step3_EditCases =
        {
            // ลบรหัสพนักงาน ให้เหลือแค่ EMP00004 (ทำในหน้าต่างเดียว)
            new(
                "TestCase03_EditData_Record1_EMP00001",
                "EMP00001",
                AnyScreen,
                "EMP00004",
                true,
                "Save",
                true),
            // ลบรหัสพนักงาน แล้วกด ... ติ๊ก EMP00004 กด Select (ลบก่อน แล้วเปิดใหม่ติ๊ก)
            new(
                "TestCase03_EditData_Record2_EMP00002",
                "EMP00002",
                AnyScreen,
                "EMP00004",
                false,
                "Save and Close",
                false)
        };

        // [ขั้นที่ 5] ข้อมูลทดสอบการลบข้อมูล (Delete Records) — TestStep ข้อ 5
        private static readonly DeleteItem[] Step5_DeleteCases =
        {
            // คลิกข้อมูล EMP00002 → Delete → Yes → ข้อมูลแถวสุดท้ายถูกลบ
            new("EMP00002", true, "Yes", true, 92, "Delete Test: EMP00002 last row (confirmed delete)..."),
            // คลิกข้อมูล EMP00001 → Delete → No → ข้อมูลแถวแรกต้องอยู่เหมือนเดิม
            new("EMP00001", false, "No", false, 100, "Delete Test: EMP00001 first row (cancelled delete)...")
        };


        // =========================================================================================
        // หมวดที่ 2: ส่วนที่ขึ้นกับหน้าจอนั้นๆ (Screen-Specific Implementation)
        // **เมื่อเปลี่ยนไปทำหน้าจออื่น ต้องมาแก้ไขค่าและเมทอดในส่วนนี้ทั้งหมด**
        // =========================================================================================

        // 2.1 Locator & Mappings ประจำหน้าจอ
        private const string MenuName = "บันทึกสิทธิ์การอนุมัติ";
        private const string ScreenLogName = "PrivilegetoApproveInfoUC";
        private const double MenuPanelWidthRatio = 0.25;      // แผงเมนูซ้ายกว้างไม่เกิน 25% ของหน้าต่างหลัก (ใช้กันไม่ให้ FindListGrid หยิบแผงเมนูมาเป็นตารางรายการ)
        private const string SearchBoxId = "baritemSearch";       // ช่องค้นหาเมนู (เหมือนหน้า BANK)
        private const string SearchButtonId = "barChkSearch";     // ปุ่มค้นหา
        private const string SearchResultFallbackName = "treeListColumn row 1";

        // 2.2 ฟิลด์ Input ประจำหน้าจอ
        private const string ApproverFieldId = "devBtnEitApprover";       // ช่อง รหัสผู้อนุมัติ (ButtonEdit มีปุ่ม ... ขอบขวา)
        private const string ScreenListFieldId = "devTreelistMenu";       // รายการหน้าจอ (ข้อมูลสิทธิ์การอนุมัติสำหรับแต่ละหน้าจอ)
        private const string EmployeeGridFieldId = "devGridListViewEmp";  // ตาราง รหัสพนักงาน / ชื่อพนักงาน
        private const string ColEmpCode = "รหัสพนักงาน";
        /// <summary>
        /// ขั้นที่ 1: เปิดหน้าจอ
        /// [แก้] ตัดการเพิ่มข้อมูล (New) ออก — ใช้ข้อมูลสิทธิ์อนุมัติที่มีอยู่แล้ว และไม่ลบข้อมูลค้างก่อนเริ่ม
        /// </summary>
        public void NavigateToPrivilegetoApproveInfoUC()
        {
            const string stageName = "TestCase01_OpenMenu";
            TestLogger.LogInfo(ScreenLogName, $"เริ่มทดสอบ{MenuName} (ข้ามขั้นตอนเพิ่มข้อมูล — ใช้ข้อมูลสิทธิ์อนุมัติที่มีอยู่แล้ว)");
            TestProgressHelper.Report(2, $"Opening {MenuName} Menu via Search...");

            TryOpenMenu(stageName, MenuName);   // เปิดไม่สำเร็จ TryOpenMenu ลง log FAIL ให้เอง
            TestProgressHelper.Report(20, $"{MenuName} opened.");
        }

        /* Method เดิมที่ชุดทดสอบอาจเรียกอยู่ — ทำงานเหมือน NavigateToPrivilegetoApproveInfoUC */
        public void RunAddNewTests() => NavigateToPrivilegetoApproveInfoUC();

        /// <summary>
        /// ขั้นที่ 2: รัน Validation ตอนสร้างข้อมูลใหม่
        /// </summary>
        public void RunAllValidationTests()
        {
            const string stage = "TestCase02_NewValidation";
            TestProgressHelper.Report(22, $"Opening {MenuName} Form for Validation Tests...");

            CloseDanglingForm();                 // เฉพาะหน้าจอนี้: เคลียร์ฟอร์ม / Pop-up ที่ค้างจากขั้นก่อน

            try { OpenForm(); }
            catch (Exception ex) { LogFailure(stage, $"เปิดฟอร์มไม่สำเร็จ: {ex.Message}"); return; }

            foreach (var testCase in Step2_AddValidationCases)
            {
                RunStep(testCase.TestCaseId,
                    () => FillValidationAndAssert(testCase.ApproverCode, testCase.SaveBtn, testCase.TestCaseId, testCase.ExpectedErrors),
                    testCase.Progress, testCase.ProgressMsg);
            }

            // เคสเสริมเฉพาะหน้าจอ: พฤติกรรมเมื่อปิด Dialog ข้อความเตือน (Close → Yes / Cancel / No log แยกทีละเคส)
            RunCloseDialogSequence(
                $"{stage}_CloseYes",
                $"{stage}_CloseCancel",
                $"{stage}_CloseNo",
                "กรุณาเลือกหน้าจอและพนักงานที่ต้องการกำหนด",
                progressFrom: 32, progressTo: 40);
        }

        /// <summary>
        /// ขั้นที่ 3: เลือกแก้ไขรายการในตาราง (เลือกแถวตาม รหัสผู้อนุมัติ + หน้าจอ)
        /// </summary>
        public void RunEditTests()
        {
            TestProgressHelper.Report(42, "Starting Edit Operations...");

            CloseDanglingForm();

            for (int i = 0; i < Step3_EditCases.Length; i++)
            {
                var item = Step3_EditCases[i];
                try
                {
                    CloseDanglingForm();
                    ClickRowAndOpenEdit(item.Code, item.Screen);
                    FillEditData(item.Emp, item.KeepInSameLookup);
                    Step($"กด {item.SaveBtn}", () => ClickFormButton(item.SaveBtn));
                    if (item.NeedsManualClose) Step("กด Close", () => ClickFormButton(BtnClose, dismissInfo: false));

                    ExpectFormClosed();

                    string change = item.KeepInSameLookup ? $"ให้เหลือรหัสพนักงาน {item.Emp}" : $"เปลี่ยนรหัสพนักงานเป็น {item.Emp}";
                    string buttons = item.NeedsManualClose ? $"{item.SaveBtn} -> Close" : item.SaveBtn;

                    if (VerifyRecordInGrid(item.Code, item.Screen))
                        LogSuccess(item.Name, $"แก้ไข {item.Code} ({ScreenText(item.Screen)}) {change} ({buttons}) สำเร็จ");
                    else
                        LogFailure(item.Name, $"แก้ไข {item.Code} ({ScreenText(item.Screen)}) ({buttons}) แล้วแต่ไม่พบในตารางหลัก");
                }
                catch (Exception ex)
                {
                    LogFailure(item.Name, $"แก้ไข {item.Code} ({ScreenText(item.Screen)}) ไม่สำเร็จ: {ex.Message}{CleanupAfterFailure()}");
                    CloseDanglingForm();   // กด Close → No ไม่บันทึก ข้อมูลเดิมไม่เปลี่ยน
                }
                finally
                {
                    TestProgressHelper.Report(42 + ((i + 1) * 13 / Step3_EditCases.Length), $"Editing Record ({i + 1}/{Step3_EditCases.Length})...");
                }
            }
        }

        /// <summary>
        /// ขั้นที่ 4: แจ้งเตือนตอนแก้ไข — [แก้] ตัดออกแล้ว
        /// เก็บ method ไว้ไม่ให้ชุดทดสอบที่เรียกอยู่ compile ไม่ผ่าน (ลบการเรียกใน ApproveCenterModuleTests ได้เลย)
        /// </summary>
        public void RunEditValidationTests()
        {
            TestProgressHelper.Report(80, "Edit Validation skipped.");
        }

        /// <summary>
        /// ขั้นที่ 5: ทดสอบการลบรายการในตาราง (ลบเฉพาะแถวของ รหัสผู้อนุมัติ + หน้าจอ ที่กำหนด)
        /// </summary>
        public void RunDeleteTests()
        {
            const string baseName = "TestCase05_DeleteData";
            TestProgressHelper.Report(84, "Starting Delete Tests...");

            CloseDanglingForm();

            foreach (var testCase in Step5_DeleteCases)
            {
                RunDeleteCase(baseName, testCase.TargetCode, testCase.PickLastRow, testCase.DialogBtn, testCase.ExpectDeleted);
                TestProgressHelper.Report(testCase.Progress, testCase.ProgressMsg);
            }

            LogTotalSummary();
        }

        // --- Custom Mapping Logic ประจำหน้าจอนี้ ---

        /* แก้ไขรหัสพนักงาน: keepInSameLookup = true → ลบให้เหลือแค่ emp / false → ลบทั้งหมด แล้วกด ... ติ๊ก emp ใหม่ */
        private void FillEditData(string emp, bool keepInSameLookup)
        {
            if (keepInSameLookup)
            {
                Step($"ลบรหัสพนักงาน ให้เหลือแค่ {emp}", () => DeleteEmployeeCodes(emp));
            }
            else
            {
                Step("ลบรหัสพนักงาน", ClearEmployees);
                Step($"กด ... ติ๊ก {emp} กด Select", () => PickEmployees(emp));
            }
        }

        /* กรอกข้อมูลสำหรับ Validation ตอน New แล้วกดบันทึก พร้อมตรวจแจ้งเตือนที่คาดหวัง (หน้าจอนี้แจ้งเตือนครั้งละ 1 ข้อความ)
           เลือกรหัสผู้อนุมัติ แล้วลบรหัสพนักงานที่ฟอร์มโหลดมาออกทั้งหมด
           (EMP00001 มีข้อมูลอยู่แล้ว → พอเลือกรหัส ฟอร์มโหลดข้อมูลเดิมมาให้ ต้องลบออกให้เป็น "ไม่เลือกรหัสพนักงาน" จริงๆ
            ไม่มีการบันทึกเกิดขึ้น เพราะ Save ไม่ผ่าน validation และเคส CloseNo กด Close -> No ข้อมูลเดิมจึงไม่เปลี่ยน) */
        private void FillValidationAndAssert(string approverCode, string saveBtn, string testName, params string[] msgs)
        {
            Step($"รหัสผู้อนุมัติ: เลือก {approverCode}", () => PickApprover(approverCode));

            // รอฟอร์มโหลดข้อมูลเดิมของผู้อนุมัติ (ชื่อหน้าต่างเปลี่ยนเป็น "... : EMP00001 ชื่อ") — ถ้าไม่มีข้อมูลเดิมก็แค่รอครบเวลาแล้วไปต่อ
            WaitUntil(() => { var f = FindOpenForm(); return f != null && SafeName(f).Contains(approverCode); }, 3000);
            WaitUntil(() => EmployeeCodesInForm().Count > 0, 3000);   // รอจนตารางพนักงานแสดงข้อมูลเดิมแล้ว

            Step("ลบรหัสพนักงานทั้งหมด (ไม่เลือกรหัสพนักงาน)", ClearEmployees);

            Step($"กด {saveBtn}", () => ClickFormButton(saveBtn, dismissInfo: false));
            AssertMessage(msgs[0], testName, $"ตรวจสอบแจ้งเตือน '{msgs[0]}' สำเร็จ");
        }

        /* Method หน้าต่างบันทึกสิทธิ์การอนุมัติ (หน้าต่างที่มีปุ่ม Save and New) */
        private AutomationElement? FindOpenForm() =>
            GetAppWindows().FirstOrDefault(w => FindButtonIn(w, BtnSaveAndNew) != null);

        private AutomationElement RequireEditForm() =>
            FindOpenForm() ?? throw new Exception("หน้าต่างบันทึกสิทธิ์การอนุมัติไม่ได้เปิดอยู่ (ขั้นตอนก่อนหน้าอาจไม่สำเร็จ)");

        // ตัวช่วยเฉพาะหน้าจอนี้ (หน้าต่างเลือกข้อมูล / ตาราง DevExpress / กล่องข้อความ / การคลิกเมาส์จริง)
        // อยู่ท้ายไฟล์ ต่อจากหมวดที่ 3

        // =========================================================================================
        // หมวดที่ 3: ส่วนการทำงานกลางที่ตายตัว (Common / Reusable Helpers)
        // **ชื่อและหน้าที่เหมือนหน้า BANK — ข้างในใช้ตัวช่วยของหน้าจอนี้ เพราะไม่มี Task Result และหน้าต่างบันทึกไม่ใช่ Window ปกติ**
        // =========================================================================================

        private AutomationElement _mainWindow;   // ไม่ readonly: เปลี่ยนเป็นหน้าต่างหลักตัวจริงได้ ถ้า BaseTest ผูกไว้กับหน้าต่าง Login
        private readonly UIA3Automation _automation;
        private int _totalSuccess = 0;
        private int _totalFail = 0;
        private const int TimeoutMs = 6000;

        public PrivilegetoApproveInfoUC(AutomationElement mainWindow, UIA3Automation automation)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

        /* กด New เพื่อเปิดหน้าต่างบันทึกใหม่ */
        private AutomationElement OpenForm()
        {
            ClickToolbar(BtnNew);
            return RequireEditForm();
        }

        /* คลิกแถวของ <code> + <screen> ในตารางรายการ แล้วกด Edit */
        private AutomationElement ClickRowAndOpenEdit(string code, string? screen)
        {
            Step($"กด Edit ข้อมูลที่ {code} ({ScreenText(screen)})", () => EditRow(code, screen));
            return RequireEditForm();
        }

        /* Method ย่อยสำหรับจัดการเคสการลบข้อมูล — คืน true ถ้าผ่าน
           ผู้อนุมัติ 1 คนมีหลายแถว → คลิกแถวสุดท้าย/แถวแรกของรหัสตาม TestStep
           จำชื่อเมนูของแถวที่คลิก แล้วตรวจเฉพาะแถวนั้น (รหัส + ชื่อเมนู): Yes = หาย / No = ยังอยู่
           [แก้] เดิมนับจาก "Record x of N" แต่ UI Automation อ่านข้อความนั้นไม่ได้ */
        private bool RunDeleteCase(string baseName, string code, bool pickLast, string btn, bool expectDeleted)
        {
            string testName = $"{baseName}_Delete{btn}";
            string where = pickLast ? "แถวสุดท้าย" : "แถวแรก";
            try
            {
                RefreshList();
                string menu = "";
                Step($"คลิกข้อมูล {code} ({where})", () => menu = SelectCodeRow(code, pickLast));
                where += $": {menu}";
                Step("กดปุ่ม Delete", () => ClickToolbar(BtnDelete));
                Step($"กด {btn}", () => ClickMessageButton(btn));

                RefreshList();
                bool exists = VerifyRecordInGrid(code, menu);
                if (expectDeleted)
                {
                    if (exists) { LogFailure(testName, $"ลบข้อมูล '{code}' ({where}) (Delete -> {btn}) ไม่สำเร็จ: ข้อมูลยังอยู่ในตาราง"); return false; }
                    LogSuccess(testName, $"ลบข้อมูล '{code}' ({where}) (Delete -> {btn}) สำเร็จ");
                }
                else
                {
                    if (!exists) { LogFailure(testName, $"ยกเลิกการลบข้อมูล '{code}' ({where}) (Delete -> {btn}) ไม่สำเร็จ: ข้อมูลหายจากตาราง"); return false; }
                    LogSuccess(testName, $"ยกเลิกการลบข้อมูล '{code}' ({where}) (Delete -> {btn}) ข้อมูลยังคงอยู่สำเร็จ");
                }
                return true;
            }
            catch (Exception ex)
            {
                LogFailure(testName, $"ลบข้อมูล '{code}' ({where}) (Delete -> {btn}) ไม่สำเร็จ: {ex.Message}{CleanupAfterFailure()}");
                return false;
            }
        }

        /* Method เปิดหน้าจอด้วยการพิมพ์ชื่อเมนูในช่องค้นหา → กดค้นหา → คลิกผลลัพธ์ → รอปุ่ม New (โครงเดียวกับหน้า BANK) */
        private bool TryOpenMenu(string stage, string searchKeyword)
        {
            try
            {
                EnsureRealMainWindow();

                var searchBox = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(SearchBoxId))
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)), 4000)
                              ?? throw new Exception($"ไม่พบช่องค้นหา {SearchBoxId}");

                WaitUntil(() => { try { searchBox.Focus(); return true; } catch { return false; } }, 1000);

                if (searchBox.Patterns.Value.IsSupported)
                    searchBox.Patterns.Value.Pattern.SetValue(searchKeyword);
                else
                    Keyboard.Type(searchKeyword);

                var searchBtn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(SearchButtonId))
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("Find")), 4000)
                              ?? throw new Exception($"ไม่พบปุ่มค้นหา {SearchButtonId}");

                SafeClickElement(searchBtn);

                // ผลลัพธ์เมนู (ไม่เอาช่องค้นหาเองที่ตอนนี้มีข้อความเท่าชื่อเมนู / แท็บ / หน้าต่าง)
                var target = Normalize(searchKeyword);
                var menuResult = Retry(() =>
                    _mainWindow.FindAllDescendants()
                        .Where(e => HasArea(e) && !e.Equals(searchBox))
                        .Where(e =>
                        {
                            var t = SafeControlTypeValue(e);
                            return t != ControlType.Edit && t != ControlType.TabItem
                                && t != ControlType.Window && t != ControlType.TitleBar;
                        })
                        .FirstOrDefault(e => TextMatches(e, target))
                    ?? _mainWindow.FindFirstDescendant(cf => cf.ByName(SearchResultFallbackName)), 4000)
                               ?? throw new Exception("ไม่พบผลลัพธ์เมนูจากการค้นหา");

                try { menuResult.Focus(); } catch { }
                ClickCenter(menuResult);

                if (WaitNewButtonReady(3000)) return true;

                PressKey(VirtualKeyShort.ENTER);
                if (WaitNewButtonReady(3000)) return true;

                // สำรอง: บางเมนูของ DevExpress ต้องดับเบิลคลิก
                DoubleClickAt(GetCenter(menuResult.BoundingRectangle));
                return WaitNewButtonReady(8000);
            }
            catch (Exception ex)
            {
                LogFailure(stage, $"ค้นหาและเปิดหน้าจอไม่สำเร็จ: {ex.Message}{CleanupAfterFailure()}");
                return false;
            }
        }

        /* รอจนปุ่ม New ในแถบเครื่องมือพร้อมใช้งาน (เหมือนเงื่อนไขท้าย TryOpenMenu ของหน้า BANK) */
        private bool WaitNewButtonReady(int timeoutMs) =>
            WaitUntil(() =>
            {
                var newBtn = FindToolbarButton(BtnNew);
                return newBtn != null && newBtn.Properties.IsEnabled.ValueOrDefault && !newBtn.Properties.IsOffscreen.ValueOrDefault;
            }, timeoutMs);

        /* Method ทดสอบลำดับการกดปุ่ม Close และตอบ Dialog (Yes, Cancel, No)
           แต่ละเคสลง log ชื่อของตัวเอง (PASS/FAIL แยกกัน แบบหน้า BANK) — คืน true ถ้าผ่านครบ 3 เคส */
        private bool RunCloseDialogSequence(string yesName, string cancelName, string noName, string expectedMsg, int progressFrom, int progressTo)
        {
            int mid = progressFrom + (progressTo - progressFrom) / 2;

            // Close → Yes: บันทึกไม่ผ่าน ต้องเจอแจ้งเตือนเดิม และฟอร์มยังเปิดอยู่
            bool yesOk = RunStep(yesName, () =>
            {
                ClickCloseAndHandleDialog("Yes", "ใช่");
                Step("แจ้งเตือน + กด Close", () => ExpectMessageAndClose(expectedMsg));
                CheckFormOpenOnly();
                LogSuccess(yesName, "ทดสอบกด Close -> Yes สำเร็จ");
            }, progressFrom, "Validating: Close -> Yes...");

            // Close → Cancel: ฟอร์มยังเปิดอยู่
            bool cancelOk = RunStep(cancelName, () =>
            {
                ClickCloseAndHandleDialog("Cancel", "ยกเลิก");
                AssertFormOpen(cancelName, "ทดสอบกด Close -> Cancel หน้าต่างยังคงเปิดอยู่สำเร็จ");
            }, mid, "Validating: Close -> Cancel...");

            // Close → No: ฟอร์มถูกปิด
            bool noOk = RunStep(noName, () =>
            {
                ClickCloseAndHandleDialog("No", "ไม่ใช่");
                AssertFormClosed(noName, "ทดสอบกด Close -> No ปิดหน้าจอสำเร็จ");
            }, progressTo, "Validating: Close -> No...");

            return yesOk && cancelOk && noOk;
        }

        /* Method คลิกปุ่ม Close ของหน้าต่างบันทึกและจัดการ Pop-up ยืนยันที่เด้งขึ้นมา */
        private void ClickCloseAndHandleDialog(params string[] btnNames)
        {
            RequireEditForm();
            Step("กด Close", () => ClickFormButton(BtnClose, dismissInfo: false));
            Step($"กด {btnNames[0]}", () => ClickMessageButton(btnNames));
        }

        /* Method ตรวจสอบแจ้งเตือน แล้วกด Close ปิดแจ้งเตือน พร้อมบันทึกผลสำเร็จ */
        private void AssertMessage(string expected, string testName, string? customMsg = null)
        {
            Step("แจ้งเตือน + กด Close", () => ExpectMessageAndClose(expected));
            LogSuccess(testName, customMsg ?? $"พบข้อความ: '{expected}'");
        }

        /* Method ตรวจสอบว่าฟอร์มยังคงเปิดอยู่หรือไม่ */
        private void CheckFormOpenOnly()
        {
            WaitForAppIdle();   // รอโปรแกรมประมวลผลการกดปุ่มเสร็จก่อน (ถ้าฟอร์มกำลังปิด จะได้ไม่ตัดสินเร็วเกินไป)
            if (!WaitUntil(() => FindOpenForm() != null, 2000))
                throw new Exception("ฟอร์มถูกปิด");
        }

        /* Method Assert ยืนยันว่าฟอร์มยังเปิดอยู่พร้อมบันทึกผลสำเร็จ */
        private void AssertFormOpen(string testName, string? customMsg = null)
        {
            CheckFormOpenOnly();
            LogSuccess(testName, customMsg ?? "ฟอร์มถูกเปิดอยู่");
        }

        /* Method Assert ยืนยันว่าฟอร์มถูกปิดเรียบร้อยแล้ว */
        private void AssertFormClosed(string testName, string? customMsg = null)
        {
            ExpectFormClosed();
            LogSuccess(testName, customMsg ?? "ฟอร์มถูกปิด");
        }

        /* Method มีแถวของ <code> (+ หน้าจอ <screen> ถ้าระบุ) ในตารางรายการหรือไม่ */
        private bool VerifyRecordInGrid(string code, string? screen = null)
        {
            var grid = FindListGrid();
            if (WaitUntil(() => FindRowCell(TakeSnapshot(grid), code, screen) != null, 3000)) return true;   // ตารางโหลดข้อมูลใหม่เสร็จและเห็นแถวแล้ว
            var (_, cell) = ScrollFind(grid, s => FindRowCell(s, code, screen), maxMs: 20000);
            return cell != null;
        }

        /* สรุปผลรวมทั้งหน้าจอ (บรรทัด INFO สุดท้ายของ log) */
        private void LogTotalSummary() =>
            TestLogger.LogInfo(ScreenLogName, $"สรุปผลการทดสอบทั้งหมด: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");

        /* ให้ Test Method เอาไป Assert ได้ — ไม่งั้น Test Explorer จะขึ้นผ่านทุกครั้ง แม้ใน log มี FAIL */
        public int TotalSuccess => _totalSuccess;
        public int TotalFail => _totalFail;

        /* Method ทำขั้นตอนหนึ่ง ถ้าไม่สำเร็จจะโยน Exception พร้อมชื่อขั้นตอน (ให้รู้ว่าพังที่ขั้นไหน) */
        private static void Step(string stepName, Action action)
        {
            try { action(); }
            catch (Exception ex) { throw new Exception($"{stepName}: {ex.Message}", ex); }
        }

        /* Method ตัวช่วยรัน Test Step พร้อมดักจับ Exception อัตโนมัติ — คืน true ถ้าผ่าน */
        private bool RunStep(string testName, Action act)
        {
            try { act(); return true; }
            catch (Exception ex)
            {
                LogFailure(testName, $"ไม่ผ่าน: {ex.Message}{CleanupAfterFailure()}");
                return false;
            }
        }

        /* Method รวมการรัน Step ทดสอบ + รายงาน Progress ในจุดเดียวกัน — คืน true ถ้าผ่าน */
        private bool RunStep(string testName, Action act, int progress, string progressMessage)
        {
            bool ok = RunStep(testName, act);
            TestProgressHelper.Report(progress, progressMessage);
            return ok;
        }

        /* Method บันทึกผลเทสเมื่อสำเร็จ (เพิ่มจำนวน Success) */
        private void LogSuccess(string testName, string msg)
        {
            _totalSuccess++;
            TestLogger.LogSuccess(testName, msg);
        }

        /* Method บันทึกผลเทสเมื่อล้มเหลว (เพิ่มจำนวน Fail) */
        private void LogFailure(string testName, string msg)
        {
            _totalFail++;
            TestLogger.LogFailure(testName, msg);
        }

        /* รอจนโปรแกรม HRMI ประมวลผลการคลิก / กดคีย์ที่ส่งไปเสร็จ (UI thread ว่างและตอบสนองแล้ว)
           ใช้แทนการหน่วงเวลาแบบตายตัว — เช็คจากโปรแกรมจริงว่าพร้อมทำขั้นต่อไปหรือยัง */
        private void WaitForAppIdle(int timeoutMs = 3000)
        {
            try { Wait.UntilResponsive(_mainWindow, TimeSpan.FromMilliseconds(timeoutMs)); }
            catch { }
        }

        /* Method ตัวช่วยรันวนลูปเช็คเงื่อนไขตามเวลา Timeout ที่กำหนด */
        private static bool WaitUntil(Func<bool> condition, int timeoutMs = TimeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try { if (condition()) return true; } catch { }
            }
            return false;
        }

        /* Method ตัวช่วยสำหรับ Retry ค้นหา Element ซ้ำๆ จนกว่าจะเจอหรือหมดเวลา Timeout */
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


        // =========================================================================================
        // ตัวช่วยเฉพาะหน้าจอ "บันทึกสิทธิ์การอนุมัติ"
        // หน้าจอนี้ไม่มี Task Result, แจ้งเตือนเป็นกล่องข้อความ และหน้าต่างเลือกข้อมูล / ตาราง DevExpress ต้องคลิกเมาส์จริง
        // จึงต้องใช้ตัวช่วยชุดนี้แทนของหน้า BANK (หมวดที่ 1-3 ด้านบนใช้งานผ่านตัวช่วยเหล่านี้)
        // =========================================================================================

        // 2.3 ปุ่ม / เวลารอ ประจำหน้าจอ
        private const string BtnNew = "New";
        private const string BtnEdit = "Edit";
        private const string BtnDelete = "Delete";
        private const string BtnFind = "Find";               // ปุ่ม Find ในแถบเครื่องมือ — โหลด/รีเฟรชข้อมูลในตารางรายการ (เหมือน RefreshGrid ของหน้า BANK)
        private const string BtnSaveAndNew = "Save and New";
        private const string BtnClose = "Close";
        private const string LookupSelectName = "Select";
        private const string LookupCancelName = "Cancel";
        private static readonly string[] DialogButtonNames = { "OK", "Yes", "No", "Cancel", "ตกลง", "ใช่", "ไม่ใช่", "ยกเลิก" };

        private const int MessageWaitMs = 2500;
        private const int LookupOpenWaitMs = 15000;       // เวลาสูงสุดที่รอหน้าต่างเลือกข้อมูลเปิดหลังกด ... (เดิม 4000 สั้นเกินไป)
        private const int LookupRetryClickAfterMs = 4000; // ถ้ายังไม่เปิดภายในเวลานี้ ให้คลิก ... ซ้ำอีกครั้ง
        private const int LookupLoadWaitMs = 30000;       // [ใหม่] เวลารอตารางในหน้าต่างเลือกข้อมูลโหลดเสร็จ (มีข้อมูลเยอะ โหลดนาน)

        // หน้าต่างเลือกข้อมูลที่เปิดอยู่ (จับได้จาก "หน้าต่างใหม่ที่เพิ่งเด้ง" หลังกด ...)
        // ใช้กรณีหน้าต่างเลือกข้อมูลไม่มีปุ่ม Select/Cancel ครบตามที่ GetLookupWindow คาดไว้
        private AutomationElement? _activeLookup;

        //----
        // * Flow: เคลียร์สิ่งที่ค้างจากเคสก่อน *
        //----

        /* Method เคลียร์สิ่งที่ค้างหลังเคสล้มเหลว (หน้าต่างเลือกข้อมูล / error dialog ของแอป)
           ถ้าไม่ปิด error dialog มันเป็น modal บังทุกอย่าง ทำให้เคสถัดไปพังตามเป็นทอดๆ
           คืนข้อความเพิ่มเติมสำหรับต่อท้าย log (ว่างถ้าไม่มีอะไรค้าง) */
        private string CleanupAfterFailure()
        {
            CancelLookupIfOpen();
            string? leftover = TryDismissAppErrorDialog();
            return leftover != null ? $" | error dialog ค้างที่ปิดให้: {leftover}" : "";
        }

        /* Method เคลียร์สิ่งที่ค้างจากเคสก่อน: ปิดหน้าต่างเลือกข้อมูล, ปิดกล่องข้อความ,
           และถ้าหน้าต่างบันทึกยังเปิดอยู่ ให้กด Close → No (ไม่บันทึก) */
        private void CloseDanglingForm()
        {
            try
            {
                CancelLookupIfOpen();

                var box = WaitMessageBox(0);
                if (box != null) PressOkOrClose(box);

                if (FindOpenForm() == null) return;

                ClickFormButton(BtnClose, dismissInfo: false);
                var confirm = WaitMessageBox(2000);
                if (confirm != null)
                {
                    var no = FindDialogButton(confirm, "No", "ไม่ใช่");
                    if (no != null) { SafeClickElement(no); WaitClosed(confirm); }
                    else PressOkOrClose(confirm);
                }
                ExpectFormClosed();
            }
            catch { }
        }

        //----
        // * หน้าต่างหลัก / การคลิกเมาส์ *
        //----

        /* Method ตรวจว่า _mainWindow เป็นหน้าต่างหลักตัวจริง
           บางครั้ง BaseTest ผูกไว้กับหน้าต่าง "Login" (log: "ผูกหน้าต่างหลัก: Login") ทำให้หาช่องค้นหาเมนูไม่เจอ
           → รอหน้าต่างหลักของโปรแกรม (ชื่อมี "Prosoft HRMI" และไม่ใช่ Login) แล้วเปลี่ยนไปใช้ตัวนั้นแทน */
        private void EnsureRealMainWindow()
        {
            if (IsRealMainWindow(_mainWindow)) return;

            int? pid = null;
            try { pid = _mainWindow.Properties.ProcessId.ValueOrDefault; } catch { }

            var real = Retry(() => _automation.GetDesktop()
                .FindAllChildren(cf => cf.ByControlType(ControlType.Window))
                .FirstOrDefault(w =>
                {
                    try
                    {
                        bool sameApp = (pid.HasValue && pid.Value > 0 && w.Properties.ProcessId.ValueOrDefault == pid.Value)
                                       || (w.Name ?? "").Contains("Prosoft HRMI");
                        return sameApp && IsRealMainWindow(w);
                    }
                    catch { return false; }
                }), 30000);

            if (real == null)
                throw new Exception($"หน้าต่างที่ผูกไว้คือ '{SafeName(_mainWindow)}' ไม่ใช่หน้าต่างหลัก และรอ 30 วินาทีแล้วยังไม่พบหน้าต่างหลักของโปรแกรม (อาจยังล็อกอินไม่เสร็จ)");

            _mainWindow = real;
            try { real.AsWindow().SetForeground(); } catch { }
            WaitUntil(IsAppForeground, 3000);
        }

        /* หน้าต่างหลักตัวจริง = ยังเปิดอยู่ ไม่ใช่ Login และ (ชื่อมี "Prosoft HRMI" หรือมีช่องค้นหาเมนู baritemSearch / ช่อง Edit) */
        private static bool IsRealMainWindow(AutomationElement w)
        {
            try
            {
                if (!w.IsAvailable) return false;
                string name = w.Name ?? "";
                if (name.Contains("Login", StringComparison.OrdinalIgnoreCase) || name.Contains("เข้าสู่ระบบ")) return false;
                // หน้าต่างหลักตั้งชื่อว่า "Prosoft HRMI (...)" — ถ้าชื่อตรงและไม่ใช่ Login ถือว่าใช่เลย
                // (ช่องค้นหา baritemSearch บางครั้ง UIA ยังไม่เห็นตอนเช็ค หน้า BANK จึงมี fallback เป็น Edit แรก)
                if (name.Contains("Prosoft HRMI", StringComparison.OrdinalIgnoreCase)) return true;
                return w.FindFirstDescendant(cf => cf.ByAutomationId(SearchBoxId)) != null
                    || w.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)) != null;
            }
            catch { return false; }
        }

        private static Point GetCenter(Rectangle rect) => new Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);

        /* Method เลื่อนเมาส์ไปที่จุดนั้นแล้วคลิกซ้าย */
        private void ClickAt(Point p)
        {
            EnsureAppForeground();
            Mouse.MoveTo(p);
            WaitCursorAt(p);
            Mouse.Click(MouseButton.Left);
            WaitForAppIdle();
        }

        /* Method คลิกตรงกลางของ element */
        private void ClickCenter(AutomationElement e) => ClickAt(GetCenter(e.BoundingRectangle));

        /* Method ดับเบิลคลิกที่จุดนั้น */
        private void DoubleClickAt(Point p)
        {
            EnsureAppForeground();
            Mouse.MoveTo(p);
            WaitCursorAt(p);
            Mouse.DoubleClick(MouseButton.Left);
            WaitForAppIdle();
        }

        private void PressKey(VirtualKeyShort key)
        {
            EnsureAppForeground();
            Keyboard.Press(key);
            Keyboard.Release(key);
        }

        //----
        // * Navigation *
        //----

        /* Method แผงเมนูซ้าย: control แบบรายการที่อยู่ซ้ายสุดและอยู่ในโซนซ้ายของหน้าต่างหลัก
           (ยังต้องเก็บไว้ — FindListGrid ใช้ตัดแผงเมนูออกจากตัวเลือก "ตารางรายการ")
           [แก้] เดิมรับเฉพาะ Tree — เมนู DevExpress TreeList มักโผล่เป็น Table/DataGrid จึงหาไม่เจอ */
        private AutomationElement? FindMenuPanel()
        {
            try
            {
                var main = _mainWindow.BoundingRectangle;
                int limitX = main.Left + (int)(main.Width * MenuPanelWidthRatio);

                return _mainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Tree)
                        .Or(cf.ByControlType(ControlType.Table))
                        .Or(cf.ByControlType(ControlType.DataGrid))
                        .Or(cf.ByControlType(ControlType.List)))
                    .Where(HasArea)
                    .Where(t => GetCenter(t.BoundingRectangle).X <= limitX)
                    .OrderBy(t => t.BoundingRectangle.Left)
                    .ThenByDescending(t => t.BoundingRectangle.Height)
                    .FirstOrDefault();
            }
            catch { return null; }
        }

        /* Method เทียบข้อความของ element กับเป้าหมาย (normalize แล้ว) จาก Name / Value / LegacyIAccessible.Value */
        private static bool TextMatches(AutomationElement e, string normalizedTarget)
        {
            try { if (Normalize(e.Name) == normalizedTarget) return true; } catch { }
            try { if (Normalize(e.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault) == normalizedTarget) return true; } catch { }
            try { if (Normalize(e.Patterns.LegacyIAccessible.PatternOrDefault?.Value.ValueOrDefault) == normalizedTarget) return true; } catch { }
            return false;
        }

        private static ControlType SafeControlTypeValue(AutomationElement e)
        {
            try { return e.Properties.ControlType.ValueOrDefault; }
            catch { return ControlType.Custom; }
        }

        //----
        // * Windows *
        //----

        /* Method หน้าต่างทั้งหมดของโปรแกรม (ยกเว้นหน้าจอหลัก) ทั้งแบบลอยและแบบอยู่ในหน้าจอหลัก */
        private List<AutomationElement> GetAppWindows()
        {
            var windows = new List<AutomationElement>();
            try
            {
                windows.AddRange(_mainWindow.AsWindow()?.ModalWindows ?? Array.Empty<Window>());
            }
            catch { }
            try
            {
                int pid = _mainWindow.Properties.ProcessId.ValueOrDefault;
                windows.AddRange(_automation.GetDesktop()
                    .FindAllChildren(cf => cf.ByControlType(ControlType.Window).And(cf.ByProcessId(pid)))
                    .Where(w => !w.Equals(_mainWindow)));
            }
            catch { }
            try { windows.AddRange(_mainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Window))); }
            catch { }

            // [แก้] หน้าต่างที่ซ้อนอยู่ "ข้างใน" หน้าต่างอื่น — กล่องยืนยัน 'คำเตือน' (Yes/No/Cancel) ของแอปนี้
            //       เป็น Window ลูกของหน้าต่างบันทึก ไม่ใช่หน้าต่างระดับบนสุด เดิมจึงหาไม่เจอเลย
            //       → กด Close แล้วติดกล่องยืนยันค้าง ทำให้เคสหลังจากนั้นล้มตามกันหมด
            foreach (var w in windows.ToList())
            {
                if (w.Equals(_mainWindow)) continue;
                try { windows.AddRange(w.FindAllDescendants(cf => cf.ByControlType(ControlType.Window))); }
                catch { }
            }
            return windows.Where(w => !w.Equals(_mainWindow)).Where(HasArea).Distinct().ToList();
        }

        /* Method key ประจำหน้าต่าง (ใช้เทียบว่าหน้าต่างไหน "เพิ่งเด้งขึ้นมาใหม่") */
        private static string WindowKey(AutomationElement w)
        {
            try
            {
                var h = w.Properties.NativeWindowHandle.ValueOrDefault;
                if (h != IntPtr.Zero) return h.ToString();
            }
            catch { }
            try { return $"{SafeName(w)}|{w.BoundingRectangle}"; }
            catch { return SafeName(w); }
        }

        private static bool HasDataGrid(AutomationElement w)
        {
            try { return w.FindFirstDescendant(cf => cf.ByControlType(ControlType.Table).Or(cf.ByControlType(ControlType.DataGrid))) != null; }
            catch { return false; }
        }

        private static AutomationElement? FindButtonIn(AutomationElement root, params string[] names)
        {
            try
            {
                return root.FindAllDescendants(cf => ButtonNameCondition(cf, names)).FirstOrDefault(IsVisible);
            }
            catch { return null; }
        }

        /* Method เงื่อนไขชื่อปุ่ม: ทั้งแบบปกติและแบบมี & นำหน้า (เช่น Yes และ &Yes) */
        private static FlaUI.Core.Conditions.ConditionBase ButtonNameCondition(
            FlaUI.Core.Conditions.ConditionFactory cf, string[] names)
        {
            FlaUI.Core.Conditions.ConditionBase? nameCond = null;
            foreach (var n in names.SelectMany(n => new[] { n, "&" + n }).Distinct())
            {
                var c = cf.ByName(n);
                nameCond = nameCond == null ? c : nameCond.Or(c);
            }
            return cf.ByControlType(ControlType.Button).And(nameCond!);
        }

        /* Method ปุ่มในแถบเครื่องมือของหน้ารายการ (New / Edit / Delete) — ไม่นับปุ่มในหน้าต่างบันทึก */
        private AutomationElement? FindToolbarButton(string name)
        {
            var form = FindOpenForm();
            try
            {
                return _mainWindow.FindAllDescendants(cf => ButtonNameCondition(cf, new[] { name }))
                    .Where(IsVisible)
                    .FirstOrDefault(b => form == null || !form.BoundingRectangle.Contains(GetCenter(b.BoundingRectangle)));
            }
            catch { return null; }
        }

        /* Method หน้าต่างเลือกข้อมูล: แบบมาตรฐาน (มีปุ่ม Select และ Cancel)
           หรือหน้าต่างที่จับได้ตอนเด้งขึ้นมาหลังกด ... (_activeLookup) ถ้ายังเปิดอยู่ */
        private AutomationElement? GetLookupWindow()
        {
            var strict = GetAppWindows().FirstOrDefault(w =>
                FindButtonIn(w, LookupSelectName) != null
                && FindButtonIn(w, LookupCancelName) != null
                && FindButtonIn(w, BtnSaveAndNew) == null);
            if (strict != null) return strict;

            if (_activeLookup != null && IsVisible(_activeLookup)) return _activeLookup;
            _activeLookup = null;
            return null;
        }

        /* Method element ระดับบนสุดทั้งหมดของ process (ทุก ControlType — popup ของ DevExpress มักเป็น Pane ไม่ใช่ Window) */
        private List<AutomationElement> GetProcessTopLevels()
        {
            try
            {
                int pid = _mainWindow.Properties.ProcessId.ValueOrDefault;
                return _automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(pid)).Where(HasArea).ToList();
            }
            catch { return new List<AutomationElement>(); }
        }

        /* Method ตาราง/รายการทั้งหมดที่มองเห็นในโปรแกรม (ใช้จับว่ามี "ตารางใหม่" โผล่ขึ้นมาหลังกด ...) */
        private List<AutomationElement> GetAllGrids()
        {
            var result = new List<AutomationElement>();
            foreach (var root in GetProcessTopLevels())
            {
                try
                {
                    result.AddRange(root.FindAllDescendants(cf => cf.ByControlType(ControlType.Table)
                        .Or(cf.ByControlType(ControlType.DataGrid))
                        .Or(cf.ByControlType(ControlType.List))).Where(IsVisible));
                }
                catch { }
            }
            return result;
        }

        private static string ElementKey(AutomationElement e)
        {
            try
            {
                var id = e.Properties.RuntimeId.ValueOrDefault;
                if (id != null && id.Length > 0) return "rt:" + string.Join(".", id);
            }
            catch { }
            return "w:" + WindowKey(e);
        }

        /* Method เก็บสภาพหน้าจอก่อนกด ... (หน้าต่างบนสุด + ตารางทั้งหมด) ไว้เทียบหาสิ่งที่เพิ่งเด้งขึ้นมา */
        private HashSet<string> SnapshotUi()
        {
            var keys = new HashSet<string>();
            foreach (var w in GetProcessTopLevels()) keys.Add(ElementKey(w));
            foreach (var w in GetAppWindows()) keys.Add(ElementKey(w));
            foreach (var g in GetAllGrids()) keys.Add(ElementKey(g));
            return keys;
        }

        /* Method หน้าต่างเลือกข้อมูลที่เพิ่งเด้งหลังกด ... ได้ทั้ง 2 แบบ:
           1) หน้าต่าง/popup ใหม่ (Window หรือ Pane) ที่มีตารางหรือปุ่ม Select (ไม่ใช่หน้าต่างบันทึก/กล่องยืนยัน)
           2) "ตารางใหม่" ที่โผล่ขึ้นมา (dropdown ฝังอยู่ในหน้าต่างบันทึก) → ใช้ container ของตารางนั้น */
        private AutomationElement? FindNewLookupCandidate(HashSet<string> before)
        {
            var newTop = GetProcessTopLevels().Concat(GetAppWindows())
                .Where(w => !before.Contains(ElementKey(w)))
                .Where(w => FindButtonIn(w, BtnSaveAndNew) == null)
                .Where(w => FindButtonIn(w, "Yes", "No", "ใช่", "ไม่ใช่") == null)
                .FirstOrDefault(w => HasDataGrid(w) || FindButtonIn(w, LookupSelectName) != null
                                     || HasListItems(w));
            if (newTop != null) return newTop;

            var newGrid = GetAllGrids().FirstOrDefault(g => !before.Contains(ElementKey(g)));
            if (newGrid == null) return null;
            try { return newGrid.Parent ?? newGrid; } catch { return newGrid; }
        }

        private static bool HasListItems(AutomationElement w)
        {
            try { return w.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataItem).Or(cf.ByControlType(ControlType.ListItem))) != null; }
            catch { return false; }
        }

        private static string Normalize(string? s) => new string((s ?? "").Where(c => !char.IsWhiteSpace(c)).ToArray());

        //----
        // * Helpers: หน้ารายการ (New / Edit / Delete) *
        //----

        /* Method กดปุ่มในแถบเครื่องมือของหน้ารายการ */
        private void ClickToolbar(string name)
        {
            var btn = Retry(() => FindToolbarButton(name), timeoutMs: 8000)
                ?? throw new Exception($"หาปุ่ม '{name}' ไม่เจอ");
            WaitUntilEnabled(btn, timeoutMs: 3000);
            SafeClickElement(btn);

            if ((name == BtnNew || name == BtnEdit) && Retry(FindOpenForm, timeoutMs: 8000) == null)
            {
                // ถ้าฟอร์มไม่เปิดเพราะแอปโยน error ให้รายงานข้อความจริง
                string? err = TryDismissAppErrorDialog();
                if (err != null) throw new Exception($"กด {name} แล้วแอปแสดง error: {err}");
                throw new Exception($"กด {name} แล้ว หน้าต่างบันทึกไม่เปิด");
            }
        }

        /* Method ตารางรายการในหน้าหลัก (ตารางที่ใหญ่ที่สุดที่ไม่ได้อยู่ในหน้าต่างบันทึก/เลือกข้อมูล/กล่องข้อความ)
           [แก้] เดิมตัดตารางที่อยู่ใน "ทุกหน้าต่าง" ของโปรแกรมทิ้ง ซึ่งรวมหน้าต่างหน้าจอรายการเองด้วย
           (หน้าจอเปิดเป็น Window ซ้อนในหน้าหลัก) จึงหาตารางไม่เจอทุกครั้ง — ตอนนี้ตัดเฉพาะหน้าต่างที่บังจริง
           [แก้] ตัดแผงเมนูซ้ายออกด้วย — เดิมถ้าหน้าจอไม่เปิด จะไปหยิบแผงเมนู Approve Center มาเป็น "ตารางรายการ" */
        private AutomationElement FindListGrid()
        {
            return Retry(() =>
            {
                var blockers = new[] { FindOpenForm(), GetLookupWindow(), FindMessageBox() }
                    .Where(w => w != null)
                    .Select(w => w!.BoundingRectangle)
                    .ToList();
                var menu = FindMenuPanel();
                bool Free(AutomationElement g)
                {
                    var c = GetCenter(g.BoundingRectangle);
                    return !blockers.Any(r => r.Contains(c))
                        && (menu == null || !menu.BoundingRectangle.Contains(c));
                }

                var grids = _mainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Table).Or(cf.ByControlType(ControlType.DataGrid)))
                    .Where(HasArea).Where(Free).ToList();
                if (grids.Count == 0)   // บางหน้ารายการเป็น TreeList
                    grids = _mainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Tree))
                        .Where(HasArea).Where(Free).ToList();

                return grids.OrderByDescending(g => g.BoundingRectangle.Width * g.BoundingRectangle.Height).FirstOrDefault();
            }, timeoutMs: 8000) ?? throw new Exception($"หาตารางรายการในหน้า บันทึกสิทธิ์การอนุมัติ ไม่เจอ (หน้าต่างที่เห็น: {DescribeAppWindows()})");
        }

        /* [ใหม่] ช่องรหัส <code> ที่แถวเดียวกันมีชื่อหน้าจอ <screen> (screen = null → แถวไหนก็ได้)
           ผู้อนุมัติ 1 คนมีหลายแถว (1 แถวต่อ 1 หน้าจอ) — เดิมหยิบแถวแรกของรหัส ซึ่งเป็นหน้าจออื่น (บันทึกผลสัมภาษณ์) */
        private static GridCell? FindRowCell(GridSnapshot s, string code, string? screen)
        {
            var target = screen == null ? null : Normalize(screen);
            return s.Cells.Where(c => c.Is(code)).FirstOrDefault(c =>
                target == null
                || s.Cells.Any(o => SameRow(o, c) && (Normalize(o.Value) == target || Normalize(o.Name) == target)));
        }

        /* Method คลิกแถวของ <code> (+ หน้าจอ <screen>) ในตารางรายการ */
        private void SelectListRow(string code, string? screen = null)
        {
            var grid = FindListGrid();
            var (snap, cell) = ScrollFind(grid, s => FindRowCell(s, code, screen), maxMs: 20000);
            if (cell == null)
            {
                // ไม่เจอ → กด Find โหลดข้อมูลใหม่ แล้วหาอีกรอบ
                RefreshList();
                grid = FindListGrid();
                (snap, cell) = ScrollFind(grid, s => FindRowCell(s, code, screen), maxMs: 20000);
            }
            if (cell == null)
                throw new Exception($"หาแถว '{code}'{(screen != null ? $" หน้าจอ '{screen}'" : "")} ในรายการไม่เจอ (ตารางที่เลือก {snap.GridRect.Width}x{snap.GridRect.Height} @{snap.GridRect.X},{snap.GridRect.Y} | ข้อความในตารางที่เห็นล่าสุด: {DescribeCells(snap)})");

            ClickAt(new Point(cell.CX, cell.CY));
            WaitRowSelected();
        }

        /* Method คลิกแถวของ <code> + <screen> แล้วกด Edit → รอฟอร์มโหลดข้อมูล → เลือกหน้าจอนั้นในฟอร์ม
           (ให้ตารางพนักงานในฟอร์มเป็นของหน้าจอนี้แน่ๆ ไม่ไปแก้หน้าจออื่น) */
        private void EditRow(string code, string? screen)
        {
            SelectListRow(code, screen);
            ClickToolbar(BtnEdit);
            WaitUntil(() => { var f = FindOpenForm(); return f != null && SafeName(f).Contains(code); }, 5000);
            if (screen != null) SelectScreen(screen);   // ไม่ระบุหน้าจอ = ใช้หน้าจอของแถวที่คลิก (ฟอร์มเปิดมาที่หน้าจอนั้นอยู่แล้ว)
            WaitForAppIdle();
        }

        /* Method กดปุ่ม Find ในแถบเครื่องมือ เพื่อโหลดข้อมูลตารางรายการใหม่ (best-effort — ไม่ throw) */
        private void RefreshList()
        {
            try
            {
                var btn = Retry(() => FindToolbarButton(BtnFind), timeoutMs: 2000);
                if (btn == null) return;
                SafeClickElement(btn);
                WaitForAppIdle();
                // รอให้ตารางโหลดเสร็จ (ข้อความ "กำลังโหลด" หาย)
                WaitUntil(() =>
                {
                    var g = FindListGrid();
                    return !TakeSnapshot(g).Cells.Any(c => (c.Value + c.Name).Contains("กำลังโหลด"));
                }, 5000);
            }
            catch { }
        }

        //----
        // * Helpers: หน้าต่างบันทึก *
        //----

        /* Method หา control ในหน้าต่างบันทึกตาม AutomationId — ถ้าไม่เจอ บอกรายชื่อ id ที่มีให้ */
        private AutomationElement FindInForm(string automationId)
        {
            var form = RequireEditForm();
            return Retry(() =>
                form.FindAllDescendants(cf => cf.ByAutomationId(automationId)).FirstOrDefault(IsVisible),
                timeoutMs: 5000)
                ?? throw new Exception($"หา control '{automationId}' ในหน้าต่างบันทึกไม่เจอ (id ที่มี: {DescribeFormIds(form)})");
        }

        private static string DescribeFormIds(AutomationElement form)
        {
            try
            {
                var ids = form.FindAllDescendants()
                    .Select(e => { try { return e.Properties.AutomationId.ValueOrDefault ?? ""; } catch { return ""; } })
                    .Where(id => id.Length > 0 && !id.All(char.IsDigit))
                    .Distinct()
                    .Take(40);
                return string.Join(", ", ids);
            }
            catch { return "(อ่านไม่ได้)"; }
        }

        /* Method เช็คครั้งเดียวว่าหน้าต่างเลือกข้อมูลเปิดแล้วหรือยัง (และ throw ทันทีถ้าแอปโยน error) */
        private bool TryDetectLookup(HashSet<string> before, string what)
        {
            if (GetLookupWindow() != null) return true;

            var fresh = FindNewLookupCandidate(before);
            if (fresh != null)
            {
                _activeLookup = ExpandToLookupContainer(fresh);
                return true;
            }

            string? err = TryDismissAppErrorDialog();
            if (err != null) throw new Exception($"{what} แล้วแอปแสดง error: {err}");
            return false;
        }

        /* Method เปิดหน้าต่างเลือกข้อมูลของช่อง ButtonEdit: กดปุ่ม ... ครั้งเดียว แล้วรอหน้าต่างเปิด */
        private void OpenLookup(string automationId)
        {
            CancelLookupIfOpen();
            var control = FindInForm(automationId);
            WaitUntilEnabled(control, timeoutMs: 3000);

            // กดปุ่ม ... ของช่อง ครั้งเดียว แล้วรอหน้าต่างเลือกข้อมูลเปิด
            var btn = Retry(() => FindChildEllipsis(control), timeoutMs: 3000)
                ?? throw new Exception($"หาปุ่ม ... ของ '{automationId}' ไม่เจอ");

            string what = $"กด ... ของ '{automationId}'";
            var before = SnapshotUi();
            SafeClickElement(btn);

            if (!WaitDetectLookup(before, what, LookupOpenWaitMs))
                throw new Exception($"{what} แล้ว หน้าต่างเลือกข้อมูลไม่เปิด (หน้าต่างที่เห็น: {DescribeAppWindows()})");
        }

        /* Method รอจนเจอหน้าต่างเลือกข้อมูล (ไม่ใช้ WaitUntil เพราะ WaitUntil กลืน exception — error ของแอปต้องโยนออกมา) */
        private bool WaitDetectLookup(HashSet<string> before, string what, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (TryDetectLookup(before, what)) return true;
            }
            return false;
        }

        /* Method ปุ่ม ... ที่เป็นลูกของช่อง (ปุ่มขวาสุด) — DevExpress บางรุ่นไม่เปิดให้ UIA เห็น จะคืน null */
        private static AutomationElement? FindChildEllipsis(AutomationElement control)
        {
            try
            {
                return control.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                    .Where(IsVisible)
                    .OrderByDescending(b => b.BoundingRectangle.Left)
                    .FirstOrDefault();
            }
            catch { return null; }
        }

        /* Method ไล่ parent ขึ้นไปหา container ที่มีปุ่ม Select + Cancel (ตัวหน้าต่าง "ข้อมูลพนักงาน" ทั้งบาน)
           กรณีจับหน้าต่างเลือกข้อมูลได้จาก "ตารางใหม่" จะได้แค่ส่วนตาราง ซึ่งไม่มีปุ่ม Select ให้กด */
        private AutomationElement ExpandToLookupContainer(AutomationElement el)
        {
            AutomationElement? cur = el;
            for (int i = 0; i < 12 && cur != null; i++)
            {
                try
                {
                    if (FindButtonIn(cur, BtnSaveAndNew) != null) break;   // ขึ้นไปถึงหน้าต่างบันทึกแล้ว
                    if (FindButtonIn(cur, LookupSelectName) != null && FindButtonIn(cur, LookupCancelName) != null) return cur;
                    cur = cur.Parent;
                }
                catch { break; }
            }
            return el;
        }

        /* Method รอให้ตารางในหน้าต่างเลือกข้อมูลโหลดแถวเสร็จ (มีข้อความอย่างน้อย 1 ช่อง และไม่มีข้อความ "กำลังโหลด") */
        private void WaitLookupRowsLoaded(AutomationElement grid, int timeoutMs = LookupLoadWaitMs) =>
            WaitUntil(() =>
            {
                var cells = TakeSnapshot(grid).Cells;
                bool loading = cells.Any(c => (c.Value + c.Name).Contains("กำลังโหลด"));
                return !loading && cells.Any(c => c.Value.Trim().Length > 0);
            }, timeoutMs);

        /* Method รหัสผู้อนุมัติ: กด ... เลือก <code> กด Select */
        private void PickApprover(string code)
        {
            try
            {
                OpenLookup(ApproverFieldId);
                var lookup = GetLookupWindow() ?? throw new Exception("หน้าต่างเลือกข้อมูลไม่ได้เปิดอยู่");
                var grid = GetLookupGrid(lookup);
                WaitLookupRowsLoaded(grid);

                var (snap, cell) = ScrollFind(grid, s => s.Cells.FirstOrDefault(c => c.Is(code)));
                if (cell == null)
                    throw new Exception($"หารหัส '{code}' ในหน้าต่างเลือกข้อมูลไม่เจอ (ข้อความในตารางที่เห็นล่าสุด: {DescribeCells(snap)})");

                // คลิกแถวให้เป็นแถวที่เลือก แล้วยืนยัน (Select / OK / ดับเบิลคลิกแถว ตามที่หน้าต่างมี)
                ClickAt(new Point(cell.CX, cell.CY));
                ClickLookupSelect();
            }
            catch
            {
                CancelLookupIfOpen();
                throw;
            }
        }

        /* Method เลือกรหัสพนักงาน: กด ... ติ๊ก ☑ <codes> กด Select */
        private void PickEmployees(params string[] codes)
        {
            // หน้าต่าง "ข้อมูลพนักงาน" ไม่แสดงพนักงานที่อยู่ในตารางแล้ว
            // ถ้ารหัสอยู่ในตารางพนักงานของฟอร์มแล้ว ข้ามไป ไม่ต้องติ๊กซ้ำ
            var already = EmployeeCodesInForm();
            var missing = codes.Where(c => !already.Contains(c)).ToArray();
            if (missing.Length == 0) return;

            try
            {
                OpenEmployeeLookup();
                var lookup = GetLookupWindow() ?? throw new Exception("หน้าต่างเลือกข้อมูลไม่ได้เปิดอยู่");
                var grid = GetLookupGrid(lookup);
                WaitLookupRowsLoaded(grid);

                // [ใหม่] ยังโหลดไม่เสร็จ → แจ้งตรงๆ (เดิมหาในตารางที่ยังโหลดไม่ครบ แล้วบอกว่าหารหัสไม่เจอ)
                if (TakeSnapshot(grid).Cells.Any(c => (c.Value + c.Name).Contains("กำลังโหลด")))
                    throw new Exception($"หน้าต่างเลือกข้อมูลยังโหลดไม่เสร็จใน {LookupLoadWaitMs / 1000} วินาที");

                foreach (var code in missing) TickLookupCode(code);
                ClickLookupSelect();
            }
            catch
            {
                CancelLookupIfOpen();
                throw;
            }
        }

        /* รหัสพนักงานที่อยู่ในตารางพนักงานของหน้าต่างบันทึกแล้ว (ของหน้าจอที่เลือกอยู่) */
        private HashSet<string> EmployeeCodesInForm()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var grid = FindInForm(EmployeeGridFieldId);
                foreach (var c in EmpCodeCells(TakeSnapshot(grid)))
                {
                    var v = c.Value.Trim();
                    if (v.Length > 0) set.Add(v);
                }
            }
            catch { }
            return set;
        }

        /* Method ลบรหัสพนักงานทั้งหมดในตารางพนักงาน */
        private void ClearEmployees() => DeleteEmployeeCodes();

        /// <summary>
        /// ข้อมูลสิทธิ์การอนุมัติสำหรับแต่ละหน้าจอ: เลือก &lt;screen&gt; — คลิกชื่อหน้าจอใน devTreelistMenu
        /// (ตารางพนักงานด้านขวาจะเปลี่ยนเป็นของหน้าจอที่เลือก)
        /// </summary>
        private void SelectScreen(string screenName)
        {
            var tree = FindInForm(ScreenListFieldId);
            var target = Normalize(screenName);

            var (snap, node) = ScrollFind(tree,
                s => s.Cells.FirstOrDefault(c => Normalize(c.Value) == target || Normalize(c.Name) == target),
                keepWide: true);
            if (node == null)
                throw new Exception($"หาหน้าจอ '{screenName}' ไม่เจอ (รายการที่เห็นล่าสุด: {DescribeCells(snap)})");

            ClickAt(new Point(node.CX, node.CY));
        }

        /* Method ช่องในคอลัมน์ รหัสพนักงาน ของตารางพนักงาน (เรียงจากบนลงล่าง)
           หัวคอลัมน์: ชื่อตรง "รหัสพนักงาน" → มีคำว่า "รหัส" → ซ้ายสุดที่มีคำว่า "พนักงาน" แต่ไม่ใช่ "ชื่อ..." */
        private static List<GridCell> EmpCodeCells(GridSnapshot s)
        {
            var headers = s.Headers.Where(h => s.GridRect.Contains(new Point(h.CX, h.CY))).ToList();
            var col =
                   headers.FirstOrDefault(h => h.Is(ColEmpCode))
                ?? headers.FirstOrDefault(h => (h.Name + h.Value).Contains("รหัส"))
                ?? headers.Where(h => (h.Name + h.Value).Contains("พนักงาน") && !(h.Name + h.Value).Contains("ชื่อ"))
                          .OrderBy(h => h.CX).FirstOrDefault();
            if (col == null) return new List<GridCell>();

            return s.Cells
                .Where(c => c.Rect.Left <= col.CX && col.CX <= c.Rect.Right && c.Checked == null)
                .Where(c => c.Rect.Width < col.Rect.Width + 20)   // เป็นช่อง ไม่ใช่ทั้งแถว
                .OrderBy(c => c.CY)
                .ToList();
        }

        /// <summary>
        /// ลบรหัสพนักงาน (ในตารางพนักงานของหน้าต่างบันทึก): คลิกช่อง รหัสพนักงาน → Ctrl+A → Delete → Enter
        /// ทีละช่อง ยกเว้นรหัสใน keep — keep ว่าง = ลบทั้งหมด
        /// (หน้าต่าง "ข้อมูลพนักงาน" ไม่แสดงพนักงานที่อยู่ในตารางแล้ว จึงลบจากในหน้าต่างนั้นไม่ได้)
        /// </summary>
        private void DeleteEmployeeCodes(params string[] keep)
        {
            CancelLookupIfOpen();
            var grid = FindInForm(EmployeeGridFieldId);

            const int maxDelete = 30;
            for (int i = 0; i < maxDelete; i++)
            {
                var target = EmpCodeCells(TakeSnapshot(grid))
                    .FirstOrDefault(c => c.Value.Trim().Length > 0
                                         && !keep.Contains(c.Value.Trim(), StringComparer.OrdinalIgnoreCase));
                if (target == null) return;   // ลบครบแล้ว

                string code = target.Value.Trim();
                ClickAt(new Point(target.CX - target.Rect.Width / 4, target.CY));   // คลิกฝั่งซ้ายของช่อง (ไม่โดนปุ่ม ...)
                Keyboard.Press(VirtualKeyShort.CONTROL);
                PressKey(VirtualKeyShort.KEY_A);
                Keyboard.Release(VirtualKeyShort.CONTROL);
                PressKey(VirtualKeyShort.DELETE);
                PressKey(VirtualKeyShort.RETURN);
                WaitForAppIdle();   // รอโปรแกรมประมวลผลการลบเสร็จ ก่อนเช็คว่ามี error ไหม

                string? err = TryDismissAppErrorDialog();
                if (err != null) throw new Exception($"ลบรหัส '{code}' แล้วแอปแสดง error: {err}");

                if (!WaitUntil(() => !EmpCodeCells(TakeSnapshot(grid)).Any(c => c.Is(code)), 3000))
                    throw new Exception($"ลบรหัส '{code}' ในตารางพนักงานไม่สำเร็จ (ยังแสดงอยู่)");
            }
            throw new Exception($"ลบรหัสพนักงานเกิน {maxDelete} รายการแล้วยังไม่หมด");
        }

        /// <summary>
        /// รหัสพนักงาน: กด ... — คลิกช่อง รหัสพนักงาน ของแถวว่างแถวแรกใน devGridListViewEmp
        /// แล้วกดปุ่ม ... (ชื่อ 'Editing control') จากนั้นรอหน้าต่าง "ข้อมูลพนักงาน" เปิด
        /// </summary>
        private void OpenEmployeeLookup()
        {
            CancelLookupIfOpen();
            var grid = FindInForm(EmployeeGridFieldId);

            // หาแถวว่างแรกในคอลัมน์ รหัสพนักงาน (ไม่เจอหัวคอลัมน์ใช้คอลัมน์แรก)
            var (snap, cell) = ScrollFind(grid, s =>
                EmpCodeCells(s).FirstOrDefault(c => c.Value.Trim().Length == 0));

            if (cell == null)
                throw new Exception($"หาแถวว่างในคอลัมน์ '{ColEmpCode}' ของตารางพนักงานไม่เจอ (หัวคอลัมน์: {string.Join(" | ", snap.Headers.Select(h => $"'{h.Name}/{h.Value}'@{h.Rect.X}"))} | ข้อความในตาราง: {DescribeCells(snap)})");

            var rect = cell.Rect;

            // คลิกช่อง รหัสพนักงาน ให้เข้าโหมดแก้ไข → ปุ่ม ... โผล่ในช่อง → กด ... ครั้งเดียว
            ClickAt(new Point(cell.CX, cell.CY));
            var btn = Retry(() => FindCellEllipsis(grid, rect), timeoutMs: 3000)
                ?? throw new Exception($"คลิกช่อง '{ColEmpCode}' แล้วไม่เจอปุ่ม ... ในช่อง");

            const string what = "กด ... ในตารางพนักงาน";
            var before = SnapshotUi();
            ClickElementByPhysicalMouse(btn);

            if (!WaitDetectLookup(before, what, LookupOpenWaitMs))
                throw new Exception($"{what} แล้ว หน้าต่างเลือกข้อมูลไม่เปิด (หน้าต่างที่เห็น: {DescribeAppWindows()})");
        }

        /* Method หาปุ่ม ... ที่อยู่ภายในกรอบช่อง (ตัวแก้ไขของช่องตอนอยู่โหมดแก้ไข) — ไม่รวม scrollbar/ปุ่มนอกช่อง */
        private static AutomationElement? FindCellEllipsis(AutomationElement grid, Rectangle cellRect)
        {
            try
            {
                return grid.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                    .Where(IsVisible)
                    .Where(b =>
                    {
                        var r = b.BoundingRectangle;
                        var c = GetCenter(r);
                        return c.X >= cellRect.Left && c.X <= cellRect.Right + 2
                            && c.Y >= cellRect.Top - 2 && c.Y <= cellRect.Bottom + 2
                            && r.Width <= 30;                       // ปุ่ม ... เล็ก ไม่ใช่ทั้งช่อง
                    })
                    .OrderByDescending(b => b.BoundingRectangle.Left)
                    .FirstOrDefault();
            }
            catch { return null; }
        }

        /* Method กดปุ่มในหน้าต่างบันทึก (Save / Save and New / Save and Close / Close) */
        private void ClickFormButton(string name, bool dismissInfo = true)
        {
            CancelLookupIfOpen();
            var form = RequireEditForm();
            var btn = Retry(() => FindButtonIn(form, name), timeoutMs: 5000)
                ?? throw new Exception($"หาปุ่ม '{name}' ในหน้าต่างบันทึกไม่เจอ");

            WaitUntilEnabled(btn, timeoutMs: 3000);
            SafeClickElement(btn);
            WaitForAppIdle();

            // หลังบันทึก ถ้ามีกล่องแจ้งผลแบบมีแค่ปุ่ม OK หรือแจ้งเตือน ให้ FAIL พร้อมข้อความ
            if (dismissInfo && name != BtnClose)
            {
                var dialog = WaitMessageBox(1500);
                if (dialog != null)
                {
                    var text = ReadDialogText(dialog);
                    bool isPlainSuccess = FindDialogButton(dialog, "Yes", "No", "Cancel") == null && IsSuccessText(text);
                    PressOkOrClose(dialog);
                    if (!isPlainSuccess) throw new Exception($"กด {name} แล้วมีแจ้งเตือน: '{text}'");
                }
            }
        }

        private static bool IsSuccessText(string text) =>
            text.Contains("เรียบร้อย") || text.Contains("สำเร็จ") || text.Contains("Success", StringComparison.OrdinalIgnoreCase);

        private void ExpectFormClosed(int timeoutMs = 8000)
        {
            if (WaitUntil(() => FindOpenForm() == null, timeoutMs))
            {
                WaitUntil(() => FindToolbarButton(BtnNew) != null, 3000);   // กลับมาหน้ารายการแล้ว
                return;
            }

            var dialog = WaitMessageBox(0);
            var extra = dialog != null ? $" (มีกล่องข้อความค้างอยู่: '{ReadDialogText(dialog)}')" : "";
            throw new Exception("ปิดฟอร์มไม่สำเร็จ" + extra);
        }

        //----
        // * Helpers: หน้าต่างเลือกข้อมูล (Lookup) *
        //----

        private static AutomationElement GetLookupGrid(AutomationElement lookup)
        {
            try
            {
                return lookup.FindAllDescendants(cf => cf.ByControlType(ControlType.Table).Or(cf.ByControlType(ControlType.DataGrid)))
                    .Where(HasArea)
                    .OrderByDescending(g => g.BoundingRectangle.Width * g.BoundingRectangle.Height)
                    .FirstOrDefault() ?? lookup;
            }
            catch { return lookup; }
        }

        /* Method ติ๊ก ☑ แถวที่มีรหัส <code> (ถ้าติ๊กไว้แล้วจะไม่คลิกซ้ำ เพราะคลิกซ้ำ = เอาติ๊กออก) */
        private void TickLookupCode(string code)
        {
            var lookup = GetLookupWindow() ?? throw new Exception("หน้าต่างเลือกข้อมูลไม่ได้เปิดอยู่");
            var grid = GetLookupGrid(lookup);

            var (snap, cell) = ScrollFind(grid, s => s.Cells.FirstOrDefault(c => c.Is(code)));
            if (cell == null)
                throw new Exception($"หารหัส '{code}' ในหน้าต่างเลือกข้อมูลไม่เจอ — พนักงานคนนี้อาจมีผู้อนุมัติคนอื่นในหน้าจอนี้แล้ว (ข้อความในตารางที่เห็นล่าสุด: {DescribeCells(snap)})");

            if (IsLookupCodeChecked(grid, code) == true) return;   // ติ๊กไว้แล้ว ไม่คลิกซ้ำ (คลิกซ้ำ = เอาติ๊กออก)

            // คลิกช่องติ๊กครั้งเดียว แล้วรอให้ติ๊กติด (แถวจะย้ายไปกลุ่ม "เลือก: Checked")
            ClickCheckOf(grid, code);
            if (!WaitUntil(() => IsLookupCodeChecked(grid, code) == true, 3000))
                throw new Exception($"คลิกติ๊ก '{code}' แล้ว แต่ยังไม่ถูกเลือก");
        }

        /* Method คลิกช่องติ๊ก (คอลัมน์ เลือก) ของแถวที่มีรหัส <code> — หาตำแหน่งใหม่ทุกครั้ง เพราะแถวย้ายกลุ่มได้ */
        private void ClickCheckOf(AutomationElement grid, string code)
        {
            var (snap, cell) = ScrollFind(grid, s => s.Cells.FirstOrDefault(c => c.Is(code)), maxMs: 10000);
            if (cell == null) throw new Exception($"หารหัส '{code}' ไม่เจอ");

            var check = FindCheckCell(snap, cell);
            int x = check?.CX ?? SelectColumnX(snap) ?? throw new Exception($"หาช่องติ๊กของ '{code}' ไม่เจอ");
            ClickAt(new Point(x, cell.CY));
        }

        /// <summary>
        /// สถานะติ๊กของรหัส &lt;code&gt;: อ่านจากช่องติ๊กก่อน ถ้าอ่านไม่ได้ ดูจากหัวกลุ่มที่อยู่เหนือแถว
        /// (ตารางนี้จัดกลุ่มตามคอลัมน์ เลือก → "เลือก: Checked" / "เลือก: Unchecked") — คืน null ถ้าบอกไม่ได้
        /// </summary>
        private bool? IsLookupCodeChecked(AutomationElement grid, string code)
        {
            var snap = TakeSnapshot(grid, keepWide: true);
            var cell = snap.Cells.FirstOrDefault(c => c.Is(code));
            if (cell == null)
            {
                var (s2, c2) = ScrollFind(grid, s => s.Cells.FirstOrDefault(c => c.Is(code)), maxMs: 8000, keepWide: true);
                snap = s2; cell = c2;
                if (cell == null) return null;
            }

            var byCell = FindCheckCell(snap, cell)?.Checked;
            if (byCell != null) return byCell;

            var group = snap.Cells
                .Where(c => c.CY < cell.CY)
                .Select(c => new { c.CY, Text = (c.Value + " " + c.Name) })
                .Where(g => g.Text.Contains("Checked", StringComparison.OrdinalIgnoreCase)
                            && (g.Text.Contains(":") || g.Text.Contains("เลือก")))   // เฉพาะหัวกลุ่ม ไม่ใช่ช่องติ๊กของแถวอื่น
                .OrderByDescending(g => g.CY)
                .FirstOrDefault();
            if (group == null) return null;
            return !group.Text.Contains("Unchecked", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// ยืนยันการเลือกในหน้าต่างเลือกข้อมูล (กดปุ่ม Select) แล้วรอจนหน้าต่างปิด
        /// </summary>
        private void ClickLookupSelect()
        {
            var lookup = GetLookupWindow() ?? throw new Exception("หน้าต่างเลือกข้อมูลไม่ได้เปิดอยู่");
            var btn = FindButtonIn(lookup, LookupSelectName)
                ?? throw new Exception($"หาปุ่ม '{LookupSelectName}' ในหน้าต่างเลือกข้อมูลไม่เจอ");

            // กด Select ครั้งเดียว แล้วรอหน้าต่างปิด
            ForceActivateWindow(lookup);
            ClickElementByPhysicalMouse(btn);

            if (!WaitForLookupClosed(5000))
            {
                string? err = TryDismissAppErrorDialog();
                if (err != null) throw new Exception($"กด {LookupSelectName} แล้วแอปแสดง error: {err}");
                throw new Exception($"กด {LookupSelectName} แล้ว หน้าต่างเลือกข้อมูลยังเปิดอยู่");
            }

            _activeLookup = null;
            WaitForAppIdle();
        }

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extraInfo);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr handle);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        /* Method ให้โปรแกรม HRMI อยู่หน้าสุดก่อนคลิก/กดคีย์ทุกครั้ง
           (กันคลิก/พิมพ์ไปโดนหน้าต่างอื่น เช่น Visual Studio ที่บังอยู่) */
        private void EnsureAppForeground()
        {
            try
            {
                if (IsAppForeground()) return;

                // หน้าต่างที่กำลังทดสอบอยู่: หน้าต่างเลือกข้อมูล > กล่องข้อความ > หน้าต่างบันทึก > หน้าต่างหลัก
                var target = GetLookupWindow() ?? FindMessageBox() ?? FindOpenForm() ?? _mainWindow;
                ForceActivateWindow(target);
                WaitUntil(IsAppForeground, 1000);
            }
            catch { }
        }

        private const uint MouseLeftDown = 0x0002;
        private const uint MouseLeftUp = 0x0004;

        private void ForceActivateWindow(AutomationElement window)
        {
            try
            {
                var handle = (IntPtr)window.Properties.NativeWindowHandle.ValueOrDefault;
                if (handle != IntPtr.Zero)
                    SetForegroundWindow(handle);
                else
                    _mainWindow.AsWindow().SetForeground();
            }
            catch
            {
                try { _mainWindow.AsWindow().SetForeground(); } catch { }
            }
        }

        private void ClickElementByPhysicalMouse(AutomationElement element)
        {
            var bounds = element.BoundingRectangle;
            if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
                throw new Exception($"ปุ่ม '{SafeName(element)}' ไม่มีตำแหน่งคลิกที่ใช้ได้");

            int x = bounds.Left + bounds.Width / 2;
            int y = bounds.Top + bounds.Height / 2;
            if (element.TryGetClickablePoint(out Point point)
                && point.X >= bounds.Left && point.X <= bounds.Right && point.Y >= bounds.Top && point.Y <= bounds.Bottom)
            {
                x = point.X;
                y = point.Y;
            }

            if (!SetCursorPos(x, y))
                throw new Exception($"ย้ายเมาส์ไปที่ปุ่ม '{SafeName(element)}' ไม่สำเร็จ ({x}, {y})");

            WaitCursorAt(new Point(x, y));
            mouse_event(MouseLeftDown, 0, 0, 0, UIntPtr.Zero);
            WaitForAppIdle();   // รอโปรแกรมรับ mouse down ก่อนปล่อย
            mouse_event(MouseLeftUp, 0, 0, 0, UIntPtr.Zero);
            WaitForAppIdle();
        }

        private bool WaitForLookupClosed(int timeoutMs) => WaitUntil(() => GetLookupWindow() == null, timeoutMs);

        /* Method ถ้าหน้าต่างเลือกข้อมูลยังเปิดค้าง ให้กด Cancel (หรือ Close / Esc) ปิดก่อน */
        private void CancelLookupIfOpen()
        {
            try
            {
                var lookup = GetLookupWindow();
                if (lookup == null) return;

                // หาปุ่ม Cancel ก่อนเสมอ — เดิมหา Cancel/Close พร้อมกันแล้วเจอปุ่มกากบาท (ชื่อ Close) บนแถบชื่อก่อน
                var cancel = FindButtonIn(lookup, LookupCancelName, "ยกเลิก")
                             ?? FindButtonIn(lookup, "Close", "ปิด");
                if (cancel != null) ClickCenter(cancel);
                else PressKey(VirtualKeyShort.ESCAPE);
                WaitForLookupClosed(3000);
            }
            catch { }
            finally
            {
                if (_activeLookup != null && !IsVisible(_activeLookup)) _activeLookup = null;
            }
        }

        //----
        // * Helpers: อ่านตารางแบบ snapshot *
        // ตาราง DevExpress มีหลายร้อยช่อง ถ้าถาม UI Automation ทีละ property ทีละช่อง จะช้ามากจนดูเหมือนค้าง
        // จึงอ่านทุกช่องพร้อมกันครั้งเดียวด้วย CacheRequest แล้วค่อยหาในข้อมูลที่อ่านมา
        //----

        private sealed class GridCell
        {
            public string Name = "";
            public string Value = "";
            public Rectangle Rect;
            public bool? Checked;
            public int CX => Rect.Left + Rect.Width / 2;
            public int CY => Rect.Top + Rect.Height / 2;

            public bool Is(string text) =>
                string.Equals(Name.Trim(), text, StringComparison.OrdinalIgnoreCase)
                || string.Equals(Value.Trim(), text, StringComparison.OrdinalIgnoreCase);
        }

        private sealed class GridSnapshot
        {
            public Rectangle GridRect;
            public List<GridCell> Headers = new List<GridCell>();
            public List<GridCell> Cells = new List<GridCell>();   // ช่องข้อมูลที่อยู่ในกรอบตาราง ใต้หัวคอลัมน์
        }

        private GridSnapshot TakeSnapshot(AutomationElement grid, bool keepWide = false)
        {
            try { return ReadSnapshot(grid, useCache: true, keepWide); }
            catch { return ReadSnapshot(grid, useCache: false, keepWide); }   // ถ้า cache ใช้ไม่ได้ อ่านแบบปกติ (ช้ากว่า)
        }

        private GridSnapshot ReadSnapshot(AutomationElement grid, bool useCache, bool keepWide)
        {
            var lib = _automation.PropertyLibrary;
            var snap = new GridSnapshot();
            try { snap.GridRect = grid.BoundingRectangle; } catch { }

            IDisposable? cacheScope = null;
            if (useCache)
            {
                var cr = new CacheRequest { TreeScope = TreeScope.Element };
                cr.Add(lib.Element.Name);
                cr.Add(lib.Element.BoundingRectangle);
                cr.Add(lib.Element.ControlType);
                cr.Add(lib.Value.Value);
                cr.Add(lib.Toggle.ToggleState);
                cr.Add(lib.LegacyIAccessible.Value);
                cr.Add(lib.LegacyIAccessible.State);
                cacheScope = cr.Activate();
            }

            var all = new List<GridCell>();
            try
            {
                foreach (var e in grid.FindAllDescendants())
                {
                    var fe = e.FrameworkAutomationElement;
                    if (!fe.TryGetPropertyValue<Rectangle>(lib.Element.BoundingRectangle, out var rect)
                        || rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0)
                        continue;

                    var cell = new GridCell { Rect = rect };
                    if (fe.TryGetPropertyValue<string>(lib.Element.Name, out var name)) cell.Name = name ?? "";
                    if (fe.TryGetPropertyValue<string>(lib.Value.Value, out var value)) cell.Value = value ?? "";
                    cell.Checked = ReadCheckedCached(e, cell.Value);

                    bool isHeader = fe.TryGetPropertyValue<ControlType>(lib.Element.ControlType, out var type)
                                    && (type == ControlType.Header || type == ControlType.HeaderItem);
                    if (isHeader) snap.Headers.Add(cell);
                    else all.Add(cell);
                }
            }
            finally
            {
                cacheScope?.Dispose();
            }

            // เอาเฉพาะช่องที่อยู่ในกรอบตารางจริง ใต้หัวคอลัมน์ และไม่ใช่แถว/กลุ่มที่กว้างเต็มตาราง
            // (keepWide = true สำหรับรายการแบบ tree ที่แต่ละแถวกว้างเต็มรายการ)
            var area = snap.GridRect;
            int headerBottom = snap.Headers.Where(h => area.Contains(new Point(h.CX, h.CY)))
                                           .Select(h => h.Rect.Bottom).DefaultIfEmpty(area.Top).Max();
            snap.Cells = all.Where(c =>
                    area.Contains(new Point(c.CX, c.CY))
                    && c.CY > headerBottom
                    && (keepWide || c.Rect.Width < area.Width * 0.9))
                .ToList();
            return snap;
        }

        /* Method อ่านสถานะติ๊กจากข้อมูลที่อ่านมาแล้ว — true/false หรือ null ถ้าไม่ใช่ช่องติ๊ก */
        private bool? ReadCheckedCached(AutomationElement e, string value)
        {
            var fe = e.FrameworkAutomationElement;
            var lib = _automation.PropertyLibrary;
            try
            {
                if (fe.TryGetPropertyValue<ToggleState>(lib.Toggle.ToggleState, out var ts))
                    return ts == ToggleState.On;
            }
            catch { }

            var texts = new List<string> { value };
            try
            {
                if (fe.TryGetPropertyValue<string>(lib.LegacyIAccessible.Value, out var lv)) texts.Add(lv ?? "");
            }
            catch { }

            foreach (var t in texts.Select(t => (t ?? "").Trim()))
            {
                if (t.Equals("Checked", StringComparison.OrdinalIgnoreCase) || t.Equals("True", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (t.Equals("Unchecked", StringComparison.OrdinalIgnoreCase) || t.Equals("False", StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return null;
        }

        private static bool SameRow(GridCell a, GridCell b) =>
            Math.Abs(a.CY - b.CY) <= Math.Max(3, Math.Min(a.Rect.Height, b.Rect.Height) / 2);

        /* Method ตำแหน่ง X ของคอลัมน์ เลือก (จากหัวคอลัมน์) */
        private static int? SelectColumnX(GridSnapshot s) => s.Headers.FirstOrDefault(h => h.Is("เลือก"))?.CX;

        /* Method ช่อง ☑ ของแถวนั้น: ช่องในแถวเดียวกันที่อ่านสถานะติ๊กได้ หรือช่องใต้หัวคอลัมน์ เลือก */
        private static GridCell? FindCheckCell(GridSnapshot s, GridCell rowCell)
        {
            var sameRow = s.Cells.Where(c => SameRow(c, rowCell)).ToList();

            var byState = sameRow.FirstOrDefault(c => c.Checked != null);
            if (byState != null) return byState;

            var x = SelectColumnX(s);
            if (x == null) return null;
            return sameRow
                .Where(c => c.Rect.Left <= x && x <= c.Rect.Right)
                .OrderBy(c => c.Rect.Width * c.Rect.Height)
                .FirstOrDefault();
        }

        /// <summary>
        /// หาในตาราง ถ้าไม่เจอ: เลื่อนขึ้นบนสุด แล้วค่อยๆ เลื่อนลงทีละนิด
        /// หยุดเมื่อเจอ, เมื่อเลื่อนต่อไม่ได้แล้ว (ถึงท้ายตาราง) หรือเมื่อเกินเวลา — ไม่วนค้าง
        /// </summary>
        private (GridSnapshot Last, T? Found) ScrollFind<T>(AutomationElement grid, Func<GridSnapshot, T?> match,
                                                            int maxMs = 15000, bool keepWide = false)
            where T : class
        {
            var sw = Stopwatch.StartNew();
            var snap = TakeSnapshot(grid, keepWide);
            var found = match(snap);
            if (found != null) return (snap, found);

            var center = GetCenter(snap.GridRect);
            Mouse.MoveTo(center);
            Mouse.Scroll(50);   // ขึ้นบนสุด
            WaitForAppIdle();

            while (sw.ElapsedMilliseconds < maxMs)
            {
                snap = TakeSnapshot(grid, keepWide);
                found = match(snap);
                if (found != null) return (snap, found);

                var signature = CellsSignature(snap);
                Mouse.MoveTo(center);
                Mouse.Scroll(-3);

                // รอจนข้อมูลในตารางเปลี่ยนจริง — ไม่เปลี่ยน = เลื่อนต่อไม่ได้แล้ว (ถึงท้ายตาราง)
                if (!WaitUntil(() => CellsSignature(TakeSnapshot(grid, keepWide)) != signature, 1500)) break;
            }
            return (snap, null);
        }

        /* [ใหม่] คลิกแถวแรก / แถวสุดท้ายของรหัส <code> ในตารางรายการ แล้วคืนชื่อเมนูของแถวนั้น
           แถวสุดท้าย: เลื่อนตารางลงล่างสุดก่อน แล้วเอาแถวล่างสุดที่เป็นรหัสนี้ (ไม่เห็นที่ล่างสุด → ใช้แถวแรกที่เจอแทน) */
        private string SelectCodeRow(string code, bool pickLast)
        {
            var grid = FindListGrid();
            GridSnapshot snap = TakeSnapshot(grid);
            GridCell? cell = null;

            if (pickLast)
            {
                snap = ScrollToBottom(grid);
                cell = snap.Cells.Where(c => c.Is(code)).OrderByDescending(c => c.CY).FirstOrDefault();
            }
            if (cell == null)
                (snap, cell) = ScrollFind(grid, s => s.Cells.Where(c => c.Is(code)).OrderBy(c => c.CY).FirstOrDefault(), maxMs: 20000);
            if (cell == null)
                throw new Exception($"หาแถว '{code}' ในรายการไม่เจอ (ข้อความในตารางที่เห็นล่าสุด: {DescribeCells(snap)})");

            string menu = RowMenuName(snap, cell)
                ?? throw new Exception($"อ่านชื่อเมนูของแถว '{code}' ไม่ได้ จึงตรวจผลการลบไม่ได้ (ข้อความในตารางที่เห็นล่าสุด: {DescribeCells(snap)})");

            ClickAt(new Point(cell.CX, cell.CY));
            WaitRowSelected();
            return menu;
        }

        /* [ใหม่] ชื่อเมนูของแถวนั้น: ช่องในแถวเดียวกันใต้หัวคอลัมน์ "ชื่อเมนู"
           ไม่เจอหัวคอลัมน์ → ใช้ช่องขวาสุดของแถวที่มีข้อความ (คอลัมน์ ชื่อเมนู อยู่ขวาสุดของตาราง) */
        private static string? RowMenuName(GridSnapshot s, GridCell rowCell)
        {
            static string Text(GridCell c) => c.Value.Trim().Length > 0 ? c.Value.Trim() : c.Name.Trim();
            var sameRow = s.Cells.Where(c => SameRow(c, rowCell) && Text(c).Length > 0 && !Text(c).Contains(" row ")).ToList();

            var header = s.Headers.FirstOrDefault(h => Normalize(h.Name + h.Value).Contains("ชื่อเมนู"));
            var cell = header != null
                ? sameRow.FirstOrDefault(c => c.Rect.Left <= header.CX && header.CX <= c.Rect.Right)
                : sameRow.OrderByDescending(c => c.Rect.Left).FirstOrDefault();
            return cell == null ? null : Text(cell);
        }

        /* [ใหม่] เลื่อนตารางลงล่างสุด (หยุดเมื่อเลื่อนแล้วข้อมูลไม่เปลี่ยน) */
        private GridSnapshot ScrollToBottom(AutomationElement grid, int maxMs = 20000)
        {
            EnsureAppForeground();
            var sw = Stopwatch.StartNew();
            var snap = TakeSnapshot(grid);
            var center = GetCenter(snap.GridRect);

            while (sw.ElapsedMilliseconds < maxMs)
            {
                var signature = CellsSignature(snap);
                Mouse.MoveTo(center);
                Mouse.Scroll(-10);
                if (!WaitUntil(() => CellsSignature(TakeSnapshot(grid)) != signature, 1500)) break;
                snap = TakeSnapshot(grid);
            }
            return TakeSnapshot(grid);
        }

        /* ลายเซ็นของข้อมูลที่เห็นในตาราง (ใช้เช็คว่าเลื่อนตารางแล้วข้อมูลเปลี่ยนหรือยัง) */
        private static string CellsSignature(GridSnapshot snap) =>
            string.Join("|", snap.Cells.Select(c => $"{c.CY}:{c.Value}"));

        /* Method ข้อความในตารางที่เห็นล่าสุด (ใช้ใส่ใน log ตอนหาไม่เจอ) */
        private static string DescribeCells(GridSnapshot snap)
        {
            var texts = snap.Cells.Select(c => c.Value.Trim().Length > 0 ? c.Value.Trim() : c.Name.Trim())
                .Where(t => t.Length > 0 && t != "Checked" && t != "Unchecked")
                .Distinct()
                .Take(30)
                .ToList();
            return texts.Count == 0 ? "(อ่านข้อความในตารางไม่ได้)" : string.Join(" | ", texts);
        }

        //----
        // * Helpers: กล่องข้อความ (แจ้งเตือน / ยืนยัน) *
        //----

        /// <summary>
        /// หากล่องข้อความ: หน้าต่างของโปรแกรมที่ไม่ใช่หน้าต่างบันทึก / หน้าต่างเลือกข้อมูล และมีปุ่ม OK / Yes / No / Cancel
        /// ข้ามหน้าต่างที่มีตารางข้อมูล (หน้าต่างเลือกข้อมูลแบบที่ไม่มีปุ่ม Select)
        /// กัน error-dialog cleanup ไปกด OK ปิดหน้าต่างเลือกข้อมูลทิ้งระหว่างรอมันเปิด
        /// </summary>
        private AutomationElement? FindMessageBox()
        {
            foreach (var w in GetDialogCandidates())
            {
                if (FindButtonIn(w, BtnSaveAndNew) != null) continue;   // หน้าต่างบันทึก
                if (FindButtonIn(w, LookupSelectName) != null) continue; // หน้าต่างเลือกข้อมูล
                if (HasDataGrid(w)) continue;                            // หน้าต่างเลือกข้อมูล / หน้ารายการ
                if (FindButtonIn(w, DialogButtonNames) != null) return w;

                if (!IsSmallDialog(w)) continue;

                // กล่องข้อความแบบ DevExpress (XtraMessageBox) ปุ่มมักไม่ได้เป็น ControlType.Button
                // (เป็น Pane/Custom) จึงหาแบบเดิมไม่เจอ → ค้นตามชื่ออย่างเดียว เฉพาะหน้าต่างเล็ก
                if (FindDialogButton(w, DialogButtonNames) != null) return w;

                // แจ้งเตือน "กรุณาเลือกหน้าจอและพนักงานที่ต้องการกำหนด" มีแค่ปุ่ม Close (TestStep: ไปกดที่ Close)
                // ทุกหน้าต่างมีปุ่ม Close บนแถบชื่อ จึงรับเฉพาะหน้าต่างเล็ก (กล่องข้อความ)
                if (FindDialogButton(w, "Close", "ปิด") != null) return w;
            }
            return null;
        }

        /* หน้าต่างที่อาจเป็นกล่องข้อความ: หน้าต่างของโปรแกรม + element ระดับบนสุดทุกแบบของ process
           (กล่องข้อความ/Alert ของ DevExpress บางแบบเป็น Pane ระดับบนสุด ไม่ใช่ Window) */
        private List<AutomationElement> GetDialogCandidates()
        {
            var seen = new HashSet<string>();
            var result = new List<AutomationElement>();
            foreach (var w in GetAppWindows().Concat(GetProcessTopLevels()))
            {
                try
                {
                    if (w.Equals(_mainWindow) || !IsVisible(w)) continue;
                    if (seen.Add(ElementKey(w))) result.Add(w);
                }
                catch { }
            }
            return result;
        }

        /* หาปุ่มในกล่องข้อความ: ลองแบบ ControlType.Button ก่อน ถ้าไม่เจอ ค้นตามชื่อทุก ControlType
           (ยกเว้น Text/Window/TitleBar — กันไปคลิกโดนข้อความหรือตัวหน้าต่าง) */
        private static AutomationElement? FindDialogButton(AutomationElement root, params string[] names)
        {
            var strict = FindButtonIn(root, names);
            if (strict != null) return strict;

            var wanted = new HashSet<string>(names.SelectMany(n => new[] { n, "&" + n }), StringComparer.OrdinalIgnoreCase);
            try
            {
                return root.FindAllDescendants()
                    .Where(e =>
                    {
                        var t = SafeControlTypeValue(e);
                        return t != ControlType.Text && t != ControlType.Window && t != ControlType.TitleBar;
                    })
                    .Where(e => wanted.Contains(SafeName(e).Trim()))
                    .FirstOrDefault(IsVisible);
            }
            catch { return null; }
        }

        /* ในหน้าต่างบันทึกมีข้อความ <expected> แสดงอยู่หรือไม่ (กรณีแจ้งเตือนแสดงในแผงข้อความของฟอร์ม
           แทนการเด้งกล่องข้อความ — เช่นแผง "0 Errors / 1 Warnings / 0 Informations") */
        private static bool FormContainsText(AutomationElement form, string expected)
        {
            var target = Normalize(expected);
            try
            {
                foreach (var e in form.FindAllDescendants())
                {
                    try { if (Normalize(e.Name).Contains(target)) return true; } catch { }
                    try { if (Normalize(e.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault).Contains(target)) return true; } catch { }
                    try { if (Normalize(e.Patterns.LegacyIAccessible.PatternOrDefault?.Value.ValueOrDefault).Contains(target)) return true; } catch { }
                }
            }
            catch { }
            return false;
        }

        /* รายละเอียด element ระดับบนสุดของ process (ใส่ใน log ตอนหากล่องข้อความไม่เจอ
           เพื่อดูว่ากล่องข้อความจริงเป็น ControlType อะไร และปุ่มชื่ออะไร) */
        private string DescribeTopLevels()
        {
            try
            {
                var list = GetProcessTopLevels().Where(w => !w.Equals(_mainWindow)).Take(8).Select(w =>
                {
                    var r = w.BoundingRectangle;
                    string kids;
                    try
                    {
                        kids = string.Join("/", w.FindAllDescendants()
                            .Where(HasArea)
                            .Select(e => $"{SafeControlType(e)}:'{SafeName(e)}'")
                            .Where(s => !s.EndsWith(":''"))
                            .Distinct().Take(12));
                    }
                    catch { kids = "?"; }
                    return $"{SafeControlType(w)} '{SafeName(w)}' {r.Width}x{r.Height} [{kids}]";
                }).ToList();
                return list.Count == 0 ? "(ไม่มี)" : string.Join(" || ", list);
            }
            catch { return "(อ่านไม่ได้)"; }
        }

        /* Method หน้าต่างขนาดเล็กแบบกล่องข้อความ (เล็กกว่าครึ่งหนึ่งของหน้าต่างหลัก) */
        private bool IsSmallDialog(AutomationElement w)
        {
            try
            {
                var r = w.BoundingRectangle;
                var main = _mainWindow.BoundingRectangle;
                return r.Width > 0 && r.Height > 0 && r.Width < main.Width * 0.5 && r.Height < main.Height * 0.5;
            }
            catch { return false; }
        }

        /// <summary>
        /// ถ้ามี error dialog ของแอปค้างอยู่ (เช่น "Picker Error!" ที่มีแค่ปุ่ม OK) ให้กด OK ปิด
        /// แล้วคืนข้อความ "[ชื่อหน้าต่าง] ข้อความ" — ถ้าไม่มีคืน null
        /// ไม่แตะกล่องยืนยันที่มีปุ่ม Yes/No/Cancel เพราะเป็นส่วนของ flow ปกติ
        /// </summary>
        private string? TryDismissAppErrorDialog()
        {
            try
            {
                var box = FindMessageBox();
                if (box == null) return null;

                if (FindDialogButton(box, "Yes", "No", "Cancel", "ใช่", "ไม่ใช่", "ยกเลิก") != null)
                    return null;

                var title = SafeName(box);
                var text = ReadDialogText(box);

                try { PressOkOrClose(box); } catch { }

                return $"[{title}] {text}";
            }
            catch { return null; }
        }

        /* Method รอกล่องข้อความขึ้นภายใน timeoutMs (0 = ดูครั้งเดียว) */
        private AutomationElement? WaitMessageBox(int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            do
            {
                var box = FindMessageBox();
                if (box != null) return box;
            } while (sw.ElapsedMilliseconds < timeoutMs);
            return null;
        }

        private static string ReadDialogText(AutomationElement dialog)
        {
            try
            {
                var texts = dialog.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
                    .Select(t => SafeName(t).Trim())
                    .Where(t => t.Length > 0)
                    .Distinct()
                    .ToList();

                // กล่องข้อความ DevExpress บางแบบไม่มี ControlType.Text → อ่านชื่อ element อื่นๆ แทน (ยกเว้นปุ่ม)
                if (texts.Count == 0)
                {
                    var buttonNames = new HashSet<string>(DialogButtonNames.Concat(new[] { "Close", "ปิด" })
                        .SelectMany(n => new[] { n, "&" + n }), StringComparer.OrdinalIgnoreCase);
                    texts = dialog.FindAllDescendants()
                        .Where(e => SafeControlTypeValue(e) != ControlType.Button)
                        .Select(e => SafeName(e).Trim())
                        .Where(t => t.Length > 0 && !buttonNames.Contains(t))
                        .Distinct()
                        .ToList();
                }
                return string.Join(" ", texts);
            }
            catch { return ""; }
        }

        /* Method กดปุ่มในกล่องข้อความ (Yes / No / Cancel) แล้วรอกล่องปิด */
        private void ClickMessageButton(params string[] names)
        {
            var dialog = WaitMessageBox(MessageWaitMs)
                ?? throw new Exception($"ไม่มีกล่องข้อความให้กด '{names[0]}' (popup ที่เห็น: {DescribeTopLevels()})");
            var btn = FindDialogButton(dialog, names)
                ?? throw new Exception($"หาปุ่ม '{names[0]}' ในกล่องข้อความไม่เจอ (ข้อความ: '{ReadDialogText(dialog)}')");

            SafeClickElement(btn);
            WaitClosed(dialog);
        }

        /// <summary>
        /// ต้องมีแจ้งเตือนที่มีข้อความ &lt;expected&gt;:
        /// - แบบกล่องข้อความเด้ง → ตรวจข้อความ แล้วกด Close (OK) ปิด
        /// - แบบแสดงในแผงข้อความของหน้าต่างบันทึก (ไม่เด้งกล่อง) → เจอข้อความในฟอร์มก็ถือว่าผ่าน
        /// </summary>
        private void ExpectMessageAndClose(string expected)
        {
            AutomationElement? dialog = null;
            bool inline = false;
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < MessageWaitMs + 2000)
            {
                dialog = FindMessageBox();
                if (dialog != null) break;

                var form = FindOpenForm();
                if (form != null && FormContainsText(form, expected)) { inline = true; break; }
            }

            if (inline) return;
            if (dialog == null)
                throw new Exception($"ไม่มีแจ้งเตือน '{expected}' (หน้าต่างที่เห็น: {DescribeAppWindows()} | popup ที่เห็น: {DescribeTopLevels()})");

            var text = ReadDialogText(dialog);
            PressOkOrClose(dialog);

            // อ่านข้อความไม่ได้ จะไม่ตัดสินว่า FAIL
            if (text.Length > 0 && !Normalize(text).Contains(Normalize(expected)))
                throw new Exception($"ข้อความแจ้งเตือนไม่ตรง (คาดหวัง: '{expected}', ที่ขึ้นจริง: '{text}')");
        }

        /* Method ปิดกล่องข้อความ: กด OK / Close ถ้าไม่มี กด Enter แล้วค่อย WindowPattern.Close */
        private void PressOkOrClose(AutomationElement dialog)
        {
            var btn = FindDialogButton(dialog, "OK", "ตกลง", "Close", "ปิด");
            if (btn != null)
            {
                SafeClickElement(btn);
            }
            else
            {
                try { dialog.Focus(); } catch { }
                PressKey(VirtualKeyShort.RETURN);
            }

            if (!WaitClosed(dialog))
            {
                try { dialog.Patterns.Window.PatternOrDefault?.Close(); } catch { }
                if (!WaitClosed(dialog)) throw new Exception("ปิดกล่องข้อความไม่ได้");
            }
        }

        /* Method รอจนหน้าต่างปิด — คืน true ถ้าปิดแล้ว */
        private bool WaitClosed(AutomationElement window, int timeoutMs = 3000)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (!IsVisible(window))   // ปิดแล้ว หรืออ่าน property ไม่ได้
                {
                    WaitForAppIdle();
                    return true;
                }
            }
            return false;
        }

        /* Method รายชื่อหน้าต่างของโปรแกรม + ปุ่มที่เห็น (ใส่ใน log ตอนหาหน้าต่าง/ตารางไม่เจอ) */
        private string DescribeAppWindows()
        {
            try
            {
                var list = GetAppWindows().Take(8).Select(w =>
                {
                    string buttons;
                    try
                    {
                        buttons = string.Join("/", w.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                            .Where(IsVisible).Select(SafeName).Where(n => n.Length > 0).Distinct().Take(8));
                    }
                    catch { buttons = "?"; }
                    return $"'{SafeName(w)}' [{buttons}]{(HasDataGrid(w) ? " +ตาราง" : "")}";
                }).ToList();
                return list.Count == 0 ? "(ไม่มี)" : string.Join(", ", list);
            }
            catch { return "(อ่านไม่ได้)"; }
        }

        //----
        // * Low-level Element Helpers *
        //----

        private static bool HasArea(AutomationElement e)
        {
            try { var r = e.BoundingRectangle; return !r.IsEmpty && r.Width > 0 && r.Height > 0; }
            catch { return false; }
        }

        private static bool IsVisible(AutomationElement e)
        {
            try
            {
                if (e.Properties.IsOffscreen.IsSupported && e.Properties.IsOffscreen.Value) return false;
                return HasArea(e);
            }
            catch { return false; }
        }

        private static string SafeName(AutomationElement element)
        {
            try { return element?.Name ?? ""; } catch { return ""; }
        }

        private static string SafeControlType(AutomationElement element)
        {
            try { return element.Properties.ControlType.ValueOrDefault.ToString(); } catch { return "?"; }
        }

        private void SafeClickElement(AutomationElement element, int waitForRectMs = 3000)
        {
            if (WaitUntil(() => HasArea(element), waitForRectMs))
            {
                ClickAt(GetCenter(element.BoundingRectangle));
                return;
            }

            try
            {
                element.Click();
            }
            catch (FlaUI.Core.Exceptions.NoClickablePointException)
            {
                try
                {
                    var invokePattern = element.Patterns.Invoke.PatternOrDefault;
                    if (invokePattern != null)
                    {
                        invokePattern.Invoke();
                        return;
                    }
                }
                catch { }
                throw new Exception($"ไม่สามารถคลิก '{SafeName(element)}' ได้ (หาตำแหน่งที่คลิกได้บนหน้าจอไม่เจอ)");
            }
        }

        private static bool WaitUntilEnabled(AutomationElement element, int timeoutMs = 3000) =>
            WaitUntil(() => element.Properties.IsEnabled.IsSupported && element.Properties.IsEnabled.Value, timeoutMs);

        //----
        // * รอเคอร์เซอร์ / หน้าต่างหน้าสุด / แถวที่เลือก *
        //----

        /* รอจนเคอร์เซอร์ไปถึงจุดที่จะคลิกจริง (แทนการหน่วงก่อนคลิก) */
        private static void WaitCursorAt(Point p) =>
            WaitUntil(() =>
            {
                var c = Mouse.Position;
                return Math.Abs(c.X - p.X) <= 2 && Math.Abs(c.Y - p.Y) <= 2;
            }, 500);

        /* โปรแกรม HRMI อยู่หน้าสุดแล้วหรือยัง */
        private bool IsAppForeground()
        {
            try
            {
                int pid = _mainWindow.Properties.ProcessId.ValueOrDefault;
                GetWindowThreadProcessId(GetForegroundWindow(), out uint fgPid);
                return fgPid == (uint)pid;
            }
            catch { return false; }
        }

        /* รอจนแถวในตารางรายการถูกเลือก (ปุ่ม Edit กดได้) */
        private void WaitRowSelected() =>
            WaitUntil(() => FindToolbarButton(BtnEdit)?.Properties.IsEnabled.ValueOrDefault == true, 2000);
    }
}