using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WeaponUpgrades.UI
{
    public class WeaponTreeUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject rootPanel;
        [SerializeField] private RectTransform nodesParent;
        [SerializeField] private WeaponTreeNodeUI nodePrefab;
        [SerializeField] private Image connectionLinePrefab;
        [SerializeField] private Weapon currentWeapon;

        private readonly Dictionary<string, WeaponTreeNodeUI> nodeLookup = new Dictionary<string, WeaponTreeNodeUI>();
        private WeaponUpgradeManager upgradeManager;

        private void Awake()
        {
            upgradeManager = WeaponUpgradeManager.Instance;
            if (rootPanel != null)
            {
                rootPanel.SetActive(false);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.U))
            {
                Toggle();
            }
        }

        public void SetCurrentWeapon(Weapon weapon)
        {
            currentWeapon = weapon;
            if (rootPanel != null && rootPanel.activeSelf)
            {
                BuildTree();
            }
        }

        public void Toggle()
        {
            if (rootPanel == null)
            {
                return;
            }

            if (rootPanel.activeSelf)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        public void Open()
        {
            if (rootPanel == null || currentWeapon == null || currentWeapon.UpgradeTree == null)
            {
                return;
            }

            BuildTree();
            rootPanel.SetActive(true);
        }

        public void Close()
        {
            if (rootPanel == null)
            {
                return;
            }

            rootPanel.SetActive(false);
            ClearTree();
        }

        private void BuildTree()
        {
            ClearTree();
            upgradeManager ??= WeaponUpgradeManager.Instance;
            if (currentWeapon == null || currentWeapon.UpgradeTree == null || nodePrefab == null)
            {
                return;
            }

            foreach (var node in currentWeapon.UpgradeTree.Nodes)
            {
                var uiNode = Instantiate(nodePrefab, nodesParent);
                uiNode.name = $"Node_{node.Id}";
                uiNode.RectTransform.anchoredPosition = node.UiPosition;
                uiNode.SetIcon(node.Icon);

                var state = GetNodeState(node);
                uiNode.SetState(state);

                uiNode.SetOnClick(() =>
                {
                    if (upgradeManager.TryUnlockNode(currentWeapon, node))
                    {
                        RefreshStates();
                    }
                });

                nodeLookup[node.Id] = uiNode;
            }

            foreach (var node in currentWeapon.UpgradeTree.Nodes)
            {
                foreach (var prereqId in node.PrerequisiteIds)
                {
                    if (nodeLookup.TryGetValue(node.Id, out var child) && nodeLookup.TryGetValue(prereqId, out var parent))
                    {
                        CreateConnection(parent.RectTransform, child.RectTransform);
                    }
                }
            }
        }

        private void RefreshStates()
        {
            foreach (var node in currentWeapon.UpgradeTree.Nodes)
            {
                if (nodeLookup.TryGetValue(node.Id, out var uiNode))
                {
                    uiNode.SetState(GetNodeState(node));
                }
            }
        }

        private NodeState GetNodeState(WeaponUpgradeNode node)
        {
            if (upgradeManager == null || currentWeapon == null)
            {
                return NodeState.Locked;
            }

            if (upgradeManager.IsNodeUnlocked(currentWeapon, node.Id))
            {
                return NodeState.Unlocked;
            }

            foreach (var prereq in node.PrerequisiteIds)
            {
                if (!upgradeManager.IsNodeUnlocked(currentWeapon, prereq))
                {
                    return NodeState.Locked;
                }
            }

            return upgradeManager.GetUpgradePoints(currentWeapon) > 0 ? NodeState.Available : NodeState.Locked;
        }

        private void ClearTree()
        {
            foreach (Transform child in nodesParent)
            {
                Destroy(child.gameObject);
            }

            nodeLookup.Clear();
        }

        private void CreateConnection(RectTransform from, RectTransform to)
        {
            if (from == null || to == null)
            {
                return;
            }

            var lineImage = connectionLinePrefab != null
                ? Instantiate(connectionLinePrefab, nodesParent)
                : new GameObject("Connection", typeof(RectTransform), typeof(Image)).GetComponent<Image>();

            var rect = lineImage.rectTransform;
            rect.SetParent(nodesParent, false);

            var start = from.anchoredPosition;
            var end = to.anchoredPosition;
            var direction = end - start;
            var length = direction.magnitude;

            rect.sizeDelta = new Vector2(length, lineImage.rectTransform.sizeDelta.y == 0 ? 4f : lineImage.rectTransform.sizeDelta.y);
            rect.anchoredPosition = start + direction * 0.5f;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }
    }
}
