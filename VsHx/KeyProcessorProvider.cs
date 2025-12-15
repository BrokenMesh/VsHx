using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace VsHx
{
    [Export(typeof(IKeyProcessorProvider))]
    [Name("HxKeyProcessor")]
    [ContentType("code")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class HxKeyProcessorProvider : IKeyProcessorProvider
    {
        [Import]
        internal IVsEditorAdaptersFactoryService Adapters;

        public KeyProcessor GetAssociatedProcessor(IWpfTextView view)
        {
            var vsView = Adapters.GetViewAdapter(view);

            IOleCommandTarget next;
            var filter = new HxCommandFilter(null);

            vsView.AddCommandFilter(filter, out next);
            filter.SetNext(next);

            return new HxKeyProcessor(view);
        }

    }
}
