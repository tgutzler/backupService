using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ServerApi.Database;
using ServerApi.Options;

namespace ServerApi.Database
{
    public class DatabaseService
    {
        private static DatabaseService _instance;
        private DbOptions _options;
        private IDatabaseConnectionConfig _dbConnectionConfig;

        public static DatabaseService Instance => _instance;

        private DatabaseService(DbOptions options)
        {
            _options = options;
            switch (_options.DbType)
            {
                case DbTypeEnym.MySQL:
                    _dbConnectionConfig = new MysqlDBConnectionConfig(_options.ConnectionString);
                    break;
                case DbTypeEnym.SQLite:
                    _dbConnectionConfig = new SqliteDBConnectionConfig(_options.ConnectionString);
                    break;
                default:
                    throw new NotImplementedException($"Database type {_options.DbType} not configured");
            }

            using (var context = new AppDbContext(_dbConnectionConfig))
            {
                context.Database.EnsureCreated();
            }
        }

        public static bool Start(DbOptions options)
        {
            if (_instance == null)
            {
                _instance = new DatabaseService(options);
                return true;
            }
            return false;
        }

        public void AddDirectory(BackedUpDirectory directory)
        {
            using (var context = new AppDbContext(_dbConnectionConfig))
            {
                context.Directories.Add(directory);
                context.DirectoryHistory.Add(new DirectoryHistory()
                {
                    DirectoryId = directory.Id,
                    LastSeen = DateTime.Now,
                    Modified = directory.Modified
                });
                context.SaveChanges();
            }
        }

        //public void AddDirectories(IEnumerable<BackedUpDirectory> directories)
        //{
        //    using (var context = new AppDbContext(_dbConnectionConfig))
        //    {
        //        context.Directories.AddRange(directories);
        //        context.SaveChanges();
        //    }
        //}

        /// <summary>
        /// Get a directory from an absolute path. Always loads the parent
        /// </summary>
        /// <param name="path"></param>
        /// <param name="includeChildren"></param>
        /// <returns></returns>
        public BackedUpDirectory GetDirectory(string path, bool includeChildren = false)
        {
            var dirNames = path.Split(Path.AltDirectorySeparatorChar);
            BackedUpDirectory dir = null;
            using (var context = new AppDbContext(_dbConnectionConfig))
            {
                int? parentId = null;
                bool saveRequired = false;
                foreach (var dirName in dirNames)
                {
                    dir = context.Directories.FirstOrDefault(d =>
                        ((d.Name == dirName) && (d.ParentId == parentId)));
                    if (dir == null)
                    {
                        dir = new BackedUpDirectory()
                        {
                            Name = dirName,
                            ParentId = parentId
                        };
                        context.Directories.Add(dir);
                        var hist = new DirectoryHistory()
                        {
                            DirectoryId = dir.Id,
                            LastSeen = DateTime.Now,
                            Modified = dir.Modified
                        };
                        context.DirectoryHistory.Add(hist);
                        saveRequired = true;
                    }
                    parentId = dir.Id;
                }
                if (saveRequired)
                {
                    context.SaveChanges();
                }
                var history = context.DirectoryHistory.OrderByDescending(h => h.LastSeen)
                    .FirstOrDefault(h => (h.DirectoryId == dir.Id));
                dir.Modified = history.Modified;
                dir.Deleted = history.Deleted;
                if ((dir != null) && includeChildren)
                {
                    context.Files.Where(f => f.ParentId == dir.Id).Load();
                    foreach (var file in dir.Files)
                    {
                        var hist = (from h in context.FileHistory
                                    orderby h.Modified descending
                                    where h.FileId == file.Id
                                    select h).First();
                        file.Modified = hist.Modified;
                    }
                    context.Directories.Include(d => d.History)
                        .Where(d => d.ParentId == dir.Id).Load();
                    foreach (var subdir in dir.Directories)
                    {
                        var hist = (from h in context.DirectoryHistory
                                    orderby h.Modified descending
                                    where h.DirectoryId == subdir.Id
                                    select h).First();
                        subdir.Modified = hist.Modified;
                    }
                }
            }
            return dir;
        }

        public BackedUpDirectory GetDirectory(string name, int parentId, bool includeChildren = false, bool includeParent = false)
        {
            BackedUpDirectory dir = null;
            using (var context = new AppDbContext(_dbConnectionConfig))
            {
                dir = context.Directories.FirstOrDefault(d => d.ParentId == parentId && d.Name == name);
                if (dir != null)
                {
                    if (includeChildren)
                    {
                        context.Files.Where(f => f.ParentId == dir.Id).Load();
                        context.Directories.Where(d => d.ParentId == dir.Id).Load();
                    }
                    if (includeParent)
                    {
                        context.Directories.Where(d => d.Id == dir.ParentId).Load();
                    }
                    var history = context.DirectoryHistory.OrderByDescending(h => h.LastSeen)
                        .FirstOrDefault(h => (h.DirectoryId == dir.Id));
                    dir.Modified = history.Modified;
                    dir.Deleted = history.Deleted;
                }
            }
            return dir;
        }

        public BackedUpDirectory GetDirectory(int id, bool includeChildren = false, bool includeParent = false)
        {
            BackedUpDirectory dir = null;
            using (var context = new AppDbContext(_dbConnectionConfig))
            {
                dir = context.Directories.FirstOrDefault(d => d.Id == id);
                if (dir != null)
                {
                    if (includeChildren)
                    {
                        context.Files.Where(f => f.ParentId == dir.Id).Load();
                        context.Directories.Where(d => d.ParentId == dir.Id).Load();
                    }
                    if (includeParent)
                    {
                        context.Directories.Where(d => d.Id == dir.ParentId).Load();
                    }
                    var history = context.DirectoryHistory.OrderByDescending(h => h.LastSeen)
                        .FirstOrDefault(h => (h.DirectoryId == dir.Id));
                    dir.Modified = history.Modified;
                    dir.Deleted = history.Deleted;
                }
            }
            return dir;
        }

        public void UpdateDirectory(BackedUpDirectory directory)
        {
            using (var context = new AppDbContext(_dbConnectionConfig))
            {
                //var dir = context.Directories.FirstOrDefault(d => d == directory);
                //if (dir == null) return false;
                //if (directory.Files != null)
                //{
                //    dir.Files = directory.Files;
                //}
                //dir.Modified = directory.Modified;
                //context.SaveChanges();
                //return true;
                context.DirectoryHistory.Add(new DirectoryHistory()
                {
                    DirectoryId = directory.Id,
                    Modified = directory.Modified,
                    LastSeen = DateTime.UtcNow
                });
                context.SaveChanges();
            }
        }

        public FileHistory AddFile(BackedUpFile file)
        {
            FileHistory hist = null;
            if (file.ParentId <= 0)
            {
                throw new InvalidDataException($"Cannot add file ({file.Name}) without a parent (ID:{file.ParentId})");
            }
            using (var context = new AppDbContext(_dbConnectionConfig))
            {
                context.Files.Add(file);
                hist = new FileHistory()
                {
                    FileId = file.Id,
                    LastSeen = DateTime.Now,
                    Modified = file.Modified
                };
                context.FileHistory.Add(hist);
                context.SaveChanges();
            }

            return hist;
        }

        public BackedUpFile GetFile(int id)
        {
            using (var context = new AppDbContext(_dbConnectionConfig))
            {
                return context.Files.FirstOrDefault(f => f.Id == id);
            }
        }

        public BackedUpFile GetFile(string name, int parentId, bool includeParent = false)
        {
            BackedUpFile file;
            using (var context = new AppDbContext(_dbConnectionConfig))
            {
                file = context.Files.FirstOrDefault(f => (f.Name == name) && (f.ParentId == parentId));
                if ((file != null) && includeParent)
                {
                    context.Directories.Where(d => d.Id == file.ParentId).Load();
                }
                var history = context.FileHistory.OrderByDescending(h => h.LastSeen)
                    .FirstOrDefault(h => (h.FileId == file.Id));
                file.Modified = history.Modified;
                file.Deleted = history.Deleted;
            }
            return file;
        }

        public FileHistory UpdateFile(BackedUpFile file)
        {
            FileHistory hist = new FileHistory()
            {
                FileId = file.Id,
                LastSeen = DateTime.Now,
                Modified = file.Modified
            };
            using (var context = new AppDbContext(_dbConnectionConfig))
            {
                context.FileHistory.Add(hist);
                context.SaveChanges();
            }

            return hist;
        }

        public void DeleteFiles(List<BackedUpFile> files)
        {
            using (var context = new AppDbContext(_dbConnectionConfig))
            {
                files.ForEach(f => context.FileHistory.Add(new FileHistory()
                {
                    FileId = f.Id,
                    LastSeen = DateTime.Now,
                    Deleted = true
                }));
                context.SaveChanges();
            }
        }

        public int DirectoryCount
        {
            get
            {
                using (var context = new AppDbContext(_dbConnectionConfig))
                {
                    return context.Directories.Count();
                }
            }
        }

        public int FileCount
        {
            get
            {
                using (var context = new AppDbContext(_dbConnectionConfig))
                {
                    return context.Files.Count();
                }
            }
        }

        public void DropDB()
        {
            using (var context = new AppDbContext(_dbConnectionConfig))
            {
                context.Database.EnsureDeleted();
                context.Database.EnsureCreated();
            }
        }
    }
}
