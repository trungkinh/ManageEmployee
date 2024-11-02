using Common.Helpers;
using ManageEmployee.Dal.DbContexts;
using ManageEmployee.DataTransferObject.FileModels;
using ManageEmployee.DataTransferObject.PagingRequest;
using ManageEmployee.DataTransferObject.PagingResultModels;
using ManageEmployee.DataTransferObject.Web;
using ManageEmployee.Entities;
using ManageEmployee.Helpers;
using ManageEmployee.Services.Interfaces.Assets;
using ManageEmployee.Services.Interfaces.Webs;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace ManageEmployee.Services.Web;

public class WebNewsServices : IWebNewsService
{
    private readonly ApplicationDbContext _context;
    private readonly IFileService _fileService;

    public WebNewsServices(ApplicationDbContext context, IFileService fileService)
    {
        _context = context;
        _fileService = fileService;
    }
    
    public async Task Delete(int id)
    {
        var itemDelete = await _context.News.Where(x => x.Id == id && x.IsDelete != true).FirstOrDefaultAsync();
        if (itemDelete != null)
        {
            itemDelete.IsDelete = true;
            itemDelete.DeleteAt = DateTime.Now;
            _context.News.Update(itemDelete);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<NewsViewModel>> GetAll()
    {
        return await _context.News.Where(x => !x.IsDelete).Select(x => new NewsViewModel
        {
            Id = x.Id,
            Title = x.Title,
            ShortContent = x.ShortContent,
            Content = x.Content,
            TitleEnglish = x.TitleEnglish,
            ShortContentEnglish = x.ShortContentEnglish,
            ContentEnglish = x.ContentEnglish,
            TitleKorean = x.TitleKorean,
            ShortContentKorean = x.ShortContentKorean,
            ContentKorean = x.ShortContentKorean,
        }).ToListAsync();



    }

    public async Task<NewsViewDetailModel> GetById(int id)
    {
        var itemData = await _context.News.Where(x => x.Id == id && !x.IsDelete).FirstOrDefaultAsync();
        if (itemData is null)
        {
            throw new ErrorException(ErrorMessages.DataNotFound);
        }

        return new NewsViewDetailModel
        {
            Id = itemData.Id,
            Title = itemData.Title,
            ShortContent = itemData.ShortContent,
            File = JsonConvert.DeserializeObject<List<FileDetailModel>>(itemData.Image),
            Content = itemData.Content,
            CreateAt = itemData.CreatedAt,
            Type = itemData.Type,
            CategoryId = itemData.CategoryId,
            TitleEnglish = itemData.TitleEnglish,
            ShortContentEnglish= itemData.ShortContentEnglish,
            ContentEnglish = itemData.ContentEnglish,
            TitleKorean = itemData.TitleKorean,
            ShortContentKorean= itemData.ShortContentKorean,
            ContentKorean= itemData.ShortContentKorean,
        };
    }

    public async Task<PagingResult<NewsViewModel>> SearchNews(WebNewPagingRequestModel searchRequest)
    {
        try
        {
            var news = _context.News.Where(x => !x.IsDelete && x.Id != 0)
                                         .Where(x => searchRequest.Type == null || x.Type == searchRequest.Type)
                                         .Where(x => string.IsNullOrEmpty(searchRequest.SearchText) ||
                                                    (!string.IsNullOrEmpty(x.Title) && x.Title.ToLower().Contains(searchRequest.SearchText.ToLower())))
                                         .Where(x => searchRequest.CategoryId == null || x.CategoryId == searchRequest.CategoryId)
                                         
                                         .Select(x => new NewsViewModel
                                         {
                                             Id = x.Id,
                                             Title = x.Title,
                                             ShortContent = x.ShortContent,
                                            // Content = x.Content,
                                             CreateAt = x.CreatedAt,
                                             Type = x.Type,
                                             Images = x.Image.Deserialize<List<FileDetailModel>>(),
                                             CategoryId = x.CategoryId,
                                             TitleEnglish = x.TitleEnglish,
                                             ShortContentEnglish = x.ShortContentEnglish,
                                             ContentEnglish = x.ContentEnglish,
                                             TitleKorean = x.TitleKorean,
                                             ShortContentKorean = x.ShortContentKorean,
                                             ContentKorean = x.ContentKorean,

                                         });

            return new PagingResult<NewsViewModel>()
            {
                CurrentPage = searchRequest.Page,
                PageSize = searchRequest.PageSize,
                TotalItems = await news.CountAsync(),
                Data = await news.Skip((searchRequest.Page - 1) * searchRequest.PageSize).Take(searchRequest.PageSize).ToListAsync()
            };
        }
        catch
        {
            return new PagingResult<NewsViewModel>()
            {
                CurrentPage = searchRequest.Page,
                PageSize = searchRequest.PageSize,
                TotalItems = 0,
                Data = new List<NewsViewModel>()
            };
        }
    }

    public async Task CreateOrUpdate(NewsViewSetupModel request)
    {
        var news = await _context.News.Where(x => x.Id == request.Id && x.IsDelete != true).FirstOrDefaultAsync();
        if (news == null)
        {
            news = new News
            {
                CreatedAt = DateTime.Now
            };
        }
        
        news.Title = request.Title;
        news.ShortContent = request.ShortContent;
        news.Content = request.Content;
        news.UpdatedAt = DateTime.Now;
        news.Type = request.Type;
        news.CategoryId = request.CategoryId;
        news.Image = "";
        news.ImageEnglish = "";
        news.ImageKorean = "";
        var files = request.UploadedFiles ?? new List<FileDetailModel>();
        if (request.File != null && request.File.Any())
        {
            foreach (var file in request.File)
            {
                var fileUrl = _fileService.Upload(file, "News", file.FileName);
                files.Add(new FileDetailModel
                {
                    FileName = file.FileName,
                    FileUrl = fileUrl,
                });
            }
        }
        news.Image = JsonConvert.SerializeObject(files).ToString();
        news.ImageEnglish = JsonConvert.SerializeObject(files).ToString();
        news.ImageKorean = JsonConvert.SerializeObject(files).ToString();

        news.TitleEnglish = request.TitleEnglish;
        news.ShortContentEnglish = request.ShortContentEnglish;
        news.ContentEnglish = request.ContentEnglish;
        news.TitleKorean = request.TitleKorean;
        news.ShortContentKorean = request.ShortContentKorean;
        news.ContentKorean = request.ContentKorean;


        if (news.Id > 0)
            _context.News.Update(news);
        else
            await _context.News.AddAsync(news);
        
        await _context.SaveChangesAsync();
    }
}