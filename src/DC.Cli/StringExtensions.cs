namespace DC.Cli
{
    public static class StringExtensions
    {
        public static string MakeRelativeUrl(this string input)
        {
            var url = input;
            
            if (url.StartsWith("/"))
                url = url.Substring(1);

            if (url.EndsWith("/"))
                url = url.Substring(0, url.Length - 1);

            return url;
        }
    }
}