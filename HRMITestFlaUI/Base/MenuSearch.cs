using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using System;
using System.Diagnostics;
using System.Threading;

namespace HRMITestFlaUI.Base
{
    /// <summary>
    /// เปิดหน้าจอด้วยช่องค้นหาเมนู (baritemSearch) แบบเดียวกับโมดูล Setup / Personnel
    /// แทนการคลิกแท็บแล้วเลือกแถวเมนูตามลำดับ (treeListColumn row N) ซึ่งพังเมื่อลำดับเมนูเปลี่ยน
    /// </summary>
    public static class MenuSearch
    {
        public static void Open(AutomationElement mainWindow, string menuLabel)
        {
            var searchBox = Retry(() => mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("baritemSearch"))
                                        ?? mainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)), 4000)
                            ?? throw new Exception("ไม่พบช่องค้นหา baritemSearch");

            // ช่องค้นหาถูก disable ชั่วคราวระหว่างที่ HRMI กำลังเปิดหน้าจอ → รอให้กดได้ก่อน
            // (SetValue ตอน disabled โยน ElementNotEnabledException)
            WaitUntil(() => searchBox.IsEnabled, 8000);
            WaitUntil(() => { try { searchBox.Focus(); return true; } catch { return false; } }, 1000);

            bool typed = false;
            if (searchBox.Patterns.Value.IsSupported)
            {
                try { searchBox.Patterns.Value.Pattern.SetValue(menuLabel); typed = true; } catch { }
            }
            if (!typed)
            {
                var r = searchBox.BoundingRectangle;
                Mouse.Click(new System.Drawing.Point((int)(r.Left + r.Width / 2), (int)(r.Top + r.Height / 2)));
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                Keyboard.Type(menuLabel);
            }

            var searchBtn = Retry(() => mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("barChkSearch"))
                                        ?? mainWindow.FindFirstDescendant(cf => cf.ByName("Find")), 4000)
                            ?? throw new Exception("ไม่พบปุ่มค้นหา barChkSearch");
            searchBtn.Click();

            var menuResult = Retry(() => mainWindow.FindFirstDescendant(cf => cf.ByName(menuLabel))
                                         ?? mainWindow.FindFirstDescendant(cf => cf.ByName("treeListColumn row 1")), 4000)
                             ?? throw new Exception($"ค้นหา '{menuLabel}' แล้วไม่พบผลลัพธ์เมนู");

            try { menuResult.Focus(); } catch { }
            menuResult.Click();
            Keyboard.Press(VirtualKeyShort.ENTER);
            Keyboard.Release(VirtualKeyShort.ENTER);
        }

        private static T? Retry<T>(Func<T?> finder, int timeoutMs) where T : class
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try { var r = finder(); if (r != null) return r; } catch { }
                Thread.Sleep(50);
            }
            return null;
        }

        private static bool WaitUntil(Func<bool> condition, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try { if (condition()) return true; } catch { }
                Thread.Sleep(50);
            }
            return false;
        }
    }
}
