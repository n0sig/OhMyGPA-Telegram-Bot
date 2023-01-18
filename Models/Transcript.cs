using System.Text;
using System.Text.Json;

namespace OhMyGPA.Telegram.Bot.Models;

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