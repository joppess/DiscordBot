using Discord; // importerar grunden för all kommunikation mot API:t
using Discord.Rest; // används för att hämta lr skicka data till DC utan att hålla konstant anslutning
using Discord.WebSocket; // impoterar allt som rör websocket.(delen som håller kontakt med discord i  realtid)
using System.Text.Json;
using System.IO;
using System.Xml.Serialization;

public class UserRow
{
    // user.json är filnamnet vi ska spara/läsa ifrån
    string DataFile = "users.json";

    // ulong är nyckeln och Userdata är värdet (xp och anv-namn)
    Dictionary<ulong, UserData> user1 = new Dictionary<ulong, UserData>();

    public ulong Id { get; set; }
    public string Username { get; set; }
    public int Xp { get; set; }



    void LoadData() // laddar user.json (fyller dicten)
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

            user1.Clear(); // töm dict:en i minnet (så nytt inte blandas med gammalt)

            // går igenom varje rad i listan och lägger in dictionaryn user1 
            foreach (UserRow r in rows)
            {
                // [r.Id] är nyckeln i dict:en (Discord id)
                // skapa nytt anv-objekt och fyll i namn och xp
                user1[r.Id] = new UserData
                {
                    Username = r.Username,
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
    void SaveData()
    {
        try // skapar en lista som ska sparas till fil
        {
            List<UserRow> rows = new List<UserRow>(); // gör om dict:en till en lista av UserRow-ojektet

            // KeyValuePair = ett objekt som innehåller 2 saker
            // 1. Key(ulong, discord id)       2. Value(Userdata, namn, xp)
            foreach (var kv in user1) // gå igenom varje post i dict:en // var kv = C# listar ut att det är KeyValuePair
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

        }
    }


    // async = väntar på sync från DC-server men fortsätt med andra saker utan att låsa programmet
    // Task är en klass som representerar ett pågående arbete eller nåt som kommer hända.
    async Task MessageReceivedAsync(SocketMessage message) // parametern handlar om allt om meddelandet som skickades(text, avsändarem, kanal)
    {
        // message.Author = avsändaren av meddelandet
        // .Id = deras unika ID av typen ulong
        ulong userId = message.Author.Id; // hämtar unika DC-ID:t för användare som skrev meddelandet
        var username = message.Author.Username; // Hämtar användarens synliga namn i DC
        System.Console.WriteLine($" {userId}, {username}");

        UserData user = new UserData(); // skapar en ny användare i minnet (nytt objekt från klassen)
        user.Username = message.Author.Username; // sparar användarens Dc-namn i UserData
        user.Xp = 1; // användaren får 1xp

        if (user1.ContainsKey(userId))
        {
            user1[userId].Xp += 1; // lägg til 1 till den xp som redan finns
        }
        else // existerar inte användaren i dictionarien
        {
            user1[userId] = new UserData // ersätt/lägg till värdet i dict med nyckeln (suerId) skapa nytt tomt användarobjekt
            {
                // fyller i värden direkt
                Username = username,
                Xp = 1
            }; // klar med användares data, sätter in det i dict
        }
        int xp = user1[userId].Xp; // user1[userId] - slår upp användaren i dict //.Xp plockar ut egenskapen xp // allt spar i en variabel
        int lvl = LevelSystem.countLvl(xp); // kallar på metoder och skickar in xp som argument som sparas värde i lvl

        Console.WriteLine($"{username} is level {lvl} with {xp} XP");

    }

}

