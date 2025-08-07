public class LevelSystem
{
    public static int countLvl(int totalXP)
    {
        int lvl = 0;
        int demandedXPForNextLvl = 10;
        while (totalXP >= demandedXPForNextLvl)
        {
            totalXP -= demandedXPForNextLvl; //xpn förbrukar när lvl sker
            lvl++; //lvln ökar med 1
            demandedXPForNextLvl *= 2; // för nästa nivå *2
        }
        return lvl;
    }
}