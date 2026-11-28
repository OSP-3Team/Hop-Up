using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 효과음 재생 도우미. Assets/Audio/Resources/SFX/&lt;폴더&gt; 안의 클립 중 하나를 무작위로 재생.
/// 인스펙터 연결 없이 폴더 이름만으로 사용 (예: Sfx.Play("PlatformPlace", pos)).
/// </summary>
public static class Sfx
{
    private static readonly Dictionary<string, AudioClip[]> cache = new Dictionary<string, AudioClip[]>();

    // 첫 재생 때 불러오느라 소리가 늦게 나지 않도록 게임 시작 시 전부 미리 로드.
    // static으로 잡아 둬야 씬 전환(UnloadUnusedAssets) 때 해제되지 않음
    private static AudioClip[] preloaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Preload()
    {
        preloaded = Resources.LoadAll<AudioClip>("SFX");
    }

    public static void Play(string folder, Vector3 position, float volume = 1f)
    {
        AudioClip clip = Pick(folder);
        if (clip != null) AudioSource.PlayClipAtPoint(clip, position, volume);
    }

    /// <summary>폴더 안 클립 중 하나를 무작위로 반환 (없으면 null). 직접 AudioSource로 재생할 때 사용</summary>
    public static AudioClip Pick(string folder)
    {
        if (!cache.TryGetValue(folder, out AudioClip[] clips))
        {
            clips = Resources.LoadAll<AudioClip>("SFX/" + folder);
            if (clips.Length == 0) Debug.LogWarning("[Sfx] 클립 없음: SFX/" + folder);
            cache[folder] = clips;
        }

        return clips.Length == 0 ? null : clips[Random.Range(0, clips.Length)];
    }
}
