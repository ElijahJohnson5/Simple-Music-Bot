using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;
using Discord;
using Discord.Audio;
using System.Collections.Generic;
using YoutubeSearch;
using System;
using Discord.WebSocket;

namespace EliBot.Services
{
    /// <summary>
    /// Class that handles the audio of the bot
    /// </summary>
    class AudioService
    {
        public static IAudioClient client;
        public bool joined = false;


        public TaskCompletionSource<bool> isSongPlaying = new TaskCompletionSource<bool>();
        bool songPlaying = false;
        //Queue for songs
        private Queue<Tuple<string, string, string, string, IUser>> queue = new Queue<Tuple<string, string, string, string, IUser>>();
        private bool pause = false;
        private bool restart = false;
        private bool exit = false;
        //Next song to play
        public Tuple<string, string, string, string, IUser> NextSong;
        public Tuple<string, string, string, string, IUser> CurrentSong;
        private readonly ConcurrentDictionary<ulong, IAudioClient> ConnectedChannels = new ConcurrentDictionary<ulong, IAudioClient>();
        public DiscordSocketClient socketClient;

        public AudioService(ref DiscordSocketClient socketClient)
        {
            this.socketClient = socketClient;
        }

        /// <summary>
        /// Join the audio channel the user who does this command is in
        /// </summary>
        /// <param name="guild"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public async Task JoinAudio(IGuild guild, IVoiceChannel target)
        {
            if (ConnectedChannels.TryGetValue(guild.Id, out client))
            {
                return;
            }
            if (target.Guild.Id != guild.Id)
            {
                return;
            }

            var audioClient = await target.ConnectAsync();

            if (ConnectedChannels.TryAdd(guild.Id, audioClient))
            {
                joined = true;
            }
        }

        /// <summary>
        /// Displays the song that is currently playing
        /// </summary>
        /// <param name="channel">The message channel this command is used from</param>
        /// <returns></returns>
        public async Task DisplayNowPlaying(IMessageChannel channel)
        {
            EmbedBuilder songs = new EmbedBuilder();
            songs.WithTitle(CurrentSong.Item2);
            songs.AddField("Duration", CurrentSong.Item3);
            //Create a new embed and send it in the chat
            await channel.SendMessageAsync("", false, songs.Build());
        }

        /// <summary>
        /// Leave the channel the bot is currently in
        /// </summary>
        /// <param name="guild">The guild the bot is in</param>
        /// <returns></returns>
        public async Task LeaveAudio(IGuild guild)
        {
            if (ConnectedChannels.TryRemove(guild.Id, out client))
            {
                await client.StopAsync();
                joined = false;
            }
        }

        /// <summary>
        /// Stop the audio the bot is currently playing and leaves the channel
        /// </summary>
        /// <param name="guild">The channel the bot is in</param>
        /// <returns></returns>
        public async Task StopAudio(IGuild guild)
        {
            if (ConnectedChannels.TryRemove(guild.Id, out client))
            {
                await client.StopAsync();
                joined = false;
            }
        }

        /// <summary>
        /// Show the current queue in the channel with thumbnails and in an embed
        /// </summary>
        /// <param name="channel">The message channel the command was called from</param>
        /// <returns></returns>
        public async Task DisplayQueue(IMessageChannel channel)
        {
            if (queue.Count >= 1)
            {
                //Create a title
                EmbedBuilder embed = new EmbedBuilder();
                embed.WithTitle("Song Playlist");
                embed.WithColor(Color.Red);
                //Send the title
                await channel.SendMessageAsync("", false, embed.Build());

                var queueAsArray = queue.ToArray();
                //Build embed with all of the songs with duration title thumbnail and author
                for (int i = 0; i < queueAsArray.Length; i++)
                {
                    EmbedBuilder songs = new EmbedBuilder();
                    songs.AddField("Song", queueAsArray[i].Item2);
                    songs.AddField("Duration", queueAsArray[i].Item3);
                    songs.WithThumbnailUrl(queueAsArray[i].Item4);
                    songs.WithAuthor(queueAsArray[i].Item5);

                    await channel.SendMessageAsync("", false, songs.Build());
                }
            }
        }

        /// <summary>
        /// Restart the current song
        /// sets restart flag to true
        /// </summary>
        /// <param name="channel">The channel the command was called from</param>
        /// <returns></returns>
        public async Task RestartAudio(IMessageChannel channel)
        {
            restart = true;
            await channel.SendMessageAsync($"{CurrentSong.Item2} has been restarted");
        }

        /// <summary>
        /// Skip the current song
        /// sets exit flag to true
        /// </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        public async Task Skip(IMessageChannel channel)
        {
            exit = true;
            await channel.SendMessageAsync($"{CurrentSong.Item2} has been skipped");
        }

        /// <summary>
        /// Skips as many songs as num
        /// </summary>
        /// <param name="channel">The message channel the command was called from</param>
        /// <param name="num">The number of songs to skip</param>
        /// <returns></returns>
        public async Task SkipNum(IMessageChannel channel, int num)
        {
            for (int i = 0; i < num; i++)
            {
                await Skip(channel);
            }
        }

        /// <summary>
        /// Clears the queue of all songs
        /// </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        public async Task ClearQueue(IMessageChannel channel)
        {
            queue.Clear();
            await channel.SendMessageAsync("Queue has been cleared");
        }

        /// <summary>
        /// Pauses or unpauses the audio
        /// </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        public async Task PauseAudio(IMessageChannel channel)
        {
            pause = !pause;
            await channel.SendMessageAsync($"{CurrentSong.Item2} has been " + ((pause) ? "paused" : "unpaused"));
        }

        /// <summary>
        /// Gets a song from a user and plays it
        /// </summary>
        /// <param name="guild"></param>
        /// <param name="channel"></param>
        /// <param name="path">The path to the song (youtube url)</param>
        /// <param name="author">The person requesting the song</param>
        /// <returns></returns>
        public async Task SendLinkAsync(IGuild guild, IMessageChannel channel, string path, IUser author)
        {
            if (ConnectedChannels.TryGetValue(guild.Id, out client))
            {
                Tuple<string, string, string, string, IUser> VideoData = null;

                var items = new VideoSearch();

                var item = items.SearchQuery(path, 1)[0];

                VideoData = new Tuple<string, string, string, string, IUser>(path, item.Title, item.Duration, item.Thumbnail, author);
                    
                queue.Enqueue(VideoData);
                if (songPlaying)
                {
                    await channel.SendMessageAsync($"{VideoData.Item2} has been added to the queue");

                    if (queue.Count == 1)
                    {
                        PlaySong(channel);
                    }
                } else
                {
                    songPlaying = true;
                    isSongPlaying.SetResult(true);
                    PlaySong(channel);
                }
            }
        }

        /// <summary>
        /// Plays songs from the queue until it is empty
        /// </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        private async void PlaySong(IMessageChannel channel)
        {
            while (queue.Count >= 1)
            {
                NextSong = queue.Peek();
                    

                System.IO.Stream stream = CreateLinkStream(NextSong.Item1).StandardOutput.BaseStream;

                //Wait for the last song to finish before playing the next song
                await isSongPlaying.Task;

                await channel.SendMessageAsync($"Now playing: {NextSong.Item2}\n");

                await socketClient.SetGameAsync(NextSong.Item2, NextSong.Item1);

                CurrentSong = NextSong;
                queue.Dequeue();
                SendAudio(stream);
                
            }

            if (!songPlaying)
            {
                await socketClient.SetGameAsync("Nothing Playing");
            }
        }

        /// <summary>
        /// Sends the audio to the output some bytes at a time
        /// </summary>
        /// <param name="path">The path to the song (youtube url)</param>
        /// <returns></returns>
        private async void SendAudio(System.IO.Stream path)
        {
            var output = path;
            var stream = client.CreatePCMStream(AudioApplication.Music);
            int bufferSize = 4096; //Play around with this value
            int bytesSent = 0;
            byte[] buffer = new byte[bufferSize];
            songPlaying = true;
            isSongPlaying = new TaskCompletionSource<bool>();

            while (!exit)
            {
                try
                {
                    if (restart)
                    {
                        //Reset the output and the stream if it needs to be restarted
                        output = CreateLinkStream(CurrentSong.Item1).StandardOutput.BaseStream;
                        stream = client.CreatePCMStream(AudioApplication.Music);
                        bytesSent = 0;
                        restart = false;
                    }

                    //Read from the output up to buffersize into the buffer
                    int read = await output.ReadAsync(buffer, 0, bufferSize);
                    if (read == 0)
                    {
                        exit = true;
                        break;
                    }

                    //Write into the stream to play the music
                    await stream.WriteAsync(buffer, 0, read);

                    //Empty loop for pausing the music
                    while (pause) ;

                    bytesSent += read;
                }
                catch (TaskCanceledException)
                {
                    exit = true;
                }
                catch
                {
                }
            }
            //Flush the stream
            await stream.FlushAsync();

            exit = false;
            isSongPlaying.SetResult(true);
        }

        /// <summary>
        /// Creates a stream from a link that is used to stream the music
        /// </summary>
        /// <param name="url">The youtube url for youtude-dl.exe to use</param>
        /// <returns>The youtube-dl process</returns>
        private Process CreateLinkStream(string url)
        {
            Process currentsong = new Process();

            currentsong.StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C youtube-dl.exe --no-playlist -o - {url} | ffmpeg -i pipe:0 -ac 2 -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            currentsong.Start();
            return currentsong;
        }
    }
}
