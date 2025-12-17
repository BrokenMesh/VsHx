using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Extensibility.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using Selection = Microsoft.VisualStudio.Text.Selection;
namespace VsHx
{
    internal sealed class HxCommandFilter : IOleCommandTarget
    {
        private IOleCommandTarget _next;

        public HxCommandFilter(IOleCommandTarget next) {
            _next = next;
        }

        public void SetNext(IOleCommandTarget next) {
            _next = next;
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
            => _next.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {

            if (!(pguidCmdGroup == VSConstants.VSStd2K && nCmdID == (uint)VSConstants.VSStd2KCmdID.SolutionPlatform) &&
                !(pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97 && nCmdID == (uint)VSConstants.VSStd97CmdID.SolutionCfg)) {
                System.Diagnostics.Debug.WriteLine($"{pguidCmdGroup} {nCmdID}");
            }

            if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97 && nCmdID == (uint)VSConstants.VSStd97CmdID.Replace) {
                HxState.Enabled = !HxState.Enabled;
                HxState.Reset();
                HxState.StateHasChanged();
                return VSConstants.S_OK;
            }

            if (pguidCmdGroup == VSConstants.VsStd11 && nCmdID == (uint)VSConstants.VSStd11CmdID.LocateFindTarget) {
                return VSConstants.S_OK;
            }

            if (HxState.Enabled && pguidCmdGroup == VSConstants.VSStd2K && nCmdID == (uint)VSConstants.VSStd2KCmdID.TYPECHAR) {
                return VSConstants.S_OK;
            }

            if (HxState.Enabled && pguidCmdGroup == VSConstants.VSStd2K) {

                if (nCmdID == (uint)VSConstants.VSStd2KCmdID.CANCEL) {
                    HxState.Reset();
                    HxState.StateHasChanged();
                    return VSConstants.S_OK;
                }

                if (nCmdID == (uint)VSConstants.VSStd2KCmdID.RETURN) {
                    if (HxState.HxMode == HxState.Mode.Register) {
                        if (HxState.RegContentStr != null) {
                            if (HxState.Registers.ContainsKey(HxState.StoredStr)) HxState.Registers.Remove(HxState.StoredStr);
                            HxState.Registers.Add(HxState.StoredStr, HxState.RegContentStr);
                        }
                        else {
                            if (HxState.Registers.ContainsKey(HxState.StoredStr)) {
                                Paste(HxState.Registers[HxState.StoredStr]);
                            }
                        }

                        HxState.Reset();
                        HxState.StateHasChanged();

                        return VSConstants.S_OK;
                    }
                    else if (HxState.HxMode == HxState.Mode.MoveToSymbol || HxState.HxMode == HxState.Mode.GoOverSymbol) {
                        bool found = HxState.MTSIsBackward ? FindPrevious(HxState.StoredStr) : FindNext(HxState.StoredStr);

                        if (found) {
                            HxState.HxMode = HxState.Mode.GoOverSymbol;
                        }
                        else {
                            HxState.Reset();
                        }

                        HxState.StateHasChanged();

                        return VSConstants.S_OK;
                    }
                }

                if (nCmdID == (uint)VSConstants.VSStd2KCmdID.BACKSPACE) {
                    if (HxState.HxMode == HxState.Mode.Register || HxState.HxMode == HxState.Mode.MoveToSymbol) {
                        int len = HxState.StoredStr.Length;
                        if (len != 0) {
                            HxState.StoredStr = HxState.StoredStr.Remove(len-1, 1);
                            HxState.StateHasChanged();
                        }
                    }

                    return VSConstants.S_OK; 
                }

                if (nCmdID == (uint)VSConstants.VSStd2KCmdID.TYPECHAR) {
                    return VSConstants.S_OK;
                }
            }

            return _next.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        private void Paste(string text) {
            if (string.IsNullOrEmpty(text)) return;

            int num = HxState.StoredNum ?? 1;

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
        }

        private bool FindNext(string text) {
            var view = HxState.View;
            if (view == null) return false;
        
            var caret = view.Caret;
            var snapshot = view.TextBuffer.CurrentSnapshot;
            var selection = view.Selection;
        
            int startPos = caret.Position.BufferPosition.Position;
        
            if (!selection.IsEmpty) startPos = selection.SelectedSpans.Last().End.Position;
        
            if (startPos >= snapshot.Length) return false;
        
            int found = snapshot.GetText().IndexOf(text, startPos, StringComparison.Ordinal);
            if (found < 0) return false;
        
            var start = new SnapshotPoint(snapshot, found);
            var end = new SnapshotPoint(snapshot, found + text.Length);
        
            if (!HxState.MTSSelect) {
                selection.Select(new SnapshotSpan(start, end), isReversed: false);
                caret.MoveTo(HxState.MTSIsTill ? start : end);
                caret.EnsureVisible();
            }
            else {
                var selectionBroker = view.GetMultiSelectionBroker();

                var newSelection = new Selection(new SnapshotSpan(start, end), isReversed: false);
                selectionBroker.AddSelection(newSelection);

                view.ViewScroller.EnsureSpanVisible(
                    new SnapshotSpan(start, end),
                    EnsureSpanVisibleOptions.ShowStart
                );
            }
        
            return true;
        }

        private bool FindPrevious(string text) {
            var view = HxState.View;
            if (view == null) return false;

            var caret = view.Caret;
            var snapshot = view.TextBuffer.CurrentSnapshot;
            var selection = view.Selection;
            string fullText = snapshot.GetText();

            int startPos = caret.Position.BufferPosition.Position;

            if (!selection.IsEmpty) startPos = selection.SelectedSpans[0].Start.Position;

            if (startPos <= 0) return false;

            int found = fullText.LastIndexOf(text, startPos - 1, StringComparison.Ordinal);
            if (found < 0) return false;

            var start = new SnapshotPoint(snapshot, found);
            var end = new SnapshotPoint(snapshot, found + text.Length);

            if (!HxState.MTSSelect) {
                selection.Select(new SnapshotSpan(start, end), isReversed: true);
                caret.MoveTo(HxState.MTSIsTill ? end : start);
                caret.EnsureVisible();
            }
            else {
                var selectionBroker = view.GetMultiSelectionBroker();
                var newSelection = new Selection(new SnapshotSpan(start, end), isReversed: true);
                selectionBroker.AddSelection(newSelection);
            }

            return true;
        }
    }




}
