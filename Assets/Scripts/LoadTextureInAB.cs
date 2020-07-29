using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LoadTextureInAB : MonoBehaviour
{
    [SerializeField] private string m_bundleName;
    [SerializeField] private string m_assetName;
    
    private static Dictionary<string, AssetBundle> s_loadedAssetBundles = new Dictionary<string, AssetBundle>();

    private void Start()
    {
        AssetBundle ab = null;
        if (s_loadedAssetBundles.ContainsKey(m_bundleName))
        {
            ab = s_loadedAssetBundles[m_bundleName];
        }
        else
        {
            ab = AssetBundle.LoadFromFile(Application.streamingAssetsPath + "/" + m_bundleName);
            s_loadedAssetBundles[m_bundleName] = ab;
        }

        var spr = ab.LoadAsset<Sprite>(m_assetName);
        
        var spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = spr;
        }

        var image = GetComponent<UnityEngine.UI.Image>();
        if (image != null)
        {
            image.sprite = spr;
            image.color = Color.white;
        }
    }
}