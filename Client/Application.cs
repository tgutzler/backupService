using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Client
{
    internal class App
    {
        CancellationToken _token;
        List<string> _monitorDirs = new List<string> { @"C:\Users\tom\Documents\programming\C#\BackupService\test" };
        List<FileSystemWatcher> _watchers = new List<FileSystemWatcher>();
        Debouncer _modifiedDebouncer = new Debouncer();
        Debouncer _renamedDebouncer = new Debouncer();
        SyncEngine _syncEngine;

        public int DebounceTimeMs { get; set; }

        public App(string serverUri, CancellationToken token)
        {
            _token = token;
            DebounceTimeMs = 10000;

            _modifiedDebouncer.Debounced += ModifiedDebouncer_Debounced;
            _renamedDebouncer.Debounced += RenamedDebouncer_Debounced;

            InitDatabaseAsync();

            _syncEngine = new SyncEngine(serverUri, "Tom");
            ActionQueue.Instance.Initialise(token, _syncEngine);
        }

        public async Task RunAsync()
        {
            // Start monitoring FileSystem events for all given directories
            foreach (var dir in _monitorDirs)
            {
                Console.WriteLine($"Monitoring {dir}");
                FileSystemWatcher watcher = new FileSystemWatcher()
                {
                    Path = dir,
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true
                };
                RegisterEvents(watcher);
                _watchers.Add(watcher);
            }

            // Wait for server to come online
            while (!await _syncEngine.PingAsync().ConfigureAwait(false))
            {
                await Task.Delay(100, _token).ConfigureAwait(false);
                _token.ThrowIfCancellationRequested();
            }
            Console.WriteLine("Backup server online. Starting sync");

            // synchronise with server while collecting FileSystem events
            try
            {
                var tasks = _monitorDirs.Select(d => _syncEngine.SynchroniseAsync(d));
                await Task.WhenAll(tasks).ConfigureAwait(false);

                Console.WriteLine("Processing queued events");
                ActionQueue.Instance.Paused = false;
                // go through queued events
                while (true)
                {
                    await Task.Delay(1000, _token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Cancellation requested. Shutting down");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception {ex.Message}");
            }
            finally
            {
                foreach (var watcher in _watchers)
                {
                    RegisterEvents(watcher, false);
                }
            }
        }

        private void RegisterEvents(FileSystemWatcher watcher, bool register = true)
        {
            if (register)
            {
                watcher.Created += Watcher_Modified;
                watcher.Changed += Watcher_Modified;
                watcher.Deleted += Watcher_Modified;
                watcher.Renamed += Watcher_Renamed;
                watcher.Error += Watcher_Error;
            }
            else
            {
                watcher.Created -= Watcher_Modified;
                watcher.Changed -= Watcher_Modified;
                watcher.Deleted -= Watcher_Modified;
                watcher.Renamed -= Watcher_Renamed;
                watcher.Error -= Watcher_Error;
            }
        }

        private async void InitDatabaseAsync()
        {
            using (var context = new AppDbContext())
            {
                await context.Database.EnsureDeletedAsync().ConfigureAwait(false);
                await context.Database.EnsureCreatedAsync().ConfigureAwait(false);
                var metaData = context.MetaData.FirstOrDefault();
                if (metaData == null)
                {
                    metaData = new MetaData()
                    {
                        LastSync = DateTime.MinValue
                    };
                    await context.MetaData.AddAsync(metaData).ConfigureAwait(false);
                }
            }
        }

        private void Watcher_Error(object sender, ErrorEventArgs e)
        {
            Console.WriteLine($"Error: {e.ToString()}");
        }

        private void Watcher_Renamed(object sender, RenamedEventArgs e)
        {
            if (IsIgnored(e.FullPath)) return;

            _renamedDebouncer.Start(e.FullPath, e, DebounceTimeMs);
        }

        private void Watcher_Modified(object sender, FileSystemEventArgs e)
        {
            if (IsIgnored(e.FullPath)) return;

            _modifiedDebouncer.Start(e.FullPath, e, DebounceTimeMs);
        }

        private void RenamedDebouncer_Debounced(object sender, object o)
        {
            if ((o is DebouncerEventArgs a) && (a.Args is RenamedEventArgs e))
            {
                Console.WriteLine($"{e.ChangeType}: {e.OldFullPath} -> {e.FullPath}");
                ActionQueue.Instance.Add(new ItemAction()
                {
                    Action = e.ChangeType,
                    OldPath = e.OldFullPath,
                    Path = e.FullPath,
                    Updated = DateTime.Now
                });                
            }
        }

        private void ModifiedDebouncer_Debounced(object sender, object o)
        {
            if ((o is DebouncerEventArgs a) && (a.Args is FileSystemEventArgs e))
            {
                Console.WriteLine($"{e.ChangeType}: {e.FullPath}");
                ActionQueue.Instance.Add(new ItemAction()
                {
                    Action = e.ChangeType,
                    Path = e.FullPath,
                    Updated = DateTime.Now
                });
            }
        }

        private bool IsIgnored(string path)
        {
            var fileName = Path.GetFileName(path);
            if (fileName == AppDbContext.DbFileName) return true;

            //var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            if (ext == ".db-journal") return true;

            return false;
        }
    }

    internal class Debouncer
    {
        private ConcurrentDictionary<DictionaryKey, ObjectContainer> _dict;

        public event EventHandler<object> Debounced;

        public Debouncer()
        {
            _dict = new ConcurrentDictionary<DictionaryKey, ObjectContainer>();
        }

        /// <summary>
        /// Sets the debounce object and debounce time
        /// If debouncing is already in progres, updates the debounce object
        /// </summary>
        /// <param name="args">Object to be returned by the Debounced event</param>
        /// <param name="dueTime">The amount of debeounce time in milliseconds</param>
        /// <returns>true if debouncing is not already in progress</returns>
        public bool Start(string path, FileSystemEventArgs args, int dueTime)
        {
            bool started = false;
            var key = new DictionaryKey { Path = path, ChangeType = args.ChangeType };
            if (_dict.TryGetValue(key, out ObjectContainer oc))
            {
                oc.Args = args;
            }
            else
            {
                var container = new ObjectContainer(key, args);
                var t = new Timer(DebouncerCallback, container, dueTime, Timeout.Infinite);
                container.Timer = t;
                _dict.TryAdd(key, container);
                started = true;
            }

            return started;
        }

        private void DebouncerCallback(object state)
        {
            if (state is ObjectContainer oc)
            {
                oc.Timer.Dispose();
                _dict.TryRemove(oc.Key, out ObjectContainer tmp);
                var args = new DebouncerEventArgs()
                { Args = oc.Args };
                Debounced?.Invoke(this, args);
            }
        }

        private class ObjectContainer
        {
            public ObjectContainer(DictionaryKey key, FileSystemEventArgs args)
            {
                Key = key;
                Args = args;
            }
            public DictionaryKey Key { get; set; }
            public Timer Timer { get; set; }
            public FileSystemEventArgs Args { get; set; }
        }
    }

    internal class DictionaryKey
    {
        public string Path { get; set; }
        public WatcherChangeTypes ChangeType { get; set; }
    }

    internal class DebouncerEventArgs : EventArgs
    {
        public FileSystemEventArgs Args { get; set; }
    }
}
