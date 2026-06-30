//-----------------------------------------------------------------------
// <copyright file="YamlDictParser.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
using System.Text;
using System.Text.RegularExpressions;

namespace OnnxOcr.Core.Recognition;

internal static class YamlDictParser
{
    private static readonly Regex EntryRegex = new(@"^\s+-\s+(.+)$", RegexOptions.Compiled);

    public static List<string> Parse(string ymlPath)
    {
        var lines = File.ReadAllLines(ymlPath, Encoding.UTF8);
        var chars = new List<string>();
        bool inDict = false;

        foreach (var line in lines)
        {
            if (line.Trim() == "character_dict:")
            {
                inDict = true;
                continue;
            }

            if (!inDict)
                continue;

            var match = EntryRegex.Match(line);
            if (!match.Success)
                break;

            var value = match.Groups[1].Value;
            if (value.Length >= 2 && value[0] == value[^1] && (value[0] == '"' || value[0] == '\''))
                value = value[1..^1];

            chars.Add(value);
        }

        if (chars.Count == 0)
            throw new InvalidOperationException($"No character_dict entries found in {ymlPath}");

        return chars;
    }
}
