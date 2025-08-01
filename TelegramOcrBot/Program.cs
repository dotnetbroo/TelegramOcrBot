using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Tesseract; 

class Program
{
    private static readonly string BotToken = "YOUR_TELEGRAM_BOT_TOKEN_HERE";

    private static readonly string TessDataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");

    private static string Languages = "uzb+eng+rus+uzb_cyrl";

    static async Task Main(string[] args)
    {
        try
        {
            Console.WriteLine($"Tessdata yo'li: {TessDataPath}");
            if (!Directory.Exists(TessDataPath))
            {
                Console.WriteLine("Error: 'tessdata' folder not found. Make sure you have placed the language files correctly.");
                Console.WriteLine("In Visual Studio, select each '.traineddata' file in the 'tessdata' folder,");
                Console.WriteLine("In the 'Properties' window, set the 'Copy to Output Directory' property to 'Copy always'.");
                return;
            }

            Languages = GetAllAvailableTesseractLanguages(TessDataPath);
            if (string.IsNullOrEmpty(Languages))
            {
                Console.WriteLine("Error: No '.traineddata' file found in the 'tessdata' folder.");
                return;
            }
            Console.WriteLine($"Detected languages:{Languages}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in initial check: {ex.Message}");
            Console.WriteLine("The program will be stopped.");
            return;
        }

        var botClient = new TelegramBotClient(BotToken);

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message }
        };

        using var cts = new CancellationTokenSource();

        botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            pollingErrorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: cts.Token
        );

        var me = await botClient.GetMeAsync();
        Console.WriteLine($"Bot started: @{me.Username}");
        Console.WriteLine("You can stop the bot by pressing any button.");
        Console.ReadKey();

        cts.Cancel();
        Console.WriteLine("The bot has been stopped.");
    }

    private static string GetAllAvailableTesseractLanguages(string tessdataPath)
    {
        var languageFiles = Directory.GetFiles(tessdataPath, "*.traineddata");
        var languages = languageFiles
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => !string.IsNullOrEmpty(name))
            .Distinct()
            .ToList();

        return string.Join("+", languages);
    }

    private static string EscapeMarkdownV2(string text)
    {
        var escapedText = new Regex(@"([_\[\]()~`>#+\-=|{}.!])").Replace(text, @"\$1");
        escapedText = escapedText.Replace("*", "\\*");

        return escapedText;
    }


    static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is not { } message)
            return;

        if (message.Photo is not { } photoArray)
        {
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "Please send a picture to identify the text.",
                cancellationToken: cancellationToken
            );
            return;
        }

        Console.WriteLine($"Picture message received. Chat ID: {message.Chat.Id}");

        var photo = photoArray[^1];
        var fileId = photo.FileId;

        try
        {
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "I'm processing your image, please wait...",
                cancellationToken: cancellationToken
            );

            using (var memoryStream = new MemoryStream())
            {
                var fileInfo = await botClient.GetFileAsync(fileId, cancellationToken: cancellationToken);
                await botClient.DownloadFileAsync(fileInfo.FilePath!, memoryStream, cancellationToken: cancellationToken);
                memoryStream.Position = 0;

                string recognizedText = string.Empty;
                using (var ocrEngine = new TesseractEngine(TessDataPath, Languages, EngineMode.Default))
                {
                    using (var pix = Pix.LoadFromMemory(memoryStream.ToArray()))
                    {
                        using (var page = ocrEngine.Process(pix))
                        {
                            recognizedText = page.GetText();
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(recognizedText))
                {
                    Console.WriteLine($"Defined text (xom holatda):\n{recognizedText}");

                    recognizedText = recognizedText.Trim(); 
                    recognizedText = Regex.Replace(recognizedText, @"\n\s*\n", "\n\n");
                    recognizedText = Regex.Replace(recognizedText, @" {2,}", " ");


                    string formattedText = $"Text in the picture:\n\n```\n{EscapeMarkdownV2(recognizedText)}\n```";


                    await botClient.SendTextMessageAsync(
                        chatId: message.Chat.Id,
                        text: formattedText,
                        parseMode: ParseMode.MarkdownV2, 
                        cancellationToken: cancellationToken
                    );
                }
                else
                {
                    await botClient.SendTextMessageAsync(
                        chatId: message.Chat.Id,
                        text: "No text found in the image. Either the image has no text, or it is difficult to read.",
                        cancellationToken: cancellationToken
                    );
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing message: {ex.Message}");
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: $"There was an error recognizing the text in the image: {ex.Message}",
                cancellationToken: cancellationToken
            );
        }
    }

    static Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Polling error: {exception.Message}");
        return Task.CompletedTask;
    }
}
