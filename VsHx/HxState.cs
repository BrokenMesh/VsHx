using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace VsHx
{
    internal static class HxState
    {
        public static bool Enabled;

        public static Mode HxMode = Mode.Normal;
        public static int? StoredNum = null;
        public static string StoredStr = null;

        public static string NumInput = null;
        public static string ActionKey = null;
        public static bool SelectionMode = false;

        public static string RegContentStr = null;

        public static bool FSIsTill = false;
        public static bool FSIsBackward = false;
        public static bool FSSelect = false;

        public static bool MTSIsTill = false;
        public static bool MTSIsBackward = false;
        public static bool MTSSelect = false;

        public static bool SOIsOutside = false;
        public static bool SOIsBackward = false;
        public static bool SOIsWrap = false;

        public static Dictionary<string, string> Registers = new Dictionary<string, string>();

        public static IWpfTextView View;

        public static event Action OnStateChanged;

        public static void StateHasChanged() => OnStateChanged.Invoke();

        public static void Reset() {
            HxMode = Mode.Normal;
            StoredNum = null;
            StoredStr = null;

            NumInput = null;
            ActionKey = null;
            SelectionMode = false;

            RegContentStr = null;

            FSIsTill = false;
            FSIsBackward = false;
            FSSelect = false;

            MTSIsTill = false;
            MTSIsBackward = false;
            MTSSelect = false;

            SOIsOutside = false;
            SOIsBackward = false;
            SOIsWrap = false;
        }

        public enum Mode
        {
            Normal, Register, FindSymbol, MoveToSymbol, GoOverSymbol, Split, Surround
        }
    }


}
