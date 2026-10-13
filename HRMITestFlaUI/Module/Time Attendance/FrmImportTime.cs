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
using System.IO;
using System.Linq;
using System.Threading;

namespace HRMITestFlaUI.Module.Time_Attendance
{
    /// <summary>
    /// ทดสอบหน้าจอ "Import ข้อมูลเวลาการทำงาน" (TestStep.xlsx ชีท "Import ข้อมูลเวลาการทำงาน")
    /// วันที่เริ่มต้น -> วันที่สิ้นสุด -> ติ๊ก Import File จาก Excel -> ค้นหาไฟล์ -> Next -> Import -> Next -> Finish
    /// </summary>
    internal class FrmImportTime
    {
        // =========================================================================================
        // หมวดที่ 1: ข้อมูลการทดสอบ (Test Data)
        // =========================================================================================

        // --- Data Records สำหรับใช้งานในแต่ละเฟส ---
        private sealed record ImportItem(
            string Name,
            string Start,
            string End,
            string File);

        // [ขั้นที่ 1] ข้อมูลทดสอบ Import ข้อมูลเวลาการทำงาน (Import)  Progress: 5 -> 100
        private static readonly ImportItem[] Step1_ImportCases =
        {
            new("TestCase_ImportTimeAttendance_1", "1/1/2569", "31/12/2569", "Import_TimeAttendance_Data")
        };

        // ไฟล์ Import (ใช้ค้นหาไฟล์ตอนกด Browse)
        private const string ImportDataFolder = @"D:\Import_TimeAttendance_Data";
        private static readonly string[] ImportFileExtensions = { ".xlsx", ".xls", ".xlsm", ".csv" };


        // =========================================================================================
        // หมวดที่ 2: ส่วนที่ขึ้นกับหน้าจอนั้นๆ (Screen-Specific Implementation)
        // **เมื่อเปลี่ยนไปทำหน้าจออื่น ต้องมาแก้ไขค่าและเมทอดในส่วนนี้ทั้งหมด**
        // =========================================================================================

        // 2.1 Locator & Mappings ประจำหน้าจอ
        private const string LogSource = "FrmImportTime";

        // ── เมนูหลัก ──
        private const string TimeAttendanceMenuName = "Time Attendance";
        private const string ImportMenuRowName = "treeListColumn row 6";
        private const string ImportMenuLabel = "Import ข้อมูลเวลาการทำงาน";

        // ── Automation ID ──
        private const string IdDateStart = "devImportFileStartDate";
        private const string IdDateEnd = "devImportFileEndDate";
        private const string IdRdgImportFileExcel = "devRdgImportFileExcel";
        private const string IdTxtFilePath = "TxtEditFilePath";
        private const string IdBtnBrowse = "devBtnBrows";
        private const string IdBtnNext = "devBtnNext";

        // ── ชื่อปุ่มสำหรับจับคู่ (Exact ก่อน แล้วค่อย Contains) และชื่อที่ห้ามกด ──
        private const string NextButtonName = "Next >";
        private const string ImportButtonName = "Import";
        private static readonly string[] NextButtonNames = { "Next", "ถัดไป" };
        private static readonly string[] NextButtonExactNames = { "Next >", "ถัดไป >" };
        private static readonly string[] FinishButtonNames = { "Finish", "เสร็จสิ้น" };
        private static readonly string[] ImportButtonNames = { "Import", "นำเข้า" };
        private static readonly string[] ForbiddenNavNames = { "Cancel", "ยกเลิก" };

        // 2.2 ค่า Timeout / Delay ประจำหน้าจอ
        private const int MenuTransitionDelayMs = 400;
        private const int PageLoadDelayMs = 800;
        private const int WizardStepDelayMs = 800;
        private const int WizardProcessingDelayMs = 2500;
        private const int WizardProcessingMaxWaitMs = 75000;   // ระบบ import/ประมวลผลไฟล์จริงอาจใช้เวลานาน
        private const int WizardProcessingPollMs = 1000;
        private const int TypingDelayMs = 100;
        private const int ToggleDelayMs = 250;
        private const int OpenDialogDelayMs = 1200;
        private const int FieldBindRetryTimeoutMs = 15000;
        private const int FieldBindRetryPollMs = 500;
        private const int MaxMenuScrollSteps = 10;

        /// <summary>
        /// ขั้นที่ 1: เปิดหน้าจอ Import ข้อมูลเวลาการทำงาน (Progress 0 -> 5)
        /// </summary>
        public void NavigateToImportTimeAttendanceDataUC()
        {
            TestProgressHelper.Report(2, "Opening Time Attendance -> Import Time Data...");
            if (!TryOpenMenu("NavigateToImportTimeAttendanceDataUC")) return;
            TestProgressHelper.Report(5, "Import Time Data screen opened");
        }

        /// <summary>
        /// ขั้นที่ 2: ทดสอบ Import ข้อมูลเวลาการทำงาน — รันทุก scenario (เปิดหน้าจอใหม่ทุกครั้ง) แล้วสรุปผลรวม (Progress 5 -> 100)
        /// </summary>
        public void RunAllValidationTests()
        {
            TestLogger.LogInfo(LogSource, "เริ่มทดสอบหน้าจอ Import ข้อมูลเวลาการทำงาน");

            foreach (var i in Step1_ImportCases)
                RunScenario(i.Name, () => ImportTimeAttendanceData(i.Start, i.End, i.File));

            TestLogger.LogInfo(LogSource, $"สรุปผลรวมทั้งหมด: สำเร็จ {_totalSuccess} รายการ, ไม่สำเร็จ {_totalFail} รายการ");
        }

        // --- Custom Mapping Logic ประจำหน้าจอนี้ ---

        /* ขั้นตอน Import ตาม Test Step: วันที่ -> Excel -> เลือกไฟล์ -> Next -> Import -> Next -> Finish */
        private void ImportTimeAttendanceData(string start, string end, string fileName)
        {
            _ = Retry(FindDateStartField, 8000)
                ?? throw new Exception($"หน้าจอ '{ImportMenuLabel}' ไม่เปิดขึ้น (หาช่องวันที่เริ่มต้น '{IdDateStart}' ไม่เจอ)");
            TestProgressHelper.Report(10, "Import screen ready");

            FillDateField(IdDateStart, start);
            FillDateField(IdDateEnd, end);
            TestProgressHelper.Report(25, "Filled date range");

            SelectRadioButton(IdRdgImportFileExcel);
            TestProgressHelper.Report(30, "Selected Import File from Excel");

            BrowseAndSelectFile(fileName);
            TestProgressHelper.Report(45, "Selected import file");

            ClickNextButton();
            TestProgressHelper.Report(55, "Clicked Next");

            ClickImportButton();
            TestProgressHelper.Report(75, "Imported data");

            ClickNextButton();
            TestProgressHelper.Report(85, "Clicked Next");

            ClickFinishButton();
            TestProgressHelper.Report(100, "Clicked Finish");
        }

        /* ตัวช่วยรัน Scenario: เปิดหน้าจอ -> รัน -> Log ผล -> ปิด dialog ค้าง */
        private void RunScenario(string testName, Action step)
        {
            try
            {
                OpenMenu();
                step();
                LogSuccess(testName, "ทำสำเร็จตามขั้นตอนครบถ้วน");
            }
            catch (Exception ex) { LogFailure(testName, $"ไม่สำเร็จ: {DescribeException(ex)}"); }
            finally { CloseAnyOpenDialog(); }
        }

        /* เปิดหน้าจอแบบไม่ throw (Log FAIL แล้วคืน false) */
        private bool TryOpenMenu(string stage)
        {
            try { OpenMenu(); return true; }
            catch (Exception ex) { LogFailure(stage, $"ไม่สำเร็จ: {DescribeException(ex)}"); return false; }
        }

        /* คลิกแท็บ Time Attendance -> เมนู Import ข้อมูลเวลาการทำงาน */
        private void OpenMenu()
        {
            var tab = Retry(() =>
                    _mainWindow.FindFirstDescendant(cf => cf.ByName(TimeAttendanceMenuName).And(cf.ByControlType(ControlType.TabItem)))
                    ?? _mainWindow.FindFirstDescendant(cf => cf.ByName(TimeAttendanceMenuName)), 5000)
                ?? throw new Exception($"หาเมนูหลัก '{TimeAttendanceMenuName}' ไม่เจอ");

            ClickCenter(tab);
            Thread.Sleep(MenuTransitionDelayMs);

            var menuRow = Retry(FindImportMenuRow, 3000) ?? ScrollToMenuRow()
                ?? throw new Exception($"หาเมนู '{ImportMenuLabel}' ('{ImportMenuRowName}') ไม่เจอ");

            ClickCenter(menuRow);
            Thread.Sleep(PageLoadDelayMs);
        }

        /* กด Line Down เลื่อนรายการเมนูลงจนเจอเมนูที่ต้องการ */
        private AutomationElement? ScrollToMenuRow()
        {
            var lineDown = _mainWindow.FindFirstDescendant(cf => cf.ByName("Line Down"));
            if (lineDown != null)
            {
                for (int i = 0; i < MaxMenuScrollSteps; i++)
                {
                    ClickCenter(lineDown);
                    var row = Retry(FindImportMenuRow, 500);
                    if (row != null) return row;
                }
            }
            return Retry(FindImportMenuRow, 3000);
        }

        private AutomationElement? FindImportMenuRow() =>
            _mainWindow.FindFirstDescendant(cf => cf.ByName(ImportMenuRowName))
            ?? _mainWindow.FindFirstDescendant(cf => cf.ByName(ImportMenuLabel));

        private AutomationElement? FindDateStartField() => FindDescendant(cf => cf.ByAutomationId(IdDateStart));

        /* ปิด dialog ค้างหลังจบ scenario (กด Cancel ถ้ามี) */
        private void CloseAnyOpenDialog()
        {
            try
            {
                var cancelBtn = FindDescendant(cf => cf.ByName("Cancel").Or(cf.ByName("ยกเลิก")));
                if (cancelBtn != null && cancelBtn.Properties.IsEnabled.ValueOrDefault)
                {
                    SafeClickElement(cancelBtn);
                    Thread.Sleep(PageLoadDelayMs);
                }
            }
            catch { }
        }

        /* กดปุ่มค้นหา (devBtnBrows) -> ใส่ path ไฟล์ในหน้าต่าง Open -> ยืนยันว่าช่องที่เก็บไฟล์ถูกเติมค่า */
        private void BrowseAndSelectFile(string fileName)
        {
            var btnBrowse = Retry(() => FindDescendant(cf => cf.ByAutomationId(IdBtnBrowse)))
                ?? FindDescendant(cf => cf.ByName("Browse"))
                ?? FindDescendant(cf => cf.ByName("ค้นหา"))
                ?? throw new Exception($"หาปุ่มค้นหา ('{IdBtnBrowse}') ไม่เจอ (ปุ่มที่เห็นตอนนี้: {DescribeVisibleButtons()})");

            ClickCenter(btnBrowse);
            Thread.Sleep(OpenDialogDelayMs);

            var openDialog = Retry(() =>
                    _automation.GetDesktop().FindFirstChild(cf => cf.ByName("Open").Or(cf.ByName("เปิด")).And(cf.ByControlType(ControlType.Window)))
                    ?? _automation.GetDesktop().FindAllChildren(cf => cf.ByControlType(ControlType.Window))
                        .FirstOrDefault(w => w.FindFirstDescendant(cf => cf.ByName("File name:").Or(cf.ByName("ชื่อไฟล์:"))) != null),
                    8000)
                ?? throw new Exception("หาหน้าต่าง Open file ไม่เจอ");

            var importFilePath = ResolveImportFilePath(fileName);
            if (!File.Exists(importFilePath))
                throw new FileNotFoundException("ไฟล์ Import ที่ค้นพบหายไปก่อนเปิด", importFilePath);

            EnterFilePathAndOpen(openDialog, importFilePath);

            if (Retry(() => FindDescendant(cf => cf.ByAutomationId(IdTxtFilePath)), 5000) == null)
                throw new Exception("หน้าต่าง Open ปิดแล้ว แต่ช่องที่เก็บไฟล์ยังไม่ถูกเติมค่า");
        }

        /* ใส่ path ลงช่อง File name -> กด Open (+ Invoke + Enter) -> รอจนหน้าต่าง Open ปิด */
        private void EnterFilePathAndOpen(AutomationElement openDialog, string importFilePath)
        {
            var fileNameBox = Retry(() =>
                    openDialog.FindFirstDescendant(cf => cf.ByAutomationId("1148"))?.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit))
                    ?? openDialog.FindFirstDescendant(cf => cf.ByName("File name:"))?.Parent?.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit))
                    ?? openDialog.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit))
                        .OrderByDescending(e => e.BoundingRectangle.Width).FirstOrDefault(),
                    5000)
                ?? throw new Exception($"พบไฟล์ '{importFilePath}' แล้ว แต่หาช่อง File name ไม่พบ");

            ClickCenter(fileNameBox);
            SelectAllText();

            var valuePattern = fileNameBox.Patterns.Value.PatternOrDefault;
            if (valuePattern != null) valuePattern.SetValue(importFilePath);
            else Keyboard.Type(importFilePath);
            Thread.Sleep(TypingDelayMs);

            var openBtn = Retry(() => openDialog.FindFirstDescendant(cf =>
                cf.ByName("Open").Or(cf.ByName("&Open")).Or(cf.ByName("เปิด")).And(cf.ByControlType(ControlType.Button))), 3000);
            if (openBtn != null && IsUsableButton(openBtn))
            {
                ClickCenter(openBtn);
                Thread.Sleep(150);
                // dialog บางแบบไม่รับคลิกขณะช่อง filename ยัง focus อยู่ -> Invoke + Enter ซ้ำ
                try { openBtn.Patterns.Invoke.Pattern.Invoke(); } catch { }
            }
            PressKey(VirtualKeyShort.RETURN);

            bool closed = WaitUntil(() => _automation.GetDesktop().FindFirstChild(cf =>
                cf.ByName("Open").Or(cf.ByName("เปิด")).And(cf.ByControlType(ControlType.Window))) == null, 10000, 150);
            if (!closed)
                throw new Exception("กด Open แล้ว แต่หน้าต่าง Open ยังไม่ปิด");
        }

        /* หาไฟล์ Import: ลอง path ที่รู้จักก่อน (D:\, Downloads ฯลฯ) แล้วค่อยไล่ค้นทุก Fixed drive */
        private static string ResolveImportFilePath(string requestedName)
        {
            var normalizedRequested = NormalizeFileName(requestedName);
            var requestedFileName = Path.GetFileNameWithoutExtension(requestedName);
            var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

            var preferredRoots = new[]
            {
                ImportDataFolder,
                downloads,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Camera Roll"),
                @"C:\Downloads"
            }.Where(Directory.Exists);

            var fileNameVariants = new[] { requestedFileName, "ImportTimeAttendanceData", "Import_TimeAttendance_Data" }
                .Distinct(StringComparer.OrdinalIgnoreCase);

            var directMatch = fileNameVariants
                .SelectMany(name => ImportFileExtensions.Select(ext => name + ext))
                .SelectMany(f => new[]
                {
                    Path.Combine(@"D:\", f),
                    Path.Combine(ImportDataFolder, f),
                    Path.Combine(downloads, f),
                    Path.Combine(@"C:\Downloads", f)
                })
                .Concat(new[] { requestedName })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(File.Exists);
            if (directMatch != null) return directMatch;

            var roots = preferredRoots.Concat(DriveInfo.GetDrives()
                    .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
                    .Select(d => d.RootDirectory.FullName))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var root in roots)
            {
                foreach (var directory in EnumerateDirectoriesSafe(root))
                {
                    IEnumerable<string> files;
                    try { files = Directory.EnumerateFiles(directory); }
                    catch { continue; }

                    var match = files.FirstOrDefault(path =>
                        ImportFileExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)
                        && NormalizeFileName(Path.GetFileNameWithoutExtension(path)) == normalizedRequested);
                    if (match != null) return match;
                }
            }

            throw new FileNotFoundException($"ไม่พบไฟล์ '{requestedName}' ในทุกไดรฟ์ของเครื่อง", requestedName);
        }

        private static string NormalizeFileName(string value) =>
            new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

        /* ไล่โฟลเดอร์แบบไม่ throw (ข้ามโฟลเดอร์ที่ไม่มีสิทธิ์และ ReparsePoint) */
        private static IEnumerable<string> EnumerateDirectoriesSafe(string root)
        {
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                yield return current;

                string[] children;
                try { children = Directory.GetDirectories(current); }
                catch { continue; }

                foreach (var child in children)
                {
                    try
                    {
                        if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0)
                            pending.Push(child);
                    }
                    catch { }
                }
            }
        }

        // --- Wizard Navigation Buttons (Next / Import / Finish) ประจำหน้าจอนี้ ---

        /* กดปุ่ม Import แล้วรอระบบประมวลผลจนปุ่ม Next/Finish พร้อม */
        private void ClickImportButton()
        {
            var button = Retry(() =>
                    SafeFindAllButtons(GetCurrentRoot()).FirstOrDefault(b =>
                        IsUsableButton(b) && NameLooksLike(b, ImportButtonNames) && !NameLooksLike(b, ForbiddenNavNames)),
                    WizardProcessingMaxWaitMs)
                ?? throw new Exception($"หาปุ่ม '{ImportButtonName}' ที่ใช้งานได้ไม่เจอ (ปุ่มที่เห็นตอนนี้: {DescribeVisibleButtons()})");

            WaitUntilEnabled(button, 3000);
            SafeClickElement(button);
            WaitForNextOrFinishReady();
        }

        /* คลิกปุ่ม Next ของ wizard (มีด่านกันกดผิดเป็น Cancel) */
        private void ClickNextButton()
        {
            Thread.Sleep(WizardProcessingDelayMs);
            WaitForNextOrFinishReady();

            var button = Retry(FindNextButton, WizardProcessingMaxWaitMs)
                ?? throw new Exception($"หาปุ่ม Next ('{NextButtonName}' / '{IdBtnNext}') ที่ใช้งานได้ไม่เจอ (ปุ่มที่เห็นตอนนี้: {DescribeVisibleButtons()})");

            if (NameLooksLike(button, ForbiddenNavNames))
                throw new Exception($"ปุ่มที่หาเจอไม่ใช่ Next แต่เป็น '{SafeName(button)}' — ยกเลิกการคลิกเพื่อกันกดผิดปุ่ม");

            WaitUntilEnabled(button, 3000);
            SafeClickElement(button);
            Thread.Sleep(WizardStepDelayMs);
        }

        /* กดปุ่ม Finish */
        private void ClickFinishButton()
        {
            var finishBtn = Retry(FindFinishButton, WizardProcessingMaxWaitMs)
                ?? throw new Exception($"หาปุ่ม 'Finish' ไม่เจอ (ปุ่มที่เห็นตอนนี้: {DescribeVisibleButtons()})");

            WaitUntilEnabled(finishBtn, 5000);
            SafeClickElement(finishBtn);
            Thread.Sleep(PageLoadDelayMs);
        }

        /* รอ (poll) จนกว่าปุ่ม Next หรือ Finish จะพร้อม */
        private void WaitForNextOrFinishReady() =>
            WaitUntil(() => FindFinishButton() != null || FindNextButton() != null, WizardProcessingMaxWaitMs, WizardProcessingPollMs);

        /* หาปุ่ม Next: AutomationId -> ชื่อตรงเป๊ะ -> ชื่อ Contains */
        private AutomationElement? FindNextButton()
        {
            var buttons = SafeFindAllButtons(GetCurrentRoot()).Where(IsUsableButton).ToList();

            var byId = buttons.FirstOrDefault(b => SafeAutomationId(b) == IdBtnNext);
            if (byId != null && !NameLooksLike(byId, ForbiddenNavNames)) return byId;

            return buttons.FirstOrDefault(b => NameEqualsAny(b, NextButtonExactNames) && !NameLooksLike(b, ForbiddenNavNames))
                ?? buttons.FirstOrDefault(b => NameLooksLike(b, NextButtonNames) && !NameLooksLike(b, ForbiddenNavNames));
        }

        private AutomationElement? FindFinishButton() =>
            SafeFindAllButtons(GetCurrentRoot()).Where(IsUsableButton)
                .FirstOrDefault(b => NameEqualsAny(b, FinishButtonNames) && !NameLooksLike(b, ForbiddenNavNames));


        // =========================================================================================
        // หมวดที่ 3: ส่วนการทำงานกลางที่ตายตัว (Common / Reusable Helpers)
        // **ใช้งานร่วมกันได้ทุกหน้าจอ ไม่ต้องแก้ไขคำสั่งเมื่อเปลี่ยนหน้า**
        // =========================================================================================

        private readonly AutomationElement _mainWindow;
        private readonly UIA3Automation _automation;
        private int _totalSuccess = 0, _totalFail = 0;
        private const int TimeoutMs = 6000;
        private const int PollMs = 300;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        public FrmImportTime(AutomationElement mainWindow, UIA3Automation automation)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        }

        // ── Field Helpers (Date / Radio) ──

        /* กรอกวันที่ลงช่อง DateEdit (Ctrl+A -> พิมพ์ -> Tab) retry เผื่อช่องยังไม่ bind */
        private void FillDateField(string automationId, string dateText)
        {
            RetryAction(() =>
            {
                var dateEdit = FindDescendant(cf => cf.ByAutomationId(automationId))
                    ?? throw new Exception($"หาช่องวันที่ '{automationId}' ไม่เจอ (ปุ่ม/หน้าจอที่เห็นตอนนี้: {DescribeVisibleButtons()})");

                WaitUntilEnabled(dateEdit, 3000);
                ClickCenter(dateEdit);
                Thread.Sleep(100);

                SelectAllText();
                Keyboard.Type(dateText);
                Thread.Sleep(TypingDelayMs);
                PressKey(VirtualKeyShort.TAB);
            }, FieldBindRetryTimeoutMs, FieldBindRetryPollMs);

            Thread.Sleep(WizardStepDelayMs);
        }

        /* คลิกเลือก Radio button ตาม AutomationId */
        private void SelectRadioButton(string automationId)
        {
            var container = Retry(() => FindDescendant(cf => cf.ByAutomationId(automationId)))
                ?? throw new Exception($"หา radio button '{automationId}' ไม่เจอ");

            var radio = container.AsRadioButton()
                ?? container.FindFirstDescendant(cf => cf.ByControlType(ControlType.RadioButton))?.AsRadioButton()
                ?? container;

            ClickCenter(radio);
            Thread.Sleep(ToggleDelayMs);
        }

        // ── Window / Search Root Helpers ──

        private AutomationElement? GetActiveTopLevelWindow()
        {
            try
            {
                var hwnd = GetForegroundWindow();
                return hwnd == IntPtr.Zero ? null : _automation.FromHandle(hwnd);
            }
            catch { return null; }
        }

        /* จุดเริ่มค้นหา: หน้าต่าง active ถ้าเป็นของ process เดียวกัน ไม่งั้นใช้ _mainWindow */
        private AutomationElement GetCurrentRoot()
        {
            try
            {
                var active = GetActiveTopLevelWindow();
                if (active != null && active.Properties.ProcessId.ValueOrDefault == _mainWindow.Properties.ProcessId.ValueOrDefault)
                    return active;
            }
            catch { }
            return _mainWindow;
        }

        /* หา element โดยเริ่มจากหน้าต่าง active ก่อน แล้ว fallback ไป _mainWindow */
        private AutomationElement? FindDescendant(Func<FlaUI.Core.Conditions.ConditionFactory, FlaUI.Core.Conditions.ConditionBase> condition)
        {
            AutomationElement? found = null;
            var root = GetCurrentRoot();
            try { found = root.FindFirstDescendant(condition); } catch { }
            if (found == null && !ReferenceEquals(root, _mainWindow))
            {
                try { found = _mainWindow.FindFirstDescendant(condition); } catch { }
            }
            return found;
        }

        // ── Click / Keyboard / Element Utilities ──

        private static Point GetCenter(Rectangle rect) => new Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);

        private static void ClickAt(Point p)
        {
            Mouse.MoveTo(p);
            Thread.Sleep(50);
            Mouse.Click(MouseButton.Left);
            Thread.Sleep(100);
        }

        private static void ClickCenter(AutomationElement e) => ClickAt(GetCenter(e.BoundingRectangle));

        /* คลิกอย่างปลอดภัย: รอพิกัดใช้ได้ -> element.Click() -> Invoke pattern */
        private static void SafeClickElement(AutomationElement element, int waitForRectMs = 3000)
        {
            if (WaitUntil(() => HasArea(element), waitForRectMs, 150))
            {
                ClickCenter(element);
                return;
            }

            try { element.Click(); }
            catch (FlaUI.Core.Exceptions.NoClickablePointException)
            {
                try
                {
                    var invoke = element.Patterns.Invoke.PatternOrDefault;
                    if (invoke != null) { invoke.Invoke(); return; }
                }
                catch { }
                throw new Exception($"ไม่สามารถคลิก '{SafeName(element)}' ได้ (หาตำแหน่งที่คลิกได้บนหน้าจอไม่เจอ)");
            }
        }

        private static void PressKey(VirtualKeyShort key)
        {
            Keyboard.Press(key);
            Keyboard.Release(key);
        }

        private static void SelectAllText()
        {
            Keyboard.Press(VirtualKeyShort.CONTROL);
            Keyboard.Press(VirtualKeyShort.KEY_A);
            Keyboard.Release(VirtualKeyShort.KEY_A);
            Keyboard.Release(VirtualKeyShort.CONTROL);
            Thread.Sleep(50);
        }

        private static bool HasArea(AutomationElement e)
        {
            try { var r = e.BoundingRectangle; return !r.IsEmpty && r.Width > 0 && r.Height > 0; }
            catch { return false; }
        }

        /* ปุ่มใช้งานได้จริง (มองเห็น, enable, มีพิกัดบนจอ) */
        private static bool IsUsableButton(AutomationElement element)
        {
            try
            {
                if (element.Properties.IsOffscreen.IsSupported && element.Properties.IsOffscreen.Value) return false;
                if (element.Properties.IsEnabled.IsSupported && !element.Properties.IsEnabled.Value) return false;
                return HasArea(element);
            }
            catch { return false; }
        }

        private static List<AutomationElement> SafeFindAllButtons(AutomationElement root)
        {
            try { return root.FindAllDescendants(cf => cf.ByControlType(ControlType.Button)).ToList(); }
            catch { return new List<AutomationElement>(); }
        }

        private static string SafeName(AutomationElement element)
        {
            try { return element?.Name ?? ""; } catch { return ""; }
        }

        private static string SafeAutomationId(AutomationElement element)
        {
            try { return element?.Properties.AutomationId.ValueOrDefault ?? ""; } catch { return ""; }
        }

        private static string CleanName(AutomationElement element) => SafeName(element).Replace("&", "").Trim();

        private static bool NameLooksLike(AutomationElement element, string[] keywords)
        {
            var clean = CleanName(element);
            return clean.Length > 0 && keywords.Any(k => clean.Contains(k, StringComparison.OrdinalIgnoreCase));
        }

        private static bool NameEqualsAny(AutomationElement element, string[] names)
        {
            var clean = CleanName(element);
            return clean.Length > 0 && names.Any(n => string.Equals(clean, n.Replace("&", "").Trim(), StringComparison.OrdinalIgnoreCase));
        }

        // ── Diagnostics, Logging & Retry Helpers ──

        private string DescribeVisibleButtons()
        {
            try
            {
                return string.Join(" | ", SafeFindAllButtons(GetCurrentRoot()).Where(IsUsableButton)
                    .Select(SafeName).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().Take(15));
            }
            catch { return "(อ่านรายชื่อปุ่มไม่ได้)"; }
        }

        private static string DescribeException(Exception ex)
        {
            string typeName = ex.GetType().FullName ?? ex.GetType().Name;
            string msg = string.IsNullOrWhiteSpace(ex.Message) ? "(ไม่มีข้อความ)" : ex.Message;
            string? firstStack = ex.StackTrace?.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
            return $"[{typeName}] {msg} | At: {firstStack}";
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

        /* ตัวช่วยวนเช็คเงื่อนไขจนเป็นจริงหรือหมดเวลา */
        private static bool WaitUntil(Func<bool> condition, int timeoutMs = TimeoutMs, int pollMs = 100)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try { if (condition()) return true; } catch { }
                Thread.Sleep(pollMs);
            }
            return false;
        }

        /* รอจนกว่า element จะ enable */
        private static bool WaitUntilEnabled(AutomationElement element, int timeoutMs = 3000) =>
            WaitUntil(() => element.Properties.IsEnabled.IsSupported && element.Properties.IsEnabled.Value, timeoutMs, 200);

        /* ตัวช่วย Retry ค้นหา Element ซ้ำๆ จนกว่าจะเจอหรือหมดเวลา */
        private static T? Retry<T>(Func<T?> finder, int timeoutMs = TimeoutMs, int pollMs = PollMs) where T : class
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
                Thread.Sleep(pollMs);
            }
            return null;
        }

        /* รัน Action ซ้ำจนกว่าจะไม่ throw หรือหมดเวลา (โยน error ล่าสุดออกไป) */
        private static void RetryAction(Action action, int timeoutMs = 8000, int pollMs = 400)
        {
            Exception? lastEx = null;
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try { action(); return; }
                catch (Exception ex) { lastEx = ex; Thread.Sleep(pollMs); }
            }
            throw lastEx ?? new Exception("การกระทำไม่สำเร็จ (timeout)");
        }
    }
}