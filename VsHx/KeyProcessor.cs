using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using System;
using System.Collections.Generic;
using System.Windows.Input;
using VsHx;

namespace VsHx
{
    internal sealed class HxKeyProcessor : KeyProcessor
    {
        private readonly IWpfTextView _view;
        private readonly ITextStructureNavigator _navigator;

        private readonly List<Key> SimpleMotionsKeys = new List<Key>()
        {
            Key.K, Key.I, Key.J, Key.L,
        };

        private readonly List<Key> ComplexMotionsKeys = new List<Key>()
        {
            Key.W, Key.B, Key.E, Key.X
        };

        private readonly List<Key> InsertKeys = new List<Key>()
        {
            Key.C, Key.O
        };

        private readonly List<Key> ManipulationKeys = new List<Key>()
        {
            Key.D,
        };

        private readonly List<Key> ActionKeys = new List<Key>()
        {
            Key.G,
        };


        public HxKeyProcessor(IWpfTextView view, ITextStructureNavigator navigator)
        {
            System.Diagnostics.Debug.WriteLine("HxKeyProcessor attached");

            _view = view;
            _navigator = navigator;
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

            bool isShift = Keyboard.Modifiers == ModifierKeys.Shift;
            bool isAlt = Keyboard.Modifiers == ModifierKeys.Alt;

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

            // Insert Keys ------------------------------------------------------
            if (InsertKeys.Contains(e.Key))
            {
                switch (e.Key)
                {
                    case Key.C: Change(); break;
                    case Key.O: Open(!isShift); break;
                }

                e.Handled = true;
                return;
            }

            // Manipulation Keys ------------------------------------------------------
            if (ManipulationKeys.Contains(e.Key))
            {
                int num = GetNumInput();

                switch (e.Key)
                {
                    case Key.D: DeleteCurrentLine(num * (isShift ? -1 : 1)); break;
                }

                ResetNumInput();
                SetActionKey(null);
                e.Handled = true;
                return;
            }

            // Complex Motion Keys ------------------------------------------------------
            if (ComplexMotionsKeys.Contains(e.Key))
            {
                SetSelectionMode(isShift);
                int num = GetNumInput();

                switch (e.Key)
                {
                    case Key.W: MoveWordForward(num); break;
                    case Key.B: MoveWordBackward(num); break;
                    case Key.E: MoveWordEnd(num); break;
                    case Key.X: SelectCurrentLine(num * (isShift ? -1 : 1)); break;
                }

                ResetNumInput();
                SetActionKey(null);
                e.Handled = true;
                return;
            }

            // Simple Motion Keys ------------------------------------------------------
            if (SimpleMotionsKeys.Contains(e.Key))
            {
                SetSelectionMode(isShift);

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
                    case Key.W: MoveWordForward(num); break;
                    case Key.B: MoveWordBackward(num); break;
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

        private void Open(bool below)
        {
            var caret = _view.Caret;
            var buffer = _view.TextBuffer;

            var line = caret.Position.BufferPosition.GetContainingLine();
            var insertPos = below ? line.EndIncludingLineBreak : line.Start;

            using (var edit = buffer.CreateEdit())
            {
                edit.Insert(insertPos, Environment.NewLine);
                edit.Apply();
            }

            _view.Caret.MoveTo(insertPos);

            ToggleHxMode();
        }

        private void Change()
        {
            var sel = _view.Selection;
            var buffer = _view.TextBuffer;

            if (sel.IsEmpty) return;

            using (var edit = buffer.CreateEdit())
            {
                foreach (var span in sel.SelectedSpans) edit.Delete(span);
                edit.Apply();
            }

            var start = sel.Start.Position;
            sel.Clear();
            _view.Caret.MoveTo(start);

            ToggleHxMode();
        }

        private void DeleteCurrentLine(int count)
        {
            var sel = _view.Selection;
            var buffer = _view.TextBuffer;
            var caret = _view.Caret;

            if (!sel.IsEmpty)
            {
                using (var edit = buffer.CreateEdit())
                {
                    foreach (var span in sel.SelectedSpans) edit.Delete(span);
                    edit.Apply();
                }

                var pos = sel.Start.Position;
                sel.Clear();
                caret.MoveTo(pos);
                return;
            }

            var snapshot = buffer.CurrentSnapshot;
            var posPoint = caret.Position.BufferPosition;

            if (posPoint.Position >= snapshot.Length) return;

            using (var edit = buffer.CreateEdit())
            {
                edit.Delete(new SnapshotSpan(posPoint, 1));
                edit.Apply();
            }

            caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, posPoint.Position));
        }

        private void SelectCurrentLine(int count)
        {
            var snapshot = _view.TextSnapshot;
            var caret = _view.Caret;
            var sel = _view.Selection;

            var line = caret.Position.BufferPosition.GetContainingLine();
            int startLine = line.LineNumber;
            int endLine = Clamp(startLine + count - 1, 0, snapshot.LineCount - 1);

            var start = snapshot.GetLineFromLineNumber(startLine).Start;
            var end = snapshot.GetLineFromLineNumber(endLine).EndIncludingLineBreak;

            HxState.SelectionMode = true;

            if (sel.IsEmpty)
            {
                sel.Select(new VirtualSnapshotPoint(start), new VirtualSnapshotPoint(end));
            }
            else
            {
                sel.Select(sel.AnchorPoint, new VirtualSnapshotPoint(end));
            }

            MoveCaret(end);
        }

        private void MoveWordEnd(int count)
        {
            var caret = _view.Caret;
            var snapshot = caret.Position.BufferPosition.Snapshot;
            var pos = caret.Position.BufferPosition;

            for (int i = 0; i < count; i++)
            {
                while (pos.Position < snapshot.Length && char.IsWhiteSpace(snapshot[pos]))
                {
                    pos = pos + 1;
                }

                if (pos.Position >= snapshot.Length) break;

                var extent = _navigator.GetExtentOfWord(pos);
                if (!extent.IsSignificant) break;

                pos = extent.Span.End;
            }

            MoveCaret(pos);
        }


        private void MoveWordForward(int count)
        {
            var caret = _view.Caret;
            var snapshot = caret.Position.BufferPosition.Snapshot;
            var pos = caret.Position.BufferPosition;

            for (int i = 0; i < count; i++)
            {
                var extent = _navigator.GetExtentOfWord(pos);

                if (extent.IsSignificant && extent.Span.Contains(pos)) pos = extent.Span.End;
                else pos = pos + 1;

                while (pos.Position < snapshot.Length && char.IsWhiteSpace(snapshot[pos]))
                {
                    pos = pos + 1;
                }
            }

            MoveCaret(pos);
        }

        private void MoveWordBackward(int count)
        {
            var caret = _view.Caret;
            var snapshot = caret.Position.BufferPosition.Snapshot;
            var pos = caret.Position.BufferPosition;

            for (int i = 0; i < count; i++)
            {
                if (pos.Position > 0) pos = pos - 1;

                while (pos.Position > 0 && char.IsWhiteSpace(snapshot[pos]))
                {
                    pos = pos - 1;
                }

                var extent = _navigator.GetExtentOfWord(pos);

                if (extent.IsSignificant)
                {
                    pos = extent.Span.Start;
                }
            }

            MoveCaret(pos);
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
            else
            {
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
            int newColumn = Clamp(column + delta, 0, line.Length);

            var newPos = line.Start + newColumn;
            MoveCaret(new SnapshotPoint(snapshot, newPos.Position));
        }

        private void MoveVertical(int delta)
        {
            var caret = _view.Caret;
            var line = caret.Position.BufferPosition.GetContainingLine();
            var targetLine = Clamp(line.LineNumber + delta, 0, _view.TextSnapshot.LineCount - 1);

            var column = caret.Position.BufferPosition.Position - line.Start.Position;
            var newLine = _view.TextSnapshot.GetLineFromLineNumber(targetLine);
            var pos = newLine.Start + System.Math.Min(column, newLine.Length);

            MoveCaret(pos);
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

        private int Clamp(int v, int min, int max) => Math.Min(Math.Max(min, v), max);
    }
}
