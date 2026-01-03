using System.Collections;
using UnityEngine;

namespace FF
{
    public class AirdropEventSystem : MonoBehaviour
    {
        [Header("Scheduling")]
        [SerializeField, Min(1)] private int startWave = 5;
        [SerializeField, Min(1)] private int waveInterval = 5;
        [SerializeField, Min(0f)] private float warningLeadSeconds = 3f;

        [Header("Drop")]
        [SerializeField] private WeaponCrate cratePrefab;
        [SerializeField, Min(0f)] private float dropHeight = 8f;
        [SerializeField, Min(0.1f)] private float dropSpeed = 12f;
        [SerializeField, Min(0f)] private float spawnRadius = 6f;
        [SerializeField, Min(0f)] private float minSpawnDistance = 2f;
        [SerializeField, Range(0f, 0.45f)] private float viewportPadding = 0.1f;

        [Header("Warning Popup")]
        [SerializeField] private Color warningTextColor = new(1f, 0.75f, 0.2f);
        [SerializeField, Min(0.1f)] private float warningTextScale = 1.1f;

        [Header("Audio")]
        [SerializeField] private AudioClip planeClip;
        [SerializeField, Range(0f, 1f)] private float planeVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float planeSpatialBlend = 0.35f;

        private PlayerController _player;
        private bool _gameManagerHooked;
        private Coroutine _airdropRoutine;
        private AudioSource _planeLoopSource;

        private void OnEnable()
        {
            PlayerController.OnPlayerReady += HandlePlayerReady;
            TryHookGameManager();
            GameAudioSettings.OnSfxVolumeChanged += HandleSfxVolumeChanged;
        }

        private void OnDisable()
        {
            PlayerController.OnPlayerReady -= HandlePlayerReady;
            UnhookGameManager();
            GameAudioSettings.OnSfxVolumeChanged -= HandleSfxVolumeChanged;
            StopPlaneLoop();

            if (_airdropRoutine != null)
            {
                StopCoroutine(_airdropRoutine);
                _airdropRoutine = null;
            }
        }

        private void Start()
        {
            if (!_player)
            {
                _player = FindFirstObjectByType<PlayerController>();
            }

            EnsurePlaneLoopSource();
        }

        private void HandlePlayerReady(PlayerController player)
        {
            _player = player;
        }

        private void TryHookGameManager()
        {
            if (_gameManagerHooked || GameManager.I == null)
            {
                return;
            }

            GameManager.I.OnWaveStarted += HandleWaveStarted;
            _gameManagerHooked = true;
        }

        private void UnhookGameManager()
        {
            if (!_gameManagerHooked || GameManager.I == null)
            {
                return;
            }

            GameManager.I.OnWaveStarted -= HandleWaveStarted;
            _gameManagerHooked = false;
        }

        private void HandleWaveStarted(int wave)
        {
            if (!cratePrefab)
            {
                return;
            }

            if (wave < startWave)
            {
                return;
            }

            int offset = wave - startWave;
            if (offset % Mathf.Max(1, waveInterval) != 0)
            {
                return;
            }

            if (_airdropRoutine != null)
            {
                StopCoroutine(_airdropRoutine);
            }

            _airdropRoutine = StartCoroutine(RunAirdropRoutine());
        }

        private IEnumerator RunAirdropRoutine()
        {
            if (!_player)
            {
                yield break;
            }

            ShowWarningPopup();

            if (warningLeadSeconds > 0f)
            {
                yield return new WaitForSeconds(warningLeadSeconds);
            }

            Vector3 targetPosition = ResolveDropTarget();
            StartPlaneLoop(targetPosition);
            yield return DropCrate(targetPosition);
            StopPlaneLoop();
        }

        private void ShowWarningPopup()
        {
            if (!_player)
            {
                return;
            }

            DamageNumberManager.ShowText(
                _player.transform.position,
                "AIRDROP INCOMING!",
                warningTextColor,
                warningTextScale);
        }

        private Vector3 ResolveDropTarget()
        {
            Vector3 playerPosition = _player ? _player.transform.position : transform.position;
            Camera cam = Camera.main;

            Vector2 candidate = playerPosition;
            if (spawnRadius > 0f)
            {
                Vector2 offset = Random.insideUnitCircle * spawnRadius;
                if (offset.sqrMagnitude < minSpawnDistance * minSpawnDistance)
                {
                    offset = offset.sqrMagnitude > 0.0001f
                        ? offset.normalized * minSpawnDistance
                        : Vector2.right * minSpawnDistance;
                }

                candidate = (Vector2)playerPosition + offset;
            }

            candidate = ClampToView(candidate, cam);

            if (Ground.Instance)
            {
                candidate = Ground.Instance.ClampPoint(candidate, Vector2.zero);
            }

            return candidate;
        }

        private Vector2 ClampToView(Vector2 position, Camera cam)
        {
            if (!cam)
            {
                return position;
            }

            float padding = Mathf.Clamp(viewportPadding, 0f, 0.45f);
            Vector3 min = cam.ViewportToWorldPoint(new Vector3(padding, padding, cam.nearClipPlane));
            Vector3 max = cam.ViewportToWorldPoint(new Vector3(1f - padding, 1f - padding, cam.nearClipPlane));

            position.x = Mathf.Clamp(position.x, min.x, max.x);
            position.y = Mathf.Clamp(position.y, min.y, max.y);

            return position;
        }

        private void StartPlaneLoop(Vector3 position)
        {
            if (!planeClip)
            {
                return;
            }

            EnsurePlaneLoopSource();
            if (!_planeLoopSource)
            {
                return;
            }

            _planeLoopSource.transform.position = position;
            _planeLoopSource.clip = planeClip;
            _planeLoopSource.spatialBlend = planeSpatialBlend;
            UpdatePlaneLoopVolume();
            if (!_planeLoopSource.isPlaying)
            {
                _planeLoopSource.Play();
            }
        }

        private void StopPlaneLoop()
        {
            if (_planeLoopSource && _planeLoopSource.isPlaying)
            {
                _planeLoopSource.Stop();
            }
        }

        private void EnsurePlaneLoopSource()
        {
            if (_planeLoopSource)
            {
                return;
            }

            _planeLoopSource = GetComponent<AudioSource>();
            if (!_planeLoopSource)
            {
                _planeLoopSource = gameObject.AddComponent<AudioSource>();
            }

            _planeLoopSource.playOnAwake = false;
            _planeLoopSource.loop = true;
            _planeLoopSource.ignoreListenerPause = true;
        }

        private void HandleSfxVolumeChanged(float volume)
        {
            UpdatePlaneLoopVolume();
        }

        private void UpdatePlaneLoopVolume()
        {
            if (!_planeLoopSource)
            {
                return;
            }

            _planeLoopSource.volume = Mathf.Clamp01(planeVolume * GameAudioSettings.SfxVolume);
        }

        private IEnumerator DropCrate(Vector3 targetPosition)
        {
            if (!cratePrefab)
            {
                yield break;
            }

            Vector3 startPosition = targetPosition + Vector3.up * Mathf.Max(0f, dropHeight);
            WeaponCrate crate = Instantiate(cratePrefab, startPosition, Quaternion.identity);
            if (!crate)
            {
                yield break;
            }

            if (dropSpeed <= 0f || dropHeight <= 0f)
            {
                crate.transform.position = targetPosition;
                yield break;
            }

            while (crate && Vector3.Distance(crate.transform.position, targetPosition) > 0.05f)
            {
                crate.transform.position = Vector3.MoveTowards(
                    crate.transform.position,
                    targetPosition,
                    dropSpeed * Time.deltaTime);
                yield return null;
            }

            if (crate)
            {
                crate.transform.position = targetPosition;
            }
        }

        private void OnValidate()
        {
            startWave = Mathf.Max(1, startWave);
            waveInterval = Mathf.Max(1, waveInterval);
            warningLeadSeconds = Mathf.Max(0f, warningLeadSeconds);
            dropHeight = Mathf.Max(0f, dropHeight);
            dropSpeed = Mathf.Max(0.1f, dropSpeed);
            spawnRadius = Mathf.Max(0f, spawnRadius);
            minSpawnDistance = Mathf.Max(0f, minSpawnDistance);
            viewportPadding = Mathf.Clamp(viewportPadding, 0f, 0.45f);
        }
    }
}
