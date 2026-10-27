namespace Lumify.Api.Configuration
{
    public static class DotEnv
    {
        public static void Load(string fileName = ".env")
        {
            string? dir = Directory.GetCurrentDirectory();
            while (!string.IsNullOrEmpty(dir))
            {
                string path = Path.Combine(dir, fileName);
                if (File.Exists(path))
                {
                    Apply(path);
                    return;
                }

                dir = Directory.GetParent(dir)?.FullName;
            }
        }

        private static void Apply(string path)
        {
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                    continue;

                if (line.StartsWith("export ", StringComparison.OrdinalIgnoreCase))
                    line = line[7..].Trim();

                int eq = line.IndexOf('=');
                if (eq <= 0)
                    continue;

                string key = line[..eq].Trim();
                string value = Unquote(line[(eq + 1)..].Trim());

                if (string.IsNullOrEmpty(key))
                    continue;

                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                    Environment.SetEnvironmentVariable(key, value);
            }
        }

        private static string Unquote(string value)
        {
            if (value.Length >= 2)
            {
                if ((value.StartsWith('"') && value.EndsWith('"')) ||
                    (value.StartsWith('\'') && value.EndsWith('\'')))
                    return value[1..^1];
            }

            return value;
        }
    }
}
