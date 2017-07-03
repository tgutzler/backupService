using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Runtime.Serialization;
using Microsoft.EntityFrameworkCore;
using MySQL.Data.EntityFrameworkCore.Extensions;
using Utils;

namespace ServerApi.Database
{
    public class AppDbContext : DbContext
    {
        private IDatabaseConnectionConfig _connectionConfig;

        public AppDbContext()
        {
            _connectionConfig = new SqliteDBConnectionConfig("DataSource=ServerApi.db");
        }

        public AppDbContext(IDatabaseConnectionConfig connectionConfig)
        {
            _connectionConfig = connectionConfig;
        }

        public DbSet<BackedUpDirectory> Directories { get; set; }

        public DbSet<BackedUpFile> Files { get; set; }

        public DbSet<FileHistory> FileHistory { get; set; }

        public DbSet<DirectoryHistory> DirectoryHistory { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            _connectionConfig.Configure(optionsBuilder);
        }
    }

    public interface IDatabaseConnectionConfig
    {
        void Configure(DbContextOptionsBuilder optionsBuilder);
    }

    public class MysqlDBConnectionConfig : IDatabaseConnectionConfig
    {
        private string _connectionString;

        public MysqlDBConnectionConfig(string connectionString)
        {
            _connectionString = connectionString;
        }

        public void Configure(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseMySQL(_connectionString);
        }
    }

    public class SqliteDBConnectionConfig : IDatabaseConnectionConfig
    {
        private string _connectionString;

        public SqliteDBConnectionConfig(string connectionString)
        {
            _connectionString = connectionString;
        }

        public void Configure(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite(_connectionString);
        }
    }

    [DataContract]
    public class BaseItem
    {
        [DataMember]
        public int Id { get; set; }
        [Timestamp]
        public byte[] RowVersion { get; set; }
    }

    //public class BackupRoot : BaseItem
    //{
    //    public BackupRoot()
    //    {
    //        Directories = new List<BackedUpDirectory>();
    //    }

    //    public string Name { get; set; }
    //    public ICollection<BackedUpDirectory> Directories { get; set; }
    //}

    [DataContract]
    public class BackedUpDirectory : BaseItem
    {
        public BackedUpDirectory()
        {
            Directories = new List<BackedUpDirectory>();
            Files = new List<BackedUpFile>();
        }

        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public UInt32 Depth { get; set; }
        [DataMember]
        public int? ParentId { get; set; }
        public BackedUpDirectory Parent { get; set; }
        public ICollection<BackedUpDirectory> Directories { get; set; }
        [DataMember]
        public ICollection<BackedUpFile> Files { get; set; }
        public ICollection<DirectoryHistory> History { get; set; }
        [DataMember]
        [NotMapped]
        public DateTime Modified { get; set; }
        [DataMember]
        [NotMapped]
        public bool Deleted { get; set; }
    }

    [DataContract]
    public class BackedUpFile : BaseItem
    {
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public int ParentId { get; set; }
        public BackedUpDirectory Parent { get; set; }
        public ICollection<FileHistory> History { get; set; }
        [DataMember]
        [NotMapped]
        public DateTime Modified { get; set; }
        [DataMember]
        [NotMapped]
        public bool Deleted { get; set; }

    }

    public class History : BaseItem
    {
        public DateTime Modified { get; set; }
        public DateTime LastSeen { get; set; }
        public bool Deleted { get; set; }
    }

    public class FileHistory : History
    {
        public int FileId { get; set; }
        public BackedUpFile File { get; set; }
    }

    public class DirectoryHistory : History
    {
        public int DirectoryId { get; set; }
        public BackedUpDirectory Directory { get; set; }
    }
}
