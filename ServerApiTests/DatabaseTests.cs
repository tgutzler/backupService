using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ServerApi.Database;
using ServerApi.Options;
using Xunit;

namespace ServerApiTests
{
    public class DatabaseTests
    {
        private DatabaseService _dbService;

        public DatabaseTests()
        {
            var isWindowsOS = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var dbOptions = new DbOptions()
            {
                ConnectionString = isWindowsOS ?
                    "DataSource=ServerApi.db" :
                    "server=192.168.1.3;userid=backup;pwd=backup;port=3306;database=backup_tom;sslmode=none;",
                DbType = isWindowsOS ? DbTypeEnym.SQLite : DbTypeEnym.MySQL
            };
            DatabaseService.Start(dbOptions);

            _dbService = DatabaseService.Instance;
            // Start fresh!
            _dbService.DropDB();
        }

        [Fact]
        public void GetNotExistingFileAndDir()
        {
            var file = _dbService.GetFile("doesnotexist", 1);
            Assert.Null(file);

            var dir = _dbService.GetDirectory(@"y:\are\you\not\there");
            Assert.Null(dir);
        }

        [Fact]
        public void AddDirAndFile()
        {
            var dir = new BackedUpDirectory()
            {
                Modified = DateTime.Now,
                Name = @"c:\root1"
            };
            _dbService.AddDirectory(dir);

            var file1 = new BackedUpFile()
            {
                Name = "file1",
                ParentId = dir.Id,
                Modified = DateTime.Now
            };
            _dbService.AddFile(file1);

            var dir2 = _dbService.GetDirectory(@"c:\root1");
            Assert.True(dir.Equals(dir2));
            var file2 = _dbService.GetFile("file1", dir.Id);
            Assert.True(file1.Id == file2.Id);
            Assert.True(file1.Hash == file2.Hash);
        }

        [Fact]
        public void AddDirAndDir()
        {
            var dir1 = new BackedUpDirectory()
            {
                Modified = DateTime.Now,
                Name = @"c:\root2"
            };
            _dbService.AddDirectory(dir1);

            var dir2 = new BackedUpDirectory()
            {
                Modified = DateTime.Now,
                Name = @"sub2",
                ParentId = dir1.Id
            };
            _dbService.AddDirectory(dir2);
        }

        [Fact]
        public void AddThreeDirsAFileAndCheckDeps()
        {
            var grandparent = new BackedUpDirectory() { Name = "vol:", Depth = 1 };
            var parent = new BackedUpDirectory { Name = "parent", Depth = 2 };
            var child = new BackedUpDirectory { Name = "child", Depth = 3 };
            var parentFile = new BackedUpFile { Name = "parentFile" };

            _dbService.AddDirectory(grandparent);
            Assert.Null(grandparent.Parent);

            parent.ParentId = grandparent.Id;
            _dbService.AddDirectory(parent);
            Assert.True(parent.ParentId == grandparent.Id);

            child.ParentId = parent.Id;
            _dbService.AddDirectory(child);
            Assert.True(child.ParentId == parent.Id);

            parentFile.ParentId = parent.Id;
            _dbService.AddFile(parentFile);

            var dir = _dbService.GetDirectory(@"vol:/parent");
            Assert.True(dir.Directories.Count == 0);
            Assert.True(dir.Files.Count == 0);

            dir = _dbService.GetDirectory(@"vol:/parent", includeChildren: true);
            Assert.True(dir.Directories.Count == 1);
            Assert.True(dir.Files.Count == 1);

            dir = _dbService.GetDirectory(parent.Id);
            Assert.True(dir.Directories.Count == 0);
            Assert.True(dir.Files.Count == 0);
            Assert.Null(dir.Parent);

            dir = _dbService.GetDirectory(parent.Id, includeChildren: true);
            Assert.True(dir.Directories.Count == 1);
            Assert.True(dir.Files.Count == 1);
            Assert.Null(dir.Parent);

            dir = _dbService.GetDirectory(parent.Id, includeParent: true);
            Assert.True(dir.Directories.Count == 0);
            Assert.True(dir.Files.Count == 0);
            Assert.True(dir.Parent.Id == parent.ParentId);

            dir = _dbService.GetDirectory(child.Name, parent.Id);
            Assert.True(dir.Id == child.Id);
            Assert.True(dir.Name == child.Name);
            Assert.True(dir.ParentId == parent.Id);
        }
    }
}
