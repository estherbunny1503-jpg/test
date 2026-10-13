using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using HRMITestFlaUI.Module.Personnel;
using HRMITestFlaUI.Pages.Personnel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;

namespace HRMITestFlaUI.Base
{
    [TestClass]
    public class PersonnelModuleTests : BaseTest
    {
        // ── Automation IDs ของระบบจัดการแท็บหน้าจอ ──
        private const string IdBtnPageList = "devBtnPageList";
        private const string IdChkPageList = "devChkPageList";
        private const string IdBtnCloseAllPage = "devBtnCloseAllPage";

        // ── Page Objects ของโมดูล Personnel ──
        private static EmpCardInfoUC empCardInfoUC = null!;                         // 1. บันทึกบัตรพนักงาน
        private static EmpInfoUC empInfoUC = null!;                                 // 2. บันทึกข้อมูลพนักงาน
        private static PromoteInfoUC promoteInfoUC = null!;                         // 3. บันทึกปรับตำแหน่งและเงินเดือน
        private static ResignationInfoUC resignationInfoUC = null!;                 // 4. บันทึกพ้นสภาพความเป็นพนักงาน
        private static EmployeeWelfareFundRateInfoUC welfareFundRateInfoUC = null!; // 5. บันทึกอัตราหักกองทุนสงเคราะห์ลูกจ้าง

        private const int TotalScreensInPersonnel = 5;

        [ClassInitialize]
        public static void SuiteSetUp(TestContext context)
        {
            TestLogger.SetContext("Personnel");

            try
            {
                ClassSetUpWithLogin("admin", "");

                if (mainWindow == null || automation == null)
                    throw new Exception("เรียก ClassSetUpWithLogin แล้ว แต่ยังไม่มี MainWindow หรือ Automation Instance");

                empCardInfoUC = new EmpCardInfoUC(mainWindow, automation);
                empInfoUC = new EmpInfoUC(mainWindow, automation);
                promoteInfoUC = new PromoteInfoUC(mainWindow, automation);
                resignationInfoUC = new ResignationInfoUC(mainWindow, automation);
                welfareFundRateInfoUC = new EmployeeWelfareFundRateInfoUC(mainWindow, automation);
            }
            catch (Exception ex)
            {
                TestLogger.LogFailure("SuiteSetUp", $"การเริ่มต้นโมดูล Personnel ล้มเหลว: {ex.Message}");
                throw new Exception($"❌ ไม่สามารถเริ่มต้นมอดูล Personnel ได้เนื่องจากระบบ Login ล้มเหลว: {ex.Message}", ex);
            }
        }

        [ClassCleanup]
        public static void SuiteTearDown()
        {
            try { ClassTearDown(); }
            catch (Exception ex) { TestLogger.LogInfo("SuiteTearDown", $"เกิดข้อผิดพลาดขณะปิดการทำงาน: {ex.Message}"); }
        }

        // -------------------------------------------------------------
        // เรียงลำดับหน้าจอ (ต้องมีบัตร → พนักงาน → ปรับตำแหน่ง/เงินเดือน → พ้นสภาพ → กองทุนสงเคราะห์)
        // -------------------------------------------------------------
        [TestMethod]
        public void Test01_EmpCardInfoUC()
           => RunPage(1, "EmpCardInfo", "บันทึกบัตรพนักงาน",
                empCardInfoUC.NavigateToEmpCardInfoUC); /*empCardInfoUC.RunAllValidationTests,*//*
               /* empCardInfoUC.RunEditTests*//*, empCardInfoUC.RunEditValidationTests, empCardInfoUC.RunDeleteTests)*/

        [TestMethod]
        public void Test02_EmpInfoUC()
            => RunPage(2, "EmpInfo", "บันทึกข้อมูลพนักงาน",
                empInfoUC.NavigateToEmpInfoUC, empInfoUC.RunAllValidationTests,
                empInfoUC.RunEditTests, empInfoUC.RunEditValidationTests, empInfoUC.RunDeleteTests);

        [TestMethod]
        public void Test03_PromoteInfoUC()
            => RunPage(3, "PromoteInfo", "บันทึกปรับตำแหน่งและเงินเดือน",
                promoteInfoUC.NavigateToPromoteInfoUC, promoteInfoUC.RunAllValidationTests,
                promoteInfoUC.RunEditTests, promoteInfoUC.RunEditValidationTests, promoteInfoUC.RunDeleteTests);

        [TestMethod]
        public void Test04_ResignationInfoUC()
            => RunPage(4, "ResignationInfo", "บันทึกพ้นสภาพความเป็นพนักงาน",
                resignationInfoUC.NavigateToResignationInfoUC, resignationInfoUC.RunAllValidationTests,
                resignationInfoUC.RunEditTests, resignationInfoUC.RunEditValidationTests, resignationInfoUC.RunDeleteTests);

        [TestMethod]
        public void Test05_EmployeeWelfareFundRateInfoUC()
            => RunPage(5, "WelfareFundRateInfo", "บันทึกอัตราหักกองทุนสงเคราะห์ลูกจ้าง",
                welfareFundRateInfoUC.NavigateToEmployeeWelfareFundRateInfoUC, welfareFundRateInfoUC.RunAllValidationTests,
                welfareFundRateInfoUC.RunEditTests, welfareFundRateInfoUC.RunEditValidationTests, welfareFundRateInfoUC.RunDeleteTests);

        // -------------------------------------------------------------
        // Engine กลางสำหรับรันขั้นตอนมาตรฐานของแต่ละหน้า
        // -------------------------------------------------------------
        private static void RunPage(int screenIndex, string logContext, string screenNameTh, params Action[] steps)
        {
            TestProgressHelper.SetScreenContext(screenIndex, TotalScreensInPersonnel, logContext);
            TestLogger.SetScreen(screenNameTh);

            // 1. เคลียร์ฟอร์มตกค้าง และเคลียร์ช่องค้นหาเดิมให้พร้อม
            CloseAnyLingeringWindow();
            ClearSearchBox();

            foreach (var step in steps)
            {
                step();
                WaitScreenIdle(); // รอจนไม่มี Modal ค้าง แทนการหน่วงเวลาคงที่
            }

            // 2. เคลียร์ฟอร์มตกค้างก่อนปิดแท็บ
            CloseAnyLingeringWindow();

            // 3. ปิดแท็บหน้าจอเดิมทิ้ง
            CloseCurrentWorkTab();
            WaitScreenIdle();

            // 4. ล้างช่องค้นหา baritemSearch ให้สะอาดเพื่อพร้อมรับหน้าจอถัดไป
            ClearSearchBox();
        }

        /// <summary>รอจนหน้าจอหลักไม่มี Modal/ฟอร์มค้างอยู่ (เช็คสถานะจริงแทน Thread.Sleep)</summary>
        private static void WaitScreenIdle(int timeoutMs = 4000)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try
                {
                    var modals = mainWindow.AsWindow()?.ModalWindows;
                    if (modals == null || modals.Length == 0) return;
                }
                catch { return; }
                Thread.Sleep(100);
            }
        }

        // ── ฟังก์ชันล้างช่องค้นหา baritemSearch ──
        private static void ClearSearchBox()
        {
            try
            {
                var winRect = mainWindow.BoundingRectangle;
                int searchBoxX = (int)winRect.Left + 55;
                int searchBoxY = (int)winRect.Top + 68;

                Mouse.MoveTo(new Point(searchBoxX, searchBoxY));
                Thread.Sleep(80);
                Mouse.Click(MouseButton.Left);
                Thread.Sleep(100);

                Mouse.Click(MouseButton.Left);
                Mouse.DoubleClick(MouseButton.Left);
                Thread.Sleep(100);

                // Shift+Home แก้ปัญหา Ctrl+A ติดภาษาไทย
                Keyboard.Press(VirtualKeyShort.END);
                Keyboard.Release(VirtualKeyShort.END);
                Keyboard.Press(VirtualKeyShort.SHIFT);
                Keyboard.Press(VirtualKeyShort.HOME);
                Keyboard.Release(VirtualKeyShort.HOME);
                Keyboard.Release(VirtualKeyShort.SHIFT);
                Keyboard.Press(VirtualKeyShort.DELETE);
                Keyboard.Release(VirtualKeyShort.DELETE);

                // Backspace เผื่อสระหรือวรรณยุกต์ไทยตกค้าง
                for (int i = 0; i < 35; i++)
                {
                    Keyboard.Press(VirtualKeyShort.BACK);
                    Keyboard.Release(VirtualKeyShort.BACK);
                }

                // ปลดปุ่มแว่นขยาย Find ถ้าค้างสถานะกดอยู่
                var findBtn = mainWindow.FindFirstDescendant(cf =>
                    cf.ByAutomationId("barChkSearch").Or(cf.ByName("Find")));

                if (findBtn != null && findBtn.Patterns.Toggle.IsSupported && findBtn.Patterns.Toggle.Pattern.ToggleState.Value == ToggleState.On)
                {
                    findBtn.Click();
                }
                else
                {
                    Mouse.MoveTo(new Point((int)winRect.Left + 135, (int)winRect.Top + 68));
                    Thread.Sleep(80);
                    Mouse.Click(MouseButton.Left);
                }
            }
            catch { }
        }

        // ── ฟังก์ชันช่วยปิดหน้าต่าง Modal / ฟอร์มที่เปิดลอยค้างอยู่ ──
        private static void CloseAnyLingeringWindow()
        {
            try
            {
                var modals = mainWindow.AsWindow()?.ModalWindows?.ToList();
                if (modals != null)
                {
                    foreach (var m in modals)
                    {
                        var closeBtn = m.FindFirstDescendant(cf => cf.ByName("Close").Or(cf.ByAutomationId("Close")));
                        closeBtn?.Click();
                        var target = m;
                        var sw = Stopwatch.StartNew();
                        while (sw.ElapsedMilliseconds < 1500)
                        {
                            try { if (!target.IsAvailable || target.Properties.IsOffscreen.ValueOrDefault) break; } catch { break; }
                            Thread.Sleep(100);
                        }
                    }
                }
            }
            catch { }
        }

        // ── ฟังก์ชันสั่งปิดแท็บหน้างานเดิมผ่านปุ่มดรอปดาวน์ devBtnPageList ──
        private static void CloseCurrentWorkTab()
        {
            try
            {
                var btnPageList = mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(IdBtnPageList));
                if (btnPageList != null && !btnPageList.Properties.IsOffscreen.ValueOrDefault)
                {
                    btnPageList.Focus();
                    btnPageList.Click();

                    // รอให้ปุ่ม Close All ปรากฏ แทนการหน่วงเวลา
                    AutomationElement? btnCloseAll = null;
                    var sw = Stopwatch.StartNew();
                    while (sw.ElapsedMilliseconds < 2000 && btnCloseAll == null)
                    {
                        btnCloseAll = mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(IdBtnCloseAllPage).Or(cf.ByName("Close All")))
                                      ?? automation.GetDesktop().FindFirstDescendant(cf => cf.ByAutomationId(IdBtnCloseAllPage).Or(cf.ByName("Close All")));
                        if (btnCloseAll == null) Thread.Sleep(100);
                    }

                    if (btnCloseAll != null && btnCloseAll.Properties.IsEnabled.ValueOrDefault)
                    {
                        btnCloseAll.Click();
                        return;
                    }

                    // ถ้าหาปุ่มไม่พบ ให้คลิกด้วยพิกัดขวาล่างของ Popup
                    var chkPageList = mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(IdChkPageList))
                                      ?? automation.GetDesktop().FindFirstDescendant(cf => cf.ByAutomationId(IdChkPageList));

                    if (chkPageList != null)
                    {
                        var popupRect = chkPageList.Parent != null ? chkPageList.Parent.BoundingRectangle : chkPageList.BoundingRectangle;
                        Mouse.MoveTo(new Point((int)popupRect.Right - 45, (int)popupRect.Bottom - 20));
                        Thread.Sleep(100);
                        Mouse.Click(MouseButton.Left);
                        return;
                    }

                    Keyboard.Press(VirtualKeyShort.ESCAPE);
                    Keyboard.Release(VirtualKeyShort.ESCAPE);
                }

                // Fallback: คลิกปุ่มกากบาท [X] บนแท็บ
                var closeTabBtn = mainWindow.FindFirstDescendant(cf =>
                    cf.ByAutomationId("closeButton")
                    .Or(cf.ByName("Close"))
                    .Or(cf.ByName("Close Tab"))
                    .Or(cf.ByName("Close Window")));

                if (closeTabBtn != null && !closeTabBtn.Properties.IsOffscreen.ValueOrDefault)
                    closeTabBtn.Click();
            }
            catch { }
        }
    }
}