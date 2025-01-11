using System.Text.Json;
using Discord;
using Discord.WebSocket;

namespace NameChangeBot {
    public class Program
    {
        private static Dictionary<ulong, List<string>> iceDwellers;

        private static DiscordSocketClient client;

        private static SocketGuild guild;
        private static SocketTextChannel channel;

        public static async Task Main()
        {
            iceDwellers = JsonSerializer.Deserialize<Dictionary<ulong, List<string>>>(File.ReadAllText("dwellers.json"));
            var socketConfig = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers | GatewayIntents.GuildMessages | GatewayIntents.MessageContent
            };
            client = new DiscordSocketClient(socketConfig);

            client.Log += Log;

            var token = File.ReadAllText("token.txt");

            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();

            client.Ready += Ready;

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        private static async Task<Task> Ready() {
            client.GuildMemberUpdated += OnGuildMemberUpdated;
            client.MessageReceived += OnMessageReceived;
            Console.WriteLine("Bot is connected!");
            guild = client.GetGuild(1127406800296226939);
            channel = guild.TextChannels.FirstOrDefault(c => c.Name == "nickname-history");
            await guild.DownloadUsersAsync();
            await CollectMembers();
            return Task.CompletedTask;
        }

        private static async Task OnGuildMemberUpdated(Cacheable<SocketGuildUser, ulong> cachedOldUser, SocketGuildUser newUser)
        {
            if(cachedOldUser.HasValue) {
                var oldUser = cachedOldUser.Value;
                if (oldUser.Nickname != newUser.Nickname)
                {
                    List<string> namesList;
                    if(!iceDwellers.TryGetValue(oldUser.Id, out namesList)) {
                        var newNamesList = new List<string>
                        {
                            newUser.Nickname
                        };
                        iceDwellers.Add(oldUser.Id, newNamesList);
                    } else {
                        namesList.Add(newUser.Nickname);
                    }
                    await channel.SendMessageAsync($"{newUser.Username}: {oldUser.Nickname} -> {newUser.Nickname}");
                }
            } else {
                var newNamesList = new List<string>
                {
                    newUser.Nickname
                };
                if(!iceDwellers.TryAdd(newUser.Id, newNamesList)) {
                    List<string> namesList;
                    if(iceDwellers.TryGetValue(newUser.Id, out namesList)) {
                        if(namesList.Last() != newUser.Nickname) {
                            await channel.SendMessageAsync($"{newUser.Username}: {namesList.Last()} -> {newUser.Nickname}");
                        }
                    }
                }
            }
            File.WriteAllText("dwellers.json", JsonSerializer.Serialize(iceDwellers));
        }

        private static async Task OnMessageReceived(SocketMessage arg)
        {
            if (arg is not SocketUserMessage message) return;
            var mChannel = message.Channel;
            if (mChannel.Name != "nickname-history")
                return;
            if (message.Content.StartsWith("!history"))
            {
                var parts = message.Content.Split(' ', 2);
                if (parts.Length > 1)
                {
                    var username = parts[1].Trim();
                    
                    if (!string.IsNullOrEmpty(username))
                    {
                        var id = await GetUserId(username, true);
                        if(id == null) {
                            id = await GetUserId(username, false);
                        }
                        if(id == null) {
                            await mChannel.SendMessageAsync($"{username} not found.");
                            return;
                        }
                        List<string> namesList;
                        if(!iceDwellers.TryGetValue((ulong)id, out namesList)) {
                            await mChannel.SendMessageAsync($"{username} has no stored history.");
                            return;
                        }

                        string outputString = "";
                        for(int i=0; i < namesList.Count; i++) {
                            outputString += $"{i+1}. {namesList[i]}\n";
                        }
                        await mChannel.SendMessageAsync($"{username}'s history:\n{outputString}");
                    }
                }
            }
        }

        private static async Task CollectMembers()
        {
            foreach(SocketGuildUser user in guild.Users) {
                if(!iceDwellers.ContainsKey(user.Id)) {
                    var newNamesList = new List<string>
                    {
                        user.Nickname
                    };
                    iceDwellers.Add(user.Id, newNamesList);
                }
            }
            File.WriteAllText("dwellers.json", JsonSerializer.Serialize(iceDwellers));
        }

        private static async Task<ulong?> GetUserId(string name, bool nickname) {
            var user = nickname
                ? guild.Users.FirstOrDefault(c => c.Nickname == name)
                : guild.Users.FirstOrDefault(c => c.Username == name);

            return user?.Id;
        }


        private static Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
    }
}
