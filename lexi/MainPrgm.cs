using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Data.Sqlite;


/* multi purpose AI Assistant
 * voice activated
 * keyword based understanding
 * Harrison O'Leary 
*/


namespace lexi;

using static Assistant;
using static GlobalScope;

internal class MainPrgm
{
    public static Bot? assistant;

    private static async Task FetchReminders()
    {
        // establish a connection to the database
        using SqliteConnection connection = new("Data Source=data/data.db");
        connection.Open();

        // return all reminders who's time is under or equal to the current time
        SqliteCommand command = connection.CreateCommand();
        command.CommandText =
        @"
            SELECT reminder
            FROM reminders
            WHERE time <= $time
        ";

        // get the current time in 12hr format with AM or PM at the end
        // "hh:mm" returns the time in 12hr format, and "tt" returns either AM or PM
        string time = DateTime.Now.ToString("h:mmtt");
        command.Parameters.AddWithValue("$time", time);

        SqliteDataReader reader = command.ExecuteReader();
        
        // until all returned queries have been read, acquire the next and deal with
        while (reader.Read())
        {
            // get the leading reminder and speak it
            string reminder = reader.GetString(0);
            await Speak(reminder);
            MessagePhoneNumber($"Reminder:\n{reminder}", assistant.PhoneNumber);

            // we have to create a new command to then remove the reminder from the database
            // a new SqliteCommand object has to be created, otherwise it crashes
            SqliteCommand removeReminderCommand = connection.CreateCommand();

            removeReminderCommand.CommandText =
            @"
                DELETE FROM reminders
                WHERE time <= $time
            ";

            removeReminderCommand.Parameters.AddWithValue("$time", time);
            removeReminderCommand.ExecuteNonQuery();
        }


        reader.Close();
        connection.Close();
    }


    // ______MAIN LOOP______ //
    public static async Task Main()
    {
        // is this the first time the user has ran the application?
        // if so, walk the user through a setup process
        if (!File.Exists("data/data.db"))
            assistant = await FirstTimeStartup();

        // else, we will query all the necessary data from the database
        else
        {
            // create all the necessary commands to query the database
            // always order by the same respective column to synchronise data return
            const string genericDataCommand =
            @"
                SELECT value
                FROM generic
                ORDER BY variable ASC
            ";

            const string customResponsePromptCommand =
            @"
                SELECT prompt
                FROM custom_responses
                ORDER BY prompt ASC
            ";

            const string customResponseResponseCommand =
            @"
                SELECT response
                FROM custom_responses
                ORDER BY prompt ASC
            ";

            const string markedDirectoriesNameCommand =
            @"
                SELECT name
                FROM marked_directories
                ORDER BY name ASC
            ";

            const string markedDirectoriesPathCommand =
            @"
                SELECT path
                FROM marked_directories
                ORDER BY name ASC
            ";

            List<string> genericData = SQLRead(genericDataCommand);
            List<string> customResponsePrompts = SQLRead(customResponsePromptCommand);
            List<string> customResponseResponses = SQLRead(customResponseResponseCommand);
            List<string> markedDirectoriesNames = SQLRead(markedDirectoriesNameCommand);
            List<string> markedDirectoriesPaths = SQLRead(markedDirectoriesPathCommand);

            Dictionary<string, string> customResponses = new();
            Dictionary<string, string> markedDirectories = new();

            // we have to do prompts and responses separately (same for marked dirs) since we cannot directly get a dictionary from a query
            // we will loop through every item and add a key-value pair to its respective dictionary
            for (int i = 0; i < customResponsePrompts.Count; i++)
                customResponses[customResponsePrompts[i]] = customResponseResponses[i];

            for (int i = 0; i < markedDirectoriesNames.Count; i++)
                markedDirectories[markedDirectoriesNames[i]] = markedDirectoriesPaths[i];

            assistant = new Bot(genericData[0], Convert.ToBoolean(genericData[2]), genericData[3], customResponses, markedDirectories, genericData[1], genericData[4]);
        }

        // initial greeting of user
        // depends on time of day
        if (DateTime.Now.Hour > 5 && DateTime.Now.Hour < 12)
            await Speak($"Good morning, {assistant.UserName}.");
        else if (DateTime.Now.Hour >= 12 && DateTime.Now.Hour < 18)
            await Speak($"Good afternoon, {assistant.UserName}.");
        else
            await Speak($"Good evening, {assistant.UserName}.");

        // _______MAIN PROGRAM LOOP_______ //
        while (true)
        {
            SpeechConfig speechConfig = SpeechConfig.FromSubscription(SUBSCRIPTION_KEY, SERVICE_REGION);
            speechConfig.SpeechRecognitionLanguage = "en-GB";

            using AudioConfig audioConfig = AudioConfig.FromDefaultMicrophoneInput();
            SpeechRecognizer speechRecognizer = new(speechConfig, audioConfig);

            Console.Write("<< ");
            SpeechRecognitionResult speechRecognitionResult = await speechRecognizer.RecognizeOnceAsync();
            string input = speechRecognitionResult.Text;

            if (input.Length > 2)
            {
                Console.Write($"{input} \n");

                if (input.Contains(assistant.Name) || assistant.IsOmniMode)
                {
                    // remove bot name from input to parse properly
                    if (input.Contains(assistant.Name.ToUpper()))
                    {
                        List<string> words = input.Split(' ').ToList();
                        words.RemoveAt(words.IndexOf(assistant.Name.ToUpper()));

                        input = string.Join(' ', words);
                    }

                    // API includes punctuation upon recognition, this interferes with parsing and thus has to be removed
                    input = input.Replace(".", "");
                    input = input.Replace(",", "");
                    input = input.Replace("?", "");

                    string response = await assistant.Process(input.ToUpper());
                    await Speak(response);
                }
            }

            await FetchReminders();
        }
    }
}