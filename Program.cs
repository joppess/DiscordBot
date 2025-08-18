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
using System.ComponentModel.Design.Serialization;
using System.Globalization;
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

        if (m.Content.StartsWith("!weather", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                // delar upp texten som anv skriver
                // m.Content är medd som anv skrev
                // texten delas upp vid varje mellanslag
                // StringSplitOptions.RemoveEmptyEntries tar bort whitespaces
                // resultatet sparas i en array som heter parts
                string[] words = m.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string city = "Borlänge";
                // finns det mer än 1 ord (kommando) i arrayen så sätt city till det andra ordet (kommandot)
                if (words.Length > 1)
                {
                    city = words[1];
                }

                // {Uri.EscapeDataString(city)} ser till att staden är korrekt kodad för webben.
                // count=1 betdyer att vi bara vill ha ett resultat 
                string geoUrl = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(city)}&count=1&language=sv&format=json";
                // skickar GET-anrop
                var geoResp = await http.GetAsync(geoUrl);
                if (!geoResp.IsSuccessStatusCode)
                {
                    await m.Channel.SendMessageAsync("Kunde inte slå upp orten just nu 🙈");
                    geoResp.Dispose();
                    return;
                }
                // geoResp.Content är datainnehållet från api-svaret
                string geoJson = await geoResp.Content.ReadAsStringAsync();
                geoResp.Dispose(); // tar bort de vi inte längre behöver från RAM
                double lat, lon;
                string geoName;
                // using stänger obj auto så vi slipper minnesproblem
                // gdoc innehåler nu hela svaret som ett JSON-träd där vi kan hämta värden genom att gå till rätt gren (t.ex. results[0].latitude)
                using (JsonDocument gdoc = JsonDocument.Parse(geoJson)) // läser json texten och gör den till objekt vi kan bläddra i
                {
                    var root = gdoc.RootElement; // tar fram rooten ur json-trädet
                                                 // försök hitta fältet "results" och lägg det i variabeln results. Hittas det returnera true annars false
                                                 // Eller om results finns men är en tom lista så skicka felmedd
                    if (!root.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
                    {
                        await m.Channel.SendMessageAsync($"Hittade ingen ort som matchar **{city}** 😕");
                        return;
                    }
                    var r0 = results[0]; // vi tar det första obj [0] i arrayen eftersom API sorterar bästa träffen först
                    lat = r0.GetProperty("latitude").GetDouble(); // går in i obj och hämtar värdet latitude
                    lon = r0.GetProperty("longitude").GetDouble(); // GetDouble() gör om värdet till ett decimal/flyttal

                    // finns fältet name i json-obj r0 så lägg det i varibalen n 
                    // stämmer det så står före ?
                    // ja - det som står mellan ? och : körs om testet är sant
                    // nej - det som står efter : körs om testet är falskt
                    // och ?? kolla om värdet är null // ja - anv värdet efter ?? // nej det finns ett värde. anv det (det som nGetString returnerar)
                    string name = r0.TryGetProperty("name", out var n) ? (n.GetString() ?? city) : city;

                    string admin = "";
                    // kollar om json-obj r0 har ett fält som heter admin1 och isf lägger värdet i variable a
                    if (r0.TryGetProperty("admin1", out var a))
                    {
                        string? adminTemp = a.GetString(); // försöker hämta texten från a och sparar den i en tillf variabel
                        if (adminTemp != null) // innehåller temp nåt så spara vi det i admin
                        {
                            admin = adminTemp;
                        }
                    }
                    string country = "";
                    if (r0.TryGetProperty("country", out var c))
                    {
                        string? countryTemp = c.GetString();
                        if (countryTemp != null)
                        {
                            country = countryTemp;
                        }
                    }
                    geoName = string.IsNullOrWhiteSpace(admin) ? $"{name}, {country}" : $"{name}, {admin}, {country}";

                    var latStr = lat.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    var lonStr = lon.ToString(System.Globalization.CultureInfo.InvariantCulture);

                    string meteoUrl =
                        $"https://api.open-meteo.com/v1/forecast?latitude={latStr}&longitude={lonStr}" +
                        $"&current=temperature_2m,relative_humidity_2m,apparent_temperature,is_day,precipitation,weather_code,wind_speed_10m" +
                        $"&hourly=precipitation_probability&timezone=auto";



                    var wxResp = await http.GetAsync(meteoUrl); // skicka get förfrågan till länken ovan

                    Console.WriteLine("[DEBUG] Meteo URL: " + meteoUrl);
                    Console.WriteLine("[DEBUG] Status: " + (int)wxResp.StatusCode + " " + wxResp.StatusCode);





                    if (!wxResp.IsSuccessStatusCode)
                    {
                        await m.Channel.SendMessageAsync("Kunde inte hämta vädret just nu 🌧️");
                        wxResp.Dispose();
                        return;
                    }





                    // wxResp.Content = själva innehållet från API svaret
                    string wxJson = await wxResp.Content.ReadAsStringAsync();
                    wxResp.Dispose();
                    double temp, feels, wind, rain;
                    int wxCode;
                    int calcNextHour = -1; // -1 anv som inget värde tills vi räknat ut något
                    string localTime = "";
                    // vi tar hela väder-svaret wxJson (som är en text) och gör den till JSON-obj
                    using (JsonDocument wdoc = JsonDocument.Parse(wxJson))
                    {
                        var wxroot = wdoc.RootElement;
                        var current = wxroot.GetProperty("current"); // hämtar current från JSON
                        temp = current.GetProperty("temperature_2m").GetDouble(); // gör om värdet till decimal
                        feels = current.GetProperty("apparent_temperature").GetDouble();
                        wind = current.GetProperty("wind_speed_10m").GetDouble();
                        rain = current.GetProperty("precipitation").GetDouble();
                        // .GetInt32 gör om det till heltal
                        wxCode = current.GetProperty("weather_code").GetInt32(); // hämtar en siffra som motsvarar vädret (tex 0 = klart)
                        localTime = current.TryGetProperty("time", out var t) ? (t.GetString() ?? "") : "";












                        // plocka sannolikhet för nederbör nästa timme, om möjligt
                        // times = listan med alla tider (t.ex. "2025-08-13T10:00", "2025-08-13T11:00", osv)
                        // pops = listan med alla nederbördssannolikheter (t.ex. 20, 45, 80 procent)
                        if (wxroot.TryGetProperty("hourly", out var hourly) &&
                            hourly.TryGetProperty("time", out var times) && // Från "hourly"-objektet, försök hitta "time"
                            hourly.TryGetProperty("precipitation_probability", out var pops) &&
                            times.ValueKind == JsonValueKind.Array && // Kolla att "time" faktiskt är en lista (array) och inte t.ex. en sträng.
                            pops.ValueKind == JsonValueKind.Array &&
                            times.GetArrayLength() == pops.GetArrayLength() && // Säkerställ att det finns lika många tider som procentsatser,annars vet vi inte vilket värde som hör till vilken tid.
                            !string.IsNullOrEmpty(localTime)) // Kolla att vi faktiskt har en giltig localTime (annars vet vi inte vilken tid som är nu)
                        {
                            int index = -1; // Vi sätter den till -1 först som en signal: “Vi har inte hittat nuvarande tid ännu”.
                            for (int i = 0; i < times.GetArrayLength(); i++) // loopar igenom alla tider i listan. funktionen ger hur många tider som finns i listan
                            {
                                // Loopen letar igenom hela listan med timvärden och hittar vilken plats som är “just nu”. Vi sparar platsen i index så vi vet var i listan vi är och kan titta på nästa timmes data.
                                // times[i] är tiden vid position i i listan
                                // .GetString är null använd tom sträng
                                // jämför tiden med localTime (som vi tidigare hämtade från"current" i JSON)
                                if ((times[i].GetString() ?? "") == localTime)
                                {
                                    index = i; // om vi hittar en matching sparar vi indexet (i) i variabeln
                                    break; // hoppa ut ur loop
                                }
                            }
                            // idx + 1 < pops.GetArrayLength() betyder att det finns en “nästa timme” efter nuvarande i listan pops
                            if (index >= 0 && index + 1 < pops.GetArrayLength())
                            {
                                calcNextHour = pops[index + 1].GetInt32(); // .GetInt32 gär om det från JSON till ett heltal
                            }
                            else if (index >= 0)
                            {
                                calcNextHour = pops[index].GetInt32(); // här tar vi sannolikheten för nederbörd vid nuvarnade tidpunkt
                            }

                            string WxDesc(int c, bool day)
                            {
                                switch (c)
                                {
                                    case 0:
                                        return day ? "Klar himmel ☀️" : "Klar himmel 🌙";
                                    case 1:
                                    case 2:
                                        return day ? "Mest klart 🌤️" : "Mest klart ☁️";
                                    case 3:
                                        return "Mulet ☁️";
                                    case 45:
                                    case 48:
                                        return "Dimma 🌫️";
                                    case 51:
                                    case 53:
                                    case 55:
                                        return "Duggregn 🌦️";
                                    case 56:
                                    case 57:
                                        return "Isduggregn 🧊🌧️";
                                    case 61:
                                    case 63:
                                    case 65:
                                        return "Regn 🌧️";
                                    case 66:
                                    case 67:
                                        return "Isregn 🧊🌧️";
                                    case 71:
                                    case 73:
                                    case 75:
                                        return "Snöfall ❄️";
                                    case 77:
                                        return "Snökorn ❄️";
                                    case 80:
                                    case 81:
                                    case 82:
                                        return "Skurar 🌦️";
                                    case 85:
                                    case 86:
                                        return "Snöbyar 🌨️";
                                    case 95:
                                        return "Åska ⛈️";
                                    case 96:
                                    case 99:
                                        return "Åska med hagel ⛈️";
                                    default:
                                        return "Vädersymbol saknas";
                                }
                            }
                            bool isDay = true;
                            string summary = WxDesc(wxCode, isDay);

                            string popTxt = calcNextHour >= 0 ? $"\n Nederbördssannolikhet nästa timme: {calcNextHour}%." : "";
                            await m.Channel.SendMessageAsync($"**Väder för {geoName}**\n" +
                            $"{summary}\n" +
                            $"Temp: {temp:0.#}°C (känns som {feels:0.#}°C)\n" +
                            $"Vind: {wind:0.#} m/s · Nederbörd: {rain:0.#} mm{popTxt}");
                        }

                    }
                }


            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[WEATHER] Error: {ex.Message}");
                await m.Channel.SendMessageAsync("Något gick fel när jag hämtade vädret 😬");
            }
            return; // avsluta så vi inte faller vidare i annan logik
        }

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

                    // root =  roten i JSON-svaret (t.ex. { "url": "...", "title": "...", "postLink":
                    //  GetProperty hämtar fältet url. vid fel kastas Exception
                    // GetString() gör om JSON-värdet till en sträng. obs kan bli null om värdet ej är en sträng
                    // ?? = om vänster sida blev null använd tom sträng (så inte imageUrl blir null)
                    string imageUrl = root.GetProperty("url").GetString() ?? "";
                    // gör en förenklad version på denna
                    string title;
                    // out är en behållare som ger tillbaka ett värde via en varibel som skickas in i metoden. 
                    // returnerar true/false pga TryGetProperty
                    // Metoden letar efter ett fält som heter "title" i root.
                    // Hittas det → metoden returnerar true och lägger värdet i burken t.
                    // Hittas det inte → metoden returnerar false och t blir ett default-värde (inget du ska använda).

                    if (root.TryGetProperty("title", out JsonElement t))
                    {
                        title = t.GetString() ?? "Meme";
                    }
                    else
                    {
                        title = "Meme";
                    }
                    // TryGetProperty försöker hämta egenskapen
                    // ? A : B = A körs om det blev true, annars körs B
                    // om det blev null, använd tom string
                    // ta postLink från JSON om det finns, annars använd tom sträng”, och out p är bara behållaren där TryGetProperty lägger hittat värde.
                    string postLink = root.TryGetProperty("postlink", out JsonElement p) ? (p.GetString() ?? "") : "";

                    if (String.IsNullOrWhiteSpace(imageUrl))
                    {
                        // m.Channel = kanalen där användaren skrev.
                        await m.Channel.SendMessageAsync("Jag hittade ingen bild i svaret 😕");
                        return;
                    }
                    if (!string.IsNullOrWhiteSpace(postLink))
                    {
                        await m.Channel.SendMessageAsync($"{title}\n{postLink}\n{imageUrl}");
                    }
                    else
                    {
                        await m.Channel.SendMessageAsync($"{title}\n{imageUrl}");
                    }
                }


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
            catch (Exception ex)
            {
                System.Console.WriteLine($"[MEME] Error: {ex.Message}");
                await m.Channel.SendMessageAsync("Något gick fel när jag hämtade memen 😬");
                return;

            }


        }
    }
}




