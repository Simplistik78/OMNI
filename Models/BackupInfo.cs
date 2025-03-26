using System;

namespace OMNI.Services
{
    public class BackupInfo
    {
        public string FileName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public DateTime CreationDate { get; set; }
        public double FileSizeInMB { get; set; }
    }
}