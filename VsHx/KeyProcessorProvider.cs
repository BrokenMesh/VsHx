using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Windows.Forms;

namespace VsHx
{
    [Export(typeof(IKeyProcessorProvider))]
    [Name("HxKeyProcessor")]
    [ContentType("code")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class HxKeyProcessorProvider : IKeyProcessorProvider
    {
        [Import] internal IVsEditorAdaptersFactoryService Adapters;

        [Import] internal ITextStructureNavigatorSelectorService NavigatorService;

        [Import] internal ITextUndoHistoryRegistry UndoRegistry;

        public KeyProcessor GetAssociatedProcessor(IWpfTextView view) {
            var vsView = Adapters.GetViewAdapter(view);

            IOleCommandTarget next;
            var filter = new HxCommandFilter(null);

            vsView.AddCommandFilter(filter, out next);
            filter.SetNext(next);

            var navigator = NavigatorService.GetTextStructureNavigator(view.TextBuffer);
            var undoHistory = UndoRegistry.GetHistory(view.TextBuffer); 
            return new HxKeyProcessor(view, navigator, undoHistory);
        }

    }
}
