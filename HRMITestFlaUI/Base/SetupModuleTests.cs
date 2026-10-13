using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using HRMITestFlaUI.Module.Setup;
using HRMITestFlaUI.Module.Time_Attendance;
using HRMITestFlaUI.Pages.Setup;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Drawing;
using System.Linq;
using System.Threading;

namespace HRMITestFlaUI.Base
{
    [TestClass]
    public class SetupModuleTests : BaseTest
    {
        // ── Automation IDs ของระบบจัดการแท็บหน้าจอ ──
        private const string IdBtnPageList = "devBtnPageList";
        private const string IdChkPageList = "devChkPageList";
        private const string IdBtnCloseSelectedPage = "devBtnCloseSelectedPage";
        private const string IdBtnCloseAllPage = "devBtnCloseAllPage";

        // ── กำหนดค่าเริ่มต้นเป็น null! เพื่อตัด Warning CS8602 ──
        private static AccountInfoUC accountInfoUC = null!;
        private static BankInfoUC bankInfoUC = null!;
        private static FractionalInfoUC fractionalInfoUC = null!;
        private static PermissionGroupInfoUC permissionGroupInfoUC = null!;
        private static LeaveTypeInfoUC leaveTypeInfoUC = null!;
        private static PosttoPayrollSetupInfoUC posttoPayrollSetupInfoUC = null!;
        private static CalendarInfoUC calendarInfoUC = null!;
        private static IncomeDeductInfoUC incomeDeductInfoUC = null!;
        private static PeriodTemplateInfoUC periodTemplateInfoUC = null!;
        private static TSInfoUC tsInfoUC = null!;
        private static TemplateEmpScheInfoUC templateEmpScheInfoUC = null!;

        private const int TotalScreensInSetup = 10;
        private const int BetweenStepDelayMs = 2000;

        [ClassInitialize]
        public static void SuiteSetUp(TestContext context)
        {
            TestLogger.SetContext("Setup");

            try
            {
                // เปิดโปรแกรม + Login (ช่วง 0% - 10%)
                ClassSetUpWithLogin("admin", "");

                // ผูก Page Objects ทั้ง 9 หน้าจอ (AccountInfoUC เป็นลำดับแรก)
                accountInfoUC = new AccountInfoUC(mainWindow, automation);
                bankInfoUC = new BankInfoUC(mainWindow, automation);
                fractionalInfoUC = new FractionalInfoUC(mainWindow, automation);
                permissionGroupInfoUC = new PermissionGroupInfoUC(mainWindow, automation);
                leaveTypeInfoUC = new LeaveTypeInfoUC(mainWindow, automation);
                posttoPayrollSetupInfoUC = new PosttoPayrollSetupInfoUC(mainWindow, automation);
                calendarInfoUC = new CalendarInfoUC(mainWindow, automation);
                incomeDeductInfoUC = new IncomeDeductInfoUC(mainWindow, automation);
                periodTemplateInfoUC = new PeriodTemplateInfoUC(mainWindow, automation);
                tsInfoUC = new TSInfoUC(mainWindow, automation);
                templateEmpScheInfoUC = new TemplateEmpScheInfoUC(mainWindow, automation);
            }
            catch (Exception ex)
            {
                throw new Exception($"❌ ไม่สามารถเริ่มต้นมอดูล Setup ได้เนื่องจากระบบ Login ล้มเหลว: {ex.Message}", ex);
            }
        }

        [ClassCleanup]
        public static void SuiteTearDown() => ClassTearDown();

        // -------------------------------------------------------------
        // เรียงลำดับ 9 หน้าจอตามตาราง
        // -------------------------------------------------------------
        [TestMethod]
        public void Test01_AccountInfoUC()
            => RunPage(1, "AccountInfo", "กำหนดรหัสบัญชี",
                accountInfoUC.NavigateToAccountInfoUC, accountInfoUC.RunAllValidationTests,
                accountInfoUC.RunEditTests, accountInfoUC.RunEditValidationTests, accountInfoUC.RunDeleteTests);

        [TestMethod]
        public void Test02_BankInfoUC()
            => RunPage(2, "BankInfo", "กำหนดรหัสธนาคาร",
                bankInfoUC.NavigateToBankInfoUC, bankInfoUC.RunAllValidationTests,
                bankInfoUC.RunEditTests, bankInfoUC.RunEditValidationTests, bankInfoUC.RunDeleteTests);

        [TestMethod]
        public void Test03_FractionalInfoUC()
            => RunPage(3, "FractionalInfo", "กำหนดรูปแบบการปัดเศษ",
                fractionalInfoUC.NavigateToFractionalInfoUC, fractionalInfoUC.RunAllValidationTests,
                fractionalInfoUC.RunEditTests, fractionalInfoUC.RunEditValidationTests, fractionalInfoUC.RunDeleteTests);

        [TestMethod]
        public void Test04_PermissionGroupInfoUC()
            => RunPage(4, "PermissionGroup", "กำหนดกลุ่มผู้ใช้",
                permissionGroupInfoUC.NavigateToPermissionGroupInfoUC, permissionGroupInfoUC.RunAllValidationTests,
                permissionGroupInfoUC.RunEditTests, permissionGroupInfoUC.RunEditValidationTests, permissionGroupInfoUC.RunDeleteTests);

        [TestMethod]
        public void Test05_LeaveTypeInfoUC()
            => RunPage(5, "LeaveType", "กำหนดประเภทการลา",
                leaveTypeInfoUC.NavigateToLeaveTypeInfoUC, leaveTypeInfoUC.RunAllValidationTests,
                leaveTypeInfoUC.RunEditTests, leaveTypeInfoUC.RunEditValidationTests, leaveTypeInfoUC.RunDeleteTests);

        [TestMethod]
        public void Test06_PosttoPayrollSetupInfoUC()
            => RunPage(6, "PosttoPayrollSetupInfo", "กำหนดรูปแบบการประมวลผลเข้าระบบเงินเดือน",
                posttoPayrollSetupInfoUC.NavigateToPosttoPayrollSetupInfoUC, posttoPayrollSetupInfoUC.RunAllValidationTests,
                posttoPayrollSetupInfoUC.RunEditTests, posttoPayrollSetupInfoUC.RunEditValidationTests, posttoPayrollSetupInfoUC.RunDeleteTests);

        [TestMethod]
        public void Test07_CalendarInfoUC()
            => RunPage(7, "Calendar", "กำหนดปฏิทินบริษัท",
                calendarInfoUC.NavigateToCalendarInfoUC, calendarInfoUC.RunAllValidationTests,
                calendarInfoUC.RunEditTests, calendarInfoUC.RunEditValidationTests, calendarInfoUC.RunDeleteTests);

        [TestMethod]
        public void Test08_IncomeDeductInfoUC()
            => RunPage(8, "IncomeDeductInfo", "กำหนดรหัสรายได้-รายหัก",
                incomeDeductInfoUC.NavigateToIncomeDeductInfoUC, incomeDeductInfoUC.RunAllValidationTests,
                incomeDeductInfoUC.RunEditTests, incomeDeductInfoUC.RunEditValidationTests, incomeDeductInfoUC.RunDeleteTests);

        [TestMethod]
        public void Test09_PeriodTemplateInfoUC()
            => RunPage(9, "PeriodTemplate", "กำหนดรูปแบบงวดการจ่าย",
                periodTemplateInfoUC.NavigateToPeriodTemplateInfoUC, periodTemplateInfoUC.RunAllValidationTests,
                periodTemplateInfoUC.RunEditTests, periodTemplateInfoUC.RunEditValidationTests, periodTemplateInfoUC.RunDeleteTests,
                extraTests: periodTemplateInfoUC.RunAutoPeriodAndMonthlyCalcTestSuite);

        [TestMethod]
        public void Test10_TSInfoUC()
            => RunPage(10, "TSInfo", "กำหนดข้อมูลกะงาน",
                tsInfoUC.NavigateToTSInfoUC, tsInfoUC.RunAllValidationTests,
                tsInfoUC.RunEditTests, tsInfoUC.RunEditValidationTests, tsInfoUC.RunDeleteTests);


        [TestMethod]
        public void Test11_TemplateEmpScheInfoUC()
            => RunPage(11, "TemplateEmpScheInfo", "กำหนดรูปแบบกลุ่มตารางการทำงาน",
                templateEmpScheInfoUC.NavigateToTemplateEmpScheInfoUC, templateEmpScheInfoUC.RunAllValidationTests,
                templateEmpScheInfoUC.RunEditTests, templateEmpScheInfoUC.RunEditValidationTests, templateEmpScheInfoUC.RunDeleteTests);
        // -------------------------------------------------------------
        // Engine กลางสำหรับรันขั้นตอนมาตรฐานของแต่ละหน้า
        // -------------------------------------------------------------
        private static void RunPage(
            int screenIndex,
            string logContext,
            string screenNameTh,
            Action navigateAndNew,
            Action validation,
            Action edit,
            Action editValidation,
            Action delete,
            Action? extraTests = null)
        {
            TestProgressHelper.SetScreenContext(screenIndex, TotalScreensInSetup, logContext);
            TestLogger.SetScreen(screenNameTh);

            // 1. เคลียร์ฟอร์มตกค้าง และเคลียร์ช่องค้นหาเดิมให้พร้อม
            CloseAnyLingeringWindow();
            ClearSearchBox();

            Action[] steps = { navigateAndNew, validation, edit, editValidation, delete };
            foreach (var step in steps)
            {
                step();
                Thread.Sleep(BetweenStepDelayMs);
            }

            extraTests?.Invoke();

            // 2. เคลียร์ฟอร์มตกค้างก่อนปิดแท็บ
            CloseAnyLingeringWindow();

            // 3. ปิดแท็บหน้าจอเดิมทิ้ง
            CloseCurrentWorkTab();
            Thread.Sleep(800);

            // 4. ล้างช่องค้นหา baritemSearch ให้สะอาดเกลี้ยงเพื่อพร้อมรับหน้าจอถัดไป
            ClearSearchBox();
            Thread.Sleep(500);
        }

        // ── ฟังก์ชันล้างช่องค้นหา baritemSearch เกลี้ยงเกลา 100% ──
        private static void ClearSearchBox()
        {
            try
            {
                // 1. ค้นหา Element ของช่องค้นหาโดยตรงก่อน (รองรับ DevExpress BarEditItem / EditBox)
                var searchBox = mainWindow.FindFirstDescendant(cf =>
                    cf.ByAutomationId("baritemSearch")
                    .Or(cf.ByAutomationId("teSearch"))
                    .Or(cf.ByAutomationId("txtSearch"))
                    .Or(cf.ByControlType(ControlType.Edit)));

                if (searchBox != null && !searchBox.Properties.IsOffscreen.ValueOrDefault)
                {
                    searchBox.Focus();
                    Thread.Sleep(100);

                    // พยายามล้างค่าผ่าน ValuePattern ระดับ API ก่อน (วิธีนี้เกลี้ยงที่สุด ไม่ขึ้นกับภาษาแป้นพิมพ์)
                    if (searchBox.Patterns.Value.IsSupported)
                    {
                        searchBox.Patterns.Value.Pattern.SetValue(string.Empty);
                        Thread.Sleep(100);
                    }
                }
                else
                {
                    // Fallback: หากหา Element ไม่เจอ ให้คลิกตามพิกัดเดิม
                    var winRect = mainWindow.BoundingRectangle;
                    int searchBoxX = (int)winRect.Left + 55;
                    int searchBoxY = (int)winRect.Top + 68;

                    Mouse.MoveTo(new Point(searchBoxX, searchBoxY));
                    Thread.Sleep(80);
                    Mouse.Click(MouseButton.Left);
                    Thread.Sleep(100);
                }

                // 2. เคลียร์ระดับ Keyboard ซ้ำเพื่อกระตุ้น Event และล้างตัวอักษรตกค้าง
                // โฟกัสไปท้ายสุด แล้วคลุมดำกลับมาหน้าสุด
                Keyboard.Press(VirtualKeyShort.END);
                Keyboard.Release(VirtualKeyShort.END);
                Thread.Sleep(50);

                Keyboard.Press(VirtualKeyShort.SHIFT);
                Keyboard.Press(VirtualKeyShort.HOME);
                Keyboard.Release(VirtualKeyShort.HOME);
                Keyboard.Release(VirtualKeyShort.SHIFT);
                Thread.Sleep(50);

                Keyboard.Press(VirtualKeyShort.DELETE);
                Keyboard.Release(VirtualKeyShort.DELETE);
                Thread.Sleep(50);

                // กรณีภาษาไทยค้างหรือมีข้อความยาว ใช้ Ctrl + A เผื่อกรณี Shift+Home ไม่คลุมทั้งหมด
                Keyboard.Press(VirtualKeyShort.CONTROL);
                Keyboard.Press(VirtualKeyShort.KEY_A);
                Keyboard.Release(VirtualKeyShort.KEY_A);
                Keyboard.Release(VirtualKeyShort.CONTROL);
                Thread.Sleep(50);

                Keyboard.Press(VirtualKeyShort.BACK);
                Keyboard.Release(VirtualKeyShort.BACK);
                Thread.Sleep(50);

                // 3. กดย้ำ Backspace เผื่อสระลอย/วรรณยุกต์ไทย
                for (int i = 0; i < 20; i++)
                {
                    Keyboard.Press(VirtualKeyShort.BACK);
                    Keyboard.Release(VirtualKeyShort.BACK);
                }

                // กด Enter เบาๆ เพื่อให้ DataSource/Grid Filter รีเซ็ตค่าว่าง
                Keyboard.Press(VirtualKeyShort.RETURN);
                Keyboard.Release(VirtualKeyShort.RETURN);
                Thread.Sleep(100);

                // 4. ตรวจสอบและปลดปุ่มแว่นขยาย Find (barChkSearch)
                var findBtn = mainWindow.FindFirstDescendant(cf =>
                    cf.ByAutomationId("barChkSearch")
                    .Or(cf.ByName("Find")));

                if (findBtn != null && findBtn.Patterns.Toggle.IsSupported)
                {
                    if (findBtn.Patterns.Toggle.Pattern.ToggleState.Value == ToggleState.On)
                    {
                        findBtn.Click();
                        Thread.Sleep(100);
                    }
                }
                else
                {
                    var winRect = mainWindow.BoundingRectangle;
                    int findBtnX = (int)winRect.Left + 135;
                    int findBtnY = (int)winRect.Top + 68;
                    Mouse.MoveTo(new Point(findBtnX, findBtnY));
                    Thread.Sleep(60);
                    Mouse.Click(MouseButton.Left);
                }

                Thread.Sleep(150);
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
                        var closeBtn = m.FindFirstDescendant(cf => cf.ByName("Close") ?? cf.ByAutomationId("Close"));
                        closeBtn?.Click();
                        Thread.Sleep(300);
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
                // 1) คลิกเปิดปุ่มดรอปดาวน์ devBtnPageList
                var btnPageList = mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(IdBtnPageList));
                if (btnPageList != null && !btnPageList.Properties.IsOffscreen.ValueOrDefault)
                {
                    btnPageList.Focus();
                    btnPageList.Click();
                    Thread.Sleep(500);

                    // 2) คลิกปุ่ม Close All ทันทีเพื่อปิดแท็บหน้างานทั้งหมด
                    var btnCloseAll = mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(IdBtnCloseAllPage).Or(cf.ByName("Close All")))
                                      ?? automation.GetDesktop().FindFirstDescendant(cf => cf.ByAutomationId(IdBtnCloseAllPage).Or(cf.ByName("Close All")));

                    if (btnCloseAll != null && btnCloseAll.Properties.IsEnabled.ValueOrDefault)
                    {
                        btnCloseAll.Click();
                        Thread.Sleep(800);
                        return;
                    }

                    // 3) ถ้าหาปุ่มไม่พบ ให้คลิกด้วยพิกัดขวาล่างของ Popup
                    var chkPageList = mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(IdChkPageList))
                                      ?? automation.GetDesktop().FindFirstDescendant(cf => cf.ByAutomationId(IdChkPageList));

                    if (chkPageList != null)
                    {
                        var popupRect = chkPageList.Parent != null ? chkPageList.Parent.BoundingRectangle : chkPageList.BoundingRectangle;
                        int closeAllX = (int)popupRect.Right - 45;
                        int closeAllY = (int)popupRect.Bottom - 20;

                        Mouse.MoveTo(new Point(closeAllX, closeAllY));
                        Thread.Sleep(100);
                        Mouse.Click(MouseButton.Left);
                        Thread.Sleep(800);
                        return;
                    }

                    Keyboard.Press(VirtualKeyShort.ESCAPE);
                    Thread.Sleep(200);
                }

                // 4) Fallback: คลิกปุ่มกากบาท [X] บนแท็บมุมขวาบนโดยตรง
                var closeTabBtn = mainWindow.FindFirstDescendant(cf =>
                    cf.ByAutomationId("closeButton")
                    .Or(cf.ByName("Close"))
                    .Or(cf.ByName("Close Tab"))
                    .Or(cf.ByName("Close Window")));

                if (closeTabBtn != null && !closeTabBtn.Properties.IsOffscreen.ValueOrDefault)
                {
                    closeTabBtn.Click();
                    Thread.Sleep(500);
                    return;
                }
            }
            catch { }
        }
    }
}