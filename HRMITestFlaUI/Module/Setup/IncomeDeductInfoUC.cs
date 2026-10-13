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
    public class IncomeDeductInfoUC
    {
        // ── ตัวแปรสำหรับเก็บออบเจ็กต์หน้าต่างหลัก, ระบบ Automation และตัวนับผลการทดสอบ (Success/Fail) ──
        private readonly AutomationElement _mainWindow;
        private readonly UIA3Automation _automation;

        private int _totalSuccess = 0;
        private int _totalFail = 0;

        // ── ค่าคงที่สำหรับการตั้งค่า Timeout และข้อมูลการระบุตัวตนของฟอร์ม ──
        private const int TimeoutMs = 6000;
        private const string FormId = "IncomeDeductInfoUC";
        private const string FormTitle = "กำหนดรายได้ - รายหัก";

        private const string GeneralTabId = "devTabPgGeneral";

        // ── Automation IDs ของคอนโทรลบนหน้าจอ ──
        private const string IdCode = "devtxtIncomeDeductCode";
        private const string IdName = "devtxtIncomeDeductName";
        private const string IdNameEng = "devtxtlIncomeDeductNameEng";
        private const string IdIncomeDeductTypeLookup = "devLookupIncomeDeductType";
        private const string IdTaxSectionLookup = "devlookupTaxName";
        private const string IdCalcMethodLookup = "devlookupCalType";
        private const string IdAmount = "devSpinEditValue";
        private const string IdUomButton = "devbtnEditUOM";
        private const string IdAccountButton = "devbtnEditAccount";
        private const string IdAccountName = "devtxtAccountName";
        private const string IdChkCalcTax = "devChkCalTax";
        private const string IdTaxMethodRadioGroup = "devRdgTax";
        private const string IdTaxFixedRate = "devRateDeductspinEdit";
        private const string IdTaxWithholdingRate = "devRateDeductspinEdit2";

        private const string IdChkCalcSocialSecurity = "devChkCalSocial";
        private const string IdChkDeductBeforeTax = "devChkDeductBeforeTax";

        private const string IdChkProvidentFund = "devChkProvidentfund";
        private const string IdChkWelfareFundEwf = "devChkWelfarefundEWF";

        private const string IdChkBaseOvertime = "devChkbaseOvertime";
        private const string IdChkBaseAbsenteeism = "devChkbaseAbsenteeism";
        private const string IdChkBaseBrokenline = "devChkbaseBrokenline";
        private const string IdChkBaseLeave = "devChkbaseLeave";

        private const string IdChkByOtPeriod = "devChkByOTperiod";
        private const string IdChkByAbsentPeriod = "devChkByAbsentperiod";
        private const string IdChkByBrokenPeriod = "devChkByBrokenperiod";
        private const string IdChkByLeavePeriod = "devChkByLeaveperiod";

        // ── กลุ่มมาตราภาษีที่มีเงื่อนไขจำกัด ไม่ต้องตั้งค่าเพิ่มเติม ──
        private static readonly HashSet<string> RestrictedTaxSections = new(StringComparer.OrdinalIgnoreCase)
        {
            "40(6)", "40(7)", "40(8)"
        };

        public IncomeDeductInfoUC(AutomationElement mainWindow, UIA3Automation automation)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

        // ── Data Model สำหรับเก็บข้อมูลการทดสอบรหัสรายได้-รายหัก ──
        private sealed record IncomeDeductRecord(
            string Code,
            string NameTh,
            string NameEn,
            string TypeText,
            string TaxSectionText,
            string? CalcMethodText,
            decimal? Amount,
            string? UnitText,
            string AccountText,
            bool CalcTax,
            int? TaxRadioIndex,
            decimal? FixedRatePercent,
            bool DeductBeforeTax,
            bool CalcSocialSecurity,
            bool CalcProvidentFund,
            bool CalcWelfareFund,
            bool BaseOvertime,
            bool BaseAbsenteeism,
            bool BaseBrokenline,
            bool BaseLeave,
            bool ByOtPeriod,
            bool ByAbsentPeriod,
            bool ByBrokenPeriod,
            bool ByLeavePeriod);

        // -------------------------------------------------------------
        // * Navigation & Search System *
        // -------------------------------------------------------------
        private bool TryOpenIncomeDeductMenu(string stage)
        {
            try
            {
                var searchBox = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("baritemSearch"))
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)), 4000)
                                ?? throw new Exception("ไม่พบช่องค้นหา baritemSearch");

                searchBox.Focus();
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
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("treeListColumn row 26")), 4000)
                                 ?? throw new Exception("ไม่พบผลลัพธ์เมนูจากการค้นหา");

                menuResult.Focus();
                menuResult.Click();
                Keyboard.Press(VirtualKeyShort.ENTER);

                var newBtn = Retry(() =>
                {
                    var btn = _mainWindow.FindFirstDescendant(cf => cf.ByName("New"));
                    return (btn != null && btn.Properties.IsEnabled.ValueOrDefault) ? btn : null;
                }, TimeoutMs);

                return newBtn != null;
            }
            catch (Exception ex)
            {
                LogFailureCounted(stage, $"ค้นหาและเปิดหน้าจอไม่สำเร็จ: {ex.Message}");
                return false;
            }
        }

        // -------------------------------------------------------------
        // * 1. Open & New Tests *
        // -------------------------------------------------------------
        public void NavigateToIncomeDeductInfoUC()
        {
            string stageName = "TestCase00_AddIncomeDeductData";
            TestLogger.LogInfo("IncomeDeductInfoUC", "เริ่มทดสอบหน้าจอ กำหนดรายได้ - รายหัก");

            if (!TryOpenIncomeDeductMenu(stageName)) return;

            var items = BuildNewTestRecords();
            Window? form = null;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                string testName = $"{stageName}_{item.Code}";

                try
                {
                    form ??= OpenNewIncomeDeductForm(testName);
                    var context = GetGeneralTab(form);

                    FillIncomeDeductFieldsFull(form, context, item);

                    string saveButtonName = (i == 0) ? "Save"
                                          : (i == items.Count - 1) ? "Save and Close"
                                          : "Save and New";

                    bool saveSuccess = TrySaveAndHandleDuplicate(form, testName, item.Code, saveButtonName);
                    if (!saveSuccess) { form = null; continue; }

                    if (saveButtonName == "Save")
                    {
                        ClickFormCloseButton(form);
                        WaitForClosed(() => FindOpenIncomeDeductForm());
                        form = null;
                        RefreshGrid();
                        if (FindRecordExistsRobust(item.Code))
                            LogSuccessCounted(testName, $"'{item.Code}' เพิ่มสำเร็จ (พบในตารางหลัก)");
                        else
                            LogFailureCounted(testName, $"'{item.Code}' ไม่พบในตารางหลักหลังบันทึก");
                    }
                    else if (saveButtonName == "Save and Close")
                    {
                        WaitForClosed(() => FindOpenIncomeDeductForm());
                        form = null;
                        RefreshGrid();
                        if (FindRecordExistsRobust(item.Code))
                            LogSuccessCounted(testName, $"'{item.Code}' เพิ่มสำเร็จ (พบในตารางหลัก)");
                        else
                            LogFailureCounted(testName, $"'{item.Code}' ไม่พบในตารางหลักหลังบันทึก");
                    }
                    else if (saveButtonName == "Save and New")
                    {
                        WaitUntil(() =>
                        {
                            var txt = form.FindFirstDescendant(cf => cf.ByAutomationId(IdCode))?.AsTextBox();
                            return txt != null && string.IsNullOrEmpty(txt.Text) && txt.Properties.IsEnabled.ValueOrDefault;
                        }, 4000);
                        LogSuccessCounted(testName, $"'{item.Code}' บันทึกสำเร็จ (Save and New)");
                    }
                }
                catch (Exception ex)
                {
                    LogFailureCounted(testName, $"'{item.Code}' เพิ่มไม่สำเร็จ: {ex.Message}");
                    CloseAnyOpenModalForm();
                    form = null;
                }
            }
        }

        private static List<IncomeDeductRecord> BuildNewTestRecords() => new()
        {
            // 1. DED001: บัญชีเงินสด | แบบอัตราก้าวหน้า | ติ๊กถูกครบทุกช่องตาม Test Step
            new IncomeDeductRecord("DED001", "เงินเดือน", "Salary",
                TypeText: "รายได้ประจำ", TaxSectionText: "40(1)", CalcMethodText: "กำหนดเอง",
                Amount: null, UnitText: null, AccountText: "บัญชีเงินสด",
                CalcTax: true, TaxRadioIndex: 0, FixedRatePercent: null, DeductBeforeTax: false,
                CalcSocialSecurity: true, CalcProvidentFund: true, CalcWelfareFund: true,
                BaseOvertime: true, BaseAbsenteeism: true, BaseBrokenline: true, BaseLeave: true,
                ByOtPeriod: true, ByAbsentPeriod: true, ByBrokenPeriod: true, ByLeavePeriod: true),

            // 2. DED002: บัญชีเงินฝากธนาคาร | Fixed Rate 0.05% | ติ๊กถูกครบทุกช่องตาม Test Step
            new IncomeDeductRecord("DED002", "ค่าเบี้ยขยัน", "Diligence Allowance",
                TypeText: "รายได้ประจำ", TaxSectionText: "40(2)", CalcMethodText: "จำนวนคงที่",
                Amount: 500m, UnitText: null, AccountText: "บัญชีเงินฝากธนาคาร",
                CalcTax: true, TaxRadioIndex: 1, FixedRatePercent: 0.05m, DeductBeforeTax: false,
                CalcSocialSecurity: true, CalcProvidentFund: true, CalcWelfareFund: true,
                BaseOvertime: true, BaseAbsenteeism: true, BaseBrokenline: true, BaseLeave: true,
                ByOtPeriod: true, ByAbsentPeriod: true, ByBrokenPeriod: true, ByLeavePeriod: true),

            // 3. INC001: บัญชีเงินฝากธนาคาร | หักก่อนคำนวณภาษี
            new IncomeDeductRecord("INC001", "หักประกันสังคม", "Social Security Deduction",
                TypeText: "รายหักประจำ", TaxSectionText: "40(1)", CalcMethodText: "กำหนดเอง",
                Amount: null, UnitText: null, AccountText: "บัญชีเงินฝากธนาคาร",
                CalcTax: false, TaxRadioIndex: null, FixedRatePercent: null, DeductBeforeTax: true,
                CalcSocialSecurity: true, CalcProvidentFund: true, CalcWelfareFund: true,
                BaseOvertime: false, BaseAbsenteeism: false, BaseBrokenline: false, BaseLeave: false,
                ByOtPeriod: false, ByAbsentPeriod: false, ByBrokenPeriod: false, ByLeavePeriod: false),

            // 4. INC002: บัญชีเงินสด | จำนวนคงที่ 1000 | หักก่อนคำนวณภาษี
            new IncomeDeductRecord("INC002", "หักกองทุนสำรอง", "Provident Fund Deduction",
                TypeText: "รายหักประจำ", TaxSectionText: "40(2)", CalcMethodText: "จำนวนคงที่",
                Amount: 1000m, UnitText: null, AccountText: "บัญชีเงินสด",
                CalcTax: false, TaxRadioIndex: null, FixedRatePercent: null, DeductBeforeTax: true,
                CalcSocialSecurity: true, CalcProvidentFund: true, CalcWelfareFund: true,
                BaseOvertime: false, BaseAbsenteeism: false, BaseBrokenline: false, BaseLeave: false,
                ByOtPeriod: false, ByAbsentPeriod: false, ByBrokenPeriod: false, ByLeavePeriod: false),

            // 5. INC003: บัญชีเงินสด | กำหนดเอง
            new IncomeDeductRecord("INC003", "โบนัสประจำปี", "Annual Bonus",
                TypeText: "รายได้ไม่ประจำ", TaxSectionText: "40(1)", CalcMethodText: "กำหนดเอง",
                Amount: null, UnitText: null, AccountText: "บัญชีเงินสด",
                CalcTax: true, TaxRadioIndex: null, FixedRatePercent: null, DeductBeforeTax: false,
                CalcSocialSecurity: true, CalcProvidentFund: true, CalcWelfareFund: true,
                BaseOvertime: false, BaseAbsenteeism: false, BaseBrokenline: false, BaseLeave: false,
                ByOtPeriod: false, ByAbsentPeriod: false, ByBrokenPeriod: false, ByLeavePeriod: false),

            // 6. INC004: บัญชีเงินฝากธนาคาร | ค่าแรงรายวัน (หน่วย วัน) | Fixed Rate 0.07%
            new IncomeDeductRecord("INC004", "ค่าแรงประจำวัน", "Daily Wage",
                TypeText: "รายได้ไม่ประจำ", TaxSectionText: "40(2)", CalcMethodText: "ค่าแรงรายวัน",
                Amount: null, UnitText: "วัน", AccountText: "บัญชีเงินฝากธนาคาร",
                CalcTax: true, TaxRadioIndex: 1, FixedRatePercent: 0.07m, DeductBeforeTax: false,
                CalcSocialSecurity: true, CalcProvidentFund: true, CalcWelfareFund: true,
                BaseOvertime: false, BaseAbsenteeism: false, BaseBrokenline: false, BaseLeave: false,
                ByOtPeriod: false, ByAbsentPeriod: false, ByBrokenPeriod: false, ByLeavePeriod: false),

            // 7. INC005: บัญชีเงินสด | ค่าแรงรายชั่วโมง (หน่วย ชั่วโมง)
            new IncomeDeductRecord("INC005", "ค่าแรงประจำชั่วโมง", "Hourly Wage",
                TypeText: "รายได้ไม่ประจำ", TaxSectionText: "40(1)", CalcMethodText: "ค่าแรงรายชั่วโมง",
                Amount: null, UnitText: "ชั่วโมง", AccountText: "บัญชีเงินสด",
                CalcTax: true, TaxRadioIndex: null, FixedRatePercent: null, DeductBeforeTax: false,
                CalcSocialSecurity: true, CalcProvidentFund: true, CalcWelfareFund: true,
                BaseOvertime: false, BaseAbsenteeism: false, BaseBrokenline: false, BaseLeave: false,
                ByOtPeriod: false, ByAbsentPeriod: false, ByBrokenPeriod: false, ByLeavePeriod: false),

            // 8. INC006: บัญชีเงินฝากธนาคาร | อัตราส่วนต่อวัน (หน่วย วัน) | Fixed Rate 0.05%
            new IncomeDeductRecord("INC006", "ค่าแรงเหมางาน", "Contract Wage",
                TypeText: "รายได้ไม่ประจำ", TaxSectionText: "40(2)", CalcMethodText: "อัตราส่วนต่อวัน",
                Amount: null, UnitText: "วัน", AccountText: "บัญชีเงินฝากธนาคาร",
                CalcTax: true, TaxRadioIndex: 1, FixedRatePercent: 0.05m, DeductBeforeTax: false,
                CalcSocialSecurity: true, CalcProvidentFund: true, CalcWelfareFund: true,
                BaseOvertime: false, BaseAbsenteeism: false, BaseBrokenline: false, BaseLeave: false,
                ByOtPeriod: false, ByAbsentPeriod: false, ByBrokenPeriod: false, ByLeavePeriod: false),

            // 9. INC007: บัญชีเงินสด | 40(6)
            new IncomeDeductRecord("INC007", "ค่าเดินทาง", "Travel Allowance",
                TypeText: "รายได้ไม่ประจำ", TaxSectionText: "40(6)", CalcMethodText: null,
                Amount: null, UnitText: null, AccountText: "บัญชีเงินสด",
                CalcTax: false, TaxRadioIndex: null, FixedRatePercent: null, DeductBeforeTax: false,
                CalcSocialSecurity: false, CalcProvidentFund: false, CalcWelfareFund: false,
                BaseOvertime: false, BaseAbsenteeism: false, BaseBrokenline: false, BaseLeave: false,
                ByOtPeriod: false, ByAbsentPeriod: false, ByBrokenPeriod: false, ByLeavePeriod: false),

            // 10. INC008: บัญชีเงินฝากธนาคาร | 40(7)
            new IncomeDeductRecord("INC008", "ค่าอาหาร", "Meal Allowance",
                TypeText: "รายได้ไม่ประจำ", TaxSectionText: "40(7)", CalcMethodText: null,
                Amount: null, UnitText: null, AccountText: "บัญชีเงินฝากธนาคาร",
                CalcTax: false, TaxRadioIndex: null, FixedRatePercent: null, DeductBeforeTax: false,
                CalcSocialSecurity: false, CalcProvidentFund: false, CalcWelfareFund: false,
                BaseOvertime: false, BaseAbsenteeism: false, BaseBrokenline: false, BaseLeave: false,
                ByOtPeriod: false, ByAbsentPeriod: false, ByBrokenPeriod: false, ByLeavePeriod: false),

            // 11. INC009: บัญชีเงินสด | 40(8)
            new IncomeDeductRecord("INC009", "เงินรางวัลพิเศษ", "Special Incentive",
                TypeText: "รายได้ไม่ประจำ", TaxSectionText: "40(8)", CalcMethodText: null,
                Amount: null, UnitText: null, AccountText: "บัญชีเงินสด",
                CalcTax: false, TaxRadioIndex: null, FixedRatePercent: null, DeductBeforeTax: false,
                CalcSocialSecurity: false, CalcProvidentFund: false, CalcWelfareFund: false,
                BaseOvertime: false, BaseAbsenteeism: false, BaseBrokenline: false, BaseLeave: false,
                ByOtPeriod: false, ByAbsentPeriod: false, ByBrokenPeriod: false, ByLeavePeriod: false),

            // 12. INC010: บัญชีค่าใช้จ่าย | รายหักไม่ประจำ 40(1) | กำหนดเอง
            new IncomeDeductRecord("INC010", "หักกรณีขาดงาน", "Absence Deduction",
                TypeText: "รายหักไม่ประจำ", TaxSectionText: "40(1)", CalcMethodText: "กำหนดเอง",
                Amount: null, UnitText: null, AccountText: "บัญชีค่าใช้จ่าย",
                CalcTax: false, TaxRadioIndex: null, FixedRatePercent: null, DeductBeforeTax: true,
                CalcSocialSecurity: true, CalcProvidentFund: true, CalcWelfareFund: true,
                BaseOvertime: false, BaseAbsenteeism: false, BaseBrokenline: false, BaseLeave: false,
                ByOtPeriod: false, ByAbsentPeriod: false, ByBrokenPeriod: false, ByLeavePeriod: false),

            // 13. INC011: บัญชีค่าใช้จ่าย | รายหักไม่ประจำ 40(2) | จำนวนคงที่ 1500
            new IncomeDeductRecord("INC011", "หักกรณีมาสาย", "Late Deduction",
                TypeText: "รายหักไม่ประจำ", TaxSectionText: "40(2)", CalcMethodText: "จำนวนคงที่",
                Amount: 1500m, UnitText: null, AccountText: "บัญชีค่าใช้จ่าย",
                CalcTax: false, TaxRadioIndex: null, FixedRatePercent: null, DeductBeforeTax: true,
                CalcSocialSecurity: true, CalcProvidentFund: true, CalcWelfareFund: true,
                BaseOvertime: false, BaseAbsenteeism: false, BaseBrokenline: false, BaseLeave: false,
                ByOtPeriod: false, ByAbsentPeriod: false, ByBrokenPeriod: false, ByLeavePeriod: false)
        };

        // -------------------------------------------------------------
        // * 2. Validation Tests *
        // -------------------------------------------------------------
        public void RunAllValidationTests()
        {
            string stageName = "TestCase01-08_Validation";
            Window form;
            try
            {
                form = OpenIncomeDeductForm();
            }
            catch (Exception ex)
            {
                LogFailureCounted(stageName, $"เปิดฟอร์มไม่สำเร็จ: {ex.Message}");
                return;
            }

            var context = GetGeneralTab(form);

            RunValidationStep("TestCase01_MissingCode", () =>
                FillAndSaveThenAssert(context, form, "", "เงินเดือน", "Save",
                    "TestCase01_MissingCode", "รหัสรายได้ - รายหัก ไม่สามารถเป็นค่าว่าง"));

            RunValidationStep("TestCase02_MissingName", () =>
                FillAndSaveThenAssert(context, form, "DED001", "", "Save and New",
                    "TestCase02_MissingName", "ชื่อรายได้ - รายหัก ไม่สามารถเป็นค่าว่าง"));

            RunValidationStep("TestCase03_MissingBoth", () =>
                FillAndSaveThenAssert(context, form, "", "", "Save and New",
                    "TestCase03_MissingBoth",
                    "รหัสรายได้ - รายหัก ไม่สามารถเป็นค่าว่าง",
                    "ชื่อรายได้ - รายหัก ไม่สามารถเป็นค่าว่าง"));

            RunValidationStep("TestCase04_DuplicateCode", () =>
                FillAndSaveThenAssert(context, form, "INC001", "เงินเดือน", "Save",
                    "TestCase04_DuplicateCode", "รหัสรายได้ - รายหัก ห้ามซ้ำ"));

            RunValidationStep("TestCase05_DuplicateName", () =>
            {
                FillAndSaveThenAssert(context, form, "INC015", "เงินเดือน", "Save and Close",
                    "TestCase05_DuplicateName", "ชื่อรายได้ - รายหัก ห้ามซ้ำ");
                AssertFormStillOpen(form, "TestCase05_DuplicateName");
            });

            RunValidationStep("TestCase05_DuplicateName_Close_Yes", () =>
            {
                ClickCloseAndHandleDialog(form, "Yes", "TestCase05_DuplicateName_Close_Yes");
                AssertWarningAndFormOpen(form, "ชื่อรายได้ - รายหัก ห้ามซ้ำ", "TestCase05_DuplicateName_Close_Yes");
            });

            RunValidationStep("TestCase05_DuplicateName_Close_Cancel", () =>
            {
                ClickCloseAndHandleDialog(form, "Cancel", "TestCase05_DuplicateName_Close_Cancel");
                AssertFormStillOpen(form, "TestCase05_DuplicateName_Close_Cancel");
            });

            RunValidationStep("TestCase05_DuplicateName_Close_No", () =>
            {
                ClickCloseAndHandleDialog(form, "No", "TestCase05_DuplicateName_Close_No");
                AssertFormClosed(form, "TestCase05_DuplicateName_Close_No");
            });
        }

        // -------------------------------------------------------------
        // * 3. Edit Tests *
        // -------------------------------------------------------------
        public void RunEditTests()
        {
            var results = new List<(string TestName, string ExistingCode, string NewCode)>();

            TryAdd(results, PerformEdit("TestCase06_EditRow1", existingCode: "DED001",
                newCode: "DED001-EDIT", newNameTh: "เงินเดือนพื้นฐาน", newNameEn: "Basic Salary",
                saveButtonName: "Save", needsExplicitClose: true));

            TryAdd(results, PerformEdit("TestCase07_EditRow3", existingCode: "INC001",
                newCode: "INC001-EDIT", newNameTh: "หักผู้มีประกันสังคม", newNameEn: "Deduct Insured Person",
                saveButtonName: "Save and Close", needsExplicitClose: false));
        }

        // -------------------------------------------------------------
        // * 4. Edit Validation Tests *
        // -------------------------------------------------------------
        public void RunEditValidationTests()
        {
            string baseTestName = "TestCase08_EditValidation";
            Window form;
            try
            {
                RefreshGrid();
                form = ClickRowAndOpenEditForm("DED001-EDIT", baseTestName);
            }
            catch (Exception ex)
            {
                LogFailureCounted(baseTestName, $"เปิดฟอร์ม Edit ไม่สำเร็จ: {ex.Message}");
                return;
            }

            var context = GetGeneralTab(form);

            var codeBox = context.FindFirstDescendant(cf => cf.ByAutomationId(IdCode));
            bool isCodeEditable = codeBox != null && codeBox.Properties.IsEnabled.ValueOrDefault;

            if (isCodeEditable)
            {
                RunValidationStep($"{baseTestName}_MissingCode", () =>
                    FillAndSaveThenAssert(context, form, "", "เงินเดือนพื้นฐาน", "Save",
                        $"{baseTestName}_MissingCode", "รหัสรายได้ - รายหัก ไม่สามารถเป็นค่าว่าง"));
            }
            else
            {
                LogSuccessCounted($"{baseTestName}_MissingCode", "ช่องรหัสถูกล็อก (Read-Only) ในโหมดแก้ไข");
            }

            RunValidationStep($"{baseTestName}_MissingName", () =>
                FillAndSaveThenAssert(context, form, isCodeEditable ? "DED001-EDIT" : null, "", "Save and New",
                    $"{baseTestName}_MissingName", "ชื่อรายได้ - รายหัก ไม่สามารถเป็นค่าว่าง"));

            if (isCodeEditable)
            {
                RunValidationStep($"{baseTestName}_MissingBoth", () =>
                    FillAndSaveThenAssert(context, form, "", "", "Save and New",
                        $"{baseTestName}_MissingBoth",
                        "รหัสรายได้ - รายหัก ไม่สามารถเป็นค่าว่าง",
                        "ชื่อรายได้ - รายหัก ไม่สามารถเป็นค่าว่าง"));

                RunValidationStep($"{baseTestName}_DuplicateCode", () =>
                {
                    string dupCode = "INC001-EDIT";
                    FillAndSaveThenAssert(context, form, dupCode, "เงินเดือนพื้นฐาน", "Save",
                        $"{baseTestName}_DuplicateCode", "รหัสรายได้ - รายหัก ห้ามซ้ำ");
                });
            }
            else
            {
                LogSuccessCounted($"{baseTestName}_MissingBoth", "ช่องรหัสถูกล็อก (Read-Only) ในโหมดแก้ไข");
                LogSuccessCounted($"{baseTestName}_DuplicateCode", "ช่องรหัสถูกล็อก ไม่สามารถแก้ไขรหัสให้ซ้ำได้");
            }

            string subCase = $"{baseTestName}_DuplicateName";
            RunValidationStep(subCase, () =>
            {
                FillAndSaveThenAssert(context, form, isCodeEditable ? "DED001-EDIT" : null, "หักผู้มีประกันสังคม", "Save and Close",
                    subCase, "ชื่อรายได้ - รายหัก ห้ามซ้ำ");
                AssertFormStillOpen(form, subCase);
            });

            RunValidationStep($"{subCase}_Close_Yes", () =>
            {
                ClickCloseAndHandleDialog(form, "Yes", $"{subCase}_Close_Yes");
                AssertWarningAndFormOpen(form, "ชื่อรายได้ - รายหัก ห้ามซ้ำ", $"{subCase}_Close_Yes");
            });

            RunValidationStep($"{subCase}_Close_Cancel", () =>
            {
                ClickCloseAndHandleDialog(form, "Cancel", $"{subCase}_Close_Cancel");
                AssertFormStillOpen(form, $"{subCase}_Close_Cancel");
            });

            RunValidationStep($"{subCase}_Close_No", () =>
            {
                ClickCloseAndHandleDialog(form, "No", $"{subCase}_Close_No");
                AssertFormClosed(form, $"{subCase}_Close_No");
            });

            LogSuccessCounted(baseTestName, "รัน Validation Edit ครบทุกเคสแล้ว");
        }

        // -------------------------------------------------------------
        // * 5. Delete Tests (ลบ INC010 และ INC011) *
        // -------------------------------------------------------------
        public void RunDeleteTests()
        {
            string baseTestName = "TestCase09_Delete";

            RunDeleteCaseByCode(baseTestName, "INC010", confirmButton: "Yes", expectDeleted: true);
            RunDeleteCaseByCode(baseTestName, "INC011", confirmButton: "No", expectDeleted: false);

            LogSuccessCounted(baseTestName, "ทดสอบ Delete ครบทุกเคส");
            TestLogger.LogInfo("IncomeDeductInfoUC", $"สรุปผลรวมทั้งหมด: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");
        }

        private void RunDeleteCaseByCode(string baseTestName, string targetCode, string confirmButton, bool expectDeleted)
        {
            string testName = $"{baseTestName}_{targetCode}_{confirmButton}";
            try
            {
                RefreshGrid();

                var row = Retry(() => FindElementMatchingValue(targetCode), timeoutMs: 5000)
                          ?? throw new Exception($"หาแถวรหัส '{targetCode}' บนตารางหลักไม่เจอ");

                row.Focus();
                ClickElementByMouse(row);

                var deleteButton = Retry(() =>
                {
                    var btn = _mainWindow.FindFirstDescendant(cf => cf.ByName("Delete"));
                    return (btn != null && btn.Properties.IsEnabled.ValueOrDefault) ? btn : null;
                }, 3000) ?? throw new Exception("หาปุ่ม Delete ไม่เจอ หรือปุ่มยังไม่เปิดใช้งาน");

                ClickElementByMouse(deleteButton);

                AutomationElement? confirmDialog = FindAlertDialog();
                if (confirmDialog == null) throw new Exception("ไม่พบ Pop-up ยืนยันการลบ");

                ClickDialogButton(confirmDialog, confirmButton, "ยืนยันการลบ");
                WaitForClosed(() => confirmDialog);

                RefreshGrid();

                bool stillExists = VerifyRecordExistsInGrid(targetCode, timeoutMs: 3000, refreshGrid: false);

                if (expectDeleted)
                {
                    if (stillExists)
                    {
                        LogFailureCounted(testName, $"'{targetCode}' ลบข้อมูลไม่สำเร็จ");
                        throw new Exception($"'{targetCode}' ลบข้อมูลไม่สำเร็จ");
                    }
                    LogSuccessCounted(testName, $"'{targetCode}' ถูกลบสำเร็จ");
                }
                else
                {
                    if (!stillExists)
                    {
                        LogFailureCounted(testName, $"'{targetCode}' ลบข้อมูลผิดพลาด");
                        throw new Exception($"'{targetCode}' ลบข้อมูลผิดพลาด");
                    }
                    LogSuccessCounted(testName, $"'{targetCode}' ข้อมูลยังไม่ถูกลบ");
                }
            }
            catch (Exception ex)
            {
                LogFailureCounted(testName, $"ลบ '{targetCode}' ไม่สำเร็จ: {ex.Message}");
            }
        }

        public void RunAllTestSteps()
        {
            NavigateToIncomeDeductInfoUC();
            RunAllValidationTests();
            RunEditTests();
            RunEditValidationTests();
            RunDeleteTests();
        }

        // -------------------------------------------------------------
        // * Helpers & Field Actions *
        // -------------------------------------------------------------
        private void RunValidationStep(string testName, Action step)
        {
            try { step(); }
            catch (Exception ex) { LogFailureCounted(testName, $"ไม่ผ่าน: {ex.Message}"); }
        }

        private static void TryAdd(List<(string, string, string)> list, (string, string, string)? item)
        {
            if (item.HasValue) list.Add(item.Value);
        }

        private (string TestName, string ExistingCode, string NewCode)? PerformEdit(string testName, string existingCode,
            string newCode, string newNameTh, string newNameEn, string saveButtonName, bool needsExplicitClose)
        {
            try
            {
                Window form = ClickRowAndOpenEditForm(existingCode, testName);
                var context = GetGeneralTab(form);

                FillIncomeDeductBasicFields(context, newCode, newNameTh, newNameEn);

                if (!TrySaveAndHandleDuplicate(form, testName, newCode, saveButtonName))
                    return null;

                if (needsExplicitClose)
                {
                    ClickFormCloseButton(form);
                }

                WaitForClosed(() => FindOpenIncomeDeductForm());
                RefreshGrid();

                if (VerifyRecordExistsInGrid(newCode, refreshGrid: false))
                    LogSuccessCounted(testName, $"แก้ไข '{existingCode}' เป็น '{newCode}' สำเร็จ (พบในตารางหลัก)");
                else
                    LogFailureCounted(testName, $"แก้ไข '{existingCode}' เป็น '{newCode}' แล้วแต่ไม่พบในตารางหลัก");

                return (testName, existingCode, newCode);
            }
            catch (Exception ex)
            {
                LogFailureCounted(testName, $"แก้ไข '{existingCode}' ไม่สำเร็จ: {ex.Message}");
                return null;
            }
        }

        private void FillIncomeDeductBasicFields(AutomationElement context, string code, string nameTh, string nameEn)
        {
            FillTextBox(context, IdCode, code);
            FillTextBox(context, IdName, nameTh);
            FillTextBox(context, IdNameEng, nameEn);
        }

        private void FillIncomeDeductFieldsFull(Window form, AutomationElement context, IncomeDeductRecord r)
        {
            FillIncomeDeductBasicFields(context, r.Code, r.NameTh, r.NameEn);

            SelectLookupValue(context, IdIncomeDeductTypeLookup, r.TypeText);
            SelectLookupValue(context, IdTaxSectionLookup, r.TaxSectionText);

            if (r.CalcMethodText != null)
                SelectLookupValue(context, IdCalcMethodLookup, r.CalcMethodText);

            if (r.UnitText != null)
                SelectFromUomPicker(form, context, r.UnitText);
            else if (r.Amount.HasValue)
                SetAmount(context, r.Amount.Value);

            SelectAccount(form, r.AccountText);

            // เมื่อกรอกฟิลด์ด้านบนครบ จึงทำการติ๊ก Checkbox ป้องกันไม่ให้ค่าโดน Event ของโปรแกรมรีเซ็ต
            if (!IsRestrictedTaxSection(r.TaxSectionText))
            {
                ApplyTaxSettings(form, context, r);
                ApplyOtherCheckboxes(form, context, r);
            }
        }

        private static bool IsRestrictedTaxSection(string? taxSectionText) =>
            RestrictedTaxSections.Contains(taxSectionText?.Trim() ?? string.Empty);

        private AutomationElement GetGeneralTab(Window form)
        {
            var tab = Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(GeneralTabId))
                               ?? form.FindFirstDescendant(cf => cf.ByName("General"))
                               ?? form.FindFirstDescendant(cf => cf.ByName("ข้อมูลทั่วไป")), 3000);

            return tab ?? form;
        }

        private void SelectLookupValue(AutomationElement context, string lookupAutomationId, string valueText)
        {
            var lookup = Retry(() => context.FindFirstDescendant(cf => cf.ByAutomationId(lookupAutomationId)), 3000)
                ?? throw new Exception($"หา Lookup '{lookupAutomationId}' ไม่เจอ");

            lookup.Focus();

            try
            {
                var rect = lookup.BoundingRectangle;
                if (!rect.IsEmpty)
                {
                    Mouse.Click(new Point(rect.Right - 8, rect.Top + (rect.Height / 2)));
                }
            }
            catch { }

            Keyboard.Press(VirtualKeyShort.CONTROL);
            Keyboard.Type("a");
            Keyboard.Release(VirtualKeyShort.CONTROL);
            Keyboard.Press(VirtualKeyShort.BACK);

            Keyboard.Type(valueText);

            Keyboard.Press(VirtualKeyShort.ENTER);
            Keyboard.Release(VirtualKeyShort.ENTER);

            EnsureDropdownClosed(lookup);
        }

        private void EnsureDropdownClosed(AutomationElement lookup)
        {
            try
            {
                Keyboard.Press(VirtualKeyShort.ESCAPE);
                Keyboard.Release(VirtualKeyShort.ESCAPE);
            }
            catch { }
        }

        private void ClickPickerEdgeButton(AutomationElement pickerField)
        {
            var rect = pickerField.BoundingRectangle;
            int clickX = rect.Right - 12;
            int clickY = rect.Top + (rect.Height / 2);

            Mouse.MoveTo(new Point(clickX, clickY));
            Mouse.Click(MouseButton.Left);
        }

        private void SelectAccount(Window form, string accountText)
        {
            var accountEdit = form.FindFirstDescendant(cf => cf.ByAutomationId(IdAccountButton))
                           ?? form.FindFirstDescendant(cf => cf.ByAutomationId(IdAccountName))
                           ?? FindAccountOpenButton(form)
                           ?? throw new Exception("หาช่อง 'รหัสบัญชี' บนฟอร์มไม่เจอ");

            accountEdit.Focus();
            ClickPickerEdgeButton(accountEdit);

            Window? popup = FindAccountPopupWindow(form);
            if (popup == null)
            {
                try
                {
                    accountEdit.Focus();
                    Keyboard.Press(VirtualKeyShort.F4);
                    Keyboard.Release(VirtualKeyShort.F4);
                }
                catch { }
                popup = FindAccountPopupWindow(form);
            }

            if (popup == null)
                throw new Exception("เปิด popup 'กำหนดรหัสบัญชี' ไม่สำเร็จ");

            AutomationElement? accountRow = Retry(() =>
            {
                if (accountText.Contains("เงินสด"))
                {
                    return popup.FindFirstDescendant(cf => cf.ByName("ชื่อบัญชี row 1"))
                        ?? popup.FindFirstDescendant(cf => cf.ByName("BAN001"))
                        ?? popup.FindFirstDescendant(cf => cf.ByName("บัญชีเงินสด"));
                }

                if (accountText.Contains("ธนาคาร"))
                {
                    return popup.FindFirstDescendant(cf => cf.ByName("ชื่อบัญชี row 2"))
                        ?? popup.FindFirstDescendant(cf => cf.ByName("BAN002"))
                        ?? popup.FindFirstDescendant(cf => cf.ByName("บัญชีเงินฝากธนาคาร"));
                }

                if (accountText.Contains("ค่าใช้จ่าย"))
                {
                    return popup.FindFirstDescendant(cf => cf.ByName("ชื่อบัญชี row 3"))
                        ?? popup.FindFirstDescendant(cf => cf.ByName("BAN003"))
                        ?? popup.FindFirstDescendant(cf => cf.ByName("บัญชีค่าใช้จ่าย"));
                }

                return FindAccountRowByText(popup, accountText);
            }, timeoutMs: 4000);

            if (accountRow == null)
                throw new Exception($"หาบัญชี '{accountText}' ใน popup 'กำหนดรหัสบัญชี' ไม่เจอ");

            ClickElementByMouse(accountRow);

            var selectButton = Retry(() =>
            {
                var btn = popup.FindFirstDescendant(cf => cf.ByName("Select"))
                       ?? popup.FindFirstDescendant(cf => cf.ByName("เลือก"))
                       ?? popup.FindFirstDescendant(cf => cf.ByAutomationId("btnSelect"));
                return (btn != null && btn.Properties.IsEnabled.ValueOrDefault) ? btn : null;
            }, timeoutMs: 5000) ?? throw new Exception("ไม่พบปุ่ม Select บน Popup 'กำหนดรหัสบัญชี'");

            var btnControl = selectButton.AsButton();
            if (btnControl != null)
            {
                try { btnControl.Focus(); } catch { }
                btnControl.Click();
            }
            else
            {
                ClickElementByMouse(selectButton);
            }

            WaitForClosed(() => popup);
        }

        private AutomationElement? FindAccountRowByText(Window popup, string accountText)
        {
            var cleanTarget = accountText.Trim();

            var rows = popup.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem));
            foreach (var row in rows)
            {
                if (!string.IsNullOrEmpty(row.Name) && row.Name.IndexOf(cleanTarget, StringComparison.OrdinalIgnoreCase) >= 0)
                    return row;

                foreach (var text in GetPanelTexts(row))
                {
                    if (text.IndexOf(cleanTarget, StringComparison.OrdinalIgnoreCase) >= 0)
                        return row;
                }
            }

            return null;
        }

        private Window? FindAccountPopupWindow(Window form)
        {
            return Retry(() =>
                form.ModalWindows.FirstOrDefault(w => w.Name.Contains("กำหนดรหัสบัญชี") && !w.Properties.IsOffscreen.ValueOrDefault)
                ?? _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault(w => w.Name.Contains("กำหนดรหัสบัญชี") && !w.Properties.IsOffscreen.ValueOrDefault)
                ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("กำหนดรหัสบัญชี"))?.AsWindow(),
                timeoutMs: 3000);
        }

        private AutomationElement? FindAccountOpenButton(Window form)
        {
            var accountLabel = form.FindFirstDescendant(cf => cf.ByName("รหัสบัญชี"));
            if (accountLabel == null) return null;

            var labelRect = accountLabel.BoundingRectangle;

            return form.FindAllDescendants()
                .Where(btn =>
                {
                    var rectBtn = btn.BoundingRectangle;
                    bool sameRow = Math.Abs(rectBtn.Top - labelRect.Top) <= 20;
                    bool rightSide = rectBtn.Left > labelRect.Right;
                    return sameRow && rightSide && rectBtn.Width > 0;
                })
                .OrderBy(btn => btn.BoundingRectangle.Left)
                .FirstOrDefault();
        }

        private void ClickElementByMouse(AutomationElement element)
        {
            var rect = element.BoundingRectangle;
            if (rect.IsEmpty)
            {
                element.Focus();
                return;
            }
            Mouse.Click(new Point(rect.Left + (rect.Width / 2), rect.Top + (rect.Height / 2)));
        }

        private void SelectFromUomPicker(Window form, AutomationElement context, string unitText)
        {
            var uomField = context.FindFirstDescendant(cf => cf.ByAutomationId(IdUomButton))
                        ?? form.FindFirstDescendant(cf => cf.ByAutomationId(IdUomButton))
                        ?? FindUomOpenButton(context);

            if (uomField == null) return;
            if (!uomField.Properties.IsEnabled.ValueOrDefault) return;

            try { uomField.Focus(); } catch { }

            ClickPickerEdgeButton(uomField);

            Window? popup = FindUomPopupWindow(form);
            if (popup == null)
            {
                try
                {
                    uomField.Focus();
                    Keyboard.Press(VirtualKeyShort.F4);
                    Keyboard.Release(VirtualKeyShort.F4);
                }
                catch { }
                popup = FindUomPopupWindow(form);
            }

            if (popup == null) return;

            AutomationElement? unitRow = Retry(() =>
            {
                var match = popup.FindFirstDescendant(cf => cf.ByName(unitText));
                if (match != null) return match;

                if (unitText == "วัน" || unitText == "001")
                    return popup.FindFirstDescendant(cf => cf.ByName("รหัสหน่วยนับ row 0"))
                        ?? popup.FindFirstDescendant(cf => cf.ByName("001"));
                if (unitText == "ชั่วโมง" || unitText == "002")
                    return popup.FindFirstDescendant(cf => cf.ByName("รหัสหน่วยนับ row 1"))
                        ?? popup.FindFirstDescendant(cf => cf.ByName("002"));

                return popup.FindFirstDescendant(cf => cf.ByName("Data Panel"))?.FindFirstDescendant(cf => cf.ByName(unitText));
            }, timeoutMs: 3000);

            if (unitRow != null)
            {
                ClickElementByMouse(unitRow);
            }

            var selectButton = Retry(() =>
            {
                var btn = popup.FindFirstDescendant(cf => cf.ByName("Select"))
                       ?? popup.FindFirstDescendant(cf => cf.ByAutomationId("btnSelect"));
                return (btn != null && btn.Properties.IsEnabled.ValueOrDefault) ? btn : null;
            }, timeoutMs: 3000);

            if (selectButton != null)
            {
                var btnControl = selectButton.AsButton();
                if (btnControl != null) btnControl.Click();
                else ClickElementByMouse(selectButton);
            }

            WaitForClosed(() => popup, 3000);
        }

        private AutomationElement? FindUomOpenButton(AutomationElement context)
        {
            var uomLabel = context.FindFirstDescendant(cf => cf.ByName("หน่วย"));
            if (uomLabel == null) return null;

            var labelRect = uomLabel.BoundingRectangle;

            return context.FindAllDescendants()
                .Where(btn =>
                {
                    var rectBtn = btn.BoundingRectangle;
                    bool sameRow = Math.Abs(rectBtn.Top - labelRect.Top) <= 20;
                    bool rightSide = rectBtn.Left > labelRect.Right;
                    return sameRow && rightSide && rectBtn.Width > 0;
                })
                .OrderBy(btn => btn.BoundingRectangle.Left)
                .FirstOrDefault();
        }

        private Window? FindUomPopupWindow(Window form)
        {
            return Retry(() =>
                form.ModalWindows.FirstOrDefault(w => w.Name.Contains("กำหนดรหัสหน่วยนับ") && !w.Properties.IsOffscreen.ValueOrDefault)
                ?? _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault(w => w.Name.Contains("กำหนดรหัสหน่วยนับ") && !w.Properties.IsOffscreen.ValueOrDefault)
                ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("กำหนดรหัสหน่วยนับ"))?.AsWindow(),
                timeoutMs: 3000);
        }

        // ── จัดการภาษี: ติ๊กคำนวณภาษี และเลือกวิธีการคิดภาษี (อัตราก้าวหน้า / Fixed Rate) ──
        private void ApplyTaxSettings(Window form, AutomationElement context, IncomeDeductRecord r)
        {
            TickCheckbox(form, context, IdChkCalcTax, r.CalcTax, "คำนวณภาษี");

            if (r.TaxRadioIndex.HasValue)
            {
                try
                {
                    SelectTaxMethodRadio(form, r.TaxRadioIndex.Value);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[IncomeDeductInfoUC] ข้ามการเลือก Radio index {r.TaxRadioIndex}: {ex.Message}");
                }
            }

            if (r.FixedRatePercent.HasValue)
            {
                string rateFieldId = (r.TaxRadioIndex == 2) ? IdTaxWithholdingRate : IdTaxFixedRate;
                var rateElement = context.FindFirstDescendant(cf => cf.ByAutomationId(rateFieldId));

                if (rateElement != null && rateElement.Properties.IsEnabled.ValueOrDefault)
                {
                    SetSpinValue(context, rateFieldId, r.FixedRatePercent.Value);
                }
            }
        }

        // ── ติ๊ก Checkbox กลุ่มอื่นๆ ครบทุกหัวข้อตาม Test Step ──
        private void ApplyOtherCheckboxes(Window form, AutomationElement context, IncomeDeductRecord r)
        {
            TickCheckbox(form, context, IdChkDeductBeforeTax, r.DeductBeforeTax, "หักก่อนคำนวณภาษี");

            // หมวดรวมฐานคำนวณ
            TickCheckbox(form, context, IdChkBaseOvertime, r.BaseOvertime, "รวมฐานคำนวณค่าล่วงเวลา", "ค่าล่วงเวลา");
            TickCheckbox(form, context, IdChkBaseAbsenteeism, r.BaseAbsenteeism, "รวมฐานคำนวณการหักขาดงาน", "การหักขาดงาน");
            TickCheckbox(form, context, IdChkBaseBrokenline, r.BaseBrokenline, "รวมฐานคำนวณการหักสาย ออกก่อน", "การหักสาย ออกก่อน", "หักสาย");
            TickCheckbox(form, context, IdChkBaseLeave, r.BaseLeave, "รวมฐานคำนวณการหักลางาน", "รวมฐานคำนวณการหักลา", "การหักลางาน", "การหักลา");

            // หมวดแยกตามงวด
            TickCheckbox(form, context, IdChkByOtPeriod, r.ByOtPeriod, "แยกตามงวดการคิดค่าล่วงเวลา");
            TickCheckbox(form, context, IdChkByAbsentPeriod, r.ByAbsentPeriod, "แยกตามงวดการคิดหักขาดงาน");
            TickCheckbox(form, context, IdChkByBrokenPeriod, r.ByBrokenPeriod, "แยกตามงวดการคิดหักสาย ออกก่อน");
            TickCheckbox(form, context, IdChkByLeavePeriod, r.ByLeavePeriod, "แยกตามงวดการคิดหักลางาน", "แยกตามงวดการคิดหักลา");

            // หมวดประกันสังคมและกองทุน
            TickCheckbox(form, context, IdChkCalcSocialSecurity, r.CalcSocialSecurity, "คำนวณประกันสังคม", "หักก่อนคำนวณประกันสังคม");
            TickCheckbox(form, context, IdChkProvidentFund, r.CalcProvidentFund, "คำนวณกองทุนสำรอง", "หักก่อนคำนวณกองทุนสำรอง");
            TickCheckbox(form, context, IdChkWelfareFundEwf, r.CalcWelfareFund, "คำนวณกองทุนสงเคราะห์ลูกจ้าง", "หักก่อนคำนวณกองทุนสงเคราะห์ลูกจ้าง");
        }

        private void SetAmount(AutomationElement context, decimal amount) => SetSpinValue(context, IdAmount, amount);

        private void SetSpinValue(AutomationElement context, string automationId, decimal value)
        {
            var el = context.FindFirstDescendant(cf => cf.ByAutomationId(automationId))
                ?? throw new Exception($"หาช่อง '{automationId}' ไม่เจอ");

            string textValue = value.ToString(System.Globalization.CultureInfo.InvariantCulture);

            if (el.Patterns.Value.IsSupported)
            {
                try
                {
                    el.Patterns.Value.Pattern.SetValue(textValue);
                    return;
                }
                catch { }
            }

            ClickElementByMouse(el);
            try { el.Focus(); } catch { }

            Keyboard.Press(VirtualKeyShort.CONTROL);
            Keyboard.Type("a");
            Keyboard.Release(VirtualKeyShort.CONTROL);
            Keyboard.Press(VirtualKeyShort.BACK);
            Keyboard.Press(VirtualKeyShort.DELETE);

            Keyboard.Type(textValue);
        }

        private void CloseAnyOpenModalForm()
        {
            try
            {
                var openForm = FindOpenIncomeDeductForm();
                if (openForm != null)
                {
                    ClickFormCloseButton(openForm);
                    HandleConfirmDialogIfPresent("No", openForm);
                    WaitForClosed(() => openForm, 2000);
                }
            }
            catch { }
        }

        private Window WaitForFormReady(Func<Window?> finder, int timeoutMs = 8000)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                var w = finder();
                if (w != null && w.IsAvailable && !w.Properties.IsOffscreen.ValueOrDefault)
                {
                    var codeBox = w.FindFirstDescendant(cf => cf.ByAutomationId(IdCode));
                    if (codeBox != null && codeBox.IsAvailable)
                    {
                        return w;
                    }
                }
                Thread.Sleep(100);
            }
            throw new Exception("หน้าต่างฟอร์มเปิดขึ้นมาแต่คอนโทรลภายในยังไม่พร้อมทำงาน");
        }

        private Window OpenNewIncomeDeductForm(string testName)
        {
            CloseAnyOpenModalForm();

            var newButton = Retry(() =>
            {
                var btn = _mainWindow.FindFirstDescendant(cf => cf.ByName("New"));
                return (btn != null && btn.Properties.IsEnabled.ValueOrDefault) ? btn : null;
            }, 3000) ?? throw new Exception("หาปุ่ม New บนตารางหลักไม่เจอ หรือปุ่มยังไม่พร้อมใช้งาน");

            ClickElementByMouse(newButton);
            return WaitForFormReady(FindOpenIncomeDeductForm);
        }

        private Window OpenIncomeDeductForm()
        {
            CloseAnyOpenModalForm();

            var newBtn = Retry(() =>
            {
                var btn = _mainWindow.FindFirstDescendant(cf => cf.ByName("New"));
                return (btn != null && btn.Properties.IsEnabled.ValueOrDefault) ? btn : null;
            }, 3000);

            if (newBtn != null) ClickElementByMouse(newBtn);

            return WaitForFormReady(FindOpenIncomeDeductForm);
        }

        private Window? FindOpenIncomeDeductForm()
        {
            var modal = _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault(w =>
                (w.AutomationId == FormId ||
                w.Name.Contains("กำหนดรหัสรายได้") ||
                w.Name.Contains("รายได้ - รายหัก")) && !w.Properties.IsOffscreen.ValueOrDefault);

            if (modal != null) return modal;

            return _automation.GetDesktop().FindFirstChild(cf =>
                (cf.ByAutomationId(FormId)
                .Or(cf.ByName(FormTitle))
                .Or(cf.ByName("กำหนดรหัสรายได้-รายหัก"))
                .Or(cf.ByName("กำหนดรหัส รายได้ - รายหัก"))))?.AsWindow();
        }

        private Window ClickRowAndOpenEditForm(string codeToFind, string testName)
        {
            AutomationElement? row = Retry(() => FindElementMatchingValue(codeToFind), timeoutMs: 4000);
            if (row == null)
            {
                RefreshGrid();
                row = Retry(() => FindElementMatchingValue(codeToFind), timeoutMs: 4000)
                      ?? throw new Exception($"หาแถวสำหรับ '{codeToFind}' ไม่เจอ");
            }

            ClickElementByMouse(row);

            var editButton = Retry(() =>
            {
                var btn = _mainWindow.FindFirstDescendant(cf => cf.ByName("Edit"));
                return (btn != null && btn.Properties.IsEnabled.ValueOrDefault) ? btn : null;
            }, 3000) ?? throw new Exception("หาปุ่ม Edit ไม่เจอ หรือปุ่มยังไม่พร้อมใช้งาน");

            ClickElementByMouse(editButton);
            return WaitForFormReady(FindOpenIncomeDeductForm);
        }

        private bool TrySaveAndHandleDuplicate(Window form, string testName, string code, string saveButtonName)
        {
            ClickFormButton(form, saveButtonName);

            if (testName.Contains("Validation")) return true;

            string? duplicateReason = TryDetectDuplicateWarning(form);
            if (duplicateReason == null) return true;

            LogFailureCounted(testName, $"'{code}' รหัสซ้ำ/ชื่อซ้ำ: {duplicateReason}");
            try { ClickCloseAndHandleDialog(form, "No", $"{testName}_Close"); }
            catch (Exception closeEx) { LogFailureCounted(testName, $"ปิดฟอร์มไม่สำเร็จ: {closeEx.Message}"); }
            return false;
        }

        private void FillAndSaveThenAssert(AutomationElement context, Window form, string? code, string name,
            string saveButtonName, string testName, params string[] expectedMessages)
        {
            if (code != null) FillTextBox(context, IdCode, code);
            FillTextBox(context, IdName, name);

            form.Focus();

            ClickFormButton(form, saveButtonName);

            var pending = new HashSet<string>(expectedMessages);
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < 5000 && pending.Count > 0)
            {
                try
                {
                    var taskResultPanel = FindTaskResultPanel(form, timeoutMs: 500);
                    if (taskResultPanel != null)
                    {
                        var texts = GetPanelTexts(taskResultPanel);
                        foreach (var m in pending.ToList())
                        {
                            string cleanExpected = m.Replace(" ", "").Trim();
                            if (texts.Any(t => t.Replace(" ", "").Contains(cleanExpected)))
                                pending.Remove(m);
                        }
                    }
                }
                catch { }
                if (pending.Count == 0) break;
                Thread.Sleep(100);
            }

            if (pending.Count > 0)
                throw new Exception($"ไม่พบข้อความ: '{string.Join("', '", pending)}'");

            string formattedList = string.Join(",", expectedMessages.Select(m => $"'{m}'"));
            LogSuccessCounted(testName, $"พบข้อความ: {formattedList}");
        }

        private void AssertWarningAndFormOpen(Window form, string expectedWarningMessage, string testName)
        {
            bool foundMessage = false;
            var sw = Stopwatch.StartNew();
            string cleanExpected = expectedWarningMessage.Replace(" ", "").Trim();

            while (sw.ElapsedMilliseconds < 5000)
            {
                try
                {
                    var taskResultPanel = FindTaskResultPanel(form, timeoutMs: 500);
                    if (taskResultPanel != null)
                    {
                        var texts = GetPanelTexts(taskResultPanel);
                        if (texts.Any(t => t.Replace(" ", "").Contains(cleanExpected)))
                        {
                            foundMessage = true;
                            break;
                        }
                    }
                }
                catch { }
                Thread.Sleep(100);
            }

            bool isOpen = WaitUntil(() => !form.Properties.IsOffscreen.ValueOrDefault && form.IsAvailable, 2000);
            if (!foundMessage || !isOpen)
                throw new Exception($"ตรวจสอบไม่ผ่าน (พบข้อความ: {foundMessage}, ฟอร์มเปิดอยู่: {isOpen})");

            LogSuccessCounted(testName, $"พบข้อความ: '{expectedWarningMessage}' และ ฟอร์มถูกเปิดอยู่");
        }

        private void ClickFormButton(Window form, string buttonName, int timeoutMs = 3000)
        {
            var button = Retry(() =>
            {
                var btn = form.FindFirstDescendant(cf => cf.ByName(buttonName) ?? cf.ByName($"&{buttonName}"));
                return (btn != null && btn.Properties.IsEnabled.ValueOrDefault) ? btn : null;
            }, timeoutMs) ?? throw new Exception($"หาปุ่ม '{buttonName}' บนฟอร์มไม่เจอ หรือปุ่มถูกปิดการใช้งาน");

            ClickElementByMouse(button);
        }

        private void ClickFormCloseButton(Window form)
        {
            var closeBtn = Retry(() =>
            {
                var btn = form.FindAllDescendants(cf => cf.ByControlType(ControlType.Button).And(cf.ByName("Close")))
                              .FirstOrDefault(b => b.Properties.IsEnabled.ValueOrDefault)
                          ?? form.FindFirstDescendant(cf => cf.ByName("Close"));
                return (btn != null && btn.Properties.IsEnabled.ValueOrDefault) ? btn : null;
            }, 3000) ?? throw new Exception("หาปุ่ม Close บนฟอร์มหลักไม่เจอ");

            ClickElementByMouse(closeBtn);
        }

        private string? TryDetectDuplicateWarning(Window form)
        {
            var taskResultPanel = FindTaskResultPanel(form, timeoutMs: 1500);
            if (taskResultPanel == null) return null;

            foreach (var text in GetPanelTexts(taskResultPanel))
            {
                if (text.Contains("รหัสรายได้ - รายหัก ห้ามซ้ำ")) return "รหัสซ้ำ";
                if (text.Contains("ชื่อรายได้ - รายหัก ห้ามซ้ำ")) return "ชื่อซ้ำ";
            }
            return null;
        }

        private AutomationElement? FindTaskResultPanel(Window form, int timeoutMs = 3000) =>
            Retry(() =>
            {
                var panel = form.FindFirstDescendant(cf => cf.ByName("Task Result").Or(cf.ByAutomationId("dockPanelTaskResult")))
                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("Task Result").Or(cf.ByAutomationId("dockPanelTaskResult")));
                return (panel != null && !panel.Properties.IsOffscreen.ValueOrDefault) ? panel : null;
            }, timeoutMs);

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

        // ── แก้ไขจุดค้าง: รีเฟรชตารางแบบไม่ทำให้ Thread ค้าง และคืน Focus ให้ DataGrid เสมอ ──
        private void RefreshGrid()
        {
            try
            {
                var dataGrid = GetMainDataGrid();
                if (dataGrid != null && dataGrid.IsAvailable)
                {
                    dataGrid.Focus();
                    Keyboard.Press(VirtualKeyShort.ESCAPE);
                    Keyboard.Press(VirtualKeyShort.F5);
                    Keyboard.Press(VirtualKeyShort.HOME);
                    Thread.Sleep(200);
                    return;
                }

                // Fallback: หากดีย์บอร์ดไม่ติด จึงใช้ปุ่ม Find แบบไม่บล็อกโฟกัส
                var findBtn = _mainWindow.FindFirstDescendant(cf => cf.ByName("Find").Or(cf.ByAutomationId("btnFind")));
                if (findBtn != null && findBtn.Properties.IsEnabled.ValueOrDefault)
                {
                    ClickElementByMouse(findBtn);
                }
            }
            catch { }

            WaitUntil(() =>
            {
                var grid = GetMainDataGrid();
                return grid != null && grid.IsAvailable && !grid.Properties.IsOffscreen.ValueOrDefault;
            }, 3000);
        }

        private AutomationElement? GetMainDataGrid() =>
            _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataGrid))
            ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Table));

        private void ScrollGridVerticalToTop(AutomationElement grid)
        {
            try
            {
                var scrollBar = grid.FindFirstDescendant(cf => cf.ByControlType(ControlType.ScrollBar))
                    ?? grid.FindAllDescendants(cf => cf.ByControlType(ControlType.ScrollBar)).FirstOrDefault();

                if (scrollBar != null && scrollBar.Patterns.Scroll.IsSupported)
                {
                    scrollBar.Patterns.Scroll.Pattern.Scroll(ScrollAmount.NoAmount, ScrollAmount.LargeDecrement);
                    return;
                }

                grid.Focus();
                Keyboard.Press(VirtualKeyShort.HOME);
            }
            catch
            {
                try { grid.Focus(); Keyboard.Press(VirtualKeyShort.HOME); } catch { }
            }
        }

        private bool ScrollGridVerticalStepDown(AutomationElement grid)
        {
            try
            {
                var scrollBar = grid.FindFirstDescendant(cf => cf.ByControlType(ControlType.ScrollBar))
                    ?? grid.FindAllDescendants(cf => cf.ByControlType(ControlType.ScrollBar)).FirstOrDefault();

                if (scrollBar != null && scrollBar.Patterns.Scroll.IsSupported)
                {
                    var scrollPattern = scrollBar.Patterns.Scroll.Pattern;
                    double before = -1;
                    try { before = scrollPattern.VerticalScrollPercent.Value; } catch { }

                    scrollPattern.Scroll(ScrollAmount.NoAmount, ScrollAmount.SmallIncrement);

                    if (before >= 0)
                    {
                        try
                        {
                            double after = scrollPattern.VerticalScrollPercent.Value;
                            if (Math.Abs(after - before) < 0.01) return false;
                        }
                        catch { }
                    }
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private AutomationElement? FindElementMatchingValueDeep(string value, int maxScrollSteps = 20)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            var found = FindElementMatchingValue(value);
            if (found != null) return found;

            var grid = GetMainDataGrid();
            if (grid == null) return null;

            ScrollGridVerticalToTop(grid);

            found = FindElementMatchingValue(value);
            if (found != null) return found;

            for (int i = 0; i < maxScrollSteps; i++)
            {
                bool moved = ScrollGridVerticalStepDown(grid);

                found = FindElementMatchingValue(value);
                if (found != null) return found;

                if (!moved) break;
            }

            return null;
        }

        private bool FindRecordExistsRobust(string value, int fastTimeoutMs = 3000)
        {
            if (Retry(() => FindElementMatchingValue(value), fastTimeoutMs) != null) return true;
            return FindElementMatchingValueDeep(value) != null;
        }

        private bool VerifyRecordExistsInGrid(string value, int timeoutMs = 3000, bool refreshGrid = true)
        {
            if (refreshGrid) RefreshGrid();
            return FindRecordExistsRobust(value, timeoutMs);
        }

        private AutomationElement? FindElementMatchingValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            var dataGrid = _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataGrid))
                           ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Table));

            var container = dataGrid ?? _mainWindow;

            return ScanCurrentRows(container, value);
        }

        private AutomationElement? ScanCurrentRows(AutomationElement container, string value)
        {
            var rows = container.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem));
            foreach (var row in rows)
            {
                if (!string.IsNullOrEmpty(row.Name) && string.Equals(row.Name.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase))
                    return row;

                foreach (var text in GetPanelTexts(row))
                {
                    if (string.Equals(text.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase))
                        return row;
                }

                var cells = row.FindAllChildren();
                foreach (var cell in cells)
                {
                    if (!string.IsNullOrEmpty(cell.Name) && string.Equals(cell.Name.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase))
                        return row;

                    try
                    {
                        if (cell.Patterns.Value.IsSupported &&
                            string.Equals(cell.Patterns.Value.Pattern.Value.Value?.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase))
                            return row;
                    }
                    catch { }

                    try
                    {
                        if (cell.Patterns.LegacyIAccessible.IsSupported)
                        {
                            var accVal = cell.Patterns.LegacyIAccessible.Pattern.Value.ValueOrDefault;
                            if (string.Equals(accVal?.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase))
                                return row;
                        }
                    }
                    catch { }
                }
            }

            foreach (var el in container.FindAllDescendants())
            {
                if (!string.IsNullOrEmpty(el.Name) && string.Equals(el.Name.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase))
                    return el;

                try
                {
                    if (el.Patterns.Value.IsSupported &&
                        string.Equals(el.Patterns.Value.Pattern.Value.Value?.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase))
                        return el;
                }
                catch { }

                try
                {
                    if (el.Patterns.LegacyIAccessible.IsSupported)
                    {
                        var accVal = el.Patterns.LegacyIAccessible.Pattern.Value.ValueOrDefault;
                        if (string.Equals(accVal?.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase))
                            return el;
                    }
                }
                catch { }
            }

            return null;
        }

        private AutomationElement? FindAlertDialog(Window? relatedForm = null)
        {
            return Retry(() =>
            {
                if (relatedForm != null)
                {
                    try
                    {
                        var formModal = relatedForm.ModalWindows.FirstOrDefault(w => !w.Properties.IsOffscreen.ValueOrDefault);
                        if (formModal != null) return formModal;
                    }
                    catch { }
                }

                var mainModal = _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault(w => !w.Properties.IsOffscreen.ValueOrDefault);
                if (mainModal != null) return mainModal;

                if (relatedForm != null)
                {
                    var containerInForm = relatedForm.FindFirstDescendant(cf =>
                        cf.ByName("&Yes").Or(cf.ByName("&No")).Or(cf.ByName("&Cancel"))
                          .Or(cf.ByName("Yes")).Or(cf.ByName("No")).Or(cf.ByName("Cancel"))
                    )?.Parent;
                    if (containerInForm != null && !containerInForm.Properties.IsOffscreen.ValueOrDefault) return containerInForm;
                }

                var dialogContainer = _mainWindow.FindFirstDescendant(cf =>
                    cf.ByName("&Yes").Or(cf.ByName("&No")).Or(cf.ByName("&Cancel"))
                      .Or(cf.ByName("Yes")).Or(cf.ByName("No")).Or(cf.ByName("Cancel"))
                )?.Parent;
                if (dialogContainer != null && !dialogContainer.Properties.IsOffscreen.ValueOrDefault) return dialogContainer;

                try
                {
                    var topWindows = _automation.GetDesktop().FindAllChildren(cf => cf.ByControlType(ControlType.Window));
                    foreach (var win in topWindows)
                    {
                        if (win == null || !win.IsAvailable || win.Properties.IsOffscreen.ValueOrDefault) continue;

                        bool nameMatches = !string.IsNullOrEmpty(win.Name) &&
                                           (win.Name.Contains("คำเตือน") || win.Name.Contains("Question"));

                        if (nameMatches) return win;

                        try
                        {
                            var rect = win.BoundingRectangle;
                            bool looksLikeSmallDialog = rect.Width > 0 && rect.Width < 500
                                                      && rect.Height > 0 && rect.Height < 400;
                            if (!looksLikeSmallDialog) continue;

                            var hasConfirmButtons = win.FindFirstDescendant(cf =>
                                cf.ByName("&Yes").Or(cf.ByName("&No")).Or(cf.ByName("&Cancel"))
                                  .Or(cf.ByName("Yes")).Or(cf.ByName("No")).Or(cf.ByName("Cancel"))) != null;

                            if (hasConfirmButtons) return win;
                        }
                        catch { }
                    }
                }
                catch { }

                return null;
            }, timeoutMs: 3000);
        }

        private void ClickCloseAndHandleDialog(Window form, string buttonToClick, string testName)
        {
            ClickFormCloseButton(form);

            AutomationElement? confirmDialog = FindAlertDialog(form);
            if (confirmDialog == null) throw new Exception("กด Close แล้วไม่พบ Pop-up ยืนยัน");

            ClickDialogButton(confirmDialog, buttonToClick, "ยืนยัน");
            WaitForClosed(() => confirmDialog);
        }

        private void HandleConfirmDialogIfPresent(string buttonToClick, Window? relatedForm = null)
        {
            try
            {
                AutomationElement? confirmDialog = FindAlertDialog(relatedForm);
                if (confirmDialog != null)
                {
                    ClickDialogButton(confirmDialog, buttonToClick, "เคลียร์ Pop-up");
                    WaitForClosed(() => confirmDialog);
                }
            }
            catch { }
        }

        private void ClickDialogButton(AutomationElement dialog, string buttonName, string dialogDescription = "")
        {
            string ampersandName = $"&{buttonName}";
            string altThName = buttonName switch
            {
                "Yes" => "ใช่",
                "No" => "ไม่",
                "Cancel" => "ยกเลิก",
                _ => buttonName
            };

            var btn = dialog.FindFirstDescendant(cf => cf.ByName(ampersandName))
                      ?? dialog.FindFirstDescendant(cf => cf.ByName(buttonName))
                      ?? dialog.FindFirstDescendant(cf => cf.ByName($"&{altThName}"))
                      ?? dialog.FindFirstDescendant(cf => cf.ByName(altThName));

            if (btn == null)
            {
                var allButtons = dialog.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
                btn = allButtons.FirstOrDefault(b =>
                    !string.IsNullOrEmpty(b.Name) &&
                    (b.Name.Contains(buttonName, StringComparison.OrdinalIgnoreCase) ||
                     b.Name.Contains(altThName, StringComparison.OrdinalIgnoreCase)));
            }

            if (btn == null)
                throw new Exception($"ไม่พบปุ่ม '{buttonName}' (หรือ '{ampersandName}') บน Pop-up {dialogDescription}");

            var btnControl = btn.AsButton();
            if (btnControl != null)
            {
                try { btnControl.Focus(); } catch { }
                try
                {
                    btnControl.Invoke();
                    return;
                }
                catch { }
            }

            ClickElementByMouse(btn);
        }

        private void AssertFormStillOpen(Window form, string testName)
        {
            bool isOpen = WaitUntil(() => !form.Properties.IsOffscreen.ValueOrDefault && form.IsAvailable, 2000);
            if (!isOpen) throw new Exception("ปิดฟอร์มไม่สำเร็จ");
            LogSuccessCounted(testName, "ฟอร์มถูกเปิดอยู่");
        }

        private void AssertFormClosed(Window form, string testName)
        {
            bool isClosed = WaitForClosed(() => FindOpenIncomeDeductForm(), 3000);
            if (!isClosed) throw new Exception("ปิดฟอร์มไม่สำเร็จ");
            LogSuccessCounted(testName, "ฟอร์มถูกปิด");
        }

        private void AssertTaskResultWarning(Window form, string expectedWarningMessage, string testName)
        {
            bool foundMessage = false;
            var sw = Stopwatch.StartNew();

            string cleanExpected = expectedWarningMessage.Replace(" ", "").Trim();

            while (sw.ElapsedMilliseconds < 5000)
            {
                try
                {
                    var taskResultPanel = FindTaskResultPanel(form, timeoutMs: 500);
                    if (taskResultPanel != null)
                    {
                        var texts = GetPanelTexts(taskResultPanel);
                        if (texts.Any(t => t.Replace(" ", "").Contains(cleanExpected)))
                        {
                            foundMessage = true;
                            break;
                        }
                    }
                }
                catch { }
                Thread.Sleep(100);
            }

            if (!foundMessage) throw new Exception($"ไม่พบข้อความ: '{expectedWarningMessage}'");
            LogSuccessCounted(testName, $"พบข้อความ: '{expectedWarningMessage}'");
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

        private void FillTextBox(AutomationElement context, string automationId, string text)
        {
            var el = context.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
            if (el == null) return;

            if (el.Patterns.Value.IsSupported)
            {
                try
                {
                    el.Patterns.Value.Pattern.SetValue(text ?? "");
                    return;
                }
                catch { }
            }

            ClickElementByMouse(el);
            try { el.Focus(); } catch { }

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

        // ── ปรับปรุง TickCheckbox ให้รองรับทั้ง CheckBox และ RadioButton พร้อมตรวจสอบสถานะแบบแม่นยำ ──
        private void TickCheckbox(Window form, AutomationElement context, string checkboxId, bool tick, params string[] fallbackNames)
        {
            AutomationElement? found = context.FindFirstDescendant(cf => cf.ByAutomationId(checkboxId))
                                     ?? form.FindFirstDescendant(cf => cf.ByAutomationId(checkboxId));

            if (found == null && fallbackNames != null)
            {
                foreach (var name in fallbackNames)
                {
                    found = context.FindFirstDescendant(cf => cf.ByName(name))
                         ?? form.FindFirstDescendant(cf => cf.ByName(name))
                         ?? context.FindAllDescendants(cf => cf.ByControlType(ControlType.CheckBox).Or(cf.ByControlType(ControlType.RadioButton)))
                                   .FirstOrDefault(c => !string.IsNullOrEmpty(c.Name) && c.Name.Contains(name))
                         ?? form.FindAllDescendants(cf => cf.ByControlType(ControlType.CheckBox).Or(cf.ByControlType(ControlType.RadioButton)))
                                .FirstOrDefault(c => !string.IsNullOrEmpty(c.Name) && c.Name.Contains(name));
                    if (found != null) break;
                }
            }

            if (found == null) return;

            bool CurrentlyTicked()
            {
                try
                {
                    var cb = found.AsCheckBox();
                    if (cb?.IsChecked != null) return cb.IsChecked == true;
                    if (found.Patterns.Toggle.IsSupported)
                        return found.Patterns.Toggle.Pattern.ToggleState.Value == ToggleState.On;
                    if (found.Patterns.SelectionItem.IsSupported)
                        return found.Patterns.SelectionItem.Pattern.IsSelected.ValueOrDefault;
                }
                catch { }
                return false;
            }

            if (CurrentlyTicked() == tick) return;

            // 1) ลอง Toggle Pattern ก่อน
            try
            {
                if (found.Patterns.Toggle.IsSupported)
                {
                    found.Patterns.Toggle.Pattern.Toggle();
                    if (WaitUntil(() => CurrentlyTicked() == tick, 400)) return;
                }
            }
            catch { }

            // 2) คลิกเมาส์ตรงกลางคอนโทรล
            ClickElementByMouse(found);
            if (WaitUntil(() => CurrentlyTicked() == tick, 400)) return;

            // 3) Fallback ด้วยการ Focus + กด Spacebar
            try
            {
                found.Focus();
                Keyboard.Press(VirtualKeyShort.SPACE);
                Keyboard.Release(VirtualKeyShort.SPACE);
                WaitUntil(() => CurrentlyTicked() == tick, 500);
            }
            catch { }
        }

        // ── เลือกวิธีคิดภาษี: แบบอัตราก้าวหน้า (Index 0) หรือ Fixed Rate (Index 1) ──
        private void SelectTaxMethodRadio(Window form, int radioIndex)
        {
            var radioGroup = form.FindFirstDescendant(cf => cf.ByAutomationId(IdTaxMethodRadioGroup));
            if (radioGroup != null)
            {
                var radios = radioGroup.FindAllChildren(cf => cf.ByControlType(ControlType.RadioButton));
                if (radios.Length > radioIndex)
                {
                    var targetRadio = radios[radioIndex];
                    if (targetRadio.Properties.IsEnabled.ValueOrDefault)
                    {
                        ClickElementByMouse(targetRadio);
                        return;
                    }
                }
            }

            // Fallback: ถ้าหาตาม Id ไม่เจอ ให้หาตามชื่อตัวเลือกภาษาไทยโดยตรง
            string[] radioLabels = { "แบบอัตราก้าวหน้า", "แบบ Fixed Rate", "หัก ณ ที่จ่าย" };
            if (radioIndex >= 0 && radioIndex < radioLabels.Length)
            {
                string targetLabel = radioLabels[radioIndex];
                var radioByName = form.FindFirstDescendant(cf => cf.ByName(targetLabel))
                               ?? form.FindAllDescendants(cf => cf.ByControlType(ControlType.RadioButton))
                                      .FirstOrDefault(r => !string.IsNullOrEmpty(r.Name) && r.Name.Contains(targetLabel));

                if (radioByName != null && radioByName.Properties.IsEnabled.ValueOrDefault)
                {
                    ClickElementByMouse(radioByName);
                }
            }
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
                Thread.Sleep(100);
            }
            return null;
        }
    }
}