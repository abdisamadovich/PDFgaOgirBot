using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

class Program
{
    private static ITelegramBotClient botClient;
    private static readonly HttpClient httpClient = new HttpClient();

    static async Task Main(string[] args)
    {
        botClient = new TelegramBotClient("7293731022:AAEMmDPiSEk8vPEXdueeMGuYlXxtbSYIW40");

        using var cts = new CancellationTokenSource();

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message }
        };

        botClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions,
            cts.Token
        );

        Console.WriteLine("Bot ishga tushdi. Chiqish uchun Ctrl+C bosing...");
        Console.ReadLine();
        cts.Cancel();
    }

    private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Type == UpdateType.Message)
        {
            var message = update.Message;

            if (message.Text == "/start")
            {
                await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "Salom! Menga rasm yuboring, men uni PDF ga aylantiraman.",
                    cancellationToken: cancellationToken
                );
                return;
            }

            if (message.Type == MessageType.Photo)
            {
                try
                {
                    // Rasmni olish (eng katta o‘lchamdagi rasmni tanlaymiz)
                    var photo = message.Photo[^1];
                    var fileId = photo.FileId;
                    var fileInfo = await botClient.GetFileAsync(fileId, cancellationToken);

                    // Faylni yuklab olish (eski usul)
                    var fileStream = new MemoryStream();
                    string fileUrl = $"https://api.telegram.org/file/bot{botClient.BotId}/{fileInfo.FilePath}";
                    var response = await httpClient.GetAsync(fileUrl, cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                        await stream.CopyToAsync(fileStream, cancellationToken);
                    }
                    fileStream.Position = 0;

                    // Rasmni PDF ga aylantirish
                    string pdfFilePath = await ConvertImageToPdf(fileStream);

                    // PDF ni foydalanuvchiga yuborish
                    using var pdfStream = new FileStream(pdfFilePath, FileMode.Open, FileAccess.Read);
                    await botClient.SendDocumentAsync(
                        chatId: message.Chat.Id,
                        document: new InputFileStream(pdfStream, "image.pdf"),
                        caption: "Mana sizning PDF faylingiz!",
                        cancellationToken: cancellationToken
                    );

                    // Vaqtinchalik PDF faylni o‘chirish
                    System.IO.File.Delete(pdfFilePath); // Aniq namespace ko‘rsatildi
                }
                catch (Exception ex)
                {
                    await botClient.SendTextMessageAsync(
                        chatId: message.Chat.Id,
                        text: $"Xato yuz berdi: {ex.Message}",
                        cancellationToken: cancellationToken
                    );
                }
            }
            else
            {
                await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "Iltimos, rasm yuboring!",
                    cancellationToken: cancellationToken
                );
            }
        }
    }

    private static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Xato yuz berdi: {exception.Message}");
        return Task.CompletedTask;
    }

    private static async Task<string> ConvertImageToPdf(Stream imageStream)
    {
        // PDF hujjatini yaratish
        using var document = new PdfDocument();
        var page = document.AddPage();

        // Rasmni XImage sifatida yuklash
        using var xGraphics = XGraphics.FromPdfPage(page);
        using var memoryStream = new MemoryStream();
        await imageStream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        // XImage.FromStream ga to‘g‘ri Stream uzatish
        using var xImage = XImage.FromStream(memoryStream);

        // Rasm o‘lchamlarini moslashtirish
        double pageWidth = page.Width;
        double pageHeight = page.Height;
        double imageWidth = xImage.PixelWidth;
        double imageHeight = xImage.PixelHeight;

        // Rasmni sahifaga moslashtirish (proporsiyani saqlab)
        double scale = Math.Min(pageWidth / imageWidth, pageHeight / imageHeight);
        double scaledWidth = imageWidth * scale;
        double scaledHeight = imageHeight * scale;

        // Rasmni sahifa markaziga joylashtirish
        double x = (pageWidth - scaledWidth) / 2;
        double y = (pageHeight - scaledHeight) / 2;

        xGraphics.DrawImage(xImage, x, y, scaledWidth, scaledHeight);

        // PDF ni vaqtinchalik fayl sifatida saqlash
        string pdfFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pdf");
        document.Save(pdfFilePath);

        return pdfFilePath;
    }
}