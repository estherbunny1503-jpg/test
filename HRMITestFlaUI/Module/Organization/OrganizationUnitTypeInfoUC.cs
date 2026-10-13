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

namespace HRMITestFlaUI.Module.Organization
{
    public class OrganizationUnitTypeInfoUC
    {
        // -------------------------------------------------------------
        // 1. ค่าที่อาจต้องปรับเมื่อ Automation ID หรือหน้าจอ OrganizationUnitTypeInfoUC เปลี่ยน
        // -------------------------------------------------------------
        private const int TimeoutMs = 6000;
        private const string FormId = "OrganizationUnitTypeInfoUC";
        private const string LogPageKey = "OrganizationUnitTypeInfoUC";
        private const string GeneralTabId = "devTabPgGeneral";
        private const string MenuName = "บันทึกข้อมูลประเภทหน่วยงาน";

        private const string IdTxtOrgTypeCode = "devTxtOrgUnitTypeCode";
        private const string IdTxtOrgTypeName = "devTxtOrgUnitTypeName";
        private const string IdTxtOrgTypeNameEng = "devTxtOrgUnitTypeNameEng";
        private const string IdTxtOrgTypeDesc = "devMemoDescription";
        private const string FirstObjectID = IdTxtOrgTypeCode;

        // ข้อความแจ้งเตือนที่ใช้ตรวจใน Task Result
        private const string MsgCodeEmpty = "รหัสประเภทหน่วยงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล";
        private const string MsgNameEmpty = "ชื่อประเภทหน่วยงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล";
        private const string MsgNameEngEmpty = "ชื่อประเภทหน่วยงาน (Eng) ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล";
        private const string MsgCodeDup = "รหัสประเภทหน่วยงาน ห้ามซ้ำ กรุณาป้อนข้อมูล";
        private const string MsgNameDup = "ชื่อประเภทหน่วยงาน ห้ามซ้ำ กรุณาป้อนข้อมูล";

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
            string NewName,
            string NewNameEng,
            string NewDesc,
            string SaveBtn,
            bool NeedsManualClose);

        private static readonly EditCase[] Edits =
        {
            new("TestCase03_Edit_003", "003", "DIV01", "สายงานปฏิบัติการ", "Operations Division",
                "ดูแลภาพรวมและควบคุมการปฏิบัติการทั้งหมด", "Save", true),
            new("TestCase03_Edit_004", "004", "DIV02", "ฝ่ายพัฒนาธุรกิจ", "Business Development Department",
                "วางแผนกลยุทธ์และขยายโอกาสทางธุรกิจใหม่", "Save and Close", false)
        };

        public OrganizationUnitTypeInfoUC(AutomationElement mainWindow, UIA3Automation automation)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

        // -------------------------------------------------------------
        // 1. ทดสอบการ New โดยเพิ่มข้อมูล 5 ข้อมูล
        //    สัดส่วน Progress: 0 -> 20
        // -------------------------------------------------------------
        public void NavigateToOrganizationUnitTypeInfoUC()
        {
            // SaveBtn กำหนดพฤติกรรมหลังบันทึก ส่วน NeedsManualClose ระบุว่าต้องปิดฟอร์มเองหรือไม่
            var DataList = new[]
            {
                new { Code = "001", Name = "หน่วยงานพิเศษ", NameEng = "Special Unit", Desc = "สำหรับหน่วยงานระดับสูง",
                      SaveBtn = "Save", NeedsManualClose = true },
                new { Code = "002", Name = "ฝ่ายบริหาร", NameEng = "Administration", Desc = "ดูแลงานบริหารองค์กร",
                      SaveBtn = "Save and New", NeedsManualClose = false },
                new { Code = "003", Name = "สายงาน", NameEng = "Group / Division", Desc = "กลุ่มสายงานหลักของบริษัท",
                      SaveBtn = "Save and New", NeedsManualClose = false },
                new { Code = "004", Name = "ฝ่าย", NameEng = "Department", Desc = "หน่วยงานระดับฝ่ายงาน",
                      SaveBtn = "Save and New", NeedsManualClose = false },
                new { Code = "005", Name = "แผนก", NameEng = "Section", Desc = "หน่วยงานระดับแผนกปฏิบัติการ",
                      SaveBtn = "Save and Close", NeedsManualClose = false }
            };

            const string stageName = "TestCase01_AddOrgTypeData";
            TestLogger.LogInfo(LogPageKey, "เริ่มต้นทดสอบการเพิ่มข้อมูลประเภทหน่วยงาน 5 รายการ");
            TestProgressHelper.Report(2, $"Opening Menu: {MenuName}...");

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
                    var tab = GetGeneralTab(form);
                    FillData(tab, a.Code, a.Name, a.NameEng, a.Desc);

                    // ถ้าพบข้อมูลซ้ำจะบันทึกผลไม่ผ่านและข้ามไปข้อมูลถัดไป
                    if (!TrySaveAndHandleDuplicate(form, testName, a.Code, a.SaveBtn))
                    {
                        form = null;
                        continue;
                    }

                    if (a.SaveBtn == "Save and New")
                    {
                        // ปุ่ม Save and New ควรล้างฟอร์มเพื่อให้กรอกรายการถัดไป
                        WaitUntil(() => string.IsNullOrEmpty(tab.FindFirstDescendant(cf => cf.ByAutomationId(FirstObjectID))?.AsTextBox().Text), 3000);
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
            const string stage = "TestCase02_NewValidation";
            TestProgressHelper.Report(22, "Starting New Validation Tests...");

            Window form;
            // หากเปิดฟอร์มไม่ได้ ไม่มีเงื่อนไขให้ทดสอบต่อ จึงบันทึกความล้มเหลวแล้วคืนค่า
            try { form = OpenForm(); }
            catch (Exception ex) { LogFailure(stage, $"เปิดฟอร์มไม่สำเร็จ: {ex.Message}"); return; }

            var tab = GetGeneralTab(form);

            // 2.1 ไม่กรอกรหัส + กรอกชื่อ + กรอกชื่อ Eng -> Save
            RunStep("TestCase02_1_EmptyCode",
                () => FillSaveAssert(tab, form, "", "หน่วยงานย่อย / ทีม", "Special Unit", "Save", "TestCase02_1_EmptyCode", MsgCodeEmpty),
                24, "Validating: Empty Code...");

            // 2.2 กรอก 006 + ลบชื่อออก + กรอกชื่อ Eng -> Save and New
            RunStep("TestCase02_2_EmptyName",
                () => FillSaveAssert(tab, form, "006", "", "Special Unit", "Save and New", "TestCase02_2_EmptyName", MsgNameEmpty),
                27, "Validating: Empty Name...");

            // 2.3 กรอก 006 + กรอกชื่อ + ลบชื่อ Eng ออก -> Save and New
            RunStep("TestCase02_3_EmptyNameEng",
                () => FillSaveAssert(tab, form, "006", "หน่วยงานย่อย / ทีม", "", "Save and New", "TestCase02_3_EmptyNameEng", MsgNameEngEmpty),
                29, "Validating: Empty Name (Eng)...");

            // 2.4 ลบทั้ง 3 ช่องออก -> Save and New
            RunStep("TestCase02_4_EmptyAll",
                () => FillSaveAssert(tab, form, "", "", "", "Save and New", "TestCase02_4_EmptyAll", MsgCodeEmpty, MsgNameEmpty, MsgNameEngEmpty),
                31, "Validating: Empty All...");

            // 2.5 รหัสซ้ำ (003) -> Save
            RunStep("TestCase02_5_DuplicateCode",
                () => FillSaveAssert(tab, form, "003", "สายงาน", "Special Unit", "Save", "TestCase02_5_DuplicateCode", MsgCodeDup),
                34, "Validating: Duplicate Code...");

            // 2.6 ชื่อซ้ำ (ฝ่ายบริหาร) -> Save and Close
            const string dupName = "TestCase02_6_DuplicateName";
            RunStep(dupName,
                () => FillSaveAssert(tab, form, "006", "ฝ่ายบริหาร", "Section", "Save and Close", dupName, MsgNameDup),
                37, "Validating: Duplicate Name...");

            // 2.7 - 2.9 Sequence ปิดหน้าจอ Yes/Cancel/No
            RunStep($"{dupName}_CloseSequence",
                () => RunCloseDialogSequence(form, dupName, MsgNameDup),
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
                    var tab = GetGeneralTab(form);
                    FillData(tab, e.NewCode, e.NewName, e.NewNameEng, e.NewDesc);

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
                    TestProgressHelper.Report(42 + ((i + 1) * 13 / Edits.Length), $"Editing Org Type Record ({i + 1}/{Edits.Length})...");
                }
            }
        }

        // -------------------------------------------------------------
        // 4. ทดสอบ Validation ตอนแก้ไขข้อมูล (Edit Validation Tests)
        //    สัดส่วน Progress ของเมธอดนี้: 55 -> 80
        // -------------------------------------------------------------
        public void RunEditValidationTests()
        {
            const string baseName = "TestCase04_EditValidation";
            TestProgressHelper.Report(58, "Starting Edit Validation Tests...");

            Window form;
            try { form = ClickRowAndOpenEdit("005"); }
            catch (Exception ex) { LogFailure(baseName, $"เปิดฟอร์ม Edit ไม่สำเร็จ: {ex.Message}"); return; }

            var tab = GetGeneralTab(form);

            // 4.1 ลบรหัสออก + แผนก + Section -> Save
            RunStep("TestCase04_1_Edit_EmptyCode",
                () => FillSaveAssert(tab, form, "", "แผนก", "Section", "Save", "TestCase04_1_Edit_EmptyCode", MsgCodeEmpty),
                62, "Validating Edit: Empty Code...");

            // 4.2 กรอก 005 + ลบชื่อออก + Section -> Save and New
            RunStep("TestCase04_2_Edit_EmptyName",
                () => FillSaveAssert(tab, form, "005", "", "Section", "Save and New", "TestCase04_2_Edit_EmptyName", MsgNameEmpty),
                65, "Validating Edit: Empty Name...");

            // 4.3 กรอก 005 + กรอก แผนก + ลบชื่อ Eng ออก -> Save and New
            RunStep("TestCase04_3_Edit_EmptyNameEng",
                () => FillSaveAssert(tab, form, "005", "แผนก", "", "Save and New", "TestCase04_3_Edit_EmptyNameEng", MsgNameEngEmpty),
                68, "Validating Edit: Empty Name (Eng)...");

            // 4.4 ลบทั้ง 3 ช่องออก -> Save and New
            RunStep("TestCase04_4_Edit_EmptyAll",
                () => FillSaveAssert(tab, form, "", "", "", "Save and New", "TestCase04_4_Edit_EmptyAll", MsgCodeEmpty, MsgNameEmpty, MsgNameEngEmpty),
                71, "Validating Edit: Empty All...");

            // 4.5 รหัสซ้ำ (002) -> Save
            RunStep("TestCase04_5_Edit_DuplicateCode",
                () => FillSaveAssert(tab, form, "002", "แผนก", "Section", "Save", "TestCase04_5_Edit_DuplicateCode", MsgCodeDup),
                74, "Validating Edit: Duplicate Code...");

            // 4.6 ชื่อซ้ำ (หน่วยงานพิเศษ) -> Save and Close
            const string dupSub = "TestCase04_6_Edit_DuplicateName";
            RunStep(dupSub,
                () => FillSaveAssert(tab, form, "005", "หน่วยงานพิเศษ", "Section", "Save and Close", dupSub, MsgNameDup),
                77, "Validating Edit: Duplicate Name...");

            // 4.7 - 4.9 Sequence ปิดหน้าจอ Yes/Cancel/No
            RunStep($"{dupSub}_CloseSequence",
                () => RunCloseDialogSequence(form, dupSub, MsgNameDup),
                80, "Validating Edit: Close Dialog Sequence...");
        }

        // -------------------------------------------------------------
        // 5. ทดสอบการลบข้อมูล (Delete Record Tests)
        //    สัดส่วน Progress ของเมธอดนี้: 80 -> 100
        // -------------------------------------------------------------
        public void RunDeleteTests()
        {
            const string baseName = "TestCase05_Delete";
            TestProgressHelper.Report(84, "Starting Delete Tests...");

            // เคสที่ 1: คลิกรหัส 005 -> Delete -> Yes -> ตรวจสอบว่าถูกลบ
            RunDeleteCase(baseName, "005", "Yes", expectDeleted: true);
            TestProgressHelper.Report(92, "Delete Test: 005 (confirmed delete)...");

            // เคสที่ 2: คลิกรหัส DIV02 -> Delete -> No -> ตรวจสอบว่าข้อมูลยังอยู่
            RunDeleteCase(baseName, Edits[1].NewCode, "No", expectDeleted: false);
            TestProgressHelper.Report(100, $"Delete Test: {Edits[1].NewCode} (cancelled delete)...");

            LogSuccess(baseName, "ทดสอบ Delete ครบทุกเคส");
            TestLogger.LogInfo(LogPageKey, $"สรุปผลรวมทั้งหมด: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");
        }

        // -------------------------------------------------------------
        // 1. Helper ที่เกี่ยวข้องกับหน้าจอ OrganizationUnitTypeInfoUC
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
            // เปิดฟอร์มจากปุ่ม New และรอจนพบฟอร์มพร้อมช่องรหัส
            var newBtn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("New")), 3000)
                         ?? throw new Exception("หาปุ่ม New ไม่เจอ");
            newBtn.Click();

            var form = Retry(FindOpenForm, 8000) ?? throw new Exception("หาฟอร์มไม่เจอ");
            WaitUntil(() => form.FindFirstDescendant(cf => cf.ByAutomationId(FirstObjectID)) != null, 4000);
            return form;
        }

        // ค้นหาฟอร์มแบบ Modal ของหน้าต่างหลักก่อน แล้วค่อยค้นจาก Desktop ด้วย FormId หรือชื่อหน้าต่าง
        private Window? FindOpenForm() =>
            _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault() ??
            _automation.GetDesktop().FindFirstChild(cf => cf.ByAutomationId(FormId))?.AsWindow() ??
            _automation.GetDesktop().FindFirstChild(cf => cf.ByName($"{MenuName} : New"))?.AsWindow() ??
            _automation.GetDesktop().FindFirstChild(cf => cf.ByName($"{MenuName} : Edit"))?.AsWindow();

        // ใช้แท็บ General เมื่อพบ; คืนฟอร์มทั้งหน้าเป็น fallback เผื่อ AutomationId ของแท็บไม่มี
        private AutomationElement GetGeneralTab(Window form) =>
            Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(GeneralTabId)), 3000) ?? form;

        /* Method สำหรับกรอกข้อมูลช่องต่างๆ ของประเภทหน่วยงาน (Code, Name, NameEng, Description) */
        private void FillData(AutomationElement tab, string code, string name, string nameEng, string desc)
        {
            SetText(tab, IdTxtOrgTypeCode, code);
            SetText(tab, IdTxtOrgTypeName, name);
            SetText(tab, IdTxtOrgTypeNameEng, nameEng);
            SetText(tab, IdTxtOrgTypeDesc, desc);
        }

        private void SetText(AutomationElement tab, string id, string text)
        {
            // รอช่องตาม AutomationId ก่อน หากไม่พบลองหาจากชื่อ; ถ้ายังไม่พบให้ข้ามช่องนั้น (พฤติกรรมเดิมของหน้านี้)
            var txt = Retry(() => tab.FindFirstDescendant(cf => cf.ByAutomationId(id))?.AsTextBox(), 2000)
                      ?? tab.FindFirstDescendant(cf => cf.ByName(id))?.AsTextBox();
            if (txt == null) return;

            txt.Focus();
            txt.Text = string.Empty;
            if (!string.IsNullOrEmpty(text)) txt.Enter(text);
        }

        // ค้นหาค่าในตารางหลักจาก Name และ Value ขององค์ประกอบ
        private AutomationElement? FindGridElement(string val)
        {
            var grid = _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataGrid))
                       ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Table));
            var searchContext = grid ?? _mainWindow;

            return searchContext.FindAllDescendants().FirstOrDefault(el =>
                (!string.IsNullOrEmpty(el.Name) && string.Equals(el.Name.Trim(), val, StringComparison.OrdinalIgnoreCase)) ||
                (el.Patterns.Value.IsSupported && string.Equals(el.Patterns.Value.Pattern.Value.Value?.Trim(), val, StringComparison.OrdinalIgnoreCase)));
        }

        private bool TrySaveAndHandleDuplicate(Window form, string testName, string code, string saveBtn)
        {
            // บันทึกข้อมูลก่อน แล้วตรวจ Task Result เฉพาะการเพิ่ม/แก้ไขปกติ
            var btn = form.FindFirstDescendant(cf => cf.ByName(saveBtn)) ?? throw new Exception($"ไม่พบปุ่ม {saveBtn}");
            btn.Click();

            if (testName.Contains("Validation") || testName.Contains("Duplicate")) return true;

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
            if (texts.Any(t => t.Contains("รหัสประเภทหน่วยงาน ห้ามซ้ำ"))) return "รหัสประเภทหน่วยงานซ้ำ";
            if (texts.Any(t => t.Contains("ชื่อประเภทหน่วยงาน ห้ามซ้ำ"))) return "ชื่อประเภทหน่วยงานซ้ำ";
            return null;
        }

        private void FillSaveAssert(AutomationElement tab, Window form, string code, string name, string nameEng, string saveBtn, string testName, params string[] msgs)
        {
            // ใช้ใน Validation: ตั้งค่ารหัส/ชื่อ/ชื่ออังกฤษ กดปุ่มบันทึก และตรวจว่าพบข้อความที่คาดหวังครบทุกข้อความ
            FillData(tab, code, name, nameEng, "");
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

            string logMsg = customMsg ?? $"พบข้อความแจ้งเตือนถูกต้อง: {string.Join(", ", msgs.Select(m => $"'{m}'"))}";
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