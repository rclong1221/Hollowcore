#if DIG_DEV_CONSOLE
using System.Collections.Generic;

namespace DIG.DebugConsole
{
    /// <summary>
    /// EPIC 18.9: Tokenizer for console input.
    /// Handles quoted strings and --flag value pairs.
    /// </summary>
    public static class CommandParser
    {
        public static ConCommandArgs Parse(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return new ConCommandArgs(input ?? "", "", System.Array.Empty<string>(), new Dictionary<string, string>());

            var tokens = Tokenize(input.Trim());
            if (tokens.Count == 0)
                return new ConCommandArgs(input, "", System.Array.Empty<string>(), new Dictionary<string, string>());

            string commandName = tokens[0].ToLowerInvariant();
            var positional = new List<string>();
            var flags = new Dictionary<string, string>();

            for (int i = 1; i < tokens.Count; i++)
            {
                if (tokens[i].StartsWith("--"))
                {
                    string flagName = tokens[i].Substring(2);
                    if (i + 1 < tokens.Count && !tokens[i + 1].StartsWith("--"))
                    {
                        flags[flagName] = tokens[i + 1];
                        i++;
                    }
                    else
                    {
                        flags[flagName] = "true";
                    }
                }
                else
                {
                    positional.Add(tokens[i]);
                }
            }

            return new ConCommandArgs(input, commandName, positional.ToArray(), flags);
        }

        private static List<string> Tokenize(string input)
        {
            var tokens = new List<string>();
            int i = 0;

            while (i < input.Length)
            {
                // Skip whitespace
                while (i < input.Length && char.IsWhiteSpace(input[i])) i++;
                if (i >= input.Length) break;

                // Quoted string
                if (input[i] == '"')
                {
                    i++;
                    int start = i;
                    while (i < input.Length && input[i] != '"') i++;
                    tokens.Add(input.Substring(start, i - start));
                    if (i < input.Length) i++; // skip closing quote
                }
                else
                {
                    // Unquoted token
                    int start = i;
                    while (i < input.Length && !char.IsWhiteSpace(input[i])) i++;
                    tokens.Add(input.Substring(start, i - start));
                }
            }

            return tokens;
        }
    }
}
#endif
