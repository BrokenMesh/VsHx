using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Windows.Input;
using VsHx;

namespace VsHx
{
    internal sealed class HxKeyProcessor : KeyProcessor
    {
        private readonly IWpfTextView _view;

        private readonly List<Key> MotionsKeys = new List<Key>()
        {
            Key.K, Key.I, Key.J, Key.L,
        };

        private readonly List<Key> ActionKeys = new List<Key>()
        {
            Key.G
        };


        public HxKeyProcessor(IWpfTextView view)
        {
            System.Diagnostics.Debug.WriteLine("HxKeyProcessor attached");

            _view = view;
        }

        public override void KeyDown(KeyEventArgs e)
        {
            if (e.IsRepeat) return;

            // Hx Mode Toggle ------------------------------------------------------
            if (e.Key == Key.H && Keyboard.Modifiers == ModifierKeys.Control)
            {
                ToggleHxMode();
                e.Handled = true;
                return;
            }

            if (!HxState.Enabled) return;

            if (e.Key == Key.H)
            {
                ToggleHxMode();
                e.Handled = true;
                return;
            }

            // Escape -------------------------------------------------------
            if (e.Key == Key.Escape)
            {
                HxState.Reset();
                HxState.StateHasChanged();
                return;
            }

            // Num Keys ------------------------------------------------------
            int numVal = (int)e.Key - 34;
            if (numVal >= 0 && numVal <= 9)
            {
                AppendNumInput(numVal);
                return;
            }

            // Action Keys ----------------------------------------------------
            if (ActionKeys.Contains(e.Key)) 
            { 
                SetActionKey(e.Key);
                return;
            }

            // Simple Motion Keys ------------------------------------------------------
            if (MotionsKeys.Contains(e.Key))
            {
                SetSelectionMode(Keyboard.Modifiers == ModifierKeys.Shift);

                int num = GetNumInput();

                if (HxState.ActionKey == "G")
                {
                    SetActionKey(null);
                    num = 9_999_999;
                }

                switch (e.Key)
                {
                    case Key.K: MoveVertical(1 * num); break;
                    case Key.I: MoveVertical(-1 * num); break;
                    case Key.J: MoveHorizontal(-1 * num); break;
                    case Key.L: MoveHorizontal(1 * num); break;
                }

                ResetNumInput();
                e.Handled = true;
                return;
            }
        }

        private void ResetNumInput()
        {
            HxState.NumInput = null;
            HxState.StateHasChanged();
        }

        private void SetActionKey(Key? a)
        {
            HxState.ActionKey = a?.ToString();
            HxState.StateHasChanged();
        }

        private void SetSelectionMode(bool newMode)
        {
            if (HxState.SelectionMode == newMode) return;
            HxState.SelectionMode = newMode;
            HxState.StateHasChanged();
        }

        private int GetNumInput()
        {
            if (HxState.NumInput == null) return 1;
            return int.Parse(HxState.NumInput);
        }

        private void AppendNumInput(int num)
        {
            HxState.NumInput = (HxState.NumInput ?? "") + num;
            HxState.StateHasChanged();
        }

        private void ToggleHxMode()
        {
            HxState.Enabled = !HxState.Enabled;
            HxState.Reset();
            HxState.StateHasChanged();
        }

        public override void TextInput(TextCompositionEventArgs e)
        {
            if (!HxState.Enabled)
                return;

            e.Handled = true;
        }

        public override void PreviewTextInput(TextCompositionEventArgs e)
        {
            if (!HxState.Enabled)
                return;

            e.Handled = true;
        }

        private void MoveCaret(SnapshotPoint target)
        {
            var caret = _view.Caret;
            var sel = _view.Selection;

            if (HxState.SelectionMode)
            {
                var targetV = new VirtualSnapshotPoint(target);
                var caretV = caret.Position.VirtualBufferPosition;

                if (sel.IsEmpty)
                {
                    sel.Select(caretV, targetV);
                }
                else
                {
                    sel.Select(sel.AnchorPoint, targetV);
                }
            }
            else {
                sel.Clear();
            }

            caret.MoveTo(target);
        }

        private void MoveHorizontal(int delta)
        {
            var caret = _view.Caret;
            var snapshot = caret.Position.BufferPosition.Snapshot;

            var pos = caret.Position.BufferPosition;
            var line = pos.GetContainingLine();

            int column = pos.Position - line.Start.Position;
            int newColumn = column + delta;

            if (newColumn < 0) newColumn = 0;
            else if (newColumn > line.Length) newColumn = line.Length;

            var newPos = line.Start + newColumn;
            MoveCaret(new SnapshotPoint(snapshot, newPos.Position));
        }

        private void MoveVertical(int delta)
        {
            var caret = _view.Caret;
            var line = caret.Position.BufferPosition.GetContainingLine();
            var targetLine = line.LineNumber + delta;

            if (targetLine < 0) targetLine = 0;
            else if (targetLine >= _view.TextSnapshot.LineCount) targetLine = _view.TextSnapshot.LineCount - 1;

            var column = caret.Position.BufferPosition.Position - line.Start.Position;
            var newLine = _view.TextSnapshot.GetLineFromLineNumber(targetLine);
            var pos = newLine.Start + System.Math.Min(column, newLine.Length);

            MoveCaret(pos);
        }
    }
}
