using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using Microsoft.EntityFrameworkCore;

namespace Client
{
    public class AppDbContext : DbContext
    {
        public static readonly string DbFileName = "BackupService.client.db";
        public DbSet<ItemAction> Actions { get; set; }
        public DbSet<MetaData> MetaData { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source={DbFileName}");
        }
    }

    public class BaseItem
    {
        public int Id { get; set; }
        public DateTime Updated { get; set; }
        // concurrency management
        [Timestamp]
        public byte[] RowVersion { get; set; }
    }

    public class MetaData : BaseItem
    {
        public DateTime LastSync { get; set; }
    }

    public class ItemAction : BaseItem
    {
        public WatcherChangeTypes Action { get; set; }
        public string OldPath { get; set; }
        public string Path { get; set; }
    }
}
