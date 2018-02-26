using System.Threading.Tasks;
using Discord.Commands;
using EliBot.Services;
using Discord;
using YoutubeSearch;

namespace EliBot.Modules
{
    /// <summary>
    /// Class that handles the commands for the audio part of the bot 
    /// </summary>
    public class AudioModule : ModuleBase<ICommandContext>
    {
        private readonly AudioService _service;

        static private IUserMessage choiceMessage;

        static private VideoInformation[] choices = new VideoInformation[5];

        private AudioModule(AudioService service)
        {
            _service = service;
        }

        /// <summary>
        /// Command to join the audio channel
        /// </summary>
        /// <returns>The join audio task</returns>
        [Command("join", RunMode = RunMode.Async)]
        public async Task JoinCmd()
        {
            await _service.JoinAudio(Context.Guild, (Context.User as IVoiceState).VoiceChannel);
        }

        /// <summary>
        /// Same as the join command just shorter hand
        /// </summary>
        /// <returns>The join audio task</returns>
        [Command("j", RunMode = RunMode.Async)]
        public async Task JCmd()
        {
            await _service.JoinAudio(Context.Guild, (Context.User as IVoiceState).VoiceChannel);
        }

        /// <summary>
        /// Leave command, leave the audio channel
        /// </summary>
        /// <returns></returns>
        [Command("leave", RunMode = RunMode.Async)]
        public async Task LeaveCmd()
        {
            await _service.LeaveAudio(Context.Guild);
        }

        /// <summary>
        /// Play a song from a youtube url or search string
        /// </summary>
        /// <param name="song">The url or the search term</param>
        /// <returns></returns>
        [Command("play", RunMode = RunMode.Async)]
        public async Task PlayCmd([Remainder] string song)
        {
            //Join the audio if not in it already
            if (!_service.joined) {
                await _service.JoinAudio(Context.Guild, (Context.User as IVoiceState).VoiceChannel);
            }

            //If the song is a youtube link and not part of a playlist
            if (song.Contains("www.youtube.com/watch?v=") && !song.Contains("&list="))
            {
                await _service.SendLinkAsync(Context.Guild, Context.Channel, song, Context.Message.Author);
            } else if (song.Contains("&list="))
            {
                //Just get the song url not with the list
                int listIndex = song.IndexOf("&list=");
                int indexIndex = song.IndexOf("&index=");

                string JustSong = (listIndex < indexIndex) ? song.Substring(0, listIndex) : song.Substring(0, indexIndex);

                await _service.SendLinkAsync(Context.Guild, Context.Channel, JustSong, Context.Message.Author);
            }
            else
            {
                //Otherwise do a search for the string
                var items = new VideoSearch();
                int i = 1;
                string list = "";
                foreach (var item in items.SearchQuery(song, 1))
                {
                    //List the first five results from the youtube search
                    list += $"{i}: {item.Title} ({item.Duration})\n";
                    
                    choices[i - 1] = item;

                    if (i == 5)
                    {
                        break;
                    }

                    i++;
                }
                choiceMessage = await Context.Channel.SendMessageAsync(list);
            }
        }

        /// <summary>
        /// Same as play command just shorter hand
        /// </summary>
        /// <param name="song"></param>
        /// <returns></returns>
        [Command("p", RunMode = RunMode.Async)]
        public async Task PCmd([Remainder] string song)
        {

            if (!_service.joined)
            {
                await _service.JoinAudio(Context.Guild, (Context.User as IVoiceState).VoiceChannel);
            }

            if (song.Contains("www.youtube.com/watch?v=") && !song.Contains("&list="))
            {
                await _service.SendLinkAsync(Context.Guild, Context.Channel, song, Context.Message.Author);
            }
            else if (song.Contains("&list="))
            {
                int listIndex = song.IndexOf("&list=");
                int indexIndex = song.IndexOf("&index=");

                string JustSong = (listIndex < indexIndex) ? song.Substring(0, listIndex) : song.Substring(0, indexIndex);

                await _service.SendLinkAsync(Context.Guild, Context.Channel, JustSong, Context.Message.Author);
            }
            else
            {
                var items = new VideoSearch();
                int i = 1;
                string list = "";
                foreach (var item in items.SearchQuery(song, 1))
                {
                    list += $"{i}: {item.Title} ({item.Duration})\n";

                    choices[i - 1] = item;

                    if (i == 5)
                    {
                        break;
                    }

                    i++;
                }
                choiceMessage = await Context.Channel.SendMessageAsync(list);
            }
        }

        /// <summary>
        /// Play command for choosing a song after a search
        /// </summary>
        /// <param name="choice"></param>
        /// <returns></returns>
        [Command("play", RunMode = RunMode.Async)]
        public async Task PlayCmd([Remainder] int choice)
        {
            //Make sure choice is in the valid range
            if (choice >= 1 && choice <= 5 && choices[choice - 1] != null)
            {
                //Modify the choice message saying what choice has been selected and then send the link
                await choiceMessage.ModifyAsync(msg => msg.Content = $"Song #{choice} has been selected: {choices[choice - 1].Title} ({choices[choice - 1].Duration})");
                await _service.SendLinkAsync(Context.Guild, Context.Channel, choices[choice - 1].Url, Context.Message.Author);
               

                for (int i = 0; i < 5; i++)
                {
                    choices[i] = null;
                }

            } else
            {
                await Context.Channel.SendMessageAsync("Not a valid choice");
            }
        }

        /// <summary>
        /// Same as play with int just shorter hand
        /// </summary>
        /// <param name="choice"></param>
        /// <returns></returns>
        [Command("p", RunMode = RunMode.Async)]
        public async Task PCmd([Remainder] int choice)
        {
            if (choice >= 1 && choice <= 5 && choices[choice - 1] != null)
            {
                await choiceMessage.ModifyAsync(msg => msg.Content = $"Song #{choice} has been selected: {choices[choice - 1].Title} ({choices[choice - 1].Duration})");
                await _service.SendLinkAsync(Context.Guild, Context.Channel, choices[choice - 1].Url, Context.Message.Author);


                for (int i = 0; i < 5; i++)
                {
                    choices[i] = null;
                }

            }
            else
            {
                await Context.Channel.SendMessageAsync("Not a valid choice");
            }
        }

        /// <summary>
        /// Clears the queue
        /// </summary>
        /// <returns></returns>
        [Command("clear", RunMode = RunMode.Async)]
        public async Task ClearQueueCmd()
        {
            await _service.ClearQueue(Context.Channel);
        }

        /// <summary>
        /// Restarts the current song
        /// </summary>
        /// <returns></returns>
        [Command("restart", RunMode = RunMode.Async)]
        public async Task RestartCmd()
        {
            await _service.RestartAudio(Context.Channel);
        }

        /// <summary>
        /// Skips the current song
        /// </summary>
        /// <returns></returns>
        [Command("skip", RunMode = RunMode.Async)]
        public async Task SkipCmd()
        {
            await _service.Skip(Context.Channel);
        }

        /// <summary>
        /// Skips the number of songs
        /// </summary>
        /// <param name="numTracks">Number of songs to skip</param>
        /// <returns></returns>
        [Command("skip", RunMode = RunMode.Async)]
        public async Task SkipCmd([Remainder] int numTracks)
        {
            await _service.SkipNum(Context.Channel, numTracks);
        }

        /// <summary>
        /// Skips the current song
        /// </summary>
        /// <returns></returns>
        [Command("s", RunMode = RunMode.Async)]
        public async Task SCmd()
        {
            await _service.Skip(Context.Channel);
        }

        /// <summary>
        /// Pauses the current song
        /// </summary>
        /// <returns></returns>
        [Command("pause", RunMode = RunMode.Async)]
        public async Task PauseCmd()
        {
            await _service.PauseAudio(Context.Channel);
        }

        /// <summary>
        /// Prints out the current queue of songs
        /// </summary>
        /// <returns></returns>
        [Command("queue", RunMode = RunMode.Async)]
        public async Task ShowQueueCmd()
        {
            await _service.DisplayQueue(Context.Channel);
        }

        /// <summary>
        /// Prints out the current queue of songs
        /// </summary>
        /// <returns></returns>
        [Command("q", RunMode = RunMode.Async)]
        public async Task ShowQCmd()
        {
            await _service.DisplayQueue(Context.Channel);
        }

        /// <summary>
        /// Stops the audio service
        /// </summary>
        /// <returns></returns>
        [Command("stop", RunMode = RunMode.Async)]
        public async Task StopCmd()
        {
            await _service.StopAudio(Context.Guild);
        }

        /// <summary>
        /// Stops the audio service
        /// </summary>
        /// <returns></returns>
        [Command("st", RunMode = RunMode.Async)]
        public async Task StCmd()
        {
            await _service.StopAudio(Context.Guild);
        }

        /// <summary>
        /// Prints the current song playing
        /// </summary>
        /// <returns></returns>
        [Command("nowplaying", RunMode = RunMode.Async)]
        public async Task NowPlayingCmd()
        {
            await _service.DisplayNowPlaying(Context.Channel);
        }

        /// <summary>
        /// Prints the current song playing
        /// </summary>
        /// <returns></returns>
        [Command("np", RunMode = RunMode.Async)]
        public async Task NPCmd()
        {
            await _service.DisplayNowPlaying(Context.Channel);
        }
    }
}