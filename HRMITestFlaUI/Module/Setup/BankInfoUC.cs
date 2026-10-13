using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using HRMITestFlaUI.Base;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace HRMITestFlaUI.Pages.Setup
{
    public class BankInfoUC
    {
        // ── ตัวแปรสำหรับเก็บออบเจ็กต์หน้าต่างหลัก, ระบบ Automation และตัวนับผลการทดสอบ (Success/Fail) ──
        private readonly AutomationElement _mainWindow;
        private readonly UIA3Automation _automation;
        private int _totalSuccess = 0, _totalFail = 0;

        // ── ค่าคงที่สำหรับการตั้งค่า Timeout และ Automation ID ของฟอร์ม ──
        private const int TimeoutMs = 6000;
        private const string FormId = "BankInfoUC";
        private const string GeneralTabId = "devTabPgGeneral";

        /* Constructor สำหรับรับค่า MainWindow และ Automation Instance เข้ามาใช้งานภายในคลาส */
        public BankInfoUC(AutomationElement mainWindow, UIA3Automation automation)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

        // -------------------------------------------------------------
        // 1. ทดสอบการเพิ่มข้อมูลธนาคารใหม่ (Add / New Bank Data Tests)
        //    สัดส่วน Progress ของเมธอดนี้: 0 -> 20
        // -------------------------------------------------------------
        /* Method สำหรับนำทางไปยังหน้าจอ Bank Info ผ่านการค้นหาเมนู และวนลูปเพิ่มข้อมูลธนาคารตัวอย่างทั้ง 5 รายการ */
        public void NavigateToBankInfoUC()
        {
            // กำหนดชุดข้อมูลธนาคารตัวอย่างสำหรับทดสอบเพิ่มข้อมูล
            var bankList = new[]
            {
                new { Code = "BANK001", NameTh = "ธนาคารกรุงศรีอยุธยา", NameEn = "Bank of Ayudhya", Short = "BAY" },
                new { Code = "BANK002", NameTh = "ธนาคารกรุงเทพ", NameEn = "Bangkok Bank", Short = "BBL" },
                new { Code = "BANK003", NameTh = "ธนาคารมิซูโฮะ", NameEn = "MIZUHO CORPORATE BANK", Short = "LTD" },
                new { Code = "BANK004", NameTh = "ธนาคารกรุงไทย", NameEn = "Krung Thai Bank", Short = "KTB" },
                new { Code = "BANK005", NameTh = "ธนาคารซูมิโตโม", NameEn = "Sumitomo Mitsui Banking Corporation ", Short = "SMBC" }
            };

            const string stageName = "TestCase00_AddBankData";
            TestLogger.LogInfo("BankSetupInfoPage", "เริ่มทดสอบหน้าจอ Bank Setup Info");
            // ช่วง 0-20: เปิดเมนู = 2, แล้วไล่ตามจำนวนธนาคารที่เพิ่มสำเร็จ (4,8,12,16,20)
            TestProgressHelper.Report(2, "Opening Setup -> Bank Info Menu via Search...");

            // ค้นหาและเปิดหน้าจอผ่านช่องค้นหาด้านบน ถ้าเปิดไม่สำเร็จให้จบการทำงานทันที
            if (!TryOpenBankMenu(stageName)) return;

            Window? form = null;
            for (int i = 0; i < bankList.Length; i++)
            {
                var b = bankList[i];
                string testName = $"{stageName}_{b.Code}";
                TestProgressHelper.Report(4 + (i * 4), $"Adding Bank ({i + 1}/5): {b.Code} - {b.NameTh}...");

                try
                {
                    form ??= GetOrOpenForm(); // ดึงฟอร์มที่เปิดอยู่ หรือเปิดฟอร์มใหม่ถ้ายังไม่มี
                    var tab = GetGeneralTab(form); // ดึง Tab หน้าจอ General
                    FillBankFields(tab, b.Code, b.NameTh, b.NameEn, b.Short); // กรอกข้อมูลธนาคาร

                    // กำหนดปุ่มบันทึกตามเงื่อนไข (ตัวแรกกด Save, ตัวสุดท้ายกด Save and Close, ตัวตรงกลางกด Save and New)
                    string saveBtn = i == 0 ? "Save" : (i == bankList.Length - 1 ? "Save and Close" : "Save and New");
                    if (!TrySaveAndHandleDuplicate(form, testName, b.Code, saveBtn))
                    {
                        form = null;
                        continue;
                    }

                    if (saveBtn == "Save and New")
                    {
                        // รอให้ช่องกรอกรหัสธนาคารว่างลง (แสดงว่าเคลียร์ฟอร์มเตรียมรับข้อมูลใหม่แล้ว)
                        WaitUntil(() => string.IsNullOrEmpty(tab.FindFirstDescendant(cf => cf.ByAutomationId("devtxtBankCode"))?.AsTextBox().Text), 3000);
                        LogSuccess(testName, $"'{b.Code}' บันทึกสำเร็จ (Save and New)");
                    }
                    else
                    {
                        if (saveBtn == "Save") form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();
                        WaitForClosed(() => FindOpenForm());
                        form = null;
                        LogGridResult(testName, b.Code); // ตรวจสอบว่ามีข้อมูลแสดงในตารางหลักจริงไหม
                    }
                }
                catch (Exception ex)
                {
                    LogFailure(testName, $"'{b.Code}' เพิ่มไม่สำเร็จ: {ex.Message}");
                    form = null;
                }
            }
        }

        // -------------------------------------------------------------
        // 2. ทดสอบการตรวจสอบความถูกต้องของข้อมูลตอนเพิ่ม (Validation Tests)
        //    สัดส่วน Progress ของเมธอดนี้: 20 -> 40
        // -------------------------------------------------------------
        /* Method สำหรับรันชุดทดสอบตรวจสอบความถูกต้องของข้อมูล (Validation) เช่น รหัสว่าง, ชื่อว่าง, รหัสซ้ำ, ชื่อซ้ำ */
        public void RunAllValidationTests()
        {
            const string stage = "TestCase01-05_Validation";
            TestProgressHelper.Report(22, "Opening Bank Info Form for Validation Tests...");

            Window form;
            try { form = OpenForm(); }
            catch (Exception ex) { LogFailure(stage, $"เปิดฟอร์มไม่สำเร็จ: {ex.Message}"); return; }

            var tab = GetGeneralTab(form);

            RunStep("TestCase01_MissingBankCode",
                () => FillSaveAssert(tab, form, "", "ธนาคารกรุงศรีอยุธยา", "Save", "TestCase01_MissingBankCode", "รหัสธนาคาร ไม่สามารถเป็นค่าว่าง"),
                25, "Validating: Missing Bank Code...");

            RunStep("TestCase02_MissingBankName",
                () => FillSaveAssert(tab, form, "BANK002", "", "Save and New", "TestCase02_MissingBankName", "ชื่อธนาคาร ไม่สามารถเป็นค่าว่าง"),
                28, "Validating: Missing Bank Name...");

            RunStep("TestCase03_MissingBankCodeAndName",
                () => FillSaveAssert(tab, form, "", "", "Save and New", "TestCase03_MissingBankCodeAndName", "รหัสธนาคาร ไม่สามารถเป็นค่าว่าง", "ชื่อธนาคาร ไม่สามารถเป็นค่าว่าง"),
                31, "Validating: Missing Code and Name...");

            RunStep("TestCase04_DuplicateBankCode",
                () => FillSaveAssert(tab, form, "BANK001", "ธนาคารกรุงศรีอยุธยา", "Save", "TestCase04_DuplicateBankCode", "รหัสธนาคาร ห้ามซ้ำ"),
                34, "Validating: Duplicate Bank Code...");

            const string dupName = "TestCase05_DuplicateBankName";
            RunStep(dupName,
                () => FillSaveAssert(tab, form, "BANK006", "ธนาคารเอเชีย", "Save and Close", dupName, "ชื่อธนาคาร ห้ามซ้ำ"),
                37, "Validating: Duplicate Bank Name...");

            RunStep($"{dupName}_CloseSequence",
                () => RunCloseDialogSequence(form, dupName, "ชื่อธนาคาร ห้ามซ้ำ"),
                40, "Validating: Duplicate Name Close Sequence...");
        }

        // -------------------------------------------------------------
        // 3. ทดสอบการแก้ไขข้อมูล (Edit Record Tests)
        //    สัดส่วน Progress ของเมธอดนี้: 40 -> 55
        // -------------------------------------------------------------
        /* Method สำหรับทดสอบการคลิกเลือกแถวข้อมูลเพื่อแก้ไข (Edit) และบันทึกผลการเปลี่ยนแปลง */
        public void RunEditTests()
        {
            TestProgressHelper.Report(42, "Starting Edit Operations...");

            // เก็บชื่อ/ปุ่ม Save ที่ต้องใช้ไว้ในข้อมูลตรง ๆ แทนการคำนวณด้วย ternary ซ้ำ ๆ ในลูป
            var edits = new[]
            {
                new { Name = "TestCase06_EditRow1", OldCode = "BANK001", NewCode = "BK01", NameTh = "ธนาคารนครหลวงไทย", NameEn = "Siam City Bank", Short = "SCIB", SaveBtn = "Save", NeedsManualClose = true },
                new { Name = "TestCase07_EditRow2", OldCode = "BANK002", NewCode = "BK02", NameTh = "ธนาคารธนชาต", NameEn = "Thanachart Bank", Short = "TBANK", SaveBtn = "Save and Close", NeedsManualClose = false }
            };

            for (int i = 0; i < edits.Length; i++)
            {
                var e = edits[i];
                try
                {
                    var form = ClickRowAndOpenEdit(e.OldCode); // คลิกเลือกแถวเก่าและกดปุ่ม Edit
                    var tab = GetGeneralTab(form);
                    FillBankFields(tab, e.NewCode, e.NameTh, e.NameEn, e.Short);

                    if (!TrySaveAndHandleDuplicate(form, e.Name, e.NewCode, e.SaveBtn)) continue;
                    if (e.NeedsManualClose) form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();

                    WaitForClosed(() => FindOpenForm());
                    RefreshGrid();

                    if (VerifyRecordInGrid(e.NewCode, refresh: false))
                        LogSuccess(e.Name, $"แก้ไข '{e.OldCode}' เป็น '{e.NewCode}' สำเร็จ (พบในตารางหลัก)");
                    else
                        LogFailure(e.Name, $"แก้ไข '{e.OldCode}' เป็น '{e.NewCode}' แล้วแต่ไม่พบในตารางหลัก");
                }
                catch (Exception ex) { LogFailure(e.Name, $"แก้ไข '{e.OldCode}' ไม่สำเร็จ: {ex.Message}"); }
                finally
                {
                    TestProgressHelper.Report(42 + ((i + 1) * 13 / edits.Length), $"Editing Bank Record ({i + 1}/{edits.Length})...");
                }
            }
        }

        // -------------------------------------------------------------
        // 4. ทดสอบ Validation ตอนแก้ไขข้อมูล (Edit Validation Tests)
        //    สัดส่วน Progress ของเมธอดนี้: 55 -> 80
        // -------------------------------------------------------------
        /* Method สำหรับทดสอบการ Validation ในโหมดแก้ไขข้อมูล (Edit Mode) */
        public void RunEditValidationTests()
        {
            const string baseName = "TestCase08_EditValidation";
            const string suffix = " กรุณาป้อนข้อมูล";

            TestProgressHelper.Report(58, "Opening Edit Form for Validation Tests...");

            Window form;
            try { form = ClickRowAndOpenEdit("BK01"); }
            catch (Exception ex) { LogFailure(baseName, $"เปิดฟอร์ม Edit ไม่สำเร็จ: {ex.Message}"); return; }

            var tab = GetGeneralTab(form);

            RunStep($"{baseName}_MissingCode",
                () => FillSaveAssert(tab, form, "", "ธนาคารกรุงศรีอยุธยา", "Save", $"{baseName}_MissingCode", "รหัสธนาคาร ไม่สามารถเป็นค่าว่าง" + suffix),
                62, "Validating Edit: Missing Code...");

            RunStep($"{baseName}_MissingName",
                () => FillSaveAssert(tab, form, "BK01", "", "Save and New", $"{baseName}_MissingName", "ชื่อธนาคาร ไม่สามารถเป็นค่าว่าง" + suffix),
                66, "Validating Edit: Missing Name...");

            RunStep($"{baseName}_MissingBoth",
                () => FillSaveAssert(tab, form, "", "", "Save", $"{baseName}_MissingBoth", "รหัสธนาคาร ไม่สามารถเป็นค่าว่าง" + suffix, "ชื่อธนาคาร ไม่สามารถเป็นค่าว่าง" + suffix),
                70, "Validating Edit: Missing Both...");

            RunStep($"{baseName}_DuplicateCode",
                () => FillSaveAssert(tab, form, "BK02", "ธนาคารธนชาต", "Save and New", $"{baseName}_DuplicateCode", "รหัสธนาคาร ห้ามซ้ำ" + suffix),
                74, "Validating Edit: Duplicate Code...");

            const string dupSub = $"{baseName}_DuplicateName";
            RunStep(dupSub,
                () => FillSaveAssert(tab, form, "BK03", "ธนาคารนครหลวงไทย", "Save and Close", dupSub, "ชื่อธนาคาร ห้ามซ้ำ" + suffix),
                78, "Validating Edit: Duplicate Name...");

            RunStep($"{dupSub}_CloseSequence",
                () => RunCloseDialogSequence(form, dupSub, "ชื่อธนาคาร ห้ามซ้ำ" + suffix),
                80, "Validating Edit: Duplicate Name Close Sequence...");

            LogSuccess(baseName, "รัน Validation Edit ครบทุกเคสแล้ว");
        }

        // -------------------------------------------------------------
        // 5. ทดสอบการลบข้อมูล (Delete Record Tests)
        //    สัดส่วน Progress ของเมธอดนี้: 80 -> 100
        // -------------------------------------------------------------
        /* Method สำหรับทดสอบการลบข้อมูลรายการธนาคาร โดยเจาะจงค้นหาจากรหัสธนาคาร (Code) โดยตรงตามเงื่อนไข Test Scenario */
        public void RunDeleteTests()
        {
            const string baseName = "TestCase09_Delete";

            TestProgressHelper.Report(84, "Starting Delete Tests...");

            // เคสที่ 1: คลิกเจาะจงรหัส "BK02" -> กด Delete -> กด Yes -> ตรวจสอบว่าถูกลบ
            RunDeleteCase(baseName, "BK02", "Yes", expectDeleted: true);
            TestProgressHelper.Report(92, "Delete Test: BK02 (confirmed delete)...");

            // เคสที่ 2: คลิกเจาะจงรหัส "BK01" -> กด Delete -> กด No -> ตรวจสอบว่าข้อมูลยังอยู่เหมือนเดิม
            RunDeleteCase(baseName, "BK01", "No", expectDeleted: false);
            TestProgressHelper.Report(100, "Delete Test: BK01 (cancelled delete)...");

            LogSuccess(baseName, "ทดสอบ Delete ครบทุกเคส");
            TestLogger.LogInfo("BankSetupInfoPage", $"สรุปผลรวมทั้งหมด: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");
        }

        /* Method ย่อยสำหรับจัดการเคสการลบข้อมูลโดยค้นหาจากรหัสธนาคาร (Code) โดยตรงด้วยวิธีค้นหาที่ครอบคลุมตาราง */
        private void RunDeleteCase(string baseName, string code, string btn, bool expectDeleted)
        {
            string testName = $"{baseName}_{code}_{btn}";
            try
            {
                // รีเฟรชตารางก่อนค้นหา เพื่อให้มั่นใจว่าข้อมูลโหลดครบถ้วน
                RefreshGrid();

                var elRow = Retry(() => FindGridElement(code), 4000)
                            ?? throw new Exception($"หาแถวรหัส '{code}' ในตารางไม่เจอ");

                try { elRow.Focus(); } catch { }
                elRow.Click();

                var delBtn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("Delete"))) ?? throw new Exception("หาปุ่ม Delete ไม่เจอ");
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
            catch (Exception ex) when (!string.IsNullOrEmpty(ex.Message)) { LogFailure(testName, $"ลบรหัส '{code}' ไม่สำเร็จ: {ex.Message}"); }
        }

        // -------------------------------------------------------------
        // UI Helpers & Core Navigation
        // -------------------------------------------------------------

        /// <summary>
        /// ค้นหาหน้าจอผ่านช่อง baritemSearch ด้านซ้ายบน กดปุ่ม Find (barChkSearch) และคลิกเลือกเมนูผลลัพธ์
        /// </summary>
        private bool TryOpenBankMenu(string stage)
        {
            try
            {
                // 1. ค้นหาช่องกรอกคำค้นหา (baritemSearch)
                var searchBox = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("baritemSearch"))
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)), 4000)
                              ?? throw new Exception("ไม่พบช่องค้นหา baritemSearch");

                searchBox.Focus();
                Thread.Sleep(200);

                // พิมพ์คำค้นหา "กำหนดรหัสธนาคาร"
                string searchKeyword = "กำหนดรหัสธนาคาร";
                if (searchBox.Patterns.Value.IsSupported)
                    searchBox.Patterns.Value.Pattern.SetValue(searchKeyword);
                else
                    Keyboard.Type(searchKeyword);

                Thread.Sleep(300);

                // 2. คลิกปุ่มค้นหา (barChkSearch)
                var searchBtn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("barChkSearch"))
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("Find")), 4000)
                              ?? throw new Exception("ไม่พบปุ่มค้นหา barChkSearch");

                searchBtn.Click();
                Thread.Sleep(1000); // รอผลลัพธ์แสดงที่แผงเมนูด้านซ้าย

                // 3. คลิกเลือกผลลัพธ์เมนูที่ปรากฏขึ้นมา
                var menuResult = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName(searchKeyword))
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("treeListColumn row 1")), 4000)
                               ?? throw new Exception("ไม่พบผลลัพธ์เมนูจากการค้นหา");

                menuResult.Focus();
                menuResult.Click();

                // กด Enter เพื่อเปิดหน้าจอ
                Keyboard.Press(VirtualKeyShort.ENTER);
                Thread.Sleep(500);

                // 4. รอให้ปุ่ม New ปรากฏขึ้นเพื่อยืนยันว่าเข้าหน้าจอสำเร็จ
                Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("New")), 6000);
                return true;
            }
            catch (Exception ex)
            {
                LogFailure(stage, $"ค้นหาและเปิดหน้าจอไม่สำเร็จ: {ex.Message}");
                return false;
            }
        }

        /* Method สำหรับดึงฟอร์มที่เปิดอยู่ หรือเปิดใหม่ถ้ายังไม่มี */
        private Window GetOrOpenForm() => Retry(FindOpenForm, 1500) ?? OpenForm();

        /* Method สำหรับคลิกปุ่ม New เพื่อเปิดฟอร์มสร้างรายการใหม่ */
        private Window OpenForm()
        {
            _mainWindow.FindFirstDescendant(cf => cf.ByName("New"))?.Click();
            return Retry(FindOpenForm, 8000) ?? throw new Exception("หาฟอร์มไม่เจอ");
        }

        /* Method ค้นหาหน้าต่างฟอร์มที่กำลังเปิดใช้งานอยู่ (Modal หรือ Child Window) */
        private Window? FindOpenForm() =>
            _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault() ??
            _automation.GetDesktop().FindFirstChild(cf => cf.ByAutomationId(FormId))?.AsWindow();

        /* Method ดึง Automation Element ของแท็บ General ภายในฟอร์ม */
        private AutomationElement GetGeneralTab(Window form) =>
            Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(GeneralTabId)), 3000) ?? form;

        /* Method สำหรับกรอกข้อมูลช่องต่างๆ ของธนาคาร (Code, NameTh, NameEn, ShortName) */
        private void FillBankFields(AutomationElement tab, string code, string nameTh, string nameEn, string sName)
        {
            SetText(tab, "devtxtBankCode", code);
            SetText(tab, "devtxtBankName", nameTh);
            SetText(tab, "devtxtBankNameEng", nameEn);
            SetText(tab, "devtxtBankShort", sName);
        }

        /* Method สำหรับใส่ข้อความลงใน TextBox ตาม Automation ID ที่ระบุ */
        private void SetText(AutomationElement tab, string id, string text)
        {
            var txt = Retry(() => tab.FindFirstDescendant(cf => cf.ByAutomationId(id))?.AsTextBox(), 3000) ?? throw new Exception($"ไม่พบช่อง {id}");
            txt.Focus();
            txt.Text = text;
        }

        /* Method ค้นหา Element ในตารางหลักที่มีค่าตรงกับ val (รองรับทั้ง Name และ ValuePattern) - ใช้ร่วมกันทั้ง VerifyRecordInGrid และ RunDeleteCase */
        private AutomationElement? FindGridElement(string val) =>
            _mainWindow.FindAllDescendants().FirstOrDefault(el =>
                (!string.IsNullOrEmpty(el.Name) && string.Equals(el.Name.Trim(), val, StringComparison.OrdinalIgnoreCase)) ||
                (el.Patterns.Value.IsSupported && string.Equals(el.Patterns.Value.Pattern.Value.Value?.Trim(), val, StringComparison.OrdinalIgnoreCase)));

        /* Method ตรวจสอบและจัดการกรณีข้อมูลซ้ำหรือบันทึกข้อมูล */
        private bool TrySaveAndHandleDuplicate(Window form, string testName, string code, string saveBtn)
        {
            var btn = form.FindFirstDescendant(cf => cf.ByName(saveBtn)) ?? throw new Exception($"ไม่พบปุ่ม {saveBtn}");
            btn.Click();

            if (testName.Contains("Validation") || testName.Contains("DuplicateBank")) return true;

            var panel = FindTaskResultPanel(form, 1500);
            if (panel == null) return true;

            string? dup = DetectDuplicateMessage(GetPanelTexts(panel));
            if (dup == null) return true;

            LogFailure(testName, $"'{code}' ข้อมูลซ้ำ: {dup}");
            try { ClickCloseAndHandleDialog(form, "No", $"{testName}_Close"); } catch { }
            return false;
        }

        /* Method ตรวจสอบข้อความในผลลัพธ์ว่าเป็นกรณี "รหัสซ้ำ" หรือ "ชื่อซ้ำ" หรือไม่ (แยกจาก TrySaveAndHandleDuplicate เพื่อลดความซับซ้อนของ ternary ซ้อนกัน) */
        private static string? DetectDuplicateMessage(IEnumerable<string> texts)
        {
            if (texts.Any(t => t.Contains("รหัสธนาคาร ห้ามซ้ำ"))) return "รหัสธนาคารซ้ำ";
            if (texts.Any(t => t.Contains("ชื่อธนาคาร ห้ามซ้ำ"))) return "ชื่อธนาคารซ้ำ";
            return null;
        }

        /* Method สำหรับกรอกข้อมูลและกดปุ่ม Save พร้อมตรวจสอบข้อความแจ้งเตือนผลลัพธ์ที่คาดหวัง */
        private void FillSaveAssert(AutomationElement tab, Window form, string code, string name, string saveBtn, string testName, params string[] msgs)
        {
            SetText(tab, "devtxtBankCode", code);
            SetText(tab, "devtxtBankName", name);
            form.FindFirstDescendant(cf => cf.ByName(saveBtn))?.Click();
            AssertTaskResult(form, msgs, testName);
        }

        /* Method ค้นหาแถวข้อมูลในตารางและกดปุ่ม Edit เพื่อเปิดฟอร์มแก้ไข */
        private Window ClickRowAndOpenEdit(string code)
        {
            var row = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName(code))
                                   ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("Row 1"))
                                   ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("row 0")))
                      ?? throw new Exception($"หาแถว '{code}' ไม่เจอ");
            row.Click();

            var btn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("Edit"))) ?? throw new Exception("ไม่พบปุ่ม Edit");
            btn.Click();
            return Retry(FindOpenForm) ?? throw new Exception("หาฟอร์มไม่เจอ");
        }

        /* Method ทดสอบลำดับการกดปุ่ม Close และตอบ Dialog (Yes, Cancel, No) */
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

        /* Method คลิกปิดฟอร์มและจัดการ Pop-up ยืนยันที่เด้งขึ้นมา */
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

        /* Method คลิกปุ่มบนกล่อง Dialog ตามชื่อที่กำหนด */
        private void ClickDialogButton(Window dlg, string name)
        {
            var btn = Retry(() => dlg.FindFirstDescendant(cf => cf.ByName(name))?.AsButton()
                                   ?? dlg.FindFirstDescendant(cf => cf.ByName($"&{name}"))?.AsButton(), 2000)
                      ?? throw new Exception($"ไม่พบปุ่ม '{name}'");
            btn.Click();
        }

        /* Method ตรวจสอบว่าฟอร์มยังคงเปิดอยู่หรือไม่ */
        private void CheckFormOpenOnly(Window form)
        {
            if (!WaitUntil(() => !form.Properties.IsOffscreen.ValueOrDefault && form.IsAvailable, 2000))
                throw new Exception("ฟอร์มถูกปิด");
        }

        /* Method Assert ยืนยันว่าฟอร์มยังเปิดอยู่พร้อมบันทึกผลสำเร็จ */
        private void AssertFormOpen(Window form, string testName)
        {
            CheckFormOpenOnly(form);
            LogSuccess(testName, "ฟอร์มถูกเปิดอยู่");
        }

        /* Method Assert ยืนยันว่าฟอร์มถูกปิดเรียบร้อยแล้ว */
        private void AssertFormClosed(Window form, string testName)
        {
            if (!WaitUntil(() => form.Properties.IsOffscreen.ValueOrDefault || !form.IsAvailable, 3000))
                throw new Exception("ฟอร์มไม่ถูกปิด");
            LogSuccess(testName, "ฟอร์มถูกปิด");
        }

        /* Method ตรวจสอบข้อความแจ้งเตือน (Task Result) ว่าตรงกับที่คาดหวังไว้หรือไม่ */
        private void AssertTaskResult(Window form, string[] msgs, string testName, string? customMsg = null)
        {
            var pending = new HashSet<string>(msgs);
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < 5000 && pending.Count > 0)
            {
                var panel = FindTaskResultPanel(form, 500);
                if (panel != null)
                {
                    var texts = GetPanelTexts(panel).ToList();
                    foreach (var m in pending.ToList())
                    {
                        if (texts.Any(t => t.Contains(m))) pending.Remove(m);
                    }
                }
                if (pending.Count == 0) break;
                Thread.Sleep(100);
            }

            if (pending.Count > 0) throw new Exception($"ไม่พบข้อความ: '{string.Join("', '", pending)}'");

            string logMsg = customMsg ?? $"พบข้อความ: {string.Join(",", msgs.Select(m => $"'{m}'"))}";
            LogSuccess(testName, logMsg);
        }

        /* Method ค้นหา Panel แสดงผลลัพธ์ของงาน (Task Result Panel) */
        private AutomationElement? FindTaskResultPanel(Window form, int timeoutMs) =>
            Retry(() => form.FindFirstDescendant(cf => cf.ByName("Task Result"))
                       ?? form.FindFirstDescendant(cf => cf.ByAutomationId("dockPanelTaskResult"))
                       ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("Task Result"))
                       ?? _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("dockPanelTaskResult")), timeoutMs);

        /* Method ดึงข้อความทั้งหมดที่อยู่ภายใน Panel */
        private IEnumerable<string> GetPanelTexts(AutomationElement panel)
        {
            foreach (var el in panel.FindAllDescendants())
            {
                if (!string.IsNullOrEmpty(el.Name)) yield return el.Name;
                if (el.Patterns.Value.IsSupported && !string.IsNullOrEmpty(el.Patterns.Value.Pattern.Value.Value))
                    yield return el.Patterns.Value.Pattern.Value.Value;
            }
        }

        /* Method รีเฟรชตารางข้อมูลหลัก (Grid) */
        private void RefreshGrid()
        {
            try { _mainWindow.FindFirstDescendant(cf => cf.ByName("Find"))?.Click(); } catch { }
            try
            {
                var g = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataGrid))
                                    ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Table)), 2000);
                g?.Focus();
                g?.Click();
            }
            catch { }
        }

        /* Method ตรวจสอบว่ามีข้อมูลเรคอร์ดที่ต้องการแสดงอยู่ในตารางหลักหรือไม่ */
        private bool VerifyRecordInGrid(string val, int timeoutMs = 5000, bool refresh = true)
        {
            if (refresh) RefreshGrid();
            return Retry(() => FindGridElement(val), timeoutMs) != null;
        }

        /* Method บันทึกผลลัพธ์การตรวจสอบข้อมูลใน Grid */
        private void LogGridResult(string testName, string code)
        {
            if (VerifyRecordInGrid(code)) LogSuccess(testName, $"'{code}' เพิ่มสำเร็จ (พบในตารางหลัก)");
            else LogFailure(testName, $"'{code}' ไม่พบในตารางหลักหลังบันทึก");
        }

        /* Method ตัวช่วยรัน Test Step พร้อมดักจับ Exception อัตโนมัติ */
        private void RunStep(string testName, Action act)
        {
            try { act(); }
            catch (Exception ex) { LogFailure(testName, $"ไม่ผ่าน: {ex.Message}"); }
        }

        /* Method รวมการรัน Step ทดสอบ + รายงาน Progress ในจุดเดียวกัน ลดการเขียนสองบรรทัดซ้ำทุกจุดที่ต้องอัปเดต progress หลังจบแต่ละเคส */
        private void RunStep(string testName, Action act, int progress, string progressMessage)
        {
            RunStep(testName, act);
            TestProgressHelper.Report(progress, progressMessage);
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

        /* Method ตัวช่วยรันวนลูปเช็คเงื่อนไขตามเวลา Timeout ที่กำหนด */
        private static bool WaitUntil(Func<bool> condition, int timeoutMs = TimeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try { if (condition()) return true; } catch { }
                Thread.Sleep(100);
            }
            return false;
        }

        /* Method ตรวจสอบจนกว่าหน้าต่างจะถูกปิดลง */
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

        /* Method ตัวช่วยสำหรับ Retry ค้นหา Element ซ้ำๆ จนกว่าจะเจอหรือหมดเวลา Timeout */
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
                Thread.Sleep(150);
            }
            return null;
        }
    }
}