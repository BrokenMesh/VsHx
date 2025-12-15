using Microsoft.VisualStudio.Shell.Interop;
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

        public static string NumInput = null;
        public static string ActionKey = null;
        public static bool SelectionMode = false;

        public static event Action OnStateChanged;

        public static void StateHasChanged() => OnStateChanged.Invoke();

        public static void Reset()
        {
            NumInput = null;
            ActionKey = null;
            SelectionMode = false;
        }
    }
}
