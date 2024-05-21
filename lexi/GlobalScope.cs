using System.Runtime.InteropServices;
using System.Text;
using Microsoft.CognitiveServices.Speech;
using Microsoft.Data.Sqlite;
using SHDocVw;
using Shell32;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace lexi;

using static Assistant;

internal struct GlobalScope
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    #region Constants
    public static readonly string[] greetings = { "hello", "hi", "hey", "yo", "hiya" };
    public static readonly string[] emphasisers = { "absolutely", "very", "extremely" };

    public static readonly string[] affirmatives =
    {
        "Okkie dokkie.", "Okkie doke.", "Okkie pokkie.", "Okkie poke.", "Will do.", "Okay.", "As you wish.",
        "If you say so."
    };

    public const string VERSION = "ALPHA 1.0.0";

    public const string SUBSCRIPTION_KEY = "";
    public const string SERVICE_REGION = "";

    public const string SQL_GENERIC_DATA =
        @"
            UPDATE generic
            SET value = $value
            WHERE variable = $varName
        ";

    // error codes for interpreter
    public static readonly Dictionary<ushort, string> ERROR_CODES = new()
    {
        {0, "Program executed successfully."},
        {10, "Conversion error: cannot convert to string."},
        {11, "Conversion error: cannot convert to int."},
        {12, "Conversion error: cannot convert to double."},
        {2, "Syntax error."},
        {3, "Variable not found."},
        {4, "Function not found."}
    };

    #endregion

    #region Functions
    public static string ParseFolderName(string folderPath)
    {
        string folderName = string.Empty;

        for (int i = folderPath.Length - 1; i > 0; i--)
        {
            // check if file or folder by extension
            if (folderPath[i] == '.')
                return "Not a directory!";

            folderName = folderPath[i] + folderName;

            // is this the end of the name?
            if (folderPath[i - 1] == '\\')
                break;
        }

        return folderName;
    }


    public static List<string> SQLRead(string sql)
    {
        // establish a connection to the database
        using SqliteConnection connection = new("Data Source=data/data.db");
        connection.Open();

        // return all reminders who's time is under or equal to the current time
        SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;

        SqliteDataReader reader = command.ExecuteReader();

        List<string> dataQueried = new();
        // until all returned queries have been read, acquire the next and deal with
        while (reader.Read())
        {
            string variable = reader.GetString(0);
            dataQueried.Add(variable);
        }

        reader.Close();
        connection.Close();

        return dataQueried;
    }


    public static void SQLWrite(string sql, Dictionary<string, dynamic>? variables = null)
    {
        // establish a connection to the database
        using SqliteConnection connection = new("Data Source=data/data.db");
        connection.Open();

        // create command
        SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;

        // sometimes we may want to execute SQL code without any variables
        if (variables is not null)
            // add all command parameters
            foreach (KeyValuePair<string, dynamic> entry in variables)
                command.Parameters.AddWithValue(entry.Key, entry.Value);

        // execute and close database
        command.ExecuteNonQuery();
        connection.Close();
    }


    public static string GenerateKey(int encryptionSeed, int length)
    {
        const string CHARS =
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789`-=_+[]{};:'@#~,<.>/?!£$%^&*()/";

        // new random object with user defined seed
        Random random = new(encryptionSeed);

        string key = string.Empty;

        // encryption key has to be at least same length as input
        for (int i = 0; i < length; i++)
            key += CHARS[random.Next(CHARS.Length)];

        return key;
    }

    public static string XOR(string text1, string text2)
    {
        string result = string.Empty;

        // equivocate both strings to ascii binary
        byte[] text1Bytes = Encoding.ASCII.GetBytes(text1);
        byte[] text2Bytes = Encoding.ASCII.GetBytes(text2);

        // xor integers, convert them to ascii and append to "result"
        for (int i = 0; i < text1Bytes.Length; i++)
            result += Convert.ToChar(text1Bytes[i] ^ text2Bytes[i]);

        return result;
    }


    public static async Task Speak(string? text)
    {
        if (text is not null)
        {
            Console.WriteLine($">> {text}");

            SpeechConfig speechConfig = SpeechConfig.FromSubscription(SUBSCRIPTION_KEY, SERVICE_REGION);

            // voice font
            speechConfig.SpeechSynthesisVoiceName = "en-GB-SoniaNeural";

            using SpeechSynthesizer speechSynthesizer = new(speechConfig);
            await speechSynthesizer.SpeakTextAsync(text);
        }
    }


    // window.HWND can't detect desktop??
    public static List<string>? GetFilesSelected()
    {
        // get current viewed window
        IntPtr handle = GetForegroundWindow();

        List<string> selectedFiles = new();

        // for every open window
        foreach (ShellBrowserWindow window in new ShellWindows())
            // if this window is the one the user is viewing
            if (window.HWND == checked((int)handle))
            {
                // get all selected files
                FolderItems items = ((IShellFolderViewDual2)window.Document).SelectedItems();

                if (items.Count == 0)
                    return null;

                // add each selected file to list
                foreach (FolderItem item in items)
                    selectedFiles.Add(item.Path);

                return selectedFiles;
            }

        return null;
    }


    public static string RandomChoice(string[] arr)
    {
        return arr[new Random().Next(arr.Length)];
    }


    public static string Arr2Str(dynamic strings)
    {
        string result = string.Empty;

        foreach (string s in strings)
            result += $"{s} ";

        return result;
    }


    public static dynamic Input<T>(string prompt)
    {
        Console.Write(prompt);

        return Convert.ChangeType(Console.ReadLine(), typeof(T));
    }

    public static bool MessagePhoneNumber(string message, string phoneNumber)
    {
        string accountSid = "";
        string authToken = "";

        TwilioClient.Init(accountSid, authToken);

        MessageResource.Create(
            body: message,
            from: new Twilio.Types.PhoneNumber("+16088893084"),
            to: new Twilio.Types.PhoneNumber(phoneNumber)
        );

        return true;
    }


    public static async Task<Bot> FirstTimeStartup()
    {
        // boilerplate
        await Speak(
            $"Hello! I am Lexi, your personal virtual assistant. I can do a variety of things; I am currently in {VERSION}.\n" +
            ">> I have determined that this is your first time booting up, therefore I shall run you through a first time setup.");

        await Speak("What would you like me to refer to you as?");
        string username = Input<string>("Name: ");

        await Speak($"Hi {username}, Please enter your phone number in +44 format.");
        string phoneNumber = Input<string>("(i.e. +447911123456)\nPhone number:");

        await Speak($"Terrific job {username}! You have now setup Lexi completely.");
        Console.WriteLine(">> Creating new bot instance.. <<");

        // create bot instance and serialize
        Bot assistant = new("Lexi", false, "unlock", new Dictionary<string, string>(), new Dictionary<string, string>(), username, phoneNumber);

        string sqlCommandText =
        @"
            INSERT INTO generic(variable, value)
            VALUES(Name, Lexi),
            (IsOmniMode, FALSE),
            (LockdownPassword, unlock),
            (Username, $username),
            (PhoneNumber, $phoneNumber);
        ";

        Dictionary<string, dynamic> keyValuePairs = new() 
        {
            { "$username", username },
            { "$phoneNumber", phoneNumber }
        };

        SQLWrite(sqlCommandText, keyValuePairs);

        return assistant;
    }
    #endregion
}