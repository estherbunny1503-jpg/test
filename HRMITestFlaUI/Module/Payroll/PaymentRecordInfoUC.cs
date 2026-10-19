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

namespace HRMITestFlaUI.Pages.Payroll
{
    public class PaymentRecordInfoUC
    {
        // ── ตัวแปรสำหรับเก็บออบเจ็กต์หน้าต่างหลัก, ระบบ Automation และตัวนับผลการทดสอบ (Success/Fail) ──
        private readonly AutomationElement _mainWindow;
        private readonly UIA3Automation _automation;
        private int _totalSuccess = 0, _totalFail = 0;

        // ── ค่าคงที่สำหรับการตั้งค่า Timeout และ Automation ID ของฟอร์ม ──
        private const int TimeoutMs = 6000;
        private const string FormId = "PaymentRecordInfoUC";

        // ── ชื่อ Pop-up ของ Lookup (TODO: ตรวจชื่อจริงจาก Inspect) ──
        private static readonly string[] EmployeePopupTitles = { "เลือกพนักงาน", "ข้อมูลพนักงาน", "รายชื่อพนักงาน" };
        private static readonly string[] PaymentTypePopupTitles = { "ประเภทการจ่าย", "เลือกประเภทการจ่าย" };
        private static readonly string[] PaymentPeriodPopupTitles = { "รูปแบบงวดการจ่าย", "งวดการจ่าย" };

        /* Constructor สำหรับรับค่า MainWindow และ Automation Instance เข้ามาใช้งานภายในคลาส */
        public PaymentRecordInfoUC(AutomationElement mainWindow, UIA3Automation automation)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

        // -------------------------------------------------------------
        // 1. ทดสอบการเพิ่มข้อมูลการจ่ายใหม่ (Add / New Payment Record Tests)
        //    สัดส่วน Progress ของเมธอดนี้: 0 -> 20
        // -------------------------------------------------------------
        /* Method สำหรับนำทางไปยังหน้าจอ บันทึกการจ่าย ผ่านการค้นหาเมนู และวนลูปเพิ่มข้อมูลตาม Excel */
        public void NavigateToPaymentRecordInfoUC()
        {
            // TODO: แทนชุดข้อมูลด้านล่างด้วยข้อมูลจาก Excel (ค่าเหล่านี้เป็นค่าจำลองให้ครบรูปแบบ ไม่ใช่ข้อมูลจริง)
            // TODO: ตารางเลือกพนักงาน และรหัสรายได้-รายหัก (devGridListPaymentRecord) ยังไม่ได้กรอก
            var paymentList = new[]
            {
                new { Code = "TODO_EMP", DocDate = "01/01/2570", PaymentType = "TODO_PAYMENT_TYPE", EffectDate = "01/01/2570", Period = "TODO_PERIOD" }
            };

            const string stageName = "TestCase00_AddPaymentRecord";
            TestLogger.LogInfo("PaymentRecordInfoPage", "เริ่มทดสอบหน้าจอ บันทึกการจ่าย");
            // ช่วง 0-20: เปิดเมนู = 2, แล้วไล่ตามจำนวนรายการที่เพิ่ม
            TestProgressHelper.Report(2, "Opening Payroll -> Payment Record Menu via Search...");

            // ค้นหาและเปิดหน้าจอผ่านช่องค้นหาด้านบน ถ้าเปิดไม่สำเร็จให้จบการทำงานทันที
            if (!TryOpenPaymentRecordMenu(stageName)) return;

            Window? form = null;
            for (int i = 0; i < paymentList.Length; i++)
            {
                var p = paymentList[i];
                string testName = $"{stageName}_{p.Code}";
                TestProgressHelper.Report(4 + (i * 4), $"Adding Payment Record ({i + 1}/{paymentList.Length}): {p.Code}...");

                try
                {
                    form ??= GetOrOpenForm(); // ดึงฟอร์มที่เปิดอยู่ หรือเปิดฟอร์มใหม่ถ้ายังไม่มี
                    FillPaymentFields(form, p.DocDate, p.PaymentType, p.EffectDate, p.Period); // กรอกข้อมูลการจ่าย

                    // กำหนดปุ่มบันทึกตามเงื่อนไข (ตัวแรกกด Save, ตัวสุดท้ายกด Save and Close, ตัวตรงกลางกด Save and New)
                    string saveBtn = i == 0 ? "Save" : (i == paymentList.Length - 1 ? "Save and Close" : "Save and New");
                    if (!TrySaveAndHandleDuplicate(form, testName, p.Code, saveBtn))
                    {
                        form = null;
                        continue;
                    }

                    if (saveBtn == "Save and New")
                    {
                        // รอให้ช่องวันที่เอกสารว่างลง (แสดงว่าเคลียร์ฟอร์มเตรียมรับข้อมูลใหม่แล้ว)
                        var f = form;
                        WaitUntil(() => string.IsNullOrEmpty(f.FindFirstDescendant(cf => cf.ByAutomationId("devDateDocuDate"))?.AsTextBox().Text), 3000);
                        LogSuccess(testName, $"'{p.Code}' บันทึกสำเร็จ (Save and New)");
                    }
                    else
                    {
                        if (saveBtn == "Save") form.FindFirstDescendant(cf => cf.ByName("Close"))?.Click();
                        WaitForClosed(() => FindOpenForm());
                        form = null;
                        LogGridResult(testName, p.Code); // ตรวจสอบว่ามีข้อมูลแสดงในตารางหลักจริงไหม
                    }
                }
                catch (Exception ex)
                {
                    LogFailure(testName, $"'{p.Code}' เพิ่มไม่สำเร็จ: {ex.Message}");
                    form = null;
                }
            }
        }

        // -------------------------------------------------------------
        // 2-5. ยังไม่มี Test Step ใน Excel สำหรับหน้านี้ (TODO: เพิ่มเมื่อมี Excel)
        // -------------------------------------------------------------
        /* Method สำหรับรันชุดทดสอบ Validation (ยังไม่มี Test Step ใน Excel) */
        public void RunAllValidationTests()
        {
            TestLogger.LogInfo("PaymentRecordInfoPage", "ยังไม่มี Test Step สำหรับ Validation");
        }

        /* Method สำหรับทดสอบการแก้ไขข้อมูล (ยังไม่มี Test Step ใน Excel) */
        public void RunEditTests()
        {
            TestLogger.LogInfo("PaymentRecordInfoPage", "ยังไม่มี Test Step สำหรับ Edit");
        }

        /* Method สำหรับทดสอบ Validation ในโหมดแก้ไข (ยังไม่มี Test Step ใน Excel) */
        public void RunEditValidationTests()
        {
            TestLogger.LogInfo("PaymentRecordInfoPage", "ยังไม่มี Test Step สำหรับ Edit Validation");
        }

        /* Method สำหรับทดสอบการลบข้อมูล (ยังไม่มี Test Step ใน Excel) */
        public void RunDeleteTests()
        {
            TestLogger.LogInfo("PaymentRecordInfoPage", "ยังไม่มี Test Step สำหรับ Delete");
            TestLogger.LogInfo("PaymentRecordInfoPage", $"สรุปผลรวมทั้งหมด: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");
        }

        // -------------------------------------------------------------
        // UI Helpers & Core Navigation
        // -------------------------------------------------------------

        /// <summary>
        /// ค้นหาหน้าจอผ่านช่อง baritemSearch ด้านซ้ายบน กดปุ่ม Find (barChkSearch) และคลิกเลือกเมนูผลลัพธ์
        /// </summary>
        private bool TryOpenPaymentRecordMenu(string stage)
        {
            try
            {
                // 1. ค้นหาช่องกรอกคำค้นหา (baritemSearch)
                var searchBox = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("baritemSearch"))
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)), 4000)
                              ?? throw new Exception("ไม่พบช่องค้นหา baritemSearch");

                searchBox.Focus();
                Thread.Sleep(200);

                // พิมพ์คำค้นหา "บันทึกการจ่าย" (TODO: ตรวจชื่อเมนูจริง)
                string searchKeyword = "บันทึกการจ่าย";
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

        /* Method สำหรับกรอกข้อมูลช่องต่างๆ ของการจ่าย */
        private void FillPaymentFields(Window form, string docDate, string paymentType, string effectDate, string period)
        {
            SetDate(form, "devDateDocuDate", docDate);
            SelectLookup(form, "devLookupPaymentType", paymentType, PaymentTypePopupTitles);
            SetDate(form, "devDateEffactDate", effectDate);
            SelectLookup(form, "devLookupPaymentPeriodName", period, PaymentPeriodPopupTitles);
        }

        /* Method สำหรับกรอกช่องวันที่ */
        private void SetDate(Window form, string id, string dateText)
        {
            var txt = Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(id))?.AsTextBox(), 3000) ?? throw new Exception($"ไม่พบช่องวันที่ {id}");
            txt.Focus();
            txt.Text = dateText;
        }

        /* Method เปิด Lookup popup จากปุ่ม "..." ของช่อง แล้วเลือกแถวที่มีรหัสตรงกันและกด Select */
        private void SelectLookup(Window form, string id, string code, string[] popupTitles)
        {
            var control = Retry(() => form.FindFirstDescendant(cf => cf.ByAutomationId(id)), 5000)
                          ?? throw new Exception($"ไม่พบคอนโทรล {id}");
            var btn = control.FindFirstDescendant(cf => cf.ByName("...")) ?? control.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button)) ?? control;
            btn.Click();

            var popup = Retry(() =>
            {
                foreach (var t in popupTitles)
                {
                    var w = _automation.GetDesktop().FindFirstChild(cf => cf.ByName(t))?.AsWindow();
                    if (w != null) return w;
                }
                return form.ModalWindows.FirstOrDefault() ?? _mainWindow.AsWindow()?.ModalWindows.FirstOrDefault();
            }, 6000) ?? throw new Exception($"เปิด Pop-up ของ {id} ไม่สำเร็จ");

            var row = Retry(() => popup.FindAllDescendants().FirstOrDefault(el =>
                string.Equals(el.Name?.Trim(), code, StringComparison.OrdinalIgnoreCase) ||
                (el.Patterns.Value.IsSupported && string.Equals(el.Patterns.Value.Pattern.Value.Value?.Trim(), code, StringComparison.OrdinalIgnoreCase))), 8000)
                ?? throw new Exception($"ไม่พบ '{code}' ใน Pop-up ของ {id}");
            row.Click();

            var sel = popup.FindFirstDescendant(cf => cf.ByName("Select")) ?? popup.FindFirstDescendant(cf => cf.ByName("เลือก"));
            if (sel != null) sel.Click();
            else Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.KEY_S);

            WaitForClosed(() => popup, 4000);
        }

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

        /* Method ตรวจสอบข้อความในผลลัพธ์ว่าเป็นกรณีข้อมูลซ้ำหรือไม่ (TODO: ตรวจข้อความจริงของหน้าจอ) */
        private static string? DetectDuplicateMessage(IEnumerable<string> texts)
        {
            if (texts.Any(t => t.Contains("ซ้ำ"))) return "ข้อมูลซ้ำ";
            return null;
        }

        /* Method ค้นหา Element ในตารางหลักที่มีค่าตรงกับ val (รองรับทั้ง Name และ ValuePattern) */
        private AutomationElement? FindGridElement(string val) =>
            _mainWindow.FindAllDescendants().FirstOrDefault(el =>
                (!string.IsNullOrEmpty(el.Name) && string.Equals(el.Name.Trim(), val, StringComparison.OrdinalIgnoreCase)) ||
                (el.Patterns.Value.IsSupported && string.Equals(el.Patterns.Value.Pattern.Value.Value?.Trim(), val, StringComparison.OrdinalIgnoreCase)));

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