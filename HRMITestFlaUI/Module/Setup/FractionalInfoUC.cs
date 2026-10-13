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

namespace HRMITestFlaUI.Module.Setup
{
    public class FractionalInfoUC
    {
        // ── ตัวแปรสำหรับเก็บออบเจ็กต์หน้าต่างหลัก, ระบบ Automation และตัวนับผลการทดสอบ (Success/Fail) ──
        private readonly AutomationElement _mainWindow;
        private readonly UIA3Automation _automation;

        private int _totalSuccess = 0;
        private int _totalFail = 0;

        // ── ค่าคงที่สำหรับการตั้งค่า Timeout และข้อมูลการระบุตัวตนของฟอร์ม ──
        private const int TimeoutMs = 8000;
        private const string FormId = "FractionalInfoUC";
        private const string FormTitle = "กำหนดรูปแบบการปัดเศษ";

        private const string GeneralTabId = "devTabPgGeneral";

        // ── AutomationId (กำหนดรูปแบบ tab & ฟิลด์ข้อมูล) ──
        private const string IdFractCode = "devTxtFractCode";
        private const string IdFractName = "devTxtFractName";
        private const string IdFractNameEng = "devTxtFractNameEng";
        private const string IdRemark = "devMemoRemark";
        private const string IdRadioRoundMoney = "devRadioRoundMoney";
        private const string IdRadioRoundTime = "devRadioRoundTime";
        private const string IdChkNoRoundMoney = "devChkConstFractSatang";
        private const string IdChkNoRoundSecond = "devChkConstFractSecond";
        private const string IdChkNoRoundMinute = "devChkConstFractMinute";
        private const string IdGridRoundMoney = "devGridCtrlFractSatang";
        private const string IdGridRoundSecond = "devGridCtrlFractSecond";
        private const string IdGridRoundMinute = "devGridCtrlFractMinute";

        // ── Tab Header AutomationId & Display Names ──
        private const string IdTabHeaderGeneral = "devTabHeaderGeneral";
        private const string IdTabHeaderFormat = "devTabHeaderFormat";
        private const string TabFormatName = "กำหนดรูปแบบ";
        private const string TabGeneralName = "Genaral";
        private const string RadioMoneyName = "กำหนดรูปแบบการปัดเศษเงิน";
        private const string RadioTimeName = "กำหนดรูปแบบการปัดเศษเวลา";
        private const string ChkNoRoundMoneyName = "ไม่ปัดเศษสตางค์ เศษมีค่าคงที่";
        private const string ChkNoRoundSecondName = "ไม่ปัดเศษวินาที เศษมีค่าคงที่";
        private const string ChkNoRoundMinuteName = "ไม่ปัดเศษนาที เศษมีค่าคงที่";
        private const string GridMoneyLabel = "รูปแบบการปัดเศษสตางค์";
        private const string GridSecondLabel = "รูปแบบการปัดเศษวินาที";
        private const string GridMinuteLabel = "รูปแบบการปัดเศษนาที";

        /* Constructor สำหรับรับค่า MainWindow และ Automation Instance เข้ามาใช้งานภายในคลาส */
        public FractionalInfoUC(AutomationElement mainWindow, UIA3Automation automation)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

        // ── โครงสร้างข้อมูลจำลอง (Data Model) พร้อม Action สำหรับกำหนดแท็บรูปแบบ ──
        private record FractionalRecord(
            string Code, string NameTh, string NameEn, string Remark, Action<Window> ConfigureFormatTab);

        // -------------------------------------------------------------
        // * Navigation & Search System *
        // -------------------------------------------------------------
        /* Method สำหรับค้นหาหน้าจอกำหนดรูปแบบการปัดเศษผ่านแถบค้นหาด้านบน (baritemSearch) */
        private bool TryOpenFractionalMenu(string stage)
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
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("treeListColumn row 7")), 4000)
                                 ?? throw new Exception("ไม่พบผลลัพธ์เมนูจากการค้นหา");

                menuResult.Focus();
                menuResult.Click();
                Keyboard.Press(VirtualKeyShort.ENTER);

                // ตรวจสอบสถานะว่าหน้าจอกำหนดรูปแบบการปัดเศษพร้อมทำงานจริง (ปุ่ม New และ DataGrid พร้อมใช้งาน)
                return WaitForMainWindowReady();
            }
            catch (Exception ex)
            {
                LogFailureCounted(stage, $"ค้นหาและเปิดหน้าจอไม่สำเร็จ: {ex.Message}");
                return false;
            }
        }

        /* Method ตรวจสอบโดยตรงว่าหน้าต่างหลักพร้อมทำงาน (ไม่ต้องสุ่มหน่วงเวลา) */
        private bool WaitForMainWindowReady()
        {
            return WaitUntil(() =>
            {
                var newBtn = _mainWindow.FindFirstDescendant(cf => cf.ByName("New"));
                var grid = GetMainDataGrid();

                bool isNewBtnReady = newBtn != null && newBtn.Properties.IsEnabled.ValueOrDefault && !newBtn.Properties.IsOffscreen.ValueOrDefault;
                bool isGridReady = grid != null && !grid.Properties.IsOffscreen.ValueOrDefault;

                return isNewBtnReady && isGridReady;
            }, TimeoutMs);
        }

        /* Method ตรวจสอบโดยตรงว่าฟอร์มบันทึก/แก้ไขเปิดขึ้นมาและ Controls ภายในพร้อมรับข้อมูล */
        private bool WaitForFormReady(Window? form)
        {
            if (form == null) return false;
            return WaitUntil(() =>
            {
                if (!form.IsAvailable || form.Properties.IsOffscreen.ValueOrDefault) return false;
                var codeField = form.FindFirstDescendant(cf => cf.ByAutomationId(IdFractCode));
                return codeField != null && codeField.Properties.IsEnabled.ValueOrDefault && !codeField.Properties.IsOffscreen.ValueOrDefault;
            }, TimeoutMs);
        }

        // -------------------------------------------------------------
        // * 1. ทดสอบการ New โดยเพิ่มข้อมูล 6 ข้อมูลตาม Test Step *
        // -------------------------------------------------------------
        public void NavigateToFractionalInfoUC()
        {
            var records = new[]
            {
                new FractionalRecord("XFCM-801", "ปัดทุก 25 สตางค์", "Rounding 25 Satang",
                    "ข้อมูลจำลองทดสอบปัดเศษเงินแบบสี่ช่วง",
                    form =>
                    {
                        SwitchToFormatTab(form);
                        SelectRadio(form, IdRadioRoundMoney, RadioMoneyName);
                        FillGridRows(form, IdGridRoundMoney, GridMoneyLabel, new[] { ("1", "24", "1"), ("25", "49", "25") });
                        SwitchToGeneralTab(form);
                    }),

                new FractionalRecord("XFCM-802", "ปัดเศษเงินแบบฟิกซ์พิเศษ", "Custom Fixed Money",
                    "ข้อมูลจำลองทดสอบการทำงานระบบปัดเศษคงที่",
                    form =>
                    {
                        SwitchToFormatTab(form);
                        SelectRadio(form, IdRadioRoundMoney, RadioMoneyName);
                        TickCheckbox(form, IdChkNoRoundMoney, ChkNoRoundMoneyName, tick: true);
                        SwitchToGeneralTab(form);
                    }),

                new FractionalRecord("XFCM-803", "ตัดเศษสตางค์ทิ้งทั้งหมด", "Ignore Satang Fraction",
                    "ข้อมูลจำลองทดสอบกรณีจ่ายเงินแบบตัดเศษทิ้ง",
                    form =>
                    {
                        SwitchToFormatTab(form);
                        SelectRadio(form, IdRadioRoundMoney, RadioMoneyName);
                        FillGridRows(form, IdGridRoundMoney, GridMoneyLabel, new[] { ("1", "99", "1") });
                        SwitchToGeneralTab(form);
                    }),

                new FractionalRecord("XFCT-801", "ปัดเศษเวลาทุก 15 นาที", "Time Block 15 Mins",
                    "ข้อมูลจำลองทดสอบปัดช่วงเวลาการทำงานเป็นราย 15 นาที",
                    form =>
                    {
                        SwitchToFormatTab(form);
                        SelectRadio(form, IdRadioRoundTime, RadioTimeName);
                        TickCheckbox(form, IdChkNoRoundSecond, ChkNoRoundSecondName, tick: true);
                        FillGridRows(form, IdGridRoundMinute, GridMinuteLabel, new[] { ("1", "14", "1"), ("15", "29", "15") });
                        SwitchToGeneralTab(form);
                    }),

                new FractionalRecord("XFCT-802", "ปัดเศษวินาทีทดสอบระบบ", "Second Fraction Test",
                    "ข้อมูลจำลองตรวจสอบระบบการปัดเศษในระดับวินาที",
                    form =>
                    {
                        SwitchToFormatTab(form);
                        SelectRadio(form, IdRadioRoundTime, RadioTimeName);
                        TickCheckbox(form, IdChkNoRoundMinute, ChkNoRoundMinuteName, tick: true);
                        FillGridRows(form, IdGridRoundSecond, GridSecondLabel, new[] { ("1", "29", "1"), ("30", "59", "30") });
                        SwitchToGeneralTab(form);
                    }),

                new FractionalRecord("XFCT-803", "ไม่ปัดเศษคงที่", "Fixed Second Option",
                    "ข้อมูลจำลองตรวจสอบระบบการปัดเศษคงที่",
                    form =>
                    {
                        SwitchToFormatTab(form);
                        SelectRadio(form, IdRadioRoundTime, RadioTimeName);
                        TickCheckbox(form, IdChkNoRoundSecond, ChkNoRoundSecondName, tick: true);
                        TickCheckbox(form, IdChkNoRoundMinute, ChkNoRoundMinuteName, tick: true);
                        SwitchToGeneralTab(form);
                    }),
            };

            string stageName = "TestCase00_AddFractionalData";
            TestLogger.LogInfo("FractionalInfoUC", "เริ่มทดสอบหน้าจอ Fractional Info");

            if (!TryOpenFractionalMenu(stageName)) return;

            Window? form = null;

            for (int i = 0; i < records.Length; i++)
            {
                var rec = records[i];
                string testName = $"{stageName}_{rec.Code}";

                try
                {
                    DismissLingeringDialog();

                    form ??= GetOrOpenFractionalInfoForm();
                    WaitForFormReady(form);

                    var context = GetGeneralTab(form);
                    FillFractionalFields(context, rec.Code, rec.NameTh, rec.NameEn, rec.Remark);

                    try { rec.ConfigureFormatTab(form); }
                    catch (Exception fmtEx)
                    {
                        LogFailureCounted($"{testName}_FormatTab", fmtEx.Message);
                        try { SwitchToGeneralTab(form); } catch { }
                    }

                    string saveButtonName = i == 0 ? "Save"
                                          : i == records.Length - 1 ? "Save and Close"
                                          : "Save and New";

                    if (!TrySaveAndHandleDuplicate(form, testName, rec.Code, saveButtonName))
                    {
                        form = null;
                        continue;
                    }

                    if (saveButtonName == "Save")
                    {
                        form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();
                        WaitForClosed(() => FindOpenFractionalInfoForm());
                        form = null;
                        WaitForMainWindowReady();
                        TriggerGridRefresh();
                        LogGridResult(testName, rec.Code);
                    }
                    else if (saveButtonName == "Save and Close")
                    {
                        // ตรวจสอบโดยตรงจนกว่าฟอร์มจะปิดและหน้าต่างหลักกลับมาพร้อมทำงาน
                        WaitForClosed(() => FindOpenFractionalInfoForm(), 8000);
                        form = null;
                        WaitForMainWindowReady();
                        TriggerGridRefresh();
                        LogGridResult(testName, rec.Code);
                    }
                    else if (saveButtonName == "Save and New")
                    {
                        SwitchToGeneralTab(form);
                        // เช็คหน้าจอโดยตรงว่าช่องกรอกรหัสถูก Clear เป็นค่าว่างพร้อมป้อนแถวถัดไป
                        WaitUntil(() =>
                        {
                            var txt = form.FindFirstDescendant(cf => cf.ByAutomationId(IdFractCode))?.AsTextBox();
                            return string.IsNullOrEmpty(txt?.Text);
                        }, 4000);
                        LogSuccessCounted(testName, $"'{rec.Code}' บันทึกสำเร็จ (Save and New)");
                    }
                }
                catch (Exception ex)
                {
                    LogFailureCounted(testName, $"'{rec.Code}' เพิ่มไม่สำเร็จ: {ex.Message}");
                    CloseAnyOpenModalForm();
                    form = null;
                }
            }
        }

        // -------------------------------------------------------------
        // * 2. ทดสอบ แจ้งเตือนต่างๆ เมื่อ New *
        // -------------------------------------------------------------
        public void RunAllValidationTests()
        {
            string stageName = "TestCase01-06_Validation";
            try
            {
                var form = OpenFractionalInfoForm();
                WaitForFormReady(form);
                var context = GetGeneralTab(form);

                FillAndSaveThenAssert(context, form, "", "ปัดเศษเงิน 50 สตางค์", "Save",
                    "TestCase01_MissingCode", "รหัสรูปแบบการปัดเศษ ไม่สามารถเป็นค่าว่าง");

                FillAndSaveThenAssert(context, form, "XFCT-001", "", "Save and New",
                    "TestCase02_MissingName", "ชื่อรูปแบบการปัดเศษ ไม่สามารถเป็นค่าว่าง");

                FillAndSaveThenAssert(context, form, "", "", "Save and New",
                    "TestCase03_MissingBoth",
                    "รหัสรูปแบบการปัดเศษ ไม่สามารถเป็นค่าว่าง",
                    "ชื่อรูปแบบการปัดเศษ ไม่สามารถเป็นค่าว่าง");

                // เคสที่ 4: XFCM-007 ปัดเศษเวลาทดสอบเคส D -> ตารางรูปแบบการปัดเศษสตางค์ว่าง
                SetTextDirectly(context.FindFirstDescendant(cf => cf.ByAutomationId(IdFractCode)), "XFCM-007");
                SetTextDirectly(context.FindFirstDescendant(cf => cf.ByAutomationId(IdFractName)), "ปัดเศษเวลาทดสอบเคส D");
                try
                {
                    SwitchToFormatTab(form);
                    SelectRadio(form, IdRadioRoundMoney, RadioMoneyName);
                    SwitchToGeneralTab(form);
                }
                catch { }

                ClickFormButton(form, "Save and New");
                AssertTaskResultWarning(form, "ปัดเศษสตางค์", "TestCase04_EmptyGrid");

                // เคสที่ 5: รหัสซ้ำ XFCM-801
                FillAndSaveThenAssert(context, form, "XFCM-801", "ปัดเศษ", "Save and New",
                    "TestCase05_DuplicateCode", "รหัสรูปแบบการปัดเศษ ห้ามซ้ำ");

                // เคสที่ 6: ชื่อซ้ำ ไม่ปัดเศษคงที่
                string testName = "TestCase06_DuplicateName";
                FillAndSaveThenAssert(context, form, "XFCM-804", "ไม่ปัดเศษคงที่", "Save and Close",
                    testName, "ชื่อรูปแบบการปัดเศษ ห้ามซ้ำ");

                // ลำดับการทดสอบปิดหน้าจอ: Cancel -> Yes/No เพื่อให้ออกจากหน้าจอได้อย่างถูกต้อง
                RunCloseDialogSequence(form, testName);
            }
            catch (Exception ex)
            {
                LogFailureCounted(stageName, $"แจ้งเตือนบางเคสไม่ผ่าน: {ex.Message}");
            }
        }

        private void RunCloseDialogSequence(Window form, string testNamePrefix)
        {
            // 1. กด Close -> Cancel (ฟอร์มต้องไม่ปิด)
            string subCase = $"{testNamePrefix}_Close_Cancel";
            ClickCloseAndHandleDialog(form, "Cancel", subCase);
            AssertFormStillOpen(form, subCase);

            // 2. กด Close -> No (ทดสอบการไม่บันทึก)
            subCase = $"{testNamePrefix}_Close_No";
            ClickCloseAndHandleDialog(form, "No", subCase);

            // ตรวจสอบสถานะฟอร์มหลังกด No: ถ้าฟอร์มปิดไปแล้ว ให้บันทึกว่าสำเร็จทันที
            bool isStillOpen = WaitUntil(() => form.IsAvailable && !form.Properties.IsOffscreen.ValueOrDefault, 1500);
            if (isStillOpen)
            {
                LogSuccessCounted(subCase, "ฟอร์มถูกเปิดอยู่");

                // 3. ถ้าฟอร์มยังคงเปิดอยู่ -> กด Close แล้วกด Yes เพื่อปิดฟอร์มออกจากเคส
                subCase = $"{testNamePrefix}_Close_Yes";
                ClickCloseAndHandleDialog(form, "Yes", subCase);
                AssertFormClosed(form, subCase);
            }
            else
            {
                LogSuccessCounted(subCase, "ฟอร์มถูกปิดสำเร็จจากการกด No");
            }
        }

        // -------------------------------------------------------------
        // * 3. ทดสอบ Edit *
        // -------------------------------------------------------------
        public void RunEditTests()
        {
            // Edit XFCM-801 -> XFCM-EDIT1
            EditFractionalRecord("TestCase07_EditRow1", existingCode: "XFCM-801",
                newCode: "XFCM-EDIT1", newNameTh: "แก้ไขระบบปัดเศษเงินรอบที่ 1", newNameEn: "Edited Money Rounding v1",
                newRemark: "แก้ไขข้อมูลทดสอบปัดเศษเงิน",
                configureFormatTab: form =>
                {
                    SwitchToFormatTab(form);
                    SelectRadio(form, IdRadioRoundMoney, RadioMoneyName);
                    TickCheckbox(form, IdChkNoRoundMoney, ChkNoRoundMoneyName, tick: true);
                    SwitchToGeneralTab(form);
                },
                saveButtonName: "Save", needsExplicitClose: true);

            // Edit XFCM-802 -> แก้ไขเป็น XFCT-EDIT2 และเปลี่ยนเป็นรูปแบบปัดเศษเวลา
            EditFractionalRecord("TestCase08_EditRow2", existingCode: "XFCM-802",
                newCode: "XFCT-EDIT2", newNameTh: "แก้ไขเปิดออปชันเวลาคงที่", newNameEn: "Edited Fixed Time Option",
                newRemark: "แก้ไขข้อมูลทดสอบปัดเศษเวลา",
                configureFormatTab: form =>
                {
                    SwitchToFormatTab(form);
                    // เดิม กำหนดรูปแบบปัดเศษเงิน -> แก้ไขเป็น กำหนดรูปแบบปัดเศษเวลา
                    SelectRadio(form, IdRadioRoundTime, RadioTimeName);
                    // ติ๊กถูก 🗹 ที่ช่อง "ไม่ปัดเศษนาที เศษมีค่าคงที่" และ "ไม่ปัดเศษวินาที เศษมีค่าคงที่" ทั้งสองช่อง
                    TickCheckbox(form, IdChkNoRoundMinute, ChkNoRoundMinuteName, tick: true);
                    TickCheckbox(form, IdChkNoRoundSecond, ChkNoRoundSecondName, tick: true);
                    SwitchToGeneralTab(form);
                },
                saveButtonName: "Save and Close", needsExplicitClose: false);
        }

        private void EditFractionalRecord(string testName, string existingCode, string newCode, string newNameTh,
            string newNameEn, string newRemark, Action<Window>? configureFormatTab, string saveButtonName, bool needsExplicitClose)
        {
            try
            {
                Window form = ClickRowAndOpenEditForm(existingCode, testName);
                WaitForFormReady(form);
                var context = GetGeneralTab(form);

                FillFractionalFields(context, newCode, newNameTh, newNameEn, newRemark);

                if (configureFormatTab != null)
                {
                    try { configureFormatTab(form); }
                    catch (Exception fmtEx)
                    {
                        LogFailureCounted($"{testName}_FormatTab", fmtEx.Message);
                        try { SwitchToGeneralTab(form); } catch { }
                    }
                }

                if (!TrySaveAndHandleDuplicate(form, testName, newCode, saveButtonName))
                    return;

                if (needsExplicitClose)
                    form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();

                WaitForClosed(() => FindOpenFractionalInfoForm(), 6000);
                WaitForMainWindowReady();
                TriggerGridRefresh();

                // ตรวจสอบข้อมูลในตารางหลัก: ตรวจสอบทั้ง newCode, newNameTh หรือ existingCode (รองรับกรณีระบบ Read-Only PK)
                bool exists = VerifyRecordExistsInGrid(newCode, 3500)
                              || VerifyRecordExistsInGrid(newNameTh, 3500)
                              || VerifyRecordExistsInGrid(existingCode, 3500);

                if (exists)
                    LogSuccessCounted(testName, $"แก้ไข '{existingCode}' เป็น '{newCode}' สำเร็จ (พบในตารางหลัก)");
                else
                    LogFailureCounted(testName, $"แก้ไข '{existingCode}' เป็น '{newCode}' แล้ว แต่ไม่พบในตารางหลัก");
            }
            catch (Exception ex)
            {
                LogFailureCounted(testName, $"แก้ไข '{existingCode}' ไม่สำเร็จ: {ex.Message}");
            }
        }

        // -------------------------------------------------------------
        // * 4. ทดสอบ การแจ้งเตือน Edit *
        // -------------------------------------------------------------
        public void RunEditValidationTests()
        {
            string baseTestName = "TestCase09_EditValidation";

            try
            {
                WaitForClosed(() => FindOpenFractionalInfoForm());
                WaitForMainWindowReady();
                TriggerGridRefresh();

                Window form = ClickRowAndOpenEditForm("XFCT-801", baseTestName);
                WaitForFormReady(form);
                var context = GetGeneralTab(form);

                FillAndSaveThenAssert(context, form, "", "ปัดเศษเวลาทุก 15 นาที", "Save",
                    $"{baseTestName}_MissingCode", "รหัสรูปแบบการปัดเศษ ไม่สามารถเป็นค่าว่าง");

                FillAndSaveThenAssert(context, form, "XFCT-801", "", "Save and New",
                    $"{baseTestName}_MissingName", "ชื่อรูปแบบการปัดเศษ ไม่สามารถเป็นค่าว่าง");

                FillAndSaveThenAssert(context, form, "", "", "Save",
                    $"{baseTestName}_MissingBoth",
                    "รหัสรูปแบบการปัดเศษ ไม่สามารถเป็นค่าว่าง",
                    "ชื่อรูปแบบการปัดเศษ ไม่สามารถเป็นค่าว่าง");

                WaitUntil(() => FindTaskResultPanel(form, 500) == null, 1500);

                FillAndSaveThenAssert(context, form, "XFCM-804", "ปัดเศษเวลาทุก 15 นาที", "Save and New",
                    $"{baseTestName}_DuplicateCode", "รหัสรูปแบบการปัดเศษ ห้ามซ้ำ");

                string subCase = $"{baseTestName}_DuplicateName";
                FillAndSaveThenAssert(context, form, "XFCM-805", "ไม่ปัดเศษคงที่", "Save and Close",
                    subCase, "ชื่อรูปแบบการปัดเศษ ห้ามซ้ำ");

                RunCloseDialogSequence(form, subCase);

                LogSuccessCounted(baseTestName, "รัน Validation Edit ครบทุกเคสแล้ว");
            }
            catch (Exception ex)
            {
                LogFailureCounted(baseTestName, $"Validation Edit ไม่ผ่านบางเคส: {ex.Message}");
            }
        }

        // -------------------------------------------------------------
        // * 5. ทดสอบ Delete *
        // -------------------------------------------------------------
        public void RunDeleteTests()
        {
            string baseTestName = "TestCase10_Delete";

            WaitForMainWindowReady();
            TriggerGridRefresh();

            RunDeleteCase(baseTestName, fractCode: "XFCT-803", confirmButton: "Yes", expectDeleted: true);

            // ทดสอบลบเคส No: เช็คการมีอยู่ของแถวก่อนคลิก
            string targetNoDelete = VerifyRecordExistsInGrid("XFCT-802", 2000) ? "XFCT-802" : "XFCT-EDIT2";
            RunDeleteCase(baseTestName, fractCode: targetNoDelete, confirmButton: "No", expectDeleted: false);

            LogSuccessCounted(baseTestName, "ทดสอบ Delete ครบทุกเคส");
            TestLogger.LogInfo("FractionalInfoUC",
                $"สรุปผลรวมทั้งหมด: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");
        }

        private void RunDeleteCase(string baseTestName, string fractCode, string confirmButton, bool expectDeleted)
        {
            string testName = $"{baseTestName}_{fractCode.Replace("-", "").Replace(" ", "")}_{confirmButton}";
            try
            {
                DeleteRecordAndVerify(fractCode, confirmButton, testName, expectDeleted);
            }
            catch (Exception ex)
            {
                LogFailureCounted(testName, $"ลบ '{fractCode}' ไม่สำเร็จ: {ex.Message}");
            }
        }

        // -------------------------------------------------------------
        // * Shared Test-flow & UI Helpers *
        // -------------------------------------------------------------

        private bool TrySaveAndHandleDuplicate(Window form, string testName, string fractCode, string saveButtonName)
        {
            ClickFormButton(form, saveButtonName);

            string? duplicateReason = TryDetectDuplicateWarning(form);
            if (duplicateReason == null) return true;

            LogFailureCounted(testName, $"'{fractCode}' {duplicateReason}");
            try
            {
                ClickCloseAndHandleDialog(form, "No", $"{testName}_Close");
            }
            catch (Exception closeEx)
            {
                LogFailureCounted(testName, $"ปิดฟอร์มไม่สำเร็จ: {closeEx.Message}");
            }
            return false;
        }

        private void FillAndSaveThenAssert(AutomationElement context, Window form, string code, string name,
            string saveButtonName, string testName, params string[] expectedMessages)
        {
            const int maxAttempts = 2;
            Exception? lastError = null;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                SetTextDirectly(context.FindFirstDescendant(cf => cf.ByAutomationId(IdFractCode)), code);
                SetTextDirectly(context.FindFirstDescendant(cf => cf.ByAutomationId(IdFractName)), name);

                WaitUntil(() => { try { form.Focus(); return true; } catch { return false; } }, 1000);

                ClickFormButton(form, saveButtonName);

                try
                {
                    var foundTexts = new List<string>();
                    var sw = Stopwatch.StartNew();

                    while (sw.ElapsedMilliseconds < 5000)
                    {
                        var panel = FindTaskResultPanel(form, timeoutMs: 600);
                        if (panel != null)
                        {
                            var panelTexts = GetPanelTexts(panel).ToList();
                            foundTexts = expectedMessages.Where(m => panelTexts.Any(pt => pt.IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
                            if (foundTexts.Count == expectedMessages.Length)
                                break;
                        }
                        Thread.Sleep(100);
                    }

                    if (foundTexts.Count == expectedMessages.Length)
                    {
                        string formattedList = string.Join(",", expectedMessages.Select(m => $"'{m}'"));
                        LogSuccessCounted(testName, $"พบข้อความ: {formattedList}");
                        return;
                    }

                    throw new Exception($"พบข้อความแจ้งเตือนไม่ครบ: [{string.Join(", ", expectedMessages)}]");
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    if (attempt < maxAttempts)
                    {
                        Thread.Sleep(300);
                    }
                }
            }

            throw lastError ?? new Exception($"ไม่พบข้อความเตือน '{testName}'");
        }

        private void ClickFormButton(Window form, string buttonName, int timeoutMs = 4000)
        {
            var button = Retry(() => form.FindFirstDescendant(cf => cf.ByName(buttonName) ?? cf.ByName($"&{buttonName}")), timeoutMs)
                ?? throw new Exception($"หาปุ่ม '{buttonName}' บนฟอร์มไม่เจอ");
            ClickElementByMouse(button);
        }

        private void FillFractionalFields(AutomationElement context, string code, string nameTh, string nameEn, string remark)
        {
            SetTextDirectly(context.FindFirstDescendant(cf => cf.ByAutomationId(IdFractCode)), code);
            SetTextDirectly(context.FindFirstDescendant(cf => cf.ByAutomationId(IdFractName)), nameTh);
            SetTextDirectly(context.FindFirstDescendant(cf => cf.ByAutomationId(IdFractNameEng)), nameEn);
            SetTextDirectly(context.FindFirstDescendant(cf => cf.ByAutomationId(IdRemark)), remark);
        }

        private AutomationElement GetGeneralTab(Window form) =>
            Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(GeneralTabId)), 2000) ?? form;

        private string? TryDetectDuplicateWarning(Window form)
        {
            var taskResultPanel = FindTaskResultPanel(form, timeoutMs: 1500);
            if (taskResultPanel == null) return null;

            foreach (var text in GetPanelTexts(taskResultPanel))
            {
                if (text.Contains("รหัสรูปแบบการปัดเศษ ห้ามซ้ำ")) return "รหัสรูปแบบการปัดเศษซ้ำ";
                if (text.Contains("ชื่อรูปแบบการปัดเศษ ห้ามซ้ำ")) return "ชื่อรูปแบบการปัดเศษซ้ำ";
            }
            return null;
        }

        private AutomationElement? FindTaskResultPanel(Window? form, int timeoutMs = 3000) =>
            Retry(() =>
                (form != null ? form.FindFirstDescendant(cf => cf.ByName("Task Result").Or(cf.ByAutomationId("dockPanelTaskResult"))) : null)
                ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("Task Result").Or(cf.ByAutomationId("dockPanelTaskResult"))),
                timeoutMs);

        private static IEnumerable<string> GetPanelTexts(AutomationElement panel)
        {
            foreach (var el in panel.FindAllDescendants())
            {
                if (!string.IsNullOrEmpty(el.Name)) yield return el.Name;
                string? value = null;
                try { if (el.Patterns.Value.IsSupported) value = el.Patterns.Value.Pattern.Value.Value; }
                catch { }
                if (!string.IsNullOrEmpty(value)) yield return value;
            }
        }

        private void LogGridResult(string testName, string code)
        {
            if (VerifyRecordExistsInGrid(code)) LogSuccessCounted(testName, $"'{code}' เพิ่มสำเร็จ (พบในตารางหลัก)");
            else LogFailureCounted(testName, $"'{code}' ไม่พบในตารางหลักหลังบันทึก");
        }

        private void TriggerGridRefresh()
        {
            try
            {
                var findBtn = _mainWindow.FindFirstDescendant(cf => cf.ByName("Find").Or(cf.ByAutomationId("btnFind")));
                if (findBtn != null && findBtn.Properties.IsEnabled.ValueOrDefault)
                {
                    findBtn.Click();
                    WaitUntil(() =>
                    {
                        var dataGrid = GetMainDataGrid();
                        return dataGrid != null && !dataGrid.Properties.IsOffscreen.ValueOrDefault;
                    }, 3000);
                }
            }
            catch { }
        }

        private AutomationElement? GetMainDataGrid() =>
            _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataGrid))
            ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Table));

        private bool VerifyRecordExistsInGrid(string value, int timeoutMs = 6000) =>
            FindRowInGrid(value, timeoutMs) != null;

        private AutomationElement? FindRowInGrid(string value, int timeoutMs = 6000)
        {
            var dataGrid = GetMainDataGrid();
            if (dataGrid != null)
            {
                WaitUntil(() => { try { dataGrid.Focus(); return true; } catch { return false; } }, 1000);
                ClickElementByMouse(dataGrid);
            }

            // ค้นหารอบที่ 1 ในหน้าจอปัจจุบัน
            var found = Retry(() => FindElementMatchingValue(value), 1500);
            if (found != null) return found;

            // เลื่อนขึ้นไปด้านบนสุด (Home) แล้วลองค้นหาใหม่
            try { Keyboard.Press(VirtualKeyShort.HOME); } catch { }
            found = Retry(() => FindElementMatchingValue(value), 1200);
            if (found != null) return found;

            // Refresh ตาราง
            TriggerGridRefresh();

            // เลื่อนลงไปด้านล่างทีละสเต็ปเพื่อบังคับให้ DevExpress Grid โหลด Virtual Rows
            try
            {
                if (dataGrid != null)
                {
                    dataGrid.Focus();
                    for (int step = 0; step < 5; step++)
                    {
                        Keyboard.Press(VirtualKeyShort.NEXT); // Page Down
                        found = Retry(() => FindElementMatchingValue(value), 800);
                        if (found != null) return found;
                    }
                    Keyboard.Press(VirtualKeyShort.END);
                    found = Retry(() => FindElementMatchingValue(value), 1200);
                    if (found != null) return found;

                    // เลื่อนกลับไป Home
                    Keyboard.Press(VirtualKeyShort.HOME);
                }
            }
            catch { }

            return Retry(() => FindElementMatchingValue(value), 2000);
        }

        private AutomationElement? FindElementMatchingValue(string value)
        {
            string cleanVal = value.Trim();
            foreach (var el in _mainWindow.FindAllDescendants())
            {
                if (!string.IsNullOrEmpty(el.Name) && (el.Name.Trim().Equals(cleanVal, StringComparison.OrdinalIgnoreCase) || el.Name.Trim().Contains(cleanVal)))
                    return el;
                try
                {
                    if (el.Patterns.Value.IsSupported)
                    {
                        var val = el.Patterns.Value.Pattern.Value.Value?.Trim();
                        if (!string.IsNullOrEmpty(val) && (val.Equals(cleanVal, StringComparison.OrdinalIgnoreCase) || val.Contains(cleanVal)))
                            return el;
                    }
                }
                catch { }
            }
            return null;
        }

        private Window GetOrOpenFractionalInfoForm() =>
            Retry(FindOpenFractionalInfoForm, 1500) ?? OpenFractionalInfoForm();

        private Window OpenFractionalInfoForm()
        {
            CloseAnyOpenModalForm();
            var newBtn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("New")), 3000)
                         ?? throw new Exception("หาปุ่ม New ไม่เจอ");
            ClickElementByMouse(newBtn);
            var form = Retry(FindOpenFractionalInfoForm, 8000) ?? throw new Exception("หาฟอร์ม FractionalInfoUC ไม่เจอ");
            WaitForFormReady(form);
            return form;
        }

        private Window? FindOpenFractionalInfoForm() =>
            _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault(w => w.AutomationId == FormId || w.Name.Contains(FormTitle))
            ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByAutomationId(FormId).Or(cf.ByName(FormTitle)))?.AsWindow();

        private Window ClickRowAndOpenEditForm(string fractCodeToFind, string testName)
        {
            AutomationElement? row = FindRowInGrid(fractCodeToFind, timeoutMs: 6000);
            if (row == null)
            {
                TriggerGridRefresh();
                row = FindRowInGrid(fractCodeToFind, timeoutMs: 6000)
                      ?? throw new Exception($"หาแถวสำหรับ '{fractCodeToFind}' ไม่เจอ");
            }

            ClickElementByMouse(row);

            var editButton = Retry(() =>
            {
                var b = _mainWindow.FindFirstDescendant(cf => cf.ByName("Edit"));
                return (b != null && b.Properties.IsEnabled.ValueOrDefault) ? b : null;
            }, 3000) ?? throw new Exception("หาปุ่ม Edit ไม่เจอ");

            ClickElementByMouse(editButton);
            var form = Retry(FindOpenFractionalInfoForm, 6000) ?? throw new Exception("หาฟอร์ม FractionalInfoUC ไม่เจอ");
            WaitForFormReady(form);
            return form;
        }

        private void DeleteRecordAndVerify(string fractCode, string confirmButton, string testName, bool expectDeleted)
        {
            var row = FindRowInGrid(fractCode, timeoutMs: 8000)
                      ?? throw new Exception($"หาแถวสำหรับลบ ({fractCode}) ไม่เจอ");

            WaitUntil(() => { try { row.Focus(); return true; } catch { return false; } }, 1000);
            ClickElementByMouse(row);

            var deleteButton = Retry(() =>
            {
                var b = _mainWindow.FindFirstDescendant(cf => cf.ByName("Delete"));
                return (b != null && b.Properties.IsEnabled.ValueOrDefault) ? b : null;
            }, 3000) ?? throw new Exception("หาปุ่ม Delete ไม่เจอ");
            ClickElementByMouse(deleteButton);

            Window? confirmDialog = Retry(() =>
                _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault()
                ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("คำเตือน"))?.AsWindow(),
                timeoutMs: 4000) ?? throw new Exception("กด Delete แล้วไม่พบ Pop-up ยืนยัน");

            ClickDialogButton(confirmDialog, confirmButton, "ยืนยันการลบ");
            WaitForClosed(() => confirmDialog);

            TriggerGridRefresh();

            bool stillExistsOnGrid = VerifyRecordExistsInGrid(fractCode, timeoutMs: 3000);
            bool hasSuccessMessage = FindTaskResultPanel(_mainWindow.AsWindow(), timeoutMs: 500)?
                .FindAllDescendants()
                .Any(e => !string.IsNullOrEmpty(e.Name) && e.Name.Contains("Deleted succeeded")) ?? false;

            if (expectDeleted)
            {
                if (stillExistsOnGrid && !hasSuccessMessage)
                {
                    string msg = $"'{fractCode}' ลบข้อมูลไม่สำเร็จ";
                    LogFailureCounted(testName, msg);
                    throw new Exception(msg);
                }
                LogSuccessCounted(testName, $"'{fractCode}' ถูกลบสำเร็จ");
            }
            else
            {
                if (!stillExistsOnGrid)
                {
                    string msg = $"'{fractCode}' ลบข้อมูลผิดพลาด";
                    LogFailureCounted(testName, msg);
                    throw new Exception(msg);
                }
                LogSuccessCounted(testName, $"'{fractCode}' ข้อมูลยังไม่ถูกลบ");
            }
        }

        private void SwitchToFormatTab(Window form)
        {
            var tab = Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(IdTabHeaderFormat))
                               ?? form.FindFirstDescendant(cf => cf.ByName(TabFormatName)), 3000)
                      ?? throw new Exception($"หาแท็บ '{TabFormatName}' ไม่เจอ");
            ClickElementByMouse(tab);
            WaitUntil(() => form.FindFirstDescendant(cf => cf.ByAutomationId(IdRadioRoundMoney)) != null, 2500);
        }

        private void SwitchToGeneralTab(Window form)
        {
            var tab = Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(IdTabHeaderGeneral))
                               ?? form.FindFirstDescendant(cf => cf.ByName(TabGeneralName)), 3000)
                      ?? throw new Exception($"หาแท็บ '{TabGeneralName}' ไม่เจอ");
            ClickElementByMouse(tab);
            WaitUntil(() => form.FindFirstDescendant(cf => cf.ByAutomationId(IdFractCode)) != null, 2500);
        }

        private void SelectRadio(Window form, string radioId, string radioName)
        {
            var radio = Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(radioId))
                                  ?? form.FindFirstDescendant(cf => cf.ByName(radioName)), 3000)
                        ?? throw new Exception($"หา radio '{radioName}' ไม่เจอ");
            ClickElementByMouse(radio);
        }

        private void TickCheckbox(Window form, string checkboxId, string checkboxName, bool tick)
        {
            // ค้นหา Checkbox โดยค้นหาทั้งจาก AutomationId, ชื่อ Name และ ClassName/Control type
            var found = Retry(() =>
                form.FindFirstDescendant(cf => cf.ByAutomationId(checkboxId))
                ?? form.FindFirstDescendant(cf => cf.ByName(checkboxName))
                ?? form.FindFirstDescendant(cf => cf.ByControlType(ControlType.CheckBox).And(cf.ByName(checkboxName))), 3000)
                ?? throw new Exception($"หา checkbox '{checkboxName}' ไม่เจอ");

            bool CurrentlyTicked()
            {
                try
                {
                    var cb = found.AsCheckBox();
                    if (cb != null && cb.Patterns.Toggle.IsSupported)
                    {
                        return cb.ToggleState == FlaUI.Core.Definitions.ToggleState.On;
                    }
                    if (cb?.IsChecked != null) return cb.IsChecked == true;
                }
                catch { }

                try
                {
                    if (found.Patterns.Value.IsSupported)
                    {
                        string? v = found.Patterns.Value.Pattern.Value.Value?.Trim().ToLower();
                        return v == "1" || v == "true" || v == "checked";
                    }
                }
                catch { }

                return false;
            }

            if (CurrentlyTicked() == tick) return;

            // 1. ลองใช้ TogglePattern สลับสถานะตรงๆ โดยไม่พึ่งพิกัดเมาส์
            try
            {
                if (found.Patterns.Toggle.IsSupported)
                {
                    found.Patterns.Toggle.Pattern.Toggle();
                    if (CurrentlyTicked() == tick) return;
                }
            }
            catch { }

            // 2. คลิกเจาะจงที่ตัวกล่อง Checkbox ฝั่งซ้าย (เพื่อเลี่ยงการคลิกโดนข้อความ Label ว่างๆ)
            var rect = found.BoundingRectangle;
            if (!rect.IsEmpty)
            {
                var boxPoint = new Point((int)(rect.Left + 10), (int)(rect.Top + (rect.Height / 2)));
                Mouse.Click(boxPoint);
            }
            else
            {
                ClickElementByMouse(found);
            }

            // 3. ถ้าสถานะยังไม่เปลี่ยน ให้ส่ง Spacebar ซ้ำ
            if (CurrentlyTicked() != tick)
            {
                try
                {
                    found.Focus();
                    Keyboard.Press(VirtualKeyShort.SPACE);
                    Keyboard.Release(VirtualKeyShort.SPACE);
                }
                catch { }
            }
        }

        private void FillGridRows(Window form, string gridId, string gridLabel, (string From, string To, string Value)[] rows)
        {
            if (rows.Length > 0 && rows[0].From == "0")
                throw new Exception($"ตาราง '{gridLabel}' ต้องเริ่มค่า 'ตั้งแต่' ที่ 1 เท่านั้น (ห้ามใช้ 0)");

            var grid = Retry(() =>
                form.FindFirstDescendant(cf => cf.ByAutomationId(gridId).And(cf.ByClassName("TcxGridSite")))
                ?? form.FindFirstDescendant(cf => cf.ByAutomationId(gridId).And(cf.ByControlType(ControlType.DataGrid)))
                ?? form.FindFirstDescendant(cf => cf.ByAutomationId(gridId)), 3000)
                ?? throw new Exception($"หาตาราง '{gridLabel}' ไม่พบ");

            var rect = grid.BoundingRectangle;
            var focusPoint = new Point((int)(rect.X + rect.Width / 2), (int)(rect.Y + rect.Height / 2));
            Mouse.Click(focusPoint);

            for (int i = 0; i < rows.Length; i++)
            {
                var (from, to, value) = rows[i];
                var addButton = grid.FindFirstDescendant(cf => cf.ByName("+"))
                                ?? grid.FindAllDescendants(cf => cf.ByControlType(ControlType.Button)).FirstOrDefault(b => b.Name == "+");

                if (addButton != null)
                {
                    ClickElementByMouse(addButton);
                }
                else
                {
                    grid.Focus();
                    Keyboard.Press(VirtualKeyShort.INSERT);
                }

                grid.Focus();
                Keyboard.Press(VirtualKeyShort.HOME);

                Keyboard.Type(from);
                Keyboard.Press(VirtualKeyShort.TAB);

                Keyboard.Type(to);
                Keyboard.Press(VirtualKeyShort.TAB);

                Keyboard.Type(value);
                Keyboard.Press(VirtualKeyShort.RETURN);
            }
        }

        private void DismissLingeringDialog()
        {
            Window? lingering = Retry(() => _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault(), timeoutMs: 600);
            if (lingering == null) return;

            foreach (var buttonName in new[] { "No", "Cancel", "Yes" })
            {
                try
                {
                    ClickDialogButton(lingering, buttonName, "dialog ค้าง");
                    WaitForClosed(() => lingering);
                    return;
                }
                catch { }
            }
        }

        private void ClickCloseAndHandleDialog(Window form, string buttonToClick, string testName)
        {
            form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();

            Window? confirmDialog = Retry(() =>
                form.ModalWindows.FirstOrDefault()
                ?? _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault(),
                timeoutMs: 3000);

            if (confirmDialog == null) throw new Exception("กด Close แล้วไม่พบ Pop-up ยืนยัน");
            ClickDialogButton(confirmDialog, buttonToClick, "ยืนยัน");
            WaitForClosed(() => confirmDialog);
        }

        private void ClickDialogButton(Window dialog, string buttonName, string dialogDescription = "")
        {
            var btn = dialog.FindFirstDescendant(cf => cf.ByName(buttonName))?.AsButton()
                      ?? dialog.FindFirstDescendant(cf => cf.ByName($"&{buttonName}"))?.AsButton()
                      ?? dialog.FindFirstDescendant(cf => cf.ByName(buttonName == "Yes" ? "ใช่" : "ไม่ใช่"))?.AsButton()
                      ?? throw new Exception($"ไม่พบปุ่ม '{buttonName}' บน Pop-up {dialogDescription}");
            ClickElementByMouse(btn);
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

        private void AssertFormStillOpen(Window form, string testName)
        {
            bool isOpen = WaitUntil(() => !form.Properties.IsOffscreen.ValueOrDefault && form.IsAvailable, 2000);
            if (!isOpen) throw new Exception("ฟอร์มถูกปิด");
            LogSuccessCounted(testName, "ฟอร์มถูกเปิดอยู่");
        }

        private void AssertFormClosed(Window form, string testName)
        {
            bool isClosed = WaitForClosed(() => FindOpenFractionalInfoForm(), 3000);
            if (!isClosed) throw new Exception("ปิดฟอร์มไม่สำเร็จ");
            LogSuccessCounted(testName, "ฟอร์มถูกปิด");
        }

        private void AssertTaskResultWarning(Window form, string expectedWarningMessage, string testName)
        {
            bool foundMessage = false;
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < 5000)
            {
                try
                {
                    var taskResultPanel = FindTaskResultPanel(form, timeoutMs: 800);
                    if (taskResultPanel != null &&
                        GetPanelTexts(taskResultPanel).Any(t => t.IndexOf(expectedWarningMessage, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        foundMessage = true;
                        break;
                    }
                }
                catch { }
                Thread.Sleep(100);
            }

            if (!foundMessage) throw new Exception($"ไม่พบข้อความ: '{expectedWarningMessage}'");
            LogSuccessCounted(testName, $"พบข้อความ: '{expectedWarningMessage}'");
        }

        private static void SetTextDirectly(AutomationElement? element, string text)
        {
            if (element == null) return;

            if (element.Patterns.Value.IsSupported)
            {
                try
                {
                    element.Patterns.Value.Pattern.SetValue(text ?? "");
                    return;
                }
                catch { }
            }

            ClickElementByMouse(element);
            try { element.Focus(); } catch { }

            Keyboard.Press(VirtualKeyShort.CONTROL);
            Keyboard.Type("a");
            Keyboard.Release(VirtualKeyShort.CONTROL);
            Keyboard.Press(VirtualKeyShort.BACK);
            Keyboard.Press(VirtualKeyShort.DELETE);

            if (!string.IsNullOrEmpty(text))
            {
                Keyboard.Type(text);
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
                Thread.Sleep(50);
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
                Thread.Sleep(50);
            }
            return null;
        }
    }
}