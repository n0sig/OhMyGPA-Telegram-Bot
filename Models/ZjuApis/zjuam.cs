using System.Net;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ZjuApi;

public class ZjuAm
{
    //统一身份认证过程中用到的Uri
    private static readonly Uri AuthUri = new("https://zjuam.zju.edu.cn/cas/login");
    private static readonly Uri PubKeyUri = new("https://zjuam.zju.edu.cn/cas/v2/getPubKey");


    /*登录统一身份认证：必须有三个参数
     @param httpClient 用于发送请求并保存Cookies的HttpClient
     @param username: 学号
     @param password: 密码
    */
    public static async Task Login(HttpClient httpClient, string username, string password)
    {
        string execution, encryptedPassword;
        httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/108.0.0.0 Safari/537.36 Edg/108.0.1462.76");

        //获取登录表单中要填的‘execution’值
        using (var response = await httpClient.GetAsync(AuthUri))
        {
            execution = Regex.Match(await response.Content.ReadAsStringAsync(),
                    "name=\"execution\" value=\"(.*?)\"")
                .Groups[1]
                .Value;
            if (execution == "") throw new Exception("解析统一身份认证页面失败");
        }

        //密钥从服务器获取，对密码进行RSA加密
        using (var response = await httpClient.GetAsync(PubKeyUri))
        {
            string e, m;
            try
            {
                var key = JsonSerializer.Deserialize<Dictionary<string, string>>(
                    await response.Content.ReadAsStringAsync()) ?? new Dictionary<string, string>();
                e = key["exponent"];
                m = key["modulus"];
            }
            catch
            {
                throw new Exception("获取密钥失败");
            }

            encryptedPassword = RsaEncrypt(password, e, m);
        }

        //提交登录表单
        using (var response = await httpClient.PostAsync(AuthUri,
                   new FormUrlEncodedContent(new Dictionary<string, string>
                   {
                       { "username", username },
                       { "password", encryptedPassword },
                       { "execution", execution },
                       { "_eventId", "submit" }
                   })))
        {
            if ((await response.Content.ReadAsStringAsync()).Contains("统一身份认证")) throw new Exception("账号或密码错误");
        }
        
        static string RsaEncrypt(string password, string exponent, string modulus)
        {
            //字符串转换为BigInteger
            var pwdInt = new BigInteger(Encoding.UTF8.GetBytes(password), isBigEndian: true);
            var modInt = new BigInteger(Convert.FromHexString(modulus), true, true);
            var expInt = Convert.ToUInt64(exponent, 16);
            //由于采用No Padding，直接幂次取余后，转换为Hex即可得到密文（大小写不敏感）
            var pwdEncInt = BigInteger.ModPow(pwdInt, expInt, modInt);
            return Convert.ToHexString(pwdEncInt.ToByteArray(true, true));
        }
    }

    /*
     * 使用统一身份认证来获取Cookies(名称为iPlanetDirectoryPro)
     * @param username 学号
     * @param password 密码
     * @return 字符串"iPlanetDirectoryPro=..."
     */
    public static async Task<string> GetCookie(string username, string password)
    {
        var cookieContainer = new CookieContainer();
        var handler = new HttpClientHandler
        {
            CookieContainer = cookieContainer
        };
        var httpClient = new HttpClient(handler);

        // 进行统一身份认证，并获取Cookie iPlanetDirectoryPro
        try
        {
            await Login(httpClient, username, password);
            var iPlanetDirectoryPro = cookieContainer.GetAllCookies()["iPlanetDirectoryPro"];
            if (iPlanetDirectoryPro == null)
                throw new Exception("无法获取Cookie iPlanetDirectoryPro");
            return iPlanetDirectoryPro.Value;
        }
        catch (Exception e)
        {
            throw new Exception("登录统一身份认证失败，" + e.Message);
        }
    }
}