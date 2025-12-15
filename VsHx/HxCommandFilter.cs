using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
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

            return _next.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }
    }




}
