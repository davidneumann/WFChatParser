using Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace WarframeDriver
{
    public class KeyboardHelper : IKeyboard
    {
        private readonly uint KEYEVENTF_KEYUP = 2;

        public void SendEscape()
        {
            Keyboard.SendScancode(Keyboard.ScanCodeShort.ESCAPE);
            //keybd_event(0x1B, 0, 0, 0);
            //System.Threading.Thread.Sleep(66);
            //keybd_event(0x1B, 0, KEYEVENTF_KEYUP, 0);
            System.Threading.Thread.Sleep(66);
        }

        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);

        public void SendPaste(string text)
        {
            TextCopy.Clipboard.SetText(text);

            byte VK_CONTROL = 0x11;
            keybd_event(VK_CONTROL, 0, 0, 0);
            System.Threading.Thread.Sleep(66);
            keybd_event(0x56, 0, 0, 0);
            System.Threading.Thread.Sleep(66);

            keybd_event(0x56, 0, KEYEVENTF_KEYUP, 0);
            System.Threading.Thread.Sleep(66);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0);// 'Left Control Up
            System.Threading.Thread.Sleep(66);

            //Keyboard.SendScancodes(Keyboard.ScanCodeShort.KEY_V, Keyboard.ScanCodeShort.CONTROL);
            //Thread.Sleep(100);
            //Keyboard.SendUp(Keyboard.ScanCodeShort.CONTROL);
            //Thread.Sleep(100);
        }

        public void SendSpace()
        {
            keybd_event(0x20, 0, 0, 0);
            System.Threading.Thread.Sleep(66);
            keybd_event(0x20, 0, KEYEVENTF_KEYUP, 0);
            System.Threading.Thread.Sleep(66);
        }
    }
}
