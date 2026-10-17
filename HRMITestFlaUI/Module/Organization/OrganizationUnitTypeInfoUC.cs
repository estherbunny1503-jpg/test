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
        // =========================================================================================
        // หมวดที่ 1: ข้อมูลการทดสอบครบทั้ง 5 ขั้น (Test Data: Step 1 - Step 5)
        // =========================================================================================

        private sealed record OrgTypeAddItem(
            string Code,
            string Name,
            string NameEng,
            string Desc,
            string Action,
            bool CloseAfter);

        private sealed record ValidationItem(
            string TestCaseId,
            Action<OrganizationUnitTypeInfoUC, Window> Action,
            string[] ExpectedMessages);

        private sealed record OrgTypeEditItem(
            string TestCaseId,
            string OldCode,
            string NewCode,
            string NewName,
            string NewNameEng,
            string NewDesc,
            string SaveAction,
            bool NeedsManualClose);

        private sealed record DeleteItem(
            string TargetCode,
            string DialogBtn,
            bool ExpectDeleted);

        // [ขั้นที่ 1] ข้อมูลทดสอบการสร้างใหม่ (Add / Navigate)
        private static readonly OrgTypeAddItem[] Step1_AddCases =
        {
            new("001", "หน่วยงานพิเศษ", "Special Unit",               "สำหรับหน่วยงานระดับสูง",        "Save",           true),
            new("002", "ฝ่ายบริหาร",     "Administration",             "ดูแลงานบริหารองค์กร",           "Save and New",   false),
            new("003", "สายงาน",        "Group / Division",           "กลุ่มสายงานหลักของบริษัท",      "Save and New",   false),
            new("004", "ฝ่าย",          "Department",                 "หน่วยงานระดับฝ่ายงาน",          "Save and New",   false),
            new("005", "แผนก",          "Section",                    "หน่วยงานระดับแผนกปฏิบัติการ",   "Save and Close", false)
        };

        // [ขั้นที่ 2] ข้อมูลทดสอบแจ้งเตือนตอนสร้างใหม่ (New Validation)
        private static readonly ValidationItem[] Step2_NewValidationCases =
        {
            // 2.1 ไม่กรอกรหัส + กรอกชื่อ + กรอกชื่อ Eng -> Save
            new("TestCase02_1_EmptyCode",
                (page, form) =>
                {
                    page.FillForm(form, "", "หน่วยงานย่อย / ทีม", "Special Unit", "");
                    form.FindFirstDescendant(cf => cf.ByName("Save"))?.Click();
                },
                new[] { "รหัสประเภทหน่วยงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล" }),

            // 2.2 กรอก 006 + ลบชื่อออก + กรอกชื่อ Eng -> Save and New
            new("TestCase02_2_EmptyName",
                (page, form) =>
                {
                    page.FillForm(form, "006", "", "Special Unit", "");
                    form.FindFirstDescendant(cf => cf.ByName("Save and New"))?.Click();
                },
                new[] { "ชื่อประเภทหน่วยงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล" }),

            // 2.3 กรอก 006 + กรอกชื่อ + ลบชื่อ Eng ออก -> Save and New
            new("TestCase02_3_EmptyNameEng",
                (page, form) =>
                {
                    page.FillForm(form, "006", "หน่วยงานย่อย / ทีม", "", "");
                    form.FindFirstDescendant(cf => cf.ByName("Save and New"))?.Click();
                },
                new[] { "ชื่อประเภทหน่วยงาน (Eng) ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล" }),

            // 2.4 ลบทั้ง 3 ช่องออก -> Save and New
            new("TestCase02_4_EmptyAll",
                (page, form) =>
                {
                    page.FillForm(form, "", "", "", "");
                    form.FindFirstDescendant(cf => cf.ByName("Save and New"))?.Click();
                },
                new[]
                {
                    "รหัสประเภทหน่วยงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล",
                    "ชื่อประเภทหน่วยงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล",
                    "ชื่อประเภทหน่วยงาน (Eng) ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล"
                }),

            // 2.5 รหัสซ้ำ (003) -> Save
            new("TestCase02_5_DuplicateCode",
                (page, form) =>
                {
                    page.FillForm(form, "003", "สายงาน", "Special Unit", "");
                    form.FindFirstDescendant(cf => cf.ByName("Save"))?.Click();
                },
                new[] { "รหัสประเภทหน่วยงาน ห้ามซ้ำ กรุณาป้อนข้อมูล" }),

            // 2.6 ชื่อซ้ำ (ฝ่ายบริหาร) -> Save and Close
            new("TestCase02_6_Edit_DuplicateName_SaveAndClose",
                (page, form) =>
                {
                    page.FillForm(form, "006", "ฝ่ายบริหาร", "Section", "");
                    form.FindFirstDescendant(cf => cf.ByName("Save and Close"))?.Click();
                },
                new[] { "ชื่อประเภทหน่วยงาน ห้ามซ้ำ กรุณาป้อนข้อมูล" })
        };

        // [ขั้นที่ 3] ข้อมูลทดสอบการแก้ไขข้อมูล (Edit Records)
        private static readonly OrgTypeEditItem[] Step3_EditCases =
        {
            new("TestCase03_Edit_003", "003", "DIV01", "สายงานปฏิบัติการ",  "Operations Division",             "ดูแลภาพรวมและควบคุมการปฏิบัติการทั้งหมด", "Save",           true),
            new("TestCase03_Edit_004", "004", "DIV02", "ฝ่ายพัฒนาธุรกิจ",   "Business Development Department", "วางแผนกลยุทธ์และขยายโอกาสทางธุรกิจใหม่", "Save and Close", false)
        };

        // [ขั้นที่ 4] ข้อมูลทดสอบแจ้งเตือนตอนแก้ไขข้อมูล (Edit Validation)
        private static readonly ValidationItem[] Step4_EditValidationCases =
        {
            // 4.1 ลบรหัสออก + แผนก + Section -> Save
            new("TestCase04_1_Edit_EmptyCode",
                (page, form) =>
                {
                    page.FillForm(form, "", "แผนก", "Section", "");
                    form.FindFirstDescendant(cf => cf.ByName("Save"))?.Click();
                },
                new[] { "รหัสประเภทหน่วยงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล" }),

            // 4.2 กรอก 005 + ลบชื่อออก + Section -> Save and New
            new("TestCase04_2_Edit_EmptyName",
                (page, form) =>
                {
                    page.FillForm(form, "005", "", "Section", "");
                    form.FindFirstDescendant(cf => cf.ByName("Save and New"))?.Click();
                },
                new[] { "ชื่อประเภทหน่วยงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล" }),

            // 4.3 กรอก 005 + กรอก แผนก + ลบชื่อ Eng ออก -> Save and New
            new("TestCase04_3_Edit_EmptyNameEng",
                (page, form) =>
                {
                    page.FillForm(form, "005", "แผนก", "", "");
                    form.FindFirstDescendant(cf => cf.ByName("Save and New"))?.Click();
                },
                new[] { "ชื่อประเภทหน่วยงาน (Eng) ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล" }),

            // 4.4 ลบทั้ง 3 ช่องออก -> Save and New
            new("TestCase04_4_Edit_EmptyAll",
                (page, form) =>
                {
                    page.FillForm(form, "", "", "", "");
                    form.FindFirstDescendant(cf => cf.ByName("Save and New"))?.Click();
                },
                new[]
                {
                    "รหัสประเภทหน่วยงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล",
                    "ชื่อประเภทหน่วยงาน ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล",
                    "ชื่อประเภทหน่วยงาน (Eng) ไม่สามารถเป็นค่าว่าง กรุณาป้อนข้อมูล"
                }),

            // 4.5 รหัสซ้ำ (002) -> Save
            new("TestCase04_5_Edit_DuplicateCode",
                (page, form) =>
                {
                    page.FillForm(form, "002", "แผนก", "Section", "");
                    form.FindFirstDescendant(cf => cf.ByName("Save"))?.Click();
                },
                new[] { "รหัสประเภทหน่วยงาน ห้ามซ้ำ กรุณาป้อนข้อมูล" }),

            // 4.6 ชื่อซ้ำ (หน่วยงานพิเศษ) -> Save and Close
            new("TestCase04_6_Edit_DuplicateName_SaveAndClose",
                (page, form) =>
                {
                    page.FillForm(form, "005", "หน่วยงานพิเศษ", "Section", "");
                    form.FindFirstDescendant(cf => cf.ByName("Save and Close"))?.Click();
                },
                new[] { "ชื่อประเภทหน่วยงาน ห้ามซ้ำ กรุณาป้อนข้อมูล" })
        };

        // [ขั้นที่ 5] ข้อมูลทดสอบการลบข้อมูล (Delete Records)
        private static readonly DeleteItem[] Step5_DeleteCases =
        {
            new("005",   "Yes", true),
            new("DIV02", "No",  false)
        };


        // =========================================================================================
        // หมวดที่ 2: ส่วนที่ขึ้นกับหน้าจอนั้นๆ (Screen-Specific Implementation)
        // =========================================================================================

        private const string FormId = "OrganizationUnitTypeInfoUC";
        private const string LogPageKey = "OrganizationUnitTypeInfoUC";
        private const string GeneralTabId = "devTabPgGeneral";
        private const string MenuSearchKeyword = "บันทึกข้อมูลประเภทหน่วยงาน";

        // Controls AutomationId
        private const string IdTxtOrgTypeCode = "devTxtOrgUnitTypeCode";
        private const string IdTxtOrgTypeName = "devTxtOrgUnitTypeName";
        private const string IdTxtOrgTypeNameEng = "devTxtOrgUnitTypeNameEng";
        private const string IdTxtOrgTypeDesc = "devMemoDescription";

        public void NavigateToOrganizationUnitTypeInfoUC()
        {
            const string stageName = "TestCase01_AddOrgTypeData";
            TestLogger.LogInfo(LogPageKey, "เริ่มต้นทดสอบการเพิ่มข้อมูลประเภทหน่วยงาน 5 รายการ");
            TestProgressHelper.Report(2, $"Opening Menu: {MenuSearchKeyword}...");

            if (!TryOpenMenu(MenuSearchKeyword, stageName)) return;

            Window? form = null;
            for (int i = 0; i < Step1_AddCases.Length; i++)
            {
                var item = Step1_AddCases[i];
                string testName = $"{stageName}_{item.Code}";
                TestProgressHelper.Report(5 + (i * 3), $"Adding Record ({i + 1}/{Step1_AddCases.Length}): {item.Code} - {item.Name}...");

                try
                {
                    form ??= OpenForm();

                    FillForm(form, item.Code, item.Name, item.NameEng, item.Desc);

                    if (item.Action == "Save" && item.CloseAfter)
                    {
                        form.FindFirstDescendant(cf => cf.ByName("Save"))?.Click();
                        Thread.Sleep(300);
                        form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();
                        WaitForClosed(() => FindOpenForm());
                        form = null;
                        LogGridResult(testName, item.Code);
                    }
                    else if (item.Action == "Save and New")
                    {
                        form.FindFirstDescendant(cf => cf.ByName("Save and New"))?.Click();
                        var codeInput = form.FindFirstDescendant(cf => cf.ByAutomationId(IdTxtOrgTypeCode))?.AsTextBox();
                        WaitUntil(() => string.IsNullOrEmpty(codeInput?.Text), 3000);
                        LogSuccess(testName, $"'{item.Code}' บันทึกสำเร็จ (Save and New)");
                    }
                    else if (item.Action == "Save and Close")
                    {
                        form.FindFirstDescendant(cf => cf.ByName("Save and Close"))?.Click();
                        WaitForClosed(() => FindOpenForm());
                        form = null;
                        LogGridResult(testName, item.Code);
                    }
                }
                catch (Exception ex)
                {
                    LogFailure(testName, $"'{item.Code}' เพิ่มไม่สำเร็จ: {ex.Message}");
                    form = null;
                }
            }
        }

        public void RunAllValidationTests()
        {
            const string stage = "TestCase02_NewValidation";
            TestProgressHelper.Report(22, "Starting New Validation Tests...");

            Window form;
            try { form = OpenForm(); }
            catch (Exception ex) { LogFailure(stage, $"เปิดฟอร์มไม่สำเร็จ: {ex.Message}"); return; }

            // รันตามชุดข้อมูล Step 2
            foreach (var item in Step2_NewValidationCases)
            {
                RunStep(item.TestCaseId, () =>
                {
                    item.Action(this, form);
                    AssertTaskResult(form, item.ExpectedMessages, item.TestCaseId);
                });
            }

            // 2.7 Close -> Yes
            RunStep("TestCase02_7_CloseThenYes", () =>
            {
                ClickCloseAndHandleDialog(form, "Yes", "TestCase02_7_CloseThenYes");
                AssertTaskResult(form, new[] { "ชื่อประเภทหน่วยงาน ห้ามซ้ำ กรุณาป้อนข้อมูล" }, "TestCase02_7_CloseThenYes", "พบข้อความ: 'ชื่อประเภทหน่วยงาน ห้ามซ้ำ กรุณาป้อนข้อมูล' และไม่ปิดหน้าจอ");
                CheckFormOpenOnly(form);
            });

            // 2.8 Close -> Cancel
            RunStep("TestCase02_8_CloseThenCancel", () =>
            {
                ClickCloseAndHandleDialog(form, "Cancel", "TestCase02_8_CloseThenCancel");
                AssertFormOpen(form, "TestCase02_8_CloseThenCancel");
            });

            // 2.9 Close -> No
            RunStep("TestCase02_9_CloseThenNo", () =>
            {
                ClickCloseAndHandleDialog(form, "No", "TestCase02_9_CloseThenNo");
                AssertFormClosed(form, "TestCase02_9_CloseThenNo");
            });
        }

        public void RunEditTests()
        {
            TestProgressHelper.Report(45, "Starting Edit Operations...");

            foreach (var item in Step3_EditCases)
            {
                try
                {
                    var form = ClickRowAndOpenEdit(item.OldCode);
                    FillForm(form, item.NewCode, item.NewName, item.NewNameEng, item.NewDesc);

                    form.FindFirstDescendant(cf => cf.ByName(item.SaveAction))?.Click();

                    if (item.NeedsManualClose)
                    {
                        Thread.Sleep(300);
                        form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();
                    }

                    WaitForClosed(() => FindOpenForm());
                    RefreshGrid();

                    if (VerifyRecordInGrid(item.NewCode, refresh: false))
                        LogSuccess(item.TestCaseId, $"แก้ไข '{item.OldCode}' เป็น '{item.NewCode}' สำเร็จ");
                    else
                        LogFailure(item.TestCaseId, $"แก้ไข '{item.OldCode}' เป็น '{item.NewCode}' แล้วแต่ไม่พบในตาราง");
                }
                catch (Exception ex)
                {
                    LogFailure(item.TestCaseId, $"แก้ไข '{item.OldCode}' ไม่สำเร็จ: {ex.Message}");
                }
            }
        }

        public void RunEditValidationTests()
        {
            const string baseName = "TestCase04_EditValidation";
            TestProgressHelper.Report(65, "Starting Edit Validation Tests...");

            Window form;
            try { form = ClickRowAndOpenEdit("005"); }
            catch (Exception ex) { LogFailure(baseName, $"เปิดฟอร์ม Edit ไม่สำเร็จ: {ex.Message}"); return; }

            // รันตามชุดข้อมูล Step 4
            foreach (var item in Step4_EditValidationCases)
            {
                RunStep(item.TestCaseId, () =>
                {
                    item.Action(this, form);
                    AssertTaskResult(form, item.ExpectedMessages, item.TestCaseId);
                });
            }

            // 4.7 Close -> Yes
            RunStep("TestCase04_7_Edit_CloseThenYes", () =>
            {
                ClickCloseAndHandleDialog(form, "Yes", "TestCase04_7_Edit_CloseThenYes");
                AssertTaskResult(form, new[] { "ชื่อประเภทหน่วยงาน ห้ามซ้ำ กรุณาป้อนข้อมูล" }, "TestCase04_7_Edit_CloseThenYes", "พบข้อความ: 'ชื่อประเภทหน่วยงาน ห้ามซ้ำ กรุณาป้อนข้อมูล' และไม่ปิดหน้าจอ");
                CheckFormOpenOnly(form);
            });

            // 4.8 Close -> Cancel
            RunStep("TestCase04_8_Edit_CloseThenCancel", () =>
            {
                ClickCloseAndHandleDialog(form, "Cancel", "TestCase04_8_Edit_CloseThenCancel");
                AssertFormOpen(form, "TestCase04_8_Edit_CloseThenCancel");
            });

            // 4.9 Close -> No
            RunStep("TestCase04_9_Edit_CloseThenNo", () =>
            {
                ClickCloseAndHandleDialog(form, "No", "TestCase04_9_Edit_CloseThenNo");
                AssertFormClosed(form, "TestCase04_9_Edit_CloseThenNo");
            });
        }

        public void RunDeleteTests()
        {
            const string baseName = "TestCase05_Delete";
            TestProgressHelper.Report(88, "Starting Delete Tests...");

            foreach (var item in Step5_DeleteCases)
            {
                RunDeleteCase(baseName, item.TargetCode, item.DialogBtn, item.ExpectDeleted);
            }

            TestProgressHelper.Report(100, "All tests completed.");
            TestLogger.LogInfo(LogPageKey, $"สรุปผลรวม: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");
        }

        // --- Custom Mapping & Control Logic ประจำหน้าจอ ---
        private void FillForm(Window form, string code, string name, string nameEng, string desc)
        {
            SwitchToTab(form, GeneralTabId, "General");
            var ctx = form.FindFirstDescendant(cf => cf.ByAutomationId(GeneralTabId)) ?? form;

            FillTextBox(ctx, IdTxtOrgTypeCode, code ?? "");
            FillTextBox(ctx, IdTxtOrgTypeName, name ?? "");
            FillTextBox(ctx, IdTxtOrgTypeNameEng, nameEng ?? "");
            FillTextBox(ctx, IdTxtOrgTypeDesc, desc ?? "");
        }

        private Window OpenForm()
        {
            var btnNew = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("New")), 3000)
                         ?? throw new Exception("หาปุ่ม New ไม่พบ");
            btnNew.Click();

            var form = Retry(FindOpenForm, 8000) ?? throw new Exception("หาฟอร์มบันทึกข้อมูลประเภทหน่วยงานไม่เจอ");

            WaitUntil(() =>
            {
                var txt = form.FindFirstDescendant(cf => cf.ByAutomationId(IdTxtOrgTypeCode));
                return txt != null && !txt.Properties.IsOffscreen.ValueOrDefault;
            }, 3000);

            return form;
        }

        private Window? FindOpenForm() =>
            _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault() ??
            _automation.GetDesktop().FindFirstChild(cf => cf.ByAutomationId(FormId))?.AsWindow() ??
            _automation.GetDesktop().FindFirstChild(cf => cf.ByName("บันทึกข้อมูลประเภทหน่วยงาน : New"))?.AsWindow() ??
            _automation.GetDesktop().FindFirstChild(cf => cf.ByName("บันทึกข้อมูลประเภทหน่วยงาน : Edit"))?.AsWindow();

        private Window ClickRowAndOpenEdit(string code)
        {
            var row = Retry(() => FindGridElement(code), 4000)
                      ?? throw new Exception($"หาแถว '{code}' ไม่เจอ");
            try { row.Focus(); } catch { }
            row.Click();

            var btn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("Edit")), 3000)
                      ?? throw new Exception("ไม่พบปุ่ม Edit");
            btn.Click();

            var form = Retry(FindOpenForm, 8000) ?? throw new Exception("หาฟอร์มแก้ไขไม่เจอ");

            WaitUntil(() =>
            {
                var txt = form.FindFirstDescendant(cf => cf.ByAutomationId(IdTxtOrgTypeCode))?.AsTextBox();
                return txt != null && !string.IsNullOrEmpty(txt.Text);
            }, 3000);

            return form;
        }

        private bool TryOpenMenu(string menuName, string stage)
        {
            try
            {
                var searchBox = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("baritemSearch"))
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)), 4000)
                              ?? throw new Exception("ไม่พบช่องค้นหา baritemSearch");

                searchBox.Focus();
                Thread.Sleep(200);

                if (searchBox.Patterns.Value.IsSupported)
                    searchBox.Patterns.Value.Pattern.SetValue(menuName);
                else
                    Keyboard.Type(menuName);

                Thread.Sleep(300);

                var searchBtn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("barChkSearch"))
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("Find")), 4000)
                              ?? throw new Exception("ไม่พบปุ่มค้นหา barChkSearch");
                searchBtn.Click();

                var menuResult = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName(menuName))
                                               ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("treeListColumn row 1")), 4000)
                               ?? throw new Exception($"ไม่พบผลลัพธ์เมนู '{menuName}'");

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
                LogFailure(stage, $"ค้นหาและเปิดเมนูไม่สำเร็จ: {ex.Message}");
                return false;
            }
        }

        private void SwitchToTab(Window form, string tabAutomationId, string tabName)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                var tab = Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(tabAutomationId))
                                       ?? form.FindFirstDescendant(cf => cf.ByName(tabName)), 2000);
                if (tab == null) continue;
                tab.Click();
                break;
            }
        }

        private void RunDeleteCase(string baseName, string code, string dialogBtn, bool expectDeleted)
        {
            string testName = $"{baseName}_{code}";
            try
            {
                RefreshGrid();

                var elRow = Retry(() => FindGridElement(code), 4000)
                            ?? throw new Exception($"หาแถวรหัส '{code}' ไม่เจอ");

                try { elRow.Focus(); } catch { }
                elRow.Click();

                var delBtn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("Delete")), 3000)
                             ?? throw new Exception("หาปุ่ม Delete ไม่เจอ");
                delBtn.Click();

                var dlg = Retry(() => _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault()
                                      ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("คำเตือน"))?.AsWindow(), 4000)
                          ?? throw new Exception("กด Delete แล้วไม่พบ Pop-up ยืนยัน");

                ClickDialogButton(dlg, dialogBtn);
                WaitForClosed(() => dlg);

                RefreshGrid();
                bool exists = VerifyRecordInGrid(code, timeoutMs: 3000, refresh: false);
                if (expectDeleted)
                {
                    if (exists) { LogFailure(testName, $"ข้อมูลที่ '{code}' ไม่ถูกลบ"); throw new Exception(); }
                    LogSuccess(testName, $"ข้อมูลที่ '{code}' ถูกลบเรียบร้อย");
                }
                else
                {
                    if (!exists) { LogFailure(testName, $"ข้อมูลที่ '{code}' หายไปผิดพลาด"); throw new Exception(); }
                    LogSuccess(testName, $"ข้อมูลที่ '{code}' ต้องอยู่เหมือนเดิม");
                }
            }
            catch (Exception ex) when (!string.IsNullOrEmpty(ex.Message))
            {
                LogFailure(testName, $"ลบรหัส '{code}' ไม่สำเร็จ: {ex.Message}");
            }
        }

        private void AssertTaskResult(Window form, string[] msgs, string testName, string? customMsg = null)
        {
            var pending = new HashSet<string>(msgs);
            var lastSeenTexts = new List<string>();
            var sw = Stopwatch.StartNew();

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
                        if (texts.Any(t => t.Replace(" ", "").Contains(cleanM)))
                            pending.Remove(m);
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

        private AutomationElement? FindTaskResultPanel(Window form, int timeoutMs) =>
            Retry(() => form.FindFirstDescendant(cf => cf.ByName("Task Result"))
                        ?? form.FindFirstDescendant(cf => cf.ByAutomationId("dockPanelTaskResult"))
                        ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("Task Result"))
                        ?? _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("dockPanelTaskResult")), timeoutMs);

        private static IEnumerable<string> GetPanelTexts(AutomationElement panel)
        {
            foreach (var el in panel.FindAllDescendants())
            {
                if (!string.IsNullOrEmpty(el.Name)) yield return el.Name;
                if (el.Patterns.Value.IsSupported && !string.IsNullOrEmpty(el.Patterns.Value.Pattern.Value.Value))
                    yield return el.Patterns.Value.Pattern.Value.Value;
            }
        }

        private void ClickCloseAndHandleDialog(Window form, string buttonToClick, string testName)
        {
            form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();

            var dialog = Retry(() => form.ModalWindows.FirstOrDefault()
                                     ?? _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault()
                                     ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("คำเตือน"))?.AsWindow(), 4000)
                         ?? throw new Exception("กด Close แล้วไม่พบ Pop-up ยืนยัน");

            ClickDialogButton(dialog, buttonToClick);
            WaitForClosed(() => dialog);
        }

        private void ClickDialogButton(AutomationElement dlg, string name)
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
                throw new Exception("ฟอร์มไม่ถูกปิด");
            LogSuccess(testName, "ฟอร์มถูกปิด");
        }


        // =========================================================================================
        // หมวดที่ 3: ส่วนการทำงานกลางที่ตายตัว (Common / Reusable Helpers)
        // =========================================================================================

        private readonly AutomationElement _mainWindow;
        private readonly UIA3Automation _automation;
        private int _totalSuccess = 0, _totalFail = 0;
        private const int TimeoutMs = 6000;

        public OrganizationUnitTypeInfoUC(AutomationElement mainWindow, UIA3Automation automation)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

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

        private bool VerifyRecordInGrid(string val, int timeoutMs = 5000, bool refresh = true)
        {
            if (refresh) RefreshGrid();
            return Retry(() => FindGridElement(val), timeoutMs) != null;
        }

        private AutomationElement? FindGridElement(string val)
        {
            var grid = _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataGrid))
                       ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Table));
            var searchContext = grid ?? _mainWindow;

            return searchContext.FindAllDescendants().FirstOrDefault(el =>
                (!string.IsNullOrEmpty(el.Name) && string.Equals(el.Name.Trim(), val, StringComparison.OrdinalIgnoreCase)) ||
                (el.Patterns.Value.IsSupported && string.Equals(el.Patterns.Value.Pattern.Value.Value?.Trim(), val, StringComparison.OrdinalIgnoreCase)));
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

        private void LogSuccess(string testName, string message)
        {
            _totalSuccess++;
            TestLogger.LogSuccess(testName, message);
        }

        private void LogFailure(string testName, string message)
        {
            _totalFail++;
            TestLogger.LogFailure(testName, message);
        }

        private void FillTextBox(AutomationElement context, string automationId, string text)
        {
            var txt = Retry(() => context.FindFirstDescendant(cf => cf.ByAutomationId(automationId))?.AsTextBox(), 2000)
                      ?? context.FindFirstDescendant(cf => cf.ByName(automationId))?.AsTextBox();

            if (txt == null) return;

            txt.Focus();
            txt.Text = string.Empty;
            if (!string.IsNullOrEmpty(text))
            {
                txt.Enter(text);
            }
        }

        private static bool WaitUntil(Func<bool> condition, int timeoutMs = TimeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try { if (condition()) return true; } catch { }
                Thread.Sleep(80);
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
                Thread.Sleep(100);
            }
            return null;
        }
    }
}