using Discord;
using Discord.Audio;
using Discord.Commands;
using System.Diagnostics;
using System.Threading.Tasks;

public class GeminiModule : ModuleBase<SocketCommandContext>
{
    private readonly GeminiService _gemini;
    private readonly VoiceService _voiceService;

    public GeminiModule(GeminiService gemini, VoiceService voiceService)
    {
        _gemini = gemini;
        _voiceService = voiceService;
    }

    [Command("ask", RunMode = RunMode.Async)]
    [Summary("Gemini에게 질문하고 답을 받아옵니다.")]
    public async Task AskAsync([Remainder] string question)
    {
        var channel = (Context.User as IGuildUser)?.VoiceChannel;
        if (channel == null)
        {
            return;
        }

        try
        {
            _voiceService.AudioClient = await (Context.User as IVoiceState)?.VoiceChannel?.ConnectAsync();
        }
        catch (Exception ex)
        {
            await ReplyAsync($"오류 발생: {ex.Message}");
            Console.WriteLine(ex);
        }

        var answer = await _gemini.AskGeminiAsync(question, 1);
        var answerKo = await _gemini.AskGeminiAsync(answer, 2);
        await ReplyAsync(answerKo);

        if (_voiceService.AudioClient == null)
        {
            return;
        }

        var voiceId = 20;
        var client = new HttpClient();

        var queryResponse = await client.PostAsync(
            $"http://localhost:50021/audio_query?text={Uri.EscapeDataString(answer)}&speaker={voiceId}", null);
        var queryJson = await queryResponse.Content.ReadAsStringAsync();

        var synthesisResponse = await client.PostAsync(
            $"http://localhost:50021/synthesis?speaker={voiceId}",
            new StringContent(queryJson, System.Text.Encoding.UTF8, "application/json"));


        if (!synthesisResponse.IsSuccessStatusCode)
        {
            return;
        }

        var audioBytes = await synthesisResponse.Content.ReadAsByteArrayAsync();
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".wav");
        await File.WriteAllBytesAsync(tempPath, audioBytes);



        var fi = new FileInfo(tempPath);
        while (!fi.Exists || fi.Length == 0)
        {
            await Task.Delay(10);
            fi.Refresh();
        }

        using var ffmpeg = CreateStream(tempPath);

        var output = ffmpeg.StandardOutput.BaseStream;
        var discord = _voiceService.AudioClient.CreatePCMStream(AudioApplication.Mixed);

        try
        {
            await output.CopyToAsync(discord);

        }
        catch (Exception ex)
        {
            Console.WriteLine($"오디오 전송 중 오류: {ex.Message}");
        }
        finally
        {
            await discord.FlushAsync();        // 반드시 플러시
            await discord.DisposeAsync();
            ffmpeg.WaitForExit();             // 프로세스가 끝날 때까지 대기
            ffmpeg.Close();                   // 리소스 정리
            File.Delete(tempPath);            // 임시 파일 제거
        }

        if (_voiceService.AudioClient != null)
        {
            await _voiceService.AudioClient.StopAsync();
            _voiceService.AudioClient = null;
        }
    }
    private Process CreateStream(string path)
    {
        return Process.Start(new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-hide_banner -loglevel panic -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        });
    }
}
