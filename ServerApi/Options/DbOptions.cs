namespace ServerApi.Options
{
    public enum DbTypeEnym
    {
        MySQL,
        SQLite
    }

    public class DbOptions
    {
        public string ConnectionString { get; set; }
        public DbTypeEnym DbType { get; set; }
    }
}
