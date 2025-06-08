using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

public class GeminiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public GeminiService(string apiKey)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient();
    }

    public async Task<string> AskGeminiAsync(string userInput, int lang)
    {
       var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={_apiKey}";

        var requestBody = new
        {
            contents = new[]
            {
                new {
                    parts = new[] {
                        new { text = (lang == 1 ? "(日本語で答えてください)" : "(이 문장을 한국어로 번역해주세요.)") + userInput }
                    }
                }
            }
        };

        var json = JsonConvert.SerializeObject(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(url, content);
        var responseJson = await response.Content.ReadAsStringAsync();

        dynamic result = JsonConvert.DeserializeObject(responseJson);
        try
        {
            return result.candidates[0].content.parts[0].text.ToString();
        }
        catch(Exception ex)
        {
            Console.WriteLine(ex);
            return "Gemini로부터 응답을 받는 데 실패했어요.";
        }
    }
}
