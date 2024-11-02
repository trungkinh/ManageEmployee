using ManageEmployee.DataTransferObject.FileModels;
using ManageEmployee.Services.Interfaces.Assets;

namespace ManageEmployee.Services;

public class FileService : IFileService
{
    public string Upload(IFormFile file, string folder = "Images", string fileNameUpload = null)
    {
        var uploadDirecotroy = "Uploads\\";
        var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), uploadDirecotroy);

        uploadDirecotroy += folder;
        uploadPath = Path.Combine(uploadPath, folder);

        if (!Directory.Exists(uploadPath))
            Directory.CreateDirectory(uploadPath);
        var fileName = (!string.IsNullOrEmpty(fileNameUpload) ? fileNameUpload : (Guid.NewGuid().ToString() + Path.GetExtension(file.FileName)));
        var filePath = Path.Combine(uploadPath, fileName);

        using (var stream = File.Create(filePath))
        {
            file.CopyTo(stream);
        }

        return Path.Combine(uploadDirecotroy, fileName);
    }

    public FileDetailModel UploadFile(IFormFile file, string folder, string fileNameUpload)
    {
        var uploadDirecotroy = "Uploads\\";
        var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), uploadDirecotroy);
        uploadDirecotroy += folder;

        uploadPath = Path.Combine(uploadPath, folder);

        if (!Directory.Exists(uploadPath))
            Directory.CreateDirectory(uploadPath);
        var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
        var filePath = Path.Combine(uploadPath, fileName);

        using (var stream = File.Create(filePath))
        {
            file.CopyTo(stream);
        }

        return new FileDetailModel
        {
            FileUrl = Path.Combine(uploadDirecotroy, fileName),
            FileName = fileNameUpload
        };
            
    }

    public bool DeleteFileUpload(string filePath)
    {
        var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), filePath);
        if (File.Exists(uploadPath))
        {
            File.Delete(uploadPath);
        }
        return true;
    }
}