using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Microsoft.Data.Sqlite;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

TelegramBotClient botClient = new("5153544287:AAEKYEMj9Z_5PBpW1yTnjkJ9eFM5O50evRc");
using CancellationTokenSource cts = new();

Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\photos");
Console.WriteLine(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\photos");

ReceiverOptions receiverOptions = new()
{
    AllowedUpdates = { }
};

botClient.StartReceiving(HandleUpdateAsync,
    HandleErrorAsync,
    receiverOptions,
    cancellationToken: cts.Token);

var me = await botClient.GetMeAsync();

Console.WriteLine($"работаем @{me.Username}");
Console.ReadLine();


cts.Cancel();


async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    if (update.Type == UpdateType.Message && update?.Message?.Text != null)
    {
        await HandleMessage(botClient, update.Message);
    }

    if (update.Type == UpdateType.CallbackQuery)
    {
        await HandleCallbackQuery(botClient, update.CallbackQuery);
    }
}

static int AddPhotoFromDoc()
{
    var pathTopDoc = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
    string pathToPhotos = pathTopDoc + "\\photos\\";

    var sqlExp = $"INSERT INTO photos (name) VALUES (@name)";

    using (var connection = new SqliteConnection($"Data Source={Environment.GetFolderPath(
        Environment.SpecialFolder.Desktop)}\\tg_bot.db"))
    {
        connection.Open();
        foreach (var name in Directory.GetFiles(pathToPhotos))
        {
            SqliteCommand command = new SqliteCommand(sqlExp, connection);

            command.Parameters.AddWithValue("name", $"{name}");
            command.ExecuteNonQuery();
        }
    }

    int fileCount = Directory.EnumerateFiles(pathToPhotos).Count();
    return fileCount;
}

static string? GetRandomPhotoFromDb()
{
    int fileCount = AddPhotoFromDoc();
    Random rnd = new();
    var id = rnd.Next(0, 10);
    var sqlExp = $"SELECT * from photos";

    using (var connection = new SqliteConnection($"Data Source={Environment.GetFolderPath(
        Environment.SpecialFolder.Desktop)}\\tg_bot.db"))
    {
        connection.Open();

        SqliteCommand command = new(sqlExp, connection);
        using (SqliteDataReader reader = command.ExecuteReader())
        {
            if (reader.HasRows)
            {
                String[] photos = new String[fileCount];
                int i = 0;
                while (reader.Read())
                {
                    string? path = reader.GetValue(0).ToString();
                    photos[i] = path;
                    i++;
                }

                return photos[id];
            }
        }
    }

    return "Error";
}


static void AddUserToDb(string id)
{
    var sqlExp = $"INSERT INTO users (id) VALUES (@id)";
    using (var connection = new SqliteConnection($"Data Source={Environment.GetFolderPath(
        Environment.SpecialFolder.Desktop)}\\tg_bot.db"))
    {
        connection.Open();
        SqliteCommand command = new SqliteCommand(sqlExp, connection);
        command.Parameters.AddWithValue("id", $"{id}");
        command.ExecuteNonQuery();
    }
}

async Task HandleMessage(ITelegramBotClient botClient, Message message)
{
    if (message.Text == "/start")
    {
        ReplyKeyboardMarkup replyKeyboardMarkupStart = new(new[]
        {
            new KeyboardButton[] { "Оценить пушистика🐱" },
        })
        {
            ResizeKeyboard = true
        };
        AddUserToDb(message.Chat.Username);
        await botClient.SendTextMessageAsync(message.Chat.Id, $"Добро пожаловать " +
                                                              $"{message.Chat.Username} в Rate Kitten!",
            replyMarkup: replyKeyboardMarkupStart);
        return;
    }

    if (message.Text == "Оценить пушистика🐱")
    {
        InlineKeyboardMarkup replyKeyboardMarkupGrade = new(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("1", "1"),
                InlineKeyboardButton.WithCallbackData("2", "2"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("3", "3"),
                InlineKeyboardButton.WithCallbackData("4", "4"),
                InlineKeyboardButton.WithCallbackData("5", "5"),
            }
        });

        using (var s = System.IO.File.OpenRead(GetRandomPhotoFromDb()))
        {
            await botClient.SendPhotoAsync(message.Chat.Id, new InputFile(s), replyMarkup: replyKeyboardMarkupGrade);
        }

        return;
    }
}


async Task HandleCallbackQuery(ITelegramBotClient botclient, CallbackQuery callbackQuery)
{
    switch (callbackQuery.Data)
    {
        case "1":
        {
            await botclient.SendTextMessageAsync(callbackQuery.Message.Chat.Id,
                "Жаль, что вам не понравился этот котёнок:(");
            break;
        }
        case "5":
        {
            await botclient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Да, он и вправду милый!");
            break;
        }
        case "Yes":
        {
            InlineKeyboardMarkup replyKeyboardMarkupGradev2 = new(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("1", "1"),
                    InlineKeyboardButton.WithCallbackData("2", "2"),
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("3", "3"),
                    InlineKeyboardButton.WithCallbackData("4", "4"),
                    InlineKeyboardButton.WithCallbackData("5", "5"),
                }
            });
            using (var s = System.IO.File.OpenRead(GetRandomPhotoFromDb()))
            {
                await botClient.SendPhotoAsync(callbackQuery.Message.Chat.Id, new InputFile(s), replyMarkup:
                    replyKeyboardMarkupGradev2);
            }

            break;
        }
        case "No":
        {
            await botclient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Как скажете, ждём снова!");
            return;
        }
    }

    InlineKeyboardMarkup replyKeyboardMarkupNext = new(new[]
    {
        InlineKeyboardButton.WithCallbackData("Да", "Yes"),
        InlineKeyboardButton.WithCallbackData("Нет", "No")
    });
    await botclient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Идём далее?",
        replyMarkup: replyKeyboardMarkupNext);
    return;
}


Task HandleErrorAsync(ITelegramBotClient client, Exception exception, CancellationToken cancellationToken)
{
    var error = exception switch
    {
        ApiRequestException apiRequestException =>
            $"Что-то произошло на стороне серверов TELEGRAM\n" +
            $"{apiRequestException.ErrorCode}\n" +
            $"{apiRequestException.Message}",
        _ => exception.ToString()
    };
    Console.WriteLine(error);
    return Task.CompletedTask;
}