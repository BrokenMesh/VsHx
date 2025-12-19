using Microsoft.VisualStudio.Text.Editor;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static System.Net.Mime.MediaTypeNames;

namespace VsHx
{
    internal sealed class HxModeMargin : IWpfTextViewMargin
    {
        public const string MarginName = "HxModeMargin";

        private readonly TextBlock _text;
        private readonly Border _root;

        public FrameworkElement VisualElement => _root;
        public double MarginSize => _root.ActualWidth;
        public bool Enabled => true;

        public HxModeMargin(IWpfTextView view) {
            _text = new TextBlock {
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Gray,
                Text = HxState.Enabled ? "HX" : "VS",
                Margin = new Thickness(6, 3, 6, 3),
                HorizontalAlignment = HorizontalAlignment.Right
            };

            _root = new Border {
                Child = _text
            };

            HxState.OnStateChanged += OnModeChanged;
        }

        private void OnModeChanged() {
            List<string> output = new List<string>();

            switch (HxState.HxMode) {
                case HxState.Mode.Normal:
                    if (HxState.SelectionMode) output.Add("SEL");
                    if (HxState.NumInput != null) output.Add(HxState.NumInput);
                    if (HxState.ActionKey != null) output.Add(HxState.ActionKey);
                    break;
                case HxState.Mode.Register:
                    output.Add("REG");
                    if (HxState.StoredStr != null) output.Add(HxState.StoredStr);
                    break;
                case HxState.Mode.MoveToSymbol:
                    output.Add("MTS" + (HxState.MTSSelect ? "&SEL" : ""));
                    break;
                case HxState.Mode.FindSymbol:
                case HxState.Mode.GoOverSymbol:
                    output.Add("FND" + (HxState.FSSelect ? "&SEL" : ""));
                    if (HxState.StoredStr != null) output.Add(HxState.StoredStr);
                    break;
                case HxState.Mode.Split:
                    if (HxState.SelectionMode) output.Add("SPT");
                    break;
                case HxState.Mode.Surround:
                    output.Add("SUR");
                    if (HxState.SOIsWrap) output.Add("W");
                    else if (HxState.SOIsOutside) output.Add("O");
                    else output.Add("I");
                    break;
                default:
                    break;
            }

            output.Add(HxState.Enabled ? "HX" : "VS");
            _text.Text = string.Join(" - ", output);
        }

        public void Dispose() {
            HxState.OnStateChanged -= OnModeChanged;
        }

        public ITextViewMargin GetTextViewMargin(string marginName)
            => marginName == MarginName ? this : null;
    }
}
