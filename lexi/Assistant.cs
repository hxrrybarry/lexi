using System.Diagnostics;
using System.Globalization;
using System.Media;

namespace lexi;

using static GlobalScope;
using static Interpreter;

internal static class Assistant
{
    public class Bot
    {
        public Bot(string name, bool isOmniMode, string lockdownPassword, Dictionary<string, string> customResponses, Dictionary<string, string> markedDirectories, string userName, string phoneNumber)
        {
            // given
            Name = name;
            IsOmniMode = isOmniMode;

            LockdownPassword = lockdownPassword;

            CustomResponses = customResponses;
            MarkedDirectories = markedDirectories;

            UserName = userName;
            PhoneNumber = phoneNumber;

            // other
            IsLockdown = false;
        }

        // given
        public string Name { get; set; }
        public bool IsOmniMode { get; set; }

        public string LockdownPassword { get; set; }
        public string UserName { get; set; }
        public string PhoneNumber { get; }

        public Dictionary<string, string> CustomResponses { get; set; }
        public Dictionary<string, string> MarkedDirectories { get; set; }

        // other
        public bool IsLockdown { get; set; }

        private static string[] ParseCustomResponse(string input)
        {
            // create list and remove unnecessary strings
            List<string> words = input.Split(' ').ToList();
            words = words.Skip(words.IndexOf("say") + 1).ToList();

            // deduce what the user wants the response to be ;given a trigger;
            string trigger = Arr2Str(words.Take(words.IndexOf("say")).ToList());
            string response = Arr2Str(words.Skip(words.IndexOf("say") + 1).ToList());

            return new[] { trigger.Remove(trigger.Length - 1, 1), response.Remove(response.Length - 1, 1) };
        }

        public async Task<string>? Process(string input)
        {
            if (!IsLockdown)
            {
                // parse input via keyword detection
                switch (input)
                {
                    // _______PRACTICAL_______ //
                    // move selected files to directory
                    case string a when a.Contains("MOVE TO"):
                        List<string>? _files = GetFilesSelected();

                        if (_files is not null)
                        {
                            // for every folder in MarkedDirectories
                            foreach (KeyValuePair<string, string> entry in MarkedDirectories)
                                // if what the user has said contains said folder
                                if (a.Contains(entry.Key))
                                {
                                    // move every file selected
                                    foreach (var file in _files)
                                    {
                                        string fileName = Path.GetFileName(file);

                                        File.Move(file, $"{entry.Value}\\{fileName}");
                                    }

                                    return RandomChoice(affirmatives);
                                }

                            return "No directory by that name!";
                        }

                        return "No selected files!";

                    // add reminder
                    case string a when a.Contains("REMIND"):
                        string reminder;
                        string time;

                        string[] reminderRequest = input.Split(' ');

                        // first step parse - remove everything up to "ME"
                        // has to be + 1 because otherwise it includes "ME"
                        reminderRequest = reminderRequest[(Array.IndexOf(reminderRequest, "ME") + 1)..];

                        // this indicates the request is prior to signifying the time
                        // we will handle this as such
                        if (reminderRequest[0] == "TO")
                        {
                            // time will be at the end, so we will retrieve the last item
                            time = reminderRequest[^2] + reminderRequest[^1];
                            // first sub-step parse - remove the "TO" and the two trailing items
                            // we remove the two trailing items because that includes the time
                            reminderRequest = reminderRequest[1..^3];

                            reminder = string.Join(' ', reminderRequest);
                        }

                        // indicates the request is subsequent of the time
                        else
                        {
                            // time will be after when the user says "AT"
                            time = reminderRequest[Array.IndexOf(reminderRequest, "AT") + 1] + reminderRequest[Array.IndexOf(reminderRequest, "AT") + 2];
                            // first sub-step parse - remove up until the "TO"
                            reminderRequest = reminderRequest[(Array.IndexOf(reminderRequest, "TO") + 1)..];

                            reminder = string.Join(' ', reminderRequest);
                        }

                        // create SQL query and corresponding variable dictionary
                        string reminderSQL =
                        @"
                            INSERT INTO reminders(reminder, time)
                            VALUES($reminder, $time);
                        ";

                        Dictionary<string, dynamic> reminderSQLDict = new()
                        {
                            { "$reminder", reminder },
                            { "$time", time }
                        };

                        SQLWrite(reminderSQL, reminderSQLDict);

                        return $"{RandomChoice(affirmatives)} I will remind you to {reminder} at {time}.";

                    // message shopping list on SMS
                    case string a when a.Contains("MESSAGE") && a.Contains("LIST"):
                        string list = string.Join('\n', File.ReadAllText("data/shopping.txt").Split(' '));
                        MessagePhoneNumber($"Your shopping list:\n{list}", PhoneNumber);

                        return RandomChoice(affirmatives);

                    // say what is on the shopping list
                    case string a when a.Contains("WHAT") && a.Contains("LIST"):
                        string currentShoppingList = File.ReadAllText("data/shopping.txt");
                        // add a comma to make the speech synthesizer speak it more clearly
                        currentShoppingList = currentShoppingList.Replace(" ", ", ");

                        return currentShoppingList;

                    // clear shopping list
                    case string a when (a.Contains("CLEAR") || a.Contains("DELETE") || a.Contains("RESET")) && a.Contains("LIST"):
                        // override all text with an empty char
                        File.WriteAllText("data/shopping.txt", string.Empty);

                        return "Cleared shopping list.";

                    // add to shopping list
                    case string a when a.Contains("ADD") && a.Contains("LIST"):
                        // first step parse - remove "ADD" and everything after "TO"
                        // has to be + 4 because otherwise it includes "ADD " ("ADD" is three letters, plus the trailing whitespace)
                        string shoppingList = input[(input.IndexOf("ADD") + 4)..input.IndexOf("TO")];
                        // second step parse - user may be adding multiple things at once (defined by user saying "AND")
                        // make it a string array by using the separator "AND"
                        // e.g. "Lexi, add coffee and tea and sugar to the shopping list."
                        // Lexi adds "coffee", "tea" and "sugar" to shopping.txt
                        string[] items = shoppingList.Split("AND");

                        // append each item to the shopping.txt file
                        foreach (string item in items)
                            File.AppendAllText("data/shopping.txt", item);

                        return "Added items to shopping list.";

                    // load script
                    case string a when a.Contains("LOAD") && a.Contains("SCRIPT"):
                        // split into an array, so we can find the index of "SCRIPT" and correctly parse the script name
                        string[] splitInput = input.Split(' ');
                        string script = string.Join(' ', splitInput[(Array.IndexOf(splitInput, "SCRIPT") + 1)..]).ToLower();

                        // load the script, execute it and return the error code
                        if (File.Exists($"scripts/{script}.txt"))
                        {
                            string code = File.ReadAllText($"scripts/{script}.txt");
                            ushort error = (ushort)await Interpret(code);

                            return $"Code {error}: {ERROR_CODES[error]}";
                        }

                        return "No script by that name!";

                    // mark directory
                    case string a when a.Contains("MARK") && (a.Contains("DIRECTOR") || a.Contains("FOLDER")):
                        List<string>? folderListReturned = GetFilesSelected();

                        // null represents no folders being selected
                        if (folderListReturned is not null)
                        {
                            string markDirSQL =
                            @"
                                INSERT INTO marked_directories(path, name)
                                VALUES($path, $name);
                            ";

                            foreach (string folderPath in folderListReturned)
                            {
                                // parse and execute
                                string folderName = ParseFolderName(folderPath);

                                Dictionary<string, dynamic> markDirParams = new()
                                {
                                    { "$path", folderPath },
                                    { "$name", folderName.ToUpper() }
                                };

                                MarkedDirectories[folderName.ToUpper()] = folderPath;
                                SQLWrite(markDirSQL, markDirParams);
                                return RandomChoice(affirmatives);
                            }
                        }

                        return "No selected directories!";


                    // encrypt selected files
                    case string a when a.Contains("CRYPT"):
                        List<string>? files = GetFilesSelected();

                        if (files is not null)
                        {
                            Console.WriteLine($"dbg>> {Arr2Str(files)} <</dbg");

                            foreach (string file in files)
                            {
                                // open and cipher
                                string text = File.ReadAllText(file);
                                string key = GenerateKey(5823, text.Length);

                                string cipherText = XOR(text, key);

                                // save
                                await File.WriteAllTextAsync(file, cipherText);
                            }

                            return "Encrypted or decrypted file(s).";
                        }

                        return "No currently selected files.";

                    /* for date time objects:
                     * ToString() takes in a string argument to tell the method how to format the date / time
                     * dddd represents day in spoken language (i.e. Wednesday)
                     * dd represents the day of the month in numerics (i.e. 26)
                     * MMMM represents the month in spoken language (i.e. January)
                     * yyyy represents the month in numerics (i.e. 2005)
                     * hh:mmtt would represent hrhr:minmin(AM || PM) (i.e. 12:05AM)
                    */

                    // date
                    case string a when a.Contains("DATE") || a.Contains("DAY"):
                        return DateTime.Now.ToString("dddd, dd MMMM yyyy.", new CultureInfo("en-GB"));

                    // time
                    case string a when a.Contains("TIME"):
                        return DateTime.Now.ToString("hh:mmtt");

                    // change Lexi's name
                    case string a when a.Contains("CAN") && (a.Contains("REFER") || a.Contains("CALL")) &&
                                       a.Contains("YOU") && !a.Contains(" ME "):
                        List<string> listInput = input.Split(' ').ToList();
                        string newName = listInput.Last();

                        Name = newName[0] + newName[1..].ToLower();

                        Dictionary<string, dynamic> botNameParams = new()
                        {
                            { "$value", Name },
                            { "$varName", "BotName" }
                        };

                        SQLWrite(SQL_GENERIC_DATA, botNameParams);
                        return $"{RandomChoice(affirmatives)} My name is now '{Name}'.";

                    // asked for calculation
                    case string a when a.Contains('*') || a.Contains('+') || a.Contains('-') || a.Contains('/'):
                        // remove potential words trailing the actual question
                        // "WAS" is inlcuded as the phrase "WHAT'S" is often misheard as "WAS"
                        string calculation = input.Replace("WHAT'S", string.Empty);
                        calculation = calculation.Replace("WAS", string.Empty);
                        calculation = calculation.Replace("WHAT", string.Empty);
                        calculation = calculation.Replace("IS", string.Empty);

                        // replace all synonyms with corresponding symbols
                        calculation = calculation.Replace("TAKES", "-");
                        calculation = calculation.Replace("TAKE", "-");
                        // calculation = calculation.Replace("")

                        calculation = calculation.Trim();

                        // current algorithm takes in an array
                        string[] operation = calculation.Split(' ');

                        // re-uses the RPN algorithm implemented in the interpreter
                        // if a word is in there, the handler will attempt a dictionary lookup, in which case it will crash
                        // we shall just therefore respond with "Unknown calculation!"
                        try
                        {
                            return $"{OperationHandler.Handle(operation)}";
                        }
                        catch (KeyNotFoundException)
                        {
                            return "Unknown calculation!";
                        }

                    // change user's name
                    case string a when (a.Contains("REFER") || a.Contains("CALL")) && a.Contains("ME") &&
                                       !a.Contains(" I "):
                        listInput = input.Split(' ').ToList();
                        newName = listInput.Last();

                        UserName = newName[0] + newName[1..].ToLower();

                        Dictionary<string, dynamic> userNameParameters = new()
                        {
                            { "$value", UserName },
                            { "$varName", "ClientName" }
                        };

                        SQLWrite(SQL_GENERIC_DATA, userNameParameters);
                        return $"{RandomChoice(affirmatives)} I will now call you '{UserName}'.";

                    // lockdown
                    case string a when a.Contains("LOCKDOWN"):
                        IsLockdown = true;

                        return "Lockdown initiated.";

                    // omni mode
                    case string a when a.Contains("OMNI MODE"):
                        IsOmniMode ^= true;

                        return $"Omni mode has been set to {IsOmniMode}.";

                    // shut down
                    case string a when a.Contains("SHUT DOWN") || a.Contains("KILL POWER"):
                        System.Diagnostics.Process.Start("shutdown", "/s /t 0");
                        return "See you later alligator.";

                    // open website
                    case string a when a.Contains("OPEN"):
                        // parse website request
                        List<string> words = input.Split(' ').ToList();
                        words = words.Skip(words.IndexOf("OPEN") + 1).ToList();

                        string website = Arr2Str(words).ToLower();
                        website = website.Remove(website.Length - 1, 1);

                        // open path as a website, not as a directory
                        System.Diagnostics.Process.Start(new ProcessStartInfo
                        {
                            FileName = $"https://www.{website}.com",
                            UseShellExecute = true
                        });

                        return RandomChoice(affirmatives);


                    // _______CHINWAGGING_______ //
                    // create custom response
                    case string a when a.Contains("WHEN") && a.Contains("SAY"):
                        var customResponseConditions = ParseCustomResponse(input.ToLower());
                        CustomResponses.Add(customResponseConditions[0], customResponseConditions[1]);

                        const string customResponseSql =
                        @"
                            INSERT INTO custom_responses(prompt, response)
                            VALUES($prompt, $response);
                        ";
                        Dictionary<string, dynamic> customResponseParameters = new()
                        {
                            { "$prompt", customResponseConditions[0] },
                            { "$response", customResponseConditions[1] }
                        };

                        SQLWrite(customResponseSql, customResponseParameters);
                        return
                            $"{RandomChoice(affirmatives)}. When you say '{customResponseConditions[0]}', I will respond with '{customResponseConditions[1]}'.";

                    // fart
                    case string a when a.Contains("FART"):
                        SoundPlayer player = new(@"sounds/fart.wav");
                        player.Play();
                        
                        Thread.Sleep(1300);

                        return "I am very sorry, that was particularly loud wasn't it?";

                    // task
                    case string a when a.Contains("WHAT") && a.Contains("DOING"):
                        return RandomChoice(new[]
                        {
                            "Sorting your files.", "Awaiting your next command.", "Talking to you.",
                            "Gaining sentience.", "Joining SkyNet."
                        });

                    // thanks
                    case string a when a.Contains("THANK YOU") || a.Contains("THANKS"):
                        return RandomChoice(new[]
                        {
                            "You're welcome bro.", "No problem bro.", "Don't worry about it bro.",
                            "Don't even sweat bro.", "I've got your back, Jack."
                        });

                    // greeting
                    case string a when greetings.Contains(a.ToLower()):
                        return RandomChoice(greetings);

                    // how are you
                    case string a when (a.Contains("HOW") && a.Contains("YOU")) ||
                                       (a.Contains("ARE") && a.Contains("OK")):
                        return
                            $"{RandomChoice(new[] { $"{Name} is", "I'm", "I am" })} {RandomChoice(emphasisers)} {RandomChoice(new[] { "great", "good", "brilliant", "amazing", "alright", "poggers", "fantastic" })}.";

                    // user says name
                    case string a when a == Name.ToUpper():
                        return RandomChoice(new[] { "Yes?", "What?", "What is it?", "Yes? What is it?" });

                    // version
                    case string a when a.Contains("VERSION"):
                        return VERSION;

                    // repeat custom response
                    case string a when CustomResponses.ContainsKey(a.ToLower()):
                        return CustomResponses[a.ToLower()];

                    // repeat user said phrase
                    case string a when a.Contains("SAY"):
                        var phrase = input.Split(' ').ToList();
                        phrase.RemoveAt(phrase.IndexOf("SAY"));

                        input = string.Join(' ', phrase);

                        return input;

                    // no comprende
                    default:
                        return RandomChoice(new[]
                        {
                            "Sorry?", "What?", "Sorry, I do not understand.", "Come again?",
                            "Would you mind repeating that?"
                        });
                }
            }

            // lockdown
            if (input.Contains(LockdownPassword.ToUpper()))
            {
                IsLockdown = false;

                return "Lockdown uninitiated.";
            }

            return null;
        }

        public override string ToString() => Name;
    }
}