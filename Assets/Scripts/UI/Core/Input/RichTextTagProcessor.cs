using System.Text;
using System.Text.RegularExpressions;

namespace DIG.UI.Core.Input
{
    /// <summary>
    /// Processes rich text tags to replace action references with input glyphs.
    /// 
    /// EPIC 15.8: Input Glyph System
    /// 
    /// Supported tags:
    ///   &lt;Action:ActionName&gt;  - Replaced with text glyph (e.g., "[F]", "(A)")
    ///   &lt;action:ActionName&gt;  - Same as above (case-insensitive tag)
    /// 
    /// Examples:
    ///   "Press &lt;Action:Jump&gt; to jump" → "Press [Space] to jump"
    ///   "Hold &lt;Action:Aim&gt; and press &lt;Action:Fire&gt;" → "Hold [RMB] and press [LMB]"
    /// </summary>
    public static class RichTextTagProcessor
    {
        // Regex pattern: <Action:ActionName> or <action:ActionName>
        // Captures the action name in group 1
        private static readonly Regex ActionTagPattern = new(
            @"<[Aa]ction:(\w+)>",
            RegexOptions.Compiled
        );
        
        /// <summary>
        /// Processes input string, replacing action tags with glyphs.
        /// </summary>
        public static string Process(string input, InputDeviceType deviceType, InputGlyphDatabase database)
        {
            if (string.IsNullOrEmpty(input))
                return input;
                
            if (database == null)
            {
                // No database - just strip tags and show action names
                return ActionTagPattern.Replace(input, match => $"[{match.Groups[1].Value}]");
            }
            
            return ActionTagPattern.Replace(input, match =>
            {
                string actionName = match.Groups[1].Value;
                return database.GetText(actionName, deviceType);
            });
        }
        
        /// <summary>
        /// Checks if a string contains any action tags.
        /// </summary>
        public static bool ContainsActionTags(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;
                
            return ActionTagPattern.IsMatch(input);
        }
        
        /// <summary>
        /// Extracts all action names from a string.
        /// </summary>
        public static string[] ExtractActionNames(string input)
        {
            if (string.IsNullOrEmpty(input))
                return System.Array.Empty<string>();
                
            var matches = ActionTagPattern.Matches(input);
            var result = new string[matches.Count];
            
            for (int i = 0; i < matches.Count; i++)
            {
                result[i] = matches[i].Groups[1].Value;
            }
            
            return result;
        }
        
        /// <summary>
        /// Strips all action tags from a string, leaving just the action names.
        /// Useful for accessibility/screen readers.
        /// </summary>
        public static string StripTags(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;
                
            return ActionTagPattern.Replace(input, match => match.Groups[1].Value);
        }
    }
}
