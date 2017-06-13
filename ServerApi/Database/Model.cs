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

        public AppDbContext(IDatabaseConnectionConfig connectionConfig)
        {
            _connectionConfig = connectionConfig;
        }

//        public DbSet<BackupRoot> BackupRoots { get; set; }
        public DbSet<BackedUpDirectory> Directories { get; set; }

        public DbSet<BackedUpFile> Files { get; set; }

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

    [DataContract(Name = "BackedUpDirectory")]
    public class BackedUpDirectory : BaseItem
    {
        public BackedUpDirectory()
        {
            Directories = new List<BackedUpDirectory>();
            Files = new List<BackedUpFile>();
        }

        [DataMember(Name = "Name")]
        public string Name { get; set; }
        [DataMember(Name = "Modified")]
        public DateTime Modified { get; set; }
        public UInt32 Depth { get; set; }
        [DataMember(Name = "ParentId")]
        public int? ParentId { get; set; }
        public BackedUpDirectory Parent { get; set; }
        public ICollection<BackedUpDirectory> Directories { get; set; }
        [DataMember(Name = "Files")]
        public ICollection<BackedUpFile> Files { get; set; }
    }

    [DataContract(Name = "BackedUpFile")]
    public class BackedUpFile : BaseItem
    {
        [DataMember(Name = "Name")]
        public string Name { get; set; }
        [DataMember(Name = "Modified")]
        public DateTime Modified { get; set; }
        [DataMember(Name = "ParentId")]
        public int ParentId { get; set; }
        public BackedUpDirectory Parent { get; set; }

        [NotMapped]
        public string Hash => DataUtils.MD5Hash($"{ParentId}{Path.AltDirectorySeparatorChar}Name");
    }
}
