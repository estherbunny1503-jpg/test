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
using System.Runtime.InteropServices;
using System.Threading;

namespace HRMITestFlaUI.Module.Organization
{
    public class OrgInfoUC
    {
        // -------------------------------------------------------------
        // 1. ค่าที่อาจต้องปรับเมื่อ Automation ID หรือหน้าจอ OrgInfoUC เปลี่ยน
        // -------------------------------------------------------------
        private const int TimeoutMs = 6000;
        private const string FormId = "OrgInfoUC";
        private const string LogPageKey = "OrgInfoUC";
        private const string GeneralTabId = "devTabPgGeneral";
        private const string AddressTabId = "devTabPgAddress";
        private const string MenuName = "บันทึกข้อมูลองค์กร";

        // General Tab
        private const string IdTxtOrgCode = "devtxtOrgCode";
        private const string IdTxtOrgName = "devtxtOrgName";
        private const string IdTxtOrgNameEng = "devtxtOrgNameEng";
        private const string IdTxtOrgSeq = "devtxtBranchListNo";
        private const string IdTxtTaxBranchNo = "devtxtTaxBranchNo";
        private const string IdTxtTaxId = "devtxtTaxNumber";
        private const string IdDateEstablished = "devDateCreateOrgDay";
        private const string IdCboEra = "devlookUpEditEra";
        private const string FirstObjectID = IdTxtOrgCode;

        // Address Tab
        private const string IdTxtOrgAddressTh = "devtxtOrgAddressTh";

        // ข้อความแจ้งเตือนที่ใช้ตรวจใน Task Result
        private const string MsgCodeEmpty = "รหัสองค์กร ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล";
        private const string MsgNameEmpty = "ชื่อองค์กร ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล";
        private const string MsgCodeDup = "รหัสองค์กร ห้ามซ้ำ กรุณาป้อนข้อมูล";

        // ที่อยู่ตัวอย่างสำหรับเคส Validation
        private const string TestAddress = "123/45 อาคารเทสติ้ง ทาวเวอร์";

        // -------------------------------------------------------------
        // 2. ค่าภายในและข้อมูลทดสอบที่ไม่ต้องปรับตามหน้าจอ
        // -------------------------------------------------------------
        private readonly AutomationElement _mainWindow;
        private readonly UIA3Automation _automation;

        // ตัวนับผลเฉพาะ Instance นี้ ใช้สรุปเมื่อจบการทดสอบ Delete
        private int _totalSuccess = 0, _totalFail = 0;

        private sealed record EditCase(
            string Name,
            string OldCode,
            string NewCode,
            string NameTh,
            string SaveBtn,
            bool NeedsManualClose);

        private static readonly EditCase[] Edits =
        {
            new("TestCase09_EditRow1", "S002", "SAL001", "บริษัท ตัวอย่าง (สาขาเชียงใหม่)", "Save", true),
            new("TestCase10_EditRow2", "S003", "SAL002", "บริษัท ตัวอย่าง (สาขากระบี่)", "Save and Close", false)
        };

        /* Constructor สำหรับรับค่า MainWindow และ Automation Instance เข้ามาใช้งานภายในคลาส */
        public OrgInfoUC(AutomationElement mainWindow, UIA3Automation automation)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

        // -------------------------------------------------------------
        // 1. ทดสอบการ New โดยเพิ่มข้อมูลองค์กร 5 ข้อมูล
        //    สัดส่วน Progress: 0 -> 20
        // -------------------------------------------------------------
        public void NavigateToOrgInfoUC()
        {
            // SaveBtn กำหนดพฤติกรรมหลังบันทึก ส่วน NeedsManualClose ระบุว่าต้องปิดฟอร์มเองหรือไม่
            var DataList = new[]
            {
                new { Code = "S002", Name = "บริษัท ตัวอย่าง (สาขาเชียงใหม่) จำกัด", NameEng = "Sample Company (Chiang Mai Branch) Co., Ltd.", Seq = "1", TaxBranch = "00001", TaxId = "0105569000012", Date = "31/07/2569", Era = "พุทธศักราช", Address = "123/45 อาคารเทสติ้ง ทาวเวอร์",
                      SaveBtn = "Save and New", NeedsManualClose = false },
                new { Code = "S003", Name = "บริษัท ตัวอย่าง (สาขากระบี่)จำกัด", NameEng = "Sample Company (Krabi Branch) Co., Ltd.", Seq = "2", TaxBranch = "00002", TaxId = "0105569000013", Date = "1/1/2569", Era = "พุทธศักราช", Address = "88/99 ถนนมหาราช ",
                      SaveBtn = "Save and New", NeedsManualClose = false },
                new { Code = "S004", Name = "บริษัท ตัวอย่าง (สาขาเชียงราย) จำกัด", NameEng = "Sample Company (Chiang Rai Branch) Co., Ltd.", Seq = "3", TaxBranch = "00003", TaxId = "0105569000014", Date = "1/2/2569", Era = "พุทธศักราช", Address = "45/1 ถนนซุเปอร์ไฮเวย์",
                      SaveBtn = "Save and New", NeedsManualClose = false },
                new { Code = "S005", Name = "บริษัท ตัวอย่าง (สาขาภูเก็ต) จำกัด", NameEng = "Sample Company (Phuket Branch) Co., Ltd.", Seq = "4", TaxBranch = "00004", TaxId = "0105569000015", Date = "15/03/2569", Era = "พุทธศักราช", Address = "74/5 ถนนเทพกระษัตริ์",
                      SaveBtn = "Save and New", NeedsManualClose = false },
                new { Code = "S006", Name = "บริษัท ตัวอย่าง (สาขาขอนแก่น) จำกัด", NameEng = "Sample Company (Khon Kaen Branch) Co., Ltd.", Seq = "5", TaxBranch = "00005", TaxId = "0105569000011", Date = "31/07/2569", Era = "พุทธศักราช", Address = "555/12 ถนนมิตรภาพ",
                      SaveBtn = "Save and Close", NeedsManualClose = false }
            };

            const string stageName = "TestCase00_AddOrgData";
            TestLogger.LogInfo(LogPageKey, "เริ่มทดสอบหน้าจอบันทึกข้อมูลองค์กร");
            TestProgressHelper.Report(2, "Opening Organization -> Org Info Menu via Search...");

            // เปิดเมนูด้วยช่องค้นหาของโปรแกรม หากเปิดไม่สำเร็จให้จบขั้นตอนนี้
            if (!TryOpenMenu(stageName, MenuName)) return;

            Window? form = null;
            for (int i = 0; i < DataList.Length; i++)
            {
                var a = DataList[i];
                string testName = $"{stageName}_{a.Code}";
                TestProgressHelper.Report(4 + (i * 4), $"Adding {MenuName} ({i + 1}/{DataList.Length}): {a.Code} - {a.Name}...");

                try
                {
                    // ใช้ฟอร์มเดิมต่อเมื่อยังเปิดอยู่ เช่น หลัง Save and New
                    form ??= GetOrOpenForm();

                    // กรอกแท็บ General ก่อน แล้วสลับไปแท็บ Address
                    SwitchToTab(form, GeneralTabId, "General");
                    var tab = GetGeneralTab(form);
                    FillData(tab, a.Code, a.Name, a.NameEng, a.Seq, a.TaxBranch, a.TaxId, a.Date, a.Era);
                    FillAddressTab(form, a.Address);

                    // ถ้าพบข้อมูลซ้ำจะบันทึกผลไม่ผ่านและข้ามไปข้อมูลถัดไป
                    if (!TrySaveAndHandleDuplicate(form, testName, a.Code, a.SaveBtn))
                    {
                        form = null;
                        continue;
                    }

                    if (a.SaveBtn == "Save and New")
                    {
                        // ปุ่ม Save and New ควรล้างฟอร์มเพื่อให้กรอกรายการถัดไป
                        // (ค้นจาก form เพราะแท็บที่เลือกอยู่ตอนนี้คือ Address)
                        var codeInput = form.FindFirstDescendant(cf => cf.ByAutomationId(FirstObjectID))?.AsTextBox();
                        WaitUntil(() => string.IsNullOrEmpty(codeInput?.Text), 3000);
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
            TestProgressHelper.Report(22, "Opening Org Info Form for Validation Tests...");

            Window form;
            // หากเปิดฟอร์มไม่ได้ ไม่มีเงื่อนไขให้ทดสอบต่อ จึงบันทึกความล้มเหลวแล้วคืนค่า
            try { form = OpenForm(); }
            catch (Exception ex) { LogFailure(stage, $"เปิดฟอร์มไม่สำเร็จ: {ex.Message}"); return; }

            // ข้อที่ 1: ไม่กรอกรหัส + ไม่กรอกชื่อ + กรอกที่อยู่ -> Save
            RunStep("TestCase01_MissingCodeAndName",
                () => FillSaveAssert(form, "", "", TestAddress, "Save", "TestCase01_MissingCodeAndName", MsgCodeEmpty, MsgNameEmpty),
                30, "Validating: Missing Code and Name...");

            // ข้อที่ 2: กรอกรหัสและชื่อซ้ำ S002 + ที่อยู่ -> Save and Close
            const string dupName = "TestCase02_DuplicateCodeAndName";
            RunStep(dupName,
                () =>
                {
                    FillSaveAssert(form, "S002", "บริษัท ตัวอย่าง (สาขาเชียงใหม่) จำกัด", TestAddress, "Save and Close", dupName, MsgCodeDup);
                    AssertFormOpen(form, dupName);
                },
                34, "Validating: Duplicate Code and Name...");

            // Sequence ปิดหน้าจอ Yes/Cancel/No
            RunStep($"{dupName}_CloseSequence",
                () => RunCloseDialogSequence(form, dupName, MsgCodeDup),
                40, "Validating: Close Dialog Sequence...");
        }

        // -------------------------------------------------------------
        // 3. ทดสอบการแก้ไขข้อมูล (Edit Record Tests)
        //    สัดส่วน Progress ของเมธอดนี้: 40 -> 55
        // -------------------------------------------------------------
        public void RunEditTests()
        {
            TestProgressHelper.Report(42, "Starting Edit Operations...");

            for (int i = 0; i < Edits.Length; i++)
            {
                var e = Edits[i];
                try
                {
                    // เปิดรายการเดิม แก้ข้อมูล และบันทึกด้วยปุ่มที่กำหนดสำหรับแต่ละเคส
                    var form = ClickRowAndOpenEdit(e.OldCode);
                    SwitchToTab(form, GeneralTabId, "General");
                    var tab = GetGeneralTab(form);
                    SetText(tab, IdTxtOrgCode, e.NewCode);
                    SetText(tab, IdTxtOrgName, e.NameTh);

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
                    // รายงานความคืบหน้าแม้เคสปัจจุบันจะเกิดข้อผิดพลาด
                    TestProgressHelper.Report(42 + ((i + 1) * 13 / Edits.Length), $"Editing Org Record ({i + 1}/{Edits.Length})...");
                }
            }
        }

        // -------------------------------------------------------------
        // 4. ทดสอบ Validation ตอนแก้ไขข้อมูล (Edit Validation Tests)
        //    สัดส่วน Progress ของเมธอดนี้: 55 -> 80
        // -------------------------------------------------------------
        public void RunEditValidationTests()
        {
            const string baseName = "TestCase11-16_EditValidation";
            TestProgressHelper.Report(58, "Opening Edit Form for Validation Tests...");

            Window form;
            try { form = ClickRowAndOpenEdit(Edits[1].NewCode); }
            catch (Exception ex) { LogFailure(baseName, $"เปิดฟอร์ม Edit ไม่สำเร็จ: {ex.Message}"); return; }

            // 4.1: ลบรหัสองค์กร + ลบชื่อ + กรอกที่อยู่ -> Save
            RunStep("TestCase11_Edit_MissingCode",
                () => FillSaveAssert(form, "", "", TestAddress, "Save", "TestCase11_Edit_MissingCode", MsgCodeEmpty, MsgNameEmpty),
                66, "Validating Edit: Missing Code and Name...");

            // 4.2: กรอกรหัสซ้ำ S004 + ชื่อซ้ำ + ที่อยู่ แล้วทดสอบ Close -> Yes/Cancel/No
            //      (Yes = บันทึก จึงต้องพบข้อความรหัสซ้ำ และฟอร์มยังเปิดอยู่)
            const string dupSub = "TestCase14_Edit_Duplicate";
            RunStep(dupSub,
                () =>
                {
                    SwitchToTab(form, GeneralTabId, "General");
                    var tab = GetGeneralTab(form);
                    SetText(tab, IdTxtOrgCode, "S004");
                    SetText(tab, IdTxtOrgName, "บริษัท ตัวอย่าง (สาขาเชียงราย) จำกัด");
                    FillAddressTab(form, TestAddress);

                    RunCloseDialogSequence(form, dupSub, MsgCodeDup);
                },
                80, "Validating Edit: Duplicate Close Sequence...");

            LogSuccess(baseName, "รัน Validation Edit ครบทุกเคสแล้ว");
        }

        // -------------------------------------------------------------
        // 5. ทดสอบการลบข้อมูล (Delete Record Tests)
        //    สัดส่วน Progress ของเมธอดนี้: 80 -> 100
        // -------------------------------------------------------------
        public void RunDeleteTests()
        {
            const string baseName = "TestCase16-17_Delete";
            TestProgressHelper.Report(84, "Starting Delete Tests...");

            // เคสที่ 1: คลิกรหัส S004 -> Delete -> Yes -> ตรวจสอบว่าถูกลบ
            RunDeleteCase(baseName, "S004", "Yes", expectDeleted: true);
            TestProgressHelper.Report(92, "Delete Test: S004 (confirmed delete)...");

            // เคสที่ 2: คลิกรหัส S005 -> Delete -> No -> ตรวจสอบว่าข้อมูลยังอยู่
            RunDeleteCase(baseName, "S005", "No", expectDeleted: false);
            TestProgressHelper.Report(100, "Delete Test: S005 (cancelled delete)...");

            LogSuccess(baseName, "ทดสอบ Delete ครบทุกเคส");
            TestLogger.LogInfo(LogPageKey, $"สรุปผลรวมทั้งหมด: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");
        }

        // -------------------------------------------------------------
        // 1. Helper ที่เกี่ยวข้องกับหน้าจอ OrgInfoUC
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

                WaitUntil(() => { try { elRow.Focus(); return true; } catch { return false; } }, 1000);
                elRow.Click();

                var delBtn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("Delete")), 3000)
                             ?? throw new Exception("หาปุ่ม Delete ไม่เจอ");
                delBtn.Click();

                var dlg = Retry(() => _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault()
                                      ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("คำเตือน"))?.AsWindow(), 4000)
                          ?? throw new Exception("กด Delete แล้วไม่พบ Pop-up ยืนยัน");

                ClickDialogButton(dlg, btn);
                WaitForClosed(() => dlg);

                // ตรวจสถานะจริงจากตารางหลังตอบ Yes/No แทนการอาศัยเพียงการปิด Dialog
                RefreshGrid();
                bool exists = VerifyRecordInGrid(code, timeoutMs: 3000, refresh: false);

                if (expectDeleted && exists) throw new Exception($"'{code}' ยังค้างอยู่หลังลบ");
                if (!expectDeleted && !exists) throw new Exception($"'{code}' หายไปทั้งที่ไม่ควรถูกลบ");

                LogSuccess(testName, expectDeleted ? $"'{code}' ถูกลบสำเร็จ" : $"'{code}' ข้อมูลยังไม่ถูกลบ");
            }
            catch (Exception ex) { LogFailure(testName, $"ลบรหัส '{code}' ไม่สำเร็จ: {ex.Message}"); }
        }

        private bool TryOpenMenu(string stage, string searchKeyword)
        {
            try
            {
                // หา Search ด้วย AutomationId ก่อน แล้วจึงใช้ ControlType.Edit เป็นทางเลือก
                var searchBox = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("baritemSearch"))
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)), 4000)
                              ?? throw new Exception("ไม่พบช่องค้นหา baritemSearch");

                WaitUntil(() => { try { searchBox.Focus(); return true; } catch { return false; } }, 1000);

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
                                               ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("treeListColumn row 1"))
                                               ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("treeListColumn row 2")), 4000)
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
            // เปิดฟอร์มจากปุ่ม New และรอจนพบฟอร์มพร้อมช่องรหัส
            var newBtn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("New")), 3000)
                         ?? throw new Exception("หาปุ่ม New ไม่เจอ");
            newBtn.Click();

            var form = Retry(FindOpenForm, 8000) ?? throw new Exception("หาฟอร์มไม่เจอ");
            WaitUntil(() =>
            {
                var txt = form.FindFirstDescendant(cf => cf.ByAutomationId(FirstObjectID));
                return txt != null && !txt.Properties.IsOffscreen.ValueOrDefault;
            }, 4000);
            return form;
        }

        // ค้นหาฟอร์มแบบ Modal ของหน้าต่างหลักก่อน แล้วค่อยค้นจาก Desktop ด้วย FormId
        private Window? FindOpenForm() =>
            _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault() ??
            _automation.GetDesktop().FindFirstChild(cf => cf.ByAutomationId(FormId))?.AsWindow();

        // ใช้แท็บ General เมื่อพบ; คืนฟอร์มทั้งหน้าเป็น fallback เผื่อ AutomationId ของแท็บไม่มี
        private AutomationElement GetGeneralTab(Window form) =>
            Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(GeneralTabId)), 3000) ?? form;

        /* Method สำหรับกรอกข้อมูลแท็บ General ขององค์กร */
        private void FillData(AutomationElement tab, string code, string name, string nameEng,
            string seq, string taxBranch, string taxId, string date, string era)
        {
            SetText(tab, IdTxtOrgCode, code ?? "");
            SetText(tab, IdTxtOrgName, name ?? "");
            if (nameEng != null) SetText(tab, IdTxtOrgNameEng, nameEng);
            if (seq != null) SetText(tab, IdTxtOrgSeq, seq);
            if (taxBranch != null) SetText(tab, IdTxtTaxBranchNo, taxBranch);
            if (taxId != null) SetText(tab, IdTxtTaxId, taxId);
            if (date != null) SetText(tab, IdDateEstablished, date);
            if (!string.IsNullOrEmpty(era))
            {
                var combo = tab.FindFirstDescendant(cf => cf.ByAutomationId(IdCboEra))?.AsComboBox();
                combo?.Select(era);
            }
        }

        private void SetText(AutomationElement tab, string id, string text)
        {
            // รอช่องตาม AutomationId ก่อน เพื่อรองรับกรณีฟอร์มยังโหลดไม่เสร็จ
            var txt = Retry(() => tab.FindFirstDescendant(cf => cf.ByAutomationId(id))?.AsTextBox(), 3000)
                      ?? throw new Exception($"ไม่พบช่อง {id}");
            txt.Focus();
            txt.Text = string.Empty;
            if (!string.IsNullOrEmpty(text)) txt.Enter(text);
        }

        // ค้นหาค่าในตารางหลักจาก Name และ Value ขององค์ประกอบ
        private AutomationElement? FindGridElement(string val)
        {
            var grid = FindMainGrid();
            var searchContext = grid ?? _mainWindow;

            return searchContext.FindAllDescendants().FirstOrDefault(el =>
                (!string.IsNullOrEmpty(el.Name) && string.Equals(el.Name.Trim(), val, StringComparison.OrdinalIgnoreCase)) ||
                (el.Patterns.Value.IsSupported && string.Equals(el.Patterns.Value.Pattern.Value.Value?.Trim(), val, StringComparison.OrdinalIgnoreCase)));
        }

        private AutomationElement? FindMainGrid() =>
            _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataGrid))
            ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Table));

        private bool TrySaveAndHandleDuplicate(Window form, string testName, string code, string saveBtn)
        {
            // บันทึกข้อมูลก่อน แล้วตรวจ Task Result เฉพาะการเพิ่ม/แก้ไขปกติ
            var btn = form.FindFirstDescendant(cf => cf.ByName(saveBtn)) ?? throw new Exception($"ไม่พบปุ่ม {saveBtn}");
            btn.Click();

            if (testName.Contains("Validation")) return true;

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
            if (texts.Any(t => t.Contains("รหัสองค์กร ห้ามซ้ำ"))) return "รหัสองค์กรซ้ำ";
            if (texts.Any(t => t.Contains("ชื่อองค์กร ห้ามซ้ำ"))) return "ชื่อองค์กรซ้ำ";
            return null;
        }

        private void FillSaveAssert(Window form, string code, string name, string address, string saveBtn, string testName, params string[] msgs)
        {
            // ใช้ใน Validation: ตั้งค่ารหัส/ชื่อ/ที่อยู่ กดปุ่มบันทึก และตรวจว่าพบข้อความที่คาดหวังครบทุกข้อความ
            // หน้านี้มีหลายแท็บ จึงสลับไปแท็บ Address เพื่อกรอกที่อยู่ แล้วกลับมาแท็บ General ก่อนกดบันทึก
            SwitchToTab(form, GeneralTabId, "General");
            var tab = GetGeneralTab(form);
            SetText(tab, IdTxtOrgCode, code);
            SetText(tab, IdTxtOrgName, name);
            FillAddressTab(form, address);
            SwitchToTab(form, GeneralTabId, "General");

            form.FindFirstDescendant(cf => cf.ByName(saveBtn))?.Click();
            AssertTaskResult(form, msgs, testName);
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
            var form = Retry(FindOpenForm, 8000) ?? throw new Exception("หาฟอร์มไม่เจอ");
            WaitUntil(() =>
            {
                var txt = form.FindFirstDescendant(cf => cf.ByAutomationId(FirstObjectID))?.AsTextBox();
                return txt != null && !string.IsNullOrEmpty(txt.Text);
            }, 3000);
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
            var lastSeenTexts = new List<string>();
            var sw = Stopwatch.StartNew();

            // ตรวจซ้ำช่วงสั้น ๆ เพราะแผงผลลัพธ์อาจอัปเดตหลังคลิก Save เล็กน้อย
            while (sw.ElapsedMilliseconds < 5000 && pending.Count > 0)
            {
                var panel = FindTaskResultPanel(form, 500);
                if (panel != null)
                {
                    var texts = GetPanelTexts(panel).ToList();
                    lastSeenTexts = texts;
                    foreach (var m in pending.ToList())
                    {
                        string cleanM = m.Replace(" ", "");
                        if (texts.Any(t => t.Replace(" ", "").Contains(cleanM))) pending.Remove(m);
                    }
                }
                if (pending.Count == 0) break;
                Thread.Sleep(100);
            }

            if (pending.Count > 0)
            {
                string actualDump = lastSeenTexts.Count > 0 ? string.Join(" | ", lastSeenTexts.Distinct()) : "(ไม่มีข้อความ)";
                throw new Exception($"ไม่พบข้อความ: '{string.Join("', '", pending)}' | ข้อความจริง: [{actualDump}]");
            }

            string logMsg = customMsg ?? $"พบข้อความ: {string.Join(",", msgs.Select(m => $"'{m}'"))}";
            LogSuccess(testName, logMsg);
        }

        // ค้นหาแผง Task Result ในฟอร์มก่อน แล้วจึงค้นในหน้าต่างหลัก เผื่อแผงถูก Dock ไว้ภายนอกฟอร์ม
        private AutomationElement? FindTaskResultPanel(Window form, int timeoutMs) =>
            Retry(() => form.FindFirstDescendant(cf => cf.ByName("Task Result"))
                        ?? form.FindFirstDescendant(cf => cf.ByAutomationId("dockPanelTaskResult"))
                        ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("Task Result"))
                        ?? _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("dockPanelTaskResult")), timeoutMs);

        private static IEnumerable<string> GetPanelTexts(AutomationElement panel)
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
                var g = Retry(() => FindMainGrid(), 2000);
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

        // เก็บกวาด Dialog ที่ค้างอยู่ (คงไว้เป็น public เหมือนเดิม เผื่อมีการเรียกจากภายนอก)
        public void EnsureNoStrayModal(string contextName)
        {
            try
            {
                var modal = _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault()
                            ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("คำเตือน"))?.AsWindow();
                if (modal != null)
                {
                    var closeBtn = modal.FindFirstDescendant(cf => cf.ByName("Close") ?? cf.ByName("No") ?? cf.ByName("Cancel"));
                    closeBtn?.Click();
                    WaitForClosed(() => modal, 2000);
                }
            }
            catch { }
        }

        // ---------- แท็บ ----------
        private void SwitchToTab(Window form, string tabAutomationId, string tabName)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try { Keyboard.Type(VirtualKeyShort.ESCAPE); } catch { }

                var tab = Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(tabAutomationId))
                                       ?? form.FindFirstDescendant(cf => cf.ByName(tabName)), 2000);

                if (tab == null) continue;

                tab.Click();

                if (WaitUntil(() => IsTabSelected(tab), 1500)) return;
            }
        }

        private bool IsTabSelected(AutomationElement tab)
        {
            try
            {
                if (tab.Patterns.SelectionItem.IsSupported)
                    return tab.Patterns.SelectionItem.Pattern.IsSelected.Value;
            }
            catch { }
            return true;
        }

        // ---------- แท็บ Address ----------
        private void FillAddressTab(Window form, string address)
        {
            SwitchToTab(form, AddressTabId, "Address");

            var txtElement = Retry(() => FindAddressTextBoxRobust(form), 6000)
                             ?? throw new Exception("หาช่องกรอก 'ชื่อที่อยู่' (devtxtOrgAddressTh) ในแท็บ Address ไม่เจอ");

            SetTextRobust(txtElement, address);

            string expected = address ?? string.Empty;
            bool matched = WaitUntil(() =>
            {
                string? current = TryReadValue(txtElement);
                return string.Equals((current ?? string.Empty).Trim(), expected.Trim(), StringComparison.Ordinal);
            }, 1000);

            if (!matched)
            {
                SetTextRobust(txtElement, address ?? string.Empty, forceKeyboard: true);
                string? finalValue = TryReadValue(txtElement);

                if (!string.Equals((finalValue ?? string.Empty).Trim(), expected.Trim(), StringComparison.Ordinal))
                    throw new Exception($"ตั้งค่าช่อง 'ชื่อที่อยู่' ไม่สำเร็จ (ต้องการ: '{expected}', ได้จริง: '{finalValue}')");
            }
        }

        private AutomationElement? FindAddressTextBoxRobust(Window form)
        {
            try
            {
                var byId = form.FindFirstDescendant(cf => cf.ByAutomationId(IdTxtOrgAddressTh));
                if (byId != null && !byId.Properties.IsOffscreen.ValueOrDefault) return byId;
            }
            catch { }

            List<AutomationElement> allDescendants;
            try { allDescendants = form.FindAllDescendants().ToList(); }
            catch { return null; }

            foreach (var el in allDescendants)
            {
                string? autoId = null;
                try { autoId = el.Properties.AutomationId.ValueOrDefault; } catch { }
                if (!string.IsNullOrEmpty(autoId) && autoId.IndexOf("OrgAddressTh", StringComparison.OrdinalIgnoreCase) >= 0)
                    return el;
            }

            for (int i = 0; i < allDescendants.Count; i++)
            {
                string? name = null;
                try { name = allDescendants[i].Name; } catch { }
                if (!string.Equals(name, "ชื่อที่อยู่", StringComparison.Ordinal)) continue;

                for (int j = i + 1; j < Math.Min(i + 4, allDescendants.Count); j++)
                {
                    var candidate = allDescendants[j];
                    bool supportsValue = false;
                    try { supportsValue = candidate.Patterns.Value.IsSupported; } catch { }
                    if (supportsValue) return candidate;
                }
            }

            return null;
        }

        private void SetTextRobust(AutomationElement element, string? text, bool forceKeyboard = false)
        {
            string target = text ?? string.Empty;

            if (!forceKeyboard)
            {
                if (TrySetValueViaPattern(element, target)) return;
                if (TrySetValueViaLegacyPattern(element, target)) return;
                if (TrySetValueViaWinApi(element, target)) return;
            }

            try { element.Click(); } catch { }

            try
            {
                Keyboard.Press(VirtualKeyShort.CONTROL);
                Keyboard.Type(VirtualKeyShort.KEY_A);
                Keyboard.Release(VirtualKeyShort.CONTROL);
                Keyboard.Type(VirtualKeyShort.DELETE);

                if (!string.IsNullOrEmpty(target)) Keyboard.Type(target);
            }
            catch { }
        }

        private bool TrySetValueViaPattern(AutomationElement element, string text)
        {
            try
            {
                if (!element.Patterns.Value.IsSupported) return false;
                element.Patterns.Value.Pattern.SetValue(text ?? string.Empty);
                string? current = element.Patterns.Value.Pattern.Value.Value;
                return string.Equals((current ?? string.Empty).Trim(), (text ?? string.Empty).Trim(), StringComparison.Ordinal);
            }
            catch { return false; }
        }

        private bool TrySetValueViaLegacyPattern(AutomationElement element, string text)
        {
            try
            {
                if (!element.Patterns.LegacyIAccessible.IsSupported) return false;
                element.Patterns.LegacyIAccessible.Pattern.SetValue(text ?? string.Empty);
                string? current = TryReadValue(element);
                return string.Equals((current ?? string.Empty).Trim(), (text ?? string.Empty).Trim(), StringComparison.Ordinal);
            }
            catch { return false; }
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        private const int WM_SETTEXT = 0x000C;

        private bool TrySetValueViaWinApi(AutomationElement element, string text)
        {
            try
            {
                var handleProp = element.Properties.NativeWindowHandle;
                if (!handleProp.IsSupported) return false;
                IntPtr handle = handleProp.ValueOrDefault;
                if (handle == IntPtr.Zero) return false;

                SendMessage(handle, WM_SETTEXT, IntPtr.Zero, text ?? string.Empty);
                string? current = TryReadValue(element);
                return string.Equals((current ?? string.Empty).Trim(), (text ?? string.Empty).Trim(), StringComparison.Ordinal);
            }
            catch { return false; }
        }

        private string? TryReadValue(AutomationElement element)
        {
            try
            {
                if (element.Patterns.Value.IsSupported)
                    return element.Patterns.Value.Pattern.Value.Value;
            }
            catch { }

            try { return element.AsTextBox()?.Text; } catch { }
            return null;
        }

        // -------------------------------------------------------------
        // 2. Helper ทั่วไปที่ไม่ต้องแก้ไขคำสั่ง
        //    กลุ่มนี้อยู่ท้ายสุดเพื่อแยกจาก Helper ที่ควบคุมหน้าจอ
        // -------------------------------------------------------------
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
                Thread.Sleep(80);
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
                Thread.Sleep(100);
            }
            return null;
        }
    }
}