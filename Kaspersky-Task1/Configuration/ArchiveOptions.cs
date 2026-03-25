namespace Kaspersky_Task1.Configuration;

public sealed class ArchiveOptions
{
    public string FilesDir { get; set; } = "";
    public string ZipArchivesDir { get; set; } = "";
    public int ArchiveCacheTtlMinutes { get; set; } = 5;
    public int MaxFilesPerArchive { get; set; } = 1000;
}

