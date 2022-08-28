
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types;
using Newtonsoft.Json;

namespace WpfBotNet
{
    class TelegramMessageClient
    {
        public static MainWindow window;
        public static ObservableCollection<MessageLog> BotMessageLog { get; set; }
        TelegramBotClient bot;

        public TelegramMessageClient(MainWindow W)
        {
            BotMessageLog = new ObservableCollection<MessageLog>();
            window = W;

            string token = System.IO.File.ReadAllText(@"C:\api_token.txt");
            var cts = new CancellationTokenSource();
            bot = new TelegramBotClient(token);

            var receiverOptions = new ReceiverOptions()
            {
                AllowedUpdates = Array.Empty<UpdateType>(),
                ThrowPendingUpdates = true,
            };

            bot.StartReceiving(updateHandler: UpdateHandlers.HandleUpdateAsync,
                               pollingErrorHandler: UpdateHandlers.PollingErrorHandler,
                               receiverOptions: receiverOptions,
                               cancellationToken: cts.Token);

            //Console.ReadLine();

            // Send cancellation request to stop bot
            //cts.Cancel();
        }

        public class UpdateHandlers
        {

            public static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
            {

                var handler = update.Type switch
                {

                    UpdateType.Message => BotOnMessageReceived(botClient, update.Message!),
                    UpdateType.EditedMessage => BotOnMessageReceived(botClient, update.EditedMessage!),
                    UpdateType.CallbackQuery => BotOnCallbackQueryReceived(botClient, update.CallbackQuery!),
                    _ => UnknownUpdateHandlerAsync(botClient, update)
                };

                try
                {
                    await handler;
                }
#pragma warning disable CA1031
                catch (Exception exception)
#pragma warning restore CA1031
                {
                    await PollingErrorHandler(botClient, exception, cancellationToken);
                }
            }
            public static Task PollingErrorHandler(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
            {
                var ErrorMessage = exception switch
                {
                    ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                    _ => exception.ToString()
                };

                Console.WriteLine(ErrorMessage);
                return Task.CompletedTask;
            }


            public  static async Task BotOnMessageReceived(ITelegramBotClient botClient, Message message)
            {
                Console.WriteLine($"Receive message type: {message.Type}");
                if (message.Type == MessageType.Document)
                {
                    DownLoad(message.Document.FileId, message.Document.FileName);
                    Message sentMessageDownload = await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: $"{message.Type} файл {message.Document.FileName} скачан");
                }
                else if (message.Type == MessageType.Photo)
                {
                    DownLoad(message.Photo.Last().FileId, $"Picture_{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}.jpg");
                    Message sentMessageDownload = await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: $"{message.Type} файл скачан");
                }
                else if (message.Type == MessageType.Audio)
                {
                    Message sentMessageDownload = await botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: $"{message.Type} не качаем, места стока нет");
                }

                if (message.Text is not { } messageText)
                    return;
                window.Dispatcher.Invoke(() =>
                {
                    BotMessageLog.Add(
                    new MessageLog(
                        DateTime.Now.ToLongTimeString(), messageText, message.Chat.FirstName, message.Chat.Id));
                });

                var action = messageText.Split(' ')[0] switch
                {
                    "/all" => SendInlineKeyboard(botClient, message),
                    "/start" => SendHello(botClient, message),
                    _ => Usage(botClient, message)
                };
                Message sentMessage = await action;
                Console.WriteLine($"The message was sent with id: {sentMessage.MessageId}");


                static async Task<Message> SendHello(ITelegramBotClient botClient, Message message)
                {
                    await botClient.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);

                    // Simulate longer running task
                    await Task.Delay(2500);
                    return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                        text: "Hi, let's try to save your files!\r\n" +
                                        "If you want download one of them - just type </all>  ",
                                        replyMarkup: new ReplyKeyboardRemove());
                }

                async void DownLoad(string fileId, string path)
                {
                    var file = await botClient.GetFileAsync(fileId);
                    FileStream fs = new FileStream(@"C:\HWDir\" + path, FileMode.Create);
                    await botClient.DownloadFileAsync(file.FilePath, fs);
                    fs.Close();

                    fs.Dispose();
                }

                // Send inline keyboard
                // You can process responses in BotOnCallbackQueryReceived handler
                static async Task<Message> SendInlineKeyboard(ITelegramBotClient botClient, Message message)
                {
                    await botClient.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);

                    // Simulate longer running task
                    await Task.Delay(500);
                    DirectoryInfo di = new(@"C:\HWDir");
                    int i = 0;
                    foreach (var fi in di.GetFiles())
                    {
                        i++;
                    }
                    string[] filesnemes = new string[i];
                    int a = 0;
                    foreach (var fi in di.GetFiles())
                    {
                        filesnemes[a] = fi.Name;
                        a++;
                    }
                    InlineKeyboardMarkup keyboardMarkup = new InlineKeyboardMarkup(GetInlineKeyboard(filesnemes));


                    return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                            text: "Choose doc",
                                                            replyMarkup: keyboardMarkup);
                }

                static InlineKeyboardButton[][] GetInlineKeyboard(string[] stringArray)
                {
                    var keyboardInline = new List<InlineKeyboardButton[]>();
                    for (var i = 0; i < stringArray.Length; i++)
                    {
                        keyboardInline.Add(
                            new[]
                            {
                                InlineKeyboardButton.WithCallbackData(text: stringArray[i],callbackData: $"{i}"),
                            }
                            );
                    }
                    return keyboardInline.ToArray();
                }


                static async Task<Message> Usage(ITelegramBotClient botClient, Message message)
                {
                    await botClient.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);

                    // Simulate longer running task
                    await Task.Delay(2500);
                    const string usage = "Usage:\n" +
                                         "/all   - send inline keyboard docs\n" +
                                         "/start   - information\n";

                    return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
                                                                text: usage,
                                                                replyMarkup: new ReplyKeyboardRemove());
                }
            }

            private static async Task BotOnCallbackQueryReceived(ITelegramBotClient botClient, CallbackQuery callbackQuery)
            {
                DirectoryInfo di = new(@"C:\HWDir");
                int d = 0;
                foreach (var file in di.GetFiles())
                {
                    if ($"{d}" == callbackQuery.Data)
                    {
                        using FileStream fileStream = new(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
                        await botClient.SendDocumentAsync(chatId: callbackQuery.Message!.Chat.Id,
                                                      document: new InputOnlineFile(fileStream, file.Name),
                                                      caption: "Nice choise");
                    }
                    d++;
                }
            }

            private static Task UnknownUpdateHandlerAsync(ITelegramBotClient botClient, Update update)
            {
                Console.WriteLine($"Unknown update type: {update.Type}");
                return Task.CompletedTask;
            }

        }

        public void SendMessage(string Text, string Id)
        {
            if (Id == "") return;
            long id = Convert.ToInt64(Id);
            bot.SendTextMessageAsync(id, Text);
            window.Dispatcher.Invoke(() =>
            {
                BotMessageLog.Add(
                new MessageLog(
                    DateTime.Now.ToLongTimeString(), Text, "ЕГО ВЕЛИЧЕСТВО БОТ", id));
            });
        }

        public void LoadJSON()
        {
            string stringJson = JsonConvert.SerializeObject(BotMessageLog);
            Debug.WriteLine(stringJson);
            System.IO.File.WriteAllText($"{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}message_log.json", stringJson);
              
        }
    }
}
   