// custom basic programming language for user designated assistant tasks
// Lexi Script

/* ERROR CODES:
 0: Code executed successfully
 1(0..2): Conversion error { 0: string, 1: int, 2: double }
 2: Syntax error
 3: Var not found
 4: Function not found
*/

using Microsoft.CSharp.RuntimeBinder;
using System.ComponentModel;
using System.Media;
using System.Text;

namespace lexi;


#region Functions
// built-in language functions
internal struct General
{
    public static bool Print(string text)
    {
        Console.WriteLine(text);
        return true;
    }

    public static bool ThreadSleep(int delay, string factor)
    {
        switch (factor)
        {
            case "s": delay *= 1000; break;
            case "m": delay *= 60_000; break;
            case "h": delay *= 3_600_000; break;
            default: break;
        }

        Thread.Sleep(delay);
        return true;
    }

    public static dynamic Input(string prompt)
    {
        Console.WriteLine(prompt);

        string userInput = Console.ReadLine();
        return Convert.ChangeType(userInput, userInput.GetType());
    }

    public static bool Playsound(string path)
    {
        try
        {
            SoundPlayer player = new(path);
            player.Play();

            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }   
    }
}


// handles all things files
internal struct FileHandler
{
    public static string ReadFile(string path)
    {
        string text = File.ReadAllText(path);
        return text;
    }

    public static bool WriteFile(string path, string text)
    {
        File.WriteAllText(path, text);
        return true;
    }

    public static bool DeleteFile(string path)
    {
        File.Delete(path);
        return true;
    }
}
#endregion

#region Handlers
public struct BooleanHandler
{
    private static bool HandleBooleanExpression(string[] expression)
    {
        // Create a stack to store the operands
        Stack<bool> operandStack = new();

        // Iterate through each character in the expression
        foreach (string token in expression)
        {
            if (token == "!")
            {
                // If the token is "!", it is a negation operator, so apply it to the top operand on the stack
                bool operand = operandStack.Pop();
                operandStack.Push(!operand);
            }
            else if (token == "&&" || token == "||")
            {
                // If the token is "&&" or "||", it is a binary operator, so apply it to the top two operands on the stack
                bool operand2 = operandStack.Pop();
                bool operand1 = operandStack.Pop();
                if (token == "&&")
                    operandStack.Push(operand1 && operand2);
                else if (token == "||")
                    operandStack.Push(operand1 || operand2);
            }
            else
                operandStack.Push(bool.Parse(token));
        }

        // After all the characters have been processed, the final result should be the only value left on the stack
        return operandStack.Pop();
    }


    public static bool EvaluateStringExpression(string input, Dictionary<string, dynamic> vars)
    {
        string[] splitInput = input.Split(' ');

        // parse variables and ensure they are converted to strings
        for (int i = 0; i < splitInput.Length; i++)
            if (vars.TryGetValue(splitInput[i], out dynamic value))
                splitInput[i] = Convert.ToString(value);

        // could just be a given boolean
        if (splitInput.Length == 1)
        {
            if (bool.Parse(splitInput[0]) == true) { return true; }
            else if (bool.Parse(splitInput[0]) == false) { return false; }
        }
        
        // parse
        string[] operations = Array.Empty<string>();
        string[] operators = Array.Empty<string>();

        string token_str = string.Empty;
        foreach (string token in splitInput) 
        { 
            // add operation and operands to a string to eventually append onto an array
            if (token != "&&" && token != "||")
                token_str += $"{token} ";
            // detected boolean operator, append the token and append operation-
            // -to eventually be dealt with via the HandleBooleanExpression method
            else
            {
                operations = operations.Append(token_str).ToArray();
                operators = operators.Append(token).ToArray();
                token_str = string.Empty;
            }
        }

        operations = operations.Append(token_str).ToArray();
        string[] booleanExpression = Array.Empty<string>();

        foreach (string token in operations)
        {
            string[] tokens = token.Split(' ');

            // check whether the user is comparing a numerical value or a string
            // if they are comparing a numerical value, it will be outputted to their respective operand variables
            if (double.TryParse(tokens[0], out double operand_1) && double.TryParse(tokens[2], out double operand_2))
            {
                // check the first token to determine the operator
                switch (tokens[1])
                {
                    case "<=":
                        booleanExpression = booleanExpression.Append($"{operand_1 <= operand_2}").ToArray(); break;   // evaluate the less-than-or-equal-to operator
                    case "<":
                        booleanExpression = booleanExpression.Append($"{operand_1 < operand_2}").ToArray(); break;   // evaluate the less-than operator
                    case ">=":
                        booleanExpression = booleanExpression.Append($"{operand_1 >= operand_2}").ToArray(); break;   // evaluate the greater-than-or-equal-to operator
                    case ">":
                        booleanExpression = booleanExpression.Append($"{operand_1 > operand_2}").ToArray(); break;   // evaluate the greater-than operator
                    case "==":
                        booleanExpression = booleanExpression.Append($"{operand_1 == operand_2}").ToArray(); break;   // evaluate the equal-to operator
                    case "!=":
                        booleanExpression = booleanExpression.Append($"{operand_1 != operand_2}").ToArray(); break;   // evaluate the not-equal-to operator
                    default:
                        return false;
                }
            }
            // here we compare strings
            else
            {
                switch (tokens[1])
                {
                    case "==":
                        booleanExpression = booleanExpression.Append($"{tokens[0] == tokens[2]}").ToArray(); break;   // evaluate the equal-to operator (for string)
                    case "!=":
                        booleanExpression = booleanExpression.Append($"{tokens[0] != tokens[2]}").ToArray(); break;   // evaluate the not-equal-to operator (for string)
                }
            }
            
        }

        booleanExpression = booleanExpression.Concat(operators).ToArray();

        return HandleBooleanExpression(booleanExpression);
    }
}


internal struct FunctionHandler
{
    private static dynamic[] ParseFunction(string func, Dictionary<string, dynamic> vars)
    {
        // get all parameters within brackets
        string trimmed = func[(func.IndexOf('(') + 1)..^1];

        dynamic[] args = trimmed.Split(',');
        args[0] = args[0].Trim();

        // string handle
        for (int i = 0; i < args.Length; i++)
        {
            // in case that it's a string the user has passed in
            if (StringHandler.IsString(args[i].Trim()))
                args[i] = StringHandler.Format(args[i].Trim(), vars);
            else
            {
                // if not, could either be a variable or a number
                // has to be converted to string here because it will crash otherwise
                if (vars.ContainsKey(args[i]))
                    args[i] = Convert.ToString(vars[args[i]]);
                else
                    args[i] = Convert.ToString(args[i]);
            }
        }

        return args;
    }


    public static async Task<dynamic> RunFunction(string func, Dictionary<string, dynamic> vars)
    {
        // split into args and function name
        dynamic[] args = ParseFunction(func, vars);

        // this will check if the passed in variable "func" could potentially be a function or not
        // used for variable assignment if it isn't a function
        try
        {
            if (func.Contains('='))
                func = func[(func.IndexOf('=') + 2)..func.IndexOf('(')].Trim();
            else
                func = func[..func.IndexOf('(')].Trim();
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }

        // dict of all inbuilt functions
        return func switch
        {
            "print" => General.Print(args[0]),   // typical console print
            "ReadFile" => FileHandler.ReadFile(args[0]),   // reads all text from file path given in args[0] and returns string
            "FileExists" => File.Exists(args[0]),   // check if file exists at path args[0] and returns boolean
            "WriteFile" => FileHandler.WriteFile(args[0], args[1]),   // overrides file at args[0] with string passed in as args[1]
            "DeleteFile" => FileHandler.DeleteFile(args[0]),   // deletes file at file path given in args[0]
            "AssistantProcess" => await MainPrgm.assistant.Process(args[0].ToUpper()),   // passes in args[0] to the running assistant to be processed as if it was spoken
            "speak" => await GlobalScope.Speak(args[0]),   // assistant speaks text given in args[0]
            "root" => Math.Pow(Convert.ToDouble(args[0]), 1 / Convert.ToDouble(args[1])),   // finds the nth root (where n = args[1]) of args[0] and returns double
            "pow" => Math.Pow(Convert.ToDouble(args[0]), Convert.ToDouble(args[1])),   // raises args[0] to the power of args[1] and returns double
            "sleep" => General.ThreadSleep(Convert.ToInt32(args[0]), args[1]),   // delays program by args[0] given the factor of args[1] (args[1] could either be 'ms' 's' or 'h')
            "input" => General.Input(args[0]),   // takes user input, prompts user with args[0] and returns string
            "playsound" => General.Playsound(args[0]),   // plays the sound (.wav) at filepath args[0]
            "message" => GlobalScope.MessagePhoneNumber(args[0], args[1]),   // messages phone number args[0] with message args[1]
            _ => null   // unrecognized function (function doesn't exist)
        };
    }
}


public struct OperationHandler
{
    // infix to postfix converter
    private static string[]? InfixToPostfix(string[] expression)
    {
        // Create a dictionary to hold the order of operations for each operator.
        Dictionary<string, int> order = new()
        {
            { "+", 1 },
            { "-", 1 },
            { "*", 2 },
            { "/", 2 },
            { "%", 2 },
            { "^", 3 }
        };

        // Create an array to hold the postfix expression and a stack to hold operators and parentheses
        string[] postfixExpr = new string[expression.Length];
        Stack<string> exprStack = new();

        // Loop through each string in the input expression.
        foreach (string s in expression)
        {
            // If the string is a number, add it to the postfix expression
            if (TypeDescriptor.GetConverter(typeof(double)).IsValid(s))
                postfixExpr = postfixExpr.Append(s).ToArray();

            // If the string is an opening parenthesis, push it onto the stack.
            else if (s == "(")
                exprStack.Push(s);

            // If the string is a closing parenthesis, pop operators off the stack and add them to the postfix expression-
            // -until an opening parenthesis is found.
            else if (s == ")")
            {
                bool cond = exprStack.Count != 0 && exprStack.Peek() != "(";
                while (cond)
                {
                    string a = exprStack.Pop();
                    postfixExpr = postfixExpr.Append(a).ToArray();
                }
                if (cond)
                    return null;
                else
                    exprStack.Pop();
            }

            // If the string is an operator, pop operators off the stack and add them to the postfix expression-
            // -until an operator with lower precedence is found, then push the new operator onto the stack
            else
            {
                while (exprStack.Count != 0 && (order[s] <= order[exprStack.Peek()]))
                    postfixExpr = postfixExpr.Append(exprStack.Pop()).ToArray();
                exprStack.Push(s);
            }
        }

        // Pop any remaining operators off the stack and add them to the postfix expression
        while (exprStack.Count != 0)
            postfixExpr = postfixExpr.Append(exprStack.Pop()).ToArray();

        return postfixExpr;
    }



    // _________Reverse Polish Notation implementation_________ //
    public static dynamic Handle(string[] operation, Dictionary<string, dynamic> vars = null)
    {
        Stack<double> values = new();

        // user could be referencing a variable
        // we do this by iterating through and checking if the vars dict contains the current index
        for (int i = 0; i < operation.Length; i++)
            if (vars is not null && vars.TryGetValue(operation[i], out dynamic value))
                operation[i] = Convert.ToString(value);

        string[]? postfixExpression = InfixToPostfix(operation);

        // for every value
        foreach (string token in postfixExpression)
        {
            // try to push it the stack
            // since the stack can only hold numerical values, it will flag an error if it is an operation
            // from there we will handle operations
            try
            {
                values.Push(Convert.ToDouble(token));
            }
            catch
            {
                if (values.Count > 1)
                {
                    // acquire operands from stack
                    double num_1 = values.Pop();
                    double num_2 = values.Pop();

                    // apply operation top operands accordingly
                    switch (token)
                    {
                        case "+": num_2 += num_1; break;
                        case "-": num_2 -= num_1; break;
                        case "*": num_2 *= num_1; break;
                        case "/": num_2 /= num_1; break;
                        case "%": num_2 %= num_1; break;
                        case "^": num_2 = Math.Pow(num_1, num_2); break;
                        // returning 2 indicates a syntax error in the written code has been made
                        default: return 2;
                    }

                    // push the result to the stack for further processing
                    values.Push(num_2);
                }
                
            }
        }

        // the last value in the stack should be the end result
        return values.Pop();
    }
}

internal struct StringHandler
{
    // interpolation notation is 'f'
    public static bool IsString(string text)
    {
        // if has quotes or designated interpolation notation
        return (text.StartsWith('"') || text.StartsWith("f\"")) && text.EndsWith('"');
    }


    public static string Format(string text, Dictionary<string, dynamic> vars)
    {
        if (!text.StartsWith('f') || !text.Contains('{'))
        {
            // If the text doesn't start with 'f' or doesn't contain '{',
            // then return a substring of the text that removes the leading and trailing quotation marks
            return text[(text.IndexOf('"') + 1)..^1];
        }

        // Use a StringBuilder object to construct the result string
        StringBuilder result = new();
        Queue<char> queue = new();

        // Add each character in the text to the queue
        foreach (char c in text)
            queue.Enqueue(c);

        // Process each character in the queue
        while (queue.Count > 0)
        {
            char c = queue.Dequeue();

            if (c == '{')
            {
                // If the character is '{', then read the variable name until the closing '}' character is found
                string variable = "";

                while (queue.Count > 0 && queue.Peek() != '}')
                    variable += queue.Dequeue();

                // If the closing '}' character is found, then remove it from the queue
                if (queue.Count > 0)
                    queue.Dequeue();

                // Append the value of the variable to the resultant string
                result.Append(vars[variable]);
            }
            else
            {
                // If the character is not '{', then append it to the resultant string as is
                result.Append(c);
            }
        }

        // Remove the leading and trailing quotation marks from the result string
        result.Remove(0, 2);
        result.Remove(result.Length - 1, 1);

        return result.ToString();
    }


}
#endregion

#region Interpreter
public static class Interpreter
{
    // loop handler
    private static async Task<bool> HandleLoop(string command, string[] snippet, Dictionary<string, dynamic> vars)
    {
        // best to create a variable for it to save performance
        string codeSnippetToExecute = string.Join('\n', snippet).Trim();

        // for loop
        if (command.StartsWith("for"))
        {
            string[] operands = command[(command.IndexOf('(') + 1)..command.IndexOf(')')].Split(',');
            
            // we need to trim the front and end whitespaces for correct parsing
            // acquire all necessary variables for a for loop
            string loopVariableName = operands[0].Trim();

            // we will try to parse the values to integers by using the TryParse method
            // this will return the value (if correctly parsed) to the corresponding loopVar (as indicated by the 'out' keyword)
            // if failed, we will check all existing variables for a reference
            if (!int.TryParse(operands[1], out int loopStartValue))
                loopStartValue = Convert.ToInt32(vars[operands[1].Trim()]);

            // we do not need to trim the operand here as the TryParse method understands regardless
            if (!int.TryParse(operands[^1], out int loopEndValue))
                loopEndValue = Convert.ToInt32(vars[operands[^1].Trim()]);

            for (int i = loopStartValue; i <= loopEndValue; i++)
            {
                // update current loop variable and pass through to be interpreted with
                vars[loopVariableName] = i;
                vars = await Interpret(codeSnippetToExecute, vars);
            }

            // we need to remove the temporary loop variable dictionary
            vars.Remove(loopVariableName);
                    
        }
        // while loop
        else
        {
            string condition = command[(command.IndexOf('(') + 1)..^1];

            while (BooleanHandler.EvaluateStringExpression(condition, vars))
                vars = await Interpret(codeSnippetToExecute, vars);
        }

        // indicates loop handling was successful
        return true;
    }


    // parses line for interpreting, returned values are error codes (see key)
    public static async Task<dynamic> Interpret(string code, Dictionary<string, dynamic>? tempVars = null)
    {
        // split code into queue    
        Queue<string> codeQueue;
        codeQueue = new(code.Split('\n'));

        // method has to be within the Interpret method so it can access codeQueue and dequeue it accordingly
        string[] ParseWigglyBrackets()
        {
            // retrieve code snippet within loop bounds by finding wiggly brackets
            string[] codeSnippet = Array.Empty<string>();

            // dequeue the statement, and the first opening wiggly bracket
            codeQueue.Dequeue();
            codeQueue.Dequeue();

            // amount of wiggly brackets potentially within the snippet, user could be nesting
            int wigglyBracketDepth = 1;
            while (wigglyBracketDepth > 0)
            {
                // get current code line and dequeue it
                string line = codeQueue.Dequeue().Trim();

                // if we have found a closing wiggly bracket, we can subtract one from the depth
                // until we have found all nesting statements, we have to carry on
                if (line == "}")
                    wigglyBracketDepth--;

                // if we have found an opening wiggly bracket, add one to the depth to account for
                else if (line == "{")
                    wigglyBracketDepth++;

                // append each line of code until it gets returned
                codeSnippet = codeSnippet.Append(line).ToArray();  
            }

            // up until the last item to remove trailing wiggly bracket
            return codeSnippet[..^1];
        }
        // varName, varValue
        Dictionary<string, dynamic> vars = new();
        // functionName, functionCode
        Dictionary<string, string[][]> functions = new();

        // vars passed in from a loop
        if (tempVars is not null)
            vars = tempVars; 

        while (codeQueue.Count > 0)
        {
            string currentCodeLine = codeQueue.Peek().Trim();
            if (!(string.IsNullOrEmpty(currentCodeLine) || currentCodeLine == "}" || currentCodeLine[..2] == "//"))
            {
                string[] args = currentCodeLine.Split(' ');

                // get initial command
                switch (args[0])
                {
                    // loops
                    case string a when a.StartsWith("for") || a.StartsWith("while"):
                        // retrieve code snippet within loop bounds by finding wiggly brackets
                        string[] codeSnippet = ParseWigglyBrackets();
                        
                        await HandleLoop(currentCodeLine, codeSnippet, vars);
                        break;

                    // if statement
                    case "if":
                        string ifStatementLine = codeQueue.Peek().Trim();
                        string[] conditionalCode = ParseWigglyBrackets();
     
                        string condition = ifStatementLine[(ifStatementLine.IndexOf('(') + 1)..^1];

                        if (BooleanHandler.EvaluateStringExpression(condition, vars))
                            vars = await Interpret(string.Join("\n", conditionalCode), vars);

                        break;

                    // user is defining a function
                    case "fn":
                        // acquire function reference (without args)
                        // parse code within wiggly brackets, for referencing in dictionary with accessor functionName
                        string functionName = args[1][..args[1].IndexOf('(')] + "()";
                        string[] functionArgs = currentCodeLine[(currentCodeLine.IndexOf('(') + 1)..^1].Split(',');

                        // remove whitespaces
                        for (int i = 0; i < functionArgs.Length; i++)
                            functionArgs[i] = functionArgs[i].Trim();

                        string[] functionCode = ParseWigglyBrackets();

                        functions[functionName] = new string[][] { functionCode, functionArgs };

                        break;

                    // user is returning a value from a function
                    case "return":
                        // acquire token after "return"
                        dynamic returnedValue = string.Join(' ', currentCodeLine.Split(' ')[1..]);

                        // handle variable type
                        if (StringHandler.IsString(returnedValue))
                            returnedValue = StringHandler.Format(returnedValue, vars);
                        else if (returnedValue == "true" || returnedValue == "false")
                            returnedValue = Convert.ToBoolean(returnedValue);
                        else if (double.TryParse(returnedValue, out double value))
                            returnedValue = value;
                        else if (vars.ContainsKey(returnedValue))
                            returnedValue = vars[returnedValue];

                        return returnedValue;
     
                    // check all functions or assign variable
                    default:
                        dynamic result;          
                        try
                        {   // first check user-defined functions
                            
                            string function = (currentCodeLine[..(currentCodeLine.IndexOf('(') + 1)] + ')').Split(' ')[^1];
                            // Console.WriteLine(function);
                            if (functions.TryGetValue(function, out string[][] value))
                            {
                                string[] arguments = currentCodeLine[(currentCodeLine.IndexOf('(') + 1)..^1].Split(',');

                                // check if function is defined with arguments
                                if (value[1].Length >= 0)
                                {
                                    Dictionary<string, dynamic> functionVariables = new();

                                    // iterate through both functionVariables and arguments
                                    // this allows for the corresponding argument variables to be matched with their respective values
                                    // then gets passed in as tempVars within the Interpret() function
                                    for (int i = 0; i < value[1].Length; i++)
                                    {
                                        // ensure string args are handled correctly (otherwise would appear with quotations)
                                        if (StringHandler.IsString(arguments[i].Trim()))
                                            arguments[i] = StringHandler.Format(arguments[i].Trim(), vars);
                                        
                                        try
                                        {
                                            arguments[i] = Convert.ToString(vars[arguments[i]]);
                                        }
                                        catch (KeyNotFoundException)
                                        {
                                            arguments[i] = arguments[i];
                                        }

                                        functionVariables[value[1][i]] = arguments[i];
                                    }
                                        
                                    result = await Interpret(string.Join('\n', value[0]), functionVariables);
                                }

                                else
                                    result = await Interpret(string.Join('\n', value[0])); 
                            }

                            else
                                // attempt to run function, will return null if it doesn't exist
                                result = await FunctionHandler.RunFunction(currentCodeLine, vars);
                            
                        }
                        // sometimes a function will return a void (which isn't a value a var can hold), in this case we will have to exception handle
                        // this negates the default outcome which assumes a function always returns a value for a var to hold
                        catch (RuntimeBinderException)
                        {
                            break;
                        }
                        
                        string variableName = args[0];
                        // if the function does not exist, then it must be a variable declaration or assignment
                        if (result is null)
                        {
                            string assignedValue = string.Join(' ', args[(Array.IndexOf(args, "=") + 1)..]);

                            if (StringHandler.IsString(assignedValue))
                                vars[variableName] = StringHandler.Format(assignedValue, vars);
                                
                            // handle particularly for booleans, as the method GetType does not work for them (?)
                            else if (assignedValue == "true" || assignedValue == "false")
                                vars[variableName] = Convert.ToBoolean(assignedValue);

                            // check if the assignment includes an operation, or if it could be a string by C#'s standards
                            else if (!new char[] { '+', '-', '*', '/', '%' }.Any(assignedValue.Contains) && assignedValue.GetType() != typeof(string))
                                vars[variableName] = Convert.ChangeType(assignedValue, assignedValue.GetType());
                                
                            else
                                vars[variableName] = OperationHandler.Handle(args[(Array.IndexOf(args, "=") + 1)..], vars);
                        }
                        else
                            if (args.Contains("="))
                                vars[variableName] = result;
                            
                        break;
                }
            }

            if (codeQueue.Count > 0)
                codeQueue.Dequeue();
        }
        // tempVars is passed in if it's being interpreted from a loop or an IF statement
        // return tempVars to be re-passed in or modified
        if (tempVars is not null)
            return vars;
        return 0;
    }
}
#endregion