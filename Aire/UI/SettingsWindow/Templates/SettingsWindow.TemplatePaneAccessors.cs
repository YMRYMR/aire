using Aire.UI.Settings.Controls;
using Button = System.Windows.Controls.Button;
using ListBox = System.Windows.Controls.ListBox;
using TextBox = System.Windows.Controls.TextBox;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private TemplateListPaneControl TemplatePane => TemplateListPaneControl;

        private ListBox TemplatesListView => TemplatePane.TemplatesListView;
        private Button TemplateAddButton => TemplatePane.AddTemplateButton;
        private TextBlock TemplateEditPanelTitle => TemplatePane.EditPanelTitle;
        private TextBox TemplateNameTextBox => TemplatePane.NameTextBox;
        private TextBox TemplatePrefixTextBox => TemplatePane.PrefixTextBox;
        private TextBox TemplateShortcutTextBox => TemplatePane.ShortcutTextBox;
        private TextBox TemplateTemplateTextBox => TemplatePane.TemplateTextBox;
        private TextBox TemplateSampleTextBox => TemplatePane.SampleTextBox;
        private TextBlock TemplatePreviewText => TemplatePane.PreviewText;
        private Button TemplateSaveButton => TemplatePane.SaveButton;
    }
}
