namespace ServerApi.Interfaces
{
    public interface IBUDirectoryInfo
    {
        int? ParentId { get; set; }
        string Name { get; set; }
        string Path { get; set; }
    }

    public class BUDirectoryInfo : IBUDirectoryInfo
    {
        public BUDirectoryInfo(int? parentId = null, string name = null, string path = null)
        {
            ParentId = parentId;
            Name = name;
            Path = path?.Replace(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        }

        public int? ParentId { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
    }
}
