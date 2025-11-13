using UnityEngine;

public class Ground : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private float followSpeed = 5f;

    void LateUpdate()
    {
        if (!player) return;

        Vector3 targetPos = new(player.position.x, player.position.y, transform.position.z);
        transform.position = Vector3.Lerp(transform.position, targetPos, followSpeed * Time.deltaTime);
    }
}
