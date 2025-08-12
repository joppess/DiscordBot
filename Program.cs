using Discord; // importerar grunden för all kommunikation mot API:t
using Discord.Rest; // används för att hämta lr skicka data till DC utan att hålla konstant anslutning
using Discord.WebSocket; // impoterar allt som rör websocket.(delen som håller kontakt med discord i  realtid)
using System.Text.Json;
using System.IO;
using System.Xml.Serialization;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Json;
internal class Program
{
    static async Task Main(string[] args)
    {
        string? token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
        if (string.IsNullOrEmpty(token))
        {
            System.Console.WriteLine("Ingen Discord-token hittades i miljövariblerna");
            return;
        }
        var config = new DiscordSocketConfig
        {
            // Guilds - gör att boten kan läsa serverinfo och veta vilka guilds(servrar) den är i
            // GuildMessages - boten ger tillgång till meddelanden som skcikas i textkanaler
            // MessageContent - gör så boten kan läsa själva innehållet i meddelandet
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent
        };

        var client = new DiscordSocketClient(config);
        var userRow = new UserRow(); // ny instans av klassen
        userRow.LoadData(); // försöker läsa in tidiager sparad xp från user.json

        client.Log += LogAsync; // prenumererar på Disord klientens logg händelser
        client.MessageReceived += userRow.MessageReceivedAsync; // när ett medd dyker upp kör den här metoden

        // autentiserar mot DC:s servrar med bot token
        await client.LoginAsync(TokenType.Bot, token); // await pausar Main (utan att frysa tråden) tills DC svarat // TokenType.Bot talar för DC att detta är en bokt-token
        await client.StartAsync(); // Startar gateway-anslutning

        System.Console.WriteLine("Botten lever");
        await Task.Delay(-1); // håller programmet igång
    }
    // Definerar event-hanteraren för loggar
    private static Task LogAsync(LogMessage msg) // är static för att metoden inte anv någon data från spec Program instans
    {
        System.Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }
}
public class UserRow
{
    private static readonly HttpClient http = new HttpClient(); // HttpClient typen (klassen) som sköter HTTP-anrop (GET/POST osv)

    // user.json är filnamnet vi ska spara/läsa ifrån
    string DataFile = "users.json";

    // ulong är nyckeln och Userdata är värdet (xp och anv-namn)
    Dictionary<ulong, UserData> users = new Dictionary<ulong, UserData>();

    public ulong Id { get; set; }
    public string? Username { get; set; }
    public int Xp { get; set; }



    public void LoadData() // laddar user.json (fyller dicten)
    {
        if (!File.Exists(DataFile))
        {
            return;
        }
        try // försök köra koden, vid fel fångar catch
        {
            string json = File.ReadAllText(DataFile); // läs in all text till en sträng
            // försök översätta json-strängen till en lista av UserRow-objekt.
            // JsonSerializer: översätter || Deserialize: gör json text till ett C#-objekt
            // List<UserRow>> talar om att vi förväntar oss en lista av UserRow-objektet i json
            // json ä variabeln som innehåller json texten från filen
            // om vänsta sidan (rows) är null(tom lista) använd högra sidan istället (gör en ny lista)
            List<UserRow> rows = JsonSerializer.Deserialize<List<UserRow>>(json) ?? new List<UserRow>();

            users.Clear(); // töm dict:en i minnet (så nytt inte blandas med gammalt)

            // går igenom varje rad i listan och lägger in dictionaryn users 
            foreach (UserRow r in rows)
            {
                // [r.Id] är nyckeln i dict:en (Discord id)
                // skapa nytt anv-objekt och fyll i namn och xp
                users[r.Id] = new UserData
                {
                    Username = r.Username ?? string.Empty, // om r.Username är null anv en tom sträng
                    Xp = r.Xp
                };
            }
            // skriver ut hur många som laddades
            System.Console.WriteLine($"[LOAD] Loaded {rows.Count} users from {DataFile}");
        }
        catch (Exception ex) // fångar felen så programmet inte kraschar
        {
            System.Console.WriteLine($"[LOAD] Failed to load {DataFile}: {ex.Message}");
        }
    }
    public void SaveData()
    {
        try // skapar en lista som ska sparas till fil
        {
            List<UserRow> rows = new List<UserRow>(); // gör om dict:en till en lista av UserRow-ojektet

            // KeyValuePair = ett objekt som innehåller 2 saker
            // 1. Key(ulong, discord id)       2. Value(Userdata, namn, xp)
            foreach (var kv in users) // gå igenom varje post i dict:en // var kv = C# listar ut att det är KeyValuePair
            {
                // kv.Key = Discord-ID (ulong)
                // kv.Value = själva användardatan (UserData)
                rows.Add(new UserRow
                {
                    Id = kv.Key,
                    Username = kv.Value.Username,
                    Xp = kv.Value.Xp
                });
            }
            // rows innehåller anv vi vill spara 
            // writeIntended gör så datan inte sparas i en lång rad
            var json = JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(DataFile, json); // skapar eller skriver över filen. lägger in JSON-texten i filen
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[SAVE] Failed to save {DataFile}: {ex.Message}");
        }
    }


    // async = väntar på sync från DC-server men fortsätt med andra saker utan att låsa programmet
    // Task är en klass som representerar ett pågående arbete eller nåt som kommer hända.
    public async Task MessageReceivedAsync(SocketMessage message) // parametern handlar om allt om meddelandet som skickades(text, avsändarem, kanal)
    {
        if (message.Author.IsBot) return; // ge ej xp till andra bottar
        if (message is not SocketUserMessage m) return; // Om message inte är en SocketUserMessage → return
        if (m.Source != MessageSource.User) return; // säkerställer att medd är från en anv..
        if (m.Channel is not Discord.WebSocket.SocketTextChannel) return; // låt bara medd i serverns textnakaler gå vidare

        // m är en lokal variabel av typen SocketUserMessage.
        if (m.Content.StartsWith("!meme", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                // en array av strängar där "parts" är variabeln som behåller arrayen
                // Split returnerar flera bitar och då för att spara flera värden behöver vi en array
                // ' ' = dela på mellanslag // 2 = max 2 delar
                //StringSplitOptions.RemoveEmptyEntries tar bort tomma delar (whitespaces)
                string[] parts = m.Content.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                // sträng får vara null 
                // om vi har mer än ett ord efter vi delat upp medd(split) så ta detta värde: (parts[1])
                // om inte ta detta värde (null) alltså ingen subreddit angavs.
                string? subreddit = parts.Length > 1 ? parts[1] : null;
                // om ingen subreddit angavs(subreddit == null) är variabeln null
                // om sant använd grund-URL(vänster om :)
                // om falskt bygg en URL som inkluderar subredditen(höger om :)
                // EscapeDataString kodar mellanslag så serven inte tolkar länken fel( tex: dank memes" → "dank%20memes" (mellanslag blir %20))
                string apiUrl = subreddit == null ? "https://meme-api.com/gimme" : $"https://meme-api.com/gimme/{Uri.EscapeDataString(subreddit)}";

                // http är en variabel av HttpClient
                var resp = await http.GetAsync(apiUrl);

                if (!resp.IsSuccessStatusCode) // svaret inte är lyckat så gör nedan
                {
                    await m.Channel.SendMessageAsync("Kunde inte hämta en meme just nu 🙈");
                    resp.Dispose(); //städar upp nätverksresurser (kom ihåg att stänga och släppa resurser när du är klar)
                    return;
                }
                string json = await resp.Content.ReadAsStringAsync(); // läser hela svaret som text(json)
                resp.Dispose();

                // JsonDocument.Parse skickar in argumentet json (vår text) den försöker tolka texten som JSON och bygger objektet/variabeln "doc"
                // using = städar upp autmatiskt när blocket slutar
                // JsonDocument läser JSON-texten till ett objekt.
                // RootElement ger dig själva json-trädet
                // doc.RootElement - Det översta elementet i den JSON som nyss parsades. Tänk: JSON är ett träd; roten är det allra översta { ... } eller [ ... ]
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    JsonElement root = doc.RootElement;
                }
                ///
                /// 
                /// 
                /// 
                /// 
                /// 
                /// 
                /// 
                /// 
                /// 
                /// 
                /// 
                /// 
                if (message.Content.Equals("!test", StringComparison.OrdinalIgnoreCase))
                {
                    await message.Channel.SendMessageAsync("Test: jag är här 👋");
                    return;
                }
                // message.Author = avsändaren av meddelandet
                // .Id = deras unika ID av typen ulong
                ulong userId = message.Author.Id; // hämtar unika DC-ID:t för användare som skrev meddelandet
                string? username = message.Author.Username; // Hämtar användarens synliga namn i DC
                System.Console.WriteLine($" {userId}, {username}");

                // FÖRE: hämta nuvarande data och level innan vi ökar XP
                users.TryGetValue(userId, out var ud);           // null om ny
                int prevXp = ud?.Xp ?? 0;
                int prevLvl = LevelSystem.countLvl(prevXp);

                Username = message.Author.Username; // sparar användarens Dc-namn i UserData

                if (users.ContainsKey(userId))
                {
                    users[userId].Xp += 4; // lägg til 2 till den xp som redan finns
                }
                else // existerar inte användaren i dictionarien
                {
                    users[userId] = new UserData // ersätt/lägg till värdet i dict med nyckeln (suerId) skapa nytt tomt användarobjekt
                    {
                        // fyller i värden direkt
                        Username = username,
                        Xp = 4
                    }; // klar med användares data, sätter in det i dict
                }
                int xp = users[userId].Xp; // users[userId] - slår upp användaren i dict //.Xp plockar ut egenskapen xp // allt spar i en variabel
                int lvl = LevelSystem.countLvl(xp); // kallar på metoder och skickar in xp som argument som sparas värde i lvl


                // ÖKA XP // ud syftar till userdata
                if (ud != null)  // är ud inte null så finns anv i dict:en
                {
                    ud.Xp += 4; // ökar värdet i ud med 1
                    if (ud.Username != username)
                        // om UserName inte är null så blir ud.UserName username
                        ud.Username = username ?? string.Empty; // om ud.Username är null ta höger (bli tom sträng)
                }
                else
                {
                    // anv fanns ej skapa ny post 
                    // users[userId] stoppar samman objekt i dict:en
                    // både ud och users[userId] pekar på samma UserData, med Xp = 1
                    users[userId] = ud = new UserData { Username = username ?? string.Empty, Xp = 2 };
                }

                // EFTER: räkna ny level
                int currXp = ud.Xp; //  kopierar nuvarande XP (värdetyp) till currXp
                int currLvl = LevelSystem.countLvl(currXp); // beräknar ny lvl baserat på ny xp

                // Skriv bara när level ökat
                if (currLvl > prevLvl)
                {
                    await message.Channel.SendMessageAsync($"{username} har nått level {currLvl}! 🎉");
                }

                Console.WriteLine($"{username} xp {prevXp}->{currXp}, lvl {prevLvl}->{currLvl}");

            }


}




