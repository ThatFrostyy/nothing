using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ButtonUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Scale Settings")]
    [SerializeField] private float hoverScale = 1.1f;
    [SerializeField] private float scaleSpeed = 10f;
    [SerializeField] private AudioClip clickSound;

    [Header("Outline Settings")]
    [SerializeField] private Color normalOutlineColor = new(0, 0, 0, 1f);
    [SerializeField] private Color hoverOutlineColor = new(1f, 0.84f, 0f, 1f); // Gold
    [SerializeField] private float outlineLerpSpeed = 10f;

    [Header("Random Rotation Settings")]
    [SerializeField] private float maxRotationOffset = 2f; // degrees

    private Vector3 _initialScale;
    private float _targetScale;
    private Outline _outline;
    private Color _targetOutlineColor;
    private float _originalRotationZ;
    private AudioSource _audioSource;

    void Awake()
    {
        _initialScale = transform.localScale;
        _targetScale = 1f;

        _originalRotationZ = transform.localEulerAngles.z;

        _audioSource = GetComponent<AudioSource>();
        if (!_audioSource)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }

        if (_audioSource)
        {
            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
            _audioSource.spatialBlend = 0f;
            _audioSource.ignoreListenerPause = true;
        }

        float randomAngle = Random.Range(-maxRotationOffset, maxRotationOffset);
        transform.localRotation = Quaternion.Euler(0, 0, _originalRotationZ + randomAngle);

        _outline = GetComponent<Outline>();
        if (_outline == null)
        {
            _outline = gameObject.AddComponent<Outline>();
            _outline.effectDistance = new Vector2(2f, -2f);
        }

        _outline.effectColor = normalOutlineColor;
        _targetOutlineColor = normalOutlineColor;
    }

    void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, _initialScale * _targetScale, Time.unscaledDeltaTime * scaleSpeed);

        if (_outline != null)
        {
            _outline.effectColor = Color.Lerp(_outline.effectColor, _targetOutlineColor, Time.unscaledDeltaTime * outlineLerpSpeed);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _targetScale = hoverScale;
        _targetOutlineColor = hoverOutlineColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _targetScale = 1f;
        _targetOutlineColor = normalOutlineColor;
    }

    public void OnClick()
    {
        _targetScale = 0.95f;
        if (_audioSource && clickSound)
        {
            _audioSource.PlayOneShot(clickSound);
        }
        Invoke(nameof(ResetScale), 0.05f);
    }

    void ResetScale() => _targetScale = 1f;
}
