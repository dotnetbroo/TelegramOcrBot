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
                Console.WriteLine("Xato: 'tessdata' papkasi topilmadi. Til fayllarini to'g'ri joylashtirganingizga ishonch hosil qiling.");
                Console.WriteLine("Visual Studio'da 'tessdata' papkasidagi har bir '.traineddata' faylini tanlab, ");
                Console.WriteLine("'Properties' oynasida 'Copy to Output Directory' xususiyatini 'Copy always' ga o'rnating.");
                return;
            }

            Languages = GetAllAvailableTesseractLanguages(TessDataPath);
            if (string.IsNullOrEmpty(Languages))
            {
                Console.WriteLine("Xato: 'tessdata' papkasida hech qanday '.traineddata' fayli topilmadi.");
                Console.WriteLine("Iltimos, kerakli til fayllarini yuklab oling va 'tessdata' papkasiga joylashtiring.");
                return;
            }
            Console.WriteLine($"Aniqlangan tillar: {Languages}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Dastlabki tekshiruvida xato: {ex.Message}");
            Console.WriteLine("Dastur to'xtatiladi.");
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
        Console.WriteLine($"Bot ishga tushdi: @{me.Username}");
        Console.WriteLine("Har qanday tugmani bosib botni to'xtatishingiz mumkin.");
        Console.ReadKey();

        cts.Cancel();
        Console.WriteLine("Bot to'xtatildi.");
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
                text: "Iltimos, matnni aniqlash uchun rasm yuboring.",
                cancellationToken: cancellationToken
            );
            return;
        }

        Console.WriteLine($"Rasmli xabar qabul qilindi. Chat ID: {message.Chat.Id}");

        var photo = photoArray[^1];
        var fileId = photo.FileId;

        try
        {
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "Rasmingizni qayta ishlayapman, iltimos kuting...",
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
                    Console.WriteLine($"Aniqangan matn (xom holatda):\n{recognizedText}");

                    recognizedText = recognizedText.Trim(); 
                    recognizedText = Regex.Replace(recognizedText, @"\n\s*\n", "\n\n");
                    recognizedText = Regex.Replace(recognizedText, @" {2,}", " ");


                    string formattedText = $"Rasmdagi matn:\n\n```\n{EscapeMarkdownV2(recognizedText)}\n```";


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
                        text: "Rasmdan matn topilmadi. Yoki rasmda matn yo'q, yoki u o'qish qiyin holatda.",
                        cancellationToken: cancellationToken
                    );
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Xabarni qayta ishlashda xato: {ex.Message}");
            await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: $"Rasmdagi matnni aniqlashda xatolik yuz berdi: {ex.Message}",
                cancellationToken: cancellationToken
            );
        }
    }

    static Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Polling xatosi: {exception.Message}");
        return Task.CompletedTask;
    }
}