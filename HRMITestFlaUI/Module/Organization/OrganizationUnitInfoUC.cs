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
    public class OrganizationUnitInfoUC
    {
        // -------------------------------------------------------------
        // 1. ค่าที่อาจต้องปรับเมื่อ Automation ID หรือหน้าจอ OrganizationUnitInfoUC เปลี่ยน
        // -------------------------------------------------------------
        private const int TimeoutMs = 6000;
        private const string FormId = "OrganizationUnitInfoUC";
        private const string LogPageKey = "OrganizationUnitInfoUC";
        private const string GeneralTabId = "devTabPgGeneral";
        private const string MenuName = "บันทึกข้อมูลหน่วยงาน";

        private const string IdTxtOrgUnitCode = "devTxtOrgUnitCode";
        private const string IdTxtOrgUnitName = "devTxtOrgUnitName";
        private const string IdTxtOrgUnitNameEng = "devTxtOrgUnitNameEng";
        private const string IdBtnOrgUnitType = "devBtnOrgUnitType";
        private const string IdBtnParentOrgUnit = "devBtnParentOrgUnit";
        private const string IdMemoDescription = "devMemoDescription";
        private const string FirstObjectID = IdTxtOrgUnitCode;

        private const string LabelOrgUnitType = "ประเภทหน่วยงาน";
        private const string LabelParentOrgUnit = "หน่วยงานหลัก";

        // ข้อความแจ้งเตือนที่ใช้ตรวจใน Task Result
        private const string MsgCodeEmpty = "รหัสหน่วยงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล";
        private const string MsgNameEmpty = "ชื่อหน่วยงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล";
        private const string MsgTypeEmpty = "ประเภทหน่วยงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล";
        private const string MsgParentEmpty = "หน่วยงานหลัก ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล";
        private const string MsgDup = "ห้ามซ้ำ กรุณาป้อนข้อมูล";

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
            string SaveBtn,
            bool NeedsManualClose);

        private static readonly EditCase[] Edits =
        {
            new("TestCase03_EditData_ORG001_to_RP001", "ORG001", "RP001", "สำนักงานใหญ่มาก", "Save", true),
            new("TestCase03_EditData_ORG002_to_RP002", "ORG002", "RP002", "ฝ่ายทรัพยากรบุคคลพิเศษ", "Save and Close", false)
        };

        public OrganizationUnitInfoUC(AutomationElement mainWindow, UIA3Automation automation)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

        // -------------------------------------------------------------
        // 1. ทดสอบการ New โดยเพิ่มข้อมูลหน่วยงาน
        //    สัดส่วน Progress: 0 -> 20
        // -------------------------------------------------------------
        public void NavigateToOrganizationUnitInfoUC()
        {
            // SaveBtn กำหนดพฤติกรรมหลังบันทึก ส่วน NeedsManualClose ระบุว่าต้องปิดฟอร์มเองหรือไม่
            var DataList = new[]
            {
                new { Code = "ORG001", Name = "สำนักงานใหญ่", NameEng = "Head Office", OrgUnitType = "001", ParentOrgUnit = "", Description = "หน่วยงานหลัก",
                      SaveBtn = "Save", NeedsManualClose = true },
                new { Code = "ORG002", Name = "ฝ่ายทรัพยากรบุคคล", NameEng = "Human Resources", OrgUnitType = "002", ParentOrgUnit = "001", Description = "ดูแลบุคลากร",
                      SaveBtn = "Save and New", NeedsManualClose = false },
                new { Code = "ORG003", Name = "ฝ่ายบัญชีและการเงิน", NameEng = "Accounting & Finance", OrgUnitType = "001", ParentOrgUnit = "", Description = "ดูแลบัญชีและการเงิน",
                      SaveBtn = "Save and New", NeedsManualClose = false },
                new { Code = "ORG004", Name = "ฝ่ายเทคโนโลยีสารสนเทศ", NameEng = "Information Technology", OrgUnitType = "002", ParentOrgUnit = "001", Description = "ดูแลระบบ IT",
                      SaveBtn = "Save and New", NeedsManualClose = false },
                new { Code = "ORG005", Name = "ฝ่ายจัดซื้อ", NameEng = "Procurement", OrgUnitType = "001", ParentOrgUnit = "", Description = "จัดซื้อวัสดุและอุปกรณ์",
                      SaveBtn = "Save and Close", NeedsManualClose = false }
            };

            const string stageName = "TestCase00_AddOrgUnitData";
            TestLogger.LogInfo(LogPageKey, "เริ่มทดสอบหน้าจอบันทึกข้อมูลหน่วยงาน");
            TestProgressHelper.Report(2, "Opening Organization -> Org Unit Menu via Search...");

            // เปิดเมนูด้วยช่องค้นหาของโปรแกรม หากเปิดไม่สำเร็จให้จบขั้นตอนนี้
            if (!TryOpenMenu(stageName, MenuName)) return;

            Window? form = null;
            for (int i = 0; i < DataList.Length; i++)
            {
                var a = DataList[i];
                string testName = $"{stageName}_{a.Code}";
                TestProgressHelper.Report(4 + (i * 16 / DataList.Length), $"Adding {MenuName} ({i + 1}/{DataList.Length}): {a.Code} - {a.Name}...");

                try
                {
                    // ใช้ฟอร์มเดิมต่อเมื่อยังเปิดอยู่ เช่น หลัง Save and New
                    form ??= GetOrOpenForm();
                    var tab = GetGeneralTab(form);
                    FillData(tab, a.Code, a.Name, a.NameEng, a.OrgUnitType, a.ParentOrgUnit, a.Description);

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
                        if (a.NeedsManualClose) SafeCloseForm(form);
                        WaitForClosed(() => FindOpenForm());
                        form = null;
                        LogGridResult(testName, a.Code);
                    }
                }
                catch (Exception ex)
                {
                    // ความผิดพลาดของรายการหนึ่งไม่หยุดการสร้างรายการอื่น
                    LogFailure(testName, $"'{a.Code}' เพิ่มไม่สำเร็จ: {ex.Message}");
                    if (form != null && form.IsAvailable) SafeCloseForm(form);
                    form = null;
                }
            }

            // เก็บกวาด Dialog ที่ค้าง และตรวจรายการที่บันทึกด้วย Save and New ว่าอยู่ในตารางหลัก
            EnsureNoStrayModal($"{stageName}_Cleanup");
            RefreshGrid();
            foreach (var a in DataList.Where(x => x.SaveBtn == "Save and New"))
                LogGridResult($"{stageName}_Verify_{a.Code}", a.Code);
        }

        // -------------------------------------------------------------
        // 2. ทดสอบ แจ้งเตือนต่างๆ เมื่อ New
        //    สัดส่วน Progress: 20 -> 40
        // -------------------------------------------------------------
        public void RunAllValidationTests()
        {
            const string stage = "TestCase01-05_Validation";
            TestProgressHelper.Report(22, "Opening Org Unit Form for Validation Tests...");
            EnsureNoStrayModal($"{stage}_PreCheck");

            Window form;
            // หากเปิดฟอร์มไม่ได้ ไม่มีเงื่อนไขให้ทดสอบต่อ จึงบันทึกความล้มเหลวแล้วคืนค่า
            try { form = OpenForm(); }
            catch (Exception ex) { LogFailure(stage, $"เปิดฟอร์มไม่สำเร็จ: {ex.Message}"); return; }

            var tab = GetGeneralTab(form);

            // ข้อที่ 1: ไม่กรอกรหัส, กรอกชื่อ, เลือกประเภท -> Save
            RunStep("TestCase01_MissingCode",
                () =>
                {
                    SelectLookupValue(tab, IdBtnOrgUnitType, "001", LabelOrgUnitType);
                    FillSaveAssertAndOpen(tab, form, "", "สำนักงานใหญ่", "Save", "TestCase01_MissingCode", MsgCodeEmpty);
                },
                25, "Validating: Missing Code...");

            // ข้อที่ 2: กรอกรหัส ORG001, ลบชื่อ, เลือกประเภทและหน่วยงานหลัก -> Save and New
            RunStep("TestCase02_MissingName",
                () =>
                {
                    SelectLookupValue(tab, IdBtnOrgUnitType, "002", LabelOrgUnitType);
                    SelectLookupValue(tab, IdBtnParentOrgUnit, "001", LabelParentOrgUnit);
                    FillSaveAssertAndOpen(tab, form, "ORG001", "", "Save and New", "TestCase02_MissingName", MsgNameEmpty);
                },
                28, "Validating: Missing Name...");

            // ข้อที่ 3: ลบรหัส, ลบชื่อ, ลบประเภท -> Save and New
            RunStep("TestCase03_MissingAllFields",
                () =>
                {
                    ClearLookupValue(tab, IdBtnOrgUnitType);
                    FillSaveAssertAndOpen(tab, form, "", "", "Save and New", "TestCase03_MissingAllFields", MsgCodeEmpty, MsgNameEmpty, MsgTypeEmpty);
                },
                31, "Validating: Missing All Fields...");

            // ข้อที่ 4: รหัส ORG006, ชื่อ, ประเภท แต่ลบหน่วยงานหลัก -> Save and New
            RunStep("TestCase04_MissingParentOrg",
                () =>
                {
                    SelectLookupValue(tab, IdBtnOrgUnitType, "002", LabelOrgUnitType);
                    ClearLookupValue(tab, IdBtnParentOrgUnit);
                    FillSaveAssertAndOpen(tab, form, "ORG006", "สำนักงาน", "Save and New", "TestCase04_MissingParentOrg", MsgParentEmpty);
                },
                34, "Validating: Missing Parent Org...");

            // ข้อที่ 5: รหัสและชื่อซ้ำ ORG005 -> Save and New
            const string dupName = "TestCase05_DuplicateCodeAndName";
            RunStep(dupName,
                () =>
                {
                    SelectLookupValue(tab, IdBtnOrgUnitType, "001", LabelOrgUnitType);
                    FillSaveAssertAndOpen(tab, form, "ORG005", "ฝ่ายจัดซื้อ", "Save and New", dupName, MsgDup);
                },
                37, "Validating: Duplicate Code and Name...");

            // Sequence ปิดหน้าจอ Yes/Cancel/No
            RunStep($"{dupName}_CloseSequence",
                () => RunCloseDialogSequence(form, dupName, MsgDup),
                40, "Validating: Close Dialog Sequence...");
        }

        // -------------------------------------------------------------
        // 3. ทดสอบการแก้ไขข้อมูล (Edit Record Tests)
        //    สัดส่วน Progress ของเมธอดนี้: 40 -> 55
        // -------------------------------------------------------------
        public void RunEditTests()
        {
            TestProgressHelper.Report(42, "Starting Edit Operations...");
            EnsureNoStrayModal("TestCase03_EditData_PreCheck");

            for (int i = 0; i < Edits.Length; i++)
            {
                var e = Edits[i];
                try
                {
                    // เปิดรายการเดิม แก้ข้อมูล และบันทึกด้วยปุ่มที่กำหนดสำหรับแต่ละเคส
                    var form = ClickRowAndOpenEdit(e.OldCode);
                    var tab = GetGeneralTab(form);
                    SetText(tab, IdTxtOrgUnitCode, e.NewCode);
                    SetText(tab, IdTxtOrgUnitName, e.NewName);

                    if (!TrySaveAndHandleDuplicate(form, e.Name, e.NewCode, e.SaveBtn)) continue;
                    if (e.NeedsManualClose) SafeCloseForm(form);

                    WaitForClosed(() => FindOpenForm());
                    RefreshGrid();

                    if (VerifyRecordInGrid(e.NewCode, refresh: false))
                        LogSuccess(e.Name, $"แก้ไข '{e.OldCode}' เป็น '{e.NewCode}' สำเร็จ (พบในตารางหลัก)");
                    else
                        LogFailure(e.Name, $"แก้ไข '{e.OldCode}' แล้ว แต่ไม่พบ '{e.NewCode}' ในตารางหลัก");
                }
                catch (Exception ex) { LogFailure(e.Name, $"แก้ไข '{e.OldCode}' ไม่สำเร็จ: {ex.Message}"); }
                finally
                {
                    // รายงานความคืบหน้าแม้เคสปัจจุบันจะเกิดข้อผิดพลาด
                    TestProgressHelper.Report(42 + ((i + 1) * 13 / Edits.Length), $"Editing Org Unit Record ({i + 1}/{Edits.Length})...");
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
            TestProgressHelper.Report(58, "Opening Edit Form for Validation Tests...");
            EnsureNoStrayModal($"{baseName}_PreCheck");

            Window form;
            try { form = ClickRowAndOpenEdit(Edits[0].NewCode); }
            catch (Exception ex) { LogFailure(baseName, $"เปิดฟอร์ม Edit ไม่สำเร็จ: {ex.Message}"); return; }

            var tab = GetGeneralTab(form);

            RunStep($"{baseName}_MissingCode",
                () => FillSaveAssert(tab, form, "", "สำนักงานย่อย", "Save", $"{baseName}_MissingCode", MsgCodeEmpty),
                64, "Validating Edit: Missing Code...");

            RunStep($"{baseName}_MissingName",
                () => FillSaveAssert(tab, form, "ORG003", "", "Save", $"{baseName}_MissingName", MsgNameEmpty),
                68, "Validating Edit: Missing Name...");

            RunStep($"{baseName}_MissingBoth",
                () => FillSaveAssert(tab, form, "", "", "Save", $"{baseName}_MissingBoth", MsgCodeEmpty, MsgNameEmpty),
                72, "Validating Edit: Missing Both...");

            const string dupSub = $"{baseName}_Duplicate";
            RunStep(dupSub,
                () => FillSaveAssert(tab, form, "ORG003", "ฝ่ายบัญชีและการเงิน", "Save", dupSub, MsgDup),
                76, "Validating Edit: Duplicate...");

            RunStep($"{dupSub}_CloseSequence",
                () => RunCloseDialogSequence(form, dupSub, MsgDup),
                80, "Validating Edit: Close Dialog Sequence...");
        }

        // -------------------------------------------------------------
        // 5. ทดสอบการลบข้อมูล (Delete Record Tests)
        //    สัดส่วน Progress ของเมธอดนี้: 80 -> 100
        // -------------------------------------------------------------
        public void RunDeleteTests()
        {
            const string baseName = "TestCase05_DeleteData";
            TestProgressHelper.Report(84, "Starting Delete Tests...");

            // เคสที่ 1: คลิกรหัส RP002 -> Delete -> Yes -> ตรวจสอบว่าถูกลบ
            RunDeleteCase(baseName, Edits[1].NewCode, "Yes", expectDeleted: true);
            TestProgressHelper.Report(92, $"Delete Test: {Edits[1].NewCode} (confirmed delete)...");

            // เคสที่ 2: คลิกรหัส RP001 -> Delete -> No -> ตรวจสอบว่าข้อมูลยังอยู่
            RunDeleteCase(baseName, Edits[0].NewCode, "No", expectDeleted: false);
            TestProgressHelper.Report(100, $"Delete Test: {Edits[0].NewCode} (cancelled delete)...");

            LogSuccess(baseName, "ทดสอบ Delete ครบทุกเคส");
            TestLogger.LogInfo(LogPageKey, $"สรุปผลรวมทั้งหมด: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");
        }

        // -------------------------------------------------------------
        // 1. Helper ที่เกี่ยวข้องกับหน้าจอ OrganizationUnitInfoUC
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
            if (!SafeClick(_mainWindow, "New", 3000)) throw new Exception("หาปุ่ม New ไม่เจอ");

            var form = Retry(FindOpenForm, 8000) ?? throw new Exception("หาฟอร์มไม่เจอ");
            WaitUntil(() => form.FindFirstDescendant(cf => cf.ByAutomationId(FirstObjectID)) != null, 4000);
            return form;
        }

        // ค้นหาฟอร์มแบบ Modal ของหน้าต่างหลักก่อน แล้วค่อยค้นจาก Desktop ด้วย FormId
        private Window? FindOpenForm() =>
            _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault() ??
            _automation.GetDesktop().FindFirstChild(cf => cf.ByAutomationId(FormId))?.AsWindow();

        // ใช้แท็บ General เมื่อพบ; คืนฟอร์มทั้งหน้าเป็น fallback เผื่อ AutomationId ของแท็บไม่มี
        private AutomationElement GetGeneralTab(Window form) =>
            Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(GeneralTabId)), 3000) ?? form;

        /* Method สำหรับกรอกข้อมูลช่องต่างๆ ของหน่วยงาน (Code, Name, NameEng, Type, Parent, Description) */
        private void FillData(AutomationElement tab, string code, string name, string nameEng, string orgUnitType, string parentOrgUnit, string description)
        {
            SetText(tab, IdTxtOrgUnitCode, code);
            SetText(tab, IdTxtOrgUnitName, name);
            SetText(tab, IdTxtOrgUnitNameEng, nameEng);

            if (!string.IsNullOrEmpty(orgUnitType))
                SelectLookupValue(tab, IdBtnOrgUnitType, orgUnitType, LabelOrgUnitType);

            if (!string.IsNullOrEmpty(parentOrgUnit))
                SelectLookupValue(tab, IdBtnParentOrgUnit, parentOrgUnit, LabelParentOrgUnit);

            SetText(tab, IdMemoDescription, description);
        }

        private void SetText(AutomationElement tab, string id, string text)
        {
            // รอช่องตาม AutomationId ก่อน เพื่อรองรับกรณีฟอร์มยังโหลดไม่เสร็จ
            var element = Retry(() => tab.FindFirstDescendant(cf => cf.ByAutomationId(id)), 4000)
                          ?? throw new Exception($"ไม่พบช่อง {id}");

            element.Focus();
            element.Click();
            Thread.Sleep(100);

            // เลือกข้อความเดิมและล้างช่องก่อนพิมพ์ ป้องกันการต่อท้ายค่าที่กรอกไว้ก่อนหน้า
            Keyboard.Press(VirtualKeyShort.CONTROL);
            Keyboard.Type(VirtualKeyShort.KEY_A);
            Keyboard.Release(VirtualKeyShort.CONTROL);
            Keyboard.Type(VirtualKeyShort.DELETE);
            Thread.Sleep(50);

            if (!string.IsNullOrEmpty(text))
            {
                // Value Pattern เร็วกว่า; หากใช้ไม่ได้จะพิมพ์ผ่าน Keyboard แทน
                if (element.Patterns.Value.IsSupported)
                {
                    try { element.Patterns.Value.Pattern.SetValue(text); }
                    catch { Keyboard.Type(text); }
                }
                else Keyboard.Type(text);
            }

            // TAB เพื่อ Commit ค่าออกจากช่อง
            Keyboard.Press(VirtualKeyShort.TAB);
            Thread.Sleep(100);
        }

        // ค้นหาแถวในตารางหลักจากรหัส (Name/Value ของแถวหรือเซลล์ลูก)
        private AutomationElement? FindGridElement(string val) => FindGridRowByCodeInContainer(_mainWindow, val);

        private AutomationElement? FindGridRowByCodeInContainer(AutomationElement container, string code)
        {
            return Retry(() =>
            {
                var rows = container.FindAllDescendants(cf =>
                    cf.ByControlType(ControlType.DataItem)
                    .Or(cf.ByControlType(ControlType.ListItem))
                    .Or(cf.ByControlType(ControlType.Custom)));

                foreach (var r in rows)
                {
                    string name = r.Name ?? "";
                    string val = "";
                    try { if (r.Patterns.Value.IsSupported) val = r.Patterns.Value.Pattern.Value.ValueOrDefault ?? ""; } catch { }

                    if (name.Contains(code) || val.Contains(code)) return r;

                    var textChild = r.FindFirstDescendant(cf => cf.ByName(code));
                    if (textChild != null) return r;
                }

                var directMatch = container.FindFirstDescendant(cf => cf.ByName(code));
                return directMatch?.Parent ?? directMatch;
            }, timeoutMs: 4000);
        }

        private bool TrySaveAndHandleDuplicate(Window form, string testName, string code, string saveBtn)
        {
            // บันทึกข้อมูลก่อน แล้วตรวจ Task Result เฉพาะการเพิ่ม/แก้ไขปกติ
            if (!SafeClick(form, saveBtn)) throw new Exception($"ไม่พบปุ่ม {saveBtn}");

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
            if (texts.Any(t => t.Contains("ห้ามซ้ำ"))) return "รหัสหรือข้อมูลซ้ำ";
            return null;
        }

        private void FillSaveAssert(AutomationElement tab, Window form, string code, string name, string saveBtn, string testName, params string[] msgs)
        {
            // ใช้ใน Validation: ตั้งค่ารหัส/ชื่อ กดปุ่มบันทึก และตรวจว่าพบข้อความที่คาดหวังครบทุกข้อความ
            try { form.Focus(); } catch { }
            SetText(tab, IdTxtOrgUnitCode, code);
            SetText(tab, IdTxtOrgUnitName, name);

            // โหมด Edit อาจไม่มีปุ่ม Save and New จึงถอยไปใช้ Save
            if (!SafeClick(form, saveBtn)) SafeClick(form, "Save");
            AssertTaskResult(form, msgs, testName);
        }

        // เหมือน FillSaveAssert และยืนยันเพิ่มว่าฟอร์มยังเปิดอยู่ (Validation ที่ไม่ผ่านต้องไม่ปิดฟอร์ม)
        private void FillSaveAssertAndOpen(AutomationElement tab, Window form, string code, string name, string saveBtn, string testName, params string[] msgs)
        {
            FillSaveAssert(tab, form, code, name, saveBtn, testName, msgs);
            AssertFormOpen(form, testName);
        }

        private Window ClickRowAndOpenEdit(string code)
        {
            RefreshGrid();

            // ค้นหาแถวที่ตรงกับรหัสโดยเฉพาะ เพื่อป้องกันการคลิกโดนแถวแรกค้าง
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
            try { form.Focus(); } catch { }
            if (!SafeClick(form, "Close")) throw new Exception("ไม่พบปุ่ม Close");

            var dlg = FindConfirmDialog(form, 4000) ?? throw new Exception("ไม่พบ Pop-up ยืนยัน");
            ClickDialogButton(dlg, btnName);
            WaitForClosed(() => dlg);
        }

        private Window? FindConfirmDialog(Window form, int timeoutMs) =>
            Retry(() => form.ModalWindows.FirstOrDefault()
                        ?? _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault()
                        ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("คำเตือน"))?.AsWindow()
                        ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("Question"))?.AsWindow(), timeoutMs);

        private void ClickDialogButton(AutomationElement dialog, string buttonName)
        {
            // รองรับทั้งชื่อปุ่มปกติและชื่อที่มี access key นำหน้าด้วย &
            var btn = dialog.FindFirstDescendant(cf => cf.ByName(buttonName))
                   ?? dialog.FindFirstDescendant(cf => cf.ByName($"&{buttonName}"))
                   ?? dialog.FindFirstDescendant(cf => cf.ByName($"button '&{buttonName}'"))
                   ?? dialog.FindFirstDescendant(cf => cf.ByAutomationId($"btn{buttonName}"));

            if (btn == null)
            {
                var allButtons = dialog.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
                btn = allButtons.FirstOrDefault(b => b.Name != null && b.Name.Contains(buttonName, StringComparison.OrdinalIgnoreCase));
            }

            if (btn != null)
            {
                btn.Click();
            }
            else
            {
                // ไม่พบปุ่ม: ใช้ Alt + ตัวอักษรแรกของปุ่มเป็นทางเลือกสุดท้าย
                char shortcutChar = buttonName.Trim().ToUpper()[0];
                Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, (VirtualKeyShort)Enum.Parse(typeof(VirtualKeyShort), $"KEY_{shortcutChar}"));
            }
        }

        // ปิดฟอร์มด้วยปุ่ม Close; หากมี Dialog ถามยืนยันให้ตอบ No
        private void SafeCloseForm(Window form)
        {
            SafeClick(form, "Close");
            var dialog = FindConfirmDialog(form, 1500);

            if (dialog != null)
            {
                try { ClickDialogButton(dialog, "No"); }
                catch { _ = SafeClick(dialog, "No") || SafeClick(dialog, "&No") || SafeClick(dialog, "Cancel"); }
                WaitForClosed(() => dialog);
            }

            WaitForClosed(() => FindOpenForm());
        }

        // เก็บกวาด Dialog ที่ค้างอยู่ก่อน/หลังชุดทดสอบ
        public void EnsureNoStrayModal(string context)
        {
            try
            {
                var modals = _mainWindow.AsWindow()?.ModalWindows?.ToList() ?? new List<Window>();
                foreach (var modal in modals)
                {
                    string autoId = modal.Properties.AutomationId.ValueOrDefault ?? "";
                    string name = modal.Name ?? "";

                    if (autoId == FormId || name.Contains(MenuName) || name.Contains("Prosoft HRMI")) continue;

                    if (name.Contains("คำเตือน") || name.Contains("Question"))
                    {
                        try { ClickDialogButton(modal, "No"); }
                        catch { _ = SafeClick(modal, "No") || SafeClick(modal, "&No") || SafeClick(modal, "Cancel"); }
                        WaitForClosed(() => modal, 2000);
                    }
                    else if (name.Contains("Task Result") || autoId.Contains("TaskResult"))
                    {
                        _ = SafeClick(modal, "Close") || SafeClick(modal, "OK");
                        WaitForClosed(() => modal, 2000);
                    }
                }
            }
            catch (Exception ex) { TestLogger.LogInfo(context, $"EnsureNoStrayModal: {ex.Message}"); }
        }

        // เลือกค่าจาก Lookup (ปุ่มเปิด Pop-up) ตามรหัสที่ต้องการ
        private void SelectLookupValue(AutomationElement context, string buttonAutomationId, string targetCode, string fieldLabel)
        {
            if (string.IsNullOrEmpty(targetCode)) return;

            var targetControl = context.FindFirstDescendant(cf => cf.ByAutomationId(buttonAutomationId))
                ?? context.FindFirstDescendant(cf => cf.ByName(fieldLabel))
                ?? throw new Exception($"หาฟิลด์ '{fieldLabel}' ไม่เจอ");

            targetControl.Focus();

            var innerBtn = targetControl.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button));
            if (innerBtn != null) innerBtn.Click();
            else
            {
                targetControl.Click();
                Keyboard.Type(VirtualKeyShort.F4);
            }

            if (!WaitUntil(IsLookupDialogOpen, 2000))
            {
                targetControl.Click();
                Keyboard.Type(VirtualKeyShort.F4);
            }

            var lookupDialog = Retry(() =>
            {
                var modal = _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault();
                if (modal != null) return modal;

                return _automation.GetDesktop().FindAllChildren(cf => cf.ByControlType(ControlType.Window))
                    .FirstOrDefault(w => !string.IsNullOrEmpty(w.Name) &&
                                         (w.Name.Contains("ข้อมูลประเภทหน่วยงาน") ||
                                          w.Name.Contains("ข้อมูลหน่วยงาน") ||
                                          w.Name.Contains("ข้อมูล")))?.AsWindow();
            }, 4000) ?? throw new Exception($"ไม่พบ Pop-up Dialog ของ '{fieldLabel}'");

            lookupDialog.Focus();

            var row = FindGridRowByCodeInContainer(lookupDialog, targetCode);
            if (row != null)
            {
                row.Click();

                var selectButton = Retry(() =>
                    lookupDialog.FindFirstDescendant(cf => cf.ByName("button 'Select'"))
                    ?? lookupDialog.FindFirstDescendant(cf => cf.ByName("Select"))
                    ?? lookupDialog.FindFirstDescendant(cf => cf.ByName("button 'ตกลง'"))
                    ?? lookupDialog.FindFirstDescendant(cf => cf.ByName("ตกลง"))
                    ?? lookupDialog.FindFirstDescendant(cf => cf.ByAutomationId("btnSelect")), 2000);

                if (selectButton != null) selectButton.Click();
                else row.DoubleClick();

                WaitForClosed(() => lookupDialog, 3000);
            }
        }

        private bool IsLookupDialogOpen()
        {
            var modal = _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault();
            if (modal != null) return true;

            var desktopWin = _automation.GetDesktop().FindAllChildren(cf => cf.ByControlType(ControlType.Window))
                .FirstOrDefault(w => !string.IsNullOrEmpty(w.Name) &&
                                     (w.Name.Contains("ข้อมูลประเภทหน่วยงาน") || w.Name.Contains("ข้อมูลหน่วยงาน")));
            return desktopWin != null;
        }

        private void ClearLookupValue(AutomationElement context, string buttonAutomationId)
        {
            var targetControl = context.FindFirstDescendant(cf => cf.ByAutomationId(buttonAutomationId));
            if (targetControl == null) return;

            targetControl.Focus();
            Keyboard.Press(VirtualKeyShort.CONTROL);
            Keyboard.Type(VirtualKeyShort.KEY_A);
            Keyboard.Release(VirtualKeyShort.CONTROL);
            Keyboard.Type(VirtualKeyShort.DELETE);

            if (targetControl.Patterns.Value.IsSupported)
            {
                try { targetControl.Patterns.Value.Pattern.SetValue(string.Empty); } catch { }
            }
        }

        private bool SafeClick(AutomationElement scope, string buttonName, int timeoutMs = 6000)
        {
            var btn = Retry(() => scope.FindFirstDescendant(cf => cf.ByName(buttonName))
                                  ?? scope.FindFirstDescendant(cf => cf.ByName($"&{buttonName}"))
                                  ?? scope.FindFirstDescendant(cf => cf.ByAutomationId(buttonName)), timeoutMs);
            if (btn == null) return false;

            try
            {
                if (btn.Patterns.Invoke.IsSupported)
                {
                    btn.Patterns.Invoke.Pattern.Invoke();
                    return true;
                }
                btn.Click();
                return true;
            }
            catch { return false; }
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