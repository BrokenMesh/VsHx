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

        public static string NumInput = null;
        public static string ActionKey = null;
        public static bool SelectionMode = false;

        public static string RegistersStr = null;
        public static string RegContentStr = null;
        public static int? RegContentNum = null;

        public static Dictionary<string, string> Registers = new Dictionary<string, string>();

        public static IWpfTextView View;

        public static event Action OnStateChanged;

        public static void StateHasChanged() => OnStateChanged.Invoke();

        public static void Reset() {
            HxMode = Mode.Normal;

            NumInput = null;
            ActionKey = null;
            SelectionMode = false;

            RegistersStr = null;
            RegContentStr = null;
            RegContentNum = null;
        }

        public enum Mode
        {
            Normal, Register
        }
    }


}
