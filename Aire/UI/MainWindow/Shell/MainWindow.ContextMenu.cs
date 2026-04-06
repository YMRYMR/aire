using System.Windows;
using System.Windows.Controls;
using Aire.Services;

namespace Aire
{
    public partial class MainWindow
    {
        private void MessagesScrollViewer_ContextMenuOpening(object sender, System.Windows.Controls.ContextMenuEventArgs e)
        {
            var menu = new System.Windows.Controls.ContextMenu();
            
            // Find menu item
            var findItem = new System.Windows.Controls.MenuItem 
            {
                Header = LocalizationService.S("menu.find", "Find...  Ctrl+F"),
                Tag = "find"
            };
            findItem.Click += FindMenuItem_Click;
            
            // Save chat menu item
            var saveItem = new System.Windows.Controls.MenuItem 
            {
                Header = LocalizationService.S("menu.saveChat", "Save chat as text..."),
                Tag = "save"
            };
            saveItem.Click += SaveChatText_Click;
            
            // Copy chat menu item
            var copyItem = new System.Windows.Controls.MenuItem 
            {
                Header = LocalizationService.S("menu.copyChat", "Copy chat as text"),
                Tag = "copy"
            };
            copyItem.Click += CopyChatText_Click;
            
            // Clear conversation menu item
            var clearItem = new System.Windows.Controls.MenuItem 
            {
                Header = LocalizationService.S("menu.clearConversation", "Clear conversation"),
                Tag = "clear"
            };
            clearItem.Click += ClearChatMenuItem_Click;
            
            // Restore window sizes menu item
            var restoreItem = new System.Windows.Controls.MenuItem 
            {
                Header = LocalizationService.S("menu.restoreWindowSizes", "Restore original window sizes"),
                Tag = "restore"
            };
            restoreItem.Click += RestoreWindowSizes_Click;
            
            // Build the menu structure
            menu.Items.Add(findItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(saveItem);
            menu.Items.Add(copyItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(clearItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(restoreItem);
            
            // Set the context menu
            MessagesScrollViewer.ContextMenu = menu;
            e.Handled = true;
        }
        

    }
}