namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private void WireTemplatePaneEvents()
        {
            TemplatePane.AddTemplateClicked += AddTemplate_Click;
            TemplatePane.TemplateSelectionChanged += TemplatesList_SelectionChanged;
            TemplatePane.DeleteTemplateClicked += DeleteTemplate_Click;
            TemplatePane.SaveTemplateClicked += SaveTemplate_Click;
            TemplatePane.TemplateTextChanged += TemplateField_TextChanged;
            TemplatePane.ExplainRecipeClicked += ExplainRecipe_Click;
            TemplatePane.FixRecipeClicked += FixRecipe_Click;
            TemplatePane.SummarizeRecipeClicked += SummarizeRecipe_Click;
            TemplatePane.ReviewRecipeClicked += ReviewRecipe_Click;
        }
    }
}
