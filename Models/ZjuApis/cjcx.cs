using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ZjuApi;

public class Transcript
{
    public Transcript(string cjcxJson)
    {
        var curriculum = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(cjcxJson) ??
                         new List<Dictionary<string, object>>();

        var sumJd = 0m;
        var pFCount = 0m;
        var sumJdThisTerm = 0m;
        var pFCountThisTerm = 0m;
        var thisYear = Convert.ToString(curriculum[0]["xn"]);
        var thisSeme = Convert.ToString(curriculum[0]["xq"]) == "春夏" ||
                       Convert.ToString(curriculum[0]["xq"]) == "春" ||
                       Convert.ToString(curriculum[0]["xq"]) == "夏";

        foreach (var subject in curriculum)
        {
            var xn = Convert.ToString(subject["xn"]);
            var xq = Convert.ToString(subject["xq"]) == "春夏" ||
                     Convert.ToString(subject["xq"]) == "春" ||
                     Convert.ToString(subject["xq"]) == "夏";
            var mc = Convert.ToString(subject["kcmc"]);
            var cj = Convert.ToString(subject["cj"]);
            var xf = Convert.ToDecimal(Convert.ToString(subject["xf"]));
            var jd = Convert.ToDecimal(Convert.ToString(subject["jd"]));

            if (cj != "弃修")
            {
                Credit += xf;
                CreditThisTerm += xn == thisYear && xq == thisSeme ? xf : 0;
                if (mc != "英语水平测试" && mc != "形式与政策II")
                {
                    sumJd += jd * xf;
                    sumJdThisTerm += xn == thisYear && xq == thisSeme ? jd * xf : 0;
                }
                else
                {
                    pFCount++;
                    pFCountThisTerm += xn == thisYear && xq == thisSeme ? 1 : 0;
                }
            }
        }

        if (Credit - pFCount > 0)
            Gpa = sumJd / (Credit - pFCount);
        else Gpa = 0;
        if (CreditThisTerm - pFCountThisTerm > 0)
            GpaThisTerm = sumJdThisTerm / (CreditThisTerm - pFCountThisTerm);
        else GpaThisTerm = 0;

        CourseCount = curriculum.Count;
    }

    private decimal Gpa { get; }
    private decimal Credit { get; }
    private decimal GpaThisTerm { get; }
    private decimal CreditThisTerm { get; }
    public int CourseCount { get; }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"均绩: {Gpa:F3}");
        sb.AppendLine($"学期均绩: {GpaThisTerm:F3}");
        sb.AppendLine($"学分: {Credit}");
        sb.AppendLine($"学期学分: {CreditThisTerm}");
        return sb.ToString();
    }
}

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
    public static async Task<string> GetTranscriptJson(string iPlanetDirectoryPro)
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
    public static async Task<string> GetTranscriptJson(string username, string password)
    {
        var iPlanetDirectoryPro = await ZjuAm.GetCookie(username, password);
        return await GetTranscriptJson(iPlanetDirectoryPro);
    }
    
    public static async Task<Transcript> GetTranscript(string iPlanetDirectoryPro)
    {
        var cjcxJson = await GetTranscriptJson(iPlanetDirectoryPro);
        return new Transcript(cjcxJson);
    }
    
    public static async Task<Transcript> GetTranscript(string username, string password)
    {
        var cjcxJson = await GetTranscriptJson(username, password);
        return new Transcript(cjcxJson);
    }
}