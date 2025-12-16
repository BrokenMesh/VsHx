using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;
namespace VsHx
{
    internal sealed class HxCommandFilter : IOleCommandTarget
    {
        private IOleCommandTarget _next;

        public HxCommandFilter(IOleCommandTarget next)
        {
            _next = next;
        }

        public void SetNext(IOleCommandTarget next)
        {
            _next = next;
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
            => _next.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            System.Diagnostics.Debug.WriteLine($"{pguidCmdGroup} {nCmdID}");


            if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97 && nCmdID == (uint)VSConstants.VSStd97CmdID.Replace)
            {
                HxState.Enabled = !HxState.Enabled;
                HxState.Reset();
                HxState.StateHasChanged();
                return VSConstants.S_OK;
            }

            if (HxState.Enabled && pguidCmdGroup == VSConstants.VSStd2K && nCmdID == (uint)VSConstants.VSStd2KCmdID.TYPECHAR)
            {
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
                            if (HxState.Registers.ContainsKey(HxState.RegistersStr)) HxState.Registers.Remove(HxState.RegistersStr);
                            HxState.Registers.Add(HxState.RegistersStr, HxState.RegContentStr);
                        }
                        else {
                            if (HxState.Registers.ContainsKey(HxState.RegistersStr)) {
                                Paste(HxState.Registers[HxState.RegistersStr]);
                            }
                        }

                        HxState.Reset();
                        HxState.StateHasChanged();
                    }

                    return VSConstants.S_OK; 
                }

                if (nCmdID == (uint)VSConstants.VSStd2KCmdID.TYPECHAR ||
                    nCmdID == (uint)VSConstants.VSStd2KCmdID.BACKSPACE) { 
                    return VSConstants.S_OK; 
                }
            }

            return _next.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        private void Paste(string text) {
            var view = HxState.View;
            if (view == null) return;

            var selection = view.Selection;
            var buffer = view.TextBuffer;

            int insertPos = selection.IsEmpty
                ? view.Caret.Position.BufferPosition.Position
                : selection.Start.Position;

            using (var edit = buffer.CreateEdit()) {
                if (!selection.IsEmpty) {
                    foreach (var span in selection.SelectedSpans)
                        edit.Delete(span);
                }

                edit.Insert(insertPos, text);
                edit.Apply();
            }

            selection.Clear();

            var snapshot = buffer.CurrentSnapshot;
            view.Caret.MoveTo(new SnapshotPoint(snapshot, insertPos + text.Length));
        }
    }




}
