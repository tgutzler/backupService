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
                foreach (var dirName in dirNames)
                {
                    dir = context.Directories.FirstOrDefault(d =>
                        ((d.Name == dirName) && (d.ParentId == parentId)));
                    if (dir == null) break;
                    parentId = dir.Id;
                }
                if ((dir != null) && includeChildren)
                {
                    context.Files.Where(f => f.ParentId == dir.Id).Load();
                    context.Directories.Where(d => d.ParentId == dir.Id).Load();
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
                }
            }
            return dir;
        }

        public bool UpdateDirectory(BackedUpDirectory directory)
        {
            using (var context = new AppDbContext(_dbConnectionConfig))
            {
                var dir = context.Directories.FirstOrDefault(d => d == directory);
                if (dir == null) return false;
                if (directory.Files != null)
                {
                    dir.Files = directory.Files;
                }
                dir.Modified = directory.Modified;
                context.SaveChanges();
                return true;
            }
        }

        public void AddFile(BackedUpFile file)
        {
            if (file.ParentId <= 0)
            {
                throw new InvalidDataException($"Cannot add file ({file.Name}) without a parent (ID:{file.ParentId})");
            }
            using (var context = new AppDbContext(_dbConnectionConfig))
            {
                context.Files.Add(file);
                context.SaveChanges();
            }
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
            }
            return file;
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
