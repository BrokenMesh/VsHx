using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using VsHx;
using static System.Net.Mime.MediaTypeNames;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

namespace VsHx
{
    internal sealed class HxKeyProcessor : KeyProcessor
    {
        private readonly IWpfTextView _view;
        private readonly ITextStructureNavigator _navigator;
        private readonly ITextUndoHistory _undoHistory;

        private readonly List<Key> SimpleMotionsKeys = new List<Key>()
        {
            Key.K, Key.I, Key.J, Key.L,
        };

        private readonly List<Key> ComplexMotionsKeys = new List<Key>()
        {
            Key.W, Key.B, Key.E, Key.X, Key.F, Key.T
        };

        private readonly List<Key> InsertKeys = new List<Key>()
        {
            Key.R, Key.O
        };

        private readonly List<Key> SpaceManipulationKeys = new List<Key>()
        {
            Key.U, Key.J, Key.S
        };

        private readonly List<Key> SManipulationKeys = new List<Key>()
        {
            Key.O, Key.I, Key.W
        };

        private readonly List<Key> ManipulationKeys = new List<Key>()
        {
            Key.D, Key.U, Key.P, Key.C
        };

        private readonly List<Key> ActionKeys = new List<Key>()
        {
            Key.G, Key.V, Key.B, Key.M, Key.S, Key.Space
        };


        public HxKeyProcessor(IWpfTextView view, ITextStructureNavigator navigator, ITextUndoHistory undoHistory) {
            System.Diagnostics.Debug.WriteLine("HxKeyProcessor attached");

            _view = view;
            _navigator = navigator;
            _undoHistory = undoHistory;

            HxState.View = view;
        }




        public override void KeyDown(KeyEventArgs e) {
            if (e.Key == Key.Q) {
                HxState.Reset();
                HxState.StateHasChanged();
                return;
            }

            switch (HxState.HxMode) {
                case HxState.Mode.Normal: Normal(e); break;
            }
        }

        public override void PreviewTextInput(TextCompositionEventArgs e) {
            if (!HxState.Enabled)
                return;

            switch (HxState.HxMode) {
                case HxState.Mode.MoveToSymbol:
                case HxState.Mode.Register: TextEnter(e); break;
                case HxState.Mode.Split:
                case HxState.Mode.Surround: CharEnter(e); break;
            }

            e.Handled = true;
        }



        private void Normal(KeyEventArgs e) {

            // Hx Mode Toggle ------------------------------------------------------
            if (e.Key == Key.H && Keyboard.Modifiers == ModifierKeys.Control) {
                ToggleHxMode();
                e.Handled = true;
                return;
            }

            if (!HxState.Enabled) return;

            e.Handled = true;

            if (e.Key == Key.H) {
                ToggleHxMode();
                return;
            }

            bool isShift = Keyboard.Modifiers == ModifierKeys.Shift;
            bool isCtrl = Keyboard.Modifiers == ModifierKeys.Control;

            // Num Keys ------------------------------------------------------
            int numVal = (int)e.Key - 34;
            if (numVal >= 0 && numVal <= 9) {
                AppendNumInput(numVal);
                return;
            }

            // Space Manipulation Keys ------------------------------------------------------

            if (HxState.ActionKey == "Space" && SpaceManipulationKeys.Contains(e.Key)) {
                int num = GetNumInput();

                switch (e.Key) {
                    case Key.U: ChangeCase(isShift); break;
                    case Key.J: JoinLines(); break;
                    case Key.S: Split(); break;
                }

                return;
            }

            // S Manipulation Keys ------------------------------------------------------

            if (HxState.ActionKey == "S" && SManipulationKeys.Contains(e.Key)) {
                int num = GetNumInput();

                switch (e.Key) {
                    case Key.O: Surround(true, false, isShift); break;
                    case Key.I: Surround(false, false, isShift); break;
                    case Key.W: Surround(false, true, false); break;
                }

                return;
            }

            // Action Keys ----------------------------------------------------
            if (ActionKeys.Contains(e.Key)) {
                if (e.Key == Key.M && !HxState.SelectionMode) return;

                SetActionKey(e.Key);
                return;
            }


            // Insert Keys ------------------------------------------------------
            if (InsertKeys.Contains(e.Key)) {
                int num = GetNumInput();

                switch (e.Key) {
                    case Key.R: Replace(); break;
                    case Key.O: Open(num, !isShift); break;
                }

                return;
            }

            // Manipulation Keys ------------------------------------------------------
            if (ManipulationKeys.Contains(e.Key)) {
                int num = GetNumInput();

                switch (e.Key) {
                    case Key.D: Delete(num); break;
                    case Key.U: Undo(num, isShift); break;
                    case Key.C: Copy(isShift); break;
                    case Key.P: Paste(num, isShift); break;
                }

                ResetNumInput();
                SetActionKey(null);
                return;
            }

            // Complex Motion Keys ------------------------------------------------------
            if (ComplexMotionsKeys.Contains(e.Key)) {
                SetSelectionMode(isShift);
                int num = GetNumInput();

                switch (e.Key) {
                    case Key.W: MoveWordForward(num); break;
                    case Key.B: MoveWordBackward(num); break;
                    case Key.E: MoveWordEnd(num); break;
                    case Key.X: SelectCurrentLine(num * (isShift ? -1 : 1)); break;
                    case Key.F: MoveToSymbol(num, isShift, false); break;
                    case Key.T: MoveToSymbol(num, isShift, true); break;
                }

                ResetNumInput();
                SetActionKey(null);
                return;
            }

            // Simple Motion Keys ------------------------------------------------------
            if (SimpleMotionsKeys.Contains(e.Key)) {
                bool move = HxState.ActionKey == "M";
                SetSelectionMode(isShift || move);

                int num = GetNumInput();

                bool snap = HxState.ActionKey == "G";
                if (snap) {
                    SetActionKey(null);
                    num = 9_999_999;
                }

                if (HxState.ActionKey == "V") {
                    int page = _view.TextViewLines.Count;
                    SetActionKey(null);
                    num = page;
                }

                switch (e.Key) {
                    case Key.K: MoveVertical(1 * num, move); break;
                    case Key.I: MoveVertical(-1 * num, move); break;
                    case Key.J: MoveHorizontal(-1 * num, !snap); break;
                    case Key.L: MoveHorizontal(1 * num, !snap); break;
                    case Key.W: MoveWordForward(num); break;
                    case Key.B: MoveWordBackward(num); break;
                }

                ResetNumInput();
                return;
            }
        }

        private void TextEnter(TextCompositionEventArgs e) {
            e.Handled = true;

            HxState.StoredStr += e.Text;
            HxState.StateHasChanged();
        }

        private void CharEnter(TextCompositionEventArgs e) {
            e.Handled = true;

            switch (HxState.HxMode) {
                case HxState.Mode.Split: SplitSelection(e.Text); break;
                case HxState.Mode.Surround: OnSurround(e.Text); break;
            }

            HxState.Reset();
        }


        private void ResetNumInput() {
            HxState.NumInput = null;
            HxState.StateHasChanged();
        }

        private void SetActionKey(Key? a) {
            HxState.ActionKey = a?.ToString();
            HxState.StateHasChanged();
        }

        private void SetSelectionMode(bool newMode) {
            if (HxState.SelectionMode == newMode) return;
            HxState.SelectionMode = newMode;
            HxState.StateHasChanged();
        }

        private int GetNumInput() {
            if (HxState.NumInput == null) return 1;
            return int.Parse(HxState.NumInput);
        }

        private void AppendNumInput(int num) {
            HxState.NumInput = (HxState.NumInput ?? "") + num;
            HxState.StateHasChanged();
        }

        private void ToggleHxMode() {
            HxState.Enabled = !HxState.Enabled;
            HxState.Reset();
            HxState.StateHasChanged();
        }

        private void Copy(bool reg) {
            if (!HxState.SelectionMode) return;

            string content = TakeSelection();

            if (reg) {
                HxState.RegContentStr = content;
                HxState.HxMode = HxState.Mode.Register;
            }
            else {
                Clipboard.SetText(content);
            }
        }

        private void Paste(int num, bool reg) {
            if (reg) {
                HxState.HxMode = HxState.Mode.Register;
                HxState.StoredNum = num;
                return;
            }

            if (num <= 0) num = 1;

            var text = Clipboard.GetText();
            if (string.IsNullOrEmpty(text)) return;

            var view = HxState.View;
            if (view == null) return;

            var selection = view.Selection;
            var buffer = view.TextBuffer;

            int insertPos = selection.IsEmpty
                ? view.Caret.Position.BufferPosition.Position
                : selection.Start.Position;

            string payload = string.Concat(Enumerable.Repeat(text, num));

            using (var edit = buffer.CreateEdit()) {
                if (!selection.IsEmpty) {
                    foreach (var span in selection.SelectedSpans)
                        edit.Delete(span);
                }

                edit.Insert(insertPos, payload);
                edit.Apply();
            }

            selection.Clear();

            var snapshot = buffer.CurrentSnapshot;
            view.Caret.MoveTo(new SnapshotPoint(snapshot, insertPos + payload.Length));
            view.Caret.EnsureVisible();
        }

        private void Open(int num, bool below) {
            var caret = _view.Caret;
            var buffer = _view.TextBuffer;

            var snapshot = buffer.CurrentSnapshot;
            var line = caret.Position.BufferPosition.GetContainingLine();

            int insertPos = below
                ? line.EndIncludingLineBreak.Position
                : line.Start.Position;

            string text = string.Concat(Enumerable.Repeat(Environment.NewLine, num));

            using (var edit = buffer.CreateEdit()) {
                edit.Insert(insertPos, text);
                edit.Apply();
            }

            var newSnapshot = buffer.CurrentSnapshot;

            int caretPos = below
                ? insertPos
                : insertPos;

            caret.MoveTo(new SnapshotPoint(newSnapshot, caretPos));

            caret.EnsureVisible();

            ToggleHxMode();
        }

        private void Replace() {
            var view = _view;
            var buffer = view.TextBuffer;
            var sel = view.Selection;

            if (sel.IsEmpty) return;

            var snapshot = buffer.CurrentSnapshot;

            var anchors = sel.SelectedSpans
                .Select(s => {
                    var line = s.Start.GetContainingLine();
                    return new {
                        LineNumber = line.LineNumber,
                        Column = s.Start.Position - line.Start.Position
                    };
                })
                .OrderByDescending(a => a.LineNumber)
                .ThenByDescending(a => a.Column)
                .ToArray();

            var spans = sel.SelectedSpans
                .Select(s => new SnapshotSpan(snapshot, s))
                .OrderByDescending(s => s.Start.Position)
                .ToArray();

            using (var edit = buffer.CreateEdit()) {
                foreach (var span in spans)
                    edit.Delete(span);
                edit.Apply();
            }

            sel.Clear();

            var newSnapshot = buffer.CurrentSnapshot;
            var broker = view.GetMultiSelectionBroker();

            foreach (var a in anchors) {
                if (a.LineNumber >= newSnapshot.LineCount) continue;

                var line = newSnapshot.GetLineFromLineNumber(a.LineNumber);
                var pos = line.Start.Position + Math.Min(a.Column, line.Length);

                var point = new SnapshotPoint(newSnapshot, pos);
                broker.AddSelection(new Selection(new SnapshotSpan(point, point), isReversed: false));
            }

            ToggleHxMode();
        }


        private void Delete(int num = 0) {
            var sel = _view.Selection;
            var buffer = _view.TextBuffer;
            var caret = _view.Caret;

            if (!sel.IsEmpty) {
                using (var edit = buffer.CreateEdit()) {
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

            int contentAfter = snapshot.Length - posPoint.Position;

            if (contentAfter <= 0) return;

            using (var edit = buffer.CreateEdit()) {
                edit.Delete(new SnapshotSpan(posPoint, Math.Min(num, contentAfter)));
                edit.Apply();
            }

            caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, posPoint.Position));
        }

        private void Surround(bool outside, bool isWrap, bool backwards) {
            var view = _view;
            var sel = view.Selection;

            if (sel.IsEmpty) return;

            HxState.HxMode = HxState.Mode.Surround;
            HxState.SOIsOutside = outside;
            HxState.SOIsBackward = backwards;
            HxState.SOIsWrap = isWrap;
            HxState.StateHasChanged();
        }

        Dictionary<char, char> SurroundOpPairs = new Dictionary<char, char> {
            { '(', ')' },
            { '[', ']' },
            { '{', '}' },
            { '<', '>' },
        };

        Dictionary<char, char> SurroundClPairs = new Dictionary<char, char> {
            { ')', '(' },
            { ']', '[' },
            { '}', '{' },
            { '>', '<' },
        };

        private void OnSurround(string text) {
            var view = _view;
            var sel = view.Selection;

            char op = text[0];
            if (!SurroundOpPairs.ContainsKey(op)) return;

            if (!HxState.SOIsWrap) {
                SelectSurroundChar(op);
            }
            else {
                SurroundSelection(op);
            }
        }

        private void SurroundSelection(char op) {
            var view = _view;
            var sel = view.Selection;
            var buffer = view.TextBuffer;

            if (sel.IsEmpty) return;

            char cl = SurroundOpPairs[op];

            var snapshot = buffer.CurrentSnapshot;

            var spans = sel.SelectedSpans
                .Select(s => new SnapshotSpan(snapshot, s))
                .OrderByDescending(s => s.Start.Position)
                .ToArray();

            using (var edit = buffer.CreateEdit()) {
                foreach (var span in spans) {
                    edit.Insert(span.End.Position, cl.ToString());
                    edit.Insert(span.Start.Position, op.ToString());
                }
                edit.Apply();
            }

            sel.Clear();

            var newSnapshot = buffer.CurrentSnapshot;
            var broker = view.GetMultiSelectionBroker();

            foreach (var span in spans.Reverse()) {
                int start = span.Start.Position + 1;
                int len = span.Length;
                var newSpan = new SnapshotSpan(newSnapshot, start, len);
                broker.AddSelection(new Selection(newSpan, isReversed: false));
            }
        }


        private void SelectSurroundChar(char op) {
            var view = _view;
            var sel = view.Selection;
            var caret = view.Caret;

            char cl = SurroundOpPairs[op];

            var snapshot = view.TextBuffer.CurrentSnapshot;
            if (snapshot.Length == 0) return;

            int caretPos = Clamp(caret.Position.BufferPosition.Position, 0, snapshot.Length);

            int foundFirstIndex = FindNext(snapshot, caretPos, op, cl, HxState.SOIsBackward);
            if (foundFirstIndex == -1) return;
            char foundFirst = snapshot[foundFirstIndex];

            bool IsBackward;
            char targetSecond;

            if (SurroundOpPairs.ContainsKey(foundFirst)) {
                IsBackward = false;
                targetSecond = SurroundOpPairs[foundFirst];
            }
            else {
                IsBackward = true;
                targetSecond = SurroundClPairs[foundFirst];
            }

            int foundSecondIndex = FindNext(snapshot, foundFirstIndex, targetSecond, IsBackward);
            if (foundSecondIndex == -1) return;

            int start = (!IsBackward ? foundFirstIndex : foundSecondIndex) + 1;
            int end = !IsBackward ? foundSecondIndex : foundFirstIndex;

            if (HxState.SOIsOutside) {
                start -= 1;
                end += 1;
            }

            int len = foundSecondIndex - foundFirstIndex;
            var span = new SnapshotSpan(snapshot, start, end - start);

            sel.Clear();
            sel.Select(span, isReversed: false);
            caret.MoveTo(new SnapshotPoint(snapshot, end));
            caret.EnsureVisible();
        }

        int FindNext(ITextSnapshot s, int start, char op, char cl, bool backward) {
            if (backward) {
                for (int i = start; i >= 0; i--) {
                    if (s[i] == op || s[i] == cl) return i;
                }
            }
            else {
                for (int i = start; i < s.Length; i++) {
                    if (s[i] == op || s[i] == cl) return i;
                }
            }

            return -1;
        }

        int FindNext(ITextSnapshot s, int start, char c, bool backward) {
            if (backward) {
                for (int i = start; i >= 0; i--) {
                    if (s[i] == c) return i;
                }
            }
            else {
                for (int i = start; i < s.Length; i++) {
                    if (s[i] == c) return i;
                }
            }

            return -1;
        }

        private void Split() {
            HxState.HxMode = HxState.Mode.Split;
            HxState.StateHasChanged();
        }

        private void SplitSelection(string c) {
            var view = _view;
            var buffer = view.TextBuffer;
            var sel = view.Selection;
            var caret = view.Caret;

            if (sel.IsEmpty) return;
            if (string.IsNullOrEmpty(c)) return;

            var snapshot = buffer.CurrentSnapshot;

            var spans = sel.SelectedSpans
                .Select(s => new SnapshotSpan(snapshot, s))
                .OrderByDescending(s => s.Start.Position)
                .ToArray();

            using (var edit = buffer.CreateEdit()) {
                foreach (var span in spans) {
                    var text = span.GetText();
                    var replaced = text.Replace(c, c + Environment.NewLine);
                    edit.Replace(span, replaced);
                }

                edit.Apply();
            }

            sel.Clear();

            var newSnapshot = buffer.CurrentSnapshot;
            caret.MoveTo(new SnapshotPoint(newSnapshot, spans.Last().Start.Position));
            caret.EnsureVisible();
        }

        private void JoinLines() {
            var view = _view;
            var buffer = view.TextBuffer;
            var sel = view.Selection;
            var caret = view.Caret;

            if (sel.IsEmpty) return;

            var snapshot = buffer.CurrentSnapshot;
            var spans = sel.SelectedSpans;

            int firstLine = spans.Min(s => s.Start.GetContainingLine().LineNumber);
            int lastLine = spans.Max(s => {
                if (s.End.Position == 0)
                    return s.End.GetContainingLine().LineNumber;

                var endPoint = new SnapshotPoint(snapshot, s.End.Position - 1);
                return endPoint.GetContainingLine().LineNumber;
            });

            if (firstLine == lastLine) return;

            var lines = new List<ITextSnapshotLine>();
            for (int i = firstLine; i <= lastLine; i++)
                lines.Add(snapshot.GetLineFromLineNumber(i));

            var joined = string.Join(" ", lines.Select(l => l.GetText().Trim()));

            int replaceStart = lines[0].Start.Position;
            int replaceEnd = lines[lines.Count - 1].EndIncludingLineBreak.Position;
            int replaceLen = replaceEnd - replaceStart;

            using (var edit = buffer.CreateEdit()) {
                edit.Replace(replaceStart, replaceLen, joined);
                edit.Apply();
            }

            sel.Clear();

            var newSnapshot = buffer.CurrentSnapshot;
            caret.MoveTo(new SnapshotPoint(newSnapshot, replaceStart + joined.Length));
            caret.EnsureVisible();
        }

        private void ChangeCase(bool upper) {
            var view = _view;
            var buffer = view.TextBuffer;
            var sel = view.Selection;
            var caret = view.Caret;

            if (sel.IsEmpty) {
                var pos = caret.Position.BufferPosition;
                if (pos.Position >= buffer.CurrentSnapshot.Length) return;

                var ch = buffer.CurrentSnapshot[pos];
                var repl = upper ? char.ToUpperInvariant(ch) : char.ToLowerInvariant(ch);

                if (ch == repl) return;

                using (var edit = buffer.CreateEdit()) {
                    edit.Replace(pos.Position, 1, repl.ToString());
                    edit.Apply();
                }

                caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, pos.Position + 1));
                return;
            }

            var snapshot = buffer.CurrentSnapshot;

            var spans = sel.SelectedSpans
                .Select(s => new SnapshotSpan(snapshot, s))
                .OrderByDescending(s => s.Start.Position)
                .ToArray();

            using (var edit = buffer.CreateEdit()) {
                foreach (var span in spans) {
                    var text = span.GetText();
                    var changed = upper ? text.ToUpperInvariant() : text.ToLowerInvariant();
                    edit.Replace(span, changed);
                }
                edit.Apply();
            }
        }

        private void Undo(int num, bool redo) {
            if (_undoHistory == null) return;
            if (!_undoHistory.CanUndo) return;

            if (!redo) {
                _undoHistory.Undo(num);
            }
            else {
                _undoHistory.Redo(num);
            }
        }

        private string TakeSelection() {
            var sel = _view.Selection;
            if (sel.IsEmpty) return string.Empty;

            var snapshot = sel.SelectedSpans[0].Snapshot;
            return string.Concat(sel.SelectedSpans.Select(s => s.GetText()));
        }

        private void ReplaceSelection(string text) {
            Delete();

            var caret = _view.Caret.Position.BufferPosition;
            using (var edit = _view.TextBuffer.CreateEdit()) {
                edit.Insert(caret, text);
                edit.Apply();
            }
        }

        private void SelectCurrentLine(int count) {
            var snapshot = _view.TextSnapshot;
            var caret = _view.Caret;
            var sel = _view.Selection;

            var line = caret.Position.BufferPosition.GetContainingLine();
            int startLine = line.LineNumber;
            int endLine = Clamp(startLine + count - 1, 0, snapshot.LineCount - 1);

            var start = snapshot.GetLineFromLineNumber(startLine).Start;
            var end = snapshot.GetLineFromLineNumber(endLine).EndIncludingLineBreak;

            HxState.SelectionMode = true;

            if (sel.IsEmpty) {
                sel.Select(new VirtualSnapshotPoint(start), new VirtualSnapshotPoint(end));
            }
            else {
                sel.Select(sel.AnchorPoint, new VirtualSnapshotPoint(end));
            }

            MoveCaret(end);
        }

        private void MoveToSymbol(int num, bool backward, bool till) {
            string content = TakeSelection();

            HxState.HxMode = HxState.Mode.MoveToSymbol;
            HxState.StoredNum = num;
            HxState.StoredStr = content;
            HxState.MTSIsTill = till;
            HxState.MTSIsBackward = backward;
            HxState.MTSSelect = HxState.ActionKey == "S";

            HxState.StateHasChanged();
        }

        private void MoveWordEnd(int count) {
            var caret = _view.Caret;
            var snapshot = caret.Position.BufferPosition.Snapshot;
            var pos = caret.Position.BufferPosition;

            for (int i = 0; i < count; i++) {
                while (pos.Position < snapshot.Length && char.IsWhiteSpace(snapshot[pos])) {
                    pos = pos + 1;
                }

                if (pos.Position >= snapshot.Length) break;

                var extent = _navigator.GetExtentOfWord(pos);
                if (!extent.IsSignificant) break;

                pos = extent.Span.End;
            }

            MoveCaret(pos);
        }


        private void MoveWordForward(int count) {
            var caret = _view.Caret;
            var snapshot = caret.Position.BufferPosition.Snapshot;
            var pos = caret.Position.BufferPosition;

            for (int i = 0; i < count; i++) {
                var extent = _navigator.GetExtentOfWord(pos);

                if (extent.IsSignificant && extent.Span.Contains(pos)) pos = extent.Span.End;
                else pos = pos + 1;

                while (pos.Position < snapshot.Length && char.IsWhiteSpace(snapshot[pos])) {
                    pos = pos + 1;
                }
            }

            MoveCaret(pos);
        }

        private void MoveWordBackward(int count) {
            var caret = _view.Caret;
            var snapshot = caret.Position.BufferPosition.Snapshot;
            var pos = caret.Position.BufferPosition;

            for (int i = 0; i < count; i++) {
                if (pos.Position > 0) pos = pos - 1;

                while (pos.Position > 0 && char.IsWhiteSpace(snapshot[pos])) {
                    pos = pos - 1;
                }

                var extent = _navigator.GetExtentOfWord(pos);

                if (extent.IsSignificant) {
                    pos = extent.Span.Start;
                }
            }

            MoveCaret(pos);
        }

        //private void MoveCaret(SnapshotPoint target) {
        //    var caret = _view.Caret;
        //    var sel = _view.Selection;
        //
        //    if (HxState.SelectionMode) {
        //        if (HxState.ActionKey != "S") {
        //            var targetV = new VirtualSnapshotPoint(target);
        //            var caretV = caret.Position.VirtualBufferPosition;
        //
        //            if (sel.IsEmpty) {
        //                sel.Select(caretV, targetV);
        //            }
        //            else {
        //                sel.Select(sel.AnchorPoint, targetV);
        //            }
        //        }
        //        else {
        //            // Implement this this
        //        }
        //    }
        //    else {
        //        sel.Clear();
        //    }
        //
        //    caret.MoveTo(target);
        //
        //    caret.EnsureVisible();
        //}

        private void MoveCaret(SnapshotPoint target) {
            var caret = _view.Caret;
            var sel = _view.Selection;

            if (HxState.SelectionMode) {
                var targetV = new VirtualSnapshotPoint(target);

                if (HxState.ActionKey != "B") {
                    if (sel.Mode != TextSelectionMode.Stream) sel.Mode = TextSelectionMode.Stream;

                    var caretV = caret.Position.VirtualBufferPosition;

                    if (sel.IsEmpty) sel.Select(caretV, targetV);
                    else sel.Select(sel.AnchorPoint, targetV);
                }
                else {
                    if (sel.Mode != TextSelectionMode.Box) sel.Mode = TextSelectionMode.Box;

                    VirtualSnapshotPoint anchor = sel.IsEmpty ? caret.Position.VirtualBufferPosition : sel.AnchorPoint;

                    sel.Select(anchor, targetV);
                }
            }
            else {
                sel.Clear();
                if (sel.Mode != TextSelectionMode.Stream) sel.Mode = TextSelectionMode.Stream;
            }

            caret.MoveTo(target);
            caret.EnsureVisible();
        }

        private void MoveHorizontal(int delta, bool wrap) {
            var caret = _view.Caret;
            var snapshot = caret.Position.BufferPosition.Snapshot;

            var pos = caret.Position.BufferPosition;
            var line = pos.GetContainingLine();

            int column = pos.Position - line.Start.Position;
            int targetColumn = column + delta;

            if (wrap) {
                if (targetColumn < 0) {
                    if (line.LineNumber == 0) targetColumn = 0;
                    else {
                        var prevLine = snapshot.GetLineFromLineNumber(line.LineNumber - 1);
                        MoveCaret(prevLine.End);
                        return;
                    }
                }

                if (targetColumn > line.Length) {
                    if (line.LineNumber == snapshot.LineCount - 1) targetColumn = line.Length;
                    else {
                        var nextLine = snapshot.GetLineFromLineNumber(line.LineNumber + 1);
                        MoveCaret(nextLine.Start);
                        return;
                    }
                }
            }

            targetColumn = Clamp(targetColumn, 0, line.Length);
            MoveCaret(line.Start + targetColumn);
        }


        private void MoveVertical(int delta, bool move) {
            if (HxState.SelectionMode && move) {
                MoveSelection(delta);
                return;
            }

            var caret = _view.Caret;
            var line = caret.Position.BufferPosition.GetContainingLine();
            var targetLine = Clamp(line.LineNumber + delta, 0, _view.TextSnapshot.LineCount - 1);

            var column = caret.Position.BufferPosition.Position - line.Start.Position;
            var newLine = _view.TextSnapshot.GetLineFromLineNumber(targetLine);
            var pos = newLine.Start + Math.Min(column, newLine.Length);

            MoveCaret(pos);
        }

        private void MoveSelection(int delta) {
            var view = _view;
            var buffer = view.TextBuffer;
            var sel = view.Selection;
            var caret = view.Caret;

            if (sel.IsEmpty) return;

            var snapshot = buffer.CurrentSnapshot;
            var span = sel.SelectedSpans[0];

            var startLine = span.Start.GetContainingLine();

            ITextSnapshotLine endLine;
            if (span.Length == 0) {
                endLine = startLine;
            }
            else {
                var endMinus1 = new SnapshotPoint(snapshot, span.End.Position - 1);
                endLine = endMinus1.GetContainingLine();
            }

            int blockStart = startLine.Start.Position;
            int blockEnd = endLine.LineNumber == snapshot.LineCount - 1 ? snapshot.Length : endLine.EndIncludingLineBreak.Position;

            int blockLen = blockEnd - blockStart;

            int caretOffset = caret.Position.BufferPosition.Position - blockStart;
            caretOffset = Clamp(caretOffset, 0, blockLen);

            string text = snapshot.GetText(blockStart, blockLen);

            int targetLineNum = Clamp(startLine.LineNumber + delta, 0, snapshot.LineCount);

            ITextSnapshot newSnapshot;

            using (var edit = buffer.CreateEdit()) {
                edit.Delete(blockStart, blockLen);
                newSnapshot = edit.Apply();
            }

            targetLineNum = Clamp(targetLineNum, 0, newSnapshot.LineCount - 1);

            int insertPos = newSnapshot.GetLineFromLineNumber(targetLineNum).Start.Position;

            using (var edit = buffer.CreateEdit()) {
                edit.Insert(insertPos, text);
                newSnapshot = edit.Apply();
            }

            sel.Clear();

            var newStart = new VirtualSnapshotPoint(newSnapshot, insertPos);
            var newEnd = new VirtualSnapshotPoint(newSnapshot, Math.Min(insertPos + blockLen, newSnapshot.Length));

            sel.Select(newStart, newEnd);

            int newCaretPos = Math.Min(insertPos + caretOffset, newSnapshot.Length);
            caret.MoveTo(new SnapshotPoint(newSnapshot, newCaretPos));
        }

        private int Clamp(int v, int min, int max) => Math.Min(Math.Max(min, v), max);

        public override void TextInput(TextCompositionEventArgs e) {
            if (!HxState.Enabled)
                return;

            e.Handled = true;
        }

    }
}
