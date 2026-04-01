using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Aire.Services;
using Aire.UI.MainWindow.Models;
using Xunit;

namespace Aire.Tests.UI.Models
{
    public class ChatMessageTests : TestBase
    {
        [Fact]
        public void ChatMessage_RaisesPropertyChangesAndComputedFlags()
        {
            List<string> changed = new List<string>();
            ChatMessage chatMessage = new ChatMessage();
            chatMessage.PropertyChanged += (s, e) => { if (e.PropertyName != null) changed.Add(e.PropertyName); };
            
            chatMessage.Text = "hello";
            chatMessage.PendingToolCall = new ToolCallRequest { Tool = "read_file" };
            chatMessage.IsApprovalPending = true;
            chatMessage.ToolCallStatus = "running";
            chatMessage.IsSearchMatch = true;
            
            // Mock bitmaps with 1x1 size
            chatMessage.ScreenshotImage = new WriteableBitmap(1, 1, 96, 96, PixelFormats.Bgra32, null);
            chatMessage.AttachedImage = new WriteableBitmap(1, 1, 96, 96, PixelFormats.Bgra32, null);
            
            chatMessage.TodoItems = new ObservableCollection<TodoItem> { new TodoItem { Id = "1", Description = "Do thing" } };
            chatMessage.FollowUpQuestion = "Next?";
            chatMessage.FollowUpOptions = new List<string> { "Yes", "No" };
            chatMessage.AnswerSubmitted = true;

            Assert.Equal("hello", chatMessage.Text);
            Assert.True(chatMessage.HasToolCallStatus);
            Assert.True(chatMessage.HasScreenshot);
            Assert.True(chatMessage.HasAttachedImage);
            Assert.True(chatMessage.HasTodoItems);
            Assert.True(chatMessage.HasFollowUpQuestion);
            Assert.True(chatMessage.HasFollowUpOptions);
            Assert.False(chatMessage.ShowAnswerButtons);
            
            Assert.Contains("Text", changed);
            Assert.Contains("HasToolCallStatus", changed);
            Assert.Contains("HasScreenshot", changed);
        }

        [Fact]
        public void TodoItem_UpdatesComputedProperties()
        {
            List<string> changed = new List<string>();
            TodoItem todoItem = new TodoItem();
            todoItem.PropertyChanged += (s, e) => { if (e.PropertyName != null) changed.Add(e.PropertyName); };
            
            todoItem.Status = "completed";
            Assert.Equal("✓", todoItem.StatusIcon);
            Assert.Equal("#5A9A5A", todoItem.StatusColor);
            Assert.True(todoItem.IsCompleted);
            
            todoItem.Status = "blocked";
            Assert.Equal("✗", todoItem.StatusIcon);
            Assert.Equal("#9A4444", todoItem.StatusColor);
            Assert.False(todoItem.IsCompleted);
            
            Assert.Contains("StatusIcon", changed);
            Assert.Contains("StatusColor", changed);
            Assert.Contains("IsCompleted", changed);
        }
    }
}
