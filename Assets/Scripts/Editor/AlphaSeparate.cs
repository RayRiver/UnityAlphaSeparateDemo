
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Sprites;
using UnityEngine;

public class AlphaSeparate
{
    private const string TempAlphaTexturePath = "__TempTextures";
    
    public class SpriteEntry
    {
        public string Path;               // sprite文件路径
        public Sprite Sprite;             // sprite
        public TextureImporter Importer;  // 纹理importer
        public Texture Texture;           // 纹理
        public string AtlasName;          // 图集名称
        public Vector2[] Uvs;             // 散图uv
        public Vector2[] AtlasUvs;        // 图集uv
        public Texture2D AtlasTexture;    // 图集纹理
        public Texture2D AlphaTexture;    // 透明通道纹理

        // 备份数据，用于恢复
        public Rect OriginTextureRect;
        public int OriginSettingsRaw;
    }

    public class AtlasEntry
    {
        public string Name;                     // 图集纹理名
        public Texture2D Texture;               // 图集纹理
        public List<SpriteEntry> SpriteEntries; // 引用到的sprite项
        public bool NeedSeparateAlpha;          // 是否需要分离alpha通道
        public Texture2D OutputTexture;         // 输出到文件的原图纹理
        public string OutputTextureAssetPath;   // 输出到文件的原图纹理文件路径
        public Texture2D AlphaTexture;          // 输出到文件的透明通道纹理
        public string AlphaTextureAssetPath;    // 输出到文件的透明通道纹理文件路径
    }
    
    private SpritePackerMode m_originSpritePackerMode;
    private List<SpriteEntry> m_spriteEntries;
    private List<AtlasEntry> m_atlasEntries;

    private static AlphaSeparate s_instance;
    
    public static void Perform(BuildTarget buildTarget)
    {
        Revert();
        s_instance = new AlphaSeparate();
        s_instance.DoAlphaSeparate(buildTarget);
    }

    public static void Revert()
    {
        if (s_instance != null)
        {
            s_instance.DoRevert();
            s_instance = null;
        }
    }
    
    private void DoAlphaSeparate(BuildTarget buildTarget)
    {
        m_originSpritePackerMode = EditorSettings.spritePackerMode;
        
        m_spriteEntries = new List<SpriteEntry>();
        m_atlasEntries = new List<AtlasEntry>();
        
        // 刷新图集缓存
        UpdateAtlases(buildTarget);
        
        // 找到所有要处理的项
        FindAllEntries(buildTarget, m_spriteEntries, m_atlasEntries);
        
        // 生成alpha纹理
        GenerateAlphaTextures(m_atlasEntries);
        
        // 保存纹理到文件
        SaveTextureAssets(m_atlasEntries);
        
        // 刷新资源
        AssetDatabase.Refresh();
        
        // 从文件中加载alpha纹理
        ReloadTextures(m_atlasEntries);
        
        // 修改所有sprite的Render Data
        WriteSpritesRenderData(m_atlasEntries);
        
        // 禁用SpritePacker准备打包
        EditorSettings.spritePackerMode = SpritePackerMode.Disabled;
    }
    
    private void UpdateAtlases(BuildTarget buildTarget)
    {
        EditorSettings.spritePackerMode = SpritePackerMode.AlwaysOn;
        Packer.SelectedPolicy = typeof(CustomSpritePackerPolicy).Name;
        Packer.RebuildAtlasCacheIfNeeded(buildTarget, true,
            Packer.Execution.ForceRegroup);
    }

    private void FindAllEntries(BuildTarget buildTarget, 
        List<SpriteEntry> spriteEntries, List<AtlasEntry> atlasEntries)
    {
        var platformString = GetPlatformString(buildTarget);
        
        foreach (var guid in AssetDatabase.FindAssets("t:Texture"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                continue;
            }
            
            // 获取sprite列表
            var sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(path)
                .Distinct()
                .OfType<Sprite>()
                .Where(x => x.packed)
                .ToArray();
            for (var i = 0; i < sprites.Length; ++i)
            {
                var sprite = sprites[i];
                Texture2D atlasTexture;
                string atlasName;
                Packer.GetAtlasDataForSprite(sprite, out atlasName, out atlasTexture);
                
                if (atlasTexture != null)
                {
                    var entry = new SpriteEntry
                    {
                        Path = path,
                        Sprite = sprite,
                        Importer = importer,
                        Texture = SpriteUtility.GetSpriteTexture(sprite, false),
                        AtlasName = atlasName,
                        Uvs = SpriteUtility.GetSpriteUVs(sprite, false),
                        AtlasUvs = SpriteUtility.GetSpriteUVs(sprite, true),
                        AtlasTexture = atlasTexture,
                    };
                    spriteEntries.Add(entry);
                }
            }
        }

        // 获取atlas列表
        var atlasGroups =
            from e in spriteEntries
            group e by e.AtlasTexture;
        foreach (var atlasGroup in atlasGroups)
        {
            var tex = atlasGroup.Key;
            var texName = tex.name;
            
            // 检查是否需要分离alpha通道
            var atlasName = string.Empty;
            var needSeparateAlpha = false;
            foreach (var spriteEntry in atlasGroup)
            {
                var importer = spriteEntry.Importer;
                atlasName = importer.spritePackingTag;
                if (!string.IsNullOrEmpty(atlasName))
                {
                    var settings = importer.GetPlatformTextureSettings(platformString);
                    var format = settings.format;
                    if (format == TextureImporterFormat.Automatic)
                    {
                        format = importer.GetAutomaticFormat(platformString);
                    }
                    needSeparateAlpha = TextureUtility.IsTransparent(format);
                }
            }
            
            if (CustomAtlasConfig.ShouldKeepAlpha(atlasName))
            {
                needSeparateAlpha = false;
            }
            
            var entry = new AtlasEntry
            {
                Name = texName,
                Texture = tex,
                SpriteEntries = atlasGroup.ToList(),
                NeedSeparateAlpha = needSeparateAlpha,
            };
            atlasEntries.Add(entry);
        }
    }

    private void GenerateAlphaTextures(List<AtlasEntry> atlasEntries)
    {
        if (atlasEntries == null) return;
        
        // alpha贴图默认材质
        var mat = new Material(Shader.Find("Unlit/Transparent"));
        
        foreach (var atlasEntry in atlasEntries)
        {
            var texWidth = atlasEntry.Texture.width;
            var texHeight = atlasEntry.Texture.height;
            
            // 拷贝原始纹理（用于测试，原Packer的纹理不可读）
            try
            {
                atlasEntry.Texture.GetPixels32();
            }
            catch (UnityException e)
            {
                Debug.Log("GetPixel32 not allowed: " + e.Message + ", " + e.StackTrace);

                var tex = atlasEntry.Texture;
                var originFilterMode = tex.filterMode;
                tex.filterMode = FilterMode.Point;
                var rt = RenderTexture.GetTemporary(texWidth, texHeight);
                rt.filterMode = FilterMode.Point;

                var activeRt = RenderTexture.active;
                RenderTexture.active = rt;
                Graphics.Blit(tex, rt);

                var finalTex = new Texture2D(texWidth, texHeight);
                finalTex.ReadPixels(new Rect(0, 0, texWidth, texHeight), 0, 0);
                finalTex.Apply();

                RenderTexture.active = activeRt;
                tex.filterMode = originFilterMode;

                atlasEntry.OutputTexture = finalTex;
            }

            // 生成alpha通道纹理
            if (atlasEntry.NeedSeparateAlpha)
            {
                // 临时渲染贴图
                var rt = RenderTexture.GetTemporary(texWidth, texHeight,
                    0, RenderTextureFormat.ARGB32);
                Graphics.SetRenderTarget(rt);
                GL.Clear(true, true, Color.clear);
                GL.PushMatrix();
                GL.LoadOrtho();

                foreach (var spriteEntry in atlasEntry.SpriteEntries)
                {
                    var sprite = spriteEntry.Sprite;
                    var uvs = spriteEntry.Uvs;
                    var atlasUvs = spriteEntry.AtlasUvs;

                    // 将压缩前sprite的顶点信息渲染到临时贴图上
                    mat.mainTexture = spriteEntry.Texture;
                    mat.SetPass(0);
                    GL.Begin(GL.TRIANGLES);
                    var triangles = sprite.triangles;
                    foreach (var index in triangles)
                    {
                        GL.TexCoord(uvs[index]);
                        GL.Vertex(atlasUvs[index]);
                    }

                    GL.End();
                }

                GL.PopMatrix();

                // 最终的alpha贴图
                var finalTex = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
                finalTex.ReadPixels(new Rect(0, 0, texWidth, texHeight), 0, 0);

                // 修改颜色
                var colors = finalTex.GetPixels32();
                var count = colors.Length;
                var newColors = new Color32[count];
                for (var i = 0; i < count; ++i)
                {
                    var a = colors[i].a;
                    newColors[i] = new Color32(a, a, a, 255);
                }

                finalTex.SetPixels32(newColors);
                finalTex.Apply();

                RenderTexture.ReleaseTemporary(rt);

                // 更新AtlasEntry
                atlasEntry.AlphaTexture = finalTex;
            }
        } 
    }

    private void SaveTextureAssets(List<AtlasEntry> atlasEntries)
    {
        if (atlasEntries == null) return;
        
        // 创建临时目录
        var path = Path.Combine(Application.dataPath, TempAlphaTexturePath);
        var assetPath = Path.Combine("Assets", TempAlphaTexturePath);
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);                
        }
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        
        // 写入文件
        foreach (var atlasEntry in atlasEntries)
        {
            // 支持多page图集
            var hashCode = atlasEntry.Texture.GetHashCode();
                
            // 导出原纹理（测试用）
            {
                var fileName = atlasEntry.Name + "_" + hashCode + ".png";
                var filePath = Path.Combine(path, fileName);
                File.WriteAllBytes(filePath, atlasEntry.OutputTexture.EncodeToPNG());
                atlasEntry.OutputTextureAssetPath = Path.Combine(assetPath, fileName);
            }

            // 导出alpha纹理
            if (atlasEntry.NeedSeparateAlpha)
            {
                var fileName = atlasEntry.Name + "_" + hashCode + "_alpha.png";
                var filePath = Path.Combine(path, fileName);
                File.WriteAllBytes(filePath, atlasEntry.AlphaTexture.EncodeToPNG());
                atlasEntry.AlphaTextureAssetPath = Path.Combine(assetPath, fileName);
            }
        }
    }

    private void ReloadTextures(List<AtlasEntry> atlasEntries)
    {
        if (atlasEntries == null) return;
        
        // 从文件中读取原始和alpha贴图
        foreach (var atlasEntry in atlasEntries)
        {
            if (!atlasEntry.NeedSeparateAlpha)
            {
                continue;
            }
            
            // 读取原始贴图
            {
                var assetPath = atlasEntry.OutputTextureAssetPath;
                
//                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
//                if (importer == null)
//                {
//                    Debug.LogError("load alpha importer failed: " + assetPath);
//                }
//                else
//                {
//                    importer.textureCompression = TextureImporterCompression.Uncompressed;
//                }
                
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (texture == null)
                {
                    UnityEngine.Debug.LogError("texture = " + assetPath);
                    continue;
                }

                atlasEntry.OutputTexture = texture;
            }
            
            // 读取alpha贴图
            {
                var assetPath = atlasEntry.AlphaTextureAssetPath;

//                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
//                if (importer == null)
//                {
//                    Debug.LogError("load alpha texture importer failed: " + assetPath);
//                }
//                else
//                {
//                    importer.textureCompression = TextureImporterCompression.Uncompressed;
//                }

                var alphaTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (alphaTexture == null)
                {
                    UnityEngine.Debug.LogError("alphaTexture = " + assetPath);
                    continue;
                }

                atlasEntry.AlphaTexture = alphaTexture;
                foreach (var spriteEntry in atlasEntry.SpriteEntries)
                {
                    spriteEntry.AlphaTexture = alphaTexture;
                }
            }
        } 
    }
    
    private static void WriteSpritesRenderData(List<AtlasEntry> atlasEntries)
    {
        if (atlasEntries == null) return;
        
        foreach (var atlasEntry in atlasEntries)
        {
            foreach (var spriteEntry in atlasEntry.SpriteEntries)
            {
                var spr = spriteEntry.Sprite;
                var so = new SerializedObject(spr);

                // 是否使用导出的贴图文件
                string atlasName = spriteEntry.AtlasName;
                Texture2D atlasTexture;
                if (CustomAtlasConfig.ShouldUseOutputAtlasTexture(atlasName))
                {
                    atlasTexture = atlasEntry.OutputTexture;
                }
                else 
                {
                    atlasTexture = atlasEntry.Texture;
                }
                var alphaTexture = spriteEntry.AlphaTexture;

                // 获取散图属性
                var rect = so.FindProperty("m_Rect").rectValue;
                var pivot = so.FindProperty("m_Pivot").vector2Value;
                var pixelsToUnits = so.FindProperty("m_PixelsToUnits").floatValue;
                var tightRect = so.FindProperty("m_RD.textureRect").rectValue;
                var originSettingsRaw = so.FindProperty("m_RD.settingsRaw").intValue;
                
                // 散图(tight)在散图(full rect)中的位置和宽高
                var tightOffset = new Vector2(tightRect.x, tightRect.y);
                var tightWidth = tightRect.width;
                var tightHeight = tightRect.height;
                
                // 计算散图(full rect)在图集中的rect和offset
                var fullRectInAtlas = GetTextureFullRectInAtlas(atlasTexture,
                    spriteEntry.Uvs, spriteEntry.AtlasUvs);
                var fullRectOffsetInAtlas = new Vector2(fullRectInAtlas.x, fullRectInAtlas.y);
                
                // 计算散图(tight)在图集中的rect
                var tightRectInAtlas = new Rect(fullRectInAtlas.x + tightOffset.x,
                    fullRectInAtlas.y + tightOffset.y, tightWidth, tightHeight);
                
                // 计算uvTransform
                // x: Pixels To Unit X
                // y: 中心点在图集中的位置X
                // z: Pixels To Unit Y 
                // w: 中心点在图集中的位置Y
                var uvTransform = new Vector4(
                    pixelsToUnits,
                    rect.width * pivot.x + fullRectOffsetInAtlas.x,
                    pixelsToUnits,
                    rect.height * pivot.y + fullRectOffsetInAtlas.y);
                
                // 计算settings
                // 0位：packed。1表示packed，0表示不packed
                // 1位：SpritePackingMode。0表示tight，1表示rectangle
                // 2-3位：SpritePackingRotation。0表示不旋转，1表示水平翻转，2表示竖直翻转，3表示180度旋转，4表示90度旋转
                // 6位：SpriteMeshType。0表示full rect，1表示tight
                // 67 = SpriteMeshType(tight) + SpritePackingMode(rectangle) + packed
                var settingsRaw = 67; 

                // 写入RenderData
                so.FindProperty("m_RD.texture").objectReferenceValue = atlasTexture;
                so.FindProperty("m_RD.alphaTexture").objectReferenceValue = alphaTexture;
                so.FindProperty("m_RD.textureRect").rectValue = tightRectInAtlas;
                so.FindProperty("m_RD.textureRectOffset").vector2Value = tightOffset;
                so.FindProperty("m_RD.atlasRectOffset").vector2Value = fullRectOffsetInAtlas;
                so.FindProperty("m_RD.settingsRaw").intValue = settingsRaw;
                so.FindProperty("m_RD.uvTransform").vector4Value = uvTransform;
                so.ApplyModifiedProperties();
                
                // 备份原数据，用于恢复
                spriteEntry.OriginTextureRect = tightRect;
                spriteEntry.OriginSettingsRaw = originSettingsRaw;
            }
        }
    }

    private void DoRevert()
    {
        EditorSettings.spritePackerMode = m_originSpritePackerMode;

        if (m_atlasEntries == null) return;
        
        foreach (var atlasEntry in m_atlasEntries)
        {
            foreach (var spriteEntry in atlasEntry.SpriteEntries)
            {
                var spr = spriteEntry.Sprite;
                var so = new SerializedObject(spr);
                
                var rect = so.FindProperty("m_Rect").rectValue;
                var pivot = so.FindProperty("m_Pivot").vector2Value;
                var pixelsToUnits = so.FindProperty("m_PixelsToUnits").floatValue;
                
                so.FindProperty("m_RD.texture").objectReferenceValue = spr.texture;
                so.FindProperty("m_RD.alphaTexture").objectReferenceValue = null;
                so.FindProperty("m_RD.textureRect").rectValue = spriteEntry.OriginTextureRect;
                so.FindProperty("m_RD.textureRectOffset").vector2Value = new Vector2(
                    spriteEntry.OriginTextureRect.x, spriteEntry.OriginTextureRect.y);
                so.FindProperty("m_RD.atlasRectOffset").vector2Value = new Vector2(-1, -1);
                so.FindProperty("m_RD.settingsRaw").intValue = spriteEntry.OriginSettingsRaw;
                so.FindProperty("m_RD.uvTransform").vector4Value = new Vector4(
                    pixelsToUnits,
                    rect.width * pivot.x,
                    pixelsToUnits,
                    rect.height * pivot.y);
                so.ApplyModifiedProperties();
            }
        }
    }
    
    private static Rect GetTextureFullRectInAtlas(Texture2D atlasTexture, 
        Vector2[] uvs, Vector2[] atlasUvs)
    {
        var textureRect = new Rect();
        
        // 找到某一个x/y都不相等的点
        var index = 0;
        var count = uvs.Length;
        for (var i = 1; i < count; i++)
        {
            if (Math.Abs(uvs[i].x - uvs[0].x) > 1E-06 && 
                Math.Abs(uvs[i].y - uvs[0].y) > 1E-06)
            {
                index = i;
                break;
            }
        }

        // 计算散图在大图中的texture rect
        var atlasWidth = atlasTexture.width;
        var atlasHeight = atlasTexture.height;
        textureRect.width = (atlasUvs[0].x - atlasUvs[index].x) / (uvs[0].x - uvs[index].x) * atlasWidth;
        textureRect.height = (atlasUvs[0].y - atlasUvs[index].y) / (uvs[0].y - uvs[index].y) * atlasHeight;
        textureRect.x = atlasUvs[0].x * atlasWidth - textureRect.width * uvs[0].x;
        textureRect.y = atlasUvs[0].y * atlasHeight - textureRect.height * uvs[0].y;
        
        return textureRect;
    }

    private string GetPlatformString(BuildTarget buildTarget)
    {
        var platformString = string.Empty;
        if (buildTarget == BuildTarget.iOS)
        {
            platformString = "iPhone";
        }
        else if (buildTarget == BuildTarget.Android)
        {
            platformString = "Android";
        }
        else
        {
            platformString = "Standalone";
        }

        return platformString;
    }
}
