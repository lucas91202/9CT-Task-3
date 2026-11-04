using System.Collections;
using UnityEngine;

public class ScreenFade : MonoBehaviour
{
    public bool fadeInOnStart = false;
    public float startFadeTime = .6f;
    CanvasGroup group;
    void Awake()
    {
        Init();
        group.alpha = fadeInOnStart ? 1f : 0f;;
    }

    void Start()
    {
        if(fadeInOnStart) StartCoroutine(FadeTo(0f, startFadeTime));
    }
    void Init()
    {
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    public IEnumerator FadeTo(float target, float time)
    {
        if (group == null) Init();
        float start = group.alpha;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(time, .01f);
            group.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t));
            yield return null;
        }
        group.alpha = target;
    }
}
