using System;
using Discord;
using Discord.WebSocket;
using Discord.Commands;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using EliBot.Services;

namespace EliBot
{
    public class Program
    {
        /// <summary>
        /// Run the bot ascyn
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args) => new Program().RunBotAsync().GetAwaiter().GetResult();

        private DiscordSocketClient client;
        private CommandService commands;
        private IServiceProvider services;
        private AudioService audioService;

        /// <summary>
        /// Start up the bot and log it in to the discord server
        /// </summary>
        /// <returns></returns>
        public async Task RunBotAsync()
        {
            client = new DiscordSocketClient();
            commands = new CommandService();
            audioService = new AudioService(ref client);
            //Add appropiate singletons to the services
            services = new ServiceCollection().AddSingleton(client).AddSingleton(commands).AddSingleton(audioService).BuildServiceProvider();

            string botToken = ;

            //event subscriptions
            client.Log += Log;

            await RegisterCommandsAsync();

            await client.LoginAsync(TokenType.Bot, botToken);

            await client.StartAsync();

            await client.SetGameAsync("Nothing Playing");
            //Run forever
            await Task.Delay(-1);
        }

        //Log simple messages to the console
        private Task Log(LogMessage arg)
        {
            Console.WriteLine(arg);

            return Task.CompletedTask;
        }

        public async Task RegisterCommandsAsync()
        {
            client.MessageReceived += HandleCommandAsync;

            await commands.AddModulesAsync(Assembly.GetEntryAssembly());
        }

        /// <summary>
        /// Handle commands with the prefix eli! or when the bot is mentioned
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        private async Task HandleCommandAsync(SocketMessage arg)
        {
            var message = arg as SocketUserMessage;

            if (message is null || message.Author.IsBot)
            {
                return;
            }

            int argPos = 0;

            if (message.HasStringPrefix("eli!", ref argPos) || message.HasMentionPrefix(client.CurrentUser, ref argPos))
            {
                var context = new SocketCommandContext(client, message);

                var result = await commands.ExecuteAsync(context, argPos, services);

                if (!result.IsSuccess)
                {
                    Console.WriteLine(result.ErrorReason);
                }
            }
        }

    }
}
