using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using HRMITestFlaUI.Base;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace HRMITestFlaUI.Pages.Payroll
{
    public class ProcessPayrollWinUC
    {
        // ── ตัวแปรสำหรับเก็บออบเจ็กต์หน้าต่างหลัก, ระบบ Automation และตัวนับผลการทดสอบ (Success/Fail) ──
        private readonly AutomationElement _mainWindow;
        private readonly UIA3Automation _automation;
        private int _totalSuccess = 0, _totalFail = 0;

        // ── ค่าคงที่สำหรับการตั้งค่า Timeout และ Automation ID ของหน้าจอ ──
        private const int TimeoutMs = 6000;
        private const int ProcessWaitMs = 60000;
        private const string ProcessButtonId = "devBtnProcess";

        /* Constructor สำหรับรับค่า MainWindow และ Automation Instance เข้ามาใช้งานภายในคลาส */
        public ProcessPayrollWinUC(AutomationElement mainWindow, UIA3Automation automation)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

        // -------------------------------------------------------------
        // 1. ทดสอบการประมวลผลรายได้ (Process Tests)
        //    สัดส่วน Progress ของเมธอดนี้: 0 -> 20
        // -------------------------------------------------------------
        /* Method สำหรับนำทางไปยังหน้าจอ ประมวลผลรายได้ ผ่านการค้นหาเมนู แล้วกดปุ่ม Process */
        public void NavigateToProcessPayrollWinUC()
        {
            const string stageName = "TestCase00_ProcessPayroll";
            TestLogger.LogInfo("ProcessPayrollPage", "เริ่มทดสอบหน้าจอ ประมวลผลรายได้");
            TestProgressHelper.Report(2, "Opening Payroll -> Process Payroll Menu via Search...");

            // ค้นหาและเปิดหน้าจอผ่านช่องค้นหาด้านบน ถ้าเปิดไม่สำเร็จให้จบการทำงานทันที
            if (!TryOpenProcessPayrollMenu(stageName)) return;

            TestProgressHelper.Report(10, "Clicking Process...");
            try
            {
                var btn = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(ProcessButtonId))
                                      ?? _mainWindow.FindFirstDescendant(cf => cf.ByName("Process")), 4000)
                          ?? throw new Exception($"ไม่พบปุ่ม Process ({ProcessButtonId})");

                btn.Click();

                // รอจนประมวลผลเสร็จ (ไม่มี Modal ค้างอยู่) แทนการหน่วงเวลาคงที่
                bool done = WaitUntil(() => _mainWindow.AsWindow()?.ModalWindows.Length == 0, ProcessWaitMs);
                if (!done) throw new Exception("การประมวลผลไม่เสร็จภายในเวลาที่กำหนด");

                // TODO: ตรวจข้อความผลลัพธ์จริงของการประมวลผล (Task Result) แทนการนับว่าสำเร็จจากการไม่มี Modal
                LogSuccess($"{stageName}_Process", "กด Process และประมวลผลเสร็จ");
            }
            catch (Exception ex)
            {
                LogFailure($"{stageName}_Process", $"ประมวลผลไม่สำเร็จ: {ex.Message}");
            }
            TestProgressHelper.Report(20, "Process finished");
        }

        // -------------------------------------------------------------
        // 2-5. ไม่มี Test Step ใน Excel สำหรับหน้านี้ (TODO: เพิ่มเมื่อมี Excel)
        // -------------------------------------------------------------
        /* Method สำหรับรันชุดทดสอบ Validation (ยังไม่มี Test Step ใน Excel) */
        public void RunAllValidationTests()
        {
            TestLogger.LogInfo("ProcessPayrollPage", "ยังไม่มี Test Step สำหรับ Validation");
        }

        /* Method สำหรับทดสอบการแก้ไขข้อมูล (ไม่มีในหน้านี้) */
        public void RunEditTests()
        {
            TestLogger.LogInfo("ProcessPayrollPage", "ยังไม่มี Test Step สำหรับ Edit");
        }

        /* Method สำหรับทดสอบ Validation ในโหมดแก้ไข (ไม่มีในหน้านี้) */
        public void RunEditValidationTests()
        {
            TestLogger.LogInfo("ProcessPayrollPage", "ยังไม่มี Test Step สำหรับ Edit Validation");
        }

        /* Method สำหรับทดสอบการลบข้อมูล (ไม่มีในหน้านี้) */
        public void RunDeleteTests()
        {
            TestLogger.LogInfo("ProcessPayrollPage", "ยังไม่มี Test Step สำหรับ Delete");
            TestLogger.LogInfo("ProcessPayrollPage", $"สรุปผลรวมทั้งหมด: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");
        }

        // -------------------------------------------------------------
        // UI Helpers & Core Navigation
        // -------------------------------------------------------------

        /// <summary>
        /// ค้นหาหน้าจอผ่านช่อง baritemSearch ด้านซ้ายบน กดปุ่ม Find (barChkSearch) และคลิกเลือกเมนูผลลัพธ์
        /// </summary>
        private bool TryOpenProcessPayrollMenu(string stage)
        {
            try
            {
                // 1. ค้นหาช่องกรอกคำค้นหา (baritemSearch)
                var searchBox = Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("baritemSearch"))
                                            ?? _mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)), 4000)
                              ?? throw new Exception("ไม่พบช่องค้นหา baritemSearch");

                searchBox.Focus();
                Thread.Sleep(200);

                // พิมพ์คำค้นหา "ประมวลผลรายได้"
                string searchKeyword = "ประมวลผลรายได้";
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

                // 4. รอให้ปุ่ม Process ปรากฏขึ้นเพื่อยืนยันว่าเข้าหน้าจอสำเร็จ
                Retry(() => _mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(ProcessButtonId)), 6000);
                return true;
            }
            catch (Exception ex)
            {
                LogFailure(stage, $"ค้นหาและเปิดหน้าจอไม่สำเร็จ: {ex.Message}");
                return false;
            }
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
    }
}