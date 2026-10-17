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

namespace HRMITestFlaUI.Pages.Personnel
{
    public class EmpCardInfoUC
    {
        // =========================================================================================
        // หมวดที่ 1: ข้อมูลการทดสอบทั้ง 5 ขั้น (Test Data)
        // =========================================================================================

        // --- Data Records สำหรับใช้งานในแต่ละเฟส ---
        private sealed record AddItem(
            string CardType,
            string CardNo,
            string SaveBtn,
            bool NeedsManualClose);

        private const string CardTypeReal = "บัตรจริง";
        private const string CardTypeTemp = "บัตรชั่วคราว";

        // [ขั้นที่ 1] ข้อมูลทดสอบการสร้างใหม่ (Add / Navigate)
        private static readonly AddItem[] Step1_AddCases =
        {
            new(
                CardTypeReal,
                "EMP0001",
                "Save",
                true),
            new(
                CardTypeReal,
                "EMP0002",
                "Save and New",
                false),
            new(
                CardTypeReal,
                "EMP0003",
                "Save and New",
                false),
            new(
                CardTypeReal,
                "EMP0004",
                "Save and New",
                false),
            new(
                CardTypeReal,
                "EMP0005",
                "Save and New",
                false),
            new(
                CardTypeReal,
                "EMP0006",
                "Save and New",
                false),
            new(
                CardTypeReal,
                "EMP0007",
                "Save and New",
                false),
            new(
                CardTypeReal,
                "EMP0008",
                "Save and New",
                false),
            new(
                CardTypeReal,
                "EMP0009",
                "Save and New",
                false),
            new(
                CardTypeTemp,
                "EMP0010",
                "Save and New",
                false),
            new(
                CardTypeTemp,
                "EMP0011",
                "Save and Close",
                false)
        };

        // [ขั้นที่ 2] - [ขั้นที่ 5] ยังไม่มี Test Step ใน TestStep.xlsx สำหรับหน้าจอนี้


        // =========================================================================================
        // หมวดที่ 2: ส่วนที่ขึ้นกับหน้าจอนั้นๆ (Screen-Specific Implementation)
        // **เมื่อเปลี่ยนไปทำหน้าจออื่น ต้องมาแก้ไขค่าและเมทอดในส่วนนี้ทั้งหมด**
        // =========================================================================================

        // 2.1 Locator & Mappings ประจำหน้าจอ
        private const string MenuName = "บันทึกบัตรพนักงาน";
        private const string ScreenLogName = "EmpCardInfoUC";
        private const string FormId = "EmpCardInfoUC";
        private const string GeneralTabId = "devTabPgGeneral";
        private const string FirstObjectID = "devTxtLastedCardNo";

        // 2.2 ฟิลด์ Input ประจำหน้าจอ
        private const string CardTypeFieldId = "devLookUpCardType";   // ประเภทบัตร
        private const string CardNoFieldId = "devTxtLastedCardNo";    // หมายเลขบัตร

        /// <summary>
        /// ขั้นที่ 1: เปิดหน้าจอและกรอกข้อมูลสร้างใหม่ (หน้าจอนี้มีแค่ขั้นนี้ จึงสรุปผลรวมตอนจบขั้น)
        /// </summary>
        public void NavigateToEmpCardInfoUC()
        {
            const string stageName = "TestCase01_AddNewEmpCard";
            TestLogger.LogInfo(ScreenLogName, $"เริ่มทดสอบ{MenuName} ({Step1_AddCases.Length} รายการ)");
            TestProgressHelper.Report(2, $"Opening Personnel -> {MenuName} Menu via Search...");

            if (!TryOpenMenu(stageName, MenuName)) { LogTotalSummary(); return; }

            Window? form = null;
            for (int i = 0; i < Step1_AddCases.Length; i++)
            {
                var item = Step1_AddCases[i];
                string testName = $"{stageName}_{item.CardNo}";
                TestProgressHelper.Report(4 + (i * 16 / Step1_AddCases.Length), $"Adding {MenuName} ({i + 1}/{Step1_AddCases.Length}): {item.CardNo} - {item.CardType}...");

                try
                {
                    form ??= GetOrOpenForm();
                    var tab = GetGeneralTab(form);
                    FillFormData(form, tab, item.CardType, item.CardNo);

                    if (!TrySaveAndHandleDuplicate(form, testName, item.CardNo, item.SaveBtn))
                    {
                        form = null;
                        continue;
                    }

                    if (item.SaveBtn == "Save and New")
                    {
                        if (WaitUntil(() => IsFormCleared(item.CardNo), 8000))
                            LogSuccess(testName, $"'{item.CardNo}' บันทึกสำเร็จ (Save and New)");
                        else
                        {
                            LogFailure(testName, $"'{item.CardNo}' กด Save and New แล้วฟอร์มไม่ถูกเคลียร์ (บันทึกไม่สำเร็จ)");
                            AbortForm(form);
                            form = null;
                        }
                    }
                    else
                    {
                        if (item.NeedsManualClose) form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();
                        if (WaitForClosed(() => FindOpenForm()))
                            LogGridResult(testName, item.CardNo);
                        else
                        {
                            LogFailure(testName, $"'{item.CardNo}' กด {item.SaveBtn} แล้วฟอร์มไม่ปิด (บันทึกไม่สำเร็จ)");
                            AbortForm(form);
                        }
                        form = null;
                    }
                }
                catch (Exception ex)
                {
                    LogFailure(testName, $"'{item.CardNo}' เพิ่มไม่สำเร็จ: {ex.Message}");
                    AbortForm(form);
                    form = null;
                }
            }

            LogTotalSummary();
        }

        /// <summary>
        /// ขั้นที่ 2: รัน Validation ตอนสร้างข้อมูลใหม่ (ยังไม่มี Test Step ใน Excel)
        /// </summary>
        public void RunAllValidationTests()
        {
            TestProgressHelper.Report(40, "Validation: no test steps...");
        }

        /// <summary>
        /// ขั้นที่ 3: เลือกแก้ไขรายการในตาราง (ยังไม่มี Test Step ใน Excel)
        /// </summary>
        public void RunEditTests()
        {
            TestProgressHelper.Report(55, "Edit: no test steps...");
        }

        /// <summary>
        /// ขั้นที่ 4: รัน Validation ตอนแก้ไขข้อมูล (ยังไม่มี Test Step ใน Excel)
        /// </summary>
        public void RunEditValidationTests()
        {
            TestProgressHelper.Report(80, "Edit Validation: no test steps...");
        }

        /// <summary>
        /// ขั้นที่ 5: ทดสอบการลบรายการในตาราง (ยังไม่มี Test Step ใน Excel)
        /// </summary>
        public void RunDeleteTests()
        {
            TestProgressHelper.Report(100, "Delete: no test steps...");
        }

        // --- Custom Mapping Logic ประจำหน้าจอนี้ ---
        private void FillFormData(Window form, AutomationElement tab, string cardType, string cardNo)
        {
            SelectCardType(form, cardType);
            SetText(tab, CardNoFieldId, cardNo);
        }

        private static string? DetectDuplicateMessage(IEnumerable<string> texts)
        {
            if (texts.Any(t => t.Contains("ซ้ำ"))) return "หมายเลขบัตรซ้ำ";
            return null;
        }

        private AutomationElement GetGeneralTab(Window form) =>
            Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(GeneralTabId)), 3000) ?? form;

        private Window? FindOpenForm() =>
            _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault() ??
            _automation.GetDesktop().FindFirstChild(cf => cf.ByAutomationId(FormId))?.AsWindow();

        // --- Dropdown ประเภทบัตร (เฉพาะหน้าจอนี้) ---

        private void SelectCardType(Window form, string cardType)
        {
            var combo = Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(CardTypeFieldId)), 8000)
                        ?? throw new Exception($"หาช่องประเภทบัตร '{CardTypeFieldId}' ไม่เจอ");

            if (ReadText(combo) == cardType) return;

            var rect = combo.BoundingRectangle;
            Mouse.Click(new Point(rect.Right - 9, rect.Top + rect.Height / 2));

            var item = Retry(() => FindDropdownItem(form, cardType), 3000);
            if (item != null)
            {
                var ir = item.BoundingRectangle;
                Mouse.Click(new Point(ir.Left + ir.Width / 2, ir.Top + ir.Height / 2));
                if (WaitUntil(() => ReadText(combo) == cardType, 2000)) return;
            }

            // สำรอง: ปิด popup แล้วเลื่อนตัวเลือกด้วยปุ่มลูกศรจนค่าในช่องตรง
            Keyboard.Type(VirtualKeyShort.ESCAPE);
            foreach (var key in new[] { VirtualKeyShort.DOWN, VirtualKeyShort.UP })
            {
                for (int i = 0; i < 5; i++)
                {
                    string before = ReadText(combo);
                    Keyboard.Type(key);
                    WaitUntil(() => ReadText(combo) != before, 500);

                    if (ReadText(combo) == cardType)
                    {
                        Keyboard.Type(VirtualKeyShort.TAB);
                        return;
                    }
                }
            }

            throw new Exception($"เลือกประเภทบัตร '{cardType}' ไม่ติด (ค่าในช่องตอนนี้: '{ReadText(combo)}')");
        }

        private AutomationElement? FindDropdownItem(Window form, string text)
        {
            int pid = _mainWindow.Properties.ProcessId.ValueOrDefault;
            var formHandle = form.Properties.NativeWindowHandle.ValueOrDefault;
            var mainHandle = _mainWindow.Properties.NativeWindowHandle.ValueOrDefault;

            var popups = _automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(pid))
                .Where(w => w.Properties.NativeWindowHandle.ValueOrDefault != formHandle
                         && w.Properties.NativeWindowHandle.ValueOrDefault != mainHandle);

            foreach (var popup in popups)
            {
                var el = popup.FindAllDescendants().FirstOrDefault(e => !e.BoundingRectangle.IsEmpty && ReadText(e) == text);
                if (el != null) return el;
            }
            return null;
        }

        private static string ReadText(AutomationElement e)
        {
            try { if (e.Patterns.Value.IsSupported && !string.IsNullOrWhiteSpace(e.Patterns.Value.Pattern.Value.Value)) return e.Patterns.Value.Pattern.Value.Value.Trim(); } catch { }
            try { return (e.Name ?? "").Trim(); } catch { return ""; }
        }


        // =========================================================================================
        // หมวดที่ 3: ส่วนการทำงานกลางที่ตายตัว (Common / Reusable Helpers)
        // **ใช้งานร่วมกันได้ทุกหน้าจอ ไม่ต้องแก้ไขคำสั่งเมื่อเปลี่ยนหน้า**
        // =========================================================================================

        private readonly AutomationElement _mainWindow;
        private readonly UIA3Automation _automation;
        private int _totalSuccess = 0;
        private int _totalFail = 0;
        private const int TimeoutMs = 6000;

        public EmpCardInfoUC(AutomationElement mainWindow, UIA3Automation automation)
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

        private Window ClickRowAndOpenEdit(string code)
        {
            RefreshGrid();

            var row = Retry(() => FindGridElement(code), 4000)
                      ?? throw new Exception($"หาแถว '{code}' ไม่เจอ");

            WaitUntil(() => { try { row.Focus(); return true; } catch { return false; } }, 1000);
            row.Click();

            var btn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("Edit")), 3000)
                      ?? throw new Exception("ไม่พบปุ่ม Edit");
            btn.Click();

            var form = Retry(FindOpenForm, 5000) ?? throw new Exception("หาฟอร์มไม่เจอ");
            WaitUntil(() => form.FindFirstDescendant(cf => cf.ByAutomationId(FirstObjectID)) != null, 3000);
            return form;
        }

        private void RunDeleteCase(string baseName, string code, string btn, bool expectDeleted)
        {
            string testName = $"{baseName}_Delete{btn}";
            try
            {
                RefreshGrid();

                var elRow = Retry(() => FindGridElement(code), 4000)
                            ?? throw new Exception($"หาแถวรหัส '{code}' ในตารางไม่เจอ");

                WaitUntil(() => { try { elRow.Focus(); return true; } catch { return false; } }, 1000);
                elRow.Click();

                var delBtn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByName("Delete")))
                             ?? throw new Exception("หาปุ่ม Delete ไม่เจอ");
                delBtn.Click();

                var dlg = Retry(() => _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault()
                                      ?? _automation.GetDesktop().FindFirstChild(cf => cf.ByName("คำเตือน"))?.AsWindow())
                          ?? throw new Exception("กด Delete แล้วไม่พบ Pop-up ยืนยัน");

                ClickDialogButton(dlg, btn);
                WaitForClosed(() => dlg);

                bool exists = VerifyRecordInGrid(code, timeoutMs: 3000, refresh: false);
                if (expectDeleted)
                {
                    if (exists) { LogFailure(testName, $"ลบข้อมูล '{code}' (Delete -> {btn}) ไม่สำเร็จ: ข้อมูลยังอยู่ในตาราง"); return; }
                    LogSuccess(testName, $"ลบข้อมูล '{code}' (Delete -> {btn}) สำเร็จ");
                }
                else
                {
                    if (!exists) { LogFailure(testName, $"ยกเลิกการลบข้อมูล '{code}' (Delete -> {btn}) ไม่สำเร็จ: ข้อมูลหายจากตาราง"); return; }
                    LogSuccess(testName, $"ยกเลิกการลบข้อมูล '{code}' (Delete -> {btn}) ข้อมูลยังคงอยู่สำเร็จ");
                }
            }
            catch (Exception ex)
            {
                LogFailure(testName, $"ลบข้อมูล '{code}' (Delete -> {btn}) ไม่สำเร็จ: {ex.Message}");
            }
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
            Keyboard.Type(VirtualKeyShort.BACK);
            Keyboard.Type(VirtualKeyShort.DELETE);

            if (!string.IsNullOrEmpty(text)) Keyboard.Type(text);
        }

        private AutomationElement? FindGridElement(string val) =>
            _mainWindow.FindAllDescendants().FirstOrDefault(el =>
                (!string.IsNullOrEmpty(el.Name) && string.Equals(el.Name.Trim(), val, StringComparison.OrdinalIgnoreCase)) ||
                (el.Patterns.Value.IsSupported && string.Equals(el.Patterns.Value.Pattern.Value.Value?.Trim(), val, StringComparison.OrdinalIgnoreCase)));

        private bool TrySaveAndHandleDuplicate(Window form, string testName, string code, string saveBtn)
        {
            var before = ReadTaskResultTexts(form, 300);

            saveBtn = saveBtn.Trim();
            var btn = form.FindFirstDescendant(cf => cf.ByName(saveBtn)) ?? throw new Exception($"ไม่พบปุ่ม {saveBtn}");
            btn.Click();

            if (testName.Contains("Validation") || testName.Contains("Duplicate")) return true;

            string? error = null;
            WaitUntil(() =>
            {
                if (!IsAlive(form) || IsFormCleared(code)) return true;
                var fresh = ReadTaskResultTexts(form, 300).Where(t => !before.Contains(t)).ToList();
                error = DetectDuplicateMessage(fresh) is string dup ? $"ข้อมูลซ้ำ: {dup}" : DetectSaveError(fresh);
                return error != null;
            }, 2000);

            if (error == null) return true;

            LogFailure(testName, $"'{code}' บันทึกไม่สำเร็จ: {error}");
            AbortForm(form);
            return false;
        }

        private HashSet<string> ReadTaskResultTexts(Window form, int timeoutMs)
        {
            try
            {
                if (!IsAlive(form)) return new HashSet<string>();
                var panel = FindTaskResultPanel(form, timeoutMs);
                return panel == null ? new HashSet<string>() : new HashSet<string>(GetPanelTexts(panel));
            }
            catch { return new HashSet<string>(); }
        }

        private static string? DetectSaveError(IEnumerable<string> texts) =>
            texts.FirstOrDefault(t => t.Contains("ไม่สามารถ") || t.Contains("ห้ามซ้ำ") || t.Contains("ต้องมีอย่างน้อย")
                                      || t.Contains("กรุณาป้อน") || t.Contains("ไม่ถูกต้อง") || t.Contains("ผิดพลาด"));

        private bool IsFormCleared(string typedCardNo)
        {
            var el = FindOpenForm()?.FindFirstDescendant(cf => cf.ByAutomationId(FirstObjectID));
            if (el == null) return false;
            string now = (el.AsTextBox().Text ?? "").Trim();
            return now.Length == 0 || !string.Equals(now, typedCardNo, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAlive(Window form)
        {
            try { return form.IsAvailable && !form.Properties.IsOffscreen.ValueOrDefault; }
            catch { return false; }
        }

        private void AbortForm(Window? form)
        {
            try
            {
                form ??= FindOpenForm();
                if (form == null || !IsAlive(form)) return;
                try { ClickCloseAndHandleDialog(form, "No", "AbortForm"); } catch { }
                WaitForClosed(() => FindOpenForm(), 3000);
            }
            catch { }
        }

        private void RunCloseDialogSequence(Window form, string prefix, string expectedMsg)
        {
            string ySub = $"{prefix}_CloseYes";
            ClickCloseAndHandleDialog(form, "Yes", ySub);
            CheckFormOpenOnly(form);
            AssertTaskResult(form, new[] { expectedMsg }, ySub, "ทดสอบกด Close -> Yes สำเร็จ");

            string cSub = $"{prefix}_CloseCancel";
            ClickCloseAndHandleDialog(form, "Cancel", cSub);
            AssertFormOpen(form, cSub, "ทดสอบกด Close -> Cancel หน้าต่างยังคงเปิดอยู่สำเร็จ");

            string nSub = $"{prefix}_CloseNo";
            ClickCloseAndHandleDialog(form, "No", nSub);
            AssertFormClosed(form, nSub, "ทดสอบกด Close -> No ปิดหน้าจอสำเร็จ");
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

        private void AssertFormOpen(Window form, string testName, string? customMsg = null)
        {
            CheckFormOpenOnly(form);
            LogSuccess(testName, customMsg ?? "ฟอร์มถูกเปิดอยู่");
        }

        private void AssertFormClosed(Window form, string testName, string? customMsg = null)
        {
            // ค้นหาฟอร์มใหม่แทนการอ่าน property ของ element เดิม (ซึ่งจะ throw หลังฟอร์มถูกปิด)
            if (!WaitForClosed(() => FindOpenForm(), 3000))
                throw new Exception("ปิดฟอร์มไม่สำเร็จ");
            LogSuccess(testName, customMsg ?? "ฟอร์มถูกปิด");
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

        private void LogTotalSummary() =>
            TestLogger.LogInfo(ScreenLogName, $"สรุปผลการทดสอบทั้งหมด: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");

        public int TotalSuccess => _totalSuccess;
        public int TotalFail => _totalFail;

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