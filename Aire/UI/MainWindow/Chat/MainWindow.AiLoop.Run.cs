using System.Threading;
using System.Threading.Tasks;

namespace Aire
{
    public partial class MainWindow
    {
        private async Task RunAiTurnAsync(int iteration = 0, bool wasVoice = false, CancellationToken cancellationToken = default)
            => await ChatFlow.RunAiTurnAsync(iteration, wasVoice, cancellationToken);
    }
}
