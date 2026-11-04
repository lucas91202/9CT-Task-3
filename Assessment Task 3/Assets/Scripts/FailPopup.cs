using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FailPopup : MonoBehaviour
{
    [Header("UI refs")]
    public GameObject popupRoot;
    public RectTransform card;
    public TMP_Text titleText;

    [Header("content")]
    public string[] FailTitles = {"Slaughtered", "Fail", "Retry Mission?"};
    public KeyCode retrykey = KeyCode.R;

    [Header("animation")]
    public float showTime = .5f;
    public float slidefrom = 400f;

    CanvasGroup group;
    Vector2 restPos;
    bool shown;

    void Awake()
    {
        if (popupRoot == null || card == null)
        {
            Debug.LogWarning("Popup Failed to load");
        }
        group = popupRoot.GetComponent<CanvasGroup>();
        if (group == null) group = popupRoot.AddComponent<CanvasGroup>();
        restPos = card.anchoredPosition;
        popupRoot.SetActive(false);
    }

    void Update()
    {
        if (shown && Input.GetKeyDown(retrykey)) Retry();
    }

    public void show()
    {
        if (shown || popupRoot == null || card == null) return;
        shown = true;
        if (titleText != null && FailTitles.Length > 0) titleText.text = FailTitles[Random.Range(0, FailTitles.Length)];
        Time.timeScale = 0f;
        popupRoot.SetActive(true);
        StartCoroutine(animate());
    }
    IEnumerator animate()
    {
        group.alpha = 0f;
        group.interactable = false;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / showTime;
            float e = Mathf.Clamp01(t);
            group.alpha = e;
            card.anchoredPosition = restPos + Vector2.up * slidefrom * (1f - EaseOutBack(e));
            yield return null;
        }
        group.alpha = 1f;
        card.anchoredPosition = restPos;
        group.interactable = true;
    }
    float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x -1f, 2f);
    }
    public void Retry()
    {
        Time.timeScale = 1f;
        #if UNITY_EDITOR 
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

}
// AHHHHHHHHHHHHHHHHH - pradhyot.N 23/10/2026