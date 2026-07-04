using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class FloatingUITestControls : MonoBehaviour
{
    [SerializeField] private FloatingUIFan target;
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;

    private void Awake()
    {
        ResolveReferences();
        ConfigureButton(openButton, "FADE IN / OPEN", new Color(0.12f, 0.55f, 0.34f, 0.95f));
        ConfigureButton(closeButton, "FADE OUT / CLOSE", new Color(0.72f, 0.19f, 0.2f, 0.95f));

        if (openButton != null)
            openButton.onClick.AddListener(Open);

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
    }

    private void OnDestroy()
    {
        if (openButton != null)
            openButton.onClick.RemoveListener(Open);

        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);
    }

    public void Open()
    {
        if (target != null)
            target.Open();
    }

    public void Close()
    {
        if (target != null)
            target.Close();
    }

    private void ResolveReferences()
    {
        if (target == null)
            target = FindObjectOfType<FloatingUIFan>();

        if (openButton == null)
        {
            Transform child = transform.Find("Test_OpenButton");
            if (child != null)
                openButton = child.GetComponent<Button>();
        }

        if (closeButton == null)
        {
            Transform child = transform.Find("Test_CloseButton");
            if (child != null)
                closeButton = child.GetComponent<Button>();
        }
    }

    private static void ConfigureButton(Button button, string labelText, Color backgroundColor)
    {
        if (button == null)
            return;

        Image image = button.GetComponent<Image>();
        if (image != null)
            image.color = backgroundColor;

        Text label = button.GetComponentInChildren<Text>(true);
        if (label == null)
            return;

        label.text = labelText;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 24;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;
    }
}
