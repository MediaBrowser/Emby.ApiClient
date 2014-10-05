using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.ApiInteraction
{
    public static class TaskHelper
    {
        public static Task Delay(int delayMs, CancellationToken cancellationToken)
        {
#if PORTABLE
                        return TaskEx.Delay(delayMs, cancellationToken);
#else
            return Task.Delay(delayMs, cancellationToken);
#endif
        }
    }
}
