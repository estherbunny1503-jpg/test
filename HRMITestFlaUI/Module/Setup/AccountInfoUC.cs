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
    /// <summary>
    /// รวมขั้นตอน UI Automation สำหรับหน้ากำหนดรหัสบัญชี (Account Info)
    /// ครอบคลุมการเปิดเมนู เพิ่มข้อมูล ตรวจสอบ Validation แก้ไข และลบรายการ
    /// โดยค้นหาองค์ประกอบผ่าน AutomationId/ชื่อ และตรวจผลจากข้อความหรือข้อมูลในตาราง
    /// </summary>
    public class AccountInfoUC
    {
        // หน้าต่างหลักและ Automation ที่ส่งมาจากผู้เรียก ใช้ร่วมกันตลอดการทดสอบคลาสนี้
        private readonly AutomationElement _mainWindow;
        private readonly UIA3Automation _automation;

        // ตัวนับผลเฉพาะ Instance นี้ ใช้สรุปเมื่อจบการทดสอบ Delete
        private int _totalSuccess = 0, _totalFail = 0;

        // เวลารอเริ่มต้นสำหรับการค้นหา UI; บางขั้นตอนกำหนดเวลาเฉพาะตามลักษณะการโหลด
        private const int TimeoutMs = 6000;

        // ใช้ระบุฟอร์มบัญชีเมื่อค้นหาจากหน้าต่าง Desktop
        private const string FormId = "AccountInfoUC";

        // แท็บข้อมูลทั่วไปซึ่งประกอบด้วยช่องรหัส ชื่อ และรายละเอียดบัญชี
        private const string GeneralTabId = "devTabPgGeneral";

        /// <summary>
        /// รับหน้าต่างหลักและ UIA3 Automation ที่สร้างไว้จากขั้นตอนเตรียมการทดสอบ
        /// การตรวจ null ตั้งแต่ต้นทำให้พบปัญหาการเรียกใช้ได้ชัดเจนก่อนเริ่มควบคุม UI
        /// </summary>
        public AccountInfoUC(AutomationElement mainWindow, UIA3Automation automation)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

        // -------------------------------------------------------------
        // 1. ทดสอบการ New โดยเพิ่มข้อมูล 5 ข้อมูล
        //    สัดส่วน Progress: 0 -> 20
        // -------------------------------------------------------------
        public void NavigateToAccountInfoUC()
        {
            // ชุดข้อมูลตัวอย่างที่จะสร้าง เรียงตามลำดับการทดสอบ
            // SaveBtn กำหนดพฤติกรรมหลังบันทึก ส่วน NeedsManualClose ระบุว่าต้องปิดฟอร์มเองหรือไม่
            var accountList = new[]
            {
                new {
                    Code = "BAN001",
                    NameTh = "บัญชีเงินสด",
                    NameEn = "Cash Account - Bank of Ayudhya",
                    Desc = "บัญชีเงินสด/เงินฝากกระแสรายวัน ธนาคารกรุงศรีอยุธยา (BAY) สำหรับสำรองจ่ายหมุนเวียน",
                    SaveBtn = "Save",
                    NeedsManualClose = true
                },
                new {
                    Code = "BAN002",
                    NameTh = "บัญชีเงินฝากธนาคาร",
                    NameEn = "Bank Deposit - Bangkok Bank",
                    Desc = "บัญชีเงินฝากออมทรัพย์/กระแสรายวัน ธนาคารกรุงเทพ (BBL) สำหรับรับ-โอนเงินในระบบ",
                    SaveBtn = "Save and New",
                    NeedsManualClose = false
                },
                new {
                    Code = "BAN003",
                    NameTh = "บัญชีค่าใช้จ่าย",
                    NameEn = "Expense Account - Mizuho Bank",
                    Desc = "บัญชีสำหรับตัดจ่ายค่าใช้จ่ายการดำเนินงาน ผ่านธนาคารมิซูโฮ (Mizuho Corporate Bank)",
                    SaveBtn = "Save and New",
                    NeedsManualClose = false
                },
                new {
                    Code = "BAN004",
                    NameTh = "เงินสมทบประกันสังคม",
                    NameEn = "Social Security Contribution - Krung Thai Bank",
                    Desc = "บัญชีตัดจ่ายเงินสมทบกองทุนประกันสังคม ผ่านธนาคารกรุงไทย (KTB)",
                    SaveBtn = "Save and New",
                    NeedsManualClose = false
                },
                new {
                    Code = "BAN005",
                    NameTh = "เจ้าหนี้การค้า",
                    NameEn = "Accounts Payable - Sumitomo Mitsui Banking Corporation",
                    Desc = "บัญชีสำหรับการจ่ายชำระเจ้าหนี้การค้าและคู่ค้าต่างประเทศ ผ่านธนาคารซูมิโตโม มิตซุย (SMBC)",
                    SaveBtn = "Save and Close",
                    NeedsManualClose = false
                }
            };

            const string stageName = "TestCase00_AddAccountData";
            TestLogger.LogInfo("AccountSetupInfoPage", "เริ่มทดสอบหน้าจอ Account Setup Info");
            TestProgressHelper.Report(2, "Opening Setup -> Account Info Menu via Search...");

            // เปิดเมนูด้วยช่องค้นหาของโปรแกรม หากเปิดไม่สำเร็จให้จบขั้นตอนนี้
            if (!TryOpenAccountMenu(stageName)) return;

            Window? form = null;
            for (int i = 0; i < accountList.Length; i++)
            {
                var a = accountList[i];
                string testName = $"{stageName}_{a.Code}";
                TestProgressHelper.Report(4 + (i * 4), $"Adding Account ({i + 1}/5): {a.Code} - {a.NameTh}...");

                try
                {
                    // ใช้ฟอร์มเดิมต่อเมื่อยังเปิดอยู่ เช่น หลัง Save and New
                    form ??= GetOrOpenForm();
                    var tab = GetGeneralTab(form);
                    FillAccountFields(tab, a.Code, a.NameTh, a.NameEn, a.Desc);

                    // ถ้าพบข้อมูลซ้ำจะบันทึกผลไม่ผ่านและข้ามไปข้อมูลถัดไป
                    if (!TrySaveAndHandleDuplicate(form, testName, a.Code, a.SaveBtn))
                    {
                        form = null;
                        continue;
                    }

                    if (a.SaveBtn == "Save and New")
                    {
                        // ปุ่ม Save and New ควรล้างฟอร์มเพื่อให้กรอกรายการถัดไป
                        WaitUntil(() => string.IsNullOrEmpty(tab.FindFirstDescendant(cf => cf.ByAutomationId("devtxtAccountCode"))?.AsTextBox().Text), 3000);
                        LogSuccess(testName, $"'{a.Code}' บันทึกสำเร็จ (Save and New)");
                    }
                    else
                    {
                        // กรณีบันทึกแล้วไม่ได้ปิดฟอร์มอัตโนมัติ ให้ปิดก่อนตรวจข้อมูลในตาราง
                        if (a.NeedsManualClose) form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();
                        WaitForClosed(() => FindOpenForm());
                        form = null;
                        LogGridResult(testName, a.Code);
                    }
                }
                catch (Exception ex)
                {
                    // ความผิดพลาดของรายการหนึ่งไม่หยุดการสร้างรายการอื่น
                    LogFailure(testName, $"'{a.Code}' เพิ่มไม่สำเร็จ: {ex.Message}");
                    form = null;
                }
            }
        }

        // -------------------------------------------------------------
        // 2. ทดสอบ แจ้งเตือนต่างๆ เมื่อ New
        //    สัดส่วน Progress: 20 -> 40
        // -------------------------------------------------------------
        public void RunAllValidationTests()
        {
            const string stage = "TestCase01-05_Validation";
            TestProgressHelper.Report(22, "Opening Account Info Form for Validation Tests...");

            Window form;
            // หากเปิดฟอร์มไม่ได้ ไม่มีเงื่อนไขให้ทดสอบต่อ จึงบันทึกความล้มเหลวแล้วคืนค่า
            try { form = OpenForm(); }
            catch (Exception ex) { LogFailure(stage, $"เปิดฟอร์มไม่สำเร็จ: {ex.Message}"); return; }

            var tab = GetGeneralTab(form);

            // แต่ละกรณีกรอกข้อมูลไม่ถูกต้อง กดปุ่มบันทึก และตรวจข้อความ Validation ใน Task Result
            // ข้อที่ 1: ไม่กรอกรหัส, กรอกชื่อ -> Save
            RunStep("TestCase01_MissingAccountCode",
                () => FillSaveAssert(tab, form, "", "บัญชีเงินสด", "Save", "TestCase01_MissingAccountCode", "รหัสบัญชี ไม่สามารถเป็นค่าว่าง"),
                25, "Validating: Missing Account Code...");

            // ข้อที่ 2: กรอกรหัส BAN006, ลบชื่อบัญชี -> Save and New
            RunStep("TestCase02_MissingAccountName",
                () => FillSaveAssert(tab, form, "BAN006", "", "Save and New", "TestCase02_MissingAccountName", "ชื่อบัญชี ไม่สามารถเป็นค่าว่าง"),
                28, "Validating: Missing Account Name...");

            // ข้อที่ 3: ลบรหัสออก, ไม่กรอกชื่อ -> Save and New
            RunStep("TestCase03_MissingAccountCodeAndName",
                () => FillSaveAssert(tab, form, "", "", "Save and New", "TestCase03_MissingAccountCodeAndName", "รหัสบัญชี ไม่สามารถเป็นค่าว่าง", "ชื่อบัญชี ไม่สามารถเป็นค่าว่าง"),
                31, "Validating: Missing Code and Name...");

            // ข้อที่ 4: รหัสซ้ำ BAN003, ชื่อ บัญชีเงินสด -> Save
            RunStep("TestCase04_DuplicateAccountCode",
                () => FillSaveAssert(tab, form, "BAN003", "บัญชีเงินสด", "Save", "TestCase04_DuplicateAccountCode", "รหัสบัญชี ห้ามซ้ำ"),
                34, "Validating: Duplicate Account Code...");

            // ข้อที่ 5: รหัส BAN006, ชื่อซ้ำ บัญชีเงินฝากธนาคาร -> Save and Close
            const string dupName = "TestCase05_DuplicateAccountName";
            RunStep(dupName,
                () => FillSaveAssert(tab, form, "BAN006", "บัญชีเงินฝากธนาคาร", "Save and Close", dupName, "ชื่อบัญชี ห้ามซ้ำ"),
                37, "Validating: Duplicate Account Name...");

            // Sequence ปิดหน้าจอ Yes/Cancel/No
            RunStep($"{dupName}_CloseSequence",
                () => RunCloseDialogSequence(form, dupName, "ชื่อบัญชี ห้ามซ้ำ"),
                40, "Validating: Duplicate Name Close Sequence...");
        }

        // -------------------------------------------------------------
        // 3. ทดสอบ Edit
        //    สัดส่วน Progress: 40 -> 55
        // -------------------------------------------------------------
        public void RunEditTests()
        {
            TestProgressHelper.Report(42, "Starting Edit Operations...");

            // แยกข้อมูลที่ใช้แก้ไขเป็นรายการ เพื่อให้ใช้ขั้นตอนเดียวกันกับหลายแถว
            var edits = new[]
            {
                new {
                    Name = "TestCase06_EditRow1",
                    OldCode = "BAN004",
                    NewCode = "B004",
                    NameTh = "เงินสมทบกองทุนเงินทดแทน",
                    NameEn = "Workmen's Compensation Fund",
                    Desc = "เงินสมทบกองทุนเงินทดแทนประจำปีส่วนของนายจ้าง",
                    SaveBtn = "Save",
                    NeedsManualClose = true
                },
                new {
                    Name = "TestCase07_EditRow2",
                    OldCode = "BAN005",
                    NewCode = "B005",
                    NameTh = "เจ้าหนี้อื่น - ค่าบริการภายนอก",
                    NameEn = "Other Accounts Payable - Outsourced Services",
                    Desc = "ค่าบริการที่ปรึกษาและผู้ให้บริการภายนอกค้างจ่าย",
                    SaveBtn = "Save and Close",
                    NeedsManualClose = false
                }
            };


            for (int i = 0; i < edits.Length; i++)
            {
                var e = edits[i];
                try
                {
                    // เปิดรายการเดิม แก้ข้อมูล และบันทึกด้วยปุ่มที่กำหนดสำหรับแต่ละเคส
                    var form = ClickRowAndOpenEdit(e.OldCode);
                    var tab = GetGeneralTab(form);
                    FillAccountFields(tab, e.NewCode, e.NameTh, e.NameEn, e.Desc);

                    if (!TrySaveAndHandleDuplicate(form, e.Name, e.NewCode, e.SaveBtn)) continue;
                    if (e.NeedsManualClose) form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();

                    WaitForClosed(() => FindOpenForm());
                    RefreshGrid();

                    // ตรวจทั้งรหัสและชื่อ เพราะระบบอาจไม่อนุญาตให้เปลี่ยนรหัสหลัก (Primary Key)
                    bool recordFound = VerifyRecordInGrid(e.NewCode, refresh: false) || VerifyRecordInGrid(e.NameTh, refresh: false);

                    if (recordFound)
                        LogSuccess(e.Name, $"แก้ไข '{e.OldCode}' สำเร็จ (พบข้อมูลที่แก้ไขในตารางหลัก)");
                    else
                        LogFailure(e.Name, $"แก้ไข '{e.OldCode}' แล้ว แต่ไม่พบข้อมูลที่อัปเดตในตารางหลัก");
                }
                catch (Exception ex) { LogFailure(e.Name, $"แก้ไข '{e.OldCode}' ไม่สำเร็จ: {ex.Message}"); }
                finally
                {
                    // รายงานความคืบหน้าแม้เคสปัจจุบันจะเกิดข้อผิดพลาด
                    TestProgressHelper.Report(42 + ((i + 1) * 13 / edits.Length), $"Editing Account Record ({i + 1}/{edits.Length})...");
                }
            }
        }

        // -------------------------------------------------------------
        // 4. ทดสอบ การแจ้งเตือน Edit
        //    สัดส่วน Progress: 55 -> 80
        // -------------------------------------------------------------
        public void RunEditValidationTests()
        {
            const string baseName = "TestCase08_EditValidation";

            TestProgressHelper.Report(58, "Opening Edit Form for Validation Tests...");

            Window form;
            try
            {
                // รองรับทั้งรหัสใหม่ B005 และรหัสเดิม BAN005 หากระบบไม่อนุญาตให้เปลี่ยนรหัส
                form = TryClickRowAndOpenEdit("B005") ?? ClickRowAndOpenEdit("BAN005");
            }
            catch (Exception ex) { LogFailure(baseName, $"เปิดฟอร์ม Edit ไม่สำเร็จ: {ex.Message}"); return; }

            var tab = GetGeneralTab(form);

            // ตรวจ Validation บนฟอร์มแก้ไข โดยสลับข้อมูลและปุ่ม Save ตามกรณี
            // ข้อที่ 1: ลบรหัสออก, ชื่อเดิม -> Save
            RunStep($"{baseName}_MissingCode",
                () => FillSaveAssert(tab, form, "", "เจ้าหนี้อื่น - ค่าบริการภายนอก", "Save", $"{baseName}_MissingCode", "รหัสบัญชี ไม่สามารถเป็นค่าว่าง"),
                62, "Validating Edit: Missing Code...");

            // ข้อที่ 2: รหัสเดิม, ลบชื่อออก -> Save and New
            RunStep($"{baseName}_MissingName",
                () => FillSaveAssert(tab, form, "B005", "", "Save and New", $"{baseName}_MissingName", "ชื่อบัญชี ไม่สามารถเป็นค่าว่าง"),
                66, "Validating Edit: Missing Name...");

            // ข้อที่ 3: ลบรหัสออก, ลบชื่อออก -> Save
            RunStep($"{baseName}_MissingBoth",
                () => FillSaveAssert(tab, form, "", "", "Save", $"{baseName}_MissingBoth", "รหัสบัญชี ไม่สามารถเป็นค่าว่าง", "ชื่อบัญชี ไม่สามารถเป็นค่าว่าง"),
                70, "Validating Edit: Missing Both...");

            // ข้อที่ 4: รหัสซ้ำ BAN001, ชื่อเดิม -> Save and New
            RunStep($"{baseName}_DuplicateCode",
                () => FillSaveAssert(tab, form, "BAN001", "เจ้าหนี้อื่น - ค่าบริการภายนอก", "Save and New", $"{baseName}_DuplicateCode", "รหัสบัญชี ห้ามซ้ำ"),
                74, "Validating Edit: Duplicate Code...");

            // ข้อที่ 5: รหัสใหม่ BAN006, ชื่อซ้ำ บัญชีเงินสด -> Save and Close
            const string dupSub = $"{baseName}_DuplicateName";
            RunStep(dupSub,
                () => FillSaveAssert(tab, form, "BAN006", "บัญชีเงินสด", "Save and Close", dupSub, "ชื่อบัญชี ห้ามซ้ำ"),
                78, "Validating Edit: Duplicate Name...");

            // Sequence ปิดหน้าจอ Yes/Cancel/No
            RunStep($"{dupSub}_CloseSequence",
                () => RunCloseDialogSequence(form, dupSub, "ชื่อบัญชี ห้ามซ้ำ"),
                80, "Validating Edit: Duplicate Name Close Sequence...");

            LogSuccess(baseName, "รัน Validation Edit ครบทุกเคสแล้ว");
        }

        // -------------------------------------------------------------
        // 5. ทดสอบ Delete
        //    สัดส่วน Progress: 80 -> 100
        // -------------------------------------------------------------
        public void RunDeleteTests()
        {
            const string baseName = "TestCase09_Delete";

            TestProgressHelper.Report(84, "Starting Delete Tests...");

            WaitForClosed(() => FindOpenForm());
            RefreshGrid();

            // กรณีตอบ Yes ต้องไม่พบรายการอีกหลังยืนยันการลบ
            // เคสที่ 1: ตรวจสอบและสั่งลบรายการ B005 (หรือ BAN005) -> กด Yes
            string targetDeleteCode = FindGridElement("B005") != null ? "B005" : "BAN005";
            RunDeleteCase(baseName, targetDeleteCode, "Yes", expectDeleted: true);
            TestProgressHelper.Report(92, $"Delete Test: {targetDeleteCode} (confirmed delete)...");

            // กรณีตอบ No ต้องพบรายการเดิมอยู่หลังยกเลิกการลบ
            // เคสที่ 2: ตรวจสอบและสั่งลบรายการ B004 (หรือ BAN004) -> กด No
            string targetCancelCode = FindGridElement("B004") != null ? "B004" : "BAN004";
            RunDeleteCase(baseName, targetCancelCode, "No", expectDeleted: false);
            TestProgressHelper.Report(100, $"Delete Test: {targetCancelCode} (cancelled delete)...");

            LogSuccess(baseName, "ทดสอบ Delete ครบทุกเคส");
            TestLogger.LogInfo("AccountSetupInfoPage", $"สรุปผลรวมทั้งหมด: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");
        }

        // -------------------------------------------------------------
        // Helper สำหรับการทดสอบและควบคุมหน้าจอ
        // -------------------------------------------------------------
        private void RunDeleteCase(string baseName, string code, string btn, bool expectDeleted)
        {
            string testName = $"{baseName}_{code}_{btn}";
            try
            {
                // รีเฟรชตารางก่อนเลือกแถว เพื่อใช้สถานะข้อมูลล่าสุด
                RefreshGrid();

                var elRow = Retry(() => FindGridElement(code), 4000)
                            ?? throw new Exception($"หาแถวรหัส '{code}' ในตารางไม่เจอ");

                // เลือกแถว เปิดปุ่ม Delete แล้วรอหน้าต่างถามยืนยัน
                WaitUntil(() => { try { elRow.Focus(); return true; } catch { return false; } }, 1000);
                elRow.Click();

                var delBtn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("Delete"))) ?? throw new Exception("หาปุ่ม Delete ไม่เจอ");
                delBtn.Click();

                var dlg = Retry(() => _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault()
                                      ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("คำเตือน"))?.AsWindow())
                          ?? throw new Exception("กด Delete แล้วไม่พบ Pop-up ยืนยัน");

                ClickDialogButton(dlg, btn);
                WaitForClosed(() => dlg);

                // ตรวจสถานะจริงจากตารางหลังตอบ Yes/No แทนการอาศัยเพียงการปิด Dialog
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

        private bool TryOpenAccountMenu(string stage)
        {
            try
            {
                // หา Search ด้วย AutomationId ก่อน แล้วจึงใช้ ControlType.Edit เป็นทางเลือก
                var searchBox = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("baritemSearch"))
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)), 4000)
                              ?? throw new Exception("ไม่พบช่องค้นหา baritemSearch");

                WaitUntil(() => { try { searchBox.Focus(); return true; } catch { return false; } }, 1000);

                string searchKeyword = "กำหนดรหัสบัญชี";
                // ใช้ Value Pattern หากรองรับ; หากไม่รองรับให้พิมพ์ผ่าน Keyboard
                if (searchBox.Patterns.Value.IsSupported)
                    searchBox.Patterns.Value.Pattern.SetValue(searchKeyword);
                else
                    Keyboard.Type(searchKeyword);

                var searchBtn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("barChkSearch"))
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("Find")), 4000)
                              ?? throw new Exception("ไม่พบปุ่มค้นหา barChkSearch");

                searchBtn.Click();

                // เลือกผลลัพธ์เมนูจากข้อความค้นหา หรือชื่อแถวที่ UI แสดงเป็น fallback
                var menuResult = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName(searchKeyword))
                                               ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("treeListColumn row 1")), 4000)
                               ?? throw new Exception("ไม่พบผลลัพธ์เมนูจากการค้นหา");

                menuResult.Focus();
                menuResult.Click();

                Keyboard.Press(VirtualKeyShort.ENTER);

                // เช็คโดยตรงว่าหน้าจอแสดงปุ่ม New พร้อมใช้งาน
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

        // คืนฟอร์มที่เปิดอยู่แล้วถ้าพบ มิฉะนั้นกด New เพื่อสร้างฟอร์มใหม่
        private Window GetOrOpenForm() => Retry(FindOpenForm, 1500) ?? OpenForm();

        private Window OpenForm()
        {
            // เปิดฟอร์มจากปุ่ม New และรอจนพบฟอร์มพร้อมช่องรหัสบัญชี
            var newBtn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("New")), 3000)
                         ?? throw new Exception("หาปุ่ม New ไม่เจอ");
            newBtn.Click();

            var form = Retry(FindOpenForm, 8000) ?? throw new Exception("หาฟอร์มไม่เจอ");
            WaitUntil(() => form.FindFirstDescendant(cf => cf.ByAutomationId("devtxtAccountCode")) != null, 4000);
            return form;
        }

        // ค้นหาฟอร์มแบบ Modal ของหน้าต่างหลักก่อน แล้วค่อยค้นจาก Desktop ด้วย FormId
        private Window? FindOpenForm() =>
            _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault() ??
            _automation.GetDesktop().FindFirstChild(cf => cf.ByAutomationId(FormId))?.AsWindow();

        // ใช้แท็บ General เมื่อพบ; คืนฟอร์มทั้งหน้าเป็น fallback เผื่อ AutomationId ของแท็บไม่มี
        private AutomationElement GetGeneralTab(Window form) =>
            Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(GeneralTabId)), 3000) ?? form;

        private void FillAccountFields(AutomationElement tab, string code, string nameTh, string nameEn, string desc)
        {
            // กรอกข้อมูลทุกฟิลด์ที่ใช้ในแบบฟอร์มบัญชี
            SetText(tab, "devtxtAccountCode", code);
            SetText(tab, "devtxtAccountName", nameTh);
            SetText(tab, "devtxtAccountNameEng", nameEn);
            SetText(tab, "devMemoAccount", desc);
        }

        private void SetText(AutomationElement tab, string id, string text)
        {
            // รอช่องตาม AutomationId ก่อน เพื่อรองรับกรณีฟอร์มยังโหลดไม่เสร็จ
            var txt = Retry(() => tab.FindFirstDescendant(cf => cf.ByAutomationId(id))?.AsTextBox(), 3000) ?? throw new Exception($"ไม่พบช่อง {id}");
            WaitUntil(() => { try { txt.Focus(); return true; } catch { return false; } }, 1000);

            // Value Pattern เป็นวิธีตั้งค่าตรงที่เร็วกว่า; หากใช้ไม่ได้จะจำลองการเลือกและพิมพ์แทน
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
            // เลือกข้อความเดิมและล้างช่องก่อนพิมพ์ ป้องกันการต่อท้ายค่าที่กรอกไว้ก่อนหน้า
            Keyboard.Press(VirtualKeyShort.CONTROL);
            Keyboard.Type("a");
            Keyboard.Release(VirtualKeyShort.CONTROL);
            Keyboard.Press(VirtualKeyShort.BACK);
            Keyboard.Press(VirtualKeyShort.DELETE);

            if (!string.IsNullOrEmpty(text)) Keyboard.Type(text);
        }

        // ค้นหาค่าจากทั้ง Name และ Value ขององค์ประกอบ เพื่อรองรับรูปแบบการเปิดเผยข้อมูลของ Grid ที่ต่างกัน
        private AutomationElement? FindGridElement(string val) =>
            _mainWindow.FindAllDescendants().FirstOrDefault(el =>
                (!string.IsNullOrEmpty(el.Name) && string.Equals(el.Name.Trim(), val, StringComparison.OrdinalIgnoreCase)) ||
                (el.Patterns.Value.IsSupported && string.Equals(el.Patterns.Value.Pattern.Value.Value?.Trim(), val, StringComparison.OrdinalIgnoreCase)));

        private bool TrySaveAndHandleDuplicate(Window form, string testName, string code, string saveBtn)
        {
            // บันทึกข้อมูลก่อน แล้วตรวจ Task Result เฉพาะการเพิ่ม/แก้ไขปกติ
            var btn = form.FindFirstDescendant(cf => cf.ByName(saveBtn)) ?? throw new Exception($"ไม่พบปุ่ม {saveBtn}");
            btn.Click();

            if (testName.Contains("Validation") || testName.Contains("DuplicateAccount")) return true;

            var panel = FindTaskResultPanel(form, 1500);
            if (panel == null) return true;

            string? dup = DetectDuplicateMessage(GetPanelTexts(panel));
            if (dup == null) return true;

            // เมื่อตรวจพบข้อมูลซ้ำ บันทึกผลล้มเหลวและพยายามปิดฟอร์มโดยตอบ No
            LogFailure(testName, $"'{code}' ข้อมูลซ้ำ: {dup}");
            try { ClickCloseAndHandleDialog(form, "No", $"{testName}_Close"); } catch { }
            return false;
        }

        private static string? DetectDuplicateMessage(IEnumerable<string> texts)
        {
            // แยกข้อความซ้ำของรหัสและชื่อ เพื่อให้รายงานสาเหตุได้ตรงประเด็น
            if (texts.Any(t => t.Contains("รหัสบัญชี ห้ามซ้ำ"))) return "รหัสบัญชีซ้ำ";
            if (texts.Any(t => t.Contains("ชื่อบัญชี ห้ามซ้ำ"))) return "ชื่อบัญชีซ้ำ";
            return null;
        }

        private void FillSaveAssert(AutomationElement tab, Window form, string code, string name, string saveBtn, string testName, params string[] msgs)
        {
            // ใช้ใน Validation: ตั้งค่ารหัส/ชื่อ กดปุ่มบันทึก และตรวจว่าพบข้อความที่คาดหวังครบทุกข้อความ
            SetText(tab, "devtxtAccountCode", code);
            SetText(tab, "devtxtAccountName", name);
            form.FindFirstDescendant(cf => cf.ByName(saveBtn))?.Click();
            AssertTaskResult(form, msgs, testName);
        }

        private Window? TryClickRowAndOpenEdit(string code)
        {
            // แปลงความล้มเหลวในการเปิดแถวเป็น null เพื่อให้ผู้เรียกลองใช้รหัสสำรองได้
            try { return ClickRowAndOpenEdit(code); }
            catch { return null; }
        }

        private Window ClickRowAndOpenEdit(string code)
        {
            RefreshGrid();

            // ค้นหาเซลล์ที่ตรงกับรหัสโดยเฉพาะ เพื่อป้องกันการคลิกโดนแถวแรกค้าง
            var row = Retry(() => FindGridElement(code), 4000)
                      ?? throw new Exception($"หาแถว '{code}' ไม่เจอ");

            WaitUntil(() => { try { row.Focus(); return true; } catch { return false; } }, 1000);
            row.Click();

            // หลังเลือกแถวจึงกด Edit และรอจนฟอร์มแก้ไขพร้อมช่องรหัส
            var btn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("Edit")), 3000) ?? throw new Exception("ไม่พบปุ่ม Edit");
            btn.Click();
            var form = Retry(FindOpenForm, 5000) ?? throw new Exception("หาฟอร์มไม่เจอ");
            WaitUntil(() => form.FindFirstDescendant(cf => cf.ByAutomationId("devtxtAccountCode")) != null, 3000);
            return form;
        }

        private void RunCloseDialogSequence(Window form, string prefix, string expectedMsg)
        {
            // ทดสอบผลของ Yes, Cancel และ No เมื่อปิดฟอร์มที่ยังมีข้อมูลผิดพลาด/ยังบันทึกไม่ได้
            // Yes ตรวจข้อความแจ้งเตือนและยืนยันว่าฟอร์มยังอยู่; Cancel ต้องคงฟอร์มไว้; No ต้องปิดฟอร์ม
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
            // กด Close แล้วค้นหา Dialog จากลำดับแหล่งต่าง ๆ เพราะ Dialog อาจผูกกับฟอร์มหรือหน้าต่างหลัก
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
            // รองรับทั้งชื่อปุ่มปกติและชื่อที่มี access key นำหน้าด้วย &
            var btn = Retry(() => dlg.FindFirstDescendant(cf => cf.ByName(name))?.AsButton()
                                   ?? dlg.FindFirstDescendant(cf => cf.ByName($"&{name}"))?.AsButton(), 2000)
                      ?? throw new Exception($"ไม่พบปุ่ม '{name}'");
            btn.Click();
        }

        private void CheckFormOpenOnly(Window form)
        {
            // ฟอร์มถือว่ายังเปิดเมื่อ UIA มองเห็นและ AutomationElement ยังใช้งานได้
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
            // รอจนฟอร์มหายไปจาก UI หรือถูกซ่อน แล้วบันทึกผลสำเร็จ
            if (!WaitUntil(() => form.Properties.IsOffscreen.ValueOrDefault || !form.IsAvailable, 3000))
                throw new Exception("ปิดฟอร์มไม่สำเร็จ");
            LogSuccess(testName, "ฟอร์มถูกปิด");
        }

        private void AssertTaskResult(Window form, string[] msgs, string testName, string? customMsg = null)
        {
            // เก็บข้อความที่ยังต้องตรวจไว้ และนำออกเมื่อพบข้อความนั้นใน Task Result
            var pending = new HashSet<string>(msgs);
            var sw = Stopwatch.StartNew();

            // ตรวจซ้ำช่วงสั้น ๆ เพราะแผงผลลัพธ์อาจอัปเดตหลังคลิก Save เล็กน้อย
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

        // ค้นหาแผง Task Result ในฟอร์มก่อน แล้วจึงค้นในหน้าต่างหลัก เผื่อแผงถูก Dock ไว้ภายนอกฟอร์ม
        private AutomationElement? FindTaskResultPanel(Window form, int timeoutMs) =>
            Retry(() => form.FindFirstDescendant(cf => cf.ByName("Task Result"))
                       ?? form.FindFirstDescendant(cf => cf.ByAutomationId("dockPanelTaskResult"))
                       ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("Task Result"))
                       ?? _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("dockPanelTaskResult")), timeoutMs);

        private IEnumerable<string> GetPanelTexts(AutomationElement panel)
        {
            // อ่านทั้งชื่อองค์ประกอบและ Value Pattern เพราะข้อความอาจถูกเปิดเผยผ่านคนละ property
            foreach (var el in panel.FindAllDescendants())
            {
                if (!string.IsNullOrEmpty(el.Name)) yield return el.Name;
                if (el.Patterns.Value.IsSupported && !string.IsNullOrEmpty(el.Patterns.Value.Pattern.Value.Value))
                    yield return el.Patterns.Value.Pattern.Value.Value;
            }
        }

        private void RefreshGrid()
        {
            // กด Find เพื่อขอให้หน้าจอโหลดรายการล่าสุด แล้วโฟกัส/คลิก Grid เพื่อให้พร้อมรับการเลือกแถว
            // การรีเฟรชเป็น best-effort เพราะบางหน้าจออาจไม่มีปุ่มหรือ Grid ที่ระบุได้
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
            // ให้เลือกได้ว่าจะรีเฟรชก่อนตรวจหรือไม่; ผู้เรียกที่เพิ่งรีเฟรชเองสามารถส่ง refresh=false
            if (refresh) RefreshGrid();
            return Retry(() => FindGridElement(val), timeoutMs) != null;
        }

        private void LogGridResult(string testName, string code)
        {
            // ใช้การปรากฏของรหัสใน Grid เป็นหลักฐานว่าการเพิ่มรายการสำเร็จ
            if (VerifyRecordInGrid(code)) LogSuccess(testName, $"'{code}' เพิ่มสำเร็จ (พบในตารางหลัก)");
            else LogFailure(testName, $"'{code}' ไม่พบในตารางหลักหลังบันทึก");
        }

        private void RunStep(string testName, Action act)
        {
            // แยกการทดสอบเป็นขั้นย่อย: ความผิดพลาดถูกบันทึกเป็น Failure โดยไม่หยุดขั้นถัดไป
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
            // นับผลก่อนส่งรายละเอียดไปยัง TestLogger
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
            // ประเมินเงื่อนไขซ้ำจนเป็นจริงหรือครบเวลา; Exception ชั่วคราวจาก UIA จะถูกลองใหม่
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try { if (condition()) return true; } catch { }
            }
            return false;
        }

        private static bool WaitForClosed(Func<AutomationElement?> finder, int timeoutMs = TimeoutMs) =>
            // ถือว่าปิดแล้วเมื่อหา Element ไม่พบ, Element ใช้งานไม่ได้ หรือถูกซ่อน
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
            // เรียกค้นหาเดิมซ้ำจนพบออบเจ็กต์หรือหมดเวลา รองรับ UI ที่ยังสร้างองค์ประกอบไม่เสร็จ
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





