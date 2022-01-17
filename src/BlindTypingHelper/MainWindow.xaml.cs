using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Forms;
using Label = System.Windows.Controls.Label;

namespace BlindTypingHelper;

public partial class MainWindow
{      
    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExA", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);


    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int CallNextHookEx(IntPtr hhk, int nCode, int wParam, ref KBDLLHOOKSTRUCT lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, IntPtr proccess);
    [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint thread);
    public CultureInfo GetCurrentKeyboardLayout() {
        try {
            IntPtr foregroundWindow = GetForegroundWindow();
            uint foregroundProcess = GetWindowThreadProcessId(foregroundWindow, IntPtr.Zero);
            int keyboardLayout = GetKeyboardLayout(foregroundProcess).ToInt32() & 0xFFFF;
            return new CultureInfo(keyboardLayout);
        } catch (Exception _) {
            return new CultureInfo(1033); // Assume English if something went wrong.
        }
    }


    public static int WhKeyboardLl = 13;
    private delegate int LowLevelKeyboardProc(int nCode, int wParam, ref KBDLLHOOKSTRUCT lParam);
    private static IntPtr _hookId = IntPtr.Zero;

#pragma warning disable CS0649
#pragma warning disable CS0169
    // ReSharper disable once InconsistentNaming
    private struct KBDLLHOOKSTRUCT
    {
        public Keys VkCode;
        int _scanCode;
        public int Flags;
        int _time;
        int _dwExtraInfo;
    }
#pragma warning restore CS0649
#pragma warning restore CS0169

    private readonly HashSet<Keys> _pressedButton = new();
    private readonly Dictionary<Keys, KeyData> _labelDictionary = new();
    private readonly HashSet<Keys> _leftKeys;
    private readonly HashSet<Keys> _rightKeys;

    private Keys _oldCode = 0;
    private int _oldFlag;
    private string? _prevCulture;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private int HookCallback(int nCode, int wParam, ref KBDLLHOOKSTRUCT lParam)
    {
        if (_oldFlag == lParam.Flags
            && _oldCode == lParam.VkCode)
        {
            return CallNextHookEx(_hookId, nCode, wParam, ref lParam);
        }
            
        _oldFlag = lParam.Flags;
        _oldCode = lParam.VkCode;

        var lShiftPressed = _pressedButton.Contains(Keys.LShiftKey);
        var rShiftPressed = _pressedButton.Contains(Keys.RShiftKey);

        if (rShiftPressed && _rightKeys.Contains(lParam.VkCode))
        {
            return 1;
        }

        if (lShiftPressed && _leftKeys.Contains(lParam.VkCode))
        {
            return 1;
        }
        
        switch (lParam.Flags)
        {
            case < 100:
                _pressedButton.Add(lParam.VkCode);
                break;
            case > 100:
                _pressedButton.Remove(lParam.VkCode);
                break;
        }

        lShiftPressed = _pressedButton.Contains(Keys.LShiftKey);
        rShiftPressed = _pressedButton.Contains(Keys.RShiftKey);

        var cultureChanged = false;
        var inputLanguage = GetCurrentKeyboardLayout();
        var currentCulture = inputLanguage.Parent.Name;
        Debug.WriteLine($"CurrentCulture {currentCulture}, prev {_prevCulture}");
        if (!Equals(currentCulture, _prevCulture))
        {
            _prevCulture = currentCulture;
            cultureChanged = true;
        }

        foreach (var label in _labelDictionary)
        {
            if ((lShiftPressed && _leftKeys.Contains(label.Key))
                || (rShiftPressed && _rightKeys.Contains(label.Key)))
            {
                label.Value.Unavailable = true;
            }
            else
            {
                label.Value.Unavailable = false;
            }

            if (cultureChanged)
            {
                (char? character, char? shiftCharacter) = GetCharacterByKey(label.Key, inputLanguage);
                label.Value.SetCharacter(character, shiftCharacter);
            }
            
            label.Value.IsPressed = _pressedButton.Contains(label.Key);
            label.Value.ShiftPressed(lShiftPressed || rShiftPressed);
        }

        return CallNextHookEx(_hookId, nCode, wParam, ref lParam);
    }

    // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
    private readonly LowLevelKeyboardProc? _callback;

    public MainWindow()
    {
        InitializeComponent();
        Topmost = true;
        
        _leftKeys = new HashSet<Keys>
        {
            Keys.Q,
            Keys.A,
            Keys.Z,
            Keys.W,
            Keys.S,
            Keys.X,
            Keys.E,
            Keys.D,
            Keys.C,
            Keys.R,
            Keys.F,
            Keys.V,
            Keys.T,
            Keys.G,
            Keys.B,
            Keys.D1,
            Keys.D2,
            Keys.D3,
            Keys.D4,
            Keys.D5
        };

        _rightKeys = new HashSet<Keys>
        {
            Keys.Y,
            Keys.H,
            Keys.N,
            Keys.U,
            Keys.J,
            Keys.M,
            Keys.I,
            Keys.K,
            Keys.Oemcomma,
            Keys.O,
            Keys.L,
            Keys.OemPeriod,
            Keys.P,
            Keys.Oem1,
            Keys.OemQuestion,
            Keys.OemOpenBrackets,
            Keys.Oem6,
            Keys.Oem7,
            Keys.Oem5,
            Keys.D7,
            Keys.D8,
            Keys.D9,
            Keys.D0,
            Keys.OemMinus,
            Keys.Oemplus,
            Keys.D6
        };

        foreach (var stackPanel in this.Form.Children.OfType<StackPanel>())
        {
            foreach (var label in stackPanel.Children.OfType<Label>())
            {
                if (int.TryParse(label.Tag?.ToString(), out var keyCode))
                {
                    var key = (Keys)keyCode;
                    var keyData = new KeyData();

                    _labelDictionary.Add(key, keyData);

                    label.DataContext = keyData;

                    var inputLanguage = GetCurrentKeyboardLayout();
                    (char? character, char? shiftCharacter) = GetCharacterByKey(key, inputLanguage);
                    keyData.SetCharacter(character, shiftCharacter);
                }
            }
        }

        _callback = (LowLevelKeyboardProc)HookCallback;
        _hookId = SetHook(_callback);
    }

    protected override void OnClosed(EventArgs e)
    {
        UnhookWindowsHookEx(_hookId);
    }

    private IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
            
        var moduleName = curModule?.ModuleName ?? throw new ArgumentException("Module name is null");
        return SetWindowsHookEx(WhKeyboardLl, proc, GetModuleHandle(moduleName), 0);
    }

    private (char?, char?) GetCharacterByKey(Keys key, CultureInfo cultureInfo)
    {
        var isEnglish = cultureInfo.Parent.Name == "en";

        return key switch
        {
            Keys.Oemtilde => isEnglish ? ('`', '~') : ('ё', 'Ё'),
            Keys.D1 => isEnglish ? ('1', '!') : ('1', '!'),
            Keys.D2 => isEnglish ? ('2', '@') : ('2', '"'),
            Keys.D3 => isEnglish ? ('3', '#') : ('3', '№'),
            Keys.D4 => isEnglish ? ('4', '$') : ('4', ';'),
            Keys.D5 => isEnglish ? ('5', '%') : ('5', '%'),
            Keys.D6 => isEnglish ? ('6', '^') : ('6', ':'),
            Keys.D7 => isEnglish ? ('7', '&') : ('7', '?'),
            Keys.D8 => isEnglish ? ('8', '*') : ('8', '*'),
            Keys.D9 => isEnglish ? ('9', '(') : ('9', '('),
            Keys.D0 => isEnglish ? ('0', ')') : ('0', ')'),
            Keys.OemMinus => isEnglish ? ('-', '_') : ('-', '_'),
            Keys.Oemplus => isEnglish ? ('=', '+') : ('=', '+'),

            Keys.Q => isEnglish ? ('q', 'Q') : ('й', 'Й'),
            Keys.W => isEnglish ? ('w', 'W') : ('ц', 'Ц'),
            Keys.E => isEnglish ? ('e', 'E') : ('у', 'У'),
            Keys.R => isEnglish ? ('r', 'R') : ('к', 'К'),
            Keys.T => isEnglish ? ('t', 'T') : ('е', 'Е'),
            Keys.Y => isEnglish ? ('y', 'Y') : ('н', 'Н'),
            Keys.U => isEnglish ? ('u', 'U') : ('г', 'Г'),
            Keys.I => isEnglish ? ('i', 'I') : ('ш', 'Ш'),
            Keys.O => isEnglish ? ('o', 'O') : ('щ', 'Щ'),
            Keys.P => isEnglish ? ('p', 'P') : ('з', 'З'),
            Keys.OemOpenBrackets => isEnglish ? ('[', '{') : ('х', 'Х'),
            Keys.Oem6 => isEnglish ? (']', '}') : ('ъ', 'Ъ'),
            Keys.Oem5 =>  isEnglish ? ('\\', '|') : ('\\', '/'),

            Keys.A => isEnglish ? ('a', 'A') : ('ф', 'Ф'),
            Keys.S => isEnglish ? ('s', 'S') : ('ы', 'Ы'),
            Keys.D => isEnglish ? ('d', 'D') : ('в', 'В'),
            Keys.F => isEnglish ? ('f', 'F') : ('а', 'А'), 
            Keys.G => isEnglish ? ('g', 'G') : ('п', 'П'),
            Keys.H => isEnglish ? ('h', 'H') : ('р', 'Р'),
            Keys.J => isEnglish ? ('j', 'J') : ('о', 'О'),
            Keys.K => isEnglish ? ('k', 'K') : ('л', 'Л'),
            Keys.L => isEnglish ? ('l', 'L') : ('д', 'Д'),
            Keys.Oem1 => isEnglish ? (';', ':') : ('ж', 'Ж'),
            Keys.Oem7 => isEnglish ? ('\'', '"') : ('э', 'Э'),

            Keys.Z => isEnglish ? ('z', 'Z') : ('я', 'Я'),
            Keys.X => isEnglish ? ('x', 'X') : ('ч', 'Ч'),
            Keys.C => isEnglish ? ('c', 'C') : ('с', 'С'),
            Keys.V => isEnglish ? ('v', 'V') : ('м', 'М'),
            Keys.B => isEnglish ? ('b', 'B') : ('и', 'И'),
            Keys.N => isEnglish ? ('n', 'N') : ('т', 'Т'),
            Keys.M => isEnglish ? ('m', 'M') : ('ь', 'Ь'),
            Keys.Oemcomma => isEnglish ? (',', '<') : ('б', 'Б'),
            Keys.OemPeriod => isEnglish ? ('.', '>') : ('ю', 'Ю'),
            Keys.OemQuestion => isEnglish ? ('/', '?') : ('.', ','), 

            _ => (null, null)
        };
    }
}