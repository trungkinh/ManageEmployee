namespace ManageEmployee.Hanet;

public class HanetAPI
{
    //public static async Task<ApiResponses.Responses> GetTokenAsync()
    //{
    //    HttpClient client = new HttpClient();
    //    string json = null, token = null;
    //    var login = new ApiResponses.LoginRequest() { username = SessionVariables.sUsernameForApiAws, password = SessionVariables.sPasswordForApiAws };



    //    var response = await client.PostAsJsonAsync(baseUri + SuffixesApiURL.Return_Suffix_String(SuffixesApiURL.Suffixes.Login_Get_Token), login);
    //    response.EnsureSuccessStatusCode();
    //    if (response.IsSuccessStatusCode)
    //    {
    //        json = await response.Content.ReadAsStringAsync();
    //        token = JsonConvert.DeserializeObject<ApiResponses.Token>(json).token;
    //        SessionVariables.sTokenForApiAws = token;
    //        return new ApiResponses.Responses { Message = null };
    //    }
    //    else
    //        return new ApiResponses.Responses { Message = response.StatusCode.ToString() };
    //}
}
