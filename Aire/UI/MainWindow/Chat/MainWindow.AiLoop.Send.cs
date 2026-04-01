using System.Threading.Tasks;

namespace Aire
{
    public partial class MainWindow
    {
        private async Task SendMessageAsync()
            => await ChatFlow.SendMessageAsync();
    }
}
