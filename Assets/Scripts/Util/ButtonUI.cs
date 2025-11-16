using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using FF;

[RequireComponent(typeof(Button))]
public class ButtonUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Scale Settings")]
    [SerializeField] private float hoverScale = 1.1f;
    [SerializeField] private float scaleSpeed = 10f;
    [SerializeField] private AudioClip clickSound;
    [SerializeField, Range(0f, 1f)] private float clickVolume = 1f;
    [SerializeField] private Vector2 clickPitchRange = new(1f, 1f);

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
    private Button _button;
    private Coroutine _resetScaleRoutine;

    void Awake()
    {
        _initialScale = transform.localScale;
        _targetScale = 1f;

        _originalRotationZ = transform.localEulerAngles.z;

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

        _button = GetComponent<Button>();
    }
    void OnDisable()
    {
        if (_resetScaleRoutine != null)
        {
            StopCoroutine(_resetScaleRoutine);
            _resetScaleRoutine = null;
        }

        transform.localScale = _initialScale;
        _targetScale = 1f;
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
        if (clickSound)
        {
            AudioListener.pause = false;
            PlayClickAudio();
        }
        if (_resetScaleRoutine != null)
        {
            StopCoroutine(_resetScaleRoutine);
        }

        _resetScaleRoutine = StartCoroutine(ResetScaleRoutine());
    }

    IEnumerator ResetScaleRoutine()
    {
        yield return new WaitForSecondsRealtime(0.05f);
        ResetScale();
        _resetScaleRoutine = null;
    }

    void ResetScale() => _targetScale = 1f;

    void PlayClickAudio()
    {
        if (!clickSound)
        {
            return;
        }

        float minPitch = Mathf.Min(clickPitchRange.x, clickPitchRange.y);
        float maxPitch = Mathf.Max(clickPitchRange.x, clickPitchRange.y);
        float targetPitch = Mathf.Approximately(minPitch, maxPitch) ? minPitch : Random.Range(minPitch, maxPitch);
        float volumeScale = Mathf.Clamp01(clickVolume);
        SoundManager.PlaySfx(clickSound, volumeScale, targetPitch);
    }
}
