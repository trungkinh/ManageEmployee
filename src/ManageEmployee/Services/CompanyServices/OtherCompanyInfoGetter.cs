using ManageEmployee.DataTransferObject.CompanyModels;
using ManageEmployee.Helpers;
using ManageEmployee.Services.Interfaces.Companies;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Text;

namespace ManageEmployee.Services.CompanyServices
{
    public class OtherCompanyInfoGetter: IOtherCompanyInfoGetter
    {
        private readonly AppSettingOtherCompany _appSettingOtherCompany;

        public OtherCompanyInfoGetter(IOptions<AppSettingOtherCompany> appSettingOtherCompany)
        {
            _appSettingOtherCompany = appSettingOtherCompany.Value;
        }

        public async Task<OtherCompanyInfomationModel> GetInforCompany(string taxCode)
        {
            using (var client = new HttpClient())
            {
                var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_appSettingOtherCompany.UserName}:{_appSettingOtherCompany.Password}"));

                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);
                var baseUrl = $"{_appSettingOtherCompany.Endpoint}?mst={taxCode}";

                var response = await client.GetAsync(baseUrl);

                try
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    OtherCompanyInfomationModel result = JsonConvert.DeserializeObject<OtherCompanyInfomationModel>(responseString);

                    if (!result.Success)
                    {
                        throw new ErrorException(result.Message);
                    }
                    return result;
                }
                catch (Exception ex)
                {
                    throw new ErrorException(ex.Message);
                }
            }
        }
    }
}
