using Discord; // importerar grunden för all kommunikation mot API:t
using Discord.Rest; // används för att hämta lr skicka data till DC utan att hålla konstant anslutning
using Discord.WebSocket; // impoterar allt som rör websocket.(delen som håller kontakt med discord i  realtid)

// ulong är nyckeln och Userdata är värdet (xp och anv-namn)
Dictionary<ulong, UserData> user1 = new Dictionary<ulong, UserData>();

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


