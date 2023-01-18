using System.Net;
using System.Text.RegularExpressions;

namespace ZjuApi;

public class Cjcx
{
    //页面和API的URI
    private static readonly Uri PageUri = new("https://appservice.zju.edu.cn/zdjw/cjcx/cjcxjg");
    private static readonly Uri ApiUri = new("https://appservice.zju.edu.cn/zju-smartcampus/zdydjw/api/kkqk_cxXscjxx");


    /*
     * 使用Cookies(iPlanetDirectoryPro)来获取成绩
     * @param wisportalId 登录后的Cookie
     * @return 成绩单
     */
    public static async Task<string> GetTranscript(string iPlanetDirectoryPro)
    {
        // 用Cookie请求API
        var cookieContainer = new CookieContainer();
        cookieContainer.Add(new Cookie("iPlanetDirectoryPro", iPlanetDirectoryPro, "/", "zju.edu.cn"));
        var handler = new HttpClientHandler
        {
            CookieContainer = cookieContainer
        };
        var httpClient = new HttpClient(handler);
        httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/108.0.0.0 Safari/537.36 Edg/108.0.1462.76");
        try
        {
            var redirects = 0;
            var response = await httpClient.GetAsync(PageUri);
            //C#的HttpClient不会自动进行HTTPS到HTTP的重定向，所以要手动重定向
            while (response.StatusCode == HttpStatusCode.Redirect)
            {
                if (redirects++ > 10)
                    throw new Exception();
                response = await httpClient.GetAsync(response.Headers.Location);
            }
        }
        catch
        {
            throw new Exception("访问成绩查询页面失败，可能是服务器故障");
        }

        try
        {
            var response = await httpClient.PostAsync(ApiUri, null);
            var cjcxJson = Regex.Match(await response.Content.ReadAsStringAsync(),
                "\"list\":(.*?)},\"").Groups[1].Value;
            if (cjcxJson == "")
                throw new Exception();
            return cjcxJson;
        }
        catch
        {
            throw new Exception("获取成绩单失败，可能是Cookie过期");
        }
    }


    /*
     * 使用统一身份认证来获取成绩
     * @param username 学号
     * @param password 密码
     * @return 成绩单
     */
    public static async Task<string> GetTranscript(string username, string password)
    {
        var iPlanetDirectoryPro = await ZjuAm.GetCookie(username, password);
        return await GetTranscript(iPlanetDirectoryPro);
    }
}