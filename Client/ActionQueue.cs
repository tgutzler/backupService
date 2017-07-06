using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ServerApi.Database;
using Utils;

namespace Client
{
    internal class ActionQueue
    {
        private static ActionQueue _instance;
        private ConcurrentQueue<ItemAction> _queue;
        private SyncEngine _syncEngine;

        public static ActionQueue Instance => _instance ?? (_instance = new ActionQueue());
        private ActionQueue()
        {
            Paused = true;
            _queue = new ConcurrentQueue<ItemAction>();
        }

        public void Initialise(CancellationToken token, SyncEngine syncEngine)
        {
            _syncEngine = syncEngine;
            Task.Run(() => DequeueTask(), token);
        }

        public bool Paused { get; set; }

        public void Add(ItemAction action)
        {
            _queue.Enqueue(action);
        }

        private async Task DequeueTask()
        {
            try
            {
                while (true)
                {
//                    _cancellationToken.ThrowIfCancellationRequested();
                    if (Paused)
                    {
                        await Task.Delay(100);
                    }
                    else if (_queue.TryDequeue(out var action))
                    {
                        switch (action.Action)
                        {
                            case System.IO.WatcherChangeTypes.Created:
                                var backedUpFile = new BackedUpFile()
                                {
                                    Modified = action.Updated,
                                    Name = PathUtils.DirectoryName(action.Path)
                                };
                                await _syncEngine.Upload(action.Path, backedUpFile).ConfigureAwait(false);
                                break;
                            default:
                                Console.WriteLine($"{action.Path} {action.Action}");
                                break;
                        }
                    }
                    else
                    {
                        await Task.Delay(100).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Cancellation requested. Shutting down action queue");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception {ex.Message}");
            }
            Console.WriteLine($"DequeueTask finishing");
        }
    }
}
