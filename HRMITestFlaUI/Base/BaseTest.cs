using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using FlaUIApplication = FlaUI.Core.Application;

namespace HRMITestFlaUI.Base
{
    public class BaseTest
    {
        // ── ตัวแปรหลักสำหรับควบคุม Automation Instance, Application และ หน้าต่างหลักของโปรแกรม ──
        protected static UIA3Automation automation = null!;
        protected static FlaUIApplication app = null!;
        protected static Window mainWindow = null!;

        // ── ค่าเริ่มต้น Fallback สำหรับระบุตำแหน่งไฟล์โปรแกรมกรณีที่อ่านจาก Config ไม่พบ ──
        private const string DefaultAppPath = @"C:\Program Files (x86)\Prosoft\Prosoft HRMI\Bin\HRMI.exe";
        private const string ProcessName = "HRMI";
        private const string ConfigFileName = "test_config.txt";

        // ── ค่ากำหนดเวลาจำกัด (Timeouts) สำหรับรอการโหลดหน้าต่างหรือองค์ประกอบต่างๆ ──
        private const int MainWindowWaitSeconds = 60;
        private const int LoginFormWaitTimeoutSeconds = 30;
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan PopupTotalWaitTimeout = TimeSpan.FromSeconds(60);

        // ── ค่าคงที่ AutomationId ของฟิลด์ต่างๆ บนหน้า Login และ Profile Connection ──
        private const string IdUsername = "devTxtUser";
        private const string IdPassword = "devTxtPassword";
        private const string IdBtnLogin = "devBtnLogin";
        private const string IdBtnCancel = "devBtnCancel";
        private const string IdCmbProfile = "devCmbProfile";
        private const string IdChkProfile = "devChkProfile";
        private const string IdCmbLanguage = "devCmbLanguage";
        private const string IdTxtServerName = "devTxtServerName";
        private const string IdTxtDataBase = "devTxtDataBase";
        private const string IdTxtDataBaseLogin = "devTxtDataBaseLogin";
        private const string IdTxtDataBasePassword = "devTxtDataBasePassword";
        private const string IdErrorPanel = "flowLayoutPanel1";

        // ── ค่าคงที่ AutomationId ของหน้าต่างเลือก Organization (องค์กร) ──
        private const string IdGridOrg = "devGridOrganization";
        private const string IdBtnOK = "devBtnOK";

        // ── ข้อความและ ClassName ของหน้าต่าง Popup / Warning ที่มักจะเด้งขึ้นมาหลัง Login สำเร็จ ──
        private const string WarningWindowClassName = "frmMessageBox";
        private const string WarningWindowTitleContains = "แจ้งเตือนการใช้งาน Prosoft HRMI (Demo)";
        private const string VersionUpdateText = "เวอร์ชันของฐานข้อมูลไม่เท่ากับเวอร์ชันของโปรแกรม";
        private const string BackupQuestionText = "คุณต้องการ Backup ฐานข้อมูลเก็บไว้ก่อนหรือไม่";

        //=========================================
        // Config Reader: ดึงการตั้งค่าจาก test_config.txt
        //=========================================
        /* Method 1: ค้นหาและอ่านไฟล์ตั้งค่า test_config.txt โดยไล่ย้อนขึ้นไปสูงสุด 5 โฟลเดอร์ */
        private static Dictionary<string, string> LoadConfigFile()
        {
            var config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string? currentDir = AppDomain.CurrentDomain.BaseDirectory;
            string? configFilePath = null;

            for (int i = 0; i < 5 && currentDir != null; i++)
            {
                string checkPath = Path.Combine(currentDir, ConfigFileName);
                if (File.Exists(checkPath))
                {
                    configFilePath = checkPath;
                    break;
                }
                currentDir = Directory.GetParent(currentDir)?.FullName;
            }

            if (configFilePath != null && File.Exists(configFilePath))
            {
                foreach (var line in File.ReadAllLines(configFilePath))
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith(";"))
                        continue;

                    int splitIndex = trimmed.IndexOf('=');
                    if (splitIndex > 0)
                    {
                        string key = trimmed.Substring(0, splitIndex).Trim();
                        string value = trimmed.Substring(splitIndex + 1).Trim().Trim('"');
                        config[key] = value;
                    }
                }
            }
            return config;
        }

        /* Method 2: ตรวจสอบและเลือก Path ของโปรแกรม HRMI ที่ถูกต้องที่สุด */
        private static string GetResolvedAppPath(Dictionary<string, string> config)
        {
            string? envPath = Environment.GetEnvironmentVariable("APP_PATH");
            if (!string.IsNullOrWhiteSpace(envPath)) return envPath.Trim().Trim('"');

            if (config.TryGetValue("APP_PATH", out var configPath))
            {
                if (File.Exists(configPath)) return configPath;
                Debug.WriteLine($"⚠️ ไม่พบไฟล์ตาม APP_PATH ใน config: {configPath} -> ใช้ DefaultAppPath แทน");
            }

            if (File.Exists(DefaultAppPath)) return DefaultAppPath;

            throw new FileNotFoundException($"❌ ไม่พบไฟล์โปรแกรม HRMI ทั้งใน Config และ Default Path ({DefaultAppPath})");
        }

        /* Method 3: ดึงค่า Config ตาม Key */
        private static string? GetConfigValue(Dictionary<string, string> config, params string[] candidateKeys)
        {
            foreach (var key in candidateKeys)
            {
                if (config.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
                    return val;
            }
            return null;
        }

        /* Method 4: ดึงค่า Config แบบยอมรับค่าว่าง */
        private static string? GetConfigValueRaw(Dictionary<string, string> config, params string[] candidateKeys)
        {
            foreach (var key in candidateKeys)
            {
                if (config.TryGetValue(key, out var val))
                    return val;
            }
            return null;
        }

        //=========================================
        // Class setup / teardown
        //=========================================
        /* Method 5: ตั้งค่าก่อนเริ่มรัน Test เปิดโปรแกรม ล็อกอิน และจัดการ Dialog */
        protected static void ClassSetUpWithLogin(string? overrideUser = null, string? overridePass = null)
        {
            var config = LoadConfigFile();

            TestProgressHelper.ReportSystem(10, "Cleaning processes & Launching App...");
            LaunchAndAttach(config);

            TestProgressHelper.ReportSystem(30, "Waiting for Login Screen to be ready...");
            WaitForLoginField(IdUsername);

            bool hasDbConfig = config.ContainsKey("DB_SERVER") || config.ContainsKey("DB_NAME") || config.ContainsKey("Profile");
            if (hasDbConfig)
            {
                TestProgressHelper.ReportSystem(45, "Configuring Profile & Database Connection...");
                SetupConnectionProfile(config);
            }

            string username = overrideUser ?? GetConfigValue(config, "User_Login", "USER_LOGIN", "USER") ?? "admin";
            string password = overridePass ?? GetConfigValue(config, "Password", "USER_PASSWORD", "PASS") ?? "";

            TestProgressHelper.ReportSystem(60, $"Logging in as '{username}'...");
            LoginAndSelectOrganization(username, password);

            TestProgressHelper.ReportSystem(85, "Verifying Popups & Dialogs...");
            EnsureNoBlockingPopup();

            try
            {
                TestProgressHelper.ReportSystem(95, "Attaching to HRMI Main Window...");

                // ตรวจสอบจนกว่าหน้าต่างหลักพร้อมทำงานอย่างสมบูรณ์โดยไม่ต้องหน่วงเวลาแบบตายตัว
                WaitForMainWindowReady(MainWindowWaitSeconds);
                AttachToMainWindowStatic();

                TestProgressHelper.ReportSystem(100, "Login Succeeded. Ready for Tests.");
                Debug.WriteLine($"✅ เข้าสู่ระบบสำเร็จ ผูกหน้าต่างหลัก: {mainWindow.Title}");
            }
            catch (Exception ex)
            {
                throw new Exception($"❌ ClassSetUp: ไม่สามารถสลับเข้าวินโดว์หน้าจอหลักได้: {ex.Message}", ex);
            }
        }

        /* Method 6: ตัวเลือกสำหรับเปิดโปรแกรมเฉยๆ */
        protected static void ClassSetUp() => LaunchAndAttach(LoadConfigFile());

        /* Method 7: คืนหน่วยความจำและปิดโปรแกรมหลังเสร็จสิ้นการทดสอบ */
        protected static void ClassTearDown() => DisposeAppAndAutomation();

        /* Method 8: ปิด Process เดิม แล้วเปิดโปรแกรม HRMI ใหม่ */
        private static void LaunchAndAttach(Dictionary<string, string> config)
        {
            DisposeAppAndAutomation();
            KillExistingProcesses();
            WaitUntil(() => !Process.GetProcessesByName(ProcessName).Any(), timeoutMs: 3000);

            string appPath = GetResolvedAppPath(config);
            Debug.WriteLine($"🚀 กำลังเปิดโปรแกรม HRMI จาก: {appPath}");

            automation = new UIA3Automation();
            app = FlaUIApplication.Launch(new ProcessStartInfo
            {
                FileName = appPath,
                WorkingDirectory = Path.GetDirectoryName(appPath)
            });

            AttachToMainWindowStatic();
        }

        /* Method 9: ปิดการทำงานของ Application และ Automation อย่างปลอดภัย */
        private static void DisposeAppAndAutomation()
        {
            SafeDispose(() =>
            {
                if (automation != null)
                {
                    var desktop = automation.GetDesktop();
                    var popups = desktop.FindAllChildren(cf => cf.ByControlType(ControlType.Window));
                    foreach (var p in popups)
                    {
                        var win = p.AsWindow();
                        if (win != null && win.IsAvailable)
                        {
                            string winText = string.Join(" ", win.FindAllDescendants().Select(el => el.Name));
                            if (winText.Contains("ต้องการออกจากระบบ ใช่หรือไม่", StringComparison.OrdinalIgnoreCase))
                            {
                                HandleGenericPopup(win, "Yes");
                            }
                        }
                    }
                }
            });

            SafeDispose(() => app?.Close());
            SafeDispose(() => app?.Dispose());
            SafeDispose(() => automation?.Dispose());
            app = null!;
            automation = null!;
        }

        private static void SafeDispose(Action action)
        {
            try { action(); } catch { }
        }

        /* Method 11: ฆ่าทุก Process ของ HRMI */
        private static void KillExistingProcesses()
        {
            foreach (var process in Process.GetProcessesByName(ProcessName))
            {
                try { process.Kill(); process.WaitForExit(3000); } catch { }
            }
        }

        /* Method: ตรวจสอบแบบ Active Polling จนกว่าหน้าต่างหลักพร้อมทำงาน */
        private static void WaitForMainWindowReady(int timeoutSeconds)
        {
            Window? readyWindow = null;
            bool isReady = WaitUntil(() =>
            {
                try
                {
                    var hrmiProc = Process.GetProcessesByName(ProcessName).FirstOrDefault();
                    if (hrmiProc == null) return false;

                    // MainWindowHandle อาจชี้ไปยังฟอร์ม Login/หน้าต่างว่าง แม้หน้าหลักเปิดอยู่แล้ว
                    // จึงไล่หน้าต่าง top-level ของ process HRMI และเลือกฟอร์มที่ไม่ใช่ Login
                    var frame = FindHrmiWindows(hrmiProc.Id)
                        .Where(IsVisibleWindow)
                        .FirstOrDefault(IsMainApplicationWindow);
                    readyWindow = frame;
                    return readyWindow != null;
                }
                catch { return false; }
            }, timeoutMs: timeoutSeconds * 1000, pollMs: 250);

            if (!isReady)
            {
                string windows = DescribeHrmiWindows();
                throw new TimeoutException($"❌ หน้าต่างหลักของ {ProcessName} ไม่พร้อมทำงานภายในเวลาที่กำหนด. หน้าต่างที่พบ: {windows}");
            }

            if (readyWindow != null)
                mainWindow = readyWindow;
        }

        /* Method 12: ดึง Window Object มาเก็บไว้ในตัวแปร mainWindow */
        protected static void AttachToMainWindowStatic()
        {
            var hrmiProcess = Process.GetProcessesByName(ProcessName).FirstOrDefault()
                ?? throw new Exception($"❌ ไม่พบโปรแกรม {ProcessName} กำลังรันอยู่");

            // ถ้าหน้าหลักถูกค้นพบจาก UI Automation แล้ว ใช้ handle นั้นโดยตรง
            // แทนการพึ่ง Process.MainWindowHandle ซึ่งอาจยังเป็นศูนย์หรือชี้ฟอร์มเก่า
            if (mainWindow != null && mainWindow.IsAvailable && IsMainApplicationWindow(mainWindow))
            {
                app = FlaUIApplication.Attach(hrmiProcess.Id);
                return;
            }

            if (!TryWaitForMainWindowHandle(hrmiProcess, MainWindowWaitSeconds))
                throw new Exception($"❌ {ProcessName} เปิดแล้วแต่หา MainWindowHandle ไม่ได้");

            app = FlaUIApplication.Attach(hrmiProcess.Id);

            Window? win = null;
            bool ok = WaitUntil(() =>
            {
                try
                {
                    win = automation.FromHandle(hrmiProcess.MainWindowHandle)?.AsWindow();
                    return win != null && !win.Properties.IsOffscreen.ValueOrDefault;
                }
                catch { return false; }
            }, timeoutMs: 15000);

            mainWindow = (ok && win != null) ? win : throw new Exception("❌ ดึง Window object จาก MainWindowHandle ไม่สำเร็จ");
        }

        private static IEnumerable<Window> FindHrmiWindows(int processId)
        {
            return automation.GetDesktop().FindAllChildren(cf => cf.ByProcessId(processId))
                .Where(e => e.ControlType == ControlType.Window)
                .Select(e => e.AsWindow())
                .Where(w => w != null)!;
        }

        private static bool IsVisibleWindow(Window window)
        {
            try { return window.IsAvailable && !window.Properties.IsOffscreen.ValueOrDefault; }
            catch { return false; }
        }

        private static bool IsMainApplicationWindow(Window window)
        {
            try
            {
                string title = window.Title?.Trim() ?? string.Empty;
                string id = window.AutomationId ?? string.Empty;
                // splash ขึ้นก่อนหน้าหลักระหว่างโปรแกรมเริ่มทำงาน ห้ามผูกเป็น mainWindow
                if (title.Contains("splash", StringComparison.OrdinalIgnoreCase)
                    || id.Contains("splash", StringComparison.OrdinalIgnoreCase))
                    return false;
                // หน้าหลักจริงคือ frmFrame / "Prosoft HRMI (...)"; ฟอร์มอื่น เช่น Configuration/Splash ไม่ใช่
                if (!id.Equals("frmFrame", StringComparison.OrdinalIgnoreCase)
                    && !title.StartsWith("Prosoft HRMI", StringComparison.OrdinalIgnoreCase))
                    return false;
                return !string.IsNullOrWhiteSpace(title)
                    && !title.Equals("Login", StringComparison.OrdinalIgnoreCase)
                    && !title.Contains("เข้าสู่ระบบ", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static string DescribeHrmiWindows()
        {
            try
            {
                var process = Process.GetProcessesByName(ProcessName).FirstOrDefault();
                if (process == null) return "ไม่พบ process";
                var details = FindHrmiWindows(process.Id)
                    .Select(w =>
                    {
                        try { return $"'{w.Title}' (id='{w.AutomationId}', offscreen={w.Properties.IsOffscreen.ValueOrDefault})"; }
                        catch { return "<อ่านรายละเอียดหน้าต่างไม่ได้>"; }
                    });
                return string.Join(", ", details.DefaultIfEmpty("ไม่พบหน้าต่าง UIA"));
            }
            catch (Exception ex) { return $"อ่านหน้าต่างไม่สำเร็จ: {ex.Message}"; }
        }

        private static bool TryWaitForMainWindowHandle(Process process, int timeoutSeconds)
        {
            return WaitUntil(() =>
            {
                process.Refresh();
                return process.MainWindowHandle != IntPtr.Zero;
            }, timeoutMs: timeoutSeconds * 1000, pollMs: 500);
        }

        protected Window GetCurrentMainWindow()
        {
            EnsureNoBlockingPopup();
            AttachToMainWindowStatic();
            return mainWindow;
        }

        //=========================================
        // Profile & Database Connection Setup
        //=========================================
        private static void SetupConnectionProfile(Dictionary<string, string> config)
        {
            try
            {
                var chkEdit = mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(IdChkProfile))?.AsCheckBox();
                if (chkEdit != null && chkEdit.IsChecked == false)
                {
                    chkEdit.Click();
                    WaitUntil(() => mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(IdTxtServerName))?.IsEnabled == true, 3000);
                }

                string? profile = GetConfigValue(config, "Profile", "PROFILE_NAME");
                if (!string.IsNullOrWhiteSpace(profile))
                {
                    mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(IdCmbProfile))?.AsComboBox()?.Select(profile);
                }

                FillFieldValue(IdTxtServerName, GetConfigValueRaw(config, "DB_SERVER", "SERVER_NAME"));
                FillFieldValue(IdTxtDataBase, GetConfigValueRaw(config, "DB_NAME", "DATABASE"));
                FillFieldValue(IdTxtDataBaseLogin, GetConfigValueRaw(config, "DB_USER", "DB_LOGIN"));
                FillFieldValue(IdTxtDataBasePassword, GetConfigValueRaw(config, "DB_PASS", "DB_PASSWORD"));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️️ ตั้งค่า Profile ไม่สำเร็จ: {ex.Message}");
            }
        }

        private static void FillFieldValue(string automationId, string? text)
        {
            if (text == null) return;

            var element = mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
            if (element != null)
            {
                var txtBox = element.AsTextBox();
                txtBox.Focus();
                txtBox.Text = text;
            }
        }

        //=========================================
        // Login elements & Actions
        //=========================================
        protected static AutomationElement? TxtUsername => mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(IdUsername));
        protected static AutomationElement? TxtPassword => mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(IdPassword));
        protected static AutomationElement? BtnLogin => mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(IdBtnLogin));
        protected static AutomationElement? BtnCancel => mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(IdBtnCancel));
        protected static AutomationElement? GridOrganization => mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(IdGridOrg));

        protected static void EnterUsername(string username) => EnterText(WaitForLoginField(IdUsername), username);
        protected static void EnterPassword(string password) => EnterText(WaitForLoginField(IdPassword), password);

        protected static void ClickLogin()
        {
            var btn = WaitForElement(IdBtnLogin, DefaultTimeout) ?? throw new Exception("ไม่พบปุ่ม Login");
            btn.AsButton().Click();
        }

        protected static void ClickCancel()
        {
            var btn = WaitForElement(IdBtnCancel, DefaultTimeout) ?? throw new Exception("ไม่พบปุ่ม Cancel");
            btn.AsButton().Click();
            WaitUntil(() => mainWindow == null || !mainWindow.IsAvailable, timeoutMs: 5000);
        }

        protected static void Login(string username, string password)
        {
            EnterUsername(username);
            EnterPassword(password);
            ClickLogin();
        }

        /* Method 20: กรอกข้อมูลเข้าสู่ระบบและดักจับ Pop-up จนเข้าสู่โปรแกรมสำเร็จ */
        protected static void LoginAndSelectOrganization(string username, string password)
        {
            EnterUsername(username);
            EnterPassword(password);
            ClickLogin();

            var desktop = automation.GetDesktop();
            var sw = Stopwatch.StartNew();

            while (sw.Elapsed < PopupTotalWaitTimeout)
            {
                var activeWindows = GetActiveWindows(desktop);

                var orgWindow = FindOrganizationWindow(activeWindows);
                if (orgWindow != null)
                {
                    DismissOrganizationWindow(orgWindow);
                    sw.Restart();
                    continue;
                }

                var warningWindow = FindWarningWindow(activeWindows);
                if (warningWindow != null)
                {
                    DismissWarningWindow(warningWindow);
                    sw.Restart();
                    continue;
                }

                var msgBoxWin = activeWindows.FirstOrDefault(w =>
                {
                    try
                    {
                        string className = w.Properties.ClassName.Value ?? string.Empty;
                        string title = w.Name ?? string.Empty;
                        bool isFrmMsgBox = className.Trim().Equals(WarningWindowClassName, StringComparison.OrdinalIgnoreCase);
                        bool isAppPopup = title.Equals("Application", StringComparison.OrdinalIgnoreCase) && w.FindAllDescendants(cf => cf.ByControlType(ControlType.Button)).Length > 0;
                        return isFrmMsgBox || isAppPopup;
                    }
                    catch { return false; }
                });

                if (msgBoxWin != null)
                {
                    string winText = string.Empty;
                    try { winText = string.Join(" ", msgBoxWin.FindAllDescendants().Select(el => el.Name)); } catch { }

                    string btnTarget = winText.Contains(BackupQuestionText, StringComparison.OrdinalIgnoreCase) ? "No" : "OK";
                    Debug.WriteLine($"ℹ️ พบ frmMessageBox -> กำลังจัดการคลิกปุ่ม [{btnTarget}]");
                    HandleGenericPopup(msgBoxWin, btnTarget);

                    sw.Restart();
                    continue;
                }

                // ตรวจสอบสถานะหน้าต่างหลักแบบปลอดภัย ป้องกัน UIA Timeout ขัดจังหวะการรัน
                try
                {
                    var hrmiProcess = Process.GetProcessesByName(ProcessName).FirstOrDefault();
                    if (hrmiProcess != null)
                    {
                        var appWindow = FindHrmiWindows(hrmiProcess.Id)
                            .FirstOrDefault(w => IsVisibleWindow(w) && IsMainApplicationWindow(w));
                        if (appWindow != null)
                        {
                            mainWindow = appWindow;
                            return;
                        }
                    }
                }
                catch
                {
                    // ปล่อยให้ลูปตรวจสอบต่อกรณี UI กำลังเรนเดอร์
                }

                Thread.Sleep(200);
            }

            // Previously this method returned silently after 60 seconds, so setup continued and
            // later blamed main-window attachment even though HRMI was still showing Login.
            throw new TimeoutException($"Login ไม่สำเร็จภายใน {PopupTotalWaitTimeout.TotalSeconds:0} วินาที; หน้าต่างที่พบ: {DescribeHrmiWindows()}; {DescribeLoginFormState()}");
        }

        private static string DescribeLoginFormState()
        {
            try
            {
                var login = FindHrmiWindows(Process.GetProcessesByName(ProcessName).FirstOrDefault()?.Id ?? -1)
                    .FirstOrDefault(w => string.Equals(w.Title?.Trim(), "Login", StringComparison.OrdinalIgnoreCase));
                if (login == null) return "ไม่พบฟอร์ม Login";
                var controls = login.FindAllDescendants()
                    .Select(e =>
                    {
                        string id = e.Properties.AutomationId.ValueOrDefault ?? "";
                        string name = e.Name ?? "";
                        string value = "";
                        if (id != IdPassword)
                        {
                            try { value = e.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault ?? ""; } catch { }
                        }
                        return new { Id = id, Name = name, Value = value };
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.Id) || !string.IsNullOrWhiteSpace(x.Name) || !string.IsNullOrWhiteSpace(x.Value))
                    .Select(x => string.IsNullOrWhiteSpace(x.Value)
                        ? $"{x.Name} (id='{x.Id}')"
                        : $"{x.Name} (id='{x.Id}', value='{x.Value}')")
                    .Distinct().Take(35);
                var loginButton = login.FindFirstDescendant(cf => cf.ByAutomationId(IdBtnLogin));
                string buttonState = loginButton == null ? "ไม่พบปุ่ม Login" :
                    $"ปุ่ม Login enabled={loginButton.Properties.IsEnabled.ValueOrDefault}, rect={loginButton.BoundingRectangle}";
                var errorPanel = login.FindFirstDescendant(cf => cf.ByAutomationId(IdErrorPanel));
                string errorText = errorPanel == null ? "ไม่พบ error panel" :
                    string.Join(" ", errorPanel.FindAllDescendants().Select(e => e.Name).Where(n => !string.IsNullOrWhiteSpace(n)));
                return $"Login controls: {string.Join(" | ", controls)}; {buttonState}; Login error text: '{errorText}'";
            }
            catch (Exception ex) { return $"อ่านสถานะ Login ไม่ได้: {ex.Message}"; }
        }

        /* Method 21: จัดการเคลียร์ Pop-up ขัดขวางทั้งหมด */
        protected static void EnsureNoBlockingPopup()
        {
            if (automation == null) return;
            try
            {
                var desktop = automation.GetDesktop();
                var activeWindows = GetActiveWindows(desktop);

                var orgWindow = FindOrganizationWindow(activeWindows);
                if (orgWindow != null)
                {
                    DismissOrganizationWindow(orgWindow);
                    return;
                }

                var warningWindow = FindWarningWindow(activeWindows);
                if (warningWindow != null)
                {
                    DismissWarningWindow(warningWindow);
                }

                foreach (var win in activeWindows)
                {
                    try
                    {
                        string className = win.Properties.ClassName.Value ?? string.Empty;
                        string title = win.Name ?? string.Empty;
                        if (className.Trim().Equals(WarningWindowClassName, StringComparison.OrdinalIgnoreCase) || title.Equals("Application", StringComparison.OrdinalIgnoreCase))
                        {
                            string winText = string.Join(" ", win.FindAllDescendants().Select(el => el.Name));
                            string btnTarget = winText.Contains(BackupQuestionText, StringComparison.OrdinalIgnoreCase) ? "No" : "OK";
                            HandleGenericPopup(win, btnTarget);
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ EnsureNoBlockingPopup Error: {ex.Message}");
            }
        }

        //=========================================
        // Login assertions
        //=========================================
        protected static bool IsOrganizationVisible() => WaitForElement(IdGridOrg, TimeSpan.FromSeconds(3)) != null;

        protected static bool IsOrganizationGone()
        {
            return WaitUntil(() =>
            {
                var el = mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(IdGridOrg));
                return el == null || el.Properties.IsOffscreen.ValueOrDefault;
            }, timeoutMs: 5000);
        }

        protected static bool IsErrorDialogVisible()
        {
            var panel = WaitForElement(IdErrorPanel, TimeSpan.FromSeconds(4));
            return panel != null && !panel.Properties.IsOffscreen.ValueOrDefault;
        }

        protected static void DismissErrorDialog()
        {
            var panel = WaitForElement(IdErrorPanel, TimeSpan.FromSeconds(4));
            if (panel != null)
            {
                var okBtn = panel.FindFirstDescendant(cf => cf.ByName("OK"));
                if (okBtn != null) okBtn.Click();
                else Keyboard.Press(VirtualKeyShort.ENTER);

                WaitUntil(() => panel.Properties.IsOffscreen.ValueOrDefault || !panel.IsAvailable, timeoutMs: 3000);
            }
        }

        protected static AutomationElement? WaitForLoginElement(string automationId, TimeSpan timeout) => WaitForElement(automationId, timeout);

        private static AutomationElement WaitForLoginField(string automationId)
        {
            var sw = Stopwatch.StartNew();
            var timeout = TimeSpan.FromSeconds(LoginFormWaitTimeoutSeconds);

            while (sw.Elapsed < timeout)
            {
                try
                {
                    if (mainWindow != null && mainWindow.IsAvailable)
                    {
                        var field = mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
                        if (field != null && !field.Properties.IsOffscreen.ValueOrDefault && field.IsEnabled)
                        {
                            return field;
                        }
                    }
                    else
                    {
                        var hrmiProc = Process.GetProcessesByName(ProcessName).FirstOrDefault();
                        if (hrmiProc != null && hrmiProc.MainWindowHandle != IntPtr.Zero)
                        {
                            var win = automation.FromHandle(hrmiProc.MainWindowHandle)?.AsWindow();
                            if (win != null && win.IsAvailable)
                            {
                                mainWindow = win;
                                var field = mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
                                if (field != null && !field.Properties.IsOffscreen.ValueOrDefault && field.IsEnabled)
                                {
                                    return field;
                                }
                            }
                        }
                    }

                    EnsureNoBlockingPopup();
                }
                catch { }

                Thread.Sleep(150);
            }

            string windowTitle = "(ไม่พบหน้าต่างหลัก)";
            string controls = "(อ่านรายการ controls ไม่ได้)";
            try
            {
                if (mainWindow != null && mainWindow.IsAvailable)
                {
                    windowTitle = mainWindow.Title ?? "(ไม่มีชื่อหน้าต่าง)";
                    controls = string.Join(" | ", mainWindow.FindAllDescendants()
                        .Take(100)
                        .Select(e =>
                        {
                            string name = "", id = "", type = "";
                            try { name = e.Name ?? ""; } catch { }
                            try { id = e.AutomationId ?? ""; } catch { }
                            try { type = e.ControlType.ToString(); } catch { }
                            return $"{type}: Name='{name}', Id='{id}'";
                        }));
                }
            }
            catch { }

            throw new Exception($"❌ ไม่พบช่องกรอก '{automationId}' บนหน้า Login ภายในเวลาที่กำหนด. Window='{windowTitle}'. Controls: {controls}");
        }

        //=========================================
        // Window Detection & Dismiss Helpers
        //=========================================
        private static List<Window> GetActiveWindows(AutomationElement desktop)
        {
            var result = new List<Window>();
            try
            {
                var allWindows = desktop.FindAllChildren(cf => cf.ByControlType(ControlType.Window));
                foreach (var el in allWindows)
                {
                    SafeDispose(() =>
                    {
                        var win = el.AsWindow();
                        if (win != null && win.IsAvailable && !win.Properties.IsOffscreen.ValueOrDefault)
                            result.Add(win);
                    });
                }
            }
            catch { }
            return result;
        }

        private static Window? FindOrganizationWindow(List<Window> windows) => windows.FirstOrDefault(IsOrganizationWindow);
        private static Window? FindWarningWindow(List<Window> windows) => windows.FirstOrDefault(IsWarningWindow);

        private static bool IsOrganizationWindow(Window w)
        {
            try { return w.FindFirstDescendant(cf => cf.ByAutomationId(IdGridOrg)) != null; }
            catch { return false; }
        }

        private static bool IsWarningWindow(Window w)
        {
            try
            {
                var className = w.Properties.ClassName.IsSupported ? (w.Properties.ClassName.Value ?? string.Empty) : string.Empty;
                var name = w.Name ?? string.Empty;
                return className.Trim().Equals(WarningWindowClassName, StringComparison.OrdinalIgnoreCase)
                    || name.Contains(WarningWindowTitleContains, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static void DismissOrganizationWindow(Window orgWindow)
        {
            orgWindow.FindFirstDescendant(cf => cf.ByAutomationId(IdBtnOK))?.AsButton()?.Click();
            WaitForWindowClosed(orgWindow);
        }

        private static void DismissWarningWindow(Window warningWindow)
        {
            try
            {
                warningWindow.Focus();
                Thread.Sleep(100);

                var btnOk = warningWindow.FindFirstDescendant(cf =>
                    cf.ByName("OK")
                    .Or(cf.ByName("&OK"))
                    .Or(cf.ByName("ตกลง"))
                    .Or(cf.ByAutomationId("btnOK"))
                    .Or(cf.ByControlType(ControlType.Button)));

                if (btnOk != null)
                {
                    if (btnOk.Patterns.Invoke.IsSupported)
                        btnOk.Patterns.Invoke.Pattern.Invoke();
                    else
                        btnOk.AsButton().Click();
                }
                else
                {
                    Keyboard.Type(VirtualKeyShort.ENTER);
                }
            }
            catch
            {
                try { Keyboard.Type(VirtualKeyShort.ENTER); } catch { }
            }

            WaitForWindowClosed(warningWindow);
        }

        private static void HandleGenericPopup(Window window, string desiredButton)
        {
            try
            {
                window.Focus();
                Thread.Sleep(300);

                AutomationElement? targetBtn = null;
                string targetId = desiredButton.ToLower() switch
                {
                    "ok" => "btnOK",
                    "cancel" => "btnCancel",
                    "yes" => "btnYes",
                    "no" => "btnNo",
                    "yes to all" => "btnYesAll",
                    "no to all" => "btnNoAll",
                    _ => $"btn{desiredButton}"
                };

                targetBtn = window.FindFirstDescendant(cf => cf.ByAutomationId(targetId));

                if (targetBtn == null)
                {
                    targetBtn = window.FindFirstDescendant(cf => cf.ByName(desiredButton))
                             ?? window.FindFirstDescendant(cf => cf.ByName($"&{desiredButton}"));
                }

                if (targetBtn == null)
                {
                    var buttons = window.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
                    if (buttons.Length > 0)
                    {
                        targetBtn = desiredButton.Equals("No", StringComparison.OrdinalIgnoreCase) || desiredButton.Equals("Cancel", StringComparison.OrdinalIgnoreCase)
                            ? buttons.Last()
                            : buttons.First();
                    }
                }

                if (targetBtn != null)
                {
                    try { targetBtn.AsButton().Click(); }
                    catch
                    {
                        if (targetBtn.Patterns.Invoke.IsSupported)
                            targetBtn.Patterns.Invoke.Pattern.Invoke();
                        else
                            Keyboard.Type(VirtualKeyShort.ENTER);
                    }
                }
                else
                {
                    Keyboard.Type(VirtualKeyShort.ENTER);
                }

                WaitForWindowClosed(window);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ HandleGenericPopup Error: {ex.Message}");
                try { Keyboard.Type(VirtualKeyShort.ENTER); } catch { }
            }
        }

        //=========================================
        // Sync / Polling State Helpers
        //=========================================
        private static void EnterText(AutomationElement element, string text)
        {
            var txt = element.AsTextBox();
            txt.Focus();
            txt.Text = text;
        }

        private static AutomationElement? WaitForElement(string automationId, TimeSpan timeout)
        {
            AutomationElement? found = null;
            WaitUntil(() =>
            {
                found = mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
                return found != null && !found.Properties.IsOffscreen.ValueOrDefault;
            }, (int)timeout.TotalMilliseconds, pollMs: 150);
            return found;
        }

        private static bool WaitForWindowClosed(Window win, int timeoutMs = 4000)
        {
            return WaitUntil(() =>
            {
                try { return !win.IsAvailable || win.Properties.IsOffscreen.ValueOrDefault; }
                catch { return true; }
            }, timeoutMs, pollMs: 100);
        }

        protected static bool WaitUntil(Func<bool> condition, int timeoutMs = 10000, int pollMs = 100)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try { if (condition()) return true; } catch { }
                Thread.Sleep(pollMs);
            }
            return false;
        }
    }
}
