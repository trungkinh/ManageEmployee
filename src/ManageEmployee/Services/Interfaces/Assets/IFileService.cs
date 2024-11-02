using ManageEmployee.DataTransferObject.FileModels;

namespace ManageEmployee.Services.Interfaces.Assets;

public interface IFileService
{
    string Upload(IFormFile file, string folder = "", string fileNameUpload = "");

    bool DeleteFileUpload(string filePath);
    FileDetailModel UploadFile(IFormFile file, string folder, string fileNameUpload);
}
