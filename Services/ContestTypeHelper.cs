using System.Text.RegularExpressions;

namespace CfMvc.Services;

/// <summary>
/// Infers a coarse "contest type" bucket from a Codeforces contest name. CF does not expose
/// division/category as structured data on contest.list, so this is parsed from the
/// human-readable name.
/// </summary>
public static class ContestTypeHelper
{
    public static readonly string[] Filters = { "All", "Div1", "Div2", "Div3", "Div4", "Educational", "Global", "CodeTON" };

    /// <summary>Returns every bucket a contest belongs to (a combined "Div. 1 + 2" round matches both).</summary>
    public static List<string> GetContestTypes(string name)
    {
        var types = new List<string>();
        if (Regex.IsMatch(name, "codeton", RegexOptions.IgnoreCase)) types.Add("CodeTON");
        if (Regex.IsMatch(name, "educational", RegexOptions.IgnoreCase)) types.Add("Educational");
        if (Regex.IsMatch(name, "global round", RegexOptions.IgnoreCase)) types.Add("Global");
        if (Regex.IsMatch(name, @"div\.?\s*1", RegexOptions.IgnoreCase)) types.Add("Div1");
        if (Regex.IsMatch(name, @"div\.?\s*2", RegexOptions.IgnoreCase)) types.Add("Div2");
        if (Regex.IsMatch(name, @"div\.?\s*3", RegexOptions.IgnoreCase)) types.Add("Div3");
        if (Regex.IsMatch(name, @"div\.?\s*4", RegexOptions.IgnoreCase)) types.Add("Div4");
        return types;
    }

    public static bool Matches(string name, string filter) =>
        filter == "All" || GetContestTypes(name).Contains(filter);
}
