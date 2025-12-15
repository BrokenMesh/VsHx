using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace VsHx
{
    [Export(typeof(IWpfTextViewMarginProvider))]
    [Name(HxModeMargin.MarginName)]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    [MarginContainer(PredefinedMarginNames.Bottom)]
    [Order(After = PredefinedMarginNames.HorizontalScrollBar)]
    internal sealed class HxModeMarginProvider : IWpfTextViewMarginProvider
    {
        public IWpfTextViewMargin CreateMargin(
            IWpfTextViewHost host,
            IWpfTextViewMargin container)
        {
            return new HxModeMargin(host.TextView);
        }
    }
}
