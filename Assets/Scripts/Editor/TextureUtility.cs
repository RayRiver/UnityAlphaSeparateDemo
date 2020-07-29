using UnityEditor;
using UnityEngine;

public static class TextureUtility
{
    public static bool IsTransparent(TextureImporterFormat format)
    {
        var isTransparentCompressed = format == TextureImporterFormat.ETC2_RGBA8 ||
                                      format == TextureImporterFormat.ETC2_RGBA8Crunched ||
                                      format == TextureImporterFormat.PVRTC_RGBA2 ||
                                      format == TextureImporterFormat.PVRTC_RGBA4 ||
                                      format == TextureImporterFormat.RGBA32;
        return isTransparentCompressed;
    }

    public static bool IsTransparent(TextureFormat format)
    {
        var isTransparentCompressed = format == TextureFormat.ETC2_RGBA8 ||
                                      format == TextureFormat.ETC2_RGBA8Crunched ||
                                      format == TextureFormat.PVRTC_RGBA2 ||
                                      format == TextureFormat.PVRTC_RGBA4 ||
                                      format == TextureFormat.RGBA32;
        return isTransparentCompressed;
    }

    public static TextureFormat TransparentToNoTransparentFormat(TextureFormat format)
    {
        if (format == TextureFormat.PVRTC_RGBA4)
        {
            return TextureFormat.PVRTC_RGB4;
        }
        else if (format == TextureFormat.PVRTC_RGBA2)
        {
            return TextureFormat.PVRTC_RGB2;
        }
        else if (format == TextureFormat.ETC2_RGBA8)
        {
            return format = TextureFormat.ETC_RGB4;
        }
        else if (format == TextureFormat.ETC2_RGBA8Crunched)
        {
            return TextureFormat.ETC_RGB4;
        }
        else if (format == TextureFormat.RGBA32)
        {
            return TextureFormat.RGB24;
        }
        else
        {
            return format;
        }
    }
}