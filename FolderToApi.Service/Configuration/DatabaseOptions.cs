using System.ComponentModel.DataAnnotations;

namespace FolderToApi.Service.Configuration;

public class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required(ErrorMessage = "ConnectionString is required")]
    public string ConnectionString { get; set; } = "Data Source=foldertoapi.db";

    public bool EnableWalMode { get; set; } = true;
}
