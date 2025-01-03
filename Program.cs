class Program
{
    static void Main(string[] args)
    {
        // Injections.Run();
        // Privileges.Run();


        string originalString = "Hej jag gillar glass";

        // Hej -> rfh4587h7854
        // rfh4587h7854 -> Hej
        string encryptedString = encrypt(originalString);

        Console.WriteLine(encryptedString);
        Console.WriteLine(decrypt(encryptedString));
    }

    static string encrypt(string s)
    {
        string encrypted = "";
        // Loopa igenom strängen och shifta tecknet med ett nummer
        // (enligt UTF8 / ASCII)
        foreach (char c in s)
        {
            int i = (int)c;
            i++;

            char ec = (char)i;
            encrypted += ec;
        }

        return encrypted;
    }

    static string decrypt(string s)
    {
        string decrypted = "";
        // Loopa igenom strängen och shifta tecknet tillbaka ett nummer
        // (enligt UTF8 / ASCII)
        foreach (char c in s)
        {
            int i = (int)c;
            i--;

            char ec = (char)i;
            decrypted += ec;
        }

        return decrypted;
    }
}
