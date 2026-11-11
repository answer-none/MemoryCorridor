using System;
using UnityEngine;

namespace MemoryCorridor.Media
{
    /// <summary>星河墙上的星点：绑定一条碎片，被射线选中即回调（星河回顾墙的最小单元）。</summary>
    public class StarNode : MonoBehaviour
    {
        public string FragmentId;

        public event Action<string> OnSelected;

        public void Select()
        {
            if (OnSelected != null) OnSelected(FragmentId);
        }
    }
}
