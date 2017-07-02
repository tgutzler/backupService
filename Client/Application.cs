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
        List<string> monitorDirs = new List<string> { @"C:\Users\tom\Documents\programming\C#\BackupService\test" };
        List<FileSystemWatcher> watchers = new List<FileSystemWatcher>();
        Debouncer _modifiedDebouncer = new Debouncer();
        Debouncer _renamedDebouncer = new Debouncer();
        SyncEngine _syncEngine;

        public int DebounceTimeMs { get; set; }

        public App(string serverUri, CancellationToken token)
        {
            _token = token;
            DebounceTimeMs = 10000;

            _modifiedDebouncer.Debounced += ModifiedDebouncer_DebouncedAsync;
            _renamedDebouncer.Debounced += RenamedDebouncer_DebouncedAsync;

            InitDatabaseAsync();

            _syncEngine = new SyncEngine(serverUri, "Tom");
        }

        public async Task RunAsync()
        {
            // Start monitoring FileSystem events for all given directories
            foreach (var dir in monitorDirs)
            {
                Console.WriteLine($"Monitoring {dir}");
                FileSystemWatcher watcher = new FileSystemWatcher()
                {
                    Path = dir,
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true
                };
                RegisterEvents(watcher);
            }

            // Wait for server to come online
            while (!await _syncEngine.PingAsync().ConfigureAwait(false))
            {
                await Task.Delay(100, _token).ConfigureAwait(false);
                _token.ThrowIfCancellationRequested();
            }

            // synchronise with server while collecting FileSystem events
            try
            {
                var tasks = monitorDirs.Select(d => _syncEngine.SynchroniseAsync(d));
                await Task.WhenAll(tasks).ConfigureAwait(false);
                // go through queued events
                while (true)
                {
                    await Task.Delay(1000, _token).ConfigureAwait(false);
                    _token.ThrowIfCancellationRequested();
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
                foreach (var watcher in watchers)
                {
                    RegisterEvents(watcher, true);
                }
            }
        }

        private void RegisterEvents(FileSystemWatcher watcher, bool unregister = false)
        {
            if (unregister == false)
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

        private async void RenamedDebouncer_DebouncedAsync(object sender, object o)
        {
            if ((o is DebouncerEventArgs a) && (a.Object is RenamedEventArgs e))
            {
                Console.WriteLine($"{e.ChangeType}: {e.OldFullPath} -> {e.FullPath}");
                using (var context = new AppDbContext())
                {
                    await context.Actions.AddAsync(new ItemAction()
                    {
                        Action = e.ChangeType,
                        OldPath = e.OldFullPath,
                        Path = e.FullPath,
                        Updated = DateTime.Now
                    }).ConfigureAwait(false);
                    await context.SaveChangesAsync().ConfigureAwait(false);
                }
            }
        }

        private async void ModifiedDebouncer_DebouncedAsync(object sender, object o)
        {
            if ((o is DebouncerEventArgs a) && (a.Object is FileSystemEventArgs e))
            {
                Console.WriteLine($"{e.ChangeType}: {e.FullPath}");
                using (var context = new AppDbContext())
                {
                    await context.Actions.AddAsync(new ItemAction()
                    {
                        Action = e.ChangeType,
                        Path = e.FullPath,
                        Updated = DateTime.Now
                    }).ConfigureAwait(false);
                    await context.SaveChangesAsync().ConfigureAwait(false);
                }
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
        private ConcurrentDictionary<string, ObjectContainer> _dict;

        public event EventHandler<object> Debounced;

        public Debouncer()
        {
            _dict = new ConcurrentDictionary<string, ObjectContainer>();
        }

        /// <summary>
        /// Sets the debounce object and debounce time
        /// If debouncing is already in progres, updates the debounce object
        /// </summary>
        /// <param name="o">Object to be returned by the Debounced event</param>
        /// <param name="dueTime">The amount of debeounce time in milliseconds</param>
        /// <returns>true if debouncing is not already in progress</returns>
        public bool Start(string item, object o, int dueTime)
        {
            bool started = false;
            if (_dict.TryGetValue(item, out ObjectContainer oc))
            {
                oc.Object = o;
            }
            else
            {
                var container = new ObjectContainer(item, o);
                var t = new Timer(DebouncerCallback, container, dueTime, Timeout.Infinite);
                container.Timer = t;
                _dict.TryAdd(item, container);
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
                { Object = oc.Object };
                Debounced?.Invoke(this, args);
            }
        }

        private class ObjectContainer
        {
            public ObjectContainer(string key, object o)
            {
                Key = key;
                Object = o;
            }
            public string Key { get; set; }
            public Timer Timer { get; set; }
            public object Object { get; set; }
        }
    }

    internal class DebouncerEventArgs : EventArgs
    {
        public object Object { get; set; }
    }
}
