
using System.Collections.Generic;

public static class CustomAtlasConfig
{
    public class AtlasConfig
    {
        public bool KeepAlpha = false;
        public bool UseOutputAtlasTexture = false;
    }

    private static Dictionary<string, AtlasConfig> s_configs = new Dictionary<string, AtlasConfig>
    {
        { 
            "Normal",
            new AtlasConfig
            {
                KeepAlpha = true,
                UseOutputAtlasTexture = false,
            }
        },
        { 
            "AlphaSeparated",
            new AtlasConfig
            {
                KeepAlpha = false,
                UseOutputAtlasTexture = false,
            }
        },
        { 
            "AlphaSeparatedUseOutput",
            new AtlasConfig
            {
                KeepAlpha = false,
                UseOutputAtlasTexture = true,
            }
        },
    };

    public static bool ShouldKeepAlpha(string atlasName)
    {
        AtlasConfig config = null;
        if (s_configs.TryGetValue(atlasName, out config))
        {
            return config.KeepAlpha;
        }
        return false;
    }
    
    public static bool ShouldUseOutputAtlasTexture(string atlasName)
    {
        AtlasConfig config = null;
        if (s_configs.TryGetValue(atlasName, out config))
        {
            return config.UseOutputAtlasTexture;
        }
        return false;
    }
}
