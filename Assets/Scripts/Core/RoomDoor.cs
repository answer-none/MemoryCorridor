using MemoryCorridor.Media;
using UnityEngine;

namespace MemoryCorridor.Core
{
    /// <summary>
    /// 房间入口门（R4 F1/F12）：发光门框 + 牌匾（目标房间名）+ 走近触发自动切房（带淡切）。
    /// 双向对称：每个房间一个门指向另一个房间。触发后 3 秒冷却防反复横跳。
    /// </summary>
    public class RoomDoor : MonoBehaviour
    {
        [SerializeField] private int targetRoomIndex;
        [SerializeField] private string targetTitle = "另一间房";
        [SerializeField] private RoomManager roomManager;

        private float cooldownUntil;
        private bool armed = true; // R7 评测修复：OnTriggerExit 玩家离开后才重新武装——防"站着不动反复切房"循环

        public string LabelText => "前往 " + targetTitle;

        /// <summary>builder 注入。</summary>
        public void Configure(int targetRoomIndexRef, string targetTitleRef, RoomManager roomManagerRef)
        {
            targetRoomIndex = targetRoomIndexRef;
            targetTitle = targetTitleRef;
            roomManager = roomManagerRef;
            armed = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!armed) return;
            if (Time.unscaledTime < cooldownUntil) return;
            if (other.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>() == null) return;

            armed = false; // 玩家须先离开触发区（OnTriggerExit）才可再次切房——根治循环
            cooldownUntil = Time.unscaledTime + 3f;
            if (roomManager != null)
            {
                StartCoroutine(roomManager.SwitchToRoomWithFade(targetRoomIndex));
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>() == null) return;
            armed = true;
        }
    }
}
